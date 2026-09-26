using System.IO;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>Résultat du traitement d'un événement de hook : soit un upsert (contenu du fichier d'état),
/// soit une suppression (fin de session), soit rien (événement ignoré).
/// <para><c>BattementSousAgent</c> : battement (<c>PreToolUse</c> / <c>PostToolUse</c>) émis par un SOUS-AGENT —
/// l'écriture ne réaffirme alors le travail que sur <c>Working</c> ou <c>WaitingTurn</c> (SUB-01, D-30.1-03).
/// Paramètre optionnel : les constructions existantes compilent sans retouche.</para></summary>
public sealed record SessionHookResult(string? SessionId, bool Delete, string? StateJson, bool Ignore, bool BattementSousAgent = false)
{
    public static SessionHookResult Ignored { get; } = new(null, false, null, true);
}

/// <summary>
/// Cœur PUR (testable) qui traduit un événement de hook Claude Code (+ son JSON stdin) en action sur le
/// fichier d'état de session. Prouvé sur la vraie machine : les hooks Notification/Stop/UserPromptSubmit/
/// SessionStart/SessionEnd portent <c>session_id</c> et <c>cwd</c> sur stdin.
///
/// Sémantique RÉELLEMENT produite :
///   PermissionRequest                 → WaitingAttention (une permission est DEMANDÉE : exact, immédiat)
///   Notification (3 types de demande) → WaitingAttention (filtré par matcher ; veto sur les neuf autres)
///   Stop                              → WaitingTurn (le tour s'est terminé, c'est OBSERVÉ)
///   UserPromptSubmit / SessionStart   → Working
///   PreToolUse / PostToolUse          → Working (battement de cœur)
///   SessionEnd                        → suppression du fichier
///   sous-agent (agent_id / agent_type présent)
///                                     → cycle de vie (SessionStart, SessionEnd, Stop, UserPromptSubmit)
///                                       ignoré ; battement → Working pour la session parente, motif suffixé
///                                       « (sous-agent) », qui ne réaffirme que Working ou WaitingTurn
///                                       (EcritureEtatSession) ; demande de permission ou du bus →
///                                       WaitingAttention, motif ordinaire
///   inconnu                           → ignoré
///
/// <para><b>La doctrine des battements, en une phrase :</b> un battement dit « ça travaillait à cet
/// instant », JAMAIS « ça travaille encore maintenant ». C'est pour cela que le seuil de silence du
/// moniteur subsiste, et qu'il porte désormais ce nom-là.</para>
/// </summary>
public static class SessionHookProcessor
{
    private static readonly JsonSerializerOptions Tolerant = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Les types du bus de notifications qui ne disent RIEN de ce que fait la session.
    /// Relevé du 2026-09-12 : le bus en compte douze, et Chronos les réduisait TOUS à un seul état —
    /// une authentification réussie et une reprise de quota fabriquaient donc une attente.
    /// Le filtre PRIMAIRE est le matcher posé dans settings.json ; ceci n'en est que le VETO de
    /// second rideau, qui protège aussi les réglages d'une version antérieure, encore sans matcher.</summary>
    private static readonly string[] NotificationsSansEtat =
    {
        "permission_prompt", "idle_prompt", "auth_success",
        "elicitation_complete", "elicitation_response", "agent_completed",
        "quota_auto_resume_fired", "quota_auto_resume_stale", "quota_auto_resume_disabled",
    };

    /// <summary>SUB-01 — le suffixe qui marque, dans le motif du fichier d'état, un battement émis par un SOUS-AGENT :
    /// « PreToolUse (sous-agent) », « PostToolUse (sous-agent) ». Le moniteur reconnaît ce suffixe (TravailSousAgent, plan
    /// 30.1-02) ; une garde croisée tient l'égalité des deux constantes (plan 30.1-03). Une DEMANDE de sous-agent
    /// (PermissionRequest, Notification) ne le porte jamais : c'est une attente, pas un travail.</summary>
    public const string SuffixeSousAgent = " (sous-agent)";

    public static SessionHookResult Process(string? eventName, string? stdinJson, long nowMs)
    {
        string sid = "", cwd = "", notifType = "", agentId = "", agentType = "";
        try
        {
            if (!string.IsNullOrWhiteSpace(stdinJson))
            {
                using var doc = JsonDocument.Parse(stdinJson!, new JsonDocumentOptions
                { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                var r = doc.RootElement;
                if (r.ValueKind == JsonValueKind.Object)
                {
                    sid = Str(r, "session_id");
                    cwd = Str(r, "cwd");
                    notifType = Str(r, "notification_type");
                    // Str rend une chaîne VIDE pour un champ absent ou non textuel — c'est exactement le
                    // comportement voulu : un marqueur illisible ne doit pas fabriquer un veto.
                    agentId = Str(r, "agent_id");
                    agentType = Str(r, "agent_type");
                    // l'événement peut aussi venir du stdin plutôt que de l'argument
                    if (string.IsNullOrEmpty(eventName)) eventName = Str(r, "hook_event_name");
                }
            }
        }
        catch (JsonException) { /* stdin illisible → on retombe sur l'argument event */ }

        if (string.IsNullOrEmpty(sid)) return SessionHookResult.Ignored; // sans session_id, rien à faire

        var ev = (eventName ?? "").Trim();

        // PIÈGE SRC-03 CÔTÉ HOOKS. Un sous-agent porte le MÊME identifiant de session que son parent
        // (relevé du 2026-09-12) et ne s'en distingue QUE par la présence de ces deux champs. Sur cette
        // machine, quatre-vingt-quatorze pour cent des transcripts sont des sous-agents.
        //
        // SUB-01 (phase 30.1) — LA DOCTRINE, en deux phrases. Un sous-agent ne parle jamais du CYCLE DE VIE
        // de son parent : SessionStart, SessionEnd, Stop et UserPromptSubmit émis par lui restent ignorés
        // (D-30.1-01). Mais son ACTIVITÉ EST celle de la session : une session dont le tour est fini et dont
        // un agent tourne en arrière-plan « réfléchit » (écart E2 du constat de phase 31, relevé de 14:44).
        // Ses deux battements passent donc, comme ses deux demandes (permission, bus) qui réclament MON
        // intervention depuis la phase 25.
        //
        // La brèche que le veto de la phase 25 fermait par le silence — une vague d'agents écrasant un
        // « à toi » encore en attente (R3) — se ferme désormais AU BON ENDROIT : dans l'écriture, sous le
        // verrou, où un battement de sous-agent ne réaffirme qu'un travail ou un tour fini
        // (EcritureEtatSession, D-30.1-03). C'est le drapeau BattementSousAgent qui l'y porte.
        //
        // Le placement est ESSENTIEL et inchangé : après le garde session_id, et AVANT le court-circuit
        // SessionEnd — un SessionEnd de sous-agent ne doit jamais supprimer le fichier d'état de son parent.
        var estSousAgent = agentId.Length > 0 || agentType.Length > 0;
        if (estSousAgent && ev is not ("PermissionRequest" or "Notification" or "PreToolUse" or "PostToolUse"))
            return SessionHookResult.Ignored;
        var battementSousAgent = estSousAgent && ev is ("PreToolUse" or "PostToolUse");

        if (ev is "SessionEnd") return new SessionHookResult(sid, Delete: true, null, false);

        // VETO de second rideau. Le sens EXACT de ce test : il ne produit JAMAIS d'état, il n'en retire
        // que. On ne lit ce champ que pour ÉCARTER, jamais pour conclure — c'est ce qui rend le code
        // robuste au fait que son nom n'est pas confirmable (page de référence tronquée). S'il changeait,
        // on retomberait simplement sur le tri par matcher, sans jamais fabriquer d'attente.
        if (ev is "Notification"
            && notifType.Length > 0
            && System.Array.IndexOf(NotificationsSansEtat, notifType) >= 0)
            return SessionHookResult.Ignored;

        SessionActivity? activity = ev switch
        {
            "PermissionRequest" => SessionActivity.WaitingAttention, // le signal DÉDIÉ, exact et immédiat
            "Notification" => SessionActivity.WaitingAttention,
            "Stop" => SessionActivity.WaitingTurn,
            "UserPromptSubmit" => SessionActivity.Working,
            "SessionStart" => SessionActivity.Working,
            "PreToolUse" => SessionActivity.Working,   // battement de cœur (EVT-03)
            "PostToolUse" => SessionActivity.Working,  // battement de cœur (EVT-03)
            _ => null, // SubagentStop, PostToolBatch, inconnu → ignorer
        };
        if (activity is null) return SessionHookResult.Ignored;

        // Le motif inscrit dans le fichier d'état : pour Notification, le type quand il est lisible ; pour
        // PermissionRequest et les deux battements, le NOM DE L'ÉVÉNEMENT. Deux lectures du relevé se sont
        // contredites sur le nom du champ de contexte de permission : rien ne doit s'y appuyer, et aucun
        // champ spécifique à un battement n'est confirmable non plus. Le nom de l'événement, lui, est un
        // fait observé — c'est nous qui l'avons câblé. Un battement de SOUS-AGENT porte en plus le suffixe
        // SuffixeSousAgent (D-30.1-02) : le moniteur et le diagnostic savent d'où vient ce travail. Une
        // DEMANDE de sous-agent garde son motif ordinaire : c'est une attente, pas un travail.
        var motif = ev switch
        {
            "Notification" => notifType,
            "PreToolUse" or "PostToolUse" when battementSousAgent => ev + SuffixeSousAgent,
            "PermissionRequest" or "PreToolUse" or "PostToolUse" => ev,
            _ => null,
        };

        var json = BuildStateJson(sid, ProjectFromCwd(cwd), activity.Value, motif, nowMs);
        return new SessionHookResult(sid, Delete: false, json, false, BattementSousAgent: battementSousAgent);
    }

    /// <summary>Contenu du fichier d'état (schéma lu par <see cref="SessionMonitor"/>).</summary>
    public static string BuildStateJson(string sessionId, string project, SessionActivity activity, string? reason, long updatedAtMs)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("session_id", sessionId);
            w.WriteString("project", project);
            w.WriteString("activity", activity.ToString());
            if (!string.IsNullOrEmpty(reason)) w.WriteString("reason", reason);
            w.WriteNumber("updated_at", updatedAtMs);
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    // Nom lisible = dernier segment du cwd (le dossier projet). Vide → « (session) ».
    internal static string ProjectFromCwd(string? cwd)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return "(session)";
        var trimmed = cwd.Replace('\\', '/').TrimEnd('/');
        var i = trimmed.LastIndexOf('/');
        var name = i >= 0 ? trimmed[(i + 1)..] : trimmed;
        return string.IsNullOrEmpty(name) ? "(session)" : name;
    }

    private static string Str(JsonElement o, string key)
        => o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
