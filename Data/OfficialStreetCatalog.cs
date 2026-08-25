using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Website_API.Models;

namespace Website_API.Data;

public sealed record OfficialStreetArea(string NameKa, IReadOnlyList<string> Streets);

public static class OfficialStreetCatalog
{
    public const string Source = "OfficialTbilisiStreetCatalog";
    private const string ResourceFileName = "tbilisi-complete.txt";

    private static readonly HashSet<string> GroupHeadings =
    [
        "ვაკე-საბურთალო", "ისანი სამგორი", "ისანი-სამგორი",
        "გლდანი-ნაძალადევი", "დიდუბე-ჩუღურეთი", "ძველი თბილისი",
        "თბილისის შემოგარენი"
    ];

    public static IReadOnlyList<OfficialStreetArea> ReadAreas()
    {
        var resourceName = typeof(OfficialStreetCatalog).Assembly
            .GetManifestResourceNames()
            .Single(name => name.EndsWith(
                $"OfficialStreets.{ResourceFileName}",
                StringComparison.OrdinalIgnoreCase));
        using var stream = typeof(OfficialStreetCatalog).Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing official street resource: {resourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var blocks = new List<List<string>>();
        var block = new List<string>();
        foreach (var rawLine in reader.ReadToEnd().Split(['\r', '\n']))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            if (line.All(character => character == '_'))
            {
                AddBlock(blocks, block);
                block = [];
                continue;
            }
            block.Add(line);
        }
        AddBlock(blocks, block);

        var comparer = StringComparer.Create(new CultureInfo("ka-GE"), false);
        return blocks
            .Select(ParseBlock)
            .Where(area => area is not null)
            .Cast<OfficialStreetArea>()
            .GroupBy(area => Normalize(area.NameKa), StringComparer.Ordinal)
            .Select(group => new OfficialStreetArea(
                group.First().NameKa,
                group.SelectMany(area => area.Streets)
                    .GroupBy(Normalize, StringComparer.Ordinal)
                    .Select(names => names.First())
                    .OrderBy(name => name, comparer)
                    .ToArray()))
            .OrderBy(area => area.NameKa, comparer)
            .ToArray();
    }

    public static string ExternalId(string areaName, string streetName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{areaName}\n{streetName}"));
        return $"official:tbilisi:v2:{Convert.ToHexString(bytes)[..24].ToLowerInvariant()}";
    }

    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character)) builder.Append(character);
        }
        var normalized = builder.ToString();
        foreach (var suffix in new[] { "ქუჩა", "გამზირი", "შესახვევი", "მოედანი", "გზატკეცილი", "ქ" })
        {
            if (normalized.EndsWith(suffix, StringComparison.Ordinal) && normalized.Length > suffix.Length)
                return normalized[..^suffix.Length];
        }
        return normalized;
    }

    private static OfficialStreetArea? ParseBlock(List<string> source)
    {
        if (source.Count == 0) return null;
        var lines = source.ToList();
        if (GroupHeadings.Contains(lines[0])) lines.RemoveAt(0);
        if (lines.Count < 2 || IsIndexHeading(lines[0])) return null;

        var areaName = lines[0];
        var streets = lines.Skip(1)
            .Where(line => !IsIndexHeading(line) && !GroupHeadings.Contains(line))
            .GroupBy(Normalize, StringComparer.Ordinal)
            .Where(group => group.Key.Length > 0)
            .Select(group => group.First())
            .ToArray();
        return streets.Length == 0 ? null : new OfficialStreetArea(areaName, streets);
    }

    private static bool IsIndexHeading(string value) =>
        value.All(char.IsDigit) ||
        (value.Length == 1 && value[0] is >= '\u10D0' and <= '\u10F0');

    private static void AddBlock(List<List<string>> blocks, List<string> block)
    {
        if (block.Count > 0) blocks.Add(block);
    }
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
                Type = "city", NameEn = "Tbilisi", NameKa = "თბილისი",
                Slug = "tbilisi", Source = OfficialStreetCatalog.Source,
                GeometryStatus = "geometry_missing"
            };
            context.LocationAreas.Add(city);
            await context.SaveChangesAsync(cancellationToken);
        }

        var catalogAreas = OfficialStreetCatalog.ReadAreas();
        // The supplied city catalog names Didi Dighomi in Georgian and used to
        // create a second geometry-less district. Merge that record into the
        // canonical district before seeding so streets such as Asmati inherit
        // the full reviewed Didi Dighomi coverage.
        var canonicalDidiDigomi = await context.LocationAreas.FirstOrDefaultAsync(
            area => area.Type == "district" && area.Slug == DidiDigomiCoverage.CanonicalSlug,
            cancellationToken);
        if (canonicalDidiDigomi is not null)
        {
            canonicalDidiDigomi.NameKa = DidiDigomiCoverage.NameKa;
            canonicalDidiDigomi.BoundaryGeoJson = DidiDigomiCoverage.BoundaryGeoJson;
            canonicalDidiDigomi.Source = DidiDigomiCoverage.Source;
            canonicalDidiDigomi.ExternalSourceId = DidiDigomiCoverage.ExternalSourceId;
            canonicalDidiDigomi.GeometryStatus = "approved";
            canonicalDidiDigomi.ApprovedAt ??= DateTime.UtcNow;
            canonicalDidiDigomi.UpdatedAt = DateTime.UtcNow;

            var duplicateDistricts = await context.LocationAreas
                .Where(area =>
                    area.Type == "district" &&
                    area.Id != canonicalDidiDigomi.Id &&
                    area.NameKa == DidiDigomiCoverage.NameKa)
                .ToListAsync(cancellationToken);
            if (duplicateDistricts.Count > 0)
            {
                var duplicateIds = duplicateDistricts.Select(area => area.Id).ToArray();
                var duplicateStreets = await context.CanonicalStreets
                    .Where(street => duplicateIds.Contains(street.DistrictId))
                    .ToListAsync(cancellationToken);
                foreach (var street in duplicateStreets)
                    street.DistrictId = canonicalDidiDigomi.Id;
                foreach (var duplicate in duplicateDistricts)
                {
                    duplicate.Type = "legacy_district";
                    duplicate.Source = $"{OfficialStreetCatalog.Source}Legacy";
                }
            }
            await context.SaveChangesAsync(cancellationToken);
        }
        var desiredExternalIds = catalogAreas
            .SelectMany(area => area.Streets.Select(street =>
                OfficialStreetCatalog.ExternalId(area.NameKa, street)))
            .ToHashSet(StringComparer.Ordinal);
        var staleStreets = await context.CanonicalStreets
            .Where(street =>
                street.Source == OfficialStreetCatalog.Source &&
                !desiredExternalIds.Contains(street.ExternalSourceId))
            .ToListAsync(cancellationToken);
        foreach (var staleStreet in staleStreets)
            staleStreet.Source = $"{OfficialStreetCatalog.Source}Legacy";

        var desiredAreaSlugs = catalogAreas
            .Select(area => AreaSlug(area.NameKa))
            .ToHashSet(StringComparer.Ordinal);
        var staleAreas = await context.LocationAreas
            .Where(area =>
                area.Type == "district" &&
                area.Source == OfficialStreetCatalog.Source &&
                !desiredAreaSlugs.Contains(area.Slug))
            .ToListAsync(cancellationToken);
        foreach (var staleArea in staleAreas)
        {
            staleArea.Type = "legacy_district";
            staleArea.Source = $"{OfficialStreetCatalog.Source}Legacy";
        }

        var geometryIndex = (await context.CanonicalStreets
                .AsNoTracking()
                .Where(street =>
                    street.Source != OfficialStreetCatalog.Source &&
                    street.GeometryStatus == "approved" &&
                    street.GeometryGeoJson != null)
                .ToListAsync(cancellationToken))
            .SelectMany(street => new[] { street.NameKa, street.NameEn }.Concat(street.Aliases)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => new { Key = OfficialStreetCatalog.Normalize(name), Street = street }))
            .Where(item => item.Key.Length > 0)
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Street).DistinctBy(street => street.Id).ToArray(),
                StringComparer.Ordinal);

        var changes = 0;
        foreach (var area in catalogAreas)
        {
            var slug = AreaSlug(area.NameKa);
            var district = area.NameKa == DidiDigomiCoverage.NameKa
                ? canonicalDidiDigomi
                : await context.LocationAreas.FirstOrDefaultAsync(
                    item => item.Type == "district" && item.Slug == slug,
                    cancellationToken);
            if (district is null)
            {
                district = new LocationArea
                {
                    ParentId = city.Id, Type = "district",
                    NameEn = area.NameKa, NameKa = area.NameKa, Slug = slug,
                    Source = OfficialStreetCatalog.Source,
                    GeometryStatus = "geometry_missing"
                };
                context.LocationAreas.Add(district);
                await context.SaveChangesAsync(cancellationToken);
            }

            var existing = await context.CanonicalStreets
                .Where(street => street.DistrictId == district.Id && street.Source == OfficialStreetCatalog.Source)
                .ToDictionaryAsync(street => street.ExternalSourceId, cancellationToken);

            foreach (var name in area.Streets)
            {
                var externalId = OfficialStreetCatalog.ExternalId(area.NameKa, name);
                if (!existing.TryGetValue(externalId, out var street))
                {
                    street = new CanonicalStreet
                    {
                        CityId = city.Id, DistrictId = district.Id,
                        NameKa = name, NameEn = name, Aliases = [name],
                        Source = OfficialStreetCatalog.Source,
                        ExternalSourceId = externalId
                    };
                    context.CanonicalStreets.Add(street);
                    changes++;
                }

                var key = OfficialStreetCatalog.Normalize(name);
                var geometryMatches = geometryIndex.GetValueOrDefault(key) ?? [];
                if (geometryMatches.Length == 1)
                {
                    var geometry = geometryMatches[0];
                    street.GeometryGeoJson = geometry.GeometryGeoJson;
                    street.BoundsGeoJson = geometry.BoundsGeoJson;
                    street.CentroidLatitude = geometry.CentroidLatitude;
                    street.CentroidLongitude = geometry.CentroidLongitude;
                    street.GeometryStatus = "approved";
                    street.ReviewNotes = $"Official catalog name matched to approved geometry {geometry.Id}.";
                }
                else
                {
                    street.GeometryStatus = "catalog_only";
                    street.ReviewNotes = geometryMatches.Length > 1
                        ? "Geometry match is ambiguous and was not guessed."
                        : "The supplied database contains no coordinates; geometry is awaiting a verified source match.";
                }
                street.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return changes;
    }

    private static string AreaSlug(string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return $"official-{Convert.ToHexString(hash)[..16].ToLowerInvariant()}";
    }
}
