using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;

namespace Website_API.Controllers;

[ApiController]
[Route("api/ListingUploads")]
public class SourceListingUploadsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _users;
    public SourceListingUploadsController(AppDbContext context, UserManager<AppUser> users) { _context = context; _users = users; }

    [HttpGet]
    public async Task<IActionResult> Get(string sourcePlatform, string sourceListingId, CancellationToken token)
    {
        var source = Normalize(sourcePlatform);
        if (source is null || string.IsNullOrWhiteSpace(sourceListingId)) return BadRequest(new { message = "Valid sourcePlatform and sourceListingId are required." });
        return Ok(await _context.ListingUploads.AsNoTracking().Where(x => x.SourcePlatform == source && x.SourceListingId == sourceListingId.Trim())
            .OrderBy(x => x.AgentName).ThenByDescending(x => x.UploadedAt)
            .Select(x => new { x.Id, x.ApartmentId, x.AgentUserId, x.AgentName, x.Platform, x.PublishedListingId, x.PublishedUrl, x.SourcePlatform, x.SourceListingId, x.SourceUrl, x.UploadedAt }).ToListAsync(token));
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> Post(CreateSourceListingUploadDto dto, CancellationToken token)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); if (userId is null) return Unauthorized();
        var source = Normalize(dto.SourcePlatform); var platform = Normalize(dto.Platform);
        if (source is null || platform is null) return BadRequest(new { message = "Platforms must be myhome or ssge." });
        var existing = await _context.ListingUploads.Include(x => x.AgentUser).FirstOrDefaultAsync(x => x.SourcePlatform == source && x.SourceListingId == dto.SourceListingId.Trim() && x.AgentUserId == userId && x.Platform == platform && x.PublishedListingId == dto.PublishedListingId.Trim(), token);
        if (existing is not null) return Ok(ToResponse(existing));
        var user = await _users.FindByIdAsync(userId); if (user is null) return Unauthorized();
        var upload = new ListingUpload { SourcePlatform = source, SourceListingId = dto.SourceListingId.Trim(), SourceUrl = dto.SourceUrl?.Trim(), AgentUserId = userId, AgentUser = user, AgentName = user.FullName ?? user.UserName ?? userId, Platform = platform, PublishedListingId = dto.PublishedListingId.Trim(), PublishedUrl = dto.PublishedUrl?.Trim() };
        _context.ListingUploads.Add(upload); await _context.SaveChangesAsync(token); return Ok(ToResponse(upload));
    }

    private static string? Normalize(string value) { var v = value.Trim().ToLowerInvariant().Replace(".", ""); return v is "myhome" or "ssge" ? v : null; }
    private static object ToResponse(ListingUpload x) => new { x.Id, x.ApartmentId, x.AgentUserId, x.AgentName, x.Platform, x.PublishedListingId, x.PublishedUrl, x.SourcePlatform, x.SourceListingId, x.SourceUrl, x.UploadedAt };
}
