using System.Text;
using Website_API.Models;

namespace Website_API.Data;

public sealed record StreetDistrictMatch(
    int Id,
    string City,
    string Region,
    string District,
    string? CityGeorgian,
    string? RegionGeorgian,
    string? DistrictGeorgian);

public static class StreetDistrictResolver
{
    public static IReadOnlyList<StreetDistrictMatch> Find(string? street)
    {
        if (string.IsNullOrWhiteSpace(street))
        {
            return [];
        }

        var keys = GetLookupKeys(street);

        return StreetData.StreetsList
            .Where(area => area.StreetNames.Any(name =>
                GetLookupKeys(name).Overlaps(keys) ||
                GetLookupKeys(
                    GeorgianStreetTranslations.Find(name) ?? string.Empty)
                    .Overlaps(keys)))
            .Select(ToMatch)
            .DistinctBy(match => match.Id)
            .OrderBy(match => match.City)
            .ThenBy(match => match.Region)
            .ThenBy(match => match.District)
            .ToList();
    }

    private static StreetDistrictMatch ToMatch(StreetModels area) => new(
        area.Id,
        area.City,
        area.Region,
        area.District,
        area.CityGeorgian,
        area.RegionGeorgian,
        area.DistrictGeorgian);

    private static HashSet<string> GetLookupKeys(string value)
    {
        var english =
            GeorgianStreetTranslations.FindEnglish(value) ?? value;
        var keys = new HashSet<string>(StringComparer.Ordinal);

        AddKey(keys, value);
        AddKey(keys, english);

        return keys;
    }

    private static void AddKey(HashSet<string> keys, string value)
    {
        var normalized = Normalize(value);
        if (normalized.Length == 0)
        {
            return;
        }

        keys.Add(normalized);

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1 && StreetSuffixes.Contains(words[^1]))
        {
            keys.Add(string.Join(' ', words[..^1]));
        }
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSpace = true;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static readonly HashSet<string> StreetSuffixes =
        new(StringComparer.Ordinal)
        {
            "st",
            "street",
            "ave",
            "avenue",
            "road",
            "rd",
            "ქუჩა",
            "გამზირი"
        };
}
