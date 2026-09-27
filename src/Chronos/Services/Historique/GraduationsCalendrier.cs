using Chronos.Models.Historique;

namespace Chronos.Services.Historique;

/// <summary>
/// Pitfall 9 de la recherche 34 — les graduations d'axe de la fenêtre Historique sont des instants du CALENDRIER LOCAL, pas
/// des multiples de 24 h ou de 3 h : une semaine de forfait dure 169 h en octobre (sept minuits dont un écart de 25 h), 167 h en
/// mars ; un jour dure 25 h le 25/10/2026 et 23 h le 28/03/2027.
///
/// <para><see cref="Jours"/> itère <see cref="BornesPlage.Jour"/> (le seul producteur de minuits locaux du dépôt) ;
/// <see cref="Heures"/> construit chaque heure locale ronde du jour et la convertit par l'offset du fuseau À CET INSTANT
/// LOCAL. Une heure locale INEXISTANTE (02:00 le 28/03) est sautée ; une heure AMBIGUË (02:00 le 25/10, qui existe deux fois)
/// reçoit le décalage STANDARD de <c>TimeZoneInfo.GetUtcOffset</c> — la graduation « 3 h » du 25/10 tombe donc APRÈS la
/// répétition, à 4 h de temps réel du minuit : c'est le calendrier, pas un défaut. Classe PURE, fuseau injecté, type NEUTRE.</para>
/// </summary>
public static class GraduationsCalendrier
{
    /// <summary>Les minuits LOCAUX de <c>[Debut, Fin[</c> (7 pour une semaine de forfait, 169 h comprise ; 1 pour un jour).</summary>
    public static IReadOnlyList<DateTimeOffset> Jours(Plage plage, TimeZoneInfo tz)
    {
        ArgumentNullException.ThrowIfNull(plage);
        ArgumentNullException.ThrowIfNull(tz);

        var minuits = new List<DateTimeOffset>();
        var jour = BornesPlage.Jour(plage.Debut, tz);
        while (jour.Debut < plage.Fin)
        {
            if (jour.Debut >= plage.Debut) minuits.Add(jour.Debut);
            jour = BornesPlage.Jour(jour.Fin, tz);
        }
        return minuits;
    }

    /// <summary>Les heures LOCALES multiples de <paramref name="pas"/> dans <c>[Debut, Fin[</c> (8 pour un jour et un pas de 3,
    /// même le 25/10 ; 23 pour le 28/03 et un pas de 1). <paramref name="pas"/> ≥ 1.</summary>
    public static IReadOnlyList<DateTimeOffset> Heures(Plage plage, TimeZoneInfo tz, int pas)
    {
        ArgumentNullException.ThrowIfNull(plage);
        ArgumentNullException.ThrowIfNull(tz);
        ArgumentOutOfRangeException.ThrowIfLessThan(pas, 1);

        var heures = new List<DateTimeOffset>();
        // Le premier jour LOCAL est celui de plage.Debut (même si la plage n'est pas alignée sur un minuit), puis les suivants.
        var jour = BornesPlage.Jour(plage.Debut, tz);
        while (jour.Debut < plage.Fin)
        {
            var minuitLocal = TimeZoneInfo.ConvertTime(jour.Debut, tz);
            for (var h = 0; h < 24; h += pas)
            {
                var local = new DateTime(minuitLocal.Year, minuitLocal.Month, minuitLocal.Day, h, 0, 0, DateTimeKind.Unspecified);
                if (tz.IsInvalidTime(local)) continue;   // 02:00 le 28/03 : cette heure n'existe pas
                var utc = new DateTimeOffset(local, tz.GetUtcOffset(local));
                if (plage.Contient(utc)) heures.Add(utc);
            }
            jour = BornesPlage.Jour(jour.Fin, tz);
        }
        return heures;
    }
}
