using System.IO;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
using Chronos.Services.Historique.Tokens;

namespace Chronos.Services.Historique;

/// <summary>
/// HIS-01 — la façade de lecture sur DISQUE : journal → analyse → agrégats + couverture (chargée séparément, Pitfall 11 :
/// <c>LecteurAgregats.Lire</c> ne rend pas l'instance <see cref="CouvertureTokens"/>) → rendu local → divergences (D-34-07).
///
/// <para>Le dossier est <c>ChronosPaths.HistoriqueDir</c>, jamais construit ici ; la cadence est
/// <c>RateLimitHeaderUsageProvider.CadenceNominale</c> (seuil de trou = deux cadences). Cette classe ne calcule AUCUNE borne :
/// elle lit la plage qu'on lui donne (les bornes viennent du VM). Elle ne lève JAMAIS et ne crée JAMAIS le dossier : un dossier
/// absent est une lecture vide ; une exception inattendue (E/S qui casse en cours de route) rend des données VIDES — la cause
/// est perdue ici, le VM affichera « aucun relevé » et le diagnostic reste l'outil de panne. Type NEUTRE (aucun WPF).</para>
/// </summary>
public sealed class SourceHistoriqueDisque(ChronosPaths paths, TimeZoneInfo tz) : ISourceHistorique
{
    private static readonly TimeSpan SeptJours = TimeSpan.FromDays(7);
    private readonly string _dossier = paths.HistoriqueDir;
    private readonly TimeZoneInfo _tz = tz;

    /// <inheritdoc />
    public DateTimeOffset? RepereHebdo(DateTimeOffset now)
    {
        try
        {
            // [now − 7 j, now] : le dernier relevé qui porte un r7 (Releves est trié par T).
            var lecture = LecteurJournal.Lire(_dossier, now - SeptJours, now.AddTicks(1));
            return lecture.Releves.LastOrDefault(r => r.R7 is not null)?.R7;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public DonneesSemaine LireSemaine(Plage semaine, Plage precedente, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(semaine);
        ArgumentNullException.ThrowIfNull(precedente);
        try
        {
            var cadence = RateLimitHeaderUsageProvider.CadenceNominale;
            var journal = LecteurJournal.Lire(_dossier, semaine.Debut, semaine.Fin);
            var analyse = AnalyseReleves.Analyser(journal, now, cadence);
            var analysePrecedente = AnalyseReleves.Analyser(LecteurJournal.Lire(_dossier, precedente.Debut, precedente.Fin), now, cadence);
            var agregats = LecteurAgregats.Lire(_dossier, semaine.Debut, semaine.Fin);
            var couverture = CouvertureTokens.Charger(Path.Combine(_dossier, CouvertureTokens.NomFichier));
            var barres = RenduLocalTokens.ParHeure(agregats.Tranches, semaine, _tz, couverture);
            var divergences = Divergences.Detecter(analyse.DeltasHebdo, barres);
            return new DonneesSemaine(semaine, analyse, precedente, analysePrecedente, barres, agregats.Couverture, divergences, journal.JournalOuvertLe, now);
        }
        catch (Exception)
        {
            return SemaineVide(semaine, precedente, now);
        }
    }

    /// <inheritdoc />
    public DonneesJour LireJour(Plage jour, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(jour);
        try
        {
            var cadence = RateLimitHeaderUsageProvider.CadenceNominale;
            var journal = LecteurJournal.Lire(_dossier, jour.Debut, jour.Fin);
            var analyse = AnalyseReleves.Analyser(journal, now, cadence);
            var agregats = LecteurAgregats.Lire(_dossier, jour.Debut, jour.Fin);
            var couverture = CouvertureTokens.Charger(Path.Combine(_dossier, CouvertureTokens.NomFichier));
            var colonnes = RenduLocalTokens.ParQuartDHeure(agregats.Tranches, jour, _tz, couverture);
            return new DonneesJour(jour, analyse, colonnes, agregats.Couverture, journal.JournalOuvertLe, now);
        }
        catch (Exception)
        {
            return JourVide(jour, now);
        }
    }

    // --- Données vides (jamais nulles) : une plage sans rien dit « aucun relevé », « hors couverture ». ---

    private static AnalyseJournal AnalyseVide(Plage plage, DateTimeOffset now)
        => AnalyseReleves.Analyser(new LectureJournal(Array.Empty<ReleveJournal>(), Array.Empty<EvenementJournal>(), 0, null, plage), now, RateLimitHeaderUsageProvider.CadenceNominale);

    private DonneesSemaine SemaineVide(Plage semaine, Plage precedente, DateTimeOffset now)
    {
        var couverture = new CouvertureTokens();
        return new DonneesSemaine(
            semaine, AnalyseVide(semaine, now), precedente, AnalyseVide(precedente, now),
            RenduLocalTokens.ParHeure(Array.Empty<TrancheTokens>(), semaine, _tz, couverture),
            RenduLocalTokens.SousPlagesCouverture(semaine, couverture),
            Array.Empty<Divergence>(), null, now);
    }

    private DonneesJour JourVide(Plage jour, DateTimeOffset now)
    {
        var couverture = new CouvertureTokens();
        return new DonneesJour(
            jour, AnalyseVide(jour, now),
            RenduLocalTokens.ParQuartDHeure(Array.Empty<TrancheTokens>(), jour, _tz, couverture),
            RenduLocalTokens.SousPlagesCouverture(jour, couverture),
            null, now);
    }
}
