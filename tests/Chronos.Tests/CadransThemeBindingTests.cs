using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Chronos.Controls;
using Chronos.Theming;
using Chronos.ViewModels;
using Chronos.Views.Cadrans;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Liaison au thème des quatre cadrans alternatifs (THM-04, plan 39-03) : chaque vue montée sous un hôte qui porte les
/// tokens d'un thème (comme <c>MainWindow.ApplyThemeBrushes</c>) prend EXACTEMENT les pinceaux de ce thème — textes,
/// sillons, canaux, volets, tuiles, plaque, hachure et état « en attente ». <c>Assert.Same</c> : l'instance même du
/// dictionnaire de tokens, donc aucune couleur recopiée ni pinceau par défaut ne passe.
/// </summary>
[Collection("XAML WPF")]   // charge du BAML : sérialisé avec les autres classes XAML (voir XamlWpfCollection)
public class CadransThemeBindingTests
{
    private static readonly string[] ClesTexte = { "TextePrincipal", "TexteSecondaire", "TexteSecondaireClair", "PlaqueTexte", "PlaqueTexteEpuise" };

    /// <summary>Monte une vue dans un hôte portant les tokens du thème, puis la met en page (résolution des ressources).</summary>
    private static Border Monter(FrameworkElement vue, ChronosTheme theme, IReadOnlyDictionary<string, Brush> tokens)
    {
        var hote = new Border { DataContext = new CadranPreviewViewModel { SelectedTheme = theme }, Child = vue };
        foreach (var kv in tokens) hote.Resources[kv.Key] = kv.Value;
        hote.Measure(new Size(400, 400));
        hote.Arrange(new Rect(0, 0, 400, 400));
        return hote;
    }

    /// <summary>Descendants logiques (les TextBlock masqués par Visibility y figurent aussi).</summary>
    private static IEnumerable<T> Descendants<T>(DependencyObject racine) where T : DependencyObject
    {
        foreach (var enfant in LogicalTreeHelper.GetChildren(racine).OfType<DependencyObject>())
        {
            if (enfant is T t) yield return t;
            foreach (var d in Descendants<T>(enfant)) yield return d;
        }
    }

    [WpfFact]
    public void Les_quatre_cadrans_suivent_chaque_theme()
    {
        int vuesTestees = 0;
        foreach (var theme in ThemeCatalog.All)
        {
            var tokens = theme.BrushTokens();
            Brush T(string cle) => tokens[cle];

            var vues = new FrameworkElement[]
            {
                new CadranBraisesView(), new CadranFusibleView(), new CadranMareeView(), new CadranVoletsView(),
            };

            foreach (var vue in vues)
            {
                var hote = Monter(vue, theme, tokens);
                var nom = $"{vue.GetType().Name} / {theme.Key}";

                var textes = Descendants<TextBlock>(hote).ToList();
                Assert.True(textes.Count >= 1, $"{nom} : aucun TextBlock vérifié (garde anti-mutisme).");
                foreach (var tb in textes)
                    Assert.True(ClesTexte.Any(c => ReferenceEquals(tb.Foreground, T(c))),
                        $"{nom} : le TextBlock « {tb.Text} » n'a pas un pinceau de texte du thème.");

                switch (vue)
                {
                    case CadranBraisesView:
                    {
                        var anneaux = Descendants<EmberRingControl>(hote).ToList();
                        Assert.Equal(2, anneaux.Count);
                        foreach (var a in anneaux)
                        {
                            Assert.Same(T("Piste5h"), a.AshBrush);   // §11 B3 : braises éteintes = piste
                            Assert.Same(T("CadranAttente"), a.WaitBrush);
                        }

                        // BRA-01 (plan 41-01) : 5 h = 20 braises en 5 groupes de 4 ; hebdo inchangé (12, sans groupe).
                        var (cinq, hebdo) = (anneaux[0], anneaux[1]);
                        Assert.Equal((66.0, 20, 4.0, 4, 13.2), (cinq.Radius, cinq.Count, cinq.PipRadius, cinq.GroupSize, cinq.GroupPitch));
                        Assert.Equal((44.0, 12, 3.6, 1, 0.0), (hebdo.Radius, hebdo.Count, hebdo.PipRadius, hebdo.GroupSize, hebdo.GroupPitch));

                        // Flèche de reset : couleur TickReset du thème (triangle et filet).
                        var triangle = Assert.Single(Descendants<Polygon>(hote));
                        Assert.Same(T("TickReset"), triangle.Fill);
                        var filet = Assert.Single(Descendants<Line>(hote));
                        Assert.Same(T("TickReset"), filet.Stroke);
                        break;
                    }
                    case CadranFusibleView:
                    {
                        var meches = Descendants<FuseBar>(hote).ToList();
                        Assert.Equal(4, meches.Count);   // deux par gabarit, phase 40
                        foreach (var f in meches)
                        {
                            Assert.Same(T("CadranTuile"), f.TrackBrush);
                            Assert.Same(T("TextePrincipal"), f.NotchBrush);
                            Assert.Same(T("CadranAttente"), f.WaitBrush);
                        }
                        LibellesEnSecondaireClair(hote, T("TexteSecondaireClair"), nom);   // §11 M1
                        break;
                    }
                    case CadranMareeView:
                    {
                        var colonnes = Descendants<TideColumn>(hote).ToList();
                        Assert.Equal(4, colonnes.Count);   // deux par gabarit, phase 40
                        foreach (var c in colonnes)
                        {
                            Assert.Same(T("FondCadran"), c.TrackBrush);
                            Assert.Same(T("TextePrincipal"), c.WaterlineBrush);
                            Assert.Same(T("CadranAttente"), c.WaitBrush);
                        }
                        LibellesEnSecondaireClair(hote, T("TexteSecondaireClair"), nom);   // §11 M1
                        break;
                    }
                    case CadranVoletsView:
                    {
                        var rangees = Descendants<FlapRow>(hote).ToList();
                        Assert.Equal(4, rangees.Count);   // deux par gabarit, phase 40
                        foreach (var r in rangees)
                        {
                            Assert.Same(T("TextePrincipal"), r.OnBrush);
                            Assert.Same(T("Piste5h"), r.OffBrush);
                            Assert.Same(T("CadranAttente"), r.WaitBrush);
                        }

                        var bordures = Descendants<Border>(hote).ToList();
                        var tuiles = bordures.Where(b => b.Width == 26).ToList();
                        Assert.Equal(4, tuiles.Count);   // deux par gabarit, phase 40
                        foreach (var t in tuiles) Assert.Same(T("CadranTuile"), t.Background);

                        var filets = bordures.Where(b => b.Height == 1).ToList();
                        Assert.Equal(4, filets.Count);   // deux par gabarit, phase 40
                        foreach (var f in filets) Assert.Same(T("PlaqueFilet"), f.Background);

                        // La silhouette de geste (phase 42) n'est pas une hachure.
                        var hachures = Descendants<Rectangle>(hote).Where(r => !Chronos.Views.ZoneGeste.GetSilhouette(r)).ToList();
                        Assert.Equal(4, hachures.Count);   // deux par gabarit, phase 40
                        foreach (var h in hachures) Assert.Same(T("PlaqueHachure"), h.Fill);
                        break;
                    }
                    default:
                        Assert.Fail($"vue inattendue : {nom}");
                        break;
                }

                vuesTestees++;
            }
        }

        // Anti-mutisme : quatre vues pour chacun des thèmes du catalogue.
        Assert.Equal(4 * ThemeCatalog.All.Count, vuesTestees);
    }

    /// <summary>§11 M1 : les libellés « 5 H » / « 7 J » (deux gabarits) portent TexteSecondaireClair.</summary>
    private static void LibellesEnSecondaireClair(DependencyObject hote, Brush attendu, string nom)
    {
        var libelles = Descendants<TextBlock>(hote).Where(tb => tb.Text is "5 H" or "7 J").ToList();
        Assert.True(libelles.Count == 4, $"{nom} : libellés 5 H / 7 J attendus : 4 (deux par gabarit), trouvés {libelles.Count}.");
        foreach (var tb in libelles)
            Assert.True(ReferenceEquals(attendu, tb.Foreground), $"{nom} : le libellé « {tb.Text} » n'est pas en TexteSecondaireClair.");
    }

    /// <summary>Chemin de liaison du Text d'un TextBlock (vide si aucun).</summary>
    private static string CheminTexte(TextBlock tb)
        => System.Windows.Data.BindingOperations.GetBindingExpression(tb, TextBlock.TextProperty)?.ParentBinding.Path?.Path ?? "";

    /// <summary>§11 B3 : sur le gris épuisé, les chiffres de la plaque Volets passent à PlaqueTexteEpuise (≥ 4,5:1), dans les
    /// deux orientations et sur les 15 thèmes ; la plaque d'une fenêtre non épuisée garde PlaqueTexte.</summary>
    [WpfFact]
    public void Volets_le_texte_de_plaque_suit_l_epuisement()
    {
        int cas = 0;
        foreach (var theme in ThemeCatalog.All)
        foreach (var orientation in new[] { Orientation.Horizontal, Orientation.Vertical })
        {
            var tokens = theme.BrushTokens();
            var vm = new CadranPreviewViewModel { SelectedTheme = theme, FiveQuotaPct = 100, SevenQuotaPct = 38 };
            Assert.True(vm.FiveHour.Exhausted);
            Assert.False(vm.SevenDay.Exhausted);

            var vue = new CadranVoletsView { Orientation = orientation };
            var hote = new Border { DataContext = vm, Child = vue };
            foreach (var kv in tokens) hote.Resources[kv.Key] = kv.Value;
            hote.Measure(new Size(400, 400));
            hote.Arrange(new Rect(0, 0, 400, 400));
            hote.UpdateLayout();

            var gabarit = (FrameworkElement)vue.FindName(orientation == Orientation.Horizontal ? "GabaritHorizontal" : "GabaritVertical");
            var textes = Descendants<TextBlock>(gabarit).ToList();
            var chiffres5h = textes.Where(tb => CheminTexte(tb).StartsWith("FiveHour.", StringComparison.Ordinal)).ToList();
            var chiffres7j = textes.Where(tb => CheminTexte(tb).StartsWith("SevenDay.", StringComparison.Ordinal)).ToList();
            Assert.Equal(2, chiffres5h.Count);   // % et décompte superposés
            Assert.Equal(2, chiffres7j.Count);
            var nom = $"Volets {orientation} / {theme.Key}";
            foreach (var tb in chiffres5h)
                Assert.True(ReferenceEquals(tokens["PlaqueTexteEpuise"], tb.Foreground), $"{nom} : chiffre 5 h épuisé pas en PlaqueTexteEpuise.");
            foreach (var tb in chiffres7j)
                Assert.True(ReferenceEquals(tokens["PlaqueTexte"], tb.Foreground), $"{nom} : chiffre 7 j pas en PlaqueTexte.");

            Assert.True(ContrasteWcag.Ratio(theme.PlaqueTexteEpuise, theme.Epuise) >= 4.5, $"{nom} : PlaqueTexteEpuise < 4,5:1 sur Epuise.");
            cas++;
        }
        Assert.Equal(2 * ThemeCatalog.All.Count, cas);
    }

    /// <summary>Preuve du <c>DynamicResource</c> (et non <c>StaticResource</c>) : recopier les tokens d'un autre thème dans
    /// le MÊME hôte rafraîchit les pinceaux déjà résolus, comme le fait l'overlay au changement de thème.</summary>
    [WpfFact]
    public void Changer_de_theme_rafraichit_les_pinceaux()
    {
        var minuit = ThemeCatalog.Default;
        var lave = ThemeCatalog.All.Single(t => t.Key == "lave");

        var vue = new CadranVoletsView();
        var hote = Monter(vue, minuit, minuit.BrushTokens());
        var rangee = Descendants<FlapRow>(hote).First();
        Assert.Same(hote.Resources["TextePrincipal"], rangee.OnBrush);

        var tokensLave = lave.BrushTokens();
        foreach (var kv in tokensLave) hote.Resources[kv.Key] = kv.Value;

        Assert.Same(tokensLave["TextePrincipal"], rangee.OnBrush);
        Assert.Same(tokensLave["CadranAttente"], rangee.WaitBrush);
    }
}
