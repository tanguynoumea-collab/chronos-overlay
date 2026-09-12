---
phase: 26-traite-veut-enfin-dire-quelque-chose
plan: 04
subsystem: documentation
tags: [contrat-des-hooks, garde-de-non-derive, garde-croisee, mutation, cloture-de-phase, tracabilite]

sha_entree_de_phase: 07ee784
sha_entree_de_plan: d28392e

requires:
  - phase: 25-le-contrat-d-evenements-refonde
    provides: "docs/hooks-contract.md et ContratHooksDocumenteTests — la garde de non-dérive EVT-05, et le §3 qui portait la divergence transmise"
  - phase: 26-traite-veut-enfin-dire-quelque-chose
    provides: "SessionTreatmentTracker refondé (26-01), ArchiveStore sans durée de vie (26-02), les huit menus et leurs libellés (26-03) — le câblage que le document devait enfin décrire"
provides:
  - "docs/hooks-contract.md §3 — la règle de traitement RÉELLEMENT câblée, la divergence transmise refermée et dite telle"
  - "Le_document_ne_transmet_plus_de_divergence_a_la_phase_26 — garde textuelle, anti-muette"
  - "Le_document_dit_la_regle_de_traitement_reellement_cablee — garde CROISÉE document ↔ SessionTreatmentTracker, falsifiabilité mesurée par mutation"
  - "26-VALIDATION.md clos — zéro case « (à mesurer) », tous chiffres observés, cinq écarts consignés"
affects: [milestone-v1.6-cloture, republication-de-l-exe]

tech-stack:
  added: []
  patterns:
    - "Une garde documentaire doit porter sur une chaîne qui N'EXISTE PAS avant l'édition : si la chaîne cherchée figure déjà ailleurs dans un autre sens, la garde naît verte et ne garde rien"
    - "Une garde croisée document ↔ code se prouve par mutation des DEUX côtés : muter le document seul ne dit pas qu'elle voit le code"
    - "Une carte de validation se clôt sur des chiffres relevés dans une sortie de runner ; un attendu qui ne tombe pas juste se justifie, il ne s'ajuste pas"

key-files:
  created:
    - .planning/phases/26-traite-veut-enfin-dire-quelque-chose/26-04-SUMMARY.md
  modified:
    - docs/hooks-contract.md
    - tests/Chronos.Tests/ContratHooksDocumenteTests.cs
    - .planning/phases/26-traite-veut-enfin-dire-quelque-chose/26-VALIDATION.md

decisions:
  - "La garde croisée porte sur la chaîne exacte « transition observée sur la MÊME source » et NON sur « même source », qui figure déjà deux fois au §5 dans un sens sans rapport : une garde adossée à ces deux mots aurait été verte AVANT l'édition. La mutation M2 le prouve — abaisser la casse fait passer le compte de « même source » de 2 à 3 et rend quand même la garde ROUGE."
  - "Le prédicat EstAttente est DÉCOUPÉ du fichier avant d'être lu (de « EstAttente(SessionActivity » au premier point-virgule) : le nom WaitingDeduced figure aussi dans les commentaires du détecteur, et un commentaire ne câble rien. Sans ce découpage, la mutation M3 serait restée verte."
  - ".planning/REQUIREMENTS.md n'a PAS été modifié : les quatre cases TRT étaient déjà cochées et les quatre lignes de traçabilité déjà à Complete, posées au fil des plans 26-01/26-02/26-03. L'état final est vérifié conforme ; le fait que TRT-04 ait été coché une vague avant l'arrivée de son second volet est consigné comme écart, pas lissé."
  - "La dette de XML-doc d'ArchiveStore.PurgerPrefixe est REPORTÉE, pas corrigée : le plan 26-04 ne la demande pas, et le plan 26-02 avait gelé ce bloc par une règle dure."

metrics:
  duration: "~30 min"
  completed: 2026-09-13
  tasks: 2
  files_changed: 3
---

# Phase 26 Plan 04 : Le contrat documenté — Summary

**Le document a cessé de décrire un défaut qu'il n'a plus, et deux gardes — dont une croisée
document ↔ code, prouvée falsifiable par trois mutations — refusent désormais que le §3 et le détecteur se
contredisent en silence. La carte de validation de la phase est close sur des chiffres relevés dans une
sortie de runner.**

- **Phase :** 26 — « Traité » veut enfin dire quelque chose (vague 4/4, dernière du milestone v1.6)
- **Plan :** 04 — le contrat documenté
- **Tâches :** 2 / 2
- **Duration :** ~30 min
- **Exigences :** TRT-01, TRT-02, TRT-03, TRT-04 (le document suit le code ; la traçabilité est close)

## SHA de base, relevé en début de plan

`git rev-parse HEAD` à l'entrée du plan : **`d28392e`** (fin du plan 26-03, commit
`docs(26-03): complete le geste explicite et les libelles qui disent la verite`). C'est **la** base
opposable du critère « la vague 4 ne touche pas `src/Chronos` ». `07ee784` ne conviendrait pas : il
montrerait les 20 fichiers des vagues 1 à 3.

```
git diff --stat d28392e..HEAD -- src/Chronos   →   (vide)
```

## Comptes de tests — avant / après

| Étape | Commande | Total | Échecs | Durée |
|---|---|---|---|---|
| Entrée de plan (baseline, sur `d28392e`) | `dotnet test Chronos.sln -c Debug --nologo -v q` | **886** | 0 | 6 s |
| Fin tâche 1 (filtre `~ContratHooksDocumente`) | `… --filter "FullyQualifiedName~ContratHooksDocumente"` | **9** (7 + 2) | 0 | 11 ms |
| Fin tâche 1 (suite complète) | idem sans filtre | **888** (+2) | 0 | 6 s |
| Fin tâche 2 | idem | **888** (+0) | 0 | 5 s |
| **Vérification, 1re exécution** | idem | **888** | **0** | **6 s** |
| **Vérification, 2e exécution** | idem | **888** | **0** | **6 s** |

Chaîne annoncée **886 → 888 (T1, +2) → 888 (T2, +0)** : **tenue exactement**, aucun écart à justifier.
`26-VALIDATION.md` annonçait **888** après 26-04 : **888 mesuré**, deux fois de suite.
`dotnet build Chronos.sln -c Debug --nologo` : **0 erreur, 0 avertissement** à chaque commit.

## Les deux cas ajoutés — verts dès leur écriture, et prouvés falsifiables

Ce plan n'a **aucune étape rouge dans son propre ordre d'exécution**, et c'est annoncé par le plan comme
par `26-VALIDATION.md` (« les deux gardes de 26-04 T1 » figurent parmi les « verts dès leur écriture »). Une
garde de non-dérive n'est pas un test de développement : sa valeur est d'attraper la **prochaine**
divergence, pas de rougir sur celle qu'on vient de refermer.

**Ce qui remplace le rouge-avant : une mutation mesurée**, jouée dans un **`git worktree` jetable**, détaché
sur `a54f5e3`, créé **hors du dépôt** (sous le dossier temporaire de session). Référence dans le worktree
avant mutation : `0 échec, 9 réussites, total 9`.

| Mutation | Portée | Attendu | **Mesuré** |
|---|---|---|---|
| **M1** — remettre au §3 le titre « ⚠ Divergence connue — transmise à la phase 26, NON corrigée ici » | document | garde 1 rouge | **`échec : 1, réussite : 8, total : 9`** — unique `[FAIL]` : `Le_document_ne_transmet_plus_de_divergence_a_la_phase_26` |
| **M2** — abaisser la casse : « transition observée sur la **même** source » | document | garde 2 rouge | **`échec : 1, réussite : 8, total : 9`** — unique `[FAIL]` : `Le_document_dit_la_regle_de_traitement_reellement_cablee`. **Et `grep -c "même source"` passe de 2 à 3** |
| **M3** — retirer `WaitingDeduced` du prédicat `EstAttente` | **code** | garde 2 rouge | **`échec : 1, réussite : 8, total : 9`** — unique `[FAIL]` : `Le_document_dit_la_regle_de_traitement_reellement_cablee` |

**Ce que M2 prouve exactement.** La chaîne `"même source"` figure **déjà deux fois** dans le document
(§5, l. 213 et 227, « Relevé le 2026-09-12, même source »), dans un sens qui n'a aucun rapport avec la règle
de traitement. Une garde qui s'en serait contentée aurait été **verte avant même l'édition** — donc vacueuse.
La mutation M2 **augmente** ce compte (2 → 3) et rend pourtant la garde **rouge** : la démonstration est
directe, la garde ne s'appuie pas dessus. Le compte de `"même source"` dans le document livré reste **2,
inchangé**, la chaîne ajoutée portant `MÊME` en capitales et `grep` étant sensible à la casse.

**Ce que M3 prouve, et que M1/M2 ne prouvent pas.** La garde est **bidirectionnelle** : elle attrape la
dérive du **code** autant que celle du document. Le détail qui la rend efficace est le **découpage du
prédicat** : `WaitingDeduced` figure aussi dans les commentaires de `SessionTreatmentTracker.cs`, et un
commentaire ne câble rien ; la garde lit donc le seul corps de `EstAttente`, de
`EstAttente(SessionActivity` au premier point-virgule.

### Révocation — prouvée

```
git checkout -- .            (dans le worktree)   → git status --porcelain : vide
git worktree remove / prune  (dans le dépôt)      → git worktree list      : 1 seule entrée
grep -rl "MUTATION-B5" .                          → 0 fichier
git status --porcelain                            → vide
```

*(Note d'exécution : `git worktree remove` a d'abord échoué avec « Filename too long » sur les artefacts
`bin`/`obj` de la compilation du worktree ; le dossier a été supprimé par `rm -rf`, puis `git worktree
prune` a nettoyé la référence. Résultat final identique et vérifié.)*

## Tâche 1 — le §3 dit la règle réellement câblée

### Ce qui a disparu du document

La sous-section « **⚠ Divergence connue — transmise à la phase 26, NON corrigée ici** » (13 lignes)
annonçait que `SessionTreatmentTracker.IsWaiting` ne reconnaissait comme attentes que `WaitingTurn` et
`WaitingAttention`, que `WaitingDeduced` en était exclue, et que **la phase 26 s'en chargerait**. Le plan
26-01 s'en est chargé. Laisser l'annonce, c'était décrire un câblage qui n'existe plus — la faute même que
ce document existe pour empêcher, et le mécanisme exact de la panne que la phase 25 a réparée.

### Ce qui l'a remplacée

« **Ce que « traité » veut dire (phase 26 — TRT-01, TRT-02)** », qui décrit la règle telle qu'elle tourne :
la transition doit être **observée sur la MÊME source** ; `WaitingDeduced` **est** une attente pour le
détecteur ; `Unknown` **ne ferme aucun** épisode ; une bascule de source **n'est pas** une transition (avec
la mesure du 2026-09-12 : quatre cent soixante-dix-huit minutes contre un seuil de quatre cent
quatre-vingts, puis la bascule, puis six heures de masquage) ; et l'épisode d'attente est **daté par
l'instant que le SIGNAL porte**, jamais par l'horloge du guetteur.

**Rien d'autre n'a bougé** dans le document : ni le tableau des cinq états, ni le rang d'urgence 3, ni la
phrase « `WaitingDeduced` n'est JAMAIS écrite dans un fichier d'état », ni le §1 et ses deux marqueurs, ni
le §5, ni les trois marqueurs de trous, ni la date du relevé.

### Les greps, valeur d'entrée et valeur de sortie

| Grep sur `docs/hooks-contract.md` | Attendu | **Entrée** | **Sortie** | |
|---|---|---|---|---|
| `grep -c "transmise à la phase 26"` | 0 | **1** | **0** | ✅ |
| `grep -c "NON corrigée ici"` | 0 | **1** | **0** | ✅ |
| `grep -c "TRT-01"` | ≥ 1 | **0** | **1** | ✅ |
| `grep -c "transition observée sur la MÊME source"` | = 1 | **0** | **1** | ✅ |
| `grep -c "même source"` | = 2, **inchangé** | **2** | **2** | ✅ — la preuve que la garde croisée ne s'y adosse pas |
| `grep -c "WaitingDeduced"` | ≥ 3 | **3** | **3** | ✅ — *valeur d'entrée mesurée avant édition : 3, identique. Une ligne quitte le document (le bloc de divergence), une ligne y entre (la nouvelle sous-section)* |
| `wc -l` | ≥ 90 | **365** | **372** | ✅ |
| `grep -c "EVENEMENTS-CABLES:debut\|EVENEMENTS-CABLES:fin\|## 5\."` | ≥ 3 | **8** | **8** | ✅ — marqueurs de la garde existante intacts |
| `grep -c "prec.Source == v.Source"` sur `SessionTreatmentTracker.cs` | ≥ 1 | **1** | **1** | ✅ (fichier non touché) |

### Tests préexistants de `ContratHooksDocumenteTests`

**Les sept sont verts**, en particulier ceux qui contrôlent la table du §1 (`La_table_documentee_liste_
EXACTEMENT_les_evenements_cables`, `Chaque_ligne_documentee_porte_le_matcher_reellement_installe`,
`Chaque_ligne_documentee_porte_le_role_reellement_cable`), la date du relevé et les trois trous documentaires
(`Le_document_porte_les_trois_trous_documentaires_avec_leur_date`), plus les 33 noms du catalogue.
**Aucun test existant de ce fichier n'a été modifié.**

## Tâche 2 — la carte de validation, close sur des chiffres mesurés

`26-VALIDATION.md` : **0 case « (à mesurer) » restante** (mesuré : `grep -c "(à mesurer)"` = **0**),
**8 mentions de `TRT-0`** (critère ≥ 4), 257 lignes.

Ce qui y a été inscrit, et qui n'y était pas :

- **les quatre totaux mesurés** — 876 / 879 / 886 / 888, chacun avec sa chaîne interne et sa durée, et la
  baseline d'entrée de phase remesurée à **868 / 0 échec / 5 s** ;
- **les deux exécutions consécutives de clôture** — `888 / 0 échec / 6 s`, deux fois ;
- **les huit rouges-avant**, nominativement, avec leur **ligne** dans le fichier de tests et la **cause**
  exacte de leur échec, plus la neuvième ligne (le cas des sept heures) et son rouge **mesuré hors commit** ;
- **la falsifiabilité des gardes de 26-04**, avec les trois mutations et leur révocation prouvée ;
- **la couverture des quatre exigences TRT**, chacune avec ses preuves nommées, leur statut rouge-avant
  quand il existe, et son verdict ;
- **les cinq critères du ROADMAP**, prouvés **en salle**, et — ce qui manquait — la mention explicite
  qu'**aucun des cinq n'est observé in vivo**, avec la **chaîne de quatre préalables** (republier l'exe →
  relancer l'overlay pour réconcilier `settings.json` → rouvrir les sessions → attendre une vraie demande) ;
- **les douze acquis des phases 20 à 25**, remesurés un par un ;
- **cinq écarts**, chacun expliqué, aucun lissé.

### Les acquis, remesurés à la clôture

| Acquis | Attendu | **Mesuré** |
|---|---|---|
| `grep -cF "?? new " src/Chronos/Services/DiagnosticService.cs` | 2 | **2** ✅ |
| `grep -c "new SessionMonitor" …/DiagnosticService.cs` | 0 | **0** ✅ |
| `grep -c "MotifMasquage" …/DiagnosticService.cs` | 3 | **3** ✅ ; énumération à **2** valeurs (`Archivee`, `Traitee`) ✅ |
| `DataTemplate x:Key=` dans `SessionStyles.xaml` | 8 | **8** ✅ |
| `Read(now) => Inspecter(now).Visibles` unique | 1 | **1** ✅, `Read_rend_exactement_les_sessions_visibles_d_Inspecter` vert |
| `EcritureEtatSession.Appliquer(` dans `App.xaml.cs` / `.tmp-` | 1 / 0 | **1 / 0** ✅ |
| `grep -c "DateTimeOffset.UtcNow"` × 4 fichiers (26-01/26-02) | 0 partout | **0, 0, 0, 0** ✅ |
| `grep -c "FromHours(6)" …/TreatedStore.cs` | 0 | **0** ✅ |
| `grep -c "RetentionMax" …/TreatedStore.cs` | ≥ 3 | **4** ✅ |
| Diff de `SessionHookProcessor.cs`, `EcritureEtatSession.cs`, `CatalogueEvenementsHooks.cs`, `SessionHookInstaller.cs` depuis `07ee784` | **vide** | **vide** (0 ligne de diff pour chacun) ✅ |
| `PurgerPrefixe` | intact | **intact** ✅ (seule la XML-doc de CLASSE d'`ArchiveStore` a bougé en 26-02) |
| Suites de gardes groupées (`NormalisationUnique`, `ServicesLayerPurity`, `CompositionRoot`, `GardesDoctrine`, `GardesPerimetre`, `Inspection`, `Arbitrage`, `EcritureEtatSession`, + les gardes nommées de 21/24) | 0 échec | **74 / 74 verts** ✅ |
| Preuves nommées des quatre TRT, relancées nominativement | 0 échec | **14 / 14 verts** ✅ |
| `ContratHooksDocumenteTests` | tous verts | **9 / 9 verts** ✅ |

## Déviations

### 1. `.planning/REQUIREMENTS.md` n'a **pas** été modifié — l'état cible y était déjà

Le plan prescrivait de passer les quatre cases `- [ ] **TRT-0x**` à `- [x]` et les quatre lignes de
traçabilité de `Pending` à `Complete`. **Ces huit changements avaient déjà été faits**, au fil des plans :

| Exigence | Cochée au commit | Plan |
|---|---|---|
| TRT-01, TRT-02 | `ebd281c` | 26-01 |
| **TRT-04** | `7660ef7` | **26-02** |
| TRT-03 | `d28392e` | 26-03 |

Vérifié à la clôture : `- [x] **TRT-0` = **4**, `| TRT-0. | Phase 26 | Complete |` = **4**,
`| TRT-0. | Phase 26 | Pending |` = **0**. **Aucune édition n'était nécessaire, aucune n'a été faite.**

**La réserve, dite plutôt que lissée.** `26-VALIDATION.md` répartit **TRT-04 sur 26-02 + 26-03**. Le
marquage a eu lieu en **26-02**, alors que seul le volet **code** (`ArchiveStore` sans durée de vie) était
livré ; le volet **libellé** (« Archiver définitivement (ne revient jamais) » sur les huit menus) n'est
arrivé qu'au plan 26-03, commit `923d1d5`, tenu par
`Les_huit_menus_offrent_le_geste_explicite_et_disent_la_verite`. **L'état final est cohérent et la case est
légitime aujourd'hui — mais elle a précédé sa seconde preuve d'une vague.** C'est consigné comme écart n° 4
de `26-VALIDATION.md`. Aucune case n'est cochée sans preuve nommée : l'interdit absolu du milestone tient.

### 2. Aucune étape rouge dans l'ordre d'exécution de ce plan — et ce qui la remplace

Les deux cas ajoutés sont des **gardes**, annoncées « vertes dès leur écriture » par le plan et par
`26-VALIDATION.md`. Une garde de non-dérive n'a pas à rougir sur le défaut qu'on vient de refermer ; sa
valeur est d'attraper le **suivant**. Le plan exigeait donc explicitement une **mutation mesurée** à la
place — trois ont été jouées (M1, M2, M3), chacune isolant nominativement la garde attendue, et la
révocation est prouvée. Ce n'est pas une déviation par rapport au plan, c'est sa lettre ; c'est noté ici
parce que le SUMMARY d'un plan sans rouge doit dire **pourquoi** il n'en a pas.

### 3. Trois tentatives d'écriture du fichier de tests avant la bonne — sans conséquence sur le dépôt

La première rédaction du fichier de tests, passée par un script, a vu ses séquences d'échappement
(`"\r\n"`, `"\n"`, `\"`) converties en caractères réels par la chaîne d'outils, produisant **12 erreurs de
compilation**. Le fichier a été **restauré par `git checkout`** — donc jamais commité dans cet état — et
réécrit par l'outil d'édition. **La règle dure « compilabilité à CHAQUE commit » est tenue :** le premier
commit du plan (`a54f5e3`) compile avec **0 erreur, 0 avertissement**. C'est consigné parce qu'un SUMMARY
qui tait ses fausses routes laisse croire qu'il n'y en a pas.

### 4. Format des messages de commit

Les deux commits de ce plan portent un pied de page `Co-Authored-By:`, absent des commits antérieurs du
dépôt. Il est requis par la configuration de l'outil d'exécution. Le reste du format (type, portée
`(26-04)`, message en ASCII sans accents, corps en liste) suit la convention des vingt-six phases
précédentes.

## Known Stubs

Aucun. Ce plan n'écrit ni code de production ni chemin d'exécution : un document Markdown, deux gardes de
test, une carte de validation.

## Deferred Issues

- **`src/Chronos/Services/ArchiveStore.cs`, l. 99 — XML-doc de `PurgerPrefixe`.** La phrase
  « Distinct aussi d'`Add`, qui applique au passage la purge des entrées expirées » décrit un comportement
  **retiré au plan 26-02**. Le plan 26-02 avait gelé ce bloc par une règle dure (« `PurgerPrefixe` reste
  INTACT ») et l'avait reportée au plan 26-04 ; **le plan 26-04 ne demande pas de la corriger, et elle ne
  l'est pas.** Vérifié : la phrase est toujours là (`grep -c "purge des entrées expirées"` = 1, l. 99), et
  `PurgerPrefixe` est intact.
  **Portée réelle :** commentaire périmé, aucune garde ne le contredit, aucun comportement n'en dépend.
  **Reportée au-delà de la phase 26** — consignée comme écart n° 5 de `26-VALIDATION.md`.

- **Aucun des cinq critères du ROADMAP n'est observé in vivo.** Ce n'est pas une dette de code : c'est un
  fait de déploiement. La configuration de l'utilisateur ne porte encore que les **5 hooks v3.0.2**, sans
  `PermissionRequest` et sans `matcher` sur `Notification`. Chaîne de préalables, dans l'ordre :
  (1) republier l'exe mono-fichier win-x64, version incrémentée et portée dans le nom du fichier publié ;
  (2) **relancer** l'overlay — la réconciliation de `~/.claude/settings.json` n'a lieu qu'au lancement, en
  mode overlay uniquement, et seulement si le widget de sessions est activé ; (3) **rouvrir** les sessions
  Claude Code — la configuration des hooks est lue au démarrage d'une session ; (4) attendre une vraie
  demande de permission, y répondre, vérifier que la session quitte le widget — puis en laisser une expirer
  et vérifier qu'elle **ne** le quitte **pas**.

## Sécurité — vérifié

- Ce plan **n'exécute aucun test qui écrive** : il n'ajoute que deux gardes en **lecture seule** de fichiers
  du dépôt (`docs/hooks-contract.md`, `src/Chronos/Services/SessionTreatmentTracker.cs`). Aucun réseau,
  aucun `%APPDATA%`, aucun `~/.claude`, aucun jeton, aucune horloge.
- `%APPDATA%\Chronos\archived.json` : **84 octets**, mtime **12 juillet 16:38** — **inchangé**.
- `%APPDATA%\Chronos\treated.json` : **2 octets**, mtime **12 septembre 21:21**, antérieur à cette
  session — **inchangé**.
- `%APPDATA%\Chronos\sessions\` : **jamais écrit**, listé en lecture seule pour le seul compte d'entrées
  (**65**). Il bouge tout seul — les hooks v3.0.2 de l'utilisateur sont vivants ; rien n'a été « réparé ».
- Le vrai `~/.claude/settings.json` n'a pas été écrit. **Aucune sonde.**
- `oauth.dat` : **non approché**, et **son mtime n'a PAS été vérifié**, conformément à la consigne.
- L'overlay (pid 119412) n'a été **ni lancé ni tué**. Aucun test n'affiche de fenêtre.
- Aucune requête réseau. **Aucune dépendance NuGet** : `Chronos.csproj` et `Chronos.Tests.csproj` n'ont pas
  été touchés.
- Le `git worktree` de mutation a été créé **hors du dépôt**, sous le dossier temporaire de session, et
  **révoqué** : `git worktree list` = 1 entrée, `git status --porcelain` vide, `grep -rl "MUTATION-B5"` = 0.

## Ce que la suite peut tenir pour acquis

- **`docs/hooks-contract.md` ne transmet plus rien à personne.** Le §3 décrit la règle qui tourne, et deux
  gardes le tiennent — dont une **croisée**, qui rougit aussi bien si le document dérive que si le détecteur
  dérive. La garde EVT-05 de la phase 25 est intacte et couvre toujours le §1, le §5, la date et les 33 noms.
- **Les quatre exigences TRT sont tracées comme couvertes, chacune par au moins une preuve nommée, verte,
  et non vacueuse.** Huit des preuves ont été mesurées **rouges avant**.
- **`26-VALIDATION.md` est close** : aucune case « (à mesurer) », cinq écarts consignés, et ce qui ne peut
  pas encore être observé est marqué comme tel avec sa chaîne de préalables — jamais laissé vide.
- **La phase 26, et avec elle le milestone v1.6, n'a plus de travail en salle.** Ce qui reste est un acte de
  déploiement : republier, relancer, rouvrir, observer.

## Commits

1. **Task 1 : le §3 dit la règle réellement câblée, deux gardes le tiennent** — `a54f5e3`
2. **Task 2 : la carte de validation close sur des chiffres mesurés** — `0cb2b48`

## Self-Check: PASSED

- Fichiers annoncés : tous présents — `26-04-SUMMARY.md`, `docs/hooks-contract.md`,
  `tests/Chronos.Tests/ContratHooksDocumenteTests.cs`, `26-VALIDATION.md`.
- Commits annoncés : `a54f5e3`, `0cb2b48` — les deux retrouvés dans `git log`.
- Suite complète relancée **deux fois** après le dernier commit de code/tests : **888 / 0 échec / 6 s**, les
  deux fois.
- `git diff --stat d28392e..HEAD -- src/Chronos` : **vide**.
- Arbre de travail **propre** après la mutation et sa révocation (`git status --porcelain` vide,
  `git worktree list` revenu à 1, `grep -rl "MUTATION-B5"` = 0).
