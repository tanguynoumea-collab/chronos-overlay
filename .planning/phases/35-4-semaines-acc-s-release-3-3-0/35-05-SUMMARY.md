---
phase: 35-4-semaines-acc-s-release-3-3-0
plan: 05
subsystem: documentation / composition — README « Historique d'utilisation », data-sources §9, publish 3.3.0, fuseau du diagnostic
tags: [ACC-04, ACC-03, README, data-sources, publish, garde-de-vocabulaire, GardeDocumentationHistoriqueTests, fuseau-injecte, CRLF, TDD, mutations]

sha_entree_de_plan: a00aafd
one_liner: "Le README gagne une section « ## Historique d'utilisation » (deux gestes d'ouverture, trois vues, trois styles avec leurs pistes, règles d'honnêteté, journal) écrite avec les mots du plan §4 et sans aucun mot de projection ; la ligne « Au centre » dit le vrai geste (clic = bascule après le délai de double-clic ≈ 0,5 s, double-clic = Historique) et la ligne Stack « plus de 1 600 tests ». docs/data-sources.md gagne le §9 « Lecture par la fenêtre Historique » (façade, InstantDAnalyse, une seule lecture pour 4 semaines, une source par vue, veille de minuit, divergence 0,01, diagnostic par la même façade, 8 écarts connus des maquettes) et sa ligne finale nomme §8 ET §9 ; publish.md dit le premier lancement de la 3.3.0 ; le diagnostic reçoit en production le fuseau de la racine de composition. Garde nouvelle limitée à la section du README et au §9 (6 faits) + 1 garde de composition ; suite 1633 / 0 deux fois, Release 0 avertissement."

requires:
  - phase: 35-01
    provides: "vue 4 semaines côté données (LireQuatreSemaines, EchelleTemps.Reporter, étiquettes, épuisée), veille de minuit (LectureVeille, D-35-04 / D-35-06)"
  - phase: 35-02
    provides: "gestes (ArbitreClicCentre, GetDoubleClickTime ≈ 500 ms), ouvreur singleton, carte F1 « Historique d'utilisation »"
  - phase: 35-03
    provides: "DiagnosticService(…, TimeZoneInfo? fuseau = null), section [Journal d'historique], GardeDiagnosticHistoriqueTests"
  - phase: 34-08
    provides: "motif Projection de GardeVocabulaireHistoriqueTests (copié tel quel)"
provides:
  - "README.md — section « ## Historique d'utilisation » (Ouvrir, Trois vues, Trois styles pour la Semaine, Règles d'honnêteté, Le journal) ; ligne « Au centre » ; Stack « plus de 1 600 tests unitaires »"
  - "docs/data-sources.md — « ## 9. Lecture par la fenêtre Historique » (Plages, Règles de lecture, Écarts connus des maquettes) ; ligne finale « … ; §8 agrégats de tokens ; §9 lecture par la fenêtre Historique) »"
  - "docs/publish.md §7 — « ### Premier lancement de la 3.3.0 »"
  - "src/Chronos/App.xaml.cs — fuseau: sp.GetRequiredService<TimeZoneInfo>() dans l'enregistrement du DiagnosticService (CRLF conservé)"
  - "tests/Chronos.Tests/GardeDocumentationHistoriqueTests.cs — 6 faits ; GardeDiagnosticHistoriqueTests +1 fait"
affects:
  - "35-06 (release 3.3.0) : documentation prête, ACC-04 à cocher par 35-06"
  - "35-07 / VAL-05 : la section [Journal d'historique] du rapport parle désormais dans le fuseau de l'utilisateur"

tech-stack:
  added: []
  patterns:
    - "Garde de vocabulaire bornée à UNE section de document (SectionDe) : le reste du document peut dire légitimement un mot banni ailleurs"
    - "Ligne finale d'un document gardée par deux tests (33-05 et 35-05) : un paragraphe ajouté se date sans effacer le précédent"

key-files:
  created:
    - tests/Chronos.Tests/GardeDocumentationHistoriqueTests.cs
  modified:
    - README.md
    - docs/data-sources.md
    - docs/publish.md
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/GardeDiagnosticHistoriqueTests.cs

key-decisions:
  - "D-35-18 — garde limitée à la section : GardeDocumentationHistoriqueTests applique le motif Projection de 34-08 (+ « mesure ») à la section « ## Historique d'utilisation » du README et au §9 SEULEMENT ; le reste du README décrit l'estimation de repli du cadran, qui existe encore"
  - "D-35-19 — honnêteté écrite sans les mots bannis : « relevés exacts du serveur », « rien n'annonce l'avenir », « aucun trou n'est interpolé », « aucune courbe n'est reconstituée » — jamais « aucune projection » ni « pas une estimation »"

requirements-completed: []
requirements-advanced: [ACC-04]

metrics:
  duration: 11min
  completed: 2026-09-27
  tasks: 3
  files: 6
  tests_added: 7
---

# Phase 35 Plan 05 : README, data-sources §9, publish 3.3.0, fuseau du diagnostic — Summary

**La documentation de la release 3.3.0 dit ce que la fenêtre Historique montre et comment elle refuse d'inventer, avec les mots
du plan §4, sous une garde qui ne vise que ses propres sections. Le diagnostic parle à l'heure de l'utilisateur.**

ACC-04 : contribution seulement ; `requirements mark-complete` non lancé (35-06 le clôt). ROADMAP.md / STATE.md non touchés.

## Performance

- **Durée :** ≈ 11 min (17:13Z → 17:24Z)
- **Tâches :** 3 (TDD : 3 RED + 3 GREEN, 6 commits)
- **Fichiers :** 1 créé, 5 modifiés
- **Tests :** +7 (6 `GardeDocumentationHistoriqueTests`, 1 `GardeDiagnosticHistoriqueTests`) ; suite complète **1633 / 0**
  deux fois de suite sur l'arbre réel (avec les commits de 35-04) ; `dotnet build -c Release` : 0 avertissement, 0 erreur.

## Commits

| Tâche | RED | GREEN |
|------|-----|-------|
| 1. Section README + garde | `2582405` (RED 4 : section introuvable, « re-clique », « 1 100 ») | `db69b66` |
| 2. data-sources §9 + publish | `d27852b` (RED 2 : §9 introuvable, ligne finale sans §9) | `2051c2f` |
| 3. Fuseau du diagnostic | `c280e9b` (RED 1 : argument absent) | `173d716` |

Tous en `--no-verify`, fichiers indexés un par un, aucun `--amend`.

## Sections écrites

- **README « ## Historique d'utilisation »** (entre « Widget de sessions Claude Code » et « Prérequis ») : *Ouvrir* (double-clic au
  centre du cadran ; Réglages → carte « Historique d'utilisation » → « Ouvrir » ; retour au premier plan ; Échap / ✕ ; prix du
  simple clic ≈ 0,5 s) · *Trois vues* (Semaine de forfait, Jour avec « 288 relevés attendus » et veille de minuit, 4 semaines d'après
  DESIGN_PLAN §2.4 : courante en couleur, précédentes en gris, « pas de relevés (avant le journal) », plateau « épuisée <jour> HH:MM →
  bloquée jusqu'au reset », pas de piste tokens) · *Trois styles* (table Pistes / Simplifié / Tuiles et une ligne par piste) ·
  *Règles d'honnêteté* · *Le journal* (`%APPDATA%\Chronos\historique\`, dernière écriture, alerte 15 min, reconstruction au premier
  lancement). Aucun autre paragraphe du README modifié hors « Au centre » et « Stack ».
- **Total de tests cité :** « plus de 1 600 tests unitaires » (1622 à l'entrée du plan, 1633 à la sortie). La garde exige un nombre
  ≥ 1 600.
- **data-sources §9** : Plages (`BornesPlage`, repli `WeeklyAnchor`), Règles de lecture (`InstantDAnalyse`, une seule lecture,
  `EchelleTemps.Reporter`, D-32-25, veille de minuit, `Divergences.SeuilDelta` = 0,01, diagnostic par `LireJour`), Écarts connus
  des maquettes (8 : coins DWM ≈ 8 px / droits sous Windows 10, badge omis, Segoe UI, tirets de reset dans un trou, pas de réticule
  en 4 semaines, épuisée absente de la galerie, rangées au pas de 16 px, simple clic retardé).
- **publish §7 « Premier lancement de la 3.3.0 »** : quitter tous les anciens exe (3.1.0 … 3.2.2 ; une 3.2.2 encore lancée ferait
  se retirer la 3.3.0), réconciliation inchangée (sauvegarde puis 9 remplacements), reconstruction ≈ 15 s à froid à priorité basse,
  gestes de l'Historique.

## Mutations (par copie, révoquées par re-copie, sha256 identiques avant / après)

| # | Mutation | Rouge nommé | sha256 (avant = après) |
|---|----------|-------------|------------------------|
| m1 | « (estimation) » ajouté à la ligne Rythme de la section | `GardeDocumentationHistoriqueTests.Aucune_projection_dans_la_section_Historique_du_README` — SEUL rouge sur 35 tests documentaires (le reste du README n'est pas gardé) | `README.md` 7e6528284fe1… |
| m2 | « ; §8 agrégats de tokens » retiré de la ligne finale | `ContratAgregatsDocumenteTests.Le_document_ecrit_HYP_4_et_le_paragraphe_7_renvoie_au_8` ET `GardeDocumentationHistoriqueTests.La_ligne_finale_nomme_le_paragraphe_huit_et_le_neuf` | `docs/data-sources.md` 4235b66f4f71… |
| m3 | argument `fuseau:` (et son commentaire) retiré | `GardeDiagnosticHistoriqueTests.Le_fuseau_du_diagnostic_vient_de_la_racine_de_composition` | `src/Chronos/App.xaml.cs` 78c022e3f591… |

`docs/publish.md` final : 163e725d555f….

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Commentaire prescrit incompatible avec la garde prescrite (Task 3)**
- Le commentaire du plan disait « … jamais TimeZoneInfo.Local dans le code neutre », alors que le test du même plan exige UNE
  seule occurrence de `TimeZoneInfo.Local` dans `App.xaml.cs`. Écrit : « … jamais le fuseau local deviné dans le code neutre ».
- **Commit :** `173d716`.

**2. [Ajout] Gardes un peu plus fortes que prescrit**
- `Le_README_dit_le_vrai_geste_du_centre` exige aussi « Historique » sur la ligne « Au centre » et un nombre de tests ≥ 1 600 (pas
  seulement l'absence de « 1 100 »). `Le_paragraphe_neuf_…` exige aussi ≥ 15 lignes non vides et refuse « mesure ».
  `Le_fuseau_…` cherche l'argument DANS l'enregistrement du `DiagnosticService`, pas n'importe où dans le fichier.

**3. [Rule 3 - Blocage] Verrous de build du voisin (35-04)**
- Deux `dotnet test` ont heurté un verrou (`testhost`, `VBCSCompiler`) : attente 45–50 s puis nouvel essai. Les gardes
  documentaires lisent les fichiers au moment du test : les mutations m1 / m2 / m3 ont été jouées avec `--no-build` sur l'arbre
  réel (aucune compilation nécessaire), sans instantané. L'arbre réel est resté vert pendant tout le plan.

## Known Stubs

Aucun.

## Self-Check: PASSED

- Fichier créé présent : `GardeDocumentationHistoriqueTests.cs` ; commits présents : `2582405`, `db69b66`, `d27852b`, `2051c2f`,
  `c280e9b`, `173d716`.
- `grep -c "^## Historique d'utilisation" README.md` = 1 ; `grep -c "double-clic au centre du cadran" README.md` = 1 ;
  « re-clique » = 0 ; « 1 100 » = 0 ; `grep -ci tanguy README.md docs/data-sources.md docs/publish.md` = 0.
- `grep -c "^## 9. Lecture par la fenêtre Historique" docs/data-sources.md` = 1 ; `tail -n 3 … | grep -c "§8.*§9"` = 1.
- `fuseau: sp.GetRequiredService<TimeZoneInfo>()` = 1 et `TimeZoneInfo.Local` = 1 dans `App.xaml.cs`, CRLF (535 / 535).
- Documents en LF (fin de ligne d'origine) ; ROADMAP.md / STATE.md non modifiés.
