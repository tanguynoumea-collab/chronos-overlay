using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Chronos.Services;
using Chronos.Theming;
using Chronos.ViewModels;

namespace Chronos.Views;

public partial class MainWindow : Window
{
    private readonly TopmostGuard _topmostGuard;
    private readonly OverlayController _controller;
    private readonly MainViewModel _vm;

    // État persisté à restaurer AVANT le premier rendu (fourni par App avant Show).
    private ChronosSettings? _restored;

    private readonly IOuvreurReglages? _ouvreurReglages;

    // Vrai pendant DragMove : un changement de DPI en glissant ne doit pas déclencher de recalage.
    private bool _enDeplacement;

    // R7 : décision clic / double-clic / glisser (pure, testée — AutomateGeste).
    private readonly AutomateGeste _geste = new();

    // Lecture de la position souris relative à un élément. Couture de test UNIQUEMENT : MouseEventArgs.GetPosition lit le
    // vrai périphérique, qu'un test ne peut pas placer ; le test de câblage du répartiteur la remplace par un point témoin.
    internal Func<MouseEventArgs, IInputElement, Point> LirePosition { get; set; } = static (e, relatif) => e.GetPosition(relatif);

    public MainWindow(MainViewModel viewModel, TopmostGuard topmostGuard, OverlayController controller,
                      IOuvreurReglages? ouvreurReglages = null)
    {
        _ouvreurReglages = ouvreurReglages;
        // MVVM : la vue reçoit son VM par injection. Posé AVANT InitializeComponent (phase 40, CAD-02) : la liaison Width/Height
        // de la fenêtre à l'empreinte se résout dès le chargement du XAML, donc avant SourceInitialized → RestorePlacement.
        DataContext = viewModel;
        InitializeComponent();
        _vm = viewModel;

        // ACC-02 : le délai de double-clic de l'UTILISATEUR (réglage Windows) arbitre le clic sur le cadran ; 500 ms si illisible.
        var ms = Interop.NativeMethods.GetDoubleClickTime();
        viewModel.DefinirDelaiDoubleClic(System.TimeSpan.FromMilliseconds(ms > 0 ? ms : 500));
        _topmostGuard = topmostGuard;
        _controller = controller;

        // Thèmes : appliquer les pinceaux du thème persisté (ressources dynamiques) AVANT le 1er rendu,
        // puis suivre chaque changement émis par le VM (sélection dans la fenêtre de réglages).
        ApplyThemeBrushes(ThemeCatalog.ByKey(viewModel.SelectedThemeKey));
        viewModel.ThemeChanged += ApplyThemeBrushes;

        // HWND garanti ici : on attache le guard puis le controller, et on restaure le placement
        // AVANT le premier rendu (pas de flash), remplaçant l'ancien PlacerCoinSuperieurDroit (Loaded).
        SourceInitialized += (_, _) =>
        {
            _topmostGuard.Attach(this);
            _controller.Attach(this);
            if (_restored is not null) _controller.RestorePlacement(_restored);
        };

        // Pattern 3 : re-caler le coin après un franchissement de moniteur DPI mixte (taille physique change).
        DpiChanged += (_, _) => _controller.SnapToNearestCorner();

        // CAD-02 (phase 40) : l'empreinte a changé (style, orientation) → WPF a redimensionné le HWND en gardant le coin haut-gauche ;
        // on le repose sur le coin d'accroche COURANT. Ignoré à la première mise en page (RestorePlacement l'a fait) et pendant un glisser.
        SizeChanged += (_, e) =>
        {
            if (e.PreviousSize.Width <= 0 || e.PreviousSize.Height <= 0)
            {
                // Première mise en page : RestorePlacement a déjà posé la fenêtre… sauf s'il l'a fait sans taille connue → un seul recalage.
                if (_controller.RestaurationSansTaille) _controller.RecalerSurCoinCourant();
                return;
            }
            if (_enDeplacement) return;
            _controller.RecalerSurCoinCourant();
        };

        // Démarre l'horloge UI 1 s côté vue (RAF-03) : le DispatcherTimer est créé sur le thread UI,
        // jamais dans le ctor du VM (Pitfall 4).
        Loaded += (_, _) => viewModel.StartClock();
    }

    /// <summary>
    /// Fournit l'état persisté à restaurer. Appelé par App AVANT <see cref="Window.Show"/> :
    /// SourceInitialized appliquera <see cref="OverlayController.RestorePlacement"/> avant le 1er rendu.
    /// </summary>
    public void ApplyRestoredState(ChronosSettings settings) => _restored = settings;

    // ===================== GST-01 : répartiteur UNIQUE des gestes (grille Racine) =====================
    // Un seul répartiteur pour toute la silhouette : clic, double-clic et glisser partagent la même surface, la décision vit
    // dans AutomateGeste (pur) puis ArbitreClicCentre (inchangé). Filtre GÉOMÉTRIQUE avant d'armer : des pixels peints
    // existent hors silhouette (pastilles inertes, flèche de reset de Braises, mot « indisponible »), ils ne doivent pas
    // déclencher de geste. Pas d'abonnement « handled inclus » : les pastilles boutons marquent leur appui Handled et doivent le garder
    // (le geste ne se réveille pas sous elles).

    // Appui gauche : seul le MouseDown porte ClickCount. Double-clic → Historique tout de suite (sans bascule) ; sinon on
    // capture la souris pour suivre le MouseMove même hors de la fenêtre.
    private void Racine_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var silhouette = ZoneGeste.Trouver(Racine);
        if (silhouette is null || !ZoneGeste.Contient(silhouette, LirePosition(e, silhouette))) return;   // hors silhouette : rien
        var p = LirePosition(e, Racine);
        var action = _geste.Appui(p.X, p.Y, e.ClickCount);
        if (action.EstClic) _vm.ClicCentre(action.ClickCount);       // double-clic → Historique, sans bascule
        else Racine.CaptureMouse();                                  // on suit le MouseMove même hors de la fenêtre
        e.Handled = true;
    }

    // Déplacement : au-delà du seuil de glisser de Windows (SM_CXDRAG / SM_CYDRAG, en DIP), la fenêtre suit le curseur
    // (DragMove, boucle système bloquante) puis s'accroche au coin le plus proche AU RETOUR de DragMove.
    private void Racine_MouseMove(object sender, MouseEventArgs e)
    {
        var p = LirePosition(e, Racine);
        if (_geste.Deplacement(p.X, p.Y, SystemParameters.MinimumHorizontalDragDistance,
                               SystemParameters.MinimumVerticalDragDistance) != ActionGeste.CommencerGlisser) return;
        // L'automate est DÉJÀ en « Glisse » : le relâchement synthétique que DragMove envoie pendant son appel (réentrance) et
        // la perte de capture tombent sur un état qui les ignore — aucune bascule après un déplacement.
        Racine.ReleaseMouseCapture();
        _enDeplacement = true;                                       // CAD-02 : SizeChanged ignoré pendant le glisser
        try { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); }   // BLOQUE jusqu'au relâchement
        catch (InvalidOperationException) { }                                     // bouton déjà relâché : DragMove lève
        finally { _enDeplacement = false; _geste.Relache(); }
        _controller.SnapToNearestCorner();                                        // accroche AU RETOUR de DragMove
    }

    // Relâchement : e.ClickCount vaut 0 ici, on ne le lit jamais. Un appui non transformé en glisser devient un simple clic,
    // que l'arbitre ne bascule qu'à l'échéance du délai de double-clic.
    private void Racine_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var action = _geste.Relache();
        if (Racine.IsMouseCaptured) Racine.ReleaseMouseCapture();
        if (action.EstClic) _vm.ClicCentre(1);                       // simple clic → bascule à l'échéance de l'arbitre
    }

    // Perte de capture (Alt+Tab, fenêtre système…) : un appui en cours est annulé, sans clic.
    private void Racine_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _geste.PerteCapture();
    }

    // Clic DROIT : ouvre la fenêtre de réglages (quick 260927-reglages-v2), ou la RAMÈNE si elle est déjà ouverte — par l'ouvreur
    // singleton injecté. La vue ne fabrique plus de fenêtre et ne lui donne plus de propriétaire : les réglages sont une fenêtre classique,
    // indépendante du cadran topmost (DESIGN_PLAN_REGLAGES §4), placée à sa géométrie mémorisée ou centrée à l'écran.
    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        _ouvreurReglages?.Ouvrir();
        e.Handled = true;
    }

    // Applique les pinceaux d'un thème dans les ressources de la fenêtre → les DynamicResource du cadran
    // (disque, pistes, graduations, textes) se mettent à jour instantanément.
    private void ApplyThemeBrushes(ChronosTheme theme)
    {
        foreach (var kv in theme.BrushTokens())
            Resources[kv.Key] = kv.Value;
    }
}
