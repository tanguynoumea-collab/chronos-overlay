using System.Collections.Generic;

namespace Chronos.Services;

/// <summary>
/// Détecteur STATEFUL d'hystérésis des sessions « traitées » — logique PURE, testable sans aucune fenêtre.
/// Invoqué à chaque cycle avec les snapshots BRUTS fusionnés (avant filtre) et l'horloge. Alimente et purge
/// le <see cref="TreatedStore"/> :
///   • NET-01 (répondu) : une session vue « en attente » au cycle précédent et PRÉSENTE et non-attente
///     maintenant (Working/Unknown) est marquée traitée. La DISPARITION seule ne déclenche RIEN (on garde
///     le dernier état connu).
///   • NET-02 (acquitté par focus) : RETIRÉE en phase 21. Elle exigeait une session d'origine « app bureau »
///     ET un identifiant synthétique de cette source. Une session Claude Code a une origine ligne de commande
///     et un UUID : la branche ne l'atteignait JAMAIS, et le sondage de focus OS était payé à chaque tick
///     pour un résultat jamais lu. Code mort PROUVÉ, pas soupçonné.
///   • NET-03 (réapparition/purge) : une session en attente dont l'épisode d'attente courant est PLUS RÉCENT
///     que le treatedWaitingTs mémorisé est RETIRÉE du magasin (elle réapparaît).
///
/// « Épisode d'attente » = horodatage du passage EN attente depuis un état non-attente, STABLE tant que la
/// session reste en attente. Cette stabilité est indispensable : si l'épisode se redatait à chaque cycle,
/// NET-03 purgerait le magasin à chaque tick et rien ne resterait jamais masqué. Le détecteur maintient donc
/// lui-même l'horodatage d'épisode et possède l'ajout ET la purge. Aucun type WPF.
/// </summary>
public sealed class SessionTreatmentTracker
{
    private readonly TreatedStore _store;

    // Dernier état VU par session (persiste tant que la session existe ; PAS réinitialisé sur absence, pour
    // que NET-01 fonctionne quand une session revient non-attente après avoir disparu un cycle).
    private readonly Dictionary<string, SessionActivity> _lastActivity = new();

    // Horodatage (ms) de l'épisode d'attente courant, stable pendant l'épisode.
    private readonly Dictionary<string, long> _waitingSince = new();

    public SessionTreatmentTracker(TreatedStore store)
        => _store = store ?? throw new System.ArgumentNullException(nameof(store));

    private static bool IsWaiting(SessionActivity a)
        => a is SessionActivity.WaitingTurn or SessionActivity.WaitingAttention;

    /// <summary>
    /// Observe un cycle de snapshots bruts fusionnés + horloge, et met à jour <see cref="TreatedStore"/>
    /// (ajout NET-01, purge NET-03).
    /// </summary>
    public void Observe(IReadOnlyList<SignalSession> vainqueurs, System.DateTimeOffset now)
    {
        var nowMs = now.ToUnixTimeMilliseconds();
        var treated = _store.Load(); // une seule lecture par cycle (sert au test de réapparition NET-03)

        foreach (var v in vainqueurs)
        {
            var s = v.Session;
            var id = s.SessionId;
            var isWaiting = IsWaiting(s.Activity);
            var wasWaiting = _lastActivity.TryGetValue(id, out var prev) && IsWaiting(prev);

            // NET-01 (répondu) : présente et passée d'attente → non-attente ce cycle → traitée.
            if (wasWaiting && !isWaiting)
            {
                _store.Set(id, _waitingSince.TryGetValue(id, out var ep) ? ep : s.UpdatedAt.ToUnixTimeMilliseconds());
                _waitingSince.Remove(id);
            }

            // Suivi de l'épisode d'attente courant (horodatage STABLE pendant l'épisode).
            if (isWaiting && !wasWaiting) _waitingSince[id] = nowMs;

            // NET-03 (réapparition/purge) : épisode d'attente courant PLUS RÉCENT que le traitement mémorisé.
            if (isWaiting && treated.TryGetValue(id, out var tts)
                          && _waitingSince.TryGetValue(id, out var cur) && cur > tts)
                _store.Remove(id);

            _lastActivity[id] = s.Activity;
        }
    }
}
