# REQUIREMENTS.md — Chronos v1.1 « Estimation utile en mode app bureau »

## Contexte

L'utilisateur travaille exclusivement dans l'app bureau Claude (Code desktop + Cowork) : la statusline
ne se rend jamais → la source primaire (rate_limits) ne se remplit pas. Les transcripts JSONL couvrent
en revanche tout son usage réel. v1.1 rend le repli JSONL réellement UTILE sans jamais trahir
l'honnêteté : tout ce qui est inféré reste marqué « estimée ».

## v1.1 Requirements

### Estimation (EST)

- [x] **EST-01**: Le reset de la fenêtre 5 h est inféré des JSONL : début de fenêtre = premier message
  (timestamp) dont l'antériorité est < 5 h après un trou d'inactivité ≥ 5 h ; reset estimé = début + 5 h.
  L'arc extérieur retrouve ainsi une longueur (temps restant) en mode repli, marquée estimée.
- [x] **EST-02**: Si aucune activité dans la fenêtre courante (trou ≥ 5 h), la fenêtre 5 h est affichée
  « pleine » (fraction = 1, aucun quota entamé) plutôt que vide — sans inventer d'utilization.
- [x] **EST-03**: L'utilization 5 h estimée = tokens sommés dans la fenêtre courante / plafond calibrable
  (setting FiveHourTokenBudget). Sans plafond défini : utilization reste null (couleur neutre) — comportement v1.0.
- [x] **EST-04**: L'utilization hebdo estimée = tokens sommés sur la fenêtre hebdo (ancrée sur WeeklyAnchor
  si défini, sinon 7 jours glissants) / plafond calibrable (WeeklyTokenBudget). Sans plafond : null.
- [x] **EST-05**: Le reset hebdo estimé utilise WeeklyAnchor (mécanique v1.0) ; sans ancre, il reste
  inconnu (countdown « — ») — jamais inventé.

### Calibration (CAL)

- [x] **CAL-01**: Les plafonds (FiveHourTokenBudget, WeeklyTokenBudget) sont persistés dans settings.json
  et réglables via le menu contextuel (« Calibrer les plafonds… », dialogue minimal).
- [x] **CAL-02**: Calibration automatique opportuniste : quand un snapshot Exact (rate_limits réel) est
  disponible avec used_percentage > 0 ET que des tokens JSONL sont mesurables sur la même fenêtre,
  Chronos mémorise le plafond déduit (tokens / (used_percentage/100)) dans settings.json — visible et
  modifiable par l'utilisateur, jamais silencieusement écrasé s'il a saisi une valeur manuelle plus récente.
- [x] **CAL-03**: Toute valeur issue d'un plafond calibré reste badgée « estimée » — seul un snapshot
  Exact (rate_limits) supprime le badge.

### Nettoyage dette v1.0 (NET)

- [x] **NET-01**: L'événement mort IUsageProvider.SnapshotChanged est retiré du contrat (DT-1) ; le champ
  UsageSnapshot.Age inutilisé est retiré ou consommé (DT-2).
- [x] **NET-02**: Les tokens estimés (EstimatedTokens) sont surfacés dans l'UI en texte secondaire discret
  quand la source est Estimated (DT-3) — l'utilisateur voit la matière première de l'estimation.

## Out of Scope (v1.1)

- Appel de l'endpoint OAuth /usage avec le token local — écarté par prudence (jetons d'auth), réévaluable en v1.2.
- Révélation au survol, tooltip riche, tray, opacité/échelle, clic-traversant (V2-02..06) — inchangés.
- Notifications, historique, multi-comptes — inchangés (v1.0).

## Traceability

| REQ-ID | Phase | Statut |
|--------|-------|--------|
| EST-01 | Phase 8 | Complete |
| EST-02 | Phase 8 | Complete |
| EST-03 | Phase 8 | Complete |
| EST-04 | Phase 8 | Complete |
| EST-05 | Phase 8 | Complete |
| NET-01 | Phase 8 | Complete |
| CAL-01 | Phase 9 | Complete |
| CAL-02 | Phase 9 | Complete |
| CAL-03 | Phase 9 | Complete |
| NET-02 | Phase 9 | Complete |

**Couverture : 10/10 requirements v1.1 mappés — aucun orphelin.**

---
*Last updated: 2026-07-09 — roadmap v1.1 créé (phases 8-9)*
