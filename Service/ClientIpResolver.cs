using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Website_API.Services;

// Finds the visitor IP for rate limits without trusting headers a client can forge.
public static class ClientIpResolver
{
    public const string ClientIpHeader = "X-Velven-Client-Ip";
    public const string ProxyKeyHeader = "X-Velven-Proxy-Key";

    public static string Resolve(HttpContext context)
    {
        var sharedKey = context.RequestServices
            .GetService<IConfiguration>()?["Proxy:SharedKey"];

        if (string.IsNullOrWhiteSpace(sharedKey))
        {
            // Legacy behaviour until Proxy__SharedKey is set on the API and the site.
            return Parse(FirstForwarded(context)) ?? Remote(context);
        }

        // The site proxy proves itself with the shared key and passes the visitor IP.
        if (KeyMatches(context.Request.Headers[ProxyKeyHeader].ToString(), sharedKey) &&
            Parse(context.Request.Headers[ClientIpHeader].ToString()) is { } proxied)
        {
            return proxied;
        }

        // Direct callers: Railway's edge appends the real peer as the last entry,
        // so only that entry is trusted; earlier entries are client-controlled.
        return Parse(LastForwarded(context)) ?? Remote(context);
    }

    private static bool KeyMatches(string supplied, string expected) =>
        supplied.Length > 0 &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(expected));

    private static string FirstForwarded(HttpContext context) =>
        context.Request.Headers["X-Forwarded-For"].ToString().Split(',', 2)[0].Trim();

    private static string LastForwarded(HttpContext context) =>
        context.Request.Headers["X-Forwarded-For"].ToString().Split(',').Last().Trim();

    private static string? Parse(string value) =>
        IPAddress.TryParse(value, out var ip) ? ip.ToString() : null;

    private static string Remote(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
