using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

// Admin-only review queue for AI image processing. Disabled unless ImageAi__Enabled=true.
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/image-processing")]
public class ImageProcessingController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ImageProcessingQueue _queue;
    private readonly SupabaseStorageService _storageService;
    private readonly IOutputCacheStore _outputCache;

    public ImageProcessingController(
        AppDbContext context,
        ImageProcessingQueue queue,
        SupabaseStorageService storageService,
        IOutputCacheStore outputCache)
    {
        _context = context;
        _queue = queue;
        _storageService = storageService;
        _outputCache = outputCache;
    }

    public sealed record EnqueueRequest(
        int[]? ApartmentImageIds,
        int? ApartmentId,
        string Mode = "detect",
        ImageAuthorizationBasis AuthorizationBasis = ImageAuthorizationBasis.Unknown,
        string? PermissionReference = null);

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var budget = await _queue.GetBudgetAsync(cancellationToken);
        var counts = await _context.ImageProcessingJobs
            .GroupBy(job => job.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            budget,
            counts = counts.ToDictionary(item => item.Status.ToString(), item => item.Count)
        });
    }

    [HttpPost("jobs")]
    public async Task<IActionResult> Enqueue(
        EnqueueRequest request,
        CancellationToken cancellationToken)
    {
        if (!_queue.Enabled)
        {
            return Conflict(new { message = "Image AI is disabled." });
        }

        if (request.Mode is not ("detect" or "process"))
        {
            return BadRequest(new { message = "Mode must be detect or process." });
        }

        if (request.Mode == "process" &&
            request.AuthorizationBasis == ImageAuthorizationBasis.Unknown)
        {
            return BadRequest(new
            {
                message = "Processing needs authorizationBasis Owned or Licensed."
            });
        }

        if (request.AuthorizationBasis == ImageAuthorizationBasis.Licensed &&
            string.IsNullOrWhiteSpace(request.PermissionReference))
        {
            return BadRequest(new
            {
                message = "Licensed images need a permissionReference (licence or written consent)."
            });
        }

        var query = _context.ApartmentImages.AsQueryable();
        if (request.ApartmentImageIds is { Length: > 0 })
        {
            query = query.Where(image => request.ApartmentImageIds.Contains(image.Id));
        }
        else if (request.ApartmentId is not null)
        {
            query = query.Where(image => image.ApartmentId == request.ApartmentId);
        }
        else
        {
            return BadRequest(new { message = "Give apartmentImageIds or apartmentId." });
        }

        var images = await query.Take(200).ToListAsync(cancellationToken);
        var jobs = await _queue.EnqueueAsync(
            images,
            request.Mode,
            request.AuthorizationBasis,
            request.PermissionReference?.Trim(),
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);

        return Accepted(new { queued = jobs.Count, jobIds = jobs.Select(job => job.Id) });
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> List(
        [FromQuery] ImageJobStatus? status,
        [FromQuery] int? apartmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.ImageProcessingJobs.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(job => job.Status == status);
        }

        if (apartmentId is not null)
        {
            query = query.Where(job => job.ApartmentId == apartmentId);
        }

        var jobs = await query
            .OrderByDescending(job => job.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<object>(jobs.Count);
        foreach (var job in jobs)
        {
            items.Add(new
            {
                job.Id,
                job.ApartmentId,
                job.ApartmentImageId,
                Status = job.Status.ToString(),
                job.Mode,
                AuthorizationBasis = job.AuthorizationBasis.ToString(),
                job.Classification,
                job.WatermarkText,
                job.DuplicateOfJobId,
                job.Attempts,
                job.LastError,
                job.GpuSeconds,
                job.EstimatedCostUsd,
                job.CreatedAt,
                job.CompletedAt,
                job.CleanOriginalRequestedAt,
                OriginalUrl = await _storageService.CreateSignedUrlAsync(
                    job.SourceStoragePath,
                    cancellationToken: cancellationToken),
                ProcessedUrl = await _storageService.CreateSignedUrlAsync(
                    job.ProcessedStoragePath,
                    cancellationToken: cancellationToken)
            });
        }

        return Ok(items);
    }

    [HttpPost("jobs/{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken cancellationToken)
    {
        var job = await _context.ImageProcessingJobs
            .Include(item => item.ApartmentImage)
            .ThenInclude(image => image!.Apartment)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (job is null)
        {
            return NotFound();
        }

        if (job.Status != ImageJobStatus.AwaitingApproval ||
            job.ProcessedStoragePath is null ||
            job.ApartmentImage is null)
        {
            return Conflict(new { message = "Only processed jobs awaiting approval can be approved." });
        }

        var image = job.ApartmentImage;
        image.OriginalStoragePath ??= image.StoragePath;
        image.StoragePath = job.ProcessedStoragePath;
        if (image.IsCover)
        {
            image.Apartment.ImageUrl = image.StoragePath;
        }

        job.Status = ImageJobStatus.Approved;
        job.ReviewedAt = DateTime.UtcNow;
        job.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _context.SaveChangesAsync(cancellationToken);
        await _outputCache.EvictByTagAsync("apartments", cancellationToken);

        return Ok(new { message = "Processed image is now live. Original kept." });
    }

    [HttpPost("jobs/{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        return await Review(id, ImageJobStatus.Rejected, cancellationToken);
    }

    // Restores the original photo for an image whose processed version is live.
    [HttpPost("jobs/{id:long}/revert")]
    public async Task<IActionResult> Revert(long id, CancellationToken cancellationToken)
    {
        var job = await _context.ImageProcessingJobs
            .Include(item => item.ApartmentImage)
            .ThenInclude(image => image!.Apartment)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (job?.ApartmentImage?.OriginalStoragePath is null ||
            job.Status != ImageJobStatus.Approved)
        {
            return Conflict(new { message = "Nothing to revert." });
        }

        var image = job.ApartmentImage;
        image.StoragePath = image.OriginalStoragePath;
        image.OriginalStoragePath = null;
        if (image.IsCover)
        {
            image.Apartment.ImageUrl = image.StoragePath;
        }

        job.Status = ImageJobStatus.Rejected;
        job.ReviewedAt = DateTime.UtcNow;
        job.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _context.SaveChangesAsync(cancellationToken);
        await _outputCache.EvictByTagAsync("apartments", cancellationToken);

        return Ok(new { message = "Original image restored." });
    }

    [HttpPost("jobs/{id:long}/retry")]
    public async Task<IActionResult> Retry(long id, CancellationToken cancellationToken)
    {
        var updated = await _context.ImageProcessingJobs
            .Where(job => job.Id == id && job.Status == ImageJobStatus.Failed)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Status, ImageJobStatus.Pending)
                    .SetProperty(job => job.Attempts, 0)
                    .SetProperty(job => job.NextAttemptAt, DateTime.UtcNow),
                cancellationToken);

        return updated == 1 ? Ok() : Conflict(new { message = "Only failed jobs can be retried." });
    }

    // Records that the listing owner was asked for an unwatermarked original.
    [HttpPost("jobs/{id:long}/clean-original-requested")]
    public async Task<IActionResult> CleanOriginalRequested(
        long id,
        CancellationToken cancellationToken)
    {
        var updated = await _context.ImageProcessingJobs
            .Where(job => job.Id == id && job.Status == ImageJobStatus.NeedsCleanOriginal)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    job => job.CleanOriginalRequestedAt,
                    DateTime.UtcNow),
                cancellationToken);

        return updated == 1 ? Ok() : NotFound();
    }

    private async Task<IActionResult> Review(
        long id,
        ImageJobStatus status,
        CancellationToken cancellationToken)
    {
        var job = await _context.ImageProcessingJobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        if (job.Status != ImageJobStatus.AwaitingApproval)
        {
            return Conflict(new { message = "Job is not awaiting approval." });
        }

        job.Status = status;
        job.ReviewedAt = DateTime.UtcNow;
        job.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
