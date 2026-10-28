using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Website_API.Data;
using Website_API.Models;

namespace Website_API.Services;

// Polls the PostgreSQL job table. One job at a time matches the single Modal GPU.
public sealed class ImageProcessingWorker : BackgroundService
{
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ImageAiOptions _options;
    private readonly ILogger<ImageProcessingWorker> _logger;
    private bool _budgetWarningLogged;

    public ImageProcessingWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ImageAiOptions> options,
        ILogger<ImageProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Image AI worker disabled (ImageAi__Enabled=false).");
            return;
        }

        _logger.LogInformation("Image AI worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var worked = false;
            try
            {
                worked = await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Image AI worker loop failed.");
            }

            if (!worked)
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollSeconds), stoppingToken);
            }
        }
    }

    private async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<ImageProcessingQueue>();

        var budget = await queue.GetBudgetAsync(cancellationToken);
        if (budget.Exhausted)
        {
            if (!_budgetWarningLogged)
            {
                _logger.LogWarning(
                    "Image AI paused: {Images}/{Quota} images, ${Spent} of ${Cutoff} cutoff this month.",
                    budget.ImagesProcessed,
                    budget.MonthlyImageQuota,
                    budget.SpentUsd,
                    budget.BudgetCutoffUsd);
                _budgetWarningLogged = true;
            }

            return false;
        }

        _budgetWarningLogged = false;

        var job = await ClaimAsync(context, cancellationToken);
        if (job is null)
        {
            return false;
        }

        try
        {
            await ProcessAsync(scope.ServiceProvider, context, job, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException ||
                                          !cancellationToken.IsCancellationRequested)
        {
            var permanent = exception is ModalImageAiException { IsPermanent: true };
            var retry = !permanent && job.Attempts < _options.MaxAttempts;

            job.LastError = exception.Message.Length > 2000
                ? exception.Message[..2000]
                : exception.Message;
            job.LockedUntil = null;
            job.Status = retry ? ImageJobStatus.Pending : ImageJobStatus.Failed;
            job.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, job.Attempts));
            if (!retry)
            {
                job.CompletedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(CancellationToken.None);

            _logger.LogWarning(
                exception,
                "Image job {JobId} attempt {Attempt} failed; {Outcome}.",
                job.Id,
                job.Attempts,
                retry ? "will retry" : "giving up");
        }

        return true;
    }

    private static async Task<ImageProcessingJob?> ClaimAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Running jobs whose lock expired belong to a crashed or redeployed instance.
        var candidateId = await context.ImageProcessingJobs
            .Where(job =>
                (job.Status == ImageJobStatus.Pending && job.NextAttemptAt <= now) ||
                (job.Status == ImageJobStatus.Running && job.LockedUntil < now))
            .OrderBy(job => job.NextAttemptAt)
            .ThenBy(job => job.Id)
            .Select(job => (long?)job.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (candidateId is null)
        {
            return null;
        }

        var claimed = await context.ImageProcessingJobs
            .Where(job =>
                job.Id == candidateId &&
                ((job.Status == ImageJobStatus.Pending && job.NextAttemptAt <= now) ||
                 (job.Status == ImageJobStatus.Running && job.LockedUntil < now)))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Status, ImageJobStatus.Running)
                    .SetProperty(job => job.LockedUntil, now.Add(LockDuration))
                    .SetProperty(job => job.Attempts, job => job.Attempts + 1)
                    .SetProperty(job => job.StartedAt, now),
                cancellationToken);

        return claimed == 1
            ? await context.ImageProcessingJobs.FirstAsync(
                job => job.Id == candidateId,
                cancellationToken)
            : null;
    }

    private async Task ProcessAsync(
        IServiceProvider services,
        AppDbContext context,
        ImageProcessingJob job,
        CancellationToken cancellationToken)
    {
        var storage = services.GetRequiredService<SupabaseStorageService>();
        var modal = services.GetRequiredService<ModalImageAiClient>();

        if (job.ApartmentImageId is null)
        {
            throw new ModalImageAiException(410, "Apartment image was deleted.");
        }

        var bytes = await storage.DownloadAsync(job.SourceStoragePath, cancellationToken);
        job.SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));

        var duplicate = await context.ImageProcessingJobs
            .AsNoTracking()
            .Where(other =>
                other.Id != job.Id &&
                other.SourceSha256 == job.SourceSha256 &&
                other.Mode == job.Mode &&
                other.AuthorizationBasis == job.AuthorizationBasis &&
                other.DuplicateOfJobId == null &&
                other.SourceStoragePath != ImageProcessingJob.UploadFormSource &&
                other.Status != ImageJobStatus.Pending &&
                other.Status != ImageJobStatus.Running &&
                other.Status != ImageJobStatus.Failed)
            .OrderByDescending(other => other.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (duplicate is not null)
        {
            job.DuplicateOfJobId = duplicate.Id;
            job.Classification = duplicate.Classification;
            job.DetectionsJson = duplicate.DetectionsJson;
            job.WatermarkText = duplicate.WatermarkText;
            job.ProcessedStoragePath = duplicate.ProcessedStoragePath;
            job.ProcessedSha256 = duplicate.ProcessedSha256;
            job.Status = duplicate.ProcessedStoragePath is null
                ? duplicate.Status
                : ImageJobStatus.AwaitingApproval;
            Complete(job);
            await context.SaveChangesAsync(CancellationToken.None);

            _logger.LogInformation(
                "Image job {JobId} reused duplicate result from job {DuplicateId}.",
                job.Id,
                duplicate.Id);
            return;
        }

        var result = await modal.ProcessAsync(
            new ModalProcessRequest
            {
                ImageB64 = Convert.ToBase64String(bytes),
                ExpectedSha256 = job.SourceSha256,
                Mode = job.Mode,
                AuthorizationBasis = job.AuthorizationBasis.ToString().ToLowerInvariant(),
                PermissionReference = job.PermissionReference,
                OwnBrandTerms = _options.RemovableBrandTerms
            },
            cancellationToken);

        var billedSeconds = result.GpuSeconds +
                            (result.ColdStart ? _options.IdleSecondsPerColdStart : 0);
        job.GpuSeconds = billedSeconds;
        job.EstimatedCostUsd = (decimal)billedSeconds * _options.GpuUsdPerSecond;
        job.Classification = result.Classification;
        job.DetectionsJson = result.Detections.GetRawText();
        job.WatermarkText = ExtractText(result.Detections);

        job.Status = result.Action switch
        {
            "no_watermark" => ImageJobStatus.NoWatermark,
            "needs_clean_original" => ImageJobStatus.NeedsCleanOriginal,
            "detected_only" => ImageJobStatus.DetectedOnly,
            "processed" => ImageJobStatus.AwaitingApproval,
            _ => ImageJobStatus.Failed
        };

        if (job.Status == ImageJobStatus.AwaitingApproval)
        {
            var processed = Convert.FromBase64String(result.ProcessedB64 ?? string.Empty);
            var processedSha = Convert.ToHexStringLower(SHA256.HashData(processed));
            if (processed.Length == 0 || processedSha != result.ProcessedSha256)
            {
                throw new InvalidOperationException("Processed image failed checksum validation.");
            }

            job.ProcessedStoragePath = await storage.UploadBytesAsync(
                processed,
                result.ContentType ?? "image/jpeg",
                "processed",
                cancellationToken);
            job.ProcessedSha256 = processedSha;
        }
        else if (job.Status == ImageJobStatus.Failed)
        {
            job.LastError = $"Output validation failed: {result.Validation?.GetRawText()}";
        }

        Complete(job);
        await context.SaveChangesAsync(CancellationToken.None);

        _logger.LogInformation(
            "Image job {JobId} finished: {Status} ({Classification}), {GpuSeconds}s GPU, cold={Cold}.",
            job.Id,
            job.Status,
            job.Classification,
            Math.Round(billedSeconds, 2),
            result.ColdStart);
    }

    private static void Complete(ImageProcessingJob job)
    {
        job.LockedUntil = null;
        job.LastError = job.Status == ImageJobStatus.Failed ? job.LastError : null;
        job.CompletedAt = DateTime.UtcNow;
    }

    private static string? ExtractText(JsonElement detections)
    {
        if (detections.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var texts = detections.EnumerateArray()
            .Where(item => item.TryGetProperty("text", out _))
            .Select(item => item.GetProperty("text").GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        var joined = string.Join(" | ", texts);
        return joined.Length == 0 ? null : joined.Length > 500 ? joined[..500] : joined;
    }
}
