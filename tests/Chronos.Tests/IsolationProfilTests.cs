using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// 42.2-02 — garde de SOURCE : aucun test ne doit viser le vrai profil (%APPDATA%\Chronos, ~/.claude).
///
/// Depuis 42.2-02, <c>SettingsService</c> peut RENOMMER un settings.json illisible (quarantaine) : un test construit sur
/// <c>ChronosPaths.Default()</c> pourrait déplacer le vrai fichier de l'utilisateur. Seules formes tolérées :
/// <list type="bullet">
///   <item><c>ChronosPaths.Default() with { UsageFile = tmp }</c> (dossier Chronos redirigé vers un dossier temporaire) ;</item>
///   <item><c>ChronosPaths.Default().SettingsFile</c> lu comme SENTINELLE (horodatage comparé avant / après, jamais écrit).</item>
/// </list>
/// </summary>
public class IsolationProfilTests
{
    private static string CheminTests() =>
        Path.GetFullPath(Path.Combine(GardesPerimetreTests.CheminSources(), "..", "..", "tests", "Chronos.Tests"));

    private static IEnumerable<string> SourcesDeTests()
    {
        var racine = CheminTests();
        return Directory.EnumerateFiles(racine, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void Aucun_test_ne_construit_ses_services_sur_le_vrai_profil()
    {
        var fichiers = SourcesDeTests().ToList();
        // Anti-muet : la garde doit voir les tests, dont elle-même.
        Assert.Contains(fichiers, f => f.EndsWith("IsolationProfilTests.cs", StringComparison.Ordinal));
        Assert.True(fichiers.Count > 50, $"Trop peu de sources de tests vues ({fichiers.Count}) : garde muette ?");

        var interdit = new Regex(@"ChronosPaths\.Default\(\)(?!\s*with\s*\{)(?!\.SettingsFile\b)");
        var dossierProfil = new Regex(@"SpecialFolder\.(ApplicationData|UserProfile|Startup)|GetEnvironmentVariable\(\s*""(APPDATA|USERPROFILE)""");
        var fautes = new List<string>();
        foreach (var f in fichiers)
        {
            if (f.EndsWith("IsolationProfilTests.cs", StringComparison.Ordinal)) continue;   // les motifs ci-dessus
            var lignes = File.ReadAllLines(f);
            for (var i = 0; i < lignes.Length; i++)
            {
                var l = lignes[i];
                if (l.TrimStart().StartsWith("//", StringComparison.Ordinal) || l.TrimStart().StartsWith("///", StringComparison.Ordinal)) continue;
                if (interdit.IsMatch(l) || dossierProfil.IsMatch(l))
                    fautes.Add($"{Path.GetFileName(f)}:{i + 1} : {l.Trim()}");
            }
        }
        Assert.True(fautes.Count == 0, "Tests visant le vrai profil :\n" + string.Join("\n", fautes));
    }

    [Fact]
    public void La_sentinelle_du_vrai_settings_n_est_jamais_passee_a_un_service()
    {
        // La forme tolérée « ChronosPaths.Default().SettingsFile » ne doit servir qu'à LIRE l'horodatage.
        foreach (var f in SourcesDeTests())
        {
            if (f.EndsWith("IsolationProfilTests.cs", StringComparison.Ordinal)) continue;
            var texte = File.ReadAllText(f);
            Assert.DoesNotMatch(new Regex(@"new\s+SettingsService\(\s*ChronosPaths\.Default\(\)"), texte);
            Assert.DoesNotMatch(new Regex(@"(Write|Append|Move|Delete|Copy)\w*\(\s*ChronosPaths\.Default\(\)"), texte);
        }
    }

    /// <summary>
    /// 42.2-03 — les magasins du widget (archived.json, treated.json) se rabattent sur %APPDATA%\Chronos quand on ne leur
    /// donne pas de chemin, et peuvent désormais y RENOMMER un fichier illisible (quarantaine) ou y JOURNALISER un incident.
    /// Interdit en test : un <c>ArchiveStore</c> / <c>TreatedStore</c> construit sans chemin (ou avec <c>null</c>), et tout
    /// <c>new SessionMonitor(…)</c> sans archive (son défaut est un ArchiveStore sur le chemin par défaut).
    /// </summary>
    [Fact]
    public void Aucun_test_ne_construit_un_magasin_de_sessions_sur_le_vrai_profil()
    {
        var sansChemin = new Regex(@"new\s+(ArchiveStore|TreatedStore)\(\s*(\)|null\b)");
        var fautes = new List<string>();
        var moniteursVus = 0;
        foreach (var f in SourcesDeTests())
        {
            if (f.EndsWith("IsolationProfilTests.cs", StringComparison.Ordinal)) continue;
            var texte = SansCommentaires(File.ReadAllText(f));
            foreach (Match m in sansChemin.Matches(texte))
                fautes.Add($"{Path.GetFileName(f)}:{Ligne(texte, m.Index)} : {m.Value}");

            const string motif = "new SessionMonitor(";
            for (var i = texte.IndexOf(motif, StringComparison.Ordinal); i >= 0; i = texte.IndexOf(motif, i + 1, StringComparison.Ordinal))
            {
                if (i > 0 && texte[i - 1] == '"') continue;   // motif cité dans une chaîne (gardes de source)
                var args = Arguments(texte, i + motif.Length);
                if (args is null) continue;
                moniteursVus++;
                var positionnels = args.Count(a => !Regex.IsMatch(a, @"^\s*\w+\s*:(?!:)"));
                var archiveNommee = args.Any(a => Regex.IsMatch(a, @"^\s*archive\s*:"));
                if (positionnels < 3 && !archiveNommee)
                    fautes.Add($"{Path.GetFileName(f)}:{Ligne(texte, i)} : new SessionMonitor sans archive (défaut = vrai %APPDATA%)");
            }
        }
        Assert.True(moniteursVus > 20, $"Trop peu de SessionMonitor vus ({moniteursVus}) : garde muette ?");
        Assert.True(fautes.Count == 0, "Magasins de sessions sur le vrai profil :\n" + string.Join("\n", fautes));
    }

    private static int Ligne(string texte, int index) => texte.AsSpan(0, index).Count('\n') + 1;

    // Retire les commentaires « // … » (fin de ligne) en gardant les sauts de ligne ; les chaînes ne sont pas analysées
    // finement — suffisant pour des sources de tests.
    private static string SansCommentaires(string texte) =>
        Regex.Replace(texte, @"(?m)//[^\r\n]*$", "");

    // Arguments de premier niveau d'un appel dont la parenthèse ouvrante précède `debut` ; null si non refermé.
    private static List<string>? Arguments(string texte, int debut)
    {
        var args = new List<string>();
        var profondeur = 0;
        var courant = new System.Text.StringBuilder();
        var dansChaine = false;
        for (var i = debut; i < texte.Length; i++)
        {
            var c = texte[i];
            if (dansChaine)
            {
                courant.Append(c);
                if (c == '\\' && i + 1 < texte.Length) { courant.Append(texte[++i]); continue; }
                if (c == '"') dansChaine = false;
                continue;
            }
            switch (c)
            {
                case '"': dansChaine = true; courant.Append(c); break;
                case '(' or '[' or '{': profondeur++; courant.Append(c); break;
                case ')' or ']' or '}' when profondeur > 0: profondeur--; courant.Append(c); break;
                case ')':
                    if (courant.ToString().Trim().Length > 0) args.Add(courant.ToString());
                    return args;
                case ',' when profondeur == 0: args.Add(courant.ToString()); courant.Clear(); break;
                default: courant.Append(c); break;
            }
        }
        return null;
    }
}
