---
phase: 26
slug: traite-veut-enfin-dire-quelque-chose
status: planned
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-12
validated: null
---

# Phase 26 — Validation Strategy

> Carte **posée à la planification**, à **remplir et mesurer** au fil des quatre plans, et à clore au plan
> 26-04. Toute case encore vide à la clôture est un **défaut de la phase**, pas une approximation.
>
> Dernière phase du milestone v1.6 : elle lui applique sa propre doctrine. **Ne rien écrire ici qui n'ait
> été mesuré.** Une case remplie « d'après le plan » plutôt que d'après une exécution est exactement la
> faute que ce milestone corrige — et « traité » est une affirmation forte sur ce que l'utilisateur a fait.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`net8.0-windows`) — `tests/Chronos.Tests` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Quick run (traitement)** | `… --filter "FullyQualifiedName~TreatedSessions\|FullyQualifiedName~GesteTraite\|FullyQualifiedName~ArchiveStorePurge\|FullyQualifiedName~SessionStylesBinding\|FullyQualifiedName~GardesPerimetre\|FullyQualifiedName~ContratHooksDocumente\|FullyQualifiedName~Inspection\|FullyQualifiedName~Arbitrage"` |
| **SHA d'entrée de phase** | `07ee784` — toute mesure de diff s'écrit `git diff --stat 07ee784..HEAD -- <chemins>` ; **sans révision de base, `git diff --stat` est VIDE après commit** |
| **Baseline d'entrée de phase** | **868 tests / 0 échec / ~4-5 s** (fin de phase 25) — *à remesurer au début du plan 26-01 et à corriger ici si l'écart est réel* |
| **Cible de fin de phase** | **0 échec, aucun test supprimé.** Le critère n'est PAS un plancher (un plancher se franchit sans rien ajouter) mais une **égalité** : `total = total mesuré du plan précédent + nombre de cas ajoutés`, tout écart — **même positif** — justifié **nominativement** dans le SUMMARY concerné. Attendus **indicatifs** : **876** (26-01), **879** (26-02), **886** (26-03), **888** (26-04). Détail indicatif : +1 (26-01 T1), **+7 (26-01 T2, dont 6 rouges)**, +0 (26-01 T3), +3 (26-02 T1), +0 (26-02 T2), **+5 (26-03 T1)**, +0 (26-03 T2), +2 (26-03 T3), +2 (26-04 T1), +0 (26-04 T2). |
| **Total mesuré après 26-01** | *(à mesurer)* |
| **Total mesuré après 26-02** | *(à mesurer)* |
| **Total mesuré après 26-03** | *(à mesurer)* |
| **Total mesuré après 26-04** | *(à mesurer)* |
| **Deux exécutions consécutives** | *(à mesurer à la clôture 26-04)* |
| **Renommages prévus** | **1, annoncé** : `Purger_n_est_pas_expirer_une_entree_vieille_reste_dans_le_fichier` → `Purger_n_est_pas_expirer_et_une_entree_vieille_reste_lisible` (26-02 T2), parce que son assertion finale s'inverse avec le retrait de la durée de vie. Les renommages **internes au code** (`IsWaiting` → `EstAttente`, `_lastActivity` → `_dernier`, `_waitingSince` → `_attenteDepuis`) ne sont pas des renommages de tests. Tout renommage supplémentaire se justifie **nominativement** au SUMMARY. Le point (b bis) du plan 26-01 remonte la borne de `TreatedStore` **sans** renommer `TreatedStore_set_load_remove_et_purge_TTL` : ce serait un second renommage, non annoncé ici. |
| **Suppressions de tests** | **0.** Aucun test n'est supprimé dans cette phase. |

## Le piège de test à retardement de ce projet

`TreatedStore.Load()` et `ArchiveStore` lisaient l'horloge **SYSTÈME** sans injection : **trois plans de la
phase 22** sont tombés dessus, avec des tests verts à l'écriture et rouges quelques heures plus tard sans
qu'une ligne de code ait bougé. Les plans 26-01 et 26-02 **introduisent l'horloge injectable** dans les deux
magasins et retirent toute dépendance à l'heure de la machine des tests concernés.

**Critère opposable, mesurable :**

| Fichier | Attendu | Mesuré |
|---|---|---|
| `grep -c "DateTimeOffset.UtcNow" src/Chronos/Services/TreatedStore.cs` | **0** (26-01) | *(à mesurer)* |
| `grep -c "DateTimeOffset.UtcNow" src/Chronos/Services/ArchiveStore.cs` | **0** (26-02) | *(à mesurer)* |
| `grep -c "DateTimeOffset.UtcNow" tests/Chronos.Tests/TreatedSessionsTests.cs` | **0** (26-01) | *(à mesurer)* |
| `grep -c "DateTimeOffset.UtcNow" tests/Chronos.Tests/ArchiveStorePurgeTests.cs` | **0** (26-02) | *(à mesurer)* |

## La borne du magasin des traitées ne porte plus sur l'instant de l'ÉPISODE

Décision du plan 26-01, point **(b bis)**. Depuis TRT-02, la valeur mémorisée dans `treated.json` est
l'instant que le **signal** porte, pas celui du guetteur. Une borne de six heures adossée à cet instant
rendait le magasin **aveugle à toute session attendant depuis plus de six heures** — soit précisément la
session `e465420e` dont ce milestone est parti, et deux heures pleines (entre six heures et le `DropAfter`
de huit) où « marquer traitée » était un **no-op silencieux**. La borne devient une RÉTENTION de fichier,
strictement supérieure à `DropAfter`. La réversibilité reste portée par NET-03, jamais par une horloge.

| Fichier | Attendu | Mesuré |
|---|---|---|
| `grep -c "FromHours(6)" src/Chronos/Services/TreatedStore.cs` | **0** (26-01, point (b bis)) | *(à mesurer)* |
| `grep -c "RetentionMax" src/Chronos/Services/TreatedStore.cs` | **≥ 3** (26-01, point (b bis)) | *(à mesurer)* |

## Le test qui compte le plus : rouge AVANT

Le critère 2 du ROADMAP porte sur une panne **réellement survenue chez l'utilisateur**, avec ses chiffres.
Un test qui naîtrait vert ne prouverait rien.

| Test | Plan | Doit être ROUGE en | Doit être VERT en | Mesuré |
|---|---|---|---|---|
| `Le_scenario_mesure_des_478_minutes_laisse_la_session_visible` | 26-01 | T2 | T3 | *(à mesurer)* |
| `Une_bascule_de_source_ne_marque_rien` | 26-01 | T2 | T3 | *(à mesurer)* |
| `Une_attente_deduite_ouvre_un_episode` | 26-01 | T2 | T3 | *(à mesurer)* |
| `Une_attente_qui_devient_deduite_n_est_pas_traitee` | 26-01 | T2 | T3 | *(à mesurer)* |
| `Un_etat_inconnu_n_affirme_pas_qu_on_a_repondu` | 26-01 | T2 | T3 | *(à mesurer)* |
| `Le_traite_survit_au_redemarrage_mais_pas_a_une_nouvelle_demande` | 26-01 | T2 | T3 | *(à mesurer)* |
| `Une_archive_ne_s_evapore_jamais_meme_apres_huit_jours` | 26-02 | T1 | T2 | *(à mesurer)* |
| `Le_magasin_d_archives_ne_connait_plus_aucune_duree_de_vie` | 26-02 | T1 | T2 | *(à mesurer)* |
| `Marquer_traitee_une_session_qui_attend_depuis_sept_heures_la_fait_disparaitre` | 26-03 | **rouge sans le point (b bis) de 26-01** — non mesurable rouge dans l'ordre des vagues, (b bis) la précède | 26-03 T1 | *(à mesurer)* |

La dernière ligne **ne compte pas** parmi les rouges mesurables : c'est la preuve opposable du point (b bis)
du plan 26-01, livré en vague 1, alors que le test naît en vague 3. Elle est ici parce que sa raison d'être
est la même que celle des huit autres — sans elle, les quatre autres cas de `GesteTraiteTests`, tous
horodatés au frais, seraient **vacueusement verts** au regard des critères 1 et 4 du ROADMAP. Si l'exécutant
veut la voir rouge, il peut, hors commit, remettre `Ttl = FromHours(6)` et le constater ; ce n'est pas exigé.

**Total des rouges attendus : 6 en 26-01 T2, 2 en 26-02 T1.** Chaque SUMMARY reporte la **liste exacte**
des échecs observés. *Un échec de moins qu'annoncé est un test vacueux ; un échec de plus est une
régression.* Les deux se disent, aucun ne s'absorbe.

**Verts dès leur écriture, et annoncés comme tels** (ne PAS les compter parmi les rouges) :
`Les_vainqueurs_sont_les_retenus_avec_leur_source` (26-01 T1),
`Un_episode_reellement_plus_recent_purge_toujours` (26-01 T2),
`L_horodatage_d_une_archive_vient_de_l_horloge_injectee` (26-02 T1),
les **cinq** cas de `GesteTraiteTests` (26-03 T1), les deux gardes de 26-03 T3, les deux gardes de 26-04 T1.

## Couverture des exigences

| Exigence | Plan | Preuve nommée attendue | Mesuré |
|---|---|---|---|
| **TRT-01** — transition observée sur la MÊME source | 26-01 | `Le_scenario_mesure_des_478_minutes_laisse_la_session_visible`, `Une_bascule_de_source_ne_marque_rien`, `Une_attente_deduite_ouvre_un_episode`, `Une_attente_qui_devient_deduite_n_est_pas_traitee`, `Un_etat_inconnu_n_affirme_pas_qu_on_a_repondu` | *(à mesurer)* |
| **TRT-02** — le traité survit au redémarrage | 26-01 | `Le_traite_survit_au_redemarrage_mais_pas_a_une_nouvelle_demande` (les DEUX temps) | *(à mesurer)* |
| **TRT-03** — geste explicite | 26-03 | `Marquer_traitee_fait_disparaitre_la_session_immediatement`, `Marquer_tout_traite_vide_le_widget_en_un_geste`, `Le_libelle_du_geste_de_masse_porte_le_nombre_de_sessions`, `Le_geste_est_reversible_et_le_libelle_ne_ment_pas`, `Marquer_traitee_une_session_qui_attend_depuis_sept_heures_la_fait_disparaitre`, `Les_huit_menus_offrent_le_geste_explicite_et_disent_la_verite`, `Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes` | *(à mesurer)* |
| **TRT-04** — contrat d'archivage unique | 26-02 + 26-03 | `Une_archive_ne_s_evapore_jamais_meme_apres_huit_jours`, `Le_magasin_d_archives_ne_connait_plus_aucune_duree_de_vie`, `Purger_n_est_pas_expirer_et_une_entree_vieille_reste_lisible`, et le libellé « Archiver définitivement (ne revient jamais) » ×8 tenu par `Les_huit_menus_…` | *(à mesurer)* |

## Les cinq critères du ROADMAP

| # | Critère | Preuve nommée attendue | Mesuré |
|---|---|---|---|
| 1 | Répondre fait disparaître, expirer non | `NET01_repondu_marque_traitee` (toujours vert) **et** `Une_bascule_de_source_ne_marque_rien` | *(à mesurer)* |
| 2 | Le masquage de 6 h ne se reproduit plus | `Le_scenario_mesure_des_478_minutes_laisse_la_session_visible`, **rouge en 26-01 T2** | *(à mesurer)* |
| 3 | Le traité survit au redémarrage | `Le_traite_survit_au_redemarrage_mais_pas_a_une_nouvelle_demande` | *(à mesurer)* |
| 4 | Un geste explicite existe, 8 styles × 9 thèmes | `Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes` + non-régression de `Les_8_styles_et_les_9_themes_se_chargent_se_mesurent_et_se_disposent` (compacité) | *(à mesurer)* |
| 5 | « Archiver » fait ce qu'il annonce | `Une_archive_ne_s_evapore_jamais_meme_apres_huit_jours` + les huit libellés | *(à mesurer)* |

## Acquis à ne pas casser — gardes qui doivent rester vertes

| Phase | Garde | Attendu | Mesuré |
|---|---|---|---|
| 20 | `grep -cF "?? new " src/Chronos/Services/DiagnosticService.cs` | **2** — *inchangé. Le dépôt en compte **6** dans `src/` ; le 2 de la phase 22 portait sur ce fichier SEUL. Les deux `clock ?? new SystemClock()` introduits par 26-01 et 26-02 sont hors de ce fichier et n'entrent pas dans ce compte.* | *(à mesurer)* |
| 21 | `DataTemplate x:Key=` dans `SessionStyles.xaml` | **8** | *(à mesurer)* |
| 21 | `Le_demarrage_purge_les_identifiants_fantomes_du_magasin_d_archives` | vert | *(à mesurer)* |
| 22 | `new SessionMonitor` dans `DiagnosticService.cs` | **0** | *(à mesurer)* |
| 22 | `Read(now) => Inspecter(now).Visibles` — unique implémentation des filtres | vert | *(à mesurer)* |
| 23 | `EcritureEtatSession.Appliquer(` câblé, `.tmp-` absent d'`App.xaml.cs` | vert | *(à mesurer)* |
| 24 | `grep -c "MotifMasquage" src/Chronos/Services/DiagnosticService.cs` | **3** (mesuré à l'entrée de phase ; l'énumération, elle, compte **2** valeurs : `Archivee`, `Traitee`) | *(à mesurer)* |
| 24 | `L_arbitrage_ne_connait_ni_horloge_ni_magasin_ni_chemin` | vert | *(à mesurer)* |
| 25 | `WaitingDeduced` jamais écrit dans un fichier d'état | vert | *(à mesurer)* |
| 25 | `ContratHooksDocumenteTests` (table du §1, date, trois trous) | **tous verts** | *(à mesurer)* |
| — | `NormalisationUniqueTests`, `ServicesLayerPurityTests`, `CompositionRootTests`, `GardesDoctrineTests` | **tous verts** | *(à mesurer)* |

## Sécurité — à vérifier à chaque plan

- Aucun test n'écrit dans le vrai `%APPDATA%\Chronos\` : ni `sessions\`, ni `archived.json` (**84 octets**),
  ni `treated.json`. Tous les chemins passent par `Path.GetTempPath()`.
- Le dossier `sessions\` **bouge tout seul** (65 entrées au dernier relevé) : les hooks v3.0.2 de
  l'utilisateur sont vivants et écrivent pendant nos sessions. **Ce n'est pas une violation — ne pas le
  « réparer ».**
- Le vrai `~/.claude/settings.json` n'est jamais écrit (lecture seule tolérée). **Aucune sonde.**
- L'overlay (`Chronos-v3.0.2.exe`, pid 119412) n'est **ni lancé ni tué**. Aucun test n'affiche de fenêtre.
- `oauth.dat` (**518 octets**) n'est pas approché — **son mtime n'est pas vérifié**.
- Aucune requête réseau réelle. Aucune dépendance NuGet nouvelle.

## Ce qui reste ouvert après cette phase (rappel, hors périmètre)

- **Republication de l'exe** : rien du milestone v1.6 ne s'exécute chez l'utilisateur tant que l'exe n'est
  pas republié **et** `settings.json` réconcilié — sa configuration ne porte encore que les **5 hooks
  v3.0.2**, sans `PermissionRequest`, sans matcher sur `Notification`.
- **Protocole de vérification in vivo consolidé** (phases 17-20 et 21-25, dont l'effet de masse A11) :
  à présenter en fin de milestone, après la republication.
- Exploitation des ~25 autres événements du catalogue : hors périmètre v1.6.
