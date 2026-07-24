using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Website_API.Models;

namespace Website_API.Services;

public class GoogleNearbyPlacesService
{
    private const string PlacesNearbyUrl =
        "https://places.googleapis.com/v1/places:searchNearby";

    private const string RoutesUrl =
        "https://routes.googleapis.com/directions/v2:computeRoutes";

    private const string GeocodingUrl =
        "https://maps.googleapis.com/maps/api/geocode/json";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GoogleNearbyPlacesService> _logger;

    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    public GoogleNearbyPlacesService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GoogleNearbyPlacesService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = configuration["GoogleMaps:ApiKey"]
            ?? throw new InvalidOperationException(
                "GoogleMaps:ApiKey is not configured."
            );
    }

    public async Task EnrichApartmentAsync(
        Apartment apartment,
        CancellationToken cancellationToken = default)
    {
        if (!apartment.Latitude.HasValue ||
            !apartment.Longitude.HasValue)
        {
            await GeocodeApartmentAsync(
                apartment,
                cancellationToken
            );
        }

        if (!apartment.Latitude.HasValue ||
            !apartment.Longitude.HasValue)
        {
            _logger.LogWarning(
                "Could not determine coordinates for apartment {ApartmentId}.",
                apartment.Id
            );

            return;
        }

        var origin = new Coordinates(
            apartment.Latitude.Value,
            apartment.Longitude.Value
        );

        var schoolTask = FindWalkingMinutesAsync(
            origin, "school", cancellationToken);
        var kindergartenTask = FindWalkingMinutesAsync(
            origin, "preschool", cancellationToken);
        var gymTask = FindWalkingMinutesAsync(
            origin, "gym", cancellationToken);
        var parkTask = FindWalkingMinutesAsync(
            origin, "park", cancellationToken);
        var metroTask = FindWalkingMinutesAsync(
            origin, "subway_station", cancellationToken);
        var universityTask = FindWalkingMinutesAsync(
            origin, "university", cancellationToken);

        await Task.WhenAll(
            schoolTask,
            kindergartenTask,
            gymTask,
            parkTask,
            metroTask,
            universityTask);

        apartment.SchoolDistanceMinutes = await schoolTask;
        apartment.KindergartenDistanceMinutes = await kindergartenTask;
        apartment.GymDistanceMinutes = await gymTask;
        apartment.ParkDistanceMinutes = await parkTask;
        apartment.MetroDistanceMinutes = await metroTask;
        apartment.UniversityDistanceMinutes = await universityTask;

        _logger.LogInformation(
            "Nearby-place information updated for apartment {ApartmentId}. " +
            "School: {SchoolMinutes}, Kindergarten: {KindergartenMinutes}, " +
            "Gym: {GymMinutes}, Park: {ParkMinutes}, Metro: {MetroMinutes}, " +
            "University: {UniversityMinutes}",
            apartment.Id,
            apartment.SchoolDistanceMinutes,
            apartment.KindergartenDistanceMinutes,
            apartment.GymDistanceMinutes,
            apartment.ParkDistanceMinutes,
            apartment.MetroDistanceMinutes,
            apartment.UniversityDistanceMinutes
        );
    }

    private async Task GeocodeApartmentAsync(
        Apartment apartment,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apartment.Address))
        {
            _logger.LogWarning(
                "Apartment {ApartmentId} has no address to geocode.",
                apartment.Id
            );

            return;
        }

        var addressParts = new[]
        {
            apartment.Address,
            apartment.District,
            apartment.City,
            "Georgia"
        };

        var completeAddress = string.Join(
            ", ",
            addressParts.Where(value =>
                !string.IsNullOrWhiteSpace(value)
            )
        );

        var encodedAddress = Uri.EscapeDataString(completeAddress);
        var encodedKey = Uri.EscapeDataString(_apiKey);

        var requestUrl =
            $"{GeocodingUrl}?address={encodedAddress}&key={encodedKey}";

        try
        {
            using var response = await _httpClient.GetAsync(
                requestUrl,
                cancellationToken
            );

            var responseText = await response.Content.ReadAsStringAsync(
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Geocoding API returned {StatusCode} for apartment " +
                    "{ApartmentId}: {Response}",
                    response.StatusCode,
                    apartment.Id,
                    responseText
                );

                return;
            }

            var geocodingResponse =
                JsonSerializer.Deserialize<GeocodingResponse>(
                    responseText,
                    _jsonOptions
                );

            if (geocodingResponse == null ||
                !string.Equals(
                    geocodingResponse.Status,
                    "OK",
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Geocoding failed for apartment {ApartmentId}. " +
                    "Google status: {Status}. Error: {ErrorMessage}",
                    apartment.Id,
                    geocodingResponse?.Status,
                    geocodingResponse?.ErrorMessage
                );

                return;
            }

            var location = geocodingResponse.Results
                .FirstOrDefault()?
                .Geometry
                .Location;

            if (location == null)
            {
                _logger.LogWarning(
                    "Google could not find coordinates for address {Address}.",
                    completeAddress
                );

                return;
            }

            apartment.Latitude = location.Latitude;
            apartment.Longitude = location.Longitude;

            _logger.LogInformation(
                "Geocoded apartment {ApartmentId} to {Latitude}, {Longitude}.",
                apartment.Id,
                apartment.Latitude,
                apartment.Longitude
            );
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Geocoding request failed for apartment {ApartmentId}.",
                apartment.Id
            );
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "Google returned invalid geocoding JSON for apartment " +
                "{ApartmentId}.",
                apartment.Id
            );
        }
    }

    private async Task<int?> FindWalkingMinutesAsync(
        Coordinates origin,
        string placeType,
        CancellationToken cancellationToken)
    {
        try
        {
            var place = await FindNearestPlaceAsync(
                origin,
                placeType,
                cancellationToken
            );

            if (place?.Location == null)
            {
                _logger.LogInformation(
                    "No nearby place found for type {PlaceType}.",
                    placeType
                );

                return null;
            }

            var destination = new Coordinates(
                place.Location.Latitude,
                place.Location.Longitude
            );

            var walkingMinutes = await CalculateWalkingMinutesAsync(
                origin,
                destination,
                cancellationToken
            );

            _logger.LogInformation(
                "Nearest {PlaceType}: {PlaceName}. Walking time: " +
                "{WalkingMinutes} minutes.",
                placeType,
                place.DisplayName?.Text ?? place.FormattedAddress,
                walkingMinutes
            );

            return walkingMinutes;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Nearby-place lookup failed for type {PlaceType}.",
                placeType
            );

            return null;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Google returned invalid JSON for type {PlaceType}.",
                placeType
            );

            return null;
        }
    }

    private async Task<GooglePlace?> FindNearestPlaceAsync(
        Coordinates origin,
        string placeType,
        CancellationToken cancellationToken)
    {
        var body = new NearbySearchRequest
        {
            IncludedPrimaryTypes = [placeType],
            MaxResultCount = 5,
            RankPreference = "DISTANCE",
            LocationRestriction = new LocationRestriction
            {
                Circle = new SearchCircle
                {
                    Center = new GoogleLocation
                    {
                        Latitude = origin.Latitude,
                        Longitude = origin.Longitude
                    },
                    Radius = 5000
                }
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            PlacesNearbyUrl
        );

        request.Headers.Add(
            "X-Goog-Api-Key",
            _apiKey
        );

        request.Headers.Add(
            "X-Goog-FieldMask",
            "places.id," +
            "places.displayName," +
            "places.formattedAddress," +
            "places.location," +
            "places.primaryType"
        );

        request.Content = JsonContent.Create(
            body,
            options: _jsonOptions
        );

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken
        );

        var responseText = await response.Content.ReadAsStringAsync(
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Places API returned {StatusCode} for {PlaceType}: " +
                "{Response}",
                response.StatusCode,
                placeType,
                responseText
            );

            return null;
        }

        var placesResponse =
            JsonSerializer.Deserialize<NearbySearchResponse>(
                responseText,
                _jsonOptions
            );

        return placesResponse?.Places
            .Where(place => place.Location != null)
            .FirstOrDefault();
    }

    private async Task<int?> CalculateWalkingMinutesAsync(
        Coordinates origin,
        Coordinates destination,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            origin = new
            {
                location = new
                {
                    latLng = new
                    {
                        latitude = origin.Latitude,
                        longitude = origin.Longitude
                    }
                }
            },
            destination = new
            {
                location = new
                {
                    latLng = new
                    {
                        latitude = destination.Latitude,
                        longitude = destination.Longitude
                    }
                }
            },
            travelMode = "WALK",
            computeAlternativeRoutes = false,
            languageCode = "en-US",
            units = "METRIC"
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            RoutesUrl
        );

        request.Headers.Add(
            "X-Goog-Api-Key",
            _apiKey
        );

        request.Headers.Add(
            "X-Goog-FieldMask",
            "routes.duration,routes.distanceMeters"
        );

        request.Content = JsonContent.Create(
            body,
            options: _jsonOptions
        );

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken
        );

        var responseText = await response.Content.ReadAsStringAsync(
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Routes API returned {StatusCode}: {Response}",
                response.StatusCode,
                responseText
            );

            return null;
        }

        var routesResponse =
            JsonSerializer.Deserialize<RoutesResponse>(
                responseText,
                _jsonOptions
            );

        var duration = routesResponse?
            .Routes
            .FirstOrDefault()?
            .Duration;

        return ParseDurationMinutes(duration);
    }

    private static int? ParseDurationMinutes(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return null;
        }

        var secondsText = duration.EndsWith(
            "s",
            StringComparison.OrdinalIgnoreCase
        )
            ? duration[..^1]
            : duration;

        if (!double.TryParse(
                secondsText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var seconds))
        {
            return null;
        }

        return Math.Max(
            1,
            (int)Math.Ceiling(seconds / 60)
        );
    }

    private sealed record Coordinates(
        double Latitude,
        double Longitude
    );

    private sealed class NearbySearchRequest
    {
        [JsonPropertyName("includedPrimaryTypes")]
        public List<string> IncludedPrimaryTypes { get; set; } = [];

        [JsonPropertyName("maxResultCount")]
        public int MaxResultCount { get; set; }

        [JsonPropertyName("rankPreference")]
        public string RankPreference { get; set; } = "DISTANCE";

        [JsonPropertyName("locationRestriction")]
        public LocationRestriction LocationRestriction { get; set; } =
            new();
    }

    private sealed class LocationRestriction
    {
        [JsonPropertyName("circle")]
        public SearchCircle Circle { get; set; } = new();
    }

    private sealed class SearchCircle
    {
        [JsonPropertyName("center")]
        public GoogleLocation Center { get; set; } = new();

        [JsonPropertyName("radius")]
        public double Radius { get; set; }
    }

    private sealed class NearbySearchResponse
    {
        [JsonPropertyName("places")]
        public List<GooglePlace> Places { get; set; } = [];
    }

    private sealed class GooglePlace
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public LocalizedText? DisplayName { get; set; }

        [JsonPropertyName("formattedAddress")]
        public string? FormattedAddress { get; set; }

        [JsonPropertyName("primaryType")]
        public string? PrimaryType { get; set; }

        [JsonPropertyName("location")]
        public GoogleLocation? Location { get; set; }
    }

    private sealed class LocalizedText
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("languageCode")]
        public string? LanguageCode { get; set; }
    }

    private sealed class GoogleLocation
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }

    private sealed class GeocodingResponse
    {
        [JsonPropertyName("results")]
        public List<GeocodingResult> Results { get; set; } = [];

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }

    private sealed class GeocodingResult
    {
        [JsonPropertyName("geometry")]
        public GeocodingGeometry Geometry { get; set; } = new();

        [JsonPropertyName("formatted_address")]
        public string? FormattedAddress { get; set; }
    }

    private sealed class GeocodingGeometry
    {
        [JsonPropertyName("location")]
        public GeocodingLocation Location { get; set; } = new();
    }

    private sealed class GeocodingLocation
    {
        [JsonPropertyName("lat")]
        public double Latitude { get; set; }

        [JsonPropertyName("lng")]
        public double Longitude { get; set; }
    }

    private sealed class RoutesResponse
    {
        [JsonPropertyName("routes")]
        public List<GoogleRoute> Routes { get; set; } = [];
    }

    private sealed class GoogleRoute
    {
        [JsonPropertyName("duration")]
        public string? Duration { get; set; }

        [JsonPropertyName("distanceMeters")]
        public int DistanceMeters { get; set; }
    }
}
