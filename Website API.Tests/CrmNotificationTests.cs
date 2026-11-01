using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
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

public class CrmNotificationTests
{
    [LocalPostgresFact]
    public async Task StatusChangesPersistAlertsForTheAgentAndOtherManagersWithIsolatedReadState()
    {
        var configured = Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE");
        var cs = new NpgsqlConnectionStringBuilder(configured);
        Assert.Contains(cs.Host, new[] { "localhost", "127.0.0.1" });
        var database = "crm_notifications_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(cs.ConnectionString))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {database}", connection).ExecuteNonQueryAsync();
        }
        cs.Database = database;
        WebApplication? app = null;
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supabase:Url"] = "https://example.invalid", ["Supabase:SecretKey"] = "test", ["Supabase:Bucket"] = "apartments",
            });
            builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs.ConnectionString));
            builder.Services.AddIdentityCore<AppUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
            builder.Services.AddControllers().AddApplicationPart(typeof(CrmController).Assembly);
            builder.Services.AddAuthentication("NoticeTest").AddScheme<AuthenticationSchemeOptions, NoticeAuthHandler>("NoticeTest", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddMemoryCache();
            builder.Services.AddHttpClient<ResendEmailService>();
            builder.Services.AddHttpClient<SupabaseStorageService>();
            builder.Services.AddScoped<ListingInterestService>();
            app = builder.Build();
            app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            int leadId, matchId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                foreach (var role in new[] { "Manager", "Admin", "Agent" })
                    db.Roles.Add(new IdentityRole { Id = role, Name = role, NormalizedName = role.ToUpperInvariant() });
                foreach (var id in new[] { "actor", "agent", "manager", "unrelated" })
                    db.Users.Add(new AppUser { Id = id, UserName = id, FullName = id == "actor" ? "Andria Manager" : id });
                db.UserRoles.AddRange(
                    new IdentityUserRole<string> { UserId = "actor", RoleId = "Manager" },
                    new IdentityUserRole<string> { UserId = "manager", RoleId = "Manager" },
                    new IdentityUserRole<string> { UserId = "manager", RoleId = "Admin" },
                    new IdentityUserRole<string> { UserId = "agent", RoleId = "Agent" },
                    new IdentityUserRole<string> { UserId = "unrelated", RoleId = "Agent" });
                var lead = new CrmLead { Name = "Giorgi Client", AssignedAgentId = "agent", Status = CrmLeadStatus.Lost };
                db.CrmLeads.Add(lead);
                var apartment = new Apartment { Title = "Flat", Price = 1100, IsApproved = true };
                var search = new SavedSearch { UserId = "agent", Name = "Home", CriteriaHash = "test" };
                db.Apartments.Add(apartment); db.SavedSearches.Add(search);
                await db.SaveChangesAsync();
                var match = new ListingInterestNotification { UserId = "agent", ReviewerUserId = "agent", ApartmentId = apartment.Id,
                    SavedSearchId = search.Id, CreatedAt = DateTime.UtcNow.AddHours(-1) };
                db.ListingInterestNotifications.Add(match);
                await db.SaveChangesAsync();
                leadId = lead.Id; matchId = match.Id;
            }
            await app.StartAsync();
            using var client = app.GetTestClient();
            void As(string id, string role)
            {
                client.DefaultRequestHeaders.Remove("X-Notice-User"); client.DefaultRequestHeaders.Remove("X-Notice-Role");
                client.DefaultRequestHeaders.Add("X-Notice-User", id); client.DefaultRequestHeaders.Add("X-Notice-Role", role);
            }
            async Task<JsonElement> Feed(string suffix = "") =>
                await client.GetFromJsonAsync<JsonElement>("/api/Crm/notifications" + suffix);
            As("actor", "Manager");
            Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/Crm/leads/{leadId}/status", new { status = "won" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/Crm/leads/{leadId}/status", new { status = "won" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/Crm/leads/{leadId}/status", new { status = "bogus" })).StatusCode);
            // Actor sees the shared match queue, but no alert for their own status change.
            Assert.Equal(1, (await Feed()).GetProperty("total").GetInt32());
            As("agent", "Agent");
            var feed = await Feed();
            Assert.Equal(2, feed.GetProperty("unread").GetInt32());
            var notice = feed.GetProperty("items")[0];
            Assert.Equal("crm-status", notice.GetProperty("type").GetString());
            Assert.Equal(leadId, notice.GetProperty("leadId").GetInt32());
            Assert.Equal("Giorgi Client", notice.GetProperty("leadName").GetString());
            Assert.Equal("Andria Manager", notice.GetProperty("actorName").GetString());
            Assert.Equal("lost", notice.GetProperty("fromStatus").GetString());
            Assert.Equal("won", notice.GetProperty("toStatus").GetString());
            var noticeId = notice.GetProperty("id").GetInt32();
            Assert.Equal("interest-review", (await Feed("?page=2&pageSize=1")).GetProperty("items")[0].GetProperty("type").GetString());
            As("unrelated", "Agent");
            Assert.Equal(0, (await Feed()).GetProperty("total").GetInt32());
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/Crm/notifications/crm-status/{noticeId}/read", null)).StatusCode);
            As("agent", "Agent");
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/Crm/notifications/crm-status/{noticeId}/read", null)).StatusCode);
            // Read is idempotent and remains read on another request/context.
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/Crm/notifications/crm-status/{noticeId}/read", null)).StatusCode);
            Assert.Equal(1, (await Feed("/summary")).GetProperty("unread").GetInt32());
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/Crm/notifications/read-all", null)).StatusCode);
            Assert.Equal(0, (await Feed("/summary")).GetProperty("unread").GetInt32());
            As("manager", "Manager");
            Assert.Equal(1, (await Feed()).GetProperty("items").EnumerateArray().Count(n => n.GetProperty("type").GetString() == "crm-status"));
            // An agent's own update alerts managers, not the agent themselves.
            As("agent", "Agent");
            Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/Crm/leads/{leadId}/status", new { status = "lost" })).StatusCode);
            Assert.Equal(0, (await Feed("/summary")).GetProperty("unread").GetInt32());
            As("actor", "Manager");
            Assert.Equal(1, (await Feed("/summary")).GetProperty("unread").GetInt32());
            // Owners and anonymous requests cannot use the staff feed.
            As("agent", "Owner");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/Crm/notifications")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Notice-User");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Crm/notifications")).StatusCode);
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal(4, await db.CrmLeadNotifications.CountAsync());
                Assert.NotNull((await db.ListingInterestNotifications.FindAsync(matchId))!.ReadAt);
            }
        }
        finally
        {
            if (app is not null) await app.DisposeAsync();
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(configured);
            await connection.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", connection).ExecuteNonQueryAsync();
        }
    }
}

public class NoticeAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var id = Request.Headers["X-Notice-User"].ToString();
        if (string.IsNullOrEmpty(id)) return Task.FromResult(AuthenticateResult.NoResult());
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, Request.Headers["X-Notice-Role"].ToString()),
        ], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
