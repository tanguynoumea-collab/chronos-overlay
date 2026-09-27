using System.Globalization;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// 35-01 (D-35-04, point différé 1 de 34-08, décision 3 : vue Jour SEULEMENT) — <see cref="LectureVeille"/> adjoint à la lecture
/// d'un jour le DERNIER relevé de la veille (source dominante du jour, au plus sept jours avant minuit) et les événements utiles,
/// pour que <see cref="AnalyseReleves"/> — inchangée, pure — voie le trou qui chevauche minuit et nomme sa cause. Puis la série
/// rendue à la vue est RESTREINTE au jour : le trou reste nommé, rien n'est dessiné ni compté avant le premier relevé du jour.
/// Lectures construites en mémoire, fuseau de Paris injecté.
/// </summary>
public class LectureVeilleTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();
    private static readonly TimeSpan Cadence = RateLimitHeaderUsageProvider.CadenceNominale;
    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    // mer. 23 sept. 2026, minuit → minuit Paris : 2026-09-22T22:00Z → 2026-09-23T22:00Z.
    private static readonly Plage Mercredi = BornesPlage.Jour(Utc("2026-09-23T10:00:00Z"), Tz);
    private static readonly DateTimeOffset DernierMardi = Utc("2026-09-22T21:00:00Z");   // mar. 23:00
    private static readonly DateTimeOffset PremierMercredi = Utc("2026-09-23T05:00:00Z"); // mer. 07:00

    private static ReleveJournal R(DateTimeOffset t, SourceUsage source = SourceUsage.SondeEnTetes)
        => new(t, source, 0.10, t + TimeSpan.FromHours(1), StatutServeur.Autorise, 0.30, Utc("2026-09-25T22:00:00Z"), StatutServeur.Autorise, null, null);

    private static IEnumerable<DateTimeOffset> Toutes5Min(DateTimeOffset de, DateTimeOffset a)
    {
        for (var t = de; t <= a; t += Cadence) yield return t;
    }

    // Relevés toutes les 5 min jusqu'à mar. 23:00, un événement « cause » juste après, un événement de reprise à 07:00, relevés dès 07:00.
    private static LectureJournal Large(TypeEvenement cause, TypeEvenement reprise)
    {
        var releves = Toutes5Min(Utc("2026-09-22T12:00:00Z"), DernierMardi)
            .Concat(Toutes5Min(PremierMercredi, Utc("2026-09-23T21:55:00Z")))
            .Select(t => R(t)).ToList();
        var evenements = new List<EvenementJournal>
        {
            new(Utc("2026-09-22T12:00:00Z"), TypeEvenement.Demarrage, Version: "tests"),   // trop ancien : jamais repris
            new(DernierMardi + TimeSpan.FromMinutes(2), cause),
            new(PremierMercredi, reprise, Version: "tests"),
        };
        return new LectureJournal(releves, evenements, 0, Utc("2026-09-14T10:00:00Z"), new Plage(Mercredi.Debut - LectureVeille.Horizon, Mercredi.Fin));
    }

    [Fact]
    public void Le_trou_qui_chevauche_minuit_garde_sa_cause()
    {
        var large = Large(TypeEvenement.Arret, TypeEvenement.Demarrage);

        var jour = LectureVeille.PourLeJour(large, Mercredi, Cadence);

        Assert.Equal(Mercredi, jour.Plage);
        Assert.Equal(DernierMardi, jour.Releves[0].T);
        Assert.Single(jour.Releves, r => r.T < Mercredi.Debut);                     // UN seul relevé antérieur
        Assert.Equal(large.Releves.Count(r => Mercredi.Contient(r.T)), jour.Releves.Count(r => Mercredi.Contient(r.T)));
        Assert.Contains(jour.Evenements, e => e.Type == TypeEvenement.Arret && e.T == DernierMardi + TimeSpan.FromMinutes(2));
        Assert.DoesNotContain(jour.Evenements, e => e.T == Utc("2026-09-22T12:00:00Z"));
        Assert.Equal(large.JournalOuvertLe, jour.JournalOuvertLe);

        var analyse = AnalyseReleves.Analyser(jour, Mercredi.Fin, Cadence);
        Assert.Contains(new Trou(DernierMardi, PremierMercredi, CauseTrou.ChronosArrete), analyse.Trous);

        // Une cause que SEULE la veille connaît : « jeton invalide » écrit mardi 23:02, reprise à 07:00 (sans démarrage).
        var jeton = AnalyseReleves.Analyser(LectureVeille.PourLeJour(Large(TypeEvenement.JetonInvalide, TypeEvenement.Reprise), Mercredi, Cadence), Mercredi.Fin, Cadence);
        Assert.Contains(new Trou(DernierMardi, PremierMercredi, CauseTrou.JetonInvalide), jeton.Trous);
    }

    [Fact]
    public void Un_jour_sans_releve_reste_tel_quel()
    {
        var releves = Toutes5Min(Utc("2026-09-22T12:00:00Z"), DernierMardi).Select(t => R(t)).ToList();
        var evenements = new List<EvenementJournal> { new(DernierMardi + TimeSpan.FromMinutes(2), TypeEvenement.Arret), new(Utc("2026-09-23T08:00:00Z"), TypeEvenement.Reprise) };
        var large = new LectureJournal(releves, evenements, 3, Utc("2026-09-14T10:00:00Z"), new Plage(Mercredi.Debut - LectureVeille.Horizon, Mercredi.Fin));

        var jour = LectureVeille.PourLeJour(large, Mercredi, Cadence);

        Assert.Empty(jour.Releves);
        Assert.Equal(new[] { Utc("2026-09-23T08:00:00Z") }, jour.Evenements.Select(e => e.T));
        Assert.Equal(Mercredi, jour.Plage);
        Assert.Equal(3, jour.LignesIgnorees);
    }

    [Fact]
    public void Sans_releve_dans_les_sept_jours_rien_n_est_ajoute()
    {
        var tropVieux = Mercredi.Debut - LectureVeille.Horizon - TimeSpan.FromMinutes(1);
        var releves = new[] { R(tropVieux) }.Concat(Toutes5Min(PremierMercredi, Utc("2026-09-23T06:00:00Z")).Select(t => R(t))).ToList();
        var large = new LectureJournal(releves, Array.Empty<EvenementJournal>(), 0, tropVieux, new Plage(tropVieux, Mercredi.Fin));

        var jour = LectureVeille.PourLeJour(large, Mercredi, Cadence);

        Assert.All(jour.Releves, r => Assert.True(Mercredi.Contient(r.T)));
        Assert.Equal(13, jour.Releves.Count);
        Assert.Equal(PremierMercredi, AnalyseReleves.Analyser(jour, Mercredi.Fin, Cadence).Serie[0].T);
    }

    [Fact]
    public void Seule_la_source_dominante_du_jour_est_reprise()
    {
        var releves = new List<ReleveJournal>
        {
            R(DernierMardi, SourceUsage.SondeEnTetes),
            R(DernierMardi + TimeSpan.FromMinutes(30), SourceUsage.PontStatusLine),
        };
        releves.AddRange(Toutes5Min(PremierMercredi, Utc("2026-09-23T06:00:00Z")).Select(t => R(t, SourceUsage.SondeEnTetes)));
        releves.Add(R(Utc("2026-09-23T06:02:00Z"), SourceUsage.PontStatusLine));
        var large = new LectureJournal(releves.OrderBy(r => r.T).ToList(), Array.Empty<EvenementJournal>(), 0, null, new Plage(Mercredi.Debut - LectureVeille.Horizon, Mercredi.Fin));

        var jour = LectureVeille.PourLeJour(large, Mercredi, Cadence);

        var veille = Assert.Single(jour.Releves, r => r.T < Mercredi.Debut);
        Assert.Equal(DernierMardi, veille.T);
        Assert.Equal(SourceUsage.SondeEnTetes, veille.Source);
        Assert.Contains(jour.Releves, r => r.Source == SourceUsage.PontStatusLine && Mercredi.Contient(r.T));   // le jour reste entier
    }

    [Fact]
    public void La_serie_rendue_ne_garde_que_le_jour_et_le_trou_reste_nomme()
    {
        var analyse = AnalyseReleves.Analyser(LectureVeille.PourLeJour(Large(TypeEvenement.Arret, TypeEvenement.Demarrage), Mercredi, Cadence), Mercredi.Fin, Cadence);
        Assert.Equal(DernierMardi, analyse.Serie[0].T);

        var rendue = LectureVeille.RestreindreAuJour(analyse, Mercredi);

        Assert.Equal(PremierMercredi, rendue.Serie[0].T);                     // rien n'est dessiné ni compté avant 07:00
        Assert.All(rendue.Serie, r => Assert.True(Mercredi.Contient(r.T)));
        Assert.Equal(analyse.Trous, rendue.Trous);                             // le trou de minuit reste nommé, avec sa cause
        Assert.Equal(analyse.Sauts, rendue.Sauts);
        Assert.Equal(analyse.Resets5h, rendue.Resets5h);
        Assert.Equal(Mercredi, rendue.Plage);
    }
}
