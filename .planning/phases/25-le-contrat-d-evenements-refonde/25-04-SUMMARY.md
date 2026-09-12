---
phase: 25-le-contrat-d-evenements-refonde
plan: 04
subsystem: sessions
tags: [documentation, contrat-externe, garde-de-non-derive, trous-documentaires, chemin-injecte, xunit]

sha_entree_de_phase: ce40e99cffe605e2baa93511e2555884aa6acdb8
sha_entree_de_plan: 78648dd

requires:
  - phase: 25-le-contrat-d-evenements-refonde
    provides: "SessionHookInstaller.Cablage (8 entrées, plan 25-02) et CatalogueEvenementsHooks.Tous (33 noms, plan 25-01) — les deux tables que le document recopie et que la garde compare"
  - phase: 25-le-contrat-d-evenements-refonde
    provides: "SessionActivity.WaitingDeduced et SilenceDesBattements (plan 25-03) — les états produits décrits au §3"
provides:
  - "docs/hooks-contract.md — le contrat des hooks : ce qui est câblé, ce qui est lu, ce qui est produit, et ce qui n'est PAS garanti"
  - "Les TROIS trous documentaires datés (2026-09-12) et sourcés, dans la section §5 elle-même"
  - "ContratHooksDocumenteTests — la garde de non-dérive : nom, matcher ET rôle comparés au câblage réel"
  - "CheminDocsChronos — le chemin de docs/ injecté par MSBuild, jamais deviné"
affects: [26-ce-que-traite-veut-dire]

tech-stack:
  added: []
  patterns:
    - "Un document de contrat n'est tenu que s'il est COMPARÉ : la garde lit le TEXTE du Markdown et le confronte au câblage"
    - "La colonne qui porte le SENS (le rôle) doit être gardée à part : le nom et le filtre peuvent rester justes pendant que la description ment"
    - "Une date de relevé doit être DANS la section qu'elle date, pas seulement dans le fichier : une date en tête ne date aucun trou"
    - "Une garde qui se désarme quand son chemin manque ne garde rien — l'absence d'injection est un ÉCHEC, jamais un silence"
    - "La barre verticale d'une valeur (un matcher) s'échappe dans une table Markdown : le découpage doit la mettre à l'abri avant de couper, sinon une valeur devient trois fausses colonnes"

key-files:
  created:
    - docs/hooks-contract.md
    - tests/Chronos.Tests/ContratHooksDocumenteTests.cs
  modified:
    - tests/Chronos.Tests/Chronos.Tests.csproj

key-decisions:
  - "La table du §1 porte QUATRE colonnes et non trois : le `timeout` y est ajouté, parce qu'il cesse d'être uniforme depuis EVT-03 et qu'il est une décision de conception, pas un réglage. Le rôle reste en TROISIÈME colonne, exactement où la garde le cherche."
  - "Le document décrit le LIVRÉ, pas le plan : reprise BORNÉE (60 essais) et non reprise unique, prédicat `IsWaiting` et non `EstAttente`, quatre colonnes et non trois."
  - "La divergence SessionTreatmentTracker / WaitingDeduced est ÉCRITE au §3 et NON corrigée : le diff du tracker reste vide sur toute la phase. Entrée de la phase 26."
  - "Deux mutations jouées, pas une : la mutation exigée (retrait d'une ligne) fait tomber TROIS tests, ce qui ne prouve pas que la colonne du rôle soit gardée. Une seconde mutation ne touchant QUE cette colonne le prouve — 1 échec, et lui seul."

metrics:
  duration: "~45 min"
  completed: 2026-09-12
  tasks: 2
  commits: 2
  tests_avant: 855
  tests_apres: 862
---

# Phase 25 Plan 04 : le contrat des hooks est écrit — et il ne peut plus mentir en silence

Le contrat externe des hooks est désormais un document du dépôt (`docs/hooks-contract.md`, 344 lignes,
neuf sections), daté et sourcé, dans lequel **ce qui n'est pas garanti occupe autant de place que ce qui
l'est** — et une garde compare le nom, le `matcher` **et le rôle** de chaque ligne au câblage réellement
installé, pour que le jour où le document cesse de dire vrai, un test le dise avant un humain.

## SHA d'entrée de phase — et le SHA d'entrée de PLAN

`ce40e99cffe605e2baa93511e2555884aa6acdb8` — réutilisé **à l'identique** depuis les SUMMARY 25-01, 25-02
et 25-03.

**Second repère, nécessaire à ce plan :** `78648dd`, dernier commit de 25-03, donc entrée de 25-04. Il sert
à mesurer ce que **CE plan** a touché, là où le SHA de phase mesure ce que **la phase** a touché. Voir la
déviation n° 1 : le plan demandait un diff vide sur `src/` depuis le SHA de PHASE, ce qui est littéralement
impossible puisque 25-01 à 25-03 ont modifié neuf fichiers de production.

## Comptes de tests

| Mesure | Valeur |
|---|---|
| Entrée (après 25-03), **remesurée avant la première ligne écrite** | **855 / 0 échec / 6 s** |
| Tâche 1 (le document) | **855** — aucun test ajouté : la tâche n'écrit que du Markdown |
| Après tâche 2 (la garde de non-dérive) — **total final** | **862** (+7), 0 échec, 6 s |
| Deuxième exécution consécutive | **862 / 0 échec / 6 s** — identique |

**862 = 855 + 7.** Aucun test supprimé, aucun test désactivé, aucun test mis en `Skip`, aucun renommage.

### Écart à l'attendu indicatif (854) — justifié nominativement, en deux parts

L'écart est de **+8**, et il se décompose exactement :

| Part | Montant | D'où elle vient |
|---|---|---|
| Décalage de BASE, **hérité et déjà justifié** | **+7** | La carte partait de **848** pour l'entrée du 25-04 ; la mesure réelle après 25-03 était **855**. Cet écart de +7 était **déjà** justifié nominativement au SUMMARY 25-03 (+4 hérités du 25-02, +3 tests ajoutés par la revue en 25-03 T2). Il se propage tel quel, **il n'est pas neuf**. |
| Test SURNUMÉRAIRE en T2 | **+1** | La carte estimait ≈ +6 pour la tâche 2 ; **7** tests y ont été livrés. Le surnuméraire est nommable : `Chaque_ligne_documentee_porte_le_role_reellement_cable`, ajouté par la revue du plan (point A9) postérieurement à la carte, et nommé explicitement dans le critère d'acceptation « les six annoncés + `Chaque_ligne_documentee_porte_le_role_reellement_cable` ». |

Détail des 7 cas ajoutés — sept `[Fact]`, aucune `[Theory]` :

| Cas | Ce qu'il tient |
|---|---|
| `Le_chemin_du_document_est_injecte_et_le_fichier_existe` | l'attribut est posé, le dossier existe, le fichier existe |
| `La_table_documentee_liste_EXACTEMENT_les_evenements_cables` | égalité d'ENSEMBLES (ni manquant ni surnuméraire) **et** égalité de cardinal (pas de doublon) |
| `Chaque_ligne_documentee_porte_le_matcher_reellement_installe` | 2ᵉ colonne = matcher, ou le littéral `(aucun)` |
| `Chaque_ligne_documentee_porte_le_role_reellement_cable` | 3ᵉ colonne = `c.Role`, comparaison **ordinale**, cellules rognées |
| `Le_document_porte_les_trente_trois_noms_du_catalogue` | les 33 noms de `CatalogueEvenementsHooks.Tous` sont dans le texte |
| `Le_document_porte_les_trois_trous_documentaires_avec_leur_date` | les trois marqueurs **et** la date, cherchés **dans le §5 découpé**, pas dans le fichier entier |
| `Le_document_lu_n_est_ni_tronque_ni_vide` | ≥ 90 lignes et table non vide — sinon tout le reste serait vert faute de matière |

## Étape ROUGE mesurée — 7 échecs / 7, et il faut dire de quoi elle témoigne

La classe de test a été écrite **avant** l'attribut `CheminDocsChronos`. Sans injection, `CheminDocs()` rend
la chaîne vide et **aucun** test ne peut lire quoi que ce soit :

```
Échoué!  - échec : 7, réussite : 0, total : 7
```

Les sept noms, tels que la sortie xUnit les rend : `Le_document_lu_n_est_ni_tronque_ni_vide`,
`La_table_documentee_liste_EXACTEMENT_les_evenements_cables`,
`Le_document_porte_les_trois_trous_documentaires_avec_leur_date`,
`Chaque_ligne_documentee_porte_le_matcher_reellement_installe`,
`Le_chemin_du_document_est_injecte_et_le_fichier_existe`,
`Chaque_ligne_documentee_porte_le_role_reellement_cable`,
`Le_document_porte_les_trente_trois_noms_du_catalogue`.

**Ce que cette étape rouge prouve, et ce qu'elle ne prouve pas.** Elle prouve la **non-mise-en-sourdine** :
une garde qui perdrait son chemin ÉCHOUE au lieu de se taire — c'est le premier point du bloc `<behavior>`,
et il est le plus facile à rater dans une garde de ce genre. Elle **ne prouve pas** que les comparaisons
soient justes : le document existait déjà, donc une fois le chemin injecté, tout passait au vert. Il serait
malhonnête de s'en contenter. C'est à cela que servent les deux mutations ci-dessous.

## LES MUTATIONS DE FALSIFICATION — deux, mesurées, et révoquées

Méthode obligatoire respectée : `git worktree` **jetable** détaché sur le commit de la tâche 1
(`6386107`), dans lequel le fichier de test et le `.csproj` non encore commités ont été **copiés**. Les
mutations ne portent donc que sur le document, dans une copie de travail isolée — **le dépôt principal n'a
jamais porté de mutation**.

Chemin du worktree : `…/DEV/chronos-mutant-contrat`, détaché sur `6386107`.

### Mutation n° 1 — celle que le plan exige : retirer une ligne de la table du §1

La ligne `PostToolUse` retirée de la table encadrée par les deux marqueurs.

```
Échoué!  - échec : 3, réussite : 4, total : 7
```

| # | Test en échec | Attendu par le plan ? |
|---|---|---|
| 1 | `La_table_documentee_liste_EXACTEMENT_les_evenements_cables` | **OUI — c'est le test nommé au critère d'acceptation** |
| 2 | `Chaque_ligne_documentee_porte_le_matcher_reellement_installe` | non (dommage collatéral : il ne trouve plus la ligne) |
| 3 | `Chaque_ligne_documentee_porte_le_role_reellement_cable` | non (idem) |

Et le message du test attendu **nomme** l'entrée disparue, ce qui est la moitié utile d'une garde :

```
Le §1 de docs/hooks-contract.md ne décrit plus le câblage réel.
  CÂBLÉS mais NON DOCUMENTÉS : PostToolUse
  DOCUMENTÉS mais NON CÂBLÉS : (aucun)
```

### Mutation n° 2 — **non demandée, et pourtant la seule probante pour le §A9**

La mutation n° 1 fait tomber **trois** tests à la fois : elle ne dit donc **rien** de la colonne du rôle
prise isolément. Or c'est précisément la colonne que la revue a demandé de garder, parce que c'est **la
seule qui puisse mentir sans qu'un nom ni un filtre ne bouge** — et que les tests existants, eux, ne
comparent que le nom et le matcher.

Document rétabli, puis **seule** la troisième cellule de la ligne `Stop` altérée (le nom et le `(aucun)`
laissés intacts) :

```
| `Stop` | (aucun) | le tour se termine, on ne sait plus rien MUTANT-2 | 10 |
```

```
Échoué!  - échec : 1, réussite : 6, total : 7
```

**Un seul test, et c'est le bon :**

```
Chaque_ligne_documentee_porte_le_role_reellement_cable
  « Stop » : le RÔLE documenté ne correspond plus au câblage. C'est la colonne que lit un humain,
  et la seule qui puisse mentir sans qu'un nom ni un filtre bouge.
  câblé      : le tour se termine → tour fini
  documenté  : le tour se termine, on ne sait plus rien MUTANT-2
```

Les six autres restent **verts** — y compris `La_table_documentee_liste_EXACTEMENT_les_evenements_cables`.
C'est la mesure : **sans cette garde, une description fausse passerait entièrement inaperçue.**

*Contrôle involontaire, et il vaut d'être noté :* la première tentative de mutation n° 2 (un `sed` dont le
motif ne rencontrait pas la flèche `→`) n'a **rien** modifié, et la suite est restée à **7 verts**. La
garde n'est donc pas rouge par construction : elle distingue un document intact d'un document altéré.

### Révocation — prouvée

| Preuve | Attendu | **Mesuré** |
|---|---|---|
| `git worktree remove --force` + `git worktree prune` | worktree disparu | **disparu** |
| `git worktree list` | une seule ligne (le dépôt principal) | **une seule ligne** |
| Le dossier `…/DEV/chronos-mutant-contrat` | absent | **absent** (`ls …/DEV` ne le liste plus) |
| `git status --porcelain` après commit | vide | **vide** |
| `grep -rlF "MUTANT" --include=*.cs .` | 0 | **0** |
| `grep -rlF "MUTANT" --include=*.md docs/` | 0 | **0** |

## Le document — ses neuf sections

| § | Titre | Ce qu'il porte |
|---|---|---|
| (en-tête) | — | date du relevé **2026-09-12**, les trois URL de la source, et l'avertissement de **troncature** : aucun nom de champ spécifique à un événement n'est confirmé. Plus le rappel de terrain : configuration lue au DÉMARRAGE d'une session, réconciliation au lancement de l'overlay **en mode overlay uniquement**. |
| 1 | Les événements câblés | la table encadrée par les deux marqueurs — **8 lignes**, dans l'ordre du `Cablage`, avec nom, `matcher`, rôle **et `timeout`** ; plus la sûreté du matcher de `Notification` et le fait que la purge est large mais repère les groupes Chronos par marqueur + nom d'exe, jamais par la clé |
| 2 | Les champs lus sur `stdin`, et leur degré de confiance | **confirmés communs** / **lu mais non confirmé** (`notification_type`, réduit à un VETO, avec le pourquoi) / **jamais lus délibérément** |
| 3 | Les états produits | les **cinq** `activity`, leur libellé, ce que chacun AFFIRME, et la colonne « observé ou déduit ». Plus la **divergence `SessionTreatmentTracker`**, écrite et **non corrigée** |
| 4 | Le piège des sous-agents | même `session_id` que le parent, 94 % des transcripts, la règle livrée, et le **placement** du veto |
| 5 | **CE QUI N'EST PAS GARANTI** | les **trois trous** (5.1 Échap, 5.2 `SessionEnd` sur crash, 5.3 nom inconnu), chacun **daté 2026-09-12 et sourcé**, avec sa conséquence LIVRÉE ; plus **5.4 la limite assumée d'EVT-03** et 5.5 ce que le relevé n'autorise pas |
| 6 | Ce que nous avons délibérément ÉCARTÉ | `MessageDisplay`, `permission_prompt` du bus, `idle_prompt`, le `timeout` uniforme, les ~25 autres événements, la sonde ; puis **pourquoi deux `timeout`** (`PreToolUse` est BLOQUANT) et la **question OUVERTE** sur la sonde de capture |
| 7 | Règles de `matcher` | omis/`"*"`/`""`, liste exacte, **regex non ancrée**, `Edit.*` attrape `NotebookEdit`, les **dix** événements sans support nommés, les **deux pièges de version** (virgule 2.1.191+, tiret 2.1.195+) |
| 8 | Le catalogue complet | les **33** noms, groupés comme dans le relevé ; plus `PermissionDenied` limité au mode `auto` et l'absence de `PermissionGranted` |
| 9 | Comment détecter une dérive | la procédure en trois lignes ; ce qu'une machine garantit ; et **ce qu'aucune machine ne peut garantir** — une dérive de la source EXTERNE, d'où la date en tête |

**344 lignes, 3 231 mots.** Neuf titres de niveau deux, quatorze de niveau trois.

## Le document décrit le LIVRÉ, pas le plan — trois écarts relevés et tranchés en faveur du code

| Point | Ce que le plan disait | Ce que le code livre | Écrit dans le document |
|---|---|---|---|
| Parade aux écrivains concurrents | « reprise unique » | **reprise BORNÉE**, soixante essais, avec cession de la main (déviation n° 1 du 25-02, mesurée : 288 → 209/180 → 0 refus sur 400) | **le livré** : « une reprise BORNÉE avec cession de la main (soixante essais au plus) », §6 |
| Le prédicat du tracker | `SessionTreatmentTracker.cs:39` (`EstAttente`) | `SessionTreatmentTracker.cs`, prédicat **`IsWaiting`**, ligne **38** | **le livré** : nom et ligne réels, §3 |
| La table du §1 | trois colonnes | le câblage porte aussi un `Timeout` non uniforme | **quatre colonnes**, le rôle restant en troisième — voir déviation n° 2 |

## Valeurs réelles de chaque `grep` et de chaque `diff`

### Tâche 1 — le document

| Critère | Attendu | **Mesuré** |
|---|---|---|
| `test -f docs/hooks-contract.md` | vrai | **vrai** |
| `wc -l < docs/hooks-contract.md` | ≥ 90 | **344** |
| `grep -c '2026-09-12'` | ≥ 1 | **5** |
| `grep -c 'EVENEMENTS-CABLES:debut'` | == 1 | **1** |
| `grep -c 'EVENEMENTS-CABLES:fin'` | == 1 | **1** |
| `grep -c '^## '` | ≥ 9 | **9** |
| `grep -ci 'Échap\|interruption'` | ≥ 1 | **4** |
| `grep -c 'prompt_input_exit'` | ≥ 1 | **1** |
| `grep -ci 'nom d.événement inconnu\|liste blanche'` | ≥ 1 | **4** |
| `grep -c 'notification_type'` | ≥ 1 | **2** |
| `grep -ci 'sonde'` | ≥ 1 | **2** |
| `grep -c 'SessionTreatmentTracker'` | ≥ 1 | **2** |
| la date `2026-09-12` **découpée dans le §5** (`awk` entre `## 5.` et le `## ` suivant) | ≥ 1 | **4** |
| `git diff --stat -- src/ tests/` à la fin de la tâche 1 | **vide** | **vide** |

### Tâche 2 — la garde

| Critère | Attendu | **Mesuré** |
|---|---|---|
| filtre `ContratHooksDocumenteTests` | 0 échec, ≥ 7 tests | **0 échec, 7 tests** |
| suite complète | 0 échec, 855 + 7 | **862 / 0 échec**, deux exécutions identiques |
| `grep -c 'CheminDocsChronos' …/Chronos.Tests.csproj` | == 1 | **1** |
| `grep -c 'SessionHookInstaller.Cablage' …/ContratHooksDocumenteTests.cs` | ≥ 1 | **4** |
| `grep -c 'CatalogueEvenementsHooks.Tous' …/ContratHooksDocumenteTests.cs` | ≥ 1 | **1** |
| `GardesPerimetreTests` | 10, 0 échec | **10, 0 échec** |
| `NormalisationUniqueTests` | 3, 0 échec | **3, 0 échec** |
| `ServicesLayerPurityTests` | 2, 0 échec | **2, 0 échec** |
| `CompositionRootTests` | 5, 0 échec | **5, 0 échec** |
| `GardesDoctrineTests` | 8, 0 échec | **8, 0 échec** |
| `SessionStylesBindingTests` | 2, 0 échec | **2, 0 échec** |
| `git diff --stat 78648dd..HEAD -- src/` (**portée de CE plan**) | vide | **VIDE** |
| `git diff --stat $SHA..HEAD -- SessionTreatmentTracker.cs TreatedStore.cs ArchiveStore.cs ArbitrageSessions.cs LectureSessions.cs` | **VIDE** | **VIDE** |
| `git diff --stat $SHA..HEAD -- '*.xaml'` | `SessionStyles.xaml` seul | **`SessionStyles.xaml` seul**, `--numstat` = **1 / 1** |
| `git diff --stat $SHA..HEAD -- '*.csproj'` | `tests/Chronos.Tests/Chronos.Tests.csproj` seul (autorisé par la carte) | **lui seul**, 10 insertions / 0 suppression |
| `git status --porcelain` après les deux commits | vide | **vide** |

### Les acquis, revérifiés

| Acquis | Attendu | **Mesuré** |
|---|---|---|
| 25-01 — `PermissionRequest` dans `CatalogueEvenementsHooks.cs` | ≥ 1 | **1** |
| 25-01 — `CatalogueEvenementsHooks` dans `SessionHookInstaller.cs` | ≥ 2 | **4** |
| 25-01 — `NotificationsSansEtat` dans `SessionHookProcessor.cs` | ≥ 2 | **2** |
| 25-02 — `agent_id` / `agent_type` dans `SessionHookProcessor.cs` | ≥ 1 / ≥ 1 | **2 / 2** |
| 25-02 — `StaleWorking` dans `src/` (`.cs`) | 0 ligne | **0 ligne** |
| 25-02 — `SilenceDesBattements` dans `SessionMonitor.cs` | ≥ 3 | **4** |
| 25-02 — `FromMinutes(20)` / `FromHours(8)` | ≥ 1 / ≥ 1 | **1 / 1** |
| 25-03 — `WaitingDeduced` dans `SessionSnapshot.cs` / `AffichageSessions.cs` / `SessionMonitor.cs` | ≥ 1 / ≥ 2 / ≥ 1 | **1 / 2 / 2** |
| 25-03 — `WaitingDeduced` dans `SessionStylesBindingTests.cs` | ≥ 1 | **1** |
| Phase 20 — `?? new ` dans `DiagnosticService.cs` | 2 | **2** |
| Phase 22 — `=> Inspecter(now).Visibles;` dans `SessionMonitor.cs` | 1 | **1** |
| Phase 22 — `new SessionMonitor` dans `DiagnosticService.cs` | 0 | **0** |
| Phase 23 — `File.Move` dans `SessionHookProcessor.cs` / `EcritureEtatSession.cs` | 0 / 0 | **0 / 0** |
| Phase 24 — `byId[` dans `SessionMonitor.cs` | 0 | **0** |
| Phase 24 — `ArbitrageSessions.Trancher(` dans `SessionMonitor.cs` | 1 | **1** |
| Phase 24 — `MotifMasquage` dans `DiagnosticService.cs` | 3 | **3** |

## Deviations from Plan

### 1. [Rule 3 — critère littéralement inatteignable] Le diff `src/` se mesure depuis l'entrée du PLAN, pas de la PHASE

- **Trouvé pendant :** tâche 2, à la vérification des critères d'acceptation.
- **Problème :** le critère écrit « `git diff --stat <SHA d'entrée de phase>..HEAD -- src/` est **vide**
  pour cette tâche : elle ne touche aucun code de production ». Depuis le SHA d'**entrée de phase**, ce
  diff rend **neuf fichiers, 395 insertions / 49 suppressions** — le travail légitime de 25-01, 25-02 et
  25-03. Le critère est donc inatteignable **par construction**, et le satisfaire exigerait de défaire la
  phase.
- **Ce qu'il voulait dire, et comment il a été mesuré :** l'intention est « ce plan-ci ne touche aucun code
  de production ». Le repère juste est le dernier commit de 25-03, `78648dd`. Mesuré :
  `git diff --stat 78648dd..HEAD -- src/` → **VIDE**. Les deux commits de ce plan ne portent que
  `docs/hooks-contract.md`, `tests/Chronos.Tests/ContratHooksDocumenteTests.cs` et
  `tests/Chronos.Tests/Chronos.Tests.csproj`.
- **Les diffs qui, eux, DOIVENT être vides depuis le SHA de PHASE** — tracker, magasins, arbitrage — ont
  été mesurés depuis ce SHA-là, et sont **vides** (tableau ci-dessus).
- **Aucun fichier modifié** par cette déviation : elle ne change qu'un point de mesure.

### 2. [Forme, assumée] La table du §1 porte QUATRE colonnes

- Le plan montrait la table à trois colonnes (`Événement` / `matcher` / rôle). Elle en porte **quatre** :
  une colonne `timeout` s'ajoute en **dernière** position.
- **Pourquoi :** `EvenementCable` a gagné un membre `Timeout` en 25-02, et ce n'est pas un réglage
  cosmétique — c'est une décision de conception (`PreToolUse` est BLOQUANT) que le SUMMARY 25-02 demande
  explicitement de reporter au document. La porter dans la table, à côté de l'entrée qu'elle qualifie, la
  rend lisible d'un coup d'œil au lieu de la reléguer en prose.
- **Ce qui n'a PAS changé :** le rôle reste en **troisième** colonne, très exactement là où
  `Chaque_ligne_documentee_porte_le_role_reellement_cable` le cherche, et la garde exige `Length >= 3`
  sans jamais exiger `== 3` — une colonne de plus ne la casse pas, une colonne de moins la fait échouer.

### 3. [Forme] Le découpage de la table met la barre verticale des VALEURS à l'abri avant de couper

- Le plan disait « prendre les deux premières cellules en retirant les accents graves et les espaces ». Le
  `matcher` de `Notification` contient **deux barres verticales**, qui sont aussi le séparateur de cellules
  d'une table Markdown : il s'écrit donc **échappé** (`\|`) dans le document, et un découpage naïf sur `|`
  l'aurait brisé en **trois fausses colonnes** — la ligne `Notification` aurait alors porté six cellules,
  le rôle se serait retrouvé en cinquième position, et la garde aurait échoué sur un document pourtant
  juste.
- **Correctif :** avant de découper, `\|` est remplacé par une sentinelle, et chaque cellule la reçoit en
  retour après découpage. Le comportement livré est exactement celui du plan ; seule la mécanique du
  découpage est plus prudente que la phrase ne le laissait croire.

### 4. [Forme] Une seconde mutation, non demandée

- Voir « Les mutations de falsification » : la mutation exigée fait tomber trois tests et ne dit donc rien
  de la garde du rôle prise isolément. Une seconde mutation, ne touchant **que** la troisième cellule,
  rend **1 échec** et lui seul. Coût : une exécution filtrée. Bénéfice : le point A9 devient une mesure au
  lieu d'une affirmation.

Aucune autre déviation. **Aucune règle 4 (architecture) déclenchée**, aucune porte d'authentification
rencontrée, **aucune dépendance NuGet ajoutée**, aucun test supprimé, aucun renommage.

## Invariants de sécurité — relevés AVANT et APRÈS

| Invariant | Attendu | **Avant → Après** |
|---|---|---|
| `~/.claude/settings.json` modifié | **jamais** | **md5 `78eb517cc1a453b2ddce9a39af528900` → `78eb517cc1a453b2ddce9a39af528900`** — identique, non touché |
| Sonde de capture installée dans `~/.claude/` | 0 | **0** — et la question est **posée par écrit** au §6 du document, pas tranchée d'office |
| `%APPDATA%\Chronos\archived.json` | 84 octets | **84 → 84** |
| `%APPDATA%\Chronos\oauth.dat` (**taille seule**) | 518 octets | **518 → 518** — mtime **JAMAIS consulté** |
| `%APPDATA%\Chronos\sessions` | 66 entrées | **66 → 65** — **voir l'encadré ci-dessous** |
| Overlay `Chronos-v3.0.2.exe` (pid 119412) | vivant, ni lancé ni tué | **vivant, pid 119412** (`tasklist` seul) |
| Dépendances NuGet ajoutées | 0 | **0** |
| Tests écrivant hors de `Path.GetTempPath()` | 0 | **0** — `ContratHooksDocumenteTests` **ne fait aucune écriture** : il ne lit que `docs/hooks-contract.md` |
| Requêtes réseau réelles depuis un test | 0 | **0** |
| Worktrees de mutation restants | 0 | **0** |

### ⚠ Le magasin de sessions est passé de 66 à 65 — ce n'est pas ce plan, et il faut le dire

**Composition mesurée après :** **53** fichiers `.json` + **12** reliquats `.tmp-*` = **65**. La
composition relevée le 2026-09-12 au plan 25-03 était **54 `.json` + 12 `.tmp-*` = 66**. **Un fichier
`.json` a disparu**, aucun n'a été créé de toutes pièces et **aucun reliquat `.tmp-*` n'est apparu** (12 →
12, donc aucune régression vers un schéma « fichier temporaire déplacé »).

**Cause, et pourquoi elle est hors de portée de ce plan :** les hooks Chronos sont **déjà installés et
vivants** dans le `settings.json` de l'utilisateur (les cinq de l'ancienne génération). Toute session Claude
Code ouverte sur cette machine — **y compris celle qui exécute ce plan** — fait donc invoquer
`Chronos.exe --hook …` par Claude Code, qui écrit ou supprime un fichier d'état. La disparition constatée
est celle d'un `SessionEnd` réel. Preuve directe : deux fichiers portent des horodatages **postérieurs au
relevé d'entrée** (`3498ae3d-….json` à 22:15:47 et `74ac9f48-….json` à 21:21:27, ce dernier étant
l'identifiant de la session courante).

**Aucune commande de ce plan n'écrit ni ne supprime dans ce dossier.** Les deux tâches n'ont touché que
`docs/` et `tests/`, la nouvelle classe de test ne fait **aucune** écriture, et l'overlay n'a été ni lancé
ni tué. Le magasin est un système **VIVANT** : figer son compte comme un invariant de sécurité était une
attente trop forte de la carte de validation. L'invariant qui compte — « ce plan n'y touche pas » — est
tenu.

## Known Stubs

Aucun. Le document est écrit en entier, ses neuf sections portent ce que le plan et l'exécution exigeaient,
et la garde qui l'empêche de mentir a été **vue rouge deux fois** avant d'être admise verte.

**Report assumé, non stub (rappel des trois plans précédents) :** les trois nouveaux événements
(`PermissionRequest`, `PreToolUse`, `PostToolUse`) et le `matcher` de `Notification` n'existeront dans la
configuration de l'utilisateur qu'**après republication de l'exe suivie d'une réconciliation au démarrage**,
et seules les sessions Claude Code ouvertes après cette réconciliation seront suivies. Le document le dit
en tête, dans le « rappel de terrain ».

**Limite ASSUMÉE, non stub, et désormais ÉCRITE :** un outil unique de plus de vingt minutes fera franchir
le seuil de silence à une session qui travaille encore (§5.4). C'est la conséquence directe de l'absence de
battement périodique dans le catalogue. Si le cas se présente souvent, c'est le choix de l'**événement
porteur** qu'il faudra rouvrir — **pas** le seuil.

## Entrées transmises à la phase 26

1. **La divergence `SessionTreatmentTracker` / `WaitingDeduced`**, écrite au **§3** de
   `docs/hooks-contract.md` : le prédicat `IsWaiting` (l. 38) exclut `WaitingDeduced`, alors que
   `SessionsViewModel.WaitingCount` la compte. Une session déduite **n'ouvre pas d'épisode d'attente** pour
   le suivi de « traité ». `git diff --stat ce40e99..HEAD -- src/Chronos/Services/SessionTreatmentTracker.cs`
   → **VIDE** sur toute la phase 25. **À trancher en phase 26**, qui redéfinit ce que « traité » veut dire.
2. **Le cas d'égalité à la milliseconde** dans `ArbitrageSessions.Departager` (rang source avant rang
   urgence), figé par un test en 25-03 et **non corrigé** — déjà consigné au `25-VALIDATION.md`.
3. **La question ouverte de la sonde de capture**, écrite au §6 du document : tant que l'utilisateur n'a pas
   tranché, aucun nom de champ spécifique à un événement n'est confirmable, et le code ne doit pas s'y
   appuyer.

## Commits

| Tâche | Hash | Message |
|---|---|---|
| 1 | `6386107` | `docs(25-04): le contrat des hooks, y compris ce qui n'est pas garanti (EVT-05)` |
| 2 | `db327d2` | `test(25-04): la garde de non-derive - le document et le cablage ne peuvent plus mentir (EVT-05)` |

## Self-Check: PASSED

Fichiers créés — vérifiés présents :
- `docs/hooks-contract.md` — FOUND (344 lignes)
- `tests/Chronos.Tests/ContratHooksDocumenteTests.cs` — FOUND (267 lignes)
- `.planning/phases/25-le-contrat-d-evenements-refonde/25-04-SUMMARY.md` — FOUND

Commits — vérifiés présents dans `git log` : `6386107`, `db327d2` — FOUND.

Suite complète : **862 tests, 0 échec**, deux exécutions consécutives identiques.
`git status --porcelain` vide, `git worktree list` réduit au dépôt principal, 0 fichier `MUTANT`.
