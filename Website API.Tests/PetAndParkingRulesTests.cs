using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

/// <summary>Client QA (Nov 2026): pets are a hard filter and parking has one answer everywhere.</summary>
public class PetAndParkingRulesTests
{
    private static Apartment Home(bool petFriendly, string description = "Deal: For Rent") =>
        new() { Title = "Apartment for rent", Description = description, IsPetFriendly = petFriendly };

    [Fact]
    public void UntickedPetFriendlyBoxIsANo()
    {
        Assert.Equal("prohibited", HomeMatchRequirements.PetPermission(Home(false), ["Dog"], "Medium"));
        Assert.Equal("allowed", HomeMatchRequirements.PetPermission(Home(false), [], null));
    }

    [Fact]
    public void PetTypeAndSizeLinesDecide()
    {
        var home = Home(true, "Deal: For Rent | Pets allowed: Dog | Pet sizes: Small, Medium");
        Assert.Equal("allowed", HomeMatchRequirements.PetPermission(home, ["Dog"], "Medium"));
        Assert.Equal("prohibited", HomeMatchRequirements.PetPermission(home, ["Dog"], "Large"));
        Assert.Equal("prohibited", HomeMatchRequirements.PetPermission(home, ["Cat"], null));
        // Pet friendly but the dog size is not stated: check with the agent.
        Assert.Equal("unconfirmed", HomeMatchRequirements.PetPermission(Home(true), ["Dog"], "Medium"));
    }

    [Fact]
    public void ParkingTypeOrDescriptionCountsAsParking()
    {
        Assert.True(ListingParking.Has(new Apartment { ParkingCondition = "Garage" }));
        Assert.True(ListingParking.Has(new Apartment { Description = "One parking space in a closed parking lot comes with the apartment." }));
        Assert.False(ListingParking.Has(new Apartment { Description = "No parking, street only." }));
        Assert.False(ListingParking.Has(new Apartment { ParkingCondition = "StreetParking" }));
        // Zone/street parking with the box ticked is still not private parking.
        Assert.False(ListingParking.Has(new Apartment { HasParking = true, ParkingCondition = "StreetParking" }));
    }
}
