namespace Chronos.Rendering.Historique;

/// <summary>
/// HIS-02 — l'ÉCHELLE DES VALEURS : une valeur (fraction 0..1 d'un relevé exact, ou un entier de tokens sur SON axe)
/// devient une ordonnée. Géométrie PURE : aucun état, aucun I/O, aucun type WPF. L'axe WPF descend : <c>y = 0</c> est
/// le haut de la piste, <c>y = hauteur</c> le bas — <see cref="Y"/> inverse donc l'axe et borne la valeur à
/// <c>[0, max]</c> (un relevé au-dessus du plafond s'écrase au bord, il ne sort pas de sa piste).
///
/// <para>Les deux séries de nature différente (% du compte, tokens Claude Code) passent ici avec leur PROPRE
/// <c>max</c> : jamais un axe commun. <see cref="MaxRythme"/> est l'échelle fixe « 0 – 25 % » de la piste Rythme ;
/// <see cref="MaxArrondi"/> donne le plafond « joli » de l'axe des tokens (« 0 – 1,2 M », D-34-12).</para>
/// </summary>
public static class EchelleValeur
{
    /// <summary>Échelle de la piste Rythme : « 0 – 25 % » (DESIGN_PLAN §2.2). Un Δ 5 h par heure au-dessus s'écrase au bord.</summary>
    public const double MaxRythme = 0.25;

    // D-34-12 : les mantisses « jolies » d'un axe, en dixièmes (10 → 1 ; 12 → 1,2 ; 15 → 1,5 ; … ; 80 → 8).
    private static readonly int[] MantissesEnDixiemes = { 10, 12, 15, 20, 25, 30, 40, 50, 60, 80 };

    /// <summary>
    /// Ordonnée de <paramref name="valeur"/> sur une piste de <paramref name="hauteur"/> pixels dont le plafond est
    /// <paramref name="max"/> : <c>hauteur − clamp(valeur / max, 0, 1) × hauteur</c>. <c>NaN</c> ou <c>max ≤ 0</c> →
    /// <paramref name="hauteur"/> (le plancher : rien à dessiner, aucune division par zéro, aucune exception).
    /// </summary>
    public static double Y(double valeur, double max, double hauteur)
    {
        if (double.IsNaN(valeur) || double.IsNaN(max) || max <= 0) return hauteur;
        var fraction = Math.Clamp(valeur / max, 0.0, 1.0);
        return hauteur - fraction * hauteur;
    }

    /// <summary>
    /// D-34-12 — le plafond « joli » d'un axe d'entiers (tokens) : la première valeur ≥ <paramref name="max"/> parmi les
    /// mantisses {1 ; 1,2 ; 1,5 ; 2 ; 2,5 ; 3 ; 4 ; 5 ; 6 ; 8} × 10^k (k = ordre de grandeur de <paramref name="max"/>),
    /// sinon 10^(k+1). Calcul en entiers exacts : 1 150 000 → 1 200 000 (« 0 – 1,2 M »), 999 → 1 000, 7 → 8.
    /// <c>max ≤ 0</c> → 1 (un axe a toujours une hauteur, même vide).
    /// </summary>
    public static long MaxArrondi(long max)
    {
        if (max <= 0) return 1;

        // 10^k : la plus grande puissance de dix ≤ max, par boucle entière (aucun log10 flottant : 1 200 000 doit rester exact).
        long puissance = 1;
        while (puissance <= max / 10) puissance *= 10;

        foreach (var dixiemes in MantissesEnDixiemes)
        {
            // mantisse × 10^k = dixiemes × 10^k / 10 ; pour k = 0 les mantisses non entières (1,2 ; 1,5 ; 2,5) sont sautées.
            if (puissance == 1 && dixiemes % 10 != 0) continue;
            var candidat = puissance == 1 ? dixiemes / 10 : dixiemes * (puissance / 10);
            if (candidat >= max) return candidat;
        }

        // Aucune mantisse ne suffit (max > 8 × 10^k) : la décade suivante. Au-delà de la capacité d'un long, on rend max lui-même.
        return puissance > long.MaxValue / 10 ? max : puissance * 10;
    }
}
