using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve le pipeline de suivi des sessions Claude Code : traduction hook→état (SessionHookProcessor),
/// installation non destructive des hooks avec chemins en slashes avant (SessionHookInstaller), et lecture
/// + staleness honnête (SessionMonitor).
/// </summary>
public class SessionsTests
{
    // --- SessionHookProcessor ---

    [Theory]
    [InlineData("Notification", "WaitingAttention")]
    [InlineData("Stop", "WaitingTurn")]
    [InlineData("UserPromptSubmit", "Working")]
    [InlineData("SessionStart", "Working")]
    public void Hook_mappe_l_evenement_vers_le_bon_etat(string ev, string expected)
    {
        var stdin = """{"session_id":"abc-123","cwd":"C:\\dev\\MonProjet"}""";
        var r = SessionHookProcessor.Process(ev, stdin, 1000);
        Assert.False(r.Ignore);
        Assert.False(r.Delete);
        using var doc = JsonDocument.Parse(r.StateJson!);
        Assert.Equal("abc-123", doc.RootElement.GetProperty("session_id").GetString());
        Assert.Equal("MonProjet", doc.RootElement.GetProperty("project").GetString());
        Assert.Equal(expected, doc.RootElement.GetProperty("activity").GetString());
        Assert.Equal(1000, doc.RootElement.GetProperty("updated_at").GetInt64());
    }

    [Fact]
    public void Hook_SessionEnd_supprime()
    {
        var r = SessionHookProcessor.Process("SessionEnd", """{"session_id":"x"}""", 0);
        Assert.True(r.Delete);
        Assert.Equal("x", r.SessionId);
    }

    [Fact]
    public void Hook_SubagentStop_et_sans_session_id_sont_ignores()
    {
        Assert.True(SessionHookProcessor.Process("SubagentStop", """{"session_id":"x"}""", 0).Ignore);
        Assert.True(SessionHookProcessor.Process("Stop", """{"cwd":"C:\\x"}""", 0).Ignore); // pas de session_id
    }

    [Fact]
    public void Notification_conserve_le_notification_type_en_reason()
    {
        var r = SessionHookProcessor.Process("Notification",
            """{"session_id":"s","cwd":"C:\\p","notification_type":"permission_prompt"}""", 0);
        using var doc = JsonDocument.Parse(r.StateJson!);
        Assert.Equal("permission_prompt", doc.RootElement.GetProperty("reason").GetString());
    }

    // --- SessionHookInstaller ---

    private const string Exe = @"C:\Apps\Chronos.exe";

    [Fact]
    public void Install_ajoute_les_5_hooks_en_slashes_avant()
    {
        var outJson = SessionHookInstaller.TransformForInstall(null, Exe);
        var hooks = (JsonNode.Parse(outJson) as JsonObject)!["hooks"] as JsonObject;
        foreach (var ev in SessionHookInstaller.Events)
        {
            var arr = hooks![ev] as JsonArray;
            Assert.NotNull(arr);
            var cmd = arr![0]!["hooks"]![0]!["command"]!.GetValue<string>();
            Assert.Contains("C:/Apps/Chronos.exe", cmd);   // SLASHES AVANT (leçon terrain)
            Assert.DoesNotContain("\\", cmd);
            Assert.Contains("--hook " + ev, cmd);
        }
    }

    [Fact]
    public void Install_preserve_les_hooks_existants_et_est_idempotent()
    {
        var existing = """
        { "hooks": { "SessionStart": [ { "hooks": [ { "type":"command", "command":"node gsd.js" } ] } ] } }
        """;
        var once = SessionHookInstaller.TransformForInstall(existing, Exe);
        var twice = SessionHookInstaller.TransformForInstall(once, Exe);

        var arr = ((JsonNode.Parse(twice) as JsonObject)!["hooks"]!["SessionStart"] as JsonArray)!;
        // gsd conservé + une SEULE entrée Chronos (idempotent)
        var cmds = arr.Select(e => e!["hooks"]![0]!["command"]!.GetValue<string>()).ToList();
        Assert.Contains(cmds, c => c == "node gsd.js");
        Assert.Single(cmds, c => c.Contains("--hook"));
    }

    [Fact]
    public void Uninstall_retire_seulement_nos_hooks()
    {
        var installed = SessionHookInstaller.TransformForInstall(
            """{ "hooks": { "Stop": [ { "hooks": [ { "type":"command", "command":"node autre.js" } ] } ] } }""", Exe);
        var cleaned = SessionHookInstaller.TransformForUninstall(installed, Exe);

        var stop = (JsonNode.Parse(cleaned) as JsonObject)!["hooks"]!["Stop"] as JsonArray;
        Assert.Single(stop!);
        Assert.Equal("node autre.js", stop![0]!["hooks"]![0]!["command"]!.GetValue<string>());
    }

    // --- SessionMonitor ---

    private static string TempDir() { var d = Path.Combine(Path.GetTempPath(), "chronos-sess-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    private static void WriteState(string dir, string id, SessionActivity a, long ms)
        => File.WriteAllText(Path.Combine(dir, id + ".json"), SessionHookProcessor.BuildStateJson(id, "Proj-" + id, a, null, ms));

    [Fact]
    public void Monitor_lit_les_sessions_et_applique_la_staleness()
    {
        var dir = TempDir();
        var now = DateTimeOffset.UtcNow;
        try
        {
            WriteState(dir, "fresh", SessionActivity.Working, now.ToUnixTimeMilliseconds());
            WriteState(dir, "waiting", SessionActivity.WaitingAttention, now.AddHours(-2).ToUnixTimeMilliseconds()); // attente ancienne mais valide
            WriteState(dir, "staleWork", SessionActivity.Working, now.AddMinutes(-30).ToUnixTimeMilliseconds());     // working périmé
            WriteState(dir, "dead", SessionActivity.WaitingTurn, now.AddHours(-9).ToUnixTimeMilliseconds());          // > drop

            var snaps = new SessionMonitor(dir).Read(now).ToDictionary(s => s.SessionId);

            Assert.Equal(SessionActivity.Working, snaps["fresh"].Activity);
            Assert.Equal(SessionActivity.WaitingAttention, snaps["waiting"].Activity);   // l'attente PERSISTE
            Assert.Equal(SessionActivity.Unknown, snaps["staleWork"].Activity);          // working périmé → Unknown
            Assert.False(snaps.ContainsKey("dead"));                                      // > 8 h → ignoré
        }
        finally { Directory.Delete(dir, true); }
    }
}
