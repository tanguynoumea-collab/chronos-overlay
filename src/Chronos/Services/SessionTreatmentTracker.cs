using System.Collections.Generic;

namespace Chronos.Services;

/// <summary>
/// Détecteur STATEFUL d'hystérésis des sessions « traitées » — logique PURE, testable sans aucune fenêtre.
/// Invoqué à chaque cycle avec les VAINQUEURS de l'arbitrage (les signaux retenus, chacun portant la source
/// qui a parlé) et l'horloge. Alimente et purge le <see cref="TreatedStore"/> :
///   • NET-01 (répondu) — TRT-01 : une transition OBSERVÉE, et seulement elle. Il faut TROIS conditions
///     réunies — la MÊME source a parlé aux deux cycles, elle disait une attente, et elle dit maintenant
///     un travail OBSERVÉ. Ce qui est exclu par construction : une source qui expire pendant qu'une autre
///     reprend la main (ce n'est pas l'utilisateur qui répond, c'est un relais entre deux sources), et un
///     état indéterminé lu comme une réponse (conclure d'une absence de lecture). La DISPARITION seule ne
///     déclenche toujours RIEN.
///   • NET-02 (acquitté par focus) : RETIRÉE en phase 21. Elle exigeait une session d'origine « app bureau »
///     ET un identifiant synthétique de cette source. Une session Claude Code a une origine ligne de commande
///     et un UUID : la branche ne l'atteignait JAMAIS, et le sondage de focus OS était payé à chaque tick
///     pour un résultat jamais lu. Code mort PROUVÉ, pas soupçonné.
///   • NET-03 (réapparition/purge) : INCHANGÉE. Une session en attente dont l'épisode d'attente courant est
///     PLUS RÉCENT que le treatedWaitingTs mémorisé est RETIRÉE du magasin (elle réapparaît).
///
/// <para>« Épisode d'attente » = l'instant que le SIGNAL porte, jamais celui du guetteur — c'est TRT-02.
/// Il reste STABLE tant que la source ne dit rien de plus récent, et n'avance que sur une demande neuve.
/// Cette datation est ce qui fait survivre le « traité » à un redémarrage de l'overlay : un détecteur neuf
/// qui redatait l'épisode à « maintenant » voyait un épisode plus récent que le traitement mémorisé et
/// purgeait aussitôt le magasin qu'il venait de lire — toutes les sessions traitées ressortaient. Les deux
/// écueils se tiennent de part et d'autre : un épisode redaté à chaque cycle purge à chaque tick, un
/// épisode figé à jamais empêche une session de revenir quand elle me redemande quelque chose.</para>
///
/// Le détecteur possède l'ajout ET la purge. Aucun type WPF.
/// </summary>
public sealed class SessionTreatmentTracker
{
    private readonly TreatedStore _store;

    // Dernier signal VU par session : son état ET sa source. La source est la moitié de l'information —
    // sans elle, « attente puis travail » ne distingue pas une réponse d'un relais entre deux sources.
    // Persiste tant que la session existe ; PAS réinitialisé sur absence, pour que NET-01 fonctionne quand
    // une session revient non-attente après avoir disparu un cycle.
    private readonly Dictionary<string, (SourceSession Source, SessionActivity Activite)> _dernier = new();

    // Instant (ms) de l'épisode d'attente courant, STABLE pendant l'épisode (il n'est posé qu'à son ouverture).
    private readonly Dictionary<string, long> _attenteDepuis = new();

    public SessionTreatmentTracker(TreatedStore store)
        => _store = store ?? throw new System.ArgumentNullException(nameof(store));

    // Trois valeurs disent « quelque chose m'attend ». L'attente DÉDUITE en fait partie depuis la phase 25 :
    // le bandeau la compte, le cadran la colore, et l'exclure ici faisait qu'une attente devenue déduite
    // était lue comme une réponse — c'est-à-dire masquée six heures. Une déduction reste une déduction ;
    // ce n'est pas une raison pour conclure qu'on y a répondu.
    private static bool EstAttente(SessionActivity a)
        => a is SessionActivity.WaitingTurn or SessionActivity.WaitingAttention
             or SessionActivity.WaitingDeduced;

    // Seul le TRAVAIL est une sortie d'attente OBSERVÉE. « Inconnu » n'affirme rien : un signal illisible
    // ou indéterminé lu comme une réponse, c'est conclure d'une absence de lecture. Sonde S2 du
    // 2026-09-12 : un seul cycle suffisait alors à masquer une session pour six heures.
    private static bool EstTravailObserve(SessionActivity a) => a is SessionActivity.Working;

    // L'instant d'un épisode est celui que le SIGNAL porte, jamais celui du guetteur — borné par l'instant
    // courant, parce qu'une source dont l'horloge avance daterait un épisode dans l'avenir que rien ne
    // pourrait plus périmer.
    private static long InstantDuSignal(SignalSession v, long nowMs)
        => System.Math.Min(v.Session.UpdatedAt.ToUnixTimeMilliseconds(), nowMs);

    /// <summary>
    /// Observe un cycle de signaux retenus (avec leur source) + horloge, et met à jour
    /// <see cref="TreatedStore"/> (ajout NET-01, purge NET-03).
    /// </summary>
    public void Observe(IReadOnlyList<SignalSession> vainqueurs, System.DateTimeOffset now)
    {
        var nowMs = now.ToUnixTimeMilliseconds();
        var traitees = _store.Load();   // une seule lecture par cycle (sert au test de réapparition NET-03)

        foreach (var v in vainqueurs)
        {
            var id = v.Session.SessionId;
            var etat = v.Session.Activity;
            var estAttente = EstAttente(etat);
            var connue = _dernier.TryGetValue(id, out var prec);

            // NET-01 (répondu) — TRT-01. Trois conditions, et il en faut TROIS : la MÊME source a parlé
            // aux deux cycles, elle disait une attente, elle dit maintenant un travail OBSERVÉ. Une source
            // qui expire pendant qu'une autre reprend la main n'est pas l'utilisateur qui répond.
            if (connue && prec.Source == v.Source && EstAttente(prec.Activite) && EstTravailObserve(etat))
            {
                _store.Set(id, _attenteDepuis.TryGetValue(id, out var ep) ? ep : InstantDuSignal(v, nowMs));
                _attenteDepuis.Remove(id);
            }

            // Épisode d'attente courant. Il ne suit PAS l'horloge du guetteur : il suit l'instant que la
            // source AFFIRME. Il n'avance donc que lorsque la source dit quelque chose de plus récent —
            // une nouvelle demande — et reste immobile tant qu'elle se tait.
            // Il n'est pas effacé par un changement de source : une attente qui continue est la même.
            if (estAttente)
            {
                var ep = InstantDuSignal(v, nowMs);
                if (!_attenteDepuis.TryGetValue(id, out var deja) || ep > deja) _attenteDepuis[id] = ep;
            }

            // NET-03 (réapparition/purge) : un épisode d'attente PLUS RÉCENT que le traitement mémorisé
            // signifie que la session me redemande quelque chose — elle revient.
            if (estAttente && traitees.TryGetValue(id, out var tts)
                           && _attenteDepuis.TryGetValue(id, out var cur) && cur > tts)
                _store.Remove(id);

            _dernier[id] = (v.Source, etat);
        }
    }
}
