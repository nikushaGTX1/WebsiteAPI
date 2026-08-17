using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[OutputCache(PolicyName = "StaticLocations")]
public sealed class StreetGeometriesController(
    AppDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDistrict(
        [FromQuery] string? district,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(district))
        {
            return BadRequest(new
            {
                message = "A supported Tbilisi district is required."
            });
        }

        var rows = await context.StreetGeometries
            .AsNoTracking()
            .Where(street =>
                street.City == "Tbilisi" &&
                EF.Functions.ILike(street.District, district.Trim()))
            .OrderBy(street => street.Id)
            .ToListAsync(cancellationToken);

        Response.Headers.Append(
            "X-Street-Data-Attribution",
            "(c) OpenStreetMap contributors");
        return Ok(new
        {
            district = district.Trim(),
            streets = rows.Select(street => new
            {
                names = street.Names,
                line = JsonSerializer.Deserialize<double[][]>(
                    street.CoordinatesJson) ?? []
            })
        });
    }
}
