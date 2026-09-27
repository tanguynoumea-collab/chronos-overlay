using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-02 — le RÉTICULE : au survol d'un instant, la surcouche montre le relevé le plus proche (« HH:MM · relevé
/// exact / 5 h : N % … », DESIGN_PLAN §2.3). Pure : une série triée par <c>T</c> et un instant ; aucun WPF.
///
/// Ce que ces tests gravent : le relevé retenu est celui de distance minimale (pas « le dernier avant » — au milieu d'un
/// trou, c'est le relevé qui suit qui peut être le plus proche) ; à égalité, le plus ANCIEN ; une série vide rend null,
/// jamais une exception.
/// </summary>
public class ReticuleTests
{
    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static ReleveJournal R(string t)
        => new(Utc(t), SourceUsage.SondeEnTetes, 0.1, Utc("2026-09-27T14:50:00Z"), StatutServeur.Autorise, null, null, null, null, null);

    private static readonly IReadOnlyList<ReleveJournal> Serie = new[]
    {
        R("2026-09-27T10:00:00Z"),
        R("2026-09-27T10:05:00Z"),
        R("2026-09-27T10:10:00Z"),
        R("2026-09-27T11:03:00Z"),   // après un trou 10:10 → 11:03
        R("2026-09-27T11:08:00Z"),
    };

    [Fact]
    public void Le_releve_le_plus_proche_est_celui_de_distance_minimale()
    {
        Assert.Equal(Utc("2026-09-27T10:05:00Z"), Reticule.PlusProche(Serie, Utc("2026-09-27T10:06:00Z"))!.T);
        Assert.Equal(Utc("2026-09-27T11:03:00Z"), Reticule.PlusProche(Serie, Utc("2026-09-27T10:40:00Z"))!.T);   // 23 min < 30 min
        Assert.Equal(Utc("2026-09-27T10:00:00Z"), Reticule.PlusProche(Serie, Utc("2026-09-27T09:00:00Z"))!.T);   // avant le premier
        Assert.Equal(Utc("2026-09-27T11:08:00Z"), Reticule.PlusProche(Serie, Utc("2026-09-27T23:00:00Z"))!.T);   // après le dernier
    }

    [Fact]
    public void A_egalite_le_plus_ancien_gagne()
    {
        Assert.Equal(Utc("2026-09-27T10:00:00Z"), Reticule.PlusProche(Serie, Utc("2026-09-27T10:02:30Z"))!.T);
    }

    [Fact]
    public void Une_serie_vide_rend_null()
    {
        Assert.Null(Reticule.PlusProche(Array.Empty<ReleveJournal>(), Utc("2026-09-27T10:02:30Z")));
    }
}
