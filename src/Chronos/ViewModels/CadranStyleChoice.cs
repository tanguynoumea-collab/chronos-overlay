using Chronos.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>Entrée du sélecteur de STYLE de cadran (settings) : le style + son libellé + l'état
/// sélectionné (pour la surbrillance). Calqué sur <see cref="ThemeChoice"/>.</summary>
public sealed partial class CadranStyleChoice : ObservableObject
{
    public CadranStyle Style { get; }
    public string Name { get; }
    [ObservableProperty] private bool _isSelected;

    public CadranStyleChoice(CadranStyle style, string name, bool isSelected)
    {
        Style = style;
        Name = name;
        IsSelected = isSelected;
    }
}
