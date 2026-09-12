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
        // Le type de fixture est passé de « permission_prompt » à « agent_needs_input » : depuis EVT-02,
        // permission_prompt est VETOÉ (PermissionRequest, l'événement dédié, en a seul la charge) et ne
        // produit donc plus d'état à observer. L'intention du test — le type voyage dans « reason » — est
        // inchangée.
        var r = SessionHookProcessor.Process("Notification",
            """{"session_id":"s","cwd":"C:\\p","notification_type":"agent_needs_input"}""", 0);
        using var doc = JsonDocument.Parse(r.StateJson!);
        Assert.Equal("agent_needs_input", doc.RootElement.GetProperty("reason").GetString());
    }

    // --- EVT-01 / EVT-02 : PermissionRequest fonde l'attente, le bus cesse d'en fabriquer une ---

    /// <summary>
    /// EVT-01. Le signal DÉDIÉ, exact et immédiat. Le motif écrit dans le fichier d'état est le NOM DE
    /// L'ÉVÉNEMENT : deux lectures du relevé se sont contredites sur le nom du champ de contexte de
    /// permission, donc rien ne doit s'y appuyer. Le nom de l'événement, lui, est un fait observé —
    /// c'est nous qui l'avons câblé.
    /// </summary>
    [Fact]
    public void PermissionRequest_fonde_l_attente_et_dit_le_nom_de_l_evenement()
    {
        var r = SessionHookProcessor.Process("PermissionRequest",
            """{"session_id":"s","cwd":"C:\\dev\\MonProjet"}""", 1000);

        Assert.False(r.Ignore);
        Assert.False(r.Delete);
        using var doc = JsonDocument.Parse(r.StateJson!);
        Assert.Equal("WaitingAttention", doc.RootElement.GetProperty("activity").GetString());
        Assert.Equal("MonProjet", doc.RootElement.GetProperty("project").GetString());
        Assert.Equal("PermissionRequest", doc.RootElement.GetProperty("reason").GetString());
        Assert.Equal(1000, doc.RootElement.GetProperty("updated_at").GetInt64());
    }

    /// <summary>EVT-02. Les TROIS seuls types du bus qui sont de véritables DEMANDES.</summary>
    [Theory]
    [InlineData("agent_needs_input")]
    [InlineData("elicitation_dialog")]
    [InlineData("elicitation_url_dialog")]
    public void Les_trois_vraies_demandes_du_bus_fondent_l_attente(string type)
    {
        var r = SessionHookProcessor.Process("Notification", StdinNotification(type), 0);

        Assert.False(r.Ignore);
        using var doc = JsonDocument.Parse(r.StateJson!);
        Assert.Equal("WaitingAttention", doc.RootElement.GetProperty("activity").GetString());
        Assert.Equal(type, doc.RootElement.GetProperty("reason").GetString());
    }

    /// <summary>
    /// EVT-02, le cas emblématique : « l'utilisateur semble parti » ne dit RIEN de ce que fait la session.
    /// </summary>
    [Fact]
    public void Une_alerte_d_absence_ne_fabrique_plus_aucun_etat()
    {
        var r = SessionHookProcessor.Process("Notification", StdinNotification("idle_prompt"), 0);

        Assert.True(r.Ignore);
        Assert.Null(r.StateJson);
    }

    /// <summary>
    /// Les huit autres types du bus qui ne sont pas des demandes. « permission_prompt » en fait partie :
    /// l'événement dédié en a la charge, et deux chemins pour un même fait rendraient la source illisible.
    /// Une authentification réussie et une reprise de quota fabriquaient jusqu'ici une attente.
    /// </summary>
    [Theory]
    [InlineData("permission_prompt")]
    [InlineData("auth_success")]
    [InlineData("elicitation_complete")]
    [InlineData("elicitation_response")]
    [InlineData("agent_completed")]
    [InlineData("quota_auto_resume_fired")]
    [InlineData("quota_auto_resume_stale")]
    [InlineData("quota_auto_resume_disabled")]
    public void Les_huit_autres_types_du_bus_sans_demande_ne_fabriquent_plus_aucun_etat(string type)
    {
        var r = SessionHookProcessor.Process("Notification", StdinNotification(type), 0);

        Assert.True(r.Ignore);
        Assert.Null(r.StateJson);
    }

    /// <summary>
    /// Le VETO ne produit jamais d'état, il n'en retire que. On ne fait donc JAMAIS dépendre la PRODUCTION
    /// d'un état de la présence d'un champ dont le nom n'est pas confirmable : sans type lisible, ou avec
    /// un type futur inconnu, le tri par matcher a déjà fait son office en amont et l'attente subsiste.
    /// </summary>
    [Fact]
    public void Un_type_de_notification_absent_ou_futur_ne_fait_jamais_disparaitre_l_attente()
    {
        var sansType = SessionHookProcessor.Process("Notification", """{"session_id":"s"}""", 0);
        Assert.False(sansType.Ignore);
        using (var doc = JsonDocument.Parse(sansType.StateJson!))
            Assert.Equal("WaitingAttention", doc.RootElement.GetProperty("activity").GetString());

        var futur = SessionHookProcessor.Process("Notification", StdinNotification("un_type_futur_inconnu"), 0);
        Assert.False(futur.Ignore);
        using (var doc = JsonDocument.Parse(futur.StateJson!))
            Assert.Equal("WaitingAttention", doc.RootElement.GetProperty("activity").GetString());
    }

    /// <summary>Stdin d'un hook Notification portant le type donné (session_id et cwd sont des champs communs).</summary>
    private static string StdinNotification(string type)
        => """{"session_id":"s","cwd":"C:/p","notification_type":"@TYPE@"}""".Replace("@TYPE@", type);

    // --- SessionHookInstaller ---

    private const string Exe = @"C:\Apps\Chronos.exe";

    [Fact]
    public void Install_pose_un_groupe_par_evenement_cable_en_slashes_avant()
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
    // laissent exactement 1 groupe Chronos par événement câblé, et non 3 par événement.
    // C'est très exactement la régression qui a produit 25 groupes sur la vraie machine.
    [Fact]
    public void Trois_chemins_dexe_successifs_ne_laissent_qu_un_groupe_par_evenement()
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

    // --- EVT-01 / EVT-02 : le câblage déclaratif, validé contre la liste blanche AVANT écriture ---

    /// <summary>Le matcher du bus : seuls les TROIS types qui sont de vraies DEMANDES l'atteignent.</summary>
    [Fact]
    public void Le_groupe_Notification_ne_laisse_passer_que_les_trois_vraies_demandes()
    {
        var outJson = SessionHookInstaller.TransformForInstall(null, Exe);
        var groupes = ((JsonNode.Parse(outJson!) as JsonObject)!["hooks"]!["Notification"] as JsonArray)!;

        Assert.Single(groupes);
        Assert.Equal("agent_needs_input|elicitation_dialog|elicitation_url_dialog",
                     groupes[0]!["matcher"]!.GetValue<string>());
    }

    /// <summary>
    /// La moitié « câblée » d'EVT-01 : sans elle, la boucle sur Events serait tautologique. Le groupe ne
    /// porte AUCUN matcher — l'événement est déjà, à lui seul, le fait qu'on veut observer.
    /// </summary>
    [Fact]
    public void Le_cablage_installe_bien_le_groupe_PermissionRequest()
    {
        var outJson = SessionHookInstaller.TransformForInstall(null, Exe);
        var groupes = ((JsonNode.Parse(outJson!) as JsonObject)!["hooks"]!["PermissionRequest"] as JsonArray)!;

        Assert.Single(groupes);
        var groupe = (groupes[0] as JsonObject)!;
        Assert.False(groupe.ContainsKey("matcher"));           // aucun filtre : tout PermissionRequest compte
        Assert.Equal(SessionHookInstaller.HookCommand(Exe, "PermissionRequest"),
                     groupe["hooks"]![0]!["command"]!.GetValue<string>());
    }

    /// <summary>
    /// Le CONTENU exact du câblage, figé. Un comptage seul (Events.Length == 6) ne prouverait rien :
    /// les tests de purge comparent des handlers à Events.Length et restent verts quelle qu'en soit la valeur.
    /// </summary>
    [Fact]
    public void Le_cablage_est_exactement_celui_que_la_phase_annonce()
    {
        Assert.Equal(new[] { "SessionStart", "UserPromptSubmit", "Stop", "SessionEnd", "PermissionRequest", "Notification" },
                     SessionHookInstaller.Events);
    }

    /// <summary>
    /// Le sort d'un nom d'événement INCONNU n'est pas documenté : il produirait un hook mort et muet.
    /// Il n'est donc JAMAIS écrit — prouvé par câblage injecté, sans jamais salir le câblage réel.
    /// </summary>
    [Fact]
    public void Un_evenement_dont_le_nom_est_inconnu_n_est_jamais_ecrit()
    {
        var root = new JsonObject();
        SessionHookInstaller.ApplyHooks(root, Exe, wanted: true,
            new[] { new EvenementCable("Notifcation", null, "faute de frappe") });

        Assert.Null(root["hooks"]);   // rien n'a été écrit, pas même une clé « hooks » vide
    }

    /// <summary>
    /// Un matcher posé sur un événement sans support est SILENCIEUSEMENT ignoré : le groupe ne ferait
    /// pas ce qu'il annonce. Une configuration morte ne s'installe pas.
    /// </summary>
    [Fact]
    public void Un_matcher_sur_un_evenement_qui_n_en_accepte_pas_n_est_jamais_ecrit()
    {
        var root = new JsonObject();
        SessionHookInstaller.ApplyHooks(root, Exe, wanted: true,
            new[] { new EvenementCable("Stop", "quelque_chose", "un matcher que Stop n'accepte pas") });

        Assert.Null(root["hooks"]);
        Assert.Null(root["hooks"]?["Stop"]);
    }

    /// <summary>
    /// La purge est LARGE : un groupe Chronos posé sur un événement qu'on cesse de câbler ne survit plus,
    /// invisible, pour toujours.
    /// </summary>
    [Fact]
    public void Un_groupe_Chronos_sur_un_evenement_qui_n_est_plus_cable_est_purge()
    {
        const string ancien =
            """{"hooks":{"SubagentStop":[{"hooks":[{"type":"command","command":"\"C:/Apps/Chronos.exe\" --hook SubagentStop","timeout":10}]}]}}""";

        var outJson = SessionHookInstaller.TransformForInstall(ancien, Exe);
        var hooks = ((JsonNode.Parse(outJson!) as JsonObject)!["hooks"] as JsonObject)!;

        Assert.False(hooks.ContainsKey("SubagentStop"));
    }

    /// <summary>
    /// La contrepartie de la purge large : les hooks d'un AUTRE outil survivent intégralement, en place.
    /// On n'asserte JAMAIS la longueur du tableau — un plan ultérieur ajoutera un groupe Chronos APRÈS
    /// le groupe tiers, et ce test doit rester vert sans retouche.
    /// </summary>
    [Fact]
    public void La_purge_large_epargne_les_hooks_d_un_autre_outil()
    {
        const string tiers = """
        {"hooks":{
          "PreToolUse":[{"matcher":"Write|Edit","hooks":[{"type":"command","command":"node gsd-prompt-guard.js","timeout":5}]}],
          "PostToolUse":[{"matcher":"Bash|Edit|Write","hooks":[{"type":"command","command":"node gsd-context-monitor.js","timeout":20}]}]
        }}
        """;

        var hooks = ((JsonNode.Parse(SessionHookInstaller.TransformForInstall(tiers, Exe)!) as JsonObject)!["hooks"] as JsonObject)!;

        var pre = ((hooks["PreToolUse"] as JsonArray)![0] as JsonObject)!;
        Assert.Equal("Write|Edit", pre["matcher"]!.GetValue<string>());
        Assert.Equal("node gsd-prompt-guard.js", pre["hooks"]![0]!["command"]!.GetValue<string>());
        Assert.Equal(5, pre["hooks"]![0]!["timeout"]!.GetValue<int>());

        var post = ((hooks["PostToolUse"] as JsonArray)![0] as JsonObject)!;
        Assert.Equal("Bash|Edit|Write", post["matcher"]!.GetValue<string>());
        Assert.Equal("node gsd-context-monitor.js", post["hooks"]![0]!["command"]!.GetValue<string>());
        Assert.Equal(20, post["hooks"]![0]!["timeout"]!.GetValue<int>());
    }

    /// <summary>
    /// Le marqueur SEUL ne suffit pas à nous appartenir : un outil tiers peut adopter « --hook ». Et un
    /// handler sans champ « command » (http/mcp_tool/prompt/agent) n'est jamais à nous non plus. Les deux
    /// survivent à l'installation ET à la désinstallation.
    /// </summary>
    [Fact]
    public void La_purge_large_ne_prend_pas_pour_nous_le_hook_d_un_tiers_qui_porte_le_meme_argument()
    {
        const string tiers = """
        {"hooks":{"PreCompact":[
          {"hooks":[{"type":"command","command":"node outil.js --hook PreCompact"}]},
          {"hooks":[{"type":"http","url":"https://exemple.invalid/pre-compact"}]}
        ]}}
        """;

        foreach (var produit in new[] { SessionHookInstaller.TransformForInstall(tiers, Exe),
                                        SessionHookInstaller.TransformForUninstall(tiers) })
        {
            var arr = ((JsonNode.Parse(produit!) as JsonObject)!["hooks"]!["PreCompact"] as JsonArray)!;
            Assert.Equal(2, arr.Count);
            Assert.Equal("node outil.js --hook PreCompact", arr[0]!["hooks"]![0]!["command"]!.GetValue<string>());
            Assert.Equal("https://exemple.invalid/pre-compact", arr[1]!["hooks"]![0]!["url"]!.GetValue<string>());
        }
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
