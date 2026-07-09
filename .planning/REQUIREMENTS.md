# REQUIREMENTS.md — Chronos v1.3 « Refonte du cadran : 3 anneaux, remplissage, compacité »

## Contexte

Ajustements visuels demandés par l'utilisateur après v1.2 : inverser le sens de remplissage des arcs,
ajouter un 3e anneau « timeline 24 h », réordonner les anneaux, afficher le % d'utilisation, et réduire
la taille de l'overlay. Aucun changement de source de données — pur rendu/ViewModel.

## v1.3 Requirements

### Remplissage (VIS)

- [x] **VIS-01**: Les arcs se REMPLISSENT à l'approche du reset (longueur = fraction de temps ÉCOULÉE
  dans la fenêtre, = 1 − FractionRemaining), au lieu de se vider. Vide en début de fenêtre, plein au reset.
- [ ] **VIS-02**: Ordre des anneaux du centre vers l'extérieur : **hebdomadaire** (le plus interne),
  **5 h courant** (milieu), **timeline 24 h** (le plus externe).
- [x] **VIS-05**: Le pourcentage d'utilization de chaque fenêtre est affiché au centre À CÔTÉ de son
  countdown (ex. « 50 min · 80 % », « 1 j 13 h · 93 % »). En source estimée : « ~80 % » ; sans plafond
  (utilization null) : pas de %, comportement honnête inchangé.

### Anneau timeline 24 h (JOUR)

- [x] **JOUR-01**: Un nouvel anneau externe représente 24 h (cercle complet = 24 h) et se remplit selon
  l'heure du jour (de minuit local jusqu'à maintenant → à 18 h, ~75 % rempli).
- [x] **JOUR-02**: Des graduations toutes les 5 h marquent, sur cet anneau, la position des resets de la
  fenêtre 5 h (projetés sur l'axe des 24 h à partir du resets_at 5 h courant).
- [ ] **JOUR-03**: La couleur de l'anneau 24 h suit l'utilization de la fenêtre 5 h courante (même rampe
  vert→ambre→rouge, gris si épuisé/neutre si inconnue) — cohérence visuelle avec l'anneau 5 h.

### Compacité (TAILLE)

- [ ] **TAILLE-01**: L'overlay est réduit à ~170 px (fenêtre et cadran mis à l'échelle proportionnellement),
  le texte central restant lisible ; les 3 anneaux et les % tiennent sans chevauchement.

## Out of Scope (v1.3)

- Changement des sources de données ou du pipeline (v1.0/1.1/1.2 inchangés).
- Sous-fenêtres opus/sonnet/cowork, refresh token, survol/tooltip — inchangés (v2/v1.3+).
- Taille configurable par réglage — la valeur ~170 px est fixée pour cette itération (réglable = v2-06 différé).

## Traceability

| REQ-ID | Phase | Statut |
|--------|-------|--------|
| VIS-01 | Phase 12 | Complete |
| VIS-02 | Phase 12 | Pending |
| VIS-05 | Phase 12 | Complete |
| JOUR-01 | Phase 12 | Complete |
| JOUR-02 | Phase 12 | Complete |
| JOUR-03 | Phase 12 | Pending |
| TAILLE-01 | Phase 12 | Pending |

**Couverture : 7/7 requirements mappés → Phase 12 (aucun orphelin, aucun doublon).**

---
*Last updated: 2026-07-09 — milestone v1.3 : roadmap créée, Phase 12 mappée (7/7)*
