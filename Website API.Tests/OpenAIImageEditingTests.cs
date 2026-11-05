using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Website_API.Controllers;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class OpenAIImageEditingTests
{
    internal static byte[] Png(SKColor color, int width = 2, int height = 2)
    {
        using var bitmap = new SKBitmap(width, height); bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray();
    }
    [Fact]
    public async Task MaskedEditUsesOfficialMultipartAndPreservesOpaquePixels()
    {
        var red = Png(new SKColor(255, 0, 0));
        using var mask = new SKBitmap(2, 2); mask.Erase(new SKColor(0, 0, 0, 255));
        mask.SetPixel(0, 0, new SKColor(0, 0, 0, 0));
        using var maskImage = SKImage.FromBitmap(mask); using var maskData = maskImage.Encode(SKEncodedImageFormat.Png, 100);
        var calls = 0;
        var handler = new Handler(async (request, ct) =>
        {
            calls++;
            Assert.Equal("https://api.openai.com/v1/images/edits", request.RequestUri!.ToString());
            Assert.Equal("test-key", request.Headers.Authorization!.Parameter);
            Assert.IsType<MultipartFormDataContent>(request.Content);
            var form = await request.Content!.ReadAsStringAsync(ct);
            Assert.Contains("gpt-image-2.5-sunburst", form);
            var parts = Assert.IsType<MultipartFormDataContent>(request.Content);
            async Task<string> Field(string name) => await parts.Single(p => p.Headers.ContentDisposition?.Name?.Trim('"') == name).ReadAsStringAsync(ct);
            Assert.Equal("low", await Field("quality")); Assert.Equal("1024x1024", await Field("size"));
            Assert.Equal("1", await Field("n")); Assert.Equal("png", await Field("output_format"));
            Assert.Contains("mask.png", form);
            Assert.DoesNotContain("/Hdreal", form);
            Assert.DoesNotContain("/RemoveWaterMark", form);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { data = new[] { new { b64_json = Convert.ToBase64String(Png(new SKColor(0, 0, 255))) } } }) };
        });
        var editor = Editor(handler);
        var result = await editor.ProcessAsync(new ImageEditRequest { ImageB64 = Convert.ToBase64String(red), MaskB64 = Convert.ToBase64String(maskData.ToArray()), AuthorizationBasis = "owned" }, default);
        using var output = SKBitmap.Decode(Convert.FromBase64String(result.ProcessedB64!));
        Assert.Equal(new SKColor(0, 0, 255), output.GetPixel(0, 0));
        Assert.Equal(new SKColor(255, 0, 0), output.GetPixel(1, 0));
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(1, calls);
    }
    [Theory]
    [InlineData(401)] [InlineData(403)] [InlineData(404)] [InlineData(429)] [InlineData(500)] [InlineData(503)]
    public async Task ProviderFailuresNeverRetryOrExposeResponseBodies(int status)
    {
        var calls = 0;
        var editor = Editor(new Handler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("sensitive-provider-error") }); }));
        var error = await Assert.ThrowsAsync<ImageEditException>(() => editor.ProcessAsync(new ImageEditRequest { ImageB64 = Convert.ToBase64String(Png(new SKColor(2, 3, 4))), AuthorizationBasis = "owned" }, default));
        Assert.DoesNotContain("sensitive", error.Message);
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task CancellationDoesNotRetry()
    {
        var calls = 0;
        var editor = Editor(new Handler((_, _) => { calls++; throw new TaskCanceledException(); }));
        await Assert.ThrowsAsync<TaskCanceledException>(() => editor.ProcessAsync(new ImageEditRequest { ImageB64 = Convert.ToBase64String(Png(new SKColor(2, 3, 4))), AuthorizationBasis = "owned" }, default));
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task MissingModelAndUnauthorizedInputsNeverReachProvider()
    {
        var calls = 0;
        var handler = new Handler((_, _) => { calls++; throw new Exception("Should not call provider"); });
        var editor = Editor(handler);
        await Assert.ThrowsAsync<ArgumentException>(() => editor.ProcessAsync(new ImageEditRequest { AuthorizationBasis = "unknown" }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => editor.ProcessAsync(new ImageEditRequest { AuthorizationBasis = "owned", ImageB64 = Convert.ToBase64String("fake png"u8.ToArray()) }, default));
        var unconfigured = new OpenAIImageEditingService(new HttpClient(handler), Options.Create(new ImageAiOptions { Enabled = true }), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OPENAI_API_KEY"] = "test" }).Build());
        await Assert.ThrowsAsync<InvalidOperationException>(() => unconfigured.ProcessAsync(new ImageEditRequest(), default));
        Assert.Equal(0, calls);
    }
    [Theory]
    [InlineData("png")] [InlineData("jpeg")] [InlineData("webp")]
    public async Task AcceptedFormatsAreDecodedAndSent(string format)
    {
        using var bitmap = new SKBitmap(2, 2); bitmap.Erase(new SKColor(40, 50, 60));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format == "png" ? SKEncodedImageFormat.Png : format == "jpeg" ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Webp, 100);
        var calls = 0;
        var editor = Editor(new Handler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { data = new[] { new { b64_json = Convert.ToBase64String(Png(new SKColor(2, 3, 4))) } } }) }); }));
        await editor.ProcessAsync(new ImageEditRequest { ImageB64 = Convert.ToBase64String(data.ToArray()), AuthorizationBasis = "owned", Mode = "enhance" }, default);
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task JpegExifRotationIsNormalizedBeforeEditing()
    {
        using var bitmap = new SKBitmap(2, 3); bitmap.Erase(new SKColor(40, 50, 60));
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        var jpeg = data.ToArray();
        // EXIF IFD0 Orientation=6: rotate 90 degrees clockwise for display.
        byte[] exif = [255, 225, 0, 34, 69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0, 1, 0, 18, 1, 3, 0, 1, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0];
        var photo = jpeg[..2].Concat(exif).Concat(jpeg[2..]).ToArray();
        var editor = Editor(new Handler(async (request, ct) =>
        {
            var form = Assert.IsType<MultipartFormDataContent>(request.Content);
            var input = form.Single(part => part.Headers.ContentDisposition?.Name?.Trim('"') == "image");
            using var normalized = SKBitmap.Decode(await input.ReadAsByteArrayAsync(ct));
            Assert.Equal(3, normalized.Width); Assert.Equal(2, normalized.Height);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { data = new[] { new { b64_json = Convert.ToBase64String(Png(new SKColor(2, 3, 4))) } } }) };
        }));
        var result = await editor.ProcessAsync(new ImageEditRequest { ImageB64 = Convert.ToBase64String(photo), AuthorizationBasis = "owned", Mode = "enhance" }, default);
        Assert.Equal(3, result.Width); Assert.Equal(2, result.Height);
    }

    [Fact]
    public async Task InvalidMasksNeverReachProvider()
    {
        var editor = Editor(new Handler((_, _) => throw new Exception("Should not call")));
        var photo = Convert.ToBase64String(Png(new SKColor(2, 3, 4)));
        foreach (var bytes in new[] { Png(new SKColor(0, 0, 0, 255)), Png(new SKColor(0, 0, 0, 0), 1, 1) })
            await Assert.ThrowsAsync<ArgumentException>(() => editor.ProcessAsync(new ImageEditRequest { ImageB64 = photo, MaskB64 = Convert.ToBase64String(bytes), AuthorizationBasis = "owned" }, default));
    }

    [Fact]
    public async Task HttpUploadPreviewApprovalDownloadStorageAndDuplicatesAreIntegrated()
    {
        await using var f = await Fixture.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/image-processing/availability")).StatusCode);
        f.As("Agent");
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Upload()).StatusCode);
        f.As("Admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Upload(rights: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Upload(data: "not-an-image"u8.ToArray())).StatusCode);
        Assert.Equal(0, f.ProviderCalls);
        var response = await f.Upload(); response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = result.GetProperty("jobId").GetInt64();
        Assert.Equal("image/png", result.GetProperty("contentType").GetString());
        Assert.Equal(1, f.ProviderCalls);
        Assert.Equal(2, f.Objects.Count);
        using (var scope = f.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var job = await db.ImageProcessingJobs.SingleAsync();
            Assert.Equal(ImageJobStatus.AwaitingApproval, job.Status);
            Assert.Equal(0.018021m, job.EstimatedCostUsd);
            Assert.Empty(await db.ApartmentImages.ToListAsync()); // No automatic publishing or listing mutation.
        }
        var download = await f.Client.GetAsync($"/api/image-processing/jobs/{id}/download"); download.EnsureSuccessStatusCode();
        Assert.Equal(Convert.FromBase64String(result.GetProperty("cleanedImage").GetString()!), await download.Content.ReadAsByteArrayAsync());
        var original = await f.Client.GetAsync($"/api/image-processing/jobs/{id}/download?original=true"); original.EnsureSuccessStatusCode();
        Assert.Equal(f.Photo, await original.Content.ReadAsByteArrayAsync());
        (await f.Upload()).EnsureSuccessStatusCode(); Assert.Equal(1, f.ProviderCalls);
        f.As("Admin", "other-admin");
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.PostAsync($"/api/image-processing/jobs/{id}/approve-draft", null)).StatusCode);
        f.As("Admin");
        (await f.Client.PostAsync($"/api/image-processing/jobs/{id}/approve-draft", null)).EnsureSuccessStatusCode();
        using (var scope = f.App.Services.CreateScope()) Assert.Equal(ImageJobStatus.Approved, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().ImageProcessingJobs.SingleAsync()).Status);
        Assert.Equal(2, f.Objects.Count); // Original remains in storage after approval.
    }
    [Fact]
    public async Task FailedRequestsReserveCostAndPreventDuplicatePaidCalls()
    {
        await using var f = await Fixture.Start(); f.As("Admin"); f.FailProvider = true;
        Assert.Equal(HttpStatusCode.BadGateway, (await f.Upload()).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Upload()).StatusCode);
        Assert.Equal(1, f.ProviderCalls);
        using var scope = f.App.Services.CreateScope();
        var job = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ImageProcessingJobs.SingleAsync();
        Assert.Equal(ImageJobStatus.Failed, job.Status); Assert.Equal(0.036042m, job.EstimatedCostUsd);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsync($"/api/image-processing/jobs/{job.Id}/retry", null)).StatusCode);
    }
    [Fact]
    public async Task TemporaryProviderFailureCanBeRetriedOnlyByExplicitAcknowledgedMatchingAction()
    {
        await using var f = await Fixture.Start(); f.As("Admin"); f.FailProvider = true; f.FailureStatus = HttpStatusCode.ServiceUnavailable;
        var failure = await f.Upload(); Assert.Equal(HttpStatusCode.BadGateway, failure.StatusCode);
        var error = await failure.Content.ReadFromJsonAsync<JsonElement>();
        var failedId = error.GetProperty("failedJobId").GetInt64();
        Assert.Equal(503, error.GetProperty("providerStatus").GetInt32());
        Assert.Contains("temporarily unavailable", error.GetProperty("message").GetString());
        Assert.Equal(1, f.ProviderCalls);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Upload()).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Upload(retryId: failedId)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Upload(retryId: failedId + 99, acknowledge: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Upload(data: Png(new SKColor(222, 3, 4)), retryId: failedId, acknowledge: true)).StatusCode);
        f.As("Admin", "different-admin");
        Assert.Equal(HttpStatusCode.Conflict, (await f.Upload(retryId: failedId, acknowledge: true)).StatusCode);
        f.As("Admin"); Assert.Equal(1, f.ProviderCalls);
        f.FailProvider = false;
        var retried = await f.Upload(retryId: failedId, acknowledge: true); retried.EnsureSuccessStatusCode();
        var succeededId = (await retried.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jobId").GetInt64();
        Assert.NotEqual(failedId, succeededId); Assert.Equal(2, f.ProviderCalls);
        // Normal resubmission returns the latest successful result; it must not keep selecting the old failure.
        var cached = await f.Upload(); cached.EnsureSuccessStatusCode();
        Assert.Equal(succeededId, (await cached.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jobId").GetInt64());
        Assert.Equal(2, f.ProviderCalls);
        using var scope = f.App.Services.CreateScope();
        var jobs = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ImageProcessingJobs.OrderBy(j => j.Id).ToListAsync();
        Assert.Equal(2, jobs.Count); Assert.Equal(ImageJobStatus.Failed, jobs[0].Status);
        Assert.Equal(0.036042m, jobs[0].EstimatedCostUsd); Assert.Contains("providerStatus", jobs[0].SafetyReportJson);
        Assert.Equal(0.018021m, jobs[1].EstimatedCostUsd);
    }

    [Fact]
    public async Task BusyAndBudgetLimitsRemainButDailyCapIsRemoved()
    {
        await using var f = await Fixture.Start(); f.As("Admin");
        f.Guard.Busy = true; Assert.Equal((HttpStatusCode)429, (await f.Upload()).StatusCode);
        f.Guard.Busy = false;
        f.Settings.BudgetCutoffUsd = 0.01m;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.Upload()).StatusCode);
        f.Settings.BudgetCutoffUsd = 8;
        Assert.Equal(0, f.ProviderCalls); Assert.Empty(f.Objects);
        using (var scope = f.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 6; i++)
                db.ImageProcessingJobs.Add(new ImageProcessingJob { RequestedByUserId = "admin", StartedAt = DateTime.UtcNow, Status = ImageJobStatus.Failed, PipelineVersion = "openai-edit-v1:earlier-settings", EstimatedCostUsd = 0.02m });
            await db.SaveChangesAsync();
        }
        (await f.Upload()).EnsureSuccessStatusCode();
        Assert.Equal(1, f.ProviderCalls); Assert.Equal(2, f.Objects.Count);
        f.Settings.MonthlyImageQuota = 7;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.Upload(data: Png(new SKColor(220, 10, 10)))).StatusCode);
        Assert.Equal(1, f.ProviderCalls); // The monthly quota still blocks additional spend.
    }
    [Fact]
    public async Task StorageFailureNeverPublishesAndNeverRetriesProvider()
    {
        await using var f = await Fixture.Start(); f.As("Admin"); f.FailDraftStorage = true;
        Assert.Equal(HttpStatusCode.BadGateway, (await f.Upload()).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Upload()).StatusCode);
        Assert.Equal(1, f.ProviderCalls); Assert.Single(f.Objects);
        using var scope = f.App.Services.CreateScope();
        var job = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ImageProcessingJobs.SingleAsync();
        Assert.Equal(0.018021m, job.EstimatedCostUsd); // API usage remains recorded even if storing its output fails.
        Assert.Equal("usage", OpenAIImageCostCalculator.ForJob(job.SafetyReportJson, job.EstimatedCostUsd).Basis);
    }

    [Fact]
    public async Task MissingUsageKeepsReservationAndUnknownPricingBlocksBeforeProvider()
    {
        await using var f = await Fixture.Start(); f.As("Admin"); f.OmitUsage = true;
        (await f.Upload()).EnsureSuccessStatusCode();
        using (var scope = f.App.Services.CreateScope())
        {
            var job = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ImageProcessingJobs.SingleAsync();
            Assert.Equal(0.036042m, job.EstimatedCostUsd);
            Assert.Equal("reservation", OpenAIImageCostCalculator.ForJob(job.SafetyReportJson, job.EstimatedCostUsd).Basis);
        }
        f.Settings.Model = "new-unpriced-model";
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.Upload()).StatusCode);
        Assert.Equal(1, f.ProviderCalls);
    }
    [Fact]
    public async Task TrialBudgetExcludesModalHistoryAndRetainsOpenAIReservationsAcrossSettings()
    {
        await using var f = await Fixture.Start(); f.As("Admin");
        using (var scope = f.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var index = 0; index < 30; index++)
                db.ImageProcessingJobs.Add(new ImageProcessingJob { RequestedByUserId = "admin", StartedAt = DateTime.UtcNow,
                    PipelineVersion = index % 2 == 0 ? null : "modal-legacy-version", EstimatedCostUsd = 10, GpuSeconds = 100, Status = ImageJobStatus.Failed });
            db.ImageProcessingJobs.Add(new ImageProcessingJob { RequestedByUserId = "admin", StartedAt = DateTime.UtcNow,
                PipelineVersion = "openai-edit-v1:previous-model-settings", EstimatedCostUsd = 0.2m, Status = ImageJobStatus.Failed });
            await db.SaveChangesAsync();
            var budget = await scope.ServiceProvider.GetRequiredService<ImageProcessingQueue>().GetBudgetAsync(default);
            Assert.Equal(1, budget.ImagesProcessed); Assert.Equal(0.2m, budget.SpentUsd);
            Assert.Equal(0, budget.GpuSeconds); Assert.False(budget.Exhausted);
            Assert.Equal(31, await db.ImageProcessingJobs.CountAsync());
        }
        // Historical Modal work must not exhaust daily limits or block a new authorized upload.
        (await f.Upload()).EnsureSuccessStatusCode(); Assert.Equal(1, f.ProviderCalls);
        using (var scope = f.App.Services.CreateScope())
        {
            var budget = await scope.ServiceProvider.GetRequiredService<ImageProcessingQueue>().GetBudgetAsync(default);
            Assert.Equal(2, budget.ImagesProcessed); Assert.Equal(0.218021m, budget.SpentUsd);
            Assert.Equal(32, await scope.ServiceProvider.GetRequiredService<AppDbContext>().ImageProcessingJobs.CountAsync());
        }
    }

    [Fact]
    public async Task WorkerRequiresExplicitJobsAndNeverRetriesFailedOrInterruptedCalls()
    {
        await using var f = await Fixture.Start();
        f.Objects["/storage/v1/object/apartments/original.png"] = f.Photo;
        f.As("Admin");
        long workerJobId;
        using (var scope = f.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ApartmentImages.Add(new ApartmentImage { Id = 42, ApartmentId = 99, StoragePath = "original.png", IsCover = true,
                Apartment = new Apartment { Id = 99, ImageUrl = "original.png" } });
            db.ImageProcessingJobs.Add(new ImageProcessingJob { SourceStoragePath = "original.png", ApartmentImageId = 42,
                RequestedByUserId = "admin", Mode = "enhance", AuthorizationBasis = ImageAuthorizationBasis.Owned,
                PipelineVersion = OpenAIImageEditingService.BuildCacheVersion(f.Settings) });
            db.ImageProcessingJobs.Add(new ImageProcessingJob { Mode = "detect", SourceStoragePath = "original.png" }); // Legacy work stays untouched.
            await db.SaveChangesAsync();
        }
        var worker = new ImageProcessingWorker(f.App.Services.GetRequiredService<IServiceScopeFactory>(), Options.Create(f.Settings), Microsoft.Extensions.Logging.Abstractions.NullLogger<ImageProcessingWorker>.Instance);
        await worker.RunOnceAsync(default);
        Assert.Equal(1, f.ProviderCalls);
        using (var scope = f.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var completed = await db.ImageProcessingJobs.SingleAsync(j => j.Mode == "enhance");
            workerJobId = completed.Id;
            Assert.Equal(ImageJobStatus.AwaitingApproval, completed.Status);
            Assert.Equal("original.png", (await db.ApartmentImages.SingleAsync()).StoragePath);
            Assert.Equal(ImageJobStatus.Pending, (await db.ImageProcessingJobs.SingleAsync(j => j.Mode == "detect")).Status);
            db.ImageProcessingJobs.Add(new ImageProcessingJob { SourceStoragePath = "original.png", ApartmentImageId = 42,
                RequestedByUserId = "admin", Mode = "process", AuthorizationBasis = ImageAuthorizationBasis.Owned,
                PipelineVersion = OpenAIImageEditingService.BuildCacheVersion(f.Settings) });
            await db.SaveChangesAsync();
        }
        (await f.Client.PostAsync($"/api/image-processing/jobs/{workerJobId}/approve", null)).EnsureSuccessStatusCode();
        using (var scope = f.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var image = await db.ApartmentImages.SingleAsync();
            Assert.Equal("original.png", image.OriginalStoragePath);
            Assert.NotEqual("original.png", image.StoragePath);
            Assert.Equal(image.StoragePath, (await db.Apartments.SingleAsync()).ImageUrl);
        }
        (await f.Client.PostAsync($"/api/image-processing/jobs/{workerJobId}/revert", null)).EnsureSuccessStatusCode();
        using (var scope = f.App.Services.CreateScope())
            Assert.Equal("original.png", (await scope.ServiceProvider.GetRequiredService<AppDbContext>().ApartmentImages.SingleAsync()).StoragePath);
        f.FailProvider = true;
        await worker.RunOnceAsync(default);
        await worker.RunOnceAsync(default);
        Assert.Equal(2, f.ProviderCalls);
        using (var scope = f.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var failed = await db.ImageProcessingJobs.SingleAsync(j => j.Mode == "process");
            Assert.Equal(ImageJobStatus.Failed, failed.Status); Assert.Equal(0.036042m, failed.EstimatedCostUsd);
            Assert.Equal(1, failed.Attempts);
        }
    }

    [LocalPostgresFact]
    public async Task PostgreSqlAdvisoryGuardExcludesOtherConnectionsAndReleasesLease()
    {
        var connectionString = Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE");
        var settings = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains(settings.Host, new[] { "localhost", "127.0.0.1" });
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db1 = new AppDbContext(options);
        await using var db2 = new AppDbContext(options);
        var first = new ImageEditingGuard(db1); var second = new ImageEditingGuard(db2);
        var lease = await first.TryEnterAsync(default); Assert.NotNull(lease);
        try { Assert.Null(await second.TryEnterAsync(default)); }
        finally { await lease.DisposeAsync(); }
        await using var next = await second.TryEnterAsync(default); Assert.NotNull(next);
    }

    private static OpenAIImageEditingService Editor(HttpMessageHandler handler) => new(new HttpClient(handler), Options.Create(new ImageAiOptions { Enabled = true, Model = "gpt-image-2.5-sunburst" }),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OPENAI_API_KEY"] = "test-key" }).Build());
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct); }

    public sealed class TestGuard : IImageEditingGuard
    {
        public bool Busy;
        public Task<IAsyncDisposable?> TryEnterAsync(CancellationToken ct) => Task.FromResult<IAsyncDisposable?>(Busy ? null : new Lease());
        private sealed class Lease : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }
    public sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Request.Headers["X-Test-User"].FirstOrDefault() ?? "admin"), new Claim(ClaimTypes.Role, role) }, "Test");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public WebApplication App = null!;
        public HttpClient Client = null!;
        public readonly Dictionary<string, byte[]> Objects = new();
        public readonly ImageAiOptions Settings = new() { Enabled = true, Model = "gpt-image-2.5-sunburst" };
        public readonly TestGuard Guard = new();
        public readonly byte[] Photo = Png(new SKColor(40, 50, 60));
        public int ProviderCalls;
        public HttpStatusCode FailureStatus = HttpStatusCode.TooManyRequests;
        public bool FailProvider, FailDraftStorage, OmitUsage;
        public void As(string role, string user = "admin") { Client.DefaultRequestHeaders.Remove("X-Test-Role"); Client.DefaultRequestHeaders.Remove("X-Test-User"); Client.DefaultRequestHeaders.Add("X-Test-Role", role); Client.DefaultRequestHeaders.Add("X-Test-User", user); }
        public async Task<HttpResponseMessage> Upload(bool rights = true, byte[]? data = null, long? retryId = null, bool acknowledge = false)
        {
            using var form = new MultipartFormDataContent { { new ByteArrayContent(data ?? Photo), "image", "photo.png" }, { new StringContent("clean"), "mode" }, { new StringContent(rights.ToString()), "rightsConfirmed" } };
            if (retryId is not null) form.Add(new StringContent(retryId.Value.ToString()), "retryFailedJobId");
            if (acknowledge) form.Add(new StringContent("true"), "acknowledgePossibleCharge");
            return await Client.PostAsync("/api/image-processing/check", form);
        }
        public static async Task<Fixture> Start()
        {
            var f = new Fixture(); var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["OPENAI_API_KEY"] = "test-key", ["Supabase:Url"] = "https://storage.example.invalid", ["Supabase:SecretKey"] = "test", ["Supabase:Bucket"] = "apartments" });
            // A stable database name is required across request scopes.
            var database = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(database));
            builder.Services.AddSingleton<IOptions<ImageAiOptions>>(Options.Create(f.Settings));
            builder.Services.AddSingleton<IImageEditingGuard>(f.Guard);
            builder.Services.AddScoped<ImageProcessingQueue>(); builder.Services.AddMemoryCache(); builder.Services.AddOutputCache();
            builder.Services.AddHttpClient<OpenAIImageEditingService>().ConfigurePrimaryHttpMessageHandler(() => new Handler((request, ct) =>
            {
                Assert.Equal("api.openai.com", request.RequestUri!.Host); f.ProviderCalls++;
                return Task.FromResult(new HttpResponseMessage(f.FailProvider ? f.FailureStatus : HttpStatusCode.OK)
                    { Content = JsonContent.Create(new { data = new[] { new { b64_json = Convert.ToBase64String(Png(new SKColor(100, 110, 120))) } },
                        usage = f.OmitUsage ? null : new { input_tokens = 1554, input_tokens_details = new { text_tokens = 97, image_tokens = 1457 }, output_tokens = 196, output_tokens_details = new { image_tokens = 196, text_tokens = 0 }, total_tokens = 1750 } }) });
            }));
            builder.Services.AddHttpClient<SupabaseStorageService>().ConfigurePrimaryHttpMessageHandler(() => new Handler(async (request, ct) =>
            {
                Assert.Equal("storage.example.invalid", request.RequestUri!.Host);
                var path = request.RequestUri!.AbsolutePath;
                if (request.Method == HttpMethod.Post)
                {
                    if (f.FailDraftStorage && path.Contains("image-edit-drafts")) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                    f.Objects[path] = await request.Content!.ReadAsByteArrayAsync(ct);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
                }
                return f.Objects.TryGetValue(path, out var bytes) ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) } : new HttpResponseMessage(HttpStatusCode.NotFound);
            }));
            builder.Services.AddControllers().AddApplicationPart(typeof(PhotoChecksController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { }); builder.Services.AddAuthorization();
            builder.Services.AddRateLimiter(o => o.AddPolicy("ImageChecks", _ => RateLimitPartition.GetNoLimiter("test")));
            f.App = builder.Build(); f.App.UseAuthentication(); f.App.UseAuthorization(); f.App.UseRateLimiter(); f.App.MapControllers();
            await f.App.StartAsync(); f.Client = f.App.GetTestClient(); return f;
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await App.DisposeAsync(); }
    }
}
