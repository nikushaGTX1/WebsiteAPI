using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StreetsController(AppDbContext context) : ControllerBase
{
    public sealed record PolygonRequest(double[][][] Coordinates);
    [HttpGet]
    public async Task<IActionResult> GetApproved(
        [FromQuery] long? districtId,
        CancellationToken cancellationToken)
    {
        var query = context.CanonicalStreets
            .AsNoTracking()
            .Where(street =>
                (street.GeometryStatus == "approved" && street.GeometryGeoJson != null) ||
                street.Source == OfficialStreetCatalog.Source);
        if (districtId.HasValue)
            query = query.Where(street => street.DistrictId == districtId.Value);
        var streets = await query
            .OrderBy(street => street.NameEn)
            .Select(street => new
            {
                street.Id,
                street.NameKa,
                street.NameEn,
                street.Aliases,
                street.CityId,
                street.DistrictId,
                District = street.District.NameEn,
                GeometryStatus = street.GeometryStatus
            })
            .ToListAsync(cancellationToken);
        return Ok(streets);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetApprovedStreet(
        long id,
        CancellationToken cancellationToken)
    {
        var street = await context.CanonicalStreets
            .AsNoTracking()
            .Include(item => item.District)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (street is null) return NotFound();
        return Ok(ToResponse(street));
    }

    [HttpPost("intersecting")]
    public async Task<IActionResult> Intersecting(
        PolygonRequest request,
        CancellationToken cancellationToken)
    {
        var ring = request.Coordinates.FirstOrDefault();
        if (ring is null || ring.Length < 4)
            return BadRequest(new { message = "A valid polygon ring is required." });
        var streets = await context.CanonicalStreets.AsNoTracking()
            .Include(street => street.District)
            .Where(street => street.GeometryStatus == "approved" && street.GeometryGeoJson != null)
            .ToListAsync(cancellationToken);
        var matches = streets.Where(street => StreetGeoJson
                .ReadGeoJsonLines(street.GeometryGeoJson)
                .Any(line => StreetGeoJson.LineIntersectsPolygon(line, ring)))
            .Select(street => new
            {
                street.Id, street.NameKa, street.NameEn,
                street.DistrictId, District = street.District.NameEn
            })
            .OrderBy(street => street.NameEn)
            .ToArray();
        return Ok(matches);
    }

    internal static object ToResponse(Models.CanonicalStreet street) => new
    {
        street.Id,
        street.NameKa,
        street.NameEn,
        street.Aliases,
        street.CityId,
        street.DistrictId,
        District = street.District.NameEn,
        Geometry = Parse(street.GeometryGeoJson),
        Bounds = Parse(street.BoundsGeoJson),
        Centroid = street.CentroidLatitude.HasValue && street.CentroidLongitude.HasValue
            ? new { lat = street.CentroidLatitude.Value, lng = street.CentroidLongitude.Value }
            : null,
        street.Source,
        street.ExternalSourceId,
        GeometryStatus = street.GeometryStatus
    };

    internal static JsonElement? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
