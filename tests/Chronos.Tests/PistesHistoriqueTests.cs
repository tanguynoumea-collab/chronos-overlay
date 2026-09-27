using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Chronos.Controls.Historique;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
using Chronos.Rendering.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Text;
using Chronos.Theming;
using Chronos.ViewModels.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-07 (plan 34-04) — les PISTES de la fenêtre Historique, au niveau du contrôle : un <c>FrameworkElement</c> par piste,
/// qui transcrit les listes pures de <c>Rendering/Historique</c> en géométries gelées, ne choisit aucune couleur (brosses par
/// DP, défaut <c>null</c> = ne rien dessiner) et ne se redessine que sur nouvelle référence de données, changement de plage ou
/// de taille — jamais parce que le réticule ou « maintenant » ont bougé.
///
/// <para>Les tests lisent la TRACE DE RENDU (<c>PisteBase.TraceRendu</c>, activée par <c>TracerPourTests</c>), pas les pixels :
/// chaque primitive dessinée écrit une ligne (« palier f0 f1 u rampe », « trou f0 f1 ChronosArrete », « tuile … grise »). Les
/// fractions sont celles d'<c>EchelleTemps.Fraction</c> sur la plage de la piste. Thread STA sans BAML : aucune collection
/// XAML n'est nécessaire. Les couleurs en dur sont AUTORISÉES ici (ce sont des brosses de test), jamais dans <c>src/</c>.</para>
/// </summary>
public class PistesHistoriqueTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();

    static PistesHistoriqueTests()
    {
        PisteBase.TracerPourTests = true;
    }

    // ------------------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------------------

    /// <summary>Mesure, arrange et rend hors écran (le rendu forcé du plan : Measure / Arrange / RenderTargetBitmap.Render).</summary>
    private static void Rendre(FrameworkElement e, double w, double h)
    {
        e.Measure(new Size(w, h));
        e.Arrange(new Rect(0, 0, w, h));
        var bmp = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(w)), Math.Max(1, (int)Math.Ceiling(h)), 96, 96, PixelFormats.Pbgra32);
        bmp.Render(e);
    }

    private static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    private static DateTimeOffset Now => ScenariosHistorique.Maintenant(Tz);

    /// <summary>La semaine de référence des maquettes (D-34-16), lue par la façade de démonstration comme le ferait le VM.</summary>
    private static DonneesSemaine Semaine()
    {
        var plage = ScenariosHistorique.Semaine(Tz);
        var precedente = BornesPlage.SemaineDeForfait(plage.Debut.AddTicks(-1), plage.Debut, null, Tz);
        return new SourceHistoriqueDemonstration(Tz).LireSemaine(plage, precedente, Now);
    }

    /// <summary>Le jour local <paramref name="jour"/> de septembre 2026 dans la semaine de référence.</summary>
    private static DonneesJour Jour(int jour)
    {
        var plage = BornesPlage.Jour(new DateTimeOffset(2026, 9, jour, 12, 0, 0, TimeSpan.Zero), Tz);
        return new SourceHistoriqueDemonstration(Tz).LireJour(plage, Now);
    }

    private static PisteNiveau NiveauComplet(DonneesSemaine d, VariantePisteNiveau variante = VariantePisteNiveau.SemaineComplete) => new()
    {
        Plage = d.Plage, Analyse = d.Analyse, Precedente = d.Precedente, PlagePrecedente = d.PlagePrecedente,
        Divergences = d.Divergences, InstantLecture = d.LueA, Variante = variante,
        Rampe = ThemeCatalog.Default, TraitReset = B(Colors.White), TraitFin = B(Colors.Silver), Gris = B(Colors.Gray),
        FondTrou = B(Colors.Black), BordTrouArrete = B(Colors.DimGray), BordTrouJeton = B(Colors.Orange),
        CadreDivergence = B(Colors.Violet), Grille = B(Colors.DarkSlateGray),
        EpaisseurFine = 1, EpaisseurEscalier = 2.2, EpaisseurPremierPlan = 2.4, LongueurTiretReset = 8, OpaciteTrou = 0.35, NbBandes = 12,
    };

    private static SurcoucheReticule SurcoucheComplete(DonneesSemaine d) => new()
    {
        Plage = d.Plage, Serie = d.Analyse.Serie, Fuseau = Tz, InstantLecture = d.LueA,
        TraitReticule = B(Colors.White), TraitMaintenant = B(Colors.White), EpaisseurFine = 1, OpaciteMaintenant = 0.6,
    };

    private static List<string> Lignes(PisteBase p, string prefixe) => p.TraceRendu.Where(l => l.StartsWith(prefixe, StringComparison.Ordinal)).ToList();

    /// <summary>Les nombres d'une ligne de trace, dans l'ordre (les mots sont ignorés).</summary>
    private static double[] Nombres(string ligne) => ligne.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN)
        .Where(v => !double.IsNaN(v)).ToArray();

    private static double Fr(DateTimeOffset t, Plage plage) => EchelleTemps.Fraction(t, plage);

    private static DateTimeOffset Utc(int jour, int heure) => new(2026, 9, jour, heure, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------------------------------------
    // Task 1 — PisteBase, PisteNiveau, SurcoucheReticule
    // ------------------------------------------------------------------------------------------------

    [WpfFact]
    public void Une_piste_sans_donnees_ni_brosses_ne_dessine_rien_et_ne_leve_pas()
    {
        var piste = new PisteNiveau();
        Rendre(piste, 800, 150);
        Assert.Equal(1, piste.RendusPourTests);
        Assert.Empty(piste.TraceRendu);

        var surcouche = new SurcoucheReticule();
        Rendre(surcouche, 800, 150);
        Assert.Equal(1, surcouche.RendusPourTests);
        Assert.Empty(surcouche.TraceRendu);

        var d = Semaine();
        piste.Plage = d.Plage;                       // plage sans analyse : toujours rien
        Rendre(piste, 800, 150);
        Assert.Empty(piste.TraceRendu);

        // Analyse sans rampe : les paliers colorés sont sautés UN À UN, les trous (FondTrou posé) se dessinent quand même.
        piste.Analyse = d.Analyse;
        piste.InstantLecture = d.LueA;
        piste.FondTrou = B(Colors.Black);
        piste.OpaciteTrou = 0.35;
        Rendre(piste, 800, 150);
        Assert.DoesNotContain(piste.TraceRendu, l => l.StartsWith("palier ", StringComparison.Ordinal) && l.EndsWith(" rampe", StringComparison.Ordinal));
        Assert.Equal(2, Lignes(piste, "trou ").Count);
    }

    [WpfFact]
    public void Le_compteur_de_rendus_ne_bouge_pas_quand_seule_la_surcouche_change()
    {
        var d = Semaine();
        var piste = NiveauComplet(d);
        Rendre(piste, 800, 150);
        Rendre(piste, 800, 150);                     // sonde (Open Question 4) : un second rendu sans changement ne ré-invoque pas OnRender
        var avant = piste.RendusPourTests;
        var invalidations = piste.InvalidationsPourTests;
        Assert.Equal(1, avant);

        var surcouche = SurcoucheComplete(d);
        Rendre(surcouche, 800, 150);
        for (var i = 0; i < 60; i++)
        {
            surcouche.Maintenant = d.LueA.AddMinutes(i);
            surcouche.AfficherMaintenant = true;
            surcouche.Survoler(i * 10);
            Rendre(surcouche, 800, 150);
        }
        Assert.True(surcouche.RendusPourTests > 1, "la surcouche, elle, se redessine");

        Rendre(piste, 800, 150);
        Assert.Equal(avant, piste.RendusPourTests);
        Assert.Equal(invalidations, piste.InvalidationsPourTests);   // aucune DP AffectsRender de la piste n'a été touchée
    }

    [WpfFact]
    public void Une_nouvelle_reference_de_donnees_redessine()
    {
        var d = Semaine();
        var piste = NiveauComplet(d);
        Rendre(piste, 800, 150);
        var avant = piste.RendusPourTests;
        var invalidations = piste.InvalidationsPourTests;

        piste.Analyse = Semaine().Analyse;           // nouvelle instance issue d'une seconde lecture (même contenu)
        Rendre(piste, 800, 150);
        Assert.Equal(avant + 1, piste.RendusPourTests);
        Assert.Equal(invalidations + 1, piste.InvalidationsPourTests);
    }

    [WpfFact]
    public void Les_brosses_des_pistes_sont_des_DP_sans_couleur_par_defaut()
    {
        var sansRendu = new HashSet<string> { "TexteInfobulle", "InfobulleVisible", "InstantSurvole" };
        var defautsDouble = new Dictionary<string, double> { ["Max"] = EchelleValeur.MaxRythme };

        foreach (var type in TypesDePistes())
        {
            var dps = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.FieldType == typeof(DependencyProperty))
                .Select(f => (DependencyProperty)f.GetValue(null)!)
                .Where(dp => typeof(PisteBase).IsAssignableFrom(dp.OwnerType))   // les DP des pistes, pas celles héritées de FrameworkElement
                .ToList();
            Assert.NotEmpty(dps);

            foreach (var dp in dps)
            {
                var meta = Assert.IsAssignableFrom<FrameworkPropertyMetadata>(dp.GetMetadata(type));
                var nom = $"{type.Name}.{dp.Name}";
                Assert.False(dp.PropertyType == typeof(Color), $"{nom} : aucune DP de type Color (D-34-18)");
                if (dp.PropertyType == typeof(Brush))
                    Assert.True(meta.DefaultValue is null, $"{nom} : une brosse a null pour défaut (D-34-18)");
                if (dp.PropertyType == typeof(double))
                    Assert.Equal(defautsDouble.GetValueOrDefault(dp.Name, 0.0), (double)meta.DefaultValue!);
                if (dp.PropertyType == typeof(int)) Assert.Equal(dp.Name == "NbBandes" ? 12 : 0, (int)meta.DefaultValue!);
                if (dp.PropertyType == typeof(long)) Assert.Equal(dp.Name == "Plafond" ? 1L : 0L, (long)meta.DefaultValue!);
                if (!sansRendu.Contains(dp.Name))
                    Assert.True(meta.AffectsRender, $"{nom} doit porter AffectsRender");
            }
        }
    }

    private static IEnumerable<Type> TypesDePistes() => typeof(PisteBase).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && typeof(PisteBase).IsAssignableFrom(t));

    [WpfFact]
    public void L_escalier_de_niveau_ne_traverse_pas_les_trous_et_les_bandes_ont_leur_couleur()
    {
        var d = Semaine();
        var piste = NiveauComplet(d);
        Rendre(piste, 840, 150);

        var trous = Lignes(piste, "trou ");
        Assert.Equal(2, trous.Count);
        Assert.EndsWith("ChronosArrete", trous[0]);
        Assert.EndsWith("JetonInvalide", trous[1]);

        // Le trou A du scénario : mar. 22 sept. 23:00 → mer. 07:00 local (21:00Z → 05:00Z), au relevé près (5 min).
        var trouA = d.Analyse.Trous[0];
        Assert.True(Math.Abs((trouA.Debut - Utc(22, 21)).TotalMinutes) <= 5);
        Assert.Equal(Utc(23, 5), trouA.Fin);
        var (fA0, fA1) = (Fr(trouA.Debut, d.Plage), Fr(trouA.Fin!.Value, d.Plage));
        Assert.Equal(fA0, Nombres(trous[0])[0], 3);
        Assert.Equal(fA1, Nombres(trous[0])[1], 3);
        Assert.True(Math.Abs(Fr(Utc(22, 21), d.Plage) - Nombres(trous[0])[0]) <= 0.001);   // ± 0,001 de la fraction de 21:00Z

        // Aucun palier de rampe n'entre dans l'INTÉRIEUR strict d'un trou.
        var paliers = Lignes(piste, "palier ");
        Assert.NotEmpty(paliers);
        foreach (var trou in d.Analyse.Trous)
        {
            var (t0, t1) = (Fr(trou.Debut, d.Plage), Fr(trou.Fin ?? d.LueA, d.Plage));
            foreach (var l in paliers)
            {
                var n = Nombres(l);
                Assert.False(n[0] < t1 - 0.001 && n[1] > t0 + 0.001, $"palier dans un trou : {l}");   // la trace arrondit à 3 décimales
            }
        }

        var bandes = Lignes(piste, "bande ");
        Assert.NotEmpty(bandes);
        Assert.All(bandes, b => Assert.True(b.Contains(" rampe ") || b.Contains(" gris "), b));

        // Le saut hebdo du trou A : +0,02 pendant l'absence, bloc plat de la durée du trou.
        var saut = Assert.Single(Lignes(piste, "saut "));
        var s = Nombres(saut);
        Assert.Equal(fA0, s[0], 3);
        Assert.Equal(fA1, s[1], 3);
        Assert.Equal(0.02, s[3] - s[2], 3);

        Assert.Equal(d.Divergences.Count, Lignes(piste, "divergence ").Count);
        Assert.True(d.Divergences.Count >= 1);
        Assert.NotEmpty(Lignes(piste, "fantome "));
        Assert.NotEmpty(Lignes(piste, "dents "));
        Assert.Equal(d.Analyse.Resets5h.Count(r => d.Plage.Contient(r.Instant)), Lignes(piste, "tiret ").Count);
        Assert.Contains(piste.TraceRendu, l => l.StartsWith("grille 0.5", StringComparison.Ordinal));
    }

    [WpfFact]
    public void La_variante_Jour_met_le_5h_au_premier_plan_et_grise_l_epuisee()
    {
        var j = Jour(23);
        var piste = new PisteNiveau
        {
            Plage = j.Plage, Analyse = j.Analyse, InstantLecture = j.LueA, Variante = VariantePisteNiveau.Jour,
            Rampe = ThemeCatalog.Default, TraitReset = B(Colors.White), TraitFin = B(Colors.Silver), Gris = B(Colors.Gray),
            FondTrou = B(Colors.Black), BordTrouArrete = B(Colors.DimGray), BordTrouJeton = B(Colors.Orange), Grille = B(Colors.DarkSlateGray),
            EpaisseurFine = 1, EpaisseurEscalier = 2.2, EpaisseurPremierPlan = 2.4, LongueurTiretReset = 8, OpaciteTrou = 0.35, NbBandes = 12,
        };
        Rendre(piste, 840, 190);

        var paliers = Lignes(piste, "palier ");
        Assert.NotEmpty(paliers);
        Assert.All(paliers, l => Assert.Contains(" premierplan ", l));
        Assert.All(Lignes(piste, "bande "), b => Assert.EndsWith(" 2.400", b));   // épaisseur = EpaisseurPremierPlan

        // Le plateau épuisé (mer. 20:00 → jeu. 00:00 local, 18:00Z → 22:00Z) est GRIS, jamais coloré par la rampe.
        var (f18, f22) = (Fr(Utc(23, 18), j.Plage), Fr(Utc(23, 22), j.Plage));
        var gris = paliers.Where(l => l.EndsWith(" gris", StringComparison.Ordinal)).Select(Nombres).ToList();
        Assert.NotEmpty(gris);
        Assert.True(gris.Min(n => n[0]) <= f18 + 0.005);
        Assert.True(gris.Max(n => n[1]) >= f22 - 0.005);
        foreach (var n in paliers.Where(l => l.EndsWith(" rampe", StringComparison.Ordinal)).Select(Nombres))
            Assert.False(n[0] < f22 - 0.004 && n[1] > f18 + 0.004, "palier de rampe pendant le plateau épuisé");

        Assert.Equal(j.Analyse.Resets5h.Count(r => j.Plage.Contient(r.Instant)), Lignes(piste, "trait ").Count);
        Assert.NotEmpty(Lignes(piste, "hebdo "));
        Assert.Empty(Lignes(piste, "fantome "));
        Assert.Empty(Lignes(piste, "dents "));
        Assert.Empty(Lignes(piste, "tiret "));

        // Semaine « hebdo seul » (style Tuiles) : ni dents ni tirets, mais le fantôme et les bandes.
        var hebdoSeul = NiveauComplet(Semaine(), VariantePisteNiveau.SemaineHebdoSeul);
        Rendre(hebdoSeul, 840, 120);
        Assert.Empty(Lignes(hebdoSeul, "dents "));
        Assert.Empty(Lignes(hebdoSeul, "tiret "));
        Assert.NotEmpty(Lignes(hebdoSeul, "fantome "));
        Assert.NotEmpty(Lignes(hebdoSeul, "bande "));
    }

    [WpfFact]
    public void La_surcouche_trouve_le_releve_survole_et_formate_l_infobulle()
    {
        var d = Semaine();
        var s = SurcoucheComplete(d);
        Rendre(s, 840, 300);

        s.Survoler(420);
        Assert.True(s.InfobulleVisible);
        var attendu = Reticule.PlusProche(d.Analyse.Serie, EchelleTemps.Instant(420, d.Plage, 840))!;
        Assert.Equal(attendu.T, s.InstantSurvole);
        Assert.Equal(EchelleTemps.X(attendu.T, d.Plage, 840), s.XReticule, 0.5);
        Assert.Equal(TextesHistorique.Infobulle(attendu, d.LueA, Tz), s.TexteInfobulle);
        Assert.Equal(4, s.TexteInfobulle.Split('\n').Length);
        Rendre(s, 840, 300);
        Assert.NotEmpty(Lignes(s, "reticule "));

        s.Quitter();
        Assert.False(s.InfobulleVisible);
        Assert.Equal("", s.TexteInfobulle);
        Rendre(s, 840, 300);
        Assert.Empty(Lignes(s, "reticule "));

        s.AfficherMaintenant = true;
        s.Maintenant = d.LueA;
        Rendre(s, 840, 300);
        var maintenant = Assert.Single(Lignes(s, "maintenant "));
        Assert.Equal(Fr(d.LueA, d.Plage), Nombres(maintenant)[0], 3);
        s.AfficherMaintenant = false;
        Rendre(s, 840, 300);
        Assert.Empty(Lignes(s, "maintenant "));

        s.Survoler(-5);                               // hors plage : borné au début
        Assert.Equal(d.Analyse.Serie[0].T, s.InstantSurvole);
        Assert.Equal(d.Plage.Debut, s.InstantSurvole);

        s.Serie = null;
        s.Survoler(420);
        Assert.False(s.InfobulleVisible);
    }

    [WpfFact]
    public void Au_dela_du_seuil_la_piste_reduit_les_dents_par_min_max()
    {
        var d = Semaine();
        var plage = d.Plage;
        var n = Binning.SeuilReduction + 1;
        var pas = TimeSpan.FromTicks(plage.Duree.Ticks / (n + 1));
        var serie = Enumerable.Range(0, n)
            .Select(i => new ReleveJournal(plage.Debut + pas * (i + 1), SourceUsage.SondeEnTetes,
                (i % 7) / 7.0, plage.Fin, StatutServeur.Autorise, 0.3, plage.Fin, StatutServeur.Autorise, null, null))
            .ToList();
        var analyse = new AnalyseJournal(SourceUsage.SondeEnTetes, serie, Array.Empty<Trou>(), Array.Empty<ResetObserve>(), Array.Empty<ResetObserve>(),
            Array.Empty<DeltaConsommation>(), Array.Empty<DeltaConsommation>(), Array.Empty<SautNonLocalise>(), null, plage);

        var piste = NiveauComplet(d);
        piste.Analyse = analyse;
        Rendre(piste, 840, 150);
        var reduction = Assert.Single(Lignes(piste, "reduction "));
        Assert.True(Nombres(reduction)[0] <= 840);
        Assert.Empty(Lignes(piste, "dents "));

        // Sous le seuil : chaque palier est dessiné.
        piste.Analyse = d.Analyse;
        Rendre(piste, 840, 150);
        Assert.Empty(Lignes(piste, "reduction "));
        Assert.Single(Lignes(piste, "dents "));
    }

    // ------------------------------------------------------------------------------------------------
    // Task 2 — PisteRythme, PisteTokens, PisteCouverture, PisteFenetres5h
    // ------------------------------------------------------------------------------------------------

    [WpfFact]
    public void Le_rythme_colore_chaque_barre_au_niveau_atteint_et_borne_a_25_pct()
    {
        var d = Semaine();
        var piste = new PisteRythme
        {
            Plage = d.Plage, Deltas = d.Analyse.Deltas5h, Serie = d.Analyse.Serie, Rampe = ThemeCatalog.Default,
            Grille = B(Colors.DarkSlateGray), EpaisseurFine = 1,
        };
        Rendre(piste, 840, 72);

        var attendues = Binning.DeltasParHeure(d.Analyse.Deltas5h, d.Analyse.Serie, r => r.U5, d.Plage);
        var barres = Lignes(piste, "barre ");
        Assert.True(attendues.Count > 0);
        Assert.Equal(attendues.Count, barres.Count);
        for (var i = 0; i < attendues.Count; i++)
        {
            var n = Nombres(barres[i]);
            Assert.Equal(Fr(attendues[i].Debut, d.Plage), n[0], 3);
            Assert.Equal(Fr(attendues[i].Fin, d.Plage), n[1], 3);
            Assert.Equal(attendues[i].Delta, n[2], 3);
            Assert.Equal(attendues[i].NiveauAtteint, n[3], 3);
        }
        Assert.All(Lignes(piste, "hauteur "), l => Assert.True(Nombres(l)[0] <= 72 + 1e-9));

        // Un Δ de 0,40 dans une heure : tracé tel quel, mais dessiné borné à la hauteur de la piste (échelle 0 – 25 %).
        var t0 = d.Plage.Debut.AddHours(10);
        piste.Serie = new[] { new ReleveJournal(t0, SourceUsage.SondeEnTetes, 0.5, t0.AddHours(3), StatutServeur.Autorise, 0.2, d.Plage.Fin, StatutServeur.Autorise, null, null) };
        piste.Deltas = new[] { new DeltaConsommation(WindowKind.FiveHour, t0.AddMinutes(-5), t0, 0.40, t0.AddHours(3), false) };
        Rendre(piste, 840, 72);
        Assert.Equal(0.40, Nombres(Assert.Single(Lignes(piste, "barre ")))[2], 3);
        Assert.Equal(72.0, Nombres(Assert.Single(Lignes(piste, "hauteur ")))[0], 3);

        piste.Rampe = null;
        Rendre(piste, 840, 72);
        Assert.Empty(Lignes(piste, "barre "));
    }

    [WpfFact]
    public void Les_tokens_empilent_principal_et_sous_agents_et_hachurent_le_hors_couverture()
    {
        var d = Semaine();
        var plafond = EchelleValeur.MaxArrondi(d.Barres.Max(b => b.Principal.Out + b.SousAgents.Out));
        var piste = new PisteTokens
        {
            Plage = d.Plage, Barres = d.Barres, Plafond = plafond, InstantLecture = d.LueA,
            Principal = B(Colors.Gray), SousAgents = B(Colors.DimGray), Hachure = B(Colors.White),
        };
        Rendre(piste, 840, 72);

        var actives = d.Barres.Where(b => b.DebutUtc < d.LueA && b.Principal.Out + b.SousAgents.Out > 0).ToList();
        var tokens = Lignes(piste, "tokens ");
        Assert.True(actives.Count > 0);
        Assert.Equal(actives.Count, tokens.Count);
        Assert.Empty(Lignes(piste, "hachure "));   // tout ce qui précède la lecture est couvert ; les heures à venir sont VIDES, pas hachurées

        // Empilement : principal du bas jusqu'à Y(principal) ; sous-agents au-dessus, jusqu'à Y(principal + sous-agents).
        var i = actives.FindIndex(b => b.SousAgents.Out > 0);
        Assert.True(i >= 0);
        var b = actives[i];
        var empile = Nombres(Lignes(piste, "empile ")[i]);
        Assert.Equal(EchelleValeur.Y(b.Principal.Out + b.SousAgents.Out, plafond, 72), empile[0], 3);
        Assert.Equal(EchelleValeur.Y(b.Principal.Out, plafond, 72), empile[1], 3);
        Assert.Equal(b.Principal.Out, Nombres(tokens[i])[2]);
        Assert.Equal(b.SousAgents.Out, Nombres(tokens[i])[3]);

        // Barres construites : transcripts absents / hors couverture → hachure, jamais un zéro muet ; couverte et vide → rien.
        var debut = d.Plage.Debut;
        piste.Barres = new[]
        {
            new BarreHeure(debut, "00:00", TotauxTokens.Vide, TotauxTokens.Vide, EtatCouverture.TranscriptsAbsents),
            new BarreHeure(debut.AddHours(1), "01:00", TotauxTokens.Vide, TotauxTokens.Vide, EtatCouverture.HorsCouverture),
            new BarreHeure(debut.AddHours(2), "02:00", TotauxTokens.Vide, TotauxTokens.Vide, EtatCouverture.Couverte),
            new BarreHeure(debut.AddHours(3), "03:00", new TotauxTokens(1, 500, 0, 0, 1), TotauxTokens.Vide, EtatCouverture.Couverte),
        };
        piste.InstantLecture = null;
        Rendre(piste, 840, 72);
        Assert.Equal(2, Lignes(piste, "hachure ").Count);
        var seule = Assert.Single(Lignes(piste, "tokens "));
        Assert.Equal(Fr(debut.AddHours(3), d.Plage), Nombres(seule)[0], 3);
        Assert.Equal(500, Nombres(seule)[2]);
        Assert.Equal(EchelleValeur.Y(500, plafond, 72), Nombres(Assert.Single(Lignes(piste, "empile ")))[1], 3);
    }

    [WpfFact]
    public void Les_colonnes_du_jour_empilent_par_modele_dans_l_ordre()
    {
        var j = Jour(24);
        var plafond = EchelleValeur.MaxArrondi(j.Colonnes.Max(c => c.ParModele.Sum(p => p.Totaux.Out)));
        var piste = new PisteTokens
        {
            Plage = j.Plage, Colonnes = j.Colonnes, Plafond = plafond, InstantLecture = j.LueA,
            Modele1 = B(Colors.White), Modele2 = B(Colors.Gray), Modele3 = B(Colors.DimGray), Hachure = B(Colors.Black),
        };
        Rendre(piste, 840, 64);

        var trois = j.Colonnes.First(c => c.ParModele.Count == 3 && c.Slot < j.LueA);
        var f0 = Fr(trois.Slot, j.Plage);
        var lignes = Lignes(piste, "modele ").Where(l => Math.Abs(Nombres(l)[0] - f0) < 0.0005).ToList();
        Assert.Equal(3, lignes.Count);
        for (var rang = 0; rang < 3; rang++)
        {
            Assert.Contains($"rang={rang} ", lignes[rang]);
            Assert.Contains($"brosse=Modele{rang + 1} ", lignes[rang]);
            Assert.Equal(trois.ParModele[rang].Totaux.Out, Nombres(lignes[rang])[2]);
        }
        Assert.Empty(Lignes(piste, "hachure "));

        // Quatre modèles construits : le rang 3 prend Modele3 ; Colonnes gagne sur Barres ; rien sans les deux.
        var slot = j.Plage.Debut.AddHours(6);
        piste.Colonnes = new[] { new ColonneQuartDHeure(slot, "08:00", new[] { Part("a", 400), Part("b", 300), Part("c", 200), Part("d", 100) }, EtatCouverture.Couverte) };
        piste.Barres = new[] { new BarreHeure(slot, "08:00", new TotauxTokens(0, 900, 0, 0, 1), TotauxTokens.Vide, EtatCouverture.Couverte) };
        piste.InstantLecture = null;
        Rendre(piste, 840, 64);
        var modeles = Lignes(piste, "modele ");
        Assert.Equal(4, modeles.Count);
        Assert.Contains("rang=3 brosse=Modele3 ", modeles[3]);
        Assert.Empty(Lignes(piste, "tokens "));

        piste.Colonnes = null;
        piste.Barres = null;
        Rendre(piste, 840, 64);
        Assert.Empty(piste.TraceRendu);
    }

    private static PartModele Part(string modele, long sortie) => new(modele, new TotauxTokens(0, sortie, 0, 0, 1));

    [WpfFact]
    public void La_couverture_peint_present_arrete_et_jeton()
    {
        var d = Semaine();
        var piste = new PisteCouverture
        {
            Plage = d.Plage, Serie = d.Analyse.Serie, Trous = d.Analyse.Trous, InstantLecture = d.LueA,
            Present = B(Colors.Green), OpacitePresent = 0.55, Arrete = B(Colors.Gray), Jeton = B(Colors.Orange),
        };
        Rendre(piste, 840, 12);

        var present = Nombres(Assert.Single(Lignes(piste, "present ")));
        Assert.Equal(Fr(d.Analyse.Serie[0].T, d.Plage), present[0], 3);
        Assert.Equal(Fr(d.Analyse.Serie[^1].T, d.Plage), present[1], 3);
        Assert.Equal(0.55, present[2], 3);
        var trous = Lignes(piste, "trou ");
        Assert.Equal(2, trous.Count);
        Assert.EndsWith(" arrete", trous[0]);
        Assert.EndsWith(" jeton", trous[1]);

        // Trous construits : sonde refusée → jeton (ambre) ; inconnue → arrêté (gris) ; ouvert → finit à l'instant de lecture.
        var t = d.Plage.Debut;
        piste.Trous = new[]
        {
            new Trou(t.AddHours(1), t.AddHours(2), CauseTrou.SondeRefusee),
            new Trou(t.AddHours(3), t.AddHours(4), CauseTrou.Inconnue),
            new Trou(t.AddHours(5), null, CauseTrou.ChronosArrete),
        };
        piste.InstantLecture = t.AddHours(9);
        Rendre(piste, 840, 12);
        trous = Lignes(piste, "trou ");
        Assert.Equal(3, trous.Count);
        Assert.EndsWith(" jeton", trous[0]);
        Assert.EndsWith(" arrete", trous[1]);
        Assert.EndsWith(" arrete", trous[2]);
        Assert.Equal(Fr(t.AddHours(9), d.Plage), Nombres(trous[2])[1], 3);
    }

    [WpfFact]
    public void Les_tuiles_finissent_au_reset_et_grisent_l_epuisee()
    {
        var d = Semaine();
        var piste = new PisteFenetres5h
        {
            Plage = d.Plage, Serie = d.Analyse.Serie, Rampe = ThemeCatalog.Default, Gris = B(Colors.Gray), TraitReset = B(Colors.White), EpaisseurFine = 1,
        };
        Rendre(piste, 840, 62);

        var attendues = Tuiles5h.Depuis(d.Analyse.Serie, d.Plage);
        var tuiles = Lignes(piste, "tuile ");
        Assert.True(attendues.Count > 1);
        Assert.Equal(attendues.Count, tuiles.Count);
        for (var i = 0; i < attendues.Count; i++)
        {
            var n = Nombres(tuiles[i]);
            Assert.Equal(Fr(attendues[i].Debut, d.Plage), n[0], 3);
            Assert.Equal(Fr(attendues[i].Fin, d.Plage), n[1], 3);
            Assert.Equal(attendues[i].UMax, n[2], 3);
            Assert.Equal(62 - EchelleValeur.Y(attendues[i].UMax, 1, 62), n[3], 3);
            Assert.Contains(attendues[i].Epuisee ? " grise " : " rampe ", tuiles[i]);
        }

        // La fenêtre épuisée mer. 19:00 → jeu. 00:00 local (fin 22:00Z) est grise, umax 1,00 ; toutes les autres sont de la rampe.
        var grise = Assert.Single(tuiles.Where(l => l.Contains(" grise ")));
        Assert.Equal(Fr(Utc(23, 22), d.Plage), Nombres(grise)[1], 3);
        Assert.Equal(1.0, Nombres(grise)[2], 3);

        var tirets = Lignes(piste, "tiret ");
        Assert.Equal(attendues.Count(t => t.ResetDansPlage), tirets.Count);
        foreach (var tiret in tirets)
            Assert.Contains(attendues, t => t.ResetDansPlage && Math.Abs(Fr(t.Fin, d.Plage) - Nombres(tiret)[0]) <= 0.001);

        piste.Gris = null;                              // sans gris, la tuile épuisée est SAUTÉE, pas peinte par la rampe
        Rendre(piste, 840, 62);
        tuiles = Lignes(piste, "tuile ");
        Assert.Equal(attendues.Count - 1, tuiles.Count);
        Assert.DoesNotContain(tuiles, l => l.Contains(" grise "));
    }

    [WpfFact]
    public void Toutes_les_pistes_survivent_a_une_taille_nulle_et_a_des_listes_vides()
    {
        var d = Semaine();
        var vide = AnalyseReleves.Analyser(new LectureJournal(Array.Empty<ReleveJournal>(), Array.Empty<EvenementJournal>(), 0, null, d.Plage), d.LueA, RateLimitHeaderUsageProvider.CadenceNominale);

        foreach (var (w, h) in new[] { (0.0, 0.0), (1.0, 1.0), (840.0, 0.0), (840.0, 150.0) })
        {
            var niveau = NiveauComplet(d);
            niveau.Analyse = vide;
            niveau.Precedente = vide;
            var surcouche = SurcoucheComplete(d);
            surcouche.Serie = vide.Serie;
            var pistes = new PisteBase[]
            {
                niveau,
                new PisteRythme { Plage = d.Plage, Deltas = vide.Deltas5h, Serie = vide.Serie, Rampe = ThemeCatalog.Default, Grille = B(Colors.Gray), EpaisseurFine = 1 },
                new PisteTokens { Plage = d.Plage, Barres = Array.Empty<BarreHeure>(), Plafond = 0, Principal = B(Colors.Gray), SousAgents = B(Colors.Gray), Hachure = B(Colors.Gray) },
                new PisteCouverture { Plage = d.Plage, Serie = vide.Serie, Trous = vide.Trous, InstantLecture = d.LueA, Present = B(Colors.Gray), OpacitePresent = 0.55, Arrete = B(Colors.Gray), Jeton = B(Colors.Gray) },
                new PisteFenetres5h { Plage = d.Plage, Serie = vide.Serie, Rampe = ThemeCatalog.Default, Gris = B(Colors.Gray), TraitReset = B(Colors.Gray), EpaisseurFine = 1 },
                surcouche,
            };
            foreach (var p in pistes)
            {
                Rendre(p, w, h);
                Assert.Equal(1, p.RendusPourTests);
                Assert.DoesNotContain(p.TraceRendu, l => l.Contains("NaN"));
            }
        }

        // Les vraies données à taille nulle puis à hauteur nulle : rien ne lève, rien n'est NaN.
        var complet = NiveauComplet(d);
        Rendre(complet, 0, 0);
        Rendre(complet, 840, 0);
        Rendre(complet, 840, 150);
        Assert.DoesNotContain(complet.TraceRendu, l => l.Contains("NaN"));
        Assert.Equal(3, complet.RendusPourTests);
    }
}
