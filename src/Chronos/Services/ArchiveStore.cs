using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Mémorise les sessions ARCHIVÉES (que l'utilisateur ne veut plus voir dans l'overlay) dans
/// %APPDATA%\Chronos\archived.json — une map { session_id : archivedAt(ms) }. Le <see cref="SessionMonitor"/>
/// filtre ces sessions. Purge auto au-delà de <see cref="Ttl"/> (bien au-delà de la fenêtre d'activité de
/// 15 min → l'entrée disparaît une fois la session de toute façon inactive, borne la croissance du fichier).
/// Tolérance totale : fichier absent/corrompu → ensemble vide, jamais d'exception.
/// </summary>
public sealed class ArchiveStore
{
    private static readonly System.TimeSpan Ttl = System.TimeSpan.FromHours(6);
    private readonly string _path;

    public ArchiveStore(string? path = null)
        => _path = path ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Chronos", "archived.json");

    /// <summary>Identifiants archivés encore valides (TTL non dépassé).</summary>
    public ISet<string> Load()
    {
        var now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var set = new HashSet<string>();
        try
        {
            if (!File.Exists(_path)) return set;
            using var doc = JsonDocument.Parse(File.ReadAllText(_path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return set;
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Value.TryGetInt64(out var ts) && now - ts < Ttl.TotalMilliseconds)
                    set.Add(p.Name);
        }
        catch { }
        return set;
    }

    /// <summary>Archive une session (idempotent) et purge les entrées expirées.</summary>
    public void Add(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
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
        map[sessionId] = now;

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
