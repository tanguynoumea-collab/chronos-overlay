using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chronos.Models.Historique.Tokens;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// TOK-01 — le magasin des agrégats de tokens : un fichier par mois UTC du slot, un état en mémoire par mois,
/// RÉÉCRITURE atomique du mois entier (temp + Move, jamais d'ajout), ordre déterministe (slot, modèle ordinal, sub)
/// — deux écritures du même état donnent le même fichier au bit près (l'assertion que l'idempotence de 33-03
/// réutilise) —, tranches RÉÉCRITES et non ajoutées (<c>RemplacerMois</c>), mois gelé chargé AVANT tout delta,
/// <see cref="IEtatMagasin"/> (dernière écriture = mtime, D-32-05), rétention alignée sur le journal des relevés.
///
/// Isolation stricte : chaque test travaille dans un dossier temp unique — jamais %APPDATA%\Chronos.
/// Horloge injectée (<see cref="FakeClock"/>), jamais l'horloge système.
/// </summary>
public class MagasinAgregatsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 09, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Septembre = new(2026, 09, 01, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset S1 = new(2026, 09, 21, 10, 45, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset S2 = new(2026, 09, 21, 11, 0, 0, TimeSpan.Zero);

    private readonly string _dir;
    private readonly FakeClock _clock = new(Now);

    public MagasinAgregatsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ChronosAgregats_" + Guid.NewGuid().ToString("N"));
        // Le dossier n'est PAS créé ici : le magasin le crée à la première écriture.
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private MagasinAgregats Magasin() => new(_dir, _clock);

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DeltaTranche Delta(DateTimeOffset slot, string model, bool sub, long entree, long sortie, long cacheW, long cacheR, bool nouveau)
        => new(slot, model, sub, entree, sortie, cacheW, cacheR, nouveau);

    private static string CheminFixture([CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "tokens", "tolerance", "tokens-2026-09.jsonl");

    private static string[] Lignes(string chemin)
        => File.ReadAllText(chemin, Encoding.UTF8).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string Sha256(string chemin) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(chemin)));

    // --- Nom de fichier, mois UTC ---

    [Fact]
    public void Le_nom_de_fichier_est_le_mois_UTC_du_slot()
    {
        Assert.Equal("tokens-2026-09.jsonl", MagasinAgregats.NomFichier(Utc("2026-09-30T23:45:00Z")));
        Assert.Equal("tokens-2026-10.jsonl", MagasinAgregats.NomFichier(Utc("2026-10-01T00:00:00Z")));
        Assert.Equal("tokens-2026-09.jsonl", MagasinAgregats.NomFichier(new DateTimeOffset(2026, 10, 01, 1, 30, 0, TimeSpan.FromHours(2))));

        Assert.True(MagasinAgregats.EstNomMensuel("tokens-2026-09.jsonl", out var mois));
        Assert.Equal(Septembre, mois);
        Assert.False(MagasinAgregats.EstNomMensuel("ids-2026-09.jsonl", out _));
        Assert.False(MagasinAgregats.EstNomMensuel("tokens-abcd.jsonl", out _));
        Assert.False(MagasinAgregats.EstNomMensuel("tokens-2026-13.jsonl", out _));

        Assert.Equal(Path.Combine(_dir, "tokens-2026-09.jsonl"), Magasin().CheminDuMois(S1));
    }

    // --- État mémoire ---

    [Fact]
    public void Appliquer_cree_la_tranche_puis_l_augmente_et_N_ne_bouge_qu_au_premier_passage()
    {
        var magasin = Magasin();

        magasin.Appliquer(Delta(S1, "claude-opus-5", false, 2, 8, 35005, 41741, nouveau: true));
        magasin.Appliquer(Delta(S1, "claude-opus-5", false, 0, 248, 0, 0, nouveau: false));   // bloc final partiel : 8 → 256

        var tranches = magasin.TranchesDuMois(Septembre);
        var t = Assert.Single(tranches);
        Assert.Equal(new TrancheTokens(S1, "claude-opus-5", false, 2, 256, 35005, 41741, 1), t);
        Assert.Equal(1, magasin.TranchesEnMemoire);
        Assert.Contains(Septembre, magasin.MoisSales);

        // Un delta nul n'ajoute rien et ne salit pas : sur un magasin neuf, MoisSales reste vide.
        var neuf = Magasin();
        var nul = Delta(S1, "claude-opus-5", false, 0, 0, 0, 0, nouveau: false);
        Assert.True(nul.EstNul);
        neuf.Appliquer(nul);
        Assert.Empty(neuf.MoisSales);
        Assert.Equal(0, neuf.TranchesEnMemoire);
    }

    [Fact]
    public void Deux_origines_ou_deux_modeles_dans_la_meme_tranche_donnent_des_lignes_distinctes()
    {
        var magasin = Magasin();

        magasin.Appliquer(Delta(S1, "claude-opus-5", false, 1, 1, 1, 1, true));
        magasin.Appliquer(Delta(S1, "claude-opus-5", true, 1, 1, 1, 1, true));
        magasin.Appliquer(Delta(S1, "claude-sonnet-5", false, 1, 1, 1, 1, true));

        var tranches = magasin.TranchesDuMois(Septembre);
        Assert.Equal(3, tranches.Count);
        Assert.All(tranches, t => Assert.Equal(1, t.N));
        Assert.Equal(3, tranches.Select(t => (t.Model, t.Sub)).Distinct().Count());
    }

    // --- Écriture atomique, triée ---

    [Fact]
    public void EcrireMoisSales_reecrit_le_mois_entier_trie_et_atomiquement()
    {
        var magasin = Magasin();

        // 5 deltas sur 2 slots × 2 modèles × 2 origines, appliqués dans le DÉSORDRE.
        magasin.Appliquer(Delta(S2, "claude-sonnet-5", true, 1, 1, 1, 1, true));
        magasin.Appliquer(Delta(S1, "claude-sonnet-5", false, 1, 1, 1, 1, true));
        magasin.Appliquer(Delta(S2, "claude-opus-5", false, 1, 1, 1, 1, true));
        magasin.Appliquer(Delta(S1, "claude-opus-5", true, 1, 1, 1, 1, true));
        magasin.Appliquer(Delta(S1, "claude-opus-5", false, 1, 1, 1, 1, true));

        Assert.True(magasin.EcrireMoisSales());

        var chemin = magasin.CheminDuMois(S1);
        var octets = File.ReadAllBytes(chemin);
        Assert.False(octets.Length >= 3 && octets[0] == 0xEF && octets[1] == 0xBB && octets[2] == 0xBF, "BOM UTF-8 interdit");
        Assert.DoesNotContain((byte)'\r', octets);
        Assert.Equal((byte)'\n', octets[^1]);

        var lignes = Lignes(chemin);
        Assert.Equal(5, lignes.Length);
        var tranches = lignes.Select(l => { Assert.True(LigneAgregat.Parser(l, out var t), l); return t!; }).ToList();

        var attendu = new[]
        {
            (S1, "claude-opus-5", false), (S1, "claude-opus-5", true), (S1, "claude-sonnet-5", false),
            (S2, "claude-opus-5", false), (S2, "claude-sonnet-5", true),
        };
        Assert.Equal(attendu, tranches.Select(t => (t.Slot, t.Model, t.Sub)).ToArray());

        Assert.Empty(Directory.GetFiles(_dir, "*.tmp-*"));
        Assert.Empty(magasin.MoisSales);
        Assert.Equal(1, magasin.MoisEcrits);
        Assert.Null(magasin.DerniereErreur);
    }

    /// <summary>
    /// La PREUVE de l'atomicité (mutation m2 du plan : un <c>WriteAllText</c> direct à la place du <c>Move</c> reste vert
    /// sur les autres tests). Fait mesuré le 2026-09-27 (.NET 8, Windows 11) : face à un lecteur qui tient le fichier en
    /// <c>FileShare.ReadWrite | Delete</c> — le partage même du lecteur tolérant —, <c>File.Move(overwrite)</c> ÉCHOUE
    /// (<c>UnauthorizedAccessException</c>) et l'ancien fichier reste intact, alors qu'une écriture directe RÉUSSIT et fait
    /// lire le nouveau contenu au lecteur déjà ouvert (<c>FileShare.None</c> ne discrimine pas : les deux échouent).
    /// Ce que le magasin garantit donc : échec PROPRE (false, erreur consignée, aucun temp, octets intacts, mois toujours
    /// sale), jamais un fichier partiel sous un lecteur ; le lot suivant rattrape.
    /// </summary>
    [Fact]
    public void Un_lecteur_concurrent_fait_echouer_l_ecriture_proprement_sans_fichier_partiel()
    {
        var magasin = Magasin();
        magasin.Appliquer(Delta(S1, "claude-opus-5", false, 1, 1, 1, 1, true));
        magasin.Appliquer(Delta(S1, "claude-opus-5", true, 1, 1, 1, 1, true));
        magasin.Appliquer(Delta(S2, "claude-opus-5", false, 1, 1, 1, 1, true));
        Assert.True(magasin.EcrireMoisSales());
        var chemin = magasin.CheminDuMois(S1);
        var avant = Sha256(chemin);

        magasin.Appliquer(Delta(S2, "claude-sonnet-5", false, 1, 1, 1, 1, true));

        bool ok;
        string luSousLecteur;
        using (var lecteur = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            ok = magasin.EcrireMoisSales();
            using var sr = new StreamReader(lecteur, Encoding.UTF8);
            luSousLecteur = sr.ReadToEnd();
        }

        Assert.False(ok, "Une réécriture sous un lecteur ouvert doit échouer proprement, jamais réussir en place.");
        Assert.NotNull(magasin.DerniereErreur);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp-*"));
        Assert.Equal(avant, Sha256(chemin));   // octets intacts
        Assert.Contains(Septembre, magasin.MoisSales);   // toujours sale : le lot suivant rattrape

        // Le lecteur déjà ouvert n'a vu que l'ANCIEN état, complet : 3 lignes, toutes lisibles.
        var lignes = luSousLecteur.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lignes.Length);
        Assert.All(lignes, l => Assert.True(LigneAgregat.Parser(l, out _), l));

        // Lecteur parti : la réécriture passe, l'erreur s'efface, les 4 tranches sont là.
        Assert.True(magasin.EcrireMoisSales());
        Assert.Null(magasin.DerniereErreur);
        Assert.Equal(4, Lignes(chemin).Length);
        Assert.Empty(magasin.MoisSales);
    }

    [Fact]
    public void Deux_ecritures_du_meme_etat_donnent_des_octets_identiques()
    {
        var magasin = Magasin();
        magasin.Appliquer(Delta(S2, "claude-sonnet-5", true, 5, 50, 0, 500, true));
        magasin.Appliquer(Delta(S1, "claude-opus-5", false, 72, 398, 48601, 20376807, true));
        magasin.Appliquer(Delta(S1, "claude-opus-5", true, 1, 1, 1, 1, true));
        Assert.True(magasin.EcrireMoisSales());
        var chemin = magasin.CheminDuMois(S1);
        var sha1 = Sha256(chemin);

        magasin.Appliquer(Delta(S1, "claude-opus-5", false, 0, 0, 0, 0, false));   // delta nul : rien ne bouge
        Assert.Empty(magasin.MoisSales);

        magasin.RemplacerMois(Septembre, magasin.TranchesDuMois(Septembre));   // reprojection du même état
        Assert.Contains(Septembre, magasin.MoisSales);
        Assert.True(magasin.EcrireMoisSales());

        Assert.Equal(sha1, Sha256(chemin));
        Assert.Equal(2, magasin.MoisEcrits);
    }

    [Fact]
    public void RemplacerMois_ecrase_l_etat_memoire_du_mois_et_le_fichier()
    {
        var magasin = Magasin();
        magasin.Appliquer(Delta(S1, "claude-opus-5", false, 1, 1, 1, 1, true));
        magasin.Appliquer(Delta(S1, "claude-opus-5", true, 1, 1, 1, 1, true));
        magasin.Appliquer(Delta(S2, "claude-opus-5", false, 1, 1, 1, 1, true));
        Assert.True(magasin.EcrireMoisSales());
        Assert.Equal(3, Lignes(magasin.CheminDuMois(S1)).Length);

        magasin.RemplacerMois(Septembre, new[] { new TrancheTokens(S2, "claude-opus-5", false, 9, 9, 9, 9, 3) });
        Assert.True(magasin.EcrireMoisSales());

        var lignes = Lignes(magasin.CheminDuMois(S1));
        var seule = Assert.Single(lignes);
        Assert.True(LigneAgregat.Parser(seule, out var t));
        Assert.Equal(new TrancheTokens(S2, "claude-opus-5", false, 9, 9, 9, 9, 3), t);
        Assert.Equal(1, magasin.TranchesEnMemoire);
    }

    // --- Chargement d'un mois gelé ---

    [Fact]
    public void ChargerMois_relit_un_mois_gele_avec_tolerance()
    {
        Directory.CreateDirectory(_dir);
        File.Copy(CheminFixture(), Path.Combine(_dir, "tokens-2026-09.jsonl"));
        var magasin = Magasin();

        Assert.Equal(2, magasin.ChargerMois(Septembre));

        Assert.Equal(5, magasin.LignesIgnorees);
        Assert.Equal(2, magasin.TranchesDuMois(Septembre).Count);
        Assert.Empty(magasin.MoisSales);   // charger ne réécrit pas
        Assert.Equal(0, magasin.MoisEcrits);

        var aout = new DateTimeOffset(2026, 08, 01, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(0, magasin.ChargerMois(aout));
        Assert.False(File.Exists(magasin.CheminDuMois(aout)));
        Assert.Empty(magasin.TranchesDuMois(aout));
    }

    [Fact]
    public void Appliquer_sur_un_mois_pas_encore_en_memoire_charge_d_abord_son_fichier()
    {
        Directory.CreateDirectory(_dir);
        var juin = new DateTimeOffset(2026, 06, 01, 0, 0, 0, TimeSpan.Zero);
        var anciennes = new[]
        {
            new TrancheTokens(Utc("2026-06-23T12:45:00Z"), "claude-opus-5", false, 10, 20, 30, 40, 2),
            new TrancheTokens(Utc("2026-06-23T13:00:00Z"), "claude-opus-5", false, 11, 21, 31, 41, 3),
        };
        File.WriteAllText(Path.Combine(_dir, "tokens-2026-06.jsonl"),
            string.Join("", anciennes.Select(t => LigneAgregat.Serialiser(t) + "\n")), new UTF8Encoding(false));

        var magasin = Magasin();
        magasin.Appliquer(Delta(Utc("2026-06-23T14:00:00Z"), "claude-opus-5", false, 1, 1, 1, 1, true));

        Assert.Equal(3, magasin.TranchesDuMois(juin).Count);
        Assert.True(magasin.EcrireMoisSales());

        var lignes = Lignes(magasin.CheminDuMois(juin));
        Assert.Equal(3, lignes.Length);   // les deux anciennes CONSERVÉES : un mois gelé n'est jamais écrasé par une tranche isolée
        Assert.True(LigneAgregat.Parser(lignes[0], out var t0));
        Assert.Equal(anciennes[0], t0);
        Assert.True(LigneAgregat.Parser(lignes[1], out var t1));
        Assert.Equal(anciennes[1], t1);
    }

    // --- Ce que le fichier ne porte JAMAIS ---

    [Fact]
    public void Le_fichier_ne_porte_ni_somme_ni_message_ni_texte()
    {
        var magasin = Magasin();
        magasin.Appliquer(Delta(S1, "claude-opus-5", true, 72, 398, 48601, 20376807, true));
        Assert.True(magasin.EcrireMoisSales());

        var contenu = File.ReadAllText(magasin.CheminDuMois(S1));
        Assert.DoesNotContain("20425878", contenu);   // 72 + 398 + 48601 + 20376807
        Assert.DoesNotContain("msg_", contenu);
        Assert.DoesNotContain("\"content\"", contenu);
        Assert.DoesNotContain("\"text\"", contenu);

        foreach (var ligne in Lignes(magasin.CheminDuMois(S1)))
        {
            using var doc = JsonDocument.Parse(ligne);
            Assert.Equal(LigneAgregat.Champs, doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        }
    }

    // --- IEtatMagasin ---

    [Fact]
    public void IEtatMagasin_donne_le_nom_le_dossier_et_le_mtime_apres_ecriture()
    {
        var magasin = Magasin();
        IEtatMagasin etat = magasin;

        Assert.Equal(NomsMagasins.AgregatsTokens, etat.Nom);
        Assert.Equal(_dir, etat.Chemin);
        Assert.Null(etat.DerniereEcriture);
        Assert.Null(etat.DerniereErreur);

        magasin.Appliquer(Delta(S1, "claude-opus-5", false, 1, 1, 1, 1, true));
        Assert.True(magasin.EcrireMoisSales());

        var mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(magasin.CheminDuMois(S1)), TimeSpan.Zero);
        Assert.Equal(mtime, etat.DerniereEcriture);
        Assert.NotEqual(Now, etat.DerniereEcriture);   // le mtime du disque, pas l'horloge injectée (D-32-05)
        Assert.Null(etat.DerniereErreur);
    }

    [Fact]
    public void Un_dossier_poison_pose_DerniereErreur_sans_lever()
    {
        Directory.CreateDirectory(_dir);
        var fichier = Path.Combine(_dir, "pas-un-dossier");
        File.WriteAllText(fichier, "x");

        var poison = new MagasinAgregats(fichier, _clock);
        poison.Appliquer(Delta(S1, "claude-opus-5", false, 1, 1, 1, 1, true));

        var ok = poison.EcrireMoisSales();

        Assert.False(ok);
        Assert.NotNull(poison.DerniereErreur);
        Assert.Matches(@"^\w*Exception : ", poison.DerniereErreur);
        Assert.Null(poison.DerniereEcriture);
        Assert.Contains(Septembre, poison.MoisSales);   // toujours sale : rien n'a été écrit
        Assert.Equal("x", File.ReadAllText(fichier));

        // Un succès ultérieur dans un dossier valide (nouvelle instance) part sans erreur.
        var sain = Magasin();
        sain.Appliquer(Delta(S1, "claude-opus-5", false, 1, 1, 1, 1, true));
        Assert.True(sain.EcrireMoisSales());
        Assert.Null(sain.DerniereErreur);
    }

    // --- Rétention ---

    [Fact]
    public void Purger_supprime_les_mois_au_dela_de_la_retention_du_journal()
    {
        Assert.Equal(JournalReleves.RetentionMois, MagasinAgregats.RetentionMois);

        Directory.CreateDirectory(_dir);
        foreach (var nom in new[] { "tokens-2024-08.jsonl", "tokens-2024-09.jsonl", "tokens-2026-09.jsonl", "ids-2026-09.jsonl", "notes.txt" })
            File.WriteAllText(Path.Combine(_dir, nom), "");

        var bilan = Magasin().Purger();   // horloge : 2026-09-27 → limite = 2024-09 (courant − 24 mois), gardé

        Assert.Equal(new BilanRetention(1, 0, 2), bilan);
        Assert.False(File.Exists(Path.Combine(_dir, "tokens-2024-08.jsonl")));
        Assert.True(File.Exists(Path.Combine(_dir, "tokens-2024-09.jsonl")));
        Assert.True(File.Exists(Path.Combine(_dir, "tokens-2026-09.jsonl")));
        Assert.True(File.Exists(Path.Combine(_dir, "ids-2026-09.jsonl")));
        Assert.True(File.Exists(Path.Combine(_dir, "notes.txt")));

        // Dossier absent : bilan à zéro, sans lever.
        Assert.Equal(new BilanRetention(0, 0, 0), new MagasinAgregats(Path.Combine(_dir, "absent"), _clock).Purger());
    }
}
