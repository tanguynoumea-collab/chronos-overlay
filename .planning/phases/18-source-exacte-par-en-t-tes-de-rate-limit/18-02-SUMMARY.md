---
phase: 18-source-exacte-par-en-t-tes-de-rate-limit
plan: 02
subsystem: data-contracts
tags: [windowstate, record-identity, enum-ouvert, canal-lateral, httpheaders, tryaddwithoutvalidation, fixtures-en-tetes, xunit]

# Dependency graph
requires:
  - phase: 16-fondations-du-delta
    provides: "WindowState / UsageSnapshot / CompositeUsageProvider — dont la mécanique de reconstruction du snapshot dicte TOUTE la conception de ce plan"
  - phase: 17-jeton-toujours-vivant
    provides: "IAuthStatus + EtatAuthentification — le motif de canal latéral copié à l'identique, et ResultatRafraichissement dont la règle « aucune chaîne » est reprise"
  - phase: 18-source-exacte-par-en-t-tes-de-rate-limit
    provides: "18-01 — UsageNormalization : point unique de conversion, consommé par les tests de ce plan (aucune conversion redupliquée)"
provides:
  - "Models/StatutServeur.cs : enum OUVERT (4 membres dont NonReconnu) + StatutServeurTexte.DepuisEnTete, lecture tolérante qui REFUSE de deviner"
  - "Models/EtatDepassement.cs : le dépassement comme fait de COMPTE, tous champs optionnels, EstRenseigne"
  - "Models/WindowState.cs : 2 champs init NULLABLE (StatutServeur? / Depassement?) posés au SEUL endroit qui survive au composite"
  - "Services/ResultatSonde.cs : 11 issues de sonde, ZÉRO membre porteur de chaîne par construction"
  - "Services/IEtatServeur.cs : canal latéral neutre du dépassement et de l'observabilité de la sonde (contrat sans implémentation, précédent IAuthStatus)"
  - "tests/Fakes/FakeHttpMessageHandler : AvecEnTetes / SequenceAvecEnTetes — une réponse porteuse d'en-têtes sur N'IMPORTE QUEL code de statut"
  - "tests/EnTetesDeReference.cs : point UNIQUE des 7 formes réelles de réponse + NominalCasseMelangee, chaque nom d'en-tête portant son niveau de confiance"
  - "6 tests de caractérisation prouvant mécaniquement ce que Best() emporte et ce qu'il JETTE"
affects: [18-03-sonde-en-tetes, 18-04-statut-et-depassement, 18-05-cablage-di, 18-06-ui, 19-doctrine-du-composite, 20-rendu-visible]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Enum de vocabulaire OUVERT : un membre NonReconnu explicite, plus la distinction null (absent) / NonReconnu (présent mais illisible) — deux inconnus, deux valeurs"
    - "Champ optionnel porté par l'instance que le composite transmet PAR RÉFÉRENCE (WindowState) plutôt que par le record qu'il reconstruit (UsageSnapshot)"
    - "Doublement d'une information de compte par un canal latéral, avec un test de caractérisation qui prouve la nécessité du doublement au lieu de l'affirmer"
    - "Faux de transport capable de poser des en-têtes non standard sur un code d'erreur (TryAddWithoutValidation, jamais Add)"
    - "Jeux d'en-têtes de référence centralisés, chaque nom annoté de son NIVEAU DE CONFIANCE (observé / lu dans le code d'origine / annoncé non confirmé)"

key-files:
  created:
    - src/Chronos/Models/StatutServeur.cs
    - src/Chronos/Models/EtatDepassement.cs
    - src/Chronos/Services/ResultatSonde.cs
    - src/Chronos/Services/IEtatServeur.cs
    - tests/Chronos.Tests/StatutServeurTests.cs
    - tests/Chronos.Tests/EnTetesDeReference.cs
  modified:
    - src/Chronos/Models/WindowState.cs
    - tests/Chronos.Tests/WindowStateTests.cs
    - tests/Chronos.Tests/CompositeUsageProviderTests.cs
    - tests/Chronos.Tests/Fakes/FakeHttpMessageHandler.cs

key-decisions:
  - "Le statut serveur et le dépassement sont posés sur WindowState et non sur UsageSnapshot, parce que CompositeUsageProvider.GetAsync reconstruit le snapshot par « new UsageSnapshot { … } » et non par « with ». Ce n'est pas une préférence de conception : un champ de snapshot serait détruit au premier composite traversé, et le fichier est interdit d'édition jusqu'à la phase 19."
  - "Un quatrième membre NonReconnu, et la distinction null / NonReconnu. Le vocabulaire allowed / allowed_warning / rejected est communautaire, pas officiel : ranger l'inconnu dans « autorisé » serait le mensonge inverse de celui que v1.5 corrige. Et « le serveur n'a rien dit » n'est pas « le serveur a dit quelque chose d'illisible » — ce second fait est le seul signal honnête d'un renommage côté serveur."
  - "La nécessité du canal latéral IEtatServeur est PROUVÉE, pas postulée : Le_depassement_de_la_fenetre_ecartee_est_JETE_par_le_composite montre que Best() choisit une INSTANCE et non des champs, donc que le dépassement d'une fenêtre perdante disparaît sans bruit."
  - "HDR-03 et HDR-04 ne sont PAS cochés comme complets : ce plan livre leurs contrats, la sonde qui les remplit est 18-03/18-04 et la remontée au cadran est 18-06. Les cocher maintenant affirmerait que l'utilisateur voit le statut, ce qui est faux."
  - "L'étape RED a été jouée contre un squelette compilable (NotImplementedException / EstRenseigne => false), précédent établi au plan 18-01 : la contrainte de compilabilité à chaque commit interdit un commit où dotnet test n'est même pas invocable."
  - "La tâche 2 n'a pas d'étape RED possible et c'est assumé : ses 6 tests caractérisent le comportement du code EXISTANT. Ils devaient passer du premier coup — s'ils avaient échoué, c'est la conception du plan qui aurait été fausse, pas le composite."

patterns-established:
  - "Extension de fichier de test par ADDITION PURE, mesurée : `git diff -- fichier | grep -c '^-[^-]'` == 0. Aucune ligne existante supprimée ni réécrite sur les 3 fichiers étendus."
  - "Test de caractérisation commenté par son ENJEU : le test qui démontre une perte d'information porte en commentaire la conception qu'il justifie, pour qu'une relecture future ne le prenne pas pour un test de régression banal."
  - "Constante de nom d'en-tête annotée de son niveau de confiance en XML-doc — la documentation du doute au même endroit que la valeur."

requirements-completed: []

# Metrics
duration: 18min
completed: 2026-09-12
---

# Phase 18 Plan 02: Contrats du statut serveur, du dépassement et Wave 0 de transport Summary

**Le statut serveur et le dépassement sont désormais posés sur `WindowState` — la seule chose que `CompositeUsageProvider` transmette par référence — avec un vocabulaire qui refuse de deviner (`NonReconnu`), un canal latéral dont un test prouve *mécaniquement* la nécessité en montrant que `Best()` jette le dépassement d'une fenêtre perdante, et un faux de transport sans lequel HDR-02 n'était tout simplement pas testable : un 429 ne pouvait pas porter d'en-têtes.**

## Performance

- **Duration:** 18 min
- **Started:** 2026-09-12T00:41:00Z
- **Completed:** 2026-09-12T00:59:30Z
- **Tasks:** 3
- **Files modified:** 10 (6 créés, 4 modifiés)

## Accomplishments

- **La conception n'est pas argumentée, elle est mesurée.** Six tests de caractérisation sur le composite
  INTOUCHÉ établissent, dans le code exécutable, les trois faits dont dépend toute la phase 18 : un champ de
  `WindowState` traverse deux composites imbriqués par référence ; un champ porté par une fenêtre écartée est
  réellement jeté ; et le dépassement de la forme « overage seule » disparaît donc sans bruit — d'où
  `IEtatServeur`. Le plan affirmait tout cela par lecture de code ; c'est désormais une garde.
- **Le vocabulaire refuse de deviner, et le test le dit.** Neuf valeurs hors de l'ensemble connu (`active`,
  `blocked`, `allow`, `allowedwarning`, `0.5`…) rendent `NonReconnu` et jamais `Autorise` — avec une
  assertion explicite `Assert.NotEqual(StatutServeur.Autorise, …)` plutôt qu'une déduction. Et les deux
  inconnus restent distincts : `null` pour « rien rapporté », `NonReconnu` pour « rapporté mais illisible ».
- **HDR-02 est devenu testable.** `FakeHttpMessageHandler` ne pouvait poser **aucun** en-tête (ses fabriques
  `Json` / `Throws` n'en posent pas) : le cas décisif de la phase — un 429 qui livre quand même les
  chiffres — était hors d'atteinte des tests. Les deux fabriques ajoutées le rendent fabricable en une
  ligne, et les trois faits .NET sur lesquels repose la phase sont maintenant assertés et non supposés.
- **Les 21 sites `new WindowState { … }` des tests compilent sans une retouche.** `dotnet build` rend
  **0 erreur, 0 avertissement** : les deux champs ajoutés sont `init`, nullables, et sans `required`.
- **Aucun fichier interdit touché.** `git diff HEAD~4 -- CompositeUsageProvider.cs UsageSnapshot.cs` est
  vide, `SchemaVersion = 1` inchangé, signature de `DiagnosticService` intacte, coffre OAuth intact.

## Task Commits

1. **Task 1 (RED): le vocabulaire qui refuse de deviner** — `dce68ee` (test) — 32 échecs / 15 succès sur 47
2. **Task 1 (GREEN): StatutServeur, EtatDepassement, ResultatSonde** — `bedb5a5` (feat) — 47/47
3. **Task 2: IEtatServeur et la preuve que le statut survit à `Best()`** — `bbcbc8d` (feat)
4. **Task 3: Wave 0 de transport — un 429 porteur d'en-têtes et les 8 jeux** — `a6dd483` (test)

**Plan metadata:** commit `docs(18-02)` final.

_Aucune étape REFACTOR n'a été nécessaire sur la tâche 1 : l'implémentation GREEN est déjà la forme finale._

## Nombre de tests — avant / après

| Moment | Total | Échecs |
|---|---|---|
| Baseline à l'entrée du plan (rejouée) | **534** | 0 |
| Après tâche 1, étape RED (squelette) | — | **32 échecs / 15 succès sur les 47 du filtre** *(RED attendu)* |
| Après tâche 1, étape GREEN | **571** | 0 |
| Après tâche 2 | **577** | 0 |
| Après tâche 3 | **582** | 0 |

**+48 tests, 0 test supprimé, 0 test modifié.**

| Origine | Tests |
|---|---|
| `StatutServeurTests` (nouveau) | **36** — 12 cas de vocabulaire connu, 9 de statut inconnu, 5 d'en-tête absent, 10 faits |
| `WindowStateTests` (étendu) | **+1** test, et 2 `Assert.Null` ajoutés DANS le test `Unavailable_…` existant |
| `CompositeUsageProviderTests` (étendu) | **+6** (8 → 14 dans la classe) |
| `EnTetesDeReferenceTests` (nouveau) | **5** |

## Confirmation des fichiers interdits

```
git diff HEAD~4 -- src/Chronos/Services/CompositeUsageProvider.cs src/Chronos/Models/UsageSnapshot.cs
→ (vide)
```

Aucun des deux fichiers n'apparaît dans `git diff --name-only HEAD~4 HEAD`, qui liste exactement les
**10** fichiers annoncés par le `files_modified` du plan — ni un de plus, ni un de moins. La refonte de la
doctrine du composite reste entière pour la phase 19.

## Noms d'en-tête retenus et NIVEAU DE CONFIANCE

**Les plans 18-03 et 18-04 dépendent de ce tableau.** Il est reproduit en XML-doc sur chaque constante de
`tests/Chronos.Tests/EnTetesDeReference.cs`, constante par constante — la documentation du doute au même
endroit que la valeur.

| Constante | Nom exact | Unité / valeurs | Confiance |
|---|---|---|---|
| `H5hUtil` | `anthropic-ratelimit-unified-5h-utilization` | fraction 0..1, TEXTE à point décimal | **Observé** dans un dump indépendant ET lu par le code d'origine |
| `H5hReset` | `anthropic-ratelimit-unified-5h-reset` | epoch **secondes**, texte | **Observé** + lu par le code d'origine |
| `H5hStatut` | `anthropic-ratelimit-unified-5h-status` | `allowed` / `allowed_warning` / `rejected` | **Observé** (valeur vue : `allowed`) + lu par le code d'origine |
| `H7dUtil` | `anthropic-ratelimit-unified-7d-utilization` | fraction 0..1 | **Observé** + lu par le code d'origine |
| `H7dReset` | `anthropic-ratelimit-unified-7d-reset` | epoch secondes | **Observé** + lu par le code d'origine |
| `H7dStatut` | `anthropic-ratelimit-unified-7d-status` | statut | **NON confirmé** — aucune observation, absent du code d'origine. Lecture OPTIONNELLE : son absence n'est pas une anomalie |
| `HOverUtil` | `anthropic-ratelimit-unified-overage-utilization` | fraction 0..1 | **Lu par le code d'origine**, branche ALTERNATIVE liée au type de compte. Jamais observé sur cette machine |
| `HOverReset` | `anthropic-ratelimit-unified-overage-reset` | epoch secondes | **Lu par le code d'origine** (branche alternative) |
| `HOverStatut` | `anthropic-ratelimit-unified-overage-status` | statut | **Annoncé par CONTEXT.md, NON confirmé** — lu quand même, par précaution, jamais exigé |
| `HStatutGlobal` | `anthropic-ratelimit-unified-status` | statut global, **SANS** segment de fenêtre | **Lu par le code d'origine** — c'est le nom réel de la branche de dépassement. **Correction d'un écart de CONTEXT.md** |
| `HClaim` | `anthropic-ratelimit-unified-representative-claim` | ex. `five_hour` | **Observé** dans le dump. Purement informatif, jamais nécessaire à un calcul |

**Règle pour 18-03/18-04 :** pour le statut, lire **les trois** noms par ordre de préférence
(`-5h-status` / `-7d-status` pour la fenêtre concernée, puis `-status` en repli global) et n'inventer aucun
statut en l'absence des trois. Et ne **jamais** traiter l'absence d'un nom « non confirmé » comme une panne.

## Les huit jeux d'en-têtes de référence

| Jeu | Contenu | Ce qu'il prouve |
|---|---|---|
| `Nominal` | 5 h `0.01` / `1783180800` / `allowed`, 7 j `0.63` / `1783713600`, claim `five_hour` | HDR-01 : deux fenêtres exactes, fractions 0..1 à point décimal, resets en epoch secondes |
| `Avertissement` | `Nominal` + `5h-status: allowed_warning` | HDR-03 : le serveur avertit AVANT de refuser, et la nuance doit survivre |
| `Refus` | `Nominal` + `rejected` + `5h-utilization: 1.0` | Le cas 429 : refusé ET épuisé, chiffres intacts |
| `StatutInconnu` | `Nominal` + `5h-status: active` | `NonReconnu`, jamais `Autorise` |
| `DepassementSeul` | **uniquement** overage `0.34` / `1783800000` + statut GLOBAL | HDR-04 dans sa forme alternative : les deux fenêtres sont légitimement `Unavailable` |
| `Absents` | dictionnaire **vide** | Cas de PREMIÈRE CLASSE : `UsageSnapshot.Empty`, jamais « 0 % » |
| `Illisibles` | `pas-un-nombre`, `9`, `0,63`, statut `""` | Tolérance par en-tête ; l'epoch `9` est celui, RÉEL, du `usage.json` de cette machine |
| `NominalCasseMelangee` | `Nominal` en casse mélangée | La recherche insensible à la casse est **vérifiée**, donc aucune normalisation de casse à écrire dans la sonde |

`Illisibles` est en outre branché sur le point unique du plan 18-01 : le test
`Le_jeu_illisible_est_reellement_illisible_pour_le_point_unique_de_normalisation` passe ses quatre valeurs à
`UsageNormalization` et à `StatutServeurTexte`, et asserte **quatre `null`** — aucun zéro fabriqué, aucun
janvier 1970, aucune fraction multipliée par l'effet d'une virgule décimale.

## Files Created/Modified

- `src/Chronos/Models/StatutServeur.cs` *(créé, 49 l.)* — enum à 4 membres (ordre figé, gardé par un test
  sur `Enum.GetNames`) + `StatutServeurTexte.DepuisEnTete` : `Trim` puis `OrdinalIgnoreCase` sur les trois
  valeurs connues, `NonReconnu` pour tout le reste, `null` sur vide/blanc.
- `src/Chronos/Models/EtatDepassement.cs` *(créé, 31 l.)* — record à 3 champs optionnels + `EstRenseigne`.
  La XML-doc explique pourquoi ce n'est **pas** une troisième `WindowKind` : `UsageSnapshot` n'a que deux
  emplacements `required` et aucun champ de snapshot ne survit au composite.
- `src/Chronos/Services/ResultatSonde.cs` *(créé, 54 l.)* — 11 issues, chacune documentée.
  `grep -cE "^\s*(public|private|internal).*string"` = **0** : un corps d'erreur ne peut pas fuir.
- `src/Chronos/Services/IEtatServeur.cs` *(créé, 47 l.)* — 4 membres (`Depassement`, `DernierResultat`,
  `NomsEnTetesRecus`, `DepassementChange`). Aucun type WPF. Contrat sans implémentation, par dessein.
- `src/Chronos/Models/WindowState.cs` *(+15 l.)* — `StatutServeur?` et `Depassement?` en `init` nullable.
  `grep -c required` reste à **2** (`Kind`, `Reliability`). Le motif légal « Color Color » est assumé :
  la propriété porte le même nom que son type, et le TYPE `StatutServeur` n'est référencé nulle part
  ailleurs dans ce fichier.
- `tests/Chronos.Tests/StatutServeurTests.cs` *(créé, 159 l., 36 tests)*.
- `tests/Chronos.Tests/EnTetesDeReference.cs` *(créé, 246 l.)* — 11 constantes annotées, 8 jeux, 5 tests.
- `tests/Chronos.Tests/WindowStateTests.cs` *(+34 l., 0 supprimée)*.
- `tests/Chronos.Tests/CompositeUsageProviderTests.cs` *(+145 l., 0 supprimée)* — helper `WinStatut` ajouté
  **à côté** de `Win`, qui n'a pas bougé.
- `tests/Chronos.Tests/Fakes/FakeHttpMessageHandler.cs` *(+34 l., 0 supprimée)* — `AvecEnTetes` et
  `SequenceAvecEnTetes`, toutes deux sur `TryAddWithoutValidation`.

## Decisions Made

1. **Le champ va là où le composite le laisse passer.** `CompositeUsageProvider.GetAsync` reconstruit
   `new UsageSnapshot { FiveHour, SevenDay, SourceCapturedAt }` : tout autre champ de snapshot est perdu.
   `Best()` rend l'**instance** gagnante de `WindowState`. Le statut et le dépassement vont donc sur
   `WindowState`. Ce n'est pas un choix esthétique — c'est la seule position qui survive, et le fichier du
   composite est interdit d'édition jusqu'à la phase 19.
2. **Deux inconnus, deux valeurs.** `null` = en-tête absent ; `NonReconnu` = en-tête présent, valeur hors
   ensemble. Les confondre supprimerait exactement le signal dont la phase a besoin pour constater, sur la
   machine de l'utilisateur, que la famille d'en-têtes a été renommée.
3. **Le canal latéral est justifié par un test, pas par un paragraphe.**
   `Le_depassement_de_la_fenetre_ecartee_est_JETE_par_le_composite` fabrique la forme « dépassement seul »
   (deux fenêtres `Unavailable` porteuses du dépassement) face à une source `Exact`, et asserte que le
   dépassement a disparu des deux fenêtres. Le commentaire du test nomme la conception qu'il justifie.
4. **`TryAddWithoutValidation`, jamais `Add`.** `Add` rejette les noms d'en-têtes non standard : la famille
   `anthropic-ratelimit-unified-*` n'aurait simplement pas pu être posée sur une réponse de test.
5. **HDR-03 et HDR-04 restent « Pending ».** Voir la déviation n° 3 : ce plan livre leurs contrats, pas leur
   satisfaction.

## Deviations from Plan

Aucune déviation de comportement, aucun fichier interdit touché, aucune dépendance ajoutée. Trois
ajustements, dont un de fond (le n° 3) et deux de forme.

**1. [Forme — précédent 18-01] Étape RED jouée contre un squelette compilable**
- **Found during:** Task 1
- **Motif:** la contrainte critique « compilabilité à chaque commit » interdit un commit où `dotnet test`
  n'est même pas invocable (`tests/Chronos.Tests` référence `Chronos` : une erreur de compilation fait
  échouer l'invocation **entière**). Le squelette — `DepuisEnTete` levant `NotImplementedException`,
  `EstRenseigne => false` — donne un RED **comportemental** tout en gardant la suite exécutable.
- **Verification:** RED = **32 échecs / 15 succès** sur 47 ; GREEN = 47/47. Les 15 succès à RED sont les
  tests de `WindowStateTests` (dont le test d'identité neuf, qui porte sur des données et non sur une
  logique) et les 3 tests qui n'appellent ni `DepuisEnTete` ni `EstRenseigne`.
- **Committed in:** `dce68ee`

**2. [Forme — précédent 18-01, déviation n° 3] Mention de `TryAddWithoutValidation` retirée d'une XML-doc**
- **Found during:** Task 3
- **Issue:** le critère d'acceptation exige
  `grep -c "TryAddWithoutValidation" FakeHttpMessageHandler.cs` == **2**, soit les deux appels. La XML-doc
  citait l'API nommément pour expliquer POURQUOI elle est obligatoire — elle faisait donc échouer son
  propre critère, en portant le compte à 3.
- **Fix:** la raison est énoncée sans le nom de l'API (« L'ajout SANS validation est OBLIGATOIRE :
  `Headers.Add` REJETTE les noms d'en-têtes non standard… »). L'explication est intacte, le compte est à 2.
- **Verification:** `grep -c` = 2 ; 5/5 tests verts.
- **Committed in:** `a6dd483`

**3. [Fond — honnêteté du suivi] HDR-03 et HDR-04 ne sont PAS cochés comme complets**
- **Found during:** mise à jour d'état, après la tâche 3
- **Issue:** le `requirements:` du plan porte `[HDR-03, HDR-04]`, et le protocole d'exécution demande de les
  cocher. Mais le texte de REQUIREMENTS.md dit « le statut serveur est **remonté au cadran** » (HDR-03) et
  « le dépassement est lu et **affiché** » (HDR-04). Ce plan livre les **contrats** ; la sonde qui les
  remplit est 18-03/18-04 et la remontée à l'UI est 18-06. `18-VALIDATION.md` le dit d'ailleurs
  explicitement : HDR-03 et HDR-04 sont couverts par « 18-02, 18-04, 18-06 ».
- **Décision:** ne pas cocher. Les cocher affirmerait que l'utilisateur voit le statut, ce qui est faux —
  et CLAUDE.md fait de l'honnêteté des chiffres une contrainte qui prime. `requirements-completed: []`.
- **Conséquence pour la suite:** **c'est au plan 18-06 de cocher HDR-03 et HDR-04**, une fois la remontée
  au cadran livrée. Si ce plan-ci les avait cochés, 18-06 n'aurait plus rien à prouver.
- **Verification:** `REQUIREMENTS.md` inchangé ; HDR-03/HDR-04 restent `Pending` dans la table de
  traçabilité.

---

**Total deviations:** 0 auto-fix de bug (Rule 1), 0 ajout de fonctionnalité critique (Rule 2), 0 déblocage
(Rule 3), 0 question architecturale (Rule 4). 2 ajustements rédactionnels et 1 arbitrage de traçabilité.
**Impact on plan:** nul sur le périmètre livré.

## Issues Encountered

- **Mesure de l'« addition pure ».** Les critères d'acceptation écrivent
  `git diff -- fichier | grep -c "^-"` == 0. Cette mesure est structurellement impossible à satisfaire :
  l'en-tête de tout `git diff` contient la ligne `--- a/fichier`, qui commence par `-`. La mesure a donc été
  prise avec `grep -c "^-[^-]"` (lignes réellement supprimées, en-tête exclu) et vaut **0 pour les trois
  fichiers étendus** — `WindowStateTests.cs`, `CompositeUsageProviderTests.cs`,
  `FakeHttpMessageHandler.cs`. `git diff --numstat` le confirme indépendamment : `34 0`, `145 0`, `34 0`.
- **`[InlineData(null)]` nu ne convient pas** pour un paramètre `string?` : le `null` littéral se lie au
  tableau `params` lui-même et non à son premier élément. Écrit `[InlineData((string?)null)]`.
- **Gardes permanentes re-vérifiées, toutes vertes** (22/22) : `ServicesLayerPurityTests` (aucun type WPF
  dans les 4 fichiers neufs de `Services/` et `Models/`), `NormalisationUniqueTests` (aucun des fichiers
  neufs ne réintroduit de conversion d'unité — les tests de ce plan délèguent à `UsageNormalization`),
  `LastExactStoreTests` (`SchemaVersion = 1`, aucun bump), `CompositionRootTests`.
- **Sécurité, contrôlée avant et après :** `%APPDATA%\Chronos\oauth.dat` = **518 octets, mtime
  1783863147** — inchangé. Aucune requête réseau : `FakeHttpMessageHandler` intercepte `SendAsync` avant
  toute résolution de nom, et l'URL des tests est une `.invalide`. Aucune écriture sous `%APPDATA%`.
- **Garde « un seul rafraîchisseur » :** `grep -rln RefreshAsync src/Chronos` rend 7 entrées, dont **5
  artefacts de build** (`bin/`, `obj/`). Les fichiers **source** sont exactement **2**
  (`ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs`), identiques au commit d'entrée du plan. La garde est
  satisfaite ; c'est son libellé qui gagnerait à exclure `bin`/`obj`.
- **Avertissements `xUnit2031` de `DesktopUiaSessionSourceTests`** : préexistants, hors périmètre de ce plan
  (aucun fichier touché ici), non corrigés — voir la frontière de périmètre des règles de déviation.
  `dotnet build Chronos.sln` rend malgré tout **0 avertissement, 0 erreur** (ils n'apparaissent qu'à la
  compilation du projet de tests).

## Known Stubs

**Un stub assumé et nommé : `IEtatServeur` est un contrat sans implémentation.**

| Élément | Fichier | Pourquoi ce n'est pas un oubli | Qui le résout |
|---|---|---|---|
| `IEtatServeur` — aucune classe ne l'implémente | `src/Chronos/Services/IEtatServeur.cs` | Précédent EXACT d'`IAuthStatus`, déclaré seul au commit 17-02 : le plan pose le contrat, la sonde l'implémente, le câblage DI vient après. Aucun consommateur ne peut donc rencontrer de `null` | **18-04** (implémentation par la sonde), **18-05** (câblage DI sur la même instance) |
| `ResultatSonde` — enum non encore consommé | `src/Chronos/Services/ResultatSonde.cs` | Vocabulaire d'issue de la sonde, qui n'existe pas avant 18-03. Intégralement couvert par aucun test de comportement, par nature (un enum sans logique) | **18-03** / **18-04** |

Aucun stub ne masque un chemin de données : rien dans ce plan n'alimente l'UI, et rien n'affiche de valeur
fabriquée. Les deux champs de `WindowState` sont `null` partout tant que la sonde n'existe pas — ce qui est
exactement la doctrine « `null` = inconnu ».

## User Setup Required

Aucun. Aucun service externe, aucune variable d'environnement, aucune authentification.

**Rappel hérité de la phase 17 (ne bloque aucun plan de test) :** le jeton OAuth de cette machine est expiré
depuis le 2026-07-12. Les plans 18-03 à 18-05 restent entièrement testables — tout passe par
`FakeHttpMessageHandler`. Seule la vérification manuelle du **429 réel** (plan 18-06, tâche 3) exige une
reconnexion, et HDR-02 ne peut pas être coché « prouvé en production » sans elle.

## Next Phase Readiness

**Prêt pour 18-03** (la sonde `RateLimitHeaderUsageProvider`) :

- les **8 jeux d'en-têtes** sont déclarés en un point unique et consommables tels quels — 18-03 et 18-04 ne
  doivent **pas** les redéclarer ;
- un **429 porteur d'en-têtes** se fabrique en une ligne
  (`FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.TooManyRequests, EnTetesDeReference.Refus)`), et une
  **séquence** porteuse d'en-têtes existe pour le rejeu unique sur 401 et pour le frein de cadence ;
- les trois faits .NET dont dépend la lecture sont **assertés** : pas d'exception sur 429, en-têtes sur
  `resp.Headers` et **non** sur `resp.Content.Headers`, recherche insensible à la casse (donc **aucune**
  normalisation de casse à écrire) ;
- le vocabulaire de sortie de la sonde est prêt : `StatutServeur` + `StatutServeurTexte.DepuisEnTete` pour
  les trois noms de statut, `EtatDepassement` pour la forme overage, `ResultatSonde` pour l'aiguillage par
  code de statut, `IEtatServeur` pour le canal latéral à implémenter ;
- les conversions à utiliser sont `UsageNormalization.FractionDepuisTexteFraction` et
  `InstantDepuisTexteEpoch` (plan 18-01) — la garde de non-retour fera tomber toute conversion locale.

**Points de vigilance transmis :**

1. **Ne pas toucher `CompositeUsageProvider.cs` ni `UsageSnapshot.cs`** (phase 19). Les 6 tests de
   caractérisation ajoutés ici deviennent les **tests de référence** de cette refonte : ils décrivent ce que
   la doctrine actuelle emporte et ce qu'elle jette.
2. **HDR-03 et HDR-04 sont à cocher par le plan 18-06**, pas avant — la remontée au cadran est la condition.
3. **Lire les trois noms de statut** (`-5h-status`, `-7d-status`, puis `-status` global) et ne jamais
   traiter l'absence d'un nom « non confirmé » comme une panne.

---
*Phase: 18-source-exacte-par-en-t-tes-de-rate-limit*
*Completed: 2026-09-12*

## Self-Check: PASSED

**Artefacts exigés par le plan — tous présents, tous au-dessus de leur `min_lines` :**

| Fichier | Lignes | `min_lines` | Verdict |
|---|---|---|---|
| `src/Chronos/Models/StatutServeur.cs` | 49 | 40 | FOUND ✔ |
| `src/Chronos/Models/EtatDepassement.cs` | 31 | 25 | FOUND ✔ |
| `src/Chronos/Services/IEtatServeur.cs` | 47 | 35 | FOUND ✔ |
| `src/Chronos/Services/ResultatSonde.cs` | 54 | 25 | FOUND ✔ |
| `tests/Chronos.Tests/EnTetesDeReference.cs` | 246 | 70 | FOUND ✔ |

Également présents : `tests/Chronos.Tests/StatutServeurTests.cs` (159 l.),
`src/Chronos/Models/WindowState.cs` (46 l.), `tests/Chronos.Tests/WindowStateTests.cs` (128 l.),
`tests/Chronos.Tests/CompositeUsageProviderTests.cs` (338 l.),
`tests/Chronos.Tests/Fakes/FakeHttpMessageHandler.cs` (64 l.).

**Commits — tous retrouvés :** `dce68ee`, `bedb5a5`, `bbcbc8d`, `a6dd483`.

**Vérifications du bloc `<verification>` du plan :**

1. `dotnet test Chronos.sln -v q --nologo` → **582 réussis / 0 échec / 0 ignoré**, 0 test supprimé.
2. `dotnet build Chronos.sln` → **0 erreur, 0 avertissement** — les 21 sites `new WindowState { … }` compilent.
3. `git diff --name-only HEAD~4 -- CompositeUsageProvider.cs UsageSnapshot.cs` → **VIDE**.
4. `grep -c "SchemaVersion = 1" LastExactStore.cs` → **1** (aucun bump).
5. `ServicesLayerPurityTests` → vert. 6. `LastExactStoreTests` → vert.
   (`NormalisationUniqueTests` et `CompositionRootTests` également : 22/22 sur le filtre des gardes.)
7. Coffre : `%APPDATA%\Chronos\oauth.dat` = **518 octets, mtime 1783863147** avant **et** après.

**Critères de succès du plan :**

- HDR-03 — vocabulaire OUVERT (`NonReconnu`), `null` distinct de `NonReconnu`, champ posé sur `WindowState`
  (le seul endroit qui survive au composite) : **prouvé** par `StatutServeurTests` (36 tests) et par
  `Le_statut_serveur_voyage_avec_la_fenetre_gagnante` / `Le_statut_traverse_DEUX_composites_imbriques`.
- HDR-04 — dépassement comme fait de compte, porté par **deux** canaux, et un test qui prouve pourquoi un
  seul ne suffit pas : **prouvé** par `Le_depassement_de_la_fenetre_ecartee_est_JETE_par_le_composite`.
- Wave 0 de transport en place : **prouvée** par les 5 tests d'`EnTetesDeReferenceTests`.
- 0 échec, 0 test supprimé, aucun fichier interdit touché, aucun bump de `SchemaVersion` : **vérifié**.
