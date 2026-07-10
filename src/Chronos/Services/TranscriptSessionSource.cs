using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Détecte l'état des sessions Claude Code EN LISANT LEURS TRANSCRIPTS (~/.claude/projects/**/*.jsonl),
/// ce que l'app DESKTOP écrit aussi (pas seulement le terminal). Ne dépend d'AUCUN hook → couvre l'usage
/// bureau. Ne montre que les sessions récemment actives (fenêtre <see cref="ActiveWindow"/>).
///
/// Règle d'état (dernier message significatif, sous-agents ignorés) :
///   • assistant AVEC un tool_use (pas encore de résultat) / dernier = user ou tool_result → Working
///   • assistant SANS outil en cours (réponse finie : end_turn/stop_sequence/…)               → WaitingTurn (t'attend)
/// Limite honnête : le transcript NE contient PAS l'état « attend une permission » (WaitingAttention),
/// non détectable côté bureau ; on n'affiche donc que Working / WaitingTurn.
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

        IEnumerable<FileInfo> recent;
        try
        {
            recent = new DirectoryInfo(_projectsRoot)
                .EnumerateFiles("*.jsonl", SearchOption.AllDirectories)
                .Where(f => now - new System.DateTimeOffset(f.LastWriteTimeUtc, System.TimeSpan.Zero) < ActiveWindow)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(MaxSessions);
        }
        catch { return result; }

        foreach (var fi in recent)
        {
            var snap = Classify(fi, now);
            if (snap is not null) result.Add(snap);
        }
        return result;
    }

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
