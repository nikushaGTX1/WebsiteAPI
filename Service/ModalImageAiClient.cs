using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Website_API.Services;

public sealed class ModalImageAiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;
    private readonly ImageAiOptions _options;

    public ModalImageAiClient(HttpClient httpClient, IOptions<ImageAiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
    }

    public async Task<ModalProcessResult> ProcessAsync(
        ModalProcessRequest body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new InvalidOperationException(
                "ImageAi__Endpoint and ImageAi__Token must be configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.Endpoint.TrimEnd('/')}/v1/process");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.Token);
        request.Content = JsonContent.Create(body, options: Json);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ModalImageAiException(
                (int)response.StatusCode,
                text.Length > 500 ? text[..500] : text);
        }

        return await response.Content.ReadFromJsonAsync<ModalProcessResult>(
                   Json,
                   cancellationToken)
               ?? throw new InvalidOperationException("Empty Modal response.");
    }
}

public sealed class ModalImageAiException(int statusCode, string body)
    : Exception($"Modal returned {statusCode}: {body}")
{
    // 4xx (except 408/429) means the input is bad; retrying will not help.
    public bool IsPermanent =>
        statusCode is >= 400 and < 500 and not 408 and not 429;
}

public sealed class ModalProcessRequest
{
    public string ImageB64 { get; set; } = string.Empty;
    public string? ExpectedSha256 { get; set; }
    public string Mode { get; set; } = "detect";
    public string AuthorizationBasis { get; set; } = "unknown";
    public string? PermissionReference { get; set; }
    public string[] OwnBrandTerms { get; set; } = [];
}

public sealed class ModalProcessResult
{
    public string SourceSha256 { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public JsonElement Detections { get; set; }
    public string Classification { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool ColdStart { get; set; }
    public JsonElement? Validation { get; set; }
    public string? ContentType { get; set; }

    [JsonPropertyName("processed_b64")]
    public string? ProcessedB64 { get; set; }

    public string? ProcessedSha256 { get; set; }
    public double GpuSeconds { get; set; }
}
