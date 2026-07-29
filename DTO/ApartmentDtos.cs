using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Website_API.DTO;

public class CreateApartmentDto
{
    // Basic information
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    public string? Address { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }

    // Upload up to 15 apartment images.
    public List<IFormFile> Images { get; set; } = [];

    // Compatibility with the current controller while gallery support
    // is introduced. The first uploaded image is used as the cover.
    public IFormFile? Image => Images.FirstOrDefault();

    // Location
    [Required]
    public string City { get; set; } = "Tbilisi";

    public string Region { get; set; } = string.Empty;

    [Required]
    public string District { get; set; } = string.Empty;

    [Required]
    public string Street { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Apartment details
    [Range(0, int.MaxValue)]
    public int Bedrooms { get; set; }

    [Range(0, int.MaxValue)]
    public int Bathrooms { get; set; }

    [Range(0.01, double.MaxValue)]
    public double SizeSquareMeters { get; set; }

    public int Floor { get; set; }

    [Range(1, int.MaxValue)]
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

    [Range(0.01, double.MaxValue)]
    public decimal? Price { get; set; }

    public string? Address { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }

    // New images that will be added to the existing gallery.
    public List<IFormFile> Images { get; set; } = [];

    // Compatibility with the current controller while gallery support
    // is introduced. The first uploaded image replaces the cover.
    public IFormFile? Image => Images.FirstOrDefault();

    // Existing ApartmentImage IDs that should be removed.
    public List<int> RemovedImageIds { get; set; } = [];

    // Existing image ID that should become the cover image.
    // A newly uploaded image cannot be selected here until it has an ID.
    public int? CoverImageId { get; set; }

    // Location
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? District { get; set; }
    public string? Street { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Apartment details
    [Range(0, int.MaxValue)]
    public int? Bedrooms { get; set; }

    [Range(0, int.MaxValue)]
    public int? Bathrooms { get; set; }

    [Range(0.01, double.MaxValue)]
    public double? SizeSquareMeters { get; set; }

    public int? Floor { get; set; }

    [Range(1, int.MaxValue)]
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
