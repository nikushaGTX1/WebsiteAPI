using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class SavedSearchMatchingTests
{
    /// <summary>
    /// Scenarios B, C and D: a matching listing creates exactly one match and one alert (however
    /// often the job runs), a listing that breaks a must-have creates none, and an unpublished
    /// listing disappears from the matches and from unread alerts.
    /// </summary>
    [LocalPostgresFact]
    public async Task PublishedListingsAlertOnceAndOnlyWhenTheyMeetEveryMustHave()
    {
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE"));
        Assert.Contains(cs.Host, new[] { "localhost", "127.0.0.1" });
        var database = "discover_test_" + Guid.NewGuid().ToString("N");
        await using (var maintenance = new NpgsqlConnection(cs.ConnectionString))
        {
            await maintenance.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {database}", maintenance).ExecuteNonQueryAsync();
        }
        cs.Database = database;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs.ConnectionString).Options;
        try
        {
            int searchId, matching, petsUnconfirmed, wrongDistrict, olderListing;
            await using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.Users.Add(new AppUser { Id = "client", UserName = "client", Email = "client@example.invalid", MatchAlertsInApp = true, MatchAlertsEmail = true });
                db.Roles.Add(new Microsoft.AspNetCore.Identity.IdentityRole { Id = "client-role", Name = "Client", NormalizedName = "CLIENT" });
                db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = "client", RoleId = "client-role" });
                var search = new SavedSearch
                {
                    UserId = "client",
                    Name = "Saburtalo rental",
                    ProfileJson = """{"propertyGoal":"Rent","districts":["Saburtalo"],"budgetMin":0,"budgetMax":1500,"currency":"USD","bedrooms":2,"bedroomChoices":[2],"hasPet":true,"petTypes":["Dog"],"dogSize":"Small","moveInTiming":"Flexible","rentalDuration":"TwelveMonths"}""",
                    CriteriaHash = "test",
                    CriteriaSince = DateTime.UtcNow.AddMinutes(-5),
                };
                db.SavedSearches.Add(search);
                Apartment Listing(string district, bool petFriendly, DateTime publishedAt) => new()
                {
                    Title = "Flat", Description = "Deal: For Rent", Price = 1200, District = district, Bedrooms = 2,
                    IsPetFriendly = petFriendly, IsApproved = true, PublishedAt = publishedAt,
                };
                var a = Listing("Saburtalo", true, DateTime.UtcNow);
                var b = Listing("Saburtalo", false, DateTime.UtcNow);
                var c = Listing("Vake", true, DateTime.UtcNow);
                var d = Listing("Saburtalo", true, DateTime.UtcNow.AddDays(-2));
                db.Apartments.AddRange(a, b, c, d);
                await db.SaveChangesAsync();
                (searchId, matching, petsUnconfirmed, wrongDistrict, olderListing) = (search.Id, a.Id, b.Id, c.Id, d.Id);
            }

            async Task Run(int apartmentId)
            {
                await using var db = new AppDbContext(options);
                await new SavedSearchMatcher(db, NullLogger<SavedSearchMatcher>.Instance).MatchApartmentAsync(apartmentId, CancellationToken.None);
            }

            // The same listing processed three times (retries, sweeps, overlapping runs).
            await Task.WhenAll(Run(matching), Run(matching));
            await Run(matching);
            await Run(petsUnconfirmed);
            await Run(wrongDistrict);
            await Run(olderListing);

            await using (var db = new AppDbContext(options))
            {
                var matches = await db.SavedSearchMatches.Where(m => m.SavedSearchId == searchId).ToListAsync();
                Assert.Equal([matching], matches.Select(m => m.ApartmentId));
                var alerts = await db.ClientNotifications.Where(n => n.UserId == "client").ToListAsync();
                var alert = Assert.Single(alerts);
                Assert.Equal(matching, alert.ApartmentId);
                Assert.Null(alert.ReadAt);
                Assert.True(alert.EmailPending);

                // A read alert survives history cleanup, but must not block a later match.
                alert.ReadAt = DateTime.UtcNow;
                var changed = await db.Apartments.FindAsync(matching);
                changed!.District = "Isani";
                await db.SaveChangesAsync();
            }
            await Run(matching);
            await using (var db = new AppDbContext(options))
            {
                Assert.False(await db.SavedSearchMatches.AnyAsync());
                Assert.False((await db.ClientNotifications.SingleAsync()).EmailPending);
                var changed = await db.Apartments.FindAsync(matching);
                changed!.District = "Saburtalo";
                await db.SaveChangesAsync();
            }
            await Run(matching);
            await using (var db = new AppDbContext(options))
            {
                Assert.Single(await db.SavedSearchMatches.ToListAsync());
                Assert.Single(await db.ClientNotifications.ToListAsync());
                // Unpublished: no active match; read history stays without pending email.
                var listing = await db.Apartments.FindAsync(matching);
                listing!.IsApproved = false;
                await db.SaveChangesAsync();
            }
            await Run(matching);
            await using (var db = new AppDbContext(options))
            {
                Assert.False(await db.SavedSearchMatches.AnyAsync());
                Assert.Single(await db.ClientNotifications.ToListAsync());
                Assert.False((await db.ClientNotifications.SingleAsync()).EmailPending);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var maintenance = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(cs.ConnectionString) { Database = "postgres" }.ConnectionString);
            await maintenance.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", maintenance).ExecuteNonQueryAsync();
        }
    }
}
