# Phase 6: Comportements overlay (placement + interaction) - Context

**Gathered:** 2026-07-08
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

L'utilisateur peut placer, ranger, régler et faire persister l'overlay entièrement via ses
interactions, sur tous ses moniteurs.

Requirements couverts : FEN-02, FEN-03, FEN-04, FEN-05, FEN-06, FEN-07, ROB-03, DEP-02.
</domain>

<decisions>
## Implementation Decisions

### Placement (verrouillé — FEATURES.md recherche)
- FEN-02 : déplacement par glisser via DragMove (MouseLeftButtonDown sur le cadran).
- FEN-03 : au relâchement, accroche automatique au coin d'écran LE PLUS PROCHE — cible la WorkingArea (pas Bounds, pour éviter la barre des tâches), avec une marge (~12 px).
- FEN-04 : multi-écrans — coordonnées Per-Monitor V2 (manifest déjà en place Phase 1), coins calculés sur le moniteur contenant la fenêtre ; repli sur écran primaire si l'écran persisté a disparu au rebranchement.
- Chaîne de dépendance stricte : drag → snap → persistance.

### Menu contextuel (verrouillé — table stake critique)
- FEN-06 : menu clic droit = SEUL point d'accès (pas de barre de titre, pas de barre des tâches, pas d'Alt-Tab). Items : « Arrière-plan » (toggle), « Recalibrer le reset hebdo… », « Lancer au démarrage » (toggle DEP-02), « Quitter ».
- Sans « Quitter » l'utilisateur doit tuer le process — c'est le garde-fou n°1.
- ContextMenu WPF standard, commandes [RelayCommand] dans le ViewModel.

### Arrière-plan (FEN-05)
- Toggle Topmost : bascule Topmost=false + renvoi au fond (SetWindowPos HWND_BOTTOM) ; retour premier plan = Topmost=true + réactivation TopmostGuard.
- Le TopmostGuard (Phase 1) doit être suspendu quand l'overlay est en mode arrière-plan (sinon il re-force le topmost toutes les 2 s).
- Pas de re-assertion agressive : pas de clignotement, pas de vol de focus.

### Persistance (FEN-07)
- %APPDATA%\Chronos\settings.json (ChronosPaths existant) : position (X,Y), coin d'accroche, écran (device name), mode arrière-plan, intervalle de refresh, offset de recalibrage hebdo.
- Écriture atomique (temp + rename, pattern du pont), lecture tolérante (fichier corrompu → défauts).
- Service SettingsService neutre (couche Services sans WPF), restauration au lancement AVANT affichage.

### Recalibrage hebdo (ROB-03)
- Le reset hebdo dérive : l'utilisateur doit pouvoir recalibrer. Approche minimale : dialogue simple (ou item de menu) permettant de définir un OFFSET/une date d'ancrage appliqué au resets_at hebdo estimé quand la source est le repli JSONL. Quand la source primaire fournit resets_at exact, le recalibrage ne s'applique PAS (les chiffres exacts priment).
- Persisté dans settings.json.

### Autostart (DEP-02)
- Toggle menu « Lancer au démarrage » : crée/supprime un raccourci .lnk dans shell:startup (%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup) pointant vers l'exe courant. Aucun droit admin. WSH COM (IWshRuntimeLibrary) ou écriture .lnk via PowerShell — choisir la voie la plus simple sans dépendance native.

### Claude's Discretion
Marge de snap exacte, animation de snap (aucune si coûteuse — rendu logiciel), structure du dialogue de recalibrage (minimal), organisation des services.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- MainWindow.xaml/cs : fenêtre 220×220, StartClock sur Loaded, TopmostGuard attaché sur SourceInitialized.
- TopmostGuard (Phase 1) : à étendre avec Suspend/Resume pour le mode arrière-plan.
- ChronosPaths (Phase 3) : répertoire %APPDATA%\Chronos.
- Pattern d'écriture atomique (pont Node) : réutiliser en C#.
- MainViewModel : ajouter les commandes de menu ([RelayCommand]).
- 68 tests verts — ne rien casser.

### Established Patterns
- MVVM strict : commandes dans le VM, code-behind limité au strict nécessaire fenêtre (DragMove doit être dans le code-behind — c'est une API fenêtre, acceptable).
- Services neutres testables (SettingsService, calcul du coin le plus proche = logique pure testable).

### Integration Points
- App.xaml.cs : DI de SettingsService, restauration position au démarrage.
- MainWindow : événements souris, ContextMenu.
</code_context>

<specifics>
## Specific Ideas

- Le calcul « coin le plus proche » (rectangle fenêtre + WorkingArea → Point cible) doit être une fonction PURE testable (pas de dépendance à System.Windows.Forms.Screen dans la logique — abstraire l'écran en rectangles).
- Attention DPI : WPF travaille en DIU (1/96"), les APIs écran en pixels physiques — utiliser les conversions du PresentationSource ou SystemParameters.WorkArea pour rester en DIU sur le moniteur courant.
- Le clic gauche = drag ; le clic droit = menu. Pas de conflit.
</specifics>

<deferred>
## Deferred Ideas

- Tray icon (V2-05), clic-traversant (V2-04), opacité/échelle (V2-06).
</deferred>
