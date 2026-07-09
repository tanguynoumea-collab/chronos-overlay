using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Diagnostic auto-explicatif (observabilité pour un outil distribué) : dit POURQUOI l'affichage
/// n'a pas de couleurs sur une machine donnée. Rassemble l'état réel — token trouvé ? statut de
/// l'appel OAuth ? sources présentes ? plafonds ? source active par fenêtre — et écrit un rapport
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

    public DiagnosticService(IClaudeTokenReader tokenReader, ChronosPaths paths,
                             SettingsService settings, IUsageProvider composite, IClock clock)
    {
        _tokenReader = tokenReader;
        _paths = paths;
        _settings = settings;
        _composite = composite;
        _clock = clock;
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
        var sb = new StringBuilder();
        sb.AppendLine("=== Chronos — Diagnostic ===");
        sb.AppendLine("Date : " + _clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();

        // 1) Réglage
        sb.AppendLine("[Réglage]");
        sb.AppendLine("  Usage exact (OAuth) : " + (s.OAuthUsageEnabled ? "ACTIVÉ" : "DÉSACTIVÉ (menu)"));
        sb.AppendLine();

        // 2) Source exacte PRIMAIRE : pont statusLine Claude Code (usage.json). Voie universelle recommandée.
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
                string W(string w) => r.TryGetProperty(w, out var o) && o.TryGetProperty("used_percentage", out var p) && p.TryGetDouble(out var v)
                    ? v.ToString("F0", CultureInfo.InvariantCulture) + " %" : "absent";
                string age = "inconnu";
                if (r.TryGetProperty("capturedAt", out var ca) && ca.TryGetInt64(out var ms))
                {
                    var mins = (_clock.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(ms)).TotalMinutes;
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
            foreach (var hit in FindTokenVaults(root))
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

        // 3) Estimation — JSONL + plafonds
        sb.AppendLine("[Estimation — transcripts JSONL (repli)]");
        var projects = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
        int jsonl = 0;
        try { if (Directory.Exists(projects)) jsonl = Directory.EnumerateFiles(projects, "*.jsonl", SearchOption.AllDirectories).Take(5000).Count(); } catch { }
        sb.AppendLine("  Dossier ~/.claude/projects : " + (Directory.Exists(projects) ? jsonl + " fichier(s) .jsonl" : "ABSENT (aucun historique local)"));
        sb.AppendLine("  Plafond 5 h   : " + (s.FiveHourTokenBudget?.ToString("N0", CultureInfo.CurrentCulture) ?? "non défini (→ pas de couleur en estimation)"));
        sb.AppendLine("  Plafond hebdo : " + (s.WeeklyTokenBudget?.ToString("N0", CultureInfo.CurrentCulture) ?? "non défini (→ pas de couleur en estimation)"));
        sb.AppendLine();

        // 4) Résultat effectivement affiché (via le composite réel)
        sb.AppendLine("[Ce qui est affiché maintenant]");
        try
        {
            var snap = await _composite.GetAsync(ct);
            sb.AppendLine("  5 h   : " + Describe(snap.FiveHour));
            sb.AppendLine("  Hebdo : " + Describe(snap.SevenDay));
        }
        catch (Exception ex) { sb.AppendLine("  (échec de lecture : " + ex.Message + ")"); }
        sb.AppendLine();

        // 5) Conseil
        sb.AppendLine("[Conseil]");
        if (!bridgeInstalled)
            sb.AppendLine("  → Active « Source exacte (Claude Code) » dans le menu (clic droit). Chronos s'intègre à\n" +
                          "    Claude Code : les vrais pourcentages 5 h/hebdo s'affichent dès ton prochain message.");
        else
            sb.AppendLine("  → Intégration active. Si usage.json est absent, envoie un message dans Claude Code :\n" +
                          "    la barre de statut se met à jour à ce moment-là et alimente le cadran.");
        sb.AppendLine("  (Repli : « Calibrer les plafonds… » colore une estimation quand aucune source exacte n'est là.)");

        return sb.ToString();
    }

    // Cherche (profondeur bornée, dossiers volumineux ignorés) les fichiers config.json contenant
    // « oauth:tokenCache » → révèle où l'app bureau range son coffre, quel que soit son nom/emplacement.
    private static IEnumerable<string> FindTokenVaults(string root)
    {
        var results = new List<string>();
        void Scan(string dir, int depth)
        {
            if (depth > 3 || results.Count >= 5) return;
            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); } catch { return; }
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir, "config.json"))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        if (fi.Length > 2_000_000) continue;                 // pas un config.json d'app
                        if (File.ReadAllText(f).Contains("oauth:tokenCache")) { results.Add(f); if (results.Count >= 5) return; }
                    }
                    catch { }
                }
            }
            catch { }
            foreach (var sub in subdirs)
            {
                var n = Path.GetFileName(sub).ToLowerInvariant();
                if (n is "cache" or "gpucache" or "code cache" or "node_modules" or "blob_storage"
                      or "logs" or "crashpad" or "dawncache" or "service worker") continue; // bruit volumineux
                Scan(sub, depth + 1);
                if (results.Count >= 5) return;
            }
        }
        Scan(root, 0);
        return results;
    }

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

    private static string Pct(JsonElement root, string name)
        => root.TryGetProperty(name, out var w) && w.ValueKind == JsonValueKind.Object
           && w.TryGetProperty("utilization", out var u) && u.TryGetDouble(out var p)
           ? p.ToString("F0", CultureInfo.InvariantCulture) + " %" : "absent";

    private static string Describe(WindowState w)
        => w.Reliability switch
        {
            SourceReliability.Exact => "EXACT — " + (w.Utilization is { } u ? (u * 100).ToString("F0") + " %" : "?"),
            SourceReliability.Estimated => "estimé — " + (w.Utilization is { } u ? "~" + (u * 100).ToString("F0") + " %" : "% inconnu (pas de plafond → gris)"),
            _ => "indisponible",
        };
}
