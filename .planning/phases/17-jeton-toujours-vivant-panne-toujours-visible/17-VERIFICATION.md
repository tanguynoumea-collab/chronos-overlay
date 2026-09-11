---
phase: 17-jeton-toujours-vivant-panne-toujours-visible
verified: 2026-09-12T00:00:00Z
status: passed
score: 4/4 must-haves verified (TOK-01, TOK-02, TOK-03, robustesse)
human_verification:
  - test: "Lisibilité de la pastille ambre/grise sur fond d'écran réel, dans les 3 thèmes × 5 styles de cadran × 2 modes (Normal/Étendu)"
    expected: "La pastille reste visible et son contraste suffisant à travers la fenêtre transparente, sans masquer aucun anneau"
    why_human: "Rendu visuel sur fenêtre AllowsTransparency avec fond d'écran réel — non automatisable. La géométrie (non-recouvrement des anneaux, 101,8 px vs 71,5 px de rayon) et l'existence du pinceau dans les 9 thèmes SONT automatisées et vertes ; seul le contraste perçu ne l'est pas."
  - test: "Parcours de reconnexion de bout en bout : clic sur la pastille ambre → navigateur → collage du code → disparition de la pastille et chiffres exacts"
    expected: "Le coffre oauth.dat existe toujours pendant le parcours (avant la fin du login), puis la pastille disparaît et le diagnostic affiche CONNECTÉ après un login réussi"
    why_human: "Exige une authentification navigateur réelle contre le serveur Anthropic — explicitement exclu de toute automatisation par la contrainte de sécurité de la phase (ne jamais appeler l'endpoint de refresh avec le jeton réel de l'utilisateur)."
---

# Phase 17 : Jeton toujours vivant, panne toujours visible — Verification Report

**Phase Goal:** L'utilisateur n'est plus jamais laissé deux mois avec un jeton mort sans le savoir : le
jeton OAuth est rafraîchi AVANT son expiration, et si l'authentification tombe malgré tout, l'overlay le
dit et permet de rouvrir le login en un clic.

**Verified:** 2026-09-12
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Le jeton est rafraîchi préventivement, AVANT expiration, sans attendre un besoin (TOK-01) | ✓ VERIFIED | `ChronosTokenAuthority.DoitRafraichir` (ligne 173) est un prédicat pur comparant l'horloge murale injectée (`IClock`) ; `TokenRefreshService` est un vrai `IHostedService` enregistré ET démarré (`App.xaml.cs:297-298`), tick fixe 60 s, `dueTime=TimeSpan.Zero` (premier tick immédiat) |
| 2 | Un échec de renouvellement ou un 401 fait apparaître une pastille, distincte de « hors ligne » (TOK-02) | ✓ VERIFIED | `EtatAuthentification` a 4 valeurs (`NonConnecte/Connecte/HorsLigne/Deconnecte`) ; `MainWindow.xaml` a deux éléments visuellement et sémantiquement distincts : `Button` ambre cliquable pour `Deconnecte`, `Ellipse` grise inerte pour `HorsLigne` ; 429 `rate_limit_error` classé `EchecTemporaire`→`HorsLigne`, jamais `Deconnecte` |
| 3 | La pastille permet de rouvrir le login en un clic, sans jamais pouvoir supprimer le coffre (TOK-03) | ✓ VERIFIED | Pastille bindée sur `ReconnecterCommand` (dédiée, monodirectionnelle), jamais `LoginClaudeCommand` (qui bascule sur `IsLoggedIn==_store.Exists`) ; `ReconnecterAsync` appelle `_authStatus.ReinitialiserApresLogin()` PUIS `_orchestrator.RequestRefresh()` sur succès, jamais `Logout()` |
| 4 | Robustesse : réseau coupé, refresh token invalide, serveur injoignable → aucun crash, aucun gel | ✓ VERIFIED | `TokenRefreshService.TickAsync` ne lève jamais (try/catch total) ; `ChronosOAuthClient.RefreshAsync` filtre nominativement les exceptions réseau/timeout en `EchecTemporaire` ; suite complète (505 tests) exécutée 3 fois consécutives, 0 échec |

**Score:** 4/4 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Chronos/Services/ChronosTokenAuthority.cs` | Autorité unique de jeton (sémaphore, décision, rotation, recul, publication d'état) | ✓ VERIFIED | 189 lignes, `SemaphoreSlim(1,1)` unique, double-vérification, `.Clear()` absent (0 occurrence), recul indépendant de tout cache |
| `src/Chronos/Services/TokenRefreshService.cs` | `IHostedService` à tick fixe, premier tick immédiat | ✓ VERIFIED | 95 lignes, `dueTime=TimeSpan.Zero`, `Periode=60s`, `TickAsync` ne lève jamais |
| `src/Chronos/Services/ChronosOAuthUsageProvider.cs` | Consommateur pur de l'autorité, plus de refresh paresseux | ✓ VERIFIED | ctor `(ChronosTokenAuthority, HttpClient, IClock)` — plus de `_store`/`_client` ; rejeu unique sur 401 ; frein de débit inconditionnel (découplé du cache) |
| `src/Chronos/Services/ChronosOAuthClient.cs` | `RefreshAsync` typé, cause d'échec distincte | ✓ VERIFIED | `ResultatRafraichissement(IssueRafraichissement, OAuthTokens?)` — exactement 2 propriétés, aucun `string` ; 429 rate_limit_error → `EchecTemporaire` |
| `src/Chronos/Services/EtatAuthentification.cs` / `IAuthStatus.cs` | Vocabulaire d'état neutre à 4 valeurs, canal observable | ✓ VERIFIED | Créés au plan 17-02, aucun type WPF, `EtatChange` + `ReinitialiserApresLogin()` |
| `src/Chronos/ViewModels/MainViewModel.cs` | Deux booléens de pastille, commande dédiée `ReconnecterCommand` | ✓ VERIFIED | `AfficherPastilleDeconnexion`/`AfficherPastilleHorsLigne`, `ReconnecterAsync` n'appelle jamais `Logout()` |
| `src/Chronos/Views/MainWindow.xaml` | Pastille ambre (Deconnecte) + pastille grise (HorsLigne), bindées correctement | ✓ VERIFIED | `Command="{Binding ReconnecterCommand}"` sur le `Button` ambre ; `Ellipse` grise sans `Command` (inerte) |
| `src/Chronos/App.xaml.cs` | Câblage DI réel : autorité singleton, `IAuthStatus` aliasé, `TokenRefreshService` hébergé | ✓ VERIFIED | Lignes 289-298 : `AddSingleton<ChronosTokenAuthority>`, `AddSingleton<IAuthStatus>(sp=>sp.GetRequiredService<ChronosTokenAuthority>())`, `AddHostedService(sp=>sp.GetRequiredService<TokenRefreshService>())` |
| `src/Chronos/Services/DiagnosticService.cs` | Distingue HORS LIGNE (informatif) de DÉCONNECTÉ (actionnable) | ✓ VERIFIED | Ligne 102 : `"État d'authentification : " + LibelleAuth(_authStatus?.Etat)`, paramètre optionnel en dernière position (0 site cassé) |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `App.xaml.cs` | `TokenRefreshService` | `AddHostedService` | ✓ WIRED | Ligne 298, résout la même instance singleton (ligne 297) — un service jamais démarré ne rafraîchit rien ; ici il l'est |
| `App.xaml.cs` | `ChronosTokenAuthority` | `AddSingleton<IAuthStatus>` alias | ✓ WIRED | Ligne 293 — même instance, pas une seconde autorité (`CompositionRootTests` prouve `Assert.Same`) |
| `ChronosOAuthUsageProvider.GetAsync` | `ChronosTokenAuthority.GetAccessTokenAsync` | appel direct + rejeu sur 401 | ✓ WIRED | Lignes 74, 91 — seule source de jeton, invalidation + un seul rejeu |
| `MainWindow.xaml` pastille ambre | `MainViewModel.ReconnecterCommand` | `Command="{Binding ReconnecterCommand}"` | ✓ WIRED | Confirmé XAML + `[WpfFact] La_pastille_est_bindee_sur_ReconnecterCommand_et_JAMAIS_sur_LoginClaudeCommand` |
| `MainViewModel.ReconnecterAsync` | `IAuthStatus.ReinitialiserApresLogin` + `RefreshOrchestrator.RequestRefresh` | appels séquentiels sur succès | ✓ WIRED | Lignes 411-412 — les deux appelés, jamais `Logout()` |
| `ChronosOAuthUsageProvider` | `ChronosOAuthStore` / `ChronosOAuthClient` | (absence attendue) | ✓ CONFIRMÉ ABSENT | `grep -rln "RefreshAsync" src/Chronos` → exactement 2 fichiers (`ChronosOAuthClient.cs` définit, `ChronosTokenAuthority.cs` appelle) |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|---------------------|--------|
| Pastille `AfficherPastilleDeconnexion`/`HorsLigne` (XAML) | `MainViewModel` booléens | `IAuthStatus.EtatChange` (event du pool) → `SurEtatAuthChange` → `_ui.Post` → `AppliquerEtatAuth` | Oui — `IAuthStatus` réel enregistré dans le graphe DI (pas de faux en production) | ✓ FLOWING |
| Diagnostic « État d'authentification » | `_authStatus?.Etat` | Même autorité singleton, lue en direct | Oui | ✓ FLOWING |
| `ChronosOAuthUsageProvider` chiffres exacts | `jeton` obtenu de l'autorité | `ChronosTokenAuthority.GetAccessTokenAsync` → coffre DPAPI réel via `ChronosOAuthStore.Load()` | Oui | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Suite complète verte (0 échec, 505 tests) | `dotnet test Chronos.sln -v q --nologo` | 505 réussis / 0 échec | ✓ PASS |
| Stabilité sur exécutions répétées (garde flakiness BAML) | Suite complète × 3 | 505/505 à chaque fois | ✓ PASS |
| Coffre réel intact après exécution des tests | `stat oauth.dat` avant/après | 518 octets, mtime epoch 1783863147 — identique | ✓ PASS |
| Un seul rafraîchisseur dans le dépôt | `grep -rln "RefreshAsync" src/Chronos` | Exactement 2 fichiers (client + autorité) | ✓ PASS |
| Aucun jeton en clair journalisé | `grep -rniE "accessToken\|refreshToken" src/Chronos --include=*.cs \| grep -iE "log\|Console\|WriteLine\|Append"` | 1 hit, `DiagnosticService.cs:220`, écrit le NOM du champ + PRÉSENT/absent, jamais la valeur | ✓ PASS (faux positif attendu, documenté depuis 17-01) |
| `.Clear()` absent de l'autorité | `grep -n "\.Clear()" ChronosTokenAuthority.cs` | Aucun résultat | ✓ PASS |
| `ResultatRafraichissement` sans propriété texte | Lecture du record | `record ResultatRafraichissement(IssueRafraichissement Issue, OAuthTokens? Jetons)` — 2 membres, aucun `string` | ✓ PASS |
| 429 `rate_limit_error` non classé `Deconnecte` | Lecture `ChronosOAuthClient.RefreshAsync` + `ChronosTokenAuthority` | 429 → `EchecTemporaire` → `HorsLigne`, jamais `Deconnecte` | ✓ PASS |
| Verrou sur refus définitif (anti-martèlement) | Test `Apres_un_refus_definitif_cinq_appels_de_plus_n_emettent_AUCUNE_requete` présent et vert | Présent | ✓ PASS |
| Recul sans cache, sur les deux chemins (jeton + usage) | Lecture `ChronosTokenAuthority` (recul en RAM propre) + `ChronosOAuthUsageProvider.GetAsync` (frein inconditionnel avant tout accès au cache) | Les deux chemins découplés du cache | ✓ PASS |
| Aucun type WPF dans les fichiers de la phase 17 sous `Services/` | `grep -n "System.Windows"` sur les 8 fichiers touchés | Aucun résultat | ✓ PASS |
| Phases 18/19/20 non anticipées | SUMMARY 17-05 + git diff (aucun fichier de source d'en-têtes, de doctrine composite, ni de cadran visuel touché) | Confirmé — `CompositeUsageProvider.Best()` intact, `UniformGrid` réglages non touchée | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|--------------|--------|----------|
| TOK-01 | 17-01→17-04 (livré 17-04) | Rafraîchissement préventif avant expiration | ✓ SATISFIED | `TokenRefreshService` hébergé, tick immédiat, `DoitRafraichir` pur, marge 12 min > tick 60 s |
| TOK-02 | 17-01→17-05 (livré 17-04/17-05) | Échec d'authentification visible | ✓ SATISFIED | 4 états distincts, pastilles ambre/grise différenciées, 401 → invalidation → rejeu → `Deconnecte` |
| TOK-03 | 17-05 | Réparation en un clic | ✓ SATISFIED | `ReconnecterCommand` dédiée, `LogoutCount==0` prouvé par test, contre-épreuve de la bascule dangereuse |

Aucun requirement orphelin détecté pour la phase 17 (REQUIREMENTS.md : TOK-01/02/03 tous `[x]` Complete).

### Anti-Patterns Found

Aucun anti-pattern bloquant. Deux points mineurs déjà documentés par les SUMMARY et jugés hors périmètre :

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `DiagnosticService.cs` | 230 | Mention du nom de champ `claudeAiOauth.accessToken` (jamais la valeur) capte les grep anti-fuite génériques | ℹ️ Info | Aucun risque réel de fuite ; préexistant, non touché par la phase 17 |
| `RefreshOrchestrator.cs` | ~105 | XML-doc de `TryTrigger` affirme un comportement faux (`false` sur file pleine, alors que `DropWrite` renvoie `true`) — a rendu une garde de test initialement muette | ⚠️ Warning (documentation) | Aucun effet fonctionnel ; découvert et contourné par une preuve de bout en bout au plan 17-05 ; correction de la doc différée (`deferred-items.md` #3), hors fichiers du plan |

Jugement sur les deux gardes de test remplacées (signalées dans `<known_flags>`) : les remplacements
sont réellement falsifiables — vérifié directement dans le code des tests (`CadranBindingTests.MonterPastille`
pose le `DataContext` sur la grille racine et purge le Dispatcher avant `Measure`/`Arrange` ; le test de
l'orchestrateur compte les appels `GetAsync` de bout en bout plutôt que de s'appuyer sur la valeur de retour
de `TryTrigger`). Les SUMMARY documentent des mutations ciblées ayant fait tomber les tests concernés,
cohérent avec une preuve falsifiable.

Jugement sur `ServeCachedOr` désormais soumis à la condition d'âge même en fenêtre de recul : c'est un gain
d'honnêteté cohérent avec la doctrine du milestone (ne jamais servir un chiffre trop vieux comme s'il était
frais), documenté comme décision assumée et non comme régression — aucun test existant ne couvrait
l'ancien comportement inverse, donc aucune régression de couverture.

Jugement sur le test d'ordre retiré (réarmement avant rafraîchissement) : le SUMMARY documente
honnêtement que l'invariant est réel en production mais inobservable en test parce que `RequestRefresh`
empile dans un channel consommé de façon asynchrone — déclarer l'absence de couverture explicitement,
plutôt que masquer avec une assertion complaisante, est le comportement correct ici.

### Human Verification Required

### 1. Lisibilité de la pastille sur fond d'écran réel

**Test:** Lancer `dotnet run --project src/Chronos/Chronos.csproj`, observer la pastille ambre (jeton
expiré sur cette machine) en bas à droite du cadran, puis parcourir les 3 thèmes × 5 styles × 2 modes.
**Expected:** La pastille reste visible et lisible (contraste suffisant) à travers la fenêtre transparente
dans toutes les combinaisons ; l'infobulle s'affiche au survol ; un glisser-déposer en saisissant la
pastille ne déplace pas la fenêtre.
**Why human:** Rendu visuel sur fenêtre `AllowsTransparency` avec composition réelle du bureau — la
géométrie (non-recouvrement) et l'existence du pinceau dans les 9 thèmes sont déjà automatisées et vertes ;
seul le contraste perçu à l'œil ne l'est pas.

### 2. Parcours de reconnexion de bout en bout

**Test:** Cliquer sur la pastille ambre, effectuer un login navigateur réel (collage du code), vérifier que
`%APPDATA%\Chronos\oauth.dat` existe toujours pendant le parcours, puis que la pastille disparaît et que
le diagnostic affiche « CONNECTÉ » après succès. Couper le wifi ~1 min pour vérifier la pastille grise
inerte.
**Why human:** Exige une authentification réelle contre le serveur Anthropic. La contrainte de sécurité de
la phase interdit explicitement tout appel automatisé au endpoint de refresh avec le jeton réel de
l'utilisateur (rotation destructive du refresh token) — ce test ne peut être qu'exécuté manuellement par
l'utilisateur.

### Gaps Summary

Aucun gap bloquant. Les 4 critères de succès de la ROADMAP et les 3 requirements (TOK-01/02/03) sont
vérifiés directement dans le code réel (pas seulement dans les SUMMARY) :

- Le câblage DI (`App.xaml.cs`) confirme que `TokenRefreshService` est réellement démarré comme
  `IHostedService`, et que `IAuthStatus` est un alias de la même instance d'autorité — pas une seconde
  autorité fantôme.
- Le piège central de la phase (brancher la pastille sur `LoginClaudeCommand`, ce qui aurait supprimé le
  coffre de l'utilisateur avec un jeton expiré) est structurellement évité : la commande `ReconnecterCommand`
  est dédiée, testée par contre-épreuve, et gardée par un `[WpfFact]` qui inspecte le binding réellement
  résolu dans le XAML.
- Les invariants de sécurité les plus sensibles (aucun `.Clear()` dans l'autorité, `ResultatRafraichissement`
  sans propriété texte, un seul rafraîchisseur dans tout le dépôt, 429 jamais classé « déconnecté », verrou
  sur refus définitif, recul indépendant de tout cache) sont tous vérifiés par lecture directe du code de
  production, pas seulement par les tests qui les couvrent.
- La suite complète (505 tests) a été exécutée 3 fois consécutives sans un seul échec ni flakiness.
- Le coffre réel `%APPDATA%\Chronos\oauth.dat` a été mesuré identique (518 octets, mtime epoch 1783863147)
  avant et après ces exécutions — aucun test n'a touché le login réel de l'utilisateur.
- Aucune anticipation des phases 18 (en-têtes de rate-limit), 19 (doctrine du composite) ou 20 (rendu
  visuel frais/daté/indisponible) n'a été détectée dans le code livré.

Seuls deux points, intrinsèquement manuels (rendu visuel sur fond d'écran réel, parcours de login navigateur
réel), restent à faire par l'utilisateur — ils ne bloquent pas le statut de la phase mais conditionnent
l'utilité pratique immédiate du login OAuth pour les phases suivantes.

---

*Verified: 2026-09-12*
*Verifier: Claude (gsd-verifier)*
