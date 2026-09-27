using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-02 / HIS-03 (côté géométrie) — les TUILES 5 h du style « Tuiles » : une tuile par groupe de relevés consécutifs
/// de même <c>resets_at</c>, du premier relevé au reset OBSERVÉ (borné à la plage), hauteur = max % 5 h, grise si épuisée.
///
/// Ce que ces tests gravent : la première tuile de <c>reset-5h-milieu-heure</c> finit EXACTEMENT à <c>ResetObserve.Instant</c>
/// (13:37Z) et la seconde commence à <c>ObserveA</c> (13:40Z) ; « épuisée » = compteur à 1 OU statut serveur <c>Rejete</c> ;
/// la journée nominale de 288 relevés donne cinq tuiles contiguës ; sans <c>R5</c> ou sans <c>U5</c>, rien ; jamais au-delà
/// de la fin de plage.
/// </summary>
public class Tuiles5hTests
{
    private static readonly TimeSpan Cadence = RateLimitHeaderUsageProvider.CadenceNominale;
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();
    private const string A = "2026-09-27T13:37:00Z";

    private static string TestDataDir(string cas, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "journal", cas);

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static ReleveJournal R(string t, double? u5, string? r5 = A, StatutServeur statut = StatutServeur.Autorise)
        => new(Utc(t), SourceUsage.SondeEnTetes, u5, r5 is null ? null : Utc(r5), statut, null, null, null, null, null);

    [Fact]
    public void Une_tuile_par_resets_at_bornee_au_reset_observe()
    {
        var a = AnalyseReleves.Analyser(
            LecteurJournal.Lire(TestDataDir("reset-5h-milieu-heure"), Utc("2026-09-27T13:00:00Z"), Utc("2026-09-27T14:00:00Z")),
            Utc("2026-09-27T14:00:00Z"), Cadence);
        var reset = Assert.Single(a.Resets5h);

        var t = Tuiles5h.Depuis(a.Serie, a.Plage);

        Assert.Equal(2, t.Count);
        Assert.Equal(Utc("2026-09-27T13:00:00Z"), t[0].Debut);
        Assert.Equal(reset.Instant, t[0].Fin);            // EXACTEMENT l'ancienne borne : 13:37Z, au milieu de l'heure
        Assert.Equal(Utc(A), t[0].ResetsAt);
        Assert.True(t[0].ResetDansPlage);
        Assert.Equal(0.86, t[0].UMax);
        Assert.False(t[0].Epuisee);

        Assert.Equal(reset.ObserveA, t[1].Debut);         // 13:40Z : le premier relevé qui porte la nouvelle borne
        Assert.Equal(a.Plage.Fin, t[1].Fin);              // le reset 18:37Z est après la plage : coupée à 14:00Z
        Assert.Equal(Utc("2026-09-27T18:37:00Z"), t[1].ResetsAt);
        Assert.False(t[1].ResetDansPlage);
        Assert.Equal(0.05, t[1].UMax);
    }

    [Fact]
    public void Une_tuile_est_epuisee_si_le_serveur_refuse_ou_si_le_compteur_atteint_un()
    {
        var plage = new Plage(Utc("2026-09-27T13:00:00Z"), Utc("2026-09-27T14:00:00Z"));

        var compteur = new[] { R("2026-09-27T13:00:00Z", 0.7), R("2026-09-27T13:05:00Z", 0.9), R("2026-09-27T13:10:00Z", 1.0), R("2026-09-27T13:15:00Z", 1.0) };
        Assert.True(Assert.Single(Tuiles5h.Depuis(compteur, plage)).Epuisee);

        var refus = new[] { R("2026-09-27T13:00:00Z", 0.7), R("2026-09-27T13:05:00Z", 0.8, statut: StatutServeur.Rejete) };
        var tuileRefus = Assert.Single(Tuiles5h.Depuis(refus, plage));
        Assert.True(tuileRefus.Epuisee);   // le serveur refuse : épuisée même si le compteur dit 0,8
        Assert.Equal(0.8, tuileRefus.UMax);

        var normal = new[] { R("2026-09-27T13:00:00Z", 0.7), R("2026-09-27T13:05:00Z", 0.8) };
        Assert.False(Assert.Single(Tuiles5h.Depuis(normal, plage)).Epuisee);
    }

    [Fact]
    public void La_journee_nominale_donne_cinq_tuiles()
    {
        var minuit = Utc("2026-09-26T22:00:00Z");   // minuit local du 27/09 (Paris, heure d'été)
        var dossier = FabriqueJournal.JourneeNominale(minuit);
        var plage = BornesPlage.Jour(minuit, Tz);
        Assert.Equal(new Plage(minuit, minuit + TimeSpan.FromHours(24)), plage);

        var a = AnalyseReleves.Analyser(LecteurJournal.Lire(dossier, plage.Debut, plage.Fin), plage.Fin, Cadence);
        Assert.Equal(288, a.Serie.Count);

        var t = Tuiles5h.Depuis(a.Serie, a.Plage);

        Assert.Equal(5, t.Count);   // 5 valeurs de R5 : 4 resets + la borne initiale
        Assert.Equal(5, t.Select(x => x.ResetsAt).Distinct().Count());
        // Tuiles contiguës : le premier relevé de la fenêtre suivante tombe pile sur le reset (la fabrique relève toutes les 5 min,
        // et ses resets sont des multiples de 5 min) — t[i].Fin == t[i+1].Debut.
        for (var i = 0; i < t.Count - 1; i++)
        {
            Assert.Equal(t[i + 1].Debut, t[i].Fin);
            Assert.True(t[i].ResetDansPlage);
        }
        Assert.Equal(minuit, t[0].Debut);
        Assert.Equal(plage.Fin, t[4].Fin);                       // la dernière fenêtre (reset à minuit + 25 h) est coupée à la fin du jour
        Assert.Equal(t[4].ResetsAt < plage.Fin, t[4].ResetDansPlage);
        Assert.False(t[4].ResetDansPlage);
        Assert.All(t, x => Assert.False(x.Epuisee));
        Assert.All(t, x => Assert.True(x.Fin <= plage.Fin));
    }

    [Fact]
    public void Les_releves_sans_R5_ou_sans_U5_sont_ignores_et_une_tuile_hors_plage_est_omise()
    {
        var plage = new Plage(Utc("2026-09-27T13:00:00Z"), Utc("2026-09-27T14:00:00Z"));

        var sansR5 = new[] { R("2026-09-27T13:00:00Z", 0.7, r5: null), R("2026-09-27T13:05:00Z", 0.8, r5: null) };
        Assert.Empty(Tuiles5h.Depuis(sansR5, plage));

        var sansU5 = new[] { R("2026-09-27T13:00:00Z", null), R("2026-09-27T13:05:00Z", null) };
        Assert.Empty(Tuiles5h.Depuis(sansU5, plage));

        var horsPlage = new[] { R("2026-09-27T14:00:00Z", 0.7, r5: "2026-09-27T18:37:00Z"), R("2026-09-27T14:05:00Z", 0.8, r5: "2026-09-27T18:37:00Z") };
        Assert.Empty(Tuiles5h.Depuis(horsPlage, plage));   // le groupe commence à Plage.Fin : omis

        var deborde = new[] { R("2026-09-27T13:50:00Z", 0.7, r5: "2026-09-27T18:37:00Z"), R("2026-09-27T13:55:00Z", 0.8, r5: "2026-09-27T18:37:00Z") };
        var tuile = Assert.Single(Tuiles5h.Depuis(deborde, plage));
        Assert.Equal(plage.Fin, tuile.Fin);                 // Fin jamais > Plage.Fin
        Assert.False(tuile.ResetDansPlage);
        Assert.Equal(Utc("2026-09-27T18:37:00Z"), tuile.ResetsAt);
    }
}
