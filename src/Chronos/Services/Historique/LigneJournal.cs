using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chronos.Models;
using Chronos.Models.Historique;

namespace Chronos.Services.Historique;

/// <summary>
/// JRN-03 — UNE ligne JSONL du journal des relevés, dans les deux sens.
///
/// <para><b>Écriture</b> : <c>System.Text.Json</c> seul (D-32-20), jamais de concaténation ni de
/// <c>ToString()</c> — un <c>double</c> part invariant (« 0.12 », jamais « 0,12 », piège fr-FR déjà payé
/// par <c>UsageNormalization</c>) et BRUT (D-32-18). Les enums partent par leur NOM (« Autorise »),
/// les <c>null</c> sont omis, une ligne tient sur une ligne (&lt; 4 Ko ; ~230 octets mesurés).
/// Les instants sont écrits en UTC au format aller-retour « O » : <c>DateTimeOffset</c> sérialisé nu
/// omettrait les fractions nulles et écrirait l'offset d'origine — deux formes pour le même instant.</para>
///
/// <para><b>Lecture</b> : TOLÉRANTE champ par champ (motif <c>TranscriptActivityProvider.ReadAsync</c>).
/// <c>v</c> est lu EN PREMIER : une ligne d'un schéma inconnu est sautée avant qu'on lise quoi que ce soit
/// d'autre. <c>t</c> illisible → ligne refusée (sans instant, une ligne ne se place nulle part). Un champ
/// optionnel absent ou du mauvais type → <c>null</c>, jamais 0. Un relevé sans <c>source</c> lisible est
/// refusé : il n'a pas de clé d'idempotence. Un <c>ev</c> inconnu est CONSERVÉ (<see cref="TypeEvenement.NonReconnu"/>).
/// Aucune exception ne sort de <see cref="Parser"/>.</para>
///
/// <para>Type NEUTRE (aucun WPF). Le texte ISO → instant passe par <see cref="UsageNormalization.InstantDepuisIso"/>,
/// point unique HDR-05.</para>
/// </summary>
public static class LigneJournal
{
    /// <summary>Version du schéma de ligne. Une ligne d'une autre version est sautée, pas le fichier.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Les champs d'une ligne de relevé, dans l'ordre d'écriture — le contrat de données §3.
    /// Exposé pour la garde documentaire (32-07 : chaque champ nommé dans <c>docs/data-sources.md</c>).</summary>
    public static readonly string[] ChampsReleve =
        { "v", "t", "u5", "r5", "u7", "r7", "statut5", "statut7", "overage", "overage_statut", "source" };

    // Encodeur RELÂCHÉ : l'encodeur par défaut échappe « + » en +, ce qui casserait l'offset ISO
    // (« +00:00 ») et rendrait le fichier illisible à l'œil. Le fichier est à nous, en UTF-8 : rien à protéger.
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // DTO d'écriture d'un relevé. L'ordre des propriétés EST l'ordre sur le fil (ChampsReleve).
    private sealed class LigneReleveDto
    {
        [JsonPropertyName("v"), JsonPropertyOrder(0)] public int V { get; set; }
        [JsonPropertyName("t"), JsonPropertyOrder(1)] public string T { get; set; } = "";
        [JsonPropertyName("u5"), JsonPropertyOrder(2)] public double? U5 { get; set; }
        [JsonPropertyName("r5"), JsonPropertyOrder(3)] public string? R5 { get; set; }
        [JsonPropertyName("u7"), JsonPropertyOrder(4)] public double? U7 { get; set; }
        [JsonPropertyName("r7"), JsonPropertyOrder(5)] public string? R7 { get; set; }
        [JsonPropertyName("statut5"), JsonPropertyOrder(6)] public StatutServeur? Statut5 { get; set; }
        [JsonPropertyName("statut7"), JsonPropertyOrder(7)] public StatutServeur? Statut7 { get; set; }
        [JsonPropertyName("overage"), JsonPropertyOrder(8)] public double? Overage { get; set; }
        [JsonPropertyName("overage_statut"), JsonPropertyOrder(9)] public StatutServeur? OverageStatut { get; set; }
        [JsonPropertyName("source"), JsonPropertyOrder(10)] public SourceUsage Source { get; set; }
    }

    // DTO d'écriture d'un événement : {v, t, ev, magasin, cause, version}.
    private sealed class LigneEvenementDto
    {
        [JsonPropertyName("v"), JsonPropertyOrder(0)] public int V { get; set; }
        [JsonPropertyName("t"), JsonPropertyOrder(1)] public string T { get; set; } = "";
        [JsonPropertyName("ev"), JsonPropertyOrder(2)] public string Ev { get; set; } = "";
        [JsonPropertyName("magasin"), JsonPropertyOrder(3)] public string? Magasin { get; set; }
        [JsonPropertyName("cause"), JsonPropertyOrder(4)] public string? Cause { get; set; }
        [JsonPropertyName("version"), JsonPropertyOrder(5)] public string? Version { get; set; }
    }

    /// <summary>Un relevé → une ligne JSON (sans « \n » final : l'écrivain l'ajoute).</summary>
    public static string Serialiser(ReleveJournal r)
        => JsonSerializer.Serialize(new LigneReleveDto
        {
            V = SchemaVersion,
            T = Instant(r.T),
            U5 = r.U5,
            R5 = r.R5 is { } r5 ? Instant(r5) : null,
            U7 = r.U7,
            R7 = r.R7 is { } r7 ? Instant(r7) : null,
            Statut5 = r.Statut5,
            Statut7 = r.Statut7,
            Overage = r.Overage,
            OverageStatut = r.OverageStatut,
            Source = r.Source,
        }, Options);

    /// <summary>Un événement → une ligne JSON. Un <see cref="TypeEvenement.NonReconnu"/> n'a pas de nom de fil :
    /// on n'écrit JAMAIS un inconnu (<see cref="ArgumentException"/>).</summary>
    public static string Serialiser(EvenementJournal e)
    {
        var ev = TypeEvenementTexte.Nom(e.Type)
                 ?? throw new ArgumentException("Un événement non reconnu ne s'écrit pas dans le journal.", nameof(e));

        return JsonSerializer.Serialize(new LigneEvenementDto
        {
            V = SchemaVersion,
            T = Instant(e.T),
            Ev = ev,
            Magasin = e.Magasin,
            Cause = e.Cause,
            Version = e.Version,
        }, Options);
    }

    /// <summary>
    /// Une ligne → un relevé OU un événement. <c>false</c> = ligne à ignorer (vide, tronquée, schéma
    /// inconnu, sans instant lisible, relevé sans source). Ne lève jamais.
    /// </summary>
    public static bool Parser(string ligne, out ReleveJournal? releve, out EvenementJournal? evenement)
    {
        releve = null;
        evenement = null;
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

            var t = LireInstant(o, "t");
            if (t is null) return false;

            if (o.TryGetProperty("ev", out var ev))
            {
                var evBrut = ev.ValueKind == JsonValueKind.String ? ev.GetString() : ev.GetRawText();
                evenement = new EvenementJournal(
                    t.Value,
                    TypeEvenementTexte.Depuis(evBrut),
                    Magasin: LireTexte(o, "magasin"),
                    Cause: LireTexte(o, "cause"),
                    Version: LireTexte(o, "version"),
                    EvBrut: evBrut);
                return true;
            }

            var source = LireEnum<SourceUsage>(o, "source");
            if (source is null) return false;   // sans source, pas de clé d'idempotence : ignoré

            releve = new ReleveJournal(
                t.Value,
                source.Value,
                U5: LireNombre(o, "u5"),
                R5: LireInstant(o, "r5"),
                Statut5: LireEnum<StatutServeur>(o, "statut5"),
                U7: LireNombre(o, "u7"),
                R7: LireInstant(o, "r7"),
                Statut7: LireEnum<StatutServeur>(o, "statut7"),
                Overage: LireNombre(o, "overage"),
                OverageStatut: LireEnum<StatutServeur>(o, "overage_statut"));
            return true;
        }
        catch (JsonException)
        {
            return false;   // ligne tronquée ou corrompue : sautée, jamais fatale
        }
    }

    // UTC, format aller-retour : « 2026-09-27T10:05:00.0000000+00:00 ». « O » est invariant par
    // construction ; la culture est passée quand même, par discipline.
    private static string Instant(DateTimeOffset t)
        => t.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset? LireInstant(JsonElement o, string nom)
        => o.TryGetProperty(nom, out var e) && e.ValueKind == JsonValueKind.String
            ? UsageNormalization.InstantDepuisIso(e.GetString())
            : null;

    private static double? LireNombre(JsonElement o, string nom)
        => o.TryGetProperty(nom, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out var d)
            ? d
            : null;

    private static string? LireTexte(JsonElement o, string nom)
        => o.TryGetProperty(nom, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    private static TEnum? LireEnum<TEnum>(JsonElement o, string nom) where TEnum : struct, Enum
        => o.TryGetProperty(nom, out var e) && e.ValueKind == JsonValueKind.String
           && Enum.TryParse<TEnum>(e.GetString(), ignoreCase: false, out var valeur)
            ? valeur
            : null;
}
