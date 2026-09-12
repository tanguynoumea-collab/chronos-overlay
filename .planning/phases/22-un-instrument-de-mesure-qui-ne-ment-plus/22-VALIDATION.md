---
phase: 22
slug: un-instrument-de-mesure-qui-ne-ment-plus
status: validated
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-12
validated: 2026-09-12
---

# Phase 22 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net8.0-windows) — `tests/Chronos.Tests` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Quick run (sessions + diagnostic)** | `… --filter "FullyQualifiedName~Inspection\|FullyQualifiedName~Affichage\|FullyQualifiedName~Diagnostic\|FullyQualifiedName~Sessions\|FullyQualifiedName~Treated\|FullyQualifiedName~GardesPerimetre\|FullyQualifiedName~CompositionRoot"` |
| **Baseline d'entrée de phase** | **719 tests / 0 échec / ~5 s** (fin de phase 21) |
| **Cible de fin de phase** | **0 échec, aucun test supprimé** — environ **+22** attendus (13 en 22-01, 5 en 22-02, 5 en 22-03, dont un test existant étendu) |
| **Résultat mesuré (2026-09-12)** | **747 tests / 0 échec / 4 s**, sur **deux exécutions consécutives**. Soit **+28** sur la baseline de 719 : +16 en 22-01, +7 en 22-02, +5 en 22-03. **Aucun test supprimé.** |

## Le critère de cette phase n'est PAS un chiffre de couverture

Cette phase ne supprime aucun code et ne supprime aucun test. Le critère opérationnel est donc simple :

> **0 échec, total ≥ 719, et les quatre critères de succès du ROADMAP prouvés un à un.**

Un total inférieur à 719 doit être expliqué **nominativement**, jamais absorbé en ajustant le chiffre
attendu. La phase 21 a fait exception (recul assumé de 33, justifié test par test) ; ce n'est pas le cas ici.

## Ce qui rend cette phase vérifiable — et ce qui la rendrait creuse

OBS-01 peut être « satisfait » de deux façons qui se ressemblent à la lecture d'un rapport et qui n'ont rien
à voir à six semaines d'écart :

| Livraison | Ce qu'on lit le jour J | Ce qu'on lit après la phase 23 |
|---|---|---|
| **Copie de comportement** — le diagnostic refait les filtres du widget | identique au widget | **faux**, en silence : le widget a changé, pas la copie |
| **Partage d'instance** — le diagnostic interroge le moniteur du conteneur | identique au widget | **toujours identique**, sans qu'une ligne du diagnostic ne change |

C'est la condition de placement de la phase en n°2 du milestone : l'instrument doit rester vrai pendant que
les phases 23 à 26 modifient l'objet mesuré. Une vérification qui se contenterait de comparer deux sorties
le jour de la livraison ne distinguerait PAS les deux livraisons. D'où trois preuves structurelles,
indépendantes du contenu du rapport :

```sh
grep -cF "new SessionMonitor" src/Chronos/Services/DiagnosticService.cs         # attendu : 0
grep -cF "GetRequiredService<SessionMonitor>()" src/Chronos/App.xaml.cs         # attendu : 2
grep -cF "=> Inspecter(now).Visibles;" src/Chronos/Services/SessionMonitor.cs   # attendu : 1
```

La troisième est la moins évidente et la plus importante : partager une instance ne suffirait pas si chaque
appelant refaisait le tri dans son coin. `Read` ne contient plus aucune logique de filtre — il délègue.

## Le risque n°1 est la compilation, pas la logique

`tests/Chronos.Tests` porte un `ProjectReference` vers `Chronos`. Une seule erreur de compilation, où que
ce soit, fait échouer **toute** l'invocation `dotnet test`, pas le test concerné.

**Parade inscrite dans l'ordre des plans :** le protocole du **paramètre optionnel en dernière position**
(précédents `authStatus` phase 17, `etatServeur` phase 18, `machine` phase 20) coûte **0 site de construction
cassé** — 1 en production, 10 en tests. Aucun « échec de build attendu » n'est admis à aucun commit ; l'étape
ROUGE d'un cycle TDD se joue contre un squelette compilable (`NotImplementedException`, précédent 18-01).

## Pourquoi trois vagues strictement sérielles

- 22-02 dépend de `Inspecter` et d'`AffichageSessions`, créés en 22-01.
- 22-02 et 22-03 se disputent `src/Chronos/Services/DiagnosticService.cs` et
  `tests/Chronos.Tests/DiagnosticServiceTests.cs` : la propriété exclusive des fichiers impose l'ordre.

Deux plans exécutés en parallèle dans le même arbre de travail partagent la même invocation `dotnet test` :
un chantier en cours dans l'un ferait échouer la vérification de l'autre sans que le défaut soit dans
l'autre. La sérialisation supprime cette classe de faux négatifs.

## Carte de vérification par tâche

| Plan / tâche | Ce qui est prouvé | Commande |
|---|---|---|
| 22-01 / T1 | `Inspecter` nomme le filtre qui masque ; `Read` délègue ; le cas `e465420e` est rejoué | `… --filter "FullyQualifiedName~InspectionSessionsTests\|FullyQualifiedName~SessionsTests\|FullyQualifiedName~TreatedSessionsTests"` |
| 22-01 / T2 | L'ordre, le libellé d'état et le libellé d'ancienneté ont un producteur unique ; le widget est inchangé | `… --filter "FullyQualifiedName~AffichageSessionsTests\|FullyQualifiedName~SessionStylesBindingTests\|FullyQualifiedName~GardesDoctrineTests"` |
| 22-02 / T1 | Le rapport décrit le moniteur injecté, nomme les masquages, ne tronque plus | `… --filter "FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~CadranBindingTests"` |
| 22-02 / T2 | Partage d'instance câblé ; deux gardes de non-retour, falsifiées par mutation | `… --filter "FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~CompositionRootTests"` |
| 22-03 / T1 | Fichiers d'état choisis par pertinence ; reste annoncé ; dossier du moniteur | `… --filter "FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~NormalisationUniqueTests"` |
| 22-03 / T2 | Suite complète verte, deux passes ; invariants de sécurité | `dotnet test Chronos.sln -c Debug --nologo -v q` |

## Sampling Rate

- Après chaque tâche : la commande rapide du domaine touché (ci-dessus)
- Après chaque plan : suite complète
- Avant vérification de phase : suite complète verte, **deux exécutions** — le chargeur BAML produit des
  échecs intermittents qui ne se voient pas à la première passe (cf. `XamlWpfCollection`)

## Critères de succès du ROADMAP — preuves et résultats

| # | Critère (ROADMAP) | Preuve nommée | Résultat mesuré |
|---|---|---|---|
| 1 | **Diagnostic et widget disent la même chose** : liste, états et âges identiques des deux côtés, comparables ligne à ligne | `Read_rend_exactement_les_sessions_visibles_d_Inspecter`, `Le_widget_affiche_ce_que_la_couche_neutre_produit`, `Le_rapport_decrit_les_sessions_du_moniteur_qu_on_lui_donne` ; `grep -cF "GetRequiredService<SessionMonitor>()" src/Chronos/App.xaml.cs` → 2 | **VERT** — les 3 tests passent ; grep → **2** (une occurrence pour le contrôleur du widget, une pour le diagnostic : le partage est lisible à l'œil nu). Reste à confirmer **in vivo** par l'utilisateur, point 1 ci-dessous. |
| 2 | **Ce qui est masqué est dit, et pourquoi** : `e465420e` devient lisible en une lecture | `Le_cas_e465420e_devient_lisible_en_une_lecture`, `Le_cas_e465420e_se_lit_en_une_ligne_du_rapport`, `Une_session_archivee_est_annoncee_masquee_par_le_magasin_d_archives` | **VERT** — les 3 tests passent. Ligne livrée, chaque fragment asserté : `· e465420e PROJET ADVANCED SHEET — à toi (il y a 10 min) — masquée par treated.json (hystérésis « traité » — posée automatiquement)`. Le filtre est nommé **avec son fichier** : « absent » n'apprend rien, « masquée par treated.json » dit quoi ouvrir. |
| 3 | **Des sessions pertinentes, pas les huit premières de l'alphabet** | `Les_fichiers_listes_sont_ceux_qui_attendent_et_les_plus_recents`, `Le_rapport_ne_tronque_plus_la_liste_des_sessions_affichees` ; `grep -cF "files.Take(8)" src/Chronos/Services/DiagnosticService.cs` → 0 | **VERT** — les 2 tests passent, plus 4 autres ajoutés en 22-03. `files.Take(8)` → **0** ; `Take(8)` → **1** (la seule survivante décrit la forme d'un blob d'identifiant, `DescribeBlobShape` — sans rapport avec les sessions) ; `Take(MaxFichiersEtat)` → **1** ; `AffichageSessions.Urgence` → **2** (voir la note d'écart ci-dessous). |
| 4 | **Non-retour garanti** : le diagnostic ne peut plus reconstruire son moniteur, et une garde le prouve | `Le_diagnostic_ne_fabrique_aucun_moniteur_de_sessions`, `Le_diagnostic_recoit_le_moniteur_du_conteneur`, `Assert.Same` dans `Le_graphe_DI_resout_la_chaine_de_sessions` | **VERT ET FALSIFIÉ** — les 3 tests passent, et les 3 mutations correspondantes ont été observées ROUGES puis révoquées (tableau ci-dessous). `grep -cF "new SessionMonitor" src/Chronos/Services/DiagnosticService.cs` → **0**. |

**Écart de comptage assumé, critère n°3.** Le plan 22-03 attendait `AffichageSessions.Urgence` → **1** dans
`DiagnosticService.cs`. Le compte mesuré est **2**, et c'est correct : le plan 22-02 avait déjà introduit une
première occurrence pour trier les sessions MASQUÉES. L'esprit du critère — « l'ordre d'urgence vient de la
couche partagée, il n'est pas recopié » — est tenu strictement : **zéro** recopie du barème d'urgence dans
`DiagnosticService.cs`, les deux occurrences étant deux appels au producteur unique. Le chiffre attendu du
plan était faux, pas le code ; il est corrigé ici plutôt qu'absorbé en silence.

## Falsification des gardes — obligatoire, pas facultative

Le critère n°4 exige une garde **falsifiable**, pas seulement verte. Trois mutations, chacune appliquée,
observée ROUGE, puis **révoquée** (motif établi au plan 21-03) :

| # | Mutation | Garde qui doit rougir | Observé |
|---|---|---|---|
| a | Retirer `moniteurSessions: …` de l'enregistrement dans `App.xaml.cs` | `Le_diagnostic_recoit_le_moniteur_du_conteneur` | **ROUGE observé** (plan 22-02) — 1 échec / 6, **et lui seul** |
| b | Remplacer `_moniteurSessions.Inspecter(…)` par une construction locale dans `DiagnosticService.cs` | `Le_diagnostic_ne_fabrique_aucun_moniteur_de_sessions` | **ROUGE observé** (plan 22-02) — 1 échec / 6, **et lui seul** |
| c | Passer le `SessionMonitor` en `AddTransient` dans le miroir DI | `Assert.Same` de `Le_graphe_DI_resout_la_chaine_de_sessions` | **ROUGE observé** (plan 22-02) — 1 échec / 5, **et lui seul** |

Aucune mutation n'a fait tomber autre chose que la garde visée : ce ne sont pas des détecteurs de bruit. La
mutation (b) mérite une note — elle **compile et démarre parfaitement**. Un rapport ainsi muté redeviendrait
faux en silence, exactement comme avant cette phase ; c'est la raison d'être de la garde.

Révocation vérifiée : checksums MD5 identiques avant/après dans les trois cas, `grep -rn "MUTATION (" src/ tests/`
→ **0**, `git status --porcelain` → **vide**. `git diff --name-only` sur toute la phase ne montre que les
**11 fichiers** prévus par les trois plans.

## Preuve de PARTAGE D'INSTANCE — la condition de placement de la phase

Trois commandes, exécutées le 2026-09-12 à la clôture de la phase. Elles sont indépendantes du CONTENU du
rapport : c'est ce qui distingue un partage d'instance d'une copie de comportement, que la seule comparaison
de deux sorties le jour J ne saurait pas séparer.

| Commande | Attendu | Mesuré |
|---|---|---|
| `grep -cF "new SessionMonitor" src/Chronos/Services/DiagnosticService.cs` | 0 | **0** |
| `grep -cF "GetRequiredService<SessionMonitor>()" src/Chronos/App.xaml.cs` | 2 | **2** |
| `grep -cF "=> Inspecter(now).Visibles;" src/Chronos/Services/SessionMonitor.cs` | 1 | **1** |

La troisième est la moins évidente et la plus importante : partager une instance ne suffirait pas si chaque
appelant refaisait le tri dans son coin. `Read` ne contient plus aucune logique de filtre — il délègue.

**Conséquence, et livrable de long terme de cette phase :** les phases 23 à 26 pourront changer le câblage du
widget **sans qu'une ligne de `DiagnosticService.cs` ne change**, et le rapport suivra. Ce n'est pas une
promesse : c'est une **attente vérifiable**, et sa vérification est datée — point 4 de la section utilisateur,
à la fin de la phase 26. Si le rapport devait être retouché entre-temps pour rester juste, cette phase aurait
livré une copie de comportement déguisée, et il faudrait le dire.

## Gardes à ne pas casser

`ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`, `GardesDoctrineTests`,
`La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte`, plus les gardes de périmètre de la phase 21
(`Aucun_type_de_la_source_app_bureau_ne_subsiste_dans_l_assembly`,
`Aucun_style_de_session_ne_binde_plus_un_libelle_de_type`,
`Le_demarrage_purge_les_identifiants_fantomes_du_magasin_d_archives`).

**Pièges de garde propres à cette phase :**

- `NormalisationUniqueTests` balaie TOUT `.cs` plat de `Services/` et `Models/`, commentaires compris, hors
  quatre exemptions nominatives. Les deux fichiers créés (`LectureSessions.cs`, `AffichageSessions.cs`) n'y
  figurent pas : aucun `/ 100`, `* 100`, `FromUnixTimeSeconds`, `FromUnixTimeMilliseconds` ni
  `DateTimeOffset.TryParse` ne doit y apparaître. `DiagnosticService.cs` non plus n'est pas exempté — la
  lecture des `updated_at` doit continuer de passer par `UsageNormalization.InstantDepuisEpochMillisecondes`.
- `GardesDoctrineTests.Aucun_seuil_d_anciennete_n_est_calcule_dans_les_ViewModels_de_la_doctrine` ne porte
  que sur `MainViewModel.cs` et `WindowGaugeViewModel.cs` : sortir le formateur d'ancienneté de
  `SessionsViewModel.cs` est neutre pour elle.
- `ServicesLayerPurityTests` : `AffichageSessions` vit dans `Chronos.Services` et ne doit exposer aucun type
  WPF. C'est la raison pour laquelle la couleur et les pinceaux restent dans le ViewModel.

## Ce que cette phase NE fait PAS — et qu'il ne faut pas vérifier ici

Anticiper une phase suivante casserait l'ordre du milestone autant qu'un oubli :

| Sujet | Phase propriétaire | Reste donc INCHANGÉ ici |
|---|---|---|
| Balayage des états expirés et des `.tmp` orphelins | 23 (CYC-01) | le magasin n'est ni purgé ni nettoyé |
| Écriture d'état par `tmp` + `Move` | 23 (CYC-02) | le chemin d'écriture n'est pas touché |
| Fusion par fraîcheur | 24 (FUS-01/02) | l'arbitrage reste par ordre d'insertion |
| Contrat d'événements (`PermissionRequest`, battements de cœur) | 25 | les 5 hooks câblés restent les mêmes |
| « Traité » et TTL de 6 h d'`ArchiveStore` | 26 (TRT-01→04) | les deux magasins gardent leur TTL et leur sémantique |

**Cette phase ne change AUCUN comportement du widget.** Preuve opérationnelle : `SessionsTests.cs`,
`TreatedSessionsTests.cs` et `SessionStylesBindingTests.cs` restent verts **sans une seule retouche**.
Leur apparition dans `git diff --name-only` est un signal d'alarme, pas un détail.

## Invariants de sécurité — à vérifier et à consigner

```sh
ls "$APPDATA/Chronos/sessions" | wc -l        # attendu : 66 (54 .json + 12 .tmp) — INCHANGÉ
wc -c < "$APPDATA/Chronos/archived.json"      # attendu : 84 — INCHANGÉ (ses deux fantômes attendent
                                              #   le prochain lancement volontaire de l'utilisateur)
wc -c < "$APPDATA/Chronos/oauth.dat"          # attendu : 518 — TAILLE SEULE
git status --porcelain                        # rien hors src/, tests/, .planning/
```

- **Ne JAMAIS vérifier le mtime d'`oauth.dat`** : le rafraîchissement préventif de la phase 17 le fait
  légitimement bouger toutes les 60 s. Une garde posée dessus serait rouge sur un système parfaitement sain
  (correction établie au plan 21-01).
- L'overlay `Chronos-v3.0.2.exe` (pid 119412) **n'est ni lancé ni tué**.
- Aucune requête réseau réelle : tous les montages de `DiagnosticServiceTests` posent `Token = null`, et la
  sonde est gardée par `if (token is not null)`.
- Aucun test n'écrit dans le vrai `%APPDATA%\Chronos` : tous les chemins de moniteur et de magasins sont
  sous `Path.GetTempPath()`, avec une assertion `Assert.StartsWith(Path.GetTempPath(), …)` en garde-fou.

| Invariant | Attendu | Résultat mesuré |
|---|---|---|
| `sessions\` | 66 entrées | **66** — INCHANGÉ (rien n'a été supprimé : le balayage appartient à la phase 23) |
| `archived.json` | 84 octets | **84** — INCHANGÉ (ses deux fantômes attendent le prochain lancement volontaire) |
| `oauth.dat` | 518 octets (taille seule) | **518** — mtime NON vérifié, à dessein |
| Overlay pid 119412 | vivant, non touché | **vivant** — `Chronos-v3.0.2.exe`, ni lancé ni tué |
| `git status --porcelain` | rien hors `src/`, `tests/`, `.planning/` | **vide** après commits ; diff de la phase = 11 fichiers, tous sous `src/` ou `tests/` |
| Requêtes réseau émises par un test | 0 | **0** — `Token = null` partout, sonde gardée par `if (token is not null)` |
| Écritures de test dans le vrai `%APPDATA%\Chronos` | 0 | **0** — tous les chemins sous `Path.GetTempPath()`, avec `Assert.StartsWith` en garde-fou |
| Suite, passe 1 | 0 échec | **747 réussis / 0 échec / 4 s** |
| Suite, passe 2 | 0 échec | **747 réussis / 0 échec / 4 s** |
| Six gardes nommées de la phase | vertes | **24 tests, 0 échec** (`ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`, `GardesDoctrineTests`, `GardesPerimetreTests`, `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte`) |
| `SessionsTests.cs` / `TreatedSessionsTests.cs` / `SessionStylesBindingTests.cs` dans le diff de la phase | absents | **absents** — le widget n'a changé en rien, comme prévu |

## À VÉRIFIER PAR L'UTILISATEUR

Ce que seul un vrai lancement peut établir, et que la contrainte de phase interdit de provoquer.

1. **Comparaison ligne à ligne (critère n°1, in vivo).** Widget de sessions affiché, ouvrir
   **clic droit → Diagnostic…**. Dans « Sessions AFFICHÉES par le widget » : mêmes projets, mêmes états
   (« à toi » / « tour fini » / « en cours » / « inconnu »), mêmes âges, même ordre qu'à l'écran. Tout écart
   est un défaut, pas une approximation.
2. **Ce qui manque à l'écran doit être nommé (critère n°2, in vivo).** Dans « Sessions MASQUÉES par un
   filtre », toute session absente du widget doit apparaître avec le fichier qui la masque. Cas attendu sur
   cette machine : `e465420e` (PROJET ADVANCED SHEET), masquée par `treated.json`.
3. **Des exemples qui servent à quelque chose (critère n°3, in vivo).** Dans « Fichiers d'état » : les
   lignes listées doivent être des sessions récentes ou en attente — plus d'entrées vieilles de plusieurs
   semaines — et la ligne « … et N autre(s) non listé(s) » doit apparaître (attendu : ~46 sur 54).
   Repères de lecture du nouveau format :
   - l'en-tête doit nommer **le dossier du moniteur** : `Fichiers d'état (…\Chronos\sessions) : 54`
     (les 12 `.tmp` orphelins ne sont pas comptés — ils appartiennent à la phase 23) ;
   - chaque ligne se lit `· projet — Activité (maj il y a N min)`, et une ligne `(date inconnue)` signale un
     fichier sans `updated_at` lisible : **c'est un fait à signaler**, pas un détail de mise en forme ;
   - si la liste contient encore des entrées de plusieurs semaines **alors qu'une session récente tourne**,
     le critère n°3 n'est PAS tenu et il faut le dire.
4. **Le non-retour, à l'échelle du milestone.** À la fin de la phase 26, rouvrir le diagnostic : il doit
   décrire le nouveau widget alors qu'aucune ligne de `DiagnosticService.cs` n'aura changé entre-temps.
   C'est la vérification décisive de cette phase, et elle ne peut être faite qu'à la fin.
5. **Reliquats de la phase 21**, à reprendre à l'occasion : purge réelle d'`archived.json` (84 octets → `{}`)
   au premier lancement, cohérence des 8 styles × 9 thèmes, absence de lignes `desktop:` après usage
   prolongé avec l'app bureau ouverte.
