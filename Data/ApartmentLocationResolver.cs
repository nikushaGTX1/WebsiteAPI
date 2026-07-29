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

        var streetMatches = StreetDistrictResolver.Find(street);
        if (streetMatches.Count > 0)
        {
            var streetArea = districtValue is null
                ? streetMatches.Count == 1 ? streetMatches[0] : null
                : streetMatches.FirstOrDefault(match =>
                    match.District.Equals(
                        districtValue,
                        StringComparison.OrdinalIgnoreCase));

            if (streetArea is null && streetMatches.Count == 1)
            {
                streetArea = streetMatches[0];
            }

            if (streetArea is null)
            {
                return false;
            }

            location = new ResolvedApartmentLocation(
                streetArea.City,
                streetArea.Region,
                streetArea.District,
                streetValue);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(streetValue))
        {
            return false;
        }

        if (districtValue is null)
        {
            return false;
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
