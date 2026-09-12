namespace Chronos.Services;

/// <summary>
/// Décorateur d'<see cref="ITranscriptActivitySource"/> à DURÉE DE VALIDITÉ. Raison d'être mesurée : une
/// passe réelle coûte 2,7 à 3,2 s et lit 536 Mo sur la machine cible ; au tick de 60 s, une lecture par
/// tick serait 536 Mo par minute d'E/S permanente sur un overlay. Le journal rendu par la phase 16 est
/// PUR et interrogeable N fois : le réutiliser est donc gratuit et sans perte.
///
/// La durée de validité est AUSSI la tolérance de certification : un journal dont le <c>Now</c> a 45 s ne
/// dit rien de ces 45 s, donc un « aucune activité » qu'il fonde n'est valable qu'à 45 s près. C'est
/// pourquoi elle est courte et pourquoi elle ne doit PAS être allongée par confort de performance.
///
/// Verrou : le motif éprouvé de ChronosTokenAuthority (SemaphoreSlim(1,1) + double vérification). Deux
/// passes concurrentes liraient 1 072 Mo simultanément ; le coût du verrou est nul en comparaison.
/// Type NEUTRE : aucun type WPF, la garde de pureté Services/Models reste verte.
/// </summary>
public sealed class SourceActiviteMemoisee : ITranscriptActivitySource
{
    /// <summary>Durée de validité par défaut. Alignée sur l'intervalle de rafraîchissement nominal (60 s) :
    /// un tick ne paie jamais deux passes, deux ticks n'en partagent jamais une.</summary>
    public static readonly TimeSpan ValiditeParDefaut = TimeSpan.FromSeconds(60);

    private readonly ITranscriptActivitySource _inner;
    private readonly IClock _horloge;
    private readonly TimeSpan _validite;
    private readonly SemaphoreSlim _verrou = new(1, 1);
    private TranscriptActivityLog? _journal;

    public SourceActiviteMemoisee(ITranscriptActivitySource inner, IClock horloge, TimeSpan? validite = null)
    {
        _inner = inner;
        _horloge = horloge;
        _validite = validite ?? ValiditeParDefaut;
    }

    public async Task<TranscriptActivityLog> ReadAsync(CancellationToken ct = default)
    {
        if (EncoreValide(_journal, _horloge.UtcNow) && _journal is { } rapide) return rapide;

        await _verrou.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double vérification : N demandeurs entrés ensemble ne produisent qu'UNE passe disque.
            if (EncoreValide(_journal, _horloge.UtcNow) && _journal is { } dejaFait) return dejaFait;

            try
            {
                _journal = await _inner.ReadAsync(ct).ConfigureAwait(false);
            }
            catch when (_journal is not null)
            {
                // Une panne de la source ne doit pas effacer ce qu'on savait : le dernier journal connu
                // reste servable, et sa propre péremption le retirera du jeu le moment venu.
            }

            // Ici _journal est forcément non-null : soit la passe vient de réussir, soit le filtre du
            // catch ci-dessus n'a laissé passer l'exception que parce qu'un journal connu existait.
            return _journal!;
        }
        finally { _verrou.Release(); }
    }

    // La validité se mesure sur l'instant de la PASSE DISQUE et non sur l'instant de mise en cache :
    // un journal né vieux doit expirer vite.
    private bool EncoreValide(TranscriptActivityLog? j, DateTimeOffset now)
        => j is not null && now - j.Now <= _validite;
}
