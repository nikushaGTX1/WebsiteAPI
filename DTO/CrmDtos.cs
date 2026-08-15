using System.ComponentModel.DataAnnotations;

namespace Website_API.DTO;

public class CreateCrmLeadDto
{
    [Required, StringLength(160)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }

    [Phone, StringLength(50)]
    public string? PhoneNumber { get; set; }

    [StringLength(30)]
    public string Source { get; set; } = "manual";

    [StringLength(30)]
    public string Status { get; set; } = "new";

    [StringLength(80)]
    public string? Goal { get; set; }

    [StringLength(30)]
    public string? PreferredContactMethod { get; set; }

    [MaxLength(20)]
    public List<string> PreferredDistricts { get; set; } = [];

    [StringLength(80)]
    public string? PreferredPropertyType { get; set; }

    [Range(0, 100)]
    public int? Bedrooms { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal? BudgetMin { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal? BudgetMax { get; set; }

    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "USD";

    [StringLength(4000)]
    public string? Preferences { get; set; }

    [StringLength(4000)]
    public string? Message { get; set; }

    public DateTime? RequestedViewingAt { get; set; }
    public int? ApartmentId { get; set; }
    public string? CustomerUserId { get; set; }
    public string? AssignedAgentId { get; set; }
    public bool ConsentGiven { get; set; }
}

public class UpdateCrmLeadDto
{
    [Required, StringLength(160)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }

    [Phone, StringLength(50)]
    public string? PhoneNumber { get; set; }

    [StringLength(30)]
    public string? Source { get; set; }

    [StringLength(80)]
    public string? Goal { get; set; }

    [StringLength(30)]
    public string? PreferredContactMethod { get; set; }

    [MaxLength(20)]
    public List<string> PreferredDistricts { get; set; } = [];

    [StringLength(80)]
    public string? PreferredPropertyType { get; set; }

    [Range(0, 100)]
    public int? Bedrooms { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal? BudgetMin { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal? BudgetMax { get; set; }

    [StringLength(3, MinimumLength = 3)]
    public string? Currency { get; set; }

    [StringLength(4000)]
    public string? Preferences { get; set; }
    public int? ApartmentId { get; set; }
}

public class UpdateCrmLeadStatusDto
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class AssignCrmLeadDto
{
    public string? AssignedAgentId { get; set; }
}

public class CreateCrmActivityDto
{
    [StringLength(30)]
    public string Type { get; set; } = "note";

    [Required, StringLength(4000)]
    public string Body { get; set; } = string.Empty;
}

public class CreateCrmTaskDto
{
    [Required, StringLength(30)]
    public string Type { get; set; } = "follow-up";

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    public DateTime DueAt { get; set; }
    public string? AssignedAgentId { get; set; }
}

public class PatchCrmTaskDto
{
    [StringLength(30)]
    public string? Type { get; set; }

    [StringLength(200)]
    public string? Title { get; set; }

    [StringLength(4000)]
    public string? Description { get; set; }

    public DateTime? DueAt { get; set; }

    [RegularExpression("^(open|completed)$")]
    public string? Status { get; set; }
}

public class PublicCrmInquiryDto
{
    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }

    [Phone, StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(4000)]
    public string? Message { get; set; }

    public int? ApartmentId { get; set; }
    public DateTime? RequestedViewingAt { get; set; }
    public bool ConsentToContact { get; set; }

    // Leave this hidden field blank. Filled values are treated as bot submissions.
    [StringLength(200)]
    public string? Website { get; set; }
}

public class PublicCrmInquiryResponseDto
{
    public bool Received { get; set; }
}

public class CrmLeadListItemDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public string? PreferredContactMethod { get; set; }
    public List<string> PreferredDistricts { get; set; } = [];
    public string? PreferredPropertyType { get; set; }
    public int? Bedrooms { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int? ApartmentId { get; set; }
    public string? ApartmentTitle { get; set; }
    public string? AssignedAgentId { get; set; }
    public string? AssignedAgentName { get; set; }
    public DateTime? NextFollowUpAt { get; set; }
    public CrmTaskResponseDto? NextTask { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CrmLeadDetailDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public string? PreferredContactMethod { get; set; }
    public List<string> PreferredDistricts { get; set; } = [];
    public string? PreferredPropertyType { get; set; }
    public int? Bedrooms { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Preferences { get; set; }
    public string? Message { get; set; }
    public DateTime? RequestedViewingAt { get; set; }
    public DateTime? NextFollowUpAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public bool ConsentGiven { get; set; }
    public DateTime? ConsentGivenAt { get; set; }
    public int? ApartmentId { get; set; }
    public string? ApartmentTitle { get; set; }
    public string? CustomerUserId { get; set; }
    public string? AssignedAgentId { get; set; }
    public string? AssignedAgentName { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<CrmActivityResponseDto> Activities { get; set; } = [];
    public List<CrmTaskResponseDto> Tasks { get; set; } = [];
}

public class CrmActivityResponseDto
{
    public int Id { get; set; }
    public int LeadId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CrmTaskResponseDto
{
    public int Id { get; set; }
    public int LeadId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "open";
    public string? AssignedAgentId { get; set; }
    public string? AssignedAgentName { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CrmLeadStatusCountsDto
{
    public int New { get; set; }
    public int Contacted { get; set; }
    public int Qualified { get; set; }
    public int Viewing { get; set; }
    public int Negotiation { get; set; }
    public int Won { get; set; }
    public int Lost { get; set; }
}

public class CrmMetricsDto
{
    public int TotalLeads { get; set; }
    public int ActiveLeads { get; set; }
    public int NewLeads { get; set; }
    public int UnassignedLeads { get; set; }
    public int OverdueTasks { get; set; }
    public int DueTodayTasks { get; set; }
    public int UpcomingViewings { get; set; }
    public int WonLeads { get; set; }
    public double ConversionRate { get; set; }
    public CrmLeadStatusCountsDto StatusCounts { get; set; } = new();
}
