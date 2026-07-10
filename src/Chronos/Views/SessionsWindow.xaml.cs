using System.Windows;
using System.Windows.Input;
using Chronos.ViewModels;

namespace Chronos.Views;

/// <summary>
/// Panneau flottant (always-on-top, sans vol de focus) listant les sessions Claude Code et leur état.
/// Déplaçable ; sa position est persistée par le <see cref="Chronos.Services.SessionsController"/>.
/// </summary>
public partial class SessionsWindow : Window
{
    public SessionsWindow(SessionsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Header_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
}
