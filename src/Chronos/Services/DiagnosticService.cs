using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Chronos.Models;
using Chronos.Models.Historique.Tokens;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;
using Chronos.Text;

namespace Chronos.Services;

/// <summary>
/// Diagnostic auto-explicatif (observabilité pour un outil distribué) : dit POURQUOI l'affichage
/// n'a pas de couleurs sur une machine donnée. Rassemble l'état réel — la chaîne de données (sonde
/// d'en-têtes, secours OAuth du login Chronos, dernier exact persisté, journal), les magasins, la
/// source active par fenêtre — et écrit un rapport lisible dans %APPDATA%/Chronos/diagnostic.txt,
/// qu'il ouvre ensuite. Phase 37 : aucun appel réseau, aucune recherche de coffres ni de dossiers.
///
/// SÉCURITÉ : le token n'est JAMAIS écrit dans le rapport (seulement « trouvé : oui/non »). Neutre
/// (aucun type WPF) : ouvre le fichier via l'application par défaut du système (Process.Start).
/// </summary>
public sealed class DiagnosticService
{
    // Combien de fichiers d'état le rapport détaille. Une borne de LISIBILITÉ, pas une troncature muette :
    // le reste est annoncé sur une ligne dédiée, et le compte total du dossier est donné avant la liste.
    private const int MaxFichiersEtat = 8;

    private readonly ChronosPaths _paths;
    private readonly SettingsService _settings;
    private readonly IUsageProvider _composite;
    private readonly IClock _clock;
    private readonly IAuthStatus? _authStatus;
    private readonly IEtatServeur? _etatServeur;
    private readonly SessionMonitor? _moniteurSessions;
    private readonly IReadOnlyList<IEtatMagasin>? _magasins;
    private readonly IEtatReconstruction? _reconstruction;
    private readonly TimeZoneInfo? _fuseau;
    private readonly ClaudeSettingsReconciler? _reglagesClaude;
    private readonly DateTimeOffset _demarrage;

    /// <param name="authStatus">État d'authentification réel (autorité de jeton). OPTIONNEL et en
    /// dernière position à dessein : les 8 sites de construction existants (1 en production, 7 en
    /// tests) compilent sans retouche, et la DI passe le vrai service.</param>
    /// <param name="etatServeur">Canal latéral de la sonde d'en-têtes (HDR-03/HDR-04) et son issue.
    /// OPTIONNEL et en DERNIÈRE position à dessein : les 10 sites de construction préexistants (1 en
    /// production, 9 en tests) compilent sans retouche. Précédent : authStatus, phase 17.</param>
    /// <param name="moniteurSessions">OBS-01 — LE moniteur du widget, partagé par le conteneur DI, jamais
    /// un second exemplaire. Le rapport décrivait jusqu'ici un moniteur fabriqué ici même, donc nu : sans le
    /// magasin d'archives de l'application, sans le filtre « traité », sans le détecteur d'hystérésis. Il
    /// décrivait un jumeau imaginaire — et c'est très probablement ce qui a rendu le défaut du widget
    /// inélucidable, l'utilisateur lisant dans le RAPPORT des sessions que le widget ne montrait pas.
    ///
    /// <para>Le partage d'instance n'est pas un détail d'implémentation : il est la raison pour laquelle le
    /// rapport suivra chaque changement futur du câblage du widget sans qu'une ligne de ce fichier ne
    /// change. Une copie du comportement du widget rouvrirait l'écart le lendemain.</para>
    ///
    /// <para>AUCUN REPLI : nul n'est absent, le rapport dit qu'il n'a rien observé. Fabriquer un moniteur
    /// de secours ici, c'est exactement le défaut corrigé. OPTIONNEL et en DERNIÈRE position à dessein :
    /// les sites de construction préexistants compilent sans retouche. Précédents : authStatus (17),
    /// etatServeur (18).</para></param>
    /// <param name="magasins">CPT-02 — état des magasins persistants (dernier exact, journal des relevés) tel que le
    /// processus le connaît : âge de la dernière écriture, dernière erreur. OPTIONNEL et en DERNIÈRE position à
    /// dessein : les sites de construction préexistants compilent sans retouche. Précédents : authStatus (17),
    /// etatServeur (18), moniteurSessions (26). Repli : les faits disque seuls (existence, taille,
    /// mtime) — le rapport reste utile sans câblage DI, mais ne peut alors pas dire POURQUOI une écriture a raté.</param>
    /// <param name="demarrageProcessus">JRN-04 — instant de démarrage du processus. D-32-21 : l'alerte « journal muet » se
    /// mesure depuis max(démarrage, dernière écriture) ; mesurée depuis la seule dernière écriture, elle s'allumerait à chaque
    /// lancement sur l'écriture de la veille, ce qui n'est pas « muet alors que Chronos tourne ». OPTIONNEL et en DERNIÈRE
    /// position à dessein ; repli = l'instant de construction du rapport (≈ démarrage, le diagnostic étant un singleton
    /// construit au lancement). Les tests injectent un démarrage ancien ou récent pour épingler la règle.</param>
    /// <param name="reconstruction">TOK-02 — progression et état de la reconstruction des agrégats de tokens (phase, N / M fichiers,
    /// dernier fichier, dernière erreur), en ENTIERS. OPTIONNEL et en DERNIÈRE position à dessein (même protocole que les précédents) ;
    /// null dans les tests qui ne s'y intéressent pas : la ligne du magasin des agrégats et le périmètre se disent quand même.</param>
    /// <param name="fuseau">ACC-03 / D-35-12 — le fuseau des heures de la section « Journal d'historique » (bornes du jour, événements,
    /// fin de reconstruction), celui de la fenêtre Historique. OPTIONNEL et en DERNIÈRE position à dessein (même protocole que les
    /// précédents) ; câblé par la racine de composition (35-05). <c>null</c> → la section parle en UTC et le DIT (« Fuseau : UTC
    /// (fuseau non injecté) ») : jamais le fuseau local de la machine deviné ici (décision 5 de la phase 35).</param>
    /// <param name="reglagesClaude">DAT-03 — le réconciliateur de <c>~/.claude/settings.json</c>, la MÊME instance que celle appelée au
    /// démarrage : son <see cref="ClaudeSettingsReconciler.DernierBilan"/> remplit la section « Réglages de Claude Code » (retrait de
    /// la barre de statut, sauvegarde). OPTIONNEL et en DERNIÈRE position à dessein (même protocole que les précédents) ; <c>null</c> →
    /// la section le dit (« non câblé »).</param>
    public DiagnosticService(ChronosPaths paths, SettingsService settings, IUsageProvider composite, IClock clock,
                             IAuthStatus? authStatus = null, IEtatServeur? etatServeur = null,
                             SessionMonitor? moniteurSessions = null,
                             IReadOnlyList<IEtatMagasin>? magasins = null,
                             DateTimeOffset? demarrageProcessus = null,
                             IEtatReconstruction? reconstruction = null,
                             TimeZoneInfo? fuseau = null,
                             ClaudeSettingsReconciler? reglagesClaude = null)
    {
        _paths = paths;
        _settings = settings;
        _composite = composite;
        _clock = clock;
        _authStatus = authStatus;
        _etatServeur = etatServeur;
        _moniteurSessions = moniteurSessions;   // pas de repli : voir le XML-doc ci-dessus
        _magasins = magasins;
        _demarrage = demarrageProcessus ?? clock.UtcNow;   // D-32-21 : référence basse de « journal muet »
        _reconstruction = reconstruction;
        _fuseau = fuseau;   // pas de repli local : voir le XML-doc (D-35-12)
        _reglagesClaude = reglagesClaude;   // pas de repli : voir le XML-doc (DAT-03)
    }

    /// <summary>Écrit le rapport dans %APPDATA%/Chronos/chronos.log AU DÉMARRAGE, SANS l'ouvrir
    /// (log automatique et silencieux). Toute erreur est absorbée : ne doit jamais empêcher le lancement.</summary>
    public async Task LogStartupAsync(CancellationToken ct = default)
    {
        try
        {
            var report = await BuildReportAsync(ct);
            var dir = Path.GetDirectoryName(_paths.SettingsFile)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "chronos.log"), "(log automatique au démarrage)\n" + report);
        }
        catch { /* le log ne doit jamais casser le démarrage */ }
    }

    /// <summary>Construit le rapport, l'écrit sur disque et l'ouvre. Toute erreur est absorbée
    /// (un diagnostic ne doit jamais planter l'app).</summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        string report;
        try { report = await BuildReportAsync(ct); }
        catch (Exception ex) { report = "Le diagnostic a rencontré une erreur : " + ex.Message; }

        try
        {
            var dir = Path.GetDirectoryName(_paths.SettingsFile)!;
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "diagnostic.txt");
            File.WriteAllText(file, report);
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true }); // ouvre avec l'éditeur par défaut
        }
        catch { /* si l'ouverture échoue, tant pis : le fichier est écrit */ }
    }

    /// <summary>Rapport textuel (testable). N'expose JAMAIS le token.</summary>
    public async Task<string> BuildReportAsync(CancellationToken ct = default)
    {
        // DAT-04 — durée de CALCUL du rapport (Stopwatch, jamais IClock) : dite en dernière ligne, elle se consigne
        // d'elle-même dans chronos.log à chaque lancement (la recherche des coffres coûtait ≈ 17 s avant la phase 37).
        var chrono = Stopwatch.StartNew();
        var s = _settings.Load();
        // SOC-01 — relevé AVANT tout await : aucune autre lecture ne peut s'intercaler et écraser ce rapport.
        var lectureReglages = _settings.DerniereLecture;

        // ORDRE CRITIQUE — interroger la chaîne AVANT de rendre la moindre section.
        // C'est cet appel qui déclenche la première sonde et peuple l'état serveur. Le laisser à sa
        // place naturelle, dans la section « Ce qui est affiché maintenant », faisait décrire au rapport
        // un état antérieur à sa propre exécution : la section « sonde » annonçait « pas encore sondé »
        // et « aucun en-tête reconnu » trois lignes au-dessus des chiffres que cette même sonde venait
        // de fournir (constaté en production le 2026-09-12). L'ordre des SECTIONS ne change pas ; seul
        // l'instant de l'appel change.
        UsageSnapshot? affiche = null;
        string? echecLecture = null;
        try { affiche = await _composite.GetAsync(ct); }
        catch (Exception ex) { echecLecture = ex.Message; }
        long msChaine = chrono.ElapsedMilliseconds;

        var sb = new StringBuilder();
        sb.AppendLine("=== Chronos — Diagnostic ===");
        sb.AppendLine("Date : " + _clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Version : " + VersionEmbarquee());
        sb.AppendLine();

        // 1) DAT-04 — LA CHAÎNE DE DONNÉES, dans son ordre réel. Rien ici n'appelle le réseau ni ne cherche sur disque :
        // la section lit l'état déjà connu (canal latéral de la sonde, autorité de jeton, état des magasins), peuplé par
        // le SEUL appel au composite fait en tête de méthode. Les lignes de la sonde et du secours sont celles des
        // anciennes sections « Source exacte », mot pour mot (des tests les assertent).
        sb.AppendLine("[Chaîne de données]");
        sb.AppendLine("  Chaîne : sonde d'en-têtes → secours OAuth du login Chronos (meilleure source PAR FENÊTRE) → journal → dernier exact → cadran");

        // 1a) LA SONDE D'EN-TÊTES (HDR-01/HDR-02/HDR-06) : la PREMIÈRE source de la chaîne exacte, et la SEULE qui
        // réponde encore quand l'API refuse. La famille d'en-têtes « unified » n'est documentée NULLE PART chez
        // Anthropic, donc seul un rapport de terrain peut dire si elle existe toujours sous ce nom.
        sb.AppendLine("  Sonde d'en-têtes :");
        if (!s.SondeEnTetesActivee)
            sb.AppendLine("    Interrupteur : désactivée (menu clic droit → Réglages) — aucune requête, aucun coût");
        else
        {
            // Cadence et coût DÉRIVÉS de la constante du provider, jamais recopiés : un chiffre recopié
            // diverge le jour où la cadence change, et ce rapport deviendrait un mensonge poli.
            var minutes = (int)RateLimitHeaderUsageProvider.CadenceNominale.TotalMinutes;
            var parJour = (int)(TimeSpan.FromDays(1).TotalSeconds
                                / RateLimitHeaderUsageProvider.CadenceNominale.TotalSeconds);
            sb.AppendLine($"    Interrupteur : ACTIVÉE — une micro-requête sur ton compte toutes les {minutes} min (≈ {parJour}/jour)");
            sb.AppendLine("    Dernière sonde : " + LibelleSonde(_etatServeur?.DernierResultat));

            // SÉCURITÉ : ces noms proviennent des CONSTANTES de la sonde, jamais du serveur (invariant
            // prouvé au plan 18-04, test en casse mélangée). Les NOMS, et JAMAIS leurs valeurs : une
            // valeur d'en-tête venue du réseau ne doit pas pouvoir se réinjecter dans un affichage.
            var noms = _etatServeur?.NomsEnTetesRecus ?? Array.Empty<string>();
            sb.AppendLine("    En-têtes « unified » reconnus : " + (noms.Count == 0
                ? "AUCUN — la famille « unified » n'est documentée nulle part chez Anthropic et peut avoir changé de nom"
                : string.Join(", ", noms) + $" ({noms.Count})"));

            // HDR-04 — canal LATÉRAL : ce dépassement survit même quand Best() a écarté la fenêtre qui le
            // portait. Pourcentage par le point unique de normalisation (garde NormalisationUniqueTests).
            var dep = _etatServeur?.Depassement;
            // TROIS cas, et non deux. Un statut SEUL n'est pas un dépassement : c'est la POLITIQUE du
            // compte à son sujet. Les confondre produisait « Dépassement :  · serveur : REJETÉ » sur un
            // compte à 23 % dont les deux fenêtres disaient « autorisé » — séparateur orphelin ET
            // contresens alarmant (constaté en production le 2026-09-12).
            sb.AppendLine("    Dépassement : " + (dep is null || !dep.EstRenseigne
                ? "aucun dépassement rapporté"
                : !dep.EstEnCours
                    ? "aucun dépassement en cours · politique du compte : " + LibellePolitiqueDepassement(dep.Statut)
                    : UsageNormalization.PourcentagePourAffichage(dep.Utilization)
                      + (dep.ResetsAt is { } dr ? " (reset le " + dr.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + ")" : "")
                      + " · serveur : " + LibelleStatutServeur(dep.Statut)));
        }

        // 1b) Le SECOURS : login OAuth intégré de Chronos (coffre chiffré oauth.dat). Le composite ne le retient, PAR
        // FENÊTRE, que s'il est strictement plus fiable que la sonde.
        sb.AppendLine("  Secours OAuth (login Chronos) — utilisé seulement quand la sonde n'a pas de chiffre :");
        var oauthDat = Path.Combine(Path.GetDirectoryName(_paths.UsageFile)!, "oauth.dat");
        sb.AppendLine("    Connecté : " + (File.Exists(oauthDat)
            ? "OUI (jeton chiffré présent) — les chiffres exacts arrivent au prochain rafraîchissement"
            : "non (menu clic droit → « Se connecter à Claude »)"));
        // TOK-02 : « le fichier existe » n'a JAMAIS voulu dire « authentifié ». Le jeton de cette
        // machine a expiré le 2026-07-12 alors que oauth.dat était bien présent : c'est exactement le
        // silence que la phase 17 brise. On affiche donc l'état RÉEL, pas la présence d'un fichier.
        sb.AppendLine("    État d'authentification : " + LibelleAuth(_authStatus?.Etat));

        // 1c) La TÊTE : le dernier relevé exact persisté et la doctrine de fraîcheur. Âge connu du processus si le magasin
        // est injecté, sinon faits disque ; limite d'âge DÉRIVÉE de DoctrineFraicheur, jamais recopiée.
        sb.AppendLine("  Dernier exact persisté : " + AgeDernierExact() + " (détail sous [Magasins persistants])");
        sb.AppendLine($"    Fraîcheur : exact jusqu'à {(int)DoctrineFraicheur.LimiteAge.TotalSeconds} s après le relevé, puis « encore valide »"
                      + " tant que Claude Code n'a pas travaillé ; sinon plancher « ≥ X % » (seul chiffre non exact)");

        // 1d) Le JOURNAL : âge de sa dernière écriture (état du magasin, jamais une relecture de sa queue) et la même
        // alerte « journal muet » que [Magasins persistants], par la même aide.
        var etatJournal = _magasins?.FirstOrDefault(m => m.Nom == NomsMagasins.JournalReleves);
        sb.AppendLine("  Journal : " + (etatJournal is null
            ? "non câblé (état du journal non injecté) — détail sous [Magasins persistants]"
            : (etatJournal.DerniereEcriture is { } dj ? "dernière écriture " + LibelleSource.Anciennete(dj, _clock.UtcNow) : "aucune écriture depuis le démarrage")
              + (AlerteJournalMuet(etatJournal) is { } alerteChaine ? " — " + alerteChaine : "")));
        sb.AppendLine();

        // 3) Transcripts JSONL — désormais source de DELTA (aucun plafond, aucun pourcentage)
        sb.AppendLine("[Transcripts JSONL (source de delta)]");
        var projects = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
        int jsonl = 0;
        try { if (Directory.Exists(projects)) jsonl = Directory.EnumerateFiles(projects, "*.jsonl", SearchOption.AllDirectories).Take(5000).Count(); } catch { }
        sb.AppendLine("  Dossier ~/.claude/projects : " + (Directory.Exists(projects) ? jsonl + " fichier(s) .jsonl" : "ABSENT (aucun historique local)"));
        sb.AppendLine();

        // 3b) CPT-02 — magasins persistants : où l'on écrit, quand, et depuis quelle VUE d'AppData on regarde.
        // La ligne « Vue AppData » d'abord : lue depuis une session Claude Code, cette section décrit la copie
        // virtualisée du paquet MSIX, pas les fichiers de l'overlay — c'est ce qui a fait croire à un « gel ».
        // D-35-11 — la table des processus est relevée UNE fois par rapport : la liste détaillée de [Magasins persistants] et le
        // compte de la section « Journal d'historique » viennent du même relevé (une seconde lecture pourrait compter un hook de plus ou de moins).
        IReadOnlyList<ProcessusChronos>? processus = null;
        Exception? echecProcessus = null;
        try { processus = InventaireProcessus.Relever(); }
        catch (Exception ex) { echecProcessus = ex; }

        sb.AppendLine("[Magasins persistants]");
        sb.AppendLine("  Réglages (settings.json) : " + LibelleLectureReglages(lectureReglages));
        sb.AppendLine("  Vue AppData : " + DetecteurVueAppData.Libelle(
            DetecteurVueAppData.Detecter(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))));
        sb.AppendLine("  " + LigneMagasin(NomsMagasins.DernierExact, _paths.LastExactFile, "fichier"));
        var fichierDuMois = Path.Combine(_paths.HistoriqueDir, "releves-" + _clock.UtcNow.UtcDateTime.ToString("yyyy-MM") + ".jsonl");
        sb.AppendLine("  " + LigneMagasin(NomsMagasins.JournalReleves, fichierDuMois,
            Directory.Exists(_paths.HistoriqueDir) ? "fichier du mois" : "dossier"));

        // JRN-04 — le journal qui se tait doit être VU se taire. Mêmes mots que les réglages (D-32-22), un seul
        // libellé dans ce fichier. Référence = max(démarrage, dernière écriture) (D-32-21) : un processus lancé il y a 5 min n'est pas muet
        // parce que la dernière écriture date de la veille. Seuil dérivé de la cadence de la sonde, jamais 900 s en dur.
        if (AlerteJournalMuet(_magasins?.FirstOrDefault(m => m.Nom == NomsMagasins.JournalReleves)) is { } alerte)
            sb.AppendLine("    " + alerte);

        // CPT-03 — combien de Chronos tournent, et qui tient le verrou mono-instance. Le relevé lit la table des processus de
        // la machine : sous try/catch, un relevé impossible se DIT, il ne fait jamais échouer le rapport.
        try
        {
            if (processus is null) throw echecProcessus ?? new InvalidOperationException("relevé absent");
            foreach (var l in InventaireProcessus.Lignes(processus, Environment.ProcessId, _clock.UtcNow,
                                                         VerrouInstanceUnique.EtatPourDiagnostic(VerrouInstanceUnique.NomOverlay)))
                sb.AppendLine("  " + l);
        }
        catch (Exception ex)
        {
            sb.AppendLine("  Processus Chronos : relevé impossible (" + ex.GetType().Name + " : " + ex.Message + ")");
        }
        // TOK-01/CPT-02 — troisième magasin : les agrégats de tokens (fichier du mois UTC courant), même moule que le journal.
        var fichierTokensDuMois = Path.Combine(_paths.HistoriqueDir, MagasinAgregats.NomFichier(_clock.UtcNow));
        sb.AppendLine("  " + LigneMagasin(NomsMagasins.AgregatsTokens, fichierTokensDuMois,
            Directory.Exists(_paths.HistoriqueDir) ? "fichier du mois" : "dossier"));
        // TOK-02 — où en est la reconstruction, en ENTIERS (jamais une fraction : la barre est l'affaire de la fenêtre Historique), et le
        // PÉRIMÈTRE mot pour mot (D-33-23) : ces tokens sont un comptage local partiel, jamais un pourcentage du forfait.
        if (_reconstruction is { } rec)
        {
            sb.AppendLine("    Reconstruction : " + LibellePhase(rec.Phase) + " — " + rec.FichiersTraites + " / " + rec.FichiersTotal + " fichiers"
                          + " · semaine courante : " + (rec.SemaineCouranteDisponible ? "complète" : "en cours")
                          + (rec.DernierFichier is { } dernier ? " · dernier fichier : " + dernier : ""));
            sb.AppendLine("    Fichiers disparus : " + rec.FichiersDisparus + " · lignes ignorées : " + rec.LignesIgnorees + " · ids connus : " + rec.IdsConnus);
            if (rec.DerniereErreur is { } erreurRec) sb.AppendLine("    ÉCHEC : " + erreurRec);
        }
        sb.AppendLine("    Périmètre : " + LigneAgregat.Perimetre);
        sb.AppendLine();

        // 3c) ACC-03 — ce que le journal d'historique sait de lui-même, par la MÊME lecture que la fenêtre (D-35-10).
        SectionJournalHistorique(sb, processus?.Count);

        // 4) Résultat effectivement affiché (via le composite réel)
        sb.AppendLine("[Ce qui est affiché maintenant]");
        // Consomme le snapshot obtenu EN TÊTE de la méthode — ne relance pas la chaîne, sans quoi la
        // sonde serait comptée deux fois et le rapport coûterait deux micro-requêtes au lieu d'une.
        if (affiche is { } snap)
        {
            sb.AppendLine("  5 h   : " + Describe(snap.FiveHour));
            sb.AppendLine("  Hebdo : " + Describe(snap.SevenDay));
        }
        else
            sb.AppendLine("  (échec de lecture : " + echecLecture + ")");
        sb.AppendLine();

        // 4b) Widget de sessions Claude Code (hooks + fichiers d'état)
        sb.AppendLine("[Widget sessions Claude Code]");
        sb.AppendLine("  Activé (réglage) : " + (s.SessionsWidgetEnabled ? "OUI" : "non"));
        sb.AppendLine("  Exe courant : " + (Environment.ProcessPath ?? "?"));

        // Hooks --hook présents dans ~/.claude/settings.json ? (+ chemin exe référencé)
        var claudeSettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
        try
        {
            if (File.Exists(claudeSettings))
            {
                using var sd = JsonDocument.Parse(File.ReadAllText(claudeSettings));
                var present = new List<string>();
                string? hookExe = null;
                if (sd.RootElement.TryGetProperty("hooks", out var hks) && hks.ValueKind == JsonValueKind.Object)
                {
                    foreach (var ev in new[] { "Notification", "Stop", "UserPromptSubmit", "SessionStart", "SessionEnd" })
                    {
                        if (!hks.TryGetProperty(ev, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                        foreach (var grp in arr.EnumerateArray())
                        {
                            if (!grp.TryGetProperty("hooks", out var hs) || hs.ValueKind != JsonValueKind.Array) continue;
                            foreach (var h in hs.EnumerateArray())
                            {
                                var cmd = h.TryGetProperty("command", out var cc) && cc.ValueKind == JsonValueKind.String ? cc.GetString() : null;
                                if (cmd is null || !cmd.Contains("--hook")) continue;
                                present.Add(ev);
                                hookExe ??= cmd;
                            }
                        }
                    }
                }
                sb.AppendLine("  Hooks --hook installés : " + (present.Count > 0 ? string.Join(", ", present.Distinct()) : "AUCUN"));
                if (hookExe is not null) sb.AppendLine("  Commande hook : " + hookExe);
            }
            else sb.AppendLine("  settings.json Claude absent");
        }
        catch { sb.AppendLine("  (lecture settings.json impossible)"); }

        // Fichiers d'état écrits par le mode --hook. OBS-02 — les fichiers PERTINENTS : ce qui attend
        // d'abord, puis le plus récent. L'ancien tri était celui de Directory.GetFiles, c'est-à-dire
        // l'ordre alphabétique des UUID. Sur la machine mesurée le 2026-09-12 (54 fichiers, dont 48 de
        // plus de sept jours), les huit retenus étaient tous vieux de plusieurs semaines : un échantillon
        // tiré par le hasard d'un nom de fichier, présenté comme un état des lieux. C'est cette liste-là
        // que l'utilisateur lisait quand il croyait voir des sessions mortes dans son widget.
        //
        // Les racines lues sont celles du MONITEUR quand il est injecté : lire un autre dossier que celui du
        // widget rouvrirait exactement l'écart qu'OBS-01 vient de fermer.
        //
        // APP-06 (phase 29) — le moniteur lit PLUSIEURS racines : la vue du paquet de l'app bureau, où les hooks
        // lancés sous l'app écrivent réellement, et la vue réelle d'AppData. Une ligne par racine, TOUTES avant
        // la liste fusionnée : c'est la seule façon de voir, dans le rapport de l'overlay, laquelle des deux vues
        // contient les fichiers. Une racine qui n'existe pas est ÉCRITE absente, jamais tue — sinon rien ne
        // distinguerait « cherchée et vide » de « jamais cherchée ».
        var racinesEtat = _moniteurSessions?.Dossiers ?? new[] { Path.Combine(Path.GetDirectoryName(_paths.UsageFile)!, "sessions") };
        try
        {
            var files = new List<string>();
            foreach (var racine in racinesEtat)
            {
                try
                {
                    if (!Directory.Exists(racine))
                    {
                        sb.AppendLine($"  Fichiers d'état ({racine}) : absent (dossier introuvable)");
                        continue;
                    }
                    var ici = Directory.GetFiles(racine, "*.json");
                    sb.AppendLine($"  Fichiers d'état ({racine}) : {ici.Length}");
                    files.AddRange(ici);
                }
                catch { sb.AppendLine($"  Fichiers d'état ({racine}) : illisible"); }
            }

            // Lire les 54 fichiers coûte 15,15 ms (mesure du 2026-09-12) : négligeable sur un rapport qui
            // dure des secondes, et c'est le prix d'un choix motivé plutôt que d'un tirage alphabétique.
            var lus = new List<(string Projet, string Activite, System.DateTimeOffset? Maj, int Urgence)>();
            var dernierRang = AffichageSessions.Urgence(SessionActivity.Unknown);   // repli : dernier rang de l'ordre d'écran, jamais deviné
            foreach (var f in files)
            {
                try
                {
                    using var d = JsonDocument.Parse(File.ReadAllText(f));
                    var r = d.RootElement;
                    string P(string k) => r.TryGetProperty(k, out var v) ? v.ToString() : "?";
                    // HDR-05 : plus aucune conversion d'unité locale — tout passe par UsageNormalization.
                    var maj = r.TryGetProperty("updated_at", out var ua) && ua.TryGetInt64(out var ms)
                        ? UsageNormalization.InstantDepuisEpochMillisecondes(ms)
                        : null;
                    var activite = P("activity");
                    // Le rang d'urgence vient de la couche partagée avec le widget : une activité que le
                    // moniteur ne saurait pas relire est reléguée au dernier rang plutôt que devinée.
                    var urgence = System.Enum.TryParse<SessionActivity>(activite, ignoreCase: true, out var a)
                        ? AffichageSessions.Urgence(a) : dernierRang;
                    lus.Add((P("project"), activite, maj, maj is null ? dernierRang : urgence));
                }
                catch { }   // fichier illisible : ignoré, jamais fatal au rapport
            }

            // NB : l'itérateur s'appelle « fic » (nom historique, conservé : aucun autre `e` n'est plus déclaré dans
            // cette méthode depuis la phase 37).
            foreach (var fic in lus.OrderBy(x => x.Urgence)
                                   .ThenByDescending(x => x.Maj ?? System.DateTimeOffset.MinValue)
                                   .Take(MaxFichiersEtat))
            {
                // Une date absente reste absente. La remplacer par l'instant courant ferait passer un
                // fichier muet pour un fichier frais — exactement ce que la doctrine du milestone interdit.
                var age = fic.Maj is { } maj ? "maj " + AffichageSessions.Age(_clock.UtcNow - maj) : "date inconnue";
                sb.AppendLine($"    · {fic.Projet} — {fic.Activite} ({age})");
            }
            if (lus.Count > MaxFichiersEtat)
                sb.AppendLine($"    … et {lus.Count - MaxFichiersEtat} autre(s) non listé(s) : ni en attente, ni parmi les plus récents");
            if (files.Count == 0)
                sb.AppendLine("    (aucun — les hooks n'ont encore rien écrit dans ces racines)");
        }
        catch { }

        // OBS-01 — Ce que le widget AFFICHE, lu sur LE moniteur du widget (l'instance du conteneur DI), à
        // l'instant du rapport. Ce fichier ne fabrique plus de moniteur : celui qu'il bâtissait était nu —
        // sans magasin d'archives, sans filtre « traité », sans détecteur — donc le rapport décrivait un
        // système qui ne tournait nulle part. Conséquence du partage d'instance : tout changement futur du
        // câblage du widget se reflète ici SANS qu'une ligne de ce fichier ne change.
        if (_moniteurSessions is null)
            sb.AppendLine("  Sessions (widget) : MONITEUR NON INJECTÉ — rien n'a été observé ici.");
        else
        {
            try
            {
                var lecture = _moniteurSessions.Inspecter(_clock.UtcNow);

                sb.AppendLine($"  Fichiers de hook écartés (trop anciens) : {lecture.FichiersEcartesParAnciennete}");

                // Pas de troncature : le widget n'en applique aucune, et comparer ligne à ligne un rapport
                // tronqué avec un écran complet, c'est reconstruire l'écart que ce plan ferme.
                sb.AppendLine($"  Sessions AFFICHÉES par le widget : {lecture.Visibles.Count}");
                foreach (var d in AffichageSessions.Ordonner(lecture.Visibles))
                    sb.AppendLine($"    · {Court(d.SessionId)} {AffichageSessions.Nom(d)} — {AffichageSessions.Etat(d.Activity)}"
                                + $" ({AffichageSessions.Age(_clock.UtcNow - d.UpdatedAt)})");
                if (lecture.Visibles.Count == 0)
                    sb.AppendLine("    → aucune session à l'écran. Si tu en attendais une, lis la liste des MASQUÉES juste en dessous.");

                // Le critère n°2 de la phase. Une session écartée n'est pas absente : elle est écartée PAR
                // QUELQUE CHOSE, et ce quelque chose a un nom et un fichier que l'utilisateur peut ouvrir.
                // Cas fondateur, mesuré le 2026-09-12 : e465420e, session vivante en attente de permission
                // depuis 10 h, écartée par treated.json — invisible du widget ET du rapport. Depuis la phase 30
                // (LUE-03), écartée par treated.json ET la cause que le détecteur a constatée : lue par focus, lue
                // au premier plan, répondue, ou marquée à la main (ou traitée avant le démarrage de l'overlay).
                sb.AppendLine($"  Sessions MASQUÉES par un filtre : {lecture.Masquees.Count}");
                foreach (var m in lecture.Masquees
                             .OrderBy(m => AffichageSessions.Urgence(m.Session.Activity))
                             .ThenByDescending(m => m.Session.UpdatedAt))
                    sb.AppendLine($"    · {Court(m.Session.SessionId)} {AffichageSessions.Nom(m.Session)} — {AffichageSessions.Etat(m.Session.Activity)}"
                                + $" ({AffichageSessions.Age(_clock.UtcNow - m.Session.UpdatedAt)}) — masquée par {LibelleMasquage(m.Motif, m.Cause)}");
                if (lecture.Masquees.Count == 0)
                    sb.AppendLine("    (aucune — aucun filtre n'écarte de session en ce moment)");

                // FUS-02 — un DÉSACCORD n'est pas un écartement de session : celle-ci est AFFICHÉE, juste
                // au-dessus. Ce qui a été écarté, c'est l'un de ses SIGNAUX. Les ranger avec le vocabulaire
                // de masquage rouvrirait la confusion « absente » / « écartée par tel filtre » que la
                // phase 22 a fermée, et noierait le seul cas qui compte ici : une SOURCE FIGÉE.
                // Cas fondateur, mesuré le 2026-09-12 : un fichier de hook immobile depuis 7 h annonçait
                // « à toi » pendant qu'un transcript de 10 s prouvait le contraire. Le hook gagnait, et rien
                // ne le disait. Depuis la phase 24 il perd — mais sans cette ligne, l'utilisateur n'aurait
                // toujours aucun moyen d'apprendre que sa source est morte.
                sb.AppendLine($"  Désaccords entre sources : {lecture.Desaccords.Count}");
                foreach (var d in lecture.Desaccords)
                    sb.AppendLine($"    · {Court(d.SessionId)} — retenu {LibelleSourceSession(d.SourceRetenue)}"
                                + $" « {AffichageSessions.Etat(d.EtatRetenu)} » ; écarté {LibelleSourceSession(d.SourceEcartee)}"
                                + $" « {AffichageSessions.Etat(d.EtatEcarte)} », plus ancien de {AffichageSessions.Ecart(d.EcartAge)}");
                if (lecture.Desaccords.Count == 0)
                    sb.AppendLine("    (aucun — aucune source n'en contredit une autre en ce moment)");

                // APP-04 — ce que l'app bureau sait, lu dans la MÊME lecture que le widget (OBS-01) : aucun second
                // lecteur, aucun second Inspecter. Le lecteur du moniteur n'est consulté que pour distinguer « non
                // branchée » d'une lecture qui a levé à ce cycle : il n'est jamais relu ici.
                DecrireSourceAppBureau(sb, lecture, lecteurBranche: _moniteurSessions.Lecteur is not null);

                // LUE-01, LUE-02, LUE-04 — ce que la règle « lue » voit, lu dans la MÊME lecture (OBS-01).
                DecrireRegleLue(sb, lecture);
            }
            catch (Exception ex) { sb.AppendLine("  (lecture du moniteur impossible : " + ex.GetType().Name + ")"); }
        }

        sb.AppendLine();

        // 4c) DAT-03 — ce que la réconciliation de ce lancement a fait de la barre de statut, puis ce que le fichier porte
        // MAINTENANT. Lu sur le fichier du réconciliateur injecté quand il l'est (le même que celui qu'il a réconcilié).
        sb.AppendLine("[Réglages de Claude Code]");
        sb.AppendLine("  Ce lancement : " + LibelleBilan(_reglagesClaude?.DernierBilan, _reglagesClaude is not null));
        sb.AppendLine("  Barre de statut actuelle : " + BarreActuelle(_reglagesClaude?.SettingsPath ?? claudeSettings));
        sb.AppendLine();

        // DAT-04 — la durée, en dernière ligne (format épinglé par test, aucun seuil).
        sb.AppendLine();
        sb.AppendLine($"Rapport construit en {chrono.ElapsedMilliseconds} ms (dont chaîne de données {msChaine} ms)");
        return sb.ToString();
    }

    /// <summary>DAT-03 — bilan lisible du dernier passage de la réconciliation (« non câblé », « pas encore passée », ou l'issue).</summary>
    private static string LibelleBilan(BilanReconciliation? bilan, bool cable)
    {
        if (!cable) return "non câblé";
        if (bilan is null) return "pas encore passée";
        if (bilan.Cause is not null) return bilan.Cause;
        var sauvegarde = bilan.Sauvegarde is { } chemin ? " — sauvegarde " + Path.GetFileName(chemin) : "";
        return bilan.Barre switch
        {
            IssueBarreStatut.Retiree   => "barre de statut Chronos retirée" + sauvegarde,
            IssueBarreStatut.Restauree => "barre de statut d'origine restaurée" + sauvegarde,
            IssueBarreStatut.Tierce    => "barre tierce laissée intacte",
            IssueBarreStatut.Absente   => "aucune barre Chronos (rien à retirer)",
            _                          => "issue inconnue",
        };
    }

    /// <summary>DAT-03 — la barre de statut que porte le fichier À L'INSTANT du rapport. Lecture seule, jamais une exception.</summary>
    private static string BarreActuelle(string cheminSettingsClaude)
    {
        try
        {
            if (!File.Exists(cheminSettingsClaude)) return "absente (settings.json Claude absent)";
            var racine = ClaudeSettingsJson.ParseOrNull(File.ReadAllText(cheminSettingsClaude));
            if (racine is null) return "inconnue (settings.json illisible)";
            if (!racine.ContainsKey("statusLine")) return "absente";
            var cmd = ClaudeSettingsJson.CommandOf(racine["statusLine"]);
            return ClaudeSettingsJson.IsChronosCommand(cmd, ClaudeSettingsJson.StatusLineMarker)
                ? "Chronos (retirée au prochain lancement)"
                : "tierce (laissée intacte)";
        }
        catch { return "inconnue (lecture impossible)"; }
    }

    /// <summary>DAT-04 — âge du dernier exact persisté : l'âge connu du PROCESSUS (magasin injecté) prime, sinon le mtime
    /// du fichier ; même règle que <see cref="LigneMagasin"/>. Faits disque best-effort : jamais une exception.</summary>
    private string AgeDernierExact()
    {
        var etat = _magasins?.FirstOrDefault(m => m.Nom == NomsMagasins.DernierExact);
        DateTimeOffset? mtime = null;
        try
        {
            if (File.Exists(_paths.LastExactFile))
                mtime = new DateTimeOffset(new FileInfo(_paths.LastExactFile).LastWriteTimeUtc, TimeSpan.Zero);
        }
        catch { /* chemin inaccessible : lu « aucune écriture connue » */ }
        var derniere = etat?.DerniereEcriture ?? mtime;
        var texte = derniere is null ? "aucune écriture connue" : "dernière écriture " + LibelleSource.Anciennete(derniere, _clock.UtcNow);
        if (etat?.DerniereErreur is not null) texte += " — ÉCHEC de la dernière écriture";
        return texte;
    }

    /// <summary>JRN-04 — « journal muet », UNE seule règle pour les deux sections qui la disent. Référence = max(démarrage,
    /// dernière écriture) (D-32-21) ; seuil DÉRIVÉ de <see cref="JournalReleves.SeuilMuet"/>, jamais en dur. Null si le
    /// journal n'est pas câblé ou parle.</summary>
    private string? AlerteJournalMuet(IEtatMagasin? journal)
    {
        if (journal is null) return null;
        var reference = Max(_demarrage, journal.DerniereEcriture ?? _demarrage);
        var silence = _clock.UtcNow - reference;
        return silence > JournalReleves.SeuilMuet ? "ALERTE — journal muet depuis " + (int)silence.TotalMinutes + " min" : null;
    }

    /// <summary>SOC-01 — une ligne par démarrage (chronos.log est réécrit à chaque lancement) : jamais de bruit à chaque Load.
    /// Sous [Magasins persistants], seule section qui parle des réglages depuis la phase 37.</summary>
    private static string LibelleLectureReglages(LectureReglages lecture) => lecture.Issue switch
    {
        IssueLectureReglages.Absent => "absent — défauts",
        IssueLectureReglages.Illisible => "illisible — défauts entiers",
        IssueLectureReglages.LuAvecRetombees => lecture.Retombees.Count + " valeur(s) retombée(s) sur leur défaut — "
                                                + string.Join(", ", lecture.Retombees.OrderBy(n => n, StringComparer.Ordinal)),
        _ => "lus, aucune valeur retombée",
    };

    // Libellé français de l'état d'authentification. Chaque branche dit à l'utilisateur s'il a
    // quelque chose à FAIRE : « hors ligne » est informatif, « déconnecté » est actionnable —
    // les confondre ferait relancer un login voué à l'échec sur un simple wifi coupé.
    private static string LibelleAuth(EtatAuthentification? etat) => etat switch
    {
        EtatAuthentification.Connecte    => "CONNECTÉ (jeton valide, chiffres exacts en circulation)",
        EtatAuthentification.HorsLigne   => "HORS LIGNE (réseau ou serveur indisponible — le jeton est peut-être bon)",
        EtatAuthentification.Deconnecte  => "DÉCONNECTÉ (le serveur a refusé les identifiants — reconnexion nécessaire)",
        EtatAuthentification.NonConnecte => "jamais connecté (clic droit → « Se connecter à Claude »)",
        _                                => "(inconnu — autorité de jeton non injectée)",
    };

    // Le filtre qui écarte, NOMMÉ AVEC SON FICHIER : « absent » n'apprend rien, « écarté par treated.json »
    // dit quoi ouvrir. C'est toute la différence entre un défaut inélucidable et un défaut diagnosticable.
    //
    // Le filtre NOMMÉ AVEC SON FICHIER, et désormais avec SA CAUSE : treated.json ne porte que l'épisode ; la cause vient
    // du détecteur, qui ne l'invente pas pour ce qu'il n'a pas vu — LUE-03. Sans cause connue (un geste, ou une
    // inscription antérieure au démarrage de l'overlay), le libellé le dit tel quel : jamais « lue » ni « répondue ».
    // Tout libellé issu de treated.json commence par « treated.json » : le fichier à ouvrir reste nommé. Les heures sont
    // locales, à la seconde (D-30-11) ; « depuis N s » est figé au constat (Constat − PremierPlanDepuis), jamais
    // recalculé à l'heure du rapport : lu une heure plus tard, il ne dira pas « depuis 3 600 s ». Interne : un test
    // parcourt tous les motifs, et aucun ne doit tomber sur le repli « un filtre non nommé ».
    internal static string LibelleMasquage(MotifMasquage motif, CauseTraitement? cause) => motif switch
    {
        MotifMasquage.Archivee => "archived.json (archivage — geste explicite de l'utilisateur)",
        MotifMasquage.Traitee => "treated.json (« traité » — marquée à la main, ou traitée avant le démarrage de l'overlay ; réversible)",
        MotifMasquage.Indeterminee => "état indéterminé (signal illisible) — aucune ligne dans le widget",
        MotifMasquage.LueParFocus => cause is { Focus: { } f }
            ? $"treated.json — lue : focus à {Heure(f)} > attente à {Heure(cause.Attente)} (réversible : revient sur une nouvelle demande)"
            : "treated.json — lue par le focus de l'app (instants non retenus ; réversible)",
        MotifMasquage.LueAuPremierPlan => cause is { PremierPlanDepuis: { } d, Constat: { } c }
            ? $"treated.json — sélectionnée au premier plan depuis {(int)(c - d).TotalSeconds} s (claude) — attente à {Heure(cause.Attente)} (réversible : revient sur une nouvelle demande)"
            : "treated.json — sélectionnée au premier plan (instants non retenus ; réversible)",
        MotifMasquage.Repondue => cause is not null
            ? $"treated.json — répondue : attente à {Heure(cause.Attente)}, puis travail observé sur la même source (réversible)"
            : "treated.json — répondue (réversible)",
        _ => "un filtre non nommé",
    };

    /// <summary>
    /// ACC-03 — section « Journal d'historique » : dossier, fichiers (taille, âge), dernière écriture du journal sur disque, la journée
    /// en cours, les cinq derniers événements, la reconstruction des tokens en UNE ligne et le nombre d'instances.
    ///
    /// <para>D-35-10 — MÊME LECTURE QUE LA FENÊTRE : la journée se lit par la façade de la vue Jour (<see cref="SourceHistoriqueDisque"/>,
    /// sans état : une instance locale lit comme le singleton) et se dit par ses mots (<see cref="TextesHistorique.LigneFraicheurJour"/>) ;
    /// les événements et l'inventaire passent par <see cref="EtatJournalHistorique"/>. Ce fichier ne relit ni n'analyse le journal
    /// lui-même (gardes <c>GardesPerimetreTests</c> et <c>GardeDiagnosticHistoriqueTests</c>).</para>
    /// <para>D-35-11 — PAS DE DUPLICATION : le détail de la reconstruction et la liste des processus restent sous [Magasins
    /// persistants] ; ici, une ligne et un compte, venus du même état et du même relevé.</para>
    /// <para>D-35-12 — FUSEAU INJECTÉ : sans fuseau, les heures sont en UTC et la section le dit.</para>
    /// Toute panne s'écrit sur une ligne, jamais une exception : le diagnostic est l'outil de panne.
    /// </summary>
    private void SectionJournalHistorique(StringBuilder sb, int? nbProcessus)
    {
        sb.AppendLine("[Journal d'historique]");
        try
        {
            var tz = _fuseau ?? TimeZoneInfo.Utc;
            var now = _clock.UtcNow;
            var dossier = _paths.HistoriqueDir;
            string Local(DateTimeOffset t, string format)
                => TimeZoneInfo.ConvertTime(t, tz).ToString(format, System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));

            sb.AppendLine("  Dossier : " + dossier);
            sb.AppendLine("  Fuseau : " + (_fuseau is { } f ? f.Id : "UTC (fuseau non injecté)"));

            var fichiers = EtatJournalHistorique.Inventaire(dossier);
            sb.AppendLine("  Fichiers : " + fichiers.Count);
            foreach (var fi in fichiers)
                sb.AppendLine("    " + fi.Nom + " — " + fi.Taille + " o — modifié " + LibelleSource.Anciennete(fi.ModifieLe, now));

            var dernierReleves = fichiers.Where(fi => fi.Nom.StartsWith("releves-", StringComparison.Ordinal))
                                         .OrderBy(fi => fi.ModifieLe).LastOrDefault();
            sb.AppendLine("  Dernière écriture du journal (disque) : " + (dernierReleves is { } d
                ? LibelleSource.Anciennete(d.ModifieLe, now) + " — " + d.Nom
                : "aucun fichier de relevés"));

            // D-35-10 : la façade et les mots de la vue Jour, pas un second chemin.
            var jour = BornesPlage.Jour(now, tz);
            var donnees = new SourceHistoriqueDisque(_paths, tz).LireJour(jour, now);
            sb.AppendLine("  Jour (" + TextesHistorique.DateLongue(jour.Debut, tz) + ") : "
                          + TextesHistorique.LigneFraicheurJour(jour, donnees.Analyse, RateLimitHeaderUsageProvider.CadenceNominale, tz));
            sb.AppendLine("  " + (donnees.JournalOuvertLe is { } ouvert ? TextesHistorique.JournalOuvertLe(ouvert, tz) : "journal ouvert le : inconnu"));

            var evenements = EtatJournalHistorique.DerniersEvenements(dossier, now);
            sb.AppendLine("  Événements récents (5 derniers) :");
            if (evenements.Count == 0) sb.AppendLine("    (aucun sur 7 jours)");
            foreach (var e in evenements)
            {
                var nom = Chronos.Models.Historique.TypeEvenementTexte.Nom(e.Type) ?? (e.EvBrut ?? "non reconnu");
                var detail = e.Version ?? e.Cause ?? e.Magasin;
                sb.AppendLine("    " + Local(e.T, "yyyy-MM-dd HH:mm") + " " + nom + (detail is null ? "" : " (" + detail + ")"));
            }

            const string Detail = " (détail sous [Magasins persistants])";
            sb.AppendLine("  Reconstruction des tokens : " + (_reconstruction switch
            {
                null => "non câblée",
                { Phase: PhaseReconstruction.Reconstruction } r => "en cours — " + r.FichiersTraites + " / " + r.FichiersTotal + " fichiers" + Detail,
                { DerniereReconstructionTerminee: { } fin } => "terminée le " + Local(fin, "d MMM HH:mm") + Detail,
                { Phase: PhaseReconstruction.JamaisLancee } => "pas encore lancée" + Detail,
                var r => LibellePhase(r.Phase) + " — " + r.FichiersTraites + " / " + r.FichiersTotal + " fichiers" + Detail,
            }));

            sb.AppendLine("  Instances Chronos : " + (nbProcessus is { } n ? n + Detail : "relevé impossible"));
        }
        catch (Exception ex)
        {
            sb.AppendLine("  Journal d'historique : relevé impossible (" + ex.GetType().Name + " : " + ex.Message + ")");
        }
        sb.AppendLine();
    }

    /// <summary>TOK-02 — la phase de la reconstruction en mots du §4 (vocabulaire unique fenêtre / réglages / diagnostic / docs).</summary>
    private static string LibellePhase(PhaseReconstruction phase) => phase switch
    {
        PhaseReconstruction.JamaisLancee => "jamais lancée",
        PhaseReconstruction.Reconstruction => "reconstruction en cours",
        PhaseReconstruction.Incremental => "à jour (incrémental)",
        PhaseReconstruction.Arretee => "arrêtée",
        PhaseReconstruction.EnEchec => "EN ÉCHEC",
        _ => phase.ToString(),
    };

    // Heure LOCALE à la seconde (D-30-11) : le relevé travaille à la seconde, « 16:00 > 16:00 » serait illisible.
    /// <summary>
    /// CPT-02 — une ligne par magasin persistant (D-32-08). Les faits DISQUE d'abord (existence, taille, mtime : ce
    /// qu'une sonde hors arbre lirait), enrichis par l'état injecté du magasin (âge connu du processus, dernière
    /// erreur). L'âge connu du PROCESSUS prime sur le mtime : dans la vue virtualisée, le fichier que l'on lit n'est
    /// pas celui que l'on a écrit, et c'est précisément cet écart que la ligne doit rendre visible.
    /// </summary>
    private string LigneMagasin(string nom, string chemin, string nature)
    {
        var etat = _magasins?.FirstOrDefault(m => m.Nom == nom);

        bool existe = false;
        long taille = 0;
        DateTimeOffset? mtime = null;
        try
        {
            if (File.Exists(chemin))
            {
                var info = new FileInfo(chemin);
                existe = true;
                taille = info.Length;
                mtime = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
            }
        }
        catch { /* faits disque best-effort : un chemin inaccessible se lit « absent », l'erreur du magasin dit le reste */ }

        var derniere = etat?.DerniereEcriture ?? mtime;
        var age = LibelleSource.Anciennete(derniere, _clock.UtcNow);

        var texte = existe
            ? $"{nom} : {chemin} — dernière écriture {age} ({taille} o)"
            : derniere is not null
                ? $"{nom} : {chemin} — dernière écriture {age} ({nature} absent sur cette vue)"   // écrit ici, invisible là : l'écart des deux vues
                : $"{nom} : {chemin} — aucune écriture ({nature} absent)";

        if (etat?.DerniereErreur is { } err)
            texte += Environment.NewLine + "    ÉCHEC de la dernière écriture : " + err;

        return texte;
    }

    private static string Heure(DateTimeOffset t) => t.ToLocalTime().ToString("HH:mm:ss");

    /// <summary>D-32-21 — le plus récent de deux instants (référence de « journal muet »).</summary>
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    // La source NOMMÉE AVEC SON DOSSIER, exactement comme un filtre est nommé avec son fichier : « hook »
    // n'apprend rien, « fichier de hook (%APPDATA%\Chronos\sessions) » dit où aller regarder quand une
    // source se fige. C'est toute la différence entre constater un désaccord et pouvoir le diagnostiquer seul.
    //
    // Le suffixe « Session » n'est pas décoratif : Chronos.Text.LibelleSource nomme déjà, pour tout le
    // projet, la source d'un RELEVÉ D'USAGE (voir Describe plus bas). Ce sont deux notions étrangères —
    // qui alimente un quota, contre qui dépose un signal de session — et les confondre sous un même nom
    // masquerait le type partagé dans cette classe.
    internal static string LibelleSourceSession(SourceSession s) => s switch
    {
        SourceSession.Hook       => @"fichier de hook (%APPDATA%\Chronos\sessions)",
        SourceSession.AppBureau => "classification de fin de tour de l'app bureau (métadonnées de session)",
        SourceSession.Transcript => "transcript (~/.claude/projects)",
        _                        => "une source non nommée",
    };

    // APP-04 — CE QUE L'APP BUREAU SAIT, et ce qu'elle ne sait pas. La section lit la lecture que le moniteur a rendue
    // au SEUL appel Inspecter du rapport (OBS-01) : aucun second lecteur, aucune racine résolue ici — le rapport écrit
    // les racines que CETTE lecture a cherchées. Quatre états, aucun tu :
    //   · NON BRANCHÉE : le moniteur n'a pas de lecteur, personne n'a cherché ;
    //   · lecture impossible : le lecteur est branché mais sa lecture a levé à ce cycle — le moniteur rend alors la même
    //     lecture nulle qu'un moniteur sans lecteur, d'où le drapeau, pour ne jamais dire « non branchée » à tort ;
    //   · absente (dossier introuvable), avec les racines cherchées : sans elles, « absente » ne dirait pas où l'on a
    //     regardé — c'est exactement ce qui a caché la vue du paquet de l'app pendant deux semaines ;
    //   · trouvée : la racine, les autres candidats, les compteurs, les champs absents, puis une ligne par session
    //     AFFICHÉE, dans l'ordre de l'écran, disant ce que l'app en sait.
    // « avec métadonnées » et « relus sur disque » sont deux nombres : avec le cache, « 0 relu » n'est pas une panne.
    // Méthode d'INSTANCE : l'ancienneté du dernier focus se mesure à l'horloge injectée, jamais à celle du système.
    private void DecrireSourceAppBureau(StringBuilder sb, LectureSessions lecture, bool lecteurBranche)
    {
        var a = lecture.AppBureau;
        if (a is null)
        {
            sb.AppendLine(lecteurBranche
                ? "  Source app-bureau : lecture impossible à ce cycle — le lecteur est branché mais sa lecture a levé ; le widget se comporte comme en v1.6"
                : "  Source app-bureau : NON BRANCHÉE — le moniteur n'a pas de lecteur ; le widget se comporte comme en v1.6");
            return;
        }

        if (!a.DossierTrouve)
        {
            sb.AppendLine("  Source app-bureau : absente (dossier introuvable) — cherché : " + string.Join(" ; ", a.RacinesCherchees));
            return;
        }

        sb.AppendLine($"  Source app-bureau : trouvée — {a.Racine}");

        // Les autres candidats, présents ou non : dans l'arbre de l'app, deux chemins montrent les mêmes fichiers ;
        // hors de l'arbre (l'overlay), la vue réelle n'existe pas. Le dire évite de chercher la mauvaise.
        var autres = a.RacinesCherchees
            .Where(c => !string.Equals(c, a.Racine, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (autres.Count > 0)
            sb.AppendLine("    Autres racines candidates : "
                        + string.Join(" ; ", autres.Select(c => c + (DossierExiste(c) ? " (présente)" : " (absente)"))));

        sb.AppendLine($"    Fichiers : {a.Enumeres} énumérés · {a.Recents} modifiés depuis moins de {(int)HorizonsSessions.LectureAppBureau.TotalHours} h"
                    + $" · {a.Valides} avec métadonnées ({a.RelusSurDisque} relus sur disque à ce cycle) · {a.Illisibles} illisible(s)"
                    + $" · {a.SansCliSessionId} sans cliSessionId (ignoré(s)) · {a.Doublons} doublon(s)");
        sb.AppendLine("    Champs absents (fichiers lus) : "
                    + (a.ChampsAbsents.Count == 0
                        ? "aucun"
                        : string.Join(" · ", a.ChampsAbsents.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key} {c.Value}"))));

        var jointes = lecture.Visibles.Count(s => a.ParSession.ContainsKey(s.SessionId));
        sb.AppendLine($"    Jointures : {jointes} session(s) affichée(s) sur {lecture.Visibles.Count} ont des métadonnées");
        foreach (var s in AffichageSessions.Ordonner(lecture.Visibles))
        {
            if (!a.ParSession.TryGetValue(s.SessionId, out var m))
            {
                sb.AppendLine($"    · {Court(s.SessionId)} — aucune métadonnée (comportement v1.6)");
                continue;
            }

            // Un titre blanc n'est pas un titre (même règle que le nom du widget) ; un focus absent reste absent,
            // jamais remplacé par l'instant courant.
            var titre = string.IsNullOrWhiteSpace(m.Titre) ? "sans titre" : $"« {m.Titre} »";
            var focus = m.DernierFocus is { } f ? "focus " + AffichageSessions.Age(_clock.UtcNow - f) : "focus inconnu";
            var archivee = m.Archivee ? " · archivée dans l'app" : "";
            sb.AppendLine($"    · {Court(s.SessionId)} {titre} — {focus} — fin de tour : {LibelleClassification(m)}{archivee}");
        }
    }

    // LUE-01, LUE-02, LUE-04 — la règle « lue », dite à partir de la MÊME lecture que le widget (OBS-01) : la sélection
    // vient de lecture.AppBureau, le premier plan de lecture.PremierPlan. Aucun second appel à la sonde ni au lecteur.
    //
    // Le rapport dit ce que le moniteur voit À L'INSTANT DU RAPPORT. Demandé depuis le menu de Chronos, le premier plan
    // est alors Chronos ou l'éditeur, et la ligne le dit : c'est vrai à cet instant. Les causes déjà constatées, elles,
    // restent lisibles sur les lignes masquées (« depuis N s » y est figé au constat).
    //
    // Quand la règle ne peut rien voir, elle le dit (LUE-04) : sans source app-bureau, la sélection est « inconnue » et
    // aucun identifiant n'est écrit ; sans sonde, le premier plan est « NON BRANCHÉ » (le premier plan : masculin, à ne
    // pas confondre avec la source « NON BRANCHÉE » de la section précédente). Aucune ligne ne commence par « · » : la
    // liste par session de la section app-bureau s'arrête juste avant.
    // Méthode d'INSTANCE : l'ancienneté du premier plan se mesure à l'horloge injectée, jamais à celle du système.
    private void DecrireRegleLue(StringBuilder sb, LectureSessions lecture)
    {
        sb.AppendLine("  Règle « lue » (LUE-01, LUE-02) :");
        var a = lecture.AppBureau;
        var source = a is { DossierTrouve: true };
        sb.AppendLine("    Session sélectionnée dans l'app : " + (!source
            ? "inconnue — source app-bureau absente : règle « lue » inactive (comportement v1.6)"
            : a!.Selection is not { } sel ? "aucune — aucun dernier focus lisible"
            : sel.CliSessionId is not { } id ? $"aucune — le dernier focus ({Heure(sel.DernierFocus)}) est une session sans cliSessionId"
            : $"{Court(id)} (focus {Heure(sel.DernierFocus)})"));
        var p = lecture.PremierPlan ?? EtatPremierPlan.NonBranche;
        sb.AppendLine("    Premier plan : " + p.Statut switch
        {
            StatutPremierPlan.Claude => $"claude depuis {(int)(_clock.UtcNow - (p.Depuis ?? _clock.UtcNow)).TotalSeconds} s — "
                                        + (source ? "LUE-02 active" : "LUE-02 inactive (source app-bureau absente)"),
            StatutPremierPlan.AutreProcessus => $"{p.Processus ?? "processus inconnu"} — LUE-02 inactive",
            StatutPremierPlan.AucuneFenetre => "aucune fenêtre au premier plan — LUE-02 inactive",
            StatutPremierPlan.Indisponible => $"indisponible ({p.Raison ?? "raison inconnue"}) — LUE-02 inactive, LUE-01 seule",
            _ => "NON BRANCHÉ — LUE-02 inactive, LUE-01 seule",
        });
    }

    // La classification de fin de tour, dite telle que l'app l'a écrite. Une catégorie que le lecteur ne connaît pas
    // est recopiée BRUTE et dite non interprétée : le rapport ne traduit pas ce que le lecteur refuse de deviner. Une
    // question sans motif n'est pas déposée comme attente (règle du moniteur) : la ligne le dit.
    private static string LibelleClassification(MetadonneesAppBureau m) => m.ClassificationFinDeTour switch
    {
        ClassificationFinDeTour.Bloquee => string.IsNullOrWhiteSpace(m.MotifBlocage)
            ? "question posée (sans motif — aucune attente déposée)"
            : $"question posée (« {m.MotifBlocage} »)",
        ClassificationFinDeTour.Terminee    => "terminée",
        ClassificationFinDeTour.PreteARevue => "prête à revue",
        _ => string.IsNullOrWhiteSpace(m.CategorieBrute)
            ? "aucune classification"
            : $"inconnue (« {m.CategorieBrute} ») — non interprétée",
    };

    // Existence d'un candidat NON retenu, pour l'écrire : un test d'existence qui lève compte comme « absente ».
    private static bool DossierExiste(string dossier)
    {
        try { return Directory.Exists(dossier); }
        catch { return false; }
    }

    // Huit premiers caractères de l'identifiant : assez pour retrouver le fichier d'état correspondant dans
    // %APPDATA%\Chronos\sessions, assez court pour que la ligne reste lisible.
    private static string Court(string id) => id.Length <= 8 ? id : id[..8];

    // VAL-03 — la version EMBARQUÉE, lue sur l'assembly : une seule source, les quatre propriétés du csproj
    // (IncludeSourceRevisionInInformationalVersion=false, donc sans « +sha »). Jamais écrite en dur ici.
    private static string VersionEmbarquee()
        => (System.Attribute.GetCustomAttribute(typeof(DiagnosticService).Assembly,
                typeof(System.Reflection.AssemblyInformationalVersionAttribute))
            as System.Reflection.AssemblyInformationalVersionAttribute)?.InformationalVersion ?? "?";

    // Issue du dernier passage de la sonde, UN LIBELLÉ PAR MEMBRE. Le grain fin est le livrable : la panne
    // silencieuse que v1.5 corrige venait précisément de l'écrasement de causes distinctes en un seul
    // « pas de données ». « 429 porteur des chiffres » et « 429 muet » ne disent pas la même chose.
    private static string LibelleSonde(ResultatSonde? r) => r switch
    {
        ResultatSonde.Desactivee            => "désactivée",
        ResultatSonde.PasDeJeton            => "pas de jeton (clic droit → Se connecter à Claude)",
        ResultatSonde.FreinActif            => "cadence bornée : sonde refusée localement, aucune requête émise",
        ResultatSonde.SuccesEnTetesLus      => "200 — en-têtes lus, chiffres exacts",
        ResultatSonde.SuccesSansEnTetes     => "200 mais AUCUN en-tête unified reconnu — la famille a peut-être été renommée",
        ResultatSonde.SaturationEnTetesLus  => "429 — en-têtes lus quand même, les chiffres restent exacts",
        ResultatSonde.SaturationSansEnTetes => "429 SANS en-tête unified — rien affiché plutôt qu'un chiffre inventé",
        ResultatSonde.RefusServeur          => "refus serveur (401/403) — seule une reconnexion répare",
        ResultatSonde.ModeleRefuse          => "modèle refusé par le serveur — identifiant de modèle à mettre à jour",
        ResultatSonde.PanneReseau           => "réseau injoignable (recul progressif)",
        // JamaisSondee ET le cas « canal latéral non injecté » : dans les deux cas, rien n'a encore été
        // observé — et ce n'est pas une panne.
        _                                   => "pas encore sondé (la première sonde arrive au prochain tick)",
    };

    // Statut DÉCLARÉ par le serveur (HDR-03). « non rapporté » (en-tête absent) et « non reconnu »
    // (en-tête présent, valeur inédite) sont deux faits distincts : une valeur inconnue n'est JAMAIS
    // rangée d'autorité dans « autorisé », sans quoi l'overlay rassurerait à tort.
    private static string LibelleStatutServeur(StatutServeur? s) => s switch
    {
        StatutServeur.Autorise              => "AUTORISÉ",
        StatutServeur.AutoriseAvertissement => "AUTORISÉ (avertissement)",
        StatutServeur.Rejete                => "REJETÉ",
        StatutServeur.NonReconnu            => "statut non reconnu (valeur inconnue, non interprétée)",
        _                                   => "non rapporté",
    };

    // MÊME enum, contexte OPPOSÉ. Sur une fenêtre, le statut dit ce que le serveur fait de tes requêtes
    // (« REJETÉ » = tu es bloqué). Sur le dépassement SANS quantité, il dit ce que le compte autorise
    // (« rejected » = le dépassement n'est pas permis ici) — une politique, pas un refus. Réutiliser le
    // libellé de la fenêtre affichait « REJETÉ » à un utilisateur à 23 % d'usage que rien ne bloquait.
    private static string LibellePolitiqueDepassement(StatutServeur? s) => s switch
    {
        StatutServeur.Autorise              => "dépassement autorisé",
        StatutServeur.AutoriseAvertissement => "dépassement autorisé (avertissement)",
        StatutServeur.Rejete                => "dépassement non autorisé sur ce compte",
        StatutServeur.NonReconnu            => "politique non reconnue (valeur inconnue, non interprétée)",
        _                                   => "non rapportée",
    };

    // HDR-03/HDR-04 — le statut serveur et le dépassement sont lus ICI, sur le snapshot DÉJÀ obtenu, et
    // non dans la section de la sonde : un second appel au composite déclencherait une seconde sonde, donc
    // DOUBLERAIT la dépense de quota à chaque ouverture du diagnostic. Les cinq branches de libellé
    // préexistantes sont conservées mot pour mot (des tests les assertent littéralement) ; les deux
    // suffixes ne s'ajoutent que lorsque le serveur a réellement dit quelque chose.
    // EXA-06 — le rapport rend le COUPLE (QUI alimente cette fenêtre, DEPUIS QUAND) en plus de son état.
    // C'est littéralement ce qui a manqué pendant deux mois : un « 10 % » parfaitement affiché, jamais
    // daté, alimenté par une source morte depuis le 2026-07-12 — et aucun endroit où le lire.
    //
    // Le vocabulaire FR vient de Chronos.Text.LibelleSource : UN seul mapping pour tout le projet,
    // partagé avec l'infobulle du cadran (20-04). Deux mappings divergeraient au premier changement de
    // vocabulaire, et l'utilisateur lirait deux noms différents pour la même source selon l'endroit où
    // il regarde. Méthode d'INSTANCE et non statique : l'ancienneté se mesure contre _clock, jamais
    // contre DateTimeOffset.UtcNow — les deux sites d'appel sont dans la même instance, leur texte ne
    // change pas.
    private string Describe(WindowState w)
    {
        var baseTexte = w.Reliability switch
        {
            SourceReliability.Exact => "EXACT — " + (w.Utilization is { } u ? UsageNormalization.PourcentagePourAffichage(u) : "?"),
            // « ≥ » et NON « ~ » (19-04) : l'incertitude d'un plancher est UNILATÉRALE. On connaît la
            // borne inférieure, c'est la borne supérieure qui est inconnue. Un tilde dirait « autour
            // de 42 », ce qui autoriserait la lecture « peut-être 38 » — un mensonge par symétrie.
            SourceReliability.Estimated => "PLANCHER — " + (w.Utilization is { } u ? "≥ " + UsageNormalization.PourcentagePourAffichage(u) : "% inconnu"),
            _ => "indisponible",
        };

        // Les deux segments d'EXA-06 sont INCONDITIONNELS : une fenêtre que personne n'alimente doit le
        // dire (« non renseignée »), et non se taire. Un silence se lit comme « rien à signaler ».
        baseTexte += " · source : " + LibelleSource.Format(w.Source);
        baseTexte += " · relevé " + LibelleSource.Anciennete(w.CapturedAt, _clock.UtcNow);
        // La provenance, elle, est CONDITIONNELLE : null veut dire « la doctrine ne s'est pas
        // prononcée », et mieux vaut ne rien dire que qualifier un chiffre sur lequel personne n'a statué.
        if (LibelleSource.Provenance(w.Provenance) is { Length: > 0 } prov) baseTexte += " · " + prov;

        if (w.StatutServeur is { } st) baseTexte += " · serveur : " + LibelleStatutServeur(st);
        if (w.Depassement?.Utilization is { } d)
            baseTexte += " · dépassement " + UsageNormalization.PourcentagePourAffichage(d);

        return baseTexte;
    }
}
