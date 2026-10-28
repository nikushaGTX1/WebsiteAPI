namespace Website_API.Models;

public class ApartmentImage
{
    public int Id { get; set; }

    public int ApartmentId { get; set; }

    public Apartment Apartment { get; set; } = null!;
    public string StoragePath { get; set; } = string.Empty;

    // Set when an approved processed version replaces StoragePath.
    public string? OriginalStoragePath { get; set; }

    public int SortOrder { get; set; }

    public bool IsCover { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}