using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;
namespace Website_API.Controllers;
[ApiController, Route("api/MyHomeVerification"), Authorize(Roles = "Admin,Manager")]
public class MyHomeVerificationController(AppDbContext db, SupabaseStorageService storage) : ControllerBase
{
    [HttpPost("jobs")]
    public async Task<IActionResult> Start(CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, token);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(78452119)", token);
        var existing = await db.MyHomeJobs.FirstOrDefaultAsync(j => j.State == "RUNNING", token);
        if (existing != null) return Ok(existing);
        var job = new MyHomeJob { RequestedBy = User.FindFirstValue(ClaimTypes.NameIdentifier) };
        db.MyHomeJobs.Add(job);
        await db.SaveChangesAsync(token);
        // SQL snapshot includes all apartments, including unpublished records, without loading them into memory.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "MyHomeChecks" ("JobId", "ApartmentId", "MyHomeId", "Address", "ImageUrl", "Status")
            SELECT {job.Id}, "Id", COALESCE("MyHomeId", ''), "Address", "ImageUrl",
                CASE WHEN "MyHomeId" IS NULL OR "MyHomeId" = '' THEN 'NO_MYHOME_ID' ELSE 'PENDING' END
            FROM "Apartments"
            """, token);
        job.TotalApartments = await db.MyHomeChecks.CountAsync(c => c.JobId == job.Id, token);
        job.MissingIds = await db.MyHomeChecks.CountAsync(c => c.JobId == job.Id && c.Status == "NO_MYHOME_ID", token);
        await db.Apartments.Where(a => a.MyHomeId == null || a.MyHomeId == "").ExecuteUpdateAsync(s => s.SetProperty(a => a.MyHomeStatus, "NO_MYHOME_ID"), token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Accepted(job);
    }
    [HttpGet("latest")]
    public async Task<IActionResult> Latest(CancellationToken token)
    {
        var job = await db.MyHomeJobs.AsNoTracking().OrderByDescending(j => j.CreatedAt).FirstOrDefaultAsync(token);
        if (job == null) return Ok(new { job = (object?)null });
        var counts = await db.MyHomeChecks.Where(c => c.JobId == job.Id).GroupBy(c => c.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(token);
        return Ok(new { job, counts });
    }
    [HttpGet("jobs/{id:guid}/results")]
    public async Task<IActionResult> Results(Guid id, string? status, string? search, int page = 1, CancellationToken token = default)
    {
        var query = db.MyHomeChecks.AsNoTracking().Where(c => c.JobId == id);
        if (!string.IsNullOrWhiteSpace(status) && status != "ALL") query = query.Where(c => c.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) {
            var term = search.Trim();
            if (term.Length > 200) return BadRequest(new { message = "Search must be 200 characters or fewer." });
            var pattern = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(c => (c.Address != null && EF.Functions.ILike(c.Address, pattern)) || c.MyHomeId.Contains(term) || c.ApartmentId.ToString() == term);
        }
        var total = await query.CountAsync(token);
        var items = await query.OrderBy(c => c.ApartmentId).Skip((Math.Clamp(page, 1, 100000) - 1) * 50).Take(50).ToListAsync(token);
        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token }, async (item, ct) => {
            item.ImageUrl = await storage.CreateSignedUrlAsync(item.ImageUrl, 3600, ct);
        });
        return Ok(new { total, items });
    }
}
