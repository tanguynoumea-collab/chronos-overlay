---
phase: 19-nouvelle-doctrine-du-composite
plan: 01
subsystem: services
tags: [usage-providers, horodatage, persistance, memoisation, transcripts, exa-02, exa-05]

# Dependency graph
requires:
  - phase: 16-fondations-du-delta
    provides: "WindowState.CapturedAt (champ existant mais non rempli), LastExactStore, ITranscriptActivitySource + TranscriptActivityLog pur"
  - phase: 18-source-en-tetes
    provides: "UsageNormalization (point unique de conversion), RateLimitHeaderUsageProvider (le seul provider qui horodatait déjà — modèle imité)"
provides:
  - "Les QUATRE providers exacts horodatent désormais chaque fenêtre : la limite d'âge d'EXA-02 devient applicable PAR FENÊTRE"
  - "ClaudeUsageObjectProvider porte l'âge du FICHIER (clé capturedAt), jamais l'instant de lecture — verrouillé par fixture"
  - "LastExactStore.Convertir exige TROIS champs : plus aucun captured_at null incertifiable n'est persisté"
  - "LastExactStore.UnExactADejaEteObtenu() distingue les deux silences de Load (EXA-05)"
  - "SourceActiviteMemoisee : décorateur à durée de validité, non levant, neutre, NON câblé"
  - "FakeTranscriptActivitySource : journal programmable + compteur de passes + mode panne"
affects: [19-02, 19-03, 19-04, 19-05, 20-honnetete-visible]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Horodatage PAR FENÊTRE porté par la source qui a produit la donnée, pas par le lecteur"
    - "Décorateur de mémoïsation SemaphoreSlim(1,1) + double vérification (motif ChronosTokenAuthority, phase 17)"
    - "Péremption mesurée sur l'instant de la PASSE DISQUE et non sur l'instant de mise en cache"

key-files:
  created:
    - src/Chronos/Services/SourceActiviteMemoisee.cs
    - tests/Chronos.Tests/Fakes/FakeTranscriptActivitySource.cs
    - tests/Chronos.Tests/SourceActiviteMemoiseeTests.cs
    - tests/Chronos.Tests/TestData/usage-ancien.json
  modified:
    - src/Chronos/Services/ClaudeUsageObjectProvider.cs
    - src/Chronos/Services/ChronosOAuthUsageProvider.cs
    - src/Chronos/Services/ClaudeOAuthUsageProvider.cs
    - src/Chronos/Services/LastExactStore.cs
    - tests/Chronos.Tests/ClaudeUsageObjectProviderTests.cs
    - tests/Chronos.Tests/LastExactStoreTests.cs

key-decisions:
  - "INVENTAIRE RÉEL MESURÉ : ZÉRO test cassé par l'ajout de CapturedAt. Aucun test des quatre providers ne comparait un WindowState entier — la crainte du plan était une hypothèse, la mesure la révoque."
  - "SourceActiviteMemoisee volontairement NON enregistrée dans le graphe DI : le câblage est le plan 19-03, en un seul commit avec le consommateur (précédent 17-03)."
  - "usage-partial.json ne contient PAS de capturedAt : la fixture de repli prévue par le plan (usage-sans-capture.json) était inutile, vérifié avant écriture du test."
  - "EXA-02 et EXA-05 restent Pending : ce plan livre les FAITS qu'ils exigent, pas la doctrine qui les applique."

patterns-established:
  - "Étape RED jouée contre un SQUELETTE compilable (NotImplementedException) et non contre un type absent : tests/Chronos.Tests référence Chronos, un commit non compilable rendrait dotnet test non invocable (précédent 18-01)."
  - "Le commentaire qui décrit un piège ne reproduit JAMAIS l'expression fautive : il la décrit, pour qu'un grep de non-retour reste falsifiable."

requirements-completed: []

# Metrics
duration: 24min
completed: 2026-09-12
---

# Phase 19 Plan 01: Horodatage certifiable, magasin lucide et mémoïseur Summary

**Les quatre providers exacts horodatent désormais chaque fenêtre avec l'instant de CAPTURE de la source (le fichier pour `usage.json`, la réponse HTTP pour les deux OAuth), le magasin refuse de persister un relevé incertifiable et sait distinguer « jamais rien vu » de « vu mais la fenêtre a roulé », et un décorateur de mémoïsation à durée de validité rend la doctrine de la phase 19 abordable (536 Mo lus une fois par minute au lieu d'une fois par tick).**

## Performance

- **Duration:** 24 min (dont ~11 min de suites complètes : 3 passes × 2 min 10)
- **Started:** 2026-09-12T05:30:00Z
- **Completed:** 2026-09-12T05:54:00Z
- **Tasks:** 3 / 3
- **Files modified:** 10 (4 créés, 6 modifiés)

## Accomplishments

- **Le piège le plus grave de la phase est désamorcé et verrouillé.** `ClaudeUsageObjectProvider` porte
  l'horodatage du **fichier** (`capturedAt`, epoch ms), pas celui de la lecture. Une fixture reproduisant la
  forme du fichier RÉEL de production (10 %, écrit le 2026-07-10) est lue deux mois plus tard et prouve que
  l'âge retenu est bien celui du fichier. `grep -cE "CapturedAt\s*=\s*_clock"` sur ce fichier rend **0**.
- **La limite d'âge d'EXA-02 devient applicable.** Elle n'avait jusqu'ici rien à mesurer pour trois des
  quatre providers exacts : seule la sonde d'en-têtes (phase 18) remplissait `WindowState.CapturedAt`.
- **Le magasin ne grave plus l'incertifiable.** `Convertir` exige trois champs au lieu de deux ; une fenêtre
  exacte sans instant de capture ne produit même plus de fichier.
- **EXA-05 a son fait de base.** `UnExactADejaEteObtenu()` rend `true` là où `Load()` rend `null` quand un
  relevé a existé mais que sa fenêtre a roulé — la fausse alerte « connecte-toi » sur un compte sain est
  structurellement évitable.
- **L'inventaire des impacts est une MESURE, pas une hypothèse** (voir section dédiée).

## Task Commits

1. **Task 1 (RED): fixture + tests de l'âge du fichier** — `0e0c14f` (test)
2. **Task 1 (GREEN): les trois providers exacts qui omettaient `CapturedAt`** — `5c3b5fd` (feat)
3. **Task 2 (RED): les deux silences du magasin + refus de l'incertifiable** — `506f2b0` (test)
4. **Task 2 (GREEN): `Convertir` à trois conditions + `UnExactADejaEteObtenu`** — `3320856` (feat)
5. **Task 3 (RED): faux à compteur de passes + contrat du mémoïseur** — `a0598a8` (test)
6. **Task 3 (GREEN): `SourceActiviteMemoisee`** — `27ef753` (feat)

Aucune étape REFACTOR n'a été nécessaire : les trois implémentations sont sorties propres du GREEN.

## Files Created/Modified

**Créés**
- `src/Chronos/Services/SourceActiviteMemoisee.cs` — décorateur `ITranscriptActivitySource` à durée de
  validité (60 s par défaut), `SemaphoreSlim(1,1)` + double vérification, non levant tant qu'un journal est
  connu, aucun type WPF.
- `tests/Chronos.Tests/Fakes/FakeTranscriptActivitySource.cs` — journal programmable, compteur de passes
  thread-safe (`Interlocked`/`Volatile`), mode panne (`Journal = null` ⇒ `IOException`).
- `tests/Chronos.Tests/SourceActiviteMemoiseeTests.cs` — 6 tests.
- `tests/Chronos.Tests/TestData/usage-ancien.json` — fixture de forme identique au fichier réel de
  production (`capturedAt: 1783673700000` = 2026-07-10T08:55:00Z, `resets_at: 1783684500`). Les deux epochs
  ont été **vérifiés en exécution** avant écriture du test, et sont au-dessus du plancher 2020-01-01 de
  `UsageNormalization` (donc non neutralisés).

**Modifiés**
- `src/Chronos/Services/ClaudeUsageObjectProvider.cs` — `ReadWindow` prend `DateTimeOffset? capturedAt` en
  5ᵉ paramètre ; `CapturedAt = capturedAt` ; XML-doc imposée décrivant le piège **sans reproduire
  l'expression fautive**.
- `src/Chronos/Services/ChronosOAuthUsageProvider.cs` — `Read` prend `DateTimeOffset capturedAt` ; les deux
  appels passent `now` (l'instant déjà utilisé pour `SourceCapturedAt`).
- `src/Chronos/Services/ClaudeOAuthUsageProvider.cs` — transformation identique.
- `src/Chronos/Services/LastExactStore.cs` — `Convertir` à trois conditions ; `UnExactADejaEteObtenu()`
  ajoutée après `Load`. `SchemaVersion` **reste 1**, aucun `[JsonPropertyName]` touché.
- `tests/Chronos.Tests/ClaudeUsageObjectProviderTests.cs` — +2 tests.
- `tests/Chronos.Tests/LastExactStoreTests.cs` — +4 tests. Helper `Exact` **non modifié**.

## INVENTAIRE RÉEL des tests impactés par l'ajout de `CapturedAt`

C'est le livrable principal de la tâche 1 : **mesurer au lieu de supposer**. Suite complète exécutée avant
et après la modification des trois providers.

| Mesure | Total | Échecs |
|---|---|---|
| Baseline, avant toute modification | **652** | 0 |
| Après tâche 1 (2 nouveaux tests) | **654** | 0 |
| Après tâche 3 (suite complète finale) | **664** | 0 |

**Liste nominative des tests cassés par l'ajout de `CapturedAt` : AUCUN. La liste est VIDE.**

C'est une mesure, pas une hypothèse. La crainte du plan — « un `Assert.Equal(attenduWindowState, obtenu)`
comparant des **records entiers** casserait » — ne s'est matérialisée **nulle part** : aucun des ~120 tests
des quatre providers ne compare un `WindowState` entier. Ils assertent champ par champ. Le coût de la
vérification a été de 2 min 10 ; il valait mieux que la journée d'hypothèses qu'il a évitée.

**Décompte exact de la variation 652 → 664 (+12), entièrement nominatif :**

| Tâche | Fichier | Tests ajoutés |
|---|---|---|
| 1 | `ClaudeUsageObjectProviderTests` | `Un_usage_json_vieux_de_deux_mois_porte_l_age_du_FICHIER_et_non_celui_de_la_lecture`, `Un_usage_json_sans_capturedAt_laisse_l_age_INCONNU` (**+2**) |
| 2 | `LastExactStoreTests` | `Aucun_fichier_signifie_qu_aucun_exact_n_a_JAMAIS_ete_obtenu`, `Un_releve_dont_la_fenetre_a_roule_prouve_tout_de_meme_qu_un_exact_a_ete_obtenu`, `Un_fichier_illisible_ne_permet_d_affirmer_aucun_exact_obtenu`, `Une_fenetre_exacte_SANS_instant_de_capture_n_est_pas_persistee` (**+4**) |
| 3 | `SourceActiviteMemoiseeTests` | `Deux_lectures_rapprochees_ne_paient_QU_UNE_passe_disque`, `Une_lecture_apres_peremption_repaie_une_passe`, `La_validite_se_mesure_sur_l_instant_de_la_passe_disque`, `Une_panne_de_la_source_rend_le_dernier_journal_connu`, `Une_panne_des_la_premiere_lecture_remonte_a_l_appelant`, `La_validite_par_defaut_est_alignee_sur_le_tick_nominal` (**+6**) |

652 + 2 + 4 + 6 = **664**. Aucune perte de couverture nette, aucun test supprimé, aucun test ignoré.

## Décision : `SourceActiviteMemoisee` n'est PAS câblée dans ce plan

`grep -c "SourceActiviteMemoisee" src/Chronos/App.xaml.cs` rend **0**, et c'est **délibéré**.

L'enregistrement DI est le plan **19-03**, où il sera fait **en un seul commit avec le consommateur** (le
4ᵉ argument du décorateur). Le précédent qui fonde ce choix est 17-03 : *« un service que le host ne démarre
jamais ne fait rien »*. Enregistrer ici un décorateur qu'aucun code n'interroge produirait l'illusion d'une
optimisation active alors qu'aucune passe de transcript ne serait mémoïsée — et la garde de composition
(`CompositionRootTests`) attesterait d'un câblage sans effet.

Le type est donc livré **prouvé** (6 tests) et **neutre** (0 occurrence de `System.Windows`,
`ServicesLayerPurityTests` vert), mais inerte.

## Décisions Made

1. **L'horodatage appartient à la source, pas au lecteur.** Les trois providers ont reçu le même paramètre
   mais **pas la même valeur** : `now` pour les deux OAuth (où la lecture *est* la capture, et où le cache
   sert l'instance d'origine, donc son horodatage d'origine vieillit honnêtement), et le `capturedAt` du
   fichier pour `ClaudeUsageObjectProvider`. Une signature commune ne signifie pas une sémantique commune.
2. **Le commentaire décrit le piège sans l'écrire.** La XML-doc de `ReadWindow` explique en toutes lettres
   pourquoi l'instant de lecture serait un mensonge, mais ne contient nulle part l'expression fautive
   `CapturedAt = _clock…` — sans quoi le critère de non-retour par grep serait mort-né, et une garde qui ne
   peut pas échouer ne garde rien (précédent 18-01).
3. **`usage-partial.json` était la bonne cible, vérifié avant écriture.** Le plan prévoyait de créer
   `usage-sans-capture.json` *si* la fixture existante portait un `capturedAt`. Lecture faite : elle vaut
   `{ "five_hour": { "used_percentage": 88.0 } }` — aucun `capturedAt`, aucune `seven_day`. La fixture de
   repli n'a donc pas été créée : elle aurait été une seconde fixture redondante.
4. **Le mémoïseur n'invente jamais un journal vide.** Une panne dès la première lecture laisse l'exception
   remonter. Rendre un `TranscriptActivityLog` vide se lirait « aucune activité » — ce qui est une
   **affirmation**, pas une absence de réponse. La conversion en branche « indisponible » appartient à
   l'appelant (19-03), qui seul sait ce que l'absence de réponse implique pour l'utilisateur.
5. **`SchemaVersion` reste 1.** `UnExactADejaEteObtenu` est une **lecture de plus**, pas un champ de plus :
   les trois fixtures qui épinglent `"version":1` / `"version":999` restent valides sans retouche.

## Deviations from Plan

**Aucune.** Le plan a été exécuté exactement comme écrit.

Deux points où le plan laissait un choix conditionnel ont été tranchés par **lecture préalable du réel**,
comme il l'exigeait :

- fixture sans `capturedAt` : `usage-partial.json` convenait, pas de fixture supplémentaire créée ;
- inventaire des tests cassés : mesuré à zéro.

Une précision de comptage, sans impact : le plan annonçait « les 15 appels » du helper `Exact` et
« 20 tests existants » dans `LastExactStoreTests` ; la mesure donne **13 tests existants** dans cette
classe. La conclusion du plan restait juste (tous les appels d'`Exact` passent un `captured` non-null, donc
durcir `Convertir` ne casse rien) — et elle est confirmée : 0 échec.

**Total deviations:** 0
**Impact on plan:** aucun. Aucun scope creep, aucun fichier hors des 10 listés au frontmatter du plan.

## Issues Encountered

Aucune. Les trois étapes RED ont produit des échecs **comportementaux** et non de compilation :

| Étape RED | Échecs / total |
|---|---|
| Tâche 1 | 1 / 6 (seul le test de l'âge du fichier échoue ; celui de l'âge inconnu passait déjà, `CapturedAt` valant `null` par défaut) |
| Tâche 2 | 4 / 17 |
| Tâche 3 | 5 / 6 |

Les tâches 2 et 3 ont utilisé un **squelette compilable** (`NotImplementedException`) pour l'étape RED,
conformément au précédent 18-01 et à la contrainte de compilabilité à chaque commit : `tests/Chronos.Tests`
a un `ProjectReference` vers `Chronos`, un commit non compilable rendrait `dotnet test` non invocable dans
son intégralité.

## Vérifications de sécurité et de non-régression

| Contrôle | Attendu | Obtenu |
|---|---|---|
| `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` (avant ET après) | `518 1783863147` | `518 1783863147` ✅ |
| Fichiers SOURCE contenant `RefreshAsync` | 2 | 2 ✅ |
| `api.anthropic.com` dans `SourceActiviteMemoiseeTests.cs` | vide | vide ✅ |
| Écriture de test sous le vrai `%APPDATA%\Chronos\` | aucune | aucune (tout sous `Path.GetTempPath()`) ✅ |
| `ServicesLayerPurityTests` + `NormalisationUniqueTests` + `CompositionRootTests` | 0 échec | 10/10 ✅ |
| `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` (garde phase 18) | 0 échec | 1/1 ✅ |
| `required` dans `WindowState.cs` | 2 | 2 ✅ (aucun champ nouveau, `CapturedAt` préexistait) |
| Signature de `DiagnosticService` | inchangée | inchangée ✅ (fichier non touché) |
| `grep -cE "CapturedAt\s*=\s*_clock"` dans `ClaudeUsageObjectProvider.cs` | 0 | 0 ✅ |
| `grep -c "public const int SchemaVersion = 1;"` | 1 | 1 ✅ |
| `grep -cE "w\.CapturedAt is \{ \} c"` | 1 | 1 ✅ |
| `JsonPropertyName` dans le diff de `LastExactStore.cs` | aucune ligne | aucune ✅ |
| `grep -c "SourceActiviteMemoisee" src/Chronos/App.xaml.cs` | 0 | 0 ✅ (volontaire) |
| `grep -c "System.Windows"` dans le mémoïseur | 0 | 0 ✅ |
| Suite complète `dotnet test Chronos.sln -v q --nologo` | 0 échec | **664 / 664, 0 échec** ✅ |

## Statut des requirements

**EXA-02 et EXA-05 restent PENDING.** Ce plan livre les **faits** qu'ils exigent, pas la **doctrine** qui
les applique :

- **EXA-02** (limite d'âge) : l'horodatage par fenêtre existe désormais pour les quatre providers exacts,
  donc la limite d'âge **peut** être écrite. Elle ne l'est pas : aucun `DoctrineFraicheur` n'existe encore,
  et rien ne rejette aujourd'hui un relevé trop vieux. Sera coché par le plan **19-03**.
- **EXA-05** (distinguer « jamais connecté » de « fenêtre roulée ») : `UnExactADejaEteObtenu()` existe et
  est prouvé, mais **aucun appelant** ne le consulte. Sera coché par le plan **19-04**.

Cocher ces requirements maintenant affirmerait un comportement que l'application n'a pas — exactement
l'erreur que le précédent 17-01 a nommée.

## Next Phase Readiness

Le plan **19-02** peut démarrer sans dépendance non satisfaite. Ce plan n'a touché **ni**
`LastExactUsageProvider`, **ni** `CompositeUsageProvider`, **ni** `App.xaml.cs`, **ni** aucun ViewModel ni
XAML — la surface reste exactement celle que 19-02 à 19-05 attendent.

Points d'attention pour la suite :

- **19-03** doit enregistrer `SourceActiviteMemoisee` dans le graphe DI **dans le même commit** que son
  consommateur, et ajouter la garde `CompositionRootTests` correspondante.
- **19-03** doit convertir l'`IOException` du mémoïseur (panne sans journal connu) en branche
  « indisponible » : le décorateur ne le fait délibérément pas.
- La durée de validité (60 s) est **aussi une tolérance de certification** : un « aucune activité » fondé
  sur un journal de 45 s n'est valable qu'à 45 s près. Elle ne doit pas être allongée par confort de
  performance — c'est écrit dans la XML-doc du type.
- Hors périmètre de cette phase, non anticipé ici : EXA-03 et EXA-06 (phase 20), trou de la `UniformGrid`
  des réglages, dette de durée des tests.

---
*Phase: 19-nouvelle-doctrine-du-composite*
*Completed: 2026-09-12*

## Self-Check: PASSED

9 / 9 fichiers déclarés présents sur disque. 6 / 6 commits de tâche présents dans l'historique git.
Suite complète : 664 / 664, 0 échec. Coffre `oauth.dat` inchangé (518 o, mtime 1783863147).
