using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

// Watermark checks for the apartment upload form. Any signed-in uploader may use them.
// Nothing is stored: the form receives the result and decides what to upload.
[ApiController]
[Authorize]
[Route("api/image-processing")]
public class PhotoChecksController : ControllerBase
{
    private const long MaxBytes = 8 * 1024 * 1024;

    private readonly AppDbContext _context;
    private readonly ImageProcessingQueue _queue;
    private readonly ModalImageAiClient _modal;
    private readonly ImageAiOptions _options;
    private readonly ILogger<PhotoChecksController> _logger;

    public PhotoChecksController(
        AppDbContext context,
        ImageProcessingQueue queue,
        ModalImageAiClient modal,
        IOptions<ImageAiOptions> options,
        ILogger<PhotoChecksController> logger)
    {
        _context = context;
        _queue = queue;
        _modal = modal;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet("availability")]
    public IActionResult Availability() => Ok(new { enabled = _options.Enabled });

    // mode=detect scans only. mode=clean also removes the watermark, but only our own
    // brand, and only when the uploader confirms they may edit the photo.
    [HttpPost("check")]
    [EnableRateLimiting("ImageChecks")]
    [RequestSizeLimit(MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Check(
        IFormFile image,
        [FromForm] string mode = "detect",
        [FromForm] bool rightsConfirmed = false,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Photo check is turned off." });
        }

        if (mode is not ("detect" or "clean"))
        {
            return BadRequest(new { message = "Mode must be detect or clean." });
        }

        if (image is null || image.Length == 0 || image.Length > MaxBytes ||
            image.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            return BadRequest(new { message = "Send one JPG, PNG or WebP image up to 8 MB." });
        }

        if (mode == "clean" && !rightsConfirmed)
        {
            return BadRequest(new { message = "Confirm that you may edit this photo first." });
        }

        var budget = await _queue.GetBudgetAsync(cancellationToken);
        if (budget.Exhausted)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Photo check is paused for this month." });
        }

        await using var stream = new MemoryStream();
        await image.CopyToAsync(stream, cancellationToken);
        var bytes = stream.ToArray();
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var basis = mode == "clean" ? ImageAuthorizationBasis.Owned : ImageAuthorizationBasis.Unknown;
        var modalMode = mode == "clean" ? "process" : "detect";

        // Same photo scanned before: reuse the answer without paying for the GPU.
        if (mode == "detect")
        {
            var previous = await _context.ImageProcessingJobs.AsNoTracking()
                .Where(job => job.SourceSha256 == sha && job.Mode == "detect" &&
                              job.Classification != null)
                .OrderByDescending(job => job.Id)
                .Select(job => new { job.Classification, job.WatermarkText })
                .FirstOrDefaultAsync(cancellationToken);
            if (previous is not null)
            {
                return Ok(new { classification = previous.Classification, watermarkText = previous.WatermarkText, action = "cached" });
            }
        }

        var job = new ImageProcessingJob
        {
            SourceStoragePath = ImageProcessingJob.UploadFormSource,
            SourceSha256 = sha,
            Mode = modalMode,
            AuthorizationBasis = basis,
            RequestedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Status = ImageJobStatus.Running,
            Attempts = 1,
            StartedAt = DateTime.UtcNow
        };

        ModalProcessResult result;
        try
        {
            result = await _modal.ProcessAsync(
                new ModalProcessRequest
                {
                    ImageB64 = Convert.ToBase64String(bytes),
                    ExpectedSha256 = sha,
                    Mode = modalMode,
                    AuthorizationBasis = basis.ToString().ToLowerInvariant(),
                    OwnBrandTerms = _options.OwnBrandTerms
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Upload photo check failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Photo check failed. You can still upload the photo." });
        }

        var billedSeconds = result.GpuSeconds + (result.ColdStart ? _options.IdleSecondsPerColdStart : 0);
        job.GpuSeconds = billedSeconds;
        job.EstimatedCostUsd = (decimal)billedSeconds * _options.GpuUsdPerSecond;
        job.Classification = result.Classification;
        job.DetectionsJson = result.Detections.GetRawText();
        job.WatermarkText = string.Join(" | ", result.Detections.EnumerateArray()
            .Select(item => item.TryGetProperty("text", out var text) ? text.GetString() : null)
            .Where(text => !string.IsNullOrWhiteSpace(text)));
        if (job.WatermarkText.Length == 0) job.WatermarkText = null;
        else if (job.WatermarkText.Length > 500) job.WatermarkText = job.WatermarkText[..500];
        job.Status = result.Action switch
        {
            "no_watermark" => ImageJobStatus.NoWatermark,
            "needs_clean_original" => ImageJobStatus.NeedsCleanOriginal,
            "processed" => ImageJobStatus.Approved,
            "validation_failed" => ImageJobStatus.Failed,
            _ => ImageJobStatus.DetectedOnly
        };
        job.CompletedAt = DateTime.UtcNow;
        job.LockedUntil = null;
        _context.ImageProcessingJobs.Add(job);
        await _context.SaveChangesAsync(CancellationToken.None);

        string? cleaned = null;
        if (result.Action == "processed" && result.ProcessedB64 is not null)
        {
            var processed = Convert.FromBase64String(result.ProcessedB64);
            if (Convert.ToHexStringLower(SHA256.HashData(processed)) == result.ProcessedSha256)
            {
                cleaned = result.ProcessedB64;
            }
        }

        return Ok(new
        {
            classification = result.Classification,
            watermarkText = job.WatermarkText,
            action = cleaned is null && result.Action == "processed" ? "validation_failed" : result.Action,
            cleanedImage = cleaned,
            contentType = result.ContentType
        });
    }
}
