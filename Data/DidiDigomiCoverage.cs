namespace Website_API.Data;

/// <summary>
/// Reviewed real-estate coverage for Didi Dighomi. OSM relation 18183807
/// contains only the northern neighbourhood core and excludes established
/// Didi Dighomi streets south of that relation, including Asmati Street.
/// </summary>
public static class DidiDigomiCoverage
{
    public const string CanonicalSlug = "didi-digomi";
    public const string NameKa = "დიდი დიღომი";
    public const string ExternalSourceId = "curated:didi-digomi-street-coverage:v1";
    public const string Source = "Curated street coverage";
    public const string BoundaryGeoJson =
        """{"type":"Polygon","coordinates":[[[44.7095,41.7790],[44.7130,41.7910],[44.7272,41.7990],[44.7468,41.8005],[44.7706,41.7960],[44.7720,41.7790],[44.7645,41.7695],[44.7390,41.7680],[44.7190,41.7710],[44.7095,41.7790]]]}""";
}
