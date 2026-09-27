using System.IO;
using System.Text.RegularExpressions;
using Chronos.Services.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// TOK-03 — les curseurs : ce que Chronos a déjà lu de chaque transcript. Ce que ces tests gravent : la clé est RELATIVE à la
/// racine des projets, à barres obliques, insensible à la casse ; un fichier absent des curseurs est nouveau (lu de 0) ;
/// taille et mtime identiques ET offset == taille → inchangé (pas ouvert) ; taille ≥ offset sinon → grandi (relu depuis
/// l'offset, « touch » compris) ; taille &lt; offset → raccourci (relu de 0) ; un curseur en retrait de la fin (fragment, ligne
/// future) n'est JAMAIS inchangé (D-33-09) ; les disparus sont retirés et comptés ; <c>curseurs.json</c> s'écrit atomiquement
/// et se relit tolérant.
///
/// Dossier temporaire = racine simulée, <c>curseurs.json</c> à côté ; jamais %APPDATA%, jamais ~/.claude.
/// </summary>
public sealed class CurseursTests : IDisposable
{
    private readonly string _racine = Path.Combine(Path.GetTempPath(), "chronos-tests-curseurs-" + Guid.NewGuid().ToString("N"));
    private readonly string _chemin;
    private static readonly DateTimeOffset M1 = Utc("2026-09-20T08:00:00.1234567Z");
    private static readonly DateTimeOffset M2 = Utc("2026-09-20T08:05:00Z");

    public CurseursTests()
    {
        Directory.CreateDirectory(_racine);
        _chemin = Path.Combine(_racine, Curseurs.NomFichier);
    }

    public void Dispose()
    {
        try { Directory.Delete(_racine, recursive: true); } catch { }
    }

    private static DateTimeOffset Utc(string iso)
        => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private Curseurs Neuf() => new(_chemin, _racine);

    private string Abs(params string[] segments) => Path.Combine(new[] { _racine }.Concat(segments).ToArray());

    [Fact]
    public void La_cle_est_relative_a_la_racine_avec_des_barres_obliques_et_insensible_a_la_casse()
    {
        var c = Neuf();

        Assert.Equal("proj-a/s1/subagents/agent-x.jsonl", c.CleRelative(_racine + "\\proj-a\\s1\\subagents\\agent-x.jsonl"));
        Assert.Equal("proj-a/s1.jsonl", c.CleRelative(Abs("proj-a", "s1.jsonl")));

        c.Enregistrer(Abs("Proj-A", "S1.jsonl"), 10, 10, M1);
        c.Enregistrer(Abs("proj-a", "s1.jsonl"), 20, 20, M2);
        Assert.Equal(1, c.Count);
        Assert.Equal(EtatFichier.Inchange, c.Classer(Abs("PROJ-A", "s1.JSONL"), 20, M2, out var depuis));   // la dernière écriture gagne
        Assert.Equal(20, depuis);
        Assert.Equal("curseurs.json", Curseurs.NomFichier);
        Assert.Equal(1, Curseurs.SchemaVersion);
    }

    [Fact]
    public void Un_fichier_absent_des_curseurs_est_nouveau_et_se_lit_de_zero()
    {
        var c = Neuf();

        Assert.Equal(EtatFichier.Nouveau, c.Classer(Abs("proj-a", "s1.jsonl"), 100, M1, out var depuis));
        Assert.Equal(0, depuis);
        Assert.Equal(0, c.Count);   // classer n'enregistre rien
    }

    [Fact]
    public void Un_fichier_dont_taille_et_mtime_n_ont_pas_bouge_est_inchange()
    {
        var c = Neuf();
        var f = Abs("proj-a", "s1.jsonl");
        c.Enregistrer(f, offset: 100, taille: 100, mtime: M1);

        Assert.Equal(EtatFichier.Inchange, c.Classer(f, 100, M1, out var depuis));
        Assert.Equal(100, depuis);
    }

    [Fact]
    public void Un_fichier_qui_a_grandi_se_relit_depuis_son_offset()
    {
        var c = Neuf();
        var f = Abs("proj-a", "s1.jsonl");
        c.Enregistrer(f, 100, 100, M1);

        Assert.Equal(EtatFichier.Grandi, c.Classer(f, 150, M2, out var depuis));
        Assert.Equal(100, depuis);

        // « touch » sans contenu : même taille, mtime différent → grandi, rien à lire, le curseur sera remis à jour.
        Assert.Equal(EtatFichier.Grandi, c.Classer(f, 100, M2, out depuis));
        Assert.Equal(100, depuis);
    }

    [Fact]
    public void Un_fichier_raccourci_se_relit_de_zero()
    {
        var c = Neuf();
        var f = Abs("proj-a", "s1.jsonl");
        c.Enregistrer(f, 100, 100, M1);

        Assert.Equal(EtatFichier.Raccourci, c.Classer(f, 60, M2, out var depuis));
        Assert.Equal(0, depuis);
        Assert.Equal(EtatFichier.Raccourci, c.Classer(f, 60, M1, out depuis));   // même avec le même mtime : la taille décide
        Assert.Equal(0, depuis);
    }

    [Fact]
    public void Un_curseur_bloque_avant_la_fin_du_fichier_n_est_jamais_inchange()
    {
        var c = Neuf();
        var f = Abs("proj-a", "s1.jsonl");
        c.Enregistrer(f, offset: 80, taille: 100, mtime: M1);   // fragment final ou ligne future : le curseur est en retrait

        Assert.Equal(EtatFichier.Grandi, c.Classer(f, 100, M1, out var depuis));   // même taille, même mtime
        Assert.Equal(80, depuis);
    }

    [Fact]
    public void Les_disparus_sont_retires_et_comptes()
    {
        var c = Neuf();
        c.Enregistrer(Abs("proj-a", "s1.jsonl"), 1, 1, M1);
        c.Enregistrer(Abs("proj-a", "s2.jsonl"), 2, 2, M1);
        c.Enregistrer(Abs("proj-b", "s3.jsonl"), 3, 3, M1);

        var presentes = new HashSet<string>(new[] { "proj-a/s1.jsonl", "PROJ-A/S2.JSONL" }, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(1, c.RetirerDisparus(presentes));
        Assert.Equal(2, c.Count);
        Assert.Equal(EtatFichier.Nouveau, c.Classer(Abs("proj-b", "s3.jsonl"), 3, M1, out _));
        Assert.Equal(EtatFichier.Inchange, c.Classer(Abs("proj-a", "s2.jsonl"), 2, M1, out _));
    }

    [Fact]
    public void Sauvegarder_puis_Charger_rend_les_memes_curseurs_et_le_fichier_est_atomique_et_tolerant()
    {
        var c = Neuf();
        c.Enregistrer(Abs("proj-a", "s1.jsonl"), 100, 100, M1);
        c.Enregistrer(Abs("proj-a", "s1", "subagents", "agent-x.jsonl"), 80, 100, M2);

        Assert.True(c.Sauvegarder());
        Assert.Null(c.DerniereErreur);
        Assert.True(File.Exists(_chemin));
        Assert.Empty(Directory.GetFiles(_racine, "*.tmp-*"));

        var json = File.ReadAllText(_chemin);
        Assert.Contains("\"v\":1", Regex.Replace(json, @"\s", ""));
        Assert.Contains("\"fichiers\"", json);
        Assert.Contains("\"proj-a/s1.jsonl\"", json);
        Assert.Contains("\"proj-a/s1/subagents/agent-x.jsonl\"", json);
        Assert.DoesNotContain(_racine.Replace('\\', '/'), json);   // clés RELATIVES : la racine n'apparaît pas
        Assert.DoesNotContain(_racine, json);
        Assert.Contains("\"offset\"", json);
        Assert.Contains("\"taille\"", json);
        Assert.Contains("\"mtime\":\"2026-09-20T08:00:00.1234567+00:00\"", Regex.Replace(json, @"\s", ""));

        var relu = Curseurs.Charger(_chemin, _racine);
        Assert.Equal(2, relu.Count);
        Assert.Equal(EtatFichier.Inchange, relu.Classer(Abs("proj-a", "s1.jsonl"), 100, M1, out var d1));
        Assert.Equal(100, d1);
        Assert.Equal(EtatFichier.Grandi, relu.Classer(Abs("proj-a", "s1", "subagents", "agent-x.jsonl"), 100, M2, out var d2));
        Assert.Equal(80, d2);

        // Tolérance : absent, tronqué, version inconnue, entrée incomplète.
        File.Delete(_chemin);
        Assert.Equal(0, Curseurs.Charger(_chemin, _racine).Count);
        File.WriteAllText(_chemin, "{");
        Assert.Equal(0, Curseurs.Charger(_chemin, _racine).Count);
        File.WriteAllText(_chemin, "{\"v\":2,\"fichiers\":{\"a.jsonl\":{\"offset\":1,\"taille\":1,\"mtime\":\"2026-09-20T08:00:00+00:00\"}}}");
        Assert.Equal(0, Curseurs.Charger(_chemin, _racine).Count);
        File.WriteAllText(_chemin,
            "{\"v\":1,\"fichiers\":{\"a.jsonl\":{\"taille\":1,\"mtime\":\"2026-09-20T08:00:00+00:00\"},"
            + "\"b.jsonl\":{\"offset\":5,\"taille\":5,\"mtime\":\"2026-09-20T08:00:00+00:00\"}}}");
        var partiel = Curseurs.Charger(_chemin, _racine);
        Assert.Equal(1, partiel.Count);
        Assert.Equal(EtatFichier.Inchange, partiel.Classer(Abs("b.jsonl"), 5, Utc("2026-09-20T08:00:00Z"), out _));
        Assert.Equal(EtatFichier.Nouveau, partiel.Classer(Abs("a.jsonl"), 1, M1, out _));
    }
}
