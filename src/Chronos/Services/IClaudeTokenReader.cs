namespace Chronos.Services;

/// <summary>Lit et déchiffre l'access token OAuth du coffre de l'app bureau Claude, en mémoire
/// seulement (TOK-01/03). Toute anomalie → null, jamais d'exception (TOK-02). Lecture seule.</summary>
public interface IClaudeTokenReader
{
    /// <summary>Renvoie l'access token en clair (mémoire) + son expiration si connue, ou null
    /// sur tout échec. N'écrit rien, ne logue rien, ne lève rien.</summary>
    string? TryReadAccessToken(out DateTimeOffset? expiresAt);
}
