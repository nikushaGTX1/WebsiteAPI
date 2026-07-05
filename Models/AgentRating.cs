namespace Website_API.Models;

public class AgentRating
{
    public int Id { get; set; }

    public string AgentId { get; set; } = "";

    public AppUser Agent { get; set; } = null!;

    public string UserId { get; set; } = "";

    public AppUser User { get; set; } = null!;

    public int Stars { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}