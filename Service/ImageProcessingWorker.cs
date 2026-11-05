using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Website_API.Data;
using Website_API.Models;

namespace Website_API.Services;

// Only explicit admin jobs created by this pipeline run. Legacy work and interrupted requests
// are never resumed automatically because their provider call may already have been billed.
public sealed class ImageProcessingWorker(IServiceScopeFactory scopes, IOptions<ImageAiOptions> options,
    ILogger<ImageProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        while (!ct.IsCancellationRequested)
        {
            try { await RunOnceAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogWarning("Image worker failed ({Type}); no provider retry.", e.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollSeconds)), ct);
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var editor = scope.ServiceProvider.GetRequiredService<OpenAIImageEditingService>();
        if (!editor.Configured) return;
        await using var lease = await scope.ServiceProvider.GetRequiredService<IImageEditingGuard>().TryEnterAsync(ct);
        if (lease is null) return;
        var queue = scope.ServiceProvider.GetRequiredService<ImageProcessingQueue>();
        var reservation = OpenAIImageCostCalculator.Reservation(options.Value);
        if (reservation is null) return;
        var budget = await queue.GetBudgetAsync(ct);
        if (budget.Exhausted || budget.SpentUsd + reservation.Value > options.Value.BudgetCutoffUsd) return;
        var job = await db.ImageProcessingJobs.Where(j => j.Status == ImageJobStatus.Pending && j.Attempts == 0 &&
            j.PipelineVersion == editor.CacheVersion && (j.Mode == "process" || j.Mode == "enhance"))
            .OrderBy(j => j.Id).FirstOrDefaultAsync(ct);
        if (job is null) return;

        job.Status = ImageJobStatus.Running;
        job.Attempts = 1;
        job.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        try
        {
            if (job.AuthorizationBasis == ImageAuthorizationBasis.Unknown || job.ApartmentImageId is null)
                throw new ArgumentException("Only explicitly authorized listing images can be edited.");
            var storage = scope.ServiceProvider.GetRequiredService<SupabaseStorageService>();
            var bytes = await storage.DownloadAsync(job.SourceStoragePath, ct);
            OpenAIImageEditingService.ValidateImage(bytes);
            job.SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var previous = await db.ImageProcessingJobs.AsNoTracking().Where(j => j.Id != job.Id &&
                j.SourceSha256 == job.SourceSha256 && j.Mode == job.Mode && j.AuthorizationBasis == job.AuthorizationBasis &&
                j.RequestedByUserId == job.RequestedByUserId && j.PipelineVersion == editor.CacheVersion && j.Attempts > 0)
                .OrderByDescending(j => j.Id).FirstOrDefaultAsync(ct);
            if (previous is not null)
            {
                if (previous.ProcessedStoragePath is null) throw new InvalidOperationException("A previous attempt may have been billed. Duplicate processing blocked.");
                job.ProcessedStoragePath = previous.ProcessedStoragePath;
                job.ProcessedSha256 = previous.ProcessedSha256;
                job.DuplicateOfJobId = previous.Id;
            }
            else
            {
                job.EstimatedCostUsd = reservation.Value;
                await db.SaveChangesAsync(ct); // Reserve spend before sending to OpenAI.
                logger.LogInformation("Reserved estimated USD {Cost} for image job {Id}, model {Model}.", job.EstimatedCostUsd, job.Id, options.Value.Model);
                var result = await editor.ProcessAsync(new ImageEditRequest
                {
                    ImageB64 = Convert.ToBase64String(bytes), Mode = job.Mode,
                    AuthorizationBasis = job.AuthorizationBasis.ToString().ToLowerInvariant(),
                    PermissionReference = job.PermissionReference
                }, ct, async accounting =>
                {
                    job.SafetyReportJson = accounting.ToMetadataJson();
                    if (accounting.Cost is not null) job.EstimatedCostUsd = accounting.Cost.TotalUsd;
                    await db.SaveChangesAsync(CancellationToken.None);
                });
                job.ProcessedStoragePath = await storage.UploadBytesAsync(Convert.FromBase64String(result.ProcessedB64!), "image/png", "processed", ct);
                job.ProcessedSha256 = result.ProcessedSha256;
            }
            job.Status = ImageJobStatus.AwaitingApproval;
            logger.LogInformation("Image job {Id}, model {Model}, size {Size}, quality {Quality}, estimated USD {Cost}; awaiting approval.", job.Id, options.Value.Model, options.Value.Size, options.Value.Quality, job.EstimatedCostUsd);
        }
        catch (Exception e)
        {
            job.Status = ImageJobStatus.Failed;
            job.LastError = e is ArgumentException ? e.Message : "Processing failed or was interrupted. No automatic retry; estimated spend reservation retained.";
            logger.LogWarning("Image job {Id} failed ({Type}); no retry.", job.Id, e.GetType().Name);
        }
        job.LockedUntil = null;
        job.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
