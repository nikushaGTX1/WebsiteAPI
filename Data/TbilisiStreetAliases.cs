namespace Website_API.Data;

public static partial class TbilisiStreetData
{
    public static string? FindAlias(string englishName)
    {
        var sourceType = StreetType(englishName);
        var sourceTokens = SignificantTokens(englishName);
        if (sourceTokens.Count == 0)
        {
            return null;
        }

        var matches = Values
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.Value) &&
                (sourceType is null || StreetType(item.Key) == sourceType) &&
                sourceTokens.IsSubsetOf(SignificantTokens(item.Key)))
            .Select(item => item.Value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static HashSet<string> SignificantTokens(string value) =>
        value.ToLowerInvariant()
            .Split(
                [' ', '.', ',', '-', '(', ')', '\'', '/'],
                StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !IgnoredAliasTokens.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string? StreetType(string value)
    {
        var tokens = value.ToLowerInvariant()
            .Split(
                [' ', '.', ',', '-', '(', ')'],
                StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Any(token => token is "avenue" or "ave"))
            return "avenue";
        if (tokens.Any(token => token is "street" or "st"))
            return "street";
        if (tokens.Any(token => token is "lane" or "ln"))
            return "lane";
        if (tokens.Any(token => token is "road" or "rd"))
            return "road";
        if (tokens.Contains("square"))
            return "square";
        return null;
    }

    private static readonly HashSet<string> IgnoredAliasTokens =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "street", "st", "avenue", "ave", "road", "rd", "lane", "ln",
            "square", "highway", "hwy", "alley", "the"
        };
}
