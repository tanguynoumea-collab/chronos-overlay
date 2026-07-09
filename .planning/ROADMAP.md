# Roadmap : Chronos

## Milestones

- ✅ **v1.0 — Overlay de quotas Claude complet** (7 phases, 18 plans, SHIPPED 2026-07-08) — [archive](.planning/milestones/v1.0-ROADMAP.md)
- ✅ **v1.1 — Estimation utile en mode app bureau** (2 phases, 5 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.1-ROADMAP.md)
- ✅ **v1.2 — Usage exact via l'endpoint OAuth** (2 phases, 4 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.2-ROADMAP.md)
- 🚧 **v1.3 — Refonte du cadran : 3 anneaux, remplissage, compacité** (1 phase, phase 12, en cours)

## Prochain milestone

Après v1.3 : rafraîchissement du token OAuth (refreshToken), sous-fenêtres opus/sonnet/cowork,
révélation au survol (V2-02), tooltip riche (V2-03), bande sous-agents (V2-01), tray (V2-05),
opacité configurable (V2-06), nettoyage dette v1.0 restante (DT-2/3).

---

## Milestone v1.3 : Refonte du cadran — 3 anneaux, remplissage, compacité

### Overview

Milestone **purement présentation** : aucune source de données ni aucun provider n'est touché
(v1.0/1.1/1.2 inchangés). L'utilisateur, après avoir vécu avec le cadran v1.2, demande cinq
ajustements visuels cohérents entre eux, qui forment un seul geste de refonte du cadran :

1. **Inverser le sens de remplissage** — les arcs se remplissent à l'approche du reset (longueur =
   fraction ÉCOULÉE = `1 − FractionRemaining`) au lieu de se vider. Vide en début de fenêtre, plein au reset.
2. **Réordonner les anneaux** — du centre vers l'extérieur : hebdo (interne), 5 h courant (milieu),
   timeline 24 h (externe).
3. **Ajouter un anneau timeline 24 h** — cercle complet = 24 h, rempli de minuit local à maintenant,
   graduations toutes les 5 h aux resets projetés, couleur suivant l'utilization 5 h.
4. **Afficher le % d'utilization** au centre à côté de chaque countdown, honnête (`~` si estimé, rien si null).
5. **Réduire l'overlay à ~170 px**, texte central lisible, 3 anneaux + % sans chevauchement.

Le code v1.2 fournit toute la matière : `RingArc` (Shape, DP `Fraction` 0..1, `EllipseGeometry` si ≥1),
`ArcGeometry` (math pure testée), `TickRing` (GeometryGroup), `UtilizationToBrushConverter`,
`WindowGaugeViewModel` (`FractionRemaining`, `Utilization`, `CountdownText`, `IsEstimated`). La refonte
se limite donc à : (a) de la **logique pure nouvelle** — inversion de fraction, math d'angles de la
timeline 24 h (minuit→now, projection des resets 5 h toutes les 5 h), formatage du % ; (b) de la
**recomposition XAML** — réordonner les anneaux, insérer le nouvel anneau 24 h, ajouter les %, redimensionner.
Un seul découpage cohérent → une phase.

### Phases

**Numérotation des phases :**
- Phases entières (…, 11, 12) : travail de milestone planifié — continue après la Phase 11 (v1.2)
- Phases décimales (12.1, 12.2) : insertions urgentes (marquées INSERTED)

- [ ] **Phase 12 : Refonte du cadran — 3 anneaux, remplissage, compacité** - Les arcs se remplissent vers le reset, 3 anneaux réordonnés (hebdo → 5 h → timeline 24 h), un nouvel anneau 24 h coloré et gradué aux resets, le % affiché à côté de chaque countdown, l'overlay réduit à ~170 px — sans toucher aux sources de données

### Phase Details

### Phase 12 : Refonte du cadran — 3 anneaux, remplissage, compacité
**Goal**: Livrer un cadran refondu où les arcs se REMPLISSENT à l'approche du reset, où trois anneaux
concentriques (hebdo interne → 5 h milieu → timeline 24 h externe) encodent le temps et l'usage, où un
nouvel anneau 24 h se remplit de minuit à maintenant avec des graduations aux resets 5 h et une couleur
suivant l'utilization, où le pourcentage d'usage s'affiche honnêtement à côté de chaque countdown, et où
l'overlay tient dans ~170 px sans chevauchement — le tout **sans modifier aucune source de données ni provider**.
**Depends on**: Phase 5 (cadran RingArc/ArcGeometry/TickRing/converters) + Phase 11 (VM enrichi) — première phase du milestone v1.3
**Requirements**: VIS-01, VIS-02, VIS-05, JOUR-01, JOUR-02, JOUR-03, TAILLE-01
**Success Criteria** (what must be TRUE):
  1. **Remplissage inversé** : chaque arc de valeur est vide en début de fenêtre et plein juste avant le reset
     (longueur = fraction écoulée = `1 − FractionRemaining`) — le sens de croissance visible à l'œil est
     inversé par rapport à v1.2, vérifié sur les deux fenêtres (VIS-01).
  2. **Trois anneaux réordonnés** : du centre vers l'extérieur on lit l'anneau hebdomadaire (interne),
     l'anneau 5 h courant (milieu), puis l'anneau timeline 24 h (externe), sans chevauchement (VIS-02).
  3. **Anneau timeline 24 h** : un anneau externe représente 24 h (cercle complet), se remplit de minuit
     local jusqu'à maintenant (à 18 h → ~75 % rempli), porte des graduations toutes les 5 h positionnées aux
     resets de la fenêtre 5 h projetés sur l'axe 24 h, et prend la couleur de l'utilization 5 h (rampe
     vert→ambre→rouge, gris si épuisé, neutre si inconnue) — cohérent avec l'anneau 5 h (JOUR-01, JOUR-02, JOUR-03).
  4. **Pourcentage au centre** : le % d'utilization de chaque fenêtre s'affiche à côté de son countdown
     (« 50 min · 80 % », « 1 j 13 h · 93 % »), préfixé « ~ » en source estimée, et **absent** quand
     l'utilization est null — l'honnêteté des chiffres est préservée (VIS-05).
  5. **Compacité** : l'overlay et le cadran sont mis à l'échelle à ~170 px ; le texte central reste lisible
     et les 3 anneaux comme les % tiennent sans chevauchement (TAILLE-01).
**Plans**: 2 plans
- [ ] 12-01-PLAN.md — Logique pure + tests (FractionElapsed, DayTimeline fraction/angles, UtilizationText, TickRing.Angles)
- [ ] 12-02-PLAN.md — Recomposition XAML (3 anneaux réordonnés + anneau 24 h + resize 170 + % au centre) + checkpoint visuel
**UI hint**: yes

### Progress

**Execution Order:**
Une seule phase : 12

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 12. Refonte du cadran — 3 anneaux, remplissage, compacité | 0/2 | Not started | - |
