using Chronos.Placement;

namespace Chronos.Services;

/// <summary>Mode d'affichage du cadran. <see cref="Normal"/> (défaut) = épuré, 2 anneaux (hebdo + timeline
/// 24 h colorée par l'usage 5 h, sous-tirets horaires). <see cref="Etendu"/> = 3 anneaux (hebdo, 5 h, timeline).</summary>
public enum CadranDisplayMode { Normal, Etendu }

/// <summary>Style visuel du cadran (refonte visuelle). <see cref="Arcs"/> (défaut) = les anneaux
/// concentriques historiques (avec leur sous-mode Normal/Étendu). Les quatre autres sont les pistes
/// issues de l'idéation llm-council : <see cref="Braises"/> (pastilles discrètes), <see cref="Fusible"/>
/// (mèche horizontale), <see cref="Maree"/> (colonnes inondées), <see cref="Volets"/> (afficheur Solari).
/// Encodage commun : temps = géométrie, quota = luminance ; estimé = grain.</summary>
public enum CadranStyle { Arcs, Braises, Fusible, Maree, Volets }

/// <summary>Style visuel du widget de sessions (refonte visuelle). <see cref="Pastilles"/> (défaut) =
/// la liste historique. Les autres sont les pistes issues de l'idéation llm-council :
/// <see cref="Marge"/> (liste à liseré), <see cref="Jetons"/> (jetons qui lèvent la main),
/// <see cref="Sonar"/> (ping), <see cref="Facade"/> (façade nocturne), <see cref="Etagere"/>
/// (étagère qui bascule), <see cref="Annonciateur"/> (voyants + compteur), <see cref="Veilleurs"/> (regards).
/// Loi commune : mouvement réservé à l'attente ; « à toi » (respire) vs « tour fini » (fixe) ; déduit = fantôme.</summary>
public enum SessionStyle { Pastilles, Marge, Jetons, Sonar, Facade, Etagere, Annonciateur, Veilleurs }

/// <summary>
/// Schéma persisté de settings.json (FEN-07). Décision verrouillée : le COIN + le nom du
/// moniteur (<see cref="MonitorDeviceName"/>) sont la VÉRITÉ pour restaurer la position ;
/// <see cref="X"/>/<see cref="Y"/> sont purement indicatifs (diagnostic, non fiables seuls car
/// dépendants du DPI/agencement des écrans). Record NEUTRE (aucun type WPF) : la seule référence
/// externe est <see cref="Chronos.Placement.OverlayCorner"/>, un enum neutre du même assembly.
///
/// <para>Phase 16 (DEL-05/DEL-06) : les six champs de plafonds ont été retirés. Un settings.json
/// antérieur qui les porte encore s'ouvre sans erreur — System.Text.Json ignore les membres non
/// mappés (JsonUnmappedMemberHandling.Skip) — et ils disparaissent du fichier au premier Save().
/// Aucune migration active n'est nécessaire ; la preuve est le test de non-régression
/// SettingsServiceTests + la fixture TestData/settings-legacy-plafonds.json.</para>
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

    /// <summary>Mode d'affichage du cadran (défaut <see cref="CadranDisplayMode.Normal"/> = épuré, 2 anneaux).
    /// Sérialisé en TEXTE (JsonStringEnumConverter) ; absent d'un ancien settings.json → défaut Normal.</summary>
    public CadranDisplayMode CadranMode { get; init; } = CadranDisplayMode.Normal;

    /// <summary>Style visuel du cadran (défaut <see cref="CadranStyle.Arcs"/> = comportement historique).
    /// Sérialisé en TEXTE ; absent d'un ancien settings.json → défaut Arcs (aucune régression).</summary>
    public CadranStyle CadranStyle { get; init; } = CadranStyle.Arcs;

    /// <summary>Style visuel du widget de sessions (défaut <see cref="SessionStyle.Pastilles"/> = historique).
    /// Sérialisé en TEXTE ; absent d'un ancien settings.json → défaut Pastilles (aucune régression).</summary>
    public SessionStyle SessionStyle { get; init; } = SessionStyle.Pastilles;

    /// <summary>Styles « en rangée » (Sonar / Jetons / Veilleurs) disposés en COLONNE plutôt qu'en rangée
    /// horizontale (défaut false = horizontal). N'a d'effet que sur ces trois styles.</summary>
    public bool VerticalLayout { get; init; }
}
