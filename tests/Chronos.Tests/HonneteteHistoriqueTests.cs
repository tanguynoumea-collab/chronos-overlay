using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Chronos.Controls.Historique;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
using Chronos.Rendering.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Text;
using Chronos.ViewModels.Historique;
using Chronos.Views.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-06 — l'HONNÊTETÉ DE BOUT EN BOUT, prouvée sur ce que l'utilisateur verra (34-08, D-34-35) : chaque test lit
/// <see cref="ScenariosHistorique"/> par <see cref="SourceHistoriqueDemonstration"/> — les objets EXACTS de la galerie
/// <c>--historique</c> — et vérifie, dans les deux vues et les trois styles de la Semaine, les MOTS (les <c>TextBlock</c> visibles de
/// l'arbre réel) et les TRAITS (la trace de rendu des pistes, <c>PisteBase.TraceRendu</c>) : trous nommés et pointillés, saut non
/// localisé en bloc plat, divergence encadrée et son pied, zone antérieure au journal datée, libellé permanent des tokens et pied de
/// page sur chaque vue, plateau épuisé gris et nommé, rien de dessiné entre deux relevés absents, semaine vide qui le dit, mots du
/// plan de design, ligne de fraîcheur du jour.
///
/// Ce que <c>dotnet build</c> ne voit pas : un escalier qui enjambe un trou, un saut dessiné en barre au réveil, un cadre violet
/// sur une heure dont on ne sait rien, un libellé d'honnêteté disparu d'un style. Aucun fichier, aucun exe : réglages en mémoire,
/// horloge figée. Collection sérialisée (course du chargeur BAML, Pitfall 12).
/// </summary>
[Collection("XAML WPF")]
public class HonneteteHistoriqueTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();

    // Les faits du scénario (heure de Paris = UTC+2 en septembre) — relus dans ScenariosHistorique, pas réinventés.
    private static readonly DateTimeOffset TrouADebut = Utc("2026-09-22T21:00:00Z");    // « Chronos arrêté » mar. 23:00
    private static readonly DateTimeOffset TrouAFin = Utc("2026-09-23T05:00:00Z");      // → mer. 07:00
    private static readonly DateTimeOffset TrouBDebut = Utc("2026-09-24T12:00:00Z");    // « jeton invalide » jeu. 14:00
    private static readonly DateTimeOffset TrouBFin = Utc("2026-09-24T14:00:00Z");      // → jeu. 16:00
    private static readonly DateTimeOffset PlateauDebut = Utc("2026-09-23T18:00:00Z");  // épuisée mer. 20:00
    private static readonly DateTimeOffset PlateauFin = Utc("2026-09-23T22:00:00Z");    // → jeu. 00:00
    private static readonly DateTimeOffset DivergenceDebut = Utc("2026-09-23T19:00:00Z");
    private static readonly DateTimeOffset DivergenceFin = Utc("2026-09-23T21:00:00Z");

    private static readonly HistoriqueStyleSemaine[] TroisStyles =
        { HistoriqueStyleSemaine.Pistes, HistoriqueStyleSemaine.Simplifie, HistoriqueStyleSemaine.Tuiles };

    /// <summary>Les mots de projection interdits (même motif que <c>GardeVocabulaireHistoriqueTests</c>, D-34-36).</summary>
    private static readonly Regex Projection = new(@"épuisé[e]? vers|à ce rythme|projection|prévision|estim(é|ation|er)|tendance|dans \d+ ?h\b",
                                                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private const double Tol = 0.002;   // la trace écrit trois décimales ; 0,002 de 168 h ≈ 20 min, de 24 h ≈ 3 min

    // ------------------------------------------------------------------ Montage (mêmes objets que la galerie)

    private static HistoriqueViewModel Vm(HistoriqueStyleSemaine style = HistoriqueStyleSemaine.Pistes, ISourceHistorique? source = null)
    {
        var reglages = new ReglagesHistoriqueMemoire();
        reglages.Modifier(s => s with { HistoriqueStyleSemaine = style });
        var vm = new HistoriqueViewModel(source ?? new SourceHistoriqueDemonstration(Tz), new FakeUiDispatcher { OnUiThread = true },
                                         ScenariosHistorique.HorlogeFigee(Tz), Tz, reglages);
        vm.Ouvrir();
        vm.AttendreLecture().GetAwaiter().GetResult();
        return vm;
    }

    /// <summary>La vue Semaine seule (920 × 900) dans le style demandé, au présent ou sur S-1.</summary>
    private static (VueSemaineView Vue, HistoriqueViewModel Vm) MonterSemaine(HistoriqueStyleSemaine style = HistoriqueStyleSemaine.Pistes,
                                                                             bool semainePrecedente = false, ISourceHistorique? source = null)
    {
        var vm = Vm(style, source);
        if (semainePrecedente)
        {
            vm.PrecedentCommand.Execute(null);
            vm.AttendreLecture().GetAwaiter().GetResult();
        }
        var vue = new VueSemaineView { DataContext = vm };
        MettreEnPage(vue, 920, 900);
        return (vue, vm);
    }

    /// <summary>La vue Jour seule : 0 = jeu. 24 (aujourd'hui), 1 = mer. 23.</summary>
    private static (VueJourView Vue, HistoriqueViewModel Vm) MonterJour(int joursEnArriere = 0)
    {
        var vm = Vm();
        vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        vm.AttendreLecture().GetAwaiter().GetResult();
        for (var i = 0; i < joursEnArriere; i++)
        {
            vm.PrecedentCommand.Execute(null);
            vm.AttendreLecture().GetAwaiter().GetResult();
        }
        var vue = new VueJourView { DataContext = vm };
        MettreEnPage(vue, 920, 900);
        return (vue, vm);
    }

    /// <summary>La vraie fenêtre (celle de la galerie) sur la source donnée, racine mise en page à 920 × 610.</summary>
    private static (HistoriqueWindow Fenetre, HistoriqueViewModel Vm, FrameworkElement Racine) MonterFenetre(ISourceHistorique? source = null)
    {
        var vm = Vm(HistoriqueStyleSemaine.Pistes, source);
        var fenetre = new HistoriqueWindow(vm);
        var racine = (FrameworkElement)fenetre.Content!;
        racine.DataContext = vm;
        MettreEnPage(racine, 920, 610);
        return (fenetre, vm, racine);
    }

    private static void BasculerEnJour(HistoriqueViewModel vm, FrameworkElement racine, double largeur, double hauteur)
    {
        vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        vm.AttendreLecture().GetAwaiter().GetResult();
        MettreEnPage(racine, largeur, hauteur);
    }

    /// <summary>Deux passes Measure / Arrange : les Canvas.Left bindés sur ActualWidth ne sont justes qu'après la première.</summary>
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

    /// <summary>Parcours de l'arbre visuel dans l'ordre du document, SANS entrer dans un sous-arbre caché : ce que l'œil voit.</summary>
    private static IEnumerable<T> Visibles<T>(DependencyObject racine) where T : DependencyObject
    {
        if (racine is UIElement ui && ui.Visibility != Visibility.Visible) yield break;
        if (racine is T t) yield return t;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(racine); i++)
            foreach (var x in Visibles<T>(VisualTreeHelper.GetChild(racine, i)))
                yield return x;
    }

    private static List<string> TextesVisibles(DependencyObject racine) => Visibles<TextBlock>(racine).Select(t => t.Text).ToList();

    private static Grid GrilleVisible(VueSemaineView vue)
        => Assert.Single(new[] { "StylePistes", "StyleSimplifie", "StyleTuiles" }.Select(n => Assert.IsType<Grid>(vue.FindName(n))),
                         g => g.Visibility == Visibility.Visible);

    /// <summary>Rend une piste hors écran et rend SA trace (InvalidateVisual → UpdateLayout → Render : OnRender est rejoué à l'arrangement).</summary>
    private static List<string> TraceRendu(PisteBase piste)
    {
        PisteBase.TracerPourTests = true;
        try
        {
            piste.InvalidateVisual();
            piste.UpdateLayout();
            var bmp = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(piste.ActualWidth)), Math.Max(1, (int)Math.Ceiling(piste.ActualHeight)),
                                             96, 96, PixelFormats.Pbgra32);
            bmp.Render(piste);
            return piste.TraceRendu.ToList();
        }
        finally
        {
            PisteBase.TracerPourTests = false;
        }
    }

    private static List<string> Lignes(List<string> trace, string primitive) => trace.Where(l => l.StartsWith(primitive + " ", StringComparison.Ordinal)).ToList();

    /// <summary>Le n-ième champ numérique d'une ligne de trace (0 = premier après le mot de la primitive).</summary>
    private static double Champ(string ligne, int n) => double.Parse(ligne.Split(' ')[n + 1], NumberStyles.Float, CultureInfo.InvariantCulture);

    private static double Fr(DateTimeOffset t, Plage p) => EchelleTemps.Fraction(t, p);

    private static double Recouvrement(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0);

    private static Color CouleurDe(Brush? b) => Assert.IsType<SolidColorBrush>(b).Color;
    private static Color Hex(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    private static readonly Color CouleurLine = Hex(0x2C, 0x29, 0x42);
    private static readonly Color Ink2 = Hex(0xA9, 0xA6, 0xC4);
    private static readonly Color Accent = Hex(0x8B, 0x7B, 0xF0);
    private static readonly Color HistoGris = Hex(0x5A, 0x59, 0x60);

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static string Dire(List<string> trace) => "\n  trace : " + string.Join(" | ", trace);

    // ------------------------------------------------------------------ 1. Trou : l'escalier s'interrompt, rectangle Line 35 % pointillé, nommé

    [WpfFact]
    public void Un_trou_interrompt_l_escalier_et_se_dessine_en_rectangle_pointille_nomme()
    {
        foreach (var style in TroisStyles)
        {
            var (vue, vm) = MonterSemaine(style);
            var grille = GrilleVisible(vue);
            var plage = vm.DonneesSemaine!.Plage;
            var alerteDuTheme = CouleurDe(vm.Theme.BrushTokens()["Alerte"]);

            var niveau = Assert.Single(Visibles<PisteNiveau>(grille));
            var trace = TraceRendu(niveau);
            var trous = Lignes(trace, "trou");
            Assert.True(trous.Count == 2, $"[{style}] exactement deux trous attendus" + Dire(trace));
            Assert.EndsWith(" ChronosArrete", trous[0], StringComparison.Ordinal);
            Assert.EndsWith(" JetonInvalide", trous[1], StringComparison.Ordinal);
            Assert.Equal(Fr(TrouADebut, plage), Champ(trous[0], 0), Tol);
            Assert.Equal(Fr(TrouAFin, plage), Champ(trous[0], 1), Tol);
            Assert.Equal(Fr(TrouBDebut, plage), Champ(trous[1], 0), Tol);
            Assert.Equal(Fr(TrouBFin, plage), Champ(trous[1], 1), Tol);

            // L'escalier s'interrompt : aucun palier (rampe ou gris) ne recouvre l'intérieur d'un trou.
            foreach (var palier in Lignes(trace, "palier"))
                foreach (var (d, f) in new[] { (TrouADebut, TrouAFin), (TrouBDebut, TrouBFin) })
                    Assert.True(Recouvrement(Champ(palier, 0), Champ(palier, 1), Fr(d, plage), Fr(f, plage)) <= 0.0005,
                                $"[{style}] le palier « {palier} » enjambe le trou {d:u} → {f:u} : une ligne interpolée");

            // Le contrat « rectangle Line 35 % à bordure pointillée grise / ambre ».
            Assert.Equal(0.35, niveau.OpaciteTrou);
            Assert.Equal(CouleurLine, CouleurDe(niveau.FondTrou));
            Assert.Equal(HistoGris, CouleurDe(niveau.BordTrouArrete));
            Assert.Equal(alerteDuTheme, CouleurDe(niveau.BordTrouJeton));

            // La couverture dit la même chose, dans le même ordre.
            var couverture = Lignes(TraceRendu(Assert.Single(Visibles<PisteCouverture>(grille))), "trou");
            Assert.Equal(2, couverture.Count);
            Assert.EndsWith(" arrete", couverture[0], StringComparison.Ordinal);
            Assert.EndsWith(" jeton", couverture[1], StringComparison.Ordinal);

            // Les mots : « Chronos arrêté » en gris, « jeton invalide » en ambre du thème.
            var arrete = Assert.Single(Visibles<TextBlock>(grille), t => t.Text == "Chronos arrêté");
            Assert.Equal(Ink2, CouleurDe(arrete.Foreground));
            var jeton = Assert.Single(Visibles<TextBlock>(grille), t => t.Text == "jeton invalide");
            Assert.Equal(alerteDuTheme, CouleurDe(jeton.Foreground));
        }
    }

    // ------------------------------------------------------------------ 2. Saut non localisé : bloc plat gris, jamais une barre au réveil

    [WpfFact]
    public void Le_saut_d_une_absence_est_un_bloc_plat_jamais_une_barre_au_reveil()
    {
        foreach (var style in TroisStyles)
        {
            var (vue, vm) = MonterSemaine(style);
            var grille = GrilleVisible(vue);
            var donnees = vm.DonneesSemaine!;
            var plage = donnees.Plage;

            var trace = TraceRendu(Assert.Single(Visibles<PisteNiveau>(grille)));
            var saut = Assert.Single(Lignes(trace, "saut"));
            Assert.Equal(Fr(TrouADebut, plage), Champ(saut, 0), Tol);   // le bloc couvre TOUT le trou…
            Assert.Equal(Fr(TrouAFin, plage), Champ(saut, 1), Tol);     // … pas une heure au réveil
            Assert.Equal(0.02, Champ(saut, 3) - Champ(saut, 2), 6);

            // Aucun Δ ne traverse le trou (l'analyse le range en saut) ; la barre de l'heure du réveil n'additionne que ce qui a été
            // relevé APRÈS le réveil.
            Assert.DoesNotContain(donnees.Analyse.DeltasHebdo, d => d.De <= TrouADebut && d.A >= TrouAFin);
            Assert.DoesNotContain(donnees.Analyse.Deltas5h, d => d.De <= TrouADebut && d.A >= TrouAFin);
            if (style != HistoriqueStyleSemaine.Simplifie)
            {
                var rythme = TraceRendu(Assert.Single(Visibles<PisteRythme>(grille)));
                var reveil = Fr(TrouAFin, plage);
                var dansLHeure = donnees.Analyse.Serie.Where(r => r.T >= TrouAFin && r.T < TrouAFin.AddHours(1) && r.U5 is not null).ToList();
                var releveApresReveil = dansLHeure.Count == 0 ? 0 : dansLHeure[^1].U5!.Value - dansLHeure[0].U5!.Value;
                var barresDuReveil = Lignes(rythme, "barre").Where(b => Champ(b, 0) - Tol <= reveil && reveil < Champ(b, 1) - Tol).ToList();
                Assert.True(barresDuReveil.Count == 1, $"[{style}] une barre attendue pour l'heure du réveil (07:00)" + Dire(rythme));
                foreach (var barre in barresDuReveil)
                    Assert.True(Champ(barre, 2) <= releveApresReveil + 0.001,
                                $"[{style}] « {barre} » : la barre du réveil porte plus que ce qui a été relevé après le réveil ({releveApresReveil:0.000})");
            }

            Assert.Contains("+2 % pendant l'absence (répartition inconnue)", TextesVisibles(grille));
        }
    }

    // ------------------------------------------------------------------ 3. Divergence : cadre Accent + pied « consommé ailleurs »

    [WpfFact]
    public void Une_marche_sans_tokens_Code_est_encadree_et_le_pied_le_dit()
    {
        foreach (var style in TroisStyles)
        {
            var (vue, vm) = MonterSemaine(style);
            var grille = GrilleVisible(vue);
            var plage = vm.DonneesSemaine!.Plage;

            var niveau = Assert.Single(Visibles<PisteNiveau>(grille));
            var trace = TraceRendu(niveau);
            var divergence = Assert.Single(Lignes(trace, "divergence"));
            Assert.Equal(Fr(DivergenceDebut, plage), Champ(divergence, 0), Tol);
            Assert.Equal(Fr(DivergenceFin, plage), Champ(divergence, 1), Tol);
            Assert.Equal(Accent, CouleurDe(niveau.CadreDivergence));

            Assert.Contains(TextesHistorique.PiedDivergence, TextesVisibles(vue));   // « … consommé ailleurs (Cowork, claude.ai) »
            Assert.Equal(Visibility.Visible, Assert.IsAssignableFrom<FrameworkElement>(vue.FindName("PiedDivergence")).Visibility);

            // Le zéro des tokens sous la marche est VRAI : heures couvertes, sans barre, sans hachure.
            var tokens = TraceRendu(Assert.Single(Visibles<PisteTokens>(grille)));
            var (p0, p1) = (Fr(PlateauDebut, plage), Fr(PlateauFin, plage));
            Assert.DoesNotContain(Lignes(tokens, "tokens"), l => Recouvrement(Champ(l, 0), Champ(l, 1), p0, p1) > 0.0005);
            Assert.DoesNotContain(Lignes(tokens, "hachure"), l => Recouvrement(Champ(l, 0), Champ(l, 1), p0, p1) > 0.0005);
        }
    }

    // ------------------------------------------------------------------ 4. Avant le journal : zone vide, hachurée, datée

    [WpfFact]
    public void La_zone_anterieure_au_journal_est_vide_et_datee()
    {
        var (vue, vm) = MonterSemaine(HistoriqueStyleSemaine.Pistes, semainePrecedente: true);
        var grille = GrilleVisible(vue);
        var plage = vm.DonneesSemaine!.Plage;
        var ouverture = Fr(ScenariosHistorique.JournalOuvertLe, plage);

        Assert.Contains("journal ouvert le 14 sept. 2026", TextesVisibles(grille));

        var niveau = Assert.Single(Visibles<PisteNiveau>(grille));
        var zone = Assert.Single(Visibles<Rectangle>(grille), r => r.Name.StartsWith("ZoneAvantJournal", StringComparison.Ordinal));
        Assert.Equal(EchelleTemps.Largeur(plage.Debut, ScenariosHistorique.JournalOuvertLe, plage, niveau.ActualWidth), zone.ActualWidth, 1.0);
        Assert.True(zone.ActualWidth > 0);

        var trace = TraceRendu(niveau);
        Assert.NotEmpty(Lignes(trace, "palier"));
        Assert.DoesNotContain(Lignes(trace, "palier"), l => Champ(l, 0) < ouverture - Tol);   // jamais une courbe reconstituée

        var present = Assert.Single(Lignes(TraceRendu(Assert.Single(Visibles<PisteCouverture>(grille))), "present"));
        Assert.Equal(ouverture, Champ(present, 0), Tol);   // rien n'est « présent » avant la première ligne du journal
    }

    // ------------------------------------------------------------------ 5. Libellé permanent des tokens + pied de page fixe, sur chaque vue

    [WpfFact]
    public void Le_libelle_permanent_des_tokens_et_le_pied_de_page_sont_sur_chaque_vue()
    {
        const string ParHeure = "par heure, comptés localement — hors Cowork et claude.ai · bruts, non pondérés · ce n'est PAS un % du forfait";
        const string ParQuart = "par quart d'heure, comptés localement — hors Cowork et claude.ai · bruts, non pondérés · ce n'est PAS un % du forfait";

        foreach (var style in TroisStyles)
        {
            var textes = TextesVisibles(MonterSemaine(style).Vue);
            Assert.True(textes.Contains(ParHeure), $"[{style}] libellé permanent des tokens absent");
            Assert.True(textes.Contains(TextesHistorique.PiedDePage), $"[{style}] pied de page absent");
        }

        var jour = TextesVisibles(MonterJour().Vue);
        Assert.Contains(ParQuart, jour);
        Assert.Contains(TextesHistorique.PiedDePage, jour);

        // Dans la fenêtre : le pied est visible par la vue ACTIVE, en Semaine comme en Jour.
        var (_, vm, racine) = MonterFenetre();
        Assert.Equal(1, TextesVisibles(racine).Count(t => t == TextesHistorique.PiedDePage));
        Assert.Contains(ParHeure, TextesVisibles(racine));
        BasculerEnJour(vm, racine, 920, 610);
        Assert.Equal(1, TextesVisibles(racine).Count(t => t == TextesHistorique.PiedDePage));
        Assert.Contains(ParQuart, TextesVisibles(racine));
    }

    // ------------------------------------------------------------------ 6. Épuisée : gris et nommée (Jour), tuile grise (Tuiles)

    [WpfFact]
    public void L_epuisee_est_grise_et_nommee_le_mercredi_et_la_tuile_l_est_aussi()
    {
        var (vue, vm) = MonterJour(joursEnArriere: 1);
        var plage = vm.DonneesJour!.Plage;
        Assert.Equal(Utc("2026-09-22T22:00:00Z"), plage.Debut);   // mercredi 23 local

        var trace = TraceRendu(Assert.Single(Visibles<PisteNiveau>(vue)));
        var gris = Lignes(trace, "palier").Where(l => l.EndsWith(" premierplan gris", StringComparison.Ordinal)).ToList();
        Assert.True(gris.Count >= 1, "aucun palier gris le mercredi" + Dire(trace));
        var (p0, p1) = (Fr(PlateauDebut, plage), Fr(PlateauFin, plage));
        Assert.True(gris.Min(l => Champ(l, 0)) <= p0 + Tol, "le gris doit commencer au début du plateau (20:00)" + Dire(trace));
        Assert.True(gris.Max(l => Champ(l, 1)) >= p1 - 2 * Tol, "le gris doit durer jusqu'à la fin du plateau (00:00)" + Dire(trace));
        Assert.DoesNotContain(Lignes(trace, "palier"),
                              l => l.EndsWith(" premierplan rampe", StringComparison.Ordinal) && Recouvrement(Champ(l, 0), Champ(l, 1), p0, p1) > 0.0005);
        Assert.Contains(TextesHistorique.Epuisee, TextesVisibles(vue));
        Assert.Equal("épuisée à 100 % — le serveur refuse (statut rejected)", TextesHistorique.Epuisee);

        var (tuiles, vmTuiles) = MonterSemaine(HistoriqueStyleSemaine.Tuiles);
        var semaine = vmTuiles.DonneesSemaine!.Plage;
        var traceTuiles = TraceRendu(Assert.Single(Visibles<PisteFenetres5h>(GrilleVisible(tuiles))));
        var grise = Assert.Single(Lignes(traceTuiles, "tuile"), l => l.Split(' ')[4] == "grise");
        Assert.Equal(1.0, Champ(grise, 2), 6);
        Assert.Equal(Fr(PlateauFin, semaine), Champ(grise, 1), Tol);
    }

    // ------------------------------------------------------------------ 7. Rien n'est dessiné entre deux relevés absents

    [WpfFact]
    public void Rien_n_est_dessine_entre_deux_relevés_absents_de_plus_de_deux_cadences()
    {
        foreach (var style in new[] { HistoriqueStyleSemaine.Pistes, HistoriqueStyleSemaine.Tuiles })
        {
            var (vue, _) = MonterSemaine(style);
            var grille = GrilleVisible(vue);
            var traceNiveau = TraceRendu(Assert.Single(Visibles<PisteNiveau>(grille)));
            var trous = Lignes(traceNiveau, "trou").Select(l => (F0: Champ(l, 0), F1: Champ(l, 1))).ToList();
            Assert.Equal(2, trous.Count);

            // Les primitives qui portent une VALEUR relevée : ni leur début ni leur étendue ne vivent dans un trou. Peuvent y vivre :
            // trou, saut (le bloc de l'absence), fantôme (S-1), hachure / tokens (axe indépendant : Claude Code tournait), et les
            // tirets / traits de reset datés par le resets_at annoncé par le serveur (un instant connu, pas une valeur).
            var primitives = new List<(string Piste, string Ligne)>();
            primitives.AddRange(Lignes(traceNiveau, "palier").Select(l => ("Niveau", l)));
            foreach (var p in Visibles<PisteRythme>(grille)) primitives.AddRange(Lignes(TraceRendu(p), "barre").Select(l => ("Rythme", l)));
            foreach (var p in Visibles<PisteFenetres5h>(grille)) primitives.AddRange(Lignes(TraceRendu(p), "tuile").Select(l => ("Fenetres5h", l)));
            Assert.NotEmpty(primitives);

            foreach (var (piste, ligne) in primitives)
                foreach (var (f0, f1) in trous)
                {
                    var debut = Champ(ligne, 0);
                    Assert.False(f0 + 0.0005 < debut && debut < f1 - 0.0005, $"[{style}] {piste} : « {ligne} » commence dans le trou ]{f0}, {f1}[");
                    if (ligne.StartsWith("palier ", StringComparison.Ordinal))
                        Assert.True(Recouvrement(debut, Champ(ligne, 1), f0, f1) <= 0.0005, $"[{style}] {piste} : « {ligne} » enjambe le trou ]{f0}, {f1}[");
                }
        }
    }

    // ------------------------------------------------------------------ 8. Semaine vide : « aucun relevé », rien de dessiné

    [WpfFact]
    public void La_semaine_vide_dit_aucun_releve_et_ne_dessine_rien()
    {
        var cadence = RateLimitHeaderUsageProvider.CadenceNominale;
        var vide = new FakeSourceHistorique(Tz)
        {
            TransformerSemaine = d => d with
            {
                Analyse = AnalyseReleves.Analyser(new LectureJournal(Array.Empty<ReleveJournal>(), Array.Empty<EvenementJournal>(), 0, d.JournalOuvertLe, d.Plage),
                                                  InstantsHistorique.InstantDAnalyse(d.LueA, d.Plage), cadence),
                Barres = d.Barres.Select(b => b with { Principal = TotauxTokens.Vide, SousAgents = TotauxTokens.Vide, Etat = EtatCouverture.HorsCouverture }).ToList(),
                Divergences = Array.Empty<Divergence>(),
            },
        };

        var (_, vm, racine) = MonterFenetre(vide);
        Assert.Equal("aucun relevé sur cette semaine de forfait", vm.TexteFraicheur);
        var textes = TextesVisibles(racine);
        Assert.Contains("aucun relevé sur cette semaine de forfait", textes);
        Assert.DoesNotContain("0 %", textes);

        var vue = Assert.Single(Visibles<VueSemaineView>(racine));
        var niveau = TraceRendu(Assert.Single(Visibles<PisteNiveau>(vue)));
        Assert.Empty(Lignes(niveau, "palier"));
        Assert.Empty(Lignes(niveau, "trou"));
        var tokens = TraceRendu(Assert.Single(Visibles<PisteTokens>(vue))).Where(l => !l.StartsWith("grille ", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(tokens);
        Assert.All(tokens, l => Assert.StartsWith("hachure ", l, StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ 9. Les mots de la fenêtre sont ceux du plan de design

    [WpfFact]
    public void Les_mots_de_la_fenetre_sont_ceux_du_plan_de_design()
    {
        var (_, vm, racine) = MonterFenetre();
        var semaine = TextesVisibles(racine);
        foreach (var attendu in new[] { "Historique", "Semaine", "Jour", "4 semaines", "Cette semaine", "Style :" })
            Assert.Contains(attendu, semaine);
        VerifierAucuneProjection(semaine);

        BasculerEnJour(vm, racine, 920, 610);
        var jour = TextesVisibles(racine);
        foreach (var attendu in new[] { "Historique", "Semaine", "Jour", "4 semaines", "Aujourd'hui" })
            Assert.Contains(attendu, jour);
        VerifierAucuneProjection(jour);
    }

    private static void VerifierAucuneProjection(List<string> textes)
    {
        Assert.NotEmpty(textes);
        foreach (var t in textes)
        {
            Assert.False(Projection.IsMatch(t), $"mot de projection affiché : « {t} »");
            Assert.DoesNotContain("mesure", t, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("estimation", t, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------------ 11. Repères de l'axe NIVEAU (grille 0 / 50 / 100 %, D-34-29)

    [WpfFact]
    public void Les_reperes_de_l_axe_niveau_sont_poses_sur_chaque_vue()
    {
        foreach (var style in TroisStyles)
        {
            var grille = GrilleVisible(MonterSemaine(style).Vue);
            var textes = TextesVisibles(grille);
            Assert.True(textes.Count(t => t == "100 %") == 1, $"[{style}] repère « 100 % » de NIVEAU absent");
            Assert.True(textes.Count(t => t == "0") == 1, $"[{style}] repère « 0 » de NIVEAU absent");
        }
        var jour = TextesVisibles(MonterJour().Vue);
        Assert.Equal(1, jour.Count(t => t == "100 %"));
        Assert.Equal(1, jour.Count(t => t == "0"));
    }

    // ------------------------------------------------------------------ 10. Fraîcheur du jour : attendus, présents, interruptions nommées

    [WpfFact]
    public void La_fraicheur_du_jour_compte_les_attendus_et_nomme_les_interruptions()
    {
        var (_, jeudi) = MonterJour();
        var n = jeudi.DonneesJour!.Analyse.Serie.Count;
        Assert.Equal($"288 relevés attendus · {n} présents · 1 interruption (jeton invalide, 14:00 → 16:00)", jeudi.TexteFraicheur);

        // Mercredi : le trou « Chronos arrêté » commence mardi 23:00 ; l'analyse du JOUR commence à son premier relevé (07:00) et ne
        // voit pas l'absence de la nuit (limite connue de 32-06 : aucun Δ ni saut ne traverse Plage.Debut). Comportement RÉEL épinglé ;
        // candidat phase 35 : lire [Debut − cadence, Fin[ pour qu'un trou ouvert à minuit soit nommé (voir 34-08-SUMMARY).
        var (_, mercredi) = MonterJour(joursEnArriere: 1);
        var m = mercredi.DonneesJour!.Analyse.Serie.Count;
        Assert.Equal($"288 relevés attendus · {m} présents · 0 interruption", mercredi.TexteFraicheur);
        Assert.Equal(TrouAFin, mercredi.DonneesJour.Analyse.Serie[0].T);   // rien n'est inventé avant 07:00
    }
}
