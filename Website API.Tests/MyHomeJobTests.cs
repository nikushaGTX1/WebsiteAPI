using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Website_API.Controllers;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;
using Xunit;
namespace Website_API.Tests;
public class MyHomeJobTests
{
    [LocalPostgresFact]
    public async Task JobsAreAuthorizedDeduplicatedPersistedAndResumeWithoutChangingListingData()
    {
        // This test refuses non-local PostgreSQL connections and creates its own database.
        var configured = Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE");
        Assert.False(string.IsNullOrEmpty(configured));
        var cs = new NpgsqlConnectionStringBuilder(configured);
        Assert.Contains(cs.Host, new[] { "localhost", "127.0.0.1" });
        var database = "myhome_test_" + Guid.NewGuid().ToString("N");
        await using var maintenance = new NpgsqlConnection(cs.ConnectionString);
        await maintenance.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {database}", maintenance).ExecuteNonQueryAsync();
        cs.Database = database;
        WebApplication? app = null;
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Supabase:Url"] = "https://example.invalid", ["Supabase:SecretKey"] = "test", ["Supabase:Bucket"] = "apartments", ["GoogleMaps:ApiKey"] = "test", ["MyHome:Concurrency"] = "2", ["MyHome:BatchDelayMs"] = "250" });
            builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs.ConnectionString));
            builder.Services.AddControllers().AddApplicationPart(typeof(MyHomeVerificationController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, RoleHandler>("Test", _ => {});
            builder.Services.AddAuthorization(); builder.Services.AddMemoryCache();
            builder.Services.AddHttpClient<SupabaseStorageService>();
            builder.Services.AddHttpClient<GoogleNearbyPlacesService>();
            builder.Services.AddOutputCache();
            var calls = 0; var concurrent = 0; var peak = 0;
            var handler = new StubHandler(async (request, token) => {
                Interlocked.Increment(ref calls); var count = Interlocked.Increment(ref concurrent); peak = Math.Max(peak, count);
                try { await Task.Delay(80, token); var id = request.RequestUri!.Segments.Last(); return new HttpResponseMessage(id == "22222222" ? HttpStatusCode.Forbidden : HttpStatusCode.OK) { Content = new StringContent(MyHomeVerifierTests.Active(id)) }; }
                finally { Interlocked.Decrement(ref concurrent); }
            });
            builder.Services.AddTransient(_ => new MyHomeVerifier(new HttpClient(handler, false)));
            app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                db.Users.Add(new AppUser { Id = "test-user", UserName = "test", Email = "test@example.invalid" });
                for (var i = 0; i < 120; i++) db.Apartments.Add(new Apartment { Title = "Preserve title", Description = "Preserve description", Address = "Tbilisi " + i, Price = 1234, IsApproved = false, MyHomeId = i < 5 ? null : i < 60 ? "12345678" : i < 90 ? "22222222" : "33333333" });
                await db.SaveChangesAsync();
            }
            await app.StartAsync();
            using var client = app.GetTestClient();
            foreach (var role in new[] { "Agent", "Uploader" }) {
                client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", role);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/MyHomeVerification/jobs", new {})).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/MyHomeVerification/latest")).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/MyHomeVerification/jobs/{Guid.NewGuid()}/results")).StatusCode);
            }
            client.DefaultRequestHeaders.Remove("X-Test-Role");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/MyHomeVerification/jobs", new {})).StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
            var starts = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PostAsJsonAsync("/api/MyHomeVerification/jobs", new {})));
            var jobs = await Task.WhenAll(starts.Select(r => r.Content.ReadFromJsonAsync<MyHomeJob>()));
            Assert.Single(jobs.Select(j => j!.Id).Distinct());
            var jobId = jobs[0]!.Id;
            Assert.Equal(120, jobs[0]!.TotalApartments); Assert.Equal(5, jobs[0]!.MissingIds);
            client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/MyHomeVerification/latest")).StatusCode);
            // Persist a completed result and an apartment edit before worker restart.
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var prior = await db.MyHomeChecks.FirstAsync(c => c.MyHomeId == "12345678");
                prior.Status = "ACTIVE"; prior.CheckedAt = DateTime.UtcNow; prior.Detail = "Persisted evidence";
                var edited = await db.Apartments.FirstAsync(a => a.MyHomeId == "33333333"); edited.MyHomeId = "44444444";
                await db.SaveChangesAsync();
            }
            // Two independent workers contend for the same PostgreSQL session lock.
            var factory = app.Services.GetRequiredService<IServiceScopeFactory>();
            var config = app.Services.GetRequiredService<IConfiguration>();
            using var first = new MyHomeWorker(factory, config, app.Services.GetRequiredService<ILogger<MyHomeWorker>>());
            using var second = new MyHomeWorker(factory, config, app.Services.GetRequiredService<ILogger<MyHomeWorker>>());
            await Task.WhenAll(first.StartAsync(default), second.StartAsync(default));
            for (var i = 0; i < 100; i++) {
                using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (await db.MyHomeJobs.AnyAsync(j => j.Id == jobId && j.State == "COMPLETED")) break;
                await Task.Delay(100);
            }
            await Task.WhenAll(first.StopAsync(default), second.StopAsync(default));
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal("COMPLETED", (await db.MyHomeJobs.FindAsync(jobId))!.State);
                Assert.Equal(0, await db.MyHomeChecks.CountAsync(c => c.Status == "PENDING"));
                Assert.Equal(30, await db.MyHomeChecks.CountAsync(c => c.Status == "UNCERTAIN"));
                Assert.Equal(5, await db.MyHomeChecks.CountAsync(c => c.Status == "NO_MYHOME_ID"));
                Assert.Equal(2, calls); Assert.InRange(peak, 1, 2);
                Assert.All(await db.Apartments.ToListAsync(), a => { Assert.Equal(1234, a.Price); Assert.Equal("Preserve description", a.Description); Assert.False(a.IsApproved); });
                Assert.Null((await db.Apartments.FirstAsync(a => a.MyHomeId == "44444444")).MyHomeStatus);
                Assert.Equal(55, await db.Apartments.CountAsync(a => a.MyHomeStatus == "ACTIVE" && a.MyHomeId == "12345678"));
            }
            using var report = JsonDocument.Parse(await client.GetStringAsync($"/api/MyHomeVerification/jobs/{jobId}/results?status=UNCERTAIN"));
            Assert.Equal(30, report.RootElement.GetProperty("total").GetInt32());
            using var all = JsonDocument.Parse(await client.GetStringAsync($"/api/MyHomeVerification/jobs/{jobId}/results?page=2"));
            Assert.Equal(50, all.RootElement.GetProperty("items").GetArrayLength());
            using var search = JsonDocument.Parse(await client.GetStringAsync($"/api/MyHomeVerification/jobs/{jobId}/results?search=22222222"));
            Assert.Equal(30, search.RootElement.GetProperty("total").GetInt32());
            // Real upload/edit endpoints: optional field, trimming, preservation, clearing,
            // model validation and existing owner/staff permissions.
            using var createForm = Form(new() { ["Title"] = "New upload", ["Description"] = "Original copy", ["Price"] = "987", ["District"] = "Test district", ["City"] = "Tbilisi", ["SizeSquareMeters"] = "65", ["TotalFloors"] = "10", ["MyHomeId"] = " 55555555 " });
            var created = await client.PostAsync("/api/Apartments", createForm);
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            var uploaded = createdJson.RootElement.GetProperty("apartment");
            var uploadedId = uploaded.GetProperty("id").GetInt32();
            Assert.Equal("55555555", uploaded.GetProperty("myHomeId").GetString());
            using var omitted = Form(new() { ["Title"] = "Edited title" });
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsync($"/api/Apartments/{uploadedId}", omitted)).StatusCode);
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var apartment = (await db.Apartments.FindAsync(uploadedId))!;
                Assert.Equal("55555555", apartment.MyHomeId); Assert.Equal(987, apartment.Price);
                apartment.MyHomeStatus = "ACTIVE"; apartment.MyHomeCheckedAt = DateTime.UtcNow; await db.SaveChangesAsync();
            }
            client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Agent");
            using var changed = Form(new() { ["MyHomeId"] = "66666666" });
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsync($"/api/Apartments/{uploadedId}", changed)).StatusCode);
            using var forbiddenEdit = Form(new() { ["MyHomeId"] = "66666666" });
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync("/api/Apartments/1", forbiddenEdit)).StatusCode);
            using var invalid = Form(new() { ["MyHomeId"] = "invalid" });
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync($"/api/Apartments/{uploadedId}", invalid)).StatusCode);
            using var clear = Form(new() { ["MyHomeId"] = "" });
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsync($"/api/Apartments/{uploadedId}", clear)).StatusCode);
            using (var scope = app.Services.CreateScope()) {
                var apartment = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Apartments.FindAsync(uploadedId))!;
                Assert.Null(apartment.MyHomeId); Assert.Null(apartment.MyHomeStatus); Assert.Null(apartment.MyHomeCheckedAt);
                Assert.Equal("Original copy", apartment.Description); Assert.Equal(987, apartment.Price);
            }
        }
        finally {
            if (app != null) await app.DisposeAsync();
            NpgsqlConnection.ClearAllPools();
            await new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", maintenance).ExecuteNonQueryAsync();
        }
    }
    private static MultipartFormDataContent Form(Dictionary<string,string> fields) {
        var form = new MultipartFormDataContent(); foreach (var (key,value) in fields) form.Add(new StringContent(value), key); return form;
    }
}
public class RoleHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        var role = Request.Headers["X-Test-Role"].ToString();
        if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "test-user") }, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
public class LocalPostgresFactAttribute : FactAttribute
{
    public LocalPostgresFactAttribute() {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE")))
            Skip = "Set MYHOME_TEST_DATABASE to a local PostgreSQL connection to run integration checks.";
    }
}
