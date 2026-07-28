using Npgsql;

namespace Website_API.Data;

public static class DatabaseConnection
{
    public static string Resolve(IConfiguration? configuration = null)
    {
        var value =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default") ??
            Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL") ??
            Environment.GetEnvironmentVariable("DATABASE_URL") ??
            configuration?.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "No database connection is configured. Set " +
                "ConnectionStrings__Default, DATABASE_PUBLIC_URL, or DATABASE_URL.");
        }

        return ToNpgsqlConnectionString(value);
    }

    public static string ToNpgsqlConnectionString(string value)
    {
        value = value.Trim();

        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);

        if (userInfo.Length != 2)
        {
            throw new InvalidOperationException(
                "The PostgreSQL URL does not contain a username and password.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1]),
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }
}
