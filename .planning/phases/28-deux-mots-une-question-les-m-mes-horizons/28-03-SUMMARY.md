---
phase: 28-deux-mots-une-question-les-m-mes-horizons
plan: 03
subsystem: widget-sessions
tags: [csharp, wpf, mvvm, xaml, libelles, contrat-hooks, xunit, gardes-texte]
one_liner: "Le widget ne parle plus qu'en trois mots — « Réflexion », « En attente », « En attente ? » — nés d'un seul producteur et recopiés d'un bloc (rôles des hooks, §1 et §3 du contrat, galerie, compteur de l'Annonciateur, boîte d'activation) ; le fantôme meurt, la déduction s'atténue à 0,7 sur six gabarits, le mot se lit en info-bulle sur les huit ; 924 → 935 tests, 0 échec"

requires:
  - phase: 28-02
    provides: "AffichageSessions.EstUneAttente, AUneLigne, ordre d'écran ; MotifMasquage.Indeterminee (l'indéterminé n'a plus de ligne)"
  - phase: 25-evt
    provides: "SessionHookInstaller.Cablage + ContratHooksDocumenteTests (garde du §1, colonne rôle)"
provides:
  - "AffichageSessions.Reflexion / EnAttente / EnAttenteDeduite (constantes) et Etat à trois mots (« indéterminé » pour le seul rapport)"
  - "AffichageSessions.TexteActivation() — texte de la boîte d'activation construit sur les constantes (réserve R9 fermée)"
  - "LibelleCompteur sur SessionsViewModel ET SessionsPreviewViewModel ; Infobulle et IsDeduced sur SessionItemVm ; IsGhost supprimé"
  - "docs/hooks-contract.md §3 balisé ETATS-AFFICHES, comparé au producteur par ContratHooksDocumenteTests"
  - "LibellesSessionsTests : 5 gardes texte/réflexion (anciens libellés, texte d'activation, fantôme, info-bulles, bindings des deux ViewModels)"
affects: [28-04, 29-app-bureau, 30-lue, 31]

tech-stack:
  added: []
  patterns:
    - "Un mot, un producteur : les gabarits XAML ne portent aucun libellé d'état, ils lient une propriété tirée d'AffichageSessions"
    - "Garde de bindings muets : chaque nom lié dans un gabarit partagé doit exister sur la ligne ou sur LES DEUX ViewModels de liste (réflexion)"
    - "Extracteur de table Markdown balisée généralisé (TableEntre) : §1 et §3 du contrat tenus par le même code"

key-files:
  created:
    - tests/Chronos.Tests/LibellesSessionsTests.cs
  modified:
    - src/Chronos/Services/AffichageSessions.cs
    - src/Chronos/Services/SessionHookInstaller.cs
    - docs/hooks-contract.md
    - src/Chronos/Views/SessionsController.cs
    - src/Chronos/Views/SessionsGalleryWindow.xaml
    - src/Chronos/Views/SessionsWindow.xaml
    - src/Chronos/ViewModels/SessionsPreviewViewModel.cs
    - src/Chronos/ViewModels/SessionsViewModel.cs
    - src/Chronos/Resources/SessionStyles.xaml
    - tests/Chronos.Tests/AffichageSessionsTests.cs
    - tests/Chronos.Tests/InspectionSessionsTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - tests/Chronos.Tests/ContratHooksDocumenteTests.cs
    - tests/Chronos.Tests/SessionStylesBindingTests.cs

key-decisions:
  - "Tout le vocabulaire en UN commit (tâche 1, 14 fichiers) : aucun commit intermédiaire ne dit deux vocabulaires, et la garde Chaque_ligne_documentee_porte_le_role_reellement_cable ne rougit jamais entre deux commits"
  - "Unknown garde un mot, « indéterminé », lu par le SEUL rapport de diagnostic ; le widget ne l'affiche jamais (masqué au moniteur depuis 28-02)"
  - "Le compteur de l'Annonciateur dit la constante telle quelle (« 3 En attente ») : trois chaînes exactes, pas de variante en minuscule"
  - "Info-bulle = « projet — mot » sur les huit gabarits : le critère « Réflexion sur les 8 styles » devient vérifiable ; la phase 29 y ajoutera le titre"
  - "Chaque déclencheur IsGhost devient IsDeduced à 0,7, dans les Style.Triggers (jamais en attribut local) ; Veilleurs : l'atténuation passe sur l'œil OUVERT, l'œil fermé étant masqué dès qu'une session attend"

patterns-established:
  - "Garde « anciens libellés » ligne à ligne sur 9 fichiers (fichier:ligne — motif), anti-muette (≥ 500 caractères par fichier, trois mots présents dans le contrat)"

requirements-completed: [LIB-01, LIB-03]

duration: 12min
completed: 2026-09-25
---

# Phase 28 Plan 03 : Trois mots, d'un seul producteur, sur les huit gabarits — Summary

**Le widget ne parle plus qu'en trois mots — « Réflexion », « En attente », « En attente ? » — nés d'un seul
producteur et recopiés d'un bloc (rôles des hooks, §1 et §3 du contrat, galerie, compteur de l'Annonciateur,
boîte d'activation) ; le fantôme meurt, la déduction s'atténue à 0,7 sur six gabarits, le mot se lit en
info-bulle sur les huit ; 924 → 935 tests, 0 échec.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-09-25T19:40:50Z
- **Completed:** 2026-09-25T19:52:16Z
- **Tasks:** 2 / 2
- **Files modified:** 15 (9 sources/document, 6 fichiers de tests dont 1 créé)
- **SHA d'entrée du plan :** `59b29c4` (vague 2, seul sur l'arbre)

## Accomplishments

- **LIB-01 livré.** `AffichageSessions.Etat` ne rend plus que trois mots pour une session qui a une ligne :
  `Working` → « Réflexion », `WaitingTurn` et `WaitingAttention` → « En attente », `WaitingDeduced` →
  « En attente ? ». « tour fini », « à toi », « en cours », « à toi ? déduit » et « inconnu » ne s'affichent plus
  nulle part (garde texte sur 9 fichiers). L'indéterminé, sans ligne depuis 28-02, garde « indéterminé » pour le
  seul rapport.
- **LIB-03 livré.** Un seul producteur : widget, rapport, galerie (`AffichageSessions.Etat(...)` × 5), compteur de
  l'Annonciateur (`LibelleCompteur`, exposé par les deux ViewModels) et texte d'activation
  (`AffichageSessions.TexteActivation()`) en tirent leurs mots. Aucun libellé d'état n'est écrit en dur dans un
  gabarit. `IsWaiting`, `WaitingCount` et la couleur lisent `EstUneAttente` : plus aucune copie locale du
  prédicat dans le ViewModel.
- **Réserve R9 fermée.** La boîte d'activation (« détectées via leurs transcripts… a fini son tour… travaille
  encore ») est remplacée par un texte construit sur les trois constantes : il ne peut plus dériver de l'écran.
- **Le contrat dit les mêmes mots que l'écran.** §1 (rôles « → Réflexion » / « → En attente ») bougé avec
  `SessionHookInstaller.Cablage` dans le même commit ; §3 balisé `ETATS-AFFICHES`, trois mots, paragraphe « Trois
  mots, pas cinq », et une garde croisée qui rougit si le §3 cesse d'égaler le producteur ; prose §4 et §5 sans
  ancien libellé, phrases gardées par test intactes ; « Deux ordres, et non un » (28-02) conservé mot pour mot.
- **Le fantôme meurt, la déduction s'atténue.** `IsGhost` supprimé ; six déclencheurs `IsDeduced` à 0,7 (Jetons,
  Sonar, Façade, Étagère, Annonciateur, Veilleurs sur l'œil ouvert) ; plus aucun déclencheur à 0,22 (le halo
  LOCAL des Jetons, `Opacity="0.22"`, inchangé). Pastilles et Marge : aucun déclencheur, le mot et son « ? »
  suffisent.
- **Le mot se lit sur les huit styles et les neuf thèmes.** Info-bulle « projet — mot » sur les huit gabarits ; en
  texte sur Pastilles et Marge, sans troncature (`TextTrimming.None`, `ActualWidth + 0,5 ≥ DesiredSize.Width`),
  sur les 72 combinaisons.

## Task Commits

1. **Tâche 1 : trois mots d'un seul producteur, et tout ce qui les recopie dans le MÊME commit** — `bf63366` (feat)
2. **Tâche 2 : les huit gabarits — le fantôme meurt, la déduction s'atténue, le mot passe en info-bulle** — `fc9c99d` (feat)

**Plan metadata :** commit `docs(28-03)` final (SUMMARY, VALIDATION, STATE, ROADMAP, REQUIREMENTS).

### Preuve du « même commit » — les 14 fichiers de `bf63366`

`docs/hooks-contract.md`, `src/Chronos/Resources/SessionStyles.xaml`, `src/Chronos/Services/AffichageSessions.cs`,
`src/Chronos/Services/SessionHookInstaller.cs`, `src/Chronos/ViewModels/SessionsPreviewViewModel.cs`,
`src/Chronos/ViewModels/SessionsViewModel.cs`, `src/Chronos/Views/SessionsController.cs`,
`src/Chronos/Views/SessionsGalleryWindow.xaml`, `src/Chronos/Views/SessionsWindow.xaml`,
`tests/Chronos.Tests/AffichageSessionsTests.cs`, `tests/Chronos.Tests/ContratHooksDocumenteTests.cs`,
`tests/Chronos.Tests/DiagnosticServiceTests.cs`, `tests/Chronos.Tests/InspectionSessionsTests.cs`,
`tests/Chronos.Tests/LibellesSessionsTests.cs` (créé) — 342 insertions, 100 suppressions. Le producteur, les rôles
câblés, le §1, le §3, la galerie, le compteur et le texte d'activation changent ensemble.

## Preuves — rouges, totaux

### Tâche 1 — RED (tests écrits avant le code), contre un squelette compilable : 15 échecs / 913 réussites sur 928

Squelette de compilation (non commité) : les trois constantes et `TexteActivation() => throw new NotImplementedException()`,
`Etat` encore à cinq libellés.

- `ContratHooksDocumenteTests.Le_paragraphe_3_affiche_les_libelles_du_producteur` (marqueur `ETATS-AFFICHES:debut` absent)
- `AffichageSessionsTests.Chaque_etat_a_son_libelle` × 5 : (`WaitingAttention`, « En attente »), (`WaitingTurn`, « En attente »),
  (`Working`, « Réflexion »), (`Unknown`, « indéterminé »), (`WaitingDeduced`, « En attente ? »)
- `AffichageSessionsTests.Le_producteur_ne_connait_que_trois_mots_visibles`
- `AffichageSessionsTests.Une_session_deduite_est_visible_ambre_et_dit_sa_deduction`
- `AffichageSessionsTests.Le_widget_affiche_ce_que_la_couche_neutre_produit`
- `LibellesSessionsTests.Le_texte_d_activation_vient_du_producteur_et_dit_les_trois_mots` (`NotImplementedException`)
- `LibellesSessionsTests.Aucun_ancien_libelle_ne_subsiste_a_l_ecran_ni_dans_le_contrat` — **47 infractions** listées
  `fichier:ligne — motif` : contrat 17, `SessionStyles.xaml` 4 (dont `Text="  en attente"` l. 317), `SessionsWindow.xaml` 2,
  `SessionsViewModel.cs` 4, `SessionsPreviewViewModel.cs` 5, `AffichageSessions.cs` 10, `SessionHookInstaller.cs` 5
- `InspectionSessionsTests.Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini`
- `DiagnosticServiceTests.Le_rapport_decrit_les_sessions_du_moniteur_qu_on_lui_donne`,
  `Le_cas_e465420e_se_lit_en_une_ligne_du_rapport`, `Un_desaccord_nomme_la_source_retenue_la_source_ecartee_et_l_ecart_d_age`

### Tâche 2 — RED contre des membres VIDES (précédent 18-01) : 6 échecs / 929 réussites sur 935

Membres vides : `[ObservableProperty] bool _isDeduced` jamais positionné, `Infobulle => Project` ; `IsGhost` encore présent.

- `AffichageSessionsTests.Une_session_deduite_est_visible_ambre_et_dit_sa_deduction` (`IsDeduced` faux)
- `AffichageSessionsTests.Le_drapeau_et_le_compteur_d_attente_viennent_du_predicat_unique` (`Expected: "p — Réflexion"`, lu « p »)
- `SessionStylesBindingTests.Les_trois_mots_se_lisent_sur_les_8_styles_et_les_9_themes`
  (« style Pastilles, thème minuit : l'info-bulle « overlay » ne dit pas « projet — mot » »)
- `LibellesSessionsTests.Le_fantome_a_quitte_les_gabarits_et_la_deduction_s_attenue_sans_s_effacer` (`{Binding IsGhost}` trouvé)
- `LibellesSessionsTests.Les_huit_infobulles_disent_le_mot` (`Expected: 8 / Actual: 0`)
- `LibellesSessionsTests.Tout_ce_que_les_gabarits_lient_existe_sur_les_deux_ViewModels` (« IsGhost doit disparaître »)
- VERTS par construction, et c'est attendu : `Le_tableau_de_l_utilisateur_se_rejoue_en_trois_mots` (l'ordre vient de
  28-02, les mots de la tâche 1 — le test REJOUE le tableau, il ne pilote rien de neuf) ;
  `L_annonciateur_annonce_En_attente_par_le_producteur` (le binding `LibelleCompteur` est posé par la tâche 1, par la
  contrainte du « même commit »).

### Totaux mesurés (`dotnet test Chronos.sln -c Debug --nologo -v q`)

| Instant | Total | Échecs | Composition |
|---|---|---|---|
| Entrée du plan (`59b29c4`) | **924** | 0 | baseline mesurée (sortie de la vague 1) |
| Après la tâche 1 (`bf63366`) | **928** | 0 | 924 + 4 |
| Après la tâche 2 (`fc9c99d`) — deux exécutions consécutives | **935** / **935** | 0 / 0 | 928 + 7 |

Conforme au plan (+4 puis +7). Aucun test supprimé, aucun renommé, aucun ignoré.

**Gardes vertes, sans assouplissement :** `ContratHooksDocumenteTests` 12/12 (dont `Chaque_ligne_documentee_porte_le_role_reellement_cable`,
`Le_document_dit_la_regle_de_traitement_reellement_cablee`, `Le_document_dit_la_monotonie_livree_ET_le_chemin_de_R3_reste_ouvert`,
`Le_document_porte_les_trois_trous_documentaires_avec_leur_date`, `Le_paragraphe_3_dit_l_ordre_d_ecran_et_le_rang_d_arbitrage_fige`),
`GardesPerimetreTests` (8 templates, 24 `MenuItem`, 8 `Separator`, aucun `KindLabel`), `SessionStylesBindingTests` (72
combinaisons et menus), `LibellesSessionsTests` 5/5, `ServicesLayerPurityTests`, `GardesDoctrineTests`,
`NormalisationUniqueTests`, `CompositionRootTests` — toute la suite verte.

### Critères grep

Tâche 1 : `public const string EnAttenteDeduite = "En attente ?";` = 1 ; `ETATS-AFFICHES:` = 2 ; rôles
`→ Réflexion|→ En attente|: Réflexion` = 7 ; `AffichageSessions.TexteActivation()` dans le contrôleur = 1,
`détectées via` = 0 ; `{Binding LibelleCompteur}` = 1 ; `LibelleCompteur` = 1 dans chaque ViewModel ; `Add("` = 5 et
`AffichageSessions.Etat(` = 5 dans la galerie ; `docs/hooks-contract.md` = 445 lignes (≥ 90).
Tâche 2 : `IsGhost` = 0 dans les trois fichiers ; `IsDeduced` = 6 ; `Value="0.22"` = 0 ; `Opacity="0.22"` = 1 ;
`ToolTip="{Binding Infobulle}"` = 8, `ToolTip="{Binding Project}"` = 0 ; `AffichageSessions.EstUneAttente(` = 3 dans
`SessionsViewModel.cs`, `WaitingAttention or SessionActivity.WaitingTurn` = 0.

## Files Created/Modified

- `src/Chronos/Services/AffichageSessions.cs` — trois constantes, `Etat` à trois mots, `TexteActivation`
- `src/Chronos/Services/SessionHookInstaller.cs` — rôles des 8 groupes aux nouveaux mots (nom, matcher, timeout inchangés)
- `docs/hooks-contract.md` — §1 (rôles), §3 (table balisée, trois mots, paragraphe), prose §4/§5
- `src/Chronos/Views/SessionsController.cs` — boîte d'activation = `AffichageSessions.TexteActivation()`
- `src/Chronos/Views/SessionsGalleryWindow.xaml`, `src/Chronos/Views/SessionsWindow.xaml` — sous-titre, commentaire
- `src/Chronos/ViewModels/SessionsPreviewViewModel.cs` — échantillons tirés du producteur, `LibelleCompteur`, `deduced`
- `src/Chronos/ViewModels/SessionsViewModel.cs` — `Infobulle`, `IsDeduced`, `LibelleCompteur`, prédicat unique, `Describe` à deux valeurs
- `src/Chronos/Resources/SessionStyles.xaml` — compteur lié, huit info-bulles, six déclencheurs `IsDeduced` à 0,7
- `tests/Chronos.Tests/LibellesSessionsTests.cs` (créé) et 5 fichiers de tests adaptés/étendus

## Decisions Made

Voir `key-decisions` en tête. En bref : un seul commit pour tout le vocabulaire ; « indéterminé » réservé au
rapport ; la constante telle quelle au compteur ; l'info-bulle comme canal textuel des six gabarits sans texte ;
l'atténuation à 0,7 dans les `Style.Triggers`, sur l'œil ouvert pour les Veilleurs.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Incohérence du plan] Commentaire du style implicite de l'info-bulle reformulé**
- **Found during:** Tâche 2 (GREEN)
- **Issue:** le texte de commentaire prescrit (« … S'applique à tous les `ToolTip="{Binding Infobulle}"` … ») recopiait
  le littéral compté : 9 occurrences, contre 8 exigées par le critère grep et par `Les_huit_infobulles_disent_le_mot`
  (qui a rougi : `Expected: 8`, lu 9).
- **Fix:** « S'applique aux huit info-bulles des sessions, liées à Infobulle (projet — mot d'état). » — même sens,
  sans le littéral.
- **Files modified:** `src/Chronos/Resources/SessionStyles.xaml`
- **Commit:** `fc9c99d`

### Ajustements mineurs (aucun changement de comportement)

**2. [Rule 3 - RED compilable] Squelette de la tâche 1** — constantes et `TexteActivation` levant
`NotImplementedException`, pour que le RED rougisse sur le COMPORTEMENT et non à la compilation (précédent 18-01).
Remplacé par le GREEN avant le commit : jamais commité.

**3. [Exactitude] Lignes décalées** — `DiagnosticServiceTests` : les assertions du désaccord étaient aux l. 648 et 650
(le plan disait 626 et 628, avant les ajouts de 28-02) ; les valeurs prescrites (« « Réflexion » », « « En attente » »)
ont été appliquées telles quelles.

**4. [Cohérence] Deux commentaires de test** — `AffichageSessionsTests.Chaque_valeur_de_l_enumeration_a_un_libelle_non_vide`
(« inconnu » → « indéterminé ») ; `LigneDe` de `ContratHooksDocumenteTests` prend une `cle` et un message neutre, puisqu'il
sert désormais aux événements (§1) ET aux états (§3). Aucune assertion assouplie.

**5. [TDD] RED et GREEN dans un même commit par tâche** — les rouges ont été exécutés et consignés nominativement
ci-dessus ; un commit rouge seul aurait été un état intermédiaire où le dépôt dit deux vocabulaires, ce que la tâche 1
existe pour interdire.

**Total deviations:** 1 correction d'incohérence du plan, 4 ajustements mineurs. **Impact:** aucun changement de périmètre.

## Issues Encountered

Aucun blocage. Note d'outillage : les scripts d'édition ont été passés par fichiers (et non par `heredoc`) après
qu'un `heredoc` a altéré des barres obliques inverses — corrigé avant tout build vert, sans trace dans les commits.

## Known Stubs

Aucun. Hors périmètre, laissé tel quel : `SessionsViewModel.Summary` (« N en attente · M session(s) ») n'est bindé
nulle part (le plan dit « inchangé ») ; quelques commentaires HISTORIQUES de `InspectionSessionsTests` citent encore
les libellés de la v1.6 pour raconter les phases 24-25 — ce fichier n'affiche rien et n'est pas balayé par la garde.

## Vérification manuelle (non bloquante, portée par 28-VALIDATION.md)

Galerie `--sessions` : les trois mots sur les 8 tuiles, « En attente ? » atténuée mais lisible, plus aucun fantôme,
sous-titre « 2 « En attente », 1 « En attente ? », 2 « Réflexion » », info-bulle « projet — mot » au survol —
**à vérifier en phase 31** (l'agent ne lance ni l'overlay ni la galerie). Mécaniquement couvert par
`Les_trois_mots_se_lisent_sur_les_8_styles_et_les_9_themes`.

## Next Phase Readiness

- 28-04 (silence étendu aux transcripts, horizons) : le §3 du contrat garde le paragraphe « `WaitingDeduced` n'est
  JAMAIS écrite » à réécrire (`SilenceDesBattements`, `DropAfter`) — seule sa phrase sur le libellé a changé ici.
- Phase 29 (app bureau) : `Infobulle` est le point d'accueil du titre de session ; `blocked` rejoindra « En attente ».
- Phase 30 (règle « lue ») : le ViewModel lit déjà `AffichageSessions.EstUneAttente` pour son drapeau et son compteur.

## Self-Check: PASSED

Fichiers annoncés présents (`LibellesSessionsTests.cs`, `28-03-SUMMARY.md`) ; commits `bf63366`, `fc9c99d` et SHA
d'entrée `59b29c4` trouvés dans l'historique ; suite 935/0 mesurée deux fois.
