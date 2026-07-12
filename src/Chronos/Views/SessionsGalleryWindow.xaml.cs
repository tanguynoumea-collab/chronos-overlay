using System.Windows;
using Chronos.Theming;
using Chronos.ViewModels;

namespace Chronos.Views;

/// <summary>
/// Galerie de prévisualisation des 8 styles du widget de sessions (prototype, lancée via « --sessions »).
/// DataContext = <see cref="SessionsPreviewViewModel"/> (données d'échantillon). N'est PAS branchée sur le
/// moniteur réel : sert uniquement à juger les concepts au coup d'œil.
/// </summary>
public partial class SessionsGalleryWindow : Window
{
    public SessionsGalleryWindow()
    {
        InitializeComponent();
        DataContext = new SessionsPreviewViewModel();
        // Pinceaux « sessions » du thème par défaut → résout les DynamicResource des templates.
        foreach (var kv in ThemeCatalog.Default.SessionBrushTokens())
            Resources[kv.Key] = kv.Value;
    }
}
