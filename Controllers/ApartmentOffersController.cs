using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Website_API.Data;
using Website_API.DTO;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/apartment-offers")]
public class ApartmentOffersController(AppDbContext db, SupabaseStorageService storage) : ControllerBase
{
    public const string StaffRoles = "Admin,Manager,Agent";
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private bool Elevated => User.IsInRole("Admin") || User.IsInRole("Manager");
    private IQueryable<ApartmentOffer> Queue => db.ApartmentOffers.Where(x => Elevated || x.ReviewerUserId == UserId);

    public record SubmitRequest(long ClientId, int ApartmentId, bool ShareWithOtherClients);

    [Authorize]
    [EnableRateLimiting("CrmInquiries")]
    [HttpPost]
    public async Task<IActionResult> Submit(SubmitRequest request, CancellationToken ct)
    {
        // The same 404 for missing and private clients, so IDs cannot be probed.
        var client = await ClientBoardEligibility.PublicProfiles(db).AsNoTracking()
            .Where(x => x.Id == request.ClientId).Select(x => new { x.Id, x.AgentUserId }).FirstOrDefaultAsync(ct);
        if (client is null) return NotFound(new { code = "client_not_found" });
        var ownsListing = await db.Apartments.AnyAsync(x => x.Id == request.ApartmentId && x.UploadedByUserId == UserId &&
            !EF.Functions.ILike(x.Description, "%Source: https://www.myhome.ge/%") &&
            !EF.Functions.ILike(x.Description, "%Source: https://home.ss.ge/%"), ct);
        if (!ownsListing) return NotFound(new { code = "apartment_not_found" });

        var offer = new ApartmentOffer { ClientProfileId = client.Id, ApartmentId = request.ApartmentId, SubmittedByUserId = UserId,
            ReviewerUserId = client.AgentUserId, ShareWithOtherClients = request.ShareWithOtherClients };
        db.ApartmentOffers.Add(offer);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { code = "already_offered" });
        }
        return Ok(new { offer.Id, offer.Status, offer.CreatedAt, offer.ShareWithOtherClients });
    }

    [Authorize(Roles = StaffRoles)]
    [HttpGet]
    public async Task<IActionResult> Reviews([FromQuery] string status = ApartmentOffer.Pending, CancellationToken ct = default)
    {
        if (status is not (ApartmentOffer.Pending or ApartmentOffer.Approved or ApartmentOffer.Rejected)) return BadRequest();
        var rows = await Queue.AsNoTracking().Where(x => x.Status == status)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(200)
            .Select(x => new
            {
                x.Id, ClientId = x.ClientProfileId, x.Status, x.ShareWithOtherClients, x.CreatedAt, x.ReviewedAt,
                IsNew = x.ReadAt == null,
                Submitter = new { Name = x.SubmittedByUser.FullName, Phone = x.SubmittedByUser.PhoneNumber },
                Apartment = new { x.Apartment.Id, x.Apartment.Title, x.Apartment.Price, x.Apartment.District, x.Apartment.Street,
                    x.Apartment.Bedrooms, x.Apartment.SizeSquareMeters, x.Apartment.ImageUrl, x.Apartment.IsApproved },
                MatchedClientIds = x.Matches.OrderBy(m => m.ClientProfileId).Select(m => m.ClientProfileId).ToList(),
            }).ToListAsync(ct);
        var items = new List<object>(rows.Count);
        foreach (var row in rows)
            items.Add(new
            {
                row.Id, row.ClientId, row.Status, row.ShareWithOtherClients, row.CreatedAt, row.ReviewedAt, row.IsNew,
                row.Submitter, row.MatchedClientIds,
                Apartment = new
                {
                    row.Apartment.Id, row.Apartment.Title, row.Apartment.Price, row.Apartment.District, row.Apartment.Street,
                    row.Apartment.Bedrooms, row.Apartment.SizeSquareMeters, row.Apartment.IsApproved,
                    ImageUrl = await storage.CreateSignedUrlAsync(row.Apartment.ImageUrl, SupabaseStorageService.ListingPhotoUrlSeconds, ct),
                },
            });
        return Ok(items);
    }

    /// <summary>Approved listings found compatible with the signed-in agent's own clients.</summary>
    [Authorize(Roles = StaffRoles)]
    [HttpGet("matches")]
    public async Task<IActionResult> Matches(CancellationToken ct) => Ok(await db.ApartmentOfferMatches.AsNoTracking()
        .Where(x => x.AgentUserId == UserId && x.Offer.Status == ApartmentOffer.Approved)
        .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(200)
        .Select(x => new
        {
            x.Id, x.ApartmentOfferId, ClientId = x.ClientProfileId, x.CreatedAt,
            Apartment = new { x.Offer.Apartment.Id, x.Offer.Apartment.Title, x.Offer.Apartment.Price, x.Offer.Apartment.District,
                x.Offer.Apartment.Street, x.Offer.Apartment.Bedrooms, x.Offer.Apartment.SizeSquareMeters, x.Offer.Apartment.IsApproved },
        }).ToListAsync(ct));

    [Authorize(Roles = StaffRoles)]
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await ReviewAsync(id, ApartmentOffer.Approved, ct);
        if (result is not null) return result;
        var offer = await db.ApartmentOffers.Include(x => x.Apartment).FirstAsync(x => x.Id == id, ct);
        var matched = 0;
        if (offer.ShareWithOtherClients)
        {
            var candidates = await ClientBoardEligibility.PublicProfiles(db).AsNoTracking()
                .Where(x => x.Id != offer.ClientProfileId).Select(x => new { x.Id, x.AgentUserId, x.ProfileJson }).ToListAsync(ct);
            foreach (var candidate in candidates)
            {
                if (!ApartmentOfferMatching.IsCompatible(offer.Apartment, ClientRequirements.Read(candidate.ProfileJson), DateTime.UtcNow)) continue;
                db.ApartmentOfferMatches.Add(new ApartmentOfferMatch { ApartmentOfferId = offer.Id, ClientProfileId = candidate.Id, AgentUserId = candidate.AgentUserId });
                matched++;
            }
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Ok(new { status = ApartmentOffer.Approved, matchedClients = matched });
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, CancellationToken ct) =>
        await ReviewAsync(id, ApartmentOffer.Rejected, ct) ?? Ok(new { status = ApartmentOffer.Rejected, matchedClients = 0 });

    /// <summary>Moves a pending offer once; a second click or reviewer gets 409. Null means success.</summary>
    private async Task<IActionResult?> ReviewAsync(int id, string status, CancellationToken ct)
    {
        if (!await Queue.AnyAsync(x => x.Id == id, ct)) return NotFound();
        var now = DateTime.UtcNow;
        var reviewer = UserId;
        var updated = await Queue.Where(x => x.Id == id && x.Status == ApartmentOffer.Pending).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, status).SetProperty(x => x.ReviewedAt, now)
            .SetProperty(x => x.ReviewedByUserId, reviewer).SetProperty(x => x.ReadAt, x => (DateTime?)(x.ReadAt ?? now)), ct);
        return updated == 0 ? Conflict(new { code = "already_reviewed" }) : null;
    }
}
