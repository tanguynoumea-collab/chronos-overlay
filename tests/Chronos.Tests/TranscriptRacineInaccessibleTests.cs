using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// DS3-02 (re-vérification dev-senior n° 3, décision D-01) : une racine des transcripts PRÉSENTE mais INACCESSIBLE ne doit
/// jamais fabriquer un journal vide qui certifierait « exact — encore valide ». Elle fait échouer la passe ; la tête
/// (LastExactUsageProvider) convertit l'échec en « Indisponible ». Une racine ABSENTE reste le cas normal d'une machine sans
/// Claude Code : journal vide sans exception — hypothèse écrite au §4 de docs/data-sources.md (D-02), épinglée ici.
///
/// Le refus est surtout prouvé par INJECTION de l'énumérateur (ctor internal) : levée immédiate (.NET ouvre la racine dès la
/// construction de l'énumération) et levée paresseuse (sous-dossier refusé pendant l'itération). Un test complémentaire pose un
/// vrai refus d'ACL sur un dossier temporaire dont l'utilisateur courant est propriétaire (aucun droit admin requis).
///
/// Tous les dossiers vivent sous Path.GetTempPath() (vérifié par Assert.StartsWith) et sont nettoyés ; jamais
/// ChronosPaths.Default(), jamais le vrai profil.
/// </summary>
public sealed class TranscriptRacineInaccessibleTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 04, 12, 00, 00, TimeSpan.Zero);

    private readonly List<string> _aNettoyer = new();

    public void Dispose()
    {
        foreach (var d in _aNettoyer)
        {
            try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { /* nettoyage best-effort */ }
        }
    }

    // Dossier unique sous le temp ; créé seulement si demandé (la racine absente ne l'est pas).
    private string DossierTemp(string prefixe, bool creer)
    {
        var d = Path.Combine(Path.GetTempPath(), prefixe + Guid.NewGuid().ToString("N"));
        Assert.StartsWith(Path.GetTempPath(), d);
        _aNettoyer.Add(d);
        if (creer) Directory.CreateDirectory(d);
        return d;
    }

    private ChronosPaths Chemins(string racine)
        => new(UsageFile: Path.Combine(DossierTemp("ChronosRacinePaths_", creer: false), "usage.json"), ProjectsRoot: racine);

    private static string LigneAssistant(DateTimeOffset quand)
        => "{\"type\":\"assistant\",\"timestamp\":\"" + quand.UtcDateTime.ToString("o")
           + "\",\"message\":{\"role\":\"assistant\",\"id\":\"msg_ds302\",\"usage\":{\"input_tokens\":10,\"output_tokens\":5}}}";

    // --- Le provider seul ---

    [Fact]
    public async Task Une_racine_presente_dont_l_enumeration_leve_tout_de_suite_fait_echouer_la_passe()
    {
        var racine = DossierTemp("ChronosRacine_", creer: true);
        var p = new TranscriptActivityProvider(Chemins(racine), new FakeClock(Now),
            _ => throw new UnauthorizedAccessException("accès refusé (test)"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => p.ReadAsync());
    }

    [Fact]
    public async Task Une_racine_presente_en_partage_refuse_fait_echouer_la_passe()
    {
        var racine = DossierTemp("ChronosRacine_", creer: true);
        var p = new TranscriptActivityProvider(Chemins(racine), new FakeClock(Now),
            _ => throw new IOException("partage refusé (test)"));

        await Assert.ThrowsAsync<IOException>(() => p.ReadAsync());
    }

    // Épingle que la levée PARESSEUSE (sous-dossier refusé pendant l'itération, hors de tout try) remonte : elle
    // remontait déjà avant la correction — le commentaire « jamais d'exception » était faux dans ce sens aussi.
    [Fact]
    public async Task Une_levee_pendant_l_iteration_paresseuse_fait_echouer_la_passe()
    {
        var racine = DossierTemp("ChronosRacine_", creer: true);
        var fichier = Path.Combine(racine, "session.jsonl");
        File.WriteAllText(fichier, LigneAssistant(Now.AddMinutes(-1)) + "\n");

        static IEnumerable<string> PuisRefus(string f)
        {
            yield return f;
            throw new UnauthorizedAccessException("sous-dossier refusé (test)");
        }

        var p = new TranscriptActivityProvider(Chemins(racine), new FakeClock(Now), _ => PuisRefus(fichier));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => p.ReadAsync());
    }

    [Fact]
    public async Task Une_racine_absente_rend_un_journal_vide_sans_exception()
    {
        var racine = DossierTemp("ChronosRacine_", creer: false);
        var appels = 0;
        var p = new TranscriptActivityProvider(Chemins(racine), new FakeClock(Now), _ =>
        {
            appels++;
            throw new InvalidOperationException("ne doit pas être appelé");
        });

        var journal = await p.ReadAsync();

        Assert.True(journal.Covers(Now));
        Assert.False(journal.Since(Now.AddHours(-1)).HasActivity);
        Assert.Equal(0L, journal.Since(Now.AddHours(-1)).Tokens);
        Assert.Equal(0, appels);
    }

    // Vrai refus d'ACL, ctor PUBLIC (vrai disque) : l'utilisateur courant, propriétaire du dossier temporaire, se refuse le
    // listage ; Directory.Exists reste vrai, l'énumération lève. La règle est retirée en finally avant la suppression
    // (le propriétaire garde le droit de modifier la DACL).
    [Fact]
    public async Task Une_racine_refusee_par_ACL_fait_echouer_la_passe()
    {
        var racine = DossierTemp("ChronosRacineAcl_", creer: true);
        var info = new DirectoryInfo(racine);
        var regle = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,
            FileSystemRights.ListDirectory, AccessControlType.Deny);

        var acl = info.GetAccessControl();
        acl.AddAccessRule(regle);
        info.SetAccessControl(acl);
        try
        {
            Assert.True(Directory.Exists(racine));
            var p = new TranscriptActivityProvider(Chemins(racine), new FakeClock(Now));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => p.ReadAsync());
        }
        finally
        {
            var retour = info.GetAccessControl();
            retour.RemoveAccessRule(regle);
            info.SetAccessControl(retour);
        }
    }

    // --- Composition réelle : tête + mémoïseur + magasin temporaire + doctrine ---

    private LastExactUsageProvider Composition(TranscriptActivityProvider provider, FakeClock horloge)
    {
        var dossier = DossierTemp("ChronosRacineDoctrine_", creer: true);
        var store = new LastExactStore(Path.Combine(dossier, "last-exact.json"));
        var capture = Now.AddHours(-2);                                   // relevé vieilli : au-delà de LimiteAge
        var inner = new FakeUsageProvider
        {
            Next = new UsageSnapshot
            {
                FiveHour = new WindowState
                {
                    Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
                    Utilization = 0.42, ResetsAt = Now.AddHours(3), CapturedAt = capture,
                },
                SevenDay = new WindowState
                {
                    Kind = WindowKind.SevenDay, Reliability = SourceReliability.Exact,
                    Utilization = 0.30, ResetsAt = Now.AddDays(4), CapturedAt = capture,
                },
            },
        };
        return new LastExactUsageProvider(inner, store, horloge, new SourceActiviteMemoisee(provider, horloge));
    }

    [Fact]
    public async Task Via_la_doctrine_une_racine_inaccessible_ne_certifie_jamais_un_releve_exact()
    {
        var horloge = new FakeClock(Now);
        var racine = DossierTemp("ChronosRacine_", creer: true);
        var provider = new TranscriptActivityProvider(Chemins(racine), horloge,
            _ => throw new UnauthorizedAccessException("accès refusé (test)"));
        var tete = Composition(provider, horloge);

        var snap = await tete.GetAsync();

        foreach (var w in new[] { snap.FiveHour, snap.SevenDay })
        {
            Assert.Equal(SourceReliability.Unavailable, w.Reliability);
            Assert.Null(w.Utilization);
            Assert.Null(w.Provenance);
        }
    }

    // Témoin de l'hypothèse DOCUMENTÉE (D-02) : hypothèse écrite au §4 de data-sources : une machine sans Claude Code n'a pas
    // d'activité ; des transcripts hors de ~/.claude/projects sont invisibles.
    [Fact]
    public async Task Via_la_doctrine_une_racine_absente_laisse_l_hypothese_encore_valide()
    {
        var horloge = new FakeClock(Now);
        var racine = DossierTemp("ChronosRacine_", creer: false);
        var provider = new TranscriptActivityProvider(Chemins(racine), horloge);
        var tete = Composition(provider, horloge);

        var snap = await tete.GetAsync();

        foreach (var w in new[] { snap.FiveHour, snap.SevenDay })
        {
            Assert.Equal(SourceReliability.Exact, w.Reliability);
            Assert.Equal(ProvenanceReleve.EncoreValide, w.Provenance);
        }
    }
}
