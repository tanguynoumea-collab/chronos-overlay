using System.Collections.ObjectModel;
using Chronos.Models;
using Chronos.Services;
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
    private readonly IBudgetPrompt _budgetPrompt;
    private readonly RefreshOrchestrator _orchestrator;
    private readonly SettingsService _settingsService;
    private readonly DiagnosticService _diagnostic;
    private readonly IStatusLineSetup _statusLineSetup;
    private readonly IOAuthLogin _oauthLogin;
    private readonly ISessionsController _sessions;

    private ChronosSettings _settings;   // état persisté courant (coin/mode/ancre)
    private UsageSnapshot? _last;         // dernier snapshot appliqué (pour ré-appliquer après recalibrage)

    public WindowGaugeViewModel FiveHour { get; } = new(TimeSpan.FromHours(5));
    public WindowGaugeViewModel SevenDay { get; } = new(TimeSpan.FromDays(7));

    [ObservableProperty] private bool _dataUnavailable;
    [ObservableProperty] private DateTimeOffset? _capturedAt; // staleness pour l'UI Phase 5
    [ObservableProperty] private bool _isStale;

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
    }

    public MainViewModel(
        RefreshOrchestrator orchestrator, IUiDispatcher ui, IClock clock,
        IWindowController controller, IAutostartService autostart,
        IRecalibrationPrompt prompt, IBudgetPrompt budgetPrompt, SettingsService settings,
        DiagnosticService diagnostic, IStatusLineSetup statusLineSetup, IOAuthLogin oauthLogin,
        ISessionsController sessions)
    {
        _ui = ui;
        _clock = clock;
        _controller = controller;
        _autostart = autostart;
        _prompt = prompt;
        _budgetPrompt = budgetPrompt;
        _orchestrator = orchestrator; // mémorisé pour re-déclencher un recalcul après calibration (CAL-01)
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
        IsStatusLineSourceEnabled = _statusLineSetup.IsEnabled();
        IsLoggedIn = _oauthLogin.IsLoggedIn;
        IsSessionsWidgetEnabled = _sessions.IsEnabled;
        IsModeEtendu = _settings.CadranMode == CadranDisplayMode.Etendu; // défaut Normal



        // Thèmes : peupler le catalogue (surbrillance du persisté) et appliquer aux jauges dès le départ.
        SelectedThemeKey = _settings.ThemeKey;
        var active = ThemeCatalog.ByKey(SelectedThemeKey);
        foreach (var t in ThemeCatalog.All) Themes.Add(new ThemeChoice(t, t.Key == active.Key));
        FiveHour.SetTheme(active);
        SevenDay.SetTheme(active);

        orchestrator.SnapshotChanged += OnSnapshotChanged; // callback thread pool (horloge données)
    }

    // FRONTIÈRE DE THREAD — franchie UNE seule fois (RAF-04). Aucune mutation d'ObservableProperty hors d'ici.
    private void OnSnapshotChanged(object? sender, UsageSnapshot snap) => _ui.Post(() => ApplySnapshot(snap));

    /// <summary>Applique un snapshot (thread UI) : recalibre le repli hebdo, pousse chaque fenêtre, l'état global, puis rend.</summary>
    internal void ApplySnapshot(UsageSnapshot snap)
    {
        _last = snap; // mémorisé pour une éventuelle ré-application après recalibrage

        // ROB-03 : recalibrage best-effort AVANT SevenDay.Apply. La fonction pure ne touche PAS une
        // source exacte (les chiffres exacts priment) et conserve Estimated pour le repli → badge « estimée ».
        var weekly = WeeklyRecalibration.Apply(snap.SevenDay, _settings.WeeklyAnchor, _clock.UtcNow);

        FiveHour.Apply(snap.FiveHour);
        SevenDay.Apply(weekly);
        CapturedAt = snap.SourceCapturedAt;
        DataUnavailable = snap.FiveHour.Reliability == SourceReliability.Unavailable
                       && snap.SevenDay.Reliability == SourceReliability.Unavailable;
        Interpolate(_clock.UtcNow); // premier rendu immédiat (pas d'overlay vide entre deux ticks)
    }

    /// <summary>PUR, aucun I/O (RAF-03) — appelé chaque seconde par le DispatcherTimer (StartClock).</summary>
    internal void Interpolate(DateTimeOffset now)
    {
        FiveHour.Interpolate(now);
        SevenDay.Interpolate(now);
        IsStale = CapturedAt is { } c && (now - c) > TimeSpan.FromMinutes(2);

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

    /// <summary>CAL-01 : demande les deux plafonds, les persiste (GAP-1 : Load DISQUE frais avant Save)
    /// en marquant source=Manual pour les champs saisis (None si laissé vide), puis redéclenche
    /// l'orchestrateur → couleur des arcs recalculée aussitôt avec les nouveaux plafonds. Annulation →
    /// aucun changement persisté.</summary>
    [RelayCommand]
    private void CalibrateBudgets()
    {
        var courant = _settingsService.Load();
        var sel = _budgetPrompt.Ask(courant.FiveHourTokenBudget, courant.WeeklyTokenBudget);
        if (sel is null) return;

        var now = _clock.UtcNow;
        _settings = _settingsService.Load() with   // GAP-1 : relire l'état disque le plus récent avant Save
        {
            FiveHourTokenBudget = sel.FiveHour,
            FiveHourBudgetSource = sel.FiveHour is not null ? BudgetSource.Manual : BudgetSource.None,
            FiveHourBudgetCalibratedAt = sel.FiveHour is not null ? now : null,
            WeeklyTokenBudget = sel.Weekly,
            WeeklyBudgetSource = sel.Weekly is not null ? BudgetSource.Manual : BudgetSource.None,
            WeeklyBudgetCalibratedAt = sel.Weekly is not null ? now : null,
        };
        _settingsService.Save(_settings);
        _orchestrator.RequestRefresh(); // application immédiate (le provider Load() frais à chaque GetAsync)
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

    /// <summary>Active/désactive la SOURCE EXACTE via le pont statusLine de Claude Code (installe ou
    /// retire l'intégration dans ~/.claude/settings.json) et reflète l'état RÉEL dans le menu.</summary>
    [RelayCommand]
    private void ToggleStatusLineSource()
    {
        if (_statusLineSetup.IsEnabled()) _statusLineSetup.Disable();
        else _statusLineSetup.Enable();
        IsStatusLineSourceEnabled = _statusLineSetup.IsEnabled();
    }

    /// <summary>Construit le rapport de diagnostic (token, appel OAuth, sources, plafonds, résultat)
    /// pour affichage à l'écran (pop-up, screenshot-able). Le token n'y figure jamais.</summary>
    public Task<string> BuildDiagnosticReportAsync() => _diagnostic.BuildReportAsync();

    /// <summary>FEN-06 : ferme l'application (seul point de sortie d'une fenêtre sans barre de titre ni des tâches).</summary>
    [RelayCommand]
    private void Quit() => _controller.Quit();
}
