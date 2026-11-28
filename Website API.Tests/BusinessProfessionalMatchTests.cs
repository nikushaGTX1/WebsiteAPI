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
    public void OfficeDistanceIsNotEstimatedFromAStraightLine()
    {
        // No saved walking route to a client's own office: Velven Match neither calls Google
        // nor guesses from the straight line, so both homes get the same (no) office points.
        var near = Lifestyle(Flat(41.7010, 44.7950), NearOffice());
        var far = Lifestyle(Flat(41.7800, 44.8000), NearOffice());
        Assert.Equal(0, near);
        Assert.Equal(near, far);
    }

    [Fact]
    public void StudentScoresOnlyTheSavedWalkToTheChosenUniversity()
    {
        var student = new HomeMatchProfileRequest { Lifestyles = ["Student"], StudentUniversityMode = "NearUniversity", StudentUniversityId = "iliauni" };
        var nearIlia = Flat(41.7, 44.8);
        nearIlia.DestinationWalkingMinutes = "{\"university:iliauni\":9,\"university:gtu\":40}";
        var nextToGtuOnly = Flat(41.7, 44.8);
        nextToGtuOnly.UniversityDistanceMinutes = 3;
        nextToGtuOnly.DestinationWalkingMinutes = "{\"university:iliauni\":38,\"university:gtu\":3}";
        var notMeasured = Flat(41.7, 44.8);
        notMeasured.UniversityDistanceMinutes = 3;
        Assert.Equal(5, Lifestyle(nearIlia, student));
        Assert.Equal(0, Lifestyle(nextToGtuOnly, student));
        Assert.Equal(0, Lifestyle(notMeasured, student));
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
