using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Services;

namespace Website_API.Controllers;

/// <summary>
/// For owners and agents: clients looking for homes like the ones they listed, and the bell
/// alerts when a client's search matches one of their listings.
/// </summary>
[ApiController]
[Authorize]
[Route("api/listing-interest")]
public class ListingInterestController : ControllerBase
{
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

    /// <summary>Every published listing of the signed-in user with its interested clients.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var listings = await _interests.InterestsForAsync(UserId, cancellationToken);
        foreach (var listing in listings)
            listing.ImageUrl = await _storage.CreateSignedUrlAsync(
                listing.ImageUrl, SupabaseStorageService.ListingPhotoUrlSeconds, cancellationToken);
        return Ok(listings);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = _context.ListingInterestNotifications.AsNoTracking()
            .Where(n => n.UserId == UserId && n.Apartment.IsApproved);
        var total = await query.CountAsync(cancellationToken);
        var unread = await query.CountAsync(n => n.ReadAt == null, cancellationToken);
        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.Id,
                n.CreatedAt,
                n.ReadAt,
                Apartment = new
                {
                    n.Apartment.Id,
                    n.Apartment.Title,
                    n.Apartment.TitleEn,
                    n.Apartment.TitleKa,
                    n.Apartment.TitleRu,
                    n.Apartment.Price,
                    n.Apartment.District,
                    n.Apartment.Street,
                    n.Apartment.Bedrooms,
                    n.Apartment.ImageUrl,
                },
            })
            .ToListAsync(cancellationToken);

        var items = new List<object>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(new
            {
                row.Id,
                Type = "interest",
                row.CreatedAt,
                row.ReadAt,
                Apartment = row.Apartment with
                {
                    ImageUrl = await _storage.CreateSignedUrlAsync(
                        row.Apartment.ImageUrl, SupabaseStorageService.ListingPhotoUrlSeconds, cancellationToken),
                },
            });
        }
        return Ok(new { items, total, unread, page, pageSize });
    }

    [HttpGet("notifications/summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var unread = await _context.ListingInterestNotifications.CountAsync(n =>
            n.UserId == UserId && n.ReadAt == null && n.Apartment.IsApproved, cancellationToken);
        return Ok(new { unread });
    }

    [HttpPost("notifications/{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var updated = await _context.ListingInterestNotifications
            .Where(n => n.Id == id && n.UserId == UserId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow), cancellationToken);
        if (updated == 0 && !await _context.ListingInterestNotifications.AnyAsync(n => n.Id == id && n.UserId == UserId, cancellationToken))
            return NotFound();
        return NoContent();
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await _context.ListingInterestNotifications
            .Where(n => n.UserId == UserId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow), cancellationToken);
        return NoContent();
    }
}
