using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

/// <summary>
/// Client matches for listings. Agents review each match in the CRM first ("reviews"); owners
/// only see clients an agent marked ready, with that agent's contact details.
/// </summary>
[ApiController]
[Authorize]
[Route("api/listing-interest")]
public class ListingInterestController : ControllerBase
{
    private const string StaffRoles = "Admin,Manager,Agent,Uploader";

    private readonly AppDbContext _context;
    private readonly ListingInterestService _interests;
    private readonly SupabaseStorageService _storage;

    public ListingInterestController(AppDbContext context, ListingInterestService interests, SupabaseStorageService storage)
    {
        _context = context;
        _interests = interests;
        _storage = storage;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private bool IsManager => User.IsInRole("Admin") || User.IsInRole("Manager");
    private bool IsStaff => IsManager || User.IsInRole("Agent") || User.IsInRole("Uploader");

    private Task<string?> SignedPhoto(string? path, CancellationToken cancellationToken) =>
        _storage.CreateSignedUrlAsync(path, SupabaseStorageService.ListingPhotoUrlSeconds, cancellationToken);

    // ---------- Owners ----------

    /// <summary>The owner's listings with clients an agent has marked ready.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var listings = await _interests.InterestsForAsync(UserId, cancellationToken);
        foreach (var listing in listings) listing.ImageUrl = await SignedPhoto(listing.ImageUrl, cancellationToken);
        return Ok(listings);
    }

    /// <summary>
    /// Bell. Staff: matches waiting for their review. Owners: clients an agent marked ready.
    /// </summary>
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var staff = IsStaff;
        var query = staff
            ? _interests.ReviewQueue(UserId, IsManager).AsNoTracking().Where(n => n.Status == ListingInterestNotification.Pending)
            : _context.ListingInterestNotifications.AsNoTracking().Where(n =>
                n.OwnerUserId == UserId && n.Status == ListingInterestNotification.Ready && n.Apartment.IsApproved);
        var total = await query.CountAsync(cancellationToken);
        var unread = await (staff ? query.Where(n => n.ReadAt == null) : query.Where(n => n.OwnerReadAt == null)).CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(n => staff ? n.CreatedAt : n.ReviewedAt ?? n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.Id,
                CreatedAt = staff ? n.CreatedAt : n.ReviewedAt ?? n.CreatedAt,
                ReadAt = staff ? n.ReadAt : n.OwnerReadAt,
                Apartment = new
                {
                    n.Apartment.Id, n.Apartment.Title, n.Apartment.TitleEn, n.Apartment.TitleKa, n.Apartment.TitleRu,
                    n.Apartment.Price, n.Apartment.District, n.Apartment.Street, n.Apartment.Bedrooms, n.Apartment.ImageUrl,
                },
            })
            .ToListAsync(cancellationToken);

        var items = new List<object>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(new
            {
                row.Id,
                Type = staff ? "interest-review" : "interest",
                row.CreatedAt,
                row.ReadAt,
                Apartment = row.Apartment with { ImageUrl = await SignedPhoto(row.Apartment.ImageUrl, cancellationToken) },
            });
        }
        return Ok(new { items, total, unread, page, pageSize });
    }

    [HttpGet("notifications/summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var unread = IsStaff
            ? await _interests.ReviewQueue(UserId, IsManager).CountAsync(n =>
                n.Status == ListingInterestNotification.Pending && n.ReadAt == null, cancellationToken)
            : await _context.ListingInterestNotifications.CountAsync(n =>
                n.OwnerUserId == UserId && n.Status == ListingInterestNotification.Ready &&
                n.OwnerReadAt == null && n.Apartment.IsApproved, cancellationToken);
        return Ok(new { unread });
    }

    [HttpPost("notifications/{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var updated = IsStaff
            ? await _interests.ReviewQueue(UserId, IsManager).Where(n => n.Id == id && n.ReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), cancellationToken)
            : await _context.ListingInterestNotifications.Where(n => n.Id == id && n.OwnerUserId == UserId && n.OwnerReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.OwnerReadAt, now), cancellationToken);
        return updated == 0 ? NotFound() : NoContent();
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (IsStaff)
            await _interests.ReviewQueue(UserId, IsManager).Where(n => n.ReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), cancellationToken);
        else
            await _context.ListingInterestNotifications.Where(n => n.OwnerUserId == UserId && n.OwnerReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.OwnerReadAt, now), cancellationToken);
        return NoContent();
    }

    // ---------- Agents: review before the owner is told ----------

    /// <summary>Matches to review (status pending|ready|dismissed), with the client's contact details.</summary>
    [Authorize(Roles = StaffRoles)]
    [HttpGet("reviews")]
    public async Task<IActionResult> GetReviews([FromQuery] string status = ListingInterestNotification.Pending, CancellationToken cancellationToken = default)
    {
        if (status is not (ListingInterestNotification.Pending or ListingInterestNotification.Ready or ListingInterestNotification.Dismissed))
            return BadRequest(new { message = "Status must be pending, ready or dismissed." });
        var reviews = await _interests.ReviewsAsync(UserId, IsManager, status, cancellationToken);
        foreach (var review in reviews) review.ImageUrl = await SignedPhoto(review.ImageUrl, cancellationToken);
        return Ok(reviews);
    }

    /// <summary>The agent checked the client: tell the owner, with the agent's phone.</summary>
    [Authorize(Roles = StaffRoles)]
    [HttpPost("reviews/{id:int}/ready")]
    public Task<IActionResult> MarkReady(int id, CancellationToken cancellationToken) => Review(id, true, cancellationToken);

    /// <summary>Not a fit: the owner is never told.</summary>
    [Authorize(Roles = StaffRoles)]
    [HttpPost("reviews/{id:int}/dismiss")]
    public Task<IActionResult> Dismiss(int id, CancellationToken cancellationToken) => Review(id, false, cancellationToken);

    private async Task<IActionResult> Review(int id, bool ready, CancellationToken cancellationToken)
    {
        var result = await _interests.ReviewAsync(id, UserId, IsManager, ready, cancellationToken);
        return result is null ? NotFound(new { message = "Match not found." }) : Ok(result);
    }
}
