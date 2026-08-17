namespace Website_API.Models;

public sealed class CanonicalStreet
{
    public long Id { get; set; }
    public long CityId { get; set; }
    public long DistrictId { get; set; }
    public string NameKa { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string[] Aliases { get; set; } = [];
    public string? GeometryGeoJson { get; set; }
    public string? BoundsGeoJson { get; set; }
    public double? CentroidLatitude { get; set; }
    public double? CentroidLongitude { get; set; }
    public string Source { get; set; } = string.Empty;
    public string ExternalSourceId { get; set; } = string.Empty;
    public string GeometryStatus { get; set; } = "geometry_missing";
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedByUserId { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public LocationArea City { get; set; } = null!;
    public LocationArea District { get; set; } = null!;
}
