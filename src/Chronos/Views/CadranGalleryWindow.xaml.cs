using System.Windows;
using Chronos.Theming;
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
        var vm = new CadranPreviewViewModel();
        DataContext = vm;
        AppliquerTheme(vm.SelectedTheme);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CadranPreviewViewModel.SelectedTheme)) AppliquerTheme(vm.SelectedTheme);
        };
    }

    /// <summary>La galerie suit le thème choisi, comme MainWindow.ApplyThemeBrushes : les cadrans lisent ces tokens
    /// en DynamicResource en remontant jusqu'aux ressources de la fenêtre.</summary>
    private void AppliquerTheme(ChronosTheme? theme)
    {
        foreach (var kv in (theme ?? ThemeCatalog.Default).BrushTokens()) Resources[kv.Key] = kv.Value;
    }
}
