using System.Collections.Generic;

namespace Chronos.Services;

/// <summary>
/// Détecteur STATEFUL d'hystérésis des sessions « traitées » — logique PURE, testable sans fenêtre Claude
/// ni UIA. Invoqué à chaque cycle avec les snapshots BRUTS fusionnés (avant filtre), l'état du focus OS et
/// l'horloge. Alimente et purge le <see cref="TreatedStore"/> :
///   • NET-01 (répondu) : une session vue « en attente » au cycle précédent et PRÉSENTE et non-attente
///     maintenant (Working/Unknown) est marquée traitée. La DISPARITION seule ne déclenche RIEN (on garde
///     le dernier état connu).
///   • NET-02 (acquitté par focus) : une session bureau PREMIER PLAN (desktop:foreground:…) actuellement en
///     attente, avec la fenêtre Claude au premier plan de façon CONTINUE depuis ≥ <see cref="FocusAckDelay"/>,
///     est marquée traitée. Toute interruption (focus perdu ou plus en attente) remet le compteur à zéro
///     (debounce anti-survol).
///   • NET-03 (réapparition/purge) : une session en attente dont l'épisode d'attente courant est PLUS RÉCENT
///     que le treatedWaitingTs mémorisé est RETIRÉE du magasin (elle réapparaît).
///
/// « Épisode d'attente » = horodatage du passage EN attente depuis un état non-attente, STABLE tant que la
/// session reste en attente. Indispensable : le snapshot bureau porte UpdatedAt == now à chaque poll → on ne
/// peut PAS dater l'épisode via UpdatedAt (sinon réapparition à chaque tick, cf. décision CONTEXT.md). Le
/// tracker maintient donc lui-même l'horodatage d'épisode et possède l'ajout ET la purge. Aucun type WPF.
/// </summary>
public sealed class SessionTreatmentTracker
{
    // Debounce NET-02 : durée de focus continu (+ attente) requise pour acquitter (constante ajustable).
    private static readonly System.TimeSpan FocusAckDelay = System.TimeSpan.FromSeconds(2.5);

    private readonly TreatedStore _store;

    // Dernier état VU par session (persiste tant que la session existe ; PAS réinitialisé sur absence, pour
    // que NET-01 fonctionne quand une session revient non-attente après avoir disparu un cycle).
    private readonly Dictionary<string, SessionActivity> _lastActivity = new();

    // Horodatage (ms) de l'épisode d'attente courant, stable pendant l'épisode.
    private readonly Dictionary<string, long> _waitingSince = new();

    // Début de la tenue continue « focus + attente » (branche NET-02).
    private readonly Dictionary<string, System.DateTimeOffset> _focusSince = new();

    public SessionTreatmentTracker(TreatedStore store)
        => _store = store ?? throw new System.ArgumentNullException(nameof(store));

    private static bool IsWaiting(SessionActivity a)
        => a is SessionActivity.WaitingTurn or SessionActivity.WaitingAttention;

    private static bool IsForegroundDesktop(SessionSnapshot s)
        => s.Origin == SessionOrigin.Desktop
           && s.SessionId.StartsWith("desktop:foreground:", System.StringComparison.Ordinal);

    /// <summary>
    /// Observe un cycle de snapshots bruts fusionnés + focus + horloge, et met à jour <see cref="TreatedStore"/>
    /// (ajout NET-01/NET-02, purge NET-03).
    /// </summary>
    public void Observe(IReadOnlyList<SessionSnapshot> raw, bool claudeForeground, System.DateTimeOffset now)
    {
        var nowMs = now.ToUnixTimeMilliseconds();
        var treated = _store.Load(); // une seule lecture par cycle (sert au test de réapparition NET-03)

        foreach (var s in raw)
        {
            var id = s.SessionId;
            var isWaiting = IsWaiting(s.Activity);
            var wasWaiting = _lastActivity.TryGetValue(id, out var prev) && IsWaiting(prev);

            // NET-01 (répondu) : présente et passée d'attente → non-attente ce cycle → traitée.
            if (wasWaiting && !isWaiting)
            {
                _store.Set(id, _waitingSince.TryGetValue(id, out var ep) ? ep : s.UpdatedAt.ToUnixTimeMilliseconds());
                _waitingSince.Remove(id);
                _focusSince.Remove(id);
            }

            // Suivi d'épisode d'attente + branche NET-02 (acquittement par focus).
            if (isWaiting)
            {
                if (!wasWaiting) _waitingSince[id] = nowMs; // nouvel épisode d'attente → horodatage stable

                if (claudeForeground && IsForegroundDesktop(s))
                {
                    // Tenue continue focus+attente : démarre le compteur, puis acquitte au-delà du debounce.
                    if (!_focusSince.ContainsKey(id)) _focusSince[id] = now;
                    else if (now - _focusSince[id] >= FocusAckDelay) _store.Set(id, _waitingSince[id]);
                }
                else
                {
                    _focusSince.Remove(id); // pas de focus (ou pas premier-plan) → reset du compteur (anti-survol)
                }
            }
            else
            {
                _focusSince.Remove(id); // plus en attente → reset du compteur NET-02
            }

            // NET-03 (réapparition/purge) : épisode d'attente courant PLUS RÉCENT que le traitement mémorisé.
            if (isWaiting && treated.TryGetValue(id, out var tts)
                          && _waitingSince.TryGetValue(id, out var cur) && cur > tts)
                _store.Remove(id);

            _lastActivity[id] = s.Activity;
        }
    }
}
