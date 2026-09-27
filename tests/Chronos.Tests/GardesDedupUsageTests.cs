using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// GARDE DE NON-RETOUR de la phase 32 (CPT-01) : « aucun lecteur de <c>message.usage</c> hors du helper ».
///
/// Depuis Claude Code 2.1.260, un message assistant est écrit sur PLUSIEURS lignes du transcript (une par
/// bloc de contenu), même <c>message.id</c>, avec un <c>output_tokens</c> partiel croissant. Tout code qui
/// lirait <c>usage</c> ligne par ligne recompterait ×2,1 (mesuré le 2026-09-27 : 128 545 lignes pour 60 349
/// ids). Le dépôt a vécu ce bug en production (correction par delta fausse en v1.5 → v1.7) ; la phase 33
/// (agrégats, curseurs) ajoutera de nouveaux lecteurs de transcripts. Cette garde les oblige à passer par
/// <see cref="DedupUsage"/>, seul endroit où les cinq littéraux JSON de <c>usage</c> ont le droit d'exister.
///
/// Motif recopié de <c>GardesDoctrineTests</c> : chemin des sources INJECTÉ par MSBuild (jamais deviné),
/// balayage textuel avec anti-mutisme (≥ 40 fichiers, le helper est vu et contient bien les littéraux),
/// complété d'une garde RÉFLEXIVE (la méthode <c>SumUsageTokens</c> n'existe plus). Balayage RÉCURSIF :
/// <c>Services/Historique/</c> (32-04) et les sous-dossiers de la phase 33 doivent être vus.
///
/// Ces tests ne lisent que des fichiers .cs du dépôt : aucun réseau, aucun %APPDATA%, aucun jeton.
/// </summary>
public sealed class GardesDedupUsageTests
{
    private const string NomHelper = "DedupUsage.cs";

    // Les cinq littéraux JSON de message.usage. Toute occurrence hors du helper est une infraction.
    private static readonly Regex MotifUsage = new(
        "\"(usage|input_tokens|output_tokens|cache_creation_input_tokens|cache_read_input_tokens)\"",
        RegexOptions.Compiled);

    private static readonly string[] Litteraux =
    {
        "\"usage\"", "\"input_tokens\"", "\"output_tokens\"",
        "\"cache_creation_input_tokens\"", "\"cache_read_input_tokens\"",
    };

    // Services/** et Models/** — RÉCURSIF (Pitfall 4 de la recherche 32 : une garde plate ne voit pas
    // les sous-dossiers créés par les plans suivants).
    private static List<string> FichiersServicesEtModels(string racine)
        => new[] { "Services", "Models" }
            .Select(d => Path.Combine(racine, d))
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .ToList();

    // --- 1. Textuelle : seul le helper lit message.usage ---

    [Fact]
    public void Aucun_fichier_de_Services_ou_Models_ne_lit_message_usage_hors_du_helper()
    {
        var racine = GardesPerimetreTests.CheminSources();
        var fichiers = FichiersServicesEtModels(racine);

        // Un chemin valide pointant sur un dossier vide rendrait la garde muette.
        Assert.True(fichiers.Count >= 40,
            $"Seulement {fichiers.Count} fichiers balayés sous {racine} : la garde ne voit manifestement "
            + "pas la vraie arborescence des sources.");

        // Anti-mutisme : le helper est vu, et il porte bien les cinq littéraux — sinon le motif ne
        // prouverait rien (il pourrait être faux sans qu'aucun fichier ne le déclenche).
        Assert.Contains(fichiers, f => Path.GetFileName(f) == NomHelper);
        var texteHelper = File.ReadAllText(fichiers.Single(f => Path.GetFileName(f) == NomHelper));
        foreach (var litteral in Litteraux)
            Assert.Contains(litteral, texteHelper, StringComparison.Ordinal);

        var infractions = new List<string>();

        foreach (var fichier in fichiers.Where(f => Path.GetFileName(f) != NomHelper))
        {
            var texte = File.ReadAllText(fichier);

            foreach (Match m in MotifUsage.Matches(texte))
            {
                var ligne = texte.Take(m.Index).Count(c => c == '\n') + 1;
                infractions.Add($"{Path.GetRelativePath(racine, fichier)}:{ligne} — {m.Value}");
            }
        }

        Assert.True(infractions.Count == 0,
            "CPT-01 : seul Services/DedupUsage.cs lit message.usage (dédup par message.id, max par champ). "
            + "Un message assistant est écrit sur plusieurs lignes (une par bloc de contenu) avec un "
            + "output_tokens partiel : lire usage ligne par ligne recompte ×2,1. "
            + "Passer par DedupUsage.LireUsage + Ajouter. Infractions :\n  "
            + string.Join("\n  ", infractions));
    }

    // --- 2. Réflexive + textuelle : la passe disque ne somme plus ligne par ligne, et la dédup est globale ---

    [Fact]
    public void TranscriptActivityProvider_ne_somme_plus_ligne_par_ligne()
    {
        // Réflexif : l'ancienne méthode de somme ligne par ligne n'existe plus, sous aucune visibilité.
        var somme = typeof(TranscriptActivityProvider).GetMethod("SumUsageTokens",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        Assert.True(somme is null,
            "TranscriptActivityProvider.SumUsageTokens est réapparue : la somme des usages passe par DedupUsage.");

        // Textuel : UN seul « new DedupUsage() », posé AVANT la boucle sur les fichiers (D-32-03). 491 ids sur
        // 8 jours vivent dans 2 à 3 fichiers (reprise / fork de session) : une dédup par fichier — dictionnaire
        // créé dans le foreach — les compterait encore plusieurs fois, et aucun test à une fixture ne le verrait.
        var fichier = Path.Combine(GardesPerimetreTests.CheminSources(), "Services", "TranscriptActivityProvider.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var lignes = File.ReadAllLines(fichier);
        var indexNew = lignes
            .Select((texte, index) => (texte, index))
            .Where(x => x.texte.Contains("new DedupUsage()", StringComparison.Ordinal))
            .Select(x => x.index)
            .ToList();
        var indexForeach = Array.FindIndex(lignes, l => l.Contains("foreach (var file", StringComparison.Ordinal));

        Assert.True(indexNew.Count == 1,
            $"Attendu exactement un « new DedupUsage() » dans TranscriptActivityProvider.cs, vu {indexNew.Count}.");
        Assert.True(indexForeach >= 0, "La boucle « foreach (var file » de ReadAsync est introuvable.");
        Assert.True(indexNew[0] < indexForeach,
            $"D-32-03 : « new DedupUsage() » (ligne {indexNew[0] + 1}) doit précéder « foreach (var file » "
            + $"(ligne {indexForeach + 1}) — la dédup est GLOBALE à la passe, pas par fichier.");
    }

    // --- 3. Le helper existe, avec la forme que la phase 33 attend ---

    [Fact]
    public void Le_helper_de_dedup_existe_et_expose_le_max_par_champ()
    {
        var type = typeof(DedupUsage);
        Assert.True(type.IsPublic, "DedupUsage doit être public : c'est l'entrée unique des futurs lecteurs.");
        Assert.True(type.IsSealed, "DedupUsage doit être sealed : pas de dérivation qui contournerait la règle.");

        var ajouter = type.GetMethod("Ajouter", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(ajouter);
        Assert.Equal(7, ajouter!.GetParameters().Length);   // messageId, requestId, ts, in, out, cacheW, cacheR

        var lire = type.GetMethod("LireUsage", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(lire);

        Assert.NotNull(type.GetMethod("Entrees", BindingFlags.Public | BindingFlags.Instance));

        // Le max par champ est la règle mesurée (12 835 / 12 835 ids divergents : dernière ligne = max) :
        // quatre champs, donc au moins quatre Math.Max dans le texte du helper.
        var texte = File.ReadAllText(Path.Combine(GardesPerimetreTests.CheminSources(), "Services", NomHelper));
        var occurrences = Regex.Matches(texte, @"Math\.Max\(").Count;
        Assert.True(occurrences >= 4,
            $"DedupUsage.cs ne contient que {occurrences} Math.Max( : la règle « max par champ » a été affaiblie.");
    }
}
