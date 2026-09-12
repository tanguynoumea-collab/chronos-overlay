---
phase: 20-honn-tet-visible-cadran-diagnostic
plan: 05
subsystem: diagnostic
tags: [exa-06, exa-03, porte-de-phase, milestone-v1.5, tdd-red-green, vocabulaire-unique, checkpoint-substitue]

# Dependency graph
requires:
  - phase: 20
    plan: 02
    provides: "Chronos.Text.LibelleSource — Format / Anciennete / Provenance, le vocabulaire FR unique, livré sans consommateur"
  - phase: 20
    plan: 04
    provides: "L'infobulle de la pastille d'âge : le PREMIER consommateur de LibelleSource, côté cadran"
  - phase: 19
    plan: 04
    provides: "La doctrine du « ≥ » — l'incertitude d'un plancher est UNILATÉRALE, jamais un tilde"
  - phase: 20
    plan: 01
    provides: "IInventaireMachine — les 11 DiagnosticServiceTests coûtent 534 ms au lieu de 2 min 8 s"
provides:
  - "Le rapport de diagnostic rend le COUPLE (QUI alimente cette fenêtre, DEPUIS QUAND) — EXA-06 refermée des deux côtés"
  - "Un seul vocabulaire dans tout le projet pour dire « borne inférieure » : « ≥ », au cadran comme au rapport"
  - "La porte de la phase 20 franchie sur preuves nommées : 748/748 deux fois, 5 gardes, comptage réconcilié nominativement depuis 699"
  - "Le protocole humain CONSOLIDÉ du milestone v1.5 — les huit constats hérités des phases 17, 18, 19 et 20 en une seule liste ordonnée"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Un vocabulaire d'interface vit à UN endroit et se consomme partout : LibelleSource est désormais lu par le ViewModel (infobulle) ET par le service de diagnostic (rapport) — l'utilisateur ne peut plus lire deux noms différents pour la même source selon l'endroit où il regarde"
    - "Une méthode statique devient méthode d'instance quand elle doit dater quelque chose : l'ancienneté se mesure contre l'horloge INJECTÉE, jamais contre DateTimeOffset.UtcNow"
    - "Une assertion d'ABSENCE se restreint à la ligne qu'elle prouve : DoesNotContain(« ~ ») sur le rapport entier serait rouge pour « Dossier ~/.claude/projects », donc pour une raison étrangère — on restreint la portée plutôt que d'affaiblir la preuve"
    - "Un point d'arrêt humain qui touche la machine réelle se SUBSTITUE en protocole écrit, jamais en constat simulé (3e application après 19-05 et 18-06)"

key-files:
  created: []
  modified:
    - src/Chronos/Services/DiagnosticService.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - .planning/phases/20-honn-tet-visible-cadran-diagnostic/20-VALIDATION.md
    - .planning/REQUIREMENTS.md
    - .planning/ROADMAP.md

key-decisions:
  - "Le tilde du diagnostic était un OUBLI de la phase 19 et non une conservation volontaire : il est mort. Le « ~ » dirait « autour de 42 », donc autoriserait « peut-être 38 » — or on SAIT que le quota vaut au moins 42 %, c'est la borne supérieure qui est inconnue"
  - "Les deux segments d'EXA-06 (source, ancienneté) sont INCONDITIONNELS : une fenêtre que personne n'alimente doit dire « non renseignée », pas se taire — un silence se lit comme « rien à signaler »"
  - "La provenance, elle, reste CONDITIONNELLE : null veut dire « la doctrine ne s'est pas prononcée », et mieux vaut ne rien dire que qualifier un chiffre sur lequel personne n'a statué"
  - "HDR-02 reste Complete au sens du code et des tests, mais N'EST PAS déclarée prouvée en production : le 429 réel exige une saturation du compte, qui n'est pas atteignable ici. La nuance est écrite plutôt que cochée en silence"
  - "La tâche 3 est substituée et non exécutée : lancer l'overlay purgerait les 25 groupes de hooks de ~/.claude/settings.json sans supervision, et le parcours de reconnexion exige l'authentification de l'utilisateur"

patterns-established:
  - "Un comptage de porte de phase se réconcilie NOMINATIVEMENT et jamais par une borne (« ≥ 699 ») : la phase supprime deux tests, tous deux nommés, tous deux remplacés par une garde de niveau supérieur"

requirements-completed: [EXA-06]

# Metrics
duration: 14min
completed: 2026-09-12
---

# Phase 20 Plan 05 : diagnostic parlant, porte de phase, et protocole humain consolidé Summary

**Le rapport de diagnostic dit enfin QUI alimente chaque fenêtre et DEPUIS QUAND — dans le même français
que le cadran — et la porte du dernier plan du milestone v1.5 est franchie sur des preuves nommées :
748/748 deux fois, cinq gardes vertes, un comptage réconcilié test par test depuis 699, et huit constats
visuels DÉLÉGUÉS à l'utilisateur plutôt que simulés.**

## Performance

- **Duration:** 14 min
- **Started:** 2026-09-12T07:17:00Z
- **Completed:** 2026-09-12T07:31:00Z
- **Tasks:** 2 exécutées / 3 — la 3ᵉ est un point d'arrêt humain, **substitué** (voir plus bas)
- **Files modified:** 5 (0 créé)

## Bilan de la suite : chaque écart nominatif

| | Tests | Durée |
|---|---|---|
| Entrée (commit `d7aacf0`) | 744 | 2 s |
| Sortie (commit `b235b74`) | **748** | **3 s** |

**Deux exécutions consécutives de la suite complète**
(`dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q`) :

| Passe | Total | Échecs | Durée runner |
|---|---|---|---|
| 1 | **748** | **0** | **3 s** |
| 2 | **748** | **0** | **3 s** |

Même total, même durée. Le précédent BAML 16-03 (course du chargeur XAML sous parallélisme xUnit,
indétectable par relance isolée) est donc levé pour cette phase, qui a ajouté onze `[WpfFact]`.

### Les cinq gardes permanentes, chacune par sa commande

| Garde | Tests | Résultat |
|---|---|---|
| `--filter "FullyQualifiedName~ServicesLayerPurityTests"` | 2 | ✅ 12 ms |
| `--filter "FullyQualifiedName~CompositionRootTests"` | 5 | ✅ 349 ms |
| `--filter "FullyQualifiedName~NormalisationUniqueTests"` | 3 | ✅ 13 ms |
| `--filter "FullyQualifiedName~GardesDoctrineTests"` | 8 | ✅ 31 ms |
| `--filter "FullyQualifiedName~La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte"` | 1 | ✅ 97 ms |

**19 tests de garde**, tous verts. `NormalisationUniqueTests` est celle qui comptait ici : elle balaie le
TEXTE SOURCE et échoue sur tout `/ 100` posé hors `UsageNormalization` — la nouvelle `Describe` n'en pose
aucun.

### Comptage réconcilié NOMINATIVEMENT depuis la baseline 699

| Plan | Écart | Nom du test ou de la classe | Cumul |
|---|---|---|---|
| — | baseline | fin de phase 19 (`71413c1`) | **699** |
| 20-01 | **+1** | `ReglagesBindingTests.Le_bouton_Diagnostic_occupe_toute_la_largeur_et_aucun_libelle_n_est_tronque` | 700 |
| 20-02 | +3 | `GardesDoctrineTests` — gardes de source et de composite | |
| 20-02 | +2 | `RateLimitHeaderUsageProviderTests` — nommage par la sonde (nominal + « dépassement seul ») | |
| 20-02 | +1 | `LastExactStoreTests` — nommage par le magasin au rechargement | |
| 20-02 | +18 | `LibelleSourceTests` — vocabulaire FR (dont un `[Theory]` que xUnit compte 3) | |
| 20-02 | +1 | `GardesDoctrineTests.Aucun_champ_nomme_EstimatedTokens_ne_reapparait_dans_Models_ni_Services` | |
| 20-02 | **−1** | `CadranBindingTests.EstimatedTokens_seul_ne_surface_PLUS_rien_au_cadran` — **SUPPRIMÉ** | **724** |
| 20-03 | **−1** | `MainViewModelTests.IsStale_vrai_quand_la_capture_depasse_deux_minutes` — **SUPPRIMÉ** | |
| 20-03 | +3 | `WindowGaugeViewModelTests` — `EstDate`, composition `plancher ⊂ daté`, couple (qui, depuis quand) | |
| 20-03 | +1 | `GardesDoctrineTests` — « aucun seuil d'ancienneté hors doctrine » | |
| 20-03 | +6 | `MainViewModelTests` — marque d'âge globale (3) + infobulle (3) | **733** |
| 20-04 | +11 | `CadranBindingTests` — pointillé (5), rangée et géométrie (3), mot « indisponible » (3) | **744** |
| 20-05 | +4 | `DiagnosticServiceTests` — source, ancienneté, source absente, plancher « ≥ » | **748** |

`699 + 1 + 24 + 9 + 11 + 4 = 748`. **Le critère de sortie n'est pas « ≥ 699 »** : cette phase SUPPRIME
deux tests, tous deux nommés ci-dessus, tous deux remplacés par une garde de niveau supérieur — un test de
COMPORTEMENT d'un champ mort devient une garde de STRUCTURE qui interdit son retour, et un test de seuil
d'ancienneté concurrent devient une garde qui interdit tout seuil hors doctrine. Une borne aurait masqué
ces deux disparitions.

Comptage par classe, vérifié à l'unité sur `--list-tests` : `DiagnosticServiceTests` **11** (7 + 4),
`CadranBindingTests` **24**, `MainViewModelTests` **47**, `LibelleSourceTests` **18**,
`GardesDoctrineTests` **8**. Aucune autre classe n'a bougé dans ce plan.

## Falsifiabilité : TDD RED/GREEN RÉEL, deuxième fois du milestone

Comme en 20-04, ce plan ne crée **aucun symbole de production nouveau** — `LibelleSource` existe depuis
20-02, `WindowState.Source` et `CapturedAt` depuis 20-02 et 19-01. Les tests compilent donc AVANT
l'implémentation, et l'étape ROUGE est un vrai commit exécutable.

| Étape | Commit | Constat |
|---|---|---|
| **ROUGE** | `ebeb0ea` | **6 échecs / 11 tests / 482 ms** — les 4 nouveaux + les 2 tests dont l'assertion `« estimé »` a été réécrite |
| **VERT** | `b6e03e7` | **14/14 / 534 ms** (`DiagnosticServiceTests` + `NormalisationUniqueTests`) |

Le rouge est instructif à deux titres : les **5 tests restants passent déjà au rouge**, ce qui prouve que
les nouvelles assertions ne cassent rien par effet de bord ; et les **2 tests réécrits** échouent pour la
bonne raison — le mot « estimé » n'existe plus nulle part dans le rapport.

## Accomplishments

### Task 1 — le rapport nomme la source et son ancienneté (EXA-06)

Deux défauts corrigés d'un coup, parce qu'ils vivaient dans la même méthode.

**1. Le diagnostic disait encore « ~ » là où le cadran dit « ≥ » depuis la phase 19.** Deux vocabulaires
pour le même fait, et le tilde véhicule une incertitude **symétrique** que 19-04 a explicitement rejetée :
« ~ 42 % » autorise la lecture « peut-être 38 », alors qu'un plancher est une borne **inférieure** — on
SAIT que le quota consommé vaut au moins 42 %, c'est la borne supérieure qui est inconnue. Ce tilde-là
était un **oubli**, à la différence de la surcharge `PercentFormatter.Format(double?, bool)` qui sert la
galerie et reste.

**2. Le rapport ne disait ni QUI alimente la fenêtre, ni DEPUIS QUAND** — littéralement l'énoncé d'EXA-06.

Le rendu passe de :

```
5 h   : estimé — ~42 %
```

à :

```
5 h   : PLANCHER — ≥ 42 % · source : dernier exact persisté · relevé il y a 12 min · borne inférieure (activité depuis)
```

- **`Describe` devient méthode d'INSTANCE** (perte du `static`) : l'ancienneté se mesure contre `_clock`,
  l'horloge injectée, et jamais contre `DateTimeOffset.UtcNow` — sans quoi aucun palier ne serait
  déterministe en test. Les deux sites d'appel (l. 368-369) sont dans la même instance : **leur texte n'a
  pas changé d'un caractère**.
- **`Chronos.Text.LibelleSource` est consommé, pas réécrit** : trois appels (`Format`, `Anciennete`,
  `Provenance`), zéro mapping FR local. Le vocabulaire livré en 20-02 avait **un** consommateur (l'infobulle
  du cadran, 20-04) ; il en a maintenant **deux**, et ils disent forcément la même chose.
- **Les deux segments d'EXA-06 sont inconditionnels.** Une fenêtre sans source se dit « non renseignée »,
  une fenêtre sans horodatage se dit « relevé de date inconnue ». Se taire serait pire : un silence se lit
  comme « rien à signaler », et c'est exactement le mensonge de deux mois que le milestone corrige.
- **La provenance reste conditionnelle** : `null` veut dire « la doctrine ne s'est pas prononcée », et
  `LibelleSource.Provenance` rend alors la chaîne vide — aucun segment n'est ajouté.
- **Les suffixes préexistants sont rendus mot pour mot** : `· serveur : REJETÉ` et `· dépassement 34 %`
  sont assertés littéralement par les tests de la phase 18, et passent sans retouche.
- **Aucune conversion d'unité locale** : tout passe par `UsageNormalization`. `NormalisationUniqueTests`
  reste vert.

Les quatre nouveaux tests, et ce qu'ils verrouillent :

| Test | Ce qu'il empêche de régresser |
|---|---|
| `Le_rapport_nomme_la_SOURCE_qui_alimente_chaque_fenetre` | Que les deux fenêtres soient attribuées à la même source alors que le composite choisit PAR FENÊTRE |
| `Le_rapport_donne_l_ANCIENNETE_du_releve_de_chaque_fenetre` | Qu'un chiffre exact soit affiché sans son âge — la panne de deux mois, exactement |
| `Une_source_absente_se_dit_non_renseignee_et_JAMAIS_un_nom_par_defaut` | Qu'une absence produise une affirmation |
| `Un_plancher_est_decrit_avec_superieur_ou_egal_et_sa_provenance` | Le retour du tilde et de son incertitude symétrique |

### Task 2 — la porte de phase, franchie sur preuves

- **Suite ×2, cinq gardes, comptage nominatif** : détaillés plus haut et reportés dans `20-VALIDATION.md`.
- **`20-VALIDATION.md` rempli** : `status: planned` → `status: validated`, `nyquist_compliant: true`,
  `wave_0_complete: true`, relevé de porte **daté du 2026-09-12**, carte des 15 tâches remplie avec la
  commande RÉELLE et le résultat MESURÉ de chacune, 3 cases de « Wave 0 Requirements » et 6 de
  « Validation Sign-Off » cochées.
- **La ligne 20-05 T3 conserve son statut DISTINCT `⏳ à vérifier par l'utilisateur`** — ni ✅ ni ⬜. Une
  porte qui se déclarerait verte sur une preuve qu'elle n'a pas produite serait exactement le défaut que
  ce milestone corrige.
- **Six vérifications héritées ajoutées à la section « Manual-Only »**, nominativement rattachées à leur
  phase et à leur exigence (17 : TOK-02, TOK-03 · 18 : HDR-02, HDR-06 · 19 : EXA-02 · 20 : EXA-03).
- **EXA-06 cochée** dans les deux endroits de `REQUIREMENTS.md` (liste et table de traçabilité).
  **EXA-03 l'était déjà** depuis 20-04, qui l'a cochée en livrant les pixels. Rien d'autre n'a été touché :
  les exigences des phases 15 à 19 sont closes et y toucher contredirait des décisions documentées.
- **`ROADMAP.md` : Phase 20 à `5/5 · Complete · 2026-09-12`**, et la ligne à puces du milestone cochée.

**Deux nuances portées par le sign-off plutôt que cochées en silence :**

1. « Les trois marques sont distinguables » est coché **au sens du code et des tests de géométrie**. Le
   jugement « à distance de lecture, sur fond d'écran réel » reste `⏳`.
2. « Les pastilles sont mutuellement exclusives » a été **reformulé** : 20-04 a établi qu'elles ne le sont
   pas et n'ont pas à l'être — elles disent des choses différentes (« le wifi est coupé » / « on n'a jamais
   rien obtenu ») et peuvent légitimement coexister. C'est le **recouvrement** qui était le défaut, et il
   est mort par la STRUCTURE. Cocher la formulation d'origine aurait certifié une propriété fausse.

## État honnête des 24 exigences du MILESTONE v1.5

| Groupe | Exigences | État | Nuance |
|---|---|---|---|
| EXA | 01, 02, 04, 05 | ✅ closes (phases 16 et 19) | — |
| EXA | **03** | ✅ close (20-04) | La part **jugement visuel** (« distinguables à distance, sur fond réel ») est ⏳ — point 1 et 2 du protocole |
| EXA | **06** | ✅ **close ici** | Les deux moitiés : infobulle du cadran (20-04) **et** rapport de diagnostic (20-05) |
| DEL | 01 → 06 | ✅ closes (phases 16 et 19) | — |
| HDR | 01, 03, 04, 05, 06 | ✅ closes (phase 18) | HDR-06 : la LISTE des en-têtes réellement servis par `/api/oauth/usage` reste ⏳ (point 7 du protocole) |
| HDR | **02** | ✅ close **au sens du code et des tests** | ⚠️ **NON prouvée en production.** Le comportement sur 429 est verrouillé par `RateLimitHeaderUsageProviderTests` sur des réponses fabriquées ; qu'un 429 **RÉEL** porte bien la famille `anthropic-ratelimit-unified-*` ne peut être constaté que sur une saturation réelle du compte. Le marqueur `human_needed` posé par la vérification de phase 18 (`ad3f60a`) **reste ouvert** |
| TOK | 01, 02, 03 | ✅ closes (phase 17) | Le **parcours** de reconnexion de bout en bout et le **contraste** des pastilles restent ⏳ (points 8 et 9) |
| PUR | 01, 02, 03 | ✅ closes (phase 15) | Effet constatable seulement **au premier lancement** : les 25 groupes de hooks tomberont à 5 |

**Couverture v1.5 : 24 / 24 exigences implémentées et testées.** Aucune n'est cochée sur la foi d'un
constat visuel absent ; **six vérifications humaines restent ouvertes**, toutes identifiées comme telles.

### Task 3 — SUBSTITUÉE, et pourquoi

Le plan prévoyait un `checkpoint:human-verify` bloquant. **Il n'a pas été exécuté, et il n'a pas non plus
été simulé.** Deux raisons dures, toutes deux portant sur la machine RÉELLE de l'utilisateur :

1. **Lancer l'overlay déclencherait la réconciliation de la phase 15**, qui purge les entrées Chronos de
   `~/.claude/settings.json` — **25 groupes de hooks** aujourd'hui au lieu de 5 (constaté, voir plus bas).
   C'est le comportement voulu, mais il doit se produire sous la supervision de son propriétaire.
2. **Les points 6 à 9 du protocole exigent l'authentification de l'utilisateur** (reconnexion OAuth, appel
   réel à `/api/oauth/usage`, saturation réelle du compte). Aucun agent ne peut les produire.

**Ce qui A ÉTÉ vérifié automatiquement, en remplacement :**

| Contrôle | Résultat |
|---|---|
| L'agent n'a lancé NI `dotnet publish` NI l'exe | ✅ `git status --short` vide ; l'exe de `bin/Release/.../publish/` date du **2026-07-12 16:22 UTC**, soit 64 jours, inchangé |
| `%APPDATA%\Chronos\oauth.dat` | ✅ **518 1783863147** — identique avant et après |
| `~/.claude/settings.json` | ✅ **6872 1785403369** — identique avant et après |
| Groupes de hooks Chronos | **25** — au-dessus du seuil de 5, **comme attendu AVANT** le premier lancement. La commande `<verify>` du plan est une **post-condition du constat de l'utilisateur**, pas une précondition de l'agent |
| **Prédiction outillée de la bascule (phase 19)** | ✅ voir ci-dessous |

**La bascule de la phase 19, prédite par la mesure et non par la lecture de l'overlay :**

| Fait mesuré | Valeur réelle sur cette machine |
|---|---|
| `%APPDATA%\Chronos\usage.json` | `{"five_hour":{"used_percentage":10,"resets_at":9},"capturedAt":1783666519131}` |
| `capturedAt` | **2026-07-10 06:55:19 UTC** — soit **64 jours** d'ancienneté |
| `DoctrineFraicheur.LimiteAge` | **6 min** (`CadenceNominale` 300 s + 60 s) |
| Branche 1 (`age <= LimiteAge` → `Exact`) | **fausse** — 64 j ≫ 6 min |
| `journal.Covers(T)` avec `T = 2026-07-10` | **faux** — l'horizon des transcripts est de 8 jours |
| Branches 2 et 3 (encore valide / plancher) | **inatteignables** ⇒ `Indisponible` |
| `%APPDATA%\Chronos\last-exact.json` | **ABSENT** ⇒ `UnExactADejaEteObtenu = false` |
| Conséquence (EXA-05) | **aucun pourcentage**, mot « indisponible » + **invitation à se connecter allumée** |
| `resets_at: 9` | epoch 9 < `PlancherEpoch` (2020-01-01) ⇒ instant **inconnu**, aucune géométrie fabriquée |

Autrement dit : **le « 10 % » figé depuis deux mois ne peut plus s'afficher**, et c'est démontré par le
code et les données réelles, sans lancer quoi que ce soit. Reste à le **voir**.

---

## À VÉRIFIER PAR L'UTILISATEUR

**Protocole consolidé du milestone v1.5 — les phases 17, 18, 19 et 20 en une seule passe.**
Neuf points, dans l'ordre où il faut les faire. Rien ici n'a été constaté par l'agent.

### ⚠️ 0. Sauvegarde — AVANT tout lancement, et sans exception

Le premier lancement de Chronos déclenchera la réconciliation de la phase 15, qui **purgera les entrées
Chronos de `~/.claude/settings.json`** : **25 groupes de hooks** y sont présents aujourd'hui au lieu de 5.
C'est le comportement voulu (PUR-01/PUR-03), il crée une **sauvegarde horodatée** tout seul — mais autant
que vous ayez la vôtre, et que vous sachiez que cela va se produire :

```
cp ~/.claude/settings.json ~/.claude/settings.json.avant-phase20
```

### 1. Republier — sinon vous ne verrez RIEN de tout cela

L'exe déployé est **`~/Downloads/Chronos-v2.8.1.exe`**, et celui de `bin/Release/.../publish/` date du
**2026-07-12** : tous deux portent le comportement d'**avant la phase 15**. Les phases 15 à 20 du milestone
v1.5 n'y sont pas.

```
dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true
```

puis lancer l'exe produit sous `bin/Release/net8.0-windows/win-x64/publish/`.

> **Note de release** (votre doctrine, hors périmètre de ce plan) : la version du `.csproj` est encore
> **2.8.1**, celle de l'exe déjà déployé. Si vous comptez garder ce build, il faudra la monter et
> republier sous `Chronos-vX.Y.exe`, sans quoi deux binaires très différents porteront le même numéro.

---

### — PHASE 20 : ce que ce cycle vient de dessiner —

**2. Le pointillé du style Anneaux — jamais rendu à l'écran, et non mesurable ici**

`StrokeDashArray` sur `RingArc`, aux rayons 38 / 52 / 54. Valeur `0.55 0.45`, en **multiples de
l'épaisseur** : 4,4 px de tiret / 3,6 px d'espace sur les arcs de 8 px, 3,85 / 3,15 px sur celui de 7 px du
mode Normal.

- Le pointillé se distingue-t-il du trait plein **à distance de lecture normale**, sans zoomer ?
- Est-il lisible **sans être bruyant** ?

Si les tirets paraissent trop longs (anneau « cassé ») ou trop fins (invisibles) : dites-le, l'ajustement
portera sur `0.55 0.45`, **en multiples de `StrokeThickness` et jamais en pixels**, et
`Le_plancher_hebdo_rend_l_arc_des_Anneaux_en_pointille` devra rester vert après correction.

**3. Les quatre marques, sur votre fond d'écran réel**

Balayer ≈ 10 combinaisons représentatives : les **5 styles** × les **2 modes**, et 2 ou 3 des **9 thèmes**
dont un clair.

- Le cas nominal (exact frais) porte-t-il bien **aucune** marque ?
- `PastilleReleveDate` — l'**anneau creux gris** de 12 px — se distingue-t-elle d'un coup d'œil des
  pastilles **pleines** voisines ?
- Le mot « indisponible » (bas-gauche) est-il lisible sans être envahissant ?

> ⚠️ Défaut connu et **hors périmètre** : les 4 `UserControl` de styles alternatifs codent leurs couleurs
> en dur. Sur un thème clair, leurs textes internes sont déjà peu lisibles, indépendamment de cette phase.

**4. La rangée n'empiète sur aucun anneau**

Dans les 9 thèmes, et surtout en **mode Étendu** (3 anneaux, le plus dense). Mesures attendues : la
pastille la plus à gauche est à **75,6 px** du centre (seuil 71,5), le mot « indisponible » à **86,9 px**,
et il reste **81,3 px** entre le bord droit du mot et le bord gauche de la rangée.

**5. L'infobulle de la pastille d'âge**

Survoler l'anneau creux : elle doit nommer **la source et l'ancienneté des DEUX fenêtres**, dans le même
français que le rapport de diagnostic.

**6. Le diagnostic — clic droit → Diagnostic…**

La ligne « 5 h » doit avoir cette forme :

```
5 h   : EXACT — 42 % · source : sonde d'en-têtes de rate-limit · relevé il y a 12 min · frais
```

- La source nommée est-elle **plausible** ?
- L'ancienneté correspond-elle à ce que vous savez de votre usage ?

> Si la source affichée n'est pas celle que vous attendiez, **c'est une information, pas un bug** — c'est
> exactement le service que rend EXA-06, et ce qui a manqué pendant deux mois.

---

### — VÉRIFICATIONS HÉRITÉES DU MILESTONE, à solder ici —

**7. Phase 19 — la bascule visuelle réelle** *(prédite par la mesure, à confirmer par l'œil)*

Le « 10 % » figé depuis le **10 juillet** doit avoir cédé la place. La prédiction outillée ci-dessus dit
**exactement** ce qui doit apparaître :

> **aucun pourcentage**, le mot **« indisponible »**, et la **pastille d'invitation à se connecter allumée**
> — parce que `last-exact.json` est absent, donc l'overlay n'a **jamais** obtenu de chiffre exact.

Si vous voyez encore « 10 % », c'est que l'exe n'a pas été republié (point 1).
Après reconnexion, les chiffres exacts doivent apparaître **et l'invitation disparaître**.

**8. Phase 18 (HDR-06) — quels en-têtes de limite `/api/oauth/usage` sert-il vraiment ?**

Le seul point de ce protocole qui peut **faire économiser du travail à la machine**. La sonde d'en-têtes
coûte **≈ 288 micro-requêtes par jour** (une toutes les 300 s). Si `/api/oauth/usage` — que Chronos appelle
**de toute façon** — porte déjà la famille `anthropic-ratelimit-unified-*` dans ses en-têtes de réponse,
**la sonde devient redondante et son coût peut être supprimé**.

Une fois **connecté**, relever la liste des noms d'en-têtes de limite de la réponse, par exemple :

```
curl -sS -D - -o /dev/null https://api.anthropic.com/api/oauth/usage \
  -H "Authorization: Bearer <votre jeton>" | grep -i "ratelimit"
```

Rapporter **la liste des noms** (pas les valeurs). C'est un fait que personne ne peut obtenir autrement :
cette famille d'en-têtes **n'est documentée nulle part chez Anthropic**.

**9. Phase 18 (HDR-02) — le 429 RÉEL** *(non prouvé en production, et assumé comme tel)*

Si l'API vous renvoie un 429, le cadran doit continuer d'afficher des chiffres **exacts et à jour** au lieu
de basculer en « indisponible » : c'est l'instant où l'overlay sert le plus.

Ce cas est verrouillé par des tests **sur réponses fabriquées**, mais il n'a **jamais** été constaté sur une
saturation réelle. HDR-02 reste donc **non cochée « prouvée en production »**, et le restera tant que vous
n'aurez pas croisé un vrai 429. Le jour où cela arrive : ouvrez le diagnostic et regardez la ligne
`[Source exacte — sonde d'en-têtes de rate-limit]` — elle doit dire
**« 429 — en-têtes lus quand même, les chiffres restent exacts »** et non « 429 SANS en-tête unified ».

**10. Phase 17 — le parcours de reconnexion, de bout en bout**

- Si la pastille **ambre** apparaît : un clic doit relancer le login, et la pastille doit **disparaître dès
  qu'un chiffre exact revient** (rafraîchissement immédiat, pas au prochain tick).
- Vérifier aussi le **contraste des quatre pastilles** sur votre fond d'écran — ambre, gris, anneau creux,
  déconnexion.

> ⚠️ Le bouton à cliquer est `ReconnecterCommand` et **surtout pas** `LoginClaudeCommand`, qui bascule et
> supprimerait le coffre. C'est verrouillé dans le XAML par deux commentaires et deux `Assert.NotSame` —
> mais si un jour l'overlay vous demandait de vous reconnecter et effaçait `oauth.dat`, ce serait ce
> défaut-là, et il faudrait le dire tout de suite.

---

### Après la vérification

Comparer `~/.claude/settings.json` à votre sauvegarde :

```
node -e "const fs=require('fs'),os=require('os'),p=os.homedir()+'/.claude/settings.json';const j=JSON.parse(fs.readFileSync(p,'utf8'));console.log('hooks Chronos:',JSON.stringify(j).split('--hook').length-1)"
```

Attendu : **5** groupes de hooks Chronos (contre **25** aujourd'hui), et **toutes les entrées non-Chronos
intactes**. C'est le constat de PUR-01 / PUR-03, jamais observé en vrai jusqu'ici.

### Répondre

**« approuvé »** si les neuf points sont satisfaisants, ou décrire ce qui cloche — en particulier pour le
**point 2** (longueur et espacement du pointillé) et le **point 3** (lisibilité des marques), qui sont les
deux seuls arbitrages de cette phase que la conception n'a **pas pu** trancher par la mesure.

---

## Deviations from Plan

### Ajustements auto-appliqués

**1. [Rule 3 - Blocage] `Assert.DoesNotContain("~", report)` est impossible sur le rapport ENTIER**

- **Found during:** Task 1
- **Issue:** Le plan prescrit cette assertion, **en demandant explicitement de vérifier d'abord** que le
  rapport ne contient aucun autre tilde. Il en contient un : la ligne 360,
  `"  Dossier ~/.claude/projects : …"`, présente **depuis l'origine du fichier**. L'assertion aurait été
  rouge pour une raison étrangère à ce qu'elle prouve.
- **Fix:** Application de la porte de sortie prévue par le plan lui-même — l'assertion est **restreinte à
  la ligne « 5 h »**, extraite par un helper `LigneCinqHeures(report)`. La preuve est **restreinte**, pas
  affaiblie : le tilde interdit l'est là où il pourrait réapparaître.
- **Files modified:** `tests/Chronos.Tests/DiagnosticServiceTests.cs`
- **Commit:** `ebeb0ea`

**2. [Rule 3 - Blocage] Le commentaire des nouveaux tests faisait déborder `grep -c` de 11 à 12**

- **Found during:** Task 1
- **Issue:** Le commentaire de sécurité commun aux quatre nouveaux tests citait littéralement
  `machine: new FakeInventaireMachine()` pour expliquer pourquoi chaque montage le passe. Le compte
  passait à **12** au lieu des 11 exigés — la contradiction exacte déjà rencontrée par 20-01, 20-02,
  20-03 et 20-04.
- **Fix:** Commentaire reformulé en « le faux inventaire de machine », sans le littéral. Aucune
  information n'est perdue, le compte est à **11** (7 préexistants + 4 nouveaux). C'est ici la **5ᵉ
  application** de la doctrine « un commentaire qui décrit un piège ne reproduit jamais l'expression
  fautive », et la première où le code n'avait pas de raison de garder le littéral (à la différence des
  commentaires de sécurité de 20-04, conservés).
- **Files modified:** `tests/Chronos.Tests/DiagnosticServiceTests.cs`
- **Commit:** `ebeb0ea`

### Écarts de lecture de critère (aucun code modifié)

**3. `grep -c 'private static string Describe'` rend 1 et non 0**

- **Found during:** Task 1
- **Issue:** Le littéral du critère matche aussi `private static string DescribeBlobShape(byte[] blob)`
  (l. 526), une méthode **sans rapport** avec ce plan (elle décrit la forme d'un blob chiffré pour le
  diagnostic des coffres OAuth).
- **Décision:** Ne PAS toucher à `DescribeBlobShape`. Le critère est lu sur ce qu'il veut prouver — la
  méthode `Describe(WindowState)` n'est plus statique — et cette lecture est vérifiée :
  `grep -c 'private static string Describe(WindowState'` = **0**, `grep -c 'private string Describe'` =
  **1**.
- **Files modified:** aucun.

### Écart de périmètre assumé

**4. La tâche 3 est SUBSTITUÉE par un protocole écrit**

- **Found during:** Task 3
- **Issue:** Le point d'arrêt exige de lancer l'overlay (purge non supervisée de 25 groupes de hooks) et
  une authentification réelle de l'utilisateur.
- **Décision:** Tout ce qui était automatisable l'a été (intégrité du coffre, absence d'artefact de
  publication, prédiction **outillée** de la bascule de la phase 19 à partir des données réelles) ; le
  reste est délivré en **un seul protocole ordonné** consolidant les phases 17, 18, 19 et 20. La ligne de
  la carte de validation conserve son statut `⏳`, distinct de ✅ et de ⬜. Précédents : 19-05, 18-06.
- **Files modified:** aucun (protocole rédigé dans ce SUMMARY).

---

**Total deviations:** 2 blocages auto-corrigés (Rule 3), 1 lecture de critère, 1 substitution de
checkpoint.
**Impact on plan:** Aucun périmètre ajouté ni retiré. **Aucune tâche n'a atteint la limite de 3
auto-corrections.**

## Issues Encountered

Aucun. La tâche 1 est passée du rouge au vert en une itération ; la tâche 2 n'a révélé aucun écart de
comptage inexplicable.

## Vérifications de sécurité (contrôle avant / après)

| Contrôle | Attendu | Avant le plan | Après le plan |
|---|---|---|---|
| `%APPDATA%\Chronos\oauth.dat` | 518 o, mtime 1783863147 | **518 1783863147** | **518 1783863147** |
| `~/.claude/settings.json` | 6872 o, mtime 1785403369 | **6872 1785403369** | **6872 1785403369** |
| Fichiers SOURCE avec `RefreshAsync` | exactement 2 | 2 | **2** (`ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs`) |
| Appel de refresh avec le jeton RÉEL | jamais | — | **jamais** |
| Requête réseau depuis un test | aucune | — | **aucune** — les 11 `DiagnosticServiceTests` montent `Token = null`, et la sonde réseau est gardée par `if (token is not null)` |
| Écriture dans le vrai `%APPDATA%\Chronos\` | aucune | — | **aucune** — tous les mtimes du dossier sont antérieurs au début du plan (le plus récent, `sessions/`, date de 4 h 30 avant et vient des hooks de Claude Code, pas de la suite de tests) |
| Overlay lancé | **NON** | non | **jamais lancé** |
| `dotnet publish` exécuté | **NON** | non | **jamais** — l'exe de `publish/` date toujours du 2026-07-12 |
| `LoginClaudeCommand` bindée | jamais | 0 binding | **0 binding** |
| Dépendance NuGet ajoutée | aucune | — | **aucune** (aucun `.csproj` touché) |

## Verification

- [x] `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q` : **748/748, 0 échec**,
      **deux passes consécutives**, 3 s chacune
- [x] 5 gardes permanentes vertes, chacune par sa commande : **19 tests**
- [x] `grep -c '"estimé — "' src/Chronos/Services/DiagnosticService.cs` = **0**
- [x] `grep -c 'PLANCHER' src/Chronos/Services/DiagnosticService.cs` = **1**
- [x] `grep -c 'LibelleSource\.' src/Chronos/Services/DiagnosticService.cs` = **3**
- [x] `grep -c 'private static string Describe(WindowState' …` = **0** et `grep -c 'private string Describe' …` = **1**
- [x] `grep -c 'new FakeInventaireMachine()' tests/Chronos.Tests/DiagnosticServiceTests.cs` = **11**
- [x] Les **11** `DiagnosticServiceTests` en **482 ms** (rouge) / **534 ms** avec `NormalisationUniqueTests`
      (vert) — très en deçà des 15 s exigées
- [x] `grep -c "⬜ pending" …/20-VALIDATION.md` = **0**
- [x] `grep -c "⏳" …/20-VALIDATION.md` = **8** (≥ 1 exigé)
- [x] `grep -c "status: validated" …/20-VALIDATION.md` = **1**
- [x] `grep -c "| EXA-03 | Phase 20 | Complete |" .planning/REQUIREMENTS.md` = **1**
- [x] `grep -c "| EXA-06 | Phase 20 | Complete |" .planning/REQUIREMENTS.md` = **1**
- [x] `ROADMAP.md` : `| 20. Honnêteté visible — cadran & diagnostic | 5/5 | Complete   | 2026-09-12 |`
- [x] `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` = **518 1783863147**
- [x] `grep -rl --include=*.cs "RefreshAsync" src/Chronos | wc -l` = **2**
- [x] Le protocole cite la sauvegarde de `~/.claude/settings.json` (point 0) **avant** toute instruction de
      lancement (point 1) — ordre vérifiable à la lecture
- [x] `git status --short` vide : aucun artefact de publication produit par l'agent

## Known Stubs

**Aucun.** `LibelleSource`, livré sans consommateur par 20-02 et bindé au cadran par 20-04, a désormais son
**second** consommateur — le rapport de diagnostic. La dette de vocabulaire orphelin est levée dans le plan
qui l'avait annoncée.

Aucune valeur codée en dur, aucun texte « à venir », aucune donnée factice.

## Ce qui reste ouvert après ce plan

1. **Six vérifications humaines**, toutes listées ci-dessus et reportées dans la section
   « Manual-Only Verifications » de `20-VALIDATION.md` avec leur phase et leur exigence.
2. **HDR-02 en production** : le marqueur `human_needed` de la phase 18 reste ouvert, et il le restera
   jusqu'à un 429 réel. Ne pas le refermer sur la foi d'un test à réponse fabriquée.
3. **Une économie possible, à décider après le point 8** : si `/api/oauth/usage` sert déjà la famille
   `anthropic-ratelimit-unified-*`, les **≈ 288 micro-requêtes/jour** de la sonde deviennent redondantes.
4. **La version du `.csproj` est encore 2.8.1**, celle de l'exe déjà déployé. Une release du milestone
   v1.5 doit la monter — hors périmètre de cette phase.

## Commits

| Task | Étape | Commit | Message |
|---|---|---|---|
| 1 | ROUGE | `ebeb0ea` | `test(20-05): 6 tests ROUGES - le rapport doit nommer la source et son anciennete (EXA-06)` |
| 1 | VERT | `b6e03e7` | `feat(20-05): le diagnostic nomme la SOURCE et son ANCIENNETE (EXA-06)` |
| 2 | — | `b235b74` | `docs(20-05): porte de phase franchie sur preuves - comptage reconcilie nominativement` |
| 3 | — | *(substituée — aucun fichier modifié)* | — |

---
*Phase: 20-honn-tet-visible-cadran-diagnostic*
*Completed: 2026-09-12*
*Dernier plan du milestone v1.5 — Exactitude permanente*

## Self-Check: PASSED

Les 5 fichiers annoncés comme modifiés sont présents sur disque, et eux seuls sont au diff
`ebeb0ea~1..HEAD`. Les 3 commits annoncés (`ebeb0ea`, `b6e03e7`, `b235b74`) sont présents dans
l'historique git. Aucun fichier créé n'est annoncé. **Ordre du protocole vérifié par extraction** : la
ligne `cp ~/.claude/settings.json …` est à la ligne 302, la ligne `dotnet publish …` à la ligne 312 — la
sauvegarde précède bien toute instruction de lancement. Aucun élément manquant.
