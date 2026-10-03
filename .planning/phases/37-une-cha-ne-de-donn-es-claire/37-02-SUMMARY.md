---
phase: 37-une-cha-ne-de-donn-es-claire
plan: 02
subsystem: services, viewmodel, démarrage
tags: [purge-r5, orphelins, gardes]
requires: ["37-01"]
provides:
  - "GardesPerimetreTests.Aucun_maillon_retire_de_la_chaine_ne_subsiste (liste de l'étape 2, à faire grandir aux étapes 3, 4, 5)"
affects: [37-03, 37-04, 37-05, 37-06]
tech-stack:
  added: []
  patterns: ["garde de non-retour par réflexion (types + membres du VM), avec assertion anti-mutisme"]
key-files:
  created: []
  deleted:
    - src/Chronos/Services/FiveHourWindowInference.cs
    - src/Chronos/Services/WeeklyWindow.cs
    - tests/Chronos.Tests/FiveHourWindowInferenceTests.cs
    - tests/Chronos.Tests/WeeklyWindowTests.cs
  modified:
    - src/Chronos/Services/Historique/BornesPlage.cs
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/BornesPlageTests.cs
    - tests/Chronos.Tests/MainViewModelTests.cs
    - tests/Chronos.Tests/ReglagesBindingTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs
decisions:
  - "Conflit C1 respecté : ChronosSettings.OAuthUsageEnabled reste (lu par GatedOAuthUsageProvider jusqu'à l'étape 3)"
  - "Les_DEUX_interrupteurs_sont_INDEPENDANTS_sur_disque renommé L_interrupteur_de_la_sonde_persiste_sans_toucher_aux_autres_reglages ; il vérifie coin, arrière-plan et mode du cadran (pas OAuthUsageEnabled, qui part à l'étape 3)"
  - "Test de dérive DST : pas fixe de 7 × 24 h calculé en ligne (fonction locale FinParPasFixe), assertions inchangées"
metrics:
  duration: "≈ 10 min"
  completed: 2026-10-03
  tasks: 2
  files: 11
requirements: [DAT-02]
---

# Phase 37 Plan 02 : étape 2 de la purge, orphelins retirés — Summary

`FiveHourWindowInference`, `WeeklyWindow`, la commande `ToggleOAuthUsage` (avec `IsOAuthUsageEnabled`) et la migration
ponctuelle `PurgerPrefixe("desktop:")` du démarrage sont retirés en un seul commit réversible. Une garde de non-retour par
réflexion les empêche de revenir.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | Orphelins supprimés (`git rm`), commande OAuth retirée du VM, migration `desktop:` retirée du démarrage, tests adaptés | d6912ab |
| 2 | Garde `Aucun_maillon_retire_de_la_chaine_ne_subsiste` ajoutée, garde de câblage `desktop:` retirée ; commit d'étape | d6912ab |

Un seul commit pour l'étape, comme le plan le prescrit (la tâche 1 n'a pas été commitée à part).

## Ce qui a changé

- `BornesPlage.cs` : le commentaire ne nomme plus `WeeklyWindow` ni `WeeklyRecalibration` (« le reset hebdo vient du
  serveur ; l'ancre enregistrée n'est lue qu'en secours, ici ») et renvoie au test renommé.
- `BornesPlageTests` : `La_derive_de_WeeklyWindow_est_mesurable` devient `La_derive_DST_d_une_semaine_calendaire_est_mesurable`,
  avec un pas fixe calculé en ligne. Il vérifie toujours l'écart d'exactement une heure (22:00Z au lieu de 23:00Z) et la
  coïncidence avant le 25/10.
- `MainViewModel` : champ, initialisation et commande retirés. Les docs HDR-06 (`IsSondeEnTetesActivee`, `ToggleSondeEnTetes`)
  sont réécrites sans eux. `ChronosSettings.OAuthUsageEnabled` n'est pas touché.
- `App.xaml.cs` : le bloc SRC-02 et son commentaire sont retirés. CYC-01 dit maintenant « Même régime que la réconciliation
  ci-dessus ». `ArchiveStore.PurgerPrefixe` reste, et `ArchiveStorePurgeTests` reste vert.
- `MainViewModelTests` : 3 tests OAuth supprimés, le test des deux interrupteurs est réduit à la sonde (voir décisions).
- `ReglagesBindingTests` : `Assert.NotSame(vm.ToggleOAuthUsageCommand, …)` retiré et commentaires réécrits. La preuve
  `Assert.Same(vm.ToggleSondeEnTetesCommand, …)` reste. Le test du miroir ne s'appuyait pas sur `IsOAuthUsageEnabled` :
  seul son commentaire a changé.
- `GardesPerimetreTests` : `Le_demarrage_purge_les_identifiants_fantomes_du_magasin_d_archives` est supprimé.
  `Aucun_maillon_retire_de_la_chaine_ne_subsiste` est ajouté après la garde app-bureau : il vérifie les types retirés,
  l'absence des propriétés `IsOAuthUsageEnabled` / `ToggleOAuthUsageCommand` sur `MainViewModel`, et contient une
  assertion anti-mutisme sur `RateLimitHeaderUsageProvider`.

## Vérification

- `dotnet build Chronos.sln -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln` : 1751 / 1751 verts. On passe de 1765 à 1751 : il y a 2 fichiers de tests en moins, 3 tests OAuth
  et une garde de câblage retirés, et une garde ajoutée.
- Greps d'acceptation : `FiveHourWindowInference|WeeklyWindow\b|ToggleOAuthUsage|IsOAuthUsageEnabled` dans src/tests ne
  trouve que la garde de non-retour et la liste `TermesRetires` de `GardeDocumentationChaineTests` (37-01, une garde
  documentaire voulue). `PurgerPrefixe` apparaît 0 fois dans App.xaml.cs, `public int PurgerPrefixe` 1 fois dans
  ArchiveStore.cs, `OAuthUsageEnabled` 3 fois dans ChronosSettings.cs.
- Réversibilité testée à blanc : `git revert --no-commit HEAD` puis build (0 avertissement, 0 erreur), puis `git revert --abort`.

## Deviations from Plan

None - plan executed exactly as written.

Note : le premier build a échoué (CS1026). Le script d'édition avait écrit de vrais sauts de ligne à la place des `\n` dans
le message de la garde. Corrigé avant le commit, sans effet sur le contenu.

Aucun stub.

## Self-Check: PASSED

- ABSENT (attendu) : src/Chronos/Services/FiveHourWindowInference.cs, src/Chronos/Services/WeeklyWindow.cs
- FOUND : commit d6912ab
