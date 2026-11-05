namespace Website_API.Services;

// Server-only settings. No key or provider configuration is returned to Angular.
public sealed class ImageAiOptions
{
    public bool Enabled { get; set; }
    // Explicit account-accessible model required; no fallback to another paid model.
    public string? Model { get; set; }
    public string Size { get; set; } = "1024x1024";
    public string Quality { get; set; } = "low";
    // Observed low/1024 sample: jobs 898-902, 2026-11-05 (five responses with identical usage).
    // These configurable token assumptions reserve spend BEFORE a request; returned usage settles it.
    public string ForecastModel { get; set; } = "gpt-image-2.5-sunburst";
    public string ForecastQuality { get; set; } = "low";
    public string ForecastSize { get; set; } = "1024x1024";
    public long ForecastTextInputTokens { get; set; } = 97;
    public long ForecastImageInputTokens { get; set; } = 1457;
    public long ForecastImageOutputTokens { get; set; } = 196;
    public decimal ReservationMultiplier { get; set; } = 2m;
    public decimal MonthlyBudgetUsd { get; set; } = 10m;
    public decimal BudgetCutoffUsd { get; set; } = 8m;
    public int MonthlyImageQuota { get; set; } = 20;
    public int DailyUserLimit { get; set; } = 5;
    public int RequestTimeoutSeconds { get; set; } = 180;
    public int PollSeconds { get; set; } = 10;
    // Invalid optional editing settings fail closed without taking listing uploads offline.
    public bool Valid => !string.IsNullOrWhiteSpace(Model) && BudgetCutoffUsd > 0 &&
        MonthlyImageQuota > 0 && DailyUserLimit > 0 && RequestTimeoutSeconds is >= 10 and <= 300 &&
        Quality is "low" or "medium" or "high" or "auto" && Size is "1024x1024" or "1536x1024" or "1024x1536" or "auto";
}
