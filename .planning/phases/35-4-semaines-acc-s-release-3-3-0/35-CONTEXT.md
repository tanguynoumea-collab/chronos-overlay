# Phase 35: 4 semaines, accès, release 3.3.0 - Context

**Gathered:** 2026-09-27
**Status:** Ready for planning (à relire après le SUMMARY 34-08 et la revue visuelle DAEDALUS : les écarts bloquants de la revue
entrent dans cette phase comme tâches de la vague 1, avant la release)
**Mode:** Auto-généré (workflow.skip_discuss=true), enrichi du plan de design VALIDÉ (`.zeus/DESIGN_PLAN.md` §2.4, §5, §6) et des
livrables réels des phases 32 à 34.

<domain>
## Phase Boundary

La vue « 4 semaines » (HIS-05) ; les deux gestes d'ouverture — carte F1 « Historique d'utilisation » dans la section DONNÉES des
réglages et double-clic au centre du cadran sans casser le simple clic (ACC-01, ACC-02) ; la section « Journal d'historique » du
diagnostic (ACC-03) ; README + `docs/data-sources.md` avec le vocabulaire §4, release **3.3.0** publiée et réconciliée (ACC-04) ;
constat de la 3.3.0 avec l'utilisateur, verdict écrit dans `35-CONSTAT.md` (VAL-05, point de contrôle humain, dernier plan,
`autonomous: false`). Dernière phase du milestone v1.8.

</domain>

<decisions>
## Implementation Decisions

### Verrouillées (utilisateur, conseil, plan de design)
- **§2.4 vue 4 semaines** : X = samedi → samedi (sept colonnes « sam. … ven. », « reset hebdo → ») ; quatre courbes hebdo superposées
  sur le même axe : la courante en couleur (rampe du thème actif, `ChronosTheme.ArcColor`), S-1 / S-2 / S-3 en `HistoGris` aux opacités
  0,8 / 0,45 / 0,25 ; étiquettes à droite (libellé de semaine + valeur finale, ou « pas de relevés (avant le journal) ») ; une semaine
  épuisée = plateau gris à 100 % annoté « épuisée <jour> HH:MM → bloquée jusqu'au reset » ; bande COUVERTURE PAR SEMAINE à quatre
  rangées (S, S-1, S-2, S-3 : relevé présent / Chronos arrêté / jeton invalide / avant le journal) ; marqueur « journal ouvert <date> »
  sur la semaine d'ouverture ; **pas de piste tokens** ; pied « Rien n'est inventé avant l'ouverture du journal. » ; le segment
  « 4 semaines » de l'en-tête (désactivé « bientôt (phase 35) » en 34-05) devient actif ; navigation ‹ › par bloc de 4 semaines.
- **§5 / §6 gestes et carte F1** : carte « Historique d'utilisation » dans DONNÉES après « Sonde d'en-têtes » et la carte « Journal des
  relevés » de 32-05 (à fusionner ou enchaîner proprement : une seule ligne d'état « dernière écriture », pas deux) — bouton « Ouvrir »,
  sous-texte « hebdo / 5 h / tokens · journal du <date> · dernière écriture il y a N min », sélecteur « Style de la vue Semaine :
  Pistes · Simplifié · Tuiles » **synchronisé** avec celui de la fenêtre (même `IReglagesHistorique`), mention « Aussi : double-clic au
  centre du cadran », bordure `Accent` 1,5 px (fonctionnalité nouvelle), carte d'état « Dernière écriture du journal : il y a N min » +
  pastille `Alerte` « alerte si > 15 min alors que Chronos tourne ». **Double-clic au centre du cadran** (`CentreHit`) : ouvre ou ramène
  au premier plan `HistoriqueWindow` SANS déclencher deux fois la bascule % / temps du simple clic — temporisation à
  `SystemInformation.DoubleClickTime` ou annulation de la première bascule, testée avec horloge injectée ; simple clic inchangé ;
  drag et clic droit (réglages) inchangés ; la fenêtre est un singleton ré-affiché (pas une seconde instance).
- **ACC-03 diagnostic** : section « Journal d'historique » — chemins des fichiers (`releves-*.jsonl`, `tokens-*.jsonl`, `ids-*.jsonl`,
  `curseurs.json`, `couverture.json`), âge de la dernière écriture, relevés du jour (N / 288), événements récents (5 derniers),
  état de la reconstruction (N / M ou « terminée », dernier fichier), tailles des fichiers, nombre d'instances Chronos — MÊME lecture
  que la fenêtre (services de 32/33, pas un second chemin) ; complète la section `[Magasins persistants]` (32-02) et
  `[Agrégats de tokens]` (33-05) sans les dupliquer.
- **ACC-04 docs et release** : README (section « Historique d'utilisation » : fenêtre, trois styles, trois vues, règles d'honnêteté,
  gestes, mots du §4) ; `docs/data-sources.md` (§7/§8 déjà sous gardes ; ajouter la lecture par la fenêtre et les écarts connus des
  maquettes) ; garde de vocabulaire de 34-08 étendue au README ; procédure de release de 32-07 / 31-02 : csproj 3.3.0 × 4,
  `dotnet publish` mono-fichier win-x64, contrôles (taille, 0 DLL, VersionInfo 3.3.0.0 / 3.3.0, md5), smoke `--hook` avec
  `settings.json` inchangé, `Chronos-v3.3.0.exe` à la racine, commit de release — **l'agent ne lance jamais l'overlay**.
- **VAL-05 constat** (dernier plan, `autonomous: false`, points de contrôle humains, relevés par sonde WMI hors de l'arbre de
  l'app) : quitter la 3.2.2 (et toute ancienne instance encore en marche : 3.1.0 / 3.2.0 / 3.2.1 si l'écart E1-ter de `32-CONSTAT.md`
  n'est pas soldé), lancer `Chronos-v3.3.0.exe` par l'Explorateur, second lancement refusé (CPT-03 en vrai — reprend le point (a) de
  32-08), réconciliation dans `~/.claude/settings.json` ; la fenêtre s'ouvre par les DEUX gestes ; la semaine courante affiche les
  relevés depuis l'ouverture du journal (3.2.2, 27/09 08:07) et les tokens reconstruits ; les trois styles se sélectionnent ; un trou
  réel (PC éteint la nuit) est hachuré et annoté avec sa cause ; la vue 4 semaines dit « avant le journal » pour S-1 et plus ;
  l'utilisateur relit les libellés d'honnêteté et rend un verdict écrit, écarts compris. Si le tableau des gestes de 32-08 (L1 … Q,
  V01 … V12) n'a toujours pas été joué, ce constat l'inclut (une seule séance).
- Doctrine inchangée : deux séries jamais fusionnées, aucun trou interpolé, aucune projection, XAML pur, tokens seulement, lecture
  seule stricte de `~/.claude`.

### Entrées attendues de la revue visuelle DAEDALUS (ZEUS phase 6, après le GATE TESTS de 34-08)
- La revue se fait sur la galerie `--historique` lancée PAR L'UTILISATEUR (captures fournies ou prises via capture d'écran en
  lecture seule). Écarts **bloquants** (texte tronqué, chevauchement, valeur hors tokens, libellé d'honnêteté absent, hauteur §2.2
  fausse, contraste) → tâches de la vague 1 de cette phase, avant la release ; écarts **mineurs** → RETOUR ROADMAP (v1.9).
- Écarts connus et acceptés : rayon DWM ≈ 8 px (pas 16), coins droits sur Windows 10, badge de version omis, infobulle non bornée
  au bord droit (à corriger si simple), repères « 100 % / 0 » selon 34-08.

### Décisions de l'orchestrateur sur les questions ouvertes de 35-RESEARCH (2026-09-27)
1. **Semaine épuisée** : prouvée par une fixture DÉRIVÉE du scénario (S-1 poussée à 100 % un jeudi soir), sans modifier
   `ScenariosHistorique.Generer` ; la galerie peut proposer ce second scénario si c'est trivial.
2. **Valeurs de la frame E** (maquette construite par l'orchestrateur, fichier Figma O8WVDejfdPcJv6314a7h6k) : zone de tracé
   700 px dans une fenêtre de 920, NIVEAU 4 semaines = **250 px**, colonne d'étiquettes à droite ≈ **140 px**, bande COUVERTURE
   PAR SEMAINE = quatre rangées de **10 px** espacées de **16 px**. Ces valeurs deviennent les nouveaux tokens (table de 34-01 mise à
   jour dans le même commit).
3. **Veille de minuit** (trou qui chevauche minuit) : vue Jour seulement en v1.8 ; la Semaine n'est pas concernée (ses bornes sont
   des samedis 00:00 déjà couverts par la lecture de la semaine).
4. **Revue DAEDALUS** : en attente de la galerie lancée par l'utilisateur. Le plan de la vue 4 semaines réserve une tâche « écarts
   bloquants de la revue » ; si la revue n'est pas rendue quand ce plan s'exécute, la tâche consigne « revue non rendue » et les
   écarts entreront par une itération DESIGN-REVIEW → GSD (max 3, pipeline ZEUS).
5. **Fuseau du diagnostic** : injecté par la DI (`TimeZoneInfo` est enregistré depuis 34-05), jamais `TimeZoneInfo.Local` dans le
   code neutre ; repli explicite seulement dans la racine de composition.
6. **Découpage** : release et constat SÉPARÉS (la release reste autonome, le constat seul est `autonomous: false`) → 7 plans en
   4 vagues : vague 1 (données/VM 4 semaines + minuit + robustesse de réouverture ∥ gestes + carte F1 + ouvreur ∥ diagnostic),
   vague 2 (vue 4 semaines XAML + tokens + segment + infobulle bornée + annotations Jour ∥ README + data-sources §9 + garde),
   vague 3 (release 3.3.0), vague 4 (constat VAL-05, qui rejoue aussi E1-ter et le tableau L1…Q / V01…V12 de 32-08).
7. **Coût du double-clic** : le simple clic bascule après le délai système (≈ 0,5 s) — accepté, annoncé au constat.
8. **Protocole du constat** : au moins une nuit avec l'overlay 3.3.0 avant la lecture d'un trou réel ; une veille donne « cause
   inconnue », c'est honnête ; le critère « hachuré » de VAL-05 se lit « rectangle gris à bordure pointillée annoté de sa cause »
   (la hachure est réservée à « avant le journal »).

### Claude's Discretion
Structure de `VueQuatreSemainesView` (réutilise `PisteNiveau`/`PisteCouverture` de 34-04 avec quatre séries ou une piste dédiée),
mécanique exacte du double-clic (temporisation vs annulation), forme du singleton de fenêtre (service `IOuvreurHistorique`), ordre
des cartes dans DONNÉES, découpage du README.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- Phase 34 : `HistoriqueWindow` (en-tête, segment, F2, sélecteur de style, géométrie persistée), `VueSemaineView`, `VueJourView`,
  pistes `Controls/Historique/*` (table des DP dans 34-04-SUMMARY), `Rendering/Historique/*`, `HistoriqueViewModel`
  (`AnnotationsDivergences`, styles, `IReglagesHistorique`, `ScenariosHistorique`, F2), `TextesHistorique`, tokens `Histo*`
  (38 `sys:Double` EXACTEMENT : toute taille nouvelle met à jour la table du test de 34-01), galerie `--historique`.
- Phase 32/33 : `IEtatJournal`, `JournalReleves.SeuilMuet`, `LecteurJournal.Lire`, `AnalyseReleves`, `BornesPlage` (4 semaines),
  `IEtatMagasin` × 3, `IEtatReconstruction`, `InventaireProcessus`, `VerrouInstanceUnique`, `DiagnosticService` (sections
  `[Magasins persistants]`, `[Agrégats de tokens]`, « journal muet »), carte « Journal des relevés » de `SettingsWindow` (32-05).
- Cadran : `MainWindow.xaml(.cs)` (`CentreHit`, clic centre = bascule % / temps, clic droit = réglages, drag), `MainViewModel`.
- Release : `docs/publish.md`, SUMMARY 32-07 (procédure 3.2.2), `Chronos.csproj` (4 propriétés de version), `.gitignore` `/Chronos-v*.exe`.
- Constat : `31-CONSTAT.md` (protocole), `32-CONSTAT.md` (temps 0, point (a) partiel, E1-ter, journal vérifié), sonde WMI.

### Established Patterns
- MVVM strict, DI, `IClock`/`IUiDispatcher` injectés, tests `[WpfFact]` collection « XAML WPF », Measure/Arrange pour les hauteurs,
  gardes textuelles (tokens, vocabulaire, pureté), TDD, mutations révoquées par copie, suite complète deux fois, zéro warning,
  exécuteurs parallèles sur fichiers disjoints, ROADMAP/STATE réservés à l'orchestrateur, jamais `--amend`.

### Integration Points
- `SettingsWindow.xaml` (+ VM) : carte F1 + sélecteur synchronisé ; `MainWindow.xaml.cs` : double-clic ; App.xaml.cs : singleton de
  fenêtre / ouvreur ; `DiagnosticService` ; README, `docs/data-sources.md` ; csproj.

</code_context>

<specifics>
## Specific Ideas

- Maquettes : frame E (4 semaines) et F1 (carte des réglages) — https://www.figma.com/design/O8WVDejfdPcJv6314a7h6k.
- Le constat VAL-05 clôt aussi le milestone : après lui, ZEUS enchaîne DEV-COUNCIL (audit multi-rôles), triage humain, DEV-SENIOR,
  PUBLICATION (release GitHub, tag, CHANGELOG) et RETOUR ROADMAP.

</specifics>

<deferred>
## Deferred Ideas

- Heatmap jour × heure, export CSV, compaction, dimension projet, projection conditionnelle, `DayTimeline` sur resets observés,
  dérive `WeeklyWindow` (test d'acceptation 25/10/2026), trou chevauchant minuit en vue Jour si non traité en 34-08 → v1.9.

</deferred>
