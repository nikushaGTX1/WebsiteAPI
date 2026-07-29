using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Website_API.Data;

namespace Website_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[OutputCache(PolicyName = "StaticLocations")]
public class LocationsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetLocations(
        [FromQuery] string? city = null,
        [FromQuery] string? region = null,
        [FromQuery] string? district = null,
        [FromQuery] string? search = null)
    {
        Response.Headers.Append(
            "X-Location-Data-Attribution",
            "(c) OpenStreetMap contributors");

        var allLocations = StreetData.StreetsList.Append(new()
        {
            Id = 0,
            City = "Tbilisi",
            Region = "Tbilisi",
            District = "All Tbilisi",
            StreetNames = TbilisiStreetData.Names
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        });

        var query = allLocations.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(city))
        {
            var cityTerm =
                GeorgianLocationTranslations.FindEnglishCity(city) ?? city.Trim();
            query = query.Where(item =>
                item.City.Equals(cityTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            var regionTerm =
                GeorgianLocationTranslations.FindEnglishRegion(region) ?? region.Trim();
            query = query.Where(item =>
                item.Region.Equals(regionTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(district))
        {
            var districtTerm =
                GeorgianLocationTranslations.FindEnglishDistrict(district) ??
                district.Trim();
            query = query.Where(item =>
                item.District.Equals(
                    districtTerm,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                item.City.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (item.CityGeorgian?.Contains(
                    term,
                    StringComparison.OrdinalIgnoreCase) ?? false) ||
                item.Region.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (item.RegionGeorgian?.Contains(
                    term,
                    StringComparison.OrdinalIgnoreCase) ?? false) ||
                item.District.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (item.DistrictGeorgian?.Contains(
                    term,
                    StringComparison.OrdinalIgnoreCase) ?? false) ||
                item.StreetNames.Any(street =>
                    street.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                item.StreetNamesGeorgian.Any(street =>
                    street?.Contains(
                        term,
                        StringComparison.OrdinalIgnoreCase) ?? false));
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

    [HttpGet("cities/bilingual")]
    public IActionResult GetBilingualCities() =>
        Ok(StreetData.StreetsList
            .Select(item => item.City)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(city => city)
            .Select(city => new
            {
                English = city,
                Georgian = GeorgianLocationTranslations.FindCity(city)
            }));

    [HttpGet("resolve-street")]
    public IActionResult ResolveStreet([FromQuery] string? street)
    {
        if (string.IsNullOrWhiteSpace(street))
        {
            return BadRequest(new
            {
                message = "The street query parameter is required."
            });
        }

        var matches = StreetDistrictResolver.Find(street);

        return Ok(new
        {
            street = street.Trim(),
            resolved = matches.Count == 1,
            ambiguous = matches.Count > 1,
            matches
        });
    }
}
