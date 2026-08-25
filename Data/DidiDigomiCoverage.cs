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
    public const double West = 44.728431796953245;
    public const double South = 41.765298125190995;
    public const double East = 44.774034254212125;
    public const double North = 41.80395201688612;
    public const string BoundaryGeoJson =
        """{"type":"Polygon","coordinates":[[[44.73045487859298,41.794920992797955],[44.728431796953245,41.78484872110453],[44.728778898731235,41.783711250470155],[44.739893983964855,41.77778704577441],[44.76445849880805,41.765541902738455],[44.76607587241355,41.765298125190995],[44.76745564040083,41.76597312232017],[44.774034254212125,41.78879116861545],[44.76423467235267,41.80351743396423],[44.76281952490795,41.80395201688612],[44.762050349444785,41.80386569460151],[44.73190385818276,41.7962041989619],[44.731218805359696,41.79591988148989],[44.73045487859298,41.794920992797955]]]}""";
}
