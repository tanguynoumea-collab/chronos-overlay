using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Magasin AUTO-GÉRÉ et RÉVERSIBLE des sessions « traitées » (répondues ou acquittées par focus) dans
/// %APPDATA%\Chronos\treated.json — une map { session_id : treatedWaitingTs(ms) }, où l'horodatage est
/// celui de l'ÉPISODE d'attente qui a été traité. Le <see cref="SessionTreatmentTracker"/> l'alimente
/// (ajout NET-01/NET-02) et le purge (réapparition NET-03) ; le <see cref="SessionMonitor"/> masque les
/// sessions encore présentes.
///
/// Calqué sur <see cref="ArchiveStore"/> (mêmes patterns : TTL, écriture atomique tmp+move, lecture
/// tolérante via <see cref="JsonDocument"/>, chemins %APPDATA%) MAIS avec une sémantique DISTINCTE :
///   • archivé (<see cref="ArchiveStore"/>) = PERMANENT, ne réapparaît jamais (NET-04) ;
///   • traité (ce magasin) = RÉVERSIBLE via <see cref="Remove"/> (la session réapparaît sur un nouvel
///     épisode d'attente, NET-03).
/// Purge auto au-delà de <see cref="Ttl"/> (borne la croissance ; une entrée disparaît une fois la session
/// de toute façon inactive). Tolérance totale : fichier absent/corrompu → map vide, jamais d'exception.
/// Aucun type WPF (couche neutre).
/// </summary>
public sealed class TreatedStore
{
    private static readonly System.TimeSpan Ttl = System.TimeSpan.FromHours(6);
    private readonly string _path;

    public TreatedStore(string? path = null)
        => _path = path ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Chronos", "treated.json");

    /// <summary>Map { session_id : treatedWaitingTs(ms) } des entrées encore valides (TTL non dépassé).</summary>
    public IReadOnlyDictionary<string, long> Load()
    {
        var now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var map = new Dictionary<string, long>();
        try
        {
            if (!File.Exists(_path)) return map;
            using var doc = JsonDocument.Parse(File.ReadAllText(_path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return map;
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Value.TryGetInt64(out var ts) && now - ts < Ttl.TotalMilliseconds)
                    map[p.Name] = ts;
        }
        catch { }
        return map;
    }

    /// <summary>
    /// Marque une session comme traitée pour l'épisode d'attente <paramref name="treatedWaitingTs"/>
    /// (idempotent) et purge les entrées expirées. Écriture atomique (tmp + move).
    /// </summary>
    public void Set(string sessionId, long treatedWaitingTs)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        var map = LoadMutable();
        map[sessionId] = treatedWaitingTs;
        WriteAtomic(map);
    }

    /// <summary>
    /// RETIRE une session du magasin (point RÉVERSIBLE, NET-03, absent d'<see cref="ArchiveStore"/>) et purge
    /// les entrées expirées. Réécriture atomique de la MÊME façon que <see cref="Set"/>. Tolérant.
    /// </summary>
    public void Remove(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        var map = LoadMutable();
        if (!map.Remove(sessionId)) return; // rien à faire → évite une réécriture inutile
        WriteAtomic(map);
    }

    // Recharge la map existante en purgeant les entrées expirées (comme ArchiveStore.Add). Tolérant.
    private Dictionary<string, long> LoadMutable()
    {
        var now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var map = new Dictionary<string, long>();
        try
        {
            if (File.Exists(_path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_path));
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    foreach (var p in doc.RootElement.EnumerateObject())
                        if (p.Value.TryGetInt64(out var ts) && now - ts < Ttl.TotalMilliseconds)
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
