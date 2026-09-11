---
phase: 17-jeton-toujours-vivant-panne-toujours-visible
plan: 03
subsystem: auth
tags: [oauth, refresh-token, rotation, semaphore, backoff, ihostedservice, timer, iclock, xunit, tdd]

# Dependency graph
requires:
  - phase: 17-jeton-toujours-vivant-panne-toujours-visible
    provides: "17-02 : EtatAuthentification, IAuthStatus, ResultatRafraichissement/IssueRafraichissement, ChronosOAuthClient à horloge injectable"
  - phase: 17-jeton-toujours-vivant-panne-toujours-visible
    provides: "17-01 : la DÉCOUVERTE que le recul anti-429 du provider est inopérant sans cache d'usage"
  - phase: 16-fondations-du-delta
    provides: "IClock/FakeClock, motif d'isolation de chemin sous Path.GetTempPath()"
provides:
  - "ChronosTokenAuthority — autorité UNIQUE de jeton du processus : sémaphore, double-vérification, rotation persistée, classification, recul, état"
  - "TokenRefreshService — IHostedService à tick fixe 60 s, premier tick IMMÉDIAT, TickAsync public et déterministe"
  - "Première implémentation de IAuthStatus (le contrat posé sans implémentation par 17-02)"
  - "Une politique de recul qui vit DANS l'autorité, indépendante de tout cache d'usage — le défaut n° 3 de 17-01 est structurellement éradiqué"
  - "Helpers de test internal static (Autorite, CheminCoffreTemp, Sequence) réutilisables par 17-04 et 17-05"
affects: [17-04, 17-05, 18-source-entetes]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Autorité unique : retirer le droit d'agir à tout le monde sauf un, plutôt que coordonner N acteurs"
    - "Section critique UNIQUE autour de charger/décider/rafraîchir/persister/publier (un seul WaitAsync, un seul Release)"
    - "Double-vérification après acquisition du sémaphore : N appelants en attente ne produisent QU'UNE action"
    - "Prédicat pur d'horloge murale (internal static) plutôt que réveil calculé — immunise contre la mise en veille"
    - "Publication d'état sur TRANSITION seulement (garde d'égalité dans Publier)"
    - "Test de non-effacement : le livrable est ce que le code NE fait PAS"
    - "Garde de falsifiabilité dans un test de dégradation (Assert.ThrowsAny sur le Save avant d'asserter la survie)"

key-files:
  created:
    - src/Chronos/Services/ChronosTokenAuthority.cs
    - src/Chronos/Services/TokenRefreshService.cs
    - tests/Chronos.Tests/ChronosTokenAuthorityTests.cs
    - tests/Chronos.Tests/TokenRefreshServiceTests.cs
  modified: []

key-decisions:
  - "requirements-completed laissé VIDE : les deux types existent et sont prouvés, mais AUCUN n'est câblé dans le graphe DI — TOK-01 n'est pas vivant en production avant le plan 17-04"
  - "Le recul (backoff) est porté par l'autorité et ne consulte AUCUN cache : c'est la réponse directe au défaut n° 3 découvert au plan 17-01"
  - "SemaphoreSlim écrit avec son nom de type explicite (et non en new(1, 1) ciblé) pour satisfaire à la fois le code d'exemple du plan et son critère grep"
  - "Aucun commit RED séparé : la contrainte « solution compilable à chaque commit » prime sur la granularité TDD (un ProjectReference fait échouer TOUTE l'invocation dotnet test)"

patterns-established:
  - "Helpers de test partagés en internal static dans le fichier de la classe principale, plutôt que dupliqués : une seule garde de sécurité de chemin à maintenir"
  - "XML-doc qui cite le numéro d'issue amont (claude-code#25609, #54443) comme justification d'un invariant structurel"

requirements-completed: []

# Metrics
duration: 10min
completed: 2026-09-12
---

# Phase 17 Plan 03 : Autorité unique de jeton et horloge de fond — Summary

**Le droit de rafraîchir a été retiré à tout le monde sauf un : dix demandes concurrentes sur un jeton mort produisent désormais UNE seule rotation — et une horloge de fond à premier tick immédiat renouvelle le jeton sans que personne n'ait besoin de regarder le cadran.**

## Performance

- **Duration:** 10 min
- **Started:** 2026-09-11T22:19:35Z
- **Completed:** 2026-09-11T22:29:26Z
- **Tasks:** 2/2
- **Files modified:** 4 (4 créés, 0 modifié) — aucun fichier existant touché

## Accomplishments

- **`ChronosTokenAuthority` supprime la course de rotation AVANT qu'elle n'existe.** Le flux OAuth fait
  *tourner* le refresh token : deux rafraîchisseurs présentant le même jeton produisent un
  `invalid_grant` sur le second, donc — avec la table de classification livrée par 17-02 — une **fausse
  déconnexion sur un compte parfaitement sain**. C'est le bug ouvert `anthropics/claude-code#25609`
  (« no coordination … a classic refresh token rotation race condition ») : Claude Code lui-même s'y
  est fait prendre. Le sémaphore et la double-vérification rendent la situation *structurellement*
  impossible dans le processus, et le test `Dix_demandes_concurrentes_ne_declenchent_QU_UNE_rotation`
  le prouve (`SendCount == 1`, dix résultats identiques).
- **Le recul vit dans l'autorité, pas dans un cache — le défaut n° 3 de la Wave 0 est éradiqué, pas
  déplacé.** Le plan 17-01 avait découvert (hors plan de phase) que le garde-fou anti-429 du provider
  exige `now < _nextAllowedCall` **ET** `_cached is not null` ; comme `_cached` vit en RAM, il est vide
  à chaque démarrage de l'exe, et un exe qui redémarre avec un jeton mort **remartèle l'endpoint à
  chaque tick sans aucun frein** — entretenant le 429 qui l'a causé. C'est le cas nominal de la panne de
  deux mois. Le test `Le_recul_freine_des_le_PREMIER_429_sans_aucun_cache_prealable` grave le contraire :
  sur une instance qui n'a **jamais rien réussi**, le frein mord dès le premier échec.
- **Le coffre de l'utilisateur est déclaré intouchable, et c'est testé.** Zéro `.Clear()` dans
  l'autorité. Après un `400 invalid_grant`, `oauth.dat` existe toujours **et contient encore REF-1** —
  parade au faux positif serveur documenté (`claude-code#54443` : « OAuth sessions rejected by the
  server before the locally stored expiresAt time … refresh returns HTTP 400 »). Un login récupérable
  ne peut plus être détruit par une réponse serveur.
- **La rotation est persistée AVANT le retour du jeton, et un échec d'écriture ne tue pas la session.**
  La preuve vient du disque (`new ChronosOAuthStore(memeChemin).Load()!.RefreshToken == "REF-2"`), pas
  de la RAM. Et quand `Save` lève (fichier maintenu ouvert par un `FileStream`), `GetAccessTokenAsync`
  rend quand même `ACC-2` et publie `Connecte` : un échec d'**écriture** ne doit jamais se faire passer
  pour un échec de **rafraîchissement**.
- **Le rafraîchissement cesse d'être paresseux (TOK-01).** `TokenRefreshService` est un
  `IHostedService` calqué structurellement sur `DesktopUiaPollService`, tick fixe 60 s, **`dueTime`
  zéro**. Le cas réel de l'utilisateur — jeton mort depuis le 2026-07-12 — est traité **au lancement**,
  pas 60 s plus tard : attendre afficherait une pastille de déconnexion transitoire, exactement le faux
  signal que TOK-02 doit éviter. Et le tick ne lève **jamais** : une exception sur un thread du pool
  tuerait la boucle, donc le préventif, donc TOK-01.
- **445 → 484 tests, 0 échec** (cible du plan : ≥ 465). Aucun test supprimé, aucun fichier existant
  modifié, aucun paquet ajouté, coffre réel byte-identique.

## Task Commits

1. **Task 1 : `ChronosTokenAuthority`** — `9b0e252` (feat, TDD) — 189 lignes de production, 569 de test, **31 tests**
2. **Task 2 : `TokenRefreshService`** — `1eafdfc` (feat, TDD) — 95 lignes de production, 184 de test, **8 tests**

**Plan metadata:** voir le commit `docs(17-03)` final.

## Files Created/Modified

- `src/Chronos/Services/ChronosTokenAuthority.cs` *(créé, 189 lignes)* — `IAuthStatus` + `IDisposable`.
  Un `SemaphoreSlim(1, 1)`, **un seul** `WaitAsync`, **un seul** `Release` : la section critique est
  unique par construction. Sept membres publics, dont un seul rend un jeton (`Task<string?>`).
- `src/Chronos/Services/TokenRefreshService.cs` *(créé, 95 lignes)* — `IHostedService` + `IDisposable`,
  `Periode = 60 s`, `dueTime = TimeSpan.Zero`, `TickAsync()` public et non-levant, `lock (_gate)` +
  `_disposed` comme `DesktopUiaPollService`.
- `tests/Chronos.Tests/ChronosTokenAuthorityTests.cs` *(créé, 569 lignes, 31 tests)* — helpers
  `Autorite(...)` / `CheminCoffreTemp()` / `Sequence(...)` en `internal static`, réutilisés par le
  fichier de la tâche 2 (un seul point de construction, une seule garde de sécurité de chemin).
- `tests/Chronos.Tests/TokenRefreshServiceTests.cs` *(créé, 184 lignes, 8 tests)* — sondage borné
  `AttendreAsync` (motif `RefreshOrchestratorTests:19`), jamais de `Thread.Sleep` nu.

## Les sept invariants de l'autorité, et le test qui les tient

| Invariant | Test |
|---|---|
| Section critique unique (charger → décider → rafraîchir → persister → publier) | `grep -c "_verrou.WaitAsync"` → 1, `_verrou.Release` → 1 |
| Double-vérification : N appelants ⇒ 1 rotation | `Dix_demandes_concurrentes_ne_declenchent_QU_UNE_rotation` |
| Rotation persistée avant le retour | `La_rotation_est_persistee_AVANT_que_le_jeton_ne_soit_rendu` |
| Un `Save` en échec ne tue pas la session courante | `Un_echec_d_ecriture_du_coffre_ne_tue_PAS_la_session_courante` |
| Le coffre n'est JAMAIS effacé | `Un_refus_d_identifiants_n_efface_JAMAIS_le_coffre` + `grep .Clear()` → 0 |
| Le jeton ne sort que comme `string` d'access token | `Aucun_membre_public_de_l_autorite_ne_laisse_sortir_le_refresh_token` (réflexif) |
| Recul indépendant de tout cache d'usage | `Le_recul_freine_des_le_PREMIER_429_sans_aucun_cache_prealable` |

## Politique de recul livrée (et vérifiée pas à pas)

| Signal | Conséquence | Test |
|---|---|---|
| `400 invalid_grant` / `401 invalid_client` / `200` tronqué | `Deconnecte` + **verrou définitif** : 5 appels de plus, `SendCount` reste à 1 | `Chaque_signal_du_serveur_se_traduit_en_un_etat_distinct`, `Apres_un_refus_definitif_cinq_appels_de_plus_n_emettent_AUCUNE_requete` |
| `429 rate_limit_error` | **`HorsLigne`**, jamais `Deconnecte` — un rate-limit n'est pas une déconnexion | `Chaque_signal_du_serveur_se_traduit_en_un_etat_distinct` |
| `500` / `HttpRequestException` / `TaskCanceledException` | `HorsLigne` + fenêtre de recul | `Un_cable_debranche_dit_hors_ligne_et_jamais_deconnecte`, `Un_timeout_…` |
| Recul | 2 min → 4 → 8 → 16 → **plafond 30 min** ; remis à 2 min au premier succès | `Le_recul_initial_…`, `Le_recul_double_…`, `Le_recul_est_plafonne_a_trente_minutes`, `Un_succes_remet_le_recul_a_son_plancher` |
| Transitions | 3 échecs consécutifs ⇒ **un seul** `EtatChange` | `Une_transition_n_emet_qu_UN_evenement_meme_apres_trois_echecs` |

## Decisions Made

- **`requirements-completed` laissé VIDE**, pour la troisième fois de la phase — et pour une raison
  différente des deux premières. Ici les deux types existent et sont prouvés, mais **aucun n'est
  enregistré dans le graphe DI** : `App.xaml.cs` n'a pas été touché (le plan l'interdit explicitement,
  c'est 17-04 qui ouvre le câblage en même temps que le rebranchement du provider, pour un seul état
  cohérent du graphe). Un `TokenRefreshService` que le host ne démarre jamais ne rafraîchit rien :
  TOK-01 n'est pas *vivant* en production à ce commit. Cocher ici mentirait sur l'état du produit.
- **Le recul est une politique de l'AUTORITÉ, pas du provider.** C'est la décision de conception la plus
  conséquente du plan, et elle vient d'une découverte de la Wave 0 qui n'était pas au plan de phase. Un
  recul adossé à un cache d'usage disparaît à chaque démarrage de l'exe ; un recul adossé à l'autorité
  survit à tout ce qui n'est pas un redémarrage de processus — et c'est précisément au redémarrage que
  le martèlement se produisait.
- **`SignalerSucces()` relâche AUSSI `_refusDefinitif` et le recul**, pas seulement l'état publié. Sans
  cela, un 2xx obtenu par un autre chemin (la sonde d'en-têtes de la phase 18, par exemple)
  éteindrait la pastille tout en laissant le rafraîchissement verrouillé — un état incohérent où
  l'overlay dit « tout va bien » alors que l'autorité a renoncé.
- **Aucun commit RED séparé**, contrairement au flux TDD nominal. La contrainte du plan
  (« garder la solution compilable à chaque commit ») prime : `tests/Chronos.Tests` a un
  `ProjectReference` vers `Chronos`, donc une erreur de compilation *où que ce soit* fait échouer
  **toute** l'invocation `dotnet test`. La discipline TDD a bien été appliquée (phase RED exécutée et
  constatée : 1 × CS0246 pour la tâche 1, 8 × CS0246 pour la tâche 2), mais les deux phases sont
  réunies en un commit par tâche — exactement ce qu'a fait le plan 17-02 (`98236fd`, « feat, TDD »).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Critères d'acceptation contradictoires] Le code d'exemple du plan viole le `grep` du plan (`SemaphoreSlim`)**

- **Found during:** Task 1, à la vérification des critères d'acceptation
- **Issue:** Le bloc de code du plan prescrivait `private readonly SemaphoreSlim _verrou = new(1, 1);`
  (`new` ciblé), tandis que son critère exigeait
  `grep -c "SemaphoreSlim(1, 1)\|SemaphoreSlim(1,1)" → 1` — et son `key_link` le motif
  `SemaphoreSlim\(1, ?1\)`. Écrits tels quels, le code du plan rendait **0**. Troisième occurrence de
  cette classe de contradiction dans la phase (plan 17-01 : helper `new(...)` vs
  `grep "new ChronosOAuthUsageProvider("` ; plan 17-02 : XML-doc vs `grep` du nom disparu).
- **Fix:** Nom de type explicite — `new SemaphoreSlim(1, 1)`. Le critère **et** le `key_link` du plan
  sont désormais tous deux satisfaits, pour un coût nul (le code est même plus explicite).
- **Files modified:** `src/Chronos/Services/ChronosTokenAuthority.cs`
- **Verification:** `grep -cE 'SemaphoreSlim\(1, ?1\)'` → 1 ; 31 tests toujours verts après.
- **Committed in:** `9b0e252`

**2. [Rule 3 — Critère d'acceptation mis en échec par un artefact de `grep`] « Aucun jeton en clair » déclenché par une XML-doc**

- **Found during:** Vérification finale du plan (point 7)
- **Issue:** Le critère
  `grep -rniE "accessToken|refreshToken" src/Chronos --include=*.cs | grep -iE "log|Console|WriteLine|Append"`
  remontait `TokenRefreshService.cs:9`. Aucun jeton n'y est journalisé : la ligne de documentation
  contenait simplement « Hor**log**e » et « Get**AccessToken**Async », deux sous-chaînes qui satisfont
  les deux filtres par accident.
- **Fix:** Reflow de la XML-doc sur trois lignes plutôt que deux, ce qui sépare les deux mots. Aucun
  contenu documentaire perdu — préférable à consigner un « écart assumé » pour un faux positif.
  Commit de la tâche 2 amendé (rien n'était poussé).
- **Files modified:** `src/Chronos/Services/TokenRefreshService.cs`
- **Committed in:** `1eafdfc`

---

**Total deviations:** 2 auto-fixed (2 × Rule 3). Aucune n'a modifié le périmètre, aucun comportement
n'a changé. **Aucun bug** n'a été trouvé dans le code livré : les 31 tests de la tâche 1 et les 8 de la
tâche 2 sont passés au premier lancement.

## Écarts de critères d'acceptation assumés

- **Verification 7 (« aucun jeton en clair ») : une occurrence subsiste, `DiagnosticService.cs:220`.**
  Elle écrit le **nom** du champ (`claudeAiOauth.accessToken`) suivi de « PRÉSENT »/« absent »,
  **jamais la valeur**. Pré-existante, non touchée, déjà relevée aux plans 17-01 et 17-02. Hors
  périmètre (règle de portée : n'auto-corriger que ce que la tâche courante a causé).
- **Task 1, critère « ≥ 18 tests »** : 31 livrés. **Task 2, critère « ≥ 6 »** : 8 livrés.
- **Verification 2, total attendu ≥ 465** : 484.

## Issues Encountered

Aucune surprise de comportement. Deux prédictions fines du plan se sont vérifiées du premier coup :

- **Le test d'échec d'écriture.** Le plan laissait le choix de la technique (« choisir la variante qui
  fait effectivement lever `Save` sur la machine »). Retenu : maintenir `oauth.dat` ouvert via un
  `FileStream(FileMode.Open, FileAccess.Read, FileShare.Read)`. C'est le point d'équilibre exact —
  `File.ReadAllBytes` (donc `Load()`) demande `FileShare.Read` et passe encore, tandis que le
  `File.Move(tmp, _path, overwrite: true)` de `Save` ne peut plus remplacer une cible ouverte sans
  `FILE_SHARE_DELETE` et lève. Une **garde de falsifiabilité** (`Assert.ThrowsAny` sur un `Save` direct)
  a été ajoutée avant les assertions utiles : sans elle, le test passerait aussi si `Save` réussissait,
  et ne prouverait rien.
- **La non-levée du tick.** Le plan pariait qu'une `InvalidOperationException` échapperait au filtre
  nominatif de `ChronosOAuthClient.RefreshAsync` et remonterait jusqu'au service. Le test est vert ; il
  est d'ailleurs robuste dans les deux cas de figure (que `HttpClient` enveloppe l'exception ou non, le
  service se termine normalement).

## Vérification finale

| Contrôle | Résultat |
|---|---|
| `dotnet build Chronos.sln -v q --nologo` | **0 erreur** (2 avertissements xUnit2031 pré-existants dans `DesktopUiaSessionSourceTests`, hors périmètre) |
| `dotnet test Chronos.sln -v q --nologo` | **484 réussis / 0 échec** (baseline 445, cible ≥ 465) |
| `ChronosTokenAuthorityTests` | **31** tests, 0 échec (cible ≥ 18) |
| `TokenRefreshServiceTests` | **8** tests, 0 échec (cible ≥ 6) |
| `ServicesLayerPurityTests` + `CompositionRootTests` | 5/5 verts, fichiers **non modifiés** |
| `RefreshOrchestratorTests` + `DesktopUiaPollServiceTests` + `ChronosOAuthStoreTests` | 13/13 verts |
| `grep -c "\.Clear()"` dans les 2 fichiers de production | **0** |
| `grep -cE "SemaphoreSlim\(1, ?1\)"` / `_verrou.WaitAsync` / `_verrou.Release` | **1 / 1 / 1** |
| `grep -c "DateTimeOffset.UtcNow"` dans l'autorité | **0** (l'horloge est toujours injectée) |
| `grep -cE "Task\.Delay\|PeriodicTimer"` dans l'autorité | **0** (aucun réveil calculé) |
| `grep -n "System.Windows"` dans les 2 fichiers | **aucun résultat** (couche neutre) |
| `grep -c "public .*OAuthTokens"` dans l'autorité | **0** (le refresh token ne sort pas) |
| `git diff --name-only -- RefreshOrchestrator.cs App.xaml.cs` | **vide** (aucun câblage touché) |
| `git diff -- **/*.csproj` | **vide** (aucun paquet ajouté) |
| Coffre réel `%APPDATA%\Chronos\oauth.dat` | **518 o, mtime 1783863147 — identique avant ET après** |
| Réseau réel joint | aucun — 100 % `FakeHttpMessageHandler` |
| `git status --porcelain` | **vide** |

## Known Stubs

Aucun stub au sens « donnée factice câblée à un rendu » : ce plan ne touche ni vue, ni ViewModel.

En revanche, **les deux types livrés sont volontairement sans consommateur à ce commit** :

| Livrable | Câblé par | Pourquoi c'est intentionnel |
|---|---|---|
| `ChronosTokenAuthority` | plan **17-04** (`App.xaml.cs` + ctor de `ChronosOAuthUsageProvider`) | L'objectif du plan interdit explicitement tout câblage DI ici : le provider doit changer de constructeur *en même temps* que l'enregistrement du singleton, sinon `CompositionRootTests` garderait un graphe incohérent le temps d'un commit. |
| `TokenRefreshService` | plan **17-04** (`AddHostedService`) | Idem. Tant que le host ne le démarre pas, il ne rafraîchit rien — d'où `requirements-completed: []`. |

**Conséquence à assumer clairement : TOK-01 n'est pas encore vivant en production.** Le mécanisme
existe et est prouvé ; il n'est pas branché. C'est l'état honnête, et il ne bloque pas l'objectif de
*ce* plan, qui était de créer l'autorité et son horloge.

## Next Phase Readiness

- **17-04 (câblage + rebranchement du provider)** a exactement ce qu'il lui faut :
  `GetAccessTokenAsync` / `InvaliderAccessToken` / `SignalerSucces` / `SignalerRefusServeur` couvrent
  le Pattern 3b du RESEARCH (401 sur l'usage ⇒ invalider ⇒ un seul rejeu ⇒ sinon `Deconnecte`) sans
  aucune méthode à ajouter. Côté tests, le corps de `Provider(...)` dans
  `ChronosOAuthUsageProviderTests` reste le point de construction unique à réécrire, et les helpers
  `internal static` de `ChronosTokenAuthorityTests` (`Autorite`, `CheminCoffreTemp`, `Sequence`) sont
  directement réutilisables.
- **17-05 (pastille)** dispose désormais d'une implémentation réelle de `IAuthStatus` à injecter à côté
  du `FakeAuthStatus` livré par 17-02.
- **Point d'attention pour 17-04 :** le défaut n° 3 de la Wave 0 (recul 429 inopérant sans cache) est
  neutralisé **du côté du jeton** uniquement. Le garde-fou de `ChronosOAuthUsageProvider.cs:53` —
  `now < _nextAllowedCall` **ET** `_cached is not null` — est toujours là, inchangé, pour l'endpoint
  d'**usage**. Le rebranchement devra décider s'il le laisse tel quel (l'endpoint d'usage n'a pas la
  criticité de rotation du point de terminaison de jeton) ou s'il lui applique le même traitement. Le
  test `Un_429_sans_cache_ne_freine_aujourd_hui_absolument_rien` est toujours vert et documente encore
  le comportement actuel.

**Aucun blocage.** `TOK-01`, `TOK-02`, `TOK-03` restent à `Pending` dans `REQUIREMENTS.md`.

---
*Phase: 17-jeton-toujours-vivant-panne-toujours-visible*
*Completed: 2026-09-12*

## Self-Check: PASSED

- `src/Chronos/Services/ChronosTokenAuthority.cs` — présent (189 lignes, création confirmée)
- `src/Chronos/Services/TokenRefreshService.cs` — présent (95 lignes, création confirmée)
- `tests/Chronos.Tests/ChronosTokenAuthorityTests.cs` — présent (569 lignes, 31 tests)
- `tests/Chronos.Tests/TokenRefreshServiceTests.cs` — présent (184 lignes, 8 tests)
- `.planning/phases/17-.../17-03-SUMMARY.md` — présent
- Commits `9b0e252` et `1eafdfc` — présents dans l'historique
- Coffre réel `%APPDATA%\Chronos\oauth.dat` — **518 o, mtime 1783863147** : identique au relevé d'avant exécution
