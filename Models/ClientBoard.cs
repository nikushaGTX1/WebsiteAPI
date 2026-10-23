namespace Website_API.Models;

public class ClientIntakeLink
{
    public long Id { get; set; }
    public string Token { get; set; } = "";
    public string? Slug { get; set; }
    public string AgentUserId { get; set; } = "";
    public AppUser Agent { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ClientProfile
{
    public long Id { get; set; }
    public long IntakeLinkId { get; set; }
    public ClientIntakeLink IntakeLink { get; set; } = null!;
    public string AgentUserId { get; set; } = "";
    public AppUser Agent { get; set; } = null!;
    public int LeadId { get; set; }
    public CrmLead Lead { get; set; } = null!;
    public string ProfileJson { get; set; } = "{}";
    public string FirstName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Nationality { get; set; }
    public DateTime ConsentAt { get; set; }
    public DateTime? PublishConsentAt { get; set; }
    public string Status { get; set; } = "Active";
    public bool IdentityVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // Indexed/searchable requirements, extracted from the validated profile at intake.
    public string[] Districts { get; set; } = [];
    public decimal BudgetMinUsd { get; set; }
    public decimal BudgetMaxUsd { get; set; }
    public DateOnly? MoveInDate { get; set; }
    public string MoveInTiming { get; set; } = "";
    public int Residents { get; set; }
    public bool HasPets { get; set; }
}
