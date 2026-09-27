using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models.Historique.Tokens;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-01 / HIS-06 — la façade de lecture NEUTRE de la fenêtre Historique (<see cref="ISourceHistorique"/>) enchaîne sur
/// disque : journal → analyse → agrégats + couverture (chargée séparément, Pitfall 11) → rendu local → divergences, pour une
/// plage DÉJÀ calculée (les bornes sont l'affaire du VM). Elle ne lève jamais, ne crée jamais le dossier, et rend des données
/// vides — jamais nulles — quand il n'y a rien. Fixtures versionnées copiées dans un dossier temp ; jamais le vrai %APPDATA%.
/// </summary>
public sealed class SourceHistoriqueDisqueTests : IDisposable
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();
    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    // Le « now » d'AnalyseRelevesTests pour la fixture trou-arrete ; son dernier relevé (11:18Z) porte r7 = 2026-10-02T22:00Z.
    private static readonly DateTimeOffset Now = Utc("2026-09-27T11:20:00Z");
    private static readonly DateTimeOffset R7Fixture = Utc("2026-10-02T22:00:00Z");

    private readonly string _dir;
    private readonly ChronosPaths _paths;

    public SourceHistoriqueDisqueTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "chronos-tests", "source-historique-" + Guid.NewGuid().ToString("N"));
        _paths = new ChronosPaths(Path.Combine(_dir, "Chronos", "usage.json"), Path.Combine(_dir, "projects"));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* nettoyage best-effort */ }
    }

    private static string TestData(string relatif, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", relatif);

    private string DossierHistorique()
    {
        Directory.CreateDirectory(_paths.HistoriqueDir);
        return _paths.HistoriqueDir;
    }

    private void DeposerJournalTrouArrete()
        => File.Copy(TestData(Path.Combine("journal", "trou-arrete", "releves-2026-09.jsonl")), Path.Combine(DossierHistorique(), "releves-2026-09.jsonl"));

    private void DeposerTokensDst()
        => File.Copy(TestData(Path.Combine("tokens", "dst-2026-10-25", "tokens-2026-10.jsonl")), Path.Combine(DossierHistorique(), "tokens-2026-10.jsonl"));

    private void DeposerCouverture(DateTimeOffset now)
    {
        var couverture = new CouvertureTokens();
        couverture.VoirLigne(Utc("2026-06-23T12:44:22Z"));
        couverture.GarantirPasse(now, now);
        Assert.True(couverture.Sauvegarder(Path.Combine(DossierHistorique(), CouvertureTokens.NomFichier)));
    }

    [Fact]
    public void Un_dossier_absent_rend_des_donnees_vides_sans_exception_ni_creation()
    {
        var source = new SourceHistoriqueDisque(_paths, Tz);
        var semaine = BornesPlage.SemaineDeForfait(Now, null, null, Tz);
        var precedente = BornesPlage.SemaineDeForfait(semaine.Debut.AddTicks(-1), semaine.Debut, null, Tz);

        Assert.Null(source.RepereHebdo(Now));
        var d = source.LireSemaine(semaine, precedente, Now);

        Assert.Empty(d.Analyse.Serie);
        Assert.Equal(168, d.Barres.Count);
        Assert.Empty(d.Divergences);
        Assert.Null(d.JournalOuvertLe);
        var sousPlage = Assert.Single(d.CouvertureTokens);
        Assert.Equal(EtatCouverture.HorsCouverture, sousPlage.Etat);
        Assert.Equal(semaine, d.Plage);
        Assert.Equal(Now, d.LueA);
        Assert.False(Directory.Exists(_paths.HistoriqueDir), "Une lecture ne crée jamais le dossier de l'historique.");
    }

    [Fact]
    public void Le_repere_hebdo_est_le_R7_du_dernier_releve()
    {
        DeposerJournalTrouArrete();
        var source = new SourceHistoriqueDisque(_paths, Tz);

        Assert.Equal(R7Fixture, source.RepereHebdo(Now));
    }

    [Fact]
    public void LireSemaine_enchaine_journal_analyse_agregats_et_couverture()
    {
        DeposerJournalTrouArrete();
        DeposerTokensDst();
        DeposerCouverture(Now);
        var source = new SourceHistoriqueDisque(_paths, Tz);
        var semaine = BornesPlage.SemaineDeForfait(Now, R7Fixture, null, Tz);   // sam. 26 sept. → sam. 3 oct. : contient la fixture
        var precedente = BornesPlage.SemaineDeForfait(semaine.Debut.AddTicks(-1), semaine.Debut, null, Tz);

        var d = source.LireSemaine(semaine, precedente, Now);

        Assert.Single(d.Analyse.Trous);
        Assert.NotEmpty(d.Analyse.Sauts);
        Assert.Empty(d.Precedente.Serie);
        Assert.Equal(precedente, d.PlagePrecedente);
        Assert.Equal(168, d.Barres.Count);
        Assert.All(d.Barres, b => Assert.NotEqual(EtatCouverture.HorsCouverture, b.Etat));
        Assert.Equal(Now, d.LueA);
        Assert.Equal(Utc("2026-09-27T10:00:00Z"), d.JournalOuvertLe);
    }

    [Fact]
    public void LireJour_donne_les_colonnes_par_quart_d_heure_du_jour_local()
    {
        DeposerTokensDst();
        var source = new SourceHistoriqueDisque(_paths, Tz);
        var jour = BornesPlage.Jour(Utc("2026-10-25T12:00:00Z"), Tz);   // 25 h

        var d = source.LireJour(jour, Now);

        Assert.Equal(100, d.Colonnes.Count);
        Assert.Empty(d.Analyse.Serie);
        Assert.Equal(jour, d.Plage);
        Assert.Contains(d.Colonnes, c => c.ParModele.Count > 0);
    }
}
