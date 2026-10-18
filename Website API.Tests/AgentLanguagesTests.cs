using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
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

public class AgentLanguagesTests
{
    [Theory]
    [InlineData(null, "ka,en,ru")]
    [InlineData("", "ka,en,ru")]
    [InlineData("en,tr", "en,tr")]
    [InlineData("xx,en, en ,de", "en,de")]
    [InlineData("xx", "ka,en,ru")]
    public void StoredLanguagesFallBackToGeorgianEnglishRussian(string? stored, string expected) =>
        Assert.Equal(expected, string.Join(",", AgentsController.ParseLanguages(stored)));

    [LocalPostgresFact]
    public async Task OnlyAdminsAndManagersSetLanguagesAndProfilesShowThem()
    {
        var configured = Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE");
        var cs = new NpgsqlConnectionStringBuilder(configured);
        Assert.Contains(cs.Host, new[] { "localhost", "127.0.0.1" });
        var database = "languages_test_" + Guid.NewGuid().ToString("N");
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
                ["Supabase:Bucket"] = "apartments"
            });
            builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs.ConnectionString));
            builder.Services.AddControllers().AddApplicationPart(typeof(AgentsController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, RoleHandler>("Test", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddMemoryCache();
            builder.Services.AddHttpClient<SupabaseStorageService>();
            app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                db.Users.Add(new AppUser { Id = "agent-1", UserName = "agent1@example.invalid", FullName = "Nika Agent", IsAgent = true });
                db.Users.Add(new AppUser { Id = "client-1", UserName = "client@example.invalid", FullName = "Client" });
                await db.SaveChangesAsync();
            }
            await app.StartAsync();
            using var client = app.GetTestClient();
            var body = new { languages = new[] { "tr", "en", "ka", "en" } };

            // Never set: the profile keeps Georgian, English and Russian.
            Assert.Equal("ka,en,ru", await Languages(client));

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/Agents/agent-1/languages", body)).StatusCode);
            foreach (var role in new[] { "Agent", "Uploader", "User" })
            {
                AsRole(client, role);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/Agents/agent-1/languages", body)).StatusCode);
            }

            AsRole(client, "Manager");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/Agents/agent-1/languages", new { languages = Array.Empty<string>() })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/Agents/agent-1/languages", new { languages = new[] { "en", "klingon" } })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/Agents/client-1/languages", body)).StatusCode);

            var saved = await client.PutAsJsonAsync("/api/Agents/agent-1/languages", body);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            // Duplicates removed, stored in the fixed display order.
            Assert.Equal("ka,en,tr", string.Join(",", (await saved.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("languages").EnumerateArray().Select(x => x.GetString())));
            Assert.Equal("ka,en,tr", await Languages(client));

            AsRole(client, "Admin");
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/Agents/agent-1/languages", new { languages = new[] { "ru" } })).StatusCode);
            Assert.Equal("ru", await Languages(client));
            var list = await client.GetFromJsonAsync<JsonElement>("/api/Agents");
            Assert.Equal("ru", string.Join(",", list[0].GetProperty("languages").EnumerateArray().Select(x => x.GetString())));
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

    private static void AsRole(HttpClient client, string role)
    {
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
    }

    private static async Task<string> Languages(HttpClient client)
    {
        var agent = await client.GetFromJsonAsync<JsonElement>("/api/Agents/agent-1");
        return string.Join(",", agent.GetProperty("languages").EnumerateArray().Select(x => x.GetString()));
    }
}
