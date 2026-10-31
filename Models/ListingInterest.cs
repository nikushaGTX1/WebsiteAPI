namespace Website_API.Models;

/// <summary>
/// Bell alert for the person who listed a home (owner or agent): a client's saved Velven Match
/// search now matches one of their published listings. The client stays anonymous.
/// </summary>
public class ListingInterestNotification
{
    public int Id { get; set; }

    /// <summary>The listing's uploader, who receives the alert.</summary>
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    public int ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;

    /// <summary>The client's saved search. Deleting the search removes the alert.</summary>
    public int SavedSearchId { get; set; }
    public SavedSearch SavedSearch { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
