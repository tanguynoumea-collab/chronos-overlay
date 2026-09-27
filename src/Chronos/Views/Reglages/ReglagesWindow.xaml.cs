using System.Windows;
using System.Windows.Media;
using Chronos.ViewModels;

namespace Chronos.Views.Reglages;

/// <summary>RED — squelette de la fenêtre de réglages v2.</summary>
public partial class ReglagesWindow : Window
{
    public ReglagesWindow(MainViewModel vm, Visual? cadranReel = null)
    {
        InitializeComponent();
        DataContext = vm;
    }

    internal void EnregistrerGeometrie() { }
}
