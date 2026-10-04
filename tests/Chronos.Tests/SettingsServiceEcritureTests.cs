using System.IO;
using System.Text;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// 42.2-02 (MAT-3, DATA-5, FIAB-2, FIAB-3, DATA-11) — un mode de panne par test : aucune réécriture de settings.json ne perd
/// l'original. Illisible → QUARANTAINE (renommage, octets identiques) puis écriture ; Inaccessible (E/S passagère) → aucune
/// écriture, aucune quarantaine ; quarantaine impossible → aucune écriture, blocage signalé. Save / Modifier ne lèvent jamais.
/// Dossiers sous <c>Path.GetTempPath()</c> uniquement : jamais le vrai %APPDATA%\Chronos.
/// </summary>
public sealed class SettingsServiceEcritureTests : IDisposable
{
    private readonly string _dir;
    private readonly ChronosPaths _paths;
    private readonly SettingsService _service;

    public SettingsServiceEcritureTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "chronos-ecriture-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
        Assert.StartsWith(Path.GetTempPath(), _dir);
        _paths = new ChronosPaths(Path.Combine(_dir, "usage.json"), Path.Combine(_dir, "projects"));
        _service = new SettingsService(_paths);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* nettoyage best-effort */ }
    }

    private const string Illisible = "{\"ThemeKey\":\"nord\"";

    private void Ecrire(string texte) => File.WriteAllText(_paths.SettingsFile, texte);
    private void EcrireOctets(byte[] octets) => File.WriteAllBytes(_paths.SettingsFile, octets);
    private string[] Quarantaines() => Directory.GetFiles(_dir, "settings.illisible-*.json");
    private string Journal() =>
        File.Exists(Path.Combine(_dir, JournalIncidents.NomFichier)) ? File.ReadAllText(Path.Combine(_dir, JournalIncidents.NomFichier)) : "";
    private int LignesIncident() =>
        Journal().Split('\n').Count(l => l.TrimStart().StartsWith(JournalIncidents.Marqueur, StringComparison.Ordinal));

    // ---------------------------------------------------------------------------------- Illisible → quarantaine

    [Fact]
    public void Illisible_est_mis_en_quarantaine_puis_la_mutation_est_ecrite()
    {
        var octets = Encoding.UTF8.GetBytes(Illisible);
        EcrireOctets(octets);

        var rendu = _service.Modifier(s => s with { ThemeKey = "rose" });

        Assert.Equal("rose", rendu.ThemeKey);
        var q = Assert.Single(Quarantaines());
        Assert.Equal(octets, File.ReadAllBytes(q));
        Assert.Equal("rose", _service.Load().ThemeKey);
        Assert.Equal(IssueLectureReglages.Lu, _service.DerniereLecture.Issue);
        Assert.Equal(q, _service.DerniereQuarantaine);
        Assert.Equal(1, _service.Quarantaines);
        Assert.Contains("[incident]", Journal());
        Assert.Contains("mis en quarantaine", Journal());
        Assert.Null(_service.DerniereEcritureRefusee);
    }

    [Fact]
    public void Fichier_vide_est_illisible_et_mis_en_quarantaine_au_Save()
    {
        EcrireOctets(Array.Empty<byte>());

        _service.Load();
        Assert.Equal(IssueLectureReglages.Illisible, _service.DerniereLecture.Issue);

        Assert.True(_service.Save(new ChronosSettings { ThemeKey = "nord" }));

        var q = Assert.Single(Quarantaines());
        Assert.Equal(0, new FileInfo(q).Length);
        Assert.Equal("nord", _service.Load().ThemeKey);
    }

    [Fact]
    public void Load_seul_ne_renomme_rien()
    {
        Ecrire(Illisible);

        var s = _service.Load();

        Assert.Equal(new ChronosSettings(), s);
        Assert.Equal(IssueLectureReglages.Illisible, _service.DerniereLecture.Issue);
        Assert.False(_service.DerniereLecture.EstFiable);
        Assert.Empty(Quarantaines());
        Assert.Equal(Illisible, File.ReadAllText(_paths.SettingsFile));
    }

    [Fact]
    public void Quarantaine_impossible_bloque_l_ecriture_sans_lever()
    {
        var octets = Encoding.UTF8.GetBytes(Illisible);
        EcrireOctets(octets);

        ChronosSettings rendu;
        // Lisible mais non renommable : pas de FileShare.Delete.
        using (new FileStream(_paths.SettingsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            rendu = _service.Modifier(s => s with { ThemeKey = "rose" });
        }

        Assert.Equal("rose", rendu.ThemeKey);   // en mémoire seulement
        Assert.Equal(octets, File.ReadAllBytes(_paths.SettingsFile));
        Assert.Empty(Quarantaines());
        Assert.Contains("quarantaine impossible", _service.DerniereEcritureRefusee);
        Assert.Equal(0, _service.Quarantaines);
        Assert.Contains("réglages non enregistrés", Journal());
    }

    [Fact]
    public void Deux_quarantaines_dans_la_meme_seconde_ont_des_noms_distincts()
    {
        Ecrire(Illisible);
        Assert.True(_service.Save(new ChronosSettings { ThemeKey = "a" }));
        Ecrire("pas du json 2");
        Assert.True(_service.Save(new ChronosSettings { ThemeKey = "b" }));

        // Deux noms distincts (le second porte le suffixe -1 s'ils tombent dans la même seconde) : aucune n'écrase l'autre.
        var qs = Quarantaines();
        Assert.Equal(2, qs.Length);
        var contenus = qs.Select(File.ReadAllText).ToHashSet();
        Assert.Contains(Illisible, contenus);
        Assert.Contains("pas du json 2", contenus);
        Assert.Equal(2, _service.Quarantaines);
    }

    [Fact]
    public void Suffixe_quand_le_nom_de_quarantaine_existe_deja()
    {
        // Pré-occupe tous les noms possibles de la seconde courante et de la suivante : la quarantaine doit suffixer.
        var maintenant = DateTime.Now;
        foreach (var t in new[] { maintenant, maintenant.AddSeconds(1) })
            File.WriteAllText(Path.Combine(_dir, "settings.illisible-" + t.ToString("yyyyMMdd-HHmmss") + ".json"), "occupé");
        Ecrire(Illisible);

        Assert.True(_service.Save(new ChronosSettings()));

        var q = _service.DerniereQuarantaine!;
        Assert.EndsWith("-1.json", q, StringComparison.Ordinal);
        Assert.Equal(Illisible, File.ReadAllText(q));
        Assert.All(Quarantaines().Where(f => f != q), f => Assert.Equal("occupé", File.ReadAllText(f)));
    }

    // ---------------------------------------------------------------------------------- Marqueur QuarantaineReglagesDepuis

    [Fact]
    public void Marqueur_pose_apres_quarantaine_et_conserve_par_les_Modifier_suivants()
    {
        Ecrire(Illisible);

        _service.Modifier(s => s with { ThemeKey = "rose" });
        var marqueur = _service.Load().QuarantaineReglagesDepuis;

        Assert.NotNull(marqueur);
        Assert.InRange(marqueur!.Value, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));

        _service.Modifier(s => s with { ThemeKey = "nord" });
        var relu = _service.Load();
        Assert.Equal("nord", relu.ThemeKey);
        Assert.Equal(marqueur, relu.QuarantaineReglagesDepuis);
    }

    [Fact]
    public void Modifier_qui_efface_le_marqueur_sur_fichier_illisible_laisse_le_marqueur_null()
    {
        Ecrire(Illisible);

        var rendu = _service.Modifier(s => s with { ThemeKey = "rose", QuarantaineReglagesDepuis = null });

        Assert.Single(Quarantaines());
        Assert.Null(rendu.QuarantaineReglagesDepuis);
        Assert.Null(_service.Load().QuarantaineReglagesDepuis);
        Assert.Equal("rose", _service.Load().ThemeKey);
    }

    [Fact]
    public void Marqueur_absent_ou_fautif_est_lu_null_sans_quarantaine()
    {
        Ecrire("{\"ThemeKey\":\"nord\"}");
        Assert.Null(_service.Load().QuarantaineReglagesDepuis);

        Ecrire("{\"ThemeKey\":\"nord\",\"QuarantaineReglagesDepuis\":\"pas une date\"}");
        var s = _service.Load();
        Assert.Null(s.QuarantaineReglagesDepuis);
        Assert.Equal("nord", s.ThemeKey);
        Assert.Contains("QuarantaineReglagesDepuis", _service.DernieresRetombees);

        _service.Modifier(x => x with { ThemeKey = "rose" });
        Assert.Empty(Quarantaines());
        Assert.Null(_service.Load().QuarantaineReglagesDepuis);
    }

    [Fact]
    public void Aucune_ecriture_sans_quarantaine_ne_pose_le_marqueur()
    {
        _service.Modifier(s => s with { ThemeKey = "nord" });   // absent
        Assert.Null(_service.Load().QuarantaineReglagesDepuis);
        Assert.True(_service.Save(new ChronosSettings { ThemeKey = "rose" }));   // lu
        Assert.Null(_service.Load().QuarantaineReglagesDepuis);
    }

    // ---------------------------------------------------------------------------------- Inaccessible (E/S passagère)

    [Fact]
    public void Fichier_verrouille_est_inaccessible_sans_quarantaine_ni_ecriture()
    {
        var octets = Encoding.UTF8.GetBytes("{\"ThemeKey\":\"nord\"}");
        EcrireOctets(octets);

        using (new FileStream(_paths.SettingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var s = _service.Load();
            Assert.Equal(new ChronosSettings(), s);
            Assert.Equal(IssueLectureReglages.Inaccessible, _service.DerniereLecture.Issue);
            Assert.NotNull(_service.DerniereLecture.Cause);

            _service.Modifier(x => x with { ThemeKey = "rose" });
            Assert.False(_service.Save(new ChronosSettings { ThemeKey = "rose" }));
        }

        Assert.Equal(octets, File.ReadAllBytes(_paths.SettingsFile));
        Assert.Empty(Quarantaines());
        Assert.Contains("lecture inaccessible", _service.DerniereEcritureRefusee);

        _service.Modifier(x => x with { ThemeKey = "rose" });
        Assert.Equal("rose", _service.Load().ThemeKey);
        Assert.Null(_service.DerniereEcritureRefusee);
    }

    [Fact]
    public void Refus_repete_pour_la_meme_cause_n_ajoute_qu_une_ligne_d_incident()
    {
        Ecrire("{\"ThemeKey\":\"nord\"}");

        using (new FileStream(_paths.SettingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            _service.Modifier(x => x with { ThemeKey = "a" });
            _service.Modifier(x => x with { ThemeKey = "b" });
            _service.Save(new ChronosSettings { ThemeKey = "c" });
        }

        Assert.Equal(3, _service.EcrituresRefusees);
        Assert.Equal(1, LignesIncident());
    }

    [Fact]
    public void Lecteur_concurrent_empeche_le_Move_Save_rend_false_sans_tmp_residuel()
    {
        Ecrire("{\"ThemeKey\":\"nord\"}");

        using (new FileStream(_paths.SettingsFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(_service.Save(new ChronosSettings { ThemeKey = "rose" }));
        }

        Assert.Empty(Directory.GetFiles(_dir, "settings.json.tmp-*"));
        Assert.Equal("nord", _service.Load().ThemeKey);
        Assert.StartsWith("écriture", _service.DerniereEcritureRefusee);
    }

    // ---------------------------------------------------------------------------------- Cas nominaux

    [Fact]
    public void Absent_Modifier_ecrit()
    {
        var rendu = _service.Modifier(s => s with { ThemeKey = "nord" });

        Assert.Equal("nord", rendu.ThemeKey);
        Assert.Equal("nord", _service.Load().ThemeKey);
        Assert.Empty(Quarantaines());
    }

    [Fact]
    public void Contenu_identique_n_est_pas_reecrit()
    {
        var x = new ChronosSettings { ThemeKey = "nord" };
        Assert.True(_service.Save(x));
        var date = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(_paths.SettingsFile, date);

        Assert.True(_service.Save(x));
        _service.Modifier(s => s);

        Assert.Equal(date, File.GetLastWriteTimeUtc(_paths.SettingsFile));
    }

    [Fact]
    public void Une_ecriture_reussie_apres_un_refus_efface_le_refus()
    {
        Ecrire(Illisible);
        using (new FileStream(_paths.SettingsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            _service.Modifier(s => s with { ThemeKey = "rose" });
        Assert.NotNull(_service.DerniereEcritureRefusee);

        Ecrire("{\"ThemeKey\":\"nord\"}");   // réparé à la main
        _service.Modifier(s => s with { ThemeKey = "rose" });

        Assert.Null(_service.DerniereEcritureRefusee);
        Assert.Equal("rose", _service.Load().ThemeKey);
    }

    // ---------------------------------------------------------------------------------- Lecture de démarrage

    [Fact]
    public void La_lecture_de_demarrage_reste_celle_du_demarrage()
    {
        Ecrire(Illisible);

        _service.ChargerPourDemarrage();
        Assert.Equal(IssueLectureReglages.Illisible, _service.LectureDuDemarrage!.Issue);

        Ecrire("{\"ThemeKey\":\"nord\"}");
        _service.Load();

        Assert.Equal(IssueLectureReglages.Lu, _service.DerniereLecture.Issue);
        Assert.Equal(IssueLectureReglages.Illisible, _service.LectureDuDemarrage!.Issue);
        Assert.True(_service.LecturesNonAbouties >= 1);
        Assert.NotNull(_service.DerniereLectureNonAboutie);
    }

    // ---------------------------------------------------------------------------------- Garde de source

    /// <summary>Plus aucun lire-modifier-écrire hors de SettingsService (MAT-3) ; le widget n'écrit plus pendant le glisser (FIAB-2).</summary>
    [Fact]
    public void Garde_de_source_tous_les_sites_passent_par_Modifier()
    {
        var src = GardesPerimetreTests.CheminSources();
        var service = Path.Combine(src, "Services", "SettingsService.cs");
        Assert.True(File.Exists(service), $"Garde muette : {service} introuvable");

        var sep = Path.DirectorySeparatorChar;
        var fichiers = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(sep + "bin" + sep) && !f.Contains(sep + "obj" + sep))
            .Where(f => !string.Equals(Path.GetFullPath(f), Path.GetFullPath(service), StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(fichiers.Count > 50, $"Garde muette : {fichiers.Count} sources vues");

        var lireEcrire = new System.Text.RegularExpressions.Regex(@"\.Save\(\s*\w*\.?_?\w*\.Load\(\)");
        var mutationNue = new System.Text.RegularExpressions.Regex(@"\.Save\(\s*mutat");
        foreach (var f in fichiers)
        {
            var texte = File.ReadAllText(f);
            Assert.False(lireEcrire.IsMatch(texte), $"Save(…Load()…) dans {f}");
            Assert.False(mutationNue.IsMatch(texte), $"Save(mutation…) dans {f}");
        }

        string Lire(params string[] morceaux) => File.ReadAllText(Path.Combine(new[] { src }.Concat(morceaux).ToArray()));
        var vm = Lire("ViewModels", "MainViewModel.cs");
        var sessions = Lire("Views", "SessionsController.cs");
        var overlay = Lire("Services", "OverlayController.cs");
        var historique = Lire("Services", "Historique", "ReglagesHistorique.cs");

        Assert.DoesNotContain("_settingsService.Save(", vm, StringComparison.Ordinal);
        Assert.DoesNotContain("LocationChanged", sessions, StringComparison.Ordinal);
        Assert.Contains("DeplacementTermine +=", sessions, StringComparison.Ordinal);
        foreach (var texte in new[] { overlay, historique, sessions, vm })
            Assert.Contains(".Modifier(", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("Save(", overlay, StringComparison.Ordinal);
    }

    [Fact]
    public void Sans_ChargerPourDemarrage_la_lecture_de_demarrage_est_nulle()
    {
        _service.Load();
        Assert.Null(_service.LectureDuDemarrage);
        Assert.Equal(0, _service.LecturesNonAbouties);
    }
}
