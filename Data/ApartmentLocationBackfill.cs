using Microsoft.EntityFrameworkCore;

namespace Website_API.Data;

public static class ApartmentLocationBackfill
{
    public static async Task<int> RepairAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var apartments = await context.Apartments
            .Where(apartment => apartment.Street != "")
            .ToListAsync(cancellationToken);
        var repaired = 0;

        foreach (var apartment in apartments)
        {
            var matches = StreetDistrictResolver.Find(apartment.Street);
            if (matches.Count != 1)
            {
                continue;
            }

            var match = matches[0];
            var canonicalStreet =
                GeorgianStreetTranslations.FindEnglish(apartment.Street) ??
                apartment.Street.Trim();

            if (apartment.City == match.City &&
                apartment.Region == match.Region &&
                apartment.District == match.District &&
                apartment.Street == canonicalStreet)
            {
                continue;
            }

            apartment.City = match.City;
            apartment.Region = match.Region;
            apartment.District = match.District;
            apartment.Street = canonicalStreet;
            repaired++;
        }

        if (repaired > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return repaired;
    }
}
