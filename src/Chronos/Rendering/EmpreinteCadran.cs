using System.Windows;
using Chronos.Services;

namespace Chronos.Rendering;

/// <summary>Empreinte DIP de chaque cadran à l'échelle 1 (plus de Viewbox, phase 40). Miroir PUR des tokens
/// CadranLargeur*/CadranHauteur* de Resources/DesignTokens.xaml (égalité prouvée par EmpreinteCadranTests) :
/// aucune lecture de dictionnaire à l'exécution, donc testable en [Fact] sans STA.</summary>
public static class EmpreinteCadran
{
    public static Size Pour(CadranStyle style, OrientationCadran orientation) => (style, orientation) switch
    {
        (CadranStyle.Fusible, OrientationCadran.Horizontal) => new Size(190, 92),
        (CadranStyle.Fusible, OrientationCadran.Vertical)   => new Size(110, 190),
        (CadranStyle.Maree,   OrientationCadran.Vertical)   => new Size(132, 160),
        (CadranStyle.Maree,   OrientationCadran.Horizontal) => new Size(190, 96),
        (CadranStyle.Volets,  OrientationCadran.Horizontal) => new Size(190, 66),
        (CadranStyle.Volets,  OrientationCadran.Vertical)   => new Size(128, 190),
        _ => new Size(170, 170),   // Arcs, Braises : l'orientation est sans objet
    };

    /// <summary>Bande réservée sous l'empreinte des cadrans rectangulaires pour la rangée de pastilles (DESIGN_PLAN_CYCLE2
    /// §11 B1) ; miroir du token CadranBandePastilles, prouvé par EmpreinteCadranTests.</summary>
    public const double BandePastilles = 14;

    /// <summary>Hauteur de bande des pastilles du style : 14 pour Fusible, Marée et Volets ; 0 pour Arcs et Braises, dont la
    /// rangée tient dans l'empreinte (borne dure x ≥ 96).</summary>
    public static double Bande(CadranStyle style)
        => style is CadranStyle.Fusible or CadranStyle.Maree or CadranStyle.Volets ? BandePastilles : 0;

    /// <summary>Taille de la fenêtre overlay : l'empreinte du cadran reste contractuelle, seule la fenêtre grandit (de la bande
    /// des pastilles, en permanence : aucun saut à l'apparition d'une pastille).</summary>
    public static Size Fenetre(CadranStyle style, OrientationCadran orientation)
    {
        var empreinte = Pour(style, orientation);
        return new Size(empreinte.Width, empreinte.Height + Bande(style));
    }
}
