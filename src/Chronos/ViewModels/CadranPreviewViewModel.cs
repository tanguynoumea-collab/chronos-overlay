using System;
using System.Collections.ObjectModel;
using Chronos.Text;
using Chronos.Theming;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>
/// VM de la galerie de prévisualisation des cadrans (prototype, lancé via « --cadrans »). Expose deux
/// <see cref="WindowGaugeViewModel"/> (5 h / 7 j) pilotés par des curseurs (temps %, quota %, estimé) et
/// un thème, pour juger les 4 pistes de cadran « au coup d'œil » avec des données réalistes — sans
/// dépendre du pipeline temps réel ni de la moindre source Claude. Aucune écriture disque.
/// </summary>
public sealed partial class CadranPreviewViewModel : ObservableObject
{
    public WindowGaugeViewModel FiveHour { get; } = new(TimeSpan.FromHours(5));
    public WindowGaugeViewModel SevenDay { get; } = new(TimeSpan.FromDays(7));

    public ObservableCollection<ChronosTheme> Themes { get; } = new(ThemeCatalog.All);

    [ObservableProperty] private ChronosTheme _selectedTheme = ThemeCatalog.Default;

    [ObservableProperty] private double _fiveTimePct = 62;
    [ObservableProperty] private double _fiveQuotaPct = 48;
    [ObservableProperty] private bool _fiveEstimated;

    [ObservableProperty] private double _sevenTimePct = 40;
    [ObservableProperty] private double _sevenQuotaPct = 71;
    [ObservableProperty] private bool _sevenEstimated = true;

    // Bascule d'affichage % ↔ temps (miroir de MainViewModel), pour prévisualiser le clic-centre.
    [ObservableProperty] private bool _showCountdown;
    public bool ShowPercent => !ShowCountdown;
    partial void OnShowCountdownChanged(bool value) => OnPropertyChanged(nameof(ShowPercent));

    public CadranPreviewViewModel() => Apply();

    partial void OnSelectedThemeChanged(ChronosTheme value) => Apply();
    partial void OnFiveTimePctChanged(double value) => Apply();
    partial void OnFiveQuotaPctChanged(double value) => Apply();
    partial void OnFiveEstimatedChanged(bool value) => Apply();
    partial void OnSevenTimePctChanged(double value) => Apply();
    partial void OnSevenQuotaPctChanged(double value) => Apply();
    partial void OnSevenEstimatedChanged(bool value) => Apply();

    private void Apply()
    {
        var theme = SelectedTheme ?? ThemeCatalog.Default;
        Push(FiveHour, FiveTimePct, FiveQuotaPct, FiveEstimated, theme, TimeSpan.FromHours(5));
        Push(SevenDay, SevenTimePct, SevenQuotaPct, SevenEstimated, theme, TimeSpan.FromDays(7));
    }

    // Pousse un jeu de valeurs d'échantillon dans une jauge en réutilisant ses propriétés réelles :
    // SetTheme d'abord (fixe la rampe), puis Utilization (recalcule ValueBrush via OnUtilizationChanged).
    private static void Push(WindowGaugeViewModel g, double timePct, double quotaPct, bool estimated,
                             ChronosTheme theme, TimeSpan windowLength)
    {
        g.SetTheme(theme);
        double time = Math.Clamp(timePct / 100.0, 0.0, 1.0);
        double? quota = Math.Clamp(quotaPct / 100.0, 0.0, 1.0);

        g.FractionRemaining = time;
        g.FractionElapsed = 1.0 - time;
        g.Utilization = quota;                                   // déclenche ValueBrush = theme.ArcBrush(quota)
        g.IsEstimated = estimated;
        g.Exhausted = quota >= 1.0;
        g.CountdownText = CountdownFormatter.Format(TimeSpan.FromTicks((long)(windowLength.Ticks * time)));
        g.UtilizationText = PercentFormatter.Format(quota, estimated);
    }
}
