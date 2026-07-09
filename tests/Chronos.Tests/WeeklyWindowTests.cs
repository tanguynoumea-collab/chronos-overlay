using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la borne de début de la fenêtre HEBDO (EST-04) : avec ancre → fenêtre roulante
/// [ancre + k·7j] contenant now (k = floor((now-ancre)/7j)) ; sans ancre → 7 j glissants
/// (comportement v1.0). Tests PURS : now passé en paramètre, aucun I/O, aucun type WPF.
/// now de référence = 2026-07-08T12:00:00Z (cohérent avec l'existant / WeeklyRecalibration).
/// </summary>
public class WeeklyWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 08, 12, 00, 00, TimeSpan.Zero);
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    [Fact]
    public void Sans_ancre_retombe_sur_7j_glissants()
    {
        var result = WeeklyWindow.CurrentStart(anchor: null, Now);

        Assert.Equal(Now - Week, result); // fallback glissant v1.0
    }

    [Fact]
    public void Avec_ancre_10j_avant_recule_dun_cycle()
    {
        var anchor = Now - TimeSpan.FromDays(10); // k = floor(10/7) = 1
        var result = WeeklyWindow.CurrentStart(anchor, Now);

        Assert.Equal(anchor + Week, result);          // ancre + 7 j
        Assert.Equal(Now - TimeSpan.FromDays(3), result); // = now - 3 j
    }

    [Fact]
    public void Avec_ancre_egale_a_now_renvoie_lancre()
    {
        var result = WeeklyWindow.CurrentStart(Now, Now); // k = 0

        Assert.Equal(Now, result);
    }

    [Fact]
    public void Avec_ancre_a_la_frontiere_exacte_7j_renvoie_now()
    {
        var anchor = Now - Week; // k = floor(7/7) = 1 → ancre + 7 j = now
        var result = WeeklyWindow.CurrentStart(anchor, Now);

        Assert.Equal(Now, result); // robustesse à la frontière exacte
    }
}
