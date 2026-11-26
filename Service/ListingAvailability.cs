using System.Text.RegularExpressions;
using Website_API.Models;

namespace Website_API.Services;

/// <summary>
/// When a listing is free to move in. Same rules as the website (listingAvailableFrom in
/// mandatory-requirements.ts): the date field, else the upload form's "Available from:" tag,
/// else a date written in the description ("free from 18 December", "18 დეკემბრიდან"),
/// else a month only ("ბინა თავისუფლდება იანვარში", "available in January",
/// "освобождается в январе"), which counts from the 1st of that month.
/// Null when nothing says, which counts as available now.
/// </summary>
public static class ListingAvailability
{
    private static readonly (string Key, int Month)[] Months =
    [
        ("january", 1), ("february", 2), ("march", 3), ("april", 4), ("may", 5), ("june", 6),
        ("july", 7), ("august", 8), ("september", 9), ("october", 10), ("november", 11), ("december", 12),
        ("sept", 9), ("jan", 1), ("feb", 2), ("mar", 3), ("apr", 4), ("jun", 6), ("jul", 7),
        ("aug", 8), ("sep", 9), ("oct", 10), ("nov", 11), ("dec", 12),
        ("იანვ", 1), ("თებერვ", 2), ("მარტ", 3), ("აპრილ", 4), ("მაის", 5), ("ივნის", 6),
        ("ივლის", 7), ("აგვისტ", 8), ("სექტემბ", 9), ("ოქტომბ", 10), ("ნოემბ", 11), ("დეკემბ", 12),
        ("январ", 1), ("феврал", 2), ("март", 3), ("апрел", 4), ("июн", 6), ("июл", 7),
        ("август", 8), ("сентябр", 9), ("октябр", 10), ("ноябр", 11), ("декабр", 12), ("ма", 5),
    ];

    private const string KaMonths = "იანვ|თებერვ|მარტ|აპრილ|მაის|ივნის|ივლის|აგვისტ|სექტემბ|ოქტომბ|ნოემბ|დეკემბ";
    private const string EnMonths = "january|february|march|april|may|june|july|august|september|october|november|december";
    private const string RuMonths = "январ|феврал|март|апрел|июн|июл|август|сентябр|октябр|ноябр|декабр|ма";

    private static readonly Regex Tag = new(@"(?:^|[|\r\n])\s*Available from:\s*(\d{4}-\d{2}-\d{2})", RegexOptions.IgnoreCase);
    private static readonly Regex WrittenEn = new(
        @"(?:available|free|vacant)\s+(?:from|after|since)\s+(?:the\s+)?(\d{1,2})(?:st|nd|rd|th)?\s+(?:of\s+)?([a-z]+)",
        RegexOptions.IgnoreCase);
    private static readonly Regex WrittenKa = new($@"(\d{{1,2}})\s+({KaMonths})\S*\s*(?:დან|-დან)");
    private static readonly Regex MonthKa = new($@"(?:თავისუფლდება|გათავისუფლდება|თავისუფალი(?:ა| იქნება)|ხელმისაწვდომი(?:ა| იქნება))[^.!?\n|]{{0,20}}?({KaMonths})");
    private static readonly Regex MonthKaBefore = new($@"({KaMonths})\S*\s+(?:თავისუფლდება|გათავისუფლდება)");
    private static readonly Regex MonthEn = new(
        $@"(?:available|free|vacant)\s+(?:from|in|after|since|starting)\s+(?:early\s+|mid[- ]|late\s+)?({EnMonths})\b",
        RegexOptions.IgnoreCase);
    private static readonly Regex MonthRu = new(
        $@"(?:освобо[жд]\S*|свободн\S*|доступн\S*)\s+(?:с|в)\s+({RuMonths})[а-яё]*",
        RegexOptions.IgnoreCase);

    public static DateTime? From(Apartment apartment, DateTime today)
    {
        if (apartment.AvailableFrom is DateTime field) return field.Date;
        var text = apartment.Description ?? "";

        var tag = Tag.Match(text);
        if (tag.Success && DateTime.TryParseExact(tag.Groups[1].Value, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var tagged))
            return tagged.Date;

        var written = WrittenEn.Match(text);
        if (!written.Success) written = WrittenKa.Match(text);
        if (written.Success)
        {
            var day = int.Parse(written.Groups[1].Value);
            var month = MonthOf(written.Groups[2].Value);
            if (month is null || day is < 1 or > 31) return null;
            var year = YearFor(month.Value, today);
            return day <= DateTime.DaysInMonth(year, month.Value) ? new DateTime(year, month.Value, day) : null;
        }

        var only = MonthKa.Match(text);
        if (!only.Success) only = MonthKaBefore.Match(text);
        if (!only.Success) only = MonthEn.Match(text);
        if (!only.Success) only = MonthRu.Match(text);
        if (!only.Success) return null;
        var onlyMonth = MonthOf(only.Groups[1].Value);
        // This month means now.
        if (onlyMonth is null || onlyMonth == today.Month) return null;
        return new DateTime(YearFor(onlyMonth.Value, today), onlyMonth.Value, 1);
    }

    private static int? MonthOf(string word)
    {
        var lower = word.ToLowerInvariant();
        foreach (var (key, month) in Months)
            if (lower.StartsWith(key, StringComparison.Ordinal)) return month;
        return null;
    }

    /// <summary>A month already past this year means next year.</summary>
    private static int YearFor(int month, DateTime today) => month < today.Month ? today.Year + 1 : today.Year;
}
