using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Website_API.Data;
using Website_API.Models;

namespace Website_API.Services;

public sealed class ImageProcessingQueue
{
    private readonly AppDbContext _context;
    private readonly ImageAiOptions _options;

    public ImageProcessingQueue(AppDbContext context, IOptions<ImageAiOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public bool Enabled => _options.Enabled && _options.Valid;

    // Inserts explicit jobs only; the worker does the slow part.
    public async Task<List<ImageProcessingJob>> EnqueueAsync(
        IEnumerable<ApartmentImage> images,
        string mode,
        ImageAuthorizationBasis basis,
        string? permissionReference,
        string? requestedByUserId,
        CancellationToken cancellationToken)
    {
        if (!Enabled) throw new InvalidOperationException("Image editing is disabled or unconfigured.");
        if (mode is not ("process" or "enhance") || basis == ImageAuthorizationBasis.Unknown)
            throw new ArgumentException("Only explicitly authorized editing jobs can be queued.");
        var list = images.ToList();
        var imageIds = list.Select(image => image.Id).ToList();
        var openIds = await _context.ImageProcessingJobs
            .Where(job =>
                job.PipelineVersion != null && job.PipelineVersion.StartsWith("openai-edit-v1:") &&
                job.ApartmentImageId != null &&
                imageIds.Contains(job.ApartmentImageId.Value) &&
                (job.Status == ImageJobStatus.Pending ||
                 job.Status == ImageJobStatus.Running))
            .Select(job => job.ApartmentImageId!.Value)
            .ToListAsync(cancellationToken);

        var jobs = list
            .Where(image => !openIds.Contains(image.Id))
            .Select(image => new ImageProcessingJob
            {
                ApartmentImageId = image.Id,
                ApartmentId = image.ApartmentId,
                // Always process the original, even if a processed version is live.
                SourceStoragePath = image.OriginalStoragePath ?? image.StoragePath,
                Mode = mode,
                PipelineVersion = OpenAIImageEditingService.BuildCacheVersion(_options),
                AuthorizationBasis = basis,
                PermissionReference = permissionReference,
                RequestedByUserId = requestedByUserId
            })
            .ToList();

        _context.ImageProcessingJobs.AddRange(jobs);
        await _context.SaveChangesAsync(cancellationToken);
        return jobs;
    }

    public async Task<ImageBudgetStatus> GetBudgetAsync(CancellationToken cancellationToken)
    {
        var monthStart = new DateTime(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var month = _context.ImageProcessingJobs
            // Provider budgets span configuration changes but exclude historical Modal work.
            .Where(job => job.StartedAt >= monthStart && job.PipelineVersion != null &&
                job.PipelineVersion.StartsWith("openai-edit-v1:"));
        var rows = await month.Select(job => new { job.EstimatedCostUsd, job.SafetyReportJson, job.DuplicateOfJobId }).ToListAsync(cancellationToken);
        var charges = rows.Select(row => OpenAIImageCostCalculator.ForJob(row.SafetyReportJson, row.EstimatedCostUsd, row.DuplicateOfJobId)).ToList();
        var spent = charges.Sum(charge => charge.Usd);
        var count = rows.Count;
        var gpuSeconds = 0d; // Kept for response compatibility; OpenAI is token-billed.

        var daysElapsed = Math.Max(1, (DateTime.UtcNow - monthStart).TotalDays);
        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
        var projected = spent / (decimal)daysElapsed * daysInMonth;

        return new ImageBudgetStatus(
            Enabled: _options.Enabled,
            MonthStart: monthStart,
            ImagesProcessed: count,
            MonthlyImageQuota: _options.MonthlyImageQuota,
            GpuSeconds: Math.Round(gpuSeconds, 1),
            SpentUsd: spent,
            ProjectedUsd: Math.Round(projected, 2),
            BudgetCutoffUsd: _options.BudgetCutoffUsd,
            MonthlyBudgetUsd: _options.MonthlyBudgetUsd,
            Exhausted: spent >= _options.BudgetCutoffUsd ||
                       count >= _options.MonthlyImageQuota,
            UsageCostUsd: charges.Where(charge => charge.Basis == "usage").Sum(charge => charge.Usd),
            ReservedCostUsd: charges.Where(charge => charge.Basis == "reservation").Sum(charge => charge.Usd),
            UsagePricedRequests: charges.Count(charge => charge.Basis == "usage"),
            UnreconciledRequests: charges.Count(charge => charge.Basis == "reservation"));
    }
}

public sealed record ImageBudgetStatus(
    bool Enabled,
    DateTime MonthStart,
    int ImagesProcessed,
    int MonthlyImageQuota,
    double GpuSeconds,
    decimal SpentUsd,
    decimal ProjectedUsd,
    decimal BudgetCutoffUsd,
    decimal MonthlyBudgetUsd,
    bool Exhausted,
    decimal UsageCostUsd,
    decimal ReservedCostUsd,
    int UsagePricedRequests,
    int UnreconciledRequests);
