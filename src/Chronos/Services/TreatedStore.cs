using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Magasin AUTO-GÉRÉ et RÉVERSIBLE des sessions « traitées » (répondues ou acquittées par un geste) dans
/// %APPDATA%\Chronos\treated.json — une map { session_id : treatedWaitingTs(ms) }, où l'horodatage est
/// celui de l'ÉPISODE d'attente qui a été traité. Le <see cref="SessionTreatmentTracker"/> l'alimente
/// (ajout NET-01) et le purge (réapparition NET-03) ; le <see cref="SessionMonitor"/> masque les
/// sessions encore présentes.
///
/// Calqué sur <see cref="ArchiveStore"/> (mêmes patterns : écriture atomique tmp+move, lecture tolérante
/// via <see cref="JsonDocument"/>, chemins %APPDATA%) MAIS avec une sémantique DISTINCTE :
///   • archivé (<see cref="ArchiveStore"/>) = PERMANENT, ne réapparaît jamais (NET-04) ;
///   • traité (ce magasin) = RÉVERSIBLE via <see cref="Remove"/> (la session réapparaît sur un nouvel
///     épisode d'attente, NET-03).
///
/// L'horloge est INJECTÉE, jamais lue au système : un filtre de durée adossé à l'heure de la machine
/// rend ses tests verts le jour où on les écrit et rouges six heures plus tard, sans qu'une ligne de
/// code ait bougé. Trois plans de la phase 22 sont tombés dans ce piège ; il ne se reproduit pas ici.
///
/// <para><see cref="RetentionMax"/> est une RÉTENTION DE FICHIER, jamais un délai d'affichage. Depuis
/// TRT-02, la valeur mémorisée est l'instant que le SIGNAL porte, pas celui du guetteur : une borne de
/// six heures adossée à cet instant rendait le magasin AVEUGLE à toute session attendant depuis plus de
/// six heures — c'est-à-dire précisément celle dont ce milestone est parti, et deux heures pleines
/// pendant lesquelles « marquer traitée » était un no-op silencieux. La borne doit donc être
/// strictement supérieure au seuil au-delà duquel le moniteur cesse de lire une session (huit heures),
/// puisqu'au-delà la session n'est de toute façon plus affichée. La réversibilité, elle, reste portée
/// par NET-03 — jamais par une horloge.</para>
///
/// Tolérance totale : fichier absent/corrompu → map vide, jamais d'exception. Aucun type WPF (couche neutre).
/// </summary>
public sealed class TreatedStore
{
    // Borne de croissance du FICHIER, strictement supérieure au seuil d'abandon de lecture du moniteur.
    private static readonly System.TimeSpan RetentionMax = System.TimeSpan.FromHours(24);

    private readonly string _path;
    private readonly IClock _horloge;

    public TreatedStore(string? path = null, IClock? clock = null)
    {
        _path = path ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Chronos", "treated.json");
        _horloge = clock ?? new SystemClock();
    }

    /// <summary>Map { session_id : treatedWaitingTs(ms) } des entrées encore retenues dans le fichier.</summary>
    public IReadOnlyDictionary<string, long> Load()
    {
        var now = _horloge.UtcNow.ToUnixTimeMilliseconds();
        var map = new Dictionary<string, long>();
        try
        {
            if (!File.Exists(_path)) return map;
            using var doc = JsonDocument.Parse(File.ReadAllText(_path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return map;
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Value.TryGetInt64(out var ts) && now - ts < RetentionMax.TotalMilliseconds)
                    map[p.Name] = ts;
        }
        catch { }
        return map;
    }

    /// <summary>
    /// Marque une session comme traitée pour l'épisode d'attente <paramref name="treatedWaitingTs"/>
    /// (idempotent) et laisse tomber les entrées hors rétention. Écriture atomique (tmp + move).
    /// </summary>
    public void Set(string sessionId, long treatedWaitingTs)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        var map = LoadMutable();
        map[sessionId] = treatedWaitingTs;
        WriteAtomic(map);
    }

    /// <summary>
    /// RETIRE une session du magasin (point RÉVERSIBLE, NET-03, absent d'<see cref="ArchiveStore"/>) et
    /// laisse tomber les entrées hors rétention. Réécriture atomique de la MÊME façon que
    /// <see cref="Set"/>. Tolérant.
    /// </summary>
    public void Remove(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        var map = LoadMutable();
        if (!map.Remove(sessionId)) return; // rien à faire → évite une réécriture inutile
        WriteAtomic(map);
    }

    // Recharge la map existante en laissant tomber les entrées hors rétention (comme ArchiveStore.Add).
    // Tolérant.
    private Dictionary<string, long> LoadMutable()
    {
        var now = _horloge.UtcNow.ToUnixTimeMilliseconds();
        var map = new Dictionary<string, long>();
        try
        {
            if (File.Exists(_path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_path));
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    foreach (var p in doc.RootElement.EnumerateObject())
                        if (p.Value.TryGetInt64(out var ts) && now - ts < RetentionMax.TotalMilliseconds)
                            map[p.Name] = ts;
            }
        }
        catch { }
        return map;
    }

    // Écrit la map atomiquement (tmp propre au process + move avec overwrite). Ne lève jamais.
    private void WriteAtomic(Dictionary<string, long> map)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(map);
            var tmp = _path + ".tmp-" + System.Environment.ProcessId;
            File.WriteAllText(tmp, json);
            File.Move(tmp, _path, overwrite: true);
        }
        catch { }
    }
}
