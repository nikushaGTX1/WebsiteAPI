namespace Website_API.Models;

/// <summary>
/// A review a client left through an agent's public review link (no sign-in needed).
/// The agent is taken from the link on the server, never from the request body.
/// </summary>
public class AgentReview
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string AgentId { get; set; } = "";

    public AppUser Agent { get; set; } = null!;

    /// <summary>Optional name the client typed. Only the first name is ever shown publicly.</summary>
    public string? CustomerName { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Random id the review form creates once; a repeated submit with it is not stored twice.</summary>
    public Guid SubmissionId { get; set; }

    /// <summary>Salted SHA-256 of the client IP, used only for spam limits. Never returned by the API.</summary>
    public string? IpHash { get; set; }
}
