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

        if (districtValue is null)
        {
            var matches = StreetDistrictResolver.Find(street);
            if (matches.Count != 1)
            {
                return false;
            }

            var match = matches[0];
            location = new ResolvedApartmentLocation(
                match.City,
                match.Region,
                match.District,
                streetValue);
            return true;
        }

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

        location = new ResolvedApartmentLocation(
            area.City,
            area.Region,
            area.District,
            streetValue);

        return true;
    }
}
