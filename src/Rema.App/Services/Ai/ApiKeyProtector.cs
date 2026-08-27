using Microsoft.AspNetCore.DataProtection;

namespace Rema.App.Services.Ai;

/// <summary>
/// Krypterer/dekrypterer butikkens Anthropic API-nøgle. Nøglematerialet ligger i
/// databasens Data Protection-nøglering; selve API-nøglen gemmes kun krypteret.
/// </summary>
public sealed class ApiKeyProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("Rema.StoreAiApiKey.v1");

    public string Protect(string apiKey) => _protector.Protect(apiKey);

    /// <summary>Returnerer null hvis værdien ikke kan dekrypteres (fx roteret nøglering).</summary>
    public string? TryUnprotect(string? protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue)) return null;
        try { return _protector.Unprotect(protectedValue); }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }

    public static string Hint(string apiKey) =>
        apiKey.Length <= 4 ? "····" : "····" + apiKey[^4..];
}
