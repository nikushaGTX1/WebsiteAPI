using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Website_API.Controllers;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class ApartmentSubmissionTests
{
    public const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j2ioAAAAASUVORK5CYII=";
    private static SubmissionProperty Property(decimal price = 1000) => new() {
        City = "Tbilisi", Location = "Vake", Street = "Abashidze", StreetNumber = "12", TotalPrice = price, Area = 85,
        Rooms = 3, Bedrooms = 2, Bathrooms = 1, Floor = 3, TotalFloors = 8, BuildingType = "New building", Condition = "Newly Renovated",
        IsFurnished = true, IsPetFriendly = true, AvailableFrom = DateTime.UtcNow.ToString("yyyy-MM-dd"), MinimumRentalPeriod = "Minimum 12 months", Description = "A verified apartment with balcony and furniture." };
    private static string Profile(string district = "Vake") {
        var node = JsonNode.Parse(ClientBoardTests.Profile(district).GetRawText())!;
        node["moveInTiming"] = "Flexible"; node["moveInDate"] = null; node["minSize"] = 70;
        return node.ToJsonString();
    }

    [Fact]
    public void GeneralFormValidatesHardDataAndOwnerContact()
    {
        Assert.Null(Property().Validate("Owner", "+995555123456"));
        var invalid = Property(); invalid.Currency = "GEL"; Assert.NotNull(invalid.Validate("Owner", "+995555123456"));
        invalid = Property(); invalid.Bedrooms = 9; Assert.NotNull(invalid.Validate("Owner", "+995555123456"));
        invalid = Property(); invalid.Floor = 20; Assert.NotNull(invalid.Validate("Owner", "+995555123456"));
        Assert.NotNull(Property().Validate("Owner", "123"));
        Assert.NotNull(SubmissionProperty.Read("{\"city\":null}")!.Validate("Owner", "+995555123456"));
    }

    [LocalPostgresFact]
    public async Task GeneralSubmissionVerificationAndIdempotentSendUseRealPostgres()
    {
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE"));
        Assert.Contains(cs.Host, new[] { "localhost", "127.0.0.1" });
        var database = "general_submissions_" + Guid.NewGuid().ToString("N");
        await using var maintenance = new NpgsqlConnection(cs.ConnectionString); await maintenance.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {database}", maintenance).ExecuteNonQueryAsync(); cs.Database = database;
        WebApplication? app = null;
        try
        {
            var script = Environment.GetEnvironmentVariable("VELVEN_SUBMISSION_BROWSER_SCRIPT");
            var builder = WebApplication.CreateBuilder();
            if (string.IsNullOrEmpty(script)) builder.WebHost.UseTestServer(); else builder.WebHost.UseKestrel(o => o.Listen(IPAddress.Loopback, 0));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Supabase:Url"] = "https://storage.example.invalid", ["Supabase:SecretKey"] = "test", ["Supabase:Bucket"] = "apartments", ["GoogleMaps:ApiKey"] = "test" });
            builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs.ConnectionString));
            builder.Services.AddIdentityCore<AppUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
            builder.Services.AddControllers().AddApplicationPart(typeof(ApartmentSubmissionsController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ClientBoardRoleHandler>("Test", _ => { });
            builder.Services.AddAuthorization(); builder.Services.AddMemoryCache(); builder.Services.AddOutputCache();
            var storage = new StorageFixture();
            builder.Services.AddHttpClient<SupabaseStorageService>().ConfigurePrimaryHttpMessageHandler(() => storage);
            builder.Services.AddHttpClient<GoogleNearbyPlacesService>(); builder.Services.AddScoped<ListingInterestService>();
            builder.Services.AddScoped<ApartmentSubmissionService>(); builder.Services.AddScoped<ImageProcessingQueue>(); builder.Services.Configure<ImageAiOptions>(_ => { }); builder.Services.AddSingleton<ApartmentMatchQueue>();
            builder.Services.AddRateLimiter(o => o.AddPolicy("CrmInquiries", _ => RateLimitPartition.GetNoLimiter("test")));
            app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.MapControllers();
            long publicClient, privateClient, wrongClient;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.MigrateAsync();
                db.Users.AddRange(new AppUser { Id = "agent-a", UserName = "a", FullName = "Agent A", PhoneNumber = "+995555000001" },
                    new AppUser { Id = "agent-b", UserName = "b", FullName = "Agent B", PhoneNumber = "+995555000002" }, new AppUser { Id = "manager", UserName = "manager" },
                    new AppUser { Id = "private-client", UserName = "p", FullName = "SECRET MATCH NAME", PhoneNumber = "+995599999999", MatchAlertsInApp = true }, new AppUser { Id = "wrong-client", UserName = "w" });
                db.Roles.AddRange(new IdentityRole { Id = "agent", Name = "Agent", NormalizedName = "AGENT" }, new IdentityRole { Id = "client", Name = "Client", NormalizedName = "CLIENT" }, new IdentityRole { Id = "manager", Name = "Manager", NormalizedName = "MANAGER" });
                db.UserRoles.AddRange(new IdentityUserRole<string> { UserId = "agent-a", RoleId = "agent" }, new IdentityUserRole<string> { UserId = "agent-b", RoleId = "agent" }, new IdentityUserRole<string> { UserId = "manager", RoleId = "manager" },
                    new IdentityUserRole<string> { UserId = "private-client", RoleId = "client" }, new IdentityUserRole<string> { UserId = "wrong-client", RoleId = "client" });
                db.SavedSearches.AddRange(new SavedSearch { UserId = "private-client", Name = "SECRET SEARCH", CriteriaHash = "one", ProfileJson = Profile() },
                    new SavedSearch { UserId = "private-client", Name = "Another search", CriteriaHash = "two", ProfileJson = Profile() },
                    new SavedSearch { UserId = "wrong-client", Name = "Wrong district", CriteriaHash = "wrong", ProfileJson = Profile("Chugureti") });
                var link = new ClientIntakeLink { Token = "staff-fixture", AgentUserId = "agent-b" };
                ClientProfile Board(string district, bool publish) => new() { IntakeLink = link, AgentUserId = "agent-b", FirstName = "PRIVATE BOARD NAME", Phone = "555888999", ConsentAt = DateTime.UtcNow, PublishConsentAt = publish ? DateTime.UtcNow : null,
                    ProfileJson = Profile(district), Districts = [district], BudgetMinUsd = 800, BudgetMaxUsd = 1400, Residents = 2, HasPets = true,
                    Lead = new CrmLead { Name = "PRIVATE BOARD NAME", Phone = "555888999", AssignedAgentId = "agent-b", Source = CrmLeadSource.AiMatch } };
                var a = Board("Vake", true); var b = Board("Vake", false); var c = Board("Chugureti", true);
                db.ClientProfiles.AddRange(a, b, c); await db.SaveChangesAsync(); publicClient = a.Id; privateClient = b.Id; wrongClient = c.Id;
            }
            await app.StartAsync(); using var client = string.IsNullOrEmpty(script) ? app.GetTestClient() : new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            void As(string? role = null, string user = "anonymous") { client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Remove("X-Test-User"); if (role is not null) client.DefaultRequestHeaders.Add("X-Test-Role", role); client.DefaultRequestHeaders.Add("X-Test-User", user); }
            async Task<JsonElement> Json(HttpResponseMessage response) { response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
            Task<HttpResponseMessage> Submit(Guid key, decimal price = 1000, bool photo = true) {
                var form = new MultipartFormDataContent { { new StringContent(key.ToString()), "requestId" }, { new StringContent("Owner Private"), "ownerName" }, { new StringContent("+995555123456"), "ownerPhone" },
                    { new StringContent(JsonSerializer.Serialize(Property(price), SubmissionProperty.Json)), "data" } };
                if (photo) { var image = new ByteArrayContent(Convert.FromBase64String(Png)); image.Headers.ContentType = new MediaTypeHeaderValue("image/png"); form.Add(image, "photos", "apartment.png"); }
                return client.PostAsync("/api/owner-listings/submissions", form);
            }
            As(); var board = await client.GetStringAsync("/api/client-board"); Assert.Contains($"\"id\":{publicClient}", board); Assert.DoesNotContain($"\"id\":{privateClient}", board);
            foreach (var secret in new[] { "SECRET MATCH NAME", "SECRET SEARCH", "PRIVATE BOARD NAME", "555888999" }) Assert.DoesNotContain(secret, board);
            Assert.Equal(HttpStatusCode.BadRequest, (await Submit(Guid.NewGuid(), photo: false)).StatusCode);
            var key = Guid.NewGuid(); var concurrent = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Submit(key)));
            var submitted = await Json(concurrent[0]); var id = submitted.GetProperty("id").GetInt64();
            foreach (var response in concurrent.Skip(1)) Assert.Equal(id, (await Json(response)).GetProperty("id").GetInt64());
            Assert.Equal(id, (await Json(await Submit(key))).GetProperty("id").GetInt64()); Assert.Equal(1, storage.Uploads);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/owner-listings/submissions/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/apartment-offers", new { clientId = publicClient })).StatusCode);
            As("Agent", "agent-b"); Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/owner-listings/submissions/{id}/send", null)).StatusCode);
            As("Agent", "agent-a"); var row = await Json(await client.GetAsync($"/api/owner-listings/submissions/{id}")); var apartmentId = row.GetProperty("publishedApartmentId").GetInt32();
            As("Agent", "agent-b"); Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Apartments/{apartmentId}")).StatusCode);
            As("Manager", "manager"); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/owner-listings/submissions/{id}")).StatusCode);
            As("Agent", "agent-a");
            Assert.Equal("new", row.GetProperty("status").GetString()); Assert.Equal("Owner Private", row.GetProperty("ownerName").GetString());
            Assert.Contains("owner-submission", await client.GetStringAsync("/api/Crm/notifications"));
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/owner-listings/submissions/{id}/send", null)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/owner-listings/submissions/{id}/approve", null)).StatusCode);
            var verification = ApartmentSubmissionService.VerificationKeys.ToDictionary(x => x, _ => true);
            async Task VerifyAndApprove(long submissionId) { (await client.PatchAsJsonAsync($"/api/owner-listings/submissions/{submissionId}", new { status = "contacted", verification })).EnsureSuccessStatusCode(); (await client.PostAsync($"/api/owner-listings/submissions/{submissionId}/approve", null)).EnsureSuccessStatusCode(); }
            await VerifyAndApprove(id);
            using (var scope = app.Services.CreateScope()) Assert.False((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Apartments.SingleAsync(x => x.Id == apartmentId)).IsApproved);
            // Correcting details invalidates the previous approval and checklist.
            var edited = await Json(await client.PatchAsJsonAsync($"/api/owner-listings/submissions/{id}", new { data = Property(), ownerName = "Owner Corrected", ownerPhone = "+995555123456" }));
            Assert.Equal("new", edited.GetProperty("status").GetString()); Assert.Equal(JsonValueKind.Null, edited.GetProperty("approvedAt").ValueKind);
            await VerifyAndApprove(id);
            var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => client.PostAsync($"/api/owner-listings/submissions/{id}/send", null)));
            foreach (var result in results) Assert.Equal(3, (await Json(result)).GetProperty("matchedClients").GetInt32());
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Equal(1, await db.SubmissionEvents.CountAsync(x => x.OwnerSubmissionId == id && x.Action == "sent"));
                Assert.Equal(2, await db.SubmissionClientMatches.CountAsync(x => x.OwnerSubmissionId == id)); Assert.DoesNotContain(wrongClient, await db.SubmissionClientMatches.Select(x => x.ClientProfileId).ToListAsync());
                Assert.Equal(2, await db.SavedSearchMatches.CountAsync(x => x.ApartmentId == apartmentId)); Assert.Equal(1, await db.ClientNotifications.CountAsync(x => x.ApartmentId == apartmentId));
                Assert.True((await db.Apartments.SingleAsync(x => x.Id == apartmentId)).IsApproved);
            }
            As("Agent", "agent-b"); var matches = await client.GetStringAsync("/api/owner-listings/matches"); Assert.Contains($"\"clientId\":{privateClient}", matches); Assert.DoesNotContain("Owner Corrected", matches);
            Assert.Contains("submission-match", await client.GetStringAsync("/api/Crm/notifications"));
            As("Client", "private-client"); Assert.NotEqual("[]", await client.GetStringAsync("/api/client/matches"));
            As(); var publicApartment = await client.GetStringAsync($"/api/Apartments/{apartmentId}"); Assert.DoesNotContain("Owner Corrected", publicApartment); Assert.DoesNotContain("+995555123456", publicApartment);
            // A submitted, unverified home stays private; rejection never distributes it.
            var rejectedId = (await Json(await Submit(Guid.NewGuid()))).GetProperty("id").GetInt64();
            As("Agent", "agent-a"); var rejected = await Json(await client.GetAsync($"/api/owner-listings/submissions/{rejectedId}"));
            (await client.PostAsync($"/api/owner-listings/submissions/{rejectedId}/reject", null)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/owner-listings/submissions/{rejectedId}/send", null)).StatusCode);
            As(); Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Apartments/{rejected.GetProperty("publishedApartmentId").GetInt32()}")).StatusCode);
            var emptyId = (await Json(await Submit(Guid.NewGuid(), price: 1))).GetProperty("id").GetInt64();
            As("Agent", "agent-a"); await VerifyAndApprove(emptyId);
            Assert.Equal(0, (await Json(await client.PostAsync($"/api/owner-listings/submissions/{emptyId}/send", null))).GetProperty("matchedClients").GetInt32());

            if (!string.IsNullOrEmpty(script)) {
                var start = new ProcessStartInfo("node") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(script)))! };
                start.ArgumentList.Add(script); start.ArgumentList.Add(JsonSerializer.Serialize(new { api = app.Urls.Single(), publicClient, privateClient, wrongClient, png = Png }));
                using var browser = Process.Start(start)!; var stdout = browser.StandardOutput.ReadToEndAsync(); var stderr = browser.StandardError.ReadToEndAsync();
                using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4)); try { await browser.WaitForExitAsync(deadline.Token); } catch { browser.Kill(true); throw; }
                Assert.True(browser.ExitCode == 0, $"Browser integration failed:\n{await stdout}\n{await stderr}");
            }
        }
        finally { if (app is not null) await app.DisposeAsync(); NpgsqlConnection.ClearAllPools(); await new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", maintenance).ExecuteNonQueryAsync(); }
    }
    private sealed class StorageFixture : HttpMessageHandler
    {
        public int Uploads;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            if (request.Method == HttpMethod.Post && !request.RequestUri!.AbsolutePath.Contains("/sign/")) Interlocked.Increment(ref Uploads);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { signedURL = "https://storage.example.invalid/test-apartment.png" }) });
        }
    }
}
