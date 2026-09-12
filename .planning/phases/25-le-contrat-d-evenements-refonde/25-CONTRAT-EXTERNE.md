# Phase 25 — Le contrat externe des hooks, vérifié indépendamment

**Relevé : 2026-09-12.** Source : référence officielle des hooks Claude Code
(`code.claude.com/docs/en/hooks`, `/hooks-guide`, `/settings-reference`).
**Confirmation indépendante** du relevé d'enquête du même jour — pas un écho.

> **Limite de la source, à reporter dans `docs/` (EVT-05).** La page de référence est **tronquée à la
> récupération**, juste avant les sections par événement qui portent les schémas JSON de stdin. Tout ce qui
> suit vient de tables obtenues **mot pour mot et identiques sur plusieurs lectures** (cycle de vie,
> matchers, champs communs). **Aucun nom de champ spécifique à un événement n'est confirmé** — deux lectures
> se sont contredites sur `SessionEnd` (`end_reason` vs `session_end_reason`) et sur `PermissionRequest`
> (`permission_request` vs `permission_context`). Ne rien construire sur ces noms-là.

---

## Ce que l'enquête avait JUSTE

| Point | Statut |
|---|---|
| `PermissionRequest` existe, nom exact, dédié à la demande de permission | **CONFIRMÉ** — « When a tool call needs a permission decision » |
| Chronos câble 5 événements sur un catalogue d'environ 30 | **CONFIRMÉ** — le catalogue en compte **33** |
| `SessionEnd` n'est pas garanti sur terminal tué / crash / redémarrage | **CONFIRMÉ comme NON DOCUMENTÉ** (voir ci-dessous) |
| `Notification` ne doit pas être traité comme un état | **CONFIRMÉ, mais pour une autre raison** (voir ci-dessous) |

## Ce que l'enquête avait FAUX — trois corrections

### 1. `Notification` n'est PAS une « alerte d'absence »

C'est un **bus de notifications généraliste** : « When Claude Code sends a notification », que l'on **filtre
par `matcher`**. Douze valeurs confirmées :

```
permission_prompt   idle_prompt          auth_success
elicitation_dialog  elicitation_url_dialog  elicitation_complete
elicitation_response  agent_needs_input   agent_completed
quota_auto_resume_fired  quota_auto_resume_stale  quota_auto_resume_disabled
```

`auth_success` et les `quota_auto_resume_*` n'ont **rien** à voir avec l'absence de l'utilisateur : la
description « l'utilisateur semble parti » ne tient pas. Le seuil de **60 s** n'est **pas confirmé**.

**Conséquence pour EVT-02 — elle change de nature, pas de conclusion.** Le défaut n'est pas « un proxy
d'absence pris pour un état », c'est **un bus entier réduit à un seul état** : aujourd'hui `Notification`
→ `WaitingAttention` **sans distinguer le type**, donc `auth_success` et une reprise de quota fabriquent une
attente. Le remède devient plus simple et plus sûr que prévu : **filtrer par `matcher`** au lieu de lire un
champ dont le nom n'est pas confirmable.

### 2. `StopFailure` ne couvre PAS l'interruption utilisateur

`StopFailure` = « When the turn ends due to an **API error** », et ses valeurs de matcher le prouvent :
`rate_limit`, `overloaded`, `authentication_failed`, `billing_error`, `max_output_tokens`, `server_error`…
**Aucune** ne correspond à une annulation humaine.

**Et AUCUN des 33 événements ne mentionne l'interruption utilisateur.** Les termes « interrupt »,
« Escape », « cancel » sont **absents de la page**.

**Conséquence pour EVT-04, majeure.** Que `Stop` soit muet sur Échap reste **plausible et non confirmable** :
c'est un **trou documentaire**, pas une réponse. EVT-04 ne peut donc pas être livré par le câblage d'un
événement dédié — il n'en existe pas. Il ne peut être que **déduit**. Or la doctrine du milestone interdit
de présenter comme observé ce qui n'a été que déduit : **l'état livré doit être celui que l'inférence
autorise, et le dire.** C'est une contrainte de conception, pas un détail de formulation.

### 3. `SessionEnd.reason` a CINQ valeurs, pas quatre

```
clear   resume   logout   prompt_input_exit   other
```

**`resume` manquait au relevé.** Ces valeurs sont documentées comme **valeurs de matcher** — voie sûre,
puisque le nom du champ n'est pas confirmable.

---

## Faits neufs, décisifs pour la conception

### A. Il n'existe AUCUN battement de cœur périodique

Pas de tick horloge dans le catalogue. Candidats, du plus fin au plus grossier :

| Événement | Déclenchement | `matcher` |
|---|---|---|
| `MessageDisplay` | « While assistant message text is displayed » — pendant le streaming | **non supporté** |
| `PreToolUse` / `PostToolUse` | une fois par appel d'outil (avant / après succès) | nom d'outil |
| `PostToolUseFailure` | après un appel d'outil en échec | nom d'outil |
| `PostToolBatch` | « After a full batch of parallel tool calls resolves » | **non supporté** |

`PreToolUse`/`PostToolUse` sont des preuves de vie **imparfaites** : entre le `PreToolUse` d'un build de
10 minutes et son `PostToolUse`, **rien n'arrive**. Idem pendant une longue réflexion sans outil.
`MessageDisplay` est le battement le plus fin, **au prix d'un volume d'invocations élevé** — le handler doit
être trivial. C'est exactement pourquoi la phase 23 (écriture directe, 0 perte sur 500) est une
**dépendance** de celle-ci.

**Le battement ne supprime pas le besoin d'expiration — il le raccourcit.** `SessionEnd` reste un raccourci
de nettoyage propre, jamais une garantie.

### B. ⚠ LE PIÈGE DES SOUS-AGENTS — c'est SRC-03 qui réapparaît côté hooks

> Les hooks d'un **sous-agent** portent le **MÊME `session_id`** que la session parente. Ils s'en
> distinguent **uniquement** par la présence de `agent_id` / `agent_type`.

Sur cette machine, **94 % des transcripts sont des sous-agents** (817 `agent-*.jsonl` sur 868). La phase 21
a réglé le problème **côté transcripts** (`Take(12)` déplacé après le filtre `isSidechain`). **Côté hooks, le
même piège est intact** : un `/gsd:execute-phase` qui lance 5 sous-agents produira 5 fois plus de battements
pour **une seule** session — et si le battement le plus récent vient d'un sous-agent qui travaille encore, la
session parente paraîtra vivante alors qu'elle ne l'est peut-être plus.

**Tout battement de cœur DOIT être filtré sur l'absence de `agent_id` / `agent_type`.** Sans ce filtre,
EVT-03 fabrique une observation fausse — précisément ce que le milestone interdit.

### C. Champs COMMUNS à tous les événements (confirmés)

| Champ | Note |
|---|---|
| `session_id` | **présent partout** |
| `hook_event_name` | nom de l'événement déclencheur |
| `cwd` | répertoire courant à l'invocation |
| `transcript_path` | **écrit de façon asynchrone, peut être en retard** |
| `prompt_id` | UUID du prompt ; absent avant la 1re saisie ; **v2.1.196+** |
| `scratchpad_dir` | **v2.1.257+**, absent si pas de scratchpad |
| `permission_mode` | `default`, `plan`, `acceptEdits`, `auto`, `dontAsk`, `bypassPermissions` — « Not all events receive this field » |
| `effort` | objet `{level}`, seulement en contexte d'outil |
| `agent_id` / `agent_type` | **présents UNIQUEMENT dans un sous-agent** — voir B |

`session_id` et `cwd`, que `SessionHookProcessor` lit déjà, sont donc sûrs.

### D. Le catalogue complet — 33 événements

**Session :** `SessionStart`, `Setup`, `SessionEnd`
**Tour :** `UserPromptSubmit`, `UserPromptExpansion`, `Stop`, `StopFailure`
**Outils :** `PreToolUse`, `PermissionRequest`, `PermissionDenied`, `PostToolUse`, `PostToolUseFailure`, `PostToolBatch`
**Affichage :** `Notification`, `MessageDisplay`
**Sous-agents / tâches :** `SubagentStart`, `SubagentStop`, `TaskCreated`, `TaskCompleted`, `TeammateIdle`
**Contexte / config :** `InstructionsLoaded`, `ConfigChange`, `PreCompact`, `PostCompact`, `PreModelSwitch`, `PostModelSwitch`
**Fichiers :** `CwdChanged`, `DirectoryAdded`, `FileChanged`, `WorktreeCreate`, `WorktreeRemove`
**MCP :** `Elicitation`, `ElicitationResult`

`PermissionDenied` ne couvre **que les refus automatiques du mode `auto`**, pas un refus humain au prompt.
**Il n'existe aucun `PermissionGranted`.**

### E. ⚠ Un nom d'événement inconnu : sort NON DOCUMENTÉ

Les termes « unrecognized », « unknown event », « typo » sont **absents de la page**. Le guide a
explicitement **rejeté sa propre affirmation** « silently ignored » comme fabriquée par extrapolation.

**C'est notre mode de défaillance le plus dangereux** : un nom mal orthographié pourrait ne produire
**aucune erreur visible** et laisser un hook mort. **Parade obligatoire : valider chaque clé d'événement
contre une liste blanche des 33 noms, codée en dur, AVANT d'écrire dans `settings.json`.** Ne jamais
compter sur un avertissement de Claude Code.

### F. Règles de `matcher` — et deux pièges de version

Structure : `hooks` → nom d'événement → groupe (`matcher` + `hooks[]`) → handler (`type`, `command`).

| Valeur de `matcher` | Évaluée comme |
|---|---|
| `"*"`, `""`, ou **omis** | tout |
| lettres, chiffres, `_`, `-`, espaces, `,`, `\|` | chaîne exacte, ou liste séparée par `\|` / `,` |
| tout autre caractère | **expression régulière JavaScript, NON ANCRÉE** |

- Le `matcher` n'est **jamais obligatoire**.
- « If you add a `matcher` field to an event **without matcher support**, it is **silently ignored**. »
  **Sans support :** `UserPromptSubmit`, `PostToolBatch`, `Stop`, `TeammateIdle`, `TaskCreated`,
  `TaskCompleted`, `WorktreeCreate`, `WorktreeRemove`, `MessageDisplay`, `CwdChanged`.
- Piège documenté : `Edit.*` attrape **aussi** `NotebookEdit` — ancrer avec `^…$`.
- **Versions minimales** : séparateur virgule **v2.1.191+** ; tiret dans le jeu exact **v2.1.195+** (avant,
  `code-reviewer` part en regex non ancrée et attrape `senior-code-reviewer`).

---

## Ce que ce relevé N'AUTORISE PAS

- **Aucun nom de champ spécifique à un événement** n'est confirmé (sections tronquées). Passer par les
  **valeurs de `matcher`** partout où c'est possible, plutôt que lire un champ dont le nom est incertain.
- **Aucune installation de sonde de capture dans `~/.claude/settings.json`.** Le guide en proposait une pour
  relever les schémas réels ; c'est la **configuration vivante de l'utilisateur** et cela sort du périmètre
  autorisé. Si le besoin devient bloquant, **le demander à l'utilisateur**, ne pas le faire d'office.
- Trois **trous documentaires réels** subsistent — comportement sur Échap, garantie de `SessionEnd` sur
  crash, sort d'un nom d'événement inconnu. Ils doivent figurer **tels quels** dans le document EVT-05, avec
  leur date de relevé : c'est précisément ce qui rendra la prochaine dérive détectable.
