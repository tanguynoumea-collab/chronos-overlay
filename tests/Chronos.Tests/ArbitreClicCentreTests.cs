using Chronos.ViewModels;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// ACC-02 / D-35-06 — l'arbitre PUR du clic au centre du cadran. Temporisation, pas annulation : un simple clic
/// bascule à l'échéance du délai de double-clic et pas avant ; un double-clic ouvre l'Historique et ne bascule
/// JAMAIS. Horloge injectée par les appels : aucune minuterie, aucune attente réelle.
/// </summary>
public class ArbitreClicCentreTests
{
    private static readonly TimeSpan Delai = TimeSpan.FromMilliseconds(500);
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Ms(int ms) => T0 + TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Un_simple_clic_bascule_a_l_echeance_et_pas_avant()
    {
        var a = new ArbitreClicCentre(Delai);

        Assert.Equal(ActionClicCentre.Rien, a.Clic(1, T0));
        Assert.True(a.EnAttente);

        Assert.Equal(ActionClicCentre.Rien, a.Echeance(Ms(499)));
        Assert.True(a.EnAttente);
        Assert.Equal(TimeSpan.FromMilliseconds(1), a.Restant(Ms(499)));   // minuterie un peu en avance : on réarme

        Assert.Equal(ActionClicCentre.Basculer, a.Echeance(Ms(500)));
        Assert.False(a.EnAttente);
        Assert.Equal(TimeSpan.Zero, a.Restant(Ms(500)));

        Assert.Equal(ActionClicCentre.Rien, a.Echeance(Ms(600)));        // une seule bascule
    }

    [Fact]
    public void Un_double_clic_ouvre_sans_aucune_bascule()
    {
        var a = new ArbitreClicCentre(Delai);

        Assert.Equal(ActionClicCentre.Rien, a.Clic(1, T0));
        Assert.Equal(ActionClicCentre.OuvrirHistorique, a.Clic(2, Ms(200)));
        Assert.False(a.EnAttente);

        Assert.Equal(ActionClicCentre.Rien, a.Echeance(Ms(500)));
        Assert.Equal(ActionClicCentre.Rien, a.Echeance(Ms(5000)));
    }

    [Fact]
    public void Deux_clics_lents_font_deux_bascules()
    {
        var a = new ArbitreClicCentre(Delai);

        Assert.Equal(ActionClicCentre.Rien, a.Clic(1, T0));
        Assert.Equal(ActionClicCentre.Basculer, a.Echeance(Ms(500)));

        Assert.Equal(ActionClicCentre.Rien, a.Clic(1, Ms(1000)));
        Assert.Equal(ActionClicCentre.Basculer, a.Echeance(Ms(1500)));
    }

    [Fact]
    public void Un_triple_clic_ouvre_une_fois_et_ne_bascule_pas()
    {
        var a = new ArbitreClicCentre(Delai);

        Assert.Equal(ActionClicCentre.Rien, a.Clic(1, T0));
        Assert.Equal(ActionClicCentre.OuvrirHistorique, a.Clic(2, Ms(150)));
        Assert.Equal(ActionClicCentre.OuvrirHistorique, a.Clic(3, Ms(300)));   // idempotent côté ouvreur (singleton)

        Assert.Equal(ActionClicCentre.Rien, a.Echeance(Ms(800)));
        Assert.Equal(ActionClicCentre.Rien, a.Echeance(Ms(5000)));
    }

    [Fact]
    public void Un_delai_nul_ou_negatif_est_refuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArbitreClicCentre(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArbitreClicCentre(TimeSpan.FromMilliseconds(-1)));
    }
}
