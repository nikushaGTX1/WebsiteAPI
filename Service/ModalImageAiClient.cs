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

        var result = await response.Content.ReadFromJsonAsync<ModalProcessResult>(
                         Json,
                         cancellationToken)
                     ?? throw new InvalidOperationException("Empty Modal response.");
        if (!string.IsNullOrWhiteSpace(result.PipelineVersion))
        {
            RememberVersion(result.PipelineVersion);
        }
        return result;
    }

    // The deployed remover's version, asked from /health at most every few minutes.
    private static readonly object VersionLock = new();
    private static string? _knownVersion;
    private static DateTime _knownVersionAt;
    private static readonly TimeSpan VersionLifetime = TimeSpan.FromMinutes(5);

    private static void RememberVersion(string version)
    {
        lock (VersionLock)
        {
            _knownVersion = version;
            _knownVersionAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Cache key for answers: the remover's version plus the brand list sent to it. Null when the
    /// version cannot be learned; then no cached answer is reused.
    /// </summary>
    public async Task<string?> CurrentCacheVersionAsync(CancellationToken cancellationToken)
    {
        string? version;
        lock (VersionLock)
        {
            version = DateTime.UtcNow - _knownVersionAt < VersionLifetime ? _knownVersion : null;
        }

        if (version is null && !string.IsNullOrWhiteSpace(_options.Endpoint) &&
            !string.IsNullOrWhiteSpace(_options.Token))
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get, $"{_options.Endpoint.TrimEnd('/')}/health");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                using var response = await _httpClient.SendAsync(request, timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                    if (doc.RootElement.TryGetProperty("pipeline_version", out var value) &&
                        value.GetString() is { Length: > 0 } found)
                    {
                        RememberVersion(found);
                        version = found;
                    }
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                // Unknown version: callers then skip the cache, which costs GPU time but is never stale.
            }
        }

        return CacheVersionFor(version);
    }

    /// <summary>The cache key for an answer made by the given remover version.</summary>
    public string? CacheVersionFor(string? pipelineVersion)
    {
        if (string.IsNullOrWhiteSpace(pipelineVersion)) return null;
        var terms = string.Join(",", _options.RemovableBrandTerms.Select(term => term.Trim().ToLowerInvariant()).Order());
        var termsHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(terms)))[..8];
        return $"{pipelineVersion}:{termsHash}";
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
    /// <summary>Partner the photo is known to come from (e.g. "myhome"), or null.</summary>
    public string? KnownSource { get; set; }
    public string WatermarkScope { get; set; } = "brands";
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
    public string? PipelineVersion { get; set; }
    public double? MaskPercent { get; set; }
    public double? GpuPeakMemoryMb { get; set; }
    public JsonElement? MaskBoxes { get; set; }
    public JsonElement? SafetyReport { get; set; }
    public JsonElement? Timings { get; set; }
}
