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
        // Images in email need an absolute URL; the logo is served by the website.
        var siteUrl = (_config["Site:BaseUrl"] ?? "https://velven.ge").TrimEnd('/');
        var safeHeading = System.Net.WebUtility.HtmlEncode(heading);
        var safeIntro = System.Net.WebUtility.HtmlEncode(intro);
        var year = DateTime.UtcNow.Year;
        // Table layout + inline styles: the most reliable markup across Gmail, Outlook and phones.
        var html = $"""
            <!doctype html>
            <html><body style="margin:0;padding:0;background:#f4f1fb">
            <span style="display:none;max-height:0;overflow:hidden;opacity:0">{code} — {safeIntro}</span>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f1fb;padding:32px 12px;font-family:Arial,Helvetica,sans-serif">
              <tr><td align="center">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:480px;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 12px 40px rgba(37,20,74,0.12)">
                  <tr><td align="center" style="background:linear-gradient(135deg,#451a8f,#6a3fd0);background-color:#451a8f;padding:28px 24px">
                    <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                      <td style="padding-right:10px;vertical-align:middle"><img src="{siteUrl}/logosh2-mark-v2.png" width="34" alt="Velven" style="display:block;width:34px;height:auto;border:0;background:#ffffff;border-radius:10px;padding:5px" /></td>
                      <td style="vertical-align:middle;color:#ffffff;font-size:22px;font-weight:700;letter-spacing:1px">VELVEN</td>
                    </tr></table>
                  </td></tr>
                  <tr><td style="padding:32px 32px 8px;color:#1b1626">
                    <h1 style="margin:0 0 10px;font-size:22px;line-height:1.3;color:#160f2b">{safeHeading}</h1>
                    <p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#4b4560">{safeIntro}</p>
                    <div style="background:#f5f1ff;border:1px solid #e4dcf0;border-radius:16px;padding:20px 12px;text-align:center">
                      <div style="font-size:12px;font-weight:700;letter-spacing:2px;color:#7a6e8e;text-transform:uppercase;margin-bottom:8px">Your code</div>
                      <div style="font-size:36px;font-weight:700;letter-spacing:10px;color:#451a8f;font-family:'Courier New',Courier,monospace">{code}</div>
                    </div>
                    <p style="margin:20px 0 0;font-size:13px;line-height:1.6;color:#7d8598">This code expires in a few minutes. If you didn't request it, you can safely ignore this email — your account stays secure.</p>
                  </td></tr>
                  <tr><td style="padding:24px 32px 28px">
                    <div style="border-top:1px solid #eee9f6;padding-top:18px;font-size:12px;line-height:1.6;color:#9a93ab;text-align:center">
                      <a href="{siteUrl}" style="color:#451a8f;font-weight:700;text-decoration:none">velven.ge</a> · Tbilisi, Georgia<br />© {year} Velven
                    </div>
                  </td></tr>
                </table>
              </td></tr>
            </table>
            </body></html>
            """;
        // Code in the subject: easy to read from the notification, and stops Gmail from
        // collapsing repeated emails into a "•••" block.
        return SendAsync(to, $"{code} is your {subject.Replace("Your ", "")}", html, cancellationToken);
    }
}
