using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// TOK-03 — l'index d'ids est la MÉMOIRE D'IDEMPOTENCE des agrégats (D-33-07). Ce que ces tests gravent : re-rencontrer un id
/// rend le delta « max − déjà compté » (bloc partiel qui grandit : 248 ; copie fork / resume, relecture après arrêt : null),
/// le timestamp retenu reste le PREMIER vu, les shards <c>ids-AAAA-MM.jsonl</c> sont en ajout seul et le max gagne à la
/// relecture, seuls les mois qui chevauchent <c>[now − 45 j, now]</c> sont chargés (au-delà : gelé, limite documentée), une
/// ligne sans identifiant est comptée telle quelle et jamais indexée, la rétention des shards est de trois mois.
///
/// Dossier temporaire par test, jamais %APPDATA% ; fixtures réelles anonymisées lues par <see cref="LecteurTranscript"/>.
/// </summary>
public sealed class IndexMessagesTests : IDisposable
{
    private readonly string _dossier = Path.Combine(Path.GetTempPath(), "chronos-tests-index-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = Utc("2026-10-01T00:00:00Z");
    private static readonly DateTimeOffset TsA = Utc("2026-09-20T08:00:00Z");

    public void Dispose()
    {
        try { if (Directory.Exists(_dossier)) Directory.Delete(_dossier, recursive: true); } catch { }
    }

    private static DateTimeOffset Utc(string iso)
        => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static string Fixture(string cas, string fichier, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "transcripts", cas, fichier);

    private IndexMessages Index(DateTimeOffset? now = null) => new(_dossier, new FakeClock(now ?? Now));

    private static List<MessageLu> LireFixture(string cas, string fichier)
    {
        var messages = new List<MessageLu>();
        LecteurTranscript.Lire(Fixture(cas, fichier), 0, Now, messages.Add, CancellationToken.None);
        return messages;
    }

    private static MessageLu M(string? id, DateTimeOffset ts, long entree, long sortie, long cw, long cr, string model = "claude-opus-5")
        => new(id, ts, model, false, entree, sortie, cw, cr);

    private void EcrireShard(string nom, params string[] lignes)
    {
        Directory.CreateDirectory(_dossier);
        File.WriteAllText(Path.Combine(_dossier, nom), string.Join("\n", lignes) + "\n", new System.Text.UTF8Encoding(false));
    }

    private static string LigneShard(string id, string ts, long entree, long sortie, long cw, long cr, string model = "claude-opus-5")
        => $"{{\"v\":1,\"id\":\"{id}\",\"ts\":\"{ts}\",\"model\":\"{model}\",\"sub\":false,\"in\":{entree},\"out\":{sortie},\"cache_w\":{cw},\"cache_r\":{cr}}}";

    [Fact]
    public void Le_nom_du_shard_est_le_mois_UTC_du_premier_timestamp()
    {
        Assert.Equal("ids-2026-09.jsonl", IndexMessages.NomFichier(Utc("2026-09-30T23:59:00Z")));
        Assert.Equal("ids-2026-10.jsonl", IndexMessages.NomFichier(Utc("2026-09-30T23:59:00-02:00")));   // 01:59Z le 1er octobre

        Assert.True(IndexMessages.EstNomShard("ids-2026-09.jsonl", out var mois));
        Assert.Equal(Utc("2026-09-01T00:00:00Z"), mois);
        Assert.False(IndexMessages.EstNomShard("tokens-2026-09.jsonl", out _));
        Assert.False(IndexMessages.EstNomShard("ids-2026-13.jsonl", out _));

        Assert.Equal(new[] { "v", "id", "ts", "model", "sub", "in", "out", "cache_w", "cache_r" }, IndexMessages.Champs);
        Assert.Equal(1, IndexMessages.SchemaVersion);
    }

    [Fact]
    public void Un_message_nouveau_rend_un_delta_egal_a_ses_compteurs_et_NouveauMessage()
    {
        var index = Index();

        var delta = index.Ajouter(M("msg_A", TsA, 2, 8, 35005, 41741));

        Assert.Equal(new DeltaMessage(TsA, "claude-opus-5", false, 2, 8, 35005, 41741, NouveauMessage: true), delta);
        Assert.Equal(1, index.IdsConnus);
        Assert.Equal(1, index.LignesEnAttente);
    }

    [Fact]
    public void Un_bloc_partiel_qui_grandit_rend_le_delta_et_garde_le_premier_timestamp()
    {
        var index = Index();
        index.Ajouter(M("msg_A", TsA, 2, 8, 35005, 41741));

        var grandi = index.Ajouter(M("msg_A", TsA.AddSeconds(5), 2, 256, 35005, 41741));
        Assert.Equal(new DeltaMessage(TsA, "claude-opus-5", false, 0, 248, 0, 0, NouveauMessage: false), grandi);

        var doublon = index.Ajouter(M("msg_A", TsA, 2, 8, 35005, 41741));   // L4 : doublon strict relu APRÈS le max
        Assert.Null(doublon);

        Assert.Equal(1, index.IdsConnus);
        Assert.Equal(2, index.LignesEnAttente);   // une ligne par delta non nul
    }

    [Fact]
    public void Une_copie_fork_resume_rend_null_dans_les_deux_ordres()
    {
        var a = LireFixture("fork-copie", "session-a.jsonl");
        var b = LireFixture("fork-copie", "session-b.jsonl");
        Assert.Equal(3, a.Count);
        Assert.Equal(4, b.Count);

        var indexAB = Index();
        var deltasAB = a.Concat(b).Select(indexAB.Ajouter).ToList();
        Assert.Equal(5, deltasAB.Count(d => d is not null));
        Assert.Equal(2, deltasAB.Count(d => d is null));
        Assert.Null(deltasAB[3]);   // FORK01 copié dans B
        Assert.Null(deltasAB[4]);   // FORK02 copié dans B
        Assert.Equal(5, indexAB.IdsConnus);

        var indexBA = Index();
        var deltasBA = b.Concat(a).Select(indexBA.Ajouter).ToList();
        Assert.Equal(5, deltasBA.Count(d => d is not null));
        Assert.Null(deltasBA[4]);   // FORK01 relu dans A
        Assert.Null(deltasBA[5]);   // FORK02 relu dans A
        Assert.Equal(5, indexBA.IdsConnus);

        static IEnumerable<(long, long, long, long)> Multiset(IEnumerable<DeltaMessage?> d)
            => d.Where(x => x is not null).Select(x => (x!.In, x.Out, x.CacheW, x.CacheR)).OrderBy(x => x);
        Assert.Equal(Multiset(deltasAB), Multiset(deltasBA));
        Assert.All(deltasAB.Where(d => d is not null), d => Assert.True(d!.NouveauMessage));
    }

    [Fact]
    public void Une_ligne_sans_identifiant_est_comptee_telle_quelle_et_jamais_indexee()
    {
        var index = Index();

        var d1 = index.Ajouter(M(null, TsA, 2, 2, 2, 2, model: "m"));
        var d2 = index.Ajouter(M(null, TsA, 2, 2, 2, 2, model: "m"));

        Assert.Equal(new DeltaMessage(TsA, "m", false, 2, 2, 2, 2, NouveauMessage: true), d1);
        Assert.Equal(d1, d2);
        Assert.Equal(0, index.IdsConnus);
        Assert.Equal(0, index.LignesEnAttente);
        Assert.True(index.Flush());
        Assert.False(Directory.Exists(_dossier) && Directory.EnumerateFiles(_dossier).Any());   // rien à écrire : aucun fichier
    }

    [Fact]
    public void Le_repli_requestId_est_la_cle_quand_message_id_manque()
    {
        var index = Index();
        var lus = LireFixture("sans-message-id", "session-g.jsonl");
        Assert.Equal(3, lus.Count);

        var deltas = lus.Select(index.Ajouter).ToList();

        Assert.Equal((1L, 10L, 100L, 1000L, true), (deltas[0]!.In, deltas[0]!.Out, deltas[0]!.CacheW, deltas[0]!.CacheR, deltas[0]!.NouveauMessage));
        Assert.Equal((0L, 30L, 0L, 0L, false), (deltas[1]!.In, deltas[1]!.Out, deltas[1]!.CacheW, deltas[1]!.CacheR, deltas[1]!.NouveauMessage));
        Assert.Equal(Utc("2026-09-21T13:00:00Z"), deltas[1]!.Ts);   // premier timestamp du requestId
        Assert.Equal((2L, 2L, 2L, 2L, true), (deltas[2]!.In, deltas[2]!.Out, deltas[2]!.CacheW, deltas[2]!.CacheR, deltas[2]!.NouveauMessage));
        Assert.Equal(1, index.IdsConnus);
    }

    [Fact]
    public void Flush_ecrit_une_ligne_par_delta_dans_le_shard_du_mois_et_la_relecture_garde_le_max()
    {
        var index = Index();
        index.Ajouter(M("msg_A", TsA, 2, 8, 35005, 41741));
        index.Ajouter(M("msg_A", TsA.AddSeconds(5), 2, 256, 35005, 41741));
        index.Ajouter(M("msg_A", TsA, 2, 8, 35005, 41741));
        Assert.Equal(2, index.LignesEnAttente);

        Assert.True(index.Flush());
        Assert.Equal(0, index.LignesEnAttente);

        var shard = Path.Combine(_dossier, "ids-2026-09.jsonl");
        Assert.True(File.Exists(shard));
        var octets = File.ReadAllBytes(shard);
        Assert.Equal((byte)'\n', octets[^1]);
        Assert.DoesNotContain((byte)'\r', octets);
        Assert.False(octets.Length >= 3 && octets[0] == 0xEF && octets[1] == 0xBB);   // pas de BOM
        var lignes = File.ReadAllLines(shard);
        Assert.Equal(2, lignes.Length);
        Assert.Equal(
            "{\"v\":1,\"id\":\"msg_A\",\"ts\":\"2026-09-20T08:00:00.0000000+00:00\",\"model\":\"claude-opus-5\",\"sub\":false,\"in\":2,\"out\":8,\"cache_w\":35005,\"cache_r\":41741}",
            lignes[0]);
        Assert.Contains("\"out\":256", lignes[1]);
        Assert.Contains("\"ts\":\"2026-09-20T08:00:00.0000000+00:00\"", lignes[1]);   // le PREMIER timestamp, pas +5 s

        // Nouvelle instance (redémarrage) : la relecture fusionne les deux lignes, le max gagne.
        var relu = Index();
        Assert.Equal(1, relu.Charger());
        Assert.Null(relu.Ajouter(M("msg_A", TsA.AddSeconds(9), 2, 256, 35005, 41741)));
        var plus = relu.Ajouter(M("msg_A", TsA.AddSeconds(9), 2, 300, 35005, 41741));
        Assert.Equal(new DeltaMessage(TsA, "claude-opus-5", false, 0, 44, 0, 0, NouveauMessage: false), plus);

        var entree = Assert.Single(relu.Entrees(Utc("2026-09-01T00:00:00Z")));
        Assert.Equal((TsA, "claude-opus-5", false, 2L, 300L, 35005L, 41741L), entree);
        Assert.Empty(relu.Entrees(Utc("2026-08-01T00:00:00Z")));
    }

    [Fact]
    public void Charger_ne_lit_que_les_mois_ouverts_de_l_horizon()
    {
        EcrireShard("ids-2026-09.jsonl", LigneShard("msg_A", "2026-09-20T08:00:00.0000000+00:00", 1, 1, 1, 1));
        EcrireShard("ids-2026-08.jsonl", LigneShard("msg_B", "2026-08-20T08:00:00.0000000+00:00", 2, 2, 2, 2));
        EcrireShard("ids-2026-06.jsonl", LigneShard("msg_C", "2026-06-20T08:00:00.0000000+00:00", 3, 3, 3, 3));
        var index = Index();

        // 45 j avant le 1er octobre = 17 août : août est ouvert, juin ne l'est pas.
        Assert.Equal(TimeSpan.FromDays(45), IndexMessages.HorizonIndex);
        Assert.Equal(
            new[] { Utc("2026-08-01T00:00:00Z"), Utc("2026-09-01T00:00:00Z"), Utc("2026-10-01T00:00:00Z") },
            index.MoisOuverts());

        Assert.Equal(2, index.Charger());
        Assert.Equal(2, index.IdsConnus);

        Assert.Null(index.Ajouter(M("msg_B", Utc("2026-08-20T08:00:00Z"), 2, 2, 2, 2)));                 // août : connu
        Assert.NotNull(index.Ajouter(M("msg_C", Utc("2026-06-20T08:00:00Z"), 3, 3, 3, 3)));              // juin : GELÉ, recompté (limite documentée)
    }

    [Fact]
    public void Une_ligne_de_shard_corrompue_est_ignoree_et_comptee()
    {
        EcrireShard("ids-2026-09.jsonl",
            LigneShard("msg_A", "2026-09-20T08:00:00.0000000+00:00", 1, 1, 1, 1),
            "{\"v\":1,\"id\":\"msg_B\",\"ts\":\"2026-09-20T08:00:0",                                       // tronquée
            LigneShard("msg_C", "2026-09-20T08:00:00.0000000+00:00", 1, 1, 1, 1).Replace("\"v\":1", "\"v\":2"),   // version inconnue
            "{\"v\":1,\"ts\":\"2026-09-20T08:00:00.0000000+00:00\",\"model\":\"m\",\"sub\":false,\"in\":1,\"out\":1,\"cache_w\":1,\"cache_r\":1}");   // sans id
        var index = Index();

        Assert.Equal(1, index.Charger());
        Assert.Equal(3, index.LignesIgnorees);
    }

    [Fact]
    public void Purger_supprime_les_shards_au_dela_de_trois_mois_et_ignore_le_reste()
    {
        EcrireShard("ids-2026-06.jsonl", LigneShard("msg_A", "2026-06-20T08:00:00.0000000+00:00", 1, 1, 1, 1));
        EcrireShard("ids-2026-07.jsonl", LigneShard("msg_B", "2026-07-20T08:00:00.0000000+00:00", 1, 1, 1, 1));
        EcrireShard("ids-2026-09.jsonl", LigneShard("msg_C", "2026-09-20T08:00:00.0000000+00:00", 1, 1, 1, 1));
        EcrireShard("tokens-2026-06.jsonl", "{\"v\":1}");
        EcrireShard("notes.txt", "…");
        var index = Index();

        Assert.Equal(3, IndexMessages.RetentionIndexMois);
        Assert.Equal(new BilanRetention(1, 0, 2), index.Purger());

        Assert.False(File.Exists(Path.Combine(_dossier, "ids-2026-06.jsonl")));
        Assert.True(File.Exists(Path.Combine(_dossier, "ids-2026-07.jsonl")));   // courant − 3 : gardé
        Assert.True(File.Exists(Path.Combine(_dossier, "ids-2026-09.jsonl")));
        Assert.True(File.Exists(Path.Combine(_dossier, "tokens-2026-06.jsonl")));   // pas à cet index
        Assert.True(File.Exists(Path.Combine(_dossier, "notes.txt")));
    }
}
