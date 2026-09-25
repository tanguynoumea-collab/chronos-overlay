using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Lit les fichiers d'état de session (*.json) écrits par les hooks (<see cref="SessionHookProcessor"/>) — dans
/// TOUTES les racines d'état : la vue réelle (%APPDATA%\Chronos\sessions) ET la vue du paquet de l'app bureau,
/// résolues par <see cref="RacinesEtat"/> (APP-06) — et les transcripts (<see cref="ISessionSource"/>), et en produit des
/// <see cref="SessionSnapshot"/>, en appliquant une politique d'HONNÊTETÉ sur la fraîcheur — la MÊME pour
/// toutes les sources (SIL-01), avec les seuils de <see cref="HorizonsSessions"/> :
///   • Working dont le dernier signal — battement de hook ou dernier message de transcript — date de plus
///     de <see cref="HorizonsSessions.Silence"/> → <see cref="SessionActivity.WaitingDeduced"/> : on ne
///     prétend pas « en travail » quand plus rien n'arrive, et on ne prétend pas davantage que le tour s'est
///     terminé — personne ne l'a vu. C'est ICI (<see cref="AppliquerSilence"/>), et nulle part ailleurs, que
///     l'attente déduite est produite, appliquée à tous les signaux avant l'arbitrage : elle est dérivée à la
///     LECTURE, jamais écrite dans un fichier d'état (EVT-04), et la source transcripts ne déduit rien.
///   • Les états d'attente PERSISTENT (le fichier ne bouge pas TANT QU'on n'a pas agi — c'est justement
///     le signal). Ils ne deviennent « périmés » qu'au-delà de <see cref="HorizonsSessions.Abandon"/>
///     (session morte, SessionEnd manqué) → ignorés, fichier de hook comme transcript.
/// Lecture TOLÉRANTE : fichier absent/corrompu → ignoré. Aucun type WPF (couche neutre).
/// </summary>
public sealed class SessionMonitor
{
    private readonly IReadOnlyList<string> _dossiers;
    private readonly ISessionSource _transcripts;
    private readonly ArchiveStore _archive;

    // Hystérésis (phase 14, réduite en phase 21) : nuls par défaut = fonctionnalité désactivée. Quand ils
    // sont fournis, le détecteur observe les snapshots BRUTS et alimente TreatedStore ; le filtre masque
    // ensuite les sessions traitées.
    private readonly TreatedStore? _treated;
    private readonly SessionTreatmentTracker? _tracker;

    /// <param name="sessionsDir">UNE racine d'état : le raccourci des tests, qui n'en ont qu'une.</param>
    /// <param name="dossiersEtat">La liste des racines d'état, dans l'ordre (APP-06) — celle que la production
    /// résout une fois par <see cref="RacinesEtat"/> : la vue du paquet de l'app bureau d'abord, la vue réelle
    /// ensuite. Donner les deux paramètres lève <see cref="System.ArgumentException"/> : deux façons de dire la
    /// même chose sont une ambiguïté, et le moniteur ne choisit pas en silence. N'en donner aucun revient aux
    /// candidats de la machine.</param>
    public SessionMonitor(string? sessionsDir = null, ISessionSource? transcripts = null,
        ArchiveStore? archive = null,
        TreatedStore? treated = null, SessionTreatmentTracker? tracker = null,
        IReadOnlyList<string>? dossiersEtat = null)
    {
        if (sessionsDir is not null && dossiersEtat is not null)
            throw new System.ArgumentException("Les racines des fichiers d'état se donnent d'UNE façon : un dossier (sessionsDir) OU la liste des racines (dossiersEtat).", nameof(dossiersEtat));

        // Copie défensive : la liste reçue ne peut plus changer sous le moniteur.
        _dossiers = dossiersEtat?.ToArray()
                    ?? (sessionsDir is not null ? new[] { sessionsDir } : RacinesEtat.ParDefaut().EtatsHooks.ToArray());
        _transcripts = transcripts ?? new TranscriptSessionSource();
        _archive = archive ?? new ArchiveStore();
        _treated = treated;
        _tracker = tracker;
    }

    /// <summary>Les racines d'état lues à chaque cycle, dans l'ordre. Une propriété SINGULIÈRE mentirait : le
    /// moniteur en lit plusieurs (APP-06). Le balayage CYC-01 et le rapport de diagnostic passent sur CES racines,
    /// jamais sur un chemin déduit dans leur coin.</summary>
    public IReadOnlyList<string> Dossiers => _dossiers;

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
    ///   • fichiers d'état des HOOKS — plus précis (permission), mais JAMAIS prioritaires du seul fait d'être
    ///     des hooks : c'est le signal le plus RÉCENT qui gagne (FUS-01). Ils sont lus dans TOUTES les racines
    ///     d'état (<see cref="Dossiers"/> : vue réelle ET vue du paquet de l'app bureau, résolues par
    ///     <see cref="RacinesEtat"/> — APP-06). Une même session vue dans deux racines est tranchée par
    ///     l'arbitrage, comme deux sources : jamais deux lignes, et un désaccord entre les deux est DIT.
    /// Puis applique les filtres, EN RENDANT COMPTE de chacun au lieu de jeter en silence.
    /// </summary>
    public LectureSessions Inspecter(System.DateTimeOffset now)
    {
        // 1 & 2) COLLECTE. Les deux sources déposent leurs signaux dans une même liste, et l'ordre de cette
        //        collecte n'a plus AUCUNE conséquence (FUS-01) : c'est ArbitrageSessions qui tranche, sur la
        //        FRAÎCHEUR. Avant ce plan, chaque source réécrivait une entrée indexée par identifiant — le
        //        dernier passage gagnait, donc l'ordre du code faisait loi, et un signal de 7 heures battait
        //        un signal de 10 secondes.
        //        Chaque signal passe par la règle de silence AVANT d'être déposé (SIL-01) : même position
        //        qu'avant la phase 28 pour les hooks, et désormais la même pour les transcripts.
        var signaux = new List<SignalSession>();

        foreach (var t in _transcripts.Read(now))
            signaux.Add(new SignalSession(SourceSession.Transcript, AppliquerSilence(t, now)));

        // Un fichier écarté pour son ÂGE est un fait observable, pas un non-événement : c'est l'écart entre
        // « 54 fichiers sur disque » et « 1 ligne à l'écran ». Un fichier ILLISIBLE n'est pas compté ici —
        // il n'a pas été écarté pour son âge, il n'a pas été lu. Le compte est CUMULÉ sur toutes les racines.
        var ecartesParAnciennete = 0;

        // APP-06 — chaque racine d'état est lue. L'overlay, lancé hors de l'arbre de l'app bureau, ne voit pas la
        // vue virtualisée où les hooks de l'app écrivent : sans cette boucle, il n'en lisait jamais un fichier.
        // L'EXISTENCE est testée ici, à chaque cycle : un dossier créé après le démarrage est lu dès qu'il existe.
        // Une racine absente ou illisible ne coûte rien aux autres. Dans l'arbre de l'app, deux racines peuvent
        // montrer les mêmes fichiers : les doublons s'y tranchent comme deux sources d'accord (aucun désaccord).
        foreach (var racine in _dossiers)
        {
            string[] files;
            try { files = System.IO.Directory.Exists(racine) ? System.IO.Directory.GetFiles(racine, "*.json") : System.Array.Empty<string>(); }
            catch { files = System.Array.Empty<string>(); }

            foreach (var f in files)
            {
                var snap = TryRead(f, now, out var perimee);
                if (perimee) { ecartesParAnciennete++; continue; }

                // Un FRAGMENT — la contrepartie assumée de l'écriture directe de la phase 23, qui tronque la
                // cible avant de la réécrire — rend null SANS être périmé. C'est une ABSENCE de signal, et elle
                // se traite comme telle : le fragment n'est pas déposé. Le compter comme un signal sans date en
                // ferait un « très vieux signal » perdant contre n'importe quoi — une fausse déposition, là où
                // il n'y a rien à déposer.
                if (snap is not null) signaux.Add(new SignalSession(SourceSession.Hook, AppliquerSilence(snap, now)));
            }
        }

        var arbitrage = ArbitrageSessions.Trancher(signaux);

        // 2.b) Le détecteur de traitement observe les snapshots RETENUS (+ horloge) et met à jour
        //      TreatedStore (ajout NET-01, purge NET-03). Best-effort : ne casse JAMAIS le pipeline. Sa
        //      logique n'est pas touchée ici ; il observe simplement, désormais, un état arbitré.
        var raw = arbitrage.Retenus;
        try { _tracker?.Observe(arbitrage.Vainqueurs, now); } catch { }

        // 3) Filtres : archivées (permanent, NET-04) PUIS traitées (réversible). Le détecteur possède l'ajout ET
        //    la purge des entrées treated ; ici on MASQUE simplement toute session encore présente dans le magasin.
        //    On NE ré-implémente PAS la comparaison treatedWaitingTs >= UpdatedAt : la réversibilité NET-03 est
        //    portée par le détecteur (purge sur nouvel épisode), pas par ce filtre.
        //    L'ORDRE d'évaluation est significatif : une session à la fois archivée et traitée est annoncée
        //    archivée, parce que c'est le geste de l'utilisateur qui prime sur une hystérésis automatique.
        //    Vient ENSUITE l'état indéterminé (LIB-01) : il n'a pas de ligne dans le widget, il est donc masqué
        //    ICI, avec son motif — et non au ViewModel, sans quoi Visibles cesserait d'être mot pour mot ce que
        //    l'écran affiche (OBS-01) et le rapport montrerait une ligne que le widget n'a pas. Ordre complet :
        //    archivée, puis traitée, puis indéterminée. Le détecteur, lui, a observé les vainqueurs AVANT ces
        //    filtres (2.b) : il n'en est pas affecté.
        var archived = _archive.Load();
        var treatedMap = _treated?.Load();
        var visibles = new List<SessionSnapshot>(raw.Count);
        var masquees = new List<SessionMasquee>();
        foreach (var s in raw)
        {
            if (archived.Contains(s.SessionId)) { masquees.Add(new SessionMasquee(s, MotifMasquage.Archivee)); continue; }
            if (treatedMap is not null && treatedMap.ContainsKey(s.SessionId)) { masquees.Add(new SessionMasquee(s, MotifMasquage.Traitee)); continue; }
            if (!AffichageSessions.AUneLigne(s.Activity)) { masquees.Add(new SessionMasquee(s, MotifMasquage.Indeterminee)); continue; }
            visibles.Add(s);
        }
        return new LectureSessions(visibles, masquees, ecartesParAnciennete, arbitrage.Desaccords);
    }

    /// <summary>LA RÈGLE DE SILENCE, en un seul point (SIL-01) : un TRAVAIL dont le signal — battement de hook ou dernier
    /// message de transcript — est plus vieux que <see cref="HorizonsSessions.Silence"/> devient une attente DÉDUITE.
    /// On ne sait plus ; la déduction que l'inférence AUTORISE est « quelque chose m'attend » (l'interruption au clavier
    /// est le cas le plus fréquent, et aucun événement ne l'émet). Un terminal tué ou une mise en veille produisent le
    /// même silence : on ne nomme pas la cause, et le mot porte son point d'interrogation. WaitingTurn est interdit ici —
    /// « le tour s'est terminé » est une observation, et personne ne l'a faite. Les attentes OBSERVÉES persistent, un état
    /// illisible reste indéterminé. Appliquée AVANT l'arbitrage, à toutes les sources : la source transcripts ne déduit
    /// rien elle-même (une garde le vérifie).
    /// <para>Depuis EVT-03, ce seuil ne devine plus combien de temps un travail peut durer : il mesure le SILENCE. Une
    /// session qui travaille est réaffirmée à chaque appel d'outil ; entre le signal d'entrée et le signal de sortie d'un
    /// outil long, il ne se passe rien, donc le seuil doit rester large. La contrepartie — la latence avant que le
    /// silence se voie — est écrite dans le contrat des hooks (EVT-05), pas tue. L'instant du signal n'est pas réécrit :
    /// seule l'activité change.</para></summary>
    private static SessionSnapshot AppliquerSilence(SessionSnapshot s, System.DateTimeOffset now)
        => s.Activity == SessionActivity.Working && now - s.UpdatedAt > HorizonsSessions.Silence
            ? s with { Activity = SessionActivity.WaitingDeduced }
            : s;

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
            if (age > HorizonsSessions.Abandon) { perimee = true; return null; } // session morte (SessionEnd manqué) → on ne l'affiche plus

            // Le silence n'est plus appliqué ici : il l'est dans Inspecter, par AppliquerSilence, à TOUS les signaux.
            return new SessionSnapshot(sid, project, activity, reason, updatedAt);
        }
        catch { return null; }
    }

    private static string Str(JsonElement o, string key)
        => o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
