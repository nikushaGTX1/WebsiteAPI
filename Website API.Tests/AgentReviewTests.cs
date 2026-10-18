using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using System.Threading.RateLimiting;
using Website_API.Controllers;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class AgentReviewTests
{
    [Theory]
    [InlineData("Giorgi Lomidze", "giorgi-lomidze")]
    [InlineData("გიორგი ლომიძე", "giorgi-lomidze")]
    [InlineData("ზუკა ღვინიაშვილი", "zuka-ghviniashvili")]
    [InlineData("  Électron Cyber Arena!! ", "electron-cyber-arena")]
    [InlineData("!!!", "agent")]
    public void SlugIsLatinAndUrlSafe(string name, string expected) =>
        Assert.Equal(expected, ReviewsController.CreateSlug(name));

    [LocalPostgresFact]
    public async Task PublicLinkStoresReviewsForTheRightAgentOnlyAndOwnerSeesOwnDashboard()
    {
        var configured = Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE");
        var cs = new NpgsqlConnectionStringBuilder(configured);
        Assert.Contains(cs.Host, new[] { "localhost", "127.0.0.1" });
        var database = "reviews_test_" + Guid.NewGuid().ToString("N");
        await using (var maintenance = new NpgsqlConnection(cs.ConnectionString))
        {
            await maintenance.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {database}", maintenance).ExecuteNonQueryAsync();
        }
        cs.Database = database;
        WebApplication? app = null;
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supabase:Url"] = "https://example.invalid",
                ["Supabase:SecretKey"] = "test",
                ["Supabase:Bucket"] = "apartments",
                ["Jwt:Key"] = "test-salt"
            });
            builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs.ConnectionString));
            builder.Services.AddControllers().AddApplicationPart(typeof(ReviewsController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ReviewUserHandler>("Test", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddMemoryCache();
            builder.Services.AddHttpClient<SupabaseStorageService>();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("PublicReviews", context => RateLimitPartition.GetFixedWindowLimiter(
                    ReviewsController.ClientIp(context),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
            });
            app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.MapControllers();
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                db.Users.Add(new AppUser { Id = "agent-a", UserName = "a@example.invalid", FullName = "გიორგი ლომიძე", IsAgent = true });
                db.Users.Add(new AppUser { Id = "agent-b", UserName = "b@example.invalid", FullName = "Giorgi Lomidze", IsAgent = true });
                db.Users.Add(new AppUser { Id = "customer", UserName = "c@example.invalid", FullName = "Client" });
                await db.SaveChangesAsync();
            }
            await app.StartAsync();
            using var client = app.GetTestClient();

            // Dashboard needs sign-in; a plain customer account cannot open one.
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Reviews/mine")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await As(client, "customer").GetAsync("/api/Reviews/mine")).StatusCode);

            // Both agents get permanent, distinct slugs (same transliterated name).
            var dashA = await GetJson(As(client, "agent-a"), "/api/Reviews/mine");
            var dashB = await GetJson(As(client, "agent-b"), "/api/Reviews/mine");
            var slugA = dashA.GetProperty("slug").GetString()!;
            var slugB = dashB.GetProperty("slug").GetString()!;
            Assert.Equal("giorgi-lomidze", slugA);
            Assert.Equal("giorgi-lomidze-2", slugB);
            Assert.Equal(slugA, (await GetJson(As(client, "agent-a"), "/api/Reviews/mine")).GetProperty("slug").GetString());

            Anonymous(client, "10.0.0.1");
            var link = await GetJson(client, $"/api/Reviews/link/{slugA}");
            Assert.Equal("გიორგი ლომიძე", link.GetProperty("agentName").GetString());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/Reviews/link/no-such-agent")).StatusCode);

            // Validation: rating required, bots too fast, links rejected.
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, slugA, new { rating = 0, submissionId = Guid.NewGuid(), elapsedMs = 5000 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, slugA, new { rating = 5, submissionId = Guid.NewGuid(), elapsedMs = 100 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, slugA, new { rating = 5, comment = "visit https://spam.example", submissionId = Guid.NewGuid(), elapsedMs = 5000 })).StatusCode);

            Anonymous(client, "10.0.0.2");
            // A filled honeypot looks successful but stores nothing.
            Assert.Equal(HttpStatusCode.OK, (await Post(client, slugA, new { rating = 1, website = "bot", submissionId = Guid.NewGuid(), elapsedMs = 5000 })).StatusCode);
            // The same submission sent twice (double click) is stored once.
            var submission = Guid.NewGuid();
            var payload = new { rating = 5, comment = "Great agent,\nvery helpful", customerName = "Nino Beridze", submissionId = submission, elapsedMs = 4000 };
            var twice = await Task.WhenAll(Post(client, slugA, payload), Post(client, slugA, payload));
            Assert.All(twice, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.Equal(HttpStatusCode.OK, (await Post(client, slugA, new { rating = 3, submissionId = Guid.NewGuid(), elapsedMs = 4000 })).StatusCode);

            Anonymous(client, "10.0.0.3");
            // Body fields cannot redirect a review to another agent: the link decides.
            Assert.Equal(HttpStatusCode.OK, (await Post(client, slugB, new { rating = 4, agentId = "agent-a", submissionId = Guid.NewGuid(), elapsedMs = 4000 })).StatusCode);
            // Per-network daily limit for one agent.
            for (var i = 0; i < 3; i++)
                Assert.Equal(HttpStatusCode.OK, (await Post(client, slugA, new { rating = 4, submissionId = Guid.NewGuid(), elapsedMs = 4000 })).StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(client, slugA, new { rating = 4, submissionId = Guid.NewGuid(), elapsedMs = 4000 })).StatusCode);

            // The agent cannot review themselves.
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(As(client, "agent-a"), slugA, new { rating = 5, submissionId = Guid.NewGuid(), elapsedMs = 4000 }, "10.0.0.9")).StatusCode);

            var dashboard = await GetJson(As(client, "agent-a"), "/api/Reviews/mine");
            var summary = dashboard.GetProperty("summary");
            Assert.Equal(5, summary.GetProperty("total").GetInt32());
            Assert.Equal(new[] { 0, 0, 1, 3, 1 }, summary.GetProperty("distribution").EnumerateArray().Select(x => x.GetInt32()).ToArray());
            Assert.Equal(4.0, summary.GetProperty("average").GetDouble());
            var reviews = dashboard.GetProperty("reviews").EnumerateArray().ToList();
            Assert.Equal(5, reviews.Count);
            Assert.DoesNotContain("ipHash", dashboard.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("submissionId", dashboard.GetRawText(), StringComparison.OrdinalIgnoreCase);

            var fiveStar = await GetJson(As(client, "agent-a"), "/api/Reviews/mine?rating=5");
            var only = Assert.Single(fiveStar.GetProperty("reviews").EnumerateArray());
            Assert.Equal("Nino Beridze", only.GetProperty("customerName").GetString());
            Assert.Equal("Great agent,\nvery helpful", only.GetProperty("comment").GetString());
            var oldest = await GetJson(As(client, "agent-a"), "/api/Reviews/mine?sort=oldest");
            Assert.Equal(5, oldest.GetProperty("reviews")[0].GetProperty("rating").GetInt32());

            // Agent B sees only their own review; an agent cannot open another agent's dashboard.
            var other = await GetJson(As(client, "agent-b"), "/api/Reviews/mine?agentId=agent-a");
            Assert.Equal("agent-b", other.GetProperty("agentId").GetString());
            Assert.Equal(1, other.GetProperty("summary").GetProperty("total").GetInt32());
        }
        finally
        {
            if (app is not null) await app.DisposeAsync();
            NpgsqlConnection.ClearAllPools();
            await using var maintenance = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(configured).ConnectionString);
            await maintenance.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", maintenance).ExecuteNonQueryAsync();
        }
    }

    private static HttpClient As(HttpClient client, string userId)
    {
        client.DefaultRequestHeaders.Remove("X-Test-User");
        client.DefaultRequestHeaders.Add("X-Test-User", userId);
        return client;
    }

    private static void Anonymous(HttpClient client, string ip)
    {
        client.DefaultRequestHeaders.Remove("X-Test-User");
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string slug, object body, string? ip = null)
    {
        if (ip is not null)
        {
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        }
        return client.PostAsJsonAsync($"/api/Reviews/link/{slug}", body);
    }

    private static async Task<JsonElement> GetJson(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}

/// <summary>Signs the request in as the user id in X-Test-User (role Agent for agent-* ids).</summary>
public class ReviewUserHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-Test-User"].ToString();
        if (string.IsNullOrEmpty(userId)) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<System.Security.Claims.Claim> { new(System.Security.Claims.ClaimTypes.NameIdentifier, userId) };
        if (userId.StartsWith("agent-")) claims.Add(new(System.Security.Claims.ClaimTypes.Role, "Agent"));
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
