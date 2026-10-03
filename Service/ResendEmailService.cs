using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Website_API.Services;

/// <summary>
/// Sends transactional email through the Resend HTTP API.
/// The key comes from configuration (Railway env var <c>Resend__ApiKey</c>) and is never sent to the browser.
/// </summary>
public class ResendEmailService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(HttpClient http, IConfiguration config, ILogger<ResendEmailService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_config["Resend:ApiKey"]);

    public async Task<bool> SendAsync(string to, string subject, string html, CancellationToken cancellationToken = default)
    {
        var apiKey = _config["Resend:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("Resend:ApiKey is not configured; email to {To} was not sent.", to);
            return false;
        }

        // Resend's shared sender only delivers to the account owner's address; set Resend:From to an
        // address on a verified domain (for example "Velven <no-reply@velven.ge>") for real users.
        var from = _config["Resend:From"] ?? "Velven <onboarding@resend.dev>";

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new { from, to = new[] { to }, subject, html }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode) return true;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Resend rejected email to {To}: {Status} {Body}", to, (int)response.StatusCode, body);
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not reach Resend for email to {To}.", to);
            return false;
        }
    }

    /// <summary>Branded email containing a 6-digit code.</summary>
    public Task<bool> SendCodeAsync(string to, string subject, string heading, string intro, string code, CancellationToken cancellationToken = default)
    {
        var html = $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:0 auto;padding:32px 24px;color:#1b1626">
              <div style="font-size:22px;font-weight:700;color:#451a8f;margin-bottom:24px">VELVEN</div>
              <h1 style="font-size:20px;margin:0 0 12px">{System.Net.WebUtility.HtmlEncode(heading)}</h1>
              <p style="font-size:15px;line-height:1.6;color:#4b4560;margin:0 0 24px">{System.Net.WebUtility.HtmlEncode(intro)}</p>
              <div style="font-size:32px;font-weight:700;letter-spacing:8px;background:#f5f1ff;border-radius:12px;padding:16px;text-align:center;color:#451a8f">{code}</div>
              <p style="font-size:13px;line-height:1.6;color:#7d8598;margin:24px 0 0">The code expires in a few minutes. If you didn't request it, you can ignore this email.</p>
            </div>
            """;
        return SendAsync(to, subject, html, cancellationToken);
    }
}
