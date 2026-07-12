using System.Windows.Media;
using Chronos.Models;
using Chronos.Text;
using Chronos.Theming;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>
/// Sous-VM d'UNE fenêtre d'usage (5 h ou hebdo). Mémorise le dernier <see cref="WindowState"/> immuable
/// et recalcule, à chaque interpolation, la fraction d'arc restante + le compte à rebours formaté FR.
/// L'interpolation est PURE (RAF-03) : elle ne lit que l'état mémorisé + l'instant fourni, jamais le disque.
/// Le XAML Phase 5 bindera un RingArc sur FractionRemaining/Utilization/Reliability.
/// </summary>
public sealed partial class WindowGaugeViewModel : ObservableObject
{
    private readonly TimeSpan _windowLength;
    private WindowState _state; // dernier snapshot de cette fenêtre (immuable)

    [ObservableProperty] private double _fractionRemaining;                    // 0..1 → longueur d'arc restante
    [ObservableProperty] private double _fractionElapsed;                       // 0..1 → longueur d'arc ÉCOULÉE (VIS-01)
    [ObservableProperty] private bool _hasTime;                                 // vrai SSI reset connu → les nouveaux styles
                                                                                // (temps = géométrie) ont de quoi dessiner ;
                                                                                // faux (chargement/pas de reset) → état « en attente »
    [ObservableProperty] private double? _utilization;                          // 0..1 ou null → couleur (Phase 5)
    [ObservableProperty] private string _utilizationText = "";                  // « 80 % » / « ~80 % » / «» (VIS-05)
    [ObservableProperty] private bool _hasUtilizationText;                      // vrai SSI utilization connue (pilote le séparateur « · »)
    [ObservableProperty] private string _countdownText = "—";
    [ObservableProperty] private bool _exhausted;
    [ObservableProperty] private SourceReliability _reliability = SourceReliability.Unavailable;
    [ObservableProperty] private bool _isEstimated;                             // provenance → marquage « estimé » (DAT-08 Phase 5)
    [ObservableProperty] private string _tokensText = "";                       // « ≈ N M/k tokens » ; vide si masqué (NET-02)
    [ObservableProperty] private bool _hasTokens;                               // vrai SSI Estimated + tokens>0 (pilote la visibilité)

    // Couleur de l'arc valeur calculée selon le THÈME courant (remplace le converter statique → switch live).
    [ObservableProperty] private Brush? _valueBrush;
    private ChronosTheme _theme = ThemeCatalog.Default;

    /// <summary>Applique un thème : recalcule la couleur de l'arc valeur pour l'utilization courante.</summary>
    public void SetTheme(ChronosTheme theme)
    {
        _theme = theme;
        ValueBrush = _theme.ArcBrush(Utilization);
    }

    // Recalcule l'arc à chaque changement d'utilization (rampe du thème courant).
    partial void OnUtilizationChanged(double? value) => ValueBrush = _theme.ArcBrush(value);

    public WindowGaugeViewModel(TimeSpan windowLength)
    {
        _windowLength = windowLength;
        _state = WindowState.Unavailable(default);
        ValueBrush = _theme.ArcBrush(null); // neutre au départ (aucune donnée)
    }

    /// <summary>Applique un nouvel état de fenêtre (thread UI). Met à jour provenance/utilization/épuisement.</summary>
    public void Apply(WindowState s)
    {
        _state = s;
        Utilization = s.Utilization;
        Exhausted = s.Exhausted;
        Reliability = s.Reliability;
        IsEstimated = s.Reliability == SourceReliability.Estimated; // pré-câble DAT-08 (Phase 5)

        // VIS-05 : % honnête au centre du cadran. « ~ » si estimé, «» si utilization null (jamais de plafond inventé).
        // HasUtilizationText pilote la visibilité du séparateur « · » côté XAML (même pattern que HasTokens).
        UtilizationText = PercentFormatter.Format(s.Utilization, IsEstimated);
        HasUtilizationText = s.Utilization is not null;

        // NET-02 : surfacer les tokens estimés (matière première) UNIQUEMENT en source Estimated avec tokens>0.
        // Jamais en Exact (les pourcentages exacts suffisent) ni sans donnée — honnêteté préservée.
        HasTokens = s.Reliability == SourceReliability.Estimated && s.EstimatedTokens is > 0;
        TokensText = HasTokens ? TokenFormatter.Format(s.EstimatedTokens!.Value) : "";
    }

    /// <summary>PUR, aucun I/O (RAF-03) : recalcule fraction d'arc + compte à rebours à l'instant <paramref name="now"/>.</summary>
    public void Interpolate(DateTimeOffset now)
    {
        var remaining = WindowState.FractionRemaining(_state.ResetsAt, now, _windowLength);
        FractionRemaining = remaining ?? 0.0;
        HasTime = _state.ResetsAt is not null;   // reset connu → géométrie fiable pour les nouveaux styles
        // VIS-01 : inversion du remplissage — l'arc est VIDE en début de fenêtre, PLEIN au reset.
        // Reset INCONNU (remaining null) → arc VIDE (0), jamais plein : on n'affiche pas un plein trompeur
        // quand on ne connaît pas le temps (countdown « — »).
        FractionElapsed = remaining is { } rem ? System.Math.Clamp(1.0 - rem, 0.0, 1.0) : 0.0;
        CountdownText = _state.ResetsAt is { } r
            ? CountdownFormatter.Format(r - now)
            : "—";
    }
}
