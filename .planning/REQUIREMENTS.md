# REQUIREMENTS.md — Chronos v1.7 « Lue ou non lue »

## Contexte

Le milestone v1.6 a rendu le widget de sessions **honnête** : « réfléchit » et « tour fini » sont observés
(battements, `Stop`, `PermissionRequest`), les sources sont arbitrées par fraîcheur, et « traité » ne se déduit
que d'une transition observée. Vérifié en production le 2026-09-25 : exe 3.1.0, 8 groupes de hooks actifs
depuis l'app bureau.

**Et pourtant le widget n'aide pas.** L'utilisateur le dit : « les widgets de sessions ne servent au final à
rien ». Relevé du 2026-09-25 à 16 h 08 : quatre sessions actives, deux « en cours », deux « tour fini » en
orange — et les deux avaient déjà été ouvertes et **lues** par l'utilisateur à 16 h 00 et 16 h 01. Le widget ne
sait pas qu'une session a été lue : une session terminée reste en attente jusqu'au prochain prompt, à un clic
droit, ou à l'expiration de 8 h. Ce n'est pas un défaut d'observation, c'est un **signal manquant**.

**Le signal existe.** L'app bureau Claude écrit un fichier JSON par session sous
`%APPDATA%\Claude\claude-code-sessions\<org>\<user>\local_<id>.json` (142 fichiers, ~275 Ko, réécrits au
changement de session et en fin de tour ; `%APPDATA%\Claude` n'est PAS une jonction : c'est l'AppData
VIRTUALISÉ par le paquet MSIX, invisible de l'overlay — il lit le cache du paquet, voir APP-06, sonde hors arbre). Il porte `cliSessionId` (l'UUID des hooks et des transcripts),
`title`, **`lastFocusedAt`** (dernier instant où la session a été sélectionnée dans l'app), et
`postTurnSummary { status_category ∈ completed | blocked | review_ready, needs_action }` — la classification
de fin de tour faite par l'app elle-même. Le relevé complet est dans `STATE.md`, section « Contexte technique
v1.7 » ; il ne se ré-enquête pas.

**Ce que l'utilisateur veut, en trois lignes :**

| Situation | Ce que le widget montre |
|---|---|
| session en train de réfléchir | **Réflexion** |
| session qui a fini (ou qui pose une question) et que je n'ai pas encore lue | **En attente** |
| session qui a fini et que j'ai lue | **rien** — elle n'apparaît pas |

**Doctrine inchangée (v1.5, v1.6) :** observé, jamais déduit ; une déduction porte son point d'interrogation ;
une source non documentée se lit avec tolérance et se dégrade vers « je ne sais pas », jamais vers une invention.

## v1.7 Requirements

### Source app-bureau par fichiers (APP)

- [x] **APP-01**: Chronos lit les métadonnées par session écrites par l'app bureau Claude
  (`%APPDATA%\Claude\claude-code-sessions\<org>\<user>\local_*.json`) et les joint aux sessions du widget par
  `cliSessionId`. Lecture tolérante (`FileShare.ReadWrite`, JSON invalide ignoré), limitée aux fichiers modifiés
  depuis moins de 24 h. Les racines se résolvent par candidats, le cache du paquet MSIX en premier (APP-06). Format interne **non documenté** : dossier absent, champ absent ou renommé ⇒ la source se tait et le
  widget garde le comportement v1.6 — jamais de crash, jamais d'invention.
- [x] **APP-02**: Le widget affiche le **titre** de la session (`title`) à la place du nom de dossier quand il est
  connu ; le dossier reste en repli quand il ne l'est pas, et reste lisible en info-bulle dans les deux cas.
- [x] **APP-03**: La classification de fin de tour de l'app est reconnue : `status_category = blocked` avec
  `needs_action` est une **attente observée** (question posée à l'utilisateur), et le motif (`needs_action`)
  est lisible en détail ou en info-bulle. `completed` et `review_ready` ne fabriquent aucun état à eux seuls.
- [x] **APP-04**: Le diagnostic rapporte l'état de la source app-bureau — dossier trouvé ou non, nombre de
  fichiers lus, nombre de jointures réussies, champs manquants — et, pour chaque session affichée, le titre, le
  dernier focus et la classification lus. Une source absente est annoncée « absente », pas passée sous silence.
- [x] **APP-05**: Chronos n'écrit **jamais** dans `%APPDATA%\Claude` : la source est en lecture seule, et une
  garde le tient par test.
- [x] **APP-06** *(ajoutée le 2026-09-25, sonde hors de l'arbre de l'app — `29-SONDE-HORS-ARBRE.txt`)* :
  l'overlay lit les fichiers d'état des hooks dans **toutes les vues d'AppData** — la vue réelle
  (`%APPDATA%\Chronos\sessions`, vide en production) ET le cache du paquet MSIX de l'app bureau
  (`%LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Chronos\sessions`, où les hooks lancés sous l'app
  écrivent réellement) — résolution des racines par candidats, fusion par `session_id` (l'arbitrage par
  fraîcheur existe), balayage CYC-01 et diagnostic sur chaque racine. La racine de la source app-bureau
  (`claude-code-sessions`) se résout de la même façon, le paquet en premier : `%APPDATA%\Claude` n'existe
  pas vu de l'overlay. Sans cette exigence, rien de v1.6 ni de v1.7 n'atteint le widget en production.
### La lecture fait disparaître (LUE)

- [x] **LUE-01**: Une session en attente (tour fini, à toi, ou question posée) dont l'instant d'attente est
  **antérieur** au dernier focus (`lastFocusedAt`) est marquée traitée automatiquement, via le magasin
  réversible existant (`TreatedStore`, épisode daté par l'instant du signal) — elle disparaît **sans clic**.
  Réversible comme aujourd'hui : un nouvel épisode d'attente plus récent la ramène (NET-03 inchangé).
  *Précisé le 2026-09-25 d'après `27-RELEVE.md`* : l'instant d'attente est le **`timestamp` du dernier message
  assistant `end_turn` (ou de la question posée) du transcript** — jamais le `mtime` du fichier (rafraîchi par
  des lignes de métadonnées sans message), ni le seul fichier de hook (supprimé à la frontière des tours par
  l'app bureau). Le `Stop` du hook, s'il existe, confirme ; il ne fait pas référence.
- [x] **LUE-02**: La session **sélectionnée** dans l'app (celle dont `lastFocusedAt` est le plus récent, tous
  fichiers confondus) compte comme lue tant que la fenêtre au **premier plan** appartient au processus
  **`claude`**, avec un délai de grâce de 2,5 s après la fin du tour — c'est le cas du tour qui se termine
  pendant qu'on le regarde, où `lastFocusedAt` précède la fin du tour. Détection Win32
  (`GetForegroundWindow` → `GetWindowThreadProcessId` → nom du processus), **sans UI Automation**.
  *Confirmée NÉCESSAIRE le 2026-09-25 par le geste B de `27-RELEVE.md`* : fin de tour 20:34:04 sous les yeux
  de l'utilisateur, `lastFocusedAt` resté à 20:30:44. Et le geste A a montré qu'un retour alt-tab met
  `lastFocusedAt` à jour : dès que le premier plan quitte `claude`, LUE-01 reprend seule.
- [x] **LUE-03**: Le diagnostic distingue « lue » de « répondue » : pour une session masquée, il nomme le motif
  (« lue : focus à HH:MM > attente à HH:MM », « sélectionnée au premier plan », ou « répondue »), et jamais un
  masquage sans cause.
- [x] **LUE-04**: Quand la source app-bureau est absente pour une session (terminal pur, fichier illisible,
  format changé), la règle « lue » ne s'applique pas à cette session : le comportement v1.6 est conservé, sans
  faux masquage.
- [x] **LUE-05** *(ajoutée le 2026-09-26, recherche de phase 30, point 9)* : le masquage « traitée » — qu'il vienne
  d'une réponse (NET-01), d'une lecture (LUE-01/02) ou d'un geste — ne s'applique qu'à une session **en
  attente**. Une session traitée qui se remet à **travailler** est visible « Réflexion » (ligne 1 du tableau de
  l'utilisateur), et son prochain épisode d'attente la ramène « En attente » (NET-03 inchangé). Aujourd'hui le
  filtre de `SessionMonitor` masque une session répondue tant qu'elle travaille.
### Deux libellés (LIB)

- [x] **LIB-01**: Le widget n'affiche plus que deux libellés d'état : **« Réflexion »** (travail observé) et
  **« En attente »** (tour fini, permission, question posée). L'attente déduite du silence s'affiche
  **« En attente ? »** — le point d'interrogation reste obligatoire. L'état « inconnu » n'est plus affiché.
- [x] **LIB-02**: Une question `AskUserQuestion` en suspens (bloc `tool_use` sans `tool_result` dans le
  transcript) est classée « En attente », pas « Réflexion ».
- [x] **LIB-03**: Les libellés et l'ordre viennent d'un seul producteur (`AffichageSessions`) partagé par le
  widget et le diagnostic ; aucun libellé ne dépasse seize caractères (garde existante) ; les huit styles
  visuels et les neuf thèmes les affichent sans troncature.
- [x] **LIB-04**: L'ordre du widget est conservé : « En attente » d'abord (permission ou question avant tour
  fini avant déduit), puis « Réflexion », puis la fraîcheur.

### Le trou de couverture §9.1 (SIL)

- [x] **SIL-01**: Une session connue par son transcript seul (sans fichier de hook) suit les **mêmes horizons**
  que les sessions à fichier de hook : travail sans écriture depuis plus de 20 min ⇒ « En attente ? »,
  abandon à 8 h. Elle ne disparaît plus en silence 15 min après la dernière écriture.

### Validation in vivo et livraison (VAL)

- [x] **VAL-01**: Les deux points ouverts du relevé sont observés **sur la vraie machine**, avec un protocole
  écrit et des valeurs relevées : (1) `lastFocusedAt` est-il mis à jour au simple retour (alt-tab) sur la
  session déjà sélectionnée ? (2) que vaut-il quand un tour se termine pendant que l'utilisateur regarde ? La
  règle LUE-02 est ajustée d'après le relevé, pas d'après une supposition — et l'ajustement est écrit.
- [ ] **VAL-02**: La source app-bureau est documentée dans `docs/desktop-app-sessions.md` (chemin, jonction,
  champs lus, ce qui n'est PAS garanti), au même titre que `docs/hooks-contract.md`.
- [ ] **VAL-03**: L'exe est publié en **3.2.0** — version embarquée et dans le nom du fichier
  (`Chronos-v3.2.0.exe`) — et les hooks et la statusLine sont réconciliés vers ce nouvel exe au premier
  lancement, comme en v1.6.

## Future Requirements (différés)

- Ventilation par modèle (opus/sonnet/cowork), survol/tooltip du cadran, icône tray, taille réglable.
- Préavis avant saturation (~90 %) et notification au reset.
- Exploiter `latestUserFrameAt` / `completedTurns` de l'app pour dater les tours sans hook.
- Cowork VM distant : toujours indéterminé (aucune source locale).

## Out of Scope (v1.7)

- **Réintroduire UI Automation** — retirée en v1.6 (entrées qui ne vieillissaient jamais, 1 294 lignes) ; les
  gardes anti-retour restent. La source app-bureau de v1.7 est un **lecteur de fichiers**, pas un lecteur
  d'arbre d'accessibilité.
- **Lire le titre de fenêtre de l'app** — il vaut « Claude » sans nom de session (vérifié) ; la technique du
  spinner de `claude-session-browser` ne s'applique qu'aux terminaux.
- **Écrire dans `%APPDATA%\Claude`** (marquer « lu » côté app, modifier `isArchived`) — données d'une autre
  application ; lecture seule stricte.
- **Notifications Windows / toasts** — inchangé.
- **Sessions de terminal sans app bureau** — elles gardent le comportement v1.6 (pas de focus connu) ; ce n'est
  pas le cas d'usage de l'utilisateur, qui n'utilise que l'app bureau.
- **R3 (écriture d'état monotone)** — cran 1 livré en v1.6, suite laissée ouverte ; ne pas rouvrir ici.

## Traceability

| REQ-ID | Phase | Statut |
|--------|-------|--------|
| APP-01 | Phase 29 | Complete |
| APP-02 | Phase 29 | Complete |
| APP-03 | Phase 29 | Complete |
| APP-04 | Phase 29 | Complete |
| APP-05 | Phase 29 | Complete |
| APP-06 | Phase 29 | Complete |
| LUE-01 | Phase 30 | Complete |
| LUE-02 | Phase 30 | Complete |
| LUE-03 | Phase 30 | Complete |
| LUE-04 | Phase 30 | Complete |
| LUE-05 | Phase 30 | Complete |
| LIB-01 | Phase 28 | Complete |
| LIB-02 | Phase 28 | Complete |
| LIB-03 | Phase 28 | Complete |
| LIB-04 | Phase 28 | Complete |
| SIL-01 | Phase 28 | Complete |
| VAL-01 | Phase 27 | Complete |
| VAL-02 | Phase 31 | Pending |
| VAL-03 | Phase 31 | Pending |

**Couverture :** 19 requirements, 17 mappés (phases 27 à 31), aucun orphelin, aucun doublon.

---
*Last updated: 2026-09-26 — LUE-05 ajoutée (une session qui travaille est toujours visible) ; APP-06 et LUE-01/02 le 2026-09-25*
