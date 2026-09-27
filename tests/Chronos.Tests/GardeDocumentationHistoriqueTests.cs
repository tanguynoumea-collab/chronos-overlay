using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// ACC-04 (phase 35, D-35-18 / D-35-19) — garde DOCUMENTAIRE de l'Historique : la section « ## Historique d'utilisation » du README
/// (et, plus bas, le §9 de <c>docs/data-sources.md</c>) parle avec les mots du plan (DESIGN_PLAN §4) et n'apprend jamais un mot de
/// projection. Le motif <see cref="Projection"/> est celui de 34-08 (<see cref="GardeVocabulaireHistoriqueTests"/>), copié tel quel.
///
/// <para><b>Périmètre limité à la section</b> (D-35-18, Pitfall 9) : le reste du README décrit l'estimation de repli du CADRAN, qui
/// existe encore (« Un `~` signale une estimation ») ; une garde sur tout le README rougirait à l'entrée pour une vérité.</para>
///
/// <para>Ces tests ne lisent que des fichiers du dépôt (chemin de docs/ injecté par MSBuild, README = docs/../README.md) : aucun
/// réseau, aucun %APPDATA%, aucun ~/.claude.</para>
/// </summary>
public class GardeDocumentationHistoriqueTests
{
    /// <summary>Copie du motif de 34-08 (<c>GardeVocabulaireHistoriqueTests.Projection</c>, privé) : mêmes mots bannis.</summary>
    private static readonly Regex Projection = new(@"épuisé[e]? vers|à ce rythme|projection|prévision|estim(é|ation|er)|tendance|dans \d+ ?h\b",
                                                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>§4 : « relevé (jamais « mesure », jamais « estimation ») ».</summary>
    private static readonly Regex Mesure = new(@"\bmesures?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private const string TitreReadme = "## Historique d'utilisation";

    /// <summary>Chemin de docs/ INJECTÉ par MSBuild (<c>AssemblyMetadata("CheminDocsChronos")</c>), jamais deviné.</summary>
    private static string CheminDocs()
    {
        var racine = typeof(GardeDocumentationHistoriqueTests).Assembly
                         .GetCustomAttributes<AssemblyMetadataAttribute>()
                         .FirstOrDefault(a => a.Key == "CheminDocsChronos")?.Value ?? "";
        Assert.False(string.IsNullOrWhiteSpace(racine),
            "L'attribut AssemblyMetadata(\"CheminDocsChronos\") manque : cette garde ne lirait rien.");
        return racine;
    }

    private static string Lire(string chemin)
    {
        Assert.True(File.Exists(chemin), $"Document introuvable : {chemin}");
        return File.ReadAllText(chemin).Replace("\r\n", "\n");
    }

    private static string Readme() => Lire(Path.GetFullPath(Path.Combine(CheminDocs(), "..", "README.md")));

    private static string SectionReadme() => ContratHooksDocumenteTests.SectionDe(Readme(), TitreReadme);

    private static int LignesNonVides(string texte) => texte.Split('\n').Count(l => !string.IsNullOrWhiteSpace(l));

    [Fact]
    public void La_section_Historique_du_README_existe_et_n_est_pas_muette()
    {
        var section = SectionReadme();

        Assert.True(LignesNonVides(section) >= 25,
            $"La section « {TitreReadme} » du README n'a que {LignesNonVides(section)} ligne(s) non vide(s) : elle ne décrit pas la fenêtre.");
    }

    [Fact]
    public void La_section_Historique_du_README_parle_avec_les_mots_du_plan()
    {
        var section = SectionReadme();

        // Noms de vues, de pistes et de gestes : la casse peut suivre la phrase (« Niveau » en titre, « niveau » dans le texte).
        string[] sansCasse =
        {
            "Semaine de forfait", "4 semaines", "Niveau", "Rythme", "Tokens Claude Code", "Couverture", "Fenêtres 5 h",
            "Pistes", "Simplifié", "Tuiles", "double-clic au centre du cadran", "Historique d'utilisation", "Ouvrir",
        };
        foreach (var mot in sansCasse)
            Assert.True(section.Contains(mot, StringComparison.OrdinalIgnoreCase), $"Mot du plan absent de la section du README : « {mot} ».");

        // Mots de l'honnêteté : mot pour mot (DESIGN_PLAN §4).
        string[] exacts =
        {
            "relevé", "reset 5 h", "reset hebdo", "trou", "Chronos arrêté", "jeton invalide", "sonde refusée", "épuisée",
            "répartition inconnue", "consommé ailleurs", "journal ouvert le", "dernière écriture",
        };
        foreach (var mot in exacts)
            Assert.True(section.Contains(mot, StringComparison.Ordinal), $"Mot du plan absent de la section du README : « {mot} ».");

        Assert.Matches(new Regex(@"\bJour\b"), section);
    }

    [Fact]
    public void Aucune_projection_dans_la_section_Historique_du_README()
    {
        var section = SectionReadme();
        Assert.True(LignesNonVides(section) >= 5, "anti-muet : la section du README est vide, la garde ne lirait rien.");

        var infractions = section.Split('\n')
            .Where(l => Projection.IsMatch(l) || Mesure.IsMatch(l))
            .ToList();

        Assert.True(infractions.Count == 0,
            "Mot de projection (ou « mesure ») dans la section « Historique d'utilisation » du README — §4 : relevé, jamais "
            + "estimation ni mesure ; rien n'annonce l'avenir :\n" + string.Join("\n", infractions));
    }

    [Fact]
    public void Le_README_dit_le_vrai_geste_du_centre()
    {
        var readme = Readme();

        var centre = readme.Split('\n').Where(l => l.Contains("**Au centre**", StringComparison.Ordinal)).ToList();
        Assert.Single(centre);
        Assert.Contains("double-clic", centre[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Historique", centre[0], StringComparison.Ordinal);
        Assert.DoesNotContain("re-clique", readme, StringComparison.Ordinal);

        var stack = ContratHooksDocumenteTests.SectionDe(readme, "## Stack");
        Assert.DoesNotContain("1 100", stack, StringComparison.Ordinal);
        var nombre = Regex.Match(stack, @"plus de (\d) (\d{3}) tests");
        Assert.True(nombre.Success, "La ligne « Stack » ne dit plus le nombre de tests (« plus de N NNN tests »).");
        Assert.True(int.Parse(nombre.Groups[1].Value + nombre.Groups[2].Value) >= 1600,
            "La ligne « Stack » annonce moins de tests que la suite n'en compte (1622 à l'entrée de 35-05).");
    }

    // ── §9 de docs/data-sources.md ────────────────────────────────────────────────────────────────────────────────────────────

    private const string TitreParagrapheNeuf = "## 9. Lecture par la fenêtre Historique";

    private static string DataSources() => Lire(Path.Combine(CheminDocs(), "data-sources.md"));

    [Fact]
    public void Le_paragraphe_neuf_dit_comment_la_fenetre_lit_le_journal()
    {
        var section = ContratHooksDocumenteTests.SectionDe(DataSources(), TitreParagrapheNeuf);
        Assert.True(LignesNonVides(section) >= 15, $"Le §9 est muet ({LignesNonVides(section)} ligne(s) non vide(s)).");

        foreach (var mot in new[] { "`ISourceHistorique`", "`InstantDAnalyse`", "une seule lecture", "veille", "0,01", "Écarts connus" })
            Assert.True(section.Contains(mot, StringComparison.Ordinal), $"Le §9 ne dit pas « {mot} ».");

        // Au moins cinq écarts connus des maquettes, un par ligne de liste, sous leur propre sous-titre.
        var lignes = section.Split('\n');
        var debut = Array.FindIndex(lignes, l => l.StartsWith("### ", StringComparison.Ordinal) && l.Contains("Écarts connus", StringComparison.Ordinal));
        Assert.True(debut >= 0, "Le §9 n'a pas de sous-partie « ### Écarts connus des maquettes ».");
        var ecarts = lignes.Skip(debut + 1).TakeWhile(l => !l.StartsWith("### ", StringComparison.Ordinal) && l.Trim() != "---")
                           .Count(l => l.StartsWith("- ", StringComparison.Ordinal));
        Assert.True(ecarts >= 5, $"Le §9 ne liste que {ecarts} écart(s) connu(s) des maquettes (au moins 5 attendus).");

        var infractions = lignes.Where(l => Projection.IsMatch(l) || Mesure.IsMatch(l)).ToList();
        Assert.True(infractions.Count == 0,
            "Mot de projection (ou « mesure ») dans le §9 de data-sources.md :\n" + string.Join("\n", infractions));
    }

    [Fact]
    public void La_ligne_finale_nomme_le_paragraphe_huit_et_le_neuf()
    {
        var derniere = DataSources().Split('\n').Last(l => !string.IsNullOrWhiteSpace(l));

        Assert.Contains("§8", derniere, StringComparison.Ordinal);   // la garde de 33-05 (ContratAgregatsDocumenteTests) la lit aussi
        Assert.Contains("§9", derniere, StringComparison.Ordinal);
    }
}
