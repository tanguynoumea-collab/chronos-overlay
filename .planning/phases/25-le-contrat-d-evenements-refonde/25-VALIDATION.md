---
phase: 25
slug: le-contrat-d-evenements-refonde
status: planned
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-12
validated: null
---

# Phase 25 — Validation Strategy

> Carte **posée à la planification**, à **remplir et mesurer** au fil des quatre plans, et à clore au plan
> 25-04. Toute case encore vide à la clôture est un **défaut de la phase**, pas une approximation.
>
> Cette phase applique à elle-même la doctrine qu'elle installe : **ne rien écrire ici qui n'ait été
> mesuré.** Une case remplie « d'après le plan » plutôt que d'après une exécution est exactement la faute
> que le milestone corrige.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`net8.0-windows`) — `tests/Chronos.Tests` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Quick run (contrat d'événements)** | `… --filter "FullyQualifiedName~CatalogueEvenementsHooks\|FullyQualifiedName~BattementsCoeur\|FullyQualifiedName~Sessions\|FullyQualifiedName~Inspection\|FullyQualifiedName~ClaudeSettings\|FullyQualifiedName~Affichage\|FullyQualifiedName~ContratHooksDocumente\|FullyQualifiedName~GardesPerimetre"` |
| **Baseline d'entrée de phase** | **788 tests / 0 échec / ~4 s** (fin de phase 24, remesurée par le vérificateur) |
| **Cible de fin de phase** | **0 échec, aucun test supprimé.** Le critère de chaque plan n'est PAS un plancher (un plancher se franchit sans rien ajouter) mais une **égalité** : `total = total mesuré du plan précédent + nombre de cas ajoutés`, tout écart — **même positif** — justifié **nominativement** dans le SUMMARY. Attendus **indicatifs** par plan : **816** (25-01), **833** (25-02), **848** (25-03), **854** (25-04). Détail indicatif : ≈ +7 (25-01 T1), ≈ +7 (25-01 T2), ≈ +14 (25-01 T3, deux `[Theory]`), ≈ +9 (25-02 T1), ≈ +4 (25-02 T2), ≈ +4 (25-02 T3), ≈ +9 (25-03 T1), ≈ +4 (25-03 T2), ≈ +2 (25-03 T3), ≈ +6 (25-04 T2). |
| **Total mesuré après 25-01** | **817 / 0 échec / 5 s** (788 + 7 + 8 + 14 ; attendu indicatif 816, écart de +1 justifié nominativement au SUMMARY 25-01 : le plan nomme HUIT tests en T2 là où la carte en estimait sept) |
| **Total mesuré après 25-02** | **837 / 0 échec / 4 s** (817 + 9 + 7 + 4 ; attendu indicatif 833, écart de +4 justifié nominativement au SUMMARY 25-02 : les deux tests ajoutés par le plan-checker — A4 `timeout` et B6 écrivains parallèles — plus le second cas de la `[Theory]` de routage) |
| **Total mesuré après 25-03** | _à mesurer_ |
| **Total mesuré après 25-04** | _à mesurer_ |
| **Deux exécutions consécutives** | _à mesurer_ (attendu : même total, 0 échec les deux fois) |
| **Renommages prévus** | **7, tous annoncés** : 3 noms de tests de comptage de hooks (25-01 T2) ; le champ `StaleWorking` → `SilenceDesBattements` (25-02 T3) ; et **3 assertions héritées** converties d'`Unknown` à `WaitingDeduced` en 25-03 T2 — `Vingt_et_une_minutes_de_SILENCE_ne_se_disent_plus_en_cours`, `Monitor_lit_les_sessions_et_applique_la_staleness` (`SessionsTests`), et `Un_hook_Working_de_25_min_ne_rend_plus_un_transcript_frais_inconnu` (`InspectionSessionsTests`, l. 195-208), ce dernier **renommé** `…_frais_deduit`. Tout renommage supplémentaire doit être **justifié nominativement** dans le SUMMARY concerné. |

## Le critère de cette phase n'est PAS un chiffre de couverture

Cette phase **n'enlève aucun code** et ne supprime aucun test. Le critère opérationnel :

> **0 échec, total > 788, aucun test supprimé, et les cinq critères de succès du ROADMAP prouvés un à un.**

Un total inférieur à 788 doit être expliqué **nominativement**, jamais absorbé en ajustant l'attendu.

## Couverture des exigences

| Exigence | Plan | Preuve nommée attendue | Mesuré |
|---|---|---|---|
| **EVT-01** — `PermissionRequest` alimente « en attente » | 25-01 | `SessionsTests` : routage `PermissionRequest` → `WaitingAttention` ; groupe installé dans `settings.json` | **PROUVÉ** — `PermissionRequest_fonde_l_attente_et_dit_le_nom_de_l_evenement` (routage, motif = nom de l'événement) + `Le_cablage_installe_bien_le_groupe_PermissionRequest` (groupe installé, sans `matcher`), 0 échec |
| **EVT-02** — `Notification` cesse d'être traité comme un état | 25-01 | `[Theory]` de veto sur les neuf types sans état + `Le_groupe_Notification_ne_laisse_passer_que_les_trois_vraies_demandes` | **PROUVÉ** — veto sur 9 types (`Une_alerte_d_absence_ne_fabrique_plus_aucun_etat` pour `idle_prompt` + `[Theory]` de 8 cas), 3 vraies demandes en `[Theory]`, matcher installé vérifié, 0 échec |
| **EVT-03** — battements de cœur | 25-02 | `Un_battement_frais_maintient_en_cours_bien_au_dela_d_une_heure` + le filtre sous-agent + la cadence de cinq cents écritures | **PROUVÉ** — `Un_battement_frais_maintient_en_cours_bien_au_dela_d_une_heure` (pipeline réel, avec témoin `Unknown` à 2 h 10), veto sous-agent sur les deux marqueurs (5 cas probants après T2), `Cinq_cents_battements_consecutifs_sous_lecteur_concurrent_ne_perdent_rien` (0 perte) et `Huit_ecrivains_paralleles_sur_la_meme_session_n_echouent_jamais` (0 refus sur 400), 0 échec |
| **EVT-04** — l'interruption ne laisse plus la session invisible | 25-03 | `Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini` + `Une_session_deduite_est_visible_ambre_et_dit_sa_deduction` | _à mesurer_ |
| **EVT-05** — le contrat documenté | 25-04 | `ContratHooksDocumenteTests` (6 tests) + `docs/hooks-contract.md` | _à mesurer_ |

## Les cinq critères du ROADMAP, un par un

| # | Critère | Preuve nommée | Résultat mesuré |
|---|---------|---------------|-----------------|
| 1 | **« Attend » naît d'une vraie demande** — une permission demandée bascule en « à toi » ; une alerte d'absence ne fabrique plus aucun état (EVT-01, EVT-02) | Routage `PermissionRequest`, veto `idle_prompt` / `auth_success` / `quota_auto_resume_*`, matcher installé | **PROUVÉ au plan 25-01** — les trois preuves vertes. Reste la vérification in vivo (§ « À VÉRIFIER PAR L'UTILISATEUR » n° 1), impossible avant republication + réconciliation. |
| 2 | **« Réfléchit » ne s'éteint plus tout seul** — réaffirmé par battements, y compris au-delà d'une heure (EVT-03) | `Un_battement_frais_maintient_en_cours_bien_au_dela_d_une_heure` (pipeline complet : `Process` → `EcritureEtatSession` → `SessionMonitor.Read`) | **PROUVÉ au plan 25-02** — vert, et non vacueusement : muter le routage de `PostToolUse` dans un worktree jetable le fait échouer. Reste la vérification in vivo (§ « À VÉRIFIER PAR L'UTILISATEUR » n° 2), impossible avant republication + réconciliation. |
| 3 | **Échap est couvert** — état juste et visible, ni figé « en cours », ni disparu (EVT-04) | `Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini` (avec `Assert.NotEqual(WaitingTurn, …)`) + `IsGhost == false` | _à mesurer_ |
| 4 | **Le silence n'affirme que ce qui a été observé** — jamais « terminée » ni « tour fini », et une déduction se **dit** déduction (**critère amendé au ROADMAP le 2026-09-12** : la lettre « se dit inconnu » imposait `Unknown`, l'intention était l'honnêteté de l'affirmation) | `Une_activite_illisible_reste_inconnue_et_non_deduite`, `Une_attente_observee_ne_se_convertit_jamais_en_deduction`, `Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini`, et le libellé `"à toi ? déduit"` | _à mesurer_ |
| 5 | **Le contrat est écrit** dans `docs/`, avec ce qui n'est PAS garanti | `docs/hooks-contract.md` §5 (les trois trous datés et sourcés) + `ContratHooksDocumenteTests` | _à mesurer_ |

## Ce qui rendrait cette phase creuse

Quatre livraisons qui produisent **le même écran** le jour J, et divergent la semaine suivante.

| Livraison | Ce qu'on lit le jour J | Ce qui se passe ensuite |
|---|---|---|
| **Supprimer purement `Notification`** du câblage | plus aucune fausse attente | `agent_needs_input` et les dialogues d'élicitation — de VRAIES demandes — cessent d'être vus. On aurait troqué un faux positif contre un faux négatif. |
| **Filtrer par `matcher` sur les trois vraies demandes** ✅ | plus aucune fausse attente | les trois demandes réelles continuent d'alerter, et les neuf autres types n'atteignent même plus Chronos |
| **Lire un champ de stdin pour décider** quel type de notification produit un état | ça marche aujourd'hui | le nom du champ n'est pas confirmable (page tronquée) : le jour où il change, Chronos fabrique ou perd des états **en silence** |
| **Le `matcher` décide, le champ ne fait que VETO** ✅ | identique | si le champ disparaît, on retombe sur le tri par matcher — **jamais** sur une attente fabriquée |

| Livraison (EVT-04) | Ce que voit l'utilisateur | Ce que ça dit |
|---|---|---|
| Le silence après travail devient **`WaitingTurn`** | « tour fini » | une **observation** que personne n'a faite. C'est l'interdit absolu du milestone. |
| Le silence reste **`Unknown`** | « inconnu », estompé à 0,22 d'opacité dans deux gabarits | honnête mais **inutile** : le critère n°3 exige un état *juste ET visible*, « elle m'attend » |
| Une **attente DÉDUITE**, ambre, lisible, libellée « à toi ? déduit » ✅ | l'incertitude est dans le libellé, la visibilité dans la forme | exactement ce que l'inférence autorise, et rien de plus |

| Livraison (EVT-03) | Ce qu'on mesure | Ce qui se passe ensuite |
|---|---|---|
| Battement sur **`MessageDisplay`** | le battement le plus fin | un processus Chronos par fragment de texte affiché. Un battement qui coûte plus cher que ce qu'il observe. |
| Battement sur `PreToolUse` / `PostToolUse` **sans filtre sous-agent** | ça paraît marcher | quatre-vingt-quatorze pour cent des transcripts de cette machine sont des sous-agents, et ils portent le **même `session_id`** : une vague d'agents réaffirmerait « en cours » sur un parent qui n'y est plus, et **écraserait un « à toi » en attente** |
| `PreToolUse` / `PostToolUse` **avec veto sous-agent** ✅ | idem | seule la session parente parle de son activité ; un sous-agent ne peut plus que réclamer une intervention |

## Preuves structurelles — attendus posés à la planification

```sh
# Le contrat externe est respecté
grep -c "PermissionRequest"        src/Chronos/Services/CatalogueEvenementsHooks.cs   # attendu : >= 1
grep -c "CatalogueEvenementsHooks" src/Chronos/Services/SessionHookInstaller.cs       # attendu : >= 2
grep -c "agent_id"                 src/Chronos/Services/SessionHookProcessor.cs       # attendu : >= 1
grep -c "agent_type"               src/Chronos/Services/SessionHookProcessor.cs       # attendu : >= 1
grep -c "NotificationsSansEtat"    src/Chronos/Services/SessionHookProcessor.cs       # attendu : >= 2

# Le seuil change de sens, jamais de valeur
grep -rn "StaleWorking"            src/                                               # attendu : 0 ligne
grep -c  "SilenceDesBattements"    src/Chronos/Services/SessionMonitor.cs             # attendu : >= 3
grep -c  "FromMinutes(20)"         src/Chronos/Services/SessionMonitor.cs             # attendu : >= 1
grep -c  "FromHours(8)"            src/Chronos/Services/SessionMonitor.cs             # attendu : >= 1

# La déduction existe et n'est pas confondue avec une observation
grep -c "WaitingDeduced"           src/Chronos/Services/SessionSnapshot.cs            # attendu : >= 1
grep -c "WaitingDeduced"           src/Chronos/Services/AffichageSessions.cs          # attendu : >= 2
grep -c "WaitingDeduced"           src/Chronos/Services/SessionMonitor.cs             # attendu : >= 1

# Le document existe et est tenu
grep -c "EVENEMENTS-CABLES:debut"  docs/hooks-contract.md                             # attendu : 1
grep -c "2026-09-12"               docs/hooks-contract.md                             # attendu : >= 1

# Les acquis des phases 20 à 24 n'ont pas bougé
grep -cF "byId["                       src/Chronos/Services/SessionMonitor.cs         # attendu : 0
grep -cF "ArbitrageSessions.Trancher(" src/Chronos/Services/SessionMonitor.cs         # attendu : 1
grep -cF "=> Inspecter(now).Visibles;" src/Chronos/Services/SessionMonitor.cs         # attendu : 1
grep -cF "new SessionMonitor"          src/Chronos/Services/DiagnosticService.cs      # attendu : 0
grep -cF "?? new "                     src/Chronos/Services/DiagnosticService.cs      # attendu : 2
grep -cF "MotifMasquage"               src/Chronos/Services/DiagnosticService.cs      # attendu : 3, INCHANGÉ

# La matrice de rendu VOIT le nouvel état, et la dette de commentaire est soldée
grep -c "WaitingDeduced"  tests/Chronos.Tests/SessionStylesBindingTests.cs            # attendu : >= 1
grep -c "SessionTreatmentTracker" docs/hooks-contract.md                              # attendu : >= 1

# ATTENTION : `git diff --stat` NU est MUET après un commit (il ne compare que l'arbre de travail).
# SHA = le SHA d'entrée de phase, relevé par `git rev-parse HEAD` AVANT le premier commit de la phase
# et consigné dans le SUMMARY 25-01.

# La phase 26 n'est pas anticipée
git diff --stat $SHA..HEAD -- src/Chronos/Services/SessionTreatmentTracker.cs \
                              src/Chronos/Services/TreatedStore.cs \
                              src/Chronos/Services/ArchiveStore.cs                    # attendu : VIDE

# L'arbitrage de la phase 24 n'est pas retouché (même quand un test fige son cas d'égalité)
git diff --stat $SHA..HEAD -- src/Chronos/Services/ArbitrageSessions.cs \
                              src/Chronos/Services/LectureSessions.cs                 # attendu : VIDE

# Le SEUL changement XAML autorisé : le commentaire de tête de SessionStyles.xaml (l. 5, « déduit
# (Ghost) = fantôme » devenu faux). Aucun DataTemplate, Style, Trigger ni x:Key dans le diff.
git diff --stat  $SHA..HEAD -- '*.xaml'                    # attendu : SessionStyles.xaml, et lui seul
git diff --numstat $SHA..HEAD -- src/Chronos/Resources/SessionStyles.xaml   # attendu : <= 1 ajout / 1 retrait
```

_Résultats mesurés : à remplir, un par un, au plan 25-04._

## Mutations de falsification — prévues

| Mutation | Test attendu en échec | Mesuré |
|---|---|---|
| Retirer une ligne de la table §1 de `docs/hooks-contract.md` (worktree jetable) | `ContratHooksDocumenteTests.La_table_documentee_liste_EXACTEMENT_les_evenements_cables` | _à mesurer_ |
| Supprimer le veto sous-agent dans `SessionHookProcessor` (worktree jetable) | **3** cas si jouée en 25-02 T1, **5** si rejouée en fin de 25-02 T2 (seule mesure probante) — dont `Un_SessionEnd_de_sous_agent_ne_supprime_pas_le_fichier_d_etat_du_parent`, qui asserte `Ignore == true` ET `Delete == false` | **3 mesurés en T1, 5 mesurés en fin de T2** (worktree `chronos-mutant-veto`, révoqué ; `git status --porcelain` vide, 0 fichier `MUTANT`) |
| Faire écrire huit processus en parallèle sans la parade du point (f) de 25-02 T2 (worktree jetable) | `Huit_ecrivains_paralleles_sur_la_meme_session_n_echouent_jamais` | **MESURÉ SUR L'ARBRE, avant correctif : 288 refus / 400** (`IOException` de partage). Avec la reprise **unique** d'abord retenue : encore **209 puis 180 / 400** — elle ne corrige pas. Avec la reprise **bornée** livrée : **0 / 400**, trois exécutions consécutives. |
| Rendre `WaitingTurn` au lieu de `WaitingDeduced` dans `SessionMonitor.TryRead` (worktree jetable) | `Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini` | _à mesurer_ |

**Méthode obligatoire** (précédents 23-01, 23-02, 24-01) : une mutation qui **ne compile pas** fait échouer
toute l'invocation `dotnet test` et ne dit **rien** de la garde. On la joue dans un `git worktree` jetable,
ou par alias temporaire, et on révoque mutation et alias **ensemble**. Le dépôt principal doit rester propre
et vérifié tel (`git status --porcelain` vide, `grep -rlF "MUTANT" --include=*.cs` → 0).

## Étapes ROUGE attendues

| Plan / tâche | Filtre | Échecs attendus avant correctif | Mesuré |
|---|---|---|---|
| 25-01 T1 | `CatalogueEvenementsHooksTests` | tous (squelette `NotImplementedException`) | **7 échecs / 7 tests** — les 7 `[Fact]` du fichier, nommés au SUMMARY 25-01 |
| 25-01 T3 | `SessionsTests` | ≥ 10 (le routage `PermissionRequest` + les neuf vetos) | **exactement 10 échecs / 102 tests** — `PermissionRequest_fonde_l_attente_et_dit_le_nom_de_l_evenement`, `Une_alerte_d_absence_ne_fabrique_plus_aucun_etat`, et les 8 cas de `Les_huit_autres_types_du_bus_sans_demande_ne_fabriquent_plus_aucun_etat` |
| 25-02 T1 | `BattementsCoeurTests` | **≥ 3 (mesuré en T1)** puis **≥ 5 (re-mesuré après 25-02 T2)** — `PreToolUse` / `PostToolUse` ne sont routés qu'à la tâche 2 : leurs deux cas de veto sont **vacueusement verts** en T1 et ne prouvent rien. La mutation « supprimer le veto » est **rejouée en fin de T2**. | **exactement 3 échecs / 9 tests en T1** — `…_est_ignore(UserPromptSubmit)`, `…_est_ignore(Stop)`, `Un_SessionEnd_de_sous_agent_ne_supprime_pas_le_fichier_d_etat_du_parent` ; puis **exactement 5 / 13 après T2**, les deux cas `PostToolUse` (`agent_id` et `agent_type`) devenant enfin probants |
| 25-03 T2 | `InspectionSessionsTests` | ≥ 2 | _à mesurer_ |
| 25-04 T2 | `ContratHooksDocumenteTests` | tous tant que le document n'est pas lu | _à mesurer_ |

## Acquis des phases précédentes, à revérifier

| Acquis | Attendu |
|---|---|
| Phase 20 — `?? new ` dans `DiagnosticService.cs` | 2 |
| Phase 21 — gardes de périmètre | `GardesPerimetreTests` : 10, 0 échec |
| Phase 22 — `Read(now) => Inspecter(now).Visibles` unique ; 0 `new SessionMonitor` dans le diagnostic | 1 / 0 |
| Phase 23 — écriture directe, aucun fichier temporaire déplacé dans le chemin des hooks | `File.Move` absent de `SessionHookProcessor.cs` et `EcritureEtatSession.cs` |
| Phase 24 — `ArbitrageSessions` / `LectureSessions` / bloc « Désaccords entre sources » intacts | diff vide, `MotifMasquage` toujours à 3 occurrences |
| Gardes — `ServicesLayerPurity` / `NormalisationUnique` / `CompositionRoot` / `GardesDoctrine` / `GardesPerimetre` | 2 / 3 / 5 / 8 / 10, 0 échec |
| Rendu — `SessionStylesBindingTests` | 2, 0 échec (8 styles × 9 thèmes) |

## Invariants de sécurité — à vérifier avant et après chaque tâche

| Invariant | Attendu | Mesuré (avant → après) |
|---|---|---|
| `~/.claude/settings.json` | **jamais modifié à la main** — seul le code livré y écrira, au prochain lancement de l'overlay | _à mesurer_ |
| `%APPDATA%\Chronos\sessions` | **66** entrées, INCHANGÉ | _à mesurer_ |
| `%APPDATA%\Chronos\archived.json` | **84** octets | _à mesurer_ |
| `%APPDATA%\Chronos\oauth.dat` (**taille seule, JAMAIS le mtime**) | **518** octets | _à mesurer_ |
| Overlay `Chronos-v3.0.2.exe` (pid 119412) | vivant, ni lancé ni tué (`tasklist` seul) | _à mesurer_ |
| Sonde de capture installée dans `~/.claude/` | **0** — interdit ; si le besoin devient bloquant, le consigner comme question à l'utilisateur | _à mesurer_ |
| `git diff -- '*.csproj'` | vide, **sauf** `tests/Chronos.Tests/Chronos.Tests.csproj` au plan 25-04 (ajout de `CheminDocsChronos`) | _à mesurer_ |
| Dépendances NuGet ajoutées | **0** | _à mesurer_ |
| Tests écrivant hors de `Path.GetTempPath()` | **0** — garde anti-accident `Assert.StartsWith(Path.GetTempPath(), d)` | _à mesurer_ |
| Requêtes réseau réelles depuis un test | **0** | _à mesurer_ |

## À VÉRIFIER PAR L'UTILISATEUR

Les tests prouvent le **mécanisme** ; ils ne prouvent pas ce que l'écran montre (leçon 21-04). La phase
s'interdit de lancer ou de tuer l'overlay, et de toucher au `settings.json` de l'utilisateur.

> ### A11 — Effet de masse **(garde obligatoire, contrepartie de la décision A10)**
>
> **Effet de masse.** Après republication, relever combien de sessions passent simultanément en
> « à toi ? déduit » et ce que devient le compteur d'attente. Si le widget se remplit de déductions, le
> signal utile est noyé — c'est alors le choix de l'**événement porteur** qu'il faut rouvrir, **pas** le
> seuil.
>
> Cette vérification n'est pas laissée au hasard de l'observation : le plan **25-03 T2** porte le test
> `Combien_de_sessions_d_un_corpus_realiste_basculent_ensemble_en_deduction`, qui **chiffre** l'effet sur
> un corpus déterministe de soixante-six états. Le nombre mesuré est reporté ici, **avant** republication.
>
> | Mesure | Attendu | Mesuré |
> |---|---|---|
> | Sessions basculant ensemble en « à toi ? déduit » (corpus de 66 états, test de 25-03 T2) | chiffré, pas deviné | _à mesurer_ |
> | Sessions en « à toi ? déduit » dans le widget réel, après republication | à relever in vivo | _à mesurer_ |
> | `WaitingCount` correspondant | à relever in vivo | _à mesurer_ |
>
> **Pourquoi cette garde existe.** Le critère n°4 du ROADMAP disait « le silence se dit **inconnu** » ; les
> plans livrent « à toi ? déduit ». L'utilisateur a tranché le 2026-09-12 en faveur de « à toi ? déduit »,
> **avec cette garde de masse pour contrepartie**, et le ROADMAP a été amendé en conséquence. Un « inconnu »
> estompé est honnête mais inutile ; une déduction affichée partout serait visible mais assourdissante. Ce
> qui départage les deux, c'est un NOMBRE — et il doit être connu avant, pas après.

1. **Critère n°1, in vivo.** Une demande de permission réelle doit basculer la session en **« à toi »**
   immédiatement. Et rester au terminal sans rien taper ne doit produire **aucun** changement d'état.
2. **Critère n°2, in vivo.** Une session qui travaille plus d'une heure (exécution de phase, long build)
   doit rester **« en cours »** du début à la fin, sans repasser par « inconnu » entre deux appels d'outil.
   **Cas limite à guetter** : un outil unique de plus de vingt minutes (build très long, attente réseau) —
   la session basculera en « à toi ? déduit » alors qu'elle travaille encore. C'est la limite ASSUMÉE des
   battements sur appels d'outil, écrite au §5 du contrat. Si le cas se présente souvent, c'est le signal
   qu'il faut rouvrir le choix de l'événement porteur — **pas** retoucher le seuil au passage.
3. **Critère n°3, in vivo.** Interrompre une réponse par Échap : la session doit devenir
   **« à toi ? déduit »** (après le délai de silence), rester **lisible** dans le widget, et ne jamais
   afficher « tour fini ».
4. **Critère n°4, in vivo.** Aucune session ne doit être annoncée « terminée » ni « tour fini » sans qu'un
   tour se soit réellement terminé.
5. **Lisibilité sur les 8 styles et les 9 thèmes.** Le libellé « à toi ? déduit » est le plus long des cinq
   (quatorze caractères). Vérifier qu'il ne tronque ni ne décale aucun des huit gabarits, sur les neuf
   thèmes — galerie `--sessions`.
6. **RIEN DE TOUT CELA NE S'EXÉCUTE TANT QUE L'EXE N'EST PAS REPUBLIÉ**, et — spécifique à cette phase —
   **tant que `settings.json` n'a pas été réconcilié au lancement de l'overlay republié**. Les trois
   nouveaux événements (`PermissionRequest`, `PreToolUse`, `PostToolUse`) et le `matcher` de `Notification`
   n'existent pas encore dans la configuration de l'utilisateur. Rappel : la configuration des hooks est lue
   au DÉMARRAGE d'une session Claude Code — seules les sessions ouvertes après la réconciliation seront
   suivies.
7. **Question ouverte, à poser à l'utilisateur.** Les noms de champs spécifiques à un événement restent
   non confirmables (page de référence tronquée). Les relever exigerait une **sonde de capture** posée
   temporairement dans son `~/.claude/settings.json` — sa configuration vivante. La phase ne l'a pas fait
   et ne le fera pas d'office. À arbitrer par lui.
8. **Reliquats hérités**, à présenter **groupés** en fin de milestone : purge réelle d'`archived.json`
   (phase 21), comparaison ligne à ligne widget ↔ rapport (phase 22), résorption du magasin de 66 entrées
   (phase 23), les quatre vérifications in vivo de la phase 24.

## Entrées ouvertes, transmises à la phase 26

| Constat | Où il est écrit | Pourquoi il n'est PAS corrigé ici |
|---|---|---|
| `SessionTreatmentTracker.cs:39` (`EstAttente`) **exclut** `WaitingDeduced`, alors que le widget la compte dans `WaitingCount` : une session déduite n'ouvre pas d'épisode d'attente | §3 de `docs/hooks-contract.md` (plan 25-04) | La phase 26 redéfinit ce que « traité » veut dire ; corriger le tracker ici, c'est l'anticiper à moitié. Le diff de `SessionTreatmentTracker.cs` doit rester VIDE. |
| Dans `ArbitrageSessions.Departager`, le rang **source** précède le rang **urgence** : à horodatage égal **à la milliseconde**, un hook `WaitingDeduced` bat un transcript `Working` | test `Un_hook_deduit_et_un_transcript_travaillant_du_MEME_instant_sont_departages_par_la_source` (plan 25-03 T2), qui **fige** le comportement sans le modifier, + SUMMARY 25-03 | Héritage de la phase 24. Le `25-CONTEXT.md` l'exigeait : « ne pas modifier l'arbitrage sans qu'un test le motive ». Le test écrit le comportement ; il ne le corrige pas. |

## Clôture

_À remplir au plan 25-04 : totaux mesurés, cinq critères prouvés un par un, mutations jouées et révoquées,
invariants de sécurité relevés avant/après, et cochage d'EVT-01 à EVT-05 dans `REQUIREMENTS.md`._
