using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Website_API.Data;
using Website_API.Models;

namespace Website_API.Services;

/// <summary>
/// Client searches that match published listings. Each match first goes to a Velven agent, who
/// checks the client and marks it ready; only then is the owner told (bell and/or email), with the
/// client's requirements and the agent's phone. The client's own contact details stay in the CRM.
/// A client counts when every must-have of a saved Velven Match search is met.
/// </summary>
public class ListingInterestService
{
    private static readonly string[] StaffOrOwnerRoles =
        ["ADMIN", "MANAGER", "AGENT", "UPLOADER", "CRM-MANAGER", "CRM_MANAGER", "OWNER"];
    private static readonly string[] StaffRoles =
        ["ADMIN", "MANAGER", "AGENT", "UPLOADER", "CRM-MANAGER", "CRM_MANAGER"];

    private readonly AppDbContext _context;
    private readonly ILogger<ListingInterestService> _logger;
    private readonly ResendEmailService? _email;
    private readonly IConfiguration? _configuration;

    public ListingInterestService(
        AppDbContext context,
        ILogger<ListingInterestService> logger,
        ResendEmailService? email = null,
        IConfiguration? configuration = null)
    {
        _context = context;
        _logger = logger;
        _email = email;
        _configuration = configuration;
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

    public static JsonObject PublicProfile(string json)
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

    private async Task<bool> HasRoleAsync(string userId, string[] roles, CancellationToken cancellationToken) =>
        await _context.UserRoles.AnyAsync(ur => ur.UserId == userId &&
            _context.Roles.Any(r => r.Id == ur.RoleId && roles.Contains(r.NormalizedName!)), cancellationToken);

    /// <summary>Adds an unsaved pending match for an agent to review unless one exists. Caller saves.</summary>
    public async Task AddAlertAsync(Apartment apartment, SavedSearch search, CancellationToken cancellationToken)
    {
        var listedBy = apartment.UploadedByUserId;
        if (string.IsNullOrWhiteSpace(listedBy) || listedBy == search.UserId) return;
        var exists = await _context.ListingInterestNotifications.AnyAsync(n =>
            n.UserId == listedBy && n.ApartmentId == apartment.Id && n.SavedSearchId == search.Id, cancellationToken);
        if (exists || _context.ListingInterestNotifications.Local.Any(n =>
                n.UserId == listedBy && n.ApartmentId == apartment.Id && n.SavedSearchId == search.Id))
            return;

        // Listed by an agent from an owner link: that agent reviews, the owner is the link's owner.
        // Listed by an owner account: the owner is that account and a manager reviews.
        var submission = await _context.OwnerSubmissions.AsNoTracking()
            .Where(item => item.PublishedApartmentId == apartment.Id)
            .Select(item => new { item.AgentUserId, item.OwnerEmail })
            .FirstOrDefaultAsync(cancellationToken);
        var listedByStaff = await HasRoleAsync(listedBy, StaffRoles, cancellationToken);
        string? reviewer = listedByStaff ? listedBy : submission?.AgentUserId;
        string? owner = listedByStaff ? null : listedBy;
        var ownerEmail = submission?.OwnerEmail?.Trim();
        if (owner is null && !string.IsNullOrWhiteSpace(ownerEmail))
        {
            var normalized = ownerEmail.ToUpperInvariant();
            owner = await _context.Users.Where(user => user.NormalizedEmail == normalized)
                .Select(user => user.Id).FirstOrDefaultAsync(cancellationToken);
        }
        if (owner is not null && string.IsNullOrWhiteSpace(ownerEmail))
            ownerEmail = await _context.Users.Where(user => user.Id == owner).Select(user => user.Email).FirstOrDefaultAsync(cancellationToken);

        _context.ListingInterestNotifications.Add(new ListingInterestNotification
        {
            UserId = listedBy,
            ApartmentId = apartment.Id,
            SavedSearchId = search.Id,
            Status = ListingInterestNotification.Pending,
            ReviewerUserId = reviewer,
            OwnerUserId = owner,
            OwnerEmail = string.IsNullOrWhiteSpace(ownerEmail) ? null : ownerEmail,
        });
    }

    /// <summary>A client saved or changed a search: queue matches with listings it already meets.</summary>
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

    /// <summary>Saves pending matches; a parallel run that added the same one first wins.</summary>
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

    // ---------- Agents: review ----------

    /// <summary>Matches an agent reviews: their own, plus unassigned ones for managers.</summary>
    public IQueryable<ListingInterestNotification> ReviewQueue(string userId, bool isManager) =>
        _context.ListingInterestNotifications.Where(n =>
            n.Apartment.IsApproved &&
            (isManager || n.ReviewerUserId == userId));

    public async Task<List<ReviewView>> ReviewsAsync(string userId, bool isManager, string status, CancellationToken cancellationToken)
    {
        var rows = await ReviewQueue(userId, isManager).AsNoTracking()
            .Where(n => n.Status == status)
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)
            .Select(n => new
            {
                n.Id, n.Status, n.CreatedAt, n.ReviewedAt, n.ReadAt, n.OwnerUserId, n.OwnerEmail, n.ReviewerUserId,
                ReviewerName = n.Reviewer != null ? n.Reviewer.FullName ?? n.Reviewer.UserName : null,
                Search = new { n.SavedSearch.Id, n.SavedSearch.Name, n.SavedSearch.ProfileJson, n.SavedSearch.CreatedAt },
                Client = new { n.SavedSearch.User.FullName, n.SavedSearch.User.Email, n.SavedSearch.User.PhoneNumber },
                Apartment = new
                {
                    n.Apartment.Id, n.Apartment.Title, n.Apartment.TitleEn, n.Apartment.TitleKa, n.Apartment.TitleRu,
                    n.Apartment.District, n.Apartment.Street, n.Apartment.Price, n.Apartment.Bedrooms,
                    n.Apartment.ImageUrl, n.Apartment.Description, n.Apartment.OwnerName, n.Apartment.OwnerPhoneNumber,
                },
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new ReviewView
        {
            Id = row.Id,
            Status = row.Status,
            CreatedAt = row.CreatedAt,
            ReviewedAt = row.ReviewedAt,
            IsNew = row.ReadAt == null,
            ReviewerName = row.ReviewerName,
            SearchId = row.Search.Id,
            SearchName = row.Search.Name,
            SearchingSince = row.Search.CreatedAt,
            Profile = PublicProfile(row.Search.ProfileJson),
            ClientName = row.Client.FullName,
            ClientEmail = row.Client.Email,
            ClientPhone = row.Client.PhoneNumber,
            ApartmentId = row.Apartment.Id,
            Title = row.Apartment.Title,
            TitleEn = row.Apartment.TitleEn,
            TitleKa = row.Apartment.TitleKa,
            TitleRu = row.Apartment.TitleRu,
            District = row.Apartment.District,
            Street = row.Apartment.Street,
            Price = row.Apartment.Price,
            Bedrooms = row.Apartment.Bedrooms,
            ImageUrl = row.Apartment.ImageUrl,
            Description = row.Apartment.Description.Length > 600 ? row.Apartment.Description[..600] : row.Apartment.Description,
            OwnerName = row.Apartment.OwnerName,
            OwnerPhone = row.Apartment.OwnerPhoneNumber,
            OwnerEmail = row.OwnerEmail,
            OwnerHasAccount = row.OwnerUserId != null,
        }).ToList();
    }

    /// <summary>
    /// The agent marks the client ready (owner is told) or not a fit. Returns null when the match
    /// is not theirs to review, otherwise whether the owner could be reached.
    /// </summary>
    public async Task<ReviewResult?> ReviewAsync(int id, string userId, bool isManager, bool ready, CancellationToken cancellationToken)
    {
        var match = await ReviewQueue(userId, isManager)
            .Include(n => n.Apartment)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (match is null) return null;
        if (match.Status != ListingInterestNotification.Pending)
            return new ReviewResult(match.Status, match.OwnerUserId != null, match.OwnerEmailedAt != null);

        match.Status = ready ? ListingInterestNotification.Ready : ListingInterestNotification.Dismissed;
        match.ReviewerUserId ??= userId;
        match.ReviewedAt = DateTime.UtcNow;
        match.ReadAt ??= DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var emailed = false;
        if (ready && !string.IsNullOrWhiteSpace(match.OwnerEmail) && _email is not null)
        {
            try
            {
                var agent = await _context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == match.ReviewerUserId, cancellationToken);
                var search = await _context.SavedSearches.AsNoTracking().FirstAsync(item => item.Id == match.SavedSearchId, cancellationToken);
                var siteUrl = (_configuration?["SiteUrl"] ?? "https://velven.ge").TrimEnd('/');
                emailed = await _email.SendAsync(
                    match.OwnerEmail!,
                    "A Velven client is interested in your apartment",
                    OwnerEmail(match.Apartment, PublicProfile(search.ProfileJson), agent, siteUrl),
                    cancellationToken);
                if (emailed)
                {
                    match.OwnerEmailedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Owner email for interest {Id} failed.", match.Id);
            }
        }
        return new ReviewResult(match.Status, match.OwnerUserId != null, emailed);
    }

    private static string OwnerEmail(Apartment apartment, JsonObject profile, AppUser? agent, string siteUrl)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
        string Field(string name) => profile.TryGetPropertyValue(name, out var value) && value is not null ? value.ToString() : "";
        var districts = profile["districts"] is JsonArray list ? string.Join(", ", list.Select(item => item?.ToString())) : "";
        var residents = (int.TryParse(Field("adults"), out var adults) ? adults : 1) + (int.TryParse(Field("children"), out var children) ? children : 0);
        var currency = Field("currency") switch { "GEL" => "₾", "EUR" => "€", _ => "$" };
        var budget = $"{currency}{Field("budgetMin")}–{currency}{Field("budgetMax")}";
        var pets = Field("hasPet") == "true" ? "Yes" : "No";
        var agentName = agent?.FullName ?? agent?.UserName ?? "Velven";
        var phone = agent?.PhoneNumber;
        return $"""
            <div style="font-family:Arial,sans-serif;max-width:560px;margin:auto;color:#14112a">
              <h2 style="color:#451a8f">A client is interested in your apartment</h2>
              <p>A Velven agent has checked a client whose requirements match <b>{E(apartment.Title)}</b> ({E(apartment.District)}).</p>
              <table style="border-collapse:collapse;width:100%;font-size:14px">
                <tr><td style="padding:6px 0;color:#6b6478">Budget</td><td>{E(budget)}</td></tr>
                <tr><td style="padding:6px 0;color:#6b6478">Preferred area</td><td>{E(districts)}</td></tr>
                <tr><td style="padding:6px 0;color:#6b6478">Residents</td><td>{residents}</td></tr>
                <tr><td style="padding:6px 0;color:#6b6478">Pets</td><td>{pets}</td></tr>
                <tr><td style="padding:6px 0;color:#6b6478">Move-in</td><td>{E(Field("moveInDate") is { Length: > 0 } date ? date : Field("moveInTiming"))}</td></tr>
              </table>
              <p style="margin-top:18px">Your Velven agent <b>{E(agentName)}</b>{(string.IsNullOrWhiteSpace(phone) ? "" : $" — <a href=\"tel:{E(phone)}\">{E(phone)}</a>")} will arrange the viewing.</p>
              <p><a href="{E(siteUrl)}/interested-clients?apartment={apartment.Id}" style="color:#451a8f">See it on Velven</a></p>
            </div>
            """;
    }

    // ---------- Owners ----------

    /// <summary>The owner's published listings with the clients an agent has marked ready.</summary>
    public async Task<List<ListingInterestView>> InterestsForAsync(string userId, CancellationToken cancellationToken)
    {
        var ready = await _context.ListingInterestNotifications.AsNoTracking()
            .Where(n => n.OwnerUserId == userId && n.Status == ListingInterestNotification.Ready && n.Apartment.IsApproved)
            .OrderByDescending(n => n.ReviewedAt)
            .Select(n => new
            {
                n.ApartmentId, n.SavedSearchId, n.OwnerReadAt, n.ReviewedAt,
                ProfileJson = n.SavedSearch.ProfileJson,
                AgentName = n.Reviewer != null ? n.Reviewer.FullName ?? n.Reviewer.UserName : null,
                AgentPhone = n.Reviewer != null ? n.Reviewer.PhoneNumber : null,
            })
            .ToListAsync(cancellationToken);
        var readyIds = ready.Select(item => item.ApartmentId).Distinct().ToList();
        var apartments = await ApartmentCatalogue.Published(_context.Apartments.AsNoTracking())
            .Where(apartment => apartment.UploadedByUserId == userId || readyIds.Contains(apartment.Id))
            .OrderByDescending(apartment => apartment.PublishedAt ?? apartment.CreatedAt)
            .ToListAsync(cancellationToken);

        return apartments.Select(apartment => new ListingInterestView
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
            Clients = ready.Where(item => item.ApartmentId == apartment.Id).Select(item => new InterestedClientView
            {
                SearchId = item.SavedSearchId,
                Profile = PublicProfile(item.ProfileJson),
                SearchingSince = item.ReviewedAt ?? DateTime.UtcNow,
                IsNew = item.OwnerReadAt == null,
                AgentName = item.AgentName,
                AgentPhone = item.AgentPhone,
            }).ToList(),
        }).ToList();
    }
}

public record ReviewResult(string Status, bool OwnerHasAccount, bool OwnerEmailed);

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

/// <summary>What the owner sees: requirements and the agent to call. Never who the client is.</summary>
public class InterestedClientView
{
    public int SearchId { get; set; }
    public JsonObject Profile { get; set; } = new();
    public DateTime SearchingSince { get; set; }
    public bool IsNew { get; set; }
    public string? AgentName { get; set; }
    public string? AgentPhone { get; set; }
}

/// <summary>What the reviewing agent sees in the CRM, including the client's contact details.</summary>
public class ReviewView
{
    public int Id { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public bool IsNew { get; set; }
    public string? ReviewerName { get; set; }
    public int SearchId { get; set; }
    public string SearchName { get; set; } = "";
    public DateTime SearchingSince { get; set; }
    public JsonObject Profile { get; set; } = new();
    public string? ClientName { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientPhone { get; set; }
    public int ApartmentId { get; set; }
    public string Title { get; set; } = "";
    public string? TitleEn { get; set; }
    public string? TitleKa { get; set; }
    public string? TitleRu { get; set; }
    public string District { get; set; } = "";
    public string Street { get; set; } = "";
    public decimal Price { get; set; }
    public int Bedrooms { get; set; }
    public string? ImageUrl { get; set; }
    public string Description { get; set; } = "";
    public string? OwnerName { get; set; }
    public string? OwnerPhone { get; set; }
    public string? OwnerEmail { get; set; }
    public bool OwnerHasAccount { get; set; }
}
