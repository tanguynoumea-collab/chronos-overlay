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
}
