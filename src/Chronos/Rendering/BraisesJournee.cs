using System;
using System.Collections.Generic;

namespace Chronos.Rendering;

/// <summary>État d'une braise de l'anneau journée de Braises (plan 43-09).</summary>
public enum EtatBraise
{
    /// <summary>Heure future : braise éteinte.</summary>
    Cendre,
    /// <summary>Heure en cours : demi-lueur.</summary>
    DemiLueur,
    /// <summary>Heure entièrement passée : braise allumée.</summary>
    Pleine,
}

/// <summary>
/// Math PURE de l'anneau EXTÉRIEUR de Braises (plan 43-09, constat du 2026-10-05) : la JOURNÉE locale en 24 braises, une
/// par heure d'horloge. Aucun état, aucun I/O, aucune horloge, aucun fuseau : on lit l'heure d'horloge du DateTimeOffset
/// FOURNI (l'appelant convertit dans le fuseau injecté), comme <see cref="DayTimeline"/>.
/// Repère : degrés, 0 = minuit en haut, sens horaire, 15° par heure ; braise i = heure [i:00, i+1:00), angle nominal
/// (i + 0,5) × 15°.
/// Groupes = tranches de la grille des resets 5 h ancrée sur le resets_at 5 h COURANT : tranche de la braise i =
/// floor((i:30 du jour − ancre) / 5 h). Seul le reset courant vient du serveur ; les autres limites de tranche sont
/// PROJETÉES de 5 h en 5 h (comme la timeline du mode Normal des Anneaux, DS-ARCH-05). Le VIDE entre deux groupes est la
/// seule délimitation. Chaque braise est rapprochée du centre angulaire de son groupe par un facteur 0,8 : écart de 12°
/// dans un groupe, au plus 6° (24 min) de son heure.
/// Limite connue (signalée au plan 43-09) : quand la grille laisse un groupe d'une à trois braises de part et d'autre de
/// minuit (ancres dont la première limite du jour tombe entre 00:30 et 03:30, modulo 5 h), le vide autour de ce petit
/// groupe est de 18° à 22,5° — toujours plus large que l'écart intra (12°), mais sous 2 × 12°. « ≤ 6° de l'heure » et
/// « vide ≥ 24° » y sont incompatibles ; le facteur 0,8 de la spécification est appliqué tel quel.
/// </summary>
public static class BraisesJournee
{
    /// <summary>Une braise par heure du jour.</summary>
    public const int Nombre = 24;

    /// <summary>Facteur de rapprochement vers le centre angulaire du groupe (spécification verrouillée).</summary>
    public const double Rapprochement = 0.8;

    private const double DegresParHeure = 360.0 / Nombre;          // 15°
    private const double MinutesParTranche = 5 * 60;               // grille des resets 5 h

    /// <summary>Angle nominal de la braise <paramref name="i"/> : le milieu de son heure, (i + 0,5) × 15°.</summary>
    public static double AngleNominal(int i) => (i + 0.5) * DegresParHeure;

    /// <summary>
    /// Tranche (index relatif à l'ancre) de chaque braise : floor((i:30 du jour local − ancre) / 5 h), calculé en heure
    /// d'horloge. Deux braises de même tranche forment un groupe. <paramref name="localReset5h"/> null → null (aucune grille).
    /// </summary>
    public static IReadOnlyList<int>? Tranches(DateTimeOffset localNow, DateTimeOffset? localReset5h)
    {
        if (localReset5h is not { } reset) return null;

        var jour = localNow.DateTime.Date;          // minuit du jour local (heure d'horloge)
        var ancre = reset.DateTime;                 // reset 5 h courant, heure d'horloge
        var tranches = new int[Nombre];
        for (int i = 0; i < Nombre; i++)
        {
            var centre = jour.AddHours(i).AddMinutes(30);
            tranches[i] = (int)Math.Floor((centre - ancre).TotalMinutes / MinutesParTranche);
        }
        return tranches;
    }

    /// <summary>
    /// Tailles des groupes successifs, de la braise 0 à la braise 23. Reset inconnu → un seul « groupe » de 24 (aucune
    /// grille inventée).
    /// </summary>
    public static IReadOnlyList<int> TaillesGroupes(DateTimeOffset localNow, DateTimeOffset? localReset5h)
    {
        var tranches = Tranches(localNow, localReset5h);
        if (tranches is null) return new[] { Nombre };

        var tailles = new List<int>();
        int taille = 1;
        for (int i = 1; i < Nombre; i++)
        {
            if (tranches[i] == tranches[i - 1]) { taille++; continue; }
            tailles.Add(taille);
            taille = 1;
        }
        tailles.Add(taille);
        return tailles;
    }

    /// <summary>
    /// Angle (degrés, 0 = minuit en haut, horaire) de chacune des 24 braises : angle = c + 0,8 × (nominal − c), c = centre
    /// angulaire du groupe (milieu de ses braises extrêmes). Reset inconnu → 24 braises uniformes à 15° (angle nominal).
    /// Les groupes ne traversent jamais minuit : 23:30 et 00:30 du même jour sont à 23 h d'écart, donc dans deux tranches.
    /// </summary>
    public static IReadOnlyList<double> Angles(DateTimeOffset localNow, DateTimeOffset? localReset5h)
    {
        var angles = new double[Nombre];
        var tranches = Tranches(localNow, localReset5h);
        if (tranches is null)
        {
            for (int i = 0; i < Nombre; i++) angles[i] = AngleNominal(i);
            return angles;
        }

        int debut = 0;
        while (debut < Nombre)
        {
            int fin = debut;
            while (fin + 1 < Nombre && tranches[fin + 1] == tranches[debut]) fin++;

            double centre = (AngleNominal(debut) + AngleNominal(fin)) / 2;
            for (int i = debut; i <= fin; i++)
                angles[i] = centre + Rapprochement * (AngleNominal(i) - centre);

            debut = fin + 1;
        }
        return angles;
    }

    /// <summary>
    /// Allumage : braise i pleine si l'heure i est entièrement passée (i &lt; floor(heures écoulées du jour)), demi-lueur
    /// pour l'heure en cours, cendre pour le futur. À minuit, la journée repart de zéro.
    /// </summary>
    public static IReadOnlyList<EtatBraise> Etats(DateTimeOffset localNow)
    {
        int enCours = Math.Clamp((int)Math.Floor(localNow.TimeOfDay.TotalHours), 0, Nombre - 1);
        var etats = new EtatBraise[Nombre];
        for (int i = 0; i < Nombre; i++)
            etats[i] = i < enCours ? EtatBraise.Pleine : i == enCours ? EtatBraise.DemiLueur : EtatBraise.Cendre;
        return etats;
    }
}
