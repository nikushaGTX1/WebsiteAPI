using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class ListingInterestTests
{
    /// <summary>
    /// The owner of a listing gets one bell alert when a client's search matches it, from both
    /// directions (new listing, new search), and sees the client on their interest page.
    /// Listings that break a must-have and other people's listings never alert them.
    /// </summary>
    [LocalPostgresFact]
    public async Task OwnersHearOnceWhenAClientSearchMatchesTheirListing()
    {
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE"));
        Assert.Contains(cs.Host, new[] { "localhost", "127.0.0.1" });
        var database = "interest_test_" + Guid.NewGuid().ToString("N");
        await using (var maintenance = new NpgsqlConnection(cs.ConnectionString))
        {
            await maintenance.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {database}", maintenance).ExecuteNonQueryAsync();
        }
        cs.Database = database;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs.ConnectionString).Options;
        try
        {
            int matching, wrongDistrict, otherOwners, searchId;
            await using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.Roles.AddRange(
                    new Microsoft.AspNetCore.Identity.IdentityRole { Id = "client-role", Name = "Client", NormalizedName = "CLIENT" },
                    new Microsoft.AspNetCore.Identity.IdentityRole { Id = "owner-role", Name = "Owner", NormalizedName = "OWNER" });
                db.Users.AddRange(
                    new AppUser { Id = "client", UserName = "client", Email = "client@example.invalid" },
                    new AppUser { Id = "owner", UserName = "owner", Email = "owner@example.invalid" },
                    new AppUser { Id = "someone-else", UserName = "else", Email = "else@example.invalid" });
                db.UserRoles.AddRange(
                    new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = "client", RoleId = "client-role" },
                    new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = "owner", RoleId = "owner-role" });
                Apartment Listing(string district, string uploader) => new()
                {
                    Title = "Flat", Description = "Deal: For Rent", Price = 1200, District = district, Bedrooms = 2,
                    IsApproved = true, PublishedAt = DateTime.UtcNow, UploadedByUserId = uploader,
                };
                var a = Listing("Saburtalo", "owner");
                var b = Listing("Vake", "owner");
                var c = Listing("Saburtalo", "someone-else");
                db.Apartments.AddRange(a, b, c);
                await db.SaveChangesAsync();
                (matching, wrongDistrict, otherOwners) = (a.Id, b.Id, c.Id);
            }

            // 1. A client saves a search after the listings exist.
            await using (var db = new AppDbContext(options))
            {
                var search = new SavedSearch
                {
                    UserId = "client", Name = "Saburtalo", CriteriaHash = "h", CriteriaSince = DateTime.UtcNow,
                    ProfileJson = """{"propertyGoal":"Rent","districts":["Saburtalo"],"budgetMin":0,"budgetMax":1500,"currency":"USD","bedrooms":2,"bedroomChoices":[2],"adults":2,"hasPet":false,"moveInTiming":"Flexible","rentalDuration":"TwelveMonths","contactName":"Nino"}""",
                };
                db.SavedSearches.Add(search);
                await db.SaveChangesAsync();
                searchId = search.Id;
                var service = new ListingInterestService(db, NullLogger<ListingInterestService>.Instance);
                await service.NotifyForSearchAsync(searchId, CancellationToken.None);
                await service.NotifyForSearchAsync(searchId, CancellationToken.None); // twice: still one alert
            }

            // 2. The listing is processed again by the publish matcher: no second alert.
            await using (var db = new AppDbContext(options))
            {
                var interests = new ListingInterestService(db, NullLogger<ListingInterestService>.Instance);
                await new SavedSearchMatcher(db, NullLogger<SavedSearchMatcher>.Instance, interests)
                    .MatchApartmentAsync(matching, CancellationToken.None);
            }

            await using (var db = new AppDbContext(options))
            {
                var alerts = await db.ListingInterestNotifications.OrderBy(n => n.UserId).ToListAsync();
                // Each uploader of a matching listing hears once; the Vake listing alerts nobody.
                Assert.Equal(
                    [("owner", matching, searchId), ("someone-else", otherOwners, searchId)],
                    alerts.Select(alert => (alert.UserId, alert.ApartmentId, alert.SavedSearchId)));

                var page = await new ListingInterestService(db, NullLogger<ListingInterestService>.Instance)
                    .InterestsForAsync("owner", CancellationToken.None);
                Assert.Equal([matching, wrongDistrict], page.Select(listing => listing.ApartmentId).Order());
                var client = Assert.Single(page.Single(listing => listing.ApartmentId == matching).Clients);
                Assert.True(client.IsNew);
                Assert.Equal(2, (int)client.Profile["adults"]!);
                Assert.False(client.Profile.ContainsKey("contactName")); // only listed requirement fields are shared
                Assert.Empty(page.Single(listing => listing.ApartmentId == wrongDistrict).Clients);
                Assert.DoesNotContain(page, listing => listing.ApartmentId == otherOwners);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            cs.Database = "postgres";
            await using var maintenance = new NpgsqlConnection(cs.ConnectionString);
            await maintenance.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", maintenance).ExecuteNonQueryAsync();
        }
    }
}
