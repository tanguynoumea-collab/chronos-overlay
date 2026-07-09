using Chronos.Models;
using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Fake IUsageProvider : compte les GetAsync (thread-safe).
/// Un gate optionnel (ManualResetEventSlim) permet de bloquer un GetAsync en vol pendant qu'on
/// empile des déclencheurs → sert à prouver la coalescence de l'orchestrateur (RAF-01).
/// Namespace Chronos.Tests pour rester cohérent avec FakeClock.</summary>
internal sealed class FakeUsageProvider : IUsageProvider
{
    private int _getCount;
    public int GetCount => Volatile.Read(ref _getCount);
    public UsageSnapshot Next = UsageSnapshot.Empty;

    /// <summary>Si posé, chaque GetAsync attend ce gate (test de coalescence).</summary>
    public ManualResetEventSlim? Gate;

    public Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _getCount);
        Gate?.Wait(ct);
        return Task.FromResult(Next);
    }
}
