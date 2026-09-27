using System.Globalization;
using System.IO;
using Chronos.Models.Historique.Tokens;
using Chronos.Services.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// TOK-04 (fondation) — la couverture des agrégats de tokens est un état PERSISTÉ et daté, jamais déduit des mtimes :
/// « juillet purgé » est vrai par mtime et FAUX par contenu (23 211 lignes de juillet vivent dans des fichiers
/// d'août/septembre, mesuré le 2026-09-27). Trois états, jamais un « zéro » implicite : <b>hors couverture</b> (avant le
/// plus vieux transcript jamais vu), <b>transcripts absents</b> (Claude Code a pu purger avant que Chronos ne lise ;
/// présence PARTIELLE possible — juillet 2026), <b>couverte</b> (l'absence de tranche est une vraie absence d'activité).
/// Chaque passe complète garantit <c>[début − HorizonPurge, fin]</c>, <c>HorizonPurge = 30 j</c> (HYP-4 : le
/// <c>cleanupPeriodDays</c> par défaut de Claude Code, non lu ici).
///
/// Isolation stricte : dossier temp unique, sans horloge (la couverture ne date rien elle-même).
/// </summary>
public class CouvertureTokensTests : IDisposable
{
    private readonly string _dir;

    public CouvertureTokensTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ChronosCouverture_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private string Chemin => Path.Combine(_dir, CouvertureTokens.NomFichier);

    [Fact]
    public void Une_couverture_vide_classe_tout_hors_couverture()
    {
        var couverture = new CouvertureTokens();

        Assert.Equal(EtatCouverture.HorsCouverture, couverture.Classer(Utc("2026-07-01T00:00:00Z")));
        Assert.Equal(EtatCouverture.HorsCouverture, couverture.Classer(Utc("2026-09-27T09:00:00Z")));
        Assert.Null(couverture.PlusAncienneLigneVue);
        Assert.Empty(couverture.Intervalles);
        Assert.Null(couverture.DerniereErreur);
        Assert.Equal(TimeSpan.FromDays(30), CouvertureTokens.HorizonPurge);
        Assert.Equal("couverture.json", CouvertureTokens.NomFichier);
    }

    [Fact]
    public void VoirLigne_ne_fait_que_descendre()
    {
        var couverture = new CouvertureTokens();

        couverture.VoirLigne(Utc("2026-08-01T00:00:00Z"));
        Assert.Equal(Utc("2026-08-01T00:00:00Z"), couverture.PlusAncienneLigneVue);

        couverture.VoirLigne(Utc("2026-06-23T12:44:22Z"));
        couverture.VoirLigne(Utc("2026-09-01T00:00:00Z"));

        Assert.Equal(Utc("2026-06-23T12:44:22Z"), couverture.PlusAncienneLigneVue);
    }

    [Fact]
    public void GarantirPasse_ajoute_l_intervalle_recule_de_l_horizon_de_purge_et_fusionne()
    {
        var couverture = new CouvertureTokens();

        couverture.GarantirPasse(Utc("2026-09-27T09:00:00Z"), Utc("2026-09-27T09:05:00Z"));
        var seul = Assert.Single(couverture.Intervalles);
        Assert.Equal(new IntervalleGaranti(Utc("2026-08-28T09:00:00Z"), Utc("2026-09-27T09:05:00Z")), seul);

        couverture.GarantirPasse(Utc("2026-09-27T10:00:00Z"), Utc("2026-09-27T10:01:00Z"));   // chevauche : fusionné
        seul = Assert.Single(couverture.Intervalles);
        Assert.Equal(Utc("2026-08-28T09:00:00Z"), seul.Debut);
        Assert.Equal(Utc("2026-09-27T10:01:00Z"), seul.Fin);

        couverture.GarantirPasse(Utc("2027-01-10T00:00:00Z"), Utc("2027-01-10T00:01:00Z"));   // après un arrêt de 3 mois : disjoint
        Assert.Equal(2, couverture.Intervalles.Count);
        Assert.Equal(Utc("2026-08-28T09:00:00Z"), couverture.Intervalles[0].Debut);
        Assert.Equal(Utc("2026-12-11T00:00:00Z"), couverture.Intervalles[1].Debut);
        Assert.Equal(Utc("2027-01-10T00:01:00Z"), couverture.Intervalles[1].Fin);
        Assert.True(couverture.Intervalles[0].Fin < couverture.Intervalles[1].Debut);
    }

    [Fact]
    public void Classer_distingue_les_trois_etats_et_juillet_est_transcripts_absents()
    {
        var couverture = new CouvertureTokens();
        couverture.VoirLigne(Utc("2026-06-23T12:44:22Z"));
        couverture.GarantirPasse(Utc("2026-09-27T09:00:00Z"), Utc("2026-09-27T09:05:00Z"));

        Assert.Equal(EtatCouverture.HorsCouverture, couverture.Classer(Utc("2026-06-01T00:00:00Z")));
        Assert.Equal(EtatCouverture.TranscriptsAbsents, couverture.Classer(Utc("2026-07-15T10:00:00Z")));   // juillet : jamais « zéro »
        Assert.Equal(EtatCouverture.Couverte, couverture.Classer(Utc("2026-08-28T09:00:00Z")));           // borne incluse
        Assert.Equal(EtatCouverture.TranscriptsAbsents, couverture.Classer(Utc("2026-09-27T09:05:00Z")));  // borne exclue
        Assert.Equal(EtatCouverture.Couverte, couverture.Classer(Utc("2026-09-20T00:00:00Z")));
    }

    [Fact]
    public void Sauvegarder_puis_Charger_rend_le_meme_etat_et_le_fichier_est_atomique()
    {
        var couverture = new CouvertureTokens();
        couverture.VoirLigne(Utc("2026-06-23T12:44:22Z"));
        couverture.GarantirPasse(Utc("2026-09-27T09:00:00Z"), Utc("2026-09-27T09:05:00Z"));
        couverture.GarantirPasse(Utc("2027-01-10T00:00:00Z"), Utc("2027-01-10T00:01:00Z"));

        Assert.True(couverture.Sauvegarder(Chemin));
        Assert.Null(couverture.DerniereErreur);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp-*"));

        var json = File.ReadAllText(Chemin);
        Assert.Contains("\"v\":1", json);
        Assert.Contains("\"plus_ancienne_ligne_vue\":\"2026-06-23T12:44:22.0000000+00:00\"", json);
        Assert.Contains("\"intervalles\"", json);
        Assert.Contains("\"debut\":\"2026-08-28T09:00:00.0000000+00:00\"", json);
        Assert.Contains("\"fin\":\"2027-01-10T00:01:00.0000000+00:00\"", json);

        var relue = CouvertureTokens.Charger(Chemin);
        Assert.Equal(couverture.PlusAncienneLigneVue, relue.PlusAncienneLigneVue);
        Assert.Equal(couverture.Intervalles, relue.Intervalles);
        Assert.Equal(EtatCouverture.TranscriptsAbsents, relue.Classer(Utc("2026-07-15T10:00:00Z")));
        Assert.Equal(EtatCouverture.Couverte, relue.Classer(Utc("2026-09-20T00:00:00Z")));

        // Un chemin impossible (sous un fichier) : false + erreur consignée, jamais d'exception.
        var poison = Path.Combine(Chemin, "impossible.json");
        Assert.False(couverture.Sauvegarder(poison));
        Assert.NotNull(couverture.DerniereErreur);
    }

    [Fact]
    public void Charger_un_fichier_absent_ou_corrompu_rend_une_couverture_vide_sans_lever()
    {
        var absente = CouvertureTokens.Charger(Path.Combine(_dir, "absent", CouvertureTokens.NomFichier));
        Assert.Null(absente.PlusAncienneLigneVue);
        Assert.Empty(absente.Intervalles);

        File.WriteAllText(Chemin, "{");
        var tronquee = CouvertureTokens.Charger(Chemin);
        Assert.Null(tronquee.PlusAncienneLigneVue);
        Assert.Empty(tronquee.Intervalles);

        File.WriteAllText(Chemin, "{\"v\":2,\"plus_ancienne_ligne_vue\":\"2026-06-23T12:44:22.0000000+00:00\",\"intervalles\":[{\"debut\":\"2026-08-28T09:00:00.0000000+00:00\",\"fin\":\"2026-09-27T09:05:00.0000000+00:00\"}]}");
        var autreVersion = CouvertureTokens.Charger(Chemin);
        Assert.Null(autreVersion.PlusAncienneLigneVue);
        Assert.Empty(autreVersion.Intervalles);
        Assert.Equal(EtatCouverture.HorsCouverture, autreVersion.Classer(Utc("2026-09-20T00:00:00Z")));
    }
}
