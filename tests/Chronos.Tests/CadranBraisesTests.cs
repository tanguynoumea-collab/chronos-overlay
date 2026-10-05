using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
/// BRA-01 (plan 41-01) — preuves PAR RENDU de l'anneau 5 h de Braises. Plan 43-08 (constat du 2026-10-05) : 25 braises de
/// 12 min en 5 groupes de 5, un groupe par heure (rien à midi ni à 72°, là où une répartition uniforme en mettrait une),
/// état « en attente » aux MÊMES angles que le nominal. Le sens de remplissage (horaire depuis midi) est prouvé au rendu de
/// la VUE, avec des positions calculées indépendamment de BraisesGeometrie. Plan 43-09 (constat du 2026-10-05) : dans la VUE,
/// l'anneau extérieur est devenu la JOURNÉE (24 braises, une par heure, minuit en haut, groupées par tranches de 5 h) ; les
/// tests de contrôle en 25 / 5 / 11 restent la preuve générique du groupement de EmberRingControl. Plan 43-05 :
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

    private static double AngleCinqHeures(int i) => BraisesGeometrie.Angle(i, 25, 5, 11.0);

    private static EmberRingControl AnneauCinqHeures(double fraction, bool hasData = true) => new()
    {
        Radius = 66, Count = 25, PipRadius = 4, GroupSize = 5, GroupPitch = 11.0,
        Fraction = fraction, HasData = hasData,
        QuotaBrush = B(Rouge), AshBrush = B(Cendre), WaitBrush = B(Attente),
    };

    [WpfFact]
    public void Plein_la_premiere_braise_est_a_14_degres_et_rien_a_midi_ni_a_72()
    {
        var px = Rendre(AnneauCinqHeures(1.0));

        Assert.True(Proche(Pixel(px, PointSur(AngleCinqHeures(0), 66)), Rouge), "braise 0 (14°) non rouge");
        Assert.Equal(0, Pixel(px, PointSur(0, 66)).A);    // midi : milieu d'un vide
        Assert.Equal(0, Pixel(px, PointSur(72, 66)).A);   // 72° : milieu du vide entre les groupes 1 et 2
    }

    [WpfFact]
    public void A_moitie_treize_braises_allumees_puis_cendres()
    {
        var px = Rendre(AnneauCinqHeures(0.5));   // 12,5 → 13 braises (arrondi au plus loin de zéro)

        Assert.True(Proche(Pixel(px, PointSur(AngleCinqHeures(8), 66)), Rouge), "braise 8 non allumée");
        Assert.True(Proche(Pixel(px, PointSur(AngleCinqHeures(14), 66)), Cendre), "braise 14 non cendre");
        Assert.Equal(0, Pixel(px, PointSur(72, 66)).A);
    }

    [WpfFact]
    public void En_attente_les_braises_neutres_sont_aux_memes_angles_que_le_nominal()
    {
        var px = Rendre(AnneauCinqHeures(1.0, hasData: false));

        Assert.True(Proche(Pixel(px, PointSur(AngleCinqHeures(0), 66)), Attente), "braise d'attente absente à 14°");
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
    public void Vue_l_anneau_exterieur_est_la_journee_en_24_braises_a_angles_explicites()
    {
        // Plan 43-09 (constat du 2026-10-05) : l'anneau extérieur est la journée locale, une braise par heure ; ses angles
        // et son allumage viennent du VM (BraisesJournee), plus de FiveHour.FractionElapsed.
        var (vue, _) = Monter(new CadranPreviewViewModel());
        var jour = ((Grid)vue.Content).Children.OfType<EmberRingControl>().First();

        Assert.Equal(66.0, jour.Radius, 6);
        Assert.Equal(24, jour.Count);
        Assert.NotNull(jour.Angles);
        Assert.Equal(24, jour.Angles!.Count);
        Assert.NotNull(jour.Etats);
        Assert.Equal(24, jour.Etats!.Count);
        Assert.Null(BindingOperations.GetBindingExpression(jour, EmberRingControl.FractionProperty));
    }

    // --- Plan 43-08 : sens de remplissage prouvé au RENDU de la vue (positions indépendantes de BraisesGeometrie) ---

    /// <summary>
    /// Centre attendu de la braise <paramref name="k"/> du groupe <paramref name="g"/>, calculé SANS BraisesGeometrie :
    /// groupe centré au milieu de son secteur, braises au pas <paramref name="pas"/>. Repère écran : x = 85 + r·sin, y = 85 − r·cos
    /// (0° = midi, angles croissants = sens horaire : un angle de 0 à 180° est à DROITE de l'axe vertical).
    /// </summary>
    private static (int X, int Y) CentreBraise(int g, int k, int parGroupe, double secteur, double pas, double rayon)
        => PointSur(g * secteur + secteur / 2 + (k - (parGroupe - 1) / 2.0) * pas, rayon);

    /// <summary>Couleur NON prémultipliée du pixel (la demi-lueur garde ainsi la teinte du quota).</summary>
    private static (byte A, Color C) Teinte(byte[] px, (int X, int Y) p)
    {
        var (a, r, g, b) = Pixel(px, p);
        if (a == 0) return (0, Colors.Transparent);
        byte U(byte v) => (byte)Math.Min(255, v * 255 / a);
        return (a, Color.FromRgb(U(r), U(g), U(b)));
    }

    private static int Distance(Color x, Color y) => Math.Abs(x.R - y.R) + Math.Abs(x.G - y.G) + Math.Abs(x.B - y.B);

    /// <summary>Monte la vue (thème par défaut), rend l'hôte et renvoie les pixels avec les deux anneaux effectifs.</summary>
    private static (byte[] px, EmberRingControl cinq, EmberRingControl hebdo) RendreVue(CadranPreviewViewModel vm)
    {
        var (vue, hote) = Monter(vm);
        var anneaux = ((Grid)vue.Content).Children.OfType<EmberRingControl>().ToList();
        var bmp = new RenderTargetBitmap(Cote, Cote, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(hote);
        var px = new byte[Cote * Cote * 4];
        bmp.CopyPixels(px, Cote * 4, 0);
        return (px, anneaux[0], anneaux[1]);
    }

    private static void AssertGroupe(byte[] px, EmberRingControl anneau, int g, int parGroupe, double secteur, bool allume, string libelle)
    {
        var quota = ((SolidColorBrush)anneau.QuotaBrush!).Color;
        var cendre = ((SolidColorBrush)anneau.AshBrush!).Color;
        Assert.True(Distance(quota, cendre) > 90, $"{libelle} : quota {quota} et cendre {cendre} trop proches pour trancher");
        for (int k = 0; k < parGroupe; k++)
        {
            var p = CentreBraise(g, k, parGroupe, secteur, anneau.GroupPitch, anneau.Radius);
            var (a, c) = Teinte(px, p);
            Assert.True(a >= 100, $"{libelle} : groupe {g} braise {k} absente en {p} (alpha {a})");   // la silhouette de geste a un alpha de 1
            bool cQuota = Distance(c, quota) < Distance(c, cendre);
            Assert.True(cQuota == allume,
                $"{libelle} : groupe {g} braise {k} en {p} attendue {(allume ? "allumée" : "éteinte")}, teinte {c} (quota {quota}, cendre {cendre})");
        }
    }

    private static CadranPreviewViewModel VmSens(double consomme5h, double consommeHebdo) => new()
    {
        FivePlancher = false, SevenPlancher = false,   // braises pleines (pas de contour pointillé)
        FiveQuotaPct = 48, SevenQuotaPct = 48,
        FiveTimePct = 100 - consomme5h, SevenTimePct = 100 - consommeHebdo,
    };

    // --- Plan 43-09 : anneau JOURNÉE prouvé au rendu de la vue (positions indépendantes de BraisesJournee) ---

    /// <summary>
    /// Angle attendu de la braise <paramref name="i"/> pour la grille du reset 14:50, calculé SANS BraisesJournee : groupes
    /// écrits à la main d'après la spécification ([00-04], [05-09], [10-14], [15-19], [20-23]), heure nominale (i + 0,5) × 15°,
    /// rapprochée du centre de son groupe par 0,8. 0° = minuit en haut, sens horaire.
    /// </summary>
    private static double AngleJournee1450(int i)
    {
        int[][] groupes = { new[] { 0, 4 }, new[] { 5, 9 }, new[] { 10, 14 }, new[] { 15, 19 }, new[] { 20, 23 } };
        var g = groupes.First(x => i >= x[0] && i <= x[1]);
        double nominal = (i + 0.5) * 15.0;
        double centre = ((g[0] + g[1]) / 2.0 + 0.5) * 15.0;
        return centre + 0.8 * (nominal - centre);
    }

    /// <summary>11:35 (offset explicite, jamais l'horloge réelle) ; reset 5 h à 14:50 = 11:35 + 65 % de 5 h.</summary>
    private static CadranPreviewViewModel VmJournee1135() => new()
    {
        MaintenantEchantillon = new DateTimeOffset(2026, 10, 5, 11, 35, 0, TimeSpan.FromHours(2)),
        FivePlancher = false, SevenPlancher = false, FiveQuotaPct = 48, SevenQuotaPct = 48,
        FiveTimePct = 65, SevenTimePct = 50,
    };

    [WpfFact]
    public void Rendu_vue_journee_a_11h35_braise_10_pleine_11_demi_lueur_12_cendre()
    {
        var (px, jour, _) = RendreVue(VmJournee1135());
        var quota = ((SolidColorBrush)jour.QuotaBrush!).Color;
        var cendre = ((SolidColorBrush)jour.AshBrush!).Color;
        Assert.True(Distance(quota, cendre) > 90, $"quota {quota} et cendre {cendre} trop proches pour trancher");

        var (a10, c10) = Teinte(px, PointSur(AngleJournee1450(10), 66));
        Assert.True(a10 >= 250 && Distance(c10, quota) < Distance(c10, cendre), $"braise 10 : pleine attendue, alpha {a10}, teinte {c10}");

        var (a11, c11) = Teinte(px, PointSur(AngleJournee1450(11), 66));
        Assert.True(a11 is >= 60 and < 250, $"braise 11 : demi-lueur attendue, alpha {a11}");
        Assert.True(Distance(c11, quota) < Distance(c11, cendre), $"braise 11 : teinte du quota attendue, {c11}");

        var (a12, c12) = Teinte(px, PointSur(AngleJournee1450(12), 66));
        Assert.True(a12 >= 100 && Distance(c12, cendre) < Distance(c12, quota), $"braise 12 : cendre attendue, alpha {a12}, teinte {c12}");

        // Heures passées toutes pleines, futur tout en cendre.
        for (int i = 0; i < 10; i++)
        {
            var (a, c) = Teinte(px, PointSur(AngleJournee1450(i), 66));
            Assert.True(a >= 250 && Distance(c, quota) < Distance(c, cendre), $"braise {i} : pleine attendue");
        }
        for (int i = 13; i < 24; i++)
        {
            var (a, c) = Teinte(px, PointSur(AngleJournee1450(i), 66));
            Assert.True(a >= 100 && Distance(c, cendre) < Distance(c, quota), $"braise {i} : cendre attendue");
        }
    }

    [WpfFact]
    public void Rendu_vue_journee_minuit_en_haut_6h_a_droite_midi_en_bas_18h_a_gauche()
    {
        var (px, _, _) = RendreVue(VmJournee1135());

        var p6 = PointSur(AngleJournee1450(6), 66);
        var p12 = PointSur(AngleJournee1450(12), 66);
        var p18 = PointSur(AngleJournee1450(18), 66);
        Assert.True(p6.X > 145 && Math.Abs(p6.Y - 85) < 10, $"braise 6 attendue à droite, en {p6}");
        Assert.True(p12.Y > 145 && Math.Abs(p12.X - 85) < 10, $"braise 12 attendue en bas, en {p12}");
        Assert.True(p18.X < 25 && Math.Abs(p18.Y - 85) < 10, $"braise 18 attendue à gauche, en {p18}");
        foreach (var (p, i) in new[] { (p6, 6), (p12, 12), (p18, 18) })
            Assert.True(Teinte(px, p).A >= 100, $"braise {i} absente en {p}");

        // Le vide entre deux tranches : rien au milieu de 14:30 et 15:30 (frontière 14:50).
        double milieu = (AngleJournee1450(14) + AngleJournee1450(15)) / 2;
        Assert.True(Teinte(px, PointSur(milieu, 66)).A < 100, "le vide entre deux tranches doit rester vide");
    }

    [WpfFact]
    public void Rendu_vue_journee_l_hebdo_ne_lie_toujours_que_le_temps_consomme()
    {
        var (_, _, hebdo) = RendreVue(VmJournee1135());
        var b = BindingOperations.GetBindingExpression(hebdo, EmberRingControl.FractionProperty);
        Assert.NotNull(b);
        Assert.Equal("SevenDay.FractionElapsed", b!.ParentBinding.Path.Path);
    }

    [WpfFact]
    public void Controle_angles_et_etats_explicites_pleine_demi_lueur_cendre()
    {
        var angles = Enumerable.Range(0, 24).Select(i => (i + 0.5) * 15.0).ToArray();
        var etats = Enumerable.Range(0, 24)
            .Select(i => i < 3 ? EtatBraise.Pleine : i == 3 ? EtatBraise.DemiLueur : EtatBraise.Cendre).ToArray();
        var r = new EmberRingControl
        {
            Radius = 66, Count = 24, PipRadius = 4, Angles = angles, Etats = etats, Fraction = 1.0,
            QuotaBrush = B(Rouge), AshBrush = B(Cendre), WaitBrush = B(Attente),
        };
        var px = Rendre(r);

        Assert.True(Proche(Pixel(px, PointSur(angles[2], 66)), Rouge), "braise 2 pleine");
        var demi = Pixel(px, PointSur(angles[3], 66));
        Assert.True(demi.A is > 0 and < 255 && demi.R > demi.G, "braise 3 en demi-lueur");
        Assert.True(Proche(Pixel(px, PointSur(angles[4], 66)), Cendre), "braise 4 en cendre malgré Fraction = 1");
        Assert.Equal(0, Pixel(px, PointSur(0, 66)).A);   // minuit : entre la braise 23 et la braise 0
    }

    [WpfFact]
    public void Rendu_vue_hebdo_deux_jours_consommes_allument_les_deux_premiers_groupes_horaires()
    {
        // 2 jours sur 7 : 4 braises — les deux premiers jours après midi, côté droit ; le jour avant midi reste éteint.
        var (px, _, hebdo) = RendreVue(VmSens(0, 200.0 / 7));
        double secteur = 360.0 / 7;

        for (int g = 0; g < 2; g++) AssertGroupe(px, hebdo, g, 2, secteur, allume: true, "hebdo à 2 j");
        for (int g = 2; g < 7; g++) AssertGroupe(px, hebdo, g, 2, secteur, allume: false, "hebdo à 2 j");
        Assert.True(CentreBraise(0, 0, 2, secteur, hebdo.GroupPitch, 44).X > 85);
        Assert.True(CentreBraise(6, 1, 2, secteur, hebdo.GroupPitch, 44).X < 85);
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
