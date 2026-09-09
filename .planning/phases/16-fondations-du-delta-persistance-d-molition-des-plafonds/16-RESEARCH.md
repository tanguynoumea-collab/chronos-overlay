# Phase 16 : Fondations du delta — persistance & démolition des plafonds — Research

**Researched:** 2026-09-09
**Domain:** C# / .NET 8 / WPF — persistance `System.Text.Json`, démolition de sous-système, refonte de contrat de service
**Confidence:** HIGH (tout le cœur est vérifié sur la base de code réelle et empiriquement sur la machine ; les 3 zones LOW/MEDIUM sont nommées)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
Aucune décision utilisateur explicite : la phase de discussion est désactivée (`workflow.skip_discuss=true`).
Les contraintes ci-dessous sont **non négociables** et priment sur toute recommandation de ce document.

### Contraintes non négociables (verbatim CONTEXT.md)
- **Ne jamais présenter une estimation comme un chiffre exact** — Core Value du projet.
- MVVM strict, `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers Models/Views/ViewModels/Services.
- Aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`).
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin, aucune dépendance native.
- Lecture tolérante : fichier absent / corrompu → dégradation, jamais de crash, jamais de valeur inventée.
- `AppContext.BaseDirectory` ou `Environment.GetFolderPath`, **jamais** `Assembly.Location` (mono-fichier).
- UI et commentaires en **français**.
- **Baseline à l'entrée de la phase : 405 tests xUnit verts.** Ils doivent le rester.

### Migration des réglages (DEL-06) — exigence de non-régression (verbatim CONTEXT.md)
`%APPDATA%\Chronos\settings.json` en production contient aujourd'hui les six champs à supprimer :
`FiveHourTokenBudget`, `WeeklyTokenBudget`, `FiveHourBudgetSource`, `WeeklyBudgetSource`,
`FiveHourBudgetCalibratedAt`, `WeeklyBudgetCalibratedAt`. Le fichier réel contient AUSSI, et doit les
conserver intacts : `Corner`, `MonitorDeviceName`, `X`, `Y`, `Background`, `RefreshIntervalSeconds`,
`WeeklyAnchor`, `OAuthUsageEnabled`, `InnerStatusLineCommand`, `StatusLinePromptDismissed`, `ThemeKey`,
`SessionsWidgetEnabled`, `SessionsX`, `SessionsY`, `CadranMode`, `CadranStyle`, `SessionStyle`,
`VerticalLayout`. Un fichier portant les anciens champs doit s'ouvrir sans erreur : les champs obsolètes
sont ignorés, pas fatals.

### Claude's Discretion
**Tous** les choix d'implémentation. S'appuyer sur le goal de la ROADMAP, les 5 critères de succès et les
conventions de la base de code.

### Deferred Ideas (OUT OF SCOPE)
Aucune — la phase de discussion a été sautée.

### Hors périmètre rappelé par la ROADMAP (à ne PAS anticiper)
La refonte de la doctrine du composite (EXA-02 / EXA-04 / EXA-05 / DEL-03 / DEL-04) est la **phase 19**.
Ici on livre les briques ; on ne change pas encore la règle de choix de source (`CompositeUsageProvider.Best`)
ni l'affichage.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **EXA-01** | Le dernier relevé exact est persisté sur disque avec son horodatage et rechargé au démarrage | §Q3 (schéma `last-exact.json`, écrivain unique = décorateur en tête de chaîne, `FractionTimeRemaining` recalculé et non persisté, purge des fenêtres déjà « rollées ») + §Code Examples 1-2 + motif atomique existant (`ArchiveStore` / `SettingsService`) |
| **DEL-01** | Les transcripts JSONL répondent à « réponse assistant depuis T ? » sans produire de pourcentage | §Q2 (contrat `ITranscriptActivitySource` + record `TranscriptActivity.HasActivity`) + §Code Examples 3 |
| **DEL-02** | Les transcripts JSONL fournissent la somme de tokens depuis T | §Q2 (`TranscriptActivity.Tokens`, borne par fenêtre, une seule passe disque, horizon 8 j exposé) + §Pitfall 6 (sous-comptage Cowork = borne INFÉRIEURE) |
| **DEL-05** | Le sous-système de plafonds disparaît du code, des réglages et du menu | §Q5 (inventaire grep exhaustif des 14 points d'accroche + ordre de démolition en 3 vagues compilables) + §Runtime State Inventory (piège `obj/**/BudgetDialog.g.cs`) |
| **DEL-06** | Les réglages existants contenant d'anciens plafonds sont migrés sans casse | §Q4 — **vérifié empiriquement** sur le vrai `settings.json` de la machine : aucune migration active nécessaire, `System.Text.Json` ignore les membres inconnus par défaut ; il faut un TEST de non-régression, pas du code de migration |
</phase_requirements>

---

## Summary

Cette phase n'a pas de risque technologique : aucune bibliothèque nouvelle, aucune API inconnue, aucun
appel réseau. Le risque est **de conception et d'ordonnancement** — trois questions décidaient de la qualité
du plan, et ce document les tranche avec des preuves.

**Premièrement, DEL-06 ne demande aucun code.** J'ai reconstruit le record `ChronosSettings` amputé de ses six
champs et désérialisé le **vrai `%APPDATA%\Chronos\settings.json` de la machine** avec les options exactes de
`SettingsService` : aucune exception, les 18 préférences survivantes intactes, y compris
`FiveHourBudgetSource: "Manual"` (enum supprimé → simplement ignoré) et même un champ inconnu de type
incohérent. La « migration » est le comportement par défaut de `System.Text.Json`
(`JsonUnmappedMemberHandling.Skip`). Le livrable DEL-06 est donc un **test de non-régression avec fixture
réelle**, pas un migrateur. Écrire un migrateur serait du code mort à maintenir.

**Deuxièmement, le sort de `JsonlEstimationProvider`.** L'option la plus sûre est la plus radicale : le
convertir immédiatement en `TranscriptActivityProvider : ITranscriptActivitySource` (donc **plus du tout un
`IUsageProvider`**) et le sortir de la chaîne composite dès la phase 16. Raison : garder un `IUsageProvider`
dégradé qui renvoie `Unavailable` (option b) est du code mort de trois phases, et laisse ouverte la porte que
DEL-05/EXA-04 ferment. La question du planner — « l'affichage est-il dégradé entre 16 et 19 ? » — a une
réponse chiffrée : sur la machine réelle, l'anneau 5 h est aujourd'hui alimenté par `usage.json` marqué
`Exact` (10 %, gelé) et **n'est pas touché** ; seul l'anneau hebdo perd sa couleur, parce qu'il était le seul
consommateur de `tokens / 5 817 635 413` — le chiffre que le milestone déclare faux. Le compte à rebours
hebdo, lui, survit (il vient de `WeeklyRecalibration` + `WeeklyAnchor`, pas du plafond). Perdre une couleur
mensongère n'est pas une dégradation au sens de ce projet ; c'est le but. Et le magasin EXA-01 rebouche le
trou pour toute fenêtre ayant déjà eu un relevé exact.

**Troisièmement, la forme du delta.** Un delta global unique depuis T ne convient pas aux deux fenêtres : le
relevé exact porte deux `resets_at` distincts, et si la fenêtre 5 h a « roulé » depuis T, la bonne borne
basse n'est plus T mais l'instant du reset. Le contrat doit donc être **borné par fenêtre**, sans pour autant
relire le disque deux fois. La forme recommandée sépare l'I/O de la requête : une passe disque
(`ReadAsync(now)`) qui matérialise le journal trié en mémoire — exactement le « Pattern 2 » déjà documenté
dans le provider actuel — puis une méthode **pure** `Since(t)` appelable N fois sans I/O. C'est aussi ce qui
rend DEL-01/DEL-02 testables sans fixture disque.

**Primary recommendation:** 3 vagues. **Vague A** (2 chantiers parallèles, indépendants) : (A1) démolition de
l'UI de calibration ; (A2) démolition du calibrateur + de la logique pure. **Vague B** : transformation de
`JsonlEstimationProvider` → `TranscriptActivityProvider` + sortie de la chaîne composite (c'est ce geste qui
libère les 6 champs de leur dernier lecteur). **Vague C** : suppression des 6 champs + `BudgetSource` +
`DiagnosticService` + test DEL-06. Le magasin EXA-01 (`LastExactStore` + décorateur `LastExactUsageProvider`)
est **totalement indépendant** de cette chaîne et se plaçe en parallèle de la vague A.

---

## Project Constraints (from CLAUDE.md)

Directives actionnables extraites de `./CLAUDE.md` (autorité égale aux décisions verrouillées) :

| Directive | Impact sur la phase 16 |
|---|---|
| Stack **imposée** : C# / .NET 8 (`net8.0-windows`) / WPF / MVVM CommunityToolkit + MS.Extensions.DI + Hosting | Aucune nouvelle dépendance NuGet n'est autorisée ni nécessaire (§Standard Stack) |
| **Aucune dépendance native** ; arcs en XAML pur | Sans objet ici (phase services/données) |
| Chemins **sous `%USERPROFILE%` / `%APPDATA%` uniquement**, aucun droit admin | `last-exact.json` va dans `%APPDATA%\Chronos\` via `ChronosPaths`, jamais ailleurs |
| **`utilization`/`resets_at` prioritaires sur le comptage de tokens** ; ne jamais présenter une estimation comme exacte | Fondement direct de la suppression de l'utilization JSONL |
| **Robustesse** : aucune source ≠ crash → état « données indisponibles » ; parsing tolérant | `LastExactStore.Load()` doit renvoyer `null` sur fichier absent/corrompu, jamais lever |
| **MVVM strict**, dossiers Models/Views/ViewModels/Services | `LastExactStore` et `TranscriptActivityProvider` vont dans `Services/` ; suppressions symétriques dans `Views/` et `ViewModels/` |
| **UI et commentaires en français** | Les XML-doc des nouveaux types en français, comme tout le reste du dépôt |
| **Interdit : `Assembly.Location` / `GetExecutingAssembly().Location`** (vide en mono-fichier) | Utiliser `Environment.GetFolderPath` — c'est déjà ce que fait `ChronosPaths.Default()` |
| **Interdit : `PublishTrimmed=true`, `PublishAot=true`** avec WPF | Sans objet (aucun changement de packaging en phase 16) |
| **GSD Workflow Enforcement** : pas d'édition hors commande GSD | Le plan doit passer par `/gsd:execute-phase` |
| Statut : « Conventions not yet established » / « Architecture not yet mapped » dans CLAUDE.md | Les conventions réelles sont dans le code : suivre les motifs existants listés en §Architecture Patterns |

---

## Standard Stack

### Core — **aucun ajout**

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Text.Json` | intégré à net8.0 | Sérialisation de `last-exact.json`, lecture tolérante des JSONL et de `settings.json` | **Déjà dans le framework, déjà utilisé partout** dans `Services/` (`SettingsService`, `ArchiveStore`, `TreatedStore`, `ClaudeUsageObjectProvider`, `JsonlEstimationProvider`). Rien à installer. |
| `Microsoft.Extensions.DependencyInjection` | 8.0.x (déjà référencée) | Enregistrement du décorateur `LastExactUsageProvider` en tête de chaîne | Déjà le conteneur du projet (`App.ConfigureServices`) |
| `xunit` | 2.9.2 (déjà référencée) | Tests | Framework en place, 405 tests verts |
| `Xunit.StaFact` | 1.1.11 (déjà référencée) | `[WpfFact]` pour `CompositionRootTests` | Déjà utilisé, requis dès qu'une `Window` est construite |

### Supporting — **aucun ajout**

Aucune bibliothèque supplémentaire n'est justifiable pour cette phase. Toute proposition d'ajout de package
(sérialiseur, mapper, migrateur de config) doit être refusée : les motifs existants du dépôt couvrent 100 %
du besoin.

**Installation :** *rien à installer.*

**Vérification de version :** non applicable — aucun nouveau package. La version du SDK et l'état du dépôt
sont vérifiés en §Environment Availability.

---

## Réponses aux 6 questions ouvertes

### Q1 — Le sort de `JsonlEstimationProvider` dans la chaîne composite

**Recommandation : option (c) + (a) combinées.** Convertir immédiatement en un type qui n'implémente PAS
`IUsageProvider`, et le sortir de la chaîne composite dès la phase 16.

| Option | Verdict | Raison |
|---|---|---|
| (a) sortir de la chaîne, composite se termine sur statusLine | **Retenu**, en corollaire de (c) | Un type qui n'est plus un `IUsageProvider` ne PEUT plus être dans la chaîne. Les deux décisions n'en font qu'une. |
| (b) `IUsageProvider` dégradé renvoyant `Unavailable` jusqu'à la phase 19 | **Rejeté** | Trois phases de code mort, un maillon composite qui ne sert à rien, et une porte laissée ouverte que DEL-05/EXA-04 doivent fermer. Coût de maintenance non nul, bénéfice nul : un maillon `Unavailable` ne change RIEN à `Best()` (rang 0), donc il ne protège aucun affichage. |
| (c) conversion immédiate en `ITranscriptActivitySource` | **Retenu** | Rend EXA-04 structurellement vrai dès la phase 16 : plus aucun type du dépôt ne sait fabriquer une `Utilization` à partir d'un comptage de tokens. C'est la garantie la plus forte possible, et elle est gratuite. |

**La base reste-t-elle compilable et les tests verts ?** Oui — voir §Q5 (ordre) et §Q6 (tests). Le seul
maillon à retirer d'`App.xaml.cs` est la ligne 296 ; le composite le plus interne devient directement
`sp.GetRequiredService<ClaudeUsageObjectProvider>()`. `CompositeUsageProvider` lui-même n'est pas touché, et
ses 9 tests (qui n'utilisent que des fakes) restent verts **sans modification** — vérifié.

**L'affichage est-il dégradé entre la phase 16 et la phase 19 ?** Analyse chiffrée sur l'état RÉEL de la
machine (`%APPDATA%\Chronos\usage.json` = `{"five_hour":{"used_percentage":10,"resets_at":9},"capturedAt":…}`) :

| Élément affiché | Aujourd'hui | Après phase 16 | Verdict |
|---|---|---|---|
| Anneau **5 h** — pourcentage | `usage.json` → 10 %, marqué `Exact` (bat l'estimation par `Best()`) | **Identique** | Aucun changement |
| Anneau **5 h** — géométrie | `resets_at:9` (epoch 1970) → fraction clampée à 0 | **Identique** | Aucun changement |
| Anneau **hebdo** — couleur | JSONL `Estimated`, `tokens / 5 817 635 413` (plafond Max x5, faux ~4×) | **Gris** (`Utilization = null`) | **Changement visible** — perte d'une couleur fausse |
| Anneau **hebdo** — compte à rebours | `WeeklyRecalibration.Apply` synthétise `ResetsAt` depuis `WeeklyAnchor` | **Conservé** — `Apply` ne teste que `Exact + ResetsAt≠null` puis l'ancre, il agit aussi sur une fenêtre `Unavailable` (vérifié `WeeklyRecalibration.cs:31-39`) | Aucun changement |
| Texte « ≈ N tokens » (`HasTokens`) | Affiché sur l'anneau hebdo (`Estimated` + tokens>0) | **Disparaît** — plus aucun producteur d'`Estimated` | **Changement visible**, cohérent avec EXA-04 |
| Bandeau « données indisponibles » (`DataUnavailable`) | false (exige les DEUX fenêtres `Unavailable`) | **false** — la 5 h reste `Exact` | Aucune régression |

**Conclusion à retenir par le planner :** l'app n'est jamais cassée entre 16 et 19. Deux éléments visuels
disparaissent, tous deux dérivés du comptage de tokens que le milestone abolit. Le magasin EXA-01 rebouche le
trou hebdo dès qu'un relevé exact hebdo aura existé une fois. **Ces deux disparitions doivent être écrites
explicitement dans le plan** (attendues, non régressives), sinon la vérification de fin de phase les
signalera comme des bugs.

**Effet de bord à documenter :** après la phase 16, **plus aucune source ne produit
`SourceReliability.Estimated`**. Ne PAS supprimer la valeur de l'enum : la phase 19 (DEL-04, « exact + delta
marqué ») en aura besoin comme marqueur, ou en introduira un troisième. `PercentFormatter` (préfixe « ~ ») et
`WeeklyRecalibration` restent en place, dormants — c'est voulu.

**Nommage recommandé :** `Services/TranscriptActivityProvider.cs` (le fichier `JsonlEstimationProvider.cs` est
renommé, pas dupliqué — le parcours disque doit être conservé à l'identique, voir §Don't Hand-Roll).

---

### Q2 — Forme exacte du contrat de delta

**Le delta doit être borné PAR FENÊTRE, pas global.** Preuve : le relevé exact porte deux `WindowState` avec
des `ResetsAt` distincts. Si `now > ResetsAt_5h(T)`, la fenêtre 5 h a été remise à zéro depuis T ; corriger
son utilization avec « tokens depuis T » sur-compterait tout ce qui précède le reset. La borne basse correcte
est donc `max(T, instant du dernier reset de CETTE fenêtre)` — différente pour la 5 h et pour l'hebdo.

**Recommandation : un seul I/O, une requête pure appelable N fois.**

```csharp
namespace Chronos.Services;

/// <summary>Résultat borné d'une interrogation des transcripts (DEL-01 + DEL-02). Aucun pourcentage.</summary>
public sealed record TranscriptActivity(bool HasActivity, long Tokens, DateTimeOffset? LastActivityAt);

/// <summary>Journal MATÉRIALISÉ des réponses assistant récentes. PUR : aucune E/S, interrogeable N fois.</summary>
public sealed class TranscriptActivityLog
{
    public DateTimeOffset Now { get; }
    /// <summary>Borne basse au-delà de laquelle le journal ne sait RIEN (filtre mtime 8 j). Une requête
    /// dont le « depuis » est antérieur à cet horizon n'est pas fiable — le caller doit refuser le delta.</summary>
    public DateTimeOffset Horizon { get; }
    public bool Covers(DateTimeOffset since) => since >= Horizon;
    /// <summary>Activité et tokens dans ]since ; Now]. Borne basse EXCLUSIVE (voir Pitfall 5).</summary>
    public TranscriptActivity Since(DateTimeOffset since) { /* … LINQ en mémoire … */ }
}

/// <summary>Source de DELTA (DEL-01/DEL-02). N'implémente VOLONTAIREMENT PAS IUsageProvider :
/// les transcripts ne produisent plus jamais d'utilization absolue (EXA-04).</summary>
public interface ITranscriptActivitySource
{
    Task<TranscriptActivityLog> ReadAsync(CancellationToken ct = default);
}
```

**Pourquoi cette forme plutôt qu'une méthode unique `ActivitySince(T)` :**

| Critère | `ActivitySince(T)` seule | `ReadAsync()` + `Since(t)` pur |
|---|---|---|
| Deux fenêtres à bornes différentes | **2 passes disque** (scan récursif de ~milliers de `.jsonl`) | **1 passe**, 2 requêtes mémoire |
| Cohérence des deux réponses | Deux instantanés disque différents → incohérence possible | Un seul instantané → cohérent par construction |
| Testabilité de la logique de bornage | Exige une fixture disque par cas | `Since()` testable en pur, sans fichier |
| Conforme au code existant | — | **Oui** : `JsonlEstimationProvider` documente déjà « Une SEULE passe disque : on matérialise (timestamp, tokens) ; les fenêtres sont calculées EN MÉMOIRE (Pattern 2) » |
| Signale l'horizon 8 j | Impossible sans 3ᵉ méthode | `Horizon`/`Covers` intégrés |

**Deux méthodes séparées (`HasActivitySince` + `TokensSince`) : rejeté.** Elles répondent à partir de la même
donnée ; les séparer force soit deux passes disque, soit un cache implicite fragile. Un seul record les porte
toutes les deux, plus `LastActivityAt` — sous-produit gratuit de la même passe, dont la phase 19 (marge
d'incertitude DEL-04) et la phase 20 (EXA-06 « depuis quand ») auront besoin.

**`HasActivity` est redondant avec `LastActivityAt is not null`** — c'est assumé : DEL-01 demande
explicitement une réponse booléenne, et l'expliciter dans le record rend l'intention lisible au point d'appel.

**`Horizon` n'est pas décoratif.** Le provider actuel filtre les fichiers sur `mtime >= now - 8 j`
(`JsonlEstimationProvider.cs:146`). Si le dernier relevé exact date de plus de 8 jours, le delta calculé sera
silencieusement sous-évalué. Exposer `Horizon` permet à la phase 19 de répondre « indisponible » plutôt qu'un
delta faux — c'est la Core Value appliquée au delta.

---

### Q3 — Format et emplacement du magasin persistant (EXA-01)

**Emplacement :** `%APPDATA%\Chronos\last-exact.json`, résolu par une **propriété calculée** de `ChronosPaths`,
exactement comme `SettingsFile` :

```csharp
public string LastExactFile => Path.Combine(Path.GetDirectoryName(UsageFile)!, "last-exact.json");
```

**Pourquoi une propriété calculée et pas un 3ᵉ paramètre du ctor positionnel :** le record est
`ChronosPaths(string UsageFile, string ProjectsRoot)` et **de très nombreux tests** construisent
`new ChronosPaths(usage, projects)` avec un dossier temp. Une propriété calculée leur donne un
`last-exact.json` isolé **gratuitement**, sans toucher une seule signature de test. Ajouter un paramètre
casserait des dizaines de sites d'appel. Le commentaire XML de `SettingsFile` documente déjà ce raisonnement —
suivre le motif à l'identique.

**Aucun risque de boucle de rafraîchissement :** `RefreshOrchestrator.CreateWatcher()` instancie
`new FileSystemWatcher(dir, "usage.json")` — le filtre est nominatif. Écrire `last-exact.json` dans le même
dossier ne déclenche donc **aucun** événement. Vérifié (`RefreshOrchestrator.cs:77`).

**Schéma recommandé (versionné, par fenêtre) :**

```json
{
  "version": 1,
  "five_hour": {
    "utilization": 0.42,
    "resets_at":  "2026-09-09T18:30:00+00:00",
    "captured_at":"2026-09-09T15:12:03+00:00"
  },
  "seven_day": {
    "utilization": 0.63,
    "resets_at":  "2026-09-14T09:00:00+00:00",
    "captured_at":"2026-09-09T15:12:03+00:00"
  }
}
```

Décisions de schéma, chacune avec son POURQUOI :

| Champ | Persisté ? | Raison |
|---|---|---|
| `Utilization` | **Oui** | C'est le chiffre exact à sauver — la raison d'être d'EXA-01 |
| `ResetsAt` | **Oui** | Indispensable pour recalculer la géométrie ET pour détecter qu'une fenêtre a « roulé » |
| `captured_at` (**par fenêtre**) | **Oui** | Les deux fenêtres peuvent venir de **sources différentes à des instants différents** (le composite choisit par fenêtre). Un `SourceCapturedAt` unique au niveau snapshot serait ambigu et mentirait sur l'âge de l'une des deux. |
| `version` | **Oui** | Les phases 18/19 ajouteront statut serveur (`allowed`/`rejected`) et `overage` ; un entier de schéma évite un parsing devinatoire |
| `FractionTimeRemaining` | **NON — recalculé au chargement** | C'est une fonction pure de `(ResetsAt, now, longueur de fenêtre)`. La persister, c'est ressusciter une fraction périmée au démarrage = **un chiffre inventé**, violation directe de la Core Value. Recalculer via `WindowState.FractionRemaining(resetsAt, now, len)` — la méthode existe déjà. |
| `Reliability` | **NON — dérivé** | Seules des fenêtres `Exact` sont écrites. Ne pas le persister empêche un fichier corrompu ou édité à la main d'injecter un faux `Exact`. Sécurité par construction. |
| `EstimatedTokens` | **NON** | Une fenêtre `Exact` n'en porte jamais (`CadranBindingTests:149` : « honnêteté : jamais de tokens affichés en source Exact »), et c'est précisément le comptage qu'on abolit |
| `Kind` | **NON — implicite** | Porté par la clé `five_hour` / `seven_day` |

**Format des dates : ISO 8601 avec offset** (`"o"` / `DateTimeOffset.TryParse` + `RoundtripKind`) — pas
d'epoch. Cohérent avec `WeeklyAnchor` dans `settings.json`, et cela évite de rejouer le piège d'unité HDR-05
au sein du dépôt : la normalisation epoch↔ISO reste à la frontière des providers, jamais dans le stockage
interne.

**Garde d'honnêteté au chargement (à implémenter en phase 16) :** si `resets_at <= now`, la fenêtre a été
remise à zéro depuis la capture — l'utilization persistée ne décrit plus rien. **Rejeter cette fenêtre**
(`null`), plutôt que de servir un « 80 % » dont le reset est passé. Ce n'est pas la limite d'âge d'EXA-02
(phase 19) : c'est une invalidation logique et non temporelle, et ne pas la poser reviendrait à livrer en
phase 16 un mensonge structurel.

**Qui écrit : un point central unique — un décorateur en tête de chaîne, PAS chaque provider.**

```
IUsageProvider (enregistré en DI)
└── LastExactUsageProvider (nouveau décorateur)
    └── CompositeUsageProvider(ChronosOAuth, Composite(GatedOAuth, ClaudeUsageObject))
```

| Écrivain candidat | Verdict |
|---|---|
| Chaque provider exact | **Rejeté** — duplication dans 2 providers aujourd'hui, 3 après la phase 18 ; chacun a déjà son propre cache RAM (`_cached`/`_cachedAt`) qui ferait doublon |
| `RefreshOrchestrator` (abonnement à `SnapshotChanged`) | **Rejeté** — c'est exactement le piège dans lequel `BudgetAutoCalibrator` est tombé : abonnement à forcer par un `GetRequiredService` **avant** `StartAsync` (`App.xaml.cs:73`), fragile et invisible au compilateur. On démolit ce motif dans cette même phase ; ne pas le reproduire. |
| **Décorateur `IUsageProvider` en tête de chaîne** | **Retenu** — voit exactement le snapshot fusionné qui sera affiché ; `RefreshOrchestrator` **et** `DiagnosticService` consomment tous deux `IUsageProvider` et en bénéficient sans câblage supplémentaire ; composable pour la phase 18 ; testable avec un fake ; zéro type WPF |

**Ce que fait le décorateur en phase 16 (et rien de plus) :**
1. délègue au composite ;
2. pour chaque fenêtre `Exact` du résultat → **écrit** dans le magasin (avec son `captured_at`) ;
3. pour chaque fenêtre **`Unavailable`** du résultat, si le magasin détient une fenêtre exacte encore
   valide (reset non passé) → **la substitue** en `Exact`, avec son `captured_at` d'origine.

Le point 3 est **strictement du rebouchage de trou** : il ne remplace jamais une réponse vivante, donc il ne
touche pas la doctrine de `CompositeUsageProvider.Best()` (qui n'est pas modifiée d'une ligne) et ne peut pas
régresser l'affichage. C'est ce qui rend le critère de succès n°1 vérifiable dès la phase 16 (relance hors
connexion → le chiffre revient), tout en laissant la phase 19 poser la limite d'âge et le delta **sur ce même
point de couture**.

**Conséquence de modèle : ajouter `DateTimeOffset? CapturedAt` à `WindowState`.** Additif et nullable, donc
aucun site de construction (tous en initialiseurs d'objet) n'est cassé, et l'unique assertion d'égalité de
valeur sur `WindowState` (`WeeklyRecalibrationTests.cs:43`) reste verte (null des deux côtés). C'est le champ
dont la phase 20 a besoin pour EXA-06 (« quelle source, et depuis quand ») et EXA-03 (`IsStale` par fenêtre).
`UsageSnapshot.SourceCapturedAt` reste en place et inchangé — l'ajouter au niveau fenêtre ne casse rien.

---

### Q4 — Migration des réglages (DEL-06) — **VÉRIFIÉ EMPIRIQUEMENT**

**Méthode :** projet console jetable `net8.0`, record `ChronosSettings` reconstruit **sans** les six champs,
`JsonSerializerOptions` copiées à l'identique depuis `SettingsService.cs:25-32`
(`PropertyNameCaseInsensitive`, `AllowTrailingCommas`, `ReadCommentHandling.Skip`, `WriteIndented`,
`JsonStringEnumConverter`), désérialisation du **vrai fichier** `%APPDATA%\Chronos\settings.json`.

**Résultats (exécutés le 2026-09-09) :**

| Test | Résultat |
|---|---|
| Désérialiser le vrai `settings.json` de prod (6 champs obsolètes, dont `FiveHourBudgetSource: "Manual"` et `WeeklyBudgetSource: "Auto"`) avec le record amputé | **Aucune exception.** `Corner=BottomRight`, `MonitorDeviceName=\\.\DISPLAY2`, `X=-182,4`, `Y=921,6`, `WeeklyAnchor=2026-07-11T00:00:00+02:00`, `ThemeKey=ardoise`, `SessionsWidgetEnabled=True`, `SessionsX/Y` intacts, `CadranMode=Normal`, `CadranStyle=Arcs`, `SessionStyle=Veilleurs`, `VerticalLayout=True`, `OAuthUsageEnabled=True`, `RefreshIntervalSeconds=60` — **les 18 préférences survivantes intactes** |
| Champ enum supprimé portant une valeur **invalide** (`"FiveHourBudgetSource":"NimporteQuoi"`) | **Ignoré**, aucune exception |
| Champ supprimé portant un **type incohérent** (`"FiveHourTokenBudget":{"a":[1,2,3]}`) | **Ignoré**, aucune exception |
| Ré-sérialisation après chargement | Les 6 champs obsolètes **disparaissent du fichier** ; les 18 autres réécrits à l'identique |
| Round-trip de `WeeklyAnchor` | `2026-07-11T00:00:00.0000000+02:00` — offset `+02:00` **conservé** |

**Explication :** `System.Text.Json` ignore les membres JSON non mappés par défaut
(`JsonUnmappedMemberHandling.Skip`). Un champ absent du type cible est **skippé au niveau du lecteur**, avant
toute tentative de conversion — c'est pourquoi ni un enum supprimé ni un type incohérent ne peuvent lever.

**Conclusions actionnables pour le planner :**
1. **Aucune option explicite à ajouter.** Ne pas toucher aux `JsonSerializerOptions` de `SettingsService`.
   En particulier, **ne jamais poser `JsonUnmappedMemberHandling.Disallow`** — ce serait exactement l'inverse
   du besoin.
2. **Aucune migration active à écrire.** La purge du fichier se fait **passivement**, au premier `Save()`
   (déclenché par n'importe quel réglage : déplacement de l'overlay, changement de thème, toggle…). Écrire un
   migrateur explicite serait du code mort à maintenir pour un problème inexistant.
3. **Le livrable DEL-06 est un TEST**, pas du code : un test de non-régression qui charge une **fixture
   figée** reproduisant le `settings.json` réel de production (les 6 obsolètes + les 18 survivants) et assert
   (a) aucune exception, (b) chacune des 18 préférences est restituée à sa valeur du fichier. La fixture doit
   vivre dans `tests/Chronos.Tests/TestData/settings-legacy-plafonds.json` (motif existant :
   `claude-settings-pollue.json`, la fixture de l'état réel pollué de la phase 15).
4. Le `catch` de `SettingsService.Load()` (`IOException or JsonException or ArgumentException`) reste adéquat
   et ne demande aucun élargissement.

---

### Q5 — Ordre de démolition sûr

#### Inventaire des points d'accroche (grep exhaustif, hors `obj/`)

| # | Fichier | Ligne(s) | Nature |
|---|---|---|---|
| 1 | `src/Chronos/App.xaml.cs` | 73 | `_ = _host.Services.GetRequiredService<BudgetAutoCalibrator>();` (résolution forcée avant `StartAsync`) |
| 2 | `src/Chronos/App.xaml.cs` | 200-201 | `services.AddSingleton<IBudgetPrompt, BudgetPrompt>();` + commentaire CAL-01 |
| 3 | `src/Chronos/App.xaml.cs` | 261, 288-296 | `AddSingleton<JsonlEstimationProvider>()` + maillon final de la chaîne composite |
| 4 | `src/Chronos/App.xaml.cs` | 318-325 | Enregistrement DI de `BudgetAutoCalibrator` |
| 5 | `src/Chronos/ViewModels/MainViewModel.cs` | 28, 185, 194 | Champ `_budgetPrompt`, paramètre de ctor `IBudgetPrompt`, affectation |
| 6 | `src/Chronos/ViewModels/MainViewModel.cs` | 325-348 | `[RelayCommand] CalibrateBudgets()` |
| 7 | `src/Chronos/Views/SettingsWindow.xaml` | 306-307 | `<Button Content="Plafonds…" Command="{Binding CalibrateBudgetsCommand}"/>` (**c'est l'entrée « menu » à supprimer**) |
| 8 | `src/Chronos/Services/DiagnosticService.cs` | 283-284 | `s.FiveHourTokenBudget` / `s.WeeklyTokenBudget` dans le rapport |
| 9 | `src/Chronos/Services/DiagnosticService.cs` | 397 | Ligne de conseil « (Repli : « Calibrer les plafonds… » colore une estimation…) » |
| 10 | `src/Chronos/Services/DiagnosticService.cs` | 471 | `"% inconnu (pas de plafond → gris)"` — libellé à reformuler |
| 11 | `src/Chronos/Services/JsonlEstimationProvider.cs` | 37, 79-80, 87-130 | Lecture des plafonds + calcul d'`Utilization` |
| 12 | `src/Chronos/Services/GatedOAuthUsageProvider.cs` | 7 | Commentaire « comme JsonlEstimationProvider relit ses plafonds » — à corriger |
| 13 | `src/Chronos/Services/RefreshOrchestrator.cs` | 93-95 | XML-doc de `RequestRefresh` citant CAL-01 — à reformuler (la méthode reste, `ToggleOAuthUsage` l'appelle) |
| 14 | Fichiers à **supprimer** | — | `Services/BudgetCalibration.cs`, `Services/BudgetAutoCalibrator.cs`, `Services/BudgetSource.cs`, `Services/IBudgetPrompt.cs`, `Views/BudgetPrompt.cs`, `Views/BudgetDialog.xaml`, `Views/BudgetDialog.xaml.cs`, `ViewModels/BudgetDialogViewModel.cs` |
| 15 | Tests à supprimer | — | `BudgetCalibrationTests.cs` (15), `BudgetAutoCalibratorTests.cs` (3), `Fakes/FakeBudgetPrompt.cs` |
| 16 | Tests à réécrire | — | voir §Q6 |

**Note :** il n'existe **aucun `ContextMenu`/`MenuItem` dans `MainWindow.xaml`** — le « menu » est la
`SettingsWindow`. L'entrée à supprimer est le `Button "Plafonds…"` de la `UniformGrid` « RÉGLAGES »
(`SettingsWindow.xaml:306-307`). Le bouton disparaissant, la `UniformGrid Columns="2"` passe de 4 à 3
boutons : vérifier visuellement que la grille reste équilibrée (ou remplir la case libérée / passer à 3
éléments sur 2 colonnes assumé).

#### Ordre en 3 vagues, chaque commit compilable

**Contrainte structurante :** `JsonlEstimationProvider:79-80` lit `settings.FiveHourTokenBudget` /
`settings.WeeklyTokenBudget`. Tant qu'il n'est pas transformé, **les 6 champs ne peuvent pas être
supprimés**. C'est cette dépendance qui impose l'ordre.

| Vague | Chantier | Contenu | Dépend de |
|---|---|---|---|
| **A1** | Démolition de l'UI de calibration | Points 2, 5, 6, 7 + suppression de `IBudgetPrompt.cs`, `BudgetPrompt.cs`, `BudgetDialog.xaml(.cs)`, `BudgetDialogViewModel.cs`, `FakeBudgetPrompt.cs` + **les 5 sites de construction de `MainViewModel` dans les tests** (arité du ctor) | — |
| **A2** | Démolition du calibrateur | Points 1, 4 + suppression de `BudgetAutoCalibrator.cs` **puis** `BudgetCalibration.cs` + `BudgetAutoCalibratorTests.cs` + `BudgetCalibrationTests.cs` + `CompositionRootTests:62-68,95` | — (indépendant d'A1) |
| **A3** | Magasin du dernier exact (EXA-01) | `ChronosPaths.LastExactFile`, `WindowState.CapturedAt`, `LastExactStore`, `LastExactUsageProvider`, câblage DI en tête de chaîne, nouveaux tests | — (totalement indépendant de la démolition) |
| **B** | Transformation JSONL → source de delta | Points 3, 11, 12 : `JsonlEstimationProvider.cs` → `TranscriptActivityProvider.cs` + `ITranscriptActivitySource` + `TranscriptActivityLog` ; sortie de la chaîne composite ; réécriture de `JsonlEstimationProviderTests.cs` ; `CompositionRootTests:37,40` | A2 (`BudgetAutoCalibrator` injectait le provider concret) |
| **C** | Suppression des 6 champs | Suppression des champs de `ChronosSettings` + `BudgetSource.cs` ; points 8, 9, 10, 13 ; `SettingsServiceTests:48-49,69-70` ; **nouveau test DEL-06 + fixture** | A1, A2, B (tous les lecteurs doivent avoir disparu) |

**Vérification de compilabilité par vague :**
- Après A1 : `BudgetCalibration` et `BudgetAutoCalibrator` référencent encore les 6 champs → **OK**, ils
  existent encore.
- Après A2 : les 6 champs n'ont plus que 3 lecteurs (`JsonlEstimationProvider`, `DiagnosticService`, tests) →
  **OK**, ils existent encore.
- Après B : plus aucun lecteur métier hors `DiagnosticService` → **OK**.
- Après C : `BudgetSource` n'a plus aucun référent → suppression sûre. **`BudgetSource.cs` doit être supprimé
  APRÈS `BudgetCalibration.cs` et APRÈS les 6 champs**, jamais avant.

**Deux gestes atomiques irréductibles** (impossible de les scinder en commits plus petits) :
1. A1 : retirer le paramètre `IBudgetPrompt` du ctor de `MainViewModel` change son arité → les **5** sites de
   construction en test (`MainViewModelTests:63,74,83,111,244` ; `CadranBindingTests:35` ;
   `OverlayWindowConfigTests:24` ; `ThemingTests:97` ; `CompositionRootTests:60`) doivent changer dans le
   **même** commit.
2. C : supprimer un champ du record change `ChronosSettings` → `DiagnosticService` et `SettingsServiceTests`
   dans le même commit.

**Parallélisation :** A1, A2 et A3 sont mutuellement indépendants → 3 plans en vague 1. B en vague 2. C en
vague 3.

---

### Q6 — Ce qui casse dans les 405 tests

Baseline **confirmée par exécution réelle** : `Réussi! — échec : 0, réussite : 405, ignorée(s) : 0, total :
405, durée : 35 s`.

| Fichier | Tests | Sort | Détail |
|---|---|---|---|
| `BudgetCalibrationTests.cs` | **15** | **SUPPRIMER** | Teste `Deduce`/`ApplyAuto` d'une classe supprimée. Rien à sauver : la déduction de plafond est la cause racine du bug. |
| `BudgetAutoCalibratorTests.cs` | **3** | **SUPPRIMER** | Teste un service supprimé |
| `Fakes/FakeBudgetPrompt.cs` | — | **SUPPRIMER** | Fake d'une interface supprimée |
| `MainViewModelTests.cs` | 3 sur 21 | **SUPPRIMER** ces 3 | `CalibrateBudgets_persiste_le_plafond_saisi_en_Manual_et_None_pour_le_champ_vide` (~381), `CalibrateBudgets_annule_ne_persiste_rien` (~401), `CalibrateBudgets_n_ecrase_pas_les_reglages_persistes_par_un_autre_writer` (~421) |
| `MainViewModelTests.cs` | helpers | **RÉÉCRIRE** | Lignes 63, 74, 83 (signatures `NewVm*` avec `FakeBudgetPrompt`) + sites 111, 244 |
| `CompositionRootTests.cs` | 1 sur 3 | **RÉÉCRIRE** | Retirer lignes 37, 40 (`JsonlEstimationProvider` comme `IUsageProvider`), 58-68 (`IBudgetPrompt` + `BudgetAutoCalibrator`), 95. Le composite de test se termine sur `ClaudeUsageObjectProvider`. **Ajouter** l'enregistrement du décorateur `LastExactUsageProvider` (c'est le rôle de cette garde : attraper un câblage qui compile mais casse au démarrage). |
| `CadranBindingTests.cs` | ligne 35 | **RÉÉCRIRE** | Arité du ctor `MainViewModel` uniquement — assertions inchangées |
| `OverlayWindowConfigTests.cs` | ligne 24 | **RÉÉCRIRE** | idem |
| `ThemingTests.cs` | ligne 97 | **RÉÉCRIRE** | idem |
| `SettingsServiceTests.cs` | 2 sur 5 | **RÉÉCRIRE** | `Save_puis_Load_round_trip` (48-49 : retirer les 2 plafonds de la fixture) ; `Load_fichier_absent_redonne_les_defauts` (69-70 : retirer les 2 asserts `Null`) |
| `SettingsServiceTests.cs` | — | **AJOUTER** | Test DEL-06 + fixture `TestData/settings-legacy-plafonds.json` |
| `JsonlEstimationProviderTests.cs` | **8** | **RÉÉCRIRE en bloc** → `TranscriptActivityProviderTests.cs` | détail ci-dessous |
| `CompositeUsageProviderTests.cs` | 9 | **INTACT** — vérifié | N'utilise que des `FakeProvider` ; teste la règle `Best()`, pas le provider concret. Les cas nommés « JSONL » deviennent des cas nommés « repli estimé » — un simple renommage de commentaire suffit (facultatif). |
| `DiagnosticServiceTests.cs` | 2 | **INTACT** — vérifié | Aucune assertion sur les plafonds. Assert `Contains("estimé")` sur un snapshot **stub** fabriqué à la main → reste vert |
| `WindowGaugeViewModelTests.cs`, `CadranBindingTests` (assertions) | 13 + 9 | **INTACT** | Construisent des `WindowState` à la main avec `EstimatedTokens` ; `WindowState` conserve ce champ |
| `WeeklyRecalibrationTests.cs` (7), `FiveHourWindowInferenceTests.cs` (7), `WeeklyWindowTests.cs` (4) | 18 | **INTACT — et à conserver** | Voir avertissement ci-dessous |

**Détail de la réécriture de `JsonlEstimationProviderTests.cs` :**

| Test actuel | Sort | Nouveau test |
|---|---|---|
| `Utilization_5h_estimee_avec_plafond` | **SUPPRIMER** | L'utilization absolue est abolie (EXA-04) |
| `Plafond_defini_laisse_la_fenetre_5h_Estimated_avec_utilization` | **SUPPRIMER** | idem |
| `Valide_somme_par_fenetre_et_marque_Estimated` | **RÉÉCRIRE** | DEL-02 : `Since(2026-07-08T06:00Z)` → `Tokens == 1550` ; `Since(2026-07-01T00:00Z)` → `Tokens == 2150` (fixture `sample-valid.jsonl` : 1550 @11:30, 600 @07-05, 9999 @06-01) |
| `Fenetre_inactive_arc_plein_sans_tokens` | **RÉÉCRIRE** | DEL-01 : `sample-inactive.jsonl` (unique message @06:00), `Since(07:00)` → `HasActivity == false`, `Tokens == 0`, `LastActivityAt == null` |
| `Tolerant_ignore_corrompue_partielle_prose_et_user_sans_exception` | **CONSERVER, assertions adaptées** | ROB-02 : 700 tokens comptés, prose/corrompue/tronquée ignorées |
| `Subagents_inclus_dans_la_somme_recursive` | **CONSERVER, assertions adaptées** | 800 tokens — prouve l'inclusion de `subagents/` |
| `Dossier_absent_renvoie_zero_sans_exception` | **CONSERVER, assertions adaptées** | `HasActivity == false`, `Tokens == 0`, aucune exception |
| — | **AJOUTER** | Borne basse **exclusive** : `Since(11:30:00)` sur `sample-valid.jsonl` → `Tokens == 0` (le message @11:30 n'est PAS recompté) |
| — | **AJOUTER** | `LastActivityAt == 2026-07-08T11:30:00Z` |
| — | **AJOUTER** | `Covers(now - 30 j) == false` (horizon 8 j) |
| — | **AJOUTER** | `Since()` testé **en pur**, sans fichier, sur un `TranscriptActivityLog` construit en mémoire |

**Bilan chiffré :** −18 tests supprimés en bloc, −3 dans `MainViewModelTests`, −2 dans le fichier JSONL,
soit −23 ; +≈16 réécrits/ajoutés côté JSONL, +≈8 pour `LastExactStore`/`LastExactUsageProvider`, +1 DEL-06.
**Cible plausible : ~405 ± 10.** Le critère « 405 tests verts » doit se lire « **0 échec**, et pas de perte de
couverture nette » — pas « exactement 405 ». Le plan doit dire ce chiffre cible explicitement, sinon la
vérification de fin de phase butera sur un écart légitime.

**⚠ Avertissement — code pur qui devient orphelin.** Après la vague B, `FiveHourWindowInference` (seuls
appelants : `JsonlEstimationProvider:75,101,109`) et `WeeklyWindow` (seul appelant :
`JsonlEstimationProvider:119`) n'ont **plus aucun appelant en production**. **NE PAS les supprimer** : ce
serait perdre 11 tests verts et une inférence de fenêtre 5 h que la phase 19 peut vouloir réutiliser (borne
basse d'un delta quand `ResetsAt` est inconnu). Les conserver, en documentant dans leur XML-doc qu'ils sont en
réserve pour la phase 19. `WeeklyRecalibration`, lui, reste **vivant** (`MainViewModel:254`).

---

## Architecture Patterns

### Structure cible après la phase

```
src/Chronos/
├── Models/
│   ├── UsageSnapshot.cs           # inchangé
│   ├── WindowState.cs             # + DateTimeOffset? CapturedAt (additif, nullable)
│   └── SourceReliability.cs       # inchangé — Estimated conservé mais plus produit
├── Services/
│   ├── ChronosPaths.cs            # + LastExactFile (propriété calculée, comme SettingsFile)
│   ├── LastExactStore.cs          # NOUVEAU — persistance atomique du dernier relevé exact (EXA-01)
│   ├── LastExactUsageProvider.cs  # NOUVEAU — décorateur IUsageProvider : écrit + rebouche les trous
│   ├── ITranscriptActivitySource.cs # NOUVEAU — contrat de delta (DEL-01/02), PAS un IUsageProvider
│   ├── TranscriptActivityProvider.cs # RENOMMÉ depuis JsonlEstimationProvider — parcours disque conservé
│   ├── ChronosSettings.cs         # −6 champs
│   ├── FiveHourWindowInference.cs # conservé, orphelin, en réserve phase 19
│   ├── WeeklyWindow.cs            # conservé, orphelin, en réserve phase 19
│   ├── BudgetCalibration.cs       # SUPPRIMÉ
│   ├── BudgetAutoCalibrator.cs    # SUPPRIMÉ
│   ├── BudgetSource.cs            # SUPPRIMÉ
│   └── IBudgetPrompt.cs           # SUPPRIMÉ
├── ViewModels/
│   ├── MainViewModel.cs           # −_budgetPrompt, −CalibrateBudgets
│   └── BudgetDialogViewModel.cs   # SUPPRIMÉ
└── Views/
    ├── SettingsWindow.xaml        # −Button « Plafonds… »
    ├── BudgetPrompt.cs            # SUPPRIMÉ
    └── BudgetDialog.xaml(.cs)     # SUPPRIMÉ
```

### Pattern 1 — Écriture atomique temp + `File.Move` (à réutiliser tel quel)

**Quand :** tout fichier de `%APPDATA%\Chronos\`.
**Motif canonique du dépôt** (`SettingsService.cs:59-70`, `ArchiveStore.cs:62-71`, `TreatedStore`,
`App.RunSessionHook`) : `Directory.CreateDirectory(dir)` → `File.WriteAllText(path + ".tmp-" +
Environment.ProcessId)` → `File.Move(tmp, path, overwrite: true)`. Le suffixe `ProcessId` évite la collision
entre processus ; `File.Move` sur le même volume est atomique → aucun fichier partiel observable.

### Pattern 2 — Lecture tolérante qui ne lève jamais

`try { … } catch { return <neutre> }` avec un neutre explicite : `LastExactStore.Load()` → `null`,
`ArchiveStore.Load()` → ensemble vide, `SettingsService.Load()` → défauts. Toujours accompagné d'un test
« fichier corrompu → neutre sans exception ».

### Pattern 3 — Séparation I/O pur / logique pure

Le dépôt sépare systématiquement le calcul pur du provider qui fait l'I/O :
`FiveHourWindowInference` / `WeeklyWindow` / `WeeklyRecalibration` / `BudgetCalibration` (pur, testable en
`[Fact]` sans fichier) vs les providers (I/O). **`TranscriptActivityLog.Since()` doit suivre ce motif** : pur,
appelable N fois, testable sans fixture disque.

### Pattern 4 — Décorateur `IUsageProvider` plutôt qu'abonnement à un événement

`GatedOAuthUsageProvider` et `CompositeUsageProvider` sont déjà des décorateurs/compositeurs.
`LastExactUsageProvider` s'inscrit exactement dans ce motif. À l'inverse, `BudgetAutoCalibrator` s'abonnait à
`RefreshOrchestrator.SnapshotChanged` et exigeait une résolution forcée avant `StartAsync` — motif que cette
phase démolit. **Ne pas le reproduire.**

### Anti-patterns à éviter

- **Écrire un migrateur de `settings.json`** — §Q4 prouve qu'il est inutile.
- **Faire de la source de delta un `IUsageProvider`** — rouvre la porte à l'utilization dérivée des tokens.
- **Persister `FractionTimeRemaining`** — ressuscite un chiffre périmé au démarrage.
- **Modifier `CompositeUsageProvider.Best()`** — c'est la phase 19, explicitement hors périmètre.
- **Ajouter un paramètre au ctor positionnel de `ChronosPaths`** — casserait des dizaines de sites de test
  pour rien (propriété calculée à la place).
- **Supprimer `SourceReliability.Estimated`** parce qu'il n'est plus produit — la phase 19 en a besoin.
- **Supprimer `FiveHourWindowInference` / `WeeklyWindow`** parce qu'ils deviennent orphelins.

---

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser à la place | Pourquoi |
|---|---|---|---|
| Migration des champs obsolètes de `settings.json` | Un `SettingsMigrator` qui détecte et réécrit | **Le comportement par défaut de `System.Text.Json`** (`JsonUnmappedMemberHandling.Skip`) | Vérifié empiriquement §Q4 : déjà correct, y compris pour un enum supprimé et un type incohérent |
| Écriture sans corruption de `last-exact.json` | Un verrou, un journal, un double-fichier | **temp + `File.Move(overwrite:true)`** (`SettingsService`, `ArchiveStore`, `TreatedStore`) | Motif déjà éprouvé 4 fois dans le dépôt, testé (`Save_est_atomique_aucun_tmp_residuel`) |
| Parcours tolérant des transcripts JSONL | Réécrire la lecture, le filtrage, la détection `assistant` | **Le corps existant de `JsonlEstimationProvider`** : `FileShare.ReadWrite`, ligne partielle ignorée, filtre `mtime >= now-8j`, `IsAssistant` structuré (`type=="assistant"` ET `message.role=="assistant"`), `SumUsageTokens` (input+output+cache_creation+cache_read), rejet des timestamps futurs | Chaque garde correspond à un bug déjà rencontré et testé (faux positifs de prose « five_hour », dernière ligne tronquée, écriture concurrente de Claude Code, horloge décalée). Le CONTEXT l'exige explicitement : « Son parcours disque existant est bon et doit être conservé ». **Renommer le fichier, ne pas le réécrire.** |
| Fraction de temps restante | Recalculer à la main | **`WindowState.FractionRemaining(resetsAt, now, len)`** | Existe, clampe [0..1], gère `null` |
| Isolation des chemins en test | Un 3ᵉ paramètre de ctor + mise à jour de tous les tests | **Propriété calculée sur `ChronosPaths`** | `SettingsFile` fait déjà exactement ça, avec le raisonnement documenté dans son XML-doc |
| Sérialisation des dates | Epoch maison, format custom | **ISO 8601 `"o"` + `DateTimeOffset.TryParse(..., RoundtripKind)`** | Déjà le format de `WeeklyAnchor`, round-trip de l'offset vérifié §Q4 |

**Insight clé :** cette phase est à 90 % de la **soustraction**. Chaque fois qu'on est tenté d'écrire du code
neuf (migrateur, parseur, verrou), le dépôt contient déjà le motif — et l'écrire à neuf, c'est réintroduire
des bugs que 405 tests ont déjà éliminés.

---

## Runtime State Inventory

Phase de démolition / refactoring : un audit grep trouve les fichiers, pas l'état runtime. Les 5 catégories
sont renseignées explicitement.

| Catégorie | Éléments trouvés | Action requise |
|---|---|---|
| **Données stockées** | `%APPDATA%\Chronos\settings.json` **contient réellement** les 6 champs obsolètes sur la machine (vérifié : `FiveHourTokenBudget: 230000000`, `WeeklyTokenBudget: 5817635413`, `FiveHourBudgetSource: "Manual"`, `WeeklyBudgetSource: "Auto"`, + les 2 `CalibratedAt`) | **Aucune migration de données** — purge passive au premier `Save()` (vérifié §Q4). **Édition de code seulement** (retrait des 6 champs du record). |
| | `%APPDATA%\Chronos\usage.json` figé au 2026-07-10 (`resets_at: 9`, epoch 1970), toujours servi `Exact` | **Hors périmètre phase 16** — c'est EXA-02, phase 19. Ne pas le corriger ici, mais en tenir compte pour évaluer la non-régression d'affichage (§Q1). |
| | `%APPDATA%\Chronos\last-exact.json` — **fichier NOUVEAU**, n'existe encore nulle part | Créé à la première écriture ; `Load()` sur fichier absent → `null`, jamais de crash (test obligatoire) |
| | `%APPDATA%\Chronos\` contient aussi `archived.json`, `treated.json`, `oauth.dat`, `chronos.log`, `sessions/` | **Aucun** — non touchés par cette phase |
| **Config de service vivant** | `~/.claude/settings.json` (hooks Chronos + statusLine) | **Aucune** — la phase 15 vient de l'assainir ; la phase 16 ne touche ni aux hooks ni au pont |
| | Aucun service externe (n8n, Datadog, Cloudflare…) dans ce projet | **Aucune** — vérifié : le projet est une app desktop mono-utilisateur sans service externe |
| **État enregistré par l'OS** | Raccourci autostart dans `shell:startup` (`AutostartService`) — pointe un chemin d'exe, ne contient aucune référence aux plafonds | **Aucune** — la suppression de code ne le périme pas |
| | Aucune tâche planifiée, aucun service Windows, aucun pm2/launchd | **Aucune** — vérifié |
| **Secrets / variables d'env** | `%APPDATA%\Chronos\oauth.dat` (jetons OAuth chiffrés DPAPI, `ChronosOAuthStore`) | **Aucune** — aucun nom de clé lié aux plafonds ; non touché |
| | Aucune variable d'environnement, aucun `.env`, aucun secret CI | **Aucune** — vérifié |
| **Artefacts de build / paquets installés** | **`src/Chronos/obj/Debug/net8.0-windows/Views/BudgetDialog.g.cs`**, **`obj/Release/net8.0-windows/Views/BudgetDialog.g.cs`** et **`obj/Release/net8.0-windows/win-x64/Views/BudgetDialog.g.cs`** — 3 copies du code XAML généré, présentes MAINTENANT dans le dépôt de travail | **Nettoyage requis** après suppression de `BudgetDialog.xaml` : `dotnet clean` (ou suppression de `src/Chronos/obj/` et `bin/`) puis rebuild complet. Un build incrémental sur un `obj/` périmé est une source classique d'erreurs fantômes en WPF (`partial class` orpheline / type dupliqué). |
| | `Chronos-vX.Y.exe` publié | **Aucune** — pas de release en phase 16 |

**Question canonique :** une fois chaque fichier du dépôt à jour, que reste-t-il qui porte l'ancien état ?
→ **Deux choses seulement :** (1) le `settings.json` de l'utilisateur, purgé passivement et sans risque ;
(2) les `obj/` de build, à nettoyer par `dotnet clean`. Rien d'autre.

---

## Common Pitfalls

### Pitfall 1 — Le `settings.json` réel n'est PAS le `settings.json` par défaut
**Ce qui va mal :** on teste la migration avec `new ChronosSettings()` sérialisé, qui ne contient jamais les
champs obsolètes → le test DEL-06 ne prouve rien.
**Pourquoi :** le fichier de production a été écrit par une version antérieure de l'app, avec des valeurs que
le code actuel ne sait plus produire.
**Comment l'éviter :** figer une **fixture** `TestData/settings-legacy-plafonds.json` reproduisant le fichier
réel (6 obsolètes + 18 survivants), comme la phase 15 a figé `claude-settings-pollue.json`.
**Signe d'alerte :** un test DEL-06 qui ne lit aucun fichier de `TestData/`.

### Pitfall 2 — Supprimer `BudgetSource.cs` trop tôt
**Ce qui va mal :** la solution ne compile plus (`ChronosSettings` et `BudgetCalibration` le référencent).
**Comment l'éviter :** ordre imposé — `BudgetAutoCalibrator` → `BudgetCalibration` → 6 champs de
`ChronosSettings` → **puis** `BudgetSource.cs`.

### Pitfall 3 — `obj/` périmé après suppression d'un `.xaml`
**Ce qui va mal :** `BudgetDialog.g.cs` subsiste dans les 3 dossiers `obj/`, et un build incrémental peut
échouer sur une `partial class` sans sa moitié.
**Comment l'éviter :** `dotnet clean` (ou suppression de `src/Chronos/obj` et `src/Chronos/bin`) puis
`dotnet build` complet **dans la tâche qui supprime le XAML**, avant de lancer les tests.
**Signe d'alerte :** une erreur de compilation mentionnant `BudgetDialog` alors que plus aucun `.cs`/`.xaml`
ne le nomme. *(MEDIUM — le comportement exact de MSBuild sur un `obj/` orphelin dépend de l'état des listes
intermédiaires ; le nettoyage est de toute façon gratuit et lève le doute.)*

### Pitfall 4 — Le magasin qui ressuscite une fenêtre déjà remise à zéro
**Ce qui va mal :** au redémarrage, `LastExactStore` sert un « 80 % » dont le `resets_at` est passé depuis
deux jours. C'est un chiffre inventé — la violation exacte que le milestone traque.
**Comment l'éviter :** rejeter au chargement toute fenêtre dont `ResetsAt <= now`.
**Signe d'alerte :** un test de chargement qui ne fait pas varier `now` par rapport à `resets_at`.

### Pitfall 5 — Borne du delta : inclusive ou exclusive ?
**Ce qui va mal :** un message dont le timestamp vaut exactement l'instant de capture du relevé exact est
compté deux fois (une fois par le serveur, une fois par le delta).
**Comment l'éviter :** borne basse **strictement exclusive** (`e.Ts > since`), borne haute inclusive
(`e.Ts <= now`, déjà en place ligne 66 pour filtrer les horloges décalées). **Documenter le choix dans le
XML-doc et le prouver par un test de borne.**

### Pitfall 6 — Le delta JSONL est une borne INFÉRIEURE, pas une mesure
**Ce qui va mal :** on présente le delta comme la consommation réelle depuis T.
**Pourquoi :** `~/.claude/projects/**/*.jsonl` ne contient que les transcripts **Claude Code**. L'usage de
l'app bureau / Cowork consomme le **même pool de compte** (documenté dans CLAUDE.md) mais **n'apparaît pas**
dans ces fichiers. De plus, les limites Anthropic **pondèrent par modèle** : une somme de tokens n'est pas
convertible en pourcentage.
**Comment l'éviter :** le XML-doc de `TranscriptActivity` doit dire noir sur blanc que `Tokens` est une
**borne inférieure de l'activité Claude Code**, jamais une consommation de compte. La phase 19 (DEL-04) en
dépend pour marquer sa marge d'incertitude.

### Pitfall 7 — Croire que le « menu » est un `ContextMenu`
**Ce qui va mal :** on cherche `MenuItem "Calibrer les plafonds…"` dans `MainWindow.xaml` et on ne trouve
rien → on croit l'entrée déjà absente.
**Réalité vérifiée :** aucun `ContextMenu`/`MenuItem` dans `MainWindow.xaml`. L'entrée est un
`<Button Content="Plafonds…" Command="{Binding CalibrateBudgetsCommand}"/>` dans
`Views/SettingsWindow.xaml:306-307`, section « RÉGLAGES », `UniformGrid Columns="2"`.
**Effet de bord :** la grille passe de 4 à 3 boutons — vérifier le rendu.

### Pitfall 8 — Écrire `last-exact.json` déclenche le `FileSystemWatcher`
**Ce qui irait mal :** boucle de rafraîchissement infinie.
**Vérification faite :** le watcher est `new FileSystemWatcher(dir, "usage.json")` — filtre nominatif
(`RefreshOrchestrator.cs:77`). **Aucun risque**, mais toute future écriture dans ce dossier doit revérifier ce
point.

### Pitfall 9 — Le critère « 405 tests verts » lu littéralement
**Ce qui va mal :** la vérification de fin de phase échoue parce que le total est passé à 398 ou 412.
**Comment l'éviter :** le plan doit écrire explicitement le total cible et son calcul (§Q6). Le critère
opérationnel est **0 échec + aucune perte de couverture nette**, `ServicesLayerPurityTests` et
`CompositionRootTests` compris.

---

## Code Examples

### 1 — `ChronosPaths` : propriété calculée (motif existant à copier)

```csharp
// Source : src/Chronos/Services/ChronosPaths.cs:13-19 (motif SettingsFile, à répliquer)
/// <summary>
/// last-exact.json colocalisé avec usage.json (même dossier %APPDATA%\Chronos). Propriété
/// calculée : le ctor positionnel (UsageFile, ProjectsRoot) reste inchangé, donc les tests qui
/// construisent un répertoire temp obtiennent aussi un LastExactFile isolé — aucun accès au
/// vrai profil utilisateur.
/// </summary>
public string LastExactFile => Path.Combine(Path.GetDirectoryName(UsageFile)!, "last-exact.json");
```

### 2 — Écriture atomique (motif canonique du dépôt)

```csharp
// Source : src/Chronos/Services/SettingsService.cs:59-70 (identique dans ArchiveStore.cs:62-71)
var dir = Path.GetDirectoryName(_path)!;
Directory.CreateDirectory(dir);
var json = JsonSerializer.Serialize(payload, Options);
// Temp unique par process, sur le même volume que la cible → File.Move atomique.
var tmp = _path + $".tmp-{Environment.ProcessId}";
File.WriteAllText(tmp, json);
File.Move(tmp, _path, overwrite: true);
```

### 3 — Ce qui doit être CONSERVÉ à l'identique dans le parcours JSONL

```csharp
// Source : src/Chronos/Services/JsonlEstimationProvider.cs:46-70, 132-188
// FileShare.ReadWrite : Claude Code écrit le transcript en parallèle.
try { fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); }
catch (IOException) { continue; }
…
try
{
    using var doc = JsonDocument.Parse(line);   // ligne partielle/corrompue -> JsonException
    var o = doc.RootElement;
    if (!IsAssistant(o)) continue;              // type=="assistant" ET message.role=="assistant"
    if (!o.TryGetProperty("timestamp", out var ts)) continue;
    if (!DateTimeOffset.TryParse(ts.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var when)) continue;
    long tokens = SumUsageTokens(o);            // input+output+cache_creation+cache_read
    if (when <= now) entries.Add((when, tokens)); // filtre les timestamps futurs (horloge décalée)
}
catch (JsonException) { /* ligne invalide ignorée (ROB-02) */ }
…
var cutoff = now - TimeSpan.FromDays(8);        // fenêtre hebdo 7 j + marge → devient Horizon
```

### 4 — Options de désérialisation à NE PAS toucher (DEL-06)

```csharp
// Source : src/Chronos/Services/SettingsService.cs:25-32 — vérifié suffisant pour DEL-06.
// N'AJOUTER SURTOUT PAS JsonUnmappedMemberHandling.Disallow : ce serait l'inverse du besoin.
private static readonly JsonSerializerOptions Options = new()
{
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() },
};
```

### 5 — Câblage DI cible dans `App.ConfigureServices`

```csharp
// AVANT (App.xaml.cs:288-296) — le maillon final est le JSONL estimé :
services.AddSingleton<IUsageProvider>(sp => new CompositeUsageProvider(
    primary:  sp.GetRequiredService<ChronosOAuthUsageProvider>(),
    fallback: new CompositeUsageProvider(
        primary:  sp.GetRequiredService<GatedOAuthUsageProvider>(),
        fallback: new CompositeUsageProvider(
            primary:  sp.GetRequiredService<ClaudeUsageObjectProvider>(),
            fallback: sp.GetRequiredService<JsonlEstimationProvider>()))));

// APRÈS — le composite se termine sur le pont statusLine ; le décorateur de persistance coiffe le tout.
// La règle Best() de CompositeUsageProvider n'est PAS modifiée (phase 19).
services.AddSingleton(_ => new LastExactStore(/* ChronosPaths.LastExactFile */));
services.AddSingleton<ITranscriptActivitySource>(sp => new TranscriptActivityProvider(
    sp.GetRequiredService<ChronosPaths>(), sp.GetRequiredService<IClock>()));  // plus de SettingsService !
services.AddSingleton<IUsageProvider>(sp => new LastExactUsageProvider(
    inner: new CompositeUsageProvider(
        primary:  sp.GetRequiredService<ChronosOAuthUsageProvider>(),
        fallback: new CompositeUsageProvider(
            primary:  sp.GetRequiredService<GatedOAuthUsageProvider>(),
            fallback: sp.GetRequiredService<ClaudeUsageObjectProvider>())),
    store: sp.GetRequiredService<LastExactStore>(),
    clock: sp.GetRequiredService<IClock>()));
```

Noter la disparition de la dépendance à `SettingsService` dans le provider de transcripts : il ne lit plus
ni plafonds ni ancre hebdo. C'est un signal de conception fort — le contrat s'allège.

---

## State of the Art

| Ancienne approche | Approche cible | Quand ça change | Impact |
|---|---|---|---|
| `Utilization = tokens / plafond` (JSONL) | **Aucune utilization dérivée de tokens** | Phase 16 (structurellement impossible) → EXA-04 formalisé en phase 19 | Les limites Anthropic pondèrent par modèle : le calcul est faux même avec le bon plafond |
| Calibration manuelle + auto des plafonds | **Supprimée** | Phase 16 (DEL-05) | Cause racine des % faux après changement de forfait ; `ApplyAuto` gelait à vie une source `Manual` |
| Dernier relevé exact en **RAM** (`_cached`) | **Persisté sur disque** | Phase 16 (EXA-01) | Tue la bascule silencieuse à chaque redémarrage de l'exe — le cas le plus fréquent |
| Repli = estimation absolue | **Repli = dernier exact ± delta borné et marqué** | Briques en 16, doctrine en 19 | Jamais de pourcentage inventé |
| `Best()` par seule fiabilité | Par fraîcheur **et** fiabilité, avec limite d'âge | **Phase 19 — pas ici** | Ne pas anticiper |

**Déprécié / à ne plus écrire :** `BudgetCalibration`, `BudgetAutoCalibrator`, `BudgetSource`,
`IBudgetPrompt`/`BudgetPrompt`, `BudgetDialog`+VM, les 6 champs de plafonds, et toute production
d'`Utilization` à partir d'un comptage de tokens.

---

## Open Questions

1. **La `UniformGrid Columns="2"` de `SettingsWindow.xaml` avec 3 boutons au lieu de 4**
   - Ce qu'on sait : le bouton « Plafonds… » disparaît, la grille passe à 3 éléments (« Recalibrer hebdo… »,
     « Source terminal », « Diagnostic… ») sur 2 colonnes → une case vide en bas à droite.
   - Ce qui est flou : acceptable esthétiquement, ou faut-il passer à 3 boutons empilés / élargir l'un d'eux ?
   - Recommandation : laisser la case vide (`Columns="2"` avec 3 enfants reste propre) et **faire valider
     visuellement à la vérification de phase**. Ne pas surinvestir : la phase 20 retouche l'UI.

2. **Le libellé de conseil du diagnostic (`DiagnosticService.cs:397`)**
   - Ce qu'on sait : « (Repli : « Calibrer les plafonds… » colore une estimation quand aucune source exacte
     n'est là.) » devient faux et doit partir.
   - Ce qui est flou : par quoi le remplacer sans anticiper EXA-06 (phase 20) ?
   - Recommandation : le **supprimer purement**, ainsi que les 2 lignes de plafonds (283-284), et reformuler
     471 en `"estimé — % inconnu"`. La refonte du diagnostic est la phase 20 ; ici on retire, on n'invente pas.

3. **Le décorateur doit-il reboucher aussi une fenêtre `Estimated` (et pas seulement `Unavailable`) ?**
   - Ce qu'on sait : après la phase 16, plus aucune source ne produit `Estimated` → la question est vide **en
     phase 16**.
   - Ce qui est flou : la phase 19 réintroduira un marqueur pour « exact + delta ».
   - Recommandation : en phase 16, **ne reboucher que `Unavailable`**. Règle la plus étroite possible, donc la
     plus sûre ; la phase 19 l'élargira en connaissance de cause.

4. **Nommage `LastExactUsageProvider` vs un nom plus explicite**
   - Ce qu'on sait : le dépôt nomme ses décorateurs `GatedOAuthUsageProvider`, `CompositeUsageProvider`.
   - Recommandation : `LastExactUsageProvider` (cohérent avec `LastExactStore`). Choix cosmétique, à la
     discrétion du planner.

---

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|---|---|---|---|---|
| .NET SDK | build / test / publish | ✓ | **10.0.201** (compile la cible `net8.0-windows`) | — |
| `net8.0-windows` target pack | `Chronos.csproj` + `Chronos.Tests.csproj` | ✓ | restauré, build OK | — |
| xunit + runner + StaFact | tests | ✓ | 2.9.2 / 2.8.2 / 1.1.11 | — |
| `System.Text.Json` | persistance + parsing | ✓ | intégré à net8.0 | — |
| `%APPDATA%\Chronos\` | magasin EXA-01 | ✓ | contient déjà `settings.json`, `usage.json`, `archived.json`, `treated.json`, `oauth.dat`, `sessions/` | — |
| `~/.claude/projects` | source de delta | ✓ (chemin standard, lecture tolérante si absent) | — | dossier absent → journal vide, aucune exception |
| Fixtures JSONL de test | tests DEL-01/02 | ✓ | `sample-valid.jsonl`, `sample-inactive.jsonl`, `sample-tolerant.jsonl`, `SubagentsRoot/` | — |
| Réseau / API Anthropic | — | non requis | — | aucune phase 16 n'appelle le réseau |

**Suite de tests exécutée pour établir la baseline :**
`dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` → **405 réussis / 0 échec / 35 s**. 2 warnings xUnit2031
préexistants dans `DesktopUiaSessionSourceTests.cs` (lignes 345, 364) — non bloquants, hors périmètre.

**Dépendances manquantes sans repli :** aucune.
**Dépendances manquantes avec repli :** aucune.

---

## Validation Architecture

### Test Framework

| Propriété | Valeur |
|---|---|
| Framework | xunit 2.9.2 + xunit.runner.visualstudio 2.8.2 + Xunit.StaFact 1.1.11 |
| Fichier de config | aucun fichier dédié — configuration dans `tests/Chronos.Tests/Chronos.Tests.csproj` (`IsTestProject`, `UseWPF`, `net8.0-windows`) |
| Commande rapide | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~<Classe>"` |
| Commande suite complète | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` (**35 s** mesurées) |

### Phase Requirements → Test Map

| Req | Comportement | Type | Commande automatisée | Fichier existe ? |
|---|---|---|---|---|
| **EXA-01** | `Save` puis `Load` restitue les fenêtres exactes avec leur `captured_at` | unit | `dotnet test … --filter "FullyQualifiedName~LastExactStoreTests"` | ❌ Wave 0 |
| **EXA-01** | Fichier absent / corrompu → `null`, aucune exception | unit | idem | ❌ Wave 0 |
| **EXA-01** | Écriture atomique : aucun `.tmp-*` résiduel | unit | idem | ❌ Wave 0 |
| **EXA-01** | `FractionTimeRemaining` **recalculé** au chargement, jamais relu du disque | unit | idem | ❌ Wave 0 |
| **EXA-01** | Fenêtre dont `resets_at <= now` **rejetée** au chargement | unit | idem | ❌ Wave 0 |
| **EXA-01** | Décorateur : snapshot `Exact` → écrit ; fenêtre `Unavailable` → rebouchée depuis le magasin ; fenêtre vivante **jamais** écrasée | unit | `--filter "FullyQualifiedName~LastExactUsageProviderTests"` | ❌ Wave 0 |
| **DEL-01** | Aucun message depuis T → `HasActivity == false`, `LastActivityAt == null` | unit | `--filter "FullyQualifiedName~TranscriptActivityProviderTests"` | ❌ Wave 0 (renommage de `JsonlEstimationProviderTests`) |
| **DEL-01** | Aucun `UsageSnapshot`, aucune `Utilization` produits par le type | unit | idem (assertion de type / absence de `IUsageProvider`) | ❌ Wave 0 |
| **DEL-02** | Somme bornée `]since ; now]` exacte sur `sample-valid.jsonl` (1550 / 2150) | unit | idem | ❌ Wave 0 |
| **DEL-02** | Borne basse **exclusive** (`Since(11:30)` → 0 token) | unit | idem | ❌ Wave 0 |
| **DEL-02** | Tolérance ROB-02 conservée (corrompue / partielle / prose / `user` ignorées) | unit | idem | ⚠ à adapter |
| **DEL-02** | `subagents/` inclus (800 tokens) | unit | idem | ⚠ à adapter |
| **DEL-02** | `Covers(now - 30 j) == false` (horizon 8 j) | unit | idem | ❌ Wave 0 |
| **DEL-02** | `Since()` pur testé sans fichier | unit | `--filter "FullyQualifiedName~TranscriptActivityLogTests"` | ❌ Wave 0 |
| **DEL-05** | Le graphe DI se résout **sans** `IBudgetPrompt` ni `BudgetAutoCalibrator`, **avec** le décorateur | integration | `--filter "FullyQualifiedName~CompositionRootTests"` | ✅ à réécrire |
| **DEL-05** | Aucun type `Budget*` dans l'assembly (garde de non-retour) | unit | `--filter "FullyQualifiedName~ServicesLayerPurityTests"` (ou une nouvelle assertion réflexive) | ✅ / ❌ assertion à ajouter |
| **DEL-06** | Fixture `settings-legacy-plafonds.json` → aucune exception + les 18 préférences intactes | unit | `--filter "FullyQualifiedName~SettingsServiceTests"` | ✅ à étendre + fixture ❌ Wave 0 |
| **Garde** | Aucun type WPF dans `Services/`/`Models/` (nouveaux types compris) | unit | `--filter "FullyQualifiedName~ServicesLayerPurityTests"` | ✅ |
| **Garde** | Suite complète verte, 0 échec | smoke | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` | ✅ |

### Sampling Rate

- **Par commit de tâche :** `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` — la suite complète tient
  en **35 s**, il n'y a donc aucune raison de sous-échantillonner. Sur une tâche de démolition, un filtre
  masquerait précisément le test cassé ailleurs qu'on cherche à détecter.
- **Par fusion de vague :** suite complète + `dotnet clean && dotnet build` (Pitfall 3 : `obj/` périmé).
- **Portillon de phase :** suite complète verte, **0 échec**, `ServicesLayerPurityTests` et
  `CompositionRootTests` compris, avant `/gsd:verify-work`.

### Wave 0 Gaps

- [ ] `tests/Chronos.Tests/LastExactStoreTests.cs` — couvre EXA-01 (round-trip, tolérance, atomicité, rejet
      des fenêtres rollées, non-persistance de `FractionTimeRemaining`)
- [ ] `tests/Chronos.Tests/LastExactUsageProviderTests.cs` — couvre EXA-01 (écriture + rebouchage, avec un
      `FakeProvider` sur le modèle de `CompositeUsageProviderTests`)
- [ ] `tests/Chronos.Tests/TranscriptActivityProviderTests.cs` — remplace `JsonlEstimationProviderTests.cs`,
      couvre DEL-01 / DEL-02
- [ ] `tests/Chronos.Tests/TranscriptActivityLogTests.cs` — requêtes pures `Since()` / `Covers()` sans disque
- [ ] `tests/Chronos.Tests/TestData/settings-legacy-plafonds.json` — **fixture DEL-06** figeant le
      `settings.json` réel de production (6 champs obsolètes + 18 survivants)
- [ ] Assertion de non-retour DEL-05 : aucun type nommé `Budget*` dans l'assembly `Chronos`
- [ ] Aucune installation de framework requise (xunit déjà en place, 405 tests verts)

---

## Sources

### Primaires (HIGH confidence)
- **Base de code du dépôt** (lecture directe, 2026-09-09) : `App.xaml.cs`, `MainViewModel.cs`,
  `JsonlEstimationProvider.cs`, `CompositeUsageProvider.cs`, `ClaudeUsageObjectProvider.cs`,
  `ChronosOAuthUsageProvider.cs`, `SettingsService.cs`, `ChronosSettings.cs`, `ChronosPaths.cs`,
  `ArchiveStore.cs`, `TreatedStore.cs`, `RefreshOrchestrator.cs`, `WeeklyRecalibration.cs`,
  `FiveHourWindowInference.cs`, `WeeklyWindow.cs`, `WindowState.cs`, `UsageSnapshot.cs`,
  `WindowGaugeViewModel.cs`, `DiagnosticService.cs`, `SettingsWindow.xaml`, `BudgetCalibration.cs`,
  `BudgetAutoCalibrator.cs`, `BudgetSource.cs`, `IBudgetPrompt.cs`
- **Suite de tests exécutée** : `dotnet test` → 405/405 verts, 35 s (baseline confirmée, pas supposée)
- **Grep exhaustif** des 8 symboles de plafonds sur `src/` + `tests/` → inventaire §Q5 complet
- **Vérification empirique DEL-06** : projet console `net8.0` jetable, record amputé + options identiques à
  `SettingsService`, désérialisation du **vrai** `%APPDATA%\Chronos\settings.json` + 3 cas dégradés → §Q4
- **État runtime réel de la machine** : contenu de `%APPDATA%\Chronos\settings.json`, `usage.json`,
  listing du dossier, présence des 3 `obj/**/BudgetDialog.g.cs`
- `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md`, `.planning/STATE.md`, `16-CONTEXT.md`,
  `.planning/config.json`, `CLAUDE.md`

### Secondaires (MEDIUM confidence)
- Comportement par défaut de `System.Text.Json` sur les membres non mappés (`JsonUnmappedMemberHandling.Skip`)
  — **connaissance documentaire confirmée par l'expérience directe sur le runtime .NET installé**, ce qui la
  hisse de fait au niveau HIGH pour ce dépôt précis
- Piège du `obj/` WPF périmé après suppression d'un `.xaml` (Pitfall 3) — raisonnement sur le fonctionnement
  de `MarkupCompilePass`, mitigation gratuite (`dotnet clean`)

### Tertiaires (LOW confidence)
- Aucune. Aucune recherche web n'a été nécessaire : la phase ne touche à aucune API externe, aucune
  bibliothèque tierce, aucune version à vérifier.

---

## Metadata

**Confidence breakdown:**

| Zone | Niveau | Raison |
|---|---|---|
| Standard stack | **HIGH** | Aucun ajout : tout est déjà dans `net8.0` et dans les `.csproj` du dépôt, build et tests vérifiés |
| Inventaire de démolition (Q5) | **HIGH** | Grep exhaustif sur `src/` + `tests/`, 14 points d'accroche localisés à la ligne près |
| Migration des réglages (Q4) | **HIGH** | **Vérifié empiriquement** sur le vrai fichier de production + 3 cas dégradés, pas déduit |
| Impact sur les tests (Q6) | **HIGH** | Baseline 405 exécutée ; chaque fichier impacté ouvert et lu ; `CompositeUsageProviderTests` et `DiagnosticServiceTests` vérifiés intacts |
| Sort du provider JSONL (Q1) | **HIGH** sur les faits (état réel de `usage.json`, règle `Best()`, `WeeklyRecalibration`), **MEDIUM** sur le jugement « la perte de la couleur hebdo n'est pas une régression » — c'est une lecture de la Core Value, à confirmer par l'utilisateur à la vérification |
| Contrat de delta (Q2) | **MEDIUM-HIGH** | Le besoin de bornage par fenêtre est démontré ; la forme exacte (`ReadAsync` + `Since` pur) est une recommandation de conception argumentée, pas un fait |
| Schéma du magasin (Q3) | **MEDIUM-HIGH** | Chaque champ persisté/non persisté est justifié par la Core Value ou par un besoin identifié des phases 19/20 ; le nommage et la version de schéma restent des choix |
| Piège `obj/` (Pitfall 3) | **MEDIUM** | Comportement MSBuild non reproduit en laboratoire ; mitigation gratuite et sans risque |

**Research date:** 2026-09-09
**Valid until:** 2026-10-09 (30 j — base de code interne, aucune dépendance externe volatile). À réviser
immédiatement si `CompositeUsageProvider`, `ChronosSettings` ou `App.ConfigureServices` sont modifiés hors
phase 16.
