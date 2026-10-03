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
}
