using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chronos.Services;

namespace Chronos.Services.Historique.Tokens;

/// <summary>Ce qu'un cycle décide pour un transcript : d'où le lire, ou ne pas l'ouvrir.</summary>
public enum EtatFichier
{
    /// <summary>Absent des curseurs (créé ou renommé) : lu de 0.</summary>
    Nouveau,
    /// <summary>Taille et mtime identiques ET curseur en fin de fichier : pas ouvert.</summary>
    Inchange,
    /// <summary>Relu depuis l'offset connu (append-only ; « touch » sans contenu, fragment ou ligne future compris).</summary>
    Grandi,
    /// <summary>Taille inférieure à l'offset (réécriture, remplacement) : relu de 0.</summary>
    Raccourci,
}

/// <summary>Position connue d'un transcript : <see cref="Offset"/> = octets jusqu'au dernier <c>\n</c> inclus d'une ligne traitée.</summary>
public sealed record CurseurFichier(long Offset, long Taille, DateTimeOffset Mtime);

/// <summary>
/// TOK-03 — ce que Chronos a déjà lu de chaque transcript : {chemin relatif → offset de la dernière ligne complète, taille, mtime}.
///
/// POURQUOI des clés RELATIVES à la racine des projets : deux fois plus court (352 Ko mesurés pour 1 603 fichiers en relatif) et
/// intact si le profil change de lettre. Comparées ordinal-insensible (NTFS l'est). Classification : absent → <see cref="EtatFichier.Nouveau"/>
/// (lu de 0) ; taille et mtime identiques ET offset == taille → <see cref="EtatFichier.Inchange"/> (pas ouvert : 63 ms pour 1 603
/// fichiers) ; taille &lt; offset → <see cref="EtatFichier.Raccourci"/> (relu de 0) ; sinon <see cref="EtatFichier.Grandi"/> (relu
/// depuis l'offset — y compris « touch » sans contenu, fragment final ou ligne future : un curseur en retrait de la fin n'est JAMAIS
/// « inchangé », sinon la ligne attendue ne serait plus jamais relue, D-33-09). Un fichier disparu (purge) est retiré : rien à
/// soustraire, l'index d'ids et les agrégats gardent ce qu'ils ont compté. Écrit atomiquement (temp + Move) APRÈS les agrégats de
/// chaque lot ; relu tolérant (absent, corrompu, version inconnue → vide ; entrée incomplète → sautée). Type NEUTRE.
/// </summary>
public sealed class Curseurs
{
    public const int SchemaVersion = 1;

    /// <summary>Sous HistoriqueDir, à côté des agrégats ; 33-03 compose le chemin avec <c>ChronosPaths.HistoriqueDir</c>.</summary>
    public const string NomFichier = "curseurs.json";

    private static readonly UTF8Encoding Utf8SansBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions OptionsEcriture = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // des chemins relatifs lisibles : pas de « / » à la place des barres
        WriteIndented = false,
    };

    private readonly Dictionary<string, CurseurFichier> _parCle = new(StringComparer.OrdinalIgnoreCase);

    public Curseurs(string chemin, string racine)
    {
        Chemin = chemin;
        Racine = racine;
    }

    /// <summary>Fichier <c>curseurs.json</c> piloté (injecté).</summary>
    public string Chemin { get; }

    /// <summary>Racine des projets (<c>ProjectsRoot</c>) dont les clés sont relatives.</summary>
    public string Racine { get; }

    public int Count => _parCle.Count;

    /// <summary>« Type : message » de la dernière sauvegarde ratée ; effacée au premier succès.</summary>
    public string? DerniereErreur { get; private set; }

    /// <summary>Relit le fichier ; absent, illisible, JSON invalide ou version ≠ <see cref="SchemaVersion"/> → curseurs vides
    /// (tout sera relu de 0 : coûteux mais juste). La relecture n'est idempotente que sur les mois que l'index d'ids connaît —
    /// mois ouverts, ou chargés à la demande tant que leur shard est sur disque ; un mois gelé déjà agrégé dont le shard a été
    /// purgé est protégé par <see cref="ReconstructionTokens"/>, qui en ignore les messages (DATA-1).</summary>
    public static Curseurs Charger(string chemin, string racine)
    {
        var c = new Curseurs(chemin, racine);
        if (!File.Exists(chemin)) return c;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(chemin));
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object
                || !o.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number
                || !v.TryGetInt32(out var version) || version != SchemaVersion
                || !o.TryGetProperty("fichiers", out var fichiers) || fichiers.ValueKind != JsonValueKind.Object)
                return c;

            foreach (var p in fichiers.EnumerateObject())
            {
                var e = p.Value;
                if (e.ValueKind != JsonValueKind.Object) continue;
                if (!Entier(e, "offset", out var offset) || !Entier(e, "taille", out var taille)) continue;
                var mtime = e.TryGetProperty("mtime", out var pm) && pm.ValueKind == JsonValueKind.String
                    ? UsageNormalization.InstantDepuisIso(pm.GetString())
                    : null;
                if (mtime is not { } instant) continue;
                c._parCle[p.Name] = new CurseurFichier(offset, taille, instant);
            }
        }
        catch (JsonException) { c._parCle.Clear(); }
        catch (IOException) { c._parCle.Clear(); }
        catch (UnauthorizedAccessException) { c._parCle.Clear(); }
        return c;
    }

    /// <summary>Chemin relatif à <see cref="Racine"/>, séparateurs <c>/</c>.</summary>
    public string CleRelative(string cheminAbsolu) => Path.GetRelativePath(Racine, cheminAbsolu).Replace('\\', '/');

    /// <summary>Décide pour un fichier observé (taille, mtime) : d'où le lire (<paramref name="depuis"/>), ou ne pas l'ouvrir.</summary>
    public EtatFichier Classer(string cheminAbsolu, long taille, DateTimeOffset mtime, out long depuis)
    {
        if (!_parCle.TryGetValue(CleRelative(cheminAbsolu), out var c)) { depuis = 0; return EtatFichier.Nouveau; }
        if (taille < c.Offset) { depuis = 0; return EtatFichier.Raccourci; }
        if (taille == c.Taille && mtime == c.Mtime && c.Offset == c.Taille) { depuis = c.Offset; return EtatFichier.Inchange; }
        depuis = c.Offset;
        return EtatFichier.Grandi;
    }

    /// <summary>Pose le curseur d'un fichier après lecture (la dernière écriture gagne).</summary>
    public void Enregistrer(string cheminAbsolu, long offset, long taille, DateTimeOffset mtime)
        => _parCle[CleRelative(cheminAbsolu)] = new CurseurFichier(offset, taille, mtime);

    /// <summary>Retire les clés absentes de <paramref name="clesPresentes"/> (fichiers disparus du disque) ; rend le nombre retiré.</summary>
    public int RetirerDisparus(IReadOnlySet<string> clesPresentes)
    {
        var disparues = _parCle.Keys.Where(k => !clesPresentes.Contains(k)).ToList();
        foreach (var k in disparues) _parCle.Remove(k);
        return disparues.Count;
    }

    /// <summary>Écriture atomique : fichier temporaire (suffixe <c>.tmp-&lt;pid&gt;</c>) puis <c>File.Move</c> avec remplacement.
    /// Échec → false + <see cref="DerniereErreur"/>, le fichier précédent reste intact.</summary>
    public bool Sauvegarder()
    {
        var tmp = Chemin + ".tmp-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        try
        {
            var dossier = Path.GetDirectoryName(Chemin);
            if (!string.IsNullOrEmpty(dossier)) Directory.CreateDirectory(dossier);

            var dto = new FichierDto
            {
                V = SchemaVersion,
                Fichiers = _parCle.ToDictionary(
                    kv => kv.Key,
                    kv => new EntreeDto
                    {
                        Offset = kv.Value.Offset,
                        Taille = kv.Value.Taille,
                        Mtime = kv.Value.Mtime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    },
                    StringComparer.OrdinalIgnoreCase),
            };
            File.WriteAllBytes(tmp, Utf8SansBom.GetBytes(JsonSerializer.Serialize(dto, OptionsEcriture)));
            File.Move(tmp, Chemin, overwrite: true);
            DerniereErreur = null;
            return true;
        }
        catch (Exception ex)
        {
            DerniereErreur = ex.GetType().Name + " : " + ex.Message;
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            return false;
        }
    }

    private static bool Entier(JsonElement o, string nom, out long valeur)
    {
        valeur = 0;
        return o.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out valeur);
    }

    // Schéma persisté : { "v": 1, "fichiers": { "<clé relative>": { "offset", "taille", "mtime" } } }.
    private sealed class FichierDto
    {
        [JsonPropertyName("v")] public int V { get; set; }
        [JsonPropertyName("fichiers")] public Dictionary<string, EntreeDto> Fichiers { get; set; } = new();
    }

    private sealed class EntreeDto
    {
        [JsonPropertyName("offset")] public long Offset { get; set; }
        [JsonPropertyName("taille")] public long Taille { get; set; }
        [JsonPropertyName("mtime")] public string Mtime { get; set; } = "";
    }
}
