using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/owner-listings")]
public class OwnerListingsController : ControllerBase
{
    private const string StaffRoles = "Admin,Manager,Agent,Uploader";
    private static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "new", "contacted", "visited", "published", "rejected" };
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly SupabaseStorageService _storage;

    public OwnerListingsController(AppDbContext context, UserManager<AppUser> userManager, SupabaseStorageService storage)
    {
        _context = context;
        _userManager = userManager;
        _storage = storage;
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPost("links")]
    public async Task<IActionResult> CreateLink([FromBody] CreateLinkRequest request, CancellationToken cancellationToken)
    {
        if (request.DealType is not ("Rent" or "Sale"))
            return BadRequest(new { message = "Deal type must be Rent or Sale." });
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        _context.OwnerListingLinks.Add(new OwnerListingLink
        {
            Token = token, AgentUserId = userId, DealType = request.DealType, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { token, path = $"/owner/{token}", dealType = request.DealType });
    }

    [AllowAnonymous]
    [HttpGet("links/{token}")]
    public async Task<IActionResult> GetLink(string token, CancellationToken cancellationToken)
    {
        var link = await _context.OwnerListingLinks.AsNoTracking().FirstOrDefaultAsync(x => x.Token == token && x.IsActive, cancellationToken);
        if (link is null) return NotFound(new { message = "This owner link is invalid or no longer active." });
        var agent = await _userManager.FindByIdAsync(link.AgentUserId);
        var photo = await _storage.CreateSignedUrlAsync(agent?.ProfilePicture, 3600, cancellationToken);
        return Ok(new { link.DealType, agentName = agent?.FullName ?? agent?.UserName ?? "Velven agent", agentPhoto = photo });
    }

    [AllowAnonymous]
    [EnableRateLimiting("CrmInquiries")]
    [HttpPost("links/{token}/submissions")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> Submit(string token, [FromForm] OwnerSubmissionForm form, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(form.OwnerName) || string.IsNullOrWhiteSpace(form.OwnerPhone))
            return BadRequest(new { message = "Owner name and phone are required." });
        try { JsonDocument.Parse(form.Data ?? "{}"); }
        catch (JsonException) { return BadRequest(new { message = "Data must be valid JSON." }); }
        var link = await _context.OwnerListingLinks.FirstOrDefaultAsync(x => x.Token == token && x.IsActive, cancellationToken);
        if (link is null) return NotFound(new { message = "This owner link is invalid or no longer active." });
        var photos = form.Photos ?? [];
        if (photos.Count > 15) return BadRequest(new { message = "Upload no more than 15 photos." });
        if (photos.Any(x => x.Length == 0 || !x.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Only image files are allowed." });
        var paths = new List<string>();
        foreach (var photo in photos)
        {
            var path = await _storage.UploadImageAsync(photo, "owner-submissions", cancellationToken);
            if (!string.IsNullOrWhiteSpace(path)) paths.Add(path);
        }
        var now = DateTime.UtcNow;
        _context.OwnerSubmissions.Add(new OwnerSubmission
        {
            LinkId = link.Id, AgentUserId = link.AgentUserId, DealType = link.DealType,
            OwnerName = form.OwnerName.Trim(), OwnerPhone = form.OwnerPhone.Trim(),
            OwnerEmail = string.IsNullOrWhiteSpace(form.OwnerEmail) ? null : form.OwnerEmail.Trim(),
            DataJson = form.Data ?? "{}", PhotoPathsJson = JsonSerializer.Serialize(paths),
            Status = "new", CreatedAt = now, UpdatedAt = now
        });
        link.Uses++;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Thank you! Our agent will call you soon to arrange a visit." });
    }

    [Authorize(Roles = StaffRoles)]
    [HttpGet("submissions")]
    public async Task<IActionResult> GetSubmissions(CancellationToken cancellationToken)
    {
        var query = AccessibleSubmissions().OrderByDescending(x => x.CreatedAt);
        var rows = await query.ToListAsync(cancellationToken);
        // Sequential on purpose: ToResponse queries the shared DbContext (agent lookup),
        // and EF Core throws when two queries run on one context at the same time.
        var result = new List<object>(rows.Count);
        foreach (var row in rows) result.Add(await ToResponse(row, cancellationToken));
        return Ok(result);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpGet("submissions/{id:long}")]
    public async Task<IActionResult> GetSubmission(long id, CancellationToken cancellationToken)
    {
        var row = await AccessibleSubmissions().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return row is null ? NotFound(new { message = "Submission not found." }) : Ok(await ToResponse(row, cancellationToken));
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPatch("submissions/{id:long}")]
    public async Task<IActionResult> UpdateSubmission(long id, [FromBody] UpdateSubmissionRequest request, CancellationToken cancellationToken)
    {
        var row = await AccessibleSubmissions().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null) return NotFound(new { message = "Submission not found." });
        if (request.Status is not null)
        {
            if (!AllowedStatuses.Contains(request.Status)) return BadRequest(new { message = "Invalid status." });
            row.Status = request.Status.ToLowerInvariant();
        }
        if (request.Verification.HasValue) row.VerificationJson = request.Verification.Value.GetRawText();
        if (request.AgentNotes is not null) row.AgentNotes = request.AgentNotes;
        if (request.PublishedApartmentId.HasValue) row.PublishedApartmentId = request.PublishedApartmentId;
        row.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(await ToResponse(row, cancellationToken));
    }

    private IQueryable<OwnerSubmission> AccessibleSubmissions()
    {
        var elevated = User.IsInRole("Admin") || User.IsInRole("Manager");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return _context.OwnerSubmissions.Where(x => elevated || x.AgentUserId == userId);
    }

    private async Task<object> ToResponse(OwnerSubmission row, CancellationToken cancellationToken)
    {
        var agent = await _userManager.FindByIdAsync(row.AgentUserId);
        var paths = Deserialize<string[]>(row.PhotoPathsJson) ?? [];
        var photos = await Task.WhenAll(paths.Select(x => _storage.CreateSignedUrlAsync(x, 3600, cancellationToken)));
        return new { row.Id, row.LinkId, row.AgentUserId, agentName = agent?.FullName ?? agent?.UserName,
            row.DealType, row.OwnerName, row.OwnerPhone, row.OwnerEmail,
            data = Deserialize<JsonElement>(row.DataJson), photos = photos.Where(x => x is not null),
            row.Status, verification = Deserialize<JsonElement>(row.VerificationJson), row.AgentNotes,
            row.PublishedApartmentId, row.CreatedAt, row.UpdatedAt };
    }

    private static T? Deserialize<T>(string json) { try { return JsonSerializer.Deserialize<T>(json); } catch { return default; } }
}

public sealed record CreateLinkRequest(string DealType);
public sealed class OwnerSubmissionForm
{
    public string? Data { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerPhone { get; set; } = string.Empty;
    public string? OwnerEmail { get; set; }
    public List<IFormFile>? Photos { get; set; }
}
public sealed class UpdateSubmissionRequest
{
    public string? Status { get; set; }
    public JsonElement? Verification { get; set; }
    public string? AgentNotes { get; set; }
    public int? PublishedApartmentId { get; set; }
}
