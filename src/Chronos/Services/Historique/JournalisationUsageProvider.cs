using System.Globalization;
using System.Reflection;
using Chronos.Models;
using Chronos.Models.Historique;
using Microsoft.Extensions.Hosting;

namespace Chronos.Services.Historique;

/// <summary>
/// JRN-01 / JRN-02 — observe la chaîne exacte et journalise.
///
/// <para>Placé ENTRE <c>LastExactUsageProvider</c> (la tête, couche de doctrine) et le composite (D-32-19) : il voit
/// l'inner BRUT — jamais le magasin (<c>Source = MagasinDernierExact</c> naît au-dessus, dans
/// <c>LastExactStore.Reconstruire</c>), jamais un plancher (la doctrine statue au-dessus). L'exclusion du magasin est
/// quand même écrite et testée : une règle qui ne tient qu'à la position dans le graphe DI n'est pas une règle.</para>
///
/// <para>Le rejeu du cache de la sonde (même instance 5 fois sur 6 : sonde 300 s, timer 60 s) est éliminé par
/// « <c>t</c> strictement supérieur au dernier <c>t</c> écrit pour cette source » — mémoire amorcée par la queue du
/// fichier (<see cref="JournalReleves.DernierT"/>), vérité tenue par la relecture sous verrou de l'écrivain.
/// Une ligne par couple (<c>CapturedAt</c>, <c>Source</c>) distinct du snapshot (D-32-15).</para>
///
/// <para>Rend le snapshot INCHANGÉ (même instance). Aucun appel réseau. Le journal ne casse JAMAIS la chaîne : une
/// exception de journalisation devient un événement <c>ecriture_ratee</c> (magasin « journal »), pas une panne du cadran.</para>
///
/// <para><see cref="IHostedService"/> : inscrit AVANT <c>RefreshOrchestrator</c> (32-05) pour que <c>demarrage</c> précède le
/// premier relevé et qu'<c>arret</c> suive le dernier (les services hébergés s'arrêtent en ordre inverse). Un kill ne
/// produit pas d'<c>arret</c> : le lecteur en fait un trou « Chronos arrêté ».</para>
/// </summary>
public sealed class JournalisationUsageProvider : IUsageProvider, IHostedService
{
    private readonly IUsageProvider _inner;
    private readonly JournalReleves _journal;
    private readonly IEtatServeur? _etatServeur;
    private readonly IAuthStatus? _authStatus;
    private readonly IClock _clock;
    private readonly string? _version;

    // Protège la mémoire de dédup et les mémoires de transition : GetAsync est sérialisé par l'orchestrateur, mais les
    // événements d'authentification arrivent sur des threads du pool.
    private readonly object _verrou = new();

    // Dernier t écrit par source ; amorcé paresseusement par la queue du fichier. Valeur null = amorcé, rien trouvé.
    private readonly Dictionary<SourceUsage, DateTimeOffset?> _dernierT = new();

    private ResultatSonde? _dernierResultatVu;
    private bool _dernierStatutRejete;
    private EtatAuthentification? _dernierEtatAuth;

    public JournalisationUsageProvider(IUsageProvider inner, JournalReleves journal, IEtatServeur? etatServeur,
                                       IAuthStatus? authStatus, IClock clock, string? version = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _etatServeur = etatServeur;
        _authStatus = authStatus;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _version = version;
    }

    /// <summary>Relevés réellement écrits par CE décorateur depuis le démarrage (les rejeux ne comptent pas).</summary>
    public int RelevesJournalises { get; private set; }

    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        var snap = await _inner.GetAsync(ct).ConfigureAwait(false);
        try
        {
            Journaliser(snap);
        }
        catch (Exception ex)
        {
            // Le journal ne casse JAMAIS la chaîne — mais il le dit, dans le journal lui-même.
            _journal.AjouterEvenement(new EvenementJournal(_clock.UtcNow, TypeEvenement.EcritureRatee, "journal", Decrire(ex)));
        }
        return snap;   // même instance : le décorateur observe, il ne transforme pas
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _journal.Purger();   // rétention 24 mois, une fois au démarrage
        _journal.AjouterEvenement(new EvenementJournal(_clock.UtcNow, TypeEvenement.Demarrage, Version: _version ?? VersionEmbarquee()));
        if (_authStatus is not null)
        {
            lock (_verrou) { _dernierEtatAuth = _authStatus.Etat; }
            _authStatus.EtatChange += SurEtatAuth;
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_authStatus is not null) _authStatus.EtatChange -= SurEtatAuth;
        _journal.AjouterEvenement(new EvenementJournal(_clock.UtcNow, TypeEvenement.Arret));
        return Task.CompletedTask;
    }

    /// <summary>CPT-02 — un magasin persistant (« last-exact »…) signale une écriture ratée : elle laisse une trace datée avec sa cause.</summary>
    public void SignalerEcritureRatee(string magasin, string cause)
        => _journal.AjouterEvenement(new EvenementJournal(_clock.UtcNow, TypeEvenement.EcritureRatee, Magasin: magasin, Cause: cause));

    // --- Journalisation d'un snapshot ---

    private void Journaliser(UsageSnapshot snap)
    {
        lock (_verrou)
        {
            var eligibles = new List<WindowState>(2);
            if (EstEligible(snap.FiveHour)) eligibles.Add(snap.FiveHour);
            if (EstEligible(snap.SevenDay)) eligibles.Add(snap.SevenDay);

            JournaliserRefusSonde(eligibles);

            // Une ligne par couple (t, source) distinct, dans l'ordre des instants.
            foreach (var groupe in eligibles.GroupBy(w => (T: w.CapturedAt!.Value, Source: w.Source!.Value)).OrderBy(g => g.Key.T))
            {
                var (t, source) = groupe.Key;
                var cinq = groupe.FirstOrDefault(w => w.Kind == WindowKind.FiveHour);
                var sept = groupe.FirstOrDefault(w => w.Kind == WindowKind.SevenDay);
                var depassement = cinq?.Depassement ?? sept?.Depassement;

                var releve = new ReleveJournal(
                    t, source,
                    U5: cinq?.Utilization, R5: cinq?.ResetsAt, Statut5: cinq?.StatutServeur,
                    U7: sept?.Utilization, R7: sept?.ResetsAt, Statut7: sept?.StatutServeur,
                    Overage: depassement?.Utilization, OverageStatut: depassement?.Statut);

                if (!_dernierT.TryGetValue(source, out var dernier))
                    _dernierT[source] = dernier = _journal.DernierT(source);   // amorce : la queue du fichier

                if (dernier is { } d && t <= d) continue;   // rejeu du cache, ou relevé plus ancien : rien

                if (dernier is { } precedent && t - precedent > JournalReleves.SeuilReprise)
                {
                    // « Le trou finit ici » : daté du relevé qui y met fin, écrit AVANT lui.
                    var minutes = ((int)(t - precedent).TotalMinutes).ToString(CultureInfo.InvariantCulture);
                    _journal.AjouterEvenement(new EvenementJournal(t, TypeEvenement.Reprise, Cause: "trou de " + minutes + " min"));
                }

                var ecrit = _journal.AjouterReleve(releve);
                // Un refus SANS erreur = un autre processus l'a déjà écrit : la mémoire avance quand même, sinon la
                // même reprise serait réémise à chaque tick.
                if (ecrit || _journal.DerniereErreur is null) _dernierT[source] = t;
                if (ecrit) RelevesJournalises++;
            }
        }
    }

    // Reliability Exact, chiffre et instant présents, source nommée et jamais le magasin (D-32-19).
    private static bool EstEligible(WindowState w)
        => w.Reliability == SourceReliability.Exact
           && w.Utilization is not null
           && w.CapturedAt is not null
           && w.Source is { } s
           && s != SourceUsage.MagasinDernierExact;

    private static bool EstRefus(ResultatSonde r)
        => r is ResultatSonde.SaturationEnTetesLus or ResultatSonde.SaturationSansEnTetes or ResultatSonde.RefusServeur;

    // sonde_refusee sur TRANSITION : DernierResultat passe à un refus, ou une fenêtre exacte passe à « rejected »
    // (un 429 porteur produit AUSSI un relevé : les en-têtes survivent au refus, HDR-02). Au plus un événement par tick.
    private void JournaliserRefusSonde(List<WindowState> eligibles)
    {
        string? cause = null;

        if (_etatServeur is not null)
        {
            var resultat = _etatServeur.DernierResultat;
            var refusAvant = _dernierResultatVu is { } avant && EstRefus(avant);
            if (EstRefus(resultat) && !refusAvant) cause = resultat.ToString();
            _dernierResultatVu = resultat;
        }

        var rejete = eligibles.Any(w => w.StatutServeur == StatutServeur.Rejete);
        if (rejete && !_dernierStatutRejete) cause ??= "statut rejected";
        _dernierStatutRejete = rejete;

        if (cause is not null)
            _journal.AjouterEvenement(new EvenementJournal(_clock.UtcNow, TypeEvenement.SondeRefusee, Cause: cause));
    }

    private void SurEtatAuth(object? sender, EtatAuthentification e)
    {
        lock (_verrou)
        {
            if (e == EtatAuthentification.Deconnecte && _dernierEtatAuth != EtatAuthentification.Deconnecte)
                _journal.AjouterEvenement(new EvenementJournal(_clock.UtcNow, TypeEvenement.JetonInvalide));
            _dernierEtatAuth = e;
        }
    }

    private static string Decrire(Exception ex) => ex.GetType().Name + " : " + ex.Message;

    // Même lecture que DiagnosticService.VersionEmbarquee (privée là-bas) : la version embarquée dans l'exe.
    private static string VersionEmbarquee()
        => (Attribute.GetCustomAttribute(typeof(JournalisationUsageProvider).Assembly, typeof(AssemblyInformationalVersionAttribute))
                as AssemblyInformationalVersionAttribute)?.InformationalVersion ?? "?";
}
