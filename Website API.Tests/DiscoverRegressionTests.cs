using Website_API.Controllers;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class DiscoverRegressionTests
{
    [Theory]
    [InlineData("{\"propertyGoal\":\"Rent\",\"districts\":null}")]
    [InlineData("{\"propertyGoal\":\"Rent\",\"bedroomChoices\":null}")]
    [InlineData("{\"propertyGoal\":\"Rent\",\"petTypes\":null}")]
    [InlineData("{\"propertyGoal\":\"Rent\",\"selectedMapArea\":{\"type\":\"Polygon\",\"coordinates\":[[[]]]}}")]
    [InlineData("{\"propertyGoal\":\"Rent\",\"budgetMin\":2000,\"budgetMax\":1000}")]
    public void InvalidCriteriaAreRejected(string json) => Assert.False(SavedSearchProfile.IsValid(json));

    [Theory]
    [InlineData("იყიდება ბინა", "Buy")]
    [InlineData("Квартира в аренду", "Rent")]
    [InlineData("ქირავდება ბინა", "Rent")]
    [InlineData("Продажа квартиры", "Buy")]
    public void LocalizedDealTypesMatch(string title, string goal) => Assert.True(
        HomeMatchRequirements.MatchesPropertyGoal(new Apartment { Title = title }, new SavedSearchProfile { PropertyGoal = goal }));

    [Fact]
    public void SavedBudgetUsesItsConversionSnapshot()
    {
        var profile = SavedSearchProfile.Parse("""{"propertyGoal":"Rent","currency":"GEL","budgetMin":0,"budgetMax":3000,"budgetConversion":{"currency":"GEL","min":0,"max":3000,"usdGel":3,"usdEur":1}}""");
        Assert.Equal(1000m, HomeMatchRequirements.BudgetRangeUsd(profile).Max);
        profile.BudgetMax = 2700;
        Assert.Equal(1000m, HomeMatchRequirements.BudgetRangeUsd(profile).Max);
    }

    [Fact]
    public void ExchangeSnapshotDoesNotCreateDuplicateSearches() => Assert.Equal(
        ClientController.CriteriaHash("""{"propertyGoal":"Rent","budgetMax":1000}"""),
        ClientController.CriteriaHash("""{"propertyGoal":"Rent","budgetMax":1000,"budgetConversion":{"usdGel":3}}"""));
}
