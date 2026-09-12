using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Les DEUX PRUDENCES de l'écriture des hooks cessent d'être des affirmations de lecture : elles
/// deviennent FALSIFIABLES.
///
/// <para>Elles sont écrites depuis le début dans la doc de <see cref="SessionHookInstaller.ApplyHooks"/> —
/// « une clé dont la valeur n'est PAS un tableau est ignorée sans être écrite », « une clé dont le tableau
/// était DÉJÀ vide à l'entrée n'est jamais retirée » — et aucune fixture ne les exerçait. C'était le seul
/// endroit du contrat d'événements où du code touchant la configuration VIVANTE de l'utilisateur n'avait
/// pas de garde : deux phrases correctes, invérifiables, et dont l'une était déjà fausse sur le chemin de
/// l'INSTALLATION (une des huit clés câblées portant autre chose qu'un tableau était écrasée).</para>
///
/// <para><b>GARDE ANTI-ACCIDENT.</b> Aucun test de ce fichier n'atteint le vrai
/// <c>~/.claude/settings.json</c> : les deux cœurs éprouvés ici sont PURS (ils prennent et rendent des
/// chaînes), et le seul aller-retour sur disque écrit dans une copie créée sous
/// <c>Path.GetTempPath()</c>, chemin INJECTÉ à l'installateur.</para>
/// </summary>
public class PrudencesEcritureHooksTests
{
    /// <summary>Exe de test : un nom en <c>Chronos*.exe</c>, seul reconnu par le prédicat d'identité.</summary>
    private const string Exe = @"C:\Apps\Chronos.exe";

    private const string ChaineSousStop = "une chaîne, pas un tableau";

    /// <summary>
    /// Un settings.json d'un tiers MAL FORMÉ, ou d'un format que nous ne connaissons pas encore :
    /// <c>Stop</c> (l'une des huit clés CÂBLÉES) porte une chaîne, <c>PostToolBatch</c> (une clé du
    /// catalogue que Chronos ne câble PAS) porte un objet, et <c>WorktreeCreate</c> porte un tableau
    /// DÉJÀ VIDE à l'entrée. <c>PreToolUse</c> héberge un groupe tiers parfaitement formé — sans lui, un
    /// test qui ne verrait rien bouger ne prouverait rien.
    /// </summary>
    private const string FixtureMalFormee = """
    {
      "hooks": {
        "Stop": "une chaîne, pas un tableau",
        "PreToolUse": [
          { "matcher": "Write|Edit", "hooks": [ { "type": "command", "command": "node \"C:/Outils/garde.js\"", "timeout": 20 } ] }
        ],
        "PostToolBatch": { "note": "un objet : format futur d'un autre outil" },
        "WorktreeCreate": []
      }
    }
    """;

    /// <summary>La même, plus UN groupe Chronos réel sous <c>Notification</c> : la purge a donc quelque
    /// chose à retirer, et un test qui ne verrait rien disparaître serait muet.</summary>
    private const string FixtureMalFormeeAvecChronos = """
    {
      "hooks": {
        "Stop": "une chaîne, pas un tableau",
        "Notification": [
          { "hooks": [ { "type": "command", "command": "\"C:/Downloads/Chronos-v3.0.2.exe\" --hook Notification", "timeout": 10 } ] }
        ],
        "PostToolBatch": { "note": "un objet : format futur d'un autre outil" },
        "WorktreeCreate": []
      }
    }
    """;

    private static JsonObject Hooks(string json)
        => ((JsonNode.Parse(json) as JsonObject)!["hooks"] as JsonObject)!;

    /// <summary>Nombre de handlers Chronos, tous événements confondus.</summary>
    private static int CompteHooksChronos(string json)
        => Hooks(json).Sum(kv => (kv.Value as JsonArray)?.Sum(g =>
               ((g as JsonObject)?["hooks"] as JsonArray)?.Count(h =>
                   ClaudeSettingsJson.IsChronosCommand(ClaudeSettingsJson.CommandOf(h),
                                                       ClaudeSettingsJson.HookMarker)) ?? 0) ?? 0);

    // ==================== Prudence n° 1 : une valeur non-tableau est INTOUCHÉE ====================

    /// <summary>
    /// LE CAS DE LA RÉSERVE R1, et le plus sensible : <c>Stop</c> est l'un des HUIT noms câblés. Sur le
    /// chemin de l'INSTALLATION — celui qui écrit dans la configuration vivante — la valeur d'un tiers
    /// doit survivre telle quelle. Chronos n'y ajoute pas son groupe et ne la réécrit pas : nous ne savons
    /// pas ce que cette valeur signifie, donc nous ne pouvons pas la remplacer sans détruire.
    /// </summary>
    [Fact]
    public void Une_cle_CABLEE_dont_la_valeur_n_est_pas_un_tableau_survit_intacte_a_l_installation()
    {
        Assert.Contains("Stop", SessionHookInstaller.Events);   // sinon le test ne parle plus du cas R1

        var apres = SessionHookInstaller.TransformForInstall(FixtureMalFormee, Exe);
        Assert.NotNull(apres);

        var hooks = Hooks(apres!);
        Assert.IsNotType<JsonArray>(hooks["Stop"]);
        Assert.Equal(ChaineSousStop, hooks["Stop"]!.GetValue<string>());

        // La clé du catalogue que nous NE câblons pas, mal formée elle aussi : intouchée.
        Assert.Equal("un objet : format futur d'un autre outil",
                     (hooks["PostToolBatch"] as JsonObject)!["note"]!.GetValue<string>());

        // Anti-mutisme : les SEPT autres entrées câblées, elles, sont bien posées — et aucune sur Stop.
        Assert.Equal(SessionHookInstaller.Events.Length - 1, CompteHooksChronos(apres!));
    }

    /// <summary>
    /// Le même refus sur le chemin de la PURGE. Cette prudence-ci était déjà correcte : ce test est une
    /// GARDE DE NON-RETOUR, pas la preuve d'une correction.
    /// </summary>
    [Fact]
    public void Une_cle_CABLEE_dont_la_valeur_n_est_pas_un_tableau_survit_intacte_a_la_purge()
    {
        var apres = SessionHookInstaller.TransformForUninstall(FixtureMalFormeeAvecChronos);
        Assert.NotNull(apres);

        var hooks = Hooks(apres!);
        Assert.IsNotType<JsonArray>(hooks["Stop"]);
        Assert.Equal(ChaineSousStop, hooks["Stop"]!.GetValue<string>());
        Assert.Equal("un objet : format futur d'un autre outil",
                     (hooks["PostToolBatch"] as JsonObject)!["note"]!.GetValue<string>());

        // Anti-mutisme : la purge a RÉELLEMENT travaillé — le groupe Chronos de Notification a disparu,
        // et sa clé, vidée par NOUS, avec lui.
        Assert.Equal(0, CompteHooksChronos(apres!));
        Assert.False(hooks.ContainsKey("Notification"));
    }

    // ==================== Prudence n° 2 : une clé DÉJÀ vide n'est jamais retirée ====================

    /// <summary>
    /// Une clé dont le tableau était vide AVANT notre passage n'est pas à nous : nous ne l'avons pas
    /// vidée, nous n'avons donc pas à la retirer. Le contraste avec <c>Notification</c> — vidée par la
    /// purge, donc retirée — est tout le propos. Prudence déjà correcte : GARDE DE NON-RETOUR.
    /// </summary>
    [Fact]
    public void Une_cle_dont_le_tableau_etait_DEJA_vide_a_l_entree_n_est_jamais_retiree_par_la_purge()
    {
        var apres = SessionHookInstaller.TransformForUninstall(FixtureMalFormeeAvecChronos);
        Assert.NotNull(apres);

        var hooks = Hooks(apres!);
        Assert.True(hooks.ContainsKey("WorktreeCreate"), "une clé que nous n'avons pas vidée n'est pas à nous");
        Assert.Empty((hooks["WorktreeCreate"] as JsonArray)!);

        Assert.False(hooks.ContainsKey("Notification"));   // celle-là, c'est NOUS qui l'avons vidée
    }

    // ==================== Un refus d'écriture DIT sa cause ====================

    /// <summary>
    /// Un refus muet et une écriture réussie se ressemblent trop : l'un comme l'autre ne lèvent rien.
    /// Les trois motifs remontent donc à l'appelant, exactement comme <c>null</c> lui remonte
    /// « NE RIEN ÉCRIRE ». Le câblage est INJECTÉ : prouver le refus d'un nom hors catalogue n'exige
    /// pas d'écrire un nom douteux dans le câblage réel.
    /// </summary>
    [Fact]
    public void Chaque_refus_d_ecriture_DIT_sa_cause_au_lieu_de_se_taire()
    {
        var racine = (JsonNode.Parse(FixtureMalFormee) as JsonObject)!;

        var refus = SessionHookInstaller.ApplyHooks(racine, Exe, wanted: true, new[]
        {
            new EvenementCable("Stop",         null,     "valeur non-tableau : la clé de la fixture"),
            new EvenementCable("Notifcation",  null,     "faute de frappe : hors catalogue"),
            new EvenementCable("TaskCreated",  "Write",  "événement du catalogue qui n'accepte pas de matcher"),
            new EvenementCable("SessionStart", null,     "la seule entrée que rien n'empêche d'écrire"),
        });

        Assert.Equal(
            new[]
            {
                new RefusEcritureHook("Stop",        MotifRefusEcritureHook.ValeurNonTableau),
                new RefusEcritureHook("Notifcation", MotifRefusEcritureHook.NomHorsCatalogue),
                new RefusEcritureHook("TaskCreated", MotifRefusEcritureHook.MatcherNonSupporte),
            },
            refus);

        // Une seule entrée était écrivable, et c'est bien elle qui a été posée.
        Assert.Equal(1, CompteHooksChronos(ClaudeSettingsJson.Serialize(racine)));
        Assert.Single((Hooks(ClaudeSettingsJson.Serialize(racine))["SessionStart"] as JsonArray)!);
    }

    /// <summary>Le cas NOMINAL ne refuse rien : sans ce contraste, une liste toujours pleine passerait
    /// pour une liste de refus légitimes.</summary>
    [Fact]
    public void Un_settings_sain_ne_produit_aucun_refus()
    {
        var racine = new JsonObject();
        Assert.Empty(SessionHookInstaller.ApplyHooks(racine, Exe, wanted: true));
        Assert.Equal(SessionHookInstaller.Events.Length, CompteHooksChronos(ClaudeSettingsJson.Serialize(racine)));
    }

    // ==================== Le même refus, sur un aller-retour RÉEL sur disque ====================

    /// <summary>
    /// Les deux cœurs ci-dessus sont purs ; c'est la couche d'E/S qui touche le fichier de l'utilisateur.
    /// Sur une COPIE sous <c>Path.GetTempPath()</c> — chemin injecté, le profil réel est hors d'atteinte —
    /// l'installation puis la purge laissent la clé mal formée rigoureusement telle quelle.
    /// </summary>
    [Fact]
    public void Sur_une_copie_temporaire_installer_puis_purger_ne_reecrit_jamais_la_cle_mal_formee()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosPrudences_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var settings = Path.Combine(dir, "settings.json");
            File.WriteAllText(settings, FixtureMalFormee);

            var installateur = new SessionHookInstaller(settings);
            Assert.StartsWith(Path.GetTempPath(), installateur.SettingsPath);   // garde anti-accident

            installateur.Install(Exe);
            var apresInstall = File.ReadAllText(settings);
            Assert.Equal(ChaineSousStop, Hooks(apresInstall)["Stop"]!.GetValue<string>());
            Assert.Equal(SessionHookInstaller.Events.Length - 1, CompteHooksChronos(apresInstall));

            installateur.Uninstall();
            var apresPurge = File.ReadAllText(settings);
            Assert.Equal(ChaineSousStop, Hooks(apresPurge)["Stop"]!.GetValue<string>());
            Assert.Equal(0, CompteHooksChronos(apresPurge));

            // Le groupe tiers de PreToolUse a traversé les deux passages sans une égratignure.
            var tiers = (Hooks(apresPurge)["PreToolUse"] as JsonArray)!;
            var groupe = (tiers.Single() as JsonObject)!;
            Assert.Equal("Write|Edit", groupe["matcher"]!.GetValue<string>());
            Assert.Equal(20, ((groupe["hooks"] as JsonArray)!.Single() as JsonObject)!["timeout"]!.GetValue<int>());
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
