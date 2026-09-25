# Phase 28 : Deux mots, une question, les mêmes horizons — Recherche

**Researched:** 2026-09-25
**Domain:** vocabulaire et ordre d'un widget WPF (8 styles × 9 thèmes), classification de transcripts JSONL,
horizons temporels d'un moniteur de sessions — archéologie de code + mesures sur la vraie machine
**Confidence:** HIGH (tout ce qui est affirmé ici est lu dans le dépôt ou mesuré sur cette machine, en lecture
seule ; les deux zones MEDIUM sont nommées : le comportement des hooks Claude Code sur `AskUserQuestion`, et
l'effet visuel exact de l'atténuation par style)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Phase Boundary (copié tel quel)

Le widget de sessions ne parle plus qu'en deux mots. Cette phase touche le VOCABULAIRE et les HORIZONS, pas
les sources : ni la source app-bureau (phase 29), ni la règle « lue » (phase 30). Elle est indépendante du
relevé de la phase 27.

Livrables : `AffichageSessions` (libellés, ordre), `TranscriptSessionSource` (AskUserQuestion, horizons),
`SessionMonitor` (horizons partagés), `SessionsViewModel` (drapeaux de gabarit), les 8 templates de
`SessionStyles.xaml` (aucun libellé codé en dur n'y subsiste), `docs/hooks-contract.md` §3, le texte
d'activation du widget, et les tests.

### Locked Decisions (copiées telles quelles)

#### Les libellés — VERROUILLÉS par l'utilisateur
- Exactement trois chaînes à l'écran : **« Réflexion »**, **« En attente »**, **« En attente ? »**.
  Rien d'autre : « tour fini », « à toi », « en cours », « à toi ? déduit », « inconnu » disparaissent.
- « En attente » couvre `WaitingTurn` (Stop observé / `end_turn`), `WaitingAttention` (PermissionRequest,
  Notification filtrée) et la question posée (LIB-02 ; en phase 29, `blocked` de l'app rejoindra ce même mot).
- « En attente ? » = `WaitingDeduced` uniquement. Le point d'interrogation est obligatoire (doctrine v1.6).
- « Réflexion » = `Working`.
- `Unknown` n'a plus de ligne dans le widget. Le diagnostic, lui, continue de la lister avec la raison.
- Les mots portent la distinction ; la couleur reste : rampe ambre pour les trois attentes, rampe verte pour
  Réflexion (inchangé). « En attente ? » peut être atténuée (opacité) mais reste lisible — pas de fantôme à
  0,22 (leçon de la phase 25).

#### La question `AskUserQuestion` (LIB-02)
- Dans le transcript : un message assistant dont le dernier `tool_use` s'appelle `AskUserQuestion` et sans
  `tool_result` postérieur ⇒ attente (rang « question », comme une permission), pas Working.
- Dès qu'un `tool_result` (ou un message user) suit ⇒ Working à nouveau.
- Le nom d'outil est comparé ordinalement, exact : `AskUserQuestion`. Pas de liste extensible « au cas où ».

#### L'ordre (LIB-04) et le couplage R4
- Ordre d'écran : WaitingAttention (permission/question) → WaitingTurn → WaitingDeduced → Working → puis
  fraîcheur décroissante. Donc **« En attente ? » passe DEVANT « Réflexion »** (aujourd'hui la déduction est
  au dernier rang, derrière Working).
- `ArbitrageSessions.Departager` consomme `AffichageSessions.Urgence` au rang 3 (réserve R4 de l'audit v1.6).
  Décision : **découpler** — l'arbitrage garde son propre rang d'état (un `RangArbitrage` privé, figé,
  documenté comme tel), et un test prouve que changer l'ordre d'écran ne change aucun résultat d'arbitrage
  (rejouer le corpus des 720 permutations avec l'ordre d'écran muté).

#### Les horizons (SIL-01)
- `TranscriptSessionSource.ActiveWindow` passe de 15 min à **8 h** (= `SessionMonitor.DropAfter`), et la
  règle de silence (`SilenceDesBattements` = 20 min, Working → WaitingDeduced) s'applique aux transcripts
  comme aux hooks — dans UN seul endroit (le moniteur), pas dupliquée dans la source.
- Les constantes d'horizon vivent dans un type unique (`HorizonsSessions` ou équivalent) et une garde
  rougit si la chaîne 20 min < 8 h < 24 h (TreatedStore.RetentionMax) < 72 h (BalayageMagasinSessions) se
  défait.
- Coût à MESURER sur la vraie machine (~1 050 transcripts, dont ~94 % de sous-agents pré-filtrés par chemin) :
  énumération + lecture des queues de 64 Ko des fichiers < 8 h, toutes les 2 s. Si > ~50 ms par cycle, mettre
  en cache par (chemin, mtime, taille) — le cache est acceptable, la lecture partielle ne l'est pas.
- `MaxSessions` = 12 reste ; le tri par fraîcheur avant la limite garantit que les plus récentes gagnent.

#### Un seul producteur (LIB-03)
- `AffichageSessions.Etat` et `Ordonner` restent LE producteur ; `SessionsViewModel.Describe` ne fait que
  colorer. Garde existante des 16 caractères conservée (« En attente ? » = 12).
- `docs/hooks-contract.md` §3 (états produits) et le texte d'activation du widget (réserve R9) reprennent
  les trois mots exacts ; la garde `ContratHooksDocumenteTests` s'étend aux libellés si elle ne le fait pas.

#### Specific Ideas (copiées telles quelles)
- Le tableau de l'utilisateur, à rejouer tel quel en test : réfléchit → « Réflexion » ; a fini ou pose une
  question, non lue → « En attente » ; a fini et lue → rien (cette dernière ligne est la phase 30, mais la
  phase 28 doit laisser un point d'entrée : le prédicat « est une attente » unique et nommé).
- Scénario SIL-01 à tester avec horloge injectée : transcript seul, Working, dernière écriture il y a 25 min
  ⇒ « En attente ? » ; transcript seul, `end_turn` il y a 3 h ⇒ « En attente » ; 8 h 01 ⇒ absent.

### Claude's Discretion (copiée telle quelle)
- Nom du type des horizons, découpage en plans/vagues, ordre des tests, choix cache ou non selon la mesure.
- Présentation exacte de l'atténuation de « En attente ? » par style (respecter chaque template).

### Deferred Ideas — OUT OF SCOPE (copiées telles quelles)
- `blocked` / `needs_action` de l'app bureau → phase 29 (même mot « En attente », rang question).
- La règle « lue » et la détection Win32 du premier plan → phase 30 (forme fixée par le relevé de phase 27).
- Exploiter `latestUserFrameAt` / `completedTurns` → hors v1.7.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description (REQUIREMENTS.md) | Ce que la recherche apporte |
|----|-------------------------------|-----------------------------|
| **LIB-01** | Deux libellés « Réflexion » / « En attente », « En attente ? » pour la déduction ; « inconnu » n'est plus affiché. | Q1 (inventaire complet fichier:ligne, 17 tests qui rougissent), Q6 (`Unknown` masqué au moniteur avec un motif, pas filtré au VM), Pattern 1, Pattern 5, Piège 5, Piège 7 |
| **LIB-02** | Une question `AskUserQuestion` en suspens (`tool_use` sans `tool_result`) est classée « En attente ». | Q3 (forme exacte mesurée sur 224 questions réelles, rappel de la règle verrouillée : **221 / 222**, extrait anonymisé et fixture), Piège 3 (le battement `PreToolUse` et `PermissionRequest` côté hooks) |
| **LIB-03** | Un seul producteur (`AffichageSessions`) pour le widget et le diagnostic ; ≤ 16 caractères ; 8 styles × 9 thèmes sans troncature. | Q1, Q6, Pattern 1 (constantes + `EstUneAttente` + `TexteActivation`), Pattern 5 (drapeaux de gabarit), Pièges 7 et 8 |
| **LIB-04** | Ordre : permission/question → tour fini → déduit → Réflexion → fraîcheur. | Q2 (le corpus des 720 permutations n'atteint JAMAIS le rang 3 : le rejouer tel quel ne prouve rien ; corpus de remplacement fourni), Pattern 2 (`RangArbitrage` figé), Piège 2 |
| **SIL-01** | Transcript seul : mêmes horizons (20 min ⇒ « En attente ? », 8 h ⇒ abandon), plus de disparition à 15 min. | Q4 (coût **mesuré** : 19,6 ms médian par cycle à 8 h, pas de cache), **découverte : l'mtime n'est pas l'instant du signal** (12 transcripts « rajeunis » en bloc à 16 h 58 par la fermeture de l'app), Q5 (`HorizonsSessions` + garde), Patterns 3 et 4, Piège 1 |

**Critères de succès de la ROADMAP** (ils gouvernent la vérification) : (1) deux mots et un « ? » partout,
aucune session indéterminée à l'écran, le diagnostic dit pourquoi ; (2) une question n'est pas une réflexion ;
(3) l'ordre dit l'urgence et ne touche pas l'arbitrage ; (4) le trou §9.1 est refermé (25 min ⇒ « En
attente ? », 3 h ⇒ « En attente », > 8 h ⇒ absent) ; (5) les horizons ne peuvent plus diverger en silence, et
le §3 du contrat comme le texte d'activation disent les mêmes mots que l'écran.
</phase_requirements>

---

## Project Constraints (from CLAUDE.md)

| Directive | Conséquence pour cette phase |
|---|---|
| C# / .NET 8 (`net8.0-windows`) / WPF / MVVM strict (CommunityToolkit.Mvvm), DI | Tout nouveau drapeau de gabarit est une `[ObservableProperty]` de `SessionItemVm` ; tout libellé naît dans `Services/AffichageSessions` (couche neutre) |
| Aucun type WPF dans `Services/` (`ServicesLayerPurityTests`) | `HorizonsSessions`, `RangArbitrage`, `TexteActivation`, `EstUneAttente` sont purs ; la couleur et l'opacité restent dans le VM / le XAML |
| Aucune dépendance native, rendu XAML pur | L'atténuation de « En attente ? » est une `Opacity` par `DataTrigger`, rien d'autre |
| Chemins sous `%USERPROFILE%` / `%APPDATA%`, aucun droit admin ; **ne jamais écrire dans `~/.claude` ni `%APPDATA%\Claude`** | La source transcripts reste en `FileShare.ReadWrite`, lecture seule ; les tests montent des racines temporaires |
| Ne jamais présenter une estimation comme exacte | « En attente ? » garde son point d'interrogation ; une question n'est classée attente que sur un `tool_use` **observé** |
| UI et commentaires en **français** | Noms : `HorizonsSessions`, `RangArbitrage`, `EstUneAttente`, `AUneLigne`, `IsDeduced`, `MotifMasquage.Indeterminee` |
| « Activer frontend-design + windows-wpf sur les tâches ui » | **Aucun de ces deux skills n'est installé** (vérifié : `~/.claude/skills` ne contient que athena, dev-senior, dev-team-council, graphify, hermes, saas-team-council ; aucun plugin). La consigne est sans objet ici ; la vérification visuelle passe par la galerie `--sessions` et le test WPF 8 × 9 |
| `Assembly.Location` interdit | Toute garde qui lit le texte source passe par `GardesPerimetreTests.CheminSources()` (attribut MSBuild `CheminSourcesChronos`) ou `CheminDocsChronos` |
| Gardes existantes à garder vertes (ROADMAP) | `ServicesLayerPurityTests`, `CompositionRootTests`, `GardesPerimetreTests`, `ContratHooksDocumenteTests`, `NormalisationUniqueTests` (**interdit `DateTimeOffset.TryParse` dans `Services/` hors exemptions** — voir Piège 6), `GardesDoctrineTests` |
| Ne pas lancer l'overlay | Aucune vérification visuelle sur l'overlay vivant ; galerie `--sessions` = contrôle humain uniquement |

---

## Summary

Le changement de vocabulaire est **petit et entièrement cartographié** : un seul producteur de libellés
(`AffichageSessions.Etat`, 5 lignes), deux gabarits sur huit qui affichent le mot, un libellé codé en dur dans
le gabarit Annonciateur (`"  en attente"`, `SessionStyles.xaml:317`), des échantillons codés en dur dans la
galerie, le texte d'activation (R9), la colonne « rôle » du câblage des hooks et le §3 du contrat. Dix-sept tests
existants rougiront, tous identifiés ci-dessous avec leur ligne. Le découplage R4 est une fonction privée de
huit lignes — mais **le test que le contexte propose ne prouve rien tel quel** : le corpus des 720 permutations
d'`ArbitrageSessionsTests` n'atteint jamais le rang 3 (son seul ex aequo, `s2`, est tranché au rang 2, la
source). Il faut un corpus qui y descend ; il est fourni.

La vraie matière de la phase est SIL-01, et la mesure a changé la recommandation. Le **coût** n'est pas un
problème : 19,6 ms médian par cycle à 8 h (12,0 ms aujourd'hui à 15 min, dont 11,3 ms d'énumération déjà payés),
très loin du seuil de 50 ms — **pas de cache**. Le problème est ailleurs : **l'mtime d'un transcript n'est pas
l'instant de son dernier signal**. À 16 h 58 min 20 s ce jour, la fermeture de l'app bureau a ajouté trois lignes
sans horodatage (`bridge-session`, `last-prompt`, `cost-state`) à **douze** transcripts dont le dernier vrai
message date de 2 h à **deux jours**. Avec la sémantique actuelle (mtime) portée à 8 h, ces douze sessions
s'afficheraient « En attente · il y a 3 h » jusqu'à 0 h 58 — la limite `MaxSessions` saturée par des fantômes.
Les mêmes lignes s'écrivent **pendant** qu'une question ou une permission attend (5 lignes entre la question et
la réponse dans l'exemple réel ci-dessous) : elles font avancer l'mtime, donc l'épisode d'attente, donc ramènent
une session « marquée traitée » (NET-03) sans qu'elle ait rien redemandé. 100 % des lignes `user` (21 200) et
`assistant` (37 370) portent un `timestamp` ISO : **l'instant du signal d'un transcript doit être l'horodatage de
sa dernière ligne significative**, borné par l'mtime, avec repli sur l'mtime s'il manque. Mesuré à l'instant T :
13 sessions visibles avec l'mtime, 5 avec l'horodatage.

LIB-02 est solide côté transcript (forme exacte relevée sur 224 questions réelles ; la règle verrouillée
reconnaît 221 des 222 questions répondues au moment où elles sont à l'écran, et aucune après la réponse). Côté
hooks, l'effet dépend d'un fait externe à vérifier in vivo : Claude Code émet `PreToolUse` (battement →
`Working`) **puis** `PermissionRequest` (→ `WaitingAttention`) pour `AskUserQuestion` — source tierce datée du
2026-09-23 (MEDIUM). Si c'est vrai, le hook dit « En attente » et gagne ; sinon le battement, plus récent que la
ligne de question, gagnerait l'arbitrage et la session resterait « Réflexion » dans la configuration réelle de
l'utilisateur. Un contrôle en lecture seule du fichier d'état pendant une question suffit à trancher.

**Primary recommendation :** `AffichageSessions` porte les trois mots en constantes, le prédicat unique
`EstUneAttente`, l'ordre d'écran et le texte d'activation ; `ArbitrageSessions` reçoit un `RangArbitrage` privé
figé aux valeurs de la phase 24 ; `HorizonsSessions` porte les quatre seuils ; `TranscriptSessionSource` date
chaque session par l'horodatage de sa dernière ligne significative et classe `AskUserQuestion` en
`WaitingAttention` ; `SessionMonitor` applique la règle de silence à tous les signaux en un seul point, et masque
`Unknown` avec un motif nommé.

---

## Q1 — Inventaire exact de ce qui produit, compare ou affiche un libellé d'état

### 1.a Producteurs et consommateurs (code)

| Fichier:ligne | Ce qui s'y trouve | Action |
|---|---|---|
| `Services/AffichageSessions.cs:26-27` | `Ordonner` = `OrderBy(Urgence).ThenByDescending(UpdatedAt)` | inchangé (il lira la nouvelle `Urgence`) |
| `Services/AffichageSessions.cs:30-40` | `Urgence` : Attention 0, Turn 1, Working 2, Deduced 3, `_` 3 | **nouvel ordre d'écran** : Attention 0, Turn 1, Deduced 2, Working 3, Unknown 4 |
| `Services/AffichageSessions.cs:47-56` | `Etat` : « à toi », « tour fini », « en cours », « à toi ? déduit », « inconnu » | **les trois mots** (+ un libellé de diagnostic pour `Unknown`, voir Q6) |
| `Services/ArbitrageSessions.cs:65` (XML-doc) et `:125-126` | rang 3 de `Departager` = `AffichageSessions.Urgence` | **`RangArbitrage` privé figé** (Q2) |
| `Services/DiagnosticService.cs:473-490` | liste des fichiers d'état triée par `Urgence` ; repli **codé `3`** pour une activité illisible ou sans date (l. 489-490) | remplacer le `3` par `AffichageSessions.Urgence(SessionActivity.Unknown)` : avec le nouvel ordre, `3` = le rang de `Working` |
| `Services/DiagnosticService.cs:531-533` | lignes « AFFICHÉES » : `Ordonner` + `Etat` | inchangé (suit le producteur) |
| `Services/DiagnosticService.cs:542-546` | lignes « MASQUÉES » triées par `Urgence`, libellé `Etat` | inchangé ; + nouveau motif (Q6) |
| `Services/DiagnosticService.cs:561-562` | désaccords : `Etat(EtatRetenu)` / `Etat(EtatEcarte)` | inchangé |
| `Services/DiagnosticService.cs:597-602` | `LibelleMotif` (Archivee, Traitee) | + `Indeterminee` |
| `ViewModels/SessionsViewModel.cs:35-40` | drapeaux `IsAttention`, `IsTurn`, `IsWorking`, `IsGhost` | `IsGhost` meurt (plus aucun `Unknown` à l'écran) ; **+ `IsDeduced`** |
| `ViewModels/SessionsViewModel.cs:197-205` | calcul des drapeaux | `IsWaiting` via `EstUneAttente` ; `IsDeduced = WaitingDeduced` |
| `ViewModels/SessionsViewModel.cs:213-217` | `WaitingCount` (3e copie du prédicat d'attente) ; `Summary` « … en attente · … » | `WaitingCount` via `EstUneAttente` ; `Summary` n'est **bindé nulle part** (vérifié) — l'aligner ou le laisser |
| `ViewModels/SessionsViewModel.cs:222-234` | `Describe` : `Etat` + pinceau + booléen d'attente (2e copie du prédicat) | ne garde que la couleur ; le booléen vient d'`EstUneAttente` |
| `Services/SessionTreatmentTracker.cs:52-54` | `EstAttente` (privé, **4e copie** du prédicat) | **ne pas toucher son corps** : `ContratHooksDocumenteTests:336-348` lit le texte du prédicat jusqu'au `;` et exige qu'il contienne `WaitingDeduced`. Le passer `internal` (le motif `EstAttente(SessionActivity` reste trouvé) et ajouter un test d'équivalence avec `AffichageSessions.EstUneAttente` sur les 5 valeurs |

### 1.b Libellés codés en dur (XAML, galerie, texte d'activation, documentation)

| Fichier:ligne | Texte | Action |
|---|---|---|
| `Resources/SessionStyles.xaml:317` | `Text="  en attente"` (compteur de l'Annonciateur) | **binder** sur une propriété de liste (`LibelleCompteur`) fournie par le producteur — elle doit exister sur `SessionsViewModel` **et** `SessionsPreviewViewModel` (gabarits partagés, Piège 7) |
| `Resources/SessionStyles.xaml:3-5` | commentaire « à toi respire, tour fini fixe ; Ghost = inconnu » | réécrire |
| `Views/SessionsWindow.xaml:10-11` | commentaire « déduit = fantôme » (faux depuis la phase 25) | réécrire |
| `ViewModels/SessionsPreviewViewModel.cs:10, 27-31` | échantillons « à toi », « tour fini », « en cours » ×2, « inconnu » (fantôme) | libellés tirés d'`AffichageSessions.Etat` ; remplacer l'échantillon fantôme par un « En attente ? » (`IsDeduced`) |
| `Views/SessionsGalleryWindow.xaml:32` | « données d'échantillon · 2 en attente, 2 en cours, 1 fantôme » | réécrire avec les nouveaux mots |
| `Views/SessionsController.cs:48-51` | texte d'activation (R9) : « détectées via leurs transcripts … a fini son tour … travaille encore » | construit par `AffichageSessions.TexteActivation` (neutre, testable) |
| `Services/SessionHookInstaller.cs:112-120` | rôles des 8 groupes : « → en cours », « → tour fini », « → à toi » | nouveaux mots, **dans le même commit** que la table du §1 du contrat (garde `Chaque_ligne_documentee_porte_le_role_reellement_cable`, `ContratHooksDocumenteTests:200-216`) |
| `docs/hooks-contract.md:45-52` | table §1 (rôles) | idem, synchronisée |
| `docs/hooks-contract.md:135-150` | table §3 (libellés affichés) + « rang 3, derrière Working : une déduction ne passe jamais devant une observation » | **réécrire** : trois mots, `Unknown` sans ligne, le nouvel ordre d'écran ET le rang d'arbitrage figé |
| `docs/hooks-contract.md:182-183, 214, 229, 256, 301, 306, 314` | « en cours », « à toi », `à toi ? déduit` dans la prose §4, §5.1, §5.2, §5.4, §5.5 | remplacer ; les phrases gardées par test (« FUS-01 vaut aussi à l'INTÉRIEUR d'une source », « n'est pas livré », « aucun `UserPromptSubmit` ne survient », « transition observée sur la MÊME source », « `WaitingDeduced` est une attente pour le détecteur », « daté par l'instant que le SIGNAL porte ») ne contiennent aucun libellé : elles restent vertes |

Menus contextuels : leurs trois libellés (« Marquer traitée (revient si elle me redemande) », etc.) ne sont
**pas** des libellés d'état ; ils sont gardés mot pour mot par `SessionStylesBindingTests:210-287` et
`GardesPerimetreTests:270-294`. Hors périmètre.

### 1.c Tests qui ROUGIRONT (17 méthodes, 15 entrées), et pourquoi

| Test (fichier:ligne) | Cause |
|---|---|
| `AffichageSessionsTests.Chaque_etat_a_son_libelle` (:17-24) | anciens libellés |
| `AffichageSessionsTests.Chaque_etat_a_son_rang_d_urgence` (:53-60) | nouvel ordre d'écran |
| `AffichageSessionsTests.Une_deduction_ne_passe_jamais_devant_une_observation` (:64-75) | LIB-04 inverse exactement cette règle d'ÉCRAN — à renommer et réécrire (la règle vaut encore pour l'ARBITRAGE, c'est le test R4 qui la porte désormais) |
| `AffichageSessionsTests.Le_widget_affiche_ce_que_la_couche_neutre_produit` (:136) | « à toi », « en cours » |
| `AffichageSessionsTests.Une_session_deduite_est_visible_ambre_et_dit_sa_deduction` (:158, :159, :169-172) | libellé ; `IsGhost` supprimé (compilation) ; le pinceau gris est obtenu via une session `Unknown` qui n'a plus de ligne (`Assert.Single` sur 0 élément) |
| `AffichageSessionsTests.Le_compteur_d_attente_inclut_la_deduction` (:188) | `TotalCount` 4 → 3 (l'`Unknown` est masqué) |
| `InspectionSessionsTests.Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini` (:410) | « à toi ? déduit » |
| `InspectionSessionsTests.Une_activite_illisible_reste_inconnue_et_non_deduite` (:436-450) | l'`Unknown` passe de `Visibles` à `Masquees` — l'assertion de fond (jamais déduite) reste, lue dans `Masquees` |
| `InspectionSessionsTests.Un_hook_deduit_et_un_transcript_travaillant_du_MEME_instant_sont_departages_par_la_source` (:484-503) | le transcript `Working` à −25 min devient LUI AUSSI `WaitingDeduced` (silence étendu aux transcripts) ⇒ doublon, plus de désaccord. **À reformuler, pas à supprimer** : c'est la dette n° 6 (R5). Version conforme : hook `Working` −25 min (→ déduit) contre transcript `WaitingTurn` au même instant ⇒ le hook gagne par la source, une déduction bat une observation |
| `DiagnosticServiceTests.Le_rapport_decrit_les_sessions_du_moniteur_qu_on_lui_donne` (:555) | « overlay — à toi (il y a 5 min) » |
| `DiagnosticServiceTests.Le_cas_e465420e_se_lit_en_une_ligne_du_rapport` (:580) | « à toi » |
| `DiagnosticServiceTests.Un_desaccord_nomme_la_source_retenue_la_source_ecartee_et_l_ecart_d_age` (:626, :628) | « en cours », « à toi » |
| `SessionsTests.Transcript_trop_ancien_est_ignore` (:556-565) | 30 min était > 15 min ; à 8 h il est lu — le porter à 8 h 01 |
| `SessionStylesBindingTests.Vm()` → `Les_8_styles…`, `Les_trois_gestes…`, `Dans_les_huit_menus…` (:49-64) | la source fixe contient un `Unknown` et asserte `5` éléments ⇒ 4 |
| `SessionStylesBindingTests.Le_style_Pastilles_n_a_plus_qu_un_seul_separateur_visible_par_session` (:151) | 5 séparateurs ⇒ 4 |

Restent verts par construction (à vérifier, pas à modifier) : tout `ArbitrageSessionsTests` (rang figé),
`InspectionSessionsTests.Combien_de_sessions_d_un_corpus_realiste…` (hooks seuls), `Permuter_l_ordre_rendu…`,
`TranscriptSousAgentsTests` (lignes sans `timestamp` ⇒ repli mtime), `TreatedSessionsTests`,
`BattementsCoeurTests`, `GesteTraiteTests`, `ContratHooksDocumenteTests` (si rôles et §1 bougent ensemble).

**Baseline réelle : 896 tests verts** (exécuté le 2026-09-25 : `dotnet test Chronos.sln --nologo`, 8 s de
tests, 16 s de mur avec la compilation) — et non 889 : les 7 tests du cran 1 de R3 (`087c95a`, `5523be7`) ont
été ajoutés après l'audit.

---

## Q2 — Le couplage R4 : où, et comment le rompre sans changer un seul arbitrage

**Où.** `AffichageSessions.Urgence` a exactement quatre appelants : `Ordonner` (écran),
`DiagnosticService` l. 489 et l. 543 (tri des listes du rapport — ils DOIVENT suivre l'écran), et
`ArbitrageSessions.Departager` l. 125-126 (rang 3). C'est le seul consommateur de nature différente.

**Ce que le rang 3 tranche réellement.** `Departager` compare (1) l'instant, (2) la source, (3) l'état,
(4) le motif, (5) le projet. Le rang 3 n'a donc la parole que pour **deux signaux de la même session, du même
instant à la milliseconde ET de la même source**. Le seul couple d'états dont l'ordre relatif change en phase 28
est **(`WaitingDeduced`, `Working`)** : écran 2 < 3, arbitrage 3 > 2. C'est lui, et lui seul, qui peut prouver le
découplage.

**Le corpus existant est muet sur R4 — vérifié ligne à ligne.** `ArbitrageSessionsTests.Corpus()` (:32-40) :
`s1` tranché au rang 1 (fraîcheur), `s2` au rang 2 (hook contre transcript du même instant), `s3` et `s4`
seuls. **Aucun couple n'atteint le rang 3.** Rejouer ses 720 permutations « avec l'ordre d'écran muté » serait
vert que l'arbitrage soit couplé ou non. Il faut un corpus qui descend au rang 3 (Pattern 2) — six signaux,
donc toujours 720 permutations, et le helper `Permutations<T>` existe déjà (:43-53).

**Une incohérence latente, héritée de la phase 24 (LOW, pour information).** `WaitingDeduced` et `Unknown`
partagent le rang 3 ; à motif et projet égaux, deux tels signaux sont ex aequo et `OrderBy` (stable) rend
l'ordre d'entrée. La XML-doc (l. 61-71) affirme pourtant un « ordre TOTAL » qui « épuise tous les champs ».
Pratiquement inatteignable (il faudrait deux fichiers d'une même source pour une même session à la même
milliseconde, dont l'un d'activité illisible). **Ne PAS séparer les deux rangs** (cela changerait des résultats
aujourd'hui départagés par le motif) ; si l'on veut rendre l'affirmation vraie, ajouter un **rang 6** :
`((int)a.Session.Activity).CompareTo((int)b.Session.Activity)` — il ne peut modifier aucun résultat qui était
déterministe. À la discrétion du planificateur.

**Garde de structure existante à respecter.** `GardesPerimetreTests.L_arbitrage_ne_connait_ni_horloge_ni_magasin_ni_chemin`
(:232-260) exige qu'`ArbitrageSessions` n'ait **qu'une** méthode `public static` (`Trancher`) et aucun champ
statique de type `string`, `IClock`, `TreatedStore`, `ArchiveStore`. `RangArbitrage` doit donc être `private`
(ou `internal`) ; un `switch` est plus simple qu'un dictionnaire.

---

## Q3 — `AskUserQuestion` dans les transcripts réels (lecture seule, 2026-09-25)

**Population.** 1 425 `.jsonl` sous `~/.claude/projects` ; **224** lignes `assistant` portant un `tool_use`
`AskUserQuestion` dans les transcripts principaux (hors sous-agents), sur ~20 sessions et 5 projets.

**Forme exacte du bloc question** (224 / 224) :
- ligne `{"type":"assistant","isSidechain":false,"timestamp":"<ISO Z>", …, "message":{"role":"assistant","stop_reason":"tool_use","content":[ <bloc> ]}}`
- `<bloc>` = `{"type":"tool_use","id":"toolu_…","name":"AskUserQuestion","input":{"questions":[{"question":…,"header":…,"multiSelect":false,"options":[{"label":…,"description":…}]}]},"caller":{"type":"direct"}}`
  (`caller` vaut `{"type":"direct"}` dans 224 cas sur 224 ; Chronos ne le lit pas).

**Forme exacte de la réponse** : ligne `{"type":"user","timestamp":…,"message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_…","content":"…"}]},"toolUseResult":{"questions":[…],"answers":{…}}}`.
Le `content` commence par « Your questions have been answered: » (194), « The user answered: » (~25),
« The user doesn't want to proceed… » (1, `is_error: true`, Échap) ou « The user did not answer… » (1). Dans
tous les cas c'est une ligne `user` ⇒ `Working` : la règle verrouillée n'a pas à distinguer ces variantes.

**Ce qui s'écrit PENDANT que la question attend** — le fait qui compte pour SIL-01 (Piège 1) : des lignes
**sans horodatage** — `last-prompt` (50 questions sur 224), `custom-title` (43), `bridge-session` (40),
`atis-latch` (40), `mode` (26), `agent-name` (16) — et, avec horodatage mais non significatives,
`queue-operation` (13) et `frame-link` (12).

**Extrait réel, anonymisé et réduit** (session JARVIS `939eb30a…`, question à 06:55:33.967Z, réponse à
06:59:44.051Z ; valeurs tronquées, forme intacte) :

```jsonl
{"parentUuid":"…","isSidechain":false,"message":{"role":"assistant","stop_reason":"tool_use","content":[{"type":"tool_use","id":"toolu_01Ng…","name":"AskUserQuestion","input":{"questions":[{"question":"BLOQUANT — …","header":"Périmètre","multiSelect":false,"options":[{"label":"Lecture + N1 (Recommandé)","description":"…"},{"label":"Lecture seule","description":"…"}]}]},"caller":{"type":"direct"}}]},"type":"assistant","timestamp":"2026-09-24T06:55:33.967Z","cwd":"C:\\…\\PROJET JARVIS","sessionId":"939eb30a-…"}
{"type":"last-prompt","lastPrompt":"…","leafUuid":"…","sessionId":"939eb30a-…"}
{"type":"custom-title","customTitle":"…","sessionId":"939eb30a-…"}
{"type":"agent-name","agentName":"…","sessionId":"939eb30a-…"}
{"type":"atis-latch","atis":"…","sessionId":"939eb30a-…"}
{"type":"bridge-session","sessionId":"939eb30a-…","bridgeSessionId":"cse_…","lastSequenceNum":0}
{"parentUuid":"…","isSidechain":false,"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_01Ng…","content":"Your questions have been answered: …"}]},"toolUseResult":{"questions":[…],"answers":{"…":"…"}},"timestamp":"2026-09-24T06:59:44.051Z","cwd":"C:\\…\\PROJET JARVIS","sessionId":"939eb30a-…"}
```

**Fixtures recommandées** (précédent DEL-06 : une fixture régénérée par sérialisation ne prouve rien — extraire
les lignes réelles puis les réduire, en lecture seule) :
- `tests/Chronos.Tests/TestData/transcript-question-en-suspens.jsonl` — une ligne `user` d'invite, la ligne
  `assistant` de la question, puis les 5 lignes de métadonnées ;
- `tests/Chronos.Tests/TestData/transcript-question-repondue.jsonl` — la même, suivie de la ligne `tool_result`.
Source réelle : `~/.claude/projects/C--Users-Tanguy-Documents-PROGRAMMES-DEV-PROJET-JARVIS/939eb30a-8200-4c6e-b89f-49ef26260e92.jsonl`
(lire seulement). Chargement par `[CallerFilePath]` + `TestData/`, motif de `ClaudeSettingsReconcilerTests:24-25`,
puis **copie** dans une racine temporaire (la source lit un dossier de projets) et `File.SetLastWriteTimeUtc`.
Les horodatages de la fixture étant du 2026-09-24, le test **injecte** `now` = instant de la question + quelques
minutes (la source compare à `now`, pas à l'horloge).

**Rappel MESURÉ de la règle verrouillée.** Pour chacune des 222 questions répondues, le transcript a été coupé
juste avant la ligne de réponse (l'instant où la question est à l'écran) puis classé :
- règle verrouillée (« le dernier `tool_use` de la dernière ligne `assistant` s'appelle `AskUserQuestion`, et
  aucune ligne `user` après ») : **221 / 222** reconnues en attente ;
- règle par identifiant (`tool_use_id` sans `tool_result`) : 222 / 222 ;
- juste après la réponse : **0** encore classée attente, pour les deux règles.
Le seul cas manqué est un appel d'outils **parallèle** (15 questions sur 224 partagent leur message avec un autre
`tool_use` ; Claude Code écrit un bloc par ligne) : la dernière ligne porte l'autre outil. Il se **dégrade vers
« Réflexion »**, c'est-à-dire vers le comportement v1.6, jamais vers une attente inventée. **Garder la règle
verrouillée** ; ne pas introduire le suivi par identifiant.

**Côté hooks — MEDIUM, à vérifier in vivo.** Sources externes : `PermissionRequest` se déclenche pour
`AskUserQuestion`, questions dans `tool_input` (mesure tierce du 2026-09-23, PR stupart/conch #374) ;
`Notification` part avec `notification_type: "permission_prompt"` (issue anthropics/claude-code #74052, v2.1.200,
juillet 2026 — **vetoé** par Chronos, `SessionHookProcessor.cs:48-52`) ; le hook `Elicitation` ne part pas
(issue #44326). `AskUserQuestion` étant un outil, `PreToolUse` part aussi (battement ⇒ `Working`). Sur cette
machine, les 8 groupes Chronos 3.1.0 sont installés (`~/.claude/settings.json`, lu). Conséquence : Piège 3.
Aucune trace locale ne permet de trancher (les fichiers d'état sont réécrits ; les transcripts ne consignent que
les hooks qui produisent une sortie ou une erreur).

---

## Q4 — SIL-01 : coût mesuré, et où placer la règle

### 4.a Le coût — MESURÉ (pas estimé)

Méthode : copie fidèle de `TranscriptSessionSource.Read/Classify` (v3.1.0) dans une console .NET 8 Release
(bloc-notes de session, hors dépôt), fenêtre paramétrée, sur le vrai `~/.claude/projects`, lecture seule ;
1 appel froid puis 30 cycles chauds. 2026-09-25, vers 20 h 10 heure locale.

| | 15 min (aujourd'hui) | 8 h (SIL-01) |
|---|---|---|
| `.jsonl` énumérés | 1 425 dans 201 dossiers (1 342 sous-agents = 94,2 %) | idem |
| candidats après pré-filtres | 1 | **18** (13 transcripts principaux + 5 `subagents/workflows/wf_*/journal.jsonl`, voir Piège 11) |
| fichiers lus (queue 64 Ko) | 1 | 12 (arrêt à `MaxSessions`) |
| cycle chaud, médiane | 12,0 ms (dont énumération 11,3) | **19,6 ms** (dont énumération 12,7) |
| cycle chaud, p90 / max | 12,9 / 13,5 ms | 21,5 / 22,6 ms |
| premier appel (JIT) | 29,9 ms | 21,0 ms |

**Verdict : ~7 ms de plus par cycle de 2 s, 2,5 fois sous le seuil de 50 ms. Pas de cache.** Classer tous les
candidats au lieu de s'arrêter à 12 (nécessaire, Pattern 3) coûte ~0,6 ms par fichier supplémentaire.
L'énumération (déjà payée aujourd'hui) reste le poste dominant.

### 4.b La découverte : l'mtime n'est pas l'instant du signal

| Instant | Fait relevé |
|---|---|
| 2026-09-25 16:58:18-20 | **12** transcripts principaux reçoivent `bridge-session`, `last-prompt`, `cost-state` (aucun horodatage) — fermeture/redémarrage de l'app bureau. Leurs derniers vrais messages : du **2026-09-23 12:16Z** au 2026-09-25 14:48Z. Le dossier `%APPDATA%\Chronos\sessions` ne contient plus que 2 fichiers (session courante + un orphelin du 2026-09-13) ; son mtime (16:59:27) suggère, sans le prouver, que des `SessionEnd` ont supprimé les autres |
| sémantique mtime, horizon 8 h | **13** sessions visibles à T (12 après `MaxSessions`), toutes « En attente », dont des sessions muettes depuis deux jours, datées « il y a 3 h » |
| sémantique horodatage du dernier message | **5** sessions visibles à T (la session courante et quatre tours finis entre 3 h 34 et 6 h 28 plus tôt) |
| pendant une question ou une permission | 5 lignes sans horodatage entre la question et la réponse (extrait Q3) : l'mtime **avance pendant l'attente** |

Couverture de l'horodatage : **100 %** des lignes `user` (21 200) et `assistant` (37 370) des transcripts
principaux portent `timestamp` (ISO 8601, suffixe `Z`) ; les types de métadonnées (`bridge-session`,
`last-prompt`, `custom-title`, `atis-latch`, `mode`, `agent-name`, `cost-state`) n'en portent **aucun**.

Trois conséquences de la sémantique mtime une fois portée à 8 h — chacune suffit à la rejeter :
1. **Fantômes en masse** après chaque fermeture de l'app : douze « En attente » pendant 8 h, `MaxSessions` saturé.
2. **NET-03 dévoyé** : `SessionTreatmentTracker` date l'épisode d'attente par `UpdatedAt` (l. 64-65, 96-100) ;
   une ligne de métadonnées fait avancer `UpdatedAt`, donc l'épisode, donc purge `treated.json` (l. 104-106) :
   une session « marquée traitée » **revient sans rien avoir redemandé**. Même mécanique pour la phase 30.
3. **FUS-01 faussé** : une permission en attente (`PermissionRequest` à T2) perd contre le transcript `Working`
   de la même session si une ligne de métadonnées est écrite après T2 — « Réflexion » affiché pendant qu'un
   prompt de permission est à l'écran, premier des trois signes du §5.5 du contrat.

**Recommandation (HIGH sur les faits, décision à consigner — voir Question ouverte 1) :** l'instant d'un signal
de transcript est l'horodatage de sa **dernière ligne significative** (`assistant` / `user` non sous-agent), borné
par l'mtime (`min`), avec repli sur l'mtime si absent ou illisible. C'est la doctrine TRT-02 appliquée à la
source : « l'instant que le SIGNAL porte, jamais celui du guetteur ». Les tests existants restent verts (leurs
lignes n'ont pas de `timestamp` ⇒ repli mtime).

### 4.c Où placer la règle de silence — UNE implémentation, dans le moniteur

Aujourd'hui la règle vit dans `SessionMonitor.TryRead` (l. 178-179), c'est-à-dire **pour les seuls fichiers de
hook** ; les signaux transcripts entrent dans l'arbitrage bruts (l. 89-90). Recommandation :
- extraire `private static SessionSnapshot AppliquerSilence(SessionSnapshot s, DateTimeOffset now)` dans
  `SessionMonitor` ;
- l'appliquer à **tous** les signaux dans la boucle de collecte, **avant** `ArbitrageSessions.Trancher`
  (l. 113) — même position qu'aujourd'hui pour les hooks, donc aucun résultat hook ne change ;
- retirer les deux lignes de `TryRead` (qui garde, lui, l'abandon à 8 h et le compteur `perimee`) ;
- la source transcripts ne contient **aucune** occurrence de `WaitingDeduced` (garde textuelle).

L'abandon à 8 h, lui, s'applique dans la source pour les transcripts (le `ActiveWindow` que nomme le contexte),
sur l'**horodatage** : un transcript dont le dernier message a plus de 8 h ne doit pas consommer d'emplacement
`MaxSessions`. L'mtime reste un **pré-filtre d'économie** (exact : horodatage ≤ mtime, donc un mtime de plus de
8 h exclut à coup sûr). Les deux usent de `HorizonsSessions.Abandon`.

**Effet de bord à connaître (sans action) :** `BalayageMagasinSessions` utilise une `new TranscriptSessionSource()`
comme attestation de vie (`App.xaml.cs:263-266`). Elle s'élargit de 15 min à 8 h ⇒ le balayage supprime
**moins** : sens sûr. `BalayageMagasinSessionsTests` a sa propre source fixe : inchangé.

---

## Q5 — La chaîne des horizons

| Constante actuelle | Valeur | Où | Devient |
|---|---|---|---|
| `TranscriptSessionSource.ActiveWindow` (privée) | 15 min | `TranscriptSessionSource.cs:31` | **disparaît** : remplacée par `HorizonsSessions.Abandon` |
| `SessionMonitor.SilenceDesBattements` (privée) | 20 min | `SessionMonitor.cs:29` | `HorizonsSessions.Silence` |
| `SessionMonitor.DropAfter` (privée) | 8 h | `SessionMonitor.cs:30` | `HorizonsSessions.Abandon` |
| `TreatedStore.RetentionMax` (privée) | 24 h | `TreatedStore.cs:44` | `HorizonsSessions.RetentionTraitees` |
| `BalayageMagasinSessions.ExpirationEtat` (**publique**) | 72 h | `BalayageMagasinSessions.cs:47` | `HorizonsSessions.ExpirationEtat` (garder le membre public comme alias : sa XML-doc et sa visibilité sont lues ailleurs) |

Type unique `public static class HorizonsSessions` dans `Services/` (Pattern 4). Deux gardes :
1. **La chaîne** : `Silence < Abandon < RetentionTraitees < ExpirationEtat` — trois assertions d'une ligne
   (recommandation n° 2 de l'audit v1.6). Optionnel : `ExpirationEtat >= 9 × Abandon` (le « huit fois neuf » de
   l'audit §6).
2. **Le câblage** (sans elle, la chaîne serait vraie pendant qu'un fichier réintroduit un littéral privé) :
   texte de `TranscriptSessionSource.cs`, `SessionMonitor.cs`, `TreatedStore.cs`, `BalayageMagasinSessions.cs`
   contient `HorizonsSessions.` et plus aucune déclaration `ActiveWindow`, `DropAfter`, `SilenceDesBattements`,
   `RetentionMax` ; `HorizonsSessions.Silence` n'est référencé que dans `SessionMonitor.cs`. Motif :
   `GardesPerimetreTests.CheminSources()`.

Mettre à jour dans le même geste `docs/hooks-contract.md:145` (`SilenceDesBattements`, `DropAfter`) et `:225`
(la conflation `DropAfter` / balayage de la réserve R10 peut être corrigée au passage).

---

## Q6 — `Unknown` sans ligne : où le retirer

**Au moniteur, avec un motif nommé — pas au ViewModel, pas par `CollectionView`, pas par `Visibility`.**

- `LectureSessions` (XML-doc l. 31-32) promet : « `Visibles` est, mot pour mot, ce que le widget affiche », et
  `Read(now)` = `Inspecter(now).Visibles` (`SessionMonitor.cs:69`) ; le diagnostic liste « Sessions AFFICHÉES
  par le widget » à partir de `Visibles` (`DiagnosticService.cs:530-533`). Filtrer au VM casserait OBS-01 :
  le rapport afficherait une ligne que l'écran n'a pas.
- Une `Visibility="Collapsed"` par gabarit laisserait l'`Unknown` compté dans `TotalCount`, `WaitingCount`, le
  libellé « Tout marquer traité (N) » et l'échantillonnage des styles en rangée (Jetons/Sonar/Veilleurs garderaient
  une case, `Margin` compris) — sur 8 gabarits, 8 occasions de rater.
- Au moniteur : ajouter `MotifMasquage.Indeterminee` (`LectureSessions.cs:15-22`) et, dans la boucle des filtres
  (`SessionMonitor.cs:131-136`), **après** archivée et traitée (le geste de l'utilisateur prime, comme
  aujourd'hui), `if (!AffichageSessions.AUneLigne(s.Activity)) → Masquees`. Le diagnostic la liste alors dans
  « Sessions MASQUÉES par un filtre » avec `LibelleMotif` (« état indéterminé : signal illisible — aucune ligne
  dans le widget »). `TotalCount`, `WaitingCount`, `Summary` et « Tout marquer traité (N) » l'excluent sans une
  ligne de code de plus. Le détecteur de traitement observe les vainqueurs **avant** les filtres (l. 118-119) :
  inchangé.
- `Etat(Unknown)` doit rester non vide (garde `Chaque_valeur_de_l_enumeration_a_un_libelle_non_vide`, :29-37) :
  il n'est plus lu que par le diagnostic (masquées, désaccords). Proposer « indéterminé » — un mot qui n'est pas
  « inconnu » et n'apparaît jamais dans le widget (Question ouverte 3).

Qui produit `Unknown` : **uniquement** `SessionMonitor.TryRead` quand `activity` est illisible (l. 163-164) ; les
transcripts n'en produisent jamais. Effet de bord : si le signal le plus récent d'une session est un hook
illisible, la session quitte le widget (au lieu de s'y montrer grise) et apparaît au diagnostic avec son motif.

---

## Standard Stack

**Aucune dépendance nouvelle.** Tout ce qu'il faut est déjà dans le dépôt.

| Brique | Version | Usage dans cette phase |
|---|---|---|
| .NET 8 (`net8.0-windows`), WPF, SDK 10.0.201 installé | runtimes 8.0.25 (NETCore + WindowsDesktop) | inchangé |
| `System.Text.Json` (`JsonDocument`) | intégré | lecture des lignes de transcript, déjà utilisée par `Classify` |
| `UsageNormalization.InstantDepuisIso` | dépôt (`Services/UsageNormalization.cs:124-127`) | **seul** point autorisé pour convertir le `timestamp` ISO (garde HDR-05) |
| CommunityToolkit.Mvvm | 8.4.2 | `[ObservableProperty] _isDeduced` |
| xUnit | 2.9.2 | tests |
| Xunit.StaFact | 1.1.11 | `[WpfFact]` + `[Collection("XAML WPF")]` pour la matrice 8 styles × 9 thèmes |
| Microsoft.NET.Test.Sdk | 17.11.1 | runner |

---

## Architecture Patterns

### Pattern 1 — Le producteur unique porte les mots, le prédicat et le texte

```csharp
// Services/AffichageSessions.cs — couche NEUTRE (aucun type WPF)
public const string Reflexion = "Réflexion";
public const string EnAttente = "En attente";
public const string EnAttenteDeduite = "En attente ?";   // espace ordinaire U+0020, comme « à toi ? déduit »

/// <summary>LE prédicat « est une attente ». Unique et nommé : c'est le point d'entrée de la règle « lue »
/// (phase 30). Le ViewModel (IsWaiting, WaitingCount) le lit ; le détecteur garde le sien, tenu égal par test.</summary>
public static bool EstUneAttente(SessionActivity a)
    => a is SessionActivity.WaitingAttention or SessionActivity.WaitingTurn or SessionActivity.WaitingDeduced;

/// <summary>Une session a-t-elle une LIGNE dans le widget ? L'état indéterminé n'en a plus (LIB-01).</summary>
public static bool AUneLigne(SessionActivity a) => a is not SessionActivity.Unknown;

/// <summary>ORDRE D'ÉCRAN, et seulement lui (LIB-04). L'arbitrage ne le lit PAS : il a son propre rang,
/// figé (ArbitrageSessions.RangArbitrage, réserve R4 de l'audit v1.6).</summary>
public static int Urgence(SessionActivity a) => a switch
{
    SessionActivity.WaitingAttention => 0,   // permission, demande du bus, question posée
    SessionActivity.WaitingTurn => 1,
    SessionActivity.WaitingDeduced => 2,     // « En attente ? » passe DEVANT « Réflexion »
    SessionActivity.Working => 3,
    _ => 4,                                  // Unknown : aucune ligne à l'écran ; dernier au diagnostic
};

public static string Etat(SessionActivity a) => a switch
{
    SessionActivity.WaitingAttention or SessionActivity.WaitingTurn => EnAttente,
    SessionActivity.WaitingDeduced => EnAttenteDeduite,
    SessionActivity.Working => Reflexion,
    _ => "indéterminé",   // lu par le DIAGNOSTIC seulement : Unknown n'a pas de ligne dans le widget
};
```

Le texte d'activation (R9) naît ici aussi, construit à partir des constantes et de `HorizonsSessions` — il ne
peut donc plus dériver des mots de l'écran, et un test non-WPF le lit. `SessionsController.Enable` ne fait plus
que l'afficher.

### Pattern 2 — `RangArbitrage` privé, figé, et le test qui le rend falsifiable

```csharp
// Services/ArbitrageSessions.cs
c = RangArbitrage(a.Session.Activity).CompareTo(RangArbitrage(b.Session.Activity));   // 3. l'état

/// Rang d'état PROPRE à l'arbitrage, FIGÉ aux valeurs de la phase 24. Il ne suit PAS l'ordre d'écran
/// (AffichageSessions.Urgence), qui a changé en phase 28 : un classement cosmétique ne doit pas pouvoir
/// modifier la règle de FUS-01 (réserve R4). Le changer, c'est changer FUS-01 — un test le tient.
private static int RangArbitrage(SessionActivity a) => a switch
{
    SessionActivity.WaitingAttention => 0,
    SessionActivity.WaitingTurn => 1,
    SessionActivity.Working => 2,
    _ => 3,   // WaitingDeduced ET Unknown, ex aequo comme en phase 24
};
```

```csharp
// tests — corpus qui DESCEND au rang 3 (six signaux ⇒ 720 permutations)
private static SignalSession[] CorpusRang3() => new[]
{
    Hook("r1", SessionActivity.WaitingDeduced, T),              // l'écran la place DEVANT…
    Hook("r1", SessionActivity.Working, T),                     // …l'arbitrage retient celui-ci
    Transcript("r2", SessionActivity.WaitingAttention, T.AddMinutes(-1)),   // question (LIB-02)
    Transcript("r2", SessionActivity.Working, T.AddMinutes(-1)),
    Hook("r3", SessionActivity.Unknown, T.AddMinutes(-2)),
    Hook("r3", SessionActivity.WaitingTurn, T.AddMinutes(-2)),
};

[Fact]
public void Remonter_la_deduction_a_l_ecran_ne_change_aucun_arbitrage()
{
    // NON-VACUITÉ : l'écran place bien la déduction devant le travail…
    Assert.True(AffichageSessions.Urgence(SessionActivity.WaitingDeduced)
              < AffichageSessions.Urgence(SessionActivity.Working));
    // …et 720 ordres d'arrivée donnent UN résultat, où l'arbitrage retient le travail.
    var distincts = Permutations(CorpusRang3()).Select(p => Canonique(ArbitrageSessions.Trancher(p))).ToHashSet();
    var r = Assert.Single(distincts);
    Assert.Contains("r1=Working", r);
    Assert.Contains("r2=WaitingAttention", r);
    Assert.Contains("r3=WaitingTurn", r);
}
```

**Contrôle de mutation (à faire par le vérificateur, précédent phase 26) :** remplacer temporairement
`RangArbitrage` par `AffichageSessions.Urgence` ⇒ `r1=WaitingDeduced` ⇒ le test rougit. Plus une garde
textuelle : `ArbitrageSessions.cs` ne contient pas `AffichageSessions.Urgence`.

### Pattern 3 — Transcript : l'instant du signal, la question, l'abandon

```csharp
// Services/TranscriptSessionSource.cs — dans Classify, par ligne significative
if (type == "assistant")
{
    state = DernierOutil(o) switch      // switch sur string = comparaison ORDINALE, exacte
    {
        null => SessionActivity.WaitingTurn,
        "AskUserQuestion" => SessionActivity.WaitingAttention,   // LIB-02 : une question n'est pas une réflexion
        _ => SessionActivity.Working,
    };
    instant = Horodatage(o);
}
else if (type == "user") { state = SessionActivity.Working; instant = Horodatage(o); }

// …après la boucle : l'instant que le SIGNAL porte, borné par l'écriture du fichier, repli sur l'mtime
var ecriture = new System.DateTimeOffset(fi.LastWriteTimeUtc, System.TimeSpan.Zero);
var updatedAt = instant is { } t && t <= ecriture ? t : ecriture;
var reason = state == SessionActivity.WaitingAttention ? "AskUserQuestion" : null;   // motif = fait observé

private static System.DateTimeOffset? Horodatage(JsonElement o)
    => o.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String
        ? UsageNormalization.InstantDepuisIso(ts.GetString())   // HDR-05 : jamais DateTimeOffset.TryParse ici
        : null;

// Dernier bloc tool_use de la ligne (Claude Code écrit un bloc par ligne) ; null s'il n'y en a pas.
private static string? DernierOutil(JsonElement o) { /* parcours de message.content, garde le dernier "name" */ }
```

```csharp
// Read : l'mtime pré-filtre (économie), l'horodatage décide (autorité), puis tri et limite
var candidats = /* énumération + EstSousAgent */ .Where(f => now - mtime(f) < HorizonsSessions.Abandon).ToList();
return candidats.Select(fi => Classify(fi, now))
                .Where(s => s is not null && now - s.UpdatedAt < HorizonsSessions.Abandon)
                .OrderByDescending(s => s!.UpdatedAt)
                .Take(MaxSessions)            // SRC-03 : la limite porte sur les sessions RETENUES
                .ToList()!;
```

### Pattern 4 — `HorizonsSessions` et la règle de silence en un point

```csharp
// Services/HorizonsSessions.cs
/// <summary>La chaîne des horizons du widget de sessions, en UN seul endroit (SIL-01, recommandation n° 2 de
/// l'audit v1.6). Chaque inégalité a une raison : se taire avant de disparaître (20 min < 8 h) ; ne jamais
/// oublier un « traité » dont la session est encore lisible (8 h < 24 h) ; ne jamais balayer ce que le widget
/// pourrait montrer (24 h < 72 h). Une garde rougit si la chaîne se défait.</summary>
public static class HorizonsSessions
{
    public static readonly System.TimeSpan Silence = System.TimeSpan.FromMinutes(20);
    public static readonly System.TimeSpan Abandon = System.TimeSpan.FromHours(8);
    public static readonly System.TimeSpan RetentionTraitees = System.TimeSpan.FromHours(24);
    public static readonly System.TimeSpan ExpirationEtat = System.TimeSpan.FromHours(72);
}

// Services/SessionMonitor.cs — appliqué à TOUS les signaux, avant l'arbitrage
private static SessionSnapshot AppliquerSilence(SessionSnapshot s, System.DateTimeOffset now)
    => s.Activity == SessionActivity.Working && now - s.UpdatedAt > HorizonsSessions.Silence
        ? s with { Activity = SessionActivity.WaitingDeduced }
        : s;
```

### Pattern 5 — Drapeaux de gabarit : `IsGhost` meurt, `IsDeduced` naît

- `IsGhost` n'a plus de cas vrai (aucun `Unknown` à l'écran). Le **retirer** de `SessionItemVm`, de
  `SessionsPreviewViewModel.Add` et des **six** gabarits qui le lisent : Jetons `:146`, Sonar `:206`, Façade
  `:238`, Étagère `:285`, Annonciateur `:341`, Veilleurs `:393-396`. Un binding sur une propriété disparue
  **n'échoue pas** : il se tait (Piège 7) — d'où une garde textuelle « `IsGhost` absent de `SessionStyles.xaml` ».
- `IsDeduced = (Activity == WaitingDeduced)`. `IsTurn` continue d'inclure la déduction (forme de la famille des
  attentes : bascule de l'Étagère `:281`, pupille des Veilleurs `:379`), `IsWaiting` aussi (lever des Jetons,
  ping du Sonar, yeux des Veilleurs). La règle mécanique la plus sûre : **chaque déclencheur `IsGhost` devient un
  déclencheur `IsDeduced` à opacité ≥ 0,6** (proposé : 0,7) — là où le fantôme était, la déduction s'atténue sans
  s'effacer. Pastilles et Marge n'ont aucun déclencheur fantôme : le mot et son « ? » suffisent (option :
  0,75 sur la pastille ou le liseré).
- Garde : expression régulière sur `SessionStyles.xaml` — toute `DataTrigger Binding="{Binding IsDeduced}"` pose
  une `Opacity` ≥ 0,6 ; aucune occurrence de `IsGhost`.

### Pattern 6 — Les mots visibles sur les 8 styles (critère 1)

Seuls **Pastilles** (`:70`) et **Marge** (`:108`) affichent `StateText`. Les six autres ne montrent l'état que
par forme et couleur ; leur seul canal textuel est l'info-bulle, qui lit `Project` dans les **huit** gabarits
(`:50, :88, :128, :171, :225, :257, :328, :363`). Pour que « une session qui travaille affiche « Réflexion » sur
les 8 styles » soit vérifiable, une propriété `Infobulle` = `$"{Project} — {StateText}"` (mots tirés du
producteur) remplacerait `{Binding Project}` dans les huit — et la phase 29 (APP-02 : titre en info-bulle) y
trouvera sa place. Voir Question ouverte 2.

Troncature : `SessionsWindow` est en `SizeToContent="WidthAndHeight"`, sans `MaxWidth` ; dans Pastilles et Marge le
`TextBlock` d'état n'a ni `TextTrimming` ni `MaxWidth` (seul le nom de projet est rogné). « En attente ? »
(12 caractères) est plus court que « à toi ? déduit » (14), déjà mesuré sur la matrice 8 × 9. La troncature est
donc structurellement impossible ; la garde des 16 caractères et un test WPF « `ActualWidth` ≥ `DesiredSize.Width`
pour le `TextBlock` d'état, sur 9 thèmes » la tiennent.

### Anti-patterns à éviter
- **Filtrer `Unknown` dans le ViewModel** : casse `Visibles` = écran (OBS-01).
- **Relire l'heure dans la source pour la règle de silence** : deux implémentations du silence, et une source
  qui cesse d'être un simple déposant.
- **Rejouer l'ancien corpus de 720 permutations pour R4** : vert quel que soit le couplage.
- **Toucher au corps de `SessionTreatmentTracker.EstAttente`** : garde documentaire croisée.
- **Un libellé en dur « pour la galerie »** : la galerie partage les gabarits, elle doit parler comme l'écran.

---

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser | Pourquoi |
|---|---|---|---|
| Convertir le `timestamp` ISO d'une ligne | `DateTimeOffset.TryParse`, `JsonElement.TryGetDateTimeOffset` | `UsageNormalization.InstantDepuisIso` | garde HDR-05 (`NormalisationUniqueTests:27-44`) ; plancher de sanité 2020 ; `RoundtripKind` |
| Ordre et libellés | un `switch` local dans le VM, la galerie, le diagnostic | `AffichageSessions` | OBS-01 : un producteur, deux consommateurs |
| Masquer `Unknown` | `CollectionView` filtrée, `Visibility` par gabarit | `MotifMasquage` + `Masquees` du moniteur | la liste du rapport et l'écran restent identiques |
| Seuils d'horizon | littéraux privés par fichier | `HorizonsSessions` | la chaîne devient une propriété testée |
| Cache de lecture des transcripts | cache `(chemin, mtime, taille)` | rien | mesuré : 19,6 ms médian à 8 h |
| Permutations | nouvelle routine | `Permutations<T>` / `Canonique` d'`ArbitrageSessionsTests` | déjà éprouvés |
| Lire les sources/doc dans un test | `Assembly.Location`, remontée depuis `AppContext.BaseDirectory` | `GardesPerimetreTests.CheminSources()`, attribut `CheminDocsChronos` | vide en mono-fichier ; injecté par MSBuild |

---

## Common Pitfalls

### Piège 1 — L'mtime rajeunit des sessions mortes et fait revenir des sessions traitées
**Ce qui casse :** 12 fantômes « En attente » pendant 8 h après chaque fermeture de l'app ; NET-03 purge un
« traité » sur une ligne de métadonnées ; une permission perd contre un transcript « Réflexion ».
**Pourquoi :** lignes sans horodatage ajoutées à la fermeture et pendant les attentes (Q4.b).
**Parade :** `UpdatedAt` = horodatage de la dernière ligne significative, borné par l'mtime (Pattern 3).
**Signe :** dans le diagnostic, plusieurs sessions « il y a N h » d'âge identique à la seconde.

### Piège 2 — Un test R4 vert pour la pire raison
Le corpus existant tranche tout au rang 1 ou 2 (Q2). Seul le couple (`WaitingDeduced`, `Working`), même session,
même instant, **même source**, discrimine. Exiger l'assertion de non-vacuité sur `Urgence`.

### Piège 3 — Le battement `PreToolUse` d'une question (sessions à hooks)
**Ce qui peut casser :** si `PermissionRequest` ne part pas pour `AskUserQuestion`, le fichier d'état reste sur le
battement `Working` (instant T1) plus récent que la ligne de question (T0 < T1) ⇒ l'arbitrage (fraîcheur) retient
« Réflexion » — critère 2 manqué dans la configuration réelle de l'utilisateur. **Aujourd'hui (MEDIUM)** : la
mesure tierce du 2026-09-23 dit que `PermissionRequest` part, avec `WaitingAttention` à T2 > T1 : le hook dit
alors « En attente » et gagne. **Parade :** un test d'arbitrage qui DOCUMENTE les deux cas (hook `Working` T1 seul
⇒ `Working` ; hook `WaitingAttention` T2 ⇒ `WaitingAttention`), et une vérification in vivo en lecture seule
(pendant une question affichée dans l'app : `%APPDATA%\Chronos\sessions\<id>.json` doit porter
`"activity":"WaitingAttention","reason":"PermissionRequest"`). Ne PAS lire `tool_name` dans le hook : le contrat
(§2 « Jamais lus, délibérément », §5.6) l'interdit ; un groupe `PreToolUse` à `matcher` `AskUserQuestion`
toucherait le câblage des hooks, hors périmètre de cette phase.

### Piège 4 — Le silence étendu change un test de dette
`Un_hook_deduit_et_un_transcript_travaillant_du_MEME_instant…` (InspectionSessionsTests:484-503) perd son
désaccord. Le reformuler avec un transcript `WaitingTurn` (Q1.c), sinon la dette n° 6 (R5) redevient non écrite.

### Piège 5 — Masquer au mauvais étage
Voir Q6. Symptôme : « Sessions AFFICHÉES : 3 » au rapport pendant que l'écran en montre 2.

### Piège 6 — La garde HDR-05
`TranscriptSessionSource.cs` n'est pas dans les exemptions de `NormalisationUniqueTests` (seuls
`UsageNormalization`, `ClaudeTokenReader`, `SessionMonitor`, `TranscriptActivityProvider`) : un
`DateTimeOffset.TryParse` y rougirait. Utiliser `InstantDepuisIso`.

### Piège 7 — Gabarits partagés, bindings muets
`SessionStyles.xaml` sert à `SessionsViewModel` (fenêtre) **et** `SessionsPreviewViewModel` (galerie
`--sessions`). Une propriété de liste (`LibelleCompteur`) absente de l'un, un `IsGhost` oublié, un `IsDeduced`
non ajouté à `SessionItemVm` : aucun ne lève d'exception, aucun ne rougit dans la matrice 8 × 9 (qui ne mesure que
la taille). D'où les gardes textuelles et un test « la galerie expose toutes les propriétés de liste bindées ».

### Piège 8 — Précédence des valeurs WPF
Une `Opacity` locale bat un `DataTrigger` de style (le halo des Jetons, `Opacity="0.22"` en local, `:150`, ne
bougera pas) ; une animation bat les deux (`PulseAttention`). Poser l'atténuation dans les `Style.Triggers` là où
était le fantôme, jamais en attribut local. `IsAttention` et `IsDeduced` étant exclusifs, le pouls et
l'atténuation ne se combattent jamais.

### Piège 9 — Rôles des hooks et §1 du contrat
Changer `SessionHookInstaller.Cablage[].Role` sans la table §1 (ou l'inverse) rougit
`Chaque_ligne_documentee_porte_le_role_reellement_cable`. Même commit.

### Piège 10 — Surface publique de l'arbitrage
`RangArbitrage` `public` ferait rougir `L_arbitrage_ne_connait_ni_horloge_ni_magasin_ni_chemin` (une seule méthode
`public static` autorisée).

### Piège 11 — `subagents/workflows/wf_*/journal.jsonl`
22 fichiers dans des sous-dossiers de `subagents/` que le pré-filtre (`Directory.Name == "subagents"`) laisse
passer ; 5 sont dans la fenêtre de 8 h à T. Ils ne contiennent que des types `started` / `result` / `failed` ⇒
`Classify` rend `null`, aucun emplacement consommé : **coût seulement** (~0,6 ms chacun). Durcissement facultatif :
écarter tout chemin dont **un** segment vaut `subagents`.

### Piège 12 — Angle mort de la queue de 64 Ko (existant)
1 transcript principal sur 83 a une queue sans aucune ligne significative complète (une ligne finale > 64 Ko) :
il est invisible. Inchangé par la phase ; ne pas le « corriger » par une lecture partielle d'une ligne tronquée.

### Piège 13 — Population et hauteur du widget
Jusqu'à 12 lignes (R8 : ni hauteur maximale ni ascenseur). Avec l'horodatage : 5 à T ; avec l'mtime : 12. La
phase 30 élaguera les sessions lues ; ne pas toucher à `MaxSessions`.

### Piège 14 — `SessionEnd` puis transcript seul
`SessionEnd` supprime le fichier d'état ; le transcript seul garde alors la session « En attente » jusqu'à 8 h après
son dernier message (hier : 15 min). C'est cohérent avec v1.7 (fini **et non lu** ⇒ « En attente ») et c'est
précisément la population que la phase 30 doit connaître (ROADMAP). À écrire, pas à corriger.

### Piège 15 — Moniteur de test sans magasin temporaire
`new SessionMonitor(dir, source)` sans `ArchiveStore` lit le **vrai** `%APPDATA%\Chronos\archived.json`
(`ArchiveStore.cs:33-36`). Lecture seule, mais non déterministe : toujours injecter un `ArchiveStore` temporaire
dans les nouveaux tests.

---

## Code Examples

### Scénarios SIL-01 avec horloge injectée (le tableau du contexte)

```csharp
// Racine TEMPORAIRE ; lignes AVEC horodatage ; now injecté ; ArchiveStore temporaire (Piège 15)
private static readonly DateTimeOffset Now = new(2026, 9, 25, 16, 0, 0, TimeSpan.Zero);

[Fact]
public void Transcript_seul_Working_muet_depuis_25_min_dit_En_attente_interrogatif()
{
    var root = Racine("s-muette", Invite(Now.AddMinutes(-26)), AssistantOutil("Bash", Now.AddMinutes(-25)),
                      mtime: Now.AddMinutes(-25));
    var s = Assert.Single(Moniteur(root).Read(Now));
    Assert.Equal(SessionActivity.WaitingDeduced, s.Activity);
    Assert.Equal("En attente ?", AffichageSessions.Etat(s.Activity));
}

[Fact] public void Transcript_seul_end_turn_depuis_3_h_dit_En_attente() { /* WaitingTurn, « En attente » */ }
[Fact] public void Transcript_seul_de_8_h_01_est_absent() { /* horodatage ET mtime à −8 h 01 : Empty */ }

[Fact]
public void Un_transcript_rajeuni_par_des_metadonnees_garde_l_age_de_son_dernier_message()
{
    // Le cas mesuré du 2026-09-25 à 16 h 58 : dernier message il y a deux jours, mtime il y a une minute.
    var root = Racine("s-fermee", Invite(Now.AddDays(-2)), AssistantFin(Now.AddDays(-2)),
                      "{\"type\":\"cost-state\",\"sessionId\":\"s-fermee\"}", mtime: Now.AddMinutes(-1));
    Assert.Empty(Moniteur(root).Read(Now));
}
```

### LIB-02 sur la fixture réelle

```csharp
[Fact]
public void Une_question_sans_reponse_est_une_attente_et_la_reponse_rend_la_reflexion()
{
    var tQuestion = DateTimeOffset.Parse("2026-09-24T06:55:33.967Z");   // Parse autorisé en TEST, pas dans Services/
    var enSuspens = LireFixture("transcript-question-en-suspens.jsonl", mtime: tQuestion.AddMinutes(1));
    var q = Assert.Single(new TranscriptSessionSource(enSuspens).Read(tQuestion.AddMinutes(2)));
    Assert.Equal(SessionActivity.WaitingAttention, q.Activity);
    Assert.Equal(tQuestion, q.UpdatedAt);            // les 5 lignes de métadonnées ne l'ont pas rajeuni

    var repondue = LireFixture("transcript-question-repondue.jsonl", mtime: tQuestion.AddMinutes(5));
    Assert.Equal(SessionActivity.Working,
                 Assert.Single(new TranscriptSessionSource(repondue).Read(tQuestion.AddMinutes(6))).Activity);
}

[Fact] public void Le_nom_d_outil_se_compare_exactement() { /* "askuserquestion", "AskUserQuestion2" ⇒ Working */ }
```

### Garde documentaire §3 ↔ producteur (extension de `ContratHooksDocumenteTests`)

```csharp
[Fact]
public void Le_paragraphe_3_affiche_les_libelles_du_producteur()
{
    var table = TableSection3(LireDocument());   // lignes « | `Working` | `Réflexion` | … »
    foreach (var a in Enum.GetValues<SessionActivity>().Where(AffichageSessions.AUneLigne))
        Assert.Equal(AffichageSessions.Etat(a), LigneDe(table, a.ToString())[1]);
    Assert.Contains("aucune ligne", LigneDe(table, "Unknown")[1], StringComparison.Ordinal);
}

[Fact]
public void Aucun_ancien_libelle_ne_subsiste_dans_le_contrat_ni_dans_les_gabarits()
{
    string[] anciens = { "`à toi`", "« à toi »", "`tour fini`", "« tour fini »", "`en cours`", "« en cours »",
                         "à toi ? déduit", "`inconnu`" };   // « inconnu » nu reste légitime ailleurs (§5.3)
    // docs/hooks-contract.md, Resources/SessionStyles.xaml, Views/SessionsGalleryWindow.xaml, Views/SessionsWindow.xaml
}
```

---

## Ce qui change par rapport à v1.6

| v1.6 | Phase 28 | Conséquence |
|---|---|---|
| 5 libellés (« à toi », « tour fini », « en cours », « à toi ? déduit », « inconnu ») | 3 mots ; `Unknown` sans ligne | 17 tests à réécrire (Q1.c) |
| Déduction au dernier rang (« ne passe jamais devant une observation ») | Déduction devant Réflexion (écran) ; rang d'arbitrage inchangé | la phrase du §3 se scinde en deux : écran / arbitrage |
| Transcript daté par l'mtime, fenêtre 15 min | daté par son dernier message, horizon 8 h | trou §9.1 refermé sans fantômes |
| Silence appliqué aux seuls hooks | appliqué à tous les signaux, un point | transcript et hook obéissent aux mêmes seuils |
| 4 horizons dans 4 fichiers, 0 garde | 1 type, 2 gardes | la chaîne est une propriété testée |
| Prédicat d'attente recopié 4 fois | `EstUneAttente` + test d'équivalence avec le détecteur | point d'entrée de la phase 30 |

---

## Open Questions

1. **« Dernière écriture » (texte de SIL-01) contre « dernier message ».**
   - Ce qu'on sait : l'mtime avance sans signal (Q4.b, trois défauts mesurés) ; 100 % des lignes significatives
     sont horodatées ; les tests existants restent verts avec le repli mtime.
   - Ce qui reste : la formulation de REQUIREMENTS.md et du critère 4 parle d'« écriture ».
   - Recommandation : horodatage du dernier message ; **consigner la décision** dans le plan et dans le §3 du
     contrat, avec le relevé du 2026-09-25 16:58 comme fondement.
2. **Les mots dans les six styles sans texte.** Recommandation : `Infobulle` = projet + mot (Pattern 6) ; sinon
   écrire explicitement que le critère 1 se vérifie par le texte sur Pastilles/Marge et par forme/couleur ailleurs.
3. **Le mot de diagnostic pour `Unknown`.** Recommandation : « indéterminé » (jamais à l'écran du widget, garde VM).
4. **Casse du compteur de l'Annonciateur** (« 3 En attente » par la constante, ou « 3 en attente »). Recommandation :
   la constante du producteur — la décision verrouillée dit « exactement trois chaînes ».
5. **`PermissionRequest` part-il pour `AskUserQuestion` sur la version de l'utilisateur ?** MEDIUM. Vérification
   en lecture seule pendant une question réelle (Piège 3) — à placer au point de contrôle humain de la phase ou
   de la phase 31.
6. **Rang 6 (ordinal de l'état) dans `Departager`** — facultatif, sans effet sur tout résultat déterministe (Q2).

Observation hors périmètre, pour la phase 29 : les transcripts de l'app bureau portent des lignes
`{"type":"custom-title","customTitle":"…"}` (4 796 lignes) et `agent-name` — le titre de session y figure aussi
(valeur identique relevée sur `939eb30a`). À confronter au `title` des `local_*.json`.

---

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|---|---|---|---|---|
| .NET SDK | build + tests | ✓ | 10.0.201 | — |
| Runtime .NET 8 (NETCore + WindowsDesktop) | tests `net8.0-windows`, `[WpfFact]` | ✓ | 8.0.25 (et 8.0.0, 8.0.21) | — |
| Suite xUnit | validation | ✓ | 896 verts, 8 s (16 s avec compilation) | — |
| `~/.claude/projects` (lecture seule) | extraction des fixtures réelles | ✓ | 1 425 `.jsonl` | fixtures déjà décrites (Q3) |
| Python 3.14 | mesures de cette recherche uniquement | ✓ | 3.14 | non requis par le projet |
| Overlay vivant | — | **à ne pas lancer** | — | galerie `--sessions` + test WPF 8 × 9 |

Aucune dépendance manquante.

---

## Validation Architecture

### Test Framework
| Propriété | Valeur |
|---|---|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`, `[Collection("XAML WPF")]`), Microsoft.NET.Test.Sdk 17.11.1 |
| Config | `tests/Chronos.Tests/Chronos.Tests.csproj` (attributs `CheminSourcesChronos`, `CheminDocsChronos` ; `InternalsVisibleTo Chronos.Tests` côté `src`) |
| Commande rapide (~2 s, 179 tests aujourd'hui) | voir le bloc ci-dessous |
| Suite complète | `dotnet test Chronos.sln --nologo` (896 aujourd'hui, ~8 s de tests) |

```bash
dotnet test Chronos.sln --nologo --no-build --filter "FullyQualifiedName~AffichageSessionsTests|FullyQualifiedName~ArbitrageSessionsTests|FullyQualifiedName~InspectionSessionsTests|FullyQualifiedName~TranscriptSousAgentsTests|FullyQualifiedName~SessionsTests|FullyQualifiedName~ContratHooksDocumenteTests|FullyQualifiedName~SessionStylesBindingTests|FullyQualifiedName~DiagnosticServiceTests"
```
(ajouter les nouvelles classes au filtre, p. ex. `HorizonsSessionsTests`, `TranscriptQuestionTests` ; `--no-build` suppose une compilation préalable)

### Phase Requirements → Test Map
| Req | Comportement | Type | Commande | Existe ? |
|---|---|---|---|---|
| LIB-01 | `Etat` rend exactement « Réflexion » / « En attente » / « En attente ? » ; ≤ 16 car. | unit | `--filter FullyQualifiedName~AffichageSessionsTests` | à réécrire (:17-24) ; garde 16 car. ✅ |
| LIB-01 | `Unknown` : aucune ligne, `TotalCount` l'exclut, le diagnostic le liste masqué avec son motif | unit + intégration moniteur/rapport | `~InspectionSessionsTests`, `~DiagnosticServiceTests` | ❌ Wave 0 |
| LIB-01 | aucun ancien libellé rendu, sur 8 styles × 9 thèmes | WPF | `~SessionStylesBindingTests` | à étendre |
| LIB-01 | §3 du contrat = producteur ; texte d'activation contient les trois mots ; aucun ancien libellé dans doc/XAML | garde texte | `~ContratHooksDocumenteTests` | ❌ Wave 0 |
| LIB-02 | question en suspens ⇒ `WaitingAttention` ; réponse ⇒ `Working` ; métadonnées ne rajeunissent pas | unit (fixture réelle) | `~TranscriptQuestionTests` (nouvelle classe) | ❌ Wave 0 + 2 fixtures |
| LIB-02 | nom comparé exactement | unit | idem | ❌ |
| LIB-02 | arbitrage hook `PreToolUse` / `PermissionRequest` contre la question | unit pur | `~ArbitrageSessionsTests` | ❌ |
| LIB-03 | `StateText` du VM = `Etat` pour chaque état ; `IsWaiting`/`WaitingCount` = `EstUneAttente` ; `EstUneAttente` ≡ `SessionTreatmentTracker.EstAttente` (5 valeurs) | unit | `~AffichageSessionsTests` | partiel (:121-139) |
| LIB-03 | galerie : libellés ⊆ producteur, propriétés de liste présentes ; `IsGhost` absent, `IsDeduced` ≥ 0,6 | garde texte + unit | `~GardesPerimetreTests` / nouvelle | ❌ |
| LIB-04 | `Ordonner` : Attention, Turn, Deduced, Working (même instant), puis fraîcheur | unit | `~AffichageSessionsTests` | à réécrire (:64-75) |
| LIB-04 | R4 : corpus rang 3, 720 permutations, non-vacuité ; `ArbitrageSessions.cs` sans `AffichageSessions.Urgence` | unit pur + garde texte | `~ArbitrageSessionsTests` | ❌ |
| SIL-01 | 25 min ⇒ « En attente ? », 3 h ⇒ « En attente », 8 h 01 ⇒ absent, rajeuni par métadonnées ⇒ absent | intégration (racine temporaire, `now` injecté) | `~SessionsTests` ou `~HorizonsSessionsTests` | à réécrire (:556-565) + ❌ |
| SIL-01 | chaîne `Silence < Abandon < RetentionTraitees < ExpirationEtat` ; 4 consommateurs lisent `HorizonsSessions` ; silence absent de la source | garde | `~HorizonsSessionsTests` | ❌ Wave 0 |
| SIL-01 | `MaxSessions` : tri par `UpdatedAt` (horodatage) avant la limite | unit | `~TranscriptSousAgentsTests` | ✅ (repli mtime) + 1 cas horodaté |

### Sampling Rate
- **Par tâche :** commande rapide ci-dessus.
- **Par vague :** `dotnet test Chronos.sln --nologo`.
- **Porte de phase :** suite complète verte (896 − réécrits + nouveaux, **0 échec**) avant `/gsd:verify-work`, et
  les deux contrôles de mutation : (a) `RangArbitrage` → `AffichageSessions.Urgence` ⇒ le test R4 rougit ;
  (b) `HorizonsSessions.Abandon` porté à 30 h ⇒ la garde de chaîne rougit.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/TestData/transcript-question-en-suspens.jsonl` et `transcript-question-repondue.jsonl`
      (lignes réelles réduites, Q3)
- [ ] `tests/Chronos.Tests/HorizonsSessionsTests.cs` — chaîne + câblage + scénarios SIL-01
- [ ] `tests/Chronos.Tests/TranscriptQuestionTests.cs` — LIB-02
- [ ] extension de `ContratHooksDocumenteTests` (§3 ↔ producteur, anciens libellés, texte d'activation)
- [ ] extension de `ArbitrageSessionsTests` (corpus rang 3, non-vacuité, garde texte)
- Aucun framework à installer.

### Vérifications manuelles (non automatisables sans lancer l'overlay)
- Galerie `--sessions` : les 8 tuiles montrent « En attente », « En attente ? » atténuée mais lisible,
  « Réflexion » ; plus aucun fantôme ; sous-titre de la galerie à jour. (Contrôle humain ; ne pas lancer l'overlay.)
- 9 thèmes : couverts mécaniquement par le test WPF 8 × 9 ; contrôle à l'œil facultatif via la galerie.
- LIB-02 in vivo (Piège 3) : lecture du fichier d'état pendant une question réelle — lecture seule.

---

## Sources

### Primary (HIGH)
- Dépôt, lu le 2026-09-25 : `AffichageSessions.cs`, `ArbitrageSessions.cs`, `TranscriptSessionSource.cs`,
  `SessionMonitor.cs`, `TreatedStore.cs`, `BalayageMagasinSessions.cs`, `SessionTreatmentTracker.cs`,
  `SessionHookProcessor.cs`, `SessionHookInstaller.cs`, `DiagnosticService.cs`, `LectureSessions.cs`,
  `UsageNormalization.cs`, `SessionsViewModel.cs`, `SessionsPreviewViewModel.cs`, `SessionsController.cs`,
  `SessionStyles.xaml`, `SessionsWindow.xaml`, `SessionsGalleryWindow.xaml`, `docs/hooks-contract.md`, et les
  tests cités.
- Mesures sur la machine (lecture seule de `~/.claude/projects` et `%APPDATA%\Chronos\sessions`) : dénombrement,
  chronométrage .NET 8 (copie fidèle de `Read/Classify`), 224 questions réelles, couverture des horodatages,
  rappel des deux règles, relevé du 16:58:20.
- Suite exécutée : `dotnet test Chronos.sln --nologo` ⇒ 896 / 896, 8 s.
- `.planning/v1.6-MILESTONE-AUDIT.md` §5.3 (R4, R9), §6, §8, §9.1, §11.

### Secondary (MEDIUM)
- [stupart/conch PR #374](https://github.com/stupart/conch/pull/374) — `PermissionRequest` part pour `AskUserQuestion` (mesuré le 2026-09-23).
- [anthropics/claude-code #74052](https://github.com/anthropics/claude-code/issues/74052) — `Notification` `permission_prompt` sur `AskUserQuestion` (v2.1.200, juillet 2026).
- [anthropics/claude-code #44326](https://github.com/anthropics/claude-code/issues/44326) — le hook `Elicitation` ne part pas pour `AskUserQuestion`.
- [anthropics/claude-code #59908](https://github.com/anthropics/claude-code/issues/59908) — attente silencieuse sur `AskUserQuestion` côté `Notification`.

### Tertiary (LOW)
- Aucune affirmation de ce document ne repose sur une source LOW seule.

---

## Metadata

**Confidence breakdown :**
- Inventaire des libellés et tests qui rougissent : HIGH — lus ligne à ligne, suite exécutée.
- Découplage R4 : HIGH — l'insuffisance du corpus existant est vérifiée signal par signal.
- Forme d'`AskUserQuestion` dans les transcripts et rappel de la règle : HIGH — 224 cas réels.
- Comportement des hooks Claude Code sur `AskUserQuestion` : MEDIUM — sources tierces récentes, non observé ici.
- Coût SIL-01 : HIGH — mesuré (réserve : cache disque chaud ; premier appel ≤ 30 ms).
- mtime contre horodatage : HIGH sur les faits ; la décision reste à consigner (Question ouverte 1).
- Atténuation visuelle par style : MEDIUM — mécanique sûre, rendu à confirmer dans la galerie.

**Research date :** 2026-09-25
**Valid until :** 2026-10-09 (le format des transcripts et le comportement des hooks de Claude Code évoluent vite ;
refaire les dénombrements de Q3/Q4 si la phase démarre plus tard)
