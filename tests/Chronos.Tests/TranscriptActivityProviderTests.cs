using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve DEL-01 (« y a-t-il eu une reponse assistant depuis T ? ») et DEL-02 (« combien de tokens
/// depuis T ? ») sur les fixtures REELLES, c'est-a-dire avec le parcours disque complet : streaming
/// tolerant, sous-dossier subagents/, dossier absent, filtre d'horizon.
///
/// Ce fichier ne contient plus AUCUNE assertion de pourcentage : les transcripts ne produisent plus
/// d'utilisation absolue (EXA-04). Le bornage lui-meme est prouve en PUR dans
/// <see cref="TranscriptActivityLogTests"/> — ici on prouve que la passe disque alimente correctement
/// ce bornage.
///
/// Horloge figee (FakeClock) a now = 2026-07-08T12:00:00Z, coherente avec les timestamps des fixtures.
/// Les fixtures vivent dans TestData/ ; le chemin est resolu via [CallerFilePath] (aucun couplage csproj).
/// Pour isoler un fichier unique du scan recursif, la fixture est copiee dans un dossier temp dedie.
/// Tests PURS (pas de Dispatcher) -> [Fact] classiques.
/// </summary>
public class TranscriptActivityProviderTests
{
    // now fixe des tests : 2026-07-08T12:00:00Z. Dernier message des fixtures : 11:30 le meme jour.
    private static readonly DateTimeOffset Now = new(2026, 07, 08, 12, 00, 00, TimeSpan.Zero);

    // Bornes basses utilisees par les tests, nommees pour dire ce qu'elles representent.
    private static readonly DateTimeOffset DepuisReleve5h = new(2026, 07, 08, 06, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset DepuisReleveHebdo = new(2026, 07, 01, 00, 00, 00, TimeSpan.Zero);

    private static string TestDataDir([CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "TestData");

    // Copie une fixture unique dans un dossier temp isole -> ProjectsRoot ne voit QUE ce fichier.
    private static string IsolatedRootWith(string fixtureFile)
    {
        var temp = Path.Combine(Path.GetTempPath(), "ChronosTranscriptTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        File.Copy(Path.Combine(TestDataDir(), fixtureFile), Path.Combine(temp, fixtureFile));
        return temp;
    }

    private static TranscriptActivityProvider ProviderFor(string projectsRoot)
    {
        // UsageFile place dans un dossier temp ISOLE et unique : la source de delta ne lit plus
        // AUCUN reglage, mais ChronosPaths colocalise ses autres fichiers avec usage.json — un
        // chemin temp garantit qu'aucun test ne peut toucher le vrai profil de l'utilisateur.
        var isolatedUsage = Path.Combine(
            Path.GetTempPath(), "ChronosTranscriptPaths_" + Guid.NewGuid().ToString("N"), "usage.json");
        var paths = new ChronosPaths(UsageFile: isolatedUsage, ProjectsRoot: projectsRoot);
        return new TranscriptActivityProvider(paths, new FakeClock(Now));
    }

    // --- GARANTIE STRUCTURELLE (EXA-04) : la source de delta n'est PAS un fournisseur d'usage ---

    [Fact]
    public void Le_provider_de_transcripts_n_est_pas_un_IUsageProvider()
    {
        // Un futur developpeur qui « rebrancherait » les transcripts dans la chaine composite casse
        // ce test : c'est la seule facon de rendre impossible le retour de l'utilisation derivee
        // d'un comptage de tokens, plutot que de compter sur la vigilance.
        Assert.False(typeof(IUsageProvider).IsAssignableFrom(typeof(TranscriptActivityProvider)));
    }

    // --- DEL-02 : tokens depuis T, borne sur la fenetre 5 h ---

    [Fact]
    public async Task Tokens_depuis_T_borne_sur_la_fenetre_5h()
    {
        var provider = ProviderFor(IsolatedRootWith("sample-valid.jsonl"));

        var log = await provider.ReadAsync();
        var delta = log.Since(DepuisReleve5h);

        // Seule la ligne 11:30 est posterieure a 06:00 : 1000+200+50+300 = 1550.
        Assert.Equal(1550L, delta.Tokens);
        Assert.True(delta.HasActivity);
    }

    // --- DEL-02 : la MEME passe disque repond aussi pour la fenetre hebdo, avec une autre borne ---

    [Fact]
    public async Task Tokens_depuis_T_borne_sur_la_fenetre_hebdo()
    {
        var provider = ProviderFor(IsolatedRootWith("sample-valid.jsonl"));

        var log = await provider.ReadAsync();
        var delta = log.Since(DepuisReleveHebdo);

        // 1550 (08/07 11:30) + 600 (05/07) = 2150 ; le message du 01/06 est hors de la fenetre.
        Assert.Equal(2150L, delta.Tokens);
    }

    // --- Anti double-comptage : la borne basse est STRICTEMENT exclusive ---

    [Fact]
    public async Task Borne_basse_strictement_exclusive()
    {
        var provider = ProviderFor(IsolatedRootWith("sample-valid.jsonl"));

        var log = await provider.ReadAsync();
        // T = l'instant EXACT du dernier message : ce message a deja ete compte par le serveur au
        // moment de la capture exacte, le recompter le compterait deux fois.
        var delta = log.Since(new DateTimeOffset(2026, 07, 08, 11, 30, 00, TimeSpan.Zero));

        Assert.Equal(0L, delta.Tokens);
        Assert.False(delta.HasActivity);
    }

    // --- Sous-produit gratuit de la meme passe : l'instant de la derniere reponse assistant ---

    [Fact]
    public async Task Derniere_activite_exposee()
    {
        var provider = ProviderFor(IsolatedRootWith("sample-valid.jsonl"));

        var log = await provider.ReadAsync();
        var delta = log.Since(DepuisReleveHebdo);

        Assert.Equal(new DateTimeOffset(2026, 07, 08, 11, 30, 00, TimeSpan.Zero), delta.LastActivityAt);
    }

    // --- DEL-01 : aucune activite depuis T -> booleen faux, aucun token, aucun instant ---

    [Fact]
    public async Task Aucune_activite_depuis_T()
    {
        // sample-inactive : un seul message a 06:00, donc rien apres 07:00.
        var provider = ProviderFor(IsolatedRootWith("sample-inactive.jsonl"));

        var log = await provider.ReadAsync();
        var delta = log.Since(new DateTimeOffset(2026, 07, 08, 07, 00, 00, TimeSpan.Zero));

        Assert.False(delta.HasActivity);
        Assert.Equal(0L, delta.Tokens);
        Assert.Null(delta.LastActivityAt);
    }

    // --- ROB-02 : corrompue / partielle / prose / user ignorees, aucune exception ---

    [Fact]
    public async Task Tolerant_ignore_corrompue_partielle_prose_et_user_sans_exception()
    {
        var provider = ProviderFor(IsolatedRootWith("sample-tolerant.jsonl"));

        // Ne doit PAS lever d'exception (ROB-02).
        var log = await provider.ReadAsync();
        var delta = log.Since(DepuisReleveHebdo);

        // Seule la ligne assistant valide (11:00 ; 400+100+100+100 = 700) est comptee.
        // La prose "five_hour" (ligne user), la ligne corrompue et la derniere ligne tronquee sont ignorees.
        Assert.Equal(700L, delta.Tokens);
    }

    // --- Arbitrage subagents/ : le scan recursif AllDirectories additionne le fichier du sous-dossier ---

    [Fact]
    public async Task Subagents_inclus_dans_la_somme_recursive()
    {
        // ProjectsRoot = TestData/SubagentsRoot : session.jsonl (500) + subagents/agent-abc.jsonl (300).
        var provider = ProviderFor(Path.Combine(TestDataDir(), "SubagentsRoot"));

        var log = await provider.ReadAsync();
        var delta = log.Since(DepuisReleveHebdo);

        // 500 + 300 = 800 : PROUVE l'inclusion du sous-dossier subagents/ (meme pool de quota).
        Assert.Equal(800L, delta.Tokens);
    }

    // --- ROB-02 : dossier ProjectsRoot inexistant -> journal vide, sans exception ---

    [Fact]
    public async Task Dossier_absent_renvoie_un_journal_vide_sans_exception()
    {
        var provider = ProviderFor(Path.Combine(Path.GetTempPath(), "ChronosNoSuchDir_" + Guid.NewGuid().ToString("N")));

        var log = await provider.ReadAsync();
        var delta = log.Since(DepuisReleveHebdo);

        Assert.False(delta.HasActivity);
        Assert.Equal(0L, delta.Tokens);
    }

    // --- Honnetete du delta : le journal declare ce qu'il ne sait PAS couvrir (filtre mtime 8 j) ---

    [Fact]
    public async Task Horizon_borne_a_huit_jours()
    {
        var provider = ProviderFor(IsolatedRootWith("sample-valid.jsonl"));

        var log = await provider.ReadAsync();

        // Un releve exact vieux de 30 jours ne peut PAS etre corrige par ce journal : le dire est la
        // seule alternative honnete a un delta silencieusement sous-evalue.
        Assert.False(log.Covers(Now - TimeSpan.FromDays(30)));
        Assert.True(log.Covers(Now - TimeSpan.FromDays(1)));
        Assert.Equal(Now - TimeSpan.FromDays(8), log.Horizon);
    }
}
