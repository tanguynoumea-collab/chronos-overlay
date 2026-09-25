# Roadmap : Chronos

## Milestones

- ✅ **v1.0 — Overlay de quotas Claude complet** (7 phases, 18 plans, SHIPPED 2026-07-08) — [archive](.planning/milestones/v1.0-ROADMAP.md)
- ✅ **v1.1 — Estimation utile en mode app bureau** (2 phases, 5 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.1-ROADMAP.md)
- ✅ **v1.2 — Usage exact via l'endpoint OAuth** (2 phases, 4 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.2-ROADMAP.md)
- ✅ **v1.3 — Refonte du cadran (3 anneaux, remplissage, compacité)** (1 phase, phase 12, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.3-ROADMAP.md)
- ✅ **v1.4 — Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code)** (2 phases, phases 13-14, 5 plans, SHIPPED 2026-07-11) — [archive](.planning/milestones/v1.4-ROADMAP.md)
- ✅ **v1.5 — Exactitude permanente** (6 phases, phases 15-20, 28 plans, SHIPPED 2026-09-12) — [archive](.planning/milestones/v1.5-ROADMAP.md)
- ✅ **v1.6 — Observer au lieu de déduire (widget de sessions)** (6 phases, phases 21-26, 19 plans, SHIPPED 2026-09-13, exe 3.1.0) — [archive](.planning/milestones/v1.6-ROADMAP.md)
- 🚧 **v1.7 — Lue ou non lue (widget de sessions)** (5 phases, phases 27-31, en cours, exe cible 3.2.0) — [exigences](.planning/REQUIREMENTS.md)

## Milestone v1.7 : Lue ou non lue

### Overview

Le widget de sessions est **honnête** depuis v1.6 ; il n'est pas **utile**. L'utilisateur le dit — « les widgets
de sessions ne servent au final à rien » — et le relevé du 2026-09-25 à 16 h 08 le chiffre : quatre sessions
actives, deux « tour fini » en orange, toutes deux **déjà ouvertes et lues** à 16:00:39 et 16:00:59. Ce n'est pas
un défaut d'observation, c'est un **signal manquant** : rien ne sait si une session a été LUE. Une session
terminée ne quitte le widget que par un nouveau prompt, un clic droit ou l'expiration de huit heures.

**Le signal existe.** L'app bureau Claude écrit un fichier JSON par session
(`%APPDATA%\Claude\claude-code-sessions\<org>\<user>\local_<id>.json`, jonction vers le cache du paquet MSIX,
lisible par un processus non packagé — vérifié) qui porte `cliSessionId` (l'UUID des hooks et des transcripts),
`title`, **`lastFocusedAt`** et `postTurnSummary`, la classification de fin de tour faite par l'app elle-même. Le
relevé complet est dans `STATE.md`, section « Contexte technique v1.7 » ; il ne se ré-enquête pas. Deux choses y
restent **ouvertes**, et le relevé le dit en toutes lettres : « deux points à valider in vivo **AVANT de figer** ».

**Cible, en trois lignes :** une session qui réfléchit affiche **« Réflexion »** ; une session qui a fini, ou qui
pose une question, et que je n'ai pas lue affiche **« En attente »** ; une session finie que j'ai lue
**n'apparaît pas**.

**Doctrine inchangée (v1.5, v1.6) :** observé, jamais déduit ; une déduction porte son point d'interrogation ;
une source non documentée se lit avec tolérance et se dégrade vers « je ne sais pas » — ici, vers le
comportement v1.6 — jamais vers une invention. Lecture seule stricte de `%APPDATA%\Claude`. **Pas d'UI
Automation** : elle a quitté le dépôt en phase 21, et des gardes rendent son retour rouge.

Le milestone se lit en **cinq gestes**, ordonnés par dépendance réelle. La forme suggérée en comptait quatre
(source app-bureau → libellés → lecture → validation et publication) ; elle est **ajustée sur deux points**, et
chacun a son argument :

1. **Relever avant de régler** (Phase 27). *Premier ajustement : la validation in vivo des deux points ouverts
   (VAL-01) passe en TÊTE au lieu de fermer le milestone.*
   - Le relevé exige de valider **avant de figer**. Placé en fin, VAL-01 validerait une règle déjà écrite,
     testée et figée ; son « ajustement » serait une réécriture.
   - Ce qui est en jeu n'est pas un paramètre mais une **forme** : selon la réponse, LUE-02 (session
     sélectionnée + fenêtre Claude au premier plan) sert à l'alt-tab ET au tour qui finit sous les yeux, à l'un
     des deux seulement, ou à rien. Tout son code Win32 en dépend.
   - Le relevé ne demande **pas une ligne de Chronos** : lire en lecture seule `lastFocusedAt`,
     `lastActivityAt` et le fichier d'état des hooks 3.1.0 (actifs depuis le 2026-09-25) suffit. À l'inverse,
     lancer un overlay de développement pour s'en servir d'instrument repointerait les hooks de la
     configuration vivante hors supervision (précédent 19-05).
   - Il re-confirme le **format non documenté** sur la version du jour juste avant qu'on en code le lecteur —
     le risque n° 1 du milestone — et fournit des fixtures réelles à la Phase 29 (précédent DEL-06).
   - C'est la seule phase qui exige l'utilisateur au clavier : en tête, elle profite de sa présence au
     lancement du milestone, et les phases 28 à 30 s'enchaînent ensuite sans lui.
   - La leçon de la rétrospective v1.6 — *la validation humaine est une phase, pas un « à faire hors GSD »* —
     reste tenue : la validation de **ce qui est livré** est planifiée, en Phase 31.
2. **Parler en deux mots** (Phase 28). *Second ajustement : les libellés et les horizons passent AVANT la source
   app-bureau.*
   - **Le vocabulaire doit être définitif avant que `blocked` y entre.** Dans l'ordre inverse, APP-03
     afficherait d'abord la question de l'app sous « à toi », puis la phase suivante réécrirait ce libellé, ses
     tests, et repasserait sur les 8 styles pour le même état.
   - **LIB-04 est exactement le changement que l'audit v1.6 a signalé comme dangereux** (réserve R4) :
     remonter l'attente déduite au-dessus du travail modifie `AffichageSessions.Urgence`, que l'arbitrage
     FUS-01 consomme en rang 3. Ce couplage doit être rompu ou gardé **avant** qu'une nouvelle source entre
     dans l'arbitrage, pas après.
   - LIB-02 (`AskUserQuestion`) et SIL-01 (horizons) modifient la même source, `TranscriptSessionSource` :
     même phase.
   - Indépendance : si le format de l'app bureau se révèle cassé, la Phase 28 livre à elle seule deux mots, la
     question classée attente et le trou §9.1 refermé.
3. **Lire ce que l'app bureau sait** (Phase 29). Titre, dernier focus, classification de fin de tour, joints par
   `cliSessionId`, dégradés vers v1.6 s'ils manquent. Prérequis de la règle « lue ».
4. **La lecture fait disparaître** (Phase 30). Consomme `lastFocusedAt` (29), applique la forme de LUE-02 fixée
   par le relevé (27), et se vérifie contre la population élargie par SIL-01 (28) : une session connue par son
   seul transcript reste désormais lisible jusqu'à huit heures, la règle doit la connaître.
5. **Écrire, publier, constater** (Phase 31). Le document décrit le contrat FINAL de la source (même règle
   qu'EVT-05 en v1.6 : documenter un contrat en cours de refonte, c'est documenter un état intermédiaire),
   l'exe 3.2.0 est publié et réconcilié, et l'utilisateur vérifie le tableau en trois lignes sur sa machine.

**Contraintes portées :** C# / .NET 8 (`net8.0-windows`) / WPF / MVVM (CommunityToolkit.Mvvm) +
Microsoft.Extensions.DependencyInjection + Hosting ; aucune dépendance native ; chemins sous `%USERPROFILE%` /
`%APPDATA%` uniquement, aucun droit admin ; UI et commentaires en français. Les **889 tests xUnit** restent
verts (suite ~6 s) et chaque phase de code ajoute les siens — horloges injectées, aucun type WPF dans
`Services/` —, y compris `ServicesLayerPurityTests`, `CompositionRootTests`, `GardesPerimetreTests` (aucun type
dont le nom contient `Uia` ou se termine par `ForegroundWatch` : la détection de premier plan de LUE-02 doit
passer **sans** que cette liste soit assouplie), `ContratHooksDocumenteTests`, `NormalisationUniqueTests` et
`GardesDoctrineTests`. Toute modification visuelle reste cohérente sur les **8 styles de session** (galerie
`--sessions`) et les **9 thèmes**.

**Hors périmètre, rappelé :** réintroduire l'UI Automation ; lire le titre de fenêtre de l'app (il vaut
« Claude ») ; écrire dans `%APPDATA%\Claude` ; notifications Windows ; rouvrir R3. Différé au-delà de v1.7 :
sous-fenêtres opus/sonnet/cowork, survol/tooltip du cadran, tray, taille réglable, préavis avant saturation et
notification au reset, exploitation de `latestUserFrameAt` / `completedTurns`.

### Phases

**Numérotation des phases :**
- Phases entières (27→31) : travail de milestone planifié — continue après la Phase 26 (v1.6)
- Phases décimales (27.1, 27.2) : insertions urgentes (marquées INSERTED)

- [x] **Phase 27: Le relevé avant la règle** - Les deux inconnues de la règle « lue » sont tranchées sur la vraie machine, en lecture seule et avec l'utilisateur, avant qu'une ligne de la règle ne soit écrite
- [ ] **Phase 28: Deux mots, une question, les mêmes horizons** - « Réflexion » / « En attente » / « En attente ? », une question `AskUserQuestion` classée attente, et une session sans fichier de hook qui ne disparaît plus en silence à 15 min
- [ ] **Phase 29: Ce que l'app bureau sait de chaque session** - Titre, dernier focus et classification de fin de tour lus dans les fichiers de l'app, en lecture seule, avec dégradation vers le comportement v1.6
- [ ] **Phase 30: La lecture fait disparaître** - Une session lue quitte le widget sans clic, revient si elle redemande, et le diagnostic dit pourquoi elle est masquée
- [ ] **Phase 31: Écrit, publié, constaté** - `docs/desktop-app-sessions.md`, exe 3.2.0 publié et réconcilié, et le tableau en trois lignes vérifié sur la machine de l'utilisateur

### Phase Details

### Phase 27: Le relevé avant la règle
**Goal**: Les deux inconnues dont dépend la règle « lue » — le simple retour (alt-tab) sur la session déjà
sélectionnée met-il `lastFocusedAt` à jour ? que vaut-il quand un tour se termine pendant que l'utilisateur
regarde ? — sont tranchées par des **valeurs relevées sur la vraie machine**, avec l'utilisateur, **avant**
qu'une ligne de la règle ne soit écrite ; et LUE-02 est réécrite d'après ces faits, pas d'après une supposition.
**Depends on**: Rien — ni code, ni phase. C'est ce qui permet de la placer en tête (Overview, geste 1).
**Requirements**: VAL-01
**Success Criteria** (what must be TRUE):
  1. **Un protocole écrit, puis joué avec l'utilisateur** : les gestes, leur ordre et les valeurs à relever sont
     écrits AVANT d'être joués. Point (1) : `lastFocusedAt` de la session est relevé avant et après un
     aller-retour alt-tab sans changer de session. Point (2) : pour un tour terminé sous les yeux de
     l'utilisateur, `lastFocusedAt` et l'instant de fin de tour (état `Stop` du fichier d'état des hooks 3.1.0,
     `lastActivityAt` de l'app) sont relevés côte à côte (VAL-01).
  2. **La prémisse de LUE-02 est vérifiée, pas seulement ses paramètres** : la session au `lastFocusedAt` le
     plus récent est-elle bien celle que l'utilisateur voit — y compris quand il est sur l'accueil de l'app ou
     dans une conversation Chat ? Et le nom du processus de la fenêtre Claude au premier plan, celui que la
     détection Win32 devra reconnaître, est relevé.
  3. **LUE-02 est réécrite d'après le relevé** — conservée, modifiée ou déclarée superflue — et l'ajustement
     est écrit, daté, avec les valeurs qui le fondent, dans `REQUIREMENTS.md` et dans le relevé de phase, avant
     tout plan de la Phase 30 (VAL-01).
  4. **Rien n'a été écrit nulle part** : ni dans `%APPDATA%\Claude`, ni dans `%APPDATA%\Chronos`, ni dans
     `~/.claude/settings.json` ; aucun overlay de développement n'a été lancé. Au passage, le relevé confirme
     sur la version de l'app du jour que les champs que la Phase 29 lira (`cliSessionId`, `title`,
     `lastFocusedAt`, `postTurnSummary`) sont toujours là.
  5. **Des fixtures réelles pour la Phase 29** : quelques `local_*.json` réels, réduits aux champs que Chronos
     lit, couvrant les valeurs de `status_category` présentes sur disque et un fichier sans `cliSessionId`,
     sont versionnés comme fixtures de test — une fixture régénérée par sérialisation ne prouverait rien
     (précédent DEL-06).
**Plans**: 1 plan

Plans:
- [x] 27-01-PLAN.md — Protocole du relevé (Task 1 passif, gestes A et B avec l'utilisateur), conclusion, LUE-01/LUE-02 réécrites, six fixtures réelles. Baseline 896 verts.

### Phase 28: Deux mots, une question, les mêmes horizons
**Goal**: Le widget ne parle plus qu'en deux mots — **« Réflexion »** ou **« En attente »**, et
**« En attente ? »** quand l'attente est déduite du silence ; une question posée par Claude est une attente et
non un travail ; et une session connue par son seul transcript vit selon les mêmes horizons que les autres au
lieu de disparaître en silence au bout de quinze minutes (trou §9.1 de l'audit v1.6).
**Depends on**: Rien dans le code : indépendante de la source app-bureau et du relevé. Placée **avant** la
Phase 29 par choix (Overview, geste 2) — le vocabulaire et l'ordre doivent être définitifs avant que `blocked`
y entre, et le couplage R4 rompu avant qu'une nouvelle source rejoigne l'arbitrage.
**Requirements**: LIB-01, LIB-02, LIB-03, LIB-04, SIL-01
**Success Criteria** (what must be TRUE):
  1. **Deux mots et un point d'interrogation, partout** : sur les 8 styles de session et les 9 thèmes, une
     session qui travaille affiche « Réflexion », une session qui a fini son tour, demande une permission ou
     pose une question affiche « En attente », une attente déduite du silence affiche « En attente ? » — sans
     troncature, aucun libellé au-delà de seize caractères. « tour fini », « à toi », « en cours » et
     « inconnu » ne s'affichent plus ; une session d'état indéterminé n'a plus de ligne, et le diagnostic dit
     pourquoi (LIB-01, LIB-03).
  2. **Une question n'est pas une réflexion** : une session dont le transcript s'arrête sur un
     `AskUserQuestion` sans réponse (`tool_use` sans `tool_result`) s'affiche « En attente », au rang des
     questions ; elle redevient « Réflexion » dès que la réponse est écrite (LIB-02).
  3. **L'ordre dit l'urgence, et ne touche pas l'arbitrage** : permission ou question, puis tour fini, puis
     déduit, puis « Réflexion », puis la fraîcheur — identique dans le widget et dans le diagnostic, qui n'ont
     qu'un producteur (`AffichageSessions`). Remonter « En attente ? » au-dessus de « Réflexion » ne change
     **aucun** arbitrage entre sources : le double consommateur d'`Urgence` (réserve R4 de l'audit v1.6) est
     découplé, ou tenu par un test qui rougit si l'ordre d'écran entraîne l'arbitrage (LIB-03, LIB-04).
  4. **Le trou §9.1 est refermé** : une session connue par son seul transcript, qui travaillait et n'écrit plus
     depuis 25 min, affiche « En attente ? » au lieu de disparaître ; une telle session en tour fini depuis 3 h
     reste « En attente » ; au-delà de 8 h, elle n'est plus lue (SIL-01).
  5. **Les horizons ne peuvent plus diverger en silence** : transcript et hook obéissent aux mêmes seuils
     (20 min de silence, 8 h d'abandon), et une garde rougit si la chaîne 20 min < 8 h < 24 h < 72 h se défait
     (recommandation n° 2 de l'audit v1.6). Le §3 de `docs/hooks-contract.md` et le texte d'activation du
     widget (réserve R9) disent les mêmes mots que l'écran (SIL-01, LIB-01).
**Plans**: 4 plans en 3 vagues — point d'attention pour le plan : porter la fenêtre des transcripts de 15 min à 8 h
élargit la population lue (limite `MaxSessions` = 12, lecture des queues de 64 Ko à chaque cycle de 2 s) ; le coût et
l'effet sur la limite se MESURENT sur la vraie machine, ils ne se supposent pas (recherche : 19,6 ms médian à 8 h ;
remesuré avec le code livré par 28-04, tâche 3). Décision consignée au plan 28-01 (D-28-01) : un transcript est daté
par le `timestamp` de son dernier message, jamais par la date d'écriture de son fichier.

Plans:
- [ ] 28-01-PLAN.md — La question `AskUserQuestion` est une attente, et un transcript est daté par son dernier message (LIB-02, SIL-01 partiel) — vague 1
- [ ] 28-02-PLAN.md — L'ordre d'écran dit l'urgence sans toucher l'arbitrage (`RangArbitrage`, R4), l'indéterminé n'a plus de ligne, prédicat d'attente unique (LIB-04, LIB-01, LIB-03) — vague 1
- [ ] 28-03-PLAN.md — Trois mots partout, d'un seul producteur : contrat §1/§3, rôles des hooks, texte d'activation, galerie, 8 gabarits × 9 thèmes (LIB-01, LIB-03) — vague 2
- [ ] 28-04-PLAN.md — Les mêmes horizons : `HorizonsSessions`, règle de silence en un point, 8 h pour les transcripts, deux gardes, mesure sur la vraie machine (SIL-01) — vague 3
**UI hint**: yes

### Phase 29: Ce que l'app bureau sait de chaque session
**Goal**: Chronos lit, en **lecture seule**, ce que l'app bureau Claude écrit de chaque session — son titre, le
dernier instant où l'utilisateur l'a sélectionnée, la façon dont l'app a classé la fin du tour —, le joint aux
sessions du widget par `cliSessionId`, et **se tait proprement** quand ces fichiers manquent ou changent de
forme : le widget retombe alors exactement sur son comportement v1.6.
**Depends on**: Phase 27 (format re-confirmé sur la version du jour, fixtures réelles) et Phase 28 (vocabulaire
et ordre définitifs : `blocked` entre directement dans « En attente », au rang des questions, sans libellé
intermédiaire à réécrire).
**Requirements**: APP-01, APP-02, APP-03, APP-04, APP-05
**Success Criteria** (what must be TRUE):
  1. **Le titre remplace le dossier** : une session de l'app bureau s'affiche sous son titre, celui que
     l'utilisateur voit dans l'app ; une session sans titre connu garde son nom de dossier ; dans les deux cas
     le dossier reste lisible en info-bulle. Sur les 8 styles et les 9 thèmes, un titre long est coupé
     proprement sans casser la compacité du widget (APP-01, APP-02).
  2. **Une question de l'app est une attente observée** : une fin de tour classée `blocked` avec
     `needs_action` s'affiche « En attente », se range avec les questions, et son motif se lit en détail ou en
     info-bulle ; `completed` et `review_ready` seuls ne créent aucune ligne et ne changent aucun état (APP-03).
  3. **Dégradation vers v1.6, jamais de crash ni d'invention** : dossier absent, JSON invalide, fichier lu à
     moitié pendant sa réécriture intégrale, champ absent ou renommé, `cliSessionId` manquant — la session
     concernée se comporte exactement comme en v1.6, et aucune lecture ratée ne fait clignoter un titre ou un
     état. Seuls les fichiers modifiés depuis moins de 24 h sont ouverts, en `FileShare.ReadWrite`, par un
     chemin construit sans séparateur mixte (APP-01).
  4. **Le diagnostic dit ce que la source sait — et ce qu'elle ne sait pas** : dossier trouvé ou « absente »,
     nombre de fichiers lus, de jointures réussies, champs manquants ; et, pour chaque session affichée, le
     titre, le dernier focus et la classification lus. Une source absente est annoncée, jamais tue (APP-04).
  5. **Lecture seule, prouvée** : aucune écriture dans `%APPDATA%\Claude`, tenue par une garde de test
     falsifiée avant commit ; les fixtures réelles de la Phase 27 passent ; `GardesPerimetreTests`,
     `ServicesLayerPurityTests` et `CompositionRootTests` restent verts sans que leurs listes soient
     assouplies (APP-05).
**Plans**: TBD — deux points de conception à trancher au plan, sur mesure : (a) 142 fichiers d'environ 275 Ko
réécrits en entier ne se relisent pas tous les 2 s — la borne de 24 h et un cache par date de modification sont
à mesurer ; (b) la place de `blocked` dans l'arbitrage FUS-01 — troisième source datée, ou enrichissement d'un
signal existant — doit être une règle écrite et testée, pas un effet de bord.
**UI hint**: yes

### Phase 30: La lecture fait disparaître
**Goal**: Une session que l'utilisateur a **lue** quitte le widget d'elle-même, sans clic — et revient si elle
lui redemande quelque chose ; une session dont on ne sait rien de la lecture garde exactement le comportement
v1.6 ; et chaque disparition a une cause écrite dans le diagnostic.
**Depends on**: Phase 29 (`lastFocusedAt` et la jointure par session), Phase 27 (la forme de LUE-02 est celle
que le relevé a fixée) et Phase 28 (SIL-01 élargit la population à laquelle la règle s'applique : elle est
vérifiée contre elle).
**Requirements**: LUE-01, LUE-02, LUE-03, LUE-04
**Success Criteria** (what must be TRUE):
  1. **Le relevé du 2026-09-25 à 16 h 08 ne se reproduit plus** : les deux sessions finies à 15:59 et 15:56,
     puis ouvertes à 16:00:39 et 16:00:59, ne sont plus affichées à 16:08 — le scénario est rejoué tel quel en
     test, horloge injectée ; une session dont le tour finit APRÈS son dernier focus reste « En attente »
     (LUE-01).
  2. **Réversible, comme aujourd'hui** : une session lue qui finit un nouveau tour ou pose une nouvelle question
     réapparaît « En attente » (NET-03 inchangé) ; le masquage passe par le magasin réversible existant,
     épisode daté par l'instant du signal, sans réécrire `treated.json` à chaque rafraîchissement ;
     « Marquer traitée » et « Archiver » se comportent comme en v1.6 (LUE-01).
  3. **Le tour qui finit sous les yeux n'est pas annoncé « En attente »** : par la règle que la Phase 27 a
     fixée — par défaut, la session sélectionnée dans une fenêtre Claude au premier plan depuis au moins
     2,5 s —, détection Win32 sans UI Automation, horloge injectée ; les gardes anti-retour de la phase 21
     restent vertes sans que leur liste de noms interdits bouge (LUE-02).
  4. **Aucun masquage sans cause** : pour chaque session masquée, le diagnostic nomme le motif —
     « lue : focus à HH:MM > attente à HH:MM », « sélectionnée au premier plan » ou « répondue » — à côté des
     motifs v1.6 (archivée, marquée à la main) (LUE-03).
  5. **Pas de faux masquage** : une session sans métadonnées app-bureau (terminal pur, fichier illisible,
     format changé) garde exactement le comportement v1.6 — elle reste « En attente » jusqu'à la réponse, au
     geste ou à 8 h (LUE-04).
**Plans**: TBD — à trancher explicitement au plan, et à écrire : LUE-01 énumère « tour fini, à toi, question
posée » ; l'attente DÉDUITE (« En attente ? ») n'y figure pas. Une session muette depuis 25 min, ouverte
ensuite par l'utilisateur, est-elle « lue » ? La réponse doit être une décision écrite, pas un effet de bord
du prédicat d'attente du détecteur.

### Phase 31: Écrit, publié, constaté
**Goal**: La source app-bureau est documentée comme l'est le contrat des hooks, l'exe **3.2.0** est publié et
réconcilié, et l'utilisateur constate **sur sa machine** que le widget répond enfin à sa phrase : « Réflexion »,
« En attente », ou rien.
**Depends on**: Phases 27 à 30. `docs/desktop-app-sessions.md` décrit le contrat FINAL de la source — même règle
qu'EVT-05 en v1.6.
**Requirements**: VAL-02, VAL-03
**Success Criteria** (what must be TRUE):
  1. **La source est écrite comme le contrat des hooks** : `docs/desktop-app-sessions.md` donne le chemin, la
     jonction MSIX, les champs lus, la date du relevé et surtout ce qui n'est PAS garanti (format interne non
     documenté, réécriture intégrale des fichiers, sémantique de `lastFocusedAt` telle que relevée en
     Phase 27) ; une garde croisée rougit si les champs cités cessent d'être ceux que le code lit (VAL-02).
  2. **`Chronos-v3.2.0.exe` est publié** : la version 3.2.0 est embarquée (propriétés du fichier, diagnostic)
     et figure dans le nom du fichier publié (VAL-03).
  3. **Réconcilié au premier lancement** : après le premier démarrage de la 3.2.0, les hooks et la statusLine
     de `~/.claude/settings.json` pointent le nouvel exe, sans doublon et sans toucher aux groupes des autres
     outils — constaté dans le fichier, pas supposé (VAL-03).
  4. **Constaté en production, avec l'utilisateur** : sur sa machine, une session qu'il lit disparaît sans clic,
     une session qui travaille dit « Réflexion », une session qui l'attend dit « En attente » — les trois lignes
     du tableau de `REQUIREMENTS.md` sont vérifiées une à une et le résultat est consigné, écarts compris
     (leçon de la rétrospective v1.6 : la validation humaine est une phase, pas un « à faire hors GSD »).
**Plans**: TBD — dernière phase, à point de contrôle humain.

### Progress

**Execution Order:**
Phase 27 (relevé in vivo, en lecture seule, avec l'utilisateur) → Phase 28 (deux mots, la question, les
horizons) → Phase 29 (source app-bureau, exige 27 pour le format et les fixtures) → Phase 30 (la lecture fait
disparaître, exige 27, 28 et 29) → Phase 31 (document, release 3.2.0, constat en production).
Les phases 27 et 28 sont indépendantes l'une de l'autre : si l'utilisateur n'est pas disponible pour le relevé,
la 28 peut passer devant sans rien casser.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 27. Le relevé avant la règle | 1/1 | Complete   | 2026-09-25 |
| 28. Deux mots, une question, les mêmes horizons | 0/4 | Planned | - |
| 29. Ce que l'app bureau sait de chaque session | 0/? | Not started | - |
| 30. La lecture fait disparaître | 0/? | Not started | - |
| 31. Écrit, publié, constaté | 0/? | Not started | - |

### Couverture des exigences

17 requirements v1.7, chacun mappé à exactement une phase, aucun orphelin, aucun doublon.

| Phase | Requirements | Nombre |
|-------|--------------|--------|
| 27 | VAL-01 | 1 |
| 28 | LIB-01, LIB-02, LIB-03, LIB-04, SIL-01 | 5 |
| 29 | APP-01, APP-02, APP-03, APP-04, APP-05 | 5 |
| 30 | LUE-01, LUE-02, LUE-03, LUE-04 | 4 |
| 31 | VAL-02, VAL-03 | 2 |
| **Total** | | **17 / 17** |
