namespace Website_API.Models;

/// <summary>
/// A client's saved Velven Match search matches a published listing. A Velven agent reviews the
/// client first; only when the agent marks it ready is the owner told (bell and/or email), with
/// the client's requirements and the agent's contact details. The client's own contact details
/// are never sent to the owner.
/// </summary>
public class ListingInterestNotification
{
    public const string Pending = "pending";
    public const string Ready = "ready";
    public const string Dismissed = "dismissed";

    public int Id { get; set; }

    /// <summary>Who listed the home (agent or owner account). Part of the duplicate check.</summary>
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    public int ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;

    /// <summary>The client's saved search. Deleting the search removes the match.</summary>
    public int SavedSearchId { get; set; }
    public SavedSearch SavedSearch { get; set; } = null!;

    /// <summary>pending (agent to review), ready (sent to the owner) or dismissed (not a fit).</summary>
    public string Status { get; set; } = Pending;

    /// <summary>The agent who reviews the client and whose phone the owner gets. Null: any manager.</summary>
    public string? ReviewerUserId { get; set; }
    public AppUser? Reviewer { get; set; }

    /// <summary>The owner's account, when they have one (bell alert).</summary>
    public string? OwnerUserId { get; set; }

    /// <summary>The owner's email from the owner link, when known (email alert).</summary>
    public string? OwnerEmail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Reviewer's bell: when they opened this pending match.</summary>
    public DateTime? ReadAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>Owner's bell: when they saw it.</summary>
    public DateTime? OwnerReadAt { get; set; }

    public DateTime? OwnerEmailedAt { get; set; }
}
