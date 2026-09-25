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

    /// <summary>La troisième source (APP-03, phase 29) : la question que l'app bureau a vue à la fin d'un tour.
    /// Elle ne dépose qu'une attente, avec son motif (le <c>needs_action</c> de l'app).</summary>
    private static SignalSession AppBureau(string id, SessionActivity a, DateTimeOffset maj, string? motif = null)
        => new(SourceSession.AppBureau, new SessionSnapshot(id, "Proj-" + id, a, motif, maj));

    /// <summary>L'écart mesuré entre la fin du tour (<c>end_turn</c> du transcript) et l'instant du résumé
    /// <c>blocked</c> de l'app, sur la fixture réelle (29-RESEARCH Q3.c) : 1,561 s.</summary>
    private static readonly TimeSpan ResumeApresFinDeTour = TimeSpan.FromMilliseconds(1561);

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

    // Le corpus de la RÉSERVE R4 (audit v1.6). Le corpus ci-dessus ne descend JAMAIS au rang 3 de l'arbitrage :
    // s1 est tranché par la fraîcheur, s2 par la source, s3 et s4 sont seules. Le rejouer après avoir changé
    // l'ordre d'écran serait vert, que l'arbitrage lise cet ordre ou non — un test muet. Ici, chaque session
    // oppose deux signaux de MÊME instant et de MÊME source : seul le rang d'état peut trancher. Et r1 porte le
    // seul couple dont l'ordre relatif a changé en phase 28 : (WaitingDeduced, Working).
    private static SignalSession[] CorpusRang3() => new[]
    {
        Hook("r1", SessionActivity.WaitingDeduced, T),                          // l'écran la place DEVANT…
        Hook("r1", SessionActivity.Working, T),                                 // …l'arbitrage retient celui-ci
        Transcript("r2", SessionActivity.WaitingAttention, T.AddMinutes(-1)),   // une question (LIB-02)
        Transcript("r2", SessionActivity.Working, T.AddMinutes(-1)),
        Hook("r3", SessionActivity.Unknown, T.AddMinutes(-2)),
        Hook("r3", SessionActivity.WaitingTurn, T.AddMinutes(-2)),
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

    /// <summary>
    /// RÉSERVE R4 de l'audit v1.6, fermée en phase 28 (LIB-04). L'écran place désormais une attente déduite
    /// DEVANT un travail ; l'arbitrage entre sources, lui, ne doit rien en savoir : pour deux signaux d'une
    /// même session, du même instant et de la même source, il retient toujours le travail. Dans l'arbitrage,
    /// une déduction ne bat jamais une observation — c'est FUS-01, et un classement d'affichage ne peut pas
    /// la réécrire en silence.
    ///
    /// <para>La NON-VACUITÉ vient d'abord : si l'écran ne plaçait pas la déduction devant le travail, les deux
    /// ordres coïncideraient sur ce couple et le test serait vert couplé ou non. Contrôle de mutation joué en
    /// phase 28 : faire lire l'ordre d'écran à l'arbitrage fait rougir ce test sur <c>r1=Working@</c>.</para>
    /// </summary>
    [Fact]
    public void Remonter_la_deduction_a_l_ecran_ne_change_aucun_arbitrage()
    {
        // NON-VACUITÉ : l'écran place bien la déduction devant le travail…
        Assert.True(AffichageSessions.Urgence(SessionActivity.WaitingDeduced)
                  < AffichageSessions.Urgence(SessionActivity.Working),
            "L'ordre d'écran ne place plus la déduction devant le travail : ce test ne prouverait plus rien.");

        // …et 720 ordres d'arrivée donnent UN résultat, où l'arbitrage retient le travail.
        var distincts = new HashSet<string>(StringComparer.Ordinal);
        var vues = 0;
        foreach (var permutation in Permutations(CorpusRang3()))
        {
            distincts.Add(Canonique(ArbitrageSessions.Trancher(permutation)));
            vues++;
        }

        Assert.Equal(720, vues);          // 6! — une garde qui n'énumérerait rien serait muette
        var resultat = Assert.Single(distincts);
        Assert.Contains("r1=Working@", resultat);
        Assert.Contains("r2=WaitingAttention@", resultat);
        Assert.Contains("r3=WaitingTurn@", resultat);
    }

    /// <summary>
    /// Le rang d'état PROPRE à l'arbitrage, FIGÉ aux valeurs de la phase 24 : attention, tour fini, travail,
    /// puis déduit et indéterminé ex aequo. Deux signaux identiques en tout sauf l'état (même source, même
    /// instant, même motif, même projet), dans LES DEUX ordres d'entrée : le gagnant ne dépend que du rang.
    /// La ligne (Working, WaitingDeduced) est celle que l'ordre d'écran de la phase 28 aurait retournée.
    /// </summary>
    [Theory]
    [InlineData(SessionActivity.WaitingAttention, SessionActivity.WaitingTurn, SessionActivity.WaitingAttention)]
    [InlineData(SessionActivity.WaitingTurn, SessionActivity.Working, SessionActivity.WaitingTurn)]
    [InlineData(SessionActivity.Working, SessionActivity.WaitingDeduced, SessionActivity.Working)]
    [InlineData(SessionActivity.Working, SessionActivity.Unknown, SessionActivity.Working)]
    public void Le_rang_d_arbitrage_reste_celui_de_la_phase_24(SessionActivity a, SessionActivity b, SessionActivity gagnant)
    {
        var direct = ArbitrageSessions.Trancher(new[] { Hook("s", a, T), Hook("s", b, T) });
        var inverse = ArbitrageSessions.Trancher(new[] { Hook("s", b, T), Hook("s", a, T) });

        Assert.Equal(gagnant, Assert.Single(direct.Retenus).Activity);
        Assert.Equal(gagnant, Assert.Single(inverse.Retenus).Activity);
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

    /// <summary>
    /// TRT-01 — le détecteur de traitement a besoin de savoir QUI a parlé : sans la source, « attente
    /// puis travail » ne distingue pas une réponse de l'utilisateur d'un relais entre deux sources. Les
    /// vainqueurs sont donc la MÊME séquence que les retenus, index pour index, chaque élément portant
    /// en plus sa source. Cette garde empêche les deux séquences de dériver l'une de l'autre.
    /// </summary>
    [Fact]
    public void Les_vainqueurs_sont_les_retenus_avec_leur_source()
    {
        var r = ArbitrageSessions.Trancher(Corpus());

        // Comparer deux listes VIDES serait muet : on exige d'abord qu'il y ait quelque chose à comparer.
        Assert.NotEmpty(r.Vainqueurs);
        Assert.Equal(r.Retenus, r.Vainqueurs.Select(v => v.Session));
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

    // ── APP-03 (phase 29) : trois sources, un arbitrage ─────────────────────────────────────────────────────
    // L'app bureau entre dans FUS-01 comme TROISIÈME source datée, rangée à âge égal ENTRE le hook (lui seul dit
    // « permission ») et le transcript (qui ne voit pas une question posée en prose). La fraîcheur prime toujours :
    // le rang ne sert qu'à âge égal. Les cas portent les numéros du tableau de 29-RESEARCH Q3.d.

    /// <summary>L'ordre de déclaration EST le rang à âge égal : le tenir par un test, c'est tenir la règle.</summary>
    [Fact]
    public void L_ordre_des_sources_est_hook_puis_app_bureau_puis_transcript()
    {
        Assert.Equal(new[] { SourceSession.Hook, SourceSession.AppBureau, SourceSession.Transcript },
                     Enum.GetValues<SourceSession>());
    }

    /// <summary>C4 — même milliseconde : l'app bat le transcript, dans les deux ordres d'entrée.</summary>
    [Fact]
    public void A_age_egal_l_app_bureau_bat_le_transcript_dans_les_deux_ordres()
    {
        var direct = new[]
        {
            AppBureau("s", SessionActivity.WaitingAttention, T, "réponse attendue"),
            Transcript("s", SessionActivity.WaitingTurn, T),
        };

        var r = ArbitrageSessions.Trancher(direct);
        Assert.Equal(SessionActivity.WaitingAttention, Assert.Single(r.Retenus).Activity);
        var d = Assert.Single(r.Desaccords);
        Assert.Equal(SourceSession.AppBureau, d.SourceRetenue);
        Assert.Equal(SourceSession.Transcript, d.SourceEcartee);
        Assert.Equal(TimeSpan.Zero, d.EcartAge);

        var inverse = ArbitrageSessions.Trancher(Enumerable.Reverse(direct).ToArray());
        Assert.Equal(Canonique(r), Canonique(inverse));
    }

    /// <summary>C5 — même milliseconde : le hook bat l'app, dans les deux ordres d'entrée.</summary>
    [Fact]
    public void A_age_egal_le_hook_bat_l_app_bureau_dans_les_deux_ordres()
    {
        var direct = new[]
        {
            Hook("s", SessionActivity.WaitingTurn, T),
            AppBureau("s", SessionActivity.WaitingAttention, T, "réponse attendue"),
        };

        var r = ArbitrageSessions.Trancher(direct);
        Assert.Equal(SessionActivity.WaitingTurn, Assert.Single(r.Retenus).Activity);
        var d = Assert.Single(r.Desaccords);
        Assert.Equal(SourceSession.Hook, d.SourceRetenue);
        Assert.Equal(SourceSession.AppBureau, d.SourceEcartee);
        Assert.Equal(TimeSpan.Zero, d.EcartAge);

        var inverse = ArbitrageSessions.Trancher(Enumerable.Reverse(direct).ToArray());
        Assert.Equal(Canonique(r), Canonique(inverse));
    }

    /// <summary>C1 — le cas nominal : le résumé <c>blocked</c> est écrit 1,561 s après la fin du tour. L'app gagne
    /// par FRAÎCHEUR, pas par rang ; le désaccord (question contre tour fini) est consigné et nommé par ses sources
    /// (Piège 8, accepté).</summary>
    [Fact]
    public void Une_question_de_l_app_plus_recente_bat_le_tour_fini_du_transcript()
    {
        var r = ArbitrageSessions.Trancher(new[]
        {
            AppBureau("s", SessionActivity.WaitingAttention, T + ResumeApresFinDeTour, "réponse attendue"),
            Transcript("s", SessionActivity.WaitingTurn, T),
        });

        var retenu = Assert.Single(r.Retenus);
        Assert.Equal(SessionActivity.WaitingAttention, retenu.Activity);
        Assert.Equal("réponse attendue", retenu.Reason);

        var d = Assert.Single(r.Desaccords);
        Assert.Equal(SourceSession.AppBureau, d.SourceRetenue);
        Assert.Equal(SessionActivity.WaitingAttention, d.EtatRetenu);
        Assert.Equal(SourceSession.Transcript, d.SourceEcartee);
        Assert.Equal(SessionActivity.WaitingTurn, d.EtatEcarte);
        Assert.Equal(ResumeApresFinDeTour, d.EcartAge);
    }

    /// <summary>C2 — l'utilisateur répond : le transcript, plus récent, l'emporte même si le lecteur n'a pas encore
    /// vu l'app effacer son résumé.</summary>
    [Fact]
    public void Une_reponse_plus_recente_bat_la_question_de_l_app()
    {
        var r = ArbitrageSessions.Trancher(new[]
        {
            AppBureau("s", SessionActivity.WaitingAttention, T + ResumeApresFinDeTour, "réponse attendue"),
            Transcript("s", SessionActivity.Working, T.AddSeconds(30)),
        });

        Assert.Equal(SessionActivity.Working, Assert.Single(r.Retenus).Activity);
        Assert.Equal(SourceSession.Transcript, Assert.Single(r.Desaccords).SourceRetenue);
    }

    /// <summary>C6 — le rang du hook ne sert qu'à âge égal : un Stop plus ancien de 1,261 s cède à la question.</summary>
    [Fact]
    public void Un_stop_de_hook_plus_ancien_perd_contre_la_question_de_l_app()
    {
        var r = ArbitrageSessions.Trancher(new[]
        {
            Hook("s", SessionActivity.WaitingTurn, T.AddMilliseconds(300)),
            AppBureau("s", SessionActivity.WaitingAttention, T + ResumeApresFinDeTour, "réponse attendue"),
        });

        Assert.Equal(SessionActivity.WaitingAttention, Assert.Single(r.Retenus).Activity);
        var d = Assert.Single(r.Desaccords);
        Assert.Equal(SourceSession.AppBureau, d.SourceRetenue);
        Assert.Equal(SourceSession.Hook, d.SourceEcartee);
    }

    // Le corpus à TROIS sources : trois sessions, chacune tranchée par une règle différente — la fraîcheur (a1),
    // le rang où l'app gagne (a4, contre le transcript), le rang où l'app perd (a5, contre le hook).
    private static SignalSession[] CorpusTroisSources() => new[]
    {
        AppBureau("a1", SessionActivity.WaitingAttention, T + ResumeApresFinDeTour, "réponse attendue"),
        Transcript("a1", SessionActivity.WaitingTurn, T),                                  // la fraîcheur tranche
        AppBureau("a4", SessionActivity.WaitingAttention, T.AddMinutes(-1), "réponse attendue"),
        Transcript("a4", SessionActivity.WaitingTurn, T.AddMinutes(-1)),                   // rang : l'app gagne
        Hook("a5", SessionActivity.WaitingTurn, T.AddMinutes(-2)),
        AppBureau("a5", SessionActivity.WaitingAttention, T.AddMinutes(-2), "réponse attendue"), // rang : l'app perd
    };

    /// <summary>720 ordres d'arrivée d'un corpus qui DESCEND au rang de source donnent un seul résultat.
    /// <para>NON-VACUITÉ d'abord : a4 et a5 opposent deux signaux du MÊME instant et de sources DIFFÉRENTES — seul
    /// le rang de source peut les trancher ; sans cela, le test serait vert que l'app soit rangée au milieu ou non.
    /// Mutation (d) jouée : déclarer <c>AppBureau</c> après <c>Transcript</c> fait rougir ce test sur
    /// <c>a4=WaitingAttention@</c>.</para></summary>
    [Fact]
    public void Permuter_trois_sources_ne_change_pas_un_seul_etat_ni_un_seul_desaccord()
    {
        var corpus = CorpusTroisSources();
        foreach (var id in new[] { "a4", "a5" })
        {
            var paire = corpus.Where(s => s.Session.SessionId == id).ToArray();
            Assert.Equal(2, paire.Length);
            Assert.Equal(paire[0].Session.UpdatedAt, paire[1].Session.UpdatedAt);
            Assert.NotEqual(paire[0].Source, paire[1].Source);
            Assert.Contains(paire, s => s.Source == SourceSession.AppBureau);
        }

        var distincts = new HashSet<string>(StringComparer.Ordinal);
        var vues = 0;
        foreach (var permutation in Permutations(corpus))
        {
            distincts.Add(Canonique(ArbitrageSessions.Trancher(permutation)));
            vues++;
        }

        Assert.Equal(720, vues);          // 6! — une garde qui n'énumérerait rien serait muette
        var resultat = Assert.Single(distincts);
        Assert.Contains("a1=WaitingAttention@", resultat);
        Assert.Contains("a4=WaitingAttention@", resultat);
        Assert.Contains("a5=WaitingTurn@", resultat);
        Assert.Equal(3, ArbitrageSessions.Trancher(corpus).Desaccords.Count);
    }
}
