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
        [FromQuery] string? city = null,
        [FromQuery] string? region = null,
        [FromQuery] string? district = null,
        [FromQuery] string? street = null,
        [FromQuery] string? search = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] int? minBedrooms = null,
        [FromQuery] int? minBathrooms = null,
        [FromQuery] double? minSize = null,
        [FromQuery] double? maxSize = null,
        [FromQuery] bool? hasElevator = null,
        [FromQuery] bool? hasParking = null,
        [FromQuery] bool? hasBalcony = null,
        [FromQuery] bool? hasAirConditioning = null,
        [FromQuery] bool? isPetFriendly = null,
        [FromQuery] bool? isFurnished = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (minPrice.HasValue && maxPrice.HasValue &&
            minPrice.Value > maxPrice.Value)
        {
            return BadRequest(new
            {
                message = "minPrice cannot be greater than maxPrice."
            });
        }

        if (minSize.HasValue && maxSize.HasValue &&
            minSize.Value > maxSize.Value)
        {
            return BadRequest(new
            {
                message = "minSize cannot be greater than maxSize."
            });
        }

        var query = _context.Apartments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(city))
        {
            var value =
                GeorgianLocationTranslations.FindEnglishCity(city) ?? city.Trim();
            query = query.Where(a => EF.Functions.ILike(a.City, value));
        }
        if (!string.IsNullOrWhiteSpace(region))
        {
            var value =
                GeorgianLocationTranslations.FindEnglishRegion(region) ??
                region.Trim();
            query = query.Where(a => EF.Functions.ILike(a.Region, value));
        }
        if (!string.IsNullOrWhiteSpace(district))
        {
            var value =
                GeorgianLocationTranslations.FindEnglishDistrict(district) ??
                district.Trim();
            query = query.Where(a => EF.Functions.ILike(a.District, value));
        }
        if (!string.IsNullOrWhiteSpace(street))
        {
            var value =
                GeorgianStreetTranslations.FindEnglish(street) ?? street.Trim();
            var pattern = $"%{value}%";
            query = query.Where(a =>
                EF.Functions.ILike(a.Street, pattern) ||
                (a.Address != null && EF.Functions.ILike(a.Address, pattern)));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(a =>
                EF.Functions.ILike(a.Title, pattern) ||
                EF.Functions.ILike(a.Description, pattern) ||
                EF.Functions.ILike(a.City, pattern) ||
                EF.Functions.ILike(a.Region, pattern) ||
                EF.Functions.ILike(a.District, pattern) ||
                EF.Functions.ILike(a.Street, pattern) ||
                (a.Address != null && EF.Functions.ILike(a.Address, pattern)));
        }
        if (minPrice.HasValue)
            query = query.Where(a => a.Price >= minPrice.Value);
        if (maxPrice.HasValue)
            query = query.Where(a => a.Price <= maxPrice.Value);
        if (minBedrooms.HasValue)
            query = query.Where(a => a.Bedrooms >= minBedrooms.Value);
        if (minBathrooms.HasValue)
            query = query.Where(a => a.Bathrooms >= minBathrooms.Value);
        if (minSize.HasValue)
            query = query.Where(a => a.SizeSquareMeters >= minSize.Value);
        if (maxSize.HasValue)
            query = query.Where(a => a.SizeSquareMeters <= maxSize.Value);
        if (hasElevator.HasValue)
            query = query.Where(a => a.HasElevator == hasElevator.Value);
        if (hasParking.HasValue)
            query = query.Where(a => a.HasParking == hasParking.Value);
        if (hasBalcony.HasValue)
            query = query.Where(a => a.HasBalcony == hasBalcony.Value);
        if (hasAirConditioning.HasValue)
            query = query.Where(a => a.HasAirConditioning == hasAirConditioning.Value);
        if (isPetFriendly.HasValue)
            query = query.Where(a => a.IsPetFriendly == isPetFriendly.Value);
        if (isFurnished.HasValue)
            query = query.Where(a => a.IsFurnished == isFurnished.Value);

        var apartments = await query
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
            .Include(apartment => apartment.Images)
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

        return Ok(await ToResponseAsync(
            apartment,
            includeGallery: true,
            cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateApartment(
        [FromForm] CreateApartmentDto dto,
        CancellationToken cancellationToken)
    {
        if (!ApartmentLocationResolver.TryResolve(
                dto.District,
                dto.Street,
                out var resolvedLocation))
        {
            return BadRequest(new
            {
                message =
                    "Select a valid district from the Locations API, or provide " +
                    "a street that belongs to exactly one district. The city " +
                    "name cannot be used as the district."
            });
        }

        List<string> storedImagePaths = [];

        try
        {
            storedImagePaths = await UploadImagesAsync(
                dto.Images,
                cancellationToken);

            var apartment = new Apartment
            {
                // Basic information
                Title = dto.Title,
                Description = dto.Description,
                Price = dto.Price,
                Address = dto.Address,
                PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber)
                    ? null
                    : dto.PhoneNumber.Trim(),
                ImageUrl = storedImagePaths.FirstOrDefault(),
                Images = storedImagePaths
                    .Select((path, index) => new ApartmentImage
                    {
                        StoragePath = path,
                        SortOrder = index,
                        IsCover = index == 0
                    })
                    .ToList(),

                // Location
                City = resolvedLocation.City,
                Region = resolvedLocation.Region,
                District = resolvedLocation.District,
                Street = resolvedLocation.Street,
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

            return Ok(new
            {
                message = "Apartment created successfully",
                apartment = await ToResponseAsync(
                    apartment,
                    includeGallery: true,
                    cancellationToken)
            });
        }
        catch
        {
            foreach (var storedImagePath in storedImagePaths)
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
            .Include(apartment => apartment.Images)
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

        ResolvedApartmentLocation? resolvedLocation = null;
        if (dto.District is not null || dto.Street is not null)
        {
            if (!ApartmentLocationResolver.TryResolve(
                    dto.District ?? apartment.District,
                    dto.Street ?? apartment.Street,
                    out resolvedLocation))
            {
                return BadRequest(new
                {
                    message =
                        "Select a valid district from the Locations API, or " +
                        "provide a street that belongs to exactly one district. " +
                        "The city name cannot be used as the district."
                });
            }
        }

        var locationChanged =
            dto.Address is not null ||
            dto.City is not null ||
            dto.Region is not null ||
            dto.District is not null ||
            dto.Street is not null ||
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

        if (dto.PhoneNumber is not null)
        {
            apartment.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber)
                ? null
                : dto.PhoneNumber.Trim();
        }

        // Location
        apartment.City =
            resolvedLocation?.City ?? dto.City ?? apartment.City;

        apartment.Region =
            resolvedLocation?.Region ?? dto.Region ?? apartment.Region;

        apartment.District =
            resolvedLocation?.District ?? apartment.District;

        apartment.Street =
            resolvedLocation?.Street ?? apartment.Street;

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

        List<string> newImagePaths = [];
        List<string> removedImagePaths = [];
        ApartmentImage? requestedCover = null;

        if (dto.CoverImageId.HasValue)
        {
            requestedCover = apartment.Images.FirstOrDefault(
                image =>
                    image.Id == dto.CoverImageId.Value &&
                    !dto.RemovedImageIds.Contains(image.Id));

            if (requestedCover is null)
            {
                return BadRequest(new
                {
                    message = "The selected cover image does not belong to this apartment or is being removed."
                });
            }
        }

        try
        {
            var imagesToRemove = apartment.Images
                .Where(image => dto.RemovedImageIds.Contains(image.Id))
                .ToList();

            if (imagesToRemove.Count > 0)
            {
                removedImagePaths.AddRange(
                    imagesToRemove.Select(image => image.StoragePath));
                _context.ApartmentImages.RemoveRange(imagesToRemove);
                foreach (var image in imagesToRemove)
                {
                    apartment.Images.Remove(image);
                }
            }

            newImagePaths = await UploadImagesAsync(
                dto.Images,
                cancellationToken);

            var nextSortOrder = apartment.Images.Count == 0
                ? 0
                : apartment.Images.Max(image => image.SortOrder) + 1;

            foreach (var path in newImagePaths)
            {
                apartment.Images.Add(new ApartmentImage
                {
                    StoragePath = path,
                    SortOrder = nextSortOrder++,
                    IsCover = false
                });
            }

            if (requestedCover is not null)
            {
                foreach (var image in apartment.Images)
                {
                    image.IsCover = image == requestedCover;
                }
            }

            if (apartment.Images.Count > 0 &&
                apartment.Images.All(image => !image.IsCover))
            {
                apartment.Images
                    .OrderBy(image => image.SortOrder)
                    .First()
                    .IsCover = true;
            }

            var coverImage = apartment.Images
                .FirstOrDefault(image => image.IsCover);
            apartment.ImageUrl = coverImage?.StoragePath;

            if (locationChanged)
            {
                await _nearbyPlacesService.EnrichApartmentAsync(
                    apartment,
                    cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            foreach (var removedImagePath in removedImagePaths)
            {
                await _storageService.DeleteImageAsync(
                    removedImagePath,
                    cancellationToken);
            }

            return Ok(new
            {
                message = "Apartment updated successfully",
                apartment = await ToResponseAsync(
                    apartment,
                    includeGallery: true,
                    cancellationToken)
            });
        }
        catch
        {
            foreach (var newImagePath in newImagePaths)
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
            .Include(apartment => apartment.Images)
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

        return Ok(new
        {
            message = "Nearby-place walking times refreshed successfully",
            apartment = await ToResponseAsync(
                apartment,
                includeGallery: true,
                cancellationToken)
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteApartment(
        int id,
        CancellationToken cancellationToken)
    {
        var apartment = await _context.Apartments
            .Include(apartment => apartment.Images)
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

        var storedImagePaths = apartment.Images
            .Select(image => image.StoragePath)
            .Append(apartment.ImageUrl)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _context.Apartments.Remove(apartment);
        await _context.SaveChangesAsync(cancellationToken);

        // Delete the file only after the database record was deleted.
        await Parallel.ForEachAsync(
            storedImagePaths,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            },
            async (path, token) =>
                await _storageService.DeleteImageAsync(path, token));

        return Ok(new
        {
            message = "Apartment deleted successfully"
        });
    }

    private async Task<object> ToResponseAsync(
        Apartment apartment,
        bool includeGallery,
        CancellationToken cancellationToken)
    {
        var orderedImages = apartment.Images
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.Id)
            .ToList();

        var coverPath = orderedImages
            .FirstOrDefault(image => image.IsCover)?
            .StoragePath ?? apartment.ImageUrl;

        var signedImageUrl =
            await _storageService.CreateSignedUrlAsync(
                coverPath,
                3600,
                cancellationToken);

        object[] images = [];

        if (includeGallery && orderedImages.Count > 0)
        {
            images = await Task.WhenAll(
                orderedImages.Select(async image => (object)new
                {
                    image.Id,
                    image.SortOrder,
                    image.IsCover,
                    Url = await _storageService.CreateSignedUrlAsync(
                        image.StoragePath,
                        3600,
                        cancellationToken)
                }));
        }

        return new
        {
            apartment.Id,
            apartment.Title,
            apartment.Description,
            apartment.Price,
            apartment.Address,
            apartment.PhoneNumber,

            // Return a temporary signed URL, not the stored object path.
            ImageUrl = signedImageUrl,
            Images = images,

            apartment.CreatedAt,

            apartment.City,
            apartment.Region,
            apartment.District,
            apartment.Street,
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

    private async Task<List<string>> UploadImagesAsync(
        IReadOnlyList<IFormFile> images,
        CancellationToken cancellationToken)
    {
        var uploads = images
            .Where(image => image.Length > 0)
            .ToList();

        if (uploads.Count > 15)
        {
            throw new InvalidOperationException(
                "A maximum of 15 apartment images can be uploaded.");
        }

        var paths = new string?[uploads.Count];

        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, uploads.Count),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = cancellationToken
                },
                async (index, token) =>
                {
                    paths[index] =
                        await _storageService.UploadImageAsync(
                            uploads[index],
                            cancellationToken: token);
                });
        }
        catch
        {
            foreach (var path in paths.Where(
                         path => !string.IsNullOrWhiteSpace(path)))
            {
                await _storageService.DeleteImageAsync(
                    path,
                    CancellationToken.None);
            }

            throw;
        }

        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToList();
    }
}
