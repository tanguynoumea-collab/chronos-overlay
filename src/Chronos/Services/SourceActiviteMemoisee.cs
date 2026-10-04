namespace Chronos.Services;

/// <summary>
/// Décorateur d'<see cref="ITranscriptActivitySource"/> à DURÉE DE VALIDITÉ. Raison d'être mesurée : une
/// passe réelle coûte 2,7 à 3,2 s et lit 536 Mo sur la machine cible ; au tick de 60 s, une lecture par
/// tick serait 536 Mo par minute d'E/S permanente sur un overlay. Le journal rendu par la phase 16 est
/// PUR et interrogeable N fois : le réutiliser est donc gratuit et sans perte.
///
/// Depuis la phase 42.3, la passe réelle n'est plus intégrale : seule la première l'est (727 fichiers /
/// 882 Mo mesurés le 2026-10-04), les suivantes ne relisent que les fichiers modifiés (cache par fichier de
/// <see cref="TranscriptActivityProvider"/>). Cette mémoïsation reste utile (deux demandeurs d'un même tick
/// ne paient qu'une passe), mais sa durée de validité n'a PAS bougé pour autant : voir ci-dessous.
///
/// La durée de validité est AUSSI la tolérance de certification : un journal dont le <c>Now</c> a 45 s ne
/// dit rien de ces 45 s, donc un « aucune activité » qu'il fonde n'est valable qu'à 45 s près. C'est
/// pourquoi elle est courte et pourquoi elle ne doit PAS être allongée par confort de performance. Pour
/// la même raison, une panne de la source ne rend jamais un journal périmé (DS2-02) : l'échec remonte.
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

            // Hors de sa validité, le journal mémorisé n'est JAMAIS rendu, même si la relecture échoue : il
            // ne dit rien de l'intervalle écoulé depuis sa passe disque, et le rendre laisserait la doctrine
            // certifier « encore valide » un relevé alors que Claude Code a pu travailler (DS2-02). La
            // validité (60 s, tolérance de certification) est servie par le chemin rapide et la double
            // vérification ; au point d'échec, le journal est par construction hors validité — un catch
            // filtré sur EncoreValide serait du code mort. L'exception remonte : la tête
            // (LastExactUsageProvider) la convertit en journal nul, donc en « Indisponible » — jamais en
            // « exact ».
            _journal = await _inner.ReadAsync(ct).ConfigureAwait(false);
            return _journal;
        }
        finally { _verrou.Release(); }
    }

    // La validité se mesure sur l'instant de la PASSE DISQUE et non sur l'instant de mise en cache :
    // un journal né vieux doit expirer vite.
    private bool EncoreValide(TranscriptActivityLog? j, DateTimeOffset now)
        => j is not null && now - j.Now <= _validite;
}
