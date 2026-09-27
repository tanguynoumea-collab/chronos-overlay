using System.IO;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// JRN-01 / JRN-02 — le décorateur qui observe la chaîne exacte et journalise.
///
/// Ce qu'il prouve : seul l'EXACT de l'inner entre (jamais un plancher, jamais une fenêtre indisponible, jamais le
/// magasin — D-32-19) ; le rejeu du cache de la sonde (même instance 5 fois sur 6) n'écrit qu'une ligne (dédup
/// <c>t</c> strictement croissant par source) ; deux sources dans un même snapshot font deux lignes (D-32-15) ;
/// les six événements de couverture s'écrivent au bon moment, sur TRANSITION ; le snapshot rendu est la MÊME
/// instance (le journal ne touche pas à la chaîne).
///
/// Isolation stricte : dossier temp unique, faux injectés (<see cref="FakeClock"/>, <see cref="FakeUsageProvider"/>,
/// <see cref="FakeEtatServeur"/>, <see cref="FakeAuthStatus"/>), aucun réseau, aucun %APPDATA%.
/// </summary>
public class JournalisationUsageProviderTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 09, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset R5 = new(2026, 09, 27, 14, 50, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset R7 = new(2026, 10, 02, 22, 0, 0, TimeSpan.Zero);

    private readonly string _dir;
    private readonly FakeClock _clock = new(Now);
    private readonly FakeUsageProvider _inner = new();
    private readonly FakeEtatServeur _etat = new();
    private readonly FakeAuthStatus _auth = new();
    private readonly JournalReleves _journal;

    public JournalisationUsageProviderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ChronosJournalDeco_" + Guid.NewGuid().ToString("N"));
        _journal = new JournalReleves(_dir, _clock);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private JournalisationUsageProvider Deco(string? version = null)
        => new(_inner, _journal, _etat, _auth, _clock, version);

    private static WindowState Win(WindowKind kind, SourceReliability reliability, double? util, DateTimeOffset? resets,
                                   DateTimeOffset? captured, SourceUsage? source, StatutServeur? statut = null,
                                   EtatDepassement? depassement = null)
        => new()
        {
            Kind = kind,
            Reliability = reliability,
            Utilization = util,
            ResetsAt = resets,
            CapturedAt = captured,
            Source = source,
            StatutServeur = statut,
            Depassement = depassement,
        };

    private static UsageSnapshot SnapExact(DateTimeOffset captured, SourceUsage source = SourceUsage.SondeEnTetes)
        => new()
        {
            FiveHour = Win(WindowKind.FiveHour, SourceReliability.Exact, 0.12, R5, captured, source, StatutServeur.Autorise),
            SevenDay = Win(WindowKind.SevenDay, SourceReliability.Exact, 0.41, R7, captured, source, StatutServeur.AutoriseAvertissement),
        };

    private string Chemin => _journal.CheminDuMois(Now);
    private LectureFichier Lire() => LecteurJournal.LireFichier(Chemin);
    private string[] LignesBrutes() => File.ReadAllLines(Chemin);

    // --- JRN-01 : ce qui entre, ce qui n'entre pas ---

    [Fact]
    public async Task Six_GetAsync_sur_la_meme_instance_de_snapshot_ecrivent_une_seule_ligne()
    {
        _inner.Next = SnapExact(Now);
        var deco = Deco();

        for (var i = 0; i < 6; i++)
            Assert.Same(_inner.Next, await deco.GetAsync());   // le snapshot rendu est INCHANGÉ

        var lecture = Lire();
        var r = Assert.Single(lecture.Releves);
        Assert.Equal(Now, r.T);
        Assert.Equal(SourceUsage.SondeEnTetes, r.Source);
        Assert.Equal(0.12, r.U5);
        Assert.Equal(R5, r.R5);
        Assert.Equal(StatutServeur.Autorise, r.Statut5);
        Assert.Equal(0.41, r.U7);
        Assert.Equal(R7, r.R7);
        Assert.Equal(StatutServeur.AutoriseAvertissement, r.Statut7);
        Assert.Equal(1, deco.RelevesJournalises);
        Assert.Equal(6, _inner.GetCount);

        // La dédup MÉMOIRE du décorateur ne se confond pas avec l'idempotence du fichier : un rejeu ne touche PAS le disque
        // (pas de verrou exclusif toutes les 60 s pour rien). Si le fichier disparaît et que la même instance est rejouée,
        // il ne renaît pas — seule la mémoire « t strictement supérieur » a tranché.
        File.Delete(Chemin);
        await deco.GetAsync();
        await deco.GetAsync();
        Assert.False(File.Exists(Chemin));
        Assert.Equal(1, deco.RelevesJournalises);
    }

    [Fact]
    public async Task Un_nouveau_CapturedAt_ecrit_une_nouvelle_ligne()
    {
        var deco = Deco();
        _inner.Next = SnapExact(Now);
        await deco.GetAsync();
        _inner.Next = SnapExact(Now.AddMinutes(5));
        await deco.GetAsync();

        Assert.Equal(2, Lire().Releves.Count);
    }

    [Fact]
    public async Task Un_CapturedAt_plus_ancien_n_ecrit_rien()
    {
        var deco = Deco();
        _inner.Next = SnapExact(Now.AddMinutes(5));
        await deco.GetAsync();
        _inner.Next = SnapExact(Now);   // rejeu d'un relevé antérieur
        await deco.GetAsync();

        var r = Assert.Single(Lire().Releves);
        Assert.Equal(Now.AddMinutes(5), r.T);
    }

    [Fact]
    public async Task Un_plancher_estime_et_une_fenetre_indisponible_n_ecrivent_rien()
    {
        _inner.Next = new UsageSnapshot
        {
            FiveHour = Win(WindowKind.FiveHour, SourceReliability.Estimated, 0.30, R5, Now, SourceUsage.SondeEnTetes),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        };

        await Deco().GetAsync();

        Assert.False(File.Exists(Chemin));
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public async Task Une_fenetre_du_magasin_n_entre_jamais()
    {
        _inner.Next = SnapExact(Now, SourceUsage.MagasinDernierExact);   // impossible sous la tête, exclu quand même (D-32-19)

        await Deco().GetAsync();

        Assert.False(File.Exists(Chemin));
    }

    [Fact]
    public async Task Deux_sources_dans_le_meme_snapshot_donnent_deux_lignes_a_leur_instant()
    {
        _inner.Next = new UsageSnapshot
        {
            FiveHour = Win(WindowKind.FiveHour, SourceReliability.Exact, 0.12, R5, Now, SourceUsage.SondeEnTetes),
            SevenDay = Win(WindowKind.SevenDay, SourceReliability.Exact, 0.41, R7, Now.AddMinutes(-2), SourceUsage.EndpointOAuthChronos),
        };

        await Deco().GetAsync();

        var releves = Lire().Releves;
        Assert.Equal(2, releves.Count);
        // Dans l'ordre des instants : l'hebdo OAuth (09:58) puis la 5 h de la sonde (10:00).
        Assert.Equal(Now.AddMinutes(-2), releves[0].T);
        Assert.Equal(SourceUsage.EndpointOAuthChronos, releves[0].Source);
        Assert.Null(releves[0].U5);
        Assert.Null(releves[0].R5);
        Assert.Equal(0.41, releves[0].U7);
        Assert.Equal(R7, releves[0].R7);

        Assert.Equal(Now, releves[1].T);
        Assert.Equal(SourceUsage.SondeEnTetes, releves[1].Source);
        Assert.Equal(0.12, releves[1].U5);
        Assert.Equal(R5, releves[1].R5);
        Assert.Null(releves[1].U7);
        Assert.Null(releves[1].R7);
    }

    [Fact]
    public async Task Le_depassement_et_les_statuts_voyagent()
    {
        _inner.Next = new UsageSnapshot
        {
            FiveHour = Win(WindowKind.FiveHour, SourceReliability.Exact, 1.0, R5, Now, SourceUsage.SondeEnTetes, StatutServeur.Rejete,
                           new EtatDepassement { Utilization = 0.05, Statut = StatutServeur.Rejete }),
            SevenDay = Win(WindowKind.SevenDay, SourceReliability.Exact, 0.41, R7, Now, SourceUsage.SondeEnTetes, StatutServeur.Autorise),
        };

        await Deco().GetAsync();

        var lecture = Lire();
        var r = Assert.Single(lecture.Releves);
        Assert.Equal(0.05, r.Overage);
        Assert.Equal(StatutServeur.Rejete, r.OverageStatut);
        Assert.Equal(StatutServeur.Rejete, r.Statut5);
        var brute = Assert.Single(LignesBrutes(), l => !l.Contains("\"ev\""));
        Assert.Contains("\"overage\":0.05", brute);
        Assert.Contains("\"overage_statut\":\"Rejete\"", brute);
        Assert.Contains("\"statut5\":\"Rejete\"", brute);
    }

    // --- JRN-02 : événements de couverture ---

    [Fact]
    public async Task StartAsync_ecrit_demarrage_avec_la_version_et_StopAsync_ecrit_arret()
    {
        Directory.CreateDirectory(_dir);
        var vieux = Path.Combine(_dir, "releves-2020-01.jsonl");
        File.WriteAllText(vieux, "");
        var deco = Deco(version: "3.2.2");

        await deco.StartAsync(CancellationToken.None);

        Assert.False(File.Exists(vieux));   // Purger() au démarrage
        var premiere = LignesBrutes()[0];
        Assert.Contains("\"ev\":\"demarrage\"", premiere);
        Assert.Contains("\"version\":\"3.2.2\"", premiere);

        _inner.Next = SnapExact(Now);
        await deco.GetAsync();
        await deco.StopAsync(CancellationToken.None);

        var lignes = LignesBrutes();
        Assert.Equal(3, lignes.Length);
        Assert.Contains("\"ev\":\"arret\"", lignes[^1]);
        var lecture = Lire();
        Assert.Equal(new[] { TypeEvenement.Demarrage, TypeEvenement.Arret }, lecture.Evenements.Select(e => e.Type));
        Assert.Equal("3.2.2", lecture.Evenements[0].Version);
    }

    [Fact]
    public async Task Une_deconnexion_ecrit_jeton_invalide_une_fois_par_transition()
    {
        var deco = Deco();
        await deco.StartAsync(CancellationToken.None);

        _auth.Declencher(EtatAuthentification.Deconnecte);
        _auth.Declencher(EtatAuthentification.Deconnecte);   // réaffirmation : pas une transition
        _auth.Declencher(EtatAuthentification.Connecte);
        _auth.Declencher(EtatAuthentification.Deconnecte);

        // Écrit depuis le thread appelant : lisible immédiatement, sans attente.
        Assert.Equal(2, Lire().Evenements.Count(e => e.Type == TypeEvenement.JetonInvalide));

        await deco.StopAsync(CancellationToken.None);
        _auth.Declencher(EtatAuthentification.Deconnecte);   // désabonné : plus rien n'est écrit
        Assert.Equal(2, Lire().Evenements.Count(e => e.Type == TypeEvenement.JetonInvalide));
    }

    [Fact]
    public async Task Une_saturation_ou_un_refus_ecrit_sonde_refusee_sur_transition()
    {
        var deco = Deco();
        _inner.Next = SnapExact(Now);

        _etat.DernierResultat = ResultatSonde.SaturationEnTetesLus;
        await deco.GetAsync();
        var refus = Lire().Evenements.Where(e => e.Type == TypeEvenement.SondeRefusee).ToList();
        var seul = Assert.Single(refus);
        Assert.Equal("SaturationEnTetesLus", seul.Cause);

        await deco.GetAsync();   // toujours saturée : pas un nouvel événement
        Assert.Single(Lire().Evenements, e => e.Type == TypeEvenement.SondeRefusee);

        _etat.DernierResultat = ResultatSonde.SuccesEnTetesLus;
        await deco.GetAsync();
        _etat.DernierResultat = ResultatSonde.RefusServeur;
        await deco.GetAsync();
        refus = Lire().Evenements.Where(e => e.Type == TypeEvenement.SondeRefusee).ToList();
        Assert.Equal(2, refus.Count);
        Assert.Equal("RefusServeur", refus[1].Cause);

        // Un 429 « porteur » : la sonde a réussi à lire, mais la fenêtre porte « rejected » (HDR-02) — les en-têtes
        // survivent au refus : l'événement ET le relevé sont écrits.
        _etat.DernierResultat = ResultatSonde.SuccesEnTetesLus;
        _inner.Next = new UsageSnapshot
        {
            FiveHour = Win(WindowKind.FiveHour, SourceReliability.Exact, 1.0, R5, Now.AddMinutes(5), SourceUsage.SondeEnTetes, StatutServeur.Rejete),
            SevenDay = Win(WindowKind.SevenDay, SourceReliability.Exact, 0.41, R7, Now.AddMinutes(5), SourceUsage.SondeEnTetes, StatutServeur.Autorise),
        };
        await deco.GetAsync();

        var lecture = Lire();
        refus = lecture.Evenements.Where(e => e.Type == TypeEvenement.SondeRefusee).ToList();
        Assert.Equal(3, refus.Count);
        Assert.Equal("statut rejected", refus[2].Cause);
        Assert.Equal(2, lecture.Releves.Count);
        Assert.Equal(Now.AddMinutes(5), lecture.Releves[1].T);
    }

    [Fact]
    public async Task Un_ecart_de_plus_de_dix_minutes_ecrit_reprise_avant_le_releve()
    {
        var deco = Deco();
        _inner.Next = SnapExact(Now);
        await deco.GetAsync();
        _inner.Next = SnapExact(Now.AddMinutes(25));
        await deco.GetAsync();

        var lignes = LignesBrutes();
        Assert.Equal(3, lignes.Length);
        Assert.DoesNotContain("\"ev\"", lignes[0]);                              // relevé 10:00
        Assert.Contains("\"ev\":\"reprise\"", lignes[1]);                         // reprise, AVANT le relevé qui met fin au trou
        Assert.Contains("\"t\":\"2026-09-27T10:25:00.0000000+00:00\"", lignes[1]);
        Assert.Contains("\"cause\":\"trou de 25 min\"", lignes[1]);
        Assert.DoesNotContain("\"ev\"", lignes[2]);                              // relevé 10:25

        _inner.Next = SnapExact(Now.AddMinutes(30));   // 5 min ≤ 10 min : pas de reprise
        await deco.GetAsync();

        var lecture = Lire();
        Assert.Single(lecture.Evenements, e => e.Type == TypeEvenement.Reprise);
        Assert.Equal(3, lecture.Releves.Count);
    }

    [Fact]
    public void SignalerEcritureRatee_ecrit_l_evenement_avec_magasin_et_cause()
    {
        Deco().SignalerEcritureRatee("last-exact", "IOException : x");

        var e = Assert.Single(Lire().Evenements);
        Assert.Equal(TypeEvenement.EcritureRatee, e.Type);
        Assert.Equal("last-exact", e.Magasin);
        Assert.Equal("IOException : x", e.Cause);
        Assert.Equal(Now, e.T);
        var brute = Assert.Single(LignesBrutes());
        Assert.Contains("\"ev\":\"ecriture_ratee\",\"magasin\":\"last-exact\",\"cause\":\"IOException : x\"", brute);
    }
}
