using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chronos.Services;

/// <summary>
/// Socle NEUTRE et PUR partagé par tout ce qui édite <c>%USERPROFILE%\.claude\settings.json</c> :
/// prédicat d'identité « cette commande appartient-elle à Chronos ? », analyse tolérante et
/// sérialisation fidèle. Aucune E/S fichier, aucun type WPF — uniquement des chaînes et des
/// <see cref="JsonNode"/>.
///
/// <para><b>(a) Pourquoi le repérage se fait sur le MARQUEUR + le NOM de fichier, jamais sur le CHEMIN.</b>
/// Les installateurs historiques identifiaient une entrée par le chemin de l'exe COURANT. Depuis un exe
/// fraîchement téléchargé, aucune entrée existante ne correspondait, donc le garde d'idempotence ne se
/// déclenchait jamais et un groupe de hooks s'ajoutait à chaque version. Cinq chemins d'exe (v2.5, v2.5.1,
/// v2.6, v2.8.1 et le build Debug) ont ainsi produit <b>25 groupes de hooks au lieu de 5</b>. Le chemin est
/// précisément la variable qu'il faut cesser de suivre : on retient le marqueur d'argument
/// (<c>--hook</c> / <c>--statusline</c>) ET le nom de fichier <c>Chronos*.exe</c>. Le marqueur seul serait
/// imprudent (rien n'empêche un outil tiers d'adopter <c>--hook</c>) ; le nom de fichier borne le rayon
/// d'action sans réintroduire la dépendance au chemin.</para>
///
/// <para><b>(b) Pourquoi <see cref="ParseOrNull"/> renvoie <c>null</c> au lieu d'un objet vide.</b>
/// Le repli « repartir d'un objet JSON vierge » des installateurs actuels est DESTRUCTIF : l'appelant écrit
/// ensuite cet objet par-dessus le fichier, et tout le settings.json de l'utilisateur (permissions, env,
/// model, hooks des autres outils) disparaît silencieusement. Un échec d'analyse doit produire
/// « je n'écris rien », jamais « je repars d'une page blanche ». <c>null</c> = <b>NE RIEN ÉCRIRE</b>.</para>
///
/// <para><b>(c) Limite assumée : les commentaires sont perdus à la réécriture.</b>
/// <see cref="JsonCommentHandling.Skip"/> permet de LIRE un fichier commenté, mais System.Text.Json ne sait
/// pas réémettre les commentaires. La parade est la sauvegarde horodatée du fichier avant écriture
/// (réconciliateur, plan 03), pas un contournement technique.</para>
/// </summary>
public static class ClaudeSettingsJson
{
    /// <summary>Marqueur d'argument des hooks de session Chronos.</summary>
    public const string HookMarker = "--hook";

    /// <summary>Marqueur d'argument du pont statusLine Chronos.</summary>
    public const string StatusLineMarker = "--statusline";

    // Lecture volontairement permissive : un settings.json édité à la main peut contenir des
    // commentaires « // » et une virgule traînante — les deux lèvent avec les options par défaut.
    private static readonly JsonDocumentOptions LectureTolerante = new()
    {
        CommentHandling = JsonCommentHandling.Skip,   // sinon « // » lève JsonReaderException
        AllowTrailingCommas = true,
    };

    // Sortie fidèle : on réécrit le fichier ENTIER, y compris les valeurs des autres outils. L'encodeur
    // par défaut échapperait tous les accents en séquences d'échappement Unicode (« Téléchargements »
    // ressortirait mutilé), ainsi que & < > + — diff illisible et churn gratuit sur des données des autres.
    private static readonly JsonSerializerOptions SortieFidele = new()
    {
        WriteIndented = true,                                    // 2 espaces — style de Claude Code
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // « Téléchargements » reste littéral
    };

    /// <summary>
    /// Premier jeton exécutable d'une chaîne de commande : le segment entre guillemets s'il y en a
    /// (donc sûr sur un chemin contenant des espaces), sinon tout ce qui précède le premier espace.
    /// Renvoie une chaîne vide pour une commande nulle ou vide.
    /// </summary>
    public static string ExtractExecutable(string? command)
    {
        var s = command?.TrimStart() ?? string.Empty;
        if (s.Length == 0) return string.Empty;

        if (s[0] == '"')
        {
            var fin = s.IndexOf('"', 1);
            return fin > 0 ? s[1..fin] : s[1..];
        }

        var esp = s.IndexOf(' ');
        return esp > 0 ? s[..esp] : s;
    }

    /// <summary>
    /// Vrai si la commande porte le <paramref name="marker"/> ET que son exécutable s'appelle
    /// <c>Chronos*.exe</c>. JAMAIS de comparaison de CHEMIN — c'est la cause racine du cumul historique.
    /// Question posée : « est-ce à Chronos ? » (appartenance), pas « est-ce CE Chronos ? ».
    /// </summary>
    public static bool IsChronosCommand(string? command, string marker)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        if (string.IsNullOrEmpty(marker)) return false;
        if (command.IndexOf(marker, StringComparison.OrdinalIgnoreCase) < 0) return false;

        var nom = Path.GetFileName(ExtractExecutable(command).Replace('\\', '/'));
        return nom.StartsWith("Chronos", StringComparison.OrdinalIgnoreCase)
            && nom.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Vrai si la commande référence CE chemin d'exe (backslashes et slashes avant tolérés).
    /// Question posée : « est-ce CE Chronos ? » (fraîcheur), à ne jamais confondre avec l'appartenance.
    /// </summary>
    public static bool PointsToExe(string? command, string exePath)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        if (string.IsNullOrWhiteSpace(exePath)) return false;

        return command.IndexOf(exePath, StringComparison.OrdinalIgnoreCase) >= 0
            || command.IndexOf(exePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Lit le champ <c>command</c> d'un handler sans jamais lever. Le schéma officiel autorise désormais
    /// des handlers <c>http</c> / <c>mcp_tool</c> / <c>prompt</c> / <c>agent</c> DÉPOURVUS de champ
    /// <c>command</c>, et un fichier mal formé peut en porter un non-textuel : d'où <c>TryGetValue</c>,
    /// et jamais l'accès typé direct, qui lèverait une InvalidOperationException sur un nombre.
    /// </summary>
    public static string? CommandOf(JsonNode? handler)
        => handler is JsonObject o && o["command"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>
    /// Vrai si le GROUPE <c>{ "hooks": [ … ] }</c> contient au moins un handler dont la commande est à
    /// Chronos. Tolère les groupes tiers (matcher, timeout, if, handlers sans commande) sans lever.
    /// </summary>
    public static bool IsChronosGroup(JsonNode? group, string marker)
        => group is JsonObject g && g["hooks"] is JsonArray hs
           && hs.Any(h => IsChronosCommand(CommandOf(h), marker));

    /// <summary>
    /// Analyse tolérante (commentaires et virgule traînante acceptés). Renvoie un objet vide quand
    /// <paramref name="json"/> est nul ou vide (fichier absent = départ légitime), et <c>null</c> quand le
    /// contenu est INEXPLOITABLE (racine non-objet, clés dupliquées, JSON invalide) : l'appelant NE DOIT
    /// alors RIEN ÉCRIRE. On ne substitue jamais une page blanche au fichier de l'utilisateur.
    /// </summary>
    public static JsonObject? ParseOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try
        {
            if (JsonNode.Parse(json, nodeOptions: null, LectureTolerante) is not JsonObject o) return null;
            _ = o.Count;   // force la matérialisation : lève ArgumentException si clés dupliquées
            return o;
        }
        catch { return null; }   // catch LARGE : ArgumentException n'est PAS une JsonException
    }

    /// <summary>
    /// Sérialisation fidèle : indentation 2 espaces et accents littéraux (pas de \uXXXX). Préserve
    /// l'ordre des clés, les clés inconnues et le texte brut des nombres tels que lus.
    /// </summary>
    public static string Serialize(JsonObject root) => root.ToJsonString(SortieFidele);
}
