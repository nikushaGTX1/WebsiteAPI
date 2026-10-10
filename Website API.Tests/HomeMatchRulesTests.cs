using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public sealed class HomeMatchRulesTests
{
    [Theory]
    [InlineData(1000, "Rent", 1000)]
    [InlineData(1001, "Rent", 1251.25)]
    [InlineData(1400, "Rent", 1750)]
    [InlineData(2000, "Rent", 2500)]
    [InlineData(3000, "Rent", 3750)]
    [InlineData(800, "Rent", 800)]
    [InlineData(3000, "Buy", 3000)]
    public void RentalToleranceOnlyAboveOneThousand(decimal max, string goal, decimal expected)
    {
        Assert.Equal(expected, RentalBudgetPolicy.MaxRecommendedPrice(max, goal));
    }

    [Theory]
    [InlineData(1000, 1000, true)]
    [InlineData(1001, 1000, false)]
    [InlineData(1750, 1400, true)]
    [InlineData(1751, 1400, false)]
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
