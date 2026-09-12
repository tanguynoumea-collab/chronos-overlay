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

    /// <summary>Canal latéral dont l'issue ne devient connue QUE lorsque la chaîne a été interrogée —
    /// exactement le comportement de la vraie sonde, qui ne part qu'au premier <c>GetAsync</c>.</summary>
    private sealed class EtatServeurQuiSAllumeALaSonde : IEtatServeur
    {
        public bool ASonde { get; set; }
        public EtatDepassement? Depassement => null;
        public ResultatSonde DernierResultat => ASonde ? ResultatSonde.SuccesEnTetesLus : ResultatSonde.JamaisSondee;
        public IReadOnlyList<string> NomsEnTetesRecus => ASonde
            ? new[] { "anthropic-ratelimit-unified-5h-utilization" }
            : System.Array.Empty<string>();
        public event System.EventHandler<EtatDepassement?>? DepassementChange { add { } remove { } }
    }

    /// <summary>Provider qui ALLUME le canal latéral au moment où on l'interroge.</summary>
    private sealed class ProviderQuiDeclencheLaSonde : IUsageProvider
    {
        private readonly UsageSnapshot _snap;
        private readonly EtatServeurQuiSAllumeALaSonde _etat;
        public ProviderQuiDeclencheLaSonde(UsageSnapshot snap, EtatServeurQuiSAllumeALaSonde etat)
            => (_snap, _etat) = (snap, etat);
        public Task<UsageSnapshot> GetAsync(System.Threading.CancellationToken ct = default)
        {
            _etat.ASonde = true;                 // la sonde part ICI, comme en production
            return Task.FromResult(_snap);
        }
    }

    /// <summary>
    /// Le rapport ne doit PAS décrire un état antérieur à sa propre exécution.
    ///
    /// Défaut constaté en production le 2026-09-12 : la section « sonde » annonçait « pas encore sondé »
    /// et « aucun en-tête reconnu » alors que la section « Ce qui est affiché maintenant », trois lignes
    /// plus bas dans le MÊME rapport, montrait des chiffres exacts dont la source était cette sonde. La
    /// cause était l'ordre : la chaîne n'était interrogée qu'au moment de rendre la dernière section.
    ///
    /// FALSIFIABILITÉ : remettre l'appel <c>await _composite.GetAsync(ct)</c> dans la section
    /// « Ce qui est affiché maintenant » fait retomber ce test — la sonde ne serait allumée qu'APRÈS le
    /// rendu de la section qui la décrit.
    /// </summary>
    [Fact]
    public async Task Le_rapport_interroge_la_chaine_AVANT_de_decrire_la_sonde()
    {
        var paths = TempPaths();
        var now = DateTimeOffset.UtcNow;
        var etat = new EtatServeurQuiSAllumeALaSonde();
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState
            {
                Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
                Utilization = 0.21, Source = SourceUsage.SondeEnTetes,
                CapturedAt = now, Provenance = ProvenanceReleve.Frais,
            },
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        };
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         new SettingsService(paths),
                                         new ProviderQuiDeclencheLaSonde(snap, etat),
                                         new FakeClock(now), etatServeur: etat,
                                         machine: new FakeInventaireMachine());

        var report = await diag.BuildReportAsync();

        // La section « sonde » rend l'état d'APRÈS l'interrogation de la chaîne…
        Assert.Contains("Dernière sonde : 200 — en-têtes lus", report);
        Assert.Contains("anthropic-ratelimit-unified-5h-utilization", report);
        // …et ne peut donc plus se contredire elle-même.
        Assert.DoesNotContain("pas encore sondé", report);
        Assert.DoesNotContain("En-têtes « unified » reconnus : AUCUN", report);
        // Le chiffre affiché vient bien de cette sonde : les deux sections racontent la même histoire.
        Assert.Contains("source : sonde d'en-têtes de rate-limit", LigneCinqHeures(report));
    }

    /// <summary>Canal latéral portant un dépassement arbitraire, pour éprouver les trois cas d'affichage.</summary>
    private sealed class EtatServeurFige : IEtatServeur
    {
        public EtatDepassement? Depassement { get; init; }
        public ResultatSonde DernierResultat => ResultatSonde.SuccesEnTetesLus;
        public IReadOnlyList<string> NomsEnTetesRecus => new[] { "anthropic-ratelimit-unified-5h-utilization" };
        public event System.EventHandler<EtatDepassement?>? DepassementChange { add { } remove { } }
    }

    private static async Task<string> RapportAvecDepassement(EtatDepassement? dep)
    {
        var paths = TempPaths();
        var now = DateTimeOffset.UtcNow;
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState
            {
                Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
                Utilization = 0.23, Source = SourceUsage.SondeEnTetes,
                CapturedAt = now, Provenance = ProvenanceReleve.Frais,
            },
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        };
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         new SettingsService(paths), new StubProvider(snap),
                                         new FakeClock(now),
                                         etatServeur: new EtatServeurFige { Depassement = dep },
                                         machine: new FakeInventaireMachine());
        return await diag.BuildReportAsync();
    }

    private static string LigneDepassement(string report)
        => report.Split('\n').Single(l => l.TrimStart().StartsWith("Dépassement :"));

    /// <summary>
    /// Un STATUT SEUL déclare la politique du compte, PAS un dépassement en cours.
    ///
    /// Défaut constaté en production le 2026-09-12 sur un compte Max x20 à 23 % d'usage : le serveur
    /// envoie <c>anthropic-ratelimit-unified-overage-status</c> sans aucune quantité ni reset, et le
    /// rapport affichait « Dépassement :  · serveur : REJETÉ » — un séparateur orphelin ET un contresens
    /// alarmant, alors que les deux fenêtres disaient « autorisé » et que rien ne bloquait l'utilisateur.
    ///
    /// FALSIFIABILITÉ : rebrancher la branche sur <c>EstRenseigne</c> seul fait retomber ce test.
    /// </summary>
    [Fact]
    public async Task Un_statut_de_depassement_SEUL_est_une_politique_et_non_une_alerte()
    {
        var report = await RapportAvecDepassement(new EtatDepassement { Statut = StatutServeur.Rejete });
        var ligne = LigneDepassement(report);

        Assert.Contains("aucun dépassement en cours", ligne);
        Assert.Contains("politique du compte : dépassement non autorisé sur ce compte", ligne);
        Assert.DoesNotContain("REJETÉ", ligne);          // le mot alarmant a disparu…
        Assert.DoesNotContain(" ·  ", ligne);            // …et le séparateur n'est plus orphelin
    }

    /// <summary>Un dépassement RÉEL (avec quantité) reste décrit comme avant — la correction ne l'efface pas.</summary>
    [Fact]
    public async Task Un_depassement_REEL_reste_decrit_avec_sa_quantite_et_son_statut()
    {
        var report = await RapportAvecDepassement(new EtatDepassement
        {
            Utilization = 0.34, Statut = StatutServeur.AutoriseAvertissement,
        });
        var ligne = LigneDepassement(report);

        Assert.Contains("34 %", ligne);
        Assert.Contains("serveur : AUTORISÉ (avertissement)", ligne);
        Assert.DoesNotContain("aucun dépassement", ligne);
    }

    /// <summary>Aucune information rapportée : le rapport le dit, et n'invente aucune politique.</summary>
    [Fact]
    public async Task Aucun_depassement_rapporte_ne_fabrique_aucune_politique()
    {
        var ligne = LigneDepassement(await RapportAvecDepassement(null));

        Assert.Contains("aucun dépassement rapporté", ligne);
        Assert.DoesNotContain("politique", ligne);
    }

    // --- Phase 22 (OBS-01/OBS-02) : le rapport décrit LE moniteur du widget, et nomme ce qui est masqué ---
    //
    // SÉCURITÉ, commune à ces tests : FakeClaudeTokenReader à Token = null (la sonde réseau est gardée par
    // `if (token is not null)`, aucune requête ne part), FakeInventaireMachine (aucun balayage de coffre),
    // et TOUS les chemins de moniteur/magasins sont sous %TEMP%. Aucun n'écrit dans %APPDATA%\Chronos.

    private static readonly DateTimeOffset T22 = new(2026, 9, 12, 13, 28, 0, TimeSpan.Zero);

    // TreatedStore purge ses entrées au-delà d'un TTL de 6 h mesuré sur l'HORLOGE RÉELLE (aucune horloge
    // injectable). Écrire l'instant FIGÉ T22 rendrait ces tests verts le jour de leur écriture puis rouges
    // six heures plus tard, sans qu'aucun code de production n'ait bougé — le genre de garde qu'on apprend
    // à ignorer. Ce qui est écrit dans le magasin porte donc l'heure du système ; T22 reste l'instant des
    // snapshots, où l'arithmétique du relevé réel doit rester littérale. La valeur écrite n'est jamais
    // assertée : le filtre ne regarde que la présence de la clé.
    private static long EcritMaintenant22() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private sealed class SourceFixe22 : ISessionSource
    {
        private readonly IReadOnlyList<SessionSnapshot> _snaps;
        public SourceFixe22(params SessionSnapshot[] snaps) => _snaps = snaps;
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => _snaps;
    }

    private static string TempDir22()
    {
        var d = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chronos-diag22-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(d);
        Assert.StartsWith(System.IO.Path.GetTempPath(), d);
        return d;
    }

    private static string TempFichier22() => System.IO.Path.Combine(TempDir22(), "magasin.json");

    private static async Task<string> Rapport(SessionMonitor? moniteur)
    {
        var paths = TempPaths();
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                         new FakeClock(T22), machine: new FakeInventaireMachine(),
                                         moniteurSessions: moniteur);
        return await diag.BuildReportAsync();
    }

    /// <summary>Doctrine du milestone appliquée au rapport lui-même : sans moniteur, on ne raconte pas une
    /// lecture. L'ancien code fabriquait ici un moniteur nu et présentait sa sortie comme « ce que le widget
    /// affiche » — un mensonge poli, et probablement la raison pour laquelle le défaut n'a jamais été élucidé.</summary>
    [Fact]
    public async Task Sans_moniteur_le_rapport_dit_qu_il_n_a_rien_observe()
    {
        var report = await Rapport(null);

        Assert.Contains("MONITEUR NON INJECTÉ", report);
        Assert.DoesNotContain("Sessions AFFICHÉES par le widget", report);
    }

    [Fact]
    public async Task Le_rapport_decrit_les_sessions_du_moniteur_qu_on_lui_donne()
    {
        var moniteur = new SessionMonitor(TempDir22(),
            new SourceFixe22(new SessionSnapshot("s-att-0001", "overlay", SessionActivity.WaitingAttention,
                                                 "permission_prompt", T22.AddMinutes(-5))),
            new ArchiveStore(TempFichier22()));

        var report = await Rapport(moniteur);

        Assert.Contains("Sessions AFFICHÉES par le widget : 1", report);
        Assert.Contains("overlay — à toi (il y a 5 min)", report);   // libellés IDENTIQUES à ceux du widget
    }

    /// <summary>Critère n°2 de la phase — ce qui est masqué est dit, ET on dit par quoi. Reconstitution du
    /// relevé du 2026-09-12T13:28Z : e465420e (PROJET ADVANCED SHEET), session VIVANTE en attente de
    /// permission, que treated.json cachait pour 6 h. Le widget ne la montrait nulle part ; le rapport non
    /// plus. Une seule ligne doit désormais suffire à la retrouver.</summary>
    [Fact]
    public async Task Le_cas_e465420e_se_lit_en_une_ligne_du_rapport()
    {
        const string Id = "e465420e-83e0-428f-97f1-f0174c0848fc";
        var treated = new TreatedStore(TempFichier22());
        treated.Set(Id, EcritMaintenant22());

        var moniteur = new SessionMonitor(TempDir22(),
            new SourceFixe22(new SessionSnapshot(Id, "PROJET ADVANCED SHEET",
                                                 SessionActivity.WaitingAttention, "permission_prompt",
                                                 T22.AddMinutes(-10))),
            new ArchiveStore(TempFichier22()), treated);

        var report = await Rapport(moniteur);

        Assert.Contains("Sessions AFFICHÉES par le widget : 0", report);
        var ligne = report.Split('\n').Single(l => l.Contains("e465420e"));
        Assert.Contains("PROJET ADVANCED SHEET", ligne);
        Assert.Contains("à toi", ligne);
        Assert.Contains("masquée par treated.json", ligne);
    }

    [Fact]
    public async Task Une_session_archivee_est_annoncee_masquee_par_le_magasin_d_archives()
    {
        var archive = new ArchiveStore(TempFichier22());
        archive.Add("arch-0001-xxxx");
        var moniteur = new SessionMonitor(TempDir22(),
            new SourceFixe22(new SessionSnapshot("arch-0001-xxxx", "vieux-projet", SessionActivity.WaitingTurn,
                                                 null, T22)),
            archive);

        var report = await Rapport(moniteur);

        Assert.Contains("Sessions MASQUÉES par un filtre : 1", report);
        Assert.Contains("masquée par archived.json", report);
    }

    /// <summary>OBS-02 — la troncature à huit disparaît : comparer ligne à ligne un rapport tronqué avec un
    /// écran complet reconstruirait l'écart que cette phase ferme. Neuf sessions, neuf lignes, attente en tête.</summary>
    [Fact]
    public async Task Le_rapport_ne_tronque_plus_la_liste_des_sessions_affichees()
    {
        var snaps = Enumerable.Range(0, 9)
            .Select(i => new SessionSnapshot($"sess-{i:D4}-xx", $"projet-{i}",
                        i == 8 ? SessionActivity.WaitingAttention : SessionActivity.Working,
                        null, T22.AddMinutes(-i)))
            .ToArray();

        var report = await Rapport(new SessionMonitor(TempDir22(), new SourceFixe22(snaps),
                                                      new ArchiveStore(TempFichier22())));

        Assert.Contains("Sessions AFFICHÉES par le widget : 9", report);
        for (var i = 0; i < 9; i++) Assert.Contains($"projet-{i}", report);

        var lignes = report.Split('\n').Where(l => l.Contains("projet-")).ToList();
        Assert.Equal(9, lignes.Count);
        Assert.Contains("projet-8", lignes[0]);   // l'attente passe devant, comme dans le widget
    }

    // --- Phase 22 (OBS-02) : des fichiers d'état PERTINENTS, pas les huit premiers de l'alphabet ---

    private static void EcrireEtat(string dir, string id, string projet, SessionActivity a, DateTimeOffset maj)
        => System.IO.File.WriteAllText(System.IO.Path.Combine(dir, id + ".json"),
               SessionHookProcessor.BuildStateJson(id, projet, a, null, maj.ToUnixTimeMilliseconds()));

    private static SessionMonitor MoniteurSur(string dir)
        => new(dir, new SourceFixe22(), new ArchiveStore(TempFichier22()));

    /// <summary>Les lignes à puce du SEUL bloc « Fichiers d'état ». Le rapport en compte d'autres bien
    /// avant (les chemins de l'app bureau, par exemple) : prendre « les premières puces du rapport »
    /// mesurerait une autre section et rendrait ces tests verts ou rouges pour de mauvaises raisons.</summary>
    private static List<string> LignesFichiersEtat(string report)
        => report.Split('\n')
                 .SkipWhile(l => !l.Contains("Fichiers d'état ("))
                 .Skip(1)
                 .TakeWhile(l => l.TrimStart().StartsWith("· "))
                 .ToList();

    /// <summary>OBS-02 — le défaut mesuré : sur 54 fichiers dont 48 vieux de plus de sept jours, le rapport
    /// montrait les huit premiers par ordre alphabétique d'UUID. Ici les identifiants sont choisis pour que
    /// l'ordre alphabétique et l'ordre de pertinence soient OPPOSÉS : « aaa… » est le plus vieux et le plus
    /// muet, « zzz… » est la session qui attend. Un tri alphabétique la reléguerait hors de la liste.</summary>
    [Fact]
    public async Task Les_fichiers_listes_sont_ceux_qui_attendent_et_les_plus_recents()
    {
        var dir = TempDir22();
        for (var i = 0; i < 9; i++)
            EcrireEtat(dir, $"aaa-{i:D2}", $"vieux-{i}", SessionActivity.Unknown, T22.AddDays(-30 - i));
        EcrireEtat(dir, "zzz-attente", "PROJET QUI ATTEND", SessionActivity.WaitingAttention, T22.AddHours(-40));

        var report = await Rapport(MoniteurSur(dir));

        Assert.Contains("Fichiers d'état", report);
        Assert.Contains($"({dir}) : 10", report);   // le compte TOTAL reste celui du dossier
        var lignes = LignesFichiersEtat(report);
        Assert.Equal(8, lignes.Count);                         // la borne de lisibilité, annoncée juste après
        Assert.Contains("PROJET QUI ATTEND", lignes[0]);        // l'attente passe devant, malgré son âge
        Assert.Contains("… et 2 autre(s) non listé(s)", report);
    }

    [Fact]
    public async Task Huit_fichiers_ou_moins_ne_produisent_aucune_ligne_de_reste()
    {
        var dir = TempDir22();
        for (var i = 0; i < 8; i++)
            EcrireEtat(dir, $"s-{i:D2}", $"p-{i}", SessionActivity.Working, T22.AddMinutes(-i));

        var report = await Rapport(MoniteurSur(dir));

        Assert.DoesNotContain("non listé(s)", report);
        // Assertions portées sur le BLOC, pas sur le rapport entier : ces mêmes projets reparaissent plus
        // bas dans la liste des sessions du widget, et un test qui s'en contenterait serait vert même si
        // le bloc des fichiers d'état était vide.
        var lignes = LignesFichiersEtat(report);
        Assert.Equal(8, lignes.Count);
        for (var i = 0; i < 8; i++) Assert.Contains(lignes, l => l.Contains($"p-{i}"));
    }

    /// <summary>Doctrine du milestone : une date absente reste absente. Lui attribuer l'instant courant
    /// ferait passer un fichier muet pour un fichier frais, et le placerait en tête de la liste.</summary>
    [Fact]
    public async Task Un_fichier_sans_horodatage_est_annonce_de_date_inconnue_et_relegue()
    {
        var dir = TempDir22();
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "muet.json"),
            """{"session_id":"muet","project":"SANS DATE","activity":"WaitingAttention"}""");
        EcrireEtat(dir, "date", "AVEC DATE", SessionActivity.Working, T22.AddMinutes(-3));

        var report = await Rapport(MoniteurSur(dir));

        var lignes = LignesFichiersEtat(report);
        Assert.Contains("AVEC DATE", lignes[0]);                          // le daté passe devant
        Assert.Contains("SANS DATE", lignes[1]);
        Assert.Contains("date inconnue", lignes[1]);
    }

    [Fact]
    public async Task Un_fichier_corrompu_est_ignore_sans_casser_le_rapport()
    {
        var dir = TempDir22();
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "corrompu.json"), "pas du JSON {{{");
        EcrireEtat(dir, "bon", "PROJET SAIN", SessionActivity.Working, T22);

        var report = await Rapport(MoniteurSur(dir));

        Assert.Contains($"({dir}) : 2", report);    // 2 fichiers sur disque…
        Assert.Contains("PROJET SAIN", report);     // …1 seul lisible, et le rapport tient debout
        Assert.Contains("[Conseil]", report);
    }

    /// <summary>Le rapport inspecte le dossier que le MONITEUR lit, pas un dossier déduit d'un autre
    /// chemin : deux dossiers pour un seul widget rouvriraient l'écart qu'OBS-01 vient de fermer.</summary>
    [Fact]
    public async Task Le_dossier_inspecte_est_celui_du_moniteur_du_widget()
    {
        var dir = TempDir22();
        EcrireEtat(dir, "s1", "PROJET DU MONITEUR", SessionActivity.Working, T22);

        var report = await Rapport(MoniteurSur(dir));

        Assert.Contains($"Fichiers d'état ({dir}) : 1", report);
        Assert.Contains(LignesFichiersEtat(report), l => l.Contains("PROJET DU MONITEUR"));
    }
}
