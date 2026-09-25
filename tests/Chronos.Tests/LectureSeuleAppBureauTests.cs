using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// APP-05 — la source app-bureau est en LECTURE SEULE, et ne gêne jamais l'écrivain de l'app. Les trois gardes de la
/// phase 29 vivent ici :
/// <list type="number">
///   <item><b>Au texte</b> : le fichier du lecteur ne porte aucun appel d'écriture, de création, de déplacement ni de
///   suppression, ni aucune lecture intégrale du framework (qui ouvre en partage lecture seule) ; il ouvre en mode
///   « ouvrir », en accès lecture, en partage lecture-écriture-suppression.</item>
///   <item><b>Au comportement</b> : un écrivain en place tenu ouvert n'empêche pas la lecture ; notre poignée
///   n'empêche ni l'écriture en place ni la suppression ; et trois cycles de lecture laissent la racine identique
///   octet pour octet — pas un fichier créé, pas un attribut touché, pas même un cache.</item>
///   <item><b>Au chemin</b> (plan 29-05) : le chemin de l'app bureau n'existe que dans le résolveur de racines, et ce
///   résolveur ne porte aucun des jetons d'écriture interdits au lecteur — aucun code d'écriture ne peut viser le
///   dossier de l'app sans passer par lui.</item>
/// </list>
/// Les deux premières ont été falsifiées par mutation avant commit (écriture ajoutée dans la lecture ; partage réduit à
/// la lecture) : voir 29-02-SUMMARY.md ; la troisième par la mutation (c) : voir 29-05-SUMMARY.md.
///
/// <para>Toutes les racines sont TEMPORAIRES ; aucun test ne vise le dossier réel de l'app. Au nettoyage, l'attribut
/// lecture seule est retiré avant la suppression.</para>
/// </summary>
public class LectureSeuleAppBureauTests : IDisposable
{
    private const string GesteB = "session-courante-geste-b.json";
    private const string Bloquee = "fin-de-tour-blocked.json";
    private const string IdGesteB = "11456cab-d447-42c7-aa85-9920ce64f7ba";

    private static readonly string[] SixFixtures =
    {
        GesteB, Bloquee, "fin-de-tour-completed.json", "fin-de-tour-review-ready.json",
        "sans-postTurnSummary.json", "sans-cliSessionId.json",
    };

    /// <summary>L'instant de référence. Fixe : jamais l'horloge.</summary>
    private static readonly DateTimeOffset M = new(2026, 9, 25, 18, 40, 0, TimeSpan.Zero);

    private readonly List<string> _racines = new();

    public void Dispose()
    {
        foreach (var r in _racines)
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(r, "*", SearchOption.AllDirectories))
                    File.SetAttributes(f, File.GetAttributes(f) & ~FileAttributes.ReadOnly);
                Directory.Delete(r, recursive: true);
            }
            catch { /* nettoyage best-effort */ }
        }
    }

    private string Racine(params string[] fixtures)
    {
        var racine = RacineAppBureau.Creer(M, fixtures);
        _racines.Add(racine);
        return racine;
    }

    private static string Chemin(string racine, string fixture)
        => Path.Combine(RacineAppBureau.DossierUtilisateur(racine), RacineAppBureau.NomFichier(fixture));

    // ------------------------------------------------------------------ Garde au comportement

    /// <summary>Le repli de l'app — une écriture EN PLACE, poignée d'écriture ouverte — n'empêche pas la lecture. Un
    /// lecteur qui ne partagerait que la lecture échouerait ici (falsifié par la mutation b).</summary>
    [Fact]
    public void Un_ecrivain_en_place_ouvert_n_empeche_pas_la_lecture()
    {
        var racine = Racine(GesteB);
        var fichier = Chemin(racine, GesteB);

        using (new FileStream(fichier, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            var l = new LecteurAppBureau(new[] { racine }).Lire(M);

            Assert.True(l.ParSession.ContainsKey(IdGesteB));
            Assert.Equal(0, l.Illisibles);
        }
    }

    /// <summary>Notre poignée, tenue, n'empêche ni l'app d'ouvrir le fichier pour l'écrire en place, ni de le
    /// supprimer (elle supprime des sessions : traces <c>deleted_&lt;uuid&gt;</c>).</summary>
    [Fact]
    public void Notre_poignee_ne_bloque_ni_l_ecriture_en_place_ni_la_suppression()
    {
        var racine = Racine(GesteB, Bloquee);
        var f1 = Chemin(racine, GesteB);
        var f2 = Chemin(racine, Bloquee);

        using (LecteurAppBureau.Ouvrir(f1))
        {
            using var ecrivain = new FileStream(f1, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            Assert.True(ecrivain.CanWrite);
        }

        using (LecteurAppBureau.Ouvrir(f2))
        {
            var suppression = Record.Exception(() => File.Delete(f2));
            Assert.Null(suppression);
        }
        Assert.False(File.Exists(f2));
    }

    /// <summary>Trois cycles de lecture sur une racine peuplée des six fixtures réelles et du bruit du dossier, tout
    /// en lecture seule : la racine en sort identique octet pour octet — chemins, tailles, dates d'écriture, SHA-256,
    /// attributs — et pas une entrée de plus.</summary>
    [Fact]
    public void Trois_lectures_laissent_la_racine_identique_octet_pour_octet()
    {
        var racine = Racine(SixFixtures);
        RacineAppBureau.AjouterBruit(racine, M);
        foreach (var f in Directory.EnumerateFiles(racine, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, File.GetAttributes(f) | FileAttributes.ReadOnly);

        var avant = Empreinte(racine);
        Assert.True(avant.Count >= 14, $"Empreinte de {avant.Count} entrées seulement : la garde ne verrait rien.");

        var lecteur = new LecteurAppBureau(new[] { racine });
        var l1 = lecteur.Lire(M);
        lecteur.Lire(M.AddSeconds(2));
        lecteur.Lire(M.AddSeconds(4));

        // Anti-muet : la lecture a réellement ouvert les fichiers.
        Assert.Equal(6, l1.Enumeres);
        Assert.Equal(5, l1.Valides);
        Assert.Equal(1, l1.SansCliSessionId);

        var apres = Empreinte(racine);
        Assert.Equal(avant.Count, apres.Count);
        Assert.Equal(avant, apres);
    }

    /// <summary>Chaque fichier (chemin relatif, taille, date d'écriture en ticks, SHA-256, attributs) puis chaque
    /// dossier, triés ordinalement.</summary>
    private static List<string> Empreinte(string racine)
    {
        var fichiers = Directory.EnumerateFiles(racine, "*", SearchOption.AllDirectories)
            .Select(f =>
            {
                var info = new FileInfo(f);
                var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)));
                return $"{Path.GetRelativePath(racine, f)} | {info.Length} | {info.LastWriteTimeUtc.Ticks} | {sha} | {info.Attributes}";
            })
            .OrderBy(x => x, StringComparer.Ordinal);
        var dossiers = Directory.EnumerateDirectories(racine, "*", SearchOption.AllDirectories)
            .Select(d => "dossier | " + Path.GetRelativePath(racine, d))
            .OrderBy(x => x, StringComparer.Ordinal);
        return fichiers.Concat(dossiers).ToList();
    }

    // ------------------------------------------------------------------ Garde au texte

    /// <summary>Les jetons qu'aucune ligne du lecteur ne peut porter, commentaires compris : écriture, création,
    /// déplacement, copie, suppression, attributs, et les lectures intégrales ou ouvertures du framework qui partagent
    /// seulement la lecture (elles feraient échouer le repli en place de l'app).</summary>
    private static readonly string[] Interdits =
    {
        "File.Write", "File.Append", "File.Create", "File.Delete", "File.Move", "File.Copy", "File.Replace",
        "File.SetAttributes", "File.SetLastWriteTime", "File.Open(", "File.OpenWrite", "File.OpenRead", "File.ReadAll",
        "FileMode.Create", "FileMode.CreateNew", "FileMode.OpenOrCreate", "FileMode.Truncate", "FileMode.Append",
        "FileAccess.Write", "FileAccess.ReadWrite",
        "Directory.Create", "Directory.Delete", "Directory.Move",
        ".Delete(", ".MoveTo(", ".CopyTo(", ".Create(", ".Write(", ".WriteByte(", ".SetLength(", "StreamWriter",
    };

    /// <summary>La garde au TEXTE : la réflexion ne voit pas un appel d'écriture, le source si. Anti-muette : le
    /// fichier existe, a du corps, et porte bien l'ouverture attendue — sans quoi l'absence d'interdits ne prouverait
    /// rien.</summary>
    [Fact]
    public void Le_lecteur_ne_contient_aucun_appel_d_ecriture_et_ouvre_en_partage_complet()
    {
        var sources = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(sources),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        var fichier = Path.Combine(sources, "Services", "LecteurAppBureau.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);
        Assert.True(texte.Length >= 3000, $"LecteurAppBureau.cs ne fait que {texte.Length} caractères : la garde serait muette.");
        Assert.Contains("FileShare.ReadWrite | FileShare.Delete", texte, StringComparison.Ordinal);
        Assert.Contains("FileMode.Open", texte, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"FileAccess\.Read(?!Write)"), texte);

        var lignes = texte.Split('\n');
        var infractions = new List<string>();
        for (var i = 0; i < lignes.Length; i++)
            foreach (var jeton in Interdits)
                if (lignes[i].Contains(jeton, StringComparison.Ordinal))
                    infractions.Add($"{i + 1} — {jeton}");

        Assert.True(infractions.Count == 0,
            "APP-05 : le lecteur de l'app bureau porte un appel qui écrit, déplace, supprime, ou ouvre sans partager "
            + "l'écriture :\n  " + string.Join("\n  ", infractions));
    }

    // ------------------------------------------------------------------ Garde au chemin (garde n° 2, plan 29-05)

    /// <summary>Ce qui désigne le dossier de l'app bureau dans un source : le nom de son dossier de sessions, et le
    /// préfixe de son paquet MSIX. Un code qui voudrait écrire là devrait porter l'un des deux.</summary>
    private static readonly string[] JetonsDuChemin = { "claude-code-sessions", "Claude_" };

    /// <summary>
    /// Le chemin de l'app bureau n'existe qu'à UN endroit de src/ : le résolveur de racines, qui n'écrit rien (garde
    /// suivante). Aucun code d'écriture ne peut donc viser le dossier de l'app sans passer par lui — et il ne rend aux
    /// écrivains (le balayage) que des racines « Chronos\sessions ».
    ///
    /// <para>Tous les sources de l'application, récursivement, hors <c>obj</c> et <c>bin</c>, commentaires compris : un
    /// littéral égaré dans un commentaire se reformule là où il est (« la racine de l'app bureau, voir le résolveur »),
    /// la garde ne s'assouplit pas. Anti-muette : au moins cent fichiers lus. Falsifiée par la mutation (c) : un
    /// commentaire portant le nom du dossier de sessions ajouté au rapport de diagnostic (29-05-SUMMARY.md).</para>
    /// </summary>
    [Fact]
    public void Le_chemin_de_l_app_bureau_n_existe_que_dans_le_resolveur_de_racines()
    {
        var sources = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(sources),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        Assert.True(Directory.Exists(sources), $"Dossier des sources introuvable : {sources}");

        var fichiers = Directory.EnumerateFiles(sources, "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(sources, f).Replace('\\', '/'))
            .Where(r => !r.Split('/').Any(segment => segment is "obj" or "bin"))
            .ToList();
        Assert.True(fichiers.Count >= 100, $"{fichiers.Count} sources seulement : la garde ne verrait rien.");

        var porteurs = fichiers
            .Where(r =>
            {
                var texte = File.ReadAllText(Path.Combine(sources, r));
                return JetonsDuChemin.Any(j => texte.Contains(j, StringComparison.Ordinal));
            })
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        Assert.True(porteurs.SequenceEqual(new[] { "Services/RacinesEtat.cs" }),
            "APP-05 : le chemin de l'app bureau ne doit exister que dans Services/RacinesEtat.cs, qui n'écrit rien. "
            + "Fichiers qui le portent :\n  " + string.Join("\n  ", porteurs));
    }

    /// <summary>
    /// Le seul porteur du chemin de l'app n'écrit rien : la MÊME liste de jetons interdits que le lecteur
    /// (<see cref="Interdits"/>), appliquée au résolveur de racines. Il nomme des chemins et teste leur existence, rien
    /// d'autre. Anti-muette : le fichier a du corps, énumère bien les paquets et porte bien le chemin — sans quoi
    /// l'absence d'interdits ne prouverait rien.
    /// </summary>
    [Fact]
    public void Le_resolveur_de_racines_n_ecrit_rien()
    {
        var sources = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(sources),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        var fichier = Path.Combine(sources, "Services", "RacinesEtat.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);
        Assert.True(texte.Length >= 1000, $"RacinesEtat.cs ne fait que {texte.Length} caractères : la garde serait muette.");
        Assert.Contains("Directory.EnumerateDirectories", texte, StringComparison.Ordinal);
        Assert.Contains("claude-code-sessions", texte, StringComparison.Ordinal);

        var lignes = texte.Split('\n');
        var infractions = new List<string>();
        for (var i = 0; i < lignes.Length; i++)
            foreach (var jeton in Interdits)
                if (lignes[i].Contains(jeton, StringComparison.Ordinal))
                    infractions.Add($"{i + 1} — {jeton}");

        Assert.True(infractions.Count == 0,
            "APP-05 : le résolveur de racines, seul porteur du chemin de l'app bureau, porte un appel qui écrit, "
            + "déplace, supprime, ou ouvre sans partager l'écriture :\n  " + string.Join("\n  ", infractions));
    }
}
