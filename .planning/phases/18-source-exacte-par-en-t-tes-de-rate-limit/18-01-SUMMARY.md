---
phase: 18-source-exacte-par-en-t-tes-de-rate-limit
plan: 01
subsystem: data-normalization
tags: [globalization, invariantculture, numberstyles, epoch, unit-conversion, source-scan-guard, msbuild-assemblymetadata, xunit]

# Dependency graph
requires:
  - phase: 16-fondations-du-delta
    provides: "WindowState / UsageSnapshot et les fixtures usage-valid.json dont les epochs servent de non-régression au plancher de sanité"
  - phase: 17-jeton-toujours-vivant
    provides: "DiagnosticService à 6 paramètres (IAuthStatus? en dernier) — signature à préserver, 10 sites de construction"
provides:
  - "Services/UsageNormalization.cs : LE point unique de conversion d'unité d'usage (9 membres publics, classe pure)"
  - "Lecture texte -> nombre immunisée contre la culture de la machine (NumberStyles + InvariantCulture)"
  - "Plancher de sanité d'epoch 2020-01-01 appliqué à TOUTES les sources : le bug réel resets_at: 9 est éradiqué par construction"
  - "NormalisationUniqueTests : garde de non-retour par balayage du texte source, falsifiabilité prouvée par mutation"
  - "AssemblyMetadata(\"CheminSourcesChronos\") injecté par MSBuild dans Chronos.Tests — chemin des sources pour toute future garde textuelle"
affects: [18-02-windowstate-statut-serveur, 18-03-sonde-en-tetes, 18-04, 18-05, 18-06, 19-doctrine-du-composite]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Point unique de conversion d'unité + garde mécanique par balayage du texte source (la réflexion ne voit pas un « / 100 »)"
    - "Chemin de sources injecté par MSBuild via AssemblyAttribute plutôt que déduit de la sortie de build (mono-fichier)"
    - "Porte de validation unique sans clamp : null = inconnu, jamais 0, et un dépassement > 1 traverse intact"

key-files:
  created:
    - src/Chronos/Services/UsageNormalization.cs
    - tests/Chronos.Tests/UsageNormalizationTests.cs
    - tests/Chronos.Tests/NormalisationUniqueTests.cs
  modified:
    - src/Chronos/Services/ChronosOAuthUsageProvider.cs
    - src/Chronos/Services/ClaudeOAuthUsageProvider.cs
    - src/Chronos/Services/ClaudeUsageObjectProvider.cs
    - src/Chronos/Services/DiagnosticService.cs
    - tests/Chronos.Tests/Chronos.Tests.csproj

key-decisions:
  - "L'étape RED de la TDD a été jouée contre un SQUELETTE compilable (membres levant NotImplementedException) et non contre une classe absente : la contrainte de compilabilité à chaque commit du plan interdit un commit où dotnet test n'est même pas invocable. 25 échecs / 1 succès à RED — un vrai RED comportemental, pas un RED de compilation."
  - "InstantDepuisEpochMillisecondes existe pour que capturedAt (pont statusLine) et les 3 horodatages du rapport de diagnostic passent aussi par le point unique : sans elle la garde aurait dû exempter DiagnosticService.cs, et une garde trouée ne garde rien."
  - "Le plancher d'epoch est appliqué DANS le point unique, donc à toutes les sources d'un coup — y compris ClaudeUsageObjectProvider qui n'en avait aucun. C'est le correctif du bug réel resets_at: 9, livré comme effet de bord assumé du refactor et non comme un patch local."
  - "Aucun clamp, ni haut ni bas : WindowState.Exhausted teste >= 1.0, un dépassement réel doit rester visible ; et une valeur négative est un symptôme de source incohérente, pas un zéro."
  - "Les 3 horodatages rapatriés dans DiagnosticService passent par un pattern `is { } x` : le plancher rend désormais null, et une valeur sous le plancher affiche « inconnu » / « ? » au lieu d'un âge de 20 ans."

patterns-established:
  - "Garde de non-retour textuelle : motifs interdits + exemptions NOMINATIVES ET JUSTIFIÉES + plancher de fichiers balayés (>= 40) + message d'échec nommant fichier:ligne:raison. Aucune mise en sourdine (pas de Skip, pas de return anticipé si le chemin manque)."
  - "Falsifiabilité d'une garde prouvée par mutation temporaire du dépôt puis révocation vérifiée par git diff vide."

requirements-completed: [HDR-05]

# Metrics
duration: 14min
completed: 2026-09-12
---

# Phase 18 Plan 01: Point unique de normalisation des unités d'usage Summary

**Les trois unités d'`utilization` et les deux formats de `resets_at` convergent désormais dans une seule classe pure `UsageNormalization`, immunisée contre le piège fr-FR où `double.TryParse("0.63")` rend `false` en silence — et une garde de balayage du texte source, prouvée falsifiable par mutation, interdit d'en réintroduire une ailleurs.**

## Performance

- **Duration:** 14 min
- **Started:** 2026-09-12T00:25:08Z
- **Completed:** 2026-09-12T00:39:00Z
- **Tasks:** 3
- **Files modified:** 8 (3 créés, 5 modifiés)

## Accomplishments

- **Le piège de culture est gravé, pas documenté.** `UsageNormalizationTests` force
  `CultureInfo.CurrentCulture = new CultureInfo("fr-FR")` (restaurée en `finally`) et asserte d'abord
  `Assert.False(double.TryParse("0.63", out _))` — le piège lui-même — avant d'asserter le remède. Une
  implémentation « naturelle » de la future sonde d'en-têtes ferait désormais tomber ce test au lieu de
  rendre `null` en silence sur toute machine française.
- **Les 12 conversions dispersées dans 4 fichiers n'existent plus qu'en un point.** Et les trois classes
  de test des providers (`ChronosOAuthUsageProviderTests`, `ClaudeOAuthUsageProviderTests`,
  `ClaudeUsageObjectProviderTests`) sont vertes **sans une ligne modifiée** : c'est la preuve mécanique
  que le rapatriement est un refactor et non un changement de comportement.
- **Le bug réel `resets_at: 9` est mort par construction.** Le `usage.json` de production de cette
  machine porte `{"five_hour":{"used_percentage":10,"resets_at":9}}` — janvier 1970, servi comme un
  instant valide, produisant une géométrie fausse sur le cadran. Le plancher de sanité 2020-01-01 vit
  dans le point unique, donc s'applique à **toutes** les sources d'un coup, présentes et futures.
- **La garde de non-retour a été prouvée falsifiable, pas supposée telle.**

## Task Commits

1. **Task 1 (RED): le test qui grave le piège de culture** — `6485902` (test)
2. **Task 1 (GREEN): UsageNormalization, le point unique** — `4c462a6` (feat)
3. **Task 2: rapatriement des 12 conversions** — `c6f4ff5` (refactor)
4. **Task 3: garde de non-retour par balayage du texte source** — `9b48d8f` (test)

**Plan metadata:** commit `docs(18-01)` final.

_Aucune étape REFACTOR n'a été nécessaire sur la tâche 1 : l'implémentation GREEN est déjà la forme finale._

## Nombre de tests — avant / après

| Moment | Total | Échecs |
|---|---|---|
| Baseline à l'entrée du plan (rejouée) | **505** | 0 |
| Après tâche 1 (RED, squelette) | 531 | 25 *(RED attendu)* |
| Après tâche 1 (GREEN) | 531 | 0 |
| Après tâche 2 (rapatriement) | **531** | 0 |
| Après tâche 3 (garde) | **534** | 0 |

**+29 tests, 0 test supprimé, 0 test modifié.** (26 dans `UsageNormalizationTests` — les deux `[Theory]`
comptent pour 13 cas — et 3 dans `NormalisationUniqueTests`.)

## Résultat de la mutation de falsifiabilité de la garde

Exigé par le critère d'acceptation de la tâche 3, exécuté réellement :

1. **Mutation** — `UsageNormalization.FractionDepuisPourcentage(p)` remplacé par `p / 100.0` dans
   `src/Chronos/Services/ChronosOAuthUsageProvider.cs` (ligne 174).
2. **Constat** — `Aucune_conversion_d_unite_ne_subsiste_hors_du_point_unique` **ÉCHOUE**
   (1 échec / 2 succès sur les 3 tests de la classe), avec le message utilisable :

   ```
   ChronosOAuthUsageProvider.cs:174 — (?<!/)/\s*100(\.0)?(?![0-9]) (division par 100 : pourcentage -> fraction)
   ```

   Les deux autres tests de la classe restent verts — l'échec est bien ciblé, pas un effondrement global.
3. **Révocation** — `git checkout --` sur le fichier ; `git diff -- src/Chronos/Services/ChronosOAuthUsageProvider.cs`
   **vide** ; `grep -c "UsageNormalization.FractionDepuisPourcentage"` = 1. Le dépôt est revenu à l'identique.
4. **Re-vérification** — 3/3 verts, puis 534/534 sur la suite complète.

La garde n'est donc pas une décoration : elle voit une conversion réintroduite, elle la nomme, et elle dit
quoi faire.

## Fichiers exemptés de la garde, et pourquoi

Cette liste est le contrat que la prochaine phase ajoutant un provider doit comprendre avant d'y toucher.
HDR-05 porte sur **l'unité du quota**, pas sur toute date du dépôt — d'où trois exemptions qui ne sont
pas des dérogations mais des hors-sujet.

| Fichier exempté | Raison | Ce qu'il convertit réellement |
|---|---|---|
| `Services/UsageNormalization.cs` | **LE point unique** — c'est ici que les conversions doivent vivre | tout, par définition |
| `Services/ClaudeTokenReader.cs` | expiration de **JETON**, jamais un quota | `expiresAt` en epoch ms et en ISO (3 sites) |
| `Services/SessionMonitor.cs` | horodatage de **SESSION** Claude Code, jamais un quota | `ts` de hook en epoch ms (1 site) |
| `Services/TranscriptActivityProvider.cs` | horodatage de **MESSAGE** de transcript, jamais un quota | `timestamp` ISO de ligne JSONL (1 site) |

**Règle pour la suite :** une exemption ne s'ajoute que si la valeur convertie n'est **pas** une donnée
d'usage/quota. Si c'en est une, la déléguer au point unique. Le message d'échec de la garde énonce cette
alternative explicitement, pour que personne n'ajoute une exemption par réflexe.

Deux garde-fous rendent la garde infalsifiable par omission :

- elle **échoue** (jamais ne s'ignore) si l'attribut MSBuild manque ou si le dossier n'existe pas ;
- elle exige **≥ 40 fichiers balayés** (66 aujourd'hui : 62 dans `Services/`, 4 dans `Models/`), sans quoi
  un chemin valide pointant sur un dossier vide la rendrait silencieusement inopérante.

## Files Created/Modified

- `src/Chronos/Services/UsageNormalization.cs` *(créé, 139 l.)* — le point unique. 9 membres publics :
  `PlancherEpoch` + `FractionDepuisFraction` (porte de validation) / `FractionDepuisPourcentage` /
  `FractionDepuisTexteFraction` / `InstantDepuisEpochSecondes` / `InstantDepuisEpochMillisecondes` /
  `InstantDepuisTexteEpoch` / `InstantDepuisIso` / `PourcentagePourAffichage`. Pure : aucun `System.IO`,
  `System.Net`, `System.Text.Json` ni `System.Windows`, aucune horloge, aucun `Math.Clamp`.
- `tests/Chronos.Tests/UsageNormalizationTests.cs` *(créé, 26 tests)* — piège de culture, convergence des
  trois unités, plancher d'epoch (dont le cas réel `9`), non-régression des epochs de fixtures, refus du
  zéro fabriqué, dépassement non clampé, tolérance d'espaces, ISO 8601, bornes `long` extrêmes.
- `tests/Chronos.Tests/NormalisationUniqueTests.cs` *(créé, 3 tests)* — garde de non-retour.
- `tests/Chronos.Tests/Chronos.Tests.csproj` — injection de `AssemblyMetadata("CheminSourcesChronos")`.
- `src/Chronos/Services/ChronosOAuthUsageProvider.cs` — 2 conversions déléguées, `using System.Globalization;` retiré.
- `src/Chronos/Services/ClaudeOAuthUsageProvider.cs` — 2 conversions déléguées, `using` retiré, commentaires reformulés.
- `src/Chronos/Services/ClaudeUsageObjectProvider.cs` — 3 conversions déléguées (dont le site du bug `resets_at: 9`).
- `src/Chronos/Services/DiagnosticService.cs` — 5 conversions déléguées, `using System.Globalization;` retiré
  (`grep -c CultureInfo` = 0), signature du constructeur intacte.

## Decisions Made

1. **RED contre un squelette compilable, pas contre une classe absente.** Les contraintes critiques du
   plan imposent la compilabilité à chaque commit (`tests/Chronos.Tests` référence `Chronos`, donc toute
   erreur de compilation fait échouer l'invocation entière de `dotnet test`). Un commit RED où la classe
   n'existe pas serait un commit où la suite n'est pas invocable. Le squelette (membres levant
   `NotImplementedException`) donne un RED **comportemental** — 25 échecs / 1 succès — tout en gardant la
   suite exécutable. Seul le test du `PlancherEpoch` passait à RED, puisqu'il porte sur une constante.
2. **Le plancher d'epoch vit dans le point unique, pas dans le provider fautif.** Un patch local dans
   `ClaudeUsageObjectProvider` aurait corrigé le symptôme constaté ; le plancher dans `UsageNormalization`
   protège aussi les sources qui n'existent pas encore (la sonde d'en-têtes du plan 18-03 lit un epoch en
   TEXTE, `InstantDepuisTexteEpoch`, qui hérite du plancher gratuitement).
3. **`InstantDepuisEpochMillisecondes` n'est pas un confort mais une condition de la garde.** Sans cette
   surcharge, les 3 horodatages en millisecondes du rapport de diagnostic et du pont statusLine auraient
   forcé l'exemption de `DiagnosticService.cs` — et une garde qui exempte le plus gros fichier de la
   couche ne garde rien.
4. **Le libellé `" %"` est reproduit à l'identique** (espace ordinaire, `F0`, `InvariantCulture`) pour que
   les `Assert.Contains` existants de `DiagnosticServiceTests` (dont `"estimé"`) ne bougent pas. Les
   branches d'absence (`"absent"`, `"?"`, `"% inconnu"`) ne passent délibérément pas par le point unique :
   il ne sert que la branche valeur.

## Deviations from Plan

Aucune déviation de comportement. Trois ajustements de forme, tous internes à la lettre du plan :

**1. [Forme — non une déviation de règle] Étape RED jouée contre un squelette plutôt qu'une classe absente**
- **Found during:** Task 1
- **Motif:** arbitrage entre le flux TDD du workflow et la contrainte critique « compilabilité à chaque
  commit » du plan. Le squelette satisfait les deux.
- **Verification:** RED = 25 échecs / 1 succès sur 26 tests ; GREEN = 26/26.
- **Committed in:** `6485902`

**2. [Forme] Deux commentaires reformulés au-delà des trois nommés par le plan**
- **Found during:** Task 2
- **Issue:** le plan nomme trois commentaires portant le littéral `/100` à réécrire. Le commentaire
  d'en-tête de `ChronosOAuthUsageProvider.Read` (« utilization 0..100 ») et la XML-doc de
  `FractionDepuisTexteFraction` (qui citait `NumberStyles.Float` en prose) devaient l'être aussi : le
  premier pour la lisibilité après rapatriement, le second parce que le critère d'acceptation exige
  `grep -c "NumberStyles.Float" == 1` et que la citation en commentaire en faisait 2.
- **Fix:** reformulation sans le littéral. Aucun changement de code.
- **Verification:** tous les `grep` de la tâche 1 et de la tâche 2 passent ; suite verte.
- **Committed in:** `4c462a6`, `c6f4ff5`

**3. [Forme] Mention de `Assembly.Location` retirée de la XML-doc de la garde**
- **Found during:** Task 3
- **Issue:** le critère `grep -c "Assembly.Location\|GetExecutingAssembly" == 0` porte sur le fichier
  entier, commentaires inclus. La XML-doc citait nommément l'API interdite pour expliquer POURQUOI le
  chemin est injecté — elle faisait donc échouer son propre critère.
- **Fix:** la raison est énoncée sans le nom de l'API (« localiser un assembly par son chemin de fichier,
  VIDE en publication mono-fichier »). L'explication est intacte, la mesure est satisfaite. La mention
  nominative survit dans le commentaire du `.csproj`, que le critère ne couvre pas.
- **Verification:** `grep -c` = 0 ; 3/3 tests verts.
- **Committed in:** `9b48d8f`

---

**Total deviations:** 0 auto-fix de bug, 0 ajout de fonctionnalité critique, 0 déblocage, 0 question
architecturale. 3 ajustements de forme rédactionnelle.
**Impact on plan:** nul sur le périmètre. Aucun fichier interdit touché, aucune dépendance ajoutée,
aucune signature modifiée.

## Issues Encountered

- **Le motif de garde `\s*` pouvait franchir les fins de ligne** (en .NET, `\s` inclut `\n`), donc
  produire un faux positif sur un `/` en fin de ligne suivi d'un `100` au début de la suivante.
  Vérification faite sur les 62 fichiers réellement balayés : **aucun faux positif**. Le motif littéral
  du plan a donc été conservé tel quel plutôt qu'affaibli en `[ \t]*` — si un jour un tel cas apparaît,
  il sera signalé au lieu d'être manqué, ce qui est le bon sens d'erreur pour une garde.
- **Vérifications de sécurité du plan, exécutées avant et après :** `%APPDATA%\Chronos\oauth.dat` =
  **518 octets, mtime 1783863147** — inchangé. Aucune requête réseau émise par aucun test (aucun des 29
  tests ajoutés n'ouvre de socket ; les deux nouvelles classes ne lisent que des `.cs` du dépôt).
- **Gardes structurelles existantes re-vérifiées :** `ServicesLayerPurityTests` 2/2 vert (la nouvelle
  classe n'expose aucun type WPF), `CompositionRootTests` 4/4 vert.

## Known Stubs

Aucun. `UsageNormalization` est complet et intégralement consommé par les 4 fichiers de production
rapatriés dès ce plan — aucun membre n'est du code mort. Les 3 membres que seuls les plans suivants
appelleront (`FractionDepuisTexteFraction`, `InstantDepuisTexteEpoch`, `FractionDepuisFraction` en appel
direct) sont intégralement couverts par les tests de ce plan, y compris sous culture `fr-FR` forcée.

## User Setup Required

Aucun. Aucun service externe, aucune variable d'environnement, aucune authentification.

**Rappel hérité de la phase 17 (ne bloque pas 18-02) :** le jeton OAuth de cette machine est expiré
depuis le 2026-07-12. Tant que l'utilisateur n'a pas refait le parcours de reconnexion, ni
`/api/oauth/usage` ni la sonde d'en-têtes du plan 18-03 ne répondront **en production**. Les plans 18-02
à 18-06 restent entièrement testables (tout passe par `FakeHttpMessageHandler`).

## Next Phase Readiness

**Prêt pour 18-02** (`WindowState` : `StatutServeur?` / `EtatDepassement?`) et **18-03** (la sonde) :

- les noms de la surface publique sont figés et déjà référencés par les plans 18-03 à 18-06 ;
- `FractionDepuisTexteFraction` et `InstantDepuisTexteEpoch` — les deux fonctions dont la sonde d'en-têtes
  a besoin, et les deux seules du projet à lire du texte venu du réseau — sont livrées et prouvées sous
  `fr-FR` ;
- la garde impose structurellement au futur `RateLimitHeaderUsageProvider` de déléguer ses conversions :
  un `/ 100.0` ou un `FromUnixTimeSeconds` local le fera tomber ;
- `CompositeUsageProvider.cs` et `UsageSnapshot.cs` sont **intacts** (`git diff --name-only` vide sur les
  quatre commits) — la refonte de la doctrine du composite reste entière pour la phase 19.

**Point de vigilance pour 18-02 :** tout champ optionnel ajouté à `WindowState` doit rester `init` et
nullable, sinon les 21 sites `new WindowState` des tests cessent de compiler — et une erreur de
compilation fait échouer l'invocation entière de `dotnet test`, pas seulement la classe concernée.

---
*Phase: 18-source-exacte-par-en-t-tes-de-rate-limit*
*Completed: 2026-09-12*

## Self-Check: PASSED

- `src/Chronos/Services/UsageNormalization.cs` — FOUND (138 l., min_lines 90 ✔)
- `tests/Chronos.Tests/UsageNormalizationTests.cs` — FOUND (208 l., min_lines 120 ✔)
- `tests/Chronos.Tests/NormalisationUniqueTests.cs` — FOUND (127 l., min_lines 70 ✔)
- Commits `6485902`, `4c462a6`, `c6f4ff5`, `9b48d8f` — tous FOUND
- `dotnet test Chronos.sln -v q --nologo` — 534/534, 0 échec
- `%APPDATA%\Chronos\oauth.dat` — 518 octets, mtime 1783863147 (inchangé)
