using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Website_API.Data;
using Website_API.Models;

namespace Website_API.Services;

/// <summary>
/// Which clients are looking for a home like the ones an owner or agent listed, and the bell
/// alerts that tell them. A client counts when every must-have of one of their saved Velven Match
/// searches is met (the same rule as client alerts). Clients are never identified.
/// </summary>
public class ListingInterestService
{
    private static readonly string[] StaffOrOwnerRoles =
        ["ADMIN", "MANAGER", "AGENT", "UPLOADER", "CRM-MANAGER", "CRM_MANAGER", "OWNER"];

    private readonly AppDbContext _context;
    private readonly ILogger<ListingInterestService> _logger;

    public ListingInterestService(AppDbContext context, ILogger<ListingInterestService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Saved searches of real client accounts (not staff or owners testing the site).</summary>
    private IQueryable<SavedSearch> ClientSearches() => _context.SavedSearches.AsNoTracking()
        .Where(search => _context.UserRoles.Any(ur => ur.UserId == search.UserId &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "CLIENT")) &&
            !_context.UserRoles.Any(ur => ur.UserId == search.UserId &&
                _context.Roles.Any(r => r.Id == ur.RoleId && StaffOrOwnerRoles.Contains(r.NormalizedName!))));

    /// <summary>Requirements an owner may see. Map shapes, names and contact details are left out.</summary>
    private static readonly string[] PublicFields =
    [
        "propertyGoal", "districts", "locationFlexible", "budgetMin", "budgetMax", "currency", "bedrooms",
        "minSize", "adults", "children", "hasPet", "petType", "petTypes", "petCount", "dogSize",
        "moveInDate", "moveInTiming", "rentalDuration", "topPriorities", "householdType", "gender",
    ];

    private static JsonObject PublicProfile(string json)
    {
        var result = new JsonObject();
        try
        {
            if (JsonNode.Parse(json) is not JsonObject source) return result;
            foreach (var field in PublicFields)
                if (source.TryGetPropertyValue(field, out var value) && value is not null)
                    result[field] = value.DeepClone();
        }
        catch (System.Text.Json.JsonException)
        {
            // Unreadable profile: show the card without details.
        }
        return result;
    }

    private static bool Matches(Apartment apartment, SavedSearch search, DateTime today) =>
        SavedSearchProfile.IsValid(search.ProfileJson) &&
        HomeMatchRequirements.Evaluate(apartment, SavedSearchProfile.Parse(search.ProfileJson), today) == RequirementStatus.Exact;

    /// <summary>Adds an unsaved alert for the listing's uploader unless one exists. Caller saves.</summary>
    public async Task AddAlertAsync(Apartment apartment, SavedSearch search, CancellationToken cancellationToken)
    {
        var ownerId = apartment.UploadedByUserId;
        if (string.IsNullOrWhiteSpace(ownerId) || ownerId == search.UserId) return;
        var exists = await _context.ListingInterestNotifications.AnyAsync(n =>
            n.UserId == ownerId && n.ApartmentId == apartment.Id && n.SavedSearchId == search.Id, cancellationToken);
        if (exists || _context.ListingInterestNotifications.Local.Any(n =>
                n.UserId == ownerId && n.ApartmentId == apartment.Id && n.SavedSearchId == search.Id))
            return;
        _context.ListingInterestNotifications.Add(new ListingInterestNotification
        {
            UserId = ownerId,
            ApartmentId = apartment.Id,
            SavedSearchId = search.Id,
        });
    }

    /// <summary>A client saved or changed a search: alert the owners of listings it already matches.</summary>
    public async Task<int> NotifyForSearchAsync(int searchId, CancellationToken cancellationToken)
    {
        var search = await ClientSearches().FirstOrDefaultAsync(item => item.Id == searchId, cancellationToken);
        if (search is null || !SavedSearchProfile.IsValid(search.ProfileJson)) return 0;
        var apartments = await ApartmentCatalogue.Published(_context.Apartments.AsNoTracking())
            .Where(apartment => apartment.UploadedByUserId != null)
            .ToListAsync(cancellationToken);
        var today = DateTime.UtcNow.Date;
        var profile = SavedSearchProfile.Parse(search.ProfileJson);
        foreach (var apartment in apartments)
        {
            if (HomeMatchRequirements.Evaluate(apartment, profile, today) == RequirementStatus.Exact)
                await AddAlertAsync(apartment, search, cancellationToken);
        }
        return await SaveAsync(cancellationToken);
    }

    /// <summary>Saves pending alerts; a parallel run that added the same alert first wins.</summary>
    public async Task<int> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _context.ChangeTracker.Clear();
            return 0;
        }
    }

    /// <summary>The user's published listings, each with the anonymous clients whose search it meets.</summary>
    public async Task<List<ListingInterestView>> InterestsForAsync(string userId, CancellationToken cancellationToken)
    {
        var apartments = await ApartmentCatalogue.Published(_context.Apartments.AsNoTracking())
            .Where(apartment => apartment.UploadedByUserId == userId)
            .OrderByDescending(apartment => apartment.PublishedAt ?? apartment.CreatedAt)
            .ToListAsync(cancellationToken);
        if (apartments.Count == 0) return [];

        var searches = await ClientSearches().Where(search => search.UserId != userId).ToListAsync(cancellationToken);
        var unread = await _context.ListingInterestNotifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .Select(n => new { n.ApartmentId, n.SavedSearchId })
            .ToListAsync(cancellationToken);
        var today = DateTime.UtcNow.Date;

        return apartments.Select(apartment =>
        {
            var clients = searches
                .Where(search => Matches(apartment, search, today))
                // One card per client, even with several matching searches.
                .GroupBy(search => search.UserId)
                .Select(group => group.OrderByDescending(search => search.UpdatedAt).First())
                .OrderByDescending(search => search.UpdatedAt)
                .Select(search => new InterestedClientView
                {
                    SearchId = search.Id,
                    Profile = PublicProfile(search.ProfileJson),
                    SearchingSince = search.CreatedAt,
                    IsNew = unread.Any(n => n.ApartmentId == apartment.Id && n.SavedSearchId == search.Id),
                })
                .ToList();
            return new ListingInterestView
            {
                ApartmentId = apartment.Id,
                Title = apartment.Title,
                TitleEn = apartment.TitleEn,
                TitleKa = apartment.TitleKa,
                TitleRu = apartment.TitleRu,
                District = apartment.District,
                Street = apartment.Street,
                Price = apartment.Price,
                Bedrooms = apartment.Bedrooms,
                SizeSquareMeters = apartment.SizeSquareMeters,
                ImageUrl = apartment.ImageUrl,
                Description = apartment.Description.Length > 600 ? apartment.Description[..600] : apartment.Description,
                Clients = clients,
            };
        }).ToList();
    }
}

public class ListingInterestView
{
    public int ApartmentId { get; set; }
    public string Title { get; set; } = "";
    public string? TitleEn { get; set; }
    public string? TitleKa { get; set; }
    public string? TitleRu { get; set; }
    public string District { get; set; } = "";
    public string Street { get; set; } = "";
    public decimal Price { get; set; }
    public int Bedrooms { get; set; }
    public double SizeSquareMeters { get; set; }
    public string? ImageUrl { get; set; }
    public string Description { get; set; } = "";
    public List<InterestedClientView> Clients { get; set; } = [];
}

/// <summary>Anonymous: only the requirements, never who the client is.</summary>
public class InterestedClientView
{
    public int SearchId { get; set; }
    public JsonObject Profile { get; set; } = new();
    public DateTime SearchingSince { get; set; }
    public bool IsNew { get; set; }
}
