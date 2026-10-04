using System.IO;
using Chronos.Models;
using Chronos.Models.Historique.Tokens;
using Chronos.Services;
using Chronos.Services.Historique.Tokens;
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
    public async Task Rapport_sans_token_n_expose_jamais_un_jeton()
    {
        var paths = TempPaths();
        var settings = new SettingsService(paths);
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Estimated, Utilization = null },
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        };
        var diag = new DiagnosticService(paths, settings, new StubProvider(snap), new FakeClock(DateTimeOffset.UtcNow));

        var report = await diag.BuildReportAsync();

        Assert.Contains("Diagnostic", report);
        Assert.Contains("[Chaîne de données]", report);
        Assert.DoesNotContain("SECRET-TOKEN", report);              // JAMAIS la valeur d'un jeton (le rapport n'en lit plus aucun depuis 37-03)
        // 19-04/20-05 : le mot « estimé » n'existe plus, et le tilde non plus — l'incertitude d'un
        // plancher est UNILATÉRALE. Forme DÉGRADÉE (ni Source ni Provenance), qui doit rester lisible.
        Assert.Contains("PLANCHER", report);                        // résultat affiché décrit
        Assert.DoesNotContain("~", LigneCinqHeures(report));
        Assert.Contains("source : non renseignée", LigneCinqHeures(report));
        Assert.Contains("relevé de date inconnue", LigneCinqHeures(report));
    }

    // ------------------------------------------------------------------------------------------
    // FIAB-4 (42.2-01) : le journal de démarrage reporte les incidents des lancements précédents
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Le_journal_de_demarrage_reporte_les_incidents_du_lancement_precedent()
    {
        var paths = TempPaths();
        var dir = System.IO.Path.GetDirectoryName(paths.SettingsFile)!;
        Assert.StartsWith(System.IO.Path.GetTempPath(), dir);
        var log = System.IO.Path.Combine(dir, "chronos.log");
        const string incident = "[incident] 2026-10-01 08:00:00 +11:00 — arrêt dépassé : x — sortie forcée";
        System.IO.File.WriteAllText(log, "(log automatique au démarrage)\nbruit\n" + incident + "\n");
        var diag = new DiagnosticService(paths, new SettingsService(paths), new StubProvider(UsageSnapshot.Empty), new FakeClock(DateTimeOffset.UtcNow));

        await diag.LogStartupAsync();

        var contenu = System.IO.File.ReadAllText(log);
        Assert.StartsWith("(log automatique au démarrage)", contenu);
        Assert.Contains("[Incidents des lancements précédents]", contenu);
        Assert.Contains(incident, contenu);
        Assert.DoesNotContain("bruit", contenu);
        Assert.Contains("[Magasins persistants]", contenu);
    }

    [Fact]
    public async Task Le_journal_de_demarrage_sans_journal_prealable_n_a_pas_de_section_d_incidents()
    {
        var paths = TempPaths();
        var dir = System.IO.Path.GetDirectoryName(paths.SettingsFile)!;
        Assert.StartsWith(System.IO.Path.GetTempPath(), dir);
        var diag = new DiagnosticService(paths, new SettingsService(paths), new StubProvider(UsageSnapshot.Empty), new FakeClock(DateTimeOffset.UtcNow));

        await diag.LogStartupAsync();

        var contenu = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "chronos.log"));
        Assert.StartsWith("(log automatique au démarrage)", contenu);
        Assert.DoesNotContain("[Incidents des lancements précédents]", contenu);
        Assert.Contains("[Magasins persistants]", contenu);
    }

    [Fact]
    public async Task Le_rapport_ne_contient_plus_les_sections_mortes()
    {
        var paths = TempPaths();
        var diag = new DiagnosticService(paths,
            new SettingsService(paths), new StubProvider(UsageSnapshot.Empty), new FakeClock(DateTimeOffset.UtcNow));

        var report = await diag.BuildReportAsync();

        // Phase 37 (DAT-04) : ces sections décrivaient des sources retirées ou mortes.
        Assert.DoesNotContain("[Réglage]", report);
        Assert.DoesNotContain("Usage exact (OAuth)", report);
        Assert.DoesNotContain("[Source exacte — pont statusLine Claude Code]", report);
        Assert.DoesNotContain("[Source exacte — endpoint OAuth (repli)]", report);
        Assert.DoesNotContain("Token déchiffré", report);
        Assert.DoesNotContain("[Conseil]", report);
    }

    /// <summary>DAT-04 — la durée de construction du rapport est mesurée (Stopwatch) et dite en dernière ligne : elle se
    /// consigne d'elle-même dans chronos.log à chaque lancement. FORMAT seulement, aucun seuil de temps (flottant).</summary>
    [Fact]
    public async Task Le_rapport_finit_par_sa_duree_de_construction()
    {
        var paths = TempPaths();
        var diag = new DiagnosticService(paths,
            new SettingsService(paths), new StubProvider(UsageSnapshot.Empty), new FakeClock(DateTimeOffset.UtcNow));

        var report = await diag.BuildReportAsync();

        var derniere = report.Split('\n').Select(l => l.TrimEnd('\r')).Last(l => l.Trim().Length > 0);
        Assert.StartsWith("Rapport construit en ", derniere);
        Assert.Contains(" ms (dont chaîne de données ", derniere);
        Assert.EndsWith(" ms)", derniere);
    }

    /// <summary>DAT-04 — la section « [Chaîne de données] » décrit la chaîne réelle DANS SON ORDRE : sonde d'en-têtes,
    /// secours OAuth du login Chronos, dernier exact persisté, journal.</summary>
    [Fact]
    public async Task Le_rapport_decrit_la_chaine_dans_l_ordre()
    {
        var paths = TempPaths();
        var diag = new DiagnosticService(paths,
            new SettingsService(paths), new StubProvider(UsageSnapshot.Empty), new FakeClock(DateTimeOffset.UtcNow));

        var report = await diag.BuildReportAsync();

        var iSection = report.IndexOf("[Chaîne de données]", StringComparison.Ordinal);
        Assert.True(iSection >= 0, "section [Chaîne de données] absente");
        var iChaine = report.IndexOf("Chaîne : ", iSection, StringComparison.Ordinal);
        var iSonde = report.IndexOf("Sonde d'en-têtes", iSection, StringComparison.Ordinal);
        var iSecours = report.IndexOf("Secours OAuth", iSection, StringComparison.Ordinal);
        var iDernier = report.IndexOf("Dernier exact persisté", iSection, StringComparison.Ordinal);
        var iJournal = report.IndexOf("Journal : ", iSection, StringComparison.Ordinal);
        Assert.True(iChaine > iSection && iSonde > iChaine && iSecours > iSonde && iDernier > iSecours && iJournal > iDernier,
            $"ordre inattendu : section {iSection}, chaîne {iChaine}, sonde {iSonde}, secours {iSecours}, dernier exact {iDernier}, journal {iJournal}");
        // La section est bornée : tout cela se dit avant la section suivante.
        Assert.True(iJournal < report.IndexOf("[Transcripts JSONL", StringComparison.Ordinal));
    }

    /// <summary>DAT-04 — garde STRUCTURELLE : le diagnostic ne cherche plus les coffres (≈ 17 s mesurées), ne cartographie
    /// plus les dossiers et n'appelle plus le réseau. Lit le source via AssemblyMetadata("CheminSourcesChronos").</summary>
    [Fact]
    public void Le_diagnostic_ne_cherche_plus_les_coffres_ni_n_appelle_le_reseau()
    {
        var racine = typeof(DiagnosticServiceTests).Assembly
                         .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                         .Cast<System.Reflection.AssemblyMetadataAttribute>()
                         .FirstOrDefault(a => a.Key == "CheminSourcesChronos")?.Value ?? "";
        Assert.False(string.IsNullOrWhiteSpace(racine), "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        var chemin = System.IO.Path.Combine(racine, "Services", "DiagnosticService.cs");
        Assert.True(System.IO.File.Exists(chemin), "DiagnosticService.cs introuvable : " + chemin);
        var lignes = System.IO.File.ReadAllLines(chemin);
        Assert.True(lignes.Length > 500, $"DiagnosticService.cs ne fait que {lignes.Length} lignes : garde muette ?");
        var texte = string.Join("\n", lignes);

        foreach (var interdit in new[] { "CoffresOAuth", "WindowsCredentialStore", "TryReadAccessToken", "HttpClient",
                                         "api/oauth/usage", "EnumerateDirectories(", "[Conseil]", "pont statusLine" })
            Assert.False(texte.Contains(interdit, StringComparison.Ordinal), $"DiagnosticService.cs contient encore « {interdit} ».");
        Assert.Contains("[Chaîne de données]", texte, StringComparison.Ordinal);
    }

    /// <summary>TOK-02 : le rapport nomme l'état d'authentification RÉEL, pas la seule présence
    /// du fichier oauth.dat — c'est cette confusion qui a laissé l'utilisateur deux mois dans le noir
    /// (jeton expiré le 2026-07-12, oauth.dat parfaitement présent, diagnostic affichant « Connecté :
    /// OUI »). Le rapport n'émet plus aucune requête réseau (phase 37) : le composite est un stub.</summary>
    [Fact]
    public async Task Le_rapport_nomme_l_etat_d_authentification_reel()
    {
        var paths = TempPaths();
        var settings = new SettingsService(paths);
        var auth = new FakeAuthStatus { Etat = EtatAuthentification.Deconnecte };
        var diag = new DiagnosticService(paths,
                                         settings, new StubProvider(UsageSnapshot.Empty),
                                         new FakeClock(DateTimeOffset.UtcNow), auth);

        var report = await diag.BuildReportAsync();

        Assert.Contains("État d'authentification : DÉCONNECTÉ", report);
    }

    // --- Phase 18 (HDR-01/HDR-03/HDR-04/HDR-06) : le rapport dit ce que la SONDE reçoit ---
    //
    // SÉCURITÉ, commune aux quatre tests : le rapport n'émet plus aucune requête réseau ni ne lit aucun coffre
    // (phase 37) ; le composite est un stub et le settings.json vit sous %TEMP%.

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
        var diag = new DiagnosticService(paths,
                                         new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                         new FakeClock(DateTimeOffset.UtcNow), null, etat);

        var report = await diag.BuildReportAsync();

        Assert.Contains("[Chaîne de données]", report);
        Assert.Contains("Sonde d'en-têtes", report);
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
        var diag = new DiagnosticService(paths,
                                         new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                         new FakeClock(DateTimeOffset.UtcNow), null, etat);

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
        var diag = new DiagnosticService(paths,
                                         new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                         new FakeClock(DateTimeOffset.UtcNow), null, etat);

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
        var diag = new DiagnosticService(paths,
                                         new SettingsService(paths), new StubProvider(snap),
                                         new FakeClock(DateTimeOffset.UtcNow));

        var report = await diag.BuildReportAsync();

        Assert.Contains("pas encore sondé", report);
        Assert.Contains("aucun dépassement rapporté", report);
        Assert.Contains("PLANCHER", report);                 // assertion existante, vocabulaire 19-04
        Assert.DoesNotContain("~", LigneCinqHeures(report));  // « ≥ » partout, plus jamais « ~ »
    }
    // --- Phase 20 (EXA-06) : le rapport nomme QUI alimente chaque fenêtre, et DEPUIS QUAND ---
    //
    // SÉCURITÉ, commune aux quatre tests : le rapport n'émet plus aucune requête réseau (phase 37) ;
    // le composite est un stub.

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
        var diag = new DiagnosticService(paths,
                                         new SettingsService(paths), new StubProvider(snap),
                                         new FakeClock(now));

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
        var diag = new DiagnosticService(paths,
                                         new SettingsService(paths), new StubProvider(snap),
                                         new FakeClock(now));

        var report = await diag.BuildReportAsync();

        Assert.Contains("relevé il y a 12 min", report);
    }

    /// <summary>Règle de non-retour : une absence de source ne produit JAMAIS d'affirmation. Le dernier
    /// Assert vise la LIGNE de résultat et non la section de la sonde, qui porte légitimement le même
    /// libellé (« Sonde d'en-têtes » sous « [Chaîne de données] »).</summary>
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
        var diag = new DiagnosticService(paths,
                                         new SettingsService(paths), new StubProvider(snap),
                                         new FakeClock(now));

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
        var diag = new DiagnosticService(paths,
                                         new SettingsService(paths), new StubProvider(snap),
                                         new FakeClock(now));

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
        var diag = new DiagnosticService(paths,
                                         new SettingsService(paths),
                                         new ProviderQuiDeclencheLaSonde(snap, etat),
                                         new FakeClock(now), etatServeur: etat);

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
        var diag = new DiagnosticService(paths,
                                         new SettingsService(paths), new StubProvider(snap),
                                         new FakeClock(now),
                                         etatServeur: new EtatServeurFige { Depassement = dep });
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
    // SÉCURITÉ, commune à ces tests : le rapport n'émet plus aucune requête réseau ni ne balaie aucun coffre
    // (phase 37), et TOUS les chemins de moniteur/magasins sont sous %TEMP%. Aucun n'écrit dans %APPDATA%\Chronos.

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
        var diag = new DiagnosticService(paths,
                                         new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                         new FakeClock(T22),
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

    /// <summary>VAL-03 — le rapport dit la version EMBARQUÉE, juste sous « Date : » : c'est le « diagnostic » du critère 2
    /// de la phase 31. La valeur attendue est lue ICI sur l'assembly, indépendamment du code du rapport, et jamais écrite en
    /// dur : la valeur publiée est tenue par le plan de release et par <see cref="VersionPublieeTests"/>.</summary>
    [Fact]
    public async Task Le_rapport_dit_la_version_embarquee()
    {
        var attendue = (typeof(DiagnosticService).Assembly
                            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                            .SingleOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute)?.InformationalVersion;
        // Anti-muet : une assembly sans version informative ne garderait rien.
        Assert.False(string.IsNullOrWhiteSpace(attendue), "l'assembly de Chronos ne porte aucune version informative");
        Assert.Matches(@"^\d+\.\d+\.\d+$", attendue);   // X.Y.Z, sans « +<sha> »

        var report = await Rapport(null);
        var lignes = report.Replace("\r\n", "\n").Split('\n');

        Assert.Equal("=== Chronos — Diagnostic ===", lignes[0]);
        Assert.StartsWith("Date : ", lignes[1]);
        Assert.Equal("Version : " + attendue, lignes[2]);
        Assert.Single(lignes, l => l.StartsWith("Version : ", System.StringComparison.Ordinal));
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
        Assert.Contains("Rapport construit en", report);
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
        var diag = new DiagnosticService(paths,
                                         new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                         new FakeClock(maintenant),
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

    // ------------------------------------------------------------------ CPT-02 (phase 32) : [Magasins persistants]
    // Le rapport dit désormais, sans qu'on le lui demande, depuis quelle VUE d'AppData il lit et quand chaque
    // magasin persistant a écrit pour la dernière fois — ou pourquoi il n'a pas pu. C'est l'absence de ces deux
    // lignes qui a laissé prendre la copie virtualisée du paquet MSIX pour un « gel » de last-exact.json.

    /// <summary>Faux magasin : la forme que 32-05 injectera (LastExactStore, journal des relevés).</summary>
    private sealed class FauxMagasin : IEtatMagasin
    {
        public string Nom { get; init; } = "";
        public string Chemin { get; init; } = "";
        public DateTimeOffset? DerniereEcriture { get; init; }
        public string? DerniereErreur { get; init; }
    }

    /// <summary>Le rapport n'émet aucune requête (phase 37) ; le composite est un stub. Seul le paramètre
    /// <c>magasins</c> varie.</summary>
    private static DiagnosticService DiagAvecMagasins(ChronosPaths paths, FakeClock clock, IReadOnlyList<IEtatMagasin>? magasins)
        => new(paths, new SettingsService(paths),
               new StubProvider(UsageSnapshot.Empty), clock, magasins: magasins);

    private static List<string> Lignes(string report)
        => report.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

    private static string Ligne(string report, string debut)
        => Lignes(report).Single(l => l.TrimStart().StartsWith(debut, StringComparison.Ordinal));

    [Fact]
    public async Task La_section_Magasins_persistants_dit_la_vue_et_les_trois_magasins()
    {
        var paths = TempPaths();

        var report = await DiagAvecMagasins(paths, new FakeClock(DateTimeOffset.UtcNow), magasins: null).BuildReportAsync();

        Assert.Contains("[Magasins persistants]", report);
        // Les tests tournent tantôt SOUS l'app bureau (vue virtualisée), tantôt non : la ligne existe toujours,
        // sa valeur est l'une des deux — on n'épingle pas la machine qui exécute la suite.
        var vue = Ligne(report, "Vue AppData : ");
        Assert.True(vue.Contains("réelle") || vue.Contains("virtualisée"), vue);
        Assert.EndsWith("aucune écriture (fichier absent)", Ligne(report, "dernier exact : "));
        Assert.Contains("aucune écriture (dossier absent)", Ligne(report, "journal des relevés : "));
        Assert.Contains("aucune écriture (dossier absent)", Ligne(report, "agrégats de tokens : "));   // 33-05 : troisième magasin réel
        Assert.True(report.IndexOf("[Magasins persistants]", StringComparison.Ordinal)
                    < report.IndexOf("[Ce qui est affiché maintenant]", StringComparison.Ordinal),
                    "la section des magasins précède « Ce qui est affiché maintenant »");
    }

    [Fact]
    public async Task Un_magasin_injecte_donne_son_age_et_sa_derniere_erreur()
    {
        var paths = TempPaths();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        System.IO.File.WriteAllText(paths.LastExactFile, "{}");   // le fichier existe ; l'âge INJECTÉ prime sur le mtime
        var magasin = new FauxMagasin
        {
            Nom = NomsMagasins.DernierExact,
            Chemin = paths.LastExactFile,
            DerniereEcriture = clock.UtcNow.AddMinutes(-4),
            DerniereErreur = "IOException : disque plein",
        };

        var report = await DiagAvecMagasins(paths, clock, new[] { magasin }).BuildReportAsync();

        var lignes = Lignes(report);
        var i = lignes.FindIndex(l => l.TrimStart().StartsWith("dernier exact : ", StringComparison.Ordinal));
        Assert.True(i >= 0, report);
        Assert.Contains("dernière écriture il y a 4 min", lignes[i]);
        Assert.Contains("ÉCHEC de la dernière écriture : IOException : disque plein", lignes[i + 1]);
    }

    [Fact]
    public async Task Un_fichier_sur_disque_sans_magasin_injecte_donne_quand_meme_son_age()
    {
        var paths = TempPaths();
        System.IO.File.WriteAllText(paths.LastExactFile, "{ \"version\": 1 }");

        var report = await DiagAvecMagasins(paths, new FakeClock(DateTimeOffset.UtcNow), magasins: null).BuildReportAsync();

        // Repli D-32-08 : sans état injecté (aucun câblage DI), les faits disque suffisent — âge et taille.
        var ligne = Ligne(report, "dernier exact : ");
        Assert.Contains("dernière écriture", ligne);
        Assert.Contains("o)", ligne);
        Assert.DoesNotContain("aucune écriture", ligne);
    }

    // ------------------------------------------------------------------ 42.2-03 (MAT-3 / MAT-4) : magasins du widget de sessions
    // Une lecture non aboutie et une quarantaine se VOIENT au diagnostic, sous [Magasins persistants], pour archived.json et
    // treated.json — les mêmes instances que le widget en production.

    private static IEtatMagasin MagasinSessions(string type, string chemin) => type == "archive"
        ? new ArchiveStore(chemin, new FakeClock(DateTimeOffset.UtcNow))
        : new TreatedStore(chemin, new FakeClock(DateTimeOffset.UtcNow));

    private static void Lire(IEtatMagasin m)
    {
        if (m is ArchiveStore a) a.Load(); else ((TreatedStore)m).Load();
    }

    private static bool Ecrire(IEtatMagasin m) =>
        m is ArchiveStore a ? a.Add("X") : ((TreatedStore)m).Set("X", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    [Theory]
    [InlineData("archive", "archived.json", "sessions archivées")]
    [InlineData("traites", "treated.json", "sessions traitées")]
    public async Task Une_lecture_non_aboutie_d_un_magasin_de_sessions_se_voit_au_diagnostic(string type, string fichier, string nom)
    {
        var paths = TempPaths();
        var chemin = Path.Combine(Path.GetDirectoryName(paths.SettingsFile)!, fichier);
        File.WriteAllText(chemin, "{\"a\": 1");
        var magasin = MagasinSessions(type, chemin);
        Lire(magasin);

        var report = await DiagAvecMagasins(paths, new FakeClock(DateTimeOffset.UtcNow), new[] { magasin }).BuildReportAsync();

        var lignes = Lignes(report);
        var i = lignes.FindIndex(l => l.TrimStart().StartsWith(nom + " : ", StringComparison.Ordinal));
        Assert.True(i >= 0, report);
        Assert.Contains("LECTURE NON ABOUTIE", lignes[i + 1]);
        Assert.Contains("illisible", lignes[i + 1]);
    }

    [Theory]
    [InlineData("archive", "archived.json", "sessions archivées", "archived.illisible-")]
    [InlineData("traites", "treated.json", "sessions traitées", "treated.illisible-")]
    public async Task Une_quarantaine_d_un_magasin_de_sessions_se_voit_au_diagnostic(string type, string fichier, string nom, string prefixe)
    {
        var paths = TempPaths();
        var chemin = Path.Combine(Path.GetDirectoryName(paths.SettingsFile)!, fichier);
        File.WriteAllText(chemin, "{\"a\": 1");
        var magasin = MagasinSessions(type, chemin);
        Assert.True(Ecrire(magasin));

        var report = await DiagAvecMagasins(paths, new FakeClock(DateTimeOffset.UtcNow), new[] { magasin }).BuildReportAsync();

        Assert.Contains(nom + " : ", report);
        var q = Lignes(report).Single(l => l.Contains("QUARANTAINE : original illisible conservé sous", StringComparison.Ordinal));
        Assert.Contains(prefixe, q);
        Assert.Contains(".json", q);
    }

    [Fact]
    public async Task Sans_magasins_de_sessions_injectes_aucune_ligne_n_est_devinee()
    {
        var paths = TempPaths();

        var report = await DiagAvecMagasins(paths, new FakeClock(DateTimeOffset.UtcNow), magasins: null).BuildReportAsync();

        Assert.DoesNotContain("sessions archivées : ", report);
        Assert.DoesNotContain("sessions traitées : ", report);
    }

    // ------------------------------------------------------------------ JRN-04 / CPT-03 (phase 32, 32-05) : journal muet, processus, verrou
    // L'âge de la dernière écriture du journal devient un chiffre de première classe : le rapport CRIE quand le journal se tait
    // alors que Chronos tourne (D-32-21 : mesuré depuis max(démarrage, dernière écriture), sinon l'alerte s'allumerait à chaque
    // lancement sur l'écriture de la veille), et il compte les processus Chronos et nomme l'état du verrou (câblage de 32-03).

    private static DiagnosticService DiagAvecJournal(ChronosPaths paths, FakeClock clock, DateTimeOffset? derniereEcriture, DateTimeOffset demarrage)
        => new(paths, new SettingsService(paths),
               new StubProvider(UsageSnapshot.Empty), clock,
               magasins: new[] { new FauxMagasin { Nom = NomsMagasins.JournalReleves, Chemin = paths.HistoriqueDir, DerniereEcriture = derniereEcriture } },
               demarrageProcessus: demarrage);

    private static string? LigneApres(string report, string debut)
    {
        var lignes = Lignes(report);
        var i = lignes.FindIndex(l => l.TrimStart().StartsWith(debut, StringComparison.Ordinal));
        Assert.True(i >= 0, "ligne « " + debut + " » introuvable :\n" + report);
        return i + 1 < lignes.Count ? lignes[i + 1].Trim() : null;
    }

    [Fact]
    public async Task Le_diagnostic_dit_journal_muet_quand_la_derniere_ecriture_a_plus_de_quinze_minutes()
    {
        var paths = TempPaths();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var now = clock.UtcNow;

        var muet = await DiagAvecJournal(paths, clock, derniereEcriture: now.AddMinutes(-16), demarrage: now.AddHours(-1)).BuildReportAsync();
        Assert.Equal("ALERTE — journal muet depuis 16 min", LigneApres(muet, "journal des relevés : "));

        var vivant = await DiagAvecJournal(paths, clock, derniereEcriture: now.AddMinutes(-4), demarrage: now.AddHours(-1)).BuildReportAsync();
        Assert.DoesNotContain("ALERTE", vivant);
        Assert.Contains("dernière écriture il y a 4 min", Ligne(vivant, "journal des relevés : "));
    }

    [Fact]
    public async Task Le_diagnostic_ne_dit_pas_muet_juste_apres_le_demarrage()
    {
        var paths = TempPaths();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var now = clock.UtcNow;

        // Dernière écriture d'il y a 3 h (la veille, en pratique), processus démarré il y a 5 min : la référence est le
        // démarrage, pas l'écriture — « muet » veut dire « alors que Chronos tourne », et il tourne depuis 5 min.
        var report = await DiagAvecJournal(paths, clock, derniereEcriture: now.AddHours(-3), demarrage: now.AddMinutes(-5)).BuildReportAsync();

        Assert.DoesNotContain("ALERTE", report);
        Assert.Contains("dernière écriture il y a 3 h 00", Ligne(report, "journal des relevés : "));
    }

    [Fact]
    public async Task Le_diagnostic_compte_les_processus_Chronos_et_dit_l_etat_du_verrou()
    {
        var paths = TempPaths();
        var clock = new FakeClock(DateTimeOffset.UtcNow);

        var report = await DiagAvecJournal(paths, clock, derniereEcriture: null, demarrage: clock.UtcNow).BuildReportAsync();

        var processus = Ligne(report, "Processus Chronos : ");
        Assert.False(string.IsNullOrWhiteSpace(processus));
        var verrou = Ligne(report, "Verrou mono-instance (Local\\Chronos-overlay) : ");
        Assert.True(verrou.EndsWith("libre", StringComparison.Ordinal)
                    || verrou.EndsWith("tenu par ce processus", StringComparison.Ordinal)
                    || verrou.EndsWith("tenu par un autre processus", StringComparison.Ordinal), verrou);
        // Les deux lignes vivent dans la section des magasins persistants, avant la ligne des agrégats (phase 33).
        Assert.True(report.IndexOf("[Magasins persistants]", StringComparison.Ordinal) < report.IndexOf("Processus Chronos : ", StringComparison.Ordinal)
                    && report.IndexOf("Processus Chronos : ", StringComparison.Ordinal) < report.IndexOf("agrégats de tokens : ", StringComparison.Ordinal),
                    "les lignes de processus et de verrou appartiennent à [Magasins persistants]");
    }

    // ------------------------------------------------------------------ TOK-01/TOK-02 (phase 33, 33-05) : le troisième magasin et la reconstruction
    // La ligne « agrégats de tokens » devient une vraie ligne de magasin (même moule que le journal : faits disque + état injecté), suivie
    // d'une sous-section qui dit où en est la reconstruction — en ENTIERS (N / M, jamais une fraction) — et le PÉRIMÈTRE mot pour mot
    // (D-33-23) : ces tokens sont un comptage local partiel, jamais un pourcentage du forfait.

    private static DiagnosticService DiagAvecReconstruction(ChronosPaths paths, FakeClock clock, IReadOnlyList<IEtatMagasin>? magasins,
                                                            IEtatReconstruction? reconstruction)
        => new(paths, new SettingsService(paths),
               new StubProvider(UsageSnapshot.Empty), clock, magasins: magasins,
               reconstruction: reconstruction);

    /// <summary>Les lignes de la sous-section des agrégats : de la ligne du magasin à la ligne vide qui clôt [Magasins persistants].</summary>
    private static List<string> SousSectionAgregats(string report)
    {
        var lignes = Lignes(report);
        var debut = lignes.FindIndex(l => l.TrimStart().StartsWith("agrégats de tokens : ", StringComparison.Ordinal));
        Assert.True(debut >= 0, report);
        var fin = lignes.FindIndex(debut, l => l.Trim().Length == 0);
        return lignes.GetRange(debut, (fin < 0 ? lignes.Count : fin) - debut);
    }

    [Fact]
    public async Task La_section_des_agregats_dit_la_progression_le_dernier_fichier_et_le_perimetre()
    {
        var paths = TempPaths();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var etat = new FakeEtatReconstruction
        {
            Phase = PhaseReconstruction.Reconstruction,
            FichiersTraites = 12,
            FichiersTotal = 1603,
            SemaineCouranteDisponible = true,
            DernierFichier = "proj-a/<s>/subagents/agent-x.jsonl",
            FichiersDisparus = 2,
            LignesIgnorees = 3,
            IdsConnus = 118401,
        };
        var magasins = new IEtatMagasin[] { new MagasinAgregats(paths.HistoriqueDir, clock) };

        var report = await DiagAvecReconstruction(paths, clock, magasins, etat).BuildReportAsync();

        var sous = SousSectionAgregats(report);
        Assert.Contains(sous, l => l.Trim() == "Reconstruction : reconstruction en cours — 12 / 1603 fichiers · semaine courante : complète · dernier fichier : proj-a/<s>/subagents/agent-x.jsonl");
        Assert.Contains(sous, l => l.Trim() == "Fichiers disparus : 2 · lignes ignorées : 3 · ids connus : 118401");
        Assert.Contains(sous, l => l.Trim() == "Périmètre : Claude Code seulement — hors Cowork et claude.ai ; bruts, non pondérés");
        Assert.Contains(sous, l => l.Trim() == "Périmètre : " + LigneAgregat.Perimetre);
        Assert.DoesNotContain(sous, l => l.Contains('%') || l.Contains("pour cent", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(sous, l => l.TrimStart().StartsWith("ÉCHEC", StringComparison.Ordinal));

        // En échec : la cause est dite, sur sa ligne, dans la même sous-section.
        etat.Phase = PhaseReconstruction.EnEchec;
        etat.DerniereErreur = "IOException : x";
        var rapportEchec = await DiagAvecReconstruction(paths, clock, magasins, etat).BuildReportAsync();
        var sousEchec = SousSectionAgregats(rapportEchec);
        Assert.Contains(sousEchec, l => l.Trim() == "ÉCHEC : IOException : x");
        Assert.Contains(sousEchec, l => l.TrimStart().StartsWith("Reconstruction : EN ÉCHEC — 12 / 1603 fichiers", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Les_messages_de_mois_geles_ignores_se_disent_au_diagnostic_seulement_s_il_y_en_a()
    {
        var paths = TempPaths();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var etat = new FakeEtatReconstruction { Phase = PhaseReconstruction.Incremental, FichiersTraites = 3, FichiersTotal = 3 };
        var magasins = new IEtatMagasin[] { new MagasinAgregats(paths.HistoriqueDir, clock) };

        var sansIgnores = SousSectionAgregats(await DiagAvecReconstruction(paths, clock, magasins, etat).BuildReportAsync());
        Assert.DoesNotContain(sansIgnores, l => l.Contains("mois gelés", StringComparison.Ordinal));

        etat.MessagesIgnoresMoisGeles = 7;
        var avecIgnores = SousSectionAgregats(await DiagAvecReconstruction(paths, clock, magasins, etat).BuildReportAsync());
        Assert.Contains(avecIgnores, l => l.Trim() == "Messages de mois gelés ignorés (déjà comptés) : 7");
    }

    [Fact]
    public async Task Sans_etat_de_reconstruction_la_ligne_des_agregats_dit_le_dossier_ou_le_fichier_du_mois()
    {
        var paths = TempPaths();
        var clock = new FakeClock(DateTimeOffset.UtcNow);

        // 1) Dossier historique absent : rien n'a jamais été écrit, et aucune ligne de progression (aucun état injecté).
        var report = await DiagAvecReconstruction(paths, clock, magasins: null, reconstruction: null).BuildReportAsync();
        Assert.EndsWith("aucune écriture (dossier absent)", Ligne(report, "agrégats de tokens : "));
        Assert.DoesNotContain(Lignes(report), l => l.TrimStart().StartsWith("Reconstruction : ", StringComparison.Ordinal));
        Assert.Contains(Lignes(report), l => l.Trim() == "Périmètre : " + LigneAgregat.Perimetre);   // le périmètre se dit TOUJOURS

        // 2) Dossier présent, fichier du mois absent : la ligne nomme le fichier du mois UTC courant.
        System.IO.Directory.CreateDirectory(paths.HistoriqueDir);
        var nomDuMois = MagasinAgregats.NomFichier(clock.UtcNow);
        report = await DiagAvecReconstruction(paths, clock, magasins: null, reconstruction: null).BuildReportAsync();
        var ligne = Ligne(report, "agrégats de tokens : ");
        Assert.Contains(nomDuMois, ligne);
        Assert.EndsWith("aucune écriture (fichier du mois absent)", ligne);

        // 3) Fichier du mois écrit à l'instant : âge et taille, motif LigneMagasin (faits disque sans état injecté).
        System.IO.File.WriteAllText(System.IO.Path.Combine(paths.HistoriqueDir, nomDuMois),
            "{\"v\":1,\"slot\":\"2026-09-01T00:00:00.0000000+00:00\",\"model\":\"m\",\"sub\":false,\"in\":1,\"out\":1,\"cache_w\":0,\"cache_r\":0,\"n\":1}\n");
        report = await DiagAvecReconstruction(paths, clock, magasins: null, reconstruction: null).BuildReportAsync();
        ligne = Ligne(report, "agrégats de tokens : ");
        Assert.Contains(nomDuMois, ligne);
        Assert.Contains("dernière écriture à l'instant", ligne);
        Assert.EndsWith(" o)", ligne);
        Assert.DoesNotContain("aucune écriture", ligne);
    }

    // ------------------------------------------------------------------ ACC-03 (phase 35, 35-03) : [Journal d'historique]
    // Le rapport dit, sans ouvrir la fenêtre, ce que le journal sait de lui-même : ses fichiers, sa dernière écriture, la journée
    // lue par la MÊME façade que la vue Jour et dite par les MÊMES mots, ses cinq derniers événements, où en est la reconstruction
    // des tokens et combien de Chronos tournent. Journal écrit par le VRAI écrivain sous %TEMP% ; AUCUN relevé la veille (le
    // résultat ne dépend pas de la lecture de la veille). Fuseau de Paris injecté (jamais TimeZoneInfo.Local en test).

    private static readonly DateTimeOffset Now35 = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Demarrage35 = new(2026, 9, 27, 6, 7, 10, TimeSpan.Zero);

    /// <summary>Un <c>demarrage</c> 3.2.2 à 06:07:10Z puis un relevé toutes les 5 min jusqu'à 11:57:10Z (71 relevés) ; le fichier
    /// du mois est daté de 11:57:10Z, et un <c>curseurs.json</c> de 11:00Z l'accompagne.</summary>
    private static ChronosPaths JournalDuJour35()
    {
        var paths = TempPaths();
        var journal = new Chronos.Services.Historique.JournalReleves(paths.HistoriqueDir, new FakeClock(Now35));
        Assert.True(journal.AjouterEvenement(new Chronos.Models.Historique.EvenementJournal(Demarrage35, Chronos.Models.Historique.TypeEvenement.Demarrage, Version: "3.2.2")));
        for (var t = Demarrage35; t < Now35; t += TimeSpan.FromMinutes(5))
            Assert.True(journal.AjouterReleve(new Chronos.Models.Historique.ReleveJournal(t, SourceUsage.SondeEnTetes,
                U5: 0.20, R5: new DateTimeOffset(2026, 9, 27, 14, 0, 0, TimeSpan.Zero), Statut5: StatutServeur.Autorise,
                U7: 0.40, R7: new DateTimeOffset(2026, 10, 2, 22, 0, 0, TimeSpan.Zero), Statut7: StatutServeur.Autorise,
                Overage: null, OverageStatut: null)));
        System.IO.File.SetLastWriteTimeUtc(System.IO.Path.Combine(paths.HistoriqueDir, "releves-2026-09.jsonl"), new DateTime(2026, 9, 27, 11, 57, 10, DateTimeKind.Utc));
        var curseurs = System.IO.Path.Combine(paths.HistoriqueDir, "curseurs.json");
        System.IO.File.WriteAllText(curseurs, "{}");
        System.IO.File.SetLastWriteTimeUtc(curseurs, new DateTime(2026, 9, 27, 11, 0, 0, DateTimeKind.Utc));
        return paths;
    }

    private static DiagnosticService Diag35(ChronosPaths paths, IEtatReconstruction? reconstruction, TimeZoneInfo? fuseau)
        => new(paths, new SettingsService(paths),
               new StubProvider(UsageSnapshot.Empty), new FakeClock(Now35),
               reconstruction: reconstruction, fuseau: fuseau);

    /// <summary>Les lignes de la section : de l'en-tête à la ligne vide qui la clôt (en-tête exclu), sans l'indentation.</summary>
    private static List<string> SectionJournal35(string report)
    {
        var lignes = Lignes(report);
        var debut = lignes.FindIndex(l => l == "[Journal d'historique]");
        Assert.True(debut >= 0, report);
        var fin = lignes.FindIndex(debut, l => l.Trim().Length == 0);
        return lignes.GetRange(debut + 1, (fin < 0 ? lignes.Count : fin) - debut - 1).Select(l => l.Trim()).ToList();
    }

    [Fact]
    public async Task La_section_journal_d_historique_suit_les_magasins_persistants()
    {
        var report = await Diag35(JournalDuJour35(), reconstruction: null, Chronos.Services.Historique.BornesPlage.FuseauParisPourTests()).BuildReportAsync();

        Assert.Single(Lignes(report), l => l == "[Journal d'historique]");
        var magasins = report.IndexOf("[Magasins persistants]", StringComparison.Ordinal);
        var journal = report.IndexOf("[Journal d'historique]", StringComparison.Ordinal);
        var affiche = report.IndexOf("[Ce qui est affiché maintenant]", StringComparison.Ordinal);
        Assert.True(magasins >= 0 && magasins < journal && journal < affiche, report);
    }

    [Fact]
    public async Task La_section_dit_les_fichiers_leur_taille_et_la_derniere_ecriture()
    {
        var paths = JournalDuJour35();
        var tailleReleves = new System.IO.FileInfo(System.IO.Path.Combine(paths.HistoriqueDir, "releves-2026-09.jsonl")).Length;
        var ligneReleves = $"releves-2026-09.jsonl — {tailleReleves} o — modifié il y a 2 min";
        const string ligneCurseurs = "curseurs.json — 2 o — modifié il y a 1 h 00";

        var section = SectionJournal35(await Diag35(paths, reconstruction: null, Chronos.Services.Historique.BornesPlage.FuseauParisPourTests()).BuildReportAsync());

        Assert.Contains("Dossier : " + paths.HistoriqueDir, section);
        Assert.Contains("Fichiers : 2", section);
        Assert.Contains(ligneReleves, section);
        Assert.Contains(ligneCurseurs, section);
        Assert.True(section.IndexOf(ligneReleves) < section.IndexOf(ligneCurseurs), string.Join("\n", section));   // familles : relevés d'abord
        Assert.Contains("Dernière écriture du journal (disque) : il y a 2 min — releves-2026-09.jsonl", section);
    }

    [Fact]
    public async Task La_section_dit_la_journee_avec_les_mots_de_la_vue_Jour()
    {
        var paths = JournalDuJour35();
        var fuseau = Chronos.Services.Historique.BornesPlage.FuseauParisPourTests();
        // La façade de la fenêtre, appelée ICI : le rapport doit rendre exactement la même ligne que la vue Jour.
        var plage = Chronos.Services.Historique.BornesPlage.Jour(Now35, fuseau);
        var donnees = new Chronos.Services.Historique.SourceHistoriqueDisque(paths, fuseau).LireJour(plage, Now35);
        var attendu = Chronos.Text.TextesHistorique.LigneFraicheurJour(plage, donnees.Analyse, RateLimitHeaderUsageProvider.CadenceNominale, fuseau);
        Assert.StartsWith("288 relevés attendus · 71 présents · 0 interruption", attendu);

        var section = SectionJournal35(await Diag35(paths, reconstruction: null, fuseau).BuildReportAsync());

        Assert.Contains("Jour (27 sept. 2026) : " + attendu, section);
        Assert.Contains("journal ouvert le 27 sept. 2026", section);
    }

    [Fact]
    public async Task La_section_dit_les_cinq_derniers_evenements_et_la_reconstruction()
    {
        var paths = JournalDuJour35();
        var fuseau = Chronos.Services.Historique.BornesPlage.FuseauParisPourTests();
        var terminee = new FakeEtatReconstruction
        {
            Phase = PhaseReconstruction.Incremental, FichiersTraites = 1603, FichiersTotal = 1603,
            DerniereReconstructionTerminee = new DateTimeOffset(2026, 9, 27, 6, 8, 0, TimeSpan.Zero),
        };

        var section = SectionJournal35(await Diag35(paths, terminee, fuseau).BuildReportAsync());

        var entete = section.IndexOf("Événements récents (5 derniers) :");
        Assert.True(entete >= 0, string.Join("\n", section));
        Assert.Equal("2026-09-27 08:07 demarrage (3.2.2)", section[entete + 1]);   // heure de Paris (UTC+2)
        Assert.Contains("Reconstruction des tokens : terminée le 27 sept. 08:08 (détail sous [Magasins persistants])", section);
        Assert.Contains(section, l => System.Text.RegularExpressions.Regex.IsMatch(l,
            @"^Instances Chronos : (\d+ \(détail sous \[Magasins persistants\]\)|relevé impossible)$"));

        var enCours = new FakeEtatReconstruction { Phase = PhaseReconstruction.Reconstruction, FichiersTraites = 886, FichiersTotal = 1603 };
        section = SectionJournal35(await Diag35(paths, enCours, fuseau).BuildReportAsync());
        Assert.Contains("Reconstruction des tokens : en cours — 886 / 1603 fichiers (détail sous [Magasins persistants])", section);

        section = SectionJournal35(await Diag35(paths, reconstruction: null, fuseau).BuildReportAsync());
        Assert.Contains("Reconstruction des tokens : non câblée", section);
    }

    [Fact]
    public async Task Sans_fuseau_la_section_parle_en_UTC_et_le_dit()
    {
        var paths = JournalDuJour35();

        var sansFuseau = SectionJournal35(await Diag35(paths, reconstruction: null, fuseau: null).BuildReportAsync());
        Assert.Contains("Fuseau : UTC (fuseau non injecté)", sansFuseau);
        Assert.Contains("2026-09-27 06:07 demarrage (3.2.2)", sansFuseau);

        var fuseau = Chronos.Services.Historique.BornesPlage.FuseauParisPourTests();
        var avecFuseau = SectionJournal35(await Diag35(paths, reconstruction: null, fuseau).BuildReportAsync());
        Assert.Contains("Fuseau : " + fuseau.Id, avecFuseau);
        Assert.DoesNotContain("Fuseau : UTC (fuseau non injecté)", avecFuseau);
    }

    // SOC-01 (phase 36) — une ligne « Réglages (settings.json) : » sous [Magasins persistants] (la section [Réglage] est
    // retirée en phase 37).

    private static async Task<string> RapportAvecSettings(string json)
    {
        var paths = TempPaths();
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(paths.SettingsFile)!);
        System.IO.File.WriteAllText(paths.SettingsFile, json);
        var diag = new DiagnosticService(paths, new SettingsService(paths),
                                         new StubProvider(UsageSnapshot.Empty), new FakeClock(DateTimeOffset.UtcNow));
        return await diag.BuildReportAsync();
    }

    [Fact]
    public async Task Rapport_nomme_les_reglages_retombes_sur_leur_defaut()
    {
        var report = await RapportAvecSettings("{\"ThemeKey\":\"nord\",\"CadranStyle\":\"Spirale\",\"SessionStyle\":\"Inconnu\"}");

        Assert.Contains("Réglages (settings.json) : 2 valeur(s) retombée(s) sur leur défaut — CadranStyle, SessionStyle", report);
        var magasins = report[report.IndexOf("[Magasins persistants]")..report.IndexOf("[Journal d'historique]")];
        Assert.Contains("Réglages (settings.json)", magasins);
    }

    [Fact]
    public async Task Rapport_signale_des_reglages_illisibles()
    {
        var report = await RapportAvecSettings("{\"ThemeKey\":\"nord\"");

        Assert.Contains("Réglages (settings.json) : illisible — original mis en quarantaine à la première écriture, défauts", report);
    }

    // 42.2-02 (MAT-4 a) — le diagnostic dit la lecture de DÉMARRAGE, les lectures non abouties, la quarantaine et le blocage.

    private static (ChronosPaths paths, SettingsService settings, DiagnosticService diag) MontageReglages42(string? json)
    {
        var paths = TempPaths();
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(paths.SettingsFile)!);
        if (json is not null) System.IO.File.WriteAllText(paths.SettingsFile, json);
        var settings = new SettingsService(paths);
        var diag = new DiagnosticService(paths, settings, new StubProvider(UsageSnapshot.Empty), new FakeClock(DateTimeOffset.UtcNow));
        return (paths, settings, diag);
    }

    private static string LigneReglages(string report) =>
        report.Split('\n').Single(l => l.Contains("Réglages (settings.json) :", StringComparison.Ordinal));

    [Fact]
    public async Task Rapport_dit_la_lecture_de_demarrage_et_non_la_relecture()
    {
        var (paths, settings, diag) = MontageReglages42("{\"ThemeKey\":\"nord\"");
        settings.ChargerPourDemarrage();
        System.IO.File.WriteAllText(paths.SettingsFile, "{\"ThemeKey\":\"nord\"}");   // réparé après le démarrage

        var report = await diag.BuildReportAsync();

        Assert.Contains("illisible", LigneReglages(report));
        Assert.Contains("lectures non abouties depuis le démarrage : 1", report);
    }

    [Fact]
    public async Task Rapport_sans_lecture_de_demarrage_dit_la_relecture_fraiche()
    {
        var (_, _, diag) = MontageReglages42("{\"ThemeKey\":\"nord\"}");

        var report = await diag.BuildReportAsync();

        Assert.Contains("lus, aucune valeur retombée", LigneReglages(report));
        Assert.DoesNotContain("lectures non abouties", report);
        Assert.DoesNotContain("QUARANTAINE", report);
        Assert.DoesNotContain("ÉCRITURE BLOQUÉE", report);
    }

    [Fact]
    public async Task Rapport_nomme_la_quarantaine_apres_un_Modifier_sur_fichier_illisible()
    {
        var (_, settings, diag) = MontageReglages42("{\"ThemeKey\":\"nord\"");
        settings.Modifier(s => s with { ThemeKey = "rose" });

        var report = await diag.BuildReportAsync();

        Assert.Contains("QUARANTAINE", report);
        Assert.Matches(@"settings\.illisible-\d{8}-\d{6}(-\d+)?\.json", report);
    }

    [Fact]
    public async Task Rapport_signale_l_ecriture_bloquee_et_sa_cause()
    {
        var (paths, settings, diag) = MontageReglages42("{\"ThemeKey\":\"nord\"");
        using (new System.IO.FileStream(paths.SettingsFile, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
            settings.Modifier(s => s with { ThemeKey = "rose" });   // quarantaine impossible

        var report = await diag.BuildReportAsync();

        Assert.Contains("ÉCRITURE BLOQUÉE : quarantaine impossible", report);
    }

    [Fact]
    public async Task Rapport_libelle_une_lecture_inaccessible()
    {
        var (paths, settings, diag) = MontageReglages42("{\"ThemeKey\":\"nord\"}");
        using (new System.IO.FileStream(paths.SettingsFile, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.None))
            settings.ChargerPourDemarrage();

        var report = await diag.BuildReportAsync();

        Assert.Contains("inaccessible (E/S) — défauts en mémoire, aucune écriture tant qu'elle dure", LigneReglages(report));
    }

    [Fact]
    public async Task Rapport_dit_que_les_reglages_sont_lus_sans_retombee()
    {
        var report = await RapportAvecSettings("{\"ThemeKey\":\"nord\"}");

        Assert.Contains("Réglages (settings.json) : lus, aucune valeur retombée", report);
    }
    // ---------------------------------------------------------------- DAT-03 (37-05) : bilan du retrait de la barre de statut

    /// <summary>
    /// Le rapport journalise le bilan du retrait : sur un fichier témoin portant une barre Chronos (version et chemin quelconques),
    /// le réconciliateur — construit avec les TROIS chemins temporaires, jamais le vrai profil — sauvegarde puis retire la barre,
    /// et la section « [Réglages de Claude Code] » le dit, sauvegarde nommée. La ligne « Barre de statut actuelle » relit le
    /// fichier témoin (et non le vrai settings.json) : la barre y est désormais absente.
    /// </summary>
    [Fact]
    public async Task Le_rapport_journalise_le_bilan_du_retrait_de_la_barre()
    {
        var dir = Path.Combine(Path.GetTempPath(), "Chronos_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "settings.json"),
                "{\"statusLine\":{\"type\":\"command\",\"command\":\"\\\"C:/X/Chronos-v3.4.0.exe\\\" --statusline\"}}");
            var reconciler = new ClaudeSettingsReconciler(
                Path.Combine(dir, "settings.json"), Path.Combine(dir, "backups"), Path.Combine(dir, "Chronos.exe"));
            Assert.StartsWith(Path.GetTempPath(), reconciler.SettingsPath);
            Assert.StartsWith(Path.GetTempPath(), reconciler.BackupDir);

            Assert.True(reconciler.Reconcile(false));

            var paths = TempPaths();
            var settings = new SettingsService(paths);
            var diag = new DiagnosticService(paths, settings, new StubProvider(UsageSnapshot.Empty),
                                             new FakeClock(DateTimeOffset.UtcNow), reglagesClaude: reconciler);
            var report = await diag.BuildReportAsync();

            Assert.Contains("[Réglages de Claude Code]", report);
            Assert.Contains("barre de statut Chronos retirée — sauvegarde claude-settings-", report);
            Assert.Contains("Barre de statut actuelle : absente", report);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { /* nettoyage best-effort */ } }
    }

    /// <summary>Sans réconciliateur injecté, la section existe et dit « non câblé » — jamais un bilan inventé.</summary>
    [Fact]
    public async Task Sans_reconciliateur_le_rapport_dit_non_cable()
    {
        var paths = TempPaths();
        var settings = new SettingsService(paths);
        var diag = new DiagnosticService(paths, settings, new StubProvider(UsageSnapshot.Empty), new FakeClock(DateTimeOffset.UtcNow));

        var report = await diag.BuildReportAsync();

        Assert.Contains("[Réglages de Claude Code]", report);
        Assert.Contains("  Ce lancement : non câblé", report);
    }

    // ---------------------------------------------------------------- 42.2-07 : marqueur de quarantaine et MAT-5

    /// <summary>Arbitrage ZEUS : tant que le marqueur de quarantaine des réglages est posé, le rapport dit que les hooks sont
    /// conservés. Source = le réglage lui-même (settings.json témoin temporaire), vraie même sans réconciliation.</summary>
    [Fact]
    public async Task Le_rapport_dit_hooks_conserves_apres_quarantaine_tant_que_le_marqueur_est_pose()
    {
        var paths = TempPaths();
        Assert.StartsWith(Path.GetTempPath(), paths.SettingsFile);
        var t = new DateTimeOffset(2026, 10, 4, 8, 30, 0, TimeSpan.Zero);
        File.WriteAllText(paths.SettingsFile, "{\"QuarantaineReglagesDepuis\":\"2026-10-04T08:30:00+00:00\"}");
        var diag = new DiagnosticService(paths, new SettingsService(paths), new StubProvider(UsageSnapshot.Empty), new FakeClock(DateTimeOffset.UtcNow));

        var report = await diag.BuildReportAsync();

        Assert.Contains("  Hooks conservés après quarantaine des réglages (depuis " + t.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + ")", report);
    }

    [Fact]
    public async Task Sans_marqueur_le_rapport_ne_dit_pas_hooks_conserves()
    {
        var paths = TempPaths();
        Assert.StartsWith(Path.GetTempPath(), paths.SettingsFile);
        File.WriteAllText(paths.SettingsFile, "{\"ThemeKey\":\"Nuit\"}");
        var diag = new DiagnosticService(paths, new SettingsService(paths), new StubProvider(UsageSnapshot.Empty), new FakeClock(DateTimeOffset.UtcNow));

        var report = await diag.BuildReportAsync();

        Assert.DoesNotContain("Hooks conservés après quarantaine", report);
        Assert.DoesNotContain("Hooks laissés tels quels", report);
    }

    /// <summary>MAT-5 : une réconciliation passée sans volonté connue (réglages Chronos illisibles au démarrage) est dite.</summary>
    [Fact]
    public async Task Le_rapport_dit_hooks_laisses_tels_quels_quand_les_reglages_etaient_illisibles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "Chronos_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "settings.json"), "{\"model\":\"opus\"}");
            var reconciler = new ClaudeSettingsReconciler(
                Path.Combine(dir, "settings.json"), Path.Combine(dir, "backups"), Path.Combine(dir, "Chronos.exe"));
            Assert.StartsWith(Path.GetTempPath(), reconciler.SettingsPath);
            Assert.StartsWith(Path.GetTempPath(), reconciler.BackupDir);
            reconciler.Reconcile(null);

            var paths = TempPaths();
            var diag = new DiagnosticService(paths, new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                             new FakeClock(DateTimeOffset.UtcNow), reglagesClaude: reconciler);
            var report = await diag.BuildReportAsync();

            Assert.Contains("  Hooks laissés tels quels : réglages Chronos illisibles au démarrage", report);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { /* nettoyage best-effort */ } }
    }
    // ---------------------------------------------------------------- 42.2-09 : MAINT-5, les 8 hooks depuis la source unique

    /// <summary>Extrait la ligne « Hooks --hook installés : » de la section du widget.</summary>
    private static string LigneHooksInstalles(string report)
        => report.Split('\n').Single(l => l.TrimStart().StartsWith("Hooks --hook installés : "));

    /// <summary>MAINT-5 : sur le settings.json du réconciliateur injecté (fichier témoin temporaire portant les 8 groupes Chronos),
    /// la ligne « Hooks --hook installés » cite CHACUN des événements de <see cref="SessionHookInstaller.Events"/> — dont les trois
    /// que l'ancienne liste en dur taisait (PermissionRequest, PreToolUse, PostToolUse).</summary>
    [Fact]
    public async Task Le_rapport_liste_les_8_hooks_caables_depuis_SessionHookInstaller_Events()
    {
        var dir = Path.Combine(Path.GetTempPath(), "Chronos_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texte = SessionHookInstaller.TransformForInstall(null, "C:/Outils/Chronos-v3.5.0.exe");
            Assert.NotNull(texte);
            File.WriteAllText(Path.Combine(dir, "settings.json"), texte);
            var reconciler = new ClaudeSettingsReconciler(
                Path.Combine(dir, "settings.json"), Path.Combine(dir, "backups"), Path.Combine(dir, "Chronos.exe"));
            Assert.StartsWith(Path.GetTempPath(), reconciler.SettingsPath);

            var paths = TempPaths();
            var diag = new DiagnosticService(paths, new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                             new FakeClock(DateTimeOffset.UtcNow), reglagesClaude: reconciler);
            var ligne = LigneHooksInstalles(await diag.BuildReportAsync());

            Assert.Equal(8, SessionHookInstaller.Events.Length);
            foreach (var ev in SessionHookInstaller.Events) Assert.Contains(ev, ligne);
            Assert.Contains("PermissionRequest", ligne);
            Assert.Contains("PreToolUse", ligne);
            Assert.Contains("PostToolUse", ligne);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { /* nettoyage best-effort */ } }
    }

    /// <summary>MAINT-5 : un settings.json témoin sans hooks donne « AUCUN » — et c'est bien le fichier du réconciliateur qui est
    /// lu, jamais le vrai ~/.claude.</summary>
    [Fact]
    public async Task Sans_hooks_dans_le_fichier_du_reconciliateur_le_rapport_dit_AUCUN()
    {
        var dir = Path.Combine(Path.GetTempPath(), "Chronos_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "settings.json"), "{\"model\":\"opus\"}");
            var reconciler = new ClaudeSettingsReconciler(
                Path.Combine(dir, "settings.json"), Path.Combine(dir, "backups"), Path.Combine(dir, "Chronos.exe"));

            var paths = TempPaths();
            var diag = new DiagnosticService(paths, new SettingsService(paths), new StubProvider(UsageSnapshot.Empty),
                                             new FakeClock(DateTimeOffset.UtcNow), reglagesClaude: reconciler);
            var ligne = LigneHooksInstalles(await diag.BuildReportAsync());

            Assert.Equal("Hooks --hook installés : AUCUN", ligne.Trim());
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { /* nettoyage best-effort */ } }
    }

    /// <summary>MAINT-5 (garde de source) : plus aucune liste d'événements tenue en dur dans le diagnostic.</summary>
    [Fact]
    public void Le_diagnostic_ne_tient_plus_sa_propre_liste_d_evenements()
    {
        var dir = AppContext.BaseDirectory;
        string? source = null;
        while (dir is not null)
        {
            var c = Path.Combine(dir, "src", "Chronos", "Services", "DiagnosticService.cs");
            if (File.Exists(c)) { source = File.ReadAllText(c); break; }
            dir = Path.GetDirectoryName(dir);
        }
        Assert.NotNull(source);
        Assert.DoesNotContain("new[] { \"Notification\", \"Stop\"", source);
        Assert.Contains("SessionHookInstaller.Events", source);
    }
}
