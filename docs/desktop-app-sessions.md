# Chronos — La source app-bureau (métadonnées de session de l'app bureau Claude)

> **Relevé le 2026-09-25** — app bureau Claude **2.9939.2.0** (paquet MSIX, famille `Claude_…`), processus `claude.exe`,
> sur la machine de l'utilisateur, en lecture seule : phase 27 (`27-RELEVE.md`), puis sonde lancée hors de l'arbre de
> l'app en phase 29 (`29-SONDE-APP06.txt`).
>
> **⚠ Limite de la source.** Ce format est **interne à l'app et non documenté** par son éditeur. Tout ce qui suit vient
> du disque (fichiers réels relevés) et, pour la façon dont l'app écrit, de son code minifié (confiance moyenne). Rien
> ici n'est une promesse de l'éditeur.

Ce document décrit ce que Chronos **lit** dans les fichiers que l'app bureau Claude écrit pour chaque session de code,
ce qu'il en **produit** — et, au moins autant, **ce qui n'est pas garanti**. Il est le pendant de `hooks-contract.md`
(les hooks, la source que Chronos câble lui-même) et de `data-sources.md` (les sources d'usage du cadran).

**La règle « lue » n'est pas écrite ici.** Elle consomme cette source, mais elle vit à un seul endroit : le §3 de
`docs/hooks-contract.md`, sous la garde de `ContratHooksDocumenteTests`. Deux copies d'une règle finissent par dériver.

**Pourquoi il existe.** Une source non documentée se lit avec tolérance et se dégrade vers « je ne sais pas » — ici,
vers le comportement v1.6 du widget. Encore faut-il savoir ce qu'on en attend : ce document dit ce qui deviendra faux
le jour où l'app changera, et comment le voir (§7).

**Deux vues d'AppData, valables pour tout ce qui suit.** Un processus lancé SOUS l'app bureau — une session Claude Code,
ses hooks, un terminal ouvert dans l'app — voit une vue **virtualisée** d'AppData. L'overlay, lancé par l'Explorateur ou
par le dossier Démarrage, voit la vue **réelle**. Un relevé fait depuis une session Claude Code ne vaut pas pour l'overlay.

---

## 1. Où Chronos lit

Les racines se **résolvent par candidats**, dans cet ordre (`RacinesEtat`, méthode `Candidats`) :

1. pour chaque dossier de paquet `%LOCALAPPDATA%\Packages\Claude_*`, dans l'ordre ordinal :
   `%LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude\claude-code-sessions` ;
2. puis `%APPDATA%\Claude\claude-code-sessions`.

Les candidats sont résolus une fois, au démarrage ; à chaque cycle, le lecteur teste leur existence dans l'ordre et lit
la **première racine qui existe**, et elle seule. `RacinesEtat` est le seul fichier de `src/` qui porte ce chemin, et il
n'écrit rien.

**`%APPDATA%\Claude` n'est pas une jonction** : c'est la virtualisation d'AppData du paquet MSIX, sans point d'analyse.
Vu de l'overlay, `%APPDATA%\Claude` n'existe pas ; vu d'une session Claude Code, il existe et montre les mêmes fichiers
que le cache du paquet. D'où l'ordre : le paquet d'abord. Le suffixe du nom de paquet (`pzs8sxrjxfjjc` le jour du
relevé) est un hachage de l'éditeur : il n'est pas codé, les dossiers `Claude_*` sont énumérés.

Sous la racine, un fichier par session : `<org>\<user>\local_<id>.json`, à cette profondeur exacte, jamais par un motif
récursif ; les temporaires de l'app (`.local_*`) sont exclus. 138 fichiers le jour du relevé, ~275 Ko chacun.

---

## 2. Les champs lus

Quatorze noms — ceux que `LecteurAppBureau` lit, et rien d'autre ; tout autre champ est ignoré. Les horodatages sont des
epochs en **millisecondes**, convertis en un seul point (`UsageNormalization`) ; une valeur absente, d'un autre type ou
sous le plancher de sanité vaut « absent ». La table est comparée au code du lecteur par `ContratAppBureauDocumenteTests` :
un champ lu sans être documenté, ou documenté sans être lu, la fait rougir.

<!-- CHAMPS-LUS:debut -->

| Champ | Type relevé | Ce que Chronos en fait |
|---|---|---|
| `lastFocusedAt` | epoch ms | le dernier focus de la session dans l'app (ce qui le met à jour : §5.5) : règle « lue » (`hooks-contract.md` §3), session sélectionnée (le plus récent, tous fichiers confondus), diagnostic |
| `cliSessionId` | texte | clé de jointure, sans casse, avec les hooks et les transcripts ; absent, le fichier est compté « sans cliSessionId » et n'est joint à rien |
| `title` | texte | le nom affiché à la place du dossier (APP-02) ; le dossier reste en info-bulle |
| `titleSource` | texte | lu, non exploité (valeur relevée : `auto`) |
| `cwd` | texte | lu, non exploité |
| `createdAt` | epoch ms | lu, non exploité |
| `lastActivityAt` | epoch ms | départage deux fichiers du même `cliSessionId` ; date une classification, figée à sa première apparition |
| `latestUserFrameAt` | epoch ms | lu, non exploité (absent sur 5 des 18 sessions récentes du relevé) |
| `completedTurns` | entier | lu, non exploité |
| `isArchived` | booléen | une session archivée dans l'app ne dépose pas de question ; diagnostic |
| `postTurnSummary` | objet | le conteneur de la classification de fin de tour ; absent le plus souvent (§5) |
| `postTurnSummary.status_category` | texte | la classification de fin de tour (table suivante) |
| `postTurnSummary.needs_action` | texte | le motif de la question : `blocked` avec un motif non vide est une attente observée (APP-03) |
| `postTurnSummaryFor` | texte | la clé d'épisode : l'instant d'une classification est figé à sa première apparition |

<!-- CHAMPS-LUS:fin -->

Un champ absent ou d'un autre type vaut nul (faux pour `isArchived`) et est compté, par nom, dans les « champs
absents » du diagnostic — sauf `cliSessionId` (le fichier est compté à part) et le résumé de fin de tour avec sa clé
`postTurnSummaryFor`, dont l'absence est normale.

### La classification de fin de tour

<!-- CATEGORIES-LUES:debut -->

| `status_category` | Ce que Chronos en fait |
|---|---|
| `completed` | `Terminee` : aucun état, aucune ligne |
| `blocked` | `Bloquee` : avec un `needs_action` non vide, une attente observée, « En attente », au rang des questions (APP-03) |
| `review_ready` | `PreteARevue` : aucun état, aucune ligne |

<!-- CATEGORIES-LUES:fin -->

Toute autre valeur est recopiée brute au diagnostic, dite « non interprétée », et ne dépose rien. Le code de l'app
connaît aussi `need_input` et `failed` : aucune des deux n'a été vue sur disque (0 fichier sur 138).

---

## 3. Ce que Chronos en produit

- **Le titre (APP-02).** Une session jointe s'affiche sous son `title` ; sans titre connu, sous son nom de dossier. Le
  dossier reste lisible en info-bulle ; un titre long est coupé par une ellipse, sans élargir le widget.
- **Le dernier focus et la session sélectionnée (LUE-01, LUE-02).** Le `lastFocusedAt` de chaque session et la session
  sélectionnée — le `lastFocusedAt` le plus récent parmi TOUS les fichiers servis au cycle, ceux sans `cliSessionId`
  compris — sont transmis au détecteur. Quand ce dernier focus appartient à un fichier sans `cliSessionId`, aucune
  session du widget n'est sélectionnée. La règle qui consomme ces deux faits est au §3 de `docs/hooks-contract.md`.
- **La question de l'app (APP-03).** `blocked` avec un `needs_action` non vide dépose une attente observée, source
  `AppBureau`, datée par l'instant d'activité lu à la première apparition de `postTurnSummaryFor`. L'app qualifie une
  ligne, elle n'en crée pas : le signal n'est déposé que pour une session déjà connue d'un hook ou d'un transcript, de
  moins de huit heures, non archivée dans l'app. Le motif se lit en info-bulle.
- **L'archivage de l'app.** Une session `isArchived` ne dépose pas de question.
- **Le diagnostic (APP-04, LUE-03, LUE-04).** La section « Source app-bureau » dit l'un de quatre états — NON BRANCHÉE,
  lecture impossible à ce cycle, absente (avec les racines cherchées), ou trouvée (racine, candidats, compteurs, champs
  absents, jointures) — puis, pour chaque ligne du widget, le titre joint, le dernier focus et la classification lus. La
  section « Règle « lue » » dit la session sélectionnée et le premier plan, ou pourquoi la règle ne voit rien. Le rapport
  lit la MÊME lecture que le widget.
- **La dégradation (APP-01, LUE-04).** Dossier absent, JSON invalide, fichier lu à moitié pendant sa réécriture, champ
  absent ou renommé, `cliSessionId` manquant : la session concernée se comporte exactement comme en v1.6. Une lecture
  ratée garde la dernière lecture valide du fichier : aucun titre ni état ne clignote.

---

## 4. Lire sans gêner l'écrivain

- **Fenêtre de lecture.** Seuls les fichiers écrits depuis moins de 24 h (`HorizonsSessions.LectureAppBureau`, sur la
  date d'écriture) sont ouverts ; un fichier n'est relu que si sa date d'écriture ou sa taille a changé ; au-delà de
  16 Mo, il n'est pas copié et compte comme illisible.
- **Une poignée courte.** Chaque fichier est ouvert une fois, en `FileShare.ReadWrite | FileShare.Delete`, le temps de
  COPIER ses octets (0,36 ms médian pour 289 Ko) ; l'analyse vient après la fermeture. L'app réécrit ses fichiers par un
  temporaire puis un renommage (trois essais), et se replie sur une écriture EN PLACE si le renommage échoue : une
  poignée tenue plus longtemps, ou ouverte sans partage d'écriture, ferait échouer ce renommage.
- **Lecture seule (APP-05).** Chronos n'écrit jamais dans ces dossiers. Trois gardes le tiennent : le texte du lecteur
  (aucune API d'écriture), son comportement sur une racine temporaire, et le chemin porté par le seul `RacinesEtat`, qui
  n'écrit rien (`LectureSeuleAppBureauTests`).
- **Coût**, mesuré hors de l'arbre de l'app le 2026-09-25 : première lecture 346 ms (18 fichiers relus), puis 0,50 ms
  médian avec le cache.

---

## 5. CE QUI N'EST PAS GARANTI

**La section la plus importante de ce document.** Tout ce qui suit a été relevé le **2026-09-25** sur la machine de
l'utilisateur, app bureau **2.9939.2.0** : des faits datés, observés sur disque, pas des propriétés promises.

### 5.1 — Le format est interne et non documenté

Aucun document de l'éditeur ne décrit ces fichiers. Les quatorze noms du §2 étaient présents sur les fichiers réels le
jour du relevé. Un champ absent, renommé ou d'un autre type vaut « absent » : la session garde le comportement v1.6
(LUE-04), et le diagnostic compte l'absence.

### 5.2 — Chaque fichier est réécrit en entier

~275 Ko réécrits EN ENTIER au changement de session et en fin de tour, avec un nouvel index NTFS à chaque réécriture ;
au redémarrage de l'app (16:58 le jour du relevé), TOUS les fichiers ont été réécrits. La date d'écriture ne dit donc
rien de l'âge d'une session : la fenêtre de 24 h n'est qu'une économie.

### 5.3 — La classification de fin de tour est transitoire

`blocked` et `review_ready` lus à 16:08 avaient disparu à 16:58 (redémarrage de l'app) ; la session du relevé n'en
portait plus après quatre tours ; l'app efface le résumé au début du tour suivant ; 127 fichiers sur 138 n'en portaient
pas. C'est un indice daté, jamais un état : seul `blocked` avec un motif dépose un signal.

### 5.4 — `lastActivityAt` avance après la fin du tour

+3 min 10 s mesurées après la fin d'un tour (activité d'agents en arrière-plan). L'instant d'une classification est
donc figé à la première apparition de `postTurnSummaryFor`, jamais relu sur `lastActivityAt`.

### 5.5 — Ce qui met `lastFocusedAt` à jour, et ce qui ne le met pas

- la re-sélection d'une session dans la barre latérale : oui (16:05:46 → 16:23:51) ;
- le simple retour alt-tab sur la session déjà sélectionnée : oui (16:23:51 → 20:30:44, geste A) ;
- la fin d'un tour que l'utilisateur regarde : **non** (resté à 20:30:44, fin du tour à 20:34:04, geste B).

La fin d'un tour regardé n'est donc pas vue par `lastFocusedAt` seul : c'est la session sélectionnée, fenêtre `claude`
au premier plan, qui la couvre (LUE-02, `hooks-contract.md` §3).

### 5.6 — La réserve de 16:23:51 à 19:51

`lastFocusedAt` est resté à 16:23:51 pendant 3 h 28 alors que l'utilisateur est revenu écrire dans la session à 19:51
(retour après une limite d'usage). Le geste A ne l'a pas reproduit. Observé une fois, jamais expliqué : à guetter, sans
en faire une règle.

### 5.7 — La latence d'écriture de `lastFocusedAt`

Un seul point relevé : focus à 20:30:44.976, fichier écrit à 20:31:09 (24 s, ambigu : l'écriture a pu être provoquée
par autre chose). Une lecture ne peut pas être vue avant que l'app ait écrit le fichier.

### 5.8 — Le premier plan ne dit pas la vue

La fenêtre au premier plan est lue par le nom de son processus (`claude`), jamais par UI Automation ni par son titre
(il vaut « Claude »). Rien ne distingue alors une session de code, l'accueil de l'app ou une conversation Chat :
pendant que l'utilisateur est sur l'accueil, la dernière session de code sélectionnée compte comme regardée. La CLI
Claude Code s'appelle elle aussi `claude` : une console où elle tourne, au premier plan, pourrait compter comme la
fenêtre de l'app. Limites écrites dès la phase 30, jamais « corrigées » sans décision ; le constat en production de la
phase 31 dira si elles se voient.

### 5.9 — Un fichier de plus de 24 h ne peut pas être sélectionné

Il n'est pas ouvert (§4) : une session restée sélectionnée 24 h sans réécriture, pendant qu'une autre travaille seule,
ne serait plus vue comme sélectionnée. Limite faible, écrite dans le lecteur.

---

## 6. Le relevé daté

| Grandeur (2026-09-25) | Valeur |
|---|---|
| Fichiers `local_*.json` | 138 ; 18 écrits depuis moins de 24 h (sonde de 21:57) |
| Sans `cliSessionId` | 23 sur 138 ; aucun parmi les 18 récents |
| Résumé de fin de tour | absent sur 127 fichiers sur 138 ; parmi les 18 récents : 6 terminées, 4 bloquées (motif présent), 2 prêtes à revue, 6 sans résumé |
| `latestUserFrameAt` absent | 5 sur 18 |
| Jointures | 4 sessions affichées sur 4 jointes à un titre (sonde hors de l'arbre de l'app, code de la phase 29) |
| Coût | lecture froide 346 ms ; chaude 0,50 ms médian ; inspection complète du moniteur 29,3 ms médian |

Six fichiers réels, réduits à seize champs au plus et anonymisés, sont versionnés comme fixtures
(`tests/Chronos.Tests/TestData/DesktopAppSessions`) : `completed`, `blocked`, `review_ready`, sans résumé, sans
`cliSessionId`, et la session du geste B.

---

## 7. Re-relever et détecter une dérive

1. **Relever hors de l'arbre de l'app** : Python lancé par son alias, ou un processus créé par WMI — jamais depuis une
   session Claude Code ni un terminal ouvert dans l'app. Vérifier d'abord que `%APPDATA%\Claude` n'existe pas pour ce
   processus : sinon, c'est la vue virtualisée.
2. **Lire brièvement** quelques `local_*.json` récents : ouvrir, copier, fermer. Jamais de guet en boucle — une poignée
   tenue fait échouer le renommage de l'app.
3. **Comparer** aux §2 et §5, et noter la version de l'app (`Get-AppxPackage Claude`).
4. **Si un écart apparaît, le DATER ici — avant de toucher au code.** L'écart est l'information ; le correctif n'en est
   que la conséquence.

**Ce qu'une machine garantit.** `ContratAppBureauDocumenteTests` rougit dès que la table du §2 cesse d'être la liste des
champs que le lecteur lit, dans un sens ou dans l'autre, ou que les catégories cessent d'être celles qu'il reconnaît ;
le chemin, la fenêtre de 24 h et le partage de lecture sont tenus contre le code.

**Ce qu'aucune machine ne peut garantir.** Si l'app renomme un champ, change le sens de `lastFocusedAt` ou cesse
d'écrire ces fichiers, tous nos tests resteront verts et ce document deviendra faux en silence. Seul un re-relevé le
verra : c'est pour cela que la date et la version de l'app figurent en tête, et dans le §5.
