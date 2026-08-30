using System.Text.Json;
using Website_API.Data;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public sealed class DidiDigomiCoverageTests
{
    public static TheoryData<string, double, double> RealEstateStreetPoints => new()
    {
        { "Asmati Street", 44.752354, 41.785076 },
        { "Mirian Mepe Street", 44.749090, 41.787380 },
        { "Petre Iberi Street", 44.755253, 41.791930 },
        { "Archil Mepe Street", 44.740300, 41.792336 },
        { "Demetre Tavdadebuli Street", 44.756977, 41.796524 },
        { "Ioane Petritsi Street", 44.756844, 41.792470 },
        { "King Parnavaz Avenue", 44.764595, 41.792316 },
        { "Giorgi Brtskinvale Street", 44.764584, 41.788914 },
        { "Didi Dighomi northern/Zurgovana coverage", 44.762753, 41.802334 },
        { "Southern Rostevani road coverage", 44.766000, 41.766000 }
    };

    [Theory]
    [MemberData(nameof(RealEstateStreetPoints))]
    public void ClassifiedStreetPoint_IsInside(string _, double longitude, double latitude)
    {
        Assert.True(StreetGeoJson.PointInsideBoundary(
            longitude, latitude, DidiDigomiCoverage.BoundaryGeoJson));
    }

    [Theory]
    [InlineData("East of coverage", 44.775000, 41.788800)]
    [InlineData("West of coverage", 44.727500, 41.785000)]
    [InlineData("South of coverage", 44.766000, 41.764500)]
    [InlineData("North of coverage", 44.762800, 41.805000)]
    public void ClearlyOutsidePoint_IsOutside(string _, double longitude, double latitude)
    {
        Assert.False(StreetGeoJson.PointInsideBoundary(
            longitude, latitude, DidiDigomiCoverage.BoundaryGeoJson));
    }

    [Theory]
    [InlineData(44.773500, 41.788800)]
    [InlineData(44.729000, 41.785000)]
    [InlineData(44.762800, 41.803500)]
    public void PointJustInsideCoverageEdge_IsInside(double longitude, double latitude)
    {
        Assert.True(StreetGeoJson.PointInsideBoundary(
            longitude, latitude, DidiDigomiCoverage.BoundaryGeoJson));
    }

    [Fact]
    public void Polygon_IsValidClosedGeoJson_WithPublishedExtrema()
    {
        Assert.True(StreetGeoJson.IsValidBoundary(DidiDigomiCoverage.BoundaryGeoJson));
        using var document = JsonDocument.Parse(DidiDigomiCoverage.BoundaryGeoJson);
        var ring = document.RootElement.GetProperty("coordinates")[0]
            .EnumerateArray()
            .Select(point => (Longitude: point[0].GetDouble(), Latitude: point[1].GetDouble()))
            .ToArray();

        Assert.Equal(ring[0], ring[^1]);
        Assert.Equal(DidiDigomiCoverage.West, ring.Min(point => point.Longitude), 12);
        Assert.Equal(DidiDigomiCoverage.South, ring.Min(point => point.Latitude), 12);
        Assert.Equal(DidiDigomiCoverage.East, ring.Max(point => point.Longitude), 12);
        Assert.Equal(DidiDigomiCoverage.North, ring.Max(point => point.Latitude), 12);
        Assert.True(DidiDigomiCoverage.South < 41.7871);
        Assert.DoesNotContain("18183807", DidiDigomiCoverage.ExternalSourceId);
    }
}
