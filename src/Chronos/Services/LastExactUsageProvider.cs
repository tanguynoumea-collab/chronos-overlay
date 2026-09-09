using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Décorateur d'<see cref="IUsageProvider"/> en tête de chaîne (EXA-01). Squelette RED : le
/// comportement est spécifié par LastExactUsageProviderTests et implémenté à l'étape GREEN.
/// </summary>
public sealed class LastExactUsageProvider : IUsageProvider
{
    private readonly IUsageProvider _inner;
    private readonly LastExactStore _store;
    private readonly IClock _clock;

    public LastExactUsageProvider(IUsageProvider inner, LastExactStore store, IClock clock)
    {
        _inner = inner;
        _store = store;
        _clock = clock;
    }

    public Task<UsageSnapshot> GetAsync(CancellationToken ct = default) => throw new NotImplementedException();
}
