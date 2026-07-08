---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: verifying
stopped_at: Completed 07-01-PLAN.md
last_updated: "2026-07-08T20:39:41.248Z"
last_activity: 2026-07-08
progress:
  total_phases: 7
  completed_phases: 7
  total_plans: 18
  completed_plans: 18
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-08)

**Core value:** Voir instantanément, sans terminal ni `/usage`, combien de quota et de temps il reste sur les deux fenêtres — sans jamais présenter une estimation comme un chiffre exact.
**Current focus:** Phase 07 — packaging-d-ploiement

## Current Position

Phase: 07
Plan: Not started
Status: Phase complete — ready for verification
Last activity: 2026-07-08

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:**

- Total plans completed: 0
- Average duration: —
- Total execution time: 0 h

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| - | - | - | - |

**Recent Trend:**

- Last 5 plans: —
- Trend: —

*Updated after each plan completion*
| Phase 01-fondations-architecture-squelette-overlay P01 | 4 | 3 tasks | 14 files |
| Phase 01-fondations-architecture-squelette-overlay P02 | 3min | 2 tasks | 8 files |
| Phase 02-d-couverte-des-sources-bloquante P01 | 3min | 2 tasks | 1 files |
| Phase 03 P01 | 5min | 3 tasks | 11 files |
| Phase 03 P02 | 4min | 2 tasks | 7 files |
| Phase 03 P04 | 5min | 2 tasks | 3 files |
| Phase 03 P03 | 6min | 3 tasks | 9 files |
| Phase 04-orchestration-refresh-viewmodel-temps-r-el P01 | 18min | 2 tasks | 5 files |
| Phase 04-orchestration-refresh-viewmodel-temps-r-el P02 | 5min | 3 tasks | 9 files |
| Phase 05 P01 | 3 min | 2 tasks | 4 files |
| Phase 05 P02 | 2 min | 3 tasks | 4 files |
| Phase 05-cadran-ringarc-converters-c-blage-view P03 | 9 min | 3 tasks | 4 files |
| Phase 06 P02 | 4 min | 3 tasks | 6 files |
| Phase 06-comportements-overlay-placement-interaction P01 | 4min | 3 tasks | 10 files |
| Phase 06-comportements-overlay-placement-interaction P03 | 4min | 3 tasks | 9 files |
| Phase 06-comportements-overlay-placement-interaction P04 | 8min | 3 tasks | 16 files |
| Phase 07 P01 | 3min | 3 tasks | 3 files |

## Accumulated Context

### Decisions

Décisions consignées dans PROJECT.md Key Decisions. Affectant le travail actuel :

- [Phase 2]: Découverte de source (docs/data-sources.md) préalable bloquant AVANT tout code de provider.
- [Phase 3]: Abstraction IUsageProvider isole les sources non documentées du cadran ; provenance Exact/Estimated portée dans le snapshot.
- [Phase 1]: Overlay net8.0-windows, DI dans App.xaml.cs (pas de StartupUri), Topmost réaffirmé sans vol de focus.
- [Phase 01-fondations-architecture-squelette-overlay]: Solution en format .sln classique (--format sln) : le SDK .NET 10 génère .slnx par défaut
- [Phase 01-fondations-architecture-squelette-overlay]: [Phase 1]: ROB-04 livré — Topmost réaffirmé par SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE) sur DispatcherTimer 2s dédié, sans vol de focus.
- [Phase 02-d-couverte-des-sources-bloquante]: Source primaire = bloc rate_limits du contrat statusLine (Fiable), consommé via un pont statusLine → fichier ; l'objet d'usage n'est persisté dans aucun fichier disque.
- [Phase 02-d-couverte-des-sources-bloquante]: Champ réel used_percentage (0..100), PAS utilization (0..1) ; resets_at = epoch secondes → DateTimeOffset.FromUnixTimeSeconds. Repli JSONL marqué Estimé.
- [Phase 03]: Modèles nullable-safe : null = inconnu, jamais inventé ; Exhausted dérivé ; FractionRemaining clampée [0..1] prend now en paramètre (modèles purs testables sans IClock).
- [Phase 03]: Garde de pureté WPF (ServicesLayerPurityTests) avec allow-list nominative des adaptateurs Phase 1 (WpfUiDispatcher, TopmostGuard) — Models/Services partagent l'assembly de l'app WPF donc contrôle par signature de type, pas au niveau assembly.
- [Phase 03]: Pont statusLine non destructif : ecriture usage.json atomique (renameSync) AVANT de relancer gsd-statusline.js ; capturedAt en epoch ms, resets_at en epoch s.
- [Phase 03]: ClaudeUsageObjectProvider = source primaire Exact, lecture tolerante (fichier absent/corrompu -> Empty, fenetre/champ absent -> Unavailable/null, jamais d'exception).
- [Phase 03]: Pont statusLine DEPLOYE via installeur idempotent non destructif (install-bridge.mjs) : backup .chronos.bak non ecrasant, chainage verifie de gsd-statusline.js, ecriture atomique de la seule cle statusLine, --uninstall reversible. Deploiement verifie programmatiquement (usage.json alimente + barre re-emise intacte).
- [Phase 03]: Repli JSONL : scan recursif AllDirectories inclut subagents/ dans la somme de tokens (meme pool de quota, arbitrage phase 3, aucun filtre) ; estimation toujours Estimated, Utilization/ResetsAt null (jamais invente).
- [Phase 03]: CompositeUsageProvider bascule PAR FENETRE (Exact>Estimated>Unavailable) ; IUsageProvider resout le composite en DI Singleton (App.xaml.cs) sans casser Phase 1.
- [Phase 04-orchestration-refresh-viewmodel-temps-r-el]: RefreshOrchestrator (BackgroundService neutre) expose SnapshotChanged ; watcher débouncé + PeriodicTimer alimentent un Channel(1, DropWrite) à consommateur unique sérialisant GetAsync.
- [Phase 04-orchestration-refresh-viewmodel-temps-r-el]: await Task.Yield() en tête d'ExecuteAsync : évite que StartAsync bloque quand la boucle traite le 1er déclencheur inline.
- [Phase 04-orchestration-refresh-viewmodel-temps-r-el]: MainViewModel : marshaling unique via IUiDispatcher.Post (RAF-04) + Interpolate(now) pur sans I/O (RAF-03) ; DispatcherTimer cree cote UI (StartClock) hors ctor.
- [Phase 05]: ArcGeometry: fraction >= 1 -> EllipseGeometry (anneau plein sans micro-fente) au lieu de clamp 359.9 ; isLargeArc = sweep > 180.0 stricte ; RampColor AmberStop = 0.55 (constante unique ajustable UAT). Math pure isolee dans Rendering/, testee en [WpfFact]/[Fact].
- [Phase 05]: RingArc/TickRing derivent de Shape (pas UserControl) : geometrie = pur produit des DP AffectsRender, redessin auto au tick 1s sans animation ; DP Fraction 0..1 (pas EndAngle) pour binding direct. UtilizationToBrushConverter mono-entree : null -> neutre #2A2932 (jamais de couleur inventee), >=1 -> gris epuise #5A5960, [0,1[ -> rampe.
- [Phase 05-cadran-ringarc-converters-c-blage-view]: [Phase 05]: Cadran compose en XAML pur (bindings + converters, zero code-behind) ; tokens/converters dans Resources/DesignTokens.xaml autonome, merge par App.xaml ET Window.Resources (vue auto-suffisante et testable). Signaux estimee/epuise PAR FENETRE (FiveHour/SevenDay independants), staleness globale en texte secondaire ; deux nuances #C7C6D0 (countdown hebdo) et #A9A8B2 (badges) chacune utilisee.
- [Phase 06]: Autostart .lnk cible Environment.ProcessPath (single-file-safe, jamais Assembly.Location) via COM late-bound WScript.Shell sans NuGet ; dossier startup injectable pour tests
- [Phase 06]: TopmostGuard.Suspend=_timer.Stop / Resume=_timer.Start+Reassert (pas de toggle Topmost, evite scintillement) ; NativeMethods etendu au placement physique multi-ecrans (MonitorFromWindow/GetMonitorInfo rcWork)
- [Phase 06]: Placement persiste coin+device comme verite (X/Y indicatifs) ; RefreshIntervalSeconds sans UI applique au demarrage 06-03 ; recalibrage hebdo au repli seulement restant Estimated (badge estimee conserve).
- [Phase 06]: Placement physique via SetWindowPos (rcWork moniteur courant) dans OverlayController — contourne bug WPF Window.Left/Top PerMonitorV2 ; snap au retour de DragMove ; restauration coin+device en SourceInitialized (avant 1er rendu), repli primaire si device disparu.
- [Phase 06-comportements-overlay-placement-interaction]: Menu contextuel = SEUL point d'acces/sortie : ContextMenu 4 items sur la Grid racine (DataContext herite) + 4 [RelayCommand] VM (ToggleBackground/Recalibrate/ToggleAutostart/Quit).
- [Phase 06-comportements-overlay-placement-interaction]: Recalibrage hebdo applique dans ApplySnapshot (re-application du dernier snapshot memorise), badge estimee conserve ; IRecalibrationPrompt neutre en Services, impl WPF (dialogue DatePicker + caler sur maintenant) en Views hors garde de purete.
- [Phase 07]: Packaging DEP-01 : exe self-contained mono-fichier win-x64 (~74 Mo), 8 props publish conditionnées au publish (build/debug restent normaux), PublishTrimmed=false non négociable (WPF non trim-safe), autostart Environment.ProcessPath avec limite déplacement documentée.

### Pending Todos

None yet.

### Blockers/Concerns

- Source Claude non documentée (MEDIUM confidence) : localisation exacte de l'objet d'usage à établir empiriquement en Phase 2 — seul vrai inconnu du projet. Research flag : Phase 2 nécessite probablement /gsd:research-phase.
- Décision clic-traversant v1 vs v2 (conflit avec le drag) à trancher explicitement lors de la planification de la Phase 6, même si l'implémentation reste différée en v2.

## Session Continuity

Last session: 2026-07-08T20:35:36.277Z
Stopped at: Completed 07-01-PLAN.md
Resume file: None
