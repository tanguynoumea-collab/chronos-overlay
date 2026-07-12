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
    private readonly ISessionSource? _desktop;

    // Hystérésis (Phase 14) — AJOUTÉS EN FIN, tous nuls par défaut → non-régression totale (null = fonctionnalité
    // désactivée, aucun défaut instancié). Quand fournis : le tracker observe les snapshots BRUTS et alimente
    // TreatedStore ; le filtre `treated` masque les sessions traitées. `foreground` reste null tant que le plan 02
    // n'a pas câblé le focus RÉEL → branche NET-02 dormante mais présente.
    private readonly TreatedStore? _treated;
    private readonly SessionTreatmentTracker? _tracker;
    private readonly IForegroundWatch? _foreground;

    // Le paramètre `desktop` est AJOUTÉ EN FIN avec une valeur par défaut nulle → tous les appels
    // existants (App.xaml.cs, tests) restent valides (non cassant). Quand il est fourni, la source
    // BUREAU (UIA) est fusionnée dans Read APRÈS transcripts + hooks.
    public SessionMonitor(string? sessionsDir = null, TranscriptSessionSource? transcripts = null, ArchiveStore? archive = null, ISessionSource? desktop = null,
        TreatedStore? treated = null, SessionTreatmentTracker? tracker = null, IForegroundWatch? foreground = null)
    {
        _dir = sessionsDir ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Chronos", "sessions");
        _transcripts = transcripts ?? new TranscriptSessionSource();
        _archive = archive ?? new ArchiveStore();
        _desktop = desktop;
        _treated = treated;
        _tracker = tracker;
        _foreground = foreground;
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

        // 2.b) Source BUREAU (UIA) : REPLI anti-doublon. Les transcripts (étape 1) couvrent DÉJÀ l'app bureau
        //      — l'app écrit ses transcripts dans ~/.claude/projects. La source UIA verrait donc les MÊMES
        //      sessions, mais sous des clés SYNTHÉTIQUES `desktop:...` disjointes des UUID (aucune clé commune,
        //      noms différents « projet » vs « titre de session ») → la fusion additive comptait chaque session
        //      locale DEUX FOIS. On ne fusionne donc l'UIA QUE si aucune session locale (transcript/hook) n'est
        //      visible — cas d'un Cowork VM pur (distant, sans transcript local). Lecture NON bloquante (cache
        //      du poll de fond, ROB-07) ; ne casse JAMAIS le pipeline → try/catch tolérant.
        if (_desktop is not null && byId.Count == 0)
        {
            try
            {
                foreach (var d in _desktop.Read(now)) byId[d.SessionId] = d;
            }
            catch { /* la source bureau ne casse jamais le pipeline des sessions CLI */ }
        }

        // 2.c) Hystérésis (Phase 14) : le tracker observe les snapshots BRUTS fusionnés (+ focus + now) et met à
        //      jour TreatedStore (ajout NET-01/NET-02, purge NET-03). Best-effort : ne casse JAMAIS le pipeline.
        var raw = byId.Values.ToList();
        bool foreground = false;
        try { foreground = _foreground?.IsClaudeForeground() ?? false; } catch { foreground = false; }
        try { _tracker?.Observe(raw, foreground, now); } catch { }

        // 3) Filtres : archivées (permanent, NET-04) PUIS traitées (réversible). Le tracker possède l'ajout ET la
        //    purge des entrées treated ; ici on MASQUE simplement toute session encore présente dans le magasin.
        //    On NE ré-implémente PAS la comparaison treatedWaitingTs >= UpdatedAt : elle serait FAUSSE pour les
        //    sessions bureau (UpdatedAt == now à chaque poll → réapparition à chaque tick). La réversibilité NET-03
        //    est portée par le tracker (purge sur nouvel épisode), pas par ce filtre.
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
