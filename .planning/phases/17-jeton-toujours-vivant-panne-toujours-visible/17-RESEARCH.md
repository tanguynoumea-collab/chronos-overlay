# Phase 17 : Jeton toujours vivant, panne toujours visible — Research

**Researched:** 2026-09-09
**Domain:** Cycle de vie d'un jeton OAuth 2.0 à rotation de refresh token, dans une app WPF .NET 8 / MVVM / Generic Host — et remontée honnête d'un état d'échec jusqu'à un overlay de 170 px.
**Confidence:** HIGH (le gros de la réponse est de l'archéologie de code local vérifiée + API .NET vérifiées sur learn.microsoft.com ; les seules zones MEDIUM/LOW sont les comportements serveur d'Anthropic, non documentés publiquement)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

Aucune décision utilisateur verrouillée : `workflow.skip_discuss = true`.

### Claude's Discretion

> Tous les choix d'implémentation sont à la discrétion de Claude — la discussion est désactivée
> (`workflow.skip_discuss=true`).

### SÉCURITÉ — contraintes absolues sur le jeton (copiées verbatim)

> - **Ne JAMAIS logger, écrire en clair, mettre en exception ou concaténer dans une URL** un access token
>   ou un refresh token. Le jeton ne vit qu'en variable locale, le temps de construire l'en-tête
>   `Authorization: Bearer`.
> - **NE JAMAIS déclencher un vrai rafraîchissement contre le jeton stocké de l'utilisateur pendant le
>   développement ou les tests.** Le flux OAuth fait tourner le refresh token : un refresh exécuté hors de
>   l'application, dont le résultat n'est pas re-sauvegardé, **invaliderait définitivement** le jeton stocké
>   et déconnecterait l'utilisateur. Tous les tests passent par des faux (`FakeHttpMessageHandler` existe déjà).
> - Le coffre reste chiffré DPAPI portée `CurrentUser` (`ChronosOAuthStore`), sous `%APPDATA%\Chronos\oauth.dat`.
> - **Rotation du refresh token** : si l'API renvoie un nouveau refresh token, il DOIT être persisté avant
>   toute autre action, sinon le suivant échouera.

### Contraintes projet (copiées verbatim)

> - MVVM strict, `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers Models/Views/ViewModels/Services.
> - Aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`) : la pastille est un état exposé
>   par le ViewModel, la logique de jeton reste neutre.
> - Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin, aucune dépendance native.
> - Dégradation : réseau coupé, refresh token invalide, serveur injoignable → aucun crash, aucun gel de l'UI.
> - UI et commentaires en **français**.
> - **Baseline à l'entrée de phase : 419 tests xUnit verts.**

### Deferred Ideas (OUT OF SCOPE)

> - Trou visuel dans la `UniformGrid` des réglages (3 boutons dans une grille à 2 colonnes) hérité de la
>   phase 16 — **reporté à la phase 20**, qui touche le cadran et le diagnostic.

**Hors périmètre rappelé par le CONTEXT et la ROADMAP :** la source par en-têtes de rate-limit (phase 18),
la doctrine du composite / limite d'âge / EXA-05 « jamais connecté → invite » (phase 19), la distinction
visuelle frais / daté / indisponible et `EXA-06` diagnostic-qui-nomme-la-source (phase 20).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description (REQUIREMENTS.md) | Research Support |
|----|-------------------------------|------------------|
| TOK-01 | Le jeton OAuth est **rafraîchi préventivement** avant expiration, sans attendre un échec au moment du besoin. | § Architecture Patterns → *Pattern 1 (autorité unique de jeton)* + *Pattern 2 (service de fond à tick court + prédicat pur)* ; § Code Examples 1-3 ; § Pitfall 1 (sommeil/veille), Pitfall 5 (backoff). |
| TOK-02 | Un **échec d'authentification est visible** dans l'overlay — plus jamais un 401 muet pendant deux mois. | § Architecture Patterns → *Pattern 3 (classification des échecs)* + *Pattern 4 (canal d'état neutre vers le VM)* ; § Surface visuelle de la pastille ; § Pitfalls 2, 3, 6. |
| TOK-03 | Le signal de déconnexion permet de **relancer le login en un clic**. | § Architecture Patterns → *Pattern 5 (commande de reconnexion ≠ commande bascule existante)* ; § Pitfall 7 (`IsLoggedIn` ment) ; § Code Example 5. |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Directives actionnables extraites de `./CLAUDE.md`, à respecter par le plan :

| Directive | Portée en phase 17 |
|-----------|--------------------|
| Stack imposée : C# / .NET 8 (`net8.0-windows`) / WPF / MVVM CommunityToolkit + `Microsoft.Extensions.DependencyInjection` + `Hosting` | **Aucune nouvelle dépendance NuGet n'est nécessaire** (voir § Standard Stack). |
| MVVM strict : `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers `Models/Views/ViewModels/Services` | La pastille = `[ObservableProperty]` + `[RelayCommand]` sur `MainViewModel`. |
| Rendu XAML pur, aucune dépendance native, pas de SkiaSharp | La pastille est une `Ellipse`/`Button` XAML. |
| `WindowStyle=None` + `AllowsTransparency=True` + `Topmost` + `ShowInTaskbar=False` | Ne pas y toucher — `OverlayWindowConfigTests` verrouille ces 6 propriétés. |
| Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin | Le coffre reste `%APPDATA%\Chronos\oauth.dat`. |
| Honnêteté : `utilization`/`resets_at` prioritaires, ne jamais présenter une estimation comme exacte | Corollaire phase 17 : ne jamais présenter « hors ligne » comme « déconnecté ». |
| Robustesse : aucune source ≠ crash, parsing tolérant | Le service de fond ne doit JAMAIS lever (motif `DesktopUiaPollService.PollOnce`). |
| **UI et commentaires en français** | Noms de types en anglais (convention observée : `RefreshOrchestrator`, `LastExactUsageProvider`), commentaires/XML-doc et libellés UI en français. |
| **Zone cliquable sur fenêtre transparente doit avoir un `Background` non-null (même `#01000000`)** | Corrigé/précisé en § Pitfall 8 : en WPF `Transparent` suffit, `{x:Null}` non — preuve en dépôt. |
| Enforcement GSD : passer par un workflow GSD avant toute édition | Cette phase passe par `/gsd:plan-phase 17` puis `/gsd:execute-phase 17`. |
| Activer `frontend-design` + `windows-wpf` sur les tâches UI | À signaler dans le plan pour la tâche « pastille ». |

Aucun répertoire `.claude/skills/` ni `.agents/skills/` dans le dépôt — aucun skill projet à charger.

---

## Summary

La panne à corriger n'est pas un bug ponctuel mais **trois défauts structurels superposés** dans
`ChronosOAuthUsageProvider.GetAsync` (lignes 60-98) :
(1) le rafraîchissement est **paresseux** — il n'a lieu qu'au moment d'un `GetAsync`, donc un jeton expiré le
2026-07-12 n'est jamais renouvelé si le refresh échoue une fois ;
(2) quand le refresh échoue, le code **tente quand même l'appel** avec le jeton mort, obtient un 401,
et le traite exactement comme un 500 ou une coupure réseau — `_nextAllowedCall = now + MinInterval; return
ServeCachedOr(UsageSnapshot.Empty, now)` ;
(3) `ChronosOAuthClient.RefreshAsync` renvoie `OAuthTokens?` et son `catch { return null; }` (ligne 119)
**détruit l'information de cause** : « refresh token révoqué » et « wifi coupé » sont le même `null`.
Le 401 muet de deux mois est la conséquence mécanique de ces trois lignes.

La réponse la plus économe en risque n'est pas d'ajouter un service de rafraîchissement **à côté** du
rafraîchissement paresseux existant — ce serait créer une **course de rotation de refresh token** (le second
rafraîchisseur présenterait un refresh token déjà roulé et récolterait un `invalid_grant`, c'est-à-dire une
fausse déconnexion). C'est le bug documenté n° 25609 du dépôt `anthropics/claude-code` lui-même. La réponse
est d'**extraire une autorité unique de jeton** (`ChronosTokenAuthority`) : un singleton neutre qui possède
le coffre, le client, un `SemaphoreSlim(1,1)`, l'état d'authentification et la politique de backoff. Le
provider d'usage et le nouveau service de fond deviennent tous deux de simples **clients** de cette autorité
(`GetAccessTokenAsync`), qui sérialise par construction. Bénéfice collatéral non négligeable : la sonde
d'en-têtes de la **phase 18** aura besoin du même jeton et pourra s'y brancher sans créer un troisième
rafraîchisseur.

L'état d'authentification remonte au `MainViewModel` par le **même motif que l'orchestrateur de données déjà
en place** : un événement neutre (`event EventHandler<EtatAuthentification>`) sur un service `Chronos.Services`,
auquel le VM s'abonne dans son constructeur et qu'il marshalle par `IUiDispatcher.Post` — la frontière de
thread unique (RAF-04). Zéro type WPF dans `Services/`, `ServicesLayerPurityTests` reste vert sans modification.

**Primary recommendation :** créer `Services/ChronosTokenAuthority.cs` (autorité unique : sémaphore, backoff,
état) + `Services/TokenRefreshService.cs` (`IHostedService` à tick court + prédicat PUR, premier tick immédiat),
faire de `ChronosOAuthUsageProvider` un simple consommateur de `GetAccessTokenAsync`, exposer
`EtatAuthentification` au `MainViewModel` par un événement neutre, et binder une pastille de 14 px ancrée en
bas-droite du `Grid` racine de `MainWindow.xaml` sur une **nouvelle** commande `ReconnecterCommand` (surtout
pas sur `LoginClaudeCommand`, qui **déconnecte** quand `IsLoggedIn` est vrai — et il l'est, puisqu'il ne teste
que l'existence du fichier).

---

## Standard Stack

### Core — rien à installer

| Composant | Version | Rôle en phase 17 | Pourquoi c'est le standard ici |
|-----------|---------|------------------|-------------------------------|
| `System.Threading.SemaphoreSlim` | BCL net8.0 | Sérialiser l'unique rafraîchissement (une seule rotation à la fois) | Primitive asynchrone (`WaitAsync`) ; `lock` est interdit autour d'un `await`. Aucun paquet. |
| `System.Threading.PeriodicTimer` | BCL net8.0 | Tick du service de fond | Déjà employé par `RefreshOrchestrator.RunPeriodicAsync` (ligne 66). **Vérifié** : `Period` est *get/set* depuis .NET 8 (moniker `net-8.0` sur learn.microsoft.com) — une cadence adaptative reste possible, mais voir Anti-pattern 2. |
| `Microsoft.Extensions.Hosting` (`IHostedService` / `BackgroundService`) | 8.0.1 (déjà référencé) | Cycle de vie du service de fond | Deux précédents dans le dépôt : `RefreshOrchestrator : BackgroundService` et `DesktopUiaPollService : IHostedService, IDisposable`. |
| `System.Net.Http.HttpRequestException.HttpRequestError` | BCL net8.0 | Distinguer « réseau absent » de « autre » | **Vérifié** sur learn.microsoft.com : propriété disponible à partir du moniker `net-8.0`. Facultatif mais gratuit. |
| `System.Security.Cryptography.ProtectedData` | 8.0.0 (déjà référencé) | Coffre DPAPI | Inchangé — `ChronosOAuthStore` ne bouge pas. |
| `CommunityToolkit.Mvvm` | 8.4.2 (déjà référencé) | `[ObservableProperty]` / `[RelayCommand]` de la pastille | Générateurs de source ; déjà le motif du VM. |
| xUnit + `Xunit.StaFact` | 2.9.2 / 1.1.11 | Tests | `[WpfFact]` pour tout ce qui construit une `Window`. |

**Installation : `npm install` sans objet — aucun paquet NuGet à ajouter.** Le `Chronos.csproj` reste inchangé.
Confirmation par lecture de `src/Chronos/Chronos.csproj` : les 3 seuls `PackageReference` (CommunityToolkit.Mvvm
8.4.2, Microsoft.Extensions.Hosting 8.0.1, System.Security.Cryptography.ProtectedData 8.0.0) couvrent tout.

### Alternatives considérées

| Au lieu de | On pourrait | Arbitrage |
|------------|-------------|-----------|
| `SemaphoreSlim(1,1)` + double-vérification | `Lazy<Task<T>>` / `AsyncLazy` recréé à chaque expiration | Élégant mais l'invalidation (401 ⇒ forcer un nouveau refresh) devient tordue ; le sémaphore est plus lisible et plus testable. |
| Autorité de jeton **maison** | `Microsoft.Identity.Client` / `Duende.AccessTokenManagement` / `IdentityModel` | Ces bibliothèques attendent un fournisseur OIDC conforme (discovery, `.well-known`). Le client public de Claude Code est un flux PKCE artisanal (code renvoyé au format `code#state`, `code=true`) — l'adaptation coûterait plus cher que 120 lignes. Et cela ajouterait une dépendance NuGet, contraire à la sobriété du csproj. |
| `DelegatingHandler` d'authentification (le motif « refresh dans le pipeline HTTP ») | Un handler qui ajoute le Bearer et rejoue sur 401 | Séduisant, mais la seconde source (phase 18, `POST /v1/messages`) veut lire les **en-têtes d'un 429** : un handler qui rejoue masquerait ces réponses. Garder le refresh **hors** du pipeline HTTP. |
| `IHostedService` dédié | Étendre `RefreshOrchestrator` | Rejeté : `RefreshOrchestrator` est une horloge **données** (channel coalescé + FileSystemWatcher). Y greffer le jeton lui donnerait deux raisons de changer et casserait `RefreshOrchestratorTests`. |
| `PeriodicTimer` + `while (await WaitForNextTickAsync)` | `System.Threading.Timer` | Les deux ont un précédent en dépôt. `PeriodicTimer` s'aligne sur `RefreshOrchestrator` et rend le `CancellationToken` naturel. Choix libre ; `Timer` (motif `DesktopUiaPollService`) donne gratuitement un `PollOnce()` public déterministe pour les tests — **c'est le motif à copier** si l'on veut le meilleur rapport testabilité/lignes. |

---

## Architecture Patterns

### Structure de fichiers recommandée

```
src/Chronos/
├── Services/
│   ├── EtatAuthentification.cs        # NOUVEAU — enum neutre (4 valeurs)
│   ├── IAuthStatus.cs                 # NOUVEAU — contrat neutre lu par le VM (état + événement)
│   ├── ChronosTokenAuthority.cs       # NOUVEAU — autorité UNIQUE : sémaphore, backoff, état, rotation
│   ├── TokenRefreshService.cs         # NOUVEAU — IHostedService, tick court, premier tick immédiat
│   ├── ChronosOAuthClient.cs          # MODIFIÉ — RefreshAsync rend une CAUSE, plus un null muet
│   ├── ChronosOAuthUsageProvider.cs   # MODIFIÉ — perd son refresh, consomme l'autorité, signale le 401
│   ├── ChronosOAuthStore.cs           # INCHANGÉ
│   └── DiagnosticService.cs           # MODIFIÉ — paramètre ctor OPTIONNEL (voir Pitfall 9)
├── ViewModels/
│   └── MainViewModel.cs               # MODIFIÉ — +1 param ctor, +2 ObservableProperty, +1 RelayCommand
├── Views/
│   ├── MainWindow.xaml                # MODIFIÉ — la pastille, dernier enfant du Grid racine
│   └── OAuthLogin.cs                  # INCHANGÉ (réutilisé tel quel pour TOK-03)
└── Resources/
    └── DesignTokens.xaml              # MODIFIÉ — 1 token « Alerte » (repli statique)
tests/Chronos.Tests/
├── Fakes/FakeAuthStatus.cs            # NOUVEAU — pour les 5 sites `new MainViewModel(...)`
├── ChronosTokenAuthorityTests.cs      # NOUVEAU — le cœur de la phase
├── TokenRefreshServiceTests.cs        # NOUVEAU
└── ChronosOAuthUsageProviderTests.cs  # NOUVEAU — la classe n'a AUCUN test aujourd'hui (voir Wave 0)
```

### Pattern 1 — Autorité UNIQUE de jeton (répond aux questions ouvertes 1 et 2)

**Quoi :** un seul objet a le droit d'appeler `RefreshAsync` et d'écrire dans `ChronosOAuthStore`. Tous les
consommateurs (provider d'usage actuel, service de fond, sonde d'en-têtes de la phase 18) demandent un jeton
valide à cet objet et ne connaissent jamais le refresh token.

**Quand :** dès qu'il existe plus d'un chemin de code capable de déclencher un refresh — c'est-à-dire
**immédiatement**, puisque TOK-01 en ajoute un second.

**Pourquoi c'est non négociable ici :** le flux fait tourner le refresh token. Deux refresh concurrents
présentant le **même** refresh token ⇒ le second récolte un `invalid_grant` ⇒ si l'on classe `invalid_grant`
comme « déconnecté » (ce que TOK-02 impose), on affiche une **fausse déconnexion** sur un compte parfaitement
sain. Ce n'est pas théorique : c'est exactement le bug ouvert
[anthropics/claude-code#25609](https://github.com/anthropics/claude-code/issues/25609) — « All sessions share
the same credential file, there is no coordination (file lock, mutex, or leader election) during token refresh
… a classic refresh token rotation race condition ». Claude Code lui-même s'y est fait prendre.

**Où vit l'unique autorité :** dans `Services/`, en `Singleton` DI, **injectée** dans `ChronosOAuthUsageProvider`
à la place du couple `(ChronosOAuthStore, ChronosOAuthClient)` qu'il reçoit aujourd'hui (App.xaml.cs:283-287).

**Invariants de l'autorité — à écrire tels quels dans le plan :**
1. Toute la section critique (charger → décider → rafraîchir → **persister** → publier) est sous
   `await _verrou.WaitAsync(ct)` / `finally { _verrou.Release(); }`.
2. **Double-vérification** : après avoir obtenu le sémaphore, re-tester si un autre appelant vient de
   rafraîchir. Sans cela, N appelants en attente déclenchent N rotations en série — donc N-1 échecs.
3. Le nouveau couple de jetons est **persisté avant d'être rendu** à l'appelant (rotation).
4. `Save` peut échouer (disque plein, fichier verrouillé). Dans ce cas les jetons neufs restent **en mémoire**
   dans l'autorité pour que la session courante continue de fonctionner ; l'ancien refresh token, lui, est déjà
   mort côté serveur. Ne jamais laisser une exception de `Save` remonter et faire croire à un échec de refresh.
5. L'autorité ne **`Clear()`** JAMAIS le coffre automatiquement (voir Pitfall 4).
6. Le jeton ne quitte l'autorité que comme `string` d'access token, jamais comme `OAuthTokens`.

### Pattern 2 — Service de fond : tick COURT + prédicat PUR (répond à la question ouverte 1)

**Quoi :** ne pas essayer de programmer un réveil « à `ExpiresAt` moins 5 minutes ». Faire tourner un tick
fixe et court (recommandation : **60 s**, aligné sur le `RefreshIntervalSeconds` par défaut) et, à chaque tick,
évaluer une fonction **pure** :

```csharp
// Chronos.Services — PUR, aucun I/O, testable en 4 lignes.
internal static bool DoitRafraichir(DateTimeOffset? expiresAt, DateTimeOffset now, TimeSpan marge)
    => expiresAt is null || expiresAt.Value - marge <= now;
```

**Pourquoi pas un réveil calculé :** un portable qui dort 8 heures. `Task.Delay(uneJournee)` et les minuteurs
.NET sont fondés sur un temps *écoulé* ; la comparaison d'horloge murale à chaque tick, elle, rattrape
naturellement toute veille, tout changement d'heure et toute dérive. C'est aussi ce qui rend le cas réel de
l'utilisateur (jeton expiré le 2026-07-12, soit **deux mois dans le passé**) trivial : `expiresAt - marge <= now`
est vrai, on rafraîchit.

**Marge :** la marge actuelle est de 5 min (`ChronosOAuthUsageProvider.RefreshMargin`, ligne 30). Recommandation :
la porter à **10-15 min** dans l'autorité. Elle doit être strictement supérieure à la période de tick (60 s),
et suffisamment large pour que le rafraîchissement préventif gagne toujours la course contre le chemin
paresseux — c'est la définition même de « préventif ».

**Premier démarrage avec jeton DÉJÀ expiré (le cas réel) : rafraîchir IMMÉDIATEMENT, ne pas attendre le
premier tick.** Deux précédents en dépôt : `DesktopUiaPollService.StartAsync` utilise `dueTime: TimeSpan.Zero`
(« un premier poll immédiat pour peupler le cache dès le démarrage »), et `RefreshOrchestrator.ExecuteAsync`
écrit `_triggers.Writer.TryWrite(true); // charge initiale immédiate`. Suivre la même convention. Attendre
60 s afficherait une pastille de déconnexion transitoire au lancement — exactement le genre de faux signal
que TOK-02 doit éviter.

**Ordonnancement avec `RefreshOrchestrator` :** les deux services hébergés démarrent quasi simultanément et
la charge initiale des données part immédiatement. C'est sans danger **précisément grâce au Pattern 1** : le
premier arrivé prend le sémaphore et rafraîchit, le second passe la double-vérification et réutilise le jeton
frais. Aucune coordination explicite n'est nécessaire — c'est l'argument décisif en faveur de l'autorité unique.

**Le service de fond ne doit jamais lever.** Copier le `try { … } catch { /* ne jamais remonter */ }` de
`DesktopUiaPollService.PollOnce` (lignes 56-63) : une exception non gérée sur un thread du pool tue la boucle
de fond, donc le rafraîchissement préventif, donc TOK-01.

### Pattern 3 — Classification des échecs (répond à la question ouverte 3)

Modèle d'état recommandé — un **enum neutre** dans `Chronos.Services` (pas un `record` : il n'y a rien à
transporter d'autre, et un enum se binde et se teste sans cérémonie) :

```csharp
namespace Chronos.Services;

/// <summary>État d'authentification de la source exacte OAuth de Chronos, du point de vue de l'overlay.</summary>
public enum EtatAuthentification
{
    /// <summary>Aucun jeton dans le coffre : l'utilisateur ne s'est jamais connecté. (Invite = phase 19, EXA-05.)</summary>
    NonConnecte,
    /// <summary>Jeton valide, chiffres exacts en circulation.</summary>
    Connecte,
    /// <summary>Réseau/serveur momentanément indisponible. Le jeton est peut-être parfaitement bon.</summary>
    HorsLigne,
    /// <summary>Le serveur a REFUSÉ les identifiants. Seule une reconnexion répare.</summary>
    Deconnecte,
}
```

**Table de décision — le cœur de TOK-02.** À reproduire telle quelle dans le plan :

| Signal observé | État | Justification |
|----------------|------|---------------|
| `_store.Load()` renvoie `null` (coffre absent ou illisible) | `NonConnecte` | Rien à rafraîchir ; ce n'est pas une panne. |
| Refresh → HTTP **400** avec `error == "invalid_grant"` | **`Deconnecte`** | RFC 6749 §5.2 : « the authorization grant (including a refresh token) is invalid, expired, revoked… ». Seule une reconnexion répare. |
| Refresh → HTTP **401** / `invalid_client` | **`Deconnecte`** | Identifiants rejetés. |
| Refresh → HTTP **429** | `HorsLigne` | **VÉRIFIÉ le 2026-09-09** : un POST anonyme avec un refresh token bidon sur `https://console.anthropic.com/v1/oauth/token` renvoie `429 {"error":{"type":"rate_limit_error"}}`. Classer un 429 comme « déconnecté » serait une fausse alerte. |
| Refresh → HTTP **5xx** | `HorsLigne` | Panne serveur, pas panne de compte. |
| Refresh → `HttpRequestException` (facultatif : `HttpRequestError` ∈ {`NameResolutionError`, `ConnectionError`, `SecureConnectionError`}) | `HorsLigne` | Wifi coupé, VPN, proxy d'entreprise. |
| Refresh → `TaskCanceledException` / `OperationCanceledException` (timeout 15 s) | `HorsLigne` | — |
| Refresh → HTTP **200** mais corps illisible / champs manquants | **`Deconnecte`** | Cas piégeux : le serveur a **déjà roulé** le refresh token, celui du coffre est mort et le nouveau est perdu. Le rejouer ne peut plus marcher. Voir Pitfall 3. |
| `GET /api/oauth/usage` → **401** avec un access token qui vient d'être rafraîchi | **`Deconnecte`** | Le jeton est frais et refusé : ce n'est pas un problème de fraîcheur. |
| `GET /api/oauth/usage` → **401** avec un access token non rafraîchi récemment | *ne rien conclure* → invalider + un seul réessai | Voir Pattern 3b. |
| `GET /api/oauth/usage` → **403** | `Deconnecte` (libellé distinct souhaitable) | Scope/abonnement ; l'action utile reste « se reconnecter ». |
| `GET /api/oauth/usage` → **429** / **5xx** / réseau / timeout | `HorsLigne` | Comportement déjà présent (`Backoff429`) — ne pas le dégrader. |
| Réponse 2xx exploitée | `Connecte` | Y compris après une période `Deconnecte` : l'état se **déverrouille** au premier succès (critère de succès 3 : « la pastille disparaît dès qu'un chiffre exact est de nouveau obtenu »). |

**Pattern 3b — le 401 sur l'endpoint d'usage, une seule fois.** L'autorité expose
`void InvaliderAccessToken()`. Sur un 401, le provider d'usage appelle cette méthode, redemande un jeton
(ce qui force un refresh) et **rejoue l'appel une seule fois**, sous garde booléenne locale. Si le second
appel est encore 401 ⇒ `Deconnecte`. Sans la garde, on obtient une boucle infinie de refresh + 401 qui
déclenchera un 429 sur le point de terminaison de jeton (cf. le 429 vérifié ci-dessus).

**`ChronosOAuthClient.RefreshAsync` doit cesser de renvoyer un `null` muet.** C'est le prérequis mécanique de
toute cette table. Type de retour recommandé :

```csharp
/// <summary>Issue d'un rafraîchissement, SANS jamais transporter le corps de la réponse (risque de fuite de jeton).</summary>
public enum IssueRafraichissement { Succes, IdentifiantsRejetes, EchecTemporaire }

public sealed record ResultatRafraichissement(IssueRafraichissement Issue, OAuthTokens? Jetons);
```

**Règle de sécurité impérative** : n'exposer dans ce résultat **ni le corps HTTP, ni les en-têtes, ni le
message d'exception**. Seuls le code de statut et la valeur du champ JSON `error` (une chaîne courte du
vocabulaire RFC 6749) peuvent en sortir. Un corps d'erreur peut échoïser la requête, donc le refresh token.
`ExchangeCodeAsync` peut conserver sa signature actuelle (`OAuthTokens?`) : `Views/OAuthLogin.cs` en dépend
et l'échange de code n'a pas de cause à distinguer.

### Pattern 4 — Canal d'état neutre vers le ViewModel (répond à la question ouverte 4)

Les quatre options envisagées dans le brief, arbitrées :

| Option | Verdict |
|--------|---------|
| **(a) Champ dans `UsageSnapshot`** | **Rejeté.** `UsageSnapshot` est un modèle de *donnée d'usage* ; y mettre l'auth le pollue. Pire, il traverse deux `CompositeUsageProvider` imbriqués (App.xaml.cs:299-303) : `Best()` choisit **par fenêtre**, il n'existe aucune règle sensée pour fusionner deux états d'auth issus de branches différentes ; et `LastExactUsageProvider` reconstruit le snapshot (`snap with { … }`) — il faudrait propager le champ à la main partout. Coût élevé, sémantique douteuse. |
| **(b) Événement sur le provider** | Impraticable tel quel : la DI n'expose que `IUsageProvider` = `LastExactUsageProvider` enveloppant les composites ; le VM ne peut pas atteindre `ChronosOAuthUsageProvider` sans une seconde inscription — ce qui revient à (c). |
| **(c) Service d'état partagé injecté** | **RETENU.** |
| (d) Sondage par le VM | Rejeté : la boucle 1 s du VM est `Interpolate`, explicitement **pure et sans I/O** (RAF-03, verrouillé par `MainViewModelTests`). |

**Pourquoi (c) colle à l'architecture existante :** c'est *exactement* le motif déjà en production. Citation
de `RefreshOrchestrator.cs` (lignes 30-32) : « Émis (thread pool) après chaque GetAsync… Le VM s'abonne ICI
et marshalle via IUiDispatcher — décision verrouillée “le service expose l'event” ». Et côté VM
(`MainViewModel.cs`, ligne 243) : « FRONTIÈRE DE THREAD — franchie UNE seule fois (RAF-04) ».

Contrat neutre recommandé :

```csharp
namespace Chronos.Services;

/// <summary>État d'authentification observable, exposé aux ViewModels. Type NEUTRE : aucun type WPF.</summary>
public interface IAuthStatus
{
    EtatAuthentification Etat { get; }
    /// <summary>Émis sur un thread du POOL à chaque transition. L'abonné marshalle lui-même (IUiDispatcher).</summary>
    event EventHandler<EtatAuthentification>? EtatChange;
}
```

**Pureté :** `ServicesLayerPurityTests` inspecte les types de retour/paramètres des méthodes publiques et les
types de propriétés des types de `Chronos.Services` / `Chronos.Models`, et refuse `PresentationCore`,
`PresentationFramework`, `WindowsBase`. Un `enum` du même assembly et un `EventHandler<T>`
(`System.Runtime`) passent — `RefreshOrchestrator.SnapshotChanged` en est la preuve vivante, la garde est
verte depuis la phase 4. **Aucune modification de l'allow-list n'est requise ni permise.**

Côté `MainViewModel` : `IAuthStatus` en **paramètre de constructeur supplémentaire**, abonnement dans le ctor,
et une seule ligne de franchissement :

```csharp
private void SurEtatAuthChange(object? s, EtatAuthentification e) => _ui.Post(() => AppliquerEtatAuth(e));
```

Puis deux `[ObservableProperty]` dérivés (`AfficherPastilleDeconnexion`, `AfficherPastilleHorsLigne`) plutôt
que d'exposer l'enum brut au XAML — cela évite un converter enum→Visibility et reste homogène avec les
`IsStyleArcs` / `IsModeNormal` existants.

### Pattern 5 — Reconnexion en un clic (répond à la question ouverte 5)

`IOAuthLogin` est **déjà** injecté dans `MainViewModel` (champ `_oauthLogin`, ligne 32) et
`Views/OAuthLogin.cs` réalise déjà tout le parcours. TOK-03 n'exige donc **aucune écriture de dialogue**.
La frontière est déjà propre : `Services/IOAuthLogin.cs` est neutre, l'implémentation WPF vit dans `Views/`.

**Ne PAS binder la pastille sur `LoginClaudeCommand`.** Cette commande **bascule** (`MainViewModel.cs:363`) :
`if (_oauthLogin.IsLoggedIn) _oauthLogin.Logout(); else await LoginAsync();`. Or `IsLoggedIn` vaut
`_store.Exists` — la simple **présence du fichier**. Avec le jeton expiré du 2026-07-12, `IsLoggedIn` est
`true` : un clic sur la pastille **supprimerait le coffre** au lieu de reconnecter. Créer une commande
distincte, mono-directionnelle :

```csharp
/// <summary>TOK-03 : relance le parcours de login depuis la pastille de déconnexion. Ne déconnecte JAMAIS
/// (contrairement à LoginClaudeCommand, qui bascule) : la pastille n'a qu'un sens, « répare-moi ».</summary>
[RelayCommand]
private async Task ReconnecterAsync()
{
    var ok = await _oauthLogin.LoginAsync();
    IsLoggedIn = _oauthLogin.IsLoggedIn;
    if (!ok) return;
    _authStatus.ReinitialiserApresLogin();  // relâche le verrou « Deconnecte » ET le backoff
    _orchestrator.RequestRefresh();         // affichage rafraîchi sans attendre le tick de 60 s
}
```

**Réponse explicite à « le prochain `GetAsync` suffit-il ? » : NON, pour deux raisons cumulatives.**
1. `RefreshOrchestrator` tourne à `RefreshIntervalSeconds` (défaut 60 s) : la pastille resterait affichée
   jusqu'à une minute après un login réussi. `RequestRefresh()` existe déjà exactement pour ça — son XML-doc
   dit « ex. après bascule de la source exacte (portillon OAuth) **ou après un login** ».
2. Surtout : l'autorité **verrouille** l'état `Deconnecte` et pose un `_prochainEssai` de backoff. Sans
   réinitialisation explicite, le jeton tout neuf ne serait pas utilisé avant l'expiration du backoff, et la
   pastille survivrait à sa propre réparation. **C'est le principal risque fonctionnel de la phase**, et il
   n'est visible que si l'on écrit le test « login réussi ⇒ pastille disparaît **au tour suivant** ».

### Anti-patterns à éviter

- **Deux rafraîchisseurs.** Ajouter le service de fond en laissant le refresh paresseux de
  `ChronosOAuthUsageProvider` en place. C'est la faute qui produit une fausse déconnexion — le bug
  claude-code#25609 en personne.
- **Cadence adaptative via `PeriodicTimer.Period`.** Techniquement disponible en .NET 8 (vérifié), mais elle
  fait dépendre l'exactitude de TOK-01 d'un ordonnancement d'horloge non déterministe, difficile à tester,
  et fragile au sommeil de la machine. Le tick fixe + prédicat pur est plus court, plus sûr et testable sans
  `Task.Delay` dans les tests.
- **`lock` autour d'un `await`.** Interdit par le compilateur (CS1996) — ne pas essayer de contourner avec un
  `Monitor` manuel. `SemaphoreSlim.WaitAsync` est la primitive.
- **Effacer le coffre sur un échec de refresh.** Voir Pitfall 4.
- **Journaliser la cause d'un échec de refresh en incluant le corps de la réponse.** Voir la règle de
  sécurité du Pattern 3.
- **Faire porter la pastille par un `Popup` / une seconde fenêtre.** L'overlay fait 170 px, sans barre de
  titre, `ShowActivated=False` : une fenêtre auxiliaire hériterait des problèmes de focus et de placement
  multi-écran déjà réglés pour `SettingsWindow`. La pastille est un élément du `Grid` racine, point.

---

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser à la place | Pourquoi |
|----------|-------------------|---------------------|----------|
| Sérialiser un refresh asynchrone | Un `bool _enCours` + `SpinWait`, ou un `lock` + `.Result` | `SemaphoreSlim(1,1)` + `await WaitAsync` + `finally Release` | `.Result` sur le thread UI = interblocage ; un `bool` non atomique laisse passer deux rotations. |
| Décider s'il faut rafraîchir | Une arithmétique de dates dispersée dans le service ET dans le provider | **Une** fonction `static` pure `DoitRafraichir(expiresAt, now, marge)` | Une seule vérité, testable en 4 `[Theory]`, et le bug « marge d'un côté, pas de l'autre » devient impossible. |
| Distinguer réseau / auth | Parser `ex.Message` | Le code de statut HTTP + le champ JSON `error` + (facultatif) `HttpRequestException.HttpRequestError` | Les messages d'exception sont **localisés** et changent de version en version. |
| Horloge testable | `DateTimeOffset.UtcNow` en dur | `IClock` (déjà là) + `FakeClock` (déjà là) | Tout le dépôt le fait ; sans ça, aucun test d'expiration n'est déterministe. |
| Fabriquer des réponses HTTP de test | Un vrai `HttpClient` vers un serveur local | `FakeHttpMessageHandler` (déjà là : `.Json(status, body)`, `.Throws(ex)`, `SendCount`, `LastRequest`) | Il couvre déjà les trois cas dont la phase a besoin. Et surtout : **c'est la seule façon de respecter l'interdiction absolue de toucher au vrai refresh token.** |
| Chiffrement du coffre | Quoi que ce soit | `ChronosOAuthStore` (DPAPI `CurrentUser`, écriture atomique tmp+Move) | Déjà fait, déjà testé (`ChronosOAuthStoreTests`). N'y toucher sous aucun prétexte. |
| Marshalling vers le thread UI | `Application.Current.Dispatcher` depuis un service | `IUiDispatcher.Post` (déjà là) | Un `Dispatcher` dans `Services/` fait échouer `ServicesLayerPurityTests` sur-le-champ. |
| Convertisseur bool→Visibility | Un converter maison | `{StaticResource BoolToVis}` (déjà dans `DesignTokens.xaml`) | Déjà présent et utilisé par tout `MainWindow.xaml`. |

**Idée maîtresse :** cette phase n'a besoin d'**aucune** brique nouvelle. Tout ce qu'elle ajoute, c'est un
point de sérialisation, une table de décision et un canal d'état. Chaque fois que le plan est tenté d'écrire
une infrastructure, c'est le signe qu'un service existant a été oublié.

---

## Runtime State Inventory

La phase n'est ni un renommage ni une migration, mais elle **touche de l'état persistant hors dépôt** — ce
tableau est donc pertinent et a été renseigné explicitement.

| Catégorie | Constaté | Action requise |
|-----------|----------|----------------|
| Données stockées | `%APPDATA%\Chronos\oauth.dat` — coffre DPAPI contenant `OAuthTokens(AccessToken, RefreshToken, ExpiresAt)`. Sur la machine réelle : présent, `ExpiresAt` = 2026-07-12 (expiré). | **Aucune migration de format.** Le schéma JSON de `OAuthTokens` ne change pas. Le premier rafraîchissement réussi le réécrira naturellement. **Ne jamais le supprimer par code** (Pitfall 4). |
| Données stockées | `%APPDATA%\Chronos\last-exact.json` (phase 16), `%APPDATA%\Chronos\usage.json`, `%APPDATA%\Chronos\settings.json` | Aucune. La phase 17 ne modifie pas leur schéma. Si un réglage de cadence de refresh devait être ajouté à `ChronosSettings`, il serait rétro-compatible (System.Text.Json ignore les membres non mappés, prouvé en phase 16) — mais **c'est déconseillé** : la cadence n'a pas besoin d'être réglable. |
| Config de service vivant | Aucune. Chronos ne dépose aucune configuration côté serveur Anthropic. `~/.claude/settings.json` (hooks + statusLine) n'est pas touché par cette phase. | Aucune. |
| État enregistré dans l'OS | Raccourci `shell:startup` (`AutostartService`) — inchangé. Aucun service Windows, aucune tâche planifiée. | Aucune. |
| Secrets / variables d'env | Le refresh token vit **uniquement** dans `oauth.dat` (DPAPI `CurrentUser`). Aucune variable d'environnement, aucun fichier `.env`, aucun secret en dépôt. Le `ClientId` `9d1c250a-…` est un identifiant de **client public** (constante publique de `ChronosOAuthClient`), ce n'est pas un secret. | Aucune. **Interdit pendant la recherche et le développement : déchiffrer, afficher ou faire tourner ce refresh token.** Respecté : aucune lecture d'`oauth.dat` n'a été faite. |
| Artefacts de build | `bin/`/`obj/` des deux projets ; l'exe publié `Chronos-v2.8.1.exe`. Le `<Version>` du csproj est à 2.8.1. | Aucune pendant la phase. La livraison ultérieure incrémentera la version (mémoire projet « versionnage de l'exe »). Hors périmètre du plan. |

**Vérification réseau menée (sans aucun identifiant réel), 2026-09-09 :**
`POST https://console.anthropic.com/v1/oauth/token` avec un corps vide → **400** ; avec un
`refresh_token` **bidon** → **429 `rate_limit_error`**. Le point de terminaison utilisé par
`ChronosOAuthClient` est donc **toujours vivant et ne redirige pas** — inutile de migrer vers
`platform.claude.com` (qui répond aussi, mais rien n'impose d'en changer). Le refresh token de l'utilisateur
n'a **jamais** été lu, déchiffré, ni envoyé.

---

## Common Pitfalls

### Pitfall 1 — Le portable qui dort (TOK-01)
**Ce qui casse :** un rafraîchissement programmé par délai (`Task.Delay(jusquALExpiration)`) ne se déclenche
pas à l'heure murale après une mise en veille de plusieurs heures. L'utilisateur rouvre le capot, le jeton est
mort, l'overlay ment.
**Racine :** les minuteurs .NET raisonnent en temps écoulé, pas en heure murale.
**Parade :** tick court fixe + comparaison `expiresAt - marge <= _clock.UtcNow` (Pattern 2).
**Signe avant-coureur :** le mot `Delay` avec une durée calculée depuis `ExpiresAt` dans le plan.

### Pitfall 2 — Le 401 muet ressuscité par le cache
**Ce qui casse :** même avec la pastille implémentée, l'utilisateur peut ne rien voir. `ChronosOAuthUsageProvider`
sert `ServeCachedOr(...)` (ligne 121) tant que le cache a moins de `CacheUsable = 15 min`, et
`LastExactUsageProvider` (phase 16) rebouche ensuite les fenêtres `Unavailable` depuis `last-exact.json`
**sans limite d'âge** (celle-ci est la phase 19). Résultat : le cadran affiche des chiffres plausibles alors
que l'authentification est morte.
**Racine :** l'état d'auth ne doit surtout PAS être déduit de l'apparence du snapshot.
**Parade :** la pastille est pilotée par le canal `IAuthStatus`, **indépendamment** du contenu du snapshot.
C'est la justification profonde du Pattern 4 (option (a) rejetée).
**Signe avant-coureur :** un test qui vérifie la pastille via `DataUnavailable` — ce serait la mauvaise sonde.

### Pitfall 3 — Le refresh token perdu en vol
**Ce qui casse :** HTTP 200, mais la réponse est illisible (JSON tronqué, `refresh_token` absent, coupure en
cours de lecture du flux). Côté serveur, la rotation a **déjà eu lieu** : le refresh token du coffre est mort
et le nouveau n'a jamais été lu. Toute tentative ultérieure avec l'ancien renverra `invalid_grant` —
indéfiniment.
**Racine :** la rotation est un effet de bord serveur, atomique de son côté, pas du nôtre.
**Parade :** classer « 200 + corps inexploitable » en **`Deconnecte`** (et non en `EchecTemporaire`) — c'est
la seule classification honnête, et elle mène l'utilisateur vers la seule action qui répare. Corollaire :
`ChronosOAuthClient.PostTokenAsync` doit lire le flux **entièrement** avant de conclure, et son `catch`
actuel (ligne 119) doit distinguer « échec avant l'envoi » de « échec après une réponse 200 ».
**Signe avant-coureur :** un `catch { return null; }` unique qui couvre à la fois `SendAsync` et le parsing.

### Pitfall 4 — Le `Clear()` automatique qui détruit un jeton récupérable
**Ce qui casse :** « refresh refusé ⇒ le coffre est mauvais ⇒ `_store.Clear()` ». Un faux positif serveur
efface alors définitivement le login. Ce faux positif est **documenté** :
[anthropics/claude-code#54443](https://github.com/anthropics/claude-code/issues/54443) — « OAuth sessions
rejected by the server before the locally stored expiresAt time. Claude Code then attempts OAuth refresh,
[which] returns HTTP 400 ».
**Parade :** l'autorité **ne supprime jamais** `oauth.dat`. Elle passe en `Deconnecte` et attend un login.
`LoginAsync` écrase le coffre via `Save` — c'est déjà le comportement de `Views/OAuthLogin.cs` (ligne 106).
La seule suppression légitime reste `Logout()`, déclenchée par l'utilisateur.
**Signe avant-coureur :** toute occurrence de `.Clear()` dans le nouveau code.

### Pitfall 5 — Marteler le point de terminaison de jeton
**Ce qui casse :** hors ligne, un tick de 60 s tenterait 1 440 refresh par jour. Sur un `invalid_grant`
définitif, il en tenterait autant pour rien. Conséquence mesurée : le point de terminaison **rate-limite**
(429 vérifié le 2026-09-09), donc on transforme une panne bénigne en blocage.
**Parade :** deux politiques distinctes portées par l'autorité :
`EchecTemporaire` → `_prochainEssai = now + backoff` (départ ~2-5 min, doublement plafonné à ~30 min,
remis à zéro au premier succès) ; `IdentifiantsRejetes` → **aucun réessai automatique**, on attend un login.
**Signe avant-coureur :** un service de fond qui appelle l'autorité sans consulter de fenêtre de réessai.

### Pitfall 6 — Crier « reconnecte-toi » quand c'est le wifi
**Ce qui casse :** exactement ce que le brief interdit. Un `HttpRequestException` devient une pastille rouge,
l'utilisateur relance un login inutile, échoue (pas de réseau), et perd confiance dans le signal.
**Parade :** la table du Pattern 3, et **deux états visuels distincts** : `Deconnecte` = actionnable (ambre,
cliquable, « Connexion perdue — clique pour te reconnecter ») ; `HorsLigne` = informatif (gris, non cliquable
ou sans action, « Hors ligne — chiffres non rafraîchis »).
**Signe avant-coureur :** un seul booléen `PastilleVisible` dans le VM.

### Pitfall 7 — `IsLoggedIn` ment
**Ce qui casse :** `IOAuthLogin.IsLoggedIn => _store.Exists` (`Views/OAuthLogin.cs:26`) ne teste que
**l'existence du fichier**. Avec le jeton expiré de l'utilisateur, il vaut `true`. Deux conséquences
concrètes : (a) `LoginClaudeCommand` **déconnecterait** au lieu de reconnecter si on lui branchait la
pastille ; (b) le réglage/menu « Se connecter à Claude » affiche « connecté » depuis deux mois alors que
l'authentification est morte — c'est une partie du silence que TOK-02 doit briser.
**Parade :** commande `ReconnecterCommand` dédiée (Pattern 5), et — recommandé — refléter `EtatAuthentification`
plutôt que `IsLoggedIn` dans le libellé des réglages.
**Signe avant-coureur :** `Command="{Binding LoginClaudeCommand}"` sur la pastille.

### Pitfall 8 — Hit-testing sur fenêtre transparente : le mythe du `#01000000`
**Ce qui casse (dans l'autre sens) :** le `CLAUDE.md` du projet indique qu'une zone cliquable doit avoir un
`Background` « même quasi-transparent `#01000000` ». En WPF, la règle exacte est plus simple : un pinceau
`Transparent` **est** hit-testable ; seul `{x:Null}` (l'absence de pinceau) ne l'est pas. **Preuve en
dépôt** : `MainWindow.xaml:132`, `<Ellipse x:Name="CentreHit" … Fill="Transparent" Cursor="Hand"
MouseLeftButtonDown="CentreHit_MouseLeftButtonDown"/>` fonctionne en production depuis la v1.3 sur une
fenêtre `AllowsTransparency=True`. Le `#01000000` est le contournement d'autres frameworks ; il est inutile ici.
**Parade :** donner à la pastille un `Background="Transparent"` (ou un fond coloré, ce qui suffit a fortiori)
et **jamais** `{x:Null}`.

### Pitfall 9 — Le `DragMove` parasite
**Ce qui casse :** `MainWindow` s'abonne à `MouseLeftButtonDown` au niveau **fenêtre** et appelle `DragMove()`
(code-behind, ligne 42/58-63), qui **bloque** jusqu'au relâchement. Un clic sur la pastille qui remonterait
jusque-là déplacerait l'overlay au lieu de lancer le login.
**Parade :** utiliser un `Button` — `ButtonBase` marque `MouseLeftButtonDown` comme `Handled`, et le handler
de fenêtre (attaché sans `handledEventsToo`) n'est donc pas invoqué. Si l'on préfère une `Ellipse` nue, il
faut reproduire explicitement `e.Handled = true` comme le fait `CentreHit_MouseLeftButtonDown` (ligne 70).
**Signe avant-coureur :** une pastille en `Ellipse` + `MouseLeftButtonDown` sans `e.Handled = true`.

### Pitfall 10 — Le rayon de souffle des signatures de constructeur
**Ce qui casse :** ajouter un paramètre à `DiagnosticService` casse **8 sites de construction** (1 en prod,
7 en tests, répartis sur 5 fichiers de tests). Ajouter un paramètre à `MainViewModel` en casse **5**.
**Parade :** pour `DiagnosticService`, ajouter un paramètre **optionnel en dernière position**
(`IAuthStatus? authStatus = null`) → **zéro** site cassé, la DI passe le vrai service. Pour `MainViewModel`,
le paramètre obligatoire est acceptable (5 sites, correction mécanique) ; c'est le prix d'une dépendance
réellement requise et la garde `CompositionRootTests` le vérifie. Détail complet en § Inventaire des tests.

---

## Code Examples

> Les extraits ci-dessous sont des **squelettes de conception** dérivés du code réel du dépôt et des API
> vérifiées ; ils ne sont pas copiés d'une documentation externe. Ils montrent la forme attendue, pas
> l'implémentation finale.

### 1. Autorité unique : sérialisation + double-vérification + rotation persistée

```csharp
// Chronos.Services — type NEUTRE (aucun type WPF). Singleton DI.
public sealed class ChronosTokenAuthority : IAuthStatus, IDisposable
{
    private static readonly TimeSpan Marge = TimeSpan.FromMinutes(12);   // > période de tick (60 s)
    private static readonly TimeSpan BackoffInitial = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan BackoffMax = TimeSpan.FromMinutes(30);

    private readonly SemaphoreSlim _verrou = new(1, 1);
    private readonly ChronosOAuthStore _coffre;
    private readonly ChronosOAuthClient _client;
    private readonly IClock _horloge;

    private OAuthTokens? _jetons;            // copie mémoire (survit à un Save en échec)
    private bool _forcerRafraichissement;    // posé par InvaliderAccessToken() après un 401
    private DateTimeOffset _prochainEssai;
    private TimeSpan _backoff = BackoffInitial;

    public EtatAuthentification Etat { get; private set; } = EtatAuthentification.NonConnecte;
    public event EventHandler<EtatAuthentification>? EtatChange;   // émis sur un thread du POOL

    /// <summary>Rend un access token VALIDE, en rafraîchissant si nécessaire. null si indisponible.
    /// SÉCURITÉ : c'est la SEULE sortie d'un jeton hors de cette classe, et jamais le refresh token.</summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        await _verrou.WaitAsync(ct);
        try
        {
            var now = _horloge.UtcNow;
            _jetons ??= _coffre.Load();
            if (_jetons is null) { Publier(EtatAuthentification.NonConnecte); return null; }

            // DOUBLE-VÉRIFICATION : un appelant concurrent vient peut-être de rafraîchir pendant l'attente.
            if (!_forcerRafraichissement && !DoitRafraichir(_jetons.ExpiresAt, now, Marge))
                return _jetons.AccessToken;

            // Fenêtre de réessai : ne jamais marteler le point de terminaison (429 constaté).
            if (now < _prochainEssai) return null;

            var res = await _client.RefreshAsync(_jetons.RefreshToken, ct);
            switch (res.Issue)
            {
                case IssueRafraichissement.Succes:
                    _jetons = res.Jetons!;
                    // ROTATION : persister AVANT toute autre action. Un Save en échec ne doit pas
                    // faire croire à un échec de refresh : les jetons neufs restent en mémoire.
                    try { _coffre.Save(_jetons); } catch { /* dégradation : session courante préservée */ }
                    _forcerRafraichissement = false;
                    _backoff = BackoffInitial; _prochainEssai = default;
                    Publier(EtatAuthentification.Connecte);
                    return _jetons.AccessToken;

                case IssueRafraichissement.IdentifiantsRejetes:
                    // AUCUN réessai automatique, et surtout AUCUN _coffre.Clear() (voir Pitfall 4).
                    Publier(EtatAuthentification.Deconnecte);
                    return null;

                default: // EchecTemporaire : réseau, 429, 5xx, timeout
                    _prochainEssai = now + _backoff;
                    _backoff = _backoff < BackoffMax ? _backoff + _backoff : BackoffMax;
                    Publier(EtatAuthentification.HorsLigne);
                    return null;
            }
        }
        finally { _verrou.Release(); }
    }

    /// <summary>Après un 401 sur l'endpoint d'usage : le prochain appel forcera un rafraîchissement.</summary>
    public void InvaliderAccessToken() => _forcerRafraichissement = true;

    /// <summary>TOK-03 : après un login réussi, relâcher le verrou « Deconnecte » ET le backoff,
    /// sinon le jeton tout neuf ne sera pas utilisé et la pastille survivrait à sa réparation.</summary>
    public void ReinitialiserApresLogin()
    {
        _jetons = null; _forcerRafraichissement = false;
        _backoff = BackoffInitial; _prochainEssai = default;
        Publier(EtatAuthentification.NonConnecte);   // sera relevé au premier GetAccessTokenAsync
    }

    /// <summary>Un 2xx exploité déverrouille l'état (critère de succès 3).</summary>
    public void SignalerSucces() => Publier(EtatAuthentification.Connecte);
    public void SignalerRefusServeur() => Publier(EtatAuthentification.Deconnecte);

    internal static bool DoitRafraichir(DateTimeOffset? expiresAt, DateTimeOffset now, TimeSpan marge)
        => expiresAt is null || expiresAt.Value - marge <= now;   // PUR

    private void Publier(EtatAuthentification e)
    {
        if (Etat == e) return;                 // n'émettre que sur TRANSITION (pas 1 event/minute)
        Etat = e;
        EtatChange?.Invoke(this, e);
    }

    public void Dispose() => _verrou.Dispose();
}
```

### 2. Service de fond : premier tick immédiat, ne lève jamais

```csharp
// Motif calqué sur DesktopUiaPollService (Timer .NET + méthode publique déterministe pour les tests).
public sealed class TokenRefreshService : IHostedService, IDisposable
{
    private static readonly TimeSpan Periode = TimeSpan.FromSeconds(60);
    private readonly ChronosTokenAuthority _autorite;
    private Timer? _timer;

    public Task StartAsync(CancellationToken ct)
    {
        // dueTime ZÉRO : le cas réel de l'utilisateur est un jeton DÉJÀ expiré (2026-07-12).
        // Attendre 60 s afficherait une pastille de déconnexion transitoire au lancement.
        _timer ??= new Timer(_ => _ = TickAsync(), null, TimeSpan.Zero, Periode);
        return Task.CompletedTask;
    }

    /// <summary>PUBLIC pour un test déterministe d'un tick. Ne LÈVE JAMAIS (tuerait la boucle de fond).</summary>
    public async Task TickAsync()
    {
        try { await _autorite.GetAccessTokenAsync(); }   // l'autorité décide seule s'il faut rafraîchir
        catch { /* dégradation silencieuse */ }
    }

    public Task StopAsync(CancellationToken ct) { _timer?.Dispose(); _timer = null; return Task.CompletedTask; }
    public void Dispose() { _timer?.Dispose(); _timer = null; }
}
```

### 3. Client OAuth : rendre la CAUSE, sans jamais transporter le corps

```csharp
// Remplace le `catch { return null; }` de ChronosOAuthClient.PostTokenAsync (ligne 119).
public async Task<ResultatRafraichissement> RefreshAsync(string refreshToken, CancellationToken ct = default)
{
    var body = new { grant_type = "refresh_token", refresh_token = refreshToken, client_id = ClientId };
    HttpResponseMessage? resp = null;
    try
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { /* … */ };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        resp = await _http.SendAsync(req, cts.Token);

        var code = (int)resp.StatusCode;
        if (code is 400 or 401)
            // RFC 6749 §5.2 : invalid_grant / invalid_client => seule une reconnexion répare.
            // SÉCURITÉ : on ne remonte QUE le code d'erreur, jamais le corps (il peut échoïser la requête).
            return new(IssueRafraichissement.IdentifiantsRejetes, null);
        if (!resp.IsSuccessStatusCode)
            return new(IssueRafraichissement.EchecTemporaire, null);   // 429 (constaté), 5xx…

        var jetons = await LireJetonsAsync(resp, cts.Token);
        // 200 + corps inexploitable : le serveur a DÉJÀ roulé le refresh token, l'ancien est mort
        // et le nouveau est perdu. Réessayer est vain (Pitfall 3) => déconnexion honnête.
        return jetons is null
            ? new(IssueRafraichissement.IdentifiantsRejetes, null)
            : new(IssueRafraichissement.Succes, jetons);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                 or OperationCanceledException or JsonException or IOException)
    {
        // Si une réponse 200 avait déjà été reçue, la rotation a pu avoir lieu -> même raisonnement.
        return resp is { IsSuccessStatusCode: true }
            ? new(IssueRafraichissement.IdentifiantsRejetes, null)
            : new(IssueRafraichissement.EchecTemporaire, null);
    }
    finally { resp?.Dispose(); }
}
```

### 4. Franchissement de thread côté ViewModel (motif RAF-04 existant)

```csharp
// MainViewModel : abonnement dans le ctor, EXACTEMENT comme orchestrator.SnapshotChanged (ligne 239).
_authStatus = authStatus;
authStatus.EtatChange += SurEtatAuthChange;
AppliquerEtatAuth(authStatus.Etat);   // état initial, sans attendre la première transition

// FRONTIÈRE DE THREAD — franchie via IUiDispatcher.Post, comme OnSnapshotChanged (ligne 243).
private void SurEtatAuthChange(object? s, EtatAuthentification e) => _ui.Post(() => AppliquerEtatAuth(e));

private void AppliquerEtatAuth(EtatAuthentification e)
{
    // Deux booléens plutôt qu'un enum bindé : cohérent avec IsStyleArcs / IsModeNormal, zéro converter.
    // NonConnecte n'allume RIEN en phase 17 : l'invite « jamais connecté » est EXA-05, phase 19.
    AfficherPastilleDeconnexion = e == EtatAuthentification.Deconnecte;
    AfficherPastilleHorsLigne   = e == EtatAuthentification.HorsLigne;
}
```

### 5. La pastille dans `MainWindow.xaml`

```xml
<!-- DERNIER enfant du Grid racine (après CentreHit) => au-dessus de TOUS les styles de cadran,
     donc unique pour les 5 styles (Anneaux/Braises/Fusible/Marée/Volets) et les 2 modes.
     Géométrie : l'anneau le plus externe atteint R≈71,5 (TickRing Radius=68 + TickLength=7) ;
     à 45° il culmine vers (136,136) dans la boîte 170x170 => le coin bas-droite est libre.
     Bouton (pas Ellipse nue) : ButtonBase marque MouseLeftButtonDown Handled => aucun DragMove
     parasite (Pitfall 9). Background non-null => hit-testable sur fenêtre transparente (Pitfall 8). -->
<Button HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,6,6"
        Width="14" Height="14" Background="Transparent" BorderThickness="0" Cursor="Hand"
        ToolTip="Connexion à Claude perdue — cliquer pour se reconnecter"
        Command="{Binding ReconnecterCommand}"
        Visibility="{Binding AfficherPastilleDeconnexion, Converter={StaticResource BoolToVis}}">
    <Button.Template>
        <ControlTemplate TargetType="Button">
            <Grid Background="Transparent">
                <Ellipse Fill="{DynamicResource Alerte}"/>
            </Grid>
        </ControlTemplate>
    </Button.Template>
</Button>

<!-- Hors ligne : informatif, gris, NON actionnable. Ne crie jamais « reconnecte-toi » (Pitfall 6). -->
<Ellipse HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,6,6"
         Width="10" Height="10" Fill="{DynamicResource TexteSecondaire}"
         ToolTip="Hors ligne — chiffres non rafraîchis"
         Visibility="{Binding AfficherPastilleHorsLigne, Converter={StaticResource BoolToVis}}"/>
```

---

## Surface visuelle de la pastille (question ouverte 6)

**Où exactement.** Dernier enfant du `<Grid MouseRightButtonUp="OnRightClick">` racine de `MainWindow.xaml`
(ligne 21), donc **après** l'`Ellipse CentreHit` (ligne 132). Cette position :
- couvre les **5 styles** de cadran d'un coup (les 4 nouveaux sont des `Viewbox` frères, lignes 112-127) et
  les **2 modes** (Normal/Étendu) sans duplication ;
- ne touche pas les `UserControl` de `Views/Cadrans/`, donc n'affecte pas la galerie `--cadrans` ;
- reste dans les 170×170 : aucun changement de taille de fenêtre, donc **placement, ancrage et
  `OverlayController.RestorePlacement` intacts** (et `OverlayWindowConfigTests` reste vert).

**Géométrie vérifiée.** Centre = (85,85). L'élément le plus externe du style Anneaux est
`TickRing Radius="68" TickLength="7"` (ligne 59) → rayon extrême ≈ 71,5 → à 45° il atteint ≈ (135,6 ; 135,6).
Les styles en `Viewbox` sont mis à l'échelle avec `Margin="4"`/`"6"` sur un contenu de 170 (ex. `EmberRingControl
Radius="66"`), donc encore plus rentrés. Une pastille de 14 px ancrée bas-droite avec `Margin="0,0,6,6"`
occupe ≈ (150..164) : **franchement à l'écart de toute géométrie de cadran**, dans les deux modes et les
cinq styles. (Le coin bas-droite est préférable au haut-droite : l'overlay s'ancre le plus souvent en haut à
droite de l'écran, le bas-droite est donc le coin le moins susceptible d'être mordu par un bord d'écran.)

**Tokens de design.** `Resources/DesignTokens.xaml` (26 lignes) ne contient **aucun** token d'alerte. Deux
mouvements, tous deux minimes :
1. `TexteSecondaire` (`#A9A8B2`) est déjà documenté comme « badges/mentions annexes (estimée, épuisé,
   **indisponible**, périmée) » → **réutiliser tel quel pour « hors ligne »**. Aucun nouveau token.
2. Pour « déconnecté », ajouter un token `Alerte` :
   - repli statique dans `DesignTokens.xaml` (pour que la vue reste autonome hors `Application` — c'est
     l'exigence explicite du commentaire des lignes 17-19 de `MainWindow.xaml`) ;
   - **et** une entrée `["Alerte"] = Frozen(RampAmber)` dans `ChronosTheme.BrushTokens()`, pour que la
     pastille suive les 9 thèmes. Précédent exact : `SessionBrushTokens()` fait déjà
     `["SessAttention"] = Frozen(RampAmber)` pour l'autre overlay.
   Mécanique confirmée : `MainWindow.ApplyThemeBrushes` écrit `Resources[kv.Key] = kv.Value` (ligne 116-120)
   et les `{DynamicResource …}` du cadran se mettent à jour instantanément.
   **Ambre plutôt que rouge** : cohérent avec la mémoire projet (« pastille session ORANGE pour les deux
   états d'attente ») et avec la rampe d'usage, où le rouge signifie déjà « quota épuisé ». Un rouge pour
   l'auth entrerait en collision sémantique avec la rampe.

**Ce qu'il ne faut PAS faire ici :** toucher à la `UniformGrid Columns="2"` des réglages (le trou visuel des
3 boutons) — explicitement **différé en phase 20** par le CONTEXT. Si le plan veut aussi un libellé
« Reconnexion » dans les réglages, il doit le placer sans réorganiser cette grille.

---

## State of the Art

| Approche ancienne (code actuel) | Approche cible | Pourquoi ça change |
|---------------------------------|----------------|--------------------|
| Refresh **paresseux** dans `GetAsync` (`ChronosOAuthUsageProvider`, lignes 60-75) | Autorité unique + service de fond préventif | TOK-01 ; et un exe laissé tourner plusieurs jours doit continuer de recevoir des chiffres exacts. |
| `RefreshAsync` → `OAuthTokens?` avec `catch { return null; }` | `ResultatRafraichissement(Issue, Jetons)` | Sans cause, TOK-02 est **impossible** : on ne peut pas distinguer wifi coupé et compte révoqué. |
| Échec de refresh → « on tente quand même l'appel » (commentaire ligne 66) | Échec définitif → on n'appelle pas, on publie `Deconnecte` | L'appel avec un jeton mort ne produit qu'un 401 de plus et consomme du rate-limit. |
| Tout échec → `_nextAllowedCall = now + MinInterval` (uniforme) | Backoff **différencié** temporaire / définitif | Ne pas marteler ; ne pas réessayer l'irréparable. |
| Aucun canal d'état vers l'UI | `IAuthStatus` + `event` + `IUiDispatcher.Post` | Motif déjà éprouvé par `RefreshOrchestrator.SnapshotChanged`. |
| `IsLoggedIn == File.Exists` comme vérité affichée | `EtatAuthentification` comme vérité affichée | L'existence d'un fichier n'a jamais signifié « authentifié ». |

**Périmé / à ne pas ressusciter :**
- Le refresh dans `ChronosOAuthUsageProvider` — il doit **disparaître**, pas coexister.
- `ClaudeOAuthUsageProvider` + `GatedOAuthUsageProvider` (voie du coffre de l'app bureau) : le CONTEXT le dit,
  le coffre est **absent** sur cette machine. **Ne rien y investir, ne pas casser leurs tests.**

---

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|------------|-------------|-----------|---------|-------|
| .NET SDK | build/test | ✓ | 10.0.201 | — (compile bien la cible `net8.0-windows`) |
| Runtime `Microsoft.WindowsDesktop.App` 8.x | exécution des tests `net8.0-windows` | ✓ | 8.0.25 (et 10.0.0 / 10.0.5) | — |
| xUnit + Xunit.StaFact | suite de tests | ✓ | 2.9.2 / 1.1.11 | — |
| `console.anthropic.com/v1/oauth/token` | rafraîchissement en PROD | ✓ (vivant, pas de redirection ; répond 400/429 aux sondes anonymes) | — | Aucun : hors ligne ⇒ état `HorsLigne`, jamais un crash |
| `api.anthropic.com/api/oauth/usage` | source exacte primaire | non re-sondé (déjà diagnostiqué : **401** avec le jeton actuel) | — | `LastExactUsageProvider` (phase 16) |
| Coffre de l'app bureau Claude (`%APPDATA%/Claude`) | `ClaudeOAuthUsageProvider` | ✗ (absent sur cette machine, cf. CONTEXT) | — | Sans objet : hors périmètre de la phase |
| Jeton OAuth réel de l'utilisateur | *rien* | présent mais **INTERDIT D'USAGE** en développement/test | expiré 2026-07-12 | `FakeHttpMessageHandler` + `FakeClock` pour 100 % des vérifications |

**Dépendances manquantes bloquantes :** aucune.
**Dépendances manquantes avec repli :** coffre de l'app bureau Claude (hors périmètre) ; endpoint d'usage
en 401 (c'est précisément le symptôme que la phase rend visible, pas un blocage de développement).

---

## Validation Architecture

`workflow.nyquist_validation = true` dans `.planning/config.json` → section incluse.

### Cadre de test

| Propriété | Valeur |
|-----------|--------|
| Framework | xUnit 2.9.2 + `Xunit.StaFact` 1.1.11 (`[WpfFact]` = STA), `Microsoft.NET.Test.Sdk` 17.11.1 |
| Fichier de config | `tests/Chronos.Tests/Chronos.Tests.csproj` (`net8.0-windows`, `UseWPF=true`) — pas de `xunit.runner.json` |
| Commande rapide | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~ChronosTokenAuthorityTests"` |
| Commande suite complète | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` |
| **Baseline mesurée le 2026-09-09** | **419 réussis / 0 échec / 0 ignoré, 37 s** — confirmé par exécution réelle |
| Contrainte de parallélisme | `[Collection("XAML WPF")]` sérialise les 4+ classes qui chargent du BAML (course du chargeur XAML WPF, décision phase 16). **Toute nouvelle classe de test qui construit `MainWindow`/`SettingsWindow` DOIT porter cet attribut.** |

### Requirements → carte des tests

| Req | Comportement | Type | Commande automatisée | Fichier existant ? |
|-----|--------------|------|----------------------|--------------------|
| TOK-01 | `DoitRafraichir` : expiré / dans la marge / hors marge / `ExpiresAt` null | unitaire pur | `dotnet test … --filter "FullyQualifiedName~ChronosTokenAuthorityTests"` | ❌ Wave 0 |
| TOK-01 | Jeton **déjà expiré au démarrage** ⇒ un tick immédiat rafraîchit (fake handler renvoie 200) | unitaire | idem `TokenRefreshServiceTests` | ❌ Wave 0 |
| TOK-01 | Jeton **encore valide** ⇒ aucun appel réseau (`handler.SendCount == 0`) | unitaire | idem | ❌ Wave 0 |
| TOK-01 | **Rotation persistée** : après refresh, `_coffre.Load().RefreshToken` == le NOUVEAU | unitaire (store sur chemin temp) | idem | ❌ Wave 0 |
| TOK-01/02 | **Concurrence** : N appels parallèles à `GetAccessTokenAsync` ⇒ `handler.SendCount == 1` | unitaire (`Task.WhenAll`) | idem | ❌ Wave 0 — **le test le plus important de la phase** |
| TOK-02 | 400 `invalid_grant` ⇒ `Deconnecte` ; 429 ⇒ `HorsLigne` ; 5xx ⇒ `HorsLigne` ; `HttpRequestException` ⇒ `HorsLigne` | unitaire `[Theory]` | idem | ❌ Wave 0 |
| TOK-02 | 200 + corps illisible ⇒ `Deconnecte` (Pitfall 3) | unitaire | idem | ❌ Wave 0 |
| TOK-02 | Échec définitif ⇒ **`oauth.dat` existe toujours** (Pitfall 4) | unitaire (fichier temp) | idem | ❌ Wave 0 |
| TOK-02 | Échec temporaire ⇒ backoff respecté (un 2ᵉ appel immédiat ne renvoie pas de requête) | unitaire | idem | ❌ Wave 0 |
| TOK-02 | Transition ⇒ **un seul** `EtatChange` (pas un event par tick) | unitaire | idem | ❌ Wave 0 |
| TOK-02 | 401 sur l'usage ⇒ invalidation + **un seul** réessai, puis `Deconnecte` | unitaire | `…~ChronosOAuthUsageProviderTests` | ❌ Wave 0 (**aucun test n'existe pour cette classe**) |
| TOK-02 | `EtatChange` ⇒ VM marshalle par `IUiDispatcher.Post` **exactement une fois** et allume `AfficherPastilleDeconnexion` | unitaire `[Fact]` (pas STA) | `…~MainViewModelTests` | ⚠️ fichier existe, cas à ajouter |
| TOK-02 | `HorsLigne` ⇒ `AfficherPastilleDeconnexion == false` (pas de fausse alerte) | unitaire | idem | ⚠️ à ajouter |
| TOK-03 | `ReconnecterCommand` appelle `LoginAsync`, **jamais** `Logout` (fake qui compte les deux) | unitaire | idem | ⚠️ à ajouter |
| TOK-03 | Login réussi ⇒ `ReinitialiserApresLogin` + `RequestRefresh` appelés ; pastille éteinte au succès suivant | unitaire | idem | ⚠️ à ajouter |
| TOK-03 | `MainWindow` se construit et se met en page avec la pastille visible (Measure/Arrange) | smoke `[WpfFact]` + `[Collection("XAML WPF")]` | `…~CadranBindingTests` | ⚠️ à ajouter |
| Garde | Aucun type WPF dans `Services/`/`Models/` | garde réflexive | `…~ServicesLayerPurityTests` | ✅ existe, **doit rester vert sans modification** |
| Garde | Le graphe DI résout `ChronosTokenAuthority`, `IAuthStatus`, `TokenRefreshService` et `MainViewModel` | garde DI | `…~CompositionRootTests` | ⚠️ à étendre (précédent : phases 13/15) |
| Critère 4 | Aucun crash/gel : le service ne lève jamais, même autorité en échec | unitaire (`Record.Exception`) | `…~TokenRefreshServiceTests` | ❌ Wave 0 |

**Aucun test ne doit jamais joindre le réseau réel.** Tout passe par `FakeHttpMessageHandler`, `FakeClock`,
un `ChronosOAuthStore(cheminTemp)` et `FakeOAuthLogin`. Garde anti-accident à reproduire (précédent
`CompositionRootTests:109`) : asserter que le chemin du coffre de test commence par `Path.GetTempPath()`.

### Cadence d'échantillonnage

- **Par commit de tâche :** `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~<ClasseTouchée>"` (< 10 s).
- **Par fusion de vague :** suite complète (`dotnet test tests/Chronos.Tests/Chronos.Tests.csproj`, ~40 s).
- **Porte de phase :** suite complète verte **et** `ServicesLayerPurityTests` + `CompositionRootTests` verts,
  avant `/gsd:verify-work`. Cible de couverture : **419 + N**, jamais moins de 419 — cette phase **n'enlève
  aucun test** (contrairement à la phase 16, où le recul était le livrable). Si un test disparaît, c'est une
  régression à justifier nominativement.

### Lacunes Wave 0

- [ ] `tests/Chronos.Tests/Fakes/FakeAuthStatus.cs` — `IAuthStatus` en mémoire, avec un déclencheur manuel
      d'`EtatChange` ; requis par les **5** sites `new MainViewModel(...)`.
- [ ] `tests/Chronos.Tests/ChronosTokenAuthorityTests.cs` — sérialisation, rotation, classification, backoff,
      non-effacement du coffre.
- [ ] `tests/Chronos.Tests/TokenRefreshServiceTests.cs` — premier tick immédiat, pas d'appel si valide, ne lève jamais.
- [ ] `tests/Chronos.Tests/ChronosOAuthUsageProviderTests.cs` — **la classe modifiée n'a aujourd'hui aucun
      test dédié** (vérifié : le dossier de tests contient `ClaudeOAuthUsageProviderTests` et
      `GatedOAuthUsageProviderTests`, mais **pas** `ChronosOAuthUsageProviderTests`). C'est la plus grosse
      lacune de couverture de la phase : on va modifier du code non testé.
- [ ] Extension de `ChronosOAuthClientTests` : les 4 tests actuels ne couvrent que `CreatePkce`,
      `BuildAuthorizeUrl` et `SplitCodeState` — `RefreshAsync` n'est **pas** testé. Ajouter des cas avec
      `FakeHttpMessageHandler` (200 nominal / 400 `invalid_grant` / 429 / 200 corps tronqué).
- [ ] Aucun cadre à installer : xUnit + StaFact sont déjà là.

---

## Inventaire des tests impactés (question ouverte 7)

**Règle générale : réécrire, jamais supprimer.** Aucun test existant ne devient obsolète — le code supprimé
(le refresh paresseux) n'est couvert par aucun test aujourd'hui.

### A. Cassés à coup sûr si `MainViewModel` gagne un paramètre de constructeur (5 sites, 4 fichiers)

| Fichier : ligne | Action |
|-----------------|--------|
| `tests/Chronos.Tests/MainViewModelTests.cs:68` (helper `Build`) | **Réécrire** : ajouter `new FakeAuthStatus()`. Ce seul point couvre la majorité des tests VM. |
| `tests/Chronos.Tests/MainViewModelTests.cs:108` (construction inline) | **Réécrire** : idem. |
| `tests/Chronos.Tests/CadranBindingTests.cs:34` | **Réécrire** : idem. |
| `tests/Chronos.Tests/OverlayWindowConfigTests.cs:23` | **Réécrire** : idem. |
| `tests/Chronos.Tests/ThemingTests.cs:96` | **Réécrire** : idem. |

### B. Casse la garde DI si l'inscription est oubliée

| Fichier | Action |
|---------|--------|
| `tests/Chronos.Tests/CompositionRootTests.cs` (`Host_resout_et_dispose_les_singletons`, ligne ~89) | **Réécrire** : ajouter `services.AddSingleton<IAuthStatus>(_ => new FakeAuthStatus());` (ou l'autorité réelle sur chemin temp) à côté de l'inscription existante d'`IOAuthLogin`. Sans cela `GetRequiredService<MainViewModel>()` lève. |
| `tests/Chronos.Tests/CompositionRootTests.cs` | **Ajouter** un `[Fact] Le_graphe_DI_resout_l_autorite_de_jeton()` reproduisant la sous-chaîne d'`App.xaml.cs` (coffre sur chemin **temp**, client, autorité, `TokenRefreshService`, `AddHostedService`). Précédent : les gardes analogues des phases 13 et 15, qui attrapent une DI qui compile mais plante au démarrage. |

### C. Cassés SEULEMENT si `DiagnosticService` gagne un paramètre OBLIGATOIRE — 7 sites de test

`CadranBindingTests:37`, `CompositionRootTests:79`, `DiagnosticServiceTests:38`, `DiagnosticServiceTests:53`,
`MainViewModelTests:67`, `MainViewModelTests:111`, `OverlayWindowConfigTests:26`, `ThemingTests:99`
(+ `App.xaml.cs:320` en prod).
→ **Recommandation forte : paramètre OPTIONNEL en dernière position** (`IAuthStatus? authStatus = null`).
Blast radius ramené à **0 test cassé**, et le rapport gagne quand même la ligne d'état d'auth réelle.
Les deux assertions de chaîne existantes (`"Token déchiffré : OUI"`, `"Usage exact (OAuth)"`, `"Conseil"`)
doivent être **préservées mot pour mot** — ce sont elles qui tiennent `DiagnosticServiceTests`.

### D. Cassés si `ChronosOAuthClient.RefreshAsync` change de type de retour

Aucun test. **Vérifié** : `ChronosOAuthClientTests` (71 lignes) ne touche que `CreatePkce`,
`BuildAuthorizeUrl` et `SplitCodeState`. `Views/OAuthLogin.cs` n'appelle que `ExchangeCodeAsync`
(dont la signature reste inchangée). Le seul appelant de `RefreshAsync` est
`ChronosOAuthUsageProvider.cs:63`, qui est réécrit de toute façon.

### E. À NE PAS toucher (doivent rester verts sans modification)

`ServicesLayerPurityTests` (2 tests, dont la garde de non-retour `Budget*`), `ChronosOAuthStoreTests`,
`ClaudeOAuthUsageProviderTests`, `GatedOAuthUsageProviderTests`, `LastExactUsageProviderTests`,
`LastExactStoreTests`, `CompositeUsageProviderTests`, `RefreshOrchestratorTests`, `SessionsTests`,
`TranscriptActivity*Tests`, `SettingsServiceTests` (dont la fixture réelle DEL-06). Si l'un d'eux devient
rouge, c'est le signe d'un débordement de périmètre.

---

## Open Questions

1. **Durée de vie réelle de l'access token Claude Code (`expires_in`).**
   - Ce qu'on sait : `ChronosOAuthClient.PostTokenAsync` prend `expires_in` de la réponse, avec un repli
     codé en dur à **3600 s** (ligne 116). Les sources communautaires évoquent tantôt ~1 h, tantôt ~8 h,
     tantôt ~24 h selon les versions. **LOW confidence.**
   - Ce qui reste flou : la valeur d'aujourd'hui.
   - Recommandation : **ne pas la coder en dur nulle part**. La marge de 12 min et le tick de 60 s
     fonctionnent pour n'importe quel `expires_in` ≥ ~15 min. Le repli 3600 s existant est un plancher
     raisonnable ; le laisser tel quel.

2. **Le serveur peut-il révoquer un jeton AVANT `ExpiresAt` ?**
   - Ce qu'on sait : oui, c'est signalé —
     [claude-code#54443](https://github.com/anthropics/claude-code/issues/54443) décrit un 401 « before the
     locally stored expiresAt time », suivi d'un 400 au refresh. **MEDIUM confidence** (rapport de bug, pas doc officielle).
   - Conséquence sur le plan : le rafraîchissement préventif **ne suffit pas** à lui seul ; le chemin
     « 401 sur l'usage ⇒ invalider ⇒ un réessai ⇒ sinon `Deconnecte` » (Pattern 3b) est **obligatoire**,
     pas un raffinement.

3. **Faut-il afficher la pastille quand l'utilisateur ne s'est JAMAIS connecté (`NonConnecte`) ?**
   - Ce qu'on sait : EXA-05 (« si aucun chiffre exact n'a jamais été obtenu, l'overlay affiche indisponible
     et **invite à se connecter** ») est explicitement assigné à la **phase 19**.
   - Recommandation : en phase 17, la valeur `NonConnecte` **existe dans l'enum mais n'allume rien**. Le
     canal `IAuthStatus` est prêt, la phase 19 n'aura qu'à ajouter une branche d'affichage. Poser le
     contraire créerait un badge permanent pour un utilisateur qui a délibérément choisi de ne pas se connecter.

4. **La cadence du service de fond doit-elle être réglable dans `settings.json` ?**
   - Recommandation : **non**. `HDR-06` (cadence bornée + coût annoncé) concerne la sonde de la **phase 18**,
     pas le rafraîchissement de jeton, qui ne coûte rien à l'utilisateur. Ajouter un champ à
     `ChronosSettings` élargirait la surface persistée sans bénéfice.

5. **Où placer `EtatAuthentification` : `Chronos.Services` ou `Chronos.Models` ?**
   - Les deux passent `ServicesLayerPurityTests`. `Chronos.Models` ne contient aujourd'hui que le modèle de
     **donnée d'usage** (`UsageSnapshot`, `WindowState`, `WindowKind`, `SourceReliability`).
   - Recommandation : **`Chronos.Services`** — c'est un état de service, pas une donnée d'usage ; le mettre
     dans `Models` inviterait plus tard à l'agréger dans `UsageSnapshot`, exactement l'option (a) rejetée.

---

## Sources

### Primaires (HIGH confidence)

- **Code du dépôt, lu intégralement** (la source la plus fiable ici) :
  `src/Chronos/Services/ChronosOAuthClient.cs`, `ChronosOAuthStore.cs`, `ChronosOAuthUsageProvider.cs`,
  `ClaudeOAuthUsageProvider.cs`, `GatedOAuthUsageProvider.cs`, `IOAuthLogin.cs`, `LastExactUsageProvider.cs`,
  `CompositeUsageProvider.cs`, `RefreshOrchestrator.cs`, `DesktopUiaPollService.cs`, `DiagnosticService.cs`,
  `IUsageProvider.cs`, `IClock.cs`, `IUiDispatcher.cs`, `ChronosPaths.cs`, `ChronosSettings.cs` ;
  `src/Chronos/ViewModels/MainViewModel.cs` ; `src/Chronos/Views/OAuthLogin.cs`, `MainWindow.xaml`,
  `MainWindow.xaml.cs`, `SettingsWindow.xaml`, `Cadrans/CadranBraisesView.xaml` ;
  `src/Chronos/Resources/DesignTokens.xaml` ; `src/Chronos/Theming/ChronosTheme.cs` ;
  `src/Chronos/App.xaml.cs` ; `src/Chronos/Chronos.csproj` ;
  `tests/Chronos.Tests/*` (inventaire complet + lecture de `CompositionRootTests`, `ServicesLayerPurityTests`,
  `MainViewModelTests`, `CadranBindingTests`, `ThemingTests`, `OverlayWindowConfigTests`,
  `DiagnosticServiceTests`, `GatedOAuthUsageProviderTests`, `DesktopUiaPollServiceTests`,
  `ChronosOAuthClientTests`, `Fakes/*`).
- **Exécution réelle de la suite de tests** (2026-09-09) : `dotnet test` → 419 réussis / 0 échec / 37 s.
- **Environnement réel** : `dotnet --version` → 10.0.201 ; `Microsoft.WindowsDesktop.App` 8.0.25 présent.
- Microsoft Learn — `HttpRequestException.HttpRequestError` : propriété présente à partir du moniker
  **net-8.0** — https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httprequestexception.httprequesterror
- Microsoft Learn — `PeriodicTimer` (un seul consommateur à la fois ; `Dispose` interrompt
  `WaitForNextTickAsync`) — https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer
- Microsoft Learn — `PeriodicTimer.Period` : **get/set**, monikers net-8.0+ ; « Setting Period affects only
  the current period and all subsequent times at which the timer will tick » —
  https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer.period
- RFC 6749 §5.2 — codes d'erreur du point de terminaison de jeton (`invalid_grant` = « grant (including a
  refresh token) is invalid, expired, revoked… », typiquement HTTP 400) —
  https://datatracker.ietf.org/doc/html/rfc6749#section-5.2
- **Sonde réseau anonyme** (2026-09-09, sans aucun identifiant réel) :
  `POST https://console.anthropic.com/v1/oauth/token` corps vide → **400** ; refresh token bidon → **429
  `rate_limit_error`**. Le point de terminaison utilisé par le code est vivant et ne redirige pas.

### Secondaires (MEDIUM confidence — rapports de bugs du dépôt officiel `anthropics/claude-code`)

- Course de rotation de refresh token entre sessions concurrentes, sans verrou —
  https://github.com/anthropics/claude-code/issues/25609
- 400 au refresh après un 401 antérieur à l'`expiresAt` local —
  https://github.com/anthropics/claude-code/issues/54443
- Absence d'auto-refresh en mode non interactif (401 après expiration) —
  https://github.com/anthropics/claude-code/issues/53063

### Tertiaires (LOW confidence — à ne pas coder en dur)

- Durées de vie annoncées des access tokens Claude (1 h / 8 h / 24 h selon les sources communautaires) et
  mention d'un point de terminaison alternatif `platform.claude.com/v1/oauth/token`. **Non utilisé dans les
  recommandations** : le code lit `expires_in` de la réponse et `console.anthropic.com` répond toujours.

---

## Metadata

**Répartition de la confiance :**
- Stack et outillage : **HIGH** — aucun paquet à ajouter ; versions lues dans les `.csproj` réels et
  l'environnement sondé ; APIs .NET vérifiées sur learn.microsoft.com.
- Architecture (autorité unique, canal d'état, placement de la pastille) : **HIGH** — chaque recommandation
  s'appuie sur un motif déjà en production dans ce dépôt (`RefreshOrchestrator`, `DesktopUiaPollService`,
  `IUiDispatcher`, `CentreHit`), et le rayon de souffle sur les tests a été énuméré par `grep` exhaustif.
- Classification des échecs : **HIGH** pour la mécanique (RFC 6749 + codes HTTP + 429 constaté en direct),
  **MEDIUM** pour les comportements spécifiques du serveur Anthropic (issues GitHub, pas de doc publique).
- Pièges : **HIGH** — 8 des 10 sont démontrables par lecture du code du dépôt ; 2 s'appuient sur des issues
  officielles.
- Durée de vie du jeton : **LOW** — délibérément neutralisée par la conception (rien de codé en dur).

**Date de recherche :** 2026-09-09
**Valide jusqu'au :** ~2026-10-09 (30 j) pour la partie .NET/architecture ; **~7 j** pour les hypothèses sur
le comportement du serveur OAuth d'Anthropic (endpoint non documenté, susceptible de changer sans préavis).
