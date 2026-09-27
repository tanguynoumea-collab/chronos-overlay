# Quick 260927 — Réglages v2 : refonte de la fenêtre de réglages (release 3.4.0)

**Type :** refonte d'interface, plan de design VALIDÉ (`.zeus/DESIGN_PLAN_REGLAGES.md`, statut « VALIDÉ ») · **Contrat visuel :**
`.zeus/maquettes/reglages-v2.html` (R1 à R4) — le plan et les maquettes font foi, mot pour mot pour les libellés.
**Demandé par l'utilisateur le 2026-09-27 :** « le panneau d'options est trop long pour rentrer dans une fenêtre ; redesigne-le
entièrement avec une fenêtre rectangulaire classique et un moyen de la redimensionner de façon dynamique ».

## Ce qui doit être vrai à la fin

1. **Fenêtre classique** (`SettingsWindow` refondue, ou nouvelle `ReglagesWindow` qui la remplace — au choix, en supprimant
   l'ancienne) : même chrome que `HistoriqueWindow` (`WindowStyle=None`, `AllowsTransparency=False`, `WindowChrome` avec bords de
   redimensionnement, coins DWM best-effort via le helper existant), boutons ─ □ ✕ fonctionnels (réduire, agrandir/restaurer,
   fermer), double-clic sur la barre de titre = agrandir/restaurer, `ResizeMode=CanResizeWithGrip`, `Topmost=False`,
   `ShowInTaskbar=True`, **aucun `Owner`**, **ne se ferme plus sur `Deactivated`**, Échap ferme. Taille par défaut 860 × 580,
   minimum 640 × 440 (tokens `ReglagesLargeurDefaut/HauteurDefaut/LargeurMin/HauteurMin`).
2. **Persistance** : `ReglagesX/Y/Width/Height` et `ReglagesSection` dans `ChronosSettings` / `settings.json`, relus à l'ouverture,
   géométrie bornée à l'écran de travail (réutiliser `PlacementHistorique` ou son principe).
3. **Ouvreur singleton** `IOuvreurReglages` (même principe que `IOuvreurHistorique` de 35-02) : le clic droit sur le cadran ouvre la
   fenêtre, ou la ramène au premier plan si elle est ouverte ; recréée après fermeture ; le VM partagé reste `MainViewModel`.
   `MainWindow.xaml.cs` n'instancie plus `new SettingsWindow(_vm) { Owner = this }`.
4. **Rail de navigation** fixe de 208 px (token `ReglagesRailLargeur`) : Données, Historique, Apparence, Sessions, Comportement,
   Diagnostic (glyphes Segoe/Inter disponibles : ◉ ◷ ◐ ☰ ◈ ▤, ou Segoe Fluent Icons si présent), sélection = fond `Panel2` + trait
   `Accent` à gauche ; ↑/↓ dans le rail et Ctrl+1…6 changent de section ; focus visible ; en bas du rail, séparé, « ⏻ Quitter
   Chronos » en brosse `Danger` (nouveau token, valeur `#E8907F`, qui remplace le littéral actuel). La section courante est un état
   du VM (enum `SectionReglages`), testable sans WPF.
5. **Colonne de contenu** dans un `ScrollViewer` vertical, largeur de lecture max 640 px (token `ReglagesContenuMax`), titre de
   section 18 + phrase d'aide 11 (tokens typo `sys:Double`), cartes `Panel2` rayon 11. Contenu par section = table
   d'iso-fonctionnalité du plan §4, sans perte :
   - **Données** : Connexion Claude ; Sonde d'en-têtes (interrupteur, coût, état) ; « Barre de statut de Claude Code » (ex-« Source
     terminal », interrupteur lié à `IsStatusLineSourceEnabled` / `ToggleStatusLineSourceCommand`, aide « installe ou retire le pont
     statusLine dans les réglages de Claude Code (utile en terminal) »).
   - **Historique** : la carte F1 de 35-02 inchangée (Ouvrir, sous-texte, dernière écriture + pastille, style de la vue Semaine,
     mention du double-clic).
   - **Apparence** : **aperçu vivant du cadran** (signature : le vrai rendu du cadran avec le thème et le style sélectionnés, via
     le moteur d'aperçu existant `CadranPreviewViewModel` / galeries ; se met à jour au clic), puis THÈME (9 vignettes existantes en
     `WrapPanel`, colonnes selon la largeur, jamais coupées), STYLE DU CADRAN (5 puces : Anneaux, Braises, Fusible, Marée,
     Volets), « Mode étendu » (visible pour Anneaux seulement, comme aujourd'hui).
   - **Sessions** : interrupteur « Widget sessions Claude Code » en tête ; si activé : aperçu vivant du widget
     (`SessionsPreviewViewModel`), STYLE DU WIDGET (8 styles), « Disposition verticale » ; si désactivé : le reste grisé et
     « Active le widget pour choisir son style ».
   - **Comportement** : « Arrière-plan », « Lancer au démarrage », « Recalibrer le reset hebdomadaire… » (commande existante).
   - **Diagnostic** : le rapport dans une zone texte mono, lecture seule, sélectionnable, qui défile et s'étire avec la fenêtre,
     boutons « ↻ Actualiser » et « ⧉ Copier » (presse-papiers), ligne « Généré à HH:MM · N lignes » ; génération asynchrone
     (« Génération du rapport… »), erreur « Le diagnostic a échoué : <cause> » + « Réessayer ». Plus de `MessageBox`.
6. **Tokens et gardes** : aucune couleur ni taille en dur dans la nouvelle fenêtre (les 12 littéraux et 30 tailles de police de
   l'ancienne disparaissent ; gabarits interrupteur/carte de thème/puces conservés mais sur tokens) ; si la table `TaillesHisto`
   (test 34-01) ou la garde « aucune valeur en dur » doivent s'étendre à la nouvelle fenêtre, le faire dans le même commit que les
   tokens ; la garde de vocabulaire (aucune projection) couvre les textes nouveaux.
7. **Tests** (TDD, RED nommé d'abord) : VM de navigation (section par défaut, persistance, Ctrl+n), ouvreur singleton (ouvre,
   ramène, recrée), fenêtre construite en STA sans `Application` (chrome : `AllowsTransparency=False`, `Topmost=False`, pas
   d'Owner, `ResizeMode`, min/défaut depuis les tokens, ne se ferme pas sur désactivation, Échap ferme), hauteur/largeur minimales
   par Measure/Arrange sans chevauchement ni vignette coupée à 640 × 440, **inventaire d'iso-fonctionnalité** (chaque commande de
   l'ancienne fenêtre est liée dans la nouvelle : Login, ToggleSondeEnTetes, OuvrirHistorique, ChoisirStyle, SelectTheme,
   SelectCadranStyle, ToggleCadranMode, ToggleBackground, ToggleAutostart, ToggleSessionsWidget, SelectSessionStyle,
   ToggleVerticalLayout, Recalibrate, ToggleStatusLineSource, diagnostic, Quit), diagnostic sans MessageBox (grep), adaptation
   des tests existants de `SettingsWindow` (`ReglagesBindingTests` etc.) sans perdre leurs assertions de fond.
8. **Release 3.4.0** (procédure 35-06 / quick arrêt-bloqué) : bump × 4, publish mono-fichier, contrôles, smoke `--hook`,
   `Chronos-v3.4.0.exe` à la racine, commit de release du csproj seul, ni tag ni push ; README (ligne « clic droit ») et
   `docs/publish.md` mis à jour ; mémoire du compte de tests du README si une garde le fixe.

## Interdits

Ne jamais lancer un overlay ni `dotnet run` (seul le smoke `--hook` stdin vide) ; ne jamais tuer ni arrêter un processus Chronos (la
3.3.1 de l'utilisateur tourne) ; aucune écriture sous `~/.claude`, `%APPDATA%\Claude`, `%APPDATA%\Chronos` ; aucun paquet NuGet
nouveau ; zéro warning Debug et Release ; suite complète deux fois avant la release ; commits en français, stage explicite, pas
d'amend, `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` ; ne pas modifier ROADMAP.md ni STATE.md.
