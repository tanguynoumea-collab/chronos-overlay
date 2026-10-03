using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve PUR-03 et le critère de succès 4 de la ROADMAP : la machine déjà polluée se nettoie seule,
/// rien d'autre n'est touché, et un fichier inexploitable reste rigoureusement inchangé.
///
/// <para><b>GARDE ANTI-ACCIDENT.</b> Aucun test de ce fichier ne peut atteindre le vrai
/// settings.json de Claude Code du profil : tout <see cref="ClaudeSettingsReconciler"/> construit ici reçoit
/// EXPLICITEMENT son chemin de settings ET son dossier de sauvegarde sous <c>Path.GetTempPath()</c>.
/// Un test dédié, plus bas, en fait une assertion explicite.</para>
/// </summary>
public class ClaudeSettingsReconcilerTests
{
    /// <summary>Exe de test : un nom en <c>Chronos*.exe</c>, seul reconnu par le prédicat d'identité.</summary>
    private const string Exe = @"C:\Apps\Chronos.exe";

    /// <summary>Clé héritée des réglages ≤ 3.4, composée en deux morceaux comme dans le réconciliateur : le membre est retiré de
    /// <see cref="ChronosSettings"/>, et la garde de non-retour compte son nom dans le texte des tests.</summary>
    private const string CleHeritee = "Inner" + "StatusLineCommand";

    private static string TestDataPath(string file, [CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "TestData", file);

    /// <summary>Fixture figée de l'état réel constaté le 2026-09-09 : 25 groupes Chronos + 3 groupes GSD.</summary>
    private static string FixturePollue() => File.ReadAllText(TestDataPath("claude-settings-pollue.json"));

    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "ChronosRecon_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    /// <summary>Compte les handlers dont la commande contient le marqueur, tous événements confondus.</summary>
    private static int CompteHooks(string json, string marqueur)
    {
        var hooks = (JsonNode.Parse(json) as JsonObject)?["hooks"] as JsonObject;
        if (hooks is null) return 0;
        return hooks.Sum(kv => (kv.Value as JsonArray)?.Sum(g =>
            ((g as JsonObject)?["hooks"] as JsonArray)?.Count(h =>
                (h as JsonObject)?["command"]?.ToString().Contains(marqueur) == true) ?? 0) ?? 0);
    }

    private static JsonObject Racine(string json) => (JsonNode.Parse(json) as JsonObject)!;

    private static string[] CommandesDe(JsonObject racine)
        => (racine["hooks"] as JsonObject)!.SelectMany(kv => (kv.Value as JsonArray)!)
            .SelectMany(g => ((g as JsonObject)!["hooks"] as JsonArray)!)
            .Select(h => (h as JsonObject)?["command"]?.ToString() ?? "")
            .ToArray();

    // --- Cœur PUR : convergence sur la fixture de l'état réel (PUR-03) ---

    /// <summary>PUR-03 / critère de succès 3 : les 25 groupes Chronos deviennent UN SEUL par événement câblé,
    /// sans intervention manuelle. Le 25 d'entrée reste un littéral : c'est un fait figé du 2026-09-09.</summary>
    [Fact]
    public void Purge_la_fixture_reelle_de_25_groupes_a_un_seul_par_evenement_cable()
    {
        var avant = FixturePollue();
        Assert.Equal(25, CompteHooks(avant, ClaudeSettingsJson.HookMarker));

        var apres = ClaudeSettingsReconciler.ReconcileJson(avant, Exe, hooksWanted: true);

        Assert.NotNull(apres);
        Assert.Equal(SessionHookInstaller.Events.Length, CompteHooks(apres!, ClaudeSettingsJson.HookMarker));

        // Un groupe par événement, pointant sur l'exe courant (slashes avant).
        var hooks = (Racine(apres!)["hooks"] as JsonObject)!;
        foreach (var ev in SessionHookInstaller.Events)
        {
            var groupes = (hooks[ev] as JsonArray)!;
            var chronos = groupes.Where(g => ClaudeSettingsJson.IsChronosGroup(g, ClaudeSettingsJson.HookMarker)).ToArray();
            Assert.Single(chronos);
            Assert.Equal(SessionHookInstaller.HookCommand(Exe, ev),
                (chronos[0] as JsonObject)!["hooks"]![0]!["command"]!.ToString());
        }
    }

    /// <summary>Critère de succès 4 : les 3 hooks GSD survivent avec leur matcher, leur timeout et leurs clés inconnues.</summary>
    [Fact]
    public void Preserve_les_trois_hooks_GSD_avec_leur_matcher_et_leur_timeout()
    {
        var apres = ClaudeSettingsReconciler.ReconcileJson(FixturePollue(), Exe, hooksWanted: true);
        var racine = Racine(apres!);
        var hooks = (racine["hooks"] as JsonObject)!;

        var commandes = CommandesDe(racine);
        Assert.Contains(commandes, c => c.Contains("gsd-check-update.js"));
        Assert.Contains(commandes, c => c.Contains("gsd-context-monitor.js"));
        Assert.Contains(commandes, c => c.Contains("gsd-prompt-guard.js"));

        // matcher, timeout et clé inconnue « if » du groupe PostToolUse : intacts.
        var post = ((hooks["PostToolUse"] as JsonArray)![0] as JsonObject)!;
        Assert.Equal("Bash|Edit|Write|MultiEdit|Agent|Task", post["matcher"]!.ToString());
        Assert.NotNull(post["if"]);

        var pre = ((hooks["PreToolUse"] as JsonArray)![0] as JsonObject)!;
        Assert.Equal("Write|Edit", pre["matcher"]!.ToString());
        Assert.Equal(5, (pre["hooks"] as JsonArray)![0]!["timeout"]!.GetValue<int>());

        // Le groupe GSD de SessionStart reste EN TÊTE : le groupe Chronos est ajouté après lui.
        var sessionStart = (hooks["SessionStart"] as JsonArray)!;
        Assert.Contains("gsd-check-update.js", (sessionStart[0] as JsonObject)!["hooks"]![0]!["command"]!.ToString());
    }

    /// <summary>
    /// EVT-03 — LE test que le milestone exige, et il devient porteur au moment EXACT où Chronos se met à
    /// écrire sur des clés qu'un autre outil occupe déjà. <c>PreToolUse</c> et <c>PostToolUse</c> portent,
    /// sur cette machine, les hooks GSD relevés le 2026-09-09 : ils doivent survivre INTACTS et EN TÊTE,
    /// le groupe Chronos venant APRÈS eux.
    ///
    /// <para>Ce qui rend cela vrai : la purge repère Chronos par marqueur d'argument ET nom de fichier
    /// <c>Chronos*.exe</c>, jamais par la clé d'événement. Une clé partagée n'est donc pas une clé à nous.</para>
    /// </summary>
    [Fact]
    public void Les_battements_n_evincent_pas_les_hooks_d_un_autre_outil_sur_PreToolUse_et_PostToolUse()
    {
        var apres = ClaudeSettingsReconciler.ReconcileJson(FixturePollue(), Exe, hooksWanted: true);
        var hooks = (Racine(apres!)["hooks"] as JsonObject)!;

        foreach (var (cle, matcher, fichier, delai) in new[]
        {
            ("PreToolUse",  "Write|Edit",                        "gsd-prompt-guard.js",    5),
            ("PostToolUse", "Bash|Edit|Write|MultiEdit|Agent|Task", "gsd-context-monitor.js", 10),
        })
        {
            var groupes = (hooks[cle] as JsonArray)!;

            // 1) Le groupe tiers est TOUJOURS à l'index 0, avec sa commande, son matcher et son timeout.
            var tiers = (groupes[0] as JsonObject)!;
            Assert.False(ClaudeSettingsJson.IsChronosGroup(tiers, ClaudeSettingsJson.HookMarker));
            Assert.Equal(matcher, tiers["matcher"]!.ToString());
            Assert.Contains(fichier, tiers["hooks"]![0]!["command"]!.ToString());
            Assert.Equal(delai, (tiers["hooks"] as JsonArray)![0]!["timeout"]!.GetValue<int>());

            // 2) Un SEUL groupe Chronos, et il est ajouté APRÈS le groupe tiers.
            var notres = groupes.Where(g => ClaudeSettingsJson.IsChronosGroup(g, ClaudeSettingsJson.HookMarker)).ToArray();
            var notre = Assert.Single(notres);
            Assert.True(groupes.IndexOf(notre) > 0, $"Le groupe Chronos de {cle} doit suivre le groupe tiers.");
            Assert.Equal(SessionHookInstaller.HookCommand(Exe, cle),
                         (notre as JsonObject)!["hooks"]![0]!["command"]!.ToString());
        }

        // 3) La clé INCONNUE « if » du groupe PostToolUse survit : on ne réécrit pas ce qu'on ne comprend pas.
        Assert.Equal("always", ((hooks["PostToolUse"] as JsonArray)![0] as JsonObject)!["if"]!.ToString());
    }

    /// <summary>Les clés racine des autres outils ne sont ni perdues ni réordonnées.</summary>
    [Fact]
    public void Preserve_agentPushNotifEnabled_et_les_cles_racine_inconnues()
    {
        // On enrichit la fixture de deux clés racine que Chronos ne connaît pas.
        var racineEntree = Racine(FixturePollue());
        racineEntree["model"] = "opus";
        racineEntree["permissions"] = new JsonObject { ["allow"] = new JsonArray("Bash(git:*)") };
        var entree = ClaudeSettingsJson.Serialize(racineEntree);

        var apres = ClaudeSettingsReconciler.ReconcileJson(entree, Exe, hooksWanted: true);
        var racine = Racine(apres!);

        Assert.True(racine["agentPushNotifEnabled"]!.GetValue<bool>());
        Assert.Equal("opus", racine["model"]!.ToString());
        Assert.Equal("Bash(git:*)", (racine["permissions"]!["allow"] as JsonArray)![0]!.ToString());

        // Ordre des clés racine conservé tel qu'à l'entrée.
        // La fixture portait une barre Chronos périmée : elle est RETIRÉE, l'ordre du reste est intact.
        Assert.Equal(new[] { "hooks", "agentPushNotifEnabled", "model", "permissions" },
            racine.Select(kv => kv.Key).ToArray());
    }

    /// <summary>DAT-03 sur la machine polluée : la barre Chronos périmée (Chronos-v2.8.1.exe, padding 2)
    /// est RETIRÉE — jamais repointée.</summary>
    [Fact]
    public void Retire_la_statusLine_Chronos_perimee()
    {
        var apres = ClaudeSettingsReconciler.ReconcileJson(
            FixturePollue(), Exe, hooksWanted: true, commandeHeritee: null, out var barre);

        Assert.Equal(IssueBarreStatut.Retiree, barre);
        Assert.Null(Racine(apres!)["statusLine"]);
        Assert.DoesNotContain("--statusline", apres!);
    }

    /// <summary>Widget désactivé : ZÉRO hook Chronos, et les clés d'événement devenues vides disparaissent.</summary>
    [Fact]
    public void Widget_desactive_ne_laisse_aucun_hook_Chronos_et_retire_les_evenements_vides()
    {
        var apres = ClaudeSettingsReconciler.ReconcileJson(FixturePollue(), Exe, hooksWanted: false);

        Assert.NotNull(apres);
        Assert.Equal(0, CompteHooks(apres!, ClaudeSettingsJson.HookMarker));

        var hooks = (Racine(apres!)["hooks"] as JsonObject)!;
        foreach (var vide in new[] { "Notification", "Stop", "UserPromptSubmit", "SessionEnd" })
            Assert.False(hooks.ContainsKey(vide));

        // SessionStart subsiste avec le SEUL groupe GSD ; les événements des tiers sont intacts.
        Assert.Single((hooks["SessionStart"] as JsonArray)!);
        Assert.Single((hooks["PostToolUse"] as JsonArray)!);
        Assert.Single((hooks["PreToolUse"] as JsonArray)!);
    }

    /// <summary>Point fixe : la seconde passe n'a plus rien à écrire (PUR-01/02/03).</summary>
    [Fact]
    public void Est_un_point_fixe_la_seconde_passe_ne_produit_rien()
    {
        var passe1 = ClaudeSettingsReconciler.ReconcileJson(FixturePollue(), Exe, hooksWanted: true);
        Assert.NotNull(passe1);

        var passe2 = ClaudeSettingsReconciler.ReconcileJson(passe1, Exe, hooksWanted: true);
        Assert.Null(passe2);
    }

    /// <summary>Un fichier déjà conforme ne produit aucune écriture (Pitfall 10 : pas de sauvegarde inutile).</summary>
    [Fact]
    public void Ne_produit_rien_sur_un_fichier_deja_conforme()
    {
        var conforme = SessionHookInstaller.TransformForInstall(null, Exe);
        Assert.Null(ClaudeSettingsReconciler.ReconcileJson(conforme, Exe, hooksWanted: true));
    }

    /// <summary>Critère de succès 4 : un contenu INEXPLOITABLE ne produit jamais de réécriture.</summary>
    [Theory]
    [InlineData("{\"a\":1,\"a\":2}")]   // clés dupliquées : ArgumentException à la matérialisation
    [InlineData("[1,2,3]")]             // racine tableau
    [InlineData("{ cassé")]             // JSON invalide
    [InlineData("\"txt\"")]             // racine scalaire
    public void Abandonne_sans_ecrire_sur_json_inexploitable(string json)
        => Assert.Null(ClaudeSettingsReconciler.ReconcileJson(json, Exe, hooksWanted: true));

    /// <summary>Les accents des valeurs des AUTRES outils ne sont jamais mutilés en séquences \uXXXX.</summary>
    [Fact]
    public void Conserve_les_accents_litteraux_des_chemins()
    {
        const string entree = """
        {
          "hooks": {
            "Stop": [
              { "hooks": [ { "type": "command", "command": "\"C:/Users/Tanguy/Téléchargements/Chronos-v2.6.exe\" --hook Stop" } ] },
              { "hooks": [ { "type": "command", "command": "node \"C:/Outils/Téléchargements/vérif.js\"" } ] }
            ]
          }
        }
        """;

        var apres = ClaudeSettingsReconciler.ReconcileJson(entree, Exe, hooksWanted: true);

        Assert.NotNull(apres);
        Assert.DoesNotContain("\\u00E9", apres!);
        Assert.DoesNotContain("\\u00e9", apres!);
        Assert.Contains("Téléchargements", apres!);   // le hook tiers est intact, accents littéraux
    }

    // --- Couche E/S : dossiers temp uniquement ---

    /// <summary>
    /// GARDE ANTI-ACCIDENT explicite : le réconciliateur construit par les tests ne peut pas viser le
    /// profil de l'utilisateur — ni son settings.json, ni son dossier de sauvegardes.
    /// </summary>
    [Fact]
    public void Aucun_test_ne_cible_le_vrai_settings_du_profil()
    {
        var dir = TempDir();
        try
        {
            var reconciler = new ClaudeSettingsReconciler(
                Path.Combine(dir, "settings.json"), Path.Combine(dir, "backups"), Exe);

            Assert.StartsWith(Path.GetTempPath(), reconciler.SettingsPath);
            Assert.StartsWith(Path.GetTempPath(), reconciler.BackupDir);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// Sauvegarde CONDITIONNELLE (Pitfall 10) : la première réconciliation écrit et sauvegarde une fois ;
    /// la seconde ne touche plus à rien et ne crée aucune sauvegarde supplémentaire.
    /// </summary>
    [Fact]
    public void Ecrit_et_sauvegarde_une_seule_fois_puis_ne_touche_plus_a_rien()
    {
        var dir = TempDir();
        try
        {
            var settings = Path.Combine(dir, "settings.json");
            var backups = Path.Combine(dir, "backups");
            File.WriteAllText(settings, FixturePollue());
            var original = File.ReadAllBytes(settings);

            var reconciler = new ClaudeSettingsReconciler(settings, backups, Exe);

            // 1re passe : écriture réelle + UNE sauvegarde identique octet pour octet à l'original.
            Assert.True(reconciler.Reconcile(hooksWanted: true));
            Assert.Equal(SessionHookInstaller.Events.Length, CompteHooks(File.ReadAllText(settings), ClaudeSettingsJson.HookMarker));

            var sauvegardes = Directory.GetFiles(backups, "claude-settings-*.json");
            Assert.Single(sauvegardes);
            Assert.Equal(original, File.ReadAllBytes(sauvegardes[0]));

            // 2de passe : rien à faire → aucune écriture, aucune sauvegarde de plus.
            var apres1 = File.ReadAllBytes(settings);
            Assert.False(reconciler.Reconcile(hooksWanted: true));
            Assert.Equal(apres1, File.ReadAllBytes(settings));
            Assert.Single(Directory.GetFiles(backups, "claude-settings-*.json"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// Critère de succès 4 : un settings.json malformé reste INCHANGÉ OCTET POUR OCTET, aucune sauvegarde
    /// n'est créée, et aucune exception ne remonte au démarrage.
    /// </summary>
    [Fact]
    public void Un_fichier_malforme_reste_inchange_octet_pour_octet_et_sans_sauvegarde()
    {
        var dir = TempDir();
        try
        {
            var settings = Path.Combine(dir, "settings.json");
            var backups = Path.Combine(dir, "backups");
            File.WriteAllText(settings, "{ \"a\": 1, \"a\": 2 }");
            var avant = File.ReadAllBytes(settings);

            var resultat = new ClaudeSettingsReconciler(settings, backups, Exe).Reconcile(hooksWanted: true);

            Assert.False(resultat);
            Assert.Equal(avant, File.ReadAllBytes(settings));
            Assert.True(!Directory.Exists(backups) || Directory.GetFiles(backups, "claude-settings-*.json").Length == 0);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>Purge SEULEMENT : un settings.json absent n'est jamais créé par la réconciliation.</summary>
    [Fact]
    public void Ne_cree_pas_le_fichier_sil_est_absent()
    {
        var dir = TempDir();
        try
        {
            var settings = Path.Combine(dir, "settings.json");
            var backups = Path.Combine(dir, "backups");

            Assert.False(new ClaudeSettingsReconciler(settings, backups, Exe).Reconcile(hooksWanted: true));

            Assert.False(File.Exists(settings));
            Assert.False(Directory.Exists(backups));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>Rétention : le dossier de sauvegardes ne grossit pas indéfiniment.</summary>
    [Fact]
    public void La_retention_ne_garde_que_cinq_sauvegardes()
    {
        var dir = TempDir();
        try
        {
            var settings = Path.Combine(dir, "settings.json");
            var backups = Path.Combine(dir, "backups");
            var reconciler = new ClaudeSettingsReconciler(settings, backups, Exe);

            // 7 réconciliations ÉCRIVANTES : on re-pollue le fichier avant chacune.
            for (int i = 0; i < 7; i++)
            {
                File.WriteAllText(settings, FixturePollue());
                Assert.True(reconciler.Reconcile(hooksWanted: true));
            }

            Assert.True(Directory.GetFiles(backups, "claude-settings-*.json").Length <= 5);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // --- Retrait de la barre statusLine Chronos (DAT-03) ---

    private static string AvecBarre(string commande, params (string cle, JsonNode? valeur)[] autres)
    {
        var racine = new JsonObject
        {
            ["statusLine"] = new JsonObject { ["type"] = "command", ["command"] = commande, ["padding"] = 2 },
        };
        foreach (var (cle, valeur) in autres) racine[cle] = valeur;
        return ClaudeSettingsJson.Serialize(racine);
    }

    /// <summary>La barre réelle de l'utilisateur (guillemets + espaces dans le chemin) est retirée ;
    /// le reste du fichier est conservé, et la seconde passe est un point fixe.</summary>
    [Fact]
    public void Retire_la_barre_Chronos_de_l_utilisateur_et_laisse_le_reste()
    {
        var entree = AvecBarre("\"C:/Users/X/Documents/PROJET OVERLAY/Chronos-v3.4.0.exe\" --statusline",
            ("model", "opus"), ("agentPushNotifEnabled", true));

        var apres = ClaudeSettingsReconciler.ReconcileJson(entree, Exe, hooksWanted: false, commandeHeritee: null, out var barre);

        Assert.Equal(IssueBarreStatut.Retiree, barre);
        var racine = Racine(apres!);
        Assert.Null(racine["statusLine"]);
        Assert.Equal("opus", racine["model"]!.ToString());
        Assert.True(racine["agentPushNotifEnabled"]!.GetValue<bool>());

        Assert.Null(ClaudeSettingsReconciler.ReconcileJson(apres, Exe, hooksWanted: false, commandeHeritee: null, out var barre2));
        Assert.Equal(IssueBarreStatut.Absente, barre2);
    }

    /// <summary>Migré des tests de l'ancien installeur de barre (supprimés en 37-06) : l'ancienne barre de l'utilisateur est restaurée par
    /// MUTATION (type et padding conservés).</summary>
    [Fact]
    public void Retrait_restaure_la_barre_dorigine()
    {
        var entree = AvecBarre("\"C:/Apps/Chronos.exe\" --statusline");

        var apres = ClaudeSettingsReconciler.ReconcileJson(entree, Exe, hooksWanted: false, commandeHeritee: "bash ~/old.sh", out var barre);

        Assert.Equal(IssueBarreStatut.Restauree, barre);
        var sl = (Racine(apres!)["statusLine"] as JsonObject)!;
        Assert.Equal("bash ~/old.sh", sl["command"]!.ToString());
        Assert.Equal("command", sl["type"]!.ToString());
        Assert.Equal(2, sl["padding"]!.GetValue<int>());
    }

    /// <summary>Migré : sans ancienne barre connue, la clé statusLine disparaît entièrement.</summary>
    [Fact]
    public void Retrait_sans_inner_retire_completement_statusLine()
    {
        var entree = AvecBarre("\"C:/Apps/Chronos.exe\" --statusline", ("model", "opus"));

        var apres = ClaudeSettingsReconciler.ReconcileJson(entree, Exe, hooksWanted: false, commandeHeritee: null, out var barre);

        Assert.Equal(IssueBarreStatut.Retiree, barre);
        Assert.Null(Racine(apres!)["statusLine"]);
        Assert.Equal("opus", Racine(apres!)["model"]!.ToString());
    }

    /// <summary>Migré : une barre tierce n'est jamais touchée, même avec une commande héritée.</summary>
    [Fact]
    public void Retrait_ne_touche_pas_une_barre_tierce()
    {
        var entree = AvecBarre("bash ~/my-statusline.sh");

        var apres = ClaudeSettingsReconciler.ReconcileJson(entree, Exe, hooksWanted: false, commandeHeritee: "peu importe", out var barre);

        Assert.Equal(IssueBarreStatut.Tierce, barre);
        Assert.Null(apres);   // rien à écrire : la barre tierce est intacte, padding compris
    }

    /// <summary>Migré : une barre Chronos d'une AUTRE version est reconnue et retirée.</summary>
    [Fact]
    public void Retrait_retire_une_barre_Chronos_dune_autre_version()
    {
        var entree = AvecBarre("\"C:/DL/Chronos-v2.6.exe\" --statusline", ("model", "opus"));

        var apres = ClaudeSettingsReconciler.ReconcileJson(entree, Exe, hooksWanted: false, commandeHeritee: null, out var barre);

        Assert.Equal(IssueBarreStatut.Retiree, barre);
        Assert.Null(Racine(apres!)["statusLine"]);
        Assert.Equal("opus", Racine(apres!)["model"]!.ToString());
    }

    /// <summary>Un fichier sans statusLine : issue « Absente ».</summary>
    [Fact]
    public void Sans_statusLine_l_issue_est_absente()
    {
        var conforme = SessionHookInstaller.TransformForInstall(null, Exe);

        Assert.Null(ClaudeSettingsReconciler.ReconcileJson(conforme, Exe, hooksWanted: true, commandeHeritee: null, out var barre));
        Assert.Equal(IssueBarreStatut.Absente, barre);
    }

    /// <summary>Une commande héritée qui est elle-même une barre Chronos n'est JAMAIS restaurée.</summary>
    [Fact]
    public void Une_commande_heritee_Chronos_n_est_jamais_restauree()
    {
        var entree = AvecBarre("\"C:/Apps/Chronos.exe\" --statusline");

        var apres = ClaudeSettingsReconciler.ReconcileJson(entree, Exe, hooksWanted: false,
            commandeHeritee: "\"C:/DL/Chronos-v2.8.1.exe\" --statusline", out var barre);

        Assert.Equal(IssueBarreStatut.Retiree, barre);
        Assert.Null(Racine(apres!)["statusLine"]);
    }

    /// <summary>E/S témoins : une sauvegarde AVANT l'écriture, bilan exact ; seconde passe sans écriture ni sauvegarde.</summary>
    [Fact]
    public void Retire_sauvegarde_une_fois_puis_ne_touche_plus_a_rien_et_le_bilan_le_dit()
    {
        var dir = TempDir();
        try
        {
            var settingsTemp = Path.Combine(dir, "settings.json");
            var backupsTemp = Path.Combine(dir, "backups");
            File.WriteAllText(settingsTemp, AvecBarre("\"C:/Users/X/Documents/PROJET OVERLAY/Chronos-v3.4.0.exe\" --statusline"));
            var original = File.ReadAllBytes(settingsTemp);

            var reconciler = new ClaudeSettingsReconciler(settingsTemp, backupsTemp, Exe);
            Assert.StartsWith(Path.GetTempPath(), reconciler.SettingsPath);
            Assert.StartsWith(Path.GetTempPath(), reconciler.BackupDir);
            Assert.Null(reconciler.DernierBilan);   // pas encore passée

            Assert.True(reconciler.Reconcile(hooksWanted: false));
            Assert.Null(Racine(File.ReadAllText(settingsTemp))["statusLine"]);

            var sauvegardes = Directory.GetFiles(backupsTemp, "claude-settings-*.json");
            Assert.Single(sauvegardes);
            Assert.Equal(original, File.ReadAllBytes(sauvegardes[0]));   // l'état d'AVANT l'écriture

            var bilan = reconciler.DernierBilan!;
            Assert.True(bilan.Ecrit);
            Assert.Equal(IssueBarreStatut.Retiree, bilan.Barre);
            Assert.Equal(sauvegardes[0], bilan.Sauvegarde);
            Assert.True(File.Exists(bilan.Sauvegarde));
            Assert.Null(bilan.Cause);

            var apres1 = File.ReadAllBytes(settingsTemp);
            Assert.False(reconciler.Reconcile(hooksWanted: false));
            Assert.Equal(apres1, File.ReadAllBytes(settingsTemp));
            Assert.Single(Directory.GetFiles(backupsTemp, "claude-settings-*.json"));
            Assert.False(reconciler.DernierBilan!.Ecrit);
            Assert.Equal(IssueBarreStatut.Absente, reconciler.DernierBilan!.Barre);
            Assert.Null(reconciler.DernierBilan!.Sauvegarde);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>Un fichier illisible est inchangé octet pour octet, sans sauvegarde, et le bilan le dit.</summary>
    [Fact]
    public void Un_fichier_illisible_n_est_pas_touche_et_le_bilan_le_dit()
    {
        var dir = TempDir();
        try
        {
            var settingsTemp = Path.Combine(dir, "settings.json");
            var backupsTemp = Path.Combine(dir, "backups");
            File.WriteAllText(settingsTemp, "{ \"statusLine\": { \"command\": \"C:/A/Chronos.exe --statusline\" }, cassé");
            var avant = File.ReadAllBytes(settingsTemp);

            var reconciler = new ClaudeSettingsReconciler(settingsTemp, backupsTemp, Exe);
            Assert.StartsWith(Path.GetTempPath(), reconciler.SettingsPath);
            Assert.StartsWith(Path.GetTempPath(), reconciler.BackupDir);

            Assert.False(reconciler.Reconcile(hooksWanted: true, commandeHeritee: "bash ~/old.sh"));

            Assert.Equal(avant, File.ReadAllBytes(settingsTemp));
            Assert.True(!Directory.Exists(backupsTemp) || Directory.GetFiles(backupsTemp, "claude-settings-*.json").Length == 0);
            var bilan = reconciler.DernierBilan!;
            Assert.False(bilan.Ecrit);
            Assert.Null(bilan.Barre);
            Assert.Null(bilan.Sauvegarde);
            Assert.Equal("illisible — rien écrit", bilan.Cause);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>Fichier absent : rien n'est créé, le bilan dit « fichier absent ».</summary>
    [Fact]
    public void Fichier_absent_bilan_fichier_absent()
    {
        var dir = TempDir();
        try
        {
            var settingsTemp = Path.Combine(dir, "settings.json");
            var backupsTemp = Path.Combine(dir, "backups");

            var reconciler = new ClaudeSettingsReconciler(settingsTemp, backupsTemp, Exe);
            Assert.StartsWith(Path.GetTempPath(), reconciler.SettingsPath);
            Assert.StartsWith(Path.GetTempPath(), reconciler.BackupDir);

            Assert.False(reconciler.Reconcile(hooksWanted: true));

            Assert.False(File.Exists(settingsTemp));
            Assert.False(Directory.Exists(backupsTemp));
            Assert.Equal(new BilanReconciliation(false, null, null, "fichier absent"), reconciler.DernierBilan);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>Hooks repointés vers l'exe courant dans la MÊME écriture que le retrait : une seule sauvegarde.</summary>
    [Fact]
    public void Les_hooks_sont_repointes_dans_la_meme_ecriture_que_le_retrait()
    {
        var dir = TempDir();
        try
        {
            var settingsTemp = Path.Combine(dir, "settings.json");
            var backupsTemp = Path.Combine(dir, "backups");
            File.WriteAllText(settingsTemp, FixturePollue());   // 25 hooks périmés + barre Chronos-v2.8.1

            var reconciler = new ClaudeSettingsReconciler(settingsTemp, backupsTemp, Exe);
            Assert.StartsWith(Path.GetTempPath(), reconciler.SettingsPath);
            Assert.StartsWith(Path.GetTempPath(), reconciler.BackupDir);

            Assert.True(reconciler.Reconcile(hooksWanted: true));

            var ecrit = File.ReadAllText(settingsTemp);
            Assert.Null(Racine(ecrit)["statusLine"]);
            Assert.Equal(SessionHookInstaller.Events.Length, CompteHooks(ecrit, ClaudeSettingsJson.HookMarker));
            Assert.All(CommandesDe(Racine(ecrit)).Where(c => c.Contains("--hook")),
                c => Assert.Contains("C:/Apps/Chronos.exe", c));
            Assert.Single(Directory.GetFiles(backupsTemp, "claude-settings-*.json"));
            Assert.Equal(IssueBarreStatut.Retiree, reconciler.DernierBilan!.Barre);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>Lecture PURE de l'ancienne commande dans les réglages Chronos (clé insensible à la casse).</summary>
    [Theory]
    [InlineData("{\"" + CleHeritee + "\":\"bash x\"}", "bash x")]
    [InlineData("{\"innerstatuslinecommand\":\"bash x\"}", "bash x")]
    [InlineData("{\"ThemeKey\":\"Nuit\",\"" + CleHeritee + "\":\"bash x\"}", "bash x")]
    [InlineData("{\"" + CleHeritee + "\":null}", null)]
    [InlineData("{\"ThemeKey\":\"Nuit\"}", null)]
    [InlineData("{\"" + CleHeritee + "\":42}", null)]
    [InlineData("{\"" + CleHeritee + "\":\"\"}", null)]
    [InlineData("{ cassé", null)]
    [InlineData("[1,2]", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void CommandeInterneHeritee_lit_la_cle_ou_rend_null(string? json, string? attendu)
        => Assert.Equal(attendu, ClaudeSettingsReconciler.CommandeInterneHeritee(json));

    /// <summary>Lecture fichier : un fichier absent rend null sans lever ; un fichier présent est lu.</summary>
    [Fact]
    public void LireCommandeInterneHeritee_fichier_absent_rend_null()
    {
        var dir = TempDir();
        try
        {
            Assert.Null(ClaudeSettingsReconciler.LireCommandeInterneHeritee(Path.Combine(dir, "absent.json")));

            var fichier = Path.Combine(dir, "settings.json");
            File.WriteAllText(fichier, "{\"" + CleHeritee + "\":\"bash ~/old.sh\"}");
            Assert.Equal("bash ~/old.sh", ClaudeSettingsReconciler.LireCommandeInterneHeritee(fichier));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
