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

public class ApartmentSubmissionMatchingTests
{
    private static readonly DateTime Today = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static Apartment Listing(string district = "Vake", int bedrooms = 2, decimal price = 1000, double size = 80) => new()
    {
        Title = "Apartment for rent", Description = "Deal: Rent | Pet-friendly: yes", District = district, Bedrooms = bedrooms,
        Price = price, SizeSquareMeters = size, IsPetFriendly = true, AvailableFrom = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
    };
    private static ClientRequirements Client() => ClientRequirements.Read(ClientBoardTests.Profile().GetRawText());

    [Fact]
    public void CompatibleListingPasses() => Assert.True(ApartmentSubmissionMatching.IsCompatible(Listing(), Client(), Today));

    [Fact]
    public void EveryHardRequirementExcludes()
    {
        var client = Client();
        Assert.False(ApartmentSubmissionMatching.IsCompatible(Listing(district: "Chugureti"), client, Today));
        Assert.False(ApartmentSubmissionMatching.IsCompatible(Listing(bedrooms: 1), client, Today));
        Assert.False(ApartmentSubmissionMatching.IsCompatible(Listing(price: 5000), client, Today));
        var sale = Listing(); sale.Description = "Deal: Sale"; sale.Title = "For sale";
        Assert.False(ApartmentSubmissionMatching.IsCompatible(sale, client, Today));
        var late = Listing(); late.AvailableFrom = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(ApartmentSubmissionMatching.IsCompatible(late, client, Today));
        var noPets = Listing(); noPets.IsPetFriendly = false; noPets.Description = "Deal: Rent";
        Assert.False(ApartmentSubmissionMatching.IsCompatible(noPets, client, Today));
        var small = Client(); small.MinSize = 100;
        Assert.False(ApartmentSubmissionMatching.IsCompatible(Listing(size: 80), small, Today));
        var crowded = Listing(); crowded.MaxOccupants = 1;
        Assert.False(ApartmentSubmissionMatching.IsCompatible(crowded, client, Today));
    }

    [Fact]
    public void OptionalPrioritiesDoNotExclude()
    {
        var client = Client(); client.TopPriorities = ["Parking", "QuietStreet", "MetroNearby"];
        var listing = Listing(); listing.HasParking = false;
        Assert.True(ApartmentSubmissionMatching.IsCompatible(listing, client, Today));
    }

    [Fact]
    public void UnknownPriceAndUnconfirmedPetsCannotSatisfyHardRequirements()
    {
        Assert.False(ApartmentSubmissionMatching.IsCompatible(Listing(price: 0), Client(), Today));
        var listing = Listing(); listing.IsPetFriendly = false; listing.Description = "Deal: Rent";
        Assert.False(ApartmentSubmissionMatching.IsCompatible(listing, Client(), Today));
    }

    [Fact]
    public void BuyerConditionMustBeConfirmedAndCompatible()
    {
        var client = Client(); client.PropertyGoal = "Buy"; client.PropertyConditions = ["WhiteFrame"];
        var listing = Listing(); listing.Description = "Deal: Sale | Condition: Black frame";
        Assert.False(ApartmentSubmissionMatching.IsCompatible(listing, client, Today));
        listing.Description = "Deal: Sale";
        Assert.False(ApartmentSubmissionMatching.IsCompatible(listing, client, Today));
        listing.Description = "Deal: Sale | Condition: White frame";
        Assert.True(ApartmentSubmissionMatching.IsCompatible(listing, client, Today));
        client.PropertyConditions = ["Any"];
        listing.Description = "Deal: Sale";
        Assert.True(ApartmentSubmissionMatching.IsCompatible(listing, client, Today));
    }

    [Fact]
    public void FlexibleDatesDoNotExcludeStatedAvailabilityAndCitiesMustMatch()
    {
        var client = Client(); client.MoveInTiming = "Flexible";
        var listing = Listing(); listing.AvailableFrom = Today.AddYears(1);
        Assert.True(ApartmentSubmissionMatching.IsCompatible(listing, client, Today));
        listing.City = "Batumi";
        Assert.False(ApartmentSubmissionMatching.IsCompatible(listing, client, Today));
        listing.City = "თბილისი";
        Assert.True(ApartmentSubmissionMatching.IsCompatible(listing, client, Today));
    }

}
