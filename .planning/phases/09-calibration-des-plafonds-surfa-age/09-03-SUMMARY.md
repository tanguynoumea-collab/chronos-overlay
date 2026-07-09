---
phase: 09-calibration-des-plafonds-surfa-age
plan: 03
subsystem: ui-cadran
tags: [tokens, estimation, cadran, viewmodel, xaml, net-02, honnetete]

# Dependency graph
requires:
  - phase: 09-calibration-des-plafonds-surfa-age
    plan: 01
    provides: "TokenFormatter.Format (fr abrégé M/k) ; EstimatedTokens toujours porté par le repli"
  - phase: 03-mod-les-pipeline-de-donn-es
    provides: "WindowState immuable (EstimatedTokens, SourceReliability) ; WindowGaugeViewModel + Apply ; converter BoolToVis + token TexteSecondaire"
provides:
  - "WindowGaugeViewModel.TokensText + HasTokens dérivés dans Apply (Estimated + tokens>0)"
  - "Deux TextBlocks discrets (TokensCinqHeures / TokensHebdo) liés à TokensText/HasTokens"
  - "NET-02 soldé : matière première de l'estimation surfacée sans jamais la présenter comme exacte"
affects: [milestone-v1.1-cloture]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dérivation UI pure dans Apply (aucun I/O) : HasTokens pilote la visibilité, TokensText le contenu"
    - "TextBlock discret = FontSize 9 + TexteSecondaire + Visibility via BoolToVis, ajouté après les badges sans déplacer le layout"

key-files:
  created: []
  modified:
    - src/Chronos/ViewModels/WindowGaugeViewModel.cs
    - src/Chronos/Views/MainWindow.xaml
    - tests/Chronos.Tests/CadranBindingTests.cs

key-decisions:
  - "Tokens affichés UNIQUEMENT en source Estimated avec tokens>0 : jamais en Exact (les % exacts suffisent) ni sans donnée — honnêteté (le badge « estimée » reste, aucun chiffre estimé présenté comme exact)"
  - "Dérivation dans Apply plutôt que propriétés calculées : cohérent avec IsEstimated/Exhausted déjà dérivés là"

requirements-completed: [NET-02]

# Metrics
duration: 2min
completed: 2026-07-09
---

# Phase 9 Plan 03 : Surfaçage des tokens estimés (NET-02) Summary

**Surfaçage de la matière première de l'estimation : `WindowGaugeViewModel` expose `TokensText` (« ≈ 62,5 M tokens », via le `TokenFormatter` de 09-01) et `HasTokens`, dérivés dans `Apply` uniquement en source Estimated avec tokens>0 ; deux TextBlocks discrets (5 h + hebdo) les affichent sous chaque countdown — clôture de la Phase 9 et du milestone v1.1.**

## Performance

- **Duration:** 2 min
- **Started:** 2026-07-09T06:16:11Z
- **Completed:** 2026-07-09T06:17:39Z
- **Tasks:** 2
- **Files modified:** 3 (0 créés, 3 modifiés)

## Accomplishments

- `WindowGaugeViewModel` : deux `[ObservableProperty]` ajoutées — `TokensText` (contenu « ≈ N M/k tokens ») et `HasTokens` (pilote la visibilité). Dérivées dans `Apply` : `HasTokens = Estimated && EstimatedTokens is > 0`, `TokensText = HasTokens ? TokenFormatter.Format(...) : ""`. Jamais de tokens en Exact ni sans donnée.
- `MainWindow.xaml` : deux TextBlocks discrets `TokensCinqHeures` / `TokensHebdo` ajoutés après les badges « estimée », liés à `FiveHour`/`SevenDay` `TokensText` (Text) et `HasTokens` (Visibility via `BoolToVis`). FontSize 9, `TexteSecondaire` (#A9A8B2), pas de gras — layout du cadran inchangé.
- `CadranBindingTests` : 3 `[Fact]` sur le sous-VM — Estimated 62 484 658 → `HasTokens=true` + `TokensText=="≈ 62,5 M tokens"` ; Exact avec `EstimatedTokens=null` → `HasTokens=false` + `""` ; Estimated avec 0 token → `HasTokens=false`.

## Task Commits

1. **Task 1 : WindowGaugeViewModel — TokensText + HasTokens** — `f02c967` (feat + 3 tests)
2. **Task 2 : MainWindow.xaml — TextBlocks tokens discrets** — `60a8156` (feat)

## Files Created/Modified

- `src/Chronos/ViewModels/WindowGaugeViewModel.cs` — +TokensText/HasTokens, dérivation dans Apply (`using Chronos.Text;` déjà présent).
- `src/Chronos/Views/MainWindow.xaml` — +2 TextBlocks tokens discrets (groupes 5 h et hebdo).
- `tests/Chronos.Tests/CadranBindingTests.cs` — +3 [Fact] sur la dérivation TokensText/HasTokens.

## Decisions Made

- **Tokens uniquement en Estimated + tokens>0** : honnêteté préservée — le badge « estimée » reste, aucun chiffre estimé n'est jamais présenté comme exact ; en Exact, les pourcentages officiels suffisent et aucun token n'est affiché.
- **Dérivation dans Apply** (plutôt que propriétés calculées) : cohérent avec `IsEstimated`/`Exhausted`/`Utilization` déjà dérivés au même endroit, pur (aucun I/O).

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## Known Stubs

None — les deux TextBlocks sont câblés sur des sous-VM réels (`FiveHour`/`SevenDay`) alimentés par le pipeline JSONL ; `TokensText` provient du `TokenFormatter` sur `EstimatedTokens` réel.

## User Setup Required

None.

## Next Phase Readiness

- **Phase 9 close ; milestone v1.1 fonctionnellement complet.** NET-02 soldé (dernier requirement du plan). Reste hors code : smoke manuel (cadran en mode repli affichant « ≈ N M tokens » sous chaque fenêtre estimée) et clôture milestone (`/gsd:complete-milestone`).
- Suite complète : **150 tests verts** (147 après 09-02 + 3 nouveaux), build XAML 0 erreur.

## Self-Check: PASSED

Tous les fichiers modifiés présents ; les 2 commits de tâche présents dans l'historique git.

---
*Phase: 09-calibration-des-plafonds-surfa-age*
*Completed: 2026-07-09*
</content>
</invoke>
