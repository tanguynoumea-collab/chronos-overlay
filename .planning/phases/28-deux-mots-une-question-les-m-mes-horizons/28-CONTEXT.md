# Phase 28 : Deux mots, une question, les mêmes horizons — Contexte

**Gathered:** 2026-09-25
**Status:** Ready for planning
**Source:** décisions de l'utilisateur (conversation du 2026-09-25, « lance tout ») + relevé de phase 27 en cours

<domain>
## Phase Boundary

Le widget de sessions ne parle plus qu'en deux mots. Cette phase touche le VOCABULAIRE et les HORIZONS, pas
les sources : ni la source app-bureau (phase 29), ni la règle « lue » (phase 30). Elle est indépendante du
relevé de la phase 27.

Livrables : `AffichageSessions` (libellés, ordre), `TranscriptSessionSource` (AskUserQuestion, horizons),
`SessionMonitor` (horizons partagés), `SessionsViewModel` (drapeaux de gabarit), les 8 templates de
`SessionStyles.xaml` (aucun libellé codé en dur n'y subsiste), `docs/hooks-contract.md` §3, le texte
d'activation du widget, et les tests.

</domain>

<decisions>
## Implementation Decisions

### Les libellés — VERROUILLÉS par l'utilisateur
- Exactement trois chaînes à l'écran : **« Réflexion »**, **« En attente »**, **« En attente ? »**.
  Rien d'autre : « tour fini », « à toi », « en cours », « à toi ? déduit », « inconnu » disparaissent.
- « En attente » couvre `WaitingTurn` (Stop observé / `end_turn`), `WaitingAttention` (PermissionRequest,
  Notification filtrée) et la question posée (LIB-02 ; en phase 29, `blocked` de l'app rejoindra ce même mot).
- « En attente ? » = `WaitingDeduced` uniquement. Le point d'interrogation est obligatoire (doctrine v1.6).
- « Réflexion » = `Working`.
- `Unknown` n'a plus de ligne dans le widget. Le diagnostic, lui, continue de la lister avec la raison.
- Les mots portent la distinction ; la couleur reste : rampe ambre pour les trois attentes, rampe verte pour
  Réflexion (inchangé). « En attente ? » peut être atténuée (opacité) mais reste lisible — pas de fantôme à
  0,22 (leçon de la phase 25).

### La question `AskUserQuestion` (LIB-02)
- Dans le transcript : un message assistant dont le dernier `tool_use` s'appelle `AskUserQuestion` et sans
  `tool_result` postérieur ⇒ attente (rang « question », comme une permission), pas Working.
- Dès qu'un `tool_result` (ou un message user) suit ⇒ Working à nouveau.
- Le nom d'outil est comparé ordinalement, exact : `AskUserQuestion`. Pas de liste extensible « au cas où ».

### L'ordre (LIB-04) et le couplage R4
- Ordre d'écran : WaitingAttention (permission/question) → WaitingTurn → WaitingDeduced → Working → puis
  fraîcheur décroissante. Donc **« En attente ? » passe DEVANT « Réflexion »** (aujourd'hui la déduction est
  au dernier rang, derrière Working).
- `ArbitrageSessions.Departager` consomme `AffichageSessions.Urgence` au rang 3 (réserve R4 de l'audit v1.6).
  Décision : **découpler** — l'arbitrage garde son propre rang d'état (un `RangArbitrage` privé, figé,
  documenté comme tel), et un test prouve que changer l'ordre d'écran ne change aucun résultat d'arbitrage
  (rejouer le corpus des 720 permutations avec l'ordre d'écran muté).

### Les horizons (SIL-01)
- `TranscriptSessionSource.ActiveWindow` passe de 15 min à **8 h** (= `SessionMonitor.DropAfter`), et la
  règle de silence (`SilenceDesBattements` = 20 min, Working → WaitingDeduced) s'applique aux transcripts
  comme aux hooks — dans UN seul endroit (le moniteur), pas dupliquée dans la source.
- Les constantes d'horizon vivent dans un type unique (`HorizonsSessions` ou équivalent) et une garde
  rougit si la chaîne 20 min < 8 h < 24 h (TreatedStore.RetentionMax) < 72 h (BalayageMagasinSessions) se
  défait.
- Coût à MESURER sur la vraie machine (~1 050 transcripts, dont ~94 % de sous-agents pré-filtrés par chemin) :
  énumération + lecture des queues de 64 Ko des fichiers < 8 h, toutes les 2 s. Si > ~50 ms par cycle, mettre
  en cache par (chemin, mtime, taille) — le cache est acceptable, la lecture partielle ne l'est pas.
- `MaxSessions` = 12 reste ; le tri par fraîcheur avant la limite garantit que les plus récentes gagnent.

### Un seul producteur (LIB-03)
- `AffichageSessions.Etat` et `Ordonner` restent LE producteur ; `SessionsViewModel.Describe` ne fait que
  colorer. Garde existante des 16 caractères conservée (« En attente ? » = 12).
- `docs/hooks-contract.md` §3 (états produits) et le texte d'activation du widget (réserve R9) reprennent
  les trois mots exacts ; la garde `ContratHooksDocumenteTests` s'étend aux libellés si elle ne le fait pas.

### Claude's Discretion
- Nom du type des horizons, découpage en plans/vagues, ordre des tests, choix cache ou non selon la mesure.
- Présentation exacte de l'atténuation de « En attente ? » par style (respecter chaque template).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Code touché
- `src/Chronos/Services/AffichageSessions.cs` — libellés, ordre, `Urgence` (consommée par l'arbitrage : R4)
- `src/Chronos/Services/ArbitrageSessions.cs` — `Departager` rang 3 = `AffichageSessions.Urgence`
- `src/Chronos/Services/TranscriptSessionSource.cs` — `ActiveWindow` 15 min, `Classify`, `HasToolUse`
- `src/Chronos/Services/SessionMonitor.cs` — `SilenceDesBattements` 20 min, `DropAfter` 8 h, `TryRead`
- `src/Chronos/Services/TreatedStore.cs` (`RetentionMax` 24 h), `src/Chronos/Services/BalayageMagasinSessions.cs` (72 h)
- `src/Chronos/ViewModels/SessionsViewModel.cs` — `Describe`, drapeaux `IsAttention/IsTurn/IsWorking/IsGhost`
- `src/Chronos/Resources/SessionStyles.xaml` — les 8 templates (aucun libellé en dur autorisé)
- `docs/hooks-contract.md` §3 + `tests/Chronos.Tests/ContratHooksDocumenteTests.cs`

### Doctrine et audit
- `.planning/v1.6-MILESTONE-AUDIT.md` — §5.3 réserves R4 (double consommateur d'`Urgence`) et R9 (texte
  d'activation), §6 chaîne des horizons, §9.1 trou de couverture
- `.planning/STATE.md` — « Contexte technique v1.7 »
- `.planning/milestones/v1.6-REQUIREMENTS.md` — EVT-04 (attente déduite), doctrine « observé, jamais déduit »

</canonical_refs>

<specifics>
## Specific Ideas

- Le tableau de l'utilisateur, à rejouer tel quel en test : réfléchit → « Réflexion » ; a fini ou pose une
  question, non lue → « En attente » ; a fini et lue → rien (cette dernière ligne est la phase 30, mais la
  phase 28 doit laisser un point d'entrée : le prédicat « est une attente » unique et nommé).
- Scénario SIL-01 à tester avec horloge injectée : transcript seul, Working, dernière écriture il y a 25 min
  ⇒ « En attente ? » ; transcript seul, `end_turn` il y a 3 h ⇒ « En attente » ; 8 h 01 ⇒ absent.

</specifics>

<deferred>
## Deferred Ideas

- `blocked` / `needs_action` de l'app bureau → phase 29 (même mot « En attente », rang question).
- La règle « lue » et la détection Win32 du premier plan → phase 30 (forme fixée par le relevé de phase 27).
- Exploiter `latestUserFrameAt` / `completedTurns` → hors v1.7.

</deferred>

---

*Phase: 28-deux-mots-une-question-les-m-mes-horizons*
*Context gathered: 2026-09-25 — décisions utilisateur consignées par l'agent en mode autonome*
