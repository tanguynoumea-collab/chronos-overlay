# Phase 27 — Le relevé avant la règle (VAL-01)

Relevé en lecture seule sur la machine de l'utilisateur, le 2026-09-25. Aucun overlay de développement lancé,
aucune écriture dans `%APPDATA%\Claude`, `%APPDATA%\Chronos` ni `~/.claude/settings.json`.

## Task 1 — relevé passif (sans geste)

### Environnement relevé à 20:00

| Élément | Valeur |
|---|---|
| App bureau | paquet MSIX `Claude_2.9939.2.0_x64__pzs8sxrjxfjjc`, processus **`claude.exe`** (`C:\Program Files\WindowsApps\…\app\claude.exe`), PID 88220 |
| Titre de fenêtre | « Claude » (aucun nom de session) |
| Fenêtre au premier plan à 20:00:17 | `explorer.exe` (« Téléchargements : Explorateur de fichiers ») — l'utilisateur n'était PAS dans Claude |
| Hooks Chronos dans `~/.claude/settings.json` | 8 groupes `Chronos-v3.1.0.exe --hook` (fichier inchangé depuis 11:07:56) |
| Overlay | `Chronos-v3.1.0.exe` PID 40772, démarré le 23.09 à 11:09 |

Commande de corrélation : script Python (lecture des `local_*.json` modifiés < 36 h, jointure par
`cliSessionId` sur `%APPDATA%\Chronos\sessions\<id>.json`).

### Corrélation app ↔ hooks, relevée à 19:58:00

| session | titre | `lastFocusedAt` | `lastActivityAt` | `latestUserFrameAt` | `postTurnSummary` | fichier de hook |
|---|---|---|---|---|---|---|
| 11456cab | Overlay session status badges (cette session) | **16:23:51** | 19:57:12 | 19:51:35 | — | `Working / PreToolUse @ 19:59` |
| 74ac9f48 | Chronos pourcentages incorrect | 16:01:13 | 12:13 | — | — | absent |
| dae57d29 | REPRISE 3.3.0 release notes | 16:01:07 | 16:48:08 | 13:51 | — | absent |
| 88677186 | ADVANCED SHEET passation checkpoint 26 | 16:00:59 | 16:48:03 | 15:19 | — (était `blocked` à 16:08) | absent |
| c17a1b03 | Recette vocale JARVIS phase 3 | 16:00:39 | 16:48:16 | 16:48:14 | — (était `review_ready` à 16:08) | absent |
| 939eb30a | Phase 3 avec phase 2 ouverte | 13:53:34 | 13:54:24 | 13:54:13 | `completed` | absent |

### Ce que ces valeurs tranchent déjà

1. **`lastFocusedAt` est mis à jour à la re-sélection d'une session.** Cette session valait `16:05:46`
   (= `createdAt`) à 16:08 ; elle vaut `16:23:51` à 19:58. Entre les deux l'utilisateur a visité d'autres
   sessions (focus 16:09:17, 16:10:17) puis est revenu. La prémisse de LUE-01 tient.
2. **Entre 16:23:51 et 19:51:35 (frappe d'un message), `lastFocusedAt` n'a pas bougé.** Pendant ces 3 h 28
   l'utilisateur a quitté l'app (limite d'usage atteinte, reprise à 19:50) et y est revenu SANS changer de
   session. Indice fort que le simple retour de fenêtre (alt-tab) **ne** met **pas** `lastFocusedAt` à jour —
   à confirmer par le geste A, qui isole ce cas.
3. **`lastActivityAt` suit l'activité de l'assistant** (19:57:12 pendant un tour en cours, fichier réécrit à
   cet instant). C'est un battement côté app, indépendant des hooks. Pour les sessions inactives, il vaut
   l'instant de leur dernier événement (16:48 = arrêt à la limite).
4. **`postTurnSummary` est TRANSITOIRE.** `blocked` (88677186) et `review_ready` (c17a1b03) lus à 16:08 ont
   disparu à 16:58, quand l'app a été redémarrée (tous les `local_*.json` réécrits à 16:58:21, nouveau PID
   `claude.exe` 88220 ≠ 78920). APP-03 ne peut s'appuyer dessus qu'en **indice** de fin de tour, jamais en
   état persistant.
5. **Le redémarrage de l'app termine les processus CLI** : `SessionEnd` a supprimé les fichiers d'état des
   5 sessions du jour ; seule cette session, reprise à 19:51, en a réécrit un (`SessionStart` → battements).
   Conséquence pour la règle : après un redémarrage de l'app, plus aucune session n'est « En attente » par les
   hooks ; les transcripts (< 15 min, ou < 8 h après SIL-01) portent seuls l'état.

## Task 2 — les deux gestes

### Geste A — retour alt-tab sur la session déjà sélectionnée (« A fait » à 20:31)

| Instant | `lastFocusedAt` de cette session (11456cab) | Source |
|---|---|---|
| 19:58:00 (avant le geste) | **16:23:51.xxx** (inchangé depuis 3 h 28, malgré le retour de l'utilisateur à 19:51 pour taper un message) | relevé Task 1 |
| 20:31:22 (après le geste) | **20:30:44.976** | `local_eb880bbb…json`, mtime 20:31:09 |

Autres champs à 20:31:22 : `lastActivityAt` 20:31:05, `latestUserFrameAt` 19:51:35 (pas encore le message
« A fait »), `completedTurns` 4, `postTurnSummary` absent. Fenêtre au premier plan à 20:33:27 : processus
**`claude`** (PID 88220), titre « Claude ».

**Inconnue 1 tranchée : OUI, le simple retour de fenêtre (alt-tab) met `lastFocusedAt` à jour**, sans
changement de session dans la barre latérale. Réserve : entre 16:23:51 et 19:51 la valeur n'avait pas bougé
alors que l'utilisateur est revenu taper un message — soit l'app n'avait pas perdu le focus (fenêtre restée
au premier plan pendant l'attente de la limite), soit un retour de focus n'est pris en compte que sous
certaines conditions. Le geste contrôlé fait foi ; la réserve est notée pour la phase 31 (constat en
production).

### Observations faites pendant le geste A (non prévues, à porter dans les phases 28-30)

1. **Le fichier d'état de cette session avait DISPARU à 20:31:22** (présent à 19:59 en `Working/PreToolUse`,
   tour terminé vers 20:06 → un `Stop`/`WaitingTurn` aurait dû y rester), puis il **réapparaît à 20:31:52**
   (`Working/PreToolUse`, écrit par l'appel d'outil suivant). Seul `SessionEnd` supprime ce fichier : le
   processus CLI de la session a donc été terminé puis relancé par l'app entre 20:06 et 20:31 (retour de
   l'utilisateur après une limite d'usage). Chronos ne lit pas le champ `source` de `SessionStart`
   (`startup` / `resume` / `clear` / `compact`) et route tout `SessionStart` vers `Working` : **une reprise
   n'est pas un travail**. À corriger (phase 28 ou 30) : `source = resume` ⇒ aucun état écrit, le transcript
   décide.
2. **Un `Stop` peut donc être perdu** (fichier supprimé par le `SessionEnd` du cycle de vie de l'app). La
   règle « lue » ne peut pas dépendre du seul fichier de hook pour dater la fin de tour : le **timestamp du
   dernier message assistant `end_turn` du transcript** (UTC, champ `timestamp`) est la date robuste. Le
   `mtime` du fichier ne l'est PAS : l'app y ajoute des lignes de métadonnées sans timestamp (`custom-title`,
   `agent-name`, `mode`, `atis-latch`, `bridge-session`, `last-prompt`, `queue-operation`) qui rafraîchissent
   le fichier sans qu'aucun message n'ait été échangé.
3. Les trois autres sessions du jour (JARVIS, ADVANCED SHEET, OLYMPE) **travaillaient réellement** pendant
   le geste (messages à 20:31:40, 20:32:12 `end_turn`, 20:32:40) : leurs fichiers d'état réapparus à
   20:31:40-51 sont des `UserPromptSubmit`/`PreToolUse` légitimes, pas une « tempête de reprise ».
4. `postTurnSummary` reste absent sur cette session après 4 tours terminés : la classification de l'app
   n'est ni systématique ni durable — indice seulement (confirme Task 1, point 4).

### Geste B — le tour qui se termine sous les yeux

Protocole joué : l'utilisateur est resté sur cette session, fenêtre Claude au premier plan (relevé à
20:33:27 : processus `claude`, PID 88220), pendant que le tour de l'agent se terminait ; puis « B fait ».

| Grandeur | Valeur | Source |
|---|---|---|
| Fin du tour de l'agent | **20:34:04.328** | transcript, dernier message assistant `stop_reason = end_turn` (`timestamp` UTC 18:34:04.328) |
| `lastFocusedAt` après la fin du tour | **20:30:44.976** (inchangé) | `local_eb880bbb…json` à 20:34:56 |
| `latestUserFrameAt` | 20:34:43.093 (« B fait ») | idem |
| `lastActivityAt` | 20:34:46.677 | idem |
| Fichier d'état des hooks (`Stop` → `WaitingTurn` attendu à 20:34:04) | **ABSENT à 20:34:56** ; réapparaît à 20:35:28 en `Working/PreToolUse` (2e appel d'outil du tour suivant) | `%APPDATA%\Chronos\sessions` |

**Inconnue 2 tranchée : quand le tour se termine sous les yeux de l'utilisateur, `lastFocusedAt` reste
ANTÉRIEUR à la fin du tour** (20:30:44 < 20:34:04). Une règle « lue » qui ne comparerait que ces deux dates
annoncerait « En attente » une session que l'utilisateur est en train de lire. **LUE-02 est donc nécessaire**,
sous la forme relevée : la session sélectionnée (celle au `lastFocusedAt` le plus récent, toutes sessions
confondues) est lue tant que la fenêtre dont le processus est `claude` est au premier plan.

**Le fichier de hook disparaît à la frontière des tours, deux fois sur deux.** Tour 5 : absent au 1er appel
d'outil (20:31:22), présent au 2e (20:31:52). Tour 6 : absent au 1er appel (20:34:56), présent au 2e
(20:35:28). Pourtant `Stop` (20:34:04) puis `UserPromptSubmit` (20:34:43) puis le 1er `PreToolUse`
(20:34:55) auraient chacun dû l'écrire. Seul `SessionEnd` supprime ce fichier : l'app bureau termine donc le
processus CLI à la fin du tour (ou au message suivant) et en relance un, et ce `SessionEnd` tardif efface les
premières écritures du tour suivant. À 16:08, en revanche, deux `WaitingTurn` avaient survécu 9 et 12 min :
la suppression n'est pas immédiate ni systématique — son délai n'a pas été mesuré. Mécanisme = hypothèse ;
les absences = faits.

## Task 3 — conclusion et LUE-02

### Ce qui est tranché (valeurs à l'appui)

| # | Question | Réponse | Preuve |
|---|---|---|---|
| 1 | Le retour alt-tab met-il `lastFocusedAt` à jour ? | **Oui** | 16:23:51 → 20:30:44 sans changement de session (geste A) |
| 2 | Tour terminé sous les yeux : `lastFocusedAt` précède-t-il la fin du tour ? | **Oui, toujours** | 20:30:44 < 20:34:04 (geste B) |
| 3 | La session au `lastFocusedAt` max est-elle celle que l'utilisateur voit ? | **Oui** (11456cab = max à 20:31 et 20:34, pendant qu'il y écrivait) | Task 1 + gestes |
| 4 | Processus de la fenêtre Claude au premier plan | **`claude`** (`C:\Program Files\WindowsApps\Claude_2.9939…pp\claude.exe`), titre « Claude » | 20:33:27 |
| 5 | Les champs lus en phase 29 existent-ils sur la version du jour (2.9939.2.0) ? | **Oui** : `cliSessionId`, `title`, `lastFocusedAt`, `lastActivityAt`, `latestUserFrameAt`, `completedTurns`, `postTurnSummary` (transitoire) | Task 1 |

### LUE-02 réécrite (ajustement du 2026-09-25, d'après ce relevé)

- **LUE-01** : l'instant de l'attente comparé à `lastFocusedAt` est le **`timestamp` du dernier message
  assistant `end_turn` (ou de la question posée) lu dans le transcript**, jamais le `mtime` du fichier (rafraîchi
  par des lignes de métadonnées sans message) ni le seul fichier de hook (supprimé à la frontière des tours).
  Le `Stop` du hook, quand il existe, porte le même instant à quelques centaines de ms près et peut servir de
  confirmation, pas de référence.
- **LUE-02 — CONSERVÉE et précisée** : la session **sélectionnée** = celle dont `lastFocusedAt` est le plus
  récent parmi tous les fichiers de l'app ; elle compte comme lue tant que la fenêtre au premier plan
  appartient au processus **`claude`** (`GetForegroundWindow` → `GetWindowThreadProcessId` → nom de
  processus, sans UI Automation), avec un délai de grâce de **2,5 s** après la fin du tour. Dès que le
  premier plan quitte `claude`, la règle LUE-01 reprend seule : un retour ultérieur (alt-tab ou clic) met
  `lastFocusedAt` à jour et fait disparaître la session.
- **Réserve pour la phase 31** : entre 16:23:51 et 19:51, `lastFocusedAt` n'a pas bougé alors que
  l'utilisateur est revenu écrire (retour après limite d'usage). Cas non reproduit par le geste A ; à
  observer en production, sans en faire une règle.

### Conséquences de conception à porter dans les phases suivantes

1. **Phase 28** : la date d'une attente transcript = `timestamp` du dernier message significatif, pas le
   `mtime` (touche `TranscriptSessionSource.Classify` → `SessionSnapshot.UpdatedAt`). Le `mtime` ne sert
   qu'au pré-filtre d'énumération (< 8 h).
2. **Phase 28 ou 30** : `SessionStart` avec `source = resume` ne doit plus écrire `Working` (« une reprise
   n'est pas un travail ») ; `SessionEnd` ne doit plus être lu comme « la session n'existe plus » dans l'app
   bureau — le transcript décide. Chronos ne lit aujourd'hui ni `source` ni `reason`.
3. **Phase 29** : `postTurnSummary` = indice transitoire (absent après 5 tours ici ; effacé au redémarrage).
4. **Phase 30** : LUE-02 telle que réécrite ci-dessus ; test avec les valeurs de ce relevé (20:30:44 /
   20:34:04 / premier plan `claude`).

### Aucune écriture hors dépôt

`~/.claude/settings.json` : mtime 25/09 11:07:56 inchangé. `%APPDATA%\Chronos\settings.json` : inchangé.
`%APPDATA%\Claude` : lecture seule. Aucun overlay de développement lancé (`Chronos-v3.1.0.exe` PID 40772 seul).

