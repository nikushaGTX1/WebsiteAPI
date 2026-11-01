using Website_API.DTO;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class QuietStreetMatchTests
{
    private static Apartment Listing(string description, string noise = "") => new()
    {
        Title = "Flat", Description = description, NoiseLevel = noise, Price = 1000, District = "Vake",
    };

    private static bool QuietReason(Apartment apartment)
    {
        var profile = new HomeMatchProfileRequest { MainPreferences = ["QuietStreet"] };
        var result = new HomeMatchScorer().Score(apartment, profile);
        return result.ScoreBreakdown.Any(item => item.Label == "Main preferences" && item.PointsEarned > 0);
    }

    // The upload form stores quiet street as a tag in the listing details, not in NoiseLevel.
    [Theory]
    [InlineData("Deal: For Rent | Quiet street: Yes | Cafe nearby: Yes", "", true)]
    [InlineData("Deal: For Rent | quiet street:yes", "", true)]
    [InlineData("Deal: For Rent | Cafe nearby: Yes", "Quiet", true)]
    [InlineData("Deal: For Rent | Cafe nearby: Yes", "", false)]
    [InlineData("Deal: For Rent | Quiet street: No", "", false)]
    public void QuietStreetCountsFromTheListingTag(string description, string noise, bool expected) =>
        Assert.Equal(expected, QuietReason(Listing(description, noise)));
}
