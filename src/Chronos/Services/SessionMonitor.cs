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
    private readonly ISessionSource _transcripts;
    private readonly ArchiveStore _archive;

    // Hystérésis (phase 14, réduite en phase 21) : nuls par défaut = fonctionnalité désactivée. Quand ils
    // sont fournis, le détecteur observe les snapshots BRUTS et alimente TreatedStore ; le filtre masque
    // ensuite les sessions traitées.
    private readonly TreatedStore? _treated;
    private readonly SessionTreatmentTracker? _tracker;

    public SessionMonitor(string? sessionsDir = null, ISessionSource? transcripts = null,
        ArchiveStore? archive = null,
        TreatedStore? treated = null, SessionTreatmentTracker? tracker = null)
    {
        _dir = sessionsDir ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Chronos", "sessions");
        _transcripts = transcripts ?? new TranscriptSessionSource();
        _archive = archive ?? new ArchiveStore();
        _treated = treated;
        _tracker = tracker;
    }

    public string Directory => _dir;

    private static readonly JsonSerializerOptions Tolerant = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Instantané courant des sessions Claude Code (staleness appliquée). FUSIONNE deux sources par
    /// session_id :
    ///   • transcripts (~/.claude/projects) — la base, universelle ;
    ///   • fichiers d'état des HOOKS (%APPDATA%\Chronos\sessions) — plus précis (permission),
    ///     PRIORITAIRES quand présents.
    /// </summary>
    public IReadOnlyList<SessionSnapshot> Read(System.DateTimeOffset now)
    {
        var byId = new Dictionary<string, SessionSnapshot>();

        // 1) Base : transcripts (~/.claude/projects).
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

        // 2.b) Le détecteur de traitement observe les snapshots BRUTS fusionnés (+ horloge) et met à jour
        //      TreatedStore (ajout NET-01, purge NET-03). Best-effort : ne casse JAMAIS le pipeline.
        var raw = byId.Values.ToList();
        try { _tracker?.Observe(raw, now); } catch { }

        // 3) Filtres : archivées (permanent, NET-04) PUIS traitées (réversible). Le détecteur possède l'ajout ET
        //    la purge des entrées treated ; ici on MASQUE simplement toute session encore présente dans le magasin.
        //    On NE ré-implémente PAS la comparaison treatedWaitingTs >= UpdatedAt : la réversibilité NET-03 est
        //    portée par le détecteur (purge sur nouvel épisode), pas par ce filtre.
        var archived = _archive.Load();
        var treatedMap = _treated?.Load();
        var result = new List<SessionSnapshot>(byId.Count);
        foreach (var s in byId.Values)
        {
            if (archived.Contains(s.SessionId)) continue;                                 // NET-04 : permanent, jamais réversible
            if (treatedMap is not null && treatedMap.ContainsKey(s.SessionId)) continue;  // traité => caché (réversible)
            result.Add(s);
        }
        return result;
    }

    /// <summary>
    /// OBS-01 — la MÊME lecture que <see cref="Read"/>, doublée de ce qu'elle a écarté et pourquoi.
    /// </summary>
    public LectureSessions Inspecter(System.DateTimeOffset now)
        => throw new System.NotImplementedException();

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
