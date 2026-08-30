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
[Route("api/Apartments/{apartmentId:int}/uploads")]
public class ListingUploadsController : ControllerBase
{
    private static readonly HashSet<string> SupportedPlatforms =
        new(StringComparer.OrdinalIgnoreCase) { "myhome", "ssge" };

    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;

    public ListingUploadsController(
        AppDbContext context,
        UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetUploads(
        int apartmentId,
        CancellationToken cancellationToken)
    {
        if (!await _context.Apartments.AsNoTracking()
            .AnyAsync(item => item.Id == apartmentId, cancellationToken))
        {
            return NotFound(new { message = "Apartment not found." });
        }

        var uploads = await _context.ListingUploads.AsNoTracking()
            .Where(item => item.ApartmentId == apartmentId)
            .OrderBy(item => item.AgentName)
            .ThenBy(item => item.Platform)
            .ThenByDescending(item => item.UploadedAt)
            .Select(item => new
            {
                item.Id,
                item.ApartmentId,
                AgentUserId = item.AgentUserId,
                item.AgentName,
                item.Platform,
                item.PublishedListingId,
                item.PublishedUrl,
                item.UploadedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(uploads);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateUpload(
        int apartmentId,
        CreateListingUploadDto dto,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (!await _context.Apartments.AsNoTracking()
            .AnyAsync(item => item.Id == apartmentId, cancellationToken))
        {
            return NotFound(new { message = "Apartment not found." });
        }

        var platform = NormalizePlatform(dto.Platform);
        if (platform is null)
        {
            return BadRequest(new
            {
                message = "Platform must be 'myhome' or 'ssge'."
            });
        }

        var publishedListingId = dto.PublishedListingId.Trim();
        if (platform == "myhome" && (!int.TryParse(publishedListingId, out var myHomeId) || myHomeId < 20_000_000))
            return BadRequest(new { message = "MyHome publishedListingId must be a listing ID, not a payment/transaction ID." });
        var existing = await _context.ListingUploads
            .Include(item => item.AgentUser)
            .FirstOrDefaultAsync(item =>
                item.ApartmentId == apartmentId &&
                item.AgentUserId == userId &&
                item.Platform == platform &&
                item.PublishedListingId == publishedListingId,
                cancellationToken);

        if (existing is not null)
            return Ok(ToResponse(existing));

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized();

        var upload = new ListingUpload
        {
            ApartmentId = apartmentId,
            AgentUserId = userId,
            AgentUser = user,
            AgentName = user.FullName ?? user.UserName ?? userId,
            Platform = platform,
            PublishedListingId = publishedListingId,
            PublishedUrl = string.IsNullOrWhiteSpace(dto.PublishedUrl)
                ? null
                : dto.PublishedUrl.Trim()
        };

        _context.ListingUploads.Add(upload);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetUploads),
            new { apartmentId },
            ToResponse(upload));
    }

    [Authorize]
    [HttpDelete("{uploadId:int}")]
    public async Task<IActionResult> DeleteUpload(
        int apartmentId,
        int uploadId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var upload = await _context.ListingUploads.FirstOrDefaultAsync(item =>
            item.Id == uploadId && item.ApartmentId == apartmentId,
            cancellationToken);

        if (upload is null)
            return NotFound(new { message = "Listing upload not found." });

        if (upload.AgentUserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        _context.ListingUploads.Remove(upload);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? NormalizePlatform(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace(".", string.Empty, StringComparison.Ordinal);
        return SupportedPlatforms.Contains(normalized) ? normalized : null;
    }

    private static object ToResponse(ListingUpload item) => new
    {
        item.Id,
        item.ApartmentId,
        AgentUserId = item.AgentUserId,
        item.AgentName,
        item.Platform,
        item.PublishedListingId,
        item.PublishedUrl,
        item.UploadedAt
    };
}
