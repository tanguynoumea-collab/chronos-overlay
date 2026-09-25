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

*(à remplir après « A fait » / « B fait »)*

## Task 3 — conclusion et LUE-02

*(à remplir)*
