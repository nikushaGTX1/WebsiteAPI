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

        using var payload = await DownloadDistrictAsync(relationId, cancellationToken);
        var importedAt = DateTime.UtcNow;
        var streets = new List<StreetGeometry>();
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
        long relationId,
        CancellationToken cancellationToken)
    {
        var query =
            $"[out:json][timeout:120];rel({relationId})->.district;" +
            "map_to_area.district->.districtArea;" +
            "way(area.districtArea)[\"highway\"][\"name\"];out tags geom;";
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
}
