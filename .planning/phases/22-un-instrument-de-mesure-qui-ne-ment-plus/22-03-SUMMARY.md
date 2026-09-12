---
phase: 22-un-instrument-de-mesure-qui-ne-ment-plus
plan: 03
subsystem: observabilite
tags: [diagnostic, sessions, pertinence, validation, cloture-de-phase, tdd]

# Dependency graph
requires:
  - phase: 22-01
    provides: "AffichageSessions.Urgence / Age — le barème d'urgence et le libellé d'ancienneté du widget"
  - phase: 22-02
    provides: "Le champ _moniteurSessions injecté depuis le conteneur DI, et donc SessionMonitor.Directory"
  - phase: 15-hooks
    provides: "SessionHookProcessor.BuildStateJson — le format réel des fichiers d'état, posé tel quel en test"
provides:
  - "Sélection par PERTINENCE des fichiers d'état : ce qui attend d'abord, puis le plus récent"
  - "Le rapport annonce combien de fichiers il n'a PAS listés, au lieu de laisser croire qu'il a tout montré"
  - "Le dossier inspecté est celui du moniteur du widget (_moniteurSessions?.Directory)"
  - "Une date absente reste absente : « date inconnue », et le fichier est relégué au dernier rang"
  - "22-VALIDATION.md rempli : 4 critères, 4 preuves nommées, résultats mesurés, invariants de sécurité"
affects: [23 balayage des états expirés, 24 fusion par fraîcheur, 25 contrat d'événements, 26 TTL ArchiveStore]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Choisir un échantillon par un critère MOTIVÉ (urgence, fraîcheur) plutôt que par l'ordre où le système d'exploitation rend les fichiers"
    - "Borne de lisibilité ANNONCÉE : « … et N autre(s) non listé(s) » au lieu d'une troncature muette"
    - "Le barème de tri est appelé, jamais recopié : AffichageSessions reste le producteur unique"
    - "Assertions de test portées sur le BLOC de rapport concerné, jamais sur le rapport entier"

key-files:
  created:
    - .planning/phases/22-un-instrument-de-mesure-qui-ne-ment-plus/22-03-SUMMARY.md
  modified:
    - src/Chronos/Services/DiagnosticService.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - .planning/phases/22-un-instrument-de-mesure-qui-ne-ment-plus/22-VALIDATION.md

key-decisions:
  - "Lire les 54 fichiers pour n'en montrer que 8 : 15,15 ms mesurés, négligeable devant un rapport qui dure des secondes — le prix d'un choix motivé"
  - "Un fichier sans updated_at lisible est relégué au dernier rang ET annoncé « date inconnue » : lui prêter l'instant courant le ferait passer pour frais"
  - "Le dossier inspecté vient du moniteur injecté : deux dossiers pour un seul widget rouvriraient l'écart qu'OBS-01 vient de fermer"
  - "L'itérateur de la boucle s'appelle « fic » : un « e » existe déjà dans la même méthode (CS0136) — écart de compilation du plan, corrigé"
  - "Le compte attendu de AffichageSessions.Urgence était faux dans le plan (1 au lieu de 2) : corrigé dans la carte, pas absorbé en silence"

patterns-established:
  - "Un échantillon présenté comme un état des lieux doit être choisi par un critère, sinon il ment sans qu'on puisse le voir"
  - "Une troncature muette est un mensonge par omission : le reste se compte et s'annonce"
  - "Un test qui cherche une chaîne dans TOUT le rapport peut être vert pour la mauvaise section"

requirements-completed: [OBS-02]

# Metrics
duration: 9 min
completed: 2026-09-12
---

# Phase 22 Plan 03 : Des fichiers d'état choisis par pertinence Summary

**Le rapport de diagnostic ne tire plus ses exemples de l'ordre alphabétique des UUID : il liste les fichiers
d'état qui ATTENDENT d'abord, puis les plus récents, dit combien il n'en a pas montrés, et inspecte le dossier
que le widget lit réellement — et la phase 22 se referme sur une carte de vérification mesurée, critère par
critère.**

## Performance

- **Duration:** 9 min
- **Started:** 2026-09-12T16:09:30Z
- **Completed:** 2026-09-12T16:18:00Z
- **Tasks:** 2 (la première en TDD : rouge puis vert)
- **Files modified:** 3 (1 créé, 3 modifiés dont la carte de validation)

## Accomplishments

- **`files.Take(8)` a disparu.** Il rendait les **huit premiers fichiers par ordre alphabétique d'UUID** —
  sur la machine mesurée le 2026-09-12 (54 fichiers, dont 48 de plus de sept jours), les huit retenus étaient
  tous vieux de plusieurs semaines. C'est très probablement cette liste-là que l'utilisateur lisait quand il
  croyait voir « des sessions mortes » : elles apparaissaient dans le **rapport**, pas dans le widget.
- **La sélection est désormais motivée** : `OrderBy(Urgence).ThenByDescending(Maj).Take(MaxFichiersEtat)`.
  Le barème d'urgence est **appelé**, pas recopié — `AffichageSessions.Urgence`, le même producteur que le
  widget. Une session en attente de permission passe devant, **même vieille de 40 h**.
- **Le rapport dit ce qu'il ne montre pas** : `… et N autre(s) non listé(s) : ni en attente, ni parmi les plus
  récents`. Le compte total du dossier reste annoncé **avant** la liste. Huit fichiers ou moins → aucune ligne
  de reste, pas de bruit inutile.
- **Le dossier inspecté est celui du moniteur du widget** (`_moniteurSessions?.Directory`), avec repli sur le
  chemin déduit de `ChronosPaths` quand aucun moniteur n'est injecté. Lire un autre dossier que celui du widget
  rouvrirait exactement l'écart qu'OBS-01 vient de fermer.
- **Une date absente reste absente.** Un fichier sans `updated_at` lisible sort `(date inconnue)` et tombe au
  dernier rang. Lui prêter l'instant courant le ferait passer pour frais et le hisserait en tête — l'inverse
  exact de la doctrine du milestone.
- **Un fichier corrompu ne casse rien** : il est ignoré, le compte du dossier reste juste, et le rapport va
  jusqu'à sa section `[Conseil]`.
- **Le conseil « normal en app bureau » a été retiré** : la source app-bureau a été démolie en phase 21, cette
  phrase ne voulait plus rien dire. Le dossier vide dit maintenant « les hooks n'ont encore rien écrit ».
- **La phase est refermée par une carte vérifiable** : `22-VALIDATION.md` porte les 4 critères du ROADMAP,
  chacun avec sa preuve nommée et son résultat, les 3 mutations de falsification, les 3 commandes du partage
  d'instance, les invariants de sécurité mesurés, et ce qui reste à voir de ses yeux.
- **Le widget n'a changé en rien** : `SessionsTests.cs`, `TreatedSessionsTests.cs` et
  `SessionStylesBindingTests.cs` sont **absents du diff de toute la phase 22**.

## Task Commits

1. **Task 1 (ROUGE) : les fichiers d'état doivent être choisis par pertinence** — `d65fd03` (test)
   — 4 échecs / 25, les 20 tests préexistants verts.
2. **Task 1 (VERT) : sélection par pertinence, reste annoncé, dossier du moniteur** — `d3d0839` (feat)
3. **Task 2 : la carte de vérification de la phase, remplie et mesurée** — `0741e0b` (docs)

Aucune étape REFACTOR : l'implémentation est sortie propre du vert, et toute reformulation aurait risqué de
faire bouger les comptes de `grep` qui servent de gardes à cette phase.

**Plan metadata:** voir le commit `docs(22-03)` qui suit.

## Files Created/Modified

- `src/Chronos/Services/DiagnosticService.cs` *(modifié)* — constante `MaxFichiersEtat = 8` déclarée à côté de
  `UsageUrl` ; bloc des fichiers d'état réécrit : lecture complète du dossier, tri par urgence puis fraîcheur,
  borne annoncée, `(date inconnue)` pour les muets, dossier pris sur `_moniteurSessions?.Directory`.
- `tests/Chronos.Tests/DiagnosticServiceTests.cs` *(modifié)* — 5 tests ajoutés + 2 aides (`EcrireEtat`,
  `MoniteurSur`) et le sélecteur de bloc `LignesFichiersEtat`. **0 ligne préexistante supprimée**
  (`git diff -U0 0ac5c6e -- …DiagnosticServiceTests.cs | grep -c "^-[^-]"` → **0**).
- `.planning/phases/22-…/22-VALIDATION.md` *(modifié)* — carte remplie, `status: validated`.

## Decisions Made

- **Lire tout le dossier pour n'en montrer que huit.** 15,15 ms mesurés sur les 54 fichiers réels, contre un
  rapport qui dure plusieurs secondes (sonde réseau, inventaire machine). Le coût est nul à l'échelle utile, et
  c'est le prix d'un échantillon choisi plutôt que subi.
- **L'urgence vient de `AffichageSessions`, jamais d'une recopie.** Le rapport et le widget classent avec le
  même barème parce qu'ils appellent la même fonction. Deux barèmes jumeaux se ressembleraient le jour J et
  divergeraient à la première phase suivante — la faute même que cette phase corrige, une octave plus bas.
- **`maj is null` force l'urgence à 3.** Sans cela, un fichier sans date mais portant `activity:
  "WaitingAttention"` serait remonté **en tête** avec une date inconnue : le rapport présenterait comme
  prioritaire un fichier dont il ne sait pas s'il date d'hier ou de six semaines.
- **Repli conservé sur le chemin déduit** (`?? Path.Combine(…, "sessions")`) : contrairement au moniteur
  lui-même — qui n'a délibérément **aucun** repli et l'annonce — un chemin de dossier n'est pas une lecture.
  Sans moniteur, dire « voici ce que contient le dossier attendu » reste vrai ; fabriquer une lecture de
  sessions ne le serait pas. `?? new ` reste à **2**, l'acquis de la phase 20 est intact.
- **OBS-02 marqué complet ici**, et ici seulement : les plans 22-01 et 22-02 s'en étaient volontairement
  abstenus tant que la sélection des fichiers d'état restait alphabétique.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Le bloc de code du plan ne compilait pas (CS0136)**

- **Found during:** Task 1, phase VERT
- **Issue:** Le plan nommait l'itérateur de la boucle `e` : `foreach (var e in lus.OrderBy(e => …))`. Or un `e`
  existe déjà plus haut dans la même méthode `BuildReportAsync` (`if (exp is { } e)`, l'expiration du jeton), et
  C# interdit d'en masquer la portée. Erreur de compilation — donc, `tests/Chronos.Tests` portant un
  `ProjectReference` vers `Chronos`, **toute** l'invocation `dotnet test` échouait, pas seulement ce test.
- **Fix:** L'itérateur s'appelle `fic`, les lambdas de tri `x`, et un commentaire explique pourquoi, pour qu'une
  relecture future ne « corrige » pas le nom en le remettant à `e`.
- **Files modified:** `src/Chronos/Services/DiagnosticService.cs`
- **Verification:** build sans erreur ni avertissement ; 747 tests verts.
- **Committed in:** `d3d0839`

**2. [Rule 1 - Bug] Les tests du plan mesuraient la mauvaise section du rapport**

- **Found during:** Task 1, phase VERT (2 tests encore rouges après une implémentation pourtant correcte)
- **Issue:** Le plan posait que « le bloc des fichiers d'état précède celui des sessions, donc les premières
  lignes à puce sont les siennes » et sélectionnait `report.Split('\n').Where(l => l.StartsWith("· "))[0]`.
  C'est faux : le rapport contient des lignes à puce **bien avant**, dont
  `· dossier : %APPDATA%\Claude`. Les assertions portaient donc sur la section des chemins de l'app bureau.
  Symétriquement, `Assert.Contains("p-0", report)` cherchait dans le rapport ENTIER — or ces mêmes projets
  reparaissent plus bas dans la liste des sessions du widget : le test serait resté **vert avec un bloc de
  fichiers d'état totalement vide**. Un faux négatif d'un côté, un faux positif de l'autre.
- **Fix:** Une aide `LignesFichiersEtat(report)` isole les puces du **seul** bloc « Fichiers d'état »
  (`SkipWhile` jusqu'à l'en-tête, puis `TakeWhile` sur les puces). Les cinq tests assertent désormais sur ce
  bloc, et deux assertions de cardinalité (`Assert.Equal(8, lignes.Count)`) ont été ajoutées pour que la borne
  de lisibilité soit elle-même prouvée, pas seulement la présence des bons noms.
- **Files modified:** `tests/Chronos.Tests/DiagnosticServiceTests.cs`
- **Verification:** les 5 tests passent ; vérifié contra-factuellement par le rouge initial.
- **Committed in:** `d3d0839`

**3. [Rule 1 - Bug] Le compte attendu de `AffichageSessions.Urgence` était faux dans le plan**

- **Found during:** Task 1, contrôle des critères d'acceptation
- **Issue:** Le plan attendait `grep -cF "AffichageSessions.Urgence"` → **1**. Le compte réel est **2** : le
  plan 22-02 avait déjà introduit une occurrence pour trier les sessions MASQUÉES. Le plan comptait comme si
  le fichier partait de zéro.
- **Fix:** Aucune modification de code — l'**esprit** du critère (« l'ordre d'urgence vient de la couche
  partagée, il n'est pas recopié ») est tenu strictement : **zéro** recopie du barème, deux appels au
  producteur unique. L'écart et sa raison sont écrits noir sur blanc dans `22-VALIDATION.md`, plutôt
  qu'absorbés en ajustant le chiffre en silence.
- **Files modified:** `.planning/phases/22-…/22-VALIDATION.md`
- **Verification:** `grep -cF "AffichageSessions.Urgence"` → 2, dont 0 recopie de barème.
- **Committed in:** `0741e0b`

---

**Total deviations:** 3 auto-fixed (1 blocking, 2 bugs)
**Impact on plan:** Aucun élargissement de portée. Les trois écarts renforcent le livrable : le premier le rend
compilable, le deuxième supprime un faux positif ET un faux négatif dans les gardes de la phase, le troisième
empêche une fausse mesure de passer pour une vérification.

## Issues Encountered

None.

## Verification

| Contrôle | Attendu | Obtenu |
| --- | --- | --- |
| `dotnet test Chronos.sln -c Debug --nologo -v q` (passe 1) | 0 échec, ≥ 719 | **0 échec, 747 réussis / 4 s** |
| `dotnet test …` (passe 2, chargeur BAML) | 0 échec | **0 échec, 747 réussis / 4 s** |
| Six gardes nommées de la phase | vertes | **24 tests, 0 échec** |
| `grep -cF "files.Take(8)"` (DiagnosticService.cs) | 0 | **0** |
| `grep -cF "Take(8)"` (DiagnosticService.cs) | 1 (`DescribeBlobShape`) | **1** |
| `grep -cF "private const int MaxFichiersEtat = 8;"` | 1 | **1** |
| `grep -cF "Take(MaxFichiersEtat)"` | 1 | **1** |
| `grep -cF "_moniteurSessions?.Directory"` | 1 | **1** |
| `grep -cF "AffichageSessions.Urgence"` | 1 *(attendu du plan)* | **2** — écart expliqué, 0 recopie de barème |
| `grep -cF "InstantDepuisEpochMillisecondes"` | 2, INCHANGÉ | **2** |
| `grep -cF "normal en app bureau"` | 0 | **0** |
| `grep -cF "new SessionMonitor"` (DiagnosticService.cs) | 0 | **0** |
| `grep -cF "?? new "` (DiagnosticService.cs) | 2 (acquis phase 20) | **2** |
| `grep -cF "GetRequiredService<SessionMonitor>()"` (App.xaml.cs) | 2 | **2** |
| `grep -cF "=> Inspecter(now).Visibles;"` (SessionMonitor.cs) | 1 | **1** |
| Lignes préexistantes supprimées de `DiagnosticServiceTests.cs` | 0 | **0** |
| `SessionsTests.cs` / `TreatedSessionsTests.cs` / `SessionStylesBindingTests.cs` dans le diff de la phase | absents | **absents** |
| Fichiers du diff du plan | les 2 déclarés + la carte | **exactement** |
| `grep -cF "(à mesurer)"` (22-VALIDATION.md) | 0 | **0** |
| `grep -cF "À VÉRIFIER PAR L'UTILISATEUR"` (22-VALIDATION.md) | 1 | **1** |
| `ls %APPDATA%\Chronos\sessions \| wc -l` | 66 | **66** |
| `archived.json` | 84 octets | **84** |
| `oauth.dat` (taille seule, jamais le mtime) | 518 octets | **518** |
| Overlay `Chronos-v3.0.2.exe` (pid 119412) | vivant, ni lancé ni tué | **vivant** |
| `git status --porcelain` | rien hors src/, tests/, .planning/ | **vide** |
| Dépendances NuGet ajoutées | 0 | **0** |

## Known Stubs

Aucun. Chaque ligne ajoutée au rapport est alimentée par une lecture réelle du disque, et le cas « pas de date »
est un état nommé (`date inconnue`), pas un placeholder.

## À VÉRIFIER PAR L'UTILISATEUR

Ce que seul un vrai lancement peut établir — la contrainte de phase interdisait de lancer ou de tuer l'overlay,
et aucun test n'a touché aux données réelles. Détail complet dans `22-VALIDATION.md`.

1. **Comparaison ligne à ligne (critère n°1, in vivo).** Widget de sessions affiché, ouvrir
   **clic droit → Diagnostic…**. Dans « Sessions AFFICHÉES par le widget » : mêmes projets, mêmes états
   (« à toi » / « tour fini » / « en cours » / « inconnu »), mêmes âges, même ordre qu'à l'écran. Tout écart est
   un défaut, pas une approximation.
2. **Ce qui manque à l'écran doit être nommé (critère n°2, in vivo).** Dans « Sessions MASQUÉES par un filtre » :
   toute session absente du widget doit y figurer avec le fichier qui la masque. Cas attendu sur cette machine :
   `e465420e` (PROJET ADVANCED SHEET), masquée par `treated.json`.
3. **Des exemples qui servent (critère n°3, in vivo).** Dans « Fichiers d'état » : l'en-tête doit nommer le
   dossier du moniteur (`…\Chronos\sessions) : 54`), les lignes listées doivent être des sessions **récentes ou
   en attente** — plus d'entrées vieilles de plusieurs semaines — et la ligne « … et N autre(s) non listé(s) »
   doit apparaître (attendu ~46 sur 54). Une ligne `(date inconnue)` est un fait à signaler.
4. **Le non-retour, à l'échelle du milestone.** À la fin de la phase 26, rouvrir le diagnostic : il doit décrire
   le **nouveau** widget alors qu'aucune ligne de `DiagnosticService.cs` n'aura changé entre-temps. C'est la
   vérification décisive de cette phase, et elle ne peut être faite qu'à la fin.
5. **Reliquats hérités de la phase 21**, à reprendre à l'occasion : purge réelle d'`archived.json`
   (84 octets → `{}`) au premier lancement volontaire, cohérence des **8 styles × 9 thèmes**, et **absence de
   lignes `desktop:`** après un usage prolongé avec l'app bureau ouverte.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- **Phase 22 close.** Les quatre critères du ROADMAP ont chacun une preuve nommée et un résultat mesuré ; les
  trois gardes de non-retour ont été falsifiées puis révoquées ; le partage d'instance est établi par trois
  commandes indépendantes du contenu du rapport.
- **L'instrument est prêt à servir les phases 23 à 26.** C'était la raison de son placement en n°2 du milestone :
  vérifier un balayage, une fusion par fraîcheur ou une refonte du « traité » avec un rapport qui reconstruisait
  son propre moniteur nu aurait été vérifier deux systèmes différents.
- **Rien n'a été anticipé** : les 12 `.tmp` orphelins et les 48 fichiers périmés sont **toujours là** (phase 23),
  l'écriture reste directe (phase 23), la fusion reste par ordre d'insertion (phase 24), les 5 hooks câblés sont
  inchangés (phase 25), les deux magasins gardent leur TTL et leur sémantique (phase 26).
- **Aucun stub, aucune dépendance ajoutée, aucune donnée utilisateur touchée.**

---
*Phase: 22-un-instrument-de-mesure-qui-ne-ment-plus*
*Completed: 2026-09-12*

## Self-Check: PASSED

Les 2 fichiers de code modifies, la carte `22-VALIDATION.md` et ce SUMMARY existent sur disque ; les
3 commits de tache (`d65fd03`, `d3d0839`, `0741e0b`) sont presents dans l'historique.
