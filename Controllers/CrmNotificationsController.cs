using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

/// <summary>One staff bell for personal CRM updates and the existing client match queue.</summary>
[ApiController]
[Authorize(Roles = "Admin,Manager,Agent,Uploader")]
[Route("api/Crm/notifications")]
public class CrmNotificationsController(AppDbContext db, ListingInterestService interests,
    SupabaseStorageService storage) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private bool IsManager => User.IsInRole("Admin") || User.IsInRole("Manager");
    private IQueryable<CrmLeadNotification> LeadNotices => db.CrmLeadNotifications.Where(n =>
        n.UserId == UserId && (IsManager || (User.IsInRole("Agent") &&
        (n.Lead.AssignedAgentId == UserId || n.Lead.CreatedByUserId == UserId ||
         (n.Lead.Apartment != null && n.Lead.Apartment.UploadedByUserId == UserId)))));
    private IQueryable<ListingInterestNotification> MatchNotices => interests.ReviewQueue(UserId, IsManager)
        .Where(n => n.Status == ListingInterestNotification.Pending);

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct) => Ok(new
    {
        unread = await LeadNotices.CountAsync(n => n.ReadAt == null, ct) +
            await MatchNotices.CountAsync(n => n.ReadAt == null, ct),
    });

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 1000);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var leads = LeadNotices.AsNoTracking();
        var matches = MatchNotices.AsNoTracking();
        var total = await leads.CountAsync(ct) + await matches.CountAsync(ct);
        var unread = await leads.CountAsync(n => n.ReadAt == null, ct) +
            await matches.CountAsync(n => n.ReadAt == null, ct);
        // Taking the top K of each source is sufficient for the top K of their merged feed.
        var leadRows = await leads.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(page * pageSize).Select(n => new NoticeRow
            {
                Id = n.Id, Type = "crm-status", CreatedAt = n.CreatedAt, ReadAt = n.ReadAt,
                LeadId = n.LeadId, LeadName = n.Lead.Name, ActorName = n.ActorName,
                FromStatus = n.FromStatus, ToStatus = n.ToStatus,
            }).ToListAsync(ct);
        var matchRows = await matches.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(page * pageSize).Select(n => new NoticeRow
            {
                Id = n.Id, Type = "interest-review", CreatedAt = n.CreatedAt, ReadAt = n.ReadAt,
                ApartmentId = n.ApartmentId, Title = n.Apartment.Title, Price = n.Apartment.Price,
                District = n.Apartment.District, Street = n.Apartment.Street,
                Bedrooms = n.Apartment.Bedrooms, ImageUrl = n.Apartment.ImageUrl,
            }).ToListAsync(ct);
        var rows = leadRows.Concat(matchRows).OrderByDescending(n => n.CreatedAt)
            .ThenBy(n => n.Type, StringComparer.Ordinal).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var items = new List<object>();
        foreach (var row in rows)
            items.Add(new
            {
                row.Id, row.Type, row.CreatedAt, row.ReadAt,
                row.LeadId, row.LeadName, row.ActorName, row.FromStatus, row.ToStatus,
                Apartment = row.ApartmentId.HasValue ? new
                {
                    Id = row.ApartmentId.Value, row.Title, row.Price, row.District, row.Street,
                    row.Bedrooms, ImageUrl = await storage.CreateSignedUrlAsync(row.ImageUrl,
                        SupabaseStorageService.ListingPhotoUrlSeconds, ct),
                } : null,
            });
        return Ok(new { items, total, unread, page, pageSize });
    }

    [HttpPost("{type}/{id:int}/read")]
    public async Task<IActionResult> Read(string type, int id, CancellationToken ct)
    {
        if (type == "crm-status")
        {
            var notice = await LeadNotices.FirstOrDefaultAsync(n => n.Id == id, ct);
            if (notice is null) return NotFound();
            notice.ReadAt ??= DateTime.UtcNow;
        }
        else if (type == "interest-review")
        {
            var notice = await MatchNotices.FirstOrDefaultAsync(n => n.Id == id, ct);
            if (notice is null) return NotFound();
            notice.ReadAt ??= DateTime.UtcNow;
        }
        else return BadRequest();
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LeadNotices.Where(n => n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
        await MatchNotices.Where(n => n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    private sealed class NoticeRow
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public int? LeadId { get; set; }
        public string? LeadName { get; set; }
        public string? ActorName { get; set; }
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
        public int? ApartmentId { get; set; }
        public string? Title { get; set; }
        public decimal Price { get; set; }
        public string? District { get; set; }
        public string? Street { get; set; }
        public int Bedrooms { get; set; }
        public string? ImageUrl { get; set; }
    }
}
