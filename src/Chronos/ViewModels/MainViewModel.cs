using System.Collections.ObjectModel;
using System.Windows.Threading;
using Chronos.Models;
using Chronos.Models.Historique.Tokens;
using Chronos.Rendering;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;
using Chronos.Text;
using Chronos.Theming;
using Chronos.ViewModels.Historique;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orientation = System.Windows.Controls.Orientation;

namespace Chronos.ViewModels;

/// <summary>
/// ViewModel racine (temps réel). S'abonne à l'horloge DONNÉES (<see cref="RefreshOrchestrator.SnapshotChanged"/>,
/// émis sur le thread pool) et franchit la frontière de thread EN UN SEUL POINT via <see cref="IUiDispatcher.Post"/>
/// (RAF-04). L'affichage vit grâce à <see cref="Interpolate"/> (PUR, aucun I/O — RAF-03), piloté par un
/// DispatcherTimer 1 s créé côté UI (<see cref="StartClock"/>) — jamais dans le ctor (Pitfall 4 : tests en [Fact] simple).
///
/// 06-04 : expose les commandes du menu contextuel (SEUL point d'accès/sortie, FEN-06) —
/// arrière-plan (FEN-05), lancer au démarrage (DEP-02), quitter.
/// Le reset hebdo affiché est toujours celui de la source : aucun reset synthétique n'est fabriqué ici.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IUiDispatcher _ui;
    private readonly IClock _clock;
    private readonly IWindowController _controller;
    private readonly IAutostartService _autostart;
    private readonly RefreshOrchestrator _orchestrator;
    private readonly SettingsService _settingsService;
    private readonly DiagnosticService _diagnostic;
    private readonly IOAuthLogin _oauthLogin;
    private readonly ISessionsController _sessions;
    private readonly IAuthStatus _authStatus;
    private readonly IEtatServeur? _etatServeur;
    private readonly IEtatJournal? _journal;       // JRN-04 : ce que le journal des relevés dit de lui-même (optionnel)
    private readonly IEtatReconstruction? _reconstruction;   // TOK-02 : ce que la reconstruction des agrégats dit d'elle-même (optionnel, bandeau F2 en phase 34)
    private readonly IOubliDernierReleve? _oubliReleve;   // DS2-03 / D-03 : efface le dernier relevé exact à la déconnexion (optionnel)
    private readonly IOuvreurHistorique? _ouvreurHistorique;   // ACC-02 : ouvre / ramène la fenêtre Historique (optionnel)
    private readonly DateTimeOffset _demarrage;    // JRN-04 / D-32-21 : référence basse de « muet » (instant de construction du VM)

    private ChronosSettings _settings;   // état persisté courant (coin/mode/ancre)
    private UsageSnapshot? _last;         // dernier snapshot appliqué (mémorisé pour le reset 5 h local)

    public WindowGaugeViewModel FiveHour { get; } = new(TimeSpan.FromHours(5));
    public WindowGaugeViewModel SevenDay { get; } = new(TimeSpan.FromDays(7));

    [ObservableProperty] private bool _dataUnavailable;

    /// <summary>EXA-03 — au moins une des deux fenêtres porte un chiffre DATÉ (relevé au-delà de la
    /// limite d'âge, réhabilité ou dégradé en plancher par la doctrine). Marque INFORMATIVE et ADDITIVE :
    /// elle s'ajoute À CÔTÉ du chiffre, elle ne lui retire ni luminance ni netteté — un EncoreValide est
    /// un chiffre PROUVÉ juste, le voiler serait une régression d'honnêteté.
    /// PORTÉE GLOBALE assumée : l'âge n'est pas une propriété de la fenêtre, c'est une propriété du
    /// PIPELINE (les deux fenêtres viennent de la même chaîne de sources). Règle de composition :
    /// on montre la marque dès que l'UNE des deux est datée — le plus vieux des deux, jamais le plus
    /// jeune. Une synthèse conservatrice n'est pas un mensonge ; une synthèse optimiste en serait un.
    /// Le détail par fenêtre est dans l'infobulle.</summary>
    [ObservableProperty] private bool _afficherReleveDate;

    /// <summary>EXA-06 au cadran — le COUPLE (qui alimente, depuis quand) pour chaque fenêtre, plus la
    /// matière brute de DEL-04 quand elle existe. Le vocabulaire n'est PAS remappé ici : il vient de
    /// Chronos.Text.LibelleSource, un seul point pour tout le projet.
    ///
    /// Recomposée une fois par RAFRAÎCHISSEMENT (≈ 60 s) et non à chaque tick d'interpolation (1 s) :
    /// reconstruire une chaîne chaque seconde pour un texte qui n'est visible qu'au survol serait du
    /// travail permanent pour un affichage occasionnel. Conséquence assumée et écrite plutôt que laissée
    /// à deviner : l'ancienneté affichée peut retarder d'un tick de rafraîchissement.</summary>
    [ObservableProperty] private string _infobulleReleve = "";

    // Anneau 24 h (JOUR-01/02) : fraction du jour local + resets 5 h projetés sur l'axe des 24 h.
    // Recalculés à chaque Interpolate (rafraîchis chaque seconde). La couleur 24 h réutilisera
    // FiveHour.Utilization côté XAML (JOUR-03, plan 02).
    [ObservableProperty] private double _dayFraction;
    [ObservableProperty] private System.Collections.Generic.IReadOnlyList<double> _dayResetAngles = System.Array.Empty<double>();
    // Sous-tirets horaires alignés sur la grille des resets 5 h (subdivisent chaque intervalle de 5 h, mode normal).
    [ObservableProperty] private System.Collections.Generic.IReadOnlyList<double> _daySubTickAngles = System.Array.Empty<double>();
    // Plan 43-09 — anneau JOURNÉE de Braises : 24 angles (tranches de 5 h) et 24 états (heures passées / en cours / futures).
    [ObservableProperty] private System.Collections.Generic.IReadOnlyList<double> _journeeAngles = System.Array.Empty<double>();
    [ObservableProperty] private System.Collections.Generic.IReadOnlyList<Rendering.EtatBraise> _journeeEtats = System.Array.Empty<Rendering.EtatBraise>();

    // État reflété dans les items « à cocher » du menu (FEN-05 / DEP-02).
    [ObservableProperty] private bool _isBackground;
    [ObservableProperty] private bool _isAutostart;

    // État reflété dans l'item « Se connecter à Claude » : un jeton OAuth Chronos est-il présent ?
    [ObservableProperty] private bool _isLoggedIn;

    // TOK-02 — DEUX booléens et non un seul : « déconnecté » est ACTIONNABLE (le serveur a refusé les
    // identifiants, seule une reconnexion répare), « hors ligne » est INFORMATIF (réseau/serveur ;
    // le jeton est peut-être parfaitement bon). Les confondre ferait relancer un login inutile.
    // Deux booléens plutôt qu'un enum bindé : cohérent avec IsStyleArcs / IsModeNormal, zéro converter.
    [ObservableProperty] private bool _afficherPastilleDeconnexion;
    [ObservableProperty] private bool _afficherPastilleHorsLigne;

    /// <summary>EXA-05 — aucun chiffre exact n'a JAMAIS été obtenu ET rien n'est disponible : l'overlay
    /// invite à se connecter plutôt que d'afficher un pourcentage. ACTIONNABLE (ReconnecterCommand).
    /// Distinct de la pastille de déconnexion : celle-ci dit « on a perdu la connexion », celle-là dit
    /// « on n'a jamais rien eu » — deux diagnostics différents pour un même geste de réparation.
    ///
    /// <see cref="AfficherInvitationConnexion"/> n'est JAMAIS posée directement : elle est recomposée
    /// avec les deux autres pastilles par <see cref="MajPastilles"/>, seul point qui voit
    /// simultanément les deux entrées brutes.</summary>
    [ObservableProperty] private bool _afficherInvitationConnexion;

    /// <summary>HDR-06 — interrupteur de la sonde d'en-têtes : le seul qui gouverne une dépense (la sonde
    /// consomme une vraie micro-requête sur le compte).</summary>
    [ObservableProperty] private bool _isSondeEnTetesActivee;

    /// <summary>HDR-03 / HDR-04 — ce que le SERVEUR a déclaré (statut par fenêtre, dépassement de compte),
    /// en une ligne pour les réglages. VIDE quand rien n'est rapporté : jamais « autorisé » par défaut.</summary>
    [ObservableProperty] private string _texteEtatSonde = "";

    /// <summary>Pilote la visibilité de la ligne ci-dessus (motif HasTokens / HasUtilizationText).</summary>
    [ObservableProperty] private bool _afficherEtatSonde;

    /// <summary>JRN-04 — l'âge de la dernière écriture du journal des relevés, en une ligne pour les réglages : mêmes mots
    /// que le diagnostic (D-32-22). VIDE sans journal injecté : jamais un « journal sain » par défaut.</summary>
    [ObservableProperty] private string _texteEtatJournal = "";

    /// <summary>JRN-04 — le journal se tait depuis plus de trois cadences (15 min) alors que Chronos tourne (D-32-21).
    /// Pilote la pastille <c>Alerte</c> de la ligne d'état du journal (carte « Historique d'utilisation » depuis 35-02).</summary>
    [ObservableProperty] private bool _alerteJournal;

    /// <summary>Pilote la visibilité de la ligne d'état du journal (motif AfficherEtatSonde) : masquée sans journal injecté.</summary>
    [ObservableProperty] private bool _afficherEtatJournal;
    partial void OnAfficherEtatJournalChanged(bool value) => OnPropertyChanged(nameof(AfficherCarteHistorique));

    /// <summary>ACC-01 / D-35-08 — le VM SINGLETON de la fenêtre Historique, exposé tel quel : les puces de style de la carte des
    /// réglages bindent SA commande et SES booléens. Même instance, mêmes réglages, même commande que le sélecteur de la fenêtre :
    /// synchronisation par construction, aucun état de style dupliqué ici. <c>null</c> hors DI (tests historiques).</summary>
    public HistoriqueViewModel? Historique { get; }

    /// <summary>Quick 260927-reglages-v2 — l'état PROPRE à la fenêtre de réglages (section du rail, géométrie, panneau Diagnostic).
    /// Les réglages eux-mêmes restent ici, sur le VM partagé avec le cadran : la fenêtre les lie sans intermédiaire.</summary>
    public ReglagesViewModel Reglages { get; }

    /// <summary>ACC-01 / D-35-09 — la carte « Historique d'utilisation » (qui absorbe la ligne d'état du journal) se montre dès
    /// qu'elle a quelque chose à dire : la fenêtre à ouvrir, ou l'état du journal.</summary>
    public bool AfficherCarteHistorique => Historique is not null || AfficherEtatJournal;

    /// <summary>Le bouton « Ouvrir », le sélecteur de style et la mention du double-clic n'ont de sens qu'avec la fenêtre.</summary>
    public bool AfficherOuvrirHistorique => Historique is not null;

    /// <summary>ACC-01 — sous-texte de la carte : « hebdo / 5 h / tokens · journal du &lt;date&gt; ». Date dans le fuseau de la
    /// fenêtre (mêmes mots, <see cref="TextesHistorique.DateLongue"/>) ; ouverture inconnue → le segment est OMIS, jamais inventé.
    /// La « dernière écriture » n'est PAS répétée ici : elle vit dans <see cref="TexteEtatJournal"/>, une seule fois.</summary>
    [ObservableProperty] private string _sousTexteHistorique = "hebdo / 5 h / tokens";

    /// <summary>TOK-02 / D-33-22 — la progression de la reconstruction des agrégats de tokens, en une ligne : « N / M fichiers »,
    /// suivie de la mention de la semaine courante dès qu'elle est sur le disque ; ou « ÉCHEC — cause ». VIDE quand tout est à jour
    /// (incrémental) ou sans état injecté : des ENTIERS formatés, jamais une fraction ni un pourcentage.</summary>
    [ObservableProperty] private string _texteReconstruction = "";

    /// <summary>Pilote la visibilité du texte de reconstruction : vrai pendant la passe initiale et en échec, faux sinon.</summary>
    [ObservableProperty] private bool _afficherReconstruction;

    // État reflété dans l'item « Sessions Claude Code » : le widget de sessions est-il activé ?
    [ObservableProperty] private bool _isSessionsWidgetEnabled;

    // Mode d'affichage du centre : false = pourcentages (défaut), true = temps avant reset.
    // Un clic sur le cadran bascule via ToggleCenterMode(). ShowPercent est l'inverse (pour le binding XAML).
    [ObservableProperty] private bool _showCountdown;
    public bool ShowPercent => !ShowCountdown;
    partial void OnShowCountdownChanged(bool value) => OnPropertyChanged(nameof(ShowPercent));

    /// <summary>Bascule le centre entre pourcentages et temps avant reset (clic sur le cadran, à l'échéance
    /// de l'arbitre — <see cref="ClicCentre"/>).</summary>
    public void ToggleCenterMode() => ShowCountdown = !ShowCountdown;

    // ACC-02 / D-35-06 : l'arbitre PUR décide (simple clic = bascule à l'échéance, double = Historique) ; la minuterie
    // one-shot n'est qu'un réveil. 500 ms = défaut Windows, remplacé par le délai réel lu par la vue (GetDoubleClickTime).
    private ArbitreClicCentre _arbitreClic = new(TimeSpan.FromMilliseconds(500));
    private DispatcherTimer? _minuterieClic;

    /// <summary>Délai de double-clic du système, lu par la VUE (P/Invoke) et transmis ici ; ignoré s'il n'est pas positif.</summary>
    public void DefinirDelaiDoubleClic(TimeSpan delai)
    {
        if (delai > TimeSpan.Zero) _arbitreClic = new ArbitreClicCentre(delai);
    }

    /// <summary>
    /// ACC-02 — clic sur le cadran (toute la silhouette, phase 42), avec le <c>ClickCount</c> de WPF (délai ET rectangle système déjà appliqués).
    /// Un simple clic ARME la bascule, qui n'a lieu qu'à l'échéance du délai de double-clic (≈ 0,5 s, coût assumé) ;
    /// un double-clic la désarme et ouvre l'Historique : jamais deux bascules, jamais une bascule avant l'ouverture
    /// (sauf le cas limite de l'échéance traitée avant le second clic — une bascule au plus, Pitfall 7).
    /// </summary>
    public void ClicCentre(int clickCount)
    {
        switch (_arbitreClic.Clic(clickCount, _clock.UtcNow))
        {
            case ActionClicCentre.OuvrirHistorique:
                _minuterieClic?.Stop();
                _ouvreurHistorique?.Ouvrir();
                break;
            case ActionClicCentre.Rien:
                ArmerMinuterieClic(_arbitreClic.Delai);
                break;
        }
    }

    // Créée ICI, au premier clic, sur le thread UI — jamais dans le ctor (Pitfall 4 : les tests construisent le VM en [Fact]).
    private void ArmerMinuterieClic(TimeSpan intervalle)
    {
        if (_minuterieClic is null)
        {
            _minuterieClic = new DispatcherTimer();
            _minuterieClic.Tick += (_, _) =>
            {
                _minuterieClic.Stop();
                EcheanceClicCentre();
                // Minuterie à la granularité du système, parfois un peu en avance : on réarme pour le reste, un simple
                // clic ne se perd jamais.
                if (_arbitreClic.EnAttente) ArmerMinuterieClic(_arbitreClic.Restant(_clock.UtcNow) + TimeSpan.FromMilliseconds(1));
            };
        }

        _minuterieClic.Stop();
        _minuterieClic.Interval = intervalle;
        _minuterieClic.Start();
    }

    /// <summary>Échéance de la minuterie (ou appel direct des tests, horloge injectée) : bascule si l'arbitre le décide.</summary>
    internal void EcheanceClicCentre()
    {
        if (_arbitreClic.Echeance(_clock.UtcNow) == ActionClicCentre.Basculer) ToggleCenterMode();
    }

    /// <summary>ACC-01 / ACC-02 — ouvre ou ramène la fenêtre Historique (bouton « Ouvrir » de la carte des réglages).
    /// Sans ouvreur injecté (tests), ne fait rien.</summary>
    [RelayCommand]
    private void OuvrirHistorique() => _ouvreurHistorique?.Ouvrir();

    // Mode d'affichage du cadran : false = Normal (défaut, 2 anneaux : hebdo + timeline 24 h), true = Étendu
    // (3 anneaux). Bindé aux Visibility des groupes d'anneaux (MainWindow.xaml). Persisté dans settings.json.
    [ObservableProperty] private bool _isModeEtendu;
    public bool IsModeNormal => !IsModeEtendu;
    partial void OnIsModeEtenduChanged(bool value) => OnPropertyChanged(nameof(IsModeNormal));

    // Style visuel du cadran (refonte visuelle) : Arcs (défaut) + 4 pistes. Persisté dans settings.json.
    // Les 5 booléens IsStyleX pilotent les Visibility des groupes de MainWindow.xaml (un seul visible).
    [ObservableProperty] private CadranStyle _cadranStyle;
    public bool IsStyleArcs    => CadranStyle == CadranStyle.Arcs;
    public bool IsStyleBraises => CadranStyle == CadranStyle.Braises;
    public bool IsStyleFusible => CadranStyle == CadranStyle.Fusible;
    public bool IsStyleMaree   => CadranStyle == CadranStyle.Maree;
    public bool IsStyleVolets  => CadranStyle == CadranStyle.Volets;
    partial void OnCadranStyleChanged(CadranStyle value)
    {
        OnPropertyChanged(nameof(NomStyleCadran));
        OnPropertyChanged(nameof(IsStyleArcs));
        OnPropertyChanged(nameof(IsStyleBraises));
        OnPropertyChanged(nameof(IsStyleFusible));
        OnPropertyChanged(nameof(IsStyleMaree));
        OnPropertyChanged(nameof(IsStyleVolets));
        NotifierEmpreinte();
    }

    // Orientation PAR CADRAN (phase 40, CAD-04) : type WPF côté VM (lié aux DP Orientation des vues), neutre (OrientationCadran)
    // côté réglages. Sans aucun lien avec VerticalLayout (widget de sessions).
    [ObservableProperty] private Orientation _orientationFusible = Orientation.Horizontal;
    [ObservableProperty] private Orientation _orientationMaree = Orientation.Vertical;
    [ObservableProperty] private Orientation _orientationVolets = Orientation.Horizontal;
    partial void OnOrientationFusibleChanged(Orientation value) => NotifierEmpreinte();
    partial void OnOrientationMareeChanged(Orientation value) => NotifierEmpreinte();
    partial void OnOrientationVoletsChanged(Orientation value) => NotifierEmpreinte();

    /// <summary>Vrai pour les trois cadrans rectangulaires (Fusible, Marée, Volets) : seuls à avoir une orientation.</summary>
    public bool EstStyleOrientable => CadranStyle is CadranStyle.Fusible or CadranStyle.Maree or CadranStyle.Volets;

    /// <summary>Orientation du cadran COURANT (Horizontal, sans objet, pour Arcs et Braises).</summary>
    public OrientationCadran OrientationCourante => CadranStyle switch
    {
        CadranStyle.Fusible => Neutre(OrientationFusible),
        CadranStyle.Maree   => Neutre(OrientationMaree),
        CadranStyle.Volets  => Neutre(OrientationVolets),
        _ => OrientationCadran.Horizontal,
    };
    public bool EstOrientationHorizontale => EstStyleOrientable && OrientationCourante == OrientationCadran.Horizontal;
    public bool EstOrientationVerticale   => EstStyleOrientable && OrientationCourante == OrientationCadran.Vertical;

    /// <summary>Empreinte DIP du cadran courant à l'échelle 1 : la fenêtre y lie sa taille (plus de Viewbox, phase 40).</summary>
    public double LargeurCadran => EmpreinteCadran.Pour(CadranStyle, OrientationCourante).Width;
    public double HauteurCadran => EmpreinteCadran.Pour(CadranStyle, OrientationCourante).Height;

    /// <summary>Bande réservée sous l'empreinte pour la rangée de pastilles (§11 B1) : 14 pour Fusible, Marée et Volets,
    /// 0 pour Arcs et Braises. L'aperçu des réglages l'exclut de son cadrage.</summary>
    public double HauteurBandePastilles => EmpreinteCadran.Bande(CadranStyle);

    /// <summary>Hauteur de la fenêtre overlay = empreinte du cadran + bande des pastilles (§11 B1) : la fenêtre y lie sa
    /// hauteur ; l'empreinte (HauteurCadran) reste celle du cadran.</summary>
    public double HauteurFenetre => EmpreinteCadran.Fenetre(CadranStyle, OrientationCourante).Height;

    private void NotifierEmpreinte()
    {
        OnPropertyChanged(nameof(EstStyleOrientable));
        OnPropertyChanged(nameof(OrientationCourante));
        OnPropertyChanged(nameof(EstOrientationHorizontale));
        OnPropertyChanged(nameof(EstOrientationVerticale));
        OnPropertyChanged(nameof(LargeurCadran));
        OnPropertyChanged(nameof(HauteurCadran));
        OnPropertyChanged(nameof(HauteurBandePastilles));
        OnPropertyChanged(nameof(HauteurFenetre));
    }

    private static OrientationCadran Neutre(Orientation o) => o == Orientation.Vertical ? OrientationCadran.Vertical : OrientationCadran.Horizontal;
    private static Orientation VersWpf(OrientationCadran o) => o == OrientationCadran.Vertical ? Orientation.Vertical : Orientation.Horizontal;

    /// <summary>Choisit l'orientation du cadran COURANT (carte « Orientation » des réglages) : ne change que la sienne,
    /// persiste avec relecture disque fraîche (GAP-1). Sans effet pour Arcs et Braises.</summary>
    [RelayCommand]
    private void ChoisirOrientation(OrientationCadran orientation)
    {
        if (!EstStyleOrientable) return;
        var style = CadranStyle;
        switch (style)
        {
            case CadranStyle.Fusible: OrientationFusible = VersWpf(orientation); break;
            case CadranStyle.Maree:   OrientationMaree   = VersWpf(orientation); break;
            case CadranStyle.Volets:  OrientationVolets  = VersWpf(orientation); break;
        }
        _settings = _settingsService.Modifier(s => style switch
        {
            CadranStyle.Fusible => s with { OrientationFusible = orientation },
            CadranStyle.Maree => s with { OrientationMaree = orientation },
            CadranStyle.Volets => s with { OrientationVolets = orientation },
            _ => s,
        });
    }

    /// <summary>Catalogue des styles de cadran affiché dans la fenêtre de réglages (surbrillance du sélectionné).</summary>
    public ObservableCollection<CadranStyleChoice> CadranStyles { get; } = new();

    /// <summary>Nom du style de cadran courant (« Anneaux », « Braises »…), tel que l'affiche le catalogue : légende de l'aperçu
    /// vivant des réglages.</summary>
    public string NomStyleCadran => CadranStyles.FirstOrDefault(c => c.Style == CadranStyle)?.Name ?? "";

    /// <summary>Sélectionne un style de cadran : surbrillance, bascule des Visibility, persistance (GAP-1 :
    /// Load DISQUE frais avant Save, pour ne pas écraser un réglage écrit ailleurs — ex. OverlayController).</summary>
    [RelayCommand]
    private void SelectCadranStyle(CadranStyleChoice? choice)
    {
        if (choice is null) return;
        foreach (var c in CadranStyles) c.IsSelected = ReferenceEquals(c, choice);
        CadranStyle = choice.Style;
        var style = choice.Style;
        _settings = _settingsService.Modifier(s => s with { CadranStyle = style });
    }

    /// <summary>Catalogue des styles du widget de sessions (settings). Surbrillance du sélectionné.</summary>
    public ObservableCollection<SessionStyleChoice> SessionStyles { get; } = new();

    /// <summary>Sélectionne un style de sessions : surbrillance + persistance (GAP-1) + application LIVE à la
    /// fenêtre de sessions via le contrôleur (si elle est affichée).</summary>
    [RelayCommand]
    private void SelectSessionStyle(SessionStyleChoice? choice)
    {
        if (choice is null) return;
        foreach (var c in SessionStyles) c.IsSelected = ReferenceEquals(c, choice);
        SessionStyle = choice.Style;
        var style = choice.Style;
        _settings = _settingsService.Modifier(s => s with { SessionStyle = style });
        _sessions.SetStyle(choice.Style);   // application immédiate si le panneau est ouvert
    }

    // Style de sessions courant (scalaire) : pilote la visibilité de l'option « Disposition verticale »
    // (réglages), affichée pour les styles EN RANGÉE (Sonar / Jetons / Veilleurs).
    [ObservableProperty] private SessionStyle _sessionStyle;
    public bool IsRowStyle => SessionStyle is SessionStyle.Sonar or SessionStyle.Jetons or SessionStyle.Veilleurs;
    partial void OnSessionStyleChanged(SessionStyle value) => OnPropertyChanged(nameof(IsRowStyle));

    /// <summary>Disposition VERTICALE (colonne) des styles en rangée (réglage). Reflète l'état persisté ;
    /// le toggle des réglages appelle <see cref="ToggleVerticalLayoutCommand"/>.</summary>
    [ObservableProperty] private bool _verticalLayout;

    // L'aperçu du widget dans les réglages suit la disposition choisie (rangée / colonne), comme le widget réel.
    partial void OnVerticalLayoutChanged(bool value)
    {
        if (Reglages is not null)
            Reglages.ApercuSessions.RowOrientation = value ? System.Windows.Controls.Orientation.Vertical : System.Windows.Controls.Orientation.Horizontal;
    }

    /// <summary>Bascule horizontal ↔ vertical (Sonar/Jetons/Veilleurs) : persiste (GAP-1) + applique en LIVE.</summary>
    [RelayCommand]
    private void ToggleVerticalLayout()
    {
        VerticalLayout = !VerticalLayout;
        var vertical = VerticalLayout;
        _settings = _settingsService.Modifier(s => s with { VerticalLayout = vertical });
        _sessions.SetVerticalLayout(VerticalLayout);
    }

    // --- Thèmes visuels (settings) ---

    /// <summary>Version de l'app (« v3.4.0 ») affichée dans la barre de titre des réglages : les trois composantes publiées.</summary>
    public string AppVersion => "v" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "");

    /// <summary>Catalogue des thèmes affiché dans la fenêtre de réglages (surbrillance du sélectionné).</summary>
    public ObservableCollection<ThemeChoice> Themes { get; } = new();

    /// <summary>Section Thème en trois groupes titrés (Pâle, Classique, Vive) : mêmes instances que <see cref="Themes"/>.</summary>
    public ObservableCollection<GroupeThemes> GroupesThemes { get; } = new();

    /// <summary>Clé du thème actif (persisté). Consommé par la vue pour appliquer les pinceaux au démarrage.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NomThemeActif))]
    private string _selectedThemeKey = "minuit";

    /// <summary>Nom du thème actif (« Aurore »…) : légende de l'aperçu vivant des réglages.</summary>
    public string NomThemeActif => ThemeCatalog.ByKey(SelectedThemeKey).Name;

    /// <summary>Émis quand le thème change → la vue met à jour les ressources de pinceaux de la fenêtre.</summary>
    public event Action<ChronosTheme>? ThemeChanged;

    /// <summary>Sélectionne un thème : surbrillance, persistance, application aux jauges + notification de la vue.</summary>
    [RelayCommand]
    private void SelectTheme(ThemeChoice? choice)
    {
        if (choice is null) return;
        foreach (var t in Themes) t.IsSelected = ReferenceEquals(t, choice);
        SelectedThemeKey = choice.Theme.Key;
        var cle = choice.Theme.Key;
        _settings = _settingsService.Modifier(s => s with { ThemeKey = cle }); // GAP-1
        FiveHour.SetTheme(choice.Theme);
        SevenDay.SetTheme(choice.Theme);
        ThemeChanged?.Invoke(choice.Theme);
        _sessions.SetTheme(choice.Theme);   // le widget de sessions (l'autre overlay) suit le même thème
    }

    /// <summary>
    /// <paramref name="etatServeur"/> est OPTIONNEL et en DERNIÈRE position, et ce n'est pas un détail de
    /// style : les sites de construction préexistants (2 en tests, la production passant par la DI)
    /// compilent sans une retouche. Même protocole d'extension qu'au plan 17-05 pour <c>IAuthStatus</c>,
    /// puis qu'au plan 18-05 pour <c>DiagnosticService</c> et ses 10 sites.
    /// <paramref name="journal"/> (JRN-04, 32-05) suit le même protocole, en toute dernière position, puis
    /// <paramref name="reconstruction"/> (TOK-02, 33-05) après lui, puis <paramref name="ouvreurHistorique"/> (ACC-02, 35-02) et
    /// <paramref name="historique"/> (ACC-01, 35-02), puis <paramref name="pressePapiers"/> (réglages v2 : « ⧉ Copier » du diagnostic),
    /// puis <paramref name="oubliReleve"/> (DS2-03 / D-03, 42.4-03 : la déconnexion efface le dernier relevé exact ; injecté par la
    /// DI dès que le type est enregistré, null sinon — la résolution sans enregistrement reste possible).
    /// </summary>
    public MainViewModel(
        RefreshOrchestrator orchestrator, IUiDispatcher ui, IClock clock,
        IWindowController controller, IAutostartService autostart,
        SettingsService settings,
        DiagnosticService diagnostic, IOAuthLogin oauthLogin,
        ISessionsController sessions, IAuthStatus authStatus,
        IEtatServeur? etatServeur = null,
        IEtatJournal? journal = null,
        IEtatReconstruction? reconstruction = null,
        IOuvreurHistorique? ouvreurHistorique = null,
        HistoriqueViewModel? historique = null,
        IPressePapiers? pressePapiers = null,
        IOubliDernierReleve? oubliReleve = null,
        TimeZoneInfo? fuseau = null)
    {
        _fuseau = fuseau ?? TimeZoneInfo.Local;
        _ui = ui;
        _clock = clock;
        _demarrage = clock.UtcNow;   // JRN-04 / D-32-21 : le démarrage du processus, pour ne pas crier sur l'écriture de la veille
        _controller = controller;
        _autostart = autostart;
        _orchestrator = orchestrator; // mémorisé pour re-déclencher un recalcul immédiat (bascule de source, login OAuth)
        _settingsService = settings;
        _diagnostic = diagnostic;
        _oauthLogin = oauthLogin;
        _sessions = sessions;
        _oubliReleve = oubliReleve;               // DS2-03 / D-03 : optionnel, en fin de liste (motif 32-05 / 33-05)
        _ouvreurHistorique = ouvreurHistorique;   // ACC-02 : optionnel, en fin de liste (motif 32-05 / 33-05)
        Historique = historique;                  // ACC-01 / D-35-08 : le singleton de la fenêtre, partagé avec la carte des réglages
        _settings = settings.Load();

        // Réglages v2 : l'état propre à la fenêtre de réglages. Le rapport de diagnostic est construit SUR LE POOL (Task.Run) :
        // BuildReportAsync commence par des lectures disque synchrones, qui gèleraient la fenêtre sur le thread UI (§5).
        Reglages = new ReglagesViewModel(new ReglagesHistoriqueSurDisque(settings),
                                         () => Task.Run(() => _diagnostic.BuildReportAsync()), clock, pressePapiers);

        // État initial des toggles du menu : miroir de l'état RÉEL (settings + service autostart).
        IsBackground = _settings.Background;
        IsAutostart = _autostart.IsEnabled();
        IsSondeEnTetesActivee = _settings.SondeEnTetesActivee;   // HDR-06 — miroir de l'état RÉEL, comme ci-dessus
        IsLoggedIn = _oauthLogin.IsLoggedIn;
        IsSessionsWidgetEnabled = _sessions.IsEnabled;
        IsModeEtendu = _settings.CadranMode == CadranDisplayMode.Etendu; // défaut Normal

        // Style de cadran : refléter le persisté (défaut Arcs) et peupler le catalogue du sélecteur (settings).
        CadranStyle = _settings.CadranStyle;
        OrientationFusible = VersWpf(_settings.OrientationFusible);   // phase 40 : orientation mémorisée par cadran
        OrientationMaree = VersWpf(_settings.OrientationMaree);
        OrientationVolets = VersWpf(_settings.OrientationVolets);
        foreach (var (style, name) in new[]
                 {
                     (CadranStyle.Arcs, "Anneaux"), (CadranStyle.Braises, "Braises"),
                     (CadranStyle.Fusible, "Fusible"), (CadranStyle.Maree, "Marée"),
                     (CadranStyle.Volets, "Volets"),
                 })
            CadranStyles.Add(new CadranStyleChoice(style, name, style == _settings.CadranStyle));

        // Style de sessions : peupler le catalogue du sélecteur (settings), surbrillance du persisté.
        foreach (var (style, name) in new[]
                 {
                     (SessionStyle.Pastilles, "Pastilles"), (SessionStyle.Marge, "Marge"),
                     (SessionStyle.Jetons, "Jetons"), (SessionStyle.Sonar, "Sonar"),
                     (SessionStyle.Facade, "Façade"), (SessionStyle.Etagere, "Étagère"),
                     (SessionStyle.Annonciateur, "Voyants"), (SessionStyle.Veilleurs, "Veilleurs"),
                 })
            SessionStyles.Add(new SessionStyleChoice(style, name, style == _settings.SessionStyle));
        SessionStyle = _settings.SessionStyle;   // scalaire courant (option disposition verticale)
        VerticalLayout = _settings.VerticalLayout;
        OnVerticalLayoutChanged(VerticalLayout);   // aperçu du widget aligné dès le départ (le setter ne notifie pas si false)

        // Thèmes : peupler le catalogue (surbrillance du persisté) et appliquer aux jauges dès le départ.
        SelectedThemeKey = _settings.ThemeKey;
        var active = ThemeCatalog.ByKey(SelectedThemeKey);
        foreach (var t in ThemeCatalog.All) Themes.Add(new ThemeChoice(t, t.Key == active.Key));
        foreach (var cat in Enum.GetValues<CategorieTheme>())
            GroupesThemes.Add(new GroupeThemes(GroupeThemes.TitreDe(cat), Themes.Where(c => c.Theme.Categorie == cat).ToList()));
        FiveHour.SetTheme(active);
        SevenDay.SetTheme(active);

        orchestrator.SnapshotChanged += OnSnapshotChanged; // callback thread pool (horloge données)

        // TOK-02 : canal d'état d'authentification. MÊME motif que l'horloge données ci-dessus —
        // le service expose l'événement (émis sur le thread pool), le VM marshalle lui-même.
        _authStatus = authStatus;
        authStatus.EtatChange += SurEtatAuthChange;
        AppliquerEtatAuth(authStatus.Etat);   // état initial, sans attendre la première transition

        // HDR-03 / HDR-04 : canal LATÉRAL de la sonde. Optionnel — absent, l'état serveur reste muet.
        // MajTexteEtatSonde() est appelé DÈS ICI, sans attendre une transition : même raison qu'au plan
        // 17-05 pour la pastille d'authentification — sur cette machine rien ne transitera avant
        // longtemps, et un état appliqué seulement sur transition resterait vide indéfiniment.
        _etatServeur = etatServeur;
        if (_etatServeur is not null)
        {
            _etatServeur.DepassementChange += SurDepassementChange;
            MajTexteEtatSonde();
        }

        // JRN-04 : l'état du journal des relevés. Optionnel — absent, la carte des réglages reste masquée. Relu à chaque
        // tick (ApplySnapshot) : 60 s suffisent pour un seuil de 15 min ; aucun événement, aucun timer supplémentaire.
        _journal = journal;
        MajTexteEtatJournal();

        // TOK-02 : la progression de la reconstruction des agrégats. Optionnelle — absente, rien n'est affiché. Relue à chaque tick
        // comme l'état du journal : l'événement Changement (levé sur le thread de fond, ≈ une fois par fichier) n'est PAS écouté ici —
        // la coalescence est le tick lui-même, et la frontière de thread reste unique (RAF-04).
        _reconstruction = reconstruction;
        MajTexteReconstruction();
    }

    // FRONTIÈRE DE THREAD — franchie UNE seule fois (RAF-04). Aucune mutation d'ObservableProperty hors d'ici.
    private void OnSnapshotChanged(object? sender, UsageSnapshot snap) => _ui.Post(() => ApplySnapshot(snap));

    // FRONTIÈRE DE THREAD — franchie UNE seule fois (RAF-04), comme OnSnapshotChanged.
    private void SurEtatAuthChange(object? s, EtatAuthentification e) => _ui.Post(() => AppliquerEtatAuth(e));

    // TROISIÈME et dernière frontière de thread du ViewModel (RAF-04). Le plan 17-05 annonçait la
    // deuxième comme « dernière » : cet ajout l'amende, en conservant le motif à l'identique — un
    // service NEUTRE émet sur un thread du pool, l'abonné marshalle lui-même.
    private void SurDepassementChange(object? s, EtatDepassement? d) => _ui.Post(MajTexteEtatSonde);

    // Entrées BRUTES des pastilles, mémorisées parce qu'elles arrivent par deux canaux distincts et à
    // des instants distincts (un snapshot ; un événement d'authentification). Sans ce point de
    // recomposition unique, un changement d'état d'authentification survenant APRÈS le dernier snapshot
    // laisserait l'invitation périmée — et réciproquement.
    private EtatAuthentification _etatAuth = EtatAuthentification.Connecte;
    private bool _jamaisDExactEtRienAAfficher;

    /// <summary>Thread UI uniquement. Recompose les QUATRE marques du cadran à partir des entrées
    /// brutes — les trois pastilles, et depuis la phase 20 la marque du relevé daté.
    /// L'invitation s'efface devant la pastille de déconnexion : les deux portent le MÊME geste
    /// (ReconnecterCommand), les afficher ensemble sur un cadran de 170 px serait une redondance, pas
    /// une information — et la déconnexion est le diagnostic le plus précis des deux.
    ///
    /// POINT DE RECOMPOSITION UNIQUE, et c'est la raison d'être de cette méthode : un empilement de
    /// visibilités posées chacune à son propre instant laisserait la plus ancienne périmée.</summary>
    private void MajPastilles()
    {
        AfficherPastilleDeconnexion = _etatAuth == EtatAuthentification.Deconnecte;
        AfficherPastilleHorsLigne   = _etatAuth == EtatAuthentification.HorsLigne;
        AfficherInvitationConnexion = _jamaisDExactEtRienAAfficher && !AfficherPastilleDeconnexion;

        // EXA-03 — RAPPORTÉ par les deux jauges, qui le tiennent elles-mêmes de la doctrine. Aucune
        // comparaison d'horodatage ici : c'est ce que la garde de source impose, et c'est la forme.
        AfficherReleveDate = FiveHour.EstDate || SevenDay.EstDate;
    }

    /// <summary>Thread UI uniquement. NonConnecte n'allume RIEN par lui-même, phase 19 comprise : c'est
    /// le BIT DU MAGASIN (« un exact a-t-il déjà été obtenu ») et non l'état d'authentification qui
    /// décide de l'invitation EXA-05. Un utilisateur peut être parfaitement connecté et n'avoir jamais
    /// obtenu le moindre chiffre (sonde coupée, endpoint muet) ; l'allumer ici créerait à l'inverse un
    /// badge permanent pour qui a délibérément choisi de ne pas se connecter.</summary>
    internal void AppliquerEtatAuth(EtatAuthentification e)
    {
        _etatAuth = e;
        MajPastilles();
    }

    /// <summary>Applique un snapshot (thread UI) : pousse chaque fenêtre, l'état global, puis rend.</summary>
    internal void ApplySnapshot(UsageSnapshot snap)
    {
        _last = snap; // mémorisé pour le reset 5 h local (timeline 24 h d'Interpolate)

        FiveHour.Apply(snap.FiveHour);
        SevenDay.Apply(snap.SevenDay);
        DataUnavailable = snap.FiveHour.Reliability == SourceReliability.Unavailable
                       && snap.SevenDay.Reliability == SourceReliability.Unavailable;

        // EXA-05 : jamais un pourcentage quand aucun exact n'a JAMAIS été obtenu — on invite à se
        // connecter. « == false » et NON « != true » : null signifie « non évalué » (magasin en panne,
        // ou snapshot né hors de la couche de doctrine, dont UsageSnapshot.Empty), et une absence de
        // réponse ne doit jamais produire une affirmation.
        _jamaisDExactEtRienAAfficher = snap.UnExactADejaEteObtenu == false && DataUnavailable;
        MajPastilles();

        MajTexteEtatSonde();        // HDR-03 : le statut déclaré suit les fenêtres, tick par tick
        MajTexteEtatJournal();      // JRN-04 : l'âge de la dernière écriture du journal, tick par tick (D-32-21)
        MajTexteReconstruction();   // TOK-02 : la progression de la reconstruction des agrégats, tick par tick (D-33-22)
        MajInfobulleReleve();       // EXA-06 : et l'infobulle nomme QUI les alimente, et depuis quand
        Interpolate(_clock.UtcNow); // premier rendu immédiat (pas d'overlay vide entre deux ticks)
    }

    /// <summary>
    /// HDR-03 / HDR-04 — thread UI uniquement. Assemble ce que le SERVEUR a déclaré : le statut de
    /// chaque fenêtre, puis le dépassement du compte.
    ///
    /// Le vocabulaire n'est PAS remappé ici : on relit les textes déjà calculés par les deux jauges.
    /// Un second mapping de statut divergerait du premier le jour où le vocabulaire du serveur bougera.
    ///
    /// Rien de rapporté → chaîne VIDE et ligne masquée. Jamais « autorisé » par défaut : l'absence
    /// d'information n'est pas une bonne nouvelle, et la présenter comme telle serait exactement la
    /// panne silencieuse que ce milestone éradique.
    /// </summary>
    private void MajTexteEtatSonde()
    {
        var morceaux = new List<string>();

        if (FiveHour.HasStatutServeur) morceaux.Add("5 h : " + FiveHour.TexteStatutServeur);
        if (SevenDay.HasStatutServeur) morceaux.Add("hebdo : " + SevenDay.TexteStatutServeur);

        // Le dépassement arrive par le canal LATÉRAL : il décrit le COMPTE, donc il survit à un Best()
        // qui aurait écarté la fenêtre porteuse. Pourcentage via le point unique de conversion (HDR-05).
        if (_etatServeur?.Depassement is { Utilization: not null } d)
            morceaux.Add("dépassement " + UsageNormalization.PourcentagePourAffichage(d.Utilization));

        TexteEtatSonde = string.Join(" · ", morceaux);
        AfficherEtatSonde = TexteEtatSonde.Length > 0;
    }

    /// <summary>
    /// JRN-04 — thread UI uniquement. L'âge de la dernière écriture du journal, en première classe : la phase 32 est née
    /// d'un magasin qu'on croyait figé sans qu'aucun canal ne le dise (CPT-02).
    ///
    /// D-32-21 : « muet » se mesure depuis max(démarrage, dernière écriture) — mesuré depuis la seule dernière écriture,
    /// l'alerte s'allumerait à chaque lancement sur l'écriture de la veille, ce qui n'est pas « muet alors que Chronos
    /// tourne ». Seuil = trois cadences de la sonde, dérivé, jamais 900 s en dur. D-32-22 : mêmes mots que le diagnostic
    /// (« dernière écriture {ancienneté} », « muet depuis N min »), <see cref="LibelleSource.Anciennete"/> pour les paliers.
    /// Recalculé au tick de l'orchestrateur (60 s suffisent pour 15 min).
    /// </summary>
    private void MajTexteEtatJournal()
    {
        // ACC-01 : le sous-texte de la carte « Historique d'utilisation », au même tick (l'amorce de l'ouverture arrive en fond).
        SousTexteHistorique = "hebdo / 5 h / tokens"
            + (_journal?.JournalOuvertLe is { } ouvert && Historique is { } h ? " · journal du " + TextesHistorique.DateLongue(ouvert, h.Fuseau) : "");

        if (_journal is null)
        {
            TexteEtatJournal = "";
            AlerteJournal = false;
            AfficherEtatJournal = false;
            return;
        }

        var now = _clock.UtcNow;
        var derniere = _journal.DerniereEcriture;
        var reference = derniere is { } d && d > _demarrage ? d : _demarrage;
        var age = now - reference;
        AlerteJournal = age > JournalReleves.SeuilMuet;

        var morceaux = new List<string>();
        if (AlerteJournal)
            morceaux.Add("journal muet depuis " + (int)age.TotalMinutes + " min");

        if (_journal.DerniereErreur is { } err)
            morceaux.Add("dernière écriture : ÉCHEC — " + err);
        else if (derniere is null)
            morceaux.Add("aucune écriture depuis le démarrage");
        else if (derniere < _demarrage)
            morceaux.Add("aucune écriture depuis le démarrage · dernière écriture " + LibelleSource.Anciennete(derniere, now));
        else
            morceaux.Add("dernière écriture " + LibelleSource.Anciennete(derniere, now)
                         + " · " + _journal.RelevesEcrits + " relevé" + (_journal.RelevesEcrits > 1 ? "s" : "") + " depuis le démarrage");

        TexteEtatJournal = string.Join(" · ", morceaux);
        AfficherEtatJournal = true;
    }

    /// <summary>
    /// TOK-02 / D-33-22 — la progression en ENTIERS, formatée ici en texte ; jamais une fraction ni un pourcentage (garde TOK-05 sur
    /// la couche historique, doctrine ici). Relue au tick comme l'état du journal (D-32-21) : pas d'abonnement à
    /// <c>Changement</c> dans le VM du cadran — la fenêtre Historique (phase 34) consommera <see cref="IEtatReconstruction"/>
    /// directement pour son bandeau et sa barre. Mêmes mots que le bandeau F2 du plan de design.
    /// </summary>
    private void MajTexteReconstruction()
    {
        if (_reconstruction is null)
        {
            TexteReconstruction = "";
            AfficherReconstruction = false;
            return;
        }

        switch (_reconstruction.Phase)
        {
            case PhaseReconstruction.Reconstruction:
                TexteReconstruction = "reconstruction des tokens — " + _reconstruction.FichiersTraites + " / " + _reconstruction.FichiersTotal + " fichiers"
                                      + (_reconstruction.SemaineCouranteDisponible ? " · la semaine courante est déjà complète" : "");
                AfficherReconstruction = true;
                break;
            case PhaseReconstruction.EnEchec:
                TexteReconstruction = "agrégats de tokens : ÉCHEC — " + (_reconstruction.DerniereErreur ?? "cause inconnue");
                AfficherReconstruction = true;
                break;
            default:   // JamaisLancee, Incremental, Arretee : rien à dire au cadran
                TexteReconstruction = "";
                AfficherReconstruction = false;
                break;
        }
    }

    /// <summary>
    /// EXA-06 au cadran — thread UI uniquement. Une ligne par fenêtre : QUI l'alimente, DEPUIS QUAND, ce
    /// que la doctrine a vérifié, et la matière brute de DEL-04 si elle existe.
    ///
    /// Le vocabulaire n'est PAS remappé ici — il vient intégralement de <see cref="LibelleSource"/>,
    /// point unique du projet : deux mappings divergeraient, et l'utilisateur lirait deux noms différents
    /// pour la même source selon l'endroit où il regarde (même règle que MajTexteEtatSonde).
    ///
    /// AUCUNE arithmétique d'horodatage ici : la mise en mots de l'ancienneté appartient à
    /// LibelleSource.Anciennete, et la garde de source de ce ViewModel reste verte par construction.
    ///
    /// Le compte de tokens est joint TEL QUEL, jamais converti en points de pourcentage (EXA-04) : les
    /// limites Anthropic pondèrent par modèle, donc « tokens / plafond » restera faux à jamais.
    ///
    /// Appelée UNIQUEMENT depuis ApplySnapshot (≈ 60 s) et jamais depuis Interpolate (1 s) : voir
    /// <see cref="InfobulleReleve"/> pour le pourquoi et le retard assumé qui en découle.
    /// </summary>
    private void MajInfobulleReleve()
    {
        var now = _clock.UtcNow;
        InfobulleReleve = Ligne("5 h", FiveHour, now) + "\n" + Ligne("hebdo", SevenDay, now);

        static string Ligne(string etiquette, WindowGaugeViewModel g, DateTimeOffset now)
        {
            var morceaux = new List<string>
            {
                LibelleSource.Format(g.SourceDuReleve),
                "relevé " + LibelleSource.Anciennete(g.InstantDuReleve, now),
            };
            if (LibelleSource.Provenance(g.ProvenanceDuReleve) is { Length: > 0 } p) morceaux.Add(p);
            if (g.HasTokens) morceaux.Add(g.TokensText);
            return etiquette + " : " + string.Join(" · ", morceaux);
        }
    }

    private readonly TimeZoneInfo _fuseau;   // plan 43-09 — fuseau INJECTÉ (production Local, tests fixes)

    /// <summary>PUR, aucun I/O (RAF-03) — appelé chaque seconde par le DispatcherTimer (StartClock).</summary>
    internal void Interpolate(DateTimeOffset now)
    {
        FiveHour.Interpolate(now);
        SevenDay.Interpolate(now);
        // AUCUN jugement d'ancienneté ici, et c'est gardé par un test de source : la seule limite d'âge du
        // projet vit dans DoctrineFraicheur.LimiteAge, dérivée de la cadence de la sonde et non réglable.
        // Ce ViewModel se contente de RAPPORTER ce que la doctrine a déjà statué (FiveHour/SevenDay.EstDate).

        // JOUR-01/02 : timeline 24 h. now est UTC → convertir en heure locale pour lire minuit/le jour local.
        // Les angles se calent sur le resets_at 5 h courant (converti local) ; vides si inconnu.
        var localNow = now.ToLocalTime();
        DayFraction = Rendering.DayTimeline.Fraction(localNow);
        var localReset5h = _last?.FiveHour.ResetsAt?.ToLocalTime();
        DayResetAngles = Rendering.DayTimeline.ResetAngles(localNow, localReset5h);
        DaySubTickAngles = Rendering.DayTimeline.SubTickAngles(localNow, localReset5h);
    }

    /// <summary>Démarre l'horloge UI 1 s (RAF-03). Créé côté UI UNIQUEMENT (jamais dans le ctor → Pitfall 4).</summary>
    public void StartClock()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Interpolate(_clock.UtcNow);
        timer.Start();
    }

    // --- Commandes du menu contextuel (FEN-06 : SEUL point d'accès/sortie) ---

    /// <summary>FEN-05 : bascule le mode arrière-plan et pilote le controller (Topmost/HWND_BOTTOM).</summary>
    [RelayCommand]
    private void ToggleBackground()
    {
        IsBackground = !IsBackground;
        if (IsBackground) _controller.SendToBackground();
        else _controller.BringToForeground();
    }

    /// <summary>DEP-02 : bascule le lancement au démarrage et reflète l'état RÉEL du raccourci (.lnk).</summary>
    [RelayCommand]
    private void ToggleAutostart()
    {
        if (_autostart.IsEnabled()) _autostart.Disable();
        else _autostart.Enable();
        IsAutostart = _autostart.IsEnabled();
    }

    /// <summary>Active/désactive le widget de sessions Claude Code (installe/retire les hooks + panneau).</summary>
    [RelayCommand]
    private void ToggleSessionsWidget()
    {
        if (_sessions.IsEnabled) _sessions.Disable();
        else _sessions.Enable();
        IsSessionsWidgetEnabled = _sessions.IsEnabled;
    }

    /// <summary>Bascule le mode d'affichage du cadran (Normal ↔ Étendu) et le persiste (GAP-1 : Load disque
    /// frais avant Save, pour ne pas écraser un réglage écrit ailleurs — ex. OverlayController). Les
    /// Visibility des groupes d'anneaux réagissent aussitôt (IsModeEtendu/IsModeNormal).</summary>
    [RelayCommand]
    private void ToggleCadranMode()
    {
        IsModeEtendu = !IsModeEtendu;
        var mode = IsModeEtendu ? CadranDisplayMode.Etendu : CadranDisplayMode.Normal;
        _settings = _settingsService.Modifier(s => s with { CadranMode = mode });
    }

    /// <summary>HDR-06 — coupe ou rallume la sonde d'en-têtes. Persiste avec une relecture disque FRAÎCHE
    /// avant Save (GAP-1 : ne pas écraser un réglage écrit ailleurs), puis redéclenche l'orchestrateur pour
    /// que l'effet soit immédiat — le provider relit son interrupteur à chaque GetAsync. La sonde dépense une
    /// micro-requête par passage : c'est pourquoi cet interrupteur existe.
    /// </summary>
    [RelayCommand]
    private void ToggleSondeEnTetes()
    {
        IsSondeEnTetesActivee = !IsSondeEnTetesActivee;
        var activee = IsSondeEnTetesActivee;
        _settings = _settingsService.Modifier(s => s with { SondeEnTetesActivee = activee });
        _orchestrator.RequestRefresh();   // application immédiate (la sonde relit son flag à chaque GetAsync)
    }

    /// <summary>Se connecter à Claude (login OAuth intégré = source exacte universelle) ou se déconnecter.
    /// P-01 : après une déconnexion OU un login réussi, réarme l'autorité de jeton — sa copie mémoire est
    /// oubliée et le coffre relu. Sans ce réarmement, le rafraîchissement suivant recréait oauth.dat avec
    /// les jetons de l'ancien compte (déconnexion annulée, nouveau login écrasé en silence).
    /// DS2-03 / D-03 (décision « Les effacer ») : la déconnexion ET le changement de compte effacent le dernier
    /// relevé exact, sinon le last-exact.json de l'ancien compte restait affiché « exact — encore valide » jusqu'au
    /// reset. Séquence : oubli ; réarmement ; Logout()|LoginAsync() ; réarmement ; oubli. La première paire ferme la
    /// porte AVANT que le coffre ne change (aucune séquence de l'autorité ne sert plus l'ancien jeton) ; la seconde
    /// rattrape ce qui serait arrivé pendant le login (navigateur ouvert plusieurs secondes). Un login échoué garde la
    /// sortie anticipée de 42.3 : seule la première paire a eu lieu, l'Historique n'est jamais touché.
    /// Redéclenche ensuite l'orchestrateur pour rafraîchir aussitôt les chiffres.</summary>
    [RelayCommand]
    private async Task LoginClaude()
    {
        _oubliReleve?.OublierDernierReleve(_clock.UtcNow);   // D-03 : déconnexion ou changement de compte — avant Logout / LoginAsync
        _authStatus.ReinitialiserApresLogin();
        if (_oauthLogin.IsLoggedIn) _oauthLogin.Logout();
        else if (!await _oauthLogin.LoginAsync()) { IsLoggedIn = _oauthLogin.IsLoggedIn; return; }
        _authStatus.ReinitialiserApresLogin();   // la copie mémoire de l'autorité est oubliée, le coffre relu
        _oubliReleve?.OublierDernierReleve(_clock.UtcNow);   // D-03 : second oubli, après le changement de coffre
        IsLoggedIn = _oauthLogin.IsLoggedIn;
        _orchestrator.RequestRefresh();          // application immédiate, sans attendre le tick de 60 s
    }

    /// <summary>
    /// TOK-03 : relance le parcours de login depuis la pastille de déconnexion. Ne déconnecte JAMAIS —
    /// contrairement à <see cref="LoginClaudeCommand"/>, qui BASCULE sur IOAuthLogin.IsLoggedIn, lequel
    /// vaut _store.Exists (la seule présence du fichier) et donc « true » avec un jeton expiré : un clic
    /// y aurait SUPPRIMÉ le coffre de l'utilisateur. La pastille n'a qu'un sens : « répare-moi ».
    ///
    /// Le prochain GetAsync ne suffirait pas : l'autorité VERROUILLE l'état « Deconnecte » et pose un
    /// recul ; sans réarmement explicite, le jeton tout neuf ne serait pas utilisé et la pastille
    /// survivrait à sa propre réparation. Et RequestRefresh évite d'attendre le tick de 60 s.
    ///
    /// DS2-03 : n'efface PAS le dernier relevé exact — même compte dans la quasi-totalité des cas (« répare-moi »),
    /// et effacer y masquerait jusqu'à 5 min (frein de la sonde) un chiffre vrai.
    /// </summary>
    [RelayCommand]
    private async Task ReconnecterAsync()
    {
        var ok = await _oauthLogin.LoginAsync();
        IsLoggedIn = _oauthLogin.IsLoggedIn;
        if (!ok) return;
        _authStatus.ReinitialiserApresLogin();
        _orchestrator.RequestRefresh();
    }

    /// <summary>FEN-06 : ferme l'application (seul point de sortie d'une fenêtre sans barre de titre ni des tâches).</summary>
    [RelayCommand]
    private void Quit() => _controller.Quit();
}
