---
phase: 42-un-geste-sur-toute-la-silhouette
plan: 04
subsystem: docs/gestes
tags: [tdd, gst-01, documentation, reglages, readme]
requires:
  - "42-03 (répartiteur unique sur Racine, CentreHit supprimé)"
provides:
  - "Carte Historique des réglages : « Aussi : double-clic sur le cadran »"
  - "README : puce « Gestes, sur tout le cadran » (les cinq gestes de la table R7)"
  - "docs/publish.md : puce « Un geste sur tout le cadran » dans la note 3.5.0"
  - "Garde GardeGestesCadranTests.La_documentation_des_gestes_suit_le_geste_unique"
affects:
  - "43 (constat utilisateur des clics réels)"
tech-stack:
  added: []
  patterns:
    - "Garde documentaire lisant README / docs via l'attribut MSBuild CheminDocsChronos (jamais Assembly.Location)"
key-files:
  created: []
  modified:
    - src/Chronos/Views/Reglages/ReglagesWindow.xaml
    - README.md
    - docs/publish.md
    - tests/Chronos.Tests/ReglagesBindingTests.cs
    - tests/Chronos.Tests/GardeDocumentationHistoriqueTests.cs
    - tests/Chronos.Tests/GardeGestesCadranTests.cs
    - src/Chronos/ViewModels/ArbitreClicCentre.cs (commentaires)
    - src/Chronos/ViewModels/MainViewModel.cs (commentaires)
    - src/Chronos/Services/IOuvreurHistorique.cs (commentaire)
    - src/Chronos/Views/Historique/OuvreurHistorique.cs (commentaire)
decisions:
  - "La puce README « **Au centre** » ne dit plus que les pourcentages ; les gestes ont leur propre puce « **Gestes, sur tout le cadran** », que la garde Le_README_dit_le_vrai_geste_du_centre lit désormais"
  - "Helper CheminDocs dupliqué dans GardeGestesCadranTests (celui de GardeDocumentationHistoriqueTests est privé) plutôt que d'élargir sa visibilité"
  - "Notes historiques (3.3.0 de docs/publish.md, docs/data-sources.md) non réécrites"
metrics:
  duration: "~10 min"
  completed: 2026-10-03
  tasks: 2
  files: 10
---

# Phase 42 Plan 04 : les mots des gestes suivent le geste unique — Summary

Les textes visibles et la documentation disent maintenant le geste unique. La carte Historique des réglages affiche
« Aussi : double-clic sur le cadran ». Le README a une puce « Gestes, sur tout le cadran » :
- clic = bascule ;
- double-clic = Historique ;
- appuyer puis glisser = déplacer puis accrocher ;
- clic droit = Réglages ;
- à côté du cadran, le clic va au bureau.

La note de la 3.5.0 annonce « Un geste sur tout le cadran ». Une nouvelle garde fige ces mots.

## Tâches

| # | Nom | Commit | Fichiers |
|---|-----|--------|----------|
| 1 | RED : tests des mots (réglages, README, note 3.5.0) | e3a5eae | ReglagesBindingTests.cs, GardeDocumentationHistoriqueTests.cs, GardeGestesCadranTests.cs |
| 2 | GREEN : carte Historique, README, note 3.5.0 | 80590eb | ReglagesWindow.xaml, README.md, docs/publish.md |
| + | Commentaires « clic au centre du cadran » périmés (demande de l'orchestrateur) | 12f47b2 | ArbitreClicCentre.cs, MainViewModel.cs, IOuvreurHistorique.cs, OuvreurHistorique.cs |

## Vérification

- RED : 5 échecs, tous par assertion, et le projet compile :
  - les deux tests des réglages ;
  - les mots du plan dans le README ;
  - le vrai geste du centre ;
  - la nouvelle garde.
- GREEN :
  - suite complète 1922/1922 ;
  - `dotnet build Chronos.sln` : 0 avertissement, 0 erreur, en Debug comme en Release.
- Comptes d'acceptation :
  - `ReglagesWindow.xaml` : « Aussi : double-clic sur le cadran » 1, « au centre du cadran » 0 ;
  - `README.md` : « au centre du cadran » ou « clic au centre » 0, « double-clic sur le cadran » 1 ;
  - `docs/publish.md` : « Un geste sur tout le cadran » 1, « double-clic au centre du cadran » 1 (la note 3.3.0, intacte).
- `grep "centre du cadran|clic au centre"` sur `src/**/*.cs, *.xaml` : 0 occurrence.

## Écarts au plan

### Corrections automatiques

**1. [Rule 3 - Blocage] Garde `Le_README_dit_le_vrai_geste_du_centre` ajustée**
- **Trouvé pendant :** la tâche 1, à la lecture de `GardeDocumentationHistoriqueTests`.
- **Problème :** le plan coupe la puce « **Au centre** » en deux. Cette garde exigeait « double-clic » et « Historique » sur la ligne « **Au centre** » : elle serait devenue rouge au GREEN.
- **Correction :** la ligne « **Au centre** » doit toujours être unique. « double-clic » et « Historique » sont maintenant attendus sur la ligne unique « **Gestes, sur tout le cadran** ».
- **Commit :** e3a5eae.

### Ajouts demandés par l'orchestrateur
- Commentaires seuls, sans changement de code ni renommage. Fichiers corrigés :
  - `ArbitreClicCentre.cs` (3) ;
  - `MainViewModel.cs` (l.163, 168, 184) ;
  - `IOuvreurHistorique.cs` ;
  - `OuvreurHistorique.cs`.

  Aucune garde textuelle ne s'y opposait. Les noms de tests et les commentaires de tests (`ArbitreClicCentreTests`, `MainViewModelTests`) sont hors périmètre et n'ont pas été modifiés.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND : src/Chronos/Views/Reglages/ReglagesWindow.xaml, README.md, docs/publish.md, tests/Chronos.Tests/GardeGestesCadranTests.cs
- FOUND : e3a5eae, 80590eb, 12f47b2
