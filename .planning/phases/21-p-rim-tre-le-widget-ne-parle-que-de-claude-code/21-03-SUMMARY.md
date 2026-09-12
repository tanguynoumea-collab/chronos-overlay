---
phase: 21-p-rim-tre-le-widget-ne-parle-que-de-claude-code
plan: 03
subsystem: services
tags: [sessions, demolition, suppression, gardes, xaml, baml, themes, src-01]

# Dependency graph
requires:
  - phase: 21-02
    provides: "Les 8 fichiers de la source app-bureau rendus ORPHELINS — aucun consommateur restant"
provides:
  - "Les 8 fichiers de la source app-bureau et leurs 2 fichiers de tests ont quitte le depot (1 294 lignes)"
  - "SessionSnapshot est revenu a 5 champs : plus de typologie ni d'origine a inventer"
  - "Le widget ne binde plus de libelle de type, et son message d'activation ne promet plus l'app bureau"
  - "GardesPerimetreTests : garde par REFLEXION sur l'assembly + garde de SOURCE sur le XAML, les deux prouvees falsifiables"
  - "SessionStylesBindingTests : 72 combinaisons style x theme chargees en BAML reel, mesurees et disposees"
affects: [21-04 purge des fantomes archives, 22 observabilite du diagnostic, 24 fusion par fraicheur]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Garde de non-retour a deux etages : reflexion sur l'assembly (types) + lecture de SOURCE (bindings XAML, invisibles a la reflexion)"
    - "Une garde se prouve par MUTATION : on reintroduit ce qu'elle interdit, on verifie qu'elle rougit, on revoque"
    - "Une garde porte toujours son anti-muette : un assembly mal resolu ou un fichier vide ne doit pas passer pour un succes"
    - "Une fenetre WPF jamais affichee n'a PAS de template applique : on monte et on mesure sa GRILLE RACINE, jamais la fenetre"

key-files:
  created:
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - tests/Chronos.Tests/SessionStylesBindingTests.cs
  modified:
    - src/Chronos/Services/SessionSnapshot.cs
    - src/Chronos/ViewModels/SessionsViewModel.cs
    - src/Chronos/ViewModels/SessionsPreviewViewModel.cs
    - src/Chronos/Resources/SessionStyles.xaml
    - src/Chronos/Views/SessionsController.cs
    - src/Chronos/Chronos.csproj
  deleted:
    - src/Chronos/Services/DesktopUiaSessionSource.cs
    - src/Chronos/Services/WindowsUiaTreeProvider.cs
    - src/Chronos/Services/UiaLabels.cs
    - src/Chronos/Services/DesktopUiaPollService.cs
    - src/Chronos/Services/WindowsForegroundWatch.cs
    - src/Chronos/Services/UiaNode.cs
    - src/Chronos/Services/IForegroundWatch.cs
    - src/Chronos/Services/IUiaTreeProvider.cs
    - tests/Chronos.Tests/DesktopUiaSessionSourceTests.cs
    - tests/Chronos.Tests/DesktopUiaPollServiceTests.cs

key-decisions:
  - "Le motif de montage BAML du plan (Measure sur la FENETRE) rendait 0x0 : une fenetre jamais affichee n'a pas de template applique. Corrige par le motif MAISON documente de CadranBindingTests (montage de la grille racine + purge du Dispatcher) — sans quoi le test aurait ete rouge pour une mauvaise raison, puis vert pour une pire."
  - "Les deux gardes ont ete prouvees FALSIFIABLES par mutation avant d'etre committees : un type Uia* reintroduit fait rougir la garde de reflexion, un binding KindLabel reintroduit fait rougir la garde de source, et un separateur orphelin fait passer le compte Pastilles de 4 a 8."
  - "Le commentaire du .csproj qui nommait WindowsUiaTreeProvider est retire : il n'y avait aucun <Reference> a supprimer, seul le commentaire survivait, et il nommait un type disparu."
  - "Aucune anticipation des phases 22 a 26 : new SessionMonitor() nu, files.Take(8), tmp+Move et le TTL de 6 h restent intacts. La purge d'archived.json reste le plan 21-04."

requirements-completed: [SRC-01]
requirements-progressed: []

# Metrics
duration: 8min
completed: 2026-09-12
---

# Phase 21 Plan 03 : Supprimer la source app-bureau (SRC-01, second geste) Summary

**Les 1 294 lignes de la source app-bureau par UI Automation ont quitte le depot en trois commits tous compilables, `SessionSnapshot` est revenu a cinq champs, et deux gardes — reflexion sur l'assembly et lecture de source XAML — rendent desormais le retour en arriere rouge au build, ce qui a ete prouve par mutation puis revoque.**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-09-12T14:52Z
- **Completed:** 2026-09-12T15:00Z
- **Tasks:** 3
- **Files:** 10 supprimes, 6 modifies, 2 crees

## Accomplishments

- **La contrainte dure est tenue : la solution compile a CHAQUE commit.** Trois `dotnet build` (un par
  tache) rendent **0 erreur / 0 avertissement**. Aucun « echec de build attendu ».
- **Le total de tests atteint le chiffre predit au test pres, apres chaque tache** : 747 → **709** (tache 1,
  −40 +2) → **710** (tache 2, +1) → **712** (tache 3, +2). Le `21-VALIDATION.md` annoncait 709 / 710 / 712.
  **Aucun ecart a instruire.**
- **Suite complete verte DEUX FOIS de suite** : **712 / 0 echec / 4 s**, deux executions consecutives — la
  verification qu'impose le chargeur BAML (un defaut de parallelisme ne se voit pas a la premiere passe).
- **Les deux gardes de non-retour sont FALSIFIABLES, et ce n'est pas une affirmation : c'est une mesure.**
  Trois mutations ont ete injectees puis revoquees (detail plus bas).
- **Le critere 4 de la phase — « aucun trou visuel » — est prouve sans lancer l'overlay** : les
  **72 combinaisons** (8 styles x 9 themes) chargent le vrai BAML, recoivent les vrais pinceaux du theme,
  et sont mesurees / disposees sans exception ni taille degeneree.
- **La correction MESUREE du contexte de phase est confirmee** : le libelle de type n'etait binde que dans
  **1** des 8 styles (`TplPastilles`, 3 occurrences de `KindLabel` entre les lignes 70 et 79), pas dans les
  8 comme l'annoncait le `21-CONTEXT.md`. `git diff --numstat` sur `SessionStyles.xaml` rend exactement
  **`0 10`** : zero ajout, dix suppressions, donc **aucun des sept autres styles n'a ete touche**.

## Task Commits

1. **Task 1 : les dix fichiers quittent le depot + garde par reflexion** — `ff783d4` (refactor) — 709 tests
2. **Task 2 : SessionSnapshot a cinq champs, XAML, message d'activation** — `c327660` (refactor) — 710 tests
3. **Task 3 : les 8 styles x 9 themes charges, mesures, disposes** — `af1b1c9` (test) — 712 tests

## Bilan NOMINATIF des 40 cas de test supprimes

Matiere du bilan de couverture du plan 04. Mesure exacte : `DesktopUiaSessionSourceTests.cs` portait
**25 `[Fact]` + 3 `[Theory]` totalisant 11 `[InlineData]` = 36 cas** ; `DesktopUiaPollServiceTests.cs`
portait **4 `[Fact]`**. Total **40**, conforme a l'annonce du plan.

Chacun instanciait **exclusivement** `DesktopUiaSessionSource` ou `DesktopUiaPollService` et assertait sur
des types qui n'existent plus (`UiaNode`, `UiaLabels`, `DesktopHealth`, `IUiaTreeProvider`). **Aucun ne
couvre une ligne survivante.**

### `DesktopUiaSessionSourceTests.cs` — 28 methodes, 36 cas

| # | Test (nom de methode) | Cas | Code supprime qu'il couvrait |
|---|---|---|---|
| 1 | `Matches_reconnait_Responding_fr_en_insensible_casse_espaces` | 4 | `UiaLabels.Matches` — appariement souple fr/en |
| 2 | `Matches_est_faux_pour_null_vide_ou_non_correspondant` | 4 | `UiaLabels.Matches` — cas degeneres |
| 3 | `StartsWithAny_extrait_le_nom_apres_prefixe_fr` | 1 | `UiaLabels.StartsWithAny` — extraction de nom (fr) |
| 4 | `StartsWithAny_extrait_le_nom_apres_prefixe_en` | 1 | `UiaLabels.StartsWithAny` — extraction de nom (en) |
| 5 | `StartsWithAny_est_faux_et_remainder_vide_sans_prefixe` | 3 | `UiaLabels.StartsWithAny` — absence de prefixe |
| 6 | `MapTree_racine_null_donne_liste_vide_sans_exception` | 1 | `DesktopUiaSessionSource.MapTree` — racine absente |
| 7 | `MapTree_fenetre_sans_ancre_RootWebArea_donne_liste_vide` | 1 | `MapTree` — ancre d'accessibilite absente |
| 8 | `MapTree_texte_Claude_repond_donne_foreground_Working` | 1 | `MapTree` — detection d'etat par texte d'`UiaNode` |
| 9 | `MapTree_bouton_Arreter_sans_texte_donne_Working` | 1 | `MapTree` — detection d'etat par bouton |
| 10 | `MapTree_toggle_persistant_Ignorer_les_permissions_ne_donne_PAS_WaitingAttention` | 1 | `MapTree` — exclusion du toggle persistant |
| 11 | `MapTree_foreground_Cowork_non_emis_car_etat_VM_non_observable` | 1 | `MapTree` — non-emission Cowork (typologie disparue) |
| 12 | `MapTree_type_Code_prime_sur_ChatMode_co_present` | 1 | `MapTree` — arbitrage de typologie d'app bureau |
| 13 | `MapTree_mode_chat_au_repos_donne_WaitingTurn_Chat` | 1 | `MapTree` — typologie « chat » (producteur disparu) |
| 14 | `MapTree_ancre_sans_signal_donne_Unknown` | 1 | `MapTree` — repli sur indetermine |
| 15 | `MapTree_panneaux_Code_donnent_Kind_Code` | 1 | `MapTree` — typologie deduite des panneaux |
| 16 | `MapTree_sidebar_enumere_les_sessions_en_cours_et_ignore_les_autres` | 1 | `MapTree` — enumeration de la barre laterale |
| 17 | `MapTree_foreground_nomme_par_l_entete_de_session` | 1 | `MapTree` — nommage par en-tete |
| 18 | `MapTree_foreground_sans_repo_retombe_sur_sans_titre` | 1 | `MapTree` — repli de nommage |
| 19 | `MapTree_sessions_sidebar_typees_Cowork_si_app_bridgee` | 1 | `MapTree` — typologie via le pont VM |
| 20 | `MapTree_ignore_les_libelles_dans_le_contenu_des_messages` | 1 | `MapTree` — anti-faux-positif sur le contenu |
| 21 | `Poll_accumule_les_sessions_en_gardant_leur_dernier_etat_connu` | 1 | `DesktopUiaSessionSource.Poll` — cache accumulatif |
| 22 | `MapTree_ne_duplique_pas_un_foreground_deja_liste_en_sidebar` | 1 | `MapTree` — deduplication |
| 23 | `Poll_ne_duplique_pas_une_session_dont_le_type_change_selon_la_vue` | 1 | `Poll` — deduplication malgre changement de typologie |
| 24 | `Poll_racine_null_donne_Health_WindowMissing_et_cache_vide` | 1 | `Poll` + `DesktopHealth.WindowMissing` |
| 25 | `Poll_fenetre_sans_ancre_donne_Health_AnchorMissing_et_cache_vide` | 1 | `Poll` + `DesktopHealth.AnchorMissing` |
| 26 | `Poll_avec_ancre_donne_Health_Ok` | 1 | `Poll` + `DesktopHealth.Ok` |
| 27 | `Read_avant_Poll_est_vide_et_ne_touche_pas_le_provider` | 1 | `DesktopUiaSessionSource.Read` — lecture non bloquante |
| 28 | `Read_rend_le_cache_sans_rappeler_le_provider` | 1 | `Read` — lecture sans appel COM |

### `DesktopUiaPollServiceTests.cs` — 4 methodes, 4 cas

| # | Test (nom de methode) | Code supprime qu'il couvrait |
|---|---|---|
| 29 | `PollOnce_remplit_le_cache_a_partir_du_vide` | `DesktopUiaPollService.PollOnce` |
| 30 | `PollOnce_provider_null_ne_leve_pas_et_laisse_le_cache_vide` | `PollOnce` — tolerance a l'absence d'`IUiaTreeProvider` |
| 31 | `PollOnce_utilise_l_heure_de_l_horloge_injectee` | `PollOnce` — horloge injectee |
| 32 | `StartAsync_puis_StopAsync_sont_surs_et_idempotents` | `DesktopUiaPollService` en tant qu'`IHostedService` |

### Arithmetique de couverture

| Repere | Valeur |
|---|---|
| Apres le plan 21-02 | 747 |
| Tache 1 : −40 cas supprimes, +2 gardes | **709** (predit : 709) |
| Tache 2 : +1 garde de source XAML | **710** (predit : 710) |
| Tache 3 : +2 tests de montage BAML | **712** (predit : 712) |

Cumul de phase : **752 − 49 + 14 = 717** a ce jour. Les 2 tests restants et la fermeture du bilan sont le
plan 21-04 (cible **719**).

## Preuves de FALSIFIABILITE des gardes (mutation puis revocation)

Une garde qu'on n'a jamais vue rougir n'est pas une garde, c'est une decoration. Les trois mutations
ci-dessous ont ete injectees, mesurees, puis **integralement revoquees** (arbre de travail revalide propre
par `git status --porcelain` avant chaque commit).

| Garde | Mutation injectee | Resultat mesure | Revocation |
|---|---|---|---|
| `Aucun_type_de_la_source_app_bureau_ne_subsiste_dans_l_assembly` | fichier temporaire `src/Chronos/Services/_MutationGardePerimetre.cs` declarant `internal sealed class UiaTreeProviderRevenant` | **ROUGE** (1 echec / 1 reussite) | fichier supprime → **2 / 0** |
| `Aucun_style_de_session_ne_binde_plus_un_libelle_de_type` | `<TextBlock Text="{Binding KindLabel}"/>` reinsere dans `TplPastilles` | **ROUGE** (1 echec / 2 reussites) | XAML restaure → **3 / 0**, `git diff --numstat` toujours `0 10` |
| `Le_style_Pastilles_n_a_plus_qu_un_seul_separateur_visible_par_session` | second `<TextBlock Text="  ·  "/>` reinsere dans `TplPastilles` | **ROUGE** : attendu 4, **mesure 8** | XAML restaure → vert |

La troisieme mutation prouve aussi que le test **voit reellement l'arbre visuel** : le compte passe de 4 a
8, il ne reste pas bloque a 0. C'est la contre-epreuve du piege signale par `CadranBindingTests` (« vert
pour de mauvaises raisons »).

## Verification

| Critere du plan | Attendu | Mesure |
|---|---|---|
| `dotnet build Chronos.sln -c Debug` apres CHAQUE tache | 0 erreur | **0 erreur, 0 avertissement** (3 fois) |
| `ls src/Chronos/Services/ \| grep -ci "uia\|foregroundwatch"` | 0 | **0** |
| `ls tests/Chronos.Tests/ \| grep -c "DesktopUia"` | 0 | **0** |
| `grep -rn "DesktopUia\|IUiaTreeProvider\|WindowsUia\|UiaLabels\|UiaNode\|IForegroundWatch\|WindowsForegroundWatch\|DesktopHealth" --include=*.cs --include=*.xaml src \| wc -l` | 0 | **0** |
| `grep -c "class GardesPerimetreTests" tests/Chronos.Tests/GardesPerimetreTests.cs` | 1 | **1** |
| `grep -rn "SessionKind\|SessionOrigin\|KindLabel\|KindText" --include=*.cs --include=*.xaml src \| wc -l` | 0 | **0** |
| `wc -l < src/Chronos/Services/SessionSnapshot.cs` | ≤ 22 | **22** |
| `grep -c "record SessionSnapshot" SessionSnapshot.cs` | 1 | **1** |
| `grep -c "System.DateTimeOffset UpdatedAt);" SessionSnapshot.cs` | 1 | **1** |
| `grep -c "enum SessionActivity" SessionSnapshot.cs` | 1 (il SURVIT) | **1** |
| `grep -c 'Text="  ·  "' SessionStyles.xaml` | 2 (3 avant) | **2** |
| `grep -c 'DataTemplate x:Key=' SessionStyles.xaml` | 8 | **8** |
| `git diff --numstat src/Chronos/Resources/SessionStyles.xaml` | `0 10` | **`0 10`** |
| `grep -ci "app bureau incluse" SessionsController.cs` | 0 | **0** |
| `grep -c "Claude Code ACTIVES" SessionsController.cs` | 1 | **1** |
| `grep -c "SessionKind" tests/.../GardesPerimetreTests.cs` | 1 (litteral de garde, hors perimetre) | **1** |
| `grep -c "ThemeCatalog.All" SessionStylesBindingTests.cs` | ≥ 1 | **1** |
| `grep -c "Enum.GetValues<SessionStyle>()" SessionStylesBindingTests.cs` | ≥ 1 | **1** |
| `grep -c 'Collection("XAML WPF")' SessionStylesBindingTests.cs` | 1 | **1** |
| `grep -c "WpfFact" SessionStylesBindingTests.cs` | 2 | **2** |
| Suite complete, **passe 1** | 712 / 0 echec | **712 / 0 echec / 4 s** |
| Suite complete, **passe 2** | 712 / 0 echec | **712 / 0 echec / 4 s** |
| `git status --porcelain` | 10 `D`, 5 `M`, 2 `A` | **10 `D`, 6 `M`, 2 `A`** — le 6e modifie est le `.csproj` (deviation 1, comment only) |

**Gardes nommement verifiees vertes** (filtre dedie) : `ServicesLayerPurityTests`, `CompositionRootTests`,
`NormalisationUniqueTests`, `GardesDoctrineTests`,
`La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` — **18 / 0 echec**, meme compte qu'aux plans 01
et 02.

## Invariants de securite

| Invariant | Attendu | Mesure apres le dernier commit |
|---|---|---|
| `%APPDATA%\Chronos\oauth.dat` | **518** octets (la TAILLE, jamais le mtime — l'overlay le fait bouger toutes les 60 s) | **518** |
| `%APPDATA%\Chronos\sessions\` | **66** entrees (54 `.json` + 12 `.tmp`) | **66** |
| `%APPDATA%\Chronos\archived.json` | **84** octets (la purge est le plan 21-04) | **84** |
| Overlay `Chronos-v3.0.2.exe` | pid **119412** vivant, ni lance ni tue | **pid 119412 present** |
| Aucune dependance NuGet nouvelle | aucune ligne `PackageReference` ajoutee ou retiree | **0** (`git diff … \| grep -cE "^[+-].*PackageReference"` → 0) |

Aucune requete reseau reelle ; aucun test n'ecrit dans le vrai `%APPDATA%\Chronos\` (les deux nouvelles
classes de test n'ouvrent que `Path.GetTempPath()` et des fichiers du depot, avec assertion de prefixe).

## Decisions Made

- **Le motif de montage BAML du plan etait faux, et c'est la seule vraie difficulte rencontree.** Le plan
  prescrivait `fenetre.Measure(...)` / `fenetre.Arrange(...)` sur la `SessionsWindow`. Mesure : `DesiredSize`
  rendait **0x0** et le compte de separateurs **0**. Cause : une fenetre jamais affichee n'a pas de template
  applique, son `Content` n'a donc **aucun parent visuel**. Le depot documentait deja la parade, dans
  `CadranBindingTests.MonterPastille` : monter la **grille racine**, y poser le `DataContext`, purger la
  file du Dispatcher a `ApplicationIdle`, puis mesurer. Applique tel quel. La contre-epreuve (mutation n°3)
  prouve que l'arbre est desormais reellement parcouru.
- **Les gardes ont ete prouvees avant d'etre committees, pas apres.** Chaque mutation a ete injectee dans
  l'arbre de travail non committe, puis revoquee par restauration d'une copie prise avant mutation — jamais
  par `git checkout`, qui aurait aussi annule le travail en cours de la tache.
- **Le fichier de garde CITE les noms interdits dans ses litteraux, et c'est voulu.** Une garde doit nommer
  ce qu'elle interdit. Tous les criteres de zero-occurrence de ce plan portent sur `src/`, jamais sur
  `tests/`.
- **Hors perimetre respecte a la lettre.** `new SessionMonitor()` nu et `files.Take(8)` restent dans
  `DiagnosticService` (OBS-01/OBS-02, phase 22) ; `tmp`+`Move` d'`ArchiveStore` reste (CYC-02, phase 23) ;
  le TTL de 6 h reste (TRT-04, phase 26) ; `archived.json` n'est pas purge (plan 21-04) et fait toujours
  **84 octets**.

## Deviations from Plan

### Auto-fixed Issues

**1. [Regle 2 - Coherence documentaire] Le `.csproj` de production nommait encore `WindowsUiaTreeProvider`**

- **Found during:** Task 1, a l'etape de verification prealable (« si un onzieme fichier apparait,
  s'arreter »).
- **Issue:** `grep -rln` rendait un **onzieme** fichier non prevu : `src/Chronos/Chronos.csproj`. Inspection :
  un commentaire de 7 lignes expliquant pourquoi les assemblies d'accessibilite Windows n'exigeaient aucun
  `<Reference>` explicite, **consommees par `WindowsUiaTreeProvider`**. Ce n'est donc **pas** un site
  d'appel — aucun element de build, aucun impact sur la compilation — mais un commentaire qui allait nommer
  un type inexistant. Verification faite : `grep -rn "System.Windows.Automation\|UIAutomation"` sur `src` et
  `tests` rend **0 ligne** apres suppression des 8 fichiers.
- **Fix:** commentaire remplace par une note de phase 21 expliquant qu'il n'y a jamais eu de `<Reference>` a
  retirer. **Aucun element de build touche** : `git diff … \| grep -cE "^[+-].*PackageReference"` rend **0**.
  Precedent exact de la meme regle au plan 21-02 (commentaire de classe d'`InventaireMachine`).
- **Files modified:** `src/Chronos/Chronos.csproj`
- **Verification:** `dotnet build` 0 erreur / 0 avertissement ; suite 709 / 0 echec.
- **Committed in:** `ff783d4`

---

**2. [Regle 3 - Blocage] Le montage BAML prescrit par le plan rendait une taille degeneree 0x0**

- **Found during:** Task 3, premiere execution de `SessionStylesBindingTests`.
- **Issue:** Les deux tests echouaient : « taille degeneree pour le style Pastilles et le theme minuit :
  0;0 » et « Expected 4, Actual 0 ». Le code du plan appelait `Measure`/`Arrange` sur la `SessionsWindow`
  elle-meme. Une `Window` jamais affichee n'a pas de template applique : sa `DesiredSize` reste nulle, son
  `Content` n'a aucun parent visuel, le `DataContext` ne se propage pas et **aucun binding ne s'evalue**.
  Sans correction, la tache etait bloquee — et une « correction » par assouplissement de l'assertion aurait
  produit un test vert pour une mauvaise raison, exactement le piege que `CadranBindingTests` documente.
- **Fix:** ajout d'une aide privee `Monter(vm, theme)` recopiant le motif MAISON deja documente dans
  `CadranBindingTests.MonterPastille` : recuperation de `fenetre.Content` en `FrameworkElement`, pose du
  `DataContext` dessus, purge de la file du Dispatcher a `ApplicationIdle`, puis `Measure`/`Arrange`/
  `UpdateLayout` sur cette **grille racine**. Les assertions portent desormais sur `racine.DesiredSize` et
  `TextesVisibles(racine)`. **La double boucle sur `ThemeCatalog.All` x `Enum.GetValues<SessionStyle>()`
  est inchangee**, ainsi que les seuils (8, 9, 4).
- **Files modified:** `tests/Chronos.Tests/SessionStylesBindingTests.cs`
- **Verification:** 2 / 0 echec ; contre-epreuve par mutation (separateur orphelin) : le compte passe de
  **4 a 8**, prouvant que l'arbre visuel est reellement parcouru.
- **Committed in:** `af1b1c9`

---

**Total deviations:** 2 auto-corrigees (1 coherence de commentaire, 1 blocage technique de montage BAML).
**Impact on plan:** nul sur le perimetre. Un fichier hors des 17 annonces (`Chronos.csproj`, commentaire
seul). Les totaux de tests, les criteres d'acceptation et les gardes sont ceux du plan, au chiffre pres.

## Issues Encountered

Le seul obstacle reel est la deviation n°2. Il valait d'etre rencontre : la version litterale du plan
aurait pu etre « reparee » en relachant l'assertion (accepter une taille nulle), ce qui aurait produit deux
tests verts prouvant **rien du tout** sur les 72 combinaisons. La mutation de contre-epreuve (4 → 8
separateurs) est la reponse a cette tentation.

Les `<interfaces>` fournies par le plan etaient exactes pour tous les contrats C# (`SessionMonitor`,
`ISessionSource`, `ArchiveStore`, `SessionsViewModel`, `ThemeCatalog`, `ChronosTheme.Key`) — aucune
exploration n'a ete necessaire pour les retrouver. Les numeros de ligne du XAML (70-79) etaient exacts au
caractere pres.

## Known Stubs

Aucun. Ce plan ne fait que **retirer** du code et ajouter des gardes. Aucun placeholder, aucune valeur en
dur, aucun composant sans source de donnees n'a ete introduit.

## Point de suivi du bilan nominatif (cumul de phase)

| Repere | Valeur |
|---|---|
| Baseline avant phase 21 | 752 |
| Apres le plan 21-01 | 756 (+4) |
| Apres le plan 21-02 | 747 (−9) |
| **Apres le plan 21-03** | **712** (−40 cas supprimes, +5 tests ajoutes) |
| Cible de fin de phase (`21-VALIDATION.md`) | 719 |
| Restant (plan 21-04) | +7 tests (`ArchiveStorePurgeTests` 6 + garde de cablage 1) |

Tests supprimes a ce jour : **49 / 49** annonces — le bilan nominatif est **complet**, il ne reste plus
qu'a le consolider au plan 21-04 tache 3.
Tests ajoutes a ce jour : **9 / 16** annonces.

## À VÉRIFIER PAR L'UTILISATEUR

Non bloquant, differe. Ces points ne sont observables qu'apres un redemarrage **volontaire** de l'overlay,
que cette phase s'interdit de provoquer (instance `Chronos-v3.0.2.exe`, pid 119412, en cours d'utilisation).

1. **Critere 4, a l'oeil** — ouvrir la galerie `--sessions` et parcourir les **8 styles** : la rangee d'un
   element **Pastilles** doit lire `etat · detail`, **sans point median orphelin en fin de ligne**, et sur
   les 9 themes. Le test de montage BAML prouve l'absence d'exception, de taille degeneree et de separateur
   en trop ; il ne juge pas l'esthetique du resultat.
2. **Message d'activation** — desactiver puis reactiver le widget de sessions : la boite de dialogue ne doit
   plus contenir « (app bureau incluse) ».
3. **Rappel du plan 21-02, toujours en attente** — avec l'app bureau Claude ouverte au premier plan, plus
   aucune ligne prefixee `desktop:` ne doit apparaitre dans le widget.

## User Setup Required

Aucune. Rien d'externe a configurer, aucune dependance nouvelle.

## Next Phase Readiness

- **Le plan 21-04 peut demarrer sans condition prealable.** Il ne partage aucun fichier de production avec
  ce plan ; il ETEND `tests/Chronos.Tests/GardesPerimetreTests.cs`, qui existe desormais et expose deja
  l'aide `CheminSources()` (`internal static`, prete a etre reutilisee par la garde de cablage de SRC-02).
- **`archived.json` est intact (84 octets)** et contient toujours les deux entrees fantomes
  `desktop:foreground:*` : la matiere du plan 21-04 n'a pas ete entamee.
- **SRC-01 est refermee** : les 8 fichiers nommes par l'exigence dans `REQUIREMENTS.md` ont tous quitte le
  depot, et deux gardes falsifiables interdisent leur retour.

## Self-Check: PASSED

- Fichiers crees presents : `tests/Chronos.Tests/GardesPerimetreTests.cs`,
  `tests/Chronos.Tests/SessionStylesBindingTests.cs`.
- Fichiers supprimes absents : les 10 (verifie par `ls … | grep -c` → 0 et `git diff --name-status` → 10 `D`).
- Commits presents : **`ff783d4`**, **`c327660`**, **`af1b1c9`** (`git log --oneline`).
- Suite complete rejouee apres le dernier commit : **712 / 0 echec**, deux passes.
- Invariants de securite revalides apres le dernier commit : `oauth.dat` 518 o, `sessions\` 66 entrees,
  `archived.json` 84 o, pid 119412 vivant, aucun `PackageReference` ajoute ni retire.

---
*Phase: 21-p-rim-tre-le-widget-ne-parle-que-de-claude-code*
*Completed: 2026-09-12*
