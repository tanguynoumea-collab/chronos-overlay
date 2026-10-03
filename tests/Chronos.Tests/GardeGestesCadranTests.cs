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

    [Fact]
    public void Une_restauration_sans_taille_est_recalee_a_la_premiere_mise_en_page()
    {
        // Filet CAD-02 : si RestorePlacement a posé la fenêtre sans taille connue, la première mise en page recale une fois.
        var code = Lire("Views", "MainWindow.xaml.cs");
        Assert.Contains("RestaurationSansTaille", code, StringComparison.Ordinal);
    }

    /// <summary>CAD-02 : le changement d'empreinte recale sur le coin COURANT (jamais le plus proche), sans persistance,
    /// sans toucher au z-order ni passer par Window.Left/Top ; ignoré pendant un glisser.</summary>
    [Fact]
    public void Le_changement_d_empreinte_recale_sur_le_coin_courant()
    {
        var vue = Lire("Views", "MainWindow.xaml.cs");
        Assert.Contains("SizeChanged +=", vue, StringComparison.Ordinal);
        Assert.Contains("RecalerSurCoinCourant()", vue, StringComparison.Ordinal);
        Assert.Contains("PreviousSize", vue, StringComparison.Ordinal);
        Assert.Contains("_enDeplacement = true", vue, StringComparison.Ordinal);
        Assert.Contains("finally", vue, StringComparison.Ordinal);
        Assert.Contains("DragMove();", vue, StringComparison.Ordinal);

        var ctrl = Lire("Services", "OverlayController.cs").Replace("\r\n", "\n");
        const string signature = "public void RecalerSurCoinCourant()";
        var debut = ctrl.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(debut >= 0, "RecalerSurCoinCourant introuvable dans OverlayController.cs");
        var apres = debut + signature.Length;
        var fin = new[]
        {
            ctrl.IndexOf("\n    public ", apres, StringComparison.Ordinal),
            ctrl.IndexOf("\n    private ", apres, StringComparison.Ordinal),
        }.Where(i => i > 0).DefaultIfEmpty(ctrl.Length).Min();
        var corps = ctrl[debut..fin];

        Assert.Contains("CornerSnap.RecalerSurCoin(", corps, StringComparison.Ordinal);
        Assert.Contains("SWP_NOZORDER", corps, StringComparison.Ordinal);
        Assert.DoesNotContain("NearestCorner", corps, StringComparison.Ordinal);
        Assert.DoesNotContain("ClassifyCorner", corps, StringComparison.Ordinal);
        Assert.DoesNotContain("_settings.Save", corps, StringComparison.Ordinal);
        Assert.DoesNotContain(".Left =", corps, StringComparison.Ordinal);
        Assert.DoesNotContain(".Top =", corps, StringComparison.Ordinal);
    }
}
