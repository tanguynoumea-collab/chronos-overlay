using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Chronos.Controls.Historique;
using Chronos.Models.Historique;
using Chronos.Services.Historique;
using Chronos.Text;
using Chronos.Theming;
using Chronos.ViewModels.Historique;
using Chronos.Views.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// 35-04 — banc commun des tests de la vue 4 semaines (mise en page et honnêteté) : le VM sur <see cref="FakeSourceHistorique"/>
/// (transformation optionnelle des données, ex. la fixture « S-1 épuisée »), horloge figée du scénario, vue montée SEULE.
/// </summary>
internal static class BancQuatreSemaines
{
    public static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();

    public static (HistoriqueViewModel Vm, VueQuatreSemainesView Vue) Monter(
        Func<DonneesQuatreSemaines, DonneesQuatreSemaines>? transformer = null, double largeur = 920, double hauteur = 900)
    {
        var source = new FakeSourceHistorique(Tz) { TransformerQuatreSemaines = transformer };
        var vm = new HistoriqueViewModel(source, new FakeUiDispatcher { OnUiThread = true },
                                         ScenariosHistorique.HorlogeFigee(Tz), Tz, new ReglagesHistoriqueMemoire());
        vm.Ouvrir();
        vm.AttendreLecture().GetAwaiter().GetResult();
        vm.ChoisirVueCommand.Execute(VueHistorique.QuatreSemaines);
        vm.AttendreLecture().GetAwaiter().GetResult();

        var vue = new VueQuatreSemainesView { DataContext = vm };
        MettreEnPage(vue, largeur, hauteur);
        return (vm, vue);
    }

    /// <summary>Measure/Arrange deux fois : les Canvas.Left et largeurs bindés sur ActualWidth ne sont justes qu'après une première passe.</summary>
    public static void MettreEnPage(FrameworkElement e, double largeur, double hauteur)
    {
        Idle(e);
        e.Measure(new Size(largeur, hauteur));
        e.Arrange(new Rect(0, 0, largeur, hauteur));
        Idle(e);
        e.Measure(new Size(largeur, hauteur));
        e.Arrange(new Rect(0, 0, largeur, hauteur));
        e.UpdateLayout();
    }

    public static void Idle(DispatcherObject o) => o.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    /// <summary>Parcours de l'arbre visuel dans l'ordre du document, sans entrer dans un sous-arbre Collapsed / Hidden.</summary>
    public static IEnumerable<T> Visibles<T>(DependencyObject racine) where T : DependencyObject
    {
        if (racine is UIElement ui && ui.Visibility != Visibility.Visible) yield break;
        if (racine is T t) yield return t;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(racine); i++)
            foreach (var x in Visibles<T>(VisualTreeHelper.GetChild(racine, i)))
                yield return x;
    }

    public static List<string> TextesVisibles(DependencyObject racine) => Visibles<TextBlock>(racine).Select(t => t.Text).ToList();

    /// <summary>Abscisse du coin haut-gauche de <paramref name="e"/> dans le repère de <paramref name="reference"/>.</summary>
    public static double XDans(FrameworkElement e, UIElement reference) => e.TranslatePoint(new Point(0, 0), reference).X;

    public static double YDans(FrameworkElement e, UIElement reference) => e.TranslatePoint(new Point(0, 0), reference).Y;

    /// <summary>Le conteneur de la rangée de couverture <paramref name="rang"/> (0 = S … 3 = S-3, ordre du VM).</summary>
    public static FrameworkElement Rangee(VueQuatreSemainesView vue, int rang)
    {
        var rangees = Assert.IsType<ItemsControl>(vue.FindName("RangeesCouverture"));
        return Assert.IsAssignableFrom<FrameworkElement>(rangees.ItemContainerGenerator.ContainerFromIndex(rang));
    }

    public static PisteQuatreSemaines Piste(VueQuatreSemainesView vue) => Assert.Single(Visibles<PisteQuatreSemaines>(vue));

    /// <summary>Rend une piste hors écran pour lire sa trace de rendu ; la trace n'est activée que le temps du rendu.</summary>
    public static List<string> Trace(PisteBase piste)
    {
        PisteBase.TracerPourTests = true;
        try
        {
            piste.InvalidateVisual();
            piste.UpdateLayout();
            var bmp = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(piste.ActualWidth)), Math.Max(1, (int)Math.Ceiling(piste.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
            bmp.Render(piste);
            return piste.TraceRendu.ToList();
        }
        finally
        {
            PisteBase.TracerPourTests = false;
        }
    }

    public static Color CouleurDe(Brush? b) => Assert.IsType<SolidColorBrush>(b).Color;
}

/// <summary>
/// HIS-05 — test de mise en page RÉEL de la vue « 4 semaines » (35-04, DESIGN_PLAN §2.4) : hauteurs, largeurs et opacités lues
/// sur l'arbre visuel après Measure/Arrange (jamais recopiées en C# hors de la table des tokens), axe « sam. … ven. » +
/// « reset hebdo → », étiquettes entières à droite, quatre rangées de couverture au pas des tokens, 760 → 1 400 px.
/// Collection sérialisée : course du chargeur BAML.
/// </summary>
[Collection("XAML WPF")]
public class VueQuatreSemainesBindingTests
{
    private static readonly string[] JoursCourts = { "sam.", "dim.", "lun.", "mar.", "mer.", "jeu.", "ven." };

    [WpfFact]
    public void La_vue_a_les_hauteurs_et_largeurs_des_tokens()
    {
        var (_, vue) = BancQuatreSemaines.Monter();

        // Le token → la DP Height, exacte ; l'ActualHeight est arrondie au pixel physique (UseLayoutRounding, 125 % DPI : 250 → 249,6),
        // d'où ± 0,5 (précédent VueJourBindingTests).
        var piste = BancQuatreSemaines.Piste(vue);
        Assert.Equal(250, piste.Height);
        Assert.InRange(piste.ActualHeight, 249.5, 250.5);

        var couvertures = BancQuatreSemaines.Visibles<PisteCouverture>(vue).ToList();
        Assert.Equal(4, couvertures.Count);
        Assert.All(couvertures, c => Assert.Equal(10, c.Height));
        Assert.All(couvertures, c => Assert.InRange(c.ActualHeight, 9.5, 10.5));
        for (var i = 1; i < 4; i++)
            Assert.InRange(BancQuatreSemaines.YDans(couvertures[i], vue) - BancQuatreSemaines.YDans(couvertures[i - 1], vue), 15.5, 16.5);

        // Les rangées de couverture sont alignées sur la piste NIVEAU (même colonne, même axe).
        foreach (var c in couvertures)
        {
            Assert.Equal(piste.ActualWidth, c.ActualWidth, 1);
            Assert.Equal(BancQuatreSemaines.XDans(piste, vue), BancQuatreSemaines.XDans(c, vue), 1);
        }

        var etiquettes = Assert.IsAssignableFrom<FrameworkElement>(vue.FindName("Etiquettes"));
        Assert.InRange(etiquettes.ActualWidth, 0.5, 140);

        Assert.Empty(BancQuatreSemaines.Visibles<PisteTokens>(vue));
        Assert.Empty(BancQuatreSemaines.Visibles<PisteRythme>(vue));
        Assert.Empty(BancQuatreSemaines.Visibles<PisteNiveau>(vue));
        Assert.Empty(BancQuatreSemaines.Visibles<SurcoucheReticule>(vue));   // D-35-16 : pas de réticule en 4 semaines
    }

    [WpfFact]
    public void La_piste_recoit_les_opacites_des_tokens()
    {
        var (vm, vue) = BancQuatreSemaines.Monter();
        var piste = BancQuatreSemaines.Piste(vue);

        Assert.Equal(0.8, piste.OpaciteS1);
        Assert.Equal(0.45, piste.OpaciteS2);
        Assert.Equal(0.25, piste.OpaciteS3);
        Assert.Equal(2.2, piste.EpaisseurEscalier);
        Assert.Equal(ThemeCatalog.Default.Epuise, BancQuatreSemaines.CouleurDe(piste.Gris)); // gris épuisé lisible du thème — phase 39, HistoGris reste pour hachure et repère
        Assert.Same(vm.Theme, piste.Rampe);
        Assert.Same(vm.DonneesQuatreSemaines!.Semaines, piste.Semaines);
        Assert.Equal(vm.DonneesQuatreSemaines.Courante.Plage, piste.Plage);
    }

    [WpfFact]
    public void Le_gris_epuise_des_quatre_semaines_suit_le_theme()
    {
        var (_, vue) = BancQuatreSemaines.Monter();
        var lave = ThemeCatalog.ByKey("lave");
        foreach (var kv in lave.BrushTokens())
            vue.Resources[kv.Key] = kv.Value;   // comme HistoriqueWindow : l'entrée locale prime sur le repli statique
        BancQuatreSemaines.MettreEnPage(vue, 920, 900);

        var piste = BancQuatreSemaines.Piste(vue);
        Assert.Equal(lave.Epuise, BancQuatreSemaines.CouleurDe(piste.Gris));   // le gris épuisé du thème Lave (token), pas HistoGris
        Assert.NotEqual(Color.FromRgb(0x5A, 0x59, 0x60), lave.Epuise);           // HistoGris : le gris de l'Historique ne se substitue pas au token
    }

    [WpfFact]
    public void L_axe_dit_samedi_a_vendredi_et_reset_hebdo()
    {
        var (_, vue) = BancQuatreSemaines.Monter();
        var textes = BancQuatreSemaines.Visibles<TextBlock>(vue).ToList();

        foreach (var jour in JoursCourts)
            Assert.Single(textes, t => t.Text == jour);
        Assert.Contains(textes, t => t.Text == TextesHistorique.ResetHebdo);

        var piste = BancQuatreSemaines.Piste(vue);
        var samedi = Assert.Single(textes, t => t.Text == "sam.");
        Assert.InRange(BancQuatreSemaines.XDans(samedi, piste), -1, 1);
        var vendredi = Assert.Single(textes, t => t.Text == "ven.");
        Assert.InRange(BancQuatreSemaines.XDans(vendredi, piste), piste.ActualWidth * 6 / 7 - 1, piste.ActualWidth * 6 / 7 + 1);
    }

    [WpfFact]
    public void Les_etiquettes_sont_a_droite_entieres_et_dans_l_ordre()
    {
        var (vm, vue) = BancQuatreSemaines.Monter();
        var colonne = Assert.IsAssignableFrom<FrameworkElement>(vue.FindName("Etiquettes"));
        var etiquettes = BancQuatreSemaines.Visibles<TextBlock>(colonne).ToList();

        Assert.Equal(vm.EtiquettesSemaines.Select(e => e.Texte).ToList(), etiquettes.Select(t => t.Text).ToList());
        Assert.Equal(4, etiquettes.Count);
        Assert.StartsWith("S · ", etiquettes[0].Text);
        Assert.StartsWith("S-3 · ", etiquettes[3].Text);
        foreach (var t in etiquettes)
        {
            Assert.Equal(TextWrapping.Wrap, t.TextWrapping);
            Assert.Equal(TextTrimming.None, t.TextTrimming);
            Assert.InRange(t.ActualWidth, 0.5, 140);
        }

        // À droite de la piste.
        var piste = BancQuatreSemaines.Piste(vue);
        Assert.True(BancQuatreSemaines.XDans(colonne, vue) >= BancQuatreSemaines.XDans(piste, vue) + piste.ActualWidth - 0.5);
    }

    [WpfFact]
    public void A_760_px_rien_ne_deborde_et_a_1400_px_tout_s_etire()
    {
        var (_, etroite) = BancQuatreSemaines.Monter(largeur: 760, hauteur: 480);
        Assert.True(etroite.DesiredSize.Width <= 760, $"DesiredSize.Width = {etroite.DesiredSize.Width}");
        var colonne = Assert.IsAssignableFrom<FrameworkElement>(etroite.FindName("Etiquettes"));
        Assert.All(BancQuatreSemaines.Visibles<TextBlock>(colonne), t => Assert.InRange(t.ActualWidth, 0.5, 140));

        var avant = BancQuatreSemaines.Visibles<TextBlock>(etroite).Where(t => t.Text == TextesHistorique.AvantJournalAucunReleve).ToList();
        Assert.Equal(2, avant.Count);
        foreach (var t in avant)
        {
            var rangee = Assert.IsAssignableFrom<FrameworkElement>(VisualTreeHelper.GetParent(t));
            Assert.True(t.ActualWidth <= rangee.ActualWidth, $"« {t.Text} » ({t.ActualWidth}) déborde de sa rangée ({rangee.ActualWidth})");
            Assert.True(t.DesiredSize.Width <= rangee.ActualWidth + 0.5, "le texte serait coupé");
        }

        var (_, large) = BancQuatreSemaines.Monter(largeur: 1400, hauteur: 900);
        Assert.True(BancQuatreSemaines.Piste(large).ActualWidth > 1000, $"piste à 1 400 px : {BancQuatreSemaines.Piste(large).ActualWidth}");
    }
}
