---
phase: 26
slug: traite-veut-enfin-dire-quelque-chose
status: complete
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-12
validated: 2026-09-13
---

# Phase 26 — Validation Strategy

> Carte **posée à la planification**, **remplie et mesurée** au fil des quatre plans, **close au plan
> 26-04**. Toute case encore vide à la clôture est un **défaut de la phase**, pas une approximation.
>
> Dernière phase du milestone v1.6 : elle lui applique sa propre doctrine. **Ne rien écrire ici qui n'ait
> été mesuré.** Une case remplie « d'après le plan » plutôt que d'après une exécution est exactement la
> faute que ce milestone corrige — et « traité » est une affirmation forte sur ce que l'utilisateur a fait.
>
> **Clôture du 2026-09-13.** Toutes les cases « Mesuré » portent une valeur **réellement observée** dans une
> sortie de test ou de `grep`. Les relevés qui ne seront possibles qu'**in vivo** sont marqués comme tels,
> avec leur chaîne de préalables — jamais laissés vides.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`net8.0-windows`) — `tests/Chronos.Tests` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Quick run (traitement)** | `… --filter "FullyQualifiedName~TreatedSessions\|FullyQualifiedName~GesteTraite\|FullyQualifiedName~ArchiveStorePurge\|FullyQualifiedName~SessionStylesBinding\|FullyQualifiedName~GardesPerimetre\|FullyQualifiedName~ContratHooksDocumente\|FullyQualifiedName~Inspection\|FullyQualifiedName~Arbitrage"` |
| **SHA d'entrée de phase** | `07ee784` — toute mesure de diff s'écrit `git diff --stat 07ee784..HEAD -- <chemins>` ; **sans révision de base, `git diff --stat` est VIDE après commit** |
| **SHA d'entrée du plan 26-04** | `d28392e` (relevé par `git rev-parse HEAD` en début de plan) — c'est la base opposable du critère « la vague 4 ne touche pas `src/Chronos` ». **`07ee784` ne convient pas pour ce critère** : il montrerait tous les fichiers des vagues 1 à 3. |
| **Baseline d'entrée de phase** | **868 tests / 0 échec / 5 s** — *remesurée au début du plan 26-01 : la prédiction de 868 est **tenue exactement**, la durée relevée est de 5 s (l'estimation portait « ~4-5 s »).* |
| **Cible de fin de phase** | **0 échec, aucun test supprimé.** Le critère n'est PAS un plancher (un plancher se franchit sans rien ajouter) mais une **égalité** : `total = total mesuré du plan précédent + nombre de cas ajoutés`, tout écart — **même positif** — justifié **nominativement** dans le SUMMARY concerné. Attendus **indicatifs** : **876** (26-01), **879** (26-02), **886** (26-03), **888** (26-04). Détail indicatif : +1 (26-01 T1), **+7 (26-01 T2, dont 6 rouges)**, +0 (26-01 T3), +3 (26-02 T1), +0 (26-02 T2), **+5 (26-03 T1)**, +0 (26-03 T2), +2 (26-03 T3), +2 (26-04 T1), +0 (26-04 T2). |
| **Total mesuré après 26-01** | **876 / 0 échec / 6 s** — attendu 876. *Chaîne interne mesurée : 868 (baseline) → 869 (T1, +1) → 876 (T2, +7 dont **6 rouges**) → 876 (T3, +0, 0 échec).* **Tenue exactement.** |
| **Total mesuré après 26-02** | **879 / 0 échec / 5 s puis 6 s** — attendu 879. *Chaîne interne mesurée : 876 → 879 (T1, +3 dont **2 rouges**) → 879 (T2, +0, 0 échec).* **Tenue exactement.** |
| **Total mesuré après 26-03** | **886 / 0 échec / 7 s** — attendu 886. *Chaîne interne mesurée : 879 → 884 (T1, +5) → 884 (T2, +0) → 886 (T3, +2), **zéro rouge**, zéro annoncé.* **Tenue exactement.** Une mesure à **9 s** relevée à la première passe suivant une reconstruction complète : **dite, non corrigée** (voir « Écarts »). |
| **Total mesuré après 26-04** | **888 / 0 échec / 6 s** — attendu 888. *Chaîne interne mesurée : 886 (baseline d'entrée de plan, relevée sur `d28392e`) → 888 (T1, +2, 0 échec) → 888 (T2, +0).* **Tenue exactement.** |
| **Deux exécutions consécutives** | **MESURÉES à la clôture 26-04, après le dernier commit de code/tests : `888 / 0 échec / 6 s` puis `888 / 0 échec / 6 s`.** Total identique, aucun test instable, aucune dépendance à l'horloge de la machine. *(Chaque plan a par ailleurs fait la sienne : 876/876 en 26-01, 879/879 en 26-02, 886/886 en 26-03.)* |
| **Renommages prévus** | **1, annoncé — 1 mesuré.** `Purger_n_est_pas_expirer_une_entree_vieille_reste_dans_le_fichier` → `Purger_n_est_pas_expirer_et_une_entree_vieille_reste_lisible` (26-02 T2), parce que son assertion finale s'inverse avec le retrait de la durée de vie. **Aucun renommage supplémentaire, dans aucun des quatre plans.** Les renommages **internes au code** effectivement survenus (`IsWaiting` → `EstAttente`, `_lastActivity` → `_dernier`, `_waitingSince` → `_attenteDepuis`, en 26-01) ne sont pas des renommages de tests, comme annoncé. Le point (b bis) du plan 26-01 a bien remonté la borne de `TreatedStore` **sans** renommer `TreatedStore_set_load_remove_et_purge_TTL` : le nom du test porte encore `TTL`, et c'est assumé pour ne pas ajouter un second renommage non annoncé. |
| **Suppressions de tests** | **0 — mesuré.** Aucun test n'a été supprimé dans cette phase : 868 → 888, soit **+20**, exactement le nombre de cas ajoutés (1+7+3+5+2+2). |

## Le piège de test à retardement de ce projet

`TreatedStore.Load()` et `ArchiveStore` lisaient l'horloge **SYSTÈME** sans injection : **trois plans de la
phase 22** sont tombés dessus, avec des tests verts à l'écriture et rouges quelques heures plus tard sans
qu'une ligne de code ait bougé. Les plans 26-01 et 26-02 **introduisent l'horloge injectable** dans les deux
magasins et retirent toute dépendance à l'heure de la machine des tests concernés.

**Critère opposable, mesurable :**

| Fichier | Attendu | Mesuré |
|---|---|---|
| `grep -c "DateTimeOffset.UtcNow" src/Chronos/Services/TreatedStore.cs` | **0** (26-01) | **0** ✅ |
| `grep -c "DateTimeOffset.UtcNow" src/Chronos/Services/ArchiveStore.cs` | **0** (26-02) | **0** ✅ |
| `grep -c "DateTimeOffset.UtcNow" tests/Chronos.Tests/TreatedSessionsTests.cs` | **0** (26-01) | **0** ✅ |
| `grep -c "DateTimeOffset.UtcNow" tests/Chronos.Tests/ArchiveStorePurgeTests.cs` | **0** (26-02) | **0** ✅ — *valeur d'entrée mesurée avant le plan 26-02 : **2***. |

*Mesures refaites à la clôture du 2026-09-13, sur l'arbre de travail de fin de phase.* Corollaire observé :
les deux exécutions consécutives de clôture rendent le **même** total à des instants différents, et les
suites `TreatedSessions` / `ArchiveStorePurge` sont vertes sur les quatre plans. Le piège est refermé par
construction, pas par chance d'horaire.

## La borne du magasin des traitées ne porte plus sur l'instant de l'ÉPISODE

Décision du plan 26-01, point **(b bis)**. Depuis TRT-02, la valeur mémorisée dans `treated.json` est
l'instant que le **signal** porte, pas celui du guetteur. Une borne de six heures adossée à cet instant
rendait le magasin **aveugle à toute session attendant depuis plus de six heures** — soit précisément la
session `e465420e` dont ce milestone est parti, et deux heures pleines (entre six heures et le `DropAfter`
de huit) où « marquer traitée » était un **no-op silencieux**. La borne devient une RÉTENTION de fichier,
strictement supérieure à `DropAfter`. La réversibilité reste portée par NET-03, jamais par une horloge.

| Fichier | Attendu | Mesuré |
|---|---|---|
| `grep -c "FromHours(6)" src/Chronos/Services/TreatedStore.cs` | **0** (26-01, point (b bis)) | **0** ✅ |
| `grep -c "RetentionMax" src/Chronos/Services/TreatedStore.cs` | **≥ 3** (26-01, point (b bis)) | **4** ✅ (`≥ 3` satisfait ; la quatrième occurrence est la XML-doc du champ) |

**Preuve opposable, MESURÉE hors commit au plan 26-03 :** `RetentionMax` remise à `FromHours(6)`, la suite
`GesteTraite` tombe à **4 réussites sur 5**, et le **seul** échec est nominativement
`Marquer_traitee_une_session_qui_attend_depuis_sept_heures_la_fait_disparaitre`. Fichier restauré par
`git checkout`, arbre de travail propre vérifié. Sans (b bis), « marquer traitée » n'aurait rien fait sur la
session qui a motivé le milestone.

## Le test qui compte le plus : rouge AVANT

Le critère 2 du ROADMAP porte sur une panne **réellement survenue chez l'utilisateur**, avec ses chiffres.
Un test qui naîtrait vert ne prouverait rien.

| Test | Plan | Doit être ROUGE en | Doit être VERT en | Mesuré |
|---|---|---|---|---|
| `Le_scenario_mesure_des_478_minutes_laisse_la_session_visible` | 26-01 | T2 | T3 | **ROUGE en T2** (l. 371 du fichier de tests ; le hook franchit `DropAfter` au cycle 2, le transcript reprend la main, NET-01 concluait « répondu ») → **VERT en T3**, et vert à la clôture |
| `Une_bascule_de_source_ne_marque_rien` | 26-01 | T2 | T3 | **ROUGE en T2** (l. 222) → **VERT en T3**, et vert à la clôture |
| `Une_attente_deduite_ouvre_un_episode` | 26-01 | T2 | T3 | **ROUGE en T2** (l. 241 ; `IsWaiting` ignorait `WaitingDeduced`) → **VERT en T3**, et vert à la clôture |
| `Une_attente_qui_devient_deduite_n_est_pas_traitee` | 26-01 | T2 | T3 | **ROUGE en T2** (l. 261) → **VERT en T3**, et vert à la clôture |
| `Un_etat_inconnu_n_affirme_pas_qu_on_a_repondu` | 26-01 | T2 | T3 | **ROUGE en T2** (l. 279) → **VERT en T3**, et vert à la clôture |
| `Le_traite_survit_au_redemarrage_mais_pas_a_une_nouvelle_demande` | 26-01 | T2 | T3 | **ROUGE en T2** (l. 300 ; tracker neuf → épisode redaté à `now` → NET-03 purgeait le magasin qu'il venait de lire) → **VERT en T3**, et vert à la clôture |
| `Une_archive_ne_s_evapore_jamais_meme_apres_huit_jours` | 26-02 | T1 | T2 | **ROUGE en T1** (l'entrée de huit jours était écartée par `now - ts < Ttl.TotalMilliseconds` dans `Load()`) → **VERT en T2**, et vert à la clôture |
| `Le_magasin_d_archives_ne_connait_plus_aucune_duree_de_vie` | 26-02 | T1 | T2 | **ROUGE en T1** (le champ statique `Ttl` de type `System.TimeSpan` siégeait encore dans un magasin qui promet le définitif) → **VERT en T2**, et vert à la clôture |
| `Marquer_traitee_une_session_qui_attend_depuis_sept_heures_la_fait_disparaitre` | 26-03 | **rouge sans le point (b bis) de 26-01** — non mesurable rouge dans l'ordre des vagues, (b bis) la précède | 26-03 T1 | **VERT dès son écriture en T1**, comme annoncé — **et MESURÉ ROUGE hors commit** : `RetentionMax` remise à `FromHours(6)`, suite `GesteTraite` **4/5**, seul échec = ce test. Fichier restauré, arbre propre. |

La dernière ligne **ne compte pas** parmi les rouges mesurables : c'est la preuve opposable du point (b bis)
du plan 26-01, livré en vague 1, alors que le test naît en vague 3. Elle est ici parce que sa raison d'être
est la même que celle des huit autres — sans elle, les quatre autres cas de `GesteTraiteTests`, tous
horodatés au frais, seraient **vacueusement verts** au regard des critères 1 et 4 du ROADMAP. L'exécutant du
plan 26-03 a choisi de la voir rouge, hors commit ; ce n'était pas exigé, c'est mesuré.

**Total des rouges attendus : 6 en 26-01 T2, 2 en 26-02 T1.**
**Total des rouges MESURÉS : 6 en 26-01 T2, 2 en 26-02 T1 — les huit nominativement ceux qui étaient
annoncés, aucun de plus, aucun de moins.** *Un échec de moins qu'annoncé est un test vacueux ; un échec de
plus est une régression.* Ni l'un ni l'autre ne s'est produit. Sorties de runner correspondantes :
`échec : 6, réussite : 9, total : 15` (26-01 T2, filtre `~TreatedSessions`) et
`échec : 2, réussite : 7, total : 9` (26-02 T1, filtre `~ArchiveStorePurge`).

**Verts dès leur écriture, et annoncés comme tels** (ne PAS les compter parmi les rouges) :
`Les_vainqueurs_sont_les_retenus_avec_leur_source` (26-01 T1),
`Un_episode_reellement_plus_recent_purge_toujours` (26-01 T2),
`L_horodatage_d_une_archive_vient_de_l_horloge_injectee` (26-02 T1),
les **cinq** cas de `GesteTraiteTests` (26-03 T1), les deux gardes de 26-03 T3, les deux gardes de 26-04 T1.
**Les douze ont été mesurés verts à leur naissance**, et le sont restés à la clôture.

## La falsifiabilité des gardes « vertes dès leur écriture » — mutation MESURÉE (26-04)

Une garde verte à la naissance ne vaut que si l'on prouve qu'elle **sait devenir rouge**. Les deux gardes du
plan 26-04 ont donc été mutées dans un **`git worktree` jetable, hors du dépôt**, détaché sur `a54f5e3`.
Référence dans le worktree avant mutation : `0 échec, 9 réussites, total 9`.

| Mutation | Portée | Attendu | Mesuré |
|---|---|---|---|
| **M1** — remettre dans le §3 le titre « ⚠ Divergence connue — transmise à la phase 26, NON corrigée ici » | document | `Le_document_ne_transmet_plus_de_divergence_a_la_phase_26` rouge | **`échec : 1, réussite : 8, total : 9`** ; l'unique `[FAIL]` est ce test, nominativement |
| **M2** — abaisser la casse : « transition observée sur la **même** source » | document | `Le_document_dit_la_regle_de_traitement_reellement_cablee` rouge | **`échec : 1, réussite : 8, total : 9`** ; l'unique `[FAIL]` est ce test. **Et `grep -c "même source"` passe de 2 à 3** : une garde adossée à cette chaîne serait restée verte — la preuve directe qu'elle ne s'y adosse pas |
| **M3** — retirer `WaitingDeduced` du prédicat `EstAttente` (la divergence exacte que la phase a refermée) | **code** | garde croisée rouge | **`échec : 1, réussite : 8, total : 9`** ; l'unique `[FAIL]` est `Le_document_dit_la_regle_de_traitement_reellement_cablee`. La garde est donc **bidirectionnelle** : elle attrape la dérive du document *et* celle du détecteur |

**Révocation prouvée.** `git checkout -- .` dans le worktree, puis `git worktree remove` / `git worktree
prune` : `git worktree list` est **revenu à 1** (le seul dépôt principal), `git status --porcelain` est
**vide**, et `grep -rl "MUTATION-B5"` sur le dépôt rend **0 fichier**.

## Couverture des exigences

| Exigence | Plan | Preuve nommée attendue | Mesuré |
|---|---|---|---|
| **TRT-01** — transition observée sur la MÊME source | 26-01 | `Le_scenario_mesure_des_478_minutes_laisse_la_session_visible`, `Une_bascule_de_source_ne_marque_rien`, `Une_attente_deduite_ouvre_un_episode`, `Une_attente_qui_devient_deduite_n_est_pas_traitee`, `Un_etat_inconnu_n_affirme_pas_qu_on_a_repondu` | **COUVERTE** — les **cinq** preuves nommées existent, sont vertes à la clôture, et **quatre d'entre elles ont été mesurées ROUGES en 26-01 T2** (la cinquième, `Une_attente_deduite_ouvre_un_episode`, l'a été aussi : les cinq sont dans la liste des six rouges). Le code applique la règle en un seul endroit : `prec.Source == v.Source && EstAttente(prec.Activite) && EstTravailObserve(etat)`. **Preuve documentaire ajoutée en 26-04 :** `Le_document_dit_la_regle_de_traitement_reellement_cablee`, dont la falsifiabilité est mesurée ci-dessus (M2, M3) |
| **TRT-02** — le traité survit au redémarrage | 26-01 | `Le_traite_survit_au_redemarrage_mais_pas_a_une_nouvelle_demande` (les DEUX temps) | **COUVERTE** — preuve nommée **mesurée ROUGE en 26-01 T2** (l. 300), verte depuis, et verte à la clôture. Les deux temps y sont : le traité survit au redémarrage **et** la session revient sur une demande neuve (NET-03 intact, tenu en plus par `Un_episode_reellement_plus_recent_purge_toujours`). Le siège dans le code est `InstantDuSignal`, dont la garde croisée de 26-04 exige la présence |
| **TRT-03** — geste explicite | 26-03 | `Marquer_traitee_fait_disparaitre_la_session_immediatement`, `Marquer_tout_traite_vide_le_widget_en_un_geste`, `Le_libelle_du_geste_de_masse_porte_le_nombre_de_sessions`, `Le_geste_est_reversible_et_le_libelle_ne_ment_pas`, `Marquer_traitee_une_session_qui_attend_depuis_sept_heures_la_fait_disparaitre`, `Les_huit_menus_offrent_le_geste_explicite_et_disent_la_verite`, `Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes` | **COUVERTE** — les **sept** preuves nommées existent et sont **vertes à la clôture** (relancées nominativement le 2026-09-13). Aucune n'est vacueuse : chacune porte une assertion anti-muette (5 items, 8 gabarits, 8 styles, 72 montages, au moins un menu par style). La cinquième a de plus été **mesurée rouge hors commit** sans le point (b bis) |
| **TRT-04** — contrat d'archivage unique | 26-02 + 26-03 | `Une_archive_ne_s_evapore_jamais_meme_apres_huit_jours`, `Le_magasin_d_archives_ne_connait_plus_aucune_duree_de_vie`, `Purger_n_est_pas_expirer_et_une_entree_vieille_reste_lisible`, et le libellé « Archiver définitivement (ne revient jamais) » ×8 tenu par `Les_huit_menus_…` | **COUVERTE, ses deux volets réunis** — volet **code** en 26-02 : les deux premières preuves **mesurées ROUGES en T1**, vertes en T2 ; la troisième renommée et son assertion inversée. Volet **libellé** en 26-03 : `Les_huit_menus_offrent_le_geste_explicite_et_disent_la_verite` contrôle le texte des **huit** menus. Les quatre preuves sont vertes à la clôture. *Réserve de traçabilité : la case a été cochée en 26-02, une vague avant l'arrivée de son second volet — voir « Écarts » n° 4* |

**Les quatre exigences TRT portent chacune au moins une preuve nommée, verte et non vacueuse. Les quatre
cases de `.planning/REQUIREMENTS.md` sont cochées et les quatre lignes de traçabilité portent `Complete`.**

## Les cinq critères du ROADMAP

| # | Critère | Preuve nommée attendue | Mesuré |
|---|---|---|---|
| 1 | Répondre fait disparaître, expirer non | `NET01_repondu_marque_traitee` (toujours vert) **et** `Une_bascule_de_source_ne_marque_rien` | **PROUVÉ en salle** — les deux verts à la clôture ; le second **mesuré rouge avant** 26-01 T3. « Expirer » ne marque plus rien : une bascule de source n'ouvre pas de transition. *Observation in vivo : préalables ci-dessous.* |
| 2 | Le masquage de 6 h ne se reproduit plus | `Le_scenario_mesure_des_478_minutes_laisse_la_session_visible`, **rouge en 26-01 T2** | **PROUVÉ en salle** — **mesuré ROUGE en 26-01 T2**, vert depuis. Le scénario rejoue les chiffres RÉELS (`tHook = 1789181509266`, `tEpisode = 1789210240523`, écart **478 min** figé par `Assert.Equal(478, …)`, cycle 2 à 481 min > `DropAfter` 480 min). *Observation in vivo : préalables ci-dessous.* |
| 3 | Le traité survit au redémarrage | `Le_traite_survit_au_redemarrage_mais_pas_a_une_nouvelle_demande` | **PROUVÉ en salle** — **mesuré ROUGE en 26-01 T2**, vert depuis, les deux temps couverts. *Observation in vivo : préalables ci-dessous.* |
| 4 | Un geste explicite existe, 8 styles × 9 thèmes | `Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes` + non-régression de `Les_8_styles_et_les_9_themes_se_chargent_se_mesurent_et_se_disposent` (compacité) | **PROUVÉ en salle** — les deux verts à la clôture ; le second relevé à **998 ms** en 26-03 sans aucune taille dégénérée sur les **72** combinaisons : la compacité tient malgré trois entrées de menu. `DataTemplate x:Key=` = **8** dans `SessionStyles.xaml`. *Observation in vivo : préalables ci-dessous.* |
| 5 | « Archiver » fait ce qu'il annonce | `Une_archive_ne_s_evapore_jamais_meme_apres_huit_jours` + les huit libellés | **PROUVÉ en salle** — **mesuré ROUGE en 26-02 T1**, vert depuis ; `ArchiveStore` n'a plus **aucun** champ de durée (garde par réflexion, insensible au texte des commentaires), et les **huit** menus disent « Archiver définitivement (ne revient jamais) ». *Observation in vivo : préalables ci-dessous.* |

### Ce qui n'est PAS encore observé, et pourquoi

Les cinq critères sont prouvés **en salle** : par des tests qui rejouent les chiffres relevés chez
l'utilisateur, pas par une observation de son écran. **Aucun des cinq n'a été observé in vivo**, et cela ne
pourra l'être qu'après la chaîne de préalables suivante, dans cet ordre :

1. **republier l'exe** (`dotnet publish`, mono-fichier win-x64, version incrémentée et portée dans le nom du
   fichier publié) ;
2. **relancer l'overlay** — la réconciliation de `~/.claude/settings.json` n'a lieu qu'au lancement, en
   **mode overlay uniquement**, et seulement si le widget de sessions est activé ;
3. **rouvrir les sessions Claude Code** — la configuration des hooks est lue au **DÉMARRAGE** d'une session ;
   les sessions déjà ouvertes ne sont pas suivies ;
4. **attendre une vraie demande de permission**, y répondre, et vérifier que la session quitte le widget —
   puis en laisser une expirer et vérifier qu'elle, **ne** le quitte **pas**.

Tant que ces quatre étapes n'ont pas eu lieu, la configuration de l'utilisateur ne porte que les **5 hooks
v3.0.2**, sans `PermissionRequest` et sans `matcher` sur `Notification` : **rien de ce que le milestone v1.6
a livré ne s'exécute chez lui.** C'est un fait mesuré, pas une prudence de style.

## Acquis à ne pas casser — gardes qui doivent rester vertes

Toutes les valeurs ci-dessous ont été **remesurées à la clôture du 2026-09-13**, sur l'arbre de fin de phase.

| Phase | Garde | Attendu | Mesuré |
|---|---|---|---|
| 20 | `grep -cF "?? new " src/Chronos/Services/DiagnosticService.cs` | **2** — *inchangé. Le dépôt en compte **6** dans `src/` ; le 2 de la phase 22 portait sur ce fichier SEUL. Les deux `clock ?? new SystemClock()` introduits par 26-01 et 26-02 sont hors de ce fichier et n'entrent pas dans ce compte.* | **2** ✅ |
| 21 | `DataTemplate x:Key=` dans `SessionStyles.xaml` | **8** | **8** ✅ |
| 21 | `Le_demarrage_purge_les_identifiants_fantomes_du_magasin_d_archives` | vert | **vert** ✅ |
| 22 | `new SessionMonitor` dans `DiagnosticService.cs` | **0** | **0** ✅ |
| 22 | `Read(now) => Inspecter(now).Visibles` — unique implémentation des filtres | vert | **1 occurrence** dans `SessionMonitor.cs` ; `Read_rend_exactement_les_sessions_visibles_d_Inspecter` **vert** ✅ |
| 23 | `EcritureEtatSession.Appliquer(` câblé, `.tmp-` absent d'`App.xaml.cs` | vert | **1** et **0** ; `Le_mode_hook_ecrit_par_le_service_teste_et_signale_son_echec` **vert** ✅ |
| 24 | `grep -c "MotifMasquage" src/Chronos/Services/DiagnosticService.cs` | **3** (mesuré à l'entrée de phase ; l'énumération, elle, compte **2** valeurs : `Archivee`, `Traitee`) | **3** ✅ — énumération relue dans `LectureSessions.cs` : **2** valeurs, `Archivee` et `Traitee` ✅ |
| 24 | `L_arbitrage_ne_connait_ni_horloge_ni_magasin_ni_chemin` | vert | **vert** ✅ |
| 25 | `WaitingDeduced` jamais écrit dans un fichier d'état | vert | `WaitingDeduced_n_est_jamais_ecrit_dans_un_fichier_d_etat` **vert** ✅ |
| 25 | `ContratHooksDocumenteTests` (table du §1, date, trois trous) | **tous verts** | **9/9 verts** (7 préexistants + 2 ajoutés en 26-04) ✅ — les marqueurs `EVENEMENTS-CABLES:debut/fin`, la date `2026-09-12` et les trois marqueurs de trous (`Échap`, `prompt_input_exit`, `liste blanche`) sont **intacts** ; `wc -l docs/hooks-contract.md` = **372** (minimum 90) |
| — | `NormalisationUniqueTests`, `ServicesLayerPurityTests`, `CompositionRootTests`, `GardesDoctrineTests` | **tous verts** | **verts** ✅ — relance groupée du 2026-09-13 : `74 réussites / 0 échec` sur les filtres de gardes (y compris `GardesPerimetre`, `Inspection`, `Arbitrage`, `EcritureEtatSession`) |
| 26 | `git diff --stat d28392e..HEAD -- src/Chronos` en fin de vague 4 | **vide** | **vide** ✅ — la vague 4 n'écrit que du Markdown et un fichier de tests |

## Sécurité — à vérifier à chaque plan

- Aucun test n'écrit dans le vrai `%APPDATA%\Chronos\` : ni `sessions\`, ni `archived.json` (**84 octets**),
  ni `treated.json`. Tous les chemins passent par `Path.GetTempPath()`.
  **Mesuré aux quatre plans :** `grep -c "GetFolderPath\|APPDATA"` = **0** sur les fichiers de tests
  concernés, hors deux occurrences en XML-doc d'`ArchiveStorePurgeTests.cs` qui ne sont pas des chemins
  d'écriture. `archived.json` : **84 octets**, mtime du **12 juillet 16:38:57**, inchangé aux trois relevés.
  `treated.json` : **2 octets**, mtime du **12 septembre 21:21:28**, antérieur aux sessions de travail.
- Le dossier `sessions\` **bouge tout seul** (65 entrées au dernier relevé) : les hooks v3.0.2 de
  l'utilisateur sont vivants et écrivent pendant nos sessions. **Ce n'est pas une violation — ne pas le
  « réparer ».** **Mesuré :** jamais écrit, listé en lecture seule au plus, rien n'a été « réparé ».
- Le vrai `~/.claude/settings.json` n'est jamais écrit (lecture seule tolérée). **Aucune sonde.**
  **Mesuré aux quatre plans : aucune écriture, aucune sonde.**
- L'overlay (`Chronos-v3.0.2.exe`, pid 119412) n'est **ni lancé ni tué**. Aucun test n'affiche de fenêtre.
  **Mesuré :** `grep -c "\.Show()\|ShowDialog"` = **0** sur les tests XAML de 26-03 ; l'overlay n'a été ni
  lancé ni tué à aucun plan.
- `oauth.dat` (**518 octets**) n'est pas approché — **son mtime n'est pas vérifié**.
  **Mesuré :** non approché aux quatre plans, mtime **non vérifié**, conformément à la consigne.
- Aucune requête réseau réelle. Aucune dépendance NuGet nouvelle.
  **Mesuré :** aucune requête ; `Chronos.csproj` et `Chronos.Tests.csproj` **non touchés** de toute la phase.

## Écarts mesurés — ce qui n'est pas tombé juste, et ce qui n'a pas été ajusté

1. **La suite franchit une fois le seuil de 8 s (26-03 T3).** Attendu ≤ 8 s ; **mesuré 9 s** à la première
   passe suivant une reconstruction complète, puis **7 s / 7 s** aux deux vérifications consécutives. Les
   72 montages de `Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes` coûtent ~0,9 s. **Dit,
   non corrigé** : retirer des combinaisons pour tenir un seuil aurait été ajuster la mesure au lieu de la
   rapporter. À la clôture 26-04, la suite tient en **6 s**.
2. **`grep -c "1789181509266\|1789210240523"` attendu 2, mesuré 4** (26-01, déviation n° 1 de son SUMMARY).
   Les deux constantes figurent chacune **deux** fois : à leur déclaration et dans l'assertion d'anti-dérive
   qui fige l'écart de 478 minutes. L'attendu du plan comptait les déclarations seules. **Valeur mesurée
   inscrite, prédiction non réajustée.**
3. **Deux tâches annoncées « rouge puis vert » ont été livrées en UN commit** (26-01 T1, 26-03 T1), parce
   que les cas ajoutés lisent des membres qui n'existaient pas : un commit « RED » y aurait été un commit
   **qui ne compile pas**, ce que les règles dures interdisent sans exception. Ces cas étaient d'ailleurs
   annoncés « verts dès leur écriture » par cette carte. **Aucun rouge attendu n'a été perdu** : les six de
   26-01 T2 et les deux de 26-02 T1 ont tous été mesurés.
4. **`requirements mark-complete TRT-04` a été exécuté au plan 26-02**, alors que cette carte répartit
   TRT-04 sur **26-02 + 26-03**. Au moment du marquage, seul le volet **code** était livré ; le volet
   **libellé** (les huit menus) est arrivé au plan 26-03, commit `923d1d5`. **L'état final est donc
   cohérent et la case est légitime aujourd'hui — mais elle a précédé sa seconde preuve d'une vague.**
   Vérifié à la clôture : `- [x] **TRT-0x**` = **4**, `| TRT-0. | Phase 26 | Complete |` = **4**,
   `| … | Pending |` = **0** pour les lignes TRT. **Aucune retouche n'était nécessaire au plan 26-04 ;
   le fait est consigné plutôt que lissé.**
5. **Une dette de documentation reste ouverte, délibérément.** La XML-doc d'`ArchiveStore.PurgerPrefixe`
   décrit encore un `Add` « qui applique au passage la purge des entrées expirées » — phrase **devenue
   fausse** au retrait du TTL. Le plan 26-02 a gelé ce bloc par une règle dure (« `PurgerPrefixe` reste
   INTACT ») et l'a reportée ; le plan 26-04 ne demande pas de la corriger et ne la corrige pas. **Elle est
   reportée au-delà de la phase 26.** Elle n'est contredite par aucune garde et ne peut donc pas devenir
   rouge : c'est un commentaire périmé, pas un câblage faux.

## Ce qui reste ouvert après cette phase (rappel, hors périmètre)

- **Republication de l'exe** : rien du milestone v1.6 ne s'exécute chez l'utilisateur tant que l'exe n'est
  pas republié **et** `settings.json` réconcilié — sa configuration ne porte encore que les **5 hooks
  v3.0.2**, sans `PermissionRequest`, sans matcher sur `Notification`. Voir la chaîne de préalables en
  quatre étapes ci-dessus.
- **Protocole de vérification in vivo consolidé** (phases 17-20 et 21-25, dont l'effet de masse A11) :
  à présenter en fin de milestone, après la republication.
- **Dette de documentation** : la XML-doc d'`ArchiveStore.PurgerPrefixe` (écart n° 5).
- Exploitation des ~25 autres événements du catalogue : hors périmètre v1.6.
