using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Chronos.Controls.Historique;
using Chronos.Models.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.ViewModels.Historique;
using Chronos.Views.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-02 / HIS-03 — la vue « Semaine de forfait » (34-06) mesurée sur l'ARBRE VISUEL RÉEL : pour chaque style, l'ordre des pistes
/// visibles et leur <c>ActualHeight</c> exacts (table DESIGN_PLAN §2.2 : 150 / 72 / 72 / 12 · 200 / 90 / 12 · 120 / 62 / 58 / 58 / 12),
/// la variante de la piste Niveau, la bascule de style à chaud, l'axe des jours commun et les libellés de pistes.
///
/// Ce que <c>dotnet build</c> ne voit pas : une piste posée dans la mauvaise grille, une hauteur prise au mauvais token, une grille
/// qui reste visible après la bascule. La vue est montée SEULE (elle fusionne <c>DesignTokens.xaml</c> elle-même), sur le scénario de
/// référence (<c>SourceHistoriqueDemonstration</c>, horloge figée, réglages en mémoire) — aucun fichier, aucun exe.
/// Collection sérialisée obligatoire (course du chargeur BAML, Pitfall 12).
/// </summary>
[Collection("XAML WPF")]
public class VueSemaineBindingTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();

    private sealed record Banc(VueSemaineView Vue, HistoriqueViewModel Vm);

    /// <summary>Monte la vue seule (920 × 900) sur le scénario de référence, dans le style demandé, au présent ou sur S-1.</summary>
    private static Banc Monter(HistoriqueStyleSemaine style = HistoriqueStyleSemaine.Pistes, bool semainePrecedente = false,
                               ISourceHistorique? source = null)
    {
        var reglages = new ReglagesHistoriqueMemoire();
        reglages.Modifier(s => s with { HistoriqueStyleSemaine = style });
        var vm = new HistoriqueViewModel(source ?? new SourceHistoriqueDemonstration(Tz), new FakeUiDispatcher { OnUiThread = true },
                                         ScenariosHistorique.HorlogeFigee(Tz), Tz, reglages);
        vm.Ouvrir();
        vm.AttendreLecture().GetAwaiter().GetResult();
        if (semainePrecedente)
        {
            vm.PrecedentCommand.Execute(null);
            vm.AttendreLecture().GetAwaiter().GetResult();
        }

        var vue = new VueSemaineView { DataContext = vm };
        MettreEnPage(vue, 920, 900);
        return new Banc(vue, vm);
    }

    /// <summary>Measure / Arrange puis un tour de dispatcher (les bindings sur <c>ActualWidth</c> se propagent) et une seconde passe.</summary>
    private static void MettreEnPage(FrameworkElement vue, double largeur, double hauteur)
    {
        vue.Measure(new Size(largeur, hauteur));
        vue.Arrange(new Rect(0, 0, largeur, hauteur));
        Idle(vue);
        vue.UpdateLayout();
    }

    private static void Idle(DispatcherObject o) => o.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    /// <summary>Parcours de l'arbre visuel dans l'ordre du document, SANS entrer dans un sous-arbre <c>Collapsed</c> / <c>Hidden</c>.</summary>
    private static IEnumerable<T> Visibles<T>(DependencyObject racine) where T : DependencyObject
    {
        if (racine is UIElement ui && ui.Visibility != Visibility.Visible) yield break;
        if (racine is T t) yield return t;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(racine); i++)
            foreach (var x in Visibles<T>(VisualTreeHelper.GetChild(racine, i)))
                yield return x;
    }

    /// <summary>Les pistes visibles (hors surcouche réticule, qui dérive aussi de <c>PisteBase</c>), dans l'ordre du document.</summary>
    private static List<PisteBase> PistesVisibles(DependencyObject racine)
        => Visibles<PisteBase>(racine).Where(p => p is not SurcoucheReticule).ToList();

    private static List<string> TextesVisibles(DependencyObject racine) => Visibles<TextBlock>(racine).Select(t => t.Text).ToList();

    private static Grid Grille(VueSemaineView vue, string nom) => Assert.IsType<Grid>(vue.FindName(nom));

    private static Grid GrilleVisible(VueSemaineView vue)
        => Assert.Single(new[] { "StylePistes", "StyleSimplifie", "StyleTuiles" }.Select(n => Grille(vue, n)), g => g.Visibility == Visibility.Visible);

    private static void VerifierPistes(List<PisteBase> pistes, params (Type Type, double Hauteur)[] attendues)
    {
        Assert.Equal(attendues.Select(a => a.Type).ToList(), pistes.Select(p => p.GetType()).ToList());
        for (var i = 0; i < attendues.Length; i++)
            Assert.Equal(attendues[i].Hauteur, pistes[i].ActualHeight);
    }

    private static readonly string[] LibellesPistes = { "NIVEAU", "FENÊTRES 5 H", "RYTHME", "TOKENS CLAUDE CODE", "COUVERTURE" };

    // ------------------------------------------------------------------ Table §2.2 par style (HIS-03)

    [WpfFact]
    public void Le_style_Pistes_a_exactement_les_pistes_et_hauteurs_du_plan()
    {
        var (vue, _) = Monter(HistoriqueStyleSemaine.Pistes);

        var pistes = PistesVisibles(vue);
        VerifierPistes(pistes, (typeof(PisteNiveau), 150d), (typeof(PisteRythme), 72d), (typeof(PisteTokens), 72d), (typeof(PisteCouverture), 12d));
        Assert.Equal(VariantePisteNiveau.SemaineComplete, ((PisteNiveau)pistes[0]).Variante);
        Assert.Empty(Visibles<PisteFenetres5h>(vue));
    }

    [WpfFact]
    public void Le_style_Simplifie_a_exactement_les_pistes_et_hauteurs_du_plan()
    {
        var (vue, _) = Monter(HistoriqueStyleSemaine.Simplifie);

        var pistes = PistesVisibles(vue);
        VerifierPistes(pistes, (typeof(PisteNiveau), 200d), (typeof(PisteTokens), 90d), (typeof(PisteCouverture), 12d));
        Assert.Equal(VariantePisteNiveau.SemaineComplete, ((PisteNiveau)pistes[0]).Variante);
        Assert.Empty(Visibles<PisteRythme>(vue));
    }

    [WpfFact]
    public void Le_style_Tuiles_a_exactement_les_pistes_et_hauteurs_du_plan()
    {
        var (vue, _) = Monter(HistoriqueStyleSemaine.Tuiles);

        var pistes = PistesVisibles(vue);
        VerifierPistes(pistes, (typeof(PisteNiveau), 120d), (typeof(PisteFenetres5h), 62d), (typeof(PisteRythme), 58d),
                       (typeof(PisteTokens), 58d), (typeof(PisteCouverture), 12d));
        Assert.Equal(VariantePisteNiveau.SemaineHebdoSeul, ((PisteNiveau)pistes[0]).Variante);
    }

    // ------------------------------------------------------------------ Bascule à chaud, axe des jours, libellés

    [WpfFact]
    public void La_bascule_de_style_a_chaud_change_la_grille_visible_et_l_axe_des_jours_est_commun()
    {
        var (vue, vm) = Monter(HistoriqueStyleSemaine.Pistes);
        Assert.Same(Grille(vue, "StylePistes"), GrilleVisible(vue));
        Assert.Equal(new[] { "NIVEAU", "RYTHME", "TOKENS CLAUDE CODE", "COUVERTURE" },
                     TextesVisibles(GrilleVisible(vue)).Where(LibellesPistes.Contains).ToArray());

        vm.ChoisirStyleCommand.Execute(HistoriqueStyleSemaine.Tuiles);
        Idle(vue);
        MettreEnPage(vue, 920, 900);

        var pistes = PistesVisibles(vue);
        VerifierPistes(pistes, (typeof(PisteNiveau), 120d), (typeof(PisteFenetres5h), 62d), (typeof(PisteRythme), 58d),
                       (typeof(PisteTokens), 58d), (typeof(PisteCouverture), 12d));
        Assert.Same(Grille(vue, "StyleTuiles"), GrilleVisible(vue));
        Assert.Equal(Visibility.Collapsed, Grille(vue, "StylePistes").Visibility);
        Assert.Equal(Visibility.Collapsed, Grille(vue, "StyleSimplifie").Visibility);

        // Axe des jours commun (hors des grilles de style) : sept minuits locaux + « reset hebdo → ».
        var textes = TextesVisibles(vue);
        foreach (var jour in new[] { "sam. 19", "dim. 20", "lun. 21", "mar. 22", "mer. 23", "jeu. 24", "ven. 25", "reset hebdo →" })
            Assert.Contains(jour, textes);
        Assert.Equal(7, vm.LibellesJours.Count);

        Assert.Equal(new[] { "NIVEAU", "FENÊTRES 5 H", "RYTHME", "TOKENS CLAUDE CODE", "COUVERTURE" },
                     TextesVisibles(GrilleVisible(vue)).Where(LibellesPistes.Contains).ToArray());
    }
}
