using System.IO;
using System.Reflection;
using Chronos.Services;
using Chronos.Text;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// GARDES DE NON-RETOUR des reliquats (phase 42.3, point d'audit 4 « pas de reliquat », DS-MAINT-03 / DS-MAINT-04).
///
/// <list type="bullet">
///   <item>PercentFormatter ne sait plus produire « ~ » : la surcharge à booléen « estimé » a disparu, le seul préfixe
///     possible est « ≥ » (plancher), décidé par la provenance.</item>
///   <item>SettingsService.Save n'est plus une API publique de production : la production écrit uniquement par
///     Modifier (fusion sous verrou) ; Save reste interne, réservé à l'amorçage des tests.</item>
///   <item>Plus aucun commentaire ne décrit « trois » ou « deux » composites : la chaîne de production n'en a qu'UN.</item>
///   <item>L'hypothèse « encore valide » (Claude Code supposé seul consommateur) est écrite dans AUDIT_POINTS.md et
///     dans docs/data-sources.md §4 (décision utilisateur du 2026-10-04 : affichage inchangé, hypothèse documentée).</item>
/// </list>
///
/// ANTI-MUET : chaque garde de source vérifie d'abord qu'elle a réellement lu l'arbre des sources.
/// </summary>
public class GardesReliquatsTests
{
    private static string CheminSources()
        => typeof(GardesReliquatsTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminSourcesChronos")?.Value
           ?? "";

    private static string CheminDocs()
        => typeof(GardesReliquatsTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminDocsChronos")?.Value
           ?? "";

    /// <summary>Tous les .cs de src/Chronos hors obj/ et bin/. ANTI-MUET : au moins 50 fichiers lus.</summary>
    private static List<string> FichiersSources()
    {
        var racine = CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        var sep = Path.DirectorySeparatorChar;
        var fichiers = Directory.GetFiles(racine, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .ToList();
        Assert.True(fichiers.Count >= 50, $"Seulement {fichiers.Count} fichiers lus sous {racine} : la garde ne lit pas le vrai arbre.");
        return fichiers;
    }

    [Fact]
    public void PercentFormatter_ne_sait_plus_produire_de_tilde()
    {
        var surchargesBool = typeof(PercentFormatter).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "Format" && m.GetParameters().Any(p => p.ParameterType == typeof(bool)))
            .ToList();
        Assert.Empty(surchargesBool);

        var fichier = Path.Combine(CheminSources(), "Text", "PercentFormatter.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");
        var texte = File.ReadAllText(fichier);
        Assert.Contains("PlancherAvecActivite", texte, StringComparison.Ordinal); // anti-muet : le vrai fichier
        Assert.DoesNotContain("\"~\"", texte, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsService_Save_n_est_pas_public()
    {
        Assert.Null(typeof(SettingsService).GetMethod("Save", BindingFlags.Public | BindingFlags.Instance));
        // Anti-muet : la méthode existe toujours (réservée aux tests), on ne garde pas un nom inexistant.
        Assert.NotNull(typeof(SettingsService).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance));
    }

    [Fact]
    public void Aucun_appel_de_production_a_SettingsService_Save()
    {
        var appels = new[] { "_settings.Save(", "_settingsService.Save(", "settings.Save(" };
        var fautifs = FichiersSources()
            .Where(f => !Path.GetFileName(f).Equals("SettingsService.cs", StringComparison.Ordinal))
            .Where(f =>
            {
                var t = File.ReadAllText(f);
                return appels.Any(a => t.Contains(a, StringComparison.Ordinal));
            })
            .ToList();
        Assert.True(fautifs.Count == 0, "Appel de production à SettingsService.Save : " + string.Join(", ", fautifs));
    }

    [Fact]
    public void Aucun_commentaire_ne_decrit_plusieurs_composites()
    {
        var motifs = new[] { "trois composites", "deux composites", "trois en production" };
        var fautifs = FichiersSources()
            .Where(f =>
            {
                // Les commentaires XML coupent les phrases en fin de ligne : on aplatit « ///» et les sauts de ligne.
                var t = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(f), @"\s*\r?\n\s*(///|//)?\s*", " ");
                return motifs.Any(m => t.Contains(m, StringComparison.OrdinalIgnoreCase));
            })
            .ToList();
        Assert.True(fautifs.Count == 0, "Commentaire périmé (un seul composite en production) : " + string.Join(", ", fautifs));
    }

}
