namespace Website_API.Models;

/// <summary>
/// A Velven Match search a client saved (automatically, after finishing the questionnaire).
/// The answers are stored as the website's own profile JSON, which only holds
/// language-independent codes ("Vake", "Rent", "Dog"), never translated text.
/// </summary>
public class SavedSearch
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>The Velven Match profile exactly as the website sent it (camelCase JSON).</summary>
    public string ProfileJson { get; set; } = "{}";

    /// <summary>SHA-256 of the normalized criteria; one client cannot save the same search twice.</summary>
    public string CriteriaHash { get; set; } = string.Empty;

    /// <summary>New-match alerts (website and/or email, per the client's preferences) for this search.</summary>
    public bool NotificationsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the criteria last changed. Only listings published after this count as "new"
    /// matches, so editing a search never floods the client with old listings.
    /// </summary>
    public DateTime CriteriaSince { get; set; } = DateTime.UtcNow;

    public List<SavedSearchMatch> Matches { get; set; } = [];
}

/// <summary>A listing published after the search was saved that meets every mandatory requirement.</summary>
public class SavedSearchMatch
{
    public int Id { get; set; }

    public int SavedSearchId { get; set; }
    public SavedSearch SavedSearch { get; set; } = null!;

    public int ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;

    public DateTime MatchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the client first saw it in Velven Discover; null shows the "New match" badge.</summary>
    public DateTime? SeenAt { get; set; }
}

public class ClientNotification
{
    public const string MatchType = "match";

    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    public string Type { get; set; } = MatchType;

    public int? SavedSearchId { get; set; }
    public SavedSearch? SavedSearch { get; set; }

    public int? ApartmentId { get; set; }
    public Apartment? Apartment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    /// <summary>True when the client wants this alert by email and it has not been sent yet.</summary>
    public bool EmailPending { get; set; }
    public DateTime? EmailedAt { get; set; }
}
