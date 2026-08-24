using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Services;

namespace Website_API.Controllers;

public sealed record StreetGeometryReviewRequest(
    JsonElement Geometry,
    string Source,
    string ExternalSourceId,
    string? NameKa,
    string? NameEn,
    string[]? Aliases,
    string? Notes);

public sealed record StreetReviewDecision(string? Notes, bool AllowOutsideDistrict = false);

[ApiController]
[Route("api/admin/streets")]
[Authorize(Roles = "Admin")]
public sealed partial class AdminStreetsController(
    AppDbContext context,
    CanonicalStreetImportService importer) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] long? districtId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = context.CanonicalStreets.AsNoTracking().Include(street => street.District).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(street => street.GeometryStatus == status);
        if (districtId.HasValue) query = query.Where(street => street.DistrictId == districtId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(street =>
                EF.Functions.ILike(street.NameEn, $"%{term}%") ||
                EF.Functions.ILike(street.NameKa, $"%{term}%") ||
                street.Aliases.Any(alias => EF.Functions.ILike(alias, $"%{term}%")));
        }
        var result = await query.OrderBy(street => street.District.NameEn)
            .ThenBy(street => street.NameEn)
            .Select(street => new
            {
                street.Id, street.NameKa, street.NameEn, street.Aliases,
                street.CityId, street.DistrictId, District = street.District.NameEn,
                street.CentroidLatitude, street.CentroidLongitude,
                street.Source, street.ExternalSourceId, street.GeometryStatus,
                street.ApprovedAt, street.ReviewNotes,
                HasGeometry = street.GeometryGeoJson != null
            }).Take(2000).ToListAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
    {
        var street = await context.CanonicalStreets.AsNoTracking()
            .Include(item => item.District)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return street is null ? NotFound() : Ok(StreetsController.ToResponse(street));
    }

    [HttpPost("import/{district}")]
    public async Task<IActionResult> Import(string district, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await importer.ImportDistrictAsync(district, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Problem(statusCode: 503, title: "Street source unavailable", detail: exception.Message);
        }
        catch (DbUpdateException exception)
        {
            return Problem(
                statusCode: 409,
                title: "Street import could not be stored",
                detail: exception.GetBaseException().Message);
        }
    }

    [HttpPut("{id:long}/geometry")]
    public async Task<IActionResult> ReplaceGeometry(
        long id,
        StreetGeometryReviewRequest request,
        CancellationToken cancellationToken)
    {
        var street = await context.CanonicalStreets.Include(item => item.District)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (street is null) return NotFound();
        var summary = StreetGeoJson.SummarizeGeoJson(request.Geometry.GetRawText());
        if (summary is null)
            return BadRequest(new { message = "Geometry must be a valid LineString or MultiLineString with real coordinates." });
        if (string.IsNullOrWhiteSpace(request.Source) || string.IsNullOrWhiteSpace(request.ExternalSourceId))
            return BadRequest(new { message = "A verifiable source and external source ID are required." });
        street.NameKa = request.NameKa?.Trim() ?? street.NameKa;
        street.NameEn = request.NameEn?.Trim() ?? street.NameEn;
        street.Aliases = request.Aliases?.Select(value => value.Trim()).Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? street.Aliases;
        street.GeometryGeoJson = summary.GeometryGeoJson;
        street.BoundsGeoJson = summary.BoundsGeoJson;
        street.CentroidLatitude = summary.CentroidLatitude;
        street.CentroidLongitude = summary.CentroidLongitude;
        street.Source = request.Source.Trim();
        street.ExternalSourceId = request.ExternalSourceId.Trim();
        street.GeometryStatus = "pending_review";
        street.ApprovedAt = null;
        street.ApprovedByUserId = null;
        street.ReviewNotes = request.Notes?.Trim();
        street.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return Ok(StreetsController.ToResponse(street));
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(
        long id,
        StreetReviewDecision decision,
        CancellationToken cancellationToken)
    {
        var street = await context.CanonicalStreets.Include(item => item.District)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (street is null) return NotFound();
        var summary = StreetGeoJson.SummarizeGeoJson(street.GeometryGeoJson);
        if (summary is null)
            return Conflict(new { message = "Geometry is missing or invalid. Add verified geometry before approval." });
        var outsideDistrict = street.District.BoundaryGeoJson is not null &&
            !StreetGeoJson.PointInsideBoundary(
                summary.CentroidLongitude, summary.CentroidLatitude,
                street.District.BoundaryGeoJson);
        if (outsideDistrict && !decision.AllowOutsideDistrict)
            return Conflict(new { message = "The street centroid is outside its district. Correct it or explicitly review the cross-district exception." });
        if (!ValidLanguageNames(street))
            return Conflict(new { message = "Georgian/English names are missing or assigned to the wrong language." });
        street.GeometryStatus = "approved";
        street.ApprovedAt = DateTime.UtcNow;
        street.ApprovedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        street.ReviewNotes = decision.Notes?.Trim();
        await context.SaveChangesAsync(cancellationToken);
        // Propagate an approved exact-name geometry to the supplied official
        // street catalog immediately; otherwise it would remain catalog-only
        // until the API process is restarted.
        await OfficialStreetCatalogSeeder.SeedAsync(context, cancellationToken);
        return Ok(new { street.Id, street.GeometryStatus, street.ApprovedAt });
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(
        long id,
        StreetReviewDecision decision,
        CancellationToken cancellationToken)
    {
        var street = await context.CanonicalStreets.FindAsync([id], cancellationToken);
        if (street is null) return NotFound();
        street.GeometryStatus = street.GeometryGeoJson is null ? "geometry_missing" : "rejected";
        street.ApprovedAt = null;
        street.ApprovedByUserId = null;
        street.ReviewNotes = decision.Notes?.Trim();
        await context.SaveChangesAsync(cancellationToken);
        return Ok(new { street.Id, street.GeometryStatus });
    }

    [HttpPost("areas/{id:long}/approve")]
    public async Task<IActionResult> ApproveArea(long id, CancellationToken cancellationToken)
    {
        var area = await context.LocationAreas.FindAsync([id], cancellationToken);
        if (area is null) return NotFound();
        if (!StreetGeoJson.IsValidBoundary(area.BoundaryGeoJson))
            return Conflict(new { message = "Boundary geometry is missing or invalid." });
        if (string.IsNullOrWhiteSpace(area.Source) || string.IsNullOrWhiteSpace(area.ExternalSourceId))
            return Conflict(new { message = "Boundary source metadata is missing." });
        area.GeometryStatus = "approved";
        area.ApprovedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return Ok(new { area.Id, area.GeometryStatus });
    }

    [HttpPost("approve-all-verified")]
    public async Task<IActionResult> ApproveAllVerified(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var areas = await context.LocationAreas
            .Where(area => area.Type == "district" && area.GeometryStatus != "approved")
            .ToListAsync(cancellationToken);
        var approvedAreaIds = new List<long>();
        var skippedAreas = new List<object>();
        foreach (var area in areas)
        {
            if (!StreetGeoJson.IsValidBoundary(area.BoundaryGeoJson) ||
                string.IsNullOrWhiteSpace(area.Source) ||
                string.IsNullOrWhiteSpace(area.ExternalSourceId))
            {
                skippedAreas.Add(new { area.Id, area.NameEn, reason = "Missing/invalid geometry or source metadata" });
                continue;
            }
            area.GeometryStatus = "approved";
            area.ApprovedAt = now;
            approvedAreaIds.Add(area.Id);
        }

        var streets = await context.CanonicalStreets
            .Include(street => street.District)
            .Where(street => street.GeometryStatus != "approved")
            .ToListAsync(cancellationToken);
        var approvedStreetIds = new List<long>();
        var skippedStreets = new List<object>();
        foreach (var street in streets)
        {
            var summary = StreetGeoJson.SummarizeGeoJson(street.GeometryGeoJson);
            string? reason = null;
            if (summary is null) reason = "Missing/invalid LineString geometry";
            else if (!ValidLanguageNames(street)) reason = "Missing or invalid Georgian/English names";
            else if (string.IsNullOrWhiteSpace(street.Source) || string.IsNullOrWhiteSpace(street.ExternalSourceId))
                reason = "Missing source metadata";
            else if (street.District.GeometryStatus != "approved" ||
                     !StreetGeoJson.IsValidBoundary(street.District.BoundaryGeoJson))
                reason = "District boundary is not approved";
            else if (!StreetGeoJson.PointInsideBoundary(
                         summary.CentroidLongitude, summary.CentroidLatitude,
                         street.District.BoundaryGeoJson))
                reason = "Centroid is outside the district";

            if (reason is not null)
            {
                skippedStreets.Add(new { street.Id, street.NameEn, reason });
                continue;
            }
            street.GeometryStatus = "approved";
            street.ApprovedAt = now;
            street.ApprovedByUserId = reviewerId;
            street.ReviewNotes = "Bulk-approved after canonical geometry validation.";
            approvedStreetIds.Add(street.Id);
        }
        await context.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            approvedDistricts = approvedAreaIds.Count,
            approvedStreets = approvedStreetIds.Count,
            skippedDistricts = skippedAreas,
            skippedStreets
        });
    }

    [HttpGet("areas/{id:long}")]
    public async Task<IActionResult> GetArea(long id, CancellationToken cancellationToken)
    {
        var area = await context.LocationAreas.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (area is null) return NotFound();
        JsonElement? geometry = null;
        if (!string.IsNullOrWhiteSpace(area.BoundaryGeoJson))
        {
            using var document = JsonDocument.Parse(area.BoundaryGeoJson);
            geometry = document.RootElement.Clone();
        }
        return Ok(new
        {
            area.Id, area.ParentId, area.Type, area.NameKa, area.NameEn, area.Slug,
            geometry, area.Source, area.ExternalSourceId, area.GeometryStatus, area.ApprovedAt
        });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit(CancellationToken cancellationToken)
    {
        var streets = await context.CanonicalStreets.AsNoTracking()
            .Include(street => street.District).ToListAsync(cancellationToken);
        var summaries = streets.ToDictionary(street => street.Id,
            street => StreetGeoJson.SummarizeGeoJson(street.GeometryGeoJson));
        var duplicateNames = streets.GroupBy(street =>
                $"{street.DistrictId}:{NormalizeName(street.NameEn)}:{NormalizeName(street.NameKa)}")
            .Where(group => group.Count() > 1)
            .Select(group => new { key = group.Key, ids = group.Select(item => item.Id).ToArray() })
            .ToArray();
        var sharedGeometry = streets.Where(street => summaries[street.Id] is not null)
            .GroupBy(street => summaries[street.Id]!.GeometryHash)
            .Where(group => group.Count() > 1)
            .Select(group => new { geometryHash = group.Key, ids = group.Select(item => item.Id).ToArray() })
            .ToArray();
        var outsideDistrict = streets.Where(street =>
                summaries[street.Id] is { } summary &&
                street.District.BoundaryGeoJson is not null &&
                !StreetGeoJson.PointInsideBoundary(summary.CentroidLongitude, summary.CentroidLatitude,
                    street.District.BoundaryGeoJson))
            .Select(street => new { street.Id, street.NameEn, district = street.District.NameEn }).ToArray();
        var invalidAliases = streets.Where(street => !ValidLanguageNames(street) ||
                street.Aliases.Any(alias => alias.Length < 2))
            .Select(street => new { street.Id, street.NameKa, street.NameEn, street.Aliases }).ToArray();
        var suspiciousPointLikeGeometry = streets.Where(street =>
                summaries[street.Id] is { } summary &&
                (summary.PointCount < 2 || summary.LengthDegrees < 0.00002))
            .Select(street => new { street.Id, street.NameEn, lengthDegrees = summaries[street.Id]!.LengthDegrees }).ToArray();
        var geometryMissing = streets.Where(street => summaries[street.Id] is null)
            .Select(street => new { street.Id, street.NameEn, street.GeometryStatus }).ToArray();
        var legacyRows = await context.StreetGeometries.AsNoTracking().ToListAsync(cancellationToken);
        var legacySummaries = legacyRows.ToDictionary(
            row => row.Id,
            row =>
            {
                try
                {
                    return StreetGeoJson.SummarizeLines(
                        [JsonSerializer.Deserialize<double[][]>(row.CoordinatesJson) ?? []]);
                }
                catch (JsonException)
                {
                    return null;
                }
            });
        var areasByName = await context.LocationAreas.AsNoTracking()
            .Where(area => area.Type == "district")
            .ToDictionaryAsync(area => area.NameEn, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var legacySharedCoordinates = legacyRows.Where(row => legacySummaries[row.Id] is not null)
            .GroupBy(row => legacySummaries[row.Id]!.GeometryHash)
            .Where(group => group.SelectMany(row => row.Names).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(group => new
            {
                geometryHash = group.Key,
                ids = group.Select(row => row.Id).ToArray(),
                names = group.SelectMany(row => row.Names).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            }).ToArray();
        var legacyDuplicateNames = legacyRows.SelectMany(row => row.Names.Select(name => new { row.Id, row.District, Name = name }))
            .GroupBy(row => $"{row.District}:{NormalizeName(row.Name)}")
            .Where(group => group.Select(item => item.Id).Distinct().Count() > 1)
            .Select(group => new { key = group.Key, ids = group.Select(item => item.Id).Distinct().ToArray() })
            .ToArray();
        var legacyOutsideDistrict = legacyRows.Where(row =>
                legacySummaries[row.Id] is { } summary &&
                areasByName.TryGetValue(row.District, out var area) &&
                area.BoundaryGeoJson is not null &&
                !StreetGeoJson.PointInsideBoundary(summary.CentroidLongitude, summary.CentroidLatitude, area.BoundaryGeoJson))
            .Select(row => new { row.Id, row.District, row.Names }).ToArray();
        var legacyInvalidAliases = legacyRows.Where(row =>
                row.Names.Length == 0 ||
                row.Names.Any(name => string.IsNullOrWhiteSpace(name)) ||
                row.Names.GroupBy(NormalizeName).Any(group => group.Count() > 1))
            .Select(row => new { row.Id, row.District, row.Names }).ToArray();
        var legacyDistrictCenterLike = legacyRows.Where(row =>
                legacySummaries[row.Id] is { } summary &&
                (summary.PointCount < 2 || summary.LengthDegrees < 0.00002))
            .Select(row => new { row.Id, row.District, row.Names, lengthDegrees = legacySummaries[row.Id]!.LengthDegrees })
            .ToArray();
        return Ok(new
        {
            generatedAt = DateTime.UtcNow,
            total = streets.Count,
            approved = streets.Count(street => street.GeometryStatus == "approved"),
            pending = streets.Count(street => street.GeometryStatus == "pending_review"),
            duplicateNames,
            sharedGeometry,
            outsideDistrict,
            invalidAliases,
            suspiciousPointLikeGeometry,
            geometryMissing,
            legacy = new
            {
                total = legacyRows.Count,
                sharedCoordinates = legacySharedCoordinates,
                duplicateNames = legacyDuplicateNames,
                outsideDistrict = legacyOutsideDistrict,
                invalidAliases = legacyInvalidAliases,
                districtCenterLikeGeometry = legacyDistrictCenterLike
            }
        });
    }

    private static bool ValidLanguageNames(Models.CanonicalStreet street) =>
        street.NameKa.Length > 0 && GeorgianText().IsMatch(street.NameKa) &&
        street.NameEn.Length > 0 && LatinText().IsMatch(street.NameEn) &&
        !GeorgianText().IsMatch(street.NameEn);
    private static string NormalizeName(string value) =>
        NonLetters().Replace(value.Trim().ToLowerInvariant(), string.Empty);

    [GeneratedRegex("[\\u10A0-\\u10FF]")]
    private static partial Regex GeorgianText();
    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex LatinText();
    [GeneratedRegex("[^a-z0-9\\u10A0-\\u10FF]")]
    private static partial Regex NonLetters();
}
