using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Models;

namespace Website_API.Services;

public sealed class StreetGeometryImportService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<StreetGeometryImportService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, Task> imports =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, long> DistrictRelations =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["Vake"] = 14900501,
            ["Saburtalo"] = 5469869,
            ["Vera"] = 13949830,
            ["Mtatsminda"] = 2073140,
            ["Didube"] = 16749659,
            ["Digomi"] = 16356610,
            // Didi Dighomi uses the real-estate coverage bounds below, not
            // the smaller OSM neighbourhood relation.
            ["Didi Digomi"] = 0,
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var district in DistrictRelations.Keys)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await EnsureDistrictAsync(district, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Could not import permanent street geometry for {District}.",
                    district);
            }
        }
    }

    public bool SupportsDistrict(string district) =>
        DistrictRelations.ContainsKey(district);

    public async Task EnsureDistrictAsync(
        string district,
        CancellationToken cancellationToken = default)
    {
        var canonicalDistrict = DistrictRelations.Keys.FirstOrDefault(name =>
            name.Equals(district.Trim(), StringComparison.OrdinalIgnoreCase));
        if (canonicalDistrict is null)
        {
            throw new ArgumentException("Unsupported Tbilisi district.", nameof(district));
        }

        var pending = imports.GetOrAdd(
            canonicalDistrict,
            name => ImportMissingDistrictAsync(
                name,
                DistrictRelations[name],
                CancellationToken.None));
        try
        {
            await pending.WaitAsync(cancellationToken);
        }
        catch
        {
            imports.TryRemove(canonicalDistrict, out _);
            throw;
        }
    }

    private async Task ImportMissingDistrictAsync(
        string district,
        long relationId,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await context.StreetGeometries.AnyAsync(
                street => street.City == "Tbilisi" && street.District == district,
                cancellationToken))
        {
            return;
        }

        using var payload = await DownloadDistrictAsync(district, relationId, cancellationToken);
        var importedAt = DateTime.UtcNow;
        var streets = new List<StreetGeometry>();
        var districtStreetNames = StreetData.StreetsList
            .Where(area =>
                area.City.Equals("Tbilisi", StringComparison.OrdinalIgnoreCase) &&
                area.District.Equals(district, StringComparison.OrdinalIgnoreCase))
            .SelectMany(area => area.StreetNames.SelectMany(name => new[]
            {
                name,
                GeorgianStreetTranslations.Find(name)
            }))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => NormalizeStreetName(name!))
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var element in payload.RootElement.GetProperty("elements").EnumerateArray())
        {
            if (!element.TryGetProperty("id", out var idElement) ||
                !element.TryGetProperty("geometry", out var geometryElement) ||
                geometryElement.GetArrayLength() < 2)
            {
                continue;
            }

            var names = ReadNames(element);
            if (names.Length == 0)
            {
                continue;
            }
            if (districtStreetNames.Count > 0 &&
                !names.Select(NormalizeStreetName).Any(districtStreetNames.Contains))
            {
                continue;
            }

            var coordinates = geometryElement.EnumerateArray()
                .Where(point =>
                    point.TryGetProperty("lon", out _) &&
                    point.TryGetProperty("lat", out _))
                .Select(point => new[]
                {
                    point.GetProperty("lon").GetDouble(),
                    point.GetProperty("lat").GetDouble()
                })
                .ToArray();
            if (coordinates.Length < 2)
            {
                continue;
            }

            streets.Add(new StreetGeometry
            {
                OsmWayId = idElement.GetInt64(),
                City = "Tbilisi",
                District = district,
                Names = names,
                CoordinatesJson = JsonSerializer.Serialize(coordinates),
                UpdatedAt = importedAt
            });
        }

        await context.StreetGeometries.AddRangeAsync(streets, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Saved {StreetSegmentCount} permanent street segments for {District}.",
            streets.Count,
            district);
    }

    private async Task<JsonDocument> DownloadDistrictAsync(
        string district,
        long relationId,
        CancellationToken cancellationToken)
    {
        var (south, west, north, east) =
            district.Equals("Didi Digomi", StringComparison.OrdinalIgnoreCase)
                ? (DidiDigomiCoverage.South, DidiDigomiCoverage.West,
                    DidiDigomiCoverage.North, DidiDigomiCoverage.East)
                : await DownloadBoundaryBoxAsync(relationId, cancellationToken);
        var query =
            "[out:json][timeout:120];" +
            $"way[\"highway\"][\"name\"]({south},{west},{north},{east});" +
            "out tags geom;";
        Exception? lastError = null;
        var client = httpClientFactory.CreateClient("OpenStreetMap");

        foreach (var provider in Providers)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{provider}?data={Uri.EscapeDataString(query)}");
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var document = await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: cancellationToken);
                if (document.RootElement.TryGetProperty("elements", out var elements) &&
                    elements.GetArrayLength() > 0)
                {
                    return document;
                }
                document.Dispose();
                throw new InvalidOperationException("Provider returned no named roads.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastError = exception;
            }
        }

        throw new InvalidOperationException(
            $"Every OpenStreetMap provider failed for relation {relationId}.",
            lastError);
    }

    private async Task<(double South, double West, double North, double East)>
        DownloadBoundaryBoxAsync(
            long relationId,
            CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("OpenStreetMap");
        using var response = await client.GetAsync(
            $"https://polygons.openstreetmap.fr/get_geojson.py?id={relationId}&params=0",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var geometry = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        if (!geometry.RootElement.TryGetProperty("coordinates", out var coordinates))
        {
            throw new InvalidOperationException(
                $"Boundary {relationId} contains no coordinates.");
        }

        var south = double.PositiveInfinity;
        var west = double.PositiveInfinity;
        var north = double.NegativeInfinity;
        var east = double.NegativeInfinity;
        ReadCoordinateBounds(
            coordinates,
            ref south,
            ref west,
            ref north,
            ref east);
        if (!double.IsFinite(south) || !double.IsFinite(west) ||
            !double.IsFinite(north) || !double.IsFinite(east))
        {
            throw new InvalidOperationException(
                $"Boundary {relationId} contains invalid coordinates.");
        }
        return (south, west, north, east);
    }

    private static void ReadCoordinateBounds(
        JsonElement value,
        ref double south,
        ref double west,
        ref double north,
        ref double east)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return;
        }
        if (value.GetArrayLength() >= 2 &&
            value[0].ValueKind == JsonValueKind.Number &&
            value[1].ValueKind == JsonValueKind.Number)
        {
            var longitude = value[0].GetDouble();
            var latitude = value[1].GetDouble();
            south = Math.Min(south, latitude);
            west = Math.Min(west, longitude);
            north = Math.Max(north, latitude);
            east = Math.Max(east, longitude);
            return;
        }
        foreach (var child in value.EnumerateArray())
        {
            ReadCoordinateBounds(child, ref south, ref west, ref north, ref east);
        }
    }

    private static string[] ReadNames(JsonElement element)
    {
        if (!element.TryGetProperty("tags", out var tags))
        {
            return [];
        }

        var names = new List<string>();
        foreach (var key in new[] { "name", "name:en", "name:ka" })
        {
            if (tags.TryGetProperty(key, out var value) &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                names.Add(value.GetString()!.Trim());
            }
        }
        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string NormalizeStreetName(string value)
    {
        var words = new string(
                value.Trim().ToLowerInvariant()
                    .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
                    .ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word is not (
                "street" or "st" or "avenue" or "ave" or "road" or "rd" or
                "lane" or "ln" or "alley" or "square"))
            .ToArray();
        return string.Join(' ', words);
    }
}
