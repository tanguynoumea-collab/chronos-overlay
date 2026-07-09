using System;
using System.Collections.Generic;

namespace Chronos.Rendering;

/// <summary>
/// Math PURE de la timeline 24 h (JOUR-01/02) : aucun état, aucun I/O, aucune CultureInfo.
/// 0° = minuit au sommet, sens horaire (cohérent <see cref="ArcGeometry"/> : 0° = 12 h horaire).
/// Les deux méthodes lisent le .TimeOfDay / .Date du DateTimeOffset FOURNI (composante offset-locale
/// du value) → déterministes en test, indépendantes du fuseau de la machine. L'appelant convertit en
/// heure locale via now.ToLocalTime() avant d'appeler.
/// </summary>
public static class DayTimeline
{
    private static readonly TimeSpan Step = TimeSpan.FromHours(5); // les resets 5 h tombent toutes les 5 h

    /// <summary>
    /// Fraction du jour écoulée : minutes-depuis-minuit-local / 1440. Minuit → 0, 18 h → 0.75,
    /// 23:59:59 → &lt; 1 (le 1 est réservé au minuit suivant).
    /// </summary>
    public static double Fraction(DateTimeOffset localNow)
        => localNow.TimeOfDay.TotalMinutes / 1440.0;

    /// <summary>
    /// Projette les resets 5 h du JOUR de <paramref name="localNow"/> sur l'axe 24 h : pour chaque reset
    /// du jour, angle = fraction-jour × 360° (h/24 × 360). Retourne la liste triée croissante.
    /// <paramref name="localReset5h"/> null → liste vide (rien à projeter honnêtement).
    /// </summary>
    public static IReadOnlyList<double> ResetAngles(DateTimeOffset localNow, DateTimeOffset? localReset5h)
    {
        if (localReset5h is not { } reset) return Array.Empty<double>();

        var jour = localNow.Date;

        // Normaliser par pas de 5 h jusqu'à retomber sur le jour de localNow (le resets_at peut être
        // n'importe quand dans le passé/futur).
        while (reset.Date > jour) reset -= Step;
        while (reset.Date < jour) reset += Step;

        // Rembobiner jusqu'au 1er reset du jour (le resets_at fourni peut tomber au milieu de la grille).
        while ((reset - Step).Date == jour) reset -= Step;

        // Énumérer tous les resets du jour, du 1er au dernier, et projeter chacun en angle.
        var angles = new List<double>();
        while (reset.Date == jour)
        {
            angles.Add(reset.TimeOfDay.TotalMinutes / 1440.0 * 360.0);
            reset += Step;
        }

        angles.Sort();
        return angles;
    }
}
