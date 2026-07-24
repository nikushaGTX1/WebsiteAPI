using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApartmentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SupabaseStorageService _storageService;
    private readonly GoogleNearbyPlacesService _nearbyPlacesService;

    public ApartmentsController(
        AppDbContext context,
        GoogleNearbyPlacesService nearbyPlacesService,
        SupabaseStorageService storageService)
    {
        _context = context;
        _nearbyPlacesService = nearbyPlacesService;
        _storageService = storageService;
    }

    [HttpGet]
    public async Task<IActionResult> GetApartments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var apartments = await _context.Apartments
            .AsNoTracking()
            .OrderByDescending(apartment => apartment.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        await Parallel.ForEachAsync(
            apartments,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 8,
                CancellationToken = cancellationToken
            },
            async (apartment, token) =>
            {
                apartment.ImageUrl =
                    await _storageService.CreateSignedUrlAsync(
                        apartment.ImageUrl,
                        3600,
                        token);
            });

        return Ok(apartments);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetApartment(
        int id,
        CancellationToken cancellationToken)
    {
        var apartment = await _context.Apartments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                apartment => apartment.Id == id,
                cancellationToken);

        if (apartment is null)
        {
            return NotFound(new
            {
                message = "Apartment not found"
            });
        }

        apartment.ImageUrl =
            await _storageService.CreateSignedUrlAsync(
                apartment.ImageUrl,
                3600,
                cancellationToken);

        return Ok(apartment);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateApartment(
        [FromForm] CreateApartmentDto dto,
        CancellationToken cancellationToken)
    {
        string? storedImagePath = null;

        try
        {
            storedImagePath =
                await _storageService.UploadImageAsync(
                    dto.Image,
                    cancellationToken);

            var apartment = new Apartment
            {
                // Basic information
                Title = dto.Title,
                Description = dto.Description,
                Price = dto.Price,
                Address = dto.Address,
                ImageUrl = storedImagePath,

                // Location
                City = dto.City,
                District = dto.District,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,

                // Apartment details
                Bedrooms = dto.Bedrooms,
                Bathrooms = dto.Bathrooms,
                SizeSquareMeters = dto.SizeSquareMeters,
                Floor = dto.Floor,
                TotalFloors = dto.TotalFloors,

                // Features
                HasElevator = dto.HasElevator,
                HasParking = dto.HasParking,
                HasBalcony = dto.HasBalcony,
                HasBathtub = dto.HasBathtub,
                HasAirConditioning = dto.HasAirConditioning,
                HasDishwasher = dto.HasDishwasher,
                IsPetFriendly = dto.IsPetFriendly,
                HasHomeOfficeSpace = dto.HasHomeOfficeSpace,
                HasLargeKitchen = dto.HasLargeKitchen,
                HasView = dto.HasView,
                IsFurnished = dto.IsFurnished,

                // Lifestyle
                ApartmentStyle = dto.ApartmentStyle,
                NoiseLevel = dto.NoiseLevel,
                Sunlight = dto.Sunlight,

                // Nearby-place walking times
                MetroDistanceMinutes = dto.MetroDistanceMinutes,
                GymDistanceMinutes = dto.GymDistanceMinutes,
                ParkDistanceMinutes = dto.ParkDistanceMinutes,
                SchoolDistanceMinutes = dto.SchoolDistanceMinutes,
                KindergartenDistanceMinutes =
                    dto.KindergartenDistanceMinutes,
                UniversityDistanceMinutes =
                    dto.UniversityDistanceMinutes
            };

            _context.Apartments.Add(apartment);
            await _context.SaveChangesAsync(cancellationToken);

            await _nearbyPlacesService.EnrichApartmentAsync(
                apartment,
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            var signedImageUrl =
                await _storageService.CreateSignedUrlAsync(
                    apartment.ImageUrl,
                    3600,
                    cancellationToken);

            return Ok(new
            {
                message = "Apartment created successfully",
                apartment = ToResponse(apartment, signedImageUrl)
            });
        }
        catch
        {
            // If Supabase upload worked but database creation failed,
            // remove the orphaned image.
            if (!string.IsNullOrWhiteSpace(storedImagePath))
            {
                await _storageService.DeleteImageAsync(
                    storedImagePath,
                    CancellationToken.None);
            }

            throw;
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateApartment(
        int id,
        [FromForm] UpdateApartmentDto dto,
        CancellationToken cancellationToken)
    {
        var apartment = await _context.Apartments
            .FirstOrDefaultAsync(
                apartment => apartment.Id == id,
                cancellationToken);

        if (apartment is null)
        {
            return NotFound(new
            {
                message = "Apartment not found"
            });
        }

        var locationChanged =
            dto.Address is not null ||
            dto.City is not null ||
            dto.District is not null ||
            dto.Latitude.HasValue ||
            dto.Longitude.HasValue;

        apartment.Title =
            dto.Title ?? apartment.Title;

        apartment.Description =
            dto.Description ?? apartment.Description;

        apartment.Price =
            dto.Price ?? apartment.Price;

        apartment.Address =
            dto.Address ?? apartment.Address;

        // Location
        apartment.City =
            dto.City ?? apartment.City;

        apartment.District =
            dto.District ?? apartment.District;

        apartment.Latitude =
            dto.Latitude ?? apartment.Latitude;

        apartment.Longitude =
            dto.Longitude ?? apartment.Longitude;

        // Apartment details
        apartment.Bedrooms =
            dto.Bedrooms ?? apartment.Bedrooms;

        apartment.Bathrooms =
            dto.Bathrooms ?? apartment.Bathrooms;

        apartment.SizeSquareMeters =
            dto.SizeSquareMeters ?? apartment.SizeSquareMeters;

        apartment.Floor =
            dto.Floor ?? apartment.Floor;

        apartment.TotalFloors =
            dto.TotalFloors ?? apartment.TotalFloors;

        // Features
        apartment.HasElevator =
            dto.HasElevator ?? apartment.HasElevator;

        apartment.HasParking =
            dto.HasParking ?? apartment.HasParking;

        apartment.HasBalcony =
            dto.HasBalcony ?? apartment.HasBalcony;

        apartment.HasBathtub =
            dto.HasBathtub ?? apartment.HasBathtub;

        apartment.HasAirConditioning =
            dto.HasAirConditioning ??
            apartment.HasAirConditioning;

        apartment.HasDishwasher =
            dto.HasDishwasher ??
            apartment.HasDishwasher;

        apartment.IsPetFriendly =
            dto.IsPetFriendly ??
            apartment.IsPetFriendly;

        apartment.HasHomeOfficeSpace =
            dto.HasHomeOfficeSpace ??
            apartment.HasHomeOfficeSpace;

        apartment.HasLargeKitchen =
            dto.HasLargeKitchen ??
            apartment.HasLargeKitchen;

        apartment.HasView =
            dto.HasView ?? apartment.HasView;

        apartment.IsFurnished =
            dto.IsFurnished ?? apartment.IsFurnished;

        // Lifestyle
        apartment.ApartmentStyle =
            dto.ApartmentStyle ?? apartment.ApartmentStyle;

        apartment.NoiseLevel =
            dto.NoiseLevel ?? apartment.NoiseLevel;

        apartment.Sunlight =
            dto.Sunlight ?? apartment.Sunlight;

        // Nearby-place walking times
        apartment.MetroDistanceMinutes =
            dto.MetroDistanceMinutes ??
            apartment.MetroDistanceMinutes;

        apartment.GymDistanceMinutes =
            dto.GymDistanceMinutes ??
            apartment.GymDistanceMinutes;

        apartment.ParkDistanceMinutes =
            dto.ParkDistanceMinutes ??
            apartment.ParkDistanceMinutes;

        apartment.SchoolDistanceMinutes =
            dto.SchoolDistanceMinutes ??
            apartment.SchoolDistanceMinutes;

        apartment.KindergartenDistanceMinutes =
            dto.KindergartenDistanceMinutes ??
            apartment.KindergartenDistanceMinutes;

        apartment.UniversityDistanceMinutes =
            dto.UniversityDistanceMinutes ??
            apartment.UniversityDistanceMinutes;

        string? oldImagePath = null;
        string? newImagePath = null;

        try
        {
            if (dto.Image is { Length: > 0 })
            {
                oldImagePath = apartment.ImageUrl;

                newImagePath =
                    await _storageService.UploadImageAsync(
                        dto.Image,
                        cancellationToken);

                apartment.ImageUrl = newImagePath;
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (locationChanged)
            {
                await _nearbyPlacesService.EnrichApartmentAsync(
                    apartment,
                    cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
            }

            // Delete the old image only after database update succeeds.
            if (!string.IsNullOrWhiteSpace(oldImagePath) &&
                !string.IsNullOrWhiteSpace(newImagePath))
            {
                await _storageService.DeleteImageAsync(
                    oldImagePath,
                    cancellationToken);
            }

            var signedImageUrl =
                await _storageService.CreateSignedUrlAsync(
                    apartment.ImageUrl,
                    3600,
                    cancellationToken);

            return Ok(new
            {
                message = "Apartment updated successfully",
                apartment = ToResponse(apartment, signedImageUrl)
            });
        }
        catch
        {
            // Delete the newly uploaded file if the database update failed.
            if (!string.IsNullOrWhiteSpace(newImagePath))
            {
                await _storageService.DeleteImageAsync(
                    newImagePath,
                    CancellationToken.None);
            }

            throw;
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/refresh-nearby-places")]
    public async Task<IActionResult> RefreshNearbyPlaces(
        int id,
        CancellationToken cancellationToken)
    {
        var apartment = await _context.Apartments
            .FirstOrDefaultAsync(
                apartment => apartment.Id == id,
                cancellationToken);

        if (apartment is null)
        {
            return NotFound(new
            {
                message = "Apartment not found"
            });
        }

        await _nearbyPlacesService.EnrichApartmentAsync(
            apartment,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var signedImageUrl =
            await _storageService.CreateSignedUrlAsync(
                apartment.ImageUrl,
                3600,
                cancellationToken);

        return Ok(new
        {
            message = "Nearby-place walking times refreshed successfully",
            apartment = ToResponse(apartment, signedImageUrl)
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteApartment(
        int id,
        CancellationToken cancellationToken)
    {
        var apartment = await _context.Apartments
            .FirstOrDefaultAsync(
                apartment => apartment.Id == id,
                cancellationToken);

        if (apartment is null)
        {
            return NotFound(new
            {
                message = "Apartment not found"
            });
        }

        var storedImagePath = apartment.ImageUrl;

        _context.Apartments.Remove(apartment);
        await _context.SaveChangesAsync(cancellationToken);

        // Delete the file only after the database record was deleted.
        await _storageService.DeleteImageAsync(
            storedImagePath,
            cancellationToken);

        return Ok(new
        {
            message = "Apartment deleted successfully"
        });
    }

    private static object ToResponse(
        Apartment apartment,
        string? signedImageUrl)
    {
        return new
        {
            apartment.Id,
            apartment.Title,
            apartment.Description,
            apartment.Price,
            apartment.Address,

            // Return a temporary signed URL, not the stored object path.
            ImageUrl = signedImageUrl,

            apartment.CreatedAt,

            apartment.City,
            apartment.District,
            apartment.Latitude,
            apartment.Longitude,

            apartment.Bedrooms,
            apartment.Bathrooms,
            apartment.SizeSquareMeters,
            apartment.Floor,
            apartment.TotalFloors,

            apartment.HasElevator,
            apartment.HasParking,
            apartment.HasBalcony,
            apartment.HasBathtub,
            apartment.HasAirConditioning,
            apartment.HasDishwasher,
            apartment.IsPetFriendly,
            apartment.HasHomeOfficeSpace,
            apartment.HasLargeKitchen,
            apartment.HasView,
            apartment.IsFurnished,

            apartment.ApartmentStyle,
            apartment.NoiseLevel,
            apartment.Sunlight,

            apartment.MetroDistanceMinutes,
            apartment.GymDistanceMinutes,
            apartment.ParkDistanceMinutes,
            apartment.SchoolDistanceMinutes,
            apartment.KindergartenDistanceMinutes,
            apartment.UniversityDistanceMinutes
        };
    }
}
