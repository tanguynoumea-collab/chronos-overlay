using Chronos.Rendering;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la math PURE de la timeline 24 h (JOUR-01/02) : fraction du jour local et projection des
/// resets 5 h sur l'axe des 24 h. Tests [Fact] déterministes — chaque cas fournit un DateTimeOffset
/// avec offset EXPLICITE (+02:00) : la math lit le .TimeOfDay/.Date du VALUE, donc indépendante du
/// fuseau de la machine de CI. 0° = minuit au sommet (cohérent ArcGeometry : 0° = 12 h horaire).
/// </summary>
public class DayTimelineTests
{
    // ---- Fraction : minutes-depuis-minuit-local / 1440 ----

    [Fact]
    public void DayTimeline_minuit_rend_zero()
    {
        var minuit = new DateTimeOffset(2026, 7, 9, 0, 0, 0, TimeSpan.FromHours(2));
        Assert.Equal(0.0, DayTimeline.Fraction(minuit), 9);
    }

    [Fact]
    public void DayTimeline_18h_rend_075()
    {
        var dixHuit = new DateTimeOffset(2026, 7, 9, 18, 0, 0, TimeSpan.FromHours(2));
        Assert.Equal(0.75, DayTimeline.Fraction(dixHuit), 9); // 1080 / 1440 = 0.75
    }

    [Fact]
    public void DayTimeline_borne_sous_1()
    {
        var presqueMinuit = new DateTimeOffset(2026, 7, 9, 23, 59, 59, TimeSpan.FromHours(2));
        var f = DayTimeline.Fraction(presqueMinuit);
        Assert.True(f < 1.0, "la fraction du jour ne doit jamais atteindre 1 (réservé au minuit suivant)");
        Assert.True(f >= 0.999, "23:59:59 doit être tout proche de 1");
    }

    // ---- ResetAngles : resets 5 h du jour local projetés en angles (h/24 × 360°), triés ----

    [Fact]
    public void DayTicks_null_rend_liste_vide()
    {
        var now = new DateTimeOffset(2026, 7, 9, 18, 0, 0, TimeSpan.FromHours(2));
        Assert.Empty(DayTimeline.ResetAngles(now, null));
    }

    [Fact]
    public void DayTicks_resets_du_jour_projetes()
    {
        var now = new DateTimeOffset(2026, 7, 9, 18, 0, 0, TimeSpan.FromHours(2));
        var reset = new DateTimeOffset(2026, 7, 9, 3, 0, 0, TimeSpan.FromHours(2)); // 03/08/13/18/23 h
        var angles = DayTimeline.ResetAngles(now, reset);
        Assert.Equal(new[] { 45.0, 120.0, 195.0, 270.0, 345.0 }, angles);
    }

    [Fact]
    public void DayTicks_offset_de_phase()
    {
        var now = new DateTimeOffset(2026, 7, 9, 18, 0, 0, TimeSpan.FromHours(2));
        var reset = new DateTimeOffset(2026, 7, 9, 1, 0, 0, TimeSpan.FromHours(2)); // 01/06/11/16/21 h
        var angles = DayTimeline.ResetAngles(now, reset);
        Assert.Equal(new[] { 15.0, 90.0, 165.0, 240.0, 315.0 }, angles);
    }

    [Fact]
    public void DayTicks_reset_milieu_de_jour_rembobine()
    {
        // Le resets_at fourni tombe au MILIEU de la grille du jour (18 h) : on doit remonter au 1er
        // reset du jour (03 h) et énumérer les 5, pas seulement 18/23 h.
        var now = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.FromHours(2));
        var reset = new DateTimeOffset(2026, 7, 9, 18, 0, 0, TimeSpan.FromHours(2));
        var angles = DayTimeline.ResetAngles(now, reset);
        Assert.Equal(new[] { 45.0, 120.0, 195.0, 270.0, 345.0 }, angles);
    }

    [Fact]
    public void DayTicks_reset_autre_jour_normalise()
    {
        // resets_at plusieurs jours avant now : la normalisation par pas de 5 h ramène sur le jour de now.
        // La grille 5 h DÉRIVE d'un jour à l'autre (24 h / 5 h = 4,8 → décalage de +4 h par jour) : de
        // 07-05 03 h, 4 jours = 96 h = 19×5 h + 1 h, la grille du 07-09 tombe donc à 02/07/12/17/22 h.
        // Ce n'est pas un bug : les resets 5 h ne s'alignent PAS sur une grille horaire fixe (honnêteté).
        var now = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.FromHours(2));
        var reset = new DateTimeOffset(2026, 7, 5, 3, 0, 0, TimeSpan.FromHours(2));
        var angles = DayTimeline.ResetAngles(now, reset);
        Assert.Equal(new[] { 30.0, 105.0, 180.0, 255.0, 330.0 }, angles);
    }
}
