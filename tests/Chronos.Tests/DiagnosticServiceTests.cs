using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Le diagnostic explique l'état réel (token, sources, plafonds, résultat affiché) — et n'expose
/// JAMAIS le token en clair dans le rapport (sécurité).
/// </summary>
public class DiagnosticServiceTests : IDisposable
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
        Assert.DoesNotContain("Désaccords entre sources", report);
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
        Assert.Contains("overlay — En attente (il y a 5 min)", report);   // libellés IDENTIQUES à ceux du widget
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
        Assert.Contains("En attente", ligne);
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

    /// <summary>LIB-01, versant RAPPORT. L'état indéterminé n'a plus de ligne dans le widget ; le rapport,
    /// lui, doit continuer de la montrer — sans quoi elle redeviendrait une session « absente », la faute que
    /// la phase 22 a fermée. Elle figure donc parmi les MASQUÉES, avec un motif qui dit pourquoi, et le
    /// compte des AFFICHÉES reste celui de l'écran (zéro).</summary>
    [Fact]
    public async Task Une_session_indeterminee_est_annoncee_masquee_avec_son_motif()
    {
        var moniteur = new SessionMonitor(TempDir22(),
            new SourceFixe22(new SessionSnapshot("inde-0001-xx", "projet-illisible", SessionActivity.Unknown,
                                                 null, T22)),
            new ArchiveStore(TempFichier22()));

        var report = await Rapport(moniteur);

        Assert.Contains("Sessions AFFICHÉES par le widget : 0", report);
        Assert.Contains("Sessions MASQUÉES par un filtre : 1", report);
        // L'identifiant est abrégé à huit caractères par le rapport : la ligne se repère par son projet.
        var ligne = report.Split('\n').Single(l => l.Contains("projet-illisible"));
        Assert.Contains("inde-000", ligne);
        Assert.Contains("masquée par état indéterminé", ligne);
    }

    // --- Phase 24 (FUS-02) : les désaccords entre sources deviennent lisibles ---

    /// <summary>Le relevé du 2026-09-12, rejoué de bout en bout : un fichier de hook figé depuis 7 h contre
    /// un transcript de 10 s. La phase 24 fait gagner le transcript ; cette ligne-ci dit à l'utilisateur
    /// POURQUOI — et surtout que sa source de hooks ne bouge plus.</summary>
    [Fact]
    public async Task Un_desaccord_nomme_la_source_retenue_la_source_ecartee_et_l_ecart_d_age()
    {
        const string Id = "e465420e-83e0-428f-97f1-f0174c0848fc";
        var dir = TempDir22();
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, Id + ".json"),
            SessionHookProcessor.BuildStateJson(Id, "PROJET ADVANCED SHEET", SessionActivity.WaitingAttention,
                "permission_prompt", T22.AddHours(-7).ToUnixTimeMilliseconds()));

        var moniteur = new SessionMonitor(dir,
            new SourceFixe22(new SessionSnapshot(Id, "PROJET ADVANCED SHEET", SessionActivity.Working,
                                                 null, T22.AddSeconds(-10))),
            new ArchiveStore(TempFichier22()));

        var report = await Rapport(moniteur);

        Assert.Contains("Désaccords entre sources : 1", report);

        // La ligne à puce du désaccord, isolée par ses deux marqueurs : l'identifiant ET le mot « retenu ».
        var ligne = report.Split('\n').Single(l => l.Contains("e465420e") && l.Contains("retenu "));
        Assert.Contains("transcript (~/.claude/projects)", ligne);
        Assert.Contains("« Réflexion »", ligne);
        Assert.Contains(@"fichier de hook (%APPDATA%\Chronos\sessions)", ligne);
        Assert.Contains("« En attente »", ligne);
        Assert.Contains("plus ancien de 6 h", ligne);   // 7 h moins 10 s → 6 h 59 min 50 s, tronqué à l'heure

        // Un désaccord n'est PAS un masquage : la session est bel et bien à l'écran.
        Assert.Contains("Sessions AFFICHÉES par le widget : 1", report);
        Assert.Contains("Sessions MASQUÉES par un filtre : 0", report);
    }

    /// <summary>Le silence se DIT. Un rapport muet sur les désaccords laisserait croire qu'il n'a pas regardé —
    /// c'est la même faute, une octave plus bas, que celle que la phase 22 a corrigée sur les masquages.</summary>
    [Fact]
    public async Task Sans_contradiction_le_rapport_annonce_zero_desaccord_et_le_dit()
    {
        var moniteur = new SessionMonitor(TempDir22(),
            new SourceFixe22(new SessionSnapshot("solo-0001", "Projet", SessionActivity.Working, null, T22)),
            new ArchiveStore(TempFichier22()));

        var report = await Rapport(moniteur);

        Assert.Contains("Désaccords entre sources : 0", report);
        Assert.Contains("aucune source n'en contredit une autre", report);
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

    // --- Phase 29 (APP-06) : le rapport compte les fichiers d'état RACINE PAR RACINE ---
    //
    // Le moniteur lit désormais plusieurs racines (vue du paquet de l'app bureau, vue réelle d'AppData). Le rapport
    // écrit une ligne « Fichiers d'état (<racine>) » par racine, TOUTES avant les lignes « · », qui fusionnent les
    // fichiers de toutes les racines.

    private static SessionMonitor MoniteurSurRacines(params string[] racines)
        => new(null, new SourceFixe22(), new ArchiveStore(TempFichier22()), dossiersEtat: racines);

    /// <summary>Les lignes à puce qui suivent la DERNIÈRE ligne « Fichiers d'état ( » — le bloc fusionné. Pas
    /// <see cref="LignesFichiersEtat"/>, qui lit après la PREMIÈRE et tomberait sur la ligne de la racine suivante.</summary>
    private static List<string> LignesApresLaDerniereRacine(string report)
    {
        var lignes = report.Split('\n');
        var derniere = Array.FindLastIndex(lignes, l => l.Contains("Fichiers d'état ("));
        Assert.True(derniere >= 0, "aucune ligne « Fichiers d'état ( » dans le rapport");
        return lignes.Skip(derniere + 1).TakeWhile(l => l.TrimStart().StartsWith("· ")).ToList();
    }

    [Fact]
    public async Task Le_rapport_compte_les_fichiers_d_etat_racine_par_racine()
    {
        var paquet = TempDir22();
        var reel = TempDir22();
        EcrireEtat(paquet, "p1", "PAQUET-UN", SessionActivity.WaitingAttention, T22.AddMinutes(-2));
        EcrireEtat(paquet, "p2", "PAQUET-DEUX", SessionActivity.Working, T22.AddMinutes(-1));
        EcrireEtat(reel, "r1", "REEL-UN", SessionActivity.WaitingTurn, T22.AddMinutes(-3));

        var report = await Rapport(MoniteurSurRacines(paquet, reel));

        var lignePaquet = $"Fichiers d'état ({paquet}) : 2";
        var ligneReel = $"Fichiers d'état ({reel}) : 1";
        Assert.Contains(lignePaquet, report);
        Assert.Contains(ligneReel, report);
        Assert.True(report.IndexOf(lignePaquet, StringComparison.Ordinal) < report.IndexOf(ligneReel, StringComparison.Ordinal),
            "la racine du paquet est écrite avant la vue réelle, dans l'ordre du moniteur");

        var puces = LignesApresLaDerniereRacine(report);
        Assert.Equal(3, puces.Count);
        Assert.Contains(puces, l => l.Contains("PAQUET-UN"));
        Assert.Contains(puces, l => l.Contains("PAQUET-DEUX"));
        Assert.Contains(puces, l => l.Contains("REEL-UN"));
    }

    /// <summary>Une racine qui n'existe pas est un FAIT à écrire : sur une machine où la vue du paquet manque, taire
    /// la ligne laisserait croire qu'on ne l'a pas cherchée.</summary>
    [Fact]
    public async Task Une_racine_d_etat_absente_est_ecrite_absente_jamais_tue()
    {
        var absente = System.IO.Path.Combine(TempDir22(), "jamais-creee");
        Assert.False(System.IO.Directory.Exists(absente));
        var reel = TempDir22();
        EcrireEtat(reel, "r1", "REEL-SEUL", SessionActivity.Working, T22);

        var report = await Rapport(MoniteurSurRacines(absente, reel));

        Assert.Contains($"Fichiers d'état ({absente}) : absent (dossier introuvable)", report);
        Assert.Contains($"Fichiers d'état ({reel}) : 1", report);
        Assert.Contains(LignesApresLaDerniereRacine(report), l => l.Contains("REEL-SEUL"));
    }

    /// <summary>APP-03 (phase 29) — une troisième source entre dans l'arbitrage. Sans libellé, le repli
    /// « une source non nommée » masquerait l'oubli : un désaccord écrit avec une source anonyme ne se diagnostique
    /// pas seul. Chaque valeur de <see cref="SourceSession"/> a donc un libellé NOMMÉ, et aucun n'en double un autre.</summary>
    [Fact]
    public void Chaque_source_de_session_a_un_libelle_nomme()
    {
        var sources = System.Enum.GetValues<SourceSession>();
        Assert.Equal(3, sources.Length);   // garde anti-muette : hook, app bureau, transcript

        var libelles = sources.Select(DiagnosticService.LibelleSourceSession).ToList();
        Assert.All(libelles, l =>
        {
            Assert.False(string.IsNullOrWhiteSpace(l));
            Assert.NotEqual("une source non nommée", l);
        });
        Assert.Equal(libelles.Count, libelles.Distinct(System.StringComparer.Ordinal).Count());
    }

    // --- Phase 29 (APP-04) : ce que l'app bureau sait de chaque session, lu dans la MÊME lecture que le widget ---
    //
    // Les fixtures RÉELLES de la phase 27 datent des 24 et 25/09 : l'instant T22 (12/09) les mettrait hors de la fenêtre
    // de lecture de 24 h, et le lecteur ne les ouvrirait pas. Ces tests prennent donc l'instant M29 des tests du lecteur,
    // et le rapport est construit sur une horloge figée à cet instant (RapportA). Tout reste sous %TEMP% : racines de
    // l'app (supprimées au Dispose), magasins, dossier d'état. Aucun ne lit le vrai %APPDATA% ni %LOCALAPPDATA%.

    private static readonly DateTimeOffset M29 = new(2026, 9, 25, 18, 40, 0, TimeSpan.Zero);
    private const string IdA29 = "11456cab-d447-42c7-aa85-9920ce64f7ba";   // geste B — « Session A », sans résumé de fin de tour
    private const string IdE29 = "adac2711-86a4-4b4d-b71c-9a594c9d959e";   // blocked — « Session E »
    private const string IdB29 = "c17a1b03-3c8d-4c05-a747-dd77bc702e1b";   // review_ready — « Session B »
    private const string IdG29 = "939eb30a-0000-4000-8000-000000000000";   // aucune métadonnée de l'app

    private readonly List<string> _racinesAppBureau29 = new();

    public void Dispose()
    {
        foreach (var r in _racinesAppBureau29)
        {
            try { System.IO.Directory.Delete(r, recursive: true); }
            catch { /* nettoyage best-effort */ }
        }
    }

    /// <summary>Une racine de l'app, SUIVIE pour être supprimée au Dispose.</summary>
    private string Suivre(string racine)
    {
        _racinesAppBureau29.Add(racine);
        return racine;
    }

    /// <summary>Même construction que <see cref="Rapport"/>, à l'instant donné.</summary>
    private static async Task<string> RapportA(SessionMonitor moniteur, DateTimeOffset maintenant)
    {
        var paths = TempPaths();
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
                                         new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                         new FakeClock(maintenant), machine: new FakeInventaireMachine(),
                                         moniteurSessions: moniteur);
        return await diag.BuildReportAsync();
    }

    /// <summary>Scénario S : quatre sessions affichées par les transcripts, dont trois ont des métadonnées de l'app.</summary>
    private static SourceFixe22 TranscriptsS29() => new(
        new SessionSnapshot(IdA29, "Projet-A", SessionActivity.WaitingTurn, null, new DateTimeOffset(2026, 9, 25, 18, 34, 4, 328, TimeSpan.Zero)),
        new SessionSnapshot(IdE29, "Projet-E", SessionActivity.WaitingTurn, null, M29.AddHours(-1)),
        new SessionSnapshot(IdB29, "Projet-B", SessionActivity.WaitingTurn, null, new DateTimeOffset(2026, 9, 25, 18, 34, 5, TimeSpan.Zero)),
        new SessionSnapshot(IdG29, "Projet-G", SessionActivity.Working, null, M29.AddMinutes(-1)));

    private static SessionMonitor MoniteurAppBureau29(string racine, ArchiveStore? archive = null)
        => new(TempDir22(), TranscriptsS29(), archive ?? new ArchiveStore(TempFichier22()),
               appBureau: new LecteurAppBureau(new[] { racine }));

    private string RacineS29()
        => Suivre(RacineAppBureau.Creer(M29, "session-courante-geste-b.json", "fin-de-tour-blocked.json",
                                        "fin-de-tour-review-ready.json", "sans-cliSessionId.json"));

    /// <summary>Les lignes par session de la section « Source app-bureau ». Elles se repèrent APRÈS « Jointures : » :
    /// les lignes AFFICHÉES commencent aussi par « · », et les prendre ferait mesurer une autre section.</summary>
    private static List<string> LignesAppBureau(string report)
        => report.Split('\n')
                 .SkipWhile(l => !l.Contains("Jointures : "))
                 .Skip(1)
                 .TakeWhile(l => l.TrimStart().StartsWith("· "))
                 .ToList();

    /// <summary>APP-04 — une source TROUVÉE dit où elle lit, combien de fichiers elle a vus et retenus, ce qui leur
    /// manque, et combien de lignes de l'écran elle qualifie. « 0 relu » n'est pas une panne (cache) : c'est pourquoi
    /// « avec métadonnées » et « relus » sont deux nombres.</summary>
    [Fact]
    public async Task La_source_app_bureau_trouvee_dit_sa_racine_ses_compteurs_et_ses_jointures()
    {
        var racine = RacineS29();

        var report = await RapportA(MoniteurAppBureau29(racine), M29);

        Assert.Contains($"Source app-bureau : trouvée — {racine}", report);
        Assert.Contains("4 énumérés", report);
        Assert.Contains("3 avec métadonnées", report);
        Assert.Contains("1 sans cliSessionId", report);
        Assert.Contains("Champs absents (fichiers lus) : aucun", report);
        Assert.Contains("Jointures : 3 session(s) affichée(s) sur 4 ont des métadonnées", report);
    }

    /// <summary>APP-04 — pour chaque ligne de l'ÉCRAN, ce que l'app en sait : le titre lu, le dernier focus, la
    /// classification de fin de tour ; ou, sans métadonnées, que la ligne se comporte comme en v1.6. Les anciennetés
    /// sont celles des fixtures réelles à M29 (focus 18:30:44.976Z, 2026-09-24T08:54:06.504Z).</summary>
    [Fact]
    public async Task Chaque_session_affichee_dit_son_titre_son_dernier_focus_et_sa_classification()
    {
        var report = await RapportA(MoniteurAppBureau29(RacineS29()), M29);

        var lignes = LignesAppBureau(report);
        Assert.Equal(4, lignes.Count);   // une ligne par session AFFICHÉE, ni plus ni moins

        var a = Assert.Single(lignes, l => l.Contains("11456cab"));
        Assert.Contains("« Session A »", a);
        Assert.Contains("focus il y a 9 min", a);
        Assert.Contains("fin de tour : aucune classification", a);

        var e = Assert.Single(lignes, l => l.Contains("adac2711"));
        Assert.Contains("« Session E »", e);
        Assert.Contains("focus il y a 33 h", e);
        Assert.Contains("fin de tour : question posée (« (anonymisé) réponse attendue »)", e);

        Assert.Contains("fin de tour : prête à revue", Assert.Single(lignes, l => l.Contains("c17a1b03")));
        Assert.Contains("aucune métadonnée (comportement v1.6)", Assert.Single(lignes, l => l.Contains("939eb30a")));
    }

    /// <summary>Une catégorie que le disque n'a jamais montrée (le code de l'app en connaît d'autres : need_input…)
    /// est écrite TELLE QUELLE et dite non interprétée : le rapport ne traduit pas ce que le lecteur refuse de deviner.</summary>
    [Fact]
    public async Task Une_categorie_inconnue_est_dite_brute_jamais_traduite()
    {
        var racine = Suivre(RacineAppBureau.NouvelleRacine());
        RacineAppBureau.Ecrire(racine, RacineAppBureau.NomFichier("fin-de-tour-blocked.json"),
            RacineAppBureau.Deriver("fin-de-tour-blocked.json",
                ("\"status_category\": \"blocked\"", "\"status_category\": \"need_input\"")),
            M29.AddMinutes(-1));

        var report = await RapportA(MoniteurAppBureau29(racine), M29);

        var e = Assert.Single(LignesAppBureau(report), l => l.Contains("adac2711"));
        Assert.Contains("fin de tour : inconnue (« need_input ») — non interprétée", e);
        Assert.DoesNotContain("question posée", e);
    }

    /// <summary>Une source ABSENTE est un fait à écrire, avec les racines cherchées : sans elles, « absente » ne dirait
    /// pas où l'on a regardé — et c'est précisément ce qui a caché la vue du paquet pendant deux semaines.</summary>
    [Fact]
    public async Task La_source_app_bureau_absente_est_annoncee_avec_les_racines_cherchees()
    {
        var c1 = System.IO.Path.Combine(TempDir22(), "jamais-creee-1");
        var c2 = System.IO.Path.Combine(TempDir22(), "jamais-creee-2");
        Assert.False(System.IO.Directory.Exists(c1));
        Assert.False(System.IO.Directory.Exists(c2));
        var moniteur = new SessionMonitor(TempDir22(), TranscriptsS29(), new ArchiveStore(TempFichier22()),
                                          appBureau: new LecteurAppBureau(new[] { c1, c2 }));

        var report = await RapportA(moniteur, M29);

        Assert.Contains($"Source app-bureau : absente (dossier introuvable) — cherché : {c1} ; {c2}", report);
        Assert.DoesNotContain("Source app-bureau : trouvée", report);
        Assert.DoesNotContain("NON BRANCHÉE", report);
    }

    /// <summary>Un moniteur SANS lecteur n'est pas une source absente : personne n'a cherché. Le rapport le dit
    /// autrement, pour que « non branchée » et « cherchée, introuvable » ne se confondent jamais.</summary>
    [Fact]
    public async Task Un_moniteur_sans_lecteur_est_annonce_non_branche()
    {
        var moniteur = new SessionMonitor(TempDir22(), TranscriptsS29(), new ArchiveStore(TempFichier22()));

        var report = await RapportA(moniteur, M29);

        Assert.Contains("Source app-bureau : NON BRANCHÉE", report);
        Assert.DoesNotContain("Source app-bureau : absente", report);
        Assert.DoesNotContain("Jointures : ", report);
    }

    /// <summary>Un champ lu qui manque est NOMMÉ et compté : le format de l'app n'est pas documenté, et un champ qui
    /// disparaît après une mise à jour de l'app doit se voir dans le rapport avant de se voir à l'écran.</summary>
    [Fact]
    public async Task Les_champs_absents_sont_nommes_et_comptes()
    {
        var racine = Suivre(RacineAppBureau.NouvelleRacine());
        RacineAppBureau.Ecrire(racine, RacineAppBureau.NomFichier("session-courante-geste-b.json"),
            RacineAppBureau.Deriver("session-courante-geste-b.json", ("\"latestUserFrameAt\": 1790361283093,", "")),
            M29.AddMinutes(-1));
        RacineAppBureau.Ecrire(racine, RacineAppBureau.NomFichier("fin-de-tour-review-ready.json"),
            RacineAppBureau.Deriver("fin-de-tour-review-ready.json", ("\"latestUserFrameAt\": 1790361160707,", "")),
            M29.AddMinutes(-1));
        RacineAppBureau.EcrireOctets(racine, RacineAppBureau.NomFichier("fin-de-tour-blocked.json"),
            RacineAppBureau.Fixture("fin-de-tour-blocked.json"), M29.AddMinutes(-1));

        var report = await RapportA(MoniteurAppBureau29(racine), M29);

        var ligne = report.Split('\n').Single(l => l.Contains("Champs absents (fichiers lus) : ")).Trim();
        Assert.Equal("Champs absents (fichiers lus) : latestUserFrameAt 2", ligne);
    }

    /// <summary>OBS-01 étendu au titre : les lignes AFFICHÉES et MASQUÉES du rapport portent le nom que le widget
    /// affiche (<see cref="AffichageSessions.Nom"/>), pas le dossier — sinon l'utilisateur ne retrouverait plus, dans le
    /// rapport, la ligne qu'il regarde à l'écran.</summary>
    [Fact]
    public async Task Les_sessions_affichees_du_rapport_portent_le_nom_du_widget()
    {
        var archive = new ArchiveStore(TempFichier22());
        archive.Add(IdB29);

        var report = await RapportA(MoniteurAppBureau29(RacineS29(), archive), M29);

        Assert.Contains("11456cab Session A — En attente", report);
        Assert.DoesNotContain("11456cab Projet-A", report);
        Assert.Contains(report.Split('\n'), l => l.Contains("c17a1b03 Session B — En attente") && l.Contains("masquée par archived.json"));
    }

    /// <summary>Les candidats d'un lecteur dont l'énumération lève : la seule façon, sans toucher au lecteur, d'obtenir
    /// une lecture qui échoue — le lecteur est écrit pour ne jamais lever sur un dossier.</summary>
    private sealed class CandidatsQuiLevent : IReadOnlyList<string>
    {
        public string this[int index] => throw new System.IO.IOException("candidats illisibles");
        public int Count => 1;
        public IEnumerator<string> GetEnumerator() => throw new System.IO.IOException("candidats illisibles");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Un lecteur BRANCHÉ dont la lecture a levé rend, au moniteur, la même lecture nulle qu'un moniteur sans
    /// lecteur (29-03, limite écrite). Le rapport ne dit donc pas « non branchée » sur la seule foi de cette valeur :
    /// il consulte le lecteur du moniteur, et dit la lecture impossible — l'écran, lui, se comporte comme en v1.6.</summary>
    [Fact]
    public async Task Un_lecteur_branche_dont_la_lecture_leve_n_est_pas_dit_non_branche()
    {
        var moniteur = new SessionMonitor(TempDir22(), TranscriptsS29(), new ArchiveStore(TempFichier22()),
                                          appBureau: new LecteurAppBureau(new CandidatsQuiLevent()));

        var report = await RapportA(moniteur, M29);

        Assert.DoesNotContain("NON BRANCHÉE", report);
        Assert.Contains("Source app-bureau : lecture impossible à ce cycle", report);
        Assert.Contains("Sessions AFFICHÉES par le widget : 4", report);   // l'écran, lui, est celui de la v1.6
    }

    // --- Phase 30 (LUE-03, LUE-04) : aucun masquage sans cause, et la règle « lue » dite telle qu'elle voit ---
    //
    // Les heures attendues sont calculées ICI par ToLocalTime() (Piège 10) : le rapport écrit l'heure locale de la
    // machine, et une heure écrite en dur ne serait vraie que dans un fuseau. Tout reste sous %TEMP% : racines de l'app
    // (supprimées au Dispose), magasins, dossier d'état. La sonde du premier plan est un faux : aucun test ne lit la
    // vraie fenêtre au premier plan de la machine.

    /// <summary>Un instant du 2026-09-25, en UTC (heure de Paris = UTC+2 ce jour-là).</summary>
    private static DateTimeOffset U30(int h, int m, int s = 0, int ms = 0) => new(2026, 9, 25, h, m, s, ms, TimeSpan.Zero);

    /// <summary>L'heure LOCALE à la seconde, telle que le rapport l'écrit (D-30-11).</summary>
    private static string Heure30(DateTimeOffset t) => t.ToLocalTime().ToString("HH:mm:ss");

    /// <summary>Le moniteur de production, aux racines près : le détecteur est construit sur le magasin, le lecteur de
    /// l'app et la sonde du premier plan sont passés par argument nommé, comme dans App.xaml.cs.</summary>
    private static SessionMonitor MoniteurLecture30(ISessionSource transcripts, TreatedStore treated, string racine, IPremierPlan sonde)
        => new(TempDir22(), transcripts, new ArchiveStore(TempFichier22()), treated, new SessionTreatmentTracker(treated),
               appBureau: new LecteurAppBureau(new[] { racine }), premierPlan: sonde);

    /// <summary>Le geste B du relevé (27-RELEVE) : la fixture réelle (focus 18:30:44.976Z), la fin du tour à
    /// 18:34:04.328Z, claude au premier plan depuis 18:33:27Z. Au rapport de 18:34:06.828Z, la grâce de 2,5 s est
    /// écoulée, borne incluse : la session est lue au premier plan (LUE-02).</summary>
    private (SessionMonitor Moniteur, FakePremierPlan Sonde) MontageGesteB30()
    {
        var racine = Suivre(RacineAppBureau.NouvelleRacine());
        RacineAppBureau.EcrireOctets(racine, RacineAppBureau.NomFichier("session-courante-geste-b.json"),
            RacineAppBureau.Fixture("session-courante-geste-b.json"), U30(18, 34));
        var sonde = FakePremierPlan.Claude(U30(18, 33, 27));
        var treated = new TreatedStore(TempFichier22(), new FakeClock(U30(18, 34)));
        var moniteur = MoniteurLecture30(
            new SourceFixe22(new SessionSnapshot(IdA29, "Projet-A", SessionActivity.WaitingTurn, null, U30(18, 34, 4, 328))),
            treated, racine, sonde);
        return (moniteur, sonde);
    }

    /// <summary>R01 — LUE-03, critère 4 : une session lue par le focus dit ses DEUX instants, à la seconde. Le relevé du
    /// 2026-09-25 : JARVIS finie à 15:59:05, ouverte dans l'app à 16:00:39 (heure de Paris), rapport à 16:08, explorer
    /// au premier plan (LUE-01 seule). Le fichier à ouvrir reste nommé : « masquée par treated.json ».</summary>
    [Fact]
    public async Task La_session_lue_par_focus_dit_ses_deux_instants()
    {
        var maintenant = U30(14, 8);
        var racine = Suivre(RacineAppBureau.NouvelleRacine());
        RacineAppBureau.Ecrire(racine, "local_jarvis.json",
            RacineAppBureau.Deriver("fin-de-tour-review-ready.json", ("1790361032950", "1790344839000")), U30(14, 1));   // focus 14:00:39Z
        var treated = new TreatedStore(TempFichier22(), new FakeClock(maintenant));
        var moniteur = MoniteurLecture30(
            new SourceFixe22(new SessionSnapshot(IdB29, "JARVIS", SessionActivity.WaitingTurn, null, U30(13, 59, 5))),
            treated, racine, FakePremierPlan.Autre("explorer"));

        var report = await RapportA(moniteur, maintenant);

        var ligne = Assert.Single(report.Split('\n'), l => l.Contains("c17a1b03") && l.Contains("masquée par"));
        Assert.Contains("masquée par treated.json", ligne);
        Assert.Contains($"lue : focus à {Heure30(U30(14, 0, 39))} > attente à {Heure30(U30(13, 59, 5))}", ligne);
        Assert.Contains("Premier plan : explorer — LUE-02 inactive", report);
    }

    /// <summary>R02 — LUE-03 : le geste B se lit au rapport. « depuis N s » est figé au constat (constat moins début du
    /// premier plan claude = 39,828 s, tronqué) ; la section « Règle « lue » » dit la session sélectionnée et l'heure de
    /// son focus, et le premier plan que la sonde a rendu à CE cycle.</summary>
    [Fact]
    public async Task La_session_lue_au_premier_plan_dit_depuis_combien_de_secondes()
    {
        var (moniteur, _) = MontageGesteB30();

        var report = await RapportA(moniteur, U30(18, 34, 6, 828));

        var ligne = Assert.Single(report.Split('\n'), l => l.Contains("11456cab") && l.Contains("masquée par"));
        Assert.Contains("sélectionnée au premier plan depuis 39 s (claude)", ligne);
        Assert.Contains($"attente à {Heure30(U30(18, 34, 4, 328))}", ligne);
        Assert.Contains("Premier plan : claude depuis 39 s — LUE-02 active", report);
        Assert.Contains($"Session sélectionnée dans l'app : 11456cab (focus {Heure30(U30(18, 30, 44, 976))})", report);
    }

    /// <summary>R03 — une réponse (NET-01) se dit « répondue », avec l'attente, et le fait qui l'a prouvée : un travail
    /// observé sur la MÊME source. Le fichier reste nommé en tête.</summary>
    [Fact]
    public void Le_libelle_de_repondue_dit_l_attente_et_le_travail_observe()
    {
        var t = new DateTimeOffset(2026, 9, 25, 13, 59, 5, TimeSpan.Zero);

        var libelle = DiagnosticService.LibelleMasquage(MotifMasquage.Repondue,
            new CauseTraitement(MotifMasquage.Repondue, t, Constat: t.AddMinutes(1)));

        Assert.StartsWith("treated.json — répondue", libelle);
        Assert.Contains($"attente à {Heure30(t)}", libelle);
        Assert.Contains("travail observé sur la même source", libelle);
    }

    /// <summary>R04 — LUE-03, le résidu honnête. Une inscription que le détecteur n'a pas constatée (un geste, ou une
    /// entrée antérieure au démarrage de l'overlay) n'a pas de cause : le rapport le dit comme tel, jamais « lue » ni
    /// « répondue ». Ici, aucun détecteur : le magasin est écrit à la main.</summary>
    [Fact]
    public async Task Un_masquage_sans_cause_connue_est_dit_marque_a_la_main_ou_avant_le_demarrage()
    {
        var treated = new TreatedStore(TempFichier22(), new FakeClock(T22));
        treated.Set("resi-0001-xx", T22.ToUnixTimeMilliseconds());
        var moniteur = new SessionMonitor(TempDir22(),
            new SourceFixe22(new SessionSnapshot("resi-0001-xx", "Residu", SessionActivity.WaitingTurn, null, T22)),
            new ArchiveStore(TempFichier22()), treated);

        var report = await Rapport(moniteur);

        var ligne = Assert.Single(report.Split('\n'), l => l.Contains("resi-000"));
        Assert.Contains("masquée par treated.json (« traité » — marquée à la main, ou traitée avant le démarrage de l'overlay ; réversible)", ligne);
        Assert.DoesNotContain("lue", ligne);
        Assert.DoesNotContain("répondue", ligne);
    }

    /// <summary>R05 — LUE-04 : la sonde du premier plan ne sait pas. Le rapport le dit, avec sa raison, et dit ce qui
    /// reste actif : LUE-01 seule.</summary>
    [Fact]
    public async Task Premier_plan_indisponible_le_rapport_dit_LUE_01_seule()
    {
        var moniteur = new SessionMonitor(TempDir22(), TranscriptsS29(), new ArchiveStore(TempFichier22()),
                                          appBureau: new LecteurAppBureau(new[] { RacineS29() }),
                                          premierPlan: FakePremierPlan.Indisponible("GetWindowThreadProcessId a échoué"));

        var report = await RapportA(moniteur, M29);

        Assert.Contains("Premier plan : indisponible (GetWindowThreadProcessId a échoué) — LUE-02 inactive, LUE-01 seule", report);
    }

    /// <summary>R06 — la sélection de l'app est le focus le plus récent de TOUS les fichiers, y compris d'une session
    /// neuve sans cliSessionId : le rapport dit alors qu'aucune session n'est sélectionnée, et pourquoi.</summary>
    [Fact]
    public async Task Le_dernier_focus_sans_cliSessionId_ne_selectionne_personne_au_rapport()
    {
        var racine = Suivre(RacineAppBureau.NouvelleRacine());
        RacineAppBureau.EcrireOctets(racine, RacineAppBureau.NomFichier("session-courante-geste-b.json"),
            RacineAppBureau.Fixture("session-courante-geste-b.json"), U30(18, 39));
        RacineAppBureau.Ecrire(racine, "local_neuve.json",
            RacineAppBureau.Deriver("sans-cliSessionId.json", ("1781190743865", "1790361100000")), U30(18, 39));   // focus 18:31:40Z

        var report = await RapportA(MoniteurAppBureau29(racine), M29);

        Assert.Contains($"Session sélectionnée dans l'app : aucune — le dernier focus ({Heure30(U30(18, 31, 40))}) est une session sans cliSessionId", report);
    }

    /// <summary>R07 — LUE-04 : sans source app-bureau ni sonde, la règle « lue » ne peut rien voir, et le rapport le dit
    /// au lieu de se taire : sélection inconnue (comportement v1.6), premier plan NON BRANCHÉ.</summary>
    [Fact]
    public async Task Sans_source_app_bureau_la_regle_lue_est_dite_inactive()
    {
        var moniteur = new SessionMonitor(TempDir22(),
            new SourceFixe22(new SessionSnapshot("solo-0001", "Projet", SessionActivity.WaitingTurn, null, T22)),
            new ArchiveStore(TempFichier22()));

        var report = await Rapport(moniteur);

        Assert.Contains("Session sélectionnée dans l'app : inconnue — source app-bureau absente : règle « lue » inactive (comportement v1.6)", report);
        Assert.Contains("Premier plan : NON BRANCHÉ — LUE-02 inactive, LUE-01 seule", report);
    }

    /// <summary>R08 — LUE-03, « aucun masquage sans cause » : chaque motif, avec ou sans cause, a un libellé NOMMÉ ; le
    /// repli « un filtre non nommé » masquerait l'oubli d'un motif ajouté. Les six libellés sans cause sont distincts.</summary>
    [Fact]
    public void Aucun_motif_de_masquage_n_est_sans_libelle()
    {
        var motifs = System.Enum.GetValues<MotifMasquage>();
        Assert.Equal(6, motifs.Length);   // garde anti-muette : archivée, traitée, indéterminée, lue par focus, lue au premier plan, répondue
        var t = new DateTimeOffset(2026, 9, 25, 13, 59, 5, TimeSpan.Zero);

        foreach (var m in motifs)
            foreach (var libelle in new[] { DiagnosticService.LibelleMasquage(m, null),
                                            DiagnosticService.LibelleMasquage(m, new CauseTraitement(m, t, t, t, t)) })
            {
                Assert.False(string.IsNullOrWhiteSpace(libelle), $"{m} : libellé blanc");
                Assert.NotEqual("un filtre non nommé", libelle);
            }

        var sansCause = motifs.Select(m => DiagnosticService.LibelleMasquage(m, null)).ToList();
        Assert.Equal(sansCause.Count, sansCause.Distinct(System.StringComparer.Ordinal).Count());
    }

    /// <summary>R09 — OBS-01, versant DYNAMIQUE : le rapport lit le premier plan dans la lecture du widget, jamais par un
    /// second appel à la sonde. Le seul appel est celui du SEUL Inspecter du rapport, et la ligne « Premier plan » est
    /// bien écrite (sans elle, un appel unique ne prouverait rien).</summary>
    [Fact]
    public async Task Le_rapport_ne_relit_pas_la_sonde()
    {
        var (moniteur, sonde) = MontageGesteB30();

        var report = await RapportA(moniteur, U30(18, 34, 6, 828));

        Assert.Single(sonde.Appels);
        Assert.Contains("Premier plan : claude depuis", report);
    }
}
