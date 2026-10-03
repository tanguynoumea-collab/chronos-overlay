using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Chronos.Controls.Historique;
using Chronos.Models.Historique;
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

    // ------------------------------------------------------------------ Vue Jour (plan 04) : montage et aides

    /// <summary>Monte la vue Jour seule (920 × 900) sur le jour de référence — même montage que <c>VueJourBindingTests.Monter</c>.</summary>
    private static VueJourView MonterJour()
    {
        var vm = new HistoriqueViewModel(new SourceHistoriqueDemonstration(Tz), new FakeUiDispatcher { OnUiThread = true },
                                         ScenariosHistorique.HorlogeFigee(Tz), Tz, new ReglagesHistoriqueMemoire());
        vm.Ouvrir();
        vm.AttendreLecture().GetAwaiter().GetResult();
        vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        vm.AttendreLecture().GetAwaiter().GetResult();
        var vue = new VueJourView { DataContext = vm };
        MettreEnPage(vue, 920, 900);
        MettreEnPage(vue, 920, 900);
        return vue;
    }

    private static (PisteNiveau Niveau, PisteRythme Rythme, PisteTokens Tokens, PisteCouverture Couverture) PistesJour(VueJourView vue)
        => (Assert.Single(Tous<PisteNiveau>(vue)), Assert.Single(Tous<PisteRythme>(vue)),
            Assert.Single(Tous<PisteTokens>(vue)), Assert.Single(Tous<PisteCouverture>(vue)));

    /// <summary>Le seul ScrollViewer de la vue (le corps).</summary>
    private static ScrollViewer DefilementUnique(FrameworkElement vue) => Assert.Single(Tous<ScrollViewer>(vue));

    /// <summary>Entre en plein écran et remet en page deux fois (ressources dynamiques, puis bindings sur ActualWidth).</summary>
    private static ResourceDictionary PleinEcranEnPage(FrameworkElement vue, double largeur, double hauteur)
    {
        var d = PleinEcran(vue);
        MettreEnPage(vue, largeur, hauteur);
        MettreEnPage(vue, largeur, hauteur);
        return d;
    }

    /// <summary>Vérifie, sur un petit écran, que <paramref name="dernier"/> finit dans la zone visible et que le pied de page est dans la vue.</summary>
    private static void RienNEstTronque(FrameworkElement vue, FrameworkElement dernier, double hauteurVue)
    {
        var defilement = DefilementUnique(vue);
        Assert.Equal(0, defilement.ScrollableHeight);

        var bas = dernier.TranslatePoint(new Point(0, dernier.ActualHeight), defilement).Y;
        Assert.True(bas <= defilement.ActualHeight + 0.5,
            $"{dernier.GetType().Name} finit à {bas}, sous le bas de la zone visible ({defilement.ActualHeight}) : tronqué.");

        var pied = Texte(vue, TextesHistorique.PiedDePage);
        Assert.Equal(Visibility.Visible, pied.Visibility);
        Assert.True(pied.ActualHeight > 0, "le pied de page d'honnêteté doit être rendu.");
        var hautPied = pied.TranslatePoint(new Point(0, 0), vue).Y;
        Assert.True(hautPied >= 0 && hautPied + pied.ActualHeight <= hauteurVue + 0.5,
            $"le pied de page ({hautPied} → {hautPied + pied.ActualHeight}) sort de la vue ({hauteurVue}).");
    }

    /// <summary>La cellule (Grid) qui porte le libellé « NIVEAU » : sa largeur est la colonne des libellés.</summary>
    private static Grid CelluleNiveau(FrameworkElement vue) => Assert.IsType<Grid>(VisualTreeHelper.GetParent(Texte(vue, TextesHistorique.PisteNiveau)));

    /// <summary>L'espaceur de la colonne de légende droite du corps du Jour (Border en colonne 2, enfant direct de « Corps »).</summary>
    private static Border EspaceurLegendeJour(VueJourView vue)
        => Assert.Single(Assert.IsType<Grid>(vue.FindName("Corps")).Children.OfType<Border>(), b => Grid.GetColumn(b) == 2);

    // ------------------------------------------------------------------ Vue Jour : proportions, plafonds, petit écran, textes

    [WpfFact]
    public void En_plein_ecran_les_pistes_du_jour_se_partagent_la_hauteur_dans_leurs_proportions()
    {
        var vue = MonterJour();
        PleinEcranEnPage(vue, 1400, 700);

        var p = PistesJour(vue);
        Assert.Equal(0, DefilementUnique(vue).ScrollableHeight);
        Assert.Equal(190.0 / 64.0, p.Niveau.ActualHeight / p.Rythme.ActualHeight, 2);
        Assert.True(Math.Abs(p.Rythme.ActualHeight - p.Tokens.ActualHeight) <= 0.5,
            $"RYTHME {p.Rythme.ActualHeight} et TOKENS {p.Tokens.ActualHeight} doivent être égales (64* / 64*).");
        Assert.Equal(12, p.Couverture.ActualHeight);
        Assert.True(p.Niveau.ActualHeight > 190, $"NIVEAU {p.Niveau.ActualHeight} doit grandir au-delà de 190 en plein écran.");
    }

    [WpfFact]
    public void En_plein_ecran_les_pistes_du_jour_s_arretent_a_leurs_plafonds()
    {
        var vue = MonterJour();
        PleinEcranEnPage(vue, 2560, 1600);

        var p = PistesJour(vue);
        Assert.Equal(240, p.Rythme.ActualHeight, 0.5);
        Assert.Equal(240, p.Tokens.ActualHeight, 0.5);
        Assert.Equal(520, p.Niveau.ActualHeight, 0.5);
        Assert.Equal(12, p.Couverture.ActualHeight);
        Assert.Equal(0, DefilementUnique(vue).ScrollableHeight);
    }

    [WpfFact]
    public void En_plein_ecran_sur_un_petit_ecran_le_jour_ne_tronque_rien()
    {
        var vue = MonterJour();
        PleinEcranEnPage(vue, 1248, 560);   // fenêtre 1 280 × 720 moins l'en-tête

        RienNEstTronque(vue, PistesJour(vue).Couverture, 560);
    }

    [WpfFact]
    public void En_plein_ecran_les_textes_et_traits_du_jour_passent_aux_valeurs_du_contrat()
    {
        var vue = MonterJour();
        var normalLibelle = Texte(vue, TextesHistorique.PisteNiveau).FontSize;
        var normalColonne = CelluleNiveau(vue).Width;
        var normalLegende = EspaceurLegendeJour(vue).Width;
        var normalTrait = PistesJour(vue).Niveau.EpaisseurPremierPlan;
        Assert.NotEqual(12, normalLibelle);

        var d = PleinEcranEnPage(vue, 1400, 700);

        Assert.Equal(12, Texte(vue, TextesHistorique.PisteNiveau).FontSize);
        Assert.Equal(128, CelluleNiveau(vue).Width);
        Assert.Equal(96, EspaceurLegendeJour(vue).Width);
        Assert.Equal(3.2, PistesJour(vue).Niveau.EpaisseurPremierPlan);

        // Retour exact au normal.
        Normal(vue, d);
        MettreEnPage(vue, 920, 900);
        MettreEnPage(vue, 920, 900);

        var p = PistesJour(vue);
        Assert.Equal(190, p.Niveau.ActualHeight);
        Assert.Equal(64, p.Rythme.ActualHeight);
        Assert.Equal(64, p.Tokens.ActualHeight);
        Assert.Equal(12, p.Couverture.ActualHeight);
        Assert.Equal(normalLibelle, Texte(vue, TextesHistorique.PisteNiveau).FontSize);
        Assert.Equal(normalColonne, CelluleNiveau(vue).Width);
        Assert.Equal(normalLegende, EspaceurLegendeJour(vue).Width);
        Assert.Equal(normalTrait, p.Niveau.EpaisseurPremierPlan);
    }

    // ------------------------------------------------------------------ Vue 4 semaines (plan 04) : montage et aides

    /// <summary>Monte la vue 4 semaines (banc partagé, 920 × 900), entre en plein écran et la remet en page à la taille demandée.</summary>
    private static VueQuatreSemainesView QuatreSemainesPleinEcran(double largeur, double hauteur)
    {
        var (_, vue) = BancQuatreSemaines.Monter();
        PleinEcranEnPage(vue, largeur, hauteur);
        return vue;
    }

    private static PisteQuatreSemaines NiveauQuatreSemaines(VueQuatreSemainesView vue) => Assert.IsType<PisteQuatreSemaines>(vue.FindName("PisteQuatreSemaines"));

    /// <summary>Les quatre pistes de couverture par semaine, dans l'ordre du document (S, S-1, S-2, S-3).</summary>
    private static List<PisteCouverture> CouverturesSemaines(VueQuatreSemainesView vue)
    {
        var rangees = Assert.IsType<ItemsControl>(vue.FindName("RangeesCouverture"));
        var pistes = Tous<PisteCouverture>(rangees).ToList();
        Assert.Equal(4, pistes.Count);
        return pistes;
    }

    /// <summary>Rangées de couverture : 10 de haut, au pas de 16 (fixes, en normal comme en plein écran).</summary>
    private static void CouverturesFixes(VueQuatreSemainesView vue)
    {
        var pistes = CouverturesSemaines(vue);
        Assert.All(pistes, p => Assert.Equal(10, p.ActualHeight));
        for (var i = 1; i < pistes.Count; i++)
            Assert.Equal(16, pistes[i].TranslatePoint(new Point(0, 0), pistes[0]).Y - pistes[i - 1].TranslatePoint(new Point(0, 0), pistes[0]).Y, 3);
    }

    // ------------------------------------------------------------------ Vue 4 semaines : NIVEAU, petit écran, textes

    [WpfFact]
    public void En_plein_ecran_le_niveau_des_quatre_semaines_s_etire_jusqu_a_son_plafond()
    {
        var moyen = QuatreSemainesPleinEcran(1400, 700);
        Assert.Equal(0, DefilementUnique(moyen).ScrollableHeight);
        Assert.True(NiveauQuatreSemaines(moyen).ActualHeight > 250,
            $"NIVEAU {NiveauQuatreSemaines(moyen).ActualHeight} doit grandir au-delà de 250 en plein écran.");
        CouverturesFixes(moyen);

        var grand = QuatreSemainesPleinEcran(2560, 1600);
        Assert.Equal(520, NiveauQuatreSemaines(grand).ActualHeight, 0.5);
        Assert.Equal(0, DefilementUnique(grand).ScrollableHeight);
        CouverturesFixes(grand);
    }

    [WpfFact]
    public void En_plein_ecran_sur_un_petit_ecran_les_quatre_semaines_ne_tronquent_rien_et_les_etiquettes_s_enroulent()
    {
        var vue = QuatreSemainesPleinEcran(1248, 560);   // fenêtre 1 280 × 720 moins l'en-tête

        var defilement = DefilementUnique(vue);
        Assert.Equal(0, defilement.ScrollableHeight);
        var derniere = CouverturesSemaines(vue)[^1];
        var bas = derniere.TranslatePoint(new Point(0, derniere.ActualHeight), defilement).Y;
        Assert.True(bas <= defilement.ActualHeight + 0.5,
            $"la dernière rangée de couverture finit à {bas}, sous le bas de la zone visible ({defilement.ActualHeight}) : tronquée.");

        var pied = Texte(vue, TextesHistorique.PiedQuatreSemaines);
        Assert.Equal(Visibility.Visible, pied.Visibility);
        Assert.True(pied.ActualHeight > 0, "le pied « rien n'est inventé » doit être rendu.");
        var hautPied = pied.TranslatePoint(new Point(0, 0), vue).Y;
        Assert.True(hautPied >= 0 && hautPied + pied.ActualHeight <= 560 + 0.5,
            $"le pied ({hautPied} → {hautPied + pied.ActualHeight}) sort de la vue (560).");

        // Étiquettes S, S-1, S-2, S-3 : colonne 140 inchangée, enroulées, jamais tronquées, contenues dans la hauteur de NIVEAU.
        var etiquettes = Assert.IsType<ItemsControl>(vue.FindName("Etiquettes"));
        Assert.Equal(140, etiquettes.ActualWidth);
        Assert.True(etiquettes.ActualHeight <= NiveauQuatreSemaines(vue).ActualHeight,
            $"les étiquettes ({etiquettes.ActualHeight}) dépassent la hauteur de NIVEAU ({NiveauQuatreSemaines(vue).ActualHeight}).");
        var textes = Tous<TextBlock>(etiquettes).Where(t => t.Name == "Texte").ToList();
        Assert.Equal(4, textes.Count);
        Assert.All(textes, t =>
        {
            Assert.Equal(TextTrimming.None, t.TextTrimming);
            Assert.Equal(TextWrapping.Wrap, t.TextWrapping);
        });
    }

    [WpfFact]
    public void En_plein_ecran_les_textes_et_traits_des_quatre_semaines_passent_aux_valeurs_du_contrat()
    {
        var (_, vue) = BancQuatreSemaines.Monter();
        Assert.Equal(250, NiveauQuatreSemaines(vue).ActualHeight, 0.5);
        var d = PleinEcranEnPage(vue, 1400, 700);

        Assert.Equal(12, Texte(vue, TextesHistorique.PisteNiveau).FontSize);
        Assert.Equal(128, CelluleNiveau(vue).Width);
        Assert.Equal(3, NiveauQuatreSemaines(vue).EpaisseurEscalier);
        var traits = Tous<System.Windows.Shapes.Rectangle>(Assert.IsType<ItemsControl>(vue.FindName("Etiquettes"))).Where(r => r.Name == "Trait").ToList();
        Assert.NotEmpty(traits);
        Assert.All(traits, r => { Assert.Equal(11, r.Width); Assert.Equal(3, r.Height); });

        // Retour exact au normal.
        Normal(vue, d);
        BancQuatreSemaines.MettreEnPage(vue, 920, 900);
        Assert.Equal(250, NiveauQuatreSemaines(vue).ActualHeight, 0.5);
        Assert.Equal(96, CelluleNiveau(vue).Width);
        CouverturesFixes(vue);
    }

    // ------------------------------------------------------------------ Garde textuelle (Pitfall 3)

    /// <summary>Vues de l'Historique déjà converties au plein écran (les plans 04 et 06 étendent la liste).</summary>
    private static readonly string[] VuesConverties = { "VueSemaineView.xaml", "VueJourView.xaml", "VueQuatreSemainesView.xaml" };

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
