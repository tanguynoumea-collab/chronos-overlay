using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Lit les fichiers d'état de session (*.json) écrits par les hooks (<see cref="SessionHookProcessor"/>) — dans
/// TOUTES les racines d'état : la vue réelle (%APPDATA%\Chronos\sessions) ET la vue du paquet de l'app bureau,
/// résolues par <see cref="RacinesEtat"/> (APP-06) — et les transcripts (<see cref="ISessionSource"/>), les qualifie
/// par les métadonnées de l'app bureau (<see cref="LecteurAppBureau"/>, APP-03 : la question posée à la fin d'un tour ;
/// APP-02 : le titre), et en produit des
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

    // Métadonnées par session de l'app bureau (APP-01) : nul par défaut = comportement v1.6 EXACT. La production le
    // donne par argument nommé, et une garde de source le vérifie (Piège 9 : un défaut nul rend l'oubli silencieux).
    private readonly LecteurAppBureau? _appBureau;

    // Ce que l'OS a au premier plan (LUE-02) : nul par défaut = LUE-02 inactive, LUE-01 seule. Même piège que le lecteur
    // (Piège 9) : la production la donne par argument nommé, sous garde de source.
    private readonly IPremierPlan? _premierPlan;

    /// <param name="sessionsDir">UNE racine d'état : le raccourci des tests, qui n'en ont qu'une.</param>
    /// <param name="dossiersEtat">La liste des racines d'état, dans l'ordre (APP-06) — celle que la production
    /// résout une fois par <see cref="RacinesEtat"/> : la vue du paquet de l'app bureau d'abord, la vue réelle
    /// ensuite. Donner les deux paramètres lève <see cref="System.ArgumentException"/> : deux façons de dire la
    /// même chose sont une ambiguïté, et le moniteur ne choisit pas en silence. N'en donner aucun revient aux
    /// candidats de la machine.</param>
    /// <param name="appBureau">Le lecteur des métadonnées par session de l'app bureau (APP-01), en LECTURE SEULE. Nul :
    /// la lecture est exactement celle de la v1.6 — aucune question, aucun titre.</param>
    /// <param name="premierPlan">La sonde du premier plan de l'OS (LUE-02), best-effort. Nulle : LUE-02 inactive, LUE-01
    /// seule — la production la donne par argument nommé, et une garde de source le vérifie (Piège 9).</param>
    public SessionMonitor(string? sessionsDir = null, ISessionSource? transcripts = null,
        ArchiveStore? archive = null,
        TreatedStore? treated = null, SessionTreatmentTracker? tracker = null,
        IReadOnlyList<string>? dossiersEtat = null, LecteurAppBureau? appBureau = null,
        IPremierPlan? premierPlan = null)
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
        _appBureau = appBureau;
        _premierPlan = premierPlan;
    }

    /// <summary>Le lecteur de l'app bureau reçu à la construction — celui du conteneur en production, que le rapport
    /// lit par <see cref="LectureSessions.AppBureau"/> (une seule instance, un seul cache : OBS-01).</summary>
    internal LecteurAppBureau? Lecteur => _appBureau;

    /// <summary>La sonde du premier plan reçue à la construction — celle du conteneur en production (miroir DI) ; le
    /// rapport lit ce qu'elle a vu par <see cref="LectureSessions.PremierPlan"/>, jamais par un second appel (OBS-01).</summary>
    internal IPremierPlan? PremierPlan => _premierPlan;

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
    /// FUSIONNE trois sources par session_id :
    ///   • transcripts (~/.claude/projects) — la base, universelle ;
    ///   • fichiers d'état des HOOKS — plus précis (permission), mais JAMAIS prioritaires du seul fait d'être
    ///     des hooks : c'est le signal le plus RÉCENT qui gagne (FUS-01). Ils sont lus dans TOUTES les racines
    ///     d'état (<see cref="Dossiers"/> : vue réelle ET vue du paquet de l'app bureau, résolues par
    ///     <see cref="RacinesEtat"/> — APP-06). Une même session vue dans deux racines est tranchée par
    ///     l'arbitrage, comme deux sources : jamais deux lignes, et un désaccord entre les deux est DIT ;
    ///   • questions de l'APP BUREAU (APP-03) — une fin de tour classée <c>blocked</c> avec un motif. L'app QUALIFIE
    ///     une ligne, elle n'en crée pas : sa question n'est déposée que pour une session déjà déposée CE cycle par un
    ///     transcript ou un hook, sous l'horizon d'abandon, non archivée dans l'app, datée par son épisode.
    /// Le TITRE de l'app n'est pas un signal : il est posé sur les retenus, APRÈS l'arbitrage, avant les filtres
    /// (les masquées le portent aussi) ; il ne départage rien (APP-02).
    /// La LECTURE (phase 30) : le détecteur reçoit le dernier focus de chaque session, la session sélectionnée
    /// et l'instant depuis lequel claude est au premier plan ; une session lue est inscrite dans treated.json et masquée
    /// dès ce cycle (le magasin est relu APRÈS l'observation).
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

        // 2.c) L'APP BUREAU (APP-03) QUALIFIE une ligne, elle n'en crée pas : seules les sessions déjà déposées CE cycle par
        //      un transcript ou un hook peuvent recevoir une question — périmètre SRC-01 et MaxSessions intacts, et
        //      dégradation v1.6 exacte par construction. Best-effort : une lecture ratée ne casse jamais le pipeline.
        //      La question porte l'identifiant et le DOSSIER de la source qui connaît la session (le plus récent de ses
        //      signaux ; à instant égal, le premier en ordre ordinal : l'ordre de collecte ne décide rien) — jamais le
        //      dossier de l'app : la ligne garde le nom que les sources d'activité lui donnent.
        LectureAppBureau? appBureau = null;
        if (_appBureau is not null) { try { appBureau = _appBureau.Lire(now); } catch { appBureau = null; } }
        if (appBureau is { DossierTrouve: true })
        {
            var projetConnu = new Dictionary<string, (string Projet, System.DateTimeOffset Instant)>(System.StringComparer.Ordinal);
            foreach (var s in signaux)
                if (!projetConnu.TryGetValue(s.Session.SessionId, out var deja)
                    || s.Session.UpdatedAt > deja.Instant
                    || (s.Session.UpdatedAt == deja.Instant && string.CompareOrdinal(s.Session.Project, deja.Projet) < 0))
                    projetConnu[s.Session.SessionId] = (s.Session.Project, s.Session.UpdatedAt);
            foreach (var (id, connu) in projetConnu)
                if (appBureau.ParSession.TryGetValue(id, out var m) && QuestionPosee(m, id, connu.Projet, now) is { } q)
                    signaux.Add(new SignalSession(SourceSession.AppBureau, AppliquerSilence(q, now)));
        }

        var arbitrage = ArbitrageSessions.Trancher(signaux);

        // 2.b) Le détecteur de traitement observe les snapshots RETENUS (+ horloge) et met à jour
        //      TreatedStore (ajout NET-01, ajout LUE, purge NET-03). Best-effort : ne casse JAMAIS le pipeline.
        //      Il observe un état arbitré — les VAINQUEURS, sans titre : le titre n'est pas une information d'état.
        //      LA LECTURE (LUE-01, LUE-02). Le premier plan est lu à CHAQUE cycle, même sans source app-bureau — le
        //      rapport doit pouvoir dire ce qu'il voit —, best-effort : une sonde qui lève vaut « indisponible », jamais
        //      une panne (LUE-04). Le contexte est bâti sur la lecture de l'app de CE cycle (OBS-01 : aucun second appel
        //      au lecteur) ; sans dossier de l'app il est NUL, et le détecteur est celui de la v1.6. Il ne reçoit du
        //      premier plan que l'instant depuis lequel claude y est.
        var premierPlan = EtatPremierPlan.NonBranche;
        if (_premierPlan is not null)
        {
            try { premierPlan = _premierPlan.Lire(now); }
            catch (System.Exception e) { premierPlan = new EtatPremierPlan(StatutPremierPlan.Indisponible, null, null, e.GetType().Name); }
        }
        ContexteLecture? contexte = appBureau is { DossierTrouve: true }
            ? new ContexteLecture(
                appBureau.ParSession.ToDictionary(kv => kv.Key, kv => kv.Value.DernierFocus, System.StringComparer.OrdinalIgnoreCase),
                appBureau.Selection?.CliSessionId,
                premierPlan.ClaudeDepuis)
            : null;
        try { _tracker?.Observe(arbitrage.Vainqueurs, now, contexte); } catch { }

        // 2.d) Le TITRE de l'app (APP-02) n'est pas un signal : posé sur les RETENUS, après l'arbitrage et avant les
        //      filtres — les masquées le portent aussi, le rapport les liste. Sans métadonnées, la liste est celle
        //      de l'arbitrage, inchangée.
        IReadOnlyList<SessionSnapshot> raw = appBureau is { DossierTrouve: true }
            ? arbitrage.Retenus.Select(s => Enrichir(s, appBureau)).ToList()
            : arbitrage.Retenus;

        // 3) Filtres : archivées (permanent, NET-04) PUIS traitées-en-attente (réversible). Le détecteur possède
        //    l'ajout ET la purge des entrées treated ; ici on MASQUE simplement toute session EN ATTENTE encore
        //    présente dans le magasin ; une session traitée qui travaille est visible — LUE-05.
        //    On NE ré-implémente PAS la comparaison treatedWaitingTs >= UpdatedAt : la réversibilité NET-03 est
        //    portée par le détecteur (purge sur nouvel épisode), pas par ce filtre.
        //    L'ORDRE d'évaluation est significatif : une session à la fois archivée et traitée est annoncée
        //    archivée, parce que c'est le geste de l'utilisateur qui prime sur une hystérésis automatique.
        //    Vient ENSUITE l'état indéterminé (LIB-01) : il n'a pas de ligne dans le widget, il est donc masqué
        //    ICI, avec son motif — et non au ViewModel, sans quoi Visibles cesserait d'être mot pour mot ce que
        //    l'écran affiche (OBS-01) et le rapport montrerait une ligne que le widget n'a pas. Ordre complet :
        //    archivée, puis traitée-en-attente, puis indéterminée (traitée ET indéterminée ⇒ Indeterminee). Le
        //    détecteur, lui, a observé les vainqueurs AVANT ces filtres (2.b) : il n'en est pas affecté.
        var archived = _archive.Load();
        var treatedMap = _treated?.Load();
        var visibles = new List<SessionSnapshot>(raw.Count);
        var masquees = new List<SessionMasquee>();
        foreach (var s in raw)
        {
            if (archived.Contains(s.SessionId)) { masquees.Add(new SessionMasquee(s, MotifMasquage.Archivee)); continue; }
            // LUE-05 — le masquage « traité » ne vaut que pour une ATTENTE : répondue, lue ou marquée traitée, une session qui se remet
            // à travailler s'affiche « Réflexion » ; son prochain épisode d'attente plus récent la ramène (NET-03, détecteur). Le filtre
            // ne purge rien. La CAUSE vient du détecteur (LUE-03) : treated.json ne la porte pas ; sans cause connue (geste, entrée
            // d'avant le démarrage non relue), le motif résiduel Traitee.
            if (treatedMap is not null && treatedMap.TryGetValue(s.SessionId, out var tts) && AffichageSessions.EstUneAttente(s.Activity))
            {
                var cause = _tracker?.CauseDe(s.SessionId, tts);
                masquees.Add(new SessionMasquee(s, cause?.Motif ?? MotifMasquage.Traitee, cause));
                continue;
            }
            if (!AffichageSessions.AUneLigne(s.Activity)) { masquees.Add(new SessionMasquee(s, MotifMasquage.Indeterminee)); continue; }
            visibles.Add(s);
        }
        return new LectureSessions(visibles, masquees, ecartesParAnciennete, arbitrage.Desaccords, appBureau, premierPlan);
    }

    /// <summary>La question posée par l'app, ou rien (APP-03). Toutes les conditions sont nécessaires (29-CONTEXT,
    /// verrouillé) : une fin de tour classée <c>blocked</c> ; un motif (<c>needs_action</c>) non blanc ; une session
    /// NON archivée dans l'app (l'app elle-même tait ses notifications pour elle) ; un instant connu et âgé d'au plus
    /// <see cref="HorizonsSessions.Abandon"/> — la MÊME borne incluse que les hooks et les transcripts.
    /// <para>L'instant est celui de l'ÉPISODE (<see cref="MetadonneesAppBureau.InstantClassification"/>, figé par le
    /// lecteur à la première apparition du résumé), jamais la dernière activité COURANTE : l'activité de fond qui
    /// continue après la fin du tour rajeunirait la question, et une session marquée traitée reviendrait sans avoir
    /// rien redemandé (TRT-02, NET-03). <c>completed</c>, <c>review_ready</c> et toute catégorie inconnue ne déposent
    /// rien.</para></summary>
    private static SessionSnapshot? QuestionPosee(MetadonneesAppBureau m, string id, string projet, System.DateTimeOffset now)
        => m.ClassificationFinDeTour == ClassificationFinDeTour.Bloquee
           && !string.IsNullOrWhiteSpace(m.MotifBlocage)
           && !m.Archivee
           && m.InstantClassification is { } t
           && now - t <= HorizonsSessions.Abandon
            ? new SessionSnapshot(id, projet, SessionActivity.WaitingAttention, m.MotifBlocage, t)
            : null;

    /// <summary>Le titre n'est pas un signal : posé sur un RETENU, après l'arbitrage, il ne départage rien (APP-02).
    /// Un titre blanc n'est pas un titre.</summary>
    private static SessionSnapshot Enrichir(SessionSnapshot s, LectureAppBureau l)
        => l.ParSession.TryGetValue(s.SessionId, out var m) && !string.IsNullOrWhiteSpace(m.Titre) ? s with { Titre = m.Titre } : s;

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
