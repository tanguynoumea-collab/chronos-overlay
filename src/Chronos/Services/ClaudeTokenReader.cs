namespace Chronos.Services;

/// <summary>Squelette temporaire (TDD RED) — l'implémentation arrive en phase GREEN.</summary>
public sealed class ClaudeTokenReader : IClaudeTokenReader
{
    public ClaudeTokenReader(string configJsonPath, string localStatePath) { }

    public string? TryReadAccessToken(out DateTimeOffset? expiresAt)
    {
        expiresAt = null;
        return null;
    }

    internal static string? DecryptAndSelectToken(byte[] aesKey, string tokenCacheB64, out DateTimeOffset? expiresAt)
    {
        expiresAt = null;
        return null;
    }
}
