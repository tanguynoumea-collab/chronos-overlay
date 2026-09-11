# Phase 18 : Source exacte par en-têtes de rate-limit — Research

**Researched:** 2026-09-12
**Domain:** HTTP `HttpClient` / lecture d'en-têtes non standard sur réponse d'erreur · normalisation d'unités · insertion d'un `IUsageProvider` dans une chaîne composite figée
**Confidence:** MEDIUM-HIGH (mécanique .NET et placement DI : HIGH, vérifiés empiriquement ; noms et présence des en-têtes `anthropic-ratelimit-unified-*` : MEDIUM, non documentés officiellement)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

Aucune décision utilisateur verrouillée : la discussion est désactivée (`workflow.skip_discuss=true`).

### Claude's Discretion

> Tous les choix d'implémentation sont à la discrétion de Claude — la discussion est désactivée
> (`workflow.skip_discuss=true`).

### Technique reprise de claude-session-browser (vérité-terrain, ne pas ré-enquêter — copié verbatim)

> Source : `github.com/juppeee/claude-session-browser`, `clawdmeter.py:150` (`poll_usage_meta`).
>
> Requête : `POST https://api.anthropic.com/v1/messages`
> En-têtes de requête : `Authorization: Bearer <access token>`, `anthropic-beta: oauth-2025-04-20`,
> `anthropic-version: 2023-06-01`, `User-Agent: claude-code/<version>`, `Content-Type: application/json`.
> Corps : modèle Haiku le moins cher, `max_tokens` = 1, un seul message utilisateur trivial.
>
> En-têtes de RÉPONSE exploités :
> - `anthropic-ratelimit-unified-5h-utilization` et `-7d-utilization` — **utilization en 0..1**
> - `anthropic-ratelimit-unified-5h-reset` et `-7d-reset` — **epoch SECONDES**
> - `anthropic-ratelimit-unified-5h-status` — `allowed` / `allowed_warning` / `rejected`
> - variante `anthropic-ratelimit-unified-overage-utilization` / `-overage-reset` / `-status` — dépassement
>
> **Point décisif (HDR-02)** : le code d'origine récupère les en-têtes MÊME sur une erreur HTTP
> (`except HTTPError: hdrs = e.headers`) — un 429 porte donc quand même les chiffres. C'est l'avantage
> structurel sur `/api/oauth/usage`, qui ne renvoie rien d'utile en erreur.
> Un 401/403 en revanche ne porte pas d'en-têtes exploitables : dégrader.

### PIÈGE D'UNITÉS (HDR-05) — trois unités pour la même donnée (copié verbatim)

| Source | Champ | Unité |
|---|---|---|
| En-têtes `anthropic-ratelimit-unified-*` | `utilization` | **0..1** |
| `GET /api/oauth/usage` | `utilization` | **0..100** |
| Pont statusLine (`rate_limits`) | `used_percentage` | **0..100** |

> `resets_at` : **epoch secondes** pour les en-têtes et le pont statusLine, **ISO 8601** pour
> `/api/oauth/usage`. La normalisation doit se faire en **un point unique**.

### SÉCURITÉ — contraintes absolues (copiées verbatim)

> - **Ne JAMAIS appeler l'endpoint de refresh avec le refresh token RÉEL** de `%APPDATA%\Chronos\oauth.dat` :
>   le flux OAuth fait tourner le refresh token, un appel non re-sauvegardé invaliderait définitivement le
>   login de l'utilisateur. Contrôle avant/après : `oauth.dat` doit rester **518 octets, mtime 1783863147**.
> - **Ne JAMAIS émettre de vraie requête réseau depuis un test.** Tout par `FakeHttpMessageHandler`.
>   Note : le jeton de l'utilisateur est de toute façon expiré depuis le 2026-07-12, donc une sonde réelle
>   échouerait — mais l'interdiction tient indépendamment de cela.
> - Le jeton ne vit qu'en variable locale, le temps de construire l'en-tête `Authorization`. Jamais logué,
>   écrit, mis en exception ni concaténé dans une URL.
> - Le corps de la réponse HTTP ne doit **jamais** être transporté ni journalisé.

### Contraintes projet (copiées verbatim)

> - MVVM strict, DI, dossiers Models/Views/ViewModels/Services.
> - Aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`).
> - Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin, aucune dépendance native.
> - Dégradation totale : réseau, timeout, 401/403, en-têtes absents ou illisibles → jamais de crash,
>   jamais de valeur inventée.
> - UI et commentaires en **français**.
> - **Baseline à l'entrée de phase : 505 tests xUnit verts.**

### Coût de la sonde (HDR-06) — à assumer et à annoncer (copié verbatim)

> Chaque appel consomme une **vraie micro-requête** sur le compte (`max_tokens` = 1 sur le modèle le moins
> cher, une dizaine de jetons d'entrée). La cadence doit être bornée et le coût indiqué honnêtement dans les
> réglages. Ordre de grandeur de référence chez l'auteur d'origine : un sondage toutes les 300 s, avec repli
> à 60 s après échec.

### Deferred Ideas (OUT OF SCOPE)

> - Trou visuel dans la `UniformGrid` des réglages (3 boutons, `Columns="2"`, cellule bas-droite vide) hérité
>   de la phase 16 — **reporté à la phase 20**.
> - Préavis avant saturation (~90 %) et notification au reset — hors périmètre v1.5 (Future Requirements).

**Hors périmètre (rappel de la Phase Boundary) :** la refonte de la doctrine du composite (phase 19) et la
distinction visuelle frais / daté / indisponible du cadran (phase 20). Ici on **AJOUTE** un `IUsageProvider`
en tête de chaîne ; on ne change pas encore la règle de choix de source.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description (REQUIREMENTS.md) | Research Support |
|----|-------------------------------|------------------|
| HDR-01 | Usage exact via les en-têtes `anthropic-ratelimit-unified-*` d'une **requête jetable** (`POST /v1/messages`, `max_tokens:1`, modèle le moins cher) | Pattern 1 (sonde) + Q2 (modèle exact vérifié : `claude-haiku-4-5` / `claude-haiku-4-5-20251001`, Active) + en-tête `anthropic-beta` **prouvé obligatoire** (sonde empirique) |
| HDR-02 | En-têtes exploités **même quand la réponse est un 429** | Q3 (vérifié empiriquement : `SendAsync` ne lève pas sur 429, en-têtes intacts dans `resp.Headers`) + Pattern 2 (aiguillage par code HTTP **sans** `EnsureSuccessStatusCode`) ; présence réelle des en-têtes sur 429 : MEDIUM, cf. Open Question 1 |
| HDR-03 | **Statut serveur** (`allowed` / `allowed_warning` / `rejected`) remonté au cadran | Pattern 3 (`StatutServeur?` PAR FENÊTRE sur `WindowState` — seul emplacement qui survive au composite non modifiable) |
| HDR-04 | Usage en **dépassement** (`overage`) lu et affiché quand présent | Pattern 3 + Pattern 4 (canal latéral, motif `IAuthStatus`) — le dépassement est **de compte**, pas une 3ᵉ fenêtre : cf. Q4 |
| HDR-05 | Unités normalisées en **un point unique** | Pattern 5 (`UsageNormalization` pur) + inventaire des **5** conversions existantes + garde mécanique par balayage de source |
| HDR-06 | Cadence **bornée** + coût annoncé dans les réglages | Pattern 6 (auto-throttle `_prochainAppelAutorise`, motif `ChronosOAuthUsageProvider`) + chiffrage du coût réel + surface `SettingsWindow.xaml` section DONNÉES |
</phase_requirements>

---

## Project Constraints (from CLAUDE.md)

Directives actionnables extraites, à vérifier par le planner :

| Directive | Impact sur cette phase |
|---|---|
| C# / .NET 8 (`net8.0-windows`) / WPF / MVVM CommunityToolkit + `Microsoft.Extensions.DependencyInjection` + `Hosting` — **imposé** | Aucun paquet à ajouter : `System.Net.Http` et `System.Globalization` sont dans le framework |
| **Aucune dépendance native** | Exclut tout client HTTP tiers ; `HttpClient` seul |
| Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin | La sonde n'écrit rien ; la persistance passe par `LastExactStore` déjà en place |
| `utilization`/`resets_at` prioritaires sur le comptage de jetons ; **ne jamais présenter une estimation comme exacte** | Les en-têtes sont une mesure serveur → `SourceReliability.Exact` légitime ; en-tête absent/illisible → `null`, **jamais 0** |
| Robustesse : aucune source disponible ≠ crash ; parsing tolérant | Chaque en-tête se lit indépendamment ; une valeur illisible n'invalide pas les autres |
| MVVM strict, `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers Models/Views/ViewModels/Services | `StatutServeur` / `EtatDepassement` → `Models/` ; sonde + normalisation → `Services/` ; exposition → `ViewModels/` |
| UI et commentaires en **français** | Vocabulaire de domaine en français (`StatutServeur`, `EtatDepassement`), types techniques en anglais (motif `ChronosTokenAuthority`) |
| Activer frontend-design + windows-wpf sur les tâches UI | Ne concerne que le libellé de coût dans `SettingsWindow.xaml` (touche minimale) |
| `InvariantGlobalization=false` (confirmé dans `Chronos.csproj`) | **Le piège de culture fr-FR est RÉEL en production** — cf. Pitfall 1, vérifié empiriquement |
| `Assembly.Location` interdit (mono-fichier) → `AppContext.BaseDirectory` | Concerne la garde de source de HDR-05 : préférer une injection MSBuild plutôt qu'une remontée de dossiers |
| GSD Workflow Enforcement : pas d'édition hors commande GSD | Cette recherche n'a modifié aucun fichier du dépôt |

---

## Summary

Cette phase n'ajoute **aucune** dépendance et n'invente **aucune** mécanique : elle fabrique un troisième
provider HTTP sur le patron exact de `ChronosOAuthUsageProvider` (auto-throttle, rejeu unique sur 401, recul,
cache, tolérance totale, consommateur de `ChronosTokenAuthority`) mais qui lit des **en-têtes de réponse** au
lieu d'un corps JSON. Toute la difficulté est ailleurs : dans trois contraintes mécaniques du code existant,
et dans un piège de culture qui ne se voit pas au compilateur.

**Contrainte mécanique n°1 (décisive pour HDR-03/HDR-04).** `CompositeUsageProvider.GetAsync` reconstruit le
snapshot par `new UsageSnapshot { FiveHour, SevenDay, SourceCapturedAt }` — pas par `with`. **Tout nouveau
champ posé sur `UsageSnapshot` est donc silencieusement DÉTRUIT** au passage du composite, et l'interdiction
de modifier `CompositeUsageProvider.cs` rend ce chemin structurellement impossible en phase 18. En revanche
`Best()` retourne l'instance de `WindowState` **par référence** : tout champ optionnel posé sur `WindowState`
voyage gratuitement, sans toucher une ligne du composite. Le statut serveur (per-fenêtre) va donc sur
`WindowState` ; le dépassement (de compte, et présent dans une forme de réponse *alternative* où les fenêtres
5 h/7 j sont absentes) réclame en plus un canal latéral sur le motif déjà établi par `IAuthStatus`.

**Contrainte mécanique n°2 (décisive pour le placement, HDR-01/HDR-02).** `Best()` privilégie le `primary`
à fiabilité ÉGALE. Si la sonde est en `fallback`, son snapshot — le seul porteur du statut serveur et du
dépassement — est écarté à chaque tick où `/api/oauth/usage` répond. La sonde doit donc être le `primary`
d'un **nouveau** composite externe, instancié dans `App.xaml.cs` (instancier n'est pas modifier le fichier
interdit). Le composite appelant **inconditionnellement les deux** `GetAsync`, le placement n'a aucune
incidence sur le coût : c'est l'auto-throttle du provider, et lui seul, qui borne la cadence (HDR-06).

**Contrainte mécanique n°3 (le piège qui ne se compile pas en erreur).** Vérifié aujourd'hui sur la machine
réelle sous `fr-FR` : `double.Parse("0.63")` **lève une `FormatException`** et `double.TryParse("0.63", out v)`
**renvoie `false`**. Seul `NumberStyles.Float` + `CultureInfo.InvariantCulture` rend `0,63`. Une lecture
d'en-tête écrite « naturellement » ne plante donc pas : elle rend `null` en silence, et la source paraît
simplement muette sur une machine française — exactement la panne silencieuse que le milestone v1.5 éradique.
C'est la première chose à graver dans un test.

**Primary recommendation :** `RateLimitHeaderUsageProvider` (patron `ChronosOAuthUsageProvider`, consommateur
de `ChronosTokenAuthority`, auto-throttle 300 s), inséré comme `primary` d'un nouveau `CompositeUsageProvider`
externe sous `LastExactUsageProvider` ; lecture via `resp.Headers.TryGetValues` **sans jamais**
`EnsureSuccessStatusCode()` ; toutes les conversions d'unité passées par un `UsageNormalization` pur et
gardé par un test de balayage de source ; `StatutServeur?` en champ optionnel de `WindowState` ; aucun bump
de `LastExactStore.SchemaVersion` (sinon 3 fixtures de test cassent pour rien).

---

## Réponses aux 8 questions ouvertes

| # | Question | Réponse | Confiance | Détail |
|---|---|---|---|---|
| 1 | Les en-têtes reviennent-ils sur un 429, et sous quels noms ? | **401 : NON, aucun en-tête `anthropic-ratelimit-*`** (vérifié empiriquement aujourd'hui, 4 variantes, 2 endpoints). **429 : non vérifiable sans jeton valide** ; 3 sources concordantes l'affirment. Noms confirmés par le code source d'origine. **La documentation publique Anthropic ne mentionne PAS la famille `unified`** — elle ne documente que `anthropic-ratelimit-{requests,tokens,input-tokens,output-tokens}-*` (reset en **RFC 3339**, pas epoch). | 401 : HIGH · 429 : MEDIUM · noms : MEDIUM | Pattern 2, Open Question 1 |
| 2 | Quel modèle, quel coût ? | `claude-haiku-4-5-20251001` est **Active** (retrait « pas avant le 2026-10-15 » — le plancher le plus proche de toute la gamme) ; l'**alias `claude-haiku-4-5` est documenté** → préférer l'alias. Haiku 4.5 est bien le **moins cher** : 1 $/MTok entrée, 5 $/MTok sortie. Coût ≈ **0,000015 $ par sonde**, ≈ 0,004 $/jour à 300 s. | HIGH | Q2 détaillée, Pitfall 6 |
| 3 | Comment `HttpClient` expose-t-il les en-têtes d'une erreur ? | Vérifié empiriquement : `SendAsync` **ne lève pas** sur 429 ; les en-têtes non standard atterrissent dans **`resp.Headers`** (PAS `resp.Content.Headers`), recherche **insensible à la casse** ; `HttpRequestException` **n'a aucune propriété `Headers`** → `EnsureSuccessStatusCode()` ne doit jamais être le chemin de contrôle (il ne détruit rien, mais l'exception ne porte pas l'information) ; `resp.Headers.RetryAfter` est typé et peuplé. | HIGH | Q3 détaillée, Code Example 1 |
| 4 | Où porter statut serveur et dépassement ? | **Pas sur `UsageSnapshot`** : le composite reconstruit le record sans `with` → champ détruit, et le fichier est interdit. → **`StatutServeur?` optionnel sur `WindowState`** (voyage par référence via `Best()`). Le **dépassement n'est PAS une 3ᵉ `WindowKind`** (`UsageSnapshot` n'a que deux emplacements requis, et la forme overage est *alternative* aux fenêtres 5 h/7 j) → `EtatDepassement?` sur `WindowState` **+ canal latéral** `IEtatServeur` (motif `IAuthStatus`) pour le cas où seules les en-têtes overage existent. | HIGH (mécanique) | Pattern 3, Pattern 4 |
| 5 | Place exacte dans la chaîne ? | **`primary` d'un nouveau composite externe**, sous `LastExactUsageProvider`, donc **avant** `ChronosOAuthUsageProvider`. Argument décisif : `Best()` privilégie le `primary` à fiabilité égale — en `fallback`, le statut serveur et le dépassement seraient jetés à chaque tick où l'endpoint OAuth répond. `/api/oauth/usage` reste moins coûteux (GET, zéro quota) mais ne répond **rien** en 429 ; la sonde, elle, répond. Le coût n'est pas gouverné par la position (le composite appelle les deux) mais par l'auto-throttle. | HIGH | Pattern 7 |
| 6 | Cadence et coût ? | **Constante 300 s** dans le provider (pas de réglage d'intervalle : `RefreshIntervalSeconds` est déjà « persisté sans UI » par décision verrouillée) + **interrupteur on/off** dédié dans les réglages avec le coût écrit noir sur blanc. Articulation avec le tick 60 s : **aucun couplage** — le provider refuse lui-même par `_prochainAppelAutorise`, exactement comme `ChronosOAuthUsageProvider` (`MinInterval`). Recul : `retry-after` si présent, sinon 60 s après un 429 porteur d'en-têtes (une sonde rejetée ne consomme pas de quota), exponentiel 60 s → 15 min sur panne réseau. | HIGH (motif existant) | Pattern 6 |
| 7 | Normalisation en un point unique ? | Nouveau `Services/UsageNormalization.cs` **pur, statique, sans I/O** ; **les 5 conversions existantes** y sont rapatriées (3 providers + 2 dans `DiagnosticService`) ; garde mécanique = **test de balayage de source** (le seul qui fonctionne : la réflexion ne voit pas un `/ 100.0`), avec le chemin des sources injecté par MSBuild (`AssemblyMetadata`) et non deviné depuis `AppContext.BaseDirectory`. | HIGH | Pattern 5, Code Example 3 |
| 8 | Ce qui casse dans les 505 tests ? | **Rien ne casse** si l'on suit les recommandations : champs `init` nullable sur `WindowState` → 21 sites `new WindowState` des tests compilent ; **ne pas bumper `LastExactStore.SchemaVersion`** (3 fixtures épinglent `"version":1` / `999`) ; **ne pas changer la signature de `DiagnosticService`** (10 sites de construction) ou alors paramètre optionnel **en dernier**. Deux gardes à **ÉTENDRE** (pas réécrire) : `CompositionRootTests` et `ServicesLayerPurityTests`. Aucun test à supprimer. | HIGH (baseline rejouée aujourd'hui : 505/505 en 54 s) | Q8 détaillée |

---

## Vérifications empiriques faites le 2026-09-12

Toutes sur la machine réelle. **Aucune requête avec un jeton réel de l'utilisateur** ; jetons manifestement
bidons uniquement. `oauth.dat` contrôlé **avant et après** : **518 octets, mtime 1783863147** — inchangé.

| Sonde | Résultat | Ce que ça tranche |
|---|---|---|
| `POST /v1/messages`, `Bearer sk-ant-INVALID-PROBE`, `anthropic-version`, corps nominal | **401** · `{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}` · `request-id: req_…` · `x-should-retry: false` · **aucun en-tête `anthropic-ratelimit-*`** | Sans `anthropic-beta`, un Bearer est interprété comme une **clé d'API** |
| Idem **+ `anthropic-beta: oauth-2025-04-20`** + `User-Agent: claude-code/2.1.30` | **401** · `{"…","message":"OAuth access token is invalid."}` · `request_id: **null**` · pas de `x-should-retry` · **aucun en-tête `anthropic-ratelimit-*`** | L'en-tête `anthropic-beta` est **obligatoire** : c'est lui qui bascule le serveur sur le chemin d'identifiant OAuth. Le 401 OAuth a une **forme distincte** (corps 117 octets, `request_id: null`) |
| `POST /v1/messages` **sans aucune authentification**, corps nominal | **401** `x-api-key header is required` | L'authentification est évaluée **AVANT** le corps |
| `POST /v1/messages` sans authentification, **corps malformé** `{"nope":1}` | **401** (pas 400) | Confirme la précédence : un identifiant de modèle périmé ne peut **pas** se manifester tant que l'authentification échoue |
| `POST /v1/messages` **sans `anthropic-version`** + beta + jeton bidon | **401** même forme | Non concluant sur l'obligation de `anthropic-version` (la doc l'exige ; l'envoyer) |
| `GET /api/oauth/usage` + jeton bidon + beta | **401** identique, `request_id: null`, **aucun en-tête `anthropic-ratelimit-*`** | Le refus OAuth est rendu **en bordure** (Cloudflare, `request_id: null`) : aucune information de quota n'atteint jamais le client. **Impossible** de savoir sans jeton valide si `/api/oauth/usage` porte AUSSI les en-têtes `unified` — s'il les portait, la sonde deviendrait gratuite (cf. Open Question 3) |
| `.NET` : `double.Parse("0.63")` sous `fr-FR` | **`FormatException`** | cf. Pitfall 1 |
| `.NET` : `double.TryParse("0.63", out v)` sous `fr-FR` | **`false`** (perte **silencieuse**) | cf. Pitfall 1 |
| `.NET` : `double.TryParse("0.63", NumberStyles.Float, InvariantCulture, out v)` | `0,63` ✔ | la seule forme correcte |
| `.NET` : en-tête custom sur réponse 429 via `HttpMessageHandler` | `resp.Headers` : **True** · `resp.Content.Headers` : **False** · casse mélangée : **True** · `Content-Type` dans `resp.Headers` : **False** | cf. Q3 / Pitfall 2 |
| `.NET` : `SendAsync` sur un 429 | **aucune exception** ; `resp.Headers.RetryAfter` = 120 | HDR-02 mécaniquement acquis |
| `.NET` : `HttpRequestException` après `EnsureSuccessStatusCode()` | propriétés publiques : `HttpRequestError, StatusCode, TargetSite, Message, Data, InnerException, HelpLink, Source, HResult, StackTrace` — **pas de `Headers`** ; la réponse reste lisible après l'exception | cf. Pitfall 2 |
| `dotnet test Chronos.sln` | **505 réussis / 0 échec / 54 s** | baseline confirmée |

---

## Standard Stack

### Core — rien à installer

| Composant | Version | Rôle | Pourquoi standard |
|---|---|---|---|
| `System.Net.Http.HttpClient` | intégré net8.0 | Émettre la sonde, lire `resp.Headers` | Déjà utilisé par les 3 clients HTTP du projet (`ChronosOAuthClient`, `ChronosOAuthUsageProvider`, `ClaudeOAuthUsageProvider`) ; `HttpResponseMessage.Headers` porte les en-têtes **quel que soit le code de statut** (vérifié) |
| `System.Globalization` (`NumberStyles`, `CultureInfo.InvariantCulture`) | intégré net8.0 | Parser des nombres d'en-tête indépendamment de la culture | Seule parade au piège fr-FR (Pitfall 1) ; `InvariantGlobalization=false` est verrouillé par CLAUDE.md |
| `System.Text.Json` | intégré net8.0 | **Non utilisé par la sonde** | Le corps de réponse ne doit jamais être lu (contrainte de sécurité). Aucun `JsonDocument` dans le nouveau provider |
| `CommunityToolkit.Mvvm` 8.4.2 | déjà référencé | `[ObservableProperty]` pour exposer statut/dépassement | Générateurs de source, aucune réflexion |
| xUnit 2.9.2 + `Xunit.StaFact` 1.1.11 | déjà référencés | Tests | `[Fact]` suffit (aucun BAML dans la sonde) → parallélisable, pas de `[Collection("XAML WPF")]` |

**Installation :** aucune.
```bash
# Rien à installer. Vérification de non-régression du graphe de paquets :
dotnet restore Chronos.sln
```

### Alternatives considérées

| Au lieu de | On pourrait | Pourquoi non |
|---|---|---|
| `resp.Headers.TryGetValues` | `resp.Headers.GetValues` | Lève `InvalidOperationException` si absent — or l'absence est le cas **normal** (401, plan sans `unified`) |
| Aiguillage par code HTTP | `try { EnsureSuccessStatusCode() } catch` | `HttpRequestException` **ne porte pas les en-têtes** (vérifié) : le chemin d'exception perd exactement l'information de HDR-02 |
| Sonde `POST /v1/messages` | `GET /api/oauth/usage` seul | Ne rend **rien** en erreur ; et c'est déjà `ChronosOAuthUsageProvider`. L'intérêt de la phase est précisément le complément |
| Sonde `POST /v1/messages` | `POST /v1/messages/count_tokens` | Piste à **ne pas** retenir sans preuve : rien n'indique que cet endpoint porte les en-têtes `unified` (il ne consomme pas de quota d'inférence, donc probablement pas) — cf. Open Question 3 |
| Modèle `claude-haiku-4-5` (alias) | `claude-haiku-4-5-20251001` (daté) | L'instantané daté a le **plancher de retrait le plus proche de la gamme** (2026-10-15). L'alias survit à son remplacement. Les deux sont acceptables si le 404 modèle est traité |
| Constante 300 s | Réglage utilisateur d'intervalle | Décision projet déjà prise pour `RefreshIntervalSeconds` : « persisté **SANS UI dédiée** (décision verrouillée) ». Un interrupteur on/off répond mieux à HDR-06 qu'un curseur |
| Champ sur `WindowState` | Champ sur `UsageSnapshot` | **Impossible** : `CompositeUsageProvider` reconstruit le record sans `with`, et le fichier est interdit |

---

## Architecture Patterns

### Structure de fichiers recommandée

```
src/Chronos/
├── Models/
│   ├── StatutServeur.cs               # NOUVEAU — enum : Autorise / AutoriseAvertissement / Rejete / NonReconnu
│   ├── EtatDepassement.cs             # NOUVEAU — record : Utilization? / ResetsAt? / Statut?
│   ├── WindowState.cs                 # +2 champs init nullable (StatutServeur?, EtatDepassement?)
│   └── UsageSnapshot.cs               # INTOUCHÉ (tout ajout serait détruit par le composite)
├── Services/
│   ├── UsageNormalization.cs          # NOUVEAU — point UNIQUE de conversion (HDR-05), pur
│   ├── RateLimitHeaderUsageProvider.cs# NOUVEAU — la sonde (HDR-01/02/03/04/06)
│   ├── IEtatServeur.cs                # NOUVEAU — canal latéral de compte (motif IAuthStatus)
│   ├── ChronosOAuthUsageProvider.cs   # conversions rapatriées dans UsageNormalization
│   ├── ClaudeOAuthUsageProvider.cs    # idem
│   ├── ClaudeUsageObjectProvider.cs   # idem
│   ├── DiagnosticService.cs           # nomme la nouvelle source ; 2 conversions rapatriées
│   └── CompositeUsageProvider.cs      # INTERDIT — non modifié (phase 19)
├── ViewModels/
│   ├── WindowGaugeViewModel.cs        # + statut serveur par fenêtre
│   └── MainViewModel.cs               # + dépassement (canal latéral)
├── Views/SettingsWindow.xaml          # + interrupteur « sonde d'en-têtes » et coût annoncé (HDR-06)
└── App.xaml.cs                        # insertion en primary d'un NOUVEAU composite externe
```

### Pattern 1 — La sonde jetable : consommatrice de l'autorité, jamais rafraîchisseuse (répond à Q1, Q2)

**Quoi.** Un `IUsageProvider` qui émet `POST https://api.anthropic.com/v1/messages` avec `max_tokens:1` dans
le seul but de lire les en-têtes de la réponse.

**Quand.** Au plus une fois par `CadenceNominale` (300 s), jamais si le coffre est vide, jamais si
l'interrupteur de réglage est à `false`.

**Règles non négociables :**
1. **Le jeton vient de `ChronosTokenAuthority.GetAccessTokenAsync` et de nulle part ailleurs.** Le
   commentaire de tête de l'autorité nomme déjà cette phase :
   « tous les autres (provider d'usage, service de fond, **sonde d'en-têtes de la phase 18**) deviennent de
   simples clients ». Un troisième rafraîchisseur = course de rotation du refresh token = **fausse
   déconnexion** (`anthropics/claude-code#25609`).
2. **Rejeu unique sur 401** (motif `ChronosOAuthUsageProvider:86-98`) : `InvaliderAccessToken()` →
   re-demander → si refus persistant `SignalerRefusServeur()`.
3. **`SignalerSucces()` sur un 429 porteur d'en-têtes.** Un 429 prouve que le jeton est **valide** : ne pas
   le signaler laisserait une pastille de déconnexion mentir pendant la saturation. C'est une différence
   assumée avec `ChronosOAuthUsageProvider`, qui sur 429 se contente de reculer parce qu'il n'apprend rien.
4. **Le corps n'est jamais lu.** Pas de `ReadAsStreamAsync`, pas de `JsonDocument`. `using (resp)` suffit.
5. **En-têtes de requête** (l'ordre et la casse sont sans importance, la présence non) :
   `Authorization: Bearer …`, `anthropic-beta: oauth-2025-04-20` (**prouvé obligatoire**),
   `anthropic-version: 2023-06-01`, `User-Agent: claude-code/2.1.30` (réutiliser la constante existante),
   `Content-Type: application/json` (posé par `StringContent`).
6. **Corps minimal, sans rien de plus** : ni `system`, ni `tools`, ni `temperature`/`top_p`/`top_k` (400 sur
   les modèles ≥ 4.7), ni `effort` (non supporté par Haiku 4.5), ni `metadata`.

### Pattern 2 — Aiguillage par code de statut, en-têtes lus AVANT tout (répond à Q1, Q3 — cœur de HDR-02)

**Quoi.** Une seule et même lecture d'en-têtes, quel que soit le code, puis décision.

```
resp = await SendAsync(...)            // ne lève pas sur 4xx/5xx
lire resp.Headers  ─────────────────┐  // AVANT toute décision : c'est ça, HDR-02
                                    │
401 (après rejeu) → SignalerRefusServeur + recul nominal  → rien (aucun en-tête, vérifié)
403              → SignalerRefusServeur + recul nominal   → rien
429 AVEC en-têtes → SignalerSucces + recul court          → SNAPSHOT EXACT ← la raison d'être de la phase
429 SANS en-têtes → recul = max(retry-after, 60 s)        → rien (ne rien inventer)
2xx AVEC en-têtes → SignalerSucces + recul nominal        → SNAPSHOT EXACT
2xx SANS en-têtes → recul nominal                         → rien (plan sans famille unified)
404/400 modèle    → recul LONG + drapeau de diagnostic    → rien (panne de configuration, pas de quota)
5xx / réseau      → recul exponentiel                     → rien
```

**Pourquoi « SANS en-têtes » doit être un cas de première classe.** Trois raisons concrètes :
la famille `unified` n'est **pas documentée** et peut disparaître ou être renommée ; un compte facturé à la
consommation d'API (et non par abonnement) n'a probablement pas de fenêtre 5 h/7 j du tout ; et un 429 de
plafond de dépense (`enforced_spend_limit_reached`, documenté) est un 429 **qui ne parle pas de quota
d'abonnement**. Dans les trois cas la bonne réponse est `UsageSnapshot.Empty`, jamais `0 %` ni `100 %`.
Corollaire utile : puisqu'on ne lit jamais le corps, on ne peut pas distinguer un 429 de plafond de dépense
d'un 429 de quota — et **on n'en a pas besoin** : l'absence d'en-têtes `unified` suffit à décider de ne rien
afficher. La contrainte de sécurité et la robustesse convergent ici.

**Anti-pattern :** `resp.EnsureSuccessStatusCode()` en tête de méthode. L'exception ne porte pas les en-têtes
(vérifié) : la phase entière serait vidée de son sens.

### Pattern 3 — Le statut serveur vit sur `WindowState`, pas sur `UsageSnapshot` (répond à Q4 — HDR-03)

**La démonstration mécanique** (`CompositeUsageProvider.cs:39-45`) :

```csharp
return new UsageSnapshot          // ← construction NEUVE, pas « p with { … } »
{
    FiveHour = fiveHour,          // instance de WindowState transmise PAR RÉFÉRENCE
    SevenDay = sevenDay,
    SourceCapturedAt = …,         // seul champ de snapshot recopié
};
```

Trois conséquences, toutes vérifiées en lecture de code :
1. Un champ ajouté à `UsageSnapshot` **n'est pas recopié** → détruit au premier composite traversé. La chaîne
   en compte deux aujourd'hui, trois demain. Et `CompositeUsageProvider.cs` est **interdit**.
2. Un champ ajouté à `WindowState` **voyage intégralement** : `Best()` rend l'instance gagnante telle quelle.
3. `LastExactUsageProvider` utilise bien `snap with { … }` — mais il est **au-dessus** du composite : il ne
   peut pas sauver ce que le composite a déjà jeté.

**Donc :**
```csharp
// Models/WindowState.cs — deux champs init nullable, aucun site de construction cassé
public StatutServeur? StatutServeur { get; init; }      // null = non rapporté (JAMAIS inventé)
public EtatDepassement? Depassement { get; init; }      // null = pas de dépassement rapporté
```

**Vocabulaire OUVERT, jamais deviné.** Trois valeurs sont rapportées par la communauté (`allowed`,
`allowed_warning`, `rejected`) ; une proposition d'issue mentionne `active`. L'enum doit donc porter un
quatrième membre :
```csharp
public enum StatutServeur { Autorise, AutoriseAvertissement, Rejete, NonReconnu }
```
`NonReconnu` quand la chaîne reçue n'est pas dans l'ensemble connu — on dit « statut serveur non reconnu »
plutôt que de le ranger d'autorité dans `Autorise`. C'est la même honnêteté que `null != 0`.

### Pattern 4 — Le dépassement : canal latéral, motif `IAuthStatus` (répond à Q4 — HDR-04)

**Fait de terrain établi par lecture du code d'origine** (`clawdmeter.py:182-209`) : les deux familles sont
lues en **`if` / `elif`**, donc **mutuellement exclusives**. La forme overage correspond à un autre type de
compte (`"acct": "ent"` contre `"acct": "pro"`) et n'a **ni 5 h ni 7 j** : `weekly_reset_at` est mis à `0.0`.
Et son statut se lit sur `anthropic-ratelimit-unified-status` — **sans segment de fenêtre**, et **non**
`-overage-status` comme l'indique CONTEXT.md (écart à corriger dans le plan : lire **les deux noms**).

**Conséquences pour la conception :**
- Le dépassement **n'est pas une troisième `WindowKind`.** `UsageSnapshot` n'a que deux emplacements
  `required` et aucun champ de snapshot ne survit au composite : une troisième fenêtre n'a littéralement
  nulle part où vivre en phase 18.
- Quand la forme overage est **seule**, les deux fenêtres sont légitimement `Unavailable` — donc `Best()`
  retiendra l'instance d'une **autre** source si celle-ci est `Exact`, et le `Depassement` porté par la
  fenêtre `Unavailable` **sera écarté**. Le champ sur `WindowState` ne suffit donc pas à lui seul.
- **Recommandation : lire les deux familles sans `elif`** (le `elif` d'origine est un choix d'affichage, pas
  une contrainte de protocole) **et doubler par un canal latéral** exactement sur le motif que le projet a
  déjà tranché pour l'état d'authentification. `IAuthStatus.cs` porte la justification mot pour mot :

  > « L'état d'auth n'est délibérément PAS un champ d'UsageSnapshot : ce dernier traverse deux composites
  > imbriqués dont Best() choisit PAR FENÊTRE, et il n'existe aucune règle sensée pour fusionner deux états
  > d'auth issus de branches différentes. »

  Le dépassement est exactement de cette nature : **un fait de compte**, produit par **une seule** source,
  sans règle de fusion par fenêtre. D'où :
```csharp
// Services/IEtatServeur.cs — type NEUTRE, motif IAuthStatus à l'identique
public interface IEtatServeur
{
    EtatDepassement? Depassement { get; }                       // null = rien rapporté
    event EventHandler<EtatDepassement?>? DepassementChange;     // émis sur un thread du POOL, sur TRANSITION
}
```
  Implémenté par `RateLimitHeaderUsageProvider` lui-même, réexposé sur la **même instance** en DI (motif
  `ChronosTokenAuthority` / `IAuthStatus` : `AddSingleton<IEtatServeur>(sp => sp.GetRequiredService<…>())`)
  et consommé par `MainViewModel` qui marshalle lui-même via `IUiDispatcher` (frontière RAF-04).

**Ce que « affiché » veut dire en phase 18.** Le critère de succès 3 de la ROADMAP exige que l'utilisateur
voie le statut et le dépassement, alors que la phase n'est pas marquée UI. Surface minimale et suffisante :
(a) `DiagnosticService` les nomme dans son rapport ; (b) une propriété `[ObservableProperty]` par
information, bindée en texte discret. **Aucune géométrie de cadran nouvelle** — c'est la phase 20.

### Pattern 5 — Un point unique de normalisation, gardé mécaniquement (répond à Q7 — HDR-05)

**Inventaire réel : il y a CINQ conversions, pas trois.**

| # | Emplacement | Entrée | Conversion actuelle |
|---|---|---|---|
| 1 | `ChronosOAuthUsageProvider.cs:172-175` | `/api/oauth/usage` | `p / 100.0` · `DateTimeOffset.TryParse(…, InvariantCulture, RoundtripKind)` |
| 2 | `ClaudeOAuthUsageProvider.cs:130-132` | `/api/oauth/usage` (coffre app bureau) | `p / 100.0` · ISO 8601 |
| 3 | `ClaudeUsageObjectProvider.cs:75-78` | pont statusLine `usage.json` | `pct / 100.0` · `FromUnixTimeSeconds(epoch)` |
| 4 | `DiagnosticService.cs:481-483` (`Pct`) | `/api/oauth/usage` (appel direct du diagnostic) | affichage brut `F0` de `utilization` 0..100 |
| 5 | `DiagnosticService.cs:137` (`W`) | `usage.json` | affichage brut de `used_percentage` |
| 6 | **NOUVEAU** | en-têtes `unified` | `0..1` (aucune division) · epoch **texte** → secondes |

Les deux du diagnostic sont de l'affichage, mais elles participent au même risque de divergence : HDR-05 dit
« un point unique », pas « un point unique pour les providers ». Les rapatrier.

**Le point unique :**
```csharp
// Services/UsageNormalization.cs — PUR, aucun I/O, aucun type WPF, aucune horloge
public static class UsageNormalization
{
    public static double? FractionDepuisPourcentage(double? p0a100);      // 0..100 → 0..1
    public static double? FractionDepuisFraction(double? f0a1);           // 0..1   → 0..1 (validé)
    public static double? FractionDepuisTexteFraction(string? valeur);    // en-tête "0.63" → 0,63 (INVARIANT)
    public static DateTimeOffset? InstantDepuisIso(string? valeur);       // ISO 8601
    public static DateTimeOffset? InstantDepuisEpochSecondes(long? s);    // epoch s
    public static DateTimeOffset? InstantDepuisTexteEpoch(string? valeur);// en-tête "1783180800" (INVARIANT)
    public static string PourcentagePourAffichage(double? fraction0a1);   // 0..1 → « 63 % » / «»
}
```

**Décisions à graver dans ce fichier :**
- Bornage : une fraction hors `[0..1]` est **rendue telle quelle et non clampée** si elle dépasse 1
  (`Exhausted` dépend de `>= 1`, et un dépassement réel doit pouvoir se voir) ; en revanche une valeur
  négative, `NaN` ou infinie → `null`. Ne jamais clamper vers 0 : ce serait inventer.
- **Plancher de sanité sur l'epoch** : rejeter tout epoch antérieur à 2020-01-01 → `null`. C'est exactement
  le bug réel déjà constaté sur cette machine (`usage.json` figé avec `"resets_at": 9`, epoch 1970) ; un
  point unique est l'endroit naturel pour l'éradiquer une fois pour toutes.
- Tolérance à la casse et aux espaces sur les valeurs d'en-tête (`valeur.Trim()`).

**La garde mécanique.** La réflexion ne peut pas voir un `/ 100.0` : le seul filet qui fonctionne est un
**balayage du texte source**, sur le modèle des gardes de non-retour déjà en place
(`ServicesLayerPurityTests.Aucun_type_de_plafond_ne_subsiste_dans_l_assembly`). Motif proposé :

- Chemin des sources **injecté par MSBuild**, jamais deviné (CLAUDE.md interdit `Assembly.Location` et une
  remontée de dossiers depuis `AppContext.BaseDirectory` casserait silencieusement) :
  ```xml
  <!-- tests/Chronos.Tests/Chronos.Tests.csproj -->
  <ItemGroup>
    <AssemblyAttribute Include="System.Reflection.AssemblyMetadata">
      <_Parameter1>CheminSourcesChronos</_Parameter1>
      <_Parameter2>$(MSBuildProjectDirectory)\..\..\src\Chronos</_Parameter2>
    </AssemblyAttribute>
  </ItemGroup>
  ```
- Le test échoue si un `.cs` de `Services/` **autre que `UsageNormalization.cs`** contient
  `/ 100`, `* 100`, `FromUnixTimeSeconds`, `FromUnixTimeMilliseconds` ou `DateTimeOffset.TryParse`, hors
  liste d'exemptions **nominative et documentée** (`ClaudeTokenReader` et `SessionMonitor` lisent des epochs
  de jeton/session, pas d'usage — à exempter explicitement).
- **Le test doit échouer, jamais être ignoré,** si le chemin injecté n'existe pas : une garde qui se met en
  sourdine ne garde rien.

### Pattern 6 — Cadence bornée, auto-throttle, découplée du tick (répond à Q6 — HDR-06)

**Motif déjà éprouvé** dans `ChronosOAuthUsageProvider:66-70` : un **frein inconditionnel** en première
instruction, et — leçon chèrement payée en phase 17 — le droit d'appeler est **découplé de l'existence d'un
cache** :

> « « Ai-je le droit d'appeler ? » est une question DISTINCTE de « ai-je un cache à servir ? » : les
> confondre (le défaut d'origine) faisait disparaître le frein à chaque redémarrage de l'exe, donc
> exactement quand un 429 en cours devait être respecté. »

**Ne pas refaire cette erreur dans la sonde** : `if (now < _prochainAppelAutorise) return ServirCacheOu(Empty)`.

**Articulation avec `RefreshOrchestrator`.** Aucune. Le tick est de 60 s (`RefreshOptions` dérivé de
`RefreshIntervalSeconds`, défaut 60). Le composite appelle **les deux** `GetAsync` sans court-circuit
(`CompositeUsageProvider.cs:25-27`, commentaire assumé) : la sonde est donc sollicitée à chaque tick et
**refuse elle-même** 4 fois sur 5. Aucun couplage à `RefreshOptions`, aucun `IHostedService` de plus.

**Constantes proposées :**

| Constante | Valeur | Justification |
|---|---|---|
| `CadenceNominale` | 300 s | Ordre de grandeur de l'auteur d'origine (`UsageWatcher.INTERVAL = 300.0`) ; le reset exact est **donné** par l'en-tête, sonder plus souvent n'apprend rien (le commentaire d'origine le dit : « Haeufiges Pollen wuerde daran nichts verbessern ») |
| `Recul429` | `max(retry-after, 60 s)` | Une sonde **rejetée ne consomme pas de quota** : on peut donc rester réactif précisément pendant la saturation — l'instant où l'overlay sert le plus |
| `ReculReseau` | 60 s → ×2 → plafond 15 min | Motif `ChronosTokenAuthority.ReculInitial/ReculMax` ; un tick de 60 s hors ligne tenterait 1 440 requêtes/jour |
| `ReculModeleInvalide` | 60 min | Un identifiant de modèle périmé est une panne de **configuration** : réessayer vite ne répare rien et consomme |
| `CacheUtilisable` | 300 s (= cadence) | **Volontairement plus court que les 15 min de `ChronosOAuthUsageProvider`** : à fiabilité égale `Best()` privilégie le `primary`, donc un cache long de la sonde battrait une lecture **fraîche** de `/api/oauth/usage`. Le classement par fraîcheur est la phase 19 ; en attendant, la parade est de ne pas servir de cache vieux |
| `Timeout` | 8 s | Aligné sur `ChronosOAuthUsageProvider.Timeout` |

**Coût réel, à annoncer sans arrondir vers le bas.** Haiku 4.5 : **1 $/MTok** en entrée, **5 $/MTok** en
sortie (tarifs officiels vérifiés). Une sonde ≈ 10 jetons d'entrée + 1 de sortie ≈ **0,000015 $**. À 300 s :
**288 sondes/jour**, ≈ 0,004 $/jour, ≈ **0,13 $/mois** *si* facturé en crédits d'API. Sur un abonnement, le
coût n'est pas en dollars mais en **quota** : la sonde consomme la fenêtre qu'elle mesure (effet
d'observation), de façon négligeable mais non nulle. Formulation honnête proposée pour
`SettingsWindow.xaml`, section **DONNÉES**, juste sous « Connexion Claude » :

```
Sonde d'en-têtes                                        [ ● ]
chiffres exacts même en saturation — consomme une micro-
requête sur ton compte toutes les 5 min (≈ 288/jour)
```

Interrupteur au motif exact des autres bascules (`ToggleButton Style="{StaticResource Switch}"`,
`IsChecked="{Binding …, Mode=OneWay}"` + `Command`), nouveau champ `ChronosSettings.SondeEnTetesActivee`.
**Champ distinct de `OAuthUsageEnabled`** : ce dernier garde le jeton de l'app bureau (`GatedOAuthUsageProvider`)
et son profil de coût est nul ; les mélanger empêcherait l'utilisateur de couper la seule source qui dépense.
Défaut **`true`**, par cohérence avec `OAuthUsageEnabled = true` (« vrais chiffres dès l'installation ») et
parce que le coût annoncé est de l'ordre du centime — mais c'est un choix à assumer explicitement dans le plan.

### Pattern 7 — Insertion dans la chaîne : `primary` d'un nouveau composite externe (répond à Q5)

**Aujourd'hui** (`App.xaml.cs:318-326`) :
```
LastExactUsageProvider                       (décorateur, tête, écrivain unique du dernier exact)
└── CompositeUsageProvider
    ├── primary : ChronosOAuthUsageProvider  (login OAuth propre, Exact)
    └── fallback: CompositeUsageProvider
        ├── primary : GatedOAuthUsageProvider(coffre app bureau, Exact, sous portillon)
        └── fallback: ClaudeUsageObjectProvider (pont statusLine, Exact)
```

**Cible :**
```
LastExactUsageProvider
└── CompositeUsageProvider                      ← NOUVELLE instance (on instancie, on ne modifie pas le .cs)
    ├── primary : RateLimitHeaderUsageProvider  ← LA SONDE
    └── fallback: CompositeUsageProvider        ← la chaîne actuelle, inchangée
        ├── primary : ChronosOAuthUsageProvider
        └── fallback: CompositeUsageProvider(GatedOAuthUsageProvider, ClaudeUsageObjectProvider)
```

**Pourquoi la sonde en `primary` et non en `fallback` — l'argument décisif :**
`Best()` ne retient le `fallback` que s'il est **STRICTEMENT** plus fiable (`CompositeUsageProvider.cs:54-55`).
Les deux sources produisent `Exact`. En `fallback`, la sonde ne gagnerait **jamais** quand l'endpoint OAuth
répond — et son snapshot est le **seul** porteur du statut serveur et du dépassement. HDR-03 et HDR-04
seraient morts-nés à chaque tick nominal.

**Ce que la position ne décide PAS :** le coût. Le composite appelle les deux `GetAsync` sans court-circuit.
Seul l'auto-throttle borne la cadence.

**Laquelle est la plus fiable, la moins coûteuse, laquelle répond en 429 :**

| Critère | `ChronosOAuthUsageProvider` (`GET /api/oauth/usage`) | `RateLimitHeaderUsageProvider` (`POST /v1/messages`) |
|---|---|---|
| Coût par appel | **nul** (aucun jeton d'inférence) | une micro-requête de quota |
| Répond en 429 | **non** — aucune information utile en erreur (vérifié : 401 en bordure, `request_id: null`) | **oui, c'est sa raison d'être** (MEDIUM : cf. Open Question 1) |
| Statut serveur / dépassement | jamais | **seule source** |
| Fiabilité de la forme | corps JSON documenté par l'usage du projet, stable depuis v2.1 | en-têtes **non documentés** → peuvent disparaître |
| Granularité | deux fenêtres | deux fenêtres + statut + dépassement + `representative-claim` |

Verdict : la sonde est **plus riche et plus résiliente**, l'endpoint OAuth est **plus sobre**. Les garder
**tous les deux**, sonde en tête, est le bon compromis — et c'est précisément ce que la phase 19 viendra
arbitrer par la fraîcheur plutôt que par la position.

**Cadeau gratuit :** placée **sous** `LastExactUsageProvider`, la sonde hérite de la persistance EXA-01 sans
une ligne de code — ses fenêtres `Exact` sont écrites dans `last-exact.json` et rebouchent les trous au
redémarrage.

**Détail à corriger au passage (non cassant, utile à la phase 19) :** `ChronosOAuthUsageProvider.Read` ne
renseigne **pas** `WindowState.CapturedAt` (seulement `SourceCapturedAt` au niveau du snapshot), si bien que
`LastExactStore.Convertir` persiste `captured_at: null`. **La sonde doit, elle, renseigner `CapturedAt` par
fenêtre** : la doctrine de la phase 19 (limite d'âge, delta borné) en dépend directement.

### Anti-patterns à éviter

- **`EnsureSuccessStatusCode()` comme chemin de contrôle** → `HttpRequestException` ne porte pas les
  en-têtes (vérifié) : HDR-02 s'évapore.
- **`double.Parse` / `TryParse` sans culture** → `FormatException` ou `false` silencieux sous fr-FR (vérifié).
- **En-tête absent interprété comme `0`** — c'est ce que fait `_pct()` du code d'origine
  (`return 0` dans le `except`). Interdit ici : `null != 0`, c'est la doctrine du projet.
- **Nouveau champ sur `UsageSnapshot`** → détruit par le composite, qu'on n'a pas le droit de modifier.
- **Troisième `WindowKind` pour le dépassement** → aucun emplacement dans un `UsageSnapshot` à deux champs
  `required`.
- **Un deuxième rafraîchisseur de jeton** dans la sonde → fausse déconnexion garantie.
- **Bump de `LastExactStore.SchemaVersion`** pour persister statut/dépassement → casse 3 fixtures de test
  pour persister une assertion serveur **volatile** qui serait un mensonge une heure plus tard.
- **Lire le corps de la réponse** « juste pour le diagnostic » → viole une contrainte de sécurité explicite.
- **Modifier la signature de `DiagnosticService`** au milieu → 10 sites de construction (1 prod + 9 tests).
  Si nécessaire : paramètre **optionnel, en dernier**, précédent documenté en phase 17.
- **Journaliser une valeur d'en-tête avec le jeton dans le même message** → aucune valeur d'en-tête ne doit
  voisiner un identifiant.

---

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser à la place | Pourquoi |
|---|---|---|---|
| Obtenir un access token valide | Un rafraîchisseur dans la sonde | `ChronosTokenAuthority.GetAccessTokenAsync` | Sémaphore + double-vérification + rotation persistée + verrou de refus définitif déjà écrits et testés ; un second rafraîchisseur = `invalid_grant` = fausse déconnexion (`claude-code#25609`) |
| Publier « déconnecté / hors ligne » | Un second canal d'état d'auth | `IAuthStatus` + `SignalerSucces` / `SignalerRefusServeur` / `InvaliderAccessToken` | Émission sur transition uniquement, déjà bindé à la pastille (TOK-02/03) |
| Persister le dernier chiffre exact | Un cache disque dans la sonde | `LastExactUsageProvider` + `LastExactStore` (déjà en tête de chaîne) | Écriture atomique, relecture tolérante, invalidation au reset, recalcul de géométrie — tout est fait |
| Choisir la meilleure source par fenêtre | Une règle d'arbitrage dans la sonde | `CompositeUsageProvider` (instancié, **non modifié**) | C'est la phase 19. Toute règle ajoutée ici serait à défaire |
| Parser un `retry-after` | `int.Parse` sur la chaîne | `resp.Headers.RetryAfter` (typé, vérifié peuplé) | Gère les deux formes (delta-seconds et date HTTP) |
| Fabriquer une réponse HTTP de test porteuse d'en-têtes | Un serveur local, un `HttpListener` | `FakeHttpMessageHandler` (à étendre d'une fabrique `AvecEnTetes`) | Interdiction absolue de réseau en test ; le fake existe et compte déjà les envois |
| Horloge déterministe | `DateTimeOffset.UtcNow` | `IClock` / `FakeClock` | Toute la suite de tests en dépend |
| Convertir un pourcentage / un epoch | Une division locale « évidente » | `UsageNormalization` | C'est littéralement HDR-05 ; 5 divergences existent déjà |

**Insight clé :** cette phase n'a **aucune brique à inventer**. Les phases 16 et 17 ont posé l'autorité de
jeton, le canal d'état, la persistance et le patron de provider HTTP testable. Le risque n'est pas
l'ignorance, c'est la **duplication** : chaque ligne qui refait quelque chose d'existant est un point de
divergence futur. Le seul code réellement neuf est la lecture d'en-têtes et le point de normalisation.

---

## Runtime State Inventory

Phase de refactorisation partielle (HDR-05 rapatrie 5 conversions) + ajout de source. Inventaire de l'état
qui vit **hors du dépôt** :

| Catégorie | Éléments trouvés | Action requise |
|---|---|---|
| **Données stockées** | `%APPDATA%\Chronos\last-exact.json` — schéma `version: 1`, clés `utilization` / `resets_at` / `captured_at` **par fenêtre**. Le fichier n'existe pas sur cette machine (aucun relevé exact depuis le 2026-07-12). | **Aucune migration.** Ne PAS persister statut/dépassement → `SchemaVersion` reste à 1 → aucun fichier utilisateur invalidé. Si un plan décide de persister, il faut bumper à 2 ET réécrire 3 fixtures de test (cf. Q8) |
| **Données stockées (2)** | `%APPDATA%\Chronos\settings.json` — 825 octets, 2026-07-12. Ajout de `SondeEnTetesActivee`. | **Aucune migration active** : `System.Text.Json` ignore les membres non mappés, et un champ absent prend son défaut. Précédent DEL-06 documenté dans `ChronosSettings` |
| **Données stockées (3)** | `%APPDATA%\Chronos\usage.json` — 77 octets, figé au 2026-07-10, contenu `{"five_hour":{"used_percentage":10,"resets_at":9}}`. `resets_at: 9` = epoch 1970. | **Aucune action de migration**, mais c'est la **fixture réelle** du plancher de sanité d'epoch de `UsageNormalization` : à reprendre telle quelle dans un test |
| **Configuration de service vivant** | Aucune configuration de service externe n'est touchée par cette phase. `~/.claude/settings.json` (hooks + statusLine) n'est pas concerné : la sonde ne passe par aucun pont. | **Aucune** — vérifié par lecture du périmètre (aucun installateur modifié) |
| **État enregistré par l'OS** | Aucun. Pas de tâche planifiée, pas de service. L'autostart (`shell:startup`) existe mais n'est pas touché. | **Aucune** |
| **Secrets / variables d'environnement** | `%APPDATA%\Chronos\oauth.dat` — **518 octets, mtime 1783863147**, chiffré DPAPI. **Ni lu, ni déchiffré, ni présenté au serveur pendant cette recherche** (contrôlé avant/après : inchangé). La sonde n'y touche pas non plus : elle passe par `ChronosTokenAuthority`. | **Aucune** — et le contrôle 518/1783863147 doit être **rejoué par chaque tâche du plan** qui touche au réseau ou aux tests OAuth |
| **Artefacts de build / paquets installés** | `tests/Chronos.Tests/bin` et `obj` en `net8.0-windows` (suite rejouée aujourd'hui, 505/505). Aucun paquet ajouté. | **Aucune** — un `dotnet test` suffit ; pas de réinstallation |

**Question canonique :** une fois chaque fichier du dépôt à jour, quel système à l'exécution conserve encore
l'ancien état ? Réponse : **aucun**, à condition de ne pas bumper `SchemaVersion`. C'est l'argument
opérationnel — et pas seulement esthétique — pour ne pas persister le statut serveur.

---

## Common Pitfalls

### Pitfall 1 — Le point décimal français (le plus grave, et invisible au compilateur)

**Ce qui se passe mal.** Un en-tête vaut `0.63`. Sous `fr-FR` le séparateur décimal est `,` et le séparateur
de groupes est une espace insécable étroite — pas le point. Vérifié aujourd'hui :

| Écriture | Résultat sous fr-FR |
|---|---|
| `double.Parse("0.63")` | **`FormatException`** |
| `double.TryParse("0.63", out v)` | **`false`** — perte SILENCIEUSE |
| `double.TryParse("0.63", NumberStyles.Float, CultureInfo.CurrentCulture, out v)` | `false` |
| `double.TryParse("0.63", NumberStyles.Float, CultureInfo.InvariantCulture, out v)` | `0,63` ✔ |

**Pourquoi ça arrive.** `InvariantGlobalization=false` est **verrouillé** par CLAUDE.md (« casse le formatage
fr-FR des dates de reset et des comptes à rebours »). Le code JSON existant y échappe par chance :
`JsonElement.TryGetDouble` est indépendant de la culture. La lecture d'en-têtes est le **premier** chemin
texte→nombre du projet.

**Comment l'éviter.** Toute conversion texte→nombre exclusivement dans `UsageNormalization`, avec
`NumberStyles.Float` + `CultureInfo.InvariantCulture` (et `NumberStyles.Integer` pour l'epoch).

**Signes d'alerte.** L'overlay reste muet sur la machine de l'utilisateur alors que la suite de tests est
verte — parce que les tests tournent sous la culture de la machine et qu'un test écrit avec
`InvariantCulture` des deux côtés ne prouve rien. **Contre-mesure de test obligatoire** : forcer
`CultureInfo.CurrentCulture = new CultureInfo("fr-FR")` dans le test (avec restauration en `finally`) et
prouver que la lecture rend quand même `0,63`.

### Pitfall 2 — L'exception qui avale l'information (HDR-02 annulé)

**Ce qui se passe mal.** `resp.EnsureSuccessStatusCode()` en début de traitement lève sur le 429.
`HttpRequestException` expose `HttpRequestError, StatusCode, TargetSite, Message, Data, …` — **aucune
propriété `Headers`** (vérifié par réflexion). L'unique instant où la sonde a de la valeur est perdu.

**Comment l'éviter.** Lire `resp.Headers` **avant** toute décision et n'aiguiller que sur `(int)resp.StatusCode`.
Nuance vérifiée : appeler `EnsureSuccessStatusCode()` ne détruit rien — la réponse reste lisible après
l'exception — mais le code du `catch` n'a pas accès à `resp` s'il est écrit comme un chemin d'erreur. C'est
la **forme** du code qui est le piège, pas l'API.

### Pitfall 3 — Le mauvais sac à en-têtes

**Ce qui se passe mal.** `.NET` scinde les en-têtes en deux collections. Chercher
`anthropic-ratelimit-unified-5h-utilization` dans `resp.Content.Headers` rend `false` **même quand
l'en-tête est là** (vérifié : `resp.Headers` → `True`, `resp.Content.Headers` → `False` ; et
symétriquement `Content-Type` **n'est pas** dans `resp.Headers`).

**Comment l'éviter.** Les en-têtes non standard sont des en-têtes de **réponse** : `resp.Headers`. La
recherche est **insensible à la casse** (vérifié : `Anthropic-RateLimit-Unified-5h-Utilization` trouve la
valeur) — aucune normalisation de casse à écrire. Ceinture et bretelles acceptables : une aide
`Lire(resp, nom)` qui tente `resp.Headers` puis `resp.Content.Headers`, avec le commentaire expliquant que
le second cas ne devrait jamais se produire.

### Pitfall 4 — Le nom d'en-tête pris pour acquis

**Ce qui se passe mal.** La famille `anthropic-ratelimit-unified-*` est **absente de la documentation
publique Anthropic** : la page officielle des limites documente `anthropic-ratelimit-requests-*`,
`-tokens-*`, `-input-tokens-*`, `-output-tokens-*` (avec `reset` en **RFC 3339**, pas en epoch) et
`retry-after`, et **rien** sur `unified`. Deux écarts concrets ont déjà été relevés entre CONTEXT.md et le
code source d'origine :
- CONTEXT.md dit `anthropic-ratelimit-unified-overage-status` ; le code d'origine lit
  `anthropic-ratelimit-unified-status` (**sans** segment de fenêtre).
- CONTEXT.md présente l'overage comme une « variante » ; le code d'origine en fait une branche **`elif`**,
  donc une forme de réponse **alternative** liée au type de compte.

**Comment l'éviter.** Traiter chaque nom d'en-tête comme une **hypothèse** : constantes nommées et
regroupées, lecture de **plusieurs noms candidats** pour le statut, absence = `null`, et un test par en-tête
absent. Envisager aussi les noms observés ailleurs mais non confirmés
(`-7d-status`, `-remaining`, `-representative-claim`) en lecture **optionnelle**.

**Signes d'alerte.** La sonde rend `Empty` sur un 2xx : c'est le signal que la famille a changé de nom. Le
diagnostic doit pouvoir le dire (« sonde : 200, aucun en-tête `unified` reconnu ») sans exposer le corps.

### Pitfall 5 — Le `0 %` fabriqué par l'absence

**Ce qui se passe mal.** Le code d'origine fait `_pct()` → `return 0` en cas d'échec de conversion, et lit
`meta["status"] = "unknown"` par défaut. Transposé tel quel, un en-tête absent afficherait **« 0 % de quota
consommé »** : le mensonge exactement inverse de celui que v1.5 corrige.

**Comment l'éviter.** `double?` et `DateTimeOffset?` partout ; `WindowState.Unavailable(kind)` quand la
fenêtre n'est pas renseignée. `StatutServeur.NonReconnu` plutôt que `Autorise` par défaut.

### Pitfall 6 — L'identifiant de modèle périmé, indistinguable d'une panne de quota

**Ce qui se passe mal.** `claude-haiku-4-5-20251001` est **Active** mais son plancher de retrait
(2026-10-15) est **le plus proche de toute la gamme** — dans un mois. Une fois retiré, la sonde reçoit
`404 not_found_error`, sans le moindre en-tête. Traité comme « pas de données », l'overlay redevient muet
sans que personne ne sache pourquoi : la panne silencieuse, reconstituée à l'identique.

**Comment l'éviter.**
- Utiliser l'**alias documenté `claude-haiku-4-5`** plutôt que l'instantané daté : il survit au remplacement
  du snapshot.
- Identifiant de modèle en **constante unique et nommée**, avec en commentaire la date de vérification.
- Un 404/400 doit poser un **drapeau distinct** de « pas de données », visible dans le diagnostic
  (« sonde : modèle refusé par le serveur — identifiant à mettre à jour »), et un recul **long** (1 h) :
  réessayer toutes les 5 minutes un modèle qui n'existe plus est du gaspillage pur.
- Vérifié par ailleurs : l'authentification est évaluée **avant** le corps (un corps malformé sans jeton rend
  401, pas 400) — donc un modèle périmé **ne peut pas** se déguiser en problème d'authentification, et
  réciproquement. Bonne nouvelle pour la lisibilité du diagnostic.

### Pitfall 7 — L'en-tête `anthropic-beta` oublié

**Ce qui se passe mal.** Sans `anthropic-beta: oauth-2025-04-20`, le serveur interprète le Bearer comme une
**clé d'API** : `401 "invalid x-api-key"` (vérifié). Le message d'erreur n'évoque même pas OAuth — on
conclurait à un jeton mort et l'on déclencherait une fausse pastille de déconnexion.

**Comment l'éviter.** Constante partagée avec `ChronosOAuthUsageProvider:157`, et un test qui **inspecte
`FakeHttpMessageHandler.LastRequest`** pour prouver les 4 en-têtes de requête (motif déjà présent dans
`ChronosOAuthUsageProviderTests`).

### Pitfall 8 — La voie de facturation (le 429 qui ne parle pas de quota)

**Ce qui se passe mal.** Il est documenté qu'un requête OAuth d'abonnement peut basculer dans la voie
« crédits d'API » et rendre un `429` de **plafond de dépense** (`error.details.error_code =
enforced_spend_limit_reached`, **sans** `retry-after`), ou un `400` « You have reached your specified API
usage limits ». Le facteur déclenchant documenté est le **contenu du prompt système** (au-delà de ~4–4,5 k
caractères d'instructions applicatives). Notre sonde n'a **aucun** prompt système : le risque est faible et
le code d'origine rend bien les compteurs d'abonnement. Mais un tel 429 ne doit **surtout pas** être lu comme
« quota épuisé ».

**Comment l'éviter.** La parade est déjà dans les contraintes : **on ne lit jamais le corps**, donc on ne
peut pas se tromper sur son contenu, et **un 429 sans en-têtes `unified` ne produit rien**. Sécurité et
robustesse convergent. Corollaire : ne jamais ajouter de `system`, `tools` ou `metadata` au corps de la sonde
« pour faire propre ».

### Pitfall 9 — Le cache de la sonde qui bat une lecture fraîche

**Ce qui se passe mal.** `Best()` privilégie le `primary` à fiabilité égale. Un cache de 15 min sur la sonde
(valeur de `ChronosOAuthUsageProvider.CacheUsable`) ferait gagner un chiffre de 14 minutes contre une lecture
**fraîche** de `/api/oauth/usage` : une régression de fraîcheur introduite par la phase censée améliorer
l'exactitude.

**Comment l'éviter.** `CacheUtilisable = CadenceNominale` (300 s), et `CapturedAt` **renseigné par fenêtre**
pour que la phase 19 puisse arbitrer par l'âge. Le résoudre proprement est hors périmètre — ne pas l'aggraver
l'est.

### Pitfall 10 — L'effet d'observation

**Ce qui se passe mal.** Les chiffres rendus par les en-têtes **incluent la sonde elle-même**. Le pourcentage
affiché est donc « après la mesure ».

**Comment l'éviter.** Rien à corriger — c'est exact et honnête. À ne pas « compenser » par un calcul : ce
serait inventer. À mentionner dans le libellé de coût, pas dans le code.

### Pitfall 11 — Le rayon de souffle des signatures de constructeur

**Ce qui se passe mal.** `DiagnosticService` a **10 sites de construction** (1 en production, 9 en tests :
`CadranBindingTests`, `CompositionRootTests`, `DiagnosticServiceTests` ×3, `MainViewModelTests` ×2,
`OverlayWindowConfigTests`, `ThemingTests`). Un paramètre ajouté au milieu casse 9 fichiers de test sans le
moindre bénéfice.

**Comment l'éviter.** Précédent explicite de la phase 17 (`DiagnosticService.cs:31-33`) : paramètre
**optionnel**, **en dernière position**, avec le commentaire expliquant pourquoi. Idem pour
`WindowGaugeViewModel` et `MainViewModel`.

---

## Code Examples

### 1. Lecture d'en-têtes robuste sur n'importe quel code de statut (vérifié empiriquement)

```csharp
// Source : comportement vérifié le 2026-09-12 sur .NET (SDK 10.0.201, cible net8.0-windows).
// resp.Headers contient les en-têtes non standard ; resp.Content.Headers NON. Casse indifférente.
// SendAsync NE LÈVE PAS sur 4xx/5xx : la réponse 429 arrive intacte, en-têtes compris.

private static string? Lire(HttpResponseMessage resp, string nom)
{
    // En-tête de RÉPONSE : resp.Headers. Le repli sur Content.Headers ne devrait jamais servir
    // (vérifié) — il est là pour qu'un jour où le serveur classerait l'en-tête autrement, on ne
    // perde pas l'information en silence.
    if (resp.Headers.TryGetValues(nom, out var v)) return v.FirstOrDefault();
    if (resp.Content?.Headers.TryGetValues(nom, out var c) == true) return c.FirstOrDefault();
    return null;
}
```

### 2. Le point unique de normalisation (HDR-05) — le seul endroit où l'on parse

```csharp
// Services/UsageNormalization.cs — PUR : aucun I/O, aucune horloge, aucun type WPF.
using System.Globalization;

public static class UsageNormalization
{
    // Plancher de sanité : le cas RÉEL de cette machine est usage.json avec "resets_at": 9,
    // soit le 1er janvier 1970. Un tel instant ne décrit rien : on rend null plutôt qu'une
    // géométrie fausse. 2020-01-01 est antérieur à l'existence des fenêtres d'usage Claude.
    private static readonly DateTimeOffset PlancherEpoch = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>En-tête « 0.63 » → 0,63. INVARIANT OBLIGATOIRE : sous fr-FR,
    /// double.Parse("0.63") LÈVE et TryParse sans culture rend FALSE (vérifié). Le point décimal
    /// des en-têtes HTTP n'est pas négociable, la culture de la machine si.</summary>
    public static double? FractionDepuisTexteFraction(string? valeur)
        => double.TryParse(valeur?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f)
           && !double.IsNaN(f) && !double.IsInfinity(f) && f >= 0.0
            ? f            // NON clampé à 1 : un dépassement réel doit rester visible (Exhausted >= 1)
            : null;        // absent / illisible / négatif → INCONNU, JAMAIS 0

    /// <summary>/api/oauth/usage et pont statusLine : 0..100 → 0..1.</summary>
    public static double? FractionDepuisPourcentage(double? p0a100)
        => p0a100 is { } p && !double.IsNaN(p) && !double.IsInfinity(p) && p >= 0.0 ? p / 100.0 : null;

    /// <summary>En-tête « 1783180800 » → instant. Epoch SECONDES, invariant, plancher de sanité.</summary>
    public static DateTimeOffset? InstantDepuisTexteEpoch(string? valeur)
        => long.TryParse(valeur?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)
            ? InstantDepuisEpochSecondes(s) : null;

    public static DateTimeOffset? InstantDepuisEpochSecondes(long? secondes)
    {
        if (secondes is not { } s) return null;
        try
        {
            var t = DateTimeOffset.FromUnixTimeSeconds(s);
            return t < PlancherEpoch ? null : t;   // « resets_at: 9 » ne décrit rien
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>/api/oauth/usage : ISO 8601 avec offset. RoundtripKind + invariant.</summary>
    public static DateTimeOffset? InstantDepuisIso(string? valeur)
        => DateTimeOffset.TryParse(valeur, CultureInfo.InvariantCulture,
                                   DateTimeStyles.RoundtripKind, out var d)
           && d >= PlancherEpoch ? d : null;
}
```

### 3. Le test qui grave le piège de culture (à écrire AVANT la sonde)

```csharp
// Source : comportement vérifié empiriquement. Ce test échouerait sur une implémentation
// « naturelle » — c'est tout son intérêt. La culture est forcée et restaurée.
[Fact]
public void Un_en_tete_en_point_decimal_se_lit_meme_sous_une_culture_a_virgule()
{
    var precedente = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

        // Preuve du piège : la forme naïve perd la donnée EN SILENCE.
        Assert.False(double.TryParse("0.63", out _));

        // Le point unique, lui, rend la valeur.
        Assert.Equal(0.63, UsageNormalization.FractionDepuisTexteFraction("0.63")!.Value, 6);
        Assert.Equal(new DateTimeOffset(2026, 3, 3, 4, 0, 0, TimeSpan.Zero),
                     UsageNormalization.InstantDepuisTexteEpoch("1772510400"));
    }
    finally { CultureInfo.CurrentCulture = precedente; }
}
```

### 4. `FakeHttpMessageHandler` étendu : une réponse porteuse d'en-têtes, y compris en 429

```csharp
// tests/Chronos.Tests/Fakes/FakeHttpMessageHandler.cs — AJOUT (ne casse aucun appelant existant)
/// <summary>Réponse porteuse d'EN-TÊTES (le corps n'a aucune importance : la sonde ne le lit jamais).
/// Sert le cas décisif de HDR-02 : un 429 qui livre quand même les chiffres.</summary>
public static FakeHttpMessageHandler AvecEnTetes(
    HttpStatusCode statut, IReadOnlyDictionary<string, string> enTetes, string corps = "{}") =>
    new(_ =>
    {
        var r = new HttpResponseMessage(statut) { Content = new StringContent(corps) };
        foreach (var (k, v) in enTetes) r.Headers.TryAddWithoutValidation(k, v);
        return r;
    });
```

### 5. Corps de la sonde et en-têtes de requête (repris de la vérité-terrain, modèle mis à jour)

```csharp
// Constantes GROUPÉES et DATÉES : chaque nom d'en-tête est une hypothèse, pas un fait.
// Vérifié le 2026-09-12 : anthropic-beta est OBLIGATOIRE — sans lui, le Bearer est lu comme
// une clé d'API et le serveur répond 401 « invalid x-api-key » (message trompeur).
private const string Url         = "https://api.anthropic.com/v1/messages";
private const string Beta        = "oauth-2025-04-20";
private const string Version     = "2023-06-01";
private const string UserAgent   = "claude-code/2.1.30";              // même valeur que le provider OAuth
// ALIAS et non instantané daté : claude-haiku-4-5-20251001 est Active mais son plancher de retrait
// (2026-10-15) est le plus proche de la gamme. Modèle le moins cher : 1 $/MTok in, 5 $/MTok out.
private const string Modele      = "claude-haiku-4-5";
private const string Corps       = """{"model":"claude-haiku-4-5","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}""";
// Ni system, ni tools (400 documenté avec un jeton OAuth), ni temperature/top_p/top_k (400 sur
// les modèles >= 4.7), ni effort (non supporté par Haiku 4.5), ni metadata.

// En-têtes de RÉPONSE. Familles CONCURRENTES : lire les deux, sans « elif » — le elif du code
// d'origine est un choix d'affichage, pas une contrainte de protocole.
private const string H5hUtil     = "anthropic-ratelimit-unified-5h-utilization";   // 0..1
private const string H5hReset    = "anthropic-ratelimit-unified-5h-reset";         // epoch SECONDES
private const string H5hStatut   = "anthropic-ratelimit-unified-5h-status";        // allowed|allowed_warning|rejected
private const string H7dUtil     = "anthropic-ratelimit-unified-7d-utilization";
private const string H7dReset    = "anthropic-ratelimit-unified-7d-reset";
private const string H7dStatut   = "anthropic-ratelimit-unified-7d-status";        // NON confirmé : optionnel
private const string HOverUtil   = "anthropic-ratelimit-unified-overage-utilization";
private const string HOverReset  = "anthropic-ratelimit-unified-overage-reset";
private const string HStatut     = "anthropic-ratelimit-unified-status";           // statut GLOBAL, sans fenêtre
private const string HClaim      = "anthropic-ratelimit-unified-representative-claim"; // ex. « five_hour »
```

### 6. Insertion en DI (`App.xaml.cs`) — instancier, ne pas modifier le composite

```csharp
// HDR-01/02 — SONDE D'EN-TÊTES en TÊTE de la chaîne exacte. Simple CONSOMMATEUR de l'autorité
// de jeton (jamais un troisième rafraîchisseur : ce serait la course de rotation du refresh token).
services.AddSingleton(sp => new RateLimitHeaderUsageProvider(
    sp.GetRequiredService<ChronosTokenAuthority>(),
    new HttpClient(),                                  // long-lived, une seule destination (constante)
    sp.GetRequiredService<IClock>(),
    sp.GetRequiredService<SettingsService>()));        // interrupteur relu FRAIS à chaque appel (motif Gated)
// HDR-04 — canal LATÉRAL du dépassement : MÊME instance réexposée (motif ChronosTokenAuthority/IAuthStatus).
services.AddSingleton<IEtatServeur>(sp => sp.GetRequiredService<RateLimitHeaderUsageProvider>());

// La sonde est le PRIMARY d'un NOUVEAU composite externe. Argument décisif : Best() privilégie le
// primary à fiabilité ÉGALE — en fallback, le seul snapshot porteur du statut serveur et du
// dépassement serait écarté à chaque tick où /api/oauth/usage répond.
// CompositeUsageProvider.cs n'est PAS modifié (phase 19) : on l'INSTANCIE, c'est tout.
services.AddSingleton<IUsageProvider>(sp => new LastExactUsageProvider(
    inner: new CompositeUsageProvider(
        primary:  sp.GetRequiredService<RateLimitHeaderUsageProvider>(),
        fallback: new CompositeUsageProvider(
            primary:  sp.GetRequiredService<ChronosOAuthUsageProvider>(),
            fallback: new CompositeUsageProvider(
                primary:  sp.GetRequiredService<GatedOAuthUsageProvider>(),
                fallback: sp.GetRequiredService<ClaudeUsageObjectProvider>()))),
    store: sp.GetRequiredService<LastExactStore>(),
    clock: sp.GetRequiredService<IClock>()));
```

---

## State of the Art

| Ancienne approche | Approche actuelle | Quand ça a changé | Impact |
|---|---|---|---|
| `claude-haiku-4-5-20251001` comme référence de modèle bon marché | Toujours **Active**, mais l'alias **`claude-haiku-4-5`** est documenté et survit au remplacement de l'instantané | Alias documentés pour toute la gamme < 4.6 | Préférer l'alias ; traiter le 404 modèle comme une panne nommée |
| `claude-3-5-haiku` / `claude-3-haiku` comme « modèle le moins cher » | **Retirés** (2026-02-19 et 2026-04-20). Remplaçant recommandé : `claude-haiku-4-5-20251001` | 2026 | Toute recette datant d'avant 2026 qui cite un Haiku 3.x **échoue** aujourd'hui |
| `temperature` / `top_p` / `top_k` librement réglables | **Dépréciés** : 400 sur les modèles ≥ Opus 4.7 dès qu'une valeur non par défaut est passée | Claude Opus 4.7 | Ne rien mettre dans le corps de la sonde au-delà du strict minimum |
| `docs.anthropic.com` | Redirection 301 permanente vers **`platform.claude.com/docs`** | — | Toute URL de doc en dur dans un commentaire est à réécrire |
| En-têtes de limites « par organisation » (`anthropic-ratelimit-tokens-*`, reset **RFC 3339**) | Famille **`unified`** (`-5h-`/`-7d-`, utilization **0..1**, reset **epoch secondes**) pour les comptes d'abonnement — **non documentée** | inconnu | **Deux familles coexistent, avec des unités de reset opposées.** Ne jamais mélanger : c'est le piège HDR-05 sous une autre forme |

**Déprécié / périmé :**
- `Microsoft.Toolkit.Mvvm`, MVVMLight — sans objet ici, aucune dépendance ajoutée.
- Toute lecture de `.credentials.json` de Claude Code : sur cette machine, le fichier ne contient que
  `mcpOAuth` (diagnostic établi, `STATE.md`). La sonde doit passer par le jeton **propre** de Chronos —
  ce qui est déjà le cas via `ChronosTokenAuthority`.

---

## Open Questions

### 1. Les en-têtes `unified` reviennent-ils réellement sur un 429 ? (cœur de HDR-02)

- **Ce qu'on sait (HIGH).** Un **401 n'en porte aucun** — vérifié aujourd'hui sur 4 variantes de requête et
  2 endpoints. Le refus OAuth est rendu **en bordure** (`request_id: null`, corps de 117 octets) : la requête
  n'atteint jamais le cœur de l'API, donc aucun compteur ne peut être joint. `.NET` livre bien les en-têtes
  d'un 429 à l'application (vérifié sur handler contrôlé), et `retry-after` est typé et peuplé.
- **Ce qui reste flou (MEDIUM).** Que le **429 réel** d'Anthropic porte la famille `unified` repose sur
  trois sources concordantes mais aucune officielle : le commentaire du code d'origine
  (« 429 & Co. liefern die Header trotzdem mit ») et son implémentation (`except HTTPError: hdrs = e.headers`),
  plus des issues publiques affirmant que l'API « renvoie ces en-têtes à chaque requête ». **Non vérifiable
  ici** : il faudrait un jeton valide (celui de l'utilisateur est expiré depuis le 2026-07-12) *et* un compte
  réellement saturé.
- **Recommandation.** Implémenter HDR-02 tel quel — le coût du doute est nul, puisque « 429 sans en-têtes »
  est de toute façon un cas à traiter (Pattern 2). Et **ajouter au plan une tâche de vérification manuelle
  explicite**, à exécuter par l'utilisateur après sa reconnexion : capturer une fois les en-têtes réels et
  les consigner dans le rapport de diagnostic (noms présents, pas de valeurs sensibles). C'est la seule voie
  honnête pour faire passer ce point en HIGH. Ne **jamais** présenter HDR-02 comme prouvé avant.

### 2. Existe-t-il un `-7d-status` ? Le statut global est-il `-status` ou `-overage-status` ?

- **Ce qu'on sait.** Le code d'origine lit `anthropic-ratelimit-unified-5h-status` dans la branche
  abonnement et `anthropic-ratelimit-unified-status` (**sans** segment) dans la branche overage. CONTEXT.md
  annonce `-overage-status`, que **rien** ne confirme.
- **Recommandation.** Lire **les trois** noms, par ordre de préférence
  (`-5h-status` / `-7d-status` pour la fenêtre concernée, puis `-status` en repli global), et n'inventer
  aucun statut en l'absence des trois. Coût : trois constantes. Bénéfice : la phase ne casse pas si le nom
  diffère.

### 3. `GET /api/oauth/usage` porte-t-il AUSSI les en-têtes `unified` ? (enjeu : une sonde gratuite)

- **Ce qu'on sait.** Rien : sur un 401 l'endpoint ne rend **aucun** en-tête de limite (vérifié). Impossible
  à trancher sans jeton valide.
- **Pourquoi ça compte.** S'il les portait, HDR-01 à HDR-04 seraient satisfaits **sans dépenser un seul
  jeton** de quota, et HDR-06 deviendrait trivial.
- **Recommandation.** **Ne pas** bâtir la phase sur cette hypothèse. Mais ajouter **une ligne** au rapport de
  diagnostic listant les noms d'en-têtes de limite présents sur la réponse de `/api/oauth/usage` (noms
  seulement, jamais de valeurs). C'est quasi gratuit et cela tranchera la question au premier
  rafraîchissement réussi de l'utilisateur — potentiellement pour supprimer le coût de la sonde en phase 19+.

### 4. Le dépassement seul (compte « ent ») est-il atteignable par cet utilisateur ?

- **Ce qu'on sait.** La branche overage du code d'origine correspond à `"acct": "ent"` et n'a ni 5 h ni 7 j.
  L'utilisateur est sur un abonnement Max x20 (`STATE.md`) : la branche abonnement (`"acct": "pro"`) est
  la sienne.
- **Recommandation.** Implémenter la lecture overage (HDR-04 l'exige) et la porter par les deux canaux
  (Pattern 3 + 4), mais **ne pas** dimensionner l'UI pour elle et **documenter** la limitation : quand
  **seule** la famille overage est présente, les deux fenêtres sont `Unavailable` et `Best()` peut écarter la
  fenêtre porteuse au profit d'une autre source `Exact` — le canal latéral est précisément là pour que
  l'information survive quand même. Un test doit graver ce scénario, même s'il n'est pas atteignable sur
  cette machine.

### 5. Faut-il `anthropic-version` sur la sonde ?

- **Ce qu'on sait.** Non vérifiable (l'authentification est évaluée avant). La documentation l'exige pour la
  Messages API, et le code d'origine l'envoie.
- **Recommandation.** L'envoyer. Aucun coût, aucun doute.

---

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|---|---|---|---|---|
| .NET SDK | build + test | ✓ | **10.0.201** (cible `net8.0-windows`) | — |
| xUnit + `Xunit.StaFact` | suite de tests | ✓ | 2.9.2 / 1.1.11 | — |
| git | commits GSD | ✓ | 2.55.0.windows.2 | — |
| `curl` | sondes de caractérisation (recherche uniquement) | ✓ | — | — |
| Accès réseau `api.anthropic.com` | **recherche uniquement** ; jamais en test | ✓ (401 obtenus) | — | — |
| Jeton OAuth **valide** | vérification empirique du 429 porteur d'en-têtes, et du contenu réel de `/api/oauth/usage` | **✗** (expiré depuis le 2026-07-12) | — | **Aucun repli acceptable** : `FakeHttpMessageHandler` valide le code, pas le protocole. D'où la tâche de vérification manuelle recommandée (Open Question 1) |
| `%APPDATA%\Chronos\oauth.dat` | — | ✓ (présent, **518 o / mtime 1783863147, inchangé**) | — | **Ne jamais ouvrir** |

**Dépendances manquantes sans repli :**
- **Jeton OAuth valide.** Conséquence directe : HDR-02 sera *implémenté et testé sur faux transport* mais
  **non prouvé contre le serveur réel** à la fin de la phase. Le plan doit le dire, et le
  `18-VERIFICATION.md` ne doit pas cocher HDR-02 comme « prouvé en production » sans la capture manuelle.

**Dépendances manquantes avec repli :** aucune.

---

## Validation Architecture

### Test Framework

| Propriété | Valeur |
|---|---|
| Framework | xUnit 2.9.2 + `Xunit.StaFact` 1.1.11 (`[WpfFact]` pour le STA/BAML) |
| Fichier de config | `tests/Chronos.Tests/Chronos.Tests.csproj` (`net8.0-windows`, `UseWPF`, `IsTestProject`) |
| Commande rapide | `dotnet test Chronos.sln --nologo --filter "FullyQualifiedName~RateLimitHeaderUsageProviderTests"` |
| Commande complète | `dotnet test Chronos.sln --nologo` |
| Baseline mesurée le 2026-09-12 | **505 réussis / 0 échec / 0 ignoré — 54 s** |

### Phase Requirements → Test Map

| Req | Comportement | Type | Commande automatisée | Fichier existe ? |
|---|---|---|---|---|
| HDR-01 | 200 + en-têtes `unified` → deux fenêtres `Exact` avec utilization 0..1 et reset epoch | unit | `dotnet test --filter "FullyQualifiedName~RateLimitHeaderUsageProviderTests.Un_200_porteur_d_en_tetes"` | ❌ Wave 0 |
| HDR-01 | 4 en-têtes de requête émis (`Authorization`, `anthropic-beta`, `anthropic-version`, `User-Agent`) + corps `max_tokens:1` | unit | `…~RateLimitHeaderUsageProviderTests.La_requete_porte_les_en_tetes_exiges` | ❌ Wave 0 |
| HDR-01 | Coffre vide → `Empty`, **zéro** envoi HTTP (`SendCount == 0`) | unit | `…~RateLimitHeaderUsageProviderTests.Sans_jeton_aucun_appel` | ❌ Wave 0 |
| HDR-02 | **429 porteur d'en-têtes → snapshot `Exact` frais** (le test central de la phase) | unit | `…~RateLimitHeaderUsageProviderTests.Un_429_livre_quand_meme_les_chiffres` | ❌ Wave 0 |
| HDR-02 | 429 **sans** en-têtes → `Empty`, recul honorant `retry-after`, **aucune** valeur inventée | unit | `…~RateLimitHeaderUsageProviderTests.Un_429_muet_n_invente_rien` | ❌ Wave 0 |
| HDR-02 | 401 → rejeu unique puis `SignalerRefusServeur`, aucun en-tête exploité | unit | `…~RateLimitHeaderUsageProviderTests.Un_401_degrade_et_dit_le_refus` | ❌ Wave 0 |
| HDR-02 | 429 → `SignalerSucces` (un rate-limit **n'est pas** une déconnexion) | unit | `…~RateLimitHeaderUsageProviderTests.Un_429_ne_declare_jamais_de_deconnexion` | ❌ Wave 0 |
| HDR-03 | `-5h-status: allowed_warning` → `StatutServeur.AutoriseAvertissement` porté par la fenêtre 5 h | unit | `…~RateLimitHeaderUsageProviderTests.Le_statut_serveur_voyage_avec_la_fenetre` | ❌ Wave 0 |
| HDR-03 | Statut inconnu (`"active"`) → `NonReconnu`, **jamais** `Autorise` | unit | `…~RateLimitHeaderUsageProviderTests.Un_statut_inconnu_ne_se_devine_pas` | ❌ Wave 0 |
| HDR-03 | Le statut survit à `CompositeUsageProvider.Best()` quand la sonde est `primary` | unit | `…~CompositeUsageProviderTests` (**ÉTENDRE**, ne pas réécrire) | ⚠ à étendre |
| HDR-04 | Famille overage seule → `EtatDepassement` publié sur le canal latéral, fenêtres `Unavailable`, rien d'inventé | unit | `…~RateLimitHeaderUsageProviderTests.Le_depassement_seul_passe_par_le_canal_lateral` | ❌ Wave 0 |
| HDR-04 | `-overage-status` **ou** `-status` : les deux noms sont lus | unit | `…~RateLimitHeaderUsageProviderTests.Les_deux_noms_de_statut_global_sont_lus` | ❌ Wave 0 |
| HDR-05 | **fr-FR** : `0.63` se lit quand même 0,63 (et `TryParse` nu rend `false`) | unit | `…~UsageNormalizationTests.Un_en_tete_en_point_decimal_se_lit_meme_sous_une_culture_a_virgule` | ❌ Wave 0 |
| HDR-05 | Les 3 unités convergent : `0.63` (en-tête), `63` (`/api/oauth/usage`), `63` (`used_percentage`) → 0,63 | unit | `…~UsageNormalizationTests.Les_trois_unites_convergent` | ❌ Wave 0 |
| HDR-05 | `resets_at: 9` (epoch 1970, cas réel de cette machine) → `null` | unit | `…~UsageNormalizationTests.Un_epoch_de_1970_ne_decrit_rien` | ❌ Wave 0 |
| HDR-05 | **Garde de non-retour** : aucun `/ 100`, `FromUnixTimeSeconds`, `DateTimeOffset.TryParse` dans `Services/` hors `UsageNormalization.cs` (liste d'exemptions nominative) | unit | `…~NormalisationUniqueTests.Aucune_conversion_d_unite_ne_subsiste_hors_du_point_unique` | ❌ Wave 0 |
| HDR-05 | Non-régression du rapatriement : les 3 providers existants gardent un comportement **identique** | unit | `…~ChronosOAuthUsageProviderTests`, `…~ClaudeOAuthUsageProviderTests`, `…~ClaudeUsageObjectProviderTests` (**verts SANS modification** = preuve du refactor) | ✅ existe |
| HDR-06 | Deux `GetAsync` dans la même minute → **un seul** envoi HTTP | unit | `…~RateLimitHeaderUsageProviderTests.La_cadence_est_bornee` | ❌ Wave 0 |
| HDR-06 | Frein actif **sans cache** (leçon phase 17 : ne pas confondre « droit d'appeler » et « cache à servir ») | unit | `…~RateLimitHeaderUsageProviderTests.Le_frein_tient_meme_sans_cache` | ❌ Wave 0 |
| HDR-06 | Interrupteur à `false` → `Empty`, **zéro** envoi, jeton jamais demandé (motif `GatedOAuthUsageProviderTests`) | unit | `…~RateLimitHeaderUsageProviderTests.L_interrupteur_coupe_tout_acces_reseau` | ❌ Wave 0 |
| HDR-06 | Le libellé de coût est présent dans `SettingsWindow.xaml` | unit (BAML) | `…~CadranBindingTests` ou nouveau `ReglagesBindingTests` (`[WpfFact]`, `[Collection("XAML WPF")]`) | ⚠ à étendre |
| Tous | Graphe DI : sonde résolue, `IEtatServeur` = **même instance**, `IUsageProvider` toujours `LastExactUsageProvider` en tête | unit | `…~CompositionRootTests` (**ÉTENDRE**) | ⚠ à étendre |
| Tous | Pureté : aucun type WPF dans la sonde ni dans `UsageNormalization` | unit | `…~ServicesLayerPurityTests` (déjà générique, reste vert) | ✅ existe |
| Tous | **Sécurité** : aucun test ne joint le réseau ; tout coffre de test sous `Path.GetTempPath()` | unit | `…~RateLimitHeaderUsageProviderTests.Le_coffre_de_test_vit_toujours_sous_le_dossier_temporaire` (motif existant) | ❌ Wave 0 |

### Sampling Rate

- **Par commit de tâche :** `dotnet test Chronos.sln --nologo --filter "FullyQualifiedName~<ClasseTouchée>"` (< 10 s)
- **Par fusion de vague :** `dotnet test Chronos.sln --nologo` (54 s à la baseline)
- **Porte de phase :** suite complète verte avant `/gsd:verify-work`, **plus** le contrôle
  `oauth.dat = 518 o / mtime 1783863147`, **plus** la vérification de `ServicesLayerPurityTests` et
  `CompositionRootTests`.

### Wave 0 Gaps

- [ ] `tests/Chronos.Tests/UsageNormalizationTests.cs` — HDR-05 (dont le test de culture fr-FR, à écrire
      **avant** toute lecture d'en-tête)
- [ ] `tests/Chronos.Tests/NormalisationUniqueTests.cs` — garde de non-retour HDR-05 (balayage de source)
- [ ] `tests/Chronos.Tests/RateLimitHeaderUsageProviderTests.cs` — HDR-01/02/03/04/06
- [ ] `tests/Chronos.Tests/Fakes/FakeHttpMessageHandler.cs` — ajout de la fabrique `AvecEnTetes` (additif)
- [ ] `tests/Chronos.Tests/Chronos.Tests.csproj` — `AssemblyMetadata("CheminSourcesChronos", …)` pour la garde
- [ ] Extension de `CompositionRootTests` (sous-chaîne de la sonde, identité d'instance `IEtatServeur`)
- [ ] Extension de `CompositeUsageProviderTests` (survie du statut serveur à travers `Best()`)

*Aucun framework à installer : xUnit et `Xunit.StaFact` sont déjà en place et la suite est verte.*

---

## Q8 en détail — ce qui casse dans les 505 tests

**Conclusion : zéro test cassé, zéro test supprimé, si les 4 règles ci-dessous sont respectées.**

| Règle | Tests concernés nominativement | Verdict |
|---|---|---|
| Champs ajoutés à `WindowState` : `init`, **nullable**, **sans `required`** | 21 sites `new WindowState { … }` dans les tests (`WindowStateTests`, `CompositeUsageProviderTests`, `LastExactUsageProviderTests`, `LastExactStoreTests`, `WindowGaugeViewModelTests`, `WeeklyRecalibrationTests`, `MainViewModelTests`…) | **Compilent sans retouche.** Les comparaisons structurelles de record restent vraies (`null == null`). À **renforcer** : ajouter dans `WindowStateTests.Unavailable_neutralise_les_champs_et_garde_la_fenetre` deux `Assert.Null` pour les nouveaux champs |
| **Ne pas bumper** `LastExactStore.SchemaVersion` | `LastExactStoreTests:151` (`"version":999` refusé), `:192` et `:205` (`"version":1` accepté) | **Restent verts.** Un bump à 2 casserait ces 3 fixtures pour persister une assertion serveur **volatile** — donc un mensonge après le premier reset. Ne pas persister |
| **Ne pas changer** la signature de `DiagnosticService` (ou paramètre optionnel **en dernier**) | 10 sites : `App.xaml.cs:340`, `CadranBindingTests:37`, `CompositionRootTests:79`, `DiagnosticServiceTests:38/53/73`, `MainViewModelTests:72/118`, `OverlayWindowConfigTests:26`, `ThemingTests:115` | **Restent verts.** Précédent explicite de la phase 17 (`DiagnosticService.cs:31-33`). Le rapport peut nommer la nouvelle source **sans** paramètre supplémentaire, via le snapshot du composite |
| `LastExactUsageProvider` reste en **tête** de `IUsageProvider` | `CompositionRootTests:108` (`Assert.IsType<LastExactUsageProvider>`) | **Reste vert** : on insère un composite **sous** le décorateur, pas au-dessus |

**Tests à ÉTENDRE (jamais réécrire) :**
- `CompositionRootTests.Le_graphe_DI_resout_l_autorite_de_jeton_et_son_service_de_fond` — y enregistrer la
  sonde et prouver `Assert.Same(sonde, provider.GetRequiredService<IEtatServeur>())`. Chaque phase a
  agrandi cette garde (13, 14, 15, 17) : la tradition est établie.
- `CompositeUsageProviderTests` — un cas « la fenêtre gagnante emporte son statut serveur », qui **documente
  mécaniquement** pourquoi le champ est sur `WindowState` et pas sur `UsageSnapshot`.
- `ServicesLayerPurityTests` — rien à modifier ; la garde est générique et couvrira automatiquement
  `RateLimitHeaderUsageProvider`, `UsageNormalization` et `IEtatServeur`.

**Tests à surveiller pendant le rapatriement HDR-05 :** `ChronosOAuthUsageProviderTests`,
`ClaudeOAuthUsageProviderTests`, `ClaudeUsageObjectProviderTests`. Ils doivent rester verts **sans une seule
modification** — c'est exactement leur fonction de filet. `ChronosOAuthUsageProviderTests:41-46` porte déjà
en commentaire le piège d'unité de la phase 17 : le conserver mot pour mot.

**Bilan attendu :** 505 + ~25 à 35 nouveaux tests, **0 suppression**, **0 échec**.

---

## Sources

### Primaires (HIGH confidence)

- **Sondes réseau directes** émises le 2026-09-12 sur `POST https://api.anthropic.com/v1/messages` et
  `GET https://api.anthropic.com/api/oauth/usage` (6 variantes, jetons manifestement bidons) — codes,
  corps et en-têtes de réponse complets ; `oauth.dat` contrôlé inchangé avant/après.
- **Programme .NET exécuté localement** (SDK 10.0.201) — comportement de `double.Parse`/`TryParse` sous
  `fr-FR`, emplacement des en-têtes non standard sur un `HttpResponseMessage` 429, propriétés publiques de
  `HttpRequestException`, `resp.Headers.RetryAfter`.
- **`dotnet test Chronos.sln`** exécuté le 2026-09-12 — 505/505, 54 s (baseline confirmée).
- **Code source de Chronos**, lu intégralement : `ChronosOAuthUsageProvider.cs`, `ChronosTokenAuthority.cs`,
  `IAuthStatus.cs`, `ClaudeUsageObjectProvider.cs`, `ClaudeOAuthUsageProvider.cs`,
  `CompositeUsageProvider.cs`, `GatedOAuthUsageProvider.cs`, `LastExactUsageProvider.cs`,
  `LastExactStore.cs`, `ChronosOAuthClient.cs` (scopes `user:inference user:profile` — la sonde n'exige
  aucun nouveau scope), `ChronosSettings.cs`, `RefreshOptions.cs`, `TokenRefreshService.cs`,
  `WindowState.cs`, `UsageSnapshot.cs`, `App.xaml.cs`, `DiagnosticService.cs`, `SettingsWindow.xaml`,
  `Chronos.csproj` (`InvariantGlobalization=false`), `ServicesLayerPurityTests.cs`, `CompositionRootTests.cs`,
  `ChronosOAuthUsageProviderTests.cs`, `FakeHttpMessageHandler.cs`, `WindowStateTests.cs`,
  `LastExactStoreTests.cs`.
- **`clawdmeter.py` (705 lignes), récupéré et lu** —
  `raw.githubusercontent.com/juppeee/claude-session-browser/main/clawdmeter.py` : `poll_usage_meta`
  (lignes 149-226), `API_HEADERS`/`API_BODY` (39-50), `POLL_INTERVAL = 60` (52),
  `UsageWatcher.INTERVAL = 300.0` / `RETRY_INTERVAL = 60.0` (628-629). **Vérité-terrain de référence.**
- **Anthropic — Rate limits** (`platform.claude.com/docs/en/api/rate-limits`) : liste **exhaustive** des
  en-têtes documentés (`retry-after`, `anthropic-ratelimit-{requests,tokens,input-tokens,output-tokens}-*`,
  `anthropic-priority-*`), reset en **RFC 3339** ; 429 de plafond de dépense
  (`enforced_spend_limit_reached`, **sans** `retry-after`). **Aucune mention de la famille `unified`.**
- **Anthropic — Models overview** (`platform.claude.com/docs/en/about-claude/models/overview`) :
  `claude-haiku-4-5-20251001` + alias `claude-haiku-4-5` ; 1 $/5 $ par MTok (le moins cher) ;
  « Default effort : Not supported » pour Haiku 4.5.
- **Anthropic — Model deprecations** (`platform.claude.com/docs/en/about-claude/model-deprecations`) :
  `claude-haiku-4-5-20251001` **Active**, retrait « pas avant le 2026-10-15 » ; `claude-3-5-haiku` et
  `claude-3-haiku` **retirés** ; `temperature`/`top_p`/`top_k` dépréciés (400 sur ≥ 4.7).

### Secondaires (MEDIUM confidence)

- **`github.com/steipete/CodexBar` issue #1894** — dump de noms d'en-têtes **avec valeurs** :
  `anthropic-ratelimit-unified-5h-utilization: 0.01`, `-5h-reset: 1783180800`, `-7d-utilization: 0.63`,
  `-7d-reset: 1783713600`, `-5h-status: allowed`, `anthropic-ratelimit-unified-representative-claim: five_hour` ;
  corps de sonde identique (`claude-haiku-4-5-20251001`, `max_tokens:1`) ; coût ≈ 1 jeton de sortie par
  sondage. **Corrobore indépendamment le code d'origine** (noms + unités).
- **Recherche web convergente** sur le vocabulaire de statut `allowed` / `allowed_warning` / `rejected`,
  lu par Claude Code lui-même depuis `anthropic-ratelimit-unified-status`.
- **`anthropics/claude-code` issue #12829** — existence de
  `anthropic-ratelimit-unified-representative-claim`.
- **`NousResearch/hermes-agent` issue #53212** — voie de facturation : un prompt système applicatif volumineux
  (> ~4–4,5 k caractères) fait basculer une requête OAuth d'abonnement vers la voie « crédits d'API »
  (429 plafond de dépense). Une sonde **sans** prompt système est hors de ce régime.

### Tertiaires (LOW confidence — à valider, non utilisées pour décider)

- **`anthropics/claude-code` issue #55333** — mentionne `-5h-remaining` et un `reset` en **ISO 8601** avec un
  statut `active` : il s'agit de la **proposition de format JSON de l'auteur de l'issue**, pas d'un relevé
  d'en-têtes. Ne pas confondre avec le protocole.
- **Mentions communautaires d'une fenêtre `7d_sonnet`/`7d_opus`** et d'un en-tête `-remaining` : plausible,
  non corroboré par le code d'origine. À lire **optionnellement**, jamais à exiger.
- **`openclaw/openclaw` issue #56047** — cite des `anthropic-ratelimit-unified-tokens-*` avec valeurs
  élidées : sans valeur d'usage pour cette phase.

---

## Metadata

**Répartition de la confiance :**

| Domaine | Niveau | Raison |
|---|---|---|
| Mécanique .NET (`HttpClient`, en-têtes sur 429, `HttpRequestException`, culture) | **HIGH** | Programme exécuté localement sur la machine cible ; résultats reproduits et cités |
| Absence d'en-têtes de limite sur un 401 | **HIGH** | 6 sondes réelles, 2 endpoints, 4 formes d'authentification |
| `anthropic-beta: oauth-2025-04-20` obligatoire | **HIGH** | Deux messages d'erreur serveur distincts, avec et sans l'en-tête |
| Identifiant et coût du modèle | **HIGH** | Documentation officielle Anthropic à jour (statut, alias, tarifs) |
| Contraintes du code existant (composite sans `with`, `Best()` favorise `primary`, 10 sites `DiagnosticService`, `SchemaVersion` épinglée) | **HIGH** | Lecture directe des sources et des tests, ligne par ligne |
| Baseline de tests et impact sur les 505 | **HIGH** | Suite rejouée (505/505, 54 s) + inventaire nominatif des sites |
| Noms exacts des en-têtes `unified` et unités | **MEDIUM** | Code source d'origine + un dump indépendant avec valeurs ; **rien d'officiel** — la doc publique ne connaît pas cette famille |
| Présence des en-têtes sur un **429 réel** | **MEDIUM** | Trois sources concordantes, aucune vérification possible sans jeton valide ni compte saturé (Open Question 1) |
| Vocabulaire de statut (`allowed` / `allowed_warning` / `rejected`) | **MEDIUM** | Convergence communautaire ; un quatrième membre `NonReconnu` est la parade conçue pour cette incertitude |
| Forme de la réponse en dépassement (overage) | **MEDIUM-LOW** | Une seule source (le code d'origine), branche `elif` liée à un type de compte que cet utilisateur n'a pas |

**Date de recherche :** 2026-09-12
**Valide jusqu'au :** 2026-10-12 (30 jours) — **mais** deux échéances plus courtes :
le plancher de retrait de `claude-haiku-4-5-20251001` est le **2026-10-15**, et la famille d'en-têtes
`unified` n'étant pas documentée, elle peut changer **sans préavis**. Revérifier l'identifiant de modèle et
les noms d'en-têtes à toute reprise de cette phase après le 2026-10-01.
