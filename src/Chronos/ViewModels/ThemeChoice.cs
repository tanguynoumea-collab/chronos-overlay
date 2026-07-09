using Chronos.Theming;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>Entrée de la grille de thèmes (settings) : le thème + son état sélectionné (pour la surbrillance).</summary>
public sealed partial class ThemeChoice : ObservableObject
{
    public ChronosTheme Theme { get; }
    [ObservableProperty] private bool _isSelected;

    public ThemeChoice(ChronosTheme theme, bool isSelected)
    {
        Theme = theme;
        IsSelected = isSelected;
    }

    public string Name => Theme.Name;
}
