public class CrmQuestionnaireLink
{
    public long Id { get; set; }

    public string Token { get; set; } = string.Empty;

    public string? Slug { get; set; }

    public string AgentUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public int Uses { get; set; } = 0;
}
