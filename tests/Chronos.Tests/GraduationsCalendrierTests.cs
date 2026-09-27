using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Pitfall 9 de la recherche 34 — « samedi → samedi » et « toutes les 3 h » ne sont PAS des multiples de 24 h / 3 h :
/// les graduations d'axe de la fenêtre Historique sont des instants du CALENDRIER LOCAL (minuits locaux, heures locales
/// rondes), obtenus par <see cref="BornesPlage.Jour"/> itéré et par le fuseau injecté. Semaine de 169 h → toujours 7 minuits
/// dont un écart de 25 h ; jour de 25 h → toujours 8 graduations de 3 h ; 28/03 → l'heure 02:00 n'existe pas et n'est pas
/// gradée. Tests purs, sans STA, fuseau de Paris injecté.
/// </summary>
public class GraduationsCalendrierTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();
    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void Une_semaine_ordinaire_a_sept_minuits_locaux()
    {
        var semaine = BornesPlage.SemaineDeForfait(Utc("2026-09-24T15:12:00Z"), Utc("2026-09-25T22:00:00Z"), null, Tz);

        var jours = GraduationsCalendrier.Jours(semaine, Tz);

        Assert.Equal(7, jours.Count);
        Assert.Equal(Utc("2026-09-18T22:00:00Z"), jours[0]);
        for (var i = 1; i < jours.Count; i++)
            Assert.Equal(TimeSpan.FromHours(24), jours[i] - jours[i - 1]);
    }

    [Fact]
    public void La_semaine_de_169_heures_a_sept_minuits_dont_un_ecart_de_25_heures()
    {
        var semaine = BornesPlage.SemaineDeForfait(Utc("2026-10-28T12:00:00Z"), null, null, Tz);   // sam. 24 → sam. 31 octobre 2026
        Assert.Equal(TimeSpan.FromHours(169), semaine.Duree);

        var jours = GraduationsCalendrier.Jours(semaine, Tz);

        Assert.Equal(7, jours.Count);
        var ecarts = Enumerable.Range(1, 6).Select(i => jours[i] - jours[i - 1]).ToList();
        Assert.Equal(1, ecarts.Count(e => e == TimeSpan.FromHours(25)));
        Assert.Equal(5, ecarts.Count(e => e == TimeSpan.FromHours(24)));
        Assert.Equal(TimeSpan.FromHours(25), jours[2] - jours[1]);   // entre le dimanche 25 et le lundi 26
    }

    [Fact]
    public void Un_jour_a_huit_graduations_de_trois_heures_meme_le_25_octobre()
    {
        var ordinaire = BornesPlage.Jour(Utc("2026-09-24T15:12:00Z"), Tz);
        var heures = GraduationsCalendrier.Heures(ordinaire, Tz, 3);
        Assert.Equal(8, heures.Count);
        Assert.Equal(ordinaire.Debut, heures[0]);
        Assert.Equal(ordinaire.Debut + TimeSpan.FromHours(3), heures[1]);

        var vingtCinqHeures = BornesPlage.Jour(Utc("2026-10-25T12:00:00Z"), Tz);
        var heuresDst = GraduationsCalendrier.Heures(vingtCinqHeures, Tz, 3);
        Assert.Equal(8, heuresDst.Count);
        Assert.Equal(vingtCinqHeures.Debut, heuresDst[0]);
        Assert.Equal(vingtCinqHeures.Debut + TimeSpan.FromHours(4), heuresDst[1]);   // « 3 h » local, à 4 h de temps réel (02:00 s'est répétée)
    }

    [Fact]
    public void Le_28_mars_2027_saute_l_heure_inexistante()
    {
        var jour = BornesPlage.Jour(Utc("2027-03-28T12:00:00Z"), Tz);

        var heures = GraduationsCalendrier.Heures(jour, Tz, 1);

        Assert.Equal(23, heures.Count);
        Assert.DoesNotContain(heures, h => TimeZoneInfo.ConvertTime(h, Tz).Hour == 2);
        Assert.Equal(jour.Debut + TimeSpan.FromHours(2), heures[2]);   // « 3 h » local, à 2 h de temps réel
    }
}
