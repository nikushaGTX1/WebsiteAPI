namespace Website_API.Models;

public enum CrmLeadStatus
{
    New,
    Contacted,
    Qualified,
    Viewing,
    Negotiation,
    Won,
    Lost
}

public enum CrmLeadSource
{
    Website,
    Manual,
    Phone,
    Referral,
    AiMatch
}

public enum CrmActivityType
{
    Note,
    Status,
    Assignment,
    Inquiry,
    Task,
    System
}

public enum CrmTaskType
{
    FollowUp,
    Call,
    Viewing,
    Email
}

public static class CrmEnumText
{
    public static string ToApiValue(this CrmLeadStatus value) => value switch
    {
        CrmLeadStatus.New => "new",
        CrmLeadStatus.Contacted => "contacted",
        CrmLeadStatus.Qualified => "qualified",
        CrmLeadStatus.Viewing => "viewing",
        CrmLeadStatus.Negotiation => "negotiation",
        CrmLeadStatus.Won => "won",
        CrmLeadStatus.Lost => "lost",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToApiValue(this CrmLeadSource value) => value switch
    {
        CrmLeadSource.Website => "website",
        CrmLeadSource.Manual => "manual",
        CrmLeadSource.Phone => "phone",
        CrmLeadSource.Referral => "referral",
        CrmLeadSource.AiMatch => "ai-match",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToApiValue(this CrmActivityType value) => value switch
    {
        CrmActivityType.Note => "note",
        CrmActivityType.Status => "status",
        CrmActivityType.Assignment => "assignment",
        CrmActivityType.Inquiry => "inquiry",
        CrmActivityType.Task => "task",
        CrmActivityType.System => "system",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToApiValue(this CrmTaskType value) => value switch
    {
        CrmTaskType.FollowUp => "follow-up",
        CrmTaskType.Call => "call",
        CrmTaskType.Viewing => "viewing",
        CrmTaskType.Email => "email",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static bool TryParseLeadStatus(
        string? value,
        out CrmLeadStatus result) =>
        TryParse(
            value,
            new Dictionary<string, CrmLeadStatus>(StringComparer.OrdinalIgnoreCase)
            {
                ["new"] = CrmLeadStatus.New,
                ["contacted"] = CrmLeadStatus.Contacted,
                ["qualified"] = CrmLeadStatus.Qualified,
                ["viewing"] = CrmLeadStatus.Viewing,
                ["negotiation"] = CrmLeadStatus.Negotiation,
                ["won"] = CrmLeadStatus.Won,
                ["lost"] = CrmLeadStatus.Lost
            },
            out result);

    public static bool TryParseLeadSource(
        string? value,
        out CrmLeadSource result) =>
        TryParse(
            value,
            new Dictionary<string, CrmLeadSource>(StringComparer.OrdinalIgnoreCase)
            {
                ["website"] = CrmLeadSource.Website,
                ["manual"] = CrmLeadSource.Manual,
                ["phone"] = CrmLeadSource.Phone,
                ["referral"] = CrmLeadSource.Referral,
                ["ai-match"] = CrmLeadSource.AiMatch
            },
            out result);

    public static bool TryParseActivityType(
        string? value,
        out CrmActivityType result) =>
        TryParse(
            value,
            new Dictionary<string, CrmActivityType>(StringComparer.OrdinalIgnoreCase)
            {
                ["note"] = CrmActivityType.Note,
                ["status"] = CrmActivityType.Status,
                ["assignment"] = CrmActivityType.Assignment,
                ["inquiry"] = CrmActivityType.Inquiry,
                ["task"] = CrmActivityType.Task,
                ["system"] = CrmActivityType.System
            },
            out result);

    public static bool TryParseTaskType(
        string? value,
        out CrmTaskType result) =>
        TryParse(
            value,
            new Dictionary<string, CrmTaskType>(StringComparer.OrdinalIgnoreCase)
            {
                ["follow-up"] = CrmTaskType.FollowUp,
                ["call"] = CrmTaskType.Call,
                ["viewing"] = CrmTaskType.Viewing,
                ["email"] = CrmTaskType.Email
            },
            out result);

    public static CrmLeadStatus ParseLeadStatus(string value) =>
        TryParseLeadStatus(value, out var result)
            ? result
            : throw new InvalidOperationException($"Unknown CRM lead status '{value}'.");

    public static CrmLeadSource ParseLeadSource(string value) =>
        TryParseLeadSource(value, out var result)
            ? result
            : throw new InvalidOperationException($"Unknown CRM lead source '{value}'.");

    public static CrmActivityType ParseActivityType(string value) =>
        TryParseActivityType(value, out var result)
            ? result
            : throw new InvalidOperationException($"Unknown CRM activity type '{value}'.");

    public static CrmTaskType ParseTaskType(string value) =>
        TryParseTaskType(value, out var result)
            ? result
            : throw new InvalidOperationException($"Unknown CRM task type '{value}'.");

    private static bool TryParse<T>(
        string? value,
        IReadOnlyDictionary<string, T> values,
        out T result)
        where T : struct
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            values.TryGetValue(value.Trim(), out result))
        {
            return true;
        }

        result = default;
        return false;
    }
}

public class CrmLead
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Goal { get; set; }
    public string? PreferredContactMethod { get; set; }
    public string[] PreferredDistricts { get; set; } = [];
    public string? PreferredPropertyType { get; set; }
    public int? Bedrooms { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Preferences { get; set; }
    public string? Message { get; set; }
    public DateTime? RequestedViewingAt { get; set; }

    public CrmLeadStatus Status { get; set; } = CrmLeadStatus.New;
    public CrmLeadSource Source { get; set; } = CrmLeadSource.Manual;

    public bool ConsentGiven { get; set; }
    public DateTime? ConsentGivenAt { get; set; }

    public int? ApartmentId { get; set; }
    public Apartment? Apartment { get; set; }

    public string? CustomerUserId { get; set; }
    public AppUser? CustomerUser { get; set; }

    public string? AssignedAgentId { get; set; }
    public AppUser? AssignedAgent { get; set; }

    public string? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public List<CrmActivity> Activities { get; set; } = [];
    public List<CrmTask> Tasks { get; set; } = [];
}

public class CrmActivity
{
    public int Id { get; set; }

    public int LeadId { get; set; }
    public CrmLead Lead { get; set; } = null!;

    public CrmActivityType Type { get; set; }
    public string Content { get; set; } = string.Empty;

    public string? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CrmTask
{
    public int Id { get; set; }

    public int LeadId { get; set; }
    public CrmLead Lead { get; set; } = null!;

    public CrmTaskType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? AssignedAgentId { get; set; }
    public AppUser? AssignedAgent { get; set; }

    public string? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
