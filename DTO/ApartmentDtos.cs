using Microsoft.AspNetCore.Http;

namespace Website_API.DTO;

public class CreateApartmentDto
{
    // Basic information
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Address { get; set; }
    public IFormFile? Image { get; set; }

    // Location
    public string City { get; set; } = "Tbilisi";
    public string District { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Apartment details
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
}

public class UpdateApartmentDto
{
    // Basic information
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? Address { get; set; }
    public IFormFile? Image { get; set; }

    // Location
    public string? City { get; set; }
    public string? District { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Apartment details
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public double? SizeSquareMeters { get; set; }
    public int? Floor { get; set; }
    public int? TotalFloors { get; set; }

    // Features
    public bool? HasElevator { get; set; }
    public bool? HasParking { get; set; }
    public bool? HasBalcony { get; set; }
    public bool? HasBathtub { get; set; }
    public bool? HasAirConditioning { get; set; }
    public bool? HasDishwasher { get; set; }
    public bool? IsPetFriendly { get; set; }
    public bool? HasHomeOfficeSpace { get; set; }
    public bool? HasLargeKitchen { get; set; }
    public bool? HasView { get; set; }
    public bool? IsFurnished { get; set; }

    // Lifestyle information
    public string? ApartmentStyle { get; set; }
    public string? NoiseLevel { get; set; }
    public string? Sunlight { get; set; }

    // Nearby places
    public int? MetroDistanceMinutes { get; set; }
    public int? GymDistanceMinutes { get; set; }
    public int? ParkDistanceMinutes { get; set; }
    public int? SchoolDistanceMinutes { get; set; }
    public int? KindergartenDistanceMinutes { get; set; }
    public int? UniversityDistanceMinutes { get; set; }
}