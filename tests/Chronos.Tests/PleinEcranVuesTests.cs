using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Chronos.Controls.Historique;
using Chronos.Rendering.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Text;
using Chronos.ViewModels.Historique;
using Chronos.Views.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-10 (phase 38) — le plein écran de l'Historique est un ÉCHANGE DE DICTIONNAIRE : <see cref="DictionnairePleinEcran"/> construit,
/// depuis les tokens <c>*PleinEcran</c> de <c>DesignTokens.xaml</c>, un dictionnaire dont les clés sont les clés normales. Ces tests
/// vérifient (1) son contenu contre la table du contrat DESIGN_PLAN_CYCLE2 § 4.2, écrite EN DUR ici, et (2) son effet sur l'arbre visuel
/// réel des vues : proportions des pistes, plafonds, couverture fixe, plus de défilement, corps et traits agrandis, retour exact au normal.
/// Collection sérialisée obligatoire (chargeur BAML).
/// </summary>
[Collection("XAML WPF")]
public class PleinEcranVuesTests
{
    /// <summary>Le dictionnaire des tokens, chargé seul (sans Application).</summary>
    internal static ResourceDictionary Tokens()
        => (ResourceDictionary)Application.LoadComponent(new Uri("/Chronos;component/Resources/DesignTokens.xaml", UriKind.Relative));

    // ------------------------------------------------------------------ Contenu du dictionnaire (contrat § 4.2)

    /// <summary>Table du contrat § 4.2 (+ en-tête 124, écart consigné) : clé normale → valeur plein écran.</summary>
    private static readonly (string Cle, double Valeur)[] ValeursContrat =
    {
        ("HistoCorpsInfime", 11.5),
        ("HistoCorpsLegende", 12),
        ("HistoCorpsMini", 13),
        ("HistoCorpsPetit", 14),
        ("HistoCorpsMoyen", 15),
        ("HistoCorpsNormal", 15.5),
        ("HistoCorpsGrand", 18),
        ("HistoCorpsTitre", 21),
        ("HistoLargeurLibelles", 128),
        ("HistoLargeurLegendeDroite", 96),
        ("HistoEpaisseurEscalier", 3),
        ("HistoEpaisseurPremierPlan", 3.2),
        ("HistoLongueurTiretReset", 11),
        ("HistoHauteurEnTete", 124),
        ("HistoPlafondNiveau", 520),
        ("HistoPlafondPiste", 240),
    };

    /// <summary>Rangées étoilées : l'étoile reprend la hauteur normale de la piste (§ 2.2 / 2.3 / 2.4).</summary>
    private static readonly (string Rangee, string Hauteur, double Etoiles)[] RangeesContrat =
    {
        ("HistoRangeeNiveauSemaine", "HistoHauteurNiveauPistes", 150),
        ("HistoRangeeRythmeSemaine", "HistoHauteurRythmePistes", 72),
        ("HistoRangeeTokensSemaine", "HistoHauteurTokensPistes", 72),
        ("HistoRangeeNiveauJour", "HistoHauteurNiveauJour", 190),
        ("HistoRangeeRythmeJour", "HistoHauteurRythmeJour", 64),
        ("HistoRangeeTokensJour", "HistoHauteurTokensJour", 64),
        ("HistoRangeeNiveauQuatreSemaines", "HistoHauteurNiveauQuatreSemaines", 250),
    };

    [WpfFact]
    public void Le_dictionnaire_plein_ecran_porte_les_valeurs_du_contrat()
    {
        var d = DictionnairePleinEcran.Construire(Tokens());

        foreach (var (cle, valeur) in ValeursContrat)
            Assert.True(d[cle] is double v && v == valeur, $"PleinEcran[« {cle} »] = {d[cle]} au lieu de {valeur} (contrat § 4.2).");

        foreach (var (rangee, hauteur, etoiles) in RangeesContrat)
        {
            Assert.Equal(new GridLength(etoiles, GridUnitType.Star), Assert.IsType<GridLength>(d[rangee]));
            Assert.True(d[hauteur] is double h && double.IsNaN(h), $"PleinEcran[« {hauteur} »] doit valoir NaN (la piste s'étire dans sa rangée).");
        }

        Assert.Equal(ScrollBarVisibility.Disabled, Assert.IsType<ScrollBarVisibility>(d["HistoDefilementVertical"]));
        Assert.Equal(16 + 7 * 2 + 1, d.Count);
    }

    // ------------------------------------------------------------------ Vue Semaine : montage et aides

    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();

    /// <summary>Monte la vue Semaine seule (920 × 900) sur le scénario de référence — même montage que <c>VueSemaineBindingTests</c>.</summary>
    private static VueSemaineView MonterSemaine()
    {
        var vm = new HistoriqueViewModel(new SourceHistoriqueDemonstration(Tz), new FakeUiDispatcher { OnUiThread = true },
                                         ScenariosHistorique.HorlogeFigee(Tz), Tz, new ReglagesHistoriqueMemoire());
        vm.Ouvrir();
        vm.AttendreLecture().GetAwaiter().GetResult();
        var vue = new VueSemaineView { DataContext = vm };
        MettreEnPage(vue, 920, 900);
        return vue;
    }

    /// <summary>Measure / Arrange puis un tour de dispatcher et une seconde passe (bindings sur ActualWidth, ressources dynamiques).</summary>
    private static void MettreEnPage(FrameworkElement vue, double largeur, double hauteur)
    {
        vue.Measure(new Size(largeur, hauteur));
        vue.Arrange(new Rect(0, 0, largeur, hauteur));
        vue.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        vue.UpdateLayout();
    }

    private static IEnumerable<T> Tous<T>(DependencyObject racine) where T : DependencyObject
    {
        if (racine is T t) yield return t;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(racine); i++)
            foreach (var x in Tous<T>(VisualTreeHelper.GetChild(racine, i)))
                yield return x;
    }

    /// <summary>Entre en plein écran : le dictionnaire est fusionné EN DERNIER dans les ressources de la vue (il gagne).</summary>
    private static ResourceDictionary PleinEcran(FrameworkElement vue)
    {
        var d = DictionnairePleinEcran.Construire(Tokens());
        vue.Resources.MergedDictionaries.Add(d);
        return d;
    }

    private static void Normal(FrameworkElement vue, ResourceDictionary d) => Assert.True(vue.Resources.MergedDictionaries.Remove(d));

    private static (PisteNiveau Niveau, PisteRythme Rythme, PisteTokens Tokens, PisteCouverture Couverture) Pistes(VueSemaineView vue)
        => (Assert.Single(Tous<PisteNiveau>(vue)), Assert.Single(Tous<PisteRythme>(vue)),
            Assert.Single(Tous<PisteTokens>(vue)), Assert.Single(Tous<PisteCouverture>(vue)));

    private static ScrollViewer Defilement(VueSemaineView vue) => Assert.IsType<ScrollViewer>(vue.FindName("Defilement"));

    private static TextBlock Texte(DependencyObject racine, string texte) => Assert.Single(Tous<TextBlock>(racine), t => t.Text == texte);

    // ------------------------------------------------------------------ Vue Semaine : proportions, plafonds, petit écran

    [WpfFact]
    public void En_plein_ecran_les_pistes_de_la_semaine_se_partagent_la_hauteur_dans_leurs_proportions()
    {
        var vue = MonterSemaine();
        PleinEcran(vue);
        MettreEnPage(vue, 1400, 700);

        var p = Pistes(vue);
        Assert.Equal(0, Defilement(vue).ScrollableHeight);
        Assert.Equal(150.0 / 72.0, p.Niveau.ActualHeight / p.Rythme.ActualHeight, 2);
        Assert.True(Math.Abs(p.Rythme.ActualHeight - p.Tokens.ActualHeight) <= 0.5,
            $"RYTHME {p.Rythme.ActualHeight} et TOKENS {p.Tokens.ActualHeight} doivent être égales (72* / 72*).");
        Assert.Equal(12, p.Couverture.ActualHeight);
        Assert.True(p.Niveau.ActualHeight > 150, $"NIVEAU {p.Niveau.ActualHeight} doit grandir au-delà de 150 en plein écran.");
    }

    [WpfFact]
    public void En_plein_ecran_les_pistes_de_la_semaine_s_arretent_a_leurs_plafonds()
    {
        var vue = MonterSemaine();
        PleinEcran(vue);
        MettreEnPage(vue, 2560, 1600);

        var p = Pistes(vue);
        Assert.Equal(240, p.Rythme.ActualHeight, 0.5);
        Assert.Equal(240, p.Tokens.ActualHeight, 0.5);
        Assert.Equal(520, p.Niveau.ActualHeight, 0.5);
        Assert.Equal(12, p.Couverture.ActualHeight);
        Assert.Equal(0, Defilement(vue).ScrollableHeight);
    }

    [WpfFact]
    public void En_plein_ecran_sur_un_petit_ecran_la_semaine_ne_tronque_rien()
    {
        var vue = MonterSemaine();
        PleinEcran(vue);
        MettreEnPage(vue, 1248, 560);   // fenêtre 1 280 × 720 moins l'en-tête

        var p = Pistes(vue);
        var defilement = Defilement(vue);
        Assert.Equal(0, defilement.ScrollableHeight);
        Assert.True(p.Niveau.ActualHeight >= 150, $"NIVEAU {p.Niveau.ActualHeight} < 150 sur un écran 1 280 × 720.");
        Assert.True(p.Rythme.ActualHeight >= 72, $"RYTHME {p.Rythme.ActualHeight} < 72 sur un écran 1 280 × 720.");

        var basCouverture = p.Couverture.TranslatePoint(new Point(0, p.Couverture.ActualHeight), defilement).Y;
        Assert.True(basCouverture <= defilement.ActualHeight + 0.5,
            $"la COUVERTURE finit à {basCouverture}, sous le bas de la zone visible ({defilement.ActualHeight}) : tronquée.");

        var pied = Texte(vue, TextesHistorique.PiedDePage);
        Assert.Equal(Visibility.Visible, pied.Visibility);
        Assert.True(pied.ActualHeight > 0, "le pied de page d'honnêteté doit être rendu.");
        var hautPied = pied.TranslatePoint(new Point(0, 0), vue).Y;
        Assert.True(hautPied >= 0 && hautPied + pied.ActualHeight <= 560 + 0.5,
            $"le pied de page ({hautPied} → {hautPied + pied.ActualHeight}) sort de la vue (560).");
    }

    // ------------------------------------------------------------------ Vue Semaine : corps et traits, retour au normal

    [WpfFact]
    public void En_plein_ecran_les_textes_et_traits_de_la_semaine_passent_aux_valeurs_du_contrat()
    {
        var vue = MonterSemaine();
        PleinEcran(vue);
        MettreEnPage(vue, 1400, 700);

        Assert.Equal(12, Texte(vue, TextesHistorique.PisteNiveau).FontSize);
        Assert.Equal(11.5, Texte(vue, TextesHistorique.RepereCent).FontSize);
        Assert.Equal(13, Texte(vue, TextesHistorique.PiedDePage).FontSize);

        var niveau = Pistes(vue).Niveau;
        Assert.Equal(3, niveau.EpaisseurEscalier);
        Assert.Equal(3.2, niveau.EpaisseurPremierPlan);
        Assert.Equal(11, niveau.LongueurTiretReset);

        var celluleRythme = Assert.IsType<StackPanel>(VisualTreeHelper.GetParent(Texte(vue, TextesHistorique.PisteRythme)));
        Assert.Equal(128, celluleRythme.Width);
    }

    [WpfFact]
    public void Sortir_du_plein_ecran_rend_la_semaine_a_ses_hauteurs_normales()
    {
        var vue = MonterSemaine();
        var d = PleinEcran(vue);
        MettreEnPage(vue, 1400, 700);
        Normal(vue, d);
        MettreEnPage(vue, 920, 900);

        var p = Pistes(vue);
        Assert.Equal(150, p.Niveau.ActualHeight);
        Assert.Equal(72, p.Rythme.ActualHeight);
        Assert.Equal(72, p.Tokens.ActualHeight);
        Assert.Equal(12, p.Couverture.ActualHeight);
        Assert.Equal(9, Texte(vue, TextesHistorique.PisteNiveau).FontSize);
        Assert.Equal(2.2, p.Niveau.EpaisseurEscalier);
    }

    // ------------------------------------------------------------------ Garde textuelle (Pitfall 3)

    /// <summary>Vues de l'Historique déjà converties au plein écran (les plans 04 et 06 étendent la liste).</summary>
    private static readonly string[] VuesConverties = { "VueSemaineView.xaml" };

    /// <summary>Une <c>StaticResource</c> sur une clé échelonnée est résolue une fois au chargement : elle ne bascule JAMAIS, sans erreur.</summary>
    [Fact]
    public void Aucune_StaticResource_sur_une_cle_echelonnee_dans_les_vues_de_l_historique()
    {
        var cles = DictionnairePleinEcran.ClesEchelonnees
            .Concat(DictionnairePleinEcran.Rangees.Select(r => r.Rangee))
            .Concat(DictionnairePleinEcran.Rangees.Select(r => r.Hauteur));
        var motif = new Regex(@"\{StaticResource (" + string.Join("|", cles.Select(Regex.Escape)) + @")\}");

        var infractions = new List<string>();
        foreach (var nom in VuesConverties)
        {
            var chemin = Path.Combine(GardesPerimetreTests.CheminSources(), "Views", "Historique", nom);
            Assert.True(File.Exists(chemin), $"fichier introuvable : {chemin}");
            var lignes = File.ReadAllLines(chemin);
            for (var i = 0; i < lignes.Length; i++)
                foreach (Match m in motif.Matches(lignes[i]))
                    infractions.Add($"{nom}:{i + 1}: {m.Value}");
        }

        Assert.True(infractions.Count == 0,
            "HIS-10 : une clé échelonnée est lue en StaticResource — le dictionnaire PleinEcran ne la remplacera jamais "
            + "(utiliser DynamicResource).\n  " + string.Join("\n  ", infractions));
    }
}
