using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// VAL-06 (phase 43, décisions D-03 et D-04) — garde DOCUMENTAIRE de la release 3.5.0. Elle ferme les reliquats :
/// <list type="bullet">
/// <item>PKG-7 — le commentaire du csproj et la section « Construire depuis les sources » du README nomment le fichier
/// publié sous son nom versionné (<c>Chronos-vX.Y.Z.exe</c>), et le README dit le compte de tests à jour ;</item>
/// <item>PKG-R2 et DS-MAINT-03 — le §5 de <c>docs/publish.md</c> dit la convergence de l'autostart au démarrage
/// (repointé vers l'exe courant, sauf build sous <c>bin\</c>, copie sous le dossier temporaire ou cible plus récente) ;</item>
/// <item>D-04 — le README dit les cinq cadrans, l'orientation, les trois groupes de thèmes et le plein écran ; l'empreinte
/// SHA-256 et la convention d'étiquette <c>exe-vX.Y.Z</c> sont écrites ; le smoke <c>--hook</c> se joue au constat.</item>
/// </list>
///
/// <para>Chemins INJECTÉS par MSBuild (<c>AssemblyMetadata("CheminDocsChronos")</c> pour docs/ et le README à
/// <c>docs/..</c>, <c>CheminSourcesChronos</c> pour le csproj), jamais devinés. Lecture ANTI-MUETTE : un fichier absent ou
/// de moins de 200 caractères fait rougir la garde.</para>
/// </summary>
public sealed class GardeDocumentationReleaseTests
{
    private static string CheminDocs()
    {
        var docs = typeof(GardeDocumentationReleaseTests).Assembly
                       .GetCustomAttributes<AssemblyMetadataAttribute>()
                       .FirstOrDefault(a => a.Key == "CheminDocsChronos")?.Value
                   ?? "";
        Assert.False(string.IsNullOrWhiteSpace(docs),
            "L'attribut AssemblyMetadata(\"CheminDocsChronos\") manque : cette garde ne lirait rien.");
        return docs;
    }

    private static string Lire(string chemin)
    {
        Assert.True(File.Exists(chemin), $"Document introuvable : {chemin}");
        var texte = File.ReadAllText(chemin).Replace("\r\n", "\n");
        Assert.True(texte.Length > 200, $"{chemin} est suspicieusement court : la garde ne lit pas le vrai fichier.");
        return texte;
    }

    private static string Readme() => Lire(Path.GetFullPath(Path.Combine(CheminDocs(), "..", "README.md")));

    private static string Publish() => Lire(Path.Combine(CheminDocs(), "publish.md"));

    private static string Csproj()
    {
        var sources = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrEmpty(sources), "attribut CheminSourcesChronos absent : la garde ne sait plus où lire le csproj");
        return Lire(Path.Combine(sources, "Chronos.csproj"));
    }

    /// <summary>Section d'un document markdown : de la ligne de titre au prochain titre de même niveau ou de niveau
    /// supérieur, ou à la fin du fichier. Les lignes d'un bloc de code (entre ```) ne sont jamais des titres : un
    /// commentaire « # … » de shell ne coupe pas la section. Le titre absent fait rougir la garde.</summary>
    private static string Section(string texte, string titre)
    {
        var niveau = titre.TakeWhile(c => c == '#').Count();
        var lignes = texte.Split('\n');
        var debut = Array.FindIndex(lignes, l => l.StartsWith(titre, StringComparison.Ordinal));
        Assert.True(debut >= 0, $"« {titre} » introuvable");

        var fin = lignes.Length;
        var dansCode = false;
        for (var i = debut + 1; i < lignes.Length; i++)
        {
            if (lignes[i].StartsWith("```", StringComparison.Ordinal)) { dansCode = !dansCode; continue; }
            if (dansCode || !lignes[i].StartsWith('#')) continue;
            var diese = lignes[i].TakeWhile(c => c == '#').Count();
            if (diese <= niveau) { fin = i; break; }
        }
        return string.Join("\n", lignes[debut..fin]);
    }

    [Fact]
    public void Le_commentaire_du_csproj_nomme_le_fichier_publie_en_trois_chiffres()
    {
        var csproj = Csproj();
        Assert.Contains("Chronos-vX.Y.Z.exe", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Chronos-vX.Y.exe", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_README_publie_sous_le_nom_versionne()
    {
        var readme = Readme();
        var construire = Section(readme, "## Construire depuis les sources");
        Assert.Contains("Chronos-v", construire, StringComparison.Ordinal);
        Assert.Contains(".exe", construire, StringComparison.Ordinal);
        Assert.DoesNotContain("plus de 1 600", readme, StringComparison.Ordinal);
        Assert.Contains("plus de 2 200", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_README_dit_les_cadrans_les_orientations_et_les_themes()
    {
        var readme = Readme();
        foreach (var mot in new[] { "Braises", "Fusible", "Marée", "Volets", "Orientation", "Pâle", "Classique", "Vive", "Plein écran" })
            Assert.Contains(mot, readme, StringComparison.Ordinal);
        Assert.DoesNotContain("style de la vue Semaine", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_README_dit_l_empreinte_a_verifier()
    {
        var installation = Section(Readme(), "## Installation (portable, sans droits admin)");
        Assert.Contains("SHA-256", installation, StringComparison.Ordinal);
    }

    [Fact]
    public void Publish_paragraphe_5_dit_la_convergence_de_l_autostart()
    {
        var publish = Publish();
        var debut = publish.IndexOf("\n## 5.", StringComparison.Ordinal);
        Assert.True(debut >= 0, "« ## 5. » introuvable dans docs/publish.md");
        var titre = publish[(debut + 1)..publish.IndexOf('\n', debut + 1)];
        var s5 = Section(publish, titre);

        foreach (var mot in new[] { "repointe", "bin", "temporaire", "plus récent" })
            Assert.Contains(mot, s5, StringComparison.Ordinal);
        Assert.DoesNotContain("Correctif : **re-basculer l'autostart**", s5, StringComparison.Ordinal);
    }

    [Fact]
    public void Publish_dit_l_empreinte_et_la_convention_de_tag()
    {
        var publish = Publish();
        Assert.Contains("SHA-256", publish, StringComparison.Ordinal);
        Assert.Contains("exe-v", publish, StringComparison.Ordinal);
        Assert.Contains("Chronos-v3.5.0.exe", publish, StringComparison.Ordinal);

        var debut = publish.IndexOf("\n## 6.", StringComparison.Ordinal);
        Assert.True(debut >= 0, "« ## 6. » introuvable dans docs/publish.md");
        var titre = publish[(debut + 1)..publish.IndexOf('\n', debut + 1)];
        var s6 = Section(publish, titre);
        // Le smoke --hook n'est plus une vérification faite par l'agent : il se joue au constat de l'utilisateur.
        Assert.Contains("au constat", s6, StringComparison.Ordinal);
        Assert.Contains("ne lance JAMAIS l'exe", s6, StringComparison.Ordinal);
    }
}
