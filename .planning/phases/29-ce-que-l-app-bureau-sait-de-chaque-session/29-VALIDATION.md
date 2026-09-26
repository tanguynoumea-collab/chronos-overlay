---
phase: 29
slug: ce-que-l-app-bureau-sait-de-chaque-session
status: planned
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-25
---

# Phase 29 — Validation Strategy

> Carte **posée à la planification** (2026-09-25), à **remplir de valeurs mesurées** au fil des cinq plans. Les
> colonnes « Attendu » viennent des plans ; les colonnes « Mesuré » restent vides tant qu'aucune exécution ne les a
> produites. **Ne rien y écrire qui n'ait été mesuré** — une case remplie « d'après le plan » est exactement la faute
> que la doctrine de ce projet interdit.
>
> **Piège propre à cette phase (29-RESEARCH Q0.c.3).** Tout ce qui tourne depuis une session Claude Code — bash,
> PowerShell, `dotnet test` — voit la vue VIRTUALISÉE d'AppData (MSIX). Un constat « la racine existe » fait d'ici ne
> vaut RIEN pour l'overlay, lancé par `explorer.exe`. Les tests n'utilisent donc jamais les racines par défaut
> (racines temporaires injectées), et le constat de production passe par un processus créé HORS de l'arbre de l'app
> (sonde WMI, 29-05 tâche 3), puis par le diagnostic de l'overlay publié (phase 31).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`, `[Collection("XAML WPF")]`), Microsoft.NET.Test.Sdk 17.11.1 — `tests/Chronos.Tests` (`net8.0-windows`) |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` (attributs `CheminSourcesChronos`, `CheminDocsChronos` ; `InternalsVisibleTo Chronos.Tests` côté `src/Chronos/Chronos.csproj`) ; fixtures RÉELLES `tests/Chronos.Tests/TestData/DesktopAppSessions/*.json` (phase 27), chargées par `[CallerFilePath]` |
| **Quick run command** | `dotnet test Chronos.sln -c Debug --nologo -v q --filter "FullyQualifiedName~RacinesEtatTests\|FullyQualifiedName~DeuxVuesAppDataTests\|FullyQualifiedName~LecteurAppBureauTests\|FullyQualifiedName~LectureSeuleAppBureauTests\|FullyQualifiedName~MoniteurAppBureauTests\|FullyQualifiedName~ArbitrageSessionsTests\|FullyQualifiedName~InspectionSessionsTests\|FullyQualifiedName~AffichageSessionsTests\|FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~BalayageMagasinSessionsTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~CompositionRootTests\|FullyQualifiedName~NormalisationUniqueTests\|FullyQualifiedName~ServicesLayerPurityTests\|FullyQualifiedName~HorizonsSessionsTests\|FullyQualifiedName~ContratHooksDocumenteTests\|FullyQualifiedName~LibellesSessionsTests\|FullyQualifiedName~SessionStylesBindingTests"` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Estimated runtime** | ~11 s de tests, ~25 s avec la compilation (baseline 947 tests, 11 s — fin de phase 28) |
| **Baseline d'entrée de phase** | **947 verts / 0 échec** (fin de phase 28, HEAD ≈ `6fea32c`) |
| **Cible de fin de phase** | **0 échec, aucun test supprimé, aucun renommé.** Égalité, pas plancher : `total = total mesuré du plan précédent + cas ajoutés`, tout écart (même positif) justifié nominativement au SUMMARY. |
| **Attendus indicatifs** | 29-01 : +6 (T1) +11 (T2) +1 (T3) = **965** seul ; 29-02 : +20 (T1) +11 (T2) +4 (T3) = **982** seul ; **1000** après la vague 1 ; 29-03 : +9 (T1) +28 (T2) +2 (T3) = **1039** ; 29-04 : +9 (T1) +2 (T2) = **1050** seul ; 29-05 : +8 (T1) +2 (T2) +0 (T3) = **1049** seul ; **1060** en fin de phase |
| **Tests adaptés sans renommage** | 29-01 : `CompositionRootTests.Le_graphe_DI_resout_la_chaine_de_sessions` (miroir à deux racines), `GardesPerimetreTests.Le_demarrage_balaie_le_magasin_de_sessions_sur_le_dossier_du_moniteur` (littéral `.Directory` → `.Dossiers`, même intention), helper `BalayageMagasinSessionsTests.Balayeur` (`Dossiers`) ; 29-03 : `CompositionRootTests.Le_graphe_DI_resout_la_chaine_de_sessions` (lecteur branché) ; 29-04 : `SessionStylesBindingTests.Les_trois_mots_se_lisent_sur_les_8_styles_et_les_9_themes` (bulle lue par sa 1re ligne + assertion AJOUTÉE sur le motif), `Vm()` / `Monter` paramétrés (défauts inchangés) |
| **Renommages annoncés** | **0** |
| **Total mesuré après 29-01** | |
| **Total mesuré après 29-02** | |
| **Total mesuré après la vague 1** | |
| **Total mesuré après 29-03** | |
| **Total mesuré après 29-04** | |
| **Total mesuré après 29-05 (deux exécutions)** | |

---

## Sampling Rate

- **Après chaque tâche :** la commande rapide ci-dessus (ou le filtre du `<verify>` de la tâche).
- **Après chaque vague :** `dotnet test Chronos.sln -c Debug --nologo -v q`.
- **Avant `/gsd:verify-work` :** suite complète verte, **deux exécutions consécutives** (les `[WpfFact]` de 29-04 le
  justifient — précédent BAML 16-03), les **neuf contrôles de mutation** consignés, et `29-SONDE-APP06.txt` présent
  avec `%APPDATA%\Claude existe : False`.
- **Latence maximale de retour :** ~25 s (suite complète avec compilation).

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 29-01-01 | 01 | 1 | APP-06 | unit pur, arborescence temporaire (candidats, ordre, paquets `Claude_*`) | `dotnet test … --filter "FullyQualifiedName~RacinesEtatTests\|FullyQualifiedName~ServicesLayerPurityTests\|FullyQualifiedName~NormalisationUniqueTests\|FullyQualifiedName~GardesPerimetreTests"` | ❌ créé par la tâche (`RacinesEtatTests.cs`) | ⬜ pending |
| 29-01-02 | 01 | 1 | APP-06 | intégration moniteur / balayage / rapport sur deux racines + miroir DI + garde de câblage | `dotnet test … --filter "FullyQualifiedName~DeuxVuesAppDataTests\|FullyQualifiedName~BalayageMagasinSessionsTests\|FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~CompositionRootTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~InspectionSessionsTests\|FullyQualifiedName~HorizonsSessionsTests\|FullyQualifiedName~SessionsTests"` | ❌ créé (`DeuxVuesAppDataTests.cs`) + classes existantes étendues | ⬜ pending |
| 29-01-03 | 01 | 1 | APP-06 | garde croisée document ↔ code + **mutations M1, M2** | `dotnet test Chronos.sln -c Debug --nologo -v q` | ✅ classe existante étendue | ⬜ pending |
| 29-02-01 | 02 | 1 | APP-01 | unit pur sur les 6 fixtures RÉELLES (UTC exacts, champs inconnus, catégories inconnues, illisibles, BOM) | `dotnet test … --filter "FullyQualifiedName~LecteurAppBureauTests\|FullyQualifiedName~HorizonsSessionsTests\|FullyQualifiedName~NormalisationUniqueTests\|FullyQualifiedName~ServicesLayerPurityTests\|FullyQualifiedName~GardesPerimetreTests"` | ❌ créé (`RacineAppBureau.cs`, `LecteurAppBureauTests.cs`) | ⬜ pending |
| 29-02-02 | 02 | 1 | APP-01 | intégration racine temporaire : profondeur 2, bruit, 24 h, cache, troncature, doublons, épisode figé | `dotnet test … --filter "FullyQualifiedName~LecteurAppBureauTests"` | ❌ (même classe) | ⬜ pending |
| 29-02-03 | 02 | 1 | APP-05 | garde texte + écrivain en place + empreinte SHA-256 + **mutations (a), (b)** | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ créé (`LectureSeuleAppBureauTests.cs`) | ⬜ pending |
| 29-03-01 | 03 | 2 | APP-03 | unit pur (trois sources, 720 permutations, non-vacuité) + libellé de source + garde doc ↔ code + **mutation (d)** | `dotnet test … --filter "FullyQualifiedName~ArbitrageSessionsTests\|FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~ContratHooksDocumenteTests\|FullyQualifiedName~LibellesSessionsTests\|FullyQualifiedName~GardesPerimetreTests"` | ✅ classes existantes étendues | ⬜ pending |
| 29-03-02 | 03 | 2 | APP-03, APP-01, APP-02 | intégration moniteur RÉEL + lecteur RÉEL + fixtures (C1-C11, jointure du titre, épisode et traité, dégradation v1.6 exacte) + **mutations (e), (f)** | `dotnet test … --filter "FullyQualifiedName~MoniteurAppBureauTests\|FullyQualifiedName~AffichageSessionsTests\|FullyQualifiedName~InspectionSessionsTests\|FullyQualifiedName~ArbitrageSessionsTests\|FullyQualifiedName~TreatedSessionsTests\|FullyQualifiedName~GesteTraiteTests\|FullyQualifiedName~HorizonsSessionsTests\|FullyQualifiedName~GardesPerimetreTests"` | ❌ créé (`MoniteurAppBureauTests.cs`) | ⬜ pending |
| 29-03-03 | 03 | 2 | APP-01, APP-03 | miroir DI + gardes texte (câblage `appBureau:`, titre après l'arbitrage) | `dotnet test Chronos.sln -c Debug --nologo -v q` | ✅ classes existantes étendues | ⬜ pending |
| 29-04-01 | 04 | 3 | APP-02 | unit (producteur : nom, info-bulle, motif) + ViewModel + galerie | `dotnet test … --filter "FullyQualifiedName~AffichageSessionsTests\|FullyQualifiedName~LibellesSessionsTests\|FullyQualifiedName~SessionStylesBindingTests"` | ✅ classes existantes étendues | ✅ green (`daffd83` RED 11/56 → `62482cb`, 56/56) |
| 29-04-02 | 04 | 3 | APP-02 | WPF 8 styles × 9 thèmes (titre long, largeur +1 DIP, 160 DIP) + garde texte + **mutation `MaxWidth`** | `dotnet test Chronos.sln -c Debug --nologo -v q` | ✅ classes existantes étendues | ✅ green (`d8ac222` RED 2 → `21a144a` ; « +1 DIP » compté avec la latitude de l'ellipse, référence PROJET OLYMPE DATAMIND 161,3 DIP ; mutation `MaxWidth` : 2 rouges, sha256 identique ; 1062 / 0 ×2 en fin de phase) |
| 29-05-01 | 05 | 3 | APP-04 | intégration rapport (trouvée / absente / non branchée, lignes par session) + garde OBS-01 étendue | `dotnet test … --filter "FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~NormalisationUniqueTests\|FullyQualifiedName~ServicesLayerPurityTests"` | ✅ classes existantes étendues | ⬜ pending |
| 29-05-02 | 05 | 3 | APP-05 | garde texte (chemin de l'app en un seul fichier, résolveur sans écriture) + **mutation (c)** | `dotnet test … --filter "FullyQualifiedName~LectureSeuleAppBureauTests"` | ✅ (créée en 29-02) | ⬜ pending |
| 29-05-03 | 05 | 3 | APP-06 | constat RÉEL hors de l'arbre de l'app (processus WMI, DLL du commit livré), lecture seule | `test -s .planning/phases/29-ce-que-l-app-bureau-sait-de-chaque-session/29-SONDE-APP06.txt && grep -c "Claude existe : False" .planning/phases/29-ce-que-l-app-bureau-sait-de-chaque-session/29-SONDE-APP06.txt` | ❌ créé (`29-SONDE-APP06.txt`) | ⬜ pending |

*Status : ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Contrôles de mutation (joués, rouges constatés, révoqués par sha256)

| Id | Plan / tâche | Mutation | Doit rougir | Rouges constatés |
|----|--------------|----------|-------------|------------------|
| M1 | 29-01 / T3 | le moniteur ne lit que la première racine | `Le_moniteur_lit_les_fichiers_d_etat_des_deux_racines`, `Les_fichiers_perimes_sont_comptes_sur_toutes_les_racines` | |
| M2 | 29-01 / T3 | le balayage ne parcourt que la première racine | `Chaque_racine_est_balayee_et_l_attestation_n_est_lue_qu_une_fois` | |
| (a) | 29-02 / T3 | une écriture ajoutée dans `Lire` | garde texte du lecteur, `Trois_lectures_laissent_la_racine_identique_octet_pour_octet` | |
| (b) | 29-02 / T3 | partage `FileShare.Read` | garde texte, `Un_ecrivain_en_place_ouvert_n_empeche_pas_la_lecture`, `Notre_poignee_ne_bloque_ni_l_ecriture_en_place_ni_la_suppression` | |
| (d) | 29-03 / T1 | `AppBureau` déclarée après `Transcript` | ordre des sources, C4, permutation à trois sources | |
| (e) | 29-03 / T2 | question datée par la `lastActivityAt` COURANTE | `Une_question_marquee_traitee_ne_revient_pas_quand_l_activite_de_fond_avance` | |
| (f) | 29-03 / T2 | dépôt sans la condition « connue d'une autre source » | `C7_sans_autre_source_la_question_de_l_app_ne_cree_aucune_ligne` | |
| MaxWidth | 29-04 / T2 | `MaxWidth="160"` retiré de Pastilles | `Un_titre_long_est_coupe_sans_elargir_le_widget…`, `Le_nom_est_borne_a_160_dans_Pastilles_et_Marge` | |
| (c) | 29-05 / T2 | le littéral `claude-code-sessions` dans DiagnosticService.cs | `Le_chemin_de_l_app_bureau_n_existe_que_dans_le_resolveur_de_racines` | |

### Mesures réelles (29-05 T3, sonde hors de l'arbre)

| Grandeur | Attendu (recherche) | Mesuré |
|---|---|---|
| `%APPDATA%\Claude existe` vu par la sonde | False | |
| Racine d'état du paquet : présente, nombre de fichiers | présente, ≥ 0 | |
| `PremiereExistante(SessionsAppBureau)` | `…\Packages\Claude_…\LocalCache\Roaming\Claude\claude-code-sessions` | |
| Fichiers énumérés / récents / avec métadonnées | ~138-142 / ~18 / ~18 | |
| `Lire` froid ; chaud médiane / p90 / max | ~96 ms ; 0,73 / 0,90 / 1,06 ms (seuil 5 ms) | |
| `Inspecter` chaud médiane / p90 | ≈ 26-28 ms (phase 28, seuil 50 ms) | |
| Sessions visibles / jointes à un titre | — | |
| Dates d'écriture `archived.json`, `treated.json`, `settings.json` avant / après | identiques | |

---

## Wave 0 Requirements

**Pas de vague 0 séparée — et c'est voulu** (précédent de la phase 28). Chaque tâche de code est TDD (`tdd="true"`) : elle
écrit ses tests d'abord, constate le rouge contre des SQUELETTES compilables (précédent 18-01 : un rouge de compilation ne
dit rien), puis livre le code, dans la même tâche. Les manques relevés par la recherche (« Wave 0 Gaps ») sont couverts ainsi :

- [ ] `tests/Chronos.Tests/RacinesEtatTests.cs` — résolution des racines par candidats (29-01, tâche 1)
- [ ] `tests/Chronos.Tests/DeuxVuesAppDataTests.cs` — moniteur à deux racines (29-01, tâche 2)
- [ ] `tests/Chronos.Tests/RacineAppBureau.cs` — helper partagé : fixtures réelles, racine `<org>\<user>` temporaire, dérivation TEXTUELLE (29-02, tâche 1)
- [ ] `tests/Chronos.Tests/LecteurAppBureauTests.cs` — interprétation, énumération, cache, troncature, épisode (29-02, tâches 1 et 2)
- [ ] `tests/Chronos.Tests/LectureSeuleAppBureauTests.cs` — les trois gardes d'APP-05 (29-02, tâche 3 ; 29-05, tâche 2)
- [ ] extension `ArbitrageSessionsTests` — helper `AppBureau`, corpus à trois sources (29-03, tâche 1)
- [ ] `tests/Chronos.Tests/MoniteurAppBureauTests.cs` — C1-C11, jointure, dégradation (29-03, tâche 2)
- [ ] extensions `DiagnosticServiceTests`, `GardesPerimetreTests`, `CompositionRootTests`, `ContratHooksDocumenteTests`,
      `BalayageMagasinSessionsTests`, `AffichageSessionsTests`, `LibellesSessionsTests`, `SessionStylesBindingTests`
- Aucun framework à installer ; les six fixtures réelles existent (`TestData/DesktopAppSessions`, phase 27) et ne sont jamais réécrites.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| La racine de PRODUCTION est trouvée par l'overlay lui-même : « Source app-bureau : trouvée — %LOCALAPPDATA%\Packages\Claude_…\LocalCache\Roaming\Claude\claude-code-sessions » et une ligne « Fichiers d'état (…) » pour la racine du paquet ET pour la vue réelle | APP-06, APP-04 | L'agent ne lance ni n'arrête l'overlay ; un constat fait depuis Claude Code voit la vue virtualisée (Q0.c.3). La sonde WMI de 29-05 (tâche 3) en est l'équivalent automatisé au niveau de la phase ; la confirmation dans l'exe publié reste humaine | Phase 31, exe 3.2.0 publié et lancé normalement (explorer / démarrage) : clic droit → « Diagnostic… » ; lire la section « Source app-bureau » et les lignes « Fichiers d'état » ; comparer « Jointures » et les titres aux lignes du widget |
| Galerie 8 styles × 9 thèmes : le titre long (`api-migration`) est coupé proprement par une ellipse (Pastilles, Marge, Jetons, Annonciateur), la largeur des tuiles ne change pas ; l'info-bulle dit « titre — dossier — mot », et celle d'`overlay` montre « permission demandée » en seconde ligne | APP-02 | Jugement visuel ; l'agent ne lance pas la galerie. La matrice WPF 8 × 9 (29-04) couvre les tailles et les textes, pas l'œil | `Chronos.exe --sessions` ; survoler `overlay` et le titre long dans chaque tuile ; changer de thème (9) ; vérifier ellipse, alignement, lisibilité de l'info-bulle à deux lignes. **À faire en phase 31** (29-04 : galerie non lancée par l'agent). Juger aussi : quatre dossiers réels de 161,3 à 166,8 DIP (OLYMPE DATAMIND, TEXTURE MANAGER, OLYMPE SENTINELLE, SMART NEWSLETTER) désormais coupés par la borne de 160 ; largeur d'une info-bulle à `needs_action` long (aucun retour à la ligne) |
| Une VRAIE question classée `blocked` par l'app s'affiche « En attente », au rang des questions, avec son motif en info-bulle ; elle repasse « Réflexion » dès la réponse | APP-03 | La classification de fin de tour est faite par l'app et ne se provoque pas à la demande ; C1-C11 la couvrent en test sur la fixture RÉELLE | Phase 31 : quand une session de l'app finit un tour sur une question (le rapport « Diagnostic… » dit « fin de tour : question posée »), vérifier la ligne du widget et son info-bulle, puis répondre et vérifier « Réflexion » |

---

## Validation Sign-Off

- [x] Toutes les tâches ont une vérification `<automated>` (la tâche 29-05-03 : présence et contenu du fichier de constat)
- [x] Continuité d'échantillonnage : aucune suite de trois tâches sans vérification automatique
- [x] Pas de vague 0 séparée : chaque manque est couvert par une tâche TDD nommée ci-dessus
- [x] Aucun mode « watch »
- [x] Latence de retour < 30 s
- [x] `nyquist_compliant: true` posé dans le frontmatter

**Approval:** planifié le 2026-09-25 — valeurs mesurées à remplir à l'exécution
