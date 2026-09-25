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

/// <summary>Une session dans la liste : nom (titre ou dossier), libellé d'état, info-bulle, couleur, détail
/// temporel, et les trois gestes du menu contextuel (traiter celle-ci, tout traiter, archiver).</summary>
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

    /// <summary>Le NOM affiché : le titre de l'app bureau s'il est connu, sinon le dossier (APP-02) — tiré de
    /// <see cref="AffichageSessions.Nom"/>. Le nom de propriété reste <c>Project</c> : huit gabarits le lient.</summary>
    [ObservableProperty] private string _project = "";
    [ObservableProperty] private string _stateText = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private Brush _stateBrush = Brushes.Gray;
    [ObservableProperty] private bool _isWaiting;

    // États d'activité distincts, exposés pour les templates de la refonte visuelle : des FORMES et des
    // rythmes seulement. Le mot est dans StateText, tiré du producteur unique (AffichageSessions).
    [ObservableProperty] private bool _isAttention;  // WaitingAttention : permission ou question (respire)
    [ObservableProperty] private bool _isTurn;       // WaitingTurn, et la déduction : famille des attentes (fixe)
    [ObservableProperty] private bool _isWorking;    // Working : la session travaille
    [ObservableProperty] private bool _isDeduced;    // WaitingDeduced → « En attente ? » : atténuée (0,7), jamais effacée

    /// <summary>L'info-bulle des huit gabarits, POSÉE par le producteur unique (<see cref="AffichageSessions.Infobulle"/>) —
    /// titre, dossier, mot, et le motif d'une attente observée — jamais composée ici. Six gabarits sur huit ne montrent
    /// l'état que par forme et couleur : c'est là que leur mot se lit, et le dossier qu'un titre remplace à l'écran.</summary>
    [ObservableProperty] private string _infobulle = "";

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
/// Liste temps réel des sessions Claude Code (source : <see cref="SessionMonitor"/>). L'ordre est celui du
/// producteur unique (<see cref="AffichageSessions.Ordonner"/>) : les attentes d'abord, puis la fraîcheur.
/// L'état indéterminé n'a pas de ligne : le moniteur le masque, avec un motif nommé que le rapport de
/// diagnostic affiche. Rafraîchi par un DispatcherTimer créé côté UI (jamais dans le ctor — Pitfall threading).
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

    /// <summary>Mot du compteur de l'Annonciateur, tiré du producteur : jamais en dur dans un gabarit.</summary>
    public string LibelleCompteur => AffichageSessions.EnAttente;

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
    // valeurs de départ = thème par défaut. Les trois attentes → rampe ambre, « Réflexion » → rampe verte,
    // gris défensif (l'état indéterminé n'a pas de ligne : le moniteur le masque).
    private ChronosTheme _theme = ThemeCatalog.Default;
    private Brush _amber = FrozenC(ThemeCatalog.Default.RampAmber);   // « En attente » et « En attente ? »
    private Brush _green = FrozenC(ThemeCatalog.Default.RampGreen);   // « Réflexion »
    private Brush _gray = FrozenC(ThemeCatalog.Default.TexteSecondaire); // défensif : jamais à l'écran

    /// <summary>Applique un thème : recolore les états (attentes / réflexion) selon la rampe et re-rend.
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
                Project = AffichageSessions.Nom(s),
                Infobulle = AffichageSessions.Infobulle(s),
                ToutTraiterLibelle = $"Tout marquer traité ({snaps.Count}) — elles reviennent si elles redemandent",
            };
            (it.StateText, it.StateBrush) = Describe(s.Activity);
            // Le drapeau d'attente LIT le prédicat unique du producteur ; il ne le recopie plus.
            it.IsWaiting = AffichageSessions.EstUneAttente(s.Activity);
            it.IsAttention = s.Activity == SessionActivity.WaitingAttention;
            // La déduction rejoint la famille VISUELLE des attentes (IsTurn), et porte en plus son propre
            // drapeau (IsDeduced) : les gabarits l'atténuent sans jamais l'effacer. Un drapeau de gabarit
            // décrit une forme, il n'affirme rien : l'affirmation est dans StateText, et c'est lui qui porte
            // le point d'interrogation.
            it.IsTurn = s.Activity is SessionActivity.WaitingTurn or SessionActivity.WaitingDeduced;
            it.IsWorking = s.Activity == SessionActivity.Working;
            it.IsDeduced = s.Activity == SessionActivity.WaitingDeduced;
            it.Detail = AffichageSessions.Age(now - s.UpdatedAt);
            Items.Add(it);
        }

        TotalCount = snaps.Count;
        // Le compteur sert à ALERTER : taire une attente probable serait pire que l'annoncer avec un point
        // d'interrogation. La déduction y entre donc — c'est le prédicat unique qui le dit — et son mot dit
        // ce qu'elle vaut.
        WaitingCount = snaps.Count(s => AffichageSessions.EstUneAttente(s.Activity));
        HasWaiting = WaitingCount > 0;
        Summary = TotalCount == 0 ? "Aucune session"
            : (WaitingCount > 0 ? $"{WaitingCount} en attente · {TotalCount} session(s)" : $"{TotalCount} session(s)");
    }

    // Le MOT vient du producteur unique (partagé avec le rapport) ; seule la couleur reste ici — c'est un
    // type WPF, il ne peut pas en descendre.
    private (string, Brush) Describe(SessionActivity a)
        => (AffichageSessions.Etat(a),
            AffichageSessions.EstUneAttente(a) ? _amber          // les trois attentes : rampe ambre
            : a == SessionActivity.Working ? _green               // « Réflexion » : rampe verte
            : _gray);                                             // défensif : l'indéterminé est masqué par le moniteur

    private static Brush FrozenC(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
