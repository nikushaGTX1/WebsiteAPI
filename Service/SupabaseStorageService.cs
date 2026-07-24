using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Website_API.Services;

public sealed class SupabaseStorageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SupabaseStorageService> _logger;

    private readonly string _supabaseUrl;
    private readonly string _secretKey;
    private readonly string _bucket;

    public SupabaseStorageService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SupabaseStorageService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _supabaseUrl = configuration["Supabase:Url"]
            ?? throw new InvalidOperationException(
                "Supabase:Url is missing.");

        _secretKey = configuration["Supabase:SecretKey"]
            ?? throw new InvalidOperationException(
                "Supabase:SecretKey is missing.");

        _bucket = configuration["Supabase:Bucket"]
            ?? throw new InvalidOperationException(
                "Supabase:Bucket is missing.");
    }

    public async Task<string?> UploadImageAsync(
        IFormFile? image,
        CancellationToken cancellationToken = default)
    {
        if (image is null || image.Length == 0)
        {
            return null;
        }

        ValidateImage(image);

        var extension = GetExtension(image.ContentType);

        // This object path is stored in PostgreSQL.
        var objectPath =
            $"apartments/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";

        var encodedPath = EncodeObjectPath(objectPath);

        var requestUrl =
            $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/{_bucket}/{encodedPath}";

        await using var imageStream = image.OpenReadStream();

        using var content = new StreamContent(imageStream);

        content.Headers.ContentType =
            new MediaTypeHeaderValue(image.ContentType);

        using var request =
            new HttpRequestMessage(HttpMethod.Post, requestUrl);

        // New sb_secret keys should be sent using the apikey header.
        request.Headers.TryAddWithoutValidation(
            "apikey",
            _secretKey
        );

        request.Headers.TryAddWithoutValidation(
            "x-upsert",
            "false"
        );

        request.Content = content;

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken
        );

        var responseText = await response.Content.ReadAsStringAsync(
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Supabase upload failed. Status: {Status}. Response: {Response}",
                response.StatusCode,
                responseText
            );

            throw new InvalidOperationException(
                $"Could not upload image to Supabase. Status: {(int)response.StatusCode}"
            );
        }

        return objectPath;
    }

    public async Task<string?> CreateSignedUrlAsync(
        string? objectPath,
        int expiresInSeconds = 3600,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
        {
            return null;
        }

        // Support old Railway image values without crashing.
        if (objectPath.StartsWith(
                "uploads/",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Uri.TryCreate(
                objectPath,
                UriKind.Absolute,
                out var existingUrl))
        {
            return existingUrl.ToString();
        }

        var encodedPath = EncodeObjectPath(objectPath);

        var requestUrl =
            $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/sign/{_bucket}/{encodedPath}";

        var json = JsonSerializer.Serialize(new
        {
            expiresIn = expiresInSeconds
        });

        using var request =
            new HttpRequestMessage(HttpMethod.Post, requestUrl);

        request.Headers.TryAddWithoutValidation(
            "apikey",
            _secretKey
        );

        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json"
        );

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken
        );

        var responseText = await response.Content.ReadAsStringAsync(
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Supabase signed URL failed. Status: {Status}. Response: {Response}",
                response.StatusCode,
                responseText
            );

            return null;
        }

        using var document = JsonDocument.Parse(responseText);

        if (!document.RootElement.TryGetProperty(
                "signedURL",
                out var signedUrlElement) &&
            !document.RootElement.TryGetProperty(
                "signedUrl",
                out signedUrlElement))
        {
            return null;
        }

        var signedUrl = signedUrlElement.GetString();

        if (string.IsNullOrWhiteSpace(signedUrl))
        {
            return null;
        }

        if (signedUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return signedUrl;
        }

        return $"{_supabaseUrl.TrimEnd('/')}{signedUrl}";
    }

    public async Task DeleteImageAsync(
        string? objectPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectPath) ||
            objectPath.StartsWith(
                "uploads/",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var requestUrl =
            $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/{_bucket}";

        var json = JsonSerializer.Serialize(new
        {
            prefixes = new[] { objectPath }
        });

        using var request =
            new HttpRequestMessage(HttpMethod.Delete, requestUrl);

        request.Headers.TryAddWithoutValidation(
            "apikey",
            _secretKey
        );

        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json"
        );

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            var responseText =
                await response.Content.ReadAsStringAsync(
                    cancellationToken
                );

            _logger.LogWarning(
                "Supabase delete failed. Status: {Status}. Response: {Response}",
                response.StatusCode,
                responseText
            );
        }
    }

    private static void ValidateImage(IFormFile image)
    {
        const long maximumSize = 8 * 1024 * 1024;

        if (image.Length > maximumSize)
        {
            throw new InvalidOperationException(
                "Image cannot be larger than 8 MB.");
        }

        string[] acceptedTypes =
        [
            "image/jpeg",
            "image/png",
            "image/webp"
        ];

        if (!acceptedTypes.Contains(
                image.ContentType,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only JPG, PNG and WebP images are allowed.");
        }
    }

    private static string GetExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException(
                "Unsupported image type.")
        };
    }

    private static string EncodeObjectPath(string objectPath)
    {
        return string.Join(
            "/",
            objectPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString)
        );
    }
}