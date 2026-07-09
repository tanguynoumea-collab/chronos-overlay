# Phase 11: Intégration composite + réglage - Context

**Gathered:** 2026-07-09
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped ; recherche sautée — intégration pure sur briques connues)

<domain>
## Phase Boundary

Le ClaudeOAuthUsageProvider (Phase 10) devient la source PRIMAIRE exacte : les arcs affichent les
vrais pourcentages sans badge « estimée », avec bascule automatique et un réglage on/off.

Requirements couverts : INT-01, INT-02, INT-03.
</domain>

<decisions>
## Implementation Decisions

### Chaîne composite à 3 niveaux (INT-01 — verrouillé)
- Aujourd'hui : `CompositeUsageProvider(primary: ClaudeUsageObjectProvider, fallback: JsonlEstimationProvider)`.
  Le composite prend déjà la MEILLEURE source PAR FENÊTRE (Exact > Estimated > Unavailable).
- Nouvelle chaîne : OAuth (exact, prioritaire) → pont statusLine usage.json (exact) → JSONL (estimé).
- Implémentation minimale recommandée : **imbriquer** les composites —
  `new CompositeUsageProvider(primary: oauthGated, fallback: new CompositeUsageProvider(claudeUsageObject, jsonl))`.
  Le composite existant gère déjà le « best par fenêtre » et la staleness (fix v1.1) ; l'imbrication le
  réutilise sans réécriture. (Alternative acceptable : généraliser le composite à N providers ; choisir
  la voie la plus simple qui ne casse pas les tests Assert.Same existants.)

### Réglage on/off (INT-03 — verrouillé)
- Setting `OAuthUsageEnabled` (bool, défaut TRUE) dans ChronosSettings (persisté settings.json, leçon GAP-1).
- Item de menu contextuel « Usage exact (OAuth) » cochable (comme arrière-plan/autostart), lié à une
  commande VM qui bascule le setting, persiste (Load frais → with → Save) et redéclenche RequestRefresh.
- Mécanisme de désactivation SANS toucher au token quand off : un petit wrapper « gated » autour du
  ClaudeOAuthUsageProvider qui, si OAuthUsageEnabled == false, retourne UsageSnapshot.Empty (Unavailable)
  SANS instancier/appeler le ClaudeTokenReader ni l'endpoint. Off = comportement v1.1 strict, zéro accès token.
  Le wrapper lit le setting frais à chaque GetAsync (comme JsonlEstimationProvider lit ses plafonds).

### Badge « estimée » (INT-02 — déjà quasi acquis)
- Le badge est lié à IsEstimated (Reliability == Estimated). Quand une fenêtre vient de l'OAuth (Exact),
  IsEstimated devient false → badge masqué automatiquement + arc en vraie couleur (utilization exacte).
  Rien de neuf à coder côté binding ; VÉRIFIER par test qu'une fenêtre Exact issue de l'OAuth n'affiche
  pas le badge et prend la couleur de rampe. Le surfaçage des tokens estimés (NET-02) ne s'affiche que si
  Estimated → naturellement masqué en mode exact.

### Honnêteté (transverse)
- Exact ne s'affiche QUE si l'OAuth (ou le pont) fournit vraiment un Exact ; sinon on retombe sur estimé
  (badge « estimée » réapparaît). L'honnêteté joue dans les deux sens — c'est le cœur du projet.

### Claude's Discretion
Imbrication vs composite N-aire ; nom du wrapper gated (ex. GatedOAuthUsageProvider) ; libellé exact de
l'item de menu ; ordre des items dans le menu.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- App.xaml.cs lignes ~78-82 : enregistrement DI du composite (à modifier pour la chaîne à 3).
- CompositeUsageProvider : best-par-fenêtre + staleness (fix v1.1) — réutilisable par imbrication.
- ClaudeOAuthUsageProvider + ClaudeTokenReader (Phase 10).
- ChronosSettings + SettingsService : ajouter OAuthUsageEnabled (bool). Pattern de toggle menu =
  ToggleBackground/ToggleAutostart (MainViewModel) + MenuItem cochable (MainWindow.xaml).
- RefreshOrchestrator.RequestRefresh() (Phase 9) : re-déclenche un GetAsync après changement de réglage.
- WindowGaugeViewModel.IsEstimated / badge XAML (Phase 5) : masquage auto en mode Exact.
- 178 tests verts — ne rien casser (notamment les Assert.Same du composite).

### Established Patterns
- Toggle menu → [RelayCommand] → Load frais/with/Save → RequestRefresh (GAP-1).
- Tests : provider gated testable via fake settings ; composite imbriqué testable.

### Integration Points
- App.xaml.cs (DI de la chaîne), MainViewModel (commande toggle + état coché), MainWindow.xaml (MenuItem),
  ChronosSettings (champ + défaut), CadranBindingTests (ctor VM si un paramètre est ajouté).
</code_context>

<specifics>
## Specific Ideas

- Défaut OAuthUsageEnabled = true : dès l'installation, l'utilisateur a les vrais chiffres (c'est le but
  de v1.2). Il peut désactiver s'il ne veut pas que Chronos touche au token.
- Vérification finale en app réelle (checkpoint) : republier, lancer, confirmer que le cadran affiche les
  vrais % (~74/93) SANS badge « estimée », arcs colorés.
</specifics>

<deferred>
## Deferred Ideas

- Refresh token (v1.3). Sous-fenêtres opus/sonnet/cowork exposées par l'endpoint (v1.3+).
</deferred>
