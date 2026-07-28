namespace Website_API.Data;

/// <summary>
/// Manually reviewed Georgian names for entries that cannot be matched
/// unambiguously against the imported bilingual map data.
/// </summary>
public static class GeorgianStreetOverrides
{
    private static readonly Dictionary<string, string> Values =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Mcxeta st."] = "მცხეთის ქუჩა",
            ["Mckheta st."] = "მცხეთის ქუჩა",
            ["m. aleksidze st."] = "ალექსიძე მერაბის ქუჩა"
        };

    public static string? Find(string englishName) =>
        Values.TryGetValue(englishName.Trim(), out var georgian)
            ? georgian
            : null;

    public static string? FindEnglish(string georgianName) =>
        Values.FirstOrDefault(item =>
            item.Value.Equals(
                georgianName.Trim(),
                StringComparison.OrdinalIgnoreCase)).Key;
}
