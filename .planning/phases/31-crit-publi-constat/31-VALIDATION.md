---
phase: 31
slug: crit-publi-constat
status: planned
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-26
---

# Phase 31 — Validation Strategy

> Carte **posée à la planification** (2026-09-26), à **remplir de valeurs mesurées** au fil des trois plans. Les colonnes « Attendu »
> viennent des plans ; les cases « Mesuré » restent vides tant qu'aucune exécution ne les a produites. **Ne rien y écrire qui n'ait été
> mesuré.** 31-01 et 31-02 tournent en parallèle (vague 1, arbres de travail isolés) et **ne modifient pas ce fichier** : 31-03 (vague 2)
> y reporte leurs valeurs, marquées « repris du 31-0N-SUMMARY ».
>
> **Pièges propres à cette phase.** (1) Deux vues d'AppData : tout relevé de fichier Chronos ou de l'app se fait HORS de l'arbre de l'app
> (Python par son alias, preuve `'Claude' not in os.listdir(APPDATA)` ; repli WMI) — un relevé depuis la session de l'agent voit la vue
> virtualisée. (2) L'agent ne lance jamais l'overlay (précédent 19-05) : le smoke de 31-02 passe par `--hook` avec une entrée vide, le
> premier lancement est fait par l'utilisateur en 31-03. (3) La réconciliation se constate dans le FICHIER (sauvegarde = état avant),
> jamais dans `chronos.log`, écrit avant elle. (4) La grâce de LUE-02 se VOIT (« En attente » au plus ~5,5 s) : elle se mesure et se fait
> juger, elle ne s'asserte pas « jamais ». (5) Garde vacueuse : chaque fragment gardé du nouveau document est prouvé rouge contre son
> absence (RED), et les mutations (d1)-(d4) prouvent que la garde lit bien le code et les renvois.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact 1.1.11, Microsoft.NET.Test.Sdk 17.11.1 — `tests/Chronos.Tests` (`net8.0-windows`) ; aucun `[WpfFact]` nouveau (aucune UI touchée) |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` (attributs `CheminSourcesChronos` → `src/Chronos`, `CheminDocsChronos` → `docs/` ; README dérivé : `docs/../README.md`) — aucun ajout |
| **Quick run command (31-01)** | `dotnet test Chronos.sln -c Debug --nologo -v q --filter "FullyQualifiedName~ContratAppBureauDocumenteTests\|FullyQualifiedName~ContratHooksDocumenteTests\|FullyQualifiedName~LibellesSessionsTests\|FullyQualifiedName~LecteurAppBureauTests\|FullyQualifiedName~LectureSeuleAppBureauTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~HorizonsSessionsTests"` |
| **Quick run command (31-02)** | `dotnet test Chronos.sln -c Debug --nologo -v q --filter "FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~VersionPublieeTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~ServicesLayerPurityTests\|FullyQualifiedName~NormalisationUniqueTests\|FullyQualifiedName~CompositionRootTests"` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Estimated runtime** | ~11 s de tests, ~25 s avec la compilation ; publication `dotnet publish` ~1-2 min (31-02, hors tests) |
| **Baseline d'entrée de phase** | **1143 verts / 0 échec** (fin de phase 30, deux exécutions, `5995fa5` ; phase 30 vérifiée 6/6 à `b54670f`) |
| **Cible de fin de phase** | **0 échec, 1152** = 1143 + 6 (31-01) + 3 (31-02). Égalité, pas plancher : tout écart, même positif, justifié nominativement au SUMMARY. 31-03 n'ajoute aucun test. |
| **Attendus indicatifs** | 31-01 : +5 (T1) +1 (T2) = **1149** isolé ; 31-02 : +3 (T1) = **1146** isolé ; **1152** après fusion de la vague 1 (mesuré en 31-03 T1, deux exécutions) |
| **Tests adaptés** | 1 — `LibellesSessionsTests.Aucun_ancien_libelle_ne_subsiste_a_l_ecran_ni_dans_le_contrat` : 9 → 11 fichiers surveillés (le nouveau document et le README décrivent les libellés) ; ÉTENDUE, pas assouplie. `ContratHooksDocumenteTests` : `TableEntre` et `SectionDe` passent `internal static`, aucune assertion touchée |
| **Renommages annoncés** | 0 |
| **Total mesuré après 31-01** | *(à reporter du 31-01-SUMMARY : 1148 après T1 ; 1149 deux fois après T2, arbre du plan)* |
| **Total mesuré après 31-02** | *(à reporter du 31-02-SUMMARY : 1146 deux fois, arbre du plan)* |
| **Total mesuré en fin de phase** | *(31-03 T1 : suite complète deux fois sur l'arbre fusionné — 1152 / 0 attendu)* |

---

## Sampling Rate

- **Après chaque tâche de code :** le filtre du `<verify>` de la tâche (commandes rapides ci-dessus).
- **Après chaque plan de la vague 1 :** suite complète, deux exécutions consécutives, dans l'arbre du plan (valeur isolée).
- **Après la vague 1 (fusion) :** suite complète deux fois en 31-03 T1 — 1152 / 0.
- **Tâches de constat (31-03) :** chaque tâche automatique a sa commande Python (structure et verdicts de `31-CONSTAT.md`, vue réelle
  assertée, ou état réconcilié de `~/.claude/settings.json`) ; aucune série de trois tâches sans vérification automatique (points de
  contrôle et relevés alternent).
- **Avant `/gsd:verify-work` :** suite complète verte deux fois ; les **sept contrôles de mutation** ci-dessous consignés (rouges nommés,
  sha256 identique après révocation) ; contrôles de publication consignés ; `31-CONSTAT.md` présent avec son verdict.
- **Latence maximale de retour :** ~25 s (suite complète avec compilation).

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 31-01-01 | 01 | 1 | VAL-02 | garde croisée document ↔ TEXTE du lecteur (D1-D5 : fichier, 14 champs dans les deux sens, 3 catégories, chemin / 24 h / partage tenus contre `RacinesEtat.cs`, `LecteurAppBureau.cs`, `HorizonsSessions`, section « non garanti » datée) + **mutations (d1), (d2), (d3)** | quick run 31-01 | ❌ W0 → créé par la tâche (`ContratAppBureauDocumenteTests.cs`, `docs/desktop-app-sessions.md`) | ⬜ à mesurer |
| 31-01-02 | 01 | 1 | VAL-02 | garde texte des renvois (D6 : §3 du contrat des hooks, `data-sources.md` §6, README ; la règle « lue » écrite une fois) + garde des libellés étendue à 11 fichiers + **mutation (d4)** | full suite (×2) | ✅ classes existantes étendues | ⬜ à mesurer |
| 31-02-01 | 02 | 1 | VAL-03 | intégration rapport (ligne « Version : » lue sur l'assembly) + cohérence csproj ↔ assembly (2 cas) + **mutations (v1), (v2), (v3)** | quick run 31-02 | ❌ W0 → créé par la tâche (`VersionPublieeTests.cs`) ; `DiagnosticServiceTests` +1 | ⬜ à mesurer |
| 31-02-02 | 02 | 1 | VAL-03 | contrôles shell de publication : csproj 3.2.0 (×4), 0 DLL, taille < 120 Mo, VersionInfo 3.2.0.0 / 3.2.0, md5 copie = sortie ≠ 3.1.0, `check-ignore`, smoke `--hook SessionStart` code 0, md5 `settings.json` inchangé, 0 processus résident ; commit de release | full suite (×2) + commandes de la tâche | n/a | ⬜ à mesurer |
| 31-03-01 | 03 | 2 | VAL-03 | relevé du temps 0 (vue réelle) + protocole commité AVANT le premier geste + suite fusionnée ×2 (1152) | `python -c …` (structure du constat, vue réelle) + full suite | ❌ W0 → `31-CONSTAT.md` créé par la tâche | ⬜ à mesurer |
| 31-03-02 | 03 | 2 | VAL-03 | **point de contrôle (a)** : l'utilisateur quitte la 3.1.0 et lance la 3.2.0 par l'Explorateur | manuel | — | ⬜ à mesurer |
| 31-03-03 | 03 | 2 | VAL-03 | contrôle de réconciliation dans le fichier (sauvegarde neuve, md5 = temps 0, égalité structurelle, 9 → 9, 8 + 1, 3 `gsd-*`) ; `Version : 3.2.0` dans `chronos.log` | `python -c …` (état réconcilié de `settings.json`) | — | ⬜ à mesurer |
| 31-03-04 | 03 | 2 | VAL-03 (critère 4) | **point de contrôle (b)** : L1, L2, L2b, L3, L4, Q | manuel | — | ⬜ à mesurer |
| 31-03-05 | 03 | 2 | VAL-03 (critère 4) | relevé (b) : fins de tour (transcripts), derniers focus, fichiers d'état (question en suspens), `treated.json`, causes du rapport | `python -c …` (six verdicts au §1) | — | ⬜ à mesurer |
| 31-03-06 | 03 | 2 | VAL-03 (critère 4) | **point de contrôle (c)** : V01-V12 | manuel | — | ⬜ à mesurer |
| 31-03-07 | 03 | 2 | VAL-03 (critère 4) | relevé (c), écarts, verdict, carte remplie | `python -c …` (douze résultats au §2, verdict au §4) | — | ⬜ à mesurer |

*Statut : ⬜ à mesurer · ✅ vert · ❌ rouge · ⚠️ instable*

### Contrôles de mutation (joués, rouges constatés, révoqués par sha256)

| Id | Plan / tâche | Mutation | Doit rougir | Rouges constatés |
|----|--------------|----------|-------------|------------------|
| (d1) | 31-01 / T1 | `"titleSource"` → `"titleOrigin"` dans `LecteurAppBureau.cs` | `La_table_documentee_liste_EXACTEMENT_les_champs_lus_par_le_lecteur` (nomme `titleOrigin` ET `titleSource`) | *(à mesurer)* |
| (d2) | 31-01 / T1 | ligne `isArchived` retirée de la table CHAMPS-LUS | `La_table_documentee_liste_EXACTEMENT_les_champs_lus_par_le_lecteur` (nomme `isArchived`) | *(à mesurer)* |
| (d3) | 31-01 / T1 | `"review_ready" =>` → `"ready" =>` dans le `switch` | `Les_categories_documentees_sont_celles_que_le_lecteur_reconnait` | *(à mesurer)* |
| (d4) | 31-01 / T2 | paragraphe de renvoi retiré du §3 de `hooks-contract.md` | `Les_documents_voisins_renvoient_au_contrat_de_l_app_et_la_regle_n_est_ecrite_qu_une_fois` | *(à mesurer)* |
| (v1) | 31-02 / T1 | `<InformationalVersion>` ≠ `<Version>` dans le csproj | `Les_quatre_proprietes_de_version_du_csproj_sont_coherentes` | *(à mesurer)* |
| (v2) | 31-02 / T1 | ligne `sb.AppendLine("Version : " + VersionEmbarquee());` retirée | `Le_rapport_dit_la_version_embarquee` | *(à mesurer)* |
| (v3) | 31-02 / T1 | `IncludeSourceRevisionInInformationalVersion` → `true` | `Les_quatre_proprietes_de_version_du_csproj_sont_coherentes` ; et, si le SDK ajoute « +sha », `L_assembly_compilee_porte_la_version_du_csproj` et `Le_rapport_dit_la_version_embarquee` | *(à mesurer)* |

### Contrôles de publication (31-02 T2 — à reporter du SUMMARY)

| Contrôle | Attendu | Mesuré |
|----------|---------|--------|
| csproj | `grep -cF "3.2.0"` = 4, `grep -cF "3.1.0"` = 0 | |
| Sortie de publication | 0 DLL ; `Chronos.exe` < 120 000 000 o (~77 Mo) | |
| VersionInfo | FileVersion 3.2.0.0 ; ProductVersion 3.2.0 | |
| md5 | copie racine = sortie ; ≠ `ab93b327…` (3.1.0) | |
| git | `check-ignore` : `/Chronos-v*.exe` ; `Chronos-v3.1.0.exe` toujours présent | |
| Smoke `--hook SessionStart` (stdin vide) | code 0 ; md5 `settings.json` identique ; 0 processus résident | |
| Commit de release | `release: Chronos 3.2.0 - milestone v1.7 << Lue ou non lue >>` ; csproj + `docs/publish.md` ; aucune étiquette | |

---

## Wave 0 Requirements

Pas de vague 0 séparée (précédent des phases 28 à 30) : chaque tâche de code est TDD et crée ses tests d'abord.

- [ ] `tests/Chronos.Tests/ContratAppBureauDocumenteTests.cs` — D1-D5 (31-01 T1, RED contre l'absence du document), D6 (31-01 T2)
- [ ] `tests/Chronos.Tests/VersionPublieeTests.cs` — cohérence csproj ↔ assembly (31-02 T1)
- [ ] `DiagnosticServiceTests` +1 — ligne de version (31-02 T1)
- [ ] `LibellesSessionsTests` — 11 fichiers surveillés (31-01 T2)
- [ ] `ContratHooksDocumenteTests` — `TableEntre` / `SectionDe` en `internal static` (31-01 T1, aucune assertion touchée)
- [ ] `.planning/phases/31-crit-publi-constat/31-CONSTAT.md` — squelette et protocole (31-03 T1, AVANT le premier geste)
- Aucun framework à installer.

---

## Manual-Only Verifications

Le protocole complet, geste par geste, est dans `31-03-PLAN.md` (tâches 2, 4 et 6) et recopié dans `31-CONSTAT.md` avant d'être joué.
L'utilisateur fait les gestes et lit l'écran ; l'agent relève en lecture seule, dans la vue réelle.

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| **(a) Réconciliation au premier lancement** : un seul overlay 3.2.0 (parent explorer) ; sauvegarde neuve, md5 = temps 0 ; fichier = sauvegarde + 9 remplacements `Chronos-v3.1.0.exe` → `Chronos-v3.2.0.exe` ; 8 hooks + statusLine ; 3 `gsd-*` intacts ; « v3.2 » aux réglages ; « Version : 3.2.0 » au rapport | VAL-03 (critères 2 et 3) | Seul l'utilisateur lance l'overlay, par l'Explorateur (vue réelle) ; l'agent ne lance rien (19-05) | 31-03 T2 (gestes) puis T3 (script de contrôle de la réconciliation) |
| **L1** — S1 regardée : « Réflexion » sous son titre | critère 4 (ligne 1) | Premier plan réel, app réelle, œil de l'utilisateur | 31-03 T4, gestes 1-2 |
| **L3** — S1 regardée finir : « En attente » au plus ~5,5 s, puis rien ; durée mesurée et jugée | critère 4 (ligne 3), LUE-02 | La grâce (2,5 s) + cycle (2 s) + cache (1 s) ne se voit qu'en vrai | 31-03 T4, geste 3 ; cause « sélectionnée au premier plan » au rapport |
| **L2** — S2 finie non regardée : « En attente » qui reste ; ouverte ⇒ disparaît sans clic ; délai | critère 4 (ligne 2), LUE-01 | Changement de focus réel, latence d'écriture de `lastFocusedAt` | 31-03 T4, gestes 4-6 |
| **L2b** — S3, Explorateur au premier plan : « En attente » qui reste ; alt-tab ⇒ disparaît | critère 4 (ligne 2), LUE-01, LUE-02 | Premier plan réel | 31-03 T4, gestes 7-9 |
| **L4** — une session lue qui se remet à travailler réapparaît « Réflexion », puis « En attente » | LUE-05, NET-03 | Enchaînement réel | 31-03 T4, gestes 11-12 |
| **Q** — une question `AskUserQuestion` non regardée : « En attente » au-dessus d'un tour fini | LIB-02, LIB-04 | Question réelle de Claude Code | 31-03 T4, gestes 13-15 |
| **V01** — galerie 8 styles : trois mots, titre long coupé, info-bulles | LIB-01, LIB-03, APP-02 | Jugement visuel (28-VALIDATION, 29-VALIDATION) | 31-03 T6, gestes 8-9 (`--sessions`, par Win+R) |
| **V02** — 9 thèmes à l'œil | LIB-03 | Contraste ; couvert mécaniquement par la matrice WPF 8 × 9 | 31-03 T6, geste 10 (facultatif) |
| **V03** — « Source app-bureau : trouvée » par l'overlay, « Fichiers d'état » des deux racines, jointures | APP-06, APP-04 | Rapport de l'exe réel, hors de l'arbre de l'app (29-VALIDATION) | 31-03 T6, geste 12 ; `chronos.log` en T3 |
| **V04** — cause de chaque masquée, section « Règle « lue » », aucun « un filtre non nommé » | LUE-03, LUE-04 | Rapport de l'exe réel sur les données réelles (30-VALIDATION) | 31-03 T4 geste 10 et T6 geste 12 |
| **V05** — borne de 160 DIP : titres et dossiers longs réels coupés proprement | APP-02 | Dossiers réels de 161 à 167 DIP (29-VALIDATION) | 31-03 T6, geste 11 |
| **V06** — info-bulle à `needs_action` long lisible | APP-03, APP-02 | Motif réel de l'app | 31-03 T6, geste 4 (opportuniste) |
| **V07** — `PermissionRequest` émis pour `AskUserQuestion` ? question vue ⇒ quitte le widget (D-30-08) ; réponse ⇒ « Réflexion » | LIB-02, LUE-01 | Comportement de Claude Code sur la version de l'utilisateur (28-VALIDATION) | 31-03 T5 (fichier d'état pendant la question) et T6, gestes 1-2 |
| **V08** — vraie question classée `blocked` par l'app | APP-03 | Classification faite par l'app, non provoquable à coup sûr (29-VALIDATION) | 31-03 T6, gestes 3-4 ; catégorie relevée en T7 |
| **V09** — accueil ou Chat au premier plan pendant une fin de tour | LUE-02 (limite écrite, Piège 1) | Sans UI Automation, rien ne distingue les vues de l'app (30-VALIDATION) | 31-03 T6, gestes 5-6 ; consigner, ne rien corriger |
| **V10** — latence d'écriture de `lastFocusedAt` | LUE-01 (Piège 11) | Un seul point relevé en phase 27 | 31-03 T5 (date d'écriture − `lastFocusedAt`) + délais de L2 / L2b |
| **V11** — réserve 16:23:51 → 19:51 (retour sans mise à jour de `lastFocusedAt`) | LUE-01, LUE-02 | Cas observé une fois, jamais reproduit (27-RELEVE) | 31-03 T6, geste 13 (passif, sur tout le constat) |
| **V12** — CLI Claude Code homonyme (`claude`) au premier plan | LUE-02 (limite, 30-02) | Dépend de l'usage de l'utilisateur | 31-03 T6, geste 7 (facultatif) ; processus relevés en T7 |

---

## Validation Sign-Off

- [x] Toutes les tâches automatiques ont une commande `<automated>` ; les points de contrôle humains alternent avec des relevés vérifiés
- [x] Continuité d'échantillonnage : jamais trois tâches consécutives sans vérification automatique
- [x] La vague 0 couvre toutes les références MISSING (créées par les tâches TDD elles-mêmes)
- [x] Aucun mode « watch »
- [x] Latence de retour < 30 s (tests) ; publication hors boucle de retour
- [x] `nyquist_compliant: true` posé dans l'en-tête

**Approval:** planifiée le 2026-09-26 — à signer par 31-03 T7 avec les valeurs mesurées.
