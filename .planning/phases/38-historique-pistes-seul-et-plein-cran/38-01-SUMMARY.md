---
phase: 38-historique-pistes-seul-et-plein-cran
plan: 01
subsystem: historique-vue-semaine
tags: [wpf, xaml, historique, suppression, HIS-09]
requires: []
provides:
  - "Vue Semaine à grille unique GrillePistes (toujours visible, plus aucun DataTrigger de style)"
  - "PisteNiveau réduite aux variantes SemaineComplete et Jour"
affects:
  - "38-02 (suppression de HistoriqueStyleSemaine : la vue n'en dépend plus)"
  - "38-03 (plein écran : un seul exemplaire de grille à convertir)"
tech-stack:
  added: []
  patterns: ["grille nommée unique GrillePistes, vérifiée par FindName dans les tests"]
key-files:
  created: []
  modified:
    - src/Chronos/Views/Historique/VueSemaineView.xaml
    - src/Chronos/Controls/Historique/PisteNiveau.cs
    - src/Chronos/Resources/DesignTokens.xaml
    - src/Chronos/Text/TextesHistorique.cs
    - tests/Chronos.Tests/VueSemaineBindingTests.cs
    - tests/Chronos.Tests/HonneteteHistoriqueTests.cs
    - tests/Chronos.Tests/PistesHistoriqueTests.cs
    - tests/Chronos.Tests/VueJourBindingTests.cs
    - tests/Chronos.Tests/GardeTokensHistoriqueTests.cs
    - tests/Chronos.Tests/TextesHistoriqueTests.cs
  deleted:
    - src/Chronos/Controls/Historique/PisteFenetres5h.cs
    - src/Chronos/Rendering/Historique/Tuiles5h.cs
    - tests/Chronos.Tests/Tuiles5hTests.cs
decisions:
  - "HIS-09 non coché dans REQUIREMENTS.md : l'exigence couvre aussi les sélecteurs et la propriété HistoriqueStyleSemaine (plan 02)"
  - "Les tests « sur les trois styles » deviennent un passage unique sur GrillePistes, avec les mêmes assertions (rien d'affaibli) ; le test des annotations vérifie maintenant en plus le compte unique de « répartition inconnue » sur la grille"
metrics:
  duration: "~6 min"
  completed: "2026-10-03"
  tasks: 2
  files: 13
---

# Phase 38 Plan 01 : vue Semaine à grille unique (Pistes seul, partie vue) — Summary

La vue Semaine n'a plus qu'une grille, `GrillePistes` (NIVEAU 150 · RYTHME 72 · TOKENS 72 · COUVERTURE 12), toujours visible. Les grilles Simplifié et Tuiles ont disparu, ainsi que la piste Fenêtres 5 h (`PisteFenetres5h`, `Tuiles5h`, « FENÊTRES 5 H »), la variante `SemaineHebdoSeul` et les 6 tokens `HistoHauteur*Simplifie` / `*Tuiles`. Les tests qui ne portaient que sur ces éléments sont partis avec eux.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | Production : grilles B/C, `GrilleStyle`, Fenêtres 5 h, `SemaineHebdoSeul`, 6 tokens, texte retirés ; `StylePistes` renommée `GrillePistes`, sans `Grid.Style` | b94cfe7 |
| 2 | Tests : `Tuiles5hTests` supprimé ; Semaine/Honnêteté passées sur la grille unique ; nouveau `La_vue_Semaine_n_a_qu_une_grille_de_pistes` ; `TaillesHisto` −6 (45 → 39) ; « Style : » retiré des mots attendus | 9706cd3 |

## Vérification

- `dotnet test Chronos.sln -c Debug` lancé deux fois de suite : 1697/1697, 0 échec.
- `dotnet build Chronos.sln -c Release` : 0 avertissement, 0 erreur ; build Debug du projet principal : 0/0.
- `git grep "PisteFenetres5h\|Tuiles5h\|SemaineHebdoSeul\|HistoHauteurNiveauTuiles" -- src tests` : vide.
- `Controls/Historique` contient exactement 7 fichiers .cs, donc la garde `>= 7` tient toujours.
- Ce qui reste de `Simplifie` / `Tuiles` relève du plan 02 : `ChronosSettings`, VM `IsStyle*` / `ChoisirStyle`, `TextesHistorique.Style*` / `LibelleStyle`, sélecteurs de `HistoriqueWindow.xaml` et de `ReglagesWindow.xaml` et leurs tests.

## Écarts par rapport au plan

Aucune correction automatique. Quelques ajustements de détail :
- Le test de bascule de style a été supprimé comme prévu. Ses vérifications de l'axe des jours (sept minuits + « reset hebdo → ») sont reprises dans `La_vue_Semaine_n_a_qu_une_grille_de_pistes` pour ne rien perdre en couverture.
- Les docs XML d'en-tête de `VueSemaineBindingTests` et `HonneteteHistoriqueTests` ne parlent plus des « trois styles ». Le commentaire de `TaillesHisto` donne le nouveau compte (39).
- Les chaînes interpolées `$"[{style}] …"`, vidées de leur seul trou, sont redevenues des littéraux simples.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND: src/Chronos/Views/Historique/VueSemaineView.xaml (`x:Name="GrillePistes"` ×1)
- ABSENT (attendu) : PisteFenetres5h.cs, Tuiles5h.cs, Tuiles5hTests.cs
- FOUND: b94cfe7, 9706cd3
