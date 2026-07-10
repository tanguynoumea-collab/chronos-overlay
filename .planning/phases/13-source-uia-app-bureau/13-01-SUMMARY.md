---
phase: 13-source-uia-app-bureau
plan: 01
subsystem: sessions
tags: [uia, accessibility, session-detection, fr-en-labels, neutral-dto, tdd]

# Dependency graph
requires:
  - phase: 12 (widget sessions)
    provides: "SessionSnapshot / SessionActivity / SessionMonitor / TranscriptSessionSource (pipeline sessions CLI)"
provides:
  - "SessionSnapshot étendu (Kind/Origin) — non cassant, défauts Unknown/Cli"
  - "Seams neutres UiaNode (DTO arbre a11y) + IUiaTreeProvider + ISessionSource"
  - "UiaLabels: table de libellés fr/en extensible + matching tolérant (Matches/StartsWithAny)"
  - "DesktopUiaSessionSource: MapTree pur (ancre RootWebArea, états honnêtes, type, sidebar) + DesktopHealth (repli tracé) + cache non bloquant"
affects: [phase-13-plan-02 (WindowsUiaTreeProvider réel + câblage DI/SessionMonitor), phase-13-plan-03, phase-14 (hystérésis / traitées)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Seam injectable (IUiaTreeProvider) isolant l'I/O OS derrière un DTO neutre testable par faux arbre"
    - "Fonction pure statique (MapTree) séparée de l'état/cache (Poll/Read) — testabilité maximale"
    - "Table de libellés externalisée fr/en, extensible sans toucher au code de logique (ROB-06)"
    - "Santé tracée (DesktopHealth) distinguant WindowMissing / AnchorMissing / Ok — pas de zéro silencieux"

key-files:
  created:
    - src/Chronos/Services/UiaNode.cs
    - src/Chronos/Services/IUiaTreeProvider.cs
    - src/Chronos/Services/ISessionSource.cs
    - src/Chronos/Services/UiaLabels.cs
    - src/Chronos/Services/DesktopUiaSessionSource.cs
    - tests/Chronos.Tests/DesktopUiaSessionSourceTests.cs
  modified:
    - src/Chronos/Services/SessionSnapshot.cs

key-decisions:
  - "Kind/Origin ajoutés EN FIN du record avec défauts (Unknown/Cli) → aucun usage positionnel existant cassé"
  - "Ancre foreground reconnue par UiaNode.AutomationId == \"RootWebArea\" (rôle a11y stable) — SEULE exception au principe « ne pas matcher par AutomationId »"
  - "Cowork VM (Contrôle à distance) forcé Activity=Unknown, quelle que soit l'inférence (BUR-05: état distant non observable)"
  - "Repli « fenêtre présente / ancre absente » tracé via Health=AnchorMissing, distinct de WindowMissing (app fermée)"
  - "MapTree PURE + Poll/Read (cache volatile) → lecture non bloquante pour le thread UI (fondation ROB-07)"

patterns-established:
  - "Faux arbre UIA (FakeUiaNode + Fenetre) pour prouver toute la logique sans fenêtre Claude réelle"
  - "Matching tolérant: trim + OrdinalIgnoreCase, null-safe, jamais d'exception"

requirements-completed: [BUR-02, BUR-03, BUR-04, BUR-05, ROB-06]

# Metrics
duration: 6min
completed: 2026-07-10
---

# Phase 13 Plan 01 : Fondation testable de la source UIA app bureau — Summary

**Toute la logique de détection des sessions de l'app bureau Claude (états honnêtes, type Chat/Code/Cowork, énumération sidebar, ancre RootWebArea, dégradation tracée) posée derrière un DTO neutre et prouvée par 28 tests sur faux arbre — sans aucune dépendance à Windows UI Automation ni fenêtre Claude réelle.**

## Performance

- **Duration:** ~6 min
- **Started:** 2026-07-10T12:22:43Z
- **Completed:** 2026-07-10T12:28:20Z
- **Tasks:** 3/3
- **Files modified/créés:** 7 (1 étendu, 6 neufs)

## Accomplishments
- Modèle `SessionSnapshot` étendu (Kind/Origin) sans casser aucun usage CLI existant (défauts Unknown/Cli).
- Seams neutres en place : `UiaNode` (DTO arbre a11y), `IUiaTreeProvider` (accès OS isolé, ne lève jamais), `ISessionSource` (contrat commun).
- `UiaLabels` : table fr/en extensible (Responding, StopButton, PermissionButton, ChatMode, ChatPlaceholder, RunningPrefix, RemoteControl, CodePanels) + helpers de matching tolérant.
- `DesktopUiaSessionSource.MapTree` PURE : ancre `RootWebArea`, états Working/WaitingAttention/WaitingTurn/Unknown, type Chat/Code/Cowork, Cowork VM forcé Unknown (BUR-05), sidebar énumérée (BUR-04), clés synthétiques stables, dédup.
- `DesktopHealth` : repli TRACÉ (WindowMissing ≠ AnchorMissing ≠ Ok), jamais de zéro silencieux.
- Cache `Poll`/`Read` : lecture non bloquante (fondation ROB-07), aucune I/O UIA dans le chemin `Read`.
- 28 tests neufs, suite complète verte (294 tests), pureté Services préservée (aucun assembly WPF).

## Task Commits

Chaque tâche committée atomiquement :

1. **Task 1: SessionSnapshot étendu + seams neutres (UiaNode/IUiaTreeProvider/ISessionSource)** — `dbc54fe` (feat)
2. **Task 2: Table de libellés fr/en UiaLabels + matching tolérant testé** — `7e663ea` (feat)
3. **Task 3: DesktopUiaSessionSource — MapTree pur + ancre RootWebArea + santé + cache** — `ab37033` (feat)

_Note : les 3 tâches sont TDD ; les tests ont été écrits avec l'implémentation puis exécutés verts à chaque étape._

## Files Created/Modified
- `src/Chronos/Services/SessionSnapshot.cs` — enums `SessionKind`/`SessionOrigin` + record étendu (Kind/Origin en fin, défauts non cassants).
- `src/Chronos/Services/UiaNode.cs` — DTO neutre d'arbre a11y (AutomationId porteur de l'ancre RootWebArea).
- `src/Chronos/Services/IUiaTreeProvider.cs` — seam `TryGetTree()`, ne lève jamais.
- `src/Chronos/Services/ISessionSource.cs` — contrat commun `Read(now)`.
- `src/Chronos/Services/UiaLabels.cs` — table fr/en + `Matches` / `StartsWithAny`.
- `src/Chronos/Services/DesktopUiaSessionSource.cs` — `MapTree` pur, `DesktopHealth`, `Poll`/`Read` (cache volatile).
- `tests/Chronos.Tests/DesktopUiaSessionSourceTests.cs` — 28 tests via faux arbre.

## Deviations from Plan

Aucune déviation de logique. Un point de contexte à noter :

- **Étape 0 de Task 2 (re-dump UIA état REPOS) non exécutée en live.** Le script `scratchpad/uia-spike.ps1` référencé par le plan n'existe pas dans le repo, et aucune fenêtre Claude à l'état repos n'était disponible/dumpable au moment de l'exécution. Conformément à la consigne (« si l'app est occupée/indisponible, NE PAS bloquer »), la détection s'appuie sur le matching tolérant + la table fr/en extensible. Les littéraux `ChatMode` = « Mode chat » et `ChatPlaceholder` = « Tapez / pour les commandes » proviennent de la mémoire projet `chronos-desktop-uia.md` (spike du 2026-07-10) et restent extensibles sans toucher au code. **Point à re-vérifier** (tracé ici et dans la mémoire) : la représentation exacte du bouton d'envoi quand « Claude répond. » a disparu — non critique car `WaitingTurn` est dérivé de `ChatMode` + placeholder, pas du bouton d'envoi.

## Known Stubs

Aucun stub. `WindowsUiaTreeProvider` (implémentation réelle de `IUiaTreeProvider`) et le câblage DI/`SessionMonitor` sont HORS périmètre de ce plan (Plan 02, par conception) — pas des stubs mais des artefacts d'une phase ultérieure. Toute la logique livrée ici est complète et testée.

## Verification
- `dotnet build src/Chronos/Chronos.csproj` : réussi (0 avertissement, 0 erreur).
- `dotnet test tests/Chronos.Tests` : **294 réussis / 0 échec** (215+ existants + 28 nouveaux DesktopUiaSessionSourceTests).
- `ServicesLayerPurityTests.La_couche_Services_ne_reference_aucun_assembly_WPF` : vert (nouveaux types Services neutres).
- Aucun fichier de Plan 02/03 modifié (WindowsUiaTreeProvider, SessionMonitor, App.xaml.cs, SessionsViewModel intacts).

## Self-Check: PASSED

- 7/7 fichiers créés/modifiés présents sur disque.
- 3/3 commits de tâche présents dans l'historique (dbc54fe, 7e663ea, ab37033).
