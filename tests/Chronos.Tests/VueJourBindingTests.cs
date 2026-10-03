using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Chronos.Controls.Historique;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;
using Chronos.Services.Historique;
using Chronos.Text;
using Chronos.Theming;
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

    /// <summary>Rend une piste hors écran (RenderTargetBitmap) pour lire sa trace de rendu ; la trace n'est activée que le temps du rendu.</summary>
    private static List<string> Trace(PisteBase piste)
    {
        PisteBase.TracerPourTests = true;
        try
        {
            piste.InvalidateVisual();
            piste.UpdateLayout();   // OnRender est rejoué à l'arrangement, pas par RenderTargetBitmap.Render seul (34-04, Open Question 4)
            var bmp = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(piste.ActualWidth)), Math.Max(1, (int)Math.Ceiling(piste.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
            bmp.Render(piste);
            return piste.TraceRendu.ToList();
        }
        finally
        {
            PisteBase.TracerPourTests = false;
        }
    }

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ Task 1 : grille, hauteurs, axe, légendes

    [WpfFact]
    public void Le_gris_epuise_de_l_historique_suit_le_theme()
    {
        var (_, vue) = Monter();
        var lave = ThemeCatalog.ByKey("lave");
        foreach (var kv in lave.BrushTokens())
            vue.Resources[kv.Key] = kv.Value;   // comme HistoriqueWindow : l'entrée locale prime sur le repli statique
        MettreEnPage(vue, 920, 900);

        var niveau = Assert.Single(PistesVisibles(vue).OfType<PisteNiveau>());
        Assert.Equal(lave.Epuise, CouleurDe(niveau.Gris));   // le gris épuisé du thème Lave (token), pas HistoGris
        Assert.NotEqual(Hex("5A5960"), lave.Epuise);           // HistoGris : le gris de l'Historique ne se substitue pas au token
    }

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
        Assert.Equal(ThemeCatalog.Default.Epuise, CouleurDe(niveau.Gris)); // gris épuisé lisible du thème — phase 39, HistoGris reste pour hachure et repère
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

        var nomsDePistes = new[] { TextesHistorique.PisteNiveau, TextesHistorique.PisteRythme, TextesHistorique.PisteTokens, TextesHistorique.PisteCouverture };
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

    // ------------------------------------------------------------------ Task 2 : annotations, « maintenant », infobulle, pied fixe

    [WpfFact]
    public void Les_resets_observes_et_l_epuisee_sont_annonces_le_mercredi()
    {
        var (vm, vue) = Monter(joursEnArriere: 1);   // mer. 23 sept. : plateau épuisé 20:00 → 00:00, trou « Chronos arrêté » fermé à 07:00
        // Grille 5 h : 04:00 / 09:00 / 14:00 / 19:00. 35-01 : la veille est lue (LectureVeille, D-35-04) — le dernier relevé du mardi (23:00)
        // annonce resets_at = 04:00 et le premier du mercredi (07:00) une NOUVELLE borne (09:00) : le reset de 04:00 est désormais OBSERVÉ
        // (un relevé de part et d'autre, D-32-27 : même à travers un trou), donc annoté et tracé. Quatre resets observés.
        var plage = vm.DonneesJour!.Plage;
        var textes = TextesVisibles(vue);

        // D-34-32 : le mot « reset 5 h HH:MM » vient du VM (AnnotationsResets), posé à l'instant du reset observé.
        Assert.Equal(new[] { "reset 5 h 04:00", "reset 5 h 09:00", "reset 5 h 14:00", "reset 5 h 19:00" }, vm.AnnotationsResets.Select(a => a.Texte).ToArray());
        Assert.Contains("reset 5 h 04:00", textes);
        Assert.Equal(vm.AnnotationsResets.Select(a => a.Texte).OrderBy(t => t, StringComparer.Ordinal),
                     textes.Where(t => t.StartsWith("reset 5 h ", StringComparison.Ordinal)).OrderBy(t => t, StringComparer.Ordinal));

        // D-34-33 : « épuisée » posée au DÉBUT du plateau, largeur maximale = la largeur du plateau.
        var plateau = Assert.Single(vm.AnnotationsEpuisee);
        Assert.Equal(Utc("2026-09-23T18:00:00Z"), plateau.Debut);
        Assert.Equal(Utc("2026-09-23T22:00:00Z"), plateau.Fin);
        var epuisee = Assert.Single(Visibles<TextBlock>(vue), t => t.Text == TextesHistorique.Epuisee);
        var canvas = Ancetre<Canvas>(epuisee);
        var xPlateau = EchelleTemps.X(plateau.Debut, plage, canvas.ActualWidth);
        Assert.InRange(XDans(epuisee, canvas), xPlateau - 1, xPlateau + 1);
        var largeurPlateau = EchelleTemps.Largeur(plateau.Debut, plateau.Fin!.Value, plage, canvas.ActualWidth);
        Assert.InRange(epuisee.MaxWidth, largeurPlateau - 1, largeurPlateau + 1);
        Assert.Equal(TextTrimming.CharacterEllipsis, epuisee.TextTrimming);
        Assert.Equal(Hex("A9A6C4"), CouleurDe(epuisee.Foreground));   // Ink2, pas l'ambre : le gris du plateau dit déjà l'état

        // Le trou « Chronos arrêté » (mar. 23:00 → mer. 07:00) chevauche minuit. 35-01 : la veille est lue, le trou de minuit est nommé —
        // UNE annotation « Chronos arrêté » posée à x = 0 (InstantVersX borné), et rien n'est interpolé avant 07:00 : la série RENDUE commence
        // au premier relevé du jour (LectureVeille.RestreindreAuJour), la couverture ne peint aucun « présent » avant 7/24 mais le trou de 0 à 7/24.
        var trou = Assert.Single(vm.AnnotationsTrous);
        Assert.Equal("Chronos arrêté", trou.Texte);
        Assert.Equal(CauseTrou.ChronosArrete, trou.Cause);
        Assert.Equal(Utc("2026-09-22T21:00:00Z"), trou.Debut);
        Assert.Equal(Utc("2026-09-23T05:00:00Z"), trou.Fin);
        var texteTrou = Assert.Single(Visibles<TextBlock>(vue), t => t.Text == "Chronos arrêté");
        var canvasTrou = Ancetre<Canvas>(texteTrou);
        Assert.InRange(XDans(texteTrou, canvasTrou), -1, 1);
        Assert.Equal(Utc("2026-09-23T05:00:00Z"), vm.DonneesJour.Analyse.Serie[0].T);
        var couverture = PistesVisibles(vue).OfType<PisteCouverture>().Single();
        var traceCouverture = Trace(couverture);
        var present = Assert.Single(traceCouverture, l => l.StartsWith("present ", StringComparison.Ordinal));
        Assert.InRange(double.Parse(present.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture), 7.0 / 24 - 0.001, 7.0 / 24 + 0.001);
        var trouCouverture = Assert.Single(traceCouverture, l => l.StartsWith("trou ", StringComparison.Ordinal));
        var champs = trouCouverture.Split(' ');
        Assert.InRange(double.Parse(champs[1], System.Globalization.CultureInfo.InvariantCulture), -0.001, 0.001);
        Assert.InRange(double.Parse(champs[2], System.Globalization.CultureInfo.InvariantCulture), 7.0 / 24 - 0.001, 7.0 / 24 + 0.001);
        Assert.Equal("arrete", champs[3]);

        // La piste dessine ce que le VM annonce : ≥ 1 palier gris (plateau) et un trait par reset observé dans la plage.
        var niveau = PistesVisibles(vue).OfType<PisteNiveau>().Single();
        var trace = Trace(niveau);
        Assert.Contains(trace, l => l.StartsWith("palier ", StringComparison.Ordinal) && l.EndsWith(" gris", StringComparison.Ordinal));
        var traits = trace.Count(l => l.StartsWith("trait ", StringComparison.Ordinal));
        Assert.Equal(vm.DonneesJour.Analyse.Resets5h.Count(r => plage.Contient(r.Instant)), traits);
        Assert.Equal(vm.AnnotationsResets.Count, traits);
    }

    [WpfFact]
    public void La_ligne_maintenant_n_existe_qu_aujourd_hui()
    {
        var (vm, vue) = Monter();   // jeu. 24, 17:12
        var surcouche = Assert.Single(Visibles<SurcoucheReticule>(vue));

        Assert.True(vm.AfficherMaintenant);
        Assert.True(surcouche.AfficherMaintenant);
        Assert.Equal(vm.Maintenant, surcouche.Maintenant);
        Assert.Equal(Hex("F2F0FB"), CouleurDe(surcouche.TraitMaintenant));   // Ink
        Assert.Equal(0.6, surcouche.OpaciteMaintenant);                        // HistoOpaciteMaintenant
        Assert.Equal(Hex("A9A6C4"), CouleurDe(surcouche.TraitReticule));      // Ink2
        Assert.Same(vm.Fuseau, surcouche.Fuseau);
        var maintenant = Assert.Single(Trace(surcouche), l => l.StartsWith("maintenant ", StringComparison.Ordinal));
        Assert.InRange(double.Parse(maintenant.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture), 17.2 / 24 - 0.001, 17.2 / 24 + 0.001);

        var (hier, vueHier) = Monter(joursEnArriere: 1);   // mer. 23 : pas de « maintenant »
        var surcoucheHier = Assert.Single(Visibles<SurcoucheReticule>(vueHier));
        Assert.False(hier.AfficherMaintenant);
        Assert.False(surcoucheHier.AfficherMaintenant);
        Assert.DoesNotContain(Trace(surcoucheHier), l => l.StartsWith("maintenant ", StringComparison.Ordinal));

        // « Aujourd'hui » ramène au présent : la ligne revient.
        Assert.Equal("Aujourd'hui", hier.TexteRetourPresent);
        hier.RetourPresentCommand.Execute(null);
        hier.AttendreLecture().GetAwaiter().GetResult();
        Idle(vueHier);
        Assert.True(hier.AfficherMaintenant);
        Assert.True(surcoucheHier.AfficherMaintenant);
        Assert.Contains(Trace(surcoucheHier), l => l.StartsWith("maintenant ", StringComparison.Ordinal));
    }

    /// <summary>35-04 (D-35-17, reprise 34-08) — au bord droit de la piste, l'infobulle recule pour rester dans sa piste ;
    /// aujourd'hui (dernier relevé 17:12) comme la veille (dernier relevé 23:55).</summary>
    [WpfFact]
    public void L_infobulle_reste_dans_la_piste_au_bord_droit()
    {
        foreach (var joursEnArriere in new[] { 0, 1 })
        {
            var (_, vue) = Monter(joursEnArriere);
            var surcouche = Assert.Single(Visibles<SurcoucheReticule>(vue));
            var infobulle = Assert.IsType<Border>(vue.FindName("Infobulle"));
            var canvas = Ancetre<Canvas>(infobulle);

            surcouche.Survoler(surcouche.ActualWidth - 1);
            Idle(vue);
            vue.UpdateLayout();
            Idle(vue);
            Assert.Equal(Visibility.Visible, infobulle.Visibility);
            Assert.True(infobulle.ActualWidth > 0);
            var gauche = Canvas.GetLeft(infobulle);
            Assert.True(gauche >= 0, $"infobulle à gauche de la piste : {gauche}");
            Assert.True(gauche + infobulle.ActualWidth <= canvas.ActualWidth + 0.5,
                $"l'infobulle sort de la piste : {gauche} + {infobulle.ActualWidth} > {canvas.ActualWidth} (jour −{joursEnArriere})");
            if (joursEnArriere == 1)   // la veille, dernier relevé 23:55 : le cas éprouve le bord droit
                Assert.True(surcouche.XReticule + infobulle.ActualWidth > canvas.ActualWidth, "le cas n'éprouve pas le bord droit");
            // Posée sur le réticule, sauf si elle déborde : elle recule alors JUSTE de ce qui dépasse.
            Assert.Equal(Math.Max(0, Math.Min(surcouche.XReticule, canvas.ActualWidth - infobulle.ActualWidth)), gauche, 0.5);

            // Au milieu de la piste, elle reste posée sur le réticule.
            surcouche.Survoler(surcouche.ActualWidth * 0.3);
            Idle(vue);
            vue.UpdateLayout();
            Idle(vue);
            Assert.Equal(surcouche.XReticule, Canvas.GetLeft(infobulle), 0.01);
        }
    }

    /// <summary>35-04 (reprise 34-08, écart « annotations d'une même rangée ») — jeudi : « reset 5 h 15:00 » et le trou « jeton
    /// invalide » (14:00 → 16:00) sont proches ; chacun a sa rangée, les deux ne se recouvrent plus.</summary>
    [WpfFact]
    public void Les_resets_et_les_trous_ont_chacun_leur_rangee()
    {
        var (_, vue) = Monter();
        var reset = Assert.Single(Visibles<TextBlock>(vue), t => t.Text == "reset 5 h 15:00");
        var jeton = Assert.Single(Visibles<TextBlock>(vue), t => t.Text == "jeton invalide");

        var rangeeReset = Assert.IsType<Grid>(vue.FindName("RangeeAnnotationsHaut"));
        var rangeeTrous = Assert.IsType<Grid>(vue.FindName("RangeeAnnotationsTrous"));
        Assert.True(EstDans(reset, rangeeReset), "le reset est dans la rangée du haut");
        Assert.True(EstDans(jeton, rangeeTrous), "le trou est dans la rangée des trous");

        var yReset = reset.TranslatePoint(new Point(0, 0), vue).Y;
        var yJeton = jeton.TranslatePoint(new Point(0, 0), vue).Y;
        Assert.True(yReset + reset.ActualHeight <= yJeton + 0.5 || yJeton + jeton.ActualHeight <= yReset + 0.5,
            $"les deux annotations se recouvrent verticalement : reset [{yReset}, {yReset + reset.ActualHeight}], trou [{yJeton}, {yJeton + jeton.ActualHeight}]");
    }

    private static bool EstDans(DependencyObject e, DependencyObject ancetre)
    {
        for (var p = VisualTreeHelper.GetParent(e); p is not null; p = VisualTreeHelper.GetParent(p))
            if (ReferenceEquals(p, ancetre)) return true;
        return false;
    }

    [WpfFact]
    public void Le_trou_jeton_invalide_et_son_saut_5h_sont_annotes_aujourd_hui()
    {
        var (vm, vue) = Monter();
        var textes = TextesVisibles(vue);

        var jeton = Assert.Single(Visibles<TextBlock>(vue), t => t.Text == "jeton invalide");
        Assert.Equal(CouleurDe(vm.Theme.BrushTokens()["Alerte"]), CouleurDe(jeton.Foreground));   // ambre : cause « jeton »
        Assert.DoesNotContain("Chronos arrêté", textes);

        // Jour : sauts 5 h seulement ; la grille 5 h passe par 15:00 pendant l'absence 14:00 → 16:00 (34-03) → « au moins un reset ».
        var saut = Assert.Single(vm.AnnotationsSauts);
        Assert.EndsWith("pendant l'absence (répartition inconnue)", saut.Texte, StringComparison.Ordinal);
        Assert.Equal(Utc("2026-09-24T12:00:00Z"), saut.Debut);
        var texteSaut = Assert.Single(Visibles<TextBlock>(vue), t => t.Text == saut.Texte);
        Assert.Equal(Hex("A9A6C4"), CouleurDe(texteSaut.Foreground));
        var canvas = Ancetre<Canvas>(texteSaut);
        Assert.InRange(XDans(texteSaut, canvas), canvas.ActualWidth * 14 / 24 - 1, canvas.ActualWidth * 14 / 24 + 1);

        var couverture = PistesVisibles(vue).OfType<PisteCouverture>().Single();
        Assert.Single(couverture.Trous!);
        Assert.Equal(CouleurDe(vm.Theme.BrushTokens()["Alerte"]), CouleurDe(couverture.Jeton));
        Assert.Contains(Trace(couverture), l => l.StartsWith("trou ", StringComparison.Ordinal) && l.EndsWith(" jeton", StringComparison.Ordinal));
    }

    [WpfFact]
    public void L_infobulle_du_jour_a_quatre_lignes_et_le_pied_de_page_est_fixe()
    {
        var (vm, vue) = Monter();
        var surcouche = Assert.Single(Visibles<SurcoucheReticule>(vue));
        Assert.True(surcouche.ActualHeight >= 190 + 64 + 64 + 12, $"la surcouche couvre toutes les pistes (ActualHeight={surcouche.ActualHeight})");
        Assert.Same(vm.DonneesJour!.Analyse.Serie, surcouche.Serie);

        var infobulle = Assert.IsType<Border>(vue.FindName("Infobulle"));
        Assert.Equal(Visibility.Collapsed, infobulle.Visibility);

        surcouche.Survoler(surcouche.ActualWidth * 0.6);
        Idle(vue);
        Assert.Equal(Visibility.Visible, infobulle.Visibility);
        var texte = Assert.Single(Visibles<TextBlock>(infobulle)).Text;
        Assert.Equal(surcouche.TexteInfobulle, texte);
        Assert.Equal(4, texte.Split('\n').Length);
        foreach (var attendu in new[] { "relevé exact", "5 h : ", "hebdo : ", "sonde d'en-têtes de rate-limit" })
            Assert.Contains(attendu, texte, StringComparison.Ordinal);
        Assert.Equal(surcouche.XReticule, Canvas.GetLeft(infobulle));
        Assert.Equal(Hex("1E1B30"), CouleurDe(infobulle.Background));   // Panel2

        surcouche.Quitter();
        Idle(vue);
        Assert.Equal(Visibility.Collapsed, infobulle.Visibility);

        // Le pied de page est HORS du défilement (D-34-31) ; pas de pied « divergence » en Jour.
        var pied = Assert.Single(Visibles<TextBlock>(vue), t => t.Text == TextesHistorique.PiedDePage);
        for (DependencyObject? p = pied; p is not null; p = VisualTreeHelper.GetParent(p))
            Assert.IsNotType<ScrollViewer>(p);
        Assert.DoesNotContain(TextesHistorique.PiedDivergence, TextesVisibles(vue));
    }
}
