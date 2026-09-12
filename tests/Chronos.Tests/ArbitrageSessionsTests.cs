using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// FUS-01 — l'arbitrage entre sources se fait sur la FRAÎCHEUR, jamais sur l'ordre dans lequel le code a
/// enregistré les sources.
///
/// <para>Le relevé du 2026-09-12 est la raison d'être de ces tests. Vérité terrain : le modèle travaillait,
/// le transcript avait été écrit 10 secondes plus tôt. Un fichier de hook figé depuis 7 heures annonçait
/// « à toi », et c'est lui qui gagnait — parce qu'il était lu en second. Un signal de 7 heures battait un
/// signal de 10 secondes.</para>
///
/// <para>Ces tests sont PURS : aucun dossier temporaire, aucune E/S, aucune horloge. Tous les instants
/// dérivent de <c>T</c>. Rien ici ne peut virer au rouge parce qu'on est le soir ou parce qu'un fichier
/// traîne sur la machine — un précédent coûteux de la phase 22.</para>
/// </summary>
public class ArbitrageSessionsTests
{
    /// <summary>L'instant de référence. Fixe : ce fichier ne demande jamais l'heure à personne.</summary>
    private static readonly DateTimeOffset T = new(2026, 9, 12, 13, 28, 0, TimeSpan.Zero);

    private static SignalSession Hook(string id, SessionActivity a, DateTimeOffset maj, string? motif = null)
        => new(SourceSession.Hook, new SessionSnapshot(id, "Proj-" + id, a, motif, maj));

    private static SignalSession Transcript(string id, SessionActivity a, DateTimeOffset maj)
        => new(SourceSession.Transcript, new SessionSnapshot(id, "Proj-" + id, a, null, maj));

    // Le corpus de PERMUTATION : 6 signaux, 4 sessions. Chaque session y illustre un cas de départage
    // différent, sinon la permutation ne prouverait qu'une seule règle.
    private static SignalSession[] Corpus() => new[]
    {
        Hook("s1", SessionActivity.WaitingAttention, T.AddHours(-7), "permission_prompt"),
        Transcript("s1", SessionActivity.Working, T.AddSeconds(-10)),   // la fraîcheur tranche
        Hook("s2", SessionActivity.WaitingAttention, T.AddMinutes(-1)),
        Transcript("s2", SessionActivity.WaitingTurn, T.AddMinutes(-1)),// l'ÉGALITÉ d'âge tranche
        Transcript("s3", SessionActivity.WaitingTurn, T.AddMinutes(-5)),// seule : aucun désaccord
        Hook("s4", SessionActivity.Working, T.AddMinutes(-2)),          // seule : aucun désaccord
    };

    /// <summary>Toutes les permutations de l'entrée — 6 signaux, donc 720 ordres d'arrivée possibles.</summary>
    private static IEnumerable<T[]> Permutations<T>(T[] source)
    {
        if (source.Length <= 1) { yield return source; yield break; }
        for (var i = 0; i < source.Length; i++)
        {
            var tete = source[i];
            var reste = source.Take(i).Concat(source.Skip(i + 1)).ToArray();
            foreach (var suite in Permutations(reste))
                yield return new[] { tete }.Concat(suite).ToArray();
        }
    }

    /// <summary>Une chaîne UNIQUE par résultat : c'est ce qui permet de comparer 720 sorties sans ambiguïté,
    /// états ET désaccords compris — l'ordre des lignes rendues fait partie du résultat.</summary>
    private static string Canonique(ResultatArbitrage r)
        => string.Join(" | ", r.Retenus.Select(s => $"{s.SessionId}={s.Activity}@{s.UpdatedAt:O}"))
         + " || "
         + string.Join(" | ", r.Desaccords.Select(d =>
               $"{d.SessionId}:{d.SourceRetenue}/{d.EtatRetenu}>{d.SourceEcartee}/{d.EtatEcarte}+{d.EcartAge}"));

    [Fact]
    public void Un_hook_de_7_h_perd_contre_un_transcript_de_10_secondes()
    {
        var r = ArbitrageSessions.Trancher(new[]
        {
            Hook("s", SessionActivity.WaitingAttention, T.AddHours(-7)),
            Transcript("s", SessionActivity.Working, T.AddSeconds(-10)),
        });

        var retenu = Assert.Single(r.Retenus);
        Assert.Equal(SessionActivity.Working, retenu.Activity);
        Assert.Equal(SourceSession.Transcript, Assert.Single(r.Desaccords).SourceRetenue);
    }

    [Fact]
    public void Un_signal_perime_de_25_minutes_n_ecrase_plus_un_transcript_frais()
    {
        // Reproduction de la 2e ligne du relevé : le hook, périmé, avait été ramené à « inconnu » — et
        // c'est cet « inconnu » qui s'affichait, à la place d'un « en cours » frais et CORRECT.
        var r = ArbitrageSessions.Trancher(new[]
        {
            Hook("s", SessionActivity.Unknown, T.AddMinutes(-25)),
            Transcript("s", SessionActivity.Working, T.AddSeconds(-10)),
        });

        Assert.Equal(SessionActivity.Working, Assert.Single(r.Retenus).Activity);
    }

    [Fact]
    public void Un_permission_prompt_plus_ancien_ne_bat_pas_un_signal_plus_recent()
    {
        // Le critère n°4 de la phase : la PRÉCISION ne bat pas la FRAÎCHEUR. Un motif de permission est
        // l'information la plus spécifique du système — elle ne vaut rien si elle date.
        var r = ArbitrageSessions.Trancher(new[]
        {
            Hook("s", SessionActivity.WaitingAttention, T.AddMinutes(-1), "permission_prompt"),
            Transcript("s", SessionActivity.Working, T.AddSeconds(-10)),
        });

        var retenu = Assert.Single(r.Retenus);
        Assert.Equal(SessionActivity.Working, retenu.Activity);

        var desaccord = Assert.Single(r.Desaccords);
        Assert.Equal(SourceSession.Transcript, desaccord.SourceRetenue);
        Assert.Equal(SourceSession.Hook, desaccord.SourceEcartee);
        Assert.Equal(SessionActivity.WaitingAttention, desaccord.EtatEcarte);
    }

    [Fact]
    public void A_age_egal_la_source_la_plus_specifique_tranche_et_l_ordre_inverse_donne_le_meme_resultat()
    {
        // À âge ÉGAL — et seulement là — la source la plus spécifique l'emporte : elle seule sait dire
        // « une permission est demandée ». La règle est NOMMÉE et testée dans LES DEUX sens d'entrée.
        var direct = new[]
        {
            Hook("s", SessionActivity.WaitingAttention, T),
            Transcript("s", SessionActivity.WaitingTurn, T),
        };

        var r = ArbitrageSessions.Trancher(direct);
        Assert.Equal(SessionActivity.WaitingAttention, Assert.Single(r.Retenus).Activity);
        Assert.Equal(TimeSpan.Zero, Assert.Single(r.Desaccords).EcartAge);

        var inverse = ArbitrageSessions.Trancher(Enumerable.Reverse(direct).ToArray());
        Assert.Equal(Canonique(r), Canonique(inverse));
    }

    [Fact]
    public void Permuter_l_ordre_des_signaux_ne_change_pas_un_seul_etat_ni_un_seul_desaccord()
    {
        var attendu = Canonique(ArbitrageSessions.Trancher(Corpus()));
        var distincts = new HashSet<string>(StringComparer.Ordinal);
        var vues = 0;

        foreach (var permutation in Permutations(Corpus()))
        {
            distincts.Add(Canonique(ArbitrageSessions.Trancher(permutation)));
            vues++;
        }

        Assert.Equal(720, vues);          // 6! — une garde qui n'énumérerait rien serait muette
        Assert.Single(distincts);         // LE critère n°2 de la phase
        Assert.Equal(attendu, distincts.Single());

        // …et le résultat n'est pas n'importe lequel : la fraîcheur a bien tranché s1.
        Assert.Contains("s1=Working", attendu);
        Assert.Contains("s2=WaitingAttention", attendu);
    }

    [Fact]
    public void Un_desaccord_nomme_la_session_les_deux_sources_les_deux_etats_et_l_ecart_d_age()
    {
        var r = ArbitrageSessions.Trancher(new[]
        {
            Hook("s7", SessionActivity.WaitingTurn, T.AddHours(-7)),
            Transcript("s7", SessionActivity.Working, T.AddSeconds(-10)),
        });

        var d = Assert.Single(r.Desaccords);
        Assert.Equal("s7", d.SessionId);
        Assert.Equal(SourceSession.Transcript, d.SourceRetenue);
        Assert.Equal(SessionActivity.Working, d.EtatRetenu);
        Assert.Equal(SourceSession.Hook, d.SourceEcartee);
        Assert.Equal(SessionActivity.WaitingTurn, d.EtatEcarte);
        Assert.Equal(TimeSpan.FromHours(7) - TimeSpan.FromSeconds(10), d.EcartAge);
    }

    [Fact]
    public void Deux_sources_d_accord_ne_produisent_aucun_desaccord()
    {
        // Un DOUBLON n'est pas un désaccord. Le confondre avec un désaccord noierait les vraies
        // contradictions sous le bruit de deux sources qui se confirment.
        var r = ArbitrageSessions.Trancher(new[]
        {
            Hook("s", SessionActivity.Working, T.AddMinutes(-3)),
            Transcript("s", SessionActivity.Working, T.AddSeconds(-10)),
        });

        Assert.Equal(SessionActivity.Working, Assert.Single(r.Retenus).Activity);
        Assert.Empty(r.Desaccords);
    }

    [Fact]
    public void Aucun_ecart_d_age_n_est_negatif_sur_tout_le_corpus()
    {
        // L'écart d'âge est une DISTANCE : le retenu est le plus récent, donc l'écart va toujours du
        // récent vers l'ancien. Un écart négatif signalerait un vainqueur plus vieux que son perdant.
        var r = ArbitrageSessions.Trancher(Corpus());

        Assert.NotEmpty(r.Desaccords);                                  // garde anti-muette
        Assert.All(r.Desaccords, d => Assert.True(d.EcartAge >= TimeSpan.Zero,
            $"ecart d'age negatif sur {d.SessionId} : {d.EcartAge}"));
    }

    [Fact]
    public void L_entree_vide_rend_deux_listes_vides()
    {
        var r = ArbitrageSessions.Trancher(Array.Empty<SignalSession>());

        Assert.NotNull(r.Retenus);
        Assert.NotNull(r.Desaccords);
        Assert.Empty(r.Retenus);
        Assert.Empty(r.Desaccords);
    }
}
