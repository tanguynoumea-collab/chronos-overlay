using System.Text.Json.Nodes;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve le socle d'identité et de tolérance sur lequel reposent PUR-01, PUR-02 et PUR-03 : repérage
/// par marqueur + nom d'exe (jamais le chemin), abandon au lieu d'effacement, sérialisation sans
/// mutilation des accents.
///
/// Tests PURS : aucun accès au système de fichiers, et donc aucun risque d'atteindre le vrai
/// %USERPROFILE%\.claude\settings.json. Les chaînes ci-dessous sont des littéraux figés copiés de
/// l'état réellement constaté sur la machine le 2026-09-09.
/// </summary>
public class ClaudeSettingsJsonTests
{
    // --- Commandes réelles relevées dans ~/.claude/settings.json (25 groupes Chronos + 3 groupes GSD) ---

    private const string ChronosV25   = "\"C:/Users/Tanguy/Downloads/Chronos-v2.5.exe\" --hook SessionStart";
    private const string ChronosV251  = "\"C:/Users/Tanguy/Downloads/Chronos-v2.5.1.exe\" --hook SessionStart";
    private const string ChronosV26   = "\"C:/Users/Tanguy/Downloads/Chronos-v2.6.exe\" --hook SessionStart";
    private const string ChronosV281  = "\"C:/Users/Tanguy/Downloads/Chronos-v2.8.1.exe\" --hook SessionStart";
    private const string ChronosDebug = "\"C:/Users/Tanguy/Documents/PROGRAMMES/DEV/PROJET OVERLAY/src/Chronos/bin/Debug/net8.0-windows/Chronos.exe\" --hook SessionStart";
    private const string GsdUpdate    = "node \"C:/Users/Tanguy/.claude/hooks/gsd-check-update.js\"";
    private const string GsdMonitor   = "node \"C:/Users/Tanguy/.claude/hooks/gsd-context-monitor.js\"";
    private const string GsdGuard     = "node \"C:/Users/Tanguy/.claude/hooks/gsd-prompt-guard.js\"";

    private static JsonNode Noeud(string json) => JsonNode.Parse(json)!;

    // --- ExtractExecutable : premier jeton, guillemets et espaces ---

    [Fact]
    public void ExtractExecutable_prend_le_segment_entre_guillemets()
        => Assert.Equal("C:/DL/Chronos-v2.5.exe",
            ClaudeSettingsJson.ExtractExecutable("\"C:/DL/Chronos-v2.5.exe\" --hook Stop"));

    [Fact]
    public void ExtractExecutable_tolere_un_chemin_avec_espaces()
        => Assert.Equal("C:/PROJET OVERLAY/Chronos.exe",
            ClaudeSettingsJson.ExtractExecutable("  \"C:/PROJET OVERLAY/Chronos.exe\" --hook Stop"));

    [Fact]
    public void ExtractExecutable_sans_guillemets_sarrete_au_premier_espace()
        => Assert.Equal("node", ClaudeSettingsJson.ExtractExecutable("node \"C:/x/gsd.js\""));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractExecutable_sur_commande_vide_donne_une_chaine_vide(string? commande)
        => Assert.Equal("", ClaudeSettingsJson.ExtractExecutable(commande));

    // --- IsChronosCommand : marqueur + NOM d'exe, jamais le chemin (cause racine du cumul) ---

    [Theory]
    [InlineData(ChronosV25)]
    [InlineData(ChronosV251)]
    [InlineData(ChronosV26)]
    [InlineData(ChronosV281)]
    [InlineData(ChronosDebug)]
    public void Reconnait_les_cinq_chemins_dexe_Chronos_historiques(string commande)
        => Assert.True(ClaudeSettingsJson.IsChronosCommand(commande, ClaudeSettingsJson.HookMarker));

    [Theory]
    [InlineData(GsdUpdate)]
    [InlineData(GsdMonitor)]
    [InlineData(GsdGuard)]
    public void Ne_reconnait_pas_les_hooks_GSD(string commande)
        => Assert.False(ClaudeSettingsJson.IsChronosCommand(commande, ClaudeSettingsJson.HookMarker));

    [Fact]
    public void Ne_reconnait_pas_un_exe_tiers_portant_le_marqueur()
        => Assert.False(ClaudeSettingsJson.IsChronosCommand("\"C:/x/autre.exe\" --hook Stop",
            ClaudeSettingsJson.HookMarker));

    [Fact]
    public void Marqueur_statusline_et_marqueur_hook_ne_se_confondent_pas()
    {
        const string barre = "\"C:/x/Chronos.exe\" --statusline";
        const string hook = "\"C:/x/Chronos.exe\" --hook Stop";

        Assert.False(ClaudeSettingsJson.IsChronosCommand(barre, ClaudeSettingsJson.HookMarker));
        Assert.True(ClaudeSettingsJson.IsChronosCommand(barre, ClaudeSettingsJson.StatusLineMarker));
        Assert.True(ClaudeSettingsJson.IsChronosCommand(hook, ClaudeSettingsJson.HookMarker));
        Assert.False(ClaudeSettingsJson.IsChronosCommand(hook, ClaudeSettingsJson.StatusLineMarker));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsChronosCommand_sur_commande_vide_est_faux(string? commande)
        => Assert.False(ClaudeSettingsJson.IsChronosCommand(commande, ClaudeSettingsJson.HookMarker));

    // --- PointsToExe : la FRAÎCHEUR, question distincte de l'appartenance ---

    [Fact]
    public void PointsToExe_normalise_les_backslashes()
        => Assert.True(ClaudeSettingsJson.PointsToExe("\"C:/DL/Chronos.exe\" --statusline",
            @"C:\DL\Chronos.exe"));

    [Fact]
    public void PointsToExe_refuse_un_exe_dune_autre_version()
        => Assert.False(ClaudeSettingsJson.PointsToExe("\"C:/DL/Chronos-v2.5.exe\" --statusline",
            @"C:\DL\Chronos.exe"));

    [Fact]
    public void PointsToExe_est_faux_sur_entrees_vides()
    {
        Assert.False(ClaudeSettingsJson.PointsToExe(null, @"C:\DL\Chronos.exe"));
        Assert.False(ClaudeSettingsJson.PointsToExe("\"C:/DL/Chronos.exe\" --statusline", ""));
    }

    // --- CommandOf : ne lève jamais, quel que soit le type de handler ---

    [Fact]
    public void CommandOf_lit_une_commande_textuelle()
        => Assert.Equal("x", ClaudeSettingsJson.CommandOf(Noeud("""{"type":"command","command":"x"}""")));

    [Fact]
    public void CommandOf_ne_leve_pas_sur_un_command_numerique()
        => Assert.Null(ClaudeSettingsJson.CommandOf(Noeud("""{"type":"command","command":42}""")));

    [Fact]
    public void CommandOf_ne_leve_pas_sur_un_handler_http()
        => Assert.Null(ClaudeSettingsJson.CommandOf(Noeud("""{"type":"http","url":"https://x"}""")));

    [Fact]
    public void CommandOf_sur_null_donne_null()
        => Assert.Null(ClaudeSettingsJson.CommandOf(null));

    // --- IsChronosGroup : le groupe { "hooks": [ … ] } ---

    [Fact]
    public void IsChronosGroup_reconnait_un_groupe_Chronos()
    {
        var groupe = Noeud("""{"hooks":[{"type":"command","command":"\"C:/DL/Chronos-v2.5.exe\" --hook Stop"}]}""");
        Assert.True(ClaudeSettingsJson.IsChronosGroup(groupe, ClaudeSettingsJson.HookMarker));
    }

    [Fact]
    public void IsChronosGroup_ignore_un_groupe_GSD_avec_matcher_et_timeout()
    {
        var groupe = Noeud("""{"matcher":"Write|Edit","hooks":[{"type":"command","command":"node gsd.js","timeout":5}]}""");
        Assert.False(ClaudeSettingsJson.IsChronosGroup(groupe, ClaudeSettingsJson.HookMarker));
    }

    [Fact]
    public void IsChronosGroup_ne_leve_pas_sur_un_handler_sans_commande()
    {
        var groupe = Noeud("""{"hooks":[{"type":"http","url":"https://x"}]}""");
        Assert.False(ClaudeSettingsJson.IsChronosGroup(groupe, ClaudeSettingsJson.HookMarker));
    }

    // --- ParseOrNull : tolérer ce qui est lisible, ABANDONNER sur le reste (jamais d'objet vide) ---

    [Fact]
    public void ParseOrNull_sur_fichier_absent_donne_un_objet_vide()
    {
        var vide = ClaudeSettingsJson.ParseOrNull(null);
        Assert.NotNull(vide);
        Assert.Empty(vide!);

        var blanc = ClaudeSettingsJson.ParseOrNull("   ");
        Assert.NotNull(blanc);
        Assert.Empty(blanc!);
    }

    [Fact]
    public void ParseOrNull_tolere_commentaires_et_virgule_trainante()
    {
        var o = ClaudeSettingsJson.ParseOrNull("{ \"a\": 1, // note\n \"b\": 2, }");

        Assert.NotNull(o);
        Assert.Equal(2, o!.Count);
    }

    [Fact]
    public void ParseOrNull_abandonne_sur_cles_dupliquees()
        => Assert.Null(ClaudeSettingsJson.ParseOrNull("""{"a":1,"a":2}"""));

    [Fact]
    public void ParseOrNull_abandonne_sur_racine_tableau()
        => Assert.Null(ClaudeSettingsJson.ParseOrNull("[1,2,3]"));

    [Fact]
    public void ParseOrNull_abandonne_sur_racine_scalaire()
        => Assert.Null(ClaudeSettingsJson.ParseOrNull("\"txt\""));

    [Fact]
    public void ParseOrNull_abandonne_sur_json_invalide()
        => Assert.Null(ClaudeSettingsJson.ParseOrNull("{ pas du json"));

    // --- Serialize : fidélité (accents, caractères HTML, ordre, texte brut des nombres) ---

    [Fact]
    public void Serialize_conserve_les_accents_litteraux()
    {
        var root = ClaudeSettingsJson.ParseOrNull("{}")!;
        root["exe"] = "C:/Users/Tanguy/Téléchargements/Chronos.exe";

        var sortie = ClaudeSettingsJson.Serialize(root);

        Assert.Contains("Téléchargements", sortie);
        Assert.DoesNotContain("\\u00E9", sortie);
        Assert.DoesNotContain("\\u00e9", sortie);
    }

    [Fact]
    public void Serialize_conserve_les_caracteres_html_litteraux()
    {
        var root = ClaudeSettingsJson.ParseOrNull("{}")!;
        root["a"] = "x&y<z>w+v";

        var sortie = ClaudeSettingsJson.Serialize(root);

        Assert.Contains("x&y<z>w+v", sortie);
        Assert.DoesNotContain("\\u0026", sortie);
    }

    [Fact]
    public void Serialize_preserve_lordre_des_cles_et_le_texte_brut_des_nombres()
    {
        var root = ClaudeSettingsJson.ParseOrNull("""{"z":5817635413,"a":1.0,"m":"x"}""")!;

        var sortie = ClaudeSettingsJson.Serialize(root);

        Assert.True(sortie.IndexOf("\"z\"", StringComparison.Ordinal) < sortie.IndexOf("\"a\"", StringComparison.Ordinal));
        Assert.True(sortie.IndexOf("\"a\"", StringComparison.Ordinal) < sortie.IndexOf("\"m\"", StringComparison.Ordinal));
        Assert.Contains("5817635413", sortie);
        Assert.Contains("1.0", sortie);
    }
}
