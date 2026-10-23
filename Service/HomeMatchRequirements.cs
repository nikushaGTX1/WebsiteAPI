using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Website_API.Models;

namespace Website_API.Services;

/// <summary>
/// The answers of a saved Velven Match search, read from the website's profile JSON.
/// Only the fields the mandatory requirements need; the full JSON is kept unchanged.
/// </summary>
public class SavedSearchProfile
{
    public string PropertyGoal { get; set; } = "";
    public List<string> Districts { get; set; } = [];
    public bool LocationFlexible { get; set; }
    public MapArea? SelectedMapArea { get; set; }
    public decimal BudgetMin { get; set; }
    public decimal BudgetMax { get; set; }
    public string Currency { get; set; } = "USD";
    public BudgetConversionSnapshot? BudgetConversion { get; set; }
    public class BudgetConversionSnapshot
    {
        public string Currency { get; set; } = "USD";
        public decimal Min { get; set; }
        public decimal Max { get; set; }
        public decimal UsdGel { get; set; }
        public decimal UsdEur { get; set; }
    }
    public int? Bedrooms { get; set; }
    public List<int> BedroomChoices { get; set; } = [];
    public string? RentalDuration { get; set; }
    public string? MoveInTiming { get; set; }
    public string? MoveInDate { get; set; }
    public bool? HasPet { get; set; }
    public string? PetType { get; set; }
    public List<string> PetTypes { get; set; } = [];
    public string? DogSize { get; set; }

    public class MapArea
    {
        public string? Type { get; set; }
        public List<List<List<double>>>? Coordinates { get; set; }
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static SavedSearchProfile Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SavedSearchProfile>(json, Json) ?? new SavedSearchProfile();
        }
        catch (JsonException)
        {
            return new SavedSearchProfile();
        }
    }

    public static bool IsValid(string json)
    {
        try
        {
            var p = JsonSerializer.Deserialize<SavedSearchProfile>(json, Json);
            if (p is null || p.PropertyGoal is not ("Rent" or "Buy") ||
                p.Districts is null || p.BedroomChoices is null || p.PetTypes is null ||
                p.Districts.Any(x => string.IsNullOrWhiteSpace(x)) ||
                p.PetTypes.Any(x => x is not ("Dog" or "Cat" or "Other")) ||
                p.BedroomChoices.Any(x => x < 0 || x > 4) ||
                p.BudgetMin < 0 || p.BudgetMax < p.BudgetMin ||
                p.Currency is not ("USD" or "GEL" or "EUR")) return false;
            if (p.SelectedMapArea is { } area)
            {
                if (area.Type != "Polygon" || area.Coordinates is not { Count: > 0 }) return false;
                foreach (var ring in area.Coordinates)
                {
                    if (ring is null || ring.Count < 4 || ring.Any(point => point is null || point.Count != 2 ||
                        !double.IsFinite(point[0]) || !double.IsFinite(point[1]) ||
                        Math.Abs(point[0]) > 180 || Math.Abs(point[1]) > 90) ||
                        !ring[0].SequenceEqual(ring[^1])) return false;
                }
            }
            return true;
        }
        catch (JsonException) { return false; }
    }
}

public enum RequirementStatus
{
    /// <summary>Meets every mandatory requirement.</summary>
    Exact,
    /// <summary>Nothing fails, but something could not be verified (e.g. pets not confirmed).</summary>
    Confirm,
    /// <summary>Fails at least one mandatory requirement.</summary>
    Alternative,
}

/// <summary>
/// Velven Match's mandatory requirements, a line-by-line port of the website's
/// mandatory-requirements.ts (plus the page's deal-type, budget-ceiling and pet hard filters),
/// so a saved search alerts about exactly the homes the website would list as exact matches.
/// Change both files together; HomeMatchRequirementsTests mirrors the website's tests.
/// </summary>
public static class HomeMatchRequirements
{
    // GEL and EUR per 1 USD, the website's defaults (DEFAULT_BUDGET_RATES).
    public const decimal UsdGel = 2.7m;
    public const decimal UsdEur = 0.92m;

    private enum Check { Pass, Fail, Unknown }

    private static readonly Dictionary<string, string> GeorgianDistricts = new()
    {
        ["ვაკე"] = "vake",
        ["საბურთალო"] = "saburtalo",
        ["ვერა"] = "vera",
        ["მთაწმინდა"] = "mtatsminda",
        ["დიღომი"] = "digomi",
        ["დიდი დიღომი"] = "didi digomi",
        ["ისანი"] = "isani",
        ["ორთაჭალა"] = "ortachala",
        ["ჩუღურეთი"] = "chugureti",
        ["გლდანი"] = "gldani",
        ["ნაძალადევი"] = "nadzaladevi",
        ["დიდუბე"] = "didube",
        ["სამგორი"] = "samgori",
        ["კრწანისი"] = "krtsanisi",
        ["ავლაბარი"] = "avlabari",
        ["სოლოლაკი"] = "sololaki",
        ["ვაშლიჯვარი"] = "vashlijvari",
    };

    /// <summary>
    /// The whole pipeline the website runs before showing a result: rent/buy, budget ceiling,
    /// pets the listing refuses, then the mandatory requirements.
    /// </summary>
    public static RequirementStatus Evaluate(Apartment apartment, SavedSearchProfile profile, DateTime today)
    {
        if (!MatchesPropertyGoal(apartment, profile)) return RequirementStatus.Alternative;
        if (!WithinBudgetCeiling(apartment.Price, profile)) return RequirementStatus.Alternative;

        var mismatch = false;
        var confirm = false;
        void Apply(Check check)
        {
            if (check == Check.Fail) mismatch = true;
            else if (check == Check.Unknown) confirm = true;
        }

        Apply(CheckLocation(apartment, profile));
        Apply(CheckBedrooms(apartment, profile));
        Apply(CheckRentalDuration(apartment, profile));
        Apply(CheckAvailability(apartment, profile, today));

        // Budget, compared in USD whatever currency the client typed it in.
        var price = apartment.Price;
        var (min, max) = BudgetRangeUsd(profile);
        if (price > 0 && min is not null && price < min) mismatch = true;
        else if (price > 0 && max is not null && price > max + 0.005m)
        {
            // Inside the rental tolerance: shown, but never as an exact match.
            if (price <= MaxRecommendedPriceUsd(max.Value, profile.PropertyGoal) + 0.005m) confirm = true;
            else mismatch = true;
        }

        var pets = IsRent(profile) ? SelectedPets(profile) : [];
        switch (PetPermission(apartment, pets, profile.DogSize))
        {
            case "prohibited": mismatch = true; break;
            case "unconfirmed": confirm = true; break;
        }

        return mismatch ? RequirementStatus.Alternative : confirm ? RequirementStatus.Confirm : RequirementStatus.Exact;
    }

    private static bool IsRent(SavedSearchProfile profile) =>
        string.Equals(profile.PropertyGoal, "Rent", StringComparison.OrdinalIgnoreCase);

    public static bool MatchesPropertyGoal(Apartment apartment, SavedSearchProfile profile)
    {
        var deal = Regex.Match(apartment.Description ?? "", @"(?:^|[|\r\n])\s*(?:Deal|Listing type):\s*([^|\r\n]+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
        var text = deal.Length > 0 ? deal : apartment.Title ?? "";
        if (string.Equals(profile.PropertyGoal, "Buy", StringComparison.OrdinalIgnoreCase))
            return Regex.IsMatch(text, "sale|buy|იყიდება|продаж", RegexOptions.IgnoreCase);
        return Regex.IsMatch(text, "rent|lease|ქირავდება|аренд", RegexOptions.IgnoreCase);
    }

    // ---------- Location ----------

    private static string NormalizeDistrict(string? value)
    {
        var raw = (value ?? "").Trim().ToLowerInvariant();
        if (raw == "") return "";
        if (GeorgianDistricts.TryGetValue(raw, out var georgian)) return georgian;
        raw = raw.Replace("dighomi", "digomi");
        return Regex.Replace(raw, "[^a-z0-9]+", " ").Trim();
    }

    /// <summary>Profile values such as "DidiDighomi" arrive without spaces; make them comparable.</summary>
    private static string NormalizeProfileDistrict(string value) =>
        NormalizeDistrict(Regex.Replace(value, "([a-z])([A-Z])", "$1 $2"));

    private static bool InsidePolygon(double lng, double lat, List<List<double>> ring)
    {
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            double xi = ring[i][0], yi = ring[i][1], xj = ring[j][0], yj = ring[j][1];
            var denominator = yj - yi == 0 ? double.Epsilon : yj - yi;
            if (yi > lat != yj > lat && lng < (xj - xi) * (lat - yi) / denominator + xi) inside = !inside;
        }
        return inside;
    }

    private static Check CheckLocation(Apartment apartment, SavedSearchProfile profile)
    {
        if (profile.LocationFlexible) return Check.Pass;
        var wanted = profile.Districts
            .Where(district => district != "SelectOnMap")
            .Select(NormalizeProfileDistrict)
            .Where(district => district != "")
            .ToList();
        var ring = profile.SelectedMapArea?.Coordinates?.FirstOrDefault();
        if (wanted.Count == 0 && profile.SelectedMapArea is null) return Check.Pass;

        var district = NormalizeDistrict(apartment.District);
        if (district != "" && wanted.Contains(district)) return Check.Pass;

        if (ring is { Count: > 0 })
        {
            var lat = apartment.PropertyLatitude ?? apartment.Latitude;
            var lng = apartment.PropertyLongitude ?? apartment.Longitude;
            if (lat is not null && lng is not null)
            {
                if (InsidePolygon(lng.Value, lat.Value, ring)) return Check.Pass;
            }
            else if (district == "")
            {
                return Check.Unknown;
            }
        }
        if (district == "")
        {
            // Without a district, the address may still name the wanted area.
            var address = NormalizeDistrict(apartment.Address);
            if (address != "" && wanted.Any(item => address.Contains(item))) return Check.Pass;
            return profile.SelectedMapArea is not null ? Check.Fail : Check.Unknown;
        }
        return Check.Fail;
    }

    // ---------- Bedrooms ----------

    public static List<int> SelectedBedrooms(SavedSearchProfile profile)
    {
        if (profile.BedroomChoices.Count > 0) return [.. profile.BedroomChoices.OrderBy(choice => choice)];
        return profile.Bedrooms is int bedrooms ? [bedrooms] : [];
    }

    private static Check CheckBedrooms(Apartment apartment, SavedSearchProfile profile)
    {
        var wanted = SelectedBedrooms(profile);
        if (wanted.Count == 0) return Check.Pass;
        // The API always sends a number here (0 when not given), so the website never
        // reaches its "unknown" branch either.
        var count = apartment.Bedrooms;
        if (wanted.Count == 1) return wanted[0] <= 0 || count >= wanted[0] ? Check.Pass : Check.Fail;
        return wanted.Any(choice => choice >= 4 ? count >= 4 : count == choice) ? Check.Pass : Check.Fail;
    }

    // ---------- Lease and availability ----------

    private static readonly Dictionary<string, int> RequestedStayMonths = new()
    {
        ["ThreeToFiveMonths"] = 3,
        ["SixMonths"] = 6,
        ["TwelveMonths"] = 12,
        ["MoreThanTwelveMonths"] = 12,
    };

    private static int? ListingMinimumMonths(Apartment apartment)
    {
        var match = Regex.Match(apartment.MinimumRentalPeriod ?? "", @"(\d+)\s*month", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private static Check CheckRentalDuration(Apartment apartment, SavedSearchProfile profile)
    {
        if (!IsRent(profile)) return Check.Pass;
        var minimum = ListingMinimumMonths(apartment);
        if (minimum is null) return Check.Pass;
        if (profile.RentalDuration is null || !RequestedStayMonths.TryGetValue(profile.RentalDuration, out var requested))
            return Check.Unknown;
        return requested >= minimum ? Check.Pass : Check.Fail;
    }

    public const int ImmediateMoveInGraceDays = 7;

    private static DateTime? RequestedMoveInDate(SavedSearchProfile profile, DateTime today) => profile.MoveInTiming switch
    {
        // "Immediately" still fits a home that is free within the next week (website: IMMEDIATE_MOVE_IN_GRACE_DAYS).
        "Immediately" => today.Date.AddDays(ImmediateMoveInGraceDays),
        "WithinOneWeek" => today.Date.AddDays(7),
        "WithinOneMonth" => today.Date.AddDays(30),
        "SpecificDate" => DateTime.TryParse(profile.MoveInDate, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date) ? date.Date : null,
        _ => null,
    };

    private static Check CheckAvailability(Apartment apartment, SavedSearchProfile profile, DateTime today)
    {
        if (!IsRent(profile)) return Check.Pass;
        if (apartment.AvailableFrom is null) return Check.Pass;
        var requested = RequestedMoveInDate(profile, today);
        if (requested is null) return Check.Unknown;
        return apartment.AvailableFrom.Value.Date <= requested.Value ? Check.Pass : Check.Fail;
    }

    // ---------- Budget ----------

    public static decimal ToUsd(decimal amount, string currency) => currency switch
    {
        "GEL" => amount / UsdGel,
        "EUR" => amount / UsdEur,
        _ => amount,
    };

    public static (decimal? Min, decimal? Max) BudgetRangeUsd(SavedSearchProfile profile)
    {
        var snapshot = profile.BudgetConversion;
        var valid = snapshot is not null && snapshot.Currency == profile.Currency &&
                    snapshot.Min == profile.BudgetMin && snapshot.Max == profile.BudgetMax &&
                    snapshot.UsdGel > 0 && snapshot.UsdEur > 0;
        decimal Convert(decimal value) => valid ? profile.Currency switch
        {
            "GEL" => value / snapshot!.UsdGel,
            "EUR" => value / snapshot!.UsdEur,
            _ => value,
        } : ToUsd(value, profile.Currency);
        return (profile.BudgetMin > 0 ? Convert(profile.BudgetMin) : null,
                profile.BudgetMax > 0 ? Convert(profile.BudgetMax) : null);
    }

    public static decimal MaxRecommendedPriceUsd(decimal maxUsd, string? goal) =>
        RentalBudgetPolicy.MaxRecommendedPrice(maxUsd, goal);

    private static bool WithinBudgetCeiling(decimal priceUsd, SavedSearchProfile profile)
    {
        var (_, max) = BudgetRangeUsd(profile);
        if (max is null || priceUsd == 0) return true;
        return priceUsd <= MaxRecommendedPriceUsd(max.Value, profile.PropertyGoal) + 0.005m;
    }

    // ---------- Pets ----------

    public static List<string> SelectedPets(SavedSearchProfile profile)
    {
        if (profile.PetTypes.Count > 0) return [.. profile.PetTypes.Distinct()];
        if (!string.IsNullOrEmpty(profile.PetType) && profile.PetType != "None") return [profile.PetType];
        return profile.HasPet == true ? ["Other"] : [];
    }

    private static readonly Regex PetsAllowed = new(
        @"pet[\s-]*friendly:\s*yes\b|pets? (?:are )?(?:allowed|welcome|permitted)|შინაური ცხოველ\S*\s+(?:დასაშვებ|ნებადართ)|можно с (?:домашними )?животными",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>"allowed", "prohibited" (the listing says so) or "unconfirmed" (it says nothing).</summary>
    public static string PetPermission(Apartment apartment, List<string> pets, string? dogSize)
    {
        if (pets.Count == 0) return "allowed";
        if (PetPolicy.IsProhibited(apartment, pets, dogSize)) return "prohibited";
        var text = $"{apartment.Title} {apartment.Description}";
        return apartment.IsPetFriendly || PetsAllowed.IsMatch(text) ? "allowed" : "unconfirmed";
    }
}
