namespace Website_API.Models;

public sealed class LocationArea
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string NameKa { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? BoundaryGeoJson { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? ExternalSourceId { get; set; }
    public string GeometryStatus { get; set; } = "geometry_missing";
    public DateTime? ApprovedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public LocationArea? Parent { get; set; }
    public List<LocationArea> Children { get; set; } = [];
}
