namespace Website_API.Models;

/// <summary>
/// A user's own listing offered to one public board client. It goes to that client's agent first;
/// approving it never publishes the listing, and it reaches other clients only when the user
/// allowed sharing and the agent approved.
/// </summary>
public class ApartmentOffer
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public int Id { get; set; }
    public long ClientProfileId { get; set; }
    public ClientProfile ClientProfile { get; set; } = null!;
    public int ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;
    public string SubmittedByUserId { get; set; } = "";
    public AppUser SubmittedByUser { get; set; } = null!;
    /// <summary>The selected client's agent. Null only if that account was removed: any manager then reviews.</summary>
    public string? ReviewerUserId { get; set; }
    public AppUser? Reviewer { get; set; }
    public bool ShareWithOtherClients { get; set; }
    public string Status { get; set; } = Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Reviewer's bell: when they opened this submission.</summary>
    public DateTime? ReadAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public AppUser? ReviewedBy { get; set; }
    public List<ApartmentOfferMatch> Matches { get; set; } = [];
}

/// <summary>Another client an approved, shareable offer meets every hard requirement of. Goes to that client's agent.</summary>
public class ApartmentOfferMatch
{
    public int Id { get; set; }
    public int ApartmentOfferId { get; set; }
    public ApartmentOffer Offer { get; set; } = null!;
    public long ClientProfileId { get; set; }
    public ClientProfile ClientProfile { get; set; } = null!;
    public string AgentUserId { get; set; } = "";
    public AppUser Agent { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
