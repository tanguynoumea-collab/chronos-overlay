using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// JRN-05 — les bornes des trois vues se calculent sur le CALENDRIER LOCAL, pas en <c>TimeSpan</c>.
///
/// Ce que ces tests gravent : la semaine de forfait est « samedi 00:00 → samedi 00:00 heure locale » (le
/// <c>resets_at</c> 7 j constaté est 2026-09-18T22:00Z = samedi 19/09 00:00 à Paris) ; celle du 24 au 31 octobre
/// 2026 dure 169 h, celle du 27 mars 2027 167 h ; le jour du 25/10/2026 dure 25 h. Et la DÉRIVE d'une heure d'un
/// pas fixe de 7 × 24 h est MESURÉE par un test — la raison pour laquelle les bornes suivent le calendrier.
///
/// Le fuseau est INJECTÉ (D-32-29) : « Romance Standard Time » avec repli « Europe/Paris », jamais celui de la
/// machine. Tests purs, sans horloge système.
/// </summary>
public class BornesPlageTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();
    private static readonly DateTimeOffset Ancre = new(2026, 07, 11, 0, 0, 0, TimeSpan.FromHours(2));   // ChronosSettings.WeeklyAnchor réel

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static string TestDataDir(string cas, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "journal", cas);

    [Fact]
    public void La_semaine_de_forfait_commence_le_samedi_a_minuit_local_depuis_le_reset_observe()
    {
        var plage = BornesPlage.SemaineDeForfait(Utc("2026-09-22T10:00:00Z"), resetHebdoObserve: Utc("2026-09-25T22:00:00Z"), ancre: null, Tz);

        Assert.Equal(Utc("2026-09-18T22:00:00Z"), plage.Debut);
        Assert.Equal(Utc("2026-09-25T22:00:00Z"), plage.Fin);
        Assert.Equal(TimeSpan.FromHours(168), plage.Duree);
    }

    [Fact]
    public void Sans_reset_observe_l_ancre_sert_de_repere()
    {
        var instant = Utc("2026-09-22T10:00:00Z");
        var attendu = new Plage(Utc("2026-09-18T22:00:00Z"), Utc("2026-09-25T22:00:00Z"));

        Assert.Equal(attendu, BornesPlage.SemaineDeForfait(instant, resetHebdoObserve: null, ancre: Ancre, Tz));
        Assert.Equal(attendu, BornesPlage.SemaineDeForfait(instant, resetHebdoObserve: null, ancre: null, Tz));   // repli : samedi du calendrier
    }

    [Fact]
    public void La_semaine_du_changement_d_heure_d_octobre_dure_169_heures()
    {
        var plage = BornesPlage.SemaineDeForfait(Utc("2026-10-27T12:00:00Z"), resetHebdoObserve: null, ancre: Ancre, Tz);

        Assert.Equal(Utc("2026-10-23T22:00:00Z"), plage.Debut);
        Assert.Equal(Utc("2026-10-30T23:00:00Z"), plage.Fin);
        Assert.Equal(TimeSpan.FromHours(169), plage.Duree);

        // Même résultat depuis un reset observé qui porte déjà la nouvelle heure.
        Assert.Equal(plage, BornesPlage.SemaineDeForfait(Utc("2026-10-27T12:00:00Z"), resetHebdoObserve: Utc("2026-10-30T23:00:00Z"), ancre: null, Tz));
    }

    [Fact]
    public void La_semaine_du_changement_d_heure_de_mars_dure_167_heures()
    {
        var plage = BornesPlage.SemaineDeForfait(Utc("2027-03-30T12:00:00Z"), resetHebdoObserve: null, ancre: null, Tz);

        Assert.Equal(Utc("2027-03-26T23:00:00Z"), plage.Debut);
        Assert.Equal(Utc("2027-04-02T22:00:00Z"), plage.Fin);
        Assert.Equal(TimeSpan.FromHours(167), plage.Duree);
    }

    /// <summary>
    /// Un pas fixe de 7 × 24 h depuis l'ancre (calculé ici en ligne) : après le 25/10/2026 sa borne tombe à 22:00Z
    /// là où le samedi 00:00 local est à 23:00Z. Ce test MESURE l'écart (exactement une heure) — c'est pourquoi
    /// <see cref="BornesPlage"/> suit le calendrier local et jamais un <c>TimeSpan</c> de 7 jours.
    /// </summary>
    [Fact]
    public void La_derive_DST_d_une_semaine_calendaire_est_mesurable()
    {
        var semaine = TimeSpan.FromDays(7);
        DateTimeOffset FinParPasFixe(DateTimeOffset instant)
            => Ancre + TimeSpan.FromTicks((instant - Ancre).Ticks / semaine.Ticks * semaine.Ticks) + semaine;

        var now = Utc("2026-10-27T12:00:00Z");

        var finParPasFixe = FinParPasFixe(now);
        var finSelonCalendrier = BornesPlage.SemaineDeForfait(now, resetHebdoObserve: null, ancre: Ancre, Tz).Fin;

        Assert.Equal(Utc("2026-10-30T22:00:00Z"), finParPasFixe);
        Assert.Equal(Utc("2026-10-30T23:00:00Z"), finSelonCalendrier);
        Assert.NotEqual(finParPasFixe, finSelonCalendrier);
        Assert.Equal(TimeSpan.FromHours(1), finSelonCalendrier - finParPasFixe);

        // Avant le changement d'heure, les deux coïncident : la dérive naît le 25/10.
        var avant = Utc("2026-09-22T10:00:00Z");
        Assert.Equal(FinParPasFixe(avant), BornesPlage.SemaineDeForfait(avant, null, Ancre, Tz).Fin);
    }

    [Fact]
    public void Le_jour_local_du_25_octobre_2026_dure_25_heures_et_celui_du_28_mars_2027_23()
    {
        var dst = BornesPlage.Jour(Utc("2026-10-25T10:00:00Z"), Tz);
        Assert.Equal(Utc("2026-10-24T22:00:00Z"), dst.Debut);
        Assert.Equal(Utc("2026-10-25T23:00:00Z"), dst.Fin);
        Assert.Equal(TimeSpan.FromHours(25), dst.Duree);

        var mars = BornesPlage.Jour(Utc("2027-03-28T10:00:00Z"), Tz);
        Assert.Equal(Utc("2027-03-27T23:00:00Z"), mars.Debut);
        Assert.Equal(Utc("2027-03-28T22:00:00Z"), mars.Fin);
        Assert.Equal(TimeSpan.FromHours(23), mars.Duree);

        var ordinaire = BornesPlage.Jour(Utc("2026-09-27T10:00:00Z"), Tz);
        Assert.Equal(Utc("2026-09-26T22:00:00Z"), ordinaire.Debut);
        Assert.Equal(TimeSpan.FromHours(24), ordinaire.Duree);

        // La fixture du jour DST porte un relevé par heure LOCALE : la plage du jour en contient 25 (le 26e est le minuit suivant, exclu).
        var lecture = LecteurJournal.Lire(TestDataDir("jour-dst-2026-10-25"), dst.Debut, dst.Fin);
        Assert.Equal(25, lecture.Releves.Count);
        Assert.Equal(2, lecture.Releves.Count(r => TimeZoneInfo.ConvertTime(r.T, Tz).Hour == 2));   // 02:00 local existe deux fois ce jour-là
    }

    [Fact]
    public void Quatre_semaines_finissent_par_la_semaine_courante_et_se_touchent()
    {
        var instant = Utc("2026-09-22T10:00:00Z");
        var reset = Utc("2026-09-25T22:00:00Z");

        var semaines = BornesPlage.QuatreSemaines(instant, reset, ancre: null, Tz);

        Assert.Equal(4, semaines.Count);
        Assert.Equal(BornesPlage.SemaineDeForfait(instant, reset, null, Tz), semaines[3]);
        for (var i = 1; i < semaines.Count; i++)
            Assert.Equal(semaines[i - 1].Fin, semaines[i].Debut);
        Assert.Equal(Utc("2026-08-28T22:00:00Z"), semaines[0].Debut);
    }

    [Fact]
    public void Une_borne_est_toujours_minuit_local_meme_a_travers_la_DST()
    {
        var semaines = BornesPlage.QuatreSemaines(Utc("2026-11-10T12:00:00Z"), resetHebdoObserve: null, ancre: null, Tz);

        Assert.Equal(4, semaines.Count);
        foreach (var plage in semaines)
        {
            foreach (var borne in new[] { plage.Debut, plage.Fin })
            {
                var locale = TimeZoneInfo.ConvertTime(borne, Tz);
                Assert.Equal(TimeSpan.Zero, locale.TimeOfDay);
                Assert.Equal(DayOfWeek.Saturday, locale.DayOfWeek);
            }
        }

        // Les quatre semaines couvrent le 25/10 : l'une d'elles dure 169 h, les trois autres 168 h.
        Assert.Equal(new[] { 168.0, 169.0, 168.0, 168.0 }, semaines.Select(s => s.Duree.TotalHours));
    }
}
