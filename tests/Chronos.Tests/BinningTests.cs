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
/// HIS-02 / HIS-07 — le BINNING pur de la piste Rythme et la réduction min/max par colonne de pixels.
///
/// Ce que ces tests gravent : le Rythme somme les Δ 5 h d'<c>AnalyseReleves</c> par case d'UNE heure depuis <c>Plage.Debut</c>
/// (25 cases le 25/10/2026), POSITIFS seulement — un Δ <c>Anormal</c> est une anomalie à compter au diagnostic, pas une
/// consommation à dessiner (D-34-10) ; un Δ est rangé dans la case de son instant d'ARRIVÉE ; <c>NiveauAtteint</c> est le
/// max du sélecteur des relevés de la case ; la réduction min/max ne s'applique qu'au-delà de 4 000 points et conserve
/// les pics (D-34-11).
/// </summary>
public class BinningTests
{
    private static readonly TimeSpan Cadence = RateLimitHeaderUsageProvider.CadenceNominale;
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();
    private const string R5 = "2026-09-27T14:50:00Z";

    private static string TestDataDir(string cas, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "journal", cas);

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static ReleveJournal R(string t, double u5)
        => new(Utc(t), SourceUsage.SondeEnTetes, u5, Utc(R5), StatutServeur.Autorise, null, null, null, null, null);

    private static DeltaConsommation D(string de, string a, double delta)
        => new(WindowKind.FiveHour, Utc(de), Utc(a), delta, Utc(R5), Anormal: delta < 0);

    [Fact]
    public void Les_deltas_5h_se_somment_par_heure_sans_les_anormaux()
    {
        // reset-5h-milieu-heure, comme dans AnalyseRelevesTests : 10 Δ, dont un Anormal −0,01 à 13:35 ; somme des positifs 0,09.
        var a = AnalyseReleves.Analyser(
            LecteurJournal.Lire(TestDataDir("reset-5h-milieu-heure"), Utc("2026-09-27T13:00:00Z"), Utc("2026-09-27T14:00:00Z")),
            Utc("2026-09-27T14:00:00Z"), Cadence);
        Assert.Contains(a.Deltas5h, d => d.Anormal);

        var barres = Binning.DeltasParHeure(a.Deltas5h, a.Serie, r => r.U5, a.Plage);

        Assert.Equal(0.09, barres.Sum(b => b.Delta), 9);   // les positifs seulement : le −0,01 n'est pas soustrait
        Assert.All(barres, b => Assert.True(b.Delta >= 0));
        Assert.All(barres, b => Assert.Equal(TimeSpan.FromHours(1), b.Fin - b.Debut));
        Assert.All(barres, b => Assert.Equal(0, (b.Debut - a.Plage.Debut).Ticks % TimeSpan.TicksPerHour));
        var seule = Assert.Single(barres);
        Assert.Equal(a.Plage.Debut, seule.Debut);
        Assert.Equal(0.86, seule.NiveauAtteint);   // le max U5 de l'heure (13:30), avant le reset
    }

    [Fact]
    public void Un_delta_est_range_dans_la_case_de_son_instant_d_arrivee()
    {
        var plage = new Plage(Utc("2026-09-27T09:00:00Z"), Utc("2026-09-27T13:00:00Z"));
        var deltas = new[]
        {
            D("2026-09-27T09:58:00Z", "2026-09-27T10:03:00Z", 0.02),   // arrive à 10:03 → case [10:00, 11:00[ (pas 09:00)
            D("2026-09-27T10:55:00Z", "2026-09-27T11:00:00Z", 0.01),   // arrive à 11:00 pile → case [11:00, 12:00[
        };
        var serie = new[] { R("2026-09-27T10:03:00Z", 0.30), R("2026-09-27T10:30:00Z", 0.35), R("2026-09-27T10:55:00Z", 0.33) };

        var barres = Binning.DeltasParHeure(deltas, serie, r => r.U5, plage);

        Assert.Equal(2, barres.Count);   // les cases sans Δ (09:00, 12:00) sont omises
        Assert.Equal(Utc("2026-09-27T10:00:00Z"), barres[0].Debut);
        Assert.Equal(Utc("2026-09-27T11:00:00Z"), barres[0].Fin);
        Assert.Equal(0.02, barres[0].Delta, 9);
        Assert.Equal(0.35, barres[0].NiveauAtteint);   // max du sélecteur des relevés de la case
        Assert.Equal(Utc("2026-09-27T11:00:00Z"), barres[1].Debut);
        Assert.Equal(0.01, barres[1].Delta, 9);
        Assert.Equal(0.0, barres[1].NiveauAtteint);    // aucun relevé dans la case → 0
        Assert.DoesNotContain(barres, b => b.Debut == Utc("2026-09-27T09:00:00Z"));
    }

    [Fact]
    public void Le_25_octobre_a_vingt_cinq_cases_possibles()
    {
        var plage = BornesPlage.Jour(Utc("2026-10-25T12:00:00Z"), Tz);
        Assert.Equal(TimeSpan.FromHours(25), plage.Duree);

        var deltas = Enumerable.Range(0, 25)
            .Select(k => plage.Debut + TimeSpan.FromHours(k) + TimeSpan.FromMinutes(30))
            .Select(a => new DeltaConsommation(WindowKind.FiveHour, a - TimeSpan.FromMinutes(5), a, 0.01, Utc(R5), Anormal: false))
            .ToList();

        var barres = Binning.DeltasParHeure(deltas, Array.Empty<ReleveJournal>(), r => r.U5, plage);

        Assert.Equal(25, barres.Count);
        Assert.Equal(plage.Debut + TimeSpan.FromHours(24), barres[24].Debut);
        Assert.Equal(plage.Fin, barres[24].Fin);
        Assert.All(barres, b => Assert.Equal(0.01, b.Delta, 9));
    }

    [Fact]
    public void La_reduction_min_max_garde_les_extremes_par_colonne()
    {
        var pts = Enumerable.Range(0, 4001).Select(i => ((double)i, i == 2000 ? 9.0 : Math.Sin(i / 7.0))).ToList();
        Assert.True(Binning.DoitReduire(4001));

        var r = Binning.ReductionMinMax(pts, 800);

        Assert.True(r.Count > 0 && r.Count <= 800, $"{r.Count} colonnes");
        Assert.Equal(9.0, r.Max(p => p.Max));                          // le pic isolé survit à la réduction
        Assert.Equal(pts.Min(p => p.Item2), r.Min(p => p.Min));
        Assert.All(r, p => Assert.True(p.Min <= p.Max));
        for (var i = 1; i < r.Count; i++) Assert.True(r[i - 1].X < r[i].X, "les X de sortie sont croissants");
        Assert.True(r[0].X >= 0 && r[^1].X <= 4000);

        Assert.Empty(Binning.ReductionMinMax(pts, 0));
        Assert.Empty(Binning.ReductionMinMax(Array.Empty<(double X, double Y)>(), 800));
    }

    [Fact]
    public void Le_seuil_de_reduction_est_quatre_mille()
    {
        Assert.Equal(4000, Binning.SeuilReduction);
        Assert.False(Binning.DoitReduire(4000));
        Assert.True(Binning.DoitReduire(4001));
        Assert.False(Binning.DoitReduire(2016));   // une semaine à la cadence de la sonde : jamais réduite
    }
}
