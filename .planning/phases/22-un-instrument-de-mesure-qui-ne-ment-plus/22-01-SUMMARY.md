---
phase: 22-un-instrument-de-mesure-qui-ne-ment-plus
plan: 01
subsystem: sessions
tags: [sessions, observabilite, monitor, mvvm, refactor, tdd]

# Dependency graph
requires:
  - phase: 21-p-rim-tre-le-widget-ne-parle-que-de-claude-code
    provides: "SessionSnapshot à cinq champs, ISessionSource comme point de substitution, SessionMonitor fusionnant transcripts + hooks"
  - phase: 14-hysteresis-traite
    provides: "TreatedStore / SessionTreatmentTracker — le filtre « traité » qui masquait e465420e"
provides:
  - "MotifMasquage / SessionMasquee / LectureSessions — le vocabulaire de ce que le moniteur écarte"
  - "SessionMonitor.Inspecter(now) : visibles + masquées (avec motif) + compteur de fichiers de hook périmés"
  - "SessionMonitor.Read(now) réduit à une projection d'Inspecter — plus aucun filtre dupliqué"
  - "AffichageSessions : producteur unique de l'ordre, du libellé d'état et du libellé d'ancienneté du widget"
  - "Le cas réel e465420e rejoué en test, aux chiffres du relevé du 2026-09-12T13:28Z"
affects: [22-02 câblage du diagnostic, 22-03 sélection des fichiers d'état, 23 balayage, 24 fusion par fraîcheur, 25 contrat d'événements, 26 TTL ArchiveStore]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Projection : la méthode historique délègue à la méthode riche (Read => Inspecter(now).Visibles) au lieu de réimplémenter"
    - "Rendre compte du filtre plutôt que jeter en silence : chaque session écartée sort nommée avec son motif"
    - "Distinguer « périmé » de « illisible » via un paramètre out, là où les deux rendaient null"
    - "Mise en forme du widget extraite en couche neutre (Services/) pour être partagée avec un consommateur texte"

key-files:
  created:
    - src/Chronos/Services/LectureSessions.cs
    - src/Chronos/Services/AffichageSessions.cs
    - tests/Chronos.Tests/InspectionSessionsTests.cs
    - tests/Chronos.Tests/AffichageSessionsTests.cs
  modified:
    - src/Chronos/Services/SessionMonitor.cs
    - src/Chronos/ViewModels/SessionsViewModel.cs

key-decisions:
  - "Read devient une projection d'Inspecter : une seule implémentation des filtres, donc aucun consommateur ne peut décrire un système différent de celui qui tourne"
  - "Un fichier de hook écarté pour ancienneté n'est PAS un masquage : il est compté à part, parce qu'une session dont le signal a expiré n'est pas cachée, elle est inconnue"
  - "L'archivage prime sur le « traité » quand les deux s'appliquent : une seule entrée masquée, motif Archivee — le geste de l'utilisateur passe avant l'hystérésis automatique"
  - "L'ordre, le libellé d'état et le libellé d'ancienneté descendent en couche neutre ; la couleur et les drapeaux WPF restent dans le ViewModel"
  - "OBS-01 n'est PAS marqué complet : le contrat est posé, mais le diagnostic ne le consomme qu'au plan 22-02"

patterns-established:
  - "Projection : toute vue historique appelle la vue riche et n'en garde que la partie qui la concerne"
  - "Vocabulaire d'absence : « absent » et « masqué par tel filtre » sont deux faits distincts, nommés séparément"
  - "Extraction à comportement constant : chaînes, seuils et ordre recopiés au caractère près, prouvés par des tests existants restés verts sans retouche"

requirements-completed: []

# Metrics
duration: 6 min
completed: 2026-09-12
---

# Phase 22 Plan 01 : Inspecter + AffichageSessions Summary

**`SessionMonitor.Inspecter` nomme désormais chaque session masquée et son filtre (`Archivee` / `Traitee`) et compte les fichiers de hook tombés pour ancienneté ; `Read` n'est plus qu'une projection d'`Inspecter`, et l'ordre / l'état / l'ancienneté du widget vivent dans un seul type neutre `AffichageSessions`.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-09-12T15:51:24Z
- **Completed:** 2026-09-12T15:57:09Z
- **Tasks:** 2 (chacune en TDD : rouge puis vert)
- **Files modified:** 6 (4 créés, 2 modifiés)

## Accomplishments

- **Le moniteur sait dire ce qu'il masque.** `Inspecter(now)` rend `Visibles`, `Masquees` (chaque session écartée
  avec son `MotifMasquage`) et `FichiersEcartesParAnciennete`. Ce qui était un `continue` silencieux est devenu
  une observation rendue.
- **`Read` ne contient plus aucune logique de filtre** : `public IReadOnlyList<SessionSnapshot> Read(now) => Inspecter(now).Visibles;`.
  C'est la condition structurelle de la phase — il n'existe plus qu'une implémentation des filtres d'archivage et
  de « traité » dans le fichier (`grep -cF "archived.Contains"` = 1, `grep -cF "ContainsKey(s.SessionId)"` = 1).
- **`TryRead` distingue « périmé » de « illisible »** via `out bool perimee`. Les deux rendaient `null` ; les
  confondre effaçait le fait qui explique l'essentiel de l'écart entre 54 fichiers sur disque et 1 ligne à l'écran.
- **Le cas réel `e465420e` est rejoué en test**, aux chiffres du relevé du 2026-09-12T13:28Z (hook de 625 min,
  transcript frais de 10 min, `treated.json` portant l'identifiant) : `Visibles` vide, une entrée masquée `Traitee`
  nommant la session, `FichiersEcartesParAnciennete` = 1.
- **`AffichageSessions` est le producteur unique** de `Ordonner` / `Urgence` / `Etat` / `Age`. Le ViewModel ne porte
  plus un seul libellé d'état en dur, et ses doublons `Rank` et `Age` ont disparu.
- **Le widget affiche exactement ce qu'il affichait** : `SessionsTests.cs`, `TreatedSessionsTests.cs` et
  `SessionStylesBindingTests.cs` sont restés verts **sans une seule retouche** — absents du diff du plan.

## Task Commits

1. **Task 1 (ROUGE) : le moniteur doit dire ce qu'il masque** — `6e7d188` (test)
2. **Task 1 (VERT) : Inspecter rend les masquages, Read n'en est qu'une projection** — `4fa2e11` (feat)
3. **Task 2 (ROUGE) : la mise en forme du widget doit avoir un producteur unique** — `e4ff566` (test)
4. **Task 2 (VERT) : AffichageSessions, producteur unique de la forme du widget** — `796685d` (feat)

Aucune étape REFACTOR n'a été nécessaire : les deux implémentations sont sorties propres du vert, et toute
reformulation aurait risqué de faire bouger ce que le widget affiche — ce que le plan interdit explicitement.

**Plan metadata:** voir le commit `docs(22-01)` qui suit.

## Files Created/Modified

- `src/Chronos/Services/LectureSessions.cs` *(créé)* — `MotifMasquage` (Archivee / Traitee), `SessionMasquee`,
  `LectureSessions`. Le vocabulaire de ce qui est masqué, et la documentation du coût de son absence.
- `src/Chronos/Services/SessionMonitor.cs` *(modifié)* — `Inspecter` porte toute la lecture ; `Read` délègue ;
  `TryRead(file, now, out bool perimee)` sépare périmé et illisible.
- `src/Chronos/Services/AffichageSessions.cs` *(créé)* — `Ordonner`, `Urgence`, `Etat`, `Age`. Couche neutre,
  aucun type WPF, prête pour le rapport de diagnostic.
- `src/Chronos/ViewModels/SessionsViewModel.cs` *(modifié)* — trois délégations à `AffichageSessions` ;
  `Rank` et `Age` supprimés ; `Describe` réduit à la couleur et au drapeau d'attente.
- `tests/Chronos.Tests/InspectionSessionsTests.cs` *(créé)* — 6 tests dont le cas `e465420e` et la preuve que
  `Read` est une projection d'`Inspecter`.
- `tests/Chronos.Tests/AffichageSessionsTests.cs` *(créé)* — 10 tests dont la non-régression du widget
  (libellés et ordre identiques via le vrai ViewModel).

## Decisions Made

- **`Read` projection plutôt que duplication.** Le plan en faisait la condition de placement de la phase : tant
  qu'un filtre reste dupliqué, « partager l'instance du moniteur » ne garantit rien. Vérifié par grep : un seul
  `archived.Contains`, un seul `ContainsKey(s.SessionId)`, un seul `=> Inspecter(now).Visibles;`.
- **Ancienneté ≠ masquage.** Un fichier de hook tombé pour son âge n'apparaît ni dans `Visibles` ni dans
  `Masquees` : on ne sait plus rien de cette session, ce n'est pas un filtre. D'où un compteur séparé.
- **Ordre d'évaluation des filtres conservé** (archivage d'abord), et documenté comme significatif : le geste
  explicite de l'utilisateur prime sur une hystérésis automatique, et la session ne compte qu'une fois.
- **Pas de REFACTOR cosmétique** sur un plan dont le critère est « le widget affiche exactement la même chose ».
- **OBS-01 laissé « Pending »** dans REQUIREMENTS.md — voir « Deviations » ci-dessous.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Horodatages de magasin figés dans le passé = tests à retardement**

- **Found during:** Task 1 (rédaction d'`InspectionSessionsTests`)
- **Issue:** Le plan écrivait `treated.Set(id, Maintenant.ToUnixTimeMilliseconds())` avec `Maintenant` =
  2026-09-12T13:28:00Z, une constante figée. Or `TreatedStore.Load()` purge au-delà d'un TTL de **6 h mesuré sur
  l'horloge réelle** (`DateTimeOffset.UtcNow`, aucune horloge injectable). Trois tests seraient donc devenus
  rouges à partir du 2026-09-12T19:28Z, sans qu'aucun code de production n'ait changé — exactement le genre de
  garde qu'on apprend à ignorer, et que le dépôt proscrit.
- **Fix:** Un helper nommé `EcritMaintenant()` rend `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` pour les
  écritures dans le magasin, avec le commentaire qui explique pourquoi. Les instants des snapshots restent la
  constante `Maintenant` : c'est l'arithmétique du relevé réel, et elle doit rester littérale. La valeur écrite
  n'est jamais assertée — le filtre ne regarde que la présence de la clé.
- **Files modified:** `tests/Chronos.Tests/InspectionSessionsTests.cs`
- **Verification:** les 6 tests passent ; ils ne dépendent plus de l'heure d'exécution.
- **Committed in:** `6e7d188` (commit ROUGE de la Task 1)

**2. [Rule 3 - Blocking] OBS-01 non marqué complet dans REQUIREMENTS.md**

- **Found during:** mise à jour des métadonnées, après la Task 2
- **Issue:** Le pas mécanique du workflow marque complets les identifiants du frontmatter du plan. OBS-01
  (« le diagnostic dit exactement ce que le widget affiche ») est **partagé avec le plan 22-02**, qui porte le
  câblage. Le cocher ici affirmerait une capacité qui n'existe pas encore : aucun diagnostic ne consomme
  `Inspecter` à cette heure.
- **Fix:** `requirements-completed: []` et aucun appel à `requirements mark-complete`. OBS-01 sera coché par
  22-02, quand le rapport lira réellement le moniteur du widget.
- **Files modified:** aucun (REQUIREMENTS.md laissé intact)
- **Verification:** `.planning/REQUIREMENTS.md` l. 121 — OBS-01 toujours `Pending`.
- **Committed in:** n/a (abstention volontaire)

---

**Total deviations:** 2 auto-fixed (1 bug, 1 blocking)
**Impact on plan:** Aucun élargissement de portée. Le premier écart supprime un test à retardement ; le second
empêche une fausse déclaration de complétude — les deux servent la doctrine d'honnêteté du projet.

## Issues Encountered

None.

## Verification

| Contrôle | Attendu | Obtenu |
| --- | --- | --- |
| `dotnet test Chronos.sln -v q --nologo` | 0 échec, ≥ 719 | **0 échec, 735 réussis** (719 + 16) |
| `grep -cF "=> Inspecter(now).Visibles;" SessionMonitor.cs` | 1 | **1** |
| `SessionsTests.cs` / `TreatedSessionsTests.cs` / `SessionStylesBindingTests.cs` dans le diff | absents | **absents** |
| `DiagnosticService.cs` et `App.xaml.cs` | intacts | **intacts** |
| `%APPDATA%\Chronos\sessions` | 66 entrées | **66** |
| `archived.json` | 84 octets | **84** |
| `oauth.dat` (taille seule, jamais le mtime) | 518 octets | **518** |
| Overlay `Chronos-v3.0.2.exe` (pid 119412) | vivant, non touché | **vivant** |
| `grep -c "AffichageSessions\." SessionsViewModel.cs` | 3 | **3** |
| `grep -cF '"à toi"' SessionsViewModel.cs` | 0 | **0** |
| Motifs interdits (`NormalisationUniqueTests`) dans les 2 fichiers créés | 0 | **0** |

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- **Prêt pour 22-02.** Les deux contrats que le câblage du diagnostic consommera sont posés et testés :
  `SessionMonitor.Inspecter` (ce qui est vu, ce qui est masqué, par quel filtre) et `AffichageSessions`
  (ordre, état, ancienneté), tous deux neutres et sans type WPF.
- **L'argument de partage d'instance est désormais vrai par construction** : un consommateur qui appelle
  `Inspecter` ou `Read` sur la même instance ne peut pas voir un autre système que le widget, parce qu'il n'existe
  plus qu'une implémentation des filtres.
- **Rien n'a été anticipé** : le câblage (22-02), la sélection des fichiers d'état (22-03), le balayage,
  `tmp`+`Move`, la fusion par fraîcheur, le contrat d'événements et le TTL d'`ArchiveStore` (phases 23-26) restent
  intacts.
- **Aucun stub** : les deux types créés sont entièrement implémentés et consommés par du code de production.

---
*Phase: 22-un-instrument-de-mesure-qui-ne-ment-plus*
*Completed: 2026-09-12*

## Self-Check: PASSED

Les 4 fichiers sources créés et le SUMMARY existent sur disque ; les 4 commits de tâche
(`6e7d188`, `4fa2e11`, `e4ff566`, `796685d`) sont présents dans l'historique.
