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
/// Règle d'état (dernier message significatif, sous-agents ignorés) — TROIS issues :
///   • dernier = user (invite ou tool_result), ou assistant dont le dernier tool_use est un autre outil → Working
///   • assistant dont le dernier tool_use s'appelle EXACTEMENT « AskUserQuestion »                 → WaitingAttention
///   • assistant SANS tool_use (réponse finie : end_turn/stop_sequence/…)                          → WaitingTurn (t'attend)
///
/// <para>LIB-02 (phase 28) — une question n'est pas une réflexion. Limite honnête : le transcript ne contient
/// pas le prompt de PERMISSION (seul le hook PermissionRequest le voit), mais il contient la question
/// AskUserQuestion, qui attend l'utilisateur au même rang ; le motif du snapshot la nomme (un fait observé).
/// Le nom est comparé ordinalement et exactement, sans liste extensible d'outils « au cas où ». Cas mesuré
/// que la règle ne reconnaît pas (1 question sur 222, 28-RESEARCH.md Q3) : l'appel d'outils PARALLÈLE dont la
/// dernière ligne porte un autre outil (Claude Code écrit un bloc par ligne). Il se dégrade vers le
/// comportement v1.6 (Working), jamais vers une attente inventée.</para>
///
/// <para>SRC-03 (phase 21) — la limite de <see cref="MaxSessions"/> porte sur les sessions RETENUES, pas
/// sur les fichiers examinés. Auparavant elle était appliquée à l'énumération, donc AVANT le filtre des
/// sous-agents : 94 % des transcripts étant des sous-agents (mesure du 2026-09-12 : 819 sur 870), une
/// vague d'agents parallèles consommait les douze emplacements et faisait disparaître la vraie session.</para>
///
/// Lecture EFFICACE : seule la fin du fichier (~64 Ko) est lue (les transcripts font plusieurs Mo).
/// </summary>
// Le contrat ISessionSource n'avait qu'une seule implémentation, la source app-bureau, qui disparaît en
// phase 21. Le porter ici évite deux choses : une interface orpheline, et — c'est le point mécanique —
// un SessionMonitor qui dépendrait du TYPE CONCRET de sa source de base, donc intestable par substitution.
public sealed class TranscriptSessionSource : ISessionSource
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
                    state = DernierOutil(o) switch   // switch sur string : comparaison ORDINALE et EXACTE
                    {
                        null => SessionActivity.WaitingTurn,                         // réponse finie : le tour est terminé
                        "AskUserQuestion" => SessionActivity.WaitingAttention,       // LIB-02 : une question n'est pas une réflexion
                        _ => SessionActivity.Working,                                // un outil est en cours
                    };
                else if (type == "user")
                    state = SessionActivity.Working; // prompt utilisateur OU tool_result → l'assistant va/continue de bosser
            }

            if (state is null) return null; // aucun message exploitable

            var project = SessionHookProcessor.ProjectFromCwd(cwd);
            var sid = Path.GetFileNameWithoutExtension(fi.Name);
            // Le motif est un FAIT observé : dans un transcript, seule la question produit une attente.
            var reason = state == SessionActivity.WaitingAttention ? "AskUserQuestion" : null;
            return new SessionSnapshot(sid, project, state.Value, reason,
                new System.DateTimeOffset(fi.LastWriteTimeUtc, System.TimeSpan.Zero));
        }
        catch { return null; }
    }

    // Nom du DERNIER bloc tool_use du message : Claude Code écrit un bloc par ligne, et c'est le dernier qui dit
    // ce que la session fait maintenant. "" si ce bloc n'a pas de nom lisible (c'est encore un outil en cours),
    // null s'il n'y a aucun bloc tool_use (réponse finie). Les ValueKind sont vérifiés : un « message » qui ne
    // serait pas un objet ne doit pas faire lever TryGetProperty et jeter tout le fichier.
    private static string? DernierOutil(JsonElement o)
    {
        if (!o.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.Object
            || !m.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return null;
        string? dernier = null;
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object) continue;
            if (!block.TryGetProperty("type", out var bt) || bt.ValueKind != JsonValueKind.String
                || bt.GetString() != "tool_use") continue;
            dernier = block.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString() ?? "" : "";
        }
        return dernier;
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
