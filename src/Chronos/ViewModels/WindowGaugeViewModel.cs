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

    // HDR-03 — l'état déclaré par le SERVEUR pour CETTE fenêtre, en texte FR prêt à afficher.
    // Posées et testées ici pour que la phase 20 (EXA-03, distinction visuelle frais / daté /
    // indisponible) n'ait plus qu'à les binder : AUCUNE géométrie de cadran n'en dépend aujourd'hui.
    [ObservableProperty] private string _texteStatutServeur = "";   // HDR-03 — état RAPPORTÉ par le serveur
    [ObservableProperty] private bool _hasStatutServeur;            // pilote la visibilité (motif HasTokens)

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

        // HDR-03 — l'état déclaré par le SERVEUR, pas un seuil déduit d'un pourcentage. Absent → rien
        // d'affiché (jamais « autorisé » par défaut : l'absence d'information n'est pas une bonne
        // nouvelle). La géométrie du cadran n'en dépend pas : la distinction visuelle est EXA-03, phase 20.
        TexteStatutServeur = LibelleStatut(s.StatutServeur);
        HasStatutServeur = s.StatutServeur is not null;

        // NET-02 : surfacer les tokens estimés (matière première) UNIQUEMENT en source Estimated avec tokens>0.
        // Jamais en Exact (les pourcentages exacts suffisent) ni sans donnée — honnêteté préservée.
        HasTokens = s.Reliability == SourceReliability.Estimated && s.EstimatedTokens is > 0;
        TokensText = HasTokens ? TokenFormatter.Format(s.EstimatedTokens!.Value) : "";
    }

    /// <summary>
    /// Vocabulaire FR UNIQUE du statut serveur pour toute la couche présentation : <c>MainViewModel</c>
    /// réutilise ces textes déjà calculés plutôt que de refaire un second mapping — deux mappings
    /// divergeraient le jour où le vocabulaire du serveur bougera.
    ///
    /// <c>null</c> rend la chaîne VIDE, et surtout PAS « autorisé » : l'en-tête absent signifie que le
    /// serveur n'a rien dit, ce qui n'est pas une bonne nouvelle — seulement une absence de nouvelle.
    /// </summary>
    private static string LibelleStatut(StatutServeur? s) => s switch
    {
        StatutServeur.Autorise => "AUTORISÉ",
        StatutServeur.AutoriseAvertissement => "AUTORISÉ (avertissement)",
        StatutServeur.Rejete => "REJETÉ",
        StatutServeur.NonReconnu => "statut non reconnu",
        _ => "",
    };

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
