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
}
