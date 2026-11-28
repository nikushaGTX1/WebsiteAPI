using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Models;

namespace Website_API.Services;

/// <summary>
/// Listings whose walking routes must be (re)calculated: added when a listing is created or
/// its location changes, and by an administrator's retry. Nothing else adds to it, so
/// Velven Match and page views never cause Google requests.
/// </summary>
public sealed class WalkingRouteQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(int apartmentId) => _channel.Writer.TryWrite(apartmentId);

    public ChannelReader<int> Reader => _channel.Reader;
}

/// <summary>Works through <see cref="WalkingRouteQueue"/> one listing at a time.</summary>
public sealed class WalkingRouteWorker : BackgroundService
{
    private readonly WalkingRouteQueue _queue;
    private readonly ApartmentMatchQueue _matches;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<WalkingRouteWorker> _logger;

    public WalkingRouteWorker(WalkingRouteQueue queue, ApartmentMatchQueue matches, IServiceScopeFactory scopes, ILogger<WalkingRouteWorker> logger)
    {
        _queue = queue;
        _matches = matches;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var apartmentId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ApartmentWalkingRouteService>();
                await service.CalculateAsync(apartmentId, stoppingToken);
                // Saved Velven Match searches are re-checked with the new walking times.
                _matches.Enqueue(apartmentId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // The listing keeps its "Pending"/"Failed" status; an administrator can retry.
                _logger.LogError(exception, "Walking routes failed for apartment {ApartmentId}.", apartmentId);
            }
        }
    }
}

/// <summary>
/// Calculates and saves the walking routes of one listing (Google Places to find the nearest
/// places, one Google Routes matrix request for the walks). No straight-line estimates: a
/// destination Google cannot route to is saved as NoRoute, and a failed request as Failed.
/// </summary>
public sealed class ApartmentWalkingRouteService
{
    private const string PlacesNearbyUrl = "https://places.googleapis.com/v1/places:searchNearby";
    private const string RouteMatrixUrl = "https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix";
    private const string GeocodingUrl = "https://maps.googleapis.com/maps/api/geocode/json";

    /// <summary>Walks longer than this also get a driving time (shown on out-of-town homes).</summary>
    private const int MaxWalkMinutes = 20;

    /// <summary>Freedom Square, used for the "Close to the city centre" priority.</summary>
    public const double CityCenterLatitude = 41.6934, CityCenterLongitude = 44.8015;

    private readonly AppDbContext _context;
    private readonly HttpClient _http;
    private readonly ILogger<ApartmentWalkingRouteService> _logger;
    private readonly string? _apiKey;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ApartmentWalkingRouteService(AppDbContext context, HttpClient http, IConfiguration configuration, ILogger<ApartmentWalkingRouteService> logger)
    {
        _context = context;
        _http = http;
        _logger = logger;
        _apiKey = configuration["GoogleMaps:ApiKey"];
    }

    /// <summary>Nearest-place categories, keyed by the Apartment minutes field they fill.</summary>
    private static readonly (string Key, string Category, string[] Types)[] Categories =
    [
        ("metroDistanceMinutes", "metro", ["subway_station"]),
        ("groceryDistanceMinutes", "supermarket", ["supermarket"]),
        ("pharmacyDistanceMinutes", "pharmacy", ["pharmacy"]),
        ("gymDistanceMinutes", "gym", ["gym", "fitness_center"]),
        ("parkDistanceMinutes", "park", ["park"]),
        ("cafeDistanceMinutes", "cafe", ["cafe"]),
        ("schoolDistanceMinutes", "school", ["school"]),
        ("kindergartenDistanceMinutes", "kindergarten", ["preschool"]),
        ("evChargerDistanceMinutes", "ev_charger", ["electric_vehicle_charging_station"]),
        ("petStoreDistanceMinutes", "pet_store", ["pet_store"]),
        ("veterinaryDistanceMinutes", "veterinary", ["veterinary_care"]),
    ];

    /// <summary>The location a listing is measured from: its map pin, else its address.</summary>
    public static string? OriginKey(Apartment apartment)
    {
        var lat = apartment.PropertyLatitude ?? apartment.Latitude;
        var lng = apartment.PropertyLongitude ?? apartment.Longitude;
        if (lat is double la && lng is double ln)
            return string.Create(CultureInfo.InvariantCulture, $"{la:0.000000},{ln:0.000000}");
        var address = string.Join(", ", new[] { apartment.Street, apartment.BuildingNumber, apartment.Address, apartment.District, apartment.City }
            .Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
        return address.Length == 0 ? null : "address:" + (address.Length > 290 ? address[..290] : address).ToLowerInvariant();
    }

    /// <summary>
    /// Marks a listing for (re)calculation when it is new or its location changed. Returns true
    /// when the caller should enqueue it. Old minutes are cleared: they belong to the old place.
    /// </summary>
    /// <param name="previousOrigin">Edits: <see cref="OriginKey"/> before the edit was applied. New listings: null.</param>
    public static bool MarkIfLocationChanged(Apartment apartment, string? previousOrigin = null)
    {
        var key = OriginKey(apartment);
        var isNew = apartment.Id == 0;
        if (!isNew && key == previousOrigin)
        {
            // Price, text, photos, rooms...: same place, so the saved walks stay as they are.
            // Listings measured before routes were stored just remember where they are now.
            apartment.WalkingRoutesOriginKey ??= key;
            return false;
        }
        apartment.WalkingRoutesOriginKey = key;
        apartment.WalkingRoutesStatus = WalkingRouteStatus.Pending;
        apartment.WalkingRoutesError = null;
        ClearSavedMinutes(apartment);
        return true;
    }

    private static void ClearSavedMinutes(Apartment apartment)
    {
        apartment.MetroDistanceMinutes = null;
        apartment.GroceryDistanceMinutes = null;
        apartment.PharmacyDistanceMinutes = null;
        apartment.GymDistanceMinutes = null;
        apartment.ParkDistanceMinutes = null;
        apartment.CafeDistanceMinutes = null;
        apartment.SchoolDistanceMinutes = null;
        apartment.KindergartenDistanceMinutes = null;
        apartment.EvChargerDistanceMinutes = null;
        apartment.PetStoreDistanceMinutes = null;
        apartment.VeterinaryDistanceMinutes = null;
        apartment.UniversityDistanceMinutes = null;
        apartment.NearbyPlaceNames = null;
        apartment.DestinationWalkingMinutes = null;
    }

    public async Task CalculateAsync(int apartmentId, CancellationToken cancellationToken)
    {
        var apartment = await _context.Apartments.FirstOrDefaultAsync(item => item.Id == apartmentId, cancellationToken);
        if (apartment is null) return; // deleted meanwhile; its rows went with it

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            await FinishAsync(apartment, WalkingRouteStatus.Failed, "GoogleMaps:ApiKey is not configured.", cancellationToken);
            return;
        }

        var lat = apartment.PropertyLatitude ?? apartment.Latitude;
        var lng = apartment.PropertyLongitude ?? apartment.Longitude;
        if (lat is null || lng is null)
        {
            // Pinless listing: find it from its address once, here at upload time.
            var point = await GeocodeAsync(apartment, cancellationToken);
            if (point is null)
            {
                await FinishAsync(apartment, WalkingRouteStatus.NoLocation, "No map pin and the address could not be found.", cancellationToken);
                return;
            }
            // Not written to the listing: its saved origin stays the address, so a later
            // address edit is still seen as a location change.
            (lat, lng) = point.Value;
        }
        var origin = new Point(lat.Value, lng.Value);
        var now = DateTime.UtcNow;
        var failures = new List<string>();

        // 1. Destinations: the nearest place of each category, every listed university, the centre.
        var destinations = new List<Destination>();
        var notFound = new List<(string Key, string Category)>();
        foreach (var (key, category, types) in Categories)
        {
            try
            {
                var place = await FindNearestAsync(origin, types, category, cancellationToken);
                if (place is null) notFound.Add((key, category));
                else destinations.Add(new Destination(key, category, place.Value.Name, place.Value.Location));
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                failures.Add($"{category}: {exception.Message}");
                notFound.Add((key, category));
            }
        }
        foreach (var university in UniversityDestinations.All)
            destinations.Add(new Destination("university:" + university.Id, "university", university.Name, new Point(university.Latitude, university.Longitude)));
        destinations.Add(new Destination("city_center", "city_center", "Freedom Square", new Point(CityCenterLatitude, CityCenterLongitude)));

        // 2. One matrix request for every walk.
        Dictionary<int, MatrixCell>? walks;
        try
        {
            walks = await RouteMatrixAsync(origin, destinations.Select(d => d.Location).ToList(), "WALK", cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            walks = null;
            failures.Add("routes: " + exception.Message);
        }
        if (walks is null)
        {
            // Nothing is invented: the listing stays without walking times until a retry works.
            await FinishAsync(apartment, WalkingRouteStatus.Failed, Trim(string.Join("; ", failures.DefaultIfEmpty("Google Routes returned no result."))), cancellationToken);
            return;
        }

        // 3. Driving times for long walks to nearby-place categories (houses: every category).
        var house = Regex.IsMatch(apartment.Description ?? "", @"Type:\s*(Private house|Country house)", RegexOptions.IgnoreCase);
        var driveIndexes = destinations
            .Select((destination, index) => (destination, index))
            .Where(item => item.destination.Category is not ("university" or "city_center"))
            .Where(item => house || !walks.TryGetValue(item.index, out var walk) || walk.Minutes is null || walk.Minutes > MaxWalkMinutes)
            .Select(item => item.index)
            .ToList();
        var drives = new Dictionary<int, int>();
        if (driveIndexes.Count > 0)
        {
            try
            {
                var driveCells = await RouteMatrixAsync(origin, driveIndexes.Select(i => destinations[i].Location).ToList(), "DRIVE", cancellationToken);
                if (driveCells is not null)
                    foreach (var (position, cell) in driveCells)
                        if (cell.Minutes is int minutes) drives[driveIndexes[position]] = minutes;
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                failures.Add("driving: " + exception.Message);
            }
        }

        // 4. Save: one row per destination, updated in place.
        var existing = await _context.ApartmentWalkingRoutes.Where(r => r.ApartmentId == apartment.Id).ToListAsync(cancellationToken);
        var byKey = existing.ToDictionary(r => r.DestinationKey, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        ApartmentWalkingRoute Row(string key, string category)
        {
            seen.Add(key);
            if (!byKey.TryGetValue(key, out var row))
            {
                row = new ApartmentWalkingRoute { ApartmentId = apartment.Id, DestinationKey = key };
                _context.ApartmentWalkingRoutes.Add(row);
                byKey[key] = row;
            }
            row.Category = category;
            row.CalculatedAt = now;
            return row;
        }

        for (var index = 0; index < destinations.Count; index++)
        {
            var destination = destinations[index];
            var row = Row(destination.Key, destination.Category);
            row.DestinationName = Trim(destination.Name, 200);
            row.DestinationLatitude = destination.Location.Latitude;
            row.DestinationLongitude = destination.Location.Longitude;
            walks.TryGetValue(index, out var walk);
            row.DurationSeconds = walk?.Seconds;
            row.DurationMinutes = walk?.Minutes;
            row.DistanceMeters = walk?.Meters;
            row.DrivingMinutes = drives.TryGetValue(index, out var drive) ? drive : null;
            row.Status = walk?.Minutes is not null ? WalkingRouteStatus.Completed : WalkingRouteStatus.NoRoute;
        }
        foreach (var (key, category) in notFound)
        {
            var row = Row(key, category);
            row.DestinationName = null;
            row.DestinationLatitude = row.DestinationLongitude = null;
            row.DurationSeconds = row.DurationMinutes = row.DistanceMeters = row.DrivingMinutes = null;
            row.Status = failures.Any(f => f.StartsWith(category + ":", StringComparison.Ordinal)) ? WalkingRouteStatus.Failed : WalkingRouteStatus.NotFound;
        }
        // Destinations no longer measured (e.g. a university removed from the list).
        _context.ApartmentWalkingRoutes.RemoveRange(existing.Where(r => !seen.Contains(r.DestinationKey)));

        ApplyToListing(apartment, byKey.Values.Where(r => seen.Contains(r.DestinationKey)));
        await FinishAsync(apartment, failures.Count == 0 ? WalkingRouteStatus.Completed : WalkingRouteStatus.Partial,
            failures.Count == 0 ? null : Trim(string.Join("; ", failures)), cancellationToken);
    }

    /// <summary>Copies the saved routes to the listing's minutes fields, place names and Match cache.</summary>
    public static void ApplyToListing(Apartment apartment, IEnumerable<ApartmentWalkingRoute> routes)
    {
        var list = routes.ToList();
        var names = new Dictionary<string, string>();
        var fixedMinutes = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int? Minutes(string key)
        {
            var row = list.FirstOrDefault(r => r.DestinationKey == key && r.Status == WalkingRouteStatus.Completed);
            if (row is null) return null;
            if (!string.IsNullOrWhiteSpace(row.DestinationName)) names[key] = row.DestinationName!;
            if (row.DrivingMinutes is int drive) names["drive:" + key] = drive.ToString(CultureInfo.InvariantCulture);
            return row.DurationMinutes;
        }
        apartment.MetroDistanceMinutes = Minutes("metroDistanceMinutes");
        apartment.GroceryDistanceMinutes = Minutes("groceryDistanceMinutes");
        apartment.PharmacyDistanceMinutes = Minutes("pharmacyDistanceMinutes");
        apartment.GymDistanceMinutes = Minutes("gymDistanceMinutes");
        apartment.ParkDistanceMinutes = Minutes("parkDistanceMinutes");
        apartment.CafeDistanceMinutes = Minutes("cafeDistanceMinutes");
        apartment.SchoolDistanceMinutes = Minutes("schoolDistanceMinutes");
        apartment.KindergartenDistanceMinutes = Minutes("kindergartenDistanceMinutes");
        apartment.EvChargerDistanceMinutes = Minutes("evChargerDistanceMinutes");
        apartment.PetStoreDistanceMinutes = Minutes("petStoreDistanceMinutes");
        apartment.VeterinaryDistanceMinutes = Minutes("veterinaryDistanceMinutes");

        foreach (var row in list.Where(r => r.Status == WalkingRouteStatus.Completed && r.DurationMinutes is not null))
            if (row.Category is "university" or "city_center") fixedMinutes[row.DestinationKey] = row.DurationMinutes!.Value;

        // "Nearest university": the closest walk among the listed universities.
        var nearestUniversity = list
            .Where(r => r.Category == "university" && r.Status == WalkingRouteStatus.Completed && r.DurationMinutes is not null)
            .OrderBy(r => r.DurationSeconds ?? int.MaxValue)
            .FirstOrDefault();
        apartment.UniversityDistanceMinutes = nearestUniversity?.DurationMinutes;
        if (nearestUniversity?.DestinationName is string universityName) names["universityDistanceMinutes"] = universityName;

        apartment.NearbyPlaceNames = NearbyPlaceNamesJson.Write(names);
        apartment.DestinationWalkingMinutes = fixedMinutes.Count == 0 ? null : JsonSerializer.Serialize(fixedMinutes);
    }

    private async Task FinishAsync(Apartment apartment, string status, string? error, CancellationToken cancellationToken)
    {
        apartment.WalkingRoutesStatus = status;
        apartment.WalkingRoutesError = error;
        apartment.WalkingRoutesCalculatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Walking routes for apartment {ApartmentId}: {Status} {Error}", apartment.Id, status, error);
    }

    private static string Trim(string? text, int max = 500) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text[..max];

    // ---------- Google requests ----------

    private async Task<(double, double)?> GeocodeAsync(Apartment apartment, CancellationToken cancellationToken)
    {
        var parts = new[] { apartment.Street, apartment.BuildingNumber, apartment.Address, apartment.District, apartment.City ?? "Tbilisi", "Georgia" }
            .Where(part => !string.IsNullOrWhiteSpace(part)).Distinct(StringComparer.OrdinalIgnoreCase);
        var address = string.Join(", ", parts);
        if (address.Length == 0) return null;
        // Tbilisi listings: look only inside Tbilisi ("Chitadze 9" also exists near Chiatura).
        var url = $"{GeocodingUrl}?address={Uri.EscapeDataString(address)}&region=ge&components=country:GE" +
            $"&bounds=41.6,44.6|41.86,45.05&key={Uri.EscapeDataString(_apiKey!)}";
        using var response = await _http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (document.RootElement.GetProperty("status").GetString() != "OK") return null;
        var location = document.RootElement.GetProperty("results")[0].GetProperty("geometry").GetProperty("location");
        var lat = location.GetProperty("lat").GetDouble();
        var lng = location.GetProperty("lng").GetDouble();
        var tbilisi = string.IsNullOrWhiteSpace(apartment.City) || Regex.IsMatch(apartment.City, "tbilisi|თბილისი", RegexOptions.IgnoreCase);
        if (tbilisi && (lat < 41.6 || lat > 41.86 || lng < 44.6 || lng > 45.05)) return null;
        return (lat, lng);
    }

    private static readonly Regex NotARealPark = new(
        @"view\s*-?\s*point|viewpoint|panoram|junction|triangle|roundabout|crossroad|intersection|parking|" +
        @"გადასახედ|სანახავი წერტილ|გზაჯვარედ|სამკუთხედ|წრიული|ავტოსადგომ|смотров|перекр[её]ст|развязк|треугольн|парковк",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private async Task<(string? Name, Point Location)?> FindNearestAsync(Point origin, string[] types, string category, CancellationToken cancellationToken)
    {
        foreach (var radius in new[] { 5000.0, 20000.0 })
        {
            var cafe = category == "cafe";
            var body = new
            {
                includedPrimaryTypes = types,
                maxResultCount = 20,
                rankPreference = "DISTANCE",
                locationRestriction = new { circle = new { center = new { latitude = origin.Latitude, longitude = origin.Longitude }, radius } },
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, PlacesNearbyUrl) { Content = JsonContent.Create(body, options: Json) };
            request.Headers.Add("X-Goog-Api-Key", _apiKey);
            request.Headers.Add("X-Goog-FieldMask", "places.displayName,places.location,places.rating,places.userRatingCount");
            using var response = await _http.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Places {(int)response.StatusCode}");
            var places = JsonSerializer.Deserialize<PlacesResponse>(text, Json)?.Places ?? [];
            var place = places
                .Where(p => p.Location is not null)
                // Google also types viewpoints and road junctions as "park".
                .Where(p => category != "park" || !NotARealPark.IsMatch(p.DisplayName?.Text ?? ""))
                // Cafés: the nearest well-reviewed one.
                .FirstOrDefault(p => !cafe || (p.Rating ?? 0) >= 4.7 && (p.UserRatingCount ?? 0) >= 100);
            if (place?.Location is { } location)
                return (place.DisplayName?.Text, new Point(location.Latitude, location.Longitude));
        }
        return null;
    }

    /// <summary>Walking or driving routes from one origin to many destinations, keyed by destination index.</summary>
    private async Task<Dictionary<int, MatrixCell>?> RouteMatrixAsync(Point origin, List<Point> destinations, string mode, CancellationToken cancellationToken)
    {
        object Waypoint(Point point) => new { waypoint = new { location = new { latLng = new { latitude = point.Latitude, longitude = point.Longitude } } } };
        var body = new
        {
            origins = new[] { Waypoint(origin) },
            destinations = destinations.Select(Waypoint).ToArray(),
            travelMode = mode,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, RouteMatrixUrl) { Content = JsonContent.Create(body, options: Json) };
        request.Headers.Add("X-Goog-Api-Key", _apiKey);
        request.Headers.Add("X-Goog-FieldMask", "originIndex,destinationIndex,status,condition,distanceMeters,duration");
        using var response = await _http.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Route matrix {Mode} returned {Status}: {Body}", mode, (int)response.StatusCode, Trim(text, 300));
            throw new HttpRequestException($"Routes {(int)response.StatusCode}");
        }
        var cells = JsonSerializer.Deserialize<List<MatrixElement>>(text, Json) ?? [];
        var result = new Dictionary<int, MatrixCell>();
        foreach (var cell in cells)
        {
            if (cell.DestinationIndex is not int index) continue;
            var ok = (cell.Status?.Code ?? 0) == 0 && cell.Condition == "ROUTE_EXISTS";
            var seconds = ok ? ParseSeconds(cell.Duration) : null;
            result[index] = new MatrixCell(
                seconds,
                seconds is int s ? Math.Max(1, (int)Math.Round(s / 60.0)) : null,
                ok ? cell.DistanceMeters : null);
        }
        return result;
    }

    private static int? ParseSeconds(string? duration) =>
        duration is not null && double.TryParse(duration.TrimEnd('s'), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? (int)Math.Round(seconds)
            : null;

    private readonly record struct Point(double Latitude, double Longitude);
    private sealed record Destination(string Key, string Category, string? Name, Point Location);
    private sealed record MatrixCell(int? Seconds, int? Minutes, int? Meters);

    private sealed class PlacesResponse
    {
        public List<PlaceItem> Places { get; set; } = [];
    }

    private sealed class PlaceItem
    {
        public LocalizedText? DisplayName { get; set; }
        public LatLng? Location { get; set; }
        public double? Rating { get; set; }
        public int? UserRatingCount { get; set; }
    }

    private sealed class LocalizedText
    {
        public string? Text { get; set; }
    }

    private sealed class LatLng
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private sealed class MatrixElement
    {
        public int? OriginIndex { get; set; }
        public int? DestinationIndex { get; set; }
        public MatrixStatus? Status { get; set; }
        public string? Condition { get; set; }
        public int? DistanceMeters { get; set; }
        public string? Duration { get; set; }
    }

    private sealed class MatrixStatus
    {
        [JsonPropertyName("code")]
        public int? Code { get; set; }
    }
}

/// <summary>
/// Universities a student can choose in Velven Match (same ids as the website's list), with
/// their main campus checked on Google Maps. Every listing gets a saved walk to each of them.
/// </summary>
public static class UniversityDestinations
{
    public sealed record University(string Id, string Name, double Latitude, double Longitude);

    public static readonly University[] All =
    [
        new("tsu", "Ivane Javakhishvili Tbilisi State University", 41.70998, 44.778),
        new("iliauni", "Ilia State University", 41.71185, 44.74863),
        new("gtu", "Georgian Technical University", 41.72397, 44.77765),
        new("tsmu", "Tbilisi State Medical University", 41.72675, 44.75478),
        new("freeuni", "Free University of Tbilisi", 41.80572, 44.76791),
        new("agruni", "Agricultural University of Georgia", 41.80627, 44.76851),
        new("cu", "Caucasus University", 41.70694, 44.81107),
        new("ug", "University of Georgia", 41.72297, 44.77974),
        new("btu", "Business and Technology University", 41.70657, 44.73867),
        new("ibsu", "International Black Sea University", 41.79822, 44.77281),
        new("gipa", "Georgian Institute of Public Affairs", 41.65472, 44.90189),
        new("gau", "Georgian American University", 41.71779, 44.78491),
        new("ciu", "Caucasus International University", 41.76998, 44.80172),
        new("seu", "Georgian National University SEU", 41.69, 44.82792),
        new("alte", "Alte University", 41.71912, 44.74152),
        new("eu", "European University", 41.79567, 44.80163),
        new("robakidze", "Grigol Robakidze University", 41.79364, 44.77253),
        new("tvildiani", "Tvildiani Medical University", 41.77314, 44.77632),
        new("nvu", "New Vision University", 41.78041, 44.77757),
        new("eeu", "East European University", 41.77958, 44.79923),
        new("sabauni", "Sulkhan-Saba Orbeliani University", 41.72277, 44.74343),
        new("sangu", "St. Andrew the First-Called Georgian University", 41.71092, 44.74143),
        new("sokhumi", "Sokhumi State University", 41.72057, 44.70883),
        new("tafu", "Shota Rustaveli Theatre and Film University", 41.69994, 44.797),
        new("arts", "Tbilisi State Academy of Arts", 41.70144, 44.79291),
        new("conservatoire", "Tbilisi State Conservatoire", 41.69952, 44.79528),
        new("aviation", "Georgian Aviation University", 41.69062, 44.81848),
        new("geomedi", "Geomedi University", 41.67667, 44.82272),
        new("giu", "International University of Georgia", 41.7326, 44.82168),
        new("aghmashenebeli", "David Aghmashenebeli University", 41.71037, 44.7671),
    ];
}
