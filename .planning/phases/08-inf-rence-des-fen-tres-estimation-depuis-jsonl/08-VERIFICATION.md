---
phase: 08-inf-rence-des-fen-tres-estimation-depuis-jsonl
verified: 2026-07-09T06:50:00Z
status: passed
score: 5/5 must-haves verifiés
---

# Phase 8 : Inférence des fenêtres + estimation depuis JSONL — Rapport de vérification

**Objectif de la phase :** En repli JSONL, les arcs retrouvent une longueur (reset 5 h inféré de l'activité) et — si un plafond est défini — une couleur (utilization estimée), le tout badgé « estimée » ; contrat providers nettoyé.
**Vérifié le :** 2026-07-09
**Statut :** passed
**Re-vérification :** Non — vérification initiale

## Goal Achievement

### Observable Truths

| # | Vérité | Statut | Preuve |
|---|--------|--------|--------|
| 1 | Arc 5 h : reset inféré (trou ≥ 5 h → début+5 h), marqué « estimée » | ✓ VERIFIED | `FiveHourWindowInference.InferWindowStart` implémenté verbatim, 7 `[Fact]` couvrant vide/unique/trou exact/rafale/activité continue/tri ; `JsonlEstimationProvider.BuildFiveHour` pose `ResetsAt = start + 5h`, `Reliability = Estimated`. Preuve E2E : sondage réel (voir Spot-Checks) → `ResetsAt = 09.07.2026 09:41:13 +00:00`, `Reliability = Estimated`. |
| 2 | Fenêtre inactive → fraction=1 (arc plein), jamais vide | ✓ VERIFIED | `BuildFiveHour` : `start is null` → `FractionTimeRemaining = 1.0`, `EstimatedTokens = 0`, `ResetsAt = null`. Test `Fenetre_inactive_arc_plein_sans_tokens` (fixture `sample-inactive.jsonl`, message à 6h avant now) vert. |
| 3 | Plafond défini → utilization=tokens/plafond ; sans plafond → null (jamais inventée) | ✓ VERIFIED | `Utilization = budget is > 0 ? Math.Max(0.0, tokens/budget.Value) : null` (pas de clamp haut, comme verrouillé). Test `Utilization_5h_estimee_avec_plafond` (settings.json `{"FiveHourTokenBudget":3100}` → 1550/3100=0.5) vert. Preuve E2E : sur la machine réelle (aucun budget dans settings.json), `Utilization = null` alors que 62 484 658 tokens sont sommés — confirme l'absence d'invention de couleur. |
| 4 | Reset hebdo via WeeklyAnchor ; sans ancre → « — » | ✓ VERIFIED | `WeeklyWindow.CurrentStart(anchor, now)` : ancrée `[ancre+k·7j]` si `WeeklyAnchor` défini, sinon 7 j glissants — 4 `[Fact]` (dont frontière exacte) verts. `BuildSevenDay` laisse `ResetsAt = null` côté provider (non-régression EST-05) ; `WeeklyRecalibration.Apply` (VM, inchangé) et `WindowGaugeViewModel.CountdownText = "—"` par défaut confirmés en lecture de code. Preuve E2E : machine réelle sans `WeeklyAnchor` → `SevenDay.ResetsAt = null`. |
| 5 | NET-01 : `SnapshotChanged` (IUsageProvider) + `Age` retirés ; `RefreshOrchestrator.SnapshotChanged` intact ; suite verte (119 attendus) | ✓ VERIFIED | `grep SnapshotChanged src/Chronos` : seules occurrences restantes = `RefreshOrchestrator.cs` (déclaration + invoke, intact) et `MainViewModel.cs`/`App.xaml.cs` (abonnement à l'orchestrateur, intact) — absent d'`IUsageProvider.cs` et des 3 providers. `grep "\.Age\b\|Age =\|Age,"` : aucune occurrence dans `src/Chronos`. `dotnet test Chronos.sln -c Debug` → 119/119 verts. |

**Score :** 5/5 vérités vérifiées

### Required Artifacts

| Artifact | Attendu | Statut | Détails |
|----------|---------|--------|---------|
| `src/Chronos/Services/FiveHourWindowInference.cs` | Fonction pure `InferWindowStart` + constante `Window=5h` | ✓ VERIFIED | Existe, verbatim du RESEARCH, aucun type WPF, 28 lignes, wiré par `JsonlEstimationProvider` et testé par 7 `[Fact]`. |
| `src/Chronos/Services/WeeklyWindow.cs` | Fonction pure `CurrentStart(anchor, now)` | ✓ VERIFIED | Existe, verbatim, 19 lignes, wiré par `JsonlEstimationProvider.BuildSevenDay` et testé par 4 `[Fact]`. |
| `src/Chronos/Services/ChronosSettings.cs` | Deux plafonds `long?` nullable par défaut | ✓ VERIFIED | `FiveHourTokenBudget`/`WeeklyTokenBudget` ajoutés en fin de record, round-trip prouvé (`SettingsServiceTests`), aucun champ existant retiré. |
| `src/Chronos/Services/JsonlEstimationProvider.cs` | `GetAsync` enrichi : passe unique, inférence, sommes bornées, utilization, `SettingsService` injecté | ✓ VERIFIED | Ctor `(ChronosPaths, IClock, SettingsService)` ; `settings = _settings.Load()` frais à chaque appel ; `InferWindowStart`/`WeeklyWindow.CurrentStart` appelés ; `BuildFiveHour`/`BuildSevenDay` conformes au RESEARCH. |
| `src/Chronos/Services/IUsageProvider.cs` | Contrat épuré sans `SnapshotChanged` | ✓ VERIFIED | Interface réduite à `Task<UsageSnapshot> GetAsync(...)`, aucune trace de `SnapshotChanged`. |
| `src/Chronos/Models/UsageSnapshot.cs` | Snapshot sans `Age` | ✓ VERIFIED | Propriété `Age` absente ; `SourceCapturedAt` présent, xmldoc documente la dérivation de la staleness. |

### Key Link Verification

| From | To | Via | Statut | Détails |
|------|----|----|--------|---------|
| `JsonlEstimationProvider.GetAsync` | `FiveHourWindowInference.InferWindowStart` | appel après tri des timestamps collectés | ✓ WIRED | `entries.Sort(...)`, `tsAsc = entries.Select(...)`, puis `FiveHourWindowInference.InferWindowStart(tsAsc, now)` (ligne 73-75). |
| `JsonlEstimationProvider.GetAsync` | `SettingsService.Load()` | lecture fraîche des plafonds à chaque `GetAsync` | ✓ WIRED | `var settings = _settings.Load();` en tête de `GetAsync` (ligne 37), pas de cache. |
| `JsonlEstimationProvider.BuildSevenDay` | `WeeklyWindow.CurrentStart` | borne de somme hebdo ancrée | ✓ WIRED | `var start = WeeklyWindow.CurrentStart(anchor, now);` (ligne 119), utilisé pour borner la somme. |
| `CompositeUsageProvider.Best` | `JsonlEstimationProvider` (fallback) | bascule Estimated quand primaire Unavailable/non-Exact | ✓ WIRED | Confirmé par code (`Best`) et par le sondage réel : `usage.json` du poste a `five_hour:null` → `ClaudeUsageObjectProvider` renvoie `Unavailable` → composite bascule sur le repli JSONL. |
| `RefreshOrchestrator.SnapshotChanged` | `MainViewModel.OnSnapshotChanged` | abonnement temps réel (event DISTINCT, non touché) | ✓ WIRED | `orchestrator.SnapshotChanged += OnSnapshotChanged;` dans `MainViewModel.cs:59`, inchangé par le nettoyage NET-01. |

### Behavioral Spot-Checks

| Comportement | Commande | Résultat | Statut |
|---|---|---|---|
| `dotnet build Chronos.sln -c Debug` | build complet | « La génération a réussi. 0 Avertissement(s) 0 Erreur(s) » | ✓ PASS |
| `dotnet test Chronos.sln -c Debug` | suite complète | 119/119 tests verts (durée 461 ms) | ✓ PASS |
| Sondage E2E réel : instanciation directe de `JsonlEstimationProvider` avec `ChronosPaths.Default()` (vrai `%APPDATA%\Chronos\usage.json`, vrai `%USERPROFILE%\.claude\projects`, 703 fichiers JSONL réels dont la session Claude courante active) | petit harnais console référençant `Chronos.csproj` (`dotnet run`), appel `provider.GetAsync()` | `FiveHour.ResetsAt = 09.07.2026 09:41:13 +00:00` ; `FractionTimeRemaining ≈ 0.8149` ; `Utilization = null` (aucun plafond configuré) ; `Reliability = Estimated` ; `EstimatedTokens = 62 484 658`. `SevenDay.ResetsAt = null` (VM le remplira), `Reliability = Estimated`. | ✓ PASS |

Détail de la preuve E2E : `%APPDATA%\Chronos\usage.json` contient `{"five_hour":null,"seven_day":null,"capturedAt":...}` — reproduisant exactement le scénario utilisateur documenté dans `08-CONTEXT.md` (statusline ne se rend jamais dans l'app bureau → usage.json toujours null). `ClaudeUsageObjectProvider.ReadWindow` renvoie donc `WindowState.Unavailable` (`Reliability=Unavailable`) pour les deux fenêtres, ce qui force `CompositeUsageProvider.Best` à sélectionner le repli JSONL (`Reliability=Estimated`). Le repli produit désormais un `ResetsAt` inféré réel (pas null), une fraction de temps calculée, et une utilization honnêtement `null` en l'absence de plafond configuré — exactement le comportement attendu par la phase. Le harnais de sondage temporaire a été supprimé du scratchpad après exécution (aucun fichier laissé dans le dépôt).

### Data-Flow Trace (Level 4)

| Artifact | Variable de données | Source | Données réelles | Statut |
|---|---|---|---|---|
| `JsonlEstimationProvider.GetAsync` | `entries` (List<(Ts,Tokens)>) | Lecture streaming des `*.jsonl` sous `ProjectsRoot`, filtrées `IsAssistant` + `when <= now` | Oui — sondage réel : 62 484 658 tokens sommés depuis les vrais fichiers de la machine (pas de valeur statique/vide) | ✓ FLOWING |
| `BuildFiveHour`/`BuildSevenDay` | `start`/`tokens`/`Utilization` | `FiveHourWindowInference.InferWindowStart` / `WeeklyWindow.CurrentStart` / `settings.Load()` | Oui — `ResetsAt` et `Fraction` non figés, dérivés du dernier timestamp réel du poste | ✓ FLOWING |
| `CompositeUsageProvider` → `MainViewModel.ApplySnapshot` | `snap.FiveHour`/`snap.SevenDay` | `CompositeUsageProvider.Best` (primaire Unavailable → fallback Estimated) | Oui — chemin de sélection confirmé par lecture de code + reproduction de l'état réel `usage.json` du poste | ✓ FLOWING |

### Requirements Coverage

| Requirement | Plan source | Description | Statut | Preuve |
|---|---|---|---|---|
| EST-01 | 08-01, 08-02 | Reset 5 h inféré des JSONL | ✓ SATISFIED | `InferWindowStart` + `BuildFiveHour.ResetsAt` ; preuve E2E réelle. |
| EST-02 | 08-01, 08-02 | Fenêtre inactive → arc plein (fraction=1, jamais vide) | ✓ SATISFIED | `BuildFiveHour` branche `start is null` ; test dédié + sémantique verrouillée dans `InferWindowStart`. |
| EST-03 | 08-01, 08-02 | Utilization 5 h = tokens/plafond calibrable, sinon neutre | ✓ SATISFIED | `BuildFiveHour.Utilization` ; test `Utilization_5h_estimee_avec_plafond` ; preuve E2E `null` sans plafond réel. |
| EST-04 | 08-01, 08-02 | Utilization hebdo = tokens/plafond (fenêtre ancrée WeeklyAnchor ou 7j glissants) | ✓ SATISFIED | `WeeklyWindow.CurrentStart` + `BuildSevenDay.Utilization` ; 4 `[Fact]` WeeklyWindow. |
| EST-05 | 08-02 | Reset hebdo via WeeklyAnchor, sans ancre reste « — » | ✓ SATISFIED | `BuildSevenDay.ResetsAt=null` (non-régression, VM inchangé) ; `WeeklyRecalibration`/`WindowGaugeViewModel.CountdownText="—"` confirmés en lecture de code ; preuve E2E `SevenDay.ResetsAt=null` sur poste sans ancre. |
| NET-01 | 08-02 | `SnapshotChanged`/`Age` retirés du contrat, suite verte | ✓ SATISFIED | Grep confirmant absence dans `IUsageProvider`/3 providers/modèle ; `RefreshOrchestrator.SnapshotChanged` intact ; 119/119 tests verts. |

Aucun requirement orphelin détecté : les six IDs déclarés dans les frontmatters des deux plans (`EST-01..05`, `NET-01`) correspondent exactement aux six IDs mappés à la Phase 8 dans `REQUIREMENTS.md`.

### Anti-Patterns Found

Aucun. Recherche `TODO|FIXME|XXX|HACK|PLACEHOLDER|placeholder|coming soon|not yet implemented` sur `FiveHourWindowInference.cs`, `WeeklyWindow.cs` et `JsonlEstimationProvider.cs` : aucune occurrence. Aucun retour statique vide masquant une non-implémentation (`BuildFiveHour`/`BuildSevenDay` calculent systématiquement à partir des données réelles ou de la sémantique verrouillée « inactive »).

### Human Verification Required

Aucune. L'objectif de la phase concerne exclusivement la logique de repli (données, pas rendu visuel) ; le sondage E2E contre les vraies données du poste (usage.json réel à null, vrais JSONL, 703 fichiers) constitue une preuve directe suffisante du comportement bout-en-bout sans besoin d'inspection visuelle de l'overlay. L'apparence de l'arc (couleur/longueur réellement dessinés à l'écran) relève du rendu WPF déjà couvert par les phases antérieures et non modifié ici.

### Gaps Summary

Aucun gap. Les cinq vérités observables sont vérifiées par code + tests + preuve E2E concrète sur données réelles de production (le propre usage Claude Code de la machine, session active comprise). Build et suite de tests complets verts (119/119). Nettoyage NET-01 complet et non-régressif (RefreshOrchestrator/MainViewModel intacts).

---

_Vérifié le : 2026-07-09_
_Vérificateur : Claude (gsd-verifier)_
