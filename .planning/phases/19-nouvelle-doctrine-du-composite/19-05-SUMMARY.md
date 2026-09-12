---
phase: 19-nouvelle-doctrine-du-composite
plan: 05
subsystem: validation
tags: [porte-de-phase, gardes-permanentes, bilan-de-comptage, securite, verification-humaine, exa-02, exa-05, del-03, del-04]

# Dependency graph
requires:
  - phase: 19-01
    provides: "CapturedAt posé par les 4 providers, UnExactADejaEteObtenu, SourceActiviteMemoisee"
  - phase: 19-02
    provides: "DoctrineFraicheur — les 4 branches, pures et falsifiables par mutation réelle"
  - phase: 19-03
    provides: "la doctrine câblée en tête de chaîne (LastExactUsageProvider), lecture paresseuse et mémoïsée"
  - phase: 19-04
    provides: "« ≥ N % » au cadran et l'invitation à se connecter, bindées et prouvées par [WpfFact]"
provides:
  - "Porte de phase 19 exécutée : 699/699 sur DEUX exécutions consécutives, 5 gardes permanentes vertes"
  - "Bilan de comptage réconcilié nominativement depuis la baseline 652 — aucun écart inexpliqué"
  - "19-VALIDATION.md : carte de vérification par tâche remplie (26 lignes), sign-off coché, relevé de porte daté"
  - "Protocole de constat de la bascule, prêt à exécuter par l'utilisateur"
affects: [phase-20-honnetete-visible, EXA-03, EXA-06, release-republication-exe]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Porte de phase = preuve automatisée rassemblée ET nommage explicite de ce que l'automatisation ne peut pas produire"
    - "Un statut de carte qui n'est ni « passed » ni « pending » : ⏳ à vérifier par l'utilisateur — une vérification déléguée n'est pas une vérification en attente de robot"

key-files:
  created:
    - .planning/phases/19-nouvelle-doctrine-du-composite/19-05-SUMMARY.md
  modified:
    - .planning/phases/19-nouvelle-doctrine-du-composite/19-VALIDATION.md

key-decisions:
  - "Le point de vérification humaine a été SUBSTITUÉ, pas simulé : lancer l'overlay déclencherait la purge des 25 groupes de hooks de ~/.claude/settings.json hors supervision de l'utilisateur"
  - "REQUIREMENTS.md laissé INTACT : les cinq exigences étaient déjà cochées par 19-03 et 19-04 ; les décocher contredirait des décisions documentées, les recocher serait un geste vide"
  - "La ligne manuelle de la carte porte un statut DISTINCT (⏳) plutôt que ✅ : une porte qui se déclare verte sur une preuve qu'elle n'a pas produite est exactement le défaut que ce milestone corrige"
  - "DEL-04 reste Complete mais sa moitié invisible est consignée : le « + delta estimé » de la lettre a été amendé pour cause (280 % mesuré), et la matière brute de substitution n'est bindée nulle part"

patterns-established:
  - "Deux exécutions consécutives de la suite dès qu'une phase ajoute des [WpfFact] — le chargeur BAML a un précédent de course sous parallélisme xUnit (16-03)"

requirements-completed: []

# Metrics
duration: 16min
completed: 2026-09-12
---

# Phase 19 Plan 05 : Porte de phase et constat de la bascule — Summary

Porte de phase 19 franchie sur preuves automatisées — 699/699 sur deux exécutions, cinq gardes permanentes
vertes, coffre de jetons intact, comptage réconcilié à l'unité près depuis 652 — avec la seule preuve
qu'aucun robot ne peut produire explicitement **déléguée à l'utilisateur** plutôt que simulée.

## Performance

- **Durée :** ~16 min
- **Tâches :** 2 (1 exécutée, 1 substituée et consignée)
- **Commits :** 1 de tâche + 1 de métadonnées
- **Tests :** 699 / 699, 0 échec, 0 ignoré — **deux exécutions consécutives**
- **Fichiers modifiés :** 1 (`19-VALIDATION.md`) — **aucun fichier sous `src/` ni `tests/`**

## Commits

1. **Task 1 : porte de phase verte, carte de validation remplie** — `c24209f` (docs)

---

## Bilan de comptage — réconcilié nominativement depuis la baseline 652

Le critère de sortie n'est pas « ≥ 652 » mais **0 échec + chaque écart justifié nominativement**
(précédent 16-01, où le recul 405 → 384 était le livrable).

| Étape | Total | Écart | Justification nominative |
|-------|-------|-------|--------------------------|
| Baseline d'entrée de phase | **652** | — | mesurée avant 19-01 |
| Après 19-01 | **664** | **+12** | +2 `ClaudeUsageObjectProviderTests` (âge du FICHIER, âge inconnu) · +4 `LastExactStoreTests` (les deux silences du magasin + refus de l'incertifiable) · +6 `SourceActiviteMemoiseeTests` |
| Après 19-02 | **680** | **+16** | +13 `DoctrineFraicheurTests` (les 4 branches + cohérence du prédicat de paresse + dérivation de la limite d'âge) · +3 `GardesDoctrineTests` |
| Après 19-03 | **688** | **+8** | +1 scission du test 3 de `LastExactUsageProviderTests` · +5 `LastExactUsageProviderTests` (coût disque, bit EXA-05) · +2 `WeeklyRecalibrationTests` |
| Après 19-04 | **699** | **+11** | +2 `WindowGaugeViewModelTests` · +1 garde `CadranBindingTests` · +6 `MainViewModelTests` (matrice EXA-05) · +2 `[WpfFact]` d'invitation |
| **Mesuré par la porte** | **699** | — | **652 + 12 + 16 + 8 + 11 = 699 — concorde exactement** |

**Zéro test supprimé, zéro test ignoré sur toute la phase.** Huit tests ont été **réécrits sur place**
(quatre en 19-03, quatre en 19-04) : la preuve est conservée, seule la matière change. Deux de ces
réécritures étaient hors plan et sont documentées comme déviations dans leurs SUMMARY respectifs.

**Aucun écart inexpliqué. Aucune anomalie ouverte de comptage.**

## Résultats de la porte

### Suite complète — deux exécutions consécutives

| Exécution | Résultat | Durée |
|-----------|----------|-------|
| 1 | **699 réussis / 699 · 0 échec · 0 ignoré** | 2 min 9 s |
| 2 | **699 réussis / 699 · 0 échec · 0 ignoré** | 2 min 8 s |

La double exécution était exigée parce que la phase a ajouté des `[WpfFact]` : le chargeur BAML de WPF a
un précédent documenté de course sous parallélisme xUnit (décision 16-03, `WpfXamlType.FindKnownMember`).
**Aucune variation entre les deux passes** — la parade par collection `DisableParallelization` tient.

### Les cinq gardes permanentes

| Garde | Résultat |
|-------|----------|
| Pureté `Services` / `Models` — `ServicesLayerPurityTests` | **2 / 2 vert** |
| Composition DI — `CompositionRootTests` | **5 / 5 vert** |
| Position de la sonde (phase 18) — `La_sonde_d_en_tetes_est_le_PRIMAIRE` | **1 / 1 vert** |
| Normalisation unique — `NormalisationUniqueTests` | **3 / 3 vert** |
| Gardes de doctrine — `GardesDoctrineTests` | **3 / 3 vert** |

La troisième mérite d'être soulignée : la garde de position de la phase 18 est **comportementale** (deux
sources exactes aux chiffres différents, l'inversion primary/fallback mesurée puis révoquée). Elle a
survécu à une refonte qui a inséré une doctrine complète **au-dessus** d'elle, sans être cassée en silence.

### Les quatre branches de la doctrine

`DoctrineFraicheurTests` : **13 / 13 vert**. Chaque branche a au moins un test qui tombe si elle
disparaît — prouvé par mutation réelle en 19-02 (4 mutations jouées, toutes révoquées, `git diff` vide).

### Détail par groupe de tests de la carte

| Groupe | Résultat |
|--------|----------|
| `ClaudeUsageObjectProviderTests` | 6 / 6 |
| `LastExactStoreTests` | 17 / 17 |
| `SourceActiviteMemoiseeTests` | 6 / 6 |
| `LastExactUsageProviderTests` | 15 / 15 |
| `WeeklyRecalibrationTests` | 9 / 9 |
| `WindowGaugeViewModelTests` | 19 / 19 |
| `CadranBindingTests` | 14 / 14 |
| `MainViewModelTests` | 42 / 42 |
| `CompositeUsageProviderTests` | 14 / 14 — **intact, aucune retouche de toute la phase** |
| `Host_resout_et_dispose_les_singletons` | 1 / 1 |

### Sécurité et intégrité des données de production

| Contrôle | Attendu | Mesuré |
|----------|---------|--------|
| `stat` de `oauth.dat` **avant** la porte | `518 1783863147` | `518 1783863147` ✅ |
| `stat` de `oauth.dat` **après** la porte | `518 1783863147` | `518 1783863147` ✅ |
| Fichiers source contenant `RefreshAsync` | exactement **2** | **2** ✅ — `ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs` |
| `usage.json` de production inchangé | 77 o, mtime 1783666519 | 77 o, mtime 1783666519 ✅ |
| Requête réseau réelle depuis un test | aucune | aucune ✅ |
| Déchiffrement du coffre | aucun | aucun ✅ |
| `~/.claude/settings.json` | intact | intact ✅ — **l'overlay n'a pas été lancé** |
| `git status --short` en entrée de plan | vide | vide ✅ |

Le coffre n'a **jamais** été ouvert, et l'endpoint de refresh n'a **jamais** été appelé avec le refresh
token réel. Les deux relevés `stat` encadrent l'intégralité du travail.

### La carte de vérification par tâche

`19-VALIDATION.md` est passée de **23 lignes toutes `⬜ pending`** à **26 lignes**, dont **25 `✅ passed`**
et une seule `⏳ à vérifier par l'utilisateur`. Les colonnes `File Exists` marquées `❌ à créer` sont
toutes passées à `✅` : les sept fichiers de test que le planner anticipait existent et sont verts.

**Trois lignes ajoutées**, pour des tests notables de la phase que la carte du planner ne couvrait pas —
tous trois nés de déviations documentées en cours d'exécution :

| Ligne ajoutée | Test | Pourquoi elle manquait |
|---|---|---|
| 19-02 T2 (ajout) | `La_limite_d_age_est_DERIVEE_de_la_cadence_de_la_sonde` | 13e test de doctrine ajouté hors plan par 19-02, il verrouille par test le `key_link` « limite dérivée, jamais recopiée » que seul un critère `grep` à usage unique garantissait |
| 19-04 T1 (ajout) | `EstimatedTokens_seul_ne_surface_PLUS_rien_au_cadran` | garde de non-retour comportementale de l'estimation absolue, ajoutée par 19-04 |
| 19-04 T1 (ajout) | `Etat_fiabilite_mixte_la_marque_du_pourcentage_est_par_fenetre` | test **renommé** par 19-04 (ex-`…_le_tilde_du_pourcentage_…`) : la carte aurait nommé un test qui n'existe plus |

Les cinq cases de `## Validation Sign-Off` sont cochées, et les quatre cases de `## Wave 0 Requirements`
également. Le frontmatter passe `status: planned` → `status: validated` et `wave_0_complete: true`.

## Cohérence des exigences — vérification honnête

Chaque exigence de la phase est reliée à une commande réelle **et** au nom du test qui tombe si elle cesse
d'être vraie. Aucune n'est un trou.

| Exigence | Test qui tombe si elle cesse d'être vraie | Verdict |
|---|---|---|
| **EXA-02** | `Un_usage_json_vieux_de_deux_mois_porte_l_age_du_FICHIER_et_non_celui_de_la_lecture` + `Un_exact_vivant_sans_CapturedAt_est_incertifiable_et_ne_passe_pas_pour_frais` + `Releve_anterieur_a_l_horizon_des_transcripts_est_indisponible_et_non_sous_evalue` | **Satisfaite** — la démotion par âge est câblée en tête de chaîne et efface le pourcentage |
| **EXA-04** | `Aucun_rapport_entre_un_comptage_de_tokens_et_un_plafond_dans_Services_et_Models` + `Le_plancher_ne_gonfle_JAMAIS_l_utilization` + `EstimatedTokens_seul_ne_surface_PLUS_rien_au_cadran` | **Satisfaite** — interdiction structurelle (balayage du texte source) ET comportementale (le champ ne surface plus rien) |
| **EXA-05** | `L_invitation_est_bindee_sur_ReconnecterCommand_et_JAMAIS_sur_LoginClaudeCommand` + `L_invitation_reste_COLLAPSED_quand_un_exact_a_deja_ete_obtenu` + `Sans_magasin_le_snapshot_declare_qu_aucun_exact_n_a_JAMAIS_ete_obtenu` | **Satisfaite** — le bit remonte du magasin jusqu'au XAML, et le binding est prouvé **par instance de commande**, pas par nom |
| **DEL-03** | `Trois_heures_sans_aucune_activite_reste_EXACT_et_non_perime` + `Un_releve_de_deux_heures_SANS_activite_depuis_reste_EXACT` | **Satisfaite** — la réhabilitation au-delà de la limite d'âge est réelle et testée de bout en bout |
| **DEL-04** | `UtilizationText_plancher_prefixe_superieur_ou_egal` + `Activite_depuis_le_releve_donne_un_plancher_marque` + `Un_plancher_n_est_JAMAIS_persiste_comme_exact` | **Satisfaite sur sa lettre amendée** — voir la réserve ci-dessous |

### La réserve sur DEL-04 — dite plutôt que masquée

La lettre de DEL-04 est : « l'affichage est *dernier exact + delta estimé*, marqué avec sa marge
d'incertitude ». Trois moitiés, deux tenues et une amendée :

- ✅ **« jamais confondu avec un relevé exact »** : le préfixe « ≥ » est calculé, bindé, affiché, testé.
- ✅ **« marqué avec sa marge d'incertitude »** : la marque est « ≥ », et elle est **unilatérale** —
  décision 19-04, on sait la borne inférieure, pas la supérieure.
- ⚠️ **« + delta estimé »** : **délibérément non fait**, et c'est un amendement de la lettre, pas un oubli.
  Ajouter le delta au chiffre produirait un nombre faux : mesure à l'appui, 643 649 933 tokens sur 5 h
  contre l'ancien plafond de 230 000 000 donnerait **280 %**. La substitution honnête décidée en 19-02 est
  d'accompagner le plancher de sa **matière brute** (tokens depuis le relevé, non convertis).
  **Cette matière brute est calculée, testée, et bindée nulle part.** C'est la dette n° 1 de la phase 20.

`REQUIREMENTS.md` a été laissé **intact** : les cinq exigences y étaient déjà passées à `Complete` par les
plans 19-03 et 19-04, qui les ont livrées. Les décocher contredirait des décisions documentées ; les
recocher serait un geste vide. La réserve ci-dessus est donc consignée ici et dans `19-VALIDATION.md`,
là où elle sera lue par le planner de la phase 20.

## État réel de la machine, relevé en lecture seule

Aucune écriture, aucune suppression. `usage.json` est le cas de test de production de tout le milestone.

```
$APPDATA/Chronos/usage.json       {"five_hour":{"used_percentage":10,"resets_at":9},"capturedAt":1783666519131}
                                  77 octets, mtime 1783666519  (2026-07-10)
$APPDATA/Chronos/last-exact.json  ABSENT
$APPDATA/Chronos/oauth.dat        518 octets, mtime 1783863147
```

**Prédiction que la vérification humaine doit confirmer.** `capturedAt = 1783666519131` ms =
**2026-07-10**, soit **64 jours** au 2026-09-12. La limite d'âge de la doctrine est de 6 minutes et
l'horizon des transcripts de 8 jours : `Covers(T)` est donc **faux**, ce qui envoie ce relevé en
**branche 4 — indisponible**, et non en plancher. `last-exact.json` étant absent, `UnExactADejaEteObtenu`
vaut **false**, ce qui **allume l'invitation à se connecter**. Attendu au cadran : **aucun pourcentage, et
une pastille d'invitation**.

---

## À VÉRIFIER PAR L'UTILISATEUR

**Le point de vérification humaine de ce plan n'a PAS été réalisé.** Il a été substitué par la porte
automatisée ci-dessus, à la demande explicite de l'utilisateur et pour trois raisons concrètes :

1. **Lancer l'overlay déclencherait la purge des 25 groupes de hooks** de `~/.claude/settings.json`
   (réconciliation codée en phase 15, appelée dans `App.OnStartup` après `window.Show()`). Ce geste est
   irréversible sans la sauvegarde, et ne doit pas se produire sans supervision.
2. **Le jeton de la machine est mort depuis le 2026-07-12.** Se reconnecter exige l'authentification
   personnelle de l'utilisateur dans un navigateur.
3. **Aucun test ne peut produire cette preuve** : elle dépend de l'état réel d'un fichier figé depuis deux
   mois, et va du fichier jusqu'au pixel.

**Ce que la porte automatisée prouve déjà :** chaque maillon de la chaîne, séparément et en composition —
l'âge du fichier, les quatre branches, le câblage en tête de chaîne, le préfixe « ≥ », le binding de
l'invitation. **Ce qu'elle ne prouve pas :** que le pixel affiché sur cette machine-ci a bien basculé.

### Protocole exact — durée ~5 min, aucune donnée modifiée, aucune requête réseau

**Étape 1 — relever l'état avant** (lecture seule) :

```bash
cat "$APPDATA/Chronos/usage.json"
ls -l "$APPDATA/Chronos/last-exact.json" 2>/dev/null || echo "ABSENT"
```

Attendu : `used_percentage: 10` avec `capturedAt` du 2026-07-10, et `last-exact.json` **absent** — c'est
exactement l'état relevé ci-dessus le 2026-09-12.

**Étape 2 — fermer l'overlay lancé par l'autostart.** C'est l'exe **publié**, qui porte encore l'ancien
comportement. Le laisser tourner mélangerait les deux versions.

**Étape 3 — lancer la version du dépôt :**

```bash
dotnet run --project src/Chronos/Chronos.csproj -c Release
```

**Étape 4 — regarder le cadran et répondre à trois questions :**

| # | Question | Attendu |
|---|---|---|
| (a) | Le cadran affiche-t-il encore **« 10 % »** quelque part ? | **NON** — c'est toute la raison d'être du milestone |
| (b) | Affiche-t-il un état **indisponible** (aucun pourcentage, arcs neutres) ? | **OUI** |
| (c) | Une **pastille cliquable** apparaît-elle en bas à droite, infobulle « Aucun chiffre d'usage n'a jamais été obtenu — cliquer pour se connecter à Claude » ? | **OUI**, si et seulement si `last-exact.json` était absent à l'étape 1 |

*Si `last-exact.json` existe désormais et porte une fenêtre encore valide, l'attendu change : pas
d'invitation, et soit un chiffre exact « encore valide », soit un plancher « ≥ N % ». Décrire ce qui est
vu — c'est tout aussi concluant.*

**Étape 5 — ne PAS cliquer sur la pastille** pour cette vérification-ci. Le parcours de reconnexion réel
fait partie des deux vérifications manuelles en attente depuis la phase 17.

**Étape 6 — contrôle de non-régression, après avoir fermé l'application :**

```bash
stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"   # doit rendre : 518 1783863147
cat "$APPDATA/Chronos/usage.json"              # doit être identique à l'étape 1
```

`last-exact.json` peut, lui, avoir été créé si une source a répondu — c'est normal et attendu.

### Vérification différée : comparer le chiffre du cadran avec `/usage`

Consignée dans `19-VALIDATION.md` sous « Manual-Only Verifications », elle **ne peut pas être faite
aujourd'hui** — le jeton est mort, donc aucune source exacte ne répond. **Après reconnexion** (parcours
manuel en attente depuis la phase 17) :

1. Se reconnecter via la pastille de l'overlay, puis attendre un tick (60 s max).
2. Dans Claude Code, taper `/usage` et relever les deux pourcentages (5 h et hebdo).
3. Comparer avec les deux anneaux du cadran.

Attendu : **les chiffres coïncident**, et le cadran ne porte **aucune marque** (ni « ≥ », ni état daté) —
un relevé frais de moins de 6 minutes est un exact frais, branche 1. Un écart entre les deux chiffres
serait un défaut de la **normalisation d'unités** (0..1 pour les en-têtes, 0..100 pour `/api/oauth/usage`)
et non de la doctrine de fraîcheur.

### Ce qui reste vrai quoi qu'il arrive

**Un seul système d'exécution conserve l'ancien comportement : l'exe déjà publié, lancé par l'autostart.**
L'utilisateur continuera de voir « 10 % » tant qu'il n'aura pas republié. La vérification ci-dessus porte
sur une exécution **du dépôt** ; la republication est une décision de release, hors du périmètre de cette
phase.

### Retour humain

> *Non recueilli — vérification substituée. Ce bloc est à remplir mot pour mot lorsque l'utilisateur aura
> exécuté le protocole ci-dessus.*

---

## Déviations par rapport au plan

### 1. [Substitution demandée] Le point de vérification humaine n'a pas été réalisé

- **Trouvé pendant :** Task 2, avant toute action
- **Nature :** l'utilisateur a explicitement demandé une exécution autonome et interdit de lancer
  l'application ou de tenter un login. La tâche 2 est un `checkpoint:human-verify` bloquant.
- **Traitement :** la porte automatisée de la tâche 1 a été exécutée intégralement (dont la suite complète
  **deux fois** au lieu d'une), la carte de validation mise à jour, et le protocole exact du constat
  consigné ci-dessus sous « À VÉRIFIER PAR L'UTILISATEUR ». La ligne `19-05 T2` de la carte porte un
  statut **distinct** (`⏳ à vérifier par l'utilisateur`) et non `✅ passed`.
- **Ce qui n'a PAS été fait :** aucun `dotnet run`, aucun login, aucune requête réseau, aucune écriture
  dans `%APPDATA%\Chronos\`, aucune modification de `~/.claude/settings.json`.
- **Fichiers modifiés :** aucun au titre de cette déviation.

### 2. [Constat] `REQUIREMENTS.md` était déjà à jour — aucun changement nécessaire

- **Trouvé pendant :** Task 1, point D
- **Constat :** le plan prescrivait de passer EXA-02, EXA-04, EXA-05, DEL-03 et DEL-04 de `Pending` à
  `Complete`. La mesure montre qu'elles l'étaient déjà : cochées par les commits `9f78f5f` (19-03) et
  `1e50c54` (19-04), qui ont livré les comportements correspondants.
- **Traitement :** fichier laissé intact, et la substance des cinq exigences vérifiée une à une (tableau
  ci-dessus) plutôt que le cochage réappliqué mécaniquement. La réserve sur la moitié invisible de DEL-04
  est consignée explicitement.
- **Conséquence :** l'artefact `REQUIREMENTS.md` annoncé au frontmatter du plan n'apparaît pas dans les
  fichiers modifiés — l'effet attendu existe, il a simplement été produit plus tôt.

**Total : 1 substitution demandée + 1 constat. Aucune correction de code, aucun fichier `src/` ni `tests/`
touché — conforme au périmètre du plan.**

## Anomalies ouvertes

**Une seule, et elle est intentionnelle :** le constat de la bascule sur la machine réelle n'est pas
produit. Ce n'est pas un échec de la porte mais une délégation assumée, documentée et outillée.

Aucune anomalie de comptage. Aucune ligne de carte en échec. Aucune garde cassée.

---

## Dettes léguées à la phase 20 — liste consolidée

Aucune de ces dettes n'est traitée ici. Elles sont regroupées pour que le planner de la phase 20 tranche
explicitement plutôt que d'en hériter en silence.

### Exigences non couvertes, qui sont l'objet même de la phase 20

1. **EXA-03** — le cadran doit distinguer **visuellement** trois états : exact frais, exact daté,
   indisponible. `ProvenanceReleve` porte déjà la distinction en données (`Frais` / `EncoreValide` /
   `PlancherAvecActivite`) ; rien ne la rend visible au-delà du préfixe « ≥ ».
2. **EXA-06 — seulement à moitié** : le diagnostic doit dire **quelle source** alimente réellement
   l'affichage, **et depuis quand**. Le « depuis quand » existe (`CapturedAt` par fenêtre) et l'**état**
   existe (`Provenance`). Le **nom de la source** n'est porté par **aucun champ** : vérifié, `ProvenanceReleve`
   ne compte que trois membres, tous des états, aucun n'identifie une source. Il faut un champ de plus.

### Choses calculées, testées, et invisibles

3. **`TokensText` / `HasTokens` / `DataUnavailable` ne sont bindés nulle part** — mesuré : **0 occurrence**
   dans les XAML, présents uniquement dans `MainViewModel.cs` et `WindowGaugeViewModel.cs`. C'est la
   matière brute de DEL-04 : correcte, prouvée, et que personne ne voit. **Dette n° 1.**
4. **`IsStale` est calculé (`MainViewModel.cs:396`) et bindé nulle part** — hérité du diagnostic de
   pré-milestone, toujours vrai aujourd'hui.

### Contradictions de vocabulaire à trancher

5. **Deux seuils d'ancienneté coexistent** : `IsStale` à **2 min** et la limite d'âge de la doctrine à
   **6 min**. Ils ne mesurent pas la même chose et ne doivent pas être confondus à l'écran.
6. **`UsageSnapshot.SourceCapturedAt` est un champ LEGACY** — l'horodatage est désormais **par fenêtre**
   (`WindowState.CapturedAt`). Conservé en 19-02 parce que le retirer coûtait 4 tests pour zéro exigence.
7. **`WindowState.EstimatedTokens` est devenu mort en production** — plus aucun chemin de présentation ne
   le lit (garde de non-retour en place). Candidat à suppression pure.

### Contraintes imposées au dessin de la phase 20

8. **Pas d'arc de delta, pas de barre d'erreur.** L'incertitude d'un plancher est **unilatérale** : on
   connaît la borne **inférieure**, il n'existe **aucune borne supérieure à représenter**. Toute
   représentation bilatérale serait un mensonge graphique.
9. **Les trois pastilles (déconnexion, hors-ligne, invitation) n'ont pas d'apparence définitive.**
   L'exclusivité déconnexion/invitation est garantie et testée côté ViewModel, mais la cohabitation
   visuelle des trois sur 170 px n'a jamais été éprouvée.
10. **Trou de la `UniformGrid` des réglages** — signalé en phase 19, non traité.

### Dette d'outillage

11. **Durée de la suite : 2 min 8 s.** `DiagnosticServiceTests` fait passer la suite de ~56 s à plus de
    2 min, parce que chaque test exécute `BuildReportAsync`, qui balaie `%APPDATA%` / `%LOCALAPPDATA%` et
    fait un **poll UIA réel**. À isoler ou à fausser.

---

## Question laissée ouverte et assumée

**La branche « frais » tolère jusqu'à ~6 minutes d'activité non vérifiée.**

La limite est **dérivée de la cadence de la sonde** (et non recopiée — un test le verrouille :
`La_limite_d_age_est_DERIVEE_de_la_cadence_de_la_sonde`) pour que le chemin nominal ne paie **jamais** la
passe disque des transcripts. Mesure réelle du 2026-09-12 : une passe coûte **2,7 à 3,2 s** et lit
**536 Mo**. Au tick de 60 s, une lecture inconditionnelle serait une E/S permanente.

Une limite plus serrée serait **strictement plus honnête**, et son seul coût est la fréquence de cette
passe. Si la phase 20 livre une **lecture en queue de fichier**, la limite pourra descendre vers 2 minutes
et **converger avec `IsStale`** — ce qui résoudrait du même geste la contradiction n° 5 ci-dessus.

**Ne pas la rendre réglable.** Le dépôt a déjà payé ce travers : une source de plafond passée en `Manual`
s'est retrouvée gelée à vie, parce que la calibration automatique refuse par conception d'écraser un
réglage manuel. Un seuil d'honnêteté exposé à l'utilisateur est un seuil qui finit désactivé.

---

## Self-Check: PASSED

- `19-VALIDATION.md` — FOUND, `status: validated`, 0 ligne `⬜ pending`, 0 `❌ à créer`, sign-off coché
- `19-05-SUMMARY.md` — FOUND
- Commit `c24209f` — FOUND
- `$APPDATA/Chronos/oauth.dat` — `518 1783863147`, identique avant et après
- `$APPDATA/Chronos/usage.json` — 77 o, mtime 1783666519, identique avant et après
- `~/.claude/settings.json` — non touché (l'overlay n'a jamais été lancé)
- Suite complète — 699 / 699, 0 échec, deux exécutions consécutives
