using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Lit les fichiers d'état de session (%APPDATA%\Chronos\sessions\*.json) écrits par les hooks
/// (<see cref="SessionHookProcessor"/>) et en produit des <see cref="SessionSnapshot"/>, en appliquant
/// une politique d'HONNÊTETÉ sur la fraîcheur :
///   • Working dont le signal date de plus de <see cref="StaleWorking"/> → <see cref="SessionActivity.Unknown"/>
///     (on ne prétend pas « en travail » si on a perdu le fil ; mais un vrai long travail reste plausible
///     un moment — d'où un seuil large).
///   • Les états d'attente PERSISTENT (le fichier ne bouge pas TANT QU'on n'a pas agi — c'est justement
///     le signal). Ils ne deviennent « périmés » qu'au-delà de <see cref="DropAfter"/> (session morte,
///     SessionEnd manqué) → ignorés.
/// Lecture TOLÉRANTE : fichier absent/corrompu → ignoré. Aucun type WPF (couche neutre).
/// </summary>
public sealed class SessionMonitor
{
    private static readonly System.TimeSpan StaleWorking = System.TimeSpan.FromMinutes(20);
    private static readonly System.TimeSpan DropAfter = System.TimeSpan.FromHours(8);

    private readonly string _dir;
    private readonly TranscriptSessionSource _transcripts;
    private readonly ArchiveStore _archive;

    public SessionMonitor(string? sessionsDir = null, TranscriptSessionSource? transcripts = null, ArchiveStore? archive = null)
    {
        _dir = sessionsDir ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Chronos", "sessions");
        _transcripts = transcripts ?? new TranscriptSessionSource();
        _archive = archive ?? new ArchiveStore();
    }

    public string Directory => _dir;

    private static readonly JsonSerializerOptions Tolerant = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Instantané courant des sessions (staleness appliquée). FUSIONNE deux sources par session_id :
    ///   • transcripts (~/.claude/projects) — universel, couvre l'app BUREAU (base) ;
    ///   • fichiers d'état des HOOKS (%APPDATA%\Chronos\sessions) — plus précis (permission), PRIORITAIRES
    ///     quand présents (utilisateurs du terminal).
    /// </summary>
    public IReadOnlyList<SessionSnapshot> Read(System.DateTimeOffset now)
    {
        var byId = new Dictionary<string, SessionSnapshot>();

        // 1) Base : transcripts (app bureau incluse).
        foreach (var t in _transcripts.Read(now)) byId[t.SessionId] = t;

        // 2) Surcharge : fichiers d'état des hooks (plus précis) quand ils existent.
        string[] files;
        try { files = System.IO.Directory.Exists(_dir) ? System.IO.Directory.GetFiles(_dir, "*.json") : System.Array.Empty<string>(); }
        catch { files = System.Array.Empty<string>(); }
        foreach (var f in files)
        {
            var snap = TryRead(f, now);
            if (snap is not null) byId[snap.SessionId] = snap;
        }

        // 3) Retirer les sessions ARCHIVÉES par l'utilisateur.
        var archived = _archive.Load();
        return byId.Values.Where(s => !archived.Contains(s.SessionId)).ToList();
    }

    private static SessionSnapshot? TryRead(string file, System.DateTimeOffset now)
    {
        try
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var doc = JsonDocument.Parse(fs);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;

            var sid = Str(r, "session_id");
            if (string.IsNullOrEmpty(sid)) sid = Path.GetFileNameWithoutExtension(file);
            var project = Str(r, "project");
            if (string.IsNullOrEmpty(project)) project = "(session)";
            var reason = r.TryGetProperty("reason", out var rr) && rr.ValueKind == JsonValueKind.String ? rr.GetString() : null;

            var updatedAt = r.TryGetProperty("updated_at", out var ua) && ua.TryGetInt64(out var ms)
                ? System.DateTimeOffset.FromUnixTimeMilliseconds(ms)
                : System.DateTimeOffset.MinValue;

            if (!System.Enum.TryParse<SessionActivity>(Str(r, "activity"), ignoreCase: true, out var activity))
                activity = SessionActivity.Unknown;

            var age = now - updatedAt;
            if (age > DropAfter) return null; // session morte (SessionEnd manqué) → on ne l'affiche plus

            // Working périmé → Unknown (on ne ment pas sur un fil perdu). Les attentes persistent telles quelles.
            if (activity == SessionActivity.Working && age > StaleWorking)
                activity = SessionActivity.Unknown;

            return new SessionSnapshot(sid, project, activity, reason, updatedAt);
        }
        catch { return null; }
    }

    private static string Str(JsonElement o, string key)
        => o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
