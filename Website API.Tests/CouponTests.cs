using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Website_API.Controllers;
using Website_API.Data;
using Website_API.Models;
using Xunit;

namespace Website_API.Tests;

public class CouponTests
{
    [LocalPostgresFact]
    public async Task GameWinAddsOneCashbackCouponThatAnAgentCanRedeemOnce()
    {
        var configured = Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE");
        var cs = new NpgsqlConnectionStringBuilder(configured);
        Assert.Contains(cs.Host, new[] { "localhost", "127.0.0.1" });
        var database = "coupons_" + Guid.NewGuid().ToString("N");
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
            builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs.ConnectionString));
            builder.Services.AddIdentityCore<AppUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
            builder.Services.AddControllers().AddApplicationPart(typeof(CouponsController).Assembly);
            builder.Services.AddAuthentication("NoticeTest")
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, NoticeAuthHandler>("NoticeTest", _ => { });
            builder.Services.AddAuthorization();
            app = builder.Build();
            app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                foreach (var id in new[] { "client", "other", "agent" })
                    db.Users.Add(new AppUser { Id = id, UserName = id, FullName = id == "client" ? "Nino Client" : id });
                await db.SaveChangesAsync();
            }
            await app.StartAsync();
            using var client = app.GetTestClient();
            void As(string id, string role)
            {
                client.DefaultRequestHeaders.Remove("X-Notice-User"); client.DefaultRequestHeaders.Remove("X-Notice-Role");
                client.DefaultRequestHeaders.Add("X-Notice-User", id); client.DefaultRequestHeaders.Add("X-Notice-Role", role);
            }

            Assert.Equal(CouponsController.GameRewardFloor,
                (await client.GetFromJsonAsync<JsonElement>("/api/Coupons/game-rules")).GetProperty("rewardFloor").GetInt32());
            As("client", "Client");
            // Below the reward floor nothing is added.
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/Coupons/game-reward", new { floor = CouponsController.GameRewardFloor - 1 })).StatusCode);
            var first = await (await client.PostAsJsonAsync("/api/Coupons/game-reward", new { floor = 31 })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(first.GetProperty("created").GetBoolean());
            var code = first.GetProperty("coupon").GetProperty("code").GetString()!;
            Assert.Matches("^VLV-[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$", code);
            // Winning again returns the same coupon: one cashback per account.
            var again = await (await client.PostAsJsonAsync("/api/Coupons/game-reward", new { floor = 50 })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(again.GetProperty("created").GetBoolean());
            Assert.Equal(code, again.GetProperty("coupon").GetProperty("code").GetString());
            var mine = await client.GetFromJsonAsync<JsonElement>("/api/Coupons");
            Assert.Equal(1, mine.GetArrayLength());
            Assert.Equal(50m, mine[0].GetProperty("amountUsd").GetDecimal());
            Assert.Equal("active", mine[0].GetProperty("status").GetString());
            // Clients cannot look up or redeem codes.
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/Coupons/lookup/{code}")).StatusCode);

            As("other", "Client");
            Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/Coupons")).GetArrayLength());

            As("agent", "Agent");
            var found = await client.GetFromJsonAsync<JsonElement>($"/api/Coupons/lookup/{code.ToLowerInvariant()}");
            Assert.Equal("Nino Client", found.GetProperty("clientName").GetString());
            var couponId = found.GetProperty("coupon").GetProperty("id").GetInt32();
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/Coupons/{couponId}/redeem", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/Coupons/{couponId}/redeem", null)).StatusCode);

            As("client", "Client");
            Assert.Equal("redeemed", (await client.GetFromJsonAsync<JsonElement>("/api/Coupons"))[0].GetProperty("status").GetString());
            client.DefaultRequestHeaders.Remove("X-Notice-User");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Coupons")).StatusCode);
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
