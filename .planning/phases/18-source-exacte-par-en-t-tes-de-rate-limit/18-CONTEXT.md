# Phase 18: Source exacte par en-têtes de rate-limit - Context

**Gathered:** 2026-09-12
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Chronos dispose d'une source exacte supplémentaire, en tête de chaîne, qui répond **même quand l'API est en
429** — précisément l'instant où l'overlay sert le plus — et qui apporte deux informations que personne
d'autre ne donne : le statut serveur et le dépassement.

Exigences couvertes : HDR-01 (usage exact via les en-têtes d'une requête jetable), HDR-02 (en-têtes exploités
même sur 429), HDR-03 (statut serveur remonté), HDR-04 (dépassement lu), HDR-05 (unités normalisées en un
point unique), HDR-06 (cadence bornée + coût annoncé).

**Hors périmètre :** la refonte de la doctrine du composite (phase 19) et la distinction visuelle
frais / daté / indisponible du cadran (phase 20). Ici on AJOUTE un `IUsageProvider` en tête de chaîne ;
on ne change pas encore la règle de choix de source.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
Tous les choix d'implémentation sont à la discrétion de Claude — la discussion est désactivée
(`workflow.skip_discuss=true`).

### Technique reprise de claude-session-browser (vérité-terrain, ne pas ré-enquêter)
Source : `github.com/juppeee/claude-session-browser`, `clawdmeter.py:150` (`poll_usage_meta`).

Requête : `POST https://api.anthropic.com/v1/messages`
En-têtes de requête : `Authorization: Bearer <access token>`, `anthropic-beta: oauth-2025-04-20`,
`anthropic-version: 2023-06-01`, `User-Agent: claude-code/<version>`, `Content-Type: application/json`.
Corps : modèle Haiku le moins cher, `max_tokens` = 1, un seul message utilisateur trivial.

En-têtes de RÉPONSE exploités :
- `anthropic-ratelimit-unified-5h-utilization` et `-7d-utilization` — **utilization en 0..1**
- `anthropic-ratelimit-unified-5h-reset` et `-7d-reset` — **epoch SECONDES**
- `anthropic-ratelimit-unified-5h-status` — `allowed` / `allowed_warning` / `rejected`
- variante `anthropic-ratelimit-unified-overage-utilization` / `-overage-reset` / `-status` — dépassement

**Point décisif (HDR-02)** : le code d'origine récupère les en-têtes MÊME sur une erreur HTTP
(`except HTTPError: hdrs = e.headers`) — un 429 porte donc quand même les chiffres. C'est l'avantage
structurel sur `/api/oauth/usage`, qui ne renvoie rien d'utile en erreur.
Un 401/403 en revanche ne porte pas d'en-têtes exploitables : dégrader.

### PIÈGE D'UNITÉS (HDR-05) — trois unités pour la même donnée

| Source | Champ | Unité |
|---|---|---|
| En-têtes `anthropic-ratelimit-unified-*` | `utilization` | **0..1** |
| `GET /api/oauth/usage` | `utilization` | **0..100** |
| Pont statusLine (`rate_limits`) | `used_percentage` | **0..100** |

`resets_at` : **epoch secondes** pour les en-têtes et le pont statusLine, **ISO 8601** pour
`/api/oauth/usage`. La normalisation doit se faire en **un point unique**.

### SÉCURITÉ — contraintes absolues
- **Ne JAMAIS appeler l'endpoint de refresh avec le refresh token RÉEL** de `%APPDATA%\Chronos\oauth.dat` :
  le flux OAuth fait tourner le refresh token, un appel non re-sauvegardé invaliderait définitivement le
  login de l'utilisateur. Contrôle avant/après : `oauth.dat` doit rester **518 octets, mtime 1783863147**.
- **Ne JAMAIS émettre de vraie requête réseau depuis un test.** Tout par `FakeHttpMessageHandler`.
  Note : le jeton de l'utilisateur est de toute façon expiré depuis le 2026-07-12, donc une sonde réelle
  échouerait — mais l'interdiction tient indépendamment de cela.
- Le jeton ne vit qu'en variable locale, le temps de construire l'en-tête `Authorization`. Jamais logué,
  écrit, mis en exception ni concaténé dans une URL.
- Le corps de la réponse HTTP ne doit **jamais** être transporté ni journalisé.

### Contraintes projet
- MVVM strict, DI, dossiers Models/Views/ViewModels/Services.
- Aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`).
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin, aucune dépendance native.
- Dégradation totale : réseau, timeout, 401/403, en-têtes absents ou illisibles → jamais de crash,
  jamais de valeur inventée.
- UI et commentaires en **français**.
- **Baseline à l'entrée de phase : 505 tests xUnit verts.**

### Coût de la sonde (HDR-06) — à assumer et à annoncer
Chaque appel consomme une **vraie micro-requête** sur le compte (`max_tokens` = 1 sur le modèle le moins
cher, une dizaine de jetons d'entrée). La cadence doit être bornée et le coût indiqué honnêtement dans les
réglages. Ordre de grandeur de référence chez l'auteur d'origine : un sondage toutes les 300 s, avec repli
à 60 s après échec.

</decisions>

<code_context>
## Existing Code Insights

### Acquis des phases précédentes, à CONSOMMER et non à redupliquer
- `src/Chronos/Services/ChronosTokenAuthority.cs` (phase 17) — **autorité UNIQUE du jeton** :
  `GetAccessTokenAsync`, sémaphore, double-vérification, rotation persistée, verrou de refus définitif,
  recul indépendant de tout cache. La sonde d'en-têtes DOIT s'y brancher — **ne jamais créer un troisième
  rafraîchisseur**, ce serait une course de rotation du refresh token et donc une fausse déconnexion.
- `src/Chronos/Services/IAuthStatus.cs` / `EtatAuthentification` (phase 17) — vocabulaire d'état
  (`NonConnecte` / `Connecte` / `HorsLigne` / `Deconnecte`) et canal de remontée vers le ViewModel.
- `src/Chronos/Services/LastExactUsageProvider.cs` + `LastExactStore.cs` (phase 16) — décorateur en tête de
  chaîne qui persiste le dernier relevé exact. La nouvelle source doit se placer de façon cohérente avec lui.
- `src/Chronos/Services/TranscriptActivityProvider.cs` (phase 16) — source de delta, hors chaîne.

### Fichiers de référence pour le motif de provider
- `src/Chronos/Services/ChronosOAuthUsageProvider.cs` — motif le plus proche : throttle, recul sur 429,
  cache, tolérance totale, lecture d'`utilization` et de `resets_at`. **Attention** : il lit
  `/api/oauth/usage` (utilization 0..100, resets_at ISO 8601) ; la nouvelle sonde lit des EN-TÊTES
  (utilization 0..1, reset epoch secondes). C'est exactement le piège HDR-05.
- `src/Chronos/Services/ClaudeUsageObjectProvider.cs` — lecture du pont statusLine (`used_percentage`
  0..100, `resets_at` epoch secondes).
- `src/Chronos/Services/CompositeUsageProvider.cs` — **NE PAS MODIFIER** : la refonte de la doctrine est la
  phase 19.
- `src/Chronos/Models/UsageSnapshot.cs`, `WindowState.cs`, `SourceReliability.cs`, `WindowKind.cs`.
- `src/Chronos/App.xaml.cs` — composition root, chaîne de providers.
- `src/Chronos/Services/DiagnosticService.cs` — doit pouvoir nommer cette nouvelle source.
- `src/Chronos/Views/SettingsWindow.xaml` — surface où annoncer le coût de la sonde (HDR-06).

### Briques de test réutilisables
`tests/Chronos.Tests/Fakes/FakeHttpMessageHandler.cs` (permet de fabriquer des réponses porteuses
d'en-têtes, y compris sur un 429), `FakeClock.cs`, `ChronosOAuthUsageProviderTests.cs` (motif de test de
provider HTTP, posé en phase 17).

</code_context>

<specifics>
## Specific Ideas

Le statut serveur (HDR-03) et le dépassement (HDR-04) sont des informations que **aucune autre source ne
fournit**. Il faut donc les porter dans le modèle sans casser les providers existants qui ne les connaissent
pas : champs optionnels, jamais inventés.

</specifics>

<deferred>
## Deferred Ideas

- Trou visuel dans la `UniformGrid` des réglages (3 boutons, `Columns="2"`, cellule bas-droite vide) hérité
  de la phase 16 — **reporté à la phase 20**.
- Préavis avant saturation (~90 %) et notification au reset — hors périmètre v1.5 (Future Requirements).

</deferred>
