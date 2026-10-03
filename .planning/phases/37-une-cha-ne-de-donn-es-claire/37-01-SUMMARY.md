---
phase: 37-une-cha-ne-de-donn-es-claire
plan: 01
subsystem: diagnostic, documentation
tags: [purge-r5, diagnostic, docs, gardes]
requires: []
provides:
  - "Section « [Chaîne de données] » du diagnostic + ligne « Rapport construit en N ms (dont chaîne de données M ms) »"
  - "GardeDocumentationChaineTests (liste TermesRetires, à faire grandir aux étapes 3 et 5)"
  - "Méthodologie unique dans docs/data-sources.md §1-§5, README, CLAUDE.md"
affects: [37-02, 37-03, 37-04, 37-05, 37-06]
tech-stack:
  added: []
  patterns: ["Stopwatch pour une durée de calcul (jamais IClock)", "aide partagée AlerteJournalMuet pour deux sections"]
key-files:
  created:
    - tests/Chronos.Tests/GardeDocumentationChaineTests.cs
  modified:
    - src/Chronos/Services/DiagnosticService.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - src/Chronos/Services/CompositeUsageProvider.cs
    - src/Chronos/Models/SourceReliability.cs
    - src/Chronos/Models/WindowState.cs
    - src/Chronos/Controls/Cadrans/EmberRingControl.cs
    - src/Chronos/Controls/Cadrans/FuseBar.cs
    - src/Chronos/Controls/Cadrans/TideColumn.cs
    - docs/data-sources.md
    - README.md
    - CLAUDE.md
decisions:
  - "Alerte « journal muet » factorisée (AlerteJournalMuet) : même règle, mêmes mots dans [Chaîne de données] et [Magasins persistants]"
  - "Ligne « Dernier exact persisté » : âge du magasin injecté sinon mtime disque (même règle que LigneMagasin), renvoi au détail sous [Magasins persistants]"
  - "Paramètres tokenReader/machine gardés (inutilisés, XML-doc « retiré à l'étape 3 ») ; champs retirés"
metrics:
  duration: "≈ 35 min"
  completed: 2026-10-03
  tasks: 3
  files: 12
requirements: [DAT-01, DAT-04, DAT-05]
---

# Phase 37 Plan 01 : diagnostic « Chaîne de données » et méthodologie unique — Summary

Le diagnostic dit la chaîne réelle (sonde d'en-têtes → secours OAuth du login Chronos → dernier exact → journal) en une
section, ne cherche plus les coffres (≈ 17 s), n'appelle plus le réseau et finit par sa durée mesurée ; README, CLAUDE.md
et docs/data-sources.md décrivent la même chaîne, avec le plancher « ≥ » comme seul chiffre non exact, sous une garde
documentaire neuve.

**DAT-01** : point de contrôle franchi le 2026-10-03 (liste-purge.md validée) — non rejoué.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | Section [Chaîne de données], sections mortes retirées, ligne de durée, garde anti-coffres (TDD : RED 7 échecs constatés, puis GREEN) | 6dcda8f |
| 2 | Commentaires « repli JSONL / estimation » corrigés (Estimated = plancher « ≥ »), aucun identifiant renommé | 6dcda8f |
| 3 | data-sources §1-§5, README, CLAUDE.md réécrits + GardeDocumentationChaineTests ; commit d'étape unique | 6dcda8f |

Commit unique d'étape, comme le plan le prescrit (tâches 1 et 2 non commitées isolément).

## Ce qui a changé

- `DiagnosticService.cs` : `[Réglage]`, `[Source exacte — pont statusLine Claude Code]`, `[Source exacte — endpoint OAuth
  (repli)]`, `[Conseil]` supprimés, avec `UsageUrl`, `DescribeBlobShape`, `Pct`, `using System.Net.Http`, les champs
  `_tokenReader` / `_machine`. Les deux sections « Source exacte » restantes sont absorbées dans `[Chaîne de données]`, lignes
  conservées mot pour mot (indentées de 4). `claudeSettings` redéclaré dans la section widget (piège CS0103). Un seul
  `_composite.GetAsync`. Fraîcheur dérivée de `DoctrineFraicheur.LimiteAge`, seuil muet de `JournalReleves.SeuilMuet`.
- Tests : 3 tests adaptés/renommés, 4 nouveaux (`Le_rapport_ne_contient_plus_les_sections_mortes`,
  `Le_rapport_finit_par_sa_duree_de_construction`, `Le_rapport_decrit_la_chaine_dans_l_ordre`,
  `Le_diagnostic_ne_cherche_plus_les_coffres_ni_n_appelle_le_reseau`), commentaires « gardée par if (token is not null) »
  corrigés.
- `docs/data-sources.md` : nouvel en-tête (schéma de la chaîne), §1-§5 réécrits ; §2 garde mot pour mot le paragraphe SUB-01 ;
  §7 HYP-3 reformulée sans nom de classe (date `2026-10-30T23:00Z` et argument DST gardés) ; dernière ligne datée phase 37
  avec « §8 ». La ligne `source` du §7 est laissée (étapes 3 et 5).

## Vérification

- `dotnet build Chronos.sln -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln` : 1765 / 1765 verts.
- Greps d'acceptation : 0 terme retiré dans les 3 documents ; 0 terme interdit dans DiagnosticService.cs ; `git show --stat HEAD`
  = exactement les 12 fichiers de `files_modified`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocage] Test `Rapport_nomme_les_reglages_retombes_sur_leur_defaut` bornait sur `[Réglage]`**
- **Trouvé pendant :** tâche 1 (non listé dans l'action du plan).
- **Correction :** la borne devient `[Magasins persistants]` → `[Journal d'historique]`, et l'assertion vérifie que la ligne
  « Réglages (settings.json) » y figure (au lieu de son absence de `[Réglage]`).
- **Fichier :** tests/Chronos.Tests/DiagnosticServiceTests.cs — commit 6dcda8f.

**2. [Rule 1 - Bug] README : le titre de section ne contenait que « Sonde d'en-têtes » (majuscule)**
- La garde `Le_README_nomme_la_sonde_d_en_tetes` (ordinale) exige « sonde d'en-têtes » ; phrase d'introduction ajustée.

Aucune autre déviation. Aucun stub.

## Self-Check: PASSED

- FOUND: tests/Chronos.Tests/GardeDocumentationChaineTests.cs
- FOUND: commit 6dcda8f
