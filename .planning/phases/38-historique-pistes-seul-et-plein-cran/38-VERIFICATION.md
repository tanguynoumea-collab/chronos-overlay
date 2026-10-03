---
phase: 38-historique-pistes-seul-et-plein-cran
verified: 2026-10-03T00:00:00Z
status: human_needed
score: 5/5 critères de succès vérifiés dans le code
human_verification:
  - test: "Revue visuelle du plein écran (F11, bouton, Échap) sur le moniteur réel, trois vues"
    expected: "Pistes agrandies sans défilement, rien de tronqué, coins carrés, barre des tâches couverte (ou non, cf. écart décidé), position et taille restaurées en sortie"
    why_human: "Rendu réel et multi-moniteur non vérifiables sans lancer l'exe (interdit). Prévu dans la DESIGN-REVIEW du cycle, après la phase 42."
---

# Phase 38 : Historique, Pistes seul et plein écran — rapport de vérification

**Objectif :** un seul style (Pistes), sans sélecteur ; plein écran général sur le moniteur courant (pistes proportionnelles plafonnées 520 / 240, textes ≈ ×1,35, sans défilement), sortie par bouton / F11 / Échap à deux niveaux, géométrie restaurée ; honnêteté identique à toutes les tailles.
**Vérifié le :** 2026-10-03
**Statut :** human_needed (automatisé : tout est conforme)
**Re-vérification :** non, vérification initiale.

Écarts décidés par l'orchestrateur, non comptés comme manques : en-tête plein écran 124 DIP ; barre des tâches par détection automatique Windows (constat manuel en phase 43) ; pas de recalcul au changement de DPI pendant le plein écran. Porte de phase déclarée : build `-warnaserror` 0 avertissement, 1733/1733 tests.

## Vérités observables (critères du ROADMAP)

| # | Vérité | Statut | Preuves |
|---|--------|--------|---------|
| 1 | Un seul style, plus aucun sélecteur ; `HistoriqueStyleSemaine` supprimée et lue sans perte (HIS-09) | VÉRIFIÉ | Recherche dans `src` et `tests` (hors bin/obj) de `Tuiles5h`, `PisteFenetres5h`, `SemaineHebdoSeul`, `StyleSimplifie`, `StyleTuiles`, `Trois styles` : aucune occurrence dans le code. Il reste des commentaires, ainsi que des tests d'absence (`Assert.Null(...GetProperty("HistoriqueStyleSemaine"))`, `FindName("PuceStyleTuiles")` nul) et la garde du README. `ChronosSettings.cs:99` porte une note de suppression. `SettingsServiceTests.cs:282-327` couvre `Pistes`, `Simplifie` et `Tuiles` en entrée, sans perte des autres réglages, et la clé est absente après `Save`. |
| 2 | Le plein écran couvre l'écran courant ; pistes proportionnelles plafonnées 520 / 240 ; sans défilement (HIS-10) | VÉRIFIÉ | `HistoriqueWindow.xaml.cs` : `FournisseurBornesMoniteur` (seam testable, bornes DIP du moniteur), pas de `WindowState.Maximized`. `DictionnairePleinEcran.cs` fait les rangées en étoiles aux hauteurs normales. Tokens `HistoPlafondNiveauPleinEcran` = 520 et `HistoPlafondPistePleinEcran` = 240. `PleinEcranVuesTests` : 14 tests Measure/Arrange. |
| 3 | Dictionnaire `PleinEcran` ≈ ×1,35 par paliers, en tokens (HIS-10) | VÉRIFIÉ | `DesignTokens.xaml:114-129` : corps 21, 18, 15,5, 15, 14, 13, 12, 11,5 ; libellés 128 ; légende 96 ; escalier 3 ; premier plan 3,2 ; tiret 11 ; en-tête 124 (écart décidé) ; plafonds 520 / 240. Les vues consomment ces valeurs en `DynamicResource`. Le dictionnaire est fusionné dans la fenêtre et dans les trois vues (`xaml.cs:231-237`) et retiré en sortie (`:245`). |
| 4 | Sortie par bouton, F11, Échap à deux niveaux ; géométrie restaurée (HIS-11) | VÉRIFIÉ | `HistoriqueWindow.xaml:140-141` : `KeyBinding` Escape → `EchapCommand`, F11 → `BasculerPleinEcranCommand`. Bouton `BoutonPleinEcran` dans l'en-tête, avec les libellés de `TextesHistorique`. `PleinEcranHistorique.Echap(bool)` est une fonction pure ; le VM l'appelle (`HistoriqueViewModel.cs:191-193`). `_avantPleinEcran` mémorise la géométrie, la restaure (`:205-214`) et reste prioritaire pour la persistance (`:123`). Sans moniteur lisible, la bascule est annulée (`:175-177`). |
| 5 | Honnêteté identique à toutes les tailles (HIS-10) | VÉRIFIÉ (statique) | Plan 06 : tests d'honnêteté mode normal / plein écran sur les trois vues. Les textes d'honnêteté ne dépendent pas du dictionnaire (seuls tailles et traits changent). Résultat des tests repris de la porte de phase (1733/1733), non relancé. |

**Score :** 5/5

## Artefacts

| Artefact | Statut | Détails |
|----------|--------|---------|
| `Placement/PleinEcranHistorique.cs` | VÉRIFIÉ | `Echap` et `BornesDip` purs, sans type WPF. |
| `Views/Historique/DictionnairePleinEcran.cs` | VÉRIFIÉ | Utilisé par la fenêtre. |
| `Resources/DesignTokens.xaml` | VÉRIFIÉ | Tokens `*PleinEcran` présents. |
| `Views/Historique/HistoriqueWindow.xaml(.cs)` | VÉRIFIÉ | Bouton, KeyBindings, fusion du dictionnaire dans 4 propriétaires. |
| `Interop/NativeMethods.cs` | VÉRIFIÉ | `DWMWCP_DONOTROUND` présent. |
| `tests/.../PleinEcranHistoriqueTests.cs`, `PleinEcranVuesTests.cs` | VÉRIFIÉ | Tests présents et non vides. |

## Liens clés

| De | Vers | Statut |
|----|------|--------|
| KeyBinding Escape / F11 | `EchapCommand` / `BasculerPleinEcranCommand` | LIÉ |
| `HistoriqueViewModel.Echap` | `PleinEcranHistorique.Echap` | LIÉ |
| Fenêtre | `DictionnairePleinEcran.Construire` (4 propriétaires) | LIÉ |
| `EnregistrerGeometrie` | `_avantPleinEcran` | LIÉ |
| Vues | clés échelonnées en `DynamicResource` | LIÉ |

## Couverture des exigences

| Exigence | Plans | Statut | Preuve |
|----------|-------|--------|--------|
| HIS-09 | 38-01, 38-02 | SATISFAITE | Critère 1. |
| HIS-10 | 38-03, 38-04, 38-05, 38-06 | SATISFAITE | Critères 2, 3 et 5. |
| HIS-11 | 38-05, 38-06 | SATISFAITE | Critère 4. |

Les trois ID sont cochés et marqués « Complete » dans `REQUIREMENTS.md` (lignes 51-58 et 141-143). Aucune exigence orpheline : HIS-09, HIS-10 et HIS-11 sont les seules mappées à la phase 38.

## Anti-patterns

Aucun TODO ni stub bloquant relevé dans les fichiers de la phase. Les occurrences de `HistoriqueStyleSemaine` sont des commentaires et des gardes de test.

## Vérifications comportementales

Étape 7b : non rejouée (l'exe ne se lance pas ; porte de phase fournie : 1733/1733, 0 avertissement).

## Vérification humaine requise

1. **Revue visuelle du plein écran**
   - Test : sur le moniteur réel, ouvrir l'Historique, F11 / bouton / Échap sur Semaine, Jour et 4 semaines. Tester aussi un petit écran.
   - Attendu : pistes proportionnelles, rien de tronqué, pas de défilement, bouton de sortie visible, géométrie restaurée.
   - Raison : rendu réel, DPI et multi-moniteur. À faire dans la DESIGN-REVIEW du cycle (après la phase 42) ; l'état de la barre des tâches se constate en phase 43.

## Résumé

Aucun gap. Le code correspond aux six plans et aux cinq critères du ROADMAP, et les trois exigences sont satisfaites. Seule la revue visuelle réelle reste, déjà planifiée dans la DESIGN-REVIEW du cycle.

---

_Vérifié : 2026-10-03_
_Vérificateur : Claude (gsd-verifier)_
