---
phase: 32-compter-juste-puis-journaliser
plan: 06
subsystem: lecture par plage du journal des relevés (records neutres, lecteur mensuel, bornes calendaires locales, analyse pure)
tags: [journal, lecture-par-plage, fixtures-jsonl, calendrier-local, DST, TimeZoneInfo, trous, resets-observes, deltas, saut-non-localise, pur, sans-wpf]

# Dependency graph
requires:
  - phase: 32-04 (JRN-01..03)
    provides: ReleveJournal, EvenementJournal/TypeEvenement, LigneJournal.Parser (tolérance), LecteurJournal.LireFichier, JournalReleves (NomFichier mois UTC, SeuilReprise = 2 × CadenceNominale), fixture tolerance
  - phase: 18-19 (HDR-01..06)
    provides: RateLimitHeaderUsageProvider.CadenceNominale (300 s), WindowKind, SourceUsage, UsageNormalization.InstantDepuisIso
  - phase: 10-11 (ROB-03)
    provides: ChronosSettings.WeeklyAnchor (repli de repère), WeeklyWindow (dont la dérive est mesurée ici, pas corrigée)
provides:
  - Models/Historique/LectureJournal.cs — Plage ([Debut, Fin[, Duree, Contient), LectureJournal, CauseTrou + CauseTrouTexte.Libelle, Trou, ResetObserve, DeltaConsommation, SautNonLocalise, AnalyseJournal
  - Services/Historique/LecteurJournal.Lire(dossier, de, a) — mois UTC chevauchants, filtre [de, a[, tri stable par t, LignesIgnorees, JournalOuvertLe
  - Services/Historique/BornesPlage — SemaineDeForfait / Jour / QuatreSemaines sur le calendrier LOCAL (tz injecté), FuseauParisPourTests
  - Services/Historique/AnalyseReleves.Analyser(lecture, now, cadence, source?) — pur : série d'une source, trous causés, resets observés, Δ de même resets_at, sauts non localisés
  - 8 fixtures JSONL (7 cas) sous tests/Chronos.Tests/TestData/journal/ + FabriqueJournal.JourneeNominale (288 relevés par un vrai JournalReleves)
affects: [34 (les trois vues dessinent exactement ces listes), 35 (couverture, « journal ouvert le »), 33 (les tokens restent sur leur axe : rien ici ne les convertit), CAD-XX v2 (dérive d'une heure de WeeklyWindow/WeeklyRecalibration mesurée par BornesPlageTests.La_derive_de_WeeklyWindow_est_mesurable)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Bornes calendaires : DateTime local (Kind Unspecified) + AddDays + new DateTimeOffset(local, tz.GetUtcOffset(local)) — jamais un TimeSpan de 7 jours, jamais ConvertTimeToUtc sur un Kind.Local du système ; fuseau injecté, TimeZoneInfo.Local seulement au site d'appel"
    - "Analyse pure : now et cadence en paramètres, seuil dérivé (2 * cadence), aucune E/S, aucun type WPF — testable sur des lectures construites en mémoire comme sur des fixtures"
    - "Un fait observé n'est jamais interpolé : la série s'arrête au trou, le Δ n'existe qu'entre relevés consécutifs de même resets_at sans trou, le saut d'une absence est « non localisé » et null si un reset l'a traversé"
    - "Fixtures de journal petites et commitées par cas (releves-AAAA-MM.jsonl dans un dossier par cas), générées au format exact de LigneJournal.Serialiser ; la journée nominale de 288 relevés est FABRIQUÉE par l'écrivain réel (D-32-30)"
    - "Exécution parallèle : instantané `git archive <commit compilable>` sous un nom UNIQUE au plan (snap-32-06) — le scratchpad est PARTAGÉ entre agents parallèles, un nom générique (`snap/`) se fait écraser"

key-files:
  created:
    - src/Chronos/Models/Historique/LectureJournal.cs
    - src/Chronos/Services/Historique/BornesPlage.cs
    - src/Chronos/Services/Historique/AnalyseReleves.cs
    - tests/Chronos.Tests/Fakes/FabriqueJournal.cs
    - tests/Chronos.Tests/LecteurJournalTests.cs
    - tests/Chronos.Tests/BornesPlageTests.cs
    - tests/Chronos.Tests/AnalyseRelevesTests.cs
    - tests/Chronos.Tests/TestData/journal/trou-arrete/releves-2026-09.jsonl
    - tests/Chronos.Tests/TestData/journal/trou-jeton/releves-2026-09.jsonl
    - tests/Chronos.Tests/TestData/journal/reset-5h-milieu-heure/releves-2026-09.jsonl
    - tests/Chronos.Tests/TestData/journal/deux-resets-hebdo/releves-2026-09.jsonl
    - tests/Chronos.Tests/TestData/journal/deux-resets-hebdo/releves-2026-10.jsonl
    - tests/Chronos.Tests/TestData/journal/jour-dst-2026-10-25/releves-2026-10.jsonl
    - tests/Chronos.Tests/TestData/journal/a-cheval/releves-2026-09.jsonl
    - tests/Chronos.Tests/TestData/journal/a-cheval/releves-2026-10.jsonl
  modified:
    - src/Chronos/Services/Historique/LecteurJournal.cs
    - .planning/REQUIREMENTS.md

key-decisions:
  - "D-32-25 — une vue lit UNE source : Analyser retient la source demandée, sinon la plus fréquente de la plage (égalité → ordre de l'enum, SondeEnTetes d'abord)"
  - "D-32-26 — cause d'un trou : dernier arret / jeton_invalide / sonde_refusee dans ]debut − cadence, fin] ; sinon demarrage dans ]debut, fin] → « Chronos arrêté » ; sinon « cause inconnue » ; reprise et ecriture_ratee ne causent rien"
  - "D-32-27 — un reset observé est daté de l'ANCIENNE borne (Instant = r[i−1]) et ObserveA = t[i] ; il est compté même à travers un trou (c'est un fait)"
  - "D-32-28 — Δ = u[i] − u[i−1] seulement si r[i] == r[i−1] (non nuls), u connus des deux côtés, et AUCUN trou entre les deux ; négatif → conservé, Anormal = true"
  - "D-32-29 — fuseau injecté : production TimeZoneInfo.Local, tests « Romance Standard Time » avec repli « Europe/Paris »"
  - "D-32-30 — la journée nominale de 288 relevés est fabriquée par FabriqueJournal via un vrai JournalReleves ; les six autres cas sont des fichiers commités"
  - "JournalOuvertLe parcourt les fichiers conformes par nom (ordinal) et rend la première ligne valide du plus ancien fichier QUI EN A UNE : un vieux fichier entièrement illisible ne cache pas l'ouverture"
  - "Un saut non localisé n'est émis pour une fenêtre que si au moins un des deux relevés porte une valeur ; Delta = null couvre « reset dans l'absence » ET « valeur manquante d'un côté » (documenté sur le record)"
  - "Le test des deux resets hebdo analyse à la cadence d'UN JOUR (la fixture a un relevé par jour) pour que les Δ hebdo existent et soient vérifiés (19 Δ de +0,05, aucun à travers un reset) ; à 300 s, il vérifie que 22 trous coupent tous les Δ et que les 3 resets restent observés"

patterns-established:
  - "Nom d'instantané unique par plan dans le scratchpad partagé (snap-32-06) — un agent parallèle a écrasé `snap/` en cours de plan"
  - "Mutation jouée dans l'instantané, sha256 comparé à l'arbre réel : la révocation est prouvée sur les deux copies"

requirements-completed: [JRN-05]

# Metrics
duration: 28min
completed: 2026-09-27
---

# Phase 32 Plan 06 : Lecture par plage pure et testée (JRN-05) — Summary

**`LecteurJournal.Lire(dossier, de, a)` ouvre les seuls mois UTC qui chevauchent la plage ; `BornesPlage` calcule semaine de forfait / jour / quatre semaines sur le calendrier LOCAL (samedi 00:00 → samedi 00:00, 169 h en octobre 2026, 167 h en mars 2027, jour de 25 h le 25/10) avec fuseau injecté ; `AnalyseReleves.Analyser` est pur et ne dit que ce que les relevés disent — série d'une source, trous > 2 cadences avec leur cause en mots du plan de design, resets 5 h et hebdo observés datés de l'ancienne borne, Δ entre relevés consécutifs de même `resets_at` (jamais à travers un reset ni un trou, négatif = `Anormal`), saut « non localisé » de part et d'autre d'un trou (`null` si un reset l'a traversé) — le tout prouvé sur 7 cas de fixtures et une journée fabriquée de 288 relevés, sans aucun type WPF.**

## Performance

- **Duration:** ≈ 28 min de code (SHA d'entrée `498055d` à 01:42:20Z → dernier commit de code `8ae2a49` à 01:59:58Z), puis suite complète × 2 sur l'arbre réel et SUMMARY
- **Started:** 2026-09-27T01:42:20Z
- **Completed:** 2026-09-27 (voir horodatage du commit de docs)
- **Tasks:** 3 / 3 (TDD : RED → GREEN, 5 mutations jouées et révoquées)
- **Files:** 15 créés, 2 modifiés (`LecteurJournal.cs`, `REQUIREMENTS.md`)
- **Tests :** entrée **1271 / 0** (`498055d`) ; ce plan ajoute **+26** (6 + 8 + 12) — total final sur l'arbre partagé : voir « Suite complète » ci-dessous (32-05 ajoute les siens en parallèle)

## Accomplishments

- **Lecture par plage (critère 4)** : `a-cheval` rend le relevé du 30/09 23:58Z et celui du 01/10 00:03Z, tirés de DEUX fichiers, dans l'ordre ; `[de, a[` strict ; `tolerance` → 4 lignes ignorées, 2 relevés, 2 événements ; dossier absent → vide sans créer le dossier ; `JournalOuvertLe` = `2026-09-14T12:00Z` quelle que soit la plage, `null` sur dossier vide, seconde ligne si la première est invalide ; journée fabriquée → 288 relevés, 1 `demarrage`, 5 valeurs de `R5`.
- **Bornes calendaires** : `2026-09-18T22:00Z → 2026-09-25T22:00Z` depuis le `resets_at` observé, depuis l'ancre `2026-07-11T00:00+02:00`, et sans repère (samedi du calendrier) ; semaine du 24 au 31 octobre 2026 : `2026-10-23T22:00Z → 2026-10-30T23:00Z` = **169 h** ; semaine du 27 mars 2027 : `2027-03-26T23:00Z → 2027-04-02T22:00Z` = **167 h** ; jour du 25/10/2026 : `2026-10-24T22:00Z → 2026-10-25T23:00Z` = **25 h** (la fixture y porte 25 relevés dont deux à 02:xx local), 28/03/2027 = 23 h ; quatre semaines contiguës commençant `2026-08-28T22:00Z` ; toutes les bornes = samedi minuit local même à travers la DST (durées 168/169/168/168).
- **Dérive de `WeeklyWindow` mesurée** : `WeeklyWindow.CurrentStart(ancre, 2026-10-27T12:00Z) + 7 j = 2026-10-30T22:00Z` contre `2026-10-30T23:00Z` pour le calendrier — écart **exactement 1 h** ; avant le 25/10 les deux coïncident. `WeeklyWindow.cs`, `WeeklyRecalibration.cs`, `DayTimeline.cs` non modifiés (diff vide contre `498055d`).
- **Analyse pure** : `trou-arrete` → 1 trou `10:10Z → 11:03Z` « Chronos arrêté », 5 Δ (aucun `10:10 → 11:03`), sauts 5 h `0,14 → 0,20` (+0,06) et hebdo `0,41 → 0,43` (+0,02) ; `trou-jeton` → « jeton invalide », saut 5 h 0,00 ; `reset-5h-milieu-heure` → 1 reset `Instant 13:37Z`, `ObserveA 13:40Z`, 10 Δ (7 avant dont un `Anormal` −0,01 à 13:35, 3 après, aucun `13:35 → 13:40`, somme des positifs 0,09) ; `deux-resets-hebdo` → 3 resets hebdo (18/09, 25/09, 02/10 à 22:00Z), 19 Δ de +0,05 sans traverser un reset ; trou ouvert `Fin = null` à `now − 25 min`, aucun à `now − 6 min` ; 2 × cadence pile n'est pas un trou, 2 × cadence + 1 s l'est ; `demarrage` seul dans le trou → « Chronos arrêté », rien → « cause inconnue », `reprise` seule → « cause inconnue » ; reset pendant le trou → `Delta == null` et le reset compté.

## Task Commits

SHA d'entrée du plan : `498055d` (1271 / 0).

1. **Task 1 — Records, lecteur par plage, fixtures (JRN-05)**
   - RED 6 : `373f221` — `test(32-06): lecture par plage sur fixtures de journal (a cheval, trous, resets, DST), RED 6` (CS0117 : `LecteurJournal.Lire` absent ; records + 8 fixtures + `FabriqueJournal` dans ce commit)
   - GREEN : `ae6916e` — `feat(32-06): LecteurJournal.Lire par plage (mois UTC chevauchants, tri, JournalOuvertLe) ; fixtures de journal (JRN-05)` — filtre : 19 verts (6 + `LigneJournalTests` + `ServicesLayerPurityTests`)
2. **Task 2 — BornesPlage (JRN-05)**
   - RED 8 : `45dd49c` — `test(32-06): bornes de semaine de forfait, jour, quatre semaines sur le calendrier local, DST, RED 8` (CS0103 `BornesPlage`)
   - GREEN : `ce552cb` — `feat(32-06): BornesPlage - semaine de forfait samedi 00:00 local (resets_at 7 j, repli WeeklyAnchor), jour, quatre semaines ; DST 169 h / 167 h (JRN-05)` — filtre : 11 verts (8 + pureté)
3. **Task 3 — AnalyseReleves (JRN-05)**
   - RED 12 : `c8dff30` — `test(32-06): analyse pure des releves - trous causes, resets observes, deltas de meme resets_at, saut non localise, RED 12` (CS0103 `AnalyseReleves`)
   - GREEN : `8ae2a49` — `feat(32-06): AnalyseReleves - serie d'une source, trous > 2 cadences avec cause, resets observes, deltas de meme resets_at, sauts non localises (JRN-05)` — filtre : 26 verts (12 + pureté + `NormalisationUniqueTests` + `GardesDoctrineTests`)

**Plan metadata :** commit `docs(32-06): …` (SUMMARY + REQUIREMENTS.md) — dernier commit du plan.

## Mutations (jouées, rouges nommés, révoquées par copie — sha256 identiques avant/après et égaux à l'arbre réel)

| Mutation | Contenu | Test(s) rougi(s) | Révocation |
|---|---|---|---|
| (r1) | `Lire` n'ouvre que le mois de `de` (`&& mois.Month == premier.Month`) | `LecteurJournalTests.Lire_ouvre_les_seuls_mois_qui_chevauchent_la_plage_et_trie_par_t` | `LecteurJournal.cs` sha256 `9aec9930c03cd8ea…` = avant |
| (b1) | `fin = fin + TimeSpan.FromDays(7)` (7 × 24 h sur l'UTC) au lieu de `Utc(minuit.AddDays(7))` | `BornesPlageTests.La_semaine_du_changement_d_heure_d_octobre_dure_169_heures` **+** `…de_mars_dure_167_heures` **+** `La_derive_de_WeeklyWindow_est_mesurable` | `BornesPlage.cs` sha256 `1303b96825d449a8…` = avant |
| (a1) | condition « pas de trou entre i−1 et i » retirée des Δ | `AnalyseRelevesTests.Les_deltas_ne_traversent_jamais_un_trou` **+** `Deux_resets_hebdo_sur_trois_semaines` | `AnalyseReleves.cs` sha256 `0b17c6f365d16b85…` = avant |
| (a2) | le `continue` après un reset retiré (Δ calculé à travers un reset) | `Les_deltas_ne_traversent_jamais_un_reset_et_un_delta_negatif_est_anormal` **+** `Deux_resets_hebdo_sur_trois_semaines` | idem |
| (a3) | `memeReset = true` (saut calculé même si `r` a changé) | `Un_reset_pendant_le_trou_rend_le_saut_indeterminable` | idem |

(r1) jouée sur l'arbre réel ; (b1), (a1), (a2), (a3) jouées dans l'instantané `snap-32-06` (fenêtres RED de 32-05 sur l'arbre réel), fichier identique octet pour octet à l'arbre réel (mêmes sha256).

## Suite complète (arbre réel, `dotnet test Chronos.sln -c Debug --nologo -v q`)

| Passe | HEAD | Résultat | Durée |
|---|---|---|---|
| essais 1-2 (02:00:45Z, 02:01:39Z) | `8ae2a49`, `07c1cf9` | compilation cassée par la fenêtre RED de 32-05 (`MainViewModelTests.cs` : `TexteEtatJournal`, `AlerteJournal`) — attente 50 s, relance | — |
| **1** (02:02:33Z) | `07c1cf9` (+ `MainViewModel.cs` / `SettingsWindow.xaml` de 32-05 en cours dans l'arbre) | **0 échec, 1309 / 1309** | 12 s |
| **2** (02:03:06Z) | idem | **0 échec, 1309 / 1309** | 10 s |

Total = 1271 (entrée) + **26** (ce plan : 6 + 8 + 12) + 12 (32-05, en parallèle) = 1309. `ServicesLayerPurityTests` (élargi aux sous-namespaces), `NormalisationUniqueTests`, `GardesDoctrineTests` verts sans exemption nouvelle. Aucun warning de compilation introduit (les deux warnings xUnit1031 / xUnit2013 préexistants sont dans `JournalRelevesTests.cs` et `GardesPerimetreTests.cs`, hors de ce plan).

## Files Created/Modified

- `src/Chronos/Models/Historique/LectureJournal.cs` — 9 types neutres (voir « Pour la phase 34 »)
- `src/Chronos/Services/Historique/LecteurJournal.cs` — `+ Lire(dossier, de, a)`, `MoisUtcChevauchant`, `PremiereLigneValide` (motif `releves-AAAA-MM.jsonl`, tri ordinal) ; `LireFichier` inchangé
- `src/Chronos/Services/Historique/BornesPlage.cs` — `SemaineDeForfait`, `Jour`, `QuatreSemaines`, `FuseauParisPourTests`, `MinuitLocal`, `Utc(local, tz)`
- `src/Chronos/Services/Historique/AnalyseReleves.cs` — `Analyser` + `SourceLaPlusFrequente`, `Cause`, `ResetsEtDeltas`, `AjouterSaut`
- `tests/Chronos.Tests/Fakes/FabriqueJournal.cs` — `DossierTemp()`, `JourneeNominale(minuitUtc, u5Depart, pas)`
- `tests/Chronos.Tests/LecteurJournalTests.cs` (6), `BornesPlageTests.cs` (8), `AnalyseRelevesTests.cs` (12)
- `tests/Chronos.Tests/TestData/journal/` — `trou-arrete` (10 l.), `trou-jeton` (8), `reset-5h-milieu-heure` (12), `deux-resets-hebdo` (17 + 6), `jour-dst-2026-10-25` (26), `a-cheval` (1 + 1) ; LF, sans BOM, format exact de `Serialiser`
- `.planning/REQUIREMENTS.md` — JRN-05 coché

## Decisions Made

Voir `key-decisions` : D-32-25 à D-32-30 (écrites dans le plan) plus trois décisions d'exécution (`JournalOuvertLe` passe au fichier suivant si le plus ancien est illisible ; un saut n'est émis que pour une fenêtre renseignée ; cadence d'un jour pour le test des resets hebdo).

## Deviations from Plan

### Écarts au plan (aucun fichier hors `files_modified`)

**1. [Rule 2 - Robustesse] `JournalOuvertLe` ne s'arrête pas à un premier fichier entièrement illisible**
- **Found during:** Task 1
- **Issue:** le plan lisait « le plus petit nom conforme, première ligne pour laquelle `Parser` rend vrai, aucun → `null` » : un `releves-2026-06.jsonl` corrompu de bout en bout aurait rendu `null` alors que le journal a des mois valides derrière — « journal ouvert le … » aurait disparu de la vue.
- **Fix:** parcours des fichiers conformes par nom ordinal, premier `t` valide du premier fichier qui en a un.
- **Files modified:** `LecteurJournal.cs` — **Commit:** `ae6916e`

**2. [Rule 1 - Test probant] Le test des deux resets hebdo analyse à la cadence d'un jour**
- **Found during:** Task 3 (conception)
- **Issue:** la fixture a un relevé par jour ; à `cadence = 300 s`, chaque paire est un trou et `DeltasHebdo` est vide — « jamais négatif », « jamais à travers un reset » auraient été vrais à vide.
- **Fix:** `Analyser(lecture, now, TimeSpan.FromDays(1))` → 0 trou, 3 resets, **19 Δ de +0,05** vérifiés un à un et jamais à cheval sur un `ObserveA` ; puis, à 300 s, 22 trous fermés, 0 Δ hebdo, 3 resets toujours observés. La mutation (a1) rougit ce test aussi.
- **Files modified:** `AnalyseRelevesTests.cs` — **Commit:** `c8dff30`

**3. [Rule 2 - Sens] Un saut non localisé n'est pas émis pour une fenêtre sans aucune valeur**
- **Found during:** Task 3
- **Issue:** le plan émettait un saut « pour chaque fenêtre » ; sur une source qui ne porte que la 5 h, le saut hebdo `(null, null, null)` aurait eu le même `Delta == null` que « reset dans l'absence » — deux sens pour un même signe.
- **Fix:** rien à dire → pas de saut ; un côté manquant → saut avec `Delta == null` (documenté sur le record).
- **Files modified:** `AnalyseReleves.cs`, `LectureJournal.cs` — **Commit:** `8ae2a49`

**4. [Rule 2 - Couverture] La fixture `jour-dst-2026-10-25` est exercée par un test**
- **Found during:** Task 2
- **Issue:** le plan la listait sans qu'aucun test ne la lise.
- **Fix:** `Le_jour_local_du_25_octobre_2026_dure_25_heures…` lit la plage du jour : 25 relevés, dont 2 à 02:xx local. Le compte de `[Fact]` reste 8.
- **Files modified:** `BornesPlageTests.cs` — **Commit:** `45dd49c`

**5. [Écart de forme] Les records sont dans le commit RED**
- Le plan les plaçait à la fois dans « RED : records + fixtures + helper + 6 tests » et dans « GREEN 1. `LectureJournal.cs` » ; le RED les porte (les tests les référencent), le GREEN ne porte que `Lire`. Le RED reste rouge (CS0117).

**6. [Écart de forme] `trou-jeton` porte aussi `u7`/`r7`**
- Le plan ne les précisait pas ; ils sont constants (0,5 / 2026-10-02T22:00Z) pour ressembler à une vraie ligne de sonde. Le test filtre le saut par `Fenetre == FiveHour`.

**7. [Écart au nom du plan] `LigneJournal.Parser` n'est pas appelé directement par `Lire`**
- `Lire` passe par `LireFichier` (32-04), qui appelle `Parser` : la tolérance est héritée et le `key_link` (`LigneJournal\.Parser` présent dans `LecteurJournal.cs`) tient.

**8. [Plage] `Duree` et `Contient` ajoutés au record `Plage`**
- Deux membres calculés utilisés par le lecteur et les tests (169 h, 167 h, 25 h lisibles).

---

**Total deviations:** 4 auto-fixes (Rule 1 × 1, Rule 2 × 3) + 4 écarts de forme. Aucune modification de `JournalReleves.cs`, `JournalisationUsageProvider.cs`, `App.xaml.cs`, `DiagnosticService.cs`, des VM/XAML, de `ROADMAP.md` ni de `STATE.md` (vérifié sur les 6 commits `(32-06)`).

## Issues Encountered

- **Scratchpad PARTAGÉ entre agents parallèles** : le dossier de scratchpad porte l'identifiant de session de l'orchestrateur, et l'agent 32-05 y travaille aussi (`edit_*.py`, `bak_*`, et son propre `snap/`). Mon instantané `snap/` (créé depuis `45dd49c`) a été **écrasé** par le sien entre la Task 2 et la Task 3 : le filtre a soudain échoué sur `MainViewModelTests.cs` dans un arbre censé ne pas le contenir. Corrigé par un nom unique `snap-32-06` (recréé depuis `c8dff30`). À retenir : nommer tout artefact de scratchpad par plan.
- **Fenêtres RED de 32-05 sur l'arbre réel** (`DiagnosticServiceTests`, puis `MainViewModelTests`/`ReglagesBindingTests`) : traitées par instantané, jamais conclues comme un échec de code ; la suite complète finale est rejouée sur l'arbre réel, deux fois, une fois la fenêtre refermée.
- **`bc` absent de Git Bash** : le générateur de fixtures est passé à `awk` (`%g` : `0.1`, pas `0.10`, comme STJ).
- **`git archive HEAD` n'est pas toujours compilable** : HEAD portait le RED `d3c22b4` de 32-05 ; l'instantané doit partir d'un commit GREEN connu (`45dd49c`, puis `c8dff30`).

## Known Stubs

Aucun. Rien n'est branché à une UI (objectif du plan : la phase 34 dessinera ces listes) ; aucune valeur vide n'est rendue à un rendu.

## Pour la phase 34 — l'API de lecture (contrat de ce plan)

```csharp
// 1. Les bornes (tz = TimeZoneInfo.Local en production ; repère = R7 du dernier relevé connu, repli settings.WeeklyAnchor)
Plage semaine = BornesPlage.SemaineDeForfait(now, dernierR7, settings.WeeklyAnchor, tz);   // [samedi 00:00 local, samedi suivant[
Plage jour    = BornesPlage.Jour(now, tz);                                                 // 25 h le 25/10/2026, 23 h le 28/03/2027
IReadOnlyList<Plage> quatre = BornesPlage.QuatreSemaines(now, dernierR7, settings.WeeklyAnchor, tz);   // S-3, S-2, S-1, S contiguës

// 2. La lecture (E/S, tolérante, jamais fatale) — dossier = ChronosPaths.HistoriqueDir
LectureJournal lecture = LecteurJournal.Lire(dossier, semaine.Debut, semaine.Fin);
// lecture.Releves / Evenements triés par T et filtrés [Debut, Fin[, lecture.LignesIgnorees, lecture.JournalOuvertLe (« journal ouvert le … », null = rien)

// 3. L'analyse (pure) — cadence = RateLimitHeaderUsageProvider.CadenceNominale ; source null = la plus fréquente (la sonde)
AnalyseJournal a = AnalyseReleves.Analyser(lecture, clock.UtcNow, RateLimitHeaderUsageProvider.CadenceNominale);
```

Ce que chaque record porte :

| Record | Champs | Pour la vue |
|---|---|---|
| `Plage(Debut, Fin)` + `Duree`, `Contient` | `[Debut, Fin[` UTC | axe X ; « reset hebdo → » à `Fin` |
| `LectureJournal` | `Releves`, `Evenements`, `LignesIgnorees`, `JournalOuvertLe`, `Plage` | ligne de fraîcheur (« N relevés »), marqueur « journal ouvert le » |
| `AnalyseJournal.Source` / `Serie` | `SourceUsage?`, relevés d'UNE source triés | piste NIVEAU (escalier % hebdo, dents de scie % 5 h) — coupée à chaque `Trou` |
| `Trou(Debut, Fin?, Cause)` + `CauseTrouTexte.Libelle` | `Fin == null` = ouvert à `now` | rectangle + libellé « Chronos arrêté » / « jeton invalide » / « sonde refusée » / « cause inconnue » ; bande COUVERTURE |
| `ResetObserve(Fenetre, Instant, ObserveA)` | `Instant` = ANCIENNE borne | tirets de reset 5 h ; « reset 5 h HH:MM » à `Instant` ; tuiles FENÊTRES 5 H (x jusqu'au `resets_at`) |
| `DeltaConsommation(Fenetre, De, A, Delta, ResetsAt, Anormal)` | jamais à travers un reset ni un trou | piste RYTHME (barres par heure = somme des Δ 5 h de l'heure) ; `Anormal` à compter au diagnostic |
| `SautNonLocalise(Fenetre, Trou, Avant, Apres, Delta?)` | `Delta == null` = reset dans l'absence ou valeur manquante | bloc plat gris « +N % pendant l'absence (répartition inconnue) » ; si `null` : « au moins un reset pendant l'absence » |

Fixtures disponibles (`tests/Chronos.Tests/TestData/journal/<cas>/releves-AAAA-MM.jsonl`) : `tolerance` (32-04), `trou-arrete`, `trou-jeton`, `reset-5h-milieu-heure`, `deux-resets-hebdo` (2 mois), `jour-dst-2026-10-25`, `a-cheval` (2 mois) ; journée nominale : `FabriqueJournal.JourneeNominale(minuitUtc)`.

Limites connues à garder en tête : la cause d'un trou est cherchée parmi les événements DE LA PLAGE LUE (fenêtre `]debut − cadence, fin]`) — un `arret` écrit juste avant `Plage.Debut` n'est pas vu ; si la vue veut l'exactitude au bord gauche, lire `[Debut − cadence, Fin[` puis dessiner `[Debut, Fin[`. `Analyser` ne connaît pas les relevés d'avant la plage : aucun Δ ni saut ne traverse `Plage.Debut` (c'est voulu : rien n'est reconstitué).

## Next Phase Readiness

- 32-07 : la garde documentaire peut citer `CauseTrouTexte.Libelle` et les records de `LectureJournal.cs` dans `docs/data-sources.md` § « Journal d'historique » (hypothèses : Δ = consommation → `Anormal` compté ; reset hebdo à l'heure locale → `BornesPlageTests`).
- 34 : aucune retouche attendue de la lecture ; les trois vues consomment `AnalyseJournal`.
- Mises à jour d'état laissées à l'orchestrateur (exécution parallèle) : `state advance-plan`, `state update-progress`, `state record-metric`, `roadmap update-plan-progress` non exécutées ; `STATE.md` et `ROADMAP.md` non modifiés.

## Self-Check: PASSED

- Fichiers créés (16/16 FOUND) : `LectureJournal.cs`, `BornesPlage.cs`, `AnalyseReleves.cs`, `FabriqueJournal.cs`, `LecteurJournalTests.cs`, `BornesPlageTests.cs`, `AnalyseRelevesTests.cs`, 8 fixtures JSONL, ce SUMMARY ; `LecteurJournal.cs` modifié.
- Commits (6/6 FOUND) : `373f221`, `ae6916e`, `45dd49c`, `ce552cb`, `c8dff30`, `8ae2a49`.
- `[Fact]` : 6 + 8 + 12 = 26 ; `git status` vide sur tous les fichiers du plan avant le commit de docs.
- Fichiers interdits : aucun des 6 commits `(32-06)` ne touche `JournalReleves.cs`, `JournalisationUsageProvider.cs`, `App.xaml.cs`, `DiagnosticService.cs`, `ViewModels/`, `Views/`, `ROADMAP.md`, `STATE.md`.
- `WeeklyWindow.cs`, `WeeklyRecalibration.cs`, `DayTimeline.cs` : diff vide contre `498055d`.
- Suite complète : 1309 / 0 puis 1309 / 0 sur l'arbre réel.

---
*Phase: 32-compter-juste-puis-journaliser*
*Completed: 2026-09-27*
