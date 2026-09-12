---
phase: 19-nouvelle-doctrine-du-composite
plan: 02
subsystem: services
tags: [doctrine, fraicheur, delta, plancher, gardes-de-non-retour, exa-02, exa-04, del-03, del-04]

# Dependency graph
requires:
  - phase: 16-fondations-du-delta
    provides: "TranscriptActivityLog (Covers / Since / Horizon), LastExactStore"
  - phase: 18-source-en-tetes
    provides: "RateLimitHeaderUsageProvider.CadenceNominale (dont la limite d'age est DERIVEE), StatutServeur/EtatDepassement sur WindowState"
  - phase: 19-nouvelle-doctrine-du-composite
    plan: "19-01"
    provides: "WindowState.CapturedAt rempli par les QUATRE providers exacts — sans quoi la limite d'age n'aurait rien a mesurer"
provides:
  - "ProvenanceReleve : la 4e distinction, en trois valeurs, sans toucher SourceReliability"
  - "WindowState.Provenance et WindowState.TokensDepuisReleve (init, nullables, sans required)"
  - "UsageSnapshot.UnExactADejaEteObtenu (bool?) — pose AU-DESSUS du composite"
  - "DoctrineFraicheur : classe PURE, 4 branches, chacune prouvee necessaire par mutation reelle"
  - "Garde de recomposition : toute propriete d'instance de UsageSnapshot doit etre nommee dans CompositeUsageProvider.cs"
  - "Garde de non-retour EXA-04 : aucun rapport entre un comptage de tokens et un plafond dans Services/ et Models/"
  - "Preuve de comportement : le repli le plus interne de la chaine est bien celui a anciennete non bornee"
affects: [19-03, 19-04, 19-05, 20-honnetete-visible]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Doctrine = classe PURE statique, now en parametre, aucune E/S, aucune horloge propre -> 4 branches deterministes en [Fact]"
    - "Reaffectation d'une valeur d'enum MORTE (Estimated) plutot qu'ajout d'un membre : zero site de construction retouche ET zero traversee silencieuse des consommateurs"
    - "Changement de NATURE du chiffre (« au moins X ») au lieu de changement de VALEUR : l'incertitude est unilaterale et la borne superieure est declaree inconnue"
    - "Predicat de paresse (ABesoinDuJournal) dont la coherence avec le verdict est imposee par test, sur une matrice de cas"

key-files:
  created:
    - src/Chronos/Models/ProvenanceReleve.cs
    - src/Chronos/Services/DoctrineFraicheur.cs
    - tests/Chronos.Tests/DoctrineFraicheurTests.cs
    - tests/Chronos.Tests/GardesDoctrineTests.cs
  modified:
    - src/Chronos/Models/WindowState.cs
    - src/Chronos/Models/UsageSnapshot.cs
    - src/Chronos/Services/CompositeUsageProvider.cs
    - tests/Chronos.Tests/CompositeUsageProviderTests.cs

key-decisions:
  - "SourceReliability.Estimated est REAFFECTE au plancher DEL-04 plutot qu'augmente d'un membre : un nouveau membre aurait traverse silencieusement WindowGaugeViewModel.cs:69/84 et fait afficher le plancher COMME UN EXACT."
  - "La limite d'age est un LAISSEZ-PASSER et non un couperet : DEL-03 re-habilite au-dela. Un releve de 3 h sans activite depuis est EXACT, pas perime — le declarer perime cacherait une information prouvable."
  - "Le plafond absolu n'est pas la limite d'age mais Covers(T), l'horizon de 8 jours deja dans le code : c'est par la que meurt le « 10 % » de deux mois."
  - "Utilization n'est JAMAIS gonflee : le delta change la NATURE du chiffre, jamais sa valeur (mesure : 643 649 933 tokens sur 5 h contre l'ancien plafond de 230 000 000 = 280 %)."
  - "Indisponible EFFACE le pourcentage mais CONSERVE reset, geometrie, statut serveur et depassement : un reset connu reste un fait."
  - "Qualifier reporte StatutServeur/Depassement de la fenetre vivante quand le magasin prend la main — sans quoi HDR-03/HDR-04 mourraient a la premiere substitution."
  - "SourceCapturedAt et IsStale conserves en LEGACY plutot que supprimes : les retirer couterait 4 tests pour zero exigence de cette phase."
  - "CompositeUsageProvider n'a recu AUCUNE injection : ni horloge, ni magasin, ni transcripts. Modification strictement documentaire."

patterns-established:
  - "Falsifiabilite JOUEE et non affirmee : quatre mutations reelles du code de production, echec constate et NOMME, puis revocation prouvee par git diff vide."
  - "Une garde par balayage de texte source impose une CONSIGNE DE REDACTION aux fichiers balayes — consigne ecrite dans le test lui-meme."

requirements-completed: []

# Metrics
duration: 15min
completed: 2026-09-12
---

# Phase 19 Plan 02: La doctrine — quatre états, une classe pure, deux gardes de non-retour Summary

**La doctrine de fraîcheur existe désormais comme une classe PURE de 132 lignes dont les quatre branches sont chacune prouvées nécessaires par une mutation réelle du code de production, elle n'invente aucun pourcentage et ne gonfle jamais `Utilization`, et deux gardes de non-retour ferment définitivement le piège de la recomposition par `new` et la voie d'un rapport entre un comptage de tokens et un plafond — le tout sans qu'aucun des 32 sites `new WindowState` du dépôt n'ait été retouché.**

## Performance

- **Duration:** 15 min
- **Started:** 2026-09-12T03:55:47Z
- **Completed:** 2026-09-12T04:10:47Z
- **Tasks:** 3 / 3 (dont une en TDD : RED puis GREEN)
- **Files modified:** 8 (4 créés, 4 modifiés)
- **Tests:** 664 → **680** (+16), 0 échec

## Task Commits

1. **Task 1 — modèle à quatre états, purement additif** — `128b0d1` (feat)
2. **Task 2 (RED) — les quatre branches contre un squelette compilable** — `794bf0d` (test)
3. **Task 2 (GREEN) — `DoctrineFraicheur`** — `179946e` (feat)
4. **Task 3 — deux gardes de non-retour + le composite dit où vit la doctrine** — `b5150c7` (test)

Aucune étape REFACTOR : l'implémentation est sortie propre du GREEN (13 / 13 au premier passage).

## Accomplishments

- **Quatre états distinguables, zéro site de construction retouché.** `SourceReliability` n'a pas bougé
  (`git diff` ne liste pas `SourceReliability.cs`) ; `WindowState` a gagné deux propriétés `init` nullables
  **sans `required`** — le compte de `required` reste à **2** (`Kind`, `Reliability`). La suite est restée à
  **664 / 664** après la tâche 1 : l'ajout est rigoureusement additif, mesuré et non supposé.
- **La branche fraîche ne paie aucune E/S, et c'est prouvé sémantiquement.**
  `La_branche_fraiche_ne_consulte_PAS_le_journal` compare deux appels — l'un sans journal, l'autre avec un
  journal riche — par **égalité de record**. Si la branche 1 regardait les transcripts, le verdict changerait.
- **Un relevé de trois heures sans activité reste EXACT.** C'est une déduction, pas une indulgence :
  `utilization` est fonction de la consommation, donc « zéro réponse assistant depuis T » implique
  « l'utilisation à `now` égale celle de T ».
- **`Utilization` n'est gonflée dans aucune branche.** `Le_plancher_ne_gonfle_JAMAIS_l_utilization` asserte
  `0.42` à l'identique face à 1 000 000 de tokens observés.
- **Le « 10 % » de deux mois meurt par `Covers(T)`.** Le test rend la prémisse explicite
  (`Assert.False(journal.Covers(t))`) avant d'asserter le verdict : la doctrine **refuse** de produire un
  delta qu'elle ne peut pas garantir plutôt que d'en servir un silencieusement sous-évalué.
- **HDR-03 / HDR-04 survivent à la substitution par le magasin.** Test dédié
  (`Le_statut_serveur_du_tick_survit_a_une_substitution_par_le_magasin`) : le magasin ne persiste ni le statut
  serveur ni le dépassement, la sonde en est l'unique porteuse — sans le report de `Qualifier`, deux
  requirements de la phase 18 disparaîtraient à la première fenêtre mémorisée servie.
- **Le piège de la recomposition cesse d'être silencieux.** Une garde exige que toute propriété d'**instance**
  de `UsageSnapshot` soit nommée dans `CompositeUsageProvider.cs`.

## LES QUATRE MUTATIONS DE BRANCHE — jouées, constatées, révoquées

Exigence centrale du plan : chaque branche doit avoir au moins un test qui **tombe** si la branche disparaît.
Ce n'est pas affirmé, c'est **mesuré**. Chaque mutation a été réellement écrite dans
`src/Chronos/Services/DoctrineFraicheur.cs`, la suite filtrée exécutée, puis la mutation révoquée par
`git checkout --`.

| # | Branche | Mutation réellement jouée | Échecs | Tests tombés (nommément) |
|---|---|---|---|---|
| **1** | Exact / **Frais** (EXA-02) | le `return` de la branche 1 (l. 72-73) mis en commentaire — les relevés frais tombent dans la suite du flux | **5 / 13** | `Releve_sous_la_limite_est_exact_frais`, `La_branche_fraiche_ne_consulte_PAS_le_journal`, `ABesoinDuJournal_est_coherent_avec_Statuer`, `Un_exact_vivant_certifiable_prime_toujours_sur_le_magasin`, `Le_statut_serveur_du_tick_survit_a_une_substitution_par_le_magasin` |
| **2** | Exact / **EncoreValide** (DEL-03) | `!activite.HasActivity` → `activite.HasActivity` (l. 85) : la branche 2 et la branche 3 échangent leurs conditions | **2 / 13** | `Trois_heures_sans_aucune_activite_reste_EXACT_et_non_perime`, `Activite_depuis_le_releve_donne_un_plancher_marque` |
| **3** | Indisponible **hors horizon** (4a, borne du delta) | `!journal.Covers(t)` → `false` (l. 80) : le refus de produire un delta non garanti est neutralisé | **1 / 13** | `Releve_anterieur_a_l_horizon_des_transcripts_est_indisponible_et_non_sous_evalue` |
| **4** | **Démotion** effaçante (4b) | `Utilization = null,` retiré de `Indisponible` (l. 127) : une fenêtre indisponible conserve son pourcentage | **3 / 13** | `Sans_journal_un_releve_trop_vieux_est_indisponible`, `Un_exact_vivant_sans_CapturedAt_est_incertifiable_et_ne_passe_pas_pour_frais`, `La_demotion_conserve_le_reset_et_le_statut_serveur_mais_efface_le_pourcentage` |

**Révocation prouvée :** après chacune des quatre mutations,
`git diff -- src/Chronos/Services/DoctrineFraicheur.cs` rend une chaîne **vide**, et la suite complète est
repassée verte (680 / 680). Aucune mutation n'a été committée.

**Ce que la mutation 2 apprend en plus :** elle fait tomber **deux** tests, un par branche échangée. C'est le
signe que les branches 2 et 3 sont mutuellement exclusives et toutes deux couvertes — une inversion n'a pas
d'endroit où se cacher.

## LA MUTATION EXA-04 — la garde de balayage est falsifiable

Trois lignes réellement insérées dans `DoctrineFraicheur.Statuer`, après `journal.Since(t)` :

```csharp
long plafondTokens = 230_000_000;
long tokensDepuisReleve = activite.Tokens;
_ = tokensDepuisReleve / plafondTokens;
```

Échec obtenu, **nommant le fichier et la ligne** :

```
EXA-04 : un rapport entre un comptage de tokens et un plafond est réapparu. Il n'a AUCUNE réponse
honnête : les limites pondèrent par modèle, les transcripts ne voient ni l'app de bureau ni Cowork, et
la mesure terrain le falsifie d'un facteur supérieur à 2,8. Le delta change la NATURE du chiffre
(« au moins X »), il ne s'y ajoute jamais.
  DoctrineFraicheur.cs:85 — « tokensDepuisReleve / plafond »
```

Mutation **révoquée** (`git diff` vide sur le fichier), garde repassée verte. La garde n'est donc pas
décorative : elle voit une infraction réelle, dans un fichier réel, et le dit.

**Consigne de rédaction imposée par cette garde** — elle est écrite **dans le test lui-même**, parce qu'un
simple commentaire suffirait à la déclencher : dans tout fichier de `Services/` et `Models/`, formuler
toujours « un rapport entre un comptage de tokens et un plafond », jamais les deux termes accolés par un
opérateur. Ce document et tous les commentaires écrits par ce plan s'y conforment.

## Décompte nominatif de la variation 664 → 680 (+16)

| Fichier | Tests ajoutés | Nombre |
|---|---|---|
| `DoctrineFraicheurTests` | `Releve_sous_la_limite_est_exact_frais`, `La_branche_fraiche_ne_consulte_PAS_le_journal`, `Trois_heures_sans_aucune_activite_reste_EXACT_et_non_perime`, `Activite_depuis_le_releve_donne_un_plancher_marque`, `Le_plancher_ne_gonfle_JAMAIS_l_utilization`, `Releve_anterieur_a_l_horizon_des_transcripts_est_indisponible_et_non_sous_evalue`, `Un_exact_vivant_sans_CapturedAt_est_incertifiable_et_ne_passe_pas_pour_frais`, `Sans_journal_un_releve_trop_vieux_est_indisponible`, `Un_exact_vivant_certifiable_prime_toujours_sur_le_magasin`, `La_demotion_conserve_le_reset_et_le_statut_serveur_mais_efface_le_pourcentage`, `Le_statut_serveur_du_tick_survit_a_une_substitution_par_le_magasin`, `ABesoinDuJournal_est_coherent_avec_Statuer`, `La_limite_d_age_est_DERIVEE_de_la_cadence_de_la_sonde` | **+13** |
| `GardesDoctrineTests` | `Toute_propriete_de_UsageSnapshot_est_nommee_dans_la_recomposition_du_composite`, `Aucun_rapport_entre_un_comptage_de_tokens_et_un_plafond_dans_Services_et_Models`, `Le_repli_le_plus_interne_est_bien_celui_a_anciennete_non_bornee` | **+3** |

664 + 13 + 3 = **680**. Aucun test supprimé, aucun test ignoré, aucune perte de couverture nette.

Le plan annonçait « environ 16 tests (12 de doctrine + 3 de gardes) ». Le compte de doctrine est de **13** et
non 12 : un treizième test a été ajouté, `La_limite_d_age_est_DERIVEE_de_la_cadence_de_la_sonde`, qui verrouille
le `key_link` exigé par le frontmatter du plan (« `LimiteAge` **DÉRIVÉE** de la cadence de la sonde, jamais
recopiée »). Sans lui, ce lien n'était garanti que par un critère `grep` à usage unique, pas par un test
permanent — or un `grep` d'acceptation disparaît avec le plan, tandis qu'une valeur littérale réintroduite
plus tard passerait inaperçue.

## Décision : `SourceCapturedAt` et `IsStale` restent en LEGACY

Les supprimer maintenant coûterait **4 tests à réécrire ou supprimer pour zéro exigence de cette phase**.
Ce plan se contente donc d'**écrire la dette dans le code** plutôt que de la laisser implicite : le
commentaire XML de `UsageSnapshot.SourceCapturedAt` dit désormais, en toutes lettres, que le champ est
**legacy**, que la vérité de la doctrine est **par fenêtre** (`WindowState.CapturedAt` + `Provenance`), que
ni lui ni le `IsStale` qui en dérive ne sont bindés où que ce soit, et que la **phase 20** les retirera en
bindant la provenance par fenêtre. Deux seuils de fraîcheur coexistent donc temporairement (6 min pour la
doctrine, 2 min pour `IsStale`) — sans conséquence, puisque le second ne pilote aucun pixel.

## Décisions Made

1. **Réaffecter `Estimated` plutôt qu'ajouter un membre d'enum.** `SourceReliability.Estimated` est **mort en
   production** depuis la phase 16 (aucun provider ne le produit) alors que son câblage de présentation est
   intact : `IsEstimated`, `HasTokens` et `Describe` disent déjà « ce chiffre n'est pas un exact courant ».
   Un membre supplémentaire aurait traversé **silencieusement** `WindowGaugeViewModel.cs:69`
   (`IsEstimated = r == Estimated` ⇒ `false`) et fait afficher le plancher **comme un exact** — la pire issue
   possible pour ce milestone. Le champ `Provenance`, nullable et additif, porte la quatrième distinction.
2. **L'ordre des règles est une position morale autant qu'algorithmique.** La limite d'âge dispense de la
   preuve en deçà ; elle ne l'interdit pas au-delà. Déclarer périmé un chiffre qu'on peut **prouver** juste
   rendrait l'overlay muet précisément quand il est le plus utile.
3. **DEL-04 n'a pas de réponse numérique honnête et n'en a pas besoin.** Le delta accompagne comme matière
   première **brute** (`TokensDepuisReleve`) et change la **qualification** du chiffre. `TokensDepuisReleve`
   est un champ **distinct** d'`EstimatedTokens` : réutiliser ce dernier rattacherait au nouveau chiffre la
   sémantique d'estimation absolue que la phase 16 a tuée.
4. **`0L` et `null` ne disent pas la même chose.** Branche 2 : `TokensDepuisReleve == 0` — *mesuré*, et mesuré
   à zéro. Branche 1 : `null` — *non mesuré*. Confondre les deux ferait d'une absence de mesure une
   affirmation, ce que le projet proscrit depuis la phase 16.
5. **La démotion efface le pourcentage mais conserve le reste.** `WindowGaugeViewModel.Apply` affecte
   `Utilization` **sans consulter** `Reliability` : une fenêtre indisponible qui garderait `0,10` peindrait
   encore l'arc à dix pour cent — le bug survivrait à sa propre correction. En revanche `ResetsAt`,
   `FractionTimeRemaining`, `StatutServeur` et `Depassement` sont **conservés** : un reset connu reste un fait.
6. **La garde de recomposition ne porte que sur les propriétés d'INSTANCE.** `typeof(UsageSnapshot).GetProperties()`
   sans `BindingFlags` remonte aussi la fabrique statique `Empty`, qui n'a rien à faire dans un `new` de
   recomposition : la garde aurait échoué dès sa naissance pour une mauvaise raison, et on l'aurait
   probablement affaiblie pour la faire passer.
7. **Le `new UsageSnapshot` est conservé, et ce n'est pas un oubli.** Un `p with { … }` ferait hériter
   silencieusement du **primaire** tout champ futur, c'est-à-dire produire une valeur **fausse** là où le
   `new` produit un `null`. `grep -c "p with"` rend **0** et `grep -c "new UsageSnapshot"` rend **1**.

## Deviations from Plan

**Aucune déviation de fond.** Le plan a été exécuté comme écrit. Deux précisions, toutes deux additives et
aucune ne modifiant un comportement prescrit :

1. **[Ajout dans le périmètre — verrouillage d'un `key_link`]** Un treizième test de doctrine,
   `La_limite_d_age_est_DERIVEE_de_la_cadence_de_la_sonde`, a été ajouté pour rendre **permanent** le lien
   `LimiteAge` ⇄ `CadenceNominale` que le frontmatter du plan exigeait mais que seul un critère `grep`
   d'acceptation garantissait. Fichier : `tests/Chronos.Tests/DoctrineFraicheurTests.cs`. Commit : `794bf0d`.
2. **[Précision de conception — garde 1]** `GetProperties()` a été restreint à
   `BindingFlags.Public | BindingFlags.Instance` (voir décision n° 6). Le plan disait
   `typeof(UsageSnapshot).GetProperties()` sans qualifier ; le prendre au pied de la lettre aurait exigé que
   le mot `Empty` figure dans `CompositeUsageProvider.cs`, ce qui n'a aucun sens. Commit : `b5150c7`.

La matrice de `ABesoinDuJournal_est_coherent_avec_Statuer` compte **cinq** cas et non quatre : le cas
« magasin sans horodatage » (candidat non nul mais d'âge indéterminable) a été ajouté, car c'est le seul où
`ABesoinDuJournal` rend `false` alors qu'un **candidat existe** — la branche de garde la plus facile à casser
par inadvertance en 19-03.

**Total deviations:** 2 (toutes deux additives, aucune régression)
**Impact on plan:** aucun. Aucun fichier hors des 7 listés au frontmatter du plan, plus
`tests/Chronos.Tests/CompositeUsageProviderTests.cs` que la tâche 3 désignait explicitement.

## Issues Encountered

Aucune. L'étape RED a produit **12 échecs sur 13**, tous **comportementaux** et non de compilation : le
treizième (`La_limite_d_age_est_DERIVEE_de_la_cadence_de_la_sonde`) passait déjà, parce que le squelette
portait la vraie valeur de `LimiteAge` — un champ `static readonly` ne peut pas raisonnablement lever sans
casser l'initialisation du type entier. Squelette compilable (`NotImplementedException`) conformément au
précédent 18-01 : `tests/Chronos.Tests` a un `ProjectReference` vers `Chronos`, un commit non compilable
rendrait `dotnet test` non invocable dans son intégralité.

## Vérifications de sécurité et de non-régression

| Contrôle | Attendu | Obtenu |
|---|---|---|
| `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` (avant ET après) | `518 1783863147` | `518 1783863147` ✅ |
| Fichiers SOURCE contenant `RefreshAsync` | 2 | 2 ✅ |
| Aucune requête réseau depuis un test | aucune | aucune ✅ (les 16 nouveaux tests sont purs : lecture de `.cs` du dépôt et objets en mémoire) |
| Écriture de test sous le vrai `%APPDATA%\Chronos\` | aucune | aucune ✅ |
| `required` dans `WindowState.cs` | 2 | 2 ✅ |
| `SourceReliability.cs` modifié | non | non ✅ (absent de `git diff --name-only`) |
| `grep -c "System.Windows"` sur `DoctrineFraicheur.cs` et `ProvenanceReleve.cs` | 0 | 0 / 0 ✅ |
| `grep -c "DateTimeOffset.UtcNow"` sur `DoctrineFraicheur.cs` | 0 | 0 ✅ |
| `grep -c "CadenceNominale"` sur `DoctrineFraicheur.cs` | 1 | 1 ✅ |
| `grep -cE "(ReadAsync\|File\.\|Directory\.)"` sur `DoctrineFraicheur.cs` | 0 | 0 ✅ |
| Constructeur de `CompositeUsageProvider` intact | 1 | 1 ✅ |
| `private readonly` dans `CompositeUsageProvider.cs` | 2 | 2 ✅ |
| `new UsageSnapshot` / `p with` | 1 / 0 | 1 / 0 ✅ |
| `UnExactADejaEteObtenu` dans `CompositeUsageProvider.cs` | ≥ 1 | 1 ✅ |
| `CheminSourcesChronos` / chemin de fichier d'assembly dans `GardesDoctrineTests.cs` | ≥ 1 / 0 | 2 / 0 ✅ |
| `CompositeUsageProviderTests` | exactement 14 | 14 ✅ (aucune suppression, aucun ajout, aucune assertion touchée) |
| `ServicesLayerPurityTests` + `NormalisationUniqueTests` + `CompositionRootTests` | 0 échec | 10 / 10 ✅ |
| `La_sonde_d_en_tetes_est_le_PRIMAIRE` (garde phase 18) | 0 échec | 1 / 1 ✅ |
| `dotnet build Chronos.sln -c Release` | 0 erreur | 0 erreur ✅ (2 avertissements xUnit2031 **préexistants**, hors périmètre) |
| Suite complète `dotnet test Chronos.sln -v q --nologo` | 0 échec | **680 / 680, 0 échec** ✅ |

## Statut des requirements

**EXA-02, EXA-04, DEL-03 et DEL-04 restent PENDING, et c'est intentionnel.**

La doctrine **existe**, elle est pure, prouvée et falsifiable — mais **rien ne l'appelle** à ce commit :
`grep -rl "DoctrineFraicheur" src/Chronos` ne remonte que **deux** fichiers, sa propre définition et le
**commentaire** de `CompositeUsageProvider.cs` qui dit où elle vit — **aucun appel**. Aucun relevé trop vieux n'est
aujourd'hui rejeté par l'application ; aucun plancher n'est affiché ; le cadran sert toujours le « 10 % ».

- **EXA-02 / DEL-03 / DEL-04** : câblage dans `LastExactUsageProvider` — plan **19-03**.
- **EXA-04** : la garde de non-retour est posée et falsifiable, mais l'interdiction ne devient un fait
  d'exécution que lorsque la branche plancher tourne réellement — plan **19-03**, cochage en **19-04**
  (affichage « ≥ N % »).

Cocher ces requirements maintenant affirmerait un comportement que l'application n'a pas — exactement
l'erreur nommée par le précédent 17-01 et répétée en 19-01.

## Next Phase Readiness

Le plan **19-03** peut démarrer sans dépendance non satisfaite. Ce plan n'a touché **ni**
`LastExactUsageProvider`, **ni** `App.xaml.cs`, **ni** aucun ViewModel ni XAML.

Points d'attention pour 19-03 :

- **Utiliser `ABesoinDuJournal` pour ne payer la passe disque que si elle change le verdict** — son contrat de
  cohérence avec `Statuer` est verrouillé par test sur cinq cas, la paresse est donc sûre par construction.
- **Les deux fenêtres doivent partager UNE seule passe disque** : `TranscriptActivityLog` est pur et
  interrogeable N fois, c'est exactement ce pour quoi la phase 16 l'a séparé de l'E/S.
- **Convertir l'`IOException` de `SourceActiviteMemoisee`** (panne sans journal connu) en `journal: null`,
  c'est-à-dire en branche indisponible — le décorateur ne le fait délibérément pas.
- **`WeeklyRecalibration` a un bug latent à corriger dans le même mouvement** : sa garde actuelle
  `Exact && ResetsAt != null` laisserait `Apply` écraser le `resets_at` réel d'une fenêtre hebdo passée en
  plancher. La garde doit porter sur `ResetsAt is not null` **seul** (correctif d'une ligne, les 7 tests
  existants restent verts).
- **Enregistrer `SourceActiviteMemoisee` dans le graphe DI dans le MÊME commit que son consommateur**
  (dette consignée par 19-01).

Hors périmètre, non anticipé ici : EXA-03 et EXA-06 (phase 20), le trou de la `UniformGrid` des réglages, la
dette de durée des tests, et la surcharge « ≥ » de `PercentFormatter` (phase 19-04).

---
*Phase: 19-nouvelle-doctrine-du-composite*
*Completed: 2026-09-12*

## Self-Check: PASSED

9 / 9 fichiers déclarés présents sur disque. 4 / 4 commits de tâche présents dans l'historique git.
Suite complète : 680 / 680, 0 échec. Coffre `oauth.dat` inchangé (518 o, mtime 1783863147).
Aucune mutation résiduelle : `git diff` vide sur `DoctrineFraicheur.cs` après les cinq mutations jouées.
