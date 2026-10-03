using System.Windows;
using System.Windows.Controls;

namespace Chronos.Views.Cadrans;

public partial class CadranMareeView : UserControl
{
    /// <summary>Orientation du cadran Marée (phase 40). Portée par la VUE et non par le DataContext : la galerie montre H et V
    /// en même temps sur un seul VM ; l'overlay la lie à l'orientation choisie dans les réglages.</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(CadranMareeView),
        new FrameworkPropertyMetadata(Orientation.Vertical, (d, _) => ((CadranMareeView)d).AppliquerOrientation()));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public CadranMareeView()
    {
        InitializeComponent();
        AppliquerOrientation();
    }

    /// <summary>Montre exactement un des deux gabarits (logique de vue pure).</summary>
    private void AppliquerOrientation()
    {
        if (GabaritHorizontal is null || GabaritVertical is null) return;   // rappel possible avant InitializeComponent
        bool h = Orientation == Orientation.Horizontal;
        GabaritHorizontal.Visibility = h ? Visibility.Visible : Visibility.Collapsed;
        GabaritVertical.Visibility = h ? Visibility.Collapsed : Visibility.Visible;
    }
}
