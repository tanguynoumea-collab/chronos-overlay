---
gsd_state_version: 1.0
milestone: v1.9
milestone_name: — Lisible partout
status: executing
last_updated: "2026-10-03T17:12:28.860Z"
last_activity: 2026-10-03
progress:
  total_phases: 9
  completed_phases: 8
  total_plans: 36
  completed_plans: 36
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-10-03)

**Core value:** Voir instantanément, sans terminal ni /usage, combien de quota et de temps il reste — sans
jamais présenter une estimation comme un chiffre exact. Et savoir quelle session m'attend ; et comprendre sa façon
d'utiliser Claude au cours du temps, avec la même honnêteté.
**Current focus:** Phase 42.1 — Corrections de la revue visuelle 1

## Current Position

Milestone: v1.9 — Lisible partout (exe 3.5.0)
Phase: 43
Plan: Not started
Status: Executing Phase 42.1
Last activity: 2026-10-03
orientations → 41 Braises → 42 Zones de geste → 43 Release 3.5.0 et constat)

Progress: [░░░░░░░░░░] 0 %

> v1.8 clos le 2026-10-03 avec écarts connus (VAL-04, VAL-05 reportés en VAL-07). Liste de purge R5 validée par
> l'utilisateur le 2026-10-03 (DAT-01). Plan de design du cycle 2 validé (`.zeus/DESIGN_PLAN_CYCLE2.md`).

## Accumulated Context

### Décisions de l'utilisateur (2026-09-27, checkpoint humain 1 du cycle ZEUS)

- Forme A (Pistes) retenue ; B (Simplifié) et C (Tuiles) codées aussi et sélectionnables.
- Vues Jour et 4 semaines conservées dans v1.8.
- Deux gestes d'ouverture : bouton des réglages + double-clic au centre du cadran.
- La phase « compter juste + journal des relevés » se publie SEULE en 3.2.2 avant toute interface.

### Décisions de l'utilisateur (2026-10-03, checkpoint humain 1 du cycle ZEUS n°2)

- Plan de design `.zeus/DESIGN_PLAN_CYCLE2.md` validé (« Je valide ») — contractuel pour les phases 38-42, pas de UI-SPEC.
- Barre statusLine **retirée** (sauvegarde puis retrait de `~/.claude/settings.json` au premier lancement de la 3.5).
- « Pâle » = sombres doux (pas de fonds clairs) ; +20 % mesuré sur le rendu actuel ; Braises = repère de reset +
  séparations horaires + heure du reset ; une seule release 3.5.0.

- Liste de purge R5 validée (DAT-01) : un commit réversible par ligne, ordre diag/docs → orphelins → jeton app bureau →
  recalibrage → pont statusLine en dernier, derrière la garde d'arguments (phase 36).

- Ordre des phases arrêté par le conseil du 2026-10-03 : socle → purge → Historique → thèmes → géométrie → Braises → zones →
  release. 39 avant 40 (vues `Views/Cadrans/*` thémées avant d'être redessinées).

### Blockers / dettes ouvertes

- **Constats avec l'utilisateur non joués (VAL-04, VAL-05)** — protocoles écrits dans
  `.planning/phases/32-compter-juste-puis-journaliser/32-CONSTAT.md` et `.planning/phases/35-4-semaines-acc-s-release-3-3-0/35-CONSTAT.md`
  (écart E1-ter : plusieurs exécutables en marche ; le point (a) commence par les quitter tous, à la main). Reportés dans la
  phase 43 de v1.9 (VAL-07). L'agent ne lance, n'arrête ni ne clique jamais l'overlay.

- **Étapes ZEUS du cycle n°1 non jouées** (DESIGN-REVIEW de la galerie Historique, DEV-COUNCIL, DEV-SENIOR) : absorbées par le
  cycle n°2, qui couvre aussi ce code (`.zeus/state.json`).

- **Décision utilisateur** : supprimer les styles Simplifié et Tuiles de la vue Semaine — planifié en v1.9 phase 38 (HIS-09).

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
