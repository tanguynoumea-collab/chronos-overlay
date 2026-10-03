---
phase: 36-socle
plan: 02
subsystem: demarrage
tags: [soc-02, arguments, verrou, liste-blanche, gardes-textuelles]
requires: []
provides:
  - "ArgumentsDemarrage.Trier (tri PUR des arguments, liste blanche ModesConnus)"
  - "ModeDemarrage.ArgumentInconnu : sortie silencieuse code 0 avant verrou"
affects:
  - "Phase 37 (retrait de --statusline : il tombera de lui-même dans ArgumentInconnu)"
tech-stack:
  added: []
  patterns: ["classificateur pur en liste blanche + switch en tête d'OnStartup", "garde textuelle d'ordre sur la source"]
key-files:
  created:
    - src/Chronos/Services/ArgumentsDemarrage.cs
    - tests/Chronos.Tests/ArgumentsDemarrageTests.cs
  modified:
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs
decisions:
  - "Le tri des arguments est une fonction pure de Chronos.Services (sans WPF), en liste blanche : tout --xxx absent de ModesConnus donne ArgumentInconnu"
  - "case ModeDemarrage.StatusLine est facultatif dans la garde de placement, pour que la phase 37 le retire sans ambiguïté"
metrics:
  duration: "~15 min"
  completed: 2026-10-03
  tasks: 2
  files: 4
---

# Phase 36 Plan 02 : garde d'arguments (SOC-02) Summary

Un tri pur en liste blanche (`ArgumentsDemarrage.Trier`) décide du mode en première instruction d'`OnStartup`. Tout `--xxx` inconnu ou retiré sort en code 0, en silence, avant `base.OnStartup`, le verrou mono-instance, le Host et la réconciliation des hooks. Les gardes de placement ont été réécrites sur les `case ModeDemarrage.*` et prouvent autant qu'avant, plus l'ordre du tri.

## Tâches

| # | Tâche | Commits |
|---|-------|---------|
| 1 | Tri pur `ArgumentsDemarrage` en liste blanche, en TDD | `59cb049` (RED : tests), `d21f845` (GREEN : implémentation) |
| 2 | `OnStartup` aiguillé par le tri + gardes de placement réécrites + nouvelle garde d'ordre | `6eb1fda` |

## Ce qui a été fait

- `src/Chronos/Services/ArgumentsDemarrage.cs` contient `enum ModeDemarrage`, `record InvocationDemarrage(Mode, EvenementHook)` et la classe statique `ArgumentsDemarrage` (constantes, `ModesConnus`, `Trier`). La préséance est celle de la 3.4.0 : `--statusline` > `--hook` > `--cadrans` > `--sessions` > `--historique`. La recherche porte sur tous les arguments et ignore la casse.
- `ArgumentsDemarrageTests` : une table de vérité de 21 `InlineData`, la preuve que chaque mode connu est reconnu (`Count == 5`), et un échantillon de 7 arguments absents de la liste blanche qui donnent tous `ArgumentInconnu`.
- `App.xaml.cs` : les cinq `if` sur des littéraux sont remplacés par `var invocation = ArgumentsDemarrage.Trier(e.Args); switch …`. `ArgumentInconnu` est le premier `case`. Les commentaires d'origine sont conservés dans chaque `case`. Les commentaires périmés (« lignes 20 et 30 », la liste CPT-03) sont mis à jour. Le reste d'`OnStartup` n'a pas changé.
- `GardesPerimetreTests` :
  - `Le_verrou_mono_instance_est_pose_apres_les_court_circuits_et_avant_le_Host` repère maintenant les `case ModeDemarrage.*`. `ArgumentInconnu`, `Hook`, `GalerieCadrans`, `GalerieSessions` et `GalerieHistorique` sont obligatoires ; `StatusLine` est facultatif. La garde vérifie aussi que le tri vient avant le verrou. Les vérifications existantes sont conservées : acquisition unique, verrou avant le Host, « tourne déjà », `Shutdown();`, pas de `.Kill(`.
  - `Le_mode_historique_precede_le_verrou_et_ne_resout_aucun_service` part maintenant du repère `case ModeDemarrage.GalerieHistorique`. Ses interdits sont inchangés.
  - Nouvelle garde `Le_tri_des_arguments_precede_le_verrou_et_l_inconnu_sort_en_silence` :
    - le tri est la première instruction, avec seulement des commentaires avant lui ;
    - il vient avant `base.OnStartup(e)`, le verrou, le Host et `.Reconcile(` ;
    - la branche inconnue contient `Environment.Exit(0)` et aucun de ces éléments : MessageBox, `.Show(`, service, stderr, `Console.`, réconciliation ;
    - il ne reste plus de `e.Args.Any(` ni de `Array.FindIndex(e.Args`.

## Vérification

- Filtre de la tâche 2 : 280/280 verts.
- `dotnet build Chronos.sln` : 0 erreur, 0 avertissement.
- `dotnet test Chronos.sln` : 1753/1753 verts. Ce chiffre inclut les commits 36-01 du voisin, déjà en place à ce moment.
- Critères d'acceptation vérifiés par grep :
  - `Trier(e.Args)` = 1 ;
  - `case ModeDemarrage.ArgumentInconnu` = 1 ;
  - 0 littéral `"--xxx"` dans App.xaml.cs ;
  - 0 `IndexOf("\"--` dans GardesPerimetreTests ;
  - acquisition du verrou = 1.
- Smoke manuel sur l'exe réel (`--zzz` doit sortir en code 0 sans fenêtre) : reporté au constat de la phase 43 (36-VALIDATION.md, Manual-Only). L'exe n'a pas été lancé.

## À faire en phase 37 (retrait de `--statusline`)

Supprimer :
- la constante `ArgumentsDemarrage.StatusLine` ;
- sa ligne dans `ModesConnus` et sa ligne dans `Trier` ;
- la valeur d'enum `ModeDemarrage.StatusLine` ;
- le `case ModeDemarrage.StatusLine` d'`App.xaml.cs`.

Dans `ArgumentsDemarrageTests` :
- passer `Chaque_mode_connu_est_reconnu` à 4 ;
- déplacer `--statusline` dans l'échantillon de `Tout_argument_absent_de_la_liste_blanche_sort_en_silence` ;
- retirer ou réécrire les `InlineData` de la table de vérité qui attendent `StatusLine`.

La garde de placement n'exige pas le `case StatusLine` : il n'y a rien à y changer.

Les arguments sans `--` (`-x`, `/x`, `Stop`) ouvrent l'overlay, comme en 3.4.0. C'est hors du périmètre de la décision.

## Écarts par rapport au plan

Aucun : le plan a été exécuté tel qu'écrit.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND: src/Chronos/Services/ArgumentsDemarrage.cs, tests/Chronos.Tests/ArgumentsDemarrageTests.cs
- FOUND commits : 59cb049, d21f845, 6eb1fda
