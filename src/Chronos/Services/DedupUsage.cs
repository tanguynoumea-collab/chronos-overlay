using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// SEUL lecteur autorisé de <c>message.usage</c> (CPT-01, garde <c>GardesDedupUsageTests</c>).
///
/// POURQUOI le max et non la première ligne : depuis Claude Code 2.1.260, chaque bloc de contenu d'un
/// message assistant (thinking, text, tool_use…) est réécrit sur sa propre ligne du transcript, avec le
/// même <c>message.id</c> et un <c>usage</c> dont <c>output_tokens</c> est PARTIEL et croissant
/// (8 → 8 → 256) ; la dernière ligne porte le total (mesuré 12 835 / 12 835 ids divergents le
/// 2026-09-27, les trois autres champs identiques). Sommer toutes les lignes compte ×2,1 ; prendre la
/// première sous-compte la sortie. Le max par champ vaut « dernière ligne » sans dépendre de l'ordre de
/// lecture (D-32-01) — robuste au tri global des fichiers.
///
/// Le timestamp retenu est le PREMIER vu (D-32-02) : le message est facturé une fois, à l'instant de la
/// réponse ; la borne basse exclusive de <see cref="TranscriptActivityLog.Since"/> reste juste.
///
/// Repli <c>requestId</c> (présent sur 3 247 / 3 248 lignes) : un filet, pas un chemin nominal. Sans
/// aucun identifiant, la ligne est comptée telle quelle : on ne jette JAMAIS un token connu.
///
/// Le dictionnaire vit le temps d'UNE passe disque : 491 ids sur 8 jours vivent dans 2 à 3 fichiers
/// (reprise / fork de session), la dédup doit donc être GLOBALE à la passe (D-32-03). Le helper
/// s'instancie de l'extérieur pour que la phase 33 puisse le scoper autrement (curseurs).
///
/// Type NEUTRE, pur : aucune E/S, aucune horloge, aucun type WPF.
/// </summary>
public sealed class DedupUsage
{
    private readonly Dictionary<string, (DateTimeOffset Ts, long In, long Out, long CacheW, long CacheR)> _parId
        = new(StringComparer.Ordinal);

    // Lignes sans message.id ni requestId : jamais dédoublonnées, jamais jetées.
    private readonly List<(DateTimeOffset Ts, long Tokens)> _sansId = new();

    /// <summary>Nombre de messages distincts vus (une clé = message.id, à défaut requestId).</summary>
    public int MessagesDistincts => _parId.Count;

    /// <summary>Nombre de lignes sans aucun identifiant, comptées telles quelles.</summary>
    public int LignesSansIdentifiant => _sansId.Count;

    /// <summary>Nombre total de lignes soumises (avant dédup) : le rapport à <see cref="MessagesDistincts"/>
    /// est le facteur de surcomptage qu'aurait produit une somme ligne par ligne.</summary>
    public int LignesVues { get; private set; }

    /// <summary>
    /// Accumule une ligne assistant. Même clé → max de chaque champ et plus petit timestamp ; aucune
    /// clé → entrée séparée comptée telle quelle.
    /// </summary>
    public void Ajouter(string? messageId, string? requestId, DateTimeOffset ts,
                        long input, long output, long cacheCreation, long cacheRead)
    {
        LignesVues++;
        var cle = messageId ?? requestId;
        if (cle is null)
        {
            _sansId.Add((ts, input + output + cacheCreation + cacheRead));
            return;
        }

        if (_parId.TryGetValue(cle, out var v))
        {
            _parId[cle] = (v.Ts <= ts ? v.Ts : ts,
                           Math.Max(v.In, input),
                           Math.Max(v.Out, output),
                           Math.Max(v.CacheW, cacheCreation),
                           Math.Max(v.CacheR, cacheRead));
        }
        else
        {
            _parId[cle] = (ts, input, output, cacheCreation, cacheRead);
        }
    }

    /// <summary>Entrées dédoublonnées (instant, tokens), NON triées : l'appelant trie globalement.</summary>
    public IReadOnlyList<(DateTimeOffset Ts, long Tokens)> Entrees()
        => _parId.Values
                 .Select(v => (v.Ts, v.In + v.Out + v.CacheW + v.CacheR))
                 .Concat(_sansId)
                 .ToList();

    /// <summary>
    /// Lit identifiants et compteurs d'une ligne assistant. Champ absent ou non numérique → 0 ;
    /// identifiant absent ou non textuel → null. Ne lève jamais (parsing tolérant, ROB-02).
    /// </summary>
    public static void LireUsage(JsonElement ligne, out string? messageId, out string? requestId,
                                 out long input, out long output, out long cacheCreation, out long cacheRead)
    {
        messageId = null;
        requestId = null;
        input = output = cacheCreation = cacheRead = 0;

        if (ligne.ValueKind != JsonValueKind.Object) return;

        requestId = Texte(ligne, "requestId");

        if (!ligne.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.Object) return;

        messageId = Texte(m, "id");

        if (!m.TryGetProperty("usage", out var u) || u.ValueKind != JsonValueKind.Object) return;

        input = Nombre(u, "input_tokens");
        output = Nombre(u, "output_tokens");
        cacheCreation = Nombre(u, "cache_creation_input_tokens");
        cacheRead = Nombre(u, "cache_read_input_tokens");
    }

    private static string? Texte(JsonElement o, string nom)
        => o.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long Nombre(JsonElement o, string nom)
        => o.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.Number
           && v.TryGetInt64(out var n) ? n : 0;
}
