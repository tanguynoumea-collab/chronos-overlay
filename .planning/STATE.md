---
gsd_state_version: 1.0
milestone: v1.5
milestone_name: — Exactitude permanente
status: executing
stopped_at: Completed 18-02-PLAN.md
last_updated: "2026-09-12T01:02:52.579Z"
last_activity: 2026-09-12
progress:
  total_phases: 6
  completed_phases: 3
  total_plans: 18
  completed_plans: 14
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-09-09)

**Core value:** Voir instantanément, sans terminal ni `/usage`, combien de quota et de temps il reste sur les
deux fenêtres — sans jamais présenter une estimation comme un chiffre exact.
**Current focus:** Phase 18 — Source exacte par en-tetes de rate-limit

## Current Position

Milestone: v1.5 — Exactitude permanente (6 phases : 15 → 20)
Phase: 18 (Source exacte par en-tetes de rate-limit) — EXECUTING
Plan: 3 of 6
Status: Ready to execute
Last activity: 2026-09-12

Progress: [░░░░░░░░░░] 0% (0/6 phases)

**Ordre d'exécution :** 15 (indépendante) → 16 (fondations : persistance + delta + démolition des plafonds)
→ 17 (jeton vivant) → 18 (source en-têtes) → 19 (doctrine du composite, exige 16 et 18) → 20 (rendu visible).

| Phase | Titre | Requirements | Statut |
|-------|-------|--------------|--------|
| 15 | Idempotence des intégrations | PUR-01..03 | Complete (3/3) |
| 16 | Fondations du delta — persistance & démolition des plafonds | EXA-01, DEL-01, DEL-02, DEL-05, DEL-06 | Complete (4/4) |
| 17 | Jeton toujours vivant, panne toujours visible | TOK-01..03 | Complete (5/5) |
| 18 | Source exacte par en-têtes de rate-limit | HDR-01..06 | Not started |
| 19 | Nouvelle doctrine du composite | EXA-02, EXA-04, EXA-05, DEL-03, DEL-04 | Not started |
| 20 | Honnêteté visible — cadran & diagnostic | EXA-03, EXA-06 | Not started |

## Performance Metrics

**Velocity:**

- Total plans completed (v1.5): 0
- Average duration: —
- Total execution time: 0 h

*Updated after each plan completion*

## Milestone v1.4 (clos)

v1.4 validé en UAT app-réelle le 2026-07-11 (phases 13-14, 5 plans, 327 tests verts). Livré hors GSD depuis :
refonte visuelle des deux overlays + 3 thèmes, cadran deux modes, exe v2.8.1.

## Accumulated Context

### Contexte technique v1.5 (diagnostic DÉJÀ ÉTABLI — ne pas re-enquêter)

Diagnostic mené le 2026-09-09 sur la machine réelle. Cause racine des pourcentages faux après le passage
Max x5 → Max x20 : **les trois sources exactes sont tombées en même temps et le composite dégrade en silence.**

- **Jeton OAuth Chronos expiré le 2026-07-12.** `GET https://api.anthropic.com/api/oauth/usage` → **HTTP 401**
  (vérifié). `ChronosOAuthUsageProvider` rafraîchit paresseusement, l'échec est totalement muet côté UI.

- **`%APPDATA%/Chronos/usage.json` figé au 2026-07-10**, contenu
  `{"five_hour":{"used_percentage":10,"resets_at":9}}` — `resets_at: 9` = epoch 1970. Le pont statusLine n'est
  visiblement jamais invoqué par l'app de bureau. `ClaudeUsageObjectProvider` n'applique **aucune limite d'âge**
  et renvoie ça marqué `Exact`.

- **`CompositeUsageProvider.Best()` classe uniquement par fiabilité** (`Exact > Estimated > Unavailable`) :
  une donnée « exacte » de deux mois bat donc une estimation fraîche.

- **`ChronosOAuthUsageProvider._cached` est un champ d'instance (RAM)** → à chaque démarrage de l'exe, aucun
  chiffre exact en mémoire, bascule immédiate sur l'estimation. Cas le plus fréquent, jamais identifié.

- **`MainViewModel.cs:269` calcule `IsStale`** (seuil 2 min) mais la propriété n'est **bindée nulle part**.
- **`settings.json`** : `FiveHourTokenBudget=230000000` en source `Manual` (donc `BudgetCalibration.ApplyAuto`
  refuse par conception de l'écraser → gelé à vie), `WeeklyTokenBudget=5817635413` calibré sous Max x5.

- **`BudgetAutoCalibrator` a un défaut logique** : il ne calibre que lorsqu'une source *exacte* est présente,
  c'est-à-dire précisément quand l'estimation ne sert pas.

- **`~/.claude/settings.json` contient 25 hooks Chronos au lieu de 5** (v2.5, v2.5.1, v2.6, v2.8.1 + le build
  Debug), aucun installateur ne retire les précédents. Le `statusLine` pointe un chemin `Downloads` en dur.

- **`.credentials.json` de Claude Code ne contient que `mcpOAuth`** sur cette machine (pas de
  `claudeAiOauth.accessToken`) : la lecture de jeton « à la claude-session-browser » n'y marcherait pas telle
  quelle ; il faut passer par le jeton propre de Chronos.

**Technique reprise de `github.com/juppeee/claude-session-browser`** (`clawdmeter.py:150`, `poll_usage_meta`) :

```
POST https://api.anthropic.com/v1/messages
  Authorization: Bearer <token>
  anthropic-beta: oauth-2025-04-20
  User-Agent: claude-code/<version>
  body {"model":"claude-haiku-4-5-...","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}
→ en-têtes anthropic-ratelimit-unified-{5h,7d}-{utilization,reset}, -5h-status, et variante -overage-*
```

Ils récupèrent les en-têtes **même sur un 429** (`except HTTPError: hdrs = e.headers`) — c'est l'avantage
décisif sur `/api/oauth/usage`.

**PIÈGE D'UNITÉ — trois unités pour la même donnée, à normaliser en un seul point :**

| Source | Champ | Unité |
|---|---|---|
| En-têtes `anthropic-ratelimit-unified-*` | `utilization` | **0..1** |
| `GET /api/oauth/usage` | `utilization` | **0..100** |
| Pont statusLine (`rate_limits`) | `used_percentage` | **0..100** (= `utilization*100`, confirmé dans le binaire `claude-2.1.87`) |

`resets_at` : **epoch secondes** pour les en-têtes et le pont statusLine, **ISO 8601** pour `/api/oauth/usage`.

**Fondement de la décision « plus d'estimation absolue »** : les limites Anthropic pondèrent par modèle (une
heure d'Opus ne pèse pas comme une heure de Haiku), donc `tokens / plafond` restera faux même avec le bon
plafond. Les transcripts ne peuvent répondre qu'à deux questions bornées : *activité depuis T ?* et
*tokens depuis T ?* — d'où la correction par delta.
| Phase 15 P01 | 12min | 3 tasks | 3 files |
| Phase 15 P02 | 7min | 3 tasks | 6 files |
| Phase 15 P03 | 20min | 4 tasks | 4 files |
| Phase 16 P01 | 24min | 3 tasks | 20 files |
| Phase 16 P02 | 9min | 2 tasks | 6 files |
| Phase 16 P03 | 29min | 3 tasks | 12 files |
| Phase 16 P04 | 24 | 3 tasks | 7 files |
| Phase 17 P01 | 18 | 2 tasks | 2 files |
| Phase 17 P02 | 7min | 2 tasks | 6 files |
| Phase 17 P03 | 10min | 2 tasks | 4 files |
| Phase 17 P04 | 26min | 2 tasks | 7 files |
| Phase 17 P05 | 19min | 3 tasks | 9 files |
| Phase 18 P01 | 14min | 3 tasks | 8 files |
| Phase 18 P02 | 18min | 3 tasks | 10 files |

### Decisions

- [v1.5/roadmap]: **6 phases, numérotées 15 → 20** (continuité après la phase 14 de v1.4). Couverture
  24/24 requirements, aucun orphelin, aucun doublon.

- [v1.5/roadmap]: **EXA-01 (persistance du dernier relevé exact) est placé en Phase 16, avant la refonte du
  composite (Phase 19)** : la correction par delta (DEL-03/04) est impossible sans un instant T de référence
  persisté.

- [v1.5/roadmap]: **DEL-05 (démolition des plafonds) et DEL-01/02 (JSONL en source de delta) sont dans la même
  phase (16)** : ils touchent le même code et cassent les mêmes tests ; les séparer produirait un état
  intermédiaire non compilable. DEL-06 (migration des réglages) les accompagne pour la même raison.

- [v1.5/roadmap]: **HDR-01..06 en Phase 18, AVANT la refonte du composite (Phase 19)** : la source en-têtes
  est indépendante (un `IUsageProvider` de plus en tête de chaîne), mais la placer avant évite de rétrofitter
  sa place dans la nouvelle doctrine.

- [v1.5/roadmap]: **TOK-01..03 en Phase 17, avant HDR** : sans jeton vivant, ni l'endpoint OAuth existant ni
  la sonde d'en-têtes ne répondent — le jeton conditionne l'utilité réelle de la Phase 18.

- [v1.5/roadmap]: **PUR-01..03 en Phase 15** : totalement indépendant du pipeline d'usage, court, plaçable
  n'importe où ; placé en tête pour assainir le terrain de mesure tout de suite.

- [v1.5/roadmap]: **Phases 17 et 20 marquées « UI hint: yes »** (pastille de déconnexion cliquable TOK-02/03 ;
  distinction visuelle frais / daté / indisponible EXA-03). Les autres phases sont purement services/données.
  Note : `ui_phase: false` dans config.json — l'indice est consigné mais aucune phase UI dédiée n'est déclenchée.

- [v1.5/roadmap]: Toute phase doit conserver vertes les **327 tests xUnit**, la garde de pureté
  `ServicesLayerPurityTests` et la garde de composition `CompositionRootTests`.

- [v1.4/roadmap]: **2 phases à dépendance forte** — la source UIA (Phase 13) avant l'hystérésis (Phase 14).
  La distinction Chat/Cowork/Code et l'acquittement par focus (NET-02) EXIGENT l'arbre UIA ; la branche
  « répondu » (NET-01) roule déjà sur les sources actuelles. On ne peut donc pas inverser l'ordre.

- [v1.4/roadmap]: Phase 13 = BUR-01..05 + ROB-06 + ROB-07 (7 req) ; Phase 14 = NET-01..04 (4 req).
- [v1.4/roadmap]: Les deux phases sont **UI hint: yes** (surface visuelle WPF du widget sessions).
- [Phase 13]: [13-02] WindowsUiaTreeProvider propage AutomationId (dont l'ancre RootWebArea) → foreground reconnu en PROD, pas seulement en test. Assemblies UIA fournies implicitement par UseWPF (aucun <Reference>, 0 warning). Poll de fond via Timer .NET hors thread UI (ROB-07).
- [Phase 13]: 13-03: source bureau fusionnée dans SessionMonitor.Read (ISessionSource? optionnel, 4e arg non cassant), après transcripts+hooks, avant archived, non bloquant (ROB-07)
- [Phase 13]: 13-03: garde DI réelle (CompositionRootTests) reproduisant la sous-chaîne bureau d'App.xaml.cs → attrape un service manquant/mal ordonné qui ne planterait qu'au démarrage
- [Phase 14]: [14-01] Réversibilité NET-03 portée par le tracker (purge sur nouvel épisode d'attente), PAS par une comparaison ts>=UpdatedAt dans le filtre (fausse pour le bureau: UpdatedAt==now à chaque poll). Horodatage d'épisode maintenu par le tracker.
- [Phase 14]: [14-02] Focus premier-plan OS réel (WindowsForegroundWatch, Win32 GetForegroundWindow, titre « Claude ») injecté 7e param de SessionMonitor => branche NET-02 VIVANTE en prod. Best-effort ne lève jamais ; couche neutre (aucun HWND public). NET-01..04 tous couverts, phase 14 complète, milestone v1.4 prêt pour audit.
- [Phase 15]: [15-01] Une entree Chronos s'identifie par le MARQUEUR d'argument (--hook/--statusline) + le NOM de fichier Chronos*.exe, JAMAIS par le chemin : le chemin est la variable qui a produit 25 groupes de hooks au lieu de 5.
- [Phase 15]: [15-01] ParseOrNull renvoie null (= NE RIEN ECRIRE) et jamais un objet vide : le repli 'objet vide' des installateurs actuels ecrase integralement le settings.json de l'utilisateur. Catch LARGE + materialisation forcee (_ = o.Count) car les cles dupliquees levent une ArgumentException, pas une JsonException.
- [Phase 15]: [15-01] Appartenance (IsChronosCommand) et fraicheur (PointsToExe) sont deux predicats distincts ; Serialize impose UnsafeRelaxedJsonEscaping pour ne pas mutiler les accents des valeurs des autres outils.
- [Phase 15]: [15-02] L'installation ne cherche plus si un groupe Chronos existe : elle retire TOUS les groupes Chronos puis ajoute le sien. Idempotent par construction — la robustesse ne depend plus de l'exactitude du predicat mais de la forme de l'algorithme.
- [Phase 15]: [15-02] TransformForUninstall / Uninstall perdent exePath des deux cotes : le retrait est volontairement LARGE (toutes les versions), sinon desactiver depuis une nouvelle version laisserait les hooks et la barre de toutes les anciennes.
- [Phase 15]: [15-02] statusLine est mute sur la seule cle command (padding et cles futures survivent) ; ApplyStatusLine repointe mais n'installe JAMAIS — le consentement reste porte par le menu, pas par la reconciliation.
- [Phase 15]: [15-03] La comparaison avant/apres se fait sur la forme NORMALISEE (serialiser avant/apres mutation), jamais sur le texte lu : une simple reindentation de Claude Code declencherait sinon une ecriture ET une sauvegarde a chaque demarrage, evincant la sauvegarde du premier passage — la seule a contenir l'etat pre-purge complet.
- [Phase 15]: [15-03] Sauvegarde BLOQUANTE (pas d'ecriture sans sauvegarde reussie) mais retention NON bloquante ; le reconciliateur ne cree jamais le fichier absent — purge et repointage seulement, jamais d'installation silencieuse.
- [Phase 15]: [15-03] Point d'appel unique dans App.OnStartup, apres window.Show() et avant OfferOnFirstRun() : atteint en mode overlay UNIQUEMENT, donc jamais 5 processus --hook concurrents en lire-modifier-ecrire.
- [Phase 16]: [16-01] Le recul 405 -> 384 tests est le LIVRABLE : les 21 tests supprimes couvraient exclusivement du code supprime (3 CalibrateBudgets, 3 BudgetAutoCalibrator, 15 BudgetCalibration). Critere = 0 echec + justification nominative, PAS >= 405.
- [Phase 16]: [16-01] BudgetSource.cs et les 6 champs de ChronosSettings laisses INTACTS : ChronosSettings les reference encore, les supprimer ici casserait la compilation. Verifie : exactement 3 occurrences de BudgetSource dans src/ -> le plan 16-04 a bien son objet.
- [Phase 16]: [16-01] dotnet clean ne nettoie QUE la configuration courante (Debug) : 2 des 3 copies obj/**/BudgetDialog.g.cs survivaient. Apres suppression d'un .xaml, nettoyer Debug ET Release puis purger explicitement les *.g.cs orphelins.
- [Phase 16]: [16-02] Une fenetre n'est persistable que si elle est Exact ET porte utilization ET resets_at : sans resets_at, ni detection de la remise a zero ni recalcul de la geometrie -> chiffre invérifiable. Meme regle au chargement.
- [Phase 16]: [16-02] Reliability et FractionTimeRemaining ne sont JAMAIS persistees : la premiere est forcee a Exact a la reconstruction (un fichier edite a la main ne peut pas injecter une fausse exactitude), la seconde est recalculee depuis ResetsAt et l'instant present (la persister ressusciterait une geometrie perimee).
- [Phase 16]: [16-02] La relecture de fusion de Save est BRUTE (sans la garde resets_at <= now) : seule Load invalide une fenetre roulee, la fusion ne doit jamais purger silencieusement.
- [Phase 16]: [16-02] Le decorateur LastExactUsageProvider rebouche UNIQUEMENT les fenetres Unavailable (jamais Estimated) et n'ecrit que de l'Exact : portee volontairement la plus etroite possible, la doctrine de choix de source reste la phase 19.
- [Phase 16]: [16-03] La source de delta n'implemente PAS IUsageProvider et un test reflexif l'interdit : EXA-04 devient structurel des la phase 16, pas seulement documente. Rebrancher les transcripts dans la chaine casse le test.
- [Phase 16]: [16-03] Le contrat de delta separe l'E/S (ReadAsync, une seule passe disque) de la requete (Since(t), PURE, appelable N fois) : les deux fenetres 5 h et hebdo ont des bornes basses differentes mais repondent depuis un SEUL instantane disque, donc coherentes par construction.
- [Phase 16]: [16-03] Horizon/Covers font partie du contrat : une interrogation anterieure au filtre mtime de 8 j est declaree NON COUVERTE plutot que servie sous-evaluee. La constante 8 j est declaree UNE fois (cutoff du scan = horizon annonce, ils ne peuvent pas diverger).
- [Phase 16]: [16-03] Borne basse STRICTEMENT exclusive, borne haute inclusive : le message pose exactement sur l'instant du releve exact a deja ete compte par le serveur, le recompter le compterait deux fois.
- [Phase 16]: [16-03] L'isolation des chemins n'a PAS stabilise Host_resout_et_dispose_les_singletons : la cause reelle est une course du chargeur BAML de WPF (WpfXamlType.FindKnownMember) sous parallelisme xUnit. Correctif : collection 'XAML WPF' DisableParallelization sur les 4 classes qui chargent du XAML.
- [Phase 16]: DEL-06 livré comme TEST, pas comme code : System.Text.Json ignore par défaut les membres non mappés (skip au niveau du LECTEUR, avant conversion) — un migrateur aurait été du code mort pour un problème inexistant.
- [Phase 16]: La fixture DEL-06 est une copie octet pour octet du settings.json RÉEL de production : une fixture régénérée par sérialisation ne contiendrait pas les champs obsolètes et ne prouverait rien.
- [Phase 16]: La garde de non-retour Budget* porte sur le NOM des types et sa falsifiabilité a été vérifiée en exécution (type témoin introduit, échec constaté, témoin retiré).
- [Phase 17]: [17-01] Wave 0 : 3 defauts d'avant-phase graves en tests VERTS (401 muet, refresh paresseux, recul 429 inoperant sans cache). Ils seront REECRITS par 17-02/17-04, jamais supprimes — trace executable du bug avant/apres.
- [Phase 17]: [17-01] DEFAUT NON PREVU AU PLAN : le garde-fou anti-429 (ChronosOAuthUsageProvider.cs:53) exige 'now < _nextAllowedCall ET _cached is not null'. _cached vivant en RAM, il est vide a CHAQUE demarrage de l'exe : le recul 429 ne freine alors rien du tout. 17-04 doit porter le backoff dans l'autorite de jeton, independamment de tout cache d'usage.
- [Phase 17]: [17-01] TOK-01/TOK-02 laisses Pending malgre le frontmatter du plan : la Wave 0 ne livre que la couverture et prouve au contraire que les requirements ne sont PAS satisfaits. Ils seront coches par les plans qui livrent le correctif.
- [Phase 17]: [17-02] La cause d'un echec de refresh est desormais typee : 429 rate_limit_error = EchecTemporaire (jamais 'deconnecte' — ce serait une fausse alerte sur un compte sain), 200 a corps inexploitable = IdentifiantsRejetes (la rotation a deja eu lieu cote serveur, reessayer est vain).
- [Phase 17]: [17-02] ResultatRafraichissement n'expose AUCUNE propriete string, et un test reflexif (GetProperties) l'impose : un corps d'erreur peut echoiser la requete, donc le refresh token. La securite est structurelle, pas conventionnelle.
- [Phase 17]: [17-02] EtatAuthentification a 4 valeurs et pas 3 : HorsLigne et Deconnecte sont irreductibles. IAuthStatus copie le motif RefreshOrchestrator.SnapshotChanged (le service expose l'event, l'abonne marshalle) — ServicesLayerPurityTests reste vert sans toucher son allow-list.
- [Phase 17]: [17-02] TOK-01/TOK-02 laisses Pending : ce plan ne cree ni autorite, ni service de fond, ni pastille. IAuthStatus et EtatAuthentification sont des contrats sans implementation a ce commit — intentionnel, 17-03 implemente, 17-05 consomme.
- [Phase 17]: [17-03] Autorite UNIQUE de jeton : SemaphoreSlim(1,1) + double-verification => N demandes concurrentes produisent UNE rotation, jamais N. Parade structurelle a la course de rotation du refresh token (bug claude-code#25609) qui fabriquerait une FAUSSE deconnexion sur un compte sain.
- [Phase 17]: [17-03] Le recul (backoff 2->30 min) vit DANS l'autorite et ne consulte AUCUN cache d'usage : c'est la reponse directe au defaut n.3 decouvert par 17-01 (le garde-fou du provider exige un _cached en RAM, donc vide a chaque demarrage de l'exe). Un recul adosse a un cache disparait precisement au redemarrage, c'est-a-dire quand le martelement se produit.
- [Phase 17]: [17-03] Aucun .Clear() dans l'autorite, et c'est teste : apres un 400 invalid_grant, oauth.dat existe toujours ET contient encore l'ancien refresh token. Parade au faux positif serveur documente (claude-code#54443). Un Save en echec ne fait pas croire a un echec de refresh : les jetons neufs restent en memoire.
- [Phase 17]: [17-03] TokenRefreshService : tick FIXE 60 s + predicat pur d'horloge murale, dueTime ZERO (premier tick immediat), TickAsync public et non-levant. Pas de reveil calcule sur ExpiresAt (casse a la mise en veille) ; RefreshOrchestrator (horloge DONNEES) laisse intact.
- [Phase 17]: [17-03] TOK-01/TOK-02 laisses Pending : les deux types existent et sont prouves (39 tests) mais AUCUN n'est enregistre dans le graphe DI. Un TokenRefreshService que le host ne demarre jamais ne rafraichit rien — le cablage est le plan 17-04, en un seul commit avec le rebranchement du provider.
- [Phase 17]: [17-04] UN SEUL rafraichisseur : le provider d'usage perd _store/_client et son refresh paresseux dans le MEME commit ou l'autorite est cablee. Une demi-mesure aurait fait coexister deux rotations du refresh token, donc un invalid_grant sur la seconde, donc une FAUSSE deconnexion sur un compte sain (claude-code#25609).
- [Phase 17]: [17-04] Le rejeu sur 401 n'est pas un raffinement mais une obligation : le serveur peut revoquer un jeton AVANT son ExpiresAt local (claude-code#54443), donc le preventif seul ne suffit jamais. UN SEUL rejeu (garde locale non rearmable) ; 403 sans rejeu ; 429/5xx/reseau ne declarent JAMAIS Deconnecte.
- [Phase 17]: [17-04] Le garde-fou de debit du provider ne consulte PLUS le cache : 'ai-je le droit d'appeler' est decouple de 'ai-je un cache a servir'. Le cache vivant en RAM, le frein disparaissait a chaque demarrage de l'exe — precisement quand le martelement se produit (defaut n.3 de 17-01, dernier segment non corrige).
- [Phase 17]: [17-04] IAuthStatus est un ALIAS de l'instance d'autorite (jamais une seconde) et TokenRefreshService est enregistre ET heberge : la garde DI le prouve par Assert.Same + Assert.Contains sur IHostedService, ce qu'un dotnet build ne voit pas. TOK-01 fonctionnellement acquis, seule la visibilite manque (17-05).
- [Phase 17]: [17-04] DiagnosticService : parametre IAuthStatus OPTIONNEL en DERNIERE position => 9 sites de construction preexistants compilent sans retouche. 'Le fichier oauth.dat existe' n'a jamais voulu dire 'authentifie' : le rapport nomme desormais l'etat reel, HORS LIGNE (informatif) distingue de DECONNECTE (actionnable).
- [Phase 17]: [17-05] La pastille porte une commande DEDIEE (ReconnecterCommand) et JAMAIS LoginClaudeCommand : cette derniere bascule sur IsLoggedIn == _store.Exists, vrai meme avec un jeton expire -> un clic aurait SUPPRIME le coffre de jetons. Verrouille a deux niveaux : LogoutCount == 0 cote VM, et un [WpfFact] qui compare l'instance de commande reellement bindee dans le XAML.
- [Phase 17]: [17-05] Deux booleens de pastille et non un enum binde : Deconnecte est ACTIONNABLE (ambre, cliquable), HorsLigne est INFORMATIF (gris, inerte). Etat applique DES le ctor : sur cette machine le jeton est mort depuis le 2026-07-12, l'autorite est deja en echec au demarrage et n'emettra aucune transition. NonConnecte n'allume rien (EXA-05, phase 19).
- [Phase 17]: [17-05] ReinitialiserApresLogin a enfin un appelant (aucun depuis 17-02), suivi de RequestRefresh : sans quoi le verrou Deconnecte et le recul restaient poses et la pastille survivait a sa propre reparation. L'ORDRE des deux est reel en production mais INOBSERVABLE en test (RequestRefresh ne fait qu'empiler, consommation asynchrone) : declare non couvert plutot que teste faussement.
- [Phase 17]: [17-05] DEUX gardes de test MUETTES attrapees par mutation : (1) RefreshOrchestrator.TryTrigger ne renvoie PAS false quand le channel est plein (DropWrite renvoie true) ; (2) une fenetre WPF jamais affichee n'a pas de parent visuel pour son Content, donc le DataContext ne se propage pas et AUCUN binding ne s'evalue (Command null, Visibility=Visible par defaut). Parade : DataContext sur la grille racine + purge du Dispatcher.
- [Phase 18]: [18-01] Le plancher d'epoch 2020-01-01 vit DANS le point unique UsageNormalization, pas dans le provider fautif : il corrige le bug reel resets_at: 9 pour TOUTES les sources d'un coup, y compris celles qui n'existent pas encore (la sonde du plan 18-03 lit un epoch en TEXTE et en herite gratuitement).
- [Phase 18]: [18-01] InstantDepuisEpochMillisecondes n'est pas un confort mais une CONDITION de la garde : sans elle, les 3 horodatages en ms du rapport de diagnostic auraient force l'exemption de DiagnosticService.cs — et une garde qui exempte le plus gros fichier de la couche ne garde rien.
- [Phase 18]: [18-01] Aucun clamp dans la porte de validation, ni haut ni bas : Exhausted teste >= 1.0 donc un depassement reel doit rester visible, et une valeur negative est un symptome de source incoherente, pas un zero (null != 0).
- [Phase 18]: [18-01] Etape RED jouee contre un SQUELETTE compilable (NotImplementedException) et non contre une classe absente : tests/Chronos.Tests reference Chronos, donc un commit non compilable rendrait dotnet test non invocable. RED = 25 echecs / 1 succes, donc comportemental et non de compilation.
- [Phase 18]: [18-01] Garde de non-retour HDR-05 prouvee FALSIFIABLE par mutation reelle (p / 100.0 reintroduit -> echec nommant ChronosOAuthUsageProvider.cs:174) puis revoquee (git diff vide). Exemptions NOMINATIVES : point unique + ClaudeTokenReader (expiration de jeton) + SessionMonitor + TranscriptActivityProvider (dates qui ne sont pas des quotas).
- [Phase 18]: 18-02 : le statut serveur et le depassement vont sur WindowState (transmis PAR REFERENCE par Best()) et non sur UsageSnapshot, que le composite reconstruit -- contrainte mecanique, pas preference
- [Phase 18]: 18-02 : vocabulaire de statut OUVERT (NonReconnu) et null distinct de NonReconnu -- un statut inconnu n'est jamais range d'autorite dans « autorise »
- [Phase 18]: 18-02 : HDR-03/HDR-04 restent Pending -- ce plan livre leurs contrats, c'est au plan 18-06 de les cocher quand la remontee au cadran sera livree

### Contexte technique (déjà établi — ne pas re-rechercher)

- **Spike UIA PROUVÉ (2026-07-10)** sur la vraie machine : l'app Electron/Chromium expose son arbre
  d'accessibilité complet. Matcher par **ControlType + Name** (PAS par `AutomationId` volatils) ; Names
  **localisés fr**. Signaux vérifiés :

  - Text « Claude répond. » / Button « Arrêter » = **Working**.
  - Button « Ignorer les permissions » = **attend permission**.
  - Text « Mode chat » + placeholder « Tapez / pour les commandes » = **repos** (attend ton message).
  - Onglets Home/Code + panneaux Terminal/Diff/Aperçu/Actions de session/Contrôle à distance.
  - Sidebar : Button « En cours d'exécution <nom> » par session active.
  - Cowork-VM = état distant **non observable** → indéterminé.
- **MANQUE** : un snapshot en état **REPOS** (le spike a été pris pendant une génération). À capturer en
  tout début de Phase 13 pour figer la représentation exacte du « m'attend » premier plan.

- **Code existant** :
  - `src/Chronos/Services/SessionSnapshot.cs` — record neutre + enum `SessionActivity`
    (Working / WaitingAttention / WaitingTurn / Unknown).

  - `src/Chronos/Services/SessionMonitor.cs` — `Read(now)` fusionne transcripts + hooks, applique la
    staleness, filtre `archived` via `ArchiveStore`. **Le filtre « traitées » (Phase 14) se branche ici,
    au même endroit que le filtre archived.**

  - `src/Chronos/Services/TranscriptSessionSource.cs`, `ArchiveStore`.
  - `src/Chronos/ViewModels/SessionsViewModel.cs` — `Refresh`, timer 2 s.
  - `tests/Chronos.Tests/SessionsTests.cs`.
- **Nouveau (Phase 13)** : `DesktopUiaSessionSource` (nouvelle `ISessionSource`), lecture hors thread UI
  puis marshalling, élément racine mis en cache, cadence ~1-2 s.

- **Contraintes** : `System.Windows.Automation` (interop COM managé, aucune dépendance native de rendu,
  pas d'admin) ; `%USERPROFILE%`/`%APPDATA%` uniquement ; honnêteté (indéterminé, jamais une inférence
  présentée comme exacte) ; lecture tolérante (aucune source ≠ crash) ; UI/commentaires en français.

### Pending Todos

- Prochaine action v1.5 : `/gsd:plan-phase 18` (source exacte par en-têtes de rate-limit).
- **À faire par l'utilisateur, hors GSD** : les deux vérifications manuelles de la phase 17, listées sous
  « À VÉRIFIER PAR L'UTILISATEUR » dans `17-05-SUMMARY.md` — (1) lisibilité de la pastille de
  déconnexion dans les 3 thèmes × 5 styles × 2 modes, (2) parcours de reconnexion en un clic de bout en
  bout (login navigateur réel). Ne bloquent aucune phase suivante, **mais** sans reconnexion réelle, ni
  `/api/oauth/usage` ni la sonde d'en-têtes de la phase 18 ne répondront sur cette machine.

- ~~Prochaine action v1.5 : `/gsd:plan-phase 15`~~ (fait, phases 15/16/17 closes).
- ~~Phase 13 (tout début) : capturer le snapshot UIA en état **repos**~~ (fait, v1.4 clos).

### Blockers/Concerns

- Le mapping d'états UIA dépend de Names **localisés** ; prévoir la table fr/en (ROB-06) dès la Phase 13
  pour ne pas coder en dur des libellés qui changent à une MAJ de l'app.

- Débounce du focus (NET-02, Phase 14) : caler ~2-3 s pour distinguer un vrai acquittement d'un survol.

## Session Continuity

Last session: 2026-09-12T01:02:45.187Z
Stopped at: Completed 18-02-PLAN.md
Resume file: None
Next: /gsd:plan-phase 18
