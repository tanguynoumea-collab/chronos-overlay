---
phase: 29-ce-que-l-app-bureau-sait-de-chaque-session
plan: 01
subsystem: widget-sessions
tags: [csharp, msix, appdata, racines, moniteur, balayage, diagnostic, gardes, mutation, xunit, tdd]

sha_entree_de_plan: 10a821f
one_liner: "Le widget lit enfin les fichiers que les hooks de l'app bureau écrivent réellement : RacinesEtat résout les racines par candidats (paquet MSIX Claude_* d'abord, vue réelle ensuite, suffixe éditeur non codé), le moniteur lit et fusionne TOUTES les racines d'état par l'arbitrage ordinaire, le balayage CYC-01 passe sur chacune avec une seule lecture d'attestation, le rapport compte racine par racine et écrit « absent (dossier introuvable) », la production câble le tout par argument nommé sous garde ; 947 → 965 tests isolés (+18), 996 combinés avec 29-02 en cours, 0 échec"

requires:
  - phase: 28-04
    provides: "HorizonsSessions et AppliquerSilence en un point (le corps de boucle du moniteur reste inchangé, gardes textuelles intactes)"
  - phase: 29-recherche
    provides: "Q0 — la sonde hors de l'arbre (29-SONDE-HORS-ARBRE.txt) : virtualisation AppData MSIX, pas une jonction"
provides:
  - "RacinesCandidates(EtatsHooks, SessionsAppBureau) et RacinesEtat.Candidats / ParDefaut / PremiereExistante — seul fichier de src/ qui porte les littéraux du chemin de l'app bureau"
  - "SessionMonitor : paramètre dossiersEtat (dernier, optionnel), propriété Dossiers (Directory supprimée), lecture de toutes les racines"
  - "BalayageMagasinSessions : constructeur à liste de racines, propriété Dossiers, attestation lue une fois, bilan = somme"
  - "DiagnosticService : une ligne « Fichiers d'état (<racine>) » par racine du moniteur, racine absente écrite absente"
  - "App.xaml.cs : singleton RacinesEtat.ParDefaut(), moniteur par argument nommé, balayage sur .Dossiers"
  - "§3 de docs/hooks-contract.md : « Deux vues d'AppData », tenu par une garde croisée document ↔ code"
affects: [29-02, 29-03, 29-05, 30-la-lecture-fait-disparaitre, 31-crit-publi-constat]

tech-stack:
  added: []
  patterns:
    - "Racines par CANDIDATS, résolues une fois (singleton), existence testée à chaque cycle par le consommateur"
    - "Énumérer par motif (Claude_*) plutôt que coder un hachage d'éditeur ; énumération MATÉRIALISÉE dans son try"
    - "Deux façons de dire la même chose au constructeur = ArgumentException, jamais un choix silencieux"
    - "Squelette RED qui garde UNE racine (comportementalement faux mais compilable) : le rouge nomme exactement les tests multi-racines"

key-files:
  created:
    - src/Chronos/Services/RacinesEtat.cs
    - tests/Chronos.Tests/RacinesEtatTests.cs
    - tests/Chronos.Tests/DeuxVuesAppDataTests.cs
  modified:
    - src/Chronos/Services/SessionMonitor.cs
    - src/Chronos/Services/BalayageMagasinSessions.cs
    - src/Chronos/Services/DiagnosticService.cs
    - src/Chronos/App.xaml.cs
    - docs/hooks-contract.md
    - tests/Chronos.Tests/BalayageMagasinSessionsTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - tests/Chronos.Tests/ContratHooksDocumenteTests.cs

key-decisions:
  - "APP-06 : les racines d'état se résolvent par candidats (paquet MSIX Claude_* en ordre ordinal, puis vue réelle) une fois au démarrage ; l'existence est testée à chaque cycle ; un paquet installé après le lancement n'est vu qu'au lancement suivant (limite écrite)"
  - "États des hooks : TOUTES les racines lues (union, fusion par l'arbitrage FUS-01) ; métadonnées de l'app bureau : la PREMIÈRE qui existe (PremiereExistante, consommée par 29-02/29-03)"
  - "SessionMonitor.Directory disparaît au profit de Dossiers ; sessionsDir reste le raccourci des tests ; sessionsDir ET dossiersEtat => ArgumentException"
  - "La garde Le_demarrage_balaie... change de littéral (.Directory → .Dossiers) sans changer d'intention ; une nouvelle garde tient le câblage du moniteur de production par argument nommé"

requirements-completed: []

duration: 16min
completed: 2026-09-25
---

# Phase 29 Plan 01 : Deux vues d'AppData, lues toutes les deux (APP-06) — Summary

**Les fichiers que les hooks de l'app bureau écrivent réellement atteignent maintenant le widget. Ils sont dans la
vue virtualisée du paquet MSIX, que l'overlay ne voyait pas. `RacinesEtat` résout les racines par candidats : les
paquets `Claude_*` d'abord, la vue réelle ensuite. Le moniteur lit toutes les racines d'état et laisse l'arbitrage
ordinaire fusionner par `session_id`. Le balayage CYC-01 passe sur chacune en ne lisant l'attestation de vie
qu'une fois. Le rapport compte les fichiers racine par racine et écrit « absent (dossier introuvable) » pour une
racine qui manque. La production câble le tout, et une garde le vérifie. La suite passe de 947 à 965 tests
(+18) mesurés seuls, et à 996 avec les tests de 29-02 en cours. 0 échec.**

## Performance

- **Durée :** ~16 min (21:01:20Z → ~21:17Z)
- **Début :** 2026-09-25T21:01:20Z
- **Fin :** 2026-09-25T21:17Z
- **Tâches :** 3 / 3
- **Fichiers :** 13 (1 source créée, 4 sources modifiées, 1 document, 2 classes de tests créées, 5 classes de tests modifiées)

## Mesures

| Point | Valeur |
|---|---|
| SHA d'entrée du plan | `10a821f` (vague 1, en parallèle de 29-02 sur le MÊME arbre, fichiers disjoints) |
| Entrée | **947 / 0** (donnée de l'orchestrateur). La suite complète après la tâche 1 en fait **953 / 0** = 947 + 6 : l'entrée est confirmée |
| RED tâche 1 (squelette `NotImplementedException`) | **6 échecs / 6** `RacinesEtatTests` : les six cas (`Le_paquet_Claude_passe_avant…`, `Sans_dossier_Packages…`, `Deux_paquets_Claude…`, `Les_racines_d_etat_designent…`, `PremiereExistante…`, `Aucun_chemin_ne_melange…`) |
| Après tâche 1 (`d3e8787`), suite complète | **953 / 0** |
| RED tâche 2 (squelettes qui ne gardent qu'UNE racine) | **13 échecs / 218** sur le filtre de la tâche : les 11 nouveaux cas, plus 2 adaptés (`Le_demarrage_balaie_le_magasin_de_sessions_sur_le_dossier_du_moniteur`, `Le_graphe_DI_resout_la_chaine_de_sessions`). Le helper `Balayeur` adapté reste vert par construction |
| Après tâche 2 (`428bf43`), filtre de la tâche | **218 / 0** |
| Mutation M1 (`foreach (var racine in _dossiers.Take(1))` dans `SessionMonitor.Inspecter`) | **4 échecs / 67** : `Le_moniteur_lit_les_fichiers_d_etat_des_deux_racines`, `Les_fichiers_perimes_sont_comptes_sur_toutes_les_racines`, `Une_meme_session_vue_dans_deux_racines_est_tranchee_par_la_fraicheur`, `Une_racine_absente_ne_coute_rien_aux_autres`. Révoquée : sha256 `b3d8e445…f028` identique avant et après |
| Mutation M2 (`foreach (var dossier in _dossiers.Take(1))` dans `BalayageMagasinSessions.Balayer`) | **2 échecs / 15** : `Chaque_racine_est_balayee_et_l_attestation_n_est_lue_qu_une_fois`, `Une_racine_absente_n_empeche_pas_de_balayer_les_autres`. Révoquée : sha256 `b8cfde23…95db` identique avant et après. `git diff --stat -- src/` vide après les deux |
| **Isolé** (suite complète hors `LecteurAppBureauTests` et `LectureSeuleAppBureauTests`, les classes de 29-02) | **965 / 0** = 947 + 18 |
| Les 18 cas de ce plan, filtrés par nom | **18 / 18** |
| **Combiné**, suite complète, 2 exécutions consécutives après `a0aad8c` | 1re : **985 / 11 échecs / 996**, les 11 rouges volontaires de 29-02 (`LecteurAppBureauTests`, RED de `b2e6fed`). 2e : **996 / 0**, 29-02 ayant écrit entre-temps son implémentation (pas encore commitée). Le total final de vague (1000 annoncés) reste à mesurer quand 29-02 aura commité |

## Accomplishments

- **Un résolveur neutre, `RacinesEtat`.** Il énumère `%LOCALAPPDATA%\Packages\Claude_*` sans coder le suffixe
  éditeur. Il filtre aussi par `StartsWith("Claude_")`, trie en ordre ordinal et matérialise la liste dans le
  `try`. Il rend deux familles, chacune dédoublonnée dans l'ordre :
  - `EtatsHooks` : `…\LocalCache\Roaming\Chronos\sessions` pour chaque paquet, puis `%APPDATA%\Chronos\sessions` ;
  - `SessionsAppBureau` : `…\LocalCache\Roaming\Claude\claude-code-sessions` pour chaque paquet, puis la vue réelle.

  Il ne lève jamais et n'écrit nulle part : les greps `File.`, `Directory.Create`, `Delete`, `FileMode` et
  `FileAccess` donnent 0. C'est le seul fichier de `src/` qui porte `Claude_` ou `claude-code-sessions`, ce que
  vérifie `grep -rl` sur `src/`.
- **Le moniteur lit toutes les racines d'état.** `foreach (var racine in _dossiers)` entoure la collecte. Le corps
  de boucle ne change pas : toujours `AppliquerSilence` et `SourceSession.Hook`. Les fichiers périmés sont cumulés
  sur toutes les racines. Une même session vue dans deux racines est tranchée par la fraîcheur, et le désaccord
  est dit (retenu Hook/Working, écarté Hook/WaitingTurn, écart de 9 min). Donner `sessionsDir` et `dossiersEtat`
  en même temps lève `ArgumentException`. La propriété `Directory`, singulière, disparaît au profit de `Dossiers`.
- **Le balayage passe sur chaque racine.** Pour chaque racine existante, il retire d'abord les débris, puis les
  états périmés. L'attestation de vie est lue une seule fois, paresseusement, à la première racine qui existe
  (`vivantes ??= LireAttestation(maintenant)`). Le bilan est la somme des racines, et une racine absente est
  sautée.
- **Le rapport compte racine par racine.** Il écrit une ligne `Fichiers d'état (<racine>) : N` par racine, toutes
  avant la liste « · » fusionnée, dont le tri et les textes ne changent pas. Une racine absente donne
  `absent (dossier introuvable)`. Le libellé de source `fichier de hook (%APPDATA%\Chronos\sessions)` ne change
  pas.
- **La production est câblée.** App.xaml.cs enregistre `services.AddSingleton(_ => RacinesEtat.ParDefaut())`. Le
  moniteur reçoit `dossiersEtat: sp.GetRequiredService<RacinesCandidates>().EtatsHooks` et le balayeur
  `sp.GetRequiredService<SessionMonitor>().Dossiers`. Le mode `--hook` n'est pas modifié : il porte seulement un
  commentaire.
- **Le contrat le dit.** Le §3 contient le paragraphe « Deux vues d'AppData, lues toutes les deux », tenu par une
  garde croisée : le document porte les trois fragments et `RacinesEtat.cs` construit ce chemin.

## Task Commits

1. **Tâche 1 : RacinesEtat, les racines se résolvent par candidats.**
   - `e422deb` (test, RED 6/6 sur squelette)
   - `d3e8787` (feat, GREEN 6/6, suite complète 953/0)
2. **Tâche 2 : moniteur, balayage et rapport sur chaque racine ; câblage de production.**
   - `4c806da` (test, RED 13/218 sur squelettes à une racine)
   - `428bf43` (feat, GREEN 218/218)
3. **Tâche 3 : contrat des hooks et garde croisée.**
   - `a0aad8c` (docs + test)
   - Mutations M1 et M2 jouées puis révoquées, sans commit.

**Plan metadata :** commit `docs(29-01)` final (SUMMARY, STATE, ROADMAP).

## Critères grep

**Tâche 1**, dans `RacinesEtat.cs` :

| Critère | Mesuré |
|---|---|
| `Candidats(string localAppData, string appData)` | **1** |
| `"Claude_*"` | **1** |
| `claude-code-sessions` | **2** (au moins 1 requis) |
| API d'écriture | **0** |

**Tâche 2 :**

| Fichier | Critère | Mesuré |
|---|---|---|
| `SessionMonitor.cs` | `public string Directory` | **0** |
| `SessionMonitor.cs` | `public IReadOnlyList<string> Dossiers` | **1** |
| `SessionMonitor.cs` | `foreach (var racine in _dossiers)` | **1** |
| `SessionMonitor.cs` | `TimeSpan.From` | **0** |
| `SessionMonitor.cs` | `Claude_\|claude-code-sessions` | **0** |
| `BalayageMagasinSessions.cs` | `Dossiers` | **1** |
| `BalayageMagasinSessions.cs` | `public string Dossier ` | **0** |
| `DiagnosticService.cs` | `_moniteurSessions?.Dossiers` | **1** |
| `DiagnosticService.cs` | `absent (dossier introuvable)` | **1** |
| `App.xaml.cs` | `RacinesEtat.ParDefaut()` | **1** |
| `App.xaml.cs` | `dossiersEtat: sp.GetRequiredService<RacinesCandidates>().EtatsHooks` | **1** |
| `App.xaml.cs` | `GetRequiredService<SessionMonitor>().Dossiers` | **1** |
| `App.xaml.cs` | `SessionMonitor>().Directory` | **0** |

**Tâche 3**, dans le contrat : `Deux vues d'AppData` donne **1** et `RacinesEtat` donne **1**.

**Vérification du plan :**
- `git diff --stat 10a821f..HEAD -- src/Chronos/ViewModels src/Chronos/Resources src/Chronos/Views` est **vide** :
  aucune UI n'a changé.
- `grep -rn "ParDefaut()" tests/` donne **1**, et non 0. Voir l'écart n° 3.

**Gardes vertes, sans assouplissement :**

| Classe de tests | Verts |
|---|---|
| `RacinesEtatTests` (nouvelle) | 6/6 |
| `DeuxVuesAppDataTests` (nouvelle) | 6/6 |
| `BalayageMagasinSessionsTests` | 10/10 |
| `DiagnosticServiceTests` | 30/30 |
| `GardesPerimetreTests` | 13/13 |
| `CompositionRootTests` | 5/5 |
| `ContratHooksDocumenteTests` | 14/14 |
| `LibellesSessionsTests` | 5/5 |
| `HorizonsSessionsTests` | 11/11 |
| `InspectionSessionsTests` | 26/26 (inchangés) |
| `ServicesLayerPurityTests` | 2/2 |
| `NormalisationUniqueTests` | 3/3 |
| `GardesDoctrineTests` | 8/8 |
| `SessionsTests` | 51/51 |

`HorizonsSessionsTests` couvre notamment `Les_quatre_fichiers_lisent_le_type_unique` et
`La_regle_de_silence_vit_en_un_seul_point`. Dans `GardesPerimetreTests`, `Le_moniteur_n_arbitre_plus_par_ordre_d_insertion`
reste vert.

**Tests adaptés, sans renommage ni suppression :**
- le helper `Balayeur(...)` de `BalayageMagasinSessionsTests` vérifie désormais la garde anti-accident sur chaque
  racine de `b.Dossiers` ;
- `GardesPerimetreTests.Le_demarrage_balaie_le_magasin_de_sessions_sur_le_dossier_du_moniteur` attend le littéral
  `.Dossiers` au lieu de `.Directory` ;
- `CompositionRootTests.Le_graphe_DI_resout_la_chaine_de_sessions` suit exactement la production :
  `RacinesEtat.Candidats` est appelé sur `<tmp>\Local\Packages\Claude_test` + `<tmp>\Roaming`, et les racines
  passent au moniteur par argument nommé. Les assertions portent sur deux racines temporaires, balayeur = moniteur
  et aucune racine `claude-code-sessions`. Le `Assert.Same` est conservé.

## Files Created/Modified

- `src/Chronos/Services/RacinesEtat.cs` (créé) : `RacinesCandidates` et `RacinesEtat` ; le fait de la sonde, les
  deux familles, la limite de résolution et le statut de seul porteur du chemin sont documentés en XML-doc.
- `src/Chronos/Services/SessionMonitor.cs` : `dossiersEtat`, `Dossiers`, boucle sur les racines, `ArgumentException`.
- `src/Chronos/Services/BalayageMagasinSessions.cs` : constructeur à liste, `Dossiers`, `LireAttestation` lue une seule fois.
- `src/Chronos/Services/DiagnosticService.cs` : une ligne par racine, racine absente écrite, liste fusionnée.
- `src/Chronos/App.xaml.cs` : singleton `RacinesEtat.ParDefaut()`, moniteur et balayeur câblés, commentaire dans `RunSessionHook`.
- `docs/hooks-contract.md` : paragraphe « Deux vues d'AppData » au §3 (fins de ligne CRLF préservées).
- `tests/Chronos.Tests/RacinesEtatTests.cs` (créé, 6 cas) et `tests/Chronos.Tests/DeuxVuesAppDataTests.cs` (créé, 6 cas, 153 lignes).
- `tests/Chronos.Tests/BalayageMagasinSessionsTests.cs` (+2 et helper adapté), `DiagnosticServiceTests.cs` (+2),
  `GardesPerimetreTests.cs` (+1 et 1 adapté), `CompositionRootTests.cs` (1 adapté), `ContratHooksDocumenteTests.cs` (+1).

## Decisions Made

- **Résolution unique, existence à chaque cycle.** Les candidats sont résolus une fois (singleton DI), et chaque
  consommateur teste `Directory.Exists` à chaque cycle. Un dossier créé plus tard est donc lu dès qu'il existe.
  Un paquet installé après le lancement de l'overlay n'est vu qu'au lancement suivant : cette limite est écrite
  dans la XML-doc de `RacinesEtat`.
- **Deux familles, deux usages.** `EtatsHooks` est lue en entier (union). `SessionsAppBureau` est lue par
  `PremiereExistante`, et 29-02 la consomme. `EtatsHooks` désigne toujours `…\Chronos\sessions`, jamais le dossier
  de l'app : c'est ce qui tient le balayage à l'écart de ce dossier (APP-05), et un test le vérifie.
- **La production ne passe jamais par le défaut du moniteur.** Le défaut « aucun paramètre ⇒
  `RacinesEtat.ParDefaut()` » existe, mais App.xaml.cs donne explicitement `dossiersEtat`. La garde n° 11 exige
  ce câblage.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Robustesse] Une racine qui lève dans le rapport est écrite « illisible »**
- **Trouvé pendant :** tâche 2 (`DiagnosticService`).
- **Problème :** le plan demande un `try` par racine, mais ne dit pas quoi écrire si `GetFiles` lève sur une
  racine qui existe. Un `catch {}` vide aurait tu la racine, ce qui contredit « jamais tu ».
- **Correctif :** le `catch` écrit `Fichiers d'état (<racine>) : illisible`, et le bloc continue avec les autres
  racines.
- **Fichier :** `src/Chronos/Services/DiagnosticService.cs`. **Commit :** `428bf43`.

**2. [Rule 1 - Test robuste] La garde « jamais le dossier de l'app » lit la partie du chemin sous la racine temporaire**
- **Trouvé pendant :** tâche 1 (`Les_racines_d_etat_designent_toujours_Chronos_sessions_jamais_le_dossier_de_l_app`).
- **Problème :** tester l'absence de `\Claude\` sur le chemin complet ferait dépendre le test du `%TEMP%` de la
  machine, qui est hors du contrôle de `RacinesEtat`.
- **Correctif :** l'assertion porte sur `Path.GetRelativePath(racineTemp, chemin)` encadré de séparateurs, en
  insensible à la casse. `EndsWith(Path.Combine("Chronos", "sessions"))` porte, lui, sur le chemin complet.
- **Commit :** `e422deb`.

**3. [Écart de vérification, documenté et non corrigé] `grep -rn "ParDefaut()" tests/` donne 1 au lieu de 0**
- La seule occurrence est le littéral `"RacinesEtat.ParDefaut()"` que la garde n° 11 du plan
  (`Le_moniteur_de_production_lit_les_deux_vues_d_AppData`) cherche dans `App.xaml.cs`. Le plan exige les deux
  choses, qui se contredisent. L'intention de la vérification est qu'aucun test n'APPELLE `ParDefaut`. Elle est
  tenue : `grep -rn '[^"]RacinesEtat.ParDefaut()' tests/` donne 0. Aucun test ne construit non plus de
  `SessionMonitor` sans racine. Contourner le grep en coupant la chaîne aurait trompé l'instrument, donc je ne
  l'ai pas fait.

**4. [Rule 2 - Robustesse] `Candidats` ignore un `localAppData` vide**
- `Environment.GetFolderPath` peut rendre `""`, et `Path.Combine("", "Packages")` donnerait un chemin relatif au
  répertoire courant. Dans ce cas, aucun paquet n'est candidat et la vue réelle reste. **Commit :** `d3e8787`.

**5. [Choix de méthode] Le RED de la tâche 2 est comportemental**
- Les squelettes ne gardent qu'une racine, au lieu de lever `NotImplementedException`. Les 13 rouges nomment
  donc exactement les tests multi-racines et les deux gardes adaptées, sans rendre rouges les 205 autres tests du
  filtre.

---

**Total :** 3 écarts auto-corrigés (Rules 1-2), 1 écart de vérification documenté, 1 choix de méthode.
**Impact :** aucun élargissement de périmètre. Aucune garde n'est assouplie.

## Issues Encountered

- **Exécution parallèle avec 29-02 sur le même arbre.** La suite complète a montré les 11 rouges volontaires de
  29-02 (`LecteurAppBureauTests`, RED de `b2e6fed`). Les preuves de ce plan ont donc été isolées par filtre :
  **965 / 0** hors classes de 29-02, et **18 / 18** pour les cas de ce plan. Aucun conflit d'`index.lock` n'a eu
  lieu.

## Known Limits (écrites, non corrigées ici)

- **Dans l'arbre de l'app bureau, les deux racines montrent les mêmes fichiers.** C'est le cas d'un processus
  lancé sous Claude Code, pas celui de l'overlay. Le moniteur y lit chaque fichier deux fois :
  - les doublons se tranchent comme deux sources d'accord, donc une seule ligne et aucun désaccord ;
  - le compte des fichiers périmés, les lignes du rapport et les états conservés par le balayage y sont doublés.

  C'est la raison de la phrase du contrat : « Un constat fait depuis une session Claude Code voit la vue
  virtualisée : il ne vaut pas pour l'overlay ».
- **Un paquet `Claude_*` installé après le lancement de l'overlay n'est vu qu'au lancement suivant.**

## Known Stubs

Aucun. Les commentaires « SQUELETTE (RED) » des commits de test ont été remplacés par l'implémentation dans les
commits `feat`.

## Next Phase Readiness

- **29-02 et 29-03** consomment `RacinesCandidates.SessionsAppBureau` via `RacinesEtat.PremiereExistante`. Le
  singleton `RacinesCandidates` est déjà enregistré dans le conteneur, et `CompositionRootTests` le résout.
- **29-05** porte la garde APP-05 : `RacinesEtat.cs` est le seul fichier de `src/` à porter `Claude_` et
  `claude-code-sessions`, ce que vérifie `grep -rl` sur `src/`. 29-05 porte aussi le constat hors de l'arbre de
  l'app (overlay publié ou sonde WMI).
- **APP-06 n'est pas coché** dans REQUIREMENTS.md, comme le prévoit le plan : le constat de production relève de
  29-05.

## Self-Check: PASSED

- Fichiers vérifiés présents : `src/Chronos/Services/RacinesEtat.cs`, `tests/Chronos.Tests/RacinesEtatTests.cs`,
  `tests/Chronos.Tests/DeuxVuesAppDataTests.cs`, `docs/hooks-contract.md`, ce SUMMARY.
- Commits vérifiés dans `git log` : `e422deb`, `d3e8787`, `4c806da`, `428bf43`, `a0aad8c`.
