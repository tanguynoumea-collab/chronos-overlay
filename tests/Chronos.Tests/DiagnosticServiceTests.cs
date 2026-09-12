using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Le diagnostic explique l'état réel (token, sources, plafonds, résultat affiché) — et n'expose
/// JAMAIS le token en clair dans le rapport (sécurité).
/// </summary>
public class DiagnosticServiceTests
{
    private static ChronosPaths TempPaths()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChronosDiag_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        return new ChronosPaths(System.IO.Path.Combine(dir, "usage.json"), System.IO.Path.Combine(dir, "projects"));
    }

    /// <summary>Extrait la ligne « 5 h » de la section « Ce qui est affiché maintenant ». Le rapport
    /// ENTIER contient légitimement un tilde — « Dossier ~/.claude/projects » — donc une assertion
    /// posée sur le rapport entier serait rouge pour une raison ÉTRANGÈRE à ce qu'elle prouve. On
    /// restreint la preuve à la ligne concernée plutôt que de l'affaiblir en la supprimant.</summary>
    private static string LigneCinqHeures(string report)
        => report.Split('\n').Single(l => l.TrimStart().StartsWith("5 h"));

    private sealed class StubProvider : IUsageProvider
    {
        private readonly UsageSnapshot _snap;
        public StubProvider(UsageSnapshot snap) => _snap = snap;
        public Task<UsageSnapshot> GetAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(_snap);
    }

    [Fact]
    public async Task Rapport_sans_token_conseille_et_n_expose_jamais_le_token()
    {
        var paths = TempPaths();
        var settings = new SettingsService(paths);
        var reader = new FakeClaudeTokenReader { Token = "SECRET-TOKEN-NE-DOIT-PAS-APPARAITRE" };
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Estimated, Utilization = null },
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        };
        var diag = new DiagnosticService(reader, paths, settings, new StubProvider(snap), new FakeClock(DateTimeOffset.UtcNow),
                                         machine: new FakeInventaireMachine());

        var report = await diag.BuildReportAsync();

        Assert.Contains("Diagnostic", report);
        Assert.Contains("Usage exact (OAuth)", report);
        Assert.Contains("Token déchiffré : OUI", report);          // présence signalée…
        Assert.DoesNotContain("SECRET-TOKEN", report);              // …mais JAMAIS la valeur
        // 19-04/20-05 : le mot « estimé » n'existe plus, et le tilde non plus — l'incertitude d'un
        // plancher est UNILATÉRALE. Forme DÉGRADÉE (ni Source ni Provenance), qui doit rester lisible.
        Assert.Contains("PLANCHER", report);                        // résultat affiché décrit
        Assert.DoesNotContain("~", LigneCinqHeures(report));
        Assert.Contains("source : non renseignée", LigneCinqHeures(report));
        Assert.Contains("relevé de date inconnue", LigneCinqHeures(report));
    }

    [Fact]
    public async Task Rapport_token_absent_le_signale_clairement()
    {
        var paths = TempPaths();
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
            new SettingsService(paths), new StubProvider(UsageSnapshot.Empty), new FakeClock(DateTimeOffset.UtcNow),
                                         machine: new FakeInventaireMachine());

        var report = await diag.BuildReportAsync();

        Assert.Contains("Token déchiffré : NON", report);
        Assert.Contains("Conseil", report);
    }

    /// <summary>TOK-02 : le rapport nomme l'état d'authentification RÉEL, pas la seule présence
    /// du fichier oauth.dat — c'est cette confusion qui a laissé l'utilisateur deux mois dans le noir
    /// (jeton expiré le 2026-07-12, oauth.dat parfaitement présent, diagnostic affichant « Connecté :
    /// OUI »). Montage à Token = null : la sonde réseau est gardée par `if (token is not null)`,
    /// donc ce test n'émet AUCUNE requête.</summary>
    [Fact]
    public async Task Le_rapport_nomme_l_etat_d_authentification_reel()
    {
        var paths = TempPaths();
        var settings = new SettingsService(paths);
        var auth = new FakeAuthStatus { Etat = EtatAuthentification.Deconnecte };
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         settings, new StubProvider(UsageSnapshot.Empty),
                                         new FakeClock(DateTimeOffset.UtcNow), auth,
                                         machine: new FakeInventaireMachine());

        var report = await diag.BuildReportAsync();

        Assert.Contains("État d'authentification : DÉCONNECTÉ", report);
        Assert.Contains("Token déchiffré : NON", report);   // assertion existante préservée
    }

    // --- Phase 18 (HDR-01/HDR-03/HDR-04/HDR-06) : le rapport dit ce que la SONDE reçoit ---
    //
    // SÉCURITÉ, commune aux quatre tests : tous montent un FakeClaudeTokenReader à Token = null, donc la
    // sonde réseau de la section « endpoint OAuth » — gardée par `if (token is not null)` — n'est JAMAIS
    // atteinte. Aucune requête ne part, aucun coffre réel n'est lu, et le settings.json vit sous %TEMP%.

    /// <summary>HDR-01/HDR-02/HDR-06 : la sonde est la première source de la chaîne, et l'utilisateur doit
    /// pouvoir constater SEUL ce qu'elle a reçu. Le cas asserté est celui qui justifie toute la phase :
    /// un 429 qui livre quand même ses en-têtes — à ne JAMAIS confondre avec « pas de données ».</summary>
    [Fact]
    public async Task Le_rapport_nomme_la_sonde_d_en_tetes_et_son_issue()
    {
        var paths = TempPaths();
        var etat = new FakeEtatServeur
        {
            DernierResultat = ResultatSonde.SaturationEnTetesLus,
            NomsEnTetesRecus = new[]
            {
                EnTetesDeReference.H5hUtil, EnTetesDeReference.H5hReset,
                EnTetesDeReference.H7dUtil, EnTetesDeReference.H7dReset,
            },
        };
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                         new FakeClock(DateTimeOffset.UtcNow), null, etat,
                                         new FakeInventaireMachine());

        var report = await diag.BuildReportAsync();

        Assert.Contains("[Source exacte — sonde d'en-têtes de rate-limit]", report);
        Assert.Contains("429 — en-têtes lus quand même", report);
        Assert.Contains(EnTetesDeReference.H5hUtil, report);
        Assert.Contains(EnTetesDeReference.H5hReset, report);
        Assert.Contains(EnTetesDeReference.H7dUtil, report);
        Assert.Contains(EnTetesDeReference.H7dReset, report);
        Assert.Contains("(4)", report);   // le compte des noms, pas leurs valeurs
    }

    /// <summary>Le signal le plus précieux de la phase, parce que c'est le seul que personne ne peut
    /// obtenir autrement : la famille « anthropic-ratelimit-unified-* » est ABSENTE de la documentation
    /// publique Anthropic. Un 200 sans aucun en-tête reconnu veut dire « elle a été renommée », et
    /// certainement pas « 0 % de quota consommé » — le mensonge inverse de celui que v1.5 corrige.</summary>
    [Fact]
    public async Task Le_rapport_dit_quand_AUCUN_en_tete_unified_n_est_reconnu()
    {
        var paths = TempPaths();
        var etat = new FakeEtatServeur
        {
            DernierResultat = ResultatSonde.SuccesSansEnTetes,
            NomsEnTetesRecus = Array.Empty<string>(),
        };
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                         new FakeClock(DateTimeOffset.UtcNow), null, etat,
                                         new FakeInventaireMachine());

        var report = await diag.BuildReportAsync();

        Assert.Contains("200 mais AUCUN en-tête unified reconnu", report);
        Assert.Contains("En-têtes « unified » reconnus : AUCUN", report);
        Assert.Contains("n'est documentée nulle part chez Anthropic", report);
    }

    /// <summary>HDR-04 : le dépassement arrive par le canal LATÉRAL (il survit à un Best() défavorable) et
    /// il est rendu en pourcentage d'affichage, PAS en valeur d'en-tête brute. La fraction 0,34 venue du
    /// réseau ne doit apparaître nulle part : le rapport ne recopie jamais une valeur d'en-tête.</summary>
    [Fact]
    public async Task Le_rapport_affiche_le_depassement_et_jamais_une_valeur_d_en_tete_brute()
    {
        var paths = TempPaths();
        var etat = new FakeEtatServeur
        {
            Depassement = new EtatDepassement
            {
                Utilization = 0.34,
                ResetsAt = new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero),
                Statut = StatutServeur.Rejete,
            },
        };
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                         new FakeClock(DateTimeOffset.UtcNow), null, etat,
                                         new FakeInventaireMachine());

        var report = await diag.BuildReportAsync();

        Assert.Contains("34 %", report);
        Assert.Contains("REJETÉ", report);
        Assert.DoesNotContain("0.34", report);   // la valeur brute d'en-tête n'est JAMAIS recopiée
    }

    /// <summary>Les 9 sites de construction préexistants laissent <c>etatServeur</c> à null : le rapport
    /// doit rester lisible et ne rien inventer. « Pas encore sondé » est un ÉTAT INITIAL, pas une panne —
    /// les confondre rejouerait la panne silencieuse sous un autre nom.</summary>
    [Fact]
    public async Task Un_diagnostic_sans_sonde_reste_lisible()
    {
        var paths = TempPaths();
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Estimated, Utilization = null },
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        };
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         new SettingsService(paths), new StubProvider(snap),
                                         new FakeClock(DateTimeOffset.UtcNow),
                                         machine: new FakeInventaireMachine());

        var report = await diag.BuildReportAsync();

        Assert.Contains("pas encore sondé", report);
        Assert.Contains("aucun dépassement rapporté", report);
        Assert.Contains("PLANCHER", report);                 // assertion existante, vocabulaire 19-04
        Assert.DoesNotContain("~", LigneCinqHeures(report));  // « ≥ » partout, plus jamais « ~ »
        Assert.Contains("Token déchiffré : NON", report);    // assertion existante préservée
    }
    // --- Phase 20 (EXA-06) : le rapport nomme QUI alimente chaque fenêtre, et DEPUIS QUAND ---
    //
    // SÉCURITÉ, commune aux quatre tests : Token = null, donc la sonde réseau de la section
    // « endpoint OAuth » — gardée par « if (token is not null) » — n'est jamais atteinte. Aucune
    // requête ne part. Le faux inventaire de machine évite les 18 s de sondage d'environnement réel.

    /// <summary>EXA-06, première moitié : le rapport nomme la source de CHAQUE fenêtre, et les deux
    /// peuvent différer — le composite choisit la meilleure source PAR FENÊTRE.</summary>
    [Fact]
    public async Task Le_rapport_nomme_la_SOURCE_qui_alimente_chaque_fenetre()
    {
        var paths = TempPaths();
        var now = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState
            {
                Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
                Utilization = 0.42, Source = SourceUsage.SondeEnTetes, CapturedAt = now,
            },
            SevenDay = new WindowState
            {
                Kind = WindowKind.SevenDay, Reliability = SourceReliability.Exact,
                Utilization = 0.10, Source = SourceUsage.MagasinDernierExact, CapturedAt = now,
            },
        };
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         new SettingsService(paths), new StubProvider(snap),
                                         new FakeClock(now), machine: new FakeInventaireMachine());

        var report = await diag.BuildReportAsync();

        Assert.Contains("source : sonde d'en-têtes de rate-limit", report);
        Assert.Contains("source : dernier exact persisté", report);
    }

    /// <summary>EXA-06, seconde moitié : un chiffre exact sans son âge ne vaut rien — c'est exactement
    /// la panne de deux mois (un « 10 % » figé, parfaitement affiché, jamais daté).</summary>
    [Fact]
    public async Task Le_rapport_donne_l_ANCIENNETE_du_releve_de_chaque_fenetre()
    {
        var paths = TempPaths();
        var now = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState
            {
                Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
                Utilization = 0.42, Source = SourceUsage.SondeEnTetes,
                CapturedAt = now - TimeSpan.FromMinutes(12),
            },
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        };
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         new SettingsService(paths), new StubProvider(snap),
                                         new FakeClock(now), machine: new FakeInventaireMachine());

        var report = await diag.BuildReportAsync();

        Assert.Contains("relevé il y a 12 min", report);
    }

    /// <summary>Règle de non-retour : une absence de source ne produit JAMAIS d'affirmation. Le dernier
    /// Assert vise la LIGNE de résultat et non la section de la sonde, qui porte légitimement le même
    /// libellé entre crochets (« [Source exacte — sonde d'en-têtes de rate-limit] »).</summary>
    [Fact]
    public async Task Une_source_absente_se_dit_non_renseignee_et_JAMAIS_un_nom_par_defaut()
    {
        var paths = TempPaths();
        var now = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState
            {
                Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
                Utilization = 0.42, Source = null, CapturedAt = null,
            },
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        };
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         new SettingsService(paths), new StubProvider(snap),
                                         new FakeClock(now), machine: new FakeInventaireMachine());

        var report = await diag.BuildReportAsync();

        Assert.Contains("source : non renseignée", report);
        Assert.Contains("relevé de date inconnue", report);
        Assert.DoesNotContain("sonde d'en-têtes de rate-limit · relevé", report);
    }

    /// <summary>Le diagnostic et le cadran parlent le MÊME français : « ≥ », jamais « ~ ». Un tilde
    /// dirait « autour de 42 », ce qui autoriserait la lecture « peut-être 38 » — or on SAIT que le
    /// quota consommé vaut au moins 42 %, c'est la borne supérieure qui est inconnue (19-04).</summary>
    [Fact]
    public async Task Un_plancher_est_decrit_avec_superieur_ou_egal_et_sa_provenance()
    {
        var paths = TempPaths();
        var now = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState
            {
                Kind = WindowKind.FiveHour, Reliability = SourceReliability.Estimated,
                Utilization = 0.42, Source = SourceUsage.MagasinDernierExact,
                CapturedAt = now - TimeSpan.FromMinutes(12),
                Provenance = ProvenanceReleve.PlancherAvecActivite,
            },
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        };
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         new SettingsService(paths), new StubProvider(snap),
                                         new FakeClock(now), machine: new FakeInventaireMachine());

        var report = await diag.BuildReportAsync();

        Assert.Contains("PLANCHER — ≥ 42 %", report);
        Assert.Contains("borne inférieure (activité depuis)", report);
        Assert.DoesNotContain("~", LigneCinqHeures(report));
    }
}
