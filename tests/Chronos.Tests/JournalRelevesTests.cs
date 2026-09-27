using System.IO;
using System.Text;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// JRN-01 / JRN-03 — l'écrivain du journal des relevés exacts : append EXCLUSIF idempotent sur la clé
/// (t, source), fichiers mensuels par mois UTC de <c>t</c>, rétention 24 mois, observabilité
/// (<c>DerniereEcriture</c> / <c>DerniereErreur</c>), seuils dérivés de la cadence nominale.
///
/// Le test à deux écrivains est LA preuve de la phase : <c>FileMode.Append</c> n'est PAS atomique sous
/// Windows (mesuré le 2026-09-27 : deux appenders → une ligne perdue). Deux instances distinctes de
/// <see cref="JournalReleves"/> (aucun état mémoire partagé) écrivent la même séquence sur deux threads ;
/// le fichier doit contenir exactement la séquence, une fois.
///
/// Isolation stricte : chaque test travaille dans un dossier temp unique — jamais %APPDATA%\Chronos.
/// Horloge injectée (<see cref="FakeClock"/>), jamais l'horloge système.
/// </summary>
public class JournalRelevesTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 09, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset R5 = new(2026, 09, 27, 14, 50, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset R7 = new(2026, 10, 02, 22, 0, 0, TimeSpan.Zero);

    private readonly string _dir;
    private readonly FakeClock _clock = new(Now);

    public JournalRelevesTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ChronosJournal_" + Guid.NewGuid().ToString("N"));
        // Le dossier n'est PAS créé ici : le premier test prouve que l'écrivain le crée.
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private JournalReleves Journal() => new(_dir, _clock);

    private static ReleveJournal Releve(DateTimeOffset t, SourceUsage source = SourceUsage.SondeEnTetes, double u5 = 0.12)
        => new(t, source, u5, R5, StatutServeur.Autorise, 0.41, R7, StatutServeur.Autorise, null, null);

    private static string[] Lignes(string chemin)
        => File.ReadAllText(chemin, Encoding.UTF8).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    // --- Mois UTC, encodage ---

    [Fact]
    public void Un_releve_s_ajoute_dans_le_fichier_du_mois_UTC_de_son_instant()
    {
        var finSeptembre = new DateTimeOffset(2026, 09, 30, 23, 58, 0, TimeSpan.Zero);
        var debutOctobre = new DateTimeOffset(2026, 10, 01, 0, 03, 0, TimeSpan.Zero);
        Assert.Equal("releves-2026-09.jsonl", JournalReleves.NomFichier(finSeptembre));
        Assert.Equal("releves-2026-10.jsonl", JournalReleves.NomFichier(debutOctobre));
        // Le mois est celui de l'instant UTC, pas de l'offset porté : 01:30+02:00 le 1er octobre = 23:30Z le 30 septembre.
        Assert.Equal("releves-2026-09.jsonl", JournalReleves.NomFichier(new DateTimeOffset(2026, 10, 01, 1, 30, 0, TimeSpan.FromHours(2))));

        var journal = Journal();
        Assert.False(Directory.Exists(_dir));

        Assert.True(journal.AjouterReleve(Releve(finSeptembre)));
        Assert.True(journal.AjouterReleve(Releve(debutOctobre)));

        Assert.True(Directory.Exists(_dir));   // créé à la première écriture
        var septembre = Path.Combine(_dir, "releves-2026-09.jsonl");
        var octobre = Path.Combine(_dir, "releves-2026-10.jsonl");
        Assert.True(File.Exists(septembre));
        Assert.True(File.Exists(octobre));
        Assert.Equal(septembre, journal.CheminDuMois(finSeptembre));

        var octets = File.ReadAllBytes(septembre);
        Assert.False(octets.Length >= 3 && octets[0] == 0xEF && octets[1] == 0xBB && octets[2] == 0xBF, "UTF-8 SANS BOM attendu");
        Assert.Equal((byte)'\n', octets[^1]);
        Assert.DoesNotContain((byte)'\r', octets);
        Assert.Single(Lignes(septembre));
        Assert.Single(Lignes(octobre));
    }

    // --- Idempotence (t, source) ---

    [Fact]
    public void Le_meme_instant_pour_la_meme_source_n_est_ecrit_qu_une_fois()
    {
        var journal = Journal();
        var r = Releve(Now);

        Assert.True(journal.AjouterReleve(r));
        Assert.False(journal.AjouterReleve(r));

        Assert.Single(Lignes(journal.CheminDuMois(Now)));
        Assert.Equal(1, journal.RelevesEcrits);
        Assert.Null(journal.DerniereErreur);   // un doublon refusé n'est pas une erreur
    }

    [Fact]
    public void Un_instant_plus_ancien_que_le_dernier_de_la_source_est_refuse()
    {
        var journal = Journal();

        Assert.True(journal.AjouterReleve(Releve(Now.AddMinutes(5))));
        Assert.False(journal.AjouterReleve(Releve(Now)));   // rejeu : plus ancien que le dernier écrit

        Assert.Single(Lignes(journal.CheminDuMois(Now)));
    }

    [Fact]
    public void Deux_sources_au_meme_instant_donnent_deux_lignes()
    {
        var journal = Journal();

        Assert.True(journal.AjouterReleve(Releve(Now, SourceUsage.SondeEnTetes)));
        Assert.True(journal.AjouterReleve(Releve(Now, SourceUsage.EndpointOAuthChronos)));

        var lecture = LecteurJournal.LireFichier(journal.CheminDuMois(Now));
        Assert.Equal(2, lecture.Releves.Count);
        Assert.Equal(new[] { SourceUsage.SondeEnTetes, SourceUsage.EndpointOAuthChronos }, lecture.Releves.Select(r => r.Source));
    }

    // --- Deux écrivains ---

    [Fact]
    public void Deux_ecrivains_concurrents_200_ecritures_zero_doublon_zero_troncature()
    {
        const int n = 100;
        var sequence = Enumerable.Range(0, n).Select(i => Releve(Now.AddMinutes(5 * i), u5: i / 100.0)).ToArray();

        // Deux instances DISTINCTES : aucun verrou mémoire partagé, seul le fichier arbitre — comme deux processus.
        var a = Journal();
        var b = Journal();
        var pret = new ManualResetEventSlim(false);

        void Ecrire(JournalReleves j) { pret.Wait(); foreach (var r in sequence) j.AjouterReleve(r); }
        var ta = Task.Run(() => Ecrire(a));
        var tb = Task.Run(() => Ecrire(b));
        pret.Set();
        Task.WaitAll(ta, tb);

        var chemin = a.CheminDuMois(Now);
        var lignes = Lignes(chemin);
        var lecture = LecteurJournal.LireFichier(chemin);

        Assert.Equal(n, lignes.Length);
        Assert.Equal(n, lecture.Releves.Count);
        Assert.Equal(0, lecture.LignesIgnorees);   // aucune ligne tronquée ni entrelacée
        Assert.Equal(sequence.Select(r => r.T), lecture.Releves.Select(r => r.T));   // distincts ET croissants
        Assert.Equal(n, a.RelevesEcrits + b.RelevesEcrits);
        Assert.Null(a.DerniereErreur);
        Assert.Null(b.DerniereErreur);
    }

    // --- Événements ---

    [Fact]
    public void Un_evenement_s_ecrit_toujours_meme_au_meme_instant()
    {
        var journal = Journal();
        var arret = new EvenementJournal(Now, TypeEvenement.Arret);

        Assert.True(journal.AjouterEvenement(arret));
        Assert.True(journal.AjouterEvenement(arret));   // pas d'idempotence sur les événements

        var lecture = LecteurJournal.LireFichier(journal.CheminDuMois(Now));
        Assert.Equal(2, lecture.Evenements.Count);
        Assert.Equal(2, journal.EvenementsEcrits);
        Assert.Equal(0, journal.RelevesEcrits);
    }

    // --- Observabilité ---

    [Fact]
    public void Une_ecriture_reussie_pose_DerniereEcriture_et_un_echec_pose_DerniereErreur_sans_lever()
    {
        var journal = Journal();
        Assert.Null(journal.DerniereEcriture);

        _clock.UtcNow = Now.AddMinutes(7);
        Assert.True(journal.AjouterReleve(Releve(Now)));
        Assert.Equal(Now.AddMinutes(7), journal.DerniereEcriture);
        Assert.Null(journal.DerniereErreur);

        // Dossier « poison » : un FICHIER là où le journal attend un dossier. Rien ne peut s'y créer.
        var poison = Path.Combine(_dir, "poison");
        File.WriteAllText(poison, "je ne suis pas un dossier");
        var enPanne = new JournalReleves(poison, _clock);

        var ecrit = enPanne.AjouterReleve(Releve(Now));

        Assert.False(ecrit);
        Assert.NotNull(enPanne.DerniereErreur);
        Assert.EndsWith("Exception", enPanne.DerniereErreur!.Split(" : ")[0]);   // commence par le nom de l'exception
        Assert.Null(enPanne.DerniereEcriture);
        Assert.Equal(0, enPanne.RelevesEcrits);
    }

    [Fact]
    public void DernierT_relit_la_queue_du_fichier_du_mois()
    {
        var premier = Journal();
        Assert.True(premier.AjouterReleve(Releve(Now)));
        Assert.True(premier.AjouterReleve(Releve(Now.AddMinutes(5))));
        Assert.True(premier.AjouterReleve(Releve(Now.AddMinutes(10))));
        Assert.True(premier.AjouterReleve(Releve(Now.AddMinutes(7), SourceUsage.EndpointOAuthChronos)));

        // NOUVELLE instance : aucun souvenir en mémoire, seule la queue du fichier fait foi.
        var second = Journal();
        Assert.Equal(Now.AddMinutes(10), second.DernierT(SourceUsage.SondeEnTetes));
        Assert.Equal(Now.AddMinutes(7), second.DernierT(SourceUsage.EndpointOAuthChronos));
        Assert.Null(second.DernierT(SourceUsage.PontStatusLine));

        // Et l'idempotence tient à travers les instances : le second refuse ce que le premier a écrit.
        Assert.False(second.AjouterReleve(Releve(Now.AddMinutes(10))));
        Assert.True(second.AjouterReleve(Releve(Now.AddMinutes(15))));
    }

    // --- Rétention ---

    [Fact]
    public void Purger_supprime_les_mois_de_plus_de_24_mois_et_ignore_le_reste()
    {
        Directory.CreateDirectory(_dir);
        foreach (var nom in new[] { "releves-2024-08.jsonl", "releves-2024-09.jsonl", "releves-2026-09.jsonl", "notes.txt", "releves-abcd.jsonl" })
            File.WriteAllText(Path.Combine(_dir, nom), "");

        var bilan = Journal().Purger();   // horloge : 2026-09-27 → limite = 2024-09 (courant − 24) ; 2024-08 < limite

        Assert.Equal(new BilanRetention(FichiersSupprimes: 1, Echecs: 0, Ignores: 2), bilan);
        Assert.False(File.Exists(Path.Combine(_dir, "releves-2024-08.jsonl")));
        Assert.True(File.Exists(Path.Combine(_dir, "releves-2024-09.jsonl")));   // = courant − 24 : gardé
        Assert.True(File.Exists(Path.Combine(_dir, "releves-2026-09.jsonl")));
        Assert.True(File.Exists(Path.Combine(_dir, "notes.txt")));
        Assert.True(File.Exists(Path.Combine(_dir, "releves-abcd.jsonl")));

        // Dossier absent : bilan à zéro, sans lever.
        Assert.Equal(new BilanRetention(0, 0, 0), new JournalReleves(Path.Combine(_dir, "inexistant"), _clock).Purger());
    }

    // --- Seuils dérivés ---

    [Fact]
    public void Les_seuils_derivent_de_la_cadence_nominale()
    {
        Assert.Equal(2 * RateLimitHeaderUsageProvider.CadenceNominale, JournalReleves.SeuilReprise);
        Assert.Equal(3 * RateLimitHeaderUsageProvider.CadenceNominale, JournalReleves.SeuilMuet);
        Assert.Equal(TimeSpan.FromMinutes(10), JournalReleves.SeuilReprise);
        Assert.Equal(TimeSpan.FromMinutes(15), JournalReleves.SeuilMuet);
        Assert.Equal(24, JournalReleves.RetentionMois);
    }
}
