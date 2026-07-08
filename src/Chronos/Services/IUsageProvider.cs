using Chronos.Models;

namespace Chronos.Services;

/// <summary>Contrat neutre isolant les sources d'usage du cadran (DAT-02). Aucun type WPF.</summary>
public interface IUsageProvider
{
    /// <summary>Lit la meilleure source disponible et produit un snapshot neutre.</summary>
    Task<UsageSnapshot> GetAsync(CancellationToken ct = default);

    /// <summary>Émis quand un nouveau snapshot est produit (thread pool — marshaling côté VM en Phase 4).</summary>
    event EventHandler<UsageSnapshot>? SnapshotChanged;
}
