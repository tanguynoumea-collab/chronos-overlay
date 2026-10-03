---
phase: 38-historique-pistes-seul-et-plein-cran
plan: 02
subsystem: historique-reglages
tags: [wpf, xaml, settings, historique, suppression, HIS-09]
requires:
  - "38-01 (vue Semaine à grille unique, plus dépendante du style)"
  - "phase 36 (lecture tolérante : DerniereLecture, DernieresRetombees, JsonUnmappedMemberHandling.Skip)"
provides:
  - "ChronosSettings sans HistoriqueStyleSemaine (enum et propriété supprimées)"
  - "Fenêtre Historique sans sélecteur de style (la place à droite de la ligne de fraîcheur est libre pour le bouton plein écran)"
  - "Carte Historique des réglages sans puces de style, mention « Aussi : double-clic au centre du cadran » conservée"
  - "Témoin : un ancien settings.json avec Tuiles / Simplifie / Pistes se lit sans perte et perd la clé au Save"
affects:
  - "38-05 / 38-06 (plein écran : bouton à la place du sélecteur)"
  - "phase 42 (reformulation de la mention du double-clic)"
tech-stack:
  added: []
  patterns: ["tests de non-existence par réflexion (GetProperty == null) pour verrouiller une suppression"]
key-files:
  created: []
  modified:
    - src/Chronos/Services/ChronosSettings.cs
    - src/Chronos/ViewModels/Historique/HistoriqueViewModel.cs
    - src/Chronos/Text/TextesHistorique.cs
    - src/Chronos/Views/Historique/HistoriqueWindow.xaml
    - src/Chronos/Views/Reglages/ReglagesWindow.xaml
    - README.md
    - tests/Chronos.Tests/SettingsServiceTests.cs
    - tests/Chronos.Tests/HistoriqueBindingTests.cs
    - tests/Chronos.Tests/HistoriqueViewModelTests.cs
    - tests/Chronos.Tests/ReglagesBindingTests.cs
    - tests/Chronos.Tests/ReglagesWindowTests.cs
    - tests/Chronos.Tests/TextesHistoriqueTests.cs
    - tests/Chronos.Tests/GardeDocumentationHistoriqueTests.cs
decisions:
  - "Aucune migration de settings.json : le membre supprimé est ignoré à la lecture (JsonUnmappedMemberHandling.Skip), ce n'est pas une retombée, et il disparaît au prochain Save"
  - "La fixture settings-valeurs-inconnues.json de la phase 36 reste inchangée : son HistoriqueStyleSemaine Mosaique est désormais un membre inconnu ; les retombées attendues passent de 4 à 3 enums"
  - "xmlns:txt retiré de ReglagesWindow.xaml : il ne servait plus qu'aux puces de style"
metrics:
  duration: "~12 min"
  completed: "2026-10-03"
  tasks: 3
  files: 13
---

# Phase 38 Plan 02 : suppression du réglage de style de la vue Semaine — Summary

`HistoriqueStyleSemaine` n'existe plus : l'enum et la propriété ont quitté `ChronosSettings`, et le VM n'a plus `Style`, `IsStyle*` ni `ChoisirStyle`. Côté textes, `Style*` et `LibelleStyle` sont partis. Les deux sélecteurs ont disparu : « Style : Pistes · Simplifié · Tuiles » dans la fenêtre Historique, et les puces de la carte des réglages, qui garde « Aussi : double-clic au centre du cadran ». Un témoin vérifie qu'un ancien `settings.json` avec `"Tuiles"`, `"Simplifie"` ou `"Pistes"` se charge sans perdre le thème, le coin, le moniteur, le mode du cadran ni les 10 géométries. Le témoin vérifie aussi deux autres points : la lecture n'est pas signalée comme une retombée, et la clé disparaît au `Save`. Le README décrit désormais un seul style.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | Production : enum et propriété, style du VM, textes, sélecteur de la fenêtre et puces des réglages retirés | f62fc95 |
| 2 | Tests : témoin `[Theory]` `Un_ancien_reglage_de_style_d_historique_se_lit_sans_perte`, `La_propriete_HistoriqueStyleSemaine_n_existe_plus`, `Le_vm_n_expose_plus_de_style`, `La_carte_Historique_n_a_plus_de_selecteur_de_style` ; tests de la phase 36 et des vues adaptés | 01f6341 |
| 3 | README : « Les pistes de la Semaine » (une phrase), puce Fenêtres 5 h retirée ; la garde de documentation refuse les styles retirés | 32f1276 |

## Vérification

- `dotnet test Chronos.sln -c Debug` lancé deux fois : 1700/1700, 0 échec (1697 → 1700 : +3 cas du témoin, +1 test de non-existence, −1 `Les_styles_ont_leurs_noms` ; le test de style du VM est remplacé par un test de non-existence).
- `dotnet build Chronos.sln -c Release` : 0 avertissement, 0 erreur ; build Debug du projet principal : 0/0.
- Dans `src`, `git grep HistoriqueStyleSemaine` ne trouve plus que le commentaire `//` de `ChronosSettings.cs`, que le plan demande d'ajouter. Dans `tests`, ne restent que les tests de non-existence, le témoin et la fixture de la phase 36.
- `git grep "ChoisirStyleCommand\|HistoriqueStyleSemaine\.\(Pistes\|Simplifie\|Tuiles\)" -- tests` : vide.
- README : `grep -cE "Trois styles|Simplifié|Tuiles|Fenêtres 5 h"` donne 0 ; « ### Les pistes de la Semaine » ×1 ; « double-clic au centre du cadran » présent.
- Les gardes de doctrine, `ServicesLayerPurityTests` et `CompositionRootTests` passent sans modification.

## Écarts par rapport au plan

Aucune correction automatique. Ajustements de détail :
- **Critère contradictoire :** « `git grep -c HistoriqueStyleSemaine -- src` ne rend rien » s'oppose à l'action 1, qui impose un commentaire contenant ce nom. J'ai gardé le commentaire, comme le demande l'action. La seule occurrence restante est donc ce commentaire.
- `xmlns:txt` retiré de `ReglagesWindow.xaml`, devenu sans usage après la suppression des puces. `xmlns:svc` est gardé (`SessionStyle`).
- `Le_segment_4_semaines_est_actif_et_ouvre_la_vue` (HistoriqueBindingTests) vérifiait que `SelecteurStyle` était replié : l'assert a été retiré, puisque `Le_segment_actif_se_voit` vérifie déjà que l'élément n'existe plus. Sa doc XML ne parle plus du sélecteur.
- Doc XML de `Fixture_valeurs_inconnues_ne_coute_que_les_valeurs_fautives` : « quatre enums » devient « trois enums », et le style supprimé est cité parmi les membres ignorés.
- La marge haute du TextBlock « Aussi : double-clic… » est inchangée : son voisin précédent, la ligne d'état du journal, a une marge de 4, sans double espacement.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND : .planning/phases/38-historique-pistes-seul-et-plein-cran/38-02-SUMMARY.md
- FOUND : f62fc95, 01f6341, 32f1276
