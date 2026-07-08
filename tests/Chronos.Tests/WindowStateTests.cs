using Chronos.Models;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve DAT-03 (modèles immuables nullable-safe : Exhausted, Unavailable, Empty)
/// et DAT-07 (FractionRemaining clampé [0..1], null si reset inconnu ou fenêtre non positive).
/// Tests PURS : aucun IClock, `now` passé en paramètre direct → [Fact]/[Theory] classiques.
/// </summary>
public class WindowStateTests
{
    // --- DAT-03 : Exhausted dérivé de Utilization >= 1 (null = inconnu != épuisé) ---

    [Fact]
    public void Exhausted_utilization_pleine_est_vrai()
    {
        var w = new WindowState { Kind = WindowKind.FiveHour, Utilization = 1.0, Reliability = SourceReliability.Exact };
        Assert.True(w.Exhausted);
    }

    [Fact]
    public void Exhausted_utilization_presque_pleine_est_faux()
    {
        var w = new WindowState { Kind = WindowKind.FiveHour, Utilization = 0.99, Reliability = SourceReliability.Exact };
        Assert.False(w.Exhausted);
    }

    [Fact]
    public void Exhausted_utilization_inconnue_est_faux()
    {
        // Inconnu (null) n'est PAS épuisé — honnêteté des chiffres.
        var w = new WindowState { Kind = WindowKind.FiveHour, Utilization = null, Reliability = SourceReliability.Unavailable };
        Assert.False(w.Exhausted);
    }

    // --- DAT-03 : Unavailable neutralise tous les champs et conserve la WindowKind ---

    [Fact]
    public void Unavailable_neutralise_les_champs_et_garde_la_fenetre()
    {
        var w = WindowState.Unavailable(WindowKind.FiveHour);

        Assert.Equal(WindowKind.FiveHour, w.Kind);
        Assert.Equal(SourceReliability.Unavailable, w.Reliability);
        Assert.Null(w.Utilization);
        Assert.Null(w.ResetsAt);
        Assert.Null(w.FractionTimeRemaining);
    }

    // --- DAT-03 : UsageSnapshot.Empty = deux fenêtres Unavailable de la bonne WindowKind ---

    [Fact]
    public void Empty_expose_deux_fenetres_indisponibles_correctes()
    {
        var snap = UsageSnapshot.Empty;

        Assert.Equal(WindowKind.FiveHour, snap.FiveHour.Kind);
        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(WindowKind.SevenDay, snap.SevenDay.Kind);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    // --- DAT-07 : FractionRemaining — clamp [0..1], null si reset inconnu ou fenêtre non positive ---

    public static IEnumerable<object?[]> FractionCases()
    {
        // now fixé, len = 5 h.
        var now = new DateTimeOffset(2026, 07, 08, 12, 00, 00, TimeSpan.Zero);
        var len = TimeSpan.FromHours(5);

        // resetsAt, now, len, attendu
        yield return new object?[] { now + TimeSpan.FromHours(2.5), now, len, 0.5 };  // milieu de fenêtre
        yield return new object?[] { now + TimeSpan.FromHours(10), now, len, 1.0 };   // > fenêtre → clamp haut
        yield return new object?[] { now - TimeSpan.FromHours(1), now, len, 0.0 };    // reset dépassé → clamp bas
        yield return new object?[] { null, now, len, null };                          // reset inconnu → null
        yield return new object?[] { now + TimeSpan.FromHours(2.5), now, TimeSpan.Zero, null }; // fenêtre non positive → null
    }

    [Theory]
    [MemberData(nameof(FractionCases))]
    public void FractionRemaining_clampe_et_gere_l_inconnu(DateTimeOffset? resetsAt, DateTimeOffset now, TimeSpan len, double? attendu)
    {
        var r = WindowState.FractionRemaining(resetsAt, now, len);

        if (attendu is null)
            Assert.Null(r);
        else
        {
            Assert.NotNull(r);
            Assert.Equal(attendu.Value, r!.Value, 9); // tolérance 1e-9
        }
    }
}
