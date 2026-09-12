---
phase: 20-honn-tet-visible-cadran-diagnostic
verified: 2026-09-12T00:00:00Z
status: human_needed
score: 4/4 must-haves verified (code) — jugement visuel restant délégué à l'humain
human_verification:
  - test: "Lisibilité du pointillé de plancher (style Anneaux) à distance normale, sans zoomer"
    expected: "Le grain (tirets 4,4 px / espaces 3,6 px sur les arcs de 8 px, 3,85/3,15 px sur celui de 7 px du mode Normal) se distingue du trait plein sans être bruyant"
    why_human: "Rendu graphique jamais affiché — l'overlay n'a volontairement pas été lancé (lancement déclencherait la purge non supervisée des 25 groupes de hooks de ~/.claude/settings.json)"
  - test: "Lisibilité des quatre marques (M-âge, M-plancher, M-indispo, rangée de pastilles) sur fond d'écran réel, balayage représentatif des 9 thèmes × 5 styles × 2 modes"
    expected: "Chaque marque reste lisible sans nuire à la compacité du cadran ; la pastille d'âge (anneau creux) se distingue des pastilles pleines voisines"
    why_human: "Jugement visuel sur fond d'écran réel — non automatisable, non simulé, matrice réelle 90 combinaisons"
  - test: "Le diagnostic (clic droit → Diagnostic…) sur un usage réel affiche une ligne « 5 h : … · source : … · relevé il y a … »"
    expected: "La source nommée est plausible et l'ancienneté correspond à l'usage réel de l'utilisateur"
    why_human: "Exige de republier l'exe (celui de publish/ date du 2026-07-12, avant les phases 15-20) et de le lancer sur la machine réelle"
  - test: "Bascule visuelle réelle de la phase 19 : le « 10 % » figé depuis le 10 juillet doit avoir cédé la place au mot « indisponible » + invitation à se connecter"
    expected: "Aucun pourcentage affiché, mot « indisponible » visible, pastille d'invitation à se connecter allumée (prédit par la donnée réelle : last-exact.json absent, usage.json vieux de 64 jours)"
    why_human: "Exige de lancer l'overlay républié — non fait par prudence (purge des hooks)"
  - test: "Parcours de reconnexion en un clic de bout en bout (TOK-03) + contraste des 4 pastilles sur fond réel"
    expected: "Un clic sur la pastille ambre/grise relance le login ; la pastille disparaît dès qu'un chiffre exact revient ; les 4 pastilles restent lisibles sur fond réel"
    why_human: "Exige une authentification OAuth réelle de l'utilisateur"
  - test: "HDR-02 en production : un vrai 429 porte-t-il bien les en-têtes anthropic-ratelimit-unified-*"
    expected: "Le diagnostic affiche « 429 — en-têtes lus quand même, les chiffres restent exacts »"
    why_human: "Exige une saturation réelle du compte, non atteignable en environnement de vérification ; hérité de la phase 18, soldé ici par le protocole consolidé"
---

# Phase 20 : Honnêteté visible — cadran & diagnostic — Verification Report

**Phase Goal:** Ce que la doctrine sait, l'utilisateur le voit : le cadran distingue à l'œil un chiffre
exact frais, un chiffre exact daté et un état indisponible, et le diagnostic nomme la source qui alimente
réellement l'affichage ainsi que son ancienneté.
**Verified:** 2026-09-12
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

Toute la matière **codée et testée** de cette phase a été vérifiée directement dans le code source (pas sur
la foi des SUMMARY) et par exécution réelle de la suite complète. Aucun gap de code n'a été trouvé. Le
statut `human_needed` reflète exclusivement la part de jugement visuel/production que l'équipe elle-même a
délibérément refusé de simuler (l'overlay n'a jamais été lancé, pour ne pas déclencher sans supervision la
purge des 25 groupes de hooks de `~/.claude/settings.json`).

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Trois états lisibles d'un coup d'œil (EXA-03) : frais / daté (ou plancher) / indisponible | ✓ VERIFIED (code+tests) / ⏳ jugement visuel humain | `MainWindow.xaml` bind `EstPlancher`→`StrokeDashArray` sur les 3 styles Anneaux (lignes 58-59, 80-81, 124-125) + les 4 autres styles (`CadranBraisesView.xaml` etc., déjà en place depuis avant + confirmés dans le diff 20-03) ; `DataUnavailable` bindé en `MotIndisponible` (l. 208-212) ; `AfficherReleveDate`/`InfobulleReleve` bindés sur `PastilleReleveDate` (l. 251-255). `IsStale`/`IsEstimated` : 0 occurrence dans tout le dépôt (grep confirmé). Suite verte 748/748 ×2. |
| 2 | Cohérent dans les modes et les thèmes | ✓ VERIFIED (code+tests) / ⏳ jugement visuel humain | `ThemeCatalog.All.Count == 9` (lu dans `ChronosTheme.cs`), `ThemingTests.cs:28` asserte `Assert.Equal(9, ThemeCatalog.All.Count)` et balaie `foreach (var t in ThemeCatalog.All)` (l. 53) — pas de liste en dur. Aucun nouveau token créé (`TexteSecondaire` réutilisé), donc rien à publier dans les 9 thèmes. |
| 3 | Diagnostic sans ambiguïté (EXA-06) | ✓ VERIFIED | `DiagnosticService.Describe` (l. 569-594) écrit `"≥ "` (jamais `"~"`) pour `SourceReliability.Estimated` (l. 577) et compose systématiquement `· source : LibelleSource.Format(w.Source)` + `· relevé LibelleSource.Anciennete(...)` (l. 583-584). `Chronos.Text.LibelleSource` est l'unique point de vocabulaire FR (lu en entier) — consommé par `DiagnosticService` ET par `WindowGaugeViewModel`/`MainViewModel` (infobulle), zéro mapping FR dupliqué constaté par lecture des deux fichiers. |
| 4 | Aucune fuite de WPF dans les services | ✓ VERIFIED | `ServicesLayerPurityTests` exécuté isolément : 2/2 verts, 15 ms. `IInventaireMachine`/`InventaireMachine`/`LibelleSource` : aucun `using System.Windows` (confirmé par lecture des fichiers). |

**Score:** 4/4 truths vérifiées côté code ; la part de jugement visuel qui les accompagne reste `⏳` par
choix assumé de l'équipe d'exécution (non simulable sans lancer l'overlay).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Chronos/Models/SourceUsage.cs` | Enum à 5 membres, aucun fourre-tout | ✓ VERIFIED | Lu en entier : 5 membres exacts calqués sur la chaîne DI, pas de membre "Inconnue" |
| `src/Chronos/Models/WindowState.cs` | Champ `Source` porté par la fenêtre, pas le snapshot | ✓ VERIFIED | `public SourceUsage? Source { get; init; }` (l. 46), commentaire explicite sur le motif "porté par référence" |
| `src/Chronos/Text/LibelleSource.cs` | Vocabulaire FR unique (Format/Anciennete/Provenance) | ✓ VERIFIED | Lu en entier, pur (aucun WPF/I-O/CultureInfo), gère l'horloge qui recule |
| `src/Chronos/Services/DiagnosticService.cs` | Diagnostic nomme source + ancienneté, `≥` et non `~` | ✓ VERIFIED | `Describe` (l. 569-594) confirmé par lecture directe |
| `src/Chronos/ViewModels/WindowGaugeViewModel.cs` | `EstPlancher`, `EstDate` rapportés (pas recalculés) | ✓ VERIFIED | `EstPlancher = s.Reliability == SourceReliability.Estimated` (l. 102), `EstDate = s.Provenance is ...` (l. 106) — affectation directe, aucune arithmétique |
| `src/Chronos/ViewModels/MainViewModel.cs` | `IsStale`/`CapturedAt` supprimés, `AfficherReleveDate`/`InfobulleReleve` bindés | ✓ VERIFIED | 0 occurrence `IsStale` ; `AfficherReleveDate =` (l. 352) et `MajInfobulleReleve()` (l. 388, 436-439) présents et appelés depuis `ApplySnapshot`, pas depuis `Interpolate` |
| `src/Chronos/Views/MainWindow.xaml` | Pointillé Anneaux, rangée de pastilles, mot indisponible | ✓ VERIFIED | `StrokeDashArray` ×3 (l. 59/81/125), `RangeePastilles` StackPanel (l. 237), `MotIndisponible` (l. 208) |
| `src/Chronos/Controls/RingArc.cs` | Hérite `StrokeDashArray` de `Shape` (pas de nouvelle DP) | ✓ VERIFIED | Lu en entier : `sealed class RingArc : Shape`, aucune DP `StrokeDashArray` propre — confirmé par héritage natif |
| `tests/Chronos.Tests/CadranBindingTests.cs` | Isolé de `%APPDATA%\Chronos\settings.json` réel | ✓ VERIFIED | `TempPaths()` (l. 38-42) sous `Path.GetTempPath()` + `Assert.StartsWith` ; 0 occurrence `ChronosPaths.Default()` dans ce fichier |
| `tests/Chronos.Tests/GardesDoctrineTests.cs` | Garde structurelle `EstimatedTokens`, garde de seuil scopée à 2 fichiers | ✓ VERIFIED | `Aucun_champ_nomme_EstimatedTokens_...` (l. 159) balaie le texte source de `Services/`+`Models/` ; garde de seuil scopée nommément à `MainViewModel.cs`/`WindowGaugeViewModel.cs` (l. 360), en excluant `SessionsViewModel` |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `WindowGaugeViewModel.EstPlancher` | `MainWindow.xaml` (5 styles) | `DataTrigger Binding="{Binding ...EstPlancher}"` | WIRED | 8 bindings XAML confirmés (grep `EstPlancher` dans les vues Cadrans + MainWindow) |
| `DoctrineFraicheur` / 5 producteurs | `WindowState.Source` | Affectation directe `Source = SourceUsage.X` | WIRED | 5 lignes `Source = SourceUsage.` en production, confirmées par la SUMMARY et cohérentes avec les 5 producteurs déclarés |
| `WindowState.Source` / `CapturedAt` | `DiagnosticService.Describe` | `LibelleSource.Format` / `LibelleSource.Anciennete` | WIRED | Lu directement dans `DiagnosticService.cs` l. 583-584 |
| `WindowState.Source` / `CapturedAt` | `MainWindow.xaml` (ToolTip) | `MainViewModel.InfobulleReleve` | WIRED | `ToolTip="{Binding InfobulleReleve}"` (l. 254), `InfobulleReleve` composée dans `MajInfobulleReleve` (l. 436-439) via `Ligne(...)` qui consomme `LibelleSource` |
| Bouton/pastille de reconnexion | `ReconnecterCommand` | `Command="{Binding ReconnecterCommand}"` | WIRED (et confirmé NON `LoginClaudeCommand`) | `grep 'Command="{Binding LoginClaudeCommand}"'` = 0 occurrence ; `Assert.Same(vm.ReconnecterCommand,...)` + `Assert.NotSame(vm.LoginClaudeCommand,...)` présents aux lignes 318-319 et 389-390 de `CadranBindingTests.cs` |
| `PastilleHorsLigne` (10×10) / `PastilleInvitationConnexion` (14×14) | Non-recouvrement | `StackPanel RangeePastilles` (structure) | WIRED | Structurellement impossible de se recouvrir (rangée horizontale) ; testé par `Rect.Intersect(r1,r2).IsEmpty` (l. 630) et `Rect.Intersect(rMot,rRangee).IsEmpty` (l. 765) |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Suite complète, 1ère passe | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q` | 748/748, 0 échec, 3 s | ✓ PASS |
| Suite complète, 2ème passe (BAML) | idem | 748/748, 0 échec, 4 s | ✓ PASS |
| 5 gardes permanentes, individuellement | `--filter FullyQualifiedName~{Garde}` ×5 | `ServicesLayerPurityTests` 2/2, `CompositionRootTests` 5/5, `NormalisationUniqueTests` 3/3, `GardesDoctrineTests` 8/8, sonde-primaire 1/1 → 19/19 | ✓ PASS |
| `EstimatedTokens` absent de `src/Chronos` | `grep -rn "EstimatedTokens" src/Chronos` | 0 ligne | ✓ PASS |
| `IsStale`/`IsEstimated` absents de tout le dépôt | `grep -rn "IsStale\|IsEstimated" src tests` | 0 ligne | ✓ PASS |
| Fichiers SOURCE avec `RefreshAsync` | `grep -rl "RefreshAsync" src/Chronos \| wc -l` | 2 (`ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs`) | ✓ PASS |
| Intégrité `oauth.dat` | `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` | `518 1783863147` | ✓ PASS (inchangé) |
| Intégrité `~/.claude/settings.json` | `stat -c '%s %Y' "$HOME/.claude/settings.json"` | `6872 1785403369` | ✓ PASS (inchangé, 25 hooks toujours présents — jamais lancé) |
| Version du `.csproj` | `grep Version src/Chronos/Chronos.csproj` | `2.8.1` (inchangée depuis avant tout le milestone) | ℹ️ INFO — point de release, hors périmètre code de cette phase |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|--------------|--------|----------|
| EXA-03 | 20-04-PLAN.md | Le cadran distingue visuellement frais/daté/indisponible | ✓ SATISFIED (code) / ⏳ jugement visuel | `StrokeDashArray`, `MotIndisponible`, `RangeePastilles` bindés et testés (11 nouveaux `[WpfFact]`, TDD RED→GREEN réel) |
| EXA-06 | 20-02, 20-05 | Le diagnostic nomme la source et son ancienneté | ✓ SATISFIED | `DiagnosticService.Describe` consomme `LibelleSource` ; 4 nouveaux tests (`DiagnosticServiceTests`) confirmés dans le fichier et exécutés (11/11 en 534 ms) |

Aucun requirement orphelin : REQUIREMENTS.md mappe exactement EXA-03 et EXA-06 à la phase 20, tous deux
présents dans les frontmatters des plans 20-02/03/04/05, et tous deux cochés `Complete` dans la table de
traçabilité (vérifié par lecture de `REQUIREMENTS.md`).

### Anti-Patterns Found

Aucun anti-pattern bloquant trouvé lors du balayage du code modifié par cette phase :
- Aucun `TODO`/`FIXME`/`placeholder` introduit.
- Aucune valeur factice codée en dur pour les nouveaux champs (`Source`, `EstPlancher`, `EstDate`,
  `InfobulleReleve`, `AfficherReleveDate`) — tous alimentés par le pipeline réel, vérifié par lecture des
  points d'assignation.
- Les deux "duplications" apparentes détectées par grep littéral (`LoginClaudeCommand` cité 2 fois,
  `ReconnecterCommand` cité 4 fois dans `MainWindow.xaml`) sont des **commentaires de sécurité
  préexistants** nommant explicitement la commande interdite pour avertir tout futur lecteur — pas des
  bindings. Vérifié : 0 binding réel sur `LoginClaudeCommand`. C'est un arbitrage documenté et jugé correct
  (voir `<known_flags>` de la mission) : supprimer ces commentaires aurait effacé l'avertissement qui
  protège le coffre de jetons.

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|---------------------|--------|
| `MainWindow.xaml` (StrokeDashArray) | `FiveHour.EstPlancher` / `SevenDay.EstPlancher` | `WindowGaugeViewModel.Apply(s)` ← composite/doctrine réelle | Oui (dérivé de `s.Reliability`, pas codé en dur) | ✓ FLOWING |
| `MainWindow.xaml` (ToolTip pastille d'âge) | `MainViewModel.InfobulleReleve` | `MajInfobulleReleve()` ← `FiveHour`/`SevenDay` (Source, CapturedAt, Provenance, TokensDepuisReleve) | Oui | ✓ FLOWING |
| `DiagnosticService.Describe` | `w.Source`, `w.CapturedAt` | `WindowState` produit par le composite réel (5 producteurs) | Oui | ✓ FLOWING |
| `MotIndisponible` | `DataUnavailable` | `MainViewModel.ApplySnapshot` ← `snap.FiveHour.Reliability == Unavailable` | Oui | ✓ FLOWING |

### Human Verification Required

### 1. Lisibilité du pointillé de plancher (style Anneaux)

**Test:** Lancer l'exe republié, observer l'anneau en pointillé sur un cas plancher, à distance normale.
**Expected:** Le grain se distingue du trait plein sans être bruyant ; sinon ajuster `0.55 0.45` (en
multiples de `StrokeThickness`, jamais en pixels).
**Why human:** Rendu jamais affiché — l'overlay n'a volontairement pas été lancé pour ne pas déclencher la
purge non supervisée des 25 groupes de hooks Chronos dans `~/.claude/settings.json`.

### 2. Lisibilité des quatre marques sur fond d'écran réel

**Test:** Balayer ≈10 combinaisons représentatives parmi les 5 styles × 2 modes × 9 thèmes.
**Expected:** Cas nominal (exact frais) ne porte aucune marque ; l'anneau creux de la pastille d'âge se
distingue des pastilles pleines voisines ; le mot « indisponible » reste lisible sans être envahissant.
**Why human:** Jugement visuel sur fond réel, matrice de 90 combinaisons — non automatisable.

### 3. Le diagnostic sur un usage réel

**Test:** Republier l'exe, se connecter, ouvrir clic droit → Diagnostic….
**Expected:** La ligne « 5 h » a la forme `EXACT/PLANCHER/indisponible — X% · source : ... · relevé il y a
...` et la source nommée est plausible.
**Why human:** Exige de republier (l'exe de `publish/` date du 2026-07-12, antérieur à tout le milestone)
et de lancer sur la machine réelle avec un jeton valide.

### 4. La bascule visuelle réelle de la phase 19

**Test:** Observer le cadran après republication — le « 10 % » figé depuis le 10 juillet doit avoir cédé.
**Expected:** Aucun pourcentage, mot « indisponible », pastille d'invitation à se connecter allumée (prédit
par la donnée réelle : `last-exact.json` absent, `usage.json` vieux de 64 jours). Après reconnexion, un
chiffre exact doit apparaître et l'invitation disparaître.
**Why human:** Exige de lancer l'overlay républié — jamais fait par l'agent.

### 5. Parcours de reconnexion de bout en bout + contraste des pastilles

**Test:** Provoquer une déconnexion, cliquer sur la pastille ambre/grise, observer le retour à l'état
connecté.
**Expected:** Le login se relance ; la pastille disparaît dès qu'un chiffre exact revient (immédiatement,
pas au tick suivant) ; les 4 pastilles restent lisibles sur le fond d'écran réel.
**Why human:** Exige une authentification OAuth réelle — hérité de la phase 17, soldé ici par le protocole
consolidé du plan 20-05.

### 6. HDR-02 en production (429 réel)

**Test:** Attendre ou provoquer une saturation réelle du compte.
**Expected:** Le diagnostic dit « 429 — en-têtes lus quand même, les chiffres restent exacts ».
**Why human:** Non atteignable sans saturation réelle du compte Anthropic ; hérité de la phase 18, marqueur
`human_needed` resté ouvert depuis cette phase, correctement non refermé ici.

## Gaps Summary

**Aucun gap de code.** Chaque affirmation vérifiable par lecture du code source, exécution de la suite de
tests ou inspection de fichiers a été confirmée directement (pas sur la seule foi des SUMMARY) :

- Les trois états (frais/daté/indisponible) sont bindés dans `MainWindow.xaml` et testés par 11 nouveaux
  `[WpfFact]` avec un vrai cycle TDD RED→GREEN (commits séparés, historiquement vérifiables).
- `ThemeCatalog` compte bien 9 thèmes, et `ThemingTests` balaie le catalogue, pas une liste en dur.
- Le diagnostic écrit `≥` et non `~`, et consomme `Chronos.Text.LibelleSource` comme unique source de
  vocabulaire FR, partagée avec l'infobulle du cadran — aucune duplication.
- `ServicesLayerPurityTests` et les 4 autres gardes permanentes sont vertes.
- Le bug de recouvrement des pastilles (10×10 dans 14×14) est corrigé par une structure (`StackPanel`) et
  gardé par un test géométrique `Rect.Intersect(...).IsEmpty`, pas par une convention de marges.
- Les assertions de sécurité par instance (`Assert.Same`/`Assert.NotSame` sur `LoginClaudeCommand`) sont
  intactes ; 0 binding réel sur cette commande.
- Une seule notion de « périmé » subsiste, et la garde qui l'impose est scopée nommément à
  `MainViewModel.cs`/`WindowGaugeViewModel.cs`, en excluant `SessionsViewModel`.
- `TokensText`/`HasTokens`/`DataUnavailable` sont consommés (dans l'infobulle et le XAML respectivement) ;
  `EstimatedTokens` a 0 occurrence dans `src/Chronos`.
- La suite tourne en 3-4 secondes (contre 2 min 12 avant), vérifié en deux passes réelles : 748/748, 0
  échec, à chaque fois.
- `CadranBindingTests` n'atteint plus `%APPDATA%\Chronos\settings.json` réel (`TempPaths()` confirmé).
- La grille des réglages (`Grid.ColumnSpan`) est corrigée sans casser les 3 `ItemsPanelTemplate`
  (`UniformGrid`) des sélecteurs — les trois sont toujours présents dans `SettingsWindow.xaml`.
- Sécurité : `oauth.dat` (518 o / mtime 1783863147) et `~/.claude/settings.json` (6872 o / mtime
  1785403369) strictement inchangés — vérifié directement par `stat`, pas seulement rapporté. Exactement
  2 fichiers SOURCE contiennent `RefreshAsync`.

**Le seul risque résiduel identifié et jugé acceptable** : `OverlayWindowConfigTests.cs` continue de monter
son `MainViewModel` sur `ChronosPaths.Default()` réel (4 usages) — dette **préexistante**, documentée dans
`deferred-items.md`, explicitement hors du périmètre du plan 20-01 (borné à `CadranBindingTests`), et
vérifiée ici : aucun plan de la phase 20 n'a modifié ce fichier (`git log` sur le fichier s'arrête à la
phase 17), donc le risque annoncé (« une future assertion de style y serait verte par accident ») ne s'est
pas matérialisé dans cette phase.

**Ce qui reste réellement en suspens** n'est pas un gap de code mais un ensemble de **six vérifications
humaines**, toutes documentées par l'équipe d'exécution elle-même dans `20-05-SUMMARY.md` et
`20-VALIDATION.md`, jamais présentées comme faites : le rendu visuel n'a jamais été observé (l'overlay n'a
pas été lancé, par choix assumé, pour ne pas déclencher sans supervision la purge des hooks), et plusieurs
constats exigent une authentification réelle ou une saturation réelle du compte Anthropic. Ces six points
sont listés dans `human_verification` ci-dessus.

**Point non-bloquant signalé pour mémoire** (jugement du vérificateur, conforme à l'indication de la
mission) : le `.csproj` porte encore la version `2.8.1`, celle de l'exe déjà déployé avant tout le
milestone v1.5. Ce n'est pas un gap du code ou des tests de la phase 20 — le goal de la phase (« l'honnêteté
est visible dans le cadran et le diagnostic ») est atteint indépendamment du numéro de version — mais c'est
un point de release à traiter à la clôture du milestone v1.5, avant toute republication destinée à
l'utilisateur.

---

*Verified: 2026-09-12*
*Verifier: Claude (gsd-verifier)*
