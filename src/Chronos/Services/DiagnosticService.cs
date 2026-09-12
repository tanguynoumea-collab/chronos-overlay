using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Chronos.Models;
using Chronos.Text;

namespace Chronos.Services;

/// <summary>
/// Diagnostic auto-explicatif (observabilité pour un outil distribué) : dit POURQUOI l'affichage
/// n'a pas de couleurs sur une machine donnée. Rassemble l'état réel — token trouvé ? statut de
/// l'appel OAuth ? sources présentes ? source active par fenêtre — et écrit un rapport
/// lisible dans %APPDATA%/Chronos/diagnostic.txt, qu'il ouvre ensuite.
///
/// SÉCURITÉ : le token n'est JAMAIS écrit dans le rapport (seulement « trouvé : oui/non »). Neutre
/// (aucun type WPF) : ouvre le fichier via l'application par défaut du système (Process.Start).
/// </summary>
public sealed class DiagnosticService
{
    private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";

    private readonly IClaudeTokenReader _tokenReader;
    private readonly ChronosPaths _paths;
    private readonly SettingsService _settings;
    private readonly IUsageProvider _composite;
    private readonly IClock _clock;
    private readonly IAuthStatus? _authStatus;
    private readonly IEtatServeur? _etatServeur;
    private readonly IInventaireMachine _machine;

    /// <param name="authStatus">État d'authentification réel (autorité de jeton). OPTIONNEL et en
    /// dernière position à dessein : les 8 sites de construction existants (1 en production, 7 en
    /// tests) compilent sans retouche, et la DI passe le vrai service.</param>
    /// <param name="etatServeur">Canal latéral de la sonde d'en-têtes (HDR-03/HDR-04) et son issue.
    /// OPTIONNEL et en DERNIÈRE position à dessein : les 10 sites de construction préexistants (1 en
    /// production, 9 en tests) compilent sans retouche. Précédent : authStatus, phase 17.</param>
    /// <param name="machine">Les deux sondages d'environnement MESURÉS chers (coffres OAuth 17 703 ms,
    /// poll UIA 936 ms — 99,4 % du coût d'un rapport ; 7 tests payaient 2 min 8 s pour cela seul).
    /// OPTIONNEL et en DERNIÈRE position à dessein : les 10 sites de construction préexistants
    /// compilent sans retouche. Le repli <c>?? new InventaireMachine()</c> laisse la PRODUCTION
    /// strictement inchangée — aucune inscription DI, aucune mémoïsation. Précédents : authStatus
    /// (phase 17), etatServeur (phase 18).</param>
    public DiagnosticService(IClaudeTokenReader tokenReader, ChronosPaths paths,
                             SettingsService settings, IUsageProvider composite, IClock clock,
                             IAuthStatus? authStatus = null, IEtatServeur? etatServeur = null,
                             IInventaireMachine? machine = null)
    {
        _tokenReader = tokenReader;
        _paths = paths;
        _settings = settings;
        _composite = composite;
        _clock = clock;
        _authStatus = authStatus;
        _etatServeur = etatServeur;
        _machine = machine ?? new InventaireMachine();
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
        var s = _settings.Load();

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

        var sb = new StringBuilder();
        sb.AppendLine("=== Chronos — Diagnostic ===");
        sb.AppendLine("Date : " + _clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();

        // 1) Réglage
        sb.AppendLine("[Réglage]");
        sb.AppendLine("  Usage exact (OAuth) : " + (s.OAuthUsageEnabled ? "ACTIVÉ" : "DÉSACTIVÉ (menu)"));
        sb.AppendLine();

        // 2a) Source exacte PRIMAIRE : login OAuth intégré de Chronos (coffre chiffré oauth.dat).
        sb.AppendLine("[Source exacte — login OAuth Chronos]");
        var oauthDat = Path.Combine(Path.GetDirectoryName(_paths.UsageFile)!, "oauth.dat");
        sb.AppendLine("  Connecté : " + (File.Exists(oauthDat)
            ? "OUI (jeton chiffré présent) — les chiffres exacts arrivent au prochain rafraîchissement"
            : "non (menu clic droit → « Se connecter à Claude »)"));
        // TOK-02 : « le fichier existe » n'a JAMAIS voulu dire « authentifié ». Le jeton de cette
        // machine a expiré le 2026-07-12 alors que oauth.dat était bien présent : c'est exactement le
        // silence que la phase 17 brise. On affiche donc l'état RÉEL, pas la présence d'un fichier.
        sb.AppendLine("  État d'authentification : " + LibelleAuth(_authStatus?.Etat));
        sb.AppendLine();

        // 2a-bis) LA SONDE D'EN-TÊTES (HDR-01/HDR-02/HDR-06) : désormais la PREMIÈRE source de la chaîne
        // exacte, et la SEULE qui réponde encore quand l'API refuse. Cette section est la seule fenêtre de
        // l'utilisateur sur ce qu'elle reçoit réellement : la famille d'en-têtes « unified » n'est
        // documentée NULLE PART chez Anthropic, donc seul un rapport de terrain peut dire si elle existe
        // toujours sous ce nom. (Le préfixe littéral n'est écrit QU'UNE fois dans ce fichier, dans le
        // filtre de l'inventaire ci-dessous : un préfixe dupliqué est un préfixe qui divergera.)
        sb.AppendLine("[Source exacte — sonde d'en-têtes de rate-limit]");
        if (!s.SondeEnTetesActivee)
            sb.AppendLine("  Interrupteur : désactivée (menu clic droit → Réglages) — aucune requête, aucun coût");
        else
        {
            // Cadence et coût DÉRIVÉS de la constante du provider, jamais recopiés : un chiffre recopié
            // diverge le jour où la cadence change, et ce rapport deviendrait un mensonge poli.
            var minutes = (int)RateLimitHeaderUsageProvider.CadenceNominale.TotalMinutes;
            var parJour = (int)(TimeSpan.FromDays(1).TotalSeconds
                                / RateLimitHeaderUsageProvider.CadenceNominale.TotalSeconds);
            sb.AppendLine($"  Interrupteur : ACTIVÉE — une micro-requête sur ton compte toutes les {minutes} min (≈ {parJour}/jour)");
            sb.AppendLine("  Dernière sonde : " + LibelleSonde(_etatServeur?.DernierResultat));

            // SÉCURITÉ : ces noms proviennent des CONSTANTES de la sonde, jamais du serveur (invariant
            // prouvé au plan 18-04, test en casse mélangée). Les NOMS, et JAMAIS leurs valeurs : une
            // valeur d'en-tête venue du réseau ne doit pas pouvoir se réinjecter dans un affichage.
            var noms = _etatServeur?.NomsEnTetesRecus ?? Array.Empty<string>();
            sb.AppendLine("  En-têtes « unified » reconnus : " + (noms.Count == 0
                ? "AUCUN — la famille « unified » n'est documentée nulle part chez Anthropic et peut avoir changé de nom"
                : string.Join(", ", noms) + $" ({noms.Count})"));

            // HDR-04 — canal LATÉRAL : ce dépassement survit même quand Best() a écarté la fenêtre qui le
            // portait. Pourcentage par le point unique de normalisation (garde NormalisationUniqueTests).
            var dep = _etatServeur?.Depassement;
            // TROIS cas, et non deux. Un statut SEUL n'est pas un dépassement : c'est la POLITIQUE du
            // compte à son sujet. Les confondre produisait « Dépassement :  · serveur : REJETÉ » sur un
            // compte à 23 % dont les deux fenêtres disaient « autorisé » — séparateur orphelin ET
            // contresens alarmant (constaté en production le 2026-09-12).
            sb.AppendLine("  Dépassement : " + (dep is null || !dep.EstRenseigne
                ? "aucun dépassement rapporté"
                : !dep.EstEnCours
                    ? "aucun dépassement en cours · politique du compte : " + LibellePolitiqueDepassement(dep.Statut)
                    : UsageNormalization.PourcentagePourAffichage(dep.Utilization)
                      + (dep.ResetsAt is { } dr ? " (reset le " + dr.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + ")" : "")
                      + " · serveur : " + LibelleStatutServeur(dep.Statut)));
        }
        sb.AppendLine();

        // 2b) Source exacte secondaire : pont statusLine Claude Code (usage.json), terminal uniquement.
        sb.AppendLine("[Source exacte — pont statusLine Claude Code]");
        var claudeSettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
        bool bridgeInstalled = false;
        string? statusLineCmd = null;
        try
        {
            if (File.Exists(claudeSettings))
            {
                using var sd = JsonDocument.Parse(File.ReadAllText(claudeSettings));
                if (sd.RootElement.TryGetProperty("statusLine", out var slNode)
                    && slNode.TryGetProperty("command", out var cmdEl) && cmdEl.ValueKind == JsonValueKind.String)
                {
                    statusLineCmd = cmdEl.GetString();
                    bridgeInstalled = statusLineCmd is not null
                        && statusLineCmd.Contains("--statusline", StringComparison.OrdinalIgnoreCase)
                        && statusLineCmd.Contains("Chronos", StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch { }
        sb.AppendLine("  Intégration Claude Code : " + (bridgeInstalled ? "INSTALLÉE (statusLine → Chronos)"
            : File.Exists(claudeSettings) ? "non installée (menu « Source exacte (Claude Code) »)"
            : "settings.json Claude absent (Claude Code jamais lancé ?)"));

        // Fraîcheur de usage.json (le fichier que le pont écrit et que l'overlay lit).
        try
        {
            if (File.Exists(_paths.UsageFile))
            {
                using var ud = JsonDocument.Parse(File.ReadAllText(_paths.UsageFile));
                var r = ud.RootElement;
                // HDR-05 : plus aucune conversion d'unité locale — tout passe par UsageNormalization.
                string W(string w) => r.TryGetProperty(w, out var o) && o.TryGetProperty("used_percentage", out var p) && p.TryGetDouble(out var v)
                    ? UsageNormalization.PourcentagePourAffichage(UsageNormalization.FractionDepuisPourcentage(v)) : "absent";
                string age = "inconnu";
                if (r.TryGetProperty("capturedAt", out var ca) && ca.TryGetInt64(out var ms)
                    && UsageNormalization.InstantDepuisEpochMillisecondes(ms) is { } capture)
                {
                    var mins = (_clock.UtcNow - capture).TotalMinutes;
                    age = mins < 1 ? "à l'instant" : $"il y a {mins:F0} min";
                }
                sb.AppendLine($"  usage.json : présent — 5 h {W("five_hour")}, hebdo {W("seven_day")} (maj {age})");
            }
            else
                sb.AppendLine("  usage.json : ABSENT (le pont n'a pas encore reçu de données — lance un message dans Claude Code)");
        }
        catch { sb.AppendLine("  usage.json : illisible"); }
        sb.AppendLine();

        // 3) Source exacte — OAuth (repli historique, désormais secondaire)
        sb.AppendLine("[Source exacte — endpoint OAuth (repli)]");
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var cfg = Path.Combine(appData, "Claude", "config.json");
        var ls = Path.Combine(appData, "Claude", "Local State");
        sb.AppendLine("  Coffre app bureau Claude :");
        sb.AppendLine("    config.json  : " + (File.Exists(cfg) ? "présent" : "ABSENT (app bureau non installée ?)"));
        sb.AppendLine("    Local State  : " + (File.Exists(ls) ? "présent" : "ABSENT"));
        var creds = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");
        sb.AppendLine("  Repli Claude Code CLI :");
        sb.AppendLine("    .credentials.json : " + (File.Exists(creds) ? "présent" : "ABSENT"));

        // Découverte : OÙ l'app range-t-elle réellement son coffre ? (le chemin varie selon l'app/version)
        sb.AppendLine("  Recherche du coffre (config.json contenant « oauth:tokenCache ») :");
        int found = 0;
        foreach (var (label, root) in new[]
        {
            ("%APPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            ("%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
        })
        {
            foreach (var hit in _machine.CoffresOAuth(root))
            {
                sb.AppendLine("    ✓ " + hit.Replace(root, label));
                found++;
            }
            // liste aussi les dossiers « Claude/Cowork/Anthropic » présents (même sans tokenCache)
            try
            {
                foreach (var d in Directory.EnumerateDirectories(root)
                             .Where(d => { var n = Path.GetFileName(d).ToLowerInvariant(); return n.Contains("claude") || n.Contains("cowork") || n.Contains("anthropic"); }))
                    sb.AppendLine("    · dossier : " + d.Replace(root, label));
            }
            catch { }
        }
        if (found == 0) sb.AppendLine("    (aucun coffre oauth:tokenCache trouvé sous %APPDATA%/%LOCALAPPDATA%)");

        // Cartographie des dossiers Claude non vides → localiser le vrai magasin du token.
        sb.AppendLine("  Structure des dossiers Claude (pour localiser le token) :");
        foreach (var (label, rt) in new[]
        {
            ("%APPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            ("%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
        })
        {
            IEnumerable<string> dirs;
            try { dirs = Directory.EnumerateDirectories(rt).Where(d => { var n = Path.GetFileName(d).ToLowerInvariant(); return n.Contains("claude") || n.Contains("cowork") || n.Contains("anthropic"); }); }
            catch { continue; }
            foreach (var d in dirs)
            {
                string[] entries;
                try { entries = Directory.GetFileSystemEntries(d); } catch { continue; }
                if (entries.Length == 0) continue; // ignore les dossiers vides (comme mon sandbox)
                sb.AppendLine("    " + d.Replace(rt, label) + " :");
                sb.AppendLine("      Local State: " + (File.Exists(Path.Combine(d, "Local State")) ? "OUI" : "non")
                            + " | leveldb: " + (Directory.Exists(Path.Combine(d, "Local Storage", "leveldb")) ? "OUI" : "non"));
                var names = entries.Select(Path.GetFileName).Where(n => n is not null).Take(14);
                sb.AppendLine("      contient: " + string.Join(", ", names));
            }
        }

        // Clés de premier niveau de .credentials.json (dit si le jeton principal « claudeAiOauth » y est,
        // ou seulement les jetons MCP « mcpOAuth »). On n'affiche QUE les noms de clés, jamais les valeurs.
        if (File.Exists(creds))
        {
            try
            {
                using var cd = JsonDocument.Parse(File.ReadAllText(creds));
                var keys = cd.RootElement.ValueKind == JsonValueKind.Object
                    ? string.Join(", ", cd.RootElement.EnumerateObject().Select(p => p.Name))
                    : "(pas un objet)";
                sb.AppendLine("    .credentials.json clés : " + keys);
                var hasMain = cd.RootElement.TryGetProperty("claudeAiOauth", out var cao)
                              && cao.ValueKind == JsonValueKind.Object
                              && cao.TryGetProperty("accessToken", out var caoTok)
                              && caoTok.ValueKind == JsonValueKind.String
                              && !string.IsNullOrEmpty(caoTok.GetString());
                sb.AppendLine("    jeton principal (claudeAiOauth.accessToken) : " + (hasMain ? "PRÉSENT" : "absent"));
            }
            catch { sb.AppendLine("    .credentials.json : illisible/JSON invalide"); }
        }

        // Gestionnaire d'identifiants Windows : où Claude Code range souvent le jeton sous Windows.
        // On liste les cibles « claude/anthropic », la taille du blob et sa forme (clés JSON), + si un
        // jeton en a été extrait. JAMAIS la valeur du jeton.
        sb.AppendLine("  Gestionnaire d'identifiants Windows (cibles claude/anthropic) :");
        try
        {
            var entries = WindowsCredentialStore.ReadClaudeEntries();
            if (entries.Count == 0) sb.AppendLine("    (aucune cible claude/anthropic)");
            foreach (var en in entries)
            {
                var shape = DescribeBlobShape(en.Blob);
                var parsed = ClaudeTokenReader.ParseCredentialBlob(en.Blob, out _) is not null;
                sb.AppendLine($"    ✓ {en.TargetName} — {en.Blob.Length} o — {shape} — jeton: {(parsed ? "OUI" : "non")}");
            }
        }
        catch (Exception ex) { sb.AppendLine("    (lecture impossible : " + ex.GetType().Name + ")"); }

        var token = _tokenReader.TryReadAccessToken(out var exp);
        sb.AppendLine("  Token déchiffré : " + (token is null ? "NON (pas de token lisible → pas de chiffres exacts)" : "OUI"));
        if (exp is { } e) sb.AppendLine("  Expiration token : " + e.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));

        if (token is not null)
        {
            sb.AppendLine("  Appel " + UsageUrl + " :");
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                using var req = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                req.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
                using var resp = await http.SendAsync(req, ct);
                sb.AppendLine("    → HTTP " + (int)resp.StatusCode + " " + resp.StatusCode);

                // OUVERTURE : on ne sait PAS si /api/oauth/usage porte AUSSI la famille unified.
                // S'il la portait, HDR-01..HDR-04 seraient satisfaits SANS dépenser un jeton de quota et le
                // coût de la sonde disparaîtrait (candidat phase 19+). Impossible à trancher sans jeton
                // valide : son 401 est rendu en bordure (request_id nul) et ne porte aucun en-tête de
                // limite. On liste donc les NOMS — JAMAIS les valeurs, JAMAIS le corps : la question se
                // tranchera au premier rafraîchissement réussi de l'utilisateur.
                var nomsLimite = resp.Headers.Select(h => h.Key)
                    .Where(k => k.StartsWith("anthropic-ratelimit", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                sb.AppendLine("    → en-têtes de limite présents : " + (nomsLimite.Count == 0
                    ? "AUCUN"
                    : string.Join(", ", nomsLimite) + $" ({nomsLimite.Count})"));

                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    sb.AppendLine("    → five_hour : " + Pct(doc.RootElement, "five_hour")
                                + "   seven_day : " + Pct(doc.RootElement, "seven_day"));
                }
                else if ((int)resp.StatusCode == 401 || (int)resp.StatusCode == 403)
                    sb.AppendLine("    → token refusé/expiré : relance/ouvre l'app bureau Claude pour le rafraîchir.");
                else if ((int)resp.StatusCode == 429)
                    sb.AppendLine("    → rate limité (temporaire) : réessaie dans quelques minutes.");
            }
            catch (Exception ex)
            {
                sb.AppendLine("    → ÉCHEC RÉSEAU : " + ex.GetType().Name + " : " + ex.Message);
                sb.AppendLine("      (pare-feu/proxy d'entreprise bloquant api.anthropic.com ? VPN ? TLS ?)");
            }
        }
        sb.AppendLine();

        // 3) Transcripts JSONL — désormais source de DELTA (aucun plafond, aucun pourcentage)
        sb.AppendLine("[Transcripts JSONL (source de delta)]");
        var projects = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
        int jsonl = 0;
        try { if (Directory.Exists(projects)) jsonl = Directory.EnumerateFiles(projects, "*.jsonl", SearchOption.AllDirectories).Take(5000).Count(); } catch { }
        sb.AppendLine("  Dossier ~/.claude/projects : " + (Directory.Exists(projects) ? jsonl + " fichier(s) .jsonl" : "ABSENT (aucun historique local)"));
        sb.AppendLine();

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

        // Fichiers d'état écrits par le mode --hook.
        var sessDir = Path.Combine(Path.GetDirectoryName(_paths.UsageFile)!, "sessions");
        try
        {
            var files = Directory.Exists(sessDir) ? Directory.GetFiles(sessDir, "*.json") : System.Array.Empty<string>();
            sb.AppendLine($"  Fichiers d'état ({sessDir}) : {files.Length}");
            foreach (var f in files.Take(8))
            {
                try
                {
                    using var d = JsonDocument.Parse(File.ReadAllText(f));
                    var r = d.RootElement;
                    string P(string k) => r.TryGetProperty(k, out var v) ? v.ToString() : "?";
                    // HDR-05 : plus aucune conversion d'unité locale — tout passe par UsageNormalization.
                    var age = r.TryGetProperty("updated_at", out var ua) && ua.TryGetInt64(out var ms)
                        && UsageNormalization.InstantDepuisEpochMillisecondes(ms) is { } maj
                        ? $"{(_clock.UtcNow - maj).TotalMinutes:F0} min" : "?";
                    sb.AppendLine($"    · {P("project")} — {P("activity")} (maj il y a {age})");
                }
                catch { }
            }
            if (files.Length == 0)
                sb.AppendLine("    (aucun — normal en app bureau : la détection passe par les transcripts, pas les hooks)");
        }
        catch { }

        // Ce que le widget AFFICHE réellement (transcripts ~/.claude/projects + hooks, fusionnés + staleness).
        try
        {
            var detected = new SessionMonitor().Read(_clock.UtcNow);
            sb.AppendLine($"  Sessions détectées (widget) : {detected.Count}");
            foreach (var d in detected.Take(8))
                sb.AppendLine($"    · {d.Project} — {d.Activity} (maj il y a {(_clock.UtcNow - d.UpdatedAt).TotalMinutes:F0} min)");
            if (detected.Count == 0)
                sb.AppendLine("    → aucune session active récente (< 15 min). Utilise une session Claude Code puis rouvre ce diagnostic.");
        }
        catch (Exception ex) { sb.AppendLine("  (détection sessions impossible : " + ex.GetType().Name + ")"); }

        // Sessions BUREAU via la VRAIE source UIA (poll one-shot, hors thread UI) — vérité-terrain de ce
        // que voit réellement DesktopUiaSessionSource (le bloc « widget » ci-dessus est volontairement
        // aveugle au bureau : new SessionMonitor() sans source bureau).
        try
        {
            var (dsk, sante) = _machine.SessionsBureau(_clock.UtcNow);
            sb.AppendLine($"  Sessions BUREAU (UIA, one-shot) : {dsk.Count}  [santé UIA : {sante}]");
            foreach (var d in dsk.Take(12))
                sb.AppendLine($"    · {d.Kind}/{d.Activity} — {d.Project}  [{d.SessionId}]");
            if (dsk.Count == 0)
                sb.AppendLine("    → aucune session bureau (app Claude fermée, ancre absente, ou libellés non reconnus).");
        }
        catch (Exception ex) { sb.AppendLine("  (source bureau UIA indisponible : " + ex.GetType().Name + ")"); }
        sb.AppendLine();

        // 5) Conseil
        sb.AppendLine("[Conseil]");
        if (!bridgeInstalled)
            sb.AppendLine("  → Active « Source exacte (Claude Code) » dans le menu (clic droit). Chronos s'intègre à\n" +
                          "    Claude Code : les vrais pourcentages 5 h/hebdo s'affichent dès ton prochain message.");
        else
            sb.AppendLine("  → Intégration active. Si usage.json est absent, envoie un message dans Claude Code :\n" +
                          "    la barre de statut se met à jour à ce moment-là et alimente le cadran.");

        return sb.ToString();
    }

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

    // La recherche des coffres OAuth vit désormais dans InventaireMachine (phase 20, vague 0) : elle a
    // été DÉPLACÉE, pas dupliquée, afin d'être substituable sous test (17 703 ms mesurés par appel).

    // Forme d'un blob d'identifiant SANS révéler son contenu : encodage probable + clés JSON de 1er niveau.
    private static string DescribeBlobShape(byte[] blob)
    {
        if (blob is null || blob.Length == 0) return "vide";
        foreach (var enc in new[] { Encoding.UTF8, Encoding.Unicode })
        {
            string text;
            try { text = enc.GetString(blob).Trim(); } catch { continue; }
            if (text.StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        return enc.WebName + " JSON {" + string.Join(", ", doc.RootElement.EnumerateObject().Select(p => p.Name).Take(8)) + "}";
                }
                catch { }
            }
            else if (text.StartsWith("sk-ant-")) return enc.WebName + " jeton brut sk-ant-…";
        }
        return "binaire/opaque";
    }

    // HDR-05 : plus aucune conversion d'unité locale — tout passe par UsageNormalization.
    private static string Pct(JsonElement root, string name)
        => root.TryGetProperty(name, out var w) && w.ValueKind == JsonValueKind.Object
           && w.TryGetProperty("utilization", out var u) && u.TryGetDouble(out var p)
           ? UsageNormalization.PourcentagePourAffichage(UsageNormalization.FractionDepuisPourcentage(p)) : "absent";

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
