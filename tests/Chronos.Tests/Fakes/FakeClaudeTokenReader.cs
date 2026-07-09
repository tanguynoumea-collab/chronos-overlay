using Chronos.Services;

namespace Chronos.Tests;

/// <summary>IClaudeTokenReader factice : renvoie le token/expiration configurés (ou null),
/// et compte les lectures — pour piloter l'inertie/court-circuit du provider dans les tests.</summary>
internal sealed class FakeClaudeTokenReader : IClaudeTokenReader
{
    public string? Token;
    public DateTimeOffset? Expires;
    public int ReadCount { get; private set; }

    public string? TryReadAccessToken(out DateTimeOffset? expiresAt)
    { ReadCount++; expiresAt = Expires; return Token; }
}
