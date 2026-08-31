namespace Rema.App.Data;

/// <summary>
/// Hjælper til at acceptere en forbindelse enten som Npgsql-nøgleformat
/// (<c>Host=...;Database=...;Username=...</c>) eller som en URL
/// (<c>postgres://bruger:kode@vært/db</c>), som fx Neon, Railway og Fly.io udleverer.
/// </summary>
public static class NpgsqlConnectionString
{
    public static string Normalize(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        raw = raw.Trim();

        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };

        // Uden eksplicit sslmode: Prefer virker både med udbydere der kræver TLS
        // (Neon) og med interne net uden TLS (Railways private netværk).
        var sslMode = GetQueryValue(uri.Query, "sslmode");
        builder.SslMode = sslMode?.ToLowerInvariant() switch
        {
            "disable" => Npgsql.SslMode.Disable,
            "allow" => Npgsql.SslMode.Allow,
            "prefer" => Npgsql.SslMode.Prefer,
            "require" => Npgsql.SslMode.Require,
            "verify-ca" => Npgsql.SslMode.VerifyCA,
            "verify-full" => Npgsql.SslMode.VerifyFull,
            _ => Npgsql.SslMode.Prefer,
        };

        return builder.ConnectionString;
    }

    private static string? GetQueryValue(string query, string key)
    {
        query = query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }
}
