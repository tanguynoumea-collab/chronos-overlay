# Fixtures — métadonnées par session de l'app bureau Claude

Six fichiers **réels**, relevés le 2026-09-25 (phase 27, app bureau 2.9939.2.0) dans
`%APPDATA%\Claude\claude-code-sessions\<org>\<user>\local_<id>.json`, puis **réduits** aux champs que Chronos
lit et **anonymisés** (`title`, `cwd`, `originCwd`, `sessionId`, `status_detail`, `needs_action`).
Une fixture régénérée par sérialisation ne prouverait rien : ces fichiers viennent du disque.

| Fichier | Ce qu'il couvre |
|---|---|
| `fin-de-tour-completed.json` | `postTurnSummary.status_category = completed` |
| `fin-de-tour-blocked.json` | `blocked` + `needs_action` non vide (question posée à l'utilisateur) |
| `fin-de-tour-review-ready.json` | `review_ready` |
| `sans-postTurnSummary.json` | session sans classification de fin de tour (cas majoritaire : 127/138) |
| `sans-cliSessionId.json` | fichier sans jointure possible (23/138 sur disque) — doit être ignoré sans bruit |
| `session-courante-geste-b.json` | la session du geste B : `lastFocusedAt` 20:30:44 antérieur à la fin de tour 20:34:04 |

Champs conservés : `sessionId`, `cliSessionId`, `cwd`, `originCwd`, `createdAt`, `lastFocusedAt`,
`lastActivityAt`, `latestUserFrameAt`, `completedTurns`, `lastAssistantUuid`, `isArchived`, `title`,
`titleSource`, `postTurnSummary`, `postTurnSummaryFor`, `model`. Le bloc `_fixture` documente l'origine ; il
n'existe pas dans les vrais fichiers et le lecteur doit l'ignorer comme tout champ inconnu.

Rappels du contrat (voir `.planning/phases/27-le-relev-avant-la-r-gle/27-RELEVE.md`) : format interne non
documenté ; fichiers de ~275 Ko réécrits en entier ; `postTurnSummary` transitoire ; horodatages en
millisecondes epoch ; lecture seule stricte.
