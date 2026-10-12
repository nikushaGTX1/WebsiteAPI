using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;

namespace Website_API.Services;

/// <summary>
/// One-time fill of pet shop and vet walking times for listings uploaded before those were
/// measured. Each listing is looked up once (marked in NearbyPlaceNames even when nothing is
/// found), so Google is never asked again for the same listing. Scraped imports are skipped.
/// </summary>
public sealed class PetServicesBackfillService : BackgroundService
{
    public const string CheckedMarker = "petServicesChecked";
    private const int BatchSize = 10;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PetServicesBackfillService> _logger;

    public PetServicesBackfillService(IServiceScopeFactory scopes, ILogger<PetServicesBackfillService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (migrations, seeding) finish first.
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        var total = 0;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var updated = await RunBatchAsync(stoppingToken);
                if (updated < 0) break;
                total += updated;
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Pet services backfill stopped.");
        }
        if (total > 0) _logger.LogInformation("Pet services backfill measured {Count} listing(s).", total);
    }

    /// <summary>Measures one batch; returns -1 when nothing is left (or Google is not configured).</summary>
    private async Task<int> RunBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        GoogleNearbyPlacesService places;
        try
        {
            places = scope.ServiceProvider.GetRequiredService<GoogleNearbyPlacesService>();
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("Pet services backfill skipped: GoogleMaps:ApiKey is not configured.");
            return -1;
        }

        var marker = $"%\"{CheckedMarker}\"%";
        var apartments = await context.Apartments
            .Where(a =>
                (a.PropertyLatitude != null || a.Latitude != null) &&
                a.PetStoreDistanceMinutes == null &&
                a.VeterinaryDistanceMinutes == null &&
                (a.NearbyPlaceNames == null || !EF.Functions.ILike(a.NearbyPlaceNames, marker)) &&
                !EF.Functions.ILike(a.Description, "%Source: https://www.myhome.ge/%") &&
                !EF.Functions.ILike(a.Description, "%Source: https://home.ss.ge/%"))
            .OrderByDescending(a => a.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
        if (apartments.Count == 0) return -1;

        var measured = 0;
        foreach (var apartment in apartments)
        {
            var latitude = (double)(apartment.PropertyLatitude ?? apartment.Latitude)!;
            var longitude = (double)(apartment.PropertyLongitude ?? apartment.Longitude)!;
            var petStore = await places.FindNearestWalkAsync(latitude, longitude, "pet_store", cancellationToken);
            var vet = await places.FindNearestWalkAsync(latitude, longitude, "veterinary_care", cancellationToken);
            // Nothing at all usually means Google refused the request (key not allowed for
            // Places API (New) / Routes API); leave the listing unmarked so it is retried.
            if (petStore is null && vet is null) continue;
            measured++;

            var names = NearbyPlaceNamesJson.Read(apartment.NearbyPlaceNames);
            if (petStore is not null)
            {
                apartment.PetStoreDistanceMinutes = petStore.Minutes;
                if (!string.IsNullOrWhiteSpace(petStore.Name)) names["petStoreDistanceMinutes"] = petStore.Name;
            }
            if (vet is not null)
            {
                apartment.VeterinaryDistanceMinutes = vet.Minutes;
                if (!string.IsNullOrWhiteSpace(vet.Name)) names["veterinaryDistanceMinutes"] = vet.Name;
            }
            names[CheckedMarker] = "1";
            apartment.NearbyPlaceNames = NearbyPlaceNamesJson.Write(names);
        }

        if (measured == 0)
        {
            // A whole batch without a single result: stop instead of retrying the same listings.
            _logger.LogWarning(
                "Pet services backfill stopped: Google returned no pet shop or vet for {Count} listing(s). " +
                "Check that GoogleMaps:ApiKey allows Places API (New) and Routes API.", apartments.Count);
            return -1;
        }
        await context.SaveChangesAsync(cancellationToken);
        await scope.ServiceProvider.GetRequiredService<IOutputCacheStore>().EvictByTagAsync("apartments", cancellationToken);
        return measured;
    }
}
