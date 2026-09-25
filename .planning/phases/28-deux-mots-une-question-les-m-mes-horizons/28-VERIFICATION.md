---
phase: 28-deux-mots-une-question-les-m-mes-horizons
verified: 2026-09-25T20:17:16Z
status: passed
score: 5/5 critères ROADMAP vérifiés — 5/5 exigences (LIB-01..04, SIL-01) satisfaites
sha_verifie: 6fea32c
sha_fin_de_phase_28: 5b9b767
sha_entree_de_phase: 5f5c3cb
suite:
  total: 947
  echecs: 0
  executions: 2
  duree: "10 s / 7 s"
mutations_rejouees:
  - id: "l'arbitrage relit l'ordre d'écran (Departager remplace RangArbitrage par AffichageSessions.Urgence)"
    attendu: "GardesPerimetreTests.L_arbitrage_ne_lit_pas_l_ordre_d_ecran rouge, ArbitrageSessionsTests.Remonter_la_deduction_a_l_ecran_ne_change_aucun_arbitrage rouge, la ligne (Working, WaitingDeduced) de Le_rang_d_arbitrage_reste_celui_de_la_phase_24 rouge"
    mesure: "échec : 3, réussite : 24, total : 27 — les trois cas nommés, à l'identique de ce que 28-02-SUMMARY.md rapporte pour la même mutation. Révoquée : sha256 f898d386636217dff315d9ab295997048e732b95fc60996ccb3266d8a568b85f identique avant/après, git diff --stat et git status --porcelain vides, suite rejouée verte (27/27)"
avertissements: []
human_verification:
  - test: "Galerie --sessions : les trois mots sur les 8 tuiles, « En attente ? » atténuée mais lisible, plus aucun fantôme, sous-titre à jour ; les 9 thèmes à l'œil"
    expected: "Rendu visuel correct sur les 8 styles et les 9 thèmes"
    why_human: "Rendu visuel ; l'agent ne lance ni l'overlay ni la galerie (une instance Chronos-v3.1.0.exe tourne). Le test WPF 8×9 (Les_trois_mots_se_lisent_sur_les_8_styles_et_les_9_themes) couvre mécaniquement mots, info-bulles, troncature et tailles, pas l'œil. Déjà reporté à la phase 31 par 28-VALIDATION.md — non bloquant pour cette phase."
  - test: "PermissionRequest part-il bien pour AskUserQuestion côté Claude Code réel ?"
    expected: "Pendant qu'une question AskUserQuestion est affichée dans l'app, le fichier d'état de hook porte activity=WaitingAttention, reason=PermissionRequest"
    why_human: "Fait externe MEDIUM, dépend du comportement de Claude Code sur la version de l'utilisateur ; non observable sans une question réelle à l'écran. Les deux issues d'arbitrage (avec/sans PermissionRequest) sont écrites et testées en pur (Sans_PermissionRequest_le_battement_d_une_question_gagne_par_fraicheur, Avec_PermissionRequest_la_question_reste_une_attente). Déjà reporté à la phase 31 par 28-VALIDATION.md — non bloquant pour cette phase."
---

# Phase 28 : Deux mots, une question, les mêmes horizons — Rapport de vérification

**Goal (ROADMAP) :** le widget ne parle plus qu'en deux mots — **« Réflexion »** ou **« En attente »** —, et
**« En attente ? »** quand l'attente est déduite du silence ; une question posée par Claude est une attente et
non un travail ; et une session connue par son seul transcript vit selon les mêmes horizons que les autres au
lieu de disparaître en silence au bout de quinze minutes (trou §9.1 de l'audit v1.6).

**SHA vérifié (arbre de travail) :** `6fea32c` — **fin réelle de la phase 28 :** `5b9b767` (le commit qui suit,
`6fea32c`, est `docs(phase-29): add validation strategy`, aucune touche au code de la phase 28) — **SHA d'entrée
de phase :** `5f5c3cb`
**Statut :** **passed** — **5/5 critères ROADMAP**, **5/5 exigences** (LIB-01, LIB-02, LIB-03, LIB-04, SIL-01)
**Vérification :** initiale (aucun `28-VERIFICATION.md` antérieur)

---

## 0. Ce qui a été mesuré, pas lu

Aucun chiffre de SUMMARY n'a été repris tel quel : chaque total ci-dessous sort d'une exécution jouée par le
vérificateur.

| Mesure | Commande | Résultat |
|---|---|---|
| Suite, 1re exécution | `dotnet test Chronos.sln --nologo -v q` | **947 / 0 échec / 10 s** |
| Suite, 2e exécution | idem | **947 / 0 échec / 7 s** |
| Gardes ciblées (10 classes : `GardesPerimetreTests`, `ServicesLayerPurityTests`, `NormalisationUniqueTests`, `ContratHooksDocumenteTests`, `GardesDoctrineTests`, `HorizonsSessionsTests`, `LibellesSessionsTests`, `TranscriptQuestionTests`, `TranscriptInstantSignalTests`, `ArbitrageSessionsTests`) | `--filter` ciblé | **85 / 0 échec** |
| Classes comportementales/gardes restantes (`GardesDoctrineTests`, `CompositionRootTests`, `TreatedSessionsTests`, `GesteTraiteTests`, `BalayageMagasinSessionsTests`, `InspectionSessionsTests`, `SessionStylesBindingTests`, `DiagnosticServiceTests`, `AffichageSessionsTests`) | `--filter` ciblé | **132 / 0 échec** |
| Mutation « l'arbitrage relit l'ordre d'écran » (point de vigilance 3) | `Departager` : `RangArbitrage(...)` → `AffichageSessions.Urgence(...)` sur les deux opérandes du rang 3 | **échec : 3, réussite : 24, total : 27** (`GardesPerimetreTests`, `ArbitrageSessionsTests`) |
| Révocation de la mutation | `sha256sum src/Chronos/Services/ArbitrageSessions.cs` avant/après | **`f898d386636217dff315d9ab295997048e732b95fc60996ccb3266d8a568b85f`** identique ; `git diff --stat` et `git status --porcelain` **vides** ; suite rejouée **27 / 0 échec** |
| `git status --porcelain` (dépôt entier, après vérification) | — | **seul `?? Chronos-v3.1.0.exe`** (préexistant, non suivi, hors périmètre) |
| Commits cités par les 4 SUMMARY | `git cat-file -e <sha>` ×9 (`8542274`, `243a535`, `147f41a`, `24a0957`, `bf63366`, `fc9c99d`, `62d6a0c`, `331a33e`, `88365e2`) | **les 9 existent** dans l'historique |

Le total mesuré (**947**) est exactement celui que `28-VALIDATION.md` et les quatre SUMMARY annoncent en sortie
de phase (896 baseline + 16 + 12 + 11 + 12 = 947), et l'attendu de `28-04-PLAN.md` (`<verification>` : « 0 échec,
947 »). Aucun test n'a été supprimé ni renommé sans que le SUMMARY correspondant ne le documente nommément.

---

## 1. Verdict par critère du ROADMAP

| # | Critère | Verdict | Ce qui le prouve — **mécaniquement** |
|---|---|---|---|
| 1 | **Deux mots et un point d'interrogation, partout** (LIB-01, LIB-03) | ✅ **VÉRIFIÉ** | `AffichageSessions.cs:26-28` — trois constantes exactes `Reflexion="Réflexion"`, `EnAttente="En attente"`, `EnAttenteDeduite="En attente ?"`. `Etat(a)` (`:68-74`) n'en rend jamais d'autres pour un état visible ; `Unknown` (aucune ligne, `AUneLigne`) garde « indéterminé » pour le SEUL rapport. **Grep sur les 9 fichiers de production** (`docs/hooks-contract.md`, `SessionStyles.xaml`, `SessionsWindow.xaml`, `SessionsGalleryWindow.xaml`, `SessionsController.cs`, `SessionsViewModel.cs`, `SessionsPreviewViewModel.cs`, `AffichageSessions.cs`, `SessionHookInstaller.cs`) pour `« à toi »`, `« tour fini »`, `« en cours »`, `` `inconnu` ``, `à toi ? déduit`, `Text="  en attente"` : **0 occurrence, dans les 9 fichiers**. `ToolTip="{Binding Infobulle}"` = **8**, `ToolTip="{Binding Project}"` = **0** (info-bulle sur les 8 gabarits). `IsGhost` : **0** occurrence dans `SessionStyles.xaml`, `SessionsViewModel.cs`, `SessionsPreviewViewModel.cs` — le fantôme n'existe plus. |
| 2 | **Une question n'est pas une réflexion** (LIB-02) | ✅ **VÉRIFIÉ** | `TranscriptSessionSource.Classify` (`:125-131`) : `DernierOutil(o) switch { null => WaitingTurn, "AskUserQuestion" => WaitingAttention, _ => Working }` — comparaison **ordinale et exacte**, aucune liste extensible. `reason = "AskUserQuestion"` posé comme fait observé (`:145`). 8 tests dans `TranscriptQuestionTests.cs` sur **fixtures RÉELLES** (`tests/Chronos.Tests/TestData/TranscriptQuestion/question-en-suspens.jsonl`, `question-repondue.jsonl`) : `Une_question_sans_reponse_est_une_attente_au_rang_des_questions`, `La_reponse_ecrite_rend_la_reflexion`, `Le_nom_d_outil_se_compare_exactement` (`[Theory]` sur `askuserquestion`/`AskUserQuestion2`/`AskUser`), `Un_appel_parallele_retombe_sur_le_comportement_v16`. |
| 3 | **L'ordre dit l'urgence, et ne touche pas l'arbitrage** (LIB-03, LIB-04) | ✅ **VÉRIFIÉ** | `AffichageSessions.Urgence` (`:38-45`) = ordre d'ÉCRAN seul : Attention 0, Turn 1, **Deduced 2** (devant Working 3), Unknown 4. `ArbitrageSessions.RangArbitrage` (`ArbitrageSessions.cs:149-155`), **private**, figé aux valeurs de la phase 24 (Attention 0, Turn 1, Working 2, Deduced/Unknown 3 ex æquo) ; `Departager` (`:135-136`) l'appelle, **0 occurrence** de `AffichageSessions.Urgence(` dans `ArbitrageSessions.cs`. **Mutation rejouée par le vérificateur** (recâbler `Departager` sur `AffichageSessions.Urgence`) → **3 rouges nommés** (`GardesPerimetreTests.L_arbitrage_ne_lit_pas_l_ordre_d_ecran`, `ArbitrageSessionsTests.Remonter_la_deduction_a_l_ecran_ne_change_aucun_arbitrage`, la ligne `(Working, WaitingDeduced)` de `Le_rang_d_arbitrage_reste_celui_de_la_phase_24`) — **révoquée**, sha256 identique, diff vide, suite verte. |
| 4 | **Le trou §9.1 est refermé** (SIL-01) | ✅ **VÉRIFIÉ** | `HorizonsSessionsTests.cs` : 5 scénarios avec un VRAI `SessionMonitor` + une VRAIE `TranscriptSessionSource`, racine temporaire, horloge injectée — `Transcript_seul_Working_muet_depuis_25_min_dit_En_attente_interrogatif`, `Transcript_seul_Working_depuis_19_min_dit_encore_Reflexion`, `Transcript_seul_en_tour_fini_depuis_3_h_dit_En_attente`, `Transcript_seul_de_7_h_59_est_encore_lu`, `Transcript_seul_de_8_h_01_est_absent`. `TranscriptSessionSource.cs:72,82` compare désormais à `HorizonsSessions.Abandon` (8 h, `<=`), plus aucune trace d'`ActiveWindow` (15 min). |
| 5 | **Les horizons ne peuvent plus diverger en silence** (SIL-01, LIB-01) | ✅ **VÉRIFIÉ** | `HorizonsSessions.cs` (créé) : `Silence`=20 min, `Abandon`=8 h, `RetentionTraitees`=24 h, `ExpirationEtat`=72 h, en un seul type. `SessionMonitor.AppliquerSilence` (`:154-157`), **privée**, appelée à `:85` (transcripts) et `:105` (hooks) — **avant** `ArbitrageSessions.Trancher` (`:108`) pour les deux sources. `TranscriptSessionSource.cs` : `grep -c WaitingDeduced` = **0** — la source ne déduit rien. `docs/hooks-contract.md` §3 nomme les quatre seuils (`HorizonsSessions.Silence`, `.Abandon`, `.RetentionTraitees`, `.ExpirationEtat`), le paragraphe D-28-01 et « Les mêmes horizons pour tous ». `HorizonsSessionsTests.La_chaine_des_horizons_tient` et `Les_quatre_fichiers_lisent_le_type_unique` sont vertes dans la suite mesurée (85/0 sur les 10 classes de garde). |

**Score : 5/5.**

---

## 2. Points de vigilance, un par un

### (1) Les trois chaînes exactes sont les SEULES affichables — vérifié par grep sur les 9 fichiers de production

`AffichageSessions.cs:26-28` déclare les trois constantes ; `Etat` (`:68-74`) ne rend jamais un cinquième mot
pour un état visible. Grep, sur les **9 fichiers** listés par `LibellesSessionsTests` (le contrat, `SessionStyles.xaml`,
les deux fenêtres XAML, `SessionsController.cs`, les deux ViewModels, `AffichageSessions.cs`, `SessionHookInstaller.cs`),
pour les motifs interdits (`« à toi »`, `« tour fini »`, `« en cours »`, `` `inconnu` ``, `à toi ? déduit`,
`Text="  en attente"`, et leurs variantes avec/sans guillemets) : **0 occurrence, dans les 9 fichiers, sans
exception**. `Unknown` conserve « indéterminé » (`AffichageSessions.cs:73`), mais **uniquement lu par le rapport
de diagnostic** (`DiagnosticService.cs:602`) — jamais par le widget, puisque `AUneLigne(Unknown)` est faux et que
la session est déjà masquée en amont (point 2).

### (2) `Unknown` masqué dans le moniteur, pas dans le ViewModel — vérifié

`SessionMonitor.cs:135` : `if (!AffichageSessions.AUneLigne(s.Activity)) { masquees.Add(new SessionMasquee(s,
MotifMasquage.Indeterminee)); continue; }` — **dans `Inspecter`**, après le filtre « archivée » puis « traitée »
(l'ordre est documenté au commentaire `:120-126`), avant que `Visibles` ne soit construit. `SessionsViewModel.cs`
: **0 occurrence** de `Unknown`, `Indeterminee` ou `AUneLigne` — le ViewModel ne voit jamais de session
`Unknown`, il ne pourrait donc pas la filtrer lui-même même s'il le voulait. `DiagnosticService.cs:602` porte le
libellé du motif (`"état indéterminé (signal illisible) — aucune ligne dans le widget"`).

### (3) L'arbitrage est découplé de l'ordre d'écran — mutation rejouée par le vérificateur, puis révoquée

`ArbitrageSessions.RangArbitrage` (`:149-155`) est **`private static`**, figé aux valeurs de la phase 24, et
`Departager` (`:135-136`) l'appelle au rang 3 au lieu d'`AffichageSessions.Urgence`. `grep -c
"AffichageSessions.Urgence(" src/Chronos/Services/ArbitrageSessions.cs` = **0**.

**Mutation jouée par le vérificateur** (pas seulement relue au SUMMARY) : remplacement des deux appels
`RangArbitrage(...)` par `AffichageSessions.Urgence(...)` dans `Departager`, puis
`dotnet test --filter "FullyQualifiedName~ArbitrageSessionsTests|FullyQualifiedName~GardesPerimetreTests"` :

```
échec : 3, réussite : 24, total : 27
  GardesPerimetreTests.L_arbitrage_ne_lit_pas_l_ordre_d_ecran [FAIL]
  ArbitrageSessionsTests.Remonter_la_deduction_a_l_ecran_ne_change_aucun_arbitrage [FAIL]
  ArbitrageSessionsTests.Le_rang_d_arbitrage_reste_celui_de_la_phase_24(a: Working, b: WaitingDeduced, gagnant: Working) [FAIL]
```

Les trois noms sont **exactement** ceux que `28-02-SUMMARY.md` rapporte pour la même mutation (« Mutation n° 2 »).
**Révocation** : fichier restauré depuis la sauvegarde faite avant mutation,
`sha256sum src/Chronos/Services/ArbitrageSessions.cs` = `f898d386636217dff315d9ab295997048e732b95fc60996ccb3266d8a568b85f`
**identique avant et après**, `git diff --stat -- src/Chronos/Services/ArbitrageSessions.cs` et
`git status --porcelain -- src/Chronos/Services/ArbitrageSessions.cs` **vides**, suite rejouée **27 / 0 échec**.

### (4) `AskUserQuestion` sans `tool_result` ⇒ attente — vérifié sur fixtures réelles

`TranscriptSessionSource.Classify` (`:125-131`) : `"AskUserQuestion" => SessionActivity.WaitingAttention`, motif
posé comme fait observé (`:145`). Les fixtures `tests/Chronos.Tests/TestData/TranscriptQuestion/question-en-suspens.jsonl`
et `question-repondue.jsonl` (existent, lignes réelles anonymisées de la session `939eb30a`) alimentent
`TranscriptQuestionTests.cs` (8 tests, dont `Une_question_sans_reponse_est_une_attente_au_rang_des_questions` et
`La_reponse_ecrite_rend_la_reflexion`), tous verts dans la suite mesurée.

### (5) `HorizonsSessions` unique + gardes de chaîne et de câblage, règle de silence en UNE fonction

`HorizonsSessions.cs` (nouveau fichier, 4 constantes). `SessionMonitor.AppliquerSilence` (`:154-157`) est la
**seule** fonction qui écrit `SessionActivity.WaitingDeduced` (`grep -c "Activity = SessionActivity.WaitingDeduced"
src/Chronos/Services/SessionMonitor.cs` = 1, confirmé lors de la lecture du fichier), appelée aux deux points de
collecte (`:85` transcripts, `:105` hooks), **avant** `ArbitrageSessions.Trancher` (`:108`). `TranscriptSessionSource.cs`
ne contient **aucune** occurrence de `WaitingDeduced` — la source ne déduit rien elle-même, conformément à la
doctrine écrite en XML-doc. `TreatedStore.cs` et `BalayageMagasinSessions.cs` lisent `HorizonsSessions.RetentionTraitees`
/ `.ExpirationEtat` (ce dernier en alias public conservé). `ActiveWindow` = **0** occurrence dans `TranscriptSessionSource.cs`
(remplacée par `HorizonsSessions.Abandon`, `:72` et `:82`).

### (6) Datation d'un transcript par le `timestamp` du dernier message significatif — vérifié

`TranscriptSessionSource.Classify` (`:137`) : `instant = Horodatage(o);` posé à **chaque** ligne significative
(`assistant` et `user`), donc c'est la **dernière** qui l'emporte ; `updatedAt = instant is { } h && h <= ecriture
? h : ecriture` (`:153`) — borné par l'écriture, repli sur elle si absent/illisible. `Horodatage` (`:180-183`)
passe par le point unique `UsageNormalization.InstantDepuisIso` (garde HDR-05). XML-doc de tête (`:32-42`)
documente le fait mesuré du 2026-09-25 16:58:20 qui a motivé la décision D-28-01.

### (7) Gardes existantes non assouplies — vérifié par exécution, pas par lecture du SUMMARY

Les cinq classes de garde citées par le point de vigilance de l'orchestrateur — `GardesPerimetreTests`,
`ServicesLayerPurityTests`, `NormalisationUniqueTests`, `ContratHooksDocumenteTests`, `GardesDoctrineTests` — ont
été rejouées explicitement par le vérificateur, avec neuf autres classes touchées par la phase
(`HorizonsSessionsTests`, `LibellesSessionsTests`, `TranscriptQuestionTests`, `TranscriptInstantSignalTests`,
`ArbitrageSessionsTests`, `CompositionRootTests`, `TreatedSessionsTests`, `GesteTraiteTests`,
`BalayageMagasinSessionsTests`, `InspectionSessionsTests`, `SessionStylesBindingTests`, `DiagnosticServiceTests`,
`AffichageSessionsTests`) : **85 / 0** puis **132 / 0**, aucune régression, aucun test manquant par rapport au
compte que les SUMMARY annoncent (72 combinaisons WPF pour `SessionStylesBindingTests`, 15/15 pour
`TreatedSessionsTests`, etc.).

---

## 3. Couverture des exigences

| Exigence | Requis par plan | Verdict | Preuve nommée |
|---|---|---|---|
| **LIB-01** | 28-02, 28-03 | ✅ **SATISFAITE** | `AffichageSessions.Etat` à trois mots (`:68-74`), `AUneLigne(Unknown) == false`, masquage moniteur (`SessionMonitor.cs:135`) ; `LibellesSessionsTests.Aucun_ancien_libelle_ne_subsiste_a_l_ecran_ni_dans_le_contrat`, `AffichageSessionsTests.Seul_l_etat_indetermine_n_a_pas_de_ligne` |
| **LIB-02** | 28-01 | ✅ **SATISFAITE** | `TranscriptSessionSource.Classify` (`AskUserQuestion => WaitingAttention`) ; `TranscriptQuestionTests` (8 tests, fixtures réelles) |
| **LIB-03** | 28-02, 28-03 | ✅ **SATISFAITE** | Producteur unique `AffichageSessions` (Etat, Urgence, EstUneAttente partagés widget/rapport/galerie) ; `AffichageSessionsTests.Le_producteur_ne_connait_que_trois_mots_visibles`, `ContratHooksDocumenteTests.Le_paragraphe_3_affiche_les_libelles_du_producteur`, `SessionStylesBindingTests.Les_trois_mots_se_lisent_sur_les_8_styles_et_les_9_themes` |
| **LIB-04** | 28-02 | ✅ **SATISFAITE** | `AffichageSessions.Urgence` (Deduced=2 devant Working=3) ; `RangArbitrage` découplé et figé ; mutation rejouée et révoquée par le vérificateur (§2, point 3) ; `ArbitrageSessionsTests.Remonter_la_deduction_a_l_ecran_ne_change_aucun_arbitrage` (corpus 720 permutations) |
| **SIL-01** | 28-01, 28-04 | ✅ **SATISFAITE** | `HorizonsSessions` (4 seuils), `AppliquerSilence` en un point, `TranscriptSessionSource` sur `HorizonsSessions.Abandon` ; `HorizonsSessionsTests` (8 scénarios + 2 gardes de chaîne/câblage) ; mesure réelle 28,2 / 25,7 ms consignée dans `28-VALIDATION.md` (décision « pas de cache ») |

`REQUIREMENTS.md` : les cinq lignes `LIB-01`, `LIB-02`, `LIB-03`, `LIB-04`, `SIL-01` sont cochées `[x]` et
marquées `| Phase 28 | Complete |`. Aucune exigence orpheline (aucun autre ID de `REQUIREMENTS.md` ne rattache la
phase 28).

---

## 4. Ce qui reste manuel — déjà reporté à la phase 31, non bloquant

`28-VALIDATION.md` (section « Manual-Only Verifications ») reporte explicitement deux vérifications à la phase 31 :

1. **La galerie `--sessions` et les 9 thèmes, à l'œil** (LIB-01, LIB-03) — le rendu visuel n'est pas observable
   sans lancer l'overlay ou la galerie, ce que l'agent (exécuteur comme vérificateur) s'interdit. Le test WPF
   `Les_trois_mots_se_lisent_sur_les_8_styles_et_les_9_themes` couvre mécaniquement les 72 combinaisons (mots,
   info-bulles, troncature, tailles) sans ouvrir de fenêtre visible — c'est la limite du programmatique, pas
   un doute sur le câblage.
2. **`PermissionRequest` part-il pour `AskUserQuestion` ?** (LIB-02) — fait externe MEDIUM (comportement de
   Claude Code lui-même), non observable sans une question réelle affichée dans l'app. Les deux issues
   d'arbitrage sont écrites et testées en pur (`Sans_PermissionRequest_le_battement_d_une_question_gagne_par_fraicheur`,
   `Avec_PermissionRequest_la_question_reste_une_attente`) : quelle que soit la réponse in vivo, le comportement
   du widget est défini et testé pour les deux cas.

Ces deux items sont repris tels quels dans le frontmatter (`human_verification`). Ils ne bloquent pas le
statut de cette phase : ils étaient déjà hors du périmètre exécutable par un agent en lecture seule
(28-CONTEXT.md, 28-VALIDATION.md), et le ROADMAP les confie explicitement à la phase 31 (« le tableau en trois
lignes vérifié sur la machine de l'utilisateur »).

---

## 5. Hygiène de la vérification

Aucune écriture hors dépôt. La seule mutation jouée (§2, point 3) a été appliquée directement au fichier du
dépôt, sauvegardée au préalable dans le dossier scratchpad de session (hors dépôt), puis restaurée depuis cette
sauvegarde — checksum identique, `git diff`/`git status` vides sur le fichier concerné. `git status --porcelain`
du dépôt entier, avant et après la session de vérification, ne montre que `Chronos-v3.1.0.exe`, non suivi et
préexistant (visible dans le statut Git fourni en tête de session). Aucun overlay n'a été lancé ni arrêté ;
aucun fichier sous `%APPDATA%\Chronos`, `%APPDATA%\Claude` ou `~/.claude` n'a été approché.

---

## 6. Conclusion

**Le goal de la phase est atteint.** Le widget ne parle plus qu'en trois chaînes exactes — vérifié par grep
négatif sur les 9 fichiers de production, sans une seule survivance de l'ancien vocabulaire. Une question
`AskUserQuestion` sans réponse est classée attente, sur des lignes réelles, pas seulement sur des fixtures
synthétiques. L'ordre d'écran place la déduction devant la réflexion **sans** entraîner l'arbitrage — la
réserve R4 de l'audit v1.6 est fermée, et je l'ai vérifié moi-même en rejouant la mutation qui la ferait
échouer, puis en la révoquant proprement. Le trou §9.1 est refermé : une session connue par son seul
transcript suit désormais exactement les mêmes horizons (20 min de silence, 8 h d'abandon) qu'une session à
fichier de hook, et ces horizons vivent dans un type unique sous deux gardes. La suite complète, rejouée deux
fois par le vérificateur, est verte à 947/947, sans aucune régression sur les gardes historiques du projet.

Deux vérifications visuelles/in vivo restent hors de portée d'un agent — elles étaient déjà reportées à la
phase 31 par le plan lui-même, et ne remettent pas en cause ce qui est prouvé mécaniquement ici.

---

_Vérifié : 2026-09-25 — Vérificateur : Claude (gsd-verifier)_
_Suite exécutée deux fois par le vérificateur : 947 / 0 échec. Une mutation rejouée et révoquée (sha256 identique). Aucun chiffre de SUMMARY repris sans exécution._
