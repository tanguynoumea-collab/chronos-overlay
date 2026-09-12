---
phase: 25
slug: le-contrat-d-evenements-refonde
status: validated
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-12
validated: 2026-09-12
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
| **Total mesuré après 25-03** | **855 / 0 échec / 4 s** (837 + 9 + 7 + 2 ; attendu indicatif 848, écart de **+7** justifié nominativement au SUMMARY 25-03 : **+4** hérités de l'écart déjà justifié du 25-02 — la carte partait de 833, la mesure réelle était 837 — et **+3** dus aux trois tests que la revue du plan a ajoutés en T2, que la carte ne pouvait pas prévoir) |
| **Total mesuré après 25-04** | **862 / 0 échec / 6 s** (855 + 0 + 7 ; la tâche 1 n'écrit que du Markdown et n'ajoute aucun test). Attendu indicatif 854, écart de **+8** justifié nominativement au SUMMARY 25-04 en deux parts : **+7** hérités de l'écart déjà justifié du 25-03 (la carte partait de 848, la mesure réelle était 855) et **+1** pour le seul test surnuméraire, `Chaque_ligne_documentee_porte_le_role_reellement_cable`, ajouté par la revue du plan (point A9) postérieurement à la carte et nommé dans le critère d'acceptation lui-même |
| **Deux exécutions consécutives** | **862 / 0 échec / 6 s** les deux fois, à la clôture 25-04 (re-mesuré après révocation du worktree de mutation) |
| **Renommages prévus** | **7, tous annoncés** : 3 noms de tests de comptage de hooks (25-01 T2) ; le champ `StaleWorking` → `SilenceDesBattements` (25-02 T3) ; et **3 assertions héritées** converties d'`Unknown` à `WaitingDeduced` en 25-03 T2 — `Vingt_et_une_minutes_de_SILENCE_ne_se_disent_plus_en_cours`, `Monitor_lit_les_sessions_et_applique_la_staleness` (`SessionsTests`), et `Un_hook_Working_de_25_min_ne_rend_plus_un_transcript_frais_inconnu` (`InspectionSessionsTests`, l. 195-208), ce dernier **renommé** `…_frais_deduit`. Tout renommage supplémentaire doit être **justifié nominativement** dans le SUMMARY concerné. **CORRECTION MESURÉE au 25-03 :** il y avait **QUATRE** assertions à convertir, pas trois — la quatrième est `Un_battement_frais_maintient_en_cours_bien_au_dela_d_une_heure`, dont le TÉMOIN passe par le pipeline réel (`SessionStart` → `Working`) et n'écrit donc `Working` nulle part, ce qui la rendait invisible à la lecture du plan. Le compte de RENOMMAGES reste **7** : une seule des quatre conversions change de nom. Déviation n° 1 du SUMMARY 25-03. |

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
| **EVT-04** — l'interruption ne laisse plus la session invisible | 25-03 | `Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini` + `Une_session_deduite_est_visible_ambre_et_dit_sa_deduction` | **PROUVÉ** — les deux tests verts, et non vacueusement : muter `WaitingDeduced` en `WaitingTurn` dans `SessionMonitor.TryRead` (worktree jetable) fait échouer **7** tests, dont le premier par son `Assert.NotEqual(WaitingTurn, …)` explicite. 9 tests EVT-04 au total (5 en T1, 7 en T2, 2 en T3, moins les recoupements), 0 échec |
| **EVT-05** — le contrat documenté | 25-04 | `ContratHooksDocumenteTests` (6 tests) + `docs/hooks-contract.md` | **PROUVÉ** — `docs/hooks-contract.md` (**344 lignes**, **9** sections, **5** occurrences de la date du relevé dont **4 dans le §5 lui-même**) + `ContratHooksDocumenteTests` : **7** tests (les 6 annoncés + `Chaque_ligne_documentee_porte_le_role_reellement_cable`), **0 échec**. Et la garde a été **vue rouge deux fois** : 3 échecs / 7 en retirant une ligne de la table §1, puis **1 échec / 7** — celui du rôle, et lui seul — en n'altérant QUE la troisième colonne |

## Les cinq critères du ROADMAP, un par un

| # | Critère | Preuve nommée | Résultat mesuré |
|---|---------|---------------|-----------------|
| 1 | **« Attend » naît d'une vraie demande** — une permission demandée bascule en « à toi » ; une alerte d'absence ne fabrique plus aucun état (EVT-01, EVT-02) | Routage `PermissionRequest`, veto `idle_prompt` / `auth_success` / `quota_auto_resume_*`, matcher installé | **PROUVÉ au plan 25-01** — les trois preuves vertes. Reste la vérification in vivo (§ « À VÉRIFIER PAR L'UTILISATEUR » n° 1), impossible avant republication + réconciliation. |
| 2 | **« Réfléchit » ne s'éteint plus tout seul** — réaffirmé par battements, y compris au-delà d'une heure (EVT-03) | `Un_battement_frais_maintient_en_cours_bien_au_dela_d_une_heure` (pipeline complet : `Process` → `EcritureEtatSession` → `SessionMonitor.Read`) | **PROUVÉ au plan 25-02** — vert, et non vacueusement : muter le routage de `PostToolUse` dans un worktree jetable le fait échouer. Reste la vérification in vivo (§ « À VÉRIFIER PAR L'UTILISATEUR » n° 2), impossible avant republication + réconciliation. |
| 3 | **Échap est couvert** — état juste et visible, ni figé « en cours », ni disparu (EVT-04) | `Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini` (avec `Assert.NotEqual(WaitingTurn, …)`) + `IsGhost == false` | **PROUVÉ au plan 25-03** — le silence rend `WaitingDeduced`, `IsGhost == false`, `IsTurn == true`, pinceau AMBRE identique à celui d'un `WaitingTurn` du même thème et DIFFÉRENT du gris de l'inconnu. Reste la vérification in vivo (§ « À VÉRIFIER PAR L'UTILISATEUR » n° 3), impossible avant republication + réconciliation. |
| 4 | **Le silence n'affirme que ce qui a été observé** — jamais « terminée » ni « tour fini », et une déduction se **dit** déduction (**critère amendé au ROADMAP le 2026-09-12** : la lettre « se dit inconnu » imposait `Unknown`, l'intention était l'honnêteté de l'affirmation) | `Une_activite_illisible_reste_inconnue_et_non_deduite`, `Une_attente_observee_ne_se_convertit_jamais_en_deduction`, `Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini`, et le libellé `"à toi ? déduit"` | **PROUVÉ au plan 25-03** — les trois tests verts, le libellé rendu par un producteur UNIQUE (`grep '"à toi\|"tour fini\|"en cours\|"inconnu'` sur le ViewModel : **aucune ligne**), et `WaitingDeduced_n_est_jamais_ecrit_dans_un_fichier_d_etat` interdit qu'une déduction devienne persistante. **La garde de masse est la contrepartie : 20 sessions sur 54 visibles — voir A11.** |
| 5 | **Le contrat est écrit** dans `docs/`, avec ce qui n'est PAS garanti | `docs/hooks-contract.md` §5 (les trois trous datés et sourcés) + `ContratHooksDocumenteTests` | **PROUVÉ au plan 25-04** — le §5 porte les **trois** trous (5.1 Échap / 5.2 `SessionEnd` sur crash, `prompt_input_exit` / 5.3 nom d'événement inconnu, liste blanche), chacun **daté 2026-09-12 et sourcé**, plus **5.4 la limite assumée d'EVT-03** (un outil unique de plus de vingt minutes) écrite comme une LIMITE et non comme un défaut. `Le_document_porte_les_trois_trous_documentaires_avec_leur_date` cherche la date **dans le §5 découpé**, pas dans le fichier entier — une date en tête ne daterait aucun trou. Le §6 porte ce qui a été **écarté** (`MessageDisplay`, `permission_prompt`, `idle_prompt`, le `timeout` uniforme) et la **question ouverte** de la sonde de capture ; le §9, la procédure de détection et l'aveu qu'**aucun test ne peut voir une dérive de la source EXTERNE** |

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

### Résultats mesurés — un par un, à la clôture du plan 25-04

| Preuve | Attendu | **Mesuré** |
|---|---|---|
| `grep -c "PermissionRequest" …/CatalogueEvenementsHooks.cs` | ≥ 1 | **1** |
| `grep -c "CatalogueEvenementsHooks" …/SessionHookInstaller.cs` | ≥ 2 | **4** |
| `grep -c "agent_id" …/SessionHookProcessor.cs` | ≥ 1 | **2** |
| `grep -c "agent_type" …/SessionHookProcessor.cs` | ≥ 1 | **2** |
| `grep -c "NotificationsSansEtat" …/SessionHookProcessor.cs` | ≥ 2 | **2** |
| `grep -rn "StaleWorking" src/` (`.cs`) | 0 ligne | **0 ligne** |
| `grep -c "SilenceDesBattements" …/SessionMonitor.cs` | ≥ 3 | **4** |
| `grep -c "FromMinutes(20)" …/SessionMonitor.cs` | ≥ 1 | **1** — la valeur n'a **pas** bougé |
| `grep -c "FromHours(8)" …/SessionMonitor.cs` | ≥ 1 | **1** |
| `grep -c "WaitingDeduced" …/SessionSnapshot.cs` | ≥ 1 | **1** |
| `grep -c "WaitingDeduced" …/AffichageSessions.cs` | ≥ 2 | **2** |
| `grep -c "WaitingDeduced" …/SessionMonitor.cs` | ≥ 1 | **2** |
| `grep -c "EVENEMENTS-CABLES:debut" docs/hooks-contract.md` | 1 | **1** (et `:fin` → **1**) |
| `grep -c "2026-09-12" docs/hooks-contract.md` | ≥ 1 | **5**, dont **4 dans le §5 découpé** |
| `grep -cF "byId[" …/SessionMonitor.cs` | 0 | **0** |
| `grep -cF "ArbitrageSessions.Trancher(" …/SessionMonitor.cs` | 1 | **1** |
| `grep -cF "=> Inspecter(now).Visibles;" …/SessionMonitor.cs` | 1 | **1** |
| `grep -cF "new SessionMonitor" …/DiagnosticService.cs` | 0 | **0** |
| `grep -cF "?? new " …/DiagnosticService.cs` | 2 | **2** |
| `grep -cF "MotifMasquage" …/DiagnosticService.cs` | 3, INCHANGÉ | **3** |
| `grep -c "WaitingDeduced" …/SessionStylesBindingTests.cs` | ≥ 1 | **1** |
| `grep -c "SessionTreatmentTracker" docs/hooks-contract.md` | ≥ 1 | **2** |
| `git diff --stat $SHA..HEAD -- SessionTreatmentTracker.cs TreatedStore.cs ArchiveStore.cs` | VIDE | **VIDE** |
| `git diff --stat $SHA..HEAD -- ArbitrageSessions.cs LectureSessions.cs` | VIDE | **VIDE** |
| `git diff --stat $SHA..HEAD -- '*.xaml'` | `SessionStyles.xaml`, et lui seul | **`SessionStyles.xaml`, et lui seul** |
| `git diff --numstat $SHA..HEAD -- …/SessionStyles.xaml` | ≤ 1 ajout / 1 retrait | **1 / 1** |

**Un repère de plus, nécessaire au plan 25-04 :** son critère d'acceptation demandait
`git diff --stat $SHA..HEAD -- src/` **vide**, ce qui est inatteignable par construction — depuis le SHA
d'**entrée de phase**, ce diff rend les **neuf fichiers** légitimement modifiés par 25-01 à 25-03 (395
insertions / 49 suppressions). L'intention (« ce plan-ci ne touche aucun code de production ») se mesure
depuis l'entrée du **PLAN**, `78648dd` : `git diff --stat 78648dd..HEAD -- src/` → **VIDE**. Déviation n° 1
du SUMMARY 25-04.

## Mutations de falsification — prévues

| Mutation | Test attendu en échec | Mesuré |
|---|---|---|
| Retirer une ligne de la table §1 de `docs/hooks-contract.md` (worktree jetable) | `ContratHooksDocumenteTests.La_table_documentee_liste_EXACTEMENT_les_evenements_cables` | **3 échecs / 7 mesurés** (worktree `chronos-mutant-contrat` détaché sur `6386107`, ligne `PostToolUse` retirée) : le test attendu — dont le message **nomme** l'entrée disparue, « CÂBLÉS mais NON DOCUMENTÉS : PostToolUse » — plus les deux gardes de cellule, qui ne trouvent plus la ligne |
| **(ajoutée, non demandée)** Altérer la SEULE troisième cellule d'une ligne — le rôle de `Stop` — nom et `matcher` laissés intacts | `ContratHooksDocumenteTests.Chaque_ligne_documentee_porte_le_role_reellement_cable` | **1 échec / 7 mesuré**, et lui seul : les six autres restent **VERTS**, y compris `La_table_documentee_liste_EXACTEMENT_les_evenements_cables`. C'est la seule mesure qui prouve le point **A9** — sans cette garde, **une description fausse passerait entièrement inaperçue**. Contrôle involontaire : une première tentative dont le motif ne rencontrait pas la flèche n'a rien modifié, et la suite est restée à **7 verts** — la garde n'est donc pas rouge par construction. Worktree **révoqué** : `git worktree list` réduit au dépôt principal, dossier absent de `…/DEV`, `git status --porcelain` vide, `grep -rlF "MUTANT"` → **0** en `.cs` comme en `.md` |
| Supprimer le veto sous-agent dans `SessionHookProcessor` (worktree jetable) | **3** cas si jouée en 25-02 T1, **5** si rejouée en fin de 25-02 T2 (seule mesure probante) — dont `Un_SessionEnd_de_sous_agent_ne_supprime_pas_le_fichier_d_etat_du_parent`, qui asserte `Ignore == true` ET `Delete == false` | **3 mesurés en T1, 5 mesurés en fin de T2** (worktree `chronos-mutant-veto`, révoqué ; `git status --porcelain` vide, 0 fichier `MUTANT`) |
| Faire écrire huit processus en parallèle sans la parade du point (f) de 25-02 T2 (worktree jetable) | `Huit_ecrivains_paralleles_sur_la_meme_session_n_echouent_jamais` | **MESURÉ SUR L'ARBRE, avant correctif : 288 refus / 400** (`IOException` de partage). Avec la reprise **unique** d'abord retenue : encore **209 puis 180 / 400** — elle ne corrige pas. Avec la reprise **bornée** livrée : **0 / 400**, trois exécutions consécutives. |
| Rendre `WaitingTurn` au lieu de `WaitingDeduced` dans `SessionMonitor.TryRead` (worktree jetable) | `Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini` | **7 échecs / 25 mesurés** (worktree `chronos-mutant-deduction` détaché sur `701321b`, mutation compilable `// MUTANT`), dont le test attendu, qui échoue sur son `Assert.NotEqual(WaitingTurn, …)`. Worktree **révoqué** : `git worktree list` réduit au dépôt principal, `git status --porcelain` vide, `grep -rlF "MUTANT" --include=*.cs` → **0**. |

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
| 25-03 T2 | `InspectionSessionsTests` | ≥ 2 | **exactement 7 échecs / 25 tests** — `Vingt_et_une_minutes_de_SILENCE_ne_se_disent_plus_en_cours`, `Un_battement_frais_maintient_en_cours_bien_au_dela_d_une_heure`, `Combien_de_sessions_d_un_corpus_realiste_basculent_ensemble_en_deduction`, `Un_hook_Working_de_25_min_ne_rend_plus_un_transcript_frais_deduit`, `Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini`, `Une_deduction_ne_bat_pas_un_transcript_plus_recent`, `Un_hook_deduit_et_un_transcript_travaillant_du_MEME_instant_sont_departages_par_la_source`. **T1 mesurée aussi : 1 échec / 23** — les quatre autres cas ajoutés en T1 sont des gardes de COMPLÉTUDE, vertes d'emblée par le `_` du switch (voir SUMMARY 25-03). **T3 : 2 échecs / 25.** |
| 25-04 T2 | `ContratHooksDocumenteTests` | tous tant que le document n'est pas lu | **exactement 7 échecs / 7 tests** — la classe a été écrite AVANT l'attribut `CheminDocsChronos`, donc `CheminDocs()` rend la chaîne vide et aucun test ne lit quoi que ce soit. **Ce que cette étape prouve : la NON-MISE-EN-SOURDINE** (une garde qui perd son chemin ÉCHOUE au lieu de se taire). **Ce qu'elle ne prouve pas :** que les comparaisons soient justes — le document existait déjà, donc tout est passé au vert dès l'injection. C'est à cela que servent les **deux mutations** ci-dessus, et il serait malhonnête de s'en passer |

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
| `~/.claude/settings.json` | **jamais modifié à la main** — seul le code livré y écrira, au prochain lancement de l'overlay | **md5 `78eb517cc1a453b2ddce9a39af528900` → `78eb517cc1a453b2ddce9a39af528900`** — identique d'un bout à l'autre des quatre plans, jamais touché |
| `%APPDATA%\Chronos\sessions` | **66** entrées, INCHANGÉ | **66 → 65** — **attente NON TENUE, et la cause n'est pas la phase.** Composition mesurée : **53 `.json` + 12 reliquats `.tmp-*`** (contre 54 + 12 au 25-03) : **un `.json` a disparu, aucun `.tmp-*` n'est apparu** — donc aucune régression vers un schéma « fichier temporaire déplacé ». Cause : les **cinq hooks de l'ancienne génération sont VIVANTS** dans le `settings.json` de l'utilisateur ; toute session Claude Code ouverte sur cette machine — **y compris celle qui exécute la phase** — fait invoquer `Chronos.exe --hook …`, et un `SessionEnd` réel supprime un fichier d'état. Preuve directe : deux fichiers portent des horodatages postérieurs au relevé d'entrée (`3498ae3d-….json` à 22:15:47, `74ac9f48-….json` à 21:21:27 — l'identifiant de la session courante). **Aucune commande des quatre plans n'écrit ni ne supprime dans ce dossier** : `ContratHooksDocumenteTests` ne fait **aucune** écriture et ne lit que `docs/`. Figer le compte d'un magasin VIVANT comme invariant de sécurité était une attente trop forte de cette carte ; l'invariant qui compte — « la phase n'y touche pas » — est tenu |
| `%APPDATA%\Chronos\archived.json` | **84** octets | **84 → 84** |
| `%APPDATA%\Chronos\oauth.dat` (**taille seule, JAMAIS le mtime**) | **518** octets | **518 → 518** — mtime **JAMAIS consulté**, aux quatre plans |
| Overlay `Chronos-v3.0.2.exe` (pid 119412) | vivant, ni lancé ni tué (`tasklist` seul) | **vivant, pid 119412** — `tasklist` seul, jamais lancé, jamais tué |
| Sonde de capture installée dans `~/.claude/` | **0** — interdit ; si le besoin devient bloquant, le consigner comme question à l'utilisateur | **0** — et la question est **posée par écrit**, au §6 de `docs/hooks-contract.md` (« Question OUVERTE, à poser à l'utilisateur ») plutôt que tranchée d'office |
| `git diff -- '*.csproj'` | vide, **sauf** `tests/Chronos.Tests/Chronos.Tests.csproj` au plan 25-04 (ajout de `CheminDocsChronos`) | **exactement cela** : `git diff --stat $SHA..HEAD -- '*.csproj'` → `tests/Chronos.Tests/Chronos.Tests.csproj`, **et lui seul**, 10 insertions / 0 suppression (l'attribut et son commentaire de justification) |
| Dépendances NuGet ajoutées | **0** | **0** — aucun `PackageReference` ajouté dans le diff du `.csproj` |
| Tests écrivant hors de `Path.GetTempPath()` | **0** — garde anti-accident `Assert.StartsWith(Path.GetTempPath(), d)` | **0** — `Aucun_test_ne_cible_le_vrai_settings_du_profil` vert ; la classe ajoutée au 25-04 **n'ouvre aucun fichier en écriture** : elle ne lit que `docs/hooks-contract.md`, par un chemin **injecté** par MSBuild |
| Requêtes réseau réelles depuis un test | **0** | **0** |
| Worktrees de mutation restants | **0** | **0** — `git worktree list` réduit au dépôt principal après chacune des mutations des plans 25-02, 25-03 et 25-04 |

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
> | Sessions basculant ensemble en « à toi ? déduit » (corpus de 66 états, test de 25-03 T2) | chiffré, pas deviné | **20**, sur **54** lignes visibles (soit un peu plus d'une sur trois) ; les 12 autres états du corpus sont écartés pour leur âge. `WaitingCount` correspondant : **42** (20 déduites + 20 « tour fini » + 2 « à toi »). Mesuré par `Combien_de_sessions_d_un_corpus_realiste_basculent_ensemble_en_deduction`, à instant FIGÉ, sous `Path.GetTempPath()`. **Mise en garde honnête : ce nombre est une propriété du CORPUS autant que du seuil** — le magasin réel de 66 entrées est aujourd'hui un cimetière (ses 54 `.json` sont TOUS au-delà de huit heures, mesuré le 2026-09-12), donc in vivo, aujourd'hui, ce nombre serait **zéro**. Le corpus place délibérément 12 `Working` juste sous le seuil (1 à 12 min) et 20 juste au-dessus (21 à 40 min) : il mesure le pire cas d'un magasin VIVANT de cette forme, et il est sensible au seuil dans les deux sens. |
> | Sessions en « à toi ? déduit » dans le widget réel, après republication | à relever in vivo | **NON MESURABLE À LA CLÔTURE, et ce n'est pas une case oubliée.** Le relevé exige (a) la republication de l'exe, (b) son lancement, (c) la réconciliation de `settings.json` qui n'a lieu qu'à ce lancement, (d) la réouverture des sessions Claude Code — la configuration des hooks étant lue au DÉMARRAGE d'une session. La phase s'interdit explicitement de lancer ou de tuer l'overlay et d'écrire dans le `settings.json` de l'utilisateur : **aucun chiffre honnête ne peut être écrit ici aujourd'hui**. Le succédané mesuré est le corpus déterministe de 66 états ci-dessus (**20 / 54**). **Reste à la charge de l'utilisateur**, après republication |
> | `WaitingCount` correspondant | à relever in vivo | **NON MESURABLE À LA CLÔTURE** — même cause, même chaîne de préalables. Valeur attendue par le corpus, à comparer une fois in vivo : **42** pour 54 lignes visibles |
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

## Clôture — mesurée le 2026-09-12, au plan 25-04

| Point de clôture | Résultat |
|---|---|
| **Totaux** | **788 → 817 → 837 → 855 → 862**, toujours **0 échec**. **862 = 788 + 74** cas ajoutés en quatre plans. **Aucun test supprimé, aucun désactivé, aucun `Skip`.** Deux exécutions consécutives à la clôture : **862 / 0 échec / 6 s**, identiques |
| **Écarts aux attendus indicatifs** | tous justifiés **nominativement**, plan par plan : +1 (25-01, le huitième test nommé au bloc (e)), +4 (25-02, corrections A4 et B6 du plan-checker + le 2ᵉ cas d'une `[Theory]`), +7 (25-03, dont +4 hérités et +3 tests ajoutés par la revue), +8 (25-04, dont **+7 hérités** et **+1** neuf : `Chaque_ligne_documentee_porte_le_role_reellement_cable`) |
| **Les cinq critères du ROADMAP** | **prouvés un par un** — voir la table ci-dessus. Les quatre premiers gardent une part **in vivo** impossible avant republication ; le cinquième est entièrement livré |
| **EVT-01 à EVT-05** | **PROUVÉS**, à cocher dans `REQUIREMENTS.md` |
| **Mutations de falsification** | **cinq jouées, cinq mesurées, cinq révoquées** : veto sous-agent (3 puis **5**), écrivains concurrents (288 → 209/180 → **0** / 400), déduction (**7**), table du contrat (**3**), rôle documenté (**1**, et lui seul). Toutes en `git worktree` jetable ; après chacune, `git worktree list` réduit au dépôt principal, `git status --porcelain` vide, `grep -rlF "MUTANT"` → **0** |
| **Renommages** | **7, tous annoncés** — aucun renommage supplémentaire dans les plans 25-03 et 25-04. Correction mesurée : **4 conversions** d'assertions héritées, dont **1** seule change de nom |
| **Invariants de sécurité** | tous tenus, **sauf le compte du magasin de sessions** (**66 → 65**), dont la cause est le système VIVANT et non la phase — analyse nominative dans la table ci-dessus et au SUMMARY 25-04 |
| **Périmètre** | `SessionTreatmentTracker.cs`, `TreatedStore.cs`, `ArchiveStore.cs`, `ArbitrageSessions.cs`, `LectureSessions.cs` : **diffs VIDES** sur toute la phase. Un seul XAML touché, **1 ligne / 1 ligne**, dans un commentaire de tête. Un seul `.csproj` touché, celui des tests, pour l'injection du chemin de `docs/`. **0 dépendance NuGet ajoutée** |

### Ce qui reste OUVERT à la sortie de la phase

1. **Les vérifications in vivo (1 à 6, et les deux lignes d'A11)** — toutes conditionnées à la
   **republication de l'exe**, à son lancement, à la réconciliation de `settings.json` et à la réouverture
   des sessions. Rien de tout cela ne peut être fait par la phase, qui se l'interdit.
2. **La question de la sonde de capture** (point 7), écrite au §6 de `docs/hooks-contract.md` :
   **à arbitrer par l'utilisateur**. Tant qu'elle n'est pas tranchée, aucun nom de champ spécifique à un
   événement n'est confirmable et le code ne doit pas s'y appuyer.
3. **Les deux entrées transmises à la phase 26** — la divergence `SessionTreatmentTracker` /
   `WaitingDeduced` (écrite au §3 du contrat) et l'ordre des rangs dans `ArbitrageSessions.Departager`.
4. **Les reliquats hérités** (point 8), à présenter **groupés** en fin de milestone.
