using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
using Chronos.Services.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// TOK-04 — la lecture PAR PLAGE des agrégats de tokens : un seul appel ouvre les seuls fichiers mensuels (UTC) qui
/// chevauchent <c>[de, a[</c>, relit chaque ligne avec tolérance (lignes refusées COMPTÉES), filtre, trie, et charge la
/// couverture (<c>couverture.json</c>) à côté des agrégats, découpée en sous-plages.
///
/// Ce que ces tests gravent : une tranche du 30/09 23:45Z et trois du 01/10, dans DEUX fichiers, sortent ensemble ; la
/// borne <c>de</c> est incluse, la borne <c>a</c> exclue ; un dossier absent est une lecture vide, jamais une exception,
/// et n'est jamais créé ; surtout : une plage SANS tranche n'est jamais « zéro » — elle dit POURQUOI elle est vide
/// (« hors couverture » avant le plus vieux transcript, « transcripts absents » hors d'un intervalle garanti,
/// « couverte » = vraiment zéro activité).
///
/// Fixtures versionnées sous TestData/tokens/&lt;cas&gt;/ ; E/S temporaires uniquement, jamais %APPDATA%.
/// </summary>
public class LecteurAgregatsTests
{
    private static string TestDataDir(string cas, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "tokens", cas);

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    // Un dossier temporaire portant la fixture dst-2026-10-25 et une couverture : plus ancienne ligne vue le 23/06/2026,
    // une passe complète le 25/10/2026 09:00Z → 09:05Z (intervalle garanti [25/09 09:00Z, 25/10 09:05Z[).
    private static string DossierAvecCouverture()
    {
        var dossier = FabriqueJournal.DossierTemp();
        File.Copy(Path.Combine(TestDataDir("dst-2026-10-25"), "tokens-2026-10.jsonl"), Path.Combine(dossier, "tokens-2026-10.jsonl"));

        var couverture = new CouvertureTokens();
        couverture.VoirLigne(Utc("2026-06-23T12:44:22Z"));
        couverture.GarantirPasse(Utc("2026-10-25T09:00:00Z"), Utc("2026-10-25T09:05:00Z"));
        Assert.True(couverture.Sauvegarder(Path.Combine(dossier, CouvertureTokens.NomFichier)));
        return dossier;
    }

    [Fact]
    public void Lire_ouvre_les_seuls_mois_qui_chevauchent_la_plage_et_trie_par_slot()
    {
        var de = Utc("2026-09-30T22:00:00Z");
        var a = Utc("2026-10-01T02:00:00Z");

        var lecture = LecteurAgregats.Lire(TestDataDir("a-cheval-mois"), de, a);

        Assert.Equal(
            new[] { Utc("2026-09-30T23:45:00Z"), Utc("2026-10-01T00:00:00Z"), Utc("2026-10-01T00:15:00Z"), Utc("2026-10-01T01:45:00Z") },
            lecture.Tranches.Select(t => t.Slot));   // 02:00Z EXCLU : [de, a[
        Assert.Equal(0, lecture.LignesIgnorees);
        Assert.Equal(new Plage(de, a), lecture.Plage);

        var octobreSeul = LecteurAgregats.Lire(TestDataDir("a-cheval-mois"), Utc("2026-10-01T00:00:00Z"), Utc("2026-10-01T01:00:00Z"));
        Assert.Equal(2, octobreSeul.Tranches.Count);
    }

    [Fact]
    public void Lire_est_tolerant_et_compte_les_lignes_refusees()
    {
        var lecture = LecteurAgregats.Lire(TestDataDir("tolerance"), Utc("2026-09-01T00:00:00Z"), Utc("2026-09-01T02:00:00Z"));

        Assert.Equal(2, lecture.Tranches.Count);
        Assert.Equal(5, lecture.LignesIgnorees);
        Assert.Equal(Utc("2026-09-01T00:00:00Z"), lecture.Tranches[0].Slot);
        Assert.True(lecture.Tranches[0].Sub);
        Assert.Equal("claude-sonnet-5", lecture.Tranches[1].Model);
    }

    [Fact]
    public void Un_dossier_absent_rend_une_lecture_vide_sans_le_creer()
    {
        var inexistant = Path.Combine(Path.GetTempPath(), "chronos-tests", "inexistant-" + Guid.NewGuid().ToString("N"));
        var de = Utc("2026-09-01T00:00:00Z");
        var a = Utc("2026-10-01T00:00:00Z");

        var lecture = LecteurAgregats.Lire(inexistant, de, a);

        Assert.Empty(lecture.Tranches);
        Assert.Equal(0, lecture.LignesIgnorees);
        Assert.Null(lecture.PlusAncienneLigneVue);
        Assert.Equal(new[] { new SousPlageCouverture(de, a, EtatCouverture.HorsCouverture) }, lecture.Couverture);
        Assert.Equal(EtatCouverture.HorsCouverture, lecture.EtatA(Utc("2026-09-15T00:00:00Z")));
        Assert.False(Directory.Exists(inexistant), "Une lecture ne crée jamais le dossier des agrégats.");
    }

    [Fact]
    public void La_couverture_est_lue_a_cote_des_agregats_et_decoupee_en_sous_plages()
    {
        var dossier = DossierAvecCouverture();

        var lecture = LecteurAgregats.Lire(dossier, Utc("2026-10-24T22:00:00Z"), Utc("2026-10-25T23:00:00Z"));

        Assert.Equal(100, lecture.Tranches.Count);
        Assert.Equal(0, lecture.LignesIgnorees);
        Assert.Equal(Utc("2026-06-23T12:44:22Z"), lecture.PlusAncienneLigneVue);
        Assert.Equal(
            new[]
            {
                new SousPlageCouverture(Utc("2026-10-24T22:00:00Z"), Utc("2026-10-25T09:05:00Z"), EtatCouverture.Couverte),
                new SousPlageCouverture(Utc("2026-10-25T09:05:00Z"), Utc("2026-10-25T23:00:00Z"), EtatCouverture.TranscriptsAbsents),
            },
            lecture.Couverture);
        Assert.Equal(EtatCouverture.Couverte, lecture.EtatA(Utc("2026-10-25T00:00:00Z")));
        Assert.Equal(EtatCouverture.TranscriptsAbsents, lecture.EtatA(Utc("2026-10-25T12:00:00Z")));
    }

    [Fact]
    public void Une_plage_sans_tranche_hors_couverture_n_est_jamais_zero()
    {
        var dossier = DossierAvecCouverture();

        // Juin, avant le plus vieux transcript jamais vu : rien ne peut être dit.
        var juin = LecteurAgregats.Lire(dossier, Utc("2026-06-01T00:00:00Z"), Utc("2026-06-02T00:00:00Z"));
        Assert.Empty(juin.Tranches);
        Assert.Equal(EtatCouverture.HorsCouverture, juin.EtatA(Utc("2026-06-01T12:00:00Z")));
        // Un instant HORS de la plage lue n'est jamais « couverte » : on ne garantit pas ce qu'on n'a pas lu.
        Assert.Equal(EtatCouverture.HorsCouverture, juin.EtatA(Utc("2026-06-05T00:00:00Z")));

        // Le jour du plus vieux transcript : la plage se coupe À la plus ancienne ligne vue (12:44:22Z) — avant, hors
        // couverture ; après, transcripts absents (juin n'a pas d'intervalle garanti). Une seule sous-plage serait un mensonge.
        var bascule = LecteurAgregats.Lire(dossier, Utc("2026-06-23T00:00:00Z"), Utc("2026-06-24T00:00:00Z"));
        Assert.Empty(bascule.Tranches);
        Assert.Equal(
            new[]
            {
                new SousPlageCouverture(Utc("2026-06-23T00:00:00Z"), Utc("2026-06-23T12:44:22Z"), EtatCouverture.HorsCouverture),
                new SousPlageCouverture(Utc("2026-06-23T12:44:22Z"), Utc("2026-06-24T00:00:00Z"), EtatCouverture.TranscriptsAbsents),
            },
            bascule.Couverture);
        Assert.Equal(EtatCouverture.HorsCouverture, bascule.EtatA(Utc("2026-06-23T06:00:00Z")));
        Assert.Equal(EtatCouverture.TranscriptsAbsents, bascule.EtatA(Utc("2026-06-23T18:00:00Z")));

        // Juillet, purgé par Claude Code avant lecture : transcripts absents, pas « zéro token ».
        var juillet = LecteurAgregats.Lire(dossier, Utc("2026-07-15T00:00:00Z"), Utc("2026-07-16T00:00:00Z"));
        Assert.Empty(juillet.Tranches);
        Assert.Equal(EtatCouverture.TranscriptsAbsents, juillet.EtatA(Utc("2026-07-15T12:00:00Z")));

        // Dans l'intervalle garanti : les tranches sont là ET la plage est couverte.
        var couverte = LecteurAgregats.Lire(dossier, Utc("2026-10-24T22:00:00Z"), Utc("2026-10-24T23:00:00Z"));
        Assert.Equal(4, couverte.Tranches.Count);
        Assert.Equal(EtatCouverture.Couverte, couverte.EtatA(Utc("2026-10-24T22:30:00Z")));
        Assert.All(couverte.Couverture, s => Assert.Equal(EtatCouverture.Couverte, s.Etat));
    }

    [Fact]
    public void TotauxTokens_somme_les_quatre_compteurs_separement_et_compte_N()
    {
        var slot = Utc("2026-09-01T00:00:00Z");
        var t1 = new TrancheTokens(slot, "claude-opus-5", false, 1, 10, 100, 1000, 1);
        var t2 = new TrancheTokens(slot, "claude-opus-5", true, 2, 20, 200, 2000, 3);

        Assert.Equal(new TotauxTokens(3, 30, 300, 3000, 4), TotauxTokens.Somme(new[] { t1, t2 }));
        Assert.Equal(TotauxTokens.Somme(new[] { t1 }), TotauxTokens.Vide.Plus(t1));
        Assert.Equal(new TotauxTokens(0, 0, 0, 0, 0), TotauxTokens.Vide);

        // Aucun membre n'expose une somme des quatre compteurs : exactement In, Out, CacheW, CacheR, N — et rien d'autre.
        var proprietes = typeof(TotauxTokens).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "CacheR", "CacheW", "In", "N", "Out" }, proprietes);
        Assert.All(typeof(TotauxTokens).GetProperties(), p => Assert.True(p.PropertyType == typeof(long) || p.PropertyType == typeof(int), $"{p.Name} : {p.PropertyType}"));
    }
}
