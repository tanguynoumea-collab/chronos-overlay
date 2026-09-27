using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.ViewModels.Historique;

namespace Chronos.Tests;

/// <summary>
/// 35-01 (décision 1 de l'orchestrateur) — la semaine ÉPUISÉE de la vue 4 semaines est prouvée par une fixture DÉRIVÉE du
/// scénario, sans toucher <see cref="ScenariosHistorique"/> (la galerie et les tests d'honnêteté gardent la même semaine de
/// référence). Helper de test, pas un test ; réutilisé par 35-04 (vue XAML).
/// </summary>
internal static class FixturesQuatreSemaines
{
    /// <summary>jeu. 17 sept. 2026 20:00 Paris : à partir de ce relevé, S-1 est à 100 % et le serveur refuse.</summary>
    public static readonly DateTimeOffset DebutEpuisee = new(2026, 9, 17, 18, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Rend <paramref name="d"/> avec S-1 (<c>Semaines[2]</c>) poussée à 100 % le jeudi 17 sept. à partir de 20:00 Paris : chaque
    /// relevé de <c>T &gt;= DebutEpuisee</c> reçoit <c>U7 = 1,0</c> et <c>Statut7 = Rejete</c>, puis la semaine est RÉ-ANALYSÉE
    /// (même règle que les façades : instant d'analyse = fin de la plage révolue). S-3, S-2 et S sont inchangées.
    /// </summary>
    public static DonneesQuatreSemaines SemaineUnEpuisee(DonneesQuatreSemaines d, TimeZoneInfo tz)
    {
        ArgumentNullException.ThrowIfNull(d);
        var plage = d.Semaines[2].Plage;
        var lecture = ScenariosHistorique.Journal(plage, tz);
        var releves = lecture.Releves
            .Select(r => r.T >= DebutEpuisee ? r with { U7 = 1.0, Statut7 = StatutServeur.Rejete } : r)
            .ToList();
        var epuisee = AnalyseReleves.Analyser(lecture with { Releves = releves }, plage.Fin, RateLimitHeaderUsageProvider.CadenceNominale);
        return d with { Semaines = new[] { d.Semaines[0], d.Semaines[1], epuisee, d.Semaines[3] } };
    }

    /// <summary>jeu. 24 sept. 2026 10:00 Paris : à partir de ce relevé, la semaine COURANTE est à 100 % et le serveur refuse (35-04).</summary>
    public static readonly DateTimeOffset DebutCouranteEpuisee = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 35-04 — comme <see cref="SemaineUnEpuisee"/>, mais sur la semaine courante S (<c>Semaines[3]</c>) à partir de
    /// <see cref="DebutCouranteEpuisee"/> ; l'analyse se fait à <c>InstantDAnalyse(d.LueA, plage)</c> (la semaine est en cours).
    /// </summary>
    public static DonneesQuatreSemaines SemaineCouranteEpuisee(DonneesQuatreSemaines d, TimeZoneInfo tz)
    {
        ArgumentNullException.ThrowIfNull(d);
        var plage = d.Semaines[3].Plage;
        var lecture = ScenariosHistorique.Journal(plage, tz);
        var releves = lecture.Releves
            .Select(r => r.T >= DebutCouranteEpuisee ? r with { U7 = 1.0, Statut7 = StatutServeur.Rejete } : r)
            .ToList();
        var epuisee = AnalyseReleves.Analyser(lecture with { Releves = releves }, InstantsHistorique.InstantDAnalyse(d.LueA, plage),
            RateLimitHeaderUsageProvider.CadenceNominale);
        return d with { Semaines = new[] { d.Semaines[0], d.Semaines[1], d.Semaines[2], epuisee } };
    }
}
