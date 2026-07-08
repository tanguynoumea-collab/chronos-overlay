# Phase 3: Modèles + pipeline de données - Context

**Gathered:** 2026-07-08
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Un pipeline de données neutre produit des UsageSnapshot immuables — fiables depuis l'objet primaire
ou estimés depuis les JSONL — entièrement isolé du cadran, sans aucun type WPF.

Requirements couverts : DAT-02, DAT-03, DAT-04, DAT-05, DAT-06, DAT-07, ROB-02.
</domain>

<decisions>
## Implementation Decisions

### Décisions verrouillées par la découverte (docs/data-sources.md — SOURCE DE VÉRITÉ)
- Source primaire = bloc `rate_limits` du contrat statusLine officiel de Claude Code. RIEN n'est persisté sur disque : il faut CODER LE PONT statusLine → fichier dans cette phase (script Node ou équivalent configuré dans ~/.claude/settings.json), NON DESTRUCTIF vis-à-vis du gsd-statusline.js existant (chaîner/wrapper, ne pas remplacer aveuglément).
- Schéma réel : `used_percentage` (0..100) → Utilization = used_percentage / 100.0 ; `resets_at` = epoch SECONDES → DateTimeOffset.FromUnixTimeSeconds. Fenêtres : five_hour / seven_day (chacune peut être absente indépendamment ; rate_limits absent pour non-Pro/Max ou avant 1re réponse API).
- Staleness : le fichier pont peut être figé hors session active → horodater l'écriture du pont et exposer l'âge de la donnée dans le snapshot.
- Repli JSONL : %USERPROFILE%\.claude\projects\**\*.jsonl, champ message.usage (input_tokens, output_tokens, cache_creation_input_tokens, cache_read_input_tokens), timestamps ISO 8601 UTC (Z). Sous-agents dans sous-dossier subagents/ (v2.1.202). Lecture FileShare.ReadWrite en streaming, try/catch par ligne, dernière ligne partielle ignorée.

### Contrats à implémenter (verrouillés par REQUIREMENTS/ARCHITECTURE)
- IUsageProvider : GetAsync + événement SnapshotChanged (DAT-02). Couche Services SANS AUCUN type WPF.
- UsageSnapshot immuable (record) : Utilization, ResetsAt, Exhausted (utilization ≥ 1), FractionTimeRemaining, SourceReliability (Fiable/Estimé) — pour CHACUNE des deux fenêtres (DAT-03). Ajouter l'âge de la donnée (staleness).
- ClaudeUsageObjectProvider : lit le fichier pont JSON (DAT-04).
- JsonlEstimationProvider : somme des tokens dans la fenêtre glissante, marqué Estimé (DAT-05). IMPORTANT honnêteté : sans plafond publié fiable, l'estimation d'utilization JSONL est approximative — la documenter comme telle ; ne jamais la présenter comme exacte.
- CompositeUsageProvider : primaire puis repli (DAT-06).
- FractionTimeRemaining calculé depuis ResetsAt (DAT-07) : (resets_at - now) / durée de fenêtre (5 h ; hebdo = 7 j best-effort).
- Parsing tolérant partout (ROB-02) : ligne/champ invalide ignoré, jamais d'exception non gérée, jamais de valeur inventée.

### Testabilité (verrouillé)
- Horloge injectable (interface type IClock) pour tester FractionTimeRemaining et fenêtres glissantes.
- Système de fichiers abstrait ou chemins injectables pour tester les providers sans toucher au vrai ~/.claude.
- Tests unitaires xUnit dans tests/Chronos.Tests (projet existant) : parsing tolérant (lignes corrompues, ligne partielle, champs manquants), mapping used_percentage→Utilization, calculs de fraction, bascule composite.

### Claude's Discretion
Détails d'implémentation du pont (langage du script, nom du fichier de sortie sous %USERPROFILE%\.claude ou %APPDATA%\Chronos), organisation interne des parsers, structure exacte des tests.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- src/Chronos/Services/IUiDispatcher.cs + WpfUiDispatcher.cs (frontière de thread posée en Phase 1).
- Composition root Generic Host dans App.xaml.cs — enregistrer les nouveaux services ici (Singleton).
- tests/Chronos.Tests opérationnel (xUnit + Xunit.StaFact), 3 tests verts.

### Established Patterns
- MVVM strict, services purs sans WPF, DI par constructeur, commentaires français.
- InternalsVisibleTo Chronos.Tests déjà en place dans Chronos.csproj.

### Integration Points
- App.xaml.cs : enregistrement DI des providers.
- docs/data-sources.md : contrat complet des sources (à lire par l'exécuteur AVANT tout code).
</code_context>

<specifics>
## Specific Ideas

- Le pont statusLine doit être configuré/documenté mais son installation dans ~/.claude/settings.json doit préserver la statusline existante de l'utilisateur (wrapper qui appelle gsd-statusline.js puis écrit le JSON rate_limits dans le fichier pont).
- Chemin du fichier pont : sous profil utilisateur uniquement (ex. %APPDATA%\Chronos\usage.json).
- La couche Services doit compiler sans référence à PresentationFramework (vérifiable).
</specifics>

<deferred>
## Deferred Ideas

- Bande d'activité sous-agents (V2-01) — exploitation STRUCTURÉE différée (UI d'activité). Les tokens des transcripts subagents/ sont en revanche inclus dans la somme du repli JSONL (même pool de quota compte) — arbitrage orchestrateur 2026-07-08.
</deferred>
