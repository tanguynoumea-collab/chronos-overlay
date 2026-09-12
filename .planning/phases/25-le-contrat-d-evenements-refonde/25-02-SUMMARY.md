---
phase: 25-le-contrat-d-evenements-refonde
plan: 02
subsystem: sessions
tags: [hooks, battements-de-coeur, veto-sous-agent, ecrivains-concurrents, seuil-de-silence, xunit]

sha_entree_de_phase: ce40e99cffe605e2baa93511e2555884aa6acdb8

requires:
  - phase: 23-l-ecriture-qui-ne-se-perd-plus
    provides: "EcritureEtatSession.Appliquer — SEUL chemin d'écriture, écriture DIRECTE, jamais tmp+Move"
  - phase: 24-l-arbitrage-par-fraicheur
    provides: "ArbitrageSessions / LectureSessions — acquis, diff VIDE dans ce plan"
  - phase: 25-le-contrat-d-evenements-refonde
    provides: "Cablage / EvenementCable / CatalogueEvenementsHooks (plan 25-01) — source de vérité unique"
provides:
  - "Veto sous-agent dans SessionHookProcessor : agent_id / agent_type font taire tout ce qui n'est pas une DEMANDE"
  - "PreToolUse et PostToolUse câblés SANS matcher et routés vers Working — les deux battements de cœur (EVT-03)"
  - "EvenementCable.Timeout : le délai cesse d'être uniforme (3 s pour les battements, 10 s pour les six autres)"
  - "EcritureEtatSession : reprise BORNÉE sur IOException — la parade aux écrivains concurrents que les battements créent"
  - "SessionMonitor.SilenceDesBattements — le seuil de vingt minutes dit enfin ce qu'il mesure"
affects: [25-03-interruption-deduite, 25-04-contrat-documente]

tech-stack:
  added: []
  patterns:
    - "Le veto sous-agent se place APRÈS la garde session_id et AVANT le court-circuit SessionEnd : un sous-agent ne doit jamais pouvoir SUPPRIMER l'état de son parent"
    - "Un marqueur illisible (vide, non textuel) ne fabrique JAMAIS un veto — la lecture tolérante ne conclut pas depuis un champ qu'elle n'a pas lu"
    - "Le motif écrit est le NOM DE L'ÉVÉNEMENT : c'est nous qui l'avons câblé, donc c'est un fait observé, là où aucun champ spécifique n'est confirmable"
    - "Écrivains concurrents : on SÉRIALISE par reprise bornée, on n'élargit jamais le partage en écriture — deux écrivains entrelacés produiraient un fragment"
    - "Un renommage qui change le SENS d'un seuil sans toucher sa VALEUR doit être motivé par un test de FRONTIÈRE, sinon il n'est que cosmétique"

key-files:
  created:
    - tests/Chronos.Tests/BattementsCoeurTests.cs
  modified:
    - src/Chronos/Services/SessionHookProcessor.cs
    - src/Chronos/Services/SessionHookInstaller.cs
    - src/Chronos/Services/EcritureEtatSession.cs
    - src/Chronos/Services/SessionMonitor.cs
    - tests/Chronos.Tests/SessionsTests.cs
    - tests/Chronos.Tests/ClaudeSettingsReconcilerTests.cs
    - tests/Chronos.Tests/InspectionSessionsTests.cs

key-decisions:
  - "Point (f) — issue (i) RETENUE (reprise sur IOException), issue (ii) ÉCARTÉE : deux écrivains entrelacés en FileShare.ReadWrite produiraient un fragment là où il y avait un état, et la phase 23 a posé qu'un fragment est une ABSENCE. On troquerait un refus visible contre une perte silencieuse."
  - "La reprise UNIQUE recommandée par le plan-checker a été implémentée, MESURÉE insuffisante (288 → 209 puis 180 refus sur 400) et remplacée par une reprise BORNÉE avec cession de la main : une reprise immédiate retente pendant que le détenteur écrit toujours."
  - "Le timeout des deux battements est 3 s et non 10 : PreToolUse est BLOQUANT, donc le délai est payé sur CHAQUE appel d'outil."
  - "Le motif des battements est le nom de l'événement, jamais un champ de contexte d'outil : aucun nom de champ spécifique à un événement n'est confirmable (page de référence tronquée)."
  - "SilenceDesBattements garde EXACTEMENT la valeur de StaleWorking (20 min) : entre le signal d'entrée et le signal de sortie d'un outil long il ne se passe rien, donc le seuil doit rester large."

metrics:
  duration: "~55 min"
  completed: 2026-09-12
  tasks: 3
  commits: 3
  tests_avant: 817
  tests_apres: 837
---

# Phase 25 Plan 02 : Les battements de cœur — « réfléchit » cesse d'être deviné

« En cours » n'est plus éteint par un seuil d'expiration : il est **réaffirmé** à chaque appel d'outil par
deux battements câblés (`PreToolUse`, `PostToolUse`), un **veto sous-agent** empêche quatre-vingt-quatorze
pour cent des transcripts de cette machine de parler à la place de leur parent, et le seuil de vingt minutes
devient — de nom, de documentation et de test — le **silence des battements**.

## SHA d'entrée de phase

`ce40e99cffe605e2baa93511e2555884aa6acdb8` — réutilisé **à l'identique** depuis le SUMMARY 25-01, comme
exigé. Tous les `git diff --stat` de ce SUMMARY le prennent pour base : un `git diff --stat` nu ne compare
que l'arbre de travail et reste MUET après un commit.

## Comptes de tests

| Mesure | Valeur |
|---|---|
| Entrée (après 25-01), remesurée avant la première ligne écrite | **817 / 0 échec / 6 s** |
| Après tâche 1 (veto sous-agent) | **826** (+9), 0 échec |
| Après tâche 2 (battements câblés, routés, parade concurrente) | **833** (+7), 0 échec |
| Après tâche 3 (seuil de silence) — **total final** | **837** (+4), 0 échec, 4 s |
| Deuxième exécution consécutive | **837 / 0 échec / 4 s** — identique |

**837 = 817 + 20.** Aucun test supprimé, aucun test désactivé.

### Écart à l'attendu indicatif (833) — justifié nominativement

`25-VALIDATION.md` posait ≈ +9 (T1), ≈ +4 (T2), ≈ +4 (T3). Mesuré : **+9 / +7 / +4**.

- **T1 : 9 mesurés contre ≈ 9 estimés — conforme**, aucun écart.
- **T3 : 4 mesurés contre ≈ 4 estimés — conforme**, ce sont exactement les quatre tests nommés au bloc (b).
- **T2 : 7 mesurés contre ≈ 4 estimés — écart de +3**, et les trois cas surnuméraires sont nommables un
  par un. L'estimation de la carte est **antérieure aux corrections du plan-checker**, qui ont ajouté deux
  tests que la carte ne pouvait pas prévoir :

  | Cas surnuméraire | D'où il vient |
  |---|---|
  | `Les_deux_battements_portent_un_timeout_court_et_les_six_autres_le_timeout_normal` | correction **A4** — « prouvé par un test nommé, pas par un grep » |
  | `Huit_ecrivains_paralleles_sur_la_meme_session_n_echouent_jamais` | correction **B6** — le test séquentiel ne peut pas voir les écrivains concurrents |
  | 2ᵉ cas de la `[Theory]` `Un_battement_de_coeur_dit_en_cours_et_nomme_l_evenement_observe` | le bloc `<behavior>` du plan spécifie **deux** battements (`PreToolUse` **et** `PostToolUse`), comptés pour 2 cas xUnit là où la carte en comptait 1 |

Détail des 20 cas ajoutés :

| Tâche | Cas | Détail |
|---|---|---|
| T1 | 9 | `[Theory]` veto 4 cas + `[Fact]` SessionEnd + `[Theory]` exceptions 2 cas + `[Fact]` non-régression des 7 routages + `[Fact]` marqueur illisible |
| T2 | 7 | `[Theory]` routage 2 cas + cadence séquentielle + cadence concurrente + sans-matcher + timeouts + hooks tiers préservés |
| T3 | 4 | les 4 `[Fact]` nommés au bloc (b) du plan |

## Étape ROUGE mesurée en T1 — **3**, et pourquoi pas 5

```
Échoué!  - échec : 3, réussite : 6, total : 9
```

Les trois noms EXACTS, tels que la sortie xUnit les rend :

| # | Test en échec |
|---|---|
| 1 | `Un_evenement_d_activite_ou_de_fin_de_tour_emis_par_un_sous_agent_est_ignore(ev: "UserPromptSubmit", marqueur: "\"agent_id\":\"a-1\"")` |
| 2 | `Un_evenement_d_activite_ou_de_fin_de_tour_emis_par_un_sous_agent_est_ignore(ev: "Stop", marqueur: "\"agent_id\":\"a-1\"")` |
| 3 | `Un_SessionEnd_de_sous_agent_ne_supprime_pas_le_fichier_d_etat_du_parent` |

**Les deux cas `PostToolUse` du même `[Theory]` étaient VERTS — et vacueusement.** À ce stade, `PostToolUse`
n'est routé nulle part : il tombe dans le `_ => null` du `switch` et rend `Ignored` **pour une raison qui
n'a rien à voir avec le veto**. Ils seraient restés verts si le veto n'avait jamais été écrit. Une garde
vacueusement verte n'est pas une garde, et c'est pour cela que la mutation a été rejouée.

## La mutation « supprimer le veto », rejouée en FIN DE T2 — **5 cas**

Méthode obligatoire respectée : `git worktree` jetable (`chronos-mutant-veto`), détaché sur le commit de la
tâche 2, mutation compilable (`_ = estSousAgent;` — le veto est retiré, la variable reste lue, donc pas
d'avertissement de compilation qui aurait pu masquer l'échec).

```
Échoué!  - échec : 5, réussite : 8, total : 13
```

| # | Test en échec | Nouveau depuis T1 ? |
|---|---|---|
| 1 | `…_est_ignore(ev: "PostToolUse", marqueur: "\"agent_id\":\"a-1\"")` | **OUI — probant seulement maintenant** |
| 2 | `…_est_ignore(ev: "PostToolUse", marqueur: "\"agent_type\":\"code-reviewer\"")` | **OUI — probant seulement maintenant** |
| 3 | `…_est_ignore(ev: "UserPromptSubmit", marqueur: "\"agent_id\":\"a-1\"")` | non (déjà rouge en T1) |
| 4 | `…_est_ignore(ev: "Stop", marqueur: "\"agent_id\":\"a-1\"")` | non |
| 5 | `Un_SessionEnd_de_sous_agent_ne_supprime_pas_le_fichier_d_etat_du_parent` | non |

**C'est la seule mesure qui prouve que le veto protège les deux battements**, c'est-à-dire très exactement
ce que la phase ajoute. Worktree révoqué : `git worktree list` ne rend que le dépôt principal,
`git status --porcelain` est vide, `grep -rlF "MUTANT" --include=*.cs` rend **0**.

### Mutation supplémentaire (non demandée, mais nécessaire) — le battement de T3

Les quatre tests de la tâche 3 sont **verts d'emblée** : la tâche ne change AUCUNE valeur, elle renomme et
redocumente. Le risque était donc qu'un test soit vert pour une mauvaise raison. Mutation jouée dans
`chronos-mutant-battement` (routage de `PostToolUse` retiré) :
`Un_battement_frais_maintient_en_cours_bien_au_dela_d_une_heure` → **1 échec / 1**. Le test dépend bien du
battement, pas d'une indulgence du seuil. Worktree révoqué, dépôt vérifié propre.

## Le point (f) — décision retenue, et pourquoi

> **RETENUE : issue (i), la reprise sur `IOException`. ÉCARTÉE : issue (ii), `FileShare.ReadWrite`.**

**Pourquoi (ii) est écartée.** Élargir le partage en écriture ferait disparaître le refus, pas le problème :
deux écrivains entrelacés sur le même descripteur produiraient un **fragment** là où il y avait un état
entier. Or la phase 23 a posé — et `Un_fragment_illisible_est_une_ABSENCE_de_signal_jamais_un_vieux_signal`
le tient — qu'un fragment est une **absence de signal**. On troquerait donc un échec **visible et rendu à
l'appelant** contre une **perte silencieuse**. C'est l'inverse de ce que ce milestone construit.

**Pourquoi (i) est la bonne.** Le partage exclusif entre écrivains est la GARANTIE que le fichier reste
entier ; il ne demande qu'une chose en retour : **attendre son tour**. La reprise est exactement cette
attente. Aucune ligne de la doctrine n'est touchée — pas de fichier temporaire déplacé
(`grep -n 'File.Move'` sur les deux fichiers du chemin des hooks rend **aucune ligne**), pas d'élargissement
de partage, et `EcritureEtatSession` reste le seul chemin d'écriture.

### Ce qui a été MESURÉ, et pourquoi la reprise « unique » n'a pas survécu à la mesure

Le plan recommandait une **reprise unique**. Elle a été implémentée telle quelle, puis mesurée par
`Huit_ecrivains_paralleles_sur_la_meme_session_n_echouent_jamais` (8 écrivains × 50 battements = 400
écritures sur la MÊME session) :

| Variante | Refus / 400 | Verdict |
|---|---|---|
| **Aucune parade** (l'état livré sans ce plan) | **288** — `IOException : The process cannot access the file … because it is being used by another process` | 72 % des battements d'un batch échouent → `App.xaml.cs:155` écrit sur stderr → **Claude Code affiche un message à l'utilisateur à chaque batch d'outils** |
| **Reprise UNIQUE** (recommandation du plan) | **209**, puis **180** sur deux exécutions | **INSUFFISANTE** — elle absorbe la moitié du problème et pas davantage |
| **Reprise BORNÉE** (livrée) | **0**, **0**, **0** sur trois exécutions consécutives | conforme |

**La raison de l'échec de la reprise unique est mécanique, pas statistique** : une reprise **immédiate**
retente pendant que le détenteur écrit **toujours**, et elle n'a qu'une seule chance. Il faut donc deux
choses qu'elle n'a pas — **céder la main** entre deux essais, et pouvoir s'y reprendre plus d'une fois.

La forme livrée reste petite et **bornée** (`EssaisMax = 60`, `Thread.Yield()` sur les douze premiers
essais puis `Thread.Sleep(1)`), parce que ce code est sur le chemin critique d'un hook **bloquant** dont le
délai de grâce est de trois secondes : un budget non borné transformerait une contention en gel de l'appel
d'outil, soit exactement ce que le `timeout` court cherche à éviter. Voir la déviation **n° 1** ci-dessous.

## Cadence — les deux mesures

| Test | Motif | Mesuré |
|---|---|---|
| `Cinq_cents_battements_consecutifs_sous_lecteur_concurrent_ne_perdent_rien` | 500 battements **séquentiels** pendant qu'un lecteur tient la cible ouverte en `FileShare.ReadWrite` — le mode EXACT de `SessionMonitor.TryRead` | **0 échec sur 500** ; l'état final est relu par le moniteur réel et vaut `Working`, motif `PostToolUse`, horodatage = celui du 500ᵉ battement |
| `Huit_ecrivains_paralleles_sur_la_meme_session_n_echouent_jamais` | 8 `Parallel.For` × 50 battements sur la **MÊME** session — motif exact d'un batch d'appels d'outil parallèles | **0 `Reussi == false` sur 400** ; le fichier final est **désérialisé** (pas mesuré : sa taille ne dirait rien d'un fragment) et porte `session_id`, `activity == Working`, `reason == PostToolUse` |

Le motif de relecture est **hermétique**, comme exigé (A5) : `grep -c 'new SessionMonitor('` sur
`BattementsCoeurTests.cs` rend **1**, cette unique occurrence injecte les **trois** arguments
(`dossier`, `TranscriptSessionSource(dossierVide)`, `ArchiveStore(…)`) et lit à un instant **FIGÉ** ;
`grep -c 'DateTimeOffset.UtcNow'` sur ce fichier rend **0**.

## Valeurs réelles de chaque `grep` d'acceptation

| Critère | Attendu | **Mesuré** |
|---|---|---|
| `test -f tests/Chronos.Tests/BattementsCoeurTests.cs` | vrai | **vrai** |
| filtre `BattementsCoeurTests` (fin T1) | 0 échec, ≥ 8 tests | **0 échec, 9 tests** |
| filtre `SessionsTests\|EcritureEtatSessionTests` (fin T1) | 0 échec | **0 échec, 108 tests** |
| `grep -c 'agent_id' …/SessionHookProcessor.cs` | ≥ 1 | **2** |
| `grep -c 'agent_type' …/SessionHookProcessor.cs` | ≥ 1 | **2** |
| `grep -c 'Un_SessionEnd_de_sous_agent_ne_supprime_pas_le_fichier_d_etat_du_parent' …/BattementsCoeurTests.cs` | ≥ 1 | **1** (et il asserte les DEUX faits : `Ignore == true` ET `Delete == false`) |
| filtre `BattementsCoeur\|ClaudeSettingsReconciler\|Sessions` (fin T2) | 0 échec | **0 échec, 135 tests** |
| `Le_cablage_est_exactement_celui_que_la_phase_annonce` étendu aux **8** noms dans l'ordre | passe | **passe** — il a bien ÉCHOUÉ à l'ajout des deux battements, exactement son rôle |
| `grep -c 'Les_battements_n_evincent_pas_les_hooks_d_un_autre_outil' …/ClaudeSettingsReconcilerTests.cs` | ≥ 1 | **1** |
| `grep -c 'PostToolUse' …/SessionHookInstaller.cs` | ≥ 1 | **1** |
| `grep -c 'PreToolUse' …/SessionHookInstaller.cs` | ≥ 1 | **3** |
| `grep -n 'File.Move' …/SessionHookProcessor.cs …/EcritureEtatSession.cs` | aucune ligne | **aucune ligne** |
| `grep -c 'new SessionMonitor(' …/BattementsCoeurTests.cs` | hermétique | **1**, trois arguments injectés, instant figé |
| `grep -c 'DateTimeOffset.UtcNow' …/BattementsCoeurTests.cs` | 0 | **0** |
| `git diff --stat $SHA..HEAD -- tests/Chronos.Tests/TestData/` | vide | **vide** — la fixture polluée est intacte |
| filtre `Inspection\|GardesPerimetre\|ArbitrageSessions` (fin T3) | 0 échec | **0 échec, 37 tests** |
| `grep -c 'SilenceDesBattements' …/SessionMonitor.cs` | ≥ 3 | **3** (déclaration, usage dans `TryRead`, référence de la XML-doc de tête) |
| `grep -rn 'StaleWorking' src/` (fichiers `.cs`) | aucune ligne | **aucune ligne** — seuls des binaires de build antérieurs la portent encore, ils sont réécrits au build suivant |
| `grep -c` des 4 tests nommés en T3 (b) | ≥ 4 | **4** |
| `grep -c 'FromMinutes(20)' …/SessionMonitor.cs` | ≥ 1 | **1** — la valeur n'a **pas** bougé |
| `grep -c 'FromHours(8)' …/SessionMonitor.cs` | ≥ 1 | **1** — `DropAfter` intouché |
| `grep -cF 'byId[' …/InspectionSessionsTests.cs` | 0 | **0** |
| `grep -cF 'Dictionary<string, SessionSnapshot>' …/InspectionSessionsTests.cs` | 0 | **0** |
| `git diff --stat $SHA..HEAD -- ArbitrageSessions.cs LectureSessions.cs SessionTreatmentTracker.cs TreatedStore.cs ArchiveStore.cs App.xaml.cs` | vide | **vide** |
| `git diff --stat $SHA..HEAD -- '*.csproj' '*.xaml'` | vide | **vide** |
| `git status --short` après les trois commits | vide | **vide** |

`git diff --stat 71e3032..HEAD` (portée de CE plan) : **exactement les 8 fichiers de `files_modified`**,
571 insertions / 18 suppressions. Aucun fichier hors périmètre touché.

## Le renommage `StaleWorking` → `SilenceDesBattements` — aucun site d'appel orphelin

Le champ est **privé** et n'apparaissait qu'à trois endroits de `SessionMonitor.cs`, comme annoncé :
la déclaration (l. 21), l'usage dans `TryRead`, et une référence `<see cref="…"/>` dans la XML-doc de tête.
Vérification faite malgré tout : `grep -rn 'StaleWorking'` sur `src/` **et** `tests/`, limité aux `.cs`,
rend **zéro ligne**. Un site d'appel orphelin aurait de toute façon cassé la compilation — mais une
référence `<see cref>` restée en arrière, elle, n'aurait rien cassé et serait devenue un mensonge muet ;
c'est précisément celle-là qui a été mise à jour.

**La valeur n'a pas bougé** — `FromMinutes(20)` est toujours là, une fois. Et ce n'est pas affirmé, c'est
**tenu par un test de frontière** : `Dix_neuf_minutes_de_silence_laissent_encore_l_etat_en_cours`. Sans lui,
le renommage serait resté vert même si le seuil était tombé à une minute.

## La preuve que les hooks de l'AUTRE OUTIL survivent aux clés désormais partagées

C'est le point de sécurité le plus sensible de ce plan : `PreToolUse` et `PostToolUse` portent DÉJÀ, sur
cette machine, des groupes GSD — et Chronos vient maintenant écrire sur ces mêmes clés.

`Les_battements_n_evincent_pas_les_hooks_d_un_autre_outil_sur_PreToolUse_et_PostToolUse` réconcilie la
**fixture réelle du 2026-09-09** et vérifie, pour chacune des deux clés :

1. le groupe tiers est **toujours à l'index 0** (assertion explicite : `IsChronosGroup` le rend `false`) ;
2. sa **commande** (`gsd-prompt-guard.js`, `gsd-context-monitor.js`), son **`matcher`** (`Write|Edit`,
   `Bash|Edit|Write|MultiEdit|Agent|Task`) et son **`timeout`** (5, 10) sont intacts ;
3. il y a **exactement un** groupe Chronos, et son index est **strictement supérieur à 0** ;
4. la clé **inconnue** `if` du groupe `PostToolUse` vaut toujours `always`.

Et comme annoncé au SUMMARY 25-01 point 4, **`La_purge_large_epargne_les_hooks_d_un_autre_outil` est resté
vert sans la moindre retouche** alors qu'un groupe Chronos s'ajoute désormais derrière les groupes tiers de
ces deux clés — parce qu'il n'assertait aucune longueur de tableau. L'invariant tient : la purge repère les
groupes Chronos par marqueur d'argument **et** nom de fichier `Chronos*.exe`, **jamais par la clé**.

## Deviations from Plan

### 1. [Rule 1 — le correctif recommandé ne corrigeait pas] Reprise BORNÉE au lieu de reprise UNIQUE

- **Trouvé pendant :** tâche 2, point (f).
- **Problème :** l'issue (i) telle que le plan-checker la formulait — « une **reprise unique** sur
  `IOException` » — a été implémentée à la lettre, puis mesurée : **209 puis 180 refus sur 400**, contre 288
  sans aucune parade. Elle absorbe la moitié du problème et laisse le mode de défaillance décrit en B6
  pleinement actif (Claude Code afficherait encore un message d'erreur à l'utilisateur sur la plupart des
  batchs d'outils). Le critère d'acceptation « **zéro** `Reussi == false` » était donc inatteignable.
- **Cause :** une reprise immédiate retente pendant que le détenteur du fichier écrit toujours, et n'a
  qu'une seule chance. Rien de statistique : c'est structurel.
- **Correctif :** reprise **bornée** (`EssaisMax = 60`) **avec cession de la main** — `Thread.Yield()` sur
  les douze premiers essais, `Thread.Sleep(1)` ensuite. Mesure après correctif : **0 refus sur 400**, trois
  exécutions consécutives.
- **Ce qui n'a PAS changé :** l'issue retenue reste bien **(i)**, pas (ii). Le partage demeure
  `FileShare.Read` (exclusif entre écrivains), aucun fichier temporaire déplacé n'est réintroduit, et
  `EcritureEtatSession` reste le seul chemin d'écriture. Seule la **forme** de la reprise a changé, parce
  que la forme recommandée ne tenait pas la mesure.
- **Fichier :** `src/Chronos/Services/EcritureEtatSession.cs` — déjà réservé dans `files_modified` pour cela.
- **Commit :** `6c09a48`

### 2. [Forme] Le veto lit `agent_id` / `agent_type` dans le bloc de parsing, pas au fil du routage

- Le plan montrait `string agentId = Str(r, "agent_id"), agentType = Str(r, "agent_type");` sur une ligne.
  Les deux champs sont lus **dans le même bloc `try` que `session_id`, `cwd` et `notification_type`**, et
  déclarés avec eux en tête de méthode. La raison est structurelle : `r` (le `JsonElement` racine) n'existe
  que dans ce bloc, et le `using var doc` qui le porte est libéré à sa sortie. Lire les marqueurs ailleurs
  aurait exigé de rouvrir le document. Le comportement livré est **exactement** celui du plan.

### 3. [Forme] Mutation de falsification supplémentaire, non demandée

- Les quatre tests de la tâche 3 étant verts d'emblée (la tâche ne change aucune valeur), une mutation du
  routage de `PostToolUse` a été jouée en worktree jetable pour vérifier que
  `Un_battement_frais_maintient_en_cours_bien_au_dela_d_une_heure` n'est pas vert pour une mauvaise raison.
  Il échoue bien. Coût : une exécution filtrée ; bénéfice : une affirmation mesurée au lieu d'une
  supposition. Worktree révoqué.

Aucune autre déviation. **Aucune règle 4 (architecture) déclenchée**, aucune porte d'authentification
rencontrée, **aucune dépendance NuGet ajoutée**.

## Acquis des phases précédentes — revérifiés

| Acquis | Attendu | Mesuré |
|---|---|---|
| Phase 20 — `?? new ` dans `DiagnosticService.cs` | 2 | **2** |
| Phase 22 — `=> Inspecter(now).Visibles;` dans `SessionMonitor.cs` | 1 | **1** |
| Phase 22 — `new SessionMonitor` dans `DiagnosticService.cs` | 0 | **0** |
| Phase 23 — `File.Move` dans `SessionHookProcessor.cs` / `EcritureEtatSession.cs` | 0 / 0 | **0 / 0** |
| Phase 24 — `byId[` dans `SessionMonitor.cs` | 0 | **0** |
| Phase 24 — `ArbitrageSessions.Trancher(` dans `SessionMonitor.cs` | 1 | **1** |
| Phase 24 — `MotifMasquage` dans `DiagnosticService.cs` | 3 | **3** |
| Phase 24 — diff `ArbitrageSessions.cs` / `LectureSessions.cs` | vide | **vide** |
| Phase 26 — diff `SessionTreatmentTracker.cs` / `TreatedStore.cs` / `ArchiveStore.cs` | vide | **vide** |
| 25-01 — `PermissionRequest` dans `CatalogueEvenementsHooks.cs` | ≥ 1 | **1** |
| 25-01 — `CatalogueEvenementsHooks` dans `SessionHookInstaller.cs` | ≥ 2 | **4** |
| 25-01 — `NotificationsSansEtat` dans `SessionHookProcessor.cs` | ≥ 2 | **2** |
| `GardesPerimetreTests` | 10, 0 échec | **10, 0 échec** |
| `NormalisationUniqueTests` | 3, 0 échec | **3, 0 échec** |
| `ServicesLayerPurityTests` | 2, 0 échec | **2, 0 échec** |
| `CompositionRootTests` | 5, 0 échec | **5, 0 échec** |
| `GardesDoctrineTests` | 8, 0 échec | **8, 0 échec** |
| `SessionStylesBindingTests` | 2, 0 échec | **2, 0 échec** |
| `EcritureEtatSessionTests` (phase 23) | 0 échec | **0 échec, 6 tests** — la parade concurrente ne régresse rien |

## Invariants de sécurité — relevés avant ET après

| Invariant | Attendu | **Avant → Après** |
|---|---|---|
| `~/.claude/settings.json` modifié | **jamais** | **md5 `78eb517cc1a453b2ddce9a39af528900` → `78eb517cc1a453b2ddce9a39af528900`** — identique, non touché |
| Hooks `PreToolUse` / `PostToolUse` de l'autre outil dans le vrai settings | intacts | **intacts** — aucune écriture ; seul l'exe republié y écrira, au prochain lancement, sous le contrôle de l'utilisateur |
| Sonde de capture installée dans `~/.claude/` | 0 | **0** |
| `%APPDATA%\Chronos\sessions` | 66 entrées | **66 → 66** |
| `%APPDATA%\Chronos\archived.json` | 84 octets | **84 → 84** |
| `%APPDATA%\Chronos\oauth.dat` (**taille seule**) | 518 octets | **518 → 518** — mtime **non consulté** |
| Overlay `Chronos-v3.0.2.exe` (pid 119412) | vivant, ni lancé ni tué | **vivant, pid 119412** (`tasklist` seul) |
| Dépendances NuGet ajoutées | 0 | **0** (`git diff -- '*.csproj'` vide) |
| Tests écrivant hors de `Path.GetTempPath()` | 0 | **0** — `Aucun_test_ne_cible_le_vrai_settings_du_profil` vert ; `BattementsCoeurTests.TempDossier()` porte la garde `Assert.StartsWith(Path.GetTempPath(), d)` et supprime en `finally` |
| Requêtes réseau réelles depuis un test | 0 | **0** |
| Worktrees de mutation restants | 0 | **0** — `git worktree list` ne rend que le dépôt principal |

## Known Stubs

Aucun. Les quatre artefacts annoncés par le plan sont pleinement câblés, routés et prouvés.

**Limite ASSUMÉE, non stub — et elle est au cœur d'EVT-03 :** `PreToolUse` / `PostToolUse` sont une preuve
de vie **imparfaite**. Entre le signal d'entrée d'un build de dix minutes et son signal de sortie, rien
n'arrive ; idem pendant une longue réflexion sans appel d'outil. Une session tenue par un **outil unique de
plus de vingt minutes** franchira donc le seuil de silence alors qu'elle travaille encore. Ce n'est ni un
oubli ni une lacune de ce plan : c'est la conséquence directe du fait que **le catalogue ne contient aucun
battement périodique**, et elle doit être écrite au §5 de `docs/hooks-contract.md` (**EVT-05, plan 25-04**).
Le `25-VALIDATION.md` en fait déjà un point de vérification in vivo (n° 2, « cas limite à guetter »), avec
la consigne explicite : si le cas se présente souvent, c'est le choix de l'**événement porteur** qu'il faut
rouvrir, **pas** le seuil.

**Report assumé, non stub (rappel de 25-01) :** les deux battements n'existeront dans la configuration de
l'utilisateur qu'**après republication de l'exe suivie d'une réconciliation au démarrage**, et seules les
sessions Claude Code ouvertes après cette réconciliation seront suivies.

## À transmettre aux plans 25-03 et 25-04

1. **Le SHA d'entrée de phase reste `ce40e99cffe605e2baa93511e2555884aa6acdb8`.**
2. **`SilenceDesBattements` est le nom à utiliser** — `StaleWorking` n'existe plus nulle part dans les
   sources. Le plan 25-03 T2 convertit trois assertions héritées d'`Unknown` vers `WaitingDeduced` ; l'une
   d'elles est `Vingt_et_une_minutes_de_SILENCE_ne_se_disent_plus_en_cours`, **livrée par ce plan** et déjà
   nommée dans la liste des 7 renommages annoncés de la phase.
3. **`EvenementCable` porte désormais un quatrième membre, `Timeout` (défaut 10).** Le §6 de
   `docs/hooks-contract.md` (25-04) doit reporter la raison du 3 s : `PreToolUse` est **bloquant**.
4. **`EcritureEtatSession.Appliquer` sérialise désormais les écrivains concurrents** par reprise bornée.
   Tout plan qui ajouterait un événement à haute fréquence hérite de cette garantie — mais le budget
   (`EssaisMax = 60`) est dimensionné pour un délai de grâce de trois secondes, pas davantage.
5. **`BattementsCoeurTests.cs` est le fichier des battements** : y ajouter les cas d'EVT-04 qui portent sur
   le pipeline `Process` → `Appliquer` → `Read` plutôt que de les disperser.

## Commits

| Tâche | Hash | Message |
|---|---|---|
| 1 | `d1ce997` | `feat(25-02): veto sous-agent - un sous-agent ne parle jamais pour son parent (EVT-03)` |
| 2 | `6c09a48` | `feat(25-02): les deux battements de coeur, cables sans matcher et routes (EVT-03)` |
| 3 | `3f8f1af` | `refactor(25-02): le seuil de vingt minutes devient le silence des battements (EVT-03)` |

## Self-Check: PASSED

Fichier créé — vérifié présent :
- `tests/Chronos.Tests/BattementsCoeurTests.cs` — FOUND
- `.planning/phases/25-le-contrat-d-evenements-refonde/25-02-SUMMARY.md` — FOUND

Commits — vérifiés présents dans `git log` : `d1ce997`, `6c09a48`, `3f8f1af` — FOUND.

Suite complète : **837 tests, 0 échec**, deux exécutions consécutives identiques.
`git status --short` vide, `git worktree list` réduit au dépôt principal, 0 fichier `MUTANT`.
