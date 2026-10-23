using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Website_API.Controllers;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class ClientBoardTests
{
    internal static JsonElement Profile(string district = "Vake", int adults = 2, bool pets = true) => JsonSerializer.SerializeToElement(new {
        propertyGoal = "Rent", districts = new[] { district }, locationFlexible = false,
        budgetMin = 800, budgetMax = 1400, currency = "USD", gender = "Female", householdType = "Couple",
        adults, children = 0, childrenAgeGroups = Array.Empty<string>(), bedrooms = 2,
        bedroomChoices = new[] { 2 }, rentalDuration = "TwelveMonths", moveInTiming = "SpecificDate", moveInDate = "2026-11-01",
        lifestyles = new[] { "RemoteWorker" }, topPriorities = new[] { "Workspace", "Parking", "ParkNearby", "QuietStreet", "MetroNearby" },
        transportation = new[] { "Walking" }, apartmentStyle = "Modern",
        hasPet = pets, petType = pets ? "Dog" : "None", petTypes = pets ? new[] { "Dog" } : Array.Empty<string>(),
        dogSize = pets ? "Medium" : null, petCount = 1,
        // Unrecognised and private fields must not leak, even through a profile JSON property.
        name = "SECRET NAME", phone = "+995555111222", email = "secret@example.invalid",
        proximityAddress = "PRIVATE ADDRESS", proximityLatitude = 41.7, proximityLongitude = 44.8,
    });
    internal static ClientIntakeRequest Request(bool consent = true, bool publish = true) => new() {
        FirstName = "PrivateFirstName", Phone = "555123456", Nationality = "Georgia",
        Consent = consent, PublishConsent = publish, Profile = Profile(),
    };

    [Fact]
    public void RequiresContactConsentValidPhonesAndCodedBoundedProfile()
    {
        Assert.Equal("consent_required", ClientRequirements.Validate(Request(false)));
        Assert.Null(ClientRequirements.Validate(Request(publish: false)));
        foreach (var phone in new[] { "abc", "123", "+995 123 456 789", "555 abc 123", "123456789" }) {
            var request = Request(); request.Phone = phone;
            Assert.Equal("invalid_contact", ClientRequirements.Validate(request));
        }
        foreach (var phone in new[] { "555 12 34 56", "+995 555-12-34-56", "+44 7700 900123" }) Assert.True(ClientRequirements.ValidPhone(phone));
        var translated = Request(); translated.Profile = Profile("ვაკე");
        Assert.Equal("invalid_profile", ClientRequirements.Validate(translated));
        var oversized = Request(); oversized.Profile = JsonSerializer.SerializeToElement(new { padding = new string('x', 102401) });
        Assert.Equal("profile_too_large", ClientRequirements.Validate(oversized));
        var invalid = Request(); invalid.Profile = Profile(adults: 8);
        Assert.Equal("invalid_profile", ClientRequirements.Validate(invalid));
        var malformed = Request(); malformed.Profile = JsonSerializer.SerializeToElement(new { propertyGoal = "Rent", adults = "bad" });
        Assert.Equal("invalid_profile", ClientRequirements.Validate(malformed));
        var partial = Request(); partial.Profile = JsonSerializer.SerializeToElement(new { propertyGoal = "Rent" });
        Assert.Equal("invalid_profile", ClientRequirements.Validate(partial));
    }

    [Fact]
    public void PublicDtoHasNoClientContactOrArbitraryJsonFields()
    {
        var row = new ClientProfile { Id = 1042, FirstName = "PrivateFirstName", Phone = "555123456", ProfileJson = Profile().GetRawText(), HasPets = true,
            Agent = new AppUser { FullName = "PublicAgent", PhoneNumber = "+995555999888" } };
        var card = ClientBoardController.ToPublic(row, null);
        var json = JsonSerializer.Serialize(card, ClientRequirements.Json);
        foreach (var secret in new[] { "PrivateFirstName", "555123456", "SECRET NAME", "secret@example.invalid", "PRIVATE ADDRESS", "proximityLatitude", "proximityLongitude", "+995555111222", "profileJson", "agentUserId" }) Assert.DoesNotContain(secret, json);
        Assert.Contains("PublicAgent", json); Assert.Contains("555999888", json);
        Assert.Equal(3, card.Profile.TopPriorities.Length); Assert.False(card.IdentityVerified);
    }

    [Fact]
    public void LinkAndStatusEndpointsRequireExactlyStaffAndIntakeIsRateLimited()
    {
        foreach (var method in new[] { "Links", "CreateLink", "UpdateLink", "Revoke", "Submissions", "Status" }) {
            var auth = typeof(ClientBoardController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>();
            Assert.Equal("Admin,Manager,Agent", auth!.Roles);
        }
        var intake = typeof(ClientBoardController).GetMethod("Intake")!;
        Assert.Equal("CrmInquiries", intake.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Equal(110 * 1024, ((Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata)intake.GetCustomAttribute<RequestSizeLimitAttribute>()!).MaxRequestBodySize);
    }

    [LocalPostgresFact]
    public async Task IntakeBoardAuthorizationFiltersPrivacyRevocationAndStatusWorkInPostgres()
    {
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE"));
        Assert.Contains(cs.Host, new[] { "localhost", "127.0.0.1" });
        var database = "client_board_" + Guid.NewGuid().ToString("N");
        await using var maintenance = new NpgsqlConnection(cs.ConnectionString);
        await maintenance.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {database}", maintenance).ExecuteNonQueryAsync();
        cs.Database = database;
        WebApplication? app = null;
        try {
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Supabase:Url"] = "https://example.invalid", ["Supabase:SecretKey"] = "test", ["Supabase:Bucket"] = "apartments" });
            builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs.ConnectionString));
            builder.Services.AddControllers().AddApplicationPart(typeof(ClientBoardController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ClientBoardRoleHandler>("Test", _ => { });
            builder.Services.AddAuthorization(); builder.Services.AddMemoryCache(); builder.Services.AddHttpClient<SupabaseStorageService>();
            builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("CrmInquiries", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Request.Headers["X-Test-IP"].ToString(), _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 })); });
            app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.MapControllers();
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.MigrateAsync();
                db.Users.AddRange(new AppUser { Id = "test-user", UserName = "test", FullName = "PublicAgent", PhoneNumber = "+995555999888" }, new AppUser { Id = "other", UserName = "other" });
                db.Roles.Add(new IdentityRole { Id = "staff", Name = "Agent", NormalizedName = "AGENT" });
                db.UserRoles.AddRange(new IdentityUserRole<string> { UserId = "test-user", RoleId = "staff" }, new IdentityUserRole<string> { UserId = "other", RoleId = "staff" });
                await db.SaveChangesAsync();
            }
            await app.StartAsync(); using var client = app.GetTestClient();
            void Role(string? role, string user = "test-user") { client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Remove("X-Test-User"); if (role is not null) client.DefaultRequestHeaders.Add("X-Test-Role", role); client.DefaultRequestHeaders.Add("X-Test-User", user); }
            async Task<HttpResponseMessage> Submit(string token, ClientIntakeRequest request, bool freshIp = true) { if (freshIp) { client.DefaultRequestHeaders.Remove("X-Test-IP"); client.DefaultRequestHeaders.Add("X-Test-IP", Guid.NewGuid().ToString()); } return await client.PostAsJsonAsync("/api/client-intake/" + token, request); }
            foreach (var role in new string?[] { null, "Client", "Owner", "Uploader" }) {
                Role(role); var expected = role is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
                Assert.Equal(expected, (await client.GetAsync("/api/client-intake-links")).StatusCode);
                Assert.Equal(expected, (await client.PostAsJsonAsync("/api/client-intake-links", new { })).StatusCode);
                Assert.Equal(expected, (await client.DeleteAsync("/api/client-intake-links/1")).StatusCode);
                Assert.Equal(expected, (await client.PatchAsJsonAsync("/api/client-intake-links/1", new { slug = "agent-fixture" })).StatusCode);
                Assert.Equal(expected, (await client.GetAsync("/api/client-profiles")).StatusCode);
                Assert.Equal(expected, (await client.PatchAsJsonAsync("/api/client-profiles/1/status", new { status = "Closed" })).StatusCode);
            }
            JsonElement link = default;
            foreach (var role in new[] { "Admin", "Manager", "Agent" }) { Role(role); var response = await client.PostAsJsonAsync("/api/client-intake-links", new { }); response.EnsureSuccessStatusCode(); link = await response.Content.ReadFromJsonAsync<JsonElement>(); }
            var token = link.GetProperty("token").GetString()!; var linkId = link.GetProperty("id").GetInt64();
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/client-intake-links/{linkId}", new { slug = "invalid slug!" })).StatusCode);
            (await client.PatchAsJsonAsync($"/api/client-intake-links/{linkId}", new { slug = "agent-fixture" })).EnsureSuccessStatusCode();
            Role(null);
            Assert.Equal(HttpStatusCode.NotFound, (await Submit("unknown", Request())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/client-intake/", Request())).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Submit(token, Request(false))).StatusCode);
            var response1 = await Submit("agent-fixture", Request()); response1.EnsureSuccessStatusCode();
            var id = (await response1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
            (await Submit(token, Request(publish: false))).EnsureSuccessStatusCode();
            var saburtalo = Request(); saburtalo.Profile = Profile("Saburtalo", 1, false);
            (await Submit(token, saburtalo)).EnsureSuccessStatusCode();
            async Task<ClientBoardPage> Board(string query = "") { var r = await client.GetAsync("/api/client-board" + query); r.EnsureSuccessStatusCode(); var raw = await r.Content.ReadAsStringAsync(); Assert.DoesNotContain("PrivateFirstName", raw); Assert.DoesNotContain("secret@example.invalid", raw); Assert.DoesNotContain("PRIVATE ADDRESS", raw); return (await r.Content.ReadFromJsonAsync<ClientBoardPage>())!; }
            Assert.Equal(2, (await Board()).Total);
            Assert.Single((await Board("?district=Vake")).Items);
            Assert.Empty((await Board("?district=Vera")).Items);
            Assert.Single((await Board("?residents=1&pets=false")).Items);
            Assert.Single((await Board("?residents=2&pets=true&budgetMin=1300&budgetMax=1500&moveInBefore=2026-11-02")).Items);
            Assert.Empty((await Board("?budgetMin=1500")).Items);
            Assert.Empty((await Board("?budgetMax=700")).Items);
            Assert.Empty((await Board("?moveInBefore=2026-10-25")).Items);
            Assert.Single((await Board("?pageSize=1&page=2")).Items);
            Assert.Empty((await Board("?page=3&pageSize=1")).Items);
            foreach (var query in new[] { "?page=0", "?pageSize=100", "?residents=8", "?budgetMin=2000&budgetMax=1000" }) Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/client-board" + query)).StatusCode);
            Role("Agent", "other");
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/client-intake-links/{linkId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/client-intake-links/{linkId}", new { slug = "agent-stolen" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/client-profiles/{id}/status", new { status = "Closed" })).StatusCode);
            Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/client-intake-links"))!);
            Role("Agent");
            var recent = await client.GetFromJsonAsync<JsonElement>("/api/client-profiles"); Assert.Equal(3, recent.GetProperty("total").GetInt32());
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/client-profiles/{id}/status", new { status = "Unknown" })).StatusCode);
            (await client.PatchAsJsonAsync($"/api/client-profiles/{id}/status", new { status = "Paused" })).EnsureSuccessStatusCode();
            Assert.Equal(1, (await Board()).Total);
            (await client.PatchAsJsonAsync($"/api/client-profiles/{id}/status", new { status = "Closed" })).EnsureSuccessStatusCode();
            Assert.Equal(1, (await Board()).Total);
            (await client.PatchAsJsonAsync($"/api/client-profiles/{id}/status", new { status = "Active" })).EnsureSuccessStatusCode();
            (await client.DeleteAsync($"/api/client-intake-links/{linkId}")).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound, (await Submit(token, Request())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Submit("agent-fixture", Request())).StatusCode);
            Assert.Equal(2, (await Board()).Total); // Revocation stops new intake, not existing client consent.
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal(3, await db.CrmLeads.CountAsync());
                Assert.All(await db.CrmLeads.ToListAsync(), l => { Assert.Equal("test-user", l.AssignedAgentId); Assert.True(l.ConsentGiven); });
                Assert.All(await db.ClientProfiles.ToListAsync(), p => { Assert.DoesNotContain("SECRET NAME", p.ProfileJson); Assert.NotEqual(default, p.ConsentAt); });
                db.UserRoles.RemoveRange(await db.UserRoles.Where(x => x.UserId == "test-user").ToListAsync()); await db.SaveChangesAsync();
            }
            Assert.Empty((await Board()).Items); // Demoted staff's profiles are no longer public.
            // Policy enforces the public endpoint's limit, including invalid/revoked tokens.
            client.DefaultRequestHeaders.Remove("X-Test-IP"); client.DefaultRequestHeaders.Add("X-Test-IP", "rate-test");
            for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.NotFound, (await Submit("unknown", Request(), false)).StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await Submit("unknown", Request(), false)).StatusCode);
            // Existing CRM/account removal flows must remain usable with the new records.
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var profile = await db.ClientProfiles.FirstAsync(x => x.Id == id);
                await db.CrmLeads.Where(x => x.Id == profile.LeadId).ExecuteDeleteAsync();
                Assert.False(await db.ClientProfiles.AnyAsync(x => x.Id == id));
                await db.Users.Where(x => x.Id == "test-user").ExecuteDeleteAsync();
                Assert.Empty(await db.ClientProfiles.ToListAsync());
                Assert.Empty(await db.ClientIntakeLinks.ToListAsync());
            }
        }
        finally {
            if (app is not null) await app.DisposeAsync(); NpgsqlConnection.ClearAllPools();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", maintenance).ExecuteNonQueryAsync();
        }
    }
}

public class ClientBoardRoleHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].ToString();
        if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
        var user = Request.Headers["X-Test-User"].FirstOrDefault() ?? "test-user";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, user) }, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
