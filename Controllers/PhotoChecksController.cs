using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;

namespace Website_API.Controllers;

[ApiController]
[Authorize]
[Route("api/image-processing")]
public class PhotoChecksController(AppDbContext context, ImageProcessingQueue queue,
    OpenAIImageEditingService editor, IImageEditingGuard guard, SupabaseStorageService storage,
    IOptions<ImageAiOptions> options, ILogger<PhotoChecksController> logger) : ControllerBase
{
    [HttpGet("availability")]
    public IActionResult Availability() => Ok(new
    {
        enabled = options.Value.Enabled && editor.Configured && User.IsInRole("Admin"),
        canRemove = options.Value.Enabled && editor.Configured && User.IsInRole("Admin")
    });

    [HttpPost("check")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("ImageChecks")]
    [RequestSizeLimit(17 * 1024 * 1024)]
    public async Task<IActionResult> Check(IFormFile image, [FromForm] string mode = "clean",
        [FromForm] bool rightsConfirmed = false, [FromForm] string authorizationBasis = "owned",
        [FromForm] string? permissionReference = null, [FromForm] string? instructions = null,
        IFormFile? mask = null, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        if (!options.Value.Enabled || !editor.Configured) return StatusCode(503, new { message = "Image editing is disabled or unconfigured." });
        if (!rightsConfirmed || authorizationBasis is not ("owned" or "licensed") ||
            (authorizationBasis == "licensed" && string.IsNullOrWhiteSpace(permissionReference)))
            return BadRequest(new { message = "Confirm ownership or provide a reference to explicit editing permission." });
        if (mode is not ("clean" or "enhance" or "objects")) return BadRequest(new { message = "Choose clean, enhance or objects. Automatic scans are disabled." });
        if (image is null || image.Length is 0 or > 8 * 1024 * 1024 || (mask?.Length ?? 0) > 8 * 1024 * 1024 || instructions?.Length > 1000 || permissionReference?.Length > 500)
            return BadRequest(new { message = "Use images up to 8 MB and concise editing instructions." });
        await using var source = new MemoryStream();
        await image.CopyToAsync(source, cancellationToken);
        var bytes = source.ToArray();
        byte[]? maskBytes = null;
        if (mask is not null)
        {
            await using var maskStream = new MemoryStream();
            await mask.CopyToAsync(maskStream, cancellationToken);
            maskBytes = maskStream.ToArray();
        }
        try { OpenAIImageEditingService.ValidateImage(bytes); if (maskBytes is not null) OpenAIImageEditingService.ValidateImage(maskBytes, true); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        await using var lease = await guard.TryEnterAsync(cancellationToken);
        if (lease is null) return StatusCode(429, new { message = "Another image is being edited. Wait before trying again." });
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();
        var version = editor.CacheVersion;
        // User, image, mask, operation, rights and configuration all participate in duplicate prevention.
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            Convert.ToHexStringLower(SHA256.HashData(bytes)) + ":" + (maskBytes is null ? "" : Convert.ToHexStringLower(SHA256.HashData(maskBytes))) + ":" + mode + ":" + instructions + ":" + authorizationBasis + ":" + permissionReference + ":" + version)));
        var previous = await context.ImageProcessingJobs.AsNoTracking().FirstOrDefaultAsync(j =>
            j.SourceSha256 == fingerprint && j.RequestedByUserId == userId && j.PipelineVersion == version, cancellationToken);
        if (previous is not null)
        {
            if (previous.ProcessedStoragePath is null) return Conflict(new { message = "This edit was already attempted. Review its status; failed or interrupted calls may have been billed." });
            var cached = await storage.DownloadAsync(previous.ProcessedStoragePath, cancellationToken);
            return Ok(new { classification = "authorized_edit", action = "processed", cleanedImage = Convert.ToBase64String(cached), contentType = "image/png", jobId = previous.Id });
        }
        var day = DateTime.UtcNow.Date;
        if (await context.ImageProcessingJobs.CountAsync(j => j.RequestedByUserId == userId && j.StartedAt >= day, cancellationToken) >= options.Value.DailyUserLimit)
            return StatusCode(429, new { message = "Daily image editing limit reached." });
        var budget = await queue.GetBudgetAsync(cancellationToken);
        if (budget.Exhausted || budget.SpentUsd + options.Value.EstimatedRequestUsd > options.Value.BudgetCutoffUsd)
            return StatusCode(503, new { message = "Image editing is paused by the application budget." });
        // Persist originals and reserve estimated spend BEFORE any billable call. Failed calls keep reservations.
        var originalPath = await storage.UploadBytesAsync(bytes, OpenAIImageEditingService.ValidateImage(bytes), "image-edit-originals", cancellationToken);
        var job = new ImageProcessingJob
        {
            SourceStoragePath = originalPath, SourceSha256 = fingerprint, PipelineVersion = version,
            Mode = mode, RequestedByUserId = userId, PermissionReference = permissionReference,
            AuthorizationBasis = authorizationBasis == "owned" ? ImageAuthorizationBasis.Owned : ImageAuthorizationBasis.Licensed,
            Status = ImageJobStatus.Running, Attempts = 1, StartedAt = DateTime.UtcNow,
            EstimatedCostUsd = options.Value.EstimatedRequestUsd
        };
        context.ImageProcessingJobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Reserved estimated USD {Cost} for image edit {JobId}, model {Model}.", job.EstimatedCostUsd, job.Id, options.Value.Model);
        try
        {
            var result = await editor.ProcessAsync(new ImageEditRequest
            {
                ImageB64 = Convert.ToBase64String(bytes), MaskB64 = maskBytes is null ? null : Convert.ToBase64String(maskBytes),
                Mode = mode == "clean" ? "process" : mode, AuthorizationBasis = authorizationBasis,
                PermissionReference = permissionReference, Instructions = instructions
            }, cancellationToken);
            job.ProcessedStoragePath = await storage.UploadBytesAsync(Convert.FromBase64String(result.ProcessedB64!), "image/png", "image-edit-drafts", cancellationToken);
            job.ProcessedSha256 = result.ProcessedSha256;
            job.SafetyReportJson = System.Text.Json.JsonSerializer.Serialize(new { provider = "openai", model = options.Value.Model, size = options.Value.Size, quality = options.Value.Quality, usage = result.UsageJson, estimateOnly = true });
            job.Status = ImageJobStatus.AwaitingApproval;
            job.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken.None);
            logger.LogInformation("OpenAI image edit {JobId}, model {Model}, quality {Quality}, size {Size}, estimated USD {Cost}", job.Id, options.Value.Model, options.Value.Quality, options.Value.Size, job.EstimatedCostUsd);
            return Ok(new { classification = "authorized_edit", action = "processed", cleanedImage = result.ProcessedB64, contentType = "image/png", jobId = job.Id });
        }
        catch (Exception e)
        {
            job.Status = ImageJobStatus.Failed;
            job.LastError = e is ArgumentException ? e.Message : "Editing failed or timed out. No automatic retry; the request may have been billed.";
            job.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken.None);
            logger.LogWarning("Image edit {JobId} failed ({Type}); reservation retained.", job.Id, e.GetType().Name);
            return StatusCode(e is ArgumentException ? 400 : e is OperationCanceledException ? 504 : 502, new { message = job.LastError });
        }
    }
}
