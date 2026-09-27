using System.Text.Json;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Preuve PURE du helper de dédup des usages (CPT-01) : aucun disque, aucune fixture. Depuis Claude Code
/// 2.1.260, chaque bloc de contenu d'un message assistant est réécrit sur sa propre ligne, même
/// <c>message.id</c>, avec un <c>output_tokens</c> PARTIEL et croissant (8 → 8 → 256). Sommer ligne par
/// ligne compte ×2,1 ; prendre la première ligne sous-compte la sortie. La règle mesurée (12 835 / 12 835
/// ids divergents le 2026-09-27) est le MAX PAR CHAMP, équivalent à « dernière ligne » mais indépendant
/// de l'ordre de lecture.
/// </summary>
public sealed class DedupUsageTests
{
    private static readonly DateTimeOffset T0 = new(2026, 07, 08, 11, 20, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 07, 08, 11, 25, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 07, 08, 11, 28, 00, TimeSpan.Zero);

    // --- Le cas réel : trois blocs (thinking / text / tool_use), output_tokens 8 / 8 / 256 ---

    [Fact]
    public void Trois_lignes_du_meme_message_id_comptent_une_fois_au_max_par_champ()
    {
        var dedup = new DedupUsage();
        dedup.Ajouter("msg_A", "req_A", T0, 2, 8, 35005, 41741);
        dedup.Ajouter("msg_A", "req_A", T0.AddSeconds(2), 2, 8, 35005, 41741);
        dedup.Ajouter("msg_A", "req_A", T0.AddSeconds(5), 2, 256, 35005, 41741);

        var entrees = dedup.Entrees();

        // 2 + 256 + 35 005 + 41 741 = 77 004 : UN message, le max de chaque champ, PAS 2 + 8 + …
        var e = Assert.Single(entrees);
        Assert.Equal(T0, e.Ts);
        Assert.Equal(77004L, e.Tokens);
    }

    [Fact]
    public void Un_doublon_strict_est_absorbe()
    {
        var dedup = new DedupUsage();
        dedup.Ajouter("msg_A", "req_A", T0, 2, 8, 35005, 41741);
        dedup.Ajouter("msg_A", "req_A", T0, 2, 8, 35005, 41741);

        var e = Assert.Single(dedup.Entrees());
        Assert.Equal(T0, e.Ts);
        Assert.Equal(2L + 8 + 35005 + 41741, e.Tokens);
    }

    [Fact]
    public void Le_max_est_independant_de_l_ordre()
    {
        // Lignes lues dans l'ordre inverse (tri global, fichiers non ordonnés entre eux) : même résultat,
        // et le timestamp retenu est le PLUS PETIT vu, pas celui du premier appel.
        var dedup = new DedupUsage();
        dedup.Ajouter("msg_A", "req_A", T0.AddSeconds(5), 2, 256, 35005, 41741);
        dedup.Ajouter("msg_A", "req_A", T0.AddSeconds(2), 2, 8, 35005, 41741);
        dedup.Ajouter("msg_A", "req_A", T0, 2, 8, 35005, 41741);

        var e = Assert.Single(dedup.Entrees());
        Assert.Equal(T0, e.Ts);
        Assert.Equal(77004L, e.Tokens);
    }

    // --- Repli requestId : un filet, pas un chemin nominal (0 ligne sans message.id mesurée) ---

    [Fact]
    public void Sans_message_id_le_requestId_sert_de_cle()
    {
        var dedup = new DedupUsage();
        dedup.Ajouter(null, "req_B", T1, 10, 20, 0, 0);
        dedup.Ajouter(null, "req_B", T1.AddSeconds(3), 10, 40, 0, 0);

        var e = Assert.Single(dedup.Entrees());
        Assert.Equal(T1, e.Ts);
        Assert.Equal(50L, e.Tokens);
    }

    // --- Jamais jeter un token connu : sans aucun identifiant, chaque ligne compte telle quelle ---

    [Fact]
    public void Sans_aucun_identifiant_la_ligne_est_comptee_telle_quelle()
    {
        var dedup = new DedupUsage();
        dedup.Ajouter(null, null, T2, 1, 1, 0, 0);
        dedup.Ajouter(null, null, T2, 1, 1, 0, 0);

        var entrees = dedup.Entrees();

        Assert.Equal(2, entrees.Count);
        Assert.All(entrees, e => Assert.Equal(2L, e.Tokens));
        Assert.All(entrees, e => Assert.Equal(T2, e.Ts));
    }

    [Fact]
    public void Un_message_id_et_un_requestId_differents_sont_deux_messages()
    {
        // La clé est message.id ?? requestId : "msg_A" et "req_A" sont deux clés distinctes.
        var dedup = new DedupUsage();
        dedup.Ajouter("msg_A", null, T0, 1, 2, 3, 4);
        dedup.Ajouter(null, "req_A", T1, 5, 6, 7, 8);

        var entrees = dedup.Entrees();

        Assert.Equal(2, entrees.Count);
        Assert.Contains(entrees, e => e.Ts == T0 && e.Tokens == 10);
        Assert.Contains(entrees, e => e.Ts == T1 && e.Tokens == 26);
    }

    // --- Lecture JSON : les quatre compteurs et les deux identifiants, tolérante, ne lève jamais ---

    [Fact]
    public void LireUsage_lit_les_quatre_champs_et_les_deux_identifiants_et_rend_zero_pour_les_champs_absents()
    {
        using var complet = JsonDocument.Parse(
            """{"type":"assistant","requestId":"req_X","message":{"id":"msg_X","role":"assistant","usage":{"input_tokens":3,"output_tokens":4}}}""");
        DedupUsage.LireUsage(complet.RootElement, out var messageId, out var requestId,
                             out var input, out var output, out var cacheW, out var cacheR);
        Assert.Equal("msg_X", messageId);
        Assert.Equal("req_X", requestId);
        Assert.Equal(3L, input);
        Assert.Equal(4L, output);
        Assert.Equal(0L, cacheW);
        Assert.Equal(0L, cacheR);

        using var vide = JsonDocument.Parse("""{"message":{}}""");
        DedupUsage.LireUsage(vide.RootElement, out messageId, out requestId,
                             out input, out output, out cacheW, out cacheR);
        Assert.Null(messageId);
        Assert.Null(requestId);
        Assert.Equal(0L, input + output + cacheW + cacheR);

        // usage non-objet (chaîne) : tout à zéro, aucune exception.
        using var nonObjet = JsonDocument.Parse("""{"requestId":7,"message":{"id":12,"usage":"n/a"}}""");
        DedupUsage.LireUsage(nonObjet.RootElement, out messageId, out requestId,
                             out input, out output, out cacheW, out cacheR);
        Assert.Null(messageId);
        Assert.Null(requestId);
        Assert.Equal(0L, input + output + cacheW + cacheR);
    }
}
