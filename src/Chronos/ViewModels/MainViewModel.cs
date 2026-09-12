using System.Collections.ObjectModel;
using Chronos.Models;
using Chronos.Services;
using Chronos.Text;
using Chronos.Theming;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chronos.ViewModels;

/// <summary>
/// ViewModel racine (temps réel). S'abonne à l'horloge DONNÉES (<see cref="RefreshOrchestrator.SnapshotChanged"/>,
/// émis sur le thread pool) et franchit la frontière de thread EN UN SEUL POINT via <see cref="IUiDispatcher.Post"/>
/// (RAF-04). L'affichage vit grâce à <see cref="Interpolate"/> (PUR, aucun I/O — RAF-03), piloté par un
/// DispatcherTimer 1 s créé côté UI (<see cref="StartClock"/>) — jamais dans le ctor (Pitfall 4 : tests en [Fact] simple).
///
/// 06-04 : expose les 4 commandes du menu contextuel (SEUL point d'accès/sortie, FEN-06) —
/// arrière-plan (FEN-05), recalibrage hebdo best-effort (ROB-03), lancer au démarrage (DEP-02), quitter.
/// Le recalibrage est appliqué DANS le pipeline temps réel (ApplySnapshot) via la fonction pure
/// <see cref="WeeklyRecalibration"/> : il ne recale que le repli et CONSERVE le badge « estimée » (honnêteté).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IUiDispatcher _ui;
    private readonly IClock _clock;
    private readonly IWindowController _controller;
    private readonly IAutostartService _autostart;
    private readonly IRecalibrationPrompt _prompt;
    private readonly RefreshOrchestrator _orchestrator;
    private readonly SettingsService _settingsService;
    private readonly DiagnosticService _diagnostic;
    private readonly IStatusLineSetup _statusLineSetup;
    private readonly IOAuthLogin _oauthLogin;
    private readonly ISessionsController _sessions;
    private readonly IAuthStatus _authStatus;
    private readonly IEtatServeur? _etatServeur;

    private ChronosSettings _settings;   // état persisté courant (coin/mode/ancre)
    private UsageSnapshot? _last;         // dernier snapshot appliqué (pour ré-appliquer après recalibrage)

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

    // État reflété dans les items « à cocher » du menu (FEN-05 / DEP-02).
    [ObservableProperty] private bool _isBackground;
    [ObservableProperty] private bool _isAutostart;

    // État reflété dans l'item « Usage exact (OAuth) » du menu (INT-03).
    [ObservableProperty] private bool _isOAuthUsageEnabled;

    // État reflété dans l'item « Source exacte (Claude Code) » : le pont statusLine est-il installé ?
    [ObservableProperty] private bool _isStatusLineSourceEnabled;

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

    /// <summary>HDR-06 — interrupteur de la sonde d'en-têtes. DISTINCT d'<see cref="IsOAuthUsageEnabled"/> :
    /// la sonde consomme une vraie micro-requête sur le compte, l'autre non. Les mélanger priverait
    /// l'utilisateur du seul interrupteur qui gouverne une dépense.</summary>
    [ObservableProperty] private bool _isSondeEnTetesActivee;

    /// <summary>HDR-03 / HDR-04 — ce que le SERVEUR a déclaré (statut par fenêtre, dépassement de compte),
    /// en une ligne pour les réglages. VIDE quand rien n'est rapporté : jamais « autorisé » par défaut.</summary>
    [ObservableProperty] private string _texteEtatSonde = "";

    /// <summary>Pilote la visibilité de la ligne ci-dessus (motif HasTokens / HasUtilizationText).</summary>
    [ObservableProperty] private bool _afficherEtatSonde;

    // État reflété dans l'item « Sessions Claude Code » : le widget de sessions est-il activé ?
    [ObservableProperty] private bool _isSessionsWidgetEnabled;

    // Mode d'affichage du centre : false = pourcentages (défaut), true = temps avant reset.
    // Un clic au centre bascule via ToggleCenterMode(). ShowPercent est l'inverse (pour le binding XAML).
    [ObservableProperty] private bool _showCountdown;
    public bool ShowPercent => !ShowCountdown;
    partial void OnShowCountdownChanged(bool value) => OnPropertyChanged(nameof(ShowPercent));

    /// <summary>Bascule le centre entre pourcentages et temps avant reset (clic au centre du cadran).</summary>
    public void ToggleCenterMode() => ShowCountdown = !ShowCountdown;

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
        OnPropertyChanged(nameof(IsStyleArcs));
        OnPropertyChanged(nameof(IsStyleBraises));
        OnPropertyChanged(nameof(IsStyleFusible));
        OnPropertyChanged(nameof(IsStyleMaree));
        OnPropertyChanged(nameof(IsStyleVolets));
    }

    /// <summary>Catalogue des styles de cadran affiché dans la fenêtre de réglages (surbrillance du sélectionné).</summary>
    public ObservableCollection<CadranStyleChoice> CadranStyles { get; } = new();

    /// <summary>Sélectionne un style de cadran : surbrillance, bascule des Visibility, persistance (GAP-1 :
    /// Load DISQUE frais avant Save, pour ne pas écraser un réglage écrit ailleurs — ex. OverlayController).</summary>
    [RelayCommand]
    private void SelectCadranStyle(CadranStyleChoice? choice)
    {
        if (choice is null) return;
        foreach (var c in CadranStyles) c.IsSelected = ReferenceEquals(c, choice);
        CadranStyle = choice.Style;
        _settings = _settingsService.Load() with { CadranStyle = choice.Style };
        _settingsService.Save(_settings);
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
        _settings = _settingsService.Load() with { SessionStyle = choice.Style };
        _settingsService.Save(_settings);
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

    /// <summary>Bascule horizontal ↔ vertical (Sonar/Jetons/Veilleurs) : persiste (GAP-1) + applique en LIVE.</summary>
    [RelayCommand]
    private void ToggleVerticalLayout()
    {
        VerticalLayout = !VerticalLayout;
        _settings = _settingsService.Load() with { VerticalLayout = VerticalLayout };
        _settingsService.Save(_settings);
        _sessions.SetVerticalLayout(VerticalLayout);
    }

    // --- Thèmes visuels (settings) ---

    /// <summary>Version de l'app (« v2.4 ») affichée dans l'en-tête des réglages.</summary>
    public string AppVersion => "v" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(2) ?? "");

    /// <summary>Catalogue des thèmes affiché dans la fenêtre de réglages (surbrillance du sélectionné).</summary>
    public ObservableCollection<ThemeChoice> Themes { get; } = new();

    /// <summary>Clé du thème actif (persisté). Consommé par la vue pour appliquer les pinceaux au démarrage.</summary>
    [ObservableProperty] private string _selectedThemeKey = "minuit";

    /// <summary>Émis quand le thème change → la vue met à jour les ressources de pinceaux de la fenêtre.</summary>
    public event Action<ChronosTheme>? ThemeChanged;

    /// <summary>Sélectionne un thème : surbrillance, persistance, application aux jauges + notification de la vue.</summary>
    [RelayCommand]
    private void SelectTheme(ThemeChoice? choice)
    {
        if (choice is null) return;
        foreach (var t in Themes) t.IsSelected = ReferenceEquals(t, choice);
        SelectedThemeKey = choice.Theme.Key;
        _settings = _settingsService.Load() with { ThemeKey = choice.Theme.Key }; // GAP-1
        _settingsService.Save(_settings);
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
    /// </summary>
    public MainViewModel(
        RefreshOrchestrator orchestrator, IUiDispatcher ui, IClock clock,
        IWindowController controller, IAutostartService autostart,
        IRecalibrationPrompt prompt, SettingsService settings,
        DiagnosticService diagnostic, IStatusLineSetup statusLineSetup, IOAuthLogin oauthLogin,
        ISessionsController sessions, IAuthStatus authStatus,
        IEtatServeur? etatServeur = null)
    {
        _ui = ui;
        _clock = clock;
        _controller = controller;
        _autostart = autostart;
        _prompt = prompt;
        _orchestrator = orchestrator; // mémorisé pour re-déclencher un recalcul immédiat (bascule de source, login OAuth)
        _settingsService = settings;
        _diagnostic = diagnostic;
        _statusLineSetup = statusLineSetup;
        _oauthLogin = oauthLogin;
        _sessions = sessions;
        _settings = settings.Load();

        // État initial des toggles du menu : miroir de l'état RÉEL (settings + service autostart).
        IsBackground = _settings.Background;
        IsAutostart = _autostart.IsEnabled();
        IsOAuthUsageEnabled = _settings.OAuthUsageEnabled;
        IsSondeEnTetesActivee = _settings.SondeEnTetesActivee;   // HDR-06 — miroir de l'état RÉEL, comme ci-dessus
        IsStatusLineSourceEnabled = _statusLineSetup.IsEnabled();
        IsLoggedIn = _oauthLogin.IsLoggedIn;
        IsSessionsWidgetEnabled = _sessions.IsEnabled;
        IsModeEtendu = _settings.CadranMode == CadranDisplayMode.Etendu; // défaut Normal

        // Style de cadran : refléter le persisté (défaut Arcs) et peupler le catalogue du sélecteur (settings).
        CadranStyle = _settings.CadranStyle;
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

        // Thèmes : peupler le catalogue (surbrillance du persisté) et appliquer aux jauges dès le départ.
        SelectedThemeKey = _settings.ThemeKey;
        var active = ThemeCatalog.ByKey(SelectedThemeKey);
        foreach (var t in ThemeCatalog.All) Themes.Add(new ThemeChoice(t, t.Key == active.Key));
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

    /// <summary>Applique un snapshot (thread UI) : recalibre le repli hebdo, pousse chaque fenêtre, l'état global, puis rend.</summary>
    internal void ApplySnapshot(UsageSnapshot snap)
    {
        _last = snap; // mémorisé pour une éventuelle ré-application après recalibrage

        // ROB-03 : recalibrage best-effort AVANT SevenDay.Apply. La fonction pure ne touche PAS une
        // source exacte (les chiffres exacts priment) et conserve Estimated pour le repli → badge « estimée ».
        var weekly = WeeklyRecalibration.Apply(snap.SevenDay, _settings.WeeklyAnchor, _clock.UtcNow);

        FiveHour.Apply(snap.FiveHour);
        SevenDay.Apply(weekly);
        DataUnavailable = snap.FiveHour.Reliability == SourceReliability.Unavailable
                       && snap.SevenDay.Reliability == SourceReliability.Unavailable;

        // EXA-05 : jamais un pourcentage quand aucun exact n'a JAMAIS été obtenu — on invite à se
        // connecter. « == false » et NON « != true » : null signifie « non évalué » (magasin en panne,
        // ou snapshot né hors de la couche de doctrine, dont UsageSnapshot.Empty), et une absence de
        // réponse ne doit jamais produire une affirmation.
        _jamaisDExactEtRienAAfficher = snap.UnExactADejaEteObtenu == false && DataUnavailable;
        MajPastilles();

        MajTexteEtatSonde();        // HDR-03 : le statut déclaré suit les fenêtres, tick par tick
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

    /// <summary>
    /// ROB-03 : demande une ancre de reset hebdo ; si fournie, la persiste et ré-applique le dernier
    /// snapshot → l'arc hebdo se recale MAIS reste « estimée ». Annulation → aucun changement.
    /// </summary>
    [RelayCommand]
    private void Recalibrate()
    {
        var anchor = _prompt.Ask(_settings.WeeklyAnchor);
        if (anchor is null) return;

        // GAP-1 : relire l'état DISQUE avant d'écrire — l'OverlayController persiste coin/écran/arrière-plan
        // indépendamment ; sauvegarder la copie du constructeur écraserait ces réglages plus récents.
        _settings = _settingsService.Load() with { WeeklyAnchor = anchor };
        _settingsService.Save(_settings);
        if (_last is { } s) ApplySnapshot(s); // ré-applique → arc hebdo recalé, badge « estimée » conservé
    }

    /// <summary>INT-03 : active/désactive la source EXACTE OAuth. Persiste le flag (GAP-1 : Load DISQUE
    /// frais avant Save, pour ne pas écraser un réglage écrit par l'OverlayController) puis redéclenche
    /// l'orchestrateur → le portillon gated relit le flag frais au prochain GetAsync et bascule aussitôt.</summary>
    [RelayCommand]
    private void ToggleOAuthUsage()
    {
        IsOAuthUsageEnabled = !IsOAuthUsageEnabled;
        _settings = _settingsService.Load() with { OAuthUsageEnabled = IsOAuthUsageEnabled };
        _settingsService.Save(_settings);
        _orchestrator.RequestRefresh();   // application immédiate (le gated Load() frais à chaque GetAsync)
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
        _settings = _settingsService.Load() with
        {
            CadranMode = IsModeEtendu ? CadranDisplayMode.Etendu : CadranDisplayMode.Normal
        };
        _settingsService.Save(_settings);
    }

    /// <summary>HDR-06 — coupe ou rallume la sonde d'en-têtes. Persiste avec une relecture disque FRAÎCHE
    /// avant Save (GAP-1 : ne pas écraser un réglage écrit ailleurs), puis redéclenche l'orchestrateur pour
    /// que l'effet soit immédiat — le provider relit son interrupteur à chaque GetAsync.
    ///
    /// Réglage DISTINCT d'OAuthUsageEnabled : couper l'un ne coupe pas l'autre, parce que leurs profils de
    /// coût sont OPPOSÉS — la sonde dépense une micro-requête par passage, l'endpoint OAuth ne dépense rien.
    /// </summary>
    [RelayCommand]
    private void ToggleSondeEnTetes()
    {
        IsSondeEnTetesActivee = !IsSondeEnTetesActivee;
        _settings = _settingsService.Load() with { SondeEnTetesActivee = IsSondeEnTetesActivee };
        _settingsService.Save(_settings);
        _orchestrator.RequestRefresh();   // application immédiate (la sonde relit son flag à chaque GetAsync)
    }

    /// <summary>Se connecter à Claude (login OAuth intégré = source exacte universelle) ou se déconnecter.
    /// Après un changement d'état, redéclenche l'orchestrateur pour rafraîchir aussitôt les chiffres.</summary>
    [RelayCommand]
    private async Task LoginClaude()
    {
        if (_oauthLogin.IsLoggedIn) _oauthLogin.Logout();
        else await _oauthLogin.LoginAsync();
        IsLoggedIn = _oauthLogin.IsLoggedIn;
        _orchestrator.RequestRefresh(); // application immédiate (le provider relit le coffre à chaque GetAsync)
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

    /// <summary>Active/désactive la SOURCE EXACTE via le pont statusLine de Claude Code (installe ou
    /// retire l'intégration dans ~/.claude/settings.json) et reflète l'état RÉEL dans le menu.</summary>
    [RelayCommand]
    private void ToggleStatusLineSource()
    {
        if (_statusLineSetup.IsEnabled()) _statusLineSetup.Disable();
        else _statusLineSetup.Enable();
        IsStatusLineSourceEnabled = _statusLineSetup.IsEnabled();
    }

    /// <summary>Construit le rapport de diagnostic (token, appel OAuth, sources, résultat)
    /// pour affichage à l'écran (pop-up, screenshot-able). Le token n'y figure jamais.</summary>
    public Task<string> BuildDiagnosticReportAsync() => _diagnostic.BuildReportAsync();

    /// <summary>FEN-06 : ferme l'application (seul point de sortie d'une fenêtre sans barre de titre ni des tâches).</summary>
    [RelayCommand]
    private void Quit() => _controller.Quit();
}
