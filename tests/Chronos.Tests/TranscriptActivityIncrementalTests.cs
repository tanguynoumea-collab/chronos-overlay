using System.IO;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// P-07 étape 1 / DS-PERF-01 (phase 42.3) : en mode dégradé, la passe sur les transcripts ne relit plus que
/// les fichiers dont (taille, date de modification) a changé. Ce fichier PROUVE que la passe incrémentale
/// d'un provider « chaud » (cache garni) rend un journal IDENTIQUE à celui d'un provider NEUF (passe
/// complète) au même instant — mêmes entrées, même ordre, même <c>Now</c>, même <c>Horizon</c> — et que le
/// verdict de <see cref="DoctrineFraicheur"/> est le même avec les deux journaux.
///
/// Compteurs de preuve : <c>FichiersRelusDernierePasse</c> / <c>FichiersReutilisesDernierePasse</c>.
///
/// Précaution « fichier chaud » : un fichier dont la mtime est à moins de 2 min du <c>now</c> de l'horloge
/// injectée est TOUJOURS relu. Les fixtures fraîchement écrites seraient donc toutes chaudes : les tests
/// A à E VIEILLISSENT explicitement chaque fixture (mtime = now − 10 min) après chaque écriture.
///
/// Isolation : toutes les fixtures vivent sous <see cref="Path.GetTempPath"/> (vérifié par
/// <c>Assert.StartsWith</c>) ; aucun accès au vrai dossier des transcripts de l'utilisateur.
/// </summary>
public sealed class TranscriptActivityIncrementalTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 04, 12, 00, 00, TimeSpan.Zero);

    private readonly string _racine;
    private readonly string _projets;
    private readonly FakeClock _horloge = new(Now);

    // Les cinq fixtures (3 projets, dont un sous-dossier subagents/).
    private readonly string _s1;   // projA : msg-1 (out 10), ligne non-assistant, ligne tronquée
    private readonly string _a1;   // projA/subagents : ligne assistant sans id
    private readonly string _s2;   // projB : msg-1 (out 50, même id que s1 → dédup inter-fichiers), msg-2
    private readonly string _s3;   // projB : ligne à horodatage futur (+2 min)
    private readonly string _s4;   // projC : msg-3

    public TranscriptActivityIncrementalTests()
    {
        _racine = Path.Combine(Path.GetTempPath(), "ChronosIncremental_" + Guid.NewGuid().ToString("N"));
        _projets = Path.Combine(_racine, "projects");
        Assert.StartsWith(Path.GetTempPath(), _projets);

        _s1 = Path.Combine(_projets, "projA", "s1.jsonl");
        _a1 = Path.Combine(_projets, "projA", "subagents", "a1.jsonl");
        _s2 = Path.Combine(_projets, "projB", "s2.jsonl");
        _s3 = Path.Combine(_projets, "projB", "s3.jsonl");
        _s4 = Path.Combine(_projets, "projC", "s4.jsonl");

        Ecrire(_s1,
            Assistant("msg-1", "req-1", Now.AddHours(-2), input: 100, output: 10),
            """{"type":"user","timestamp":"2026-10-04T10:01:00Z","message":{"role":"user","content":"bonjour"}}""",
            """{"type":"assistant","timestamp":"2026-10-04T10:02:00Z","message":{"role":"assis""");   // tronquée
        Ecrire(_a1, Assistant(null, null, Now.AddMinutes(-90), input: 7, output: 3));
        Ecrire(_s2,
            Assistant("msg-1", "req-1", Now.AddMinutes(-100), input: 100, output: 50),
            Assistant("msg-2", "req-2", Now.AddHours(-1), input: 20, output: 5));
        Ecrire(_s3, Assistant("msg-fut", "req-fut", Now.AddMinutes(2), input: 1000, output: 1));
        Ecrire(_s4, Assistant("msg-3", "req-3", Now.AddHours(-3), input: 40, output: 4));

        foreach (var f in Tous) Vieillir(f);
    }

    public void Dispose()
    {
        try { Directory.Delete(_racine, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private string[] Tous => new[] { _s1, _a1, _s2, _s3, _s4 };

    // ------------------------------------------------------------------ fabriques

    private static string Assistant(string? id, string? requestId, DateTimeOffset ts, long input, long output)
    {
        var idJson = id is null ? "" : $"\"id\":\"{id}\",";
        var reqJson = requestId is null ? "" : $"\"requestId\":\"{requestId}\",";
        return "{\"type\":\"assistant\"," + reqJson
             + $"\"timestamp\":\"{ts.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fffZ}\","
             + "\"message\":{" + idJson + "\"role\":\"assistant\","
             + $"\"usage\":{{\"input_tokens\":{input},\"output_tokens\":{output},"
             + "\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}";
    }

    private static void Ecrire(string chemin, params string[] lignes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);
        File.WriteAllText(chemin, string.Join("\n", lignes) + "\n");
    }

    private static void Ajouter(string chemin, params string[] lignes)
        => File.AppendAllText(chemin, string.Join("\n", lignes) + "\n");

    // mtime fixée par rapport à l'horloge INJECTÉE (jamais DateTime.UtcNow) : hors de la zone « chaude ».
    private void Vieillir(string chemin, TimeSpan? age = null)
        => File.SetLastWriteTimeUtc(chemin, (_horloge.UtcNow - (age ?? TimeSpan.FromMinutes(10))).UtcDateTime);

    private ChronosPaths Chemins()
    {
        var usage = Path.Combine(_racine, "paths", "usage.json");
        Assert.StartsWith(Path.GetTempPath(), usage);
        return new ChronosPaths(UsageFile: usage, ProjectsRoot: _projets);
    }

    private TranscriptActivityProvider Neuf() => new(Chemins(), _horloge);

    private static void AssertJournauxEgaux(TranscriptActivityLog attendu, TranscriptActivityLog obtenu)
    {
        Assert.Equal(attendu.Now, obtenu.Now);
        Assert.Equal(attendu.Horizon, obtenu.Horizon);
        Assert.Equal(attendu.Entrees, obtenu.Entrees);   // séquence : mêmes entrées, même ordre
    }

    // ------------------------------------------------------------------ A. cache réutilisé

    [Fact]
    public async Task A_seconde_passe_sans_changement_ne_relit_aucun_fichier()
    {
        var chaud = Neuf();

        var j1 = await chaud.ReadAsync();
        Assert.Equal(5, chaud.FichiersRelusDernierePasse);
        Assert.Equal(0, chaud.FichiersReutilisesDernierePasse);

        var j2 = await chaud.ReadAsync();
        Assert.Equal(0, chaud.FichiersRelusDernierePasse);
        Assert.Equal(5, chaud.FichiersReutilisesDernierePasse);

        AssertJournauxEgaux(j1, j2);

        // Garde-fou sur le jeu de fixtures lui-même : msg-1 dédupliqué entre deux fichiers (max par champ,
        // plus petit ts), ligne sans id séparée, ligne future écartée, tronquée et non-assistant ignorées.
        Assert.Equal(4, j2.Entrees.Count);
        Assert.Equal(150L, j2.Entrees.Single(e => e.Ts == Now.AddHours(-2)).Tokens);   // 100 + max(10, 50)
        Assert.DoesNotContain(j2.Entrees, e => e.Ts > Now);
    }

    // ------------------------------------------------------------------ B. append

    [Fact]
    public async Task B_ajout_de_lignes_ne_relit_que_le_fichier_modifie_et_egale_la_passe_complete()
    {
        var chaud = Neuf();
        await chaud.ReadAsync();

        Ajouter(_s4,
            Assistant("msg-4", "req-4", Now.AddMinutes(-30), input: 9, output: 1),
            Assistant("msg-4", "req-4", Now.AddMinutes(-29), input: 9, output: 8));
        Vieillir(_s4, TimeSpan.FromMinutes(9));

        var jChaud = await chaud.ReadAsync();
        Assert.Equal(1, chaud.FichiersRelusDernierePasse);
        Assert.Equal(4, chaud.FichiersReutilisesDernierePasse);

        var jNeuf = await Neuf().ReadAsync();
        AssertJournauxEgaux(jNeuf, jChaud);
        Assert.Contains(jChaud.Entrees, e => e.Ts == Now.AddMinutes(-30) && e.Tokens == 17);
    }

    // ------------------------------------------------------------------ C. suppression / horizon

    [Fact]
    public async Task C_fichier_supprime_ou_sorti_de_l_horizon_quitte_le_cache_et_le_journal()
    {
        var chaud = Neuf();
        await chaud.ReadAsync();
        Assert.Equal(5, chaud.FichiersEnCache);

        File.Delete(_a1);
        Vieillir(_s4, TimeSpan.FromDays(9));

        var jChaud = await chaud.ReadAsync();
        var jNeuf = await Neuf().ReadAsync();

        AssertJournauxEgaux(jNeuf, jChaud);
        Assert.Equal(3, chaud.FichiersEnCache);
        Assert.Equal(0, chaud.FichiersRelusDernierePasse);
        Assert.Equal(3, chaud.FichiersReutilisesDernierePasse);
        Assert.DoesNotContain(jChaud.Entrees, e => e.Ts == Now.AddHours(-3));      // msg-3 (s4) parti
        Assert.DoesNotContain(jChaud.Entrees, e => e.Ts == Now.AddMinutes(-90));   // ligne sans id (a1) partie
    }

    // ------------------------------------------------------------------ D. futur devenu passé

    [Fact]
    public async Task D_horodatage_futur_devenu_passe_apparait_au_rejeu_sans_relecture()
    {
        var chaud = Neuf();
        var j1 = await chaud.ReadAsync();
        Assert.DoesNotContain(j1.Entrees, e => e.Ts == Now.AddMinutes(2));

        _horloge.UtcNow = Now.AddMinutes(5);

        var jChaud = await chaud.ReadAsync();
        Assert.Equal(0, chaud.FichiersRelusDernierePasse);       // le filtre « futur » est appliqué AU REJEU
        var jNeuf = await Neuf().ReadAsync();

        AssertJournauxEgaux(jNeuf, jChaud);
        Assert.Contains(jChaud.Entrees, e => e.Ts == Now.AddMinutes(2) && e.Tokens == 1001);
    }

    // ------------------------------------------------------------------ E. doctrine

    private static WindowState Memorisee(DateTimeOffset capturee) => new()
    {
        Kind = WindowKind.FiveHour,
        Reliability = SourceReliability.Exact,
        Utilization = 0.42,
        CapturedAt = capturee,
        ResetsAt = Now.AddHours(3),
    };

    [Fact]
    public async Task E_le_verdict_de_la_doctrine_est_identique_avec_les_deux_journaux()
    {
        var vivante = WindowState.Unavailable(WindowKind.FiveHour);
        var memorisee = Memorisee(Now.AddMinutes(-10));          // vieilli de 10 min : au-delà de LimiteAge
        Assert.True(DoctrineFraicheur.ABesoinDuJournal(vivante, memorisee, Now));

        var chaud = Neuf();
        await chaud.ReadAsync();

        // Sans activité depuis la capture : EncoreValide des deux côtés.
        var jChaud = await chaud.ReadAsync();
        var jNeuf = await Neuf().ReadAsync();
        var vChaud = DoctrineFraicheur.Statuer(vivante, memorisee, jChaud, Now);
        var vNeuf = DoctrineFraicheur.Statuer(vivante, memorisee, jNeuf, Now);
        Assert.Equal(ProvenanceReleve.EncoreValide, vChaud.Provenance);
        Assert.Equal(vNeuf.Provenance, vChaud.Provenance);
        Assert.Equal(vNeuf.Utilization, vChaud.Utilization);
        Assert.Equal(vNeuf, vChaud);

        // Activité après la capture (ajout) : PlancherAvecActivite des deux côtés.
        Ajouter(_s1, Assistant("msg-5", "req-5", Now.AddMinutes(-1), input: 30, output: 3));
        Vieillir(_s1, TimeSpan.FromMinutes(9));

        jChaud = await chaud.ReadAsync();
        Assert.Equal(1, chaud.FichiersRelusDernierePasse);
        jNeuf = await Neuf().ReadAsync();
        vChaud = DoctrineFraicheur.Statuer(vivante, memorisee, jChaud, Now);
        vNeuf = DoctrineFraicheur.Statuer(vivante, memorisee, jNeuf, Now);
        Assert.Equal(ProvenanceReleve.PlancherAvecActivite, vChaud.Provenance);
        Assert.Equal(vNeuf.Provenance, vChaud.Provenance);
        Assert.Equal(vNeuf.Utilization, vChaud.Utilization);
        Assert.Equal(33L, vChaud.TokensDepuisReleve);
        Assert.Equal(vNeuf, vChaud);
    }

    // ------------------------------------------------------------------ F. fichier chaud

    [Fact]
    public async Task F_un_fichier_modifie_il_y_a_moins_de_2_min_est_toujours_relu()
    {
        Vieillir(_s2, TimeSpan.FromSeconds(30));                 // seul fichier « chaud »

        var chaud = Neuf();
        await chaud.ReadAsync();
        Assert.Equal(5, chaud.FichiersRelusDernierePasse);

        var j2 = await chaud.ReadAsync();
        Assert.Equal(1, chaud.FichiersRelusDernierePasse);       // taille et mtime identiques, mais chaud
        Assert.Equal(4, chaud.FichiersReutilisesDernierePasse);
        AssertJournauxEgaux(await Neuf().ReadAsync(), j2);

        _horloge.UtcNow = Now.AddMinutes(3);                     // mtime désormais à 3 min 30 : plus chaud

        await chaud.ReadAsync();
        Assert.Equal(0, chaud.FichiersRelusDernierePasse);
        Assert.Equal(5, chaud.FichiersReutilisesDernierePasse);
    }
}
