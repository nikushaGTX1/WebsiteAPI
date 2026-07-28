namespace Website_API.Models
{
    public class StreetModels
    {
        public int Id { get; set; }
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public List<string> StreetNames { get; set; } = new();

        public string? CityGeorgian =>
            Data.GeorgianLocationTranslations.FindCity(City);

        public string? RegionGeorgian =>
            Data.GeorgianLocationTranslations.FindRegion(Region);

        public string? DistrictGeorgian =>
            Data.GeorgianLocationTranslations.FindDistrict(District);

        public string CityDisplay =>
            CityGeorgian ?? City;

        public string RegionDisplay =>
            RegionGeorgian ?? Region;

        public string DistrictDisplay =>
            DistrictGeorgian ?? District;

        public List<string?> StreetNamesGeorgian =>
            StreetNames
                .Select(Data.GeorgianStreetTranslations.Find)
                .ToList();

        public List<LocalizedStreetName> Streets =>
            StreetNames
                .Select(name => new LocalizedStreetName
                {
                    English = name,
                    Georgian =
                        Data.GeorgianStreetTranslations.Find(name)
                })
                .ToList();
    }

    public class LocalizedStreetName
    {
        public string English { get; set; } = string.Empty;
        public string? Georgian { get; set; }

        public string Display =>
            Georgian ?? English;
    }
}
