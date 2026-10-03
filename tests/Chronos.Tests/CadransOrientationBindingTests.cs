using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Chronos.Controls;
using Chronos.Rendering;
using Chronos.Services;
using Chronos.Theming;
using Chronos.ViewModels;
using Chronos.Views.Cadrans;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Cadrans à l'échelle 1 et à deux orientations (CAD-01 / CAD-03, phase 40). Chaque vue porte une DP <c>Orientation</c> (portée
/// par la VUE, pas par le DataContext) qui montre exactement un des deux gabarits <c>GabaritHorizontal</c> / <c>GabaritVertical</c>.
/// On vérifie, par variante : le gabarit visible, l'empreinte exacte (miroir <see cref="EmpreinteCadran"/>), les corps de texte
/// non réduits, les textes au pire cas (« ≥ 100 % », « 6 j 23 h ») dans leur boîte, et l'absence de recouvrement valeur / contrôle.
/// Table de variantes extensible (plans 05 et 08 : Volets, Braises, Arcs, galerie).
/// </summary>
[Collection("XAML WPF")]   // charge du BAML : sérialisé avec les autres classes XAML
public class CadransOrientationBindingTests
{
    // ------------------------------------------------------------------ Table des variantes

    /// <summary>Variantes à deux gabarits : fabrique de vue, style, orientation.</summary>
    public static readonly (Func<FrameworkElement> Creer, CadranStyle Style, OrientationCadran O)[] Variantes =
    {
        (() => new CadranFusibleView(), CadranStyle.Fusible, OrientationCadran.Horizontal),
        (() => new CadranFusibleView(), CadranStyle.Fusible, OrientationCadran.Vertical),
        (() => new CadranMareeView(),   CadranStyle.Maree,   OrientationCadran.Vertical),
        (() => new CadranMareeView(),   CadranStyle.Maree,   OrientationCadran.Horizontal),
    };

    /// <summary>Variantes Volets (plan 05) : structure propre (tuile, plaque chiffrée, rangée de volets), d'où des tests dédiés
    /// pour les corps, les textes au pire cas et l'empilement ; les tests génériques (gabarit, empreinte, orientation des
    /// contrôles) couvrent <see cref="ToutesVariantes"/>.</summary>
    public static readonly (Func<FrameworkElement> Creer, CadranStyle Style, OrientationCadran O)[] VariantesVolets =
    {
        (() => new CadranVoletsView(), CadranStyle.Volets, OrientationCadran.Horizontal),
        (() => new CadranVoletsView(), CadranStyle.Volets, OrientationCadran.Vertical),
    };

    /// <summary>Toutes les variantes à deux gabarits.</summary>
    public static readonly (Func<FrameworkElement> Creer, CadranStyle Style, OrientationCadran O)[] ToutesVariantes =
        Variantes.Concat(VariantesVolets).ToArray();

    /// <summary>Textes de valeur les plus larges attendus (pire cas).</summary>
    private static readonly string[] TextesPireCas = { "≥ 100 %", "6 j 23 h" };

    // ------------------------------------------------------------------ Helpers

    private static Orientation VersWpf(OrientationCadran o)
        => o == OrientationCadran.Horizontal ? Orientation.Horizontal : Orientation.Vertical;

    /// <summary>Pose l'orientation sur la vue (DP de vue, propre à chaque type).</summary>
    private static void Orienter(FrameworkElement vue, OrientationCadran o)
    {
        switch (vue)
        {
            case CadranFusibleView f: f.Orientation = VersWpf(o); break;
            case CadranMareeView m: m.Orientation = VersWpf(o); break;
            case CadranVoletsView v: v.Orientation = VersWpf(o); break;
            default: throw new InvalidOperationException($"vue sans orientation : {vue.GetType().Name}");
        }
    }

    /// <summary>Monte la vue sous un hôte qui porte les tokens (dimensions, corps) et les pinceaux du thème par défaut,
    /// purge le Dispatcher puis met en page à l'empreinte.</summary>
    private static Border Monter(FrameworkElement vue, Size empreinte, bool decompte = false)
    {
        var hote = new Border { DataContext = new CadranPreviewViewModel { ShowCountdown = decompte } };
        hote.Resources.MergedDictionaries.Add(PleinEcranVuesTests.Tokens());
        foreach (var kv in ThemeCatalog.Default.BrushTokens()) hote.Resources[kv.Key] = kv.Value;
        hote.Child = vue;
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        hote.Measure(empreinte);
        hote.Arrange(new Rect(empreinte));
        hote.UpdateLayout();
        return hote;
    }

    /// <summary>Descendants logiques (les éléments masqués y figurent aussi).</summary>
    private static IEnumerable<T> Descendants<T>(DependencyObject racine) where T : DependencyObject
    {
        foreach (var enfant in LogicalTreeHelper.GetChildren(racine).OfType<DependencyObject>())
        {
            if (enfant is T t) yield return t;
            foreach (var d in Descendants<T>(enfant)) yield return d;
        }
    }

    /// <summary>Le gabarit visible (exactement un, vérifié).</summary>
    private static FrameworkElement GabaritVisible(FrameworkElement vue)
    {
        var h = (FrameworkElement)vue.FindName("GabaritHorizontal");
        var v = (FrameworkElement)vue.FindName("GabaritVertical");
        Assert.NotNull(h);
        Assert.NotNull(v);
        var visibles = new[] { h, v }.Where(g => g.Visibility == Visibility.Visible).ToList();
        Assert.Single(visibles);
        return visibles[0];
    }

    /// <summary>Descendants du gabarit visible seulement.</summary>
    private static List<T> DescendantsVisuels<T>(FrameworkElement vue) where T : DependencyObject
        => Descendants<T>(GabaritVisible(vue)).ToList();

    private static bool EstValeur(TextBlock tb)
    {
        var chemin = BindingOperations.GetBindingExpression(tb, TextBlock.TextProperty)?.ParentBinding.Path?.Path ?? "";
        return chemin.EndsWith("UtilizationText", StringComparison.Ordinal) || chemin.EndsWith("CountdownText", StringComparison.Ordinal);
    }

    private static bool EstLibelle(TextBlock tb) => tb.Text is "5 H" or "7 J";

    /// <summary>Rectangle RENDU d'un élément dans le repère de la vue (jamais le layout slot).</summary>
    private static Rect Rendu(FrameworkElement e, FrameworkElement vue)
        => e.TransformToAncestor(vue).TransformBounds(new Rect(e.RenderSize));

    /// <summary>Boîte de valeur : le Grid parent des deux TextBlock superposés.</summary>
    private static Grid Boite(TextBlock tb, FrameworkElement vue)
    {
        var boite = Assert.IsType<Grid>(tb.Parent);
        Assert.True(boite.IsDescendantOf(vue));
        return boite;
    }

    private static double LargeurTexte(TextBlock tb, string texte)
        => new FormattedText(texte, CultureInfo.GetCultureInfo("fr-FR"), FlowDirection.LeftToRight,
                new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch), tb.FontSize, Brushes.Black, 1.0)
            .WidthIncludingTrailingWhitespace;

    private static string Nom(FrameworkElement vue, OrientationCadran o) => $"{vue.GetType().Name} / {o}";

    // ------------------------------------------------------------------ Tests

    [WpfFact]
    public void Le_gabarit_visible_suit_l_orientation_de_la_vue()
    {
        foreach (var (creer, style, o) in ToutesVariantes)
        {
            var vue = creer();
            Orienter(vue, o);
            Monter(vue, EmpreinteCadran.Pour(style, o));
            var attendu = o == OrientationCadran.Horizontal ? "GabaritHorizontal" : "GabaritVertical";
            var autre = o == OrientationCadran.Horizontal ? "GabaritVertical" : "GabaritHorizontal";
            Assert.Equal(Visibility.Visible, ((FrameworkElement)vue.FindName(attendu)).Visibility);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)vue.FindName(autre)).Visibility);

            // Bascule : les deux gabarits s'inversent.
            var inverse = o == OrientationCadran.Horizontal ? OrientationCadran.Vertical : OrientationCadran.Horizontal;
            Orienter(vue, inverse);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)vue.FindName(attendu)).Visibility);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)vue.FindName(autre)).Visibility);
        }
    }

    [WpfFact]
    public void Les_defauts_de_vue_sont_les_sens_historiques()
    {
        Assert.Equal(Orientation.Horizontal, new CadranFusibleView().Orientation);
        Assert.Equal(Orientation.Vertical, new CadranMareeView().Orientation);
        Assert.Equal(Orientation.Horizontal, new CadranVoletsView().Orientation);
    }

    [WpfFact]
    public void Chaque_variante_fait_son_empreinte_exacte()
    {
        foreach (var (creer, style, o) in ToutesVariantes)
        {
            var vue = creer();
            Orienter(vue, o);
            var empreinte = EmpreinteCadran.Pour(style, o);
            Monter(vue, empreinte);
            var nom = Nom(vue, o);

            Assert.True(vue.DesiredSize == empreinte, $"{nom} : DesiredSize {vue.DesiredSize} ≠ {empreinte}.");
            var g = GabaritVisible(vue);
            Assert.True(g.ActualWidth == empreinte.Width && g.ActualHeight == empreinte.Height,
                $"{nom} : gabarit {g.ActualWidth} × {g.ActualHeight} ≠ {empreinte}.");
        }
    }

    [WpfFact]
    public void Les_corps_de_texte_ne_sont_pas_reduits()
    {
        foreach (var (creer, style, o) in Variantes)
        {
            var vue = creer();
            Orienter(vue, o);
            Monter(vue, EmpreinteCadran.Pour(style, o));
            var nom = Nom(vue, o);

            var textes = DescendantsVisuels<TextBlock>(vue);
            var libelles = textes.Where(EstLibelle).ToList();
            var valeurs = textes.Where(EstValeur).ToList();
            Assert.Equal(2, libelles.Count);
            Assert.Equal(4, valeurs.Count);   // % et décompte superposés, par fenêtre
            foreach (var tb in libelles) Assert.True(tb.FontSize == 11, $"{nom} : libellé « {tb.Text} » en corps {tb.FontSize}.");
            foreach (var tb in valeurs) Assert.True(tb.FontSize == 12, $"{nom} : valeur en corps {tb.FontSize}.");
        }
    }

    [WpfFact]
    public void Les_textes_au_pire_cas_tiennent_dans_leur_boite()
    {
        foreach (var (creer, style, o) in Variantes)
        foreach (var decompte in new[] { false, true })   // chaque TextBlock de valeur est mesuré quand il est visible
        {
            var vue = creer();
            Orienter(vue, o);
            var empreinte = EmpreinteCadran.Pour(style, o);
            Monter(vue, empreinte, decompte);
            var nom = $"{Nom(vue, o)} (décompte = {decompte})";
            var cadre = new Rect(empreinte);

            var valeurs = DescendantsVisuels<TextBlock>(vue).Where(EstValeur).Where(tb => tb.Visibility == Visibility.Visible).ToList();
            Assert.Equal(2, valeurs.Count);
            foreach (var tb in valeurs)
            {
                // Largeur : le slot de mise en page, moins les marges (en hauteur, le slot vaut toute la cellule : ignoré).
                var largeur = LayoutInformation.GetLayoutSlot(tb).Width - tb.Margin.Left - tb.Margin.Right;
                foreach (var texte in TextesPireCas)
                {
                    var w = LargeurTexte(tb, texte);
                    Assert.True(w <= largeur, $"{nom} : « {texte} » ({w:F1}) dépasse la boîte ({largeur:F1}).");
                }

                // Rectangle rendu de la boîte, entièrement dans l'empreinte.
                var boite = Boite(tb, vue);
                Assert.Equal(16, boite.Height);
                var r = Rendu(boite, vue);
                Assert.True(cadre.Contains(r), $"{nom} : boîte de valeur {r} hors de l'empreinte {cadre}.");
            }
        }
    }

    [WpfFact]
    public void La_valeur_de_maree_horizontale_ne_recouvre_pas_la_bande()
    {
        foreach (var (creer, style, o) in Variantes)
        {
            var vue = creer();
            Orienter(vue, o);
            Monter(vue, EmpreinteCadran.Pour(style, o));
            var nom = Nom(vue, o);

            var boites = DescendantsVisuels<TextBlock>(vue).Where(EstValeur).Select(tb => Boite(tb, vue)).Distinct()
                .Select(b => Rendu(b, vue)).ToList();
            Assert.Equal(2, boites.Count);

            var controles = DescendantsVisuels<FrameworkElement>(vue).Where(e => e is FuseBar or TideColumn)
                .Select(c => Rendu(c, vue)).ToList();
            Assert.Equal(2, controles.Count);

            foreach (var c in controles)
            foreach (var b in boites)
            {
                // Recouvrement = intersection d'aire non nulle (des bords qui se touchent ne recouvrent pas).
                var inter = Rect.Intersect(c, b);
                Assert.False(!inter.IsEmpty && inter.Width > 0 && inter.Height > 0,
                    $"{nom} : le contrôle {c} recouvre la boîte de valeur {b}.");
            }
        }
    }

    [WpfFact]
    public void Les_controles_portent_l_orientation_du_gabarit()
    {
        foreach (var (creer, _, _) in ToutesVariantes.DistinctBy(v => v.Style))
        {
            var vue = creer();
            var h = (FrameworkElement)vue.FindName("GabaritHorizontal");
            var v = (FrameworkElement)vue.FindName("GabaritVertical");

            Orientation OrientationDe(FrameworkElement c) => c switch
            {
                FuseBar f => f.Orientation,
                TideColumn t => t.Orientation,
                FlapRow r => r.Orientation,
                _ => throw new InvalidOperationException(),
            };

            var dansH = Descendants<FrameworkElement>(h).Where(e => e is FuseBar or TideColumn or FlapRow).ToList();
            var dansV = Descendants<FrameworkElement>(v).Where(e => e is FuseBar or TideColumn or FlapRow).ToList();
            Assert.Equal(2, dansH.Count);
            Assert.Equal(2, dansV.Count);
            foreach (var c in dansH) Assert.Equal(Orientation.Horizontal, OrientationDe(c));
            foreach (var c in dansV) Assert.Equal(Orientation.Vertical, OrientationDe(c));
        }
    }

    // ------------------------------------------------------------------ Volets (plan 05)

    private static bool EstTuileVolets(TextBlock tb) => tb.Text is "5H" or "7J";

    /// <summary>Plaque chiffrée : la Border parente du Grid qui superpose hachure, filet et chiffres.</summary>
    private static Border Plaque(TextBlock tb)
    {
        var grille = Assert.IsType<Grid>(tb.Parent);
        return Assert.IsType<Border>(grille.Parent);
    }

    [WpfFact]
    public void Volets_les_corps_de_texte_ne_sont_pas_reduits()
    {
        foreach (var (creer, style, o) in VariantesVolets)
        {
            var vue = creer();
            Orienter(vue, o);
            Monter(vue, EmpreinteCadran.Pour(style, o));
            var nom = Nom(vue, o);
            var corpsPlaque = o == OrientationCadran.Horizontal ? 13d : 14d;

            var textes = DescendantsVisuels<TextBlock>(vue);
            var tuiles = textes.Where(EstTuileVolets).ToList();
            var chiffres = textes.Where(EstValeur).ToList();
            Assert.Equal(2, tuiles.Count);
            Assert.Equal(4, chiffres.Count);   // % et décompte superposés, par fenêtre
            foreach (var tb in tuiles) Assert.True(tb.FontSize == 11, $"{nom} : tuile « {tb.Text} » en corps {tb.FontSize}.");
            foreach (var tb in chiffres) Assert.True(tb.FontSize == corpsPlaque, $"{nom} : chiffre de plaque en corps {tb.FontSize} ≠ {corpsPlaque}.");
        }
    }

    [WpfFact]
    public void Volets_les_textes_au_pire_cas_tiennent_dans_la_plaque()
    {
        foreach (var (creer, style, o) in VariantesVolets)
        foreach (var decompte in new[] { false, true })
        {
            var vue = creer();
            Orienter(vue, o);
            var empreinte = EmpreinteCadran.Pour(style, o);
            Monter(vue, empreinte, decompte);
            var nom = $"{Nom(vue, o)} (décompte = {decompte})";
            var largeurAttendue = o == OrientationCadran.Horizontal ? 90d : 56d;
            var cadre = new Rect(empreinte);

            var chiffres = DescendantsVisuels<TextBlock>(vue).Where(EstValeur).Where(tb => tb.Visibility == Visibility.Visible).ToList();
            Assert.Equal(2, chiffres.Count);
            foreach (var tb in chiffres)
            {
                var plaque = Plaque(tb);
                Assert.Equal(largeurAttendue, plaque.ActualWidth);
                foreach (var texte in TextesPireCas)
                {
                    var w = LargeurTexte(tb, texte);
                    Assert.True(w <= plaque.ActualWidth, $"{nom} : « {texte} » ({w:F1}) dépasse la plaque ({plaque.ActualWidth:F1}).");
                }
                var r = Rendu(plaque, vue);
                Assert.True(cadre.Contains(r), $"{nom} : plaque {r} hors de l'empreinte {cadre}.");
            }
        }
    }

    [WpfFact]
    public void Volets_la_hachure_et_le_filet_restent_dans_les_deux_sens()
    {
        foreach (var (creer, style, o) in VariantesVolets)
        {
            var vue = creer();
            Orienter(vue, o);
            Monter(vue, EmpreinteCadran.Pour(style, o));
            var nom = Nom(vue, o);

            Assert.True(DescendantsVisuels<System.Windows.Shapes.Rectangle>(vue).Count == 2, $"{nom} : hachures « plancher » attendues : 2.");
            Assert.True(DescendantsVisuels<Border>(vue).Count(b => b.Height == 1) == 2, $"{nom} : filets attendus : 2.");
        }
    }

    [WpfFact]
    public void Les_volets_verticaux_sont_empiles()
    {
        foreach (var (creer, style, o) in VariantesVolets)
        {
            var vue = creer();
            Orienter(vue, o);
            Monter(vue, EmpreinteCadran.Pour(style, o));
            var nom = Nom(vue, o);
            var (w, h) = o == OrientationCadran.Horizontal ? (62d, 14d) : (16d, 104d);

            var rangees = DescendantsVisuels<FlapRow>(vue);
            Assert.Equal(2, rangees.Count);
            foreach (var r in rangees)
                Assert.True(r.ActualWidth == w && r.ActualHeight == h, $"{nom} : rangée {r.ActualWidth} × {r.ActualHeight} ≠ {w} × {h}.");
        }
    }

    // ------------------------------------------------------------------ Braises (plan 05)

    [WpfFact]
    public void Braises_fait_son_empreinte_exacte()
    {
        var vue = new CadranBraisesView();
        var empreinte = EmpreinteCadran.Pour(CadranStyle.Braises, OrientationCadran.Horizontal);
        Monter(vue, empreinte);
        Assert.Equal(new Size(170, 170), empreinte);
        Assert.True(vue.DesiredSize == empreinte, $"Braises : DesiredSize {vue.DesiredSize} ≠ {empreinte}.");
    }
}
