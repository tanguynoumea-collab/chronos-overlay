using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve que la correction par delta HÉRITE de la dédup CPT-01 SANS CODE PROPRE : la tête
/// <see cref="LastExactUsageProvider"/> statue par <see cref="DoctrineFraicheur"/>, qui reçoit
/// <c>journal.Since(t).Tokens</c> du VRAI <see cref="TranscriptActivityProvider"/> lu sur la fixture
/// réelle multi-blocs. Si la passe disque compte un message une fois, <c>TokensDepuisReleve</c> vaut le
/// total dédoublonné (77 056) ; aucune ligne de doctrine n'a changé pour cela.
///
/// Contrairement à <c>LastExactUsageProviderTests</c> (journal programmé par un fake), on branche ICI
/// le provider réel : c'est le chaînage complet disque → journal → doctrine qui est sous preuve.
///
/// Isolation stricte : magasin ET transcripts dans des dossiers temp uniques — jamais %APPDATA%\Chronos,
/// jamais ~/.claude. Tests purs (aucun type WPF) → [Fact] classiques.
/// </summary>
public sealed class DedupHeritageDeltaTests : IDisposable
{
    // Même « now » que TranscriptActivityProviderTests : la fixture vit entre 11:20 et 11:28 le même jour.
    private static readonly DateTimeOffset Now = new(2026, 07, 08, 12, 00, 00, TimeSpan.Zero);

    private readonly string _dir;
    private readonly LastExactStore _store;

    public DedupHeritageDeltaTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ChronosDedupHeritage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new LastExactStore(Path.Combine(_dir, "last-exact.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    // --- Montage : recopie privée des helpers de TranscriptActivityProviderTests (insertions seulement là-bas) ---

    private static string TestDataDir([CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "TestData");

    // Copie une fixture unique dans un dossier temp isolé -> ProjectsRoot ne voit QUE ce fichier (mtime frais).
    private static string IsolatedRootWith(string fixtureFile)
    {
        var temp = Path.Combine(Path.GetTempPath(), "ChronosDedupHeritageRoot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        File.Copy(Path.Combine(TestDataDir(), fixtureFile), Path.Combine(temp, fixtureFile));
        return temp;
    }

    private static TranscriptActivityProvider ProviderFor(string projectsRoot)
    {
        var isolatedUsage = Path.Combine(
            Path.GetTempPath(), "ChronosDedupHeritagePaths_" + Guid.NewGuid().ToString("N"), "usage.json");
        var paths = new ChronosPaths(UsageFile: isolatedUsage, ProjectsRoot: projectsRoot);
        return new TranscriptActivityProvider(paths, new FakeClock(Now));
    }

    // Relevé 5 h EXACT de la sonde, pris à capturedAt : 30 %, reset dans 3 h.
    private static WindowState Releve5h(DateTimeOffset capturedAt) => new()
    {
        Kind = WindowKind.FiveHour,
        Utilization = 0.30,
        ResetsAt = Now.AddHours(3),
        CapturedAt = capturedAt,
        Reliability = SourceReliability.Exact,
        Source = SourceUsage.SondeEnTetes,
    };

    // Magasin garni d'un relevé exact daté, inner muet (Empty), activité = VRAI provider sur la fixture réelle.
    private async Task<UsageSnapshot> StatuerAvecReleveA(DateTimeOffset capturedAt)
    {
        _store.Save(new UsageSnapshot
        {
            FiveHour = Releve5h(capturedAt),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        });

        var inner = new FakeUsageProvider { Next = UsageSnapshot.Empty };
        var activite = ProviderFor(IsolatedRootWith("transcript-multi-blocs.jsonl"));
        var tete = new LastExactUsageProvider(inner, _store, new FakeClock(Now), activite);

        return await tete.GetAsync();
    }

    // --- Le chiffre du plancher est le total DÉDOUBLONNÉ, sans que la doctrine ait changé ---

    [Fact]
    public async Task TokensDepuisReleve_herite_de_la_dedup_sans_code_propre()
    {
        // Relevé pris à 11:00 (âge 60 min > LimiteAge 6 min) : la doctrine consulte le journal.
        var snap = await StatuerAvecReleveA(new DateTimeOffset(2026, 07, 08, 11, 00, 00, TimeSpan.Zero));

        var cinq = snap.FiveHour;
        Assert.Equal(SourceReliability.Estimated, cinq.Reliability);
        Assert.Equal(ProvenanceReleve.PlancherAvecActivite, cinq.Provenance);
        Assert.Equal(0.30, cinq.Utilization);                 // EXA-04 : jamais gonflé d'un delta
        // 77 004 (un message multi-blocs, max par champ) + 50 (repli requestId) + 2 (sans identifiant).
        Assert.Equal(77056L, cinq.TokensDepuisReleve);
    }

    // --- Non-régression DEL-03 : aucune activité après le relevé → encore exact, zéro token ---

    [Fact]
    public async Task Un_releve_pris_apres_le_dernier_message_reste_exact_sans_activite()
    {
        // Relevé à 11:29, dernier message de la fixture à 11:28 : rien dans ]11:29 ; 12:00].
        var snap = await StatuerAvecReleveA(new DateTimeOffset(2026, 07, 08, 11, 29, 00, TimeSpan.Zero));

        var cinq = snap.FiveHour;
        Assert.Equal(SourceReliability.Exact, cinq.Reliability);
        Assert.Equal(ProvenanceReleve.EncoreValide, cinq.Provenance);
        Assert.Equal(0L, cinq.TokensDepuisReleve);
    }
}
