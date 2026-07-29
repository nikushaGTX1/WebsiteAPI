using System.Text;
using System.Text.RegularExpressions;

namespace Website_API.Data;

public static partial class GeorgianStreetTransliterator
{
    private static readonly Dictionary<string, string> Words =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["street"] = "ქუჩა",
            ["st"] = "ქუჩა",
            ["avenue"] = "გამზირი",
            ["ave"] = "გამზირი",
            ["road"] = "გზა",
            ["rd"] = "გზა",
            ["lane"] = "შესახვევი",
            ["ln"] = "შესახვევი",
            ["square"] = "მოედანი",
            ["highway"] = "გზატკეცილი",
            ["hwy"] = "გზატკეცილი",
            ["alley"] = "ხეივანი",
            ["descent"] = "დაღმართი",
            ["bridge"] = "ხიდი",
            ["embankment"] = "სანაპირო",
            ["turn"] = "შესახვევი"
        };

    private static readonly (string Latin, string Georgian)[] LetterGroups =
    [
        ("tch", "ჭ"),
        ("shch", "შჩ"),
        ("kh", "ხ"),
        ("gh", "ღ"),
        ("sh", "შ"),
        ("ch", "ჩ"),
        ("ts", "ც"),
        ("dz", "ძ"),
        ("zh", "ჟ"),
        ("ph", "ფ"),
        ("th", "თ")
    ];

    private static readonly Dictionary<char, string> Letters = new()
    {
        ['a'] = "ა", ['b'] = "ბ", ['c'] = "ც", ['d'] = "დ",
        ['e'] = "ე", ['f'] = "ფ", ['g'] = "გ", ['h'] = "ჰ",
        ['i'] = "ი", ['j'] = "ჯ", ['k'] = "კ", ['l'] = "ლ",
        ['m'] = "მ", ['n'] = "ნ", ['o'] = "ო", ['p'] = "პ",
        ['q'] = "ქ", ['r'] = "რ", ['s'] = "ს", ['t'] = "ტ",
        ['u'] = "უ", ['v'] = "ვ", ['w'] = "ვ", ['x'] = "ქს",
        ['y'] = "ი", ['z'] = "ზ"
    };

    public static string Transliterate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var result = WordPattern().Replace(value.Trim(), match =>
        {
            if (Words.TryGetValue(match.Value.TrimEnd('.'), out var translated))
            {
                return translated;
            }

            return TransliterateWord(match.Value);
        });

        return WhitespacePattern().Replace(result, " ").Trim();
    }

    private static string TransliterateWord(string word)
    {
        var source = word.ToLowerInvariant();
        var result = new StringBuilder(source.Length);

        for (var index = 0; index < source.Length;)
        {
            var group = LetterGroups.FirstOrDefault(item =>
                source.AsSpan(index).StartsWith(
                    item.Latin,
                    StringComparison.Ordinal));

            if (group.Latin is not null)
            {
                result.Append(group.Georgian);
                index += group.Latin.Length;
                continue;
            }

            var character = source[index];
            result.Append(
                Letters.TryGetValue(character, out var translated)
                    ? translated
                    : character);
            index++;
        }

        return result.ToString();
    }

    [GeneratedRegex(@"[A-Za-z]+\.?")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
