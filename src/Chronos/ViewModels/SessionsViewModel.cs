using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using Chronos.Services;
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

    private static readonly Brush Amber = Frozen("#E9A23C");   // à toi (permission/question)
    private static readonly Brush Blue = Frozen("#5B8DEF");    // tour fini, à toi
    private static readonly Brush Green = Frozen("#3FB98A");   // en cours
    private static readonly Brush Gray = Frozen("#7A7A85");    // inconnu/périmé

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

    private static (string, Brush, bool) Describe(SessionActivity a) => a switch
    {
        SessionActivity.WaitingAttention => ("à toi", Amber, true),
        SessionActivity.WaitingTurn => ("tour fini", Blue, true),
        SessionActivity.Working => ("en cours", Green, false),
        _ => ("inconnu", Gray, false),
    };

    private static string Age(System.TimeSpan d)
    {
        if (d < System.TimeSpan.FromSeconds(60)) return "à l'instant";
        if (d < System.TimeSpan.FromHours(1)) return $"il y a {(int)d.TotalMinutes} min";
        return $"il y a {(int)d.TotalHours} h";
    }

    private static Brush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
