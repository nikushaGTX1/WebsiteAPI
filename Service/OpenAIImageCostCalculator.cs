using System.Text.Json;
using Website_API.Models;

namespace Website_API.Services;

public sealed record ImageUsageCost(long TextInputTokens, long ImageInputTokens, long ImageOutputTokens,
    decimal TextInputUsd, decimal ImageInputUsd, decimal ImageOutputUsd)
{
    public decimal TotalUsd => TextInputUsd + ImageInputUsd + ImageOutputUsd;
    public string Basis => "usage";
    public string RateCard => OpenAIImageCostCalculator.RateCardVersion;
}

// Standard direct Images API rates, verified 2026-11-05. No Batch or cached-input discount.
// https://developers.openai.com/api/docs/models/gpt-image-2.5-sunburst
// https://developers.openai.com/api/docs/guides/image-generation#cached-input-pricing
public static class OpenAIImageCostCalculator
{
    public const string RateCardVersion = "openai-images-standard-2026-11-05";
    public const decimal TextInputUsdPerMillion = 5m;
    public const decimal ImageInputUsdPerMillion = 8m;
    public const decimal ImageOutputUsdPerMillion = 30m;
    public static bool HasVerifiedRates(string? model) => model is "gpt-image-2.5-sunburst" or "gpt-image-2.5-sunburst-2026-09-08";

    public static ImageUsageCost? Calculate(string? model, JsonElement usage)
    {
        if (!HasVerifiedRates(model) || usage.ValueKind != JsonValueKind.Object) return null;
        if (!usage.TryGetProperty("input_tokens_details", out var details) || details.ValueKind != JsonValueKind.Object ||
            !Count(details, "text_tokens", out var text) || !Count(details, "image_tokens", out var image) ||
            !Count(usage, "input_tokens", out var input) || !Count(usage, "output_tokens", out var output) ||
            input != text + image || input == 0 || output == 0) return null;
        if (usage.TryGetProperty("output_tokens_details", out var outputDetails) &&
            (outputDetails.ValueKind != JsonValueKind.Object || !Count(outputDetails, "image_tokens", out var outputImage) ||
             !Count(outputDetails, "text_tokens", out var outputText) || outputText != 0 || outputImage != output)) return null;
        if (usage.TryGetProperty("total_tokens", out _) && (!Count(usage, "total_tokens", out var total) || total != input + output)) return null;
        // Cached fields intentionally do not reduce rates on this direct /v1/images/edits endpoint.
        return FromTokens(model, text, image, output);
    }

    public static ImageUsageCost? FromTokens(string? model, long text, long image, long output)
    {
        if (!HasVerifiedRates(model) || text < 0 || image < 0 || output < 0 ||
            text > 1_000_000_000 || image > 1_000_000_000 || output > 1_000_000_000) return null;
        return new(text, image, output, text * TextInputUsdPerMillion / 1_000_000m,
            image * ImageInputUsdPerMillion / 1_000_000m, output * ImageOutputUsdPerMillion / 1_000_000m);
    }

    public static decimal? Reservation(ImageAiOptions options)
    {
        if (options.Quality != options.ForecastQuality || options.Size != options.ForecastSize ||
            options.Model != options.ForecastModel || options.ReservationMultiplier < 1m || options.ReservationMultiplier > 100m) return null;
        var cost = FromTokens(options.Model, options.ForecastTextInputTokens, options.ForecastImageInputTokens, options.ForecastImageOutputTokens);
        return cost is { TotalUsd: > 0 } ? cost.TotalUsd * options.ReservationMultiplier : null;
    }

    // Read-only reconciliation of old records: their raw usage was saved even though their cost was flat $0.10.
    // No production history is rewritten. Missing/invalid usage retains its existing reservation.
    public static (decimal Usd, string Basis, ImageUsageCost? Usage) ForJob(string? metadataJson, decimal reservedUsd, long? duplicateOfJobId = null)
    {
        if (duplicateOfJobId is not null) return (0m, "duplicate", null);
        try
        {
            using var json = JsonDocument.Parse(metadataJson ?? "null");
            var metadata = json.RootElement;
            if (metadata.ValueKind != JsonValueKind.Object || !metadata.TryGetProperty("model", out var model) ||
                model.ValueKind != JsonValueKind.String || !metadata.TryGetProperty("usage", out var usage)) return (reservedUsd, "reservation", null);
            using var legacy = usage.ValueKind == JsonValueKind.String ? JsonDocument.Parse(usage.GetString() ?? "null") : null;
            var billedModel = metadata.TryGetProperty("responseModel", out var responseModel) && responseModel.ValueKind == JsonValueKind.String
                ? responseModel.GetString() : model.GetString();
            var cost = Calculate(billedModel, legacy?.RootElement ?? usage);
            return cost is null ? (reservedUsd, "reservation", null) : (cost.TotalUsd, "usage", cost);
        }
        catch (JsonException) { return (reservedUsd, "reservation", null); }
    }

    private static bool Count(JsonElement element, string name, out long value)
    {
        value = 0;
        return element.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.Number &&
            item.TryGetInt64(out value) && value is >= 0 and <= 1_000_000_000;
    }
}

public sealed record ImageEditAccounting(string Model, string Quality, string Size, string? ResponseModel,
    string? ResponseQuality, string? ResponseSize, string? RequestId, JsonElement? Usage, ImageUsageCost? Cost)
{
    public string ToMetadataJson() => JsonSerializer.Serialize(new
    {
        provider = "openai", endpoint = "https://api.openai.com/v1/images/edits", model = Model,
        quality = Quality, size = Size, responseModel = ResponseModel, responseQuality = ResponseQuality,
        responseSize = ResponseSize, requestId = RequestId, usage = Usage, cost = Cost,
        costBasis = Cost is null ? "reservation" : "usage", rateCard = OpenAIImageCostCalculator.RateCardVersion,
        ratesUsdPerMillion = new { textInput = OpenAIImageCostCalculator.TextInputUsdPerMillion,
            imageInput = OpenAIImageCostCalculator.ImageInputUsdPerMillion, imageOutput = OpenAIImageCostCalculator.ImageOutputUsdPerMillion },
        invoiceVerified = false
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
