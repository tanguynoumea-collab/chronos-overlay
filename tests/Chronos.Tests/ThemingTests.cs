using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using Chronos.Models;
using Chronos.Services;
using Chronos.Theming;
using Chronos.ViewModels;
using Chronos.Views;
using Xunit;
using WindowState = Chronos.Models.WindowState;

namespace Chronos.Tests;

/// <summary>
/// Prouve le moteur de thèmes : intégrité du catalogue, rampe à stops personnalisés, couleur de l'arc
/// (neutre/épuisé/rampe) selon le thème, réactivité de <see cref="WindowGaugeViewModel.ValueBrush"/>,
/// persistance de la clé de thème, et smoke test XAML de la fenêtre de réglages.
/// </summary>
[Collection("XAML WPF")]   // charge du BAML : serialise avec les autres classes XAML (voir XamlWpfCollection)
public class ThemingTests
{
    private static string TempDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chronos-thm-" + Guid.NewGuid().ToString("N"));
    private static ChronosPaths TempPaths() { var d = TempDir(); System.IO.Directory.CreateDirectory(d); return new(System.IO.Path.Combine(d, "usage.json"), System.IO.Path.Combine(d, "projects")); }

    // --- Helpers de test (copies indépendantes de la production, pour ne pas prouver le code par lui-même) ---

    /// <summary>Parse « #RRGGBB » ou « #AARRGGBB ».</summary>
    private static Color H(string hex)
    {
        var s = hex.TrimStart('#');
        byte a = 0xFF;
        if (s.Length == 8) { a = Convert.ToByte(s[..2], 16); s = s[2..]; }
        return Color.FromArgb(a, Convert.ToByte(s[..2], 16), Convert.ToByte(s.Substring(2, 2), 16), Convert.ToByte(s.Substring(4, 2), 16));
    }

    /// <summary>Disque OPAQUE du thème (FondCadran est translucide E6 : le contraste se mesure sur le disque plein).</summary>
    private static Color Disque(ChronosTheme t) => Color.FromRgb(t.FondCadran.R, t.FondCadran.G, t.FondCadran.B);

    /// <summary>Copie exacte du Lerp de ChronosTheme : Math.Round par canal (arrondi bancaire), alpha FF.</summary>
    private static Color LerpTest(Color a, Color b, double t) => Color.FromArgb(0xFF,
        (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

    /// <summary>Gris pur de même luminance (copie indépendante, pour ne pas prouver le code par lui-même) : le plus petit
    /// v (0..255) dont la luminance WCAG atteint celle de <paramref name="c"/> ; 255 si aucun.</summary>
    private static Color GrisTest(Color c)
    {
        var cible = ContrasteWcag.Luminance(c);
        for (int v = 0; v <= 255; v++)
        {
            var g = Color.FromRgb((byte)v, (byte)v, (byte)v);
            if (ContrasteWcag.Luminance(g) >= cible) return g;
        }
        return Color.FromRgb(255, 255, 255);
    }

    /// <summary>Composition sRGB par canal d'une couleur translucide sur un fond opaque (copie indépendante) ; alpha FF.</summary>
    private static Color ComposeTest(Color avant, Color fond)
    {
        double a = avant.A / 255.0;
        byte Canal(byte x, byte y) => (byte)Math.Round(a * x + (1 - a) * y);
        return Color.FromRgb(Canal(avant.R, fond.R), Canal(avant.G, fond.G), Canal(avant.B, fond.B));
    }

    /// <summary>Neutre attendu (§11 B3) : mi-chemin disque → piste.</summary>
    private static Color NeutreAttendu(ChronosTheme t) => LerpTest(Disque(t), t.Piste5h, 0.5);

    /// <summary>Les deux contraintes de l'épuisé (§11 B3) : ≥ 3:1 contre le disque opaque ET ≥ 2:1 contre le neutre.</summary>
    private static bool EpuiseSuffit(Color g, ChronosTheme t) =>
        ContrasteWcag.Ratio(g, Disque(t)) >= 3.0 && ContrasteWcag.Ratio(g, NeutreAttendu(t)) >= 2.0;

    /// <summary>Teinte HSV standard en degrés [0, 360) ; 0 pour un gris.</summary>
    private static double TeinteHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
        if (delta == 0) return 0;
        double h;
        if (max == r) h = 60 * (((g - b) / delta) % 6);
        else if (max == g) h = 60 * (((b - r) / delta) + 2);
        else h = 60 * (((r - g) / delta) + 4);
        return h < 0 ? h + 360 : h;
    }

    /// <summary>Catalogue cible §5.2 : clé → disque, piste, graduation, texte, vert, ambre, rouge (valeurs EXACTES).</summary>
    private static readonly (string Cle, string[] Hex)[] Palettes =
    {
        ("minuit",    new[] { "#16151B", "#2A2932", "#C9C8D2", "#F4F2EC", "#7BB13C", "#EFA23A", "#D8503A" }),
        ("ardoise",   new[] { "#1B2027", "#2C333D", "#CBD3DE", "#EEF2F6", "#5FB39A", "#E0A94E", "#E06B5A" }),
        ("ambre",     new[] { "#1E1712", "#33271C", "#EAD9B8", "#F6ECD9", "#E4B24A", "#E07E3C", "#D24A3A" }),
        ("graphite",  new[] { "#121314", "#26282B", "#C4C7CC", "#F2F3F5", "#6DBE45", "#F0A830", "#E04B3C" }),
        ("marine",    new[] { "#0F1A2A", "#1E2D44", "#B9C9DE", "#EAF0F7", "#5DBB7A", "#F2B544", "#E25C4F" }),
        ("nord",      new[] { "#2E3440", "#3B4252", "#D8DEE9", "#ECEFF4", "#A3BE8C", "#EBCB8B", "#BF616A" }),
        ("foret",     new[] { "#2D353B", "#3A454A", "#A6B0A0", "#D3C6AA", "#A7C080", "#DBBC7F", "#E67E80" }),
        ("moka",      new[] { "#1E1E2E", "#313244", "#BAC2DE", "#CDD6F4", "#A6E3A1", "#FAB387", "#F38BA8" }),
        ("roseraie",  new[] { "#191724", "#26233A", "#B3AECC", "#E0DEF4", "#9CCFD8", "#F6C177", "#EB6F92" }),
        ("sauge",     new[] { "#262B28", "#343B37", "#AEB8B0", "#DCE3DD", "#8FB996", "#D9C27E", "#D08A7E" }),
        ("lavande",   new[] { "#22202C", "#302D3D", "#C3BCD9", "#E6E2F2", "#9FCFB0", "#E8C88E", "#E08E9E" }),
        ("neon",      new[] { "#0E0A1F", "#241B3A", "#7DF9FF", "#E6E1FF", "#38E8C6", "#FFC23D", "#FF2E63" }),
        ("aurore",    new[] { "#0E1726", "#1D2B44", "#BFE3FF", "#EAF2FF", "#4FD1C5", "#F0C36D", "#F2577A" }),
        ("synthwave", new[] { "#140B24", "#2A1745", "#9AE6FF", "#F5EEFF", "#2BFF88", "#FFD000", "#FF2D55" }),
        ("lave",      new[] { "#1A0E0A", "#33190F", "#FFC9A3", "#FFF1E6", "#7CFF4F", "#FFB000", "#FF3B1F" }),
    };

    /// <summary>THM-01/02 : 15 thèmes aux clés uniques, minuit par défaut, trois familles 6/5/4 avec la composition exacte du §5.1.</summary>
    [Fact]
    public void Catalogue_quinze_themes_trois_categories()
    {
        Assert.Equal(15, ThemeCatalog.All.Count);
        Assert.Equal(15, ThemeCatalog.All.Select(t => t.Key).Distinct().Count());
        Assert.Equal("minuit", ThemeCatalog.Default.Key);

        string[] Cles(CategorieTheme c) => ThemeCatalog.All.Where(t => t.Categorie == c).Select(t => t.Key).OrderBy(k => k).ToArray();
        Assert.Equal(new[] { "foret", "lavande", "moka", "nord", "roseraie", "sauge" }, Cles(CategorieTheme.Pale));
        Assert.Equal(new[] { "ambre", "ardoise", "graphite", "marine", "minuit" }, Cles(CategorieTheme.Classique));
        Assert.Equal(new[] { "aurore", "lave", "neon", "synthwave" }, Cles(CategorieTheme.Vive));
    }

    /// <summary>Un thème déjà choisi reste choisi : les neuf clés historiques sont toujours résolues telles quelles.</summary>
    [Fact]
    public void Les_neuf_cles_existantes_restent_resolues()
    {
        foreach (var cle in new[] { "minuit", "ardoise", "nord", "neon", "aurore", "ambre", "moka", "roseraie", "foret" })
            Assert.Equal(cle, ThemeCatalog.ByKey(cle).Key);
    }

    /// <summary>THM-02 : chaque thème a EXACTEMENT ses sept couleurs (dont Néon/Aurore à rampe corrigée).</summary>
    [Fact]
    public void Chaque_theme_a_exactement_sa_palette()
    {
        Assert.Equal(ThemeCatalog.All.Count, Palettes.Length);
        foreach (var (cle, hex) in Palettes)
        {
            var t = ThemeCatalog.ByKey(cle);
            Assert.Equal(cle, t.Key);
            Assert.True(H(hex[0]) == Disque(t), $"{cle} : disque");
            Assert.True(H(hex[1]) == t.Piste5h, $"{cle} : piste");
            Assert.True(H(hex[2]) == t.TickVisible, $"{cle} : graduation");
            Assert.True(H(hex[3]) == t.TextePrincipal, $"{cle} : texte");
            Assert.True(H(hex[4]) == t.RampGreen, $"{cle} : vert");
            Assert.True(H(hex[5]) == t.RampAmber, $"{cle} : ambre");
            Assert.True(H(hex[6]) == t.RampRed, $"{cle} : rouge");
        }
    }

    /// <summary>Valeurs de référence WCAG 2.x : blanc/noir = 21:1, #777777 sur blanc ≈ 4,48:1, rapport symétrique.</summary>
    [Fact]
    public void Contraste_wcag_de_reference()
    {
        Assert.InRange(ContrasteWcag.Ratio(Colors.White, Colors.Black), 20.999, 21.001);
        Assert.InRange(ContrasteWcag.Ratio(H("#777777"), H("#FFFFFF")), 4.47, 4.49);
        Color a = H("#D8503A"), b = H("#16151B");
        Assert.Equal(ContrasteWcag.Ratio(a, b), ContrasteWcag.Ratio(b, a));
    }

    /// <summary>THM-03 : le gris « épuisé » se lit (≥ 3:1) contre le disque opaque, sur tout le catalogue. Comparaison
    /// SANS arrondi préalable (Lave vaut 3,0027).</summary>
    [Fact]
    public void Le_gris_epuise_est_lisible_sur_tout_le_catalogue()
    {
        foreach (var t in ThemeCatalog.All)
        {
            var ratio = ContrasteWcag.Ratio(t.Epuise, Disque(t));
            Assert.True(ratio >= 3.0, $"thème {t.Key} : épuisé {t.Epuise} à {ratio:F4}:1 contre le disque (< 3:1)");
        }
    }

    /// <summary>THM-03 (B3, DESIGN_PLAN_CYCLE2 §11) : l'épuisé est le gris de même luminance du PLUS PETIT mélange
    /// piste → graduation (pas 0,18 à 1,00) qui satisfait les deux contraintes (≥ 3:1 disque, ≥ 2:1 neutre).</summary>
    [Fact]
    public void Le_gris_epuise_est_le_plus_petit_melange_qui_suffit()
    {
        foreach (var t in ThemeCatalog.All)
        {
            Color Gris(int pas) => GrisTest(LerpTest(t.Piste5h, t.TickVisible, pas / 100.0));
            int p = Enumerable.Range(18, 83).FirstOrDefault(i => Gris(i) == t.Epuise && EpuiseSuffit(Gris(i), t), -1);
            Assert.True(p >= 18, $"thème {t.Key} : épuisé {t.Epuise} n'est le gris d'aucun mélange piste → graduation qui suffit");
            for (int q = 18; q < p; q++)
                Assert.False(EpuiseSuffit(Gris(q), t),
                    $"thème {t.Key} : le pas {q} suffisait déjà ({Gris(q)}), l'épuisé n'est pas minimal");
        }
    }

    /// <summary>B3, DESIGN_PLAN_CYCLE2 §11 : le neutre (utilisation inconnue) vaut Lerp(disque, piste, 0,5) et est
    /// strictement plus sombre que la piste — « rien » se lit comme du vide.</summary>
    [Fact]
    public void Le_neutre_est_plus_sombre_que_la_piste()
    {
        foreach (var t in ThemeCatalog.All)
        {
            Assert.True(t.Neutre == NeutreAttendu(t), $"thème {t.Key} : neutre {t.Neutre} ≠ {NeutreAttendu(t)} (Lerp disque → piste 0,5)");
            Assert.True(ContrasteWcag.Luminance(t.Neutre) < ContrasteWcag.Luminance(t.Piste5h),
                $"thème {t.Key} : neutre {t.Neutre} n'est pas plus sombre que la piste {t.Piste5h}");
        }
    }

    /// <summary>B3, DESIGN_PLAN_CYCLE2 §11 : l'épuisé est un gris PUR (R = G = B) — il ne passe plus pour une teinte de la
    /// rampe (plus de bleu sur Synthwave / Marine / Néon).</summary>
    [Fact]
    public void Le_gris_epuise_est_desature()
    {
        foreach (var t in ThemeCatalog.All)
        {
            Assert.True(t.Epuise.R == t.Epuise.G && t.Epuise.G == t.Epuise.B, $"thème {t.Key} : épuisé {t.Epuise} n'est pas un gris pur");
            Assert.True(GrisTest(t.Epuise) == t.Epuise, $"thème {t.Key} : épuisé {t.Epuise} n'est pas son propre gris de même luminance");
        }
    }

    /// <summary>B3, DESIGN_PLAN_CYCLE2 §11 : « épuisé » ne se confond plus avec « aucune donnée » (≥ 2:1 contre le neutre).</summary>
    [Fact]
    public void Le_gris_epuise_se_distingue_du_neutre()
    {
        foreach (var t in ThemeCatalog.All)
        {
            var ratio = ContrasteWcag.Ratio(t.Epuise, t.Neutre);
            Assert.True(ratio >= 2.0, $"thème {t.Key} : épuisé / neutre à {ratio:F4}:1 (< 2:1)");
        }
    }

    /// <summary>B3, DESIGN_PLAN_CYCLE2 §11 : les chiffres de la plaque Volets restent lisibles (≥ 4,5:1) sur un quota épuisé.
    /// Règle : le meilleur de PlaqueTexte / TextePrincipal s'il atteint 4,5:1, sinon le meilleur de noir / blanc.</summary>
    [Fact]
    public void Le_texte_de_plaque_reste_lisible_sur_epuise()
    {
        foreach (var t in ThemeCatalog.All)
        {
            var ratio = ContrasteWcag.Ratio(t.PlaqueTexteEpuise, t.Epuise);
            Assert.True(ratio >= 4.5, $"thème {t.Key} : texte de plaque sur épuisé à {ratio:F4}:1 (< 4,5:1)");

            double rp = ContrasteWcag.Ratio(t.PlaqueTexte, t.Epuise), rt = ContrasteWcag.Ratio(t.TextePrincipal, t.Epuise);
            Color attendu = Math.Max(rp, rt) >= 4.5
                ? (rp >= rt ? t.PlaqueTexte : t.TextePrincipal)
                : (ContrasteWcag.Ratio(Colors.Black, t.Epuise) >= ContrasteWcag.Ratio(Colors.White, t.Epuise) ? Colors.Black : Colors.White);
            Assert.True(attendu == t.PlaqueTexteEpuise, $"thème {t.Key} : PlaqueTexteEpuise {t.PlaqueTexteEpuise} ≠ {attendu} attendu");
        }
    }

    /// <summary>B2, DESIGN_PLAN_CYCLE2 §11 : la plaque « indisponible » (FondCadran, alpha E6) garantit ≥ 4,5:1 au texte
    /// principal, que le bureau dessous soit noir ou blanc.</summary>
    [Fact]
    public void La_plaque_indisponible_garantit_le_texte_principal()
    {
        foreach (var t in ThemeCatalog.All)
        {
            var surNoir = ContrasteWcag.Ratio(t.TextePrincipal, ComposeTest(t.FondCadran, Colors.Black));
            var surBlanc = ContrasteWcag.Ratio(t.TextePrincipal, ComposeTest(t.FondCadran, Colors.White));
            Assert.True(surNoir >= 4.5, $"thème {t.Key} : texte sur plaque (bureau noir) à {surNoir:F4}:1 (< 4,5:1)");
            Assert.True(surBlanc >= 4.5, $"thème {t.Key} : texte sur plaque (bureau blanc) à {surBlanc:F4}:1 (< 4,5:1)");
        }
    }

    /// <summary>Ancrages de l'épuisé : minuit, Lave, Forêt. Gris purs (B3, §11) ; valeurs exactes épinglées en tâche 2.</summary>
    [Fact]
    public void Ancrage_minuit()
    {
        foreach (var cle in new[] { "minuit", "lave", "foret" })
        {
            var e = ThemeCatalog.ByKey(cle).Epuise;
            Assert.True(e.R == e.G && e.G == e.B, $"{cle} : épuisé {e} n'est pas un gris pur");   // épinglé en tâche 2
        }
    }

    /// <summary>THM-03 : le rouge de fin de rampe est bien ROUGE (teinte 335°–20°) et lisible (≥ 3:1) sur tout le catalogue.</summary>
    [Fact]
    public void Le_rouge_de_rampe_est_rouge_et_lisible_sur_tout_le_catalogue()
    {
        foreach (var t in ThemeCatalog.All)
        {
            var teinte = TeinteHsv(t.RampRed);
            Assert.True(teinte >= 335 || teinte <= 20, $"thème {t.Key} : rouge {t.RampRed} en teinte {teinte:F1}° (hors 335°–20°)");
            var ratio = ContrasteWcag.Ratio(t.RampRed, Disque(t));
            Assert.True(ratio >= 3.0, $"thème {t.Key} : rouge à {ratio:F4}:1 contre le disque (< 3:1)");
        }
    }

    /// <summary>THM-04 (fondations) : les pinceaux consommés par les cadrans et l'Historique existent dans les 15 thèmes,
    /// gelés, aux dérivations attendues.</summary>
    [Fact]
    public void Les_nouveaux_tokens_existent_dans_tous_les_themes()
    {
        foreach (var t in ThemeCatalog.All)
        {
            var tokens = t.BrushTokens();
            foreach (var cle in new[] { "TickReset", "Epuise", "CadranTuile", "CadranAttente", "PlaqueTexte", "PlaqueTexteEpuise", "PlaqueFilet", "PlaqueHachure" })
                Assert.True(tokens.ContainsKey(cle), $"thème {t.Key} : token {cle} manquant");

            Color Couleur(string cle)
            {
                var b = Assert.IsType<SolidColorBrush>(tokens[cle]);
                Assert.True(b.IsFrozen, $"thème {t.Key} : {cle} non gelé");
                return b.Color;
            }

            var d = Disque(t);
            var plaque = Color.FromRgb((byte)(d.R * 0.5), (byte)(d.G * 0.5), (byte)(d.B * 0.5));
            Assert.Equal(t.TextePrincipal, Couleur("TickReset"));
            Assert.Equal(t.Epuise, Couleur("Epuise"));
            Assert.Equal(LerpTest(d, t.Piste5h, 0.5), Couleur("CadranTuile"));
            Assert.Equal(Color.FromArgb(0x6E, t.TickVisible.R, t.TickVisible.G, t.TickVisible.B), Couleur("CadranAttente"));
            Assert.Equal(plaque, Couleur("PlaqueTexte"));
            Assert.Equal(t.PlaqueTexteEpuise, Couleur("PlaqueTexteEpuise"));   // B3, §11
            Assert.Equal(Color.FromArgb(0x33, plaque.R, plaque.G, plaque.B), Couleur("PlaqueFilet"));
            var hachure = Assert.IsType<DrawingBrush>(tokens["PlaqueHachure"]);
            Assert.True(hachure.IsFrozen, $"thème {t.Key} : PlaqueHachure non gelé");
        }
    }

    /// <summary>Ancrage des nouveaux tokens pour minuit (valeurs du tableau « Tokens à ajouter » de 39-RESEARCH).</summary>
    [Fact]
    public void Ancrage_tokens_minuit()
    {
        var tokens = ThemeCatalog.Default.BrushTokens();
        Color C(string cle) => ((SolidColorBrush)tokens[cle]).Color;
        Assert.Equal(H("#FFF4F2EC"), C("TickReset"));
        Assert.Equal(H("#FF201F26"), C("CadranTuile"));
        Assert.Equal(H("#6EC9C8D2"), C("CadranAttente"));
        Assert.Equal(H("#FF0B0A0D"), C("PlaqueTexte"));
        Assert.Equal(H("#330B0A0D"), C("PlaqueFilet"));
    }

    /// <summary>Les replis statiques de DesignTokens.xaml valent exactement les tokens de minuit (thème par défaut) :
    /// une vue chargée avant l'application du thème ne flashe pas une autre couleur.</summary>
    [WpfFact]
    public void Les_replis_statiques_valent_minuit()
    {
        var dict = (ResourceDictionary)Application.LoadComponent(
            new Uri("/Chronos;component/Resources/DesignTokens.xaml", UriKind.Relative));
        var tokens = ThemeCatalog.Default.BrushTokens();
        foreach (var cle in new[] { "TickReset", "Epuise", "CadranTuile", "CadranAttente", "PlaqueTexte", "PlaqueTexteEpuise", "PlaqueFilet" })
        {
            var repli = Assert.IsType<SolidColorBrush>(dict[cle]);
            Assert.True(((SolidColorBrush)tokens[cle]).Color == repli.Color, $"repli {cle} : {repli.Color} ≠ minuit");
        }
        Assert.IsType<DrawingBrush>(dict["PlaqueHachure"]);
    }

    [Fact]
    public void ByKey_inconnu_retombe_sur_minuit_insensible_a_la_casse()
    {
        Assert.Equal("minuit", ThemeCatalog.ByKey("n'existe pas").Key);
        Assert.Equal("nord", ThemeCatalog.ByKey("NORD").Key);
    }

    [Fact]
    public void Disque_est_translucide_alpha_E6()
    {
        Assert.Equal(0xE6, ThemeCatalog.ByKey("minuit").FondCadran.A);
    }

    /// <summary>TOK-02 : le token « Alerte » de la pastille de déconnexion suit tous les thèmes. Un thème
    /// où il manquerait rendrait la pastille invisible (pinceau non résolu) précisément chez les
    /// utilisateurs qui n'ont pas gardé « minuit » — panne silencieuse d'un signal anti-silence.
    /// AMBRE et non rouge : le rouge signifie déjà « quota épuisé » dans la rampe d'usage.</summary>
    [Fact]
    public void Le_token_Alerte_existe_dans_tous_les_themes_et_vaut_l_ambre_du_theme()
    {
        foreach (var t in ThemeCatalog.All)
        {
            Assert.NotEqual(t.RampAmber, t.RampRed);   // ambre ≠ rouge dans la rampe elle-même
            var tokens = t.BrushTokens();
            Assert.True(tokens.ContainsKey("Alerte"), $"thème {t.Key} : token Alerte manquant");
            Assert.Equal(t.RampAmber, ((SolidColorBrush)tokens["Alerte"]).Color);
            Assert.NotEqual(t.RampRed, ((SolidColorBrush)tokens["Alerte"]).Color);
        }
    }

    [Fact]
    public void ArcColor_gere_neutre_epuise_et_rampe()
    {
        var t = ThemeCatalog.ByKey("nord");
        Assert.Equal(t.Neutre, t.ArcColor(null));       // utilization inconnue → neutre
        Assert.Equal(t.Epuise, t.ArcColor(1.0));         // ≥ 100 % → épuisé
        Assert.Equal(t.RampGreen, t.ArcColor(0.0));      // 0 % → vert de la rampe du thème
        Assert.Equal(t.RampRed, t.ArcColor(0.999999));   // proche de 100 % → ~rouge
    }

    [Fact]
    public void Rampe_a_stops_personnalises_respecte_les_bornes()
    {
        Color g = Color.FromRgb(1, 2, 3), a = Color.FromRgb(10, 20, 30), r = Color.FromRgb(200, 100, 50);
        Assert.Equal(g, Chronos.Rendering.RampColor.Interpolate(0.0, g, a, r));
        Assert.Equal(a, Chronos.Rendering.RampColor.Interpolate(0.55, g, a, r));
        Assert.Equal(r, Chronos.Rendering.RampColor.Interpolate(1.0, g, a, r));
    }

    [Fact]
    public void ValueBrush_suit_le_theme_et_l_utilization()
    {
        var g = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        g.SetTheme(ThemeCatalog.ByKey("nord"));
        g.Apply(new WindowState { Kind = WindowKind.FiveHour, Utilization = 0.0, Reliability = SourceReliability.Exact });
        Assert.Equal(ThemeCatalog.ByKey("nord").RampGreen, ((SolidColorBrush)g.ValueBrush!).Color);

        // Changer de thème recalcule la couleur pour l'utilization courante.
        g.SetTheme(ThemeCatalog.ByKey("ambre"));
        Assert.Equal(ThemeCatalog.ByKey("ambre").RampGreen, ((SolidColorBrush)g.ValueBrush!).Color);
    }

    [Fact]
    public void ThemeKey_persiste_dans_settings()
    {
        var svc = new SettingsService(TempPaths());
        Assert.Equal("minuit", svc.Load().ThemeKey);           // défaut
        svc.Save(svc.Load() with { ThemeKey = "aurore" });
        Assert.Equal("aurore", svc.Load().ThemeKey);
    }

    // --- Smoke test XAML de la fenêtre de réglages v2 (STA) ---

    [WpfFact]
    public void ReglagesWindow_se_construit_et_se_met_en_page_sans_crash()
    {
        var settings = new SettingsService(TempPaths());
        var provider = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(provider, new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero));
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero));
        var vm = new MainViewModel(orch, new FakeUiDispatcher { OnUiThread = true }, clock,
            new FakeWindowController(), new FakeAutostartService(), settings,
            new DiagnosticService(TempPaths(), settings, provider, clock),
            new FakeOAuthLogin(), new FakeSessionsController(), new FakeAuthStatus());

        var win = new Chronos.Views.Reglages.ReglagesWindow(vm);
        win.Measure(new Size(1000, 1000));
        win.Arrange(new Rect(0, 0, 1000, 1000));

        Assert.NotNull(win.Content);
        Assert.Equal(ThemeCatalog.All.Count, vm.Themes.Count);   // tous les thèmes alimentent la grille
        Assert.Contains(vm.Themes, t => t.IsSelected);    // un thème est sélectionné
        Assert.Equal(3, vm.GroupesThemes.Count);          // section Thème en trois groupes (THM-01)
    }

    // --- Groupes de la section Thème (THM-01, §5.1) ---

    [WpfFact]
    public void Les_groupes_de_themes_suivent_les_categories_et_partagent_les_choix()
    {
        var settings = new SettingsService(TempPaths());
        settings.Save(settings.Load() with { ThemeKey = "nord" });   // thème persisté AVANT la construction du VM
        var provider = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(provider, new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero));
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero));
        var vm = new MainViewModel(orch, new FakeUiDispatcher { OnUiThread = true }, clock,
            new FakeWindowController(), new FakeAutostartService(), settings,
            new DiagnosticService(TempPaths(), settings, provider, clock),
            new FakeOAuthLogin(), new FakeSessionsController(), new FakeAuthStatus());

        // Trois groupes, titres en capitales dans l'ordre de l'enum.
        Assert.Equal(new[] { "PÂLE", "CLASSIQUE", "VIVE" }, vm.GroupesThemes.Select(g => g.Titre).ToArray());
        Assert.Equal(new[] { "Nord", "Forêt", "Moka", "Roseraie", "Sauge", "Lavande" },
            vm.GroupesThemes[0].Themes.Select(c => c.Name).ToArray());
        Assert.Equal(new[] { "Minuit", "Ardoise", "Ambre chaud", "Graphite", "Marine" },
            vm.GroupesThemes[1].Themes.Select(c => c.Name).ToArray());
        Assert.Equal(new[] { "Néon", "Aurore", "Synthwave", "Lave" },
            vm.GroupesThemes[2].Themes.Select(c => c.Name).ToArray());

        // Mêmes instances que Themes (sinon la surbrillance de SelectTheme ne s'afficherait plus).
        Assert.Equal(15, vm.Themes.Count);
        Assert.Equal(vm.Themes.Count, vm.GroupesThemes.Sum(g => g.Themes.Count));
        foreach (var choix in vm.GroupesThemes.SelectMany(g => g.Themes))
            Assert.Same(vm.Themes.Single(t => t.Theme.Key == choix.Theme.Key), choix);

        // Le persisté est en surbrillance, et lui seul.
        var nord = vm.GroupesThemes[0].Themes.Single(c => c.Name == "Nord");
        Assert.True(nord.IsSelected);
        Assert.Single(vm.GroupesThemes.SelectMany(g => g.Themes), c => c.IsSelected);

        // Sélection depuis le groupe VIVE.
        var lave = vm.GroupesThemes[2].Themes.Single(c => c.Name == "Lave");
        vm.SelectThemeCommand.Execute(lave);
        Assert.True(lave.IsSelected);
        Assert.False(nord.IsSelected);
        Assert.Equal("lave", vm.SelectedThemeKey);
    }
}
