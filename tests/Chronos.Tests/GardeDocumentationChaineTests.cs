using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// DAT-05 (phase 37) — garde DOCUMENTAIRE de la chaîne de données : <c>README.md</c>, <c>CLAUDE.md</c> et
/// <c>docs/data-sources.md</c> décrivent UNE méthodologie (sonde d'en-têtes → secours OAuth du login Chronos → journal →
/// dernier exact ; seul le plancher « ≥ » n'est pas exact) et ne citent plus aucune source retirée.
///
/// <para>Ces tests ne lisent que des fichiers du dépôt : le chemin de docs/ est INJECTÉ par MSBuild
/// (<c>AssemblyMetadata("CheminDocsChronos")</c>), jamais deviné ; README et CLAUDE.md sont à <c>docs/..</c>. Lecture
/// ANTI-MUETTE : un document absent, vide ou tronqué fait rougir la garde au lieu de la rendre verte pour rien.</para>
/// </summary>
public sealed class GardeDocumentationChaineTests
{
    /// <summary>Termes d'une source retirée : aucun des trois documents ne doit les citer.
    /// La liste GRANDIT : étape 3 ajoute EndpointOAuthClaude ; étape 5 ajoute PontStatusLine, usage.json, pont statusLine,
    /// --statusline. Ne JAMAIS viser « estimation » seul : « ne jamais présenter une estimation comme exacte » est une règle
    /// de doctrine de CLAUDE.md, à garder.</summary>
    internal static readonly string[] TermesRetires =
    {
        "ClaudeOAuthUsageProvider", "GatedOAuthUsageProvider", "ClaudeTokenReader", "WindowsCredentialStore",
        "InventaireMachine", "ClaudeUsageObjectProvider", "StatusLineBridge", "StatusLineInstaller",
        "WeeklyRecalibration", "WeeklyWindow", "FiveHourWindowInference",
        "Usage exact (OAuth)", "Estimation (repli)", "estimation par transcripts", "repli JSONL",
        // étape 3 — la valeur d'enum de la source retirée (jeton de l'app bureau)
        "EndpointOAuthClaude",
    };

    private static string CheminDocs()
    {
        var docs = typeof(GardeDocumentationChaineTests).Assembly
                       .GetCustomAttributes<AssemblyMetadataAttribute>()
                       .FirstOrDefault(a => a.Key == "CheminDocsChronos")?.Value
                   ?? "";
        Assert.False(string.IsNullOrWhiteSpace(docs),
            "L'attribut AssemblyMetadata(\"CheminDocsChronos\") manque : le .csproj de tests doit injecter le chemin de docs/, "
            + "sans quoi cette garde ne lit rien et ne garde rien.");
        return docs;
    }

    private static string CheminDe(string nom) => nom switch
    {
        "docs/data-sources.md" => Path.Combine(CheminDocs(), "data-sources.md"),
        "README.md" => Path.GetFullPath(Path.Combine(CheminDocs(), "..", "README.md")),
        "CLAUDE.md" => Path.GetFullPath(Path.Combine(CheminDocs(), "..", "CLAUDE.md")),
        _ => throw new ArgumentOutOfRangeException(nameof(nom), nom, "document non surveillé"),
    };

    /// <summary>Lit un document surveillé. ANTI-MUET : présent, non vide, et data-sources.md fait plus de 300 lignes.</summary>
    private static string Lire(string nom)
    {
        var chemin = CheminDe(nom);
        Assert.True(File.Exists(chemin), $"{nom} introuvable : {chemin}");
        var texte = File.ReadAllText(chemin);
        Assert.False(string.IsNullOrWhiteSpace(texte), $"{nom} est vide : {chemin}");
        if (nom == "docs/data-sources.md")
        {
            var lignes = texte.Replace("\r\n", "\n").Split('\n').Length;
            Assert.True(lignes > 300, $"{nom} ne fait que {lignes} lignes : la garde ne lit pas le vrai document.");
        }
        return texte;
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("CLAUDE.md")]
    [InlineData("docs/data-sources.md")]
    public void Aucun_document_ne_cite_une_source_retiree(string nom)
    {
        var lignes = Lire(nom).Replace("\r\n", "\n").Split('\n');
        var fautes = (from i in Enumerable.Range(0, lignes.Length)
                      from terme in TermesRetires
                      where lignes[i].Contains(terme, StringComparison.Ordinal)
                      select $"« {terme} » ligne {i + 1}").ToList();
        Assert.True(fautes.Count == 0, $"{nom} cite une source retirée : " + string.Join(" ; ", fautes));
    }

    [Fact]
    public void La_methodologie_dit_que_seul_le_plancher_n_est_pas_exact()
    {
        var section = ContratHooksDocumenteTests.SectionDe(Lire("docs/data-sources.md"), "## 3.");
        Assert.Contains("plancher", section, StringComparison.Ordinal);
        Assert.Contains("≥", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_README_nomme_la_sonde_d_en_tetes()
    {
        var section = ContratHooksDocumenteTests.SectionDe(Lire("README.md"), "## D'où viennent les chiffres");
        Assert.Contains("sonde d'en-têtes", section, StringComparison.Ordinal);
    }

    [Fact]
    public void CLAUDE_md_nomme_la_chaine()
    {
        var claude = Lire("CLAUDE.md");
        Assert.Contains("sonde d'en-têtes", claude, StringComparison.Ordinal);
        Assert.Contains("plancher", claude, StringComparison.Ordinal);
    }
}
