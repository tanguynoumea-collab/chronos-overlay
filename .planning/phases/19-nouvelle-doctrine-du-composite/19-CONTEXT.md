# Phase 19: Nouvelle doctrine du composite - Context

**Gathered:** 2026-09-12
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

**C'est le cœur du milestone v1.5.** Chronos n'affiche plus jamais qu'un chiffre exact, éventuellement
corrigé d'un delta borné et marqué comme tel, ou rien du tout :

> **exact frais → dernier exact persisté encore rigoureusement valide → dernier exact + delta borné avec sa
> marge → indisponible.**

Exigences couvertes : EXA-02 (limite d'âge sur toute source exacte), EXA-04 (plus aucune utilization absolue
dérivée d'un comptage de tokens), EXA-05 (aucun chiffre exact jamais obtenu → « indisponible » + invitation à
se connecter, jamais un pourcentage), DEL-03 (sans activité depuis le dernier relevé exact, celui-ci est
encore exact), DEL-04 (avec activité, « dernier exact + delta » marqué avec sa marge).

**Hors périmètre :** la distinction VISUELLE frais / daté / indisponible au cadran et le diagnostic qui nomme
la source (EXA-03, EXA-06) sont la **phase 20**. Ici on livre la DOCTRINE et l'état qu'elle produit ; la
phase 20 le rend lisible à l'œil.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
Tous les choix d'implémentation sont à la discrétion de Claude — la discussion est désactivée
(`workflow.skip_discuss=true`).

### Le défaut central à corriger (diagnostic du 2026-09-09, ne pas ré-enquêter)
`CompositeUsageProvider.Best()` classe **uniquement par fiabilité** (`Exact` > `Estimated` > `Unavailable`) :
une donnée marquée `Exact` mais vieille de deux mois bat donc une donnée fraîche. C'est ce qui a fait afficher
« 10 % » pendant deux mois à partir d'un `usage.json` figé au 2026-07-10, sans le moindre signal.
`ClaudeUsageObjectProvider` n'applique **aucune limite d'âge** et marque `Exact` ce qu'il lit, quel que soit
l'âge du fichier.

### La règle de fraîcheur, dans l'ordre
1. **Exact frais** — relevé dont l'âge est sous la limite : affiché tel quel.
2. **Dernier exact persisté, encore rigoureusement valide** — si **aucune réponse assistant n'est apparue
   dans les transcripts depuis l'horodatage du relevé**, alors l'utilisation n'a objectivement pas bougé :
   ce relevé est **encore exact**, pas périmé. C'est l'idée centrale du milestone.
3. **Dernier exact + delta borné** — s'il y a eu de l'activité depuis, afficher le relevé augmenté du delta
   estimé, **marqué avec sa marge d'incertitude** et visuellement distinct d'un relevé exact.
4. **Indisponible** — sinon. Et si aucun chiffre exact n'a **jamais** été obtenu : « indisponible » +
   invitation à se connecter, **jamais un pourcentage**.

### Contrainte d'honnêteté (Core Value du projet)
Ne JAMAIS présenter une estimation comme un chiffre exact. Le delta de l'étape 3 n'est PAS exact : il doit
porter sa marge, et le modèle doit permettre à la phase 20 de le distinguer à l'œil.
Corollaire déjà acquis : `tokens / plafond` est définitivement supprimé (phase 16) — ne pas le réintroduire
sous une autre forme.

### Borne du delta — piège connu
`TranscriptActivityProvider` expose un `Horizon` : le filtre `mtime ≥ now − 8 j` fait qu'un `since` plus
ancien produirait un delta **silencieusement sous-évalué**. La doctrine doit consulter `Horizon` / `Covers`
et refuser de produire un delta qu'elle ne peut pas garantir, plutôt que d'en inventer un.

Second piège : le delta doit être **borné par fenêtre**. Les fenêtres 5 h et 7 j ont des `resets_at`
distincts ; un delta global depuis T sur-compterait si la fenêtre 5 h a roulé entre-temps.

### Contraintes projet
- MVVM strict, DI, dossiers Models/Views/ViewModels/Services.
- Aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`).
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin, aucune dépendance native.
- Dégradation totale : aucune source disponible ≠ crash → état « données indisponibles ».
- **Aucune requête réseau réelle depuis un test.** Ne JAMAIS appeler l'endpoint de refresh avec le refresh
  token RÉEL de `%APPDATA%\Chronos\oauth.dat` : contrôle avant/après, **518 octets, mtime 1783863147**.
  Aucun test n'écrit dans le vrai `%APPDATA%\Chronos\`.
- **UN SEUL RAFRAÎCHISSEUR** : les fichiers SOURCE contenant `RefreshAsync` doivent rester exactement 2
  (`ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs`).
- **Compilabilité à chaque commit** : `tests/Chronos.Tests` a un `ProjectReference` vers `Chronos`, donc une
  erreur de compilation où que ce soit fait échouer TOUTE l'invocation `dotnet test`. Une tâche qui change une
  signature adapte ses sites d'appel dans la MÊME tâche.
- UI et commentaires en **français**.
- **Baseline à l'entrée de phase : 652 tests xUnit verts.**

### C'est LA phase qui a le droit de modifier `CompositeUsageProvider.cs`
Les phases 16, 17 et 18 se l'interdisaient explicitement, précisément pour que cette refonte parte d'une base
intacte. Vérifié : aucun commit des phases 16-18 ne l'a touché. `UsageSnapshot.cs` était également interdit ;
cette phase peut le toucher si nécessaire — **mais avec prudence** : la phase 18 a établi que
`CompositeUsageProvider.GetAsync` reconstruit le record par `new UsageSnapshot { FiveHour, SevenDay,
SourceCapturedAt }` et **non** par `with`, donc tout champ ajouté était détruit au passage. Si cette phase
ajoute un champ, elle doit corriger la reconstruction en même temps.

</decisions>

<code_context>
## Existing Code Insights

### Le fichier à refondre
`src/Chronos/Services/CompositeUsageProvider.cs` — `Best(primary, fallback)` compare des rangs de fiabilité
(`Rank`) et rien d'autre. `GetAsync` appelle les deux providers, prend la meilleure source **par fenêtre**,
et recompose `SourceCapturedAt` selon qu'une fenêtre vient du primaire. La chaîne réelle est composée de
**trois composites imbriqués** dans `App.xaml.cs`, sous le décorateur `LastExactUsageProvider`.

### Les briques livrées par les phases précédentes, à CONSOMMER
- `src/Chronos/Services/LastExactStore.cs` + `LastExactUsageProvider.cs` (phase 16) — persistance du dernier
  relevé exact, avec `captured_at` **par fenêtre** ; `Reliability` non persistée (dérivée) et
  `FractionTimeRemaining` recalculée, pour qu'un fichier corrompu ne puisse injecter un faux `Exact`.
  `SchemaVersion` = 1 : **3 fixtures épinglent `"version":1` / `"version":999`**, un bump les casserait.
- `src/Chronos/Services/ITranscriptActivitySource.cs` + `TranscriptActivityProvider.cs` (phase 16) —
  `ReadAsync()` (une seule passe disque) → `TranscriptActivityLog` → `Since(t)` **PUR** et appelable N fois,
  plus `Horizon` / `Covers`. Ne produit AUCUN pourcentage. C'est la source du delta.
- `src/Chronos/Services/UsageNormalization.cs` (phase 18) — **point unique** de conversion, avec plancher
  d'epoch à 2020-01-01 (c'est lui qui neutralise le `resets_at: 9` réel). Une garde balaie le texte source
  et échoue si une conversion divergente est réintroduite : **toute conversion passe par là**.
- `src/Chronos/Services/RateLimitHeaderUsageProvider.cs` (phase 18) — la sonde, **`primary` de la chaîne
  exacte**. Sa position est gardée par un test de comportement falsifiable : ne pas la déplacer sans
  comprendre que `Best()` n'écarte le fallback que s'il est STRICTEMENT moins fiable.
- `src/Chronos/Models/StatutServeur.cs`, `EtatDepassement.cs`, `IEtatServeur.cs` (phase 18) — statut serveur
  et dépassement, portés par `WindowState` (qui survit à `Best()` par référence) et par un canal latéral.
- `src/Chronos/Services/ChronosTokenAuthority.cs` + `IAuthStatus` (phase 17) — autorité unique du jeton,
  état d'authentification.

### Modèles
`src/Chronos/Models/UsageSnapshot.cs`, `WindowState.cs` (porte `Utilization`, `ResetsAt`, `Reliability`,
`FractionTimeRemaining`, `EstimatedTokens`, `CapturedAt`, le statut serveur et le dépassement),
`SourceReliability.cs` (`Exact` / `Estimated` / `Unavailable`), `WindowKind.cs`.

`SourceReliability` n'a aujourd'hui que trois valeurs. La doctrine en distingue quatre états
(frais / encore valide / corrigé par delta / indisponible) : une décision de modélisation est nécessaire —
le planner tranchera, en gardant à l'esprit que la phase 20 devra pouvoir les distinguer à l'œil et que
21 sites `new WindowState` existent dans les tests (tout nouveau champ reste `init` et nullable, sans
`required`).

### Autres consommateurs à ne pas casser
`src/Chronos/ViewModels/MainViewModel.cs` (porte `IsStale`, calculé mais **bindé nulle part** — c'est EXA-03,
phase 20), `WindowGaugeViewModel.cs`, `src/Chronos/Services/RefreshOrchestrator.cs`,
`src/Chronos/Services/DiagnosticService.cs` (**signature gelée** : 10 sites de construction ; tout nouveau
paramètre est optionnel et en dernière position), `src/Chronos/Services/WeeklyRecalibration.cs` (agit aussi
sur une fenêtre `Unavailable`).

</code_context>

<specifics>
## Specific Ideas

La doctrine doit être **testable de façon déterministe** sur ses quatre branches, avec une horloge injectée
(`FakeClock` existe). Chaque branche doit avoir au moins un test qui tombe si la branche est supprimée.

</specifics>

<deferred>
## Deferred Ideas

- **EXA-03 et EXA-06 → phase 20** : distinction visuelle frais / daté / indisponible au cadran, et diagnostic
  nommant la source réellement affichée et son âge.
- **Trou visuel dans la `UniformGrid` des réglages** (3 boutons, `Columns="2"`, cellule bas-droite vide)
  hérité de la phase 16 → phase 20.
- **Dette de durée des tests** : `DiagnosticServiceTests` fait passer la suite de 56 s à ~2 min 35 parce que
  chaque test exécute `BuildReportAsync`, qui balaie `%APPDATA%` / `%LOCALAPPDATA%` et fait un **poll UIA
  réel**, non injectables sans changer la signature de `DiagnosticService` → candidat phase 20.
- **HDR-02 en production** : qu'un 429 réel porte bien les en-têtes `anthropic-ratelimit-unified-*` n'est pas
  vérifiable sans reconnexion de l'utilisateur et compte saturé. Protocole dans le SUMMARY du plan 18-06.
- **Piste d'économie** : si `/api/oauth/usage` porte lui aussi la famille `unified`, le coût de la sonde
  (≈ 288 micro-requêtes/jour) peut être supprimé. Le diagnostic liste désormais les noms d'en-têtes reçus.

</deferred>
