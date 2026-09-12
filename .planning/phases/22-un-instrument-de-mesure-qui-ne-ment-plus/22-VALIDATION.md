---
phase: 22
slug: un-instrument-de-mesure-qui-ne-ment-plus
status: planned
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-12
validated: (à remplir au plan 22-03, tâche 2)
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
| 1 | **Diagnostic et widget disent la même chose** : liste, états et âges identiques des deux côtés, comparables ligne à ligne | `Read_rend_exactement_les_sessions_visibles_d_Inspecter`, `Le_widget_affiche_ce_que_la_couche_neutre_produit`, `Le_rapport_decrit_les_sessions_du_moniteur_qu_on_lui_donne` ; `grep -cF "GetRequiredService<SessionMonitor>()" src/Chronos/App.xaml.cs` → 2 | (à mesurer) |
| 2 | **Ce qui est masqué est dit, et pourquoi** : `e465420e` devient lisible en une lecture | `Le_cas_e465420e_devient_lisible_en_une_lecture`, `Le_cas_e465420e_se_lit_en_une_ligne_du_rapport`, `Une_session_archivee_est_annoncee_masquee_par_le_magasin_d_archives` | (à mesurer) |
| 3 | **Des sessions pertinentes, pas les huit premières de l'alphabet** | `Les_fichiers_listes_sont_ceux_qui_attendent_et_les_plus_recents`, `Le_rapport_ne_tronque_plus_la_liste_des_sessions_affichees` ; `grep -cF "files.Take(8)" src/Chronos/Services/DiagnosticService.cs` → 0 | (à mesurer) |
| 4 | **Non-retour garanti** : le diagnostic ne peut plus reconstruire son moniteur, et une garde le prouve | `Le_diagnostic_ne_fabrique_aucun_moniteur_de_sessions`, `Le_diagnostic_recoit_le_moniteur_du_conteneur`, `Assert.Same` dans `Le_graphe_DI_resout_la_chaine_de_sessions` | (à mesurer) |

## Falsification des gardes — obligatoire, pas facultative

Le critère n°4 exige une garde **falsifiable**, pas seulement verte. Trois mutations, chacune appliquée,
observée ROUGE, puis **révoquée** (motif établi au plan 21-03) :

| # | Mutation | Garde qui doit rougir | Observé |
|---|---|---|---|
| a | Retirer `moniteurSessions: …` de l'enregistrement dans `App.xaml.cs` | `Le_diagnostic_recoit_le_moniteur_du_conteneur` | (à mesurer) |
| b | Remplacer `_moniteurSessions.Inspecter(…)` par une construction locale dans `DiagnosticService.cs` | `Le_diagnostic_ne_fabrique_aucun_moniteur_de_sessions` | (à mesurer) |
| c | Passer le `SessionMonitor` en `AddTransient` dans le miroir DI | `Assert.Same` de `Le_graphe_DI_resout_la_chaine_de_sessions` | (à mesurer) |

Après révocation, `git diff` sur `src/` ne doit montrer que les modifications prévues par les plans.

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
| `sessions\` | 66 entrées | (à mesurer) |
| `archived.json` | 84 octets | (à mesurer) |
| `oauth.dat` | 518 octets (taille seule) | (à mesurer) |
| Overlay pid 119412 | vivant, non touché | (à mesurer) |
| Suite, passe 1 | 0 échec | (à mesurer) |
| Suite, passe 2 | 0 échec | (à mesurer) |

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
4. **Le non-retour, à l'échelle du milestone.** À la fin de la phase 26, rouvrir le diagnostic : il doit
   décrire le nouveau widget alors qu'aucune ligne de `DiagnosticService.cs` n'aura changé entre-temps.
   C'est la vérification décisive de cette phase, et elle ne peut être faite qu'à la fin.
5. **Reliquats de la phase 21**, à reprendre à l'occasion : purge réelle d'`archived.json` (84 octets → `{}`)
   au premier lancement, cohérence des 8 styles × 9 thèmes, absence de lignes `desktop:` après usage
   prolongé avec l'app bureau ouverte.
