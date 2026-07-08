# REQUIREMENTS.md — Chronos

## v1 Requirements

### Fenêtre overlay (FEN)

- [x] **FEN-01**: L'utilisateur voit une fenêtre borderless transparente always-on-top (WindowStyle=None, AllowsTransparency=True, Topmost=True, ShowInTaskbar=False)
- [ ] **FEN-02**: L'utilisateur peut déplacer l'overlay par glisser (DragMove)
- [ ] **FEN-03**: L'overlay s'accroche automatiquement au coin d'écran le plus proche au relâchement (WorkingArea, pas Bounds)
- [ ] **FEN-04**: L'accroche aux coins fonctionne sur tous les moniteurs en multi-écrans (coordonnées Per-Monitor, repli si l'écran persisté disparaît)
- [ ] **FEN-05**: L'utilisateur peut basculer l'overlay en arrière-plan (toggle Topmost, renvoi au fond) et le ramener au premier plan
- [ ] **FEN-06**: L'utilisateur accède à un menu contextuel clic droit (arrière-plan, recalibrer, quitter) — seul point d'accès d'une fenêtre sans barre de titre ni barre des tâches
- [ ] **FEN-07**: La position, le coin et les réglages sont persistés dans %APPDATA%/Chronos/settings.json et restaurés au lancement

### Cadran (CAD)

- [ ] **CAD-01**: L'utilisateur voit un cadran circulaire sombre avec graduations (ticks mineurs/majeurs), rendu en XAML pur selon les tokens de design validés
- [ ] **CAD-02**: L'arc extérieur encode la fenêtre 5 h : sa longueur reflète le temps restant avant reset (plein en début de fenêtre, vide à l'approche du reset)
- [ ] **CAD-03**: L'arc intérieur encode la fenêtre hebdomadaire : sa longueur reflète le temps restant avant reset
- [ ] **CAD-04**: La couleur de chaque arc reflète l'utilization (vert #7BB13C → ambre #EFA23A → rouge #D8503A) via un converter dédié
- [ ] **CAD-05**: Un arc passe en gris #5A5960 avec mention « quota épuisé » quand utilization ≥ 1
- [ ] **CAD-06**: L'utilisateur voit au centre un compte à rebours texte des deux fenêtres (temps avant reset 5 h et hebdo)
- [ ] **CAD-07**: Le contrôle RingArc est réutilisable, paramétré par angle et couleur (dérivé de Shape, DefiningGeometry, DP AffectsRender)

### Données (DAT)

- [x] **DAT-01**: La méthode d'obtention de l'objet d'usage Claude Code (five_hour/seven_day : utilization + resets_at) est découverte et documentée dans docs/data-sources.md AVANT le code des providers
- [x] **DAT-02**: L'interface IUsageProvider (GetAsync + événement SnapshotChanged) isole les sources du cadran — couche Services sans aucun type WPF
- [x] **DAT-03**: Les modèles UsageSnapshot (Utilization, ResetsAt, Exhausted, FractionTimeRemaining, SourceReliability) et WindowState sont définis, immuables et neutres
- [x] **DAT-04**: ClaudeUsageObjectProvider lit l'objet d'usage localisé (source primaire, fiable)
- [x] **DAT-05**: JsonlEstimationProvider estime l'usage par somme de tokens des transcripts JSONL (~/.claude/projects), lecture FileShare.ReadWrite en streaming
- [x] **DAT-06**: CompositeUsageProvider tente le primaire puis bascule sur le repli
- [x] **DAT-07**: FractionTimeRemaining des deux fenêtres est calculé à partir de ResetsAt
- [ ] **DAT-08**: Toute donnée issue du repli JSONL est visuellement marquée « estimée » dans l'UI — jamais présentée comme exacte

### Rafraîchissement (RAF)

- [ ] **RAF-01**: Un FileSystemWatcher débouncé déclenche la relecture sur écriture des sources
- [ ] **RAF-02**: Un PeriodicTimer relit les données à intervalle configurable (filet de sécurité du watcher)
- [ ] **RAF-03**: Un DispatcherTimer 1 s interpole arcs et compte à rebours à partir du dernier snapshot, sans I/O
- [ ] **RAF-04**: Tout franchissement thread pool → UI passe par un point de marshaling unique (IUiDispatcher)

### Robustesse (ROB)

- [ ] **ROB-01**: Aucune source disponible n'entraîne de crash : l'overlay affiche un état « données indisponibles »
- [x] **ROB-02**: Le parsing est tolérant : lignes ou champs invalides ignorés, dernière ligne JSONL partielle ignorée
- [ ] **ROB-03**: Le compte à rebours hebdo est traité en best-effort et recalibrable par l'utilisateur (réglage)
- [x] **ROB-04**: Le Topmost est réaffirmé périodiquement (SetWindowPos HWND_TOPMOST, SWP_NOACTIVATE) sans vol de focus

### Déploiement (DEP)

- [ ] **DEP-01**: L'app se publie en exe self-contained mono-fichier win-x64 (PublishSingleFile, PublishTrimmed=false, IncludeNativeLibrariesForSelfExtract=true)
- [ ] **DEP-02**: L'utilisateur peut activer le lancement au démarrage Windows via un raccourci shell:startup

## v2 Requirements

*Différées — la recherche les identifie comme confort, hors cœur v1.*

- **V2-01**: Bande d'activité des sous-agents actifs (blocs Task parsés du JSONL)
- **V2-02**: Révélation au survol (opaque au hover, discret au repos)
- **V2-03**: Tooltip détaillé au survol (chiffres exacts + provenance)
- **V2-04**: Clic-traversant (WS_EX_TRANSPARENT) — incompatible avec le drag, exige tray/hotkey de sortie
- **V2-05**: Icône de zone de notification (tray)
- **V2-06**: Réglage d'opacité et échelle S/M/L

## Out of Scope

- Notifications Windows / toasts — alerte purement visuelle (couleur + grisé)
- Source Cowork séparée — pool partagé compte, déjà inclus dans l'usage de Code
- Dépendances de rendu natives (SkiaSharp…) — XAML pur imposé
- Historique / graphes d'usage — l'overlay est un instrument temps réel, pas un outil d'analyse
- Skins/thèmes configurables — la couleur EST la sémantique
- Redimensionnement libre, multi-comptes, mode wallpaper WorkerW — scope creep
- Droits admin / modifications système — chemins profil utilisateur uniquement
- ClickOnce / SharePoint — exe mono-fichier uniquement
- Animation continue / blur / shadow — AllowsTransparency force le rendu logiciel

## Traceability

*Rempli par la roadmap. Couverture : 32/32 requirements v1 mappées, aucun orphelin.*

| REQ-ID | Phase | Statut |
|--------|-------|--------|
| FEN-01 | Phase 1 | Complete |
| ROB-04 | Phase 1 | Complete |
| DAT-01 | Phase 2 | Complete |
| DAT-02 | Phase 3 | Complete |
| DAT-03 | Phase 3 | Complete |
| DAT-04 | Phase 3 | Complete |
| DAT-05 | Phase 3 | Complete |
| DAT-06 | Phase 3 | Complete |
| DAT-07 | Phase 3 | Complete |
| ROB-02 | Phase 3 | Complete |
| RAF-01 | Phase 4 | Pending |
| RAF-02 | Phase 4 | Pending |
| RAF-03 | Phase 4 | Pending |
| RAF-04 | Phase 4 | Pending |
| CAD-01 | Phase 5 | Pending |
| CAD-02 | Phase 5 | Pending |
| CAD-03 | Phase 5 | Pending |
| CAD-04 | Phase 5 | Pending |
| CAD-05 | Phase 5 | Pending |
| CAD-06 | Phase 5 | Pending |
| CAD-07 | Phase 5 | Pending |
| DAT-08 | Phase 5 | Pending |
| ROB-01 | Phase 5 | Pending |
| FEN-02 | Phase 6 | Pending |
| FEN-03 | Phase 6 | Pending |
| FEN-04 | Phase 6 | Pending |
| FEN-05 | Phase 6 | Pending |
| FEN-06 | Phase 6 | Pending |
| FEN-07 | Phase 6 | Pending |
| ROB-03 | Phase 6 | Pending |
| DEP-02 | Phase 6 | Pending |
| DEP-01 | Phase 7 | Pending |

---
*Last updated: 2026-07-08 after roadmap creation*
