using Chronos.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>Entrée du sélecteur de STYLE du widget de sessions (settings) : le style + son libellé +
/// l'état sélectionné (pour la surbrillance). Calqué sur <see cref="CadranStyleChoice"/>.</summary>
public sealed partial class SessionStyleChoice : ObservableObject
{
    public SessionStyle Style { get; }
    public string Name { get; }
    [ObservableProperty] private bool _isSelected;

    public SessionStyleChoice(SessionStyle style, string name, bool isSelected)
    {
        Style = style;
        Name = name;
        IsSelected = isSelected;
    }
}
