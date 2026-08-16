namespace Website_API.Models;

public class StreetGeometry
{
    public long Id { get; set; }
    public long OsmWayId { get; set; }
    public string City { get; set; } = "Tbilisi";
    public string District { get; set; } = string.Empty;
    public string[] Names { get; set; } = [];
    public string CoordinatesJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
