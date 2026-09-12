---
phase: 21
slug: p-rim-tre-le-widget-ne-parle-que-de-claude-code
status: planned
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-12
validated: —
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
| 21-01 T1 — limite après le filtre sous-agents | 01 | 1 | SRC-03 | unité (TDD) | `~TranscriptSousAgentsTests` · `~SessionsTests` | +4 | 756 | ⬜ |
| 21-01 T2 — `TranscriptSessionSource : ISessionSource` | 01 | 1 | SRC-03 (infra) | compilation + suite | *(suite complète)* | 0 | 756 | ⬜ |
| 21-02 T1 — la couture : moniteur, détecteur, DI, 4 fichiers de tests | 02 | 2 | SRC-01 | unité + garde DI | `~SessionsTests` · `~TreatedSessionsTests` · `~CompositionRootTests` | −9 | 747 | ⬜ |
| 21-02 T2 — diagnostic et inventaire de machine | 02 | 2 | SRC-01 | unité + pureté | `~DiagnosticServiceTests` · `~TokenRefreshServiceTests` · `~ServicesLayerPurityTests` | 0 | 747 | ⬜ |
| 21-03 T1 — suppression des 10 fichiers + garde par réflexion | 03 | 3 | SRC-01 | garde d'assembly | `~GardesPerimetreTests` *(suite complète)* | −40 / +2 | 709 | ⬜ |
| 21-03 T2 — `SessionSnapshot` à 5 champs, XAML, message d'activation | 03 | 3 | SRC-01 | garde de source | `~GardesPerimetreTests` | +1 | 710 | ⬜ |
| 21-03 T3 — 8 styles × 9 thèmes | 03 | 3 | SRC-01 (critère 4) | BAML `Measure`/`Arrange` | `~SessionStylesBindingTests` | +2 | 712 | ⬜ |
| 21-04 T1 — `ArchiveStore.PurgerPrefixe` | 04 | 4 | SRC-02 | unité (TDD) | `~ArchiveStorePurgeTests` | +6 | 718 | ⬜ |
| 21-04 T2 — câblage au démarrage + garde | 04 | 4 | SRC-02 | garde de source | `~GardesPerimetreTests` · `~CompositionRootTests` | +1 | 719 | ⬜ |
| 21-04 T3 — bilan nominatif | 04 | 4 | SRC-01/02/03 | suite complète ×2 | *(suite complète)* | 0 | 719 | ⬜ |

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

> **À REMPLIR au plan 04, tâche 3.** Une ligne par test supprimé, avec le nom de la méthode et le code
> disparu qu'elle couvrait. Les noms des 40 méthodes des deux fichiers supprimés s'obtiennent par
> `git show <sha-avant>:tests/Chronos.Tests/DesktopUiaSessionSourceTests.cs | grep "public .*()"`.

### Supprimés — attendu : 49

| # | Test (nom de méthode) | Fichier d'origine | Code supprimé qu'il couvrait |
|---|---|---|---|
| 1-36 | *(à énumérer)* | `DesktopUiaSessionSourceTests.cs` | `DesktopUiaSessionSource`, `UiaLabels`, `UiaNode`, `DesktopHealth` |
| 37-40 | *(à énumérer)* | `DesktopUiaPollServiceTests.cs` | `DesktopUiaPollService` |
| 41 | `Monitor_source_bureau_ignoree_si_sessions_locales_presentes` | `SessionsTests.cs` | étape de repli app-bureau de `SessionMonitor.Read` (anti-doublon) |
| 42 | `Monitor_source_bureau_utilisee_en_repli_si_aucune_session_locale` | `SessionsTests.cs` | étape de repli app-bureau de `SessionMonitor.Read` |
| 43 | `Monitor_archive_une_session_bureau` | `SessionsTests.cs` | archivage d'une clé synthétique `desktop:` — l'archivage générique reste couvert par `Archive_retire_la_session_de_l_affichage` |
| 44 | `Widget_affiche_le_type_de_session_bureau` | `SessionsTests.cs` | libellé de type du widget (producteur disparu) |
| 45 | `Widget_mappe_chaque_type_bureau_vers_son_libelle` | `SessionsTests.cs` | libellé de type du widget |
| 46 | `NET02_focus_continu_2_5s_acquitte` | `TreatedSessionsTests.cs` | branche d'acquittement par focus de `SessionTreatmentTracker` |
| 47 | `NET02_interruption_focus_remet_a_zero` | `TreatedSessionsTests.cs` | idem (debounce anti-survol) |
| 48 | `NET02_ne_declenche_pas_sans_focus` | `TreatedSessionsTests.cs` | idem |
| 49 | `NET02_le_monitor_masque_apres_focus_2_5s` | `TreatedSessionsTests.cs` | idem, bout-en-bout via `SessionMonitor` |

**Aucun de ces 49 tests ne couvre une ligne survivante.** Un test qui couvrirait à la fois du code supprimé
et du code survivant ne serait pas supprimé mais réduit — c'est le cas de
`Monitor_sans_source_bureau_ne_regresse_pas`, conservé et renommé
`Monitor_lit_une_session_du_transcript_sans_hook`.

### Ajoutés — attendu : 16

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

**Arithmétique : 752 − 49 + 16 = 719.** Un écart ne se corrige pas en ajustant le chiffre : il s'explique
ligne à ligne.

## Invariants de sécurité — à vérifier en fin de phase

| Invariant | Commande | Attendu |
|---|---|---|
| Aucune donnée de session touchée | `ls "$APPDATA/Chronos/sessions" \| wc -l` | **66** (54 `.json` + 12 `.tmp`) |
| Jeton intact | `ls -l "$APPDATA/Chronos/oauth.dat"` | **518** octets, mtime **1783863147** |
| `archived.json` non modifié par les tests | `ls -l "$APPDATA/Chronos/archived.json"` | **84** octets — la purge ne s'exécute qu'au prochain lancement **volontaire** de l'overlay |
| Overlay ni lancé ni tué | — | pid **119412** toujours celui du départ |
| Aucune dépendance NuGet nouvelle | `git diff --stat -- '*.csproj'` | aucun ajout de `PackageReference` |

## À VÉRIFIER PAR L'UTILISATEUR

> Section à ouvrir au plan 04, tâche 3. Ces trois points ne sont observables qu'après un redémarrage
> **volontaire** de l'overlay, que cette phase s'interdit de provoquer.

1. **SRC-02 in vivo** — au prochain lancement, `%APPDATA%\Chronos\archived.json` doit passer de **84
   octets** à `{}` (2 octets), sans qu'aucun fichier n'ait été touché à la main.
2. **Critère 4, à l'œil** — galerie `--sessions`, parcourir les 8 styles : une ligne Pastilles doit lire
   `état · détail`, sans point médian orphelin en fin de ligne, sur les 9 thèmes.
3. **Critère 1, après quelques heures** — avec l'app bureau Claude ouverte au premier plan, plus aucune
   ligne préfixée `desktop:` ne doit apparaître dans le widget, et il ne doit plus rien y avoir à archiver
   à la main.

## Hors périmètre — à ne pas anticiper

`DiagnosticService` conserve délibérément son `new SessionMonitor()` nu et son `files.Take(8)` : c'est
**OBS-01/OBS-02, phase 22**. `ArchiveStore.Add` conserve son écriture par fichier temporaire (**CYC-02,
phase 23**) et son TTL de 6 h face à un contrat annoncé « permanent » (**TRT-04, phase 26**). Le balayage
du magasin de sessions est la **phase 23**. L'arbitrage par fraîcheur est la **phase 24**. Le contrat
d'événements est la **phase 25**.

Cette phase **démolit et nettoie** ; elle ne change **aucune** sémantique d'état.
