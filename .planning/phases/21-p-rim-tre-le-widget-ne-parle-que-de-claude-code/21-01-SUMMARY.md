---
phase: 21-p-rim-tre-le-widget-ne-parle-que-de-claude-code
plan: 01
subsystem: services
tags: [sessions, transcripts, jsonl, sous-agents, isSidechain, ISessionSource, xunit, tdd]

# Dependency graph
requires:
  - phase: 13-source-app-bureau
    provides: "le contrat ISessionSource, jusqu'ici porte par la seule source app-bureau"
provides:
  - "TranscriptSessionSource rend jusqu'a douze SESSIONS (et non douze FICHIERS examines)"
  - "Pre-filtre de chemin EstSousAgent : dossier subagents/ ou nom agent-*"
  - "Enumeration materialisee DANS le try : une erreur d'acces disque rend une liste vide"
  - "TranscriptSessionSource : ISessionSource — point de substitution ouvert au plan 21-02"
  - "TranscriptSousAgentsTests : le scenario mesure du 2026-09-12 rejoue en test"
affects: [21-02 debranchement du moniteur, 21-03 suppression de la source app-bureau, 24 fusion par fraicheur]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Limite de cardinalite appliquee APRES classification, jamais a l'enumeration"
    - "Pre-filtre de chemin = economie d'I/O ; filtre de contenu = autorite. Les deux coexistent."
    - "Materialiser l'enumeration paresseuse a l'interieur du try qui pretend la proteger"

key-files:
  created:
    - tests/Chronos.Tests/TranscriptSousAgentsTests.cs
  modified:
    - src/Chronos/Services/TranscriptSessionSource.cs
    - src/Chronos/Services/ISessionSource.cs

key-decisions:
  - "Le pre-filtre de chemin ne REMPLACE pas le filtre isSidechain : il le DOUBLE. Un test verrouille l'autorite du champ de contenu sur un fichier mal range."
  - "La coupe se fait par un break APRES Classify, pas par un Take : un fichier non exploitable ne consomme plus aucun des douze emplacements."
  - "MaxSessions reste a 12 : la valeur n'a jamais ete le defaut, seul son point d'application l'etait."
  - "L'enumeration etait paresseuse DANS un try : le catch ne protegeait rien. Materialisation par ToList() a l'interieur du try (Regle 1 — bug latent corrige au passage, prevu par le plan)."
  - "ISessionSource est porte par la source transcripts AVANT la demolition du plan 21-02, pour que SessionMonitor puisse prendre sa source de base par le contrat sans etape non compilable."

patterns-established:
  - "Cardinalite apres classification : toute limite de resultat se pose sur les elements RETENUS"
  - "Autorite de contenu doublee d'une economie de chemin, jamais remplacee par elle"

requirements-completed: [SRC-03]

# Metrics
duration: 14min
completed: 2026-09-12
---

# Phase 21 Plan 01 : La limite de douze porte sur les sessions retenues (SRC-03) Summary

**La source transcripts ne s'aveugle plus pendant les vagues de sous-agents : `Take(12)` sur l'enumeration est remplace par un `break` apres classification, double d'un pre-filtre de chemin `EstSousAgent`, et `TranscriptSessionSource` porte desormais `ISessionSource`.**

## Performance

- **Duration:** ~14 min
- **Started:** 2026-09-12T14:21Z
- **Completed:** 2026-09-12T14:35Z
- **Tasks:** 2 (dont 1 en TDD : RED puis GREEN)
- **Files modified:** 3 (1 cree, 2 modifies)

## Accomplishments

- **Le defaut mesure est corrige et prouve.** Douze sous-agents chauds sous `<uuid>/subagents/` ne repoussent
  plus la vraie session hors des douze emplacements. Le test
  `Douze_sous_agents_chauds_ne_font_plus_disparaitre_la_vraie_session` echouait avant la correction
  (`Assert.Single` sur une liste vide) et passe apres.
- **Le pre-filtre de chemin economise 94 % des I/O de contenu** sur la machine cible (819 transcripts
  `agent-*.jsonl` sur 870, mesure du 2026-09-12) — sans jamais devenir l'autorite : le champ `isSidechain`
  lu ligne a ligne dans `Classify` reste le juge, et un test le verrouille sur un fichier mal range.
- **Un catch qui ne protegeait rien protege maintenant quelque chose.** L'enumeration LINQ etait paresseuse :
  une erreur d'acces disque survenait dans le `foreach`, hors du `try`, et remontait en exception.
  `ToList()` a l'interieur du `try` la ramene sous le catch.
- **`ISessionSource` cesse d'etre une future orpheline.** Aucun corps de methode touche : la signature de
  `Read` satisfaisait deja le contrat mot pour mot.
- **Suite complete : 756 tests / 0 echec / 5 s** — exactement le total annonce par le plan (752 + 4).

## Task Commits

Chaque tache committee atomiquement :

1. **Task 1 (RED) : le scenario mesure des 12 sous-agents chauds, en rouge** — `cccc0df` (test)
2. **Task 1 (GREEN) : la limite de douze porte sur les sessions RETENUES** — `88cc390` (feat)
3. **Task 2 : TranscriptSessionSource porte le contrat ISessionSource** — `d487421` (feat)

Aucune etape REFACTOR : le GREEN etait deja la forme finale (remplacement integral du corps de `Read`
specifie par le plan).

## Files Created/Modified

- `tests/Chronos.Tests/TranscriptSousAgentsTests.cs` **(cree, 110 lignes)** — 4 tests montant une racine de
  projets TEMPORAIRE (`Path.GetTempPath()`, assertion `Assert.StartsWith` qui interdit structurellement
  d'ecrire ailleurs) ; aucun ne lit le vrai `~/.claude/projects`.
- `src/Chronos/Services/TranscriptSessionSource.cs` — corps de `Read` remplace, methode `EstSousAgent`
  ajoutee, declaration `: ISessionSource`, commentaire XML de classe amende (il ne promet plus de couvrir
  l'app bureau) et enrichi d'un paragraphe SRC-03.
- `src/Chronos/Services/ISessionSource.cs` — commentaire XML corrige : plus de « app bureau UIA », mention
  explicite que la seule implementation de production est `TranscriptSessionSource` et que l'interface
  survit comme point de substitution des tests du moniteur.

## Verification

| Critere du plan | Attendu | Mesure |
|---|---|---|
| `grep -c "\.Take(MaxSessions)"` dans la source | 0 | **0** |
| `grep -c "result.Count >= MaxSessions"` | 1 | **1** |
| `grep -c "EstSousAgent"` | 2 | **2** (declaration + unique appel) |
| `grep -c "isSidechain"` | >= 1 | **2** (filtre de `Classify` + commentaire) |
| `grep -c "private const int MaxSessions = 12"` | 1 | **1** |
| `grep -c "class TranscriptSessionSource : ISessionSource"` | 1 | **1** |
| `grep -ci "app bureau UIA"` dans `ISessionSource.cs` | 0 | **0** |
| `grep -c "TranscriptSessionSource"` dans `ISessionSource.cs` | 1 | **1** |
| `grep -rn "Take(MaxSessions)" src/` | aucun resultat | **aucun resultat** |
| `git diff --name-only` contient `SessionsTests.cs` | non | **non** (jamais retouche) |
| Suite complete | 756 / 0 echec | **756 / 0 echec / 5 s** |

**Gardes nommees, verifiees vertes explicitement** (18 tests, filtre dedie) :
`ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`, `GardesDoctrineTests`,
`La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte`.

**Etape RED, effectivement jouee** : `2 echecs / 2 reussites sur 4`. Les deux echecs sont COMPORTEMENTAUX
(la solution compilait), conformement a la contrainte « aucun echec de build attendu ».
Les deux tests deja verts au rouge ne sont pas du decor : `La_limite_de_douze_porte_sur_les_sessions_retenues`
et `Le_champ_isSidechain_garde_l_autorite_sur_un_fichier_mal_range` sont des gardes de NON-REGRESSION —
ils sont precisement ce que la correction aurait pu casser.

## Decisions Made

- **Le pre-filtre est une economie, l'autorite reste au contenu.** On aurait pu supprimer le test
  `isSidechain` de `Classify` une fois le pre-filtre de chemin en place : ce serait parier que l'agencement
  `<uuid>/subagents/agent-*.jsonl` d'Anthropic ne changera jamais. Le test
  `Le_champ_isSidechain_garde_l_autorite_sur_un_fichier_mal_range` grave le contraire : un sous-agent range
  n'importe ou, au nom banal, est quand meme ecarte — et sans consommer d'emplacement.
- **`break` apres `Classify`, pas `Take` avant.** La difference n'est pas cosmetique : avec un `Take`, tout
  fichier ecarte plus tard (sous-agent, transcript sans message exploitable, fichier illisible) coute un
  emplacement. Avec le `break`, seul un resultat RETENU en coute un.
- **`MaxSessions` reste a 12.** Augmenter la limite aurait masque le symptome en payant 819 lectures de
  queue de fichier ; le defaut n'etait pas la valeur mais son point d'application.
- **`ISessionSource` porte par la source transcripts des maintenant** (vague 1, avant toute demolition) :
  c'est la seule piece dont le plan 21-02 a besoin pour que `SessionMonitor` prenne sa source de base par
  le contrat sans passer par un commit non compilable.

## Deviations from Plan

### Auto-fixed Issues

**1. [Regle 1 - Bug] Le `catch` de `Read` ne protegeait pas l'enumeration qu'il pretendait proteger**

- **Found during:** Task 1
- **Issue:** `recent` etait un `IEnumerable<FileInfo>` construit par LINQ, donc PARESSEUX. L'enumeration
  reelle du disque avait lieu dans le `foreach`, **hors** du `try`. Une `IOException` ou
  `UnauthorizedAccessException` pendant le parcours de `~/.claude/projects` remontait en exception au lieu
  de rendre une liste vide — contredisant la contrainte projet « aucune source disponible != crash ».
- **Fix:** `List<FileInfo> recents = … .ToList();` **a l'interieur** du `try`. Commentaire explicatif pose
  sur la ligne du `ToList()`.
- **Files modified:** `src/Chronos/Services/TranscriptSessionSource.cs`
- **Verification:** Suite complete verte ; le comportement « une erreur d'enumeration rend une liste vide »
  figure dans les `must_haves.truths` du plan.
- **Committed in:** `88cc390` (commit de la tache 1)

Note : cette correction etait **prescrite par le plan** (le corps de remplacement fourni la contenait, avec
son commentaire). Elle est consignee ici comme deviation parce qu'elle corrige un defaut distinct de SRC-03,
non couvert par un test dedie — l'injection d'une panne d'enumeration disque exigerait une couche
d'abstraction de systeme de fichiers qui n'existe pas dans ce depot.

---

**Total deviations:** 1 auto-corrigee (1 bug), deja prevue par le plan.
**Impact on plan:** nul. Aucun elargissement de perimetre ; aucun fichier hors des trois annonces.

## Issues Encountered

**Un invariant de securite du `21-VALIDATION.md` est FAUX — et il faut le corriger avant le plan 21-04.**

Le document annonce `oauth.dat` : « **518 octets, mtime 1783863147** ». Mesure du jour :

| Fichier | Taille | mtime (epoch) |
|---|---|---|
| `%APPDATA%\Chronos\oauth.dat` | **518** (conforme) | **1789211525** (et non 1783863147) |
| `%APPDATA%\Chronos\archived.json` | **84** (conforme) | **1783867137** |

Le mtime annonce pour `oauth.dat` (1783863147) est a 4 000 secondes de celui d'`archived.json`
(1783867137) : tout indique une **transposition** au moment de la redaction de la carte de validation.
Surtout, `oauth.dat` est **legitimement reecrit par l'overlay en cours d'execution** (pid 119412) : le
`TokenRefreshService` de la phase 17 tourne avec un tick de 60 s et fait tourner le refresh token. Un mtime
fige ne peut donc pas etre un invariant tant que l'overlay tourne.

**Invariant de remplacement, a utiliser au plan 21-04 :** `oauth.dat` fait **518 octets** et aucun test de
la phase ne touche a l'authentification (verifiable par `grep`). La taille est l'invariant ; le mtime ne
l'est pas. Cette execution n'a **jamais** appele de refresh ni lu le refresh token.

**Aucun autre ecart.** `%APPDATA%\Chronos\sessions\` compte toujours **66** entrees ; `archived.json` fait
toujours **84** octets (mtime inchange) ; aucun `PackageReference` ajoute (`git diff --stat -- '*.csproj'`
vide) ; l'overlay `Chronos-v3.0.2.exe` **pid 119412** n'a ete ni lance ni tue et tourne toujours.

## Known Stubs

Aucun. Les deux fichiers de production modifies sont entierement cables ; le fichier de test cree est
integralement executif (4 `[Fact]`, aucun `Skip`).

## Point de depart du bilan nominatif (plans 02 a 04)

Consigne comme le demande la section `<output>` du plan :

| Repere | Valeur |
|---|---|
| Baseline avant phase 21 | **752** |
| **Total apres le plan 21-01** | **756** (0 echec, 5 s) |
| Ajoutes par ce plan | **+4**, tous dans `TranscriptSousAgentsTests.cs` |
| Supprimes par ce plan | **0** |
| Cible de fin de phase (`21-VALIDATION.md`) | **719** |

Les 4 noms, pour le bilan nominatif du plan 21-04 :
`Douze_sous_agents_chauds_ne_font_plus_disparaitre_la_vraie_session`,
`La_limite_de_douze_porte_sur_les_sessions_retenues`,
`Un_fichier_agent_hors_dossier_subagents_est_ecarte_par_son_nom`,
`Le_champ_isSidechain_garde_l_autorite_sur_un_fichier_mal_range`.

## User Setup Required

Aucune. Rien d'externe a configurer.

**A VERIFIER PAR L'UTILISATEUR (differe, non bloquant)** — l'effet de SRC-03 n'est observable in vivo
qu'apres un redemarrage **volontaire** de l'overlay, que cette phase s'interdit de provoquer : pendant une
vague de sous-agents paralleles, la session Claude Code reelle doit rester visible dans le widget.

## Next Phase Readiness

- **Le plan 21-02 a sa piece.** `TranscriptSessionSource : ISessionSource` compile et la suite est verte :
  `SessionMonitor` peut prendre sa source de base par le contrat, et `MutableSource` de
  `TreatedSessionsTests` survivra sans reecriture.
- **Rien du perimetre des plans 02/03/04 n'a ete anticipe** : `SessionSnapshot` garde ses 7 champs,
  `DesktopUiaSessionSource` et ses sept compagnons sont intacts, `archived.json` n'est pas purge,
  `SessionMonitor` n'est pas touche.
- **Point d'attention pour le plan 21-04** : remplacer l'invariant « mtime 1783863147 » par « 518 octets »
  dans la verification de fin de phase (voir « Issues Encountered »).

## Self-Check: PASSED

- Fichiers annonces presents : `tests/Chronos.Tests/TranscriptSousAgentsTests.cs`,
  `src/Chronos/Services/TranscriptSessionSource.cs`, `src/Chronos/Services/ISessionSource.cs`,
  `21-01-SUMMARY.md`.
- Commits annonces presents : `cccc0df`, `88cc390`, `d487421`.
- Suite complete rejouee apres le dernier commit : **756 / 0 echec**.

---
*Phase: 21-p-rim-tre-le-widget-ne-parle-que-de-claude-code*
*Completed: 2026-09-12*
