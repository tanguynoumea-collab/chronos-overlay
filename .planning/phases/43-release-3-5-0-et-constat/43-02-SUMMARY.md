---
phase: 43-release-3-5-0-et-constat
plan: 02
subsystem: documentation / version
tags: [VAL-06, PKG-7, PKG-R2, DS-MAINT-03, D-03, D-04, release]
requires: [43-01 (limites DS3-01 / DS3-02 au §4 de data-sources)]
provides:
  - version 3.5.0 aux quatre propriétés du csproj, commentaire Chronos-vX.Y.Z.exe
  - README de la 3.5 (cinq cadrans, orientation, huit variantes, SHA-256, nom versionné, limites, plus de 2 200 tests)
  - docs/publish.md §2 (empreinte, étiquette exe-vX.Y.Z), §4, §5 (convergence de l'autostart), §6 (smoke au constat), §7 (3.5.0)
  - garde GardeDocumentationReleaseTests (6 faits)
affects: [43-03 (publication de l'exe), 43-04 (constat)]
tech-stack:
  added: []
  patterns: [découpe de section markdown qui ignore les blocs de code]
key-files:
  created:
    - tests/Chronos.Tests/GardeDocumentationReleaseTests.cs
  modified:
    - src/Chronos/Chronos.csproj
    - README.md
    - docs/publish.md
    - docs/data-sources.md
decisions:
  - "Recours manuel du §5 : « cocher la case » et non « désactiver puis réactiver » — IsEnabled n'est vrai que si le raccourci vise l'exe courant, donc depuis un exe non repointé la case est décochée et la cocher appelle Enable()"
  - "Garde du §6 resserrée : « au constat » et « ne lance JAMAIS l'exe » plutôt que « constat » seul, déjà présent dans « se constatent »"
metrics:
  duration: ~30 min
  completed: 2026-10-04
  tasks: 2
  files: 5
---

# Phase 43 Plan 02 : version 3.5.0 et documentation de la 3.5 — Summary

Le csproj porte la 3.5.0. Le README, `docs/publish.md` et `docs/data-sources.md` décrivent la 3.5 telle que le code la
fait. Les reliquats PKG-7, PKG-R2 et DS-MAINT-03 sont fermés et une garde documentaire les surveille.

## RED / GREEN

| Test | RED (2d01347) | GREEN (7ef8058) |
|------|---------------|-----------------|
| `Le_commentaire_du_csproj_nomme_le_fichier_publie_en_trois_chiffres` | échec | vert |
| `Le_README_publie_sous_le_nom_versionne` | échec | vert |
| `Le_README_dit_les_cadrans_les_orientations_et_les_themes` | échec | vert |
| `Le_README_dit_l_empreinte_a_verifier` | échec | vert |
| `Publish_paragraphe_5_dit_la_convergence_de_l_autostart` | échec | vert |
| `Publish_dit_l_empreinte_et_la_convention_de_tag` | échec | vert |

Filtre RED : 6 échecs sur 6. Aucun test n'était déjà vert.

## Affirmations vérifiées dans le code

- **Cadrans** : `CadranStyle { Arcs, Braises, Fusible, Maree, Volets }`, libellés affichés « Anneaux, Braises, Fusible,
  Marée, Volets » (`MainViewModel` l.492-494). Normal = timeline 24 h colorée par l'usage 5 h, sous-tirets horaires et
  marques de reset 5 h ; Étendu = trois anneaux (`CadranArcsView.xaml`).
- **Orientation** : la carte « Orientation » (Horizontal · Vertical) n'apparaît que pour Fusible, Marée et Volets. Une
  orientation par cadran ; par défaut Marée est verticale, Fusible et Volets horizontaux (`ChronosSettings` l.96-104).
  Huit empreintes (`EmpreinteCadran`).
- **Braises** : 20 braises de 15 min en 5 groupes d'une heure, flèche fixe à midi, 12 braises pour l'hebdo, « ↻ HH:MM »
  en mode temps (`CadranBraisesView.xaml`, `WindowGaugeViewModel`).
- **Thèmes** : 15 thèmes en trois familles Pâle · Classique · Vive (`ChronosTheme.cs` l.224).
- **Autostart** : `ConvergerVersExeCourant` est appelé au démarrage, avant le MainViewModel (`App.xaml.cs` l.120). Un
  raccourci absent n'est jamais créé. Un raccourci déjà conforme est laissé tel quel. Le repointage n'a lieu que si la
  cible a disparu ou si sa version est strictement inférieure. Il n'a jamais lieu depuis `bin` ou `%TEMP%`
  (`EmplacementJetable`), ni si une version est illisible. Les cas Repointe, Ignore et Echec sont écrits au journal
  d'incidents. `DiagnosticService` ne nomme pas l'autostart, ce qui confirme la limite PKG-R2.
- **data-sources** : `CadenceNominale` = 300 s, `LimiteAge` = 300 + 60 s, un seul `CompositeUsageProvider` instancié.
  Le §1 et le §3 le disent déjà.

## Écarts doc ↔ code trouvés et corrigés

- README : « Trois anneaux concentriques » présentés comme la seule forme, alors qu'il y a cinq cadrans et que le mode
  Normal (le défaut) n'a que deux anneaux. Corrigé.
- README : « glissant par les anneaux », alors que le geste vaut sur toute la silhouette. Corrigé.
- README : « choisit le style de la vue Semaine », puce supprimée en phase 38. Retiré.
- README §Construire et §Installation : `Chronos.exe` au lieu du nom versionné, et commande sans `Chronos.csproj`.
  Corrigé (PKG-7).
- README : « plus de 1 600 tests » au lieu de 2 224. Corrigé.
- csproj : commentaire `Chronos-vX.Y.exe` au lieu de `Chronos-vX.Y.Z.exe`. Corrigé (PKG-7).
- publish.md §5 : il ne parlait que de « re-basculer l'autostart » et ignorait la convergence au démarrage. Réécrit
  (PKG-R2, DS-MAINT-03).
- publish.md §7 (paragraphe général) : « Activer “Lancer au démarrage” DEPUIS le nouvel exe » est devenu inexact.
  Remplacé par un renvoi au §5.
- publish.md §6 : le smoke `--hook` était présenté comme fait par l'agent. Il passe au constat utilisateur (D-05).
- data-sources.md : aucun écart réel. Seule la note d'en-tête a été mise à jour.

## Écarts par rapport au plan

1. **[Rule 1 - Bug] Découpe de section de ma propre garde.** Les commentaires `# …` du bloc de code bash du README
   coupaient la section « ## Construire depuis les sources ». Le helper ignore maintenant les lignes entre ```. Ce
   correctif est dans le commit 7ef8058, qui touche donc le fichier de test en plus des quatre fichiers prévus.
2. **[Rule 1 - Bug] Garde existante `Le_README_dit_le_vrai_geste_du_centre`.** Elle exige exactement une ligne
   « **Au centre** ». La réécriture l'avait fait disparaître ; elle est rétablie dans la puce Anneaux.
3. **Recours manuel du §5.** Il est formulé « cocher la case » plutôt que « désactiver puis réactiver » (voir la
   décision plus haut).
4. **Ajout au §7 de la 3.5.0.** Une puce « Autostart » renvoie au §5.

## Vérification finale

- `dotnet build Chronos.sln -warnaserror --no-incremental` : **0 avertissement, 0 erreur**.
- `dotnet test Chronos.sln` : **2 223 réussis, 0 échec, 1 ignoré, total 2 224**.
- Critères grep : `3.5.0` × 4 et `3.4.0` × 0 dans le csproj ; `Chronos-vX.Y.Z.exe` × 1 ; `Chronos-v3.5.0.exe` × 3 dans
  le README ; « plus de 1 600 » et « style de la vue Semaine » × 0 ; `exe-v` × 1 et `SHA-256` × 2 dans publish.md ;
  « Correctif : **re-basculer l'autostart** » × 0 ; aucun CR dans les quatre fichiers.
- Aucun exe construit ni lancé. Aucune modification de STATE.md, ROADMAP.md ou REQUIREMENTS.md. Pas de tag, pas de push.

## Known Stubs

Aucun.

## Commits

- 2d01347 — test(43-02): garde documentaire de la release 3.5.0 (RED)
- 7ef8058 — docs(43-02): version 3.5.0 ; README, publish.md et data-sources de la 3.5 (PKG-7, PKG-R2, DS-MAINT-03)

## Self-Check: PASSED

- FOUND: tests/Chronos.Tests/GardeDocumentationReleaseTests.cs
- FOUND: 2d01347, 7ef8058
