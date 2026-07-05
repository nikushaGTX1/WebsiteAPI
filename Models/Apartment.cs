namespace Website_API.Models;

public class Apartment
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public decimal Price { get; set; }

    public string? Address { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}