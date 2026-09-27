using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chronos.Models.Historique.Tokens;
using Chronos.Services;
using Chronos.Services.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// TOK-02 / TOK-03 — la reconstruction de fond des agrégats de tokens. Ce que ces tests gravent : une passe lit les
/// transcripts du plus récent au plus ancien (mtime décroissant), écrit la semaine courante AVANT l'historique
/// (checkpoint par mtime, Pitfall 7), dans l'ordre ids → agrégats → curseurs ; l'incrémental n'ouvre que ce qui a bougé
/// (grandi : depuis l'offset ; raccourci : de zéro sans rien recompter ; disparu : retiré sans rien soustraire) ; un
/// échec se dit (<c>EnEchec</c>, <c>DerniereErreur</c>) sans tuer le service, et la passe suivante réessaie.
///
/// Racine de test = copies des fixtures réelles anonymisées de 33-02 sous un dossier temporaire, mtimes posés à la main
/// autour d'une horloge fausse (2026-09-27T12:00Z). Jamais %APPDATA%, jamais ~/.claude : lecture des fixtures, écriture
/// sous le temp seulement.
/// </summary>
public sealed class ReconstructionTokensTests : IDisposable
{
    private static readonly DateTimeOffset Now = Utc("2026-09-27T12:00:00Z");
    private static readonly UTF8Encoding Utf8SansBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "chronos-tests-reconstruction-" + Guid.NewGuid().ToString("N"));

    // Les six tranches de la racine de test (horloge 2026-09-27T12:00Z), dans l'ordre d'écriture du magasin (slot, modèle, origine).
    private static readonly TrancheTokens[] Attendues =
    {
        new(Utc("2026-09-20T08:00:00Z"), "claude-opus-5", false, 5, 50, 500, 2500, 1),        // sous-agent/session-b.jsonl (principal)
        new(Utc("2026-09-20T08:00:00Z"), "claude-sonnet-5", true, 60, 600, 3000, 18000, 3),   // agent-x.jsonl (sous-agent)
        new(Utc("2026-09-21T10:45:00Z"), "claude-opus-5", false, 7, 256, 70, 700, 1),         // a-cheval-tranche (slot du premier timestamp)
        new(Utc("2026-09-21T14:00:00Z"), "claude-opus-5", false, 6, 60, 600, 6000, 3),        // fork A (+ copies dans B à delta 0)
        new(Utc("2026-09-21T14:15:00Z"), "claude-opus-5", false, 9, 90, 900, 9000, 2),        // fork B (FORK04, FORK05)
        new(Utc("2026-09-22T11:15:00Z"), "claude-opus-4-1", false, 13, 297, 35005, 41741, 3), // multi-blocs décalé
    };

    public ReconstructionTokensTests() => Directory.CreateDirectory(_temp);

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { }
    }

    // --- Aides ---

    private static DateTimeOffset Utc(string iso)
        => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static string Fixture(string cas, string fichier, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "transcripts", cas, fichier);

    /// <summary>Un contexte isolé : <c>{temp}/{nom}/usage.json</c> (donc HistoriqueDir = <c>{temp}/{nom}/historique</c>) et la
    /// racine des projets <c>{temp}/{nom}/projets</c>, peuplée de la racine de test standard (clés <c>proj/…</c>).</summary>
    private (ChronosPaths Paths, string Racine) Contexte(string nom, bool peupler = true)
    {
        var dossier = Path.Combine(_temp, nom);
        var racine = Path.Combine(dossier, "projets");
        var paths = new ChronosPaths(Path.Combine(dossier, "usage.json"), racine);
        if (peupler) Peupler(racine);
        return (paths, racine);
    }

    private static void Peupler(string racine)
    {
        var proj = Path.Combine(racine, "proj");
        Copier(Fixture("multi-blocs", "session-a.jsonl"), Path.Combine(proj, "s1.jsonl"), Now - TimeSpan.FromHours(1), decaler: true);
        Copier(Fixture("sous-agent", "session-b.jsonl"), Path.Combine(proj, "s2.jsonl"), Now - TimeSpan.FromDays(2));
        Copier(Fixture("sous-agent", Path.Combine("session-b", "subagents", "agent-x.jsonl")),
               Path.Combine(proj, "s2", "subagents", "agent-x.jsonl"), Now - TimeSpan.FromDays(2));
        Copier(Fixture("fork-copie", "session-b.jsonl"), Path.Combine(proj, "fb.jsonl"), Now - TimeSpan.FromDays(9));
        Copier(Fixture("fork-copie", "session-a.jsonl"), Path.Combine(proj, "fa.jsonl"), Now - TimeSpan.FromDays(10));
        Copier(Fixture("a-cheval-tranche", "session-e.jsonl"), Path.Combine(proj, "old.jsonl"), Now - TimeSpan.FromDays(20));
    }

    // Le multi-blocs réel date du 2026-07-08 : décalé au 2026-09-22 à la copie (même longueur, mêmes offsets) pour tomber dans le mois testé.
    private static void Copier(string source, string destination, DateTimeOffset mtime, bool decaler = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (decaler)
            File.WriteAllText(destination, File.ReadAllText(source, Utf8SansBom).Replace("2026-07-08T", "2026-09-22T"), Utf8SansBom);
        else
            File.Copy(source, destination, overwrite: true);
        File.SetLastWriteTimeUtc(destination, mtime.UtcDateTime);
    }

    private static ReconstructionTokens Service(ChronosPaths paths, FakeClock? clock = null, Action<string>? apresFichier = null,
                                                Action<string>? apresEtapeFlush = null, TimeSpan? cadence = null)
    {
        clock ??= new FakeClock(Now);
        return new ReconstructionTokens(paths, new MagasinAgregats(paths.HistoriqueDir, clock), new IndexMessages(paths.HistoriqueDir, clock),
                                        clock, cadence, apresFichier, apresEtapeFlush);
    }

    private static string CheminTokens(ChronosPaths paths) => Path.Combine(paths.HistoriqueDir, "tokens-2026-09.jsonl");
    private static string CheminCurseurs(ChronosPaths paths) => Path.Combine(paths.HistoriqueDir, Curseurs.NomFichier);
    private static string CheminCouverture(ChronosPaths paths) => Path.Combine(paths.HistoriqueDir, CouvertureTokens.NomFichier);

    private static List<TrancheTokens> Tranches(string chemin)
    {
        var tranches = new List<TrancheTokens>();
        foreach (var ligne in File.ReadAllLines(chemin))
        {
            Assert.True(LigneAgregat.Parser(ligne, out var t) && t is not null, "ligne d'agrégat illisible : " + ligne);
            tranches.Add(t!);
        }
        return tranches;
    }

    private static string Sha256(string chemin) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(chemin)));

    private static Dictionary<string, (long Offset, long Taille)> LireCurseurs(string chemin)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(chemin));
        var resultat = new Dictionary<string, (long, long)>(StringComparer.Ordinal);
        foreach (var p in doc.RootElement.GetProperty("fichiers").EnumerateObject())
            resultat[p.Name] = (p.Value.GetProperty("offset").GetInt64(), p.Value.GetProperty("taille").GetInt64());
        return resultat;
    }

    // --- Task 2 : une passe, l'ordre, l'incrémental, l'échec ---

    [Fact]
    public void Une_passe_complete_produit_les_six_tranches_attendues_les_curseurs_la_couverture_et_l_index()
    {
        var (paths, _) = Contexte("complete");
        var service = Service(paths);

        var bilan = service.ExecuterUnePasse(CancellationToken.None);

        Assert.True(bilan.Complete);
        Assert.Equal(Attendues, Tranches(CheminTokens(paths)));

        var curseurs = LireCurseurs(CheminCurseurs(paths));
        Assert.Equal(6, curseurs.Count);
        Assert.Contains("proj/s1.jsonl", curseurs.Keys);
        Assert.Contains("proj/s2/subagents/agent-x.jsonl", curseurs.Keys);
        Assert.All(curseurs.Values, c => Assert.Equal(c.Taille, c.Offset));

        var couverture = CouvertureTokens.Charger(CheminCouverture(paths));
        Assert.Equal(Utc("2026-09-20T08:02:10Z"), couverture.PlusAncienneLigneVue);
        var intervalle = Assert.Single(couverture.Intervalles);
        Assert.Equal(Now - TimeSpan.FromDays(30), intervalle.Debut);
        Assert.True(intervalle.Fin >= Now);

        Assert.True(File.Exists(Path.Combine(paths.HistoriqueDir, "ids-2026-09.jsonl")));

        Assert.Equal(PhaseReconstruction.Incremental, service.Phase);
        Assert.Equal(6, service.FichiersTotal);
        Assert.Equal(6, service.FichiersTraites);
        Assert.Equal(6, service.FichiersOuvertsDernierePasse);
        Assert.True(service.SemaineCouranteDisponible);
        Assert.Equal(13, service.IdsConnus);   // 2 + 1 (sans id, clé synthétique) + 1 + 3 + 5 + 1
        Assert.NotNull(service.DureeMurDernierePasse);
        Assert.NotNull(service.DureeCpuProcessusDernierePasse);
        Assert.Equal(Now, service.DerniereReconstructionTerminee);
        Assert.Null(service.DerniereErreur);
    }

    [Fact]
    public void Les_fichiers_sont_lus_par_mtime_decroissant_et_la_semaine_courante_est_disponible_avant_l_historique()
    {
        var (paths, _) = Contexte("ordre");
        var journal = new List<(string Cle, bool Semaine, List<TrancheTokens>? Ecrites)>();
        ReconstructionTokens service = null!;
        service = Service(paths, apresFichier: cle => journal.Add((
            cle,
            service.SemaineCouranteDisponible,
            File.Exists(CheminTokens(paths)) ? Tranches(CheminTokens(paths)) : null)));

        service.ExecuterUnePasse(CancellationToken.None);

        Assert.Equal(6, journal.Count);
        Assert.Equal("proj/s1.jsonl", journal[0].Cle);
        Assert.Equal(new HashSet<string> { "proj/s2.jsonl", "proj/s2/subagents/agent-x.jsonl" }, new HashSet<string> { journal[1].Cle, journal[2].Cle });
        Assert.Equal("proj/fb.jsonl", journal[3].Cle);
        Assert.Equal("proj/fa.jsonl", journal[4].Cle);
        Assert.Equal("proj/old.jsonl", journal[5].Cle);

        // La semaine courante n'est « disponible » qu'au premier fichier plus vieux qu'une semaine : fb (now − 9 j).
        Assert.All(journal.Take(3), j => Assert.False(j.Semaine));
        Assert.All(journal.Skip(3), j => Assert.True(j.Semaine));

        // Rien n'est encore écrit pendant la semaine ; au callback de fb, le checkpoint a écrit les 3 tranches de s1 + s2 — pas encore fa / old.
        Assert.All(journal.Take(3), j => Assert.Null(j.Ecrites));
        Assert.Equal(new[] { Attendues[0], Attendues[1], Attendues[5] }, journal[3].Ecrites);
    }

    [Fact]
    public void L_incremental_ne_rouvre_que_ce_qui_a_bouge_et_relit_un_fichier_grandi_depuis_son_offset()
    {
        var (paths, racine) = Contexte("incremental");
        var service = Service(paths);
        service.ExecuterUnePasse(CancellationToken.None);
        var sha1 = Sha256(CheminTokens(paths));

        var bilan2 = service.ExecuterUnePasse(CancellationToken.None);

        Assert.Equal(0, bilan2.FichiersOuverts);
        Assert.Equal(0, service.FichiersOuvertsDernierePasse);
        Assert.Equal(6, service.FichiersTraites);
        Assert.Equal(sha1, Sha256(CheminTokens(paths)));

        // FORK06 : même gabarit que FORK01, autres compteurs, 14:22Z → tranche 14:15Z.
        var fa = Path.Combine(racine, "proj", "fa.jsonl");
        var premiere = File.ReadLines(fa, Utf8SansBom).First();
        var fork06 = premiere
            .Replace("FORK000000000000001", "FORK000000000000006")
            .Replace("\"input_tokens\":1,", "\"input_tokens\":6,")
            .Replace("\"output_tokens\":10,", "\"output_tokens\":60,")
            .Replace("\"cache_creation_input_tokens\":100,", "\"cache_creation_input_tokens\":600,")
            .Replace("\"cache_read_input_tokens\":1000,", "\"cache_read_input_tokens\":6000,")
            .Replace("2026-09-21T14:00:00", "2026-09-21T14:22:00");
        Assert.NotEqual(premiere, fork06);
        File.AppendAllText(fa, fork06 + "\n", Utf8SansBom);
        File.SetLastWriteTimeUtc(fa, (Now - TimeSpan.FromMinutes(1)).UtcDateTime);

        var bilan3 = service.ExecuterUnePasse(CancellationToken.None);

        Assert.Equal(1, bilan3.FichiersOuverts);
        Assert.Equal(1, service.FichiersOuvertsDernierePasse);
        var attendues = Attendues.ToArray();
        attendues[4] = attendues[4] with { In = 15, Out = 150, CacheW = 1500, CacheR = 15000, N = 3 };
        Assert.Equal(attendues, Tranches(CheminTokens(paths)));
    }

    [Fact]
    public void Un_fichier_raccourci_est_relu_de_zero_sans_rien_recompter()
    {
        var (paths, racine) = Contexte("raccourci");
        var service = Service(paths);
        service.ExecuterUnePasse(CancellationToken.None);
        var sha1 = Sha256(CheminTokens(paths));

        // fa réécrit avec sa seule première ligne : taille < offset connu → relu de zéro ; FORK01 est connu → delta 0.
        var fa = Path.Combine(racine, "proj", "fa.jsonl");
        var premiere = File.ReadLines(fa, Utf8SansBom).First();
        File.WriteAllText(fa, premiere + "\n", Utf8SansBom);
        File.SetLastWriteTimeUtc(fa, (Now - TimeSpan.FromSeconds(30)).UtcDateTime);

        var bilan = service.ExecuterUnePasse(CancellationToken.None);

        Assert.Equal(1, bilan.FichiersOuverts);
        Assert.Equal(1, bilan.RaccourcisRelus);
        Assert.Equal(1, service.FichiersOuvertsDernierePasse);
        Assert.Equal(1, service.RaccourcisRelusDernierePasse);
        Assert.Equal(sha1, Sha256(CheminTokens(paths)));
    }

    [Fact]
    public void Un_fichier_disparu_est_retire_des_curseurs_et_rien_n_est_soustrait()
    {
        var (paths, racine) = Contexte("disparu");
        var service = Service(paths);
        service.ExecuterUnePasse(CancellationToken.None);

        File.Delete(Path.Combine(racine, "proj", "old.jsonl"));
        var bilan = service.ExecuterUnePasse(CancellationToken.None);

        Assert.Equal(1, bilan.Disparus);
        Assert.Equal(1, service.FichiersDisparus);
        Assert.Equal(5, service.FichiersTotal);
        Assert.Equal(5, LireCurseurs(CheminCurseurs(paths)).Count);
        // L'historique déjà journalisé survit à une purge : la tranche 10:45Z (old.jsonl) est toujours là.
        Assert.Contains(Attendues[2], Tranches(CheminTokens(paths)));
        Assert.Equal(Attendues, Tranches(CheminTokens(paths)));
    }

    [Fact]
    public void Un_dossier_historique_poison_met_la_passe_en_echec_sans_lever_et_la_suivante_reessaie()
    {
        var (paths, _) = Contexte("poison");
        Directory.CreateDirectory(Path.GetDirectoryName(paths.HistoriqueDir)!);
        File.WriteAllText(paths.HistoriqueDir, "un fichier porte le nom du dossier historique", Utf8SansBom);
        var service = Service(paths);

        var bilan = service.ExecuterUnePasse(CancellationToken.None);

        Assert.False(bilan.Complete);
        Assert.Equal(PhaseReconstruction.EnEchec, service.Phase);
        Assert.NotNull(service.DerniereErreur);
        Assert.Matches(new Regex(@"^\w+Exception : "), service.DerniereErreur);

        File.Delete(paths.HistoriqueDir);
        var bilan2 = service.ExecuterUnePasse(CancellationToken.None);

        Assert.True(bilan2.Complete);
        Assert.Equal(PhaseReconstruction.Incremental, service.Phase);
        Assert.Null(service.DerniereErreur);
        Assert.Equal(Attendues, Tranches(CheminTokens(paths)));
    }

    [Fact]
    public void Une_racine_absente_donne_une_passe_vide_sans_lever()
    {
        var (paths, racine) = Contexte("absente", peupler: false);
        Assert.False(Directory.Exists(racine));
        var service = Service(paths);

        var bilan = service.ExecuterUnePasse(CancellationToken.None);

        Assert.True(bilan.Complete);
        Assert.Equal(0, bilan.FichiersTotal);
        Assert.Equal(0, service.FichiersTotal);
        Assert.Equal(PhaseReconstruction.Incremental, service.Phase);
        Assert.True(service.SemaineCouranteDisponible);
        Assert.Null(service.DerniereErreur);
        Assert.False(Directory.Exists(paths.HistoriqueDir) && Directory.EnumerateFiles(paths.HistoriqueDir, "tokens-*.jsonl").Any(),
                     "aucun fichier d'agrégats ne doit naître d'une passe vide");
    }
}
