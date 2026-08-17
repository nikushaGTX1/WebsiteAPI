using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Models;

namespace Website_API.Services;

public sealed record CanonicalStreetImportResult(
    long DistrictId,
    string District,
    int CandidateCount,
    int CreatedCount,
    int UpdatedCount,
    int MissingNameCount);

public sealed partial class CanonicalStreetImportService(
    AppDbContext context,
    IHttpClientFactory httpClientFactory)
{
    public static readonly IReadOnlyDictionary<string, long> DistrictRelations =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["Vake"] = 14900501,
            ["Saburtalo"] = 5469869,
            ["Vera"] = 13949830,
            ["Mtatsminda"] = 2073140,
            ["Didube"] = 16749659,
            ["Digomi"] = 16356610,
            ["Didi Digomi"] = 18183807,
            ["Gldani"] = 13438812,
            ["Nadzaladevi"] = 10790351,
            ["Isani"] = 18467266,
            ["Samgori"] = 11300436,
            ["Avlabari"] = 18467265,
            ["Sololaki"] = 2073133,
            ["Chugureti"] = 18466649,
            ["Krtsanisi"] = 18467369,
            ["Vashlijvari"] = 20111730
        };

    private static readonly string[] Providers =
    [
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass.private.coffee/api/interpreter",
        "https://overpass-api.de/api/interpreter"
    ];

    public async Task<CanonicalStreetImportResult> ImportDistrictAsync(
        string requestedDistrict,
        CancellationToken cancellationToken)
    {
        var districtName = DistrictRelations.Keys.FirstOrDefault(name =>
            name.Equals(requestedDistrict.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Unsupported Tbilisi district.", nameof(requestedDistrict));
        var city = await EnsureAreaAsync(null, "city", "Tbilisi", 0, cancellationToken);
        var relationId = DistrictRelations[districtName];
        var district = await EnsureAreaAsync(city.Id, "district", districtName, relationId, cancellationToken);
        await StoreBoundaryAsync(district, relationId, cancellationToken);
        using var payload = await DownloadRoadsAsync(relationId, cancellationToken);

        var candidates = payload.RootElement.GetProperty("elements")
            .EnumerateArray()
            .Select(ReadRoad)
            .Where(road => road is not null)
            .Cast<ImportedRoad>()
            // OSM's exact primary `name` is the import identity. This joins
            // segments of one named road without fuzzy or translated-name
            // matching, while preserving every source way ID for review.
            .GroupBy(road => CanonicalKey(road.SourceName), StringComparer.Ordinal)
            .Where(group => group.Key.Length > 0)
            .Select(group => new
            {
                NameEn = group.Select(item => item.NameEn).FirstOrDefault(value => value.Length > 0) ?? string.Empty,
                NameKa = group.Select(item => item.NameKa).FirstOrDefault(value => value.Length > 0) ?? string.Empty,
                Aliases = group.SelectMany(item => item.Aliases)
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Lines = group.Select(item => item.Line).ToArray(),
                ExternalIds = group.Select(item => $"osm:way/{item.OsmWayId}")
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
            })
            .ToArray();

        var existing = await context.CanonicalStreets
            .Where(street => street.DistrictId == district.Id && street.Source == "OpenStreetMap")
            .ToListAsync(cancellationToken);
        var created = 0;
        var updated = 0;
        var missingName = 0;

        foreach (var candidate in candidates)
        {
            var summary = StreetGeoJson.SummarizeLines(candidate.Lines);
            if (summary is null) continue;
            if (candidate.NameEn.Length == 0 || candidate.NameKa.Length == 0) missingName++;
            var externalSourceId = string.Join(',', candidate.ExternalIds);
            var street = existing.FirstOrDefault(item =>
                item.ExternalSourceId == externalSourceId) ??
                existing.FirstOrDefault(item =>
                    Exact(item.NameEn, candidate.NameEn) && Exact(item.NameKa, candidate.NameKa));
            if (street is null)
            {
                street = new CanonicalStreet
                {
                    CityId = city.Id,
                    DistrictId = district.Id,
                    GeometryStatus = "pending_review"
                };
                context.CanonicalStreets.Add(street);
                existing.Add(street);
                created++;
            }
            else
            {
                // A new source version never silently preserves approval. An
                // admin must inspect changed geometry before it is public.
                if (!string.Equals(street.GeometryGeoJson, summary.GeometryGeoJson, StringComparison.Ordinal))
                {
                    street.GeometryStatus = "pending_review";
                    street.ApprovedAt = null;
                    street.ApprovedByUserId = null;
                }
                updated++;
            }
            street.NameEn = candidate.NameEn;
            street.NameKa = candidate.NameKa;
            street.Aliases = candidate.Aliases;
            street.GeometryGeoJson = summary.GeometryGeoJson;
            street.BoundsGeoJson = summary.BoundsGeoJson;
            street.CentroidLatitude = summary.CentroidLatitude;
            street.CentroidLongitude = summary.CentroidLongitude;
            street.Source = "OpenStreetMap";
            street.ExternalSourceId = externalSourceId;
            street.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return new CanonicalStreetImportResult(
            district.Id, districtName, candidates.Length, created, updated, missingName);
    }

    private async Task<LocationArea> EnsureAreaAsync(
        long? parentId,
        string type,
        string nameEn,
        long relationId,
        CancellationToken cancellationToken)
    {
        var slug = Slug(nameEn);
        var area = await context.LocationAreas.FirstOrDefaultAsync(
            item => item.Slug == slug,
            cancellationToken);
        if (area is not null)
        {
            area.ParentId ??= parentId;
            if (string.IsNullOrWhiteSpace(area.NameKa))
                area.NameKa = type == "city"
                    ? GeorgianLocationTranslations.FindCity(nameEn) ?? string.Empty
                    : GeorgianLocationTranslations.FindDistrict(nameEn) ?? string.Empty;
            area.Source = "OpenStreetMap";
            if (relationId > 0) area.ExternalSourceId = $"osm:relation/{relationId}";
            return area;
        }
        area = new LocationArea
        {
            ParentId = parentId,
            Type = type,
            NameEn = nameEn,
            NameKa = type == "city"
                ? GeorgianLocationTranslations.FindCity(nameEn) ?? string.Empty
                : GeorgianLocationTranslations.FindDistrict(nameEn) ?? string.Empty,
            Slug = slug,
            Source = "OpenStreetMap",
            ExternalSourceId = relationId > 0 ? $"osm:relation/{relationId}" : null,
            GeometryStatus = relationId > 0 ? "pending_review" : "geometry_missing"
        };
        context.LocationAreas.Add(area);
        await context.SaveChangesAsync(cancellationToken);
        return area;
    }

    private async Task StoreBoundaryAsync(
        LocationArea district,
        long relationId,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("OpenStreetMap");
        using var response = await client.GetAsync(
            $"https://polygons.openstreetmap.fr/get_geojson.py?id={relationId}&params=0",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        var type = document.RootElement.GetProperty("type").GetString();
        if (type is not ("Polygon" or "MultiPolygon"))
            throw new InvalidOperationException("District source returned invalid boundary geometry.");
        district.BoundaryGeoJson = json;
        district.GeometryStatus = "pending_review";
        district.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<JsonDocument> DownloadRoadsAsync(
        long relationId,
        CancellationToken cancellationToken)
    {
        var query = "[out:json][timeout:120];" +
            $"rel({relationId});map_to_area->.districtArea;" +
            "way(area.districtArea)[\"highway\"][\"name\"];out tags geom;";
        var client = httpClientFactory.CreateClient("OpenStreetMap");
        Exception? last = null;
        foreach (var provider in Providers)
        {
            try
            {
                using var response = await client.GetAsync(
                    $"{provider}?data={Uri.EscapeDataString(query)}",
                    cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.GetProperty("elements").GetArrayLength() > 0) return document;
                document.Dispose();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                last = exception;
            }
        }
        throw new InvalidOperationException("Every OpenStreetMap provider failed.", last);
    }

    private static ImportedRoad? ReadRoad(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var id) ||
            !element.TryGetProperty("tags", out var tags) ||
            !element.TryGetProperty("geometry", out var geometry)) return null;
        var sourceName = ReadTag(tags, "name");
        var nameEn = ReadTag(tags, "name:en");
        var nameKa = ReadTag(tags, "name:ka");
        if (nameEn.Length == 0 && LatinText().IsMatch(sourceName)) nameEn = sourceName;
        if (nameKa.Length == 0 && GeorgianText().IsMatch(sourceName)) nameKa = sourceName;
        var aliases = new[]
        {
            sourceName, nameEn, nameKa, ReadTag(tags, "alt_name"),
            ReadTag(tags, "official_name"), ReadTag(tags, "short_name")
        }.Where(value => value.Length > 0).ToArray();
        var line = geometry.EnumerateArray()
            .Where(point => point.TryGetProperty("lon", out _) && point.TryGetProperty("lat", out _))
            .Select(point => new[]
            {
                point.GetProperty("lon").GetDouble(),
                point.GetProperty("lat").GetDouble()
            }).ToArray();
        return line.Length >= 2
            ? new ImportedRoad(id.GetInt64(), sourceName, nameEn, nameKa, aliases, line)
            : null;
    }

    private static string ReadTag(JsonElement tags, string name) =>
        tags.TryGetProperty(name, out var value) ? value.GetString()?.Trim() ?? string.Empty : string.Empty;
    private static bool Exact(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string CanonicalKey(string sourceName) =>
        sourceName.Trim().ToLowerInvariant();
    private static string Slug(string value) => SlugCharacters().Replace(
        value.Trim().ToLowerInvariant().Replace(' ', '-'), "-").Trim('-');

    private sealed record ImportedRoad(
        long OsmWayId, string SourceName, string NameEn, string NameKa, string[] Aliases, double[][] Line);

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex LatinText();
    [GeneratedRegex("[\\u10A0-\\u10FF]")]
    private static partial Regex GeorgianText();
    [GeneratedRegex("[^a-z0-9-]+")]
    private static partial Regex SlugCharacters();
}
