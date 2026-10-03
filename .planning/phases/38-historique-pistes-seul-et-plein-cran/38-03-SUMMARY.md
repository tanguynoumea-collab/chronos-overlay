---
phase: 38-historique-pistes-seul-et-plein-cran
plan: 03
subsystem: historique-ui
tags: [wpf, plein-ecran, design-tokens, dynamicresource, grid-star]
requires:
  - phase: 38-02
    provides: vue Semaine à grille unique GrillePistes, style unique
provides:
  - tokens *PleinEcran du contrat § 4.2 (+ en-tête 124) dans DesignTokens.xaml
  - tokens de rangées GridLength, plafonds, défilement (mode normal)
  - DictionnairePleinEcran (ClesEchelonnees, Rangees, Construire)
  - vue Semaine basculable par simple fusion du dictionnaire
affects: [38-04, 38-05, 38-06]
tech-stack:
  added: []
  patterns:
    - "Plein écran = échange d'un dictionnaire fusionné (clés normales, valeurs plein écran) lu en DynamicResource"
    - "Rangées de pistes Auto / étoilées par ressource, MaxHeight par ressource, espacements en lignes fixes"
    - "ScrollViewer.VerticalScrollBarVisibility par ressource (Auto → Disabled)"
key-files:
  created:
    - src/Chronos/Views/Historique/DictionnairePleinEcran.cs
    - tests/Chronos.Tests/PleinEcranVuesTests.cs
  modified:
    - src/Chronos/Resources/DesignTokens.xaml
    - src/Chronos/Views/Historique/VueSemaineView.xaml
    - tests/Chronos.Tests/GardeTokensHistoriqueTests.cs
key-decisions:
  - "HistoHauteurEnTetePleinEcran = 124 DIP, hors liste du contrat § 4.2 (décision de l'orchestrateur) : à 92, trois lignes de corps ×1,35 débordent et la fraîcheur ne peut pas passer sur deux lignes (§ 7)"
  - "DictionnairePleinEcran.Construire lève InvalidOperationException si un token manque ou n'est pas un double : aucun repli silencieux"
requirements-completed: []
duration: 5min
completed: 2026-10-03
---

# Phase 38 Plan 03 : Socle plein écran et vue Semaine Summary

**Dictionnaire PleinEcran construit depuis 16 tokens `*PleinEcran` (étoiles = hauteurs normales, pistes en NaN, défilement coupé) ; la vue Semaine s'agrandit par sa seule fusion : 150* / 72* / 72*, plafonds 520 / 240, couverture fixe à 12, corps et traits du contrat.**

## Performance

- **Duration :** ~5 min
- **Completed :** 2026-10-03
- **Tasks :** 2 (TDD, RED puis GREEN pour chacune)
- **Files modified :** 5

## Accomplishments

- `DesignTokens.xaml` : 16 tokens plein écran (corps 11,5 → 21, libellés 128, légende 96, escalier 3, premier plan 3,2, tiret 11,
  en-tête 124, plafonds 520 / 240) ; plafonds normaux infinis ; 7 rangées de pistes `Auto` ; espacements 6 (Semaine) / 8 (Jour) ;
  `HistoDefilementVertical` = `Auto`.
- `DictionnairePleinEcran` : clés normales → valeurs plein écran, rangées en `GridLength(hauteur normale, Star)`, hauteurs de
  pistes `NaN`, défilement `Disabled` (31 entrées).
- `VueSemaineView.xaml` : `GrillePistes` en 10 rangées (lignes d'espacement fixes à la place de `HistoMargePiste`), `RowSpan` 9,
  toutes les clés échelonnées et les hauteurs de pistes en `DynamicResource`, défilement par ressource. Mode normal inchangé
  (tests existants verts sans modification).
- Tests : contenu du dictionnaire (table du contrat en dur), types des tokens normaux, proportions à 1400 × 700, plafonds à
  2560 × 1600, petit écran 1248 × 560 (couverture et pied dans la vue, NIVEAU ≥ 150), corps / traits / largeur de cellule en plein
  écran, retour exact au normal, garde « aucune StaticResource sur une clé échelonnée ».

## Task Commits

1. **Task 1 RED :** `9632b5e` test(38-03): tests du dictionnaire plein écran et des tokens de rangées
2. **Task 1 GREEN :** `01f7ff4` feat(38-03): tokens plein écran du contrat § 4.2 et DictionnairePleinEcran
3. **Task 2 RED :** `49c0d01` test(38-03): tests de mise en page plein écran de la vue Semaine et garde StaticResource
4. **Task 2 GREEN :** `cf8cc06` feat(38-03): vue Semaine étoilable et plafonnée, tailles en DynamicResource

## Decisions Made

- **Écart au contrat consigné — `HistoHauteurEnTetePleinEcran = 124` :** ce token n'est pas listé au § 4.2 de
  DESIGN_PLAN_CYCLE2. Il est nécessaire (RESEARCH Pitfall 6) : à 92 DIP, trois lignes de corps ×1,35 débordent et la fraîcheur ne
  peut pas passer sur deux lignes (§ 7). Décision de l'orchestrateur ; valeur ≈ ×1,35 dans l'esprit du contrat. À confirmer à la
  revue visuelle. Il est déjà dans `DictionnairePleinEcran.ClesEchelonnees` ; son usage (en-tête de la fenêtre) arrive avec le plan
  de la fenêtre.
- `HistoLargeurEtiquettesSemaines` (140) inchangée, conformément au plan (le plan 04 la teste).
- Lecture des tokens par une aide `Lire` qui lève une `InvalidOperationException` nommant la clé (au lieu d'une
  `InvalidCastException` / `NullReferenceException` muette) : même contrat « pas de repli silencieux », message exploitable.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. Le test petit écran et le test de retour au normal passaient déjà en RED (en mode normal, le contenu tient dans 560 DIP et
les valeurs normales sont déjà là) ; ils deviennent significatifs une fois le dictionnaire actif, et passent en GREEN.

## Verification

- `dotnet test Chronos.sln -c Debug` deux fois : 1708 / 1708, 0 échec.
- `dotnet build Chronos.sln -c Release` : 0 avertissement, 0 erreur.
- Critères grep du plan : 16 / 9 / 1 (tokens), 1 / 2 / 3 / 1 / 0 / 0 (vue Semaine).
- Gardes `Aucune_couleur_ni_taille_en_dur…` et `Aucun_texte_visible_ecrit_en_dur…` vertes, sans exemption.

## Known Stubs

None. Le dictionnaire n'est pas encore fusionné par la fenêtre (bouton, F11, géométrie) : c'est l'objet des plans suivants de la
phase, pas un bouchon.

## Next Phase Readiness

- Plans 04 / 06 : convertir Jour et 4 semaines sur le même motif (rangées `HistoRangee*Jour` / `*QuatreSemaines` déjà en tokens),
  étendre `VuesConverties` de la garde.
- Plan fenêtre : fusionner `DictionnairePleinEcran.Construire(...)` dans la fenêtre + les trois vues, et consommer
  `HistoHauteurEnTete` en `DynamicResource`.

## Self-Check: PASSED

- FOUND : src/Chronos/Views/Historique/DictionnairePleinEcran.cs, tests/Chronos.Tests/PleinEcranVuesTests.cs
- FOUND : 9632b5e, 01f7ff4, 49c0d01, cf8cc06
