# Inventaire cycle 2 — purge de la récupération des données (R5)

Agent Explore, lecture seule, 2026-10-03. Chemins relatifs à `src/Chronos/` sauf mention.

## Preuve terrain
Journal réel `%APPDATA%\Chronos\historique\releves-*.jsonl` (hors paquet MSIX, donc vue réelle) :
septembre 894 `SondeEnTetes` + 7 `EndpointOAuthChronos` ; octobre 637 `SondeEnTetes` ; **zéro** `EndpointOAuthClaude`,
**zéro** `PontStatusLine`. `chronos.log` 3.4.0 (28/09) : `Token déchiffré : NON` ; `usage.json` figé au 10/07.

## Chaîne effective (`App.xaml.cs:475-528`)
```
RefreshOrchestrator (tick 60 s + watcher usage.json)
 └ LastExactUsageProvider (fraîcheur, last-exact.json, transcripts)
    └ JournalisationUsageProvider
       └ Composite( SONDE D'EN-TÊTES,
            Composite( OAuth login Chronos,
              Composite( Gated → OAuth jeton app bureau, pont statusLine usage.json )))
```
| Maillon | Produit ? |
|---|---|
| `RateLimitHeaderUsageProvider` (sonde 1 req / 300 s) | **OUI — source réelle**, porte statut serveur + overage |
| `ChronosOAuthUsageProvider` | rarement (secours) ; `ChronosTokenAuthority` indispensable à la sonde |
| `GatedOAuth` → `ClaudeOAuthUsageProvider` → `ClaudeTokenReader` | **JAMAIS** (coffre MSIX dans le paquet) |
| `ClaudeUsageObjectProvider` (usage.json via `--statusline`) | **JAMAIS gagnant** (figé, vue virtualisée) |
| `LastExactUsageProvider` + `DoctrineFraicheur` + `LastExactStore` | OUI |
| `SourceActiviteMemoisee` / `TranscriptActivityProvider` | OUI, à la demande |
| `WeeklyRecalibration` | pratiquement jamais actif (reset hebdo toujours fourni) |

## Candidats (F faible / M moyen / É élevé)
**A. Sources mortes**
- A1 `ClaudeOAuthUsageProvider` + câblage `App.xaml.cs:408-414,491-493` — M — `GardesPerimetreTests:627` exige 3 composites.
- A2 `GatedOAuthUsageProvider` — F.
- A3 `ClaudeTokenReader` (370 l.), `IClaudeTokenReader`, `WindowsCredentialStore` — M (signature de `DiagnosticService`, nombreux montages de tests). Code le plus sensible (déchiffre le jeton d'une autre app).
- A4 `IInventaireMachine` / `InventaireMachine` (17,7 s par rapport de diagnostic) — F.
- A5 `ClaudeUsageObjectProvider` — **É** (lié à A6).
- A6 Pont statusLine complet (`--statusline`, `StatusLineBridge`, `StatusLineInstaller`, `StatusLineSetup`, proposition au 1er lancement, `ClaudeSettingsReconciler.cs:91`) — **É** : `~/.claude/settings.json` pointe aujourd'hui sur `Chronos-v3.4.0.exe --statusline` ; sans ce mode, chaque rendu de barre lancerait l'overlay et heurterait le verrou mono-instance. **Migration obligatoire** (désinstaller + restaurer `InnerStatusLineCommand`, ou garder un `--statusline` relais pur).
- A7 Watcher `usage.json` dans `RefreshOrchestrator.cs:80-96,105,116-121` — M.
- A8 `SourceUsage.EndpointOAuthClaude` / `PontStatusLine` + libellés — M (lecture du journal tolérante, aucune ligne existante).
- A9 `UsageNormalization.InstantDepuisEpochMillisecondes` — F.

**B. Réglages et UI**
- B1 `OAuthUsageEnabled`, `IsOAuthUsageEnabled`, `ToggleOAuthUsageCommand` — non lié au XAML depuis réglages v2 — F.
- B2 `InnerStatusLineCommand`, `StatusLinePromptDismissed`, `IsStatusLineSourceEnabled`, carte « Barre de statut » (`ReglagesWindow.xaml:664-681`) — É (avec A6).
- B3 Recalibrage hebdo (`WeeklyRecalibration`, `Recalibrate`, `RecalibrationDialog`, `RecalibrationViewModel`, carte « Recalibrer… ») — M. `WeeklyAnchor` reste lu par `BornesPlage` / `HistoriqueViewModel` comme repli : garder en lecture seule ou basculer sur le samedi calendaire.
- B4 `FiveHourWindowInference` (orphelin) — F.
- B5 `WeeklyWindow` (orphelin ; `BornesPlageTests:75-95` à réécrire) — F.

**C. Diagnostic** (`DiagnosticService.cs`)
- C1 `[Réglage] Usage exact (OAuth)` (menu disparu) ; C2 `[pont statusLine]` ; C3 `[endpoint OAuth (repli)]` — 94 % du coût d'un rapport ; C4 `[Conseil]` recommande le pont statusLine (trompeur) ; C5 commentaire UIA.

**D. Docs et commentaires périmés** : `README.md:46-58` entièrement faux (sonde absente) ; `docs/data-sources.md` §1-5 (statusLine primaire, estimation JSONL) ; `CLAUDE.md:13-16` ; commentaires « repli JSONL » / « estimation » (CompositeUsageProvider, SourceReliability, WindowState, EmberRingControl, FuseBar, TideColumn, MainViewModel, RateLimitHeaderUsageProvider).

**E. Widget sessions** : `App.xaml.cs:132-143` migration `PurgerPrefixe("desktop:")` (épinglée `GardesPerimetreTests:84-93`) ; commentaires UIA. Garder la garde de non-retour `GardesPerimetreTests:26`. Note mémoire `chronos-desktop-uia.md` périmée.

**À NE PAS purger** : `ChronosOAuthUsageProvider`, `ChronosTokenAuthority`, `TokenRefreshService`, `OAuthLogin`, `LastExactUsageProvider`, `LastExactStore`, `DoctrineFraicheur`, `TranscriptActivityProvider`, `SourceActiviteMemoisee`, `DedupUsage`, `JournalisationUsageProvider`.

## Méthodologie cible
1. Primaire unique : sonde d'en-têtes (pourcentages, resets, statut serveur, overage).
2. Secours exact unique : `/api/oauth/usage` avec le login Chronos.
3. `Composite(sonde, OAuthChronos)` par fenêtre.
4. Journal autour du composite, deux sources possibles.
5. Tête `LastExactUsageProvider` : frais (≤ 360 s) = exact ; plus vieux sans activité transcripts = encore exact ; avec activité = plancher « ≥ X % » ; sinon indisponible.
6. Resets toujours serveur ; plus de reset synthétique ni d'ancre manuelle.
7. Tokens des transcripts = matière du plancher et de l'historique, jamais un pourcentage.
8. Diagnostic = exactement ces deux sources + tête + journal.

## Ordre de suppression conseillé
diag + docs → B4, B5, B1 → A1-A4, A8, A9 (garde 3 → 1 composite) → B3 → A5, A6, A7, B2 derrière la migration statusLine.
