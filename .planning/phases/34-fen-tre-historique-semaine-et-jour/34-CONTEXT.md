# Phase 34: Fenêtre Historique : Semaine et Jour - Context

**Gathered:** 2026-09-27
**Status:** Ready for planning
**Mode:** Auto-généré (workflow.skip_discuss=true), enrichi du plan de design VALIDÉ par l'utilisateur (`.zeus/DESIGN_PLAN.md`,
checkpoint ZEUS du 2026-09-27, maquettes Figma A/B/C/D/F2) et des livrables réels des phases 32 et 33. Le plan de design est
CONTRACTUEL : mêmes mots, mêmes pistes, mêmes hauteurs, tokens via ressources, aucune valeur en dur.

<domain>
## Phase Boundary

Une fenêtre « Historique » séparée (opaque, non topmost, redimensionnable, mémorisée) montre la semaine de forfait dans les trois
styles (Pistes = A, Simplifié = B, Tuiles = C, sélectionnables par réglage) et le jour au grain de 5 min (D), en consommant les
deux journaux sur deux axes distincts ; honnêteté testée mot pour mot ; rendu `OnRender` par piste gouverné par
`DesignTokens.xaml` ; bandeau F2 de reconstruction. Exigences : HIS-01, HIS-02, HIS-03, HIS-04, HIS-06, HIS-07, HIS-08.
HORS périmètre : la vue 4 semaines (HIS-05), les gestes d'ouverture (ACC-01/02 : carte des réglages, double-clic centre), la
section du diagnostic, la release 3.3.0 et le constat (phase 35). La fenêtre n'a donc pas encore de geste d'ouverture dans
l'app : une **galerie `--historique`** sur fixtures (même mécanisme CLI que `--sessions`, sans réconciliation des hooks) sert à la
revue visuelle DAEDALUS (DESIGN_PLAN §8) — l'agent ne la lance pas, l'utilisateur oui.

</domain>

<decisions>
## Implementation Decisions

### Décisions de l'utilisateur (verrouillées, 2026-09-27)
- Forme A (Pistes) par défaut ; **B (Simplifié) et C (Tuiles) codées aussi et sélectionnables** (`HistoriqueStyleSemaine` dans
  `settings.json`, sélecteur dans la fenêtre à droite de la ligne de fraîcheur ; la carte des réglages arrive en phase 35).
- Vues Jour (cette phase) et 4 semaines (phase 35) conservées.
- Deux séries de nature différente, jamais fusionnées : % du compte (relevés exacts) et tokens Claude Code ; jamais d'axe ni de
  palette communs ; aucun trou interpolé ; aucune projection.

### Contrat du plan de design (`.zeus/DESIGN_PLAN.md`)
- **§2 fenêtre** : `Views/HistoriqueWindow.xaml` + `ViewModels/HistoriqueViewModel` ; `WindowStyle=None`, `AllowsTransparency=False`,
  `Topmost=False`, `ShowInTaskbar=True`, coins 16, ombre, redimensionnable (min 760 × 480, défaut 920 × 610), position/taille
  mémorisées (`HistoriqueX/Y/Width/Height`) ; palette `Panel #151322 / Panel2 #1E1B30 / Line #2C2942 / Ink #F2F0FB / Ink2 #A9A6C4 /
  Accent #8B7BF0 / Ok #4EC98A` **promue de `SettingsWindow` vers `Resources/DesignTokens.xaml` sans changer une valeur** ; rampe du
  thème actif via `UtilizationToBrushConverter` ; nouveaux tokens `HistoTokens #8C89A8`, `HistoSousAgent #5A5776`,
  `HistoModele1/2/3 #9D9AB8 / #6E6B8C / #4A4762`, `HistoGris` ; Segoe UI ; corps 16 / 14 / 11,5 / 11 / 10,5 / 9,5 / 9 / 8,5 ; ✕ et Échap.
- **§2.1 en-tête commun** : segment Jour / Semaine / 4 semaines (le bouton « 4 semaines » existe mais renvoie « bientôt » ou est
  désactivé jusqu'à la phase 35 — à trancher au plan, sans tromper), ‹ ›, « Cette semaine » / « Aujourd'hui », ligne de fraîcheur
  « Dernier relevé il y a N min · source · N relevés · N interruptions · journal ouvert le … », pastille `Alerte` + « journal muet
  depuis N min » au-delà de 15 min (source : `IEtatJournal` / `JournalReleves.SeuilMuet`).
- **§2.2 Semaine** : trois styles avec EXACTEMENT les pistes et hauteurs du tableau (Pistes : NIVEAU 150 / RYTHME 72 / TOKENS 72 /
  COUVERTURE 12 ; Simplifié : NIVEAU 200 / TOKENS 90 / COUVERTURE 12 ; Tuiles : NIVEAU 120 / FENÊTRES 5 H 62 / RYTHME 58 / TOKENS 58 /
  COUVERTURE 12) ; X = samedi 00:00 → samedi 00:00 local (`BornesPlage`), grille et libellés des jours, « reset hebdo → » ; réticule
  vertical commun + infobulle (heure, valeur, source, âge) ; éléments d'honnêteté : trou (> 2 cadences) = rectangle `Line` 35 % +
  bordure pointillée (grise « Chronos arrêté », ambre « jeton invalide ») ; saut non localisé = bloc plat gris « +N % pendant
  l'absence (répartition inconnue) » ; marche de % sans tokens Code = cadre pointillé `Accent` + pied « consommé ailleurs (Cowork,
  claude.ai) » ; « journal ouvert le <date> » et zone antérieure vide ; libellé permanent des tokens et pied de page fixe.
- **§2.3 Jour** : 0 h → 24 h locale, grille 3 h, grain 5 min ; NIVEAU 190 (% 5 h au premier plan 2,4 px, gris à 100 % « épuisée à
  100 % — le serveur refuse (statut rejected) », % hebdo trait fin `Ink2`, traits `TickReset` « reset 5 h HH:MM ») ; RYTHME 64 ;
  TOKENS 64 par quart d'heure empilés par modèle (`HistoModele1/2/3`, légende « opus · sonnet · haiku · sous-agents inclus ») ;
  COUVERTURE 12 ; ligne « maintenant » `Ink` 60 % ; ligne de fraîcheur « 288 relevés attendus · N présents · N interruption(s)
  (cause, HH:MM → HH:MM) » ; « Aujourd'hui ».
- **§3 données** : la fenêtre ne reçoit rien du cadran ; elle lit par les services : `LecteurJournal.Lire` + `AnalyseReleves` +
  `BornesPlage` (32-06), `LecteurAgregats.Lire` + `RenduLocalTokens` (33-04), `IEtatJournal` (32-04/05), `IEtatReconstruction`
  (33-03/05 : `Changement` levé sur le thread de fond → `Post` + coalescence côté VM), `IEtatMagasin` des trois magasins.
- **§4 vocabulaire** : Historique · Semaine de forfait · Jour · 4 semaines · Niveau · Rythme · Tokens Claude Code · Couverture ·
  Fenêtres 5 h · relevé · reset 5 h / reset hebdo · trou · « Chronos arrêté » · « jeton invalide » · « sonde refusée » · « épuisée » ·
  « répartition inconnue » · « consommé ailleurs » · « journal ouvert le … » · « dernière écriture » · style : Pistes / Simplifié /
  Tuiles. Une garde sur le vocabulaire interdit toute projection (« épuisé vers », « à ce rythme »).
- **Rendu (HIS-07)** : un `FrameworkElement` par piste, `OnRender` + `StreamGeometry` gelée, réduction min/max par colonne de pixels
  au-delà de 4 000 points, redessin sur changement de données ou de plage — JAMAIS sur le tick 1 s (test) ; géométrie (temps → x,
  valeur → y, binning, trous, tuiles, segments par bande de rampe) en classes PURES de `Rendering/` testées comme `ArcGeometry`.
- **Bandeau F2 (HIS-08)** : « Reconstruction des tokens depuis vos transcripts Claude Code — N / M fichiers · la semaine courante
  est déjà complète », barre `Accent`, sous-texte « en arrière-plan, priorité basse · du plus récent au plus ancien · les
  pourcentages du forfait ne se reconstruisent pas : ils commencent au <date> » ; disparaît à la fin.

### Claude's Discretion
Découpage des contrôles (une piste = un `FrameworkElement` ; conteneur de vue), structure du ViewModel (un VM de fenêtre + un VM
par vue ou un seul), format du réticule/infobulle, mécanique de la galerie `--historique` (fixtures = celles de 32-06/33-04 +
scénarios du DESIGN_PLAN §8 : trous, saut, épuisée, divergence, semaine précédente), stratégie de test du « jamais au tick 1 s »
(compteur de rendus), tout dans les conventions du dépôt.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- Fenêtres existantes : `Views/SettingsWindow.xaml` (chrome, palette locale à promouvoir, styles `Flat`, bascules), `SessionsWindow`,
  `CadranGalleryWindow` / `SessionsGalleryWindow` (galeries sur fixtures — modèle de la galerie `--historique`), `MainWindow`
  (170 × 170 layered, `CentreHit`, clic droit → réglages ; à ne pas toucher dans cette phase).
- `Resources/DesignTokens.xaml` (tokens verrouillés : `Piste5h`, `PisteHebdo`, `TickReset #F4F2EC`, `TexteSecondaire`, `Alerte`,
  `UtilBrush`), `Theming/ChronosTheme.cs` (rampe par thème, `BrushTokens()`), `Rendering/ArcGeometry.cs` & `DayTimeline.cs`
  (style des classes pures), `Controls/RingArc.cs` & `TickRing.cs` (contrôles à `OnRender`).
- Services (phases 32/33) : `Services/Historique/{LecteurJournal, BornesPlage, AnalyseReleves, JournalReleves, IEtatJournal}`,
  `Models/Historique/LectureJournal.cs`, `Services/Historique/Tokens/{LecteurAgregats, RenduLocalTokens, MagasinAgregats,
  CouvertureTokens, ReconstructionTokens, IEtatReconstruction, ProjectionAgregats}`, `Models/Historique/Tokens/*`,
  `Services/IEtatMagasin.cs`, `ChronosSettings`/`SettingsService` (persistance de `settings.json`), `IClock`, `IUiDispatcher`.
- App.xaml.cs : DI (`AddSingleton<MainViewModel>()` sans fabrique ; paramètres optionnels injectés), hosted services ; mode CLI
  `--sessions` (galerie) et `--hook` — modèle pour `--historique`.
- Tests : 1415 verts ; `ServicesLayerPurityTests` (récursif), gardes textuelles de périmètre, `GardeTokensSansPourcentageTests`,
  gardes documentaires §7/§8, fixtures de journal (32-06, 8 fichiers) et d'agrégats (33-04, DST 100/92 lignes).

### Established Patterns
- MVVM strict (CommunityToolkit : `[ObservableProperty]`, `[RelayCommand]`), aucune logique dans le code-behind au-delà du drag/
  fermeture, marshaling UI via `IUiDispatcher`, tick 1 s du cadran séparé des rafraîchissements de données (5 min).
- TDD par plan, mutations révoquées par copie, suite complète deux fois, zéro warning, exécuteurs parallèles sur fichiers disjoints,
  ROADMAP/STATE réservés à l'orchestrateur.
- Toute chaîne visible en français, vocabulaire §4 ; les couleurs ne vivent que dans `DesignTokens.xaml` / le thème.

### Integration Points
- `DesignTokens.xaml` (promotion de la palette + nouveaux tokens) et `SettingsWindow.xaml` (remplacer ses ressources locales par
  les tokens promus, valeurs identiques, sans autre changement visuel).
- `ChronosSettings` : `HistoriqueStyleSemaine`, `HistoriqueX/Y/Width/Height`.
- App.xaml.cs : enregistrement de `HistoriqueViewModel`/`HistoriqueWindow` (fabrique ou singleton), mode `--historique`.
- La phase 35 branchera les gestes (carte des réglages, double-clic centre), la vue 4 semaines et le diagnostic.

</code_context>

<specifics>
## Specific Ideas

- Maquettes Figma : https://www.figma.com/design/O8WVDejfdPcJv6314a7h6k — frames A (Pistes), B (Simplifié), C (Tuiles), D (Jour),
  F2 (bandeau) ; aperçus PNG envoyés à l'utilisateur le 2026-09-27. Données synthétiques : les fixtures de la galerie doivent
  reproduire les mêmes cas (trou « Chronos arrêté » mar. 23 h → mer. 7 h, trou « jeton invalide » jeu. 14 h → 16 h, fenêtre épuisée,
  divergence mer. soir, semaine précédente).
- Critères de revue visuelle (DESIGN_PLAN §8) : trois styles capturés avec les mêmes fixtures ; Jour avec reset, plateau épuisé et
  infobulle ; aucune valeur hors tokens ; aucun texte tronqué à 100 % et 150 % d'échelle ; redimensionnement 760 → 1 400 px ; libellés
  d'honnêteté présents mot pour mot. La revue DAEDALUS (ZEUS phase 6) se fait sur captures de la galerie `--historique` lancée par
  l'utilisateur, après le GATE TESTS.

</specifics>

<deferred>
## Deferred Ideas

- Vue 4 semaines (HIS-05), carte des réglages et double-clic (ACC-01/02), diagnostic (ACC-03), release 3.3.0 et docs (ACC-04),
  constat (VAL-05) → phase 35.
- Heatmap, export CSV, projection conditionnelle, dimension projet → v1.9.

</deferred>
