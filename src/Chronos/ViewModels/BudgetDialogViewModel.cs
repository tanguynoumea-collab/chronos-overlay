using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chronos.ViewModels;

/// <summary>
/// VM du dialogue MINIMAL de calibration manuelle des plafonds (CAL-01). Deux champs texte
/// (plafond 5 h, plafond hebdo) pré-remplis depuis les valeurs courantes (null → chaîne vide).
/// Le VM reste neutre (aucun type WPF) : il signale la fermeture via <see cref="CloseRequested"/>
/// (true = validé), que le code-behind traduit en DialogResult. Le parsing texte→long? est pur
/// (chaîne vide ou valeur ≤ 0 → null) pour que le prompt n'ait qu'à lire <see cref="ParsedFiveHour"/>.
/// </summary>
public sealed partial class BudgetDialogViewModel : ObservableObject
{
    /// <summary>Texte saisi du plafond 5 h (lié TwoWay au TextBox). Vide = pas de plafond.</summary>
    [ObservableProperty] private string _fiveHourText;

    /// <summary>Texte saisi du plafond hebdo (lié TwoWay au TextBox). Vide = pas de plafond.</summary>
    [ObservableProperty] private string _weeklyText;

    /// <summary>Émis à la fermeture : true = validé (plafonds retenus), false = annulé.</summary>
    public event Action<bool>? CloseRequested;

    public BudgetDialogViewModel(long? five, long? weekly)
    {
        _fiveHourText = five?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _weeklyText = weekly?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>Plafond 5 h parsé (null si vide ou invalide).</summary>
    public long? ParsedFiveHour => Parse(FiveHourText);

    /// <summary>Plafond hebdo parsé (null si vide ou invalide).</summary>
    public long? ParsedWeekly => Parse(WeeklyText);

    /// <summary>
    /// Parseur pur : chaîne vide/espaces → null ; sinon retire espaces et séparateurs de milliers
    /// puis <c>long.TryParse</c> (invariant) → valeur si &gt; 0, sinon null.
    /// </summary>
    private static long? Parse(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte))
            return null;

        // Retirer espaces (y compris insécables) et séparateurs de milliers courants.
        var nettoye = texte
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(",", string.Empty)
            .Replace(".", string.Empty);

        return long.TryParse(nettoye, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valeur) && valeur > 0
            ? valeur
            : null;
    }

    /// <summary>« Valider » : retient les plafonds saisis, fermeture acceptée.</summary>
    [RelayCommand]
    private void Validate() => CloseRequested?.Invoke(true);

    /// <summary>« Annuler » : aucun changement, fermeture refusée.</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
