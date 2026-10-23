using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

public class SaveSearchDto
{
    public string? Name { get; set; }
    public JsonElement Profile { get; set; }
}

public class UpdateSearchDto
{
    public string? Name { get; set; }
    public JsonElement? Profile { get; set; }
    public bool? NotificationsEnabled { get; set; }
}

public class MarkSeenDto
{
    public int? SearchId { get; set; }
    public List<int> ApartmentIds { get; set; } = [];
}

public class ClientPreferencesDto
{
    public bool? MatchAlertsInApp { get; set; }
    public bool? MatchAlertsEmail { get; set; }
    public string? PreferredLanguage { get; set; }
}

/// <summary>
/// Lets only client accounts through. Checked against the database, not the sign-in token, so
/// accounts that became clients after signing in (and staff who stopped being clients) are right
/// without signing in again.
/// </summary>
public class RequireClientRoleAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var isClient = await db.UserRoles.AnyAsync(userRole =>
            userRole.UserId == userId &&
            db.Roles.Any(role => role.Id == userRole.RoleId && role.NormalizedName == "CLIENT"));
        var isStaff = await db.UserRoles.AnyAsync(ur => ur.UserId == userId && db.Roles.Any(r =>
            r.Id == ur.RoleId && (r.NormalizedName == "ADMIN" || r.NormalizedName == "MANAGER" ||
            r.NormalizedName == "AGENT" || r.NormalizedName == "UPLOADER" || r.NormalizedName == "CRM-MANAGER" || r.NormalizedName == "CRM_MANAGER" || r.NormalizedName == "OWNER")));
        if (!isClient || isStaff)
            context.Result = new ObjectResult(new { message = "Velven Discover is available for client accounts." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
    }
}

/// <summary>
/// Velven Discover for client accounts: saved Velven Match searches, their new matches and
/// alerts. Every query is limited to the signed-in client's own records.
/// </summary>
[ApiController]
[Authorize]
[RequireClientRole]
[Route("api/client")]
public class ClientController : ControllerBase
{
    private const int MaxSearches = 20;
    private const int MaxProfileBytes = 100_000;

    private readonly AppDbContext _context;
    private readonly SupabaseStorageService _storage;

    public ClientController(AppDbContext context, SupabaseStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    // ---------- Saved searches ----------

    [HttpGet("searches")]
    public async Task<IActionResult> GetSearches(CancellationToken cancellationToken)
    {
        var searches = await _context.SavedSearches
            .AsNoTracking()
            .Where(search => search.UserId == UserId)
            .OrderByDescending(search => search.UpdatedAt)
            .Select(search => new
            {
                search.Id,
                search.Name,
                search.ProfileJson,
                search.NotificationsEnabled,
                search.CreatedAt,
                search.UpdatedAt,
                search.CriteriaSince,
                NewMatches = search.Matches.Count(match =>
                    match.SeenAt == null && match.Apartment.IsApproved),
            })
            .ToListAsync(cancellationToken);

        return Ok(searches.Select(search => new
        {
            search.Id,
            search.Name,
            Profile = JsonNode.Parse(search.ProfileJson),
            search.NotificationsEnabled,
            search.CreatedAt,
            search.UpdatedAt,
            search.CriteriaSince,
            search.NewMatches,
        }));
    }

    /// <summary>
    /// Saves a finished Velven Match search. The same answers saved again return the existing
    /// search (no duplicate); its "last updated" time moves forward.
    /// </summary>
    [HttpPost("searches")]
    public async Task<IActionResult> SaveSearch(SaveSearchDto dto, CancellationToken cancellationToken)
    {
        var (profileJson, error) = ReadProfile(dto.Profile);
        if (error is not null) return BadRequest(new { message = error });

        var hash = CriteriaHash(profileJson!);
        var existing = await _context.SavedSearches
            .FirstOrDefaultAsync(search => search.UserId == UserId && search.CriteriaHash == hash, cancellationToken);
        if (existing is not null)
        {
            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { existing.Id, existing.Name, existed = true });
        }

        if (await _context.SavedSearches.CountAsync(search => search.UserId == UserId, cancellationToken) >= MaxSearches)
            return BadRequest(new { message = $"You can keep up to {MaxSearches} saved searches. Delete one to save a new search." });

        var search = new SavedSearch
        {
            UserId = UserId,
            Name = CleanName(dto.Name) ?? "My search",
            ProfileJson = profileJson!,
            CriteriaHash = hash,
        };
        _context.SavedSearches.Add(search);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A double submit saved it a moment ago.
            var saved = await _context.SavedSearches.AsNoTracking()
                .FirstAsync(item => item.UserId == UserId && item.CriteriaHash == hash, cancellationToken);
            return Ok(new { saved.Id, saved.Name, existed = true });
        }
        return Ok(new { search.Id, search.Name, existed = false });
    }

    [HttpPut("searches/{id:int}")]
    public async Task<IActionResult> UpdateSearch(int id, UpdateSearchDto dto, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var search = await _context.SavedSearches
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == UserId, cancellationToken);
        if (search is null) return NotFound(new { message = "Saved search not found." });

        if (dto.Name is not null)
        {
            var name = CleanName(dto.Name);
            if (name is null) return BadRequest(new { message = "Please enter a name." });
            search.Name = name;
        }
        if (dto.NotificationsEnabled is bool enabled) search.NotificationsEnabled = enabled;

        if (dto.Profile is JsonElement profile)
        {
            var (profileJson, error) = ReadProfile(profile);
            if (error is not null) return BadRequest(new { message = error });
            var hash = CriteriaHash(profileJson!);
            if (hash != search.CriteriaHash)
            {
                if (await _context.SavedSearches.AnyAsync(item => item.UserId == UserId && item.CriteriaHash == hash && item.Id != id, cancellationToken))
                    return Conflict(new { message = "You already have a saved search with these preferences." });
                search.ProfileJson = profileJson!;
                search.CriteriaHash = hash;
                // New criteria: old matches no longer apply, and only listings published from now count as new.
                search.CriteriaSince = DateTime.UtcNow;
                await _context.ClientNotifications.Where(n => n.SavedSearchId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.EmailPending, false), cancellationToken);
                await _context.SavedSearchMatches.Where(match => match.SavedSearchId == id).ExecuteDeleteAsync(cancellationToken);
                await _context.ClientNotifications
                    .Where(notification => notification.SavedSearchId == id && notification.ReadAt == null)
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        search.UpdatedAt = DateTime.UtcNow;
        if (!search.NotificationsEnabled)
            await _context.ClientNotifications.Where(n => n.SavedSearchId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.EmailPending, false), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { search.Id, search.Name, search.NotificationsEnabled });
    }

    [HttpDelete("searches/{id:int}")]
    public async Task<IActionResult> DeleteSearch(int id, CancellationToken cancellationToken)
    {
        var deleted = await _context.SavedSearches
            .Where(search => search.Id == id && search.UserId == UserId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? NotFound(new { message = "Saved search not found." }) : NoContent();
    }

    // ---------- New matches ----------

    /// <summary>Listings published after a search was saved that meet all its must-haves.</summary>
    [HttpGet("matches")]
    public async Task<IActionResult> GetMatches([FromQuery] int? searchId, CancellationToken cancellationToken)
    {
        var matches = await _context.SavedSearchMatches
            .AsNoTracking()
            .Where(match => match.SavedSearch.UserId == UserId &&
                            (searchId == null || match.SavedSearchId == searchId))
            // Matches are only recorded for public listings; one unpublished since is hidden.
            .Where(match => match.Apartment.IsApproved)
            .OrderByDescending(match => match.MatchedAt)
            .Take(200)
            .Select(match => new { match.SavedSearchId, match.ApartmentId, match.MatchedAt, match.SeenAt })
            .ToListAsync(cancellationToken);
        return Ok(matches);
    }

    [HttpPost("matches/seen")]
    public async Task<IActionResult> MarkMatchesSeen(MarkSeenDto dto, CancellationToken cancellationToken)
    {
        var ids = dto.ApartmentIds.Distinct().Take(200).ToList();
        var now = DateTime.UtcNow;
        var updated = await _context.SavedSearchMatches
            .Where(match => match.SavedSearch.UserId == UserId && match.SeenAt == null &&
                            (dto.SearchId == null || match.SavedSearchId == dto.SearchId) &&
                            (ids.Count == 0 || ids.Contains(match.ApartmentId)))
            .ExecuteUpdateAsync(setters => setters.SetProperty(match => match.SeenAt, now), cancellationToken);
        return Ok(new { updated });
    }

    // ---------- Notifications ----------

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = _context.ClientNotifications.AsNoTracking()
            .Where(notification => notification.UserId == UserId &&
                                   (notification.Apartment == null || notification.Apartment.IsApproved));
        var total = await query.CountAsync(cancellationToken);
        var unread = await query.CountAsync(notification => notification.ReadAt == null, cancellationToken);
        var rows = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(notification => new
            {
                notification.Id,
                notification.Type,
                notification.CreatedAt,
                notification.ReadAt,
                notification.SavedSearchId,
                SearchName = notification.SavedSearch != null ? notification.SavedSearch.Name : null,
                Apartment = notification.Apartment == null ? null : new
                {
                    notification.Apartment.Id,
                    notification.Apartment.Title,
                    notification.Apartment.Description,
                    notification.Apartment.Price,
                    notification.Apartment.City,
                    notification.Apartment.District,
                    notification.Apartment.Street,
                    notification.Apartment.Bedrooms,
                    notification.Apartment.SizeSquareMeters,
                    notification.Apartment.ImageUrl,
                },
            })
            .ToListAsync(cancellationToken);

        var items = new List<object>(rows.Count);
        foreach (var row in rows)
        {
            var apartment = row.Apartment;
            items.Add(new
            {
                row.Id,
                row.Type,
                row.CreatedAt,
                row.ReadAt,
                row.SavedSearchId,
                row.SearchName,
                Apartment = apartment == null ? null : new
                {
                    apartment.Id,
                    apartment.Title,
                    // Only the listing meta line (deal type), not the whole description.
                    Description = apartment.Description.Length > 600 ? apartment.Description[..600] : apartment.Description,
                    apartment.Price,
                    apartment.City,
                    apartment.District,
                    apartment.Street,
                    apartment.Bedrooms,
                    apartment.SizeSquareMeters,
                    ImageUrl = await _storage.CreateSignedUrlAsync(
                        apartment.ImageUrl, SupabaseStorageService.ListingPhotoUrlSeconds, cancellationToken),
                },
            });
        }
        return Ok(new { items, total, unread, page, pageSize });
    }

    /// <summary>Cheap badge counts for the header bell and the Velven Discover menu item.</summary>
    [HttpGet("notifications/summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var unread = await _context.ClientNotifications.CountAsync(notification =>
            notification.UserId == UserId && notification.ReadAt == null &&
            (notification.Apartment == null || notification.Apartment.IsApproved), cancellationToken);
        var newMatches = await _context.SavedSearchMatches.CountAsync(match =>
            match.SavedSearch.UserId == UserId && match.SeenAt == null && match.Apartment.IsApproved, cancellationToken);
        return Ok(new { unread, newMatches });
    }

    [HttpPost("notifications/{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var updated = await _context.ClientNotifications
            .Where(notification => notification.Id == id && notification.UserId == UserId && notification.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAt, DateTime.UtcNow), cancellationToken);
        if (updated == 0 && !await _context.ClientNotifications.AnyAsync(notification => notification.Id == id && notification.UserId == UserId, cancellationToken))
            return NotFound();
        return NoContent();
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await _context.ClientNotifications
            .Where(notification => notification.UserId == UserId && notification.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAt, DateTime.UtcNow), cancellationToken);
        return NoContent();
    }

    // ---------- Preferences ----------

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == UserId, cancellationToken);
        if (user is null) return Unauthorized();
        return Ok(new { user.MatchAlertsInApp, user.MatchAlertsEmail, user.PreferredLanguage, emailConfirmed = user.EmailConfirmed });
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences(ClientPreferencesDto dto, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == UserId, cancellationToken);
        if (user is null) return Unauthorized();
        if (dto.PreferredLanguage is not null)
        {
            if (dto.PreferredLanguage is not ("ka" or "en" or "ru"))
                return BadRequest(new { message = "Unsupported language." });
            user.PreferredLanguage = dto.PreferredLanguage;
        }
        if (dto.MatchAlertsInApp is bool inApp) user.MatchAlertsInApp = inApp;
        if (dto.MatchAlertsEmail is bool email)
        {
            user.MatchAlertsEmail = email;
            // Turning email off stops anything still waiting to be sent.
            if (!email)
                await _context.ClientNotifications
                    .Where(notification => notification.UserId == UserId && notification.EmailPending)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.EmailPending, false), cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { user.MatchAlertsInApp, user.MatchAlertsEmail, user.PreferredLanguage, emailConfirmed = user.EmailConfirmed });
    }

    // ---------- Helpers ----------

    private static string? CleanName(string? name)
    {
        var clean = string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length == 0) return null;
        return clean.Length > 80 ? clean[..80] : clean;
    }

    private static (string? Json, string? Error) ReadProfile(JsonElement profile)
    {
        if (profile.ValueKind != JsonValueKind.Object) return (null, "The search preferences are missing.");
        var json = profile.GetRawText();
        if (Encoding.UTF8.GetByteCount(json) > MaxProfileBytes) return (null, "The search preferences are too large.");
        var goal = profile.TryGetProperty("propertyGoal", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
        if (goal is not ("Rent" or "Buy")) return (null, "Choose whether you want to rent or buy.");
        if (!SavedSearchProfile.IsValid(json)) return (null, "The search preferences are invalid.");
        return (json, null);
    }

    /// <summary>Lists whose order carries no meaning; sorted so the same answers hash the same.</summary>
    private static readonly HashSet<string> UnorderedLists = new(StringComparer.OrdinalIgnoreCase)
    {
        "districts", "childrenAgeGroups", "transportation", "lifestyles", "petTypes", "bedroomChoices",
    };

    /// <summary>SHA-256 of the answers with keys sorted and empty values dropped.</summary>
    public static string CriteriaHash(string profileJson)
    {
        var canonical = Canonical(JsonNode.Parse(profileJson), null)?.ToJsonString() ?? "{}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static JsonNode? Canonical(JsonNode? node, string? key)
    {
        switch (node)
        {
            case JsonObject obj:
                var result = new JsonObject();
                foreach (var (name, child) in obj.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    if (name == "budgetConversion") continue;
                    var value = Canonical(child, name);
                    if (value is null) continue;
                    if (value is JsonValue text && text.TryGetValue<string>(out var s) && s.Length == 0) continue;
                    if (value is JsonArray { Count: 0 }) continue;
                    result[name] = value;
                }
                return result;
            case JsonArray array:
                var items = array.Select(item => Canonical(item, null)).ToList();
                if (key is not null && UnorderedLists.Contains(key))
                    items = [.. items.OrderBy(item => item?.ToJsonString(), StringComparer.Ordinal)];
                return new JsonArray([.. items]);
            case null:
                return null;
            default:
                return JsonNode.Parse(node.ToJsonString());
        }
    }
}
