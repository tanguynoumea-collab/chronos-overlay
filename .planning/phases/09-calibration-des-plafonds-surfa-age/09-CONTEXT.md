# Phase 9: Calibration des plafonds + surfaçage - Context

**Gathered:** 2026-07-09
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

L'utilisateur calibre les plafonds de tokens (manuel via menu + auto opportuniste) et voit les tokens
estimés en texte secondaire — sans qu'une estimation ne soit jamais présentée comme exacte.

Requirements couverts : CAL-01, CAL-02, CAL-03, NET-02.
</domain>

<decisions>
## Implementation Decisions

### Calibration manuelle (CAL-01 — verrouillé)
- Item de menu contextuel « Calibrer les plafonds… » → dialogue minimal (pattern RecalibrationPrompt
  existant en Chronos.Views, hors gate de pureté) : deux champs numériques (plafond 5 h, plafond hebdo),
  valeurs actuelles pré-remplies, vide = null (pas de plafond → couleur neutre).
- Persistance immédiate dans settings.json (pattern Load frais → with → Save, leçon GAP-1 : NE PAS
  écrire depuis une copie périmée). Application au prochain GetAsync (le provider Load() déjà frais — Phase 8).

### Calibration auto opportuniste (CAL-02 — verrouillé)
- Déclencheur : un snapshot dont une fenêtre est Exact avec Utilization > 0 arrive (usage.json réel)
  ET des tokens JSONL sont mesurables sur la même fenêtre au même moment.
- Plafond déduit = tokens_fenêtre / utilization_exacte. Mémorisé dans settings.json AVEC un timestamp
  de calibration auto (champs : FiveHourBudgetAutoCalibratedAt / Weekly...).
- Règle de priorité : une saisie MANUELLE plus récente ne doit jamais être écrasée silencieusement →
  ajouter aussi un timestamp de saisie manuelle (ou un flag source Manual/Auto) ; l'auto n'écrase que
  l'auto ou l'absence de valeur.
- Emplacement : logique neutre (couche Services, testable) branchée dans le flux de snapshots —
  candidat naturel : à l'écoute de RefreshOrchestrator.SnapshotChanged ou dans le composite. Choisir
  l'emplacement le plus simple SANS créer de cycle de dépendances ; un petit service dédié
  (BudgetAutoCalibrator) écoutant l'orchestrateur est acceptable.
- Réalité du poste utilisateur : usage.json restera null en pratique (app bureau) — CAL-02 est un
  bonus opportuniste, il doit être inerte et sans coût quand il n'y a jamais d'Exact.

### Honnêteté (CAL-03 — verrouillé)
- Un plafond calibré (manuel ou auto) ne change PAS la Reliability : Estimated reste Estimated,
  badge « estimée » conservé. Seul un snapshot Exact réel supprime le badge (comportement existant).

### Surfaçage des tokens (NET-02 — verrouillé)
- Quand la source d'une fenêtre est Estimated : afficher les tokens estimés en texte secondaire discret
  (ex. « ≈ 62,5 M tokens » formaté français, abrégé M/k) près du countdown ou en pied de cadran.
- Ne rien afficher quand Exact (les pourcentages exacts suffisent) ni quand aucune donnée.
- Tokens exposés depuis WindowState.EstimatedTokens (existant) via le WindowGaugeViewModel.

### Claude's Discretion
Format exact de l'abréviation des tokens, layout précis du texte secondaire (rester discret, tokens
de design existants #A9A8B2), structure du dialogue de calibration, nommage des champs settings.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- RecalibrationPrompt/IRecalibrationPrompt (Phase 6) : pattern dialogue à dupliquer/étendre pour les plafonds.
- ChronosSettings : FiveHourTokenBudget/WeeklyTokenBudget (long?) déjà présents (Phase 8) — ajouter les
  métadonnées de source/timestamp de calibration.
- SettingsService : Save atomique, Load tolérant.
- RefreshOrchestrator.SnapshotChanged : point d'écoute pour CAL-02.
- WindowGaugeViewModel : ajouter TokensText (ou équivalent) dérivé de EstimatedTokens.
- MainWindow.xaml : menu contextuel existant (ajouter l'item), zone texte secondaire.
- 119 tests verts — ne rien casser. Garde de pureté : BudgetAutoCalibrator doit être neutre.

### Established Patterns
- [RelayCommand] dans MainViewModel pour les items de menu.
- Leçon GAP-1 : toujours Load() frais avant Save() dans le VM.
- TDD sur la logique pure (règle de priorité manuel/auto = fonction pure testable).

### Integration Points
- App.xaml.cs : DI du nouveau service/prompt.
- MainViewModel : commande CalibrateBudgetsCommand.
- MainWindow.xaml : MenuItem + TextBlock tokens.
</code_context>

<specifics>
## Specific Ideas

- Le formatage des tokens : abréger (62 484 658 → « ≈ 62,5 M »), CultureInfo fr-FR, fonction pure testable.
- Le dialogue plafonds peut proposer un texte d'aide court (« laissez vide pour ne pas colorer l'arc »).
</specifics>

<deferred>
## Deferred Ideas

- Endpoint OAuth /usage (v1.2 éventuel). Tooltip riche (V2-03).
</deferred>
