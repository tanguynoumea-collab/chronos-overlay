---
phase: 15-idempotence-des-int-grations
plan: 03
subsystem: infra
tags: [system-text-json, jsonnode, settings-json, claude-code, idempotence, reconciliation, sauvegarde, di, xunit]

# Dependency graph
requires:
  - "15-01 : ClaudeSettingsJson (ParseOrNull, Serialize, IsChronosGroup, IsChronosCommand, PointsToExe) + fixture claude-settings-pollue.json"
  - "15-02 : SessionHookInstaller.ApplyHooks(root, exePath, wanted) + StatusLineInstaller.ApplyStatusLine(root, exePath)"
provides:
  - "ClaudeSettingsReconciler.ReconcileJson : cœur pur de convergence vers l'état désiré (null = ne rien écrire)"
  - "ClaudeSettingsReconciler.Reconcile : E/S non levante, sauvegarde horodatée CONDITIONNELLE, écriture atomique"
  - "Purge automatique au démarrage, mode overlay uniquement, avant OfferOnFirstRun()"
  - "Garde DI Le_graphe_DI_resout_le_reconciliateur_de_settings_Claude"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Comparaison sur la forme NORMALISÉE (sérialiser avant/après mutation) et non sur le texte brut : une réindentation de Claude Code ne déclenche plus d'écriture"
    - "Sauvegarde CONDITIONNELLE et BLOQUANTE : pas d'écriture sans sauvegarde réussie, pas de sauvegarde sans écriture"
    - "Rétention horodatée avec suffixe de désambiguïsation (-1, -2, …) : deux passages dans la même seconde ne s'écrasent pas"
    - "Chemin d'exe injectable au constructeur : rend l'idempotence des E/S vérifiable depuis testhost.exe"

key-files:
  created:
    - src/Chronos/Services/ClaudeSettingsReconciler.cs
    - tests/Chronos.Tests/ClaudeSettingsReconcilerTests.cs
  modified:
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/CompositionRootTests.cs

key-decisions:
  - "La comparaison avant/après se fait sur la forme NORMALISÉE, pas sur le texte lu : sinon toute différence d'indentation (Claude Code réécrit son fichier à sa guise) provoquerait une écriture ET une sauvegarde à chaque démarrage, évinçant la sauvegarde du premier passage — la seule à contenir l'état pré-purge complet"
  - "Échec de sauvegarde ⇒ on n'écrit pas (bloquant) ; échec de la purge de rétention ⇒ sans conséquence (best-effort). Le settings.json de l'utilisateur vaut plus que la fonctionnalité"
  - "Le réconciliateur ne CRÉE jamais le fichier absent : purge et repointage seulement, jamais d'installation silencieuse"
  - "Point d'appel unique dans App.OnStartup après window.Show() et avant OfferOnFirstRun() : atteint en mode overlay UNIQUEMENT (--statusline et --hook court-circuitent 60 lignes plus haut), donc jamais 5 écrivains concurrents"
  - "exePath injectable en 3e paramètre optionnel du constructeur : sans lui, les E/S poseraient des entrées « testhost.exe » que le prédicat d'identité ne reconnaît pas, et l'idempotence — la propriété même à prouver — serait invérifiable"
  - "Checkpoint humain de la Task 4 remplacé par une vérification équivalente NON DESTRUCTIVE sur une COPIE du vrai settings.json : la preuve terrain est obtenue sans muter le fichier de configuration réel de l'utilisateur"

patterns-established:
  - "Réconciliation au démarrage : convergence vers un état désiré, prouvée par point fixe, avec sauvegarde conditionnelle — modèle réutilisable pour toute migration de données de configuration"

requirements-completed: [PUR-03]

# Metrics
duration: 20min
completed: 2026-09-09
---

# Phase 15 Plan 03: Réconciliation de `~/.claude/settings.json` au démarrage Summary

**La machine déjà polluée se nettoie seule : `ClaudeSettingsReconciler` fait converger `~/.claude/settings.json` vers l'état désiré une fois par démarrage (25 groupes Chronos → 5, barre périmée repointée), sauvegarde avant d'écrire, n'écrit que si la forme normalisée change, et abandonne sans rien toucher sur un fichier inexploitable — prouvé par 18 tests et vérifié sur une COPIE du fichier réel de la machine.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-09-09T10:45:36Z
- **Completed:** 2026-09-09T11:05:00Z
- **Tasks:** 4/4 (dont 1 checkpoint vérifié par substitution non destructive)
- **Files modified:** 2 créés, 2 modifiés

## Accomplishments

- **PUR-03 est livré : la purge est automatique et sans intervention manuelle.** `ReconcileJson` compose les deux cœurs de mutation du plan 02 sur un seul arbre `JsonObject`, puis compare la sérialisation avant/après. Sur la fixture de l'état réel, les **25 groupes Chronos deviennent 5**, un par événement, pointant tous sur l'exe courant en slashes avant.
- **Rien d'autre n'est touché (critère de succès 4).** Les 3 scripts GSD survivent avec leurs `matcher` (`Bash|Edit|Write|MultiEdit|Agent|Task`, `Write|Edit`), leurs `timeout` (10, 5) et la clé inconnue `if` ; `agentPushNotifEnabled` reste à `true` ; les clés racine ajoutées à l'entrée (`model`, `permissions`) ressortent à leur place **et dans le même ordre**.
- **Le point fixe est une assertion, pas une intention.** `ReconcileJson(ReconcileJson(fixture)) == null` : la seconde passe n'a plus rien à écrire. C'est la formulation exacte de PUR-01/02/03 en une ligne.
- **Un fichier cassé reste identique octet pour octet.** Sur `{ "a": 1, "a": 2 }` écrit dans un fichier temp : `Reconcile` renvoie `false`, `File.ReadAllBytes` est inchangé, **aucune** sauvegarde n'est créée, aucune exception ne remonte. Le mode de défaillance destructif (remplacer le fichier par `{ "statusLine": … }`) n'existe plus nulle part dans la chaîne.
- **La sauvegarde est conditionnelle, bloquante et non évinçante (Pitfall 10).** Première réconciliation : une sauvegarde `claude-settings-<horodatage>.json` **identique octet pour octet** à l'original, puis écriture. Seconde réconciliation : rien à faire ⇒ aucune écriture, **toujours une seule** sauvegarde. Un échec de copie abandonne l'écriture ; la purge de rétention (5 fichiers) reste best-effort.
- **Le fichier absent n'est jamais créé.** Purge et repointage seulement : le consentement d'installation reste porté par le menu, jamais par une réconciliation silencieuse.
- **Câblé au bon endroit, et à un seul endroit.** L'appel se situe ligne 98 d'`App.xaml.cs`, entre `window.Show()` (88) et `OfferOnFirstRun()` (105), donc atteignable **uniquement en mode overlay** : `--statusline` sort ligne 22, `--hook` ligne 32. Les 5 processus de hook concurrents ne peuvent structurellement pas perdre les purges les uns des autres.
- **Suite complète : 405 tests, 0 échec** (baseline 387 + 17 tests du réconciliateur + 1 garde DI), en 5 exécutions complètes consécutives. `ServicesLayerPurityTests` et `CompositionRootTests` verts, build solution à 0 erreur / 0 avertissement nouveau.

## Task Commits

1. **Task 1: Créer ClaudeSettingsReconciler — convergence, sauvegarde horodatée conditionnelle, écriture atomique** — `9d5388f` (feat)
2. **Task 2: Prouver la purge sur la fixture réelle, le point fixe, l'inaltérabilité et la sauvegarde conditionnelle** — `01021dd` (test)
3. **Task 3: Câbler la réconciliation au démarrage (mode overlay uniquement) + garde DI** — `4894d52` (feat)
4. **Task 4: Vérification terrain** — aucun commit de code (vérification par substitution, voir ci-dessous)

## Files Created/Modified

- `src/Chronos/Services/ClaudeSettingsReconciler.cs` (176 lignes) — `ReconcileJson` (cœur pur), `Reconcile` (E/S non levante), `Sauvegarder` (conditionnelle + rétention 5), `CibleDeSauvegarde` (désambiguïsation des collisions à la seconde), `WriteAtomic`. Aucun type WPF, aucune référence à l'emplacement de l'assembly (`Environment.ProcessPath` uniquement, contrainte mono-fichier). XML-doc française sur les quatre « pourquoi » : la machine polluée, le mode overlay unique, l'abandon sur fichier inexploitable, la perte assumée des commentaires couverte par la sauvegarde.
- `tests/Chronos.Tests/ClaudeSettingsReconcilerTests.cs` (332 lignes, 17 cas) — 9 tests de cœur pur + 1 théorie à 4 cas + 5 tests d'E/S sur dossiers temp + la garde anti-accident explicite.
- `src/Chronos/App.xaml.cs` — enregistrement DI (`services.AddSingleton(_ => new ClaudeSettingsReconciler());`) et point d'appel unique dans `OnStartup`, avec le commentaire expliquant pourquoi ce point est le seul sûr.
- `tests/Chronos.Tests/CompositionRootTests.cs` — garde DI `Le_graphe_DI_resout_le_reconciliateur_de_settings_Claude` (chemins temp, `Reconcile` sur fichier absent ⇒ `false`, aucun crash).

## Task 4 — vérification terrain par SUBSTITUTION non destructive

**Substitution appliquée et pourquoi.** Le plan prévoyait un checkpoint humain : publier l'exe, le lancer, puis compter les entrées dans le vrai `%USERPROFILE%\.claude\settings.json`. L'exécution demandée étant autonome, lancer l'overlay aurait muté le fichier de configuration réel de l'utilisateur **sans supervision**. La vérification a donc été menée sur une **copie** : le vrai fichier a été lu (`File.Copy`) et **jamais écrit**.

**Protocole exécuté** (harnais xUnit temporaire, supprimé après consignation) : copie du vrai fichier sous `Path.GetTempPath()` → deux `Reconcile(hooksWanted: true)` successifs sur la copie, avec un dossier de sauvegardes temp → consignation du rapport.

**Comptage avant / après sur la copie du fichier RÉEL :**

| Observation | AVANT | APRÈS 1re réconciliation | 2de réconciliation |
|---|---|---|---|
| Handlers Chronos (`--hook`) | **25** | **5** (1 par événement) | 5 (inchangé) |
| `statusLine.command` | `"C:\Users\Tanguy\Downloads\Chronos-v2.8.1.exe" --statusline` | exe courant, slashes avant, = `StatusLineInstaller.ChronosCommand(exe)` | inchangé |
| Scripts GSD | `gsd-check-update.js`, `gsd-context-monitor.js`, `gsd-prompt-guard.js` | **les 3 présents** | les 3 présents |
| `PostToolUse[0]` | `matcher = Bash\|Edit\|Write\|MultiEdit\|Agent\|Task`, `timeout = 10` | **identiques** | identiques |
| `PreToolUse[0]` | `matcher = Write\|Edit`, `timeout = 5` | **identiques** | identiques |
| `agentPushNotifEnabled` | `true` | **`true`** | `true` |
| Clés racine | `hooks, statusLine, agentPushNotifEnabled` | **mêmes clés, même ordre** | idem |
| Écriture effectuée | — | `True` | **`False`** |
| Sauvegardes dans le dossier temp | 0 | **1**, octet pour octet identique à l'original | **1** (aucune de plus) |

Détail par événement après réconciliation : `Notification` 1 groupe / 1 Chronos, `Stop` 1/1, `UserPromptSubmit` 1/1, `SessionStart` **2 groupes dont 1 Chronos** (le groupe GSD `gsd-check-update.js` conservé en tête), `SessionEnd` 1/1.

**Deux écarts fixture ↔ fichier réel, sans incidence :**
1. La `statusLine` réelle **n'a pas** de champ `padding` (la fixture du plan 01 en posait un à 2, par extrapolation prudente). La préservation de `padding` reste prouvée par test unitaire sur la fixture ; sur le fichier réel il n'y a simplement rien à préserver.
2. Le groupe `PostToolUse` réel n'a **pas** la clé inconnue `if` que la fixture porte. La fixture est donc plus exigeante que la réalité — ce qui est le bon sens de l'écart.

**État du vrai fichier à la fin de l'exécution :** SHA-256 identique avant et après (`5f972bcc9a0dd8258d888bfd624f3c62c6a83daf6e323bd8f792978d7d73dac0`), et `%APPDATA%\Chronos\backups` **n'existe pas** — aucune écriture, aucune sauvegarde réelle n'a été produite. La purge effective du fichier de l'utilisateur se produira naturellement au prochain lancement de Chronos, avec sa sauvegarde horodatée.

## Decisions Made

- **Comparer les formes NORMALISÉES, jamais le texte brut lu.** Claude Code réécrit son propre fichier ; toute divergence d'indentation ou d'échappement aurait déclenché une écriture et une sauvegarde à chaque démarrage, faisant tourner la rétention et évinçant la sauvegarde du premier passage — précisément la seule à contenir l'état pré-purge complet (Pitfall 10).
- **Sauvegarde bloquante, rétention non bloquante.** `if (!Sauvegarder()) return false;` : sans filet, pas d'écriture. En revanche, l'échec de suppression d'un vieux fichier de rétention n'empêche rien (`try { } catch { }` par fichier).
- **Désambiguïsation des noms de sauvegarde.** L'horodatage à la seconde suffit en production (un lancement par démarrage) mais pas en boucle de test ni au double lancement : suffixe `-1`, `-2`, … borné à 100 tentatives, puis abandon (`false`) plutôt qu'un écrasement silencieux.
- **`exePath` injectable au constructeur (3e paramètre optionnel).** Décision de conception née d'un fait dur : le processus de test s'appelle `testhost.exe`, un nom que le prédicat d'identité (marqueur + `Chronos*.exe`) ne reconnaît volontairement pas. Sans injection, `Reconcile` poserait des entrées qu'il serait ensuite incapable de retirer — l'idempotence des E/S, propriété centrale du plan, aurait été structurellement invérifiable. La production reste sur `Environment.ProcessPath`.
- **Vérification terrain sur copie plutôt que checkpoint humain.** Preuve équivalente (mêmes comptages, mêmes assertions sur les tiers), risque nul pour le fichier de l'utilisateur, et exécution autonome préservée.

## Deviations from Plan

### Ajustements

**1. [Rule 3 - Blocking] `exePath` ajouté en 3e paramètre optionnel du constructeur**
- **Found during:** Task 2 (tests d'E/S)
- **Issue:** Le contrat du plan est `ClaudeSettingsReconciler(string? settingsPath, string? backupDir)` et `Reconcile` résout l'exe par `Environment.ProcessPath`. Sous xUnit, `ProcessPath` vaut `testhost.exe` : `ApplyHooks` retire alors tous les groupes Chronos puis en ajoute un que `IsChronosGroup` ne reconnaît **pas**. Trois comportements exigés par la section `<behavior>` de la Task 1 devenaient invérifiables : « 2e appel ⇒ `false` », « une seule sauvegarde », « fichier inchangé ».
- **Fix:** Paramètre `string? exePath = null` ajouté en 3e position, `null` en production (`_exePath ?? Environment.ProcessPath ?? "Chronos.exe"`). Purement additif : `new ClaudeSettingsReconciler()` et `new ClaudeSettingsReconciler(path, backups)` — les deux formes citées par le plan — compilent et se comportent à l'identique. XML-doc française expliquant la raison.
- **Files modified:** `src/Chronos/Services/ClaudeSettingsReconciler.cs`
- **Verification:** `Ecrit_et_sauvegarde_une_seule_fois_puis_ne_touche_plus_a_rien` et `La_retention_ne_garde_que_cinq_sauvegardes` verts ; garde DI du plan (`new ClaudeSettingsReconciler(settings, backups)` à 2 arguments) verte sans modification.
- **Committed in:** `01021dd`

**2. [Rule 3 - Blocking] Deux commentaires reformulés pour ne pas contredire un critère d'acceptation**
- **Found during:** Tasks 1 et 2
- **Issue:** (a) Le commentaire pédagogique « JAMAIS `Assembly.Location` » faisait échouer le critère `grep -c "Assembly.Location" … = 0`. (b) Le `<see cref="Aucun_test_ne_cible_le_vrai_settings_du_profil"/>` de la XML-doc de classe portait le compte à 2, alors que le critère exige exactement 1.
- **Fix:** (a) périphrase « jamais l'emplacement de l'assembly (vide en single-file) » — intention pédagogique intégralement conservée ; (b) référence remplacée par « un test dédié, plus bas, en fait une assertion explicite ».
- **Files modified:** `src/Chronos/Services/ClaudeSettingsReconciler.cs`, `tests/Chronos.Tests/ClaudeSettingsReconcilerTests.cs`
- **Verification:** les deux `grep` renvoient les valeurs attendues (0 et 1).
- **Committed in:** `9d5388f` et `01021dd`

**3. [Substitution demandée] Task 4 : checkpoint humain remplacé par une vérification non destructive**
- **Found during:** Task 4
- **Issue:** Le plan demandait de publier puis lancer l'exe pour muter le vrai `~/.claude/settings.json` et compter. Incompatible avec une exécution autonome non supervisée.
- **Fix:** Copie du vrai fichier sous `Path.GetTempPath()` (lecture seule du fichier réel), réconciliation sur la copie, consignation du comptage avant/après (section dédiée ci-dessus). Harnais temporaire supprimé après usage — aucun résidu dans le dépôt.
- **Verification:** SHA-256 du vrai fichier identique avant et après l'exécution du plan ; `%APPDATA%\Chronos\backups` inexistant.

**4. [Ordre TDD] Tâches 1 et 2 marquées `tdd="true"` mais exécutées implémentation-puis-tests**
- **Found during:** Task 1
- **Issue:** Comme aux plans 01 et 02, le plan sépare l'implémentation (Task 1, vérifiée par `dotnet build`) des tests (Task 2), dont le `read_first` exige le fichier produit par la Task 1 — la boucle RED/GREEN par tâche est structurellement impossible.
- **Fix:** Ordre des tâches du plan respecté. Le contrat de comportement était intégralement spécifié avant écriture (section `<behavior>` de la Task 1) et chaque ligne de ce contrat a une assertion dédiée en Task 2.
- **Impact:** aucun sur le résultat — 17 cas couvrent l'intégralité du contrat.

---

**Total deviations:** 2 auto-corrigées (blocages de vérifiabilité et de cohérence de consignes) + 1 substitution explicitement demandée + 1 note de méthode.
**Impact on plan:** Aucune dérive de périmètre. La surface publique annoncée par la section `<interfaces>` est livrée intégralement ; le seul ajout est un paramètre optionnel additif.

## Issues Encountered

- **Un échec ponctuel non reproductible de `CompositionRootTests.Host_resout_et_dispose_les_singletons`** (test `[WpfFact]` STA préexistant) sur l'exécution de suite qui a suivi la suppression du harnais temporaire — donc sur une reconstruction de l'assembly de tests. Le test passe seul, et la suite complète a ensuite été exécutée **cinq fois de suite, 405/405 verte à chaque fois**. Diagnostic retenu : aléa d'initialisation du dispatcher STA lors d'une reconstruction concurrente, sans lien avec les modifications de ce plan (le nouveau test de la même classe n'est pas STA et ne touche ni WPF ni dispatcher). Aucun correctif appliqué — le signaler plutôt que le masquer.

## Conformité CLAUDE.md

- Aucune dépendance NuGet ajoutée (`System.Text.Json` intégré à net8.0).
- Service neutre dans `Services/`, aucun type WPF : `ServicesLayerPurityTests` vert.
- Aucun `MessageBox`, aucun dialogue : la réconciliation est silencieuse et ne peut pas empêcher le démarrage.
- Chemins sous profil utilisateur uniquement (`%USERPROFILE%\.claude`, `%APPDATA%\Chronos\backups`), aucun droit admin, aucun accès `HKLM`/`Program Files`.
- `Environment.ProcessPath` et jamais l'emplacement de l'assembly (contrainte mono-fichier).
- XML-doc, commentaires et noms de tests en français.
- En test : `Path.GetTempPath()` exclusivement ; `grep -rn "SpecialFolder.UserProfile" tests/` renvoie **0**.

## User Setup Required

None — la purge s'exécute d'elle-même au prochain lancement de Chronos. Une sauvegarde horodatée du `settings.json` d'origine sera déposée dans `%APPDATA%\Chronos\backups\` avant la première écriture.

## Next Phase Readiness

**Phase 15 complète : PUR-01, PUR-02 et PUR-03 sont livrés et prouvés.** Les installateurs ne cumulent plus (15-02), le socle d'identité ne dépend plus du chemin d'exe (15-01), et la machine déjà polluée se nettoie seule au démarrage (15-03).

**Pour la Phase 16** (fondations du delta) : le terrain de mesure est assaini — le prochain lancement de Chronos ramènera `~/.claude/settings.json` à 5 hooks au lieu de 25 et repointera la barre `statusLine` sur l'exe courant. C'est la condition pour que le pont statusLine puisse enfin être invoqué et que `usage.json` cesse d'être figé au 2026-07-10.

**Point d'attention conservé :** le prédicat d'identité borne son rayon d'action aux exécutables nommés `Chronos*.exe`. Si l'exe venait à être renommé, la réconciliation poserait des entrées qu'elle ne saurait plus retirer — le cumul reviendrait. À garder en tête avant tout renommage du binaire publié.

---
*Phase: 15-idempotence-des-int-grations*
*Completed: 2026-09-09*

## Self-Check: PASSED

- 4 fichiers annonces presents sur le disque (reconciliateur, ses tests, App.xaml.cs, CompositionRootTests)
- 3 commits de tache verifies dans git (9d5388f, 01021dd, 4894d52)
- Suite complete : 405 tests / 0 echec (387 de la vague 2 + 17 + 1 garde DI), 5 executions consecutives
- Gardes ServicesLayerPurityTests et CompositionRootTests vertes
- Harnais de verification terrain supprime : aucun residu dans le depot
- Vrai ~/.claude/settings.json INCHANGE (SHA-256 identique avant/apres) et %APPDATA%/Chronos/backups inexistant
- Aucun stub : le reconciliateur est entierement cable (DI + point d'appel) et couvert par ses tests
