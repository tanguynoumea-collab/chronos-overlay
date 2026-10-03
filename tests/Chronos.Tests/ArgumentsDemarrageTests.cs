using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// SOC-02 — tri PUR des arguments de démarrage, en liste blanche. Table de vérité : préséance et sémantique identiques à la
/// 3.4.0 (mode cherché parmi TOUS les arguments, insensible à la casse) ; tout « --xxx » absent de la liste blanche (inconnu
/// ou retiré) donne ArgumentInconnu → sortie silencieuse code 0 avant le verrou.
/// </summary>
public sealed class ArgumentsDemarrageTests
{
    [Theory]
    // Aucun argument, ou arguments sans « -- » : overlay, comme en 3.4.0 (seuls les « --xxx » sont visés).
    [InlineData("", ModeDemarrage.Overlay, null)]
    [InlineData("Stop", ModeDemarrage.Overlay, null)]
    [InlineData("-x", ModeDemarrage.Overlay, null)]
    [InlineData("/foo", ModeDemarrage.Overlay, null)]
    // Modes connus, insensibles à la casse.
    [InlineData("--statusline", ModeDemarrage.StatusLine, null)]
    [InlineData("--STATUSLINE", ModeDemarrage.StatusLine, null)]
    [InlineData("--hook Stop", ModeDemarrage.Hook, "Stop")]
    [InlineData("--hook", ModeDemarrage.Hook, null)]
    [InlineData("--HOOK Notification", ModeDemarrage.Hook, "Notification")]
    [InlineData("--cadrans", ModeDemarrage.GalerieCadrans, null)]
    [InlineData("--sessions", ModeDemarrage.GalerieSessions, null)]
    [InlineData("--historique", ModeDemarrage.GalerieHistorique, null)]
    // Argument « --xxx » inconnu : sortie silencieuse.
    [InlineData("--zzz", ModeDemarrage.ArgumentInconnu, null)]
    [InlineData("--", ModeDemarrage.ArgumentInconnu, null)]
    [InlineData("--zzz Stop", ModeDemarrage.ArgumentInconnu, null)]
    // Un mode connu présent n'importe où l'emporte sur un inconnu qui l'accompagne.
    [InlineData("--hook Stop --zzz", ModeDemarrage.Hook, "Stop")]
    [InlineData("--zzz --cadrans", ModeDemarrage.GalerieCadrans, null)]
    // Préséance 3.4.0 : --statusline > --hook > --cadrans > --sessions > --historique.
    [InlineData("--statusline --hook X", ModeDemarrage.StatusLine, null)]
    [InlineData("--hook X --cadrans", ModeDemarrage.Hook, "X")]
    [InlineData("--cadrans --sessions", ModeDemarrage.GalerieCadrans, null)]
    [InlineData("--sessions --historique", ModeDemarrage.GalerieSessions, null)]
    public void Tri_des_arguments_reproduit_la_3_4_0_et_ecarte_l_inconnu(string ligne, ModeDemarrage attendu, string? evenement)
    {
        var args = ligne.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var invocation = ArgumentsDemarrage.Trier(args);

        Assert.Equal(attendu, invocation.Mode);
        Assert.Equal(evenement, invocation.EvenementHook);
    }

    [Fact]
    public void Chaque_mode_connu_est_reconnu()
    {
        foreach (var a in ArgumentsDemarrage.ModesConnus)
        {
            var mode = ArgumentsDemarrage.Trier(new[] { a }).Mode;
            Assert.NotEqual(ModeDemarrage.ArgumentInconnu, mode);
            Assert.NotEqual(ModeDemarrage.Overlay, mode);
        }

        // la phase 37 retire --statusline : passer à 4
        Assert.Equal(5, ArgumentsDemarrage.ModesConnus.Count);
    }

    /// <summary>Preuve de la liste blanche : un « --nom » qui n'y figure pas (proche d'un mode, ancien, inventé) sort en silence.</summary>
    [Theory]
    [InlineData("--statusline-v2")]
    [InlineData("--recalibrer")]
    [InlineData("--oauth")]
    [InlineData("--cli")]
    [InlineData("--hooks")]
    [InlineData("--cadran")]
    [InlineData("--x")]
    public void Tout_argument_absent_de_la_liste_blanche_sort_en_silence(string arg)
    {
        Assert.DoesNotContain(arg, ArgumentsDemarrage.ModesConnus, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(ModeDemarrage.ArgumentInconnu, ArgumentsDemarrage.Trier(new[] { arg }).Mode);
    }
}
