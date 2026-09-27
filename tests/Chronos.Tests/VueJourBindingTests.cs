using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Chronos.Controls.Historique;
using Chronos.Models.Historique;
using Chronos.Services.Historique;
using Chronos.Text;
using Chronos.ViewModels.Historique;
using Chronos.Views.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-04 — test de mise en page RÉEL de la vue « Jour » (34-07) : la table §2.3 du plan de design (NIVEAU 190 / RYTHME 64 /
/// TOKENS 64 / COUVERTURE 12) est lue sur l'arbre visuel après Measure/Arrange, jamais recopiée en C# ; la variante Jour de la
/// piste Niveau (5 h au premier plan 2,4 px, hebdo en trait fin Ink2), les colonnes par quart d'heure empilées par modèle, les
/// huit heures locales de l'axe, la légende des modèles et le libellé permanent « par quart d'heure, … », la grille unique qui
/// défile. La vue est montée SEULE (elle fusionne DesignTokens.xaml elle-même) sur le scénario de référence (jeu. 24 sept. 2026,
/// 17:12 Paris). Collection sérialisée : course du chargeur BAML.
/// </summary>
[Collection("XAML WPF")]
public class VueJourBindingTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();

    /// <summary>Monte la vue Jour sur le scénario, <paramref name="joursEnArriere"/> × ‹ (0 = aujourd'hui, jeu. 24 ; 1 = mer. 23).</summary>
    private static (HistoriqueViewModel Vm, VueJourView Vue) Monter(int joursEnArriere = 0, double largeur = 920, double hauteur = 900)
    {
        var vm = new HistoriqueViewModel(new SourceHistoriqueDemonstration(Tz), new FakeUiDispatcher { OnUiThread = true },
                                         ScenariosHistorique.HorlogeFigee(Tz), Tz, new ReglagesHistoriqueMemoire());
        vm.Ouvrir();
        vm.AttendreLecture().GetAwaiter().GetResult();
        vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        vm.AttendreLecture().GetAwaiter().GetResult();
        for (var i = 0; i < joursEnArriere; i++)
        {
            vm.PrecedentCommand.Execute(null);
            vm.AttendreLecture().GetAwaiter().GetResult();
        }

        var vue = new VueJourView { DataContext = vm };
        MettreEnPage(vue, largeur, hauteur);
        return (vm, vue);
    }

    /// <summary>Measure/Arrange deux fois : les Canvas.Left bindés sur ActualWidth ne sont justes qu'après une première passe.</summary>
    private static void MettreEnPage(FrameworkElement e, double largeur, double hauteur)
    {
        Idle(e);
        e.Measure(new Size(largeur, hauteur));
        e.Arrange(new Rect(0, 0, largeur, hauteur));
        Idle(e);
        e.Measure(new Size(largeur, hauteur));
        e.Arrange(new Rect(0, 0, largeur, hauteur));
        e.UpdateLayout();
    }

    private static void Idle(DispatcherObject o) => o.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    /// <summary>Parcours de l'arbre visuel dans l'ordre du document, sans entrer dans un sous-arbre Collapsed.</summary>
    private static IEnumerable<T> Visibles<T>(DependencyObject racine) where T : DependencyObject
    {
        if (racine is UIElement { Visibility: Visibility.Collapsed }) yield break;
        if (racine is T t) yield return t;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(racine); i++)
            foreach (var x in Visibles<T>(VisualTreeHelper.GetChild(racine, i)))
                yield return x;
    }

    private static List<string> TextesVisibles(DependencyObject racine) => Visibles<TextBlock>(racine).Select(t => t.Text).ToList();

    /// <summary>Les pistes visibles dans l'ordre du document (la surcouche est une PisteBase mais pas une piste de données).</summary>
    private static List<PisteBase> PistesVisibles(DependencyObject racine)
        => Visibles<PisteBase>(racine).Where(p => p is not SurcoucheReticule).ToList();

    private static Color CouleurDe(Brush? b) => Assert.IsType<SolidColorBrush>(b).Color;

    private static Color Hex(string rrggbb) => (Color)ColorConverter.ConvertFromString("#" + rrggbb);

    private static T Ancetre<T>(DependencyObject e) where T : DependencyObject
    {
        for (var p = VisualTreeHelper.GetParent(e); p is not null; p = VisualTreeHelper.GetParent(p))
            if (p is T t) return t;
        throw new Xunit.Sdk.XunitException($"aucun ancêtre {typeof(T).Name} pour {e}");
    }

    /// <summary>Abscisse du coin haut-gauche de <paramref name="e"/> dans le repère de son ancêtre (le Canvas.Left est posé sur le conteneur d'item).</summary>
    private static double XDans(FrameworkElement e, Visual ancetre) => e.TransformToAncestor(ancetre).Transform(new Point(0, 0)).X;

    // ------------------------------------------------------------------ Task 1 : grille, hauteurs, axe, légendes

    [WpfFact]
    public void La_vue_Jour_a_exactement_les_pistes_et_hauteurs_du_plan()
    {
        var (vm, vue) = Monter();

        var pistes = PistesVisibles(vue);
        Assert.Equal(new[] { typeof(PisteNiveau), typeof(PisteRythme), typeof(PisteTokens), typeof(PisteCouverture) }, pistes.Select(p => p.GetType()).ToArray());
        // Le token → la DP Height, exacte ; l'ActualHeight est arrondie au pixel physique (UseLayoutRounding, 125 % DPI : 190 → 190,4).
        Assert.Equal(new[] { 190d, 64d, 64d, 12d }, pistes.Select(p => p.Height).ToArray());
        Assert.All(pistes.Zip(new[] { 190d, 64d, 64d, 12d }), x => Assert.InRange(x.First.ActualHeight, x.Second - 0.5, x.Second + 0.5));

        var niveau = (PisteNiveau)pistes[0];
        Assert.Equal(VariantePisteNiveau.Jour, niveau.Variante);
        Assert.Equal(2.4, niveau.EpaisseurPremierPlan);
        Assert.Equal(1.0, niveau.EpaisseurFine);
        Assert.Equal(Hex("A9A6C4"), CouleurDe(niveau.TraitFin));           // Ink2 : l'hebdo en trait fin
        Assert.Equal(Hex("5A5960"), CouleurDe(niveau.Gris));               // HistoGris : « épuisée »
        Assert.Equal(Hex("F4F2EC"), CouleurDe(niveau.TraitReset));         // TickReset (thème par défaut)
        Assert.Same(vm.Theme, niveau.Rampe);
        Assert.Same(vm.DonneesJour!.Analyse, niveau.Analyse);
        Assert.Same(vm.DonneesJour.Plage, niveau.Plage);
        Assert.Equal(vm.DonneesJour.LueA, niveau.InstantLecture);

        var tokens = (PisteTokens)pistes[2];
        Assert.Same(vm.DonneesJour.Colonnes, tokens.Colonnes);
        Assert.Equal(96, tokens.Colonnes!.Count);
        Assert.Null(tokens.Barres);
        Assert.Equal(Hex("9D9AB8"), CouleurDe(tokens.Modele1));
        Assert.Equal(Hex("6E6B8C"), CouleurDe(tokens.Modele2));
        Assert.Equal(Hex("4A4762"), CouleurDe(tokens.Modele3));
        Assert.IsType<DrawingBrush>(tokens.Hachure);
        Assert.Equal(vm.PlafondTokens, tokens.Plafond);
        Assert.Equal(vm.DonneesJour.LueA, tokens.InstantLecture);

        var rythme = (PisteRythme)pistes[1];
        Assert.Same(vm.DonneesJour.Analyse.Deltas5h, rythme.Deltas);
        Assert.Same(vm.Theme, rythme.Rampe);

        var couverture = (PisteCouverture)pistes[3];
        Assert.Same(vm.DonneesJour.Analyse.Trous, couverture.Trous);
        Assert.Equal(Hex("4EC98A"), CouleurDe(couverture.Present));
        Assert.Equal(0.55, couverture.OpacitePresent);
    }

    [WpfFact]
    public void L_axe_des_heures_a_huit_libelles_locaux()
    {
        var (vm, vue) = Monter();
        var textes = TextesVisibles(vue);

        Assert.Equal(8, vm.LibellesHeures.Count);
        foreach (var heure in new[] { "0 h", "3 h", "6 h", "9 h", "12 h", "15 h", "18 h", "21 h" })
            Assert.Contains(heure, textes);

        var midi = Assert.Single(Visibles<TextBlock>(vue), t => t.Text == "12 h");
        var canvas = Ancetre<Canvas>(midi);
        Assert.True(canvas.ActualWidth > 500, $"l'axe des heures doit avoir la largeur de la colonne des pistes (ActualWidth={canvas.ActualWidth})");
        Assert.InRange(XDans(midi, canvas), canvas.ActualWidth / 2 - 1, canvas.ActualWidth / 2 + 1);   // jour de 24 h : midi au milieu
        Assert.InRange(XDans(Assert.Single(Visibles<TextBlock>(vue), t => t.Text == "0 h"), canvas), -1, 1);
    }

    [WpfFact]
    public void Les_libelles_de_pistes_la_legende_et_le_libelle_permanent_sont_ceux_du_jour()
    {
        var (vm, vue) = Monter();
        var textes = TextesVisibles(vue);

        var nomsDePistes = new[] { TextesHistorique.PisteNiveau, TextesHistorique.PisteRythme, TextesHistorique.PisteTokens, TextesHistorique.PisteCouverture, TextesHistorique.PisteFenetres5h };
        Assert.Equal(new[] { "NIVEAU", "RYTHME", "TOKENS CLAUDE CODE", "COUVERTURE" }, textes.Where(nomsDePistes.Contains).ToArray());

        Assert.Equal("opus · sonnet · haiku · sous-agents inclus", vm.LegendeModeles);
        Assert.Contains(vm.LegendeModeles, textes);
        Assert.StartsWith("par quart d'heure, comptés localement — hors Cowork et claude.ai", vm.LibellePermanentTokens, StringComparison.Ordinal);
        Assert.Contains(vm.LibellePermanentTokens, textes);
        Assert.Contains(TextesHistorique.PiedDePage, textes);
        Assert.Equal("0 – 25 %", vm.EchelleRythme);
        Assert.Contains(vm.EchelleRythme, textes);
        Assert.StartsWith("0 – ", vm.EchelleTokens, StringComparison.Ordinal);
        Assert.Contains(vm.EchelleTokens, textes);
        Assert.Contains("100 %", textes);
        Assert.Contains("0", textes);

        // La ligne de fraîcheur est affichée par la fenêtre, pas par la vue : ici on vérifie que la vue Jour active produit bien ce texte.
        Assert.StartsWith("288 relevés attendus · ", vm.TexteFraicheur, StringComparison.Ordinal);
        Assert.Contains("1 interruption (jeton invalide, 14:00 → 16:00)", vm.TexteFraicheur, StringComparison.Ordinal);
    }

    [WpfFact]
    public void La_grille_est_unique_et_defile_dans_le_corps_de_la_fenetre_minimale()
    {
        var (_, vue) = Monter();

        Assert.DoesNotContain(Visibles<FrameworkElement>(vue), e => e.Name.StartsWith("Style", StringComparison.Ordinal));   // D-34-31 : une seule grille
        var defilement = Assert.Single(Visibles<ScrollViewer>(vue));
        Assert.Equal(ScrollBarVisibility.Disabled, defilement.HorizontalScrollBarVisibility);

        // 760 × 360 : le corps de la fenêtre minimale (480 − en-tête 92 − marges) — la vue ne déborde pas en largeur et défile en hauteur.
        MettreEnPage(vue, 760, 360);
        Assert.True(vue.DesiredSize.Width <= 760, $"DesiredSize.Width={vue.DesiredSize.Width}");
        Assert.True(defilement.ScrollableHeight > 0, $"à 360 px de haut le corps doit défiler (ScrollableHeight={defilement.ScrollableHeight})");
        Assert.All(PistesVisibles(vue), p => Assert.True(p.ActualWidth > 0 && p.ActualWidth <= 760 - 96 - 72, $"{p.GetType().Name}: {p.ActualWidth}"));

        MettreEnPage(vue, 1400, 900);
        Assert.True(PistesVisibles(vue).OfType<PisteNiveau>().Single().ActualWidth > 1000);
        Assert.Equal(0, defilement.ScrollableHeight);
    }
}
