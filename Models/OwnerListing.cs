namespace Website_API.Models;

/// <summary>
/// A link an agent sends to a property owner. The owner fills in a simplified
/// listing form without an account; the submission goes to the agent who made the link.
/// </summary>
public class OwnerListingLink
{
    public long Id { get; set; }

    public string Token { get; set; } = string.Empty;

    public string AgentUserId { get; set; } = string.Empty;

    /// <summary>"Rent" or "Sale".</summary>
    public string DealType { get; set; } = "Rent";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public int Uses { get; set; }
}

public class OwnerSubmission
{
    public long Id { get; set; }

    public long LinkId { get; set; }

    public string AgentUserId { get; set; } = string.Empty;

    public string DealType { get; set; } = "Rent";

    public string OwnerName { get; set; } = string.Empty;

    public string OwnerPhone { get; set; } = string.Empty;

    public string? OwnerEmail { get; set; }

    /// <summary>The owner's answers, using the upload form's field names, as JSON.</summary>
    public string DataJson { get; set; } = "{}";

    /// <summary>Storage paths of the owner's photos, as a JSON array.</summary>
    public string PhotoPathsJson { get; set; } = "[]";

    /// <summary>new → contacted → visited → published, or rejected.</summary>
    public string Status { get; set; } = "new";

    /// <summary>The agent's on-site checks: { "hasElevator": true, ... } as JSON.</summary>
    public string VerificationJson { get; set; } = "{}";

    public string? AgentNotes { get; set; }

    public int? PublishedApartmentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
