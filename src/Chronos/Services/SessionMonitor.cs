using System.Collections.Generic;
using System.IO;
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
    /// Ce que le widget AFFICHE. Simple PROJECTION d'<see cref="Inspecter"/> : il n'existe qu'une
    /// implémentation des filtres dans ce fichier, donc aucun consommateur — widget ou rapport — ne peut
    /// décrire un système différent de celui qui tourne. C'est la condition d'OBS-01 : partager une
    /// instance ne suffirait pas si chaque appelant refaisait le tri dans son coin.
    /// </summary>
    public IReadOnlyList<SessionSnapshot> Read(System.DateTimeOffset now) => Inspecter(now).Visibles;

    /// <summary>
    /// OBS-01 — la MÊME lecture que <see cref="Read"/>, doublée de ce qu'elle a écarté et pourquoi.
    /// FUSIONNE deux sources par session_id :
    ///   • transcripts (~/.claude/projects) — la base, universelle ;
    ///   • fichiers d'état des HOOKS (%APPDATA%\Chronos\sessions) — plus précis (permission),
    ///     mais JAMAIS prioritaires du seul fait d'être des hooks : c'est le signal le plus RÉCENT
    ///     qui gagne (FUS-01).
    /// Puis applique les filtres, EN RENDANT COMPTE de chacun au lieu de jeter en silence.
    /// </summary>
    public LectureSessions Inspecter(System.DateTimeOffset now)
    {
        // 1 & 2) COLLECTE. Les deux sources déposent leurs signaux dans une même liste, et l'ordre de cette
        //        collecte n'a plus AUCUNE conséquence (FUS-01) : c'est ArbitrageSessions qui tranche, sur la
        //        FRAÎCHEUR. Avant ce plan, chaque source réécrivait une entrée indexée par identifiant — le
        //        dernier passage gagnait, donc l'ordre du code faisait loi, et un signal de 7 heures battait
        //        un signal de 10 secondes.
        var signaux = new List<SignalSession>();

        foreach (var t in _transcripts.Read(now))
            signaux.Add(new SignalSession(SourceSession.Transcript, t));

        string[] files;
        try { files = System.IO.Directory.Exists(_dir) ? System.IO.Directory.GetFiles(_dir, "*.json") : System.Array.Empty<string>(); }
        catch { files = System.Array.Empty<string>(); }

        // Un fichier écarté pour son ÂGE est un fait observable, pas un non-événement : c'est l'écart entre
        // « 54 fichiers sur disque » et « 1 ligne à l'écran ». Un fichier ILLISIBLE n'est pas compté ici —
        // il n'a pas été écarté pour son âge, il n'a pas été lu.
        var ecartesParAnciennete = 0;
        foreach (var f in files)
        {
            var snap = TryRead(f, now, out var perimee);
            if (perimee) { ecartesParAnciennete++; continue; }

            // Un FRAGMENT — la contrepartie assumée de l'écriture directe de la phase 23, qui tronque la
            // cible avant de la réécrire — rend null SANS être périmé. C'est une ABSENCE de signal, et elle
            // se traite comme telle : le fragment n'est pas déposé. Le compter comme un signal sans date en
            // ferait un « très vieux signal » perdant contre n'importe quoi — une fausse déposition, là où
            // il n'y a rien à déposer.
            if (snap is not null) signaux.Add(new SignalSession(SourceSession.Hook, snap));
        }

        var arbitrage = ArbitrageSessions.Trancher(signaux);

        // 2.b) Le détecteur de traitement observe les snapshots RETENUS (+ horloge) et met à jour
        //      TreatedStore (ajout NET-01, purge NET-03). Best-effort : ne casse JAMAIS le pipeline. Sa
        //      logique n'est pas touchée ici ; il observe simplement, désormais, un état arbitré.
        var raw = arbitrage.Retenus;
        try { _tracker?.Observe(raw, now); } catch { }

        // 3) Filtres : archivées (permanent, NET-04) PUIS traitées (réversible). Le détecteur possède l'ajout ET
        //    la purge des entrées treated ; ici on MASQUE simplement toute session encore présente dans le magasin.
        //    On NE ré-implémente PAS la comparaison treatedWaitingTs >= UpdatedAt : la réversibilité NET-03 est
        //    portée par le détecteur (purge sur nouvel épisode), pas par ce filtre.
        //    L'ORDRE d'évaluation est significatif : une session à la fois archivée et traitée est annoncée
        //    archivée, parce que c'est le geste de l'utilisateur qui prime sur une hystérésis automatique.
        var archived = _archive.Load();
        var treatedMap = _treated?.Load();
        var visibles = new List<SessionSnapshot>(raw.Count);
        var masquees = new List<SessionMasquee>();
        foreach (var s in raw)
        {
            if (archived.Contains(s.SessionId)) { masquees.Add(new SessionMasquee(s, MotifMasquage.Archivee)); continue; }
            if (treatedMap is not null && treatedMap.ContainsKey(s.SessionId)) { masquees.Add(new SessionMasquee(s, MotifMasquage.Traitee)); continue; }
            visibles.Add(s);
        }
        return new LectureSessions(visibles, masquees, ecartesParAnciennete, arbitrage.Desaccords);
    }

    // <paramref name="perimee"/> distingue « lu, mais trop vieux pour valoir quelque chose » de
    // « illisible » : les deux rendent null, et les confondre effacerait l'information qui explique
    // l'essentiel de l'écart entre le disque et l'écran.
    private static SessionSnapshot? TryRead(string file, System.DateTimeOffset now, out bool perimee)
    {
        perimee = false;
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
            if (age > DropAfter) { perimee = true; return null; } // session morte (SessionEnd manqué) → on ne l'affiche plus

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
