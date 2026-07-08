# Feature Research

**Domain:** Overlay de bureau Windows always-on-top (widget/HUD de monitoring mono-fonction) — suivi de quotas API
**Researched:** 2026-07-08
**Confidence:** HIGH (conventions overlays desktop bien établies : Rainmeter, MSI Afterburner OSD, gadgets/horloges de bureau ; mécaniques WPF vérifiées)

> Périmètre de cette recherche : **fonctionnalités d'INTERACTION et d'UX** d'un overlay de bureau.
> La maquette visuelle (cadran, arcs, couleurs, compte à rebours) est **validée et hors périmètre** — on ne la remet pas en cause.

## Feature Landscape

### Table Stakes (Users Expect These)

Sans ces fonctions, l'overlay est concrètement inutilisable ou frustrant au quotidien.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Déplacement par glisser** (drag n'importe où sur le cadran) | Un widget sans barre de titre doit être repositionnable, sinon il est piégé où il apparaît | LOW | `DragMove()` sur `MouseLeftButtonDown`. Interdit avec clic-traversant actif (voir dépendances) |
| **Persistance de la position** entre sessions | Reconfigurer à chaque lancement est rédhibitoire pour un outil « lancé au démarrage » | LOW | Déjà prévu : `settings.json` sous `%APPDATA%/Chronos`. Sauver X/Y + coin + écran |
| **Multi-écrans corrects** (rester sur le bon écran, dans la zone de travail) | Setup dev = souvent 2-3 écrans ; un widget qui saute d'écran ou passe sous la barre des tâches est cassé | MEDIUM | Utiliser `WorkingArea` (pas `Bounds`) pour éviter la barre des tâches. Gérer débranchement d'écran → repli sur écran valide |
| **Accroche au coin le plus proche** (snap corners) | Convention forte des HUD : un widget « traîne » proprement dans un coin, pas au milieu de l'écran | MEDIUM | Déjà prévu. Snap au relâchement vers le coin le + proche de la `WorkingArea`. Marge fixe (ex. 16px) |
| **Mode arrière-plan / bascule Topmost** | L'always-on-top devient gênant quand on lit un doc ou regarde une vidéo ; il faut pouvoir l'envoyer au fond sans le fermer | MEDIUM | Déjà prévu. Attention : `Topmost=true` perd contre les apps **plein écran exclusif** (jeux) — comportement Windows normal, ne pas sur-ingénier |
| **Accès aux réglages + Quitter** (menu contextuel clic droit) | `ShowInTaskbar=False` + borderless = **aucun** bouton de fermeture ni entrée Alt-Tab → l'utilisateur ne peut pas quitter l'app | LOW | **Souvent oublié, critique.** Menu contextuel WPF sur clic droit du cadran : Réglages / Arrière-plan / Recalibrer / Quitter. Alternative/complément : icône zone de notification (tray) |
| **État « données indisponibles » sans crash** | Un HUD de monitoring qui plante quand la source est absente détruit la confiance | MEDIUM | Déjà prévu. Cadran grisé + libellé explicite plutôt que fenêtre morte ou exception |
| **Curseur/feedback de déplacement** | L'utilisateur doit comprendre qu'il peut saisir le cadran (sinon il ne devine pas qu'il est déplaçable) | LOW | Curseur `SizeAll` au survol quand déplaçable, ou légère surbrillance du rim au hover |

### Differentiators (Competitive Advantage)

Non indispensables, mais elles élèvent nettement le confort d'un widget « posé pour toujours » sur le bureau. À doser : rester minimal.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Opacité réglable** (globale, via réglages) | Discrétion : l'utilisateur ajuste combien le widget « s'efface » dans le décor | LOW | `Window.Opacity` ou `AllowsTransparency` déjà actif. Un simple curseur/valeur en settings suffit |
| **Révélation au survol** (semi-transparent au repos, opaque au hover) | Le meilleur des deux mondes : discret quand ignoré, lisible quand consulté. Convention Rainmeter (`AlphaValue`/hover) | LOW | Animation d'opacité sur `MouseEnter`/`MouseLeave`. Fort ratio valeur/coût |
| **Clic-traversant (click-through)** togglable | Le widget devient « décoratif » : les clics passent à travers vers la fenêtre en dessous → zéro gêne | MEDIUM | `WS_EX_LAYERED + WS_EX_TRANSPARENT` via interop. **Conflit majeur avec le drag** : en mode traversant on ne peut plus saisir le cadran → prévoir un raccourci global ou le désactiver via le tray pour repositionner |
| **Recalibrage du reset hebdo** | Le reset 7 j dérive (~72 h, ancrage non documenté) ; laisser l'utilisateur réaligner rend le compte à rebours crédible | LOW | Déjà prévu (best-effort). Entrée de menu « Recalibrer reset hebdo » → saisie de l'instant d'ancrage, persistée |
| **Lancement au démarrage Windows** | Un moniteur de quota n'a de valeur que s'il est *toujours* là sans y penser | LOW | Déjà prévu : raccourci `shell:startup`. Case à cocher dans les réglages pour poser/retirer le raccourci |
| **Info-bulle détaillée au survol** | Le cadran encode 4 variables ; un tooltip donne les chiffres exacts (utilization %, resets_at horodaté, source active + mention « estimée ») | LOW | `ToolTip` WPF au hover. Renforce l'honnêteté des chiffres (Core Value) |
| **Verrouillage de position** (lock) | Une fois placé, éviter de le déplacer par accident en cliquant | LOW | Convention Rainmeter. Toggle menu ; simple garde sur `DragMove`. Recouvre en partie le clic-traversant |
| **Taille/échelle réglable** (2-3 crans prédéfinis : S/M/L) | Adaptation densité de pixels / préférence, sans redimensionnement libre | MEDIUM | Cadran vectoriel (XAML pur) → scale propre. **Limiter à des paliers**, pas de resize libre (voir anti-features) |

### Anti-Features (Commonly Requested, Often Problematic)

Fonctions séduisantes qui feraient dériver un overlay mono-fonction vers une usine à gaz. À NE PAS construire délibérément.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **Notifications / toasts Windows** | « Préviens-moi quand j'approche de la limite » | Casse le principe « savoir sans y penser » ; interruptions, gestion d'état de seuils, permissions | Alerte **purement visuelle** (couleur vert→ambre→rouge, grisé si épuisé). Déjà décidé hors périmètre |
| **Historique / graphes de consommation** | « Voir ma tendance d'usage sur la semaine » | Nécessite stockage temporel, agrégation, UI de graphe, fenêtre séparée → une autre app | Le cadran est un **instantané** ; l'historique est un produit différent |
| **Redimensionnement libre** (poignées de resize) | « Le mettre à ma taille exacte » | Poignées sur borderless = complexe, ratio à préserver, états de layout multiples, bugs DPI | Paliers d'échelle S/M/L prédéfinis (différenciateur), ratio verrouillé |
| **Skins / thèmes configurables par l'utilisateur** | « Personnaliser les couleurs » | Les tokens de design (rampe utilization) **encodent l'information** (vert/ambre/rouge = sémantique) ; les rendre configurables casse la lisibilité et démultiplie les combinaisons à tester | Un seul thème sombre soigné, validé sur maquette. Non négociable |
| **Multi-comptes / multi-sources visibles simultanément** | « Suivre plusieurs comptes Claude » | Pool partagé au niveau compte ; multiplie fenêtres, sélecteurs, état. Hors cas d'usage mono-utilisateur | Un overlay = un compte. Le provider composite gère déjà primaire→repli |
| **Source Cowork séparée** | « Voir Cowork à part » | Pool partagé compte : déjà inclus dans l'usage de Code | Déjà décidé hors périmètre |
| **Sons / alertes audio** | « Bip quand épuisé » | Intrusif, contraire à l'esprit ambiant/passif d'un HUD de bureau | Grisé visuel du cadran |
| **Fenêtre de configuration riche** (onglets, thèmes, profils) | « Tout paramétrer » | Sur-ingénierie pour ~5 réglages (opacité, échelle, autostart, recalibrage, clic-traversant) | Menu contextuel clic droit + petit panneau/dialogue léger. Pas de shell d'options complet |
| **Épinglage au bureau / mode « wallpaper » (WorkerW)** | « L'intégrer au fond d'écran comme Rainmeter On Desktop » | Injection dans `WorkerW`/`Progman` = fragile, casse à chaque MAJ d'explorer, incompatible avec always-on-top | La bascule Topmost/arrière-plan (table stakes) couvre le besoin |
| **Raccourcis clavier globaux configurables** | « Toggle au clavier » | Hooks globaux = permissions, conflits, surface de bug ; superflu pour un widget qu'on ne touche presque jamais | Menu contextuel suffit ; éventuellement UN hotkey fixe non configurable pour sortir du mode clic-traversant |

## Feature Dependencies

```
Déplacement par glisser (DragMove)
    └──requires──> Fenêtre borderless + AllowsTransparency (base, déjà actif)

Accroche aux coins (snap)
    ├──requires──> Déplacement par glisser (snap = au relâchement du drag)
    └──requires──> Multi-écrans (calcul du coin sur la WorkingArea du bon écran)

Persistance de position
    └──requires──> Déplacement + Snap + Multi-écrans (on persiste écran+coin+offset)

Clic-traversant (click-through)  ──CONFLICTS──>  Déplacement par glisser
Verrouillage de position (lock)  ──CONFLICTS──>  Déplacement par glisser
Révélation au survol / Tooltip   ──CONFLICTS──>  Clic-traversant (pas d'events souris en mode traversant)

Menu contextuel (Réglages/Quitter)
    └──enables──> Opacité, Échelle, Autostart, Recalibrage, sortie du clic-traversant
    (indispensable car ShowInTaskbar=False → seul point d'accès à l'app)

Mode arrière-plan (toggle Topmost)
    └──requires──> Interop Win32 (SetWindowPos HWND_TOPMOST / HWND_BOTTOM)
```

### Dependency Notes

- **Snap requiert Drag + Multi-écrans :** l'accroche se déclenche au relâchement du glisser et doit viser le coin de la `WorkingArea` de l'écran *courant* (DPI/barre des tâches inclus), pas du bureau virtuel global.
- **Clic-traversant CONFLICTUE avec Drag / Hover / Tooltip :** une fenêtre `WS_EX_TRANSPARENT` ne reçoit **aucun** événement souris → impossible de la saisir, de la survoler ou d'afficher un tooltip. Il faut un chemin de sortie hors-fenêtre (entrée tray, ou hotkey fixe) pour la repositionner. **C'est la décision d'interaction la plus structurante** : décider tôt si le clic-traversant est dans la v1.
- **Verrouillage vs Clic-traversant :** se recouvrent partiellement (les deux « figent » le widget). Ne pas construire les deux en v1 — le clic-traversant est un sur-ensemble plus utile ; le lock est un intermédiaire léger si le clic-traversant est différé.
- **Menu contextuel = prérequis d'accès :** sans lui, aucune façon de quitter ou configurer. À traiter comme fondation, pas comme feature de confort.
- **Persistance dépend de tout le pipeline de placement :** ne persister qu'après avoir stabilisé écran+coin+offset, sinon on sauve des coordonnées invalides au rebranchement d'écran.

## MVP Definition

### Launch With (v1)

Le minimum pour qu'un overlay always-on-top soit *utilisable au quotidien*, pas seulement affichable.

- [ ] **Déplacement par glisser** — sans lui le widget est piégé ; fondation de tout le placement
- [ ] **Accroche au coin le plus proche (multi-écrans, WorkingArea)** — convention HUD, évite la barre des tâches
- [ ] **Persistance position/coin/écran** (`settings.json`) — indispensable pour un outil lancé au démarrage
- [ ] **Menu contextuel clic droit** (Réglages / Arrière-plan / Recalibrer / Quitter) — seul point d'accès et de sortie de l'app
- [ ] **Bascule mode arrière-plan (Topmost on/off)** — l'always-on-top permanent est vite gênant
- [ ] **État « données indisponibles »** — robustesse, confiance
- [ ] **Lancement au démarrage** (case à cocher `shell:startup`) — valeur = présence permanente
- [ ] **Info-bulle / tooltip détaillé au survol** — chiffres exacts + mention « estimée » (sert la Core Value d'honnêteté)

### Add After Validation (v1.x)

Confort une fois le cœur validé.

- [ ] **Opacité réglable + révélation au survol** — déclencheur : retour utilisateur « trop présent / pas assez discret »
- [ ] **Recalibrage fin du reset hebdo** — déclencheur : dérive constatée du compte à rebours en usage réel
- [ ] **Paliers d'échelle S/M/L** — déclencheur : plainte de lisibilité selon densité d'écran

### Future Consideration (v2+)

- [ ] **Clic-traversant togglable** — différer : décision d'interaction lourde (conflit drag/hover), à valider seulement si la gêne au clic est réelle. Exige un chemin de repositionnement hors-fenêtre
- [ ] **Bande d'activité des sous-agents** (blocs Task JSONL) — déjà marqué optionnel/différé dans PROJECT.md ; parsing en direct, complexité MEDIUM

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Déplacement par glisser | HIGH | LOW | P1 |
| Menu contextuel (Réglages/Quitter) | HIGH | LOW | P1 |
| Persistance position | HIGH | LOW | P1 |
| Accroche coins multi-écrans | HIGH | MEDIUM | P1 |
| Bascule arrière-plan (Topmost) | HIGH | MEDIUM | P1 |
| État données indisponibles | HIGH | MEDIUM | P1 |
| Lancement au démarrage | HIGH | LOW | P1 |
| Tooltip détaillé au survol | MEDIUM | LOW | P1 |
| Révélation au survol / opacité | MEDIUM | LOW | P2 |
| Recalibrage reset hebdo | MEDIUM | LOW | P2 |
| Paliers d'échelle S/M/L | LOW | MEDIUM | P2 |
| Verrouillage de position | LOW | LOW | P3 |
| Clic-traversant togglable | MEDIUM | MEDIUM | P3 |
| Bande d'activité sous-agents | LOW | MEDIUM | P3 |

**Priority key:** P1 = requis au lancement · P2 = à ajouter dès que possible · P3 = confort/futur

## Competitor Feature Analysis

| Feature | Rainmeter (widgets bureau) | MSI Afterburner OSD / HUD jeu | Notre approche (Chronos) |
|---------|----------------------------|-------------------------------|--------------------------|
| Déplacement | Glisser libre partout | Position fixe/coin configurée | Glisser + **snap coin auto** |
| Placement multi-écrans | `@N` par écran, keep-on-screen | Écran cible en config | Snap sur `WorkingArea` de l'écran courant |
| Z-order | Stay Topmost/Topmost/Normal/Bottom/On Desktop | Overlay au-dessus du jeu | **Toggle simple** Topmost ↔ arrière-plan (pas 5 modes) |
| Opacité | `AlphaValue` + hover reveal | Opacité OSD réglable | Opacité globale + hover reveal (v1.x) |
| Clic-traversant | Option par skin | N/A (overlay non interactif) | Différé v2, conflit drag assumé |
| Persistance | `Rainmeter.ini` | Profil app | `settings.json` sous `%APPDATA%` |
| Personnalisation | Skins entièrement libres | Layout OSD configurable | **Un seul thème** (couleur = sémantique, non éditable) |
| Notifications | Plugins possibles | Seuils/alertes | **Aucune** — alerte purement visuelle |

**Lecture :** Chronos reprend les *table stakes* de placement de Rainmeter (drag, snap, keep-on-screen, persistance, opacité/hover) mais **coupe volontairement** la configurabilité (skins, z-order à 5 niveaux, plugins, notifications) qui font de Rainmeter une plateforme plutôt qu'un widget mono-fonction. C'est le bon compromis pour un outil minimal.

## Notes techniques transverses (impactant les features)

- **Clic-traversant vs drag** = le vrai arbitrage d'interaction. Une fois `WS_EX_TRANSPARENT` posé, la fenêtre ne reçoit plus de souris : décider du mécanisme de sortie (tray/hotkey) *avant* de coder cette feature.
- **Topmost et plein écran exclusif :** `Topmost=true` passe **sous** les jeux/vidéos en plein écran exclusif (comportement Windows). Ne pas tenter de forcer (re-assertion agressive = clignotements, vol de focus). La bascule arrière-plan manuelle est la bonne réponse.
- **DPI par écran (Per-Monitor v2) :** le snap aux coins et l'échelle doivent tenir compte du facteur DPI de l'écran cible ; un widget vectoriel XAML rescale proprement mais les calculs de position doivent être en coordonnées physiques correctes. Déclarer l'app PerMonitorV2 dans le manifeste.
- **Rebranchement / changement de résolution d'écran :** prévoir un repli si l'écran/coin persisté n'existe plus au démarrage (sinon widget hors-écran invisible). S'abonner aux changements d'affichage.
- **Point d'accès unique :** `ShowInTaskbar=False` implique que le menu contextuel (et/ou l'icône tray) est le **seul** moyen de quitter/configurer — à ne surtout pas oublier, sinon l'utilisateur doit tuer le process.

## Sources

- Rainmeter Documentation — Arranging Skins, Skin sections, Default Settings (snap edges + override Ctrl, keep-on-screen, transparency `AlphaValue`, z-order Stay Topmost/Topmost/Normal/Bottom/On Desktop, positionnement `@N` par écran) — https://docs.rainmeter.net/manual/arranging-skins/ — **HIGH**
- Microsoft Q&A / CodeProject — WPF/Win32 layered windows, `WS_EX_LAYERED` + `WS_EX_TRANSPARENT` pour transparence + clic-traversant — https://learn.microsoft.com/en-us/answers/questions/1096479/ — **HIGH**
- DisplayFusion Discussions — snap aux bords entre écrans, comportement multi-moniteurs — https://www.displayfusion.com/Discussions/ — **MEDIUM**
- PROJECT.md (Chronos) — périmètre validé, Out of Scope (notifications, Cowork séparé, dépendances natives), tokens de design — **HIGH**
- Connaissance de domaine : conventions HUD/OSD desktop (MSI Afterburner, gadgets/horloges de bureau type Fliqlo, iStat/menubar widgets) — **MEDIUM**

---
*Feature research for: overlay de bureau always-on-top de monitoring de quotas (Chronos)*
*Researched: 2026-07-08*
