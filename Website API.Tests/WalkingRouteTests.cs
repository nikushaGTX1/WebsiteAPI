using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

/// <summary>
/// Walking routes are measured only when a listing is created or moves; edits that keep the
/// location never trigger Google, and saved rows are copied to the listing without estimates.
/// </summary>
public class WalkingRouteTests
{
    private static Apartment Listing(int id = 7) => new()
    {
        Id = id,
        PropertyLatitude = 41.7120,
        PropertyLongitude = 44.7490,
        Street = "Chavchavadze Ave",
        District = "Vake",
        City = "Tbilisi",
    };

    [Fact]
    public void NewListingIsMarkedPendingAndOldMinutesAreNotKept()
    {
        var apartment = Listing(id: 0);
        apartment.MetroDistanceMinutes = 3; // sent by an old browser: ignored
        Assert.True(ApartmentWalkingRouteService.MarkIfLocationChanged(apartment));
        Assert.Equal(WalkingRouteStatus.Pending, apartment.WalkingRoutesStatus);
        Assert.Null(apartment.MetroDistanceMinutes);
        Assert.Equal("41.712000,44.749000", apartment.WalkingRoutesOriginKey);
    }

    [Fact]
    public void EditWithoutMovingKeepsSavedWalks()
    {
        var apartment = Listing();
        apartment.WalkingRoutesStatus = WalkingRouteStatus.Completed;
        apartment.MetroDistanceMinutes = 6;
        var before = ApartmentWalkingRouteService.OriginKey(apartment);
        apartment.Price = 1500; // price, text, photos, rooms...
        Assert.False(ApartmentWalkingRouteService.MarkIfLocationChanged(apartment, before));
        Assert.Equal(WalkingRouteStatus.Completed, apartment.WalkingRoutesStatus);
        Assert.Equal(6, apartment.MetroDistanceMinutes);
    }

    [Fact]
    public void MovingThePinRecalculatesAndClearsTheOldPlace()
    {
        var apartment = Listing();
        apartment.WalkingRoutesStatus = WalkingRouteStatus.Completed;
        apartment.MetroDistanceMinutes = 6;
        apartment.DestinationWalkingMinutes = "{\"university:iliauni\":4}";
        var before = ApartmentWalkingRouteService.OriginKey(apartment);
        apartment.PropertyLatitude = 41.7300;
        Assert.True(ApartmentWalkingRouteService.MarkIfLocationChanged(apartment, before));
        Assert.Equal(WalkingRouteStatus.Pending, apartment.WalkingRoutesStatus);
        Assert.Null(apartment.MetroDistanceMinutes);
        Assert.Null(apartment.DestinationWalkingMinutes);
    }

    [Fact]
    public void LegacyListingRemembersItsLocationOnAPlainEdit()
    {
        var apartment = Listing(); // measured in the browser before routes were stored
        apartment.GymDistanceMinutes = 5;
        var before = ApartmentWalkingRouteService.OriginKey(apartment);
        Assert.False(ApartmentWalkingRouteService.MarkIfLocationChanged(apartment, before));
        Assert.Equal(WalkingRouteStatus.NotCalculated, apartment.WalkingRoutesStatus);
        Assert.Equal(before, apartment.WalkingRoutesOriginKey);
        Assert.Equal(5, apartment.GymDistanceMinutes);
    }

    [Fact]
    public void SavedRoutesFillTheListingWithoutEstimates()
    {
        var apartment = Listing();
        ApartmentWalkingRouteService.ApplyToListing(apartment,
        [
            new() { DestinationKey = "metroDistanceMinutes", Category = "metro", DestinationName = "Vake M/S", DurationSeconds = 420, DurationMinutes = 7, Status = WalkingRouteStatus.Completed },
            new() { DestinationKey = "parkDistanceMinutes", Category = "park", Status = WalkingRouteStatus.NoRoute },
            new() { DestinationKey = "gymDistanceMinutes", Category = "gym", Status = WalkingRouteStatus.NotFound },
            new() { DestinationKey = "university:iliauni", Category = "university", DestinationName = "Ilia State University", DurationSeconds = 240, DurationMinutes = 4, Status = WalkingRouteStatus.Completed },
            new() { DestinationKey = "university:gtu", Category = "university", DestinationName = "Georgian Technical University", DurationSeconds = 2400, DurationMinutes = 40, Status = WalkingRouteStatus.Completed },
            new() { DestinationKey = "city_center", Category = "city_center", DurationSeconds = 1800, DurationMinutes = 30, Status = WalkingRouteStatus.Completed },
        ]);
        Assert.Equal(7, apartment.MetroDistanceMinutes);
        Assert.Null(apartment.ParkDistanceMinutes); // Google found no walk: not invented
        Assert.Null(apartment.GymDistanceMinutes);
        Assert.Equal(4, apartment.UniversityDistanceMinutes); // nearest listed university
        Assert.Contains("\"universityDistanceMinutes\":\"Ilia State University\"", apartment.NearbyPlaceNames);
        Assert.Equal("{\"city_center\":30,\"university:gtu\":40,\"university:iliauni\":4}", apartment.DestinationWalkingMinutes);
    }

    [Fact]
    public void EveryUniversityOnTheWebsiteListHasACampusPoint()
    {
        Assert.Equal(30, UniversityDestinations.All.Length);
        Assert.Equal(UniversityDestinations.All.Length, UniversityDestinations.All.Select(u => u.Id).Distinct().Count());
        Assert.All(UniversityDestinations.All, u => Assert.InRange(u.Latitude, 41.6, 41.86));
    }
}
