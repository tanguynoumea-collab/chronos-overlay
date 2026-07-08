# Phase 1: Fondations architecture + squelette overlay - Context

**Gathered:** 2026-07-08
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Une fenêtre overlay vide — borderless, transparente, always-on-top — s'affiche sur le bureau,
portée par un graphe de services câblé dans App.xaml.cs (sans StartupUri) sur cible net8.0-windows.

Requirements couverts : FEN-01 (fenêtre borderless transparente topmost), ROB-04 (réaffirmation périodique du Topmost).

</domain>

<decisions>
## Implementation Decisions

### Décisions déjà verrouillées (PROJECT.md + recherche)
- TFM `net8.0-windows` obligatoire (net8.0 seul ne compile pas WPF) ; SDK .NET 10 installé compile cette cible sans problème.
- Packages : CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting/DependencyInjection ligne 8.0.x.
- Composition root explicite dans App.OnStartup (retirer StartupUri), providers/services en Singleton, disposés dans OnExit.
- Fenêtre : WindowStyle=None, AllowsTransparency=True, Topmost=True, ShowInTaskbar=False, Background transparent.
- ROB-04 : réaffirmation périodique du Topmost via SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE) — sans vol de focus.
- Structure dossiers : Models / Views / ViewModels / Services. MVVM strict, [ObservableProperty]/[RelayCommand].
- Commentaires et UI en français.
- Pas d'animation continue/blur/shadow (AllowsTransparency force le rendu logiciel).

### Claude's Discretion
Tous les autres choix d'implémentation sont à la discrétion de Claude — phase infrastructure,
guidée par le goal ROADMAP, les success criteria et les conventions du CLAUDE.md.

</decisions>

<code_context>
## Existing Code Insights

Projet greenfield — aucun code existant. Le dépôt ne contient que CLAUDE.md et .planning/.
Références : .planning/research/STACK.md (csproj concret, config publish), ARCHITECTURE.md (composition root, IUiDispatcher), PITFALLS.md (pièges transparence/topmost).

</code_context>

<specifics>
## Specific Ideas

- Le csproj doit être correct dès le départ : `net8.0-windows`, UseWPF, propriétés de publish conditionnées (pas dans le PropertyGroup inconditionnel) — corriger le TFM plus tard casse le build WPF.
- La fenêtre vide doit déjà respecter la taille/forme prévue du cadran (fenêtre carrée, ~régions transparentes) pour que la Phase 5 s'y pose sans retouche.
- Prévoir l'abstraction IUiDispatcher dès cette phase (contrat vide acceptable) pour que la couche Services ne référence jamais de type WPF.

</specifics>

<deferred>
## Deferred Ideas

None — discuss phase skipped.

</deferred>
