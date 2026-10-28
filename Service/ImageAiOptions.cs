namespace Website_API.Services;

// Bound from the "ImageAi" section (Railway: ImageAi__Enabled, ImageAi__Endpoint, ...).
public sealed class ImageAiOptions
{
    public bool Enabled { get; set; }

    // Queue detection jobs for newly uploaded apartment photos.
    public bool AutoEnqueueUploads { get; set; }

    public string? Endpoint { get; set; }
    public string? Token { get; set; }

    public decimal MonthlyBudgetUsd { get; set; } = 15m;

    // Stop below the hard budget to leave room for idle container time.
    public decimal BudgetCutoffUsd { get; set; } = 12m;

    public int MonthlyImageQuota { get; set; } = 5000;

    // Modal T4 rate. Check modal.com/pricing before changing the budget.
    public decimal GpuUsdPerSecond { get; set; } = 0.000164m;

    // Modal bills the idle scaledown window after every cold start.
    public int IdleSecondsPerColdStart { get; set; } = 60;

    public int MaxAttempts { get; set; } = 4;
    public int PollSeconds { get; set; } = 10;
    public int RequestTimeoutSeconds { get; set; } = 300;
    public string[] OwnBrandTerms { get; set; } = ["velven"];
}
