using System.IO;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// ACC-02 — garde TEXTUELLE des gestes du cadran. <c>MouseButtonEventArgs.ClickCount</c> n'est pas réglable en test
/// (setter interne) : on garde donc la FORME du code-behind — il ne fait que transmettre le compte de clics au
/// ViewModel (la décision vit dans l'arbitre pur), il marque toujours l'événement Handled (pas de DragMove depuis le
/// centre), et le drag par les anneaux comme le clic droit (réglages) restent câblés tels quels.
/// </summary>
public class GardeGestesCadranTests
{
    private static string Lire(params string[] morceaux)
    {
        var fichier = Path.Combine(new[] { GardesPerimetreTests.CheminSources() }.Concat(morceaux).ToArray());
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");
        var texte = File.ReadAllText(fichier);
        Assert.True(texte.Length > 200, $"{fichier} est suspicieusement court : la garde ne lit pas le vrai fichier.");
        return texte;
    }

    /// <summary>Le corps du gestionnaire du centre : de sa signature jusqu'à la première accolade fermante de méthode.</summary>
    private static string CorpsDuCentre(string texte)
    {
        var debut = texte.IndexOf("private void CentreHit_MouseLeftButtonDown", StringComparison.Ordinal);
        Assert.True(debut >= 0, "CentreHit_MouseLeftButtonDown introuvable dans MainWindow.xaml.cs");
        var fin = texte.IndexOf("\n    }", debut, StringComparison.Ordinal);
        Assert.True(fin > debut, "fin du gestionnaire du centre introuvable");
        return texte[debut..fin];
    }

    [Fact]
    public void Le_centre_transmet_le_compte_de_clics_et_reste_Handled()
    {
        var corps = CorpsDuCentre(Lire("Views", "MainWindow.xaml.cs"));

        Assert.Contains("ClicCentre(e.ClickCount)", corps, StringComparison.Ordinal);
        Assert.Contains("e.Handled = true", corps, StringComparison.Ordinal);
        Assert.DoesNotContain("ToggleCenterMode()", corps, StringComparison.Ordinal);   // la bascule directe ferait double emploi
    }

    [Fact]
    public void Le_drag_et_le_clic_droit_sont_inchanges()
    {
        var code = Lire("Views", "MainWindow.xaml.cs");
        Assert.Contains("MouseLeftButtonDown += Cadran_MouseLeftButtonDown", code, StringComparison.Ordinal);
        Assert.Contains("DragMove();", code, StringComparison.Ordinal);

        var xaml = Lire("Views", "MainWindow.xaml");
        Assert.Contains("MouseRightButtonUp=\"OnRightClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MouseLeftButtonDown=\"CentreHit_MouseLeftButtonDown\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_delai_vient_du_systeme_et_l_arbitre_reste_pur()
    {
        Assert.Contains("GetDoubleClickTime", Lire("Interop", "NativeMethods.cs"), StringComparison.Ordinal);

        var arbitre = Lire("ViewModels", "ArbitreClicCentre.cs");
        Assert.DoesNotContain("System.Windows", arbitre, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", arbitre, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", arbitre, StringComparison.Ordinal);

        Assert.DoesNotContain("Thread.Sleep", Lire("ViewModels", "MainViewModel.cs"), StringComparison.Ordinal);
    }
}
