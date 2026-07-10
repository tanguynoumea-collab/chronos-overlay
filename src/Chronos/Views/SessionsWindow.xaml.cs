using System.Windows;
using System.Windows.Input;
using Chronos.ViewModels;

namespace Chronos.Views;

/// <summary>
/// Panneau flottant CHROMELESS (always-on-top, sans vol de focus) : pastilles des sessions Claude Code.
/// Glisser n'importe où = déplacer ; clic droit sur une pastille = Archiver. Position persistée par
/// <see cref="Chronos.Services.SessionsController"/>.
/// </summary>
public partial class SessionsWindow : Window
{
    public SessionsWindow(SessionsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
