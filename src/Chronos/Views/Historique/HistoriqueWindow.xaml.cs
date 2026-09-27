using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Chronos.Interop;
using Chronos.Placement;
using Chronos.ViewModels.Historique;

namespace Chronos.Views.Historique;

/// <summary>
/// HIS-01 — la fenêtre Historique : une COQUILLE opaque, redimensionnable, mémorisée (DESIGN_PLAN §2).
///
/// <para>D-34-22 : fenêtre de consultation sans transparence ; bords de redimensionnement et ombre par <c>WindowChrome</c> (XAML),
/// coins arrondis DWM best-effort posés ici dans <c>SourceInitialized</c> (Windows 11 ; Windows 10 garde des coins droits, sans
/// exception). D-34-23 : pas de fenêtre propriétaire, pas de <c>Topmost</c>, pas de fermeture à la désactivation — fenêtre
/// indépendante, rappelée par les gestes de la phase 35.</para>
///
/// <para>Aucune logique métier ici : tout est dans <see cref="HistoriqueViewModel"/>. Le code-behind ne fait que : poser le
/// DataContext, injecter les pinceaux du thème (fenêtre et vues hébergées), restaurer / persister la géométrie (bornée par <see cref="PlacementHistorique"/>,
/// écrite en <c>Normal</c> seulement), le drag de l'en-tête, la fermeture demandée par le VM, l'horloge du VM dans <c>Loaded</c>.</para>
/// </summary>
public partial class HistoriqueWindow : Window
{
    private readonly HistoriqueViewModel _vm;

    /// <summary>Nombre de <c>FermetureDemandee</c> reçues (observable par les tests : une fenêtre jamais montrée ne peut pas se fermer).</summary>
    internal int DemandesDeFermeture { get; private set; }

    public HistoriqueWindow(HistoriqueViewModel vm)
    {
        InitializeComponent();
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        DataContext = vm;

        // Pinceaux du thème actif (Alerte, …) → résout les DynamicResource de la fenêtre (motif MainWindow) ET des deux vues : chaque vue
        // fusionne DesignTokens.xaml (elle se monte seule en test), dont le repli statique « Alerte » serait trouvé AVANT celui de la
        // fenêtre par un DynamicResource posé dans la vue. Une entrée locale de la vue prime sur son dictionnaire fusionné (34-08).
        foreach (var kv in vm.Theme.BrushTokens())
        {
            Resources[kv.Key] = kv.Value;
            VueSemaine.Resources[kv.Key] = kv.Value;
            VueJour.Resources[kv.Key] = kv.Value;
        }

        RestaurerGeometrie();

        // Échap et ✕ passent par FermerCommand → le VM lève FermetureDemandee ; Close() n'a de sens que sur une fenêtre chargée.
        vm.FermetureDemandee += (_, _) =>
        {
            DemandesDeFermeture++;
            if (IsLoaded) Close();
        };
        SourceInitialized += (_, _) => ArrondirCoinsDwm(new WindowInteropHelper(this).Handle);
        Loaded += (_, _) =>
        {
            vm.Ouvrir();            // repère hebdo, plage au présent, première lecture hors UI, bandeau F2
            vm.DemarrerHorloge();   // tick 60 s — jamais dans le constructeur du VM
        };
        Closing += (_, _) => EnregistrerGeometrie();
        Closed += (_, _) => vm.ArreterHorloge();
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
    /// géométrie « normale » mémorisée). Appelée par <c>Closing</c> ; <c>internal</c> pour les tests.</summary>
    internal void EnregistrerGeometrie()
    {
        if (WindowState != WindowState.Normal) return;
        var largeur = ActualWidth > 0 ? ActualWidth : Width;
        var hauteur = ActualHeight > 0 ? ActualHeight : Height;
        _vm.EnregistrerGeometrie(Left, Top, largeur, hauteur);
    }

    /// <summary>
    /// Coins arrondis + ombre DWM (D-34-22) : <c>DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND</c>. Rend <c>true</c> si DWM a accepté
    /// (Windows 11), <c>false</c> sinon — Windows 10 rend un HRESULT d'échec, un handle nul aussi ; aucune exception ne sort d'ici.
    /// </summary>
    internal static bool ArrondirCoinsDwm(IntPtr hwnd)
    {
        try
        {
            int preference = NativeMethods.DWMWCP_ROUND;
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
        if (e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); }
        catch (InvalidOperationException) { }
    }
}
