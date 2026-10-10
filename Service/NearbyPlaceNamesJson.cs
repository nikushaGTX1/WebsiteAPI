using System.Text.Json;

namespace Website_API.Services;

/// <summary>The nearest place's name and the walking minutes to it.</summary>
public sealed record NearestPlaceWalk(string? Name, int Minutes);

/// <summary>
/// Reads/writes Apartment.NearbyPlaceNames: a small JSON object keyed like the minutes
/// fields, e.g. {"groceryDistanceMinutes":"Carrefour","petStoreDistanceMinutes":"Pet City"}.
/// </summary>
public static class NearbyPlaceNamesJson
{
    public static Dictionary<string, string> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string? Write(Dictionary<string, string> names)
    {
        if (names.Count == 0) return null;
        var json = JsonSerializer.Serialize(names);
        return json.Length <= 4000 ? json : null;
    }
}
