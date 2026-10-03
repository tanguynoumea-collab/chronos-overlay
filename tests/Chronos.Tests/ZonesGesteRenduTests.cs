using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Chronos.Models;
using Chronos.Rendering;
using Chronos.Services;
using Chronos.ViewModels;
using Chronos.Views;
using Xunit;
using Path = System.IO.Path;
using WindowState = Chronos.Models.WindowState; // lève l'ambiguïté avec System.Windows.WindowState

namespace Chronos.Tests;

/// <summary>
/// GST-02 / GST-03 (phase 42, plan 02) — chaque variante de cadran déclare UNE silhouette de geste, et le RENDU prouve
/// qu'elle capte la souris partout dans la forme et nulle part ailleurs.
///
/// <para><b>Pourquoi lire l'alpha.</b> Sur une fenêtre <c>AllowsTransparency</c> (fenêtre layered), c'est Windows qui
/// décide à quelle fenêtre livrer un clic, et son seul critère est l'alpha du pixel : alpha 0, le clic passe au bureau ;
/// alpha &gt; 0, il arrive à Chronos. Le <c>HitTest</c> de WPF ne prouverait rien : il dit « touché » sur un pinceau
/// <c>Transparent</c> (alpha 0), exactement le piège des anciens commentaires. On rend donc la grille racine en
/// <see cref="RenderTargetBitmap"/> et on lit l'octet d'alpha aux points témoins.</para>
///
/// <para><b>Pourquoi mettre en page le <c>Content</c> et non la <c>Window</c>.</b> Une fenêtre jamais affichée n'applique
/// pas son gabarit : son contenu n'a pas de parent visuel, le DataContext ne se propage pas et les liaisons ne s'évaluent
/// pas. On pose le DataContext sur la grille racine, on purge la file du Dispatcher, puis on mesure et arrange CETTE grille
/// à l'empreinte exacte du style (Pitfall 2).</para>
///
/// <para>Les points témoins sont GÉNÉRÉS depuis le contrat (disque r 74 centré, rectangle arrondi r 10 à l'empreinte
/// <see cref="EmpreinteCadran.Pour"/>), jamais lus dans le XAML, et s'auto-contrôlent.</para>
/// </summary>
[Collection("XAML WPF")]
public class ZonesGesteRenduTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private static readonly (CadranStyle Style, OrientationCadran Orientation)[] Variantes =
    {
        (CadranStyle.Arcs, OrientationCadran.Horizontal),
        (CadranStyle.Braises, OrientationCadran.Horizontal),
        (CadranStyle.Fusible, OrientationCadran.Horizontal),
        (CadranStyle.Fusible, OrientationCadran.Vertical),
        (CadranStyle.Maree, OrientationCadran.Vertical),
        (CadranStyle.Maree, OrientationCadran.Horizontal),
        (CadranStyle.Volets, OrientationCadran.Horizontal),
        (CadranStyle.Volets, OrientationCadran.Vertical),
    };

    private const double RayonDisque = 74;
    private const double RayonCongé = 10;

    // ================================ Points témoins ================================

    /// <summary>Témoins du disque 170 × 170 centré (85,85), rayon 74. Distances mesurées au centre du pixel (+0,5).</summary>
    internal static (IReadOnlyList<(int X, int Y)> Dedans, IReadOnlyList<(int X, int Y)> Dehors) TemoinsDisque()
    {
        var dedans = new List<(int X, int Y)>
        {
            (85, 85), (15, 85), (155, 85), (85, 15), (85, 155),
            (35, 35), (134, 35), (35, 134), (134, 134),
            (85, 40), // r 45 : entre l'anneau hebdo (R38) et l'anneau normal (R52) — l'entre-anneaux doit capter
        };
        var dehors = new List<(int X, int Y)>
        {
            (2, 2), (167, 2), (2, 167), (167, 167),
            (6, 85), (164, 85), (85, 164),
            (40, 3), (130, 3),
        };

        // Auto-contrôle : marge de 2 px de part et d'autre du bord (anticrénelage).
        foreach (var (x, y) in dedans)
            Assert.True(DistanceCentre(x, y) <= RayonDisque - 2, $"Témoin dedans ({x},{y}) trop près du bord : {DistanceCentre(x, y):0.0}");
        foreach (var (x, y) in dehors)
        {
            Assert.True(DistanceCentre(x, y) >= RayonDisque + 2, $"Témoin dehors ({x},{y}) trop près du bord : {DistanceCentre(x, y):0.0}");
            // Phase 41 : la flèche de reset de Braises (triangle (80,5)(90,5)(85,12) + filet x 85, y 13→25) dépasse le
            // disque r 74. Ces pixels peints hors silhouette sont ASSUMÉS : aucun témoin dehors dans ce secteur.
            Assert.False(x >= 76 && x <= 94 && y >= 0 && y <= 26, $"Témoin dehors ({x},{y}) dans le secteur de la flèche de reset.");
        }
        return (dedans, dehors);

        static double DistanceCentre(int x, int y)
        {
            double dx = x + 0.5 - 85, dy = y + 0.5 - 85;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>Témoins du rectangle arrondi W × H (rayon 10). Les quasi-coins (5,5) sont à 6,4 du centre du congé (&lt; 10),
    /// les vrais coins (0,0) à 13,4 (&gt; 10).</summary>
    internal static (IReadOnlyList<(int X, int Y)> Dedans, IReadOnlyList<(int X, int Y)> Dehors) TemoinsRectangle(double w, double h)
    {
        int W = (int)w, H = (int)h;
        var dedans = new List<(int X, int Y)>
        {
            (W / 2, H / 2), (3, H / 2), (W - 4, H / 2), (W / 2, 3), (W / 2, H - 4),
            (5, 5), (W - 6, 5), (5, H - 6), (W - 6, H - 6),
        };
        var dehors = new List<(int X, int Y)> { (0, 0), (W - 1, 0), (0, H - 1), (W - 1, H - 1) };

        foreach (var (x, y) in dedans)
        {
            Assert.InRange(x, 0, W - 1);
            Assert.InRange(y, 0, H - 1);
            Assert.True(DistanceConge(x, y) <= RayonCongé - 2, $"Témoin dedans ({x},{y}) hors du congé : {DistanceConge(x, y):0.0}");
        }
        foreach (var (x, y) in dehors)
            Assert.True(DistanceConge(x, y) >= RayonCongé + 2, $"Témoin dehors ({x},{y}) trop près du congé : {DistanceConge(x, y):0.0}");
        return (dedans, dehors);

        // Distance au centre du congé le plus proche (0 hors des carrés de coin).
        double DistanceConge(int x, int y)
        {
            double px = x + 0.5, py = y + 0.5;
            double cx = Math.Clamp(px, RayonCongé, w - RayonCongé), cy = Math.Clamp(py, RayonCongé, h - RayonCongé);
            double dx = px - cx, dy = py - cy;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    private static (IReadOnlyList<(int X, int Y)> Dedans, IReadOnlyList<(int X, int Y)> Dehors) Temoins(CadranStyle style, Size taille)
        => style is CadranStyle.Arcs or CadranStyle.Braises ? TemoinsDisque() : TemoinsRectangle(taille.Width, taille.Height);

    // ================================ Montage ================================

    private static ChronosPaths TempPaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosZonesGeste_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Assert.StartsWith(Path.GetTempPath(), dir);
        return new ChronosPaths(Path.Combine(dir, "usage.json"), Path.Combine(dir, "projects"));
    }

    private static UsageSnapshot SnapshotNominal() => new()
    {
        FiveHour = new WindowState
        {
            Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact, Provenance = ProvenanceReleve.Frais,
            Utilization = 0.3, ResetsAt = Now + TimeSpan.FromHours(2),
        },
        SevenDay = new WindowState
        {
            Kind = WindowKind.SevenDay, Reliability = SourceReliability.Exact, Provenance = ProvenanceReleve.Frais,
            Utilization = 0.6, ResetsAt = Now + TimeSpan.FromDays(3),
        },
        SourceCapturedAt = Now,
    };

    private static UsageSnapshot SnapshotIndisponible() => new()
    {
        FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
        SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
    };

    private static (MainWindow fenetre, FrameworkElement racine, MainViewModel vm, Size taille) Monter(
        CadranStyle style, OrientationCadran o, UsageSnapshot snap)
    {
        var paths = TempPaths();
        var prov = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(prov, RefreshOptions.Default);
        var settings = new SettingsService(paths);
        var vm = new MainViewModel(orch, new FakeUiDispatcher { OnUiThread = true }, new FakeClock(Now),
            new FakeWindowController(), new FakeAutostartService(), settings,
            new DiagnosticService(paths, settings, prov, new FakeClock(Now)),
            new FakeOAuthLogin(), new FakeSessionsController(), new FakeAuthStatus());

        vm.CadranStyle = style;
        vm.IsModeEtendu = false;
        var orientation = o == OrientationCadran.Vertical ? Orientation.Vertical : Orientation.Horizontal;
        vm.OrientationFusible = orientation;
        vm.OrientationMaree = orientation;
        vm.OrientationVolets = orientation;
        vm.ApplySnapshot(snap);
        vm.AppliquerEtatAuth(EtatAuthentification.Connecte);

        var guard = new TopmostGuard();
        var controller = new OverlayController(guard, new SettingsService(paths));
        var fenetre = new MainWindow(vm, guard, controller);

        var racine = (FrameworkElement)fenetre.Content!;
        racine.DataContext = vm;
        racine.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

        var taille = EmpreinteCadran.Pour(style, o);
        racine.Measure(taille);
        racine.Arrange(new Rect(taille));
        racine.UpdateLayout();
        return (fenetre, racine, vm, taille);
    }

    private static byte[] Rendre(FrameworkElement racine, int w, int h)
    {
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(racine);
        var pixels = new byte[w * h * 4];
        bmp.CopyPixels(pixels, w * 4, 0);
        return pixels;
    }

    private static byte Alpha(byte[] pixels, int w, int x, int y) => pixels[(y * w + x) * 4 + 3];

    // ================================ Tests ================================

    [WpfFact]
    public void Chaque_variante_declare_une_seule_silhouette_conforme_au_contrat()
    {
        foreach (var (style, o) in Variantes)
        {
            var (fenetre, racine, _, taille) = Monter(style, o, SnapshotNominal());
            try
            {
                var nom = $"{style}/{o}";
                var silhouettes = ZoneGeste.TrouverToutes(racine);
                Assert.True(silhouettes.Count == 1, $"{nom} : silhouettes visibles attendues : 1, trouvées : {silhouettes.Count}.");
                var s = silhouettes[0];
                Assert.Same(s, ZoneGeste.Trouver(racine));

                var pinceau = Assert.IsType<SolidColorBrush>(s.Fill);
                Assert.Equal(Color.FromArgb(1, 0, 0, 0), pinceau.Color);

                var origine = s.TransformToAncestor(racine).Transform(new Point(0, 0));
                if (style is CadranStyle.Arcs or CadranStyle.Braises)
                {
                    Assert.IsType<Ellipse>(s);
                    Assert.Equal(2 * RayonDisque, s.ActualWidth, 2);
                    Assert.Equal(2 * RayonDisque, s.ActualHeight, 2);
                    Assert.Equal(85, origine.X + s.ActualWidth / 2, 2);
                    Assert.Equal(85, origine.Y + s.ActualHeight / 2, 2);
                    Assert.True(ZoneGeste.Contient(s, new Point(74, 74)), $"{nom} : le centre doit être dans la silhouette.");
                }
                else
                {
                    var r = Assert.IsType<Rectangle>(s);
                    Assert.Equal(RayonCongé, r.RadiusX);
                    Assert.Equal(RayonCongé, r.RadiusY);
                    Assert.Equal(taille.Width, s.ActualWidth, 2);
                    Assert.Equal(taille.Height, s.ActualHeight, 2);
                    Assert.Equal(0, origine.X, 2);
                    Assert.Equal(0, origine.Y, 2);
                    Assert.False(ZoneGeste.Contient(s, new Point(0.5, 0.5)), $"{nom} : le coin (0,0) est hors du congé.");
                }
            }
            finally { fenetre.Close(); }
        }
    }

    [WpfFact]
    public void Le_rendu_capte_dans_la_silhouette_et_laisse_passer_dehors()
    {
        foreach (var (style, o) in Variantes)
        {
            var (fenetre, racine, vm, taille) = Monter(style, o, SnapshotNominal());
            try
            {
                Assert.False(vm.DataUnavailable);
                Assert.False(vm.AfficherReleveDate);
                Assert.False(vm.AfficherPastilleHorsLigne);
                Assert.False(vm.AfficherInvitationConnexion);
                Assert.False(vm.AfficherPastilleDeconnexion);
                VerifierRendu($"{style}/{o} (nominal)", style, racine, taille, retirerCoinBasDroit: false);
            }
            finally { fenetre.Close(); }
        }
    }

    [WpfFact]
    public void Le_rendu_tient_aussi_quand_les_donnees_sont_indisponibles()
    {
        foreach (var (style, o) in Variantes)
        {
            var (fenetre, racine, vm, taille) = Monter(style, o, SnapshotIndisponible());
            try
            {
                Assert.True(vm.DataUnavailable);
                // Le témoin du coin bas-droit n'est retiré QUE si une pastille de la rangée est affichée dans cet état.
                var pastille = vm.AfficherReleveDate || vm.AfficherPastilleHorsLigne
                               || vm.AfficherInvitationConnexion || vm.AfficherPastilleDeconnexion;
                VerifierRendu($"{style}/{o} (indisponible{(pastille ? ", pastille affichée : coin bas-droit retiré" : "")})",
                              style, racine, taille, retirerCoinBasDroit: pastille);
            }
            finally { fenetre.Close(); }
        }
    }

    private static void VerifierRendu(string nom, CadranStyle style, FrameworkElement racine, Size taille, bool retirerCoinBasDroit)
    {
        // Non-vacuité : la grille a bien l'empreinte, sinon le rendu serait vide et les « dehors » verts par accident.
        Assert.Equal(taille.Width, racine.ActualWidth, 2);
        Assert.Equal(taille.Height, racine.ActualHeight, 2);

        int w = (int)taille.Width, h = (int)taille.Height;
        var pixels = Rendre(racine, w, h);
        var (dedans, dehors) = Temoins(style, taille);

        var echecs = new List<string>();
        foreach (var (x, y) in dedans)
        {
            var a = Alpha(pixels, w, x, y);
            if (a == 0) echecs.Add($"dedans ({x},{y}) : alpha {a}, attendu > 0");
        }
        foreach (var (x, y) in dehors)
        {
            if (retirerCoinBasDroit && x >= w - 4 && y >= h - 4) continue;
            var a = Alpha(pixels, w, x, y);
            if (a != 0) echecs.Add($"dehors ({x},{y}) : alpha {a}, attendu 0");
        }
        Assert.True(echecs.Count == 0, $"{nom} :\n  " + string.Join("\n  ", echecs));
    }

    [Fact]
    public void Les_vues_de_cadran_n_ont_ni_fond_Transparent_ni_alpha_litteral()
    {
        var racine = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine), "CheminSourcesChronos manquant : cette garde ne lirait rien.");

        var vues = Directory.GetFiles(Path.Combine(racine, "Views", "Cadrans"), "*.xaml");
        Assert.Equal(5, vues.Length);
        foreach (var vue in vues)
        {
            var texte = File.ReadAllText(vue);
            var nom = Path.GetFileName(vue);
            Assert.False(texte.Contains("=\"Transparent\"", StringComparison.Ordinal), $"{nom} : fond Transparent (alpha 0, laisse passer le clic).");
            Assert.False(texte.Contains("#01000000", StringComparison.Ordinal), $"{nom} : alpha littéral, utiliser le token ZoneSilhouette.");
            Assert.Contains("ZoneGeste.Silhouette=\"True\"", texte, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource ZoneSilhouette}", texte, StringComparison.Ordinal);
        }

        var tokens = File.ReadAllText(Path.Combine(racine, "Resources", "DesignTokens.xaml"));
        Assert.Matches("<SolidColorBrush x:Key=\"ZoneSilhouette\"\\s+Color=\"#01000000\"", tokens);
    }
}
