using System.Text.RegularExpressions;

namespace Website_API.Services;

/// <summary>Cleans listing text on import and save.</summary>
public static partial class ListingTextNormalizer
{
    // The same short abbreviation twice in a row: "შარაშიძე ქ. ქ." -> "შარაშიძე ქ.".
    [GeneratedRegex(@"(?<=^|\s)(\p{L}{1,6}\.)(?:\s+\1)+(?=[\s,]|$)")]
    private static partial Regex RepeatedAbbreviation();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex ExtraSpaces();

    [GeneratedRegex(@"\s+,")]
    private static partial Regex SpaceBeforeComma();

    public static string? CleanAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value is null ? null : value.Trim();
        var text = RepeatedAbbreviation().Replace(value.Trim(), "$1");
        text = ExtraSpaces().Replace(text, " ");
        return SpaceBeforeComma().Replace(text, ",");
    }

    /// <summary>Optional per-language title: blank means "no translation, use the original".</summary>
    public static string? CleanTitle(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
