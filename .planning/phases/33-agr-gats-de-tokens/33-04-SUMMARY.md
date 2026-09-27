---
phase: 33-agr-gats-de-tokens
plan: 04
subsystem: lecture par plage des agrégats de tokens et rendu local (records neutres, lecteur mensuel, barres UTC libellées en local, couverture en sous-plages)
tags: [tokens, agregats, lecture-par-plage, DST, TimeZoneInfo, couverture, hors-couverture, transcripts-absents, jamais-zero, sous-agents, entiers, pur, sans-wpf, TOK-04, TOK-05]

# Dependency graph
requires:
  - phase: 33-01 (TOK-01)
    provides: TrancheTokens (SlotDe, MoisDe, Tranche), EtatCouverture / IntervalleGaranti, LigneAgregat.Parser (tolérant), MagasinAgregats.NomFichier, CouvertureTokens (Charger, Classer, VoirLigne, GarantirPasse, Sauvegarder, PlusAncienneLigneVue, Intervalles), garde TOK-05, fixture tolerance
  - phase: 32-06 (JRN-05)
    provides: Plage ([Debut, Fin[, Duree, Contient), BornesPlage.Jour / SemaineDeForfait / FuseauParisPourTests (25 h le 25/10/2026, 23 h le 28/03/2027), motif LecteurJournal.Lire (MoisUtcChevauchant, FileShare.ReadWrite | Delete, 20 reprises)
  - phase: 33 (recherche)
    provides: Pattern 5 (grouper sur l'heure UTC, libeller en local), Pattern 6 (couverture par intervalles, jamais déduite des mtimes), Pitfall 3 (28/03/2027, pas le 29)
provides:
  - Models/Historique/Tokens/LectureAgregats.cs — TotauxTokens (Vide, Plus, Somme), SousPlageCouverture, LectureAgregats (+ EtatA), BarreHeure, PartModele, ColonneQuartDHeure — entiers seulement, XML-doc de doctrine
  - Services/Historique/Tokens/LecteurAgregats.Lire(dossier, de, a) — mois UTC chevauchants, tolérance (refusées comptées), filtre [de, a[, tri (slot, modèle ordinal, sub), couverture.json chargée à côté et découpée en sous-plages, PlusAncienneLigneVue ; dossier absent → vide sans créer
  - Services/Historique/Tokens/RenduLocalTokens — ParHeure (25 / 23 / 24 barres), ParQuartDHeure (par modèle, sub fusionné), PartSousAgents (couple d'entiers), SousPlagesCouverture — pur, tz injecté
  - 4 fixtures d'agrégats sous tests/Chronos.Tests/TestData/tokens/ (dst-2026-10-25 : 100 lignes ; dst-2027-03-28 : 92 ; a-cheval-mois : 1 + 4)
affects: [34 (la piste « Tokens Claude Code » consomme Lire + ParHeure / ParQuartDHeure / PartSousAgents sans calcul), 33-05 (rien à câbler ici : classes statiques pures ; le dossier des agrégats est celui du magasin), 35 (constat : « hors couverture » / « transcripts absents » visibles)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Barre par DÉBUT D'HEURE UTC de [Debut, Fin[ libellée en local (TimeZoneInfo.ConvertTime + « HH:mm » invariant) : 25 barres le 25/10/2026 (deux « 02:00 »), 23 le 28/03/2027 (aucune « 02:00 »), 24 sinon — la plage vient de BornesPlage, le rendu ne calcule aucune borne"
    - "Indexation des tranches par case ((Slot − Debut).Ticks / pas.Ticks) : O(n) pour une semaine de 168 h, exact même si Debut n'est pas aligné"
    - "L'état de couverture voyage avec chaque agrégat rendu (barre, colonne, sous-plage) : « jamais zéro » est structurel — un consommateur ne peut pas obtenir un 0 sans son état"
    - "Sous-plages de couverture = points de coupe (bornes, plus ancienne ligne vue, débuts / fins d'intervalles) → segments classés par leur DÉBUT → fusion des adjacents de même état"
    - "Mutation restée verte = test à renforcer, pas une mutation à oublier (la2 → plage à cheval sur PlusAncienneLigneVue + instant hors plage)"

key-files:
  created:
    - src/Chronos/Models/Historique/Tokens/LectureAgregats.cs
    - src/Chronos/Services/Historique/Tokens/LecteurAgregats.cs
    - src/Chronos/Services/Historique/Tokens/RenduLocalTokens.cs
    - tests/Chronos.Tests/LecteurAgregatsTests.cs
    - tests/Chronos.Tests/RenduLocalTokensTests.cs
    - tests/Chronos.Tests/TestData/tokens/dst-2026-10-25/tokens-2026-10.jsonl
    - tests/Chronos.Tests/TestData/tokens/dst-2027-03-28/tokens-2027-03.jsonl
    - tests/Chronos.Tests/TestData/tokens/a-cheval-mois/tokens-2026-09.jsonl
    - tests/Chronos.Tests/TestData/tokens/a-cheval-mois/tokens-2026-10.jsonl
  modified:
    - .planning/REQUIREMENTS.md

key-decisions:
  - "D-33-18 — regrouper sur l'heure UTC, libeller en local : une barre = un début d'heure UTC de [Debut, Fin[, libellé TimeZoneInfo.ConvertTime(h, tz).ToString(\"HH:mm\", InvariantCulture) ; le rendu ne calcule aucune borne (BornesPlage.Jour donne 25 h / 23 h)"
  - "D-33-19 — l'état de couverture voyage avec chaque agrégat rendu (BarreHeure.Etat, ColonneQuartDHeure.Etat, LectureAgregats.Couverture en sous-plages + EtatA) ; EtatA hors de toute sous-plage → HorsCouverture (on ne dit jamais « couverte » de ce qu'on n'a pas lu)"
  - "D-33-20 — part sous-agents = (TotauxTokens SousAgents, TotauxTokens Principal) ; vue Jour = colonnes par quart d'heure, PartModele triées par Out décroissant puis modèle ordinal, sub fusionné dans le modèle"
  - "D-33-21 — fuseau injecté : production = fuseau local du système au site d'appel (phase 34), tests BornesPlage.FuseauParisPourTests() ; jamais de fuseau système dans le code pur"
  - "Exécution : Lire relit les FICHIERS (pas MagasinAgregats.TranchesDuMois) — la lecture ne mute pas l'état du magasin et tient le fichier le moins longtemps possible (un lecteur fait échouer le Move de l'écrivain, mesuré en 33-01)"
  - "Exécution : dossier absent → Couverture = SousPlagesCouverture(plage, new CouvertureTokens()) (même chemin de code : une sous-plage [de, a[ hors couverture, vide si la plage est vide)"
  - "Exécution : ParQuartDHeure pas TrancheTokens.Tranche (15 min) — une seule définition de la largeur de tranche"

patterns-established:
  - "Boucle TDD sous parallélisme sans worktree : instantané git archive af7f52a src tests Chronos.sln (snap-33-04) + sync des seuls fichiers du plan ; mutations jouées sur la copie de l'instantané, révocation par re-copie depuis l'arbre réel (jamais touché), sha256 comparé"
  - "Mutation par remplacement littéral (perl index/substr sur un heredoc) quand le motif est multi-lignes et contient des accolades : plus sûr qu'un s{}{} perl ou un sed"

requirements-completed: [TOK-04]

# Metrics
duration: 13min
completed: 2026-09-27
---

# Phase 33 Plan 04 : Lecture par plage et rendu local des agrégats de tokens (TOK-04) — Summary

**`LecteurAgregats.Lire(dossier, de, a)` ouvre les seuls mois UTC qui chevauchent la plage, relit chaque `tokens-AAAA-MM.jsonl` avec tolérance (refusées comptées), trie (slot, modèle, sub) et charge `couverture.json` à côté, découpé en sous-plages (« hors couverture » avant le 23/06/2026 12:44:22Z, « transcripts absents » pour juillet, « couverte » dans un intervalle garanti — une plage vide dit POURQUOI elle est vide) ; `RenduLocalTokens` rend les agrégats en heure locale avec le fuseau injecté : **25 barres le 25/10/2026** (deux « 02:00 »), **23 le 28/03/2027** (aucune « 02:00 » ; le lundi 29 = 24 h, prouvé), 24 sinon — une barre = un début d'heure UTC libellé en local, jamais un regroupement sur l'horloge locale ; colonnes par quart d'heure empilées par modèle (sous-agents inclus), part sous-agents en couple d'entiers ; tout pur, sans WPF, zéro flottant (garde TOK-05 verte).**

## Performance

- **Duration:** ≈ 13 min de code (SHA d'entrée `af7f52a`, début 10:58:52Z → suite complète × 2 terminée 11:11:07Z), puis SUMMARY
- **Started:** 2026-09-27T10:58:52Z
- **Completed:** 2026-09-27 (horodatage du commit de docs)
- **Tasks:** 2 / 2 (TDD : 2 RED → 2 GREEN + 1 renforcement ; 6 mutations jouées et révoquées)
- **Files:** 9 créés, 1 modifié (`REQUIREMENTS.md`)
- **Tests :** entrée **1373 / 0** (`af7f52a`) ; ce plan ajoute **+13** (6 + 7) ; suite finale sur l'arbre partagé : **1396 / 0** deux fois (33-03 en portait 10 dans l'arbre à cet instant)

## Accomplishments

- **Lecture par plage** : `a-cheval-mois` rend 23:45Z (fichier de septembre) puis 00:00Z, 00:15Z, 01:45Z (fichier d'octobre), 02:00Z exclu (`[de, a[`) ; `tolerance` (33-01) → 2 tranches, **5 lignes ignorées** ; dossier absent → 0 tranche, 0 ignorée, une sous-plage `[de, a[` hors couverture, `PlusAncienneLigneVue == null`, dossier jamais créé.
- **Couverture en sous-plages, jamais « zéro »** : plage du 25/10 → `[24/10 22:00Z, 25/10 09:05Z[ Couverte` + `[09:05Z, 23:00Z[ TranscriptsAbsents` ; juin → hors couverture ; juillet → transcripts absents ; `[22:00Z, 23:00Z[` → 4 tranches ET couverte ; le 23/06 se coupe À 12:44:22Z (hors couverture avant, transcripts absents après) ; un instant hors de la plage lue n'est jamais « couverte ».
- **Rendu local juste aux changements d'heure (critère 4 de la phase)** : 25/10/2026 → 25 barres, `[2]` et `[3]` toutes deux « 02:00 » (`DebutUtc` 00:00Z puis 01:00Z), chaque barre N 4 / Out 4, somme des N = 100 ; 28/03/2027 → 23 barres, aucune « 02:00 », `[1]` « 01:00 » puis `[2]` « 03:00 », libellés tous distincts, somme = 92 ; **29/03/2027 = lundi de 24 h, 24 barres** ; 26/09/2026 → 24 barres, état hors couverture / transcripts absents / couverte selon la couverture fournie ; deux tranches à « 02:30 local » (00:30Z et 01:30Z) restent DEUX barres.
- **Vue Jour** : 100 colonnes sur 25 h ; colonne 0 = `[opus (2, 10, 0, 0, N 2) — sub fusionné, sonnet (1, 5, 0, 0, N 1)]` ; libellés « 00:00 », « 00:15 », « 02:00 » (00:00Z) et « 02:00 » (01:00Z), « 23:45 » ; l'état vient de la couverture, pas des tranches.
- **Part sous-agents** : `(SousAgents (60, 600, 3000, 18000, N 3), Principal (5, 50, 500, 2500, N 1))` ; par réflexion, le retour est un couple de `TotauxTokens` et `TotauxTokens` n'expose que `long` / `int` — exactement `{In, Out, CacheW, CacheR, N}`, aucune somme des quatre.

## Task Commits

SHA d'entrée du plan : `af7f52a` (1373 / 0).

1. **Task 1 — Records neutres, fixtures, `LecteurAgregats.Lire` (TOK-04)**
   - RED 6 : `f2d4570` — `test(33-04): lecture par plage des agregats (…), fixtures DST 25/10/2026 et 28/03/2027, RED 6` (CS0103 / CS0246 : `LecteurAgregats`, `TotauxTokens`, `SousPlageCouverture` absents)
   - GREEN : `3ca5c44` — `feat(33-04): records neutres des agregats rendus, LecteurAgregats.Lire par plage (…) (TOK-04)` — filtre : 9 verts (6 + garde TOK-05 3)
   - Renforcement : `59a85ac` — `test(33-04): couverture - la plage se coupe A la plus ancienne ligne vue et un instant hors plage n'est jamais couvert (mutation la2 restee verte -> test renforce)`
2. **Task 2 — `RenduLocalTokens` (TOK-04)**
   - RED 7 : `0a86c10` — `test(33-04): rendu local - 25 barres le 25/10/2026, 23 le 28/03/2027, (…), RED 7` (CS0117 : `ParHeure`, `ParQuartDHeure`, `PartSousAgents`)
   - GREEN : `b1e68c4` — `feat(33-04): RenduLocalTokens - barres par debut d'heure UTC libellees en local (25 / 23 / 24), colonnes par quart d'heure et modele, part sous-agents en entiers (TOK-04)` — filtre : 28 verts (7 + 6 + garde 3 + pureté + doctrine)

**Plan metadata :** commit `docs(33-04): …` (SUMMARY + REQUIREMENTS.md) — dernier commit du plan.

## Mutations (jouées dans l'instantané `snap-33-04`, révoquées par re-copie depuis l'arbre réel, sha256 identiques)

| Mutation | Contenu | Test(s) rougi(s) | sha256 (réel = snap révoqué) |
|---|---|---|---|
| (la1) | `MoisUtcChevauchant` n'ouvre que le mois de `de` | `LecteurAgregatsTests.Lire_ouvre_les_seuls_mois_qui_chevauchent_la_plage_et_trie_par_slot` (1/6) | `LecteurAgregats.cs` `5fb3fe118d45c969` |
| (la2) | `EtatA` rend `Couverte` par défaut ET `SousPlagesCouverture` ignore `PlusAncienneLigneVue` | **restée verte** sur les 6 tests initiaux (aucune plage ne chevauchait la plus ancienne ligne vue ; `Classer` la consulte encore) → **test renforcé** (`59a85ac`) → `Une_plage_sans_tranche_hors_couverture_n_est_jamais_zero` (1/6) | `LectureAgregats.cs` `5b9137d6daaef625`, `RenduLocalTokens.cs` `2736ad7f03d4475b` (version Task 1) |
| (rl1) | regroupement par heure d'horloge LOCALE (itération locale, clé = date + heure locale) | `Le_25_octobre_2026_a_vingt_cinq_barres_dont_deux_a_02h` **+** `Le_28_mars_2027_a_vingt_trois_barres_sans_02h` **+** `Le_regroupement_se_fait_sur_l_heure_UTC_pas_sur_l_heure_locale` (3/13) | `RenduLocalTokens.cs` `7da1fe7c7a59ba3d` |
| (rl2) | segment classé par sa FIN (`Classer(p)`) au lieu de son début | `Les_sous_plages_de_couverture_decoupent_la_plage_aux_bornes_connues` **+** `La_couverture_est_lue_a_cote_des_agregats…` **+** `Une_plage_sans_tranche…` (3/13) | idem |
| (rl3) | `ParQuartDHeure` groupe par `(Model, Sub)` | `La_vue_jour_donne_une_colonne_par_quart_d_heure_empilee_par_modele_sous_agents_inclus` (1/13) | idem |
| (rl4) | CONTRÔLE de la fixture : dans le TEST, `BornesPlage.Jour(2027-03-28…)` → `new Plage(debut, debut + 24 h)` | `Le_28_mars_2027_a_vingt_trois_barres_sans_02h` (24 barres : « 23 » vient bien de la borne calendaire) (1/7) | `RenduLocalTokensTests.cs` `3f10d9d5e63140df` |

## Suite complète (arbre réel, `dotnet test Chronos.sln -c Debug --nologo -v q`)

| Passe | Heure | Résultat | Durée |
|---|---|---|---|
| filtre Task 1 (arbre réel) | 11:02Z | 9 / 9 — puis la fenêtre RED de 33-03 (`ProjectionAgregatsTests`) casse la compilation du projet de test → instantané `snap-33-04` | — |
| **1** | 11:10:17Z | **0 échec, 1396 / 1396** | 10 s |
| **2** | 11:10:47Z | **0 échec, 1396 / 1396** | 10 s |

Total = 1373 (entrée) + **13** (ce plan : 6 + 7) + 10 (33-03, en parallèle, dans l'arbre à cet instant) = 1396. `dotnet build Chronos.sln -c Debug` : **0 warning**. `GardeTokensSansPourcentageTests` (3), `ServicesLayerPurityTests`, `GardesDoctrineTests` verts sans exemption nouvelle.

## Fixtures (`tests/Chronos.Tests/TestData/tokens/<cas>/`, LF, sans BOM, `\n` final, format exact de `LigneAgregat.Serialiser`)

- `dst-2026-10-25/tokens-2026-10.jsonl` — **100 lignes**, une par quart d'heure sur `[2026-10-24T22:00Z, 2026-10-25T23:00Z[` (25 h), `claude-opus-5`, `sub: false`, (1, 1, 0, 0, N 1) ; première ligne = `{"v":1,"slot":"2026-10-24T22:00:00.0000000+00:00","model":"claude-opus-5","sub":false,"in":1,"out":1,"cache_w":0,"cache_r":0,"n":1}`.
- `dst-2027-03-28/tokens-2027-03.jsonl` — **92 lignes** sur `[2027-03-27T23:00Z, 2027-03-28T22:00Z[` (23 h).
- `a-cheval-mois/tokens-2026-09.jsonl` (1 ligne : 23:45Z) + `tokens-2026-10.jsonl` (4 lignes : 00:00Z, 00:15Z, 01:45Z, 02:00Z).
- Générées par `fixtures-33-04.sh` (scratchpad, `date -u -d @epoch`, pas de 900 s), comptées par `wc -l`, `file` = « New Line Delimited JSON text data ».

## Écarts à l'énoncé et coquille « 29/03/2027 »

- L'énoncé initial de TOK-04 disait « 23 h le 29/03/2027 » ; le 29/03/2027 est un **lundi ordinaire** (offset +02:00 stable), le jour de 23 h est le **dimanche 28/03/2027**. REQUIREMENTS.md et ROADMAP.md disent déjà 28/03 (Pitfall 3, corrigé en amont). Le test `Le_28_mars_2027_a_vingt_trois_barres_sans_02h` prouve les deux : 23 barres le 28, **`BornesPlage.Jour(2027-03-29T12:00Z).Duree == 24 h` et 24 barres le 29**.

## Files Created/Modified

- `src/Chronos/Models/Historique/Tokens/LectureAgregats.cs` — 6 records neutres (voir « Pour la phase 34 ») ; XML-doc de doctrine (jamais un pourcentage, jamais la somme des quatre, un agrégat absent n'est un zéro QUE dans une sous-plage couverte ; mots du §4).
- `src/Chronos/Services/Historique/Tokens/LecteurAgregats.cs` — `Lire`, `LireFichier` (partage large, 20 reprises, `\r` toléré, ligne vide ni lue ni comptée), `MoisUtcChevauchant` (via `TrancheTokens.MoisDe`).
- `src/Chronos/Services/Historique/Tokens/RenduLocalTokens.cs` — `ParHeure`, `ParQuartDHeure`, `PartSousAgents`, `SousPlagesCouverture`, `Libelle`, `ParIndex`.
- `tests/Chronos.Tests/LecteurAgregatsTests.cs` (6), `RenduLocalTokensTests.cs` (7), 4 fixtures.
- `.planning/REQUIREMENTS.md` — TOK-04 coché + traçabilité « Complete » (aucune ligne d'un autre plan dans l'arbre au moment du marquage).

## Decisions Made

Voir `key-decisions` : D-33-18 à D-33-21 telles qu'écrites au plan, plus trois décisions d'exécution (lecture des fichiers plutôt que du magasin ; dossier absent par le même chemin de code de couverture ; `TrancheTokens.Tranche` comme unique largeur de tranche).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Test probant] Mutation (la2) restée verte → test renforcé**
- **Found during:** Task 1 (mutation la2)
- **Issue:** aucune plage des 6 tests initiaux ne chevauchait `PlusAncienneLigneVue`, et `Classer` (33-01) consulte encore cette borne : ignorer la coupe dans `SousPlagesCouverture` ne changeait aucun résultat ; `EtatA` par défaut « couverte » n'était jamais atteint (toute plage lue est entièrement couverte de sous-plages).
- **Fix:** `Une_plage_sans_tranche_hors_couverture_n_est_jamais_zero` lit la plage du 23/06/2026 (à cheval sur 12:44:22Z → 2 sous-plages exigées) et interroge `EtatA` sur un instant hors de la plage lue (→ hors couverture). Rejouée : rouge nommé.
- **Files modified:** `tests/Chronos.Tests/LecteurAgregatsTests.cs` — **Commit:** `59a85ac`. Le compte de `[Fact]` reste 6.

**2. [Rule 3 - Blocage] Compilation du projet de test cassée par le RED de 33-03 (`ProjectionAgregatsTests` : `ProjectionAgregats` introuvable) dès la première mutation**
- **Found during:** Task 1 (la1 sur l'arbre réel — révoquée proprement, sha identique, avant de basculer)
- **Fix:** instantané `git archive af7f52a src tests Chronos.sln` sous `snap-33-04` + `sync-33-04.sh` (copie des seuls fichiers de ce plan, jamais l'inverse) ; RED / GREEN / mutations de la Task 2 et toutes les mutations jouées dans l'instantané ; suite complète finale sur l'arbre réel une fois la fenêtre refermée (1396 / 0 × 2).
- **Files modified:** aucun fichier du dépôt.

**3. [Critère d'acceptation] `TimeZoneInfo.Local` cité en XML-doc de `RenduLocalTokens.cs` (critère : 0 occurrence)**
- **Fix:** reformulé « le fuseau local du système au site d'appel » avant le GREEN. `grep -c "TimeZoneInfo.Local"` = 0.

**4. [Écart de forme] Les records sont dans le commit GREEN, pas dans le RED**
- Le RED (`f2d4570`) porte fixtures + tests seulement et rougit en compilation (CS0103 / CS0246) ; le GREEN (`3ca5c44`) porte les 6 records, `Lire` et `SousPlagesCouverture` (créée dès la Task 1 dans `RenduLocalTokens.cs`, option recommandée par le plan).

**5. [Écart de forme] `requirements mark-complete TOK-04` n'a coché que la table de traçabilité**
- La case `- [ ] **TOK-04**` (libellé en gras, l. 66) n'a pas été reconnue par l'outil ; cochée à la main (`[x]`), comme TOK-01 / TOK-03 le sont.

---

**Total deviations:** 1 renforcement de test (Rule 1), 1 blocage d'environnement (Rule 3), 1 conformité littérale, 2 écarts de forme. Aucune modification de `LigneAgregat.cs`, `MagasinAgregats.cs`, `CouvertureTokens.cs`, `BornesPlage.cs`, `App.xaml.cs`, `DiagnosticService.cs`, `ROADMAP.md`, `STATE.md` (diff vide contre `af7f52a`, vérifié après le dernier commit de code) ; aucun fichier de 33-03 touché.

## Issues Encountered

- **Nommage des rouges** : mon premier filtre de capture (`grep` sur « Échec ») ne voyait pas les lignes `[FAIL]` / « Échoué ! » de xUnit en français — la1 et la2 ont été rejouées une fois de plus pour nommer le test (sha identiques à chaque révocation).
- **Mutation multi-lignes avec accolades** : `perl -0pi 's{…}{…}'` casse sur les `{ }` du corps de lambda ; remplacement littéral par `index` / `substr` sur des heredocs (`rl1.pl`) — fiable.
- `core.autocrlf = true` : avertissements « LF will be replaced by CRLF » attendus ; fixtures forcées LF par `.gitattributes` (`af7f52a`) ; révocations par copie, jamais `git checkout --`.
- Scratchpad partagé : tous les artefacts sont nommés par plan (`snap-33-04`, `mut-33-04/`, `fixtures-33-04.sh`).

## Known Stubs

Aucun. Rien n'est branché à une UI (la phase 34 dessinera ces listes) ; `Array.Empty<…>()` rendu pour une plage vide ou un dossier absent est un comportement documenté et testé, pas un stub.

## Pour la phase 34 — l'API de lecture des tokens (contrat de ce plan)

```csharp
// 1. Les bornes (32-06) — tz = fuseau local du système en production ; repère = R7 du dernier relevé connu, repli settings.WeeklyAnchor
Plage semaine = BornesPlage.SemaineDeForfait(now, dernierR7, settings.WeeklyAnchor, tz);   // 169 h / 167 h aux changements d'heure
Plage jour    = BornesPlage.Jour(now, tz);                                                 // 25 h le 25/10/2026, 23 h le 28/03/2027

// 2. La lecture (E/S, tolérante, jamais fatale) — dossier = celui du MagasinAgregats (historique\), couverture.json à côté
LectureAgregats lecture = LecteurAgregats.Lire(dossier, semaine.Debut, semaine.Fin);
// lecture.Tranches (triées slot / modèle / sub, filtrées [Debut, Fin[), lecture.LignesIgnorees, lecture.Couverture (sous-plages),
// lecture.EtatA(instant), lecture.PlusAncienneLigneVue (« hors couverture » avant)

// 3. Le rendu (pur) — la couverture rechargée pour classer chaque barre : CouvertureTokens.Charger(Path.Combine(dossier, CouvertureTokens.NomFichier))
IReadOnlyList<BarreHeure> barres = RenduLocalTokens.ParHeure(lecture.Tranches, semaine, tz, couverture);            // piste TOKENS (Semaine) : 168 / 169 / 167 barres
IReadOnlyList<ColonneQuartDHeure> colonnes = RenduLocalTokens.ParQuartDHeure(lectureJour.Tranches, jour, tz, couverture);   // piste TOKENS (Jour) : 96 / 100 / 92 colonnes
var (sousAgents, principal) = RenduLocalTokens.PartSousAgents(lecture.Tranches);                                   // légende « ▮ principal ▮ sous-agents » — deux entiers
IReadOnlyList<SousPlageCouverture> bande = lecture.Couverture;                                                     // bande COUVERTURE (tokens) : trois états
```

Ce que chaque record porte :

| Record | Champs | Pour la vue |
|---|---|---|
| `TotauxTokens(In, Out, CacheW, CacheR, N)` + `Vide`, `Plus`, `Somme` | quatre compteurs SÉPARÉS + messages distincts — `long` / `int` seulement | hauteur de barre = le compteur que la vue choisit d'afficher (jamais leur somme) ; libellé d'axe (« 0 – 1,2 M ») calculé par la vue |
| `LectureAgregats` | `Tranches`, `LignesIgnorees`, `Plage`, `Couverture`, `PlusAncienneLigneVue` + `EtatA(instant)` | ligne de fraîcheur (« N tranches, N lignes ignorées »), bande de couverture, « hors couverture avant le … » |
| `SousPlageCouverture(Debut, Fin, Etat)` | segments contigus de la plage lue | bande COUVERTURE : `HorsCouverture` = avant le plus vieux transcript ; `TranscriptsAbsents` = purgé (présence partielle possible) ; `Couverte` = zéro = vraiment zéro |
| `BarreHeure(DebutUtc, Libelle, Principal, SousAgents, Etat)` | un DÉBUT D'HEURE UTC, « HH:mm » local | barre empilée principal + sous-agents ; deux barres « 02:00 » le 25/10 ; barre vide dessinée SELON `Etat`, jamais comme un 0 muet |
| `ColonneQuartDHeure(Slot, Libelle, ParModele, Etat)` | une tranche 15 min UTC, « HH:mm » local | vue Jour : empilement par modèle dans l'ordre de `ParModele` (Out décroissant), légende « sous-agents inclus » |
| `PartModele(Model, Totaux)` | identifiant tel que lu (« claude-opus-5 »), sub fusionné | couleur `HistoModele1/2/3` par rang |
| `(TotauxTokens SousAgents, TotauxTokens Principal)` | couple d'entiers | légende / infobulle : « principal N · sous-agents N » — jamais un rapport |

Fixtures disponibles (`tests/Chronos.Tests/TestData/tokens/<cas>/tokens-AAAA-MM.jsonl`) : `tolerance` (33-01), `dst-2026-10-25` (100), `dst-2027-03-28` (92), `a-cheval-mois` (1 + 4).

Limites connues : `ParHeure` / `ParQuartDHeure` supposent `plage.Debut` aligné sur l'heure / le quart d'heure (vrai pour `BornesPlage`) ; sinon les cases partent de `plage.Debut` tel quel, sans recalage (documenté). `Lire` ne rend pas la `CouvertureTokens` elle-même (seulement ses sous-plages et `PlusAncienneLigneVue`) : la vue la recharge par `CouvertureTokens.Charger` pour la passer au rendu — lecture tolérante, jamais fatale. L'état d'une barre est celui de son DÉBUT (`Classer(h)`) ; une borne de couverture qui tombe au milieu d'une heure est visible dans `lecture.Couverture`, pas dans la barre.

## Next Phase Readiness

- **34** : la piste Tokens se dessine sans calcul ; aucune retouche attendue de la lecture ni du rendu.
- **33-05** : rien à câbler pour ce plan (classes statiques pures) ; le diagnostic peut citer `LectureAgregats.LignesIgnorees` s'il lit une plage.
- Mises à jour d'état laissées à l'orchestrateur (exécution parallèle) : `state advance-plan`, `state update-progress`, `state record-metric`, `roadmap update-plan-progress` non exécutées ; `STATE.md` et `ROADMAP.md` non modifiés.

---
*Phase: 33-agr-gats-de-tokens*
*Completed: 2026-09-27*

## Self-Check: PASSED

- Fichiers créés (9/9 FOUND) : 3 sources, 2 tests, 4 fixtures ; ce SUMMARY présent.
- Commits (5/5 FOUND) : `f2d4570`, `3ca5c44`, `59a85ac`, `0a86c10`, `b1e68c4`.
- `[Fact]` : 6 + 7 = 13 ; suite complète : 1396 / 0 puis 1396 / 0 sur l'arbre réel ; 0 warning.
- Fichiers interdits : diff vide contre `af7f52a` (33-01, `BornesPlage.cs`, `App.xaml.cs`, `DiagnosticService.cs`, `ROADMAP.md`, `STATE.md`) ; aucun fichier de 33-03 touché.
- `REQUIREMENTS.md` : seules les 2 lignes TOK-04 diffèrent de HEAD au moment du commit de docs.
