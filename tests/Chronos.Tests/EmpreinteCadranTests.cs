using System.Globalization;
using System.IO;
using System.Windows;
using System.Xml.Linq;
using Chronos.Rendering;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Phase 40 (CAD-01 / CAD-04) — empreintes DIP des cadrans à l'échelle 1. Deux contrats prouvés égaux : les tokens
/// <c>CadranLargeur*</c>/<c>CadranHauteur*</c> de <c>Resources/DesignTokens.xaml</c> (source de vérité visuelle) et la fonction
/// pure <see cref="EmpreinteCadran.Pour"/> (miroir C#, consommé sans dictionnaire ni STA). Lecture XML du dictionnaire (faits
/// purs). Le décompte global des <c>sys:Double</c> est tenu par <see cref="GardeTokensHistoriqueTests"/>, qui additionne
/// <see cref="TaillesCadran"/> : un token de cadran non déclaré ici fait rougir les deux gardes.
/// </summary>
[Collection("XAML WPF")]
public class EmpreinteCadranTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Sys = "clr-namespace:System;assembly=mscorlib";

    /// <summary>Table contractuelle des 21 tokens de cadran (DESIGN_PLAN_CYCLE2 §1.1 et §11 B1) : 16 empreintes (Arcs ET Braises
    /// ont leurs clés, carrées ; Fusible, Marée, Volets en H et V) + 4 corps de texte + la bande des pastilles (14).</summary>
    public static readonly (string Cle, double Valeur)[] TaillesCadran =
    {
        ("CadranLargeurArcs", 170),
        ("CadranHauteurArcs", 170),
        ("CadranLargeurBraises", 170),
        ("CadranHauteurBraises", 170),
        ("CadranLargeurFusibleH", 190),
        ("CadranHauteurFusibleH", 92),
        ("CadranLargeurFusibleV", 110),
        ("CadranHauteurFusibleV", 190),
        ("CadranLargeurMareeV", 132),
        ("CadranHauteurMareeV", 160),
        ("CadranLargeurMareeH", 190),
        ("CadranHauteurMareeH", 96),
        ("CadranLargeurVoletsH", 190),
        ("CadranHauteurVoletsH", 66),
        ("CadranLargeurVoletsV", 128),
        ("CadranHauteurVoletsV", 190),
        ("CadranCorpsLibelle", 11),
        ("CadranCorpsValeur", 12),
        ("CadranCorpsPlaqueH", 13),
        ("CadranCorpsPlaqueV", 14),
        ("CadranBandePastilles", 14),
    };

    /// <summary>Les 10 cas (style, orientation) et le suffixe de token correspondant.</summary>
    private static readonly (CadranStyle Style, OrientationCadran Orientation, string Nom)[] Cas =
    {
        (CadranStyle.Arcs, OrientationCadran.Horizontal, "Arcs"),
        (CadranStyle.Arcs, OrientationCadran.Vertical, "Arcs"),
        (CadranStyle.Braises, OrientationCadran.Horizontal, "Braises"),
        (CadranStyle.Braises, OrientationCadran.Vertical, "Braises"),
        (CadranStyle.Fusible, OrientationCadran.Horizontal, "FusibleH"),
        (CadranStyle.Fusible, OrientationCadran.Vertical, "FusibleV"),
        (CadranStyle.Maree, OrientationCadran.Vertical, "MareeV"),
        (CadranStyle.Maree, OrientationCadran.Horizontal, "MareeH"),
        (CadranStyle.Volets, OrientationCadran.Horizontal, "VoletsH"),
        (CadranStyle.Volets, OrientationCadran.Vertical, "VoletsV"),
    };

    private static XDocument Tokens()
    {
        var racine = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine), "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        var chemin = Path.Combine(racine, "Resources", "DesignTokens.xaml");
        Assert.True(File.Exists(chemin), $"Fichier introuvable : {chemin}");
        return XDocument.Load(chemin);
    }

    private static double Valeur(List<XElement> doubles, string cle)
    {
        var element = doubles.SingleOrDefault(e => e.Attribute(X + "Key")?.Value == cle);
        Assert.True(element is not null, $"DesignTokens.xaml : le sys:Double « {cle} » manque.");
        return double.Parse(element!.Value.Trim(), CultureInfo.InvariantCulture);
    }

    /// <summary>Arcs et Braises restent carrés quelle que soit l'orientation (sans objet) ; les trois cadrans rectangulaires
    /// ont une empreinte par orientation.</summary>
    [Theory]
    [InlineData(CadranStyle.Arcs, OrientationCadran.Horizontal, 170, 170)]
    [InlineData(CadranStyle.Arcs, OrientationCadran.Vertical, 170, 170)]
    [InlineData(CadranStyle.Braises, OrientationCadran.Horizontal, 170, 170)]
    [InlineData(CadranStyle.Braises, OrientationCadran.Vertical, 170, 170)]
    [InlineData(CadranStyle.Fusible, OrientationCadran.Horizontal, 190, 92)]
    [InlineData(CadranStyle.Fusible, OrientationCadran.Vertical, 110, 190)]
    [InlineData(CadranStyle.Maree, OrientationCadran.Vertical, 132, 160)]
    [InlineData(CadranStyle.Maree, OrientationCadran.Horizontal, 190, 96)]
    [InlineData(CadranStyle.Volets, OrientationCadran.Horizontal, 190, 66)]
    [InlineData(CadranStyle.Volets, OrientationCadran.Vertical, 128, 190)]
    public void Chaque_empreinte_est_contractuelle(CadranStyle style, OrientationCadran o, double largeur, double hauteur)
    {
        Assert.Equal(new Size(largeur, hauteur), EmpreinteCadran.Pour(style, o));
    }

    /// <summary>Chaque token de la table existe avec sa valeur, et chaque empreinte d'EmpreinteCadran est égale au couple
    /// de tokens CadranLargeur{Nom}/CadranHauteur{Nom} : deux sources, une seule vérité.</summary>
    [Fact]
    public void Les_tokens_de_DesignTokens_sont_le_miroir_d_EmpreinteCadran()
    {
        var doubles = Tokens().Descendants(Sys + "Double").ToList();

        foreach (var (cle, valeur) in TaillesCadran)
            Assert.True(Valeur(doubles, cle) == valeur, $"DesignTokens.xaml : « {cle} » ne vaut pas {valeur}.");

        foreach (var (style, orientation, nom) in Cas)
        {
            var empreinte = EmpreinteCadran.Pour(style, orientation);
            Assert.True(Valeur(doubles, "CadranLargeur" + nom) == empreinte.Width,
                $"CadranLargeur{nom} ≠ EmpreinteCadran.Pour({style}, {orientation}).Width ({empreinte.Width}).");
            Assert.True(Valeur(doubles, "CadranHauteur" + nom) == empreinte.Height,
                $"CadranHauteur{nom} ≠ EmpreinteCadran.Pour({style}, {orientation}).Height ({empreinte.Height}).");
        }
    }

    /// <summary>§11 B1 : la fenêtre des trois cadrans rectangulaires vaut l'empreinte + la bande de 14 px des pastilles, en
    /// permanence ; Arcs et Braises gardent 170 × 170. La largeur ne change jamais et l'empreinte reste contractuelle.</summary>
    [Theory]
    [MemberData(nameof(CasFenetre))]
    public void Fenetre_ajoute_la_bande_aux_seuls_cadrans_rectangulaires(CadranStyle style, OrientationCadran o)
    {
        var rectangulaire = style is CadranStyle.Fusible or CadranStyle.Maree or CadranStyle.Volets;
        var empreinte = EmpreinteCadran.Pour(style, o);
        var fenetre = EmpreinteCadran.Fenetre(style, o);

        Assert.Equal(14, EmpreinteCadran.BandePastilles);
        Assert.Equal(rectangulaire ? 14 : 0, EmpreinteCadran.Bande(style));
        Assert.Equal(empreinte.Width, fenetre.Width);
        Assert.Equal(empreinte.Height + (rectangulaire ? 14 : 0), fenetre.Height);
    }

    public static IEnumerable<object[]> CasFenetre() => Cas.Select(c => new object[] { c.Style, c.Orientation });

    /// <summary>§11 B2 : la plaque « indisponible » passe par deux tokens typés (coins 6, marge 6 × 2).</summary>
    [WpfFact]
    public void Les_tokens_de_plaque_sont_types()
    {
        var dico = PleinEcranVuesTests.Tokens();

        Assert.Equal(new CornerRadius(6), Assert.IsType<CornerRadius>(dico["CadranPlaqueRayon"]));
        Assert.Equal(new Thickness(6, 2, 6, 2), Assert.IsType<Thickness>(dico["CadranPlaqueMarge"]));
    }

    /// <summary>Phase 40 (CAD-01) : MainWindow n'enveloppe plus aucun cadran dans un Viewbox, et sa taille est liée à l'empreinte
    /// courante du VM (plus de 170 × 170 figé).</summary>
    [Fact]
    public void MainWindow_n_a_plus_de_Viewbox_et_lie_sa_taille()
    {
        var racine = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine), "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        var chemin = Path.Combine(racine, "Views", "MainWindow.xaml");
        Assert.True(File.Exists(chemin), $"Fichier introuvable : {chemin}");
        var xaml = File.ReadAllText(chemin);

        Assert.DoesNotContain("<Viewbox", xaml);
        Assert.Contains("Width=\"{Binding LargeurCadran, Mode=OneWay}\"", xaml);
        Assert.Contains("Height=\"{Binding HauteurFenetre, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("Width=\"170\" Height=\"170\"", xaml);
    }
}
