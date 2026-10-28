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

    // Partners whose watermarks Velven may remove with their permission (myhome.ge cooperates
    // with Velven). Railway: ImageAi__PartnerBrandTerms__0=myhome. Any other mark stays third-party.
    public string[] PartnerBrandTerms { get; set; } = ["myhome"];

    // Marks the AI service may remove: our own plus licensed partners.
    public string[] RemovableBrandTerms => [.. OwnBrandTerms, .. PartnerBrandTerms];

    /// <summary>
    /// "partner_brand" when every watermark read on the photo is a licensed partner's (or ours),
    /// otherwise the AI service's own answer. Also corrects answers cached before a partner was added.
    /// </summary>
    public string? PublicClassification(string? classification, string? watermarkText)
    {
        if (classification is not ("own_brand" or "third_party") || string.IsNullOrWhiteSpace(watermarkText))
            return classification;
        var parts = watermarkText.Split(" | ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool Has(string text, string[] terms) =>
            terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
        var allRemovable = parts.All(part => Has(part, RemovableBrandTerms));
        var anyPartner = parts.Any(part => Has(part, PartnerBrandTerms) && !Has(part, OwnBrandTerms));
        return allRemovable && anyPartner ? "partner_brand" : classification;
    }
}
