using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Détecte l'état des sessions Claude Code EN LISANT LEURS TRANSCRIPTS (~/.claude/projects/**/*.jsonl).
/// Ne dépend d'AUCUN hook : c'est la source de base du widget, et elle ne parle QUE de Claude Code.
/// Ne montre que les sessions récemment actives (fenêtre <see cref="ActiveWindow"/>).
///
/// Règle d'état (dernier message significatif, sous-agents ignorés) :
///   • assistant AVEC un tool_use (pas encore de résultat) / dernier = user ou tool_result → Working
///   • assistant SANS outil en cours (réponse finie : end_turn/stop_sequence/…)               → WaitingTurn (t'attend)
/// Limite honnête : le transcript NE contient PAS l'état « attend une permission » (WaitingAttention) ;
/// on n'affiche donc que Working / WaitingTurn.
///
/// <para>SRC-03 (phase 21) — la limite de <see cref="MaxSessions"/> porte sur les sessions RETENUES, pas
/// sur les fichiers examinés. Auparavant elle était appliquée à l'énumération, donc AVANT le filtre des
/// sous-agents : 94 % des transcripts étant des sous-agents (mesure du 2026-09-12 : 819 sur 870), une
/// vague d'agents parallèles consommait les douze emplacements et faisait disparaître la vraie session.</para>
///
/// Lecture EFFICACE : seule la fin du fichier (~64 Ko) est lue (les transcripts font plusieurs Mo).
/// </summary>
public sealed class TranscriptSessionSource
{
    private static readonly System.TimeSpan ActiveWindow = System.TimeSpan.FromMinutes(15);
    private const int MaxSessions = 12;
    private const int TailBytes = 64 * 1024;

    private readonly string _projectsRoot;

    public TranscriptSessionSource(string? projectsRoot = null)
        => _projectsRoot = projectsRoot ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".claude", "projects");

    public IReadOnlyList<SessionSnapshot> Read(System.DateTimeOffset now)
    {
        var result = new List<SessionSnapshot>();
        if (!Directory.Exists(_projectsRoot)) return result;

        List<FileInfo> recents;
        try
        {
            recents = new DirectoryInfo(_projectsRoot)
                .EnumerateFiles("*.jsonl", SearchOption.AllDirectories)
                .Where(f => !EstSousAgent(f))
                .Where(f => now - new System.DateTimeOffset(f.LastWriteTimeUtc, System.TimeSpan.Zero) < ActiveWindow)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();   // MATÉRIALISÉ ici, dans le try : l'énumération était paresseuse, donc une erreur
                             // d'accès disque survenait DANS le foreach, hors de ce catch, et remontait.
        }
        catch { return result; }

        foreach (var fi in recents)
        {
            var snap = Classify(fi, now);
            if (snap is null) continue;                 // rien d'exploitable → ne consomme AUCUN emplacement
            result.Add(snap);
            if (result.Count >= MaxSessions) break;     // SRC-03 : la limite porte sur les sessions RETENUES
        }
        return result;
    }

    // SRC-03 — reconnaît un transcript de SOUS-AGENT par son chemin, avant tout I/O de contenu.
    // Mesuré le 2026-09-12 sur la machine cible : 819 des 870 transcripts (94 %) sont des
    // « <uuid-de-session>/subagents/agent-*.jsonl ». Ce pré-filtre est une ÉCONOMIE, pas l'autorité :
    // l'autorité reste le champ isSidechain lu ligne à ligne dans Classify, qui rattrape un fichier
    // mal rangé ou un agencement de dossiers qui changerait chez Anthropic.
    private static bool EstSousAgent(FileInfo f)
        => string.Equals(f.Directory?.Name, "subagents", System.StringComparison.OrdinalIgnoreCase)
           || f.Name.StartsWith("agent-", System.StringComparison.Ordinal);

    private static SessionSnapshot? Classify(FileInfo fi, System.DateTimeOffset now)
    {
        try
        {
            string tail = ReadTail(fi.FullName, TailBytes);
            var lines = tail.Split('\n');

            string? cwd = null;
            SessionActivity? state = null; // dernier verdict rencontré

            // On saute la 1re ligne SEULEMENT si le fichier a été tronqué par le seek (sinon elle est complète).
            int i0 = fi.Length > TailBytes ? 1 : 0;
            for (int i = i0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                JsonElement o;
                try { using var d = JsonDocument.Parse(line); o = d.RootElement.Clone(); }
                catch { continue; }
                if (o.ValueKind != JsonValueKind.Object) continue;

                if (o.TryGetProperty("cwd", out var c) && c.ValueKind == JsonValueKind.String) cwd = c.GetString();
                if (o.TryGetProperty("isSidechain", out var sc) && sc.ValueKind == JsonValueKind.True) continue; // sous-agent

                var type = o.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                if (type == "assistant")
                    state = HasToolUse(o) ? SessionActivity.Working : SessionActivity.WaitingTurn;
                else if (type == "user")
                    state = SessionActivity.Working; // prompt utilisateur OU tool_result → l'assistant va/continue de bosser
            }

            if (state is null) return null; // aucun message exploitable

            var project = SessionHookProcessor.ProjectFromCwd(cwd);
            var sid = Path.GetFileNameWithoutExtension(fi.Name);
            return new SessionSnapshot(sid, project, state.Value, null,
                new System.DateTimeOffset(fi.LastWriteTimeUtc, System.TimeSpan.Zero));
        }
        catch { return null; }
    }

    // Un message assistant contient-il un bloc tool_use dans son content ? (→ exécution en cours)
    private static bool HasToolUse(JsonElement o)
    {
        if (!o.TryGetProperty("message", out var m) || !m.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.Array) return false;
        foreach (var block in content.EnumerateArray())
            if (block.TryGetProperty("type", out var bt) && bt.ValueKind == JsonValueKind.String && bt.GetString() == "tool_use")
                return true;
        return false;
    }

    // Lit au plus les derniers <paramref name="bytes"/> octets du fichier (transcripts volumineux).
    private static string ReadTail(string path, int bytes)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var start = System.Math.Max(0, fs.Length - bytes);
        fs.Seek(start, SeekOrigin.Begin);
        using var sr = new StreamReader(fs, System.Text.Encoding.UTF8);
        return sr.ReadToEnd();
    }
}
