using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Chronos.Models.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// GARDE STRUCTURELLE TOK-05 — « aucun pourcentage n'est jamais dérivé de tokens ».
///
/// POURQUOI : EXA-04 a payé un ratio tokens/plafond faux d'un facteur 2,8 (643 649 933 tokens sur 5 h là où l'ancien
/// plafond valait 230 000 000) ; les tokens de Claude Code sont PARTIELS (hors Cowork, hors claude.ai — même pool de
/// forfait) et NON PONDÉRÉS (les limites pondèrent par modèle et par nature). Aucun type de cette couche ne doit
/// pouvoir en faire un pourcentage, et la garde le prouve par RÉFLEXION (aucun flottant dans aucune signature, aucun
/// membre nommé comme un quota) ET par le TEXTE source (aucun flottant ni « / 100 » même en commentaire), avec un
/// CONTRÔLE POSITIF : le <c>double</c> légitime de <c>DeltaConsommation.Delta</c> (relevés, hors Tokens) serait attrapé
/// s'il vivait dans le quartier Tokens — le filtre mord, et son périmètre est le bon (D-33-01 : deux sous-namespaces,
/// une phrase de garde).
///
/// Ces tests ne lisent que l'assembly et des fichiers .cs du dépôt : aucun réseau, aucun %APPDATA%, aucun jeton.
/// </summary>
public sealed class GardeTokensSansPourcentageTests
{
    private static readonly string[] PrefixesTokens = { "Chronos.Models.Historique.Tokens", "Chronos.Services.Historique.Tokens" };

    // Un membre dont le nom parle de quota n'a rien à faire sur des tokens bruts et partiels.
    private static readonly Regex MotifNomInterdit = new("Utilization|Utilisation|Pourcent|Quota|Ratio|Fraction|Pct", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Textuelle : flottants (mot entier), conversions par 100, et le nom même du pourcentage serveur.
    private const string MotifTexteInterdit = @"\b(double|float|decimal)\b|/\s*100\b|\*\s*100\b|Utilization";

    private const BindingFlags Tous = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static bool EstDansTokens(Type t)
        => t.Namespace is { } n && PrefixesTokens.Any(p => n.StartsWith(p, StringComparison.Ordinal));

    /// <summary>Récursif : le type lui-même, l'élément d'un tableau / byref, tout argument générique (Nullable, List, Dictionary, Func…).</summary>
    private static bool EstFlottant(Type t)
    {
        if (t.IsByRef || t.IsArray || t.IsPointer) return EstFlottant(t.GetElementType()!);
        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return true;
        if (t.IsGenericType) return t.GetGenericArguments().Any(EstFlottant);
        return false;
    }

    private static List<Type> TypesTokens()
        => typeof(TrancheTokens).Assembly.GetTypes().Where(EstDansTokens).ToList();

    [Fact]
    public void Aucun_type_du_namespace_Tokens_n_expose_un_flottant_ni_un_membre_de_quota()
    {
        var types = TypesTokens();

        // Anti-mutisme : TrancheTokens, DeltaTranche, EtatCouverture, IntervalleGaranti, LigneAgregat, MagasinAgregats, CouvertureTokens (+ DTO privés).
        Assert.True(types.Count >= 6, $"Seulement {types.Count} types vus sous {string.Join(" / ", PrefixesTokens)} : la garde ne voit pas le quartier Tokens.");

        var infractions = new List<string>();

        foreach (var t in types)
        {
            if (MotifNomInterdit.IsMatch(t.Name)) infractions.Add($"{t.FullName} : nom de type");

            foreach (var p in t.GetProperties(Tous))
            {
                if (EstFlottant(p.PropertyType)) infractions.Add($"{t.Name}.{p.Name} : {p.PropertyType}");
                if (MotifNomInterdit.IsMatch(p.Name)) infractions.Add($"{t.Name}.{p.Name} : nom de propriété");
            }

            foreach (var f in t.GetFields(Tous))
            {
                if (EstFlottant(f.FieldType)) infractions.Add($"{t.Name}.{f.Name} : {f.FieldType}");
                if (MotifNomInterdit.IsMatch(f.Name)) infractions.Add($"{t.Name}.{f.Name} : nom de champ");
            }

            foreach (var m in t.GetMethods(Tous))
            {
                if (m.IsSpecialName) continue;   // accesseurs et opérateurs : leur type est inspecté via la propriété
                if (EstFlottant(m.ReturnType)) infractions.Add($"{t.Name}.{m.Name}() : retour {m.ReturnType}");
                foreach (var prm in m.GetParameters())
                    if (EstFlottant(prm.ParameterType)) infractions.Add($"{t.Name}.{m.Name}({prm.Name}) : {prm.ParameterType}");
                if (MotifNomInterdit.IsMatch(m.Name)) infractions.Add($"{t.Name}.{m.Name}() : nom de méthode");
            }

            foreach (var c in t.GetConstructors(Tous))
                foreach (var prm in c.GetParameters())
                    if (EstFlottant(prm.ParameterType)) infractions.Add($"{t.Name}..ctor({prm.Name}) : {prm.ParameterType}");
        }

        Assert.True(infractions.Count == 0,
            "TOK-05 : un flottant ou un membre de quota vit dans le quartier Tokens. Ces tokens sont partiels (hors Cowork, "
            + "hors claude.ai) et non pondérés : ils ne peuvent fonder AUCUN pourcentage. Entiers seulement.\n  "
            + string.Join("\n  ", infractions));
    }

    /// <summary>
    /// CONTRÔLE POSITIF : le même filtre appliqué au <c>double</c> légitime des relevés (Δ de consommation, hors Tokens)
    /// rend true — le filtre mord — et ce type n'est PAS dans le périmètre — le périmètre est le bon. Sans ce test, une
    /// garde verte pourrait n'être qu'un filtre cassé.
    /// </summary>
    [Fact]
    public void Le_filtre_attraperait_un_double_legitime_s_il_vivait_dans_Tokens()
    {
        var delta = typeof(Chronos.Models.Historique.DeltaConsommation).GetProperty("Delta")!;

        Assert.True(EstFlottant(delta.PropertyType), "Le filtre ne reconnaît plus un double : la garde est aveugle.");
        Assert.False(EstDansTokens(typeof(Chronos.Models.Historique.DeltaConsommation)),
            "DeltaConsommation (relevés) ne doit pas être dans le périmètre Tokens : le préfixe est trop large.");

        // Et les formes composées : Nullable, tableau, générique, byref.
        Assert.True(EstFlottant(typeof(double?)));
        Assert.True(EstFlottant(typeof(float[])));
        Assert.True(EstFlottant(typeof(List<decimal>)));
        Assert.True(EstFlottant(typeof(Dictionary<string, double>)));
        Assert.True(EstFlottant(typeof(double).MakeByRefType()));
        Assert.False(EstFlottant(typeof(long)));
        Assert.False(EstFlottant(typeof(List<long>)));
        Assert.False(EstFlottant(typeof(DateTimeOffset)));

        // Et le nom : un « Ratio » ou une « Utilization » serait nommé.
        Assert.Matches(MotifNomInterdit, "TauxUtilisation");
        Assert.Matches(MotifNomInterdit, "PctSousAgents");
        Assert.DoesNotMatch(MotifNomInterdit, "TranchesEnMemoire");
    }

    [Fact]
    public void Aucun_fichier_de_Tokens_ne_contient_de_flottant_ni_de_pourcentage_meme_en_commentaire()
    {
        var racine = GardesPerimetreTests.CheminSources();

        var fichiers = new[] { Path.Combine("Services", "Historique", "Tokens"), Path.Combine("Models", "Historique", "Tokens") }
            .Select(d => Path.Combine(racine, d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .ToList();

        // Un chemin valide pointant sur un dossier vide rendrait la garde muette.
        Assert.True(fichiers.Count >= 3,
            $"Seulement {fichiers.Count} fichiers balayés sous {racine} : la garde ne voit manifestement pas le quartier Tokens.");

        var infractions = new List<string>();

        foreach (var fichier in fichiers)
        {
            var texte = File.ReadAllText(fichier);
            foreach (Match m in Regex.Matches(texte, MotifTexteInterdit))
            {
                var ligne = texte.Take(m.Index).Count(c => c == '\n') + 1;
                infractions.Add($"{Path.GetFileName(fichier)}:{ligne} — « {m.Value.Trim()} »");
            }
        }

        Assert.True(infractions.Count == 0,
            "TOK-05 (texte) : un flottant, une conversion par 100 ou le mot du pourcentage serveur apparaît dans un fichier "
            + "du quartier Tokens — même en commentaire, c'est la porte ouverte. Reformuler en entiers (une part sous-agents "
            + "est une paire d'entiers, pas un ratio).\n  "
            + string.Join("\n  ", infractions));
    }
}
