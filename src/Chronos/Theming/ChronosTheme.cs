using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Chronos.Rendering;

namespace Chronos.Theming;

/// <summary>
/// Palette complète d'un thème visuel du cadran. Chaque thème redéfinit le disque, les pistes des
/// trois anneaux, les graduations, les textes et la RAMPE d'utilisation (froid = marge, chaud = quota
/// entamé). Vit dans <c>Chronos.Theming</c> (hors pureté Services/Models) car il manipule
/// <see cref="Color"/> (WPF). Les nuances secondaires sont DÉRIVÉES de quelques couleurs de base
/// (<see cref="From"/>) pour garder le catalogue concis et cohérent.
/// </summary>
public sealed class ChronosTheme
{
    public required string Key { get; init; }
    public required string Name { get; init; }

    /// <summary>Famille du thème (Pâle · Classique · Vive, §5.1) : sert au regroupement de la section Thème des réglages.</summary>
    public CategorieTheme Categorie { get; init; }

    // Tokens de couleur (miroir des clés de DesignTokens.xaml, appliqués en ressources dynamiques).
    public Color FondCadran { get; init; }
    public Color Rim { get; init; }
    public Color TickMineur { get; init; }
    public Color TickMajeur { get; init; }
    public Color TickVisible { get; init; }
    public Color Piste5h { get; init; }
    public Color PisteHebdo { get; init; }
    public Color Piste24h { get; init; }
    public Color TextePrincipal { get; init; }
    public Color TexteSecondaireClair { get; init; }
    public Color TexteSecondaire { get; init; }

    // Rampe d'utilisation + états spéciaux de l'arc valeur.
    public Color RampGreen { get; init; }
    public Color RampAmber { get; init; }
    public Color RampRed { get; init; }
    public Color Neutre { get; init; }   // utilization inconnue → arc visible mais neutre
    public Color Epuise { get; init; }   // utilization ≥ 100 % → gris « épuisé »

    /// <summary>Couleur de l'arc valeur pour une utilization donnée (null → neutre, ≥1 → épuisé, sinon rampe).</summary>
    public Color ArcColor(double? utilization) => utilization switch
    {
        null => Neutre,
        >= 1.0 => Epuise,
        _ => RampColor.Interpolate(utilization.Value, RampGreen, RampAmber, RampRed),
    };

    /// <summary>Pinceau gelé (partageable) de l'arc valeur.</summary>
    public Brush ArcBrush(double? utilization)
    {
        var b = new SolidColorBrush(ArcColor(utilization));
        b.Freeze();
        return b;
    }

    /// <summary>Ressources (clé DesignTokens → pinceau gelé) à injecter dans les ressources de la fenêtre.</summary>
    public IReadOnlyDictionary<string, Brush> BrushTokens() => new Dictionary<string, Brush>
    {
        ["FondCadran"] = Frozen(FondCadran),
        ["Rim"] = Frozen(Rim),
        ["TickMineur"] = Frozen(TickMineur),
        ["TickMajeur"] = Frozen(TickMajeur),
        ["TickVisible"] = Frozen(TickVisible),
        ["Piste5h"] = Frozen(Piste5h),
        ["PisteHebdo"] = Frozen(PisteHebdo),
        ["Piste24h"] = Frozen(Piste24h),
        ["TextePrincipal"] = Frozen(TextePrincipal),
        ["TexteSecondaireClair"] = Frozen(TexteSecondaireClair),
        ["TexteSecondaire"] = Frozen(TexteSecondaire),
        ["Alerte"] = Frozen(RampAmber),   // TOK-02 : la pastille de déconnexion suit tous les thèmes.
                                          // Précédent exact : SessionBrushTokens()["SessAttention"].
    };

    /// <summary>Pinceaux du WIDGET DE SESSIONS (l'autre overlay), dérivés du thème pour que l'ensemble soit
    /// cohérent : fond des pastilles, encre, texte atténué, couleur d'attente. Les couleurs d'ÉTAT par session
    /// (attente/en cours/déduit) sont calculées côté SessionsViewModel à partir de la rampe (RampAmber/
    /// RampGreen/TexteSecondaire).</summary>
    public IReadOnlyDictionary<string, Brush> SessionBrushTokens() => new Dictionary<string, Brush>
    {
        ["SessPanel"] = Frozen(Color.FromArgb(0xCC, FondCadran.R, FondCadran.G, FondCadran.B)),
        ["SessInk"] = Frozen(TextePrincipal),
        ["SessMuted"] = Frozen(TexteSecondaire),
        ["SessAttention"] = Frozen(RampAmber),
    };

    // Pinceaux d'APERÇU (settings) — disque opaque + 3 stops de rampe + encre. Gelés, calculés une fois.
    public Brush PreviewDisc => _pDisc ??= Frozen(Color.FromRgb(FondCadran.R, FondCadran.G, FondCadran.B));
    public Brush PreviewGreen => _pGreen ??= Frozen(RampGreen);
    public Brush PreviewAmber => _pAmber ??= Frozen(RampAmber);
    public Brush PreviewRed => _pRed ??= Frozen(RampRed);
    public Brush PreviewInk => _pInk ??= Frozen(TextePrincipal);
    private Brush? _pDisc, _pGreen, _pAmber, _pRed, _pInk;

    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    /// <summary>Construit un thème à partir de 4 couleurs de base + 3 stops de rampe ; dérive le reste.</summary>
    public static ChronosTheme From(string key, string name, CategorieTheme categorie, string disc, string track, string tick,
                                    string ink, string green, string amber, string red)
    {
        Color d = Hex(disc), tr = Hex(track), tk = Hex(tick), nk = Hex(ink);   // d : disque OPAQUE
        return new ChronosTheme
        {
            Key = key,
            Name = name,
            Categorie = categorie,
            FondCadran = WithAlpha(d, 0xE6),                // disque légèrement translucide (il flotte sur le bureau)
            Rim = Lerp(d, tk, 0.12),
            TickMineur = Lerp(d, tr, 0.6),
            TickMajeur = Lerp(tr, tk, 0.2),
            TickVisible = tk,
            Piste5h = tr,
            PisteHebdo = Scale(tr, 0.9),
            Piste24h = Scale(tr, 0.82),
            TextePrincipal = nk,
            TexteSecondaireClair = Scale(nk, 0.82),
            TexteSecondaire = Scale(nk, 0.66),
            RampGreen = Hex(green),
            RampAmber = Hex(amber),
            RampRed = Hex(red),
            Neutre = Scale(tk, 0.5),
            Epuise = EpuiseLisible(d, tr, tk),
        };
    }

    /// <summary>
    /// Gris « épuisé » (règle §5.3) : le PLUS PETIT mélange piste → graduation (t ≥ 0,18, pas de 0,01) qui atteint un
    /// contraste WCAG ≥ 3:1, mesuré contre le disque OPAQUE (jamais FondCadran, translucide). Compteur entier pour
    /// éviter la dérive d'un <c>t += 0.01</c> ; comparaison sans arrondi (Lave vaut 3,0027). Un thème futur qui n'y
    /// parvient pas retombe sur la graduation elle-même — et fait rougir ThemingTests.
    /// </summary>
    private static Color EpuiseLisible(Color disque, Color piste, Color graduation)
    {
        for (int pas = 18; pas <= 100; pas++)
        {
            var c = Lerp(piste, graduation, pas / 100.0);
            if (ContrasteWcag.Ratio(c, disque) >= 3.0) return c;
        }
        return graduation;
    }

    // --- helpers couleur ---
    private static Color Hex(string s)
    {
        s = s.TrimStart('#');
        byte a = 0xFF, r, g, b;
        if (s.Length == 8) { a = Convert.ToByte(s[..2], 16); s = s[2..]; }
        r = Convert.ToByte(s[..2], 16); g = Convert.ToByte(s.Substring(2, 2), 16); b = Convert.ToByte(s.Substring(4, 2), 16);
        return Color.FromArgb(a, r, g, b);
    }
    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);
    private static Color Scale(Color c, double f) => Color.FromArgb(c.A,
        (byte)Math.Clamp(c.R * f, 0, 255), (byte)Math.Clamp(c.G * f, 0, 255), (byte)Math.Clamp(c.B * f, 0, 255));
    private static Color Lerp(Color a, Color b, double t) => Color.FromArgb(0xFF,
        (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
}

/// <summary>Catalogue des thèmes embarqués (15), en trois familles Pâle · Classique · Vive (§5). « minuit » est le
/// défaut historique et reste en tête (<see cref="Default"/>). Rangé par famille, dans l'ordre du plan à l'intérieur de
/// chaque groupe ; les réglages regroupent ensuite dans l'ordre de <see cref="CategorieTheme"/>. Les clés existantes ne
/// changent jamais : un thème déjà choisi reste choisi.</summary>
public static class ThemeCatalog
{
    public static readonly IReadOnlyList<ChronosTheme> All = new[]
    {
        // --- Classique (minuit en tête : Default => All[0]) ---
        ChronosTheme.From("minuit",    "Minuit",      CategorieTheme.Classique, "#16151B", "#2A2932", "#C9C8D2", "#F4F2EC", "#7BB13C", "#EFA23A", "#D8503A"),
        ChronosTheme.From("ardoise",   "Ardoise",     CategorieTheme.Classique, "#1B2027", "#2C333D", "#CBD3DE", "#EEF2F6", "#5FB39A", "#E0A94E", "#E06B5A"),
        ChronosTheme.From("ambre",     "Ambre chaud", CategorieTheme.Classique, "#1E1712", "#33271C", "#EAD9B8", "#F6ECD9", "#E4B24A", "#E07E3C", "#D24A3A"),
        ChronosTheme.From("graphite",  "Graphite",    CategorieTheme.Classique, "#121314", "#26282B", "#C4C7CC", "#F2F3F5", "#6DBE45", "#F0A830", "#E04B3C"),
        ChronosTheme.From("marine",    "Marine",      CategorieTheme.Classique, "#0F1A2A", "#1E2D44", "#B9C9DE", "#EAF0F7", "#5DBB7A", "#F2B544", "#E25C4F"),
        // --- Pâle ---
        ChronosTheme.From("nord",      "Nord",        CategorieTheme.Pale,      "#2E3440", "#3B4252", "#D8DEE9", "#ECEFF4", "#A3BE8C", "#EBCB8B", "#BF616A"),
        // Forêt — d'après Everforest (dark) : vert forêt terreux, texte sauge, rampe vert olive → jaune blé → rouge doux.
        ChronosTheme.From("foret",     "Forêt",       CategorieTheme.Pale,      "#2D353B", "#3A454A", "#A6B0A0", "#D3C6AA", "#A7C080", "#DBBC7F", "#E67E80"),
        // Moka — d'après Catppuccin Mocha : dark lavande pastel, texte bleu-lavande, rampe vert menthe → pêche → rose-rouge.
        ChronosTheme.From("moka",      "Moka",        CategorieTheme.Pale,      "#1E1E2E", "#313244", "#BAC2DE", "#CDD6F4", "#A6E3A1", "#FAB387", "#F38BA8"),
        // Roseraie — d'après Rosé Pine : dark rose-mauve, texte lilas clair, rampe écume (teal) → or → « love » (rose-rouge).
        ChronosTheme.From("roseraie",  "Roseraie",    CategorieTheme.Pale,      "#191724", "#26233A", "#B3AECC", "#E0DEF4", "#9CCFD8", "#F6C177", "#EB6F92"),
        ChronosTheme.From("sauge",     "Sauge",       CategorieTheme.Pale,      "#262B28", "#343B37", "#AEB8B0", "#DCE3DD", "#8FB996", "#D9C27E", "#D08A7E"),
        ChronosTheme.From("lavande",   "Lavande",     CategorieTheme.Pale,      "#22202C", "#302D3D", "#C3BCD9", "#E6E2F2", "#9FCFB0", "#E8C88E", "#E08E9E"),
        // --- Vive --- (Néon et Aurore : rampe corrigée §5.2, ambre et rouge vrais ; décors inchangés)
        ChronosTheme.From("neon",      "Néon",        CategorieTheme.Vive,      "#0E0A1F", "#241B3A", "#7DF9FF", "#E6E1FF", "#38E8C6", "#FFC23D", "#FF2E63"),
        ChronosTheme.From("aurore",    "Aurore",      CategorieTheme.Vive,      "#0E1726", "#1D2B44", "#BFE3FF", "#EAF2FF", "#4FD1C5", "#F0C36D", "#F2577A"),
        ChronosTheme.From("synthwave", "Synthwave",   CategorieTheme.Vive,      "#140B24", "#2A1745", "#9AE6FF", "#F5EEFF", "#2BFF88", "#FFD000", "#FF2D55"),
        ChronosTheme.From("lave",      "Lave",        CategorieTheme.Vive,      "#1A0E0A", "#33190F", "#FFC9A3", "#FFF1E6", "#7CFF4F", "#FFB000", "#FF3B1F"),
    };

    public static ChronosTheme Default => All[0];

    public static ChronosTheme ByKey(string? key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Default;
}
