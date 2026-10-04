using System.IO;
using System.Text;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// 42.2-03 — MAT-3 / DATA-7 / TEST-3 / MAT-4 sur les deux magasins du widget de sessions (archived.json, treated.json) :
/// aucune réécriture ne perd l'original ; illisible ⇒ quarantaine puis écriture ; E/S passagère ⇒ rien n'est écrit ni
/// renommé ; les échecs ne sont plus avalés. Chaque test travaille dans SON dossier sous <see cref="Path.GetTempPath"/>.
/// </summary>
public class MagasinsSessionsLectureTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<string> Magasins => new() { "archive", "traites" };

    /// <summary>Façade de test commune aux deux magasins.</summary>
    private sealed class Sujet
    {
        private readonly ArchiveStore? _archive;
        private readonly TreatedStore? _traites;

        public Sujet(string type, string chemin)
        {
            Chemin = chemin;
            if (type == "archive") _archive = new ArchiveStore(chemin, new FakeClock(T));
            else _traites = new TreatedStore(chemin, new FakeClock(T));
        }

        public string Chemin { get; }
        public string Dossier => Path.GetDirectoryName(Chemin)!;
        public IEtatMagasin Etat => (IEtatMagasin?)_archive ?? _traites!;

        public bool Ecrire(string id) =>
            _archive?.Add(id) ?? _traites!.Set(id, T.ToUnixTimeMilliseconds());

        public IReadOnlyCollection<string> Lire() =>
            _archive is not null ? _archive.Load().ToList() : _traites!.Load().Keys.ToList();

        public string[] Quarantaines() =>
            Directory.GetFiles(Dossier, Path.GetFileNameWithoutExtension(Chemin) + ".illisible-*");

        public string Journal() =>
            File.Exists(Path.Combine(Dossier, JournalIncidents.NomFichier))
                ? File.ReadAllText(Path.Combine(Dossier, JournalIncidents.NomFichier)) : "";
    }

    private static Sujet Nouveau(string type)
    {
        var dossier = Path.Combine(Path.GetTempPath(), "chronos-magsess-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        return new Sujet(type, Path.Combine(dossier, type == "archive" ? "archived.json" : "treated.json"));
    }

    private static readonly byte[] Tronque = Encoding.UTF8.GetBytes("{\"a\": 17000000");

    [Theory]
    [MemberData(nameof(Magasins))]
    public void Deux_ecritures_conservent_la_premiere(string type)
    {
        var s = Nouveau(type);
        Assert.True(s.Ecrire("A"));
        Assert.True(s.Ecrire("B"));
        Assert.Equal(new[] { "A", "B" }, s.Lire().OrderBy(x => x));
    }

    [Theory]
    [MemberData(nameof(Magasins))]
    public void Fichier_tronque_la_lecture_seule_ne_renomme_rien_et_pose_l_erreur_de_lecture(string type)
    {
        var s = Nouveau(type);
        File.WriteAllBytes(s.Chemin, Tronque);

        Assert.Empty(s.Lire());
        Assert.Empty(s.Quarantaines());
        Assert.Equal(Tronque, File.ReadAllBytes(s.Chemin));
        Assert.Contains("illisible", s.Etat.DerniereErreurLecture);
    }

    [Theory]
    [MemberData(nameof(Magasins))]
    public void Fichier_tronque_l_ecriture_met_l_original_en_quarantaine_puis_ecrit(string type)
    {
        var s = Nouveau(type);
        File.WriteAllBytes(s.Chemin, Tronque);
        s.Lire();

        Assert.True(s.Ecrire("X"));

        var q = Assert.Single(s.Quarantaines());
        Assert.Equal(Tronque, File.ReadAllBytes(q));
        Assert.Equal(q, s.Etat.DerniereQuarantaine);
        Assert.Equal(new[] { "X" }, s.Lire());
        Assert.Contains(JournalIncidents.Marqueur, s.Journal());
        Assert.Contains("mis en quarantaine", s.Journal());
    }

    [Theory]
    [MemberData(nameof(Magasins))]
    public void Fichier_vide_est_mis_en_quarantaine_puis_ecrit(string type)
    {
        var s = Nouveau(type);
        File.WriteAllBytes(s.Chemin, Array.Empty<byte>());

        Assert.True(s.Ecrire("X"));

        var q = Assert.Single(s.Quarantaines());
        Assert.Equal(0, new FileInfo(q).Length);
        Assert.Equal(new[] { "X" }, s.Lire());
    }

    [Theory]
    [MemberData(nameof(Magasins))]
    public void Racine_non_objet_est_mise_en_quarantaine_puis_ecrite(string type)
    {
        var s = Nouveau(type);
        File.WriteAllText(s.Chemin, "[1,2,3]");

        Assert.True(s.Ecrire("X"));

        var q = Assert.Single(s.Quarantaines());
        Assert.Equal("[1,2,3]", File.ReadAllText(q));
        Assert.Equal(new[] { "X" }, s.Lire());
    }

    [Theory]
    [MemberData(nameof(Magasins))]
    public void Quarantaine_impossible_rien_n_est_ecrit_et_le_blocage_est_signale(string type)
    {
        var s = Nouveau(type);
        File.WriteAllBytes(s.Chemin, Tronque);

        using (new FileStream(s.Chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))   // sans FileShare.Delete
        {
            Assert.False(s.Ecrire("X"));
        }

        Assert.Equal(Tronque, File.ReadAllBytes(s.Chemin));
        Assert.Empty(s.Quarantaines());
        Assert.Contains("quarantaine impossible", s.Etat.DerniereErreur);
    }

    [Theory]
    [MemberData(nameof(Magasins))]
    public void Fichier_verrouille_ni_ecriture_ni_quarantaine_puis_le_geste_suivant_reussit(string type)
    {
        var s = Nouveau(type);
        Assert.True(s.Ecrire("A"));
        var avant = File.ReadAllBytes(s.Chemin);

        using (new FileStream(s.Chemin, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var ex = Record.Exception(() => s.Lire());
            Assert.Null(ex);
            Assert.False(s.Ecrire("B"));
        }

        Assert.Equal(avant, File.ReadAllBytes(s.Chemin));
        Assert.Empty(s.Quarantaines());
        Assert.NotNull(s.Etat.DerniereErreur);

        Assert.True(s.Ecrire("B"));
        Assert.Equal(new[] { "A", "B" }, s.Lire().OrderBy(x => x));
        Assert.Null(s.Etat.DerniereErreur);
    }

    [Theory]
    [MemberData(nameof(Magasins))]
    public void Lecture_non_aboutie_rend_le_dernier_ensemble_lu(string type)
    {
        var s = Nouveau(type);
        s.Ecrire("A");
        s.Ecrire("B");
        Assert.Equal(2, s.Lire().Count);

        using (new FileStream(s.Chemin, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Equal(new[] { "A", "B" }, s.Lire().OrderBy(x => x));   // pas un ensemble vide : rien ne réapparaît
            Assert.NotNull(s.Etat.DerniereErreurLecture);
        }
    }

    [Fact]
    public void Archive_une_lecture_ratee_au_premier_appel_rend_un_ensemble_vide_sans_lever()
    {
        var s = Nouveau("archive");
        File.WriteAllBytes(s.Chemin, Tronque);
        Assert.Empty(s.Lire());
    }

    [Theory]
    [MemberData(nameof(Magasins))]
    public void L_erreur_de_lecture_ne_s_efface_que_par_une_lecture_reussie(string type)
    {
        var s = Nouveau(type);
        File.WriteAllBytes(s.Chemin, Tronque);
        s.Lire();
        Assert.NotNull(s.Etat.DerniereErreurLecture);

        Assert.True(s.Ecrire("X"));                      // quarantaine + écriture réussie…
        Assert.NotNull(s.Etat.DerniereErreurLecture);    // …n'efface PAS l'erreur de lecture (MAT-4)

        s.Lire();                                        // fichier réparé, lecture réussie
        Assert.Null(s.Etat.DerniereErreurLecture);
    }

    [Theory]
    [MemberData(nameof(Magasins))]
    public void Ecriture_impossible_rend_false_expose_l_erreur_et_ne_laisse_aucun_temporaire(string type)
    {
        var s = Nouveau(type);
        Assert.True(s.Ecrire("A"));

        using (new FileStream(s.Chemin, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(s.Ecrire("B"));
        }

        Assert.NotNull(s.Etat.DerniereErreur);
        Assert.Empty(Directory.GetFiles(s.Dossier, "*.tmp-*"));
        Assert.Equal(new[] { "A" }, s.Lire());
    }

    [Theory]
    [MemberData(nameof(Magasins))]
    public void Fichier_absent_l_ecriture_cree_le_fichier(string type)
    {
        var s = Nouveau(type);
        Assert.Null(s.Etat.DerniereEcriture);
        Assert.True(s.Ecrire("A"));
        Assert.NotNull(s.Etat.DerniereEcriture);
        Assert.True(File.Exists(s.Chemin));
        Assert.Null(s.Etat.DerniereErreur);
    }

    [Theory]
    [MemberData(nameof(Magasins))]
    public void Une_meme_erreur_de_lecture_n_est_journalisee_qu_une_fois(string type)
    {
        var s = Nouveau(type);
        File.WriteAllBytes(s.Chemin, Tronque);
        s.Lire();
        s.Lire();
        s.Lire();
        var lignes = s.Journal().Split('\n').Count(l => l.Contains("lecture non aboutie", StringComparison.Ordinal));
        Assert.Equal(1, lignes);
    }

    [Fact]
    public void Noms_et_chemins_injectes()
    {
        var a = Nouveau("archive");
        var t = Nouveau("traites");
        Assert.Equal(NomsMagasins.SessionsArchivees, a.Etat.Nom);
        Assert.Equal("sessions archivées", a.Etat.Nom);
        Assert.Equal(a.Chemin, a.Etat.Chemin);
        Assert.Equal(NomsMagasins.SessionsTraitees, t.Etat.Nom);
        Assert.Equal("sessions traitées", t.Etat.Nom);
        Assert.Equal(t.Chemin, t.Etat.Chemin);
    }

    [Fact]
    public void Traites_Remove_d_une_cle_absente_rend_true_sans_ecrire()
    {
        var s = Nouveau("traites");
        var store = new TreatedStore(s.Chemin, new FakeClock(T));
        Assert.True(store.Remove("inconnue"));
        Assert.False(File.Exists(s.Chemin));
    }

    [Fact]
    public void Traites_Remove_sur_fichier_illisible_met_en_quarantaine_puis_ecrit_une_map_vide()
    {
        var s = Nouveau("traites");
        File.WriteAllBytes(s.Chemin, Tronque);
        var store = new TreatedStore(s.Chemin, new FakeClock(T));

        Assert.True(store.Remove("A"));

        var q = Assert.Single(s.Quarantaines());
        Assert.Equal(Tronque, File.ReadAllBytes(q));
        Assert.Equal("{}", File.ReadAllText(s.Chemin));
    }

    [Fact]
    public void Traites_Remove_retire_l_entree_et_conserve_les_autres()
    {
        var s = Nouveau("traites");
        var store = new TreatedStore(s.Chemin, new FakeClock(T));
        var ts = T.ToUnixTimeMilliseconds();
        store.Set("A", ts);
        store.Set("B", ts);

        Assert.True(store.Remove("A"));

        Assert.Equal(new[] { "B" }, store.Load().Keys);
    }

    // ---- TRT-04 (repris d'ArchiveStorePurgeTests, retiré avec la purge par préfixe — PERT-2) ----

    /// <summary>TRT-04 — ce qui est archivé NE REVIENT JAMAIS : huit jours plus tard, l'entrée est toujours là.</summary>
    [Fact]
    public void Archive_une_archive_ne_s_evapore_jamais_meme_apres_huit_jours()
    {
        var s = Nouveau("archive");
        File.WriteAllText(s.Chemin, $$"""{"s":{{T.AddDays(-8).ToUnixTimeMilliseconds()}}}""");

        Assert.Contains("s", new ArchiveStore(s.Chemin, new FakeClock(T)).Load());
    }

    /// <summary>L'horodatage écrit vient de l'horloge REÇUE, jamais de celle de la machine.</summary>
    [Fact]
    public void Archive_l_horodatage_vient_de_l_horloge_injectee()
    {
        var s = Nouveau("archive");

        Assert.True(new ArchiveStore(s.Chemin, new FakeClock(T)).Add("s"));

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(s.Chemin));
        Assert.Equal(T.ToUnixTimeMilliseconds(), doc.RootElement.GetProperty("s").GetInt64());
    }

    /// <summary>Garde par réflexion : aucune durée ne siège dans le magasin d'archives, et son horloge est injectable.</summary>
    [Fact]
    public void Archive_le_magasin_ne_connait_aucune_duree_de_vie()
    {
        foreach (var t in new[] { typeof(ArchiveStore), typeof(MagasinMapSessions) })
        {
            var champs = t.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            Assert.DoesNotContain(champs, c => c.FieldType == typeof(TimeSpan));
        }

        var parametres = typeof(ArchiveStore).GetConstructors().SelectMany(c => c.GetParameters()).ToList();
        Assert.NotEmpty(parametres);
        Assert.Contains(parametres, p => p.ParameterType == typeof(IClock));
    }

    // ---------------------------------------------------------------- 42.2-11 : FIAB-R5, socle après quarantaine

    /// <summary>FIAB-R5 : un fichier lu avec succès puis devenu illisible → après la quarantaine, l'écriture repart du DERNIER
    /// ensemble lu (les sessions déjà archivées / traitées ne réapparaissent pas), pas d'un ensemble vide.</summary>
    [Theory]
    [MemberData(nameof(Magasins))]
    public void Apres_quarantaine_le_socle_est_le_dernier_ensemble_lu(string type)
    {
        var s = Nouveau(type);
        Assert.True(s.Ecrire("A"));
        Assert.True(s.Ecrire("B"));
        Assert.Equal(new[] { "A", "B" }, s.Lire().OrderBy(x => x));

        File.WriteAllBytes(s.Chemin, Tronque);   // corrompu entre deux gestes
        Assert.True(s.Ecrire("C"));

        Assert.Single(s.Quarantaines());
        Assert.Equal(new[] { "A", "B", "C" }, s.Lire().OrderBy(x => x));
    }

    /// <summary>FIAB-R5 : sans aucune lecture réussie dans ce processus, le socle reste vide (rien d'inventé).</summary>
    [Theory]
    [MemberData(nameof(Magasins))]
    public void Sans_lecture_reussie_le_socle_apres_quarantaine_reste_vide(string type)
    {
        var s = Nouveau(type);
        File.WriteAllBytes(s.Chemin, Tronque);

        Assert.True(s.Ecrire("X"));

        Assert.Equal(new[] { "X" }, s.Lire());
    }
}
