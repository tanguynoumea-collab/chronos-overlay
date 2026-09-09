# Phase 17: Jeton toujours vivant, panne toujours visible - Context

**Gathered:** 2026-09-09
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

L'utilisateur n'est plus jamais laissé deux mois avec un jeton mort sans le savoir : le jeton OAuth est
rafraîchi **avant** son expiration, et si l'authentification tombe malgré tout, l'overlay le dit et permet
de rouvrir le login en un clic.

Exigences couvertes : TOK-01 (rafraîchissement préventif), TOK-02 (échec d'authentification visible),
TOK-03 (réparation en un clic).

**Hors périmètre :** la source par en-têtes de rate-limit (phase 18), la doctrine du composite (phase 19),
la distinction visuelle frais / daté / indisponible du cadran (phase 20). Ici on traite UNIQUEMENT le
cycle de vie du jeton et la visibilité de sa panne.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
Tous les choix d'implémentation sont à la discrétion de Claude — la discussion est désactivée
(`workflow.skip_discuss=true`).

### SÉCURITÉ — contraintes absolues sur le jeton
- **Ne JAMAIS logger, écrire en clair, mettre en exception ou concaténer dans une URL** un access token
  ou un refresh token. Le jeton ne vit qu'en variable locale, le temps de construire l'en-tête
  `Authorization: Bearer`.
- **NE JAMAIS déclencher un vrai rafraîchissement contre le jeton stocké de l'utilisateur pendant le
  développement ou les tests.** Le flux OAuth fait tourner le refresh token : un refresh exécuté hors de
  l'application, dont le résultat n'est pas re-sauvegardé, **invaliderait définitivement** le jeton stocké
  et déconnecterait l'utilisateur. Tous les tests passent par des faux (`FakeHttpMessageHandler` existe déjà).
- Le coffre reste chiffré DPAPI portée `CurrentUser` (`ChronosOAuthStore`), sous `%APPDATA%\Chronos\oauth.dat`.
- **Rotation du refresh token** : si l'API renvoie un nouveau refresh token, il DOIT être persisté avant
  toute autre action, sinon le suivant échouera.

### Contraintes projet
- MVVM strict, `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers Models/Views/ViewModels/Services.
- Aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`) : la pastille est un état exposé
  par le ViewModel, la logique de jeton reste neutre.
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin, aucune dépendance native.
- Dégradation : réseau coupé, refresh token invalide, serveur injoignable → aucun crash, aucun gel de l'UI.
- UI et commentaires en **français**.
- **Baseline à l'entrée de phase : 419 tests xUnit verts.**

</decisions>

<code_context>
## Existing Code Insights

### État réel constaté (diagnostic du 2026-09-09, ne pas ré-enquêter)
Le jeton stocké de l'utilisateur a **expiré le 2026-07-12**. `GET https://api.anthropic.com/api/oauth/usage`
renvoie **HTTP 401**. L'échec est **totalement silencieux** : aucun signal dans l'overlay, aucune entrée
visible ; l'utilisateur a vécu deux mois avec des chiffres faux sans savoir que sa source exacte était morte.

### Fichiers concernés
- `src/Chronos/Services/ChronosOAuthClient.cs` — flux PKCE complet (`CreatePkce`, `BuildAuthorizeUrl`,
  échange de code, `RefreshAsync`). Client public de Claude Code, scopes `user:inference user:profile`.
- `src/Chronos/Services/ChronosOAuthStore.cs` — coffre DPAPI (`Load`, `Save`, `Clear`, `Exists`),
  record `OAuthTokens(AccessToken, RefreshToken, ExpiresAt)`.
- `src/Chronos/Services/ChronosOAuthUsageProvider.cs` — **rafraîchissement PARESSEUX actuel** : le refresh
  n'a lieu qu'au moment d'un `GetAsync`, quand `tokens.ExpiresAt - RefreshMargin <= now` (marge 5 min).
  En cas d'échec du refresh, il pose `_nextAllowedCall = now + MinInterval` et **tente quand même l'appel**,
  puis retombe silencieusement sur le cache ou `UsageSnapshot.Empty`. C'est le comportement à corriger.
- `src/Chronos/Services/IOAuthLogin.cs` et `src/Chronos/Views/OAuthLogin.cs` — parcours de login existant
  (à réutiliser pour TOK-03, ne pas réécrire).
- `src/Chronos/Services/ClaudeOAuthUsageProvider.cs` + `GatedOAuthUsageProvider.cs` — voie secondaire
  (coffre de l'app bureau). Sur cette machine le coffre est ABSENT ; ne pas y investir.
- `src/Chronos/ViewModels/MainViewModel.cs` — porte l'état exposé à l'overlay et les `[RelayCommand]`.
- `src/Chronos/Views/MainWindow.xaml` — surface de l'overlay où la pastille doit apparaître.
- `src/Chronos/Services/DiagnosticService.cs` — doit refléter l'état d'authentification réel.
- `src/Chronos/Services/LastExactUsageProvider.cs` — décorateur livré en phase 16, en tête de chaîne.

### Briques réutilisables
- `tests/Chronos.Tests/Fakes/FakeHttpMessageHandler.cs`, `FakeOAuthLogin.cs`, `FakeClock.cs`.
- `tests/Chronos.Tests/ChronosOAuthClientTests.cs`, `ChronosOAuthStoreTests.cs`.
- `IHostedService` + `PeriodicTimer` : motif déjà employé par `RefreshOrchestrator` et
  `DesktopUiaPollService` pour les travaux de fond hors thread UI.

</code_context>

<specifics>
## Specific Ideas

La pastille de TOK-02/TOK-03 doit être discrète mais non ratable, cohérente avec les tokens de design
existants (voir `src/Chronos/Resources/DesignTokens.xaml` et les 3 thèmes). Le cadran fait 170 px et
l'overlay est compact : pas de bandeau envahissant.

</specifics>

<deferred>
## Deferred Ideas

- Trou visuel dans la `UniformGrid` des réglages (3 boutons dans une grille à 2 colonnes) hérité de la
  phase 16 — **reporté à la phase 20**, qui touche le cadran et le diagnostic.

</deferred>
