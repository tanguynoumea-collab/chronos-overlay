# Fixtures — un sous-agent qui travaille en arrière-plan après la fin du tour parent

Quatre fichiers **réels**, relevés le 2026-09-26 (phase 30.1, plan 02) dans la session
`88677186-8690-44dc-ac77-7a319a0aa5cb` — celle de l'**écart E2** du constat de phase 31 (`31-CONSTAT.md` §3,
signalé par l'utilisateur à 14:44 heure locale) :

- le transcript PARENT `~/.claude/projects/C--…-PROJET-ADVANCED-SHEET/<sid>.jsonl` ;
- deux transcripts de SOUS-AGENTS lancés en arrière-plan (`"requestShape":"background"` dans leurs
  `agent-*.meta.json` voisins) : `…/<sid>/subagents/agent-a7df37762e5ce2df4.jsonl` (encore en cours au relevé)
  et `…/<sid>/subagents/agent-af0db524b633162f3.jsonl` (terminé).

Extraction en **lecture seule** (`readFileSync`, aucune écriture dans `~/.claude`), par un script Node jetable
du bloc-notes de l'exécuteur. Une fixture régénérée par sérialisation ne prouverait rien (précédent DEL-06) :
ces lignes viennent du disque, elles ont été **réduites** et **anonymisées**, rien n'y a été ajouté. Le script
échoue si la séquence des `type` extraits n'est pas exactement celle du relevé de planification.

Les lignes ont été repérées par leur `timestamp`, jamais par leur numéro : le parent et `a7df…` grandissaient
encore pendant l'extraction (la session travaillait).

| Fichier | Source | Lignes (par `timestamp`) | Nb | Dernière ligne significative | Rôle dans les tests |
|---|---|---|---|---|---|
| `parent-fin-de-tour.jsonl` | parent | `assistant` 12:32:51.350Z → `system` 12:33:23.529Z, + les 6 métadonnées SANS horodatage qui suivent (`last-prompt`, `custom-title`, `agent-name`, `mode`, `atis-latch`, `bridge-session`) | 15 | `assistant` `end_turn` **12:33:21.818Z** (juste après le `tool_use` `Agent` 12:33:18.994Z et son `tool_result` 12:33:20.231Z) | le tour parent fini, un agent de fond lancé : « En attente » sans le sous-agent |
| `sous-agent-en-cours.jsonl` | `agent-a7df37762e5ce2df4.jsonl` | `user` 12:43:48.060Z → `user` 12:45:09.853Z | 20 | `user` `tool_result` **12:45:09.853Z** (l'agent travaille) | le relevé de 14:44 : il rend la session « Réflexion » |
| `parent-avant-fin-agent.jsonl` | parent | `user` 12:32:10.915Z → `system` 12:32:25.608Z | 6 | `assistant` `end_turn` **12:32:23.817Z** | un parent fini AVANT la fin d'un agent de fond |
| `sous-agent-termine.jsonl` | `agent-af0db524b633162f3.jsonl` | `assistant` 12:32:04.223Z → `assistant` 12:32:39.611Z | 10 | `assistant` `end_turn` **12:32:39.611Z** | un sous-agent terminé après le parent (travail) ou avant lui (rien) |

Toutes les lignes des deux sous-agents portent `"isSidechain":true` ; celles du parent `false`. Les heures
sont en UTC (14:33:21 locale = 12:33:21Z).

Fait réel utile (relevé de planification) : quand un agent de fond finit (12:32:39.611Z), le parent écrit une
ligne `user` (notification de tâche) 43 ms plus tard (12:32:39.654Z) — le parent reprend de lui-même. Cette
ligne n'est PAS dans `parent-avant-fin-agent.jsonl` (elle suit sa borne de fin) : les tests la simulent par
l'absence, c'est-à-dire le cas où le parent n'aurait pas encore repris.

## Dates d'écriture des sources, avant / après l'extraction

Deux exécutions du script (la seconde après l'extension de l'anonymisation aux clés-chemins, ci-dessous).
Chaque exécution relève la date d'écriture et la taille AVANT de lire et APRÈS avoir écrit les fixtures :

| Source | 1re exécution (avant = après) | 2e exécution (avant = après) |
|---|---|---|
| parent `<sid>.jsonl` | 2026-09-26T13:31:15.248Z, 3 788 213 o | 2026-09-26T13:35:05.003Z, 3 803 869 o |
| `agent-a7df37762e5ce2df4.jsonl` | 2026-09-26T13:34:23.188Z, 2 973 203 o | 2026-09-26T13:35:04.411Z, 2 989 149 o |
| `agent-af0db524b633162f3.jsonl` | 2026-09-26T12:32:39.713Z, 2 369 213 o | 2026-09-26T12:32:39.713Z, 2 369 213 o |

Dans chaque exécution, avant = après : l'extraction n'a rien écrit. Entre les deux exécutions, le parent et
`a7df…` ont grandi : c'est la session elle-même qui travaillait encore (l'agent de fond puis le parent), pas
l'extraction. Le sous-agent terminé n'a pas bougé depuis 12:32:39.713Z — c'est la date d'écriture qu'utilisent
les tests (`EcritureSousAgentTermine`). Pour le parent et `a7df…`, les tests posent les dates du relevé de
14:44 (12:33:23.541Z et 12:45:10.000Z), celles des fichiers tels qu'ils étaient quand l'écart a été vu.

## Ce qui a été retiré

- Lignes `user` / `assistant` / `attachment` / `system` : seules les clés `type`, `isSidechain`, `timestamp`,
  `cwd`, `sessionId`, `message` sont gardées ; dans `message` : `role`, `stop_reason`, `content` ; dans chaque
  bloc de `content`, celles présentes parmi `type`, `id`, `name`, `input`, `caller`, `tool_use_id`, `content`,
  `is_error`, `thinking`, `signature`, `text`. Tout le reste est RETIRÉ (`agentId`, `uuid`, `parentUuid`,
  `promptId`, `requestId`, `usage`, `model`, `toolUseResult`, `attachment`, `version`, `gitBranch`, `slug`…).
- Les lignes `attachment` et `system` n'ont plus de `message` : il n'en reste que leur forme horodatée — le fait
  utile (une ligne horodatée qui n'est pas un message).

## Ce qui a été anonymisé

- Toute valeur textuelle libre devient `"(anonymisé)"` : textes, `thinking`, `signature`, contenus des
  `tool_result`, valeurs des `input` d'outils (les clés d'`input` restent).
- `cwd` devient `"C:\Projets\Projet-A"` (le projet lu par Chronos est donc « Projet-A »).
- Lignes de métadonnées (`last-prompt`, `custom-title`, `agent-name`, `mode`, `atis-latch`, `bridge-session`,
  `file-history-snapshot`) : toutes leurs clés sont gardées, leurs valeurs textuelles anonymisées sauf `type` et
  `sessionId` ; nombres et booléens gardés. **Écart au script du plan** : une CLÉ d'objet qui est un chemin
  (celles de `file-history-snapshot.snapshot.trackedFileBackups` sont des chemins complets, nom d'utilisateur
  compris) devient `"(anonymisé) 1"`, `"(anonymisé) 2"`… — numérotée pour rester unique, la forme de l'objet
  est gardée.
- Conservés tels quels : `type`, `name`, `id`, `tool_use_id`, `role`, `stop_reason`, `timestamp`, `sessionId`,
  `isSidechain`, `caller`, `is_error`.
- Une chaîne VIDE dans la source reste vide (`thinking` des blocs de réflexion, `atis`) : la remplacer serait
  inventer un contenu.

Contrôles faits après extraction (second `node -e`, lecture) : dernière ligne significative de chaque fichier
conforme au tableau ; `isSidechain` = `true` sur toutes les lignes des sous-agents ; aucune clé `agentId`,
`uuid`, `parentUuid`, `requestId`, `usage` ; aucun nom d'utilisateur ; UTF-8 sans BOM ; fin de ligne `\n`.

Chargement par `[CallerFilePath]` puis COPIE dans une racine temporaire (`TranscriptTravailSousAgentTests`,
`SousAgentMoniteurTests`), disposée comme `~/.claude/projects` : `C--Projets-Projet-A/<sid>.jsonl` et
`C--Projets-Projet-A/<sid>/subagents/agent-….jsonl`, dates d'écriture posées par `File.SetLastWriteTimeUtc`.
