---
phase: 23-un-magasin-qui-ne-croit-plus-et-n-oublie-plus
plan: 01
subsystem: services
tags: [sessions, hooks, io, filestream, exit-code, stderr, utf8, tdd]

# Dependency graph
requires:
  - phase: 21-perimetre-le-widget-ne-parle-que-de-claude-code
    provides: "ISessionSource / TranscriptSessionSource, précédent d'écriture DIRECTE d'ArchiveStore.PurgerPrefixe"
  - phase: 22-un-instrument-de-mesure-qui-ne-ment-plus
    provides: "SessionMonitor.Read = projection d'Inspecter, partage d'instance du moniteur avec le diagnostic"
provides:
  - "Services/EcritureEtatSession : l'écriture d'état de session, DIRECTE, hors de la couche WPF, testable"
  - "ResultatEcritureEtat : un résultat porteur de sa CAUSE réelle d'échec (type + message de l'exception)"
  - "RunSessionHook rend un CODE DE SORTIE (0 ou 1, jamais 2) et écrit sa cause sur le flux d'erreur en UTF-8"
  - "Garde de câblage Le_mode_hook_ecrit_par_le_service_teste_et_signale_son_echec, prouvée falsifiable"
affects: [23-02 balayage du magasin, 24 arbitrage par fraîcheur, 25 contrat d'événements]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Écriture de fichier DIRECTE (FileMode.Create / FileShare.Read) contre un lecteur concurrent, au lieu d'un fichier temporaire déplacé par-dessus la cible"
    - "Un point d'entrée WPF non testable délègue à un service neutre ; la garde de câblage sur le TEXTE source empêche le débranchement"
    - "Code de sortie de hook Claude Code : 1 (non bloquant) et jamais 2 (bloquant)"

key-files:
  created:
    - src/Chronos/Services/EcritureEtatSession.cs
    - tests/Chronos.Tests/EcritureEtatSessionTests.cs
  modified:
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs

key-decisions:
  - "L'écriture directe est un ARBITRAGE assumé, pas un optimum : elle tronque la cible avant de la réécrire, donc un lecteur malchanceux peut lire un fragment et le relira 2 s plus tard — contre une perte DÉFINITIVE auparavant."
  - "ResultatEcritureEtat porte la cause RÉELLE (type + message de l'exception levée), jamais un libellé fabriqué : c'est ce qui distingue « rien à faire » d'« impossible »."
  - "Le hook sort 1 et JAMAIS 2 : un code 2 renverrait stderr à Claude et BLOQUERAIT l'action de l'utilisateur. « Constatable » ne doit pas devenir « bloquant »."
  - "Rien à faire n'est pas un échec : événement ignoré, sans session_id, ou suppression d'un fichier déjà absent rendent Reussi == true avec Cause == null (précédent ArchiveStore.PurgerPrefixe)."
  - "ArchiveStore.Add garde volontairement son écriture par fichier temporaire : il n'a aucun canal pour rendre un échec constatable, donc changer sa mécanique produirait un correctif invérifiable."
  - "DiagnosticService.cs est ABSENT du diff : le rapport reflète le changement sans qu'une ligne n'y bouge — c'est la preuve à l'usage du partage d'instance de la phase 22."

patterns-established:
  - "Étape ROUGE jouée contre un SQUELETTE compilable (NotImplementedException) : 6 échecs / 0 succès, donc comportementaux et non de compilation"
  - "Mutation de falsification jouée AVANT commit, avec un alias temporaire pour que le dépôt reste compilable — sinon dotnet test échouerait à la compilation et ne dirait rien de la garde"

requirements-completed: [CYC-02]

# Metrics
duration: 6min
completed: 2026-09-12
---

# Phase 23 Plan 01 : L'écriture d'état sort de la couche WPF, devient directe, et son échec se voit — Summary

**`EcritureEtatSession.Appliquer` remplace l'écriture par fichier temporaire du mode `--hook` par une
écriture directe `FileMode.Create`/`FileShare.Read` (200 écritures sous lecteur concurrent, 0 perte),
rend sa CAUSE au lieu de l'avaler, et `RunSessionHook` l'écrit sur le flux d'erreur en UTF-8 avant de
sortir avec 1 — jamais 2.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-09-12T16:50:47Z
- **Completed:** 2026-09-12T16:56:41Z
- **Tasks:** 2 (dont 1 en TDD, 2 commits)
- **Files modified:** 4 (2 créés, 2 modifiés)

## Accomplishments

- **Le défaut mesuré est corrigé à sa mécanique.** L'écriture passait par un fichier temporaire déplacé
  par-dessus la cible — 290 pertes sur 500 sous lecteur serré. Elle est désormais DIRECTE, et le scénario
  mesuré est rejoué en test : **200 écritures pendant qu'un lecteur tient la cible en `FileShare.ReadWrite`
  (le mode EXACT de `SessionMonitor.TryRead`) → 0 échec**, et le contenu final sur disque porte bien le
  DERNIER `updated_at` écrit.
- **Le silence est levé.** `ResultatEcritureEtat` porte la cause réelle ; `RunSessionHook` la publie sur le
  flux d'erreur du processus en UTF-8 strict et rend un code de sortie. Plus aucun `catch` vide dans le
  chemin d'écriture.
- **L'écriture a quitté `App.xaml.cs`** — fichier WPF que nul test ne peut instancier, ce qui est
  précisément la raison pour laquelle ce défaut a vécu si longtemps sans couverture. Six tests la couvrent.
- **Plus aucun débris.** Une écriture réussie laisse exactement `<session_id>.json` dans le dossier : la
  garde des 12 orphelins mesurés sur la machine.
- **747 → 754 tests verts** (+6 en Task 1, +1 en Task 2), 0 échec, deux exécutions consécutives, 0 avertissement.

## Task Commits

1. **Task 1 — ROUGE : le scénario mesuré du lecteur concurrent** - `0d7b81c` (test)
2. **Task 1 — VERT : l'écriture quitte la couche WPF et rend sa cause** - `f3310d2` (feat)
3. **Task 2 : le mode `--hook` consomme le résultat, dit son échec et sort 1** - `503d868` (feat)

**Plan metadata:** (commit final `docs(23-01)`)

## Chiffres demandés par le plan

| Mesure | Valeur |
|---|---|
| **Compte du ROUGE** | **6 échecs / 0 succès / 6 au total** — comportemental, pas de compilation (build 0 erreur / 0 avertissement au commit `0d7b81c`) |
| **200 écritures sous lecteur concurrent** | **0 échec** ; `updated_at` final = dernier écrit (`Assert.Equal(0, echecs)`) |
| **Mutation de falsification** | `EcritureEtatSession.Appliquer(` → `EcritureEtatSession.AppliquerMUTANT(` dans `App.xaml.cs` → **`Chronos.Tests.GardesPerimetreTests.Le_mode_hook_ecrit_par_le_service_teste_et_signale_son_echec` [FAIL]**, 1 échec / 6 succès sur le filtre `GardesPerimetre`. Mutation révoquée, `git diff` du service vide, `grep -cF "EcritureEtatSession.Appliquer(" src/Chronos/App.xaml.cs` → **1** |
| **Suite complète** | **754 / 754**, 0 échec, ~4 s, **deux exécutions consécutives** identiques |

## Contrepartie ASSUMÉE de l'écriture directe

L'écriture directe **tronque la cible avant de la réécrire**. Un lecteur qui tombe exactement dans cette
fenêtre lit un fragment de JSON. `SessionMonitor.TryRead` ignore un fichier illisible (son `catch` rend
`null`) et le relira **deux secondes plus tard**.

Ce qui est échangé : **une lecture possiblement manquée sur un cycle de 2 s**, contre **une écriture perdue
définitivement et en silence**. Ce n'est pas le même ordre de gravité — et c'est exactement ce que la phase 24
ne pourra pas se permettre, puisqu'elle arbitrera en comparant des horodatages : un état perdu paraît **plus
vieux qu'il n'est**.

Le coût est en outre borné par la mesure : en production, ~0,7 événement sur 5 000 est concerné. CYC-02 visait
**le silence**, pas l'optimisation fine.

## Files Created/Modified

- `src/Chronos/Services/EcritureEtatSession.cs` *(créé, 72 lignes)* — `ResultatEcritureEtat` (résultat porteur
  de sa cause) et `EcritureEtatSession.Appliquer(dossier, resultat)` : applique au disque l'ordre rendu par
  `SessionHookProcessor`, en écriture DIRECTE, et rend le résultat au lieu de l'avaler.
- `tests/Chronos.Tests/EcritureEtatSessionTests.cs` *(créé, 6 tests)* — lecteur concurrent × 200, échec rendu
  avec sa cause sans jamais lever, `SessionEnd` sur fichier présent puis absent, événement ignoré, absence de
  débris, relecture par le vrai `SessionMonitor`. Tous en dossier temporaire, avec la garde
  `Assert.StartsWith(Path.GetTempPath(), …)`.
- `src/Chronos/App.xaml.cs` *(modifié)* — `RunSessionHook` devient `private static int`, délègue l'écriture au
  service, signale la cause par `SignalerSurErreurStandard` (flux d'erreur brut + `UTF8Encoding(false)`) et
  rend 1 ; l'aiguillage `--hook` fait `Environment.Exit(RunSessionHook(…))`.
- `tests/Chronos.Tests/GardesPerimetreTests.cs` *(modifié)* — garde de câblage
  `Le_mode_hook_ecrit_par_le_service_teste_et_signale_son_echec`.

## Decisions Made

1. **L'écriture directe est un arbitrage, et il est écrit dans le code.** Le XML-doc du service porte la
   contrepartie (lecture fragmentaire possible) plutôt que de la taire. Le lecteur du code suivant ne pourra
   pas croire qu'on a juste « simplifié ».
2. **La cause n'est jamais fabriquée** : `ex.GetType().Name + " : " + ex.Message`. Un test l'ancre en exigeant
   la présence du nom de type (`Assert.Contains("Exception", res.Cause!)`). Un libellé maison aurait rendu la
   garde tautologique.
3. **Code de sortie 1, jamais 2.** Le contrat des hooks Claude Code fait de 2 une erreur **bloquante** —
   `stderr` renvoyé à Claude, action bloquée. `grep -c "return 2;" src/Chronos/App.xaml.cs` → **0**.
4. **`Environment.Exit(0)` ne subsiste qu'une fois** dans `App.xaml.cs` : celui du mode `--statusline`. Celui
   du mode `--hook` a été remplacé par la transmission du code rendu.
5. **UTF-8 strict sur le flux d'erreur**, jamais `Console.Error` : l'encodage OEM par défaut mutilerait les
   accents du message — leçon déjà tirée pour `RunStatusLineBridge`, désormais appliquée aux deux flux.
6. **`ArchiveStore.Add` n'a pas été touché.** Il écrit toujours par fichier temporaire. Il n'a aucun canal
   pour rendre un échec constatable (méthode `void`, appelée depuis l'UI) : changer sa mécanique aurait
   produit un correctif invérifiable. Hors périmètre, consigné plutôt que fait en passant.
7. **Horloge : `DateTimeOffset.UtcNow` = 0 dans le service et 0 dans ses tests.** Le service ne lit aucune
   horloge (l'horodatage lui arrive déjà calculé par `SessionHookProcessor.Process`), et les tests dérivent
   tous leurs `updated_at` d'un littéral fixe `Maintenant`. Le piège de test à retardement de la phase 22 ne
   peut pas se reproduire ici.

## Deviations from Plan

**None - plan executed exactly as written.**

Une seule précision d'exécution, sans écart de contenu : la mutation de falsification exigée par le plan
(`Appliquer(` → `AppliquerMUTANT(`) ne compile pas telle quelle. Elle a été jouée avec un **alias temporaire**
`AppliquerMUTANT => Appliquer` ajouté au service le temps de la mesure, de sorte que le dépôt reste
compilable et que l'échec observé soit bien celui de la **garde** et non de la compilation (précédent
explicite du plan : « une erreur de compilation fait échouer *toute* l'invocation `dotnet test` »). Alias et
mutation révoqués ensemble ; `git diff` du service vide après révocation.

## Issues Encountered

Aucun. Les six tests sont passés du rouge au vert sans itération, et aucune garde préexistante n'a bronché.

## Invariants de sécurité — vérifiés avant et après

| Invariant | Attendu | Mesuré |
|---|---|---|
| `%APPDATA%\Chronos\sessions` | 66 entrées | **66** |
| `%APPDATA%\Chronos\archived.json` | 84 octets | **84** |
| `%APPDATA%\Chronos\oauth.dat` (taille SEULE, jamais le mtime) | 518 octets | **518** |
| Overlay `Chronos-v3.0.2` (pid 119412) | vivant, ni lancé ni tué | **vivant** |
| `git status --porcelain` | rien hors `src/`, `tests/`, `.planning/` | **conforme** |
| `git diff -- '*.csproj'` | vide | **vide** |
| `DiagnosticService.cs` dans le diff | absent | **absent** (`git diff --stat` vide) |
| Aucune dépendance NuGet ajoutée | 0 | **0** |

Gardes acquises, revérifiées : `new SessionMonitor` dans `DiagnosticService.cs` → **0** (phase 22) ;
`?? new ` → **2** (phase 20) ; `=> Inspecter(now).Visibles;` dans `SessionMonitor.cs` → **1** (phase 22).
`ServicesLayerPurity`, `CompositionRoot`, `NormalisationUnique`, `GardesDoctrine`, `GardesPerimetre` :
25 tests, 0 échec.

## User Setup Required

None - no external service configuration required.

Note d'usage : le correctif ne prendra effet chez l'utilisateur qu'avec un **exe republié** — les hooks
installés dans `~/.claude/settings.json` pointent l'exe v3.0.2 actuellement en cours d'exécution. Aucune
action demandée à ce stade du milestone.

## Next Phase Readiness

- **23-02 (CYC-01, balayage) est débloqué et sa dépendance de fond est satisfaite** : le balayage datera les
  fichiers par leur `updated_at`, et ces dates sont désormais celles des événements réels.
- **Conflit de fichiers levé** : 23-01 possédait `App.xaml.cs` et `GardesPerimetreTests.cs` ; les deux sont
  committés, 23-02 peut les reprendre.
- **Phase 24 (arbitrage par fraîcheur)** : sa prémisse — un horodatage d'état est fiable — est maintenant
  vraie côté écriture. Il restera à la rendre vraie côté fusion.
- Aucun blocage. Les 12 `.tmp` orphelins réels subsistent volontairement sur la machine : c'est le code livré
  par 23-02 qui les balaiera, au prochain lancement, sous son contrôle.

---
*Phase: 23-un-magasin-qui-ne-croit-plus-et-n-oublie-plus*
*Completed: 2026-09-12*

## Self-Check: PASSED

- Fichiers annoncés présents : `EcritureEtatSession.cs` (72 lignes, min. 60), `EcritureEtatSessionTests.cs`
  (166 lignes, min. 90), `App.xaml.cs`, `GardesPerimetreTests.cs`, ce SUMMARY.
- Commits annoncés présents dans l'historique : `0d7b81c`, `f3310d2`, `503d868`.
- Liens de câblage vérifiés par `grep` : `EcritureEtatSession.Appliquer(` → 1, `OpenStandardError` → 1.
