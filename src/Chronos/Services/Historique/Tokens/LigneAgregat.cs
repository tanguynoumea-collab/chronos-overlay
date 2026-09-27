using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chronos.Models.Historique.Tokens;

namespace Chronos.Services.Historique.Tokens;

/// <summary>
/// TOK-01 — UNE ligne JSONL d'agrégat de tokens, dans les deux sens.
///
/// <para><b>Écriture</b> : <c>System.Text.Json</c> seul, jamais de concaténation ni de <c>ToString()</c> (un entier
/// part invariant même sous fr-FR : pas d'espace de groupe). Neuf champs, TOUJOURS tous présents, dans l'ordre du
/// contrat de données §3 (<see cref="Champs"/>) : <c>{v, slot, model, sub, in, out, cache_w, cache_r, n}</c>. Les
/// quatre compteurs sont écrits SÉPARÉS ; leur somme n'apparaît nulle part (elle n'a pas de sens : les limites
/// pondèrent par modèle et par nature). Le <c>slot</c> est écrit en UTC au format aller-retour « O » (D-33-02) :
/// le même dialecte d'instant que <c>t</c> du journal des relevés — un seul <c>Instant()</c> dans historique\,
/// pour +13 octets par ligne (≈ 148 o/ligne, ≈ 335 Ko pour un mois chargé — négligeable).</para>
///
/// <para><b>Lecture</b> : TOLÉRANTE ligne par ligne, jamais d'exception. <c>v</c> est lu EN PREMIER : une ligne d'un
/// schéma inconnu est sautée avant qu'on lise quoi que ce soit d'autre. POURQUOI un agrégat sans compteur est REFUSÉ
/// (D-33-03) — et non lu avec des <c>null</c> comme un relevé du journal : une tranche est une SOMME ; un compteur
/// absent ou non entier ne peut être ni « 0 » (ce serait un chiffre inventé) ni « inconnu » (on ne saurait plus
/// sommer la tranche). De même <c>model</c> absent → refusée (la tranche n'a pas de clé) et <c>slot</c> non aligné
/// sur 15 min → refusée (elle ne correspond à aucune tranche). Seul <c>sub</c> est optionnel : absent = false
/// (l'origine « principal » est le défaut naturel). Une ligne refusée est COMPTÉE par l'appelant, jamais tue.</para>
///
/// <para>Le texte ISO → instant passe par <see cref="UsageNormalization.InstantDepuisIso"/>, point unique HDR-05.
/// Entiers seulement (garde TOK-05). Type NEUTRE (aucun WPF).</para>
/// </summary>
public static class LigneAgregat
{
    /// <summary>Version du schéma de ligne. Une ligne d'une autre version est sautée, pas le fichier.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Les neuf champs d'une ligne d'agrégat, dans l'ordre d'écriture — le contrat de données §3.
    /// Exposé pour la garde documentaire (33-05 : chaque champ nommé au §8 de <c>docs/data-sources.md</c>).</summary>
    public static readonly string[] Champs = { "v", "slot", "model", "sub", "in", "out", "cache_w", "cache_r", "n" };

    /// <summary>Le périmètre PARTIEL de ces agrégats, écrit une fois pour toutes (D-33-06) : comparé mot pour mot au §8
    /// de <c>docs/data-sources.md</c> par la garde documentaire (33-05) et affiché tel quel au diagnostic. Ces tokens
    /// ne voient ni Cowork ni claude.ai (même pool de forfait) et ne sont pas pondérés par modèle : ils ne peuvent
    /// donc jamais fonder un pourcentage.</summary>
    public const string Perimetre = "Claude Code seulement — hors Cowork et claude.ai ; bruts, non pondérés";

    // Encodeur RELÂCHÉ : l'encodeur par défaut échappe « + » en +, ce qui casserait l'offset ISO (« +00:00 »).
    // Pas de WhenWritingNull : aucun champ n'est optionnel à l'écriture.
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // DTO d'écriture. L'ordre des propriétés EST l'ordre sur le fil (Champs). Les propriétés s'appellent Entree/Sortie
    // parce que « in » et « out » sont des mots-clés C# — le NOM SUR LE FIL reste « in » / « out ».
    private sealed class LigneAgregatDto
    {
        [JsonPropertyName("v"), JsonPropertyOrder(0)] public int V { get; set; }
        [JsonPropertyName("slot"), JsonPropertyOrder(1)] public string Slot { get; set; } = "";
        [JsonPropertyName("model"), JsonPropertyOrder(2)] public string Model { get; set; } = "";
        [JsonPropertyName("sub"), JsonPropertyOrder(3)] public bool Sub { get; set; }
        [JsonPropertyName("in"), JsonPropertyOrder(4)] public long Entree { get; set; }
        [JsonPropertyName("out"), JsonPropertyOrder(5)] public long Sortie { get; set; }
        [JsonPropertyName("cache_w"), JsonPropertyOrder(6)] public long CacheW { get; set; }
        [JsonPropertyName("cache_r"), JsonPropertyOrder(7)] public long CacheR { get; set; }
        [JsonPropertyName("n"), JsonPropertyOrder(8)] public int N { get; set; }
    }

    /// <summary>Une tranche → une ligne JSON (sans « \n » final : l'écrivain l'ajoute).</summary>
    public static string Serialiser(TrancheTokens t)
        => JsonSerializer.Serialize(new LigneAgregatDto
        {
            V = SchemaVersion,
            Slot = Instant(t.Slot),
            Model = t.Model,
            Sub = t.Sub,
            Entree = t.In,
            Sortie = t.Out,
            CacheW = t.CacheW,
            CacheR = t.CacheR,
            N = t.N,
        }, Options);

    /// <summary>
    /// Une ligne → une tranche. <c>false</c> = ligne à ignorer (vide, tronquée, schéma inconnu, slot illisible ou
    /// non aligné, modèle absent, compteur absent / non entier / négatif). Ne lève jamais.
    /// </summary>
    public static bool Parser(string ligne, out TrancheTokens? tranche)
    {
        tranche = null;
        if (string.IsNullOrWhiteSpace(ligne)) return false;

        try
        {
            using var doc = JsonDocument.Parse(ligne);
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object) return false;

            // v EN PREMIER : absent, pas un nombre, ou d'une autre version → on ne lit rien d'autre.
            if (!o.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number
                || !v.TryGetInt32(out var version) || version != SchemaVersion)
                return false;

            if (!o.TryGetProperty("slot", out var s) || s.ValueKind != JsonValueKind.String) return false;
            var slot = UsageNormalization.InstantDepuisIso(s.GetString());
            if (slot is null) return false;
            var aligne = TrancheTokens.SlotDe(slot.Value);
            if (slot.Value != aligne) return false;   // D-33-03 : un slot non aligné ne correspond à aucune tranche

            if (!o.TryGetProperty("model", out var m) || m.ValueKind != JsonValueKind.String) return false;
            var model = m.GetString();
            if (string.IsNullOrEmpty(model)) return false;

            // sub : optionnel, false par défaut ; présent mais pas un booléen → refusée (une valeur inattendue n'est pas « false »).
            var sub = false;
            if (o.TryGetProperty("sub", out var sb))
            {
                if (sb.ValueKind == JsonValueKind.True) sub = true;
                else if (sb.ValueKind != JsonValueKind.False) return false;
            }

            if (LireCompteur(o, "in") is not { } entree) return false;
            if (LireCompteur(o, "out") is not { } sortie) return false;
            if (LireCompteur(o, "cache_w") is not { } cacheW) return false;
            if (LireCompteur(o, "cache_r") is not { } cacheR) return false;

            if (!o.TryGetProperty("n", out var ne) || ne.ValueKind != JsonValueKind.Number
                || !ne.TryGetInt32(out var n) || n < 0)
                return false;

            tranche = new TrancheTokens(aligne, model, sub, entree, sortie, cacheW, cacheR, n);
            return true;
        }
        catch (JsonException)
        {
            return false;   // ligne tronquée ou corrompue : sautée, jamais fatale
        }
    }

    // UTC, format aller-retour : « 2026-09-01T00:00:00.0000000+00:00 ». « O » est invariant par construction ;
    // la culture est passée quand même, par discipline.
    private static string Instant(DateTimeOffset t)
        => t.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    // Un compteur : entier 64 bits, non négatif ; absent, non nombre, non entier ou négatif → null (ligne refusée).
    private static long? LireCompteur(JsonElement o, string nom)
        => o.TryGetProperty(nom, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var c) && c >= 0
            ? c
            : null;
}
