using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chronos.Services;

/// <summary>
/// Persistance de <see cref="ChronosSettings"/> dans %APPDATA%\Chronos\settings.json (FEN-07).
///
/// Lecture TOLÉRANTE (ROB-02) : fichier absent OU corrompu OU illisible → défauts, jamais
/// d'exception qui remonte. Écriture ATOMIQUE : on écrit un fichier temp puis on le renomme
/// par-dessus la cible (<see cref="File.Move(string, string, bool)"/> sur le même volume), de
/// sorte qu'un settings.json partiel ne soit jamais observable en cas d'arrêt brutal.
///
/// Type NEUTRE (aucun type WPF en signature) : la garde de pureté Services/Models reste verte.
/// </summary>
public sealed class SettingsService
{
    private readonly ChronosPaths _paths;

    public SettingsService(ChronosPaths paths) => _paths = paths;

    // OverlayCorner sérialisé en texte (lisible/robuste au réordonnancement de l'enum) ;
    // lecture insensible à la casse et tolérante aux virgules trainantes.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Relit settings.json. Fichier absent, JSON corrompu ou erreur d'E/S → <see cref="ChronosSettings"/>
    /// par défaut (Corner=TopRight, RefreshIntervalSeconds=60, Background=false) sans exception.
    /// </summary>
    public ChronosSettings Load()
    {
        try
        {
            if (!File.Exists(_paths.SettingsFile))
                return new ChronosSettings();

            var json = File.ReadAllText(_paths.SettingsFile);
            return JsonSerializer.Deserialize<ChronosSettings>(json, Options) ?? new ChronosSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException)
        {
            // Corrompu / illisible → défauts (ROB-02 : jamais de crash au démarrage).
            return new ChronosSettings();
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
