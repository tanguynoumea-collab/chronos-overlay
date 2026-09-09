---
phase: 15-idempotence-des-int-grations
verified: 2026-09-09T00:00:00Z
status: passed
score: 4/4 must-haves verified
---

# Phase 15: Idempotence des intégrations — Verification Report

**Phase Goal:** L'utilisateur peut installer et réinstaller Chronos autant de fois qu'il veut sans que
`~/.claude/settings.json` n'accumule d'entrées : chaque installation REMPLACE l'entrée Chronos précédente
et PURGE les entrées fantômes pointant sur des exes de versions révolues.
**Verified:** 2026-09-09
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Hooks non cumulatifs (PUR-01) : 3 installations depuis 3 chemins d'exe différents laissent exactement 5 hooks Chronos, repérage sur `--hook` jamais sur le chemin | ✓ VERIFIED | `SessionHookInstaller.ApplyHooks` retire tous les groupes reconnus par `ClaudeSettingsJson.IsChronosGroup` (marqueur `--hook` + nom `Chronos*.exe`) puis ajoute un seul groupe pour l'exe courant. `IsOurEntry` (comparaison au chemin) n'existe plus (`grep -c IsOurEntry` = 0 dans tout `src/`, hors binaires compilés). Test `SessionsTests` prouve le non-cumul sur 3 chemins successifs. Test `ClaudeSettingsReconcilerTests.Purge_la_fixture_reelle_de_25_a_5_hooks_Chronos` prouve 25→5 sur la fixture de l'état réel constaté. |
| 2 | statusLine non cumulatif (PUR-02) : entrée repointée jamais dupliquée, repérage sur `--statusline`, clés inconnues (`padding`) survivent | ✓ VERIFIED | `StatusLineInstaller.ApplyStatusLine` mute uniquement `sl["command"]`, jamais de reconstruction d'objet. Ancien `IsChronosCommand` "contient Chronos" remplacé par le prédicat partagé marqueur+nom d'exe. Test `Repointe_la_statusLine_perimee_et_conserve_padding` prouve `padding == 2` et `type == "command"` après repointage sur la fixture réelle. |
| 3 | Fantômes purgés (PUR-03) : sur une machine polluée, les entrées obsolètes disparaissent au démarrage sans intervention manuelle | ✓ VERIFIED | `ClaudeSettingsReconciler.Reconcile` appelé une seule fois dans `App.xaml.cs` (ligne 98), entre `window.Show()` et `OfferOnFirstRun()`, en mode overlay uniquement. `ReconcileJson` compose `ApplyHooks` + `ApplyStatusLine` sur un même arbre puis compare la forme normalisée avant/après. Vérification terrain documentée dans 15-03-SUMMARY sur une COPIE du vrai fichier (25→5 hooks, statusLine repointée, SHA-256 du vrai fichier inchangé). |
| 4 | Rien d'autre n'est touché : hooks/réglages non-Chronos survivent intacts, fichier illisible/malformé ne provoque ni crash ni écriture | ✓ VERIFIED | `ParseOrNull` renvoie `null` (jamais `new JsonObject()`) sur JSON inexploitable ; seule occurrence légitime de `new JsonObject()` dans un chemin d'écriture est `ClaudeSettingsJson.ParseOrNull` ligne 135 (cas fichier absent/vide). Tests `Un_fichier_malforme_reste_inchange_octet_pour_octet_et_sans_sauvegarde` et théories 4-cas (clés dupliquées, racine tableau, JSON invalide, scalaire) confirment aucune écriture ni exception. `Preserve_les_trois_hooks_GSD_avec_leur_matcher_et_leur_timeout` et `Preserve_agentPushNotifEnabled_et_les_cles_racine_inconnues` prouvent la survie des tiers et l'ordre des clés racine. |

**Score:** 4/4 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Chronos/Services/ClaudeSettingsJson.cs` | Prédicat d'identité + analyse tolérante + sérialisation fidèle | ✓ VERIFIED | 150 lignes, expose les 8 membres publics du contrat (`HookMarker`, `StatusLineMarker`, `ExtractExecutable`, `IsChronosCommand`, `PointsToExe`, `CommandOf`, `IsChronosGroup`, `ParseOrNull`, `Serialize`). Aucune E/S, aucun type WPF. |
| `src/Chronos/Services/SessionHookInstaller.cs` | Installation retirer-puis-ajouter | ✓ VERIFIED | `ApplyHooks(root, exePath, wanted)` public, parcours descendant, `IsOurEntry`/`Parse` supprimés. `Install`/`Uninstall` n'écrivent jamais sur `null`. |
| `src/Chronos/Services/StatusLineInstaller.cs` | Mutation ciblée de `command` | ✓ VERIFIED | `ApplyStatusLine(root, exePath)` public, mute uniquement `sl["command"]`, ne crée jamais l'entrée. `IsInstalled` exige appartenance ET fraîcheur. |
| `src/Chronos/Services/ClaudeSettingsReconciler.cs` | Convergence + sauvegarde conditionnelle + câblage overlay-only | ✓ VERIFIED | `ReconcileJson` (cœur pur, comparaison sur forme normalisée) + `Reconcile` (E/S non levante, sauvegarde bloquante avant écriture, rétention 5, jamais de création de fichier absent). |
| `tests/Chronos.Tests/ClaudeSettingsJsonTests.cs`, `SessionsTests.cs`, `StatusLineInstallerTests.cs`, `ClaudeSettingsReconcilerTests.cs` | Preuves unitaires | ✓ VERIFIED | 405 tests au total, 0 échec, 2 exécutions complètes consécutives confirmées en direct pendant cette vérification. |
| `tests/Chronos.Tests/TestData/claude-settings-pollue.json` | Fixture figée de l'état réel pollué | ✓ VERIFIED | 25 groupes Chronos / 7 clés d'événement / 3 groupes GSD avec `matcher`/`timeout`/`if`, `statusLine` avec `padding`, `agentPushNotifEnabled`. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `SessionHookInstaller` | `ClaudeSettingsJson` | `IsChronosGroup`/`ParseOrNull`/`Serialize` | ✓ WIRED | Utilisés directement dans `ApplyHooks`/`TransformForInstall`/`TransformForUninstall`. |
| `StatusLineInstaller` | `ClaudeSettingsJson` | `IsChronosCommand`/`PointsToExe`/`ParseOrNull`/`Serialize` | ✓ WIRED | Utilisés directement dans `ApplyStatusLine`/`TransformForInstall`. |
| `ClaudeSettingsReconciler` | `SessionHookInstaller.ApplyHooks` + `StatusLineInstaller.ApplyStatusLine` | composition sur un seul `JsonObject` | ✓ WIRED | `ReconcileJson` appelle les deux cœurs de mutation sur le même arbre avant une unique sérialisation. |
| `App.xaml.cs` (`OnStartup`) | `ClaudeSettingsReconciler.Reconcile` | appel après `window.Show()`, avant `OfferOnFirstRun()` | ✓ WIRED | Ligne 98, en dehors des branches `--statusline` (ligne 20-25) et `--hook` (ligne 29-35) qui retournent avant d'atteindre ce point — confirmé par lecture du fichier. |
| `SessionsController.Uninstall` / `StatusLineSetup.Uninstall` | nouvelles signatures sans `exePath` | appel adapté | ✓ WIRED | `SessionsController.cs:63` et `StatusLineSetup.cs:57` compilent contre les nouvelles signatures. |

### Data-Flow Trace (Level 4)

Non applicable : cette phase ne rend aucune donnée dynamique à l'écran (pas de composant UI/ViewModel concerné). La donnée pertinente est le contenu de `~/.claude/settings.json`, tracée de bout en bout via les tests d'E/S sur dossiers temp (lecture → réconciliation → sauvegarde → écriture atomique → relecture), et via la vérification terrain sur copie documentée dans 15-03-SUMMARY.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Suite complète verte | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --nologo -v q` (exécuté 2 fois pendant cette vérification) | 405/405, 0 échec, les 2 fois | ✓ PASS |
| `CompositionRootTests` isolé (test STA flaky signalé) | `dotnet test ... --filter "FullyQualifiedName~CompositionRootTests"` | 3/3, 0 échec | ✓ PASS |
| Build solution | `dotnet build Chronos.sln --nologo -v q` | 0 erreur, 0 avertissement | ✓ PASS |
| `IsOurEntry` absent du code de production | `grep -rn "IsOurEntry" src/` (hors binaires compilés) | 0 occurrence | ✓ PASS |
| `return new JsonObject()` limité à 1 occurrence légitime | `grep -rn "new JsonObject()" src/Chronos/Services/*.cs` | 1 seule dans un chemin de retour (`ClaudeSettingsJson.ParseOrNull`, cas fichier absent) ; les autres occurrences créent des sous-objets internes normaux (`hooks = new JsonObject()`), pas des replis destructeurs | ✓ PASS |
| Aucun test ne cible le vrai profil | `grep -rln "SpecialFolder.UserProfile" tests/Chronos.Tests/*.cs` | 0 fichier | ✓ PASS |
| `GetValue<string>()` (levant) absent du socle | `grep -rn "GetValue<string>()" src/Chronos/Services/*.cs` | 0 occurrence | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| PUR-01 | 15-01, 15-02 | Hooks remplacent au lieu de cumuler (match `--hook`, pas chemin) | ✓ SATISFIED | `ApplyHooks` retirer-puis-ajouter, prouvé par tests multi-chemins et fixture réelle 25→5 |
| PUR-02 | 15-01, 15-02 | statusLine remplace (match `--statusline`) | ✓ SATISFIED | `ApplyStatusLine` mutation ciblée, prouvé par tests de repointage + préservation `padding` |
| PUR-03 | 15-01, 15-03 | Entrées fantômes purgées automatiquement | ✓ SATISFIED | `ClaudeSettingsReconciler` câblé au démarrage overlay-only, prouvé sur fixture et vérification terrain sur copie |

Note sur le flag connu : le plan 15-01 a exécuté `requirements mark-complete PUR-01 PUR-02 PUR-03` en ne posant que le socle (vague 1/3). La traçabilité était bien en avance d'un cran à ce moment-là. Cette vérification confirme que, maintenant que les 3 plans sont livrés (vagues 1, 2 et 3 complètes, 405 tests verts), la satisfaction réelle des trois exigences est effectivement atteinte — le statut `Complete` dans REQUIREMENTS.md et ROADMAP.md est donc correct a posteriori, sans besoin de correction.

Aucun requirement orphelin détecté : REQUIREMENTS.md mappe exactement PUR-01/02/03 à la Phase 15, et les 3 plans déclarent ces mêmes IDs.

### Anti-Patterns Found

Aucun anti-pattern bloquant détecté dans les fichiers de la phase :
- Aucun `TODO`/`FIXME`/`PLACEHOLDER` dans `ClaudeSettingsJson.cs`, `SessionHookInstaller.cs`, `StatusLineInstaller.cs`, `ClaudeSettingsReconciler.cs`.
- Aucun repli destructeur `return new JsonObject()` hors du cas légitime documenté.
- Aucune fuite de type WPF dans les 4 fichiers de service créés/modifiés par cette phase (les occurrences de `System.Windows` dans `Services/` proviennent de fichiers préexistants hors périmètre de la phase 15 — `DesktopUiaPollService`, `OverlayController`, `TopmostGuard`, `UiaNode`, `WindowsUiaTreeProvider`, `WpfUiDispatcher` — couverts par ailleurs par `ServicesLayerPurityTests`, resté vert).

### Human Verification Required

Aucune. Le point qui aurait normalement nécessité une vérification humaine (purge effective du vrai `~/.claude/settings.json` sur la machine réelle, checkpoint 15-03 tâche 4) a été traité par une vérification équivalente non destructive sur une copie du fichier réel (documentée dans 15-03-SUMMARY, SHA-256 du fichier réel inchangé). La purge effective se produira naturellement au prochain lancement normal de Chronos.

### Gaps Summary

Aucun gap. Les 4 critères de succès de la ROADMAP sont vérifiés par lecture directe du code de production (pas seulement des SUMMARY) : `IsOurEntry` a bien disparu, le réconciliateur ne s'exécute jamais en mode `--hook`/`--statusline` (les deux modes retournent avant le point d'appel dans `App.xaml.cs`), la sauvegarde est bloquante et conditionnelle à un changement de forme normalisée, aucun test n'atteint le vrai profil utilisateur, et la suite complète (405 tests) est verte sur deux exécutions consécutives réalisées pendant cette vérification — y compris le test STA `CompositionRootTests.Host_resout_et_dispose_les_singletons` isolément signalé comme ponctuellement instable, confirmé non reproductible (3/3 vert isolé, 405/405 vert en suite complète × 2).

---

*Verified: 2026-09-09*
*Verifier: Claude (gsd-verifier)*
