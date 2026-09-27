using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// JRN-05 — la lecture PAR PLAGE du journal : un seul appel ouvre les seuls fichiers mensuels (UTC) qui
/// chevauchent <c>[de, a[</c>, rend relevés et événements triés par <c>t</c> et filtrés, compte les lignes
/// ignorées, et date l'ouverture du journal indépendamment de la plage demandée.
///
/// Ce que ces tests gravent : un relevé du 30/09 23:58Z et un du 01/10 00:03Z, dans DEUX fichiers, sortent
/// ensemble (critère 4 de la phase) ; la borne <c>de</c> est incluse, la borne <c>a</c> exclue ; un dossier
/// absent est une lecture vide, jamais une exception ; la journée nominale FABRIQUÉE par un vrai écrivain
/// (D-32-30) se relit à 288 relevés.
///
/// Fixtures versionnées sous TestData/journal/&lt;cas&gt;/ ; E/S temporaires uniquement, jamais %APPDATA%.
/// </summary>
public class LecteurJournalTests
{
    private static string TestDataDir(string cas, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "journal", cas);

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void Lire_ouvre_les_seuls_mois_qui_chevauchent_la_plage_et_trie_par_t()
    {
        var dossier = TestDataDir("a-cheval");

        var aCheval = LecteurJournal.Lire(dossier, Utc("2026-09-30T23:00:00Z"), Utc("2026-10-01T01:00:00Z"));
        Assert.Equal(2, aCheval.Releves.Count);
        Assert.Equal(Utc("2026-09-30T23:58:00Z"), aCheval.Releves[0].T);
        Assert.Equal(Utc("2026-10-01T00:03:00Z"), aCheval.Releves[1].T);
        Assert.Equal(0.50, aCheval.Releves[0].U5);
        Assert.Equal(0.51, aCheval.Releves[1].U5);
        Assert.Equal(0, aCheval.LignesIgnorees);

        var octobreSeul = LecteurJournal.Lire(dossier, Utc("2026-10-01T00:00:00Z"), Utc("2026-10-02T00:00:00Z"));
        Assert.Single(octobreSeul.Releves);
        Assert.Equal(Utc("2026-10-01T00:03:00Z"), octobreSeul.Releves[0].T);
    }

    [Fact]
    public void Lire_filtre_strictement_sur_la_plage_bornes_de_incluse_a_exclue()
    {
        var lecture = LecteurJournal.Lire(TestDataDir("trou-arrete"), Utc("2026-09-27T10:05:00Z"), Utc("2026-09-27T11:03:00Z"));

        Assert.Equal(new[] { Utc("2026-09-27T10:05:00Z"), Utc("2026-09-27T10:10:00Z") }, lecture.Releves.Select(r => r.T));
        Assert.Equal(2, lecture.Evenements.Count);
        Assert.Equal(TypeEvenement.Arret, lecture.Evenements[0].Type);
        Assert.Equal(Utc("2026-09-27T10:12:00Z"), lecture.Evenements[0].T);
        Assert.Equal(TypeEvenement.Demarrage, lecture.Evenements[1].Type);
        Assert.Equal("3.2.2", lecture.Evenements[1].Version);
        Assert.Equal(new Plage(Utc("2026-09-27T10:05:00Z"), Utc("2026-09-27T11:03:00Z")), lecture.Plage);
    }

    [Fact]
    public void JournalOuvertLe_est_le_t_de_la_premiere_ligne_valide_du_plus_ancien_fichier()
    {
        var dossier = TestDataDir("deux-resets-hebdo");
        var ouverture = Utc("2026-09-14T12:00:00Z");

        Assert.Equal(ouverture, LecteurJournal.Lire(dossier, Utc("2026-09-14T00:00:00Z"), Utc("2026-10-07T00:00:00Z")).JournalOuvertLe);
        Assert.Equal(ouverture, LecteurJournal.Lire(dossier, Utc("2026-10-03T00:00:00Z"), Utc("2026-10-04T00:00:00Z")).JournalOuvertLe);
        Assert.Equal(ouverture, LecteurJournal.Lire(dossier, Utc("2027-01-01T00:00:00Z"), Utc("2027-01-02T00:00:00Z")).JournalOuvertLe);

        var vide = FabriqueJournal.DossierTemp();
        Assert.Null(LecteurJournal.Lire(vide, Utc("2026-09-01T00:00:00Z"), Utc("2026-10-01T00:00:00Z")).JournalOuvertLe);

        var unSeul = FabriqueJournal.DossierTemp();
        File.WriteAllText(Path.Combine(unSeul, "releves-2026-08.jsonl"),
            "{\"v\":1}\n"
            + "{\"v\":1,\"t\":\"2026-08-03T09:00:00.0000000+00:00\",\"ev\":\"demarrage\"}\n"
            + "{\"v\":1,\"t\":\"2026-08-03T09:05:00.0000000+00:00\",\"u5\":0.1,\"source\":\"SondeEnTetes\"}\n");
        Assert.Equal(Utc("2026-08-03T09:00:00Z"),
            LecteurJournal.Lire(unSeul, Utc("2026-09-01T00:00:00Z"), Utc("2026-10-01T00:00:00Z")).JournalOuvertLe);
    }

    [Fact]
    public void Lire_compte_les_lignes_ignorees_sans_lever()
    {
        var lecture = LecteurJournal.Lire(TestDataDir("tolerance"), Utc("2026-09-01T00:00:00Z"), Utc("2026-10-01T00:00:00Z"));

        Assert.Equal(4, lecture.LignesIgnorees);
        Assert.Equal(2, lecture.Releves.Count);
        Assert.Equal(2, lecture.Evenements.Count);
        Assert.Equal(Utc("2026-09-27T10:05:00Z"), lecture.JournalOuvertLe);
    }

    [Fact]
    public void Un_dossier_absent_rend_une_lecture_vide()
    {
        var inexistant = Path.Combine(Path.GetTempPath(), "chronos-tests", "inexistant-" + Guid.NewGuid().ToString("N"));

        var lecture = LecteurJournal.Lire(inexistant, Utc("2026-09-01T00:00:00Z"), Utc("2026-10-01T00:00:00Z"));

        Assert.Empty(lecture.Releves);
        Assert.Empty(lecture.Evenements);
        Assert.Null(lecture.JournalOuvertLe);
        Assert.Equal(0, lecture.LignesIgnorees);
        Assert.False(Directory.Exists(inexistant), "Une lecture ne crée jamais le dossier du journal.");
    }

    [Fact]
    public void La_journee_nominale_fabriquee_donne_288_releves_et_un_demarrage()
    {
        var minuit = Utc("2026-09-26T22:00:00Z");   // minuit local du 27/09 (Paris, heure d'été)
        var dossier = FabriqueJournal.JourneeNominale(minuit);

        var lecture = LecteurJournal.Lire(dossier, minuit, minuit + TimeSpan.FromHours(24));

        Assert.Equal(288, lecture.Releves.Count);
        Assert.Single(lecture.Evenements);
        Assert.Equal(TypeEvenement.Demarrage, lecture.Evenements[0].Type);
        Assert.Equal(5, lecture.Releves.Select(r => r.R5).Distinct().Count());   // 24 h / 5 h : 4 resets + la borne initiale
        Assert.Equal(0, lecture.LignesIgnorees);
        Assert.Equal(minuit, lecture.JournalOuvertLe);
        Assert.All(lecture.Releves, r => Assert.Equal(SourceUsage.SondeEnTetes, r.Source));
    }
}
