using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using Chronos.Services;
using Chronos.Theming;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chronos.ViewModels;

/// <summary>Une session dans la liste : projet, libellé d'état, couleur, détail temporel, + archivage.</summary>
public sealed partial class SessionItemVm : ObservableObject
{
    public string SessionId { get; }
    private readonly System.Action<string> _archive;

    [ObservableProperty] private string _project = "";
    [ObservableProperty] private string _stateText = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private Brush _stateBrush = Brushes.Gray;
    [ObservableProperty] private bool _isWaiting;
    [ObservableProperty] private string _kindLabel = "";   // type bureau (Chat/Code/Cowork) ; vide pour les sessions CLI

    // États d'activité distincts, exposés pour les templates de la refonte visuelle (formes/rythmes par état).
    // « à toi » (Attention) vs « tour fini » (Turn) = les DEUX attentes, distinguées SANS deux oranges.
    [ObservableProperty] private bool _isAttention;  // WaitingAttention → « à toi » (respire)
    [ObservableProperty] private bool _isTurn;       // WaitingTurn → « tour fini » (fixe)
    [ObservableProperty] private bool _isWorking;    // en cours
    [ObservableProperty] private bool _isGhost;      // Unknown / périmé → fantôme

    public SessionItemVm(string sessionId, System.Action<string> archive)
    {
        SessionId = sessionId;
        _archive = archive;
    }

    /// <summary>Clic droit → Archiver : retire la session de l'overlay (elle ne réapparaît plus).</summary>
    [RelayCommand]
    private void Archive() => _archive(SessionId);
}

/// <summary>
/// Liste temps réel des sessions Claude Code (source : <see cref="SessionMonitor"/>). Tri « en attente
/// d'abord ». N'affiche jamais « en attente » sur un signal périmé (le monitor l'a déjà ramené à Unknown).
/// Rafraîchi par un DispatcherTimer créé côté UI (jamais dans le ctor — Pitfall threading).
/// </summary>
public sealed partial class SessionsViewModel : ObservableObject
{
    private readonly SessionMonitor _monitor;
    private readonly IClock _clock;
    private readonly ArchiveStore _archive;

    public ObservableCollection<SessionItemVm> Items { get; } = new();

    [ObservableProperty] private int _waitingCount;   // sessions qui réclament une intervention
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _hasWaiting;
    [ObservableProperty] private string _summary = "Aucune session";

    // Style visuel du widget (refonte). Piloté par la fenêtre de réglages via SessionsController.SetStyle ;
    // les booléens IsStyleX sélectionnent le template dans SessionsWindow.xaml.
    [ObservableProperty] private SessionStyle _style;
    public bool IsStylePastilles    => Style == SessionStyle.Pastilles;
    public bool IsStyleMarge        => Style == SessionStyle.Marge;
    public bool IsStyleJetons       => Style == SessionStyle.Jetons;
    public bool IsStyleSonar        => Style == SessionStyle.Sonar;
    public bool IsStyleFacade       => Style == SessionStyle.Facade;
    public bool IsStyleEtagere      => Style == SessionStyle.Etagere;
    public bool IsStyleAnnonciateur => Style == SessionStyle.Annonciateur;
    public bool IsStyleVeilleurs    => Style == SessionStyle.Veilleurs;
    partial void OnStyleChanged(SessionStyle value)
    {
        OnPropertyChanged(nameof(IsStylePastilles));
        OnPropertyChanged(nameof(IsStyleMarge));
        OnPropertyChanged(nameof(IsStyleJetons));
        OnPropertyChanged(nameof(IsStyleSonar));
        OnPropertyChanged(nameof(IsStyleFacade));
        OnPropertyChanged(nameof(IsStyleEtagere));
        OnPropertyChanged(nameof(IsStyleAnnonciateur));
        OnPropertyChanged(nameof(IsStyleVeilleurs));
    }

    // Disposition des styles « en rangée » (Sonar / Jetons / Veilleurs) : horizontale (défaut) ou colonne.
    // Les panneaux de ces templates lient leur Orientation à RowOrientation.
    [ObservableProperty] private bool _vertical;
    public Orientation RowOrientation => Vertical ? Orientation.Vertical : Orientation.Horizontal;
    partial void OnVerticalChanged(bool value) => OnPropertyChanged(nameof(RowOrientation));

    // Couleurs d'ÉTAT dérivées du THÈME courant (cohérence avec le cadran). Recalculées par SetTheme ;
    // valeurs de départ = thème par défaut. attente → rampe ambre, en cours → rampe verte, déduit → texte atténué.
    private ChronosTheme _theme = ThemeCatalog.Default;
    private Brush _amber = FrozenC(ThemeCatalog.Default.RampAmber);   // EN ATTENTE (tour fini / à toi)
    private Brush _green = FrozenC(ThemeCatalog.Default.RampGreen);   // en cours
    private Brush _gray = FrozenC(ThemeCatalog.Default.TexteSecondaire); // inconnu/périmé

    /// <summary>Applique un thème : recolore les états (attente/en cours/déduit) selon la rampe et re-rend.
    /// Les fonds/textes des templates suivent via les DynamicResource posés par SessionsWindow.ApplyThemeBrushes.</summary>
    public void SetTheme(ChronosTheme theme)
    {
        _theme = theme;
        _amber = FrozenC(theme.RampAmber);
        _green = FrozenC(theme.RampGreen);
        _gray = FrozenC(theme.TexteSecondaire);
        Refresh(_clock.UtcNow);   // recolore les items existants
    }

    public SessionsViewModel(SessionMonitor monitor, IClock clock, ArchiveStore archive)
    {
        _monitor = monitor;
        _clock = clock;
        _archive = archive;
    }

    // Archive une session puis rafraîchit (elle disparaît immédiatement de la liste).
    private void ArchiveSession(string sessionId)
    {
        _archive.Add(sessionId);
        Refresh(_clock.UtcNow);
    }

    /// <summary>Démarre l'horloge de rafraîchissement (2 s), côté UI uniquement.</summary>
    public void StartClock()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => Refresh(_clock.UtcNow);
        timer.Start();
        Refresh(_clock.UtcNow);
    }

    /// <summary>Relit le monitor et met à jour la liste (PUR hors I/O du monitor) — testable directement.</summary>
    public void Refresh(System.DateTimeOffset now)
    {
        var snaps = _monitor.Read(now)
            .OrderBy(s => Rank(s.Activity))
            .ThenByDescending(s => s.UpdatedAt)
            .ToList();

        // Réconciliation simple (liste courte) : on aligne Items sur snaps par SessionId.
        Items.Clear();
        foreach (var s in snaps)
        {
            var it = new SessionItemVm(s.SessionId, ArchiveSession) { Project = s.Project };
            (it.StateText, it.StateBrush, it.IsWaiting) = Describe(s.Activity);
            it.IsAttention = s.Activity == SessionActivity.WaitingAttention;
            it.IsTurn = s.Activity == SessionActivity.WaitingTurn;
            it.IsWorking = s.Activity == SessionActivity.Working;
            it.IsGhost = s.Activity == SessionActivity.Unknown;
            it.Detail = Age(now - s.UpdatedAt);
            it.KindLabel = KindText(s.Kind);   // BUR-03 : type bureau visible ; vide (Unknown) pour les sessions CLI
            Items.Add(it);
        }

        TotalCount = snaps.Count;
        WaitingCount = snaps.Count(s => s.Activity is SessionActivity.WaitingAttention or SessionActivity.WaitingTurn);
        HasWaiting = WaitingCount > 0;
        Summary = TotalCount == 0 ? "Aucune session"
            : (WaitingCount > 0 ? $"{WaitingCount} en attente · {TotalCount} session(s)" : $"{TotalCount} session(s)");
    }

    private static int Rank(SessionActivity a) => a switch
    {
        SessionActivity.WaitingAttention => 0,
        SessionActivity.WaitingTurn => 1,
        SessionActivity.Working => 2,
        _ => 3,
    };

    // BUR-03 : libellé court du type de session bureau. Unknown (sessions CLI) → "" (rien affiché, pas de bruit).
    // « Chat », « Code », « Cowork » sont des noms propres de modes Claude, conservés tels quels.
    private static string KindText(SessionKind k) => k switch
    {
        SessionKind.Chat => "Chat",
        SessionKind.Code => "Code",
        SessionKind.Cowork => "Cowork",
        _ => "",
    };

    private (string, Brush, bool) Describe(SessionActivity a) => a switch
    {
        SessionActivity.WaitingAttention => ("à toi", _amber, true),        // attend une intervention → rampe ambre
        SessionActivity.WaitingTurn => ("tour fini", _amber, true),         // réflexion finie non consultée → rampe ambre
        SessionActivity.Working => ("en cours", _green, false),
        _ => ("inconnu", _gray, false),
    };

    private static string Age(System.TimeSpan d)
    {
        if (d < System.TimeSpan.FromSeconds(60)) return "à l'instant";
        if (d < System.TimeSpan.FromHours(1)) return $"il y a {(int)d.TotalMinutes} min";
        return $"il y a {(int)d.TotalHours} h";
    }

    private static Brush FrozenC(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
