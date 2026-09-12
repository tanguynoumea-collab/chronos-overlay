# Roadmap : Chronos

## Milestones

- ✅ **v1.0 — Overlay de quotas Claude complet** (7 phases, 18 plans, SHIPPED 2026-07-08) — [archive](.planning/milestones/v1.0-ROADMAP.md)
- ✅ **v1.1 — Estimation utile en mode app bureau** (2 phases, 5 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.1-ROADMAP.md)
- ✅ **v1.2 — Usage exact via l'endpoint OAuth** (2 phases, 4 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.2-ROADMAP.md)
- ✅ **v1.3 — Refonte du cadran (3 anneaux, remplissage, compacité)** (1 phase, phase 12, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.3-ROADMAP.md)
- ✅ **v1.4 — Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code)** (2 phases, phases 13-14, 5 plans, SHIPPED 2026-07-11) — [archive](.planning/milestones/v1.4-ROADMAP.md)
- 🚧 **v1.5 — Exactitude permanente** (6 phases, phases 15-20, en cours)

## Prochain milestone

Après v1.5 : sous-fenêtres opus/sonnet/cowork, survol/tooltip, tray, taille réglable, notification Windows
en bonus du signal UIA (`UserNotificationListener`), préavis avant saturation du quota et notification au reset.

---

## Milestone v1.5 : Exactitude permanente

### Overview

Chronos affiche depuis deux mois des pourcentages faux (~4×) sans jamais le dire. Le diagnostic du 2026-09-09
(voir `.planning/STATE.md`, section « Contexte technique v1.5 ») montre que ce n'est pas un bug isolé mais une
**panne silencieuse des trois sources exactes** doublée d'une **doctrine de repli défaillante** : jeton OAuth
expiré (401 muet), `usage.json` figé au 2026-07-10 mais toujours servi comme `Exact` faute de limite d'âge,
cache exact en RAM seulement (perdu à chaque démarrage), composite classant uniquement par fiabilité, et repli
divisant une somme de tokens par des plafonds calibrés sous l'ancien forfait.

**Décision de fond du milestone :** on ne cherche plus à rendre l'estimation absolue juste — les limites
Anthropic pondèrent par modèle, donc `tokens / plafond` restera faux même avec le bon plafond. L'estimation
absolue est **supprimée**. Doctrine cible : **exact frais → dernier exact persisté (encore rigoureusement exact
si rien ne s'est passé) → dernier exact + delta borné et marqué → indisponible.** Jamais de pourcentage inventé.

Le milestone se lit en **six gestes**, ordonnés par dépendance technique réelle :

1. **Nettoyer les intégrations** (Phase 15). Les installateurs de hooks et de statusLine cumulent au lieu de
   remplacer — 25 hooks Chronos au lieu de 5, pointant sur des exes de versions révolues. Indépendant du
   pipeline d'usage, court, réparable tout de suite, et il assainit le terrain de mesure.
2. **Poser les fondations de la correction par delta** (Phase 16). Deux gestes qui touchent le même code et
   cassent les mêmes tests, donc indissociables : persister sur disque le dernier relevé exact (l'instant T de
   référence, sans lequel aucun delta n'est calculable) et transformer `JsonlEstimationProvider` en source de
   delta, ce qui implique la démolition complète du sous-système de plafonds et la migration des réglages.
3. **Garder le jeton vivant et la panne visible** (Phase 17). Sans jeton valide, aucune source exacte ne
   répond — ni l'endpoint OAuth existant, ni la sonde d'en-têtes de la phase suivante.
4. **Ajouter la source par en-têtes de rate-limit** (Phase 18). Une source exacte de plus en tête de chaîne,
   qui répond **même sur un 429** et apporte statut serveur et dépassement. Elle arrive avant la refonte du
   composite pour que la nouvelle doctrine intègre sa place dès sa conception.
5. **Refondre la doctrine du composite** (Phase 19). Le cœur du milestone : limite d'âge sur toute source
   exacte, choix par fraîcheur et non par seule fiabilité, « encore exact » sans activité, « exact + delta
   borné » avec activité, « indisponible » sinon.
6. **Rendre l'état visible** (Phase 20). `IsStale` est calculé mais bindé nulle part : la distinction frais /
   daté / indisponible arrive enfin à l'écran, et le diagnostic dit quelle source alimente réellement
   l'affichage et depuis quand.

**Contraintes portées :** C# / .NET 8 (`net8.0-windows`) / WPF / MVVM (CommunityToolkit.Mvvm) +
Microsoft.Extensions.DependencyInjection + Hosting ; aucune dépendance native ; chemins sous `%USERPROFILE%` /
`%APPDATA%` uniquement, aucun droit admin ; UI et commentaires en français ; les **327 tests xUnit** restent
verts, y compris la garde de pureté `ServicesLayerPurityTests` (aucun type WPF dans `Services/` ni `Models/`)
et la garde de composition `CompositionRootTests`.

### Phases

**Numérotation des phases :**
- Phases entières (15→20) : travail de milestone planifié — continue après la Phase 14 (v1.4)
- Phases décimales (15.1, 15.2) : insertions urgentes (marquées INSERTED)

- [x] **Phase 15 : Idempotence des intégrations** - Les installateurs de hooks et de statusLine remplacent l'entrée Chronos existante au lieu de la cumuler, et purgent les entrées fantômes déjà présentes (completed 2026-09-09)
- [x] **Phase 16 : Fondations du delta — persistance & démolition des plafonds** - Le dernier relevé exact survit au redémarrage de l'exe, les transcripts JSONL ne produisent plus qu'« activité depuis T ? » et « tokens depuis T ? », le sous-système de plafonds disparaît et les réglages existants migrent sans casse (completed 2026-09-09)
- [x] **Phase 17 : Jeton toujours vivant, panne toujours visible** - Le jeton OAuth est rafraîchi préventivement et un échec d'authentification devient visible et réparable en un clic
 (completed 2026-09-11)
- [x] **Phase 18 : Source exacte par en-têtes de rate-limit** - Une requête jetable `max_tokens:1` livre l'usage exact via les en-têtes `anthropic-ratelimit-unified-*`, exploitables même sur un 429, avec statut serveur et dépassement (completed 2026-09-12)
- [x] **Phase 19 : Nouvelle doctrine du composite** - Exact frais → dernier exact encore valide → dernier exact + delta borné et marqué → indisponible, avec limite d'âge sur toute source exacte et plus jamais d'utilization dérivée d'un comptage de tokens
 (completed 2026-09-12)
- [ ] **Phase 20 : Honnêteté visible — cadran & diagnostic** - Le cadran distingue à l'œil chiffre frais / chiffre daté / indisponible, et le diagnostic nomme la source réellement affichée et son âge

### Phase Details

### Phase 15 : Idempotence des intégrations
**Goal**: L'utilisateur peut installer et réinstaller Chronos autant de fois qu'il veut sans que
`~/.claude/settings.json` n'accumule d'entrées : chaque installation **remplace** l'entrée Chronos précédente
et **purge** les entrées fantômes pointant sur des exes de versions révolues.
**Depends on**: Rien (première phase du milestone v1.5, totalement indépendante du pipeline d'usage).
**Requirements**: PUR-01, PUR-02, PUR-03
**Success Criteria** (what must be TRUE):
  1. **Hooks non cumulatifs** : après trois lancements successifs de Chronos depuis trois chemins d'exe
     différents, `~/.claude/settings.json` contient exactement 5 hooks Chronos — pas 15 — le repérage se
     faisant sur le marqueur `--hook` et non sur le chemin d'exe (PUR-01).
  2. **statusLine non cumulatif** : l'entrée `statusLine` de Chronos est remplacée et non dupliquée, repérée
     par le marqueur `--statusline`, quel que soit le chemin d'exe précédemment enregistré (PUR-02).
  3. **Fantômes purgés** : au premier lancement sur une machine déjà polluée (cas réel constaté : 25 hooks
     Chronos au lieu de 5, statusLine pointant un chemin `Downloads` en dur), les entrées obsolètes
     disparaissent automatiquement, sans intervention manuelle de l'utilisateur (PUR-03).
  4. **Rien d'autre n'est touché** : les hooks et réglages non-Chronos présents dans
     `~/.claude/settings.json` survivent intacts à l'opération, et un fichier illisible ou malformé ne
     provoque aucun crash au démarrage.
**Plans**: 3 plans (3 vagues)
- [x] 15-01-PLAN.md — Socle neutre : prédicat d'identité marqueur+nom d'exe, analyse tolérante « ne rien écrire », sérialisation fidèle, fixture de l'état réel pollué (vague 1)
- [x] 15-02-PLAN.md — Installateurs idempotents : hooks en retirer-puis-ajouter (PUR-01), statusLine repointée par mutation ciblée (PUR-02) (vague 2)
- [x] 15-03-PLAN.md — Réconciliateur au démarrage : purge des fantômes, sauvegarde horodatée conditionnelle, câblage overlay-only (PUR-03) (vague 3)

### Phase 16 : Fondations du delta — persistance & démolition des plafonds
**Goal**: Chronos dispose d'un **instant T de référence persistant** (le dernier relevé exact, sur disque avec
son horodatage, rechargé au démarrage) et d'une **source de delta bornée** (les transcripts JSONL répondent à
« activité depuis T ? » et « tokens depuis T ? »), le sous-système de plafonds ayant entièrement disparu du
code, des réglages et du menu.
**Depends on**: Phase 15 (aucune dépendance technique — simple ordre d'exécution). Constitue le **prérequis
technique de la Phase 19** : sans relevé exact persisté et sans source de delta, aucune correction par delta
n'est calculable.
**Requirements**: EXA-01, DEL-01, DEL-02, DEL-05, DEL-06
**Success Criteria** (what must be TRUE):
  1. **Le chiffre survit au redémarrage** : après fermeture puis relancement de l'exe hors connexion, Chronos
     réaffiche immédiatement le dernier chiffre exact connu avec son horodatage, au lieu de repartir sans
     chiffre — la bascule silencieuse au redémarrage, cas le plus fréquent, disparaît (EXA-01).
  2. **Les transcripts répondent à deux questions bornées** : pour un instant T donné, Chronos sait dire s'il
     y a eu une réponse assistant depuis T, et combien de tokens depuis T — et ne produit plus aucun
     pourcentage (DEL-01, DEL-02).
  3. **Plus une trace de plafond** : l'entrée de menu « Calibrer les plafonds… » a disparu, aucun dialogue de
     calibration n'est atteignable, et `BudgetCalibration` / `BudgetAutoCalibrator` / `BudgetSource` /
     `BudgetDialog` + VM / `IBudgetPrompt` / `BudgetPrompt` ne sont plus ni enregistrés en DI ni présents dans
     le code — le bug de forfait devient structurellement impossible (DEL-05).
  4. **Réglages migrés sans casse** : un `settings.json` existant contenant les six champs de plafonds
     s'ouvre sans erreur, les champs obsolètes sont ignorés, et coin, écran, thème, style de cadran et
     réglages du widget sessions sont conservés à l'identique (DEL-06).
  5. **Aucune régression de garde** : les tests xUnit restent verts, `ServicesLayerPurityTests` (aucun type
     WPF dans `Services/` ni `Models/`) et `CompositionRootTests` (composition DI complète) compris.
     Critère opérationnel : **0 échec + aucune perte de couverture nette** (bilan attendu −23 / +≈36,
     cible ~405 ± 10), et NON « ≥ 405 » — cette phase supprime du code et ses tests.
**Plans**: 4 plans (3 vagues)
- [x] 16-01-PLAN.md — Démolition du sous-système de plafonds : entrée « Plafonds… », dialogue, prompt, calibrateur auto et logique pure de déduction (DEL-05) (vague 1)
- [x] 16-02-PLAN.md — Magasin persistant du dernier relevé exact : `LastExactStore` + décorateur `LastExactUsageProvider`, sans câblage DI (EXA-01) (vague 1)
- [x] 16-03-PLAN.md — Transcripts en source de delta : `ITranscriptActivitySource` / `TranscriptActivityLog`, sortie de la chaîne composite, décorateur de persistance en tête (DEL-01, DEL-02, EXA-01) (vague 2)
- [x] 16-04-PLAN.md — Retrait des six champs de plafonds + `BudgetSource`, preuve de migration sur fixture réelle et garde de non-retour (DEL-05, DEL-06) (vague 3)

### Phase 17 : Jeton toujours vivant, panne toujours visible
**Goal**: L'utilisateur n'est plus jamais laissé deux mois avec un jeton mort sans le savoir : le jeton OAuth
est rafraîchi **avant** son expiration, et si l'authentification tombe malgré tout, l'overlay le dit et permet
de rouvrir le login en un clic.
**Depends on**: Phase 16 (ordre d'exécution). Conditionne l'utilité réelle de l'endpoint OAuth existant **et**
de la sonde d'en-têtes de la Phase 18 : sans jeton vivant, aucune source exacte ne répond.
**Requirements**: TOK-01, TOK-02, TOK-03
**Success Criteria** (what must be TRUE):
  1. **Rafraîchissement préventif** : le jeton est renouvelé avant sa date d'expiration, sans attendre qu'une
     requête échoue au moment où l'utilisateur regarde le cadran — un exe laissé tourner plusieurs jours
     continue de recevoir des chiffres exacts (TOK-01).
  2. **Panne d'authentification visible** : quand le renouvellement échoue ou qu'une requête revient en 401,
     une pastille de déconnexion apparaît dans l'overlay — plus jamais de 401 muet (TOK-02).
  3. **Réparation en un clic** : un clic sur la pastille de déconnexion relance le parcours de login, et la
     pastille disparaît dès qu'un chiffre exact est de nouveau obtenu (TOK-03).
  4. **Robustesse préservée** : une panne réseau, un refresh token invalide ou un serveur injoignable ne
     provoquent aucun crash ni gel de l'overlay — la dégradation reste silencieuse côté logs et explicite
     côté pastille.
**Plans**: 5 plans (5 vagues)
- [x] 17-01-PLAN.md — Wave 0 : couverture de `ChronosOAuthUsageProvider` (aucun test) et de `RefreshAsync` (non testé), avant toute modification (vague 1)
- [x] 17-02-PLAN.md — Contrats neutres `EtatAuthentification` / `IAuthStatus` + `RefreshAsync` qui rend sa CAUSE au lieu d'un null muet (vague 2)
- [x] 17-03-PLAN.md — `ChronosTokenAuthority` (autorité UNIQUE : sémaphore, rotation persistée, recul) + `TokenRefreshService` (tick 60 s, premier tick immédiat) — TOK-01 (vague 3)
- [x] 17-04-PLAN.md — Le provider devient consommateur (rejeu unique sur 401), câblage DI, diagnostic qui nomme l'état réel (vague 4)
- [x] 17-05-PLAN.md — Pastille ambre actionnable / grise informative + `ReconnecterCommand` dédiée — TOK-02, TOK-03 (vague 5, checkpoint humain)
**UI hint**: yes

### Phase 18 : Source exacte par en-têtes de rate-limit
**Goal**: Chronos dispose d'une source exacte supplémentaire, en tête de chaîne, qui répond **même quand
l'API est en 429** — précisément l'instant où l'overlay sert le plus — et qui apporte deux informations que
personne d'autre ne donne : le statut serveur et le dépassement.
**Depends on**: Phase 17 (la sonde exige un jeton valide ; sans rafraîchissement préventif ni signal de
déconnexion, elle retombe dans la panne silencieuse qu'on éradique).
**Requirements**: HDR-01, HDR-02, HDR-03, HDR-04, HDR-05, HDR-06
**Success Criteria** (what must be TRUE):
  1. **Chiffres exacts sans pont ni fichier** : sur une machine où `usage.json` est absent ou périmé,
     Chronos affiche quand même des chiffres exacts pour les deux fenêtres, obtenus par une requête jetable
     (`POST /v1/messages`, `max_tokens:1`, modèle le moins cher) et la lecture des en-têtes
     `anthropic-ratelimit-unified-*` (HDR-01).
  2. **Répond encore en saturation** : quand l'API renvoie un 429, le cadran continue d'afficher des chiffres
     exacts et à jour au lieu de basculer en « indisponible » (HDR-02).
  3. **Statut serveur et dépassement affichés** : l'utilisateur voit l'état déclaré par le serveur
     (`allowed` / `allowed_warning` / `rejected`) plutôt qu'un état déduit d'un pourcentage, et l'usage en
     dépassement (`overage`) quand il est présent (HDR-03, HDR-04).
  4. **Aucune divergence d'unité à l'écran** : quelle que soit la source qui alimente le cadran (en-têtes en
     0..1, `/api/oauth/usage` en 0..100, pont statusLine `used_percentage` en 0..100 ; `resets_at` en epoch
     secondes ou en ISO 8601), le pourcentage et le compte à rebours affichés sont cohérents entre eux —
     la normalisation se fait en un point unique (HDR-05).
  5. **Coût maîtrisé et annoncé** : la cadence d'interrogation est bornée (pas de sonde à chaque tick), et
     les réglages indiquent honnêtement que chaque appel consomme une micro-requête sur le compte (HDR-06).
**Plans**: 6 plans (5 vagues)
- [x] 18-01-PLAN.md — Point unique de normalisation des unités : 12 conversions rapatriées, piège de culture fr-FR gravé, garde de non-retour par balayage de source (HDR-05) (vague 1)
- [x] 18-02-PLAN.md — Vocabulaire ouvert du statut serveur (`NonReconnu`) + dépassement, 2 champs sur `WindowState`, canal latéral `IEtatServeur`, faux de transport porteur d'en-têtes sur 429 et 8 jeux de référence (HDR-03, HDR-04) (vague 1)
- [x] 18-03-PLAN.md — La sonde `RateLimitHeaderUsageProvider` : en-têtes lus AVANT l'aiguillage par code, frein 300 s, interrupteur `SondeEnTetesActivee` (HDR-01, HDR-02, HDR-06) (vague 2)
- [x] 18-04-PLAN.md — Statut serveur par fenêtre (3 noms candidats) et dépassement par les deux canaux, sans `elif` (HDR-03, HDR-04) (vague 3)
- [x] 18-05-PLAN.md — Câblage DI : la sonde en PRIMAIRE d'un nouveau composite externe, garde de position par le comportement, diagnostic qui nomme l'issue et les NOMS d'en-têtes (HDR-01, HDR-02, HDR-06) (vague 4)
- [x] 18-06-PLAN.md — Réglages : interrupteur + coût annoncé (≈ 288 micro-requêtes/jour), état serveur visible, et checkpoint humain du 429 RÉEL (HDR-03, HDR-04, HDR-06) (vague 5, checkpoint humain)

### Phase 19 : Nouvelle doctrine du composite
**Goal**: Le cœur du milestone — Chronos n'affiche plus jamais qu'un chiffre exact, éventuellement corrigé
d'un delta borné et marqué comme tel, ou rien du tout : **exact frais → dernier exact persisté encore
rigoureusement valide → dernier exact + delta borné avec sa marge → indisponible.**
**Depends on**: Phase 16 (relevé exact persisté = instant T de référence, et source de delta) et Phase 18
(la source en-têtes doit déjà exister pour que la doctrine intègre sa place plutôt que d'être rétrofittée).
**Requirements**: EXA-02, EXA-04, EXA-05, DEL-03, DEL-04
**Success Criteria** (what must be TRUE):
  1. **Fin du « exact » périmé** : un relevé exact au-delà de l'âge maximal cesse d'être présenté comme
     exact — un `usage.json` figé depuis deux mois ne peut plus battre une donnée fraîche, et le classement
     du composite ne se fait plus par seule fiabilité (EXA-02).
  2. **Encore exact quand rien n'a bougé** : si aucune réponse assistant n'est survenue depuis le dernier
     relevé exact, ce relevé est présenté comme **encore exact** — l'utilisation n'a effectivement pas
     changé — et non comme périmé (DEL-03).
  3. **Exact + delta marqué quand ça a bougé** : s'il y a eu de l'activité depuis, l'affichage devient
     « dernier exact + delta estimé » avec sa marge d'incertitude, visiblement distinct d'un relevé exact
     (DEL-04).
  4. **Plus aucun pourcentage inventé** : aucune utilization absolue dérivée d'un comptage de tokens n'est
     affichée dans quelque situation que ce soit ; et si aucun chiffre exact n'a jamais été obtenu, l'overlay
     affiche « indisponible » et invite à se connecter plutôt que d'afficher un pourcentage (EXA-04, EXA-05).
  5. **Doctrine reproductible sous test** : les quatre branches de la doctrine (frais / encore valide /
     delta / indisponible) sont vérifiables par des scénarios déterministes, et les gardes de pureté et de
     composition restent vertes.
**Plans**: 5 plans (5 vagues, séquentielles : chaque plan consomme ce que le précédent livre)

Plans:
- [x] 19-01-PLAN.md — CapturedAt par fenêtre dans les 3 providers exacts qui l'omettent (le piège : l'âge
      du FICHIER, jamais celui de la lecture), magasin qui refuse l'incertifiable et sait dire « jamais
      rien vu », mémoïseur d'activité, faux manquant, inventaire réel des impacts
- [x] 19-02-PLAN.md — Le modèle à quatre états sans casser un seul site de construction, la doctrine PURE
      (4 branches falsifiables une par une), et deux gardes de non-retour (recomposition du snapshot,
      interdiction EXA-04)
- [x] 19-03-PLAN.md — La doctrine branchée en tête de chaîne : lecture paresseuse et mémoïsée des
      transcripts, 4 sites de construction adaptés dans la même tâche, correctif d'une ligne de
      WeeklyRecalibration
- [x] 19-04-PLAN.md — L'honnêteté visible : « ≥ N % » plutôt que « ~N % », matière brute rattachée au bon
      champ, et l'invitation à se connecter d'EXA-05 (moitié visible incluse)
- [x] 19-05-PLAN.md — Porte de phase : preuves automatisées rassemblées, carte de validation remplie, et
      vérification humaine de la bascule « 10 % » vers « indisponible + invitation » sur la machine réelle

### Phase 20 : Honnêteté visible — cadran & diagnostic
**Goal**: Ce que la doctrine sait, l'utilisateur le voit : le cadran distingue à l'œil un chiffre exact frais,
un chiffre exact daté et un état indisponible, et le diagnostic nomme la source qui alimente réellement
l'affichage ainsi que son ancienneté.
**Depends on**: Phase 19 (les trois états à distinguer n'existent qu'une fois la nouvelle doctrine en place).
**Requirements**: EXA-03, EXA-06
**Success Criteria** (what must be TRUE):
  1. **Trois états lisibles d'un coup d'œil** : sans lire de texte, l'utilisateur distingue un chiffre exact
     frais, un chiffre exact daté (ou corrigé d'un delta) et un état indisponible — `IsStale`, jusqu'ici
     calculé mais bindé nulle part, devient un signal réel à l'écran (EXA-03).
  2. **Cohérent dans les deux modes et les trois thèmes** : la distinction reste lisible en mode Normal comme
     en mode Étendu, et dans les trois thèmes de couleur, sans casser la compacité du cadran ni les tokens de
     design validés.
  3. **Diagnostic sans ambiguïté** : le diagnostic indique quelle source alimente l'affichage à cet instant
     (en-têtes / endpoint OAuth / pont statusLine / dernier exact persisté / dernier exact + delta) et depuis
     quand — l'utilisateur peut constater seul une panne de source sans instrumenter le code (EXA-06).
  4. **Aucune fuite de WPF dans les services** : le nouveau signal visuel passe par les ViewModels ;
     `ServicesLayerPurityTests` reste vert.
**Plans**: 5 plans (5 vagues, séquentielles : l'enchevêtrement des fichiers de test — `CadranBindingTests`
et `GardesDoctrineTests` sont touchés par presque tous les plans — et la contrainte de compilabilité à
chaque commit rendent le parallélisme illusoire ici)

Plans:
- [x] 20-01-PLAN.md — Vague 0 : les trois dettes sans dépendance — couture `IInventaireMachine`
      (la suite passe de 2 min 10 à quelques secondes), `CadranBindingTests` isolé du vrai
      `settings.json`, trou de la `UniformGrid` des réglages (vague 1)
- [x] 20-02-PLAN.md — Le modèle de la source : `SourceUsage` posée sur `WindowState` (0 site cassé sur
      48), les 5 producteurs, vocabulaire FR unique dans `Chronos.Text`, mort d'`EstimatedTokens`
      remplacée par une garde structurelle — EXA-06 (vague 2)
- [x] 20-03-PLAN.md — Le ViewModel : `IsEstimated` → `EstPlancher`, mort d'`IsStale` (une seule notion
      de « périmé », gardée), `EstDate` rapporté et non calculé, infobulle qui binde enfin
      `TokensText` — EXA-03 (vague 3)
- [ ] 20-04-PLAN.md — Le dessin : pointillé de plancher aux Anneaux (le 5ᵉ style enfin marqué), rangée
      de pastilles qui rend la superposition structurellement impossible + marque d'âge, mot
      « indisponible » — EXA-03 (vague 4)
- [ ] 20-05-PLAN.md — Le diagnostic nomme la source et l'ancienneté (« ≥ » et non « ~ »), porte de
      phase, et protocole de vérification humaine consolidant les constats hérités des phases 17, 18
      et 19 — EXA-03, EXA-06 (vague 5, checkpoint humain)
**UI hint**: yes

### Progress

**Execution Order:**
Phase 15 (indépendante) → Phase 16 (fondations : persistance + delta + démolition des plafonds) →
Phase 17 (jeton vivant) → Phase 18 (source en-têtes) → Phase 19 (doctrine du composite, exige 16 et 18) →
Phase 20 (rendu visible de la doctrine, exige 19).

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 15. Idempotence des intégrations | 3/3 | Complete   | 2026-09-09 |
| 16. Fondations du delta — persistance & démolition des plafonds | 4/4 | Complete   | 2026-09-09 |
| 17. Jeton toujours vivant, panne toujours visible | 5/5 | Complete   | 2026-09-11 |
| 18. Source exacte par en-têtes de rate-limit | 6/6 | Complete   | 2026-09-12 |
| 19. Nouvelle doctrine du composite | 5/5 | Complete   | 2026-09-12 |
| 20. Honnêteté visible — cadran & diagnostic | 3/5 | In Progress|  |

### Couverture des exigences

24 requirements v1.5, chacun mappé à exactement une phase, aucun orphelin, aucun doublon.

| Phase | Requirements | Nombre |
|-------|--------------|--------|
| 15 | PUR-01, PUR-02, PUR-03 | 3 |
| 16 | EXA-01, DEL-01, DEL-02, DEL-05, DEL-06 | 5 |
| 17 | TOK-01, TOK-02, TOK-03 | 3 |
| 18 | HDR-01, HDR-02, HDR-03, HDR-04, HDR-05, HDR-06 | 6 |
| 19 | EXA-02, EXA-04, EXA-05, DEL-03, DEL-04 | 5 |
| 20 | EXA-03, EXA-06 | 2 |
| **Total** | | **24 / 24** |
