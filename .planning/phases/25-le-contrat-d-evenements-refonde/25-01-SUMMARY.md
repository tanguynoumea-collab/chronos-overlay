---
phase: 25-le-contrat-d-evenements-refonde
plan: 01
subsystem: sessions
tags: [hooks, contrat-externe, liste-blanche, matcher, permission-request, notification, xunit]

sha_entree_de_phase: ce40e99cffe605e2baa93511e2555884aa6acdb8

requires:
  - phase: 15-la-purete-du-settings
    provides: "ClaudeSettingsReconciler / ClaudeSettingsJson — SEUL chemin autorisé vers settings.json"
  - phase: 24-l-arbitrage-par-fraicheur
    provides: "ArbitrageSessions / LectureSessions — acquis, diff VIDE dans ce plan"
provides:
  - "CatalogueEvenementsHooks — liste blanche des 33 noms d'événements + support du matcher, codée en dur"
  - "EvenementCable (événement + matcher + rôle) — câblage déclaratif, source de vérité unique"
  - "SessionHookInstaller.ApplyHooks(root, exe, wanted, cablage) — surcharge à câblage injecté, validée avant écriture"
  - "Purge Chronos LARGE : toutes les clés de hooks, sans jamais toucher un groupe tiers"
  - "PermissionRequest câblé ET routé vers WaitingAttention (EVT-01)"
  - "Veto de second rideau sur les neuf types du bus qui ne sont pas des demandes (EVT-02)"
affects: [25-02-battements-de-coeur, 25-03-interruption-deduite, 25-04-contrat-documente]

tech-stack:
  added: []
  patterns:
    - "Liste blanche consultée AVANT mutation du JSON : un nom inconnu produirait un hook MORT et MUET"
    - "Le matcher est le filtre PRIMAIRE ; la lecture d'un champ de stdin n'est qu'un VETO — elle ne produit jamais d'état, elle n'en retire que"
    - "Surcharge à dépendance injectée pour prouver un refus d'écriture sans salir la configuration réelle"
    - "Purge large + ensemble des clés effectivement purgées : une clé vide qu'on n'a jamais touchée n'est pas retirée"

key-files:
  created:
    - src/Chronos/Services/CatalogueEvenementsHooks.cs
    - tests/Chronos.Tests/CatalogueEvenementsHooksTests.cs
  modified:
    - src/Chronos/Services/SessionHookInstaller.cs
    - src/Chronos/Services/SessionHookProcessor.cs
    - tests/Chronos.Tests/SessionsTests.cs
    - tests/Chronos.Tests/ClaudeSettingsReconcilerTests.cs

key-decisions:
  - "permission_prompt est ABSENT du matcher de Notification ET vetoé au routage : PermissionRequest, l'événement dédié, en a seul la charge — deux chemins pour un même fait rendraient la source illisible."
  - "Le motif écrit pour PermissionRequest est le NOM DE L'ÉVÉNEMENT, jamais un champ de contexte de permission : deux lectures du relevé se sont contredites sur ce nom."
  - "Un type de notification absent ou futur produit quand même l'attente : le veto ne porte que sur des valeurs RELEVÉES, donc la disparition du champ ne peut jamais fabriquer ni perdre un état en silence."
  - "Events est CONSERVÉ mais DÉRIVÉ de Cablage : une seule liste à tenir, jamais deux à garder d'accord."

metrics:
  duration: "~50 min"
  completed: 2026-09-12
  tasks: 3
  commits: 3
  tests_avant: 788
  tests_apres: 817
---

# Phase 25 Plan 01 : Le contrat d'événements refondé — liste blanche, câblage validé, routage refondé

L'attente cesse de naître d'un proxy : `PermissionRequest` (événement dédié) est câblé et routé vers
« à toi », le bus `Notification` est filtré par `matcher` sur ses trois seules vraies demandes et vetoé
sur les neuf autres, et aucun nom d'événement ne peut plus être écrit dans `settings.json` sans avoir été
validé contre une liste blanche des 33 noms.

## SHA D'ENTRÉE DE PHASE

```
ce40e99cffe605e2baa93511e2555884aa6acdb8
```

Relevé par `git rev-parse HEAD` **avant le premier commit de la phase** (commit
`fix(25): appliquer les corrections du plan-checker`). **Les plans 25-02, 25-03 et 25-04 doivent réutiliser
CE SHA À L'IDENTIQUE** pour leurs critères `git diff --stat <SHA>..HEAD -- …` : un `git diff --stat` nu ne
compare que l'arbre de travail et reste MUET après un commit.

## Comptes de tests

| Mesure | Valeur |
|---|---|
| Baseline d'entrée de phase (mesurée avant la première ligne écrite) | **788 / 0 échec / 7 s** (run à froid) |
| Après tâche 1 (`CatalogueEvenementsHooksTests`) | 795 (+7) |
| Après tâche 2 (câblage déclaratif + purge large) | **803** (+8), 0 échec |
| Après tâche 3 (routage) — **total final** | **817** (+14), 0 échec, 5 s |
| Deuxième exécution consécutive | **817 / 0 échec / 5 s** — identique |

**817 = 788 + 29.** Aucun test supprimé.

### Écart à l'attendu indicatif (816) — justifié nominativement

`25-VALIDATION.md` posait des attendus **indicatifs** de ≈ +7 (T1), ≈ +7 (T2), ≈ +14 (T3) = 816.
Mesuré : **+7 / +8 / +14 = 817**. L'écart de **+1** est entièrement en tâche 2 : le plan y **nomme
huit** nouveaux tests au bloc (e) alors que l'estimation de la carte de validation en supposait sept.
Les huit tests nommés ont tous été écrits, aucun test surnuméraire n'a été ajouté. Le test
surnuméraire par rapport à l'estimation est donc, nominativement, le huitième de la liste du plan :
`La_purge_large_ne_prend_pas_pour_nous_le_hook_d_un_tiers_qui_porte_le_meme_argument`.

Détail des 29 cas ajoutés :

| Tâche | Cas ajoutés | Détail |
|---|---|---|
| T1 | 7 | 7 `[Fact]` dans `CatalogueEvenementsHooksTests` |
| T2 | 8 | les 8 tests nommés au bloc (e) du plan, tous `[Fact]` |
| T3 | 14 | 1 `[Fact]` PermissionRequest + `[Theory]` 3 demandes + 1 `[Fact]` alerte d'absence + `[Theory]` 8 autres types + 1 `[Fact]` type absent/futur |

## Étapes ROUGE mesurées

| Tâche | Filtre | Attendu | **Mesuré** |
|---|---|---|---|
| T1 | `CatalogueEvenementsHooksTests` | tous (squelette `NotImplementedException`) | **7 échecs / 7 tests** — `Le_catalogue_compte_trente_trois_noms_distincts`, `EstConnu_reconnait_les_noms_exacts_du_releve`, `EstConnu_refuse_une_faute_de_frappe_une_casse_differente_et_tout_rognage`, `Les_dix_evenements_sans_support_de_matcher_sont_refuses`, `Les_vingt_trois_autres_evenements_acceptent_un_matcher`, `Un_nom_inconnu_n_accepte_aucun_matcher`, `Les_cinq_noms_cables_aujourd_hui_sont_tous_connus` |
| T3 | `SessionsTests` | ≥ 10 | **exactement 10 échecs / 102 tests** — `PermissionRequest_fonde_l_attente_et_dit_le_nom_de_l_evenement`, `Une_alerte_d_absence_ne_fabrique_plus_aucun_etat`, et les **8** cas de `Les_huit_autres_types_du_bus_sans_demande_ne_fabriquent_plus_aucun_etat` (`permission_prompt`, `auth_success`, `elicitation_complete`, `elicitation_response`, `agent_completed`, `quota_auto_resume_fired`, `quota_auto_resume_stale`, `quota_auto_resume_disabled`) |

La tâche 2 n'était pas marquée `tdd="true"` dans le plan : aucune étape ROUGE n'y était demandée ni
mesurée (le câblage change une signature et ses sites d'appel dans la même tâche — l'exigence de
compilabilité à chaque commit interdit un échec de build intermédiaire).

## Valeurs réelles de chaque `grep` d'acceptation

| Critère | Attendu | **Mesuré** |
|---|---|---|
| `test -f src/Chronos/Services/CatalogueEvenementsHooks.cs` | vrai | **vrai** |
| filtre `CatalogueEvenementsHooksTests` | 0 échec, ≥ 6 tests | **0 échec, 7 tests** |
| `NormalisationUniqueTests` / `ServicesLayerPurityTests` | 3 / 2, 0 échec | **3 / 2, 0 échec** |
| `grep -c 'PermissionRequest' …/CatalogueEvenementsHooks.cs` | ≥ 1 | **1** |
| `grep -c 'MessageDisplay' …/CatalogueEvenementsHooks.cs` | ≥ 1 | **1** |
| `grep -E 'System\.Windows\|System\.IO\|DateTimeOffset\.UtcNow' …/CatalogueEvenementsHooks.cs` | aucune ligne | **aucune ligne** |
| filtre `SessionsTests\|ClaudeSettingsReconcilerTests\|ClaudeSettingsJsonTests\|StatusLineInstallerTests` | 0 échec | **0 échec, 160 tests** (avant T3) |
| `grep -c` des 8 tests nommés dans `SessionsTests.cs` | ≥ 8 | **8** |
| `grep -n 'CatalogueEvenementsHooks' …/SessionHookInstaller.cs` | ≥ 2 lignes | **3 lignes** (l. 143 XML-doc, l. 178 `EstConnu`, l. 179 `AccepteUnMatcher`) |
| `grep -c` des 3 anciens noms de tests (récursif sous `tests/`) | 0 | **0** |
| `grep -c 'PermissionRequest' …/SessionHookProcessor.cs` | ≥ 2 | **4** |
| `grep -c 'NotificationsSansEtat' …/SessionHookProcessor.cs` | ≥ 2 | **2** |
| `Le_catalogue_compte_trente_trois_noms_distincts` existe et passe | oui | **oui** — c'est LUI qui fixe le 33, aucun comptage par `grep` sur la source |
| `Une_alerte_d_absence_ne_fabrique_plus_aucun_etat` existe et passe | oui | **oui** — écrit en `[Fact]` dédié (`idle_prompt`), pas noyé dans la `[Theory]` de veto |
| `git diff --stat $SHA..HEAD -- App.xaml.cs ClaudeSettingsReconciler.cs SessionTreatmentTracker.cs TreatedStore.cs ArchiveStore.cs` | vide | **vide** |
| `git diff --stat $SHA..HEAD -- SessionTreatmentTracker.cs TreatedStore.cs ArchiveStore.cs ArbitrageSessions.cs LectureSessions.cs` | vide | **vide** |
| `git diff --stat $SHA..HEAD -- '*.xaml'` | (aucun XAML autorisé ici) | **vide** |
| `git diff --stat $SHA..HEAD -- '*.csproj'` | vide | **vide** |
| `git status --short` après les commits | vide | **vide** |

`git diff --stat $SHA..HEAD` global : **exactement les 6 fichiers de `files_modified`**, 576 insertions /
38 suppressions. Aucun fichier hors périmètre touché.

## La preuve que les hooks tiers survivent

Trois preuves distinctes, toutes vertes :

1. **`La_purge_large_epargne_les_hooks_d_un_autre_outil`** (nouveau) — un `settings.json` portant des
   groupes tiers sur `PreToolUse` (`node gsd-prompt-guard.js`, `matcher` `Write|Edit`, `timeout` 5) et
   `PostToolUse` (`node gsd-context-monitor.js`, `matcher` `Bash|Edit|Write`, `timeout` 20) ressort de
   `TransformForInstall` avec ces groupes **toujours à l'index 0**, commande, `matcher` et `timeout`
   d'origine intacts. Le test **n'asserte jamais la longueur du tableau** : le plan 25-02 y ajoutera un
   groupe Chronos APRÈS, et ce test doit rester vert sans retouche.
2. **`La_purge_large_ne_prend_pas_pour_nous_le_hook_d_un_tiers_qui_porte_le_meme_argument`** (nouveau) —
   un groupe tiers sur `PreCompact` dont la commande est `node outil.js --hook PreCompact` (marqueur
   **présent**, exécutable **non Chronos**) et un groupe dont le handler est `{"type":"http","url":…}`
   (**sans** `command`) survivent tous deux, intacts et dans l'ordre, à `TransformForInstall` **ET** à
   `TransformForUninstall`.
3. **`Preserve_les_trois_hooks_GSD_avec_leur_matcher_et_leur_timeout`** (existant, inchangé) — sur la
   fixture réelle du 2026-09-09, les trois hooks GSD survivent avec leur `matcher`, leur `timeout` et leur
   clé inconnue `if`, et le groupe GSD de `SessionStart` reste **en tête**. Il reste vert avec la purge
   élargie.

**Invariant de conception qui rend cela vrai :** la purge ne retire que les groupes reconnus par
`ClaudeSettingsJson.IsChronosGroup` (marqueur `--hook` **ET** exécutable `Chronos*.exe`) ; une clé dont la
valeur n'est **pas un tableau** est ignorée sans être écrite ; et **seules les clés dont on a effectivement
retiré un groupe à nous** peuvent être supprimées si elles deviennent vides — une clé vide qu'on n'a jamais
touchée appartient à un autre outil et n'est jamais mutilée.

## Le piège des littéraux `5` — désamorcé, et non rouvert

Le plan-checker avait identifié **exactement deux** littéraux `5` à convertir en
`SessionHookInstaller.Events.Length`. Les deux ont été convertis :

| Fichier:ligne (avant) | Contenu | Action |
|---|---|---|
| `ClaudeSettingsReconcilerTests.cs:67` | `Assert.Equal(5, CompteHooks(apres!, HookMarker))` | → `Events.Length` |
| `ClaudeSettingsReconcilerTests.cs:253` | `Assert.Equal(5, CompteHooks(File.ReadAllText(settings), HookMarker))` | → `Events.Length` |

**Tous les autres `5` de ces deux fichiers sont intacts**, vérifié après coup par `grep` :
`SessionsTests.cs:135`, `:142`, `:181` et `ClaudeSettingsReconcilerTests.cs:102` sont des `timeout`
appartenant à un **autre outil** ; `ClaudeSettingsReconcilerTests.cs:329` est la rétention de sauvegardes.
(Les deux dernières lignes ont glissé de +1 par rapport au plan — `:101` → `:102`, `:328` → `:329` —
du fait des deux lignes ajoutées à la XML-doc du test renommé ; ce sont bien les mêmes assertions.)
L'assertion d'entrée `Assert.Equal(25, …)` reste un littéral : c'est un fait figé du 2026-09-09, pas une
conséquence du code.

## Renommages — 3, tous annoncés au plan

| Avant | Après |
|---|---|
| `SessionsTests.Install_ajoute_les_5_hooks_en_slashes_avant` | `Install_pose_un_groupe_par_evenement_cable_en_slashes_avant` |
| `SessionsTests.Trois_chemins_dexe_successifs_ne_laissent_que_cinq_hooks` | `Trois_chemins_dexe_successifs_ne_laissent_qu_un_groupe_par_evenement` |
| `ClaudeSettingsReconcilerTests.Purge_la_fixture_reelle_de_25_a_5_hooks_Chronos` | `Purge_la_fixture_reelle_de_25_groupes_a_un_seul_par_evenement_cable` |

Aucun renommage supplémentaire. Aucun test supprimé.

## Deviations from Plan

### 1. [Rule 3 — blocage] `Notification_conserve_le_notification_type_en_reason` : fixture adaptée

- **Trouvé pendant :** tâche 3.
- **Problème :** ce test existant (non listé au plan) utilisait `notification_type: "permission_prompt"`
  et lisait `r.StateJson!`. Or `permission_prompt` devient **vetoé** par EVT-02 : `StateJson` vaut
  désormais `null` et le test aurait levé. Sans correctif, la tâche 3 n'était pas livrable.
- **Correctif :** la valeur de fixture passe à `agent_needs_input` — l'un des trois types qui restent de
  vraies demandes. **Le nom du test, son intention (le type voyage dans `reason`) et sa forme sont
  inchangés** ; ce n'est pas un renommage et il n'entre pas au compte des 7 renommages prévus pour la
  phase. Un commentaire de quatre lignes dans le test explique le pourquoi du changement de valeur.
- **Fichier :** `tests/Chronos.Tests/SessionsTests.cs`
- **Commit :** `7e9c31f`

### 2. [Rule 3 — outillage] Aide privée `StdinNotification(string type)` dans `SessionsTests`

- **Trouvé pendant :** tâche 3.
- **Problème :** les deux `[Theory]` de notification doivent injecter un type variable dans un stdin JSON
  contenant un chemin Windows échappé. L'interpolation de chaîne brute mêlée à des antislashs est une
  source d'erreur silencieuse (le JSON reste valide mais le champ change).
- **Correctif :** une aide privée d'une ligne bâtit le stdin, avec un `cwd` en **slashes avant**
  (`C:/p`) — `ProjectFromCwd` les normalise déjà, donc le comportement testé est identique et le littéral
  n'a plus d'antislash. Aucun test existant n'utilise cette aide.
- **Fichier :** `tests/Chronos.Tests/SessionsTests.cs`
- **Commit :** `7e9c31f`

### 3. [Forme] `EvenementCable` déclaré au niveau du namespace

- Le plan montrait `EvenementCable` dans l'extrait de `SessionHookInstaller`. Il est déclaré **au niveau
  du namespace `Chronos.Services`**, juste au-dessus de la classe, et non imbriqué : les tests écrivent
  `new EvenementCable(...)` sans qualification, exactement comme le plan le fait au bloc (e)
  (`new EvenementCable("Notifcation", null, "faute de frappe")`). Un type imbriqué aurait exigé
  `SessionHookInstaller.EvenementCable`, contredisant le code que le plan demande d'écrire.

Aucune autre déviation. Aucune règle 4 (architecture) déclenchée, aucune porte d'authentification
rencontrée, aucune dépendance NuGet ajoutée.

## Acquis des phases précédentes — revérifiés

| Acquis | Attendu | Mesuré |
|---|---|---|
| Phase 20 — `?? new ` dans `DiagnosticService.cs` | 2 | **2** |
| Phase 22 — `=> Inspecter(now).Visibles;` dans `SessionMonitor.cs` | 1 | **1** |
| Phase 22 — `new SessionMonitor` dans `DiagnosticService.cs` | 0 | **0** |
| Phase 23 — `File.Move` dans `SessionHookProcessor.cs` / `EcritureEtatSession.cs` | 0 / 0 | **0 / 0** |
| Phase 24 — `MotifMasquage` dans `DiagnosticService.cs` | 3 | **3** |
| Phase 24 — diff `ArbitrageSessions.cs` / `LectureSessions.cs` | vide | **vide** |
| Phase 26 — diff `SessionTreatmentTracker.cs` / `TreatedStore.cs` / `ArchiveStore.cs` | vide | **vide** |
| `GardesPerimetreTests` | 10, 0 échec | **10, 0 échec** |
| `NormalisationUniqueTests` | 3, 0 échec | **3, 0 échec** |
| `ServicesLayerPurityTests` | 2, 0 échec | **2, 0 échec** |
| `CompositionRootTests` | 5, 0 échec | **5, 0 échec** |
| `GardesDoctrineTests` | 8, 0 échec | **8, 0 échec** |
| `SessionStylesBindingTests` | 2, 0 échec | **2, 0 échec** |

## Invariants de sécurité — relevés après exécution

| Invariant | Attendu | **Mesuré** |
|---|---|---|
| `~/.claude/settings.json` modifié à la main | **jamais** | **jamais** — relecture seule : toujours **5** hooks Chronos (`SessionStart`, `Notification`, `Stop`, `UserPromptSubmit`, `SessionEnd`), **aucun** `PermissionRequest`, **aucun** `matcher`. Conforme : seul l'exe republié y écrira, au prochain lancement, sous le contrôle de l'utilisateur. |
| Sonde de capture installée dans `~/.claude/` | 0 | **0** |
| Hooks `PreToolUse` / `PostToolUse` de l'autre outil | intacts | **intacts** (non comptés comme nôtres, non modifiés) |
| `%APPDATA%\Chronos\sessions` | 66 entrées | **66** |
| `%APPDATA%\Chronos\archived.json` | 84 octets | **84** |
| `%APPDATA%\Chronos\oauth.dat` (**taille seule**) | 518 octets | **518** — mtime **non consulté** |
| Overlay `Chronos-v3.0.2.exe` (pid 119412) | vivant, ni lancé ni tué | **vivant, pid 119412** (`tasklist` seul) |
| Dépendances NuGet ajoutées | 0 | **0** (`git diff -- '*.csproj'` vide) |
| Tests écrivant hors de `Path.GetTempPath()` | 0 | **0** — `Aucun_test_ne_cible_le_vrai_settings_du_profil` reste vert ; les 8 nouveaux tests d'installateur sont des cœurs PURS (chaînes et `JsonObject`), aucun n'ouvre de fichier |
| Requêtes réseau réelles depuis un test | 0 | **0** |

## Known Stubs

Aucun. Les trois artefacts du plan sont pleinement câblés et prouvés.

**Report assumé, non stub :** le matcher installé filtre en amont, mais la configuration de l'utilisateur
ne le portera qu'**après republication de l'exe suivie d'une réconciliation au démarrage**. C'est la
condition déjà écrite au `25-CONTEXT.md` et au `25-VALIDATION.md`, pas une lacune de ce plan. Le **veto de
second rideau** existe précisément pour couvrir l'intervalle : un réglage encore sans `matcher` ne
fabriquera plus d'attente sur `auth_success` ni sur une reprise de quota.

## Commits

| Tâche | Hash | Message |
|---|---|---|
| 1 | `4f6d90d` | `feat(25-01): liste blanche des 33 noms d'evenements de hooks (EVT-01/EVT-02)` |
| 2 | `0277e78` | `feat(25-01): cablage declaratif valide avant ecriture, purge large (EVT-01/EVT-02)` |
| 3 | `7e9c31f` | `feat(25-01): PermissionRequest fonde l'attente, le bus cesse d'en fabriquer une (EVT-01/EVT-02)` |

## À transmettre aux plans 25-02 à 25-04

1. **Le SHA d'entrée de phase est `ce40e99cffe605e2baa93511e2555884aa6acdb8`.** À réutiliser tel quel.
2. **`Cablage` est la source de vérité unique.** Ajouter un événement (25-02 : `PreToolUse` /
   `PostToolUse`) = ajouter une entrée `EvenementCable`, rien d'autre. `Events` en dérive
   automatiquement, et les tests qui comparent à `Events.Length` suivent seuls.
3. **`Le_cablage_est_exactement_celui_que_la_phase_annonce` devra être mis à jour** par 25-02 : il fige
   le contenu exact du câblage (`SessionStart`, `UserPromptSubmit`, `Stop`, `SessionEnd`,
   `PermissionRequest`, `Notification`). C'est voulu — c'est le seul test qui échoue si l'on ajoute un
   événement sans le dire.
4. **`La_purge_large_epargne_les_hooks_d_un_autre_outil` n'asserte aucune longueur de tableau** : quand
   25-02 posera un groupe Chronos sur `PreToolUse` / `PostToolUse` derrière les groupes tiers, ce test
   doit rester vert **sans retouche**. S'il fallait le retoucher, c'est que la préservation des tiers a
   régressé.
5. **`SessionHookProcessor.Process` n'a pas encore de veto sous-agent** (`agent_id` / `agent_type`) :
   c'est explicitement le périmètre de 25-02 (EVT-03), et `grep -c 'agent_id'` sur ce fichier rend
   aujourd'hui **0**, conformément au plan.

## Self-Check: PASSED

Fichiers créés — vérifiés présents :
- `src/Chronos/Services/CatalogueEvenementsHooks.cs` — FOUND
- `tests/Chronos.Tests/CatalogueEvenementsHooksTests.cs` — FOUND
- `.planning/phases/25-le-contrat-d-evenements-refonde/25-01-SUMMARY.md` — FOUND

Commits — vérifiés présents dans `git log` : `4f6d90d`, `0277e78`, `7e9c31f` — FOUND.

Suite complète : **817 tests, 0 échec**, deux exécutions consécutives identiques. `git status --short` vide.
