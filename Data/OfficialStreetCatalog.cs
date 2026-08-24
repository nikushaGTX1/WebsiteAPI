using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Website_API.Models;

namespace Website_API.Data;

public sealed record OfficialStreetGroup(
    string NameEn,
    string NameKa,
    string ResourceFileName);

public static class OfficialStreetCatalog
{
    public const string Source = "OfficialTbilisiStreetCatalog";

    public static readonly IReadOnlyList<OfficialStreetGroup> Groups =
    [
        new("Vake-Saburtalo", "ვაკე-საბურთალო", "vake-saburtalo.txt"),
        new("Isani-Samgori", "ისანი-სამგორი", "isani-samgori.txt"),
        new("Gldani-Nadzaladevi", "გლდანი-ნაძალადევი", "gldani-nadzaladevi.txt"),
        new("Didube-Chughureti", "დიდუბე-ჩუღურეთი", "didube-chughureti.txt")
    ];

    public static IReadOnlyList<string> ReadStreetNames(OfficialStreetGroup group)
    {
        var resourceName = typeof(OfficialStreetCatalog).Assembly
            .GetManifestResourceNames()
            .Single(name => name.EndsWith(
                $"OfficialStreets.{group.ResourceFileName}",
                StringComparison.OrdinalIgnoreCase));
        using var stream = typeof(OfficialStreetCatalog).Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing official street resource: {resourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsStreetName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Create(new System.Globalization.CultureInfo("ka-GE"), false))
            .ToArray();
    }

    public static string ExternalId(OfficialStreetGroup group, string streetName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{group.NameEn}\n{streetName}"));
        return $"official:tbilisi:{Convert.ToHexString(bytes)[..24].ToLowerInvariant()}";
    }

    private static bool IsStreetName(string value) =>
        value.Length > 1 &&
        !value.All(char.IsDigit) &&
        !(value.Length == 1 && value[0] is >= '\u10D0' and <= '\u10F0');
}

public static class OfficialStreetCatalogSeeder
{
    public static async Task<int> SeedAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var city = await context.LocationAreas.FirstOrDefaultAsync(
            area => area.Type == "city" && area.NameEn == "Tbilisi",
            cancellationToken);
        if (city is null)
        {
            city = new LocationArea
            {
                Type = "city",
                NameEn = "Tbilisi",
                NameKa = "თბილისი",
                Slug = "tbilisi",
                Source = Source,
                GeometryStatus = "geometry_missing"
            };
            context.LocationAreas.Add(city);
            await context.SaveChangesAsync(cancellationToken);
        }

        var changes = 0;
        foreach (var group in OfficialStreetCatalog.Groups)
        {
            var district = await context.LocationAreas.FirstOrDefaultAsync(
                area => area.Type == "district" && area.NameEn == group.NameEn,
                cancellationToken);
            if (district is null)
            {
                district = new LocationArea
                {
                    ParentId = city.Id,
                    Type = "district",
                    NameEn = group.NameEn,
                    NameKa = group.NameKa,
                    Slug = group.NameEn.ToLowerInvariant(),
                    Source = OfficialStreetCatalog.Source,
                    GeometryStatus = "geometry_missing"
                };
                context.LocationAreas.Add(district);
                await context.SaveChangesAsync(cancellationToken);
            }

            var existing = await context.CanonicalStreets
                .Where(street => street.DistrictId == district.Id && street.Source == OfficialStreetCatalog.Source)
                .ToDictionaryAsync(street => street.ExternalSourceId, cancellationToken);

            foreach (var name in OfficialStreetCatalog.ReadStreetNames(group))
            {
                var externalId = OfficialStreetCatalog.ExternalId(group, name);
                if (existing.ContainsKey(externalId)) continue;
                context.CanonicalStreets.Add(new CanonicalStreet
                {
                    CityId = city.Id,
                    DistrictId = district.Id,
                    NameKa = name,
                    NameEn = name,
                    Aliases = [name],
                    Source = OfficialStreetCatalog.Source,
                    ExternalSourceId = externalId,
                    GeometryStatus = "catalog_only",
                    ReviewNotes = "Imported from the supplied official Georgian street catalog."
                });
                changes++;
            }
        }

        if (changes > 0) await context.SaveChangesAsync(cancellationToken);
        return changes;
    }

    private const string Source = OfficialStreetCatalog.Source;
}
