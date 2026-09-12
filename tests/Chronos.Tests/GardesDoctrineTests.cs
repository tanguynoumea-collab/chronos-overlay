using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// DEUX GARDES DE NON-RETOUR de la phase 19, plus une preuve de comportement.
///
/// Le chemin des sources est INJECTÉ par MSBuild (attribut <c>AssemblyMetadata("CheminSourcesChronos")</c>
/// posé dans le .csproj de tests) et jamais deviné : CLAUDE.md proscrit de localiser un assembly par son
/// chemin de fichier — cette propriété est VIDE en publication mono-fichier — et une remontée de dossiers
/// depuis <c>AppContext.BaseDirectory</c> casserait en silence au premier changement d'agencement de
/// sortie. Motif recopié de <c>NormalisationUniqueTests</c>.
///
/// Ces tests ne lisent que des fichiers .cs du dépôt : aucun réseau, aucun %APPDATA%, aucun jeton.
/// </summary>
public class GardesDoctrineTests
{
    /// <summary>
    /// GARDE 1 — le piège de la recomposition cesse d'être silencieux.
    ///
    /// <c>CompositeUsageProvider.GetAsync</c> reconstruit <see cref="UsageSnapshot"/> par
    /// <c>new UsageSnapshot { … }</c> et non par <c>with</c>. Toute propriété non nommée dans ce « new »
    /// est donc DÉTRUITE à chaque passage de la chaîne — et la chaîne réelle en compte trois imbriqués.
    /// Le <c>new</c> est le bon choix (un <c>with</c> ferait hériter silencieusement du primaire tout
    /// champ futur, c'est-à-dire produire une valeur FAUSSE là où le <c>new</c> produit un <c>null</c>) ;
    /// ce qui manquait était une garde. La voici.
    ///
    /// Seules les propriétés d'INSTANCE sont exigées : <c>Empty</c> est une fabrique statique, pas un
    /// champ d'état, et n'a rien à faire dans une recomposition.
    /// </summary>
    [Fact]
    public void Toute_propriete_de_UsageSnapshot_est_nommee_dans_la_recomposition_du_composite()
    {
        var fichier = Path.Combine(CheminSources(), "Services", "CompositeUsageProvider.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        var proprietes = typeof(UsageSnapshot)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        // Un record dont on ne verrait aucune propriété rendrait la garde muette.
        Assert.True(proprietes.Count >= 3, $"Seulement {proprietes.Count} propriétés vues sur UsageSnapshot.");

        var manquantes = proprietes.Where(n => !texte.Contains(n, StringComparison.Ordinal)).ToList();

        Assert.True(manquantes.Count == 0,
            "CompositeUsageProvider.GetAsync reconstruit UsageSnapshot par « new » : toute propriété qui "
            + "n'y est pas nommée est détruite EN SILENCE à chaque passage de la chaîne (trois composites "
            + "imbriqués en production). Nommer ces propriétés dans le « new », ou — si le champ est posé "
            + "AU-DESSUS du composite par la couche de doctrine — le dire en commentaire dans ce fichier.\n  "
            + string.Join("\n  ", manquantes));
    }

    /// <summary>
    /// GARDE 2 (EXA-04) — aucun rapport entre un comptage de tokens et un plafond ne peut réapparaître.
    ///
    /// La phase 16 a déjà interdit aux transcripts d'implémenter <c>IUsageProvider</c> ; cette garde ferme
    /// l'AUTRE voie, celle d'un taux points-par-token — qui n'est qu'un plafond auto-calibré, c'est-à-dire
    /// le calibrateur de plafonds supprimé en phase 16. Trois faits la rendent nécessaire : les limites
    /// Anthropic pondèrent par modèle ; les transcripts ignorent l'app de bureau et Cowork, qui consomment
    /// le même pool ; et la mesure du 2026-09-12 donne 643 649 933 tokens sur 5 h là où l'ancien plafond
    /// valait 230 000 000, soit 280 %.
    ///
    /// CONSIGNE DE RÉDACTION, impérative, pour tout fichier de Services/ et Models/ — y compris les
    /// commentaires : formuler toujours « un rapport entre un comptage de tokens et un plafond », et
    /// JAMAIS les deux termes accolés par un opérateur, sinon un simple commentaire déclencherait le motif.
    /// </summary>
    [Fact]
    public void Aucun_rapport_entre_un_comptage_de_tokens_et_un_plafond_dans_Services_et_Models()
    {
        const string motif =
            @"(Tokens?\w*\s*[/*]\s*\w*(Plafond|Budget|Limite|Capacite))|((Plafond|Budget)\w*\s*[/*]\s*\w*Tokens?)";

        var racine = CheminSources();

        var fichiers = new[] { "Services", "Models" }
            .Select(d => Path.Combine(racine, d))
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs"))   // dossiers plats, non récursif
            .ToList();

        // Un chemin valide pointant sur un dossier vide rendrait la garde muette.
        Assert.True(fichiers.Count >= 40,
            $"Seulement {fichiers.Count} fichiers balayés sous {racine} : la garde ne voit manifestement "
            + "pas la vraie arborescence des sources.");

        var infractions = new List<string>();

        foreach (var fichier in fichiers)
        {
            var texte = File.ReadAllText(fichier);

            foreach (Match m in Regex.Matches(texte, motif, RegexOptions.IgnoreCase))
            {
                var ligne = texte.Take(m.Index).Count(c => c == '\n') + 1;
                infractions.Add($"{Path.GetFileName(fichier)}:{ligne} — « {m.Value.Trim()} »");
            }
        }

        Assert.True(infractions.Count == 0,
            "EXA-04 : un rapport entre un comptage de tokens et un plafond est réapparu. Il n'a AUCUNE "
            + "réponse honnête : les limites pondèrent par modèle, les transcripts ne voient ni l'app de "
            + "bureau ni Cowork, et la mesure terrain le falsifie d'un facteur supérieur à 2,8. Le delta "
            + "change la NATURE du chiffre (« au moins X »), il ne s'y ajoute jamais.\n  "
            + string.Join("\n  ", infractions));
    }

    /// <summary>
    /// PREUVE DE COMPORTEMENT, dans l'esprit de la garde de position de la phase 18.
    ///
    /// L'équivalence « porte d'âge au-dessus du composite ≡ porte d'âge dans Best() » repose sur un fait
    /// d'ORDRE : la seule source à ancienneté non bornée (l'objet d'usage sur disque) est le repli le plus
    /// interne, donc elle ne peut gagner que lorsque tout ce qui est au-dessus est indisponible. Un
    /// réordonnancement futur de la chaîne casserait cette équivalence EN SILENCE. Ce test transforme la
    /// fragilité en signal : trois sources exactes aux chiffres différents, c'est la plus externe qui sort.
    /// </summary>
    [Fact]
    public async Task Le_repli_le_plus_interne_est_bien_celui_a_anciennete_non_bornee()
    {
        var externe = new FakeUsageProvider { Next = Snap(0.11) };
        var median = new FakeUsageProvider { Next = Snap(0.22) };
        var interne = new FakeUsageProvider { Next = Snap(0.33) };

        var chaine = new CompositeUsageProvider(
            externe,
            new CompositeUsageProvider(median, interne));

        var snap = await chaine.GetAsync();

        Assert.Equal(0.11, snap.FiveHour.Utilization);
        Assert.Equal(0.11, snap.SevenDay.Utilization);
    }

    private static UsageSnapshot Snap(double util) => new()
    {
        FiveHour = new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = util,
        },
        SevenDay = new WindowState
        {
            Kind = WindowKind.SevenDay,
            Reliability = SourceReliability.Exact,
            Utilization = util,
        },
    };

    /// <summary>Chemin des sources tel qu'injecté par MSBuild (jamais deviné depuis la sortie de build).</summary>
    private static string CheminSources()
        => typeof(GardesDoctrineTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminSourcesChronos")?.Value
           ?? "";
}
