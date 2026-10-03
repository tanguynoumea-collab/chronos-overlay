using System;

namespace Chronos.Rendering;

/// <summary>
/// Position angulaire PURE d'une braise sur un anneau de braises (aucun état, aucun I/O).
/// Repère du cadran : degrés, 0 = midi, sens horaire.
/// </summary>
public static class BraisesGeometrie
{
    /// <summary>
    /// Angle (degrés, 0 = midi, horaire) de la braise <paramref name="i"/> sur un anneau de <paramref name="count"/> braises.
    /// Avec des groupes (<paramref name="groupSize"/> &gt; 1), chaque groupe occupe un secteur de 360·groupSize/count degrés ;
    /// ses braises sont espacées de <paramref name="pasDansGroupe"/> et le groupe est CENTRÉ dans son secteur : le vide entre
    /// deux groupes est la délimitation (20 / 4 / 13,2 → 16,2 ; 29,4 ; 42,6 ; 55,8 ; puis +72° par groupe ; midi au milieu
    /// d'un vide de 32,4°).
    /// Repli sur la répartition uniforme i·360/count (comportement historique, anneau hebdo inchangé) si groupSize ≤ 1,
    /// si count n'est pas un multiple de groupSize, si le pas est nul ou négatif, ou si le groupe ne tient pas dans son secteur.
    /// </summary>
    public static double Angle(int i, int count, int groupSize, double pasDansGroupe)
    {
        int n = Math.Max(1, count);
        if (groupSize <= 1 || n % groupSize != 0) return i * 360.0 / n;

        double secteur = 360.0 * groupSize / n;                  // 20 / 4 → 72°
        double largeur = (groupSize - 1) * pasDansGroupe;        // 3 × 13,2 = 39,6°
        if (pasDansGroupe <= 0 || largeur >= secteur) return i * 360.0 / n;

        int g = i / groupSize, k = i % groupSize;
        return g * secteur + (secteur - largeur) / 2 + k * pasDansGroupe;   // groupe centré : 16,2 + k·13,2
    }
}
