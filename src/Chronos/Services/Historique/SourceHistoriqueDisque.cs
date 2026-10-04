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
/// absent est une lecture vide ; un mois de journal inaccessible (verrou, droits, dossier « poison ») rend une lecture partielle
/// marquée <c>LectureIncomplete</c> ; une exception inattendue rend des données vides AVEC <c>LectureIncomplete = true</c> — la
/// lecture est dite incomplète, et la vue l'écrit au lieu d'un faux « aucun relevé » (42.2-05, TEST-4 / DATA-13). Type NEUTRE.</para>
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
            var analyse = AnalyseReleves.Analyser(journal, InstantsHistorique.InstantDAnalyse(now, semaine), cadence);
            var lecturePrecedente = LecteurJournal.Lire(_dossier, precedente.Debut, precedente.Fin);
            var analysePrecedente = AnalyseReleves.Analyser(lecturePrecedente, InstantsHistorique.InstantDAnalyse(now, precedente), cadence);
            var agregats = LecteurAgregats.Lire(_dossier, semaine.Debut, semaine.Fin);
            var barres = RenduLocalTokens.ParHeure(agregats.Tranches, semaine, _tz, ChargerCouverture());
            var divergences = Divergences.Detecter(analyse.DeltasHebdo, barres);
            return new DonneesSemaine(semaine, analyse, precedente, analysePrecedente, barres, agregats.Couverture, divergences, journal.JournalOuvertLe, now,
                LectureIncomplete: journal.LectureIncomplete || lecturePrecedente.LectureIncomplete);
        }
        catch (Exception)
        {
            return SemaineVide(semaine, precedente, now);   // repli : lecture dite incomplète, jamais « aucun relevé »
        }
    }

    /// <inheritdoc />
    public DonneesJour LireJour(Plage jour, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(jour);
        try
        {
            var cadence = RateLimitHeaderUsageProvider.CadenceNominale;
            // D-35-04 : la veille de minuit (vue Jour seulement) — une lecture sur [Debut − Horizon, Fin[, le dernier relevé de la veille
            // ferme le trou qui chevauche minuit (cause nommée), puis la série rendue est restreinte au jour.
            var large = LecteurJournal.Lire(_dossier, jour.Debut - LectureVeille.Horizon, jour.Fin);
            var journal = LectureVeille.PourLeJour(large, jour, cadence);
            var analyse = LectureVeille.RestreindreAuJour(AnalyseReleves.Analyser(journal, InstantsHistorique.InstantDAnalyse(now, jour), cadence), jour);
            var agregats = LecteurAgregats.Lire(_dossier, jour.Debut, jour.Fin);
            var colonnes = RenduLocalTokens.ParQuartDHeure(agregats.Tranches, jour, _tz, ChargerCouverture());
            // La veille reconstruit une LectureJournal : le drapeau se lit sur la lecture LARGE, celle qui a ouvert les fichiers.
            return new DonneesJour(jour, analyse, colonnes, agregats.Couverture, journal.JournalOuvertLe, now, LectureIncomplete: large.LectureIncomplete);
        }
        catch (Exception)
        {
            return JourVide(jour, now);
        }
    }

    /// <inheritdoc />
    public DonneesQuatreSemaines LireQuatreSemaines(IReadOnlyList<Plage> semaines, DateTimeOffset now)
    {
        if (semaines is null || semaines.Count != 4)
            throw new ArgumentException("Quatre semaines de forfait contiguës sont attendues (BornesPlage.QuatreSemaines).", nameof(semaines));
        try
        {
            // D-35-01 : LecteurJournal relit le plus ancien fichier à chaque appel (JournalOuvertLe) — UNE lecture, partitionnée en mémoire.
            var cadence = RateLimitHeaderUsageProvider.CadenceNominale;
            var lecture = LecteurJournal.Lire(_dossier, semaines[0].Debut, semaines[3].Fin);
            var analyses = semaines.Select(p => AnalyseReleves.Analyser(
                    new LectureJournal(
                        lecture.Releves.Where(r => p.Contient(r.T)).ToList(),
                        lecture.Evenements.Where(e => p.Contient(e.T)).ToList(),
                        0, lecture.JournalOuvertLe, p),
                    InstantsHistorique.InstantDAnalyse(now, p), cadence))
                .ToList();
            return new DonneesQuatreSemaines(analyses, lecture.JournalOuvertLe, now, LectureIncomplete: lecture.LectureIncomplete);
        }
        catch (Exception)
        {
            return new DonneesQuatreSemaines(semaines.Select(p => AnalyseVide(p, now)).ToList(), null, now, LectureIncomplete: true);
        }
    }

    // Pitfall 11 : LecteurAgregats.Lire ne rend pas l'instance ; couverture.json se charge à part (tolérant : absent → tout hors couverture).
    private CouvertureTokens ChargerCouverture() => CouvertureTokens.Charger(Path.Combine(_dossier, CouvertureTokens.NomFichier));

    // --- Replis des catch (jamais nuls) : données vides MARQUÉES « lecture incomplète » — une exception n'est pas une absence. ---

    private static AnalyseJournal AnalyseVide(Plage plage, DateTimeOffset now)
        => AnalyseReleves.Analyser(new LectureJournal(Array.Empty<ReleveJournal>(), Array.Empty<EvenementJournal>(), 0, null, plage), now, RateLimitHeaderUsageProvider.CadenceNominale);

    private DonneesSemaine SemaineVide(Plage semaine, Plage precedente, DateTimeOffset now)
    {
        var couverture = new CouvertureTokens();
        return new DonneesSemaine(
            semaine, AnalyseVide(semaine, now), precedente, AnalyseVide(precedente, now),
            RenduLocalTokens.ParHeure(Array.Empty<TrancheTokens>(), semaine, _tz, couverture),
            RenduLocalTokens.SousPlagesCouverture(semaine, couverture),
            Array.Empty<Divergence>(), null, now, LectureIncomplete: true);
    }

    private DonneesJour JourVide(Plage jour, DateTimeOffset now)
    {
        var couverture = new CouvertureTokens();
        return new DonneesJour(
            jour, AnalyseVide(jour, now),
            RenduLocalTokens.ParQuartDHeure(Array.Empty<TrancheTokens>(), jour, _tz, couverture),
            RenduLocalTokens.SousPlagesCouverture(jour, couverture),
            null, now, LectureIncomplete: true);
    }
}
