---
phase: 43-release-3-5-0-et-constat
plan: 07
subsystem: cadrans / release
tags: [VAL-07, gap_closure, cadrans, braises, release]
requires: [43-06 (Chronos-v3.5.0.exe reconstruit, rempli = temps consommé)]
provides:
  - cadran Braises sans flèche de reset (décision utilisateur au constat du 2026-10-04)
  - Chronos-v3.5.0.exe reconstruit (même version), empreinte mise à jour
affects: [43-04 (constat utilisateur)]
tech-stack:
  added: []
  patterns: [témoin de rendu « dehors » placé dans l'ancien secteur d'exclusion pour figer le retrait]
key-files:
  created: []
  modified:
    - src/Chronos/Views/Cadrans/CadranBraisesView.xaml
    - src/Chronos/Views/MainWindow.xaml.cs
    - src/Chronos/Theming/ChronosTheme.cs
    - src/Chronos/Controls/Cadrans/EmberRingControl.cs
    - tests/Chronos.Tests/CadranBraisesTests.cs
    - tests/Chronos.Tests/CadransThemeBindingTests.cs
    - tests/Chronos.Tests/ZonesGesteRenduTests.cs
    - README.md
    - .zeus/DESIGN_PLAN_CYCLE2.md
    - docs/publish.md
    - .planning/phases/43-release-3-5-0-et-constat/43-CONSTAT.md
decisions:
  - "Flèche de reset de Braises retirée (décision utilisateur, « elle ne sert à rien »)"
  - "Pinceau TickReset conservé : encore utilisé par les Anneaux et l'Historique"
metrics:
  duration: ~15 min
  completed: 2026-10-04
  tasks: 2
  files: 11
---

# Phase 43 Plan 07 : retrait de la flèche de Braises — Summary

La flèche de reset fixe à midi du cadran Braises a été retirée : le Canvas `FlecheReset` (triangle et filet) n'existe plus.
Les braises et le « ↻ HH:MM » du centre n'ont pas changé. Chronos-v3.5.0.exe a été reconstruit et son empreinte mise à jour.

## Tests adaptés (RED 6bcd2a4 : 5 échecs, tous attendus)

- `CadranBraisesTests` : `La_fleche_de_reset_est_fixe_a_midi_hors_anneau_et_non_cliquable` est remplacé par
  `La_vue_Braises_n_a_plus_de_fleche_de_reset`. Le test vérifie que `FindName("FlecheReset")` est null, que la vue
  ne contient ni `Polygon` ni `Line`, et que `HeureResetBraises` est toujours là.
- `CadransThemeBindingTests` : la liaison TickReset du triangle et du filet est remplacée par une absence de `Polygon`
  et de `Line` dans Braises, sur tous les thèmes.
- `ZonesGesteRenduTests` :
  - le secteur d'exclusion de la flèche (x 76 à 94, y 0 à 26) est retiré ;
  - un nouveau témoin « dehors » est ajouté en (85, 8), à r 76,5, à midi. Pour les 8 variantes, ce pixel doit avoir
    un alpha à 0 et le HitTest ne doit rien y toucher.
  - Le RED échouait sur Braises (alpha 255, Polygon touché). Arcs passait déjà.

## Sort de TickReset

Le pinceau est conservé, avec ses gardes (`ThemingTests`). Il a encore trois consommateurs :
- `CadranArcsView.xaml` (tirets de reset 5 h) ;
- `VueJourView.xaml` ;
- `VueSemaineView.xaml`.

Seul le commentaire de `ChronosTheme.cs` a changé : il ne mentionne plus la flèche.

## Porte

- `dotnet build Chronos.sln -c Debug -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln -c Debug` : 2 249 réussis, 0 échec, 1 ignoré, total 2 250.
- `dotnet build Chronos.sln -c Release -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln -c Release` : 2 249 réussis, 0 échec, 1 ignoré, total 2 250.
- Gardes documentaires relancées après l'édition de publish.md : 20 / 20.
- `grep "FlecheReset\|Polygon" CadranBraisesView.xaml` : aucune occurrence.

## L'exe reconstruit

| Champ | Valeur |
|-------|--------|
| Chemin | `C:\Users\Tanguy\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.5.0.exe` |
| Taille | 78 200 218 o (< 120 000 000) |
| FileVersion / ProductVersion / ProductName | 3.5.0.0 / 3.5.0 / Chronos (lus par `VersionInfo`, sans lancer) |
| SHA-256 | `041f1f0780756a6896fdac70a41d74a44b02159679eac3e979d41a9f22815419` |
| Sortie de publish | `Chronos.exe` + `Chronos.pdb`, 0 DLL ; SHA-256 identique à la copie |
| Empreinte précédente | `adea832f…` (78 200 158 o) : barrée dans 43-CONSTAT.md, remplacée dans publish.md §2 et §4 |
| git | ignoré (`/Chronos-v*.exe`) ; 8 exe à la racine |

Avant la copie, `Get-Process` (0 processus `Chronos*` sur 585) et `tasklist` ont été consultés en lecture seule.
`Chronos-v3.5.0` ne tournait pas, donc la copie a été faite. Aucun exe n'a été lancé et aucun processus n'a été arrêté.

## Écarts par rapport au plan

Deux commentaires de plus que prévu mentionnaient la flèche. Ils ont été corrigés dans le commit GREEN :
- `ChronosTheme.cs` l.83 ;
- `EmberRingControl.cs` l.11 (« retour sur la flèche » devient « retour à midi »).

## Known Stubs

Aucun.

## Commits

- 6bcd2a4 — test(43-07) : la vue Braises n'a plus de flèche de reset (RED)
- 6c73bf5 — fix(43-07) : flèche de reset de Braises retirée
- bf799d1 — release(43-07) : Chronos 3.5.0 reconstruit, flèche de Braises retirée

Pas d'étiquette, pas de push. STATE.md, ROADMAP.md et REQUIREMENTS.md n'ont pas été modifiés.

## Self-Check: PASSED

- FOUND: Chronos-v3.5.0.exe (SHA-256 041f1f07… recalculé)
- FOUND: 6bcd2a4, 6c73bf5, bf799d1
