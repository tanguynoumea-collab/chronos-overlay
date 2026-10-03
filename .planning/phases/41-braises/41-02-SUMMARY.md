---
phase: 41-braises
plan: 02
subsystem: cadrans
tags: [wpf, braises, mvvm, fuseau, tdd]
requires:
  - phase: 41-01 (vue Braises groupée, flèche de reset)
provides:
  - WindowGaugeViewModel.HeureResetTexte / HasHeureReset (fuseau injecté, exact ou rien)
  - CadranBraisesView : 3e ligne HeureResetBraises dans le centre temps
  - CadranPreviewViewModel : échantillon déterministe de l'heure du reset
affects: [42 (zones de geste / silhouette du centre Braises)]
tech-stack:
  added: []
  patterns: [fuseau injecté par paramètre optionnel du ctor (production Local, tests Paris)]
key-files:
  created: []
  modified:
    - src/Chronos/ViewModels/WindowGaugeViewModel.cs
    - src/Chronos/ViewModels/CadranPreviewViewModel.cs
    - src/Chronos/Views/Cadrans/CadranBraisesView.xaml
    - tests/Chronos.Tests/WindowGaugeViewModelTests.cs
    - tests/Chronos.Tests/CadranBraisesTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
decisions:
  - "Heure du reset calculée dans Interpolate (dépend de now), masquée si resets_at inconnu ou ≤ now ; un plancher garde l'heure"
  - "Galerie : HasHeureReset = reset > maintenant (échantillon), donc rien à FiveTimePct = 0, comme en production"
metrics:
  duration: ~12 min
  completed: 2026-10-03
  tasks: 2
  files: 6
requirements: [BRA-02]
---

# Phase 41 Plan 02 : Heure exacte du reset dans Braises — Summary

En mode temps, le centre de Braises affiche une 3e ligne « ↻ HH:MM ». C'est l'heure locale du reset 5 h, tirée de resets_at
avec un fuseau injecté (`TimeZoneInfo.Local` en production, Paris dans les tests, changement d'heure compris). La ligne
disparaît quand l'heure est inconnue ou déjà atteinte : exact ou rien. Le mode pourcentages ne change pas.

## Tâches

| # | Tâche | Commits |
|---|-------|---------|
| 1 | WindowGaugeViewModel : HeureResetTexte / HasHeureReset, ctor `(TimeSpan, TimeZoneInfo? fuseau = null)` | `3888a22` (RED), `818385a` (GREEN) |
| 2 | Vue Braises : ligne HeureResetBraises + échantillon de la galerie | `a39af44` (RED), `2dd15d3` (GREEN) |

## Ce qui a été livré

- `WindowGaugeViewModel` : champ `_fuseau` (`fuseau ?? TimeZoneInfo.Local`). Dans `Interpolate(now)` :
  `HasHeureReset = _state.ResetsAt is { } rr && rr > now`, puis le texte via `TextesHistorique.HeureMinute`. Aucun
  `DateTime.Now`, aucun `ToLocalTime`, `ResetsAt` reste privé, aucune comparaison avec `TimeSpan.From…` (garde de doctrine
  verte). Les quelque 25 appels existants du ctor compilent sans changement.
- `CadranBraisesView.xaml` : `TextBlock x:Name="HeureResetBraises"` (10,5, Margin 0,2,0,0, `TexteSecondaire`), placé en
  3e enfant du seul StackPanel temps et lié à `FiveHour.HeureResetTexte` / `FiveHour.HasHeureReset`. Le panneau % n'a pas
  été modifié, l'en-tête a été mis à jour.
- `CadranPreviewViewModel.Apply()` : l'heure de l'échantillon déterministe (14:00Z + 5 h × temps %) ne s'affiche que si ce
  reset est futur (**correction du vérificateur** : `reset > maintenant`, équivalent à `FiveTimePct > 0`). L'hebdo n'a jamais
  d'heure.
- Tests : 7 tests purs côté VM (connu, inconnu, atteint à now et à now − 1 min, plancher, DST du 25/10 → « ↻ 02:30 »,
  disparition, défaut). 4 tests de vue seule ou de galerie (3e ligne, mode % à 2 lignes, ligne masquée sans heure, hebdo et
  `FiveTimePct = 0`). 1 test sur la vraie MainWindow (heure locale visible avec resets_at, masquée avec `UsageSnapshot.Empty`).

## Vérification

- Filtre de la tâche 1 : 88/88. Filtre de la tâche 2 : 96/96.
- `dotnet build Chronos.sln` en Debug et en Release : 0 avertissement, 0 erreur.
- `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` : 1893/1893 verts.
- Critères grep d'acceptation : tous conformes.
- Revue visuelle manuelle (galerie `--cadrans` et overlay réel, à 100 % et 150 %) : à faire par l'utilisateur. L'exe n'a pas
  été lancé.

## À signaler pour la phase 42

Avec la ligne « ↻ HH:MM », le centre temps de Braises mesure environ 50 px de haut (16 + 11 + 10,5 de corps, plus les
interlignes et les marges). Il tient toujours dans le diamètre utile de l'anneau hebdo (R 44). Une zone de clic au centre
doit couvrir ces trois lignes.

## Deviations from Plan

**1. [Correction du vérificateur] Échantillon de galerie conditionné**
- Le plan posait `FiveHour.HasHeureReset = true` sans condition. Comme demandé par le vérificateur, la galerie suit
  maintenant la règle de production : `HasHeureReset = reset > maintenant`. Un test le couvre (`FiveTimePct = 0` → pas
  d'heure). Commit : `2dd15d3`.

À part ce point, le plan a été exécuté tel qu'écrit. Les skills `frontend-design` et `windows-wpf` ne sont pas disponibles
dans cet environnement d'exécution. Le XAML suit donc .zeus/DESIGN_PLAN_CYCLE2.md § 3 (« ↻ 14:20 », 10,5, `TexteSecondaire`)
et les conventions de la vue : corps littéraux, `DynamicResource`, aucune couleur, aucune animation.

## Known Stubs

Aucun.

## Self-Check: PASSED
