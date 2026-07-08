# Phase 3 : Modèles + pipeline de données - Recherche

**Researched:** 2026-07-08
**Domain:** Pipeline de données neutre C#/.NET 8 (providers, records immuables, parsing tolérant System.Text.Json) + pont Node.js statusLine → fichier
**Confidence:** HIGH (contrat statusLine confirmé mot pour mot par la doc officielle ; APIs .NET stables ; environnement sondé)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions (décisions verrouillées)

**Source primaire — pont statusLine → fichier :**
- Source primaire = bloc `rate_limits` du contrat statusLine officiel de Claude Code. RIEN n'est persisté sur disque : il faut **CODER LE PONT** statusLine → fichier dans cette phase (script Node ou équivalent configuré dans `~/.claude/settings.json`), **NON DESTRUCTIF** vis-à-vis du `gsd-statusline.js` existant (chaîner/wrapper, ne pas remplacer aveuglément).
- Schéma réel : `used_percentage` (0..100) → `Utilization = used_percentage / 100.0` ; `resets_at` = epoch **SECONDES** → `DateTimeOffset.FromUnixTimeSeconds`. Fenêtres : `five_hour` / `seven_day` (chacune peut être absente indépendamment ; `rate_limits` absent pour non-Pro/Max ou avant 1re réponse API).
- Staleness : le fichier pont peut être figé hors session active → horodater l'écriture du pont et exposer l'âge de la donnée dans le snapshot.

**Repli JSONL :**
- `%USERPROFILE%\.claude\projects\**\*.jsonl`, champ `message.usage` (input_tokens, output_tokens, cache_creation_input_tokens, cache_read_input_tokens), timestamps ISO 8601 UTC (Z). Sous-agents dans sous-dossier `subagents/` (v2.1.202). Lecture `FileShare.ReadWrite` en streaming, try/catch par ligne, dernière ligne partielle ignorée.

**Contrats à implémenter (verrouillés) :**
- `IUsageProvider` : `GetAsync` + événement `SnapshotChanged` (DAT-02). Couche Services SANS AUCUN type WPF.
- `UsageSnapshot` immuable (record) : Utilization, ResetsAt, Exhausted (utilization ≥ 1), FractionTimeRemaining, SourceReliability (Fiable/Estimé) — pour CHACUNE des deux fenêtres (DAT-03). Ajouter l'âge de la donnée (staleness).
- `ClaudeUsageObjectProvider` : lit le fichier pont JSON (DAT-04).
- `JsonlEstimationProvider` : somme des tokens dans la fenêtre glissante, marqué Estimé (DAT-05). **IMPORTANT honnêteté** : sans plafond publié fiable, l'estimation d'utilization JSONL est approximative — la documenter comme telle ; ne jamais la présenter comme exacte.
- `CompositeUsageProvider` : primaire puis repli (DAT-06).
- `FractionTimeRemaining` calculé depuis ResetsAt (DAT-07) : (resets_at - now) / durée de fenêtre (5 h ; hebdo = 7 j best-effort).
- Parsing tolérant partout (ROB-02) : ligne/champ invalide ignoré, jamais d'exception non gérée, jamais de valeur inventée.

**Testabilité (verrouillé) :**
- Horloge injectable (interface type `IClock`) pour tester FractionTimeRemaining et fenêtres glissantes.
- Système de fichiers abstrait ou chemins injectables pour tester les providers sans toucher au vrai `~/.claude`.
- Tests unitaires xUnit dans `tests/Chronos.Tests` (projet existant).

### Claude's Discretion
Détails d'implémentation du pont (langage du script, nom du fichier de sortie sous `%USERPROFILE%\.claude` ou `%APPDATA%\Chronos`), organisation interne des parsers, structure exacte des tests.

### Deferred Ideas (OUT OF SCOPE)
- Bande d'activité sous-agents (V2-01) — layout `subagents/` documenté, exploitation STRUCTURÉE différée (pas de parsing des blocs/méta de sous-agents, pas d'UI d'activité). **Arbitrage orchestrateur (2026-07-08) : les tokens des transcripts `subagents/*.jsonl` consomment le même pool de quota compte — ils SONT inclus dans la somme brute de tokens du repli JSONL (scan récursif voulu). Seule l'exploitation structurée (bande d'activité) reste différée en V2-01.**
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **DAT-02** | `IUsageProvider` (GetAsync + événement SnapshotChanged) isole les sources du cadran — Services sans type WPF | Signature d'interface fournie (§ Architecture Patterns, Pattern 1). Vérification « zéro type WPF » automatisable (§ Validation). |
| **DAT-03** | Modèles `UsageSnapshot` (Utilization, ResetsAt, Exhausted, FractionTimeRemaining, SourceReliability) + `WindowState`, immuables et neutres | Structure de records concrète + nullabilité honnête (§ Architecture Patterns, Pattern 2 ; § Structure de classes). |
| **DAT-04** | `ClaudeUsageObjectProvider` lit l'objet d'usage (source primaire, fiable) | Pont Node → `usage.json`, schéma confirmé officiellement, lecture tolérante (§ Le pont statusLine, § Code Examples). |
| **DAT-05** | `JsonlEstimationProvider` estime par somme de tokens JSONL, streaming FileShare.ReadWrite | Streaming ligne-par-ligne, filtre `type==assistant`, honnêteté du plafond (§ Estimation JSONL honnête, § Code Examples). |
| **DAT-06** | `CompositeUsageProvider` tente primaire puis bascule sur repli | Règle de bascule par fenêtre (§ Architecture Patterns, Pattern 3). |
| **DAT-07** | `FractionTimeRemaining` des deux fenêtres calculé depuis ResetsAt | Formule + clamp [0..1] + gestion hebdo dérivant (§ FractionTimeRemaining). |
| **ROB-02** | Parsing tolérant : lignes/champs invalides ignorés, dernière ligne JSONL partielle ignorée | Options `JsonSerializerOptions` + try/catch par ligne (§ Don't Hand-Roll, § Common Pitfalls). |
</phase_requirements>

## Summary

Cette phase construit un **pipeline de données pur .NET 8, sans aucun type WPF**, qui produit des `UsageSnapshot` immuables à partir de deux sources locales : (1) un fichier pont `usage.json` alimenté par un **script Node.js branché sur la statusLine de Claude Code**, et (2) un repli par estimation de tokens sur les transcripts JSONL. Le point le plus délicat n'est pas le C# — les patterns providers/records/DI sont standards et déjà amorcés en Phase 1 — mais **le pont Node non destructif** : il n'existe qu'**une seule** commande `statusLine` configurable, et l'utilisateur en a déjà une active (`gsd-statusline.js`). Le pont doit donc **envelopper** l'existant : bufferiser le stdin, écrire `usage.json` **atomiquement**, ré-exécuter `gsd-statusline.js` avec le même stdin et **ré-émettre sa sortie** intacte.

Le contrat `rate_limits` est **confirmé mot pour mot par la doc officielle** (`code.claude.com/docs/en/statusline`, § « Rate limit usage ») : `used_percentage` 0-100 et `resets_at` en epoch **secondes**, présents uniquement pour les abonnés Pro/Max après la 1re réponse API, chaque fenêtre indépendamment absente. Ceci élève la confiance du schéma de MEDIUM (binaire 2.1.87 seul) à **HIGH** pour la localisation et les noms de champs. Reste MEDIUM : la stabilité inter-versions (API privée de facto, susceptible de bouger à une MAJ).

Le second enjeu est **l'honnêteté de l'estimation JSONL** : les plafonds de tokens ne sont **pas publiés** et sont **mouvants** (×2 le 6 mai, +50 % hebdo jusqu'au 13 juillet 2026). On ne peut donc pas dériver une `Utilization` fiable d'une somme de tokens. La recommandation est de rendre `Utilization` et `ResetsAt` **nullables** dans le modèle : le repli JSONL renseigne une somme de tokens brute + `SourceReliability.Estimated`, et laisse `Utilization`/`ResetsAt` à `null` plutôt que d'inventer une valeur (sauf si un plafond calibré/configuré existe). Cela satisfait à la fois « estimer par somme de tokens » et « ne jamais inventer ».

**Primary recommendation :** Modèles nullable-safe (`WindowState` avec `double? Utilization`, `DateTimeOffset? ResetsAt`, `SourceReliability`) + pont Node wrapper non destructif écrivant `%APPDATA%\Chronos\usage.json` en write-temp-then-rename + providers streaming/tolérants via `System.Text.Json` in-box, tout injecté par chemins + `IClock` pour la testabilité.

## Project Constraints (from CLAUDE.md)

Directives actionnables extraites de `CLAUDE.md` (autorité = décisions verrouillées) :

- **Stack imposée** : C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm) + Microsoft.Extensions.DependencyInjection. **Ne rien ajouter** pour le parsing : `System.Text.Json` est in-box.
- **MVVM strict**, `[ObservableProperty]`/`[RelayCommand]`, DI par constructeur, dossiers `Models/Views/ViewModels/Services`.
- **Chemins sous profil utilisateur uniquement**, aucun droit admin (`%USERPROFILE%` / `%APPDATA%`).
- **Honnêteté des chiffres** : `utilization`/`resets_at` prioritaires sur le comptage de tokens ; **ne jamais présenter une estimation comme exacte**. Reset hebdo best-effort et recalibrable.
- **Robustesse** : aucune source ≠ crash → état « données indisponibles » ; parsing tolérant.
- **Langue** : UI **et commentaires en français** (le code de cette phase doit être commenté en français).
- **Piège mono-fichier** (pertinent pour les chemins des providers) : **ne jamais** utiliser `Assembly.Location` / `GetExecutingAssembly().Location` (vides en mono-fichier). Construire les chemins via `Environment.GetFolderPath` / `Environment.ExpandEnvironmentVariables`.
- **Frontière stricte** : la couche Services ne référence **aucun** type WPF (pas de `Dispatcher`, pas de `Brush`).

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | in-box net8.0 (8.0.x) | Parsing tolérant `usage.json` + JSONL, lecture/écriture | Déjà dans le framework — **rien à installer**. `Utf8JsonReader`/`JsonDocument`/`JsonSerializer` haute perf, streaming natif. |
| CommunityToolkit.Mvvm | 8.4.2 (déjà référencé) | (côté VM/Phase 6) — **pas requis par la couche données** | Records/POCO neutres n'en dépendent pas. Ne pas l'introduire dans Models/Services. |
| Microsoft.Extensions.Hosting | 8.0.1 (déjà référencé) | Enregistrement DI des providers en Singleton dans `App.xaml.cs` | Composition root déjà en place (Generic Host). Providers = Singleton (state + futurs watchers). |
| Node.js | v24.14.1 (installé, confirmé) | Runtime du **pont statusLine** (script wrapper) | Déjà utilisé par `gsd-statusline.js` ; garanti présent car la statusLine actuelle est en Node. |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit | 2.9.2 (déjà référencé) | Tests unitaires du pipeline | Projet `tests/Chronos.Tests` opérationnel (3 tests verts). |
| Xunit.StaFact | 1.1.11 (déjà référencé) | `[WpfFact]` pour tests nécessitant STA | **Non requis** pour les tests de données pures (utiliser `[Fact]` classique) ; réservé aux tests qui touchent un `Dispatcher`. |
| Microsoft.NET.Test.Sdk | 17.11.1 (déjà référencé) | Runner de tests | En place. |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| System.Text.Json | Newtonsoft.Json | Newtonsoft est plus « permissif » par défaut mais ajoute une dépendance externe (contre la contrainte « rien d'inutile ») et est plus lent/plus gourmand en allocations sur du streaming JSONL plurimégaoctets. **Rejeté.** |
| Pont en Node.js | Pont en mode CLI de Chronos (`Chronos.exe --statusline`) | Un mode CLI éviterait la dépendance Node MAIS lancerait le runtime .NET à **chaque** rendu de statusLine (coûteux, ~centaines de ms de démarrage) — inacceptable vu le debounce 300 ms et l'annulation en vol. **Node reste recommandé** (démarrage quasi instantané, déjà la techno de la statusLine). |
| Chemins injectés | Abstraction `IFileSystem` complète (System.IO.Abstractions) | Abstraction FS complète = dépendance + sur-ingénierie pour 2 providers. **Recommandé** : injecter les *chemins* (options record) et écrire les tests contre des dossiers temporaires réels. |

**Installation :** aucune nouvelle dépendance NuGet. `System.Text.Json` est in-box net8.0.

**Version verification (sondé le 2026-07-08) :**
- `node --version` → **v24.14.1** ✓
- `dotnet --version` → **10.0.201** (SDK) ; runtime `Microsoft.WindowsDesktop.App` **8.0.25** présent ✓
- `System.Text.Json` : version in-box alignée sur le runtime net8.0 (8.0.x) — pas de `PackageReference` à ajouter.

## Le pont statusLine → fichier (point de conception n°1)

### Contrat d'entrée confirmé (HIGH — doc officielle)

La doc officielle `code.claude.com/docs/en/statusline` confirme **mot pour mot** le schéma de `docs/data-sources.md` :

| Champ | Type | Plage | Confirmation |
|-------|------|-------|--------------|
| `rate_limits.five_hour.used_percentage` | number | 0..100 | « Percentage of the 5-hour rate limit consumed, from 0 to 100 » |
| `rate_limits.five_hour.resets_at` | number | epoch **secondes** | « Unix epoch seconds when the 5-hour rate limit window resets » |
| `rate_limits.seven_day.used_percentage` | number | 0..100 | idem 7 j |
| `rate_limits.seven_day.resets_at` | number | epoch **secondes** | idem 7 j |

**Conditions de présence (verbatim doc) :** « `rate_limits`: appears only for Claude.ai subscribers (Pro/Max) after the first API response in the session. Each window (`five_hour`, `seven_day`) may be independently absent. »

**Fréquence / déclenchement (verbatim doc) — critique pour le pont :**
- Le script tourne « after each new assistant message, after `/compact` finishes, when the permission mode changes, or when vim mode toggles ». **Debounced à 300 ms.**
- « If a new update triggers while your script is still running, the **in-flight execution is cancelled**. » → **conséquence de conception** : le pont doit **écrire `usage.json` AVANT** de ré-exécuter la statusline enfant, pour que l'écriture aboutisse même si la ré-émission est annulée.
- Option `refreshInterval` (min 1 s) re-lance la commande sur timer même en session idle — mais **ne pas en dépendre** pour la fraîcheur (l'overlay interpole localement via `resets_at`).
- Sur **Windows**, Claude Code exécute la commande via **Git Bash si installé, sinon PowerShell**. La config actuelle (`node "C:/Users/Tanguy/.claude/hooks/gsd-statusline.js"`) utilise des **slashes avant** — impératif sous Git Bash (les backslashes sont mangés comme échappements).

### Config statusLine actuelle de l'utilisateur (LECTURE SEULE — `~/.claude/settings.json`)

```json
"statusLine": {
  "type": "command",
  "command": "node \"C:/Users/Tanguy/.claude/hooks/gsd-statusline.js\""
}
```

Une **seule** commande possible. `gsd-statusline.js` lit le stdin, affiche `model | task | dir | contexte` et écrit déjà un **fichier pont de contexte** dans `os.tmpdir()` (`claude-ctx-<session>.json`) — **aucun conflit** avec notre `usage.json` (chemins et objets différents).

### Design recommandé du pont (wrapper non destructif)

Script `chronos-statusline-bridge.js` (ou `.mjs`), placé sous `~/.claude/hooks/`. Séquence :

1. **Bufferiser tout le stdin** (le JSON de session) dans une variable — il ne peut être lu qu'**une fois**.
2. **Parser en try/catch.** Extraire `data.rate_limits` (peut être `undefined`).
3. **Écrire `usage.json` atomiquement AVANT tout** (avant de spawn l'enfant, pour survivre à l'annulation en vol) :
   - `mkdirSync(%APPDATA%/Chronos, { recursive: true })` (le dossier **n'existe pas encore** — sondé).
   - Écrire dans un fichier **temp du même dossier** (`usage.json.tmp-<pid>`), puis `fs.renameSync(tmp, final)` → remplacement atomique (Windows : `MoveFileEx` + `MOVEFILE_REPLACE_EXISTING`).
   - Payload : `{ five_hour: rate_limits?.five_hour ?? null, seven_day: rate_limits?.seven_day ?? null, capturedAt: <epoch ms ou ISO> }`. **Ne rien écrire d'inventé** ; propager `null` fenêtre par fenêtre.
   - Best-effort : envelopper l'écriture en try/catch, **ne jamais casser la statusline** si l'écriture échoue.
4. **Ré-exécuter la statusline existante** : `spawnSync("node", ["C:/Users/Tanguy/.claude/hooks/gsd-statusline.js"], { input: <stdin bufferisé>, encoding: "utf8" })`. **Passer le même stdin** à l'enfant.
5. **Ré-émettre la sortie de l'enfant** sur `process.stdout` **telle quelle** (préserver ANSI/multi-lignes), et propager le code de sortie.

> **Décision de chemin (discrétion) :** `%APPDATA%\Chronos\usage.json` (cohérent avec `settings.json` du projet, hors `~/.claude`). Résolu en Node via `path.join(process.env.APPDATA, "Chronos", "usage.json")`.

**Installation — recommandation : automatique + documentée.** Fournir un petit script d'installation idempotent (Node ou `.ps1`) qui :
- lit `~/.claude/settings.json`, **détecte la commande statusLine existante**, la mémorise dans une variable d'env ou en dur dans le wrapper généré,
- remplace `statusLine.command` par le wrapper (en préservant l'existant à ré-émettre),
- **jamais destructif** : si la commande est déjà le wrapper, ne rien faire ; sauvegarder l'ancienne valeur.
Doubler d'une **procédure manuelle documentée** (README de phase) au cas où l'utilisateur préfère éditer lui-même. L'installation touche `~/.claude/settings.json` — obtenir un accord explicite avant d'écrire (hors périmètre code : c'est une étape de déploiement/doc).

**⚠️ Contrainte de sécurité :** le pont ne lit **que** `rate_limits` du stdin. Il ne lit **jamais** `.credentials.json` ni le contenu des conversations.

## Estimation JSONL honnête (point de conception n°2)

### Le problème
`message.usage` donne des tokens bruts (input/output/cache). Mais **les plafonds ne sont pas publiés** et sont **mouvants** (×2 le 06/05, +50 % hebdo jusqu'au 13/07/2026). Une somme de tokens **ne peut pas** produire une `Utilization` fiable en % — la présenter comme telle **violerait la Core Value** du projet.

### Options évaluées

| Option | Description | Fiabilité | Verdict |
|--------|-------------|-----------|---------|
| **(a) Ne pas estimer l'utilization** | Sommer les tokens sur la fenêtre glissante, exposer la **somme brute** + `Estimated` ; laisser `Utilization = null` et `ResetsAt = null` | Honnête, jamais faux | **RECOMMANDÉ comme socle** |
| (b) Plafond configurable | L'utilisateur saisit un plafond de tokens ; `Utilization = somme / plafond` | Dépend de l'utilisateur ; faux s'il se trompe | Différable (réglage — hors périmètre données) |
| (c) Auto-calibration | Quand la source primaire est dispo, mémoriser (tokens_fenêtre ↔ used_percentage) → plafond implicite persisté, réutilisé en repli | MEDIUM, **décroissante** (plafonds mobiles) | Hook prévu, **implémentation différée** |

### Recommandation
**Option (a) comme comportement par défaut**, avec le modèle conçu pour accueillir (b)/(c) sans refonte :

- `JsonlEstimationProvider` calcule pour chaque fenêtre (5 h glissante depuis `now`, 7 j glissants depuis `now`) : `EstimatedTokens` = somme de `input_tokens + output_tokens + cache_creation_input_tokens + cache_read_input_tokens` des lignes `assistant` dont `timestamp ≥ now - fenêtre`.
- `SourceReliability = Estimated` **toujours**.
- `Utilization = null` **tant qu'aucun plafond n'est connu** (pas d'invention). Si un plafond calibré/configuré existe un jour → `Utilization = clamp(EstimatedTokens / plafond, 0, 1)`.
- `ResetsAt = null` pour le repli : **le JSONL ne contient aucune information de reset** (les timestamps sont des instants de messages, pas des ancres de fenêtre). N'inventer aucun `resets_at`. `FractionTimeRemaining` de la fenêtre concernée sera donc `null` (inconnu) → l'UI (Phase 5) l'affiche comme « estimé / temps inconnu ».

**Degré de fiabilité annoncé :** la somme de tokens est **exacte** (comptage réel) ; sa **traduction en % de quota est inconnue** → toujours marquée `Estimé`. C'est le compromis honnête exigé par DAT-08 / Core Value.

> **Faux positifs à éviter (ROB-02) :** ne compter que des **objets structurés** `message.usage` sur lignes `type == "assistant"` **et** `message.role == "assistant"`. Ne **jamais** matcher les chaînes `"five_hour"`/`"seven_day"` trouvées dans du texte (ce sont de la prose de transcripts, pas des objets d'usage).

## Architecture Patterns

### Structure de fichiers recommandée (concrète)

```
src/Chronos/
├── Models/                          # POCO/records neutres — ZÉRO using WPF
│   ├── SourceReliability.cs         # enum { Exact, Estimated, Unavailable }
│   ├── WindowKind.cs                # enum { FiveHour, SevenDay }
│   ├── WindowState.cs               # record : état d'UNE fenêtre
│   └── UsageSnapshot.cs             # record : les deux fenêtres + staleness
├── Services/
│   ├── IClock.cs / SystemClock.cs   # horloge injectable (UtcNow)
│   ├── ChronosPaths.cs              # record d'options : chemins injectables
│   ├── IUsageProvider.cs            # GetAsync + event SnapshotChanged (DAT-02)
│   ├── ClaudeUsageObjectProvider.cs # lit %APPDATA%\Chronos\usage.json (DAT-04)
│   ├── JsonlEstimationProvider.cs   # streaming JSONL, somme tokens (DAT-05)
│   └── CompositeUsageProvider.cs    # primaire → repli (DAT-06)
scripts/ (ou tools/)
│   └── chronos-statusline-bridge.js # le pont Node (DAT-04, non destructif)
tests/Chronos.Tests/
│   ├── Fakes/FakeClock.cs
│   ├── TestData/…                    # usage.json valides/corrompus, *.jsonl échantillons
│   ├── ClaudeUsageObjectProviderTests.cs
│   ├── JsonlEstimationProviderTests.cs
│   ├── CompositeUsageProviderTests.cs
│   ├── WindowStateTests.cs          # FractionTimeRemaining, Exhausted, clamp
│   └── ServicesLayerPurityTests.cs  # prouve : aucun type WPF dans Services (DAT-02)
```

### Pattern 1 : `IUsageProvider` — contrat neutre (DAT-02)

**What :** interface asynchrone qui isole les sources du cadran. `GetAsync` pour un pull ; `SnapshotChanged` pour un push (branché en Phase 4 par l'orchestrateur).
**When :** frontière Services→VM. Aucun type WPF.

```csharp
// Source : ARCHITECTURE.md (frontière Services) + DAT-02
namespace Chronos.Services;

public interface IUsageProvider
{
    /// <summary>Lit la meilleure source disponible et produit un snapshot neutre.</summary>
    Task<UsageSnapshot> GetAsync(CancellationToken ct = default);

    /// <summary>Émis quand un nouveau snapshot est produit (thread pool — marshaling côté VM).</summary>
    event EventHandler<UsageSnapshot>? SnapshotChanged;
}
```
> En Phase 3, `SnapshotChanged` peut n'être émis que par le composite/orchestrateur ; l'implémenter mais le déclenchement réel (watcher) arrive en Phase 4. Le déclarer maintenant fige le contrat (DAT-02).

### Pattern 2 : modèles immuables nullable-safe (DAT-03)

**What :** `record`s immuables, neutres, avec **nullabilité qui encode l'inconnu** (jamais de valeur sentinelle inventée).

```csharp
namespace Chronos.Models;

public enum SourceReliability { Exact, Estimated, Unavailable }
public enum WindowKind { FiveHour, SevenDay }

/// <summary>État immuable d'UNE fenêtre (5 h ou hebdo). null = inconnu (jamais inventé).</summary>
public sealed record WindowState
{
    public required WindowKind Kind { get; init; }
    public double? Utilization { get; init; }           // 0..1 ; null si inconnu (repli sans plafond)
    public DateTimeOffset? ResetsAt { get; init; }       // null si inconnu (repli JSONL)
    public double? FractionTimeRemaining { get; init; }  // 0..1 clampé ; null si ResetsAt inconnu
    public long? EstimatedTokens { get; init; }          // somme brute (repli) ; info honnête
    public required SourceReliability Reliability { get; init; }

    /// <summary>Épuisé si utilization connue ≥ 1. Inconnu (null) ≠ épuisé.</summary>
    public bool Exhausted => Utilization is >= 1.0;

    public static WindowState Unavailable(WindowKind k) =>
        new() { Kind = k, Reliability = SourceReliability.Unavailable };
}

/// <summary>Snapshot immuable des deux fenêtres + fraîcheur de la donnée sous-jacente.</summary>
public sealed record UsageSnapshot
{
    public required WindowState FiveHour { get; init; }
    public required WindowState SevenDay { get; init; }
    /// <summary>Horodatage de capture de la source (bridge capturedAt / lecture JSONL).</summary>
    public DateTimeOffset? SourceCapturedAt { get; init; }
    /// <summary>Âge de la donnée au moment du snapshot (staleness) ; null si capture inconnue.</summary>
    public TimeSpan? Age { get; init; }

    public static UsageSnapshot Empty => new()
    {
        FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
        SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
    };
}
```
> **Pourquoi nullable :** DAT-03 liste `Exhausted` et `FractionTimeRemaining`, mais l'honnêteté exige de distinguer « 0 % » de « inconnu ». `double?`/`DateTimeOffset?` porte cette distinction sans valeur sentinelle. `Exhausted` reste un calcul dérivé (pas de champ stocké contradictoire).

### Pattern 3 : `CompositeUsageProvider` — bascule par fenêtre (DAT-06)

**What :** tente le primaire ; bascule sur le repli. **Granularité par fenêtre** : le primaire peut fournir `five_hour` mais pas `seven_day` (chaque fenêtre indépendamment absente). Le composite prend la meilleure source **par fenêtre**.

**Règle recommandée :**
- Si primaire donne une fenêtre `Exact` → la garder.
- Sinon, si le repli donne cette fenêtre `Estimated` → l'utiliser.
- Sinon → `Unavailable` (déclenche ROB-01 en Phase 5, pas de crash).

```csharp
public sealed class CompositeUsageProvider : IUsageProvider
{
    private readonly IUsageProvider _primary;   // ClaudeUsageObjectProvider
    private readonly IUsageProvider _fallback;  // JsonlEstimationProvider
    public event EventHandler<UsageSnapshot>? SnapshotChanged;

    public CompositeUsageProvider(IUsageProvider primary, IUsageProvider fallback)
        { _primary = primary; _fallback = fallback; }

    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        var p = await _primary.GetAsync(ct);
        // Repli calculé seulement si au moins une fenêtre primaire manque (paresse possible).
        var f = await _fallback.GetAsync(ct);
        var snap = new UsageSnapshot
        {
            FiveHour = Best(p.FiveHour, f.FiveHour),
            SevenDay = Best(p.SevenDay, f.SevenDay),
            SourceCapturedAt = p.SourceCapturedAt ?? f.SourceCapturedAt,
            Age = p.Age ?? f.Age,
        };
        SnapshotChanged?.Invoke(this, snap);
        return snap;
    }

    private static WindowState Best(WindowState primary, WindowState fallback) =>
        primary.Reliability == SourceReliability.Exact ? primary
        : fallback.Reliability == SourceReliability.Estimated ? fallback
        : primary; // reste Unavailable
}
```

### Enregistrement DI (App.xaml.cs — Singleton)

```csharp
// Dans ConfigureServices (Generic Host déjà en place)
services.AddSingleton<IClock, SystemClock>();
services.AddSingleton(new ChronosPaths(/* usage.json, projectsRoot résolus via Environment */));
services.AddSingleton<ClaudeUsageObjectProvider>();
services.AddSingleton<JsonlEstimationProvider>();
services.AddSingleton<IUsageProvider>(sp => new CompositeUsageProvider(
    primary:  sp.GetRequiredService<ClaudeUsageObjectProvider>(),
    fallback: sp.GetRequiredService<JsonlEstimationProvider>()));
```
> Cohérent avec `App.xaml.cs` existant (Host + Singletons). Chemins résolus via `Environment.GetFolderPath(SpecialFolder.ApplicationData)` et `SpecialFolder.UserProfile` — **jamais** `Assembly.Location` (mono-fichier).

### FractionTimeRemaining — calcul, clamp et hebdo dérivant (DAT-07 + ROB-03)

**Formule :** `FractionTimeRemaining = clamp((ResetsAt - now) / windowLength, 0, 1)`
- `windowLength` : 5 h = `TimeSpan.FromHours(5)` ; hebdo = `TimeSpan.FromDays(7)` (**nominal best-effort**).
- **Clamp [0..1] obligatoire :**
  - hebdo peut dériver (~72 h, ancrage non documenté) → `(ResetsAt - now)` peut dépasser 7 j → ratio > 1 → **clamp à 1**.
  - donnée périmée / reset dépassé → ratio négatif → **clamp à 0**.
- `ResetsAt == null` (repli JSONL) → `FractionTimeRemaining = null` (inconnu, pas 0).

```csharp
public static double? FractionRemaining(DateTimeOffset? resetsAt, DateTimeOffset now, TimeSpan windowLength)
{
    if (resetsAt is null || windowLength <= TimeSpan.Zero) return null;
    var ratio = (resetsAt.Value - now) / windowLength;   // TimeSpan / TimeSpan = double
    return Math.Clamp(ratio, 0.0, 1.0);
}
```
> Calcul via `IClock.UtcNow` injecté → testable de façon déterministe. Recalibrage hebdo (ROB-03) = réglage utilisateur en Phase 6 ; ici, traiter `resets_at` **tel que fourni**.

### Anti-Patterns to Avoid
- **Inventer une valeur pour combler l'inconnu** (utilization estimée sans plafond, resets_at déduit du JSONL) → viole la Core Value. Utiliser `null`.
- **Charger le JSONL entier en mémoire** → fichiers plurimégaoctets. Streamer ligne par ligne.
- **`FileShare.Read` seul sur le JSONL** → `IOException` car Claude Code écrit en même temps. Utiliser `FileShare.ReadWrite`.
- **Type WPF dans Models/Services** (`Brush`, `Dispatcher`, `Color`) → casse la testabilité et la frontière. Test de pureté automatisé (§ Validation).
- **Écriture in-place du pont** (`writeFileSync` direct sur `usage.json`) → le lecteur peut voir un fichier à moitié écrit. Write-temp-then-rename.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Parsing JSON tolérant | Parser JSON maison / regex | `System.Text.Json` (`JsonDocument.Parse`, `JsonSerializer.Deserialize`) + try/catch par ligne | Gère nombres décimaux, unicode, échappements ; regex sur JSON = bugs garantis. |
| Lecture epoch → date | Arithmétique manuelle sur ticks | `DateTimeOffset.FromUnixTimeSeconds(long)` | Gère fuseaux/overflow correctement. **Secondes**, pas millisecondes. |
| Parsing ISO 8601 JSONL | Split manuel de string | `DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)` | Le suffixe `Z` + millisecondes est géré nativement ; culture invariante obligatoire. |
| Écriture atomique fichier | `writeFileSync` direct | write-temp + `rename` (Node) / `File.Replace` ou temp+`File.Move(overwrite:true)` (C#, pour settings) | Le rename est atomique ; évite les lectures de fichier partiel. |
| Lecture concurrente | Verrou maison / retry infini | `FileShare.ReadWrite` + retry court borné sur `IOException` | Claude Code écrit le JSONL en parallèle ; ReadWrite évite le crash. |
| Tolérance nombres/commentaires | Pré-nettoyage du texte | `JsonSerializerOptions { ReadCommentHandling = Skip, AllowTrailingCommas = true, NumberHandling = AllowReadingFromString, PropertyNameCaseInsensitive = true }` | Options in-box couvrent les cas de laxisme sans code défensif exotique. |

**Key insight :** tout ce dont cette phase a besoin (JSON tolérant, epoch, ISO, écriture atomique, partage de fichier) existe **in-box** dans .NET 8 et Node. Le seul code « métier » à écrire est la **logique de fenêtre glissante**, la **bascule composite** et le **wrapper du pont**.

## Common Pitfalls

### Pitfall 1 : confondre epoch secondes et millisecondes
**Ce qui se passe :** `resets_at` traité comme ms → dates en 1970 ou an +50000.
**Cause :** `rate_limits.resets_at` est en **secondes** (confirmé doc). Les timestamps JSONL sont **ISO 8601** (autre format — ne pas confondre les deux).
**Éviter :** `DateTimeOffset.FromUnixTimeSeconds` pour le primaire ; `DateTimeOffset.Parse` pour le JSONL. Test dédié avec la valeur `1738425600` → attendu 2025-02-01T16:00:00Z.
**Signes :** comptes à rebours absurdes.

### Pitfall 2 : dernière ligne JSONL partielle
**Ce qui se passe :** exception à la fin d'un fichier en cours d'écriture.
**Cause :** Claude Code écrit en append ; la dernière ligne peut être tronquée.
**Éviter :** `StreamReader.ReadLine` retourne la dernière ligne même sans `\n` final → l'entourer d'un **try/catch par ligne** ; une ligne qui ne parse pas est **ignorée silencieusement**. Ne jamais laisser l'exception remonter.
**Signes :** provider qui crashe en fin de session active.

### Pitfall 3 : le pont casse la statusline existante
**Ce qui se passe :** la barre de l'utilisateur disparaît après installation du pont.
**Cause :** une seule commande statusLine ; remplacement au lieu de wrapper ; ou stdin consommé une fois et non repassé à l'enfant.
**Éviter :** bufferiser le stdin, le **repasser** à `gsd-statusline.js`, ré-émettre sa sortie ; écrire `usage.json` **avant** le spawn (survie à l'annulation en vol).
**Signes :** statusline vide / `statusline skipped`.

### Pitfall 4 : fenêtre présente mais champ manquant
**Ce qui se passe :** NRE quand `five_hour` existe mais `resets_at` absent.
**Cause :** chaque champ peut manquer indépendamment.
**Éviter :** accès nullable/`TryGetProperty` sur chaque champ ; une fenêtre incomplète → `WindowState` partiel (utilization connue, resets_at null) plutôt qu'un rejet total.
**Signes :** `NullReferenceException` intermittente selon l'abonnement/le moment.

### Pitfall 5 : type WPF qui fuit dans Services
**Ce qui se passe :** un `using System.Windows.Media;` se glisse dans un provider.
**Cause :** copier-coller, converter placé au mauvais endroit.
**Éviter :** **test automatisé de pureté** (§ Validation) qui échoue si un assembly WPF est référencé par un type de `Chronos.Services`/`Chronos.Models`.
**Signes :** la couche données ne compile plus sans `PresentationFramework`.

## Code Examples

### Lecture tolérante du fichier pont (ClaudeUsageObjectProvider, DAT-04)
```csharp
// Source : contrat rate_limits (doc officielle) + System.Text.Json in-box
private static readonly JsonSerializerOptions Tolerant = new()
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
};

public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
{
    try
    {
        // FileShare.ReadWrite : le pont Node peut réécrire en parallèle.
        await using var fs = new FileStream(_paths.UsageFile, FileMode.Open,
            FileAccess.Read, FileShare.ReadWrite);
        using var doc = await JsonDocument.ParseAsync(fs, cancellationToken: ct);
        var root = doc.RootElement;

        DateTimeOffset? capturedAt = root.TryGetProperty("capturedAt", out var c) && c.TryGetInt64(out var ms)
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;

        var five = ReadWindow(root, "five_hour", WindowKind.FiveHour, TimeSpan.FromHours(5));
        var week = ReadWindow(root, "seven_day", WindowKind.SevenDay, TimeSpan.FromDays(7));

        return new UsageSnapshot
        {
            FiveHour = five, SevenDay = week,
            SourceCapturedAt = capturedAt,
            Age = capturedAt is null ? null : _clock.UtcNow - capturedAt,
        };
    }
    catch (Exception ex) when (ex is IOException or JsonException or FileNotFoundException or DirectoryNotFoundException)
    {
        return UsageSnapshot.Empty; // Unavailable → jamais de crash (ROB-01)
    }
}

private WindowState ReadWindow(JsonElement root, string name, WindowKind kind, TimeSpan len)
{
    if (!root.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object)
        return WindowState.Unavailable(kind);

    double? util = w.TryGetProperty("used_percentage", out var up) && up.TryGetDouble(out var pct)
        ? pct / 100.0 : null;
    DateTimeOffset? reset = w.TryGetProperty("resets_at", out var ra) && ra.TryGetInt64(out var epoch)
        ? DateTimeOffset.FromUnixTimeSeconds(epoch) : null;

    return new WindowState
    {
        Kind = kind, Utilization = util, ResetsAt = reset, Reliability = SourceReliability.Exact,
        FractionTimeRemaining = FractionRemaining(reset, _clock.UtcNow, len),
    };
}
```

### Streaming tolérant du JSONL (JsonlEstimationProvider, DAT-05 + ROB-02)
```csharp
public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
{
    var now = _clock.UtcNow;
    long five = 0, week = 0;
    foreach (var file in EnumerateJsonl(_paths.ProjectsRoot)) // Directory.EnumerateFiles récursif, try/catch
    {
        FileStream? fs = null;
        try { fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); }
        catch (IOException) { continue; }
        await using (fs)
        using (var reader = new StreamReader(fs))
        {
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                if (line.Length == 0) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);       // ligne partielle/corrompue → JsonException
                    var o = doc.RootElement;
                    if (!IsAssistant(o)) continue;                   // type==assistant && message.role==assistant
                    if (!o.TryGetProperty("timestamp", out var ts)) continue;
                    if (!DateTimeOffset.TryParse(ts.GetString(), CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out var when)) continue;

                    long tokens = SumUsageTokens(o);                 // input+output+cache_creation+cache_read
                    if (when >= now - TimeSpan.FromHours(5)) five += tokens;
                    if (when >= now - TimeSpan.FromDays(7))  week += tokens;
                }
                catch (JsonException) { /* ligne invalide ignorée (ROB-02) */ }
            }
        }
    }
    return new UsageSnapshot
    {
        FiveHour = EstimatedWindow(WindowKind.FiveHour, five),   // Utilization=null, ResetsAt=null, Estimated
        SevenDay = EstimatedWindow(WindowKind.SevenDay, week),
        SourceCapturedAt = now, Age = TimeSpan.Zero,
    };
}
```

### FakeClock pour tests déterministes
```csharp
internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }
    public FakeClock(DateTimeOffset now) => UtcNow = now;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Champ `utilization` 0..1 (modélisation initiale PROJECT.md) | `used_percentage` 0..100 → `/100.0` côté modèle | Découverte Phase 2 | Le champ `utilization` **n'existe pas** dans la source — normalisation côté modèle. |
| Sous-agents `tool_use name=Task` inline | Dossier `subagents/agent-*.jsonl` | v2.1.202 | Tokens inclus dans la somme du repli (même pool). Exploitation structurée **hors périmètre** (V2-01). Ne pas parser inline. |
| `context_window` tokens cumulés session | Tokens du **contexte courant** | v2.1.132 | N'affecte pas `rate_limits` (indépendant), mais à noter si on lisait `context_window`. |

**Deprecated/outdated :**
- Toute hypothèse d'un `usage.json` déjà présent sur disque : **faux** — rien n'est persisté, d'où le pont à coder.

## Open Questions

1. **Stabilité inter-versions du schéma `rate_limits`**
   - Ce qu'on sait : confirmé HIGH par la doc officielle courante + binaire 2.1.87.
   - Ce qui est flou : runtime actif 2.1.202 non vérifié champ par champ ; API privée de facto.
   - Recommandation : **test de contrat** sur échantillon en Phase 3 ; dégradation vers `Unavailable` si un champ/fenêtre manque, plutôt que du code défensif exotique. Revalider `docs/data-sources.md` à chaque MAJ majeure.

2. **Repli JSONL : quand la source primaire disparaît, faut-il calibrer ?**
   - Ce qu'on sait : socle recommandé = Option (a), pas d'utilization inventée.
   - Ce qui est flou : l'auto-calibration (c) apporterait une utilization approximative en repli.
   - Recommandation : livrer (a) en Phase 3 ; concevoir `WindowState` pour accueillir (b)/(c) sans refonte ; trancher (b)/(c) plus tard (réglage utilisateur, Phase 6).

3. **Paresse du composite (perf)**
   - Ce qu'on sait : le repli scanne des JSONL potentiellement plurimégaoctets.
   - Recommandation : ne calculer le repli **que si** au moins une fenêtre primaire manque (court-circuit), pour éviter un scan disque inutile quand le primaire suffit.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Node.js | Pont statusLine (DAT-04) | ✓ | v24.14.1 | — (déjà requis par la statusline actuelle) |
| .NET SDK | Build/test du pipeline | ✓ | 10.0.201 (compile net8.0-windows) | — |
| Runtime WindowsDesktop.App 8.0 | Exécution/tests WPF | ✓ | 8.0.25 | — |
| `%APPDATA%\Chronos\` | Chemin du fichier pont `usage.json` | ✗ (pas encore créé) | — | Le pont doit `mkdir -p` (mkdirSync recursive) au 1er run |
| `%USERPROFILE%\.claude\projects\` | Source de repli JSONL | ✓ | — | Présent, peuplé de dossiers `<slug>` |
| `~/.claude/settings.json` (statusLine) | Config du pont | ✓ (lecture seule faite) | — | Une seule commande → wrapper obligatoire |

**Missing dependencies with no fallback :** aucune.
**Missing dependencies with fallback :** `%APPDATA%\Chronos\` inexistant → création par le pont (et par les tests via dossier temp). Aucun blocage.

## Validation Architecture

*(nyquist_validation = true dans config.json → section incluse.)*

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (STA seulement si besoin) + Microsoft.NET.Test.Sdk 17.11.1 |
| Config file | `tests/Chronos.Tests/Chronos.Tests.csproj` (référence projet Chronos ; `InternalsVisibleTo` en place) |
| Quick run command | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~<Classe>"` |
| Full suite command | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` |

> Les tests du pipeline données sont **purs** (pas de Dispatcher) → utiliser `[Fact]`/`[Theory]` classiques, **pas** `[WpfFact]`. Réserver StaFact aux tests qui construisent un objet WPF.

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DAT-02 | Services/Models ne référencent aucun type WPF | unit (réflexion) | `dotnet test --filter "FullyQualifiedName~ServicesLayerPurity"` | ❌ Wave 0 |
| DAT-03 | `Exhausted` = utilization ≥ 1 ; immuabilité ; null = inconnu | unit | `dotnet test --filter "FullyQualifiedName~WindowStateTests"` | ❌ Wave 0 |
| DAT-04 | `used_percentage`→/100 ; `resets_at` epoch s→DateTimeOffset ; fenêtre absente→Unavailable ; capturedAt→Age | unit | `dotnet test --filter "FullyQualifiedName~ClaudeUsageObjectProviderTests"` | ❌ Wave 0 |
| DAT-05 | Somme tokens fenêtre glissante ; marqué Estimated ; utilization null sans plafond ; filtre assistant | unit | `dotnet test --filter "FullyQualifiedName~JsonlEstimationProviderTests"` | ❌ Wave 0 |
| DAT-06 | Bascule par fenêtre : Exact prioritaire, sinon Estimated, sinon Unavailable | unit | `dotnet test --filter "FullyQualifiedName~CompositeUsageProviderTests"` | ❌ Wave 0 |
| DAT-07 | FractionTimeRemaining = clamp((reset-now)/len,0,1) ; hebdo >7j→1 ; périmé→0 ; null si reset null | unit (Theory) | `dotnet test --filter "FullyQualifiedName~WindowStateTests"` | ❌ Wave 0 |
| ROB-02 | Ligne corrompue ignorée ; dernière ligne partielle ignorée ; champ manquant toléré ; jamais d'exception | unit | `dotnet test --filter "FullyQualifiedName~JsonlEstimationProviderTests"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit :** `dotnet test --filter` de la classe touchée.
- **Per wave merge :** `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` (suite complète).
- **Phase gate :** suite verte avant `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/Fakes/FakeClock.cs` — horloge déterministe (IClock).
- [ ] `tests/Chronos.Tests/TestData/usage-valid.json`, `usage-partial.json` (five_hour seul), `usage-corrupt.json` — fixtures pont.
- [ ] `tests/Chronos.Tests/TestData/sample-*.jsonl` — lignes valides + ligne corrompue + dernière ligne tronquée + lignes non-assistant + prose contenant « five_hour ».
- [ ] `tests/Chronos.Tests/ServicesLayerPurityTests.cs` — réflexion : aucun assembly WPF (`PresentationCore`/`PresentationFramework`/`WindowsBase`) référencé par les types de `Chronos.Services`/`Chronos.Models`.
- [ ] Providers construits avec **chemins injectés** vers `TestData` / dossiers temp — ne jamais toucher le vrai `~/.claude`.
- Framework install : **aucun** — l'infra de test (xUnit) est déjà en place et verte.

## Sources

### Primary (HIGH confidence)
- Doc officielle `code.claude.com/docs/en/statusline` — table `rate_limits` (used_percentage 0-100, resets_at epoch secondes), conditions de présence, déclencheurs/debounce 300 ms, annulation en vol, `refreshInterval`, comportement Windows (Git Bash/PowerShell), § « Rate limit usage ». **Confirme mot pour mot le schéma.**
- `docs/data-sources.md` (projet, capturé 2026-07-08) — source de vérité verrouillée : schéma, repli JSONL, staleness, faux positifs, layout subagents.
- Sondage environnement local (2026-07-08) — `node v24.14.1`, `dotnet 10.0.201`, `WindowsDesktop.App 8.0.25`, `%APPDATA%\Chronos` absent, `~/.claude/projects` peuplé.
- `~/.claude/settings.json` (lecture seule) — commande statusLine active = `node ".../gsd-statusline.js"`.
- `.planning/research/ARCHITECTURE.md` — patterns providers/threading/frontière Services, build order, anti-patterns.
- `.planning/research/STACK.md` (via CLAUDE.md) — versions vérifiées NuGet (CommunityToolkit.Mvvm 8.4.2, Extensions 8.0.x), System.Text.Json in-box.

### Secondary (MEDIUM confidence)
- System.Text.Json `JsonSerializerOptions` (ReadCommentHandling, AllowTrailingCommas, NumberHandling, PropertyNameCaseInsensitive), `JsonDocument`, `DateTimeOffset.FromUnixTimeSeconds` / `Parse(RoundtripKind)` — connaissance établie, APIs stables net8.0.
- Écriture atomique Node `fs.renameSync` (MoveFileEx MOVEFILE_REPLACE_EXISTING sur Windows) — comportement documenté Node/OS.

### Tertiary (LOW confidence)
- Stabilité inter-versions du schéma `rate_limits` en runtime 2.1.202 — non vérifié champ par champ → test de contrat recommandé.

## Metadata

**Confidence breakdown :**
- Standard stack : **HIGH** — aucune nouvelle dépendance, versions in-box/déjà référencées, environnement sondé.
- Contrat statusLine / mapping : **HIGH** — confirmé mot pour mot par doc officielle + binaire local.
- Architecture (providers/records/composite/DI) : **HIGH** — patterns standards, alignés ARCHITECTURE.md et code Phase 1.
- Pont Node non destructif : **HIGH** sur le design (contraintes doc explicites) ; **MEDIUM** sur l'installation auto (dépend de la config utilisateur, à valider en exécution).
- Estimation JSONL : **HIGH** sur l'honnêteté (Option a) ; **LOW** sur toute utilization dérivée (plafonds non publiés/mobiles).
- Stabilité inter-versions : **MEDIUM** — API privée de facto.

**Research date :** 2026-07-08
**Valid until :** ~2026-08-07 pour la partie .NET/architecture (stable) ; **revalider à chaque MAJ majeure de Claude Code** pour le schéma `rate_limits` (API privée de facto).
