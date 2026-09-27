using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Services.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// TOK-03 (lecture pour TOK-02) — le lecteur de transcript AU NIVEAU OCTET. Ce que ces tests gravent : l'offset rendu est
/// toujours juste après le dernier <c>\n</c> (un fragment final n'est pas une ligne, il attend son tour et la reprise
/// depuis cet offset donne exactement la suite) ; le pré-filtre texte est une économie, l'autorité reste
/// <c>type == assistant</c> ET <c>message.role == assistant</c> ; <c>Sub</c> vient du dossier parent <c>subagents</c> ;
/// une ligne datée du futur proche BLOQUE le curseur devant elle (latence d'écriture mesurée : +3 s), une ligne à plus
/// de 24 h d'avance est ignorée et comptée (horloge décalée) ; une ligne à quatre compteurs nuls n'est ni traitée ni
/// « ignorée » ; un fichier absent ou verrouillé rend un résultat vide sans lever.
///
/// Fixtures RÉELLES anonymisées sous TestData/transcripts/&lt;cas&gt;/ (localisées par [CallerFilePath], rien n'est copié
/// en sortie de build). E/S temporaires uniquement, jamais ~/.claude.
/// </summary>
public sealed class LecteurTranscriptTests
{
    private static string Fixture(string cas, string fichier, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "transcripts", cas, fichier);

    private static DateTimeOffset Utc(string iso)
        => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static readonly DateTimeOffset NowParDefaut = Utc("2026-10-01T00:00:00Z");

    private static (ResultatLecture Resultat, List<MessageLu> Messages) Lire(
        string chemin, long depuis = 0, DateTimeOffset? now = null, CancellationToken ct = default)
    {
        var messages = new List<MessageLu>();
        var r = LecteurTranscript.Lire(chemin, depuis, now ?? NowParDefaut, messages.Add, ct);
        return (r, messages);
    }

    // Offset juste après le n-ième '\n' (1-based), calculé sur les octets du fichier, indépendamment du lecteur.
    private static long FinDeLigne(string chemin, int n)
    {
        var octets = File.ReadAllBytes(chemin);
        var vus = 0;
        for (var i = 0; i < octets.Length; i++)
        {
            if (octets[i] != (byte)'\n') continue;
            if (++vus == n) return i + 1;
        }
        throw new InvalidOperationException($"Moins de {n} lignes dans {chemin}");
    }

    [Fact]
    public void Le_gabarit_multi_blocs_donne_sept_messages_lus_et_l_offset_de_fin_de_fichier()
    {
        var chemin = Fixture("multi-blocs", "session-a.jsonl");

        var (r, m) = Lire(chemin);

        Assert.Equal(7, m.Count);
        Assert.Equal(new FileInfo(chemin).Length, r.OffsetDerniereLigneComplete);
        Assert.Equal(6014, r.OffsetDerniereLigneComplete);
        Assert.Equal(7, r.LignesLues);
        Assert.Equal(7, r.LignesPrefiltrees);
        Assert.Equal(7, r.LignesAssistant);
        Assert.Equal(0, r.LignesIgnorees);
        Assert.All(m, x => Assert.Equal("claude-opus-4-1", x.Model));
        Assert.All(m, x => Assert.False(x.Sub));
        Assert.Equal("msg_01EXEMPLEMULTIBLOCS0000000001", m[0].Id);
        Assert.Equal((2L, 8L, 35005L, 41741L), (m[0].In, m[0].Out, m[0].CacheW, m[0].CacheR));
        Assert.Equal(256L, m[2].Out);
        Assert.Equal("req_01EXEMPLESANSMESSAGEID00000002", m[4].Id);
        Assert.Null(m[6].Id);
        Assert.Equal(Utc("2026-07-08T11:20:00Z"), r.PlusAncienTs);
        Assert.Equal(Utc("2026-07-08T11:28:00Z"), r.PlusRecentTs);
        Assert.False(r.BloqueSurLigneFuture);
        Assert.False(r.Annulee);
    }

    [Fact]
    public void Une_ligne_tronquee_en_fin_de_fichier_n_est_pas_traitee_et_le_curseur_s_arrete_avant_elle()
    {
        var chemin = Fixture("tronque", "session-c.jsonl");

        var (r, m) = Lire(chemin);

        Assert.Equal(2, m.Count);
        Assert.Equal("msg_01EXEMPLETRONQUE000000000001", m[0].Id);
        Assert.Equal("msg_01EXEMPLETRONQUE000000000002", m[1].Id);
        Assert.Equal(FinDeLigne(chemin, 2), r.OffsetDerniereLigneComplete);
        Assert.True(r.OffsetDerniereLigneComplete < new FileInfo(chemin).Length);   // le fragment reste devant le curseur
        Assert.Equal(2, r.LignesLues);
        Assert.Equal(0, r.LignesIgnorees);                                          // un fragment n'est pas une ligne
    }

    [Fact]
    public void Reprendre_depuis_l_offset_apres_completion_donne_exactement_les_deux_messages_suivants()
    {
        var temp = Path.Combine(Path.GetTempPath(), "chronos-tests-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            File.Copy(Fixture("tronque", "session-c.jsonl"), temp);
            var (r1, m1) = Lire(temp);
            Assert.Equal(2, m1.Count);
            var x = r1.OffsetDerniereLigneComplete;

            // Claude Code termine la ligne et en écrit une autre : on ajoute les octets de suite.jsonl en fin de fichier.
            using (var fs = new FileStream(temp, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                fs.Seek(0, SeekOrigin.End);
                var suite = File.ReadAllBytes(Fixture("tronque", "suite.jsonl"));
                fs.Write(suite, 0, suite.Length);
            }

            var (r2, m2) = Lire(temp, depuis: x);

            Assert.Equal(2, m2.Count);
            Assert.Equal("msg_01EXEMPLETRONQUE000000000003", m2[0].Id);
            Assert.Equal((3L, 30L, 300L, 3000L), (m2[0].In, m2[0].Out, m2[0].CacheW, m2[0].CacheR));
            Assert.Equal("msg_01EXEMPLETRONQUE000000000004", m2[1].Id);
            Assert.Equal((4L, 40L, 400L, 4000L), (m2[1].In, m2[1].Out, m2[1].CacheW, m2[1].CacheR));
            Assert.Equal(new FileInfo(temp).Length, r2.OffsetDerniereLigneComplete);
            Assert.Equal(0, r2.LignesIgnorees);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Le_prefiltre_est_une_economie_pas_l_autorite()
    {
        var (r, m) = Lire(Fixture("prefiltre-piege", "session-f.jsonl"));

        Assert.Equal(3, r.LignesLues);
        Assert.Equal(2, r.LignesPrefiltrees);   // la ligne user porte les octets du marqueur, la ligne summary non
        Assert.Equal(1, r.LignesAssistant);     // seule la vraie ligne passe l'autorité type + role
        Assert.Equal(0, r.LignesIgnorees);      // refuser une ligne user n'est pas « l'ignorer »
        var seul = Assert.Single(m);
        Assert.Equal("msg_01EXEMPLEPIEGE00000000000001", seul.Id);
        Assert.Equal((1L, 1L, 1L, 1L), (seul.In, seul.Out, seul.CacheW, seul.CacheR));
    }

    [Fact]
    public void Sub_vient_du_dossier_parent_subagents()
    {
        var agentX = Fixture("sous-agent", Path.Combine("session-b", "subagents", "agent-x.jsonl"));
        var principal = Fixture("sous-agent", "session-b.jsonl");

        Assert.True(LecteurTranscript.EstSousAgent(agentX));
        Assert.False(LecteurTranscript.EstSousAgent(principal));
        Assert.True(LecteurTranscript.EstSousAgent(Path.Combine("x", "SUBAGENTS", "agent-y.jsonl")));   // ordinal-insensible
        Assert.False(LecteurTranscript.EstSousAgent(Path.Combine("subagents-archive", "agent-y.jsonl")));

        var (_, sous) = Lire(agentX);
        Assert.Equal(3, sous.Count);
        Assert.All(sous, x => Assert.True(x.Sub));
        Assert.All(sous, x => Assert.Equal("claude-sonnet-5", x.Model));
        Assert.Equal("msg_01EXEMPLESOUSAGENT0000000001", sous[0].Id);
        Assert.Equal((20L, 200L, 0L, 6000L), (sous[1].In, sous[1].Out, sous[1].CacheW, sous[1].CacheR));

        var (_, prin) = Lire(principal);
        var seul = Assert.Single(prin);
        Assert.False(seul.Sub);
        Assert.Equal("claude-opus-5", seul.Model);
        Assert.Equal("msg_01EXEMPLEPRINCIPAL0000000001", seul.Id);
    }

    [Fact]
    public void Une_ligne_future_de_quelques_secondes_bloque_le_curseur_devant_elle()
    {
        var chemin = Fixture("futur", "session-d.jsonl");

        // FUTUR02 est daté 10:00:03 : à 10:00:01 elle est « dans 2 s » — latence d'écriture, pas horloge décalée.
        var (r, m) = Lire(chemin, now: Utc("2026-09-20T10:00:01Z"));

        var seul = Assert.Single(m);
        Assert.Equal("msg_01EXEMPLEFUTUR00000000000001", seul.Id);
        Assert.True(r.BloqueSurLigneFuture);
        Assert.Equal(FinDeLigne(chemin, 1), r.OffsetDerniereLigneComplete);   // le curseur reste AVANT la ligne future
        Assert.Equal(0, r.LignesFuturesIgnorees);
        Assert.Equal(TimeSpan.FromHours(24), LecteurTranscript.ToleranceFutur);
    }

    [Fact]
    public void Une_ligne_future_de_plus_de_24_h_est_ignoree_et_comptee()
    {
        var chemin = Fixture("futur", "session-d.jsonl");

        // À 10:00:10, FUTUR02 est passée ; FUTUR03 (le 23/09) est à ~3 j : horloge décalée → ignorée, comptée, curseur avancé.
        var (r, m) = Lire(chemin, now: Utc("2026-09-20T10:00:10Z"));

        Assert.Equal(2, m.Count);
        Assert.Equal("msg_01EXEMPLEFUTUR00000000000001", m[0].Id);
        Assert.Equal("msg_01EXEMPLEFUTUR00000000000002", m[1].Id);
        Assert.Equal(1, r.LignesFuturesIgnorees);
        Assert.False(r.BloqueSurLigneFuture);
        Assert.Equal(new FileInfo(chemin).Length, r.OffsetDerniereLigneComplete);
        Assert.Equal(3, r.LignesAssistant);
    }

    [Fact]
    public void Une_ligne_synthetic_a_quatre_zeros_n_est_ni_traitee_ni_ignoree()
    {
        var (r, m) = Lire(Fixture("synthetic", "session-h.jsonl"));

        Assert.Empty(m);
        Assert.Equal(1, r.LignesLues);
        Assert.Equal(1, r.LignesAssistant);
        Assert.Equal(1, r.LignesSansTokens);
        Assert.Equal(0, r.LignesIgnorees);
        Assert.Null(r.PlusAncienTs);   // aucun message traité : aucune borne
    }

    [Fact]
    public void Un_fichier_absent_ou_verrouille_rend_un_resultat_vide_sans_lever_et_l_annulation_est_honoree()
    {
        var absent = Path.Combine(Path.GetTempPath(), "chronos-tests-" + Guid.NewGuid().ToString("N"), "absent.jsonl");

        var (rAbsent, mAbsent) = Lire(absent, depuis: 42);
        Assert.Empty(mAbsent);
        Assert.Equal(42, rAbsent.OffsetDerniereLigneComplete);   // le curseur ne bouge pas : le fichier sera retenté
        Assert.Equal(0, rAbsent.LignesLues);
        Assert.False(rAbsent.Annulee);

        // Un jeton déjà annulé : rien n'est compté à moitié, le contrôle se fait AVANT la première ligne.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (rAnnule, mAnnule) = Lire(Fixture("multi-blocs", "session-a.jsonl"), ct: cts.Token);
        Assert.True(rAnnule.Annulee);
        Assert.Empty(mAnnule);
        Assert.Equal(0, rAnnule.OffsetDerniereLigneComplete);
        Assert.Equal(0, rAnnule.LignesLues);
    }
}
