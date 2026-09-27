# Phase 35 : 4 semaines, accès, release 3.3.0 — Research

**Researched:** 2026-09-27
**Domain:** WPF .NET 8 (MVVM CommunityToolkit, DI) — vue OnRender supplémentaire, gestes souris (double-clic), singleton de fenêtre, diagnostic texte, docs gardées, release mono-fichier, constat humain
**Confidence:** HIGH (tout ce qui suit est lu dans le code réel à `5bb415f`, 1565 tests verts ; aucune bibliothèque nouvelle)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
#### Verrouillées (utilisateur, conseil, plan de design)
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

#### Entrées attendues de la revue visuelle DAEDALUS (ZEUS phase 6, après le GATE TESTS de 34-08)
- La revue se fait sur la galerie `--historique` lancée PAR L'UTILISATEUR (captures fournies ou prises via capture d'écran en
  lecture seule). Écarts **bloquants** (texte tronqué, chevauchement, valeur hors tokens, libellé d'honnêteté absent, hauteur §2.2
  fausse, contraste) → tâches de la vague 1 de cette phase, avant la release ; écarts **mineurs** → RETOUR ROADMAP (v1.9).
- Écarts connus et acceptés : rayon DWM ≈ 8 px (pas 16), coins droits sur Windows 10, badge de version omis, infobulle non bornée
  au bord droit (à corriger si simple), repères « 100 % / 0 » selon 34-08.

### Claude's Discretion
Structure de `VueQuatreSemainesView` (réutilise `PisteNiveau`/`PisteCouverture` de 34-04 avec quatre séries ou une piste dédiée),
mécanique exacte du double-clic (temporisation vs annulation), forme du singleton de fenêtre (service `IOuvreurHistorique`), ordre
des cartes dans DONNÉES, découpage du README.

### Deferred Ideas (OUT OF SCOPE)
- Heatmap jour × heure, export CSV, compaction, dimension projet, projection conditionnelle, `DayTimeline` sur resets observés,
  dérive `WeeklyWindow` (test d'acceptation 25/10/2026), trou chevauchant minuit en vue Jour si non traité en 34-08 → v1.9.

> Note du chercheur sur le dernier point : 34-08 ne l'a PAS traité et l'a explicitement « consigné pour la phase 35 »
> (34-08-SUMMARY, Deferred 1) ; la consigne de l'orchestrateur (point 6) demande de proposer où le traiter. La recommandation
> ci-dessous l'intègre à 35-01 parce qu'il touche exactement les fichiers que 35-01 modifie déjà (les deux façades) ; si
> l'utilisateur préfère s'en tenir au CONTEXT, il reste un bloc détachable (tâche isolée, tests isolés) à renvoyer en v1.9.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| HIS-05 | Vue « 4 semaines » : 4 courbes hebdo superposées samedi → samedi, courante en couleur, S-1..S-3 gris 0,8/0,45/0,25, étiquettes, plateau épuisé annoté, couverture par semaine (4 rangées), semaines antérieures au journal vides et dites | `BornesPlage.QuatreSemaines` existe déjà (32-06, testé) ; `VueHistorique.QuatreSemaines` existe ; recette de projection « par fraction de SA plage » (D-34-20) ; piste dédiée `PisteQuatreSemaines : PisteBase` recommandée ; `PisteCouverture` réutilisable telle quelle par rangée ; 5 tokens `sys:Double` nouveaux (table de test à mettre à jour) — §Architecture Patterns 1-3 |
| ACC-01 | Carte F1 « Historique d'utilisation » dans DONNÉES (Ouvrir, sous-texte, sélecteur synchronisé, mention double-clic, état « dernière écriture » + alerte > 15 min) | Carte 32-05 à fusionner (`LigneEtatJournal`/`PastilleJournal` et `TexteEtatJournal`/`AlerteJournal` déjà tenus par tests) ; synchronisation « par construction » en exposant le `HistoriqueViewModel` singleton au `MainViewModel` ; `JournalOuvertLe` à ajouter à `IEtatJournal` — Pattern 5 |
| ACC-02 | Double-clic `CentreHit` ouvre / ramène `HistoriqueWindow`, sans double bascule ; drag et clic droit inchangés | `CentreHit_MouseLeftButtonDown` → `ToggleCenterMode()` aujourd'hui ; `e.ClickCount` WPF ; arbitre pur + minuterie `DispatcherTimer` à `GetDoubleClickTime` (P/Invoke user32, pas de WinForms dans le projet) ; `IOuvreurHistorique` (motif `IRecalibrationPrompt` / `ISessionsController`) — Pattern 4 |
| ACC-03 | Section « Journal d'historique » du diagnostic | `DiagnosticService` (CRLF, 1054 l.) ; garde `GardesPerimetreTests` interdit `.Lire(` dans ce fichier → lecture par la façade de la fenêtre (`SourceHistoriqueDisque.LireJour`) + helper neutre nouveau pour les événements et l'inventaire — Pattern 6 |
| ACC-04 | Release 3.3.0 + README + `docs/data-sources.md` avec les mots §4, garde de vocabulaire étendue au README | Procédure 32-07 relevée ligne à ligne ; gardes documentaires existantes (`ContratJournalDocumenteTests`, `ContratAgregatsDocumenteTests` — la dernière ligne du document DOIT contenir « §8 ») ; la garde de vocabulaire doit être **limitée à la section** du README (le reste du README parle légitimement d'« estimation » du cadran) — Pattern 7, Pitfalls 9-10 |
| VAL-05 | Constat de la 3.3.0 avec l'utilisateur → `35-CONSTAT.md` | Protocole 32-CONSTAT réutilisable ; **E1-ter NON soldé au 27/09 (relevé ici : 4 overlays en marche, dont 3.1.0 / 3.2.0 / 3.2.1)** ; point (b)/(c) de 32-08 jamais joués ; sonde WMI documentée dans 32-08-PLAN l. 85-105 — §Constat |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- C# / .NET 8 (`net8.0-windows`) / WPF / MVVM CommunityToolkit.Mvvm 8.4.2 / Microsoft.Extensions.DependencyInjection + Hosting 8.0.x — imposé ; **aucune dépendance NuGet nouvelle**.
- Rendu en XAML pur / `OnRender` + `StreamGeometry` gelée ; aucune dépendance native (pas de SkiaSharp).
- MVVM strict, `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers Models / Views / ViewModels / Services ; types NEUTRES (aucun `System.Windows`) sous `Services/`, `Models/`, `Rendering/` (`ServicesLayerPurityTests`).
- Chemins sous profil utilisateur uniquement, aucun droit admin ; lecture seule stricte de `~/.claude` et `%APPDATA%\Claude`.
- Ne jamais présenter une estimation comme exacte ; `utilization`/`resets_at` prioritaires.
- UI et commentaires en **français**.
- Déploiement : exe self-contained mono-fichier win-x64 (`PublishTrimmed=false`, `IncludeNativeLibrariesForSelfExtract=true`), sans ClickOnce ; version dans l'exe ET dans le nom `Chronos-vX.Y.Z.exe` (mémoire « versionnage de l'exe »).
- `Assembly.Location` interdit (vide en mono-fichier) → `AppContext.BaseDirectory` / `Environment.GetFolderPath`.
- Workflow GSD : pas d'édition hors workflow ; ROADMAP/STATE réservés à l'orchestrateur ; jamais `--amend` ; stage explicite.
- ATHENA/ZEUS : projet .NET desktop → pipeline **ZEUS** (DEV-COUNCIL, DEV-SENIOR, PUBLICATION suivent le constat).
- Règles d'observation : l'agent ne lance, n'arrête ni ne clique **jamais** l'overlay ; relevés de fichiers par sonde WMI hors de l'arbre de l'app ; jamais l'endpoint de refresh OAuth avec le jeton réel.

## Summary

La phase 35 ne demande **aucune technologie nouvelle** : tout se construit avec les briques livrées en 32-34, et le code réel les expose déjà presque toutes. `BornesPlage.QuatreSemaines(instant, repere, ancre, tz)` rend les quatre plages contiguës S-3…S (testé en 32-06), `VueHistorique.QuatreSemaines` existe, `PisteBase` fournit trace de rendu, plumes gelées et `GeometrieEscalier`, et la recette « projeter une semaine par fraction de SA propre plage sur l'axe de la semaine courante » est déjà utilisée pour le fantôme S-1 (D-34-20). La vue 4 semaines se fait donc proprement avec **une piste dédiée `PisteQuatreSemaines`** (une géométrie, une grille, un ordre de peinture, une trace) plus **quatre `PisteCouverture` réutilisées telles quelles** pour la bande par semaine ; réutiliser quatre `PisteNiveau` superposées obligerait à modifier la piste la plus testée de 34 (mode monochrome, suppression des trous/sauts par brosses nulles, grille dessinée quatre fois).

Les gestes demandent trois décisions de conception que le code tranche : (1) la **temporisation** du simple clic (arbitre pur + minuterie à `GetDoubleClickTime`, P/Invoke user32 — WinForms n'est pas référencé) plutôt que l'annulation (qui ferait clignoter % → temps → % et déclencherait bien deux bascules) ; (2) un **ouvreur singleton** `IOuvreurHistorique` (interface sous `Services/`, implémentation sous `Views/Historique/`, motif `IRecalibrationPrompt`) qui recrée une `HistoriqueWindow` après fermeture — une `Window` WPF fermée ne peut pas être ré-affichée ; (3) la **synchronisation du style** « par construction » : la carte F1 bind directement le `HistoriqueViewModel` singleton (exposé par le `MainViewModel`) au lieu de dupliquer un état de style.

Le code réel révèle quatre pièges bloquants que le plan doit traiter : la garde `GardesPerimetreTests` interdit la chaîne `.Lire(` dans `DiagnosticService.cs` (le diagnostic doit lire via la façade de la fenêtre et un helper neutre) ; `HistoriqueViewModel.ArreterHorloge()` se désabonne de `IEtatReconstruction.Changement` et rien ne se réabonne à la réouverture (bandeau F2 figé à la deuxième ouverture du singleton) ; `HistoriqueViewModel.Theme` est lu une fois au constructeur (un singleton garderait le thème du premier affichage) ; et **E1-ter n'est pas soldé** : à 2026-09-27 le relevé montre encore quatre overlays (3.1.0, 3.2.0, 3.2.1, 3.2.2) en marche.

**Primary recommendation:** 6 plans en 3 vagues — vague 1 : 35-01 (4 semaines : données + VM + textes, reprise du trou de minuit, robustesse de réouverture du VM) ∥ 35-02 (gestes + carte F1 + ouvreur) ∥ 35-03 (diagnostic) ; vague 2 : 35-04 (vue 4 semaines XAML + piste + tokens + fenêtre + reprises visuelles 34 + écarts bloquants DAEDALUS) ∥ 35-05 (README + data-sources §9 + garde de vocabulaire du README) ; vague 3 : 35-06 (release 3.3.0 puis constat VAL-05, `autonomous: false`).

## Standard Stack

### Core (déjà en place — ne rien ajouter)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK | 10.0.201 (installé) ciblant `net8.0-windows` | build / test / publish | vérifié `dotnet --version` le 2026-09-27 |
| WPF (`UseWPF`) | net8.0-windows | fenêtres, `OnRender`, `DispatcherTimer`, `MouseButtonEventArgs.ClickCount` | framework imposé |
| CommunityToolkit.Mvvm | 8.4.2 (csproj) | `[ObservableProperty]`, `[RelayCommand]` | déjà référencé |
| Microsoft.Extensions.Hosting | 8.0.1 (csproj) | DI, services hébergés | déjà référencé |
| xunit / Xunit.StaFact | 2.9.2 / 1.1.11 | `[Fact]`, `[WpfFact]` (collection « XAML WPF ») | projet de tests |

### Supporting (dans le dépôt, à réutiliser)
| Brique | Où | Usage en 35 |
|--------|----|-------------|
| `BornesPlage.QuatreSemaines` | `Services/Historique/BornesPlage.cs` | les 4 plages S-3…S, contiguës, calendrier local (169 h / 167 h) |
| `EchelleTemps.Fraction / X / Largeur` | `Rendering/Historique/EchelleTemps.cs` | projection par fraction de la plage de chaque semaine |
| `Escalier.Segments / ParBandes` | `Rendering/Historique/Escalier.cs` | escalier coupé aux trous, bandes de rampe, bande « épuisé » isolée |
| `PisteBase` (`Rendu`, `Plume`, `GeometrieEscalier`, `DessinerGrille`, `Tracer`) | `Controls/Historique/PisteBase.cs` | souche de la piste 4 semaines (trace + compteur gratuits) |
| `PisteCouverture` | `Controls/Historique/PisteCouverture.cs` | une rangée de couverture par semaine (DP `Plage/Serie/Trous/InstantLecture`) |
| `InstantVersXConverter`, `LargeurIntervalleConverter` | `Converters/HistoriqueConverters.cs` | annotations et zone « avant le journal » posées en XAML pur |
| `TextesHistorique` | `Text/TextesHistorique.cs` | SEUL producteur des mots (garde « aucun texte visible en dur ») |
| `SourceHistoriqueDisque` / `SourceHistoriqueDemonstration` | façades | à étendre (`LireQuatreSemaines`) ; la même façade sert le diagnostic |
| `InventaireProcessus`, `VerrouInstanceUnique` | Services | instances Chronos (déjà au diagnostic) |
| `NativeMethods` | `Interop/NativeMethods.cs` (`internal`) | ajouter `GetDoubleClickTime` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `PisteQuatreSemaines` dédiée | 4 × `PisteNiveau` superposées (`Opacity` par élément) | réutilise, MAIS exige un mode monochrome dans `PisteNiveau`, des brosses nulles pour taire trous/sauts, 4 grilles superposées, et touche le fichier le plus muté de 34 (h1, h2, p2, p3…) → rejeté |
| Temporisation du simple clic | Annulation (bascule immédiate puis re-bascule au 2ᵉ clic) | pas de latence, MAIS clignotement visible et deux bascules réelles (contraire au critère « sans déclencher la bascule ») → rejeté |
| `GetDoubleClickTime` (P/Invoke) | `System.Windows.Forms.SystemInformation.DoubleClickTime` | exige `UseWindowsForms` (non référencé) → P/Invoke user32, une ligne |
| Carte F1 bindée sur le `HistoriqueViewModel` | État de style dupliqué dans `MainViewModel` + événement `Modifie` sur `IReglagesHistorique` | duplication, et modification du VM de la fenêtre dans un plan parallèle → rejeté |

**Installation :** aucune (`dotnet restore` suffit).

## Architecture Patterns

### Recommended Project Structure (fichiers nouveaux / touchés)
```
src/Chronos/
├── Services/IOuvreurHistorique.cs                    # NOUVEAU (35-02) contrat neutre : void Ouvrir()
├── Services/Historique/
│   ├── ISourceHistorique.cs                          # 35-01 : + LireQuatreSemaines
│   ├── SourceHistoriqueDisque.cs                     # 35-01 : + LireQuatreSemaines, LireJour avec veille
│   ├── LectureVeille.cs                              # NOUVEAU (35-01) pur : relevé de la veille + événements utiles
│   ├── IEtatJournal.cs, JournalReleves.cs            # 35-02 : + JournalOuvertLe (amorcé hors UI)
│   └── EtatJournalHistorique.cs                      # NOUVEAU (35-03) pur/disque : inventaire, 5 derniers événements
├── Models/Historique/DonneesHistorique.cs            # 35-01 : + DonneesQuatreSemaines
├── Rendering/Historique/EchelleTemps.cs              # 35-01 : + Reporter(t, plageSource, plageCible)
├── ViewModels/
│   ├── ArbitreClicCentre.cs                          # NOUVEAU (35-02) pur : simple / double clic, horloge injectée
│   ├── MainViewModel.cs                              # 35-02
│   └── Historique/{HistoriqueViewModel, SourceHistoriqueDemonstration}.cs   # 35-01
├── Text/TextesHistorique.cs                          # 35-01 (mots) ; 35-04 (retrait InfobulleBientot)
├── Controls/Historique/PisteQuatreSemaines.cs        # NOUVEAU (35-04)
├── Views/Historique/
│   ├── VueQuatreSemainesView.xaml(.cs)               # NOUVEAU (35-04)
│   ├── OuvreurHistorique.cs                          # NOUVEAU (35-02)
│   └── HistoriqueWindow.xaml(.cs), VueSemaineView.xaml, VueJourView.xaml   # 35-04
├── Views/{MainWindow.xaml(.cs), SettingsWindow.xaml} # 35-02
├── Converters/HistoriqueConverters.cs                # 35-04 : + BorneInfobulleConverter
├── Resources/DesignTokens.xaml                       # 35-04 : + 5 sys:Double
├── Interop/NativeMethods.cs                          # 35-02 : + GetDoubleClickTime
├── Services/DiagnosticService.cs                     # 35-03 (CRLF à conserver)
└── App.xaml.cs                                       # 35-02 SEUL (IOuvreurHistorique)
README.md, docs/data-sources.md                       # 35-05
src/Chronos/Chronos.csproj                            # 35-06 (3.3.0 × 4)
```

### Pattern 1 : Données 4 semaines — une lecture, quatre analyses (35-01)
**What :** la façade lit le journal UNE fois sur `[S-3.Debut, S.Fin[`, partitionne en mémoire par plage, analyse chaque semaine à `InstantDAnalyse(now, plage_k)` (D-34-17 : sinon chaque semaine révolue finit par un faux trou ouvert), sans agrégats de tokens (pas de piste tokens en v1.8).
**Why one read :** `LecteurJournal.Lire` relit **à chaque appel** le plus ancien fichier mensuel en entier pour `JournalOuvertLe` (`PremiereLigneValide`) ; quatre appels = quatre relectures intégrales en plus.
```csharp
// Models/Historique/DonneesHistorique.cs (neutre)
public sealed record DonneesQuatreSemaines(
    IReadOnlyList<AnalyseJournal> Semaines,   // ordre CHRONOLOGIQUE S-3, S-2, S-1, S (celui de BornesPlage.QuatreSemaines) ; chaque Analyse porte sa Plage
    DateTimeOffset? JournalOuvertLe,
    DateTimeOffset LueA);

// ISourceHistorique : DonneesQuatreSemaines LireQuatreSemaines(IReadOnlyList<Plage> semaines, DateTimeOffset now);  // ne lève jamais
// SourceHistoriqueDisque (esquisse) :
var lecture = LecteurJournal.Lire(_dossier, semaines[0].Debut, semaines[^1].Fin);
var analyses = semaines.Select(p => AnalyseReleves.Analyser(
        new LectureJournal(lecture.Releves.Where(r => p.Contient(r.T)).ToList(),
                           lecture.Evenements.Where(e => p.Contient(e.T)).ToList(),
                           0, lecture.JournalOuvertLe, p),
        InstantsHistorique.InstantDAnalyse(now, p), cadence)).ToList();
```
**VM (`HistoriqueViewModel`) :** `IsVueQuatreSemaines` ; `ChoisirVue` n'ignore plus `QuatreSemaines` ; `AllerAuPresent` → `_semaines = BornesPlage.QuatreSemaines(now, _repere, _ancre, _tz)`, `PlageCourante = new Plage(_semaines[0].Debut, _semaines[3].Fin)` ; ‹ → `BornesPlage.QuatreSemaines(p.Debut.AddTicks(-1), p.Debut, _ancre, _tz)` (même motif que la semaine, D-34-13) ; › → avancer par `BornesPlage.SemaineDeForfait` quatre fois depuis `p.Fin`, puis `QuatreSemaines(derniere.Debut, derniere.Debut, …)` ; `LibellePeriode` = `TextesHistorique.LibellePeriodeQuatreSemaines(...)` (« 4 semaines de forfait · du sam. 29 août au sam. 26 sept. 2026 ») ; `TexteRetourPresent` = « Cette semaine » ; `TexteFraicheur` = `LigneFraicheurSemaine(S)` ; `AfficherMaintenant` reste Jour seulement.
**Dérivés VM à exposer (records neutres, mots de `TextesHistorique`) :**
- `LibellesJoursCourts` : 7 minuits locaux de S → « sam. », « dim. », … (nouveau `TextesHistorique.LibelleJourCourt` = `ddd` fr-FR ; l'axe ne peut pas porter « sam. 19 » puisqu'il sert quatre semaines).
- `EtiquettesSemaines` (4 entrées, rang 0 = S) : « S · 26 sept. · 43 % » / « S-1 · 19 sept. · pas de relevés (avant le journal) » / « pas de relevés » (semaine APRÈS l'ouverture mais sans relevé : ne pas dire « avant le journal » — distinction honnête). Valeur finale = `U7` du dernier relevé de la semaine via `TextesHistorique.Pourcent`.
- `AnnotationsEpuiseeSemaines` : premier relevé de la semaine où `Statut7 == Rejete || U7 >= 1.0` (miroir de D-34-19) → « épuisée jeu. 14:20 → bloquée jusqu'au reset », `Debut` **reporté sur l'axe de S** par `EchelleTemps.Reporter(t, plageSemaine_k, plageS)` (= `S.Debut + Fraction(t, k) × S.Duree`) pour être posé par `InstantVersXConverter` sans C# de vue.
- `RangeesCouverture` (4 entrées : libellé « S », « S-1 »…, `Plage`, `Serie`, `Trous`, `AvantJournal` (bool : `Plage.Fin <= JournalOuvertLe` ou journal absent), texte « avant le journal — aucun relevé » ou « aucun relevé », `JournalOuvert` (annotation si `JournalOuvertLe ∈ Plage`)) → `ItemsControl` + `DataTemplate` d'une `PisteCouverture` (DP bindées aux champs de l'entrée).
- `PiedQuatreSemaines` = « Rien n'est inventé avant l'ouverture du journal. »

### Pattern 2 : `PisteQuatreSemaines : PisteBase` (35-04)
**What :** une seule piste NIVEAU ; les quatre escaliers sont projetés chacun par fraction de SA plage sur la même largeur (D-34-20 généralisé) — la différence 168 / 169 h est absorbée par construction.
```csharp
// DP (toutes par PisteBase.Rendu, AffectsRender ; brosses à défaut null, tailles/opacités à 0 → piste non bindée invisible, D-34-18)
// Semaines (IReadOnlyList<AnalyseJournal>?) · Rampe (ChronosTheme?) · Gris (Brush?) · EpaisseurEscalier (double)
// OpaciteS1 / OpaciteS2 / OpaciteS3 (double, 0) · NbBandes (int, 12)
protected override void Dessiner(DrawingContext dc, double w, double h)
{
    DessinerGrille(dc, w, h, 0, 0.5, 1);
    // fantômes d'abord (S-3 le plus pâle), courante en dernier (au-dessus)
    for (var k = 3; k >= 1; k--)            // rang k = S-k
    {
        var a = Semaines[3 - k];             // ordre chronologique → S-k
        var segs = Escalier.Segments(a.Serie, a.Trous, r => r.Statut7 == StatutServeur.Rejete ? 1.0 : r.U7, a.Plage);
        if (segs.Count == 0 || Gris is null) continue;
        dc.PushOpacity(Opacite(k));          // DP liée au token, jamais un littéral
        dc.DrawGeometry(null, Plume(Gris, EpaisseurEscalier), GeometrieEscalier(segs, a.Plage, w, h, 1.0));
        dc.Pop();
        Tracer($"semaine {k} {segs.Count} {F(Opacite(k))}");
    }
    // S : bandes de rampe ; bande >= 1.0 peinte par Gris (plateau « épuisée », ReferenceEquals dans la trace comme 34-04)
}
```
**Trace de rendu à ajouter au vocabulaire 34-04 :** `semaine k n opacite` ; `bande …` / `palier …` réutilisés pour S. Les tests d'honnêteté 4 semaines lisent cette trace (motif `HonneteteHistoriqueTests`), jamais des pixels.
**Couverture par semaine :** 4 `PisteCouverture` existantes (hauteur `HistoHauteurCouverture` = 12 par rangée — pas de token nouveau) ; la zone « avant le journal » d'une rangée = `Rectangle` `HistoHachure` à `HistoOpaciteTrou` (motif `ZoneAvantJournal` de `VueSemaineView`) + texte de l'entrée.
**Réticule :** §2.4 n'en prévoit pas et `SurcoucheReticule` n'accepte qu'UNE série → pas de réticule en vue 4 semaines (le dire dans le SUMMARY et en docs, écart assumé).

### Pattern 3 : Tokens et table fermée (35-04)
`GardeTokensHistoriqueTests.Les_tokens_de_taille_sont_des_doubles_nommes` exige `doubles.Count == TaillesHisto.Length` : ajouter les lignes au tableau `TaillesHisto` EN MÊME TEMPS que les `sys:Double`. Tokens proposés (valeurs à confirmer sur la frame E de Figma si l'exécuteur y a accès ; sinon ces valeurs) :

| Clé | Valeur | Justification |
|-----|--------|---------------|
| `HistoOpaciteSemaine1` | 0.8 | §2.4 (verrouillé) |
| `HistoOpaciteSemaine2` | 0.45 | §2.4 (verrouillé) |
| `HistoOpaciteSemaine3` | 0.25 | §2.4 (verrouillé) |
| `HistoHauteurNiveauQuatreSemaines` | 200 (à confirmer frame E) | même corps que NIVEAU « Simplifié », seule piste de données de la vue |
| `HistoLargeurEtiquettesSemaines` | 180 (à confirmer frame E) | « S-2 · 5 sept. · pas de relevés (avant le journal) » ne tient pas dans `HistoLargeurLegendeDroite` (72) — sinon repli : étiquettes sur deux lignes |

38 → 43 `sys:Double`. Les `Thickness` ne sont pas comptés par ce test (seul `HistoBordRedimensionnement` est vérifié par le `[WpfFact]`). La garde « aucune valeur en dur » (motif durci 34-08 : `Opacite\w*|Epaisseur\w*|Longueur\w*|Plafond|NbBandes`) rougira tout `OpaciteS1="0.8"` littéral : lier `{StaticResource HistoOpaciteSemaine1}`. **Ne pas** utiliser `Opacity=` sur des éléments pour les fantômes : l'opacité se pousse dans la piste (`PushOpacity`).

### Pattern 4 : Double-clic sans double bascule — temporisation testable (35-02)
**WPF :** sur `MouseLeftButtonDown`, `e.ClickCount` vaut 1 au premier clic, 2 au second s'il arrive dans le délai ET le rectangle système de double-clic. Le code-behind ne fait que transmettre (garde textuelle), la décision est pure :
```csharp
// ViewModels/ArbitreClicCentre.cs — PUR (aucun WPF), horloge injectée par les appels
public enum ActionClicCentre { Rien, Basculer, OuvrirHistorique }
public sealed class ArbitreClicCentre(TimeSpan delai)
{
    private DateTimeOffset? _enAttenteDepuis;
    public ActionClicCentre Clic(int clickCount, DateTimeOffset t)
    {
        if (clickCount >= 2) { _enAttenteDepuis = null; return ActionClicCentre.OuvrirHistorique; }   // annule la bascule en attente
        _enAttenteDepuis = t; return ActionClicCentre.Rien;                                          // armée, pas encore décidée
    }
    public ActionClicCentre Echeance(DateTimeOffset t)
    {
        if (_enAttenteDepuis is { } t0 && t - t0 >= delai) { _enAttenteDepuis = null; return ActionClicCentre.Basculer; }
        return ActionClicCentre.Rien;
    }
}
// MainViewModel : ClicCentre(int clickCount) → arbitre.Clic(n, _clock.UtcNow) ; si Rien, démarre une DispatcherTimer one-shot
// (créée côté UI, jamais dans le ctor — Pitfall 4 du projet) ; au Tick : arbitre.Echeance(_clock.UtcNow) == Basculer → ToggleCenterMode().
// OuvrirHistorique → _ouvreurHistorique?.Ouvrir(). internal EcheanceClicCentre() pour les tests (FakeClock, sans timer).
// MainWindow.xaml.cs :
private void CentreHit_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    (DataContext as MainViewModel)?.ClicCentre(e.ClickCount);
    e.Handled = true;   // INCHANGÉ : pas de DragMove depuis le centre
}
// Délai : NativeMethods.GetDoubleClickTime() (user32, uint ms) lu une fois par la VUE et passé au VM (défaut VM 500 ms, valeur Windows par défaut).
[DllImport("user32.dll")] internal static extern uint GetDoubleClickTime();
```
**Tests :** `ArbitreClicCentreTests` (`[Fact]`) : un clic + échéance à `delai − 1 tick` → Rien, à `delai` → Basculer ; `1` puis `2` → OuvrirHistorique puis échéance → Rien (**zéro** bascule) ; deux clics lents (`2 × delai`, `ClickCount` 1 et 1) → deux bascules ; triple-clic (3) → ouvre (idempotent). `MainViewModelTests` : `ClicCentre(1)` + `FakeClock.Avancer(delai)` + `EcheanceClicCentre()` → `ShowCountdown` basculé une fois ; `ClicCentre(1)`,`ClicCentre(2)` → `FakeOuvreurHistorique.Ouvertures == 1`, `ShowCountdown` inchangé. `MouseButtonEventArgs.ClickCount` n'est pas réglable en test (setter interne) → **garde textuelle** sur `MainWindow.xaml.cs` (le handler transmet `e.ClickCount` et garde `e.Handled = true`) + `MouseRightButtonUp="OnRightClick"` et `MouseLeftButtonDown += Cadran_MouseLeftButtonDown` présents (drag / clic droit inchangés).
**Coût assumé, à dire au constat :** le simple clic bascule après ≈ `GetDoubleClickTime` (500 ms par défaut) — comportement standard Windows (renommage de l'Explorateur).

### Pattern 5 : Ouvreur singleton + carte F1 synchronisée (35-02)
```csharp
// Services/IOuvreurHistorique.cs (neutre)
public interface IOuvreurHistorique { void Ouvrir(); }

// Views/Historique/OuvreurHistorique.cs
public sealed class OuvreurHistorique(Func<Window> fabrique) : IOuvreurHistorique
{
    private Window? _fenetre;
    public void Ouvrir()
    {
        if (_fenetre is null)
        {
            _fenetre = fabrique();
            _fenetre.Closed += (_, _) => _fenetre = null;   // une Window WPF fermée ne se ré-affiche pas : on en recrée une
            _fenetre.Show();
        }
        else
        {
            if (_fenetre.WindowState == WindowState.Minimized) _fenetre.WindowState = WindowState.Normal;
            _fenetre.Activate();                              // premier plan (geste utilisateur → SetForeground autorisé)
        }
    }
}
// App.xaml.cs (35-02 SEUL propriétaire) :
services.AddSingleton<IOuvreurHistorique>(sp => new OuvreurHistorique(() => new HistoriqueWindow(sp.GetRequiredService<HistoriqueViewModel>())));
```
`MainViewModel` : paramètres optionnels **en dernière position** (motif 32-05 / 33-05, `AddSingleton<MainViewModel>()` sans fabrique les injecte) : `IOuvreurHistorique? ouvreurHistorique = null`, `HistoriqueViewModel? historique = null` ; `[RelayCommand] OuvrirHistorique()` ; propriété `Historique` exposée.
**Carte F1 (SettingsWindow.xaml, DataContext = `MainViewModel`) :** bouton `Command="{Binding OuvrirHistoriqueCommand}"` ; puces de style `Command="{Binding Historique.ChoisirStyleCommand}"` + `CommandParameter` `x:Static` + surbrillance `{Binding Historique.IsStylePistes}` — c'est **la même instance, le même `IReglagesHistorique`, la même commande** que le sélecteur de la fenêtre : synchronisation par construction, zéro état dupliqué, zéro modification du VM de la fenêtre (propriété de 35-01). Carte masquée si `Historique` est null (tests existants qui ne l'injectent pas).
**Fusion avec la carte 32-05 :** la carte « Journal des relevés » disparaît en tant que carte ; son contenu devient la ligne d'état de la carte F1 — **garder les noms `LigneEtatJournal` et `PastilleJournal`** (tenus par `ReglagesBindingTests`) et la même source `TexteEtatJournal` / `AlerteJournal` (mêmes mots que le diagnostic, D-32-22). Sous-texte : « hebdo / 5 h / tokens · journal du <date> » ; la « dernière écriture » n'apparaît qu'UNE fois (dans `TexteEtatJournal`). Le texte « … — sans interface pour l'instant » (SettingsWindow.xaml l. 215) devient faux : le retirer.
**« journal du <date> » :** `IEtatJournal` ne connaît aujourd'hui que la dernière écriture de CE processus. Ajouter `DateTimeOffset? JournalOuvertLe { get; }` (implémenteurs : `JournalReleves`, `FakeEtatJournal` — vérifié, il n'y en a pas d'autre), amorcé **hors du thread UI** au démarrage (`Task.Run` depuis `JournalisationUsageProvider.StartAsync`, qui s'exécute aujourd'hui synchronement sur le thread UI d'`OnStartup`) et posé à la première écriture si le dossier était vide. Lecture légère : la première ligne valide du plus ancien `releves-AAAA-MM.jsonl` (pas un `LireFichier` complet). Inconnu → le segment « journal du … » est omis, jamais inventé.
**Couleurs et tailles de la carte :** couleurs uniquement par `{StaticResource Accent|Panel2|Ink|Ink2}` / `{DynamicResource Alerte}` — `SettingsWindow_garde_ses_douze_litteraux_et_pas_un_de_plus` rougit au 13ᵉ hexadécimal ; tailles selon la convention LOCALE de `SettingsWindow` (FontSize 13 / 10.5 / 12, `BorderThickness="1.5"` déjà employé par `StyleChip`) — ce fichier n'est pas sous la garde numérique de `Views/Historique`.
**Ordre dans DONNÉES (discrétion) :** Connexion Claude → Sonde d'en-têtes → **Historique d'utilisation** (bordure `Accent` 1,5, remplace la carte « Journal des relevés »).

### Pattern 6 : Section « Journal d'historique » du diagnostic (35-03)
**Contrainte dure (gardes existantes sur `DiagnosticService.cs`) :** `Assert.DoesNotContain(".Lire(")`, `Assert.DoesNotContain("new SessionMonitor")`, `Assert.Single("Inspecter(")`, `RacinesEtat` absent. Donc **aucun** `LecteurJournal.Lire(` ni `_reglages.Lire(` dans ce fichier.
**Recette « même lecture que la fenêtre » :**
- relevés du jour, interruptions, journal ouvert le : `new SourceHistoriqueDisque(_paths, fuseau).LireJour(BornesPlage.Jour(now, fuseau), now)` (la façade EXACTE de la fenêtre, sans état → une instance locale lit comme le singleton ; `.LireJour(` ne matche pas `.Lire(`) puis `TextesHistorique.LigneFraicheurJour(...)` → « 288 relevés attendus · N présents · N interruption(s) (cause, HH:MM → HH:MM) », les MÊMES mots que la vue Jour ;
- 5 derniers événements + inventaire des fichiers : nouveau helper neutre `Services/Historique/EtatJournalHistorique.cs` (lit `LecteurJournal` sur `[now − 7 j, now]`, `TakeLast(5)`, noms de fil `TypeEvenementTexte.Nom` — `demarrage`, `arret`, `jeton_invalide`, `sonde_refusee`, `reprise`, `ecriture_ratee` ; inventaire par familles via les constantes de nom (`JournalReleves.NomFichier`, `MagasinAgregats.NomFichier`, `Curseurs.NomFichier`, `CouvertureTokens.NomFichier`, motif `ids-*`) : nom, taille, âge de modification) ;
- reconstruction : UNE ligne qui n'existe pas encore — « terminée le <date HH:mm> » (`DerniereReconstructionTerminee`) ou « en cours — N / M fichiers » ; le détail (phase, dernier fichier, disparus, ids) reste dans `[Magasins persistants]` (pas de copie) ;
- instances : relever `InventaireProcessus.Relever()` UNE fois dans `BuildReportAsync` et référencer le compte (« N (détail sous [Magasins persistants]) ») — pas de seconde lecture de la table des processus.
**Paramètres :** `TimeZoneInfo? fuseau = null` en dernière position, repli `TimeZoneInfo.Local` (le reste du rapport emploie déjà `ToLocalTime()`, et c'est exactement ce que la DI enregistre) ; **ne pas toucher `App.xaml.cs`** en vague 1 (propriété de 35-02). Placement : section `[Journal d'historique]` juste après `[Magasins persistants]`. Garde nouvelle dans un fichier de test NOUVEAU (pas `GardesPerimetreTests.cs`, que 35-02 peut toucher) : le diagnostic lit la journée par `LireJour(` et n'appelle ni `AnalyseReleves.Analyser(` ni `LecteurJournal.Lire(`. Fichier en **CRLF** (32-05 : « CRLF conservé »).

### Pattern 7 : Docs gardées (35-05)
- README : nouvelle section `## Historique d'utilisation` (fenêtre, 3 vues, 3 styles, gestes « double-clic au centre » / « Réglages → Historique d'utilisation → Ouvrir », règles d'honnêteté, mots §4) ; ligne « Au centre » à corriger (simple clic = bascule après le délai de double-clic ; double-clic = Historique) ; ligne « Stack » (« plus de 1 100 tests » → chiffre réel).
- `docs/data-sources.md` : `## 9. Lecture par la fenêtre Historique` APRÈS le `---` qui clôt le §8 (motif 32-07 / 33-05 : `SectionDe` coupe au prochain `## `, `SectionAgregats` au dernier `---`), puis `---` et ligne finale — **la ligne finale doit encore contenir « §8 »** (`ContratAgregatsDocumenteTests` : `Assert.Contains("§8", derniere)`), ex. « … complété le 2026-09-27 (§7 … ; §8 agrégats de tokens ; §9 lecture par la fenêtre) ». Contenu : façade `ISourceHistorique`, `InstantDAnalyse`, une source par vue (D-32-25), veille de minuit (si 35-01 la fait), divergence (seuil 0,01), et **écarts connus des maquettes** (rayon DWM ≈ 8 px, coins droits W10, badge omis, Segoe UI au lieu d'Inter, tirets de reset dans un trou, pas de réticule en 4 semaines).
- Garde : nouveau `GardeDocumentationHistoriqueTests.cs` — motif `Projection` de 34-08 appliqué **à la section README « Historique d'utilisation » et au §9 seulement** ; mots §4 présents dans la section ; anti-mutisme (section ≥ N lignes). La phrase d'honnêteté s'écrit SANS les mots bannis (« les pourcentages sont des relevés exacts du serveur », « rien n'annonce l'avenir » — jamais « aucune projection », jamais « pas une estimation »).

### Anti-Patterns to Avoid
- **Ré-afficher une `Window` fermée** (`Show()` après `Close()`) → `InvalidOperationException`. Recréer (Pattern 5).
- **`Opacity` littéral sur les fantômes** → garde rouge ; et opacité d'élément ≠ opacité de trait (la grille serait atténuée aussi).
- **Une piste qui calcule** : toute arithmétique de temps/valeur passe par `EchelleTemps` / `EchelleValeur` / `Escalier` (34-02).
- **Texte visible écrit en XAML** (`Text="…"` littéral sous `Views/Historique`) → `Aucun_texte_visible_ecrit_en_dur…` rouge ; tolérés : « Chronos », ✕ ‹ ›.
- **Appeler un nouvel API d'un plan voisin de la même vague** (ex. 35-02 appelant `HistoriqueViewModel.ActualiserTheme()` créé par 35-01) → compilation cassée tant que le voisin n'a pas commité. Les appels croisés passent en vague 2 (35-04).
- **`GardesPerimetreTests.cs` / `CompositionRootTests.cs` / `VueJourBindingTests.cs` modifiés par deux plans de la même vague** → une nouvelle garde va dans un fichier de test NOUVEAU.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Bornes de 4 semaines (DST, 169 h) | arithmétique `AddDays(-7)` sur UTC | `BornesPlage.QuatreSemaines` | calendrier local, contiguïté testée (32-06) |
| Détection simple / double clic | comparer des `Environment.TickCount` dans le code-behind | `e.ClickCount` WPF + `ArbitreClicCentre` pur | WPF applique déjà délai ET rectangle système |
| Délai de double-clic | constante 500 | `GetDoubleClickTime()` user32 | réglage utilisateur Windows |
| Trous / sauts / resets des semaines | recalcul dans la piste | `AnalyseReleves.Analyser` + `Escalier.Segments` | coupure aux trous prouvée par mutations h1/s1 |
| Projection d'une semaine sur l'axe d'une autre | `(t − Debut).TotalHours / 168` | `EchelleTemps.X(t, plageSemaine, w)` / `Reporter` | 168 ≠ 169 h (Pitfall 10 de 34) |
| Mots visibles | chaînes dans le VM / la vue / le diagnostic | `TextesHistorique` (et `CauseTrouTexte`) | gardes « texte en dur » et vocabulaire |
| Couverture d'une semaine | nouvelle piste | `PisteCouverture` × 4 | déjà testée (q4, mutation couleur) |
| Lecture de la journée au diagnostic | `LecteurJournal.Lire` + `AnalyseReleves` recopiés | `SourceHistoriqueDisque.LireJour` + `LigneFraicheurJour` | « même lecture que la fenêtre », et garde `.Lire(` |
| Singleton de fenêtre | `static HistoriqueWindow Instance` | `IOuvreurHistorique` DI | testable, motif `ISessionsController` |
| Publication | script maison | `dotnet publish … -p:PublishSingleFile=true --self-contained true` + procédure 32-07 | contrôles déjà définis et rejoués 3 fois |

**Key insight :** chaque promesse d'honnêteté du plan de design est déjà une fonction pure testée ; la phase 35 ne doit ajouter que de la composition (façade, VM, XAML) et des gardes — toute logique nouvelle de temps ou de valeur est un risque de régression sur des mutations déjà prouvées.

## Common Pitfalls

### Pitfall 1 : La garde `.Lire(` du diagnostic
**What goes wrong :** `LecteurJournal.Lire(` écrit dans `DiagnosticService.cs` rougit `Le_diagnostic_lit_la_source_app_bureau_dans_la_meme_lecture_que_le_widget` (garde écrite pour les sessions, mais textuelle sur tout le fichier).
**How to avoid :** Pattern 6 (façade `.LireJour(` + helper `EtatJournalHistorique`). **Warning sign :** un test de `GardesPerimetreTests` rouge sur une assertion `DoesNotContain(".Lire(")`.

### Pitfall 2 : Bandeau F2 figé à la deuxième ouverture
**What goes wrong :** `HistoriqueViewModel.ArreterHorloge()` (appelée au `Closed`) fait `_reconstruction.Changement -= …` ; l'abonnement n'existe qu'au constructeur. Singleton + seconde ouverture = plus aucune mise à jour temps réel du bandeau (seul le tick de 60 s le rafraîchit).
**How to avoid :** 35-01 : `DemarrerHorloge()` (ré)abonne si désabonné (drapeau, idempotent) ; test : ouvrir → arrêter → redémarrer → 50 `Changement` → `PostCount + 1`.

### Pitfall 3 : Thème figé du singleton
**What goes wrong :** `Theme = ThemeCatalog.ByKey(lus.ThemeKey)` au constructeur ; si le `MainViewModel` injecte le `HistoriqueViewModel` (Pattern 5), le VM naît au démarrage et garde ce thème pour toujours ; la fenêtre injecte `vm.Theme.BrushTokens()` à SA construction.
**How to avoid :** 35-01 rend `Theme` observable + `ActualiserTheme()` (relit `IReglagesHistorique`) ; 35-04 l'appelle en tête du constructeur de `HistoriqueWindow` avant l'injection des pinceaux (vague 2 : l'API existe).

### Pitfall 4 : Fuite d'abonnement des fenêtres recréées
**What goes wrong :** `vm.FermetureDemandee += …` dans le constructeur de `HistoriqueWindow`, jamais retiré : le VM singleton retient toutes les fenêtres fermées (arbre visuel complet).
**How to avoid :** 35-04 : handler nommé, retiré au `Closed`.

### Pitfall 5 : Trou de minuit — ce que la correction change dans les tests épinglés
**What goes wrong :** lire « la veille » fait entrer dans `DonneesJour.Analyse.Serie` UN relevé antérieur à `Plage.Debut`. Conséquences sur le scénario (mercredi 23) : un trou « Chronos arrêté » (21:00Z → 05:00Z) apparaît, un saut 5 h « au moins un reset pendant l'absence (répartition inconnue) » apparaît, un reset 5 h OBSERVÉ à l'ancienne borne (Wed 00:00 local = `Plage.Debut`, donc `Contient` vrai) peut apparaître, et `LigneFraicheurJour` compterait un relevé de trop.
**How to avoid :** helper pur `LectureVeille` (dernier relevé de la source la plus fréquente du jour dans `[Debut − 7 j, Debut[` + événements de `]T − cadence, Debut[`) appliqué **à la vue Jour seulement** dans les deux façades ; `LigneFraicheurJour` compte `Serie.Count(r => jour.Contient(r.T))` et préfixe le jour quand un trou commence avant la plage (« mar. 23:00 → 07:00 ») ; mettre à jour, dans 35-01, les assertions qui épinglaient l'ancien comportement : `VueJourBindingTests` l. 254-258 et 313, `HonneteteHistoriqueTests` l. 508-513, et toute assertion sur le nombre de resets du mercredi (34-07 : `DoesNotContain("reset 5 h 04:00")`, liste « 09:00 / 14:00 / 19:00 »). Aucun changement de `AnalyseReleves` (pur). Les pistes tolèrent déjà `T < Plage.Debut` (clip + `InstantVersX` borné à 0).
**Warning signs :** « 0 interruption » le mercredi après correction ; un palier dessiné avant x = 0 non clippé.

### Pitfall 6 : `InfobulleBientot` retiré trop tôt
**What goes wrong :** `HistoriqueWindow.xaml` référence `{x:Static txt:TextesHistorique.InfobulleBientot}` ; si 35-01 retire la constante en vague 1, la compilation casse jusqu'à 35-04.
**How to avoid :** 35-01 garde la constante ; 35-04 retire le `IsEnabled="False"`, l'infobulle ET la constante, et met à jour `HistoriqueBindingTests` l. 144 et `TextesHistoriqueTests` l. 46.

### Pitfall 7 : Race temporisation / second clic
**What goes wrong :** minuterie à exactement `delai` : si le `Tick` passe avant l'entrée souris du second clic, bascule PUIS ouverture.
**How to avoid :** l'arbitre décide par l'instant (`t − t0 >= delai`) et le second clic annule toute attente ; accepter le cas limite (une bascule au plus, jamais deux) et l'écrire. Ne jamais recourir à `Thread.Sleep`.

### Pitfall 8 : Chevauchements de fichiers entre plans parallèles
**What goes wrong :** agents parallèles sans worktree ; un `git add` explicite d'un fichier partagé emporte les hunks du voisin (précédents 32-05 / 34-06 / 34-07).
**How to avoid :** matrice de propriété ci-dessous ; nouvelles gardes dans des fichiers NOUVEAUX ; `App.xaml.cs` à 35-02 seul ; `requirements mark-complete` par blob depuis HEAD (mémoire « outillage de session »).

### Pitfall 9 : Garde de vocabulaire sur tout le README
**What goes wrong :** le README (cadran) dit légitimement « Estimation (repli) », « estimée », « `~` signale une estimation » (le mode estimé existe encore : 37 occurrences d'`Estimated` dans `src/`). Une garde sur tout le README rougit à l'entrée.
**How to avoid :** garde limitée à la section `## Historique d'utilisation` (et au §9) ; écrire l'honnêteté sans « projection » / « estim… » / « tendance ».

### Pitfall 10 : Dernière ligne de `data-sources.md`
**What goes wrong :** ajouter le §9 et réécrire la ligne finale sans « §8 » → `ContratAgregatsDocumenteTests` rouge.
**How to avoid :** Pattern 7.

### Pitfall 11 : « Hachuré » vs « rectangle `Line` 35 % » et « cause inconnue »
**What goes wrong :** VAL-05 dit « un trou réel … est hachuré et annoté avec sa cause ». Dans le code, un trou est un rectangle `Line` à 35 % à bordure pointillée (la hachure `HistoHachure` sert à « avant le journal » et aux tokens hors couverture). Et un PC mis en **veille** (processus vivant, aucun `arret` ni `demarrage`) produit un trou « **cause inconnue** » (D-32-26 : `reprise` ne cause rien) ; seul un arrêt/redémarrage produit « Chronos arrêté ».
**How to avoid :** le protocole du constat l'écrit à l'avance (attendus selon que le PC a été éteint ou mis en veille) ; « cause inconnue » est un résultat honnête, pas un écart.

### Pitfall 12 : E1-ter toujours ouvert
**What goes wrong :** relevé le 2026-09-27 (`Get-CimInstance Win32_Process`) : `Chronos-v3.1.0.exe` PID 40772, `Chronos-v3.2.0.exe` 126160, `Chronos-v3.2.1.exe` 121900, `Chronos-v3.2.2.exe` 87604 — tous parent `explorer.exe`. Le verrou `Local\Chronos-overlay` n'est connu que de la 3.2.2 : la 3.3.0 lancée avant de quitter la 3.2.2 serait REFUSÉE (bon), mais les trois anciennes continueraient d'écrire `treated.json` / `last-exact.json`.
**How to avoid :** étape 1 du constat = quitter les QUATRE (réglages → « Quitter Chronos » ; la 3.1.0 peut avoir l'ancien menu) ; sonde WMI « 0 processus Chronos » AVANT le lancement de la 3.3.0.

## Code Examples

### Relevé de la sonde hors arbre (précédent 32-08-PLAN l. 85-105)
```powershell
# sonde.ps1 (écrit sous %USERPROFILE%\Documents\chronos-constat, jamais sous AppData)
$me = Get-CimInstance Win32_Process -Filter "ProcessId = $PID"
L ("parent: " + (Get-CimInstance Win32_Process -Filter "ProcessId = $($me.ParentProcessId)").Name)   # attendu : WmiPrvSE.exe
L ("APPDATA\Claude existe : " + (Test-Path "$env:APPDATA\Claude"))                                    # attendu : False
Get-CimInstance Win32_Process -Filter "Name like 'Chronos%'" | % { L ("proc {0} pid={1} parent={2} cree={3:o} cmd={4}" -f $_.Name,$_.ProcessId,$_.ParentProcessId,$_.CreationDate,$_.CommandLine) }
# lancement :
# powershell -NoProfile -Command "Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"<chemin du ps1>\"' } | Select-Object -ExpandProperty ReturnValue"  → 0
```

### Procédure de release (32-07, à rejouer pour 3.3.0)
```bash
# 0. porte zéro warning : dotnet build Chronos.sln -c Release --nologo → 0 avertissement ; dotnet test Chronos.sln -c Debug --nologo -v q ×2
# 1. csproj : Version 3.3.0 · FileVersion 3.3.0.0 · AssemblyVersion 3.3.0.0 · InformationalVersion 3.3.0 (4 lignes, 0 CR, 3.2.2 × 0)
# 2. suite complète ×2 après le bump (VersionPublieeTests relit csproj + assembly)
dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
# 3. contrôles : publish/ = Chronos.exe (+ .pdb), 0 DLL ; taille < 120 000 000 o (3.2.2 = 77 557 997 o ; attendu +~0,3-0,6 Mo)
#    VersionInfo FileVersion 3.3.0.0 / ProductVersion 3.3.0 / ProductName Chronos ; md5 ≠ 51f4d95bb346b9a4ce33f3f63f3d434c (3.2.2)
# 4. copie à la racine : Chronos-v3.3.0.exe (md5 identique) ; git check-ignore -v → .gitignore:22:/Chronos-v*.exe
# 5. smoke : md5 ~/.claude/settings.json avant ; "./Chronos-v3.3.0.exe" --hook SessionStart < /dev/null → code 0 ; md5 après IDENTIQUE ;
#    0 processus Chronos-v3.3.0 résident ; (vue virtualisée de l'agent : le smoke ne prouve ni verrou ni journal, D-32-34)
# 6. commit « release: Chronos 3.3.0 - v1.8 Historique d'utilisation » ne contenant QUE src/Chronos/Chronos.csproj ; aucun tag, aucun push
#    (PUBLICATION GitHub = étape ZEUS après le constat)
```

### Galerie de revue (utilisateur seulement)
```
dotnet run --project src/Chronos -- --historique      # ou Chronos-v3.3.0.exe --historique ; ne prend pas le verrou, n'écrit rien
```

## Matrice de propriété des fichiers (découpage recommandé)

| Plan | Vague | Req | Fichiers (source) | Fichiers (tests) |
|------|-------|-----|-------------------|------------------|
| 35-01 | 1 | HIS-05 (données/VM) + minuit + réouverture | `Models/Historique/DonneesHistorique.cs`, `Services/Historique/{ISourceHistorique,SourceHistoriqueDisque,LectureVeille*}.cs`, `ViewModels/Historique/{HistoriqueViewModel,SourceHistoriqueDemonstration}.cs` (+ `ScenariosHistorique.cs` seulement si l'Open Question 1 est tranchée « scénario »), `Text/TextesHistorique.cs`, `Rendering/Historique/EchelleTemps.cs` | `Fakes/FakeSourceHistorique.cs`, `HistoriqueViewModelTests`, `TextesHistoriqueTests`, `SourceHistoriqueDisqueTests`, `ScenariosHistoriqueTests`, `EchellesHistoriqueTests`, `LectureVeilleTests*`, `VueJourBindingTests` (lignes minuit), `HonneteteHistoriqueTests` (lignes minuit) |
| 35-02 | 1 | ACC-01, ACC-02 | `Services/IOuvreurHistorique.cs*`, `Views/Historique/OuvreurHistorique.cs*`, `ViewModels/ArbitreClicCentre.cs*`, `ViewModels/MainViewModel.cs`, `Views/MainWindow.xaml(.cs)`, `Views/SettingsWindow.xaml`, `Interop/NativeMethods.cs`, `Services/Historique/{IEtatJournal,JournalReleves,JournalisationUsageProvider}.cs`, `App.xaml.cs` | `ArbitreClicCentreTests*`, `OuvreurHistoriqueTests*`, `Fakes/FakeOuvreurHistorique.cs*`, `Fakes/FakeEtatJournal.cs`, `MainViewModelTests`, `ReglagesBindingTests`, `CompositionRootTests`, `JournalRelevesTests`, `GardeGestesCadranTests*` |
| 35-03 | 1 | ACC-03 | `Services/DiagnosticService.cs` (CRLF), `Services/Historique/EtatJournalHistorique.cs*` | `DiagnosticServiceTests`, `EtatJournalHistoriqueTests*`, `GardeDiagnosticHistoriqueTests*` |
| 35-04 | 2 | HIS-05 (vue) + reprises 34 + bloquants DAEDALUS | `Controls/Historique/PisteQuatreSemaines.cs*`, `Views/Historique/VueQuatreSemainesView.xaml(.cs)*`, `Views/Historique/{HistoriqueWindow.xaml(.cs),VueSemaineView.xaml,VueJourView.xaml}`, `Converters/HistoriqueConverters.cs`, `Resources/DesignTokens.xaml`, `Text/TextesHistorique.cs` (retrait `InfobulleBientot`) | `GardeTokensHistoriqueTests`, `HistoriqueBindingTests`, `PistesHistoriqueTests`, `HistoriqueConvertersTests`, `VueQuatreSemainesBindingTests*`, `HonneteteQuatreSemainesTests*`, `GardeVocabulaireHistoriqueTests` (fichiers-clés), `TextesHistoriqueTests`, `VueSemaineBindingTests` / `VueJourBindingTests` si besoin |
| 35-05 | 2 | ACC-04 (docs) | `README.md`, `docs/data-sources.md` (§9), `docs/publish.md` (premier lancement 3.3.0 : reconstruction ≈ 15 s à froid) | `GardeDocumentationHistoriqueTests*` |
| 35-06 | 3 | ACC-04 (release) + VAL-05 | `src/Chronos/Chronos.csproj`, `.planning/phases/35-…/35-CONSTAT.md` | — (suite complète ×2) |

`*` = fichier nouveau. Alternative si l'orchestrateur tient à séparer release et constat comme la roadmap : 35-06 release (autonome, vague 3) puis 35-07 constat (`autonomous: false`, vague 4).

**Reprises de 34 dans 35-04 (point 6 de la consigne) :**
- *Infobulle non bornée* : `BorneInfobulleConverter` (`IMultiValueConverter` tolérant, `x, largeurInfobulle, largeurCanvas → clamp(x, 0, max(0, W − wInfobulle))`) appliqué aux 4 infobulles (3 styles Semaine + Jour). Les tests existants épinglent `Canvas.GetLeft(infobulle) == XReticule` (VueJourBindingTests l. 349, VueSemaineBindingTests l. 291) au milieu de la piste : ils restent verts ; un test nouveau couvre le bord droit.
- *Annotations Jour qui se chevauchent* (reset 15:00 dans le trou « jeton » 14:00-16:00 du scénario) : séparer resets / épuisée et trous sur deux rangées `HistoHauteurAnnotations` (token existant) ; aucun calcul de largeur de texte côté VM.
- *Écarts bloquants DAEDALUS* : `.zeus/state.json` est encore en `DESIGN-REVIEW` / `en_attente_galerie_utilisateur` et aucun rapport de revue n'existe dans `.zeus/reports/` au 2026-09-27 → le planificateur réserve une tâche « écarts bloquants de la revue » dans 35-04, à remplir à partir du rapport s'il existe au moment de planifier, sinon plan de rattrapage avant 35-06.

## Constat VAL-05 — ce que la fenêtre doit montrer sur les VRAIES données

| Élément | Attendu (faits relevés) |
|---------|------------------------|
| Semaine courante | sam. 26 sept. 00:00 → sam. 3 oct. 00:00 ; zone hachurée sam. 26 00:00 → dim. 27 08:07 + « journal ouvert le 27 sept. 2026 » (premier `demarrage` 2026-09-27T06:07:10Z, version 3.2.2) |
| Relevés | ≈ 1 / 5 min depuis 08:07 (36 relevés constatés à T+194 min, 0 doublon) |
| Tokens | reconstruits au PREMIER lancement de la 3.3.0 (la 3.2.2 ne les écrit pas) : `tokens-2026-06..09.jsonl`, `ids-…`, `curseurs.json`, `couverture.json` apparaissent (≈ 15 s à froid) ; bandeau F2 visible seulement si la fenêtre est ouverte dans ces secondes ; barres visibles aussi AVANT le 27/09 08:07 (tokens couverts ≠ relevés) |
| Trou réel | exige au moins une nuit avec l'overlay puis PC éteint → jouer le point « trou » le lendemain ; cause « Chronos arrêté » (arrêt/redémarrage) ou « cause inconnue » (veille) |
| 4 semaines | S partielle (marqueur « journal ouvert 27 sept. »), S-1 (19→26), S-2, S-3 : « pas de relevés (avant le journal) » ; aucune semaine épuisée (données réelles) |
| Diagnostic | `[Journal d'historique]` : fichiers et tailles, « 288 relevés attendus · N présents · … », 5 derniers événements, reconstruction terminée le …, instances : 1 |
| Réconciliation | `~/.claude/settings.json` : md5 relevé juste avant le lancement ; 9 occurrences `Chronos-v3.2.2.exe` → `Chronos-v3.3.0.exe` ; sauvegarde horodatée de md5 = l'état d'avant |
| Gestes | carte F1 « Ouvrir » ; double-clic centre (fenêtre fermée → ouverte ; ouverte derrière une autre → premier plan, UNE seule entrée dans la barre des tâches) ; simple clic toujours → bascule (après ≈ 0,5 s) ; drag par les anneaux ; clic droit → réglages |
| 32-08 | tableau L1, L2, L2b, L3, L4, Q et V01…V12 jamais joués (32-CONSTAT §1-§2 « à relever ») → à inclure dans la même séance (sur la 3.3.0) |

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Segment « 4 semaines » désactivé (« bientôt (phase 35) ») | actif, vue dédiée | 35-04 | retrait de `InfobulleBientot` + 2 tests à réécrire |
| Carte minimale « Journal des relevés » (« sans interface pour l'instant ») | carte F1 « Historique d'utilisation » | 35-02 | texte devenu faux à retirer |
| Clic centre = bascule immédiate | bascule après le délai de double-clic ; double-clic = Historique | 35-02 | latence ≈ 500 ms à dire |
| Vue Jour muette sur le trou qui chevauche minuit | trou nommé, borné à la plage | 35-01 (recommandé) | 4 à 6 assertions épinglées changent |

**Deprecated/outdated :** `TextesHistorique.InfobulleBientot` (après 35-04) ; phrase « sans interface pour l'instant » ; README « Clique au centre … re-clique » et liste « Clic droit dessus pour le menu » (il n'y a plus de menu contextuel).

## Open Questions

1. **Une semaine épuisée dans la galerie `--historique` ?**
   - Ce qu'on sait : le scénario de référence n'a aucune semaine épuisée ; S-1 finit à ≈ 0,55, épinglé par `ScenariosHistoriqueTests` l. 161 (`InRange 0.40 … 0.60`) ; S-2 / S-3 sont avant le journal (14 sept.). DESIGN_PLAN §8.2 veut capturer « vue 4 semaines avec semaine épuisée ».
   - Recommandation : prouver l'épuisée par une fixture DÉRIVÉE des objets du scénario (motif `FakeSourceHistorique.TransformerSemaine`, D-34-35) dans `HonneteteQuatreSemainesTests` ; pour la galerie, soit accepter l'écart (noté dans la revue), soit ajouter à `SourceHistoriqueDemonstration` une transformation explicite et nommée de S-1 réservée à la vue 4 semaines — à trancher par le planificateur (ne PAS modifier `ScenariosHistorique.Generer`).
2. **Valeurs exactes des 2 tokens de mise en page (hauteur NIVEAU 4 semaines, largeur des étiquettes)** — frame E de Figma non lue ici (pas d'accès MCP Figma depuis cet agent). Recommandation : l'exécuteur lit la frame E s'il en a l'accès ; sinon 200 / 180 et écart dit en revue.
3. **Veille de minuit : Jour seulement, ou aussi Semaine / 4 semaines ?** La même coupure existe au samedi 00:00. Recommandation : Jour seulement en v1.8 (c'est le point différé nommé ; l'appliquer à la Semaine changerait des comptes épinglés sur le scénario) ; noter l'extension en RETOUR ROADMAP.
4. **Rapport de revue DAEDALUS absent** au moment de la recherche — voir ci-dessus (tâche réservée dans 35-04).
5. **`TimeZoneInfo.Local` en repli dans `DiagnosticService`** : conforme à l'usage actuel du rapport (`ToLocalTime()`), mais D-32-29 préfère un fuseau composé à la racine. Si l'orchestrateur le veut injecté, la ligne `fuseau: sp.GetRequiredService<TimeZoneInfo>()` s'ajoute en vague 2 (35-04 ou 35-05 ne touchent pas `App.xaml.cs` ; la faire porter par un plan de vague 2 explicitement).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build / test / publish | ✓ | 10.0.201 (cible net8.0-windows) | — |
| Runtime pack .NET 8 win-x64 | publish self-contained | ✓ (publié 3.2.2 le 27/09) | 8.x | — |
| PowerShell + CIM (`Invoke-CimMethod`) | sonde hors arbre | ✓ | Windows 11 Pro 26200 | — |
| `Chronos-v3.2.2.exe` (référence md5) | contrôle « md5 ≠ » | ✓ | 77 557 997 o | — |
| Figma MCP (frame E / F1) | valeurs de mise en page | ✗ pour cet agent | — | valeurs recommandées + écart dit |
| Overlay en marche | constat | ✓ mais **4 instances** (E1-ter) | 3.1.0 / 3.2.0 / 3.2.1 / 3.2.2 | geste utilisateur : tout quitter |

**Missing dependencies with no fallback :** aucune.
**Missing with fallback :** accès Figma (valeurs par défaut documentées).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`, `[Collection("XAML WPF")]`) |
| Config file | `tests/Chronos.Tests/Chronos.Tests.csproj` (AssemblyMetadata `CheminSourcesChronos` / `CheminDocsChronos`) |
| Quick run command | `dotnet test Chronos.sln -c Debug --nologo -v q --filter "FullyQualifiedName~<Classe1>|FullyQualifiedName~<Classe2>"` |
| Full suite command | `dotnet test Chronos.sln -c Debug --nologo -v q` (1565 verts à l'entrée, ≈ 10-15 s) ; `dotnet build Chronos.sln -c Release --nologo` → 0 avertissement |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| HIS-05 | façade : une lecture, 4 analyses à `InstantDAnalyse`, semaines avant le journal vides | unit | `--filter FullyQualifiedName~SourceHistoriqueDisqueTests` | ✅ (à étendre) |
| HIS-05 | VM : bascule vers 4 semaines, ‹ › par bloc de 4, libellé de période, étiquettes, épuisée reportée sur l'axe de S | unit | `--filter FullyQualifiedName~HistoriqueViewModelTests` | ✅ (l. 134 à réécrire) |
| HIS-05 | textes : période, jour court, étiquettes, « épuisée … → bloquée jusqu'au reset », pied, sans mot de projection | unit | `--filter FullyQualifiedName~TextesHistoriqueTests` | ✅ |
| HIS-05 | piste : fantômes gris aux opacités des tokens, S en rampe, bande ≥ 1 peinte par `Gris`, trous coupent l'escalier (trace) | WpfFact | `--filter FullyQualifiedName~PistesHistoriqueTests` | ✅ (à étendre) |
| HIS-05 | vue : segment actif, hauteurs des tokens (Measure/Arrange), 4 rangées de couverture, « avant le journal », marqueur, pied, 760 → 1 400 px | WpfFact | `--filter FullyQualifiedName~VueQuatreSemainesBindingTests` | ❌ Wave 0 (35-04) |
| HIS-05 | honnêteté 4 semaines sur objets du scénario (+ fixture épuisée dérivée) | WpfFact | `--filter FullyQualifiedName~HonneteteQuatreSemainesTests` | ❌ (35-04) |
| HIS-05 | tokens : table 38 → 43, aucun littéral | unit + WpfFact | `--filter FullyQualifiedName~GardeTokensHistorique` | ✅ (table) |
| (34 différé) | trou de minuit nommé en Jour, fraîcheur « 1 interruption » | unit + WpfFact | `--filter FullyQualifiedName~LectureVeilleTests|FullyQualifiedName~VueJourBindingTests|FullyQualifiedName~HonneteteHistoriqueTests` | ❌ LectureVeilleTests (35-01) |
| ACC-02 | arbitre : 1 clic → bascule à l'échéance ; 2 clics → ouverture, 0 bascule ; clics lents → 2 bascules | unit | `--filter FullyQualifiedName~ArbitreClicCentreTests` | ❌ (35-02) |
| ACC-02 | VM : `ClicCentre` + `FakeClock` + `EcheanceClicCentre` ; `FakeOuvreurHistorique.Ouvertures` | unit | `--filter FullyQualifiedName~MainViewModelTests` | ✅ |
| ACC-02 | ouvreur : 2 × Ouvrir = 1 fenêtre ; après Close → nouvelle ; minimisée → Normal | WpfFact | `--filter FullyQualifiedName~OuvreurHistoriqueTests` | ❌ (35-02) |
| ACC-02 | garde textuelle : handler transmet `e.ClickCount`, `Handled = true`, drag et clic droit inchangés | unit | `--filter FullyQualifiedName~GardeGestesCadranTests` | ❌ (35-02) |
| ACC-01 | carte F1 : Ouvrir → commande ; puces → `Historique.ChoisirStyleCommand` (même instance que la fenêtre) ; `LigneEtatJournal` / `PastilleJournal` conservés ; « sans interface » absent ; 12 littéraux | WpfFact | `--filter FullyQualifiedName~ReglagesBindingTests|FullyQualifiedName~GardeTokensHistorique` | ✅ |
| ACC-01 | DI : `IOuvreurHistorique` et `HistoriqueViewModel` injectés au `MainViewModel` ; garde textuelle App.xaml.cs | unit | `--filter FullyQualifiedName~CompositionRootTests` | ✅ |
| ACC-01 | `IEtatJournal.JournalOuvertLe` : amorcé hors UI, posé à la 1ʳᵉ écriture, null si inconnu | unit | `--filter FullyQualifiedName~JournalRelevesTests` | ✅ |
| ACC-03 | section : fichiers + tailles, jour « N / 288 » via `LigneFraicheurJour`, 5 derniers événements, reconstruction terminée / en cours, instances ; pas de duplication | unit | `--filter FullyQualifiedName~DiagnosticServiceTests|FullyQualifiedName~EtatJournalHistoriqueTests` | ✅ / ❌ |
| ACC-03 | garde : `.LireJour(` présent, `.Lire(` / `AnalyseReleves.Analyser(` absents du diagnostic | unit | `--filter FullyQualifiedName~GardeDiagnosticHistoriqueTests|FullyQualifiedName~GardesPerimetreTests` | ❌ / ✅ |
| ACC-04 | README section + §9 : mots §4 présents, aucun mot de projection, dernière ligne contient « §8 » | unit | `--filter FullyQualifiedName~GardeDocumentationHistoriqueTests|FullyQualifiedName~ContratAgregatsDocumenteTests|FullyQualifiedName~ContratJournalDocumenteTests` | ❌ / ✅ |
| ACC-04 | version 3.3.0 csproj ↔ assembly | unit | `--filter FullyQualifiedName~VersionPublieeTests` | ✅ |
| ACC-04 | exe publié, VersionInfo, md5, 0 DLL, smoke `--hook` | manuel-scripté (agent) | procédure 32-07 | — |
| VAL-05 | gestes, vues, styles, trou réel, réconciliation, verdict | manuel (utilisateur) + sondes WMI | `35-CONSTAT.md` | ❌ (35-06) |

### Mutations recommandées (par copie, révoquées par copie, sha256 comparés)
- 35-01 : `LireQuatreSemaines` analyse au vrai `now` (sans `InstantDAnalyse`) → faux trou ouvert en fin de S-1 ; `EchelleTemps.Reporter` sur 168 h fixes → test DST ; épuisée sans `Statut7 == Rejete` ; « avant le journal » pour une semaine postérieure sans relevé ; `LectureVeille` sans les événements de la veille → cause « inconnue » au lieu d'« arrêté » ; `DemarrerHorloge` sans réabonnement.
- 35-02 : arbitre qui bascule au premier clic ; `ClickCount >= 2` qui n'annule pas ; ouvreur qui crée une fenêtre à chaque appel ; puce de la carte liée à une autre commande.
- 35-03 : section sans `LigneFraicheurJour` (texte recopié) ; `TakeLast(4)` ; seconde lecture de la table des processus.
- 35-04 : fantôme peint par la rampe ; opacité S-1 liée au token de S-3 ; bande épuisée par `ArcBrush(1.0)` ; `OpaciteSemaine1="0.8"` littéral (garde) ; infobulle non bornée.
- 35-05 : « estimation » ajouté à la section README ; ligne finale sans « §8 ».

### Sampling Rate
- **Per task commit :** filtre du plan (≤ 30 s).
- **Per wave merge :** suite complète deux fois sur l'arbre réel + build Release 0 avertissement (orchestrateur).
- **Phase gate :** suite complète verte deux fois avant 35-06 ; `/gsd:verify-work` après le constat.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/Fakes/FakeOuvreurHistorique.cs` — compteur `Ouvertures` (35-02)
- [ ] `tests/Chronos.Tests/ArbitreClicCentreTests.cs`, `OuvreurHistoriqueTests.cs`, `GardeGestesCadranTests.cs` (35-02)
- [ ] `tests/Chronos.Tests/LectureVeilleTests.cs` (35-01)
- [ ] `tests/Chronos.Tests/EtatJournalHistoriqueTests.cs`, `GardeDiagnosticHistoriqueTests.cs` (35-03)
- [ ] `tests/Chronos.Tests/VueQuatreSemainesBindingTests.cs`, `HonneteteQuatreSemainesTests.cs` (35-04)
- [ ] `tests/Chronos.Tests/GardeDocumentationHistoriqueTests.cs` (35-05)
- Framework : aucun ajout.

## Sources

### Primary (HIGH confidence — code réel à `5bb415f`, lu en lecture seule)
- `src/Chronos/Views/MainWindow.xaml(.cs)` (CentreHit, drag, clic droit), `ViewModels/MainViewModel.cs` (ctor à paramètres optionnels, `ToggleCenterMode`, `MajTexteEtatJournal`), `Views/SettingsWindow.xaml(.cs)` (DONNÉES, carte 32-05, popover), `App.xaml.cs` (DI Historique, `--historique`, verrou, diagnostic)
- `ViewModels/Historique/HistoriqueViewModel.cs` (abonnement / désabonnement `Changement`, `Theme` au ctor, `ChoisirVue` no-op), `Views/Historique/HistoriqueWindow.xaml(.cs)`, `HistoriqueGalerie.cs`
- `Services/Historique/{BornesPlage,AnalyseReleves,LecteurJournal,SourceHistoriqueDisque,ISourceHistorique,ReglagesHistorique,IEtatJournal,JournalReleves,JournalisationUsageProvider}.cs`, `Models/Historique/{LectureJournal,DonneesHistorique}.cs`, `Text/TextesHistorique.cs`, `Controls/Historique/{PisteBase,PisteNiveau}.cs`
- `Services/DiagnosticService.cs` (ctor, `[Magasins persistants]`, `LigneMagasin`)
- Tests : `GardeTokensHistoriqueTests` (table des 38), `GardeVocabulaireHistoriqueTests`, `GardesPerimetreTests` (garde `.Lire(`), `ContratAgregatsDocumenteTests` (ligne finale « §8 »), `CompositionRootTests` (miroir Historique), `ReglagesBindingTests`, `VueJourBindingTests` / `VueSemaineBindingTests` (infobulle), `HonneteteHistoriqueTests` (minuit), `ScenariosHistoriqueTests` (S-1 ∈ [0,40 ; 0,60])
- SUMMARY 32-05, 32-07, 32-CONSTAT, 33-05, 34-01 … 34-08, 34-VERIFICATION ; `.zeus/DESIGN_PLAN.md` ; `.zeus/DOD.md` ; `.zeus/state.json`
- Relevés machine du 2026-09-27 : `dotnet --version` = 10.0.201 ; `Get-CimInstance Win32_Process` (4 overlays) ; `ls Chronos-v*.exe`

### Secondary (MEDIUM)
- Comportement WPF `MouseButtonEventArgs.ClickCount` (délai + rectangle système), `Window` non ré-affichable après `Close`, `GetDoubleClickTime` (user32) — connaissance de plateforme stable, cohérente avec l'usage déjà fait du code (`DragMove`, `Handled`) ; non re-vérifiée en ligne dans cette session.

### Tertiary (LOW)
- Valeurs de mise en page de la frame E (hauteur NIVEAU, largeur des étiquettes) : non lues (Figma inaccessible à cet agent).

## Metadata

**Confidence breakdown :**
- Standard stack : HIGH — aucun ajout ; versions lues dans les csproj et la machine.
- Architecture : HIGH — chaque brique citée est lue dans le code ; la piste dédiée et l'arbitre sont des compositions de fonctions déjà testées.
- Pitfalls : HIGH — les 12 pièges sont des faits du code / de la machine (gardes textuelles, désabonnement, thème, E1-ter relevé), sauf la course temporisation/second clic (MEDIUM, bornée par l'arbitre).

**Research date :** 2026-09-27
**Valid until :** fin de la phase 35 (code en mouvement rapide : relire la matrice de propriété si un plan de 34 est rouvert).
