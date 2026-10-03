using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Chronos.Interop;
using Chronos.Placement;
using Chronos.ViewModels.Historique;

namespace Chronos.Views.Historique;

/// <summary>
/// HIS-01 — la fenêtre Historique : une COQUILLE opaque, redimensionnable, mémorisée (DESIGN_PLAN §2).
///
/// <para>D-34-22 : fenêtre de consultation sans transparence ; bords de redimensionnement et ombre par <c>WindowChrome</c> (XAML),
/// coins arrondis DWM best-effort posés ici dans <c>SourceInitialized</c> (Windows 11 ; Windows 10 garde des coins droits, sans
/// exception). D-34-23 : pas de fenêtre propriétaire, jamais « toujours au premier plan », pas de fermeture à la désactivation — fenêtre
/// indépendante, rappelée par les gestes de la phase 35.</para>
///
/// <para>Aucune logique métier ici : tout est dans <see cref="HistoriqueViewModel"/>. Le code-behind ne fait que : poser le
/// DataContext, injecter les pinceaux du thème (fenêtre et vues hébergées), restaurer / persister la géométrie (bornée par <see cref="PlacementHistorique"/>,
/// écrite en <c>Normal</c> seulement), le drag de l'en-tête, la fermeture demandée par le VM, l'horloge du VM dans <c>Loaded</c>.</para>
///
/// <para>Phase 38 (HIS-10 / HIS-11) : il observe aussi <see cref="HistoriqueViewModel.EstPleinEcran"/> et applique le plein écran
/// décidé par le VM — bornes du moniteur courant en DIP (rcMonitor : barre des tâches couverte, jamais l'état maximisé), géométrie
/// d'avant mémorisée puis restaurée exactement, redimensionnement et déplacement bloqués, coins DWM carrés, et dictionnaire
/// <see cref="DictionnairePleinEcran"/> fusionné en dernier dans la fenêtre ET les trois vues. Rien de tout cela n'est persisté.</para>
/// </summary>
public partial class HistoriqueWindow : Window
{
    private readonly HistoriqueViewModel _vm;

    /// <summary>Nombre de <c>FermetureDemandee</c> reçues (observable par les tests : une fenêtre jamais montrée ne peut pas se fermer).</summary>
    internal int DemandesDeFermeture { get; private set; }

    /// <summary>Géométrie du mode normal mémorisée à l'entrée en plein écran ; <c>null</c> hors plein écran.</summary>
    private (double X, double Y, double Largeur, double Hauteur)? _avantPleinEcran;

    /// <summary>Dictionnaires PleinEcran ajoutés (propriétaire, instance) : retirés exactement à la sortie.</summary>
    private readonly List<(ResourceDictionary Proprietaire, ResourceDictionary Ajoute)> _dictionnairesPleinEcran = new();

    /// <summary>Bornes du moniteur courant en DIP, ou <c>null</c> si illisibles. Seam de test : une fenêtre jamais montrée n'a pas de
    /// handle, les tests injectent des bornes.</summary>
    internal Func<RectangleEcran?> FournisseurBornesMoniteur { get; set; }

    /// <summary>Vrai tant que la géométrie plein écran est appliquée.</summary>
    internal bool EstEnPleinEcran => _avantPleinEcran is not null;

    public HistoriqueWindow(HistoriqueViewModel vm)
    {
        InitializeComponent();
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        FournisseurBornesMoniteur = BornesMoniteurCourantDip;
        // Le VM singleton survit à la fenêtre (piège 4, D-35-05) : une fenêtre rouvre toujours en mode normal. Remis à faux AVANT
        // tout abonnement, pour ne rien appliquer à une fenêtre en construction.
        vm.EstPleinEcran = false;
        DataContext = vm;
        // Le VM est un singleton né au démarrage : relire le thème ACTIF avant d'injecter les pinceaux (piège 3, D-35-05).
        vm.ActualiserTheme();

        // Pinceaux du thème actif (Alerte, …) → résout les DynamicResource de la fenêtre (motif MainWindow) ET des deux vues : chaque vue
        // fusionne DesignTokens.xaml (elle se monte seule en test), dont le repli statique « Alerte » serait trouvé AVANT celui de la
        // fenêtre par un DynamicResource posé dans la vue. Une entrée locale de la vue prime sur son dictionnaire fusionné (34-08).
        foreach (var kv in vm.Theme.BrushTokens())
        {
            Resources[kv.Key] = kv.Value;
            VueSemaine.Resources[kv.Key] = kv.Value;
            VueJour.Resources[kv.Key] = kv.Value;
            VueQuatreSemaines.Resources[kv.Key] = kv.Value;
        }

        RestaurerGeometrie();

        // Échap (hors plein écran) et ✕ passent par la fermeture du VM → FermetureDemandee ; handlers NOMMÉS, retirés au Closed : le VM
        // singleton ne retient aucune fenêtre fermée (piège 4, D-35-05).
        vm.FermetureDemandee += SurFermetureDemandee;
        vm.PropertyChanged += SurVmPropertyChanged;
        SourceInitialized += (_, _) => ArrondirCoinsDwm(new WindowInteropHelper(this).Handle);
        Loaded += (_, _) =>
        {
            vm.Ouvrir();            // repère hebdo, plage au présent, première lecture hors UI, bandeau F2
            vm.DemarrerHorloge();   // tick 60 s — jamais dans le constructeur du VM
        };
        Closing += (_, _) => EnregistrerGeometrie();
        Closed += (_, _) =>
        {
            _vm.FermetureDemandee -= SurFermetureDemandee;
            _vm.PropertyChanged -= SurVmPropertyChanged;
            _vm.EstPleinEcran = false;   // après le désabonnement : la fenêtre fermée n'est plus touchée
            _vm.ArreterHorloge();
        };
    }

    /// <summary>Fermeture demandée par le VM (Échap, ✕) : comptée ; <c>Close()</c> n'a de sens que sur une fenêtre chargée.</summary>
    private void SurFermetureDemandee(object? sender, EventArgs e)
    {
        DemandesDeFermeture++;
        if (IsLoaded) Close();
    }

    /// <summary>Relit la géométrie persistée, la borne à l'écran virtuel et aux minima (Pitfall 2) ; sinon défaut centré.</summary>
    private void RestaurerGeometrie()
    {
        var ecran = new RectangleEcran(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                                       SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (PlacementHistorique.Borner(_vm.GeometriePersistee(), ecran, MinWidth, MinHeight) is { } g)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = g.X;
            Top = g.Y;
            Width = g.Largeur;
            Height = g.Hauteur;
        }
    }

    /// <summary>Mémorise position et taille — en état normal seulement (une fenêtre maximisée ou réduite ne doit pas écraser la
    /// géométrie « normale » mémorisée). En plein écran, c'est la géométrie d'avant qui est écrite. Appelée par <c>Closing</c> ;
    /// <c>internal</c> pour les tests.</summary>
    internal void EnregistrerGeometrie()
    {
        // Pitfall 8 : pendant le plein écran, la géométrie persistée reste celle du mode normal.
        if (_avantPleinEcran is { } g) { _vm.EnregistrerGeometrie(g.X, g.Y, g.Largeur, g.Hauteur); return; }
        if (WindowState != WindowState.Normal) return;
        var largeur = ActualWidth > 0 ? ActualWidth : Width;
        var hauteur = ActualHeight > 0 ? ActualHeight : Height;
        _vm.EnregistrerGeometrie(Left, Top, largeur, hauteur);
    }

    /// <summary>
    /// Coins arrondis + ombre DWM (D-34-22) : <c>DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND</c>. Rend <c>true</c> si DWM a accepté
    /// (Windows 11), <c>false</c> sinon — Windows 10 rend un HRESULT d'échec, un handle nul aussi ; aucune exception ne sort d'ici.
    /// </summary>
    internal static bool ArrondirCoinsDwm(IntPtr hwnd) => PreferenceCoinsDwm(hwnd, NativeMethods.DWMWCP_ROUND);

    /// <summary>Pose la préférence de coins DWM (<c>DWMWCP_ROUND</c>, ou <c>DWMWCP_DONOTROUND</c> en plein écran). Best-effort :
    /// <c>false</c> sur Windows 10, avec un handle nul ou sans dwmapi ; aucune exception ne sort d'ici.</summary>
    internal static bool PreferenceCoinsDwm(IntPtr hwnd, int preference)
    {
        try
        {
            return NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int)) == 0;
        }
        catch (Exception)
        {
            return false;   // dwmapi absent ou point d'entrée introuvable : coins droits, dit dans le SUMMARY, pas masqué
        }
    }

    /// <summary>Drag sur l'en-tête (caption 0 dans le WindowChrome). <c>DragMove</c> hors d'un vrai clic lève : on l'ignore.</summary>
    private void EnTete_Drag(object sender, MouseButtonEventArgs e)
    {
        if (_vm.EstPleinEcran) return;   // Pitfall 9 : aucun déplacement en plein écran
        if (e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); }
        catch (InvalidOperationException) { }
    }

    // ================= Plein écran (HIS-10 / HIS-11) =================

    /// <summary>Applique l'état plein écran décidé par le VM ; toute autre propriété est ignorée.</summary>
    private void SurVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HistoriqueViewModel.EstPleinEcran)) return;
        if (_vm.EstPleinEcran) EntrerPleinEcran(); else QuitterPleinEcran();
    }

    /// <summary>
    /// Pose la fenêtre sur les bornes du moniteur courant, à la main (pas d'état maximisé : une fenêtre active au rectangle exact du
    /// moniteur fait passer la barre des tâches dessous). Idempotent ; sans moniteur lisible, la bascule est annulée sans exception.
    /// </summary>
    internal void EntrerPleinEcran()
    {
        if (_avantPleinEcran is not null) return;
        if (FournisseurBornesMoniteur() is not { } bornes)
        {
            _vm.EstPleinEcran = false;   // bascule annulée : le bouton reste « ⛶ Plein écran »
            return;
        }

        if (WindowState != WindowState.Normal)
        {
            var r = RestoreBounds;
            _avantPleinEcran = r.IsEmpty ? (Left, Top, Width, Height) : (r.X, r.Y, r.Width, r.Height);
            WindowState = WindowState.Normal;
        }
        else
        {
            _avantPleinEcran = (Left, Top, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
        }

        ResizeMode = ResizeMode.NoResize;
        // Position PUIS taille.
        Left = bornes.Gauche;
        Top = bornes.Haut;
        Width = bornes.Largeur;
        Height = bornes.Hauteur;
        PreferenceCoinsDwm(new WindowInteropHelper(this).Handle, NativeMethods.DWMWCP_DONOTROUND);
        AjouterDictionnaires();
    }

    /// <summary>Retire le dictionnaire puis restaure exactement la géométrie d'avant ; sans effet hors plein écran.</summary>
    internal void QuitterPleinEcran()
    {
        if (_avantPleinEcran is not { } g) return;
        RetirerDictionnaires();
        // Taille PUIS position : symétrique de l'entrée.
        Width = g.Largeur;
        Height = g.Hauteur;
        Left = g.X;
        Top = g.Y;
        ResizeMode = ResizeMode.CanResize;
        PreferenceCoinsDwm(new WindowInteropHelper(this).Handle, NativeMethods.DWMWCP_ROUND);
        _avantPleinEcran = null;
    }

    /// <summary>
    /// Une instance NEUVE du dictionnaire PleinEcran ajoutée EN DERNIER à la fenêtre et à chacune des trois vues : le dernier fusionné
    /// gagne, et chaque vue fusionne ses propres tokens, qui masqueraient un dictionnaire posé sur la seule fenêtre (Pitfall 2). Les
    /// tokens sont lus dans les ressources de la fenêtre (l'indexeur cherche dans son DesignTokens.xaml fusionné).
    /// </summary>
    private void AjouterDictionnaires()
    {
        // Les quatre instances sont construites AVANT toute fusion : une fois la première fusionnée dans la fenêtre, l'indexeur de
        // Resources y trouverait ses hauteurs NaN (le dernier fusionné gagne) au lieu des tokens normaux.
        var fenetre = DictionnairePleinEcran.Construire(Resources);
        var semaine = DictionnairePleinEcran.Construire(Resources);
        var jour = DictionnairePleinEcran.Construire(Resources);
        var quatreSemaines = DictionnairePleinEcran.Construire(Resources);

        Resources.MergedDictionaries.Add(fenetre);
        _dictionnairesPleinEcran.Add((Resources, fenetre));
        VueSemaine.Resources.MergedDictionaries.Add(semaine);
        _dictionnairesPleinEcran.Add((VueSemaine.Resources, semaine));
        VueJour.Resources.MergedDictionaries.Add(jour);
        _dictionnairesPleinEcran.Add((VueJour.Resources, jour));
        VueQuatreSemaines.Resources.MergedDictionaries.Add(quatreSemaines);
        _dictionnairesPleinEcran.Add((VueQuatreSemaines.Resources, quatreSemaines));
    }

    /// <summary>Retire exactement les instances ajoutées par <see cref="AjouterDictionnaires"/>.</summary>
    private void RetirerDictionnaires()
    {
        foreach (var (proprietaire, ajoute) in _dictionnairesPleinEcran)
            proprietaire.MergedDictionaries.Remove(ajoute);
        _dictionnairesPleinEcran.Clear();
    }

    /// <summary>
    /// Bornes du moniteur le plus proche de la fenêtre, converties en DIP par la matrice de la fenêtre. rcMonitor, pas la zone de
    /// travail : la barre des tâches est couverte. <c>null</c> si le handle est nul ou le moniteur illisible. Pas de recalcul sur
    /// <c>DpiChanged</c> pendant le plein écran (consigné en 38-06).
    /// </summary>
    private RectangleEcran? BornesMoniteurCourantDip()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return null;
        var moniteur = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (moniteur == IntPtr.Zero) return null;
        var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        if (!NativeMethods.GetMonitorInfo(moniteur, ref mi)) return null;
        var m = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return PleinEcranHistorique.BornesDip(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom, m.M11, m.M22);
    }
}
