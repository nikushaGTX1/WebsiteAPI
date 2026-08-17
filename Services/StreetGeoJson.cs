using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Website_API.Services;

public sealed record StreetGeometrySummary(
    string GeometryGeoJson,
    string BoundsGeoJson,
    double CentroidLatitude,
    double CentroidLongitude,
    int PointCount,
    double LengthDegrees,
    string GeometryHash);

public static class StreetGeoJson
{
    public static StreetGeometrySummary? SummarizeLines(IEnumerable<double[][]> sourceLines)
    {
        var lines = sourceLines
            .Where(line => line is { Length: >= 2 })
            .Select(line => line
                .Where(IsCoordinate)
                .Select(point => new[] { point[0], point[1] })
                .ToArray())
            .Where(line => line.Length >= 2 && line.DistinctBy(CoordinateKey).Count() >= 2)
            .ToArray();
        if (lines.Length == 0) return null;

        var points = lines.SelectMany(line => line).ToArray();
        var west = points.Min(point => point[0]);
        var east = points.Max(point => point[0]);
        var south = points.Min(point => point[1]);
        var north = points.Max(point => point[1]);
        var totalLength = 0d;
        var weightedLongitude = 0d;
        var weightedLatitude = 0d;

        foreach (var line in lines)
        {
            for (var index = 1; index < line.Length; index++)
            {
                var first = line[index - 1];
                var second = line[index];
                var length = Math.Sqrt(
                    Math.Pow(second[0] - first[0], 2) +
                    Math.Pow(second[1] - first[1], 2));
                if (length <= 0) continue;
                totalLength += length;
                weightedLongitude += ((first[0] + second[0]) / 2) * length;
                weightedLatitude += ((first[1] + second[1]) / 2) * length;
            }
        }

        var centroidLongitude = totalLength > 0
            ? weightedLongitude / totalLength
            : points.Average(point => point[0]);
        var centroidLatitude = totalLength > 0
            ? weightedLatitude / totalLength
            : points.Average(point => point[1]);
        var geometry = JsonSerializer.Serialize(new
        {
            type = lines.Length == 1 ? "LineString" : "MultiLineString",
            coordinates = lines.Length == 1 ? (object)lines[0] : lines
        });
        var bounds = JsonSerializer.Serialize(new
        {
            type = "Polygon",
            coordinates = new[]
            {
                new[]
                {
                    new[] { west, south }, new[] { east, south },
                    new[] { east, north }, new[] { west, north },
                    new[] { west, south }
                }
            }
        });
        var canonical = string.Join('|', lines
            .Select(line => string.Join(';', line.Select(CoordinateKey)))
            .OrderBy(value => value, StringComparer.Ordinal));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new StreetGeometrySummary(
            geometry, bounds, centroidLatitude, centroidLongitude,
            points.Length, totalLength, hash);
    }

    public static StreetGeometrySummary? SummarizeGeoJson(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson)) return null;
        try
        {
            using var document = JsonDocument.Parse(geoJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement) ||
                !root.TryGetProperty("coordinates", out var coordinates)) return null;
            var type = typeElement.GetString();
            if (type == "LineString")
            {
                return SummarizeLines([ReadLine(coordinates)]);
            }
            if (type == "MultiLineString")
            {
                return SummarizeLines(coordinates.EnumerateArray().Select(ReadLine));
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static double[][][] ReadGeoJsonLines(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson)) return [];
        try
        {
            using var document = JsonDocument.Parse(geoJson);
            var root = document.RootElement;
            var type = root.GetProperty("type").GetString();
            var coordinates = root.GetProperty("coordinates");
            return type switch
            {
                "LineString" => [ReadLine(coordinates)],
                "MultiLineString" => coordinates.EnumerateArray().Select(ReadLine).ToArray(),
                _ => []
            };
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static bool LineIntersectsPolygon(double[][] line, double[][] ring)
    {
        if (line.Any(point => PointInsideRing(point[0], point[1], ring))) return true;
        for (var lineIndex = 1; lineIndex < line.Length; lineIndex++)
        {
            for (var ringIndex = 1; ringIndex < ring.Length; ringIndex++)
            {
                if (SegmentsIntersect(line[lineIndex - 1], line[lineIndex],
                        ring[ringIndex - 1], ring[ringIndex])) return true;
            }
        }
        return false;
    }

    public static bool PointInsideBoundary(double longitude, double latitude, string? boundaryGeoJson)
    {
        if (string.IsNullOrWhiteSpace(boundaryGeoJson)) return false;
        try
        {
            using var document = JsonDocument.Parse(boundaryGeoJson);
            var root = document.RootElement;
            var type = root.GetProperty("type").GetString();
            var coordinates = root.GetProperty("coordinates");
            return type switch
            {
                "Polygon" => PointInsidePolygon(longitude, latitude, coordinates),
                "MultiPolygon" => coordinates.EnumerateArray()
                    .Any(polygon => PointInsidePolygon(longitude, latitude, polygon)),
                _ => false
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool PointInsidePolygon(double longitude, double latitude, JsonElement polygon)
    {
        var rings = polygon.EnumerateArray().ToArray();
        if (rings.Length == 0 || !PointInsideRing(longitude, latitude, rings[0])) return false;
        return rings.Skip(1).All(ring => !PointInsideRing(longitude, latitude, ring));
    }

    private static bool PointInsideRing(double longitude, double latitude, JsonElement ringElement)
    {
        var ring = ReadLine(ringElement);
        return PointInsideRing(longitude, latitude, ring);
    }

    private static bool PointInsideRing(double longitude, double latitude, double[][] ring)
    {
        var inside = false;
        for (var current = 0; current < ring.Length; current++)
        {
            var previous = current == 0 ? ring.Length - 1 : current - 1;
            var currentPoint = ring[current];
            var previousPoint = ring[previous];
            var crosses = (currentPoint[1] > latitude) != (previousPoint[1] > latitude) &&
                longitude < (previousPoint[0] - currentPoint[0]) *
                (latitude - currentPoint[1]) /
                (previousPoint[1] - currentPoint[1]) + currentPoint[0];
            if (crosses) inside = !inside;
        }
        return inside;
    }

    private static bool SegmentsIntersect(double[] a, double[] b, double[] c, double[] d)
    {
        static double Cross(double[] p, double[] q, double[] r) =>
            (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]);
        var abC = Cross(a, b, c);
        var abD = Cross(a, b, d);
        var cdA = Cross(c, d, a);
        var cdB = Cross(c, d, b);
        return ((abC > 0 && abD < 0) || (abC < 0 && abD > 0)) &&
            ((cdA > 0 && cdB < 0) || (cdA < 0 && cdB > 0));
    }

    private static double[][] ReadLine(JsonElement element) => element
        .EnumerateArray()
        .Where(point => point.ValueKind == JsonValueKind.Array && point.GetArrayLength() >= 2)
        .Select(point => new[] { point[0].GetDouble(), point[1].GetDouble() })
        .ToArray();

    private static bool IsCoordinate(double[] point) =>
        point is { Length: >= 2 } &&
        double.IsFinite(point[0]) && double.IsFinite(point[1]) &&
        point[0] is >= -180 and <= 180 && point[1] is >= -90 and <= 90;

    private static string CoordinateKey(double[] point) =>
        $"{point[0]:F7},{point[1]:F7}";
}
