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
    public static ChronosTheme From(string key, string name, string disc, string track, string tick, string ink,
                                    string green, string amber, string red)
    {
        Color d = Hex(disc), tr = Hex(track), tk = Hex(tick), nk = Hex(ink);
        return new ChronosTheme
        {
            Key = key,
            Name = name,
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
            Epuise = Lerp(tr, tk, 0.18),
        };
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

/// <summary>Catalogue des thèmes embarqués (6). « minuit » est le défaut historique.</summary>
public static class ThemeCatalog
{
    public static readonly IReadOnlyList<ChronosTheme> All = new[]
    {
        ChronosTheme.From("minuit",  "Minuit",      "#16151B", "#2A2932", "#C9C8D2", "#F4F2EC", "#7BB13C", "#EFA23A", "#D8503A"),
        ChronosTheme.From("ardoise", "Ardoise",     "#1B2027", "#2C333D", "#CBD3DE", "#EEF2F6", "#5FB39A", "#E0A94E", "#E06B5A"),
        ChronosTheme.From("nord",    "Nord",        "#2E3440", "#3B4252", "#D8DEE9", "#ECEFF4", "#A3BE8C", "#EBCB8B", "#BF616A"),
        ChronosTheme.From("neon",    "Néon",        "#0E0A1F", "#241B3A", "#7DF9FF", "#E6E1FF", "#38E8C6", "#B14BFF", "#FF2E97"),
        ChronosTheme.From("aurore",  "Aurore",      "#0E1726", "#1D2B44", "#BFE3FF", "#EAF2FF", "#4FD1C5", "#6D8CF0", "#C86BE0"),
        ChronosTheme.From("ambre",   "Ambre chaud", "#1E1712", "#33271C", "#EAD9B8", "#F6ECD9", "#E4B24A", "#E07E3C", "#D24A3A"),
    };

    public static ChronosTheme Default => All[0];

    public static ChronosTheme ByKey(string? key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Default;
}
