using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la contrainte de COÛT de la phase 19 : une passe de transcripts coûte 2,7 à 3,2 s et lit
/// 536 Mo sur la machine cible. Au tick de 60 s, une lecture inconditionnelle est exclue — le
/// mémoïseur existe pour cela, et le compteur de passes du faux est l'instrument de la preuve.
///
/// Prouve aussi les deux décisions d'HONNÊTETÉ du décorateur : la validité se mesure sur l'instant de
/// la PASSE DISQUE (un journal né vieux expire vite) et une panne de la source n'efface pas ce qu'on
/// savait, sans pour autant inventer un journal quand on ne savait rien.
///
/// Tests PURS (aucun type WPF, aucune E/S réelle) → [Fact] classiques, horloge injectée.
/// </summary>
public class SourceActiviteMemoiseeTests
{
    private static readonly DateTimeOffset Depart = new(2026, 09, 12, 12, 0, 0, TimeSpan.Zero);

    // Journal vide daté de « now » : seul son instant de passe disque compte pour la péremption.
    private static TranscriptActivityLog Journal(DateTimeOffset now)
        => new(now, now - TimeSpan.FromDays(8), Array.Empty<(DateTimeOffset, long)>());

    [Fact]
    public async Task Deux_lectures_rapprochees_ne_paient_QU_UNE_passe_disque()
    {
        var horloge = new FakeClock(Depart);
        var faux = new FakeTranscriptActivitySource { Journal = Journal(Depart) };
        var memo = new SourceActiviteMemoisee(faux, horloge, TimeSpan.FromSeconds(60));

        var j1 = await memo.ReadAsync();
        var j2 = await memo.ReadAsync();

        Assert.Equal(1, faux.Lectures);   // 536 Mo lus UNE fois, pas deux
        Assert.Same(j1, j2);              // le journal est PUR : le réutiliser est sans perte
    }

    [Fact]
    public async Task Une_lecture_apres_peremption_repaie_une_passe()
    {
        var horloge = new FakeClock(Depart);
        var faux = new FakeTranscriptActivitySource { Journal = Journal(Depart) };
        var memo = new SourceActiviteMemoisee(faux, horloge, TimeSpan.FromSeconds(60));

        await memo.ReadAsync();

        horloge.UtcNow = Depart.AddSeconds(61);           // au-delà de la validité
        faux.Journal = Journal(horloge.UtcNow);
        var frais = await memo.ReadAsync();

        Assert.Equal(2, faux.Lectures);
        Assert.Equal(Depart.AddSeconds(61), frais.Now);
    }

    [Fact]
    public async Task La_validite_se_mesure_sur_l_instant_de_la_passe_disque()
    {
        var horloge = new FakeClock(Depart);
        // Journal NÉ VIEUX : sa passe disque date de 120 s, alors qu'on vient tout juste de l'obtenir.
        var faux = new FakeTranscriptActivitySource { Journal = Journal(Depart.AddSeconds(-120)) };
        var memo = new SourceActiviteMemoisee(faux, horloge, TimeSpan.FromSeconds(60));

        await memo.ReadAsync();
        await memo.ReadAsync();   // l'horloge n'a PAS bougé, et pourtant il faut repayer

        Assert.Equal(2, faux.Lectures);
    }

    [Fact]
    public async Task Une_panne_de_la_source_rend_le_dernier_journal_connu()
    {
        var horloge = new FakeClock(Depart);
        var faux = new FakeTranscriptActivitySource { Journal = Journal(Depart) };
        var memo = new SourceActiviteMemoisee(faux, horloge, TimeSpan.FromSeconds(60));

        var connu = await memo.ReadAsync();

        faux.Journal = null;                      // le disque tombe
        horloge.UtcNow = Depart.AddSeconds(61);   // et la validité expire

        var servi = await memo.ReadAsync();       // aucune exception : on sait encore quelque chose

        Assert.Same(connu, servi);
        Assert.Equal(2, faux.Lectures);           // la tentative A BIEN eu lieu
    }

    [Fact]
    public async Task Une_panne_des_la_premiere_lecture_remonte_a_l_appelant()
    {
        var horloge = new FakeClock(Depart);
        var faux = new FakeTranscriptActivitySource { Journal = null };
        var memo = new SourceActiviteMemoisee(faux, horloge, TimeSpan.FromSeconds(60));

        // Rien de connu : le décorateur n'invente PAS un journal vide — un journal vide se lirait
        // « aucune activité », ce qui est une AFFIRMATION. C'est l'appelant (plan 19-03) qui
        // convertira cela en branche « indisponible ».
        await Assert.ThrowsAsync<IOException>(() => memo.ReadAsync());
    }

    [Fact]
    public void La_validite_par_defaut_est_alignee_sur_le_tick_nominal()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), SourceActiviteMemoisee.ValiditeParDefaut);
    }
}
