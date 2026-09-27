using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// JRN-05 — l'analyse PURE d'une lecture : ce que les relevés DISENT, et rien de plus.
///
/// Ce que ces tests gravent : une vue lit UNE source (D-32-25) ; un trou est un écart de plus de deux cadences
/// (deux cadences pile n'en est pas un) et porte sa cause dans les mots du plan de design (D-32-26) — « Chronos
/// arrêté », « jeton invalide », « cause inconnue », jamais tu ; un reset observé est daté de l'ANCIENNE borne
/// (D-32-27) ; un Δ n'existe qu'entre relevés consécutifs de MÊME <c>resets_at</c> et sans trou entre eux, un Δ
/// négatif est conservé et marqué <c>Anormal</c> (D-32-28) ; le saut de part et d'autre d'un trou est « non
/// localisé », et <c>null</c> si un reset est tombé pendant l'absence.
///
/// <c>now</c> et <c>cadence</c> sont des paramètres : aucune horloge, aucune E/S hors des fixtures versionnées.
/// </summary>
public class AnalyseRelevesTests
{
    private static readonly TimeSpan Cadence = TimeSpan.FromSeconds(300);
    private const string A = "2026-09-27T13:37:00Z";   // ancienne borne 5 h de la fixture reset-5h-milieu-heure
    private const string B = "2026-09-27T18:37:00Z";   // nouvelle borne

    private static string TestDataDir(string cas, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "journal", cas);

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static LectureJournal Fixture(string cas, string de, string a)
        => LecteurJournal.Lire(TestDataDir(cas), Utc(de), Utc(a));

    private static ReleveJournal R(string t, double u5, string r5, SourceUsage source = SourceUsage.SondeEnTetes, double? u7 = null, string? r7 = null)
        => new(Utc(t), source, u5, Utc(r5), StatutServeur.Autorise, u7, r7 is null ? null : Utc(r7), null, null, null);

    private static LectureJournal EnMemoire(IEnumerable<ReleveJournal> releves, params EvenementJournal[] evenements)
    {
        var liste = releves.ToList();
        var plage = new Plage(liste.Min(r => r.T), liste.Max(r => r.T) + TimeSpan.FromTicks(1));
        return new LectureJournal(liste, evenements, 0, null, plage);
    }

    // --- Source ---

    [Fact]
    public void La_serie_ne_garde_qu_une_source()
    {
        var lecture = EnMemoire(new[]
        {
            R("2026-09-27T10:00:00Z", 0.10, A),
            R("2026-09-27T10:02:00Z", 0.10, A, SourceUsage.EndpointOAuthChronos),
            R("2026-09-27T10:05:00Z", 0.11, A),
            R("2026-09-27T10:07:00Z", 0.11, A, SourceUsage.EndpointOAuthChronos),
            R("2026-09-27T10:10:00Z", 0.12, A),
        });
        var now = Utc("2026-09-27T10:12:00Z");

        var sonde = AnalyseReleves.Analyser(lecture, now, Cadence);
        Assert.Equal(SourceUsage.SondeEnTetes, sonde.Source);   // la plus fréquente
        Assert.Equal(3, sonde.Serie.Count);
        Assert.All(sonde.Serie, r => Assert.Equal(SourceUsage.SondeEnTetes, r.Source));

        var oauth = AnalyseReleves.Analyser(lecture, now, Cadence, source: SourceUsage.EndpointOAuthChronos);
        Assert.Equal(SourceUsage.EndpointOAuthChronos, oauth.Source);
        Assert.Equal(2, oauth.Serie.Count);
        Assert.All(oauth.Serie, r => Assert.Equal(SourceUsage.EndpointOAuthChronos, r.Source));
    }

    // --- Trous ---

    [Fact]
    public void Un_ecart_de_plus_de_deux_cadences_est_un_trou_et_un_de_deux_cadences_n_en_est_pas_un()
    {
        var lecture = EnMemoire(new[]
        {
            R("2026-09-27T10:00:00Z", 0.10, A),
            R("2026-09-27T10:10:00Z", 0.11, A),      // = 2 × cadence : PAS un trou
            R("2026-09-27T10:20:01Z", 0.12, A),      // > 2 × cadence : trou
        });

        var analyse = AnalyseReleves.Analyser(lecture, Utc("2026-09-27T10:25:00Z"), Cadence);

        var trou = Assert.Single(analyse.Trous);
        Assert.Equal(Utc("2026-09-27T10:10:00Z"), trou.Debut);
        Assert.Equal(Utc("2026-09-27T10:20:01Z"), trou.Fin);
    }

    [Fact]
    public void Le_trou_arrete_porte_Chronos_arrete()
    {
        var analyse = AnalyseReleves.Analyser(Fixture("trou-arrete", "2026-09-27T10:00:00Z", "2026-09-27T11:20:00Z"), Utc("2026-09-27T11:20:00Z"), Cadence);

        var trou = Assert.Single(analyse.Trous);
        Assert.Equal(Utc("2026-09-27T10:10:00Z"), trou.Debut);
        Assert.Equal(Utc("2026-09-27T11:03:00Z"), trou.Fin);
        Assert.Equal(CauseTrou.ChronosArrete, trou.Cause);
        Assert.Equal("Chronos arrêté", CauseTrouTexte.Libelle(trou.Cause));
        Assert.Equal(7, analyse.Serie.Count);
    }

    [Fact]
    public void Le_trou_jeton_porte_jeton_invalide()
    {
        var analyse = AnalyseReleves.Analyser(Fixture("trou-jeton", "2026-09-27T14:00:00Z", "2026-09-27T15:20:00Z"), Utc("2026-09-27T15:20:00Z"), Cadence);

        var trou = Assert.Single(analyse.Trous);
        Assert.Equal(Utc("2026-09-27T14:05:00Z"), trou.Debut);
        Assert.Equal(Utc("2026-09-27T15:00:00Z"), trou.Fin);
        Assert.Equal(CauseTrou.JetonInvalide, trou.Cause);
        Assert.Equal("jeton invalide", CauseTrouTexte.Libelle(trou.Cause));
    }

    [Fact]
    public void Un_trou_sans_evenement_mais_avec_un_demarrage_dedans_est_Chronos_arrete_sinon_cause_inconnue()
    {
        var releves = new[] { R("2026-09-27T10:00:00Z", 0.10, A), R("2026-09-27T11:00:00Z", 0.20, A) };
        var now = Utc("2026-09-27T11:05:00Z");

        var avecDemarrage = AnalyseReleves.Analyser(
            EnMemoire(releves, new EvenementJournal(Utc("2026-09-27T10:50:00Z"), TypeEvenement.Demarrage, Version: "3.2.2")), now, Cadence);
        Assert.Equal(CauseTrou.ChronosArrete, Assert.Single(avecDemarrage.Trous).Cause);   // kill ou veille : pas d'arret écrit

        var sansRien = AnalyseReleves.Analyser(EnMemoire(releves), now, Cadence);
        var trou = Assert.Single(sansRien.Trous);
        Assert.Equal(CauseTrou.Inconnue, trou.Cause);
        Assert.Equal("cause inconnue", CauseTrouTexte.Libelle(trou.Cause));

        // Une reprise seule n'explique rien : elle dit « le trou finit ici », pas pourquoi il a commencé.
        var avecReprise = AnalyseReleves.Analyser(
            EnMemoire(releves, new EvenementJournal(Utc("2026-09-27T11:00:00Z"), TypeEvenement.Reprise, Cause: "trou de 60 min")), now, Cadence);
        Assert.Equal(CauseTrou.Inconnue, Assert.Single(avecReprise.Trous).Cause);
    }

    [Fact]
    public void Un_trou_ouvert_a_la_fin_a_une_fin_nulle()
    {
        var releves = new[] { R("2026-09-27T09:50:00Z", 0.10, A), R("2026-09-27T09:55:00Z", 0.11, A) };

        var ouvert = AnalyseReleves.Analyser(EnMemoire(releves), Utc("2026-09-27T10:20:00Z"), Cadence);   // dernier relevé à now − 25 min
        var trou = Assert.Single(ouvert.Trous);
        Assert.Null(trou.Fin);
        Assert.Equal(Utc("2026-09-27T09:55:00Z"), trou.Debut);
        Assert.Equal(CauseTrou.Inconnue, trou.Cause);
        Assert.Empty(ouvert.Sauts);   // un trou ouvert n'a pas d'« après » : aucun saut

        var recent = AnalyseReleves.Analyser(EnMemoire(releves), Utc("2026-09-27T10:01:00Z"), Cadence);   // dernier relevé à now − 6 min
        Assert.Empty(recent.Trous);
    }

    // --- Resets et Δ ---

    [Fact]
    public void Les_resets_observes_sont_dates_de_l_ancienne_borne()
    {
        var analyse = AnalyseReleves.Analyser(Fixture("reset-5h-milieu-heure", "2026-09-27T13:00:00Z", "2026-09-27T14:00:00Z"), Utc("2026-09-27T14:00:00Z"), Cadence);

        var reset = Assert.Single(analyse.Resets5h);
        Assert.Equal(WindowKind.FiveHour, reset.Fenetre);
        Assert.Equal(Utc(A), reset.Instant);
        Assert.Equal(Utc("2026-09-27T13:40:00Z"), reset.ObserveA);
        Assert.Empty(analyse.ResetsHebdo);
        Assert.Empty(analyse.Trous);
    }

    [Fact]
    public void Les_deltas_ne_traversent_jamais_un_reset_et_un_delta_negatif_est_anormal()
    {
        var analyse = AnalyseReleves.Analyser(Fixture("reset-5h-milieu-heure", "2026-09-27T13:00:00Z", "2026-09-27T14:00:00Z"), Utc("2026-09-27T14:00:00Z"), Cadence);
        var deltas = analyse.Deltas5h;

        Assert.Equal(10, deltas.Count);
        Assert.DoesNotContain(deltas, d => d.De == Utc("2026-09-27T13:35:00Z"));   // AUCUN Δ 13:35 → 13:40 : le reset est entre les deux

        var avant = deltas.Where(d => d.A <= Utc("2026-09-27T13:35:00Z")).ToList();
        Assert.Equal(7, avant.Count);
        Assert.All(avant, d => Assert.Equal(Utc(A), d.ResetsAt));
        var anormal = Assert.Single(avant, d => d.Anormal);
        Assert.Equal(Utc("2026-09-27T13:30:00Z"), anormal.De);
        Assert.Equal(Utc("2026-09-27T13:35:00Z"), anormal.A);
        Assert.Equal(-0.01, anormal.Delta, 9);

        var apres = deltas.Where(d => d.De >= Utc("2026-09-27T13:40:00Z")).ToList();
        Assert.Equal(3, apres.Count);
        Assert.All(apres, d => Assert.Equal(Utc(B), d.ResetsAt));
        Assert.All(apres, d => Assert.False(d.Anormal));

        Assert.Equal(0.09, deltas.Where(d => d.Delta > 0).Sum(d => d.Delta), 9);   // 6 × 0.01 + (0.01 + 0.00 + 0.02)
        Assert.All(deltas, d => Assert.Equal(WindowKind.FiveHour, d.Fenetre));
    }

    [Fact]
    public void Les_deltas_ne_traversent_jamais_un_trou()
    {
        var analyse = AnalyseReleves.Analyser(Fixture("trou-arrete", "2026-09-27T10:00:00Z", "2026-09-27T11:20:00Z"), Utc("2026-09-27T11:20:00Z"), Cadence);

        var paires = analyse.Deltas5h.Select(d => (d.De, d.A)).ToList();
        Assert.Equal(new[]
        {
            (Utc("2026-09-27T10:00:00Z"), Utc("2026-09-27T10:05:00Z")),
            (Utc("2026-09-27T10:05:00Z"), Utc("2026-09-27T10:10:00Z")),
            (Utc("2026-09-27T11:03:00Z"), Utc("2026-09-27T11:08:00Z")),
            (Utc("2026-09-27T11:08:00Z"), Utc("2026-09-27T11:13:00Z")),
            (Utc("2026-09-27T11:13:00Z"), Utc("2026-09-27T11:18:00Z")),
        }, paires);
        Assert.DoesNotContain(analyse.Deltas5h, d => d.De == Utc("2026-09-27T10:10:00Z"));
        Assert.DoesNotContain(analyse.DeltasHebdo, d => d.De == Utc("2026-09-27T10:10:00Z"));
        Assert.All(analyse.Deltas5h, d => Assert.False(d.Anormal));
    }

    // --- Sauts non localisés ---

    [Fact]
    public void Le_saut_de_part_et_d_autre_d_un_trou_est_non_localise()
    {
        var arrete = AnalyseReleves.Analyser(Fixture("trou-arrete", "2026-09-27T10:00:00Z", "2026-09-27T11:20:00Z"), Utc("2026-09-27T11:20:00Z"), Cadence);

        Assert.Equal(2, arrete.Sauts.Count);
        var cinq = Assert.Single(arrete.Sauts, s => s.Fenetre == WindowKind.FiveHour);
        Assert.Equal(0.14, cinq.Avant);
        Assert.Equal(0.20, cinq.Apres);
        Assert.NotNull(cinq.Delta);
        Assert.Equal(0.06, cinq.Delta!.Value, 9);
        Assert.Equal(arrete.Trous[0], cinq.Trou);

        var hebdo = Assert.Single(arrete.Sauts, s => s.Fenetre == WindowKind.SevenDay);
        Assert.Equal(0.41, hebdo.Avant);
        Assert.Equal(0.43, hebdo.Apres);
        Assert.Equal(0.02, hebdo.Delta!.Value, 9);

        var jeton = AnalyseReleves.Analyser(Fixture("trou-jeton", "2026-09-27T14:00:00Z", "2026-09-27T15:20:00Z"), Utc("2026-09-27T15:20:00Z"), Cadence);
        var sautJeton = Assert.Single(jeton.Sauts, s => s.Fenetre == WindowKind.FiveHour);
        Assert.Equal(0.0, sautJeton.Delta!.Value, 9);   // rien consommé pendant l'absence — et on ne sait toujours pas quand
    }

    [Fact]
    public void Un_reset_pendant_le_trou_rend_le_saut_indeterminable()
    {
        var lecture = EnMemoire(new[] { R("2026-09-27T10:00:00Z", 0.9, A), R("2026-09-27T11:00:00Z", 0.1, B) });

        var analyse = AnalyseReleves.Analyser(lecture, Utc("2026-09-27T11:05:00Z"), Cadence);

        Assert.Single(analyse.Trous);
        var saut = Assert.Single(analyse.Sauts);
        Assert.Equal(WindowKind.FiveHour, saut.Fenetre);
        Assert.Equal(0.9, saut.Avant);
        Assert.Equal(0.1, saut.Apres);
        Assert.Null(saut.Delta);   // au moins un reset dans l'absence : indéterminable, dit comme tel

        var reset = Assert.Single(analyse.Resets5h);
        Assert.Equal(Utc(A), reset.Instant);
        Assert.Equal(Utc("2026-09-27T11:00:00Z"), reset.ObserveA);
        Assert.Empty(analyse.Deltas5h);   // ni à travers le trou, ni à travers le reset
    }

    [Fact]
    public void Deux_resets_hebdo_sur_trois_semaines()
    {
        var lecture = Fixture("deux-resets-hebdo", "2026-09-14T00:00:00Z", "2026-10-07T00:00:00Z");
        Assert.Equal(23, lecture.Releves.Count);
        var now = Utc("2026-10-06T13:00:00Z");

        // Un relevé par jour : à la cadence d'un jour, aucun trou, et les Δ hebdo se calculent.
        var analyse = AnalyseReleves.Analyser(lecture, now, TimeSpan.FromDays(1));

        Assert.Empty(analyse.Trous);
        Assert.Equal(new[] { Utc("2026-09-18T22:00:00Z"), Utc("2026-09-25T22:00:00Z"), Utc("2026-10-02T22:00:00Z") },
                     analyse.ResetsHebdo.Select(r => r.Instant));
        Assert.All(analyse.ResetsHebdo, r => Assert.Equal(WindowKind.SevenDay, r.Fenetre));
        Assert.Equal(new[] { Utc("2026-09-19T12:00:00Z"), Utc("2026-09-26T12:00:00Z"), Utc("2026-10-03T12:00:00Z") },
                     analyse.ResetsHebdo.Select(r => r.ObserveA));

        Assert.Equal(19, analyse.DeltasHebdo.Count);   // 22 paires consécutives − 3 qui traversent un reset
        Assert.All(analyse.DeltasHebdo, d => Assert.False(d.Anormal));
        Assert.All(analyse.DeltasHebdo, d => Assert.Equal(0.05, d.Delta, 9));
        foreach (var reset in analyse.ResetsHebdo)
            Assert.DoesNotContain(analyse.DeltasHebdo, d => d.De < reset.ObserveA && reset.ObserveA <= d.A);

        // À la cadence de la sonde, chaque journée sans relevé est un trou : aucun Δ ne le traverse.
        var cadenceSonde = AnalyseReleves.Analyser(lecture, now, Cadence);
        Assert.Equal(22, cadenceSonde.Trous.Count(t => t.Fin is not null));
        Assert.Empty(cadenceSonde.DeltasHebdo);
        Assert.Equal(3, cadenceSonde.ResetsHebdo.Count);   // les resets restent observés à travers les trous
    }
}
