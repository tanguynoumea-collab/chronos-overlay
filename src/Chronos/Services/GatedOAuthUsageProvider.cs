using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Portillon (INT-03) autour du provider OAuth EXACT. À CHAQUE GetAsync, relit OAuthUsageEnabled
/// FRAIS depuis settings.json (comme JsonlEstimationProvider relit ses plafonds). Si désactivé,
/// retourne UsageSnapshot.Empty SANS JAMAIS appeler le provider interne — donc sans lire ni
/// déchiffrer le token, sans le moindre appel réseau. Off = v1.1 strict, ZÉRO accès token.
/// Type NEUTRE (aucun type WPF) : la garde de pureté Services/Models reste verte.
/// </summary>
public sealed class GatedOAuthUsageProvider : IUsageProvider
{
    private readonly IUsageProvider _inner;      // ClaudeOAuthUsageProvider (Exact)
    private readonly SettingsService _settings;

    public GatedOAuthUsageProvider(IUsageProvider inner, SettingsService settings)
    {
        _inner = inner;
        _settings = settings;
    }

    public Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        // Lecture FRAÎCHE : un toggle menu prend effet au prochain GetAsync sans redémarrage.
        if (!_settings.Load().OAuthUsageEnabled)
            return Task.FromResult(UsageSnapshot.Empty);   // court-circuit : le token n'est JAMAIS touché
        return _inner.GetAsync(ct);
    }
}
