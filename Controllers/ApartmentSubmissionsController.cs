using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Route("api/owner-listings")]
public sealed class ApartmentSubmissionsController(AppDbContext db, SupabaseStorageService storage, ApartmentSubmissionService workflow, IOutputCacheStore cache) : ControllerBase
{
    private const string ReviewRoles = "Admin,Manager,Agent";
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private bool Elevated => User.IsInRole("Admin") || User.IsInRole("Manager");
    private IQueryable<OwnerSubmission> Accessible => db.OwnerSubmissions.Where(x => x.IsGeneralSubmission && (Elevated || x.AgentUserId == UserId));

    [AllowAnonymous, EnableRateLimiting("CrmInquiries")]
    [HttpPost("submissions"), RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> Submit([FromForm] GeneralSubmissionForm form, CancellationToken ct)
    {
        var data = SubmissionProperty.Read(form.Data ?? "{}");
        if (data is null) return BadRequest(new { message = "ბინის მონაცემები არასწორია." });
        var error = data.Validate(form.OwnerName, form.OwnerPhone);
        if (error is not null) return BadRequest(new { message = error });
        if (!Guid.TryParse(form.RequestId, out var key) || key == Guid.Empty) return BadRequest(new { message = "გაგზავნის კოდი არასწორია. განაახლეთ გვერდი." });
        if ((form.OwnerEmail?.Length ?? 0) > 160 || !string.IsNullOrWhiteSpace(form.OwnerEmail) && !System.Net.Mail.MailAddress.TryCreate(form.OwnerEmail, out _)) return BadRequest(new { message = "ელფოსტა არასწორია." });
        var photos = form.Photos ?? [];
        if (photos.Count is < 1 or > 15 || photos.Any(x => x.Length is <= 0 or > 8 * 1024 * 1024 || !new[] { "image/jpeg", "image/png", "image/webp" }.Contains(x.ContentType.ToLowerInvariant())))
            return BadRequest(new { message = "ატვირთეთ 1–15 JPG, PNG ან WebP ფოტო, თითოეული მაქსიმუმ 8 მბ." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key.ToString()}, 0))", ct);
        var existing = await db.OwnerSubmissions.FirstOrDefaultAsync(x => x.SubmissionKey == key, ct);
        if (existing is not null) return Ok(new { existing.Id, existing.Status });
        // Pick an active reviewing account, preferring agents and balancing pending work.
        var reviewer = await db.Users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && (r.NormalizedName == "AGENT" || r.NormalizedName == "MANAGER" || r.NormalizedName == "ADMIN"))))
            .OrderBy(u => db.OwnerSubmissions.Count(s => s.AgentUserId == u.Id && s.Status == "new"))
            .ThenBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (reviewer is null) return StatusCode(503, new { message = "აგენტი დროებით მიუწვდომელია. სცადეთ მოგვიანებით." });
        var paths = new List<string>();
        try
        {
            foreach (var photo in photos)
            {
                var path = await storage.UploadImageAsync(photo, "owner-submissions", ct);
                if (path is null) throw new InvalidOperationException("Photo upload failed.");
                paths.Add(path);
            }
            var apartment = new Apartment { IsApproved = false, UploadedByUserId = reviewer };
            data.Apply(apartment, form.OwnerName, form.OwnerPhone);
            apartment.ImageUrl = paths[0];
            apartment.Images = paths.Select((p, i) => new ApartmentImage { StoragePath = p, SortOrder = i, IsCover = i == 0 }).ToList();
            db.Apartments.Add(apartment); await db.SaveChangesAsync(ct);
            var row = new OwnerSubmission { IsGeneralSubmission = true, SubmissionKey = key, AgentUserId = reviewer,
                DealType = data.DealType == "For Rent" ? "Rent" : "Sale", OwnerName = form.OwnerName.Trim(), OwnerPhone = form.OwnerPhone.Trim(), OwnerEmail = form.OwnerEmail?.Trim(),
                DataJson = JsonSerializer.Serialize(data, SubmissionProperty.Json), PhotoPathsJson = JsonSerializer.Serialize(paths), PublishedApartmentId = apartment.Id };
            db.OwnerSubmissions.Add(row); workflow.History(row, "submitted"); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return Ok(new { row.Id, row.Status });
        }
        catch
        {
            foreach (var path in paths) { try { await storage.DeleteImageAsync(path, CancellationToken.None); } catch { /* Cleanup may be retried through storage administration. */ } }
            throw;
        }
    }

    [Authorize(Roles = ReviewRoles), HttpPost("submissions/{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var row = await Locked(id, ct);
        if (row is null) return NotFound();
        if (row.Status is "sent" or "rejected") return Conflict(new { message = "განაცხადის სტატუსი უკვე შეცვლილია." });
        if (!ApartmentSubmissionService.Verified(row.VerificationJson) || row.Status != "contacted" && row.Status != "visited" && row.Status != "approved")
            return BadRequest(new { message = "ჯერ დაუკავშირდით მესაკუთრეს და დაასრულეთ შემოწმება." });
        if (row.Status != "approved") { row.Status = "approved"; row.ApprovedAt = DateTime.UtcNow; row.ApprovedByUserId = UserId; row.UpdatedAt = DateTime.UtcNow; workflow.History(row, "approved", UserId); }
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Ok(new { row.Status });
    }

    [Authorize(Roles = ReviewRoles), HttpPost("submissions/{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var row = await Locked(id, ct); if (row is null) return NotFound();
        if (row.Status == "sent") return Conflict(new { message = "გაგზავნილი განაცხადის უარყოფა შეუძლებელია." });
        if (row.Status != "rejected") { row.Status = "rejected"; row.UpdatedAt = DateTime.UtcNow; workflow.History(row, "rejected", UserId); }
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Ok(new { row.Status });
    }

    [Authorize(Roles = ReviewRoles), HttpPost("submissions/{id:long}/send")]
    public async Task<IActionResult> Send(long id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var row = await Locked(id, ct); if (row is null) return NotFound();
        var previous = await db.SubmissionEvents.AsNoTracking().FirstOrDefaultAsync(x => x.OwnerSubmissionId == id && x.Action == "sent", ct);
        if (previous is not null) return Ok(new { status = "sent", previous.MatchedClients, sentAt = previous.CreatedAt, alreadySent = true });
        if (row.Status != "approved" || !ApartmentSubmissionService.Verified(row.VerificationJson)) return Conflict(new { message = "გაგზავნამდე საჭიროა განაცხადის შემოწმება და დამტკიცება." });
        var count = await workflow.DistributeAsync(row, UserId, ct);
        await transaction.CommitAsync(ct); await cache.EvictByTagAsync("apartments", ct);
        return Ok(new { status = row.Status, matchedClients = count, sentAt = row.UpdatedAt, alreadySent = false });
    }

    private async Task<OwnerSubmission?> Locked(long id, CancellationToken ct) => await db.OwnerSubmissions
        .FromSqlInterpolated($"SELECT * FROM \"OwnerSubmissions\" WHERE \"Id\" = {id} FOR UPDATE")
        .FirstOrDefaultAsync(x => x.IsGeneralSubmission && (Elevated || x.AgentUserId == UserId), ct);

    [Authorize(Roles = ReviewRoles), HttpGet("matches")]
    public async Task<IActionResult> Matches(CancellationToken ct) => Ok(await db.SubmissionClientMatches.AsNoTracking()
        .Where(x => x.ClientProfile.AgentUserId == UserId && x.ClientProfile.Status == "Active" && x.Submission.Status == "sent")
        .OrderByDescending(x => x.CreatedAt).Take(200)
        .Select(x => new { x.Id, SubmissionId = x.OwnerSubmissionId, ClientId = x.ClientProfileId, x.CreatedAt,
            ApartmentId = x.Submission.PublishedApartmentId, x.Submission.DataJson }).ToListAsync(ct));
}

public sealed class GeneralSubmissionForm : OwnerSubmissionForm
{
    public string RequestId { get; set; } = "";
}
