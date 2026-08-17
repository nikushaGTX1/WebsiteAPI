using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/locations/catalog")]
public sealed class LocationCatalogController(AppDbContext context) : ControllerBase
{
    public sealed record PointRequest(double Latitude, double Longitude);
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var areas = await context.LocationAreas
            .AsNoTracking()
            .Where(area => area.Type == "city" || area.Type == "district")
            .OrderBy(area => area.Type)
            .ThenBy(area => area.NameEn)
            .Select(area => new
            {
                area.Id,
                area.ParentId,
                area.Type,
                area.NameKa,
                area.NameEn,
                area.Slug,
                area.GeometryStatus
            })
            .ToListAsync(cancellationToken);
        return Ok(areas);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetArea(long id, CancellationToken cancellationToken)
    {
        var area = await context.LocationAreas.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (area is null) return NotFound();
        if (area.GeometryStatus != "approved" || area.BoundaryGeoJson is null)
        {
            return Ok(new
            {
                area.Id,
                area.ParentId,
                area.Type,
                area.NameKa,
                area.NameEn,
                area.Slug,
                geometry = (object?)null,
                geometryStatus = area.GeometryStatus,
                message = "This area has no approved boundary geometry."
            });
        }
        using var document = JsonDocument.Parse(area.BoundaryGeoJson);
        return Ok(new
        {
            area.Id,
            area.ParentId,
            area.Type,
            area.NameKa,
            area.NameEn,
            area.Slug,
            geometry = document.RootElement.Clone(),
            area.Source,
            area.ExternalSourceId,
            area.GeometryStatus
        });
    }

    [HttpPost("resolve-point")]
    public async Task<IActionResult> ResolvePoint(
        PointRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
            return BadRequest(new { message = "Invalid coordinates." });
        var districts = await context.LocationAreas.AsNoTracking()
            .Where(area => area.Type == "district" &&
                area.GeometryStatus == "approved" && area.BoundaryGeoJson != null)
            .ToListAsync(cancellationToken);
        var area = districts.FirstOrDefault(item => StreetGeoJson.PointInsideBoundary(
            request.Longitude, request.Latitude, item.BoundaryGeoJson));
        return area is null ? NotFound() : Ok(new
        {
            area.Id, area.NameKa, area.NameEn, area.Slug
        });
    }
}
