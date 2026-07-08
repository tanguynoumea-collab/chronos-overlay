---
phase: 05-cadran-ringarc-converters-c-blage-view
plan: 03
subsystem: ui
tags: [wpf, xaml, mvvm, binding, ivalueconverter, resourcedictionary, design-tokens, stafact, smoke-test]

# Dependency graph
requires:
  - phase: 05-cadran-ringarc-converters-c-blage-view
    provides: "RingArc / TickRing / UtilizationToBrushConverter (05-02) — enveloppes WPF instanciées ici en XAML"
  - phase: 04-orchestration-refresh-viewmodel-temps-r-el
    provides: "MainViewModel (FiveHour/SevenDay WindowGaugeViewModel, DataUnavailable, IsStale) — surface de binding, non modifiée"
provides:
  - "Cadran complet composé dans MainWindow.xaml : fond/rim, ticks mineurs/majeurs, 2 pistes, 2 arcs de valeur bindés, textes centraux, badges par fenêtre, staleness/indisponible"
  - "Resources/DesignTokens.xaml : ResourceDictionary autonome (tokens couleur verrouillés + converters) mergé par App.xaml ET Window.Resources — source unique de vérité testable"
  - "CadranBindingTests : smoke [WpfFact] prouvant la construction sans crash dans 4 états (exact/estimé/indisponible/fiabilité mixte), DAT-08 par fenêtre + ROB-01"
affects: [06-comportements-overlay, MainWindow.xaml]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Tokens de design + converters dans un ResourceDictionary autonome (Resources/DesignTokens.xaml), mergé au niveau App ET Window → la vue résout ses StaticResource sans dépendre d'une Application démarrée (testabilité)"
    - "Composition XAML pure : arcs/ticks/pistes empilés dans un Grid, tout piloté par Binding + converters, zéro code-behind ajouté (MainWindow.xaml.cs inchangé)"
    - "Signaux de fiabilité PAR FENÊTRE : chaque anneau porte ses propres badges IsEstimated/Exhausted (les fenêtres 5 h et hebdo ont des provenances indépendantes)"
    - "Smoke [WpfFact] : Measure/Arrange forcé sur MainWindow pour déclencher les bindings, assertions sur FindName + propriétés VM ; fenêtre auto-suffisante (aucun setup de resources en test)"

key-files:
  created:
    - src/Chronos/Resources/DesignTokens.xaml
    - tests/Chronos.Tests/CadranBindingTests.cs
  modified:
    - src/Chronos/App.xaml
    - src/Chronos/Views/MainWindow.xaml

key-decisions:
  - "Tokens extraits dans Resources/DesignTokens.xaml (dictionnaire autonome) plutôt qu'inline dans App.xaml : mergé par App.xaml (global) ET par Window.Resources (la vue est auto-suffisante) → les StaticResource résolvent en test sans Application.Run et sans mutation cross-thread d'un singleton Application"
  - "Les DEUX nuances secondaires verrouillées servent chacune un usage : TexteSecondaireClair (#C7C6D0) = countdown hebdo (vivant, secondaire) ; TexteSecondaire (#A9A8B2) = badges/mentions annexes (estimée, épuisé, périmée, indisponible) — aucun token orphelin"
  - "Signaux estimée/épuisé bindés PAR FENÊTRE (FiveHour.* et SevenDay.* indépendants) ; staleness globale (IsStale) en mention secondaire dédiée, conformément à la décision verrouillée « staleness signalée en texte secondaire »"
  - "Aucun code-behind : MainWindow.xaml.cs (placement + StartClock) laissé strictement inchangé ; tout le cadran = bindings + converters"

patterns-established:
  - "Resources/ = dictionnaires de tokens/converters autonomes, mergés au niveau App et Window pour une vue testable en isolation"
  - "Tests de vue = [WpfFact] construisant la fenêtre réelle + Measure/Arrange, assertions FindName + surface VM, fenêtre auto-suffisante (pas de dépendance à Application.Current)"

requirements-completed: [CAD-01, CAD-02, CAD-03, CAD-05, CAD-06, DAT-08, ROB-01]

# Metrics
duration: 9 min
completed: 2026-07-08
---

# Phase 5 Plan 3 : Composition du cadran + câblage View Summary

**Le cadran Chronos assemblé : `MainWindow.xaml` empile fond/rim, ticks mineurs/majeurs, deux pistes et deux arcs de valeur (RingArc 05-02) bindés sur `FiveHour`/`SevenDay` du `MainViewModel` (Phase 4), avec countdown central, badges « estimée »/« quota épuisé » INDÉPENDANTS par fenêtre, staleness et « données indisponibles » — tokens verrouillés centralisés dans un `DesignTokens.xaml` autonome, le tout prouvé sans crash dans 4 états par un smoke [WpfFact].**

## Performance

- **Duration:** ~9 min
- **Started:** 2026-07-08T16:43:21Z
- **Completed:** 2026-07-08T16:52:06Z
- **Tasks:** 3 (2 auto + 1 checkpoint human-verify auto-vérifié)
- **Files:** 2 créés, 2 modifiés

## Accomplishments

- **Task 1 — Cadran + tokens.** `Resources/DesignTokens.xaml` centralise les 9 tokens couleur
  verrouillés (dont les deux nuances secondaires `#A9A8B2` / `#C7C6D0`), le `UtilizationToBrushConverter`
  (05-02) et `BooleanToVisibilityConverter`. `MainWindow.xaml` remplace le placeholder `#CC1E1E1E`
  par le cadran complet : Ellipse fond+rim, `TickRing` ×2 (mineurs 60 / majeurs 12), `RingArc` ×2
  pistes + ×2 arcs de valeur bindés (`FractionRemaining` + `Utilization`→couleur via converter),
  StackPanel central avec countdown 5 h (principal) et hebdo (secondaire clair), badges par fenêtre,
  mentions staleness/indisponible. Zéro code-behind ajouté. Build vert.
- **Task 2 — Smoke [WpfFact].** `CadranBindingTests` construit `MainWindow` dans 4 états sans crash :
  exact, estimé (Utilization null → converter neutre, aucune exception), indisponible
  (`UsageSnapshot.Empty` → `DataUnavailable`, ROB-01), et fiabilité MIXTE prouvant que les badges
  « estimée » sont indépendants par fenêtre (`FiveHour.IsEstimated == false` ET
  `SevenDay.IsEstimated == true`). Suite complète à 68 tests verts (0 régression sur les 64 existants).
- **Task 3 — Checkpoint visuel auto-vérifié (mode autonome).** Build + suite (68/68) verts ; smoke run
  de `Chronos.exe` (~8 s) : Host démarré, fenêtre affichée, aucun crash. Les critères purement visuels
  (fidélité maquette, sens de vidage des arcs, distinction des deux nuances, progression temps réel)
  persistés dans `05-HUMAN-UAT.md` (6 tests pending) pour validation humaine ultérieure.

## Task Commits

1. **Task 1 : composition cadran + tokens** — `1d8bd23` (feat)
2. **Task 2 : smoke [WpfFact] + extraction DesignTokens.xaml** — `a9c4606` (test)
3. **Task 3 : checkpoint auto-vérifié + UAT** — `f2201e1` (docs)

## Files Created/Modified

- `src/Chronos/Resources/DesignTokens.xaml` *(créé)* — ResourceDictionary autonome : 9 SolidColorBrush
  de tokens verrouillés + `UtilizationToBrushConverter` (clé `UtilBrush`) + `BooleanToVisibilityConverter`
  (clé `BoolToVis`).
- `src/Chronos/App.xaml` *(modifié)* — `Application.Resources` merge `DesignTokens.xaml` (source unique
  globale).
- `src/Chronos/Views/MainWindow.xaml` *(modifié)* — placeholder remplacé par le cadran complet ;
  `Window.Resources` merge aussi `DesignTokens.xaml` (vue auto-suffisante).
- `tests/Chronos.Tests/CadranBindingTests.cs` *(créé)* — 4 `[WpfFact]` (exact / estimé / indisponible /
  fiabilité mixte), `Measure`/`Arrange` + assertions `FindName` et surface VM.

## Decisions Made

- **`DesignTokens.xaml` autonome, mergé App + Window.** Extraire tokens/converters dans un dictionnaire
  autonome référencé à la fois par `App.xaml` (global) et par `Window.Resources` rend la vue
  auto-suffisante : ses `StaticResource` résolvent en smoke test sans exécuter `App.OnStartup` et sans
  mutation cross-thread d'un `Application.Current` partagé entre threads STA de test. Source unique de
  vérité préservée (un seul fichier de tokens).
- **Deux nuances secondaires, deux usages.** `#C7C6D0` (clair) → countdown hebdo ; `#A9A8B2` (sourd) →
  badges/mentions annexes. Aucune nuance verrouillée orpheline.
- **Signaux PAR FENÊTRE.** `FiveHour.IsEstimated/Exhausted` et `SevenDay.IsEstimated/Exhausted` bindés
  séparément (le composite choisit la meilleure source par fenêtre) ; `IsStale` global en mention
  secondaire dédiée.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Extraction des tokens dans un ResourceDictionary autonome + merge au niveau Window**
- **Found during:** Task 2 (smoke test).
- **Issue:** Le plan spécifiait les tokens/converters inline dans `Application.Resources` (App.xaml).
  Les `{StaticResource}` du cadran ne résolvent PAS quand le smoke test construit `MainWindow` : le host
  de test n'exécute pas `App.OnStartup`, donc `Application.Resources` n'est pas chargé. Une première
  tentative (charger le dictionnaire dans `Application.Current.Resources` côté test) a échoué à cause de
  la mutation cross-thread d'un `Application` singleton partagé entre les threads STA de `Xunit.StaFact`.
- **Fix:** Tokens/converters extraits dans `src/Chronos/Resources/DesignTokens.xaml` (dictionnaire
  autonome), mergé par `App.xaml` (production, source globale) ET par `MainWindow.xaml`
  (`Window.Resources`) → la vue est auto-suffisante et testable sans aucun setup de resources.
- **Files modified:** `src/Chronos/Resources/DesignTokens.xaml` (créé), `src/Chronos/App.xaml`,
  `src/Chronos/Views/MainWindow.xaml`.
- **Commit:** `a9c4606`.

**2. [Rule 3 - Blocking] Alias de type pour lever l'ambiguïté WindowState dans le test**
- **Found during:** Task 2.
- **Issue:** `using System.Windows;` (requis pour `Size`/`Rect`) rend `WindowState` ambigu entre
  `Chronos.Models.WindowState` et `System.Windows.WindowState` → erreur CS0104.
- **Fix:** Ajout de `using WindowState = Chronos.Models.WindowState;` dans le test.
- **Files modified:** `tests/Chronos.Tests/CadranBindingTests.cs`.
- **Commit:** `a9c4606`.

**Total deviations:** 2 (toutes Rule 3, résolues automatiquement — aucune divergence fonctionnelle du
livrable prévu).
**Impact on plan:** aucun sur le comportement ; la seule différence de FORME est la localisation des
tokens (fichier `DesignTokens.xaml` dédié plutôt qu'inline App.xaml), qui renforce la testabilité tout
en conservant la source unique de vérité voulue par le plan.

## Issues Encountered

- Le smoke test échouait initialement (StaticResource non résolue puis mutation cross-thread) ; résolu
  par l'extraction/merge décrite ci-dessus. Vérifié en isolation (`--filter Cadran` et
  `--filter OverlayWindowConfig` séparément) ET en suite complète (68/68), pour écarter toute fragilité
  d'ordonnancement des tests.

## Known Stubs

None — tous les TextBlock/arcs sont bindés sur des propriétés réelles du `MainViewModel` (Phase 4).
Aucune valeur codée en dur ni placeholder de données. Le placeholder visuel `#CC1E1E1E` a été retiré.

## User Setup Required

None — aucun service externe. La validation VISUELLE humaine (fidélité maquette + temps réel) reste
recommandée : voir `05-HUMAN-UAT.md` (6 tests pending, non bloquants).

## Next Phase Readiness

- Le cadran est visible et temps réel : cœur observable de la Phase 5 livré. Prêt pour la Phase 6
  (comportements overlay : drag/accroche, menu, persistance) qui s'appuiera sur cette fenêtre sans la
  remodeler.
- Aucun blocage. Validation visuelle humaine à recueillir en conditions réelles (non bloquante pour la
  vérification programmatique).

---
*Phase: 05-cadran-ringarc-converters-c-blage-view*
*Completed: 2026-07-08*

## Self-Check: PASSED

- Tous les fichiers créés/modifiés vérifiés sur disque (DesignTokens.xaml, CadranBindingTests.cs,
  App.xaml, MainWindow.xaml, 05-HUMAN-UAT.md, 05-03-SUMMARY.md).
- Les 3 commits de tâche vérifiés dans l'historique git (1d8bd23, a9c4606, f2201e1).
