using Microsoft.AspNetCore.Identity;
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
    /// A client search that matches a listing becomes one pending match for the agent who listed
    /// it (from both directions: new search, listing re-processed). The owner sees nothing until
    /// that agent marks it ready, then gets the client's requirements with the agent's phone, never
    /// the client's contact details. Other agents cannot review it; listings that break a must-have
    /// create nothing.
    /// </summary>
    [LocalPostgresFact]
    public async Task OwnersHearOnlyAfterTheAgentMarksTheClientReady()
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
        ListingInterestService Service(AppDbContext db) => new(db, NullLogger<ListingInterestService>.Instance);
        try
        {
            int fromOwnerLink, wrongDistrict, listedByOwner, searchId;
            await using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.Roles.AddRange(
                    new IdentityRole { Id = "owner-role", Name = "Owner", NormalizedName = "OWNER" },
                    new IdentityRole { Id = "agent-role", Name = "Agent", NormalizedName = "AGENT" });
                db.Users.AddRange(
                    new AppUser { Id = "client", UserName = "client", Email = "client@example.invalid", PhoneNumber = "+995500000001", FullName = "Nino Client" },
                    new AppUser { Id = "owner", UserName = "owner", Email = "owner@example.invalid", NormalizedEmail = "OWNER@EXAMPLE.INVALID" },
                    new AppUser { Id = "agent", UserName = "agent", Email = "agent@example.invalid", PhoneNumber = "+995555123123", FullName = "Andria Agent" },
                    new AppUser { Id = "other-agent", UserName = "other", Email = "other@example.invalid" });
                db.UserRoles.AddRange(
                    new IdentityUserRole<string> { UserId = "owner", RoleId = "owner-role" },
                    new IdentityUserRole<string> { UserId = "agent", RoleId = "agent-role" },
                    new IdentityUserRole<string> { UserId = "other-agent", RoleId = "agent-role" });
                Apartment Listing(string district, string uploader) => new()
                {
                    Title = "Flat", Description = "Deal: For Rent", Price = 1200, District = district, Bedrooms = 2,
                    IsApproved = true, PublishedAt = DateTime.UtcNow, UploadedByUserId = uploader,
                };
                var a = Listing("Saburtalo", "agent");
                var b = Listing("Vake", "owner");
                var c = Listing("Saburtalo", "owner");
                db.Apartments.AddRange(a, b, c);
                await db.SaveChangesAsync();
                var link = new OwnerListingLink { Token = "t", AgentUserId = "agent" };
                db.OwnerListingLinks.Add(link);
                await db.SaveChangesAsync();
                db.OwnerSubmissions.Add(new OwnerSubmission
                {
                    LinkId = link.Id, AgentUserId = "agent", OwnerName = "Giorgi", OwnerPhone = "+995599000000",
                    OwnerEmail = "owner@example.invalid", Status = "published", PublishedApartmentId = a.Id,
                });
                await db.SaveChangesAsync();
                (fromOwnerLink, wrongDistrict, listedByOwner) = (a.Id, b.Id, c.Id);
            }

            // A client saves a search after the listings exist (twice: still one match each).
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
                await Service(db).NotifyForSearchAsync(searchId, CancellationToken.None);
                await Service(db).NotifyForSearchAsync(searchId, CancellationToken.None);
            }
            // The publish matcher sees the same listing again: no second match.
            await using (var db = new AppDbContext(options))
                await new SavedSearchMatcher(db, NullLogger<SavedSearchMatcher>.Instance, Service(db))
                    .MatchApartmentAsync(fromOwnerLink, CancellationToken.None);

            int linkMatch, ownerMatch;
            await using (var db = new AppDbContext(options))
            {
                var matches = await db.ListingInterestNotifications.OrderBy(n => n.ApartmentId).ToListAsync();
                Assert.Equal([fromOwnerLink, listedByOwner], matches.Select(n => n.ApartmentId));
                Assert.All(matches, n => Assert.Equal(ListingInterestNotification.Pending, n.Status));
                var viaLink = matches[0];
                Assert.Equal(("agent", "owner", "owner@example.invalid"), (viaLink.ReviewerUserId, viaLink.OwnerUserId, viaLink.OwnerEmail));
                Assert.Equal(("owner", null), (matches[1].OwnerUserId, matches[1].ReviewerUserId)); // a manager reviews
                (linkMatch, ownerMatch) = (viaLink.Id, matches[1].Id);

                // Before review the owner sees no clients.
                var before = await Service(db).InterestsForAsync("owner", CancellationToken.None);
                Assert.All(before, listing => Assert.Empty(listing.Clients));

                // The agent sees the client's contact details; another agent sees nothing.
                var review = Assert.Single(await Service(db).ReviewsAsync("agent", false, ListingInterestNotification.Pending, CancellationToken.None));
                Assert.Equal(("Nino Client", "+995500000001"), (review.ClientName, review.ClientPhone));
                Assert.Empty(await Service(db).ReviewsAsync("other-agent", false, ListingInterestNotification.Pending, CancellationToken.None));
                Assert.Null(await Service(db).ReviewAsync(linkMatch, "other-agent", false, true, CancellationToken.None));
                Assert.Null(await Service(db).ReviewAsync(ownerMatch, "agent", false, true, CancellationToken.None));
                Assert.Equal(2, (await Service(db).ReviewsAsync("manager", true, ListingInterestNotification.Pending, CancellationToken.None)).Count);

                var result = await Service(db).ReviewAsync(linkMatch, "agent", false, true, CancellationToken.None);
                Assert.Equal(ListingInterestNotification.Ready, result!.Status);
            }

            await using (var db = new AppDbContext(options))
            {
                var page = await Service(db).InterestsForAsync("owner", CancellationToken.None);
                Assert.Equal(new[] { fromOwnerLink, listedByOwner, wrongDistrict }.Order(), page.Select(l => l.ApartmentId).Order());
                var client = Assert.Single(page.Single(l => l.ApartmentId == fromOwnerLink).Clients);
                Assert.Equal(("Andria Agent", "+995555123123"), (client.AgentName, client.AgentPhone));
                Assert.Equal(2, (int)client.Profile["adults"]!);
                Assert.False(client.Profile.ContainsKey("contactName")); // only requirement fields reach the owner
                Assert.Empty(page.Single(l => l.ApartmentId == listedByOwner).Clients); // still waiting for a manager
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
