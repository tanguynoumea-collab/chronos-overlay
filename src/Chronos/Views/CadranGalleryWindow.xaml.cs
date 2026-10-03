using System.Windows;
using Chronos.Theming;
using Chronos.ViewModels;

namespace Chronos.Views;

/// <summary>
/// Galerie de revue visuelle des HUIT variantes de cadran (lancée via « --cadrans ») : Anneaux, Braises, puis Fusible,
/// Marée et Volets dans leurs deux orientations, chacune à l'échelle 1 dans son empreinte. DataContext =
/// <see cref="CadranPreviewViewModel"/> (données d'échantillon pilotables, anneau du jour d'Anneaux compris). N'est PAS
/// branchée sur le pipeline temps réel. Les tokens de taille viennent de DesignTokens.xaml (fusionné dans la fenêtre) ;
/// la recopie des BrushTokens() ci-dessous écrase les replis statiques des pinceaux par ceux du thème choisi.
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
