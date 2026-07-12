using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chronos.Services;
using Chronos.ViewModels;
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

    // --- TranscriptSessionSource (détection app bureau, sans hooks) ---

    private static string WriteTranscript(string root, string session, string[] lines, TimeSpan ago)
    {
        var projDir = Path.Combine(root, "C--dev-MonProjet");
        Directory.CreateDirectory(projDir);
        var f = Path.Combine(projDir, session + ".jsonl");
        File.WriteAllText(f, string.Join("\n", lines) + "\n");
        File.SetLastWriteTimeUtc(f, DateTime.UtcNow - ago);
        return f;
    }

    private const string CwdLine = """{"type":"user","cwd":"C:\\dev\\MonProjet","message":{"role":"user","content":[{"type":"text","text":"salut"}]}}""";
    private const string AssistantToolUse = """{"type":"assistant","message":{"role":"assistant","stop_reason":"tool_use","content":[{"type":"tool_use","name":"Bash"}]}}""";
    private const string AssistantEndTurn = """{"type":"assistant","message":{"role":"assistant","stop_reason":"end_turn","content":[{"type":"text","text":"fini"}]}}""";

    [Fact]
    public void Transcript_dernier_assistant_end_turn_est_WaitingTurn()
    {
        var root = TempDir();
        try
        {
            WriteTranscript(root, "s-wait", new[] { CwdLine, AssistantToolUse, AssistantEndTurn }, TimeSpan.FromMinutes(2));
            var snap = new TranscriptSessionSource(root).Read(DateTimeOffset.UtcNow).Single();
            Assert.Equal("s-wait", snap.SessionId);
            Assert.Equal("MonProjet", snap.Project);
            Assert.Equal(SessionActivity.WaitingTurn, snap.Activity);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Transcript_dernier_assistant_tool_use_est_Working()
    {
        var root = TempDir();
        try
        {
            WriteTranscript(root, "s-work", new[] { CwdLine, AssistantEndTurn, CwdLine, AssistantToolUse }, TimeSpan.FromMinutes(1));
            var snap = new TranscriptSessionSource(root).Read(DateTimeOffset.UtcNow).Single();
            Assert.Equal(SessionActivity.Working, snap.Activity);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Transcript_trop_ancien_est_ignore()
    {
        var root = TempDir();
        try
        {
            WriteTranscript(root, "s-old", new[] { CwdLine, AssistantEndTurn }, TimeSpan.FromMinutes(30)); // > 15 min
            Assert.Empty(new TranscriptSessionSource(root).Read(DateTimeOffset.UtcNow));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Monitor_fusionne_transcripts_et_hooks_hook_prioritaire()
    {
        var hookDir = TempDir();
        var projRoot = TempDir();
        var now = DateTimeOffset.UtcNow;
        try
        {
            // Même session_id des deux côtés : le hook (WaitingAttention) doit primer sur le transcript (Working).
            WriteTranscript(projRoot, "dup", new[] { CwdLine, AssistantToolUse }, TimeSpan.FromMinutes(1));
            WriteState(hookDir, "dup", SessionActivity.WaitingAttention, now.ToUnixTimeMilliseconds());

            var snaps = new SessionMonitor(hookDir, new TranscriptSessionSource(projRoot)).Read(now);
            Assert.Single(snaps);
            Assert.Equal(SessionActivity.WaitingAttention, snaps[0].Activity); // hook prioritaire
        }
        finally { Directory.Delete(hookDir, true); Directory.Delete(projRoot, true); }
    }

    [Fact]
    public void Archive_retire_la_session_de_l_affichage()
    {
        var hookDir = TempDir();
        var archPath = Path.Combine(TempDir(), "archived.json");
        var now = DateTimeOffset.UtcNow;
        try
        {
            WriteState(hookDir, "keep", SessionActivity.WaitingTurn, now.ToUnixTimeMilliseconds());
            WriteState(hookDir, "bye", SessionActivity.WaitingTurn, now.ToUnixTimeMilliseconds());
            var archive = new ArchiveStore(archPath);
            var monitor = new SessionMonitor(hookDir, new TranscriptSessionSource(TempDir()), archive);

            Assert.Equal(2, monitor.Read(now).Count);
            archive.Add("bye");
            var after = monitor.Read(now);
            Assert.Single(after);
            Assert.Equal("keep", after[0].SessionId);
        }
        finally { Directory.Delete(hookDir, true); }
    }

    // Faux ISessionSource : rend une liste FIXE de snapshots (simule le cache de la source bureau UIA,
    // sans aucune fenêtre Claude ni dépendance UIA). Sert à prouver la fusion dans SessionMonitor.
    private sealed class FakeSessionSource : ISessionSource
    {
        private readonly IReadOnlyList<SessionSnapshot> _snaps;
        public FakeSessionSource(params SessionSnapshot[] snaps) => _snaps = snaps;
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => _snaps;
    }

    [Fact]
    public void Monitor_source_bureau_ignoree_si_sessions_locales_presentes()
    {
        // ANTI-DOUBLON : la source bureau (UIA) est un REPLI. Les transcripts couvrent déjà l'app bureau,
        // donc fusionner l'UIA quand une session locale existe re-listerait la MÊME session sous une clé
        // `desktop:...` → doublon. Attendu : seule la session locale, l'entrée bureau écartée.
        var hookDir = TempDir();
        var projRoot = TempDir();
        var now = DateTimeOffset.UtcNow;
        try
        {
            WriteTranscript(projRoot, "cli-1", new[] { CwdLine, AssistantToolUse }, TimeSpan.FromMinutes(1));
            var bureau = new FakeSessionSource(new SessionSnapshot(
                "desktop:foreground:chat", "Claude (bureau)", SessionActivity.WaitingTurn, null, now,
                SessionKind.Chat, SessionOrigin.Desktop));

            var monitor = new SessionMonitor(hookDir, new TranscriptSessionSource(projRoot),
                new ArchiveStore(Path.Combine(TempDir(), "a.json")), bureau);
            var snaps = monitor.Read(now).ToDictionary(s => s.SessionId);

            Assert.Single(snaps);
            Assert.Equal(SessionActivity.Working, snaps["cli-1"].Activity);
            Assert.Equal(SessionOrigin.Cli, snaps["cli-1"].Origin);
            Assert.False(snaps.ContainsKey("desktop:foreground:chat")); // doublon écarté
        }
        finally { Directory.Delete(hookDir, true); Directory.Delete(projRoot, true); }
    }

    [Fact]
    public void Monitor_source_bureau_utilisee_en_repli_si_aucune_session_locale()
    {
        // Aucune session locale (transcript/hook) → la source bureau alimente la liste (ex. Cowork VM pur,
        // distant, sans transcript local). Le repli reste donc utile là où il n'y a pas de doublon possible.
        var now = DateTimeOffset.UtcNow;
        var bureau = new FakeSessionSource(new SessionSnapshot(
            "desktop:session:X", "X", SessionActivity.Working, null, now, SessionKind.Cowork, SessionOrigin.Desktop));
        var monitor = new SessionMonitor(TempDir(), new TranscriptSessionSource(TempDir()),
            new ArchiveStore(Path.Combine(TempDir(), "a.json")), bureau);

        var snaps = monitor.Read(now).ToDictionary(s => s.SessionId);
        Assert.Single(snaps);
        Assert.True(snaps.ContainsKey("desktop:session:X"));
    }

    [Fact]
    public void Monitor_sans_source_bureau_ne_regresse_pas()
    {
        // desktop = null (défaut) → comportement identique à aujourd'hui (sessions CLI seules), aucun crash.
        var projRoot = TempDir();
        var now = DateTimeOffset.UtcNow;
        try
        {
            WriteTranscript(projRoot, "cli-only", new[] { CwdLine, AssistantEndTurn }, TimeSpan.FromMinutes(1));
            var snaps = new SessionMonitor(TempDir(), new TranscriptSessionSource(projRoot)).Read(now);
            Assert.Single(snaps);
            Assert.Equal("cli-only", snaps[0].SessionId);
        }
        finally { Directory.Delete(projRoot, true); }
    }

    [Fact]
    public void Monitor_archive_une_session_bureau()
    {
        // Les clés desktop:... sont archivables comme les autres (le filtre archived s'applique à l'ensemble fusionné).
        var now = DateTimeOffset.UtcNow;
        var archPath = Path.Combine(TempDir(), "archived.json");
        var bureau = new FakeSessionSource(new SessionSnapshot(
            "desktop:foreground:code", "Claude (bureau)", SessionActivity.Working, null, now,
            SessionKind.Code, SessionOrigin.Desktop));
        var archive = new ArchiveStore(archPath);
        var monitor = new SessionMonitor(TempDir(), new TranscriptSessionSource(TempDir()), archive, bureau);

        Assert.Single(monitor.Read(now));
        archive.Add("desktop:foreground:code");
        Assert.Empty(monitor.Read(now));
    }

    // --- SessionsViewModel : affichage du TYPE (BUR-03) ---

    [WpfFact]
    public void Widget_affiche_le_type_de_session_bureau()
    {
        var now = DateTimeOffset.UtcNow;
        // Une session bureau Kind=Code + une session bureau Kind=Unknown (comme une CLI) → le VM doit
        // exposer KindLabel="Code" pour la première et "" pour la seconde (pas de bruit).
        var bureau = new FakeSessionSource(
            new SessionSnapshot("desktop:foreground:code", "Claude (bureau)", SessionActivity.Working, null, now,
                SessionKind.Code, SessionOrigin.Desktop),
            new SessionSnapshot("cli-x", "MonProjet", SessionActivity.WaitingTurn, null, now,
                SessionKind.Unknown, SessionOrigin.Cli));
        var monitor = new SessionMonitor(TempDir(), new TranscriptSessionSource(TempDir()),
            new ArchiveStore(Path.Combine(TempDir(), "a.json")), bureau);
        var vm = new SessionsViewModel(monitor, new FakeClock(now), new ArchiveStore(Path.Combine(TempDir(), "b.json")));

        vm.Refresh(now);

        var byId = vm.Items.ToDictionary(i => i.SessionId);
        Assert.Equal("Code", byId["desktop:foreground:code"].KindLabel);
        Assert.Equal("", byId["cli-x"].KindLabel);   // Unknown → rien affiché
    }

    [WpfFact]
    public void Widget_mappe_chaque_type_bureau_vers_son_libelle()
    {
        var now = DateTimeOffset.UtcNow;
        var bureau = new FakeSessionSource(
            new SessionSnapshot("desktop:foreground:chat", "Claude (bureau)", SessionActivity.WaitingTurn, null, now, SessionKind.Chat, SessionOrigin.Desktop),
            new SessionSnapshot("desktop:foreground:cowork", "Claude (bureau)", SessionActivity.Unknown, null, now, SessionKind.Cowork, SessionOrigin.Desktop));
        var monitor = new SessionMonitor(TempDir(), new TranscriptSessionSource(TempDir()),
            new ArchiveStore(Path.Combine(TempDir(), "a.json")), bureau);
        var vm = new SessionsViewModel(monitor, new FakeClock(now), new ArchiveStore(Path.Combine(TempDir(), "b.json")));

        vm.Refresh(now);

        var byId = vm.Items.ToDictionary(i => i.SessionId);
        Assert.Equal("Chat", byId["desktop:foreground:chat"].KindLabel);
        Assert.Equal("Cowork", byId["desktop:foreground:cowork"].KindLabel);
    }

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

            var snaps = new SessionMonitor(dir, new TranscriptSessionSource(TempDir()), new ArchiveStore(Path.Combine(TempDir(), "a.json")))
                .Read(now).ToDictionary(s => s.SessionId);

            Assert.Equal(SessionActivity.Working, snaps["fresh"].Activity);
            Assert.Equal(SessionActivity.WaitingAttention, snaps["waiting"].Activity);   // l'attente PERSISTE
            Assert.Equal(SessionActivity.Unknown, snaps["staleWork"].Activity);          // working périmé → Unknown
            Assert.False(snaps.ContainsKey("dead"));                                      // > 8 h → ignoré
        }
        finally { Directory.Delete(dir, true); }
    }
}
