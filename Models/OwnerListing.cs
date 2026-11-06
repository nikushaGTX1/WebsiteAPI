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

    public long? LinkId { get; set; }

    public bool IsGeneralSubmission { get; set; }
    public Guid? SubmissionKey { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedByUserId { get; set; }

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

/// <summary>Audit events are append-only; SEND is serialized on the parent submission.</summary>
public class SubmissionEvent
{
    public long Id { get; set; }
    public long OwnerSubmissionId { get; set; }
    public OwnerSubmission Submission { get; set; } = null!;
    public string Action { get; set; } = "";
    public string? ActorUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int MatchedClients { get; set; }
}

public class SubmissionClientMatch
{
    public long Id { get; set; }
    public long OwnerSubmissionId { get; set; }
    public OwnerSubmission Submission { get; set; } = null!;
    public long ClientProfileId { get; set; }
    public ClientProfile ClientProfile { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
