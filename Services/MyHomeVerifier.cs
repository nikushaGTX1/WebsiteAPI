using System.Text.Json;
using System.Text.RegularExpressions;
namespace Website_API.Services;
public record MyHomeResult(string Status, string Detail, int? HttpStatus = null);
public class MyHomeVerifier(HttpClient client)
{
    public static bool ValidId(string? id) => id is not null && Regex.IsMatch(id, @"^[1-9][0-9]{4,7}$");
    // Adapted from Nikas-Extension/background.js scrapeRenderedListing:
    // Cloudflare guard, Georgian deleted message, and exact hydrated statement identity.
    public static MyHomeResult Classify(string id, int status, string html)
    {
        if (status is 401 or 403 or 429 || Regex.IsMatch(html, @"just a moment|enable javascript and cookies|captcha|cf-chl-|access denied", RegexOptions.IgnoreCase))
            return new("UNCERTAIN", "Access restricted or verification challenge", status);
        if (status is 404 or 410) return new("REMOVED", "Public listing returned HTTP " + status, status);
        if (status >= 500) return new("ERROR", "Upstream server failure", status);
        if (status != 200) return new("UNCERTAIN", "Unexpected HTTP response or redirect", status);
        // Only visible text counts; embedded translation dictionaries contain this same phrase.
        var visible = Regex.Replace(html, @"<(script|style)\b[^>]*>[\s\S]*?</\1>", "", RegexOptions.IgnoreCase);
        visible = System.Net.WebUtility.HtmlDecode(Regex.Replace(visible, "<[^>]+>", " "));
        visible = Regex.Replace(visible, @"\s+", " ");
        if (visible.Contains("განცხადება რომელსაც ეძებდით წაშლილი ან არააქტიურია"))
            return new("REMOVED", "MyHome explicitly reports deleted or inactive listing", status);
        foreach (Match script in Regex.Matches(html, @"<script\b(?<attrs>[^>]*)>(?<json>[\s\S]*?)</script>", RegexOptions.IgnoreCase))
        {
            if (!Regex.IsMatch(script.Groups["attrs"].Value, "__NEXT_DATA__|application/json", RegexOptions.IgnoreCase)) continue;
            try
            {
                using var doc = JsonDocument.Parse(script.Groups["json"].Value);
                if (!doc.RootElement.TryGetProperty("props", out var props) || !props.TryGetProperty("pageProps", out var page) ||
                    !page.TryGetProperty("dehydratedState", out var state) || !state.TryGetProperty("queries", out var queries) || queries.ValueKind != JsonValueKind.Array) continue;
                foreach (var query in queries.EnumerateArray())
                {
                    if (!query.TryGetProperty("state", out var qs) || !qs.TryGetProperty("data", out var data) || !data.TryGetProperty("data", out var inner) || !inner.TryGetProperty("statement", out var statement) || statement.ValueKind != JsonValueKind.Object) continue;
                    foreach (var key in new[] { "statement_id", "listing_id", "id" })
                        if (statement.TryGetProperty(key, out var value) && value.ToString() == id &&
                            (statement.TryGetProperty("price", out _) || statement.TryGetProperty("area", out _) || statement.TryGetProperty("total_area", out _)))
                            return new("ACTIVE", "Exact listing found in public hydrated statement", status);
                }
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException) { }
        }
        return new("UNCERTAIN", "Public page contains no reliable listing evidence", status);
    }
    public async Task<MyHomeResult> Verify(string? id, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(id)) return new("NO_MYHOME_ID", "No linked MyHome ID");
        if (!ValidId(id)) return new("ERROR", "Invalid MyHome ID");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var url = new Uri($"https://www.myhome.ge/ka/pr/{id}");
                // Follow only public same-site redirects, never login or another host.
                for (var redirect = 0; redirect < 4; redirect++)
                {
                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                    var status = (int)response.StatusCode;
                    if (status is 301 or 302 or 303 or 307 or 308 && response.Headers.Location is { } location)
                    {
                        var next = new Uri(url, location);
                        if (next.Scheme != "https" || next.Host != "www.myhome.ge" && next.Host != "myhome.ge" ||
                            !(Regex.IsMatch(next.AbsolutePath, $@"/pr/{id}(?:/|$)") || Regex.IsMatch(next.AbsolutePath, $@"^/udzravi-qoneba/[^/]*-{id}/?$")))
                            return new("UNCERTAIN", "Redirect left the public listing", status);
                        url = next; continue;
                    }
                    if (status >= 500 && attempt < 2) break;
                    if (response.Content.Headers.ContentLength > 4_000_000) return new("UNCERTAIN", "Response exceeds safe size", status);
                    using var stream = await response.Content.ReadAsStreamAsync(token);
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                    deadline.CancelAfter(TimeSpan.FromSeconds(20));
                    using var memory = new MemoryStream();
                    var buffer = new byte[8192]; int count;
                    while ((count = await stream.ReadAsync(buffer, deadline.Token)) > 0) {
                        if (memory.Length + count > 4_000_000) return new("UNCERTAIN", "Response exceeds safe size", status);
                        memory.Write(buffer, 0, count);
                    }
                    return Classify(id, status, System.Text.Encoding.UTF8.GetString(memory.ToArray()));
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { if (attempt == 2) return new("ERROR", "Request timed out"); }
            catch (HttpRequestException) { if (attempt == 2) return new("ERROR", "Network request failed"); }
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt) + Random.Shared.NextDouble()), token);
        }
        return new("ERROR", "Retry or redirect limit reached");
    }
}
