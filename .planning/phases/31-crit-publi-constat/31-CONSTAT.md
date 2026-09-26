# Phase 31 — Constat en production (critère 4, VAL-03)

**Date :** 2026-09-26, temps 0 relevé à 13:03 (heure locale).
**App bureau Claude :** paquet MSIX `Claude`, version **2.9939.2.0** (celle du relevé de la phase 27 : les 14 champs
sont ceux du document `docs/desktop-app-sessions.md`).
**Exe publié :** `Chronos-v3.2.0.exe` à la racine du dépôt — 77 377 466 o, md5 `2cffcec5a50a7b296b869f4fb5732170`,
FileVersion 3.2.0.0 / ProductVersion 3.2.0 (identique à 31-02-SUMMARY). `Chronos-v3.1.0.exe` présent, intact
(77 218 013 o, md5 `ab93b3270abdec55cec42ac710552661`).
**Overlay en marche au temps 0 :** `Chronos-v3.1.0.exe`, PID 40772, parent 27872 (explorer), démarré le
2026-09-23 à 11:09:31.
**Preuve de vue réelle :** les relevés de fichiers sont faits par un processus hors de l'arbre de l'app
(`'Claude' in os.listdir(APPDATA)` = False) — ce que l'overlay voit, pas la vue virtualisée des sessions.
**Fin de phase côté code :** `dotnet test Chronos.sln -c Debug --nologo -v q` deux fois ⇒ **1152 / 0**, 8 s et 8 s.

## Règles du constat

- Le protocole est écrit et commité AVANT d'être joué ; les gestes sont ceux de l'utilisateur ; l'agent relève
  en lecture seule, hors de l'arbre de l'app, et ne lance, n'arrête ni ne clique jamais l'overlay.
- Aucune écriture de l'agent dans `%APPDATA%\Claude`, `%APPDATA%\Chronos`, le cache du paquet ni
  `~/.claude/settings.json`. Seule exception : le dossier vide `%USERPROFILE%\Documents\chronos-constat`, créé au
  temps 0 pour les sessions de test.
- La grâce (2,5 s après la fin du tour, jusqu'à ~5,5 s selon le cycle) est MESURÉE et jugée par l'utilisateur,
  jamais consignée en échec.
- « Non observé » et « non applicable » sont des réponses valables ; tout écart est écrit au §3, jamais tu.
- Le milestone ne se clôt pas sans ce fichier rempli (§4 « Verdict »).

## Protocole (écrit le 2026-09-26 à 13:05, avant d'être joué)

### Point de contrôle (a) — quitter la 3.1.0, lancer la 3.2.0

1. Quitter la 3.1.0 : clic droit sur le cadran → fenêtre de réglages → « Quitter Chronos » (aucun verrou
   mono-instance : sans ce geste, deux overlays écriraient le même `treated.json`).
2. Dans l'Explorateur de fichiers, ouvrir le dossier du dépôt et double-cliquer `Chronos-v3.2.0.exe` — jamais
   depuis un terminal ouvert dans l'app Claude (vue virtualisée d'AppData). SmartScreen éventuel : « Informations
   complémentaires » → « Exécuter quand même ».
3. Attendre le cadran et le widget (quelques secondes au premier lancement : extraction des DLL natives).
4. Clic droit sur le cadran : l'en-tête des réglages doit afficher « v3.2 ». Refermer.
5. Facultatif : « Lancer au démarrage » depuis CETTE fenêtre (le raccourci visera la 3.2.0). Rien ne l'exige.
6. Ne pas supprimer `Chronos-v3.1.0.exe` : les sessions ouvertes avant ce lancement l'appellent encore.
   Attendu : un seul cadran, un seul widget ; l'agent constate ensuite dans le fichier que les 8 hooks et la barre
   de statut pointent la 3.2.0, et que le rapport dit « Version : 3.2.0 ».

### Point de contrôle (b) — les trois lignes du tableau, une par une (≈ 15 min)

Consignes : ne jamais cliquer sur le widget pendant une mesure ; « invite longue » = « Sans utiliser aucun outil,
écris un texte d'environ 1200 mots sur <sujet>. » ; ne pas se servir de la session GSD pour les mesures.

1. Préparation : ouvrir trois NOUVELLES sessions de code dans `chronos-constat`, S1, S2, S3 ; dans chacune,
   envoyer « Constat Chronos S1 : réponds seulement prêt. » (puis S2, S3) et attendre la réponse.
2. L1 + L3 — S1 regardée du début à la fin : invite longue (les horloges), rester sur S1, fenêtre Claude au
   premier plan. Attendu L1 : « Réflexion » sous le TITRE. Attendu L3 : à la fin du tour, « En attente » au plus
   ~5 s, puis disparition sans clic ; compter les secondes, dire si cela gêne.
3. L2 — S2 finie sans être regardée, puis ouverte : invite longue (les phares), puis cliquer aussitôt S1 et y
   rester. Attendu : S2 « Réflexion » puis « En attente », et y reste (15 s). Cliquer S2 dans la barre latérale :
   S2 disparaît sans clic sur le widget ; compter les secondes.
4. L2b — S3, fenêtre Claude au second plan : invite longue (les cartes marines), puis passer sur l'Explorateur
   avant la fin du tour. Attendu : S3 « En attente » tant que l'Explorateur est devant (15 s) ; revenir par
   alt-tab sans cliquer une autre session : S3 disparaît en quelques secondes ; compter.
5. Diagnostic — MAINTENANT, avant L4 : clic droit → « Diagnostic… » → Ctrl+C → coller dans la réponse. Attendu :
   S1, S2, S3 parmi les « Sessions MASQUÉES », chacune avec une cause ; aucune ligne « un filtre non nommé ».
6. L4 — S2, déjà lue, qui se remet à travailler : invite longue (les sabliers), cliquer aussitôt S1. Attendu : S2
   réapparaît « Réflexion », puis « En attente » à la fin, et reste.
7. Q — une question posée, non regardée : dans S1, « Réfléchis quelques secondes sans outil, puis pose-moi une
   question à choix multiple (A ou B) avec l'outil AskUserQuestion. » ; cliquer aussitôt S3. Attendu : S1
   « En attente » AU-DESSUS de S2. Si S1 affiche « Réflexion », le noter (limite connue). NE PAS répondre à la
   question avant le point (c).

### Point de contrôle (c) — les douze vérifications renvoyées par les phases 28 à 30 (≈ 15 min)

1. V07 (suite) : cliquer S1 (la question est à l'écran). Attendu : S1 quitte le widget avant la réponse ; dire si
   cela gêne. Répondre à la question : S1 « Réflexion » pendant le travail.
2. V08 + V06 : dans S2, « Écris trois phrases, puis termine en me demandant de choisir entre deux options, sans
   utiliser l'outil AskUserQuestion. » ; cliquer aussitôt S3. Attendu : S2 « En attente » ; survoler sa ligne : si
   l'app a classé `blocked`, l'info-bulle porte le motif, lisible.
3. V09 : dans S3, invite longue (les boussoles), puis AVANT la fin aller sur l'accueil ou une conversation Chat
   (fenêtre Claude au premier plan). Attendu (limite écrite) : S3 disparaît probablement sans être vue.
4. V12 (facultatif, seulement si Claude Code tourne aussi en console) : refaire 3 avec la console devant ;
   attendu : S3 reste « En attente ». Sinon « non applicable ».
5. V01 : Win+R, `"<dépôt>\Chronos-v3.2.0.exe" --sessions` (n'écrit rien, ne réconcilie rien). Attendu : 8 tuiles,
   les trois mots (« En attente ? » atténué mais lisible), le titre long `api-migration` coupé par une ellipse
   sans élargir la tuile, info-bulle « titre — dossier — mot », celle d'`overlay` sur deux lignes avec
   « permission demandée ». Fermer la galerie.
6. V02 (facultatif) : clic droit → THÈME, passer les 9 thèmes, les mots restent lisibles.
7. V05 : un titre long est coupé proprement par une ellipse sans élargir le widget (« vu » / « non observé »).
8. V03, V04 : clic droit → « Diagnostic… » → Ctrl+C → coller (rapport final).
9. V11 : pendant tout le constat, une session est-elle restée « En attente » après avoir été ouverte et lue ?

## 0. Temps 0 et réconciliation (VAL-03)

| contrôle | attendu | constaté |
|---|---|---|
| Vue réelle du relevé | `Claude` absent de `%APPDATA%` | True |
| `~/.claude/settings.json` au temps 0 | md5, taille, date | md5 `9eab8a8ecdb7f1d541841dd1c6ea418b`, 3 384 o, 2026-09-25 11:07:56 |
| Occurrences `Chronos-v3.1.0.exe` | 9 | 9 |
| Groupes `--hook` Chronos | 8 | 8 |
| Groupes tiers `gsd-` | 3 | 3 |
| Fin de `statusLine.command` | `…/Chronos-v3.1.0.exe" --statusline` | `OVERLAY/Chronos-v3.1.0.exe" --statusline` |
| `%APPDATA%\Chronos\backups\` | liste | `claude-settings-20260923-110933.json` |
| `%APPDATA%\Chronos\settings.json` (réel) | `SessionsWidgetEnabled = True` (sinon STOP : purge) | True (écrit le 2026-09-26 11:24:59) |
| `%APPDATA%\Chronos\treated.json` (réel) | nombre d'entrées héritées | 2 |
| `%APPDATA%\Chronos\sessions` (réel) | vide ou absent (APP-06) | absent |
| Processus Chronos | un seul : la 3.1.0 | PID 40772, parent 27872, 2026-09-23 11:09:31 |
| Dossier Démarrage (réel) | aucun `Chronos.lnk` | aucun |
| `Chronos-v3.2.0.exe` | 77 377 466 o, md5 `2cffcec5…`, 3.2.0.0 / 3.2.0 | conforme |
| App Claude | 2.9939.2.0 | 2.9939.2.0 |
| Dossier de test `Documents\chronos-constat` | créé, vide | créé |
| **Réconciliation** : sauvegarde horodatée | md5 = `9eab8a8ecdb7f1d541841dd1c6ea418b` | `claude-settings-20260926-141838.json`, md5 `9eab8a8ecdb7f1d541841dd1c6ea418b` ✅ |
| **Réconciliation** : fichier courant | = sauvegarde + 9 remplacements `Chronos-v3.1.0.exe` → `Chronos-v3.2.0.exe` | égalité structurelle True ; 9 occurrences 3.2.0 / 0 de 3.1.0 ; écrit le 2026-09-26 14:18:38 (md5 `3c68422466b8d62d15acf0dfcaf7e2c3`) ✅ |
| **Réconciliation** : groupes | 8 `--hook` + statusLine sur la 3.2.0, 3 `gsd-` intacts, aucun doublon | 8 hooks 3.2.0, statusLine 3.2.0, 3 `gsd-`, un seul groupe Chronos par clé (8 clés) ✅ |
| Processus après lancement | un seul : la 3.2.0, parent explorer | ⚠ DEUX overlays à 14:32 : `Chronos-v3.2.0.exe` PID 126160 (parent explorer, 14:18:37) ET `Chronos-v3.1.0.exe` PID 40772 toujours en marche (la 3.1.0 n'a pas été quittée) — écart §3, à corriger avant le point (b) |
| Rapport « Diagnostic… » | ligne 3 « Version : 3.2.0 » | `chronos.log` (vue réelle) : « Version : 3.2.0 », « Exe courant : …\Chronos-v3.2.0.exe », « Source app-bureau : trouvée — …Packages\Claude_…\claude-code-sessions », fichiers d'état : 5 (paquet) / absent (vue réelle), « Règle « lue » » présente ; « v3.2 » vu par l'utilisateur ✅ |

## 1. Les trois lignes

| id | situation (tableau de l'utilisateur) | geste | attendu | vu (utilisateur) | relevé (agent) | verdict |
|---|---|---|---|---|---|---|
| L1 | session qui réfléchit | S1, invite longue, regardée | « Réflexion » sous le titre | à relever | à relever | à relever |
| L2 | finie, non lue, puis ouverte | S2 finie pendant que S1 est ouverte, puis clic S2 | « En attente » qui reste ; disparaît sans clic après le clic S2 | à relever | à relever | à relever |
| L2b | finie, fenêtre Claude au second plan, puis alt-tab | S3, Explorateur devant | « En attente » qui reste ; disparaît après l'alt-tab | à relever | à relever | à relever |
| L3 | finie et lue en direct | S1 regardée jusqu'à la fin | « En attente » ≤ ~5 s puis disparition sans clic | à relever | à relever | à relever |
| L4 | lue, qui se remet à travailler | S2, invite longue | réapparaît « Réflexion », puis « En attente » | à relever | à relever | à relever |
| Q | question posée, non regardée | S1 AskUserQuestion, S3 devant | S1 « En attente » au-dessus de S2 | à relever | à relever | à relever |

## 2. Vérifications déférées

| id | vérification (phase d'origine) | geste | attendu | vu (utilisateur) | relevé (agent) | verdict |
|---|---|---|---|---|---|---|
| V01 | galerie des 8 styles (28, 29) | `--sessions` | trois mots, titre long en ellipse, info-bulles | à relever | à relever | à relever |
| V02 | 9 thèmes (28) | menu THÈME | mots lisibles | à relever | à relever | à relever |
| V03 | section « Source app-bureau » (29) | rapport final | trouvée, racine du paquet, fichiers d'état | à relever | à relever | à relever |
| V04 | causes de masquage et « Règle « lue » » (30) | rapport final | chaque masquée a une cause ; section présente | à relever | à relever | à relever |
| V05 | borne de 160 DIP (29) | widget | titre coupé sans élargir | à relever | à relever | à relever |
| V06 | info-bulle longue (29) | survol de S2 | lisible | à relever | à relever | à relever |
| V07 | PermissionRequest pour AskUserQuestion ; question vue puis répondue (28, 30) | S1 | fichier d'état pendant la question ; disparition avant la réponse ; « Réflexion » après | à relever | à relever | à relever |
| V08 | question classée `blocked` par l'app (29) | S2 | « En attente » + motif si classée | à relever | à relever | à relever |
| V09 | accueil ou Chat au premier plan (30) | S3 | limite écrite : disparition probable | à relever | à relever | à relever |
| V10 | latence d'écriture de `lastFocusedAt` (27, 30) | relevé agent | écart date d'écriture − `lastFocusedAt` | — | à relever | à relever |
| V11 | réserve de la phase 27 (16:23 → 19:51) | tout le constat | aucune session lue restée « En attente » | à relever | à relever | à relever |
| V12 | CLI homonyme `claude` (30) | console devant | reste « En attente » | à relever | à relever | à relever |

## 3. Écarts

- **E1 (point a, 14:32) — deux overlays.** L'utilisateur a lancé la 3.2.0 (PID 126160) sans que la 3.1.0 (PID 40772,
  du 23.09) soit quittée ; aucun verrou mono-instance n'existe. Les deux écrivent `treated.json` (mtime 14:32:41).
  Correction demandée avant le point (b) : quitter la 3.1.0 (réglages « v3.1 » → « Quitter Chronos », ou fin de tâche
  sur `Chronos-v3.1.0.exe`). Ne change rien à la réconciliation (faite par la 3.2.0, constatée dans le fichier).
- **Note (non écart)** : le rapport n'énumère que 5 des 8 événements câblés (« Hooks --hook installés : Notification,
  Stop, UserPromptSubmit, SessionStart, SessionEnd ») — limite du rapport connue de la recherche de phase 31 ; la preuve
  des 8 groupes est le fichier.
- **E2 (signalé par l'utilisateur à 14:44) — une session qui travaille par un SOUS-AGENT EN ARRIÈRE-PLAN est
  affichée « En attente ».** Session « ADVANCED SHEET passation checkpoint 26.2-21 » (88677186) : le tour parent
  s'est terminé à 14:33:21 (`end_turn`, `Stop` à 14:33:23) juste après avoir lancé un agent en arrière-plan ; le
  transcript `subagents/agent-a7df3776…jsonl` est encore écrit à 14:45:09 (310 lignes, dernier message un
  `tool_result`). Pour l'utilisateur, la session « réfléchit et tourne » (l'app la marque en exécution) ; pour
  Chronos, hooks et transcript disent « tour fini » : les événements des sous-agents sont VETOÉS (phase 25-02) et
  les transcripts `subagents/` ignorés (phase 21). **Ligne 1 du tableau non tenue dans ce cas** — cause de
  conception, pas de mesure. Correction : phase insérée 30.1 « un sous-agent qui écrit est un travail de sa
  session » (battements de sous-agent → Réflexion sans jamais effacer une attente d'intervention ; dernier
  message des `subagents/*.jsonl` comme signal de travail côté transcript), puis republication 3.2.1 et reprise
  du point (a).
- **E1, suite (14:45)** : les deux overlays tournent toujours (40772 et 126160) ; la 3.1.0 reste à quitter.
- **E1, suite (16:38) — TROIS overlays.** La 3.2.1 (PID 101708, parent explorer, 16:37:45) a été lancée sans que
  la 3.1.0 (40772) ni la 3.2.0 (126160) soient quittées. Les trois réécrivent `chronos.log` et `treated.json` ;
  le rapport lu à 16:38 est celui de la 3.2.0 (« Version : 3.2.0 »). Les mesures du point (b) sont impossibles
  tant que la 3.2.1 n'est pas seule. À vérifier avec l'utilisateur : « Quitter Chronos » a-t-il été tenté et
  a-t-il échoué (ce serait un écart de plus), ou le geste a-t-il été sauté ?
- **Réconciliation vers la 3.2.1 (16:37:46) — CONFORME** : sauvegarde `claude-settings-20260926-163746.json`
  dont le md5 (`3c68422466b8d62d15acf0dfcaf7e2c3`) est celui du fichier laissé par la 3.2.0 ; fichier courant
  (md5 `de9e31438c90a067c59fc31c73d82919`) = sauvegarde + 9 remplacements `Chronos-v3.2.0.exe` →
  `Chronos-v3.2.1.exe` (égalité structurelle True) ; 8 hooks 3.2.1, statusLine 3.2.1, 3 `gsd-` intacts, 0
  occurrence de 3.2.0 ou 3.1.0. La ligne « Version : 3.2.1 » du rapport sera relue quand la 3.2.1 sera seule.
- **E2, suite (2026-09-26 16:03)** — corrigé par la phase 30.1 (SUB-01, SUB-02) : `Chronos-v3.2.1.exe` publié
  (release `b981e41`, 77 385 116 o, md5 `3a1dc26f92f7547cb244538f0447779b`), jamais lancé par l'agent. Reprise du
  point (a) avec la 3.2.1 : quitter la 3.1.0 ET la 3.2.0 (réglages → « Quitter Chronos », sur chacun des deux
  cadrans), puis lancer `Chronos-v3.2.1.exe` par l'Explorateur ; attendus : dans `~/.claude/settings.json`,
  9 remplacements `Chronos-v3.2.0.exe` → `Chronos-v3.2.1.exe` (sauvegarde horodatée d'abord, de md5
  `3c68422466b8d62d15acf0dfcaf7e2c3` s'il n'a pas bougé d'ici là — relevé à 16:02), « Version : 3.2.1 » au
  diagnostic ; au point (b), une session dont un agent tourne en arrière-plan dit « Réflexion ».
- **Autostart** : non activé (aucun `Chronos.lnk` dans le dossier Démarrage réel) — facultatif, pas un écart.

## 4. Verdict

à rendre
