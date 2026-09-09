---
phase: 16-fondations-du-delta-persistance-d-molition-des-plafonds
plan: 03
subsystem: services
tags: [usage-pipeline, delta, dependency-injection, decorator, tolerant-parsing, test-parallelism]

# Dependency graph
requires:
  - phase: 03-pipeline-de-donnees
    provides: le parcours disque tolérant des transcripts JSONL, IUsageProvider, CompositeUsageProvider, ChronosPaths, IClock
  - phase: 16-01
    provides: la démolition du calibrateur, qui libérait le provider JSONL de son consommateur DI concret
  - phase: 16-02
    provides: LastExactStore + LastExactUsageProvider + ChronosPaths.LastExactFile, câblés ici
provides:
  - ITranscriptActivitySource / TranscriptActivity / TranscriptActivityLog — contrat de delta borné, sans aucun pourcentage
  - TranscriptActivityProvider — parcours disque conservé, amputé de toute production d'usage absolu
  - Chaîne IUsageProvider recâblée : composite exact-seulement, coiffé du décorateur de persistance EXA-01
  - Garantie structurelle EXA-04 — plus aucun type du dépôt ne sait dériver un pourcentage d'un comptage de tokens
  - Collection de test « XAML WPF » sérialisée — supprime une flakiness BAML de la suite
affects: [16-04 suppression des 6 champs de plafonds, 19 doctrine du composite + correction par delta DEL-03/04, 20 affichage de l'âge du relevé]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Séparation E/S pure / logique pure poussée jusqu'au contrat : ReadAsync() fait UNE passe disque, Since(t) est pur et appelable N fois"
    - "Contrat qui interdit un retour en arrière par sa FORME (ne pas hériter de IUsageProvider) plutôt que par une convention"
    - "Constante d'horizon déclarée une seule fois et servant à la fois au filtre de scan et à la valeur annoncée — les deux ne peuvent pas diverger"
    - "Renommage par git mv quand le corps doit survivre mot pour mot : l'historique prouve que le parcours n'a pas été réécrit"
    - "Classes de test chargeant du BAML regroupées dans une collection xUnit DisableParallelization"

key-files:
  created:
    - src/Chronos/Services/ITranscriptActivitySource.cs
    - tests/Chronos.Tests/TranscriptActivityLogTests.cs
    - tests/Chronos.Tests/XamlWpfCollection.cs
  modified:
    - src/Chronos/Services/TranscriptActivityProvider.cs
    - src/Chronos/Services/FiveHourWindowInference.cs
    - src/Chronos/Services/WeeklyWindow.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/TranscriptActivityProviderTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
    - tests/Chronos.Tests/OverlayWindowConfigTests.cs
    - tests/Chronos.Tests/ThemingTests.cs

key-decisions:
  - "La source de delta n'implémente PAS IUsageProvider, et un test réflexif l'interdit : EXA-04 devient structurel dès la phase 16, pas seulement documenté."
  - "Le journal expose Horizon/Covers : une interrogation antérieure au filtre mtime de 8 jours est déclarée non couverte plutôt que servie sous-évaluée."
  - "Borne basse STRICTEMENT exclusive, borne haute inclusive : le message posé exactement sur l'instant du relevé exact a déjà été compté par le serveur."
  - "CompositeUsageProvider.cs n'a pas bougé d'un octet (git diff vide sur bfb8ec8..HEAD) — la doctrine de choix de source reste la phase 19."
  - "L'isolation des chemins de CompositionRootTests n'a PAS stabilisé le [WpfFact] instable : la cause réelle est une course du chargeur BAML de WPF sous parallélisme xUnit, corrigée par une collection sérialisée."

patterns-established:
  - "Un contrat de delta se teste à deux niveaux : le bornage en PUR (aucune fixture), l'alimentation du bornage sur fixtures disque réelles."
  - "Avant d'imputer une flakiness au plan courant, la mesurer : worktree git sur le commit de base + N exécutions comparées."

requirements-completed: [DEL-01, DEL-02]

# Metrics
duration: 29min
completed: 2026-09-09
---

# Phase 16 Plan 03 : Les transcripts deviennent une source de delta — Summary

**Les transcripts JSONL ne savent plus répondre qu'à deux questions bornées — « activité depuis T ? » et « tokens depuis T ? » — et ne peuvent plus, par construction du type, fabriquer un pourcentage : le maillon estimé a quitté la chaîne des sources, remplacée par une chaîne exacte coiffée du décorateur de persistance du dernier relevé.**

## Performance

- **Duration:** 29 min
- **Started:** 2026-09-09T12:07:53Z
- **Completed:** 2026-09-09T12:37:18Z
- **Tasks:** 3 / 3 (+ 1 correctif de flakiness)
- **Files:** 3 créés, 9 modifiés (dont 2 renommages `git mv`)
- **Tests:** 406 → **414**, **0 échec** (9 exécutions complètes après correctif, dont 3 à froid)

## Accomplishments

- **DEL-01 et DEL-02 sont livrés sous une forme qui ne peut pas mentir.** `ITranscriptActivitySource.ReadAsync()`
  fait UNE passe disque et rend un `TranscriptActivityLog` **pur** ; `Since(t)` répond `HasActivity` /
  `Tokens` / `LastActivityAt` sur `]t ; Now]`, autant de fois qu'on veut, sans re-toucher au disque. Les
  deux fenêtres 5 h et hebdo ont des bornes basses différentes mais répondent depuis un **seul**
  instantané disque — donc cohérentes entre elles par construction.

- **EXA-04 est acquis structurellement, pas par convention.** `TranscriptActivityProvider` n'implémente
  plus `IUsageProvider`, ne connaît plus `SettingsService`, et `BuildFiveHour`/`BuildSevenDay` — où vivait
  `Utilization = tokens / plafond` — ont disparu. Un test réflexif
  (`Assert.False(typeof(IUsageProvider).IsAssignableFrom(typeof(TranscriptActivityProvider)))`) casse si un
  futur développeur rebranche le provider dans la chaîne. `grep` sur `src/` : plus aucune dérivation d'un
  pourcentage à partir d'un comptage.

- **Le journal déclare ce qu'il ignore.** `Horizon = Now − 8 j` (le filtre mtime du scan) et
  `Covers(since)` : un relevé exact plus vieux que l'horizon ne peut pas être corrigé par ce journal, et
  l'appelant de la phase 19 pourra refuser le delta au lieu d'en servir un silencieusement sous-évalué.
  La constante des 8 jours est déclarée **une seule fois** et sert à la fois au `cutoff` du scan et à
  l'`Horizon` annoncé : les deux ne peuvent pas diverger.

- **Le parcours disque a survécu mot pour mot**, prouvé par le `git mv`
  (`git log --follow` sur `TranscriptActivityProvider.cs` → 4 commits) : partage lecture/écriture du flux,
  ligne partielle et ligne corrompue ignorées, détection d'assistant STRUCTURÉE (jamais une chaîne de
  prose), somme `input + output + cache_creation + cache_read`, rejet des timestamps futurs, scan récursif
  incluant `subagents/`, filtre mtime. Les 4 tests de tolérance qui couvraient ces gardes ont été conservés,
  assertions adaptées au delta.

- **EXA-01 est ACTIF de bout en bout.** Le magasin et le décorateur livrés « en briques » par le plan 16-02
  sont câblés : `LastExactStore` sur `ChronosPaths.LastExactFile`, `LastExactUsageProvider` en **tête** de
  la chaîne `IUsageProvider`. La garde DI le prouve par `Assert.IsType<LastExactUsageProvider>` — le
  décorateur ne peut pas être enterré au milieu de la chaîne sans casser le test.

- **La chaîne composite se termine désormais sur une source EXACTE** (pont statusLine), l'imbrication
  passant de 3 à 2 niveaux. `CompositeUsageProvider.Best()` n'a pas bougé d'une ligne.

- **Une flakiness de la suite a été éliminée** (voir Déviations) : `Host_resout_et_dispose_les_singletons`
  échouait par intermittence sur une course du chargeur BAML de WPF. 9 exécutions complètes vertes après
  correctif, dont 3 à froid.

## Task Commits

| # | Tâche | Type | Commit | Résultat |
|---|---|---|---|---|
| 1 | Contrat de delta + conversion du provider (renommage, pas réécriture) | `refactor` | `e365b83` | build : 2 erreurs, **exactement** les 2 références résiduelles à `JsonlEstimationProvider` dans `App.xaml.cs` prévues par le plan |
| 2 | Réécriture des tests JSONL en preuves de delta + tests purs du journal | `test` | `82ad71b` | 10 + 5 = **15 tests, 0 échec** |
| 3 | Recâblage de la chaîne (sortie du JSONL, décorateur en tête, garde DI) | `feat` | `d4afe4e` | build 0 erreur, suite **414 / 0 échec** |
| — | Correctif de flakiness BAML (déviation Rule 1) | `fix` | `8fdb222` | 6 exécutions complètes vertes, dont 3 à froid |

### Note d'ordonnancement — deux états intermédiaires non compilables

Le plan sanctionne explicitement cet état (critère d'acceptation de la tâche 1 : « la compilation ÉCHOUE
de façon attendue à cette étape UNIQUEMENT sur les références restantes »). La granularité imposée le rend
inévitable : sortir le type de la chaîne (production) et réécrire ses tests sont deux gestes que rien ne
permet de rendre simultanément compilables sans les fusionner en un seul commit.

**Mitigation appliquée :** le CONTENU de chaque commit a été vérifié vert **avant** d'être committé — les
modifications des tâches 2 et 3 ont été écrites, buildées et exécutées ensemble, puis committées en deux
temps. Aucun commit ne contient de code non exécuté ; seuls deux états d'arbre intermédiaires ne buildent
pas. `git log` est donc utilisable pour la revue ; `git bisect` sur ces deux commits ne l'est pas.

## Files Created/Modified

### Créés (3)

| Fichier | Rôle |
|---|---|
| `src/Chronos/Services/ITranscriptActivitySource.cs` | Contrat de delta : `TranscriptActivity` (record de résultat borné), `TranscriptActivityLog` (journal PUR : `Now`, `Horizon`, `Covers`, `Since`), `ITranscriptActivitySource` (une seule méthode d'E/S). N'hérite d'aucun contrat d'usage. |
| `tests/Chronos.Tests/TranscriptActivityLogTests.cs` | 5 tests PURS du bornage, aucun accès disque : somme sur `]since ; Now]`, exclusivité stricte de la borne basse, inclusivité de la borne haute, dernière activité sur entrées volontairement non triées, `Covers` de part et d'autre de l'horizon. |
| `tests/Chronos.Tests/XamlWpfCollection.cs` | `[CollectionDefinition("XAML WPF", DisableParallelization = true)]` — sérialise les classes de test qui chargent du BAML. |

### Modifiés (9)

- `src/Chronos/Services/TranscriptActivityProvider.cs` — **renommé** depuis `JsonlEstimationProvider.cs`
  (`git mv`). Perd `: IUsageProvider`, le champ `_settings` et le 3ᵉ paramètre de ctor, `GetAsync`,
  `BuildFiveHour`, `BuildSevenDay`, les appels à `FiveHourWindowInference`/`WeeklyWindow` et
  `using Chronos.Models`. Gagne `ReadAsync` et la constante `HorizonSpan`. Le corps du parcours disque est
  inchangé.
- `src/Chronos/Services/FiveHourWindowInference.cs`, `src/Chronos/Services/WeeklyWindow.cs` — `<remarks>`
  « ORPHELIN ASSUMÉ » ajouté. **Non supprimés**, 11 tests toujours verts, en réserve pour la phase 19.
- `src/Chronos/App.xaml.cs` — `AddSingleton<JsonlEstimationProvider>` remplacé par
  `AddSingleton<ITranscriptActivitySource>` (hors chaîne) + `AddSingleton<LastExactStore>` ;
  `IUsageProvider` devient `LastExactUsageProvider(inner: Composite(ChronosOAuth, Composite(GatedOAuth,
  ClaudeUsageObject)), store, clock)` ; commentaire d'en-tête du pipeline reformulé (plus de « repli JSONL »).
- `tests/Chronos.Tests/TranscriptActivityProviderTests.cs` — **renommé** depuis
  `JsonlEstimationProviderTests.cs` (`git mv`), réécrit : 10 tests. Helpers d'isolation
  (`TestDataDir` via `[CallerFilePath]`, `IsolatedRootWith`) et horloge figée conservés ; `ProviderFor`
  perd son `SettingsService`.
- `tests/Chronos.Tests/CompositionRootTests.cs` — la garde DI reproduit le nouveau graphe, prouve le
  décorateur en tête, résout `ITranscriptActivitySource`, et bascule sur un `usage.json` en dossier temp.
- `tests/Chronos.Tests/CadranBindingTests.cs`, `OverlayWindowConfigTests.cs`, `ThemingTests.cs` —
  attribut `[Collection("XAML WPF")]` uniquement. **Aucune assertion modifiée.**

### Fixtures : intactes

`git status --porcelain tests/Chronos.Tests/TestData/` → vide. Les 4 fixtures JSONL existantes suffisaient,
conformément à la Wave 0 de `16-VALIDATION.md`.

## Tests supprimés — justification nominative

**Critère appliqué : 0 échec + aucune perte de couverture nette.** Bilan : **406 → 414 (+8)** — 7 tests de
`JsonlEstimationProviderTests` remplacés par 15.

| Test supprimé | Code disparu qui le justifie |
|---|---|
| `Utilization_5h_estimee_avec_plafond` | `BuildFiveHour` + `settings.FiveHourTokenBudget` : l'utilisation absolue dérivée d'un comptage de tokens n'existe plus (EXA-04). Aucun comportement survivant ne perd sa couverture. |
| `Plafond_defini_laisse_la_fenetre_5h_Estimated_avec_utilization` | idem — il testait la relation plafond → couleur, disparue avec le plafond. La règle CAL-03 qu'il portait (« un plafond ne rend jamais une fenêtre Exact ») n'a plus d'objet : plus aucun plafond, plus aucun producteur d'`Estimated`. |
| `Valide_somme_par_fenetre_et_marque_Estimated` | **Remplacé**, pas perdu : sa somme 5 h (1550) et sa somme hebdo (2150) sont devenues `Tokens_depuis_T_borne_sur_la_fenetre_5h` et `..._hebdo`. Ses assertions `ResetsAt`/`FractionTimeRemaining`/`Estimated` portaient sur `BuildFiveHour`, supprimé. |
| `Fenetre_inactive_arc_plein_sans_tokens` | **Remplacé** par `Aucune_activite_depuis_T` (DEL-01) sur la même fixture. Ses assertions d'arc plein portaient sur `BuildFiveHour`. |
| `Tolerant_ignore_corrompue_partielle_prose_et_user_sans_exception` | **Conservé**, assertions adaptées (700 tokens via `Since`). |
| `Subagents_inclus_dans_la_somme_recursive` | **Conservé**, assertions adaptées (800 tokens via `Since`). |
| `Dossier_absent_renvoie_zero_sans_exception` | **Conservé** sous le nom `Dossier_absent_renvoie_un_journal_vide_sans_exception`. |

**Tests ajoutés (10) :** borne basse strictement exclusive, dernière activité exposée, horizon 8 jours,
non-retour réflexif (`n_est_pas_un_IUsageProvider`), + les 5 tests purs du journal, + la reformulation des 4
conservés en preuves de delta. **Aucune garde de robustesse n'a disparu.**

## Décisions prises

- **La source de delta n'est pas un `IUsageProvider`, et un test l'interdit.** C'était l'option la plus
  radicale des trois envisagées par la recherche (§Q1) ; c'est aussi la seule qui rend le retour en arrière
  détectable automatiquement. Un maillon composite dégradé renvoyant `Unavailable` aurait été du code mort
  sur trois phases, sans protéger aucun affichage (rang 0 dans `Best()`).
- **`Horizon` et `Covers` font partie du contrat, pas de l'implémentation.** Sans eux, un relevé exact vieux
  de plus de 8 jours produirait un delta silencieusement sous-évalué — exactement le genre de chiffre faux
  que ce milestone démolit. La constante est déclarée une fois pour que le filtre et la promesse ne puissent
  pas diverger.
- **Borne basse exclusive / borne haute inclusive.** Le message dont le timestamp vaut exactement l'instant
  du relevé exact a déjà été compté par le serveur ; le recompter le compterait deux fois. Deux tests
  distincts couvrent la règle (un sur fixture réelle, un en pur).
- **`FiveHourWindowInference` et `WeeklyWindow` conservés.** Orphelins de production assumés et documentés
  dans leur `<remarks>` ; 11 tests verts conservés ; réemploi probable en phase 19 (borne basse d'un delta
  quand `ResetsAt` est inconnu).
- **`CompositeUsageProvider` n'a pas été rouvert.** `git diff --name-only bfb8ec8..HEAD` sur ce fichier :
  vide. La règle `Best()` reste la phase 19.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Flakiness de `Host_resout_et_dispose_les_singletons` : course du chargeur BAML de WPF**

- **Found during:** vérification finale de la Task 3 (suite complète).
- **Issue:** le test échouait par intermittence — **2 échecs sur 9 exécutions**, les deux immédiatement
  après un `dotnet clean` — sur
  `XamlParseException : The given key 'ColumnDefinitions' was not present in the dictionary`, levée depuis
  `MainWindow.InitializeComponent()` sur un XAML pourtant valide et inchangé. La pile désigne
  `System.Windows.Baml2006.WpfXamlType.FindKnownMember` → `ConcurrentDictionary.get_Item` →
  `ThrowKeyNotFoundException` : le chargeur BAML peuple **paresseusement** une table de membres connus qui
  n'est pas sûre en accès concurrent. xUnit exécutant les classes de test en parallèle, deux `[WpfFact]` sur
  des threads STA distincts peuvent charger du BAML en même temps.
- **Pourquoi c'est traité ici et pas différé :** le plan pose « 0 échec » en critère de succès, et la
  contrainte d'entrée demandait explicitement de vérifier si l'isolation des chemins stabilisait ce test.
  **Réponse mesurée : non.** L'isolation des chemins (`ChronosPaths` en dossier temp) supprime bien la
  pollution du profil réel — c'était son but — mais elle a probablement *aggravé* l'exposition à la course :
  `SettingsService.Load()` ne lit plus un vrai fichier, donc la fenêtre WPF est construite quelques
  millisecondes plus tôt, en chevauchement avec le chargement BAML d'une autre classe. Mesure comparative
  sur worktree git au commit de base `bfb8ec8` : **7 exécutions complètes, 0 échec** (dont 3 à froid), contre
  2 échecs sur 9 après le recâblage. Le défaut est dans WPF, mais son déclenchement est imputable à ce plan.
- **Fix:** collection xUnit `« XAML WPF »` avec `DisableParallelization = true`, appliquée aux **4** classes
  qui chargent un type XAML (`CompositionRootTests`, `CadranBindingTests`, `OverlayWindowConfigTests`,
  `ThemingTests`). Les chargements de BAML sont sérialisés entre eux ; **le reste de la suite garde son
  parallélisme** (une désactivation globale aurait coûté bien plus : les deux `DiagnosticServiceTests`
  durent 17 s chacun et s'exécutent aujourd'hui en parallèle).
- **Files modified:** `tests/Chronos.Tests/XamlWpfCollection.cs` (créé),
  `CompositionRootTests.cs`, `CadranBindingTests.cs`, `OverlayWindowConfigTests.cs`, `ThemingTests.cs`
  (attribut uniquement, aucune assertion touchée).
- **Verification:** **6 exécutions complètes vertes** après correctif, dont **3 à froid** (`dotnet clean`
  préalable, le scénario qui déclenchait les deux échecs). Coût : ~38 s au lieu de ~35 s.
- **Committed in:** `8fdb222`

### Écarts de critères assumés (littéralement inatteignables)

**1. `grep -rn "JsonlEstimationProvider" src/ tests/ --include=*.cs` → attendu 0, obtenu 1.**
L'unique occurrence restante est un **commentaire de fin de ligne** dans
`src/Chronos/Services/CompositeUsageProvider.cs:14`
(`private readonly IUsageProvider _fallback;  // JsonlEstimationProvider (Estimated)`).
Ce critère est en **contradiction directe** avec deux contraintes dures du même plan : « NE PAS toucher à
`CompositeUsageProvider.cs` » et le critère voisin `git diff --name-only … CompositeUsageProvider.cs` →
vide. La contrainte dure l'emporte : le fichier n'a pas été rouvert. **Même arbitrage qu'au plan 16-01** pour
le commentaire `CAL-02` résiduel de `BudgetSource.cs`. Le commentaire est désormais périmé et devra être
reformulé par la phase 19, qui rouvre ce fichier de toute façon.

**2. `grep -c "CompositeUsageProvider" src/Chronos/App.xaml.cs` → attendu 2, obtenu 3.**
Le 3ᵉ résultat est le **commentaire fourni par le plan lui-même** (« La règle `Best()` de
`CompositeUsageProvider` n'est PAS modifiée »). L'intention du critère — l'imbrication passe de 3 à 2
niveaux — est vérifiée à la lettre par `grep -c "new CompositeUsageProvider("` → **2**.

**3. `grep -c "FileShare.ReadWrite"` sur le provider → attendu 1, obtenu 2.**
Le fichier en comptait **déjà 2 avant ce plan** (le commentaire d'explication + l'appel). Le compte est donc
strictement inchangé, ce qui est précisément ce que le critère cherche à prouver (« parcours conservé mot
pour mot »). Une mention supplémentaire introduite dans le nouveau XML-doc a été reformulée pour ne pas
faire dériver ce compte.

**4. `dotnet test --filter "FullyQualifiedName~CompositeUsageProviderTests"` → attendu 9, obtenu 8.**
Déjà relevé et documenté par le plan 16-02 : le fichier en comptait 8 avant la phase. Exigence réelle
(fichier intact, tous verts) satisfaite.

**5. Verification §6 — `grep -rn "Utilization\s*=\s*.*[Bb]udget\|tokens / \|/ budget" src/` → attendu 0,
obtenu 3.** Aucune n'est une dérivation d'un pourcentage :
`BudgetSource.cs:8` (commentaire d'un fichier supprimé au plan **16-04**, hors périmètre par contrainte
dure) et `Text/TokenFormatter.cs:17,19` (`tokens / 1_000_000d` — abréviation d'affichage « ≈ N M tokens »,
sans aucun rapport avec un quota). Une formulation de mon propre XML-doc qui déclenchait ce motif a été
reformulée pour garder la garde lisible.

---

**Total deviations:** 1 auto-corrigée (Rule 1, bug de flakiness mesuré) + 5 écarts de critères documentés.
**Impact on plan:** aucun scope creep sur la production — les 5 fichiers de `src/` touchés sont exactement
ceux de `files_modified`. Le correctif de flakiness ajoute 4 fichiers de test au périmètre, sans modifier
une seule assertion.

## Disparitions visuelles ATTENDUES (à ne PAS traiter comme des régressions)

Conformes à l'analyse chiffrée §Q1 de `16-RESEARCH.md` sur l'état réel de la machine :

| Élément | État attendu après ce plan | Pourquoi ce n'est pas une régression |
|---|---|---|
| Couleur de l'anneau **hebdo** | **gris** (`Utilization = null`) | elle venait de `tokens / 5 817 635 413`, plafond calibré sous Max x5 et faux d'un facteur ~4. Perdre une couleur mensongère est le BUT du milestone. |
| Texte **« ≈ N tokens »** | **disparu** | plus aucun producteur de `SourceReliability.Estimated` dans le dépôt. |
| Compte à rebours **hebdo** | **conservé** | il vient de `WeeklyRecalibration` + `WeeklyAnchor`, pas du plafond. `Apply` agit aussi sur une fenêtre `Unavailable`. |
| Anneau **5 h** | **inchangé** | toujours alimenté par `usage.json` marqué `Exact`, désormais aussi persisté et rebouchable par le décorateur EXA-01. |
| Bandeau **« données indisponibles »** | **non déclenché** | il exige les DEUX fenêtres `Unavailable` ; la 5 h reste `Exact`. |

`SourceReliability.Estimated`, `PercentFormatter` (préfixe « ~ ») et `WeeklyRecalibration` restent en place,
**dormants** — la phase 19 (DEL-04, « exact + delta marqué ») en aura besoin.

## Issues Encountered

- **2 avertissements xUnit2031 pré-existants** dans `DesktopUiaSessionSourceTests.cs` (lignes 345 et 364),
  déjà signalés aux plans 16-01 et 16-02. Hors périmètre (SCOPE BOUNDARY), non corrigés.
- **Les deux `DiagnosticServiceTests` durent 17 s chacun** (temporisation réseau du lecteur de token). Ils
  dominent la durée de la suite et sont la raison pour laquelle la sérialisation du correctif de flakiness a
  été limitée aux 4 classes XAML plutôt qu'appliquée globalement. Non traité (hors périmètre), consigné.
- **Le critère « 0 échec » n'est vérifiable que sur plusieurs exécutions** quand une flakiness est en jeu :
  la suite était verte 2 fois sur 3 avant correctif. Toute vérification de phase qui se contenterait d'une
  seule exécution peut donner une fausse assurance — d'où les 9 exécutions consignées ici.

## Known Stubs

Aucun. `grep "TODO\|FIXME\|placeholder\|coming soon"` sur les 3 fichiers créés et les 5 fichiers de
production modifiés → 0 résultat. Aucune valeur codée en dur ne rejoint l'UI.

**À noter (différé par conception, pas un stub) :** `ITranscriptActivitySource` est enregistré en DI mais
**n'a encore aucun consommateur de production**. C'est délibéré et conforme à la ROADMAP : la correction par
delta (DEL-03/DEL-04) qui l'appellera est la **phase 19**. Le contrat, son implémentation et sa résolution DI
sont prouvés ici pour que la phase 19 n'ait qu'à le consommer.

## User Setup Required

Aucune. `%APPDATA%\Chronos\last-exact.json` se crée tout seul au premier relevé exact, sans droit admin.

## Verification

| # | Vérification | Attendu | Résultat |
|---|---|---|---|
| 1 | `dotnet clean && dotnet build Chronos.sln -v q --nologo` | 0 erreur | **0 erreur** (2 avertissements pré-existants) |
| 2 | `dotnet test Chronos.sln -v q --nologo` | 0 échec | **414 / 0 échec**, 9 exécutions (6 après correctif de flakiness, dont 3 à froid) |
| 3 | `--filter "…ServicesLayerPurityTests"` | vert | 1/1 — aucun type WPF dans les nouveaux services |
| 4 | `--filter "…CompositionRootTests"` | vert | 3/3, stable sur 6 exécutions |
| 5 | `--filter "…FiveHourWindowInferenceTests"` + `"…WeeklyWindowTests"` | 7 + 4 | **11/11** — orphelins conservés avec leur couverture |
| 6 | `--filter "…TranscriptActivityProviderTests"` | 10 tests | **10/10** |
| 7 | `--filter "…TranscriptActivityLogTests"` | 5 tests | **5/5** |
| 8 | `--filter "…CompositeUsageProviderTests"` | verts, fichier intact | **8/8**, `git diff` du fichier vide |
| 9 | `git diff --name-only bfb8ec8..HEAD -- …/CompositeUsageProvider.cs` | vide | **vide** |
| 10 | `git log --follow --oneline -- …/TranscriptActivityProvider.cs \| wc -l` | > 1 | **4** — historique préservé |
| 11 | `grep "IUsageProvider\|UsageSnapshot\|Utilization\|TokenBudget\|SettingsService"` sur le provider | 0 | **0** |
| 12 | `grep -c "TimeSpan.FromDays(8)"` sur le provider | 1 | **1** — constante unique |
| 13 | `grep -c "new LastExactUsageProvider("` / `"AddSingleton<ITranscriptActivitySource>"` dans `App.xaml.cs` | 1 / 1 | **1 / 1** |
| 14 | `grep -c "new CompositeUsageProvider("` dans `App.xaml.cs` | 2 | **2** — imbrication 3 → 2 niveaux |
| 15 | `grep -c "ORPHELIN ASSUMÉ"` sur les 2 calculateurs purs | 1 chacun | **1 / 1**, fichiers présents |
| 16 | `ls "$APPDATA/Chronos/last-exact.json"` après la suite | absent | **absent** — garde anti-accident effective |
| 17 | `git status --porcelain tests/Chronos.Tests/TestData/` | vide | **vide** — aucune fixture modifiée |

## Next Phase Readiness

**Prêt pour le plan 16-04** (suppression des 6 champs de plafonds + `BudgetSource` + `DiagnosticService` +
preuve DEL-06) : son verrou est levé. `FiveHourTokenBudget` et `WeeklyTokenBudget` n'ont plus qu'un seul
lecteur dans le dépôt, `DiagnosticService`, ce que 16-04 traite dans le même commit atomique que la
suppression des champs. `BudgetSource.cs` reste intact, avec son commentaire `CAL-02` — 16-04 le supprime.

**Prêt pour la phase 19** (doctrine du composite + correction par delta) :
- le point de couture est unique et déjà en place — `LastExactUsageProvider.GetAsync`, qui voit le snapshot
  fusionné, dispose du `CapturedAt` **par fenêtre** et peut résoudre `ITranscriptActivitySource` ;
- le delta est bornable **par fenêtre** depuis une seule passe disque, avec un `Horizon` qui dit quand
  refuser de répondre ;
- `FiveHourWindowInference` et `WeeklyWindow` sont disponibles pour inférer une borne basse quand
  `ResetsAt` est inconnu ;
- `SourceReliability.Estimated` est libre : plus aucun producteur, donc réutilisable comme marqueur
  « exact + delta » sans ambiguïté rétrospective.

**Aucun blocage.** Suite complète : 414 tests, 0 échec, stable.

---
*Phase: 16-fondations-du-delta-persistance-d-molition-des-plafonds*
*Completed: 2026-09-09*

## Self-Check: PASSED

- 10 fichiers verifies presents sur disque (3 crees, 6 modifies, + le SUMMARY).
- 2 fichiers verifies absents : `JsonlEstimationProvider.cs` et `JsonlEstimationProviderTests.cs`
  (renommes par `git mv`, historique preserve).
- 4 commits verifies presents dans l historique : `e365b83`, `82ad71b`, `d4afe4e`, `8fdb222`.
- Aucun stub : `grep "TODO|FIXME|placeholder|coming soon"` sur les fichiers crees -> 0.
- `dotnet build Chronos.sln` : 0 erreur. `dotnet test Chronos.sln` : 414 reussis, 0 echec.
