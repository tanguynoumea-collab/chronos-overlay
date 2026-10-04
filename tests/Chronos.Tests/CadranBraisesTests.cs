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
/// 72°, là où une répartition uniforme en mettrait une), état « en attente » aux MÊMES angles que le nominal. Plan 43-05 :
/// anneau hebdo en 14 braises, 7 groupes de 2 (un jour par groupe, une braise par demi-journée). Les couleurs littérales sont permises ICI (tests) : la garde de la phase 39 ne vise que les sources.
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

    // --- Plan 43-05 (écart du constat) : anneau hebdo en 14 braises, 7 groupes de 2 (un jour par groupe) ---

    private static double AngleHebdo(int i) => BraisesGeometrie.Angle(i, 14, 2, 15.6);

    private static EmberRingControl AnneauHebdo(double fraction) => new()
    {
        Radius = 44, Count = 14, PipRadius = 3.6, GroupSize = 2, GroupPitch = 15.6,
        Fraction = fraction, HasData = true,
        QuotaBrush = B(Rouge), AshBrush = B(Cendre), WaitBrush = B(Attente),
    };

    [WpfFact]
    public void Anneau_hebdo_sept_groupes_de_deux_rien_a_midi_ni_entre_deux_jours()
    {
        var px = Rendre(AnneauHebdo(1.0));

        Assert.True(Proche(Pixel(px, PointSur(AngleHebdo(0), 44)), Rouge), "braise hebdo 0 non rouge");
        Assert.True(Proche(Pixel(px, PointSur(AngleHebdo(1), 44)), Rouge), "braise hebdo 1 non rouge");
        Assert.Equal(0, Pixel(px, PointSur(0, 44)).A);              // midi : milieu d'un vide
        Assert.Equal(0, Pixel(px, PointSur(360.0 / 7, 44)).A);      // frontière entre le jour 1 et le jour 2
    }

    [WpfTheory]
    [InlineData(128.0, 11)]   // 5 j 8 h restants (constat du 2026-10-04) → 11 braises
    [InlineData(24.0, 2)]
    [InlineData(0.0, 0)]
    public void Anneau_hebdo_allume_une_braise_par_demi_journee_restante(double heures, int allumees)
    {
        var px = Rendre(AnneauHebdo(heures / 168.0));

        for (int i = 0; i < 14; i++)
        {
            var p = Pixel(px, PointSur(AngleHebdo(i), 44));
            if (i < allumees - 1) Assert.True(Proche(p, Rouge), $"braise {i} non allumée");
            else if (i == allumees - 1) Assert.True(p.A is > 0 and < 255 && p.R > p.G, $"braise {i} : demi-lueur attendue");   // dernière = demi-lueur
            else Assert.True(Proche(p, Cendre), $"braise {i} non cendre");
        }
    }

    [WpfFact]
    public void Vue_l_anneau_hebdo_est_en_quatorze_braises_groupees_par_deux_et_le_groupe_tient_dans_son_secteur()
    {
        var (vue, _) = Monter(new CadranPreviewViewModel());
        var anneaux = ((Grid)vue.Content).Children.OfType<EmberRingControl>().ToList();
        Assert.Equal(2, anneaux.Count);

        var hebdo = anneaux[1];
        Assert.Equal(44.0, hebdo.Radius, 6);
        Assert.Equal(14, hebdo.Count);
        Assert.Equal(2, hebdo.GroupSize);
        Assert.True(hebdo.GroupPitch > 0, "GroupPitch nul : repli uniforme silencieux");
        // Le groupe doit tenir dans son secteur, sinon BraisesGeometrie retombe en uniforme sans rien dire.
        Assert.True((hebdo.GroupSize - 1) * hebdo.GroupPitch < 360.0 * hebdo.GroupSize / hebdo.Count);
        Assert.NotEqual(360.0 / 14, BraisesGeometrie.Angle(1, hebdo.Count, hebdo.GroupSize, hebdo.GroupPitch), 3);
    }

    [WpfFact]
    public void La_vue_Braises_n_a_plus_de_fleche_de_reset()
    {
        // Constat du 2026-10-04 (plan 43-07) : la flèche de reset fixe à midi « ne sert à rien » — décision utilisateur,
        // retirée. Les braises et le « ↻ HH:MM » du centre restent.
        var theme = ThemeCatalog.Default;
        var vm = new CadranPreviewViewModel { SelectedTheme = theme };
        var vue = new CadranBraisesView();
        var hote = new Border { DataContext = vm, Child = vue };
        foreach (var kv in theme.BrushTokens()) hote.Resources[kv.Key] = kv.Value;
        hote.Measure(new Size(Cote, Cote));
        hote.Arrange(new Rect(0, 0, Cote, Cote));
        hote.UpdateLayout();

        // Les noms vivent dans le namescope du UserControl (Pitfall 3).
        Assert.Null(vue.FindName("FlecheReset"));
        Assert.Empty(Descendants(hote).OfType<Polygon>());
        Assert.Empty(Descendants(hote).OfType<Line>());
        Assert.NotNull(vue.FindName("HeureResetBraises"));

        static IEnumerable<DependencyObject> Descendants(DependencyObject racine)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(racine); i++)
            {
                var enfant = VisualTreeHelper.GetChild(racine, i);
                yield return enfant;
                foreach (var d in Descendants(enfant)) yield return d;
            }
        }
    }

    [Fact]
    public void La_vue_Braises_n_a_aucune_animation()
    {
        var xaml = File.ReadAllText(System.IO.Path.Combine(GardesPerimetreTests.CheminSources(), "Views", "Cadrans", "CadranBraisesView.xaml"));
        Assert.DoesNotContain("Storyboard", xaml);
        Assert.DoesNotContain("Animation", xaml);
    }

    [Fact]
    public void L_heure_du_reset_passe_par_le_token_de_corps()
    {
        // §11 B4 : « ↻ HH:MM » au même pinceau que la ligne hebdo, corps par token, plus aucun 10,5 littéral.
        var xaml = File.ReadAllText(System.IO.Path.Combine(GardesPerimetreTests.CheminSources(), "Views", "Cadrans", "CadranBraisesView.xaml"));
        var debut = xaml.IndexOf("x:Name=\"HeureResetBraises\"", StringComparison.Ordinal);
        Assert.True(debut >= 0, "bloc HeureResetBraises introuvable");
        var fin = xaml.IndexOf("/>", debut, StringComparison.Ordinal);
        var bloc = xaml.Substring(debut, fin - debut);
        Assert.Contains("FontSize=\"{DynamicResource CadranCorpsLibelle}\"", bloc);
        Assert.Contains("TexteSecondaireClair", bloc);
        Assert.DoesNotContain("FontSize=\"10.5\"", xaml);
    }

    // --- BRA-02 (plan 41-02) : heure exacte du reset dans le centre temps ---

    /// <summary>Monte la vue Braises dans un hôte Border portant les tokens de corps et les pinceaux du thème par défaut ; renvoie (vue, hôte).</summary>
    private static (CadranBraisesView vue, Border hote) Monter(CadranPreviewViewModel vm)
    {
        var vue = new CadranBraisesView();
        var hote = new Border { DataContext = vm, Child = vue };
        hote.Resources.MergedDictionaries.Add(PleinEcranVuesTests.Tokens());   // corps par token (CadranCorpsLibelle, §11 B4)
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
        Assert.Equal(11, heure.FontSize, 6);   // §11 B4 : corps 11 (token CadranCorpsLibelle)
        Assert.Same(hote.Resources["TexteSecondaireClair"], heure.Foreground);   // même pinceau que la ligne hebdo

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
