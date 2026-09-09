using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Preuve PURE du bornage du journal de delta : AUCUN acces disque, aucune fixture. Le journal est
/// construit directement en memoire, comme le ferait la passe disque.
///
/// Pourquoi cette separation (Pattern 3 du depot) : les deux fenetres 5 h et hebdo ont des bornes
/// basses DIFFERENTES mais doivent repondre depuis un SEUL instantane disque. La logique de bornage
/// est donc pure et interrogeable N fois — donc testable sans fichier, et sans dependre du systeme
/// de fichiers pour prouver des regles arithmetiques.
/// </summary>
public class TranscriptActivityLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 08, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset Horizon = Now - TimeSpan.FromDays(8);

    private static DateTimeOffset At(int heure, int minute = 0)
        => new(2026, 07, 08, heure, minute, 00, TimeSpan.Zero);

    private static TranscriptActivityLog Journal(params (DateTimeOffset Ts, long Tokens)[] entrees)
        => new(Now, Horizon, entrees);

    // --- DEL-02 : somme sur ]since ; Now] ---

    [Fact]
    public void Somme_les_tokens_strictement_posterieurs_a_since()
    {
        var log = Journal((At(8), 100), (At(10), 200), (At(11), 300));

        var delta = log.Since(At(9));

        // 200 + 300 : l'entree de 08:00 est anterieure a la borne basse, donc exclue.
        Assert.Equal(500L, delta.Tokens);
        Assert.True(delta.HasActivity);
    }

    // --- Anti double-comptage : l'entree posee EXACTEMENT sur la borne basse n'est pas comptee ---

    [Fact]
    public void Borne_basse_exclusive_l_entree_posee_sur_since_n_est_pas_comptee()
    {
        var log = Journal((At(10), 200));

        var delta = log.Since(At(10));

        // Ce message a deja ete compte par le serveur au moment de la capture exacte a 10:00.
        Assert.Equal(0L, delta.Tokens);
        Assert.False(delta.HasActivity);
        Assert.Null(delta.LastActivityAt);
    }

    // --- Borne haute inclusive : l'entree posee exactement sur Now appartient a la fenetre ---

    [Fact]
    public void Borne_haute_inclusive_l_entree_posee_sur_now_est_comptee()
    {
        var log = Journal((At(11), 300), (Now, 400));

        var delta = log.Since(At(10));

        Assert.Equal(700L, delta.Tokens);
        Assert.Equal(Now, delta.LastActivityAt);
    }

    // --- LastActivityAt = l'entree la plus recente de la fenetre, quel que soit l'ordre d'insertion ---

    [Fact]
    public void Derniere_activite_est_l_entree_la_plus_recente_de_la_fenetre()
    {
        // Volontairement non trie : le bornage ne doit dependre d'aucune precondition d'ordre.
        var log = Journal((At(11), 300), (At(9, 30), 100), (At(10, 45), 200));

        var delta = log.Since(At(9));

        Assert.Equal(At(11), delta.LastActivityAt);
        Assert.Equal(600L, delta.Tokens);
    }

    // --- Honnetete : le journal declare ce qu'il ne couvre pas, de part et d'autre de l'horizon ---

    [Fact]
    public void Couvre_a_partir_de_l_horizon_et_pas_avant()
    {
        var log = Journal((At(11), 300));

        Assert.False(log.Covers(Horizon - TimeSpan.FromSeconds(1)));   // trop ancien : delta sous-evalue
        Assert.True(log.Covers(Horizon));                              // borne incluse
        Assert.True(log.Covers(Horizon + TimeSpan.FromSeconds(1)));
        Assert.Equal(Now, log.Now);
        Assert.Equal(Horizon, log.Horizon);
    }
}
