namespace Website_API.Data;

/// <summary>
/// Real-estate coverage for Didi Dighomi derived from SS.ge subdistrict 45
/// street coordinates and the corresponding named-road geometry. It is not
/// the smaller OSM neighbourhood relation.
/// </summary>
public static class DidiDigomiCoverage
{
    public const string CanonicalSlug = "didi-digomi";
    public const string NameKa = "დიდი დიღომი";
    public const string ExternalSourceId = "curated:didi-digomi-real-estate-coverage:v2";
    public const string Source = "SS.ge subdistrict 45 + verified named-road geometry";
    public const double West = 44.7284318;
    public const double South = 41.7652981;
    public const double East = 44.7740343;
    public const double North = 41.8039520;
    public const string BoundaryGeoJson =
        """{"type":"Polygon","coordinates":[[[44.7304549,41.7949210],[44.7284318,41.7848487],[44.7287789,41.7837113],[44.7398940,41.7777870],[44.7644585,41.7655419],[44.7660759,41.7652981],[44.7674556,41.7659731],[44.7740343,41.7887912],[44.7642347,41.8035174],[44.7628195,41.8039520],[44.7620503,41.8038657],[44.7319039,41.7962042],[44.7312188,41.7959199],[44.7304549,41.7949210]]]}""";
}
