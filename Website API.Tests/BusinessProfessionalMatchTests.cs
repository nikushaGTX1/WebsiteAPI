using Website_API.DTO;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class BusinessProfessionalMatchTests
{
    // Office at Rustaveli Avenue; the near flat is ~0.5 km away, the far one ~9 km.
    private const double OfficeLat = 41.6975, OfficeLng = 44.7990;

    private static Apartment Flat(double lat, double lng, int rooms = 3, int bedrooms = 2, bool office = false) => new()
    {
        Title = "Flat", Description = "", Price = 1000, District = "Vake",
        Latitude = lat, Longitude = lng, Rooms = rooms, Bedrooms = bedrooms, HasHomeOfficeSpace = office,
    };

    private static int Lifestyle(Apartment apartment, HomeMatchProfileRequest profile) =>
        new HomeMatchScorer().Score(apartment, profile).ScoreBreakdown.First(item => item.Label == "Lifestyle").PointsEarned;

    private static HomeMatchProfileRequest NearOffice() => new()
    {
        Lifestyles = ["BusinessProfessional"], BusinessMode = "NearOffice", OfficeLatitude = OfficeLat, OfficeLongitude = OfficeLng,
    };

    [Fact]
    public void CloserToTheOfficeScoresHigher()
    {
        var near = Lifestyle(Flat(41.7010, 44.7950), NearOffice());
        var far = Lifestyle(Flat(41.7800, 44.8000), NearOffice());
        Assert.Equal(10, near);
        Assert.True(near > far);
    }

    [Theory]
    [InlineData(1.0, 10)]
    [InlineData(2.5, 8)]
    [InlineData(4.5, 5)]
    [InlineData(7.0, 2)]
    [InlineData(12.0, 0)]
    public void OfficeDistanceBands(double km, int points) => Assert.Equal(points, HomeMatchScorer.OfficeDistancePoints(km));

    [Fact]
    public void HomeBusinessRewardsASpareRoomLikeWorkFromHome()
    {
        var profile = new HomeMatchProfileRequest { Lifestyles = ["BusinessProfessional"], BusinessMode = "HomeBusiness" };
        Assert.Equal(6, Lifestyle(Flat(41.7, 44.8, rooms: 4, bedrooms: 2), profile));
        Assert.Equal(6, Lifestyle(Flat(41.7, 44.8, rooms: 2, bedrooms: 1, office: true), profile));
        Assert.Equal(0, Lifestyle(Flat(41.7, 44.8, rooms: 2, bedrooms: 1), profile));
    }

    [Fact]
    public void WithoutAFollowUpAnswerMetroOrParkingStillCounts()
    {
        var profile = new HomeMatchProfileRequest { Lifestyles = ["BusinessProfessional"] };
        var flat = Flat(41.7, 44.8);
        flat.HasParking = true;
        Assert.Equal(5, Lifestyle(flat, profile));
    }
}
