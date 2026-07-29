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
            if (!ApartmentLocationResolver.TryResolve(
                    null,
                    apartment.Street,
                    out var resolved))
            {
                continue;
            }

            if (apartment.City == resolved.City &&
                apartment.Region == resolved.Region &&
                apartment.District == resolved.District &&
                apartment.Street == resolved.Street)
            {
                continue;
            }

            apartment.City = resolved.City;
            apartment.Region = resolved.Region;
            apartment.District = resolved.District;
            apartment.Street = resolved.Street;
            repaired++;
        }

        if (repaired > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return repaired;
    }
}
