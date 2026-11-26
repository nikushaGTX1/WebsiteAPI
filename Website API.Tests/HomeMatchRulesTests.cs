using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public sealed class HomeMatchRulesTests
{
    [Theory]
    [InlineData(400, "Rent", 500)]
    [InlineData(600, "Rent", 750)]
    [InlineData(1000, "Rent", 1200)]
    [InlineData(1400, "Rent", 1650)]
    [InlineData(2450, "Rent", 2750)]
    [InlineData(3000, "Rent", 3300)]
    [InlineData(3000, "Buy", 3000)]
    public void RentalToleranceIsFixedDollarsAtMostThreeHundred(decimal max, string goal, decimal expected)
    {
        Assert.Equal(expected, RentalBudgetPolicy.MaxRecommendedPrice(max, goal));
    }

    [Theory]
    [InlineData(1200, 1000, true)]
    [InlineData(1201, 1000, false)]
    [InlineData(2750, 2450, true)]
    [InlineData(3000, 2450, false)]
    [InlineData(5000, 0, true)]
    public void CeilingFilter(decimal price, decimal max, bool expected)
    {
        Assert.Equal(expected, RentalBudgetPolicy.IsWithinCeiling(price, max, "Rent"));
    }

    [Theory]
    [InlineData("Cozy flat. Pet friendly: No", "Cat", null, true)]
    [InlineData("No dogs please", "Dog", "Small", true)]
    [InlineData("No dogs please", "Cat", null, false)]
    [InlineData("Small dogs only", "Dog", "Large", true)]
    [InlineData("Small dogs only", "Dog", "Small", false)]
    [InlineData("Bright apartment near the park", "Dog", "Large", false)]
    [InlineData("შინაური ცხოველები არ დაიშვება", "Cat", null, true)]
    public void PetProhibitionComesFromListingText(string description, string pet, string? dogSize, bool prohibited)
    {
        var apartment = new Apartment { Title = "Apartment", Description = description };
        Assert.Equal(prohibited, PetPolicy.IsProhibited(apartment, [pet], dogSize));
    }

    [Fact]
    public void UntickedPetFriendlyBoxIsNotAProhibition()
    {
        var apartment = new Apartment { Title = "Apartment", Description = "Nice home", IsPetFriendly = false };
        Assert.False(PetPolicy.IsProhibited(apartment, ["Dog", "Cat"], "Medium"));
    }
}
