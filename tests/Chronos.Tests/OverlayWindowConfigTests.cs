using System.Windows;
using Chronos.Services;
using Chronos.ViewModels;
using Chronos.Views;
using Xunit;

namespace Chronos.Tests;

/// <summary>Vérifie que la fenêtre overlay porte bien les propriétés FEN-01 (contexte STA requis).</summary>
public class OverlayWindowConfigTests
{
    [WpfFact]
    public void Fenetre_expose_les_proprietes_overlay()
    {
        // La fenêtre WPF doit être construite sur un thread STA — fourni par [WpfFact].
        // Le ctor exige désormais un TopmostGuard (ROB-04) ; non attaché ici, on teste FEN-01.
        var fenetre = new MainWindow(new MainViewModel(), new TopmostGuard());

        // Chaque propriété FEN-01 : l'oubli d'une seule casse ou dénature l'overlay.
        Assert.Equal(WindowStyle.None, fenetre.WindowStyle);
        Assert.True(fenetre.AllowsTransparency);
        Assert.True(fenetre.Topmost);
        Assert.False(fenetre.ShowInTaskbar);
        Assert.False(fenetre.ShowActivated);
        Assert.Equal(ResizeMode.NoResize, fenetre.ResizeMode);
    }
}
