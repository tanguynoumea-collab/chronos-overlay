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

    private static JsonNode Root(string json) => JsonNode.Parse(json)!;
    private static string? Cmd(string json) => Root(json)["statusLine"]?["command"]?.GetValue<string>();

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

        var outJson = StatusLineInstaller.TransformForUninstall(installed, Exe, innerCommand: "node gsd.js");

        Assert.Equal("node gsd.js", Cmd(outJson));
    }

    [Fact]
    public void Uninstall_sans_inner_retire_completement_statusLine()
    {
        var installed = SettingsWith(ChronosCmd, "model", "opus");

        var outJson = StatusLineInstaller.TransformForUninstall(installed, Exe, innerCommand: null);

        Assert.Null(Root(outJson)["statusLine"]);                       // statusLine retirée
        Assert.Equal("opus", Root(outJson)["model"]!.GetValue<string>()); // reste préservé
    }

    [Fact]
    public void Uninstall_ne_touche_pas_une_barre_tierce()
    {
        var tierce = """{ "statusLine": { "type": "command", "command": "node autre-barre.js" } }""";

        var outJson = StatusLineInstaller.TransformForUninstall(tierce, Exe, innerCommand: "peu importe");

        Assert.Equal("node autre-barre.js", Cmd(outJson)); // pas Chronos → intouchée
    }
}
