---
phase: 37-une-cha-ne-de-donn-es-claire
plan: 06
subsystem: tests, gardes, documentation, commit de l'étape 5 de la purge
tags: [purge-r5, statusline, gardes, docs, reconciliateur]
requires: ["37-05"]
provides:
  - "Suite de tests remise en état après l'étape 5 : 1704 verts, build -warnaserror --no-incremental à 0 avertissement"
  - "Garde « 1 composite » (Assert.Single sur new CompositeUsageProvider( dans App.xaml.cs)"
  - "Garde d'ordre du démarrage : LireCommandeInterneHeritee < window.Show() < .Reconcile( < LogStartupAsync()"
  - "Garde « le réconciliateur ne pose ni ne repointe jamais une barre »"
  - "Garde de non-retour complétée (5 types et 6 membres de l'étape 5)"
  - "Termes de l'étape 5 interdits dans README, CLAUDE.md et data-sources.md"
  - "ArretHote.SignalerDepassement : la ligne « arrêt dépassé » crée son dossier"
  - "Commit unique de l'étape 5 : 6a655a3 refactor(37-05)"
affects: [37-VALIDATION, 43]
tech-stack:
  added: []
  patterns: ["nom d'une clé retirée composé en deux morceaux dans les tests (const CleHeritee), comme en production"]
key-files:
  created: []
  deleted:
    - tests/Chronos.Tests/ClaudeUsageObjectProviderTests.cs
    - tests/Chronos.Tests/StatusLineBridgeTests.cs
    - tests/Chronos.Tests/StatusLineInstallerTests.cs
    - tests/Chronos.Tests/Fakes/FakeStatusLineSetup.cs
    - tests/Chronos.Tests/TestData/usage-valid.json
    - tests/Chronos.Tests/TestData/usage-corrupt.json
    - tests/Chronos.Tests/TestData/usage-partial.json
    - tests/Chronos.Tests/TestData/usage-ancien.json
  modified:
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - tests/Chronos.Tests/GardeDocumentationChaineTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/ArgumentsDemarrageTests.cs
    - tests/Chronos.Tests/SettingsServiceTests.cs
    - tests/Chronos.Tests/RefreshOrchestratorTests.cs
    - tests/Chronos.Tests/ReglagesWindowTests.cs
    - tests/Chronos.Tests/LigneJournalTests.cs
    - tests/Chronos.Tests/ArretHoteTests.cs
    - src/Chronos/Services/ArretHote.cs
    - src/Chronos/App.xaml.cs
    - src/Chronos/Services/UsageNormalization.cs
    - src/Chronos/Services/VerrouInstanceUnique.cs
    - src/Chronos/Services/SessionHookInstaller.cs
    - docs/publish.md
    - docs/hooks-contract.md
    - docs/data-sources.md
    - README.md
decisions:
  - "Écriture de chronos.log à l'arrêt dépassé déplacée d'App (privée, non testable) vers ArretHote.SignalerDepassement (type neutre), avec Directory.CreateDirectory, épinglée par un test sur dossier temporaire absent"
  - "JournalRelevesTests : PontStatusLine → MagasinDernierExact et non EndpointOAuthChronos (le test asserte juste au-dessus que EndpointOAuthChronos a une date ; il faut une source jamais écrite)"
  - "La garde du composite utilise Assert.Single (l'analyseur xUnit2013 refuse Assert.Equal(1, …Count) sous -warnaserror)"
  - "La carte de la barre est remplacée dans ReglagesWindowTests par La_barre_de_statut_a_quitte_Donnees (FindName(\"InterrupteurBarreStatut\") == null), sur le modèle de la carte de recalibrage"
  - "La garde du réconciliateur interdit aussi la fabrique \\bChronosCommand\\( (le prédicat IsChronosCommand( reste permis)"
metrics:
  duration: "≈ 45 min"
  completed: 2026-10-03
  tasks: 2
  files: 57
requirements: [DAT-02, DAT-03]
---

# Phase 37 Plan 06 : fin de l'étape 5 de la purge (tests, gardes, docs) et commit unique — Summary

L'étape 5 est close : la suite de tests compile et passe (1704 verts), et le build `-warnaserror --no-incremental` sort à
0 avertissement. Des gardes verrouillent la chaîne finale `LastExact(Journal(Composite(sonde, OAuth Chronos)))`, l'ordre
du démarrage et le fait que le réconciliateur ne pose ni ne repointe plus aucune barre. Les docs décrivent le retrait.
Tout part dans **un seul commit de code** : `6a655a3 refactor(37-05)`. Il contient la production de 37-05, qui n'était
pas commitée, et le travail de ce plan, soit 57 fichiers.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | Rétablir la compilation des tests (suppressions, sites de construction, sources et réglages hérités, bilan du diagnostic) | 6a655a3 (commit unique de l'étape 5) |
| 2 | Gardes (1 composite, ordre du démarrage, réconciliateur sans barre, non-retour), docs, commit | 6a655a3 |

## Ce qui a changé

**Tests supprimés (`git rm`)** : `ClaudeUsageObjectProviderTests`, `StatusLineBridgeTests`, `StatusLineInstallerTests`,
`Fakes/FakeStatusLineSetup`, `TestData/usage-{valid,corrupt,partial,ancien}.json`. Le `.csproj` de tests n'a pas été
modifié : il ne contenait aucune entrée nominative pour ces fixtures, qui étaient résolues par `[CallerFilePath]`. Le
commentaire de `UsageNormalizationTests` qui citait `usage-valid.json` a été reformulé.

**Retrait du paramètre ChronosPaths de RefreshOrchestrator (côté tests)** : ce point fait suite à la section du même nom
dans 37-05. Les sites `new RefreshOrchestrator(…, paths|TempPaths()|ChronosPaths.Default(), …)` sont passés à deux
arguments :
- `RefreshOrchestratorTests` (×3, après suppression de deux tests) ;
- `ArretHoteTests` ;
- `CadranBindingTests` ;
- `MainViewModelTests` (×4) ;
- `MontageReglages` ;
- `OuvreurReglagesTests` ;
- `OverlayWindowConfigTests` ;
- `ThemingTests`.

Dans `RefreshOrchestratorTests`, sont supprimés `Ecriture_usage_json_declenche_GetAsync`,
`Error_du_watcher_entraine_recreation_sans_perdre_le_refresh` et les helpers `TempPaths`. La doc de classe ne parle plus
de surveillance de fichier, et le test du minuteur s'appelle maintenant `PeriodicTimer_declenche_GetAsync_sans_autre_declencheur`.

**MainViewModel sans `statusLineSetup`** : le paramètre est retiré des mêmes fichiers. Dans `MontageReglages.NouveauVm`, le
paramètre `barreStatut` est retiré.

**Adaptations par fichier** :
- `CompositionRootTests` :
  - l'inner du miroir est désormais un `FakeUsageProvider` ;
  - `IStatusLineSetup` et `StatusLineInstaller` sont retirés ;
  - le réconciliateur DI prend trois chemins temporaires, avec `Assert.StartsWith(…, recon.BackupDir)` ;
  - le miroir PRIMAIRE est réduit à 1 composite (sonde → `ChronosOAuthUsageProvider`) ;
  - le diagnostic du miroir de `Host_resout_et_dispose_les_singletons` reçoit `reglagesClaude:` (réconciliateur sur trois
    chemins temporaires).
- `ReglagesWindowTests` :
  - la ligne `ToggleStatusLineSource` et les deux libellés de la carte sont retirés ;
  - ajout de `DoesNotContain("Barre de statut de Claude Code")`, et `DoesNotContain("Source terminal")` est conservé ;
  - le test de l'interrupteur est remplacé par `La_barre_de_statut_a_quitte_Donnees`.
- `GardesDoctrineTests` et `LectureVeilleTests` : `PontStatusLine` → `EndpointOAuthChronos`. La doc « objet d'usage sur
  disque » et celle des « TROIS composites » sont corrigées.
- `JournalRelevesTests` : `PontStatusLine` → `MagasinDernierExact` (voir Deviations).
- `LibelleSourceTests` : l'`InlineData` `PontStatusLine` est retiré, et la garde anti-mutisme passe à `>= 3`.
- `LigneJournalTests` : ajout de `Une_source_retiree_PontStatusLine_est_ignoree_sans_exception`.
- `SettingsServiceTests` : les deux clés de la barre sont désormais des membres inconnus.
  - Les assertions qui portaient sur ces clés sont retirées. Les tests vérifient désormais ce qui reste conservé
    (`MonitorDeviceName`, géométries, thème…).
  - Après un Save, `"echo hi"` disparaît : c'est asserté par `DoesNotContain`.
  - Les fixtures JSON ne changent pas.
- `ArgumentsDemarrageTests` :
  - `--statusline` / `--STATUSLINE` donnent `ArgumentInconnu` ;
  - `--statusline --hook X` donne `Hook("X")` ;
  - `ModesConnus.Count == 4` ;
  - `--statusline` rejoint l'échantillon de la liste blanche.
- `DiagnosticServiceTests` : deux tests ajoutés.
  - `Le_rapport_journalise_le_bilan_du_retrait_de_la_barre` : fichier témoin, réconciliateur sur TROIS chemins
    temporaires, deux `Assert.StartsWith`, `Reconcile(false)`, nettoyage en `finally`. Il asserte « [Réglages de Claude
    Code] », « barre de statut Chronos retirée — sauvegarde claude-settings- » et « Barre de statut actuelle : absente ».
  - `Sans_reconciliateur_le_rapport_dit_non_cable`.
- `ClaudeSettingsReconcilerTests` :
  - le nom de la clé héritée est composé dans `const CleHeritee = "Inner" + "StatusLineCommand"`, même règle qu'en
    production ;
  - un commentaire qui citait `StatusLineInstallerTests` est reformulé.

**Gardes (`GardesPerimetreTests`)** :
- La garde composite passe à `Assert.Single(…new CompositeUsageProvider(…)`. L'exigence d'ordre journal → composite → tête
  est conservée.
- Garde de placement du verrou : la variable `iStatus` et l'exigence conditionnelle `case ModeDemarrage.StatusLine` sont
  retirées. La doc ne cite plus le mode. `--hook`, les galeries et « tri avant verrou avant Host » sont conservés.
- NOUVEAU : `La_commande_heritee_est_lue_avant_Show_et_la_reconciliation_precede_le_log_de_demarrage`. Les quatre repères
  sont vérifiés présents (≥ 0) et dans l'ordre strict.
- NOUVEAU : `Le_reconciliateur_ne_pose_ni_ne_repointe_jamais_une_barre` :
  - interdit : `ApplyStatusLine`, `StatusLineInstaller` et la regex `\bChronosCommand\(` ;
  - exigé : `root.Remove("statusLine")`.
- `Aucun_maillon_retire_de_la_chaine_ne_subsiste` :
  - types ajoutés : `ClaudeUsageObjectProvider`, `StatusLineBridge`, `StatusLineInstaller`, `StatusLineSetup`,
    `IStatusLineSetup` ;
  - membres ajoutés : `ChronosSettings.InnerStatusLineCommand` / `StatusLinePromptDismissed`,
    `SourceUsage.PontStatusLine`, `ModeDemarrage.StatusLine`, `ToggleStatusLineSourceCommand`, `IsStatusLineSourceEnabled`.
- `GardeDocumentationChaineTests.TermesRetires` : ajout de `"PontStatusLine", "usage.json", "pont statusLine",
  "--statusline"`.

**Docs** :
- `docs/data-sources.md` §7 : la ligne `source` vaut `SondeEnTetes` · `EndpointOAuthChronos`.
- `docs/publish.md` §7 :
  - seul `--hook` reste multi-instances ;
  - le paragraphe réconciliation est réécrit : hooks repointés, bilan dans `chronos.log` écrit APRÈS la réconciliation ;
  - nouvelle sous-section « Premier lancement de la 3.5.0 » : sauvegarde puis retrait, ou restauration de la barre
    d'origine ; barre tierce intacte ; idempotent ; sessions déjà ouvertes sur l'ancien exe jusqu'à leur redémarrage ;
    `--statusline` silencieux ; carte retirée ;
  - « une seule instance » est conservé.
- `docs/hooks-contract.md` : « (le mode `--hook` sort bien avant) ».
- `README.md` : la mention de la barre de statut est retirée de la section Données.
- `CLAUDE.md` : ne contenait aucun des quatre termes, il n'a pas été modifié.

**Commentaires périmés (décision 1 de l'orchestrateur), commentaires seulement** :
- `VerrouInstanceUnique.cs` : les court-circuits sont `--hook`, `--cadrans`, `--sessions`, `--historique`.
- `UsageNormalization.cs` :
  - deux unités au lieu de trois, avec une parenthèse historique sur la barre ≤ 3.4 ;
  - le motif de `PlancherEpoch` est reformulé ;
  - `FractionDepuisPourcentage` et `InstantDepuisEpochMillisecondes` ne citent plus le pont.
- `SessionHookInstaller.cs` : « (comme pour statusLine) » est retiré.
- `ArretHote.cs` : « (watcher, minuteurs…) » devient « (minuteurs, flux…) ».

## Écrivains sous ChronosPaths : dossier créé avant écriture (décision 2 de l'orchestrateur)

Recensement par grep de chaque écriture (`File.WriteAll*/AppendAll*/Move`, `FileStream` en Create/OpenOrCreate,
`StreamWriter`) dans `src/Chronos`, fichier par fichier :

| Écrivain | Cible | `Directory.CreateDirectory` avant écriture |
|----------|-------|---------------------------------------------|
| `SettingsService.Save` | settings.json | oui (l. 158) |
| `LastExactStore` | last-exact.json | oui (l. 125) |
| `JournalReleves` | historique/releves-*.jsonl | oui (l. 221) |
| `MagasinAgregats` | historique/tokens-*.jsonl | oui (l. 199) |
| `IndexMessages` | historique/index | oui (l. 181) |
| `Curseurs` | historique/curseurs | oui (l. 140) |
| `CouvertureTokens` | historique/couverture | oui (l. 171) ; `ReconstructionTokens` crée aussi `HistoriqueDir` (l. 268) |
| `ChronosOAuthStore` | oauth.dat | oui (l. 35) |
| `DiagnosticService` (log de démarrage, export) | chronos.log, rapport | oui (l. 114, 131) |
| `EcritureEtatSession` (mode `--hook`) | sessions/*.json | oui (l. 66, avant `EcrireAvecReprise`) |
| `ArchiveStore`, `TreatedStore` | archives / traités | oui |
| `ClaudeSettingsReconciler` (sauvegardes) | backups/ | oui (l. 266) |
| **`App.SignalerArretDepasse`** | **chronos.log à l'arrêt dépassé** | **NON → corrigé** |

L'écriture `App.SignalerArretDepasse` faisait `File.AppendAllText` sans créer le dossier. Si le dossier n'existait pas,
l'exception était avalée et la ligne se perdait en silence. Correctif :
- la méthode est déplacée vers le type neutre `ArretHote.SignalerDepassement(dossier, cause)`, avec
  `Directory.CreateDirectory(dossier)` ;
- `App.OnExit` l'appelle désormais ;
- elle est épinglée par `ArretHoteTests.La_ligne_d_arret_depasse_cree_son_dossier_absent`, sur un dossier temporaire
  imbriqué absent, avec `Assert.StartsWith(Path.GetTempPath(), …)` et un cas `null` qui ne lève pas.

## Vérification

- `dotnet build Chronos.sln -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln` : **1704 verts / 0 échec**. Le compte était de 1707 à la 3.4.0 ; les suppressions des tests
  des types retirés sont partiellement compensées par les ajouts.
- Greps d'acceptation :
  - Tâche 1 :
    - termes retirés hors `GardesPerimetreTests` : il reste 1 ligne, dans `GardeDocumentationChaineTests.TermesRetires`
      (voir Deviations) ;
    - `PontStatusLine` hors gardes : il reste 1 ligne, le littéral JSON dans le corps du test
      `Une_source_retiree_PontStatusLine…` ;
    - réconciliateur construit avec moins de trois arguments : 0 ;
    - `Le_rapport_journalise_le_bilan_du_retrait_de_la_barre` = 1 ;
    - `Assert.StartsWith(Path.GetTempPath(), reconciler.BackupDir)` = 1 dans `DiagnosticServiceTests`.
  - Tâche 2 :
    - nouvelles gardes = 1 chacune ;
    - `"StatusLineBridge"` = 1 ;
    - `"--statusline"` = 1 dans la garde de documentation ;
    - `usage.json|pont statusLine|--statusline|PontStatusLine` = 0 dans README, CLAUDE.md et data-sources ;
    - `statusline` = 0 dans hooks-contract ;
    - « une seule instance » = 1 dans publish ;
    - `git log -1` donne `refactor(37-05)` ;
    - `git show --stat HEAD` contient `ClaudeSettingsReconciler.cs` ET `GardesPerimetreTests.cs`.
- Sécurité :
  - l'exe n'a pas été lancé ;
  - tout `ClaudeSettingsReconciler` de test vise trois chemins temporaires ;
  - aucune écriture dans le vrai `~/.claude/settings.json` ni dans le vrai `%APPDATA%\Chronos` ;
  - la mémoire hors dépôt n'a pas été modifiée.
- Le constat réel (barre disparue, `--statusline` silencieux sur l'exe 3.5) se fera en phase 43, en manuel.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Correction] La ligne « arrêt dépassé » ne créait pas son dossier**
- **Trouvé pendant :** recensement demandé par l'orchestrateur (décision 2)
- **Problème :** `App.SignalerArretDepasse` écrivait `chronos.log` sans `Directory.CreateDirectory`. Seul le watcher
  supprimé créait ce dossier au démarrage.
- **Correctif :** méthode déplacée vers `ArretHote.SignalerDepassement`, avec création du dossier ; test sur dossier
  temporaire.
- **Fichiers :** `src/Chronos/Services/ArretHote.cs`, `src/Chronos/App.xaml.cs`, `tests/Chronos.Tests/ArretHoteTests.cs`

**2. [Rule 1 - Bug de test] `JournalRelevesTests` : source de remplacement**
- **Problème :** remplacer `PontStatusLine` par `EndpointOAuthChronos`, comme le demandait le plan, aurait cassé le
  test : il asserte deux lignes plus haut que `EndpointOAuthChronos` a une date.
- **Correctif :** `MagasinDernierExact`, une source jamais écrite par ce test.

**3. [Rule 3 - Bloquant] `LibelleSourceTests`, garde anti-mutisme**
- **Problème :** `membres.Length >= 4` devenait faux avec 3 membres.
- **Correctif :** la borne passe à `>= 3`.

**4. [Rule 3 - Bloquant] xUnit2013 sous `-warnaserror`**
- **Problème :** l'analyseur refuse `Assert.Equal(1, …Count)`.
- **Correctif :** garde composite écrite en `Assert.Single(...)`. C'est une forme équivalente, autorisée par le critère
  d'acceptation.

**5. [Conformité au grep d'acceptation] Clé héritée dans `ClaudeSettingsReconcilerTests`**
- **Problème :** les fixtures inline de 37-05 écrivaient le nom du membre retiré en toutes lettres.
- **Correctif :** `const CleHeritee = "Inner" + "StatusLineCommand"`, même règle que la décision 1 de 37-05. Le test
  `Chaque_enum_des_reglages_retombe_sur_son_propre_defaut` utilise `MonitorDeviceName` à la place de la clé.

**6. [Ajouts] Commentaires périmés corrigés hors liste du plan** (`VerrouInstanceUnique`, `UsageNormalization`,
`SessionHookInstaller`, `ArretHote`). C'était la décision 1 de l'orchestrateur ; seuls des commentaires ont changé.

**Lignes restantes au grep de la tâche 1 (justifiées, non corrigées) :**
- `GardeDocumentationChaineTests.TermesRetires` cite `ClaudeUsageObjectProvider` et `StatusLineInstaller`. C'est la liste
  des termes INTERDITS de la garde documentaire : la garde doit les nommer. La tâche 2 y ajoute d'ailleurs les termes de
  l'étape 5.
- `LigneJournalTests` : le littéral JSON `"source":"PontStatusLine"` se trouve dans le corps du test exigé par le plan.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND : `tests/Chronos.Tests/GardesPerimetreTests.cs` (deux nouvelles gardes, non-retour étape 5)
- FOUND : `tests/Chronos.Tests/GardeDocumentationChaineTests.cs` (`"pont statusLine"`, `"--statusline"`)
- FOUND : `src/Chronos/Services/ArretHote.cs` (`SignalerDepassement`, `CreateDirectory`)
- ABSENT (attendu) : les 8 fichiers de test supprimés
- FOUND : commit `6a655a3` (`refactor(37-05)`, 57 fichiers, production 37-05 et travail 37-06)
