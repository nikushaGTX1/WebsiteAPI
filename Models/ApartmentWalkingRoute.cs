namespace Website_API.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Walking route from a listing to one destination (nearest metro, supermarket, a listed
/// university, the city centre...). Calculated with Google Routes only when the listing is
/// created or its location changes; Velven Match only reads these rows.
/// One row per (ApartmentId, DestinationKey); rows are deleted with the listing.
/// </summary>
public class ApartmentWalkingRoute
{
    public int Id { get; set; }

    public int ApartmentId { get; set; }

    /// <summary>Stable key: a minutes field ("metroDistanceMinutes"), "university:iliauni" or "city_center".</summary>
    [MaxLength(80)]
    public string DestinationKey { get; set; } = string.Empty;

    /// <summary>"metro", "supermarket", "university", "city_center"...</summary>
    [MaxLength(40)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DestinationName { get; set; }

    public double? DestinationLatitude { get; set; }

    public double? DestinationLongitude { get; set; }

    public int? DurationSeconds { get; set; }

    public int? DurationMinutes { get; set; }

    public int? DistanceMeters { get; set; }

    /// <summary>Driving minutes, measured only when the walk is long (out-of-town homes).</summary>
    public int? DrivingMinutes { get; set; }

    /// <summary>Completed, NotFound (no such place nearby), NoRoute (Google found no walk) or Failed.</summary>
    [MaxLength(20)]
    public string Status { get; set; } = WalkingRouteStatus.Completed;

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public static class WalkingRouteStatus
{
    // Per listing (Apartment.WalkingRoutesStatus)
    public const string NotCalculated = "NotCalculated";
    public const string Pending = "Pending";
    public const string Partial = "Partial";
    public const string NoLocation = "NoLocation";

    // Per listing and per route
    public const string Completed = "Completed";
    public const string Failed = "Failed";

    // Per route
    public const string NotFound = "NotFound";
    public const string NoRoute = "NoRoute";
}
