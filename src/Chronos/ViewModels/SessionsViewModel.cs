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

/// <summary>Une session dans la liste : projet, libellé d'état, couleur, détail temporel, et les trois
/// gestes du menu contextuel (traiter celle-ci, tout traiter, archiver).</summary>
public sealed partial class SessionItemVm : ObservableObject
{
    public string SessionId { get; }

    /// <summary>Instant que le SIGNAL de cette session portait au dernier rafraîchissement, en
    /// millisecondes. C'est ce que le geste explicite écrit dans le magasin réversible : marquer traité,
    /// c'est dire « j'ai vu CET épisode-là », pas « ne me montre plus jamais cette session ». Un signal
    /// plus récent la ramènera (NET-03).</summary>
    public long UpdatedAtMs { get; }

    private readonly System.Action<string> _archive;
    private readonly System.Action<SessionItemVm> _traiter;
    private readonly System.Action _traiterTout;

    [ObservableProperty] private string _project = "";
    [ObservableProperty] private string _stateText = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private Brush _stateBrush = Brushes.Gray;
    [ObservableProperty] private bool _isWaiting;

    // États d'activité distincts, exposés pour les templates de la refonte visuelle (formes/rythmes par état).
    // « à toi » (Attention) vs « tour fini » (Turn) = les DEUX attentes, distinguées SANS deux oranges.
    [ObservableProperty] private bool _isAttention;  // WaitingAttention → « à toi » (respire)
    [ObservableProperty] private bool _isTurn;       // WaitingTurn → « tour fini » (fixe)
    [ObservableProperty] private bool _isWorking;    // en cours
    [ObservableProperty] private bool _isGhost;      // Unknown / périmé → fantôme

    /// <summary>Libellé du geste de masse, porté par chaque ligne parce que le menu contextuel a pour
    /// DataContext la ligne et non la liste. Il porte le NOMBRE : un geste qui agit sur vingt sessions
    /// doit le dire avant d'être cliqué. Il dit AUSSI qu'il revient : encadré par un libellé réversible et
    /// un libellé définitif, un libellé muet sur ce point serait l'ambiguïté même que le verrou UI de ce
    /// projet interdit.</summary>
    [ObservableProperty] private string _toutTraiterLibelle = "Tout marquer traité — elles reviennent si elles redemandent";

    public SessionItemVm(string sessionId, long updatedAtMs, System.Action<string> archive,
                         System.Action<SessionItemVm> traiter, System.Action traiterTout)
    {
        SessionId = sessionId;
        UpdatedAtMs = updatedAtMs;
        _archive = archive;
        _traiter = traiter;
        _traiterTout = traiterTout;
    }

    /// <summary>Clic droit → Archiver : DÉFINITIF, la session ne revient jamais (TRT-04).</summary>
    [RelayCommand]
    private void Archive() => _archive(SessionId);

    /// <summary>Clic droit → Marquer traitée : RÉVERSIBLE, la session revient si elle me redemande
    /// quelque chose (TRT-03).</summary>
    [RelayCommand]
    private void MarquerTraitee() => _traiter(this);

    /// <summary>Clic droit → Tout marquer traité : le même geste, sur toutes les lignes visibles.</summary>
    [RelayCommand]
    private void MarquerToutTraite() => _traiterTout();
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
    private readonly TreatedStore _treated;

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

    public SessionsViewModel(SessionMonitor monitor, IClock clock, ArchiveStore archive, TreatedStore treated)
    {
        _monitor = monitor;
        _clock = clock;
        _archive = archive;
        _treated = treated;
    }

    // Archive une session puis rafraîchit (elle disparaît immédiatement de la liste).
    private void ArchiveSession(string sessionId)
    {
        _archive.Add(sessionId);
        Refresh(_clock.UtcNow);
    }

    // Geste explicite, RÉVERSIBLE : on inscrit l'épisode que l'utilisateur vient de voir, pas l'instant
    // du clic. Écrire « maintenant » masquerait aussi les demandes arrivées entre-temps — et c'est
    // précisément la confusion que cette phase supprime.
    private void MarquerSessionTraitee(SessionItemVm item)
    {
        _treated.Set(item.SessionId, item.UpdatedAtMs);
        Refresh(_clock.UtcNow);
    }

    // L'effet de masse mesuré le 2026-09-12 : vingt sessions sur cinquante-quatre basculent ensemble en
    // attente déduite. Un geste par session n'en serait pas un. La copie de la collection est
    // indispensable : Refresh vide Items.
    private void MarquerToutTraite()
    {
        foreach (var it in Items.ToList()) _treated.Set(it.SessionId, it.UpdatedAtMs);
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
        // L'ordre vient de la couche neutre : le rapport de diagnostic lit le même (OBS-01).
        var snaps = AffichageSessions.Ordonner(_monitor.Read(now));

        // Réconciliation simple (liste courte) : on aligne Items sur snaps par SessionId.
        Items.Clear();
        foreach (var s in snaps)
        {
            var it = new SessionItemVm(s.SessionId, s.UpdatedAt.ToUnixTimeMilliseconds(),
                                       ArchiveSession, MarquerSessionTraitee, MarquerToutTraite)
            {
                Project = s.Project,
                ToutTraiterLibelle = $"Tout marquer traité ({snaps.Count}) — elles reviennent si elles redemandent",
            };
            (it.StateText, it.StateBrush, it.IsWaiting) = Describe(s.Activity);
            it.IsAttention = s.Activity == SessionActivity.WaitingAttention;
            // La déduction rejoint la famille VISUELLE des attentes. Un drapeau de gabarit décrit une
            // forme, il n'affirme rien : l'affirmation est dans StateText, et c'est lui qui porte
            // l'interrogation. La ranger parmi les fantômes l'estomperait jusqu'à 0,22 d'opacité dans deux
            // gabarits — une session qui m'attend peut-être ne doit pas être la plus effacée de l'écran.
            it.IsTurn = s.Activity is SessionActivity.WaitingTurn or SessionActivity.WaitingDeduced;
            it.IsWorking = s.Activity == SessionActivity.Working;
            it.IsGhost = s.Activity == SessionActivity.Unknown;
            it.Detail = AffichageSessions.Age(now - s.UpdatedAt);
            Items.Add(it);
        }

        TotalCount = snaps.Count;
        // Le compteur sert à ALERTER : taire une attente probable serait pire que l'annoncer avec un point
        // d'interrogation. La déduction y entre donc, et son libellé dit ce qu'elle vaut.
        WaitingCount = snaps.Count(s => s.Activity is SessionActivity.WaitingAttention
                                        or SessionActivity.WaitingTurn or SessionActivity.WaitingDeduced);
        HasWaiting = WaitingCount > 0;
        Summary = TotalCount == 0 ? "Aucune session"
            : (WaitingCount > 0 ? $"{WaitingCount} en attente · {TotalCount} session(s)" : $"{TotalCount} session(s)");
    }

    // Le LIBELLÉ vient de la couche neutre (partagé avec le rapport) ; seules la couleur et le drapeau
    // d'attente restent ici — ce sont des types WPF, ils ne peuvent pas en descendre.
    private (string, Brush, bool) Describe(SessionActivity a)
        => (AffichageSessions.Etat(a),
            a switch
            {
                // Les deux attentes OBSERVÉES et l'attente DÉDUITE → rampe ambre. Le gris reste au seul
                // inconnu : ce qu'on n'a pas pu lire s'efface, ce qu'on déduit se lit.
                SessionActivity.WaitingAttention or SessionActivity.WaitingTurn
                    or SessionActivity.WaitingDeduced => _amber,
                SessionActivity.Working => _green,
                _ => _gray,
            },
            a is SessionActivity.WaitingAttention or SessionActivity.WaitingTurn
                 or SessionActivity.WaitingDeduced);

    private static Brush FrozenC(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
