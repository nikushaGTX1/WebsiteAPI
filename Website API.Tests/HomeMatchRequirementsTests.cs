using Website_API.Controllers;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

/// <summary>
/// Same cases as the website's mandatory-requirements.spec.ts: a saved search must alert about
/// exactly the homes the website lists as exact matches.
/// </summary>
public class HomeMatchRequirementsTests
{
    private static readonly DateTime Today = new(2026, 10, 23);

    private static SavedSearchProfile Profile(Action<SavedSearchProfile>? change = null)
    {
        var profile = new SavedSearchProfile
        {
            PropertyGoal = "Rent",
            Districts = ["Saburtalo"],
            BudgetMin = 500,
            BudgetMax = 1500,
            Currency = "USD",
            Bedrooms = 1,
            RentalDuration = "TwelveMonths",
            MoveInTiming = "Immediately",
        };
        change?.Invoke(profile);
        return profile;
    }

    private static Apartment Listing(Action<Apartment>? change = null)
    {
        var apartment = new Apartment
        {
            Id = 1,
            Title = "Apartment for rent",
            Description = "Deal: For Rent",
            Price = 1000,
            District = "Saburtalo",
            Bedrooms = 1,
            IsPetFriendly = true,
            IsApproved = true,
        };
        change?.Invoke(apartment);
        return apartment;
    }

    private static RequirementStatus Evaluate(Apartment apartment, SavedSearchProfile profile) =>
        HomeMatchRequirements.Evaluate(apartment, profile, Today);

    [Fact]
    public void HomeInsideEveryRequirementIsExact() =>
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(), Profile()));

    [Fact]
    public void BudgetBelowMinimumAndInsideRange()
    {
        var profile = Profile(p => { p.BudgetMin = 1300; p.BudgetMax = 1500; });
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.Price = 850), profile));
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.Price = 1400), profile));
    }

    [Fact]
    public void GelBudgetIsConvertedBeforeComparing()
    {
        // GEL 4,500 is about $1,667: $3,600 is over even the 25% rental tolerance.
        var profile = Profile(p => { p.Currency = "GEL"; p.BudgetMin = 0; p.BudgetMax = 4500; });
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.Price = 3600), profile));
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.Price = 1500), profile));
    }

    [Fact]
    public void OverBudgetRentalInsideToleranceNeedsConfirmingNotAlerting()
    {
        var profile = Profile(p => { p.BudgetMin = 0; p.BudgetMax = 1400; });
        Assert.Equal(RequirementStatus.Confirm, Evaluate(Listing(a => a.Price = 1600), profile));
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.Price = 1400), profile));
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.Price = 1751), profile));
    }

    [Fact]
    public void WrongDistrictIsNeverAMatch() =>
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.District = "Vake"), Profile()));

    [Fact]
    public void GeorgianDistrictNamesAndMultiWordCodesMatch()
    {
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.District = "საბურთალო"), Profile()));
        var didi = Profile(p => p.Districts = ["DidiDighomi"]);
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.District = "Didi Digomi"), didi));
    }

    [Fact]
    public void MapAreaUsesTheListingPin()
    {
        var profile = Profile(p =>
        {
            p.Districts = [];
            p.SelectedMapArea = new SavedSearchProfile.MapArea
            {
                Type = "Polygon",
                Coordinates = [[[44.70, 41.70], [44.80, 41.70], [44.80, 41.80], [44.70, 41.80], [44.70, 41.70]]],
            };
        });
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => { a.District = "Other"; a.PropertyLatitude = 41.75; a.PropertyLongitude = 44.75; }), profile));
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => { a.District = "Other"; a.PropertyLatitude = 41.90; a.PropertyLongitude = 44.75; }), profile));
    }

    [Fact]
    public void RentSearchesNeverMatchSalesAndTheOtherWayRound()
    {
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.Description = "Deal: For Sale"), Profile()));
        var buy = Profile(p => { p.PropertyGoal = "Buy"; p.BudgetMin = 0; p.BudgetMax = 200000; });
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => { a.Description = "Deal: For Sale"; a.Price = 150000; }), buy));
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(), buy));
    }

    [Fact]
    public void PetsMustBeExplicitlyAllowed()
    {
        var dog = Profile(p => { p.HasPet = true; p.PetType = "Dog"; });
        // Not confirmed: never alerted, as required for uncertain mandatory requirements.
        Assert.Equal(RequirementStatus.Confirm, Evaluate(Listing(a => a.IsPetFriendly = false), dog));
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(), dog));
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => { a.IsPetFriendly = false; a.Description = "Deal: For Rent | Pets allowed"; }), dog));

        var dogAndCat = Profile(p => { p.HasPet = true; p.PetTypes = ["Dog", "Cat"]; p.DogSize = "Medium"; });
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.Description = "Deal: For Rent | No cats"), dogAndCat));

        var largeDog = Profile(p => { p.PetTypes = ["Dog"]; p.DogSize = "Large"; });
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.Description = "Deal: For Rent | Small dogs only"), largeDog));
        var smallDog = Profile(p => { p.PetTypes = ["Dog"]; p.DogSize = "Small"; });
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.Description = "Deal: For Rent | Small dogs only"), smallDog));

        var noPets = Profile(p => { p.HasPet = false; p.PetType = "None"; });
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.Description = "Deal: For Rent | No pets"), noPets));
    }

    [Fact]
    public void BedroomChoices()
    {
        var single = Profile(p => { p.Bedrooms = 2; p.BedroomChoices = [2]; });
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.Bedrooms = 3), single));
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.Bedrooms = 1), single));

        var several = Profile(p => { p.Bedrooms = 1; p.BedroomChoices = [1, 2, 4]; });
        foreach (var bedrooms in new[] { 1, 2, 5 })
            Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.Bedrooms = bedrooms), several));
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.Bedrooms = 3), several));

        // Older saved profiles only had `bedrooms`.
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.Bedrooms = 1), Profile(p => p.Bedrooms = 2)));
    }

    [Fact]
    public void MinimumLeaseAndAvailability()
    {
        var sixMonths = Profile(p => p.RentalDuration = "SixMonths");
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.MinimumRentalPeriod = "Minimum 12 months"), sixMonths));
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.MinimumRentalPeriod = "Minimum 6 months"), sixMonths));
        var unsure = Profile(p => p.RentalDuration = "Unknown");
        Assert.Equal(RequirementStatus.Confirm, Evaluate(Listing(a => a.MinimumRentalPeriod = "Minimum 6 months"), unsure));

        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.AvailableFrom = Today.AddDays(20)), Profile()));
        // "Immediately" accepts a home that is free within the next week (listing 657: free tomorrow).
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.AvailableFrom = Today.AddDays(1)), Profile()));
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.AvailableFrom = Today.AddDays(7)), Profile()));
        Assert.Equal(RequirementStatus.Alternative, Evaluate(Listing(a => a.AvailableFrom = Today.AddDays(8)), Profile()));
        var withinMonth = Profile(p => p.MoveInTiming = "WithinOneMonth");
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => a.AvailableFrom = Today.AddDays(20)), withinMonth));
    }

    [Fact]
    public void ProfileJsonFromTheWebsiteIsRead()
    {
        var profile = SavedSearchProfile.Parse("""
            {"propertyGoal":"Rent","districts":["Vake"],"budgetMin":0,"budgetMax":"1200","currency":"USD",
             "bedrooms":2,"bedroomChoices":[2,3],"petTypes":["Cat"],"hasPet":true,"moveInTiming":"Flexible",
             "lifestyles":["Student"],"topPriorities":["MetroNearby"]}
            """);
        Assert.Equal(["Vake"], profile.Districts);
        Assert.Equal(1200, profile.BudgetMax);
        Assert.Equal([2, 3], profile.BedroomChoices);
        Assert.Equal(RequirementStatus.Exact, Evaluate(Listing(a => { a.District = "Vake"; a.Bedrooms = 3; }), profile));
        Assert.Equal(new SavedSearchProfile().PropertyGoal, SavedSearchProfile.Parse("not json").PropertyGoal);
    }

    [Fact]
    public void SameAnswersInAnotherOrderAreTheSameSearch()
    {
        var a = ClientController.CriteriaHash("""{"propertyGoal":"Rent","districts":["Vake","Saburtalo"],"proximityAddress":"","topPriorities":["A","B"]}""");
        var b = ClientController.CriteriaHash("""{"districts":["Saburtalo","Vake"],"topPriorities":["A","B"],"propertyGoal":"Rent"}""");
        var reordered = ClientController.CriteriaHash("""{"districts":["Saburtalo","Vake"],"topPriorities":["B","A"],"propertyGoal":"Rent"}""");
        Assert.Equal(a, b);
        // Priority order is part of the answer.
        Assert.NotEqual(a, reordered);
    }
}
