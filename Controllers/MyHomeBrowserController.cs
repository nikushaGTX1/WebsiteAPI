using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Website_API.Data;
using Website_API.Models;
namespace Website_API.Controllers;

[ApiController, Route("api/MyHomeVerification/jobs/{id:guid}/browser"), Authorize(Roles = "Admin,Manager")]
public class MyHomeBrowserController(AppDbContext db) : ControllerBase
{
    [HttpPost("claim")]
    public async Task<IActionResult> Claim(Guid id, BrowserClaim request, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(78452119)", token);
        var job = await db.MyHomeJobs.FindAsync([id], token);
        if (job == null) return NotFound();
        if (job.Mode != "BROWSER") return BadRequest(new { message = "This job uses server verification." });
        if (job.State != "RUNNING") return Ok(new { done = true });
        if (job.BrowserLeaseExpiresAt > DateTime.UtcNow && !Owns(job, request.LeaseToken))
            return Conflict(new { message = "Another browser is checking this job. Retry after its lease expires." });
        if (!Owns(job, request.LeaseToken) || job.BrowserLeaseExpiresAt <= DateTime.UtcNow)
        {
            job.BrowserLeaseToken = Guid.NewGuid().ToString("N");
            job.BrowserUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        var next = await db.MyHomeChecks.Where(c => c.JobId == id && c.Status == "PENDING")
            .OrderBy(c => c.ApartmentId).Select(c => c.MyHomeId).FirstOrDefaultAsync(token);
        if (next == null)
        {
            job.State = "COMPLETED"; job.CompletedAt = DateTime.UtcNow; job.Error = null;
        }
        job.BrowserListingId = next;
        job.BrowserLeaseExpiresAt = DateTime.UtcNow.AddMinutes(2);
        job.Error = null;
        await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return Ok(new { done = next == null, leaseToken = job.BrowserLeaseToken, myHomeId = next });
    }
    [HttpPost("result")]
    public async Task<IActionResult> Result(Guid id, BrowserEvidence request, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(78452119)", token);
        var job = await db.MyHomeJobs.FindAsync([id], token);
        if (job == null) return NotFound();
        if (job.State != "RUNNING" || job.Mode != "BROWSER" || !Owns(job, request.LeaseToken) ||
            job.BrowserLeaseExpiresAt <= DateTime.UtcNow || job.BrowserListingId != request.MyHomeId)
            return Conflict(new { message = "The browser lease expired. Resume verification in the extension." });
        var status = Classify(request);
        var now = DateTime.UtcNow;
        var ids = await db.MyHomeChecks.Where(c => c.JobId == id && c.MyHomeId == request.MyHomeId && c.Status == "PENDING").Select(c => c.ApartmentId).ToListAsync(token);
        var detail = "Browser verification: " + request.Evidence;
        await db.MyHomeChecks.Where(c => c.JobId == id && c.MyHomeId == request.MyHomeId && c.Status == "PENDING")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, status).SetProperty(c => c.CheckedAt, now).SetProperty(c => c.Detail, detail), token);
        await db.Apartments.Where(a => ids.Contains(a.Id) && a.MyHomeId == request.MyHomeId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.MyHomeStatus, status).SetProperty(a => a.MyHomeCheckedAt, now), token);
        job.BrowserListingId = null;
        if (!await db.MyHomeChecks.AnyAsync(c => c.JobId == id && c.Status == "PENDING", token))
        { job.State = "COMPLETED"; job.CompletedAt = now; }
        await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return Ok(new { status, done = job.State == "COMPLETED" });
    }
    [HttpPost("pause")]
    public async Task<IActionResult> Pause(Guid id, BrowserClaim request, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(78452119)", token);
        var job = await db.MyHomeJobs.FindAsync([id], token);
        if (job == null) return NotFound();
        if (job.Mode != "BROWSER" || job.State != "RUNNING" || !Owns(job, request.LeaseToken) || job.BrowserLeaseExpiresAt <= DateTime.UtcNow) return Conflict();
        job.Error = "Browser verification paused. Complete any MyHome challenge, then resume in the Nikas extension.";
        job.BrowserLeaseExpiresAt = DateTime.UtcNow;
        await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return Ok();
    }
    private bool Owns(MyHomeJob job, string? lease) => !string.IsNullOrEmpty(lease) && job.BrowserLeaseToken == lease && job.BrowserUserId == User.FindFirstValue(ClaimTypes.NameIdentifier);
    public static string Classify(BrowserEvidence evidence)
    {
        if (evidence.Status == "ERROR") return "ERROR";
        if (!Uri.TryCreate(evidence.PageUrl, UriKind.Absolute, out var url) || url.Scheme != "https" ||
            url.Host != "www.myhome.ge" && url.Host != "myhome.ge" ||
            !(Regex.IsMatch(url.AbsolutePath, $@"/(?:ka/|en/|ru/)?pr/{Regex.Escape(evidence.MyHomeId)}(?:/|$)") ||
              Regex.IsMatch(url.AbsolutePath, $@"^/udzravi-qoneba/[^/]*-{Regex.Escape(evidence.MyHomeId)}/?$"))) return "UNCERTAIN";
        if (evidence.Status == "REMOVED" && evidence.Evidence == "DELETED_MESSAGE" &&
            evidence.VisibleExcerpt.Contains("განცხადება რომელსაც ეძებდით წაშლილი ან არააქტიურია")) return "REMOVED";
        if (evidence.Status == "ACTIVE" && evidence.DetectedId == evidence.MyHomeId && evidence.Evidence is "HYDRATED_STATEMENT" or "VISIBLE_LISTING_ID") return "ACTIVE";
        return "UNCERTAIN";
    }
}
public class BrowserClaim { [MaxLength(64)] public string? LeaseToken { get; set; } }
public class BrowserEvidence : BrowserClaim
{
    [Required, RegularExpression(@"^[1-9][0-9]{4,7}$")] public string MyHomeId { get; set; } = "";
    [Required, RegularExpression("^(ACTIVE|REMOVED|UNCERTAIN|ERROR)$")] public string Status { get; set; } = "UNCERTAIN";
    [Required, MaxLength(80)] public string Evidence { get; set; } = "NO_EVIDENCE";
    [MaxLength(2048)] public string PageUrl { get; set; } = "";
    [MaxLength(8)] public string? DetectedId { get; set; }
    [MaxLength(500)] public string VisibleExcerpt { get; set; } = "";
}
