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
/// <c>~/.claude/settings.json</c> : tout <see cref="ClaudeSettingsReconciler"/> construit ici reçoit
/// EXPLICITEMENT son chemin de settings ET son dossier de sauvegarde sous <c>Path.GetTempPath()</c>.
/// Un test dédié, plus bas, en fait une assertion explicite.</para>
/// </summary>
public class ClaudeSettingsReconcilerTests
{
    /// <summary>Exe de test : un nom en <c>Chronos*.exe</c>, seul reconnu par le prédicat d'identité.</summary>
    private const string Exe = @"C:\Apps\Chronos.exe";

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
        Assert.Equal(new[] { "hooks", "statusLine", "agentPushNotifEnabled", "model", "permissions" },
            racine.Select(kv => kv.Key).ToArray());
    }

    /// <summary>PUR-02 sur la machine polluée : la barre périmée est repointée, « padding » survit.</summary>
    [Fact]
    public void Repointe_la_statusLine_perimee_et_conserve_padding()
    {
        var apres = ClaudeSettingsReconciler.ReconcileJson(FixturePollue(), Exe, hooksWanted: true);
        var sl = (Racine(apres!)["statusLine"] as JsonObject)!;

        Assert.Equal(StatusLineInstaller.ChronosCommand(Exe), sl["command"]!.ToString());
        Assert.Equal(2, sl["padding"]!.GetValue<int>());
        Assert.Equal("command", sl["type"]!.ToString());
        Assert.DoesNotContain("Chronos-v2.8.1.exe", apres!);
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
}
