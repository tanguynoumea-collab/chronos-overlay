---
phase: 11-int-gration-composite-r-glage
plan: 01
subsystem: api
tags: [oauth, usage, composite, settings, dependency-injection, di, csharp, dotnet]

# Dependency graph
requires:
  - phase: 10-s-curit-lecture-token-appel-endpoint
    provides: ClaudeOAuthUsageProvider (Exact), ClaudeTokenReader, IClaudeTokenReader
  - phase: 03 (v1.0)
    provides: CompositeUsageProvider (best-par-fenêtre), IUsageProvider, UsageSnapshot
  - phase: 06 (v1.0)
    provides: SettingsService atomique + ChronosSettings record neutre
provides:
  - "ChronosSettings.OAuthUsageEnabled (bool, défaut true) persisté round-trip"
  - "GatedOAuthUsageProvider : portillon zéro-accès (Empty sans lire le token si désactivé)"
  - "Chaîne DI composite à 3 niveaux imbriquée : OAuth gated → statusLine → JSONL"
  - "CompositeUsageProvider.Best généralisé au rang de fiabilité (supporte l'imbrication)"
affects: [11-02, toggle-menu-oauth, ui-reglage]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Portillon gated : lecture FRAÎCHE du flag settings à chaque GetAsync (comme les plafonds JSONL)"
    - "Chaîne de providers par IMBRICATION de CompositeUsageProvider plutôt que réécriture"

key-files:
  created:
    - src/Chronos/Services/GatedOAuthUsageProvider.cs
    - tests/Chronos.Tests/GatedOAuthUsageProviderTests.cs
  modified:
    - src/Chronos/Services/ChronosSettings.cs
    - src/Chronos/Services/CompositeUsageProvider.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/SettingsServiceTests.cs
    - tests/Chronos.Tests/CompositeUsageProviderTests.cs

key-decisions:
  - "OAuthUsageEnabled défaut TRUE : vrais chiffres exacts dès l'installation ; false = v1.1 strict, coffre jamais ouvert"
  - "Portillon gated séparé du provider OAuth : la garde de pureté Services reste verte, sécurité prouvée par ReadCount==0/SendCount==0"
  - "Généralisation de CompositeUsageProvider.Best au rang de fiabilité (Exact>Estimated>Unavailable) pour supporter l'imbrication sans dupliquer la logique"

patterns-established:
  - "Gated provider : relit le flag frais → toggle menu effectif au prochain tick sans redémarrage"
  - "Composite imbriqué : best-par-fenêtre réutilisé pour une chaîne à N sources"

requirements-completed: [INT-01, INT-03]

# Metrics
duration: 4min
completed: 2026-07-09
---

# Phase 11 Plan 01: Intégration composite OAuth + réglage OAuthUsageEnabled Summary

**Provider OAuth exact branché en tête d'une chaîne composite à 3 niveaux (OAuth gated → statusLine → JSONL) avec un portillon `OAuthUsageEnabled` garantissant zéro accès au token quand désactivé.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-07-09T08:35:42Z
- **Completed:** 2026-07-09T08:40:00Z
- **Tasks:** 3
- **Files modified:** 7 (2 créés, 5 modifiés)

## Accomplishments
- Flag `OAuthUsageEnabled` (défaut true) ajouté au record neutre `ChronosSettings`, persistance round-trip prouvée.
- `GatedOAuthUsageProvider` : relit le flag frais à chaque appel ; désactivé → `UsageSnapshot.Empty` SANS lire le token (`ReadCount==0`) ni appeler l'endpoint (`SendCount==0`).
- Chaîne DI à 3 niveaux dans `App.xaml.cs` par imbrication de `CompositeUsageProvider` : OAuth (exact, gated) → pont statusLine (exact) → JSONL (estimé), best-par-fenêtre.
- Suite complète verte : 178 tests d'origine + 5 nouveaux = **183/183**, `ServicesLayerPurityTests` incluse.

## Task Commits

Each task was committed atomically:

1. **Task 1: champ OAuthUsageEnabled + round-trip settings.json** - `ca23887` (feat)
2. **Task 2: GatedOAuthUsageProvider (Empty sans accès token si désactivé)** - `dc8e7d3` (feat)
3. **Task 3: chaîne DI à 3 niveaux + fix ranking composite** - `efff91f` (feat)

## Files Created/Modified
- `src/Chronos/Services/ChronosSettings.cs` - Ajout du flag `OAuthUsageEnabled` (défaut true).
- `src/Chronos/Services/GatedOAuthUsageProvider.cs` - Portillon neutre autour du provider OAuth (créé).
- `src/Chronos/Services/CompositeUsageProvider.cs` - `Best` généralisé au rang de fiabilité (déviation Rule 1).
- `src/Chronos/App.xaml.cs` - Reader + provider OAuth + gated + chaîne `IUsageProvider` imbriquée à 3.
- `tests/Chronos.Tests/SettingsServiceTests.cs` - Round-trip du flag + assertion de défaut true.
- `tests/Chronos.Tests/GatedOAuthUsageProviderTests.cs` - Off/On/bascule à chaud (créé).
- `tests/Chronos.Tests/CompositeUsageProviderTests.cs` - 2 tests de priorité imbriquée.

## Decisions Made
- `OAuthUsageEnabled` défaut **true** : l'overlay est exact dès l'install ; le passage à false rétablit le comportement v1.1 strict (le coffre n'est jamais ouvert).
- Portillon **séparé** du provider OAuth : garde de pureté Services verte, et la preuve de sécurité (0 lecture token / 0 appel réseau) est isolée et testable.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Généralisation de `CompositeUsageProvider.Best` au rang de fiabilité**
- **Found during:** Task 3 (chaîne DI à 3 niveaux + test de priorité imbriquée)
- **Issue:** Le plan supposait « le CompositeUsageProvider gère déjà best-par-fenêtre → aucune réécriture » et demandait explicitement de NE PAS toucher `CompositeUsageProvider.cs`. Or l'ancien `Best` ne promouvait le repli QUE s'il était `Estimated` (`fallback.Reliability == Estimated ? fallback : primary`). Dans la chaîne imbriquée, le repli (composite interne statusLine) peut produire un `Exact` pour la fenêtre 7j ; l'ancien code le traitait comme « non-Estimated » et conservait le primaire `Unavailable` de l'OAuth. Le test `Chaine_imbriquee_OAuth_prime_puis_statusLine_puis_JSONL_par_fenetre` échouait (7j attendu Exact statusLine, obtenu Unavailable).
- **Fix:** `Best` classe désormais par rang de fiabilité (Exact=2 > Estimated=1 > Unavailable=0) et ne retient le repli que s'il est STRICTEMENT plus fiable (égalité → primaire prioritaire). Cela couvre l'Exact venant d'un repli imbriqué tout en préservant les 6 cas d'origine (Assert.Same intacts).
- **Files modified:** src/Chronos/Services/CompositeUsageProvider.cs
- **Verification:** Les 8 tests composite passent (6 d'origine + 2 imbriqués) ; suite complète 183/183.
- **Committed in:** `efff91f` (Task 3 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** La correction était nécessaire pour honorer la must_have truth #1 (« OAuth prime PAR FENÊTRE sur statusLine puis JSONL »). Changement minimal, aucun test d'origine cassé, aucun scope creep.

## Issues Encountered
None au-delà de la déviation ci-dessus (détectée et corrigée par le test d'imbrication).

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Backend de l'intégration prêt : le flag `OAuthUsageEnabled` est persisté et le portillon opérationnel.
- Reste pour 11-02 : exposer le toggle « Usage exact (OAuth) » dans le menu contextuel (UI) qui écrit `OAuthUsageEnabled` via `SettingsService`.

---
*Phase: 11-int-gration-composite-r-glage*
*Completed: 2026-07-09*

## Self-Check: PASSED

- Fichiers créés vérifiés présents (GatedOAuthUsageProvider.cs, GatedOAuthUsageProviderTests.cs, 11-01-SUMMARY.md)
- Commits vérifiés présents (ca23887, dc8e7d3, efff91f)
