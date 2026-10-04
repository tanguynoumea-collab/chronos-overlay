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
///   <item>Les limites DS3-01 (reconnexion avec un autre compte) et DS3-02 (transcripts hors de ~/.claude/projects,
///     CLAUDE_CONFIG_DIR non lu, racine inaccessible → « Indisponible ») sont écrites au §4 de docs/data-sources.md (D-02).</item>
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

    /// <summary>
    /// DS-ARCH-02 — décision utilisateur du 2026-10-04 : « encore valide » reste affiché comme exact, sans signe ; l'hypothèse
    /// qui le fonde (Claude Code seul consommateur pendant une panne des sources vivantes) est ÉCRITE, aux deux endroits.
    /// </summary>
    [Fact]
    public void L_hypothese_encore_valide_est_ecrite()
    {
        var docs = CheminDocs();
        Assert.False(string.IsNullOrWhiteSpace(docs), "L'attribut AssemblyMetadata(\"CheminDocsChronos\") manque.");

        var audit = Path.GetFullPath(Path.Combine(docs, "..", "AUDIT_POINTS.md"));
        Assert.True(File.Exists(audit), $"AUDIT_POINTS.md introuvable : {audit}");
        var texteAudit = File.ReadAllText(audit);
        Assert.Contains("seul consommateur", texteAudit, StringComparison.Ordinal);
        Assert.Contains("Cowork", texteAudit, StringComparison.Ordinal);

        var sources = Path.Combine(docs, "data-sources.md");
        Assert.True(File.Exists(sources), $"data-sources.md introuvable : {sources}");
        var texte = File.ReadAllText(sources);
        var debut4 = texte.IndexOf("\n## 4.", StringComparison.Ordinal);
        var debut5 = texte.IndexOf("\n## 5.", StringComparison.Ordinal);
        Assert.True(debut4 >= 0 && debut5 > debut4, "Sections « ## 4. » / « ## 5. » introuvables dans data-sources.md");
        var section4 = texte[debut4..debut5];
        Assert.Contains("seul consommateur", section4, StringComparison.Ordinal);
        Assert.Contains("Cowork", section4, StringComparison.Ordinal);
        Assert.Contains("hypothèse : voir §4", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// DS2-03 / D-03 — décision utilisateur du 2026-10-04 (« Les effacer ») : la déconnexion efface le dernier relevé exact,
    /// et c'est ÉCRIT en §4 de data-sources.md, à côté de l'hypothèse « encore valide » qu'elle borne.
    /// </summary>
    [Fact]
    public void La_deconnexion_qui_efface_le_dernier_releve_est_ecrite()
    {
        var docs = CheminDocs();
        Assert.False(string.IsNullOrWhiteSpace(docs), "L'attribut AssemblyMetadata(\"CheminDocsChronos\") manque.");
        var sources = Path.Combine(docs, "data-sources.md");
        Assert.True(File.Exists(sources), $"data-sources.md introuvable : {sources}");
        var texte = File.ReadAllText(sources);
        var debut4 = texte.IndexOf("\n## 4.", StringComparison.Ordinal);
        var debut5 = texte.IndexOf("\n## 5.", StringComparison.Ordinal);
        Assert.True(debut4 >= 0 && debut5 > debut4, "Sections « ## 4. » / « ## 5. » introuvables dans data-sources.md");
        Assert.Contains("La déconnexion efface le dernier relevé exact", texte[debut4..debut5], StringComparison.Ordinal);
    }

    /// <summary>
    /// DS3-01 / DS3-02 — phase 43, décision D-02 : les deux limites restantes de la 3.5 sont ÉCRITES au §4 de data-sources.md
    /// (reconnexion avec un autre compte : ancien relevé « exact » au plus ~5 min ; transcripts hors de ~/.claude/projects :
    /// CLAUDE_CONFIG_DIR non suivi, racine absente → activité invisible, racine inaccessible → « Indisponible »).
    /// </summary>
    [Fact]
    public void Les_limites_DS3_01_et_DS3_02_sont_ecrites()
    {
        var docs = CheminDocs();
        Assert.False(string.IsNullOrWhiteSpace(docs), "L'attribut AssemblyMetadata(\"CheminDocsChronos\") manque.");
        var sources = Path.Combine(docs, "data-sources.md");
        Assert.True(File.Exists(sources), $"data-sources.md introuvable : {sources}");
        var texte = File.ReadAllText(sources);
        var debut4 = texte.IndexOf("\n## 4.", StringComparison.Ordinal);
        var debut5 = texte.IndexOf("\n## 5.", StringComparison.Ordinal);
        Assert.True(debut4 >= 0 && debut5 > debut4, "Sections « ## 4. » / « ## 5. » introuvables dans data-sources.md");
        var section4 = texte[debut4..debut5];

        Assert.Contains("**Reconnexion avec un autre compte.**", section4, StringComparison.Ordinal);
        Assert.Contains("5 min", section4, StringComparison.Ordinal);
        Assert.Contains("**Transcripts hors de `~/.claude/projects`.**", section4, StringComparison.Ordinal);
        Assert.Contains("CLAUDE_CONFIG_DIR", section4, StringComparison.Ordinal);
        Assert.Contains("inaccessible", section4, StringComparison.Ordinal);
        Assert.Contains("Indisponible", section4, StringComparison.Ordinal);
    }

    /// <summary>
    /// DS2-03 / D-03 — l'oubli vise la MÊME instance de magasin que la tête : un second LastExactStore n'aurait pas
    /// l'instant d'oubli et laisserait la tête réécrire le relevé de l'ancien compte. Et la commande du menu l'appelle.
    /// </summary>
    [Fact]
    public void L_oubli_du_dernier_releve_est_cable_sur_la_meme_instance_que_la_tete()
    {
        var racine = CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine), "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque.");
        var app = File.ReadAllText(Path.Combine(racine, "App.xaml.cs"));
        Assert.Contains("AddSingleton<IOubliDernierReleve>(sp => sp.GetRequiredService<LastExactStore>())", app, StringComparison.Ordinal);
        var vm = File.ReadAllText(Path.Combine(racine, "ViewModels", "MainViewModel.cs"));
        Assert.Contains("IOubliDernierReleve? oubliReleve = null", vm, StringComparison.Ordinal);
        Assert.Contains("_oubliReleve?.OublierDernierReleve(_clock.UtcNow)", vm, StringComparison.Ordinal);
    }
}
