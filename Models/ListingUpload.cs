using System.Text.Json.Serialization;

namespace Website_API.Models;

public class ListingUpload
{
    public int Id { get; set; }
    public int? ApartmentId { get; set; }
    public string? SourcePlatform { get; set; }
    public string? SourceListingId { get; set; }
    public string? SourceUrl { get; set; }
    public string? AgentUserId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string PublishedListingId { get; set; } = string.Empty;
    public string? PublishedUrl { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public Apartment? Apartment { get; set; }

    [JsonIgnore]
    public AppUser? AgentUser { get; set; }
}
