---
phase: 23-un-magasin-qui-ne-croit-plus-et-n-oublie-plus
plan: 02
subsystem: services
tags: [sessions, magasin, expiration, balayage, horloge-injectee, doctrine, di, tdd]

# Dependency graph
requires:
  - phase: 21-perimetre-le-widget-ne-parle-que-de-claude-code
    provides: "ISessionSource / TranscriptSessionSource, précédent de purge câblée au démarrage (ArchiveStore.PurgerPrefixe)"
  - phase: 22-un-instrument-de-mesure-qui-ne-ment-plus
    provides: "SessionMonitor.Inspecter (Visibles + Masquees), partage d'instance du moniteur avec le diagnostic"
  - phase: 23-un-magasin-qui-ne-croit-plus-et-n-oublie-plus
    plan: 01
    provides: "des updated_at fiables côté écriture — le balayage date les fichiers par cette valeur"
provides:
  - "Services/BalayageMagasinSessions : deux gestes distincts (retirer les débris, balayer les états périmés) sur horloge INJECTÉE"
  - "BilanBalayage : trois observations de ce qui a été RETIRÉ, jamais de ce qui a été repéré"
  - "Critère DOUBLE d'expiration : 72 h ET aucune attestation de vie — l'âge n'est jamais le seul critère"
  - "Garde de câblage Le_demarrage_balaie_le_magasin_de_sessions_sur_le_dossier_du_moniteur, prouvée falsifiable"
  - "Garde par réflexion Le_balayage_ne_connait_aucun_magasin_de_verdict (insensible au texte des commentaires)"
  - "Garde DI anti-accident : le miroir de CompositionRootTests ne pointe plus sur le vrai magasin de l'utilisateur"
affects: [24 arbitrage par fraîcheur, 25 contrat d'événements, 26 « traité » et archivage unique]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Horloge IClock INJECTÉE dès la conception d'un mécanisme temporel — remède au test à retardement de la phase 22"
    - "Critère d'expiration DOUBLE (âge + attestation par une source externe) plutôt que le seul âge"
    - "Garde par RÉFLEXION sur les champs et paramètres d'un type, pour prouver une doctrine que les commentaires ne peuvent qu'affirmer"
    - "Mutation de falsification jouée via un ALIAS temporaire, pour que le dépôt reste compilable pendant la mesure"

key-files:
  created:
    - src/Chronos/Services/BalayageMagasinSessions.cs
    - tests/Chronos.Tests/BalayageMagasinSessionsTests.cs
  modified:
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - .planning/phases/23-un-magasin-qui-ne-croit-plus-et-n-oublie-plus/23-VALIDATION.md

key-decisions:
  - "Le critère d'âge N'EST PAS le seul : un état n'est balayé que s'il dépasse 72 h ET qu'aucune source n'atteste la vie de sa session. Un fichier de 40 jours dont la session est attestée vivante survit — c'est le critère n°4 du ROADMAP dans son versant opératoire."
  - "72 h = neuf fois le DropAfter de 8 h du moniteur : on ne balaie jamais quelque chose que le widget pourrait encore montrer, et le passé récent reste lisible dans le rapport de diagnostic."
  - "Balayer ne conclut RIEN. Le balayeur ne connaît aucun magasin de verdict (garde par réflexion) et, après balayage, Inspecter rend Visibles vide ET Masquees vide (garde de comportement). Les deux preuves sont exigées : une doctrine que seuls des commentaires affirment n'est pas une doctrine."
  - "Horloge INJECTÉE dès la conception (IClock au constructeur) : 0 occurrence de l'horloge système dans le service ET dans ses tests, y compris pour File.SetLastWriteTimeUtc. Trois plans de la phase 22 avaient dû corriger des tests à retardement ; le défaut n'est pas reproduit."
  - "Le balayage lit le dossier DU MONITEUR du widget (GetRequiredService<SessionMonitor>().Directory), jamais un second chemin déduit : deux chemins pour un seul widget rouvriraient l'écart que la phase 22 vient de fermer, et un balayage qui se tromperait de dossier effacerait les fichiers de quelqu'un d'autre."
  - "Le bilan est une OBSERVATION : une suppression qui échoue n'est pas comptée comme retirée (précédent ArchiveStore.PurgerPrefixe). Rien à retirer ⇒ rien n'est touché, date de dernière écriture comprise."
  - "Un fichier d'état illisible est daté par sa date d'écriture — un fait observé — et jamais par une date inventée ; si même cette date est inaccessible, le fichier n'est jamais balayé."
  - "CompositionRootTests enregistrait new SessionMonitor(null, …), dont le dossier est le VRAI %APPDATA%\\Chronos\\sessions. Le miroir DI reçoit désormais un dossier temporaire, et Balayer() n'y est jamais appelé : une garde DI prouve la résolution du graphe, pas le comportement."
  - "DiagnosticService.cs et SessionMonitor.cs sont ABSENTS du diff de toute la phase : le rapport reflétera le balayage sans qu'une ligne n'y change — preuve à l'usage du partage d'instance de la phase 22."

patterns-established:
  - "Étape ROUGE contre un squelette compilable : 7 échecs comportementaux / 1 succès structurel (la garde par réflexion, que le squelette satisfait déjà — et c'est normal)"
  - "Le seuil d'expiration se justifie par un RAPPORT à un seuil existant (9 × DropAfter), pas par un chiffre rond choisi seul"

requirements-completed: [CYC-01]

# Metrics
duration: 14min
completed: 2026-09-12
---

# Phase 23 Plan 02 : Le magasin d'états cesse de croître, sans jamais conclure — Summary

**`BalayageMagasinSessions` retire au démarrage les débris temporaires de plus d'une heure et les états de
plus de 72 h dont aucune source n'atteste la vie de la session — sur horloge injectée, sur le dossier DU
MONITEUR du widget, et sans jamais rien déclarer « terminé » ni « traité » : après balayage, `Inspecter`
rend `Visibles` vide ET `Masquees` vide.**

## Performance

- **Duration:** ~14 min
- **Started:** 2026-09-12T17:00:00Z
- **Completed:** 2026-09-12T17:14:00Z
- **Tasks:** 3 (dont 1 en TDD, 4 commits)
- **Files modified:** 6 (2 créés, 4 modifiés)

## Accomplishments

- **CYC-01 est couverte à sa cause.** La seule suppression d'un fichier d'état du dépôt était conditionnée à
  l'événement de fin de session, dont aucune valeur de motif ne couvre un terminal tué, un plantage ou un
  redémarrage machine — et dans ces cas-là le hook ne s'exécute pas du tout. Le balayage ne consulte **aucun**
  événement de fin (`grep -c "SessionEnd"` sur le service → **0**) : seuls l'âge et l'absence d'attestation
  tranchent.
- **Le critère d'âge n'est pas le seul, et c'est testé nominativement.** Un fichier de **40 jours** dont le
  `session_id` est rendu par `ISessionSource` → `EtatsRetires == 0`, fichier intact
  (`Une_session_vivante_depuis_des_jours_survit_au_nettoyage`). Balayer par l'âge seul aurait produit le même
  écran le jour de la livraison et effacé une session vivante trois jours plus tard.
- **Balayer ne conclut rien — deux preuves de natures différentes.** *Réflexion* : aucun champ ni paramètre
  de type `TreatedStore` / `ArchiveStore`, et une `IClock` exigée au constructeur. *Comportement* : après
  balayage d'un état de 40 jours, `Inspecter(Maintenant)` rend `Visibles` **vide**, `Masquees` **vide**,
  `FichiersEcartesParAnciennete == 0`, et **aucun** des deux fichiers de magasin de verdict n'a été créé.
- **Deux gestes distincts.** Retirer des **débris** (fichiers temporaires qui ne sont l'état de personne :
  ils n'ont jamais atteint leur destination) n'est pas balayer un **état** (qui, lui, a été vrai). Le cas
  mesuré est rejoué : cinq débris portant le même `session_id` avec cinq pid différents partent tous les
  cinq ; un débris de 2 minutes est épargné — un hook est peut-être en train d'écrire.
- **Le seuil se justifie par un rapport, pas par un chiffre rond.** 72 h = **9 × le `DropAfter` de 8 h** du
  moniteur : on ne balaie jamais ce que le widget pourrait encore montrer. Le fichier de **10 h** du test 1
  est le cas qui le prouve — il a quitté l'écran, il reste sur le disque.
- **Le risque de destruction de données réelles a été désamorcé AVANT d'y brancher quoi que ce soit.**
  `CompositionRootTests` enregistrait `new SessionMonitor(null, …)`, dont le dossier est le **vrai**
  `%APPDATA%\Chronos\sessions` ; le balayeur en dérivant son dossier, la garde DI aurait pointé sur les
  66 entrées de l'utilisateur. Le miroir reçoit un dossier temporaire, l'assertion anti-accident le vérifie,
  et `Balayer()` n'apparaît **nulle part** dans ce fichier de tests.
- **754 → 763 tests verts** (+8 en Task 1, +1 en Task 2), 0 échec, **deux exécutions consécutives au même
  total**, build 0 erreur / 0 avertissement, suite en 4 s.

## Task Commits

1. **Task 1 — ROUGE : le critère double et la doctrine, contre un squelette compilable** - `2859afc` (test)
2. **Task 1 — VERT : deux gestes distincts, horloge injectée, critère double** - `13ac1c7` (feat)
3. **Task 2 : le balayage s'exécute au démarrage sur le dossier du moniteur, deux gardes** - `1d8b637` (feat)
4. **Task 3 : la carte de vérification de la phase, remplie et mesurée** - `43562fe` (docs)

**Plan metadata:** commit final `docs(23-02)`.

## Chiffres demandés par le plan

### Comptes de tests, plan par plan

| Jalon | Total | Écart |
|---|---|---|
| Baseline d'entrée de phase 23 (fin de phase 22) | **747** | — |
| Après 23-01 | **754** | +7 (6 en T1, 1 garde de câblage en T2) |
| Après 23-02 | **763** | +9 (8 en T1, 1 garde de câblage en T2) |
| **Total phase 23** | **763** | **+16** (le plan de phase en prévoyait +14 — l'écart est celui des **deux gardes de câblage**, non comptées dans le chiffre indicatif) |

Deux exécutions consécutives : **763 / 763**, 0 échec, 4 s chacune.

### L'étape ROUGE

**7 échecs / 1 succès / 8 au total**, build **0 erreur / 0 avertissement** au commit `2859afc`.

Le succès isolé mérite d'être nommé plutôt que caché : c'est `Le_balayage_ne_connait_aucun_magasin_de_verdict`,
garde **structurelle** et non comportementale. Le squelette ne portait déjà aucun champ ni paramètre de type
de verdict et exposait bien une `IClock` — il la satisfaisait donc légitimement. Les **7** autres échouent sur
le comportement (`NotImplementedException`), pas sur la compilation : c'est exactement ce que l'étape ROUGE
doit établir.

### La mutation de falsification

| Élément | Valeur |
|---|---|
| Mutation | `GetRequiredService<BalayageMagasinSessions>().Balayer()` → `….BalayerMUTANT()` dans `App.xaml.cs` |
| Échec constaté | **`Chronos.Tests.GardesPerimetreTests.Le_demarrage_balaie_le_magasin_de_sessions_sur_le_dossier_du_moniteur` [FAIL]** — **1 échec / 7 succès** sur le filtre `GardesPerimetre` |
| Révocation | `git diff --stat` du service **vide** ; `grep -cF "GetRequiredService<BalayageMagasinSessions>().Balayer()" src/Chronos/App.xaml.cs` → **1** ; `grep -c MUTANT` → **0** dans les deux fichiers |

Comme en 23-01, la mutation ne compile pas telle quelle — et une erreur de compilation ferait échouer *toute*
l'invocation `dotnet test`, ce qui ne dirait **rien** de la garde. Elle a donc été jouée avec un **alias
temporaire** (`public BilanBalayage BalayerMUTANT() => Balayer();`) ajouté au service le temps de la mesure.
Alias et mutation révoqués ensemble.

### Chiffres de `grep` RÉELLEMENT obtenus

Leçon 22-03 : un écart se documente, il ne s'absorbe pas.

| Contrôle | Prédit | **Mesuré** |
|---|---|---|
| signature du constructeur à trois paramètres | 1 | **1** |
| `_horloge.UtcNow` dans le service | 1 | **1** |
| `DateTimeOffset.UtcNow` dans le service | 0 | **0** |
| `DateTimeOffset.UtcNow` dans le fichier de tests | 0 | **0** |
| `new FakeClock(Maintenant)` dans les tests | 1 | **1** |
| `Assert.StartsWith(Path.GetTempPath()` dans les tests | ≥ 2 | **3** |
| `FromUnixTimeMilliseconds` / `TreatedStore` / `SessionActivity` dans le service | 0 / 0 / 0 | **0 / 0 / 0** |
| `UsageNormalization.InstantDepuisEpochMillisecondes` dans le service | 1 | **1** |
| `ArchiveStore` dans le service | 2 (XML-doc) | **2** — l. 9 et l. 24, relues une à une : **0 usage de code** |
| `GetRequiredService<BalayageMagasinSessions>().Balayer()` dans `App.xaml.cs` | 1 | **1** |
| `GetRequiredService<SessionMonitor>().Directory` dans `App.xaml.cs` | 1 | **1** |
| `GetRequiredService<SessionMonitor>()` dans `App.xaml.cs` | 3 | **3** |
| `new BalayageMagasinSessions(` dans `App.xaml.cs` | 1 | **1** |
| `Balayer()` dans `CompositionRootTests.cs` | 0 | **0** |
| `Path.GetTempPath()` dans `CompositionRootTests.cs` | +≥ 2 | **15 → 17** (+2) |
| `new SessionMonitor` dans `DiagnosticService.cs` (phase 22) | 0 | **0** |
| `?? new ` dans `DiagnosticService.cs` (phase 20) | 2 | **2** |
| `=> Inspecter(now).Visibles;` dans `SessionMonitor.cs` (phase 22) | 1 | **1** |

**Aucun écart entre prédit et mesuré sur ce plan.** Les deux seuls chiffres non fixés à l'avance par le plan
(`Assert.StartsWith` ≥ 2, `GetTempPath` en hausse) valent respectivement **3** et **15 → 17**.

## Files Created/Modified

- `src/Chronos/Services/BalayageMagasinSessions.cs` *(créé, 146 lignes — min. 90)* — `BilanBalayage`
  (trois observations) et `BalayageMagasinSessions.Balayer()` : retire les débris temporaires de plus
  d'`AgeMinimalTemporaire` (1 h), lit l'attestation de vie **une** fois, puis balaie les états dépassant
  `ExpirationEtat` (72 h) **et** non attestés. Conversion d'époque par le point unique
  `UsageNormalization`, jamais en local. Ne lève jamais.
- `tests/Chronos.Tests/BalayageMagasinSessionsTests.cs` *(créé, 255 lignes — min. 130, 8 tests)* — critère
  double, survie d'une session vivante depuis 40 jours, les cinq débris du cas mesuré, datation d'un fichier
  illisible par son écriture, « rien à retirer ⇒ rien touché », le test de doctrine, la garde par réflexion,
  le dossier absent. Tous en dossier temporaire, garde anti-accident **avant** toute construction.
- `src/Chronos/App.xaml.cs` *(modifié)* — enregistrement DI du balayeur immédiatement après `SessionMonitor`
  (dossier dérivé du moniteur, attestation par `TranscriptSessionSource`, horloge du conteneur) et appel
  best-effort dans `OnStartup`, après la purge du préfixe `desktop:`, en mode overlay uniquement.
- `tests/Chronos.Tests/GardesPerimetreTests.cs` *(modifié)* — garde de câblage
  `Le_demarrage_balaie_le_magasin_de_sessions_sur_le_dossier_du_moniteur`.
- `tests/Chronos.Tests/CompositionRootTests.cs` *(modifié)* — le miroir DI reçoit un dossier de sessions
  **temporaire** au lieu de `null`, enregistre `IClock` et le balayeur, et vérifie que
  `balayeur.Dossier == moniteur.Directory` **sous `Path.GetTempPath()`**.
- `.planning/phases/23-.../23-VALIDATION.md` *(modifié)* — carte de vérification remplie, `status: validated`.

## Decisions Made

1. **Le critère est double, et le test qui l'établit est nommé dans le ROADMAP.** L'âge seul aurait suffi à
   faire fondre le dossier — et à effacer une session vivante depuis plusieurs jours. C'est la différence
   entre une livraison qui paraît bonne le jour J et une livraison correcte au bout de trois jours.
2. **72 h dérive d'un seuil existant.** Neuf fois le `DropAfter` de 8 h du moniteur. Un chiffre rond choisi
   isolément n'aurait eu aucune garantie de ne jamais mordre sur ce que le widget affiche.
3. **Deux preuves pour une doctrine, jamais une.** Un commentaire peut jurer que « balayer ne conclut rien »
   pendant qu'un champ de verdict siège dans la classe ; la réflexion ne lit pas les intentions. Et un type
   sans champ de verdict pourrait tout de même écrire ailleurs : d'où la preuve de comportement.
4. **L'horloge est injectée dès la conception, pas ajoutée après coup.** `DateTimeOffset.UtcNow` = 0 dans le
   service **et** 0 dans ses tests, y compris pour `File.SetLastWriteTimeUtc` : tous les horodatages des
   tests sont dérivés du littéral `Maintenant`. Le piège qui a rendu rouges trois plans de la phase 22 à une
   heure donnée du soir ne peut pas se reproduire ici.
5. **Le bilan compte ce qui a été SUPPRIMÉ.** Une suppression qui échoue incrémente les *conservés*, jamais
   les *retirés*. Le nombre rendu reste une observation — précédent `ArchiveStore.PurgerPrefixe`, où annoncer
   ce qu'on a repéré plutôt que ce qu'on a fait aurait menti sur le seul canal disponible.
6. **Un fichier illisible n'est pas un fichier sans date.** Il est daté par sa date d'écriture. Et si même
   cette date est inaccessible, il n'est **jamais** balayé : ne pas savoir quand un fichier a été écrit
   n'autorise pas à le supprimer.
7. **Le dossier balayé est celui du moniteur, résolu par le conteneur.** Aucun second chemin déduit. La garde
   de câblage l'exige littéralement (`GetRequiredService<SessionMonitor>().Directory`), et elle est prouvée
   falsifiable.
8. **`DiagnosticService.cs` n'a pas été touché** — ni `SessionMonitor.cs`, ni `ArchiveStore.cs`, ni
   `TreatedStore.cs`, ni `SessionHookProcessor.cs`, ni `SessionHookInstaller.cs`. Le diff **complet** de la
   phase sous `src/` et `tests/` compte sept fichiers, pas un de plus.

## Deviations from Plan

**None - plan executed exactly as written.**

Deux précisions d'exécution, sans écart de contenu :

1. **L'étape ROUGE rend 7 échecs / 1 succès, non 8 / 0.** Le plan ne fixait pas ce compte, mais la précision
   compte : la garde par **réflexion** est structurelle, et le squelette la satisfait déjà légitimement
   (aucun champ de verdict, `IClock` au constructeur). Le rendre rouge artificiellement aurait demandé
   d'écrire un squelette *faux*, ce qui n'aurait rien prouvé. Les 7 échecs restants sont comportementaux.
2. **La mutation de falsification a été jouée via un alias temporaire**, comme en 23-01 et pour la même
   raison explicitée par le plan de phase : une erreur de compilation fait échouer *toute* l'invocation
   `dotnet test` et ne dit rien de la garde. Alias et mutation révoqués ensemble, `git diff` du service vide
   après révocation.

## Issues Encountered

Aucun. Les huit tests sont passés du rouge au vert sans itération, et aucune garde préexistante n'a bronché.

## Known Stubs

Aucun. Aucun champ vide, aucun texte d'attente, aucune donnée non câblée n'a été introduit par ce plan.

## Invariants de sécurité — vérifiés avant et après

| Invariant | Attendu | Mesuré |
|---|---|---|
| `%APPDATA%\Chronos\sessions` | 66 entrées, **INCHANGÉ** | **66** (avant et après chaque tâche) |
| `%APPDATA%\Chronos\archived.json` | 84 octets | **84** |
| `%APPDATA%\Chronos\oauth.dat` (taille SEULE, jamais le mtime) | 518 octets | **518** |
| Overlay `Chronos-v3.0.2.exe` (pid 119412) | vivant, ni lancé ni tué | **vivant** (`tasklist` seul) |
| `git diff -- '*.csproj'` | vide | **vide** |
| `git status --porcelain` | rien hors `src/`, `tests/`, `.planning/` | **conforme** |
| Requêtes réseau réelles depuis un test | 0 | **0** |
| Aucune dépendance NuGet ajoutée | 0 | **0** |

Gardes acquises, revérifiées : `ServicesLayerPurity` 2/2, `CompositionRoot` 5/5, `NormalisationUnique` 3/3,
`GardesDoctrine` 8/8, `GardesPerimetre` 8/8, `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` verte.

## À VÉRIFIER PAR L'UTILISATEUR

Le test unitaire prouve le **mécanisme** ; il ne prouve pas que le dossier de l'utilisateur a changé
(leçon 21-04, `archived.json`). La phase s'est interdit de lancer ou de tuer l'overlay.

1. **Le magasin se résorbe (critère n°1 du ROADMAP).** Au prochain lancement **volontaire** de la version du
   dépôt, le dossier de **66 entrées** doit revenir aux seules sessions plausibles :

   ```sh
   ls "$APPDATA/Chronos/sessions" | wc -l          # avant : 66
   ls "$APPDATA/Chronos/sessions"/*.json | wc -l   # avant : 54
   ls "$APPDATA/Chronos/sessions" | grep -c 'tmp-' # avant : 12
   ```

   Attendu après : **0 fichier temporaire**, et seuls les états de moins de 72 h ou attestés vivants
   (ordre de grandeur : quelques unités sur 54 — 51 des 54 ont plus de 24 h, 48 plus de 7 jours).
   *En connaissance de cause* : ce lancement déclenche aussi la réconciliation de la phase 15 et la purge
   d'`archived.json` de la phase 21 (84 octets → `{}`).

2. **Un terminal tué (critère n°2).** Fermer brutalement un terminal, vérifier que le fichier d'état reste,
   puis qu'il a disparu au lancement suivant une fois passées les 72 h. Le widget, lui, cesse de l'afficher
   au bout de 8 h — comportement antérieur, à ne pas confondre avec le balayage.

3. **Le balayage ne ment pas (critère n°4), à l'œil.** Une session balayée doit simplement **disparaître** du
   widget. Si elle réapparaît marquée « traitée », ou présentée comme terminée, c'est un défaut à signaler.

4. **Le non-retour de la phase 22.** Rouvrir le diagnostic : il doit décrire le **nouveau** magasin alors
   qu'aucune ligne de `DiagnosticService.cs` n'a changé.

5. **Rien de tout cela ne s'exécute tant que l'exe n'est pas republié.** Les hooks installés dans
   `~/.claude/settings.json` pointent l'exe **v3.0.2** actuellement en cours d'exécution, antérieur à tout ce
   milestone. La republication (version dans l'exe **et** dans le nom du fichier publié) reste à faire en fin
   de milestone.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- **Phase 23 CLOSE.** CYC-01 et CYC-02 sont couvertes, `23-VALIDATION.md` porte `status: validated` avec les
  quatre critères du ROADMAP, leurs preuves nommées et leurs résultats mesurés.
- **Phase 24 (arbitrage par fraîcheur) débloquée sur ses deux prémisses.** *Côté écriture* (23-01) : un
  horodatage d'état n'est plus perdu en silence, donc il ne fait plus paraître un état plus vieux qu'il
  n'est. *Côté magasin* (23-02) : l'arbitrage ne portera plus sur un dossier de 37 jours d'âge médian dont
  32 entrées se déclarent faussement actives. Il restera à rendre la fusion elle-même honnête —
  `SessionMonitor.Inspecter` arbitre toujours **par ordre d'insertion**, et c'est resté intact volontairement
  (fichier absent du diff de la phase).
- **Aucune anticipation des phases 24 à 26** : `SessionMonitor.cs`, `SessionHookProcessor.cs`,
  `SessionHookInstaller.cs`, `TreatedStore.cs` et `ArchiveStore.cs` sont absents du diff de toute la phase.
- **Aucun blocage.** Les 66 entrées réelles subsistent volontairement sur la machine : c'est le code livré
  qui les balaiera, au prochain lancement, sous le contrôle de l'utilisateur.

---
*Phase: 23-un-magasin-qui-ne-croit-plus-et-n-oublie-plus*
*Completed: 2026-09-12*

## Self-Check: PASSED

- Fichiers annoncés présents : `BalayageMagasinSessions.cs` (146 lignes, min. 90),
  `BalayageMagasinSessionsTests.cs` (255 lignes, min. 130), `App.xaml.cs`, `GardesPerimetreTests.cs`,
  `CompositionRootTests.cs`, `23-VALIDATION.md`, ce SUMMARY.
- Commits annoncés présents dans l'historique : `2859afc`, `13ac1c7`, `1d8b637`, `43562fe`.
- Liens de câblage vérifiés par `grep` : `GetRequiredService<BalayageMagasinSessions>().Balayer()` → 1,
  `GetRequiredService<SessionMonitor>().Directory` → 1, `ISessionSource` présent dans le service.
- Suite complète revérifiée après écriture des documents : **763 / 763**, 0 échec.
