---
phase: 15-idempotence-des-int-grations
plan: 01
subsystem: infra
tags: [system-text-json, jsonnode, settings-json, claude-code, idempotence, xunit]

# Dependency graph
requires: []
provides:
  - "ClaudeSettingsJson : prédicat d'identité Chronos par marqueur d'argument + nom d'exe (jamais le chemin)"
  - "ClaudeSettingsJson.PointsToExe : test de FRAÎCHEUR séparé du test d'APPARTENANCE"
  - "ClaudeSettingsJson.ParseOrNull : analyse tolérante qui ABANDONNE (null) au lieu d'effacer le fichier"
  - "ClaudeSettingsJson.Serialize : sérialisation fidèle (UnsafeRelaxedJsonEscaping, accents littéraux)"
  - "ClaudeSettingsJson.CommandOf / IsChronosGroup : lecture non levante des groupes et handlers tiers"
  - "Fixture figée claude-settings-pollue.json : état réel à 25 groupes Chronos + 3 groupes GSD"
affects: [15-02 SessionHookInstaller, 15-02 StatusLineInstaller, 15-03 ClaudeSettingsReconciler]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Prédicat d'identité en deux volets : marqueur d'argument + nom de fichier Chronos*.exe"
    - "Séparation appartenance (IsChronosCommand) / fraîcheur (PointsToExe)"
    - "null = NE RIEN ÉCRIRE : l'échec d'analyse n'autorise jamais une réécriture"
    - "catch large (Exception) : ArgumentException des clés dupliquées n'est pas une JsonException"

key-files:
  created:
    - src/Chronos/Services/ClaudeSettingsJson.cs
    - tests/Chronos.Tests/ClaudeSettingsJsonTests.cs
    - tests/Chronos.Tests/TestData/claude-settings-pollue.json
  modified: []

key-decisions:
  - "Le repérage d'une entrée Chronos se fait sur le MARQUEUR d'argument + le NOM de fichier Chronos*.exe, jamais sur le CHEMIN — le chemin est la variable qui a produit 25 groupes de hooks au lieu de 5"
  - "ParseOrNull renvoie null (et non un objet vide) sur un fichier inexploitable : le repli « objet vide » des installateurs actuels écrase intégralement le settings.json de l'utilisateur"
  - "La matérialisation forcée (_ = o.Count) + un catch large sont indispensables : sur clés dupliquées, JsonNode.Parse réussit et c'est l'accès qui lève une ArgumentException, pas une JsonException"
  - "UnsafeRelaxedJsonEscaping obligatoire : l'encodeur par défaut mutile en \\uXXXX toutes les valeurs non-ASCII du fichier, y compris celles des autres outils"
  - "Le socle est purement en mémoire (aucune E/S, aucun type WPF) : ses tests ne peuvent structurellement pas atteindre le vrai ~/.claude/settings.json"

patterns-established:
  - "Socle neutre partagé : les trois plans de la phase codent contre une seule surface publique, pas trois implémentations divergentes"
  - "Fixture figée d'état runtime réel comme preuve exécutable d'une migration de données"

requirements-completed: [PUR-01, PUR-02, PUR-03]

# Metrics
duration: 12min
completed: 2026-09-09
---

# Phase 15 Plan 01: Socle d'idempotence `ClaudeSettingsJson` Summary

**Socle neutre `ClaudeSettingsJson` : identité Chronos par marqueur + nom d'exe (jamais le chemin, cause racine des 25 hooks cumulés), analyse tolérante qui abandonne au lieu d'écraser le settings.json, et sérialisation `UnsafeRelaxedJsonEscaping` — prouvé par 38 tests et une fixture figée de l'état réel pollué.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-09-09T10:20:00Z
- **Completed:** 2026-09-09T10:33:00Z
- **Tasks:** 3/3
- **Files modified:** 3 créés, 0 modifiés

## Accomplishments

- **Cause racine neutralisée à la source.** `IsChronosCommand` identifie une entrée par le marqueur d'argument (`--hook` / `--statusline`) **et** le nom de fichier `Chronos*.exe`, jamais par le chemin. Les 5 chemins d'exe historiques (v2.5, v2.5.1, v2.6, v2.8.1, build Debug) sont tous reconnus ; les 3 hooks GSD (`node ".../gsd-*.js"`) ne le sont pas, ni un exe tiers portant le marqueur.
- **Le mode de défaillance destructif est supprimé.** `ParseOrNull` renvoie `null` — « ne rien écrire » — sur les 4 formes de contenu inexploitable (clés dupliquées, racine tableau, racine scalaire, JSON invalide), et tolère commentaires et virgule traînante. Le repli « repartir d'un objet vide », qui écrasait tout le fichier de l'utilisateur, n'existe plus dans ce socle.
- **Appartenance et fraîcheur sont désormais deux questions distinctes.** `IsChronosCommand` (est-ce à Chronos ?) et `PointsToExe` (est-ce CE Chronos ?) : c'est ce qui permettra à `IsEnabled()` de cesser de répondre « oui » pour un exe périmé.
- **Fidélité de réécriture garantie.** `Serialize` conserve les accents (`Téléchargements` littéral, jamais `\u00E9`), les caractères `& < > +`, l'ordre des clés et le texte brut des nombres (`5817635413` inchangé).
- **L'état réel pollué est figé en fixture exécutable** : 28 groupes dont 25 Chronos sur 7 clés d'événement, 3 groupes GSD complets (`matcher`, `timeout`, clé inconnue `if`), `statusLine` périmée avec son `padding` officiel, et la clé racine tierce `agentPushNotifEnabled`.
- **Suite complète : 366 tests, 0 échec** (baseline 328 + 38 nouveaux), gardes `ServicesLayerPurityTests` et `CompositionRootTests` vertes.

## Task Commits

1. **Task 1: Créer ClaudeSettingsJson — prédicat d'identité, analyse tolérante, sérialisation fidèle** — `7827b81` (feat)
2. **Task 2: Prouver le socle par tests unitaires (prédicat, tolérance, fidélité)** — `9b385dc` (test)
3. **Task 3: Figer la fixture de l'état réel pollué (25 groupes Chronos + 3 groupes GSD)** — `59998f1` (test)

## Files Created/Modified

- `src/Chronos/Services/ClaudeSettingsJson.cs` (150 lignes) — classe statique neutre : `HookMarker`, `StatusLineMarker`, `ExtractExecutable`, `IsChronosCommand`, `PointsToExe`, `CommandOf`, `IsChronosGroup`, `ParseOrNull`, `Serialize`. Aucune E/S fichier, aucun type WPF. XML-doc française expliquant les trois « pourquoi » structurants (marqueur+nom vs chemin, null vs objet vide, perte assumée des commentaires).
- `tests/Chronos.Tests/ClaudeSettingsJsonTests.cs` (234 lignes, 38 cas) — preuves du socle, entièrement en mémoire, sans le moindre accès disque.
- `tests/Chronos.Tests/TestData/claude-settings-pollue.json` — fixture figée de `~/.claude/settings.json` tel que constaté le 2026-09-09.

## Decisions Made

- **Repérage par marqueur + nom de fichier, jamais par chemin.** Le marqueur seul serait imprudent (rien n'empêche un outil tiers d'adopter `--hook`) ; le nom `Chronos*.exe` borne le rayon d'action sans réintroduire la dépendance au chemin qui a causé le cumul. Résidu accepté et documenté : un binaire tiers nommé `Chronos*.exe` utilisant `--hook` serait capturé.
- **`ParseOrNull` renvoie `null`, jamais un objet vide.** C'est le point le plus important de la phase : un échec d'analyse doit produire « je n'écris rien ».
- **`catch` large plutôt que `catch (JsonException)`.** Sur clés dupliquées, `JsonNode.Parse` réussit et c'est le premier accès qui lève une `ArgumentException` — d'où `_ = o.Count;` pour forcer la matérialisation à l'intérieur du `try`.
- **`CommandOf` via `TryGetValue` et jamais l'accès typé direct.** Le schéma officiel autorise désormais des handlers `http` / `mcp_tool` / `prompt` / `agent` sans champ `command`, et un fichier mal formé peut en porter un numérique.
- **Fixture générée par script déterministe** (script jetable dans le scratchpad, non versionné) plutôt qu'à la main : garantit exactement 25 groupes Chronos et l'ordre des clés.

## Deviations from Plan

### Ajustements

**1. [Rule 1 - Bug] Avertissement xUnit2013 introduit par le nouveau fichier de tests**
- **Found during:** Task 2 (tests du socle)
- **Issue:** `Assert.Equal(0, blanc!.Count)` déclenche `xUnit2013: Do not use Assert.Equal() to check for collection size`, ajoutant un avertissement au build de la solution.
- **Fix:** Remplacé par `Assert.Empty(blanc!)`.
- **Files modified:** `tests/Chronos.Tests/ClaudeSettingsJsonTests.cs`
- **Verification:** `dotnet build Chronos.sln` — plus aucun avertissement provenant du fichier ; seuls les 2 avertissements préexistants de `DesktopUiaSessionSourceTests` subsistent (hors périmètre, non touchés).
- **Committed in:** `9b385dc` (commit de la Task 2)

**2. [Rule 3 - Blocking] XML-doc reformulée pour ne pas contredire un critère d'acceptation**
- **Found during:** Task 1 (socle)
- **Issue:** L'action demandait une XML-doc citant littéralement le repli destructif `new JsonObject()`, alors qu'un critère d'acceptation exige exactement **une** occurrence de cette chaîne dans le fichier (le cas json null/vide). Les deux consignes s'excluaient.
- **Fix:** La XML-doc décrit le repli par sa périphrase (« repartir d'un objet JSON vierge ») ; l'intention pédagogique est intégralement conservée et le critère d'acceptation passe.
- **Files modified:** `src/Chronos/Services/ClaudeSettingsJson.cs`
- **Verification:** `grep -c "new JsonObject()"` → 1.
- **Committed in:** `7827b81` (commit de la Task 1)

**3. [Rule 3 - Blocking] Échappement de `statusLine.command` dans la fixture**
- **Found during:** Task 3 (fixture)
- **Issue:** L'action rendait la commande sous la forme `"\"C:\\\\Users\\\\...\""`, ce qui, dans un fichier JSON, décoderait en un chemin à **doubles** backslashes — état impossible dans le vrai fichier.
- **Fix:** La valeur porte des backslashes simples (donc `\\` dans le texte JSON), fidèle au `~/.claude/settings.json` réel. Sans impact sur le comportement : `PointsToExe` normalise les backslashes, ce qui est explicitement testé.
- **Files modified:** `tests/Chronos.Tests/TestData/claude-settings-pollue.json`
- **Verification:** commande de vérification Python du plan → `OK 25` ; `statusLine.padding == 2`.
- **Committed in:** `59998f1` (commit de la Task 3)

**4. [Ordre TDD] Tâches 1 et 2 marquées `tdd="true"` mais exécutées implémentation-puis-tests**
- **Found during:** Task 1
- **Issue:** Le plan sépare l'implémentation (Task 1, vérifiée par un `dotnet build`) et les tests (Task 2), et le `read_first` de la Task 2 exige le fichier produit par la Task 1 — la boucle RED/GREEN par tâche était structurellement impossible.
- **Fix:** Suivi de l'ordre des tâches du plan. Le contrat de comportement était intégralement spécifié avant écriture (section `<behavior>` de la Task 1), et chaque ligne de ce contrat a une assertion dédiée en Task 2.
- **Impact:** aucun sur le résultat — 38 tests couvrent l'intégralité du contrat.

---

**Total deviations:** 3 auto-corrigées (1 bug d'avertissement, 2 blocages de cohérence de consignes) + 1 note de méthode.
**Impact on plan:** Aucune dérive de périmètre. Les trois corrections servent la conformité aux critères d'acceptation et à la réalité du fichier cible.

## Issues Encountered

- **Création de fichier par heredoc Bash en échec** sur ce shell (`unexpected EOF while looking for matching`), sur un contenu C# contenant apostrophes françaises et chevrons. Contourné par l'outil `Write`, qui garantit aussi l'encodage UTF-8 des accents. Sans conséquence.

## Conformité CLAUDE.md

- Aucune dépendance NuGet ajoutée (`System.Text.Json` / `System.Text.Encodings.Web` sont intégrés à net8.0).
- Type neutre dans `Services/`, aucun type WPF : `ServicesLayerPurityTests` vert.
- XML-doc, commentaires et noms de tests en français ; identifiants de types et méthodes en anglais, conformément aux conventions du dépôt.
- Aucun chemin hors profil utilisateur, aucun droit admin — et surtout **aucune E/S du tout** dans ce plan.

## User Setup Required

None - aucune configuration externe requise.

## Next Phase Readiness

**Prêt pour le plan 15-02** (`SessionHookInstaller` retirer-puis-ajouter, `StatusLineInstaller` mutation ciblée) : la surface publique du contrat est figée et testée, les deux installateurs peuvent coder contre elle sans dupliquer de prédicat.

**Prêt pour le plan 15-03** (`ClaudeSettingsReconciler`) : la fixture `claude-settings-pollue.json` fournit la preuve d'entrée de la purge (25 → 5 groupes Chronos, 3 groupes GSD intacts avec `matcher`/`timeout`/`if`, `agentPushNotifEnabled` et `statusLine.padding` préservés).

**Point d'attention pour la suite :** le socle est purement en mémoire. Toute la discipline d'E/S (sauvegarde horodatée avant écriture, écriture atomique, abandon sur `ParseOrNull == null`, non-écriture quand rien ne change) reste à porter par les plans 02 et 03 — le socle la rend possible, il ne la garantit pas.

---
*Phase: 15-idempotence-des-int-grations*
*Completed: 2026-09-09*

## Self-Check: PASSED

- 3 fichiers annonces presents sur le disque (socle, tests, fixture)
- 3 commits de tache verifies dans git (7827b81, 9b385dc, 59998f1)
- Suite complete : 366 tests / 0 echec (baseline 328 + 38 nouveaux)
- Aucun stub : le socle est complet et entierement cable par ses tests
