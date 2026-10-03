using Chronos.Models.Historique;

namespace Chronos.Services.Historique;

/// <summary>
/// JRN-05 — les bornes des trois vues (Semaine de forfait, Jour, 4 semaines).
///
/// <para><b>POURQUOI le calendrier local et pas un <c>TimeSpan</c> de 7 jours</b> : la semaine de forfait est
/// « samedi 00:00 → samedi 00:00 HEURE LOCALE » (<c>resets_at</c> 7 j constaté : 2026-09-18T22:00Z = samedi 19/09
/// 00:00 à Paris). Le reset hebdo vient du serveur ; l'ancre enregistrée n'est lue qu'en secours, ici. Avancer par
/// 7 × 24 h fixes dériverait d'UNE heure le 25/10/2026 (2026-10-30T22:00Z au lieu de 23:00Z) — mesuré par
/// <c>BornesPlageTests.La_derive_DST_d_une_semaine_calendaire_est_mesurable</c>. Ici : <c>DateTime</c> local +
/// <c>AddDays</c> + <c>tz.GetUtcOffset</c> →
/// 169 h en octobre, 167 h en mars, 25 h le jour du 25/10.</para>
///
/// <para>Le fuseau est INJECTÉ (D-32-29) : production <c>TimeZoneInfo.Local</c>, tests <see cref="FuseauParisPourTests"/>.
/// Repère de la semaine : le <c>resets_at</c> 7 j observé dans le journal, repli <c>ChronosSettings.WeeklyAnchor</c>
/// (paramètre <c>ancre</c>), repli le samedi du calendrier. Classe PURE, type NEUTRE (aucun WPF).</para>
/// </summary>
public static class BornesPlage
{
    /// <summary>
    /// La semaine de forfait qui contient <paramref name="instant"/> : <c>[samedi 00:00 local, samedi suivant 00:00 local[</c>,
    /// alignée sur le jour de semaine du repère (<paramref name="resetHebdoObserve"/> ?? <paramref name="ancre"/> ?? samedi).
    /// </summary>
    public static Plage SemaineDeForfait(DateTimeOffset instant, DateTimeOffset? resetHebdoObserve, DateTimeOffset? ancre, TimeZoneInfo tz)
    {
        ArgumentNullException.ThrowIfNull(tz);

        // Repère : le prochain reset hebdo OBSERVÉ (r7 du dernier relevé) ; repli : l'ancre des réglages ; repli : l'instant
        // lui-même, ramené au samedi. Puis MINUIT LOCAL du jour du repère, reculé au samedi — sur le CALENDRIER local.
        var repere = resetHebdoObserve ?? ancre ?? instant;
        var minuit = MinuitLocal(repere, tz);
        while (minuit.DayOfWeek != DayOfWeek.Saturday) minuit = minuit.AddDays(-1);

        // Avancer / reculer de 7 jours LOCAUX (AddDays sur le DateTime local : 169 h le 25/10, 167 h le 28/03) jusqu'à
        // encadrer l'instant : Debut <= instant < Fin.
        var fin = Utc(minuit, tz);
        while (fin <= instant) fin = Utc(minuit = minuit.AddDays(7), tz);
        while (Utc(minuit.AddDays(-7), tz) > instant) fin = Utc(minuit = minuit.AddDays(-7), tz);

        return new Plage(Utc(minuit.AddDays(-7), tz), fin);
    }

    /// <summary>Le jour local qui contient <paramref name="instant"/> : minuit local → minuit local suivant (25 h le 25/10/2026, 23 h le 28/03/2027).</summary>
    public static Plage Jour(DateTimeOffset instant, TimeZoneInfo tz)
    {
        ArgumentNullException.ThrowIfNull(tz);
        var minuit = MinuitLocal(instant, tz);
        return new Plage(Utc(minuit, tz), Utc(minuit.AddDays(1), tz));
    }

    /// <summary>
    /// Les quatre semaines de forfait S-3, S-2, S-1, S (la dernière = <see cref="SemaineDeForfait"/>), contiguës :
    /// chaque <c>Fin</c> est le <c>Debut</c> de la suivante. Reculs successifs de 7 jours LOCAUX depuis le début de S.
    /// </summary>
    public static IReadOnlyList<Plage> QuatreSemaines(DateTimeOffset instant, DateTimeOffset? resetHebdoObserve, DateTimeOffset? ancre, TimeZoneInfo tz)
    {
        var courante = SemaineDeForfait(instant, resetHebdoObserve, ancre, tz);
        var semaines = new Plage[4];
        semaines[3] = courante;

        var debutLocal = MinuitLocal(courante.Debut, tz);
        for (var i = 2; i >= 0; i--)
        {
            var precedent = debutLocal.AddDays(-7);
            semaines[i] = new Plage(Utc(precedent, tz), Utc(debutLocal, tz));
            debutLocal = precedent;
        }
        return semaines;
    }

    /// <summary>
    /// Fuseau des tests : Windows « Romance Standard Time », repli IANA « Europe/Paris » (les deux acceptés sur la
    /// machine cible, .NET 8 sait lire les identifiants IANA sous Windows). Jamais <c>TimeZoneInfo.Local</c> en test.
    /// </summary>
    public static TimeZoneInfo FuseauParisPourTests()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"); }
    }

    // Minuit LOCAL (DateTimeKind.Unspecified) du jour où tombe l'instant dans le fuseau donné.
    private static DateTime MinuitLocal(DateTimeOffset instant, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTime(instant, tz);
        return new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
    }

    // Local → UTC par l'offset du fuseau À CET INSTANT LOCAL : jamais ConvertTimeToUtc sur un DateTimeKind.Local
    // du système (qui appliquerait le fuseau de la machine, pas celui injecté). Minuit n'est jamais ambigu à Paris.
    private static DateTimeOffset Utc(DateTime local, TimeZoneInfo tz)
        => new(local, tz.GetUtcOffset(local));
}
