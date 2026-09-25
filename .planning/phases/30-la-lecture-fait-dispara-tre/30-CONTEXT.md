# Phase 30 : La lecture fait disparaître — Contexte

**Gathered:** 2026-09-25
**Status:** Ready for planning (après les phases 28 et 29)
**Source:** décisions de l'utilisateur (« lance tout ») + relevé de phase 27 (gestes A et B) + REQUIREMENTS.md (LUE-01/LUE-02 ajustées le 2026-09-25)

<domain>
## Phase Boundary

Une session **lue** quitte le widget d'elle-même. Cette phase consomme `lastFocusedAt` (phase 29) et la forme
de LUE-02 fixée par le relevé (phase 27), et s'applique à la population élargie par SIL-01 (phase 28). Elle
n'ajoute ni source ni libellé.

Ce que dit l'utilisateur, ligne 3 de son tableau : « session qui a fini de réfléchir et qui est lue :
n'apparaît pas ».

</domain>

<decisions>
## Implementation Decisions

### La règle LUE-01 — VERROUILLÉE par le relevé
- Une session en attente (WaitingTurn, WaitingAttention, question posée, `blocked`) est **lue** si
  `instantAttente < dernierFocus`, où :
  - `instantAttente` = l'instant que le signal RETENU porte (`SessionSnapshot.UpdatedAt` après la phase 28 :
    `timestamp` du dernier message significatif du transcript, ou `updated_at` du hook, ou `lastActivityAt`
    pour `blocked`) ;
  - `dernierFocus` = `lastFocusedAt` de la session (phase 29), via `cliSessionId`.
- Effet : `TreatedStore.Set(id, instantAttente)` — le magasin réversible EXISTANT, épisode daté par le signal
  (TRT-02). NET-03 inchangé : un signal d'attente plus récent que l'épisode traité fait revenir la session.
- Pas d'écriture à chaque cycle : `Set` seulement si la session n'est pas déjà traitée pour cet épisode
  (comparer à la valeur mémorisée avant d'écrire).
- Où : dans `SessionTreatmentTracker.Observe` (le détecteur possède déjà l'ajout et la purge), qui reçoit
  désormais aussi le dernier focus par session (dictionnaire `id → DateTimeOffset?`) et l'état du premier plan.

### La règle LUE-02 — CONFIRMÉE NÉCESSAIRE par le geste B, forme fixée
- La session **sélectionnée** = celle dont `lastFocusedAt` est le plus récent parmi TOUS les fichiers de l'app
  (pas seulement celles affichées).
- Elle compte comme lue tant que la fenêtre au premier plan appartient au processus **`claude`**, avec un
  délai de grâce de **2,5 s** après `instantAttente` (le regard a le temps de se poser).
- Détection Win32 : `GetForegroundWindow()` → `GetWindowThreadProcessId()` → nom du processus (`Process
  .GetProcessById(pid).ProcessName`, comparaison ordinale insensible à la casse avec `claude`). Aucune UI
  Automation. Implémentation derrière une interface neutre (`IPremierPlan` / `PremierPlanWin32`), horloge
  injectée, sondage à la cadence du widget (2 s), résultat mis en cache 1 s.
- Nommage : `GardesPerimetreTests` interdit tout type dont le nom contient `Uia` ou se termine par
  `ForegroundWatch` — choisir `PremierPlanWin32` / `IPremierPlan` ; ne pas assouplir la garde.
- Dès que le premier plan quitte `claude`, LUE-01 reprend seule : le retour ultérieur (alt-tab ou clic) met
  `lastFocusedAt` à jour (geste A) et la session disparaît par LUE-01.

### La déduction (« En attente ? ») — décision écrite
- Une session en attente DÉDUITE (silence) ouverte ensuite par l'utilisateur est **lue** au même titre : le
  prédicat d'attente du détecteur (`EstAttente`) inclut déjà `WaitingDeduced` depuis la phase 26, et le
  relevé de phase 27 ne donne aucune raison de l'exclure. Si elle redevient une attente observée plus récente,
  elle revient (NET-03).

### Le diagnostic (LUE-03)
- `MotifMasquage` gagne `LueParFocus` (« lue : focus à HH:MM > attente à HH:MM ») et `LueAuPremierPlan`
  (« sélectionnée au premier plan depuis N s ») ; « répondue » (NET-01) et les motifs v1.6 (archivée, marquée
  à la main) restent distincts. Le diagnostic imprime, par session masquée, le motif et les deux instants.

### Pas de faux masquage (LUE-04)
- Sans `lastFocusedAt` pour une session (pas de fichier app, pas de `cliSessionId`, format changé) : LUE-01 et
  LUE-02 ne s'appliquent PAS à cette session ; comportement v1.6 strict. Test dédié avec la fixture
  `sans-cliSessionId.json`.
- Sans détection de premier plan (échec Win32) : LUE-02 inactive, LUE-01 seule, et le diagnostic le dit.

### Claude's Discretion
- Découpage en plans ; nom des types ; présentation exacte des lignes du diagnostic.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

- `.planning/phases/27-le-relev-avant-la-r-gle/27-RELEVE.md` — gestes A/B, valeurs 20:30:44 / 20:34:04, processus `claude`
- `.planning/REQUIREMENTS.md` — LUE-01..04 (ajustées le 2026-09-25)
- `src/Chronos/Services/SessionTreatmentTracker.cs`, `TreatedStore.cs`, `SessionMonitor.cs`, `LectureSessions.cs`
- `tests/Chronos.Tests/TreatedSessionsTests.cs` (scénario des 478 min : à préserver), `GardesPerimetreTests.cs`
- `tests/Chronos.Tests/TestData/DesktopAppSessions/session-courante-geste-b.json` (fixture du geste B)

</canonical_refs>

<specifics>
## Specific Ideas

- Test « le relevé du 2026-09-25 à 16 h 08 ne se reproduit plus » (critère 1 de la roadmap) : deux sessions
  `end_turn` à 15:59:05 et 15:56:10, focus à 16:00:39 et 16:00:59, horloge à 16:08 ⇒ masquées « lue » ; une
  troisième `end_turn` à 16:05 avec focus à 16:00 ⇒ visible « En attente ».
- Test du geste B : attente à 20:34:04.328, focus 20:30:44.976, premier plan `claude` depuis 20:33:27 ⇒ lue
  (LUE-02) ; même chose avec premier plan `explorer` ⇒ visible.
- Test alt-tab (geste A) : focus passe de 16:23:51 à 20:30:44 après une attente à 20:06 ⇒ lue par LUE-01.

</specifics>

<deferred>
## Deferred Ideas

- Marquer « lu » côté app (écriture dans `%APPDATA%\Claude`) — hors périmètre définitif.
- Exploiter `latestUserFrameAt` pour dater une réponse (NET-01 côté app) — après v1.7.

</deferred>

---

*Phase: 30-la-lecture-fait-dispara-tre*
*Context gathered: 2026-09-25 — décisions consignées par l'agent en mode autonome, d'après le relevé de phase 27*
