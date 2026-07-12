using System.Windows;
using Chronos.ViewModels;

namespace Chronos.Views;

/// <summary>
/// Galerie de prévisualisation des 4 pistes de cadran (prototype, lancée via « --cadrans »). DataContext =
/// <see cref="CadranPreviewViewModel"/> (données d'échantillon pilotables). N'est PAS branchée sur le
/// pipeline temps réel : sert uniquement à juger les concepts au coup d'œil.
/// </summary>
public partial class CadranGalleryWindow : Window
{
    public CadranGalleryWindow()
    {
        InitializeComponent();
        DataContext = new CadranPreviewViewModel();
    }
}
