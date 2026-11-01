namespace Website_API.Models;

/// <summary>A persisted, per-recipient alert created with a lead status change.</summary>
public class CrmLeadNotification
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;
    public int LeadId { get; set; }
    public CrmLead Lead { get; set; } = null!;
    public string ActorName { get; set; } = string.Empty;
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
