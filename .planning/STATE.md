---
gsd_state_version: 1.0
milestone: v1.8
milestone_name: — Historique d'utilisation
status: v1.8 milestone complete
last_updated: "2026-10-03T12:00:00.000Z"
last_activity: 2026-10-03 -- v1.8 milestone completed and archived (avec écarts connus)
progress:
  total_phases: 4
  completed_phases: 4
  total_plans: 28
  completed_plans: 26
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-10-03)

**Core value:** Voir instantanément, sans terminal ni /usage, combien de quota et de temps il reste — sans
jamais présenter une estimation comme un chiffre exact. Et savoir quelle session m'attend ; et comprendre sa façon
d'utiliser Claude au cours du temps, avec la même honnêteté.
**Current focus:** Planning next milestone — milestone v1.8 clos, prochain milestone à définir (`/gsd:new-milestone`)

## Current Position

Milestone: v1.8 — Historique d'utilisation — CLOS le 2026-10-03 (avec écarts connus)
Phase: aucune en cours (phases 32 à 35 closes ; 26 / 28 plans)
Plan: —
Status: v1.8 milestone complete
Last activity: 2026-10-03 -- v1.8 milestone completed and archived

Progress: [██████████] v1.8 clos — prochain milestone à définir

> Clos avec écarts connus : les plans 32-08 (constat VAL-04) et 35-07 (constat VAL-05), `autonomous: false`, ne sont pas joués
> et sont reportés dans la phase de constat du milestone suivant. Exes publiés localement : 3.2.2, 3.3.0, 3.3.1, 3.4.0 (aucun tag
> de release, aucun push). Archive : `.planning/milestones/v1.8-ROADMAP.md`, `v1.8-REQUIREMENTS.md` ; détail des écarts :
> `.planning/MILESTONES.md` › v1.8 › Known Gaps.

## Accumulated Context

### Décisions de l'utilisateur (2026-09-27, checkpoint humain 1 du cycle ZEUS)

- Forme A (Pistes) retenue ; B (Simplifié) et C (Tuiles) codées aussi et sélectionnables.
- Vues Jour et 4 semaines conservées dans v1.8.
- Deux gestes d'ouverture : bouton des réglages + double-clic au centre du cadran.
- La phase « compter juste + journal des relevés » se publie SEULE en 3.2.2 avant toute interface.

### Blockers / dettes ouvertes

- **Constats avec l'utilisateur non joués (VAL-04, VAL-05)** — protocoles écrits dans
  `.planning/phases/32-compter-juste-puis-journaliser/32-CONSTAT.md` et `.planning/phases/35-4-semaines-acc-s-release-3-3-0/35-CONSTAT.md`
  (écart E1-ter : plusieurs exécutables en marche ; le point (a) commence par les quitter tous, à la main). Reportés dans la
  phase de constat du milestone suivant. L'agent ne lance, n'arrête ni ne clique jamais l'overlay.
- **Étapes ZEUS du cycle n°1 non jouées** (DESIGN-REVIEW de la galerie Historique, DEV-COUNCIL, DEV-SENIOR) : absorbées par le
  cycle n°2, qui couvre aussi ce code (`.zeus/state.json`).
- **Décision utilisateur** : supprimer les styles Simplifié et Tuiles de la vue Semaine au milestone suivant.

Résolus en v1.8 : parser de tokens ×2 à 2,75 (CPT-01, dédup `message.id`) ; « gel » de `last-exact.json` = vue virtualisée MSIX
(CPT-02, vue AppData au diagnostic) ; absence de verrou mono-instance (CPT-03) ; processus zombie à l'arrêt (quick 260927, 3.3.1).

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

**Persistance existante** : `last-exact.json` réel réécrit chaque minute (la vue lue depuis une session est la copie COW du paquet, figée au 13/09 — voir 32-02 : `IEtatMagasin`, diagnostic « Vue AppData ») ; `LastExactStore` (temp + `File.Move`, lecture tolérante, `SchemaVersion`) est le modèle maison.
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

- Suite de tests à la clôture de v1.8 : **1707 verts / 0 échec, 0 warning** Debug et Release (release 3.4.0, `8bec859`) ;
  1648 à la 3.3.0.
- Exe courant : `Chronos-v3.4.0.exe` (md5 `ddbe7995…`) ; précédents de v1.8 : 3.2.2, 3.3.0 (78 104 918 o), 3.3.1.
- Historique : v1.6 = 6 phases, 19 plans, 110 commits, 752 → 889 tests ; v1.7 = 6 phases, 20 plans, 125 commits, 896 → 1200 tests ;
  v1.8 = 4 phases, 26 / 28 plans, 239 commits, 1200 → 1707 tests.

## Milestones clos

v1.8 clos le 2026-10-03 (écarts connus : VAL-04 et VAL-05 reportés, voir `.planning/MILESTONES.md` › Known Gaps). Détail :
`.planning/milestones/v1.8-*.md`, `.planning/RETROSPECTIVE.md`.
v1.7 clos le 2026-09-27 (écart connu VAL-03, repris en v1.8 puis à nouveau reporté avec VAL-04). Détail : `.planning/milestones/v1.7-*.md`.
