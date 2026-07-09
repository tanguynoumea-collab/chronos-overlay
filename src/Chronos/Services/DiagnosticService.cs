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

        // 2) Source exacte — OAuth
        sb.AppendLine("[Source exacte — endpoint OAuth]");
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
        if (token is null)
            sb.AppendLine("  Pas de token → pas de chiffres exacts. Installe et connecte l'app bureau Claude sur cette machine,");
        sb.AppendLine("  ou renseigne tes plafonds via le menu « Calibrer les plafonds… » pour colorer l'estimation.");

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
