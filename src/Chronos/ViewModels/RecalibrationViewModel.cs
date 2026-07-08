using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chronos.ViewModels;

/// <summary>
/// VM du dialogue MINIMAL de recalibrage hebdo (ROB-03). L'utilisateur pointe la date d'un reset
/// hebdo connu (le reset « 7 jours » dérive ~72 h) via un DatePicker, OU cale sur « maintenant ».
/// Le VM reste neutre (aucun type WPF) : il signale la fermeture via <see cref="CloseRequested"/>
/// (true = accepté), que le code-behind traduit en DialogResult.
///
/// <see cref="SelectedDate"/> est un <see cref="DateTime"/> pour se lier directement au DatePicker
/// (dont SelectedDate est DateTime?) ; la conversion en <see cref="DateTimeOffset"/> a lieu dans le prompt.
/// </summary>
public sealed partial class RecalibrationViewModel : ObservableObject
{
    /// <summary>Date d'ancrage sélectionnée (liée au DatePicker). Défaut = date de <c>current</c>.</summary>
    [ObservableProperty] private DateTime _selectedDate;

    /// <summary>Émis à la fermeture : true = validé (ancre retenue), false = annulé.</summary>
    public event Action<bool>? CloseRequested;

    public RecalibrationViewModel(DateTimeOffset current) => _selectedDate = current.LocalDateTime;

    /// <summary>« Caler sur maintenant » : ancre = maintenant, puis fermeture acceptée.</summary>
    [RelayCommand]
    private void Now()
    {
        SelectedDate = DateTime.Now;
        CloseRequested?.Invoke(true);
    }

    /// <summary>« Valider » : retient la date sélectionnée, fermeture acceptée.</summary>
    [RelayCommand]
    private void Validate() => CloseRequested?.Invoke(true);

    /// <summary>« Annuler » : aucune ancre retenue, fermeture refusée.</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
