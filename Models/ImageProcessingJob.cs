using System.Text.Json.Serialization;

namespace Website_API.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImageJobStatus
{
    Pending,
    Running,
    NoWatermark,
    NeedsCleanOriginal,
    DetectedOnly,
    AwaitingApproval,
    Approved,
    Rejected,
    Failed
}

// Who may modify the photo. Unknown photos are only scanned, never edited.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImageAuthorizationBasis
{
    Unknown,
    Owned,
    Licensed
}

public class ImageProcessingJob
{
    // Marks checks run from the upload form; they have no stored photo.
    public const string UploadFormSource = "upload-form";

    public long Id { get; set; }

    public int? ApartmentImageId { get; set; }
    public ApartmentImage? ApartmentImage { get; set; }
    public int? ApartmentId { get; set; }

    // The original stays untouched in storage; approval only repoints the image.
    public string SourceStoragePath { get; set; } = string.Empty;
    public string? SourceSha256 { get; set; }

    public string Mode { get; set; } = "detect";
    public ImageAuthorizationBasis AuthorizationBasis { get; set; }
    public string? PermissionReference { get; set; }
    public string? RequestedByUserId { get; set; }

    public ImageJobStatus Status { get; set; } = ImageJobStatus.Pending;
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? LockedUntil { get; set; }
    public string? LastError { get; set; }

    public string? Classification { get; set; }
    public string? DetectionsJson { get; set; }
    public string? WatermarkText { get; set; }
    public long? DuplicateOfJobId { get; set; }

    public string? ProcessedStoragePath { get; set; }
    public string? ProcessedSha256 { get; set; }

    public double GpuSeconds { get; set; }
    public decimal EstimatedCostUsd { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? CleanOriginalRequestedAt { get; set; }
}
