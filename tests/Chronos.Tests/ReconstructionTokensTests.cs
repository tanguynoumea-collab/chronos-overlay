using System.Diagnostics;
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

    // --- Task 3 : les preuves de fond — le thread, l'annulation, la reprise sur les octets, la panne entre deux étapes ---

    /// <summary>Pitfall 1, écrit en test : la priorité et le nom sont capturés SUR le thread qui lit les fichiers. Une continuation
    /// asynchrone (pool) rendrait Normal et un autre nom — c'est la mutation (a).</summary>
    [Fact]
    public async Task Le_thread_de_fond_est_IsBackground_BelowNormal_nomme_et_StartAsync_rend_la_main_avant_la_fin()
    {
        var (paths, _) = Contexte("thread");
        using var atteint = new ManualResetEventSlim(false);
        using var liberer = new ManualResetEventSlim(false);
        (ThreadPriority Priorite, bool Fond, string? Nom)? capture = null;
        var service = Service(paths, apresFichier: _ =>
        {
            if (capture is not null) return;
            capture = (Thread.CurrentThread.Priority, Thread.CurrentThread.IsBackground, Thread.CurrentThread.Name);
            atteint.Set();
            liberer.Wait(TimeSpan.FromSeconds(10));   // le premier fichier reste « en cours » tant que le test ne libère pas
        });

        var chrono = Stopwatch.StartNew();
        await service.StartAsync(CancellationToken.None);
        chrono.Stop();

        Assert.True(chrono.ElapsedMilliseconds < 50, $"StartAsync a pris {chrono.ElapsedMilliseconds} ms : la passe ne doit pas s'exécuter inline");
        Assert.True(atteint.Wait(TimeSpan.FromSeconds(10)), "le thread de fond n'a pas atteint le premier fichier");
        Assert.Equal(1, service.FichiersTraites);   // le callback est encore bloqué : rien n'a avancé au-delà du premier fichier

        liberer.Set();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StopAsync(cts.Token);

        Assert.Equal((ThreadPriority.BelowNormal, true, ReconstructionTokens.NomThread), capture);
        Assert.Equal(PhaseReconstruction.Arretee, service.Phase);
        service.Dispose();
    }

    [Fact]
    public void L_annulation_entre_deux_fichiers_est_honoree_en_moins_de_200_ms_et_un_dernier_flush_a_lieu()
    {
        var (paths, racine) = Contexte("annulation", peupler: false);
        var source = File.ReadAllBytes(Fixture("fork-copie", "session-a.jsonl"));
        var proj = Path.Combine(racine, "proj");
        Directory.CreateDirectory(proj);
        for (var i = 0; i < 300; i++)
        {
            var chemin = Path.Combine(proj, "c" + i.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".jsonl");
            File.WriteAllBytes(chemin, source);
            File.SetLastWriteTimeUtc(chemin, (Now - TimeSpan.FromMinutes(i + 1)).UtcDateTime);   // mtimes décroissants distincts
        }
        using var cts = new CancellationTokenSource();
        var appels = 0;
        var service = Service(paths, apresFichier: _ => { if (++appels == 3) cts.Cancel(); });

        var chrono = Stopwatch.StartNew();
        var bilan = service.ExecuterUnePasse(cts.Token);
        chrono.Stop();

        Assert.True(chrono.ElapsedMilliseconds < 200, $"l'annulation a été honorée en {chrono.ElapsedMilliseconds} ms");
        Assert.False(bilan.Complete);
        Assert.Equal(300, service.FichiersTotal);
        Assert.InRange(service.FichiersTraites, 3, 4);
        Assert.NotEqual(PhaseReconstruction.EnEchec, service.Phase);

        // Dernier flush : les curseurs des fichiers lus sont là ; une passe annulée ne garantit aucune couverture.
        Assert.True(File.Exists(CheminCurseurs(paths)));
        Assert.Equal(service.FichiersTraites, LireCurseurs(CheminCurseurs(paths)).Count);
        Assert.True(!File.Exists(CheminCouverture(paths)) || CouvertureTokens.Charger(CheminCouverture(paths)).Intervalles.Count == 0,
                    "une passe annulée ne doit garantir aucun intervalle de couverture");
    }

    /// <summary>TOK-03 prouvée de bout en bout : annulée après k fichiers, puis une instance NEUVE (nouveaux magasin et index, même
    /// dossier) — les octets de <c>tokens-2026-09.jsonl</c> sont ceux d'une passe ininterrompue, et les k fichiers déjà persistés ne
    /// sont pas rouverts.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Une_reprise_apres_annulation_donne_des_fichiers_identiques_octet_pour_octet(int k)
    {
        var (reference, _) = Contexte("reference-" + k);
        Assert.True(Service(reference).ExecuterUnePasse(CancellationToken.None).Complete);
        var shaReference = Sha256(CheminTokens(reference));
        var lignesIdsReference = File.ReadAllLines(Path.Combine(reference.HistoriqueDir, "ids-2026-09.jsonl")).Length;

        var (paths, _) = Contexte("reprise-" + k);
        using var cts = new CancellationTokenSource();
        var appels = 0;
        var premiere = Service(paths, apresFichier: _ => { if (++appels == k) cts.Cancel(); });
        var bilan1 = premiere.ExecuterUnePasse(cts.Token);
        Assert.False(bilan1.Complete);
        Assert.Equal(k, premiere.FichiersTraites);

        var seconde = Service(paths);
        var bilan2 = seconde.ExecuterUnePasse(CancellationToken.None);

        Assert.True(bilan2.Complete);
        Assert.Equal(shaReference, Sha256(CheminTokens(paths)));
        Assert.Equal(6, Curseurs.Charger(CheminCurseurs(paths), paths.ProjectsRoot).Count);
        Assert.Equal(6 - k, seconde.FichiersOuvertsDernierePasse);   // les k fichiers persistés par la première instance sont « inchangés »
        Assert.True(File.ReadAllLines(Path.Combine(paths.HistoriqueDir, "ids-2026-09.jsonl")).Length >= lignesIdsReference);
        Assert.Equal(13, seconde.IdsConnus);   // des lignes en plus sont possibles, jamais un id compté deux fois
    }

    [Fact]
    public void Une_panne_juste_apres_l_ecriture_des_ids_puis_une_reprise_donnent_les_memes_agregats()
        => PanneEntreDeuxEtapes(rang: 1, etapeAttendue: "ids", fichierJamaisEcrit: CheminTokens);

    [Fact]
    public void Une_panne_juste_apres_l_ecriture_des_agregats_puis_une_reprise_donnent_les_memes_agregats()
        => PanneEntreDeuxEtapes(rang: 2, etapeAttendue: "agregats", fichierJamaisEcrit: CheminCurseurs);

    // D-33-14 prouvé par l'ordre observé des étapes ET par les octets après reprise.
    private void PanneEntreDeuxEtapes(int rang, string etapeAttendue, Func<ChronosPaths, string> fichierJamaisEcrit)
    {
        var (reference, _) = Contexte("reference-panne-" + etapeAttendue);
        Assert.True(Service(reference).ExecuterUnePasse(CancellationToken.None).Complete);
        var shaReference = Sha256(CheminTokens(reference));

        var (paths, _) = Contexte("panne-" + etapeAttendue);
        var appels = 0;
        string? etapeVue = null;
        var premiere = Service(paths, apresEtapeFlush: etape =>
        {
            if (++appels != rang) return;
            etapeVue = etape;
            throw new IOException("panne simulée");
        });

        var bilan1 = premiere.ExecuterUnePasse(CancellationToken.None);

        Assert.False(bilan1.Complete);
        Assert.Equal(PhaseReconstruction.EnEchec, premiere.Phase);
        Assert.NotNull(premiere.DerniereErreur);
        Assert.Contains("IOException", premiere.DerniereErreur);
        Assert.Equal(etapeAttendue, etapeVue);
        Assert.False(File.Exists(fichierJamaisEcrit(paths)), fichierJamaisEcrit(paths) + " n'aurait jamais dû être écrit");

        var seconde = Service(paths);
        var bilan2 = seconde.ExecuterUnePasse(CancellationToken.None);

        Assert.True(bilan2.Complete);
        Assert.Equal(PhaseReconstruction.Incremental, seconde.Phase);
        Assert.Equal(shaReference, Sha256(CheminTokens(paths)));
        Assert.Equal(13, seconde.IdsConnus);
    }

    [Fact]
    public void Un_jeton_deja_annule_ne_lit_rien_et_ne_compte_rien()
    {
        var (paths, _) = Contexte("deja-annule");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = Service(paths);

        var bilan = service.ExecuterUnePasse(cts.Token);

        Assert.False(bilan.Complete);
        Assert.Equal(0, bilan.FichiersOuverts);
        Assert.Equal(0, service.FichiersTraites);
        Assert.Equal(6, service.FichiersTotal);
        Assert.False(File.Exists(CheminTokens(paths)));
        Assert.NotEqual(PhaseReconstruction.EnEchec, service.Phase);
        Assert.Null(service.DerniereErreur);
    }

    // --- DATA-3 (phase 42.2) : une brique illisible interrompt la passe SANS flush, la passe suivante rejoue ---

    private static readonly TrancheTokens TrancheSemee = new(Utc("2026-09-15T10:00:00Z"), "claude-opus-5", false, 1, 2, 3, 4, 1);

    // Un état disque cohérent d'avant : un id dans le shard de septembre et sa tranche dans tokens-2026-09.jsonl.
    private static void SemerSeptembre(ChronosPaths paths)
    {
        var clock = new FakeClock(Now);
        var index = new IndexMessages(paths.HistoriqueDir, clock);
        Assert.NotNull(index.Ajouter(new MessageLu("msg_seme", Utc("2026-09-15T10:02:00Z"), "claude-opus-5", false, 1, 2, 3, 4)));
        Assert.True(index.Flush());
        var magasin = new MagasinAgregats(paths.HistoriqueDir, clock);
        Assert.True(magasin.Appliquer(new DeltaTranche(TrancheSemee.Slot, "claude-opus-5", false, 1, 2, 3, 4, true)));
        Assert.True(magasin.EcrireMoisSales());
    }

    private static string CheminShardSeptembre(ChronosPaths paths) => Path.Combine(paths.HistoriqueDir, "ids-2026-09.jsonl");

    private static FileStream Tenir(string chemin) => new(chemin, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    [Fact]
    public void Un_shard_d_ids_tenu_au_demarrage_interrompt_la_passe_sans_rien_ecrire_puis_la_passe_suivante_rejoue()
    {
        var (paths, _) = Contexte("shard-tenu");
        SemerSeptembre(paths);
        var octets = File.ReadAllBytes(CheminTokens(paths));
        var service = Service(paths);

        BilanPasse bilan;
        using (Tenir(CheminShardSeptembre(paths)))
            bilan = service.ExecuterUnePasse(CancellationToken.None);

        Assert.False(bilan.Complete);
        Assert.Equal(0, bilan.FichiersOuverts);   // aucune lecture de transcript sur un index incomplet
        Assert.Equal(PhaseReconstruction.EnEchec, service.Phase);
        Assert.NotNull(service.DerniereErreur);
        Assert.Contains("index", service.DerniereErreur);
        Assert.Contains("2026-09", service.DerniereErreur);
        Assert.Equal(octets, File.ReadAllBytes(CheminTokens(paths)));
        Assert.False(File.Exists(CheminCurseurs(paths)));

        // Shard libéré : la même instance rejoue l'initialisation, l'id semé est connu, rien n'est perdu.
        var bilan2 = service.ExecuterUnePasse(CancellationToken.None);
        Assert.True(bilan2.Complete);
        Assert.Equal(PhaseReconstruction.Incremental, service.Phase);
        Assert.Equal(new[] { TrancheSemee }.Concat(Attendues).ToArray(), Tranches(CheminTokens(paths)));
    }

    [Fact]
    public void Un_shard_d_ids_non_vide_sans_ligne_valide_interrompt_la_passe_sans_reprojeter()
    {
        var (paths, _) = Contexte("shard-texte");
        SemerSeptembre(paths);
        File.WriteAllText(CheminShardSeptembre(paths), "ceci n'est pas du JSON\n", Utf8SansBom);
        var octets = File.ReadAllBytes(CheminTokens(paths));
        var service = Service(paths);

        var bilan = service.ExecuterUnePasse(CancellationToken.None);

        Assert.False(bilan.Complete);
        Assert.Equal(PhaseReconstruction.EnEchec, service.Phase);
        Assert.Contains("index", service.DerniereErreur);
        Assert.Equal(octets, File.ReadAllBytes(CheminTokens(paths)));
        Assert.False(File.Exists(CheminCurseurs(paths)));
    }

    [Fact]
    public void Un_shard_d_ids_de_zero_octet_ne_fait_pas_perdre_les_agregats_existants_du_mois()
    {
        // 0 octet = index vide légitime pour l'index ; mais un mois ouvert dont l'index ne sait RIEN alors que son fichier
        // d'agrégats porte des tranches n'est pas reprojeté (il serait réécrit vide) : il est relu tel quel.
        var (paths, _) = Contexte("shard-zero");
        SemerSeptembre(paths);
        File.WriteAllBytes(CheminShardSeptembre(paths), Array.Empty<byte>());
        var service = Service(paths);

        var bilan = service.ExecuterUnePasse(CancellationToken.None);

        Assert.True(bilan.Complete);
        Assert.Equal(new[] { TrancheSemee }.Concat(Attendues).ToArray(), Tranches(CheminTokens(paths)));
    }

    [Fact]
    public void Un_mois_d_agregats_ancien_tenu_interrompt_la_passe_sans_flush_puis_la_passe_suivante_l_applique()
    {
        var (paths, racine) = Contexte("agregats-tenus", peupler: false);
        var juinSeme = new TrancheTokens(Utc("2026-06-10T10:00:00Z"), "claude-opus-5", false, 1, 2, 3, 4, 1);
        var magasinSeme = new MagasinAgregats(paths.HistoriqueDir, new FakeClock(Now));
        Assert.True(magasinSeme.Appliquer(new DeltaTranche(juinSeme.Slot, "claude-opus-5", false, 1, 2, 3, 4, true)));
        Assert.True(magasinSeme.EcrireMoisSales());
        // DATA-1 : un mois gelé déjà agrégé n'accepte de deltas que si son index d'ids est sur disque (sinon : déjà compté).
        var indexSeme = new IndexMessages(paths.HistoriqueDir, new FakeClock(Now));
        Assert.NotNull(indexSeme.Ajouter(new MessageLu("msg_juin_seme", Utc("2026-06-10T10:02:00Z"), "claude-opus-5", false, 1, 2, 3, 4)));
        Assert.True(indexSeme.Flush());
        var cheminJuin = Path.Combine(paths.HistoriqueDir, "tokens-2026-06.jsonl");
        var octets = File.ReadAllBytes(cheminJuin);

        // Un transcript porte le multi-blocs, décalé en juin (mois gelé : hors des mois ouverts de l'index).
        var transcript = Path.Combine(racine, "proj", "juin.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
        File.WriteAllText(transcript, File.ReadAllText(Fixture("multi-blocs", "session-a.jsonl"), Utf8SansBom).Replace("2026-07-08T", "2026-06-22T"), Utf8SansBom);
        File.SetLastWriteTimeUtc(transcript, (Now - TimeSpan.FromHours(1)).UtcDateTime);
        var service = Service(paths);

        BilanPasse bilan;
        using (Tenir(cheminJuin))
            bilan = service.ExecuterUnePasse(CancellationToken.None);

        Assert.False(bilan.Complete);
        Assert.Equal(PhaseReconstruction.EnEchec, service.Phase);
        Assert.Contains("agrégats", service.DerniereErreur);
        Assert.Contains("2026-06", service.DerniereErreur);
        Assert.Equal(octets, File.ReadAllBytes(cheminJuin));
        Assert.False(File.Exists(CheminCurseurs(paths)));

        // Libéré : la même instance rejoue depuis l'état disque — le delta n'est ni perdu ni compté deux fois.
        var bilan2 = service.ExecuterUnePasse(CancellationToken.None);
        Assert.True(bilan2.Complete);
        Assert.Equal(new[] { juinSeme, new TrancheTokens(Utc("2026-06-22T11:15:00Z"), "claude-opus-4-1", false, 13, 297, 35005, 41741, 3) },
                     Tranches(cheminJuin));

        // Et une troisième passe ne recompte rien.
        Assert.True(service.ExecuterUnePasse(CancellationToken.None).Complete);
        Assert.Equal(2, Tranches(cheminJuin).Count);
        Assert.Equal(13, Tranches(cheminJuin)[1].In);
    }

    // --- DATA-1 / DATA-2 (phase 42.2) : un mois gelé n'est jamais doublé, un curseur ne dépasse jamais l'index persisté ---

    // Le multi-blocs réel (13 / 297 / 35005 / 41741, trois messages) décalé au jour voulu, mtime récent.
    private static void TranscriptDuJour(string racine, string nom, string jour)
    {
        var transcript = Path.Combine(racine, "proj", nom);
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
        File.WriteAllText(transcript, File.ReadAllText(Fixture("multi-blocs", "session-a.jsonl"), Utf8SansBom).Replace("2026-07-08T", jour + "T"), Utf8SansBom);
        File.SetLastWriteTimeUtc(transcript, (Now - TimeSpan.FromHours(1)).UtcDateTime);
    }

    [Fact]
    public void Relire_un_transcript_d_un_mois_gele_deja_agrege_ne_modifie_pas_ce_mois()
    {
        // Mars 2026 : hors des mois ouverts (août, septembre) ET antérieur à la rétention des shards (juin) — son shard
        // d'ids disparaît à la purge du démarrage suivant ; seul le fichier d'agrégats en garde la trace.
        var (paths, racine) = Contexte("gele-relu", peupler: false);
        TranscriptDuJour(racine, "mars.jsonl", "2026-03-22");
        var cheminMars = Path.Combine(paths.HistoriqueDir, "tokens-2026-03.jsonl");

        var premiere = Service(paths);
        Assert.True(premiere.ExecuterUnePasse(CancellationToken.None).Complete);
        Assert.Equal(0, premiere.MessagesIgnoresMoisGeles);
        var octets = File.ReadAllBytes(cheminMars);

        File.Delete(CheminCurseurs(paths));   // curseurs perdus : tout sera relu de zéro
        var seconde = Service(paths);         // nouveau démarrage
        var bilan = seconde.ExecuterUnePasse(CancellationToken.None);

        Assert.True(bilan.Complete);
        Assert.Equal(1, bilan.FichiersOuverts);
        Assert.Equal(octets, File.ReadAllBytes(cheminMars));
        Assert.True(seconde.MessagesIgnoresMoisGeles >= 1);
        Assert.Null(seconde.DerniereErreur);
    }

    [Fact]
    public void Un_mois_hors_fenetre_dont_le_shard_existe_est_charge_a_la_demande_et_la_relecture_reste_idempotente()
    {
        // Juillet 2026 : hors des mois ouverts, mais dans la rétention des shards (juin et après sont gardés).
        var (paths, racine) = Contexte("paresseux", peupler: false);
        TranscriptDuJour(racine, "juillet.jsonl", "2026-07-22");
        var cheminJuillet = Path.Combine(paths.HistoriqueDir, "tokens-2026-07.jsonl");

        Assert.True(Service(paths).ExecuterUnePasse(CancellationToken.None).Complete);
        Assert.True(File.Exists(Path.Combine(paths.HistoriqueDir, "ids-2026-07.jsonl")));
        var octets = File.ReadAllBytes(cheminJuillet);

        File.Delete(CheminCurseurs(paths));
        var seconde = Service(paths);
        Assert.True(seconde.ExecuterUnePasse(CancellationToken.None).Complete);

        Assert.Equal(octets, File.ReadAllBytes(cheminJuillet));
        Assert.Equal(0, seconde.MessagesIgnoresMoisGeles);   // dédoublonné par l'index chargé à la demande, pas ignoré
    }

    [Fact]
    public void La_premiere_reconstruction_d_un_mois_ancien_reste_possible()
    {
        var (paths, racine) = Contexte("premiere-ancienne", peupler: false);
        TranscriptDuJour(racine, "mars.jsonl", "2026-03-22");
        var service = Service(paths);

        Assert.True(service.ExecuterUnePasse(CancellationToken.None).Complete);

        Assert.Equal(new[] { new TrancheTokens(Utc("2026-03-22T11:15:00Z"), "claude-opus-4-1", false, 13, 297, 35005, 41741, 3) },
                     Tranches(Path.Combine(paths.HistoriqueDir, "tokens-2026-03.jsonl")));
        Assert.Equal(0, service.MessagesIgnoresMoisGeles);
    }

    [Fact]
    public void Un_index_non_ecrit_bloque_les_agregats_et_les_curseurs()
    {
        // Un DOSSIER au nom du shard de septembre : l'écriture de l'index échoue.
        var (paths, _) = Contexte("index-bloque");
        Directory.CreateDirectory(CheminShardSeptembre(paths));
        var service = Service(paths);

        var bilan = service.ExecuterUnePasse(CancellationToken.None);

        Assert.Equal(PhaseReconstruction.EnEchec, service.Phase);
        Assert.NotNull(service.DerniereErreur);
        Assert.StartsWith("index", service.DerniereErreur);
        Assert.False(File.Exists(CheminTokens(paths)), "les agrégats ne doivent pas être écrits si l'index ne l'est pas");
        Assert.False(File.Exists(CheminCurseurs(paths)), "les curseurs ne doivent pas dépasser l'index persisté");
        Assert.True(bilan.FichiersOuverts > 0);
    }

    // --- FIAB-R4 (42.2-11) : pour un mois GELÉ, les agrégats s'écrivent AVANT que ses ids ne soient persistés ---

    // Le multi-blocs décalé au jour voulu, sous un AUTRE id de message (un second message distinct du même mois).
    private static void TranscriptDuJourAutreId(string racine, string nom, string jour)
    {
        var transcript = Path.Combine(racine, "proj", nom);
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
        File.WriteAllText(transcript, File.ReadAllText(Fixture("multi-blocs", "session-a.jsonl"), Utf8SansBom)
                                          .Replace("2026-07-08T", jour + "T")
                                          .Replace("msg_01EXEMPLEMULTIBLOCS", "msg_01AUTREIDMULTIBLOCS")
                                          .Replace("req_01EXEMPLE", "req_01AUTREID"), Utf8SansBom);   // requestId : repli d'id sans message.id
        File.SetLastWriteTimeUtc(transcript, (Now - TimeSpan.FromMinutes(30)).UtcDateTime);
    }

    // Un DOSSIER au nom du temporaire d'écriture de juillet : l'écriture des agrégats de ce mois échoue (le fichier existant,
    // lui, reste lisible — le mois se charge normalement).
    private static string BloquerEcritureJuillet(ChronosPaths paths)
    {
        var tmp = Path.Combine(paths.HistoriqueDir, "tokens-2026-07.jsonl") + ".tmp-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Directory.CreateDirectory(tmp);
        return tmp;
    }

    private byte[] ReferenceJuilletDeuxMessages()
    {
        var (reference, racineRef) = Contexte("ref-gele-deux", peupler: false);
        TranscriptDuJour(racineRef, "juillet-a.jsonl", "2026-07-22");
        TranscriptDuJourAutreId(racineRef, "juillet-b.jsonl", "2026-07-23");
        Assert.True(Service(reference).ExecuterUnePasse(CancellationToken.None).Complete);
        return File.ReadAllBytes(Path.Combine(reference.HistoriqueDir, "tokens-2026-07.jsonl"));
    }

    /// <summary>FIAB-R4 : index d'un mois gelé + échec d'écriture de ses agrégats → les ids de ce mois ne sont PAS persistés ;
    /// au redémarrage, le transcript est relu et le delta appliqué UNE fois (avant : ids écrits, delta perdu pour toujours).</summary>
    [Fact]
    public void Un_mois_gele_dont_les_agregats_echouent_ne_persiste_pas_ses_ids_et_le_redemarrage_applique_le_delta_une_fois()
    {
        var attendu = ReferenceJuilletDeuxMessages();
        var (paths, racine) = Contexte("gele-agregats-echec", peupler: false);
        TranscriptDuJour(racine, "juillet-a.jsonl", "2026-07-22");
        Assert.True(Service(paths).ExecuterUnePasse(CancellationToken.None).Complete);   // juillet : agrégé + shard présent
        var shard = Path.Combine(paths.HistoriqueDir, "ids-2026-07.jsonl");
        Assert.True(File.Exists(shard));

        TranscriptDuJourAutreId(racine, "juillet-b.jsonl", "2026-07-23");
        var tmp = BloquerEcritureJuillet(paths);
        var enPanne = Service(paths);   // nouveau démarrage : juillet est GELÉ (hors août/septembre, fichier présent)
        enPanne.ExecuterUnePasse(CancellationToken.None);

        Assert.Equal(PhaseReconstruction.EnEchec, enPanne.Phase);
        Assert.StartsWith("agrégats", enPanne.DerniereErreur);
        Assert.DoesNotContain("AUTREID", File.ReadAllText(shard));   // ids du mois gelé NON persistés

        Directory.Delete(tmp);
        var reprise = Service(paths);   // redémarrage : la mémoire est perdue, le disque doit suffire
        Assert.True(reprise.ExecuterUnePasse(CancellationToken.None).Complete);

        Assert.Equal(attendu, File.ReadAllBytes(Path.Combine(paths.HistoriqueDir, "tokens-2026-07.jsonl")));
        Assert.Contains("AUTREID", File.ReadAllText(shard));
    }

    /// <summary>FIAB-R4 : même panne, mais la reprise a lieu dans le MÊME processus (passe suivante) : le delta est appliqué une
    /// seule fois — ni perdu, ni doublé.</summary>
    [Fact]
    public void Un_mois_gele_dont_les_agregats_echouent_est_rattrape_une_seule_fois_par_la_passe_suivante()
    {
        var attendu = ReferenceJuilletDeuxMessages();
        var (paths, racine) = Contexte("gele-agregats-echec-meme", peupler: false);
        TranscriptDuJour(racine, "juillet-a.jsonl", "2026-07-22");
        Assert.True(Service(paths).ExecuterUnePasse(CancellationToken.None).Complete);

        TranscriptDuJourAutreId(racine, "juillet-b.jsonl", "2026-07-23");
        var tmp = BloquerEcritureJuillet(paths);
        var service = Service(paths);
        service.ExecuterUnePasse(CancellationToken.None);
        Assert.Equal(PhaseReconstruction.EnEchec, service.Phase);

        Directory.Delete(tmp);
        Assert.True(service.ExecuterUnePasse(CancellationToken.None).Complete);

        Assert.Equal(attendu, File.ReadAllBytes(Path.Combine(paths.HistoriqueDir, "tokens-2026-07.jsonl")));
    }
}
