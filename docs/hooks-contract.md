# Chronos — Le contrat des hooks Claude Code

> **Relevé le 2026-09-12** — source : référence officielle des hooks Claude Code
> (`code.claude.com/docs/en/hooks`, `/hooks-guide`, `/settings-reference`), lue et **confirmée deux fois**,
> la seconde lecture étant indépendante de la première et non un écho.
>
> **⚠ Limite de la source.** La page de référence est **TRONQUÉE à la récupération**, juste avant les
> sections par événement qui portent les schémas JSON de `stdin`. Tout ce qui figure ici provient de tables
> obtenues **mot pour mot et identiques sur plusieurs lectures** (cycle de vie, règles de `matcher`, champs
> communs). **Aucun nom de champ spécifique à un événement n'est confirmé** — deux lectures se sont
> contredites sur le motif de fin de session (`end_reason` contre `session_end_reason`) et sur le contexte
> de permission (`permission_request` contre `permission_context`). **Ne rien construire sur ces noms-là.**

Ce document décrit **ce qui est réellement câblé** par Chronos, ce qu'il lit, ce qu'il en produit — et,
au moins autant, **ce qui n'est pas garanti**. Il est le pendant de `data-sources.md` pour la seconde
source de Chronos : les hooks.

**Pourquoi il existe.** Son absence est précisément ce qui a laissé une sémantique fausse survivre des mois
sans que personne ne puisse la contredire : un bus de notifications réduit à un seul état, un proxy préféré
à l'événement dédié, une interruption jamais couverte. Rien ne le disait, donc rien ne pouvait le démentir.
Ce document est écrit pour que la **prochaine** dérive soit détectable — voir §9.

**Rappel de terrain, valable pour tout ce qui suit.** La configuration des hooks est lue au **DÉMARRAGE**
d'une session Claude Code. Seules les sessions ouvertes **après** une réconciliation de
`~/.claude/settings.json` sont suivies — et cette réconciliation n'a lieu qu'au **lancement de l'overlay**,
en **mode overlay uniquement** (les modes `--hook` et `--statusline` sortent bien avant), et seulement si le
widget de sessions est activé. Republier l'exe ne suffit donc pas : il faut le relancer, puis rouvrir les
sessions.

---

## 1. Les événements câblés

Huit entrées, recopiées de `SessionHookInstaller.Cablage` — la **source de vérité unique**. Ajouter un
événement, c'est ajouter une entrée `EvenementCable` et **rien d'autre** : `Events` en dérive, et cette
table est comparée au câblage par `ContratHooksDocumenteTests`.

Chaque groupe installé appelle `"<chemin>/Chronos.exe" --hook <Événement>` — chemin en **slashes avant**
(des antislashs seraient avalés par le shell ; leçon vérifiée sur la vraie machine).

<!-- EVENEMENTS-CABLES:debut -->

| Événement | `matcher` | Ce que Chronos en produit | `timeout` |
|---|---|---|---|
| `SessionStart` | (aucun) | la session démarre → en cours | 10 |
| `UserPromptSubmit` | (aucun) | un message est envoyé → en cours | 10 |
| `Stop` | (aucun) | le tour se termine → tour fini | 10 |
| `SessionEnd` | (aucun) | fin de session → le fichier d'état est supprimé | 10 |
| `PermissionRequest` | (aucun) | une permission est DEMANDÉE → à toi (EVT-01) | 10 |
| `Notification` | `agent_needs_input\|elicitation_dialog\|elicitation_url_dialog` | les TROIS types du bus qui sont de vraies demandes → à toi (EVT-02) | 10 |
| `PreToolUse` | (aucun) | un outil va être appelé → battement de cœur : en cours (EVT-03) | 3 |
| `PostToolUse` | (aucun) | un outil vient de réussir → battement de cœur : en cours (EVT-03) | 3 |

<!-- EVENEMENTS-CABLES:fin -->

**Le `matcher` de `Notification` est sûr par construction :** il ne contient que des lettres, des tirets bas
et des barres verticales, donc il est évalué comme une **liste exacte** et non comme une expression
régulière non ancrée (§7). On n'entre pas dans le piège documenté.

**La purge est LARGE, l'installation reste CIBLÉE.** Chronos balaie *toutes* les clés de `hooks` pour
retirer les siennes — sinon un groupe posé sur un événement qu'on cesse de câbler survivrait pour toujours,
invisible. Ses groupes sont repérés par **marqueur d'argument `--hook` ET nom de fichier `Chronos*.exe`**,
**jamais par la clé** : les groupes d'autres outils qui partagent `PreToolUse` / `PostToolUse` ne sont ni
touchés, ni comptés comme nôtres, et un test le prouve sur la configuration réelle.

---

## 2. Les champs lus sur `stdin`, et leur degré de confiance

### Confirmés comme COMMUNS à tous les événements

| Champ | Note |
|---|---|
| `session_id` | présent partout — sans lui, Chronos ignore l'événement |
| `hook_event_name` | nom de l'événement déclencheur ; sert de repli quand l'argument manque |
| `cwd` | répertoire courant à l'invocation ; le dernier segment devient le nom de projet affiché |
| `transcript_path` | **écrit de façon ASYNCHRONE, peut être en retard** — non lu par le chemin des hooks |
| `agent_id` / `agent_type` | **présents UNIQUEMENT dans un sous-agent** — voir §4 |

Chronos lit `session_id`, `cwd`, `hook_event_name`, `agent_id` et `agent_type`. Ce sont les seuls champs
confirmés dont il dépend.

### Lu mais NON confirmé — et volontairement réduit à un VETO

`notification_type` est lu sur `Notification`, et **uniquement pour ÉCARTER**. Il ne produit **jamais** un
état ; il peut seulement en retirer un. Neuf des douze types du bus sont vetoés (`permission_prompt`,
`idle_prompt`, `auth_success`, `elicitation_complete`, `elicitation_response`, `agent_completed`, et les
trois `quota_auto_resume_*`).

**Pourquoi cette asymétrie est le cœur de la conception.** Le nom de ce champ n'est pas confirmable. S'il
était renommé ou disparaissait, la lecture rendrait une chaîne vide et Chronos retomberait simplement sur le
tri par `matcher` — **sans jamais fabriquer une attente**. Un champ lu pour *conclure* aurait, lui,
silencieusement produit ou perdu des états le jour du renommage. Le `matcher` est le filtre **primaire** ;
ce champ n'est que le **second rideau**, utile tant que des configurations antérieures, encore sans
`matcher`, survivent chez l'utilisateur.

Corollaire écrit dans le code : un marqueur **illisible** (absent, vide, non textuel) ne fabrique jamais un
veto. On ne conclut pas depuis un champ qu'on n'a pas lu.

### Jamais lus, délibérément

**Tout champ spécifique à un événement.** Le motif inscrit dans le fichier d'état est donc le **nom de
l'événement** (`PermissionRequest`, `PreToolUse`, `PostToolUse`) — un fait **observé**, puisque c'est nous
qui l'avons câblé — et non un champ de contexte dont deux lectures du relevé se sont contredites sur le nom.
Seul `Notification` inscrit `notification_type` comme motif, lorsqu'il est lisible.

---

## 3. Les états produits

Cinq valeurs de `SessionActivity`. Le fichier d'état (`%APPDATA%\Chronos\sessions\<id>.json`) porte
`session_id`, `project`, `activity`, `reason` (optionnel) et `updated_at`.

| `activity` | Libellé affiché | Ce que l'état AFFIRME | Observé ou déduit ? |
|---|---|---|---|
| `Working` | `en cours` | un signal d'activité est arrivé **à cet instant-là** — jamais « ça travaille encore maintenant » | **observé** |
| `WaitingTurn` | `tour fini` | `Stop` est arrivé : le tour s'est réellement terminé | **observé** |
| `WaitingAttention` | `à toi` | une permission a été demandée, ou le bus a porté une vraie demande | **observé** |
| `WaitingDeduced` | `à toi ? déduit` | la session travaillait, et **plus aucun battement n'arrive** depuis le seuil de silence | **DÉDUIT — jamais observé** |
| `Unknown` | `inconnu` | signal illisible ou indéterminé ; n'est jamais présenté comme une attente | ni l'un ni l'autre |

**`WaitingDeduced` n'est JAMAIS écrite dans un fichier d'état.** Elle est dérivée **à la lecture**, par
`SessionMonitor`, et en un seul endroit : `Working` dont le dernier battement dépasse
`SilenceDesBattements` (**vingt minutes**). Au-delà de `DropAfter` (**huit heures**), un état n'est plus lu
du tout. L'interrogation et le mot « déduit » sont dans le **libellé** — pas dans un commentaire, pas dans
une documentation : dans les mots que l'utilisateur lit. C'est la doctrine du milestone, appliquée à
l'endroit où elle se vérifie.

Son rang d'urgence est **3**, derrière `Working` (2) : **une déduction ne passe jamais devant une
observation.**

### ⚠ Divergence connue — transmise à la phase 26, NON corrigée ici

`SessionTreatmentTracker` (`src/Chronos/Services/SessionTreatmentTracker.cs`, prédicat `IsWaiting`,
l. 38) ne reconnaît comme attentes que `WaitingTurn` et `WaitingAttention` : **`WaitingDeduced` en est
exclue**, alors que `SessionsViewModel.WaitingCount` la **compte** comme attente.

Conséquence concrète : une session déduite **n'ouvre pas d'épisode d'attente** pour le suivi de « traité ».
Elle est comptée dans le bandeau d'alerte, mais le détecteur d'hystérésis ne la voit pas passer en attente,
donc il ne la marquera pas « traitée » quand elle en sortira.

**Elle n'est pas corrigée dans cette phase, et c'est délibéré** : la phase 26 redéfinit précisément ce que
« traité » veut dire. Corriger le tracker ici, ce serait anticiper cette phase à moitié. Le diff de
`SessionTreatmentTracker.cs` est **vide** sur toute la phase 25, vérifié. **Entrée ouverte de la phase 26.**

---

## 4. Le piège des sous-agents

> Les hooks d'un **sous-agent** portent le **MÊME `session_id`** que la session parente. Ils ne s'en
> distinguent **que** par la présence de `agent_id` / `agent_type`.

Sur cette machine, **quatre-vingt-quatorze pour cent** des transcripts sont des sous-agents (817
`agent-*.jsonl` sur 868). Sans filtre, une vague d'agents parallèles réaffirmerait « en cours » sur une
session parente qui n'y est plus — et écraserait un « à toi » encore en attente.

**La règle livrée :** un sous-agent ne parle **jamais** de l'activité ni du cycle de vie de son parent ; il
peut seulement **réclamer une intervention**. Concrètement, tout événement portant l'un des deux marqueurs
est ignoré, **sauf** `PermissionRequest` et `Notification`.

Le **placement** du veto est essentiel, et c'est un choix, pas un hasard : il est posé **après** la garde
`session_id` et **avant** le court-circuit `SessionEnd`. Un `SessionEnd` de sous-agent ne doit jamais
supprimer le fichier d'état de son parent.

---

## 5. CE QUI N'EST PAS GARANTI

**La section la plus importante de ce document.** Tout ce qui suit a été relevé le **2026-09-12** sur la
référence officielle des hooks Claude Code (`code.claude.com/docs/en/hooks`, `/hooks-guide`,
`/settings-reference`). Ce ne sont pas des suppositions sur le comportement du produit : ce sont des
**trous documentaires** constatés, c'est-à-dire des questions auxquelles la source **ne répond pas**.

### 5.1 — L'interruption au clavier (Échap) : `Stop` muet, plausible et NON CONFIRMABLE

**Relevé le 2026-09-12.** Source : page de référence des hooks. **Aucun des 33 événements du catalogue ne
mentionne l'interruption utilisateur.** Les termes « interrupt », « Escape » et « cancel » sont **absents de
la page**. `StopFailure` ne couvre **que** les erreurs d'API — ses valeurs de `matcher` le prouvent
(`rate_limit`, `overloaded`, `authentication_failed`, `billing_error`, `max_output_tokens`,
`server_error`) : aucune ne correspond à une annulation humaine.

Que `Stop` soit **muet** lorsque l'utilisateur appuie sur Échap reste donc **plausible et non
confirmable**. C'est un trou, pas une réponse.

**Conséquence livrée.** Il n'existait rien à câbler : l'état ne peut être que **DÉDUIT** du silence des
battements, et le libellé le dit (`à toi ? déduit`, §3). **Latence assumée : vingt minutes** — le seuil de
silence. La même signature (plus aucun battement) vaut aussi pour un terminal tué, une mise en veille ou un
outil anormalement long : la cause n'est donc **jamais nommée**.

### 5.2 — `SessionEnd` sur terminal tué, crash ou redémarrage : NON DOCUMENTÉ

**Relevé le 2026-09-12**, même source. `SessionEnd.reason` a **cinq** valeurs documentées — `clear`,
`resume`, `logout`, `prompt_input_exit`, `other` — et **aucune ne couvre** un terminal tué, un crash ou un
redémarrage de la machine. Rien, sur la page, ne documente une garantie d'émission dans ces cas.

**Conséquence livrée.** `SessionEnd` reste un **raccourci de nettoyage propre**, jamais une garantie. Le
vrai filet est le **balayage d'expiration de la phase 23** et son seuil de **huit heures** (`DropAfter`) :
au-delà, un état n'est plus lu, quelle que soit la raison de sa survie.

**Et — c'est le point à retenir — le battement de cœur ne SUPPRIME pas le besoin d'expiration : il le
RACCOURCIT.** Une session morte sans `SessionEnd` cesse de se dire « en cours » au bout de vingt minutes de
silence au lieu de huit heures. Elle ne disparaît pour autant qu'à huit heures.

### 5.3 — Le sort d'un nom d'événement INCONNU : NON DOCUMENTÉ

**Relevé le 2026-09-12**, même source. Les termes « unrecognized », « unknown event » et « typo » sont
**absents de la page**. Le guide a explicitement **rejeté sa propre affirmation** « silently ignored »
comme **fabriquée par extrapolation** — ce qui est une information, et pas une absence d'information : nous
savons que nous ne savons pas.

**C'est notre mode de défaillance le plus dangereux** : un nom mal orthographié produirait un hook **mort
et muet**, sans aucune erreur visible.

**Conséquence livrée.** Une **liste blanche des 33 noms**, codée en dur
(`CatalogueEvenementsHooks.Tous`, §8), consultée **AVANT toute écriture** dans `settings.json`. Un nom
absent du catalogue n'est pas installé. Un `matcher` posé sur un événement qui n'en accepte pas n'est pas
installé non plus — une configuration morte ne s'installe pas. On ne compte **jamais** sur un avertissement
de Claude Code.

### 5.4 — La limite ASSUMÉE des battements de cœur (EVT-03)

**Mesurée et écrite au plan 25-02.** Il **n'existe aucun battement périodique** dans le catalogue : pas de
tick horloge à câbler. `PreToolUse` / `PostToolUse` sont donc une preuve de vie **imparfaite** — entre le
signal d'entrée d'un build de dix minutes et son signal de sortie, **rien n'arrive** ; idem pendant une
longue réflexion sans appel d'outil.

**Donc : un outil unique de plus de vingt minutes fera franchir le seuil de silence à une session qui
travaille encore**, et elle s'affichera « à toi ? déduit ».

**C'est une LIMITE, pas un défaut à corriger.** Elle est la conséquence directe de l'absence de battement
périodique dans le catalogue, et le seuil ne peut pas la résoudre : le baisser multiplierait les fausses
déductions, le monter rendrait le silence invisible. **Si le cas se présente souvent, c'est le choix de
l'ÉVÉNEMENT PORTEUR qu'il faut rouvrir — pas le seuil.**

### 5.5 — Ce que ce relevé n'autorise pas, en une ligne

Aucun **nom de champ spécifique à un événement** n'est confirmé : passer par les **valeurs de `matcher`**
partout où c'est possible, plutôt que lire un champ dont le nom est incertain.

---

## 6. Ce que nous avons délibérément ÉCARTÉ, et pourquoi

Sans cette section, un successeur recâblerait ce que nous avons refusé.

| Écarté | Raison |
|---|---|
| `MessageDisplay` comme battement | C'est le battement le **plus fin** du catalogue — et il se déclenche **pendant le streaming du texte** : un processus Chronos **par fragment affiché**. Écarté pour son **COÛT**, pas pour sa précision. Un battement ne doit pas coûter plus cher que ce qu'il observe. |
| `permission_prompt` du bus `Notification` | L'événement **dédié** `PermissionRequest` en a seul la charge : il est exact et immédiat. Deux chemins pour un même fait rendraient la source illisible. Le type est **absent du `matcher`** ET vetoé au routage. |
| `idle_prompt` | Une alerte d'**ABSENCE** de l'utilisateur. Au mieux un indice sur lui, **jamais** une vérité sur ce que fait la session. Le seuil de 60 s qu'on lui prêtait n'est d'ailleurs **pas confirmé**. |
| Un `timeout` **uniforme** de dix secondes | Voir ci-dessous. |
| Les ~25 autres événements du catalogue (`StopFailure`, `PreCompact`, `TeammateIdle`, `SubagentStart`, `PostToolBatch`…) | Hors périmètre : cette phase câble ce que les cinq critères exigent, **pas le catalogue**. Ils restent dans la liste blanche (§8) pour qu'un câblage futur soit *validable*, pas parce qu'ils sont prévus. |
| Une **sonde de capture** dans `~/.claude/settings.json` | Voir la question ouverte ci-dessous. |

### Pourquoi deux `timeout` et pas un

Les deux battements portent **`timeout: 3`**, les six autres entrées **`10`**. Ce n'est pas un réglage
cosmétique : **`PreToolUse` est BLOQUANT** — Claude Code attend la fin du hook avant de lancer l'outil. Dix
secondes de gel potentiel sur **chaque** appel d'outil coûteraient infiniment plus cher que le battement ne
rapporte, alors que l'écriture d'un fichier d'état se compte en millisecondes. Les six entrées de cycle de
vie, elles, sont rares et jamais dans le chemin critique d'un outil : elles gardent dix secondes.

Ce budget de trois secondes a une conséquence en aval, qu'il faut connaître avant de la rouvrir : les
battements créent des **écrivains concurrents** sur le même fichier d'état (un batch d'appels d'outil
parallèles). La parade livrée est une **reprise BORNÉE** avec cession de la main (soixante essais au plus),
et non un élargissement du partage en écriture — deux écrivains entrelacés produiraient un **fragment** là
où il y avait un état, et un fragment est une **absence** de signal. Le budget de reprise est dimensionné
pour un délai de grâce de **trois secondes**, pas davantage.

### ⚠ Question OUVERTE, à poser à l'utilisateur

Relever les **schémas réels de `stdin`** — donc les noms de champs spécifiques à chaque événement, que la
page tronquée ne donne pas — exigerait d'installer une **sonde de capture** temporaire dans
`~/.claude/settings.json`. C'est sa **configuration vivante**.

**Ce n'est pas fait, ce n'est pas planifié, et ce ne sera pas fait d'office.** Si le besoin devient
bloquant, **le lui demander**. Tant que la réponse est non, les noms de champs spécifiques restent non
confirmables et le code ne doit pas s'y appuyer.

---

## 7. Règles de `matcher`, telles que relevées

Structure d'un réglage : `hooks` → nom d'événement → groupe (`matcher` + `hooks[]`) → handler (`type`,
`command`, `timeout`).

| Valeur de `matcher` | Évaluée comme |
|---|---|
| `"*"`, `""`, ou **omis** | tout |
| lettres, chiffres, `_`, `-`, espaces, `,`, barre verticale | **chaîne exacte**, ou liste séparée par barre verticale / virgule |
| tout autre caractère | **expression régulière JavaScript, NON ANCRÉE** |

- Le `matcher` n'est **jamais obligatoire**.
- Piège documenté : `Edit.*` attrape **aussi** `NotebookEdit`. Ancrer avec `^…$` si l'on entre en regex.
- **Un `matcher` posé sur un événement qui n'en accepte pas est SILENCIEUSEMENT IGNORÉ** : le groupe
  installé ne ferait alors pas ce qu'il annonce. D'où le refus d'écrire un tel groupe (§5.3).
- **Les dix événements SANS support de `matcher`** : `UserPromptSubmit`, `Stop`, `PostToolBatch`,
  `MessageDisplay`, `TaskCreated`, `TaskCompleted`, `TeammateIdle`, `CwdChanged`, `WorktreeCreate`,
  `WorktreeRemove`.
- **Deux pièges de VERSION** : le séparateur **virgule** n'existe qu'à partir de la version **2.1.191** ;
  le **tiret** n'entre dans le jeu de la chaîne exacte qu'à partir de la **2.1.195** (avant,
  `code-reviewer` part en regex non ancrée et attrape `senior-code-reviewer`).

---

## 8. Le catalogue complet — 33 événements

La liste blanche codée en dur (`CatalogueEvenementsHooks.Tous`), groupée comme dans le relevé.

- **Session :** `SessionStart`, `Setup`, `SessionEnd`
- **Tour :** `UserPromptSubmit`, `UserPromptExpansion`, `Stop`, `StopFailure`
- **Outils :** `PreToolUse`, `PermissionRequest`, `PermissionDenied`, `PostToolUse`, `PostToolUseFailure`,
  `PostToolBatch`
- **Affichage :** `Notification`, `MessageDisplay`
- **Sous-agents et tâches :** `SubagentStart`, `SubagentStop`, `TaskCreated`, `TaskCompleted`,
  `TeammateIdle`
- **Contexte et configuration :** `InstructionsLoaded`, `ConfigChange`, `PreCompact`, `PostCompact`,
  `PreModelSwitch`, `PostModelSwitch`
- **Fichiers :** `CwdChanged`, `DirectoryAdded`, `FileChanged`, `WorktreeCreate`, `WorktreeRemove`
- **MCP :** `Elicitation`, `ElicitationResult`

Deux précisions du relevé, utiles à qui voudrait câbler la permission autrement : `PermissionDenied` ne
couvre **que les refus automatiques du mode `auto`**, pas un refus humain au prompt — et **il n'existe
aucun `PermissionGranted`**.

---

## 9. Comment détecter une dérive

1. **Relire** la page de référence officielle des hooks Claude Code.
2. **Comparer** à §1 (ce que nous câblons), §7 (les règles de `matcher`) et §8 (le catalogue).
3. **Si un écart apparaît, le DATER ici — avant de toucher au code.** L'écart est l'information ; le
   correctif n'en est que la conséquence.

**Ce qu'une machine garantit.** `ContratHooksDocumenteTests` échoue dès que la table du §1 cesse de décrire
le câblage réel : le nom, le `matcher` **et le rôle** de chaque entrée y sont comparés à
`SessionHookInstaller.Cablage`, et les 33 noms du §8 à `CatalogueEvenementsHooks.Tous`. Un document qui
décrit un câblage qu'il ne décrit plus est **pire** que pas de document : il fait croire qu'on sait.

**Ce qu'aucune machine ne peut garantir.** **Aucun test ne peut détecter une dérive de la source EXTERNE.**
Si Claude Code renomme un événement, change la sémantique de `Stop` ou comble l'un des trois trous du §5,
tous nos tests resteront verts et ce document deviendra faux en silence. Seule une **relecture humaine** de
la référence officielle peut le voir — et c'est exactement pour cela que la **date du relevé figure en
tête** de ce document, et dans le §5 lui-même.
