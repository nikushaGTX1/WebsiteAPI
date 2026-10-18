using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Website_API.Controllers;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class DealAmountFlowTests
{
    [LocalPostgresFact]
    public async Task WinningALeadEstimatesTheCommissionAndAManagerCanCorrectIt()
    {
        var configured = Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE");
        var cs = new NpgsqlConnectionStringBuilder(configured);
        Assert.Contains(cs.Host, new[] { "localhost", "127.0.0.1" });
        var database = "deal_test_" + Guid.NewGuid().ToString("N");
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
            builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs.ConnectionString));
            builder.Services.AddIdentityCore<AppUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
            builder.Services.AddControllers().AddApplicationPart(typeof(CrmController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, RoleHandler>("Test", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddMemoryCache();
            builder.Services.AddHttpClient<ResendEmailService>();
            builder.Services.AddHttpClient<SupabaseStorageService>();
            builder.Services.AddHttpClient<GoogleNearbyPlacesService>();
            app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            int rentLeadId, otherLeadId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                db.Users.Add(new AppUser { Id = "test-user", UserName = "manager", Email = "m@example.invalid" });
                var flat = new Apartment { Title = "Flat", Price = 1300, Description = "Nice\n\nDeal: For Rent | Currency: $", IsApproved = true };
                db.Apartments.Add(flat);
                await db.SaveChangesAsync();
                var rentLead = new CrmLead { Name = "Renter", Apartment = flat, AssignedAgentId = "test-user" };
                var other = new CrmLead { Name = "Other", AssignedAgentId = "test-user" };
                db.CrmLeads.AddRange(rentLead, other);
                await db.SaveChangesAsync();
                rentLeadId = rentLead.Id;
                otherLeadId = other.Id;
            }
            await app.StartAsync();
            using var client = app.GetTestClient();
            client.DefaultRequestHeaders.Add("X-Test-Role", "Manager");

            // Only won leads take an amount.
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/Crm/leads/{otherLeadId}/deal-amount", new { amountUsd = 100 })).StatusCode);

            var won = await client.PatchAsJsonAsync($"/api/Crm/leads/{rentLeadId}/status", new { status = "won" });
            Assert.Equal(HttpStatusCode.OK, won.StatusCode);
            Assert.Equal(1300m, (await won.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dealAmountUsd").GetDecimal());

            // No linked listing: nothing to estimate from.
            var otherWon = await client.PatchAsJsonAsync($"/api/Crm/leads/{otherLeadId}/status", new { status = "won" });
            Assert.Equal(JsonValueKind.Null, (await otherWon.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dealAmountUsd").ValueKind);

            var corrected = await client.PatchAsJsonAsync($"/api/Crm/leads/{rentLeadId}/deal-amount", new { amountUsd = 1450.555 });
            Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/Crm/leads/{rentLeadId}/deal-amount", new { amountUsd = -5 })).StatusCode);

            var list = await client.GetFromJsonAsync<JsonElement>("/api/Crm/leads");
            var item = list.EnumerateArray().First(x => x.GetProperty("id").GetInt32() == rentLeadId);
            Assert.Equal(1450.56m, item.GetProperty("dealAmountUsd").GetDecimal());
            Assert.NotEqual(JsonValueKind.Null, item.GetProperty("closedAt").ValueKind);
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
}
