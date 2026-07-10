using System.IO;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>Résultat du traitement d'un événement de hook : soit un upsert (contenu du fichier d'état),
/// soit une suppression (fin de session), soit rien (événement ignoré).</summary>
public sealed record SessionHookResult(string? SessionId, bool Delete, string? StateJson, bool Ignore)
{
    public static SessionHookResult Ignored { get; } = new(null, false, null, true);
}

/// <summary>
/// Cœur PUR (testable) qui traduit un événement de hook Claude Code (+ son JSON stdin) en action sur le
/// fichier d'état de session. Prouvé sur la vraie machine : les hooks Notification/Stop/UserPromptSubmit/
/// SessionStart/SessionEnd portent <c>session_id</c> et <c>cwd</c> sur stdin.
///
/// Sémantique :
///   Notification (permission/idle/agent_needs_input…) → WaitingAttention (réclame une action MAINTENANT)
///   Stop                                              → WaitingTurn (a fini, attend ton message)
///   UserPromptSubmit / SessionStart                   → Working
///   SessionEnd                                        → suppression du fichier
///   SubagentStop / inconnu                            → ignoré (activité d'un sous-agent, pas la session)
/// </summary>
public static class SessionHookProcessor
{
    private static readonly JsonSerializerOptions Tolerant = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static SessionHookResult Process(string? eventName, string? stdinJson, long nowMs)
    {
        string sid = "", cwd = "", notifType = "";
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
                    // l'événement peut aussi venir du stdin plutôt que de l'argument
                    if (string.IsNullOrEmpty(eventName)) eventName = Str(r, "hook_event_name");
                }
            }
        }
        catch (JsonException) { /* stdin illisible → on retombe sur l'argument event */ }

        if (string.IsNullOrEmpty(sid)) return SessionHookResult.Ignored; // sans session_id, rien à faire

        var ev = (eventName ?? "").Trim();
        if (ev is "SessionEnd") return new SessionHookResult(sid, Delete: true, null, false);

        SessionActivity? activity = ev switch
        {
            "Notification" => SessionActivity.WaitingAttention,
            "Stop" => SessionActivity.WaitingTurn,
            "UserPromptSubmit" => SessionActivity.Working,
            "SessionStart" => SessionActivity.Working,
            _ => null, // SubagentStop, PostToolUse, inconnu → ignorer
        };
        if (activity is null) return SessionHookResult.Ignored;

        var json = BuildStateJson(sid, ProjectFromCwd(cwd), activity.Value, ev == "Notification" ? notifType : null, nowMs);
        return new SessionHookResult(sid, Delete: false, json, false);
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
