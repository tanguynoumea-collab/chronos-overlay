# Phase 20: Honnêteté visible — cadran & diagnostic - Context

**Gathered:** 2026-09-12
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

**Dernière phase du milestone v1.5.** Ce que la doctrine sait, l'utilisateur le voit : le cadran distingue à
l'œil un chiffre exact frais, un chiffre exact daté et un état indisponible, et le diagnostic nomme la source
qui alimente réellement l'affichage ainsi que son ancienneté.

Exigences couvertes : **EXA-03** (distinction visuelle des trois états ; `IsStale`, calculé mais bindé nulle
part, devient un signal réel à l'écran) et **EXA-06** (le diagnostic indique quelle source alimente
l'affichage, et depuis quand).

**C'est la seule phase du milestone dont le livrable est majoritairement visuel.** Les phases 16 à 19 ont
livré la doctrine et son état ; ici on le rend lisible.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
Tous les choix d'implémentation sont à la discrétion de Claude — la discussion est désactivée
(`workflow.skip_discuss=true`), et `workflow.ui_phase=false` (pas de contrat UI séparé).

### Loi d'encodage du cadran — contrainte de design ÉTABLIE, à respecter
Le projet a une loi d'encodage décidée lors de la refonte visuelle (idéation llm-council) :
**temps = géométrie, quota = luminance.** Toute nouvelle information visuelle doit s'y conformer plutôt que
d'inventer un troisième canal. Les tokens de design sont dans `src/Chronos/Resources/DesignTokens.xaml` et
`src/Chronos/Theming/ChronosTheme.cs`.

### Contrainte imposée par la phase 19, non négociable
**DEL-04 : pas d'arc de delta, pas de barre d'erreur.** Le plancher n'est pas un nombre augmenté d'une marge :
« 42 % » devient « ≥ 42 % », incertitude **unilatérale**. Mesure à l'appui : 643 649 933 tokens sur 5 h contre
l'ancien plafond de 230 000 000 = 280 % — un delta chiffré serait une fiction. La sémiologie doit donc
exprimer « au moins », pas « environ ± x ».

### Surface réelle à couvrir
L'overlay est **compact (170 px)**, sans barre de titre, sur fenêtre transparente. Il existe :
- **2 modes de cadran** (Normal épuré par défaut / Étendu 3 anneaux),
- **5 styles de cadran** (Arcs, Braises, Fusible, Marée, Volets),
- **3 thèmes de couleur**.
Toute distinction visuelle doit rester lisible dans **toutes** ces combinaisons, sans casser la compacité ni
les tokens de design validés. Deux pastilles occupent déjà le coin bas-droite du `Grid` racine (déconnexion,
invitation à se connecter) et sont mutuellement exclusives.

### Contraintes projet
- MVVM strict : aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`). Le signal visuel
  passe par les ViewModels.
- `Transparent` EST hit-testable en WPF ; seul `{x:Null}` ne l'est pas (pas besoin de `#01000000`).
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin, aucune dépendance native.
- **Ne JAMAIS présenter une estimation comme un chiffre exact** — Core Value du projet, et raison d'être de
  tout ce milestone.
- UI et commentaires en **français**.
- **Baseline à l'entrée de phase : 699 tests xUnit verts.**

### SÉCURITÉ
- Aucune requête réseau réelle depuis un test.
- Ne JAMAIS appeler l'endpoint de refresh avec le refresh token RÉEL de `%APPDATA%\Chronos\oauth.dat` —
  contrôle : **518 octets, mtime 1783863147**.
- Aucun test n'écrit dans le vrai `%APPDATA%\Chronos\`.
- **Ne PAS lancer l'overlay** sans supervision : cela déclencherait la purge des 25 hooks de
  `~/.claude/settings.json` codée en phase 15.
- Les fichiers SOURCE contenant `RefreshAsync` doivent rester exactement 2.
- **Ne pas binder quoi que ce soit sur `LoginClaudeCommand`** : elle BASCULE, et un clic supprimerait le
  coffre de jetons. `ReconnecterCommand` est la commande dédiée.

</decisions>

<code_context>
## Existing Code Insights

### Les onze dettes consolidées par la phase 19 — matière première de cette phase
1. **`TokensText` / `HasTokens` sont calculés, testés et bindés NULLE PART** (0 occurrence dans les XAML) :
   le « centre épuré » de la v1.3 avait retiré la ligne de tokens. La matière brute de DEL-04 est correcte et
   **invisible**.
2. **`DataUnavailable` n'est bindé nulle part non plus** : le mot « indisponible » comme label explicite à
   l'écran relève d'EXA-03, donc de cette phase.
3. **`IsStale` (`MainViewModel.cs`) est calculé avec un seuil de 2 min et bindé nulle part** — alors que la
   limite d'âge de la doctrine est d'environ **6 min** (dérivée de `CadenceNominale`). **Incohérence à
   trancher ici** : deux notions de « périmé » coexistent, il ne doit en rester qu'une.
4. **`ProvenanceReleve` n'a que 3 membres, tous des ÉTATS** : le **nom** de la source alimentant l'affichage
   n'est porté par aucun champ. C'est la moitié manquante d'EXA-06 — à livrer ici.
5. **Trou visuel dans la `UniformGrid` des réglages** : 3 boutons dans une grille à `Columns="2"`, cellule
   bas-droite vide depuis le retrait du bouton « Plafonds… » en phase 16. Passer à `Columns="3"` tronquerait
   les libellés (panneau 330 px, « Recalibrer hebdo… » demande ~105 px à `FontSize=12`). Piste : grille 2×2
   avec « Diagnostic… » en `ColumnSpan=2`.
6. **Dette de durée des tests** : la suite est passée de 56 s à **plus de 2 min** parce que chaque test de
   `DiagnosticServiceTests` exécute `BuildReportAsync`, qui balaie `%APPDATA%` / `%LOCALAPPDATA%` et fait un
   **poll UIA réel** — non injectables sans changer la signature de `DiagnosticService` (10 sites de
   construction), ce que les phases 18 et 19 s'interdisaient. Cette phase peut la lever.
7. **`SourceReliability.Estimated` a été réaffecté au plancher** en phase 19 ; `EstimatedTokens` est mort.
8. Les autres dettes sont listées dans `.planning/phases/19-nouvelle-doctrine-du-composite/19-05-SUMMARY.md`
   et `deferred-items.md`.

### Fichiers concernés
- `src/Chronos/ViewModels/MainViewModel.cs` (`IsStale`, `DataUnavailable`, pastilles, commandes),
  `WindowGaugeViewModel.cs` (`UtilizationText`, `TokensText`, `HasTokens`, provenance).
- `src/Chronos/Views/MainWindow.xaml` (overlay, 170×170, deux pastilles déjà présentes en bas-droite).
- `src/Chronos/Views/Cadrans/CadranBraisesView.xaml`, `CadranFusibleView.xaml`, `CadranMareeView.xaml`,
  `CadranVoletsView.xaml` — les 4 styles alternatifs, plus les arcs par défaut.
- `src/Chronos/Controls/RingArc.cs`, `TickRing.cs`, `Controls/Cadrans/*.cs`.
- `src/Chronos/Resources/DesignTokens.xaml`, `src/Chronos/Theming/ChronosTheme.cs` (3 thèmes).
- `src/Chronos/Views/SettingsWindow.xaml` (la `UniformGrid` à corriger).
- `src/Chronos/Services/DiagnosticService.cs` (EXA-06 ; signature gelée jusqu'ici — cette phase peut la
  changer si elle adapte les 10 sites dans la même tâche).
- `src/Chronos/Services/DoctrineFraicheur.cs`, `LastExactUsageProvider.cs` (d'où vient la provenance).
- Tests : `CadranBindingTests.cs`, `WindowGaugeViewModelTests.cs`, `MainViewModelTests.cs`,
  `ThemingTests.cs`, `DiagnosticServiceTests.cs`.

### Gardes à ne pas casser
`ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`, `GardesDoctrineTests`,
et la garde de position de la sonde `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` (phase 18).
Une flakiness du chargeur BAML de WPF a été corrigée en phase 16 par une collection xUnit
`DisableParallelization` sur les classes chargeant du BAML : **toute nouvelle classe de test chargeant du
XAML doit y être rattachée**, et la suite doit être exécutée **deux fois** avant de conclure.

</code_context>

<specifics>
## Specific Ideas

Le cœur de cette phase est une question de **sémiologie**, pas de plomberie : comment dire « ce chiffre est
frais », « ce chiffre est daté mais toujours vrai », « ce chiffre est un plancher », « je ne sais pas » —
d'un coup d'œil, sur 170 px, dans 5 styles × 3 thèmes × 2 modes, sans ajouter de bruit.

La doctrine de la phase 19 distingue quatre situations ; l'exigence EXA-03 en nomme trois (frais / daté /
indisponible). Le planner devra décider si le **plancher** (« ≥ N % ») est une quatrième apparence ou s'il se
confond avec « daté ». Argument à trancher : un plancher n'est pas daté — il est *incomplet vers le haut*.

</specifics>

<deferred>
## Deferred Ideas

- **Vérifications manuelles héritées, à présenter à l'utilisateur en fin de milestone** : la bascule visuelle
  réelle (phase 19), le 429 réel portant les en-têtes (phase 18, HDR-02), le parcours de reconnexion de bout
  en bout et le contraste des pastilles sur fond d'écran réel (phase 17).
- **Piste d'économie** : si `/api/oauth/usage` porte lui aussi la famille d'en-têtes `unified`, le coût de la
  sonde (≈ 288 micro-requêtes/jour) peut être supprimé. Le diagnostic liste désormais les noms d'en-têtes
  reçus — la réponse viendra au premier rafraîchissement réussi.
- **Préavis avant saturation (~90 %) et notification au reset** — Future Requirements, hors v1.5.

</deferred>
