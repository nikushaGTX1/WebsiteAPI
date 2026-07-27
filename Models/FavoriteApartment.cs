namespace Website_API.Models;

public class FavoriteApartment
{
    public string UserId { get; set; } = string.Empty;

    public AppUser User { get; set; } = null!;

    public int ApartmentId { get; set; }

    public Apartment Apartment { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
