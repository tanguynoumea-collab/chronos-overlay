using Chronos.Placement;

namespace Chronos.Services;

/// <summary>
/// Schéma persisté de settings.json (FEN-07). Décision verrouillée : le COIN + le nom du
/// moniteur (<see cref="MonitorDeviceName"/>) sont la VÉRITÉ pour restaurer la position ;
/// <see cref="X"/>/<see cref="Y"/> sont purement indicatifs (diagnostic, non fiables seuls car
/// dépendants du DPI/agencement des écrans). Record NEUTRE (aucun type WPF) : la seule référence
/// externe est <see cref="Chronos.Placement.OverlayCorner"/>, un enum neutre du même assembly.
/// </summary>
public sealed record ChronosSettings
{
    /// <summary>Coin d'accroche persisté (vérité de placement). Défaut : haut-droite.</summary>
    public OverlayCorner Corner { get; init; } = OverlayCorner.TopRight;

    /// <summary>Nom device du moniteur (\\.\DISPLAY1). Repli primaire si absent.</summary>
    public string? MonitorDeviceName { get; init; }

    /// <summary>Abscisse DIU indicative (diagnostic ; non fiable seule).</summary>
    public double? X { get; init; }

    /// <summary>Ordonnée DIU indicative (diagnostic ; non fiable seule).</summary>
    public double? Y { get; init; }

    /// <summary>Mode « arrière-plan » (topmost désactivé).</summary>
    public bool Background { get; init; }

    /// <summary>
    /// Intervalle de rafraîchissement en secondes. Persisté dans le schéma et appliqué au
    /// démarrage (câblage en 06-03), mais SANS UI dédiée (décision verrouillée). Défaut : 60.
    /// </summary>
    public double RefreshIntervalSeconds { get; init; } = 60;

    /// <summary>Ancre du recalibrage hebdomadaire best-effort (ROB-03). null = pas d'ancre.</summary>
    public DateTimeOffset? WeeklyAnchor { get; init; }

    /// <summary>Plafond de tokens de la fenêtre 5 h (calibrable, Phase 9). null = pas de plafond → utilization estimée null.</summary>
    public long? FiveHourTokenBudget { get; init; }

    /// <summary>Plafond de tokens de la fenêtre hebdo (calibrable, Phase 9). null = pas de plafond → utilization estimée null.</summary>
    public long? WeeklyTokenBudget { get; init; }

    /// <summary>Provenance du plafond 5 h (None/Manual/Auto). Défaut None : la calibration auto peut écrire dessus.</summary>
    public BudgetSource FiveHourBudgetSource { get; init; } = BudgetSource.None;

    /// <summary>Horodatage de la dernière calibration du plafond 5 h. null = jamais calibré.</summary>
    public DateTimeOffset? FiveHourBudgetCalibratedAt { get; init; }

    /// <summary>Provenance du plafond hebdo (None/Manual/Auto). Défaut None : la calibration auto peut écrire dessus.</summary>
    public BudgetSource WeeklyBudgetSource { get; init; } = BudgetSource.None;

    /// <summary>Horodatage de la dernière calibration du plafond hebdo. null = jamais calibré.</summary>
    public DateTimeOffset? WeeklyBudgetCalibratedAt { get; init; }

    /// <summary>Active la source EXACTE OAuth (INT-03). Défaut TRUE : vrais chiffres dès l'installation.
    /// false → comportement v1.1 strict, AUCUN accès au token (le portillon gated court-circuite).</summary>
    public bool OAuthUsageEnabled { get; init; } = true;

    /// <summary>Commande statusLine préexistante de l'utilisateur, mémorisée lors de l'installation du
    /// pont Chronos pour le chaînage non destructif et la restauration à la désinstallation. null = aucune.</summary>
    public string? InnerStatusLineCommand { get; init; }

    /// <summary>L'utilisateur a-t-il déjà répondu à la proposition d'activer la source exacte (pont
    /// statusLine) ? true → ne plus reproposer au démarrage (qu'il ait accepté ou refusé).</summary>
    public bool StatusLinePromptDismissed { get; init; }

    /// <summary>Clé du thème visuel sélectionné (« minuit » par défaut). Voir Chronos.Theming.ThemeCatalog.</summary>
    public string ThemeKey { get; init; } = "minuit";

    /// <summary>Widget de sessions Claude Code activé (hooks installés + panneau affiché).</summary>
    public bool SessionsWidgetEnabled { get; init; }

    /// <summary>Position persistée du panneau de sessions (DIU). null = position par défaut.</summary>
    public double? SessionsX { get; init; }
    public double? SessionsY { get; init; }
}
