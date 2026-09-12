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
        var hooks = (JsonNode.Parse(outJson!) as JsonObject)!["hooks"] as JsonObject;
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

        var arr = ((JsonNode.Parse(twice!) as JsonObject)!["hooks"]!["SessionStart"] as JsonArray)!;
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
        var cleaned = SessionHookInstaller.TransformForUninstall(installed);

        var stop = (JsonNode.Parse(cleaned!) as JsonObject)!["hooks"]!["Stop"] as JsonArray;
        Assert.Single(stop!);
        Assert.Equal("node autre.js", stop![0]!["hooks"]![0]!["command"]!.GetValue<string>());
    }

    // PUR-01 — critère de succès 1 : trois installations depuis trois chemins d'exe DIFFÉRENTS
    // laissent exactement 1 groupe Chronos par événement (5 au total), et non 3 par événement.
    // C'est très exactement la régression qui a produit 25 groupes sur la vraie machine.
    [Fact]
    public void Trois_chemins_dexe_successifs_ne_laissent_que_cinq_hooks()
    {
        var v25 = SessionHookInstaller.TransformForInstall(null, @"C:\DL\Chronos-v2.5.exe");
        var v26 = SessionHookInstaller.TransformForInstall(v25, @"C:\DL\Chronos-v2.6.exe");
        var v281 = SessionHookInstaller.TransformForInstall(v26, @"C:\DL\Chronos-v2.8.1.exe");

        var hooks = (JsonNode.Parse(v281!) as JsonObject)!["hooks"] as JsonObject;
        foreach (var ev in SessionHookInstaller.Events)
        {
            var arr = (hooks![ev] as JsonArray)!;
            Assert.Single(arr);                                   // 1 seul groupe Chronos, jamais 3
            var cmd = arr[0]!["hooks"]![0]!["command"]!.GetValue<string>();
            Assert.Contains("C:/DL/Chronos-v2.8.1.exe", cmd);     // et il pointe le DERNIER exe
        }
    }

    // PUR-01 — un groupe d'un autre outil (GSD) survit intégralement : « matcher », « timeout »,
    // et sa POSITION (les survivants gardent leur ordre, le groupe Chronos est ajouté en fin).
    [Fact]
    public void Install_preserve_un_groupe_tiers_avec_son_matcher_et_son_timeout()
    {
        const string existing =
            """{"hooks":{"SessionStart":[{"matcher":"Write|Edit","hooks":[{"type":"command","command":"node gsd.js","timeout":5}]}]}}""";

        var outJson = SessionHookInstaller.TransformForInstall(existing, Exe);
        var arr = ((JsonNode.Parse(outJson!) as JsonObject)!["hooks"]!["SessionStart"] as JsonArray)!;

        Assert.Equal(2, arr.Count);
        Assert.Equal("Write|Edit", arr[0]!["matcher"]!.GetValue<string>());          // matcher préservé
        Assert.Equal(5, arr[0]!["hooks"]![0]!["timeout"]!.GetValue<int>());          // timeout préservé
        Assert.Equal("node gsd.js", arr[0]!["hooks"]![0]!["command"]!.GetValue<string>());
        Assert.Contains("--hook SessionStart", arr[1]!["hooks"]![0]!["command"]!.GetValue<string>());
    }

    // PUR-01 — le schéma officiel autorise des handlers SANS champ « command » (http/mcp_tool/prompt/
    // agent). L'ancien prédicat faisait GetValue<string>() dessus et aurait levé.
    [Fact]
    public void Install_ne_leve_pas_sur_un_handler_sans_command()
    {
        const string existing = """{"hooks":{"Stop":[{"hooks":[{"type":"http","url":"https://x"}]}]}}""";

        var outJson = SessionHookInstaller.TransformForInstall(existing, Exe);
        var arr = ((JsonNode.Parse(outJson!) as JsonObject)!["hooks"]!["Stop"] as JsonArray)!;

        Assert.Equal(2, arr.Count);
        Assert.Equal("https://x", arr[0]!["hooks"]![0]!["url"]!.GetValue<string>());  // groupe voisin intact
    }

    // PUR-03 — un settings.json INEXPLOITABLE ne produit AUCUNE écriture : les cœurs purs renvoient
    // null. Le repli historique « repartir d'un objet vide » effaçait tout le fichier de l'utilisateur.
    [Theory]
    [InlineData("""{"a":1,"a":2}""")]
    [InlineData("[1,2,3]")]
    [InlineData("{ cassé")]
    [InlineData("\"scalaire\"")]
    public void Install_sur_json_inexploitable_ne_produit_rien(string json)
    {
        Assert.Null(SessionHookInstaller.TransformForInstall(json, Exe));
        Assert.Null(SessionHookInstaller.TransformForUninstall(json));
    }

    // PUR-01 — la purge est volontairement LARGE : elle retire les hooks de TOUTES les versions,
    // pas seulement ceux de l'exe courant, sinon désactiver depuis une nouvelle version laisserait
    // derrière lui les hooks de toutes les anciennes.
    [Fact]
    public void Uninstall_retire_les_hooks_de_toutes_les_versions()
    {
        const string tiers =
            """{"hooks":{"SessionStart":[{"matcher":"Write|Edit","hooks":[{"type":"command","command":"node gsd.js","timeout":5}]}]}}""";

        var v25 = SessionHookInstaller.TransformForInstall(tiers, @"C:\DL\Chronos-v2.5.exe");
        var v26 = SessionHookInstaller.TransformForInstall(v25, @"C:\DL\Chronos-v2.6.exe");
        var v281 = SessionHookInstaller.TransformForInstall(v26, @"C:\DL\Chronos-v2.8.1.exe");

        var cleaned = SessionHookInstaller.TransformForUninstall(v281);

        Assert.DoesNotContain("--hook", cleaned!);                       // plus AUCUNE commande Chronos
        var hooks = (JsonNode.Parse(cleaned!) as JsonObject)!["hooks"] as JsonObject;
        var arr = (hooks!["SessionStart"] as JsonArray)!;
        Assert.Single(arr);
        Assert.Equal("node gsd.js", arr[0]!["hooks"]![0]!["command"]!.GetValue<string>()); // tiers survivant
        Assert.Null(hooks["Stop"]);                                       // clé devenue vide → retirée
    }

    // PUR-03 — désinstaller sur un fichier sans hooks ne DOIT PAS créer la clé « hooks », ni toucher
    // aux autres réglages.
    [Fact]
    public void Uninstall_sur_settings_sans_hooks_ne_cree_pas_la_cle()
    {
        var outJson = SessionHookInstaller.TransformForUninstall("""{"model":"opus"}""");
        var root = (JsonNode.Parse(outJson!) as JsonObject)!;

        Assert.Null(root["hooks"]);
        Assert.Equal("opus", root["model"]!.GetValue<string>());
    }

    // PUR-01 — FRAÎCHEUR : un groupe Chronos d'une AUTRE version ne compte pas comme « installé ici ».
    // Chemin injecté depuis Path.GetTempPath() : ce test ne peut pas atteindre le vrai ~/.claude.
    [Fact]
    public void IsInstalled_est_faux_quand_le_groupe_pointe_une_autre_version()
    {
        var fichier = Path.Combine(TempDir(), "settings.json");
        File.WriteAllText(fichier, SessionHookInstaller.TransformForInstall(null, @"C:\DL\Chronos-v2.8.1.exe")!);

        var installer = new SessionHookInstaller(fichier);   // chemin TEMP, jamais le profil utilisateur
        Assert.False(installer.IsInstalled(Exe));                              // pas CET exe
        Assert.True(installer.IsInstalled(@"C:\DL\Chronos-v2.8.1.exe"));       // mais bien celui-là
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
    public void Monitor_fusionne_transcripts_et_hooks_le_plus_recent_gagne()
    {
        var hookDir = TempDir();
        var projRoot = TempDir();
        var now = DateTimeOffset.UtcNow;
        try
        {
            // Même session_id des deux côtés : le hook est plus RÉCENT d'une minute, c'est à ce titre — et à ce
            // titre seul — qu'il l'emporte.
            WriteTranscript(projRoot, "dup", new[] { CwdLine, AssistantToolUse }, TimeSpan.FromMinutes(1));
            WriteState(hookDir, "dup", SessionActivity.WaitingAttention, now.ToUnixTimeMilliseconds());

            var snaps = new SessionMonitor(hookDir, new TranscriptSessionSource(projRoot)).Read(now);
            Assert.Single(snaps);
            Assert.Equal(SessionActivity.WaitingAttention, snaps[0].Activity); // le plus RÉCENT gagne — ici c'est le hook
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

    [Fact]
    public void Monitor_lit_une_session_du_transcript_sans_hook()
    {
        // Cas NOMINAL : un transcript seul, aucun fichier de hook → la session est lue et rendue telle quelle.
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
