using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// P-03 (42.3, audit externe DS-ARCH-03) — <see cref="IUsageProvider"/> en LECTURE SEULE adossé au
/// <see cref="RefreshOrchestrator"/>. Il n'appelle JAMAIS la chaîne d'usage : il rend le dernier snapshot publié par
/// l'orchestrateur, c'est-à-dire exactement ce que le cadran affiche.
///
/// <para>Pourquoi : le diagnostic interrogeait la TÊTE de chaîne en parallèle de l'orchestrateur (au démarrage, pendant la
/// charge initiale ; et depuis les Réglages) — double sonde possible, last-exact.json disputé, fausse ligne
/// « ecriture_ratee ». Avec cet adaptateur, l'orchestrateur reste le consommateur UNIQUE de la chaîne.</para>
///
/// <para>Tant que rien n'est publié (charge initiale en cours), il attend au plus le délai donné, puis lève une
/// <see cref="InvalidOperationException"/> dont le message atterrit dans « (échec de lecture : …) » du rapport : le rapport dit
/// « pas encore de relevé publié », il n'invente jamais un chiffre. Neutre (aucun type WPF).</para>
/// </summary>
public sealed class DernierSnapshotPublie : IUsageProvider
{
    private static readonly TimeSpan DelaiParDefaut = TimeSpan.FromSeconds(15);

    private readonly RefreshOrchestrator _orchestrateur;
    private readonly TimeSpan _delai;

    /// <param name="orchestrateur">L'orchestrateur du conteneur (même instance que celle du cadran).</param>
    /// <param name="delai">Attente maximale du premier relevé publié (15 s par défaut).</param>
    public DernierSnapshotPublie(RefreshOrchestrator orchestrateur, TimeSpan? delai = null)
    {
        _orchestrateur = orchestrateur ?? throw new ArgumentNullException(nameof(orchestrateur));
        _delai = delai ?? DelaiParDefaut;
    }

    /// <summary>Rend le dernier snapshot publié ; n'appelle jamais la chaîne.</summary>
    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        var s = await _orchestrateur.AttendrePremierAsync(_delai, ct).ConfigureAwait(false);
        return s ?? throw new InvalidOperationException("pas encore de relevé publié par la chaîne (charge initiale en cours)");
    }
}
