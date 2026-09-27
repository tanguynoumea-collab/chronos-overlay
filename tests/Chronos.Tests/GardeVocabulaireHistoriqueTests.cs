using System.IO;
using System.Text.RegularExpressions;
using Chronos.Models.Historique;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-06 — garde de VOCABULAIRE de la fenêtre Historique (34-08, D-34-36) : la fenêtre montre ce qui a été relevé, jamais ce qui
/// pourrait l'être. Aucun mot de projection (le motif <see cref="Projection"/> : ce qui annonce un épuisement, un rythme prolongé,
/// une prévision, une estimation, une tendance ou une échéance « dans N h ») ne peut entrer dans TOUT le code de la fenêtre — vues, pistes, convertisseurs, ViewModel, textes,
/// scénarios, modèles, géométrie — sans rougir ici ; et les mots du plan (§4) doivent, eux, être présents dans la source unique des
/// mots (<c>TextesHistorique</c>).
///
/// Périmètre : tout <c>.cs</c> / <c>.xaml</c> de <c>src/Chronos</c> dont le chemin ou le nom contient « Historique », HORS
/// <c>Services/Historique/**</c> (journal, agrégats, analyse : couverts par leurs propres gardes de 32 / 33) et hors <c>obj</c> /
/// <c>bin</c>. Anti-mutisme : au moins 12 fichiers, et les fichiers-clés nommément (une garde qui ne lit rien passerait toujours).
/// Exclusion documentée : une ligne portant le marqueur <c>garde-vocabulaire: ignorer</c> n'est pas lue (aucune n'existe en 34-08).
/// </summary>
public class GardeVocabulaireHistoriqueTests
{
    /// <summary>Les mots de projection (Pattern 9 de la recherche) ; « estim » couvre aussi « estimation » / « estimé » (§4 : relevé,
    /// jamais estimation).</summary>
    private static readonly Regex Projection = new(@"épuisé[e]? vers|à ce rythme|projection|prévision|estim(é|ation|er)|tendance|dans \d+ ?h\b",
                                                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const string MarqueurExclusion = "garde-vocabulaire: ignorer";

    private static string Racine()
    {
        var racine = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine), "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        return racine;
    }

    /// <summary>Les fichiers de la fenêtre Historique vus par la garde (chemins relatifs à <c>src/Chronos</c>).</summary>
    private static List<string> FichiersDeLaFenetre(string racine)
    {
        var separateur = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(racine, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(racine, f))
            .Where(r => !r.StartsWith("obj" + separateur, StringComparison.OrdinalIgnoreCase) && !r.StartsWith("bin" + separateur, StringComparison.OrdinalIgnoreCase))
            .Where(r => r.Contains("Historique", StringComparison.Ordinal))
            .Where(r => !r.StartsWith(Path.Combine("Services", "Historique") + separateur, StringComparison.Ordinal))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void Aucune_projection_dans_le_code_de_la_fenetre_historique()
    {
        var racine = Racine();
        var fichiers = FichiersDeLaFenetre(racine);
        Assert.True(fichiers.Count >= 12, "la garde ne voit pas la fenêtre Historique : " + fichiers.Count + " fichier(s)");

        var infractions = new List<string>();
        foreach (var relatif in fichiers)
        {
            var lignes = File.ReadAllLines(Path.Combine(racine, relatif));
            for (var i = 0; i < lignes.Length; i++)
            {
                if (lignes[i].Contains(MarqueurExclusion, StringComparison.Ordinal)) continue;
                var m = Projection.Match(lignes[i]);
                if (m.Success) infractions.Add($"{relatif}:{i + 1}: « {m.Value} » — {lignes[i].Trim()}");
            }
        }

        Assert.True(infractions.Count == 0,
            "HIS-06 : un mot de projection est entré dans la fenêtre Historique (DESIGN_PLAN §4 : la fenêtre montre ce qui a été RELEVÉ, "
            + "jamais ce qui pourrait l'être) :\n  " + string.Join("\n  ", infractions));
    }

    [Fact]
    public void Les_mots_du_plan_sont_presents_dans_les_textes()
    {
        var chemin = Path.Combine(Racine(), "Text", "TextesHistorique.cs");
        Assert.True(File.Exists(chemin), "TextesHistorique.cs introuvable : " + chemin);
        var texte = File.ReadAllText(chemin);

        foreach (var mot in new[] { "Semaine de forfait", "reset hebdo", "répartition inconnue", "consommé ailleurs", "journal ouvert le", "épuisée", "relevé" })
            Assert.True(texte.Contains(mot, StringComparison.Ordinal), $"TextesHistorique.cs ne porte plus le mot du plan « {mot} » (§4)");

        // « Chronos arrêté » n'est pas recopié : TextesHistorique délègue à CauseTrouTexte (un seul producteur des causes).
        Assert.Contains("CauseTrouTexte.Libelle", texte, StringComparison.Ordinal);
        Assert.Equal("Chronos arrêté", CauseTrouTexte.Libelle(CauseTrou.ChronosArrete));
        Assert.Equal("jeton invalide", CauseTrouTexte.Libelle(CauseTrou.JetonInvalide));

        // « relevé, jamais « mesure » » : aucune chaîne de TextesHistorique ne contient le mot (les commentaires de doctrine le citent).
        var chaines = Regex.Matches(texte, "\"(?:[^\"\\\\]|\\\\.)*\"").Select(m => m.Value).ToList();
        Assert.True(chaines.Count >= 30, "la garde ne lit pas les chaînes de TextesHistorique : " + chaines.Count);
        Assert.DoesNotContain(chaines, c => c.Contains("mesure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void La_garde_voit_les_vues_les_pistes_le_vm_les_textes_et_les_scenarios()
    {
        var vus = FichiersDeLaFenetre(Racine()).Select(Path.GetFileName).ToList();
        foreach (var attendu in new[] { "HistoriqueWindow.xaml", "VueSemaineView.xaml", "VueJourView.xaml", "PisteNiveau.cs", "SurcoucheReticule.cs",
                                        "HistoriqueViewModel.cs", "TextesHistorique.cs", "ScenariosHistorique.cs", "HistoriqueConverters.cs" })
            Assert.Contains(attendu, vus);
        Assert.DoesNotContain("AnalyseReleves.cs", vus);   // Services/Historique : hors périmètre (gardes de 32 / 33)
    }
}
