namespace Website_API.Models;

using System.Text.Json.Serialization;

public class Apartment
{
    public int Id { get; set; }

    // Basic informacia
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Original account that submitted this property. Admins may publish on
    // the uploader's behalf after approval.
    public string? UploadedByUserId { get; set; }

    [JsonIgnore]
    public AppUser? UploadedByUser { get; set; }


    // Location
    public string City { get; set; } = "Tbilisi";

    public string Region { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    public string Street { get; set; } = string.Empty;

    // Canonical Velven street identity. Text fields remain temporarily for
    // backwards compatibility while existing listings are backfilled.
    public long? StreetId { get; set; }

    public string? BuildingNumber { get; set; }

    [JsonIgnore]
    public CanonicalStreet? CanonicalStreet { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    // Independently verified listing point. These values must come from the
    // owner/admin point-selection workflow, never from a street centroid.
    public double? PropertyLatitude { get; set; }

    public double? PropertyLongitude { get; set; }


    // apartamentis detalebi

    public List<ApartmentImage> Images { get; set; } = [];
    public int Bedrooms { get; set; }

    public int Bathrooms { get; set; }

    public double SizeSquareMeters { get; set; }

    public int Floor { get; set; }

    public int TotalFloors { get; set; }


    // Features
    public bool HasElevator { get; set; }

    public bool HasParking { get; set; }

    public bool HasBalcony { get; set; }

    public bool HasBathtub { get; set; }

    public bool HasAirConditioning { get; set; }

    public bool HasDishwasher { get; set; }

    public bool IsPetFriendly { get; set; }

    public bool HasHomeOfficeSpace { get; set; }

    public bool HasLargeKitchen { get; set; }

    public bool HasView { get; set; }

    public bool IsFurnished { get; set; }


    // Lifestyle information
    public string ApartmentStyle { get; set; } = string.Empty;

    public string NoiseLevel { get; set; } = string.Empty;

    public string Sunlight { get; set; } = string.Empty;


    // Nearby places in walking minutes
    public int? MetroDistanceMinutes { get; set; }

    public int? GymDistanceMinutes { get; set; }

    public int? ParkDistanceMinutes { get; set; }

    public int? SchoolDistanceMinutes { get; set; }

    public int? KindergartenDistanceMinutes { get; set; }

    public int? UniversityDistanceMinutes { get; set; }

    [JsonIgnore]
    public List<FavoriteApartment> FavoritedBy { get; set; } = [];

    [JsonIgnore]
    public List<ListingUpload> ListingUploads { get; set; } = [];
}
