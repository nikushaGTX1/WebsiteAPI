using System.Text.RegularExpressions;
using Website_API.Models;

namespace Website_API.Services;

/// <summary>
/// One answer to "does this listing have parking?" for the listing page, Discover cards and
/// Velven Match. Listings contradicted themselves (#636: the description said a closed
/// parking spot comes with the flat while the "Parking" box was unticked), so the parking
/// type and the description count too.
/// </summary>
public static class ListingParking
{
    private static readonly HashSet<string> ParkingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Garage", "PrivateCourtyard", "CourtyardWithBarrier", "CourtyardWithoutBarrier",
    };

    private static readonly Regex SpotIncluded = new(
        @"parking (?:space|spot|place|lot)\b[^.|\n]{0,40}\b(?:included|comes with|belongs|with the (?:apartment|flat))|" +
        @"(?:one|1|own|private|underground|closed|covered)\s+parking (?:space|spot|place|lot)|" +
        @"(?:garage|parking) (?:is )?included|" +
        @"(?:საკუთარი|კერძო|დახურული|მიწისქვეშა)\s+(?:პარკინგ|ავტოსადგომ)|პარკინგის ადგილი|ავტოსადგომის ადგილი|" +
        @"(?:своё|собственное|подземное|закрытое)\s+парковочное место|паркинг в комплекте",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NoParking = new(
        @"\bno parking\b|parking (?:is )?not (?:included|available)|without parking|პარკინგ\S*\s+არ|без парков",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Street or zone parking is not private parking, even with the "Parking" box ticked.</summary>
    private static readonly HashSet<string> PublicParking = new(StringComparer.OrdinalIgnoreCase) { "StreetParking", "DifficultParking" };

    public static bool Has(Apartment apartment)
    {
        var type = apartment.ParkingCondition?.Trim() ?? "";
        if (PublicParking.Contains(type) || System.Text.RegularExpressions.Regex.IsMatch(apartment.Description ?? "", @"Parking type:\s*(?:Street parking|Parking is difficult)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return false;
        if (apartment.HasParking) return true;
        if (!string.IsNullOrWhiteSpace(apartment.ParkingCondition) && ParkingTypes.Contains(apartment.ParkingCondition.Trim()))
            return true;
        var text = apartment.Description ?? "";
        return !NoParking.IsMatch(text) && SpotIncluded.IsMatch(text);
    }
}
