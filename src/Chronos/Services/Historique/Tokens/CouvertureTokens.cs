using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chronos.Models.Historique.Tokens;

namespace Chronos.Services.Historique.Tokens;

/// <summary>
/// TOK-04 — ce que les agrégats GARANTISSENT et ce qu'ils ne peuvent pas dire.
///
/// <para><b>POURQUOI un état persisté et non une déduction sur les mtimes</b> : « juillet purgé » est vrai par mtime
/// (1 fichier) et FAUX par contenu (23 211 lignes de juillet vivent dans des fichiers d'août/septembre, mesuré le
/// 2026-09-27) — on ne peut pas savoir quelles tranches de juillet manquent. Chaque passe COMPLÈTE (reconstruction
/// terminée, puis chaque cycle incrémental complet) garantit l'intervalle <c>[début − HorizonPurge, fin[</c> : tout
/// transcript de moins de <see cref="HorizonPurge"/> existait encore et a été lu. Les intervalles sont fusionnés ; un arrêt
/// de Chronos plus long que l'horizon laisse un trou entre deux intervalles — zone « transcripts absents », vraie et dite.</para>
///
/// <para><b>HYP-4</b> : <see cref="HorizonPurge"/> = <c>cleanupPeriodDays</c> par défaut de Claude Code (30 j ; absent de
/// <c>~/.claude/settings.json</c> sur cette machine, vérifié ; NON lu ici en v1.8 — couplage inutile, écrit au §8 des docs).</para>
///
/// <para><see cref="PlusAncienneLigneVue"/> : le plus vieux timestamp jamais lu, monotone décroissant — avant lui, « hors
/// couverture ». Fichier <see cref="NomFichier"/> à côté de <c>curseurs.json</c> sous le dossier historique ; écriture
/// atomique (temp + Move), lecture tolérante (absent, corrompu, version inconnue → couverture vide). Les instants sont
/// écrits en UTC au format « O » (un seul dialecte dans historique\) et relus par <see cref="UsageNormalization.InstantDepuisIso"/>.
/// <see cref="Classer"/> est appelé par le lecteur (33-04) pendant que la reconstruction (33-03) appelle <see cref="VoirLigne"/>
/// et <see cref="GarantirPasse"/> : un verrou, <see cref="Intervalles"/> rendu en copie. Entiers seulement (garde TOK-05). Type NEUTRE.</para>
/// </summary>
public sealed class CouvertureTokens
{
    /// <summary>Version du schéma persisté. Une autre version est refusée en bloc : couverture vide, jamais devinée.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Nom du fichier, sous le dossier historique (écrit par la reconstruction 33-03, lu par le lecteur 33-04).</summary>
    public const string NomFichier = "couverture.json";

    /// <summary>HYP-4 — horizon de purge de Claude Code (<c>cleanupPeriodDays</c> par défaut, 30 j). Une passe complète
    /// garantit tout ce qui a moins que cet âge au moment où elle a commencé.</summary>
    public static readonly TimeSpan HorizonPurge = TimeSpan.FromDays(30);

    // Encodeur relâché : l'encodeur par défaut échappe « + » et casserait l'offset ISO. Compact : c'est un fichier d'état.
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private sealed class CouvertureDto
    {
        [JsonPropertyName("v"), JsonPropertyOrder(0)] public int V { get; set; }
        [JsonPropertyName("plus_ancienne_ligne_vue"), JsonPropertyOrder(1)] public string? PlusAncienneLigneVue { get; set; }
        [JsonPropertyName("intervalles"), JsonPropertyOrder(2)] public List<IntervalleDto> Intervalles { get; set; } = new();
    }

    private sealed class IntervalleDto
    {
        [JsonPropertyName("debut"), JsonPropertyOrder(0)] public string Debut { get; set; } = "";
        [JsonPropertyName("fin"), JsonPropertyOrder(1)] public string Fin { get; set; } = "";
    }

    private readonly object _verrou = new();
    private List<IntervalleGaranti> _intervalles = new();

    /// <summary>Le plus vieux timestamp de ligne jamais lu (UTC). null = rien lu encore : tout est hors couverture.</summary>
    public DateTimeOffset? PlusAncienneLigneVue { get; private set; }

    /// <summary>Intervalles garantis, fusionnés, triés par début — copie.</summary>
    public IReadOnlyList<IntervalleGaranti> Intervalles
    {
        get { lock (_verrou) return _intervalles.ToList(); }
    }

    /// <summary>« Type : message » de la dernière sauvegarde ratée ; null après un succès.</summary>
    public string? DerniereErreur { get; private set; }

    /// <summary>Abaisse <see cref="PlusAncienneLigneVue"/> si <paramref name="ts"/> est plus ancien. Ne remonte jamais.</summary>
    public void VoirLigne(DateTimeOffset ts)
    {
        var u = ts.ToUniversalTime();
        lock (_verrou)
        {
            if (PlusAncienneLigneVue is null || u < PlusAncienneLigneVue.Value) PlusAncienneLigneVue = u;
        }
    }

    /// <summary>Une passe COMPLÈTE de <paramref name="debutPasse"/> à <paramref name="finPasse"/> garantit
    /// <c>[debutPasse − HorizonPurge, finPasse[</c> ; l'intervalle est fusionné aux chevauchants et contigus.
    /// Une passe à l'envers (fin avant début) n'ajoute rien.</summary>
    public void GarantirPasse(DateTimeOffset debutPasse, DateTimeOffset finPasse)
    {
        var debut = debutPasse.ToUniversalTime() - HorizonPurge;
        var fin = finPasse.ToUniversalTime();
        if (fin < debutPasse.ToUniversalTime()) return;

        lock (_verrou)
        {
            _intervalles.Add(new IntervalleGaranti(debut, fin));
            _intervalles = Fusionner(_intervalles);
        }
    }

    /// <summary>Trois états, jamais un « zéro » implicite : avant le plus vieux transcript vu → hors couverture ;
    /// dans un intervalle garanti (début inclus, fin exclue) → couverte ; sinon → transcripts absents.</summary>
    public EtatCouverture Classer(DateTimeOffset slot)
    {
        lock (_verrou)
        {
            if (PlusAncienneLigneVue is null || slot < PlusAncienneLigneVue.Value) return EtatCouverture.HorsCouverture;
            return _intervalles.Any(i => i.Debut <= slot && slot < i.Fin) ? EtatCouverture.Couverte : EtatCouverture.TranscriptsAbsents;
        }
    }

    /// <summary>Lecture TOLÉRANTE : absent, illisible, JSON invalide, version inconnue → couverture vide. Un intervalle
    /// mal formé est sauté, les autres sont gardés. Ne lève jamais.</summary>
    public static CouvertureTokens Charger(string chemin)
    {
        var c = new CouvertureTokens();
        try
        {
            if (!File.Exists(chemin)) return c;

            using var doc = JsonDocument.Parse(File.ReadAllText(chemin));
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object) return c;

            // v EN PREMIER : un schéma inconnu n'est pas lu du tout.
            if (!o.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number
                || !v.TryGetInt32(out var version) || version != SchemaVersion)
                return c;

            if (LireInstant(o, "plus_ancienne_ligne_vue") is { } plusAncienne) c.PlusAncienneLigneVue = plusAncienne;

            var intervalles = new List<IntervalleGaranti>();
            if (o.TryGetProperty("intervalles", out var tableau) && tableau.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in tableau.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object) continue;
                    if (LireInstant(e, "debut") is not { } debut || LireInstant(e, "fin") is not { } fin || fin < debut) continue;
                    intervalles.Add(new IntervalleGaranti(debut, fin));
                }
            }
            c._intervalles = Fusionner(intervalles);
            return c;
        }
        catch
        {
            return new CouvertureTokens();   // un état illisible n'est pas une panne : tout redevient « hors couverture », honnêtement
        }
    }

    /// <summary>Écriture ATOMIQUE (temp unique par processus + <c>Move</c>). <c>false</c> + <see cref="DerniereErreur"/> en échec ; ne lève jamais.</summary>
    public bool Sauvegarder(string chemin)
    {
        CouvertureDto dto;
        lock (_verrou)
        {
            dto = new CouvertureDto
            {
                V = SchemaVersion,
                PlusAncienneLigneVue = PlusAncienneLigneVue is { } p ? Instant(p) : null,
                Intervalles = _intervalles.Select(i => new IntervalleDto { Debut = Instant(i.Debut), Fin = Instant(i.Fin) }).ToList(),
            };
        }

        try
        {
            var dossier = Path.GetDirectoryName(chemin);
            if (!string.IsNullOrEmpty(dossier)) Directory.CreateDirectory(dossier);

            var tmp = chemin + ".tmp-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
            File.WriteAllText(tmp, JsonSerializer.Serialize(dto, Options));
            File.Move(tmp, chemin, overwrite: true);
            DerniereErreur = null;
            return true;
        }
        catch (Exception ex)
        {
            DerniereErreur = ex.GetType().Name + " : " + ex.Message;
            return false;
        }
    }

    // --- Internes ---

    // Tri par début, puis fusion des chevauchants et des contigus (le début du suivant ≤ la fin du courant).
    private static List<IntervalleGaranti> Fusionner(IEnumerable<IntervalleGaranti> intervalles)
    {
        var resultat = new List<IntervalleGaranti>();
        foreach (var i in intervalles.OrderBy(i => i.Debut).ThenBy(i => i.Fin))
        {
            if (resultat.Count > 0 && i.Debut <= resultat[^1].Fin)
            {
                var dernier = resultat[^1];
                if (i.Fin > dernier.Fin) resultat[^1] = dernier with { Fin = i.Fin };
            }
            else
            {
                resultat.Add(i);
            }
        }
        return resultat;
    }

    private static string Instant(DateTimeOffset t) => t.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset? LireInstant(JsonElement o, string nom)
        => o.TryGetProperty(nom, out var e) && e.ValueKind == JsonValueKind.String
            ? UsageNormalization.InstantDepuisIso(e.GetString())
            : null;
}
