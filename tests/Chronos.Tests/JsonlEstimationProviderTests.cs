using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve DAT-05 (estimation par somme de tokens JSONL, marquee Estimated) enrichie en v1.1 :
/// la fenetre 5 h est desormais INFEREE ([start, now]) — l'arc retrouve longueur (ResetsAt =
/// start + 5 h, FractionTimeRemaining calcule, EST-01) et couleur si un plafond est defini
/// (Utilization = tokens / plafond, EST-03) ; sans plafond l'utilization reste null (honnetete
/// v1.0). Fenetre inactive → arc plein (fraction = 1, tokens = 0, EST-02). Prouve aussi ROB-02
/// (parsing tolerant : ligne corrompue, prose "five_hour", user ignoree ; dossier absent ; jamais
/// d'exception) et l'INCLUSION du sous-dossier subagents/ (meme pool de quota).
///
/// Horloge figee (FakeClock) a now = 2026-07-08T12:00:00Z, coherente avec les timestamps des fixtures.
/// Les fixtures vivent dans TestData/ ; le chemin est resolu via [CallerFilePath] (aucun couplage csproj).
/// Pour isoler un fichier unique du scan recursif, la fixture est copiee dans un dossier temp dedie.
/// Tests PURS (pas de Dispatcher) -> [Fact] classiques.
/// </summary>
public class JsonlEstimationProviderTests
{
    // now fixe des tests : 2026-07-08T12:00:00Z. Fenetre 5 h inferee -> reset attendu 16:30 (dernier msg 11:30).
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
    {
        // UsageFile place dans un dossier temp ISOLE et unique -> le SettingsFile colocalise resout
        // dans ce dossier (jamais le CWD du test host, ou un settings.json pourrait trainer et
        // rendre le test flaky). Aucun settings.json ecrit ici -> Load() renvoie les defauts
        // (plafonds null -> Utilization null, coherent avec les assertions ci-dessous).
        var isolatedUsage = Path.Combine(
            Path.GetTempPath(), "ChronosJsonlSettings_" + Guid.NewGuid().ToString("N"), "usage.json");
        var paths = new ChronosPaths(UsageFile: isolatedUsage, ProjectsRoot: projectsRoot);
        return new JsonlEstimationProvider(paths, new FakeClock(Now), new SettingsService(paths));
    }

    // --- EST-01 : fenetre 5 h inferee active -> somme bornee, ResetsAt/Fraction peuples, Estimated ---

    [Fact]
    public async Task Valide_somme_par_fenetre_et_marque_Estimated()
    {
        var provider = ProviderFor(IsolatedRootWith("sample-valid.jsonl"));

        var snap = await provider.GetAsync();

        // Fenetre 5 h ACTIVE (dernier message 11:30, now 12:00) : desormais peuplee (EST-01).
        // Somme = seule la ligne 11:30 dans la fenetre inferee [11:30, 12:00] : 1000+200+50+300 = 1550.
        Assert.Equal(1550L, snap.FiveHour.EstimatedTokens);
        // Reset infere = debut (11:30) + 5 h = 16:30.
        Assert.Equal(new DateTimeOffset(2026, 07, 08, 16, 30, 00, TimeSpan.Zero), snap.FiveHour.ResetsAt);
        // Fraction restante = (16:30 - 12:00) / 5 h = 0.9 -> l'arc retrouve une longueur.
        Assert.NotNull(snap.FiveHour.FractionTimeRemaining);
        Assert.Equal(0.9, snap.FiveHour.FractionTimeRemaining!.Value, 6);
        // Aucun plafond fourni -> utilization null (honnetete v1.0, pas de couleur inventee).
        Assert.Null(snap.FiveHour.Utilization);
        Assert.Equal(SourceReliability.Estimated, snap.FiveHour.Reliability);

        // Fenetre hebdo : somme 7 j glissants (1550 + 600 = 2150) ; reste null cote provider
        // (ResetsAt/Fraction remplis par WeeklyRecalibration cote VM — EST-05).
        Assert.Equal(2150L, snap.SevenDay.EstimatedTokens);
        Assert.True(snap.SevenDay.EstimatedTokens >= snap.FiveHour.EstimatedTokens);
        Assert.Null(snap.SevenDay.Utilization);
        Assert.Null(snap.SevenDay.ResetsAt);
        Assert.Null(snap.SevenDay.FractionTimeRemaining);
        Assert.Equal(SourceReliability.Estimated, snap.SevenDay.Reliability);
    }

    // --- EST-03 : utilization 5 h = tokens fenetre / plafond quand budget defini (pas de clamp haut) ---

    [Fact]
    public async Task Utilization_5h_estimee_avec_plafond()
    {
        // Dossier temp isole : la fixture 5 h active + un settings.json colocalise au UsageFile.
        var temp = Path.Combine(Path.GetTempPath(), "ChronosJsonlBudget_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        File.Copy(Path.Combine(TestDataDir(), "sample-valid.jsonl"), Path.Combine(temp, "sample-valid.jsonl"));
        File.WriteAllText(Path.Combine(temp, "settings.json"), "{\"FiveHourTokenBudget\":3100}");

        var paths = new ChronosPaths(UsageFile: Path.Combine(temp, "usage.json"), ProjectsRoot: temp);
        var provider = new JsonlEstimationProvider(paths, new FakeClock(Now), new SettingsService(paths));

        var snap = await provider.GetAsync();

        // 1550 / 3100 = 0.5 : utilization estimee, badge « estimee » conserve.
        Assert.NotNull(snap.FiveHour.Utilization);
        Assert.Equal(0.5, snap.FiveHour.Utilization!.Value, 9);
        Assert.Equal(SourceReliability.Estimated, snap.FiveHour.Reliability);
    }

    // --- CAL-03 : un plafond defini ne rend JAMAIS la fenetre Exact -> elle reste Estimated ---
    // (le badge « estimee » de l'UI reste donc du). L'utilization apparait (couleur) mais la
    // provenance ne ment pas : une somme JSONL calibree reste une ESTIMATION.

    [Fact]
    public async Task Plafond_defini_laisse_la_fenetre_5h_Estimated_avec_utilization()
    {
        // Fixture 5 h active + settings colocalise portant un FiveHourTokenBudget.
        var temp = Path.Combine(Path.GetTempPath(), "ChronosCal03_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        File.Copy(Path.Combine(TestDataDir(), "sample-valid.jsonl"), Path.Combine(temp, "sample-valid.jsonl"));
        File.WriteAllText(Path.Combine(temp, "settings.json"), "{\"FiveHourTokenBudget\":3100}");

        var paths = new ChronosPaths(UsageFile: Path.Combine(temp, "usage.json"), ProjectsRoot: temp);
        var provider = new JsonlEstimationProvider(paths, new FakeClock(Now), new SettingsService(paths));

        var snap = await provider.GetAsync();

        // CAL-03 : Reliability reste Estimated (jamais Exact) MALGRE le plafond...
        Assert.Equal(SourceReliability.Estimated, snap.FiveHour.Reliability);
        // ...et l'utilization est desormais connue (couleur), donc le badge « estimee » vient de l'UI.
        Assert.NotNull(snap.FiveHour.Utilization);
    }

    // --- EST-02 : fenetre inactive (dernier message > 5 h avant now) -> arc plein, jamais vide ---

    [Fact]
    public async Task Fenetre_inactive_arc_plein_sans_tokens()
    {
        // sample-inactive : un seul message a 06:00 (6 h avant now) -> reset 11:00 <= now -> inactive.
        var provider = ProviderFor(IsolatedRootWith("sample-inactive.jsonl"));

        var snap = await provider.GetAsync();

        Assert.Equal(1.0, snap.FiveHour.FractionTimeRemaining);   // arc plein (rien d'entame)
        Assert.Equal(0L, snap.FiveHour.EstimatedTokens);
        Assert.Null(snap.FiveHour.ResetsAt);
        Assert.Equal(SourceReliability.Estimated, snap.FiveHour.Reliability);
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
