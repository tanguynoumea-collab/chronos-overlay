---
phase: 13-source-uia-app-bureau
verified: 2026-07-10T13:10:00Z
status: human_needed
score: 7/7 must-haves vérifiés automatiquement (logique/mapping/DI/non-régression) ; 1 validation en app réelle requise
re_verification: null
human_verification:
  - test: "Ouvrir l'app bureau Claude (Chat au repos) avec Chronos lancé, widget sessions activé"
    expected: "Une pastille « Claude (bureau) » apparaît avec le badge « Chat » et l'état « tour fini » (attend ton message)"
    why_human: "La reconnaissance dépend des libellés réels et de l'ancre AutomationId=RootWebArea produits par le build courant de l'app bureau Claude — non observables sans fenêtre réelle. Le spike n'avait pas capturé l'état REPOS (noté dans 13-01-SUMMARY et la ROADMAP)."
  - test: "Lancer une session Code agentique dans l'app bureau (panneaux Terminal/Diff), puis en démarrer une seconde nommée dans la sidebar"
    expected: "La conversation au premier plan est typée « Code » et la/les session(s) « En cours d'exécution <nom> » de la sidebar sont énumérées en plus (état « en cours »)"
    why_human: "Dépend des ControlType/Name réels des panneaux Code et du préfixe sidebar dans le build courant ; matching prouvé sur faux arbre uniquement."
  - test: "Ouvrir une session Cowork exécutée en VM distante (Contrôle à distance visible)"
    expected: "La pastille est typée « Cowork » et l'état affiché est « inconnu » (jamais « en cours »)"
    why_human: "BUR-05 : la logique force Unknown (testée), mais la présence du signal « Contrôle à distance » réel doit être confirmée en app."
  - test: "Fermer l'app bureau Claude pendant que Chronos tourne"
    expected: "Aucune pastille bureau, les sessions CLI restent affichées, aucun crash ni gel de l'overlay"
    why_human: "Vérifie la dégradation WindowMissing et la non-régression CLI dans des conditions réelles (le try/catch et le cache vide sont vérifiés en test unitaire)."
---

# Phase 13 : Source UIA app bureau — Rapport de vérification

**Phase Goal:** Livrer un `DesktopUiaSessionSource` lisant l'arbre UI Automation de la fenêtre Claude : faire apparaître dans le widget les sessions Chat/Code/Cowork de l'app bureau (en plus du CLI), chacune avec un état honnête, son type identifié, la sidebar énumérée ; robuste aux MAJ (matching souple fr/en, test de santé, dégradation) et sans jamais bloquer le thread UI.

**Verified:** 2026-07-10T13:10:00Z
**Status:** human_needed
**Re-verification:** Non — vérification initiale

## Distinction clé : ce qui EST vérifié automatiquement vs ce qui exige l'app réelle

- **Vérifié automatiquement (code + 304 tests verts)** : toute la LOGIQUE (mapping arbre→snapshots, états honnêtes, dérivation du type, énumération sidebar, ancre, santé/dégradation), le CÂBLAGE DI (résolution réelle prouvée), la FUSION dans `SessionMonitor`, la NON-RÉGRESSION CLI, l'AFFICHAGE du type, et — par inspection de code — la propagation de `AutomationId` par le provider réel (piège « tests verts / prod vide » évité).
- **Exige l'app réelle (human_needed)** : que les libellés fr/en de la table et l'ancre `RootWebArea` correspondent EXACTEMENT à ce que produit le build courant de l'app bureau Claude. C'est intrinsèquement non testable sans fenêtre réelle (et le snapshot « repos » n'avait pas été capturé au spike).

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
| --- | ----- | ------ | -------- |
| 1 | Sessions bureau visibles via UIA (fusionnées dans le pipeline) | ✓ VERIFIED (logique/DI) / ? app réelle | `SessionMonitor.Read` étape 2.b fusionne `_desktop.Read(now)` ; DI câblée (`App.xaml.cs:182-191`) ; poll de fond alimente le cache ; test DI réel résout `SessionMonitor`+`DesktopUiaPollService` |
| 2 | États honnêtes ; Cowork VM forcé indéterminé | ✓ VERIFIED | `InferActivity` (Working/WaitingAttention/WaitingTurn/Unknown) ; `MapTree` ligne 102 force `Unknown` si `Kind==Cowork` ; test `MapTree_controle_a_distance_donne_Cowork_indetermine` |
| 3 | Type Chat/Code/Cowork dérivé ET affiché | ✓ VERIFIED | `InferKind` ; `SessionItemVm.KindLabel`+`KindText` ; badge XAML masqué si vide ; tests `Widget_affiche/mappe_...` |
| 4 | Sessions sidebar énumérées | ✓ VERIFIED | `MapTree` boucle sidebar (préfixe `RunningPrefix`) ; test `MapTree_sidebar_enumere_...` (ignore les non-préfixées) |
| 5 | Matching souple fr/en, pas d'AutomationId volatil | ✓ VERIFIED | `UiaLabels` (table fr/en extensible) + `Matches`/`StartsWithAny` tolérants ; seul AutomationId matché = ancre `RootWebArea` (justifiée) |
| 6 | Test de santé + dégradation tracée, aucune source ≠ crash | ✓ VERIFIED | `DesktopHealth` (Ok/WindowMissing/AnchorMissing) ; `Poll`/`TryGetTree`/`Read` en try/catch ne lèvent jamais ; tests Health + `MapTree_racine_null` |
| 7 | Lecture UIA hors thread UI, `Read` non bloquant (cache), racine cachée | ✓ VERIFIED | `DesktopUiaPollService` Timer .NET (thread pool) ; `Read` rend le cache sans I/O (test `Read_..._ne_touche_pas_le_provider`) ; `WindowsUiaTreeProvider._root` caché+réacquis |

**Score:** 7/7 truths vérifiées au niveau logique/mapping/DI ; validation comportementale en app réelle requise (human_needed).

### Required Artifacts

| Artifact | Attendu | Status | Détails |
| -------- | ------- | ------ | ------- |
| `SessionSnapshot.cs` | Kind/Origin ajoutés non cassants | ✓ VERIFIED | enums + record étendu, défauts Unknown/Cli en fin |
| `UiaNode.cs` | DTO neutre avec AutomationId | ✓ VERIFIED | record 4 champs + Children |
| `IUiaTreeProvider.cs` / `ISessionSource.cs` | seams neutres | ✓ VERIFIED | présents, consommés |
| `UiaLabels.cs` | table fr/en + matching | ✓ VERIFIED | 8 catégories de libellés, helpers tolérants |
| `DesktopUiaSessionSource.cs` | MapTree pur + santé + cache | ✓ VERIFIED | logique complète, câblé dans SessionMonitor via DI |
| `WindowsUiaTreeProvider.cs` | provider réel, AutomationId propagé | ✓ VERIFIED (code) | ligne 138 `automationId = info.AutomationId` — ancre produite en prod ; câblé en DI |
| `DesktopUiaPollService.cs` | IHostedService poll hors UI | ✓ VERIFIED | Timer pool, PollOnce, AddHostedService |
| `SessionMonitor.cs` | fusion desktop | ✓ VERIFIED | 4e param optionnel, étape 2.b try/catch |
| `App.xaml.cs` | câblage DI complet | ✓ VERIFIED | lignes 182-191, ordre correct |
| `SessionsViewModel.cs` + `SessionsWindow.xaml` | affichage du type | ✓ VERIFIED | KindLabel + badge conditionnel |

### Key Link Verification

| From | To | Via | Status | Détails |
| ---- | -- | --- | ------ | ------- |
| `DesktopUiaPollService` | `DesktopUiaSessionSource.Poll` | Timer → PollOnce | ✓ WIRED | remplit le cache hors thread UI |
| `SessionMonitor.Read` | source bureau | `_desktop.Read(now)` | ✓ WIRED | fusion additive avant filtre archived |
| DI | toute la chaîne | `App.xaml.cs` ConfigureServices | ✓ WIRED | garde par test de résolution réel |
| `SessionsViewModel.Refresh` | `KindLabel` | `KindText(s.Kind)` | ✓ WIRED | badge XAML lié |
| `WindowsUiaTreeProvider.Convert` | ancre RootWebArea | `info.AutomationId` propagé | ✓ WIRED | **piège prod-vide évité** (inspection code) |

### Data-Flow Trace (Level 4)

| Artifact | Variable | Source | Produit données réelles | Status |
| -------- | -------- | ------ | ----------------------- | ------ |
| Widget (pastilles bureau) | `Items` (KindLabel/State) | `SessionMonitor.Read` ← cache `DesktopUiaSessionSource` ← `Poll` ← `WindowsUiaTreeProvider.TryGetTree` | ✓ en logique ; dépend de l'arbre a11y réel | ⚠️ FLOWING sous réserve app réelle |

Le flux de données est complet et branché de bout en bout. La seule inconnue est la source la plus en amont (`AutomationElement` de la vraie fenêtre Claude), non inspectable sans l'app — d'où le human_needed.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Suite complète verte | `dotnet test tests/Chronos.Tests` | 304 réussis / 0 échec | ✓ PASS |
| Résolution DI de la chaîne bureau | test `Le_graphe_DI_resout_les_services_bureau_UIA` | vert | ✓ PASS |
| Lecture UIA réelle sur fenêtre Claude | — | nécessite app réelle | ? SKIP → human |

### Requirements Coverage

| Requirement | Source Plan | Status | Evidence |
| ----------- | ----------- | ------ | -------- |
| BUR-01 | 02+03 | ✓ SATISFIED (auto) / ? app réelle | provider réel + fusion + DI + garde de résolution |
| BUR-02 | 01 | ✓ SATISFIED | InferActivity, tests d'états |
| BUR-03 | 01+03 | ✓ SATISFIED | InferKind + KindLabel + XAML |
| BUR-04 | 01 | ✓ SATISFIED | boucle sidebar testée |
| BUR-05 | 01 | ✓ SATISFIED | Cowork forcé Unknown, testé |
| ROB-06 | 01 | ✓ SATISFIED | UiaLabels fr/en + DesktopHealth tracée |
| ROB-07 | 02+03 | ✓ SATISFIED | poll hors thread UI, Read cache non bloquant |

Aucun requirement orphelin : les 7 IDs de la ROADMAP sont couverts par les plans.

### Anti-Patterns Found

Aucun blocker ni warning. Points relevés (info) :
- `Read` retourne `_cache` (volatile) — attendu (contrat non bloquant ROB-07), pas un stub : le cache est peuplé par le poll de fond, prouvé par test.
- `WindowsUiaTreeProvider` non unit-testé — par conception (dépend de l'OS), sans logique métier ; sa correction est validée à l'exécution (human_needed ci-dessus).
- Déviation csproj (pas de `<Reference>` UIA explicite, assemblies fournies par WindowsDesktop/UseWPF) — auto-corrigée, build 0 avertissement. Non bloquant.

### Human Verification Required

Voir le bloc `human_verification` du frontmatter (4 tests en app réelle : Chat repos, Code+sidebar, Cowork VM, app fermée). Ces tests confirment que les libellés/ancre réels du build Claude courant correspondent à la table — le seul maillon non observable en test.

### Gaps Summary

Aucun gap bloquant. Toute la logique, le câblage DI, la fusion, la non-régression et l'affichage sont vérifiés automatiquement (304 tests verts). Le piège « tests verts / prod vide » est explicitement évité : `WindowsUiaTreeProvider.Convert` renseigne `AutomationId = info.AutomationId` sur chaque nœud, donc l'ancre `RootWebArea` est réellement émise en production (vérifié par inspection — non testable hors OS).

Le statut est `human_needed` (et non `passed`) uniquement parce que l'atteinte réelle du goal — voir apparaître les sessions bureau — ne peut être confirmée sans une fenêtre Claude réelle : la table de libellés et l'ancre reposent sur le spike du 2026-07-10 (dont l'état « repos » manquait). Le matching tolérant et extensible rend toute correction triviale (ajouter une variante dans `UiaLabels`, sans toucher au code de logique) si un libellé diffère en pratique.

---

_Verified: 2026-07-10T13:10:00Z_
_Verifier: Claude (gsd-verifier)_
