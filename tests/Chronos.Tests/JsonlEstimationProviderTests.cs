using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve DAT-05 (estimation par somme de tokens JSONL en fenetre glissante, marquee Estimated,
/// Utilization/ResetsAt null — jamais inventees) et ROB-02 (parsing tolerant : ligne corrompue,
/// derniere ligne partielle, ligne non-assistant et prose "five_hour" ignorees, dossier absent,
/// jamais d'exception). Prouve aussi l'INCLUSION intentionnelle du sous-dossier subagents/ dans la
/// somme (meme pool de quota — arbitrage phase 3, scan recursif AllDirectories, aucun filtre).
///
/// Horloge figee (FakeClock) a now = 2026-07-08T12:00:00Z, coherente avec les timestamps des fixtures.
/// Les fixtures vivent dans TestData/ ; le chemin est resolu via [CallerFilePath] (aucun couplage csproj).
/// Pour isoler un fichier unique du scan recursif, la fixture est copiee dans un dossier temp dedie.
/// Tests PURS (pas de Dispatcher) -> [Fact] classiques.
/// </summary>
public class JsonlEstimationProviderTests
{
    // now fixe des tests : 2026-07-08T12:00:00Z. Fenetre 5 h -> depuis 07:00:00Z ; 7 j -> depuis 2026-07-01T12:00:00Z.
    private static readonly DateTimeOffset Now = new(2026, 07, 08, 12, 00, 00, TimeSpan.Zero);

    private static string TestDataDir([CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "TestData");

    // Copie une fixture unique dans un dossier temp isole -> ProjectsRoot ne voit QUE ce fichier.
    private static string IsolatedRootWith(string fixtureFile)
    {
        var temp = Path.Combine(Path.GetTempPath(), "ChronosJsonlTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        File.Copy(Path.Combine(TestDataDir(), fixtureFile), Path.Combine(temp, fixtureFile));
        return temp;
    }

    private static JsonlEstimationProvider ProviderFor(string projectsRoot)
        => new(new ChronosPaths(UsageFile: "N/A", ProjectsRoot: projectsRoot), new FakeClock(Now));

    // --- DAT-05 : somme par fenetre glissante, toujours Estimated, sans valeur inventee ---

    [Fact]
    public async Task Valide_somme_par_fenetre_et_marque_Estimated()
    {
        var provider = ProviderFor(IsolatedRootWith("sample-valid.jsonl"));

        var snap = await provider.GetAsync();

        // 5 h : seule la ligne 11:30 compte -> 1000+200+50+300 = 1550. La ligne >7j (juin) est exclue.
        Assert.Equal(1550L, snap.FiveHour.EstimatedTokens);
        // 7 j : ligne 11:30 (1550) + ligne du 05/07 (500+100 = 600) = 2150 ; la ligne de juin reste exclue.
        Assert.Equal(2150L, snap.SevenDay.EstimatedTokens);
        // Fenetre plus large >= fenetre 5 h.
        Assert.True(snap.SevenDay.EstimatedTokens >= snap.FiveHour.EstimatedTokens);

        // Les DEUX fenetres : Estimated, sans utilization / reset / fraction inventes.
        foreach (var w in new[] { snap.FiveHour, snap.SevenDay })
        {
            Assert.Equal(SourceReliability.Estimated, w.Reliability);
            Assert.Null(w.Utilization);
            Assert.Null(w.ResetsAt);
            Assert.Null(w.FractionTimeRemaining);
        }
    }

    // --- ROB-02 : corrompue / partielle / prose / user ignorees, aucune exception ---

    [Fact]
    public async Task Tolerant_ignore_corrompue_partielle_prose_et_user_sans_exception()
    {
        var provider = ProviderFor(IsolatedRootWith("sample-tolerant.jsonl"));

        // Ne doit PAS lever d'exception (ROB-02).
        var snap = await provider.GetAsync();

        // Seule la ligne assistant valide (11:00 ; 400+100+100+100 = 700) est comptee.
        // La prose "five_hour" (ligne user), la ligne corrompue et la derniere ligne tronquee sont ignorees.
        Assert.Equal(700L, snap.FiveHour.EstimatedTokens);
        Assert.Equal(700L, snap.SevenDay.EstimatedTokens);
        Assert.Equal(SourceReliability.Estimated, snap.FiveHour.Reliability);
    }

    // --- Arbitrage subagents/ : le scan recursif AllDirectories additionne le fichier du sous-dossier ---

    [Fact]
    public async Task Subagents_inclus_dans_la_somme_recursive()
    {
        // ProjectsRoot = TestData/SubagentsRoot : session.jsonl (500) + subagents/agent-abc.jsonl (300).
        var provider = ProviderFor(Path.Combine(TestDataDir(), "SubagentsRoot"));

        var snap = await provider.GetAsync();

        // 500 (session principale) + 300 (sous-agent) = 800 : PROUVE l'inclusion du sous-dossier subagents/.
        Assert.Equal(800L, snap.FiveHour.EstimatedTokens);
        Assert.Equal(SourceReliability.Estimated, snap.FiveHour.Reliability);
    }

    // --- ROB-02 : dossier ProjectsRoot inexistant -> deux fenetres Estimated a 0 token, sans exception ---

    [Fact]
    public async Task Dossier_absent_renvoie_zero_sans_exception()
    {
        var provider = ProviderFor(Path.Combine(Path.GetTempPath(), "ChronosNoSuchDir_" + Guid.NewGuid().ToString("N")));

        var snap = await provider.GetAsync();

        Assert.Equal(0L, snap.FiveHour.EstimatedTokens);
        Assert.Equal(0L, snap.SevenDay.EstimatedTokens);
        Assert.Equal(SourceReliability.Estimated, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Estimated, snap.SevenDay.Reliability);
    }
}
