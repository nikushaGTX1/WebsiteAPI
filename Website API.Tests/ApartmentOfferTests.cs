using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
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
using Website_API.DTO;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class ApartmentOfferTests
{
    private static readonly DateTime Today = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static Apartment Listing(string district = "Vake", int bedrooms = 2, decimal price = 1000, double size = 80) => new()
    {
        Title = "Apartment for rent", Description = "Deal: Rent | Pet-friendly: yes", District = district, Bedrooms = bedrooms,
        Price = price, SizeSquareMeters = size, IsPetFriendly = true, AvailableFrom = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
    };
    private static ClientRequirements Client() => ClientRequirements.Read(ClientBoardTests.Profile().GetRawText());

    [Fact]
    public void CompatibleListingPasses() => Assert.True(ApartmentOfferMatching.IsCompatible(Listing(), Client(), Today));

    [Fact]
    public void EveryHardRequirementExcludes()
    {
        var client = Client();
        Assert.False(ApartmentOfferMatching.IsCompatible(Listing(district: "Chugureti"), client, Today));
        Assert.False(ApartmentOfferMatching.IsCompatible(Listing(bedrooms: 1), client, Today));
        Assert.False(ApartmentOfferMatching.IsCompatible(Listing(price: 5000), client, Today));
        var sale = Listing(); sale.Description = "Deal: Sale"; sale.Title = "For sale";
        Assert.False(ApartmentOfferMatching.IsCompatible(sale, client, Today));
        var late = Listing(); late.AvailableFrom = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(ApartmentOfferMatching.IsCompatible(late, client, Today));
        var noPets = Listing(); noPets.IsPetFriendly = false; noPets.Description = "Deal: Rent";
        Assert.False(ApartmentOfferMatching.IsCompatible(noPets, client, Today));
        var small = Client(); small.MinSize = 100;
        Assert.False(ApartmentOfferMatching.IsCompatible(Listing(size: 80), small, Today));
        var crowded = Listing(); crowded.MaxOccupants = 1;
        Assert.False(ApartmentOfferMatching.IsCompatible(crowded, client, Today));
    }

    [Fact]
    public void OptionalPrioritiesDoNotExclude()
    {
        var client = Client(); client.TopPriorities = ["Parking", "QuietStreet", "MetroNearby"];
        var listing = Listing(); listing.HasParking = false;
        Assert.True(ApartmentOfferMatching.IsCompatible(listing, client, Today));
    }

    [Fact]
    public void UnknownPriceAndUnconfirmedPetsCannotSatisfyHardRequirements()
    {
        Assert.False(ApartmentOfferMatching.IsCompatible(Listing(price: 0), Client(), Today));
        var listing = Listing(); listing.IsPetFriendly = false; listing.Description = "Deal: Rent";
        Assert.False(ApartmentOfferMatching.IsCompatible(listing, Client(), Today));
    }

    [Fact]
    public void BuyerConditionMustBeConfirmedAndCompatible()
    {
        var client = Client(); client.PropertyGoal = "Buy"; client.PropertyConditions = ["WhiteFrame"];
        var listing = Listing(); listing.Description = "Deal: Sale | Condition: Black frame";
        Assert.False(ApartmentOfferMatching.IsCompatible(listing, client, Today));
        listing.Description = "Deal: Sale";
        Assert.False(ApartmentOfferMatching.IsCompatible(listing, client, Today));
        listing.Description = "Deal: Sale | Condition: White frame";
        Assert.True(ApartmentOfferMatching.IsCompatible(listing, client, Today));
        client.PropertyConditions = ["Any"];
        listing.Description = "Deal: Sale";
        Assert.True(ApartmentOfferMatching.IsCompatible(listing, client, Today));
    }

    [LocalPostgresFact]
    public async Task PrivacySubmissionNotificationReviewAndSharingWorkInPostgres()
    {
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MYHOME_TEST_DATABASE"));
        Assert.Contains(cs.Host, new[] { "localhost", "127.0.0.1" });
        var database = "apartment_offers_" + Guid.NewGuid().ToString("N");
        await using var maintenance = new NpgsqlConnection(cs.ConnectionString);
        await maintenance.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {database}", maintenance).ExecuteNonQueryAsync();
        cs.Database = database;
        WebApplication? app = null;
        try
        {
            var browserScript = Environment.GetEnvironmentVariable("VELVEN_OFFER_BROWSER_SCRIPT");
            var builder = WebApplication.CreateBuilder();
            if (string.IsNullOrEmpty(browserScript)) builder.WebHost.UseTestServer();
            else builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Supabase:Url"] = "https://example.invalid", ["Supabase:SecretKey"] = "test", ["Supabase:Bucket"] = "apartments", ["GoogleMaps:ApiKey"] = "test" });
            builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs.ConnectionString));
            builder.Services.AddControllers().AddApplicationPart(typeof(ClientBoardController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ClientBoardRoleHandler>("Test", _ => { });
            builder.Services.AddAuthorization(); builder.Services.AddMemoryCache(); builder.Services.AddHttpClient<SupabaseStorageService>();
            builder.Services.AddScoped<ListingInterestService>();
            builder.Services.AddHttpClient<GoogleNearbyPlacesService>();
            builder.Services.AddOutputCache();
            builder.Services.AddSingleton<ApartmentMatchQueue>();
            builder.Services.AddScoped<ImageProcessingQueue>();
            builder.Services.Configure<ImageAiOptions>(_ => { });
            builder.Services.AddRateLimiter(o => o.AddPolicy("CrmInquiries", _ => RateLimitPartition.GetNoLimiter("test")));
            app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.MapControllers();
            int vake, chugureti, unshared, privateSearchId;
            long selected, compatible, incompatible, unpublished;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.MigrateAsync();
                db.Users.AddRange(new AppUser { Id = "agent-a", UserName = "a", FullName = "Agent A", PhoneNumber = "+995555000001" },
                    new AppUser { Id = "agent-b", UserName = "b", FullName = "Agent B" }, new AppUser { Id = "owner", UserName = "owner", FullName = "Owner" },
                    new AppUser { Id = "other-owner", UserName = "o2" }, new AppUser { Id = "match-client", UserName = "m" });
                db.Roles.AddRange(new IdentityRole { Id = "agent", Name = "Agent", NormalizedName = "AGENT" },
                    new IdentityRole { Id = "client", Name = "Client", NormalizedName = "CLIENT" });
                db.UserRoles.AddRange(new IdentityUserRole<string> { UserId = "agent-a", RoleId = "agent" }, new IdentityUserRole<string> { UserId = "agent-b", RoleId = "agent" });
                db.UserRoles.AddRange(new IdentityUserRole<string> { UserId = "owner", RoleId = "client" }, new IdentityUserRole<string> { UserId = "match-client", RoleId = "client" });
                // A self-serve Velven Match client: a saved search, never a board profile.
                db.SavedSearches.Add(new SavedSearch { UserId = "match-client", Name = "PrivateMatchSearch", CriteriaHash = "h", ProfileJson = ClientBoardTests.Profile().GetRawText() });
                var v = Listing(); v.UploadedByUserId = "owner"; v.IsApproved = false; var c = Listing(district: "Chugureti", bedrooms: 1); c.UploadedByUserId = "owner"; var u = Listing(); u.UploadedByUserId = "owner";
                db.Apartments.AddRange(v, c, u);
                var link = new ClientIntakeLink { Token = "token", AgentUserId = "agent-a" };
                var linkB = new ClientIntakeLink { Token = "token-b", AgentUserId = "agent-b" };
                ClientProfile Profile(ClientIntakeLink l, string district, bool published) => new()
                {
                    IntakeLink = l, AgentUserId = l.AgentUserId, FirstName = "PrivateFirstName", Phone = "555123456", ConsentAt = DateTime.UtcNow,
                    PublishConsentAt = published ? DateTime.UtcNow : null, ProfileJson = JsonSerializer.Serialize(ClientRequirements.Read(ClientBoardTests.Profile(district).GetRawText()), ClientRequirements.Json),
                    Districts = [district], BudgetMinUsd = 800, BudgetMaxUsd = 1400, Residents = 2, HasPets = true,
                    Lead = new CrmLead { Name = "PrivateFirstName", Phone = "555123456", AssignedAgentId = l.AgentUserId, Source = CrmLeadSource.AiMatch },
                };
                var a = Profile(link, "Vake", true); var b = Profile(linkB, "Vake", true); var d = Profile(linkB, "Gldani", true); var p = Profile(linkB, "Vake", false);
                db.ClientProfiles.AddRange(a, b, d, p);
                await db.SaveChangesAsync();
                privateSearchId = (await db.SavedSearches.SingleAsync()).Id;
                vake = v.Id; chugureti = c.Id; unshared = u.Id; selected = a.Id; compatible = b.Id; incompatible = d.Id; unpublished = p.Id;
                // Dates in the shared fixture are in the past: make the fixture's move-in irrelevant.
                await db.Apartments.ExecuteUpdateAsync(s => s.SetProperty(x => x.AvailableFrom, (DateTime?)null));
            }
            await app.StartAsync();
            using var client = string.IsNullOrEmpty(browserScript) ? app.GetTestClient() : new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            void As(string? role, string user) { client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Remove("X-Test-User"); if (role is not null) client.DefaultRequestHeaders.Add("X-Test-Role", role); client.DefaultRequestHeaders.Add("X-Test-User", user); }
            Task<HttpResponseMessage> Offer(long c, int a, bool share) => client.PostAsJsonAsync("/api/apartment-offers", new { clientId = c, apartmentId = a, shareWithOtherClients = share });

            // Scenarios 1-3: the board shows agent clients only, with no private details.
            As(null, "anonymous");
            var board = await client.GetStringAsync("/api/client-board");
            Assert.Contains($"\"id\":{selected}", board);
            Assert.DoesNotContain($"\"id\":{unpublished}", board);
            foreach (var secret in new[] { "PrivateMatchSearch", "match-client", "PrivateFirstName", "555123456" }) Assert.DoesNotContain(secret, board);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Offer(selected, vake, false)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Apartments/{vake}")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/client-profiles")).StatusCode);
            As("Client", "owner");
            Assert.DoesNotContain("PrivateMatchSearch", await client.GetStringAsync("/api/client/searches"));
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/client/searches/{privateSearchId}", new { name = "No access" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/Apartments/{vake}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/client-profiles")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Offer(unpublished, vake, false)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Offer(999999, vake, false)).StatusCode);
            As("Client", "other-owner");
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Apartments/{vake}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Offer(selected, vake, false)).StatusCode);

            // Scenarios 4-5: submit, no duplicates, the responsible agent is told.
            As("Client", "owner");
            var created = await (await Offer(selected, vake, true)).Content.ReadFromJsonAsync<JsonElement>();
            var sharedId = created.GetProperty("id").GetInt32();
            Assert.Equal("pending", created.GetProperty("status").GetString());
            Assert.Equal(HttpStatusCode.Conflict, (await Offer(selected, vake, true)).StatusCode);
            var unsharedId = (await (await Offer(selected, unshared, false)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
            var incompatibleId = (await (await Offer(selected, chugureti, true)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/apartment-offers/{sharedId}/approve", null)).StatusCode);
            As("Agent", "agent-a");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/Apartments/{vake}")).StatusCode);
            var bell = await client.GetStringAsync("/api/Crm/notifications");
            Assert.Contains("apartment-offer", bell); Assert.Contains($"/crm/apartment-offers?offer={sharedId}", bell); Assert.Contains($"\"clientId\":{selected}", bell);
            As("Agent", "agent-b");
            Assert.DoesNotContain("apartment-offer", await client.GetStringAsync("/api/Crm/notifications"));
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/apartment-offers/{sharedId}/approve", null)).StatusCode);

            // Scenarios 6-9: review, sharing off, sharing on, incompatible.
            As("Agent", "agent-a");
            async Task<int> Approve(int id) => (await (await client.PostAsync($"/api/apartment-offers/{id}/approve", null)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("matchedClients").GetInt32();
            Assert.Equal(0, await Approve(unsharedId));
            Assert.Equal(0, await Approve(incompatibleId));
            Assert.Equal(1, await Approve(sharedId));
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/apartment-offers/{sharedId}/reject", null)).StatusCode);
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var match = await db.ApartmentOfferMatches.SingleAsync();
                Assert.False((await db.Apartments.SingleAsync(x => x.Id == vake)).IsApproved);
                Assert.Equal(compatible, match.ClientProfileId); Assert.Equal("agent-b", match.AgentUserId); Assert.NotEqual(incompatible, match.ClientProfileId);
            }
            As("Agent", "agent-b");
            Assert.Contains("apartment-offer-match", await client.GetStringAsync("/api/Crm/notifications"));
            var theirs = await client.GetStringAsync("/api/apartment-offers/matches");
            Assert.Contains($"\"clientId\":{compatible}", theirs); Assert.DoesNotContain("PrivateFirstName", theirs);

            As(null, "anonymous");
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Apartments/{vake}")).StatusCode);
            As("Client", "owner");
            var rejectable = (await (await Offer(compatible, unshared, true)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
            As("Agent", "agent-b");
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/apartment-offers/{rejectable}/reject", null)).StatusCode);
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal(0, await db.ApartmentOfferMatches.CountAsync(x => x.ApartmentOfferId == rejectable));
            }

            if (!string.IsNullOrEmpty(browserScript))
            {
                int sharedListing, privateListing, rejectedListing;
                using (var scope = app.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    Apartment BrowserListing(string title) { var row = Listing(); row.Title = title; row.IsApproved = false; row.UploadedByUserId = "owner"; row.AvailableFrom = null; return row; }
                    var shared = BrowserListing("Browser shared apartment");
                    var onlySelected = BrowserListing("Browser selected client only");
                    var rejected = BrowserListing("Browser rejected apartment");
                    db.Apartments.AddRange(shared, onlySelected, rejected); await db.SaveChangesAsync();
                    sharedListing = shared.Id; privateListing = onlySelected.Id; rejectedListing = rejected.Id;
                }
                var start = new ProcessStartInfo("node") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(browserScript)))! };
                start.ArgumentList.Add(browserScript);
                start.ArgumentList.Add(JsonSerializer.Serialize(new { api = app.Urls.Single(), selected, compatible, incompatible, sharedListing, privateListing, rejectedListing }));
                using var browser = Process.Start(start)!;
                var stdout = browser.StandardOutput.ReadToEndAsync(); var stderr = browser.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                try { await browser.WaitForExitAsync(timeout.Token); }
                catch { browser.Kill(entireProcessTree: true); throw; }
                Assert.True(browser.ExitCode == 0, $"Browser journey failed:\n{await stdout}\n{await stderr}");
                using var scopeAfter = app.Services.CreateScope();
                var verified = scopeAfter.ServiceProvider.GetRequiredService<AppDbContext>();
                var sharedOffer = await verified.ApartmentOffers.Include(x => x.Matches).SingleAsync(x => x.ApartmentId == sharedListing);
                Assert.Equal(ApartmentOffer.Approved, sharedOffer.Status);
                Assert.Equal(compatible, Assert.Single(sharedOffer.Matches).ClientProfileId);
                Assert.Empty((await verified.ApartmentOffers.Include(x => x.Matches).SingleAsync(x => x.ApartmentId == privateListing)).Matches);
                Assert.Equal(ApartmentOffer.Rejected, (await verified.ApartmentOffers.SingleAsync(x => x.ApartmentId == rejectedListing)).Status);
                Assert.False((await verified.Apartments.SingleAsync(x => x.Id == sharedListing)).IsApproved);
            }
        }
        finally
        {
            if (app is not null) await app.DisposeAsync();
            NpgsqlConnection.ClearAllPools();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", maintenance).ExecuteNonQueryAsync();
        }
    }
}
