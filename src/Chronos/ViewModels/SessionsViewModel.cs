using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using Chronos.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>Une session dans la liste : projet, libellé d'état, couleur, détail temporel.</summary>
public sealed partial class SessionItemVm : ObservableObject
{
    public string SessionId { get; }
    [ObservableProperty] private string _project = "";
    [ObservableProperty] private string _stateText = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private Brush _stateBrush = Brushes.Gray;
    [ObservableProperty] private bool _isWaiting;

    public SessionItemVm(string sessionId) => SessionId = sessionId;
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

    public ObservableCollection<SessionItemVm> Items { get; } = new();

    [ObservableProperty] private int _waitingCount;   // sessions qui réclament une intervention
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _hasWaiting;
    [ObservableProperty] private string _summary = "Aucune session";

    private static readonly Brush Amber = Frozen("#E9A23C");   // à toi (permission/question)
    private static readonly Brush Blue = Frozen("#5B8DEF");    // tour fini, à toi
    private static readonly Brush Green = Frozen("#3FB98A");   // en cours
    private static readonly Brush Gray = Frozen("#7A7A85");    // inconnu/périmé

    public SessionsViewModel(SessionMonitor monitor, IClock clock)
    {
        _monitor = monitor;
        _clock = clock;
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
            var it = new SessionItemVm(s.SessionId) { Project = s.Project };
            (it.StateText, it.StateBrush, it.IsWaiting) = Describe(s.Activity);
            it.Detail = Age(now - s.UpdatedAt);
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
