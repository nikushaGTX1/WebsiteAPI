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

        _supabaseUrl =
            configuration["Supabase:Url"]?.TrimEnd('/')
            ?? throw new InvalidOperationException(
                "Supabase:Url is missing.");

        _secretKey =
            configuration["Supabase:SecretKey"]
            ?? throw new InvalidOperationException(
                "Supabase:SecretKey is missing.");

        _bucket =
            configuration["Supabase:Bucket"]
            ?? throw new InvalidOperationException(
                "Supabase:Bucket is missing.");
    }

    public async Task<string?> UploadImageAsync(
        IFormFile? image,
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        if (image is null || image.Length == 0)
        {
            _logger.LogWarning(
                "Supabase upload skipped because no image was received.");

            return null;
        }

        ValidateImage(image);

        var extension = GetExtension(image.ContentType);

        var normalizedFolder = string.IsNullOrWhiteSpace(folder)
            ? string.Empty
            : $"{folder.Trim().Trim('/')}/";

        var objectPath =
            $"{normalizedFolder}{DateTime.UtcNow:yyyy/MM}/" +
            $"{Guid.NewGuid():N}{extension}";

        var encodedBucket = Uri.EscapeDataString(_bucket);
        var encodedPath = EncodeObjectPath(objectPath);

        var requestUrl =
            $"{_supabaseUrl}/storage/v1/object/" +
            $"{encodedBucket}/{encodedPath}";

        _logger.LogInformation(
            "Uploading image {FileName} to Supabase path {ObjectPath}.",
            image.FileName,
            objectPath);

        await using var imageStream = image.OpenReadStream();
        using var content = new StreamContent(imageStream);

        content.Headers.ContentType =
            new MediaTypeHeaderValue(image.ContentType);

        using var request =
            new HttpRequestMessage(HttpMethod.Post, requestUrl);

        // Correct for the new sb_secret_... key.
        request.Headers.TryAddWithoutValidation(
            "apikey",
            _secretKey);

        request.Headers.TryAddWithoutValidation(
            "x-upsert",
            "false");

        request.Content = content;

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        var responseText =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Supabase upload failed. Status: {Status}. Response: {Response}",
                response.StatusCode,
                responseText);

            throw new InvalidOperationException(
                $"Supabase upload failed with status " +
                $"{(int)response.StatusCode}: {responseText}");
        }

        _logger.LogInformation(
            "Supabase upload succeeded. Stored path: {ObjectPath}",
            objectPath);

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

        // Old Railway-local files cannot be signed by Supabase.
        if (objectPath.StartsWith(
                "uploads/",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Uri.TryCreate(
                objectPath,
                UriKind.Absolute,
                out var absoluteUrl))
        {
            return absoluteUrl.ToString();
        }

        var encodedBucket = Uri.EscapeDataString(_bucket);
        var encodedPath = EncodeObjectPath(objectPath);

        var requestUrl =
            $"{_supabaseUrl}/storage/v1/object/sign/" +
            $"{encodedBucket}/{encodedPath}";

        var json = JsonSerializer.Serialize(new
        {
            expiresIn = expiresInSeconds
        });

        using var request =
            new HttpRequestMessage(HttpMethod.Post, requestUrl);

        request.Headers.TryAddWithoutValidation(
            "apikey",
            _secretKey);

        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        var responseText =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Supabase signed URL failed. Status: {Status}. Response: {Response}",
                response.StatusCode,
                responseText);

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
            _logger.LogWarning(
                "Supabase response did not contain a signed URL. Response: {Response}",
                responseText);

            return null;
        }

        var signedUrl = signedUrlElement.GetString();

        if (string.IsNullOrWhiteSpace(signedUrl))
        {
            return null;
        }

        if (signedUrl.StartsWith(
                "http",
                StringComparison.OrdinalIgnoreCase))
        {
            return signedUrl;
        }

        // Supabase commonly returns /object/sign/...,
        // so /storage/v1 must be included.
        if (signedUrl.StartsWith(
                "/object/",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"{_supabaseUrl}/storage/v1{signedUrl}";
        }

        if (!signedUrl.StartsWith('/'))
        {
            signedUrl = $"/{signedUrl}";
        }

        return $"{_supabaseUrl}{signedUrl}";
    }

    public async Task DeleteImageAsync(
        string? objectPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
        {
            return;
        }

        if (objectPath.StartsWith(
                "uploads/",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Uri.TryCreate(
                objectPath,
                UriKind.Absolute,
                out _))
        {
            _logger.LogWarning(
                "Supabase deletion skipped because ImageUrl is an absolute URL.");

            return;
        }

        var encodedBucket = Uri.EscapeDataString(_bucket);

        var requestUrl =
            $"{_supabaseUrl}/storage/v1/object/{encodedBucket}";

        var json = JsonSerializer.Serialize(new
        {
            prefixes = new[] { objectPath }
        });

        using var request =
            new HttpRequestMessage(HttpMethod.Delete, requestUrl);

        request.Headers.TryAddWithoutValidation(
            "apikey",
            _secretKey);

        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        var responseText =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Supabase deletion failed. Status: {Status}. Response: {Response}",
                response.StatusCode,
                responseText);

            return;
        }

        _logger.LogInformation(
            "Deleted Supabase object {ObjectPath}.",
            objectPath);
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
                $"Unsupported image type: {image.ContentType}. " +
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
                $"Unsupported image content type: {contentType}")
        };
    }

    private static string EncodeObjectPath(string objectPath)
    {
        return string.Join(
            "/",
            objectPath
                .Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
    }
}
