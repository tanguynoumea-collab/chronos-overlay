---
phase: 25-le-contrat-d-evenements-refonde
plan: 03
subsystem: sessions
tags: [interruption, deduction, honnetete-des-affirmations, seuil-de-silence, garde-de-masse, xunit]

sha_entree_de_phase: ce40e99cffe605e2baa93511e2555884aa6acdb8

requires:
  - phase: 23-l-ecriture-qui-ne-se-perd-plus
    provides: "DropAfter à huit heures — un état trop vieux n'est plus lu du tout"
  - phase: 24-l-arbitrage-par-fraicheur
    provides: "ArbitrageSessions / LectureSessions — acquis, diff VIDE dans ce plan"
  - phase: 25-le-contrat-d-evenements-refonde
    provides: "SilenceDesBattements (plan 25-02) — le seuil qui mesure le silence, et non une durée de travail supposée"
provides:
  - "SessionActivity.WaitingDeduced — l'attente DÉDUITE, cinquième état, ajouté EN FIN d'énumération"
  - "AffichageSessions.Etat → « à toi ? déduit » et Urgence → 3 : une déduction ne devance jamais une observation"
  - "SessionMonitor.TryRead : Working + silence des battements → WaitingDeduced, dérivé à la LECTURE et jamais écrit"
  - "SessionsViewModel : la déduction est ambre, lisible, non estompée, et comptée par WaitingCount"
  - "La garde de masse CHIFFRÉE : 20 sessions sur 54 visibles, sur un corpus déterministe de 66 états"
affects: [25-04-contrat-documente, 26-ce-que-traite-veut-dire]

tech-stack:
  added: []
  patterns:
    - "Un état qui n'a été que DÉDUIT doit le dire dans son libellé — pas dans un commentaire, pas dans une doc : dans les mots que l'utilisateur lit"
    - "Un membre d'énumération s'ajoute EN FIN : l'insérer au milieu change la valeur entière des suivants, et rien ne garantit qu'aucun consommateur ne s'y adosse"
    - "Un cas de switch volontairement REDONDANT avec son défaut n'est pas du bruit : il ÉCRIT l'intention là où le défaut ne fait que l'absorber"
    - "Un drapeau de gabarit décrit une FORME et n'affirme rien ; l'affirmation appartient au libellé, et à lui seul"
    - "Une décision de produit qui élargit un état doit être bornée par un NOMBRE mesuré avant livraison, pas par une intention"
    - "Un test qui FIGE un comportement hérité sans le corriger est un livrable à part entière : le diff du fichier concerné doit rester VIDE"

key-files:
  created: []
  modified:
    - src/Chronos/Services/SessionSnapshot.cs
    - src/Chronos/Services/AffichageSessions.cs
    - src/Chronos/Services/SessionMonitor.cs
    - src/Chronos/ViewModels/SessionsViewModel.cs
    - src/Chronos/Resources/SessionStyles.xaml
    - tests/Chronos.Tests/AffichageSessionsTests.cs
    - tests/Chronos.Tests/InspectionSessionsTests.cs
    - tests/Chronos.Tests/SessionsTests.cs
    - tests/Chronos.Tests/SessionStylesBindingTests.cs

key-decisions:
  - "EVT-04 est livré comme une DÉDUCTION qui se dit déduction : aucun des 33 événements du catalogue ne couvre l'interruption au clavier, donc aucun câblage n'était possible — seul le silence des battements est observable, et il a trois causes indiscernables."
  - "Rang d'urgence 3, derrière Working (2) : une déduction ne devance jamais une observation. Le cas est écrit en toutes lettres bien qu'il soit redondant avec le défaut."
  - "IsTurn = true et IsGhost = false pour la déduction : c'est un choix de FORME (la famille visuelle des attentes), l'affirmation restant dans le seul libellé. La ranger parmi les fantômes l'aurait estompée à 0,22 d'opacité — l'inverse du critère n°3."
  - "Le corpus de la garde de masse ne copie PAS les âges du magasin réel : ses 54 états sont tous au-delà de huit heures (mesuré), donc in vivo le nombre serait zéro aujourd'hui. Le corpus mesure le pire cas d'un magasin VIVANT de cette forme — la mise en garde est écrite au point A11."
  - "Le cas d'égalité à la milliseconde est FIGÉ par un test et NON corrigé : le diff d'ArbitrageSessions.cs reste vide, comme le 25-CONTEXT.md l'exigeait."

metrics:
  duration: "~50 min"
  completed: 2026-09-12
  tasks: 3
  commits: 3
  tests_avant: 837
  tests_apres: 855
---

# Phase 25 Plan 03 : l'interruption au clavier cesse d'être un angle mort — sans jamais être présentée comme observée

Une session interrompue ne se fige plus sur « en cours » et ne disparaît plus : elle porte un cinquième
état, **`WaitingDeduced`**, libellé **« à toi ? déduit »**, ambre et lisible, compté par le compteur
d'attente — et rien, dans le code, les libellés ou les commentaires, ne le présente comme une observation.

## SHA d'entrée de phase

`ce40e99cffe605e2baa93511e2555884aa6acdb8` — réutilisé **à l'identique** depuis les SUMMARY 25-01 et 25-02.
Tous les `git diff --stat` de ce document le prennent pour base : un `git diff --stat` nu ne compare que
l'arbre de travail et reste **muet** après un commit.

## Le point doctrinal, tenu

**Aucun des trente-trois événements du catalogue ne couvre l'interruption utilisateur.** `StopFailure` ne
concerne que les erreurs d'API. Que `Stop` soit muet sur Échap reste **plausible et non confirmable** —
c'est un trou documentaire, pas une réponse. Il n'existait donc rien à câbler, et EVT-04 ne pouvait être
qu'une **déduction**.

Ce qui est réellement observable : la session travaillait, puis **plus aucun battement n'arrive**. La même
signature vaut pour un terminal tué, une mise en veille, ou un outil anormalement long. La cause n'est donc
jamais nommée — c'est l'interrogation du libellé qui porte cette ignorance, et le mot « déduit » qui
l'assume.

| Vérification | Résultat |
|---|---|
| Le mot « déduit » et l'interrogation sont dans le libellé, pas dans un commentaire | `AffichageSessions.Etat(WaitingDeduced) == "à toi ? déduit"`, figé par la `[Theory]` |
| Rang d'urgence **3** — derrière `Working` (2) | figé par `Chaque_etat_a_son_rang_d_urgence` (5 cas) et par `Une_deduction_ne_passe_jamais_devant_une_observation` |
| « tour fini » reste réservé à l'observé | `Assert.NotEqual(SessionActivity.WaitingTurn, …)` explicite dans le test nommé |
| La déduction n'est jamais **persistée** | `WaitingDeduced_n_est_jamais_ecrit_dans_un_fichier_d_etat` : boucle sur les 8 événements du `Cablage`, aucun `StateJson` ne porte la chaîne |
| Le ViewModel ne fabrique aucun libellé | `grep -n '"à toi\|"tour fini\|"en cours\|"inconnu' src/Chronos/ViewModels/SessionsViewModel.cs` → **aucune ligne** |

## Comptes de tests

| Mesure | Valeur |
|---|---|
| Entrée (après 25-02), **remesurée avant la première ligne écrite** | **837 / 0 échec / 5 s** |
| Après tâche 1 (l'attente déduite et son libellé) | **846** (+9), 0 échec |
| Après tâche 2 (le silence produit la déduction) | **853** (+7), 0 échec |
| Après tâche 3 (le widget la montre) — **total final** | **855** (+2), 0 échec, 4 s |
| Deuxième exécution consécutive | **855 / 0 échec / 5 s** — identique |

**855 = 837 + 18.** Aucun test supprimé, aucun test désactivé, aucun test mis en `Skip`.

### Écart à l'attendu indicatif (848) — justifié nominativement, et en deux parts

L'écart est de **+7**, et il se décompose exactement :

| Part | Montant | D'où elle vient |
|---|---|---|
| Décalage de BASE | **+4** | La carte partait de **833** pour l'entrée du 25-03 ; la mesure réelle après 25-02 était **837**. Cet écart était **déjà** justifié nominativement au SUMMARY 25-02 (corrections A4 et B6 du plan-checker + le 2ᵉ cas de la `[Theory]` de routage). Il se propage tel quel, il n'est pas neuf. |
| Tests SURNUMÉRAIRES en T2 | **+3** | La carte estimait ≈ +4 pour la tâche 2 ; sept tests y ont été livrés. Les trois surnuméraires sont **nommables un par un**, et tous trois ont été ajoutés par la revue du plan, postérieure à la carte. |

Les trois cas surnuméraires de la tâche 2 :

| Cas | D'où il vient |
|---|---|
| `Un_hook_deduit_et_un_transcript_travaillant_du_MEME_instant_sont_departages_par_la_source` | point ajouté par la revue — fige le cas d'égalité à la milliseconde hérité de la phase 24 |
| `WaitingDeduced_n_est_jamais_ecrit_dans_un_fichier_d_etat` | point ajouté par la revue — la promesse n'était portée que par une XML-doc |
| `Combien_de_sessions_d_un_corpus_realiste_basculent_ensemble_en_deduction` | **garde de masse**, contrepartie explicite de la décision de l'utilisateur du 2026-09-12 (point A11) |

Détail des 18 cas ajoutés :

| Tâche | Cas | Détail |
|---|---|---|
| T1 | **9** | 1 `[InlineData]` sur la `[Theory]` des libellés + `[Theory]` `Chaque_etat_a_son_rang_d_urgence` (**5 cas**) + 3 `[Fact]` (complétude, compacité, ordre) |
| T2 | **7** | les sept `[Fact]` nommés au plan |
| T3 | **2** | les deux `[Fact]` nommés au plan |

La cinquième entrée ajoutée à `SessionStylesBindingTests.Vm()` **n'ajoute aucun cas** : elle enrichit le
corpus des deux tests existants, elle n'en crée pas un troisième.

## Étapes ROUGE mesurées — les trois, avec noms et comptes

### T1 — **1 échec / 23**, et il faut dire pourquoi seulement un

```
[FAIL] Chronos.Tests.AffichageSessionsTests.Chaque_etat_a_son_libelle(a: WaitingDeduced, attendu: "à toi ? déduit")
Échoué!  - échec : 1, réussite : 22, total : 23
```

**Les quatre autres cas ajoutés en T1 étaient VERTS d'emblée, et il serait malhonnête de le taire.** Le
`_ => "inconnu"` et le `_ => 3` du switch absorbent le nouveau membre : la complétude, la compacité, le
rang et l'ordre passaient donc *avant* que la moindre ligne de production soit écrite. Ce ne sont pas des
gardes vides pour autant — ce sont des gardes **de non-retour**, et leur falsifiabilité est structurelle :

| Garde | Ce qui la ferait échouer |
|---|---|
| `Chaque_valeur_de_l_enumeration_a_un_libelle_non_vide` | un futur `Etat` qui rendrait `""` pour un membre, ou un `Enum.GetValues` qui ne rendrait plus 5 valeurs |
| `Aucun_libelle_d_etat_ne_depasse_seize_caracteres` | tout libellé de plus de seize caractères — « à toi ? déduit » en fait quatorze, la marge est de deux |
| `Chaque_etat_a_son_rang_d_urgence` / `Une_deduction_ne_passe_jamais_devant_une_observation` | `WaitingDeduced => 1` ou `=> 2`, c'est-à-dire précisément la faute que la doctrine interdit |

C'est exactement la raison pour laquelle le plan demandait d'**écrire** `SessionActivity.WaitingDeduced => 3`
bien que ce cas soit redondant avec le défaut : le défaut donne le bon résultat, il ne donne pas l'intention.

### T2 — **7 échecs / 25**

```
Échoué!  - échec : 7, réussite : 18, total : 25
```

| # | Test en échec |
|---|---|
| 1 | `Vingt_et_une_minutes_de_SILENCE_ne_se_disent_plus_en_cours` (hérité, assertion convertie) |
| 2 | `Un_battement_frais_maintient_en_cours_bien_au_dela_d_une_heure` (hérité, **4ᵉ** assertion — voir déviation n° 1) |
| 3 | `Combien_de_sessions_d_un_corpus_realiste_basculent_ensemble_en_deduction` |
| 4 | `Un_hook_Working_de_25_min_ne_rend_plus_un_transcript_frais_deduit` (hérité, renommé) |
| 5 | `Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini` |
| 6 | `Une_deduction_ne_bat_pas_un_transcript_plus_recent` |
| 7 | `Un_hook_deduit_et_un_transcript_travaillant_du_MEME_instant_sont_departages_par_la_source` |

Deux des sept tests neufs étaient verts à l'étape rouge, et pour la bonne raison :
`Une_attente_observee_ne_se_convertit_jamais_en_deduction` et
`Une_activite_illisible_reste_inconnue_et_non_deduite` sont des **non-régressions** — ils décrivent ce qui
ne devait précisément **pas** changer.

### T3 — **2 échecs / 25**

```
[FAIL] Une_session_deduite_est_visible_ambre_et_dit_sa_deduction
[FAIL] Le_compteur_d_attente_inclut_la_deduction
Échoué!  - échec : 2, réussite : 23, total : 25
```

## La mutation de falsification prévue au `25-VALIDATION.md`, jouée

Méthode obligatoire respectée : `git worktree` jetable (`chronos-mutant-deduction`), détaché sur le commit
de la tâche 2 (`701321b`), mutation **compilable**
(`activity = SessionActivity.WaitingTurn; // MUTANT`).

```
Échoué!  - échec : 7, réussite : 18, total : 25
```

`Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini` échoue — et il échoue sur son
`Assert.NotEqual(SessionActivity.WaitingTurn, …)`, c'est-à-dire **sur l'interdit lui-même**, pas sur un
effet de bord. Six autres tombent avec lui.

Worktree **révoqué** : `git worktree list` ne rend que le dépôt principal, `git status --porcelain` est
vide, `grep -rlF "MUTANT" --include=*.cs` rend **0**.

## LA GARDE DE MASSE — le nombre, et ce qu'il vaut vraiment

> ### **20 sessions sur 54 visibles basculent ensemble en « à toi ? déduit ».**
> `WaitingCount` correspondant : **42** (20 déduites + 20 « tour fini » + 2 « à toi »).

Mesuré par `Combien_de_sessions_d_un_corpus_realiste_basculent_ensemble_en_deduction`, à instant **figé**,
sur un corpus **déterministe** de soixante-six états écrits sous `Path.GetTempPath()`. Reporté au point
**A11** de `25-VALIDATION.md`, comme exigé.

Composition du corpus, et sa justification :

| Part | Nombre | Âges | Devient |
|---|---|---|---|
| `Working` frais | 12 | 1 à 12 min | reste `Working` |
| `Working` silencieux | 20 | 21 à 40 min | **`WaitingDeduced`** |
| `WaitingTurn` observées | 20 | 20 min à 6 h 40 | inchangées |
| `WaitingAttention` observées | 2 | 2 h | inchangées |
| États trop vieux | 12 | > 9 h | écartés pour leur âge, jamais lus |
| **Total écrit** | **66** | | 54 visibles, 12 écartés |

**La FORME du corpus vient du magasin réel** relevé le 2026-09-12 : 66 entrées, dont 54 fichiers `.json`
(32 `Working`, 20 `WaitingTurn`, 2 `WaitingAttention`) et **12 reliquats `.tmp-*`** — des orphelins de
l'époque tmp+Move, antérieure à la phase 23, que le moniteur ne lit pas (il n'énumère que `*.json`). Les
douze « états trop vieux » du corpus tiennent leur place.

**Mise en garde, et elle est importante — les ÂGES, eux, ne viennent PAS du magasin réel.** Mesure faite :
les 54 états réels sont **tous** au-delà du seuil de huit heures. Le magasin est un cimetière (c'est le
reliquat « résorption du magasin de 66 entrées » hérité de la phase 23). **In vivo, aujourd'hui, le nombre
serait donc zéro** — et un zéro ne garderait rien. Le corpus place délibérément les `Working` **de part et
d'autre** du seuil de silence, et les y place **serrés** : si le seuil descendait à dix minutes, deux des
douze « frais » basculeraient et le nombre deviendrait 22 ; s'il montait à vingt-cinq, quatre des vingt
« silencieux » cesseraient de basculer et il deviendrait 16. Le test est donc sensible dans les deux sens.

**Ce que ce nombre dit, et ce qu'il ne dit pas.** Il dit qu'un magasin VIVANT de la forme du magasin réel
verrait **un peu plus d'une ligne sur trois** passer en déduction, et que le compteur d'attente afficherait
42 pour 54 sessions. C'est beaucoup — et c'est exactement l'information que la décision A10 demandait de
connaître **avant** la republication. Il ne dit pas ce que l'utilisateur verra réellement : cela reste la
vérification in vivo A11, rangée au `25-VALIDATION.md`. **Si le widget se remplit de déductions, c'est le
choix de l'ÉVÉNEMENT PORTEUR qu'il faut rouvrir, pas le seuil** — la consigne est déjà écrite, elle n'a pas
été retouchée.

## Le comportement FIGÉ, et non corrigé : l'égalité à la milliseconde

`Un_hook_deduit_et_un_transcript_travaillant_du_MEME_instant_sont_departages_par_la_source` écrit ce qui
suit, **sans le modifier** :

Dans `ArbitrageSessions.Departager`, l'ordre des rangs est : **1. fraîcheur — 2. SOURCE — 3. urgence** —
4. motif — 5. projet. Le rang **source précède donc le rang urgence**. À horodatage rigoureusement
identique **à la milliseconde**, un hook l'emporte sur un transcript avant que l'urgence n'ait la parole :
un hook `WaitingDeduced` (urgence 3) bat donc un transcript `Working` (urgence 2).

Mesuré : la session visible porte `WaitingDeduced`, `SourceRetenue == Hook`, `SourceEcartee == Transcript`,
`EcartAge == TimeSpan.Zero`.

**Le test n'est pas vacueux** : si le rang d'urgence précédait le rang de source, le transcript
gagnerait et l'assertion tomberait. Il ne s'agit donc pas d'un test qui constate une tautologie, mais d'un
verrou sur un ordre de comparaison précis.

**Rien n'a été corrigé, et c'est délibéré.** Le `25-CONTEXT.md` exige qu'aucune retouche de l'arbitrage ne
se fasse sans qu'un test la motive ; ce test **constate**, il ne motive pas. Le constat est déjà consigné
au tableau « Entrées ouvertes, transmises à la phase 26 » du `25-VALIDATION.md`. Conformément à cela :

```
git diff --stat ce40e99..HEAD -- src/Chronos/Services/ArbitrageSessions.cs   →   VIDE
```

## L'unique retouche XAML de la phase

```
git diff --numstat ce40e99..HEAD -- src/Chronos/Resources/SessionStyles.xaml
1       1       src/Chronos/Resources/SessionStyles.xaml
```

Une ligne ajoutée, une retirée, **à l'intérieur du bloc de commentaire de tête (l. 5)** :

```diff
-         « à toi » (Attention) respire, « tour fini » (Turn) fixe ; déduit (Ghost) = fantôme. -->
+         « à toi » (Attention) respire, « tour fini » (Turn) fixe ; Ghost = inconnu, estompé — l'attente DÉDUITE, elle, reste lisible. -->
```

Aucune ligne ajoutée ou retirée ne porte `DataTemplate`, `Style`, `Trigger` ni `x:Key` — **mesuré à 0** sur
les lignes `+`/`-` du diff (le seul `x:Key` visible dans la sortie est une ligne de **contexte** non
modifiée, `<BooleanToVisibilityConverter x:Key="BoolToVis"/>`). `git diff --stat ce40e99..HEAD -- '*.xaml'`
ne rend que ce fichier.

Laisser ce commentaire faux aurait été livrer, dans la phase même qui interdit d'affirmer ce qui n'a pas
été observé, une affirmation fausse sur son propre écran.

## Valeurs réelles de chaque `grep` et de chaque `diff`

| Critère | Attendu | **Mesuré** |
|---|---|---|
| `grep -c 'WaitingDeduced' src/Chronos/Services/SessionSnapshot.cs` | ≥ 1 | **1** |
| `grep -c 'WaitingDeduced' src/Chronos/Services/AffichageSessions.cs` | ≥ 2 | **2** (le libellé et le rang) |
| `grep -c 'WaitingDeduced' src/Chronos/Services/SessionMonitor.cs` | ≥ 1 | **2** (la conversion et la XML-doc de tête) |
| `grep -c 'WaitingDeduced' tests/Chronos.Tests/SessionStylesBindingTests.cs` | ≥ 1 | **1** |
| les 4 tests nommés en T1 (c) | ≥ 4 | **4** |
| les 7 tests nommés en T2 | ≥ 7 | **7** |
| les 2 tests nommés en T3 | ≥ 2 | **2** |
| `grep -rc 'Un_hook_Working_de_25_min_ne_rend_plus_un_transcript_frais_inconnu' tests/` | **0** | **0** — l'ancien nom a disparu |
| `AffichageSessionsTests` : cas exécutés | ≥ 23 | **25**, 0 échec |
| `grep -n '"à toi\|"tour fini\|"en cours\|"inconnu' …/SessionsViewModel.cs` | aucune ligne | **aucune ligne** |
| les deux `Assert.Equal(5, …)` de `SessionStylesBindingTests` (l. 61 et 135) | présents | **présents**, les `Assert.Equal(4, …)` ont disparu |
| `grep -c 'FromHours(8)' …/SessionMonitor.cs` | ≥ 1 | **1** — `DropAfter` intact |
| `grep -c 'FromMinutes(20)' …/SessionMonitor.cs` | ≥ 1 | **1** — le seuil n'a pas bougé |
| `grep -c 'SilenceDesBattements' …/SessionMonitor.cs` | ≥ 3 | **4** (déclaration, usage, **deux** références de XML-doc — la nouvelle ligne d'EVT-04 en ajoute une) |
| `grep -rn 'StaleWorking' --include=*.cs src/` | aucune ligne | **aucune ligne** |
| `grep -cF 'byId[' …/SessionMonitor.cs` | 0 | **0** |
| `grep -cF 'Dictionary<string, SessionSnapshot>' …/SessionMonitor.cs` | 0 | **0** |
| `grep -cF 'ArbitrageSessions.Trancher(' …/SessionMonitor.cs` | 1 | **1** |
| `grep -cF '=> Inspecter(now).Visibles;' …/SessionMonitor.cs` | 1 | **1** |
| `grep -cF '?? new ' …/DiagnosticService.cs` | 2 | **2** |
| `grep -cF 'new SessionMonitor' …/DiagnosticService.cs` | 0 | **0** |
| `grep -cF 'MotifMasquage' …/DiagnosticService.cs` | 3 | **3** |
| `grep -c 'File.Move'` sur `SessionHookProcessor.cs` / `EcritureEtatSession.cs` | 0 / 0 | **0 / 0** |
| `git diff --stat $SHA..HEAD --` `ArbitrageSessions.cs` `LectureSessions.cs` `SessionTreatmentTracker.cs` `TreatedStore.cs` `ArchiveStore.cs` `DiagnosticService.cs` | **VIDE** | **VIDE** |
| `git diff --stat $SHA..HEAD -- '*.csproj'` | vide | **vide** |
| `git diff --numstat $SHA..HEAD -- …/SessionStyles.xaml` | ≤ 1 / 1 | **1 / 1** |
| lignes `+`/`-` du diff XAML portant `DataTemplate\|Style\|Trigger\|x:Key` | 0 | **0** |
| `git status --short` après les trois commits | vide | **vide** |

`git diff --stat 7a44fa3..HEAD` (portée de CE plan) : **exactement les 9 fichiers de `files_modified`**,
400 insertions / 27 suppressions. Aucun fichier hors périmètre touché.

## Suites de gardes — 0 échec partout

| Suite | Attendu | **Mesuré** |
|---|---|---|
| `GardesPerimetreTests` | 10, 0 échec | **10, 0 échec** |
| `NormalisationUniqueTests` | 3, 0 échec | **3, 0 échec** |
| `ServicesLayerPurityTests` | 2, 0 échec | **2, 0 échec** |
| `CompositionRootTests` | 5, 0 échec | **5, 0 échec** |
| `GardesDoctrineTests` | 8, 0 échec | **8, 0 échec** |
| `SessionStylesBindingTests` | 2, 0 échec | **2, 0 échec** — 8 styles × 9 thèmes, avec le 5ᵉ état et son libellé de 14 caractères dans la liste |

La **consigne de rédaction** est tenue : aucune des chaînes interdites (division ou multiplication par
cent, `FromUnixTimeSeconds`, `DateTimeOffset.TryParse`) n'a été écrite dans `SessionSnapshot.cs` ni
`AffichageSessions.cs`, commentaires et XML-doc compris — `NormalisationUniqueTests` le vérifie au TEXTE et
reste vert. `ServicesLayerPurityTests` confirme qu'aucun type WPF n'est descendu dans `Services/`.

## Deviations from Plan

### 1. [Rule 1 — bug révélé par la tâche] Il y avait QUATRE assertions héritées à corriger, pas trois

- **Trouvé pendant :** tâche 2, à l'étape rouge.
- **Problème :** le plan (corrigé par la revue, point B2) nommait **trois** assertions héritées à convertir
  d'`Unknown` vers `WaitingDeduced`. Il en existait une **quatrième**, non nommée :
  `InspectionSessionsTests.Un_battement_frais_maintient_en_cours_bien_au_dela_d_une_heure` (l. 328 avant ce
  plan). Son **TÉMOIN** est un `SessionStart` daté de deux heures et dix minutes ; ce `SessionStart` route
  vers `Working`, donc il franchit le seuil de silence exactement comme les trois autres, et il devient
  `WaitingDeduced`.
- **Pourquoi le plan ne pouvait pas la voir :** les trois assertions nommées écrivent explicitement
  `SessionActivity.Working` dans leur état de départ. Celle-ci ne l'écrit nulle part — elle passe par le
  **pipeline réel** (`SessionHookProcessor.Process("SessionStart", …)` → `EcritureEtatSession.Appliquer` →
  `SessionMonitor`), et le `Working` n'apparaît qu'à l'intérieur du routage.
- **Correctif :** assertion convertie et **commentaire du témoin réécrit**. Ce que le témoin doit prouver
  est **inchangé** : que « en cours » ne tient PAS tout seul, donc que le `Working` de la ligne suivante ne
  peut venir que du battement. C'est toujours ce qu'il prouve — la valeur du témoin est simplement
  `WaitingDeduced` au lieu d'`Unknown`, et il reste ≠ `Working`.
- **Fichier :** `tests/Chronos.Tests/InspectionSessionsTests.cs` — déjà dans `files_modified`.
- **Commit :** `701321b`
- **Conséquence pour `25-VALIDATION.md` :** la ligne « Renommages prévus » annonce **7** renommages, dont
  « 3 assertions héritées converties ». Il faut lire **4 conversions**, dont **1 renommage** (seul
  `…_frais_inconnu` → `…_frais_deduit` change de nom). Le compte de RENOMMAGES, lui, reste **7**.

### 2. [Rule 1 — bug dans le corpus que j'écrivais] Les âges des attentes du corpus de masse dépassaient huit heures

- **Trouvé pendant :** tâche 2, première exécution verte du reste de la suite.
- **Problème :** la première écriture du corpus étalait les vingt `WaitingTurn` par pas de trente minutes
  (`-30 × i`), ce qui portait les quatre dernières à 8 h 30, 9 h, 9 h 30 et 10 h — **au-delà de `DropAfter`**.
  Elles étaient donc écartées pour leur âge, et le corpus mesurait 50 lignes visibles au lieu des 54
  annoncées. Mesuré : `Assert.Equal() Failure: Expected 54, Actual 50`.
- **Ce que cela aurait coûté :** le nombre-clé (20) était **déjà juste** — ces quatre entrées ne basculaient
  pas en déduction. Mais le **dénominateur** aurait été faux, et un nombre de masse sans son dénominateur
  ne veut rien dire : « 20 sur 54 » et « 20 sur 50 » ne racontent pas la même chose.
- **Correctif :** pas de vingt minutes (`-20 × i`), soit de 20 min à 6 h 40 — toutes sous le seuil de huit
  heures. Les assertions de contrôle du corpus (54 visibles, 12 écartés, 12 `Working`, 20 `WaitingTurn`,
  2 `WaitingAttention`, 0 `Unknown`) existent précisément pour que le nombre principal ne puisse pas être
  juste par accident : **c'est l'une d'elles qui a attrapé la faute.**
- **Fichier :** `tests/Chronos.Tests/InspectionSessionsTests.cs`
- **Commit :** `701321b`

### 3. [Forme] La rampe ambre est assertée par COMPARAISON, jamais par une couleur recopiée

- Le plan demandait d'asserter que `StateBrush` est « la rampe AMBRE ». Recopier une valeur de couleur dans
  le test l'aurait rendu faux au premier changement de thème, et il y en a neuf. Le pinceau de la session
  déduite est donc comparé à celui qu'un `WaitingTurn` **reçoit du même thème** (égalité attendue) et à
  celui d'un `Unknown` (différence attendue). Le comportement livré est exactement celui du plan.

### 4. [Forme] Le corpus de masse ne copie pas les âges du magasin réel — et le dit

- Le plan demandait un corpus « dans la forme du magasin réel relevée au 2026-09-12 ». La **forme** (66
  entrées, 32/20/2 + 12 reliquats) est reprise ; les **âges** ne pouvaient pas l'être, puisque les 54 états
  réels sont tous au-delà de huit heures — un corpus qui les aurait copiés aurait rendu **zéro** et n'aurait
  rien gardé. Le choix est écrit dans la XML-doc du test, dans ce SUMMARY et au point A11.

Aucune autre déviation. **Aucune règle 4 (architecture) déclenchée**, aucune porte d'authentification
rencontrée, **aucune dépendance NuGet ajoutée**.

## Invariants de sécurité — relevés avant ET après

| Invariant | Attendu | **Avant → Après** |
|---|---|---|
| `~/.claude/settings.json` modifié | **jamais** | md5 `78eb517cc1a453b2ddce9a39af528900` → `78eb517cc1a453b2ddce9a39af528900` — **identique** |
| Sonde de capture installée dans `~/.claude/` | 0 | **0** |
| `%APPDATA%\Chronos\sessions` | 66 entrées | **66 → 66** (lu en LECTURE SEULE pour relever la forme du magasin ; aucune écriture) |
| `%APPDATA%\Chronos\archived.json` | 84 octets | **84 → 84** |
| `%APPDATA%\Chronos\oauth.dat` (**taille seule**) | 518 octets | **518 → 518** — mtime **non consulté** |
| Overlay `Chronos-v3.0.2.exe` (pid 119412) | vivant, ni lancé ni tué | **vivant, pid 119412** (`tasklist` seul) |
| Dépendances NuGet ajoutées | 0 | **0** (`git diff -- '*.csproj'` vide) |
| Tests écrivant hors de `Path.GetTempPath()` | 0 | **0** — les 66 états du corpus sont écrits sous `InspectionSessionsTests.TempDir()`, qui porte la garde `Assert.StartsWith(Path.GetTempPath(), d)` |
| Requêtes réseau réelles depuis un test | 0 | **0** |
| Horloge réelle dans les tests neufs | 0 | **0** — tous à instant FIGÉ (`Maintenant`) ; aucun test à retardement |
| Worktrees de mutation restants | 0 | **0** — `git worktree list` réduit au dépôt principal, 0 fichier `MUTANT` |

## Known Stubs

Aucun. Les quatre artefacts annoncés par le plan sont livrés, câblés et prouvés.

**Limite ASSUMÉE, non stub — et c'est la nature même d'EVT-04 :** la déduction ne distingue pas
l'interruption au clavier d'un terminal tué, d'une mise en veille ou d'un outil de plus de vingt minutes.
C'est la conséquence directe du fait qu'**aucun des 33 événements du catalogue n'émet sur Échap**, et c'est
précisément pour cela que le libellé porte une interrogation et le mot « déduit ». Cette limite doit figurer
au §5 de `docs/hooks-contract.md` (**EVT-05, plan 25-04**), avec le trou documentaire daté qui la fonde.

**Report assumé, non stub :** rien de ce milestone ne s'exécute avant republication de l'exe. La
vérification in vivo du rendu (8 styles × 9 thèmes avec « à toi ? déduit », 14 caractères) et le relevé réel
de l'effet de masse restent aux points 3, 5 et A11 de `25-VALIDATION.md`.

## À transmettre au plan 25-04 (et à la phase 26)

1. **Le SHA d'entrée de phase reste `ce40e99cffe605e2baa93511e2555884aa6acdb8`.**
2. **`SessionActivity` compte désormais CINQ membres**, `WaitingDeduced` étant le dernier. Toute table du
   contrat documenté qui énumère les états doit l'inclure, avec la mention qu'il est **dérivé à la lecture
   et jamais persisté** — c'est `WaitingDeduced_n_est_jamais_ecrit_dans_un_fichier_d_etat` qui le tient.
3. **`SessionTreatmentTracker.cs:39` (`EstAttente`) exclut toujours `WaitingDeduced`**, alors que le widget
   la compte dans `WaitingCount` : une session déduite n'ouvre donc **pas** d'épisode d'attente. **Ce n'est
   PAS corrigé ici**, délibérément — le diff de ce fichier est **VIDE**, comme exigé. À écrire au §3 de
   `docs/hooks-contract.md` (25-04) et à trancher en **phase 26**.
4. **Le cas d'égalité à la milliseconde est désormais ÉCRIT** (voir plus haut) et le diff
   d'`ArbitrageSessions.cs` reste vide. Le §6 du contrat documenté peut s'y référer par le nom du test.
5. **Le nombre de la garde de masse est 20 sur 54** — mais il mesure un magasin VIVANT, alors que le magasin
   réel est aujourd'hui entièrement périmé. La comparaison in vivo devra en tenir compte : un écart entre
   « 20 » et ce que l'utilisateur verra n'est pas nécessairement une régression.
6. **Le libellé le plus long du widget fait quatorze caractères**, pour une garde à seize. Tout nouvel état
   devra tenir dans cette marge, et `Aucun_libelle_d_etat_ne_depasse_seize_caracteres` le refusera sinon.

## Commits

| Tâche | Hash | Message |
|---|---|---|
| 1 | `d9c4fd5` | `feat(25-03): l'attente DEDUITE existe et son libelle porte l'incertitude (EVT-04)` |
| 2 | `701321b` | `feat(25-03): le silence apres travail produit la deduction, jamais un tour fini (EVT-04)` |
| 3 | `d2c9e7d` | `feat(25-03): le widget montre la deduction sans la faire passer pour une observation (EVT-04)` |

## Self-Check: PASSED

Fichiers modifiés — vérifiés présents et porteurs du changement :
- `src/Chronos/Services/SessionSnapshot.cs` — FOUND, `WaitingDeduced` × 1
- `src/Chronos/Services/AffichageSessions.cs` — FOUND, `WaitingDeduced` × 2
- `src/Chronos/Services/SessionMonitor.cs` — FOUND, `WaitingDeduced` × 2
- `src/Chronos/ViewModels/SessionsViewModel.cs` — FOUND, 0 libellé fabriqué
- `src/Chronos/Resources/SessionStyles.xaml` — FOUND, 1 ajout / 1 retrait
- `.planning/phases/25-le-contrat-d-evenements-refonde/25-03-SUMMARY.md` — FOUND

Commits — vérifiés présents dans `git log` : `d9c4fd5`, `701321b`, `d2c9e7d` — FOUND.

Suite complète : **855 tests, 0 échec**, deux exécutions consécutives identiques.
`git status --short` vide, `git worktree list` réduit au dépôt principal, 0 fichier `MUTANT`.
