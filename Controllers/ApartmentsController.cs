using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
    private readonly IOutputCacheStore _outputCache;
    private readonly ILogger<ApartmentsController> _logger;

    public ApartmentsController(
        AppDbContext context,
        GoogleNearbyPlacesService nearbyPlacesService,
        SupabaseStorageService storageService,
        IOutputCacheStore outputCache,
        ILogger<ApartmentsController> logger)
    {
        _logger = logger;
        _context = context;
        _nearbyPlacesService = nearbyPlacesService;
        _storageService = storageService;
        _outputCache = outputCache;
    }

    [HttpGet]
    [OutputCache(PolicyName = "Apartments")]
    public async Task<IActionResult> GetApartments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? city = null,
        [FromQuery] string? region = null,
        [FromQuery] string? district = null,
        [FromQuery] string? street = null,
        [FromQuery(Name = "street_id")] long? streetId = null,
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

        var query = _context.Apartments
            .AsNoTracking()
            .Where(apartment => apartment.IsApproved)
            .Where(apartment =>
                !EF.Functions.ILike(apartment.Description, "%Source: https://www.myhome.ge/%") &&
                !EF.Functions.ILike(apartment.Description, "%Source: https://home.ss.ge/%"));

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
                GeorgianStreetTranslations.FindEnglish(street) ??
                GeorgianStreetTranslations.FindEnglishPartial(street) ??
                street.Trim();
            var pattern = $"%{value}%";
            query = query.Where(a =>
                EF.Functions.ILike(a.Street, pattern) ||
                (a.Address != null && EF.Functions.ILike(a.Address, pattern)));
        }
        if (streetId.HasValue)
            query = query.Where(apartment => apartment.StreetId == streetId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            var translatedDistrict =
                GeorgianLocationTranslations.FindEnglishDistrict(search);
            var translatedStreet =
                GeorgianStreetTranslations.FindEnglish(search) ??
                GeorgianStreetTranslations.FindEnglishPartial(search);
            var districtPattern = translatedDistrict is null
                ? null
                : $"%{translatedDistrict}%";
            var streetPattern = translatedStreet is null
                ? null
                : $"%{translatedStreet}%";

            query = query.Where(a =>
                EF.Functions.ILike(a.Title, pattern) ||
                EF.Functions.ILike(a.Description, pattern) ||
                EF.Functions.ILike(a.City, pattern) ||
                EF.Functions.ILike(a.Region, pattern) ||
                EF.Functions.ILike(a.District, pattern) ||
                EF.Functions.ILike(a.Street, pattern) ||
                (a.Address != null && EF.Functions.ILike(a.Address, pattern)) ||
                (districtPattern != null &&
                    EF.Functions.ILike(a.District, districtPattern)) ||
                (streetPattern != null &&
                    (EF.Functions.ILike(a.Street, streetPattern) ||
                     (a.Address != null &&
                        EF.Functions.ILike(a.Address, streetPattern)))));
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
    [OutputCache(PolicyName = "Apartments")]
    public async Task<IActionResult> GetApartment(
        int id,
        CancellationToken cancellationToken)
    {
        var apartment = await _context.Apartments
            .Include(apartment => apartment.Images)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                apartment => apartment.Id == id &&
                    !EF.Functions.ILike(apartment.Description, "%Source: https://www.myhome.ge/%") &&
                    !EF.Functions.ILike(apartment.Description, "%Source: https://home.ss.ge/%"),
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

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateApartment(
        [FromForm] CreateApartmentDto dto,
        CancellationToken cancellationToken)
    {
        if (IsScrapedSource(dto.Description))
        {
            return BadRequest(new
            {
                message = "Scraped source listings cannot be published as Velven apartments."
            });
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId)) return Unauthorized();
        var uploadedByUserId = User.IsInRole("Admin") && !string.IsNullOrWhiteSpace(dto.UploadedByUserId)
            ? dto.UploadedByUserId.Trim()
            : currentUserId;
        if (uploadedByUserId is not null &&
            !await _context.Users.AnyAsync(
                user => user.Id == uploadedByUserId,
                cancellationToken))
        {
            return BadRequest(new { message = "Uploader user is not valid." });
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
                Description = SanitizeVerifiedTag(dto.Description),
                Price = dto.Price,
                Address = null,
                PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber)
                    ? null
                    : dto.PhoneNumber.Trim(),
                ImageUrl = storedImagePaths.FirstOrDefault(),
                UploadedByUserId = uploadedByUserId,
                IsApproved = User.IsInRole("Admin") || User.IsInRole("Manager"),
                Images = storedImagePaths
                    .Select((path, index) => new ApartmentImage
                    {
                        StoragePath = path,
                        SortOrder = index,
                        IsCover = index == 0
                    })
                    .ToList(),

                // Location
                City = dto.City.Trim(),
                Region = dto.Region.Trim(),
                District = dto.District.Trim(),
                Street = string.Empty,
                StreetId = null,
                BuildingNumber = null,
                Latitude = null,
                Longitude = null,
                PropertyLatitude = null,
                PropertyLongitude = null,

                // Apartment details
                Rooms = dto.Rooms,
                OwnerName = dto.OwnerName?.Trim(),
                OwnerPhoneNumber = dto.OwnerPhoneNumber?.Trim(),
                AgentName = dto.AgentName?.Trim(),
                AgentPhoneNumber = dto.AgentPhoneNumber?.Trim(),
                ParkingCondition = dto.ParkingCondition,
                ParkingPoints = dto.ParkingPoints,
                ViewType = dto.ViewType,
                MinimumRentalPeriod = dto.MinimumRentalPeriod,
                AvailableFrom = ToUtcDate(dto.AvailableFrom),
                MaxOccupants = dto.MaxOccupants,
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
                    dto.UniversityDistanceMinutes,
                GroceryDistanceMinutes = dto.GroceryDistanceMinutes,
                PharmacyDistanceMinutes = dto.PharmacyDistanceMinutes,
                CafeDistanceMinutes = dto.CafeDistanceMinutes,
                EvChargerDistanceMinutes = dto.EvChargerDistanceMinutes
            };

            _context.Apartments.Add(apartment);
            await _context.SaveChangesAsync(cancellationToken);

            await InvalidateApartmentCacheAsync(cancellationToken);

            return Ok(new
            {
                message = "Apartment created successfully",
                apartment = await ToResponseAsync(
                    apartment,
                    includeGallery: true,
                    cancellationToken)
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            foreach (var storedImagePath in storedImagePaths)
            {
                await _storageService.DeleteImageAsync(
                    storedImagePath,
                    CancellationToken.None);
            }

            _logger.LogError(exception, "Apartment upload failed.");
            return StatusCode(500, new
            {
                message = "The apartment could not be saved. Please try again or contact support.",
                detail = exception.GetBaseException().Message
            });
        }
    }

    // Dates arrive as plain "yyyy-MM-dd" (Kind=Unspecified); PostgreSQL timestamptz only accepts UTC.
    private static DateTime? ToUtcDate(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc) : null;

    // The "Verified listing" badge is read from this tag, so only staff may set it;
    // anyone else's copy is stripped instead of trusted.
    private string SanitizeVerifiedTag(string description)
    {
        if (User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Agent"))
            return description;
        return System.Text.RegularExpressions.Regex.Replace(
            description,
            @"\|?\s*Verified listing:\s*\w+",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static bool IsScrapedSource(string? description) =>
        description?.Contains(
            "Source: https://www.myhome.ge/",
            StringComparison.OrdinalIgnoreCase) == true ||
        description?.Contains(
            "Source: https://home.ss.ge/",
            StringComparison.OrdinalIgnoreCase) == true;

    // Admins can edit any listing; other signed-in users only the listings they uploaded.
    [Authorize]
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

        if (!User.IsInRole("Admin") &&
            (string.IsNullOrEmpty(apartment.UploadedByUserId) ||
             apartment.UploadedByUserId != User.FindFirstValue(ClaimTypes.NameIdentifier)))
        {
            return Forbid();
        }

        CanonicalStreet? canonicalStreet = null;
        if (dto.StreetId.HasValue)
        {
            canonicalStreet = await _context.CanonicalStreets
                .AsNoTracking()
                .Include(street => street.City)
                .Include(street => street.District)
                .FirstOrDefaultAsync(street =>
                    street.Id == dto.StreetId.Value &&
                    (street.GeometryStatus == "approved" ||
                        street.Source == OfficialStreetCatalog.Source),
                    cancellationToken);
            if (canonicalStreet is null)
            {
                return BadRequest(new
                {
                    message = "The selected street_id is not in the canonical street catalog."
                });
            }
        }
        if (dto.PropertyLatitude.HasValue != dto.PropertyLongitude.HasValue ||
            dto.PropertyLatitude is < -90 or > 90 ||
            dto.PropertyLongitude is < -180 or > 180)
        {
            return BadRequest(new { message = "Property latitude and longitude must be supplied together and be valid." });
        }

        apartment.Title =
            dto.Title ?? apartment.Title;

        apartment.Description =
            (dto.Description is null ? null : SanitizeVerifiedTag(dto.Description)) ?? apartment.Description;

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
            canonicalStreet?.City.NameEn ?? dto.City ?? apartment.City;

        apartment.Region = dto.Region ?? apartment.Region;

        apartment.District =
            canonicalStreet?.District.NameEn ?? dto.District ?? apartment.District;

        apartment.Street =
            canonicalStreet?.NameEn ?? apartment.Street;

        if (canonicalStreet is not null) apartment.StreetId = canonicalStreet.Id;
        apartment.BuildingNumber = dto.BuildingNumber ?? apartment.BuildingNumber;

        apartment.Latitude =
            dto.Latitude ?? apartment.Latitude;

        apartment.Longitude =
            dto.Longitude ?? apartment.Longitude;

        apartment.PropertyLatitude =
            dto.PropertyLatitude ?? apartment.PropertyLatitude;

        apartment.PropertyLongitude =
            dto.PropertyLongitude ?? apartment.PropertyLongitude;

        // Apartment details
        apartment.Rooms =
            dto.Rooms ?? apartment.Rooms;

        apartment.OwnerName = dto.OwnerName?.Trim() ?? apartment.OwnerName;
        apartment.OwnerPhoneNumber = dto.OwnerPhoneNumber?.Trim() ?? apartment.OwnerPhoneNumber;
        apartment.AgentName = dto.AgentName?.Trim() ?? apartment.AgentName;
        apartment.AgentPhoneNumber = dto.AgentPhoneNumber?.Trim() ?? apartment.AgentPhoneNumber;
        apartment.ParkingCondition = dto.ParkingCondition ?? apartment.ParkingCondition;
        apartment.ParkingPoints = dto.ParkingPoints ?? apartment.ParkingPoints;
        apartment.ViewType = dto.ViewType ?? apartment.ViewType;
        apartment.MinimumRentalPeriod = dto.MinimumRentalPeriod ?? apartment.MinimumRentalPeriod;
        apartment.AvailableFrom = ToUtcDate(dto.AvailableFrom) ?? apartment.AvailableFrom;
        apartment.MaxOccupants = dto.MaxOccupants ?? apartment.MaxOccupants;

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
        apartment.GroceryDistanceMinutes =
            dto.GroceryDistanceMinutes ?? apartment.GroceryDistanceMinutes;
        apartment.PharmacyDistanceMinutes =
            dto.PharmacyDistanceMinutes ?? apartment.PharmacyDistanceMinutes;
        apartment.CafeDistanceMinutes =
            dto.CafeDistanceMinutes ?? apartment.CafeDistanceMinutes;
        apartment.EvChargerDistanceMinutes =
            dto.EvChargerDistanceMinutes ?? apartment.EvChargerDistanceMinutes;

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

            await _context.SaveChangesAsync(cancellationToken);
            await InvalidateApartmentCacheAsync(cancellationToken);

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
        await InvalidateApartmentCacheAsync(cancellationToken);

        return Ok(new
        {
            message = "Nearby-place walking times refreshed successfully",
            apartment = await ToResponseAsync(
                apartment,
                includeGallery: true,
                cancellationToken)
        });
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingApartments(
        CancellationToken cancellationToken)
    {
        var apartments = await _context.Apartments
            .Include(apartment => apartment.Images)
            .Include(apartment => apartment.UploadedByUser)
            .AsNoTracking()
            .Where(apartment => !apartment.IsApproved)
            .OrderByDescending(apartment => apartment.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = new List<object>();
        foreach (var apartment in apartments)
        {
            result.Add(new
            {
                id = apartment.Id,
                apartment = await ToResponseAsync(apartment, includeGallery: true, cancellationToken),
                status = "pending",
                submittedAt = apartment.CreatedAt,
                submittedByUserId = apartment.UploadedByUserId,
                submittedByName = apartment.UploadedByUser?.FullName
                    ?? apartment.UploadedByUser?.UserName
                    ?? "Unknown user",
                submittedByEmail = apartment.UploadedByUser?.Email ?? string.Empty,
                submittedByPhone = apartment.UploadedByUser?.PhoneNumber,
                submittedByPicture = apartment.UploadedByUser?.ProfilePicture,
                submittedByIsAgent = apartment.UploadedByUser?.IsAgent ?? false,
            });
        }

        return Ok(result);
    }

    // Fills in the walking time to the nearest EV charger for listings that
    // were uploaded before chargers were tracked.
    // Re-measures the walking time to the nearest gym now that fitness centers count
    // as gyms. Only shorter times are saved. Pass skip to continue with the next page.
    [Authorize(Roles = "Admin,Manager")]
    [HttpPost("refresh-gym-distances")]
    public async Task<IActionResult> RefreshGymDistances(
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 60;
        var query = _context.Apartments
            .Where(apartment => apartment.PropertyLatitude != null || apartment.Latitude != null);
        var total = await query.CountAsync(cancellationToken);
        var apartments = await query
            .OrderBy(apartment => apartment.Id)
            .Skip(Math.Max(0, skip))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var updated = 0;
        foreach (var apartment in apartments)
        {
            var latitude = apartment.PropertyLatitude ?? apartment.Latitude;
            var longitude = apartment.PropertyLongitude ?? apartment.Longitude;
            if (latitude is null || longitude is null) continue;

            var minutes = await _nearbyPlacesService.FindGymWalkingMinutesAsync(
                latitude.Value,
                longitude.Value,
                cancellationToken);
            if (minutes is null) continue;
            if (apartment.GymDistanceMinutes is not null && minutes >= apartment.GymDistanceMinutes) continue;

            apartment.GymDistanceMinutes = minutes;
            updated++;
        }

        if (updated > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await InvalidateApartmentCacheAsync(cancellationToken);
        }

        var nextSkip = skip + apartments.Count;
        return Ok(new
        {
            message = $"Gym walking times improved for {updated} of {apartments.Count} listing(s).",
            updated,
            checkedCount = apartments.Count,
            nextSkip = nextSkip < total ? nextSkip : (int?)null,
        });
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost("refresh-ev-chargers")]
    public async Task<IActionResult> RefreshEvChargerDistances(
        CancellationToken cancellationToken)
    {
        var apartments = await _context.Apartments
            .Where(apartment =>
                apartment.EvChargerDistanceMinutes == null &&
                (apartment.PropertyLatitude != null || apartment.Latitude != null))
            .OrderByDescending(apartment => apartment.CreatedAt)
            .Take(60)
            .ToListAsync(cancellationToken);

        var updated = 0;
        foreach (var apartment in apartments)
        {
            var latitude = apartment.PropertyLatitude ?? apartment.Latitude;
            var longitude = apartment.PropertyLongitude ?? apartment.Longitude;
            if (latitude is null || longitude is null) continue;

            var minutes = await _nearbyPlacesService.FindEvChargerWalkingMinutesAsync(
                (double)latitude.Value,
                (double)longitude.Value,
                cancellationToken);
            if (minutes is null) continue;

            apartment.EvChargerDistanceMinutes = minutes;
            updated++;
        }

        if (updated > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await InvalidateApartmentCacheAsync(cancellationToken);
        }

        return Ok(new
        {
            message = $"EV charger walking times updated for {updated} of {apartments.Count} listing(s).",
            updated,
            checkedCount = apartments.Count,
        });
    }

    // Staff-only uploader details for published listings. Kept out of the
    // public (output-cached) list response so uploader contacts never leak.
    [Authorize(Roles = "Admin,Manager")]
    [HttpGet("uploaders")]
    public async Task<IActionResult> GetApartmentUploaders(
        CancellationToken cancellationToken)
    {
        var rows = await _context.Apartments
            .AsNoTracking()
            .Where(apartment => apartment.IsApproved)
            .Select(apartment => new
            {
                apartmentId = apartment.Id,
                userId = apartment.UploadedByUserId,
                name = apartment.UploadedByUser != null
                    ? apartment.UploadedByUser.FullName ?? apartment.UploadedByUser.UserName
                    : null,
                email = apartment.UploadedByUser != null ? apartment.UploadedByUser.Email : null,
                phone = apartment.UploadedByUser != null ? apartment.UploadedByUser.PhoneNumber : null,
                picture = apartment.UploadedByUser != null ? apartment.UploadedByUser.ProfilePicture : null,
                isAgent = apartment.UploadedByUser != null && apartment.UploadedByUser.IsAgent,
                imageCount = apartment.Images.Count,
            })
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> ApproveApartment(
        int id,
        CancellationToken cancellationToken)
    {
        var apartment = await _context.Apartments
            .Include(apartment => apartment.Images)
            .FirstOrDefaultAsync(apartment => apartment.Id == id, cancellationToken);
        if (apartment is null) return NotFound(new { message = "Apartment not found" });

        apartment.IsApproved = true;
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateApartmentCacheAsync(cancellationToken);

        return Ok(new
        {
            message = "Apartment confirmed and published.",
            apartment = await ToResponseAsync(apartment, includeGallery: true, cancellationToken)
        });
    }

    [Authorize(Roles = "Admin,Manager")]
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

        // Managers may only decline listings still waiting for approval.
        if (!User.IsInRole("Admin") && apartment.IsApproved)
        {
            return Forbid();
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
        await InvalidateApartmentCacheAsync(cancellationToken);

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

    private ValueTask InvalidateApartmentCacheAsync(
        CancellationToken cancellationToken)
    {
        return _outputCache.EvictByTagAsync(
            "apartments",
            cancellationToken);
    }

    private async Task<object> ToResponseAsync(
        Apartment apartment,
        bool includeGallery,
        CancellationToken cancellationToken)
    {
        var canSeeOwnerContact =
            User.Identity?.IsAuthenticated == true &&
            (User.IsInRole("Admin") ||
             User.IsInRole("Agent") ||
             User.IsInRole("Manager") ||
             (!string.IsNullOrEmpty(apartment.UploadedByUserId) &&
              apartment.UploadedByUserId == User.FindFirstValue(ClaimTypes.NameIdentifier)));

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

            // Owner contact is private: only the owner, agents, managers and admins receive it.
            OwnerName = canSeeOwnerContact ? apartment.OwnerName : null,
            OwnerPhoneNumber = canSeeOwnerContact ? apartment.OwnerPhoneNumber : null,
            apartment.AgentName,
            apartment.AgentPhoneNumber,
            apartment.ParkingCondition,
            apartment.ParkingPoints,
            apartment.ViewType,
            apartment.MinimumRentalPeriod,
            apartment.AvailableFrom,
            apartment.MaxOccupants,

            // Return a temporary signed URL, not the stored object path.
            ImageUrl = signedImageUrl,
            Images = images,
            apartment.IsApproved,

            apartment.CreatedAt,

            apartment.City,
            apartment.Region,
            apartment.District,
            apartment.Street,
            apartment.StreetId,
            apartment.BuildingNumber,
            apartment.Latitude,
            apartment.Longitude,
            apartment.PropertyLatitude,
            apartment.PropertyLongitude,

            apartment.Rooms,
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
            apartment.UniversityDistanceMinutes,
            apartment.GroceryDistanceMinutes,
            apartment.PharmacyDistanceMinutes,
            apartment.CafeDistanceMinutes,
            apartment.EvChargerDistanceMinutes
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
