---
phase: 32-compter-juste-puis-journaliser
plan: 02
subsystem: persistance / diagnostic (observabilité des magasins)
tags: [last-exact, IEtatMagasin, DerniereEcriture, DerniereErreur, EcritureRatee, VueAppData, MSIX, copy-on-write, diagnostic, HistoriqueDir]

# Dependency graph
requires:
  - phase: 16-19 (EXA-01, EXA-05)
    provides: LastExactStore (temp + File.Move, lecture tolérante), LastExactUsageProvider et son try/catch « je ne sais pas »
  - phase: 29/31 (APP-01..06, sondes hors arbre)
    provides: règle « %APPDATA%\Claude absent = vue réelle », sonde WMI hors arbre comme seul instrument de constat
  - phase: 32 (recherche, §CPT-02)
    provides: le fait daté qui tranche (mtime 01:55:37 le 27/09 pour une capture 23:53:37Z ; copie COW figée au 13/09 12:44:59)
provides:
  - IEtatMagasin (Nom, Chemin, DerniereEcriture, DerniereErreur) + NomsMagasins (dernier exact, journal des relevés, agrégats de tokens)
  - LastExactStore observable — DerniereEcriture = mtime après le Move (amorcée du disque au ctor), DerniereErreur « Type : message », événement EcritureRatee ; Save consigne PUIS relance
  - VueAppData / DetecteurVueAppData — détection pure de la vue (réelle / virtualisée) sur un chemin injecté, libellé qui n'avertit que si virtualisée
  - DiagnosticService — paramètre optionnel `magasins` (dernière position), section [Magasins persistants] (Vue AppData + un magasin par ligne + « ÉCHEC de la dernière écriture »)
  - ChronosPaths.HistoriqueDir = <dossier de usage.json>\historique (JRN-01)
  - STATE.md : la cause du « gel », datée et définitive ; le try/catch n'a rien avalé
affects: [32-04 (journal : IEtatMagasin à implémenter), 32-05 (câblage DI : magasins au DiagnosticService, EcritureRatee → ecriture_ratee), 33 (agrégats de tokens : troisième magasin), 35 (carte des réglages : « dernière écriture »)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Observabilité d'un magasin persistant : IEtatMagasin — âge de dernière écriture (mtime, jamais une horloge injectée pour un fichier réécrit en entier) + dernière erreur + événement ; l'écriture ne se tait plus, la lecture reste tolérante"
    - "Consigner PUIS relancer : la couche basse pose DerniereErreur et lève l'événement, la tête garde son catch (EXA-05 : null, jamais false)"
    - "Section de diagnostic GÉNÉRIQUE : faits disque toujours lus, enrichis par les états injectés, appariés par NomsMagasins"
    - "Le diagnostic nomme la VUE d'AppData depuis laquelle il lit (D-32-07) — règle : ne jamais juger l'overlay depuis une session"

key-files:
  created:
    - src/Chronos/Services/IEtatMagasin.cs
    - src/Chronos/Services/VueAppData.cs
    - tests/Chronos.Tests/VueAppDataTests.cs
  modified:
    - src/Chronos/Services/LastExactStore.cs
    - src/Chronos/Services/ChronosPaths.cs
    - src/Chronos/Services/DiagnosticService.cs
    - tests/Chronos.Tests/LastExactStoreTests.cs
    - tests/Chronos.Tests/LastExactUsageProviderTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - .planning/STATE.md

key-decisions:
  - "D-32-05 — DerniereEcriture d'un fichier réécrit en entier = son mtime (File.GetLastWriteTimeUtc après le Move, amorcé de même au ctor) : c'est le chiffre qu'une sonde hors arbre lit, indépendant de toute horloge injectée"
  - "D-32-06 — Save consigne PUIS relance ; LastExactUsageProvider.cs n'est pas modifié (diff vide) : le catch garde son rôle EXA-05, la panne laisse désormais une trace et un événement"
  - "D-32-07 — le diagnostic nomme la vue par Directory.Exists(%APPDATA%\\Claude) ; détection pure, chemin injecté, sans RacinesEtat ni nom de paquet (gardes de périmètre vertes)"
  - "D-32-08 — [Magasins persistants] est générique : trois emplacements nommés, faits disque toujours lus, états injectés en enrichissement ; 32-05 n'aura qu'à passer la liste"
  - "Ligne de magasin quand l'état injecté connaît une écriture que le disque ne montre pas : « dernière écriture il y a N min (fichier absent sur cette vue) » — c'est exactement l'écart des deux vues, rendu lisible plutôt que masqué"

patterns-established:
  - "Boucle TDD isolée sous parallélisme sans worktree : instantané `git archive <SHA d'entrée>` + fichiers du plan copiés par-dessus, dans le scratchpad — les RED des voisins ne bloquent pas, leur bin/obj n'est pas touché ; la suite complète finale se joue sur l'arbre réel"
  - "Édition de STATE.md par un exécuteur parallèle : blob construit depuis HEAD + les seuls remplacements du plan (`git hash-object` / `update-index`), pour ne pas embarquer les modifications en attente de l'orchestrateur"

requirements-completed: [CPT-02]

# Metrics
duration: 48 min
completed: 2026-09-27
---

# Phase 32 Plan 02 : Le « gel » de last-exact.json était la vue virtualisée MSIX — la persistance devient observable Summary

**Le « gel » n'a jamais existé (copie copy-on-write du paquet MSIX lue depuis les sessions, fichier réel réécrit chaque minute) ; ce plan ne « répare » donc pas `Save` mais rend la persistance observable — `IEtatMagasin`, `DerniereEcriture` (mtime), `DerniereErreur`, événement `EcritureRatee` sur `LastExactStore`, section `[Magasins persistants]` avec la ligne « Vue AppData » au diagnostic, `HistoriqueDir` pour le journal — avec un test qui reproduit la panne et prouve que le `try/catch` de `LastExactUsageProvider` n'avale rien.**

## Ce que le gel était

Repris mot pour mot des quatre points « Ce que CPT-02 devient » de `32-RESEARCH.md` :

1. *Cause établie et datée* : virtualisation AppData du paquet MSIX ; copie COW du 2026-09-13 12:44:59 ; vue réelle saine (sondes du
   25/09 21:17 et du 27/09 01:56). Le `try/catch` de `LastExactUsageProvider` n'a rien avalé.
2. *Correction d'observabilité, quand même due* : `LastExactStore` expose `DateTimeOffset? DerniereEcriture` (posée après le `Move`,
   amorcée au démarrage par `File.GetLastWriteTimeUtc` si le fichier existe) et `string? DerniereErreur` (`type : message` de la
   dernière exception, effacée au succès) ; `LastExactUsageProvider` n'avale plus : il consigne `store.DerniereErreur` et, si le journal
   est injecté, écrit un événement `ecriture_ratee` (`{v, t, ev: "ecriture_ratee", magasin: "last-exact", cause}` — extension du
   vocabulaire, lecteur tolérant). Le rendu du snapshot reste identique (pas de crash, EXA-05 conservé : `null`, pas `false`).
3. *Diagnostic* : nouvelle section `[Magasins persistants]` — pour `last-exact.json`, `historique\releves-AAAA-MM.jsonl`, et
   « agrégats de tokens : aucun (phase 33) » : chemin, existe ?, taille, « dernière écriture il y a N min » (`LibelleSource.Anciennete`),
   dernière erreur ; plus une ligne d'en-tête **« Vue AppData : réelle »** ou **« virtualisée (paquet Claude_…) — les fichiers lus ici
   ne sont pas ceux de l'overlay lancé par l'Explorateur »** (`Directory.Exists(APPDATA\Claude)` + `RacinesEtat.Candidats`).
4. *Test qui reproduit « la panne »* : magasin dont le dossier parent est un FICHIER (motif exact de
   `Un_magasin_en_panne_ne_leve_pas_et_n_affirme_rien_sur_le_passe`, l. 230-255) → `DerniereErreur` non nulle, événement
   `ecriture_ratee` dans le faux journal, ligne de diagnostic « ÉCHEC », snapshot rendu identique. Et un test de non-régression :
   magasin sain → `DerniereEcriture == clock.UtcNow` après `Save`.

**L'énoncé « `Save` dans un `try/catch` muet = cause du gel » (CONTEXT, ROADMAP, conseil du 26/09) est périmé.**

Précisions d'exécution par rapport à ce texte de recherche (arbitrées par le PLAN, qui prime) :
- Point 2 : c'est le **magasin** qui consigne et lève l'événement (`EcritureRatee`), pas le provider — `LastExactUsageProvider.cs` n'est
  pas modifié (D-32-06) ; l'écriture de la ligne `ecriture_ratee` dans le journal est câblée en 32-05.
- Point 3 : le libellé virtualisé ne nomme **pas** le paquet (`Claude_…`) et n'utilise pas `RacinesEtat.Candidats` : les gardes de
  périmètre interdisent ces jetons hors du résolveur de racines (D-32-07). Il dit « virtualisée (paquet de l'app bureau) — … ».
- Point 4 : la non-régression compare `DerniereEcriture` au **mtime** du fichier (D-32-05), pas à `clock.UtcNow`.

**Réponse à la question « pourquoi la copie du 13/09 ? »** (hors périmètre du plan, laissée ouverte par la recherche) : un overlay a
tourné SOUS l'arbre de l'app ce jour-là de 07:36 à 12:44 ; son dernier `Save` avant d'être quitté a fabriqué la copie COW. Tout ce
qu'une session Claude Code lit ensuite dans `%APPDATA%\Chronos` est cette copie. **Règle : ne jamais juger l'overlay depuis une session.**

## Performance

- **Duration:** 48 min
- **Started:** 2026-09-27T00:50Z (environ ; SHA d'entrée `c9d65cf`)
- **Completed:** 2026-09-27T01:38Z
- **Tasks:** 3 / 3
- **Files modified:** 10 (3 créés, 7 modifiés) — 477 insertions, 13 suppressions

## Accomplishments

- La persistance du dernier exact dit **quand** elle a écrit (`DerniereEcriture` = mtime, amorcée du disque) et **pourquoi** elle n'a pas pu
  (`DerniereErreur`, `EcritureRatee`) ; la panne d'écriture a une trace et un événement, et la tête garde son « je ne sais pas » (EXA-05).
- Le diagnostic dit **depuis quelle vue d'AppData** il lit et, pour chacun des trois magasins, l'âge de la dernière écriture ou l'absence de
  fichier — et « ÉCHEC de la dernière écriture : … » quand une erreur existe.
- La cause du « gel » est écrite, datée et définitive dans STATE.md ; l'hypothèse du `try/catch` muet y a disparu.
- `ChronosPaths.HistoriqueDir` : le contrat de chemin du journal, dérivé, jamais construit en dur.

## Task Commits

SHA d'entrée : `c9d65cf`. Chaque tâche est commitée atomiquement (`--no-verify`, fichiers stagés explicitement) :

1. **Task 1 (TDD) : LastExactStore observable — DerniereEcriture, DerniereErreur, EcritureRatee ; test qui reproduit « la panne »**
   - `aca8670` (test) — RED : 20 erreurs de compilation nommant les seuls membres attendus ; puis, surface d'API posée sans
     comportement, **RED nommé 5 échecs / 41** (`Une_ecriture_reussie_pose_DerniereEcriture…`, `Un_magasin_jamais_ecrit_par_ce_processus…`,
     `Une_ecriture_ratee_consigne…`, `Un_succes_apres_un_echec…`, `Un_magasin_en_panne_ne_se_tait_plus…`)
   - `76f57f8` (feat) — GREEN **41 / 41** sur le filtre (`LastExactStoreTests`, `LastExactUsageProviderTests`, `ServicesLayerPurityTests`)
2. **Task 2 (TDD) : Le diagnostic dit d'où il regarde et quand chaque magasin a écrit — [Magasins persistants]**
   - `316c4bd` (test) — RED : 9 erreurs de compilation (`VueAppData`, `DetecteurVueAppData`, paramètre `magasins`)
   - `159ccb5` (feat) — GREEN **81 / 81** sur le filtre (`DiagnosticServiceTests`, `VueAppDataTests`, `GardesPerimetreTests`,
     `LectureSeuleAppBureauTests`, `ServicesLayerPurityTests`)
3. **Task 3 : Écrire la cause, datée, là où l'hypothèse vivait — STATE.md ; suite complète ×2**
   - `59c2743` (docs) — STATE.md, 2 lignes (entrée « Blockers / dettes ouvertes » et « Persistance existante »), rien d'autre

**Plan metadata :** commit final `docs(32-02): complete …` (SUMMARY + STATE.md), voir la fin de ce fichier.

## Mutations (jouées, rouge nommé, révoquées)

| Mutation | Rouge(s) obtenu(s) | Révocation |
|---|---|---|
| (e1) `throw;` supprimé dans le `catch` de `Save` | **4** : `Une_ecriture_ratee_consigne_l_erreur_leve_l_evenement_et_relance`, `Un_magasin_en_panne_ne_leve_pas_et_n_affirme_rien_sur_le_passe` (les 2 exigés — `UnExactADejaEteObtenu` deviendrait `false` : le relancement tient EXA-05), plus `Un_magasin_en_panne_ne_se_tait_plus…` et `Un_succes_apres_un_echec…` (leurs `ThrowsAny`/`Null`) | sha256 `688c44162db8142b…` identique avant/après |
| (e2) `DerniereErreur = null;` supprimé du succès | **1** : `Un_succes_apres_un_echec_efface_DerniereErreur` | sha256 `688c44162db8142b…` identique |
| (d1) `Directory.Exists(…)` → `false` dans `Detecter` | **1** : `Un_dossier_Claude_present_signe_la_vue_virtualisee` | blob git `65547cdd…` identique à HEAD (le sha256 des octets diffère : `git checkout` a restitué le fichier en CRLF via `autocrlf`, contenu normalisé identique, `git diff` vide) |

## Suite complète (arbre réel, `dotnet test Chronos.sln -c Debug --nologo -v q`)

| Passe | HEAD | Résultat |
|---|---|---|
| 1 (01:38:08Z) | `cb1bfaf` | **0 échec, 1271 réussis, 1271 au total**, 9 s |
| 2 (01:38:21Z) | `cb1bfaf` | **0 échec, 1271 réussis, 1271 au total**, 9 s |

Avant : 20 relances automatiques (01:14Z → 01:36Z) le temps que 32-01, 32-03 et 32-04 passent au vert dans le même arbre — jamais un rouge de ce plan.

Les totaux dépassent 1200 + 12 parce que les plans parallèles 32-01, 32-03 et 32-04 ont ajouté les leurs dans le même arbre.

## Files Created/Modified

- `src/Chronos/Services/IEtatMagasin.cs` — contrat neutre d'observabilité d'un magasin + `NomsMagasins` (3 constantes)
- `src/Chronos/Services/LastExactStore.cs` — `: IEtatMagasin` ; ctor amorce `DerniereEcriture` du disque ; `Save` consigne puis relance ;
  XML-doc « Observabilité (phase 32) » ; commentaire de `LireBrut` réécrit (tolérance en LECTURE, l'ÉCRITURE ne se tait plus)
- `src/Chronos/Services/ChronosPaths.cs` — `HistoriqueDir`
- `src/Chronos/Services/VueAppData.cs` — `enum VueAppData`, `DetecteurVueAppData.Detecter/Libelle`
- `src/Chronos/Services/DiagnosticService.cs` (CRLF conservé) — paramètre `magasins`, section 3b `[Magasins persistants]` entre
  `[Transcripts JSONL]` et `[Ce qui est affiché maintenant]`, helper `LigneMagasin`
- `tests/Chronos.Tests/LastExactStoreTests.cs` (+5), `LastExactUsageProviderTests.cs` (+1), `DiagnosticServiceTests.cs` (+3),
  `VueAppDataTests.cs` (nouveau, 3) — **12 tests** ajoutés
- `.planning/STATE.md` — 2 lignes

## Decisions Made

D-32-05 à D-32-08 telles qu'écrites dans le plan (voir le frontmatter). Une décision de forme prise à l'exécution : quand l'état injecté
connaît une écriture mais que le fichier est absent sur le disque lu, la ligne dit « dernière écriture il y a N min (fichier absent sur
cette vue) » plutôt que « aucune écriture » — c'est précisément l'écart des deux vues, et le masquer reproduirait le malentendu.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Contrat sous test] Test de `HistoriqueDir` ajouté**
- **Found during:** Task 1
- **Issue:** le plan livre `HistoriqueDir` (truth n° 5 : « dérivé de UsageFile, jamais construit en dur ») sans test, alors que
  `LastExactFile` et `SettingsFile` en ont un.
- **Fix:** `HistoriqueDir_reste_dans_le_dossier_du_usage_file_injecte` (motif du test 12 existant). Total : 12 tests au lieu de ≈ 11.
- **Files modified:** `tests/Chronos.Tests/LastExactStoreTests.cs`
- **Committed in:** `aca8670`

**2. [Rule 3 - Blocage] Boucle TDD dans un instantané isolé**
- **Found during:** Task 1 (première compilation)
- **Issue:** 4 exécuteurs partagent le même arbre sans worktree ; le projet de tests ne compilait pas à cause des RED commités des
  voisins (`DedupUsage`, `ProcessusChronos`, `ReleveJournal`, `JournalReleves`), et HEAD bougeait pendant l'exécution.
- **Fix:** script `runtests.sh` (scratchpad) : `git archive c9d65cf` (SHA d'entrée du plan) + les 9 fichiers du plan copiés
  par-dessus, `dotnet test` filtré là-dedans. Aucun fichier étranger touché, `bin/obj` partagé jamais utilisé pour la boucle.
  La suite complète de la tâche 3 est, elle, jouée sur l'arbre réel (avec relances jusqu'à ce que les voisins soient verts).
- **Files modified:** aucun du dépôt

---

**Total deviations:** 2 (1 × Rule 2, 1 × Rule 3). **Impact :** aucun sur le périmètre ; un test de plus ; méthode de boucle documentée
dans `patterns-established`.

## Issues Encountered

- **STATE.md déjà modifié dans l'arbre de travail par l'orchestrateur** (frontmatter « executing », position). Pour ne commiter que mes deux
  remplacements : blob construit depuis `HEAD:.planning/STATE.md` + `edit-state.pl`, `git hash-object -w` / `git update-index --cacheinfo`,
  commit ; l'arbre de travail a reçu les mêmes deux remplacements et garde les modifications en attente de l'orchestrateur (`M` toujours
  affiché, à lui de les commiter).
- **RED de compilation vs RED nommé** : une API nouvelle ne peut pas rougir par assertion avant d'exister ; pour la tâche 1 j'ai posé la
  surface d'API sans comportement entre le commit RED et le commit GREEN afin d'obtenir les 5 rouges nommés (non commité comme état
  intermédiaire). Pour la tâche 2 le RED est de compilation (9 erreurs) ; la mutation (d1) et les assertions de contenu tiennent lieu de preuve.

## Known Stubs

Aucun. La ligne « agrégats de tokens : aucun (phase 33) » est un texte ASSUMÉ et documenté (troisième emplacement de `NomsMagasins`,
réservé à la phase 33), pas un stub qui empêcherait l'objectif du plan.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Pour 32-05 (câblage DI)** — rappel exigé par le plan : passer `magasins: [LastExactStore, JournalReleves]` au `DiagnosticService` et
abonner `EcritureRatee` → `SignalerEcritureRatee("last-exact", cause)`. Le journal (32-04) implémente `IEtatMagasin` avec
`Nom = NomsMagasins.JournalReleves`, `Chemin = paths.HistoriqueDir` (dossier), `DerniereEcriture` datée par `IClock` (il n'a pas de
fichier unique — D-32-05 ne s'applique qu'aux fichiers réécrits en entier).

**Pour VAL-04 (constat)** : la ligne « Vue AppData » du rapport produit par l'overlay lancé par l'Explorateur doit dire « réelle » ;
un rapport lu depuis une session dira « virtualisée » — et c'est normal. Ne jamais juger l'overlay depuis une session.

**Pour la phase 33** : troisième ligne de `[Magasins persistants]` déjà réservée (`NomsMagasins.AgregatsTokens`).

---
*Phase: 32-compter-juste-puis-journaliser*
*Completed: 2026-09-27*

## Self-Check: PASSED

- 10 fichiers du plan présents (FOUND ×10) ; commits `aca8670`, `76f57f8`, `316c4bd`, `159ccb5`, `59c2743` présents dans `git log --all` ; ROADMAP.md non modifié par ce plan ; `LastExactUsageProvider.cs` : diff vide.
