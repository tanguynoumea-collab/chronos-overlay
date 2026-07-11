---
gsd_state_version: 1.0
milestone: v1.4
milestone_name: — Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code)
status: "v1.4 VALIDÉ en UAT app-réelle (2026-07-11) : sessions bureau (BUR/NET/ROB) + nombreux correctifs de test réel. Feature cadran deux modes (Normal/Étendu) ajoutée en direct (hors phases GSD). 327 tests verts."
stopped_at: v1.4 validé (UAT app-réelle) + cadran deux modes
last_updated: "2026-07-11"
last_activity: 2026-07-11 — UAT app-réelle (correctifs sessions) + feature cadran deux modes ; 327 tests verts, tout committé sur main (non poussé)
progress:
  total_phases: 2
  completed_phases: 2
  total_plans: 5
  completed_plans: 5
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-10)

**Core value:** Voir instantanément, sans terminal ni `/usage`, combien de quota et de temps il reste sur les deux fenêtres — sans jamais présenter une estimation comme un chiffre exact.
**Current focus:** v1.4 VALIDÉ en UAT app-réelle + feature cadran deux modes livrée. Prêt à shipper (versionner l'exe + push) au feu vert de l'utilisateur.

## Current Position

Milestone: v1.4 — Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code) — VALIDÉ
Phase: 13 + 14 terminées ; correctifs UAT app-réelle appliqués (voir git) ; cadran deux modes ajouté en direct
Plan: —
Status: **v1.4 validé en UAT app-réelle le 2026-07-11.** Sessions bureau (BUR/NET/ROB) opérationnelles + série de correctifs trouvés en test réel (faux positif permission, priorité de type, walk UIA trop peu profond, ControlType non normalisé, pollution par le texte des messages, stabilité inter-modes, dédoublonnage, foreground Cowork supprimé, dernier état conservé). BONUS hors GSD : feature cadran **deux modes** (Normal épuré par défaut / Étendu), bascule dans les réglages, sous-tirets alignés sur la grille des resets 5 h, pastille session ORANGE pour les deux états d'attente. 327 tests verts, tout committé sur `main` (non poussé).
Last activity: 2026-07-11 — validation utilisateur ; « tout valider »

Progress: [██████████] 100% (2/2 plans, phase 14)

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
| Phase 13 P01 | 6 | 3 tasks | 7 files |
| Phase 13 P02 | 4min | 2 tasks | 4 files |
| Phase 13 P03 | 4min | 3 tasks | 6 files |
| Phase 14 P01 | 5m | 3 tasks | 6 files |
| Phase 14 P02 | ~2 min | 2 tasks | 3 files |

## Accumulated Context

### Decisions

- [v1.4/roadmap]: **2 phases à dépendance forte** — la source UIA (Phase 13) avant l'hystérésis (Phase 14).
  La distinction Chat/Cowork/Code et l'acquittement par focus (NET-02) EXIGENT l'arbre UIA ; la branche
  « répondu » (NET-01) roule déjà sur les sources actuelles. On ne peut donc pas inverser l'ordre.

- [v1.4/roadmap]: Phase 13 = BUR-01..05 + ROB-06 + ROB-07 (7 req) ; Phase 14 = NET-01..04 (4 req).
- [v1.4/roadmap]: Les deux phases sont **UI hint: yes** (surface visuelle WPF du widget sessions).
- [Phase 13]: [13-02] WindowsUiaTreeProvider propage AutomationId (dont l'ancre RootWebArea) → foreground reconnu en PROD, pas seulement en test. Assemblies UIA fournies implicitement par UseWPF (aucun <Reference>, 0 warning). Poll de fond via Timer .NET hors thread UI (ROB-07).
- [Phase 13]: 13-03: source bureau fusionnée dans SessionMonitor.Read (ISessionSource? optionnel, 4e arg non cassant), après transcripts+hooks, avant archived, non bloquant (ROB-07)
- [Phase 13]: 13-03: garde DI réelle (CompositionRootTests) reproduisant la sous-chaîne bureau d'App.xaml.cs → attrape un service manquant/mal ordonné qui ne planterait qu'au démarrage
- [Phase 14]: [14-01] Réversibilité NET-03 portée par le tracker (purge sur nouvel épisode d'attente), PAS par une comparaison ts>=UpdatedAt dans le filtre (fausse pour le bureau: UpdatedAt==now à chaque poll). Horodatage d'épisode maintenu par le tracker.
- [Phase 14]: [14-02] Focus premier-plan OS réel (WindowsForegroundWatch, Win32 GetForegroundWindow, titre « Claude ») injecté 7e param de SessionMonitor => branche NET-02 VIVANTE en prod. Best-effort ne lève jamais ; couche neutre (aucun HWND public). NET-01..04 tous couverts, phase 14 complète, milestone v1.4 prêt pour audit.

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

Last session: 2026-07-10T13:11:52.267Z
Stopped at: Completed 14-02-PLAN.md
Resume file: None
