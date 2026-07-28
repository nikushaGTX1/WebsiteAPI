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

        if (string.IsNullOrWhiteSpace(district))
        {
            return false;
        }

        var districtValue =
            GeorgianLocationTranslations.FindEnglishDistrict(district) ??
            district.Trim();

        if (districtValue.Equals("All Tbilisi", StringComparison.OrdinalIgnoreCase) ||
            districtValue.Equals("Tbilisi", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var area = StreetData.StreetsList.FirstOrDefault(item =>
            item.District.Equals(
                districtValue,
                StringComparison.OrdinalIgnoreCase));

        if (area is null)
        {
            return false;
        }

        var streetValue = street?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(streetValue))
        {
            streetValue =
                GeorgianStreetTranslations.FindEnglish(streetValue) ??
                streetValue;
        }

        location = new ResolvedApartmentLocation(
            area.City,
            area.Region,
            area.District,
            streetValue);

        return true;
    }
}
