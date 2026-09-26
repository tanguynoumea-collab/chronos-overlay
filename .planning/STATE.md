---
gsd_state_version: 1.0
milestone: v1.8
milestone_name: — Historique d'utilisation
status: defining requirements
stopped_at: Milestone v1.8 started — requirements defined, roadmap to create
last_updated: "2026-09-27T00:00:00.000Z"
last_activity: 2026-09-27
progress:
  total_phases: 0
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-09-27)

**Core value:** Voir instantanément, sans terminal ni /usage, combien de quota et de temps il reste — sans
jamais présenter une estimation comme un chiffre exact. Et savoir quelle session m'attend. v1.8 : comprendre
sa façon d'utiliser Claude au cours du temps, avec la même honnêteté.
**Current focus:** Milestone v1.8 — Historique d'utilisation (cycle ZEUS n°1, `.zeus/state.json`)

## Current Position

Milestone: v1.8 — Historique d'utilisation
Phase: Not started (roadmap to create — phases 32 à 35, numérotation continue)
Plan: —
Status: Defining requirements → roadmap
Last activity: 2026-09-27 — Milestone v1.8 started

Progress: [░░░░░░░░░░] 0 %

## Accumulated Context

### Décisions de l'utilisateur (2026-09-27, checkpoint humain 1 du cycle ZEUS)
- Forme A (Pistes) retenue ; B (Simplifié) et C (Tuiles) codées aussi et sélectionnables.
- Vues Jour et 4 semaines conservées dans v1.8.
- Deux gestes d'ouverture : bouton des réglages + double-clic au centre du cadran.
- La phase « compter juste + journal des relevés » se publie SEULE en 3.2.2 avant toute interface.

### Blockers / dettes ouvertes
- **Constat en production v1.7 PARTIEL** (VAL-03) : trois exécutables en marche (3.1.0, 3.2.0, 3.2.1), tableau des gestes non
  joué → repris en tête de la phase 32 (VAL-04) sur la 3.2.2. L'agent ne lance, n'arrête ni ne clique jamais l'overlay.
- **`last-exact.json` figé** depuis le 2026-09-13 12:44 (Save dans un try/catch muet) → CPT-02.
- **Parser de tokens ×2 à 2,75** (lignes `assistant` dupliquées par bloc, même `usage`) → CPT-01.

### Sécurité — contrainte qui prime sur tout
Ne JAMAIS appeler l'endpoint de refresh OAuth avec le refresh token réel de `%APPDATA%\Chronos\oauth.dat`. Ne JAMAIS écrire
dans `%APPDATA%\Claude\` ni dans `~/.claude/projects`. Lecture seule stricte.

## Contexte technique v1.8 (relevé DÉJÀ FAIT les 26 et 27/09 — ne pas re-enquêter)

**Sources.** Sonde d'en-têtes `anthropic-ratelimit-unified-*` : 1 relevé exact / 5 min (`CadenceNominale` 300 s) mais
`RefreshOrchestrator` tourne toutes les 60 s et ressert le même relevé 5 fois sur 6 → dédup par `CapturedAt` strictement
croissant obligatoire. `utilization` en fraction 0..1 texte (précision constatée 0,01 ; à vérifier finement avec le journal).
`resets_at` 7 j = samedi 00:00 heure locale (2026-09-18T22:00Z), `WeeklyAnchor` 2026-07-11T00:00+02:00 ; `WeeklyWindow`
avance par 7 × 24 h fixes → dérivera d'une heure le 25/10/2026 (à trancher avec le journal). `DayTimeline` suppose des resets
toutes les 5 h (vrai seulement en enchaînement continu) : la grille doit venir des resets observés.

**Transcripts** (`~/.claude/projects/**/*.jsonl`, sous-agents dans `<session>/subagents/agent-*.jsonl`) : 1 565 fichiers,
2,2 Go, du 2026-06-23 à aujourd'hui, juillet purgé (cleanupPeriodDays), 1 480 fichiers de sous-agents, plus gros fichier 53 Mo.
**Une ligne `assistant` par bloc de contenu, même `message.usage` recopié** (3 248 lignes / 1 183 `message.id` sur le plus gros
fichier ; usage identique entre doublons, 0 divergent) → dédup par `message.id` (repli `requestId`) par fichier.
`TranscriptActivityProvider` fait une passe complète bornée à 8 jours (536 Mo → 2,7–3,2 s) ; **aucune lecture par offsets
n'existe** (`TranscriptSessionSource` fait un `Seek` de queue) : les curseurs sont à construire. Balayage `grep` de 2 Go : 3,8 s
cache chaud ; C# en flux avec pré-filtre : 10–20 s CPU chaud estimés.

**Persistance existante** : `LastExactStore` (temp + `File.Move`, lecture tolérante, `SchemaVersion`) est le modèle maison.
`ChronosPaths` construit les chemins sous `%APPDATA%\Chronos`. `ServicesLayerPurityTests` interdit tout type WPF sous
`Services/` et `Models/`. AppData est VIRTUALISÉ (MSIX) pour tout ce qui tourne sous l'app bureau : l'overlay lancé par
l'Explorateur écrit la vue réelle, un processus lancé depuis une session écrit `Packages\Claude_*\LocalCache\Roaming` —
observer avec une sonde hors de l'arbre (WMI), jamais depuis une session.

**UI existante** : plus de menu contextuel — clic droit = `SettingsWindow` (330 px, palette locale Panel/Panel2/Line/Ink/Ink2/
Accent/Ok à promouvoir dans `DesignTokens.xaml`) ; clic centre = bascule % / temps (`CentreHit`) ; cadran 170 × 170 layered.
Tokens existants : `Piste5h`, `PisteHebdo`, `TickReset` (#F4F2EC), `TexteSecondaire`, `Alerte`, `UtilBrush` (rampe du thème :
Aurore #4FD1C5 → #6D8CF0 → #C86BE0). Police Segoe UI.

**Artefacts du cycle** : brief et rapport du conseil (`.zeus/reports/llm-council-2026-09-26*.md`), plan de design validé
(`.zeus/DESIGN_PLAN.md`), maquettes Figma https://www.figma.com/design/O8WVDejfdPcJv6314a7h6k (A retenue, B, C, D Jour,
E 4 semaines, F1 carte des réglages, F2 bandeau), DoD `.zeus/DOD.md`, état `.zeus/state.json`.

## Performance Metrics

- Suite de tests à l'entrée de v1.8 : **1200 verts / 0 échec** (fin de v1.7, `0bd27cb`), ≈ 10 s.
- Exe courant : `Chronos-v3.2.1.exe` (77 385 116 o, md5 `3a1dc26f…`). Cibles : 3.2.2 (après la phase 32), 3.3.0 (fin de milestone).
- Historique : v1.6 = 6 phases, 19 plans, 110 commits, 752 → 889 tests ; v1.7 = 6 phases, 20 plans, 125 commits, 896 → 1200 tests.

## Milestones clos

v1.7 clos le 2026-09-27 (écart connu, voir `.planning/MILESTONES.md` › Known Gaps). Détail : `.planning/milestones/v1.7-*.md`,
`.planning/RETROSPECTIVE.md`.
