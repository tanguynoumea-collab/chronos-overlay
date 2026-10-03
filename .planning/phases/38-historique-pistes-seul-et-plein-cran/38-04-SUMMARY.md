---
phase: 38-historique-pistes-seul-et-plein-cran
plan: 04
subsystem: historique-ui
tags: [wpf, plein-ecran, dynamicresource, grid-star]
requires:
  - phase: 38-03
    provides: DictionnairePleinEcran, tokens de rangées / plafonds / défilement, aides PleinEcranVuesTests
provides:
  - vue Jour basculable par simple fusion du dictionnaire (190* / 64* / 64*, plafonds 520 / 240)
  - vue 4 semaines à corps en Grid, NIVEAU étoilable (250*) plafonné à 520
  - garde « aucune StaticResource échelonnée » étendue aux trois vues
affects: [38-05, 38-06]
tech-stack:
  added: []
  patterns:
    - "Vue montée seule sans UseLayoutRounding : l'arrondi au pixel est hérité de HistoriqueWindow (mesures en DIP exactes)"
key-files:
  created: []
  modified:
    - src/Chronos/Views/Historique/VueJourView.xaml
    - src/Chronos/Views/Historique/VueQuatreSemainesView.xaml
    - tests/Chronos.Tests/PleinEcranVuesTests.cs
key-decisions:
  - "UseLayoutRounding / SnapsToDevicePixels retirés des vues Jour et 4 semaines (hérités de la fenêtre hôte, comme la Semaine au 38-03) : posés sur la vue, ils arrondissent les rangées étoilées au pixel physique (125 % : 240 → 240,8 ; ratio 2,963 au lieu de 2,969)"
  - "HistoLargeurEtiquettesSemaines = 140 inchangée (décision de l'orchestrateur), non-troncature testée sur 1248 × 560"
requirements-completed: []
duration: 15min
completed: 2026-10-03
---

# Phase 38 Plan 04 : Vues Jour et 4 semaines au plein écran Summary

**Vue Jour en 10 rangées (NIVEAU / RYTHME / TOKENS étoilables 190* / 64* / 64*, plafonnées 520 / 240, espacements en lignes fixes) et vue 4 semaines à corps en Grid (NIVEAU 250* plafonné à 520, couvertures fixes 10 au pas de 16) : les deux s'agrandissent par la seule fusion du dictionnaire PleinEcran, sans défilement. En mode normal, rien ne change.**

## Performance

- **Durée :** ~15 min
- **Terminé le :** 2026-10-03
- **Tâches :** 2 (TDD : RED puis GREEN pour chacune)
- **Fichiers modifiés :** 3

## Accomplishments

- `VueJourView.xaml` : rangées 2 / 5 / 7 = `HistoRangee{Niveau,Rythme,Tokens}Jour` + `MaxHeight` `HistoPlafondNiveau` / `HistoPlafondPiste` ;
  lignes 4 et 6 fixes (`HistoRangeeEspaceJour`) à la place des quatre `Margin="0,8,0,0"` ; légende en rangée 8, couverture en 9 ; surcouche
  et Canvas d'infobulle en `RowSpan` 8 ; `ScrollViewer x:Name="Defilement"` piloté par `HistoDefilementVertical` ; clés échelonnées et
  hauteurs de pistes en `DynamicResource`.
- `VueQuatreSemainesView.xaml` : `StackPanel Corps` → `Grid Corps` (0 annotations, 1 NIVEAU étoilable plafonné, 2 titre, 3 couvertures) ;
  piste en `DynamicResource HistoHauteurNiveauQuatreSemaines` ; défilement par ressource ; clés échelonnées en `DynamicResource`, y compris
  l'axe du haut (colonne des libellés) et le trait des étiquettes. `HistoLargeurEtiquettesSemaines`, `HistoHauteurCouvertureSemaine` et
  `HistoPasCouvertureSemaines` restent en `StaticResource` (fixes). Le style `Etiquette` avait déjà `TextWrapping="Wrap"` et aucun `TextTrimming`.
- `PleinEcranVuesTests` : 7 nouveaux tests (4 Jour, 3 pour les 4 semaines), aides `MonterJour`, `PleinEcranEnPage`, `DefilementUnique`,
  `CelluleNiveau`, `CouverturesFixes` ; `VuesConverties` = les trois vues.

## Task Commits

1. **Task 1 RED :** `8473fd2` test(38-04): tests plein écran de la vue Jour et garde StaticResource étendue
2. **Task 1 GREEN :** `cc76ab7` feat(38-04): vue Jour étoilable et plafonnée, tailles en DynamicResource
3. **Task 2 RED :** `d80407b` test(38-04): tests plein écran de la vue 4 semaines et garde étendue aux trois vues
4. **Task 2 GREEN :** `3b5b00c` feat(38-04): vue 4 semaines — corps en Grid, NIVEAU étoilable et plafonné

## Decisions Made

- La colonne des étiquettes des 4 semaines reste à 140 DIP (décision de l'orchestrateur). Le test petit écran vérifie : largeur 140, 4 étiquettes en
  `Wrap` sans `TextTrimming`, hauteur totale ≤ hauteur de NIVEAU.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Arrondi au pixel posé sur la vue : proportions et plafonds faux**
- **Trouvé pendant :** Task 1 (GREEN)
- **Problème :** `VueJourView` portait `UseLayoutRounding="True" SnapsToDevicePixels="True"`. À 125 % d'échelle, les rangées étoilées sont
  arrondies au pixel physique (0,8 DIP) : RYTHME 240,8 au plafond, ratio NIVEAU / RYTHME 2,963 au lieu de 190/64 = 2,969, retour au normal à 190,4.
- **Correction :** attributs retirés de la vue, avec un commentaire. Ils sont hérités de `HistoriqueWindow`, donc le rendu dans l'application
  ne change pas. C'est le motif de la vue Semaine (38-03). Même retrait sur `VueQuatreSemainesView`, par cohérence. Les tests existants
  (`VueJourBindingTests`, qui tolèrent ±0,5, et les rendus RenderTargetBitmap) restent verts sans modification. Le commentaire de la ligne 123 de
  `VueJourBindingTests` (« 190 → 190,4 ») est désormais caduc, mais il est sans effet.
- **Fichiers :** VueJourView.xaml, VueQuatreSemainesView.xaml
- **Commits :** cc76ab7, 3b5b00c

## Issues Encountered

- Les tests petit écran passaient déjà en RED (en mode normal, le contenu tient dans 560 DIP). Ils ne prennent leur sens qu'une fois le dictionnaire
  actif, et ils passent en GREEN. C'était déjà le cas au 38-03.
- L'outil Edit a retiré les espaces de fin de `old_string`, ce qui a collé deux attributs XAML (MC3000). C'est corrigé avant le commit.

## Verification

- `dotnet test Chronos.sln -c Debug`, deux fois : 1715 / 1715, 0 échec.
- `dotnet build Chronos.sln -c Release` : 0 avertissement, 0 erreur.
- Critères grep du plan : Jour 1 / 2 / 0 / 2 / 1 / 0 ; 4 semaines 0 et 1 / 1 / 1 / 0 ; `HistoLargeurEtiquettesSemaines` 1 occurrence (140) ;
  `StaticResource HistoCorps` : 0 dans chacune des trois vues.

## Known Stubs

Aucun. La fenêtre ne fusionne pas encore le dictionnaire (bouton, F11, géométrie) : c'est l'objet des plans suivants de la phase.

## Next Phase Readiness

- Les trois vues sont converties. Le plan de la fenêtre n'a plus qu'à fusionner `DictionnairePleinEcran.Construire(...)` dans la fenêtre
  et dans les vues. Les vues fusionnent `DesignTokens.xaml` localement, donc un dictionnaire posé seulement sur la fenêtre serait masqué par
  ce repli local : il faut le fusionner EN DERNIER dans les ressources de chaque vue, comme le font les tests.
- HIS-10 reste à cocher en 38-06.

## Self-Check: PASSED

- FOUND : src/Chronos/Views/Historique/VueJourView.xaml, src/Chronos/Views/Historique/VueQuatreSemainesView.xaml, tests/Chronos.Tests/PleinEcranVuesTests.cs
- FOUND : 8473fd2, cc76ab7, d80407b, 3b5b00c
