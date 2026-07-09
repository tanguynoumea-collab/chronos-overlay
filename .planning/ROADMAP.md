# Roadmap : Chronos

## Milestones

- ✅ **v1.0 — Overlay de quotas Claude complet** (7 phases, 18 plans, SHIPPED 2026-07-08) — [archive](.planning/milestones/v1.0-ROADMAP.md)
- 🚧 **v1.1 — Estimation utile en mode app bureau** (2 phases, phases 8-9, en cours)

---

## Milestone v1.1 : Estimation utile en mode app bureau

### Overview

L'utilisateur travaille exclusivement dans l'app bureau Claude (Code desktop + Cowork) : la statusline
ne se rend jamais, donc la source primaire `rate_limits` reste vide et le cadran perd sa longueur d'arc
et sa couleur. Les transcripts JSONL couvrent pourtant tout son usage réel. v1.1 **enrichit le repli
JSONL existant** (JsonlEstimationProvider, WeeklyRecalibration/WeeklyAnchor, SettingsService,
CompositeUsageProvider) pour le rendre réellement utile — sans jamais trahir l'honnêteté : tout ce qui
est inféré reste badgé « estimée ».

Deux phases suivent le fil naturel des dépendances : d'abord rendre le repli utile en **inférant les
fenêtres depuis les JSONL** (les arcs retrouvent leur longueur, et leur couleur dès qu'un plafond est
connu) — Phase 8 ; puis donner à l'utilisateur le moyen de **calibrer ces plafonds** (manuellement et
automatiquement) et de voir la matière première de l'estimation — Phase 9. Le code v1.0 est en place :
v1.1 enrichit, ne réécrit pas.

### Phases

**Numérotation des phases :**
- Phases entières (…, 8, 9) : travail de milestone planifié — continue après la Phase 7 (v1.0)
- Phases décimales (8.1, 8.2) : insertions urgentes (marquées INSERTED)

- [ ] **Phase 8 : Inférence des fenêtres + estimation depuis JSONL** - En mode repli, les arcs retrouvent longueur (temps restant inféré) et couleur (utilization estimée quand un plafond est défini), tout restant « estimée »
- [ ] **Phase 9 : Calibration des plafonds + surfaçage de l'estimation** - L'utilisateur calibre les plafonds de tokens (manuel + auto opportuniste) et voit les tokens estimés, sans qu'une estimation soit jamais présentée comme exacte

### Phase Details

### Phase 8 : Inférence des fenêtres + estimation depuis JSONL
**Goal**: En mode repli JSONL (app bureau, statusline vide), le cadran redevient utile : les arcs retrouvent une longueur (reset inféré depuis les transcripts) et, dès qu'un plafond de tokens est défini, une couleur (utilization estimée) — tout demeurant marqué « estimée », aucune valeur jamais inventée.
**Depends on**: Phase 3 (pipeline JSONL + composite existants) — première phase du milestone v1.1
**Requirements**: EST-01, EST-02, EST-03, EST-04, EST-05, NET-01
**Success Criteria** (what must be TRUE):
  1. En mode repli, l'arc extérieur 5 h retrouve une longueur : le reset est inféré depuis les JSONL (début de fenêtre = premier message après un trou d'inactivité ≥ 5 h ; reset = début + 5 h), affiché « estimée ».
  2. Sans activité dans la fenêtre 5 h courante (trou ≥ 5 h), l'arc 5 h s'affiche plein (aucun quota entamé) plutôt que vide.
  3. Quand un plafond (FiveHourTokenBudget / WeeklyTokenBudget) est défini, l'arc correspondant prend une couleur — utilization = tokens sommés dans la fenêtre / plafond ; sans plafond, l'utilization reste null (couleur neutre), comme en v1.0.
  4. Le reset hebdo estimé utilise WeeklyAnchor s'il est défini (mécanique v1.0) ; sans ancre, le countdown hebdo reste « — » (jamais inventé).
  5. Le contrat de données est nettoyé : l'événement mort IUsageProvider.SnapshotChanged est retiré et le champ UsageSnapshot.Age est retiré ou consommé, suite de tests toujours verte.
**Plans**: 2 plans
- [x] 08-01-PLAN.md — Logique pure d'inférence (FiveHourWindowInference, WeeklyWindow) + plafonds de settings (round-trip)
- [x] 08-02-PLAN.md — Enrichissement JsonlEstimationProvider (arcs longueur+couleur) + nettoyage contrat NET-01
**UI hint**: yes

### Phase 9 : Calibration des plafonds + surfaçage de l'estimation
**Goal**: L'utilisateur peut calibrer les plafonds de tokens — à la main et automatiquement quand une mesure fiable se présente — pour rendre l'utilization estimée juste, et voit la matière première de l'estimation ; une valeur calibrée n'est jamais présentée comme exacte.
**Depends on**: Phase 8
**Requirements**: CAL-01, CAL-02, CAL-03, NET-02
**Success Criteria** (what must be TRUE):
  1. Via le menu contextuel (« Calibrer les plafonds… », dialogue minimal), l'utilisateur saisit FiveHourTokenBudget et WeeklyTokenBudget ; ils sont persistés dans settings.json et appliqués aussitôt à la couleur des arcs.
  2. Quand un snapshot Exact (rate_limits réel, used_percentage > 0) coïncide avec des tokens JSONL mesurables sur la même fenêtre, Chronos déduit et mémorise le plafond (tokens / (used_percentage/100)) dans settings.json — visible et modifiable, sans écraser une valeur manuelle plus récente saisie par l'utilisateur.
  3. Toute valeur dérivée d'un plafond calibré reste badgée « estimée » ; seul un snapshot Exact (rate_limits) supprime le badge.
  4. En source Estimated, les tokens estimés (EstimatedTokens) sont affichés en texte secondaire discret — l'utilisateur voit la matière première de l'estimation.
**Plans**: 3 plans
- [ ] 09-01-PLAN.md — Fondations neutres : méta source des plafonds + BudgetCalibration (déduction + priorité manuel/auto) + TokenFormatter fr + BudgetAutoCalibrator (CAL-02/CAL-03/NET-02)
- [ ] 09-02-PLAN.md — Calibration manuelle : dialogue plafonds + CalibrateBudgetsCommand (GAP-1) + RequestRefresh + câblage DI du calibrateur (CAL-01/CAL-02)
- [ ] 09-03-PLAN.md — Surfaçage des tokens estimés : WindowGaugeViewModel.TokensText + TextBlocks discrets (NET-02)
**UI hint**: yes

### Progress

**Execution Order:**
Les phases s'exécutent dans l'ordre numérique : 8 → 9

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 8. Inférence des fenêtres + estimation depuis JSONL | 0/2 | Not started | - |
| 9. Calibration des plafonds + surfaçage de l'estimation | 0/3 | Not started | - |
