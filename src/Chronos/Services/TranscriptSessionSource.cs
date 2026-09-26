using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Détecte l'état des sessions Claude Code EN LISANT LEURS TRANSCRIPTS (~/.claude/projects/**/*.jsonl).
/// Ne dépend d'AUCUN hook : c'est la source de base du widget, et elle ne parle QUE de Claude Code.
/// Ne montre que les sessions dont le dernier message a moins de <see cref="HorizonsSessions.Abandon"/> ; la règle
/// de silence n'est PAS ici, elle est dans le moniteur (SIL-01 : une seule règle, appliquée à tous les signaux).
///
/// Règle d'état (dernier message significatif ; sous-agents : jamais une ligne ; le dernier message de leurs
/// transcripts est un signal de TRAVAIL de la session (SUB-01)) — TROIS issues :
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
/// <para>D-28-01 (phase 28) — l'instant du signal. Un transcript est daté par le « timestamp » de sa DERNIÈRE
/// ligne significative (user / assistant), borné par la date d'écriture du fichier, avec repli sur celle-ci si
/// le champ manque ou est illisible — jamais par la seule date d'écriture. Fait mesuré le 2026-09-25 à
/// 16:58:20 : la fermeture de l'app bureau a ajouté des lignes SANS horodatage (bridge-session, last-prompt,
/// cost-state) à douze transcripts dont le dernier vrai message datait de deux heures à deux jours ; les mêmes
/// lignes s'écrivent pendant qu'une question attend. Datés par l'écriture, ces transcripts revenaient
/// « en attente » d'un bloc (fantômes, limite saturée), faisaient avancer l'épisode d'attente du détecteur
/// (une session marquée traitée ressortait sans avoir rien redemandé : NET-03 dévoyé) et faisaient perdre une
/// permission contre un transcript (FUS-01 faussé). La date d'écriture ne sert plus qu'au PRÉ-FILTRE
/// d'énumération, qui reste exact : l'horodatage d'un message ne dépasse jamais l'écriture du fichier. Classer
/// tous les candidats avant la limite coûte ~0,6 ms par fichier supplémentaire (mesuré en recherche).</para>
///
/// <para>SUB-01 (phase 30.1) — un sous-agent qui écrit est un travail de sa session. Écart E2 du constat de phase 31
/// (2026-09-26, 14:44) : le tour parent s'était fini (end_turn) juste après avoir lancé un agent en arrière-plan, qui
/// écrivait encore son transcript douze minutes plus tard ; la session restait « En attente ». Décision D-30.1-05 : APRÈS
/// la classification du parent et le filtre d'abandon, si le parent travaille ou a fini son tour, les
/// « &lt;session&gt;/subagents/agent-*.jsonl » (dossier seul, non récursif) écrits APRÈS le dernier message du parent sont
/// lus par leur queue, à rebours, jusqu'à leur dernier message user / assistant ; le plus récent de ces instants, s'il
/// est postérieur à celui du parent, rend la session Working, datée de cet instant, motif
/// « transcript (sous-agent) » (TravailSousAgent). Sans horodatage lisible sur ce dernier message : aucun signal (pas de
/// repli sur l'écriture). Une question du parent n'est jamais modifiée. Un sous-agent n'est toujours pas une LIGNE
/// (<see cref="EstSousAgent"/>, <see cref="MaxSessions"/> inchangés).</para>
///
/// Lecture EFFICACE : seule la fin du fichier (~64 Ko) est lue (les transcripts font plusieurs Mo).
/// </summary>
// Le contrat ISessionSource n'avait qu'une seule implémentation, la source app-bureau, qui disparaît en
// phase 21. Le porter ici évite deux choses : une interface orpheline, et — c'est le point mécanique —
// un SessionMonitor qui dépendrait du TYPE CONCRET de sa source de base, donc intestable par substitution.
public sealed class TranscriptSessionSource : ISessionSource
{
    private const int MaxSessions = 12;
    private const int TailBytes = 64 * 1024;

    private readonly string _projectsRoot;

    public TranscriptSessionSource(string? projectsRoot = null)
        => _projectsRoot = projectsRoot ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".claude", "projects");

    public IReadOnlyList<SessionSnapshot> Read(System.DateTimeOffset now)
    {
        if (!Directory.Exists(_projectsRoot)) return new List<SessionSnapshot>();

        List<FileInfo> candidats;
        try
        {
            candidats = new DirectoryInfo(_projectsRoot)
                .EnumerateFiles("*.jsonl", SearchOption.AllDirectories)
                .Where(f => !EstSousAgent(f))
                // PRÉ-FILTRE d'économie, jamais l'autorité : l'horodatage d'un message ne dépasse pas l'écriture
                // du fichier, donc un fichier écrit hors fenêtre ne peut contenir aucun signal dans la fenêtre.
                .Where(f => now - new System.DateTimeOffset(f.LastWriteTimeUtc, System.TimeSpan.Zero) <= HorizonsSessions.Abandon)
                .ToList();   // MATÉRIALISÉ dans le try : l'énumération paresseuse levait hors du catch (phase 21)
        }
        catch { return new List<SessionSnapshot>(); }

        // L'AUTORITÉ : l'instant que le signal porte. Tout candidat est classé AVANT la limite — un fichier
        // rajeuni par des métadonnées ne doit plus voler un emplacement à une session réellement récente.
        // Un fichier sans rien d'exploitable (Classify → null) ne consomme AUCUN emplacement.
        return candidats
            .Select(fi => (Fichier: fi, Session: Classify(fi, now)))
            .Where(x => x.Session is not null && now - x.Session.UpdatedAt <= HorizonsSessions.Abandon)   // même borne que le moniteur : n'écarte qu'au-delà
            .Select(x => AvecSesSousAgents(x.Fichier, x.Session!))                                         // SUB-01 : après le parent
            .OrderByDescending(s => s.UpdatedAt)
            .ThenBy(s => s.SessionId, System.StringComparer.Ordinal)   // départage déterministe
            .Take(MaxSessions)                                        // SRC-03 : la limite porte sur les RETENUES
            .ToList();
    }

    // SRC-03 — reconnaît un transcript de SOUS-AGENT par son chemin, avant tout I/O de contenu.
    // Mesuré le 2026-09-12 sur la machine cible : 819 des 870 transcripts (94 %) sont des
    // « <uuid-de-session>/subagents/agent-*.jsonl ». Ce pré-filtre est une ÉCONOMIE, pas l'autorité :
    // l'autorité reste le champ isSidechain lu ligne à ligne dans Classify, qui rattrape un fichier
    // mal rangé ou un agencement de dossiers qui changerait chez Anthropic.
    private static bool EstSousAgent(FileInfo f)
        => string.Equals(f.Directory?.Name, "subagents", System.StringComparison.OrdinalIgnoreCase)
           || f.Name.StartsWith("agent-", System.StringComparison.Ordinal);

    // SUB-01 (phase 30.1) — un sous-agent qui écrit est un travail de sa session. Jamais une LIGNE (EstSousAgent reste),
    // seulement un signal : APRÈS la classification du parent, les subagents/agent-*.jsonl de CETTE session dont l'écriture est
    // postérieure au dernier message du parent (pré-filtre d'économie : rien de plus récent ne peut être écrit dans un fichier
    // plus ancien) ; le plus récent de leurs derniers messages, s'il est postérieur à celui du parent, rend la session Working,
    // datée de cet instant — end_turn compris : le parent va reprendre, et le seuil de silence couvre le cas où il ne reprend pas.
    // Une QUESTION du parent (WaitingAttention) n'est jamais effacée par un sous-agent : même doctrine que le hook (30.1-01).
    private static SessionSnapshot AvecSesSousAgents(FileInfo parent, SessionSnapshot s)
    {
        if (s.Activity is not (SessionActivity.Working or SessionActivity.WaitingTurn)) return s;
        System.DateTimeOffset? plusRecent = null;
        try
        {
            var dossier = new DirectoryInfo(Path.Combine(parent.DirectoryName ?? "",
                                                         Path.GetFileNameWithoutExtension(parent.Name), "subagents"));
            if (!dossier.Exists) return s;
            foreach (var f in dossier.EnumerateFiles("agent-*.jsonl", SearchOption.TopDirectoryOnly))
            {
                if (new System.DateTimeOffset(f.LastWriteTimeUtc, System.TimeSpan.Zero) <= s.UpdatedAt) continue;
                if (DernierMessage(f) is { } t && t > s.UpdatedAt && (plusRecent is null || t > plusRecent)) plusRecent = t;
            }
        }
        catch { return s; }   // un dossier de sous-agents illisible ne coûte rien au parent
        return plusRecent is { } p
            ? s with { Activity = SessionActivity.Working, Reason = TravailSousAgent.MotifTranscript, UpdatedAt = p }
            : s;
    }

    // L'instant du DERNIER message (user / assistant, isSidechain indifférent : tout un sous-agent est « sidechain ») d'un
    // transcript de sous-agent, lu à rebours dans la queue, borné par l'écriture du fichier (D-28-01). Le dernier message fait
    // foi : sans horodatage lisible, AUCUN signal — pas de repli sur l'écriture, ce signal ne sert qu'à dire « travail ».
    private static System.DateTimeOffset? DernierMessage(FileInfo f)
    {
        try
        {
            var lignes = ReadTail(f.FullName, TailBytes).Split('\n');
            int i0 = f.Length > TailBytes ? 1 : 0;   // première ligne tronquée par le seek
            for (int i = lignes.Length - 1; i >= i0; i--)
            {
                if (string.IsNullOrWhiteSpace(lignes[i])) continue;
                JsonElement o;
                try { using var d = JsonDocument.Parse(lignes[i]); o = d.RootElement.Clone(); }
                catch { continue; }
                if (o.ValueKind != JsonValueKind.Object) continue;
                var type = o.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                if (type is not ("user" or "assistant")) continue;
                if (Horodatage(o) is not { } h) return null;
                var ecriture = new System.DateTimeOffset(f.LastWriteTimeUtc, System.TimeSpan.Zero);
                return h <= ecriture ? h : ecriture;
            }
            return null;
        }
        catch { return null; }
    }

    private static SessionSnapshot? Classify(FileInfo fi, System.DateTimeOffset now)
    {
        try
        {
            string tail = ReadTail(fi.FullName, TailBytes);
            var lines = tail.Split('\n');

            string? cwd = null;
            SessionActivity? state = null; // dernier verdict rencontré
            System.DateTimeOffset? instant = null; // horodatage de la DERNIÈRE ligne significative (null : absent ou illisible)

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
                else
                    continue;                        // métadonnées (bridge-session, cost-state…) : ni état ni instant

                instant = Horodatage(o);   // à CHAQUE ligne significative : la dernière l'emporte, null compris
            }

            if (state is null) return null; // aucun message exploitable

            var project = SessionHookProcessor.ProjectFromCwd(cwd);
            var sid = Path.GetFileNameWithoutExtension(fi.Name);
            // Le motif est un FAIT observé : dans un transcript, seule la question produit une attente.
            var reason = state == SessionActivity.WaitingAttention ? "AskUserQuestion" : null;

            // L'instant que le SIGNAL porte (doctrine TRT-02, déjà appliquée par le détecteur), borné par l'écriture
            // du fichier — une source dont l'horloge avance ne doit pas dater un signal dans l'avenir — et repli sur
            // l'écriture si la ligne n'est pas horodatée ou illisible. Décision D-28-01 : la date d'écriture n'est PAS
            // l'instant du signal ; l'app bureau ajoute des lignes de métadonnées sans horodatage à la fermeture et
            // pendant qu'une question attend (relevé du 2026-09-25, 16 h 58 : douze transcripts rajeunis d'un bloc).
            var ecriture = new System.DateTimeOffset(fi.LastWriteTimeUtc, System.TimeSpan.Zero);
            var updatedAt = instant is { } h && h <= ecriture ? h : ecriture;
            return new SessionSnapshot(sid, project, state.Value, reason, updatedAt);
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

    // Le « timestamp » ISO d'une ligne, par le point UNIQUE de conversion (garde HDR-05) ; null si absent ou illisible.
    private static System.DateTimeOffset? Horodatage(JsonElement o)
        => o.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String
            ? UsageNormalization.InstantDepuisIso(ts.GetString())
            : null;

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
