# Fixtures — une question `AskUserQuestion` dans un transcript réel

Deux fichiers **réels**, relevés le 2026-09-25 (phase 28, plan 01) dans
`~/.claude/projects/C--…-PROJET-JARVIS/939eb30a-8200-4c6e-b89f-49ef26260e92.jsonl` (5,7 Mo, session de l'app
bureau du 2026-09-24), en **lecture seule** : la date d'écriture de la source a été relevée avant et après
l'extraction (`stat -c %Y` = 1790348300 dans les deux cas). Une fixture régénérée par sérialisation ne
prouverait rien (précédent DEL-06) : ces lignes viennent du disque, elles ont été **réduites** et
**anonymisées**, rien n'y a été ajouté.

Les lignes ont été repérées par leur `timestamp`, pas par leur numéro (le fichier peut grandir) :

| Ligne source | Type | `timestamp` | Rôle dans la fixture |
|---|---|---|---|
| 154 | `user` (`tool_result`) | `2026-09-24T06:54:55.718Z` | la réponse d'outil qui précède la réflexion |
| 155 | `attachment` | `2026-09-24T06:54:55.727Z` | ligne horodatée NON significative |
| 156 | `assistant` (bloc `thinking` seul) | `2026-09-24T06:55:24.745Z` | réflexion |
| 157 | `assistant` (bloc `thinking` seul) | `2026-09-24T06:55:24.752Z` | réflexion |
| 158 | `assistant` (`tool_use` **`AskUserQuestion`**) | `2026-09-24T06:55:33.967Z` | **la question** |
| 159-163 | `last-prompt`, `custom-title`, `agent-name`, `atis-latch`, `bridge-session` | **aucun** | métadonnées écrites PENDANT que la question attend |
| 164 | `user` (`tool_result`) | `2026-09-24T06:59:44.051Z` | **la réponse** de l'utilisateur |

| Fichier | Lignes | Ce qu'il couvre |
|---|---|---|
| `question-en-suspens.jsonl` | 154 à 163 (10) | la question est à l'écran : attente, au rang des questions (LIB-02) ; les métadonnées écrites après elle ne la rajeunissent pas (D-28-01) |
| `question-repondue.jsonl` | 154 à 164 (11) | la même séquence suivie de la réponse réelle : la session redevient une réflexion |

## Ce qui a été retiré

- Lignes `user` / `assistant` / `attachment` : seules les clés `type`, `isSidechain`, `timestamp`, `cwd`,
  `sessionId`, `message` sont gardées ; dans `message` : `role`, `stop_reason`, `content` ; dans chaque bloc de
  `content`, celles présentes parmi `type`, `id`, `name`, `input`, `caller`, `tool_use_id`, `content`,
  `is_error`, `thinking`, `signature`, `text`. Tout le reste est RETIRÉ (`toolUseResult`, `uuid`,
  `parentUuid`, `promptId`, `requestId`, `usage`, `model`, `attachment`, `rendered`, `version`, `gitBranch`…).
- La ligne `attachment` n'a pas de `message` : il n'en reste que sa forme horodatée, ce qui est le fait utile
  (une ligne horodatée qui n'est pas un message).

## Ce qui a été anonymisé

- Toute valeur textuelle libre devient `"(anonymisé)"` : textes des questions, `header`, `label`,
  `description`, `thinking`, `signature`, `content` des `tool_result`.
- `cwd` devient `"C:\Projets\Projet-J"` (le projet lu par Chronos est donc « Projet-J »).
- Conservés tels quels : `type`, `name`, `id`, `tool_use_id`, `role`, `stop_reason`, `timestamp`,
  `sessionId`, `isSidechain`, `multiSelect`, `caller`, `lastSequenceNum`, et les booléens (`is_error`).
- Une chaîne VIDE dans la source reste vide (`thinking` de la ligne 156, `atis` de la ligne 162) : elle ne
  porte aucune information, la remplacer serait inventer un contenu.

## Pourquoi les métadonnées gardent leur forme

Les lignes 159 à 163 gardent TOUTES leurs clés (valeurs textuelles anonymisées, sauf `type` et `sessionId`) :
leur forme **sans horodatage** est précisément le fait testé. L'app bureau écrit ces lignes pendant qu'une
question attend et à sa fermeture (relevé du 2026-09-25, 16 h 58 : douze transcripts rajeunis d'un bloc) ;
elles font avancer la date d'écriture du fichier sans être un message. Décision D-28-01 : un transcript est
daté par le `timestamp` de sa dernière ligne `user` / `assistant`, jamais par la date d'écriture de son fichier.

Format : une ligne JSON compacte par ligne, fin de ligne `\n`, UTF-8 sans BOM. Chargement par `[CallerFilePath]`
puis COPIE dans une racine temporaire (`TranscriptQuestionTests`) : la source lit un dossier de projets.
