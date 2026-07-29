namespace Website_API.Data;

public sealed record ResolvedApartmentLocation(
    string City,
    string Region,
    string District,
    string Street);

public static class ApartmentLocationResolver
{
    public static bool TryResolve(
        string? district,
        string? street,
        out ResolvedApartmentLocation location)
    {
        location = null!;

        var streetValue = street?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(streetValue))
        {
            streetValue =
                GeorgianStreetTranslations.FindEnglish(streetValue) ??
                streetValue;
        }

        var districtValue = string.IsNullOrWhiteSpace(district)
            ? null
            : GeorgianLocationTranslations.FindEnglishDistrict(district) ??
              district.Trim();

        if (districtValue is null ||
            districtValue.Equals(
                "All Tbilisi",
                StringComparison.OrdinalIgnoreCase) ||
            districtValue.Equals(
                "Tbilisi",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var area = StreetData.StreetsList.FirstOrDefault(item =>
            item.District.Equals(
                districtValue,
                StringComparison.OrdinalIgnoreCase));
        if (area is null || string.IsNullOrWhiteSpace(streetValue))
        {
            return false;
        }

        var streetMatches = StreetDistrictResolver.Find(street);
        if (!streetMatches.Any(match =>
                match.Id == area.Id))
        {
            return false;
        }

        location = new ResolvedApartmentLocation(
            area.City,
            area.Region,
            area.District,
            streetValue);

        return true;
    }
}
