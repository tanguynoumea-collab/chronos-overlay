using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la transformation PURE de settings.json (<see cref="StatusLineInstaller"/>) : installation
/// non destructive (chaînage de la barre préexistante, préservation des autres réglages), idempotence,
/// et désinstallation réversible (restauration ou retrait), sans jamais toucher une barre tierce.
/// </summary>
public class StatusLineInstallerTests
{
    private const string Exe = @"C:\Apps\Chronos.exe";
    private static string ChronosCmd => StatusLineInstaller.ChronosCommand(Exe);

    private static JsonNode Root(string? json) => JsonNode.Parse(json!)!;
    private static string? Cmd(string? json) => Root(json)["statusLine"]?["command"]?.GetValue<string>();

    // Construit un settings.json valide (échappement correct des guillemets de la commande Chronos).
    private static string SettingsWith(string command, string? extraKey = null, string? extraVal = null)
    {
        var o = new JsonObject { ["statusLine"] = new JsonObject { ["type"] = "command", ["command"] = command } };
        if (extraKey is not null) o[extraKey] = extraVal;
        return o.ToJsonString();
    }

    [Fact]
    public void Install_sur_settings_absent_pose_la_commande_Chronos_sans_inner()
    {
        var outJson = StatusLineInstaller.TransformForInstall(null, Exe, out var inner);

        Assert.Null(inner);
        Assert.Equal(ChronosCmd, Cmd(outJson));
        Assert.Equal("command", Root(outJson)["statusLine"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Install_chaine_la_barre_existante_et_preserve_les_autres_cles()
    {
        var existing = """{ "model": "opus", "statusLine": { "type": "command", "command": "node gsd.js" } }""";

        var outJson = StatusLineInstaller.TransformForInstall(existing, Exe, out var inner);

        Assert.Equal("node gsd.js", inner);              // barre préexistante mémorisée pour le chaînage
        Assert.Equal(ChronosCmd, Cmd(outJson));           // statusLine pointe désormais sur Chronos
        Assert.Equal("opus", Root(outJson)["model"]!.GetValue<string>()); // autres réglages intacts
    }

    [Fact]
    public void Install_idempotent_ne_rechaine_pas_Chronos_sur_lui_meme()
    {
        var already = SettingsWith(ChronosCmd);

        var outJson = StatusLineInstaller.TransformForInstall(already, Exe, out var inner);

        Assert.Null(inner);                    // déjà Chronos → rien à mémoriser (pas de boucle)
        Assert.Equal(ChronosCmd, Cmd(outJson));
    }

    [Fact]
    public void Uninstall_restaure_la_barre_dorigine()
    {
        var installed = SettingsWith(ChronosCmd);

        var outJson = StatusLineInstaller.TransformForUninstall(installed, innerCommand: "node gsd.js");

        Assert.Equal("node gsd.js", Cmd(outJson));
    }

    [Fact]
    public void Uninstall_sans_inner_retire_completement_statusLine()
    {
        var installed = SettingsWith(ChronosCmd, "model", "opus");

        var outJson = StatusLineInstaller.TransformForUninstall(installed, innerCommand: null);

        Assert.Null(Root(outJson)["statusLine"]);                       // statusLine retirée
        Assert.Equal("opus", Root(outJson)["model"]!.GetValue<string>()); // reste préservé
    }

    [Fact]
    public void Uninstall_ne_touche_pas_une_barre_tierce()
    {
        var tierce = """{ "statusLine": { "type": "command", "command": "node autre-barre.js" } }""";

        var outJson = StatusLineInstaller.TransformForUninstall(tierce, innerCommand: "peu importe");

        Assert.Equal("node autre-barre.js", Cmd(outJson)); // pas Chronos → intouchée
    }

    // PUR-02 — critère de succès 2 : une barre Chronos PÉRIMÉE est REPOINTÉE sur l'exe courant,
    // sans être dupliquée ni chaînée sur elle-même, et « padding » survit à l'opération.
    [Fact]
    public void Install_repointe_une_barre_Chronos_perimee_sans_dupliquer()
    {
        var perime = new JsonObject
        {
            ["statusLine"] = new JsonObject
            {
                ["type"] = "command",
                ["command"] = "\"C:/DL/Chronos-v2.8.1.exe\" --statusline",
                ["padding"] = 2,
            },
        }.ToJsonString();

        var outJson = StatusLineInstaller.TransformForInstall(perime, Exe, out var inner);

        Assert.Null(inner);                        // une barre Chronos n'est JAMAIS chaînée sur elle-même
        Assert.Equal(ChronosCmd, Cmd(outJson));    // repointée sur CET exe
        Assert.Equal(2, Root(outJson)["statusLine"]!["padding"]!.GetValue<int>());
    }

    // PUR-02 — « padding » (champ officiel optionnel) et toute clé future survivent : la mutation est
    // ciblée sur « command ». Reconstruire l'objet les effaçait sans bruit.
    [Fact]
    public void Install_preserve_padding_et_les_cles_inconnues()
    {
        var avecExtras = new JsonObject
        {
            ["statusLine"] = new JsonObject
            {
                ["type"] = "command",
                ["command"] = "node autre.js",
                ["padding"] = 2,
                ["futur"] = "valeur",
            },
        }.ToJsonString();

        var outJson = StatusLineInstaller.TransformForInstall(avecExtras, Exe, out var inner);

        Assert.Equal("node autre.js", inner);           // barre tierce mémorisée pour le chaînage
        Assert.Equal(ChronosCmd, Cmd(outJson));
        var sl = Root(outJson)["statusLine"]!;
        Assert.Equal(2, sl["padding"]!.GetValue<int>());
        Assert.Equal("valeur", sl["futur"]!.GetValue<string>());
    }

    // PUR-02 — point fixe : réinstaller le même exe ne change plus rien, et ne re-capture rien.
    [Fact]
    public void Install_est_un_point_fixe()
    {
        var une = StatusLineInstaller.TransformForInstall("""{"model":"opus"}""", Exe, out _);
        var deux = StatusLineInstaller.TransformForInstall(une, Exe, out var inner2);

        Assert.Equal(une, deux);
        Assert.Null(inner2);
    }

    // PUR-02 — ApplyStatusLine ne fait que REPOINTER : il ne crée jamais l'entrée (le consentement
    // reste porté par le menu / StatusLinePromptDismissed) et n'attrape jamais une barre tierce.
    [Fact]
    public void ApplyStatusLine_ne_touche_ni_une_barre_tierce_ni_une_cle_absente()
    {
        var tierce = (JsonNode.Parse("""{"statusLine":{"type":"command","command":"node autre.js"}}""") as JsonObject)!;
        StatusLineInstaller.ApplyStatusLine(tierce, Exe);
        Assert.Equal("node autre.js", tierce["statusLine"]!["command"]!.GetValue<string>());

        var sans = (JsonNode.Parse("""{"model":"opus"}""") as JsonObject)!;
        StatusLineInstaller.ApplyStatusLine(sans, Exe);
        Assert.Null(sans["statusLine"]);   // aucune installation sans consentement
    }

    // PUR-03 — un settings.json INEXPLOITABLE ne produit AUCUNE écriture : les cœurs purs renvoient null.
    [Theory]
    [InlineData("""{"a":1,"a":2}""")]
    [InlineData("[1,2,3]")]
    [InlineData("{ cassé")]
    [InlineData("\"scalaire\"")]
    public void Install_sur_json_inexploitable_ne_produit_rien(string json)
    {
        Assert.Null(StatusLineInstaller.TransformForInstall(json, Exe, out _));
        Assert.Null(StatusLineInstaller.TransformForUninstall(json, innerCommand: null));
    }

    // PUR-02 — le retrait est LARGE : une barre Chronos d'une AUTRE version est retirée elle aussi.
    [Fact]
    public void Uninstall_retire_une_barre_Chronos_dune_autre_version()
    {
        var perime = SettingsWith("\"C:/DL/Chronos-v2.6.exe\" --statusline", "model", "opus");

        var outJson = StatusLineInstaller.TransformForUninstall(perime, innerCommand: null);

        Assert.Null(Root(outJson)["statusLine"]);
        Assert.Equal("opus", Root(outJson)["model"]!.GetValue<string>());
    }

    // PUR-02 — slashes AVANT, comme SessionHookInstaller.HookCommand (des backslashes seraient
    // avalés par le shell — leçon vérifiée sur la vraie machine).
    [Fact]
    public void ChronosCommand_utilise_des_slashes_avant()
    {
        var cmd = StatusLineInstaller.ChronosCommand(@"C:\Apps\Chronos.exe");

        Assert.DoesNotContain("\\", cmd);
        Assert.Contains("C:/Apps/Chronos.exe", cmd);
    }

    // PUR-02 — FRAÎCHEUR : IsEnabled() répondait « oui » pour un exe périmé, donc le pont restait
    // branché à vie sur Chronos-v2.8.1.exe. Chemin injecté depuis Path.GetTempPath() : ce test ne
    // peut pas atteindre le vrai ~/.claude/settings.json.
    [Fact]
    public void IsInstalled_est_faux_quand_la_barre_pointe_une_autre_version()
    {
        var fichier = Path.Combine(Path.GetTempPath(), "chronos-sl-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(fichier, SettingsWith("\"C:/DL/Chronos-v2.8.1.exe\" --statusline"));
        try
        {
            var installer = new StatusLineInstaller(fichier);   // chemin TEMP, jamais le profil utilisateur
            Assert.False(installer.IsInstalled(Exe));                            // pas CET exe
            Assert.True(installer.IsInstalled(@"C:\DL\Chronos-v2.8.1.exe"));     // mais bien celui-là
        }
        finally { File.Delete(fichier); }
    }
}
