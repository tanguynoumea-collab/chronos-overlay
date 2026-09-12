using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve le recalibrage hebdo pur (ROB-03) et sa règle d'honnêteté (Pitfall 7) : garde Exact,
/// synthèse d'un reset futur sur le repli, et badge « estimée » CONSERVÉ après recalibrage.
/// Tests PURS : now passé en paramètre, aucun type WPF, aucun I/O.
/// </summary>
public class WeeklyRecalibrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 08, 12, 00, 00, TimeSpan.Zero);
    // Ancre 4 jours avant now → le prochain reset est 3 jours après now.
    private static readonly DateTimeOffset Anchor = Now - TimeSpan.FromDays(4);

    private static WindowState Weekly(SourceReliability rel, DateTimeOffset? resets) => new()
    {
        Kind = WindowKind.SevenDay,
        Reliability = rel,
        ResetsAt = resets,
    };

    [Fact]
    public void Exact_avec_reset_reste_inchange()
    {
        var exact = Weekly(SourceReliability.Exact, Now + TimeSpan.FromDays(2));

        var result = WeeklyRecalibration.Apply(exact, Anchor, Now);

        // Les chiffres exacts priment : la MÊME instance est renvoyée, rien n'est touché.
        Assert.Same(exact, result);
    }

    [Fact]
    public void Repli_sans_ancre_reste_inchange()
    {
        var repli = Weekly(SourceReliability.Estimated, null);

        var result = WeeklyRecalibration.Apply(repli, anchor: null, Now);

        Assert.Equal(repli, result); // pas d'ancre → on n'invente aucune date
        Assert.Null(result.ResetsAt);
    }

    [Fact]
    public void Repli_avec_ancre_synthetise_un_reset_futur_aligne()
    {
        var repli = Weekly(SourceReliability.Estimated, null);

        var result = WeeklyRecalibration.Apply(repli, Anchor, Now);

        Assert.NotNull(result.ResetsAt);
        Assert.True(result.ResetsAt > Now); // strictement futur
        // Aligné sur l'ancre + n×7j : ici ancre + 7j = 3 jours après now.
        Assert.Equal(Anchor + TimeSpan.FromDays(7), result.ResetsAt);
    }

    [Fact]
    public void Recalibre_reste_Estimated_badge_conserve()
    {
        var repli = Weekly(SourceReliability.Estimated, null);

        var result = WeeklyRecalibration.Apply(repli, Anchor, Now);

        // Core Value : le recalibrage ne « ment » pas — la provenance reste estimée.
        Assert.Equal(SourceReliability.Estimated, result.Reliability);
    }

    [Fact]
    public void Repli_ancre_FUTURE_dans_la_semaine_est_le_reset_lui_meme_sans_forcer_un_cycle()
    {
        // L'utilisateur saisit naturellement la PROCHAINE date de reset (future) vue dans /usage.
        // Bug corrigé : l'ancienne logique forçait « +1 semaine » et projetait le reset trop loin
        // (> 7 j restants → arc saturé à plein). Ici l'ancre à +2 j EST le prochain reset.
        var repli = Weekly(SourceReliability.Estimated, null);
        var ancreFuture = Now + TimeSpan.FromDays(2);

        var result = WeeklyRecalibration.Apply(repli, ancreFuture, Now);

        Assert.Equal(ancreFuture, result.ResetsAt);                 // le reset = l'ancre, pas +7j
        Assert.True(result.ResetsAt - Now <= TimeSpan.FromDays(7)); // jamais plus d'une semaine restante
    }

    [Fact]
    public void Repli_ancre_future_lointaine_est_ramenee_au_prochain_cycle_dans_la_semaine()
    {
        var repli = Weekly(SourceReliability.Estimated, null);
        var ancreLoin = Now + TimeSpan.FromDays(20); // ancre future > 1 semaine

        var result = WeeklyRecalibration.Apply(repli, ancreLoin, Now);

        Assert.NotNull(result.ResetsAt);
        Assert.True(result.ResetsAt > Now);
        Assert.True(result.ResetsAt - Now <= TimeSpan.FromDays(7)); // ramenée dans ]now ; now+7j]
        Assert.Equal(ancreLoin - TimeSpan.FromDays(14), result.ResetsAt); // 20 - 2×7 = 6 j après now
    }

    [Fact]
    public void Repli_ancre_tres_ancienne_choisit_le_prochain_cycle_strictement_futur()
    {
        var repli = Weekly(SourceReliability.Estimated, null);
        var vieilleAncre = Now - TimeSpan.FromDays(30); // ~4.28 cycles

        var result = WeeklyRecalibration.Apply(repli, vieilleAncre, Now);

        Assert.NotNull(result.ResetsAt);
        Assert.True(result.ResetsAt > Now);
        // ceil(30/7)=5 → ancre + 35j.
        Assert.Equal(vieilleAncre + TimeSpan.FromDays(35), result.ResetsAt);
    }

    /// <summary>
    /// Phase 19 — une fenêtre hebdo passée en PLANCHER conserve le resets_at que le serveur avait donné.
    /// Avec l'ancienne garde (exacte ET datée), elle serait tombée dans le chemin de synthèse et son
    /// reset réel aurait été remplacé par « ancre + n semaines » : une régression silencieuse du compte
    /// à rebours, précisément sur les fenêtres que cette phase fait exister.
    ///
    /// Le reset du serveur est à +5 j et NON à +3 j : l'ancre de cette classe synthétise justement
    /// « now + 3 j », donc un reset à +3 j rendrait ce test vert même avec l'ancienne garde — il ne
    /// garderait rien. Une garde qui ne peut pas échouer ne garde rien (précédent 18-01).
    /// </summary>
    [Fact]
    public void Un_plancher_portant_un_reset_reel_n_est_PAS_recalibre()
    {
        var reelDuServeur = Now + TimeSpan.FromDays(5);
        var plancher = Weekly(SourceReliability.Estimated, reelDuServeur);

        var result = WeeklyRecalibration.Apply(plancher, Anchor, Now);

        Assert.Equal(reelDuServeur, result.ResetsAt);                        // le fait du serveur survit
        Assert.Equal(SourceReliability.Estimated, result.Reliability);
        Assert.Equal(plancher, result);                                      // strictement inchangée
    }

    /// <summary>
    /// La contre-épreuve : le chemin de synthèse reste vivant pour une fenêtre SANS reset, et il recevra
    /// plus de trafic après la phase 19 (les fenêtres démotées n'en portent pas toujours un). La
    /// fiabilité est conservée : le recalibrage donne une date, il ne prétend pas donner un chiffre.
    /// </summary>
    [Fact]
    public void Une_fenetre_indisponible_sans_reset_reste_recalibrable_en_conservant_sa_fiabilite()
    {
        var indisponible = Weekly(SourceReliability.Unavailable, null);

        var result = WeeklyRecalibration.Apply(indisponible, Anchor, Now);

        Assert.NotNull(result.ResetsAt);
        Assert.True(result.ResetsAt > Now);
        Assert.Equal(SourceReliability.Unavailable, result.Reliability);
    }
}
