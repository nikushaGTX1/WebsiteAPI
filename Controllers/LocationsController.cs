using Microsoft.AspNetCore.Mvc;
using Website_API.Data;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetLocations(
        [FromQuery] string? city = null,
        [FromQuery] string? region = null,
        [FromQuery] string? district = null,
        [FromQuery] string? search = null)
    {
        var query = StreetData.StreetsList.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(item =>
                item.City.Equals(city.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(region))
            query = query.Where(item =>
                item.Region.Equals(region.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(district))
            query = query.Where(item =>
                item.District.Equals(district.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                item.City.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Region.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.District.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.StreetNames.Any(street =>
                    street.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        return Ok(query
            .OrderBy(item => item.City)
            .ThenBy(item => item.Region)
            .ThenBy(item => item.District)
            .ToList());
    }

    [HttpGet("cities")]
    public IActionResult GetCities() =>
        Ok(StreetData.StreetsList
            .Select(item => item.City)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(city => city));
}
