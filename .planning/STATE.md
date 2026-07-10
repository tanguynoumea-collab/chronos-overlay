---
gsd_state_version: 1.0
milestone: v1.4
milestone_name: "— Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code)"
status: roadmap_ready
stopped_at: Roadmap v1.4 créé (phases 13-14)
last_updated: "2026-07-10"
last_activity: 2026-07-10
progress:
  total_phases: 2
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-10)

**Core value:** Voir instantanément, sans terminal ni `/usage`, combien de quota et de temps il reste sur les deux fenêtres — sans jamais présenter une estimation comme un chiffre exact.
**Current focus:** Phase 13 — Source UIA app bureau (première phase de v1.4)

## Current Position

Milestone: v1.4 — Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code)
Phase: 13 — Source UIA app bureau (not started)
Plan: —
Status: Roadmap prêt — en attente de `/gsd:plan-phase 13`
Last activity: 2026-07-10 — Roadmap v1.4 créé (2 phases, 11 requirements mappés)

Progress: [░░░░░░░░░░] 0% (0/2 phases)

## Performance Metrics

**Velocity:**

- Total plans completed (v1.4): 0
- Average duration: —
- Total execution time: 0 h

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 13 | 0/TBD | - | - |
| 14 | 0/TBD | - | - |

*Updated after each plan completion*

## Accumulated Context

### Decisions

- [v1.4/roadmap]: **2 phases à dépendance forte** — la source UIA (Phase 13) avant l'hystérésis (Phase 14).
  La distinction Chat/Cowork/Code et l'acquittement par focus (NET-02) EXIGENT l'arbre UIA ; la branche
  « répondu » (NET-01) roule déjà sur les sources actuelles. On ne peut donc pas inverser l'ordre.
- [v1.4/roadmap]: Phase 13 = BUR-01..05 + ROB-06 + ROB-07 (7 req) ; Phase 14 = NET-01..04 (4 req).
- [v1.4/roadmap]: Les deux phases sont **UI hint: yes** (surface visuelle WPF du widget sessions).

### Contexte technique (déjà établi — ne pas re-rechercher)

- **Spike UIA PROUVÉ (2026-07-10)** sur la vraie machine : l'app Electron/Chromium expose son arbre
  d'accessibilité complet. Matcher par **ControlType + Name** (PAS par `AutomationId` volatils) ; Names
  **localisés fr**. Signaux vérifiés :
  - Text « Claude répond. » / Button « Arrêter » = **Working**.
  - Button « Ignorer les permissions » = **attend permission**.
  - Text « Mode chat » + placeholder « Tapez / pour les commandes » = **repos** (attend ton message).
  - Onglets Home/Code + panneaux Terminal/Diff/Aperçu/Actions de session/Contrôle à distance.
  - Sidebar : Button « En cours d'exécution <nom> » par session active.
  - Cowork-VM = état distant **non observable** → indéterminé.
- **MANQUE** : un snapshot en état **REPOS** (le spike a été pris pendant une génération). À capturer en
  tout début de Phase 13 pour figer la représentation exacte du « m'attend » premier plan.
- **Code existant** :
  - `src/Chronos/Services/SessionSnapshot.cs` — record neutre + enum `SessionActivity`
    (Working / WaitingAttention / WaitingTurn / Unknown).
  - `src/Chronos/Services/SessionMonitor.cs` — `Read(now)` fusionne transcripts + hooks, applique la
    staleness, filtre `archived` via `ArchiveStore`. **Le filtre « traitées » (Phase 14) se branche ici,
    au même endroit que le filtre archived.**
  - `src/Chronos/Services/TranscriptSessionSource.cs`, `ArchiveStore`.
  - `src/Chronos/ViewModels/SessionsViewModel.cs` — `Refresh`, timer 2 s.
  - `tests/Chronos.Tests/SessionsTests.cs`.
- **Nouveau (Phase 13)** : `DesktopUiaSessionSource` (nouvelle `ISessionSource`), lecture hors thread UI
  puis marshalling, élément racine mis en cache, cadence ~1-2 s.
- **Contraintes** : `System.Windows.Automation` (interop COM managé, aucune dépendance native de rendu,
  pas d'admin) ; `%USERPROFILE%`/`%APPDATA%` uniquement ; honnêteté (indéterminé, jamais une inférence
  présentée comme exacte) ; lecture tolérante (aucune source ≠ crash) ; UI/commentaires en français.

### Pending Todos

- Phase 13 (tout début) : capturer le snapshot UIA en état **repos** avant de coder le mapping d'états.

### Blockers/Concerns

- Le mapping d'états UIA dépend de Names **localisés** ; prévoir la table fr/en (ROB-06) dès la Phase 13
  pour ne pas coder en dur des libellés qui changent à une MAJ de l'app.
- Débounce du focus (NET-02, Phase 14) : caler ~2-3 s pour distinguer un vrai acquittement d'un survol.

## Session Continuity

Last session: 2026-07-10
Stopped at: Roadmap v1.4 créé (phases 13-14) — prêt pour `/gsd:plan-phase 13`
Resume file: None
