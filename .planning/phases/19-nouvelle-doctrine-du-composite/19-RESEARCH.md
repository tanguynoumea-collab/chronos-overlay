# Phase 19 : Nouvelle doctrine du composite — Research

**Researched:** 2026-09-12
**Domain:** Architecture de doctrine de fraîcheur dans une chaîne de providers C#/.NET 8 — aucun nouveau paquet
**Confidence:** HIGH (tout est vérifié par lecture du code réel + 4 mesures sur la machine de l'utilisateur)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Claude's Discretion
Tous les choix d'implémentation sont à la discrétion de Claude — la discussion est désactivée
(`workflow.skip_discuss=true`).

### Locked Decisions (copiées verbatim du CONTEXT)

**Le défaut central à corriger (diagnostic du 2026-09-09, ne pas ré-enquêter)**
`CompositeUsageProvider.Best()` classe **uniquement par fiabilité** (`Exact` > `Estimated` > `Unavailable`) :
une donnée marquée `Exact` mais vieille de deux mois bat donc une donnée fraîche. C'est ce qui a fait afficher
« 10 % » pendant deux mois à partir d'un `usage.json` figé au 2026-07-10, sans le moindre signal.
`ClaudeUsageObjectProvider` n'applique **aucune limite d'âge** et marque `Exact` ce qu'il lit, quel que soit
l'âge du fichier.

**La règle de fraîcheur, dans l'ordre**
1. **Exact frais** — relevé dont l'âge est sous la limite : affiché tel quel.
2. **Dernier exact persisté, encore rigoureusement valide** — si **aucune réponse assistant n'est apparue
   dans les transcripts depuis l'horodatage du relevé**, alors l'utilisation n'a objectivement pas bougé :
   ce relevé est **encore exact**, pas périmé. C'est l'idée centrale du milestone.
3. **Dernier exact + delta borné** — s'il y a eu de l'activité depuis, afficher le relevé augmenté du delta
   estimé, **marqué avec sa marge d'incertitude** et visuellement distinct d'un relevé exact.
4. **Indisponible** — sinon. Et si aucun chiffre exact n'a **jamais** été obtenu : « indisponible » +
   invitation à se connecter, **jamais un pourcentage**.

**Contrainte d'honnêteté (Core Value du projet)**
Ne JAMAIS présenter une estimation comme un chiffre exact. Le delta de l'étape 3 n'est PAS exact : il doit
porter sa marge, et le modèle doit permettre à la phase 20 de le distinguer à l'œil.
Corollaire déjà acquis : `tokens / plafond` est définitivement supprimé (phase 16) — ne pas le réintroduire
sous une autre forme.

**Borne du delta — piège connu**
`TranscriptActivityProvider` expose un `Horizon` : le filtre `mtime ≥ now − 8 j` fait qu'un `since` plus
ancien produirait un delta **silencieusement sous-évalué**. La doctrine doit consulter `Horizon` / `Covers`
et refuser de produire un delta qu'elle ne peut pas garantir, plutôt que d'en inventer un.
Second piège : le delta doit être **borné par fenêtre**. Les fenêtres 5 h et 7 j ont des `resets_at`
distincts ; un delta global depuis T sur-compterait si la fenêtre 5 h a roulé entre-temps.

**Contraintes projet**
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

**C'est LA phase qui a le droit de modifier `CompositeUsageProvider.cs`** (et `UsageSnapshot.cs`, avec
prudence : `GetAsync` reconstruit le record par `new` et non par `with`, donc tout champ ajouté est détruit
au passage — si cette phase ajoute un champ, elle doit corriger la reconstruction en même temps).

**Testabilité déterministe** : les quatre branches doivent être vérifiables avec une horloge injectée
(`FakeClock` existe). Chaque branche doit avoir au moins un test qui tombe si la branche est supprimée.

### Deferred Ideas (OUT OF SCOPE)
- **EXA-03 et EXA-06 → phase 20** : distinction visuelle frais / daté / indisponible au cadran, et diagnostic
  nommant la source réellement affichée et son âge.
- **Trou visuel dans la `UniformGrid` des réglages** (3 boutons, `Columns="2"`, cellule bas-droite vide) → phase 20.
- **Dette de durée des tests** : `DiagnosticServiceTests` fait passer la suite de 56 s à ~2 min 35 → phase 20.
- **HDR-02 en production** : qu'un 429 réel porte bien les en-têtes `anthropic-ratelimit-unified-*` n'est pas
  vérifiable sans reconnexion + compte saturé.
- **Piste d'économie** : si `/api/oauth/usage` porte aussi la famille `unified`, le coût de la sonde peut être supprimé.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **EXA-02** | Au-delà d'un **âge maximal**, une source exacte cesse d'être présentée comme exacte. | Pattern 2 (porte de certifiabilité) + Pattern 3 (valeur de la limite, **dérivée** de `CadenceNominale`) + Pitfall 1 (`CapturedAt` est `null` dans 3 des 4 providers exacts — la limite est inapplicable sans ce correctif). |
| **EXA-04** | Aucune utilization absolue dérivée d'un comptage de tokens n'est plus jamais affichée. | Pattern 5 (le delta est un **plancher unilatéral**, jamais une conversion) + Open Question 4 + mesure terrain : **643 649 933 tokens en 5 h** contre l'ancien plafond 230 000 000 → toute conversion est fausse d'un facteur > 2,8. Garde de non-retour proposée. |
| **EXA-05** | Aucun chiffre exact jamais obtenu → « indisponible » + invitation à se connecter, jamais un pourcentage. | Pattern 6 (bit `UnExactADejaEteObtenu`, dérivé du magasin persistant) + constat terrain : `last-exact.json` **n'existe pas** sur la machine réelle → c'est l'état courant, donc directement vérifiable. |
| **DEL-03** | Sans activité depuis le dernier relevé exact, ce relevé est présenté comme **encore exact**. | Pattern 4 (ordre des règles : la limite d'âge est un laissez-passer, DEL-03 est une **ré-habilitation** au-delà) + `TranscriptActivityLog.Since` / `Covers` déjà livrés. |
| **DEL-04** | Avec activité, « dernier exact + delta », **marqué avec sa marge**. | Pattern 5 (plancher « ≥ X % » + tokens bruts non convertis) + Pattern 1 (modélisation des 4 états) + ce que cela impose à la phase 20. |
</phase_requirements>

---

## Project Constraints (from CLAUDE.md)

| Directive CLAUDE.md | Conséquence pour ce plan |
|---|---|
| C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm) + MS.Extensions.DI — **imposé** | Aucun nouveau paquet. Tout se fait avec `System.*` et les types déjà présents. |
| MVVM strict, `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers Models/Views/ViewModels/Services | Les nouveaux types de doctrine vont dans `Services/` (logique) et `Models/` (état). Le signal EXA-05 passe par un `[ObservableProperty]` de `MainViewModel`. |
| **Aucun type WPF dans `Services/` ni `Models/`** (`ServicesLayerPurityTests`) | La doctrine est une classe **pure** (aucun `System.Windows.*`). Le test balaie les signatures publiques des deux namespaces — un nouveau type y est automatiquement couvert. |
| Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin | Aucun nouveau chemin : `LastExactStore` (injecté) et `ChronosPaths.ProjectsRoot` suffisent. |
| `utilization`/`resets_at` prioritaires sur le comptage de tokens ; **ne jamais présenter une estimation comme exacte** | C'est littéralement le sujet de la phase. Contrainte dure sur Open Question 4 : pas de tokens → points de pourcentage. |
| Robustesse : aucune source ≠ crash → état « données indisponibles » ; parsing tolérant | La doctrine ne doit **jamais lever** : une panne du magasin ou de la source de transcripts dégrade vers « indisponible ». `LastExactUsageProvider` a déjà un `try/catch` englobant à étendre. |
| Reset hebdo best-effort et recalibrable | Voir Open Question 8 : `WeeklyRecalibration` doit cesser de tester la fiabilité et tester le **`ResetsAt`** — sinon il écrasera le reset réel d'une fenêtre corrigée par delta. |
| **UI et commentaires en français** | Nommage français pour tout nouveau type (`DoctrineFraicheur`, `ProvenanceReleve`, `SourceActiviteMemoisee`…), cohérent avec les phases 17-18. |
| `PublishTrimmed=false`, pas de réflexion risquée en prod | La réflexion n'est utilisée que dans les **tests** (gardes). Aucun impact mono-fichier. |
| Pas de `Assembly.Location` | Les gardes de balayage de texte source utilisent `AssemblyMetadata("CheminSourcesChronos")` déjà injecté par MSBuild dans le csproj de test (précédent phase 18). |

---

## Summary

La phase 19 n'est **pas** un travail d'algorithmique mais un travail de **placement de connaissance**. Trois
faits établis par la lecture du code décident presque tout :

1. **Le composite n'a ni horloge, ni magasin, ni source de transcripts.** `Best()` travaille par fenêtre, sur
   des `WindowState` nus, et il est instancié **trois fois de façon imbriquée** dans `App.xaml.cs`. Y faire
   entrer la doctrine exige d'y injecter `IClock` + une limite + (pour DEL-03/04) le magasin et les
   transcripts — soit **22 sites de construction** à retoucher, et trois exécutions de la doctrine par tick.
   Le décorateur `LastExactUsageProvider`, lui, est **déjà** en tête de chaîne, **déjà** porteur de `IClock`
   et du `LastExactStore`, et **déjà** l'unique écrivain du magasin. C'est là que la doctrine doit vivre.

2. **`WindowState.CapturedAt` n'est renseigné que par UN des quatre providers exacts** (la sonde d'en-têtes).
   `ChronosOAuthUsageProvider`, `ClaudeOAuthUsageProvider` et `ClaudeUsageObjectProvider` ne posent que
   `UsageSnapshot.SourceCapturedAt` (niveau snapshot) et laissent `CapturedAt` à `null`. Conséquence : **la
   limite d'âge d'EXA-02 est aujourd'hui inapplicable par fenêtre**, et le magasin persiste des relevés
   `captured_at: null` qui sont définitivement incertifiables. Cela doit être corrigé dans cette phase, et
   c'est le plus gros risque de régression silencieuse (voir Pitfall 1 : le mauvais correctif ressuscite le
   bug des « 10 % »).

3. **Une passe de transcripts coûte 2,7 à 3,2 s et lit 536 Mo sur la machine réelle** (mesuré, voir
   § Vérifications empiriques). Avec un tick de 60 s, consulter les transcripts à chaque `GetAsync` est exclu.
   La doctrine doit donc avoir un **chemin rapide** (relevé manifestement frais → aucune E/S) et la source
   d'activité doit être **mémoïsée** derrière un décorateur à TTL. C'est ce qui donne sa vraie raison d'être à
   la limite d'âge d'EXA-02 : ce n'est pas un jugement de valeur sur la donnée, c'est le **régulateur de
   débit** de la passe disque. D'où une valeur **dérivée** de `RateLimitHeaderUsageProvider.CadenceNominale`
   et non choisie au doigt mouillé.

Sur le fond doctrinal, la question la plus délicate (DEL-04 : comment convertir des tokens en points de
pourcentage) se résout par un refus argumenté : **elle n'a pas de réponse honnête, et elle n'a pas besoin
d'en avoir**. L'utilisation est monotone croissante à l'intérieur d'une fenêtre ; le dernier relevé exact est
donc une **borne inférieure rigoureuse** tant que la fenêtre n'a pas roulé. Le delta n'est pas un nombre à
ajouter, c'est un **changement de nature du chiffre** : on passe de « 42 % » à « **au moins** 42 % », on
affiche les tokens bruts comme matière première non convertie, et on laisse la phase 20 dessiner une
incertitude **unilatérale**. Le chiffre ne bouge jamais vers le haut sans preuve ; seule sa qualification
change. C'est la seule forme qui respecte à la fois DEL-04 (« marqué avec sa marge ») et EXA-04 (« aucune
utilization absolue dérivée d'un comptage de tokens »).

**Primary recommendation :** une classe **pure** `DoctrineFraicheur` (Services/, statique, sans E/S, sans
horloge propre) qui décide les 4 branches à partir de (fenêtre vivante, fenêtre mémorisée, journal d'activité,
`now`, limite) ; consommée par **`LastExactUsageProvider` promu en couche de doctrine** (nom conservé pour ne
pas casser les deux `Assert.IsType` de `CompositionRootTests`) ; `CompositeUsageProvider.cs` reçoit une
modification **minimale et justifiée** (une garde de recomposition, pas une injection d'horloge) ; les 3
providers exacts sans `CapturedAt` sont corrigés ; `ITranscriptActivitySource` est mémoïsé par un décorateur.

---

## Réponses aux 8 questions ouvertes

| # | Question | Réponse tranchée | Où c'est argumenté |
|---|---|---|---|
| 1 | Modélisation des 4 états | **`SourceReliability` reste à 3 valeurs**, `Estimated` est **réaffecté** à « plancher corrigé par delta » (il est **mort en production** aujourd'hui : aucun provider ne le produit plus), et un champ **nullable `ProvenanceReleve?`** est ajouté à `WindowState` pour la 4ᵉ distinction. Zéro rupture de compilation sur les 32 sites `new WindowState`, et les consommateurs existants (`IsEstimated`, `DataUnavailable`, `Describe`) restent **corrects sans édition**. | Pattern 1 |
| 2 | Où vit la doctrine ? | Dans une **classe pure** consommée par le **décorateur de tête** (`LastExactUsageProvider`). Pas dans `Best()` : argument mécanique en trois points (pas d'horloge/magasin/transcripts ; 3 instances imbriquées ⇒ 3 exécutions ; 22 sites de construction ; et la garde de position de la phase 18 deviendrait une coïncidence au lieu d'un choix). | Pattern 2 |
| 3 | Limite d'âge : une ou par source ? Valeur ? Avant ou après DEL-03 ? | **Une seule** valeur, **dérivée** (`CadenceNominale + 60 s` = 6 min), **constante et non réglable**. Elle s'applique **AVANT** DEL-03, mais comme **laissez-passer** (chemin rapide sans E/S), pas comme couperet : DEL-03 est une **ré-habilitation** qui opère au-delà de la limite. Un relevé de 3 h sans aucune activité depuis est donc **exact**. | Pattern 3, Pattern 4 |
| 4 | Borner le delta / convertir les tokens | **On ne convertit pas.** Le delta est un **plancher unilatéral** (`≥ X %`) + les tokens bruts. `Covers(T)` faux ⇒ refus (branche 4). Bornage par fenêtre obtenu **gratuitement** : `T` est `CapturedAt` **par fenêtre** et `LastExactStore.Load` invalide déjà une fenêtre dont le reset est passé. | Pattern 5, Open Question 4 |
| 5 | `SourceCapturedAt` et la staleness | **Ne rien supprimer en phase 19.** Le par-fenêtre (`CapturedAt`) devient la vérité de la doctrine ; `SourceCapturedAt` et `IsStale` sont **gelés comme legacy** (bindés nulle part) et retirés par la phase 20 quand elle bindera la provenance par fenêtre. Les supprimer ici casserait 4 tests pour zéro exigence. | Pattern 7 |
| 6 | Distinguer « jamais eu d'exact » de « eu un exact trop vieux » | Le magasin suffit : `Load()` rend `null` dans les deux cas, mais la **lecture brute** (fichier présent et schéma v1 valide) distingue les deux. Exposer `LastExactStore.UnExactADejaEteObtenu()`. Le modèle expose le bit sur `UsageSnapshot` — **sûr ici** parce que le décorateur est **au-dessus** du composite (le composite ne le reconstruira jamais). | Pattern 6 |
| 7 | Ce qui casse dans les 652 tests | **Aucun des 14 `CompositeUsageProviderTests`** avec la conception recommandée. **9 `LastExactUsageProviderTests`** à adapter (3 changent de sens, 6 n'ont besoin que du 4ᵉ argument de ctor). **3 sites `new LastExactUsageProvider`**. **7 `WeeklyRecalibrationTests` restent verts** avec le correctif Q8. Inventaire nominatif complet ci-dessous. | § Impact nominatif sur les 652 tests |
| 8 | Interaction avec `WeeklyRecalibration` | **Oui, elle change** et c'est un bug latent à corriger : la garde actuelle `Exact && ResetsAt != null` laisserait `Apply` **écraser le `resets_at` réel** d'une fenêtre hebdo passée en `Estimated` (plancher). La garde doit porter sur `ResetsAt is not null` **seul**. Correctif d'une ligne, **les 7 tests existants restent verts** (tous les cas « Repli » ont `ResetsAt = null`). | Open Question 8 |

---

## Vérifications empiriques faites le 2026-09-12 (machine réelle)

Aucune requête réseau. Aucune lecture/déchiffrement du coffre. Contrôle du coffre **avant et après** :
`%APPDATA%\Chronos\oauth.dat` = **518 octets, mtime 1783863147** — inchangé.

| # | Mesure | Résultat | Ce que ça décide |
|---|---|---|---|
| 1 | **Baseline** `dotnet test -c Release` | **652 réussis / 0 échec**, 2 min 15 s | Baseline confirmée. Commande de suite complète établie. |
| 2 | **Coût d'une passe de transcripts** : `TranscriptActivityProvider.ReadAsync()` exécuté 3 fois sur le vrai `~/.claude/projects` | **3218 ms, 2692 ms, 2791 ms** | **Interdit d'appeler la source de delta à chaque tick.** Impose le chemin rapide + la mémoïsation. C'est la contrainte de conception n°1 de la phase. |
| 3 | **Volume dans l'horizon de 8 j** | **474 fichiers, 536,1 Mo, 155 171 lignes** (923 Mo au total, 830 fichiers) | Confirme (2). Et confirme que le filtre `mtime ≥ now − 8 j` retire déjà 42 % du volume. |
| 4 | **Tokens sur les 5 dernières heures** (`log.Since(now − 5 h).Tokens`) | **643 649 933** | **Falsification quantitative de toute conversion tokens → %.** Contre l'ancien `FiveHourTokenBudget = 230 000 000` encore présent dans `settings.json`, cela donnerait **280 %**. Preuve à citer dans le plan pour EXA-04. |
| 5 | `%APPDATA%\Chronos\last-exact.json` | **Absent** | L'état « **aucun exact jamais obtenu** » (EXA-05) est l'état **courant** de la machine → vérifiable de bout en bout après build, pas seulement en test. |
| 6 | `%APPDATA%\Chronos\usage.json` | `{"five_hour":{"used_percentage":10,"resets_at":9},"capturedAt":1783666519131}` | Le bug des « 10 % » vit toujours. `resets_at:9` est désormais neutralisé (plancher epoch, phase 18) ⇒ `ResetsAt = null` ⇒ **le magasin refuse de le persister** ; mais `Best()` le sert encore comme `Exact`. |
| 7 | `%APPDATA%\Chronos\settings.json` | `SondeEnTetesActivee` **absent** du fichier ⇒ défaut `true` (code) ; `OAuthUsageEnabled: true` ; `FiveHourTokenBudget`/`WeeklyTokenBudget` obsolètes toujours présents (ignorés, DEL-06) | La sonde est active en production. La chaîne exacte est donc complète — et pourtant muette (jeton mort depuis le 2026-07-12). |

### Ce que la chaîne produit aujourd'hui, et ce qu'elle doit produire

| Étape | Aujourd'hui (vérifié) | Après phase 19 (attendu) |
|---|---|---|
| Sonde d'en-têtes | jeton mort → 401 → `Deconnecte` → `Empty` | idem |
| `ChronosOAuthUsageProvider` | autorité verrouillée → `Empty` | idem |
| `GatedOAuthUsageProvider` | coffre app sans `claudeAiOauth` → `Empty` | idem |
| `ClaudeUsageObjectProvider` | **5 h = `Exact`, 10 %, `ResetsAt = null`, `CapturedAt = null`** | **démoté : âge indéterminable ⇒ non certifiable** |
| `Best()` | retient le 10 % (seul non-`Unavailable`) | retient la même instance, mais elle n'est plus `Exact` |
| Magasin | vide (fichier absent) | rien à substituer |
| Cadran | **« 10 % » et « — »** | **« indisponible » + invitation à se connecter** (EXA-05) |

Ce tableau est le **critère d'acceptation humain** de la phase : il se vérifie en lançant l'exe, sans
instrumenter le code.

---

## Standard Stack

### Core — rien à installer

| Brique | Version | Rôle dans cette phase | Pourquoi elle suffit |
|---|---|---|---|
| .NET 8 (`net8.0-windows`) | SDK 10.0.201 installé, cible net8 | — | Rien de neuf n'est requis. `record` + `init` + `with` + pattern matching couvrent tout le besoin de modélisation. |
| CommunityToolkit.Mvvm | déjà référencé | `[ObservableProperty]` pour le signal EXA-05 dans `MainViewModel` | Générateur de source, aucune réflexion — compatible mono-fichier. |
| xunit 2.9.2 + Xunit.StaFact 1.1.11 | déjà référencés | 4 branches déterministes avec `FakeClock` | `[Fact]` pur suffit pour la doctrine (aucun type WPF). `[WpfFact]` seulement si un binding est touché. |

**Aucun paquet à ajouter. Aucune version à vérifier.** Vérification de non-régression du graphe, si le plan y tient :

```bash
dotnet list "src/Chronos/Chronos.csproj" package --outdated
```

### Briques internes à CONSOMMER (livrées, testées, à ne pas réécrire)

| Brique | Fichier | Ce qu'on en prend | Piège |
|---|---|---|---|
| `TranscriptActivityLog` | `Services/ITranscriptActivitySource.cs` | `Since(t)` **pur**, `Covers(t)`, `Horizon`, `Now` | `Since` borne basse **exclusive** (voulu : le message posé sur l'instant du relevé a déjà été compté par le serveur). `Covers(since) => since >= Horizon`. |
| `TranscriptActivityProvider` | `Services/TranscriptActivityProvider.cs` | `ReadAsync()` — **une** passe disque | **2,7–3,2 s, 536 Mo.** Aucun cache interne. À mémoïser. |
| `LastExactStore` | `Services/LastExactStore.cs` | `Save`, `Load(now)`, `Path` ; `SchemaVersion = 1` | **Ne pas bumper `SchemaVersion`** : 3 fixtures épinglent `"version":1` / `"version":999`. `Load` invalide déjà une fenêtre dont `ResetsAt <= now` — c'est le **bornage par fenêtre gratuit** du delta. |
| `LastExactUsageProvider` | `Services/LastExactUsageProvider.cs` | Position de tête, `IClock`, écrivain unique, `try/catch` englobant | Deux `Assert.IsType<LastExactUsageProvider>` dans `CompositionRootTests` (l. 108 et l. 348) : **garder le nom de classe**. |
| `UsageNormalization` | `Services/UsageNormalization.cs` | Point unique de conversion, plancher epoch 2020-01-01 | `NormalisationUniqueTests` **balaie le texte source** : toute nouvelle arithmétique d'unité doit passer par là ou la garde tombe. |
| `WindowState.FractionRemaining` | `Models/WindowState.cs` | Recalcul de géométrie | Déjà utilisé par `LastExactStore.Reconstruire`. |
| `IAuthStatus` / `EtatAuthentification` | `Services/EtatAuthentification.cs` | `NonConnecte` — **explicitement réservé à EXA-05** par la phase 17 | Le commentaire du code dit : « N'allume RIEN en phase 17 — l'invite "jamais connecté" est EXA-05, phase 19 ». C'est une dette **adressée** à cette phase. |
| `ReconnecterCommand` | `ViewModels/MainViewModel.cs` | Commande de login **qui ne déconnecte jamais** | **Ne JAMAIS utiliser `LoginClaudeCommand`** pour l'invite EXA-05 : elle BASCULE sur `IsLoggedIn == _store.Exists` — verrouillé à deux niveaux par la phase 17. |

### Alternatives considérées

| Recommandé | Alternative | Arbitrage |
|---|---|---|
| Doctrine dans le décorateur de tête | Doctrine dans `Best()` | Coût : injection d'`IClock` + limite dans `CompositeUsageProvider` ⇒ **22 sites `new CompositeUsageProvider`** (App.xaml.cs ×3, `CompositeUsageProviderTests` ×16, `CompositionRootTests` ×3) à retoucher dans la **même tâche** (compilabilité). Bénéfice réel : un seul cas — « primaire exact périmé bat fallback exact frais » — et ce cas est **mécaniquement impossible** dans la chaîne actuelle (voir Pattern 2, preuve). |
| `Estimated` réaffecté au plancher | Nouveau membre d'enum `ExactPlusDelta` | Un nouveau membre traverse **silencieusement** `WindowGaugeViewModel.Apply` (`IsEstimated = r == Estimated` ⇒ `false` ⇒ le plancher serait affiché comme **exact** : violation frontale de DEL-04) et `DiagnosticService.Describe` (`_ => "indisponible"`). Il faut donc éditer ces deux fichiers de toute façon — l'enum ne protège rien et ajoute du bruit. |
| Décorateur de mémoïsation de l'activité | Cache dans la doctrine | Le cache dans la doctrine rend la doctrine impure et non testable sans horloge interne. Un décorateur `ITranscriptActivitySource` est testable seul, remplaçable en DI, et neutre. |
| Garde de recomposition par balayage de texte | `with` au lieu de `new` dans `GetAsync` | `with` sur `p` ferait **hériter silencieusement du primaire** tout champ futur de `UsageSnapshot` — une valeur fausse est pire qu'un `null`. Le `new` explicite est le bon choix ; ce qui manque est une **garde** qui échoue si une propriété de `UsageSnapshot` n'est pas nommée dans `CompositeUsageProvider.cs`. Précédent : `NormalisationUniqueTests`. |

---

## Architecture Patterns

### Structure de fichiers recommandée

```
src/Chronos/
├── Models/
│   ├── WindowState.cs                    # MODIFIÉ : + ProvenanceReleve? Provenance, + long? TokensDepuisReleve
│   ├── ProvenanceReleve.cs               # NOUVEAU : enum Frais | EncoreValide | PlancherAvecActivite
│   └── UsageSnapshot.cs                  # MODIFIÉ : + bool? UnExactADejaEteObtenu  (posé AU-DESSUS du composite)
├── Services/
│   ├── DoctrineFraicheur.cs              # NOUVEAU : classe PURE, statique — les 4 branches, 0 E/S, 0 horloge
│   ├── LastExactUsageProvider.cs         # MODIFIÉ : devient la COUCHE DE DOCTRINE (nom conservé)
│   ├── LastExactStore.cs                 # MODIFIÉ : + UnExactADejaEteObtenu(), + CapturedAt requis à la persistance
│   ├── SourceActiviteMemoisee.cs         # NOUVEAU : décorateur ITranscriptActivitySource à TTL
│   ├── CompositeUsageProvider.cs         # MODIFIÉ a minima (commentaire de doctrine + rien d'autre)
│   ├── ClaudeUsageObjectProvider.cs      # MODIFIÉ : CapturedAt = l'horodatage DU FICHIER (piège n°1)
│   ├── ChronosOAuthUsageProvider.cs      # MODIFIÉ : CapturedAt = now
│   ├── ClaudeOAuthUsageProvider.cs       # MODIFIÉ : CapturedAt = now
│   └── WeeklyRecalibration.cs            # MODIFIÉ : garde sur ResetsAt, plus sur Reliability
├── ViewModels/
│   └── MainViewModel.cs                  # MODIFIÉ : signal EXA-05 (AfficherInvitationConnexion)
└── App.xaml.cs                           # MODIFIÉ : 4e arg du décorateur + enregistrement du mémoïseur

tests/Chronos.Tests/
├── DoctrineFraicheurTests.cs             # NOUVEAU : les 4 branches + falsifiabilité de chacune
├── SourceActiviteMemoiseeTests.cs        # NOUVEAU : TTL, une seule passe, tolérance de certification
├── Fakes/FakeTranscriptActivitySource.cs # NOUVEAU : n'existe pas encore
├── LastExactUsageProviderTests.cs        # RÉÉCRIT partiellement (3 tests changent de sens)
└── GardesDoctrineTests.cs                # NOUVEAU : recomposition de UsageSnapshot + non-retour tokens→%
```

---

### Pattern 1 — Modéliser 4 états sans casser 32 sites de construction (répond à Q1)

**Le fait décisif, vérifié :** `SourceReliability.Estimated` **n'est plus produit par aucun provider** de
`src/`. La phase 16 a supprimé l'estimation absolue ; il ne reste que 5 **lectures** :

```
src/Chronos/Services/CompositeUsageProvider.cs:60    Rank(Estimated) => 1
src/Chronos/Services/DiagnosticService.cs:590         "estimé — ~X %"
src/Chronos/Services/WeeklyRecalibration.cs:17        (commentaire seulement)
src/Chronos/ViewModels/WindowGaugeViewModel.cs:69     IsEstimated = r == Estimated
src/Chronos/ViewModels/WindowGaugeViewModel.cs:84     HasTokens   = r == Estimated && EstimatedTokens > 0
```

`Estimated` est donc une **valeur morte avec son câblage de présentation intact** : « ce chiffre n'est pas un
exact courant » est exactement sa sémantique résiduelle, et c'est exactement la sémantique du plancher DEL-04.

**Mapping recommandé :**

| Branche de la doctrine | `Reliability` | `Provenance` (neuf, nullable) | `Utilization` | `TokensDepuisReleve` |
|---|---|---|---|---|
| 1. Exact frais | `Exact` | `Frais` | valeur du serveur | `null` |
| 2. Encore rigoureusement valide (DEL-03) | `Exact` | `EncoreValide` | valeur mémorisée, **inchangée** | `0` |
| 3. Plancher + activité (DEL-04) | **`Estimated`** | `PlancherAvecActivite` | valeur mémorisée, **JAMAIS gonflée** | somme brute depuis T |
| 4. Indisponible | `Unavailable` | `null` | **`null`** | `null` |

Branche 2 porte `Exact` **par fidélité à DEL-03 lui-même** : « ce relevé est **encore exact**, pas périmé ».
Le rendre `Estimated` contredirait l'exigence écrite.

```csharp
// src/Chronos/Models/ProvenanceReleve.cs
namespace Chronos.Models;

/// <summary>
/// Provenance FINE d'un chiffre affiché (phase 19). Complète <see cref="SourceReliability"/>, qui dit
/// « peut-on s'y fier », par « d'où vient-il et qu'a-t-on vérifié ». null = la doctrine n'a pas statué
/// (fenêtre indisponible, ou WindowState fabriqué hors doctrine : provider, test, magasin).
///
/// La phase 20 (EXA-03) binde CES trois valeurs pour la distinction visuelle ; aucune géométrie de cadran
/// n'en dépend en phase 19.
/// </summary>
public enum ProvenanceReleve
{
    /// <summary>Relevé dont l'âge est sous la limite : exact, affiché tel quel, aucune E/S consultée.</summary>
    Frais,

    /// <summary>Relevé plus vieux que la limite MAIS dont on a PROUVÉ qu'aucune réponse assistant n'est
    /// survenue depuis sa capture : l'utilisation n'a objectivement pas bougé, il est encore exact (DEL-03).</summary>
    EncoreValide,

    /// <summary>De l'activité est survenue depuis la capture : le chiffre est une BORNE INFÉRIEURE
    /// (« au moins X % »), pas une valeur. Jamais gonflé, jamais confondu avec un exact (DEL-04).</summary>
    PlancherAvecActivite,
}
```

```csharp
// src/Chronos/Models/WindowState.cs — AJOUTS (init, nullable, SANS required : les 32 sites
// « new WindowState » existants compilent sans une retouche)

/// <summary>Phase 19 — provenance fine décidée par la doctrine. null = non statué.
/// Porté par WindowState et NON par UsageSnapshot, pour la même raison mécanique que StatutServeur :
/// Best() rend l'INSTANCE gagnante par référence, donc ce champ traverse gratuitement les trois
/// composites imbriqués, là où un champ de snapshot serait détruit par la recomposition.</summary>
public ProvenanceReleve? Provenance { get; init; }

/// <summary>Phase 19 (DEL-04) — tokens de réponses assistant observés depuis CapturedAt, BORNE
/// INFÉRIEURE et matière première BRUTE. JAMAIS converti en points de pourcentage (EXA-04) : les
/// limites Anthropic pondèrent par modèle, et les transcripts ignorent l'app de bureau et Cowork.
/// Champ DISTINCT d'EstimatedTokens, qui portait la somme de l'estimation absolue supprimée —
/// réutiliser ce dernier rattacherait au nouveau chiffre la sémantique que le milestone a tuée.</summary>
public long? TokensDepuisReleve { get; init; }
```

**Ce que chaque option casse — inventaire demandé :**

| Option | Rupture de compilation | Rupture de comportement silencieuse | Verdict |
|---|---|---|---|
| **A.** Ajouter des membres à `SourceReliability` | Aucune (les enums sont additifs ; les 32 sites passent des valeurs explicites). | **Trois**, toutes graves : (1) `WindowGaugeViewModel.cs:69` ⇒ `IsEstimated = false` ⇒ le plancher s'affiche **sans marque**, DEL-04 violé à l'écran ; (2) `WindowGaugeViewModel.cs:84` ⇒ `HasTokens = false` ⇒ la matière brute disparaît ; (3) `DiagnosticService.cs:590` `_ =>` ⇒ un plancher est décrit « **indisponible** » dans le rapport. Plus `CompositeUsageProvider.Rank` qui range les nouveaux membres en `0` = moins fiable qu'`Unavailable`. | **À éviter** : exige les mêmes éditions que l'option retenue, sans rien garantir de plus, et chaque oubli est muet. |
| **B.** Type de « provenance » séparé, `SourceReliability` intouché et **non démoté** | Aucune. | `Reliability == Exact` + `Provenance == PlancherAvecActivite` est une **contradiction lisible de deux façons**. Tous les consommateurs lisent `Reliability` seul ⇒ le plancher passe pour exact. | **À éviter** sous cette forme. |
| **C. (retenue)** `Estimated` réaffecté + `Provenance?` additif | Aucune. | Aucune : les 5 lectures de `Estimated` produisent **exactement le bon comportement** pour le plancher (marque « ~ », tokens visibles, « estimé — » au diagnostic). Seule retouche recommandée : le préfixe (voir ci-dessous). | **Recommandée.** |
| **D.** Champ sur `UsageSnapshot` plutôt que `WindowState` | Aucune. | **Détruit** par `CompositeUsageProvider.GetAsync` (`new UsageSnapshot { … }`) — c'est le piège documenté de la phase 18. Sauf pour un champ posé **au-dessus** du composite (cas du bit EXA-05, voir Pattern 6). | **Interdit** pour tout ce qui naît sous le décorateur. |

**Retouche de présentation recommandée (petite, mais c'est de l'honnêteté, pas du visuel — donc phase 19) :**
`PercentFormatter.Format(util, isEstimated)` préfixe « ~ », qui suggère une erreur **symétrique**. Le plancher
est **unilatéral**. Proposition : surcharge additive, les 4 assertions existantes dans
`WindowGaugeViewModelTests.cs:43-56` restent intactes.

```csharp
// src/Chronos/Text/PercentFormatter.cs — AJOUT (la surcharge à 2 arguments reste, mot pour mot)
/// <summary>Phase 19 — « 80 % » (exact), « ≥ 80 % » (plancher : au moins, borne inférieure), «» (null).
/// « ≥ » et non « ~ » : l'incertitude d'un plancher est UNILATÉRALE. « ~ » dirait « autour de 80 »,
/// ce qui autoriserait une lecture « peut-être 75 » — or on SAIT qu'on est à 80 au minimum.</summary>
public static string Format(double? utilization, ProvenanceReleve? provenance)
{
    if (utilization is null) return "";
    int pct = (int)Math.Round(utilization.Value * 100, MidpointRounding.AwayFromZero);
    string prefixe = provenance == ProvenanceReleve.PlancherAvecActivite ? "≥ " : "";
    return $"{prefixe}{pct} %";
}
```

> Si le plan juge ce changement de signe hors périmètre, il doit l'**écrire** comme dette explicite pour la
> phase 20 — pas le laisser implicite : « ~ » sur un plancher est une imprécision d'honnêteté, pas de style.

---

### Pattern 2 — La doctrine vit dans le décorateur de tête, pas dans `Best()` (répond à Q2)

**Trois arguments mécaniques, pas esthétiques.**

**(a) `Best()` n'a accès à rien de ce dont la doctrine a besoin.** Il reçoit deux `WindowState` nus. La
doctrine a besoin de `now` (horloge), du relevé persisté (`LastExactStore`) et du journal d'activité
(`ITranscriptActivitySource`). Les trois sont déjà dans les mains de `LastExactUsageProvider`, deux y sont
déjà injectés.

**(b) La chaîne réelle est faite de trois composites imbriqués** (`App.xaml.cs:339-348`) :

```
LastExactUsageProvider
└── Composite( primary: RateLimitHeaderUsageProvider ,
              fallback: Composite( primary: ChronosOAuthUsageProvider ,
                                  fallback: Composite( primary: GatedOAuthUsageProvider ,
                                                      fallback: ClaudeUsageObjectProvider )))
```

Une doctrine dans `Best()` s'exécuterait **trois fois par tick**, et les instances internes statueraient sur
une information partielle. Si elle consulte les transcripts, c'est **3 × 2,9 s = 8,7 s par tick** (mesure n°2).
Rédhibitoire. Et l'injection impose de retoucher **22 sites `new CompositeUsageProvider`** dans la même tâche
(compilabilité) — pour un bénéfice d'un seul cas, qui est :

**(c) Le cas « primaire exact périmé bat fallback exact frais » est mécaniquement impossible ici.** Preuve par
énumération des sources capables de servir un `Exact` vieux :

| Source | Position | Staleness maximale servie | Vérifié à |
|---|---|---|---|
| `RateLimitHeaderUsageProvider` | primaire le plus externe | **300 s** (`CacheUtilisable`) | `RateLimitHeaderUsageProvider.cs:157,507` |
| `ChronosOAuthUsageProvider` | primaire du 2ᵉ composite | **900 s** (`CacheUsable = 15 min`) | `ChronosOAuthUsageProvider.cs:44,163` |
| `GatedOAuthUsageProvider` → `ClaudeOAuthUsageProvider` | primaire du 3ᵉ composite | cache d'instance (RAM) | `ClaudeOAuthUsageProvider.cs:43` |
| `ClaudeUsageObjectProvider` | **fallback le plus interne** | **non bornée** (le fichier peut avoir 2 mois) | `ClaudeUsageObjectProvider.cs` — aucune limite d'âge |

La **seule** source à staleness non bornée est le **repli le plus interne**. Elle ne peut donc gagner que
lorsque tout ce qui est au-dessus est `Unavailable` — auquel cas appliquer la porte d'âge **au-dessus** du
composite est **strictement équivalent** à l'appliquer dans `Best()`. Les sources à cache borné (300 s / 900 s)
ne produisent jamais un écart d'âge pathologique entre deux `Exact`.

> **Fragilité à consigner** : cette équivalence est une propriété de **l'ordre actuel** de la chaîne. Un
> réordonnancement futur la casserait en silence. Parade conseillée, dans l'esprit de la garde de position de
> la phase 18 : un test de comportement qui prouve que `ClaudeUsageObjectProvider` est bien le repli le plus
> interne (deux sources `Exact` aux chiffres différents, on vérifie lequel sort).

**(d) Et la garde de position de la phase 18 ?**
`CompositionRootTests.La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` (l. 284) pose `FakeClock` à
`2026-09-09T12:00Z`, la sonde répond avec `CapturedAt = now` ⇒ âge = 0 ⇒ branche 1 ⇒ `Exact`, `0.01`,
`StatutServeur.Autorise`. **Elle reste verte** avec la conception recommandée — seule édition nécessaire : le
**4ᵉ argument de ctor** du décorateur (l. 326-332).

En revanche, si l'on avait mis la doctrine dans `Best()` **en arbitrant par récence entre deux `Exact`**, la
garde deviendrait une **coïncidence** : `ChronosOAuthUsageProvider` répondant frais pendant que la sonde sert
son cache de 300 s aurait une `CapturedAt` **plus récente** et gagnerait — emportant `StatutServeur` et
`Depassement` (HDR-03/HDR-04 morts-nés, exactement le défaut que 18-05 a corrigé). **Conclusion : `Best()` ne
doit JAMAIS arbitrer par récence entre deux `Exact`.** La porte d'âge est **binaire** (certifiable / pas), pas
un classement.

```csharp
// src/Chronos/Services/DoctrineFraicheur.cs
using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// LA DOCTRINE (EXA-02, EXA-04, DEL-03, DEL-04). Classe PURE : aucune E/S, aucune horloge propre, aucun
/// type WPF. Elle reçoit tout ce dont elle a besoin et rend un WindowState — donc entièrement testable
/// en [Fact] sur les quatre branches, et chaque branche est supprimable pour prouver qu'un test tombe.
///
/// Ordre des règles, et POURQUOI cet ordre (voir RESEARCH Pattern 4) :
///   1. âge sous la limite                      -> Exact / Frais                 (laissez-passer, 0 E/S)
///   2. journal couvre T et AUCUNE activité      -> Exact / EncoreValide          (DEL-03 : ré-habilitation)
///   3. journal couvre T et activité             -> Estimated / Plancher          (DEL-04 : borne inférieure)
///   4. sinon                                   -> Unavailable                   (jamais de chiffre inventé)
/// </summary>
public static class DoctrineFraicheur
{
    /// <summary>
    /// Âge maximal d'un relevé exact affiché SANS vérification d'activité (EXA-02). DÉRIVÉE de la cadence
    /// de la sonde, jamais recopiée (motif du plan 18-05 : « cadence et coût DÉRIVÉS de CadenceNominale ») :
    /// sous cette limite, le cache légitime de la sonde n'est jamais démoté, donc le chemin nominal ne paie
    /// JAMAIS la passe de transcripts (mesurée à 2,7-3,2 s / 536 Mo sur la machine cible).
    ///
    /// NON RÉGLABLE, et c'est délibéré : EXA-02 est une propriété de sûreté, pas une préférence. Un réglage
    /// permettrait de la porter à deux mois — c'est-à-dire de recréer le bug que ce milestone éradique.
    /// </summary>
    public static readonly TimeSpan LimiteAge =
        RateLimitHeaderUsageProvider.CadenceNominale + TimeSpan.FromSeconds(60);

    /// <summary>Âge d'une fenêtre, ou null si elle ne dit pas QUAND elle a été capturée. Un Exact sans
    /// horodatage est INCERTIFIABLE : on ne peut ni mesurer son âge, ni demander « activité depuis T ».
    /// Rendre null (et non TimeSpan.Zero) est le cœur de la correction du bug des « 10 % ».</summary>
    public static TimeSpan? Age(WindowState w, DateTimeOffset now)
        => w.CapturedAt is { } t ? now - t : null;

    /// <summary>
    /// Statue sur UNE fenêtre. <paramref name="vivante"/> est ce que la chaîne vient de produire,
    /// <paramref name="memorisee"/> ce que le magasin détient (déjà validé : reset futur), et
    /// <paramref name="journal"/> le journal d'activité (null = source indisponible ou non consultée).
    /// </summary>
    public static WindowState Statuer(WindowState vivante, WindowState? memorisee,
                                      TranscriptActivityLog? journal, DateTimeOffset now)
    {
        // Le meilleur CANDIDAT : une réponse vivante exacte, sinon le relevé mémorisé. On ne remplace
        // jamais un exact vivant par le magasin ; mais un exact vivant INCERTIFIABLE (CapturedAt null,
        // ou trop vieux) ne vaut pas mieux que le magasin — c'est là que le vieux 10 % tombe.
        var candidat = Candidat(vivante, memorisee, now);
        if (candidat is null) return Indisponible(vivante);

        var age = Age(candidat, now);
        if (age is null) return Indisponible(vivante);          // incertifiable : aucun horodatage

        // 1. FRAIS — laissez-passer : pas d'E/S, pas de question posée aux transcripts.
        if (age.Value <= LimiteAge)
            return candidat with { Reliability = SourceReliability.Exact,
                                   Provenance = ProvenanceReleve.Frais };

        var t = candidat.CapturedAt!.Value;

        // 4a. Sans journal, ou journal qui NE COUVRE PAS T : on ne peut rien prouver. On ne borne pas
        // un delta qu'on ne sait pas borner (piège Horizon / mtime 8 j) — on se déclare indisponible.
        if (journal is null || !journal.Covers(t)) return Indisponible(vivante);

        var activite = journal.Since(t);

        // 2. ENCORE EXACT (DEL-03) — aucune réponse assistant depuis T : l'utilisation n'a PAS bougé.
        if (!activite.HasActivity)
            return candidat with { Reliability = SourceReliability.Exact,
                                   Provenance = ProvenanceReleve.EncoreValide,
                                   TokensDepuisReleve = 0 };

        // 3. PLANCHER (DEL-04) — il y a eu de l'activité : le chiffre devient une BORNE INFÉRIEURE.
        // Utilization INCHANGÉE : on ne l'augmente pas d'un delta converti (EXA-04). C'est la
        // QUALIFICATION du chiffre qui change, pas sa valeur.
        return candidat with { Reliability = SourceReliability.Estimated,
                               Provenance = ProvenanceReleve.PlancherAvecActivite,
                               TokensDepuisReleve = activite.Tokens };
    }

    // Exact vivant CERTIFIABLE -> lui. Sinon le magasin. Sinon rien.
    private static WindowState? Candidat(WindowState vivante, WindowState? memorisee, DateTimeOffset now)
    {
        bool vivanteUtile = vivante.Reliability == SourceReliability.Exact
                            && vivante.Utilization is not null
                            && vivante.CapturedAt is not null;
        if (vivanteUtile) return vivante;
        return memorisee;
    }

    // Démotion : on EFFACE le chiffre (sinon WindowGaugeViewModel.Apply peindrait encore l'arc à 10 %,
    // il lit Utilization sans regarder Reliability), mais on CONSERVE l'instance vivante et donc le
    // statut serveur / le dépassement : un reset et un statut restent des faits, même sans pourcentage.
    private static WindowState Indisponible(WindowState vivante)
        => vivante with { Reliability = SourceReliability.Unavailable,
                          Utilization = null, FractionTimeRemaining = vivante.FractionTimeRemaining,
                          EstimatedTokens = null, TokensDepuisReleve = null, Provenance = null };
}
```

> **Décision à graver dans le plan** : `Indisponible` conserve-t-il `ResetsAt`/`FractionTimeRemaining` ? **Oui
> recommandé** : un `resets_at` est un fait indépendant de l'âge du pourcentage, et le chemin « fenêtre
> `Unavailable` + reset synthétisé » existe déjà (`WeeklyRecalibration`). L'alternative (tout effacer) est
> défendable mais perd le compte à rebours. Ce qui ne l'est **pas** : garder `Utilization`.

---

### Pattern 3 — La limite d'âge est un régulateur de débit, pas un jugement (répond à Q3, partie « valeur »)

**Une seule valeur, pas une par source.** Une limite par source serait une invitation à la dérive : chaque
provider négocierait sa propre définition de « frais », et la garde de non-divergence n'existerait pas. Une
déclaration unique dans `DoctrineFraicheur` est la même discipline que `UsageNormalization` (point unique) et
que `TranscriptActivityProvider.HorizonSpan` (« UNE seule déclaration : le filtre de scan et l'horizon annoncé
ne doivent JAMAIS pouvoir diverger »).

**La valeur est contrainte par des nombres mesurés, pas par le goût :**

| Contrainte | Borne | Source |
|---|---|---|
| Doit **dépasser** le cache de la sonde, sinon le chemin nominal paie 2,9 s de disque 4 ticks sur 5 | **> 300 s** | `CacheUtilisable = 300 s`, `CadenceNominale = 300 s` |
| Peut rester **sous** le cache OAuth : un relevé OAuth de 10 min gagne à être **certifié** par les transcripts plutôt qu'**affirmé** | < 900 s souhaitable | `CacheUsable = 15 min` |
| Doit être **très inférieure** à la plus courte fenêtre, sinon un relevé périmé survit à un cycle entier | ≪ 5 h | — |
| Doit être **supérieure** à l'intervalle de rafraîchissement | > 60 s | `RefreshIntervalSeconds: 60` |

**Recommandation : `CadenceNominale + 60 s` = 6 min.** Dérivée, donc elle suit automatiquement un changement
de cadence de la sonde ; au-dessus de 300 s donc le chemin nominal est **gratuit** ; au-dessous de 900 s donc
un cache OAuth profond bascule vers la branche certifiée, qui est **plus** honnête que la branche 1.

**Ne pas la réglabiliser.** Un `LimiteAgeMinutes` dans `settings.json` permettrait de reproduire exactement le
bug corrigé. Et le projet a un précédent direct : `FiveHourBudgetSource: "Manual"` — un réglage utilisateur qui
a **gelé à vie** un plafond faux et causé tout le milestone.

**Ne pas confondre avec `MainViewModel.IsStale` (seuil 2 min).** Deux seuils coexisteront temporairement. Ce
n'est pas grave en phase 19 (`IsStale` est bindé **nulle part**, vérifié : zéro occurrence dans tout le XAML),
mais le plan doit **écrire** que la phase 20 unifie le vocabulaire en bindant `Provenance` et en retirant
`IsStale`. Sinon la dette devient silencieuse.

---

### Pattern 4 — L'ordre des règles : la limite d'âge est un laissez-passer, DEL-03 une ré-habilitation (répond à Q3, partie « ordre »)

La question posée — « un relevé de 3 h sans aucune activité depuis est-il exact ou trop vieux ? » — se tranche
par l'honnêteté, et la réponse est : **exact**.

**Argument.** `utilization` est une fonction de la consommation. Sans consommation, elle ne change pas (elle ne
peut que **baisser** au reset, et `LastExactStore.Load` écarte déjà toute fenêtre dont le reset est passé).
Donc : *zéro réponse assistant depuis T* ⇒ *l'utilisation à `now` est égale à celle de T*. Ce n'est pas une
approximation, c'est une **déduction**. Déclarer « trop vieux » un chiffre dont on peut **prouver** qu'il est
juste, ce n'est pas de la prudence : c'est **cacher une information vraie**, et l'overlay devient muet
précisément dans le cas où il est le plus utile (vous revenez le matin, rien n'a tourné, Chronos doit dire
« 10 % » et non « indisponible »).

**Donc la limite d'âge ne peut pas être un couperet placé en amont.** Elle est un **laissez-passer** : sous la
limite, on se dispense de la preuve (et de la passe disque de 2,9 s). Au-delà, on **exige** la preuve. Et la
preuve peut **ré-habiliter** au-delà de la limite, sans plafond d'ancienneté.

**Mais alors, y a-t-il un plafond absolu ?** Oui, et il est **déjà** dans le code, gratuitement : l'horizon de
8 jours de `TranscriptActivityProvider`. Un relevé plus vieux que 8 jours rend `Covers(T) == false` ⇒ aucune
preuve possible ⇒ branche 4. **Le bug des « 10 % de deux mois » meurt donc par `Covers`, pas par la limite
d'âge.** Vérifié arithmétiquement : `Horizon = now − 8 j` = 2026-09-04, `T` = 2026-07-10 ⇒ `Covers` faux.

Ordre final, à écrire tel quel dans le plan :

```
Candidat ← exact vivant certifiable, sinon relevé persisté, sinon ∅      → ∅ ? indisponible
CapturedAt inconnu ?                                                     → indisponible   (EXA-02)
âge ≤ LimiteAge ?                                                        → Exact / Frais  (EXA-02)
journal absent OU !Covers(CapturedAt) ?                                  → indisponible   (DEL-04 borne)
aucune activité depuis CapturedAt ?                                      → Exact / EncoreValide (DEL-03)
sinon                                                                    → Estimated / Plancher (DEL-04)
```

---

### Pattern 5 — Le delta ne se convertit pas : il change la NATURE du chiffre (répond à Q4 — le cœur de la phase)

**La conversion demandée est impossible honnêtement.** Passer de *N tokens* à *Δ points de pourcentage* exige
un taux *points / token*. Ce taux **est** un plafond. EXA-04 l'interdit, et trois faits le rendent faux de
toute façon :

1. **Les limites Anthropic pondèrent par modèle** (fondement verrouillé du milestone, STATE.md).
2. **Les transcripts sont une borne inférieure de l'activité, pas la consommation du compte** :
   `~/.claude/projects` ne contient que Claude Code ; l'app de bureau et Cowork consomment le **même pool** et
   n'y figurent pas. Le déficit est d'amplitude **inconnue** (documenté dans `ITranscriptActivitySource.cs`).
3. **Mesure n°4** : 643 649 933 tokens sur les 5 dernières heures de cette machine. Contre le
   `FiveHourTokenBudget = 230 000 000` encore présent dans `settings.json` : **280 %**. Tout taux calibré de
   cette façon est faux d'un facteur > 2,8.

**Piste à rejeter explicitement dans le plan** (elle est séduisante et quelqu'un la proposera) : calibrer le
taux empiriquement à partir de **deux** relevés exacts successifs (`Δutilization / Δtokens`). Elle échoue pour
trois raisons distinctes : (a) le magasin ne garde **qu'un** relevé par fenêtre — pas d'historique ; (b) le
taux dépend du mélange de modèles, qui change d'une période à l'autre ; (c) `Δtokens` est une borne inférieure
à déficit inconnu, donc le taux est biaisé d'un facteur inconnu. C'est `tokens / plafond` avec un plafond
auto-calibré — c'est-à-dire **`BudgetAutoCalibrator`**, supprimé en phase 16 et gardé par
`ServicesLayerPurityTests.Aucun_type_de_plafond_ne_subsiste_dans_l_assembly`.

**Ce qu'on affiche à la place — réponse défendable.** À l'intérieur d'une fenêtre non encore remise à zéro,
`utilization` est **monotone croissante**. Donc le dernier relevé exact est une **borne inférieure rigoureuse**
de l'utilisation présente. La présence d'activité transforme l'assertion « c'est 42 % » en assertion
« c'est **au moins** 42 % », dont la borne supérieure est **inconnue** (et non 42 % + quelque chose).

| Grandeur | Statut | Affichage |
|---|---|---|
| `Utilization` = valeur du relevé | **exacte en tant que borne inférieure** | « ≥ 42 % » |
| Borne supérieure | **inconnue** — et il est honnête de le dire | rien (jamais un intervalle inventé) |
| Tokens depuis T | **borne inférieure brute** de l'activité | « ≈ 643 M tokens » (déjà géré par `TokensText`/`HasTokens`) |
| Âge du relevé (`CapturedAt`) | fait | « il y a 3 h » (phase 20, EXA-06) |

**Le bornage « par fenêtre » demandé est obtenu gratuitement**, et il faut le dire explicitement au planner
pour qu'il ne réécrive pas ce qui existe :

- `T` est `WindowState.CapturedAt` — **par fenêtre** depuis la phase 16. Les deux fenêtres ont donc leur propre
  borne basse. Aucun « delta global » n'est jamais calculé.
- Si la fenêtre 5 h a roulé depuis T, `LastExactStore.Reconstruire` rend **`null`** pour elle
  (`if (e?.ResetsAt is not { } resets || resets <= now) return null;`). Elle ne peut donc pas arriver à la
  branche 3. Le sur-comptage redouté est **structurellement impossible**.
- Les deux fenêtres interrogent **un seul** `TranscriptActivityLog` (une passe disque), avec deux `Since`
  différents — c'est exactement la raison d'être de la séparation `ReadAsync` / `Since` de la phase 16. **Un
  seul `ReadAsync` par `GetAsync`, jamais deux.**

**Ce que cela impose à la phase 20 — à écrire dans le plan pour qu'elle en hérite :**

1. **Aucun « arc de delta », aucune barre d'erreur, aucun dégradé borné** : il n'existe pas de borne
   supérieure à dessiner. Le vocabulaire visuel doit être **unilatéral** : l'arc est dessiné à la borne
   inférieure, et le fait que le vrai soit « quelque part au-delà » se marque par une **ouverture** (bord
   hachuré, estompé, flèche) et non par une longueur.
2. Le texte central porte « ≥ » et non « ~ ».
3. Les trois états d'EXA-03 se bindent sur `WindowState.Provenance` (3 valeurs + `null`), pas sur
   `IsStale`.
4. EXA-06 nomme la source **et** l'âge : `CapturedAt` + `Provenance` suffisent, aucun champ neuf.

---

### Pattern 6 — « Jamais eu d'exact » : le magasin le sait déjà (répond à Q6)

`LastExactStore.Load(now)` rend `null` dans **deux** cas indiscernables : fichier absent/illisible, et fichier
valide dont toutes les fenêtres ont roulé. La distinction existe un étage plus bas : `LireBrut()` rend un
`Payload` non-null **si et seulement si** le fichier existe, est lisible, et porte `version == 1`. C'est
exactement « un chiffre exact a déjà été obtenu et mémorisé ».

```csharp
// src/Chronos/Services/LastExactStore.cs — AJOUT
/// <summary>
/// EXA-05 — un relevé exact a-t-il DÉJÀ été obtenu et mémorisé ? Distinct de <see cref="Load"/>, qui rend
/// null aussi bien pour « jamais rien » que pour « quelque chose, mais la fenêtre a roulé ». Le premier cas
/// appelle « connecte-toi », le second non : l'utilisateur EST connecté, sa fenêtre a simplement tourné.
/// Aucune évolution de schéma (SchemaVersion reste 1 : trois fixtures épinglent "version":1 / "version":999).
/// </summary>
public bool UnExactADejaEteObtenu() => LireBrut() is { } p && (p.FiveHour is not null || p.SevenDay is not null);
```

**Où poser le bit pour que la phase 20 l'utilise ?** Sur `UsageSnapshot`, et **c'est sûr ici** :

```csharp
// src/Chronos/Models/UsageSnapshot.cs — AJOUT
/// <summary>EXA-05 — un relevé exact a-t-il déjà été obtenu au moins une fois ? null = non évalué
/// (tout snapshot produit SOUS la couche de doctrine, dont UsageSnapshot.Empty). false = jamais :
/// l'overlay doit inviter à se connecter et ne JAMAIS afficher de pourcentage.
///
/// Sur UsageSnapshot et non WindowState : c'est un fait de COMPTE, pas de fenêtre. Sans danger malgré la
/// recomposition par « new » de CompositeUsageProvider.GetAsync, parce que ce champ est posé par la couche
/// de doctrine, qui est AU-DESSUS du composite : aucun composite ne le reverra jamais.</summary>
public bool? UnExactADejaEteObtenu { get; init; }
```

`bool?` et non `bool` : avec `bool`, `UsageSnapshot.Empty` vaudrait `false` = « jamais eu d'exact », ce qui
serait une **affirmation** produite par une absence — précisément le travers que le projet proscrit
(« `null != 0` est la doctrine du projet », `UsageNormalization`).

**Alternative à présenter : le canal latéral**, sur le modèle de `IEtatServeur`/`IAuthStatus` (phases 17-18).
Plus coûteux (une interface + un enregistrement + un alias d'instance) et inutile ici, puisque le champ n'est
pas menacé par `Best()`. **Mais** si le plan veut déjà préparer EXA-06 (phase 20 : « quelle source alimente
l'affichage »), un `IEtatDoctrine` serait le bon véhicule — à arbitrer, pas à subir.

**Côté ViewModel**, le signal. Ne pas se contenter de `EtatAuthentification.NonConnecte` : un utilisateur peut
être connecté **et** n'avoir jamais obtenu de chiffre (sonde coupée, endpoint muet). Les deux informations se
composent :

```csharp
// ViewModels/MainViewModel.cs — dans ApplySnapshot
// EXA-05 : jamais un pourcentage quand aucun exact n'a jamais été obtenu. L'invite est ACTIONNABLE
// (ReconnecterCommand, qui ne déconnecte JAMAIS — contrairement à LoginClaudeCommand, verrouillée par 17-05).
AfficherInvitationConnexion = snap.UnExactADejaEteObtenu == false && DataUnavailable;
```

`DataUnavailable` existe déjà et devient **vrai** dans le scénario réel (tableau § Vérifications empiriques) :
la moitié « indisponible » d'EXA-05 est donc déjà acquise côté données. Seule l'**invite** est neuve.

> **Tension de périmètre à trancher dans le plan** : ni `DataUnavailable` ni `IsStale` ne sont bindés dans le
> XAML (vérifié : 0 occurrence). EXA-05 a donc une moitié visible dont le foyer naturel est la phase 20.
> Deux issues honnêtes : (a) phase 19 livre le signal VM + son test et **consigne** le binding en phase 20 ;
> (b) phase 19 allume la **pastille existante** (déjà bindée à `ReconnecterCommand`, phase 17-05) via un
> troisième booléen. (b) est peu coûteux et rend EXA-05 réellement observable — recommandé, à condition de
> reproduire la garde de 17-05 : un `[WpfFact]` qui compare **l'instance de commande réellement bindée**.

---

### Pattern 7 — Mémoïser la source d'activité, et ne jamais la consulter pour rien (répond à Q5 + contrainte de coût)

Sans mémoïsation, dès que toutes les sources exactes sont muettes (c'est-à-dire l'état **actuel** de la
machine), chaque tick de 60 s paierait **2,9 s / 536 Mo**. Le décorateur résout ça sans toucher au contrat de
la phase 16.

```csharp
// src/Chronos/Services/SourceActiviteMemoisee.cs
namespace Chronos.Services;

/// <summary>
/// Décorateur de <see cref="ITranscriptActivitySource"/> à durée de validité. MESURÉ sur la machine cible :
/// une passe réelle coûte 2,7 à 3,2 s et lit 536 Mo (474 fichiers, 155 171 lignes) — inacceptable au tick de
/// 60 s. Le journal rendu est PUR et interrogeable N fois, donc le réutiliser est gratuit.
///
/// La TTL est aussi la TOLÉRANCE DE CERTIFICATION : un journal dont le Now a 45 s ne dit rien de ces 45 s,
/// donc un « aucune activité » qu'il fonde n'est valable qu'à 45 s près. C'est pour ça que la TTL est courte
/// et qu'elle ne doit pas être réglée par confort de performance.
///
/// Type NEUTRE (aucun type WPF). Ne lève jamais : une panne de l'inner rend le dernier journal connu, ou null.
/// </summary>
public sealed class SourceActiviteMemoisee : ITranscriptActivitySource
{
    public static readonly TimeSpan ValiditeParDefaut = TimeSpan.FromSeconds(60);

    private readonly ITranscriptActivitySource _inner;
    private readonly IClock _horloge;
    private readonly TimeSpan _validite;
    private readonly SemaphoreSlim _verrou = new(1, 1);   // une seule passe concurrente, jamais N
    private TranscriptActivityLog? _journal;

    public SourceActiviteMemoisee(ITranscriptActivitySource inner, IClock horloge, TimeSpan? validite = null)
        => (_inner, _horloge, _validite) = (inner, horloge, validite ?? ValiditeParDefaut);

    public async Task<TranscriptActivityLog> ReadAsync(CancellationToken ct = default)
    {
        var now = _horloge.UtcNow;
        if (_journal is { } j && now - j.Now <= _validite) return j;

        await _verrou.WaitAsync(ct);
        try
        {
            if (_journal is { } j2 && _horloge.UtcNow - j2.Now <= _validite) return j2;  // double vérification
            _journal = await _inner.ReadAsync(ct);
            return _journal;
        }
        finally { _verrou.Release(); }
    }
}
```

**Et la lecture paresseuse côté doctrine** — le point le plus important pour le coût : le décorateur ne doit
appeler `ReadAsync` **que si** au moins une des deux fenêtres en a besoin.

```csharp
// Services/LastExactUsageProvider.cs — esquisse du GetAsync refondu
public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
{
    var snap = await _inner.GetAsync(ct);
    var now = _clock.UtcNow;

    LastExactWindows? memorise = null;
    bool dejaEuUnExact = false;
    try
    {
        if (snap.FiveHour.Reliability == SourceReliability.Exact
            || snap.SevenDay.Reliability == SourceReliability.Exact)
            _store.Save(snap);

        memorise = _store.Load(now);
        dejaEuUnExact = _store.UnExactADejaEteObtenu();
    }
    catch { memorise = null; }   // un magasin en échec ne prive jamais l'utilisateur de son affichage

    // PARESSE : la passe disque (2,9 s) n'est payée que si une fenêtre sort du laissez-passer d'âge.
    // Sans cette garde, le chemin nominal lirait 536 Mo par tick.
    TranscriptActivityLog? journal = null;
    if (ABesoinDuJournal(snap.FiveHour, memorise?.FiveHour, now)
        || ABesoinDuJournal(snap.SevenDay, memorise?.SevenDay, now))
    {
        try { journal = await _activite.ReadAsync(ct); }
        catch { journal = null; }   // source absente -> branche 4, jamais de crash (ROB-01)
    }

    return new UsageSnapshot
    {
        FiveHour = DoctrineFraicheur.Statuer(snap.FiveHour, memorise?.FiveHour, journal, now),
        SevenDay = DoctrineFraicheur.Statuer(snap.SevenDay, memorise?.SevenDay, journal, now),
        SourceCapturedAt = snap.SourceCapturedAt,   // LEGACY gelé (phase 20) : on ne l'invente pas
        UnExactADejaEteObtenu = dejaEuUnExact,
    };
}
```

**`SourceCapturedAt` et `IsStale` : ne rien supprimer en phase 19 (réponse à Q5).** Le par-fenêtre
(`CapturedAt`) est la vérité de la doctrine, et `Provenance` le rend exploitable. Mais supprimer
`SourceCapturedAt` coûterait : 4 providers à modifier, `CompositeUsageProviderTests` cas 5 et 6 à supprimer,
`MainViewModelTests.IsStale_vrai_quand_la_capture_depasse_deux_minutes` à supprimer,
`MainViewModel.CapturedAt`/`IsStale` à retirer — pour **aucune exigence de la phase 19**, et en retirant à la
phase 20 le champ dont EXA-03 était censé partir. **Décision : figer, documenter, déléguer à la phase 20.** Le
commentaire de `UsageSnapshot.SourceCapturedAt` doit être amendé pour dire qu'il est legacy et que la vérité
est par fenêtre — sinon la prochaine personne le croira canonique.

---

### Anti-patterns à éviter

- **Faire de `Best()` un classement par récence.** Deux `Exact` ne se comparent pas par âge : la sonde doit
  gagner parce qu'elle est la seule porteuse de `StatutServeur`/`Depassement` (HDR-03/04). La porte d'âge est
  **binaire**, pas ordinale.
- **Démoter en gardant `Utilization`.** `WindowGaugeViewModel.Apply` fait `Utilization = s.Utilization`
  **sans regarder `Reliability`** (`WindowGaugeViewModel.cs:66`). Une fenêtre `Unavailable` qui garde 0,10
  peint encore l'arc à 10 %. La démotion **doit** annuler `Utilization`.
- **Horodater `ClaudeUsageObjectProvider` avec `now`.** Voir Pitfall 1 : c'est la résurrection du bug.
- **Bumper `LastExactStore.SchemaVersion`.** Trois fixtures épinglent `"version":1` / `"version":999`.
  Tous les ajouts proposés ici sont **additifs hors schéma** (une méthode, pas un champ persisté).
- **Appeler `ReadAsync` deux fois (une par fenêtre).** 5,8 s. La séparation `ReadAsync` / `Since` existe
  précisément pour l'éviter.
- **Introduire un réglage de limite d'âge.** Rejoue `FiveHourBudgetSource: "Manual"`, cause racine du milestone.
- **Réutiliser `EstimatedTokens` pour les tokens du delta.** Ce champ portait la somme de l'estimation absolue
  supprimée ; y rattacher le nouveau chiffre ressuscite une sémantique morte. Champ distinct.
- **Laisser `DoctrineFraicheur` lire une horloge ou un disque.** La pureté est ce qui rend les 4 branches
  testables par `[Fact]` et falsifiables une par une (exigence du CONTEXT).

---

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser | Pourquoi |
|---|---|---|---|
| « Activité depuis T ? » / « tokens depuis T ? » | un second parcours de JSONL | `ITranscriptActivitySource` + `TranscriptActivityLog.Since` | Livré phase 16, 2 classes de tests, gère : écriture concurrente de Claude Code, dernière ligne tronquée, faux positifs de prose, timestamps futurs, `subagents/`, filtre mtime. |
| Savoir si une interrogation est couverte | comparer des dates à la main | `log.Covers(since)` / `log.Horizon` | La constante de 8 j est déclarée **une** fois : filtre de scan == horizon annoncé, par construction. |
| Savoir si une fenêtre mémorisée a roulé | comparer `ResetsAt <= now` dans la doctrine | `LastExactStore.Load(now)` | Le fait déjà, et recalcule `FractionTimeRemaining` — c'est le bornage par fenêtre du delta, gratuit. |
| Convertir une unité d'usage | `p / 100.0`, `double.Parse` | `UsageNormalization` | `NormalisationUniqueTests` **balaie le texte source** et échoue sur toute conversion divergente. Piège `fr-FR` : `double.TryParse("0.63")` rend `false` sans culture invariante. |
| Recalculer la géométrie temporelle | arithmétique de `TimeSpan` locale | `WindowState.FractionRemaining` | Clampe [0..1], rend `null` si reset inconnu. |
| Formater un pourcentage | `ToString("P0")` | `PercentFormatter` / `UsageNormalization.PourcentagePourAffichage` | `P0` insère une espace insécable sous `fr-FR` et casse les assertions littérales existantes. |
| Horloge testable | `DateTimeOffset.UtcNow` | `IClock` + `FakeClock` | Les 4 branches doivent être déterministes ; un test de la branche 2 avec l'horloge système est non reproductible. |
| Sérialisation tolérante | un parseur maison | `System.Text.Json` + les `JsonSerializerOptions` de `LastExactStore` | DEL-06 a établi que `System.Text.Json` ignore les membres non mappés **au niveau du lecteur** : aucun migrateur n'est nécessaire. |

**Intuition-clé :** cette phase **n'écrit presque aucun algorithme neuf**. Les phases 16 et 18 ont livré les
briques exprès. Tout plan qui commence à reparser des JSONL, à recomparer des resets ou à reconvertir des
unités recrée du code déjà gardé par des tests de non-retour — et cassera ces gardes.

---

## Runtime State Inventory

Phase de refonte de doctrine : elle change le **sens** de données déjà persistées. Inventaire explicite.

| Catégorie | Trouvé | Action requise |
|---|---|---|
| **Données stockées** | `%APPDATA%\Chronos\last-exact.json` : **absent** sur la machine réelle (vérifié). Aucun relevé à migrer. Le schéma v1 reste **inchangé** (`captured_at` déjà présent). | **Aucune migration de données.** Mais un correctif de **code** : `LastExactStore.Convertir` doit exiger `CapturedAt` en plus de `Utilization`/`ResetsAt`, sinon il continuera d'écrire des `captured_at: null` incertifiables. Vérifié : les 20 appels de `LastExactStoreTests` passent tous un `captured` ⇒ **tests verts**. |
| **Données stockées (2)** | `%APPDATA%\Chronos\usage.json` : `{"five_hour":{"used_percentage":10,"resets_at":9},"capturedAt":1783666519131}` — le porteur du bug. **Ne pas le supprimer** : c'est le cas de test de production. | Aucune. Le correctif est dans le code (horodatage par fenêtre) ; le fichier doit rester tel quel pour que l'utilisateur constate la bascule « 10 % » → « indisponible ». |
| **Données stockées (3)** | `settings.json` contient encore `FiveHourTokenBudget`, `WeeklyTokenBudget`, `FiveHourBudgetSource`, `WeeklyBudgetSource`, `*CalibratedAt` (obsolètes, ignorés par DEL-06). | Aucune. Les ignorer est un livrable acquis (fixture octet-pour-octet). **Ne pas nettoyer** : la fixture DEL-06 tirerait sa valeur de leur présence. |
| **Configuration de service vivant** | `~/.claude/settings.json` : hooks + statusLine Chronos (phase 15). Le pont statusLine alimente `usage.json`. | Aucune. La phase 19 ne touche ni les hooks ni le pont. |
| **État enregistré par l'OS** | `shell:startup` (autostart). Aucune chaîne de doctrine n'y figure. | Aucune. |
| **Secrets / variables d'environnement** | `%APPDATA%\Chronos\oauth.dat` — **518 octets, mtime 1783863147**, contrôlé avant/après la recherche, **inchangé**. La phase 19 ne le lit pas, ne le déchiffre pas, ne l'écrit pas. | Aucune. Contrôle à **reproduire après chaque tâche** du plan. |
| **Artefacts de build / paquets installés** | `bin/` et `obj/` de `Chronos` et `Chronos.Tests` (Debug + Release). Aucun `.xaml` supprimé dans cette phase ⇒ pas de `*.g.cs` orphelin (piège de la phase 16). | Aucune, sauf si un `.xaml` est finalement supprimé : alors nettoyer **Debug ET Release** puis purger les `*.g.cs`. |

**Question canonique :** *une fois tous les fichiers du dépôt à jour, quel système d'exécution conserve encore
l'ancien comportement ?* → **Un seul : l'exe déjà publié et lancé par l'autostart.** L'utilisateur continuera
de voir « 10 % » jusqu'à republication. À consigner dans le plan comme étape de vérification humaine
(cf. `chronos-versionnage-exe` : embarquer la version **et** la mettre dans le nom du fichier publié).

---

## Common Pitfalls

### Pitfall 1 — Horodater la lecture au lieu de la capture (LE piège de la phase : il ressuscite le bug)

**Ce qui va mal.** `ClaudeUsageObjectProvider.ReadWindow` doit remplir `CapturedAt`. Le réflexe est
`CapturedAt = _clock.UtcNow` — comme le fait légitimement la sonde. Mais ici l'instant de lecture n'a **rien à
voir** avec l'instant de capture : le fichier a été écrit par le pont statusLine il y a **deux mois**. Avec
`now`, le relevé paraît avoir **0 seconde** d'âge, passe la branche 1, et « 10 % » revient — cette fois avec
l'autorité d'une limite d'âge prétendument appliquée. **Pire que le bug d'origine.**

**Pourquoi ça arrive.** Les trois providers à corriger ne sont pas symétriques :

| Provider | Bon `CapturedAt` | Disponible ? |
|---|---|---|
| `RateLimitHeaderUsageProvider` | `now` (réponse HTTP à l'instant) | **déjà fait** (l. 431) |
| `ChronosOAuthUsageProvider` | `now` (réponse HTTP) — et le cache sert l'instance d'origine, donc l'horodatage d'origine | à ajouter |
| `ClaudeOAuthUsageProvider` | `now` (réponse HTTP) | à ajouter |
| `ClaudeUsageObjectProvider` | **l'horodatage du FICHIER** : `capturedAt` (epoch ms), déjà parsé ligne 48-49 en `SourceCapturedAt`. **Absent du fichier ⇒ `null`**, jamais `now`. | à ajouter, **avec la bonne valeur** |

**Prévention.** Un test qui verrouille la distinction, et qui tombe si quelqu'un écrit `now` :

```csharp
[Fact]
public async Task Un_usage_json_vieux_de_deux_mois_porte_l_age_du_FICHIER_et_non_celui_de_la_lecture()
{
    var ecritLe = new DateTimeOffset(2026, 7, 10, 8, 55, 0, TimeSpan.Zero);
    var luLe    = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
    File.WriteAllText(_usageFile,
        $"{{\"five_hour\":{{\"used_percentage\":10,\"resets_at\":{ecritLe.AddHours(3).ToUnixTimeSeconds()}}}," +
        $"\"capturedAt\":{ecritLe.ToUnixTimeMilliseconds()}}}");

    var snap = await new ClaudeUsageObjectProvider(_paths, new FakeClock(luLe)).GetAsync();

    Assert.Equal(ecritLe, snap.FiveHour.CapturedAt);            // l'âge du FICHIER
    Assert.NotEqual(luLe, snap.FiveHour.CapturedAt);            // SURTOUT pas l'instant de lecture
}
```

**Signes d'alerte.** Un `CapturedAt = _clock.UtcNow` dans `ClaudeUsageObjectProvider`. Un test de doctrine qui
passe avec le vrai `usage.json` de production sans rien démoter.

---

### Pitfall 2 — `Exact` + `CapturedAt == null` traité comme frais

Si la doctrine fait `Age(w, now) ?? TimeSpan.Zero`, toute fenêtre sans horodatage passe la branche 1 : le bug
survit. Si elle fait `?? TimeSpan.MaxValue` **sans** corriger les 3 providers, les relevés OAuth légitimes
sont tous démotés : régression fonctionnelle complète. **Les deux moitiés de la correction — `null` est
incertifiable **et** les 3 providers horodatent — doivent être dans la même phase**, idéalement dans des tâches
voisines avec un test qui couvre les deux sens.

---

### Pitfall 3 — La passe de transcripts dans le chemin nominal

**Mesuré : 2,7-3,2 s, 536 Mo, 155 171 lignes JSON-parsées.** Un `ReadAsync` inconditionnel dans `GetAsync`
fait, à 60 s de tick : 536 Mo/min d'E/S et 5 % de charge CPU permanente sur un overlay. Le `RefreshOrchestrator`
sérialise les `GetAsync` sur un consommateur unique ⇒ chaque rafraîchissement est retardé de 3 s (pas de gel
d'UI : l'interpolation est locale, mais le snapshot traîne).

**Prévention :** (1) lecture **paresseuse** (garde `ABesoinDuJournal`) ; (2) **mémoïsation** TTL ; (3) une
limite d'âge **au-dessus** du cache de la sonde pour que le chemin nominal soit gratuit ; (4) un test qui
compte les appels à `ReadAsync` : **0** quand tout est frais, **1** (pas 2) quand les deux fenêtres en ont
besoin.

---

### Pitfall 4 — `WeeklyRecalibration` écrase un `resets_at` réel

`Apply` rend la fenêtre inchangée seulement si `Reliability == Exact && ResetsAt is not null`
(`WeeklyRecalibration.cs:31`). Une fenêtre hebdo passée en `Estimated` (plancher) **avec** un `resets_at`
serveur tombe donc dans le chemin de synthèse et son reset réel est **remplacé** par
`ancre + n×7 j`. Régression silencieuse : le compte à rebours hebdo devient une supposition alors que le
serveur avait donné la réponse.

**Correctif (une ligne)** : la garde doit porter sur le `ResetsAt`, pas sur la fiabilité — le recalibrage
existe pour les sources qui **n'exposent pas** de reset hebdo :

```csharp
// Un resets_at connu est un FAIT, quelle que soit la fraîcheur du pourcentage qui l'accompagne :
// on ne le remplace jamais par une synthèse (phase 19 — les fenêtres corrigées par delta en portent un).
if (weekly.ResetsAt is not null) return weekly;
```

**Vérifié : les 7 `WeeklyRecalibrationTests` restent verts** — le seul cas à `ResetsAt` non-null est
`Exact_avec_reset_reste_inchange`, et les 6 cas « Repli » passent tous `ResetsAt = null`.
Et le chemin « hebdo `Unavailable` + ancre ⇒ reset synthétisé, `Reliability` conservée » reste intact : il
recevra **plus** de trafic après la phase 19, donc mérite un test neuf.

---

### Pitfall 5 — Un champ de `UsageSnapshot` détruit par la recomposition

`CompositeUsageProvider.GetAsync` fait `new UsageSnapshot { FiveHour, SevenDay, SourceCapturedAt }`. Tout champ
ajouté **sous** le composite est perdu. C'est documenté (phase 18) mais ce n'est **pas gardé** : rien ne le
signale au prochain qui ajoutera un champ.

**Ne pas « corriger » par `with`** : `p with { … }` ferait hériter silencieusement du **primaire** tout champ
futur — une valeur fausse plutôt qu'un `null`, c'est pire. **Garder le `new` explicite et ajouter une garde**,
dans le style établi (`NormalisationUniqueTests`, la garde de texte source de la sonde) :

```csharp
[Fact]
public void Toute_propriete_de_UsageSnapshot_est_explicitement_recomposee_par_le_composite()
{
    // POURQUOI : GetAsync reconstruit le record par « new » et non par « with ». Un champ ajouté à
    // UsageSnapshot sans être nommé ici serait DÉTRUIT à chaque passage de composite, en silence.
    // Le chemin des sources est injecté par MSBuild (AssemblyMetadata) — jamais Assembly.Location.
    var racine = typeof(CompositeUsageProviderTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(a => a.Key == "CheminSourcesChronos").Value!;
    var texte = File.ReadAllText(Path.Combine(racine, "Services", "CompositeUsageProvider.cs"));

    var oubliees = typeof(UsageSnapshot).GetProperties()
        .Select(p => p.Name)
        .Where(n => !texte.Contains(n, StringComparison.Ordinal))
        .ToList();

    Assert.Empty(oubliees);
}
```

C'est la **modification minimale et justifiée** de `CompositeUsageProvider` que cette phase apporte : pas une
injection d'horloge, mais la fin du piège silencieux — plus un commentaire d'en-tête disant où vit désormais la
doctrine. Le nouveau champ `UnExactADejaEteObtenu` passe la garde s'il est nommé dans un commentaire
expliquant qu'il est posé **au-dessus** — ce qui est exactement la documentation qu'on veut forcer.

---

### Pitfall 6 — Démoter en laissant le pourcentage sur la fenêtre

`WindowGaugeViewModel.Apply` (l. 66) : `Utilization = s.Utilization`, **sans consulter `Reliability`**. Une
fenêtre `Unavailable` qui conserve `Utilization = 0.10` peint l'arc à 10 % et le thème lui donne sa couleur.
Seul `UtilizationText` s'efface (`HasUtilizationText = s.Utilization is not null`… qui serait **vrai**). La
démotion **doit** annuler `Utilization`. Test : `Assert.Null(snap.FiveHour.Utilization)` **et**
`Assert.Equal(0.0, gauge.FractionRemaining)`-style sur le VM.

---

### Pitfall 7 — Casser la compilation en changeant un ctor

`tests/Chronos.Tests` a un `ProjectReference` vers `Chronos` : **une** erreur de compilation rend
`dotnet test` entièrement non invocable. Inventaire des sites à déplacer dans la **même tâche** :

| Signature changée | Sites |
|---|---|
| `LastExactUsageProvider(…, + ITranscriptActivitySource)` | `App.xaml.cs:339` · `CompositionRootTests.cs:57` · `CompositionRootTests.cs:326` · `LastExactUsageProviderTests.Deco()` l. 53 |
| `PercentFormatter.Format` (si **surcharge** → 0 site ; si **signature modifiée** → 3) | `WindowGaugeViewModel.cs:73` · `CadranPreviewViewModel.cs:70` · 4 assertions `WindowGaugeViewModelTests.cs:43-56` |
| `CompositeUsageProvider(… + IClock, TimeSpan)` **si l'alternative est retenue** | **22 sites** : `App.xaml.cs` ×3, `CompositeUsageProviderTests` ×16, `CompositionRootTests` ×3 |

**Recommandation** : surcharges additives partout où c'est possible, **sauf** pour le 4ᵉ argument du décorateur
— là, un paramètre **obligatoire** est préférable à un `= null` optionnel, sans quoi le site de production
pourrait rester sans source d'activité et la doctrine dégraderait en branche 4 **en silence** (le bug de forme
déjà rencontré en 17-03 : « un `TokenRefreshService` que le host ne démarre jamais ne rafraîchit rien »).

---

### Pitfall 8 — Les tests de caractérisation qu'on supprime au lieu de réécrire

Six des 14 `CompositeUsageProviderTests` ont été écrits en phase 18 pour documenter le comportement **actuel**.
Avec la conception recommandée, ils **ne changent pas de sens** (le composite n'est pas touché) — mais ils
décrivent désormais un composite qui **ignore délibérément** l'âge. Il faut **amender leurs commentaires** pour
dire où la doctrine est passée, sinon le prochain lecteur croira que le composite arbitre la fraîcheur.
Les supprimer serait perdre la preuve que `StatutServeur`/`Depassement` traversent la chaîne (HDR-03/04).

---

### Pitfall 9 — Déclarer la sonde coupable du 401

Sur cette machine le jeton est mort depuis le 2026-07-12 : la chaîne exacte est **structurellement complète et
fonctionnellement muette**. Un test de doctrine qui « échoue » parce qu'aucune source ne répond ne teste pas la
doctrine. Tous les tests de la phase doivent passer par `FakeUsageProvider` / `FakeTranscriptActivitySource` /
`FakeClock` — **jamais** par la vraie chaîne. Le seul artefact réel autorisé est le **magasin** et
l'`usage.json`, sous `Path.GetTempPath()`.

---

### Pitfall 10 — La parallélisation xUnit et le chargeur BAML

Si un test de la phase charge du XAML (`[WpfFact]` pour l'invite EXA-05), il doit rejoindre la collection
`XAML WPF` (`DisableParallelization`). Cause établie en phase 16 : une course du chargeur BAML
(`WpfXamlType.FindKnownMember`) sous parallélisme. Et rappel de 17-05 : une fenêtre WPF **jamais affichée** n'a
pas de parent visuel pour son `Content`, donc le `DataContext` ne se propage pas et **aucun binding ne
s'évalue** (`Command` null, `Visibility` = Visible par défaut) — parade : `DataContext` sur la grille racine +
purge du `Dispatcher`.

---

## Code Examples

### 1. Les quatre branches, en quatre tests falsifiables

```csharp
// tests/Chronos.Tests/DoctrineFraicheurTests.cs
public class DoctrineFraicheurTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static WindowState Exact(double util, DateTimeOffset captured, DateTimeOffset? resets = null)
        => new() { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
                   Utilization = util, CapturedAt = captured, ResetsAt = resets ?? Now.AddHours(3) };

    private static TranscriptActivityLog Journal(DateTimeOffset now, params (DateTimeOffset, long)[] e)
        => new(now, now - TimeSpan.FromDays(8), e);

    // BRANCHE 1 — frais : affiché tel quel, et AUCUN journal n'est nécessaire.
    [Fact]
    public void Releve_sous_la_limite_est_exact_frais_sans_consulter_les_transcripts()
    {
        var w = DoctrineFraicheur.Statuer(Exact(0.42, Now.AddMinutes(-1)), null, journal: null, Now);
        Assert.Equal(SourceReliability.Exact, w.Reliability);
        Assert.Equal(ProvenanceReleve.Frais, w.Provenance);
        Assert.Equal(0.42, w.Utilization);
    }

    // BRANCHE 2 — DEL-03, le cœur du milestone : 3 h, zéro activité, donc ENCORE EXACT.
    [Fact]
    public void Trois_heures_sans_aucune_activite_reste_EXACT_et_non_perime()
    {
        var t = Now.AddHours(-3);
        var memorise = Exact(0.42, t);
        var journal = Journal(Now, (t.AddHours(-1), 5_000));   // activité AVANT T seulement

        var w = DoctrineFraicheur.Statuer(WindowState.Unavailable(WindowKind.FiveHour), memorise, journal, Now);

        Assert.Equal(SourceReliability.Exact, w.Reliability);          // « encore exact », pas « périmé »
        Assert.Equal(ProvenanceReleve.EncoreValide, w.Provenance);
        Assert.Equal(0.42, w.Utilization);                              // valeur INCHANGÉE
        Assert.Equal(0L, w.TokensDepuisReleve);
    }

    // BRANCHE 3 — DEL-04 : activité depuis T -> PLANCHER marqué, valeur JAMAIS gonflée (EXA-04).
    [Fact]
    public void Activite_depuis_le_releve_donne_un_plancher_marque_sans_gonfler_le_chiffre()
    {
        var t = Now.AddHours(-3);
        var journal = Journal(Now, (t.AddMinutes(10), 120_000), (Now.AddMinutes(-5), 880_000));

        var w = DoctrineFraicheur.Statuer(WindowState.Unavailable(WindowKind.FiveHour), Exact(0.42, t), journal, Now);

        Assert.Equal(SourceReliability.Estimated, w.Reliability);       // jamais confondu avec un exact
        Assert.Equal(ProvenanceReleve.PlancherAvecActivite, w.Provenance);
        Assert.Equal(0.42, w.Utilization);                              // EXA-04 : AUCUN delta ajouté
        Assert.Equal(1_000_000L, w.TokensDepuisReleve);                 // matière brute, non convertie
    }

    // BRANCHE 4a — le journal ne couvre pas T (piège Horizon) : on REFUSE, on n'invente pas.
    [Fact]
    public void Releve_anterieur_a_l_horizon_des_transcripts_est_indisponible_et_non_sous_evalue()
    {
        var t = Now.AddDays(-60);                                       // le 10 juillet réel
        var journal = Journal(Now);                                     // horizon = now - 8 j
        Assert.False(journal.Covers(t));                                // la prémisse, rendue explicite

        var w = DoctrineFraicheur.Statuer(WindowState.Unavailable(WindowKind.FiveHour), Exact(0.10, t), journal, Now);

        Assert.Equal(SourceReliability.Unavailable, w.Reliability);
        Assert.Null(w.Utilization);                                     // le « 10 % » de deux mois MEURT ICI
        Assert.Null(w.Provenance);
    }

    // BRANCHE 4b — EXA-02 : un Exact VIVANT sans horodatage est incertifiable, jamais « frais ».
    [Fact]
    public void Un_exact_vivant_sans_CapturedAt_est_incertifiable_et_ne_passe_pas_pour_frais()
    {
        var sansDate = new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
                                         Utilization = 0.10, CapturedAt = null };

        var w = DoctrineFraicheur.Statuer(sansDate, null, journal: null, Now);

        Assert.Equal(SourceReliability.Unavailable, w.Reliability);
        Assert.Null(w.Utilization);
    }
}
```

### 2. Le fake manquant

```csharp
// tests/Chronos.Tests/Fakes/FakeTranscriptActivitySource.cs — n'existe pas encore
internal sealed class FakeTranscriptActivitySource : ITranscriptActivitySource
{
    private int _lectures;
    public int Lectures => Volatile.Read(ref _lectures);

    /// <summary>Journal rendu. null = la source échoue (ou n'a rien) -> la doctrine doit dégrader.</summary>
    public TranscriptActivityLog? Journal;

    public Task<TranscriptActivityLog> ReadAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _lectures);
        return Journal is null
            ? Task.FromException<TranscriptActivityLog>(new IOException("source indisponible"))
            : Task.FromResult(Journal);
    }
}
```

### 3. Le test de coût — il garde la mesure de 2,9 s

```csharp
[Fact]
public async Task Chemin_nominal_ne_lit_JAMAIS_les_transcripts()
{
    // POURQUOI CE TEST : une passe réelle coûte 2,7-3,2 s et lit 536 Mo (mesuré le 2026-09-12 sur la
    // machine cible, 474 fichiers / 155 171 lignes). Au tick de 60 s, une lecture inconditionnelle serait
    // 536 Mo/min d'E/S permanente. La paresse n'est pas une optimisation, c'est une contrainte de forme.
    var activite = new FakeTranscriptActivitySource { Journal = Journal(Now) };
    var inner = new FakeUsageProvider { Next = Snap(Exact(0.42, Now), Exact(0.63, Now)) };   // tout frais

    await new LastExactUsageProvider(inner, _store, _clock, activite).GetAsync();

    Assert.Equal(0, activite.Lectures);
}

[Fact]
public async Task Les_deux_fenetres_degradees_partagent_UNE_SEULE_passe_disque()
{
    var t = Now.AddHours(-3);
    _store.Save(Snap(Exact(0.42, t), Exact(0.63, t)));
    var activite = new FakeTranscriptActivitySource { Journal = Journal(Now) };

    await new LastExactUsageProvider(new FakeUsageProvider { Next = UsageSnapshot.Empty },
                                    _store, _clock, activite).GetAsync();

    Assert.Equal(1, activite.Lectures);   // 1 et non 2 : c'est la raison d'être de ReadAsync / Since
}
```

### 4. La garde de non-retour EXA-04 (balayage de texte source, motif HDR-05)

```csharp
[Fact]
public void Aucune_conversion_de_tokens_en_utilization_dans_la_couche_de_donnees()
{
    // EXA-04 devient STRUCTUREL, pas seulement documenté. La phase 16 a déjà interdit aux transcripts
    // d'implémenter IUsageProvider ; cette garde interdit l'autre voie : un taux points/token, c'est-à-dire
    // « tokens / plafond » avec un plafond auto-calibré — donc BudgetAutoCalibrator ressuscité.
    // Falsifiabilité à PROUVER par mutation réelle avant de committer (motif 18-01), puis révoquer.
    var racine = /* AssemblyMetadata CheminSourcesChronos */;
    var suspects = Directory.EnumerateFiles(Path.Combine(racine, "Services"), "*.cs")
        .Concat(Directory.EnumerateFiles(Path.Combine(racine, "Models"), "*.cs"))
        .Where(f => Regex.IsMatch(File.ReadAllText(f),
            @"(Tokens?\w*\s*[/*]\s*\w*(Plafond|Budget|Limite|Capacite))|((Plafond|Budget)\w*\s*[/*]\s*\w*Tokens?)",
            RegexOptions.IgnoreCase))
        .Select(Path.GetFileName)
        .ToList();

    Assert.Empty(suspects);
}
```

---

## Impact nominatif sur les 652 tests (réponse à Q7)

Hypothèse : conception recommandée (doctrine dans le décorateur, `CompositeUsageProvider` quasi intouché).

### A. Restent verts SANS aucune édition

| Fichier | Tests | Raison |
|---|---|---|
| `CompositeUsageProviderTests.cs` | **14 / 14** | Le composite n'est pas modifié comportementalement. **Les 6 tests de caractérisation de la phase 18 gardent leur sens** (transmission par référence de `StatutServeur`/`Depassement`) ; seuls leurs **commentaires** doivent être amendés (Pitfall 8). |
| `LastExactStoreTests.cs` | **20** | `SchemaVersion` inchangé ; les 3 fixtures `"version":1`/`"version":999` intactes ; l'ajout de `UnExactADejaEteObtenu()` est additif ; l'exigence neuve de `CapturedAt` à la persistance est **déjà satisfaite** par le helper `Exact(...)` de ce fichier (vérifié l. 36-43). |
| `WeeklyRecalibrationTests.cs` | **7 / 7** | Le correctif de garde (`ResetsAt is not null`) ne change le verdict d'aucun cas existant : le seul cas à reset non-null est `Exact_avec_reset_reste_inchange` (vérifié). |
| `WindowGaugeViewModelTests.cs` | **~20** | `Provenance`/`TokensDepuisReleve` sont `init` + nullables ⇒ les 13 `new WindowState` compilent. Les 4 assertions `PercentFormatter` survivent **si** la surcharge est additive. |
| `CadranBindingTests.cs` · `WindowStateTests.cs` · `ThemingTests.cs` · `ReglagesBindingTests.cs` | ~25 | Idem : aucun champ `required`, aucune signature touchée. |
| `MainViewModelTests.cs` | ~40 | `SourceCapturedAt` et `IsStale` **gelés** (Pattern 7) ⇒ `IsStale_vrai_quand_la_capture_depasse_deux_minutes` et `DataUnavailable_vrai_seulement_si…` restent verts. |
| `TranscriptActivityLogTests.cs` · `TranscriptActivityProviderTests.cs` | ~25 | Contrat de la phase 16 **consommé**, jamais modifié. |
| `ServicesLayerPurityTests.cs` | 2 | Nouveaux types **neutres** (zéro `System.Windows.*`) et aucun nom `Budget*`. |
| `DiagnosticServiceTests.cs` | 2 concernés | Aucune assertion littérale sur `Describe()` (vérifié par grep : aucun test ne contient `"EXACT —"` ni `"estimé —"`). Signature de `DiagnosticService` **non touchée** (elle est gelée : 10 sites). |
| `RateLimitHeaderUsageProviderTests.cs` · `ChronosOAuthUsageProviderTests.cs` · `ClaudeOAuthUsageProviderTests.cs` · `ClaudeUsageObjectProviderTests.cs` | ~120 | **À VÉRIFIER** : ajouter `CapturedAt` à un `WindowState` produit peut casser un `Assert.Equal(attendu, snap.FiveHour)` par égalité de **record entier**. Aucun repéré, mais c'est le risque résiduel n°1 de l'inventaire — à confirmer au premier `dotnet test` de la phase. |

### B. Changent de sens — à RÉÉCRIRE (jamais à supprimer)

| Test | Aujourd'hui | Après | Verdict |
|---|---|---|---|
| `LastExactUsageProviderTests.Rebouche_une_fenetre_indisponible_depuis_le_magasin` (l. 96) | Un relevé capturé **2 h avant** est substitué et asserté `Exact` **sans aucune question sur l'activité**. | Devient **deux** tests : branche 2 (journal sans activité ⇒ `Exact`/`EncoreValide`) et branche 3 (journal avec activité ⇒ `Estimated`/`PlancherAvecActivite`). | **Réécrire** — c'est la trace exécutable du changement de doctrine, le cœur de DEL-03/04. |
| `LastExactUsageProviderTests.Fenetre_estimee_n_est_ni_rebouchee_ni_ecrite` (l. 154) | Injecte un `Estimated` **vivant** — impossible en production depuis la phase 16, et `Estimated` change de sens. | Réécrire en « un **plancher** n'est jamais persisté comme exact » (garantie de non-contamination du magasin). | **Réécrire.** |
| `LastExactUsageProviderTests.Magasin_corrompu_ne_leve_pas…` (l. 179) | Assertion `Estimated` sur l'entrée de l'inner. | Même propriété (robustesse) mais entrée `Exact` fraîche : un magasin illisible **ne prive pas** de l'affichage. | **Réécrire** (l'intention est conservée, l'entrée change). |
| `LastExactUsageProviderTests` — 6 autres (l. 59, 79, 116, 137, 198, 211) | — | Inchangés **sauf** le 4ᵉ argument de ctor (helper `Deco()` l. 53). | **Adapter le helper** (1 ligne). |
| `CompositionRootTests.Host_resout_et_dispose_les_singletons` (l. 108) · `La_sonde_…_PRIMAIRE` (l. 348) | `Assert.IsType<LastExactUsageProvider>` | Verts **si le nom de classe est conservé**. Sinon 2 éditions. | **Conserver le nom** ; ajouter le 4ᵉ argument aux 2 sites de construction (l. 57, 326). |

### C. À CRÉER

| Fichier | Contenu | Tests estimés |
|---|---|---|
| `DoctrineFraicheurTests.cs` | 4 branches + `CapturedAt` null + fenêtre roulée + bornage par fenêtre + « le plancher ne gonfle jamais » | ~12 |
| `SourceActiviteMemoiseeTests.cs` | TTL respectée, une seule passe concurrente, expiration, panne de l'inner non levante | ~5 |
| `Fakes/FakeTranscriptActivitySource.cs` | compteur de lectures + journal programmable + mode panne | — |
| `GardesDoctrineTests.cs` | recomposition de `UsageSnapshot` (Pitfall 5) · non-retour tokens→% (EXA-04) · repli le plus interne (fragilité du Pattern 2) | ~3 |
| Compléments dans `ClaudeUsageObjectProviderTests.cs` | l'âge vient du **fichier**, jamais de la lecture (Pitfall 1) | ~2 |
| Compléments dans `LastExactUsageProviderTests.cs` | paresse (0 lecture si frais) · une seule passe pour deux fenêtres · `UnExactADejaEteObtenu` propagé | ~4 |
| Compléments dans `MainViewModelTests.cs` | EXA-05 : signal d'invitation allumé **uniquement** si jamais-d'exact **et** indisponible | ~3 |
| Compléments dans `WeeklyRecalibrationTests.cs` | un `resets_at` réel porté par un plancher n'est **pas** écrasé · `Unavailable` + ancre reste recalibrable | ~2 |

**Projection : 652 → ~680-690 tests.** Le critère de sortie n'est **pas** « ≥ 652 » mais **0 échec + chaque
suppression/réécriture justifiée nominativement** (précédent établi en 16-01 : le recul 405 → 384 était le
livrable).

---

## State of the Art

| Ancien comportement | Nouveau comportement | Quand | Conséquence |
|---|---|---|---|
| `Best()` classe par seule fiabilité ⇒ un `Exact` de 2 mois bat tout | La fiabilité d'une fenêtre est **conditionnée à sa certifiabilité** avant d'entrer dans le classement | phase 19 | Le bug des « 10 % » meurt, et par `Covers(T)` plutôt que par une constante arbitraire. |
| `Estimated` = estimation absolue `tokens / plafond` | `Estimated` = **plancher** d'un relevé exact daté | phases 16 → 19 | Aucun câblage de présentation neuf ; la sémantique passe de « à peu près » à « au moins ». |
| Incertitude **symétrique** (« ~80 % ») | Incertitude **unilatérale** (« ≥ 80 % ») | phase 19 | Impose à la phase 20 un vocabulaire visuel unilatéral : pas de barre d'erreur, pas d'arc de delta. |
| Fraîcheur au niveau **snapshot** (`SourceCapturedAt`, `IsStale` 2 min) | Fraîcheur **par fenêtre** (`CapturedAt` + `Provenance`) | phase 16 → 19 → 20 | Deux vocabulaires coexistent temporairement ; `IsStale` n'est bindé nulle part (vérifié), la phase 20 unifie. |
| Plafonds de tokens calibrés (`BudgetCalibration`) | **Supprimés**, gardés par un test de non-retour | phase 16 | La tentation de calibrer un taux points/token doit être interdite **structurellement**, pas seulement documentée. |

**Obsolète / à ne pas ressusciter :** `BudgetCalibration`, `BudgetAutoCalibrator`, `BudgetSource`,
`BudgetDialog` (gardés par `Aucun_type_de_plafond_ne_subsiste_dans_l_assembly`) · `WindowState.EstimatedTokens`
devient mort en production (3 usages de test + 2 lectures du gauge) — **candidat de suppression phase 20**,
hors périmètre ici.

---

## Open Questions

### 1. La branche 1 tolère jusqu'à 6 minutes d'activité non vérifiée — acceptable ?

- **Ce qu'on sait** : le laissez-passer existe pour éviter la passe disque de 2,9 s ; en régime nominal un
  relevé a < 60 s (tick 60 s) ; la limite ne mord que si toutes les sources tombent pendant 6 min.
- **Ce qui reste flou** : en 6 min d'Opus intensif, l'utilisation réelle peut dépasser le chiffre affiché de
  plusieurs points, sans marque.
- **Recommandation** : accepter (c'est une décision verrouillée du CONTEXT : « relevé dont l'âge est sous la
  limite : affiché tel quel »), **mais** consigner qu'une limite plus serrée est strictement plus honnête et
  que son seul coût est la fréquence de la passe disque. Si la phase 20 livre l'optimisation de lecture en
  queue de fichier, la limite pourra descendre à ~2 min et converger avec `IsStale`.

### 2. Faut-il un `IEtatDoctrine` dès maintenant, pour EXA-06 ?

- **Ce qu'on sait** : le bit EXA-05 voyage sans danger sur `UsageSnapshot` (posé au-dessus du composite).
  EXA-06 (phase 20) demandera « quelle source alimente l'affichage » — or `Provenance` dit *quel état*, pas
  *quelle source* (sonde / endpoint OAuth / pont statusLine / magasin).
- **Ce qui reste flou** : le nom de la source n'est aujourd'hui porté par **aucun** champ. Il faudra soit un
  `string`/enum sur `WindowState` (posé par chaque provider), soit un canal latéral.
- **Recommandation** : **ne pas le livrer en phase 19** (hors périmètre, et un champ mal conçu est plus coûteux
  que pas de champ) mais **écrire dans le SUMMARY** que `Provenance` ne répond qu'à la moitié d'EXA-06 et que
  l'autre moitié (nom de la source) reste à concevoir. Sinon la phase 20 découvrira le trou tard.

### 3. L'invite EXA-05 : nouvelle pastille ou réutilisation de l'existante ?

- **Ce qu'on sait** : la pastille de déconnexion existe, est bindée à `ReconnecterCommand` (qui ne déconnecte
  jamais), et est verrouillée par un `[WpfFact]` comparant l'instance de commande. `EtatAuthentification.NonConnecte`
  a été **explicitement réservé** à EXA-05 par la phase 17.
- **Ce qui reste flou** : trois pastilles (déconnecté / hors ligne / jamais connecté) sur un cadran compact —
  la phase 20 porte les tokens de design.
- **Recommandation** : phase 19 livre le **signal VM + son test** et, si le budget le permet, un troisième
  booléen sur la pastille existante avec la même garde `[WpfFact]`. L'apparence finale est phase 20.

### 4. Y a-t-il des tests de providers qui comparent des `WindowState` par égalité de record ?

- **Ce qu'on sait** : `WindowStateTests` l. 107-108 construit deux instances identiques (probablement pour
  tester l'égalité structurelle du record). Ajouter `CapturedAt` à un provider change la valeur du record.
- **Ce qui reste flou** : aucun `Assert.Equal(attenduWindowState, obtenu)` n'a été repéré dans les 4 fichiers
  de tests de providers, mais la recherche n'a pas été exhaustive sur les ~120 tests concernés.
- **Recommandation** : première tâche du plan = ajouter `CapturedAt` aux 3 providers, **lancer la suite**, et
  traiter la liste d'échecs comme l'inventaire réel. Coût : 2 min 15 s. Ne pas planifier autour d'une
  hypothèse quand la mesure coûte deux minutes.

### 5. Le coût du `SemaphoreSlim` du mémoïseur est-il justifié ?

- **Ce qu'on sait** : `RefreshOrchestrator` a un consommateur **unique** ⇒ pas d'appel concurrent par ce
  chemin. Mais `DiagnosticService` appelle aussi `IUsageProvider.GetAsync` (1 fois, contrainte gravée par un
  grep) et peut le faire pendant un tick.
- **Recommandation** : garder le sémaphore (motif `ChronosTokenAuthority`, déjà éprouvé). Deux passes
  concurrentes = 1 072 Mo lus simultanément ; le coût du verrou est nul en comparaison.

---

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|---|---|---|---|---|
| .NET SDK | build / test | ✓ | **10.0.201** (cible `net8.0-windows`) | — |
| xunit + Xunit.StaFact | tests | ✓ | 2.9.2 / 1.1.11 | — |
| `%APPDATA%\Chronos\` | magasin, usage.json, réglages | ✓ | `last-exact.json` **absent**, `usage.json` présent (77 o), `settings.json` présent | — |
| `%USERPROFILE%\.claude\projects` | source de delta | ✓ | **830 fichiers / 923 Mo** ; 474 / 536 Mo dans l'horizon 8 j | Source absente ⇒ branche 4 (comportement testé) |
| `oauth.dat` | **NON requis par cette phase** | présent, **518 o, mtime 1783863147, inchangé** | — | — |
| Réseau Anthropic | **NON requis** — aucun test réseau | — | — | Tout passe par `FakeHttpMessageHandler` |

**Aucune dépendance manquante. Aucun repli nécessaire.** La phase est purement locale : zéro requête réseau,
zéro lecture de secret.

---

## Validation Architecture

### Test Framework

| Propriété | Valeur |
|---|---|
| Framework | xunit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]` pour le XAML) |
| Fichier de config | aucun (`Chronos.Tests.csproj`, `IsTestProject=true`) ; `XamlWpfCollection.cs` porte la collection `DisableParallelization` |
| Commande rapide | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Release --filter "FullyQualifiedName~DoctrineFraicheur\|FullyQualifiedName~LastExact\|FullyQualifiedName~Composite" --nologo` |
| Commande complète | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Release --nologo` — **652 verts, 2 min 15 s** (mesuré) |

### Exigences de phase → carte des tests

| Req | Comportement | Type | Commande automatisée | Fichier existe ? |
|---|---|---|---|---|
| EXA-02 | Relevé au-delà de la limite non certifiable ⇒ cesse d'être `Exact` | unit | `--filter "FullyQualifiedName~DoctrineFraicheurTests"` | ❌ Wave 0 |
| EXA-02 | `Exact` sans `CapturedAt` ⇒ incertifiable (ni « frais », ni démotion des sources légitimes) | unit | idem + `~ClaudeUsageObjectProviderTests` | ❌ Wave 0 (doctrine) / ✅ (fichier provider) |
| EXA-02 | Un `usage.json` vieux de 2 mois porte l'âge **du fichier** | unit | `--filter "FullyQualifiedName~ClaudeUsageObjectProviderTests"` | ✅ (tests à ajouter) |
| EXA-04 | Le plancher ne gonfle **jamais** `Utilization` | unit | `--filter "FullyQualifiedName~DoctrineFraicheurTests"` | ❌ Wave 0 |
| EXA-04 | Aucune conversion tokens → utilization dans `Services/`+`Models/` (balayage de texte, falsifiable par mutation) | guard | `--filter "FullyQualifiedName~GardesDoctrineTests"` | ❌ Wave 0 |
| EXA-05 | Jamais d'exact ⇒ indisponible + invite ; jamais de pourcentage | unit | `--filter "FullyQualifiedName~LastExactStoreTests\|FullyQualifiedName~MainViewModelTests"` | ✅ (tests à ajouter) |
| EXA-05 | Distinguer « jamais eu » de « eu, fenêtre roulée » | unit | `--filter "FullyQualifiedName~LastExactStoreTests"` | ✅ |
| DEL-03 | Zéro activité depuis T ⇒ **encore exact** (y compris à 3 h) | unit | `--filter "FullyQualifiedName~DoctrineFraicheurTests"` | ❌ Wave 0 |
| DEL-04 | Activité ⇒ plancher marqué + tokens bruts, jamais confondu avec un exact | unit | idem | ❌ Wave 0 |
| DEL-04 | `!Covers(T)` ⇒ refus, jamais un delta sous-évalué | unit | idem | ❌ Wave 0 |
| DEL-04 | Bornage par fenêtre : fenêtre roulée ⇒ jamais de plancher | unit | idem + `~LastExactStoreTests` | ❌ Wave 0 / ✅ |
| (coût) | 0 lecture de transcripts en nominal ; 1 seule pour deux fenêtres dégradées | unit | `--filter "FullyQualifiedName~LastExactUsageProviderTests"` | ✅ (tests à ajouter) |
| (non-régression) | Garde de position de la sonde + décorateur en tête | intégration | `--filter "FullyQualifiedName~CompositionRootTests"` | ✅ |
| (non-régression) | Pureté Services/Models + non-retour `Budget*` | guard | `--filter "FullyQualifiedName~ServicesLayerPurityTests"` | ✅ |
| (non-régression) | Point unique de normalisation | guard | `--filter "FullyQualifiedName~NormalisationUniqueTests"` | ✅ |
| (non-régression) | Recomposition de `UsageSnapshot` par le composite | guard | `--filter "FullyQualifiedName~GardesDoctrineTests"` | ❌ Wave 0 |
| (sécurité) | `oauth.dat` = 518 o / mtime 1783863147, avant **et** après chaque tâche | manuel | `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` | — |

### Fréquence d'échantillonnage

- **Par commit de tâche** : commande rapide (filtrée) — quelques secondes. Plus le contrôle `oauth.dat`.
- **Par fusion de vague** : suite complète (2 min 15 s). Critère : **0 échec**, et toute variation du nombre de
  tests justifiée **nominativement**.
- **Porte de phase** : suite complète verte + le tableau § « Ce que la chaîne produit aujourd'hui » vérifié **à
  la main sur l'exe publié** (« 10 % » → « indisponible + connecte-toi »). Aucune vérification automatique ne
  peut prouver ce basculement : il dépend de l'`usage.json` réel de la machine.

### Lacunes de Wave 0

- [ ] `tests/Chronos.Tests/Fakes/FakeTranscriptActivitySource.cs` — **n'existe pas**, et rien ne peut tester la
      doctrine sans lui.
- [ ] `tests/Chronos.Tests/DoctrineFraicheurTests.cs` — les 4 branches (EXA-02, DEL-03, DEL-04), chacune
      supprimable pour prouver qu'un test tombe.
- [ ] `tests/Chronos.Tests/GardesDoctrineTests.cs` — recomposition de `UsageSnapshot` + non-retour tokens→%
      (falsifiabilité à **prouver par mutation réelle puis révoquer**, motif 18-01).
- [ ] `tests/Chronos.Tests/SourceActiviteMemoiseeTests.cs` — TTL et unicité de passe.
- [ ] **Tâche zéro recommandée** : ajouter `CapturedAt` aux 3 providers puis lancer la suite complète, et
      prendre la liste d'échecs comme inventaire réel (Open Question 4). 2 min 15 s contre une journée
      d'hypothèses.

---

## Sources

### Primaires (HIGH) — code réel du dépôt, lu intégralement
- `src/Chronos/Services/CompositeUsageProvider.cs` — `Best()`, `Rank()`, recomposition par `new`
- `src/Chronos/Services/LastExactUsageProvider.cs` · `LastExactStore.cs` — position, écrivain unique, `Convertir`/`Reconstruire`, `SchemaVersion`
- `src/Chronos/Services/ITranscriptActivitySource.cs` · `TranscriptActivityProvider.cs` — `Since`/`Covers`/`Horizon`, `HorizonSpan = 8 j`
- `src/Chronos/Services/RateLimitHeaderUsageProvider.cs` — `CadenceNominale = 300 s`, `CacheUtilisable = 300 s`, `CapturedAt = now` (l. 431)
- `src/Chronos/Services/ChronosOAuthUsageProvider.cs` — `CacheUsable = 15 min`, **pas de `CapturedAt`** (l. 178-187)
- `src/Chronos/Services/ClaudeOAuthUsageProvider.cs` — **pas de `CapturedAt`** (l. 136-145)
- `src/Chronos/Services/ClaudeUsageObjectProvider.cs` — **pas de `CapturedAt`**, `capturedAt` du fichier en ms (l. 48-49)
- `src/Chronos/Services/UsageNormalization.cs` — point unique, `PlancherEpoch`
- `src/Chronos/Services/WeeklyRecalibration.cs` — garde `Exact && ResetsAt != null` (l. 31)
- `src/Chronos/Models/{UsageSnapshot,WindowState,SourceReliability,WindowKind}.cs`
- `src/Chronos/App.xaml.cs` l. 249-378 — chaîne de 3 composites imbriqués
- `src/Chronos/ViewModels/MainViewModel.cs` l. 290-370 · `WindowGaugeViewModel.cs` · `Text/PercentFormatter.cs`
- `src/Chronos/Services/RefreshOrchestrator.cs` — consommateur unique, tick 60 s
- `tests/Chronos.Tests/{CompositeUsageProvider,LastExactUsageProvider,LastExactStore,WeeklyRecalibration,WindowGaugeViewModel,MainViewModel,CompositionRoot,ServicesLayerPurity}Tests.cs`

### Primaires (HIGH) — mesures sur la machine réelle, 2026-09-12
- `dotnet test -c Release` → **652 / 0 / 2 min 15 s**
- Micro-banc (projet jetable dans le scratchpad, référence au vrai `Chronos.csproj`) : `TranscriptActivityProvider.ReadAsync()` ×3 → **3218 / 2692 / 2791 ms** ; `Since(now − 5 h).Tokens` = **643 649 933**
- `find`/`stat` : **474 fichiers / 536,1 Mo / 155 171 lignes** dans l'horizon 8 j (830 / 923 Mo au total)
- `%APPDATA%\Chronos\` : `last-exact.json` absent · `usage.json` = `{"five_hour":{"used_percentage":10,"resets_at":9},"capturedAt":1783666519131}` · `oauth.dat` **518 o / mtime 1783863147** (inchangé après recherche)

### Secondaires (MEDIUM) — documentation de projet
- `.planning/REQUIREMENTS.md` (EXA-02/04/05, DEL-03/04) · `.planning/ROADMAP.md` (phases 19-20) ·
  `.planning/STATE.md` § « Contexte technique v1.5 » et les 60 décisions des phases 15-18 ·
  `.planning/phases/19-…/19-CONTEXT.md` · `CLAUDE.md` ·
  mémoire `chronos-source-exacte-statusline.md`, `chronos-versionnage-exe.md`

### Tertiaires (LOW — à valider)
- Estimation « 652 → 680-690 tests » : projection, pas une mesure.
- « Aucun test de provider ne compare un `WindowState` par égalité de record » : recherche **non exhaustive**
  sur ~120 tests → Open Question 4, à résoudre par un `dotnet test` de 2 min.

---

## Metadata

**Répartition de la confiance :**

| Domaine | Niveau | Raison |
|---|---|---|
| Placement de la doctrine (Q2) | **HIGH** | Argument mécanique vérifié par énumération des 4 sources et de leurs caches (300 s / 900 s / RAM / non borné), et par les 22 sites de construction comptés. |
| Modélisation des 4 états (Q1) | **HIGH** | Les 5 lectures de `Estimated` ont été énumérées ligne par ligne ; l'absence de producteur est vérifiée par grep sur tout `src/`. |
| Ordre des règles et rôle de la limite (Q3, Q4) | **HIGH** | Dérivé de deux faits mesurés (coût de la passe disque, caches des providers) et d'un argument d'honnêteté explicite, pas d'un goût. |
| Impossibilité de convertir les tokens (Q4) | **HIGH** | Falsifié quantitativement : 643 649 933 tokens / 5 h contre un plafond de 230 000 000. |
| Valeur exacte de la limite (6 min) | **MEDIUM** | Les **bornes** sont dures ; le point choisi dans l'intervalle est un arbitrage coût/honnêteté explicité, révisable. |
| Inventaire des 652 tests (Q7) | **MEDIUM-HIGH** | Les fichiers structurants ont été lus intégralement ; les ~120 tests de providers n'ont été que grepés → Open Question 4. |
| Périmètre visible d'EXA-05 (Q6) | **MEDIUM** | La tension phase 19 / phase 20 est réelle et n'est pas tranchée par les documents amont ; deux issues honnêtes sont proposées. |

**Date de recherche :** 2026-09-12
**Valide jusqu'au :** ~2026-10-12 (30 j). Invalidé plus tôt par : toute modification de
`CompositeUsageProvider.cs`, `LastExactUsageProvider.cs`, `TranscriptActivityProvider.cs`, des caches des
providers, ou une variation d'ordre de grandeur du volume de `~/.claude/projects` (les mesures 2-4 seraient à
refaire).
