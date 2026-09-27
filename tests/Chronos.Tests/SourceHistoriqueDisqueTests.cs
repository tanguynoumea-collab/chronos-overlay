using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models;
using Chronos.Models.Historique;
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

    // ------------------------------------------------------------------ 35-01 : quatre semaines (D-35-01)

    // Le « now » du scénario (jeu. 24 sept. 17:12 Paris) et ses quatre semaines : S-3 = 29 août, S-2 = 5 sept., S-1 = 12 sept., S = 19 sept.
    private static readonly DateTimeOffset NowQuatre = Utc("2026-09-24T15:12:00Z");
    private static IReadOnlyList<Plage> QuatreSemaines() => BornesPlage.QuatreSemaines(NowQuatre, Utc("2026-09-25T22:00:00Z"), null, Tz);

    private static ReleveJournal ReleveSonde(DateTimeOffset t, double u7)
        => new(t, SourceUsage.SondeEnTetes, 0.10, t + TimeSpan.FromHours(1), StatutServeur.Autorise,
               u7, Utc("2026-09-25T22:00:00Z"), StatutServeur.Autorise, null, null);

    // Un journal réel (écrit par JournalReleves) : quelques relevés dans S-1 (le premier ouvre le journal, le dernier à S-1.Fin − 5 min)
    // et dans S ; rien dans S-3 ni S-2.
    private DateTimeOffset EcrireJournalQuatreSemaines(Plage semaineMoinsUn, Plage semaine)
    {
        var horloge = new FakeClock(NowQuatre);
        var journal = new JournalReleves(DossierHistorique(), horloge);
        var premier = Utc("2026-09-14T10:00:00Z");
        var instants = new[]
        {
            premier, premier + TimeSpan.FromMinutes(5), premier + TimeSpan.FromMinutes(10),
            semaineMoinsUn.Fin - TimeSpan.FromMinutes(10), semaineMoinsUn.Fin - TimeSpan.FromMinutes(5),
            semaine.Debut + TimeSpan.FromHours(12), semaine.Debut + TimeSpan.FromHours(12) + TimeSpan.FromMinutes(5),
            NowQuatre - TimeSpan.FromMinutes(5),
        };
        var u7 = 0.10;
        foreach (var t in instants)
        {
            horloge.UtcNow = t;
            Assert.True(journal.AjouterReleve(ReleveSonde(t, u7)));
            u7 += 0.01;
        }
        return premier;
    }

    [Fact]
    public void Quatre_semaines_une_lecture_quatre_analyses_dans_l_ordre()
    {
        var semaines = QuatreSemaines();
        var premier = EcrireJournalQuatreSemaines(semaines[2], semaines[3]);
        var source = new SourceHistoriqueDisque(_paths, Tz);

        var d = source.LireQuatreSemaines(semaines, NowQuatre);

        Assert.Equal(4, d.Semaines.Count);
        for (var i = 0; i < 4; i++) Assert.Equal(semaines[i], d.Semaines[i].Plage);
        Assert.Empty(d.Semaines[0].Serie);
        Assert.Empty(d.Semaines[1].Serie);
        Assert.Equal(5, d.Semaines[2].Serie.Count);
        Assert.Equal(3, d.Semaines[3].Serie.Count);
        Assert.All(d.Semaines[2].Serie, r => Assert.True(semaines[2].Contient(r.T)));
        Assert.Equal(premier, d.JournalOuvertLe);
        Assert.Equal(NowQuatre, d.LueA);
        Assert.Same(d.Semaines[3], d.Courante);
    }

    [Fact]
    public void Quatre_semaines_revolues_ne_finissent_pas_par_un_faux_trou_ouvert()
    {
        var semaines = QuatreSemaines();
        EcrireJournalQuatreSemaines(semaines[2], semaines[3]);
        var source = new SourceHistoriqueDisque(_paths, Tz);

        var d = source.LireQuatreSemaines(semaines, NowQuatre);

        Assert.Equal(semaines[2].Fin - TimeSpan.FromMinutes(5), d.Semaines[2].Serie[^1].T);
        Assert.DoesNotContain(d.Semaines[2].Trous, t => t.Fin is null);
        Assert.DoesNotContain(d.Semaines[3].Trous, t => t.Fin is null);   // dernier relevé de S à now − 5 min
    }

    [Fact]
    public void Quatre_semaines_sur_un_dossier_absent_rendent_quatre_semaines_vides()
    {
        var semaines = QuatreSemaines();
        var source = new SourceHistoriqueDisque(_paths, Tz);

        var d = source.LireQuatreSemaines(semaines, NowQuatre);

        Assert.Equal(4, d.Semaines.Count);
        Assert.All(d.Semaines, a => Assert.Empty(a.Serie));
        for (var i = 0; i < 4; i++) Assert.Equal(semaines[i], d.Semaines[i].Plage);
        Assert.Null(d.JournalOuvertLe);
        Assert.Equal(NowQuatre, d.LueA);
        Assert.False(Directory.Exists(_paths.HistoriqueDir), "Une lecture ne crée jamais le dossier de l'historique.");

        Assert.Throws<ArgumentException>(() => source.LireQuatreSemaines(semaines.Take(3).ToList(), NowQuatre));
    }

    // ------------------------------------------------------------------ 35-01 : veille de minuit en vue Jour (D-35-04)

    [Fact]
    public void Le_jour_lit_la_veille_pour_nommer_le_trou_de_minuit()
    {
        // Mardi : relevés jusqu'à 23:00 Paris, « arret » à 23:02 ; mercredi : « demarrage » et relevés dès 07:00 (même fixture que LectureVeilleTests).
        var dernierMardi = Utc("2026-09-22T21:00:00Z");
        var premierMercredi = Utc("2026-09-23T05:00:00Z");
        var horloge = new FakeClock(Utc("2026-09-22T12:00:00Z"));
        var journal = new JournalReleves(DossierHistorique(), horloge);
        for (var t = Utc("2026-09-22T12:00:00Z"); t <= dernierMardi; t += TimeSpan.FromMinutes(5)) { horloge.UtcNow = t; Assert.True(journal.AjouterReleve(ReleveSonde(t, 0.30))); }
        horloge.UtcNow = dernierMardi + TimeSpan.FromMinutes(2);
        Assert.True(journal.AjouterEvenement(new EvenementJournal(horloge.UtcNow, TypeEvenement.Arret)));
        horloge.UtcNow = premierMercredi;
        Assert.True(journal.AjouterEvenement(new EvenementJournal(premierMercredi, TypeEvenement.Demarrage, Version: "tests")));
        for (var t = premierMercredi; t <= Utc("2026-09-23T06:00:00Z"); t += TimeSpan.FromMinutes(5)) { horloge.UtcNow = t; Assert.True(journal.AjouterReleve(ReleveSonde(t, 0.31))); }

        var source = new SourceHistoriqueDisque(_paths, Tz);
        var mercredi = BornesPlage.Jour(premierMercredi, Tz);
        var d = source.LireJour(mercredi, Utc("2026-09-23T06:02:00Z"));

        Assert.Contains(new Trou(dernierMardi, premierMercredi, CauseTrou.ChronosArrete), d.Analyse.Trous);
        Assert.Equal(premierMercredi, d.Analyse.Serie[0].T);   // la série rendue ne garde que le jour
        Assert.Equal(13, d.Analyse.Serie.Count);
        Assert.Equal(Utc("2026-09-22T12:00:00Z"), d.JournalOuvertLe);
        Assert.Equal("288 relevés attendus · 13 présents · 1 interruption (Chronos arrêté, mar. 23:00 → 07:00)",
            Chronos.Text.TextesHistorique.LigneFraicheurJour(mercredi, d.Analyse, RateLimitHeaderUsageProvider.CadenceNominale, Tz));
    }
}
