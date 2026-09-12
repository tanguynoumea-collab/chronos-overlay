---
phase: 21-p-rim-tre-le-widget-ne-parle-que-de-claude-code
verified: 2026-09-12T00:00:00Z
status: human_needed
score: 5/5 must-haves verified (automated) + 3 items require human confirmation in vivo
human_verification:
  - test: "Lancer volontairement l'overlay Chronos (version du dépôt) et vérifier %APPDATA%\\Chronos\\archived.json"
    expected: "Le fichier passe de 84 octets ({\"desktop:foreground:unknown\":...,\"desktop:foreground:code\":...}) à 2 octets ({})"
    why_human: "La contrainte de phase interdit de lancer/tuer l'overlay en cours (pid 119412, en cours d'utilisation par l'utilisateur). Le test unitaire ArchiveStorePurgeTests prouve la méthode, la garde de câblage prouve l'appel dans OnStartup ; ni l'un ni l'autre ne prouve que le VRAI fichier de l'utilisateur a changé — seul un lancement réel le prouve."
  - test: "Chronos.exe --sessions puis parcourir les 8 styles sur les 9 thèmes à l'œil"
    expected: "Une ligne du style Pastilles lit « état · détail » — un seul point médian, aucun séparateur orphelin en fin de ligne, aucune case vide, aucune rangée décalée"
    why_human: "SessionStylesBindingTests monte le vrai BAML et mesure (72 combinaisons, 0 exception, tailles > 0, exactement 1 séparateur par session en Pastilles) mais ne juge pas l'esthétique / l'équilibre visuel — c'est l'œil qui tranche ce dernier point."
  - test: "Après plusieurs heures d'usage avec l'app bureau Claude ouverte au premier plan, observer le widget"
    expected: "Plus aucune ligne préfixée desktop: n'apparaît, et plus rien à archiver à la main"
    why_human: "Critère de succès 1 de la ROADMAP — comportement en usage réel prolongé, non reproductible par un test unitaire sans lancer l'application réelle sur la durée."
---

# Phase 21 : Périmètre — le widget ne parle que de Claude Code — Verification Report

**Phase Goal:** Le widget ne montre plus que des sessions Claude Code réellement vivantes : la source
app-bureau par UI Automation et tout ce qui n'existait que pour elle disparaissent du dépôt, les entrées
fantômes `desktop:foreground:*` s'évaporent y compris celles déjà archivées à la main, et une vague de
sous-agents parallèles ne peut plus faire disparaître la vraie session.

**Verified:** 2026-09-12
**Status:** human_needed (tout ce qui est vérifiable par code/test est VERT ; 3 points restent bloqués sur
un lancement volontaire de l'overlay, que la phase s'interdit explicitement de provoquer)
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (5 critères de succès de la ROADMAP)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Plus une seule session non-Claude Code (SRC-01) | ✓ VERIFIED (code) / ? IN VIVO | 8 fichiers source + 2 fichiers de tests supprimés ; `grep -rn "DesktopUia\|Uia\|IForegroundWatch\|SessionKind\|SessionOrigin\|KindLabel" src/` → 0 résultat. Deux gardes de non-retour falsifiables (réflexion + source XAML), prouvées par mutation. L'effet en usage réel (aucune ligne `desktop:` après usage prolongé) reste à observer par l'utilisateur (item human_verification #3). |
| 2 | Les fantômes déjà archivés disparaissent (SRC-02) | ✓ VERIFIED (code) / ? IN VIVO | `ArchiveStore.PurgerPrefixe` retire du fichier, sans filtre TTL, sans réécriture si rien à retirer ; câblée dans `App.OnStartup` entre `ClaudeSettingsReconciler` et `OfferOnFirstRun` ; garde de câblage sur la source. Le vrai `archived.json` de l'utilisateur reste à 84 octets avec les deux entrées `desktop:foreground:*` intactes — comportement ATTENDU tant que l'overlay n'a pas été relancé (item human_verification #1). |
| 3 | La vraie session survit aux vagues d'agents (SRC-03) | ✓ VERIFIED | `TranscriptSessionSource.Read` : `break` après `Classify` (pas de `Take` sur l'énumération), pré-filtre de chemin `EstSousAgent` doublant (sans remplacer) le filtre de contenu `isSidechain`. Test `Douze_sous_agents_chauds_ne_font_plus_disparaitre_la_vraie_session` rejoue exactement le scénario mesuré (12 sous-agents chauds + 1 vraie session plus ancienne) et passe. |
| 4 | Aucun trou visuel | ✓ VERIFIED | `SessionStylesBindingTests` (72 combinaisons, `[Collection("XAML WPF")]`, `DisableParallelization`) charge le vrai BAML, applique les vrais pinceaux de thème, monte la grille racine (motif corrigé, cf. Anti-Patterns), mesure `DesiredSize > 0` sur les 72 cas, et compte exactement 4 séparateurs "· " visibles (1 par session) en Pastilles. Prouvé falsifiable par mutation (réinsertion d'un second séparateur → compte passe de 4 à 8). Le jugement esthétique final reste humain (item human_verification #2). |
| 5 | Aucune régression de garde | ✓ VERIFIED | `ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`, `GardesDoctrineTests`, `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` tous verts (34 cas au filtre dédié incluant les nouvelles classes phase 21, 0 échec). Critère opérationnel de la phase — **0 échec + justification nominative du recul de couverture** — atteint : 719/719, 0 échec, 2 exécutions consécutives ; 49 tests supprimés nommés un à un dans `21-VALIDATION.md`, chacun rattaché au code disparu qu'il couvrait ; 16 tests ajoutés nommés. |

**Score:** 5/5 truths vérifiées côté code/tests ; 3 sous-points restent `? IN VIVO` (observation humaine différée, non substituable, documentée par la phase elle-même comme telle).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Chronos/Services/TranscriptSessionSource.cs` | Énumération pré-filtrée + limite après classification, `: ISessionSource` | ✓ VERIFIED | Contient `EstSousAgent`, `result.Count >= MaxSessions`, `ToList()` dans le `try`, déclaration `: ISessionSource`. Code lu intégralement, conforme au plan 01. |
| `src/Chronos/Services/ISessionSource.cs` | Commentaire recentré Claude Code, plus de mention app bureau | ✓ VERIFIED | 0 occurrence "app bureau UIA" ; référence `TranscriptSessionSource` comme seule implémentation de production. |
| `src/Chronos/Services/SessionSnapshot.cs` | Record à 5 champs, `SessionKind`/`SessionOrigin` disparus | ✓ VERIFIED | 22 lignes, `enum SessionActivity` intact, record à 5 champs terminé par `UpdatedAt);`. |
| `src/Chronos/Services/SessionMonitor.cs` | Constructeur 5 paramètres, plus d'étape de repli app-bureau | ✓ VERIFIED | `_desktop`/`_foreground`/`IForegroundWatch` absents ; `_tracker?.Observe(raw, now)` présent. |
| `src/Chronos/Services/SessionTreatmentTracker.cs` | `Observe` à 2 paramètres, NET-02 retirée avec épitaphe | ✓ VERIFIED | `FocusAckDelay`/`_focusSince`/`IsForegroundDesktop`/`claudeForeground` absents ; épitaphe NET-02 présente une seule fois. |
| `src/Chronos/Services/ArchiveStore.cs` | `PurgerPrefixe` sans TTL, écriture directe, pas de réécriture si rien à retirer | ✓ VERIFIED | Code lu intégralement ; `Ttl` = 4 occurrences (constante + 2 usages Load/Add + 1 doc `<see cref>`), 0 dans `PurgerPrefixe` ; `File.Move` = 1 (seulement dans `Add`) ; `return ecrit ? retirees : 0`. |
| `src/Chronos/App.xaml.cs` | Appel `PurgerPrefixe("desktop:")` câblé au démarrage, mode overlay uniquement | ✓ VERIFIED | Appel présent entre `ClaudeSettingsReconciler.Reconcile` et `OfferOnFirstRun`, en `try/catch` best-effort. |
| `src/Chronos/Resources/SessionStyles.xaml` | Libellé de type retiré du seul template concerné (Pastilles), 7 autres templates intacts | ✓ VERIFIED | 8 `DataTemplate x:Key=` présents, 2 occurrences `Text="  ·  "` (Pastilles + Marge), 0 `KindLabel`. |
| `src/Chronos/Views/SessionsController.cs` | Message d'activation ne promet plus l'app bureau | ✓ VERIFIED | 0 occurrence "app bureau incluse", message reformulé "Claude Code ACTIVES". |
| `tests/Chronos.Tests/GardesPerimetreTests.cs` | Garde par réflexion + garde de source XAML + garde de câblage, falsifiables | ✓ VERIFIED | 4 `[Fact]` présents, mutations de preuve documentées et revoquées (SUMMARY 21-03/21-04). |
| `tests/Chronos.Tests/SessionStylesBindingTests.cs` | 72 combinaisons chargées/mesurées/disposées, preuve visuelle réelle | ✓ VERIFIED | Motif `Monter()` sur la grille racine (pas la fenêtre) — corrige le défaut initial du plan documenté honnêtement dans le SUMMARY ; `[Collection("XAML WPF")]` présent. |
| `tests/Chronos.Tests/ArchiveStorePurgeTests.cs` | Contenu réel mesuré rejoué | ✓ VERIFIED | `desktop:foreground:unknown` présent, 6 `[Fact]`, tous sous `Path.GetTempPath()` avec assertion de préfixe. |
| `tests/Chronos.Tests/TranscriptSousAgentsTests.cs` | Scénario des 12 sous-agents chauds rejoué | ✓ VERIFIED | 4 tests, code lu intégralement, conforme au plan. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `App.xaml.cs` (OnStartup) | `ArchiveStore.PurgerPrefixe` | appel best-effort, mode overlay uniquement | ✓ WIRED | `try { _host.Services.GetRequiredService<ArchiveStore>().PurgerPrefixe("desktop:"); } catch { }` présent, positionné après `ClaudeSettingsReconciler` et avant `OfferOnFirstRun`. Garde de câblage sur source (`Le_demarrage_purge_les_identifiants_fantomes_du_magasin_d_archives`) verte. |
| `App.xaml.cs` (ConfigureServices) | `SessionMonitor` | `new SessionMonitor(null, null, ArchiveStore, TreatedStore, SessionTreatmentTracker)` — 5 arguments | ✓ WIRED | Enregistrement DI conforme, `CompositionRootTests.Le_graphe_DI_resout_la_chaine_de_sessions` résout le graphe sans exception. |
| `TranscriptSessionSource.Read` | limite `MaxSessions` | `break` après `Classify`, aucun `Take` sur l'énumération | ✓ WIRED | `grep -c "\.Take(MaxSessions)"` → 0 ; `result.Count >= MaxSessions` présent une fois, dans la boucle après ajout. |
| `SessionMonitor.Read` | `SessionTreatmentTracker.Observe` | appel à 2 arguments, best-effort | ✓ WIRED | `_tracker?.Observe(raw, now)` sous `try/catch`. |
| `DiagnosticService` | `SessionMonitor` (nu) | `new SessionMonitor()` DÉLIBÉRÉMENT laissé intact | ✓ CONFORME (hors périmètre assumé) | Vérifié comme NON touché : c'est OBS-01/OBS-02, phase 22. `files.Take(8)` également intact. Le fait que ces deux éléments restent "faux" au sens du futur refactor est INTENTIONNEL et documenté par le plan et le SUMMARY — pas un gap de cette phase. |

### Data-Flow Trace (Level 4)

Non applicable au sens strict (phase de démolition/nettoyage, pas de nouveau flux de données affiché). Le
seul flux neuf est celui de `SessionStylesBindingTests` (source substituée → `SessionMonitor` → `SessionsViewModel`
→ rendu XAML) : la source `SourceFixe` produit 4 snapshots réels et non vides (`Assert.Equal(4, vm.Items.Count)`
avant tout montage), donc la preuve visuelle n'est pas basée sur une liste vide — vérifié en lisant le code.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Suite complète, 2 exécutions consécutives | `dotnet test Chronos.sln -c Debug --nologo -v q` (×2) | 719 / 0 échec, 719 / 0 échec (5 s chacune) | ✓ PASS |
| Gardes nommées de la phase + nouvelles classes phase 21 | `dotnet test … --filter "…ServicesLayerPurityTests|…CompositionRootTests|…NormalisationUniqueTests|…GardesDoctrineTests|…La_sonde…|…GardesPerimetreTests|…SessionStylesBindingTests|…ArchiveStorePurgeTests|…TranscriptSousAgentsTests"` | 34 / 0 échec | ✓ PASS |
| Aucune référence interdite dans `src/` | `grep -rn "DesktopUia\|Uia\|IForegroundWatch\|SessionKind\|SessionOrigin\|KindLabel" --include=*.cs --include=*.xaml src/` | 0 résultat | ✓ PASS |
| Invariant `sessions/` (66 entrées) | `ls "$APPDATA/Chronos/sessions" \| wc -l` | 66 | ✓ PASS |
| Invariant `archived.json` (84 octets, contenu inchangé) | `stat -c '%s'` + `cat` | 84 octets, contenu identique aux deux entrées `desktop:foreground:*` | ✓ PASS (attendu : la purge n'a lieu qu'au prochain lancement volontaire) |
| Invariant `oauth.dat` (518 octets) | `stat -c '%s'` | 518 | ✓ PASS |
| Overlay `Chronos-v3.0.2.exe` toujours vivant, non relancé | `tasklist` | pid 119412 présent | ✓ PASS |
| Git status propre après exécution | `git status --porcelain` | vide | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| SRC-01 | 21-02, 21-03 | Retrait de la source app-bureau UIA et de tout ce qui n'existait que pour elle | ✓ SATISFIED | 8 fichiers `src/` + 2 fichiers de tests supprimés ; 0 référence résiduelle dans `src/` ; 2 gardes de non-retour falsifiables vertes. |
| SRC-02 | 21-04 | Entrées fantômes `desktop:foreground:*` retirées du fichier archivé | ✓ SATISFIED (code) / ? NEEDS HUMAN (in vivo) | `PurgerPrefixe` implémentée, câblée, gardée, testée sur le contenu réel mesuré. L'effet sur le vrai fichier utilisateur n'est observable qu'après un lancement volontaire (non provoqué par cette exécution, comme documenté). |
| SRC-03 | 21-01 | Limite de 12 appliquée après le filtre des sous-agents, pas avant | ✓ SATISFIED | `TranscriptSousAgentsTests` rejoue le scénario mesuré des 12 sous-agents chauds ; passe. |

Aucun requirement orphelin : `REQUIREMENTS.md` ne mappe que SRC-01/02/03 à la phase 21, et les 4 plans (01-04)
déclarent exactement ces 3 requirements dans leur frontmatter, sans doublon.

### Anti-Patterns Found

Aucun. Recherche de `TODO|FIXME|XXX|HACK|PLACEHOLDER|not yet implemented|coming soon` sur les 9 fichiers de
production modifiés/créés par la phase (`TranscriptSessionSource.cs`, `ISessionSource.cs`, `SessionSnapshot.cs`,
`SessionMonitor.cs`, `SessionTreatmentTracker.cs`, `ArchiveStore.cs`, `App.xaml.cs`, `SessionsController.cs`,
`SessionStyles.xaml`) : 0 résultat. Aucune valeur en dur suspecte, aucun stub, aucun composant sans source de
données réelle.

Deux constats documentés par les exécuteurs eux-mêmes et jugés ici :

1. **Le `catch` de `TranscriptSessionSource.Read` reste non couvert par un test** (bug latent corrigé au
   plan 01, cf. SUMMARY). Le défaut réel (énumération LINQ paresseuse hors du `try`) est corrigé — vérifié
   en lisant le code : `ToList()` est bien à l'intérieur du `try`. Seule sa couverture par test manque, et
   l'exécuteur l'a signalé honnêtement plutôt que de le taire. **Jugement : acceptable, pas un gap.** Injecter
   une panne d'énumération disque exigerait une abstraction du système de fichiers absente du dépôt ; créer
   cette abstraction pour un seul test dépasserait le périmètre de la phase (qui est démolition/nettoyage,
   pas introduction d'infrastructure nouvelle). Documenté comme point différé dans `21-VALIDATION.md`.
2. **Deux critères grep du plan étaient inexacts** (`Ttl` attendu à 3, mesuré à 4 ; un `<see cref>` oublié
   dans le décompte du plan). Vérifié : le code de production n'a PAS été plié pour satisfaire un chiffre
   de plan erroné — le comportement réel (`PurgerPrefixe` n'utilise `Ttl` nulle part) est celui exigé par
   l'intention du critère, et l'écart de chiffre est documenté dans le SUMMARY comme une erreur du plan,
   pas du code. **Jugement : acceptable.**

Le onzième fichier signalé (`src/Chronos/Chronos.csproj`, commentaire nommant `WindowsUiaTreeProvider`) a été
vérifié : aucun `<Reference>` ou `<PackageReference>` touché (`git diff --stat -- '*.csproj'` vide sur toute
la phase), seul un commentaire obsolète a été reformulé. **Jugement : traitement correct, pas un gap.**

### Human Verification Required

### 1. Purge in vivo de `archived.json` (SRC-02)

**Test:** Lancer volontairement la version du dépôt de l'overlay Chronos (fermer l'instance `Chronos-v3.0.2.exe`
pid 119412 en cours, puis relancer la version compilée depuis ce dépôt), puis inspecter
`%APPDATA%\Chronos\archived.json`.
**Expected:** Le fichier passe de 84 octets (contenant les deux entrées `desktop:foreground:unknown` et
`desktop:foreground:code`) à 2 octets (`{}`), sans qu'aucun fichier n'ait été touché à la main.
**Why human:** La phase interdit explicitement de lancer ou tuer l'overlay en cours d'utilisation. Le test
unitaire et la garde de câblage prouvent que le code est correct et appelé ; seul un lancement réel prouve
que le fichier RÉEL de l'utilisateur est effectivement modifié.

### 2. Cohérence visuelle des 8 styles × 9 thèmes (critère de succès 4)

**Test:** `Chronos.exe --sessions`, parcourir les 8 styles sur les 9 thèmes.
**Expected:** Une ligne du style Pastilles lit « état · détail » avec un seul point médian, sans séparateur
orphelin, sans case vide ni décalage sur aucun des 8 styles / 9 thèmes.
**Why human:** `SessionStylesBindingTests` prouve l'absence d'exception, de taille dégénérée et de séparateur
en trop par la mesure automatisée (72 combinaisons) — mais ne juge pas l'esthétique du résultat.

### 3. Disparition effective des lignes `desktop:` en usage prolongé (critère de succès 1)

**Test:** Utiliser l'app bureau Claude au premier plan pendant plusieurs heures, observer le widget de sessions.
**Expected:** Plus aucune ligne préfixée `desktop:` n'apparaît, et plus rien à archiver manuellement.
**Why human:** Comportement d'usage réel prolongé, non reproductible par un test unitaire sans faire tourner
l'application sur la durée avec l'app bureau Claude réellement ouverte.

### Gaps Summary

Aucun gap bloquant identifié. Le code, les tests et la documentation de vérification (`21-VALIDATION.md`,
`21-01` à `21-04-SUMMARY.md`) sont cohérents entre eux et avec l'état réel du dépôt, vérifié ligne par ligne
pour tous les fichiers listés dans `<files_to_read>` :

- Les 3 requirements (SRC-01, SRC-02, SRC-03) sont satisfaits côté code et tests.
- Les 5 critères de succès de la ROADMAP sont vérifiés automatiquement dans la mesure du possible ; les 3
  sous-points non substituables (purge in vivo, jugement esthétique, usage prolongé) sont correctement
  routés en vérification humaine par la phase elle-même, pas escamotés.
- Le recul de couverture (752 → 719) est justifié nominativement : 49 tests supprimés, chacun rattaché au
  code disparu qu'il couvrait ; 16 ajoutés. Mesuré indépendamment ici : 719 / 0 échec, deux passes.
- Aucune anticipation des phases 22-26 constatée : `DiagnosticService` garde son `new SessionMonitor()` nu
  et son `files.Take(8)` ; `ArchiveStore.Add` garde son écriture `tmp`+`Move` et le TTL de 6 h.
- Les invariants de sécurité (sessions/ = 66, oauth.dat = 518 o, archived.json = 84 o inchangé, overlay
  pid 119412 vivant et non touché, git status propre) sont tous vérifiés indépendamment et conformes.
- Les deux écarts mineurs documentés par les exécuteurs eux-mêmes (couverture manquante du `catch` de
  `TranscriptSessionSource.Read`, deux critères grep de plan inexacts) sont jugés acceptables : ils sont
  documentés honnêtement, n'affectent aucun comportement de production, et ne justifient pas de créer une
  abstraction hors périmètre pour un seul test.

Le statut `human_needed` reflète uniquement les 3 points que la phase elle-même a correctement identifiés
comme non automatisables sous sa contrainte (overlay en cours d'utilisation, jugement esthétique, usage
prolongé) — ce ne sont pas des gaps de qualité mais des vérifications différées légitimes.

---

*Verified: 2026-09-12*
*Verifier: Claude (gsd-verifier)*
