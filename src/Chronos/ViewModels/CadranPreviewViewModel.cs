using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Chronos.Rendering;
using Chronos.Text;
using Chronos.Theming;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>
/// VM de la galerie de prévisualisation des cadrans (prototype, lancé via « --cadrans »). Expose deux
/// <see cref="WindowGaugeViewModel"/> (5 h / 7 j) pilotés par des curseurs (temps %, quota %, estimé) et
/// un thème, pour juger les cadrans (huit variantes, Anneaux compris) « au coup d'œil » avec des données réalistes — sans
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

    // Propriétés lues par CadranArcsView (miroir de MainViewModel) : la galerie montre Anneaux en mode normal (2 anneaux).
    public bool IsModeEtendu => false;
    public bool IsModeNormal => true;
    [ObservableProperty] private double _dayFraction;
    [ObservableProperty] private IReadOnlyList<double> _dayResetAngles = Array.Empty<double>();
    [ObservableProperty] private IReadOnlyList<double> _daySubTickAngles = Array.Empty<double>();

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

        // Anneau du jour d'Anneaux : données d'échantillon DÉTERMINISTES (« maintenant » fixe, reset 5 h déduit du curseur
        // de temps restant) — jamais une source Claude, jamais l'horloge réelle.
        var maintenant = new DateTimeOffset(2026, 10, 3, 14, 0, 0, TimeSpan.Zero);
        var reset = maintenant + TimeSpan.FromHours(5) * (Math.Clamp(FiveTimePct, 0, 100) / 100.0);
        DayFraction = DayTimeline.Fraction(maintenant);
        DayResetAngles = DayTimeline.ResetAngles(maintenant, reset);
        DaySubTickAngles = DayTimeline.SubTickAngles(maintenant, reset);
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
        g.HasTime = true;   // la galerie a toujours des données (pas d'état « en attente »)
        g.Utilization = quota;                                   // déclenche ValueBrush = theme.ArcBrush(quota)
        g.EstPlancher = estimated;
        g.Exhausted = quota >= 1.0;
        g.CountdownText = CountdownFormatter.Format(TimeSpan.FromTicks((long)(windowLength.Ticks * time)));
        g.UtilizationText = PercentFormatter.Format(quota, estimated);
    }
}
