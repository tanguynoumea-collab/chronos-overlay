# Phase 9 — UAT humain (validation sur écran réel)

**Statut : EN ATTENTE de validation utilisateur**
Généré le 2026-07-09 à la vérification de phase (mode autonome : tout ce qui est automatisable a été
vérifié programmatiquement ; les critères ci-dessous exigent un écran/interaction réels et ne peuvent
PAS être automatisés).

## Contexte

La Phase 9 livre la calibration manuelle des plafonds (CAL-01), la calibration auto opportuniste
(CAL-02, inerte en pratique sans snapshot Exact — l'app tourne en mode bureau), l'honnêteté du badge
« estimée » malgré un plafond calibré (CAL-03), et le surfaçage des tokens estimés en texte secondaire
(NET-02). Les vérifications programmatiques suivantes ont déjà été confirmées automatiquement :

- `dotnet build Chronos.sln -c Debug` : **0 avertissement, 0 erreur**.
- `dotnet test Chronos.sln -c Debug` : **150/150 tests verts** (119 avant Phase 9 + 31 nouveaux),
  `ServicesLayerPurityTests` verte (BudgetSource/BudgetCalibration/BudgetAutoCalibrator neutres).
- `CalibrateBudgetsCommand` : Load frais → with (source=Manual/None) → Save → `RequestRefresh()`
  couvert par 3 `[Fact]` (saisie, annulation, non-écrasement GAP-1 d'un writer concurrent).
- `BudgetAutoCalibrator.CalibrateAsync` : déduction + priorité Manual/Auto + inertie totale (aucun
  accès disque, `ThrowingProvider` prouve qu'aucun `GetAsync` n'a lieu) couverts par 3 `[Fact]`.
- Régression CAL-03 : un plafond défini laisse `FiveHour.Reliability == Estimated` (jamais Exact).
- `WindowGaugeViewModel.TokensText`/`HasTokens` : dérivation Estimated+tokens>0 uniquement, couverte
  par 3 `[Fact]` (« ≈ 62,5 M tokens », Exact sans texte, Estimated à 0 token sans texte).
- `MenuItem "Calibrer les plafonds…"` présent dans `MainWindow.xaml`, lié à `CalibrateBudgetsCommand`.
- Deux `TextBlock` (`TokensCinqHeures`/`TokensHebdo`) présents, liés à `TokensText`/`HasTokens`
  (Visibility via `BoolToVis`), FontSize 9, couleur `TexteSecondaire` (#A9A8B2).
- Câblage DI : `IBudgetPrompt`→`BudgetPrompt`, `BudgetAutoCalibrator` résolu eager avant `StartAsync`
  (abonnement à `SnapshotChanged` garanti avant la première charge), `CompositionRootTests` verte.

## Comment lancer

```
dotnet run --project src/Chronos/Chronos.csproj -c Debug
```

## Critères à valider manuellement (écran réel requis)

| # | Exigence | Étape de vérification | Résultat attendu | Statut |
|---|----------|-----------------------|------------------|--------|
| 1 | CAL-01 | Clic droit sur le cadran → « Calibrer les plafonds… » | Dialogue sombre minimal s'ouvre, centré sur l'overlay, deux champs numériques (« Plafond 5 h (tokens) », « Plafond hebdo (tokens) »), texte d'aide « Laissez vide pour ne pas colorer l'arc » | ⬜ |
| 2 | CAL-01 | Rouvrir le dialogue après une première saisie | Les valeurs précédemment saisies sont pré-remplies (pas de champ vide alors qu'un plafond existe) | ⬜ |
| 3 | CAL-01 | Saisir un plafond 5 h (ex. 2000000) et valider | L'arc 5 h se colore selon l'utilisation aussitôt (sans attendre le prochain tick périodique) | ⬜ |
| 4 | CAL-01 | Annuler le dialogue (bouton Annuler ou Échap) après avoir modifié un champ | Aucun changement : les plafonds restent ceux d'avant l'ouverture | ⬜ |
| 5 | CAL-01 | Fermer puis relancer l'app après une calibration manuelle | `%APPDATA%\Chronos\settings.json` contient bien `FiveHourTokenBudget`/`WeeklyTokenBudget` + `FiveHourBudgetSource`/`WeeklyBudgetSource` = "Manual" ; les valeurs sont restaurées au redémarrage | ⬜ |
| 6 | CAL-03 | Après une calibration manuelle (ou en mode repli JSONL usuel), observer le cadran | Le badge « estimée » reste affiché sous la fenêtre calibrée (l'utilization est colorée mais jamais présentée comme exacte) | ⬜ |
| 7 | NET-02 | Observer le cadran en mode repli (app bureau, JSONL) | Sous chaque fenêtre en source Estimated (avec tokens > 0), un texte discret « ≈ N M tokens » ou « ≈ N k tokens » apparaît sous le badge « estimée », petit et gris clair (#A9A8B2), sans perturber le layout | ⬜ |
| 8 | NET-02 | Comparer avec une fenêtre Exact (si un snapshot rate_limits réel est un jour disponible) | Aucun texte de tokens n'apparaît en source Exact (les pourcentages exacts suffisent) | ⬜ (probablement non testable en pratique — app bureau) |
| 9 | Lisibilité | Observer la taille/contraste du texte « ≈ N M tokens » sur le fond de l'overlay | Le texte reste lisible sans être trop visible (discret, cohérent avec les autres mentions secondaires : « estimée », « quota épuisé ») | ⬜ |

## Consigne en cas d'écart

Toute anomalie constatée est consignée comme **gap** pour un plan de clôture (`--gaps`) ;
ne pas « corriger en douce » sans replanifier.
