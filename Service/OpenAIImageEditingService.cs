using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Website_API.Services;

public sealed class OpenAIImageEditingService
{
    private readonly HttpClient _http;
    private readonly ImageAiOptions _options;
    private readonly string? _key;
    public OpenAIImageEditingService(HttpClient http, IOptions<ImageAiOptions> options, IConfiguration configuration)
    {
        _http = http;
        _options = options.Value;
        _key = configuration["OPENAI_API_KEY"];
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.RequestTimeoutSeconds, 10, 300));
    }
    public bool Configured => !string.IsNullOrWhiteSpace(_key) && _options.Valid;
    public static string BuildCacheVersion(ImageAiOptions o) => "openai-edit-v1:" + Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{o.Model}:{o.Size}:{o.Quality}")))[..32];
    public string CacheVersion => BuildCacheVersion(_options);

    public static string ValidateImage(byte[] bytes, bool mask = false)
    {
        if (bytes.Length is 0 or > 8 * 1024 * 1024) throw new ArgumentException("Images must be at most 8 MB.");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new ArgumentException("The image cannot be decoded.");
        if (codec.Info.Width <= 0 || codec.Info.Height <= 0 || (long)codec.Info.Width * codec.Info.Height > 20_000_000)
            throw new ArgumentException("Images must be at most 20 megapixels.");
        if (codec.FrameCount > 1) throw new ArgumentException("Use a still image rather than an animation.");
        if (mask && codec.EncodedFormat != SKEncodedImageFormat.Png) throw new ArgumentException("Masks must be transparent PNG images.");
        return codec.EncodedFormat switch { SKEncodedImageFormat.Png => "image/png", SKEncodedImageFormat.Jpeg => "image/jpeg", SKEncodedImageFormat.Webp when !mask => "image/webp", _ => throw new ArgumentException("Use PNG, JPEG or WebP.") };
    }

    public async Task<ImageEditResult> ProcessAsync(ImageEditRequest body, CancellationToken ct, Func<ImageEditAccounting, Task>? recordUsage = null)
    {
        if (!_options.Enabled || !Configured) throw new InvalidOperationException("Image editing is disabled or unconfigured.");
        if (body.Mode is not ("process" or "enhance" or "objects")) throw new ArgumentException("Choose an explicit editing operation; automatic detection is disabled.");
        if (body.AuthorizationBasis is not ("owned" or "licensed")) throw new ArgumentException("Photo editing requires ownership or permission.");
        if (body.AuthorizationBasis == "licensed" && string.IsNullOrWhiteSpace(body.PermissionReference)) throw new ArgumentException("Provide your written permission reference.");
        if (_options.Quality is not ("low" or "medium" or "high" or "auto") ||
            _options.Size is not ("1024x1024" or "1536x1024" or "1024x1536" or "auto")) throw new InvalidOperationException("Unsupported image output configuration.");
        var bytes = Convert.FromBase64String(body.ImageB64);
        ValidateImage(bytes);
        using var original = DecodeOriented(bytes);
        // Normalize EXIF orientation before the provider sees the image. Masks use displayed dimensions.
        using var canonicalImage = SKImage.FromBitmap(original);
        using var canonicalData = canonicalImage.Encode(SKEncodedImageFormat.Png, 100);
        var providerBytes = canonicalData.ToArray();
        byte[]? maskBytes = body.MaskB64 is null ? null : Convert.FromBase64String(body.MaskB64);
        using var mask = maskBytes is null ? null : LoadMask(maskBytes, original.Width, original.Height);
        if (body.Mode == "objects" && (mask is null || string.IsNullOrWhiteSpace(body.Instructions))) throw new ArgumentException("Object removal needs a mask and a description.");
        // /Hdreal and /RemoveWaterMark are product intent, not undocumented API flags.
        var instruction = body.Mode switch
        {
            "enhance" => "Make subtle, photorealistic exposure, white balance and clarity corrections only. Do not add or remove objects, conceal defects or change the condition, dimensions or appearance of the property.",
            "objects" => "Remove only the unwanted movable object described by the uploader within the transparent mask region: " + body.Instructions + ". Do not remove structural features, permanent fixtures, damage or defects.",
            _ => "Remove only the uploader-owned or explicitly authorized overlaid watermark marks. " + body.Instructions + ". Do not remove unrelated signs, artwork or text."
        };
        var prompt = instruction + " Preserve composition, framing, architecture, furniture, materials, textures, lighting and all unrelated areas as closely as possible. Maintain realistic detail; do not restyle, stage, invent amenities or misrepresent this property. If masked, change only fully transparent pixels; opaque pixels must remain unchanged.";
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(_options.Model!), "model");
        content.Add(new StringContent(prompt), "prompt");
        content.Add(new StringContent("1"), "n");
        content.Add(new StringContent(_options.Size), "size");
        content.Add(new StringContent(_options.Quality), "quality");
        content.Add(new StringContent("png"), "output_format");
        var imageContent = new ByteArrayContent(providerBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "photo.png");
        if (maskBytes is not null)
        {
            var maskContent = new ByteArrayContent(maskBytes);
            maskContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(maskContent, "mask", "mask.png");
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/edits") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
        // Exactly one attempt. Ambiguous timeouts may already have been billed.
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            string? code = null;
            try
            {
                using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (error.RootElement.TryGetProperty("error", out var detail) && detail.ValueKind == JsonValueKind.Object &&
                    detail.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var candidate = value.GetString();
                    // Never expose the provider's free-text error, which can echo submitted content.
                    if (candidate is "server_is_overloaded" or "rate_limit_exceeded" or "insufficient_quota" or
                        "billing_hard_limit_reached" or "content_policy_violation" or "moderation_blocked" or
                        "invalid_api_key" or "model_not_found" or "invalid_request_error") code = candidate;
                }
            }
            catch (JsonException) { }
            var message = status >= 500 ? $"OpenAI is temporarily unavailable (HTTP {status}). No automatic retry was made. An explicit retry may incur another charge."
                : status == 429 ? "OpenAI rejected the request due to an account rate or usage limit. Check the account before explicitly retrying."
                : $"OpenAI rejected the edit (HTTP {status}). Review the image and account configuration before explicitly retrying.";
            throw new ImageEditException(status, message, code,
                response.Headers.TryGetValues("x-request-id", out var requestIds) ? requestIds.FirstOrDefault() : null);
        }
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        string? StringField(string name) => json.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        var usageElement = json.RootElement.TryGetProperty("usage", out var usageValue) ? usageValue.Clone() : (JsonElement?)null;
        var responseModel = StringField("model");
        // Unknown or conflicting response models never inherit another model's rates.
        var pricedModel = responseModel ?? _options.Model;
        var accounting = new ImageEditAccounting(_options.Model!, _options.Quality, _options.Size,
            responseModel, StringField("quality"), StringField("size"),
            response.Headers.TryGetValues("x-request-id", out var ids) ? ids.FirstOrDefault() : null,
            usageElement, usageElement is { } found ? OpenAIImageCostCalculator.Calculate(pricedModel, found) : null);
        // Persist usage before decoding/compositing or storage, which can fail after the API has billed.
        if (recordUsage is not null) await recordUsage(accounting);
        var encoded = json.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString() ?? throw new InvalidOperationException("OpenAI returned no image.");
        var output = Convert.FromBase64String(encoded);
        ValidateImage(output);
        using var generated = SKBitmap.Decode(output) ?? throw new InvalidOperationException("Invalid OpenAI image.");
        using var edited = new SKBitmap(original.Width, original.Height);
        // Return original dimensions. With masks, copy only requested transparent pixels.
        using (var canvas = new SKCanvas(edited)) canvas.DrawBitmap(generated, new SKRect(0, 0, original.Width, original.Height), new SKSamplingOptions(SKFilterMode.Linear));
        if (mask is not null)
        {
            for (var y = 0; y < original.Height; y++)
                for (var x = 0; x < original.Width; x++)
                    if (mask.GetPixel(x, y).Alpha != 0) edited.SetPixel(x, y, original.GetPixel(x, y));
        }
        using var resultImage = SKImage.FromBitmap(edited);
        using var encodedOutput = resultImage.Encode(SKEncodedImageFormat.Png, 100);
        output = encodedOutput.ToArray();
        return new ImageEditResult
        {
            SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            ProcessedB64 = Convert.ToBase64String(output),
            ProcessedSha256 = Convert.ToHexStringLower(SHA256.HashData(output)),
            ContentType = "image/png", Action = "processed", Classification = "authorized_edit",
            PipelineVersion = CacheVersion, Accounting = accounting,
            Width = original.Width, Height = original.Height,
            UsageJson = json.RootElement.TryGetProperty("usage", out var usage) ? usage.GetRawText() : null
        };
    }
    private static SKBitmap DecodeOriented(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new ArgumentException("Cannot decode image.");
        var source = SKBitmap.Decode(bytes) ?? throw new ArgumentException("Cannot decode image.");
        var origin = codec.EncodedOrigin;
        if (origin == SKEncodedOrigin.TopLeft) return source;
        using (source)
        {
            var w = source.Width; var h = source.Height;
            var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var target = new SKBitmap(swap ? h : w, swap ? w : h);
            for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
            {
                var (dx, dy) = origin switch
                {
                    SKEncodedOrigin.TopRight => (w - 1 - x, y),
                    SKEncodedOrigin.BottomRight => (w - 1 - x, h - 1 - y),
                    SKEncodedOrigin.BottomLeft => (x, h - 1 - y),
                    SKEncodedOrigin.LeftTop => (y, x),
                    SKEncodedOrigin.RightTop => (h - 1 - y, x),
                    SKEncodedOrigin.RightBottom => (h - 1 - y, w - 1 - x),
                    SKEncodedOrigin.LeftBottom => (y, w - 1 - x),
                    _ => (x, y)
                };
                target.SetPixel(dx, dy, source.GetPixel(x, y));
            }
            return target;
        }
    }
    private static SKBitmap LoadMask(byte[] bytes, int width, int height)
    {
        ValidateImage(bytes, true);
        var mask = SKBitmap.Decode(bytes) ?? throw new ArgumentException("Cannot decode image.");
        if (mask.Width != width || mask.Height != height) { mask.Dispose(); throw new ArgumentException("Mask dimensions must match the original."); }
        var transparent = false;
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) transparent |= mask.GetPixel(x, y).Alpha == 0;
        if (!transparent) { mask.Dispose(); throw new ArgumentException("Mask must contain transparent pixels to edit."); }
        return mask;
    }
}
public sealed class ImageEditException(int statusCode, string message, string? code = null, string? requestId = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string? Code { get; } = code;
    public string? RequestId { get; } = requestId;
}
public sealed class ImageEditRequest
{
    public string ImageB64 { get; set; } = "";
    public string Mode { get; set; } = "process";
    public string AuthorizationBasis { get; set; } = "unknown";
    public string? PermissionReference { get; set; }
    public string? MaskB64 { get; set; }
    public string? Instructions { get; set; }
}
public sealed class ImageEditResult
{
    public string SourceSha256 { get; set; } = "";
    public string? ProcessedB64 { get; set; }
    public string? ProcessedSha256 { get; set; }
    public string? ContentType { get; set; }
    public string Action { get; set; } = "";
    public string Classification { get; set; } = "";
    public string? PipelineVersion { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public ImageEditAccounting? Accounting { get; set; }
    public string? UsageJson { get; set; }
    public JsonElement Detections { get; set; } = JsonSerializer.SerializeToElement(Array.Empty<object>());
    public JsonElement? SafetyReport { get; set; }
    public JsonElement? Validation { get; set; }
}
