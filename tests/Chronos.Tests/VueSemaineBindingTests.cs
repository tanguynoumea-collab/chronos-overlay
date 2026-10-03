using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Chronos.Controls.Historique;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Text;
using Chronos.Theming;
using Chronos.ViewModels.Historique;
using Chronos.Views.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-02 / HIS-03 / HIS-09 — la vue « Semaine de forfait » (34-06, grille unique depuis la phase 38) mesurée sur l'ARBRE VISUEL RÉEL :
/// l'ordre des pistes visibles et leur <c>ActualHeight</c> exacts (table DESIGN_PLAN §2.2 : 150 / 72 / 72 / 12), la variante de la piste
/// Niveau, une seule grille (GrillePistes), l'axe des jours et les libellés de pistes ; puis (HIS-02, HIS-06) les annotations d'honnêteté
/// posées à leur instant, la zone hachurée avant le journal sur S-1, le libellé permanent des tokens, la légende et les pieds, la
/// surcouche réticule + infobulle, les tokens et le thème reçus par les pistes, 760 → 1 400 px.
///
/// Ce que <c>dotnet build</c> ne voit pas : une piste posée dans la mauvaise grille, une hauteur prise au mauvais token, une ancienne
/// grille de style qui réapparaîtrait. La vue est montée SEULE (elle fusionne <c>DesignTokens.xaml</c> elle-même), sur le scénario de
/// référence (<c>SourceHistoriqueDemonstration</c>, horloge figée, réglages en mémoire) — aucun fichier, aucun exe.
/// Collection sérialisée obligatoire (course du chargeur BAML, Pitfall 12).
/// </summary>
[Collection("XAML WPF")]
public class VueSemaineBindingTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();

    private sealed record Banc(VueSemaineView Vue, HistoriqueViewModel Vm);

    /// <summary>Monte la vue seule (920 × 900) sur le scénario de référence, au présent ou sur S-1.</summary>
    private static Banc Monter(bool semainePrecedente = false, ISourceHistorique? source = null)
    {
        var reglages = new ReglagesHistoriqueMemoire();
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

    /// <summary>Parcours complet (entre aussi dans les sous-arbres cachés) : pour retrouver un élément <c>Collapsed</c> par son nom.</summary>
    private static IEnumerable<T> Tous<T>(DependencyObject racine) where T : DependencyObject
    {
        if (racine is T t) yield return t;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(racine); i++)
            foreach (var x in Tous<T>(VisualTreeHelper.GetChild(racine, i)))
                yield return x;
    }

    /// <summary>Le <c>Canvas.Left</c> effectif d'un élément posé par instant (porté par son conteneur direct dans le Canvas) et la largeur de ce Canvas.</summary>
    private static (double Left, double LargeurCanvas) PositionDe(DependencyObject e)
    {
        for (var x = e; x is not null; x = VisualTreeHelper.GetParent(x))
        {
            if (x is UIElement ui && !double.IsNaN(Canvas.GetLeft(ui)) && VisualTreeHelper.GetParent(x) is Canvas canvas)
                return (Canvas.GetLeft(ui), canvas.ActualWidth);
        }
        throw new Xunit.Sdk.XunitException("élément non posé dans un Canvas par Canvas.Left");
    }

    private static Color CouleurDe(Brush? b) => Assert.IsType<SolidColorBrush>(b).Color;
    private static Color Hex(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    private static readonly DateTimeOffset DebutTrouArrete = new(2026, 9, 22, 21, 0, 0, TimeSpan.Zero);   // mar. 22 sept. 23:00 Paris

    private static Grid Grille(VueSemaineView vue, string nom) => Assert.IsType<Grid>(vue.FindName(nom));

    /// <summary>HIS-09 : la grille unique des pistes, toujours visible (aucun DataTrigger de style).</summary>
    private static Grid GrillePistes(VueSemaineView vue)
    {
        var g = Grille(vue, "GrillePistes");
        Assert.Equal(Visibility.Visible, g.Visibility);
        return g;
    }

    private static void VerifierPistes(List<PisteBase> pistes, params (Type Type, double Hauteur)[] attendues)
    {
        Assert.Equal(attendues.Select(a => a.Type).ToList(), pistes.Select(p => p.GetType()).ToList());
        for (var i = 0; i < attendues.Length; i++)
            Assert.Equal(attendues[i].Hauteur, pistes[i].ActualHeight);
    }

    private static readonly string[] LibellesPistes = { "NIVEAU", "RYTHME", "TOKENS CLAUDE CODE", "COUVERTURE" };

    // ------------------------------------------------------------------ Table §2.2 (HIS-03), grille unique (HIS-09)

    [WpfFact]
    public void La_grille_des_pistes_a_exactement_les_pistes_et_hauteurs_du_plan()
    {
        var (vue, _) = Monter();

        var pistes = PistesVisibles(vue);
        VerifierPistes(pistes, (typeof(PisteNiveau), 150d), (typeof(PisteRythme), 72d), (typeof(PisteTokens), 72d), (typeof(PisteCouverture), 12d));
        Assert.Equal(VariantePisteNiveau.SemaineComplete, ((PisteNiveau)pistes[0]).Variante);
    }

    [WpfFact]
    public void La_vue_Semaine_n_a_qu_une_grille_de_pistes()
    {
        var (vue, vm) = Monter();
        Assert.Null(vue.FindName("StylePistes"));
        Assert.Null(vue.FindName("StyleSimplifie"));
        Assert.Null(vue.FindName("StyleTuiles"));
        Assert.DoesNotContain(Visibles<FrameworkElement>(vue), e => e.Name.StartsWith("Style", StringComparison.Ordinal));
        Assert.Single(Visibles<PisteNiveau>(vue));

        var grille = GrillePistes(vue);
        Assert.Equal(new[] { "NIVEAU", "RYTHME", "TOKENS CLAUDE CODE", "COUVERTURE" },
                     TextesVisibles(grille).Where(LibellesPistes.Contains).ToArray());

        // Axe des jours (hors de la grille des pistes) : sept minuits locaux + « reset hebdo → ».
        var textes = TextesVisibles(vue);
        foreach (var jour in new[] { "sam. 19", "dim. 20", "lun. 21", "mar. 22", "mer. 23", "jeu. 24", "ven. 25", "reset hebdo →" })
            Assert.Contains(jour, textes);
        Assert.Equal(7, vm.LibellesJours.Count);
    }

    // ------------------------------------------------------------------ Annotations d'honnêteté (HIS-02, HIS-06)

    [WpfFact]
    public void Les_annotations_de_trous_et_de_saut_sont_des_TextBlock_poses_a_leur_instant()
    {
        var (vue, vm) = Monter();
        var grille = GrillePistes(vue);
        var textes = TextesVisibles(grille);
        Assert.Equal(1, textes.Count(t => t == "Chronos arrêté"));
        Assert.Equal(1, textes.Count(t => t == "jeton invalide"));
        Assert.Contains("+2 % pendant l'absence (répartition inconnue)", textes);

        var arrete = Assert.Single(Visibles<TextBlock>(grille), t => t.Text == "Chronos arrêté");
        var (left, largeurCanvas) = PositionDe(arrete);
        Assert.True(largeurCanvas > 0, "le Canvas des annotations doit avoir la largeur de la colonne des pistes");
        Assert.Equal(EchelleTemps.X(DebutTrouArrete, vm.DonneesSemaine!.Plage, largeurCanvas), left, 1.0);
        Assert.Equal(1, textes.Count(t => t == "+2 % pendant l'absence (répartition inconnue)"));
    }

    [WpfFact]
    public void La_zone_avant_le_journal_et_son_marqueur_n_apparaissent_que_sur_la_semaine_precedente()
    {
        var (vue, _) = Monter();
        Assert.DoesNotContain(TextesVisibles(vue), t => t.StartsWith("journal ouvert le", StringComparison.Ordinal));
        var zone = Assert.Single(Tous<Rectangle>(GrillePistes(vue)), r => r.Name.StartsWith("ZoneAvantJournal", StringComparison.Ordinal));
        Assert.Equal(Visibility.Collapsed, zone.Visibility);

        var (avant, vm) = Monter(semainePrecedente: true);
        Assert.Contains("journal ouvert le 14 sept. 2026", TextesVisibles(avant));
        var grille = GrillePistes(avant);
        var zoneAvant = Assert.Single(Tous<Rectangle>(grille), r => r.Name.StartsWith("ZoneAvantJournal", StringComparison.Ordinal));
        Assert.Equal(Visibility.Visible, zoneAvant.Visibility);
        Assert.IsType<DrawingBrush>(zoneAvant.Fill);

        var plage = vm.DonneesSemaine!.Plage;
        var largeurPistes = Assert.Single(Visibles<PisteNiveau>(grille)).ActualWidth;
        Assert.True(largeurPistes > 0);
        Assert.Equal(EchelleTemps.Largeur(plage.Debut, ScenariosHistorique.JournalOuvertLe, plage, largeurPistes), zoneAvant.ActualWidth, 1.0);
        Assert.True(zoneAvant.ActualWidth > 0);
    }

    [WpfFact]
    public void Le_libelle_permanent_la_legende_et_les_pieds_sont_presents()
    {
        var (vue, _) = Monter();
        var textes = TextesVisibles(vue);
        Assert.Contains("par heure, comptés localement — hors Cowork et claude.ai · bruts, non pondérés · ce n'est PAS un % du forfait", textes);
        Assert.Contains("▮ principal ▮ sous-agents", textes);
        Assert.Contains(TextesHistorique.PiedDePage, textes);
        Assert.Contains(TextesHistorique.PiedDivergence, textes);
        Assert.Equal(Visibility.Visible, Assert.IsAssignableFrom<FrameworkElement>(vue.FindName("PiedDivergence")).Visibility);

        var dansGrille = TextesVisibles(GrillePistes(vue));
        Assert.Contains("0 – 25 %", dansGrille);
        Assert.Contains(dansGrille, t => t.StartsWith("0 – ", StringComparison.Ordinal) && t.EndsWith("(sortie)", StringComparison.Ordinal));

        var sansDivergence = new FakeSourceHistorique(Tz) { TransformerSemaine = d => d with { Divergences = Array.Empty<Divergence>() } };
        var (sans, vmSans) = Monter(source: sansDivergence);
        Assert.False(vmSans.AfficherPiedDivergence);
        Assert.Equal(Visibility.Collapsed, Assert.IsAssignableFrom<FrameworkElement>(sans.FindName("PiedDivergence")).Visibility);
    }

    // ------------------------------------------------------------------ Surcouche réticule + infobulle

    [WpfFact]
    public void La_surcouche_couvre_les_pistes_et_l_infobulle_suit_le_survol()
    {
        var (vue, vm) = Monter();
        var grille = GrillePistes(vue);
        var surcouche = Assert.Single(Visibles<SurcoucheReticule>(grille));

        Assert.True(Grid.GetRowSpan(surcouche) >= 4, "la surcouche couvre toutes les rangées de pistes");
        Assert.True(surcouche.ActualHeight >= 150 + 72 + 72 + 12, $"ActualHeight={surcouche.ActualHeight}");
        Assert.NotNull(surcouche.Serie);
        Assert.NotNull(surcouche.Plage);
        Assert.NotNull(surcouche.Fuseau);
        Assert.Equal(vm.DonneesSemaine!.LueA, surcouche.InstantLecture);
        Assert.NotNull(surcouche.TraitReticule);

        var infobulle = Assert.Single(Tous<Border>(grille), b => b.Name.StartsWith("Infobulle", StringComparison.Ordinal));
        Assert.Equal(Visibility.Collapsed, infobulle.Visibility);

        surcouche.Survoler(surcouche.ActualWidth / 2);
        Idle(vue);
        Assert.True(surcouche.InfobulleVisible);
        Assert.Equal(Visibility.Visible, infobulle.Visibility);
        var texte = Assert.Single(Tous<TextBlock>(infobulle));
        Assert.Equal(surcouche.TexteInfobulle, texte.Text);
        Assert.Equal(4, texte.Text.Split('\n').Length);
        Assert.Contains("relevé exact", texte.Text);
        Assert.Equal(surcouche.XReticule, Canvas.GetLeft(infobulle), 0.01);
        Assert.True(surcouche.XReticule > 0);

        surcouche.Quitter();
        Idle(vue);
        Assert.Equal(Visibility.Collapsed, infobulle.Visibility);
    }

    /// <summary>35-04 (D-35-17, reprise 34-08) — au bord droit (dernier relevé jeu. 17:12), l'infobulle recule pour rester dans sa
    /// piste ; au milieu, elle reste posée sur le réticule.</summary>
    [WpfFact]
    public void L_infobulle_reste_dans_la_piste()
    {
        var (vue, _) = Monter();
        var grille = GrillePistes(vue);
        var surcouche = Assert.Single(Visibles<SurcoucheReticule>(grille));
        var infobulle = Assert.Single(Tous<Border>(grille), b => b.Name.StartsWith("Infobulle", StringComparison.Ordinal));
        var canvas = Assert.IsType<Canvas>(VisualTreeHelper.GetParent(infobulle));

        surcouche.Survoler(surcouche.ActualWidth - 1);
        Idle(vue);
        vue.UpdateLayout();
        Idle(vue);
        Assert.Equal(Visibility.Visible, infobulle.Visibility);
        var gauche = Canvas.GetLeft(infobulle);
        Assert.True(gauche >= 0, $"infobulle à gauche de la piste ({gauche})");
        Assert.True(gauche + infobulle.ActualWidth <= canvas.ActualWidth + 0.5,
            $"l'infobulle sort de la piste : {gauche} + {infobulle.ActualWidth} > {canvas.ActualWidth}");
        Assert.True(surcouche.XReticule + infobulle.ActualWidth > canvas.ActualWidth, "le cas n'éprouve pas le bord droit");
        Assert.Equal(canvas.ActualWidth - infobulle.ActualWidth, gauche, 0.5);   // recule JUSTE de ce qui dépasse

        surcouche.Survoler(surcouche.ActualWidth * 0.3);
        Idle(vue);
        vue.UpdateLayout();
        Idle(vue);
        Assert.Equal(surcouche.XReticule, Canvas.GetLeft(infobulle), 0.01);
    }

    // ------------------------------------------------------------------ Tokens et thème reçus par les pistes

    [WpfFact]
    public void Les_pistes_recoivent_les_tokens_et_le_theme()
    {
        var (vue, vm) = Monter();
        var grille = GrillePistes(vue);

        var niveau = Assert.Single(Visibles<PisteNiveau>(grille));
        Assert.Same(vm.Theme, niveau.Rampe);
        Assert.Equal(2.2, niveau.EpaisseurEscalier);
        Assert.Equal(1.0, niveau.EpaisseurFine);
        Assert.Equal(8.0, niveau.LongueurTiretReset);
        Assert.Equal(0.35, niveau.OpaciteTrou);
        Assert.Equal(Hex(0xF4, 0xF2, 0xEC), CouleurDe(niveau.TraitReset));      // TickReset (DynamicResource, repli statique du dictionnaire)
        Assert.Equal(ThemeCatalog.Default.Epuise, CouleurDe(niveau.Gris));      // gris épuisé lisible du thème — phase 39, HistoGris reste pour hachure et repère
        Assert.Equal(Hex(0x8B, 0x7B, 0xF0), CouleurDe(niveau.CadreDivergence)); // Accent
        Assert.Equal(Hex(0x2C, 0x29, 0x42), CouleurDe(niveau.FondTrou));        // Line
        Assert.Same(vm.DonneesSemaine!.Analyse, niveau.Analyse);
        Assert.Same(vm.DonneesSemaine.Precedente, niveau.Precedente);
        Assert.Equal(1, niveau.Divergences?.Count);

        var tokens = Assert.Single(Visibles<PisteTokens>(grille));
        Assert.Equal(vm.PlafondTokens, tokens.Plafond);
        Assert.True(tokens.Plafond > 1);
        Assert.Equal(Hex(0x8C, 0x89, 0xA8), CouleurDe(tokens.Principal));
        Assert.Equal(Hex(0x5A, 0x57, 0x76), CouleurDe(tokens.SousAgents));
        Assert.IsType<DrawingBrush>(tokens.Hachure);
        Assert.Equal(vm.DonneesSemaine.LueA, tokens.InstantLecture);

        var couverture = Assert.Single(Visibles<PisteCouverture>(grille));
        Assert.Equal(Hex(0x4E, 0xC9, 0x8A), CouleurDe(couverture.Present));
        Assert.Equal(0.55, couverture.OpacitePresent);

        // Les pistes dessinent bien les données bindées : deux trous et le cadre de divergence dans la trace de rendu de la piste Niveau.
        // OnRender est appelé à la fin de l'Arrange d'un visuel invalidé (pas par RenderTargetBitmap.Render) : invalider, remettre en page, rendre.
        PisteBase.TracerPourTests = true;
        niveau.InvalidateVisual();
        vue.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(niveau.ActualWidth), (int)Math.Ceiling(niveau.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bmp.Render(niveau);
        Assert.Equal(2, niveau.TraceRendu.Count(l => l.StartsWith("trou ", StringComparison.Ordinal)));
        Assert.Contains(niveau.TraceRendu, l => l.StartsWith("divergence ", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ 760 → 1 400 px (DESIGN_PLAN §8)

    [WpfFact]
    public void A_760_px_rien_ne_deborde_et_a_1400_px_tout_s_etire()
    {
        var (vue, _) = Monter();
        var defilement = Assert.IsType<ScrollViewer>(vue.FindName("Defilement"));

        MettreEnPage(vue, 760, 480);
        Assert.True(vue.DesiredSize.Width <= 760, $"DesiredSize.Width={vue.DesiredSize.Width}");
        var grille = GrillePistes(vue);
        foreach (var libelle in Visibles<TextBlock>(grille).Where(t => LibellesPistes.Contains(t.Text)))
            Assert.True(libelle.ActualWidth <= 96, $"« {libelle.Text} » déborde de sa colonne : {libelle.ActualWidth}");
        var largeurs = PistesVisibles(grille).Select(p => p.ActualWidth).ToList();
        Assert.All(largeurs, l => Assert.True(l > 0));
        Assert.Single(largeurs.Distinct());

        // Hauteur réellement disponible pour la vue à la taille minimale de la fenêtre (480 − en-tête 92 − marges) : le corps défile.
        MettreEnPage(vue, 760, 360);
        Assert.True(defilement.ScrollableHeight > 0, $"ScrollableHeight={defilement.ScrollableHeight}");

        MettreEnPage(vue, 1400, 900);
        Assert.Equal(0, defilement.ScrollableHeight);
        Assert.True(Assert.Single(Visibles<PisteNiveau>(GrillePistes(vue))).ActualWidth > 1000);
        var dernier = Assert.Single(Visibles<TextBlock>(vue), t => t.Text == "ven. 25");
        var (left, largeurCanvas) = PositionDe(dernier);
        Assert.True(left > 0 && left < largeurCanvas, $"ven. 25 à {left} sur {largeurCanvas}");
    }
}
