---
phase: 21
slug: p-rim-tre-le-widget-ne-parle-que-de-claude-code
status: validated
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-12
validated: 2026-09-12
---

# Phase 21 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net8.0-windows) — `tests/Chronos.Tests` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Quick run (sessions)** | `… --filter "FullyQualifiedName~Sessions\|FullyQualifiedName~Treated\|FullyQualifiedName~Transcript\|FullyQualifiedName~ArchiveStore\|FullyQualifiedName~GardesPerimetre"` |
| **Baseline MESURÉE le 2026-09-12** | **752 tests / 0 échec / 5 s** (`Réussi! - échec : 0, réussite : 752`) |
| **Cible de fin de phase** | **719 tests / 0 échec** — un **recul assumé de 33** |

## Le critère de cette phase est renversé — et c'est le livrable

Le milestone v1.6 exige « les 752 tests restent verts ». **Cette phase fait exception, explicitement.**
529 lignes de tests couvrent exclusivement du code qui disparaît. Le critère opérationnel est donc :

> **0 échec + justification NOMINATIVE de chaque test supprimé** — pas « ≥ 752 ».

Précédent exact : **phase 16 du milestone v1.5** (démolition des plafonds), où un recul de couverture
justifié a déjà été accepté et documenté.

Une justification nominative veut dire : **chaque nom de méthode de test figure au bilan**, accompagné du
type ou de la branche supprimés qu'elle couvrait. Citer un nom de fichier ne suffit pas.

## Le risque n°1 est la compilation, pas la logique

`tests/Chronos.Tests` porte un `ProjectReference` vers `Chronos`. Une seule erreur de compilation, où que
ce soit, fait échouer **toute** l'invocation `dotnet test` — pas le test concerné. Dans une phase de
démolition, c'est le mode d'échec dominant.

**Parade structurelle, inscrite dans l'ordre des plans :** *débrancher* (plan 02) puis *supprimer*
(plan 03), jamais l'inverse. À la fin du plan 02, les huit fichiers à supprimer sont des orphelins
référencés par personne ; leur suppression au plan 03 ne peut donc rien casser. Toute tâche qui change une
signature adapte **tous** ses sites d'appel dans la **même** tâche. **Aucun « échec de build attendu »
n'est admis à aucun commit.**

## Pourquoi quatre vagues strictement sérielles

Les quatre plans auraient pu s'entrelacer davantage — les fichiers des plans 03 et 04 sont disjoints. Ils
ne le font pas, délibérément : deux plans exécutés en parallèle dans le même arbre de travail partagent
la même invocation `dotnet test`, et une démolition en cours dans l'un ferait échouer la vérification de
l'autre sans que le défaut soit dans l'autre. La sérialisation supprime entièrement cette classe de
faux négatifs. Le coût — quatre vagues au lieu de deux — est payé une fois.

De plus, trois plans se disputent `src/Chronos/App.xaml.cs` (02 et 04) et `tests/…/GardesPerimetreTests.cs`
(03 et 04) : la propriété exclusive des fichiers impose déjà un ordre entre eux.

## Sampling Rate

- Après chaque tâche : la commande rapide du domaine touché
- Après chaque plan : suite complète
- Avant vérification de phase : suite complète verte, **deux exécutions** — le chargeur BAML produit des
  échecs intermittents qui ne se voient pas à la première passe (cf. `XamlWpfCollection`)

## Corrections MESURÉES apportées au contexte de phase

Deux affirmations du `21-CONTEXT.md` ne résistent pas à la mesure. Les plans intègrent la version corrigée.

| Affirmation du CONTEXT | Mesure du 2026-09-12 | Conséquence |
|---|---|---|
| « les **8 styles** bindent le libellé de type » | `grep -c "KindLabel" SessionStyles.xaml` → **3**, toutes dans le SEUL template `TplPastilles` (l. 70-79) | La modification visuelle est confinée à **1** style. La **vérification** reste sur 8 × 9, pour prouver qu'on n'a rien décalé ailleurs. |
| `TokenRefreshServiceTests.cs` — « occurrence peut-être fortuite » | **Confirmé fortuite** : commentaire XML l. 80-81 citant `DesktopUiaPollService.StartAsync:44` comme précédent de motif | Simple reformulation, aucun test touché (plan 02, tâche 2). |

Deux constats supplémentaires, non prévus par le CONTEXT :

- **`ISessionSource` deviendrait orpheline** : sa seule implémentation était la source app-bureau. Plutôt
  que de la supprimer, `TranscriptSessionSource` la porte (plan 01, tâche 2) et `SessionMonitor` prend sa
  source de base par le contrat. Bénéfice mécanique immédiat : `MutableSource` de `TreatedSessionsTests`
  survit, au lieu d'exiger une réécriture des tests d'intégration de l'hystérésis.
- **`SessionsController.cs:47`** annonce à l'utilisateur que le widget couvre « (app bureau incluse) ».
  C'est une promesse qui devient fausse : elle est corrigée au plan 03, tâche 2.

## Substitutions imposées par les contraintes de sécurité

| Vérification naturelle | Interdite parce que | Substitut retenu |
|---|---|---|
| Lancer l'overlay et regarder le widget | Une instance `Chronos-v3.0.2.exe` (pid 119412) est **en cours d'utilisation** | `SessionStylesBindingTests` : le vrai BAML chargé, les vrais pinceaux de thème appliqués, `Measure`/`Arrange`/`UpdateLayout` sur les **72** combinaisons |
| Lancer l'exe pour voir `archived.json` se vider | Idem | Test unitaire sur le **contenu réel mesuré** + garde de source prouvant que l'appel est câblé dans `OnStartup` |
| Balayer `%APPDATA%\Chronos\sessions\` | 54 `.json` + 12 `.tmp` = données de l'utilisateur ET matière d'enquête ; le balayage est la **phase 23** | Aucune. Invariant vérifié en fin de phase : le dossier compte toujours **66** entrées |
| Tester un rafraîchissement de jeton | `oauth.dat` porte le **vrai** refresh token (518 o, mtime 1783863147) | Aucun test de cette phase ne touche à l'authentification |

## Per-Task Verification Map

Carte posée par le planner, à RELEVER à l'exécution. Toutes les commandes se préfixent de
`dotnet test Chronos.sln -c Debug --nologo -v q`.

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command (`--filter …`) | Δ tests | Total | Status |
|---------|------|------|-------------|-----------|----------------------------------|---------|-------|--------|
| 21-01 T1 — limite après le filtre sous-agents | 01 | 1 | SRC-03 | unité (TDD) | `~TranscriptSousAgentsTests` · `~SessionsTests` | +4 | 756 | ✅ 756 / 0 |
| 21-01 T2 — `TranscriptSessionSource : ISessionSource` | 01 | 1 | SRC-03 (infra) | compilation + suite | *(suite complète)* | 0 | 756 | ✅ 756 / 0 |
| 21-02 T1 — la couture : moniteur, détecteur, DI, 4 fichiers de tests | 02 | 2 | SRC-01 | unité + garde DI | `~SessionsTests` · `~TreatedSessionsTests` · `~CompositionRootTests` | −9 | 747 | ✅ 747 / 0 |
| 21-02 T2 — diagnostic et inventaire de machine | 02 | 2 | SRC-01 | unité + pureté | `~DiagnosticServiceTests` · `~TokenRefreshServiceTests` · `~ServicesLayerPurityTests` | 0 | 747 | ✅ 747 / 0 |
| 21-03 T1 — suppression des 10 fichiers + garde par réflexion | 03 | 3 | SRC-01 | garde d'assembly | `~GardesPerimetreTests` *(suite complète)* | −40 / +2 | 709 | ✅ 709 / 0 |
| 21-03 T2 — `SessionSnapshot` à 5 champs, XAML, message d'activation | 03 | 3 | SRC-01 | garde de source | `~GardesPerimetreTests` | +1 | 710 | ✅ 710 / 0 |
| 21-03 T3 — 8 styles × 9 thèmes | 03 | 3 | SRC-01 (critère 4) | BAML `Measure`/`Arrange` | `~SessionStylesBindingTests` | +2 | 712 | ✅ 712 / 0 |
| 21-04 T1 — `ArchiveStore.PurgerPrefixe` | 04 | 4 | SRC-02 | unité (TDD) | `~ArchiveStorePurgeTests` | +6 | 718 | ✅ 718 / 0 |
| 21-04 T2 — câblage au démarrage + garde | 04 | 4 | SRC-02 | garde de source | `~GardesPerimetreTests` · `~CompositionRootTests` | +1 | 719 | ✅ 719 / 0 |
| 21-04 T3 — bilan nominatif | 04 | 4 | SRC-01/02/03 | suite complète ×2 | *(suite complète)* | 0 | 719 | ✅ 719 / 0 ×2 |

## Couverture des exigences

| Exigence | Plan(s) | Ce qui la prouve |
|---|---|---|
| **SRC-01** — retrait de la source app-bureau | 02, 03 | `GardesPerimetreTests.Aucun_type_de_la_source_app_bureau_ne_subsiste_dans_l_assembly` (réflexion), `…Aucun_style_de_session_ne_binde_plus_un_libelle_de_type` (source XAML), `CompositionRootTests.Le_graphe_DI_resout_la_chaine_de_sessions` |
| **SRC-02** — fantômes archivés purgés | 04 | `ArchiveStorePurgeTests` (6 tests, dont le contenu réel mesuré), `GardesPerimetreTests.Le_demarrage_purge_les_identifiants_fantomes_du_magasin_d_archives` |
| **SRC-03** — limite après le filtre sous-agents | 01 | `TranscriptSousAgentsTests` (4 tests, dont le scénario mesuré des 12 sous-agents chauds) |

## Gardes à ne pas casser

Nommément vérifiées vertes à chaque plan :

- `ServicesLayerPurityTests` — aucun type WPF dans `Chronos.Services` / `Chronos.Models`
- `CompositionRootTests` — le graphe DI se résout dans le bon ordre
- `NormalisationUniqueTests`
- `GardesDoctrineTests`
- `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte`

## Bilan nominatif du recul de couverture

**REMPLI au plan 04, tâche 3, le 2026-09-12.** Une ligne par test supprimé, chacune portant un **nom de
méthode** et le code disparu qu'elle couvrait. Les noms des deux fichiers entièrement supprimés ont été
relevés dans l'historique, au commit qui précède leur suppression :
`git show ff783d4^:tests/Chronos.Tests/DesktopUiaSessionSourceTests.cs`.

> **Cas de test ≠ méthode — précision mesurée à l'exécution.** `DesktopUiaSessionSourceTests` comptait
> **28 méthodes** (25 `[Fact]` + 3 `[Theory]`) mais **36 cas de test** : les trois théories portaient
> 4 + 4 + 3 = **11** `[InlineData]`, et c'est le **cas** que xUnit compte (25 + 11 = 36). La colonne `#`
> numérote des **cas**, et le tableau en porte **49 lignes** : les trois théories y sont éclatées cas
> par cas, chacun nommant sa méthode ET la donnée `[InlineData]` qui le distingue. Au total pour les deux fichiers
> supprimés : **32 méthodes = 40 cas**, plus **9 méthodes = 9 cas** retirées de fichiers conservés.

### Supprimés — attendu : 49 — **mesuré : 49**

| # | Test (nom de méthode) | Forme | Fichier d'origine | Code supprimé qu'il couvrait |
|---|---|---|---|---|
| 1 | `Matches_reconnait_Responding_fr_en_insensible_casse_espaces` | Theory · `"  claude RÉPOND.  "` — casse + espaces (fr) | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.Matches` — reconnaissance fr/en de « Claude répond » |
| 2 | `Matches_reconnait_Responding_fr_en_insensible_casse_espaces` | Theory · `"Claude répond."` — exact (fr) | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.Matches` — reconnaissance fr/en de « Claude répond » |
| 3 | `Matches_reconnait_Responding_fr_en_insensible_casse_espaces` | Theory · `"Claude is responding"` — variante en | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.Matches` — reconnaissance fr/en de « Claude répond » |
| 4 | `Matches_reconnait_Responding_fr_en_insensible_casse_espaces` | Theory · `"  CLAUDE IS RESPONDING "` — casse + espaces (en) | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.Matches` — reconnaissance fr/en de « Claude répond » |
| 5 | `Matches_est_faux_pour_null_vide_ou_non_correspondant` | Theory · `null` — absence | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.Matches` — rejet du vide et du hors-sujet |
| 6 | `Matches_est_faux_pour_null_vide_ou_non_correspondant` | Theory · `""` — chaîne vide | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.Matches` — rejet du vide et du hors-sujet |
| 7 | `Matches_est_faux_pour_null_vide_ou_non_correspondant` | Theory · `"   "` — blancs seuls | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.Matches` — rejet du vide et du hors-sujet |
| 8 | `Matches_est_faux_pour_null_vide_ou_non_correspondant` | Theory · `"autre chose"` — hors-sujet | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.Matches` — rejet du vide et du hors-sujet |
| 9 | `StartsWithAny_extrait_le_nom_apres_prefixe_fr` | Fact | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.StartsWithAny` — extraction du nom après préfixe fr |
| 10 | `StartsWithAny_extrait_le_nom_apres_prefixe_en` | Fact | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.StartsWithAny` — extraction du nom après préfixe en |
| 11 | `StartsWithAny_est_faux_et_remainder_vide_sans_prefixe` | Theory · `null` — absence | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.StartsWithAny` — absence de préfixe |
| 12 | `StartsWithAny_est_faux_et_remainder_vide_sans_prefixe` | Theory · `""` — chaîne vide | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.StartsWithAny` — absence de préfixe |
| 13 | `StartsWithAny_est_faux_et_remainder_vide_sans_prefixe` | Theory · `"Projet sans préfixe"` — aucun préfixe connu | `DesktopUiaSessionSourceTests.cs` | `UiaLabels.StartsWithAny` — absence de préfixe |
| 14 | `MapTree_racine_null_donne_liste_vide_sans_exception` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` — tolérance à la racine absente |
| 15 | `MapTree_fenetre_sans_ancre_RootWebArea_donne_liste_vide` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` — ancre `RootWebArea` manquante |
| 16 | `MapTree_texte_Claude_repond_donne_foreground_Working` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` — signal Working par le texte |
| 17 | `MapTree_bouton_Arreter_sans_texte_donne_Working` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` — signal Working par le bouton « Arrêter » |
| 18 | `MapTree_toggle_persistant_Ignorer_les_permissions_ne_donne_PAS_WaitingAttention` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` — désambiguïsation du toggle de permissions |
| 19 | `MapTree_foreground_Cowork_non_emis_car_etat_VM_non_observable` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` — Cowork-VM déclaré non observable |
| 20 | `MapTree_type_Code_prime_sur_ChatMode_co_present` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` + `SessionKind` — arbitrage de type |
| 21 | `MapTree_mode_chat_au_repos_donne_WaitingTurn_Chat` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` + `SessionKind.Chat` |
| 22 | `MapTree_ancre_sans_signal_donne_Unknown` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` — absence de signal |
| 23 | `MapTree_panneaux_Code_donnent_Kind_Code` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` + `SessionKind.Code` |
| 24 | `MapTree_sidebar_enumere_les_sessions_en_cours_et_ignore_les_autres` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` — énumération de la sidebar |
| 25 | `MapTree_foreground_nomme_par_l_entete_de_session` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` — nommage par l'en-tête de session |
| 26 | `MapTree_foreground_sans_repo_retombe_sur_sans_titre` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` — repli « sans titre » |
| 27 | `MapTree_sessions_sidebar_typees_Cowork_si_app_bridgee` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` + `SessionKind.Cowork` |
| 28 | `MapTree_ignore_les_libelles_dans_le_contenu_des_messages` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` — anti-faux-positif de contenu |
| 29 | `Poll_accumule_les_sessions_en_gardant_leur_dernier_etat_connu` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.Poll` — cache accumulatif |
| 30 | `MapTree_ne_duplique_pas_un_foreground_deja_liste_en_sidebar` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.MapTree` — anti-doublon premier plan / sidebar |
| 31 | `Poll_ne_duplique_pas_une_session_dont_le_type_change_selon_la_vue` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.Poll` — anti-doublon au changement de type |
| 32 | `Poll_racine_null_donne_Health_WindowMissing_et_cache_vide` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopHealth.WindowMissing` |
| 33 | `Poll_fenetre_sans_ancre_donne_Health_AnchorMissing_et_cache_vide` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopHealth.AnchorMissing` |
| 34 | `Poll_avec_ancre_donne_Health_Ok` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopHealth.Ok` |
| 35 | `Read_avant_Poll_est_vide_et_ne_touche_pas_le_provider` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.Read` — lecture avant tout poll |
| 36 | `Read_rend_le_cache_sans_rappeler_le_provider` | Fact | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource.Read` — lecture sans rappel de l'arbre `UiaNode` |
| 37 | `PollOnce_remplit_le_cache_a_partir_du_vide` | Fact | `DesktopUiaPollServiceTests.cs` | `DesktopUiaPollService.PollOnce` |
| 38 | `PollOnce_provider_null_ne_leve_pas_et_laisse_le_cache_vide` | Fact | `DesktopUiaPollServiceTests.cs` | `DesktopUiaPollService.PollOnce` — tolérance au fournisseur absent |
| 39 | `PollOnce_utilise_l_heure_de_l_horloge_injectee` | Fact | `DesktopUiaPollServiceTests.cs` | `DesktopUiaPollService` — horloge injectée |
| 40 | `StartAsync_puis_StopAsync_sont_surs_et_idempotents` | Fact | `DesktopUiaPollServiceTests.cs` | `DesktopUiaPollService` — cycle de vie `IHostedService` |
| 41 | `Monitor_source_bureau_ignoree_si_sessions_locales_presentes` | Fact | `SessionsTests.cs` | étape de repli app-bureau de `SessionMonitor.Read` (anti-doublon) |
| 42 | `Monitor_source_bureau_utilisee_en_repli_si_aucune_session_locale` | Fact | `SessionsTests.cs` | étape de repli app-bureau de `SessionMonitor.Read` |
| 43 | `Monitor_archive_une_session_bureau` | Fact | `SessionsTests.cs` | archivage d'une clé synthétique `desktop:` — l'archivage générique reste couvert par `Archive_retire_la_session_de_l_affichage` |
| 44 | `Widget_affiche_le_type_de_session_bureau` | Fact | `SessionsTests.cs` | `SessionSnapshot.Kind` → `KindLabel` du widget (producteur disparu) |
| 45 | `Widget_mappe_chaque_type_bureau_vers_son_libelle` | Fact | `SessionsTests.cs` | table `SessionKind` → libellé du widget |
| 46 | `NET02_focus_continu_2_5s_acquitte` | Fact | `TreatedSessionsTests.cs` | branche d'acquittement par focus de `SessionTreatmentTracker` |
| 47 | `NET02_interruption_focus_remet_a_zero` | Fact | `TreatedSessionsTests.cs` | idem (debounce anti-survol) |
| 48 | `NET02_ne_declenche_pas_sans_focus` | Fact | `TreatedSessionsTests.cs` | idem |
| 49 | `NET02_le_monitor_masque_apres_focus_2_5s` | Fact | `TreatedSessionsTests.cs` | idem, bout-en-bout via `SessionMonitor` |

**Aucun de ces 49 tests ne couvre une ligne survivante.** Un test qui couvrirait à la fois du code
supprimé et du code survivant ne serait pas supprimé mais **réduit** — c'est le cas de
`Monitor_sans_source_bureau_ne_regresse_pas`, conservé et renommé
`Monitor_lit_une_session_du_transcript_sans_hook`. Il apparaît des DEUX côtés du diff de `9d0c8ca` :
c'est la seule raison pour laquelle ce commit montre 10 méthodes retirées pour **9** suppressions nettes.

### Ajoutés — attendu : 16 — **mesuré : 16**

| # | Test | Fichier | Exigence |
|---|---|---|---|
| 1 | `Douze_sous_agents_chauds_ne_font_plus_disparaitre_la_vraie_session` | `TranscriptSousAgentsTests.cs` | SRC-03 |
| 2 | `La_limite_de_douze_porte_sur_les_sessions_retenues` | idem | SRC-03 |
| 3 | `Un_fichier_agent_hors_dossier_subagents_est_ecarte_par_son_nom` | idem | SRC-03 |
| 4 | `Le_champ_isSidechain_garde_l_autorite_sur_un_fichier_mal_range` | idem | SRC-03 |
| 5 | `Aucun_type_de_la_source_app_bureau_ne_subsiste_dans_l_assembly` | `GardesPerimetreTests.cs` | SRC-01 (non-retour) |
| 6 | `La_garde_voit_bien_l_assembly_Chronos` | idem | SRC-01 (anti-muette) |
| 7 | `Aucun_style_de_session_ne_binde_plus_un_libelle_de_type` | idem | SRC-01 (critère 4) |
| 8 | `Le_demarrage_purge_les_identifiants_fantomes_du_magasin_d_archives` | idem | SRC-02 (câblage) |
| 9 | `Les_8_styles_et_les_9_themes_se_chargent_se_mesurent_et_se_disposent` | `SessionStylesBindingTests.cs` | SRC-01 (critère 4) |
| 10 | `Le_style_Pastilles_n_a_plus_qu_un_seul_separateur_visible_par_session` | idem | SRC-01 (critère 4) |
| 11 | `Les_deux_fantomes_mesures_sont_retires_du_fichier` | `ArchiveStorePurgeTests.cs` | SRC-02 |
| 12 | `Une_session_Claude_Code_archivee_survit_intacte` | idem | SRC-02 |
| 13 | `Purger_n_est_pas_expirer_une_entree_vieille_reste_dans_le_fichier` | idem | SRC-02 |
| 14 | `Sans_fantome_le_fichier_de_l_utilisateur_n_est_pas_reecrit` | idem | SRC-02 |
| 15 | `Fichier_absent_ou_illisible_rend_zero_sans_exception` | idem | SRC-02 |
| 16 | `Un_prefixe_vide_ne_purge_rien` | idem | SRC-02 |

**Arithmétique : 752 − 49 + 16 = 719. MESURÉ : 719, deux exécutions consécutives, 0 échec.**
Aucun écart à expliquer. Le chemin plan par plan a été mesuré à chaque étape, sans jamais dériver :

| Étape | Δ | Total prédit | Total mesuré |
|---|---|---|---|
| Baseline avant la phase 21 | — | 752 | **752** |
| Plan 21-01 (SRC-03) | +4 | 756 | **756** |
| Plan 21-02 (débranchement) | −9 | 747 | **747** |
| Plan 21-03 T1 (suppression des 10 fichiers + 2 gardes) | −40 / +2 | 709 | **709** |
| Plan 21-03 T2 (garde de source XAML) | +1 | 710 | **710** |
| Plan 21-03 T3 (montage BAML 8 × 9) | +2 | 712 | **712** |
| Plan 21-04 T1 (`ArchiveStorePurgeTests`) | +6 | 718 | **718** |
| Plan 21-04 T2 (garde de câblage) | +1 | 719 | **719** |
| **Fin de phase, passe 1** | — | **719** | **719 / 0 échec / 6 s** |
| **Fin de phase, passe 2** | — | **719** | **719 / 0 échec / 6 s** |

Les 5 gardes nommées de la phase sont vertes, vérifiées par filtre dédié (18 cas) :
`ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`, `GardesDoctrineTests`,
`La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte`.

## Invariants de sécurité — RELEVÉS en fin de phase (2026-09-12)

| Invariant | Attendu | **Mesuré en fin de phase** |
|---|---|---|
| Aucune donnée de session touchée | **66** (54 `.json` + 12 `.tmp`) | **66** ✅ |
| Jeton intact | **518** octets | **518** ✅ |
| `archived.json` non modifié par les tests | **84** octets | **84** octets, mtime **1783867137** (12 juillet), contenu **identique** ✅ |
| Overlay ni lancé ni tué | pid **119412** | pid **119412** toujours vivant (`tasklist`), jamais lancé ni tué ✅ |
| Aucune dépendance NuGet nouvelle | aucun `PackageReference` ajouté | `git diff -- '*.csproj'` **vide** sur les trois commits ✅ |
| Aucun écrit hors périmètre | — | `git status --porcelain` **vide** ; tous les commits sous `src/`, `tests/`, `.planning/` ✅ |

**Note sur le mtime d'`oauth.dat`** : l'invariant annoncé à la rédaction (1783863147) a été démontré faux
au plan 21-01 — c'était le mtime d'`archived.json` (1783867137), par transposition. L'overlay en cours fait
tourner le refresh token toutes les 60 s : un mtime figé ne peut PAS être un invariant. **Seule la taille
l'est**, et elle est vérifiée. Le mtime n'a pas été relu (le lire ne prouverait rien et le fichier ne doit
pas être approché).

**Aucun test de cette phase n'écrit dans le vrai `%APPDATA%\Chronos\`.** `ArchiveStorePurgeTests` construit
chacun de ses fichiers sous `Path.GetTempPath()` et l'**assertion** `Assert.StartsWith(Path.GetTempPath(), f)`
le verrouille : un chemin qui sortirait du dossier temporaire ferait échouer le test avant toute écriture.

## À VÉRIFIER PAR L'UTILISATEUR

**Section ouverte au plan 04, tâche 3.** Elle remplace les checkpoints que la contrainte de phase interdit
(l'overlay `Chronos-v3.0.2.exe`, pid 119412, est en cours d'utilisation — le relancer ou le tuer n'était pas
au pouvoir de l'agent). Ces points ne sont observables qu'après un redémarrage **volontaire**.

### 1. SRC-02 in vivo — la preuve que la purge s'exécute vraiment

Au prochain lancement de la version **du dépôt**, `%APPDATA%\Chronos\archived.json` doit passer de
**84 octets** à `{}` (**2 octets**), sans qu'aucun fichier n'ait été touché à la main.

```sh
# AVANT (état actuel, vérifié le 2026-09-12) :
#   84 octets — {"desktop:foreground:unknown":1783867136065,"desktop:foreground:code":1783867137990}
stat -c '%s' "$APPDATA/Chronos/archived.json"   # attendu APRÈS lancement : 2
cat "$APPDATA/Chronos/archived.json"            # attendu APRÈS lancement : {}
```

**C'est le critère de succès de SRC-02, et il n'est pas substituable.** Le test unitaire prouve que la
méthode retire bien les deux entrées mesurées ; la garde de source prouve que l'appel est câblé dans
`OnStartup`. Ni l'un ni l'autre ne prouve que le fichier **de l'utilisateur** a changé — seul son
lancement le prouve.

**Attention, à faire en connaissance de cause** : lancer l'overlay déclenche aussi la réconciliation de la
phase 15, qui purge les groupes de hooks périmés de `~/.claude/settings.json` (avec sauvegarde horodatée).
C'est voulu, et c'est l'avertissement déjà consigné depuis le plan 19-05.

### 2. Critère 4, à l'œil — la galerie des 8 styles

```sh
Chronos.exe --sessions
```

Parcourir les **8 styles**. Une ligne du style **Pastilles** doit lire `état · détail` — **un seul** point
médian, aucun séparateur orphelin en fin de ligne, sur les **9 thèmes**. Le libellé de type (Chat / Code /
Cowork) a disparu avec son producteur : aucune case vide, aucune rangée décalée.

Le test `SessionStylesBindingTests` monte le vrai BAML sur les 72 combinaisons et mesure ; il attrape un
binding cassé ou une disposition à zéro, **pas** un déséquilibre visuel. C'est l'œil qui tranche ce dernier.

### 3. Critère 1, après quelques heures d'usage

Avec l'app bureau Claude ouverte au premier plan, laisser tourner. Attendu :

- **plus aucune ligne préfixée `desktop:`** dans le widget de sessions ;
- **plus rien à archiver à la main** — le geste de contournement n'a plus d'objet.

C'est la vérification qui ferme la phase du point de vue de l'usage : le widget ne parle plus que de
Claude Code.

### Points différés hérités des plans 01 à 03 — repris ici pour mémoire

| # | Point | Origine | Pourquoi il n'a pas pu être fermé |
|---|---|---|---|
| 4 | Le `catch` de `TranscriptSessionSource.Read` n'est **pas couvert** par un test | 21-01 | Injecter une panne d'énumération disque exigerait une abstraction de système de fichiers absente du dépôt. Le défaut réel (l'énumération LINQ paresseuse levait HORS du `try`) est **corrigé** ; c'est sa couverture qui manque. |
| 5 | `NET-02` (acquittement par focus) n'a **plus aucun mécanisme vivant** | 21-02 | Documenté par une épitaphe qui donne la raison mécanique de sa mort, plutôt que supprimé en silence. Si l'acquittement par focus redevient souhaité, c'est une exigence à re-poser, pas un code à ressusciter. |
| 6 | L'exe déployé est antérieur à tout ce milestone | v1.5, toujours vrai | Rien des phases 15 à 21 n'est visible tant qu'une release n'est pas republiée. La version du `.csproj` doit être montée à la prochaine release (cf. mémoire « versionnage de l'exe » : la version va dans l'exe **et** dans le nom du fichier publié). |

## Hors périmètre — à ne pas anticiper

`DiagnosticService` conserve délibérément son `new SessionMonitor()` nu et son `files.Take(8)` : c'est
**OBS-01/OBS-02, phase 22**. `ArchiveStore.Add` conserve son écriture par fichier temporaire (**CYC-02,
phase 23**) et son TTL de 6 h face à un contrat annoncé « permanent » (**TRT-04, phase 26**). Le balayage
du magasin de sessions est la **phase 23**. L'arbitrage par fraîcheur est la **phase 24**. Le contrat
d'événements est la **phase 25**.

Cette phase **démolit et nettoie** ; elle ne change **aucune** sémantique d'état.
