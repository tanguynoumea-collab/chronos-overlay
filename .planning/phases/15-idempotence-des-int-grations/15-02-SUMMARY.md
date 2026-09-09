---
phase: 15-idempotence-des-int-grations
plan: 02
subsystem: infra
tags: [system-text-json, jsonnode, settings-json, claude-code, idempotence, hooks, statusline, xunit]

# Dependency graph
requires:
  - "15-01 : ClaudeSettingsJson (IsChronosCommand, PointsToExe, CommandOf, IsChronosGroup, ParseOrNull, Serialize)"
provides:
  - "SessionHookInstaller.ApplyHooks(root, exePath, wanted) : cœur de mutation retirer-puis-ajouter, réutilisable par le réconciliateur"
  - "StatusLineInstaller.ApplyStatusLine(root, exePath) : repointage ciblé de la barre, sans jamais créer l'entrée"
  - "Quatre cœurs purs en string? : null = NE RIEN ÉCRIRE sur un settings.json inexploitable"
  - "IsInstalled(exePath) durci des deux côtés : appartenance ET fraîcheur"
affects: [15-03 ClaudeSettingsReconciler]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Retirer-puis-ajouter inconditionnel : idempotent par construction, ne dépend d'aucun prédicat de présence exact"
    - "RemoveAt en parcours descendant : mutation en place, aucun reparentage de JsonNode, ordre des survivants préservé"
    - "Mutation ciblée de la valeur (sl[\"command\"]) plutôt que reconstruction de l'objet parent"
    - "Purge LARGE à la désinstallation (toutes les versions), là où l'installation ne pose que l'exe courant"

key-files:
  created: []
  modified:
    - src/Chronos/Services/SessionHookInstaller.cs
    - src/Chronos/Services/StatusLineInstaller.cs
    - src/Chronos/Views/SessionsController.cs
    - src/Chronos/Views/StatusLineSetup.cs
    - tests/Chronos.Tests/SessionsTests.cs
    - tests/Chronos.Tests/StatusLineInstallerTests.cs

key-decisions:
  - "L'installation ne cherche plus si un groupe Chronos est présent : elle retire TOUS les groupes Chronos puis ajoute le sien. Un remplacement inconditionnel est idempotent par construction — même si le prédicat d'identité ratait une variante exotique, on laisserait au pire une orpheline, on n'en créerait plus une de plus"
  - "TransformForUninstall et Uninstall perdent leur paramètre exePath des deux côtés : le retrait est volontairement LARGE, sinon désactiver depuis une nouvelle version laisserait derrière lui les hooks et la barre de toutes les anciennes"
  - "statusLine est muté sur la seule clé command : reconstruire l'objet effaçait padding (champ officiel optionnel) et effacerait toute clé future de Claude Code"
  - "ApplyStatusLine repointe mais n'installe JAMAIS : le consentement reste porté par le menu / StatusLinePromptDismissed, jamais par une réconciliation silencieuse"
  - "ChronosCommand aligné sur les slashes avant, comme HookCommand (Open Question 7 du RESEARCH tranchée oui) : les deux intégrations écrivent désormais la même forme de chemin"
  - "IsInstalled des deux installateurs exige appartenance ET fraîcheur : c'est ce qui débloque OfferOnFirstRun, qui sortait immédiatement parce que la barre d'un exe périmé répondait « oui »"

patterns-established:
  - "Cœur de mutation public (ApplyHooks / ApplyStatusLine) séparé du cœur de transformation string→string : le réconciliateur du plan 03 compose les deux sur un seul arbre, sans double sérialisation"

requirements-completed: [PUR-01, PUR-02]

# Metrics
duration: 7min
completed: 2026-09-09
---

# Phase 15 Plan 02: Installateurs non cumulatifs Summary

**Les deux installateurs cessent de cumuler : `SessionHookInstaller` retire tous les groupes Chronos avant d'ajouter le sien (quel que soit le chemin d'exe), `StatusLineInstaller` mute la seule clé `command` au lieu de reconstruire l'objet — et un `settings.json` inexploitable ne provoque plus aucune écriture. Prouvé par 21 tests nouveaux, 387 au total, 0 échec.**

## Performance

- **Duration:** ~7 min
- **Started:** 2026-09-09T10:35:53Z
- **Completed:** 2026-09-09T10:42:05Z
- **Tasks:** 3/3
- **Files modified:** 0 créés, 6 modifiés

## Accomplishments

- **La cause racine du cumul est supprimée du code de production.** `IsOurEntry`, qui comparait au chemin de l'exe COURANT, n'existe plus. L'installation ne consulte plus aucun garde de présence : elle exécute `ApplyHooks` — retrait descendant de tous les groupes Chronos par `ClaudeSettingsJson.IsChronosGroup`, puis ajout du groupe courant. Prouvé : chaîner `Chronos-v2.5.exe` → `v2.6` → `v2.8.1` laisse **exactement 1 groupe par événement**, pointant sur le dernier exe (critère de succès 1 de la ROADMAP).
- **Les groupes des autres outils survivent intégralement.** Un groupe GSD `{"matcher":"Write|Edit","hooks":[{...,"timeout":5}]}` ressort avec son `matcher`, son `timeout`, sa commande et sa **position en tête** — le groupe Chronos est ajouté après. La mutation en place (`RemoveAt` descendant) ne reparente aucun `JsonNode` et ne clone rien.
- **`statusLine` est repointée par mutation ciblée.** Une barre `Chronos-v2.8.1.exe --statusline` avec `padding: 2` devient une barre de l'exe courant, `padding` **toujours à 2**, clé inconnue `futur` intacte, `capturedInner` à `null` (une barre Chronos n'est jamais chaînée sur elle-même). La reconstruction `root["statusLine"] = new JsonObject{...}` ne subsiste que dans la seule branche « clé totalement absente ».
- **Une barre tierce n'est jamais capturée.** `ApplyStatusLine` sur `node autre.js` laisse le root strictement inchangé, et sur un fichier sans clé `statusLine` il n'en crée pas — le consentement d'installation reste porté par le menu, pas par la réconciliation.
- **Le mode de défaillance destructif est éteint jusqu'aux E/S.** Les quatre cœurs purs renvoient `null` sur les quatre formes de contenu inexploitable (clés dupliquées, racine tableau, racine scalaire, JSON invalide), et `Install` / `Uninstall` sortent sans écrire quand ils reçoivent `null`. `grep -c "return new JsonObject()"` = 0 dans les deux installateurs.
- **`IsInstalled` distingue enfin « à Chronos » de « à CE Chronos ».** Un `settings.json` (temp) dont la barre pointe `Chronos-v2.8.1.exe` répond `false` pour l'exe courant et `true` pour ce chemin-là. C'est le déblocage qui rendra `OfferOnFirstRun` de nouveau atteignable.
- **Le retrait est LARGE des deux côtés.** `TransformForUninstall` perd son paramètre `exePath` : après trois installations depuis trois chemins, la sortie ne contient plus une seule occurrence de `--hook`, la clé d'événement devenue vide est retirée, et le groupe GSD subsiste.
- **Suite complète : 387 tests, 0 échec** (366 de la vague 1 + 21 nouveaux). `ServicesLayerPurityTests` et `CompositionRootTests` verts. Build : 0 erreur, seulement les 2 avertissements préexistants de `DesktopUiaSessionSourceTests` (hors périmètre).

## Task Commits

1. **Task 1: SessionHookInstaller — retirer-puis-ajouter inconditionnel, prédicat large, abandon sur JSON inexploitable** — `a79bb66` (fix)
2. **Task 2: StatusLineInstaller — muter `command` au lieu de reconstruire, séparer appartenance et fraîcheur** — `0535d78` (fix)
3. **Task 3: Étendre les tests — non-cumul multi-chemins, survie des tiers, préservation de padding** — `c1191d4` (test)

## Files Created/Modified

- `src/Chronos/Services/SessionHookInstaller.cs` — `IsOurEntry`, `Parse` et le champ `Indented` supprimés ; `ApplyHooks(root, exePath, wanted)` ajouté (cœur de mutation public, réutilisé par le plan 03) ; `TransformForInstall` / `TransformForUninstall` en `string?` ; `TransformForUninstall` sans `exePath` ; `Install` / `Uninstall` n'écrivent plus sur `null` ; `IsInstalled` durci ; XML-doc de classe reformulée (« REMPLACE inconditionnellement » au lieu de « NON DESTRUCTIF »).
- `src/Chronos/Services/StatusLineInstaller.cs` — `IsChronosCommand`, `ParseObject`, `ReadCommand` et `Indented` supprimés ; `ApplyStatusLine(root, exePath)` ajouté ; `TransformForInstall` en mutation ciblée ; `TransformForUninstall` à prédicat large sans `exePath` ; `ChronosCommand` en slashes avant ; `IsInstalled` = appartenance ET fraîcheur, avec la note sur l'ordre `réconciliateur → OfferOnFirstRun` dans `App.OnStartup`.
- `src/Chronos/Views/SessionsController.cs` — `_installer.Uninstall(ExePath)` → `_installer.Uninstall()`.
- `src/Chronos/Views/StatusLineSetup.cs` — `_installer.Uninstall(ExePath, s.InnerStatusLineCommand)` → `_installer.Uninstall(s.InnerStatusLineCommand)`.
- `tests/Chronos.Tests/SessionsTests.cs` — 3 tests existants adaptés aux nouvelles signatures ; 7 tests ajoutés (non-cumul 3 chemins, groupe tiers avec matcher/timeout/position, handler `http` sans `command`, théorie 4 JSON inexploitables, purge large, clé `hooks` non créée, fraîcheur d'`IsInstalled`).
- `tests/Chronos.Tests/StatusLineInstallerTests.cs` — 3 tests existants adaptés ; 8 tests ajoutés (repointage sans duplication, `padding` + clé inconnue, point fixe, `ApplyStatusLine` inoffensif, théorie 4 JSON inexploitables, retrait d'une autre version, slashes avant, fraîcheur d'`IsInstalled`).

## Decisions Made

- **Remplacement inconditionnel plutôt que garde de présence.** C'est le cœur du correctif : la robustesse ne vient plus de l'exactitude du prédicat mais de la forme de l'algorithme. Au pire une entrée exotique reste orpheline ; on n'en empile plus jamais une de plus.
- **`exePath` retiré des deux `Uninstall` / `TransformForUninstall`.** Conserver le paramètre aurait recréé la faille du côté du retrait : désactiver depuis la v3 aurait laissé les hooks de la v2.5 à la v2.8.1.
- **`ApplyStatusLine` ne crée jamais l'entrée.** Il aurait été facile de faire « si absent, installer » ; ce serait installer sans consentement au premier démarrage du réconciliateur. Le repointage et l'installation restent deux gestes distincts.
- **`padding` prouvé par test, pas seulement documenté.** Deux tests posent `padding: 2` en entrée et l'exigent en sortie — c'est le seul filet contre une future tentation de reconstruire l'objet.
- **Chemins de tests injectés depuis `Path.GetTempPath()`.** Les deux tests qui construisent un installateur passent un fichier temporaire ; `grep -r "SpecialFolder.UserProfile" tests/` renvoie 0. Le vrai `~/.claude/settings.json` reste hors d'atteinte de la suite.

## Deviations from Plan

### Ajustements

**1. [Rule 3 - Blocking] Tests existants adaptés dans les tâches 1 et 2, pas seulement dans la tâche 3**
- **Found during:** Task 1
- **Issue:** Les vérifications automatisées des tâches 1 et 2 sont des `dotnet test --filter`, or les signatures changées (`string?`, perte de `exePath`) cassaient la compilation du projet de tests. Sans adaptation immédiate, aucune des deux tâches n'était vérifiable.
- **Fix:** Adaptation minimale des 3 + 3 tests existants aux nouvelles signatures dans les commits des tâches 1 et 2 (déréférencement `!`, arguments retirés, helpers `Root`/`Cmd` en `string?`). La tâche 3 conserve l'intégralité de la **nouvelle** couverture prévue par le plan.
- **Files modified:** `tests/Chronos.Tests/SessionsTests.cs`, `tests/Chronos.Tests/StatusLineInstallerTests.cs`
- **Verification:** `dotnet build Chronos.sln` → 0 erreur ; filtres `SessionsTests` (34) et `StatusLineInstallerTests` (6) verts à leur tâche respective.
- **Committed in:** `a79bb66` et `0535d78`

**2. [Note de vérification] La commande de vérification 5 du plan contredit un critère d'acceptation du plan 01**
- **Found during:** vérification finale
- **Issue:** Le plan 02 exige `grep -rn "IsOurEntry\|return new JsonObject()" src/Chronos/Services/` → aucune occurrence. Or le plan 01 exige **exactement une** occurrence de `return new JsonObject()` dans `ClaudeSettingsJson.ParseOrNull` — le cas légitime « fichier absent ou vide = départ à blanc », qui n'écrase rien puisqu'il n'y a rien à écraser.
- **Résolution:** L'intention de la vérification porte sur les **installateurs** : les deux répondent 0, tout comme `IsOurEntry` sur l'ensemble de `Services/`. L'unique occurrence restante est celle du socle, voulue et testée. Aucune action.
- **Verification:** `grep -c "return new JsonObject()"` = 0 dans `SessionHookInstaller.cs` et `StatusLineInstaller.cs` ; 1 dans `ClaudeSettingsJson.cs` (ligne 135, conforme au plan 01).

**3. [Ordre TDD] Tâches marquées `tdd="true"` mais exécutées implémentation-puis-tests**
- **Found during:** Task 1
- **Issue:** Comme au plan 01, la structure du plan sépare l'implémentation (tâches 1 et 2) des nouveaux tests (tâche 3), et le `read_first` de la tâche 3 exige les fichiers produits par les tâches 1 et 2 — la boucle RED/GREEN par tâche était structurellement impossible.
- **Fix:** Suivi de l'ordre des tâches du plan. Le contrat de comportement était intégralement spécifié avant écriture (sections `<behavior>` des tâches 1 et 2), et chaque ligne de ces contrats a une assertion dédiée en tâche 3.
- **Impact:** aucun sur le résultat — 21 tests nouveaux couvrent l'intégralité des deux contrats.

---

**Total deviations:** 1 auto-corrigée (blocage de compilation des tests) + 2 notes de méthode/vérification.
**Impact on plan:** Aucune dérive de périmètre. Toutes les signatures publiques annoncées par la section `<interfaces>` du plan sont livrées telles quelles.

## Issues Encountered

- **Insertion de blocs C# accentués par heredoc Bash** de nouveau en échec sur ce shell (même symptôme qu'au plan 01). Contourné en écrivant les blocs avec l'outil `Write` dans le scratchpad, puis en les insérant par un court script Python en UTF-8. Sans conséquence sur le résultat.

## Conformité CLAUDE.md

- Aucune dépendance NuGet ajoutée.
- Aucun type WPF introduit dans `Services/` : `ServicesLayerPurityTests` vert. Les deux fichiers modifiés de `Views/` le sont d'un seul argument chacun.
- XML-doc, commentaires et noms de tests en français.
- Aucun chemin hors profil utilisateur ; en test, uniquement `Path.GetTempPath()`.
- Le mode `--hook` reste inchangé et ne touche jamais `settings.json` (pitfall 8 du RESEARCH : les 5 processus de hook ne sont pas des écrivains concurrents).

## User Setup Required

None - aucune configuration externe requise.

## Next Phase Readiness

**Prêt pour le plan 15-03** (`ClaudeSettingsReconciler`) : les deux cœurs de mutation publics `SessionHookInstaller.ApplyHooks(root, exePath, wanted)` et `StatusLineInstaller.ApplyStatusLine(root, exePath)` opèrent sur un `JsonObject` partagé, ce qui permet au réconciliateur de composer les deux en une seule analyse et une seule sérialisation — puis de comparer la chaîne obtenue à la chaîne lue pour ne rien écrire et ne rien sauvegarder quand rien ne change (pitfall 10).

**Rappel pour le plan 03 :** la note d'ordonnancement est déjà inscrite dans la XML-doc de `StatusLineInstaller.IsInstalled` — le réconciliateur doit s'exécuter **avant** `OfferOnFirstRun()` dans `App.OnStartup`, faute de quoi le repointage arriverait trop tard et un dialogue parasite s'afficherait.

**Ce plan assainit l'avenir, pas le présent.** La machine déjà polluée (25 groupes constatés) reste inchangée tant que le plan 03 n'est pas exécuté : les correctifs livrés ici garantissent seulement qu'aucune installation future n'empilera davantage.

---
*Phase: 15-idempotence-des-int-grations*
*Completed: 2026-09-09*

## Self-Check: PASSED

- 6 fichiers annonces presents sur le disque (2 installateurs, 2 appelants, 2 fichiers de tests)
- 3 commits de tache verifies dans git (a79bb66, 0535d78, c1191d4)
- Suite complete : 387 tests / 0 echec (366 de la vague 1 + 21 nouveaux)
- Gardes ServicesLayerPurityTests et CompositionRootTests vertes
- Aucun stub : les deux installateurs sont entierement cables et couverts par leurs tests
