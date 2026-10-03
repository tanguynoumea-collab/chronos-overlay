using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Chronos.Controls;
using Chronos.Rendering;
using Chronos.Text;
using Chronos.Theming;
using Chronos.ViewModels;
using Chronos.Views.Cadrans;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// BRA-01 (plan 41-01) — preuves PAR RENDU de l'anneau 5 h de Braises : 20 braises en 5 groupes de 4 (rien à midi ni à
/// 72°, là où une répartition uniforme en mettrait une), état « en attente » aux MÊMES angles que le nominal, anneau
/// hebdo inchangé. Les couleurs littérales sont permises ICI (tests) : la garde de la phase 39 ne vise que les sources.
/// </summary>
[Collection("XAML WPF")]
public class CadranBraisesTests
{
    private static readonly Color Rouge = Color.FromRgb(0xFF, 0x00, 0x00);
    private static readonly Color Cendre = Color.FromRgb(0x30, 0x30, 0x30);
    private static readonly Color Attente = Color.FromRgb(0x80, 0x80, 0x80);

    private static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    private const int Cote = 170;

    /// <summary>Mesure, arrange et rend hors écran l'élément (170 × 170, 96 DPI) ; renvoie les pixels Pbgra32.</summary>
    private static byte[] Rendre(FrameworkElement c)
    {
        c.Width = Cote;
        c.Height = Cote;
        c.Measure(new Size(Cote, Cote));
        c.Arrange(new Rect(0, 0, Cote, Cote));
        c.UpdateLayout();
        var bmp = new RenderTargetBitmap(Cote, Cote, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(c);
        var px = new byte[Cote * Cote * 4];
        bmp.CopyPixels(px, Cote * 4, 0);
        return px;
    }

    /// <summary>(A, R, G, B) du pixel (x, y) — composantes prémultipliées.</summary>
    private static (byte A, byte R, byte G, byte B) Pixel(byte[] px, (int X, int Y) p)
    {
        int i = (p.Y * Cote + p.X) * 4;
        return (px[i + 3], px[i + 2], px[i + 1], px[i]);
    }

    /// <summary>Pixel qui contient le point de l'anneau à <paramref name="angleDeg"/> (0 = midi, horaire).</summary>
    private static (int X, int Y) PointSur(double angleDeg, double rayon)
    {
        double a = angleDeg * Math.PI / 180.0;
        return ((int)Math.Floor(85 + rayon * Math.Sin(a)), (int)Math.Floor(85 - rayon * Math.Cos(a)));
    }

    private static bool Proche((byte A, byte R, byte G, byte B) p, Color c)
        => p.A == 255 && Math.Abs(p.R - c.R) <= 3 && Math.Abs(p.G - c.G) <= 3 && Math.Abs(p.B - c.B) <= 3;

    private static double AngleCinqHeures(int i) => BraisesGeometrie.Angle(i, 20, 4, 13.2);

    private static EmberRingControl AnneauCinqHeures(double fraction, bool hasData = true) => new()
    {
        Radius = 66, Count = 20, PipRadius = 4, GroupSize = 4, GroupPitch = 13.2,
        Fraction = fraction, HasData = hasData,
        QuotaBrush = B(Rouge), AshBrush = B(Cendre), WaitBrush = B(Attente),
    };

    [WpfFact]
    public void Plein_la_premiere_braise_est_a_16_2_degres_et_rien_a_midi_ni_a_72()
    {
        var px = Rendre(AnneauCinqHeures(1.0));

        Assert.True(Proche(Pixel(px, PointSur(AngleCinqHeures(0), 66)), Rouge), "braise 0 (16,2°) non rouge");
        Assert.Equal(0, Pixel(px, PointSur(0, 66)).A);    // midi : milieu d'un vide
        Assert.Equal(0, Pixel(px, PointSur(72, 66)).A);   // 72° : milieu du vide entre les groupes 1 et 2
    }

    [WpfFact]
    public void A_moitie_dix_braises_allumees_puis_cendres()
    {
        var px = Rendre(AnneauCinqHeures(0.5));

        Assert.True(Proche(Pixel(px, PointSur(AngleCinqHeures(8), 66)), Rouge), "braise 8 non allumée");
        Assert.True(Proche(Pixel(px, PointSur(AngleCinqHeures(12), 66)), Cendre), "braise 12 non cendre");
        Assert.Equal(0, Pixel(px, PointSur(72, 66)).A);
    }

    [WpfFact]
    public void En_attente_les_braises_neutres_sont_aux_memes_angles_que_le_nominal()
    {
        var px = Rendre(AnneauCinqHeures(1.0, hasData: false));

        Assert.True(Proche(Pixel(px, PointSur(AngleCinqHeures(0), 66)), Attente), "braise d'attente absente à 16,2°");
        Assert.Equal(0, Pixel(px, PointSur(0, 66)).A);
        Assert.Equal(0, Pixel(px, PointSur(72, 66)).A);
    }

    [WpfFact]
    public void Anneau_hebdo_sans_groupe_reste_uniforme()
    {
        var hebdo = new EmberRingControl
        {
            Radius = 44, Count = 12, PipRadius = 3.6, Fraction = 1.0,
            QuotaBrush = B(Rouge), AshBrush = B(Cendre), WaitBrush = B(Attente),
        };
        Assert.Equal(1, hebdo.GroupSize);
        Assert.Equal(0.0, hebdo.GroupPitch);

        var px = Rendre(hebdo);
        Assert.True(Pixel(px, PointSur(30, 44)).A > 0, "braise hebdo absente à 30°");
    }

    [WpfFact]
    public void La_fleche_de_reset_est_fixe_a_midi_hors_anneau_et_non_cliquable()
    {
        var theme = ThemeCatalog.Default;
        var vm = new CadranPreviewViewModel { SelectedTheme = theme };
        var vue = new CadranBraisesView();
        var hote = new Border { DataContext = vm, Child = vue };
        foreach (var kv in theme.BrushTokens()) hote.Resources[kv.Key] = kv.Value;
        hote.Measure(new Size(Cote, Cote));
        hote.Arrange(new Rect(0, 0, Cote, Cote));
        hote.UpdateLayout();

        // Les noms vivent dans le namescope du UserControl (Pitfall 3).
        var fleche = Assert.IsType<Canvas>(vue.FindName("FlecheReset"));
        Assert.False(fleche.IsHitTestVisible);
        Assert.Equal(Visibility.Visible, fleche.Visibility);

        var triangle = Assert.Single(fleche.Children.OfType<Polygon>());
        Assert.Equal(new[] { new Point(80, 5), new Point(90, 5), new Point(85, 12) }, triangle.Points.ToArray());   // base 10, hauteur 7

        var filet = Assert.Single(fleche.Children.OfType<Line>());
        Assert.Equal((85.0, 13.0, 85.0, 25.0), (filet.X1, filet.Y1, filet.X2, filet.Y2));                           // 12 px
        Assert.Equal(1.2, filet.StrokeThickness, 6);

        foreach (var p in triangle.Points.Append(new Point(filet.X1, filet.Y1)).Append(new Point(filet.X2, filet.Y2)))
            Assert.True(p.X is >= 0 and <= Cote && p.Y is >= 0 and <= Cote, $"point hors empreinte : {p}");

        // Structure, pas donnée : la flèche reste quand le reset 5 h est inconnu.
        vm.FiveHour.HasTime = false;
        hote.UpdateLayout();
        Assert.Equal(Visibility.Visible, fleche.Visibility);
        // IsVisible exige une PresentationSource (fenêtre) : on vérifie plutôt toute la chaîne d'ancêtres jusqu'à la vue.
        for (DependencyObject? e = fleche; e is not null && !ReferenceEquals(e, hote); e = VisualTreeHelper.GetParent(e))
            if (e is UIElement u) Assert.Equal(Visibility.Visible, u.Visibility);
    }

    [Fact]
    public void La_vue_Braises_n_a_aucune_animation()
    {
        var xaml = File.ReadAllText(System.IO.Path.Combine(GardesPerimetreTests.CheminSources(), "Views", "Cadrans", "CadranBraisesView.xaml"));
        Assert.DoesNotContain("Storyboard", xaml);
        Assert.DoesNotContain("Animation", xaml);
    }

    // --- BRA-02 (plan 41-02) : heure exacte du reset dans le centre temps ---

    /// <summary>Monte la vue Braises dans un hôte Border portant les pinceaux du thème par défaut ; renvoie (vue, hôte).</summary>
    private static (CadranBraisesView vue, Border hote) Monter(CadranPreviewViewModel vm)
    {
        var vue = new CadranBraisesView();
        var hote = new Border { DataContext = vm, Child = vue };
        foreach (var kv in ThemeCatalog.Default.BrushTokens()) hote.Resources[kv.Key] = kv.Value;
        hote.Measure(new Size(Cote, Cote));
        hote.Arrange(new Rect(0, 0, Cote, Cote));
        hote.UpdateLayout();
        return (vue, hote);
    }

    [WpfFact]
    public void Galerie_en_mode_temps_la_3e_ligne_montre_l_heure_du_reset()
    {
        var vm = new CadranPreviewViewModel { ShowCountdown = true };
        var (vue, hote) = Monter(vm);

        var heure = Assert.IsType<TextBlock>(vue.FindName("HeureResetBraises"));
        Assert.Equal(Visibility.Visible, heure.Visibility);
        Assert.Equal(10.5, heure.FontSize, 6);
        Assert.Same(hote.Resources["TexteSecondaire"], heure.Foreground);

        var attendu = "↻ " + TextesHistorique.HeureMinute(
            new DateTimeOffset(2026, 10, 3, 14, 0, 0, TimeSpan.Zero) + TimeSpan.FromHours(5) * 0.62, TimeZoneInfo.Local);
        Assert.Equal(attendu, heure.Text);

        // 3e enfant du StackPanel temps, sous les deux comptes à rebours.
        var panneauTemps = Assert.IsType<StackPanel>(heure.Parent);
        Assert.Equal(3, panneauTemps.Children.Count);
        Assert.Same(heure, panneauTemps.Children[2]);
        Assert.Equal(Visibility.Visible, panneauTemps.Visibility);
    }

    [WpfFact]
    public void En_mode_pourcentages_le_centre_reste_a_deux_lignes_sans_heure()
    {
        var vm = new CadranPreviewViewModel { ShowCountdown = false };
        var (vue, _) = Monter(vm);

        var heure = Assert.IsType<TextBlock>(vue.FindName("HeureResetBraises"));
        var panneauTemps = Assert.IsType<StackPanel>(heure.Parent);
        Assert.Equal(Visibility.Collapsed, panneauTemps.Visibility);

        var grille = Assert.IsType<Grid>(vue.Content);
        var panneauPct = grille.Children.OfType<StackPanel>().Single(sp => !ReferenceEquals(sp, panneauTemps));
        var lignes = panneauPct.Children.OfType<TextBlock>().ToList();
        Assert.Equal(2, lignes.Count);
        Assert.Equal(2, panneauPct.Children.Count);
        Assert.DoesNotContain(heure, lignes);
        Assert.Equal(vm.FiveHour.UtilizationText, lignes[0].Text);
        Assert.Equal(vm.SevenDay.UtilizationText, lignes[1].Text);
    }

    [WpfFact]
    public void Sans_heure_de_reset_la_ligne_est_masquee_meme_en_mode_temps()
    {
        var vm = new CadranPreviewViewModel { ShowCountdown = true };
        var (vue, hote) = Monter(vm);

        vm.FiveHour.HasHeureReset = false;
        hote.UpdateLayout();

        var heure = Assert.IsType<TextBlock>(vue.FindName("HeureResetBraises"));
        Assert.Equal(Visibility.Collapsed, heure.Visibility);
    }

    [WpfFact]
    public void Galerie_aucune_heure_sur_l_hebdo_et_aucune_heure_sans_temps_restant()
    {
        var vm = new CadranPreviewViewModel();
        Assert.True(vm.FiveHour.HasHeureReset);
        Assert.False(vm.SevenDay.HasHeureReset);   // différé : pas d'heure sur l'hebdo
        Assert.Equal("", vm.SevenDay.HeureResetTexte);

        // Comme en production : un reset non futur (temps restant nul) ne montre pas d'heure.
        vm.FiveTimePct = 0;
        Assert.False(vm.FiveHour.HasHeureReset);
        Assert.Equal("", vm.FiveHour.HeureResetTexte);

        vm.FiveTimePct = 30;
        Assert.True(vm.FiveHour.HasHeureReset);
    }
}
