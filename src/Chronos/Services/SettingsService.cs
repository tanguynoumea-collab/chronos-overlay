using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Chronos.Services;

/// <summary>
/// Persistance de <see cref="ChronosSettings"/> dans %APPDATA%\Chronos\settings.json (FEN-07).
///
/// Lecture TOLÉRANTE VALEUR PAR VALEUR (SOC-01, phase 36) : une valeur inconnue (enum disparu ou futur, entier hors
/// enum) ou mal typée retombe sur le défaut de SA propriété (l'initialiseur du record), le reste est conservé. Un JSON
/// réellement illisible (syntaxe, racine non objet, E/S) reste un cas à part : défauts ENTIERS, jamais de réglages « à
/// moitié lus », jamais d'exception qui remonte (ROB-02). La lecture n'écrit RIEN sur disque : la retombée est exposée par
/// <see cref="DerniereLecture"/> et journalisée une fois par démarrage par le rapport de diagnostic ; le Save suivant
/// réécrit simplement le fichier assaini.
///
/// <see cref="SettingsService"/> ne normalise PAS <see cref="ChronosSettings.ThemeKey"/> : le catalogue des thèmes est WPF ;
/// une clé inconnue est conservée brute et <c>ThemeCatalog.ByKey</c> la fait retomber sur « minuit » à l'affichage.
///
/// Écriture ATOMIQUE : on écrit un fichier temp puis on le renomme par-dessus la cible
/// (<see cref="File.Move(string, string, bool)"/> sur le même volume), de sorte qu'un settings.json partiel ne soit jamais
/// observable en cas d'arrêt brutal.
///
/// Type NEUTRE (aucun type WPF en signature) : la garde de pureté Services/Models reste verte.
/// </summary>
public sealed class SettingsService
{
    private readonly ChronosPaths _paths;

    public SettingsService(ChronosPaths paths) => _paths = paths;

    // SOC-01 — rapport de la dernière lecture (issue + valeurs retombées), lu par le diagnostic.
    private volatile LectureReglages _derniereLecture = LectureReglages.Aucune;

    /// <summary>Issue de la dernière lecture de settings.json (SOC-01).</summary>
    public LectureReglages DerniereLecture => _derniereLecture;

    /// <summary>Noms (tels qu'écrits dans le JSON) des valeurs retombées sur leur défaut à la dernière lecture.</summary>
    public IReadOnlyList<string> DernieresRetombees => _derniereLecture.Retombees;

    // OverlayCorner sérialisé en texte (lisible/robuste au réordonnancement de l'enum) ;
    // lecture insensible à la casse et tolérante aux virgules trainantes.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        // Résolveur EXPLICITE : en .NET 8, Options.GetTypeInfo(...) lève tant qu'aucun TypeInfoResolver n'est posé
        // (les options ne sont pas encore « verrouillées » par une première sérialisation). Il sert à énumérer les
        // propriétés telles que le sérialiseur les voit, pour la validation valeur par valeur de Load().
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    // Mêmes tolérances syntaxiques que la lecture finale. NE PAS rendre le JsonObject insensible à la casse (piège 5).
    private static readonly JsonDocumentOptions OptionsDocument = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private sealed record ProprieteReglage(Type Type, bool NullAccepte);

    // Propriétés telles que le SÉRIALISEUR les voit (source de vérité unique : ChronosSettings), calculées une fois.
    private static readonly Lazy<Dictionary<string, ProprieteReglage>> Proprietes = new(() =>
    {
        var contexte = new NullabilityInfoContext();
        return Options.GetTypeInfo(typeof(ChronosSettings)).Properties.ToDictionary(
            p => p.Name,
            p => new ProprieteReglage(p.PropertyType, NullAccepte(p, contexte)),
            StringComparer.OrdinalIgnoreCase);
    });

    private static bool NullAccepte(JsonPropertyInfo p, NullabilityInfoContext contexte)
    {
        if (p.PropertyType.IsValueType) return Nullable.GetUnderlyingType(p.PropertyType) is not null;
        // Référence : null refusé si la propriété est déclarée non-nullable (ThemeKey), accepté sinon (string?).
        return p.AttributeProvider is not PropertyInfo pi || contexte.Create(pi).ReadState != NullabilityState.NotNull;
    }

    /// <summary>
    /// Relit settings.json, tolérant VALEUR PAR VALEUR (SOC-01) : une valeur inconnue ou mal typée prend le défaut de
    /// CETTE propriété, le reste est conservé (<see cref="IssueLectureReglages.LuAvecRetombees"/>). Fichier absent →
    /// défauts (<see cref="IssueLectureReglages.Absent"/>) ; JSON illisible, racine non objet ou erreur d'E/S → défauts
    /// ENTIERS (<see cref="IssueLectureReglages.Illisible"/>). Jamais d'exception, aucune écriture disque ;
    /// <see cref="DerniereLecture"/> est renseignée à chaque appel.
    /// </summary>
    public ChronosSettings Load()
    {
        try
        {
            if (!File.Exists(_paths.SettingsFile))
            {
                _derniereLecture = new(IssueLectureReglages.Absent, Array.Empty<string>());
                return new ChronosSettings();
            }
            var texte = File.ReadAllText(_paths.SettingsFile);

            // 1) Document ENTIER parsé d'abord : une erreur de syntaxe n'importe où lève ICI, avant qu'une seule
            //    valeur ne soit exploitée → jamais de réglages « à moitié lus » (SOC-01, critère 2).
            if (JsonNode.Parse(texte, nodeOptions: null, documentOptions: OptionsDocument) is not JsonObject racine)
            {
                _derniereLecture = new(IssueLectureReglages.Illisible, Array.Empty<string>());   // null, tableau, scalaire
                return new ChronosSettings();
            }

            // 2) Valeur par valeur, avec les MÊMES options que la lecture finale ; les fautives sont retirées.
            var retombees = new List<string>();
            foreach (var (nom, valeur) in racine.ToList())   // copie : on retire pendant le parcours
            {
                if (!Proprietes.Value.TryGetValue(nom, out var p)) continue;   // membre inconnu : STJ l'ignore déjà
                if (!ValeurAcceptable(valeur, p)) { racine.Remove(nom); retombees.Add(nom); }
            }

            // 3) Lecture finale : chaque membre retiré prend l'initialiseur de SA propriété dans le record.
            var lu = racine.Deserialize<ChronosSettings>(Options) ?? new ChronosSettings();
            _derniereLecture = new(retombees.Count == 0 ? IssueLectureReglages.Lu : IssueLectureReglages.LuAvecRetombees, retombees);
            return lu;
        }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException
                                      or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            // Illisible (syntaxe, clés strictement dupliquées, E/S) → défauts ENTIERS (ROB-02 conservé).
            _derniereLecture = new(IssueLectureReglages.Illisible, Array.Empty<string>());
            return new ChronosSettings();
        }
    }

    /// <summary>Vrai si <paramref name="valeur"/> se lit dans le type de la propriété avec les options réelles, et qu'un
    /// enum lu est une valeur DÉFINIE (STJ accepte un entier hors enum).</summary>
    private static bool ValeurAcceptable(JsonNode? valeur, ProprieteReglage p)
    {
        if (valeur is null) return p.NullAccepte;   // null sur bool/double/enum/ThemeKey → retombée (piège 4)
        try
        {
            var lu = valeur.Deserialize(p.Type, Options);
            var t = Nullable.GetUnderlyingType(p.Type) ?? p.Type;
            // STJ accepte un entier hors enum (« Corner »: 99) : on le refuse (piège 3). Aucun enum des réglages n'est [Flags].
            return !(t.IsEnum && lu is not null && !Enum.IsDefined(t, lu));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException or FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Écrit settings.json de façon atomique : le dossier est créé au besoin, on écrit un temp
    /// puis on le renomme par-dessus la cible (aucun .tmp résiduel après succès).
    /// </summary>
    public void Save(ChronosSettings settings)
    {
        var dir = Path.GetDirectoryName(_paths.SettingsFile)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, Options);

        // Temp unique par process, sur le même volume que la cible → File.Move atomique.
        var tmp = _paths.SettingsFile + $".tmp-{Environment.ProcessId}";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _paths.SettingsFile, overwrite: true);
    }
}
