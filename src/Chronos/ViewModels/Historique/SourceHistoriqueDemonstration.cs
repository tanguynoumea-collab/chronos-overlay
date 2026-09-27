using Chronos.Models.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;

namespace Chronos.ViewModels.Historique;

/// <summary>
/// D-34-16 — la façade de lecture servie par <see cref="ScenariosHistorique"/> : MÊME enchaînement que
/// <see cref="SourceHistoriqueDisque"/> (analyse → rendu local → divergences), sur les objets en mémoire, pour toute plage.
/// La galerie <c>--historique</c> (34-05) la branche sur la vraie <c>HistoriqueWindow</c> avec <see cref="ScenariosHistorique.HorlogeFigee"/>
/// et <see cref="ReglagesHistoriqueMemoire"/> ; les tests d'honnêteté (34-08) lisent les mêmes données.
/// </summary>
public sealed class SourceHistoriqueDemonstration(TimeZoneInfo tz) : ISourceHistorique
{
    private readonly TimeZoneInfo _tz = tz;

    /// <inheritdoc />
    public DateTimeOffset? RepereHebdo(DateTimeOffset now) => ScenariosHistorique.RepereHebdo;

    /// <inheritdoc />
    public DonneesSemaine LireSemaine(Plage semaine, Plage precedente, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(semaine);
        ArgumentNullException.ThrowIfNull(precedente);
        var cadence = RateLimitHeaderUsageProvider.CadenceNominale;
        var journal = ScenariosHistorique.Journal(semaine, _tz);
        var analyse = AnalyseReleves.Analyser(journal, InstantsHistorique.InstantDAnalyse(now, semaine), cadence);
        var analysePrecedente = AnalyseReleves.Analyser(ScenariosHistorique.Journal(precedente, _tz), InstantsHistorique.InstantDAnalyse(now, precedente), cadence);
        var agregats = ScenariosHistorique.Agregats(semaine, _tz);
        var barres = RenduLocalTokens.ParHeure(agregats.Tranches, semaine, _tz, ScenariosHistorique.Couverture());
        var divergences = Divergences.Detecter(analyse.DeltasHebdo, barres);
        return new DonneesSemaine(semaine, analyse, precedente, analysePrecedente, barres, agregats.Couverture, divergences, journal.JournalOuvertLe, now);
    }

    /// <inheritdoc />
    public DonneesJour LireJour(Plage jour, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(jour);
        var cadence = RateLimitHeaderUsageProvider.CadenceNominale;
        // D-35-04 : même veille de minuit que le disque (le mercredi du scénario nomme « Chronos arrêté, mar. 23:00 → 07:00 »).
        var journal = LectureVeille.PourLeJour(ScenariosHistorique.Journal(new Plage(jour.Debut - LectureVeille.Horizon, jour.Fin), _tz), jour, cadence);
        var analyse = LectureVeille.RestreindreAuJour(AnalyseReleves.Analyser(journal, InstantsHistorique.InstantDAnalyse(now, jour), cadence), jour);
        var agregats = ScenariosHistorique.Agregats(jour, _tz);
        var colonnes = RenduLocalTokens.ParQuartDHeure(agregats.Tranches, jour, _tz, ScenariosHistorique.Couverture());
        return new DonneesJour(jour, analyse, colonnes, agregats.Couverture, journal.JournalOuvertLe, now);
    }

    /// <inheritdoc />
    public DonneesQuatreSemaines LireQuatreSemaines(IReadOnlyList<Plage> semaines, DateTimeOffset now)
    {
        if (semaines is null || semaines.Count != 4)
            throw new ArgumentException("Quatre semaines de forfait contiguës sont attendues (BornesPlage.QuatreSemaines).", nameof(semaines));
        // Même règle que le disque (instant d'analyse borné à chaque plage) ; en mémoire, quatre lectures ne coûtent rien.
        var cadence = RateLimitHeaderUsageProvider.CadenceNominale;
        var analyses = semaines
            .Select(p => AnalyseReleves.Analyser(ScenariosHistorique.Journal(p, _tz), InstantsHistorique.InstantDAnalyse(now, p), cadence))
            .ToList();
        return new DonneesQuatreSemaines(analyses, ScenariosHistorique.JournalOuvertLe, now);
    }
}
