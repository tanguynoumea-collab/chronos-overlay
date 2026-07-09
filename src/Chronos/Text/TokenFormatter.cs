using System.Globalization;

namespace Chronos.Text;

/// <summary>
/// Formatage FR pur et abrégé d'un compte de tokens (NET-02). Aucune dépendance WPF, aucun I/O.
/// Abréviation M/k à 1 décimale, virgule française, décimale « ,0 » supprimée. Déterministe
/// (indépendant de la culture machine, comme <see cref="CountdownFormatter"/>). Type neutre.
/// </summary>
public static class TokenFormatter
{
    public static string Format(long tokens)
    {
        if (tokens < 0) tokens = 0; // borne : jamais de valeur négative affichée

        if (tokens >= 1_000_000)
            return $"≈ {Abbrev(tokens / 1_000_000d)} M tokens";
        if (tokens >= 1_000)
            return $"≈ {Abbrev(tokens / 1_000d)} k tokens";
        return $"≈ {tokens} tokens"; // cas brut : nombre entier, pas d'unité
    }

    // 1 décimale max, virgule française, « ,0 » supprimé ; culture invariante puis '.' -> ',' (déterministe).
    private static string Abbrev(double value)
        => value.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');
}
