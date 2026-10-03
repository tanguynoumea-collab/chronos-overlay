using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Chronos.Interop;
using Chronos.Placement;
using Chronos.Theming;
using Chronos.ViewModels;
using Chronos.Views.Historique;
using CommunityToolkit.Mvvm.Input;

namespace Chronos.Views.Reglages;

/// <summary>
/// Quick 260927-reglages-v2 — la fenêtre de réglages, fenêtre CLASSIQUE (DESIGN_PLAN_REGLAGES §4) : rectangulaire, redimensionnable
/// par les bords et la poignée, agrandissable, visible dans la barre des tâches, indépendante du cadran (aucun <c>Owner</c>, pas
/// de <c>Topmost</c>) et qui ne se ferme plus quand elle perd le focus. Ouverte par <see cref="OuvreurReglages"/> (singleton).
///
/// <para>Aucune logique métier ici : le DataContext est le <see cref="MainViewModel"/> PARTAGÉ avec le cadran, l'état propre à la
/// fenêtre (section, géométrie, diagnostic) est dans <see cref="ReglagesViewModel"/>. Le code-behind ne fait que : poser le
/// DataContext, injecter les pinceaux du thème actif (et les suivre), brancher l'aperçu vivant du cadran, restaurer / persister la
/// géométrie (bornée, écrite en <c>Normal</c> seulement), les boutons ─ □ ✕ et Échap, les coins DWM et l'agrandissement borné à
/// la zone de travail du moniteur.</para>
/// </summary>
public partial class ReglagesWindow : Window
{
    private readonly MainViewModel _vm;

    /// <param name="vm">Le VM partagé avec le cadran.</param>
    /// <param name="cadranReel">Le contenu visuel du cadran (MainWindow) : l'aperçu de la section Apparence le peint EN DIRECT —
    /// c'est le vrai rendu, avec le thème, le style et les chiffres du moment. <c>null</c> (tests) : l'aperçu le dit.</param>
    public ReglagesWindow(MainViewModel vm, Visual? cadranReel = null)
    {
        InitializeComponent();
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));

        // Coin des deux barres de défilement du rapport : le gabarit système le peint avec la couleur de contrôle (gris clair) ;
        // ici, au fond Panel. Posé en code : un <StaticResource x:Key="{x:Static …}"/> ne passe pas le chargement BAML différé.
        Resources[SystemColors.ControlBrushKey] = FindResource("Panel");
        DataContext = vm;

        // Pinceaux du thème actif (Alerte de la carte F1, Sess* de l'aperçu du widget) → entrées LOCALES de la fenêtre, qui priment
        // sur le repli statique du dictionnaire fusionné (34-08) ; suivies à chaque changement de thème.
        AppliquerTheme(ThemeCatalog.ByKey(vm.SelectedThemeKey));
        vm.ThemeChanged += AppliquerTheme;

        PoserApercuCadran(cadranReel);

        vm.Reglages.Ouvrir();   // dernière section relue ; diagnostic lancé si l'on rouvre dessus sans rapport
        vm.Reglages.PropertyChanged += SurReglagesChange;

        RestaurerGeometrie();

        // Échap ferme (DESIGN_PLAN §4). ✕ aussi ; ni l'un ni l'autre ne passe par le VM : fermer est l'affaire de la fenêtre.
        InputBindings.Add(new KeyBinding(new RelayCommand(Close), Key.Escape, ModifierKeys.None));

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HistoriqueWindow.ArrondirCoinsDwm(hwnd);
            HwndSource.FromHwnd(hwnd)?.AddHook(CrochetAgrandissement);
        };
        Loaded += (_, _) => FocaliserSectionCourante();
        Closing += (_, _) => EnregistrerGeometrie();
        Closed += (_, _) =>
        {
            _vm.ThemeChanged -= AppliquerTheme;
            _vm.Reglages.PropertyChanged -= SurReglagesChange;
        };
    }

    private void AppliquerTheme(ChronosTheme theme)
    {
        foreach (var kv in theme.BrushTokens()) Resources[kv.Key] = kv.Value;
        foreach (var kv in theme.SessionBrushTokens()) Resources[kv.Key] = kv.Value;
    }

    /// <summary>L'aperçu de la section Apparence : un pinceau visuel sur le contenu RÉEL du cadran (mis à jour par WPF à chaque
    /// rendu du cadran — thème, style, chiffres). Sans cadran (tests, galerie), une phrase le dit au lieu d'un vide.</summary>
    private void PoserApercuCadran(Visual? cadranReel)
    {
        if (cadranReel is null)
        {
            ApercuCadran.Fill = null;
            ApercuCadranAbsent.Visibility = Visibility.Visible;
            return;
        }

        var brosse = new VisualBrush(cadranReel)
        {
            Stretch = Stretch.Uniform,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center,
        };
        ApercuCadran.Fill = brosse;
        ApercuCadranAbsent.Visibility = Visibility.Collapsed;

        // Cadrage sur l'EMPREINTE du cadran, pas sur la boîte englobante de ce qu'il dessine : celle-ci est asymétrique
        // (graduations, pastilles en bas à droite) et changerait dès qu'une pastille apparaît — l'aperçu se décentrerait et sauterait.
        // La bande des pastilles des cadrans rectangulaires (§11 B1), sous l'empreinte, est exclue : l'aperçu ne saute pas quand
        // une pastille apparaît.
        if (cadranReel is FrameworkElement empreinte)
        {
            void Cadrer()
            {
                if (empreinte.ActualWidth <= 0 || empreinte.ActualHeight <= 0) return;
                brosse.ViewboxUnits = BrushMappingMode.Absolute;
                brosse.Viewbox = new Rect(0, 0, empreinte.ActualWidth, Math.Max(0, empreinte.ActualHeight - _vm.HauteurBandePastilles));
            }
            SizeChangedEventHandler surTaille = (_, _) => Cadrer();
            Cadrer();
            empreinte.SizeChanged += surTaille;
            Closed += (_, _) => empreinte.SizeChanged -= surTaille;
        }
    }

    /// <summary>Changement de section : la colonne repart du haut (on ne tombe pas au milieu de la section suivante).</summary>
    private void SurReglagesChange(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReglagesViewModel.Section)) DefilementContenu.ScrollToTop();
    }

    /// <summary>Le focus clavier sur l'entrée sélectionnée du rail : ↑ / ↓ fonctionnent dès l'ouverture.</summary>
    private void FocaliserSectionCourante()
    {
        if (ListeSections.ItemContainerGenerator.ContainerFromItem(ListeSections.SelectedItem) is ListBoxItem entree)
            entree.Focus();
    }

    // ------------------------------------------------------------------ Barre de titre

    private void Reduire_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Agrandir_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Fermer_Click(object sender, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------------ Géométrie (motif HistoriqueWindow)

    /// <summary>Relit la géométrie persistée, la borne à l'écran virtuel et aux minima (un moniteur débranché ne cache jamais la
    /// fenêtre) ; sinon défaut 860 × 580 centré.</summary>
    private void RestaurerGeometrie()
    {
        var ecran = new RectangleEcran(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                                       SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (PlacementHistorique.Borner(_vm.Reglages.GeometriePersistee(), ecran, MinWidth, MinHeight) is { } g)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = g.X;
            Top = g.Y;
            Width = g.Largeur;
            Height = g.Hauteur;
        }
    }

    /// <summary>Mémorise position et taille — en état normal seulement (une fenêtre agrandie ou réduite n'écrase pas la géométrie
    /// « normale »). Appelée par <c>Closing</c> ; <c>internal</c> pour les tests.</summary>
    internal void EnregistrerGeometrie()
    {
        if (WindowState != WindowState.Normal) return;
        var largeur = ActualWidth > 0 ? ActualWidth : Width;
        var hauteur = ActualHeight > 0 ? ActualHeight : Height;
        _vm.Reglages.EnregistrerGeometrie(Left, Top, largeur, hauteur);
    }

    // ------------------------------------------------------------------ Agrandissement borné à la zone de travail

    /// <summary>
    /// Sans barre de titre système (<c>WindowStyle=None</c>), Windows agrandit la fenêtre à la taille du MONITEUR, barre des tâches
    /// comprise. On répond à <c>WM_GETMINMAXINFO</c> avec la zone de travail du moniteur courant (coordonnées relatives au moniteur,
    /// en pixels physiques). <c>handled</c> reste faux : WPF applique ensuite ses propres MinWidth / MinHeight au même message.
    /// </summary>
    private static IntPtr CrochetAgrandissement(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_GETMINMAXINFO) BornerAgrandissement(hwnd, lParam);
        return IntPtr.Zero;
    }

    private static void BornerAgrandissement(IntPtr hwnd, IntPtr lParam)
    {
        var moniteur = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (moniteur == IntPtr.Zero) return;
        var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        if (!NativeMethods.GetMonitorInfo(moniteur, ref mi)) return;

        var mmi = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lParam);
        mmi.ptMaxPosition.X = mi.rcWork.Left - mi.rcMonitor.Left;
        mmi.ptMaxPosition.Y = mi.rcWork.Top - mi.rcMonitor.Top;
        mmi.ptMaxSize.X = mi.rcWork.Right - mi.rcWork.Left;
        mmi.ptMaxSize.Y = mi.rcWork.Bottom - mi.rcWork.Top;
        Marshal.StructureToPtr(mmi, lParam, false);
    }
}
