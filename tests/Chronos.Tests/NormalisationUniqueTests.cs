using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// GARDE DE NON-RETOUR HDR-05 — aucune conversion d'unité d'usage ne doit vivre ailleurs que dans
/// <c>UsageNormalization</c>.
///
/// Pourquoi un balayage du TEXTE source et non de la réflexion : la réflexion ne peut pas voir un
/// « / 100 ». Seul le texte du fichier porte l'information. Le chemin des sources est INJECTÉ par
/// MSBuild (attribut <c>AssemblyMetadata("CheminSourcesChronos")</c> posé dans le .csproj) et jamais
/// deviné — CLAUDE.md interdit de localiser un assembly par son chemin de fichier (cette propriété est
/// VIDE en publication mono-fichier), et une remontée de dossiers depuis
/// <c>AppContext.BaseDirectory</c> casserait en silence au premier changement d'agencement de sortie.
///
/// Cette classe ne lit que des fichiers .cs du dépôt : aucun réseau, aucun %APPDATA%, aucun jeton.
/// </summary>
public class NormalisationUniqueTests
{
    // Motifs de conversion d'unité d'USAGE. Le lookbehind (?<!/) empêche « // 100 collisions »
    // (ClaudeSettingsReconciler.cs) de déclencher un faux positif : dans « // », le second slash est
    // précédé d'un slash. « ToUnixTimeMilliseconds » n'est pas visé : c'est une SORTIE d'instant, pas
    // une lecture d'unité d'usage.
    private static readonly (string Motif, string Pourquoi)[] Interdits =
    {
        (@"(?<!/)/\s*100(\.0)?(?![0-9])", "division par 100 : pourcentage -> fraction"),
        (@"\*\s*100(\.0)?(?![0-9])",      "multiplication par 100 : fraction -> pourcentage"),
        (@"FromUnixTimeSeconds",           "epoch secondes -> instant"),
        (@"FromUnixTimeMilliseconds",      "epoch millisecondes -> instant"),
        (@"DateTimeOffset\.TryParse",      "texte ISO -> instant"),
    };

    // EXEMPTIONS NOMINATIVES ET DOCUMENTÉES. Chacune lit un epoch ou une date qui n'est PAS une
    // donnée d'usage — HDR-05 porte sur l'unité du quota, pas sur toute date du dépôt.
    private static readonly (string Fichier, string Pourquoi)[] Exemptions =
    {
        ("UsageNormalization.cs",        "LE point unique — c'est ici que les conversions doivent vivre"),
        ("ClaudeTokenReader.cs",         "expiration de JETON (expiresAt), jamais un quota"),
        ("SessionMonitor.cs",            "horodatage de SESSION Claude Code, jamais un quota"),
        ("TranscriptActivityProvider.cs","horodatage de MESSAGE de transcript, jamais un quota"),
    };

    /// <summary>
    /// Le chemin doit être présent ET valide. Aucune mise en sourdine possible : une garde qui se
    /// désarme toute seule quand son chemin est absent ne garde rien.
    /// </summary>
    [Fact]
    public void Le_chemin_des_sources_est_injecte_et_existe()
    {
        var chemin = CheminSources();

        Assert.False(string.IsNullOrWhiteSpace(chemin),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : le .csproj de tests doit "
            + "l'injecter, sinon la garde HDR-05 ne balaie rien.");
        Assert.True(Directory.Exists(chemin), $"Chemin des sources injecté mais introuvable : {chemin}");
    }

    /// <summary>
    /// LE CŒUR DE LA GARDE. Tout fichier de Services/ ou Models/ hors exemptions qui porte un motif
    /// de conversion d'unité d'usage fait échouer ce test, en nommant le fichier, la ligne et la
    /// raison.
    /// </summary>
    [Fact]
    public void Aucune_conversion_d_unite_ne_subsiste_hors_du_point_unique()
    {
        var racine = CheminSources();
        var exemptes = Exemptions.Select(e => e.Fichier).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fichiers = new[] { "Services", "Models" }
            .Select(d => Path.Combine(racine, d))
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs"))   // dossiers plats, non récursif
            .ToList();

        // Un chemin valide mais pointant sur un dossier vide rendrait la garde muette.
        Assert.True(fichiers.Count >= 40,
            $"Seulement {fichiers.Count} fichiers balayés sous {racine} : la garde ne voit manifestement "
            + "pas la vraie arborescence des sources.");

        var infractions = new List<string>();

        foreach (var fichier in fichiers)
        {
            var nom = Path.GetFileName(fichier);
            if (exemptes.Contains(nom)) continue;

            var texte = File.ReadAllText(fichier);

            foreach (var (motif, pourquoi) in Interdits)
                foreach (Match m in Regex.Matches(texte, motif))
                {
                    var ligne = texte.Take(m.Index).Count(c => c == '\n') + 1;
                    infractions.Add($"{nom}:{ligne} — {motif} ({pourquoi})");
                }
        }

        Assert.True(infractions.Count == 0,
            "HDR-05 : des conversions d'unité d'usage vivent hors de UsageNormalization.\n"
            + "Soit les déléguer au point unique, soit — si la valeur concernée n'est PAS un quota — "
            + "ajouter le fichier aux exemptions NOMINATIVES de ce test, avec sa raison.\n  "
            + string.Join("\n  ", infractions));
    }

    /// <summary>
    /// Falsifiabilité structurelle, sans muter le dépôt : la garde ci-dessus n'a de sens que si ses
    /// motifs décrivent des conversions RÉELLES. Le point unique doit donc en porter au moins un —
    /// sinon la garde n'interdirait rien d'existant et l'exemption n° 1 serait inutile.
    /// </summary>
    [Fact]
    public void La_garde_voit_bien_le_point_unique()
    {
        var texte = File.ReadAllText(Path.Combine(CheminSources(), "Services", "UsageNormalization.cs"));

        var vus = Interdits.Where(i => Regex.IsMatch(texte, i.Motif)).Select(i => i.Motif).ToList();

        Assert.NotEmpty(vus);
    }

    /// <summary>Chemin des sources tel qu'injecté par MSBuild (jamais deviné depuis la sortie de build).</summary>
    private static string CheminSources()
        => typeof(NormalisationUniqueTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminSourcesChronos")?.Value
           ?? "";
}
