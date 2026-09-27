# Phase 32 — Constat en production sur la 3.2.2 (critère 6, VAL-04)

**Date :** 2026-09-27, temps 0 relevé à 04:27 (heure locale, UTC+02:00).
**App bureau Claude :** paquet MSIX `Claude`, version **2.9939.2.0** (celle des relevés des phases 27 et 31), famille `Claude_pzs8sxrjxfjjc`.
**Exe publié :** `Chronos-v3.2.2.exe` à la racine du dépôt — **77 557 997 o**, md5 `51f4d95bb346b9a4ce33f3f63f3d434c`,
FileVersion 3.2.2.0 / ProductVersion 3.2.2 / ProductName Chronos (identique à 32-07-SUMMARY, release `769c9a0`). Jamais lancé en
overlay par l'agent. Anciens exe présents et intacts : `Chronos-v3.1.0.exe` (77 218 013 o), `Chronos-v3.2.0.exe` (77 377 466 o),
`Chronos-v3.2.1.exe` (77 385 116 o).
**Overlays en marche au temps 0 (sonde t0, 04:27:35) :** TROIS, tous sans argument, tous parent 27872 (`explorer.exe`) :
- `Chronos-v3.1.0.exe` PID **40772**, démarré le 2026-09-23 à 11:09:31 ;
- `Chronos-v3.2.0.exe` PID **126160**, démarré le 2026-09-26 à 14:18:37 ;
- `Chronos-v3.2.1.exe` PID **121900**, démarré le 2026-09-26 à 22:34:35 ;
- aucun `Chronos-v3.2.2.exe`.
Aucun des trois ne connaît le verrou `Local\Chronos-overlay` : le point (a) commence par les quitter, à la main.
**Preuve de vue réelle :** tout relevé de fichier Chronos est fait par un processus créé par WMI (`Win32_Process.Create`,
`ReturnValue = 0`), hors de l'arbre de l'app bureau. Sonde t0 : `parent: WmiPrvSE.exe` ; `APPDATA\Claude existe : False`.
Ce que l'overlay lancé par l'Explorateur voit, pas la vue virtualisée (copie COW du paquet) que lit toute session.
**Fin de phase côté code :** `dotnet test Chronos.sln -c Debug --nologo -v q` deux fois ⇒ **1313 / 0** et **1313 / 0**
(durée de la suite 12 s et 12 s ; 20 s et 15 s mur, compilation comprise) — total identique à 32-07.

## Règles du constat

- Le protocole est écrit et commité AVANT d'être joué ; les gestes sont ceux de l'utilisateur ; l'agent relève en lecture
  seule, hors de l'arbre de l'app, et **ne lance, n'arrête ni ne clique jamais un overlay** ; il ne tue aucun processus.
- L'agent n'écrit QUE dans `.planning/phases/32-compter-juste-puis-journaliser/` et dans `%USERPROFILE%\Documents\chronos-constat`
  (scripts de sonde `.ps1` et sorties `.txt`, supprimés après copie des valeurs ici ; le dossier reste, vide). Jamais d'écriture dans
  `%APPDATA%\Claude`, `%APPDATA%\Chronos`, le cache du paquet, `~/.claude/settings.json`.
- **Sonde hors de l'arbre de l'app, obligatoire** : chaque relevé de fichier Chronos passe par un processus créé par WMI
  (`Invoke-CimMethod -ClassName Win32_Process -MethodName Create`), dont les DEUX premières lignes de sortie prouvent la vue
  réelle : parent `WmiPrvSE.exe` (≠ `claude.exe`) et `Test-Path $env:APPDATA\Claude` = **False**. Une sonde dont la preuve échoue
  est jetée et relancée. Jamais de lecture de `%APPDATA%\Chronos` depuis une session (vue virtualisée : le malentendu de CPT-02).
- Lectures brèves des fichiers de l'app ; jamais de guet en boucle sur un fichier (les sondes différées attendent par
  `Start-Sleep` en tête de script, hors de l'arbre de l'app).
- La grâce de LUE-02 (2,5 s après la fin du tour, jusqu'à ~5,5 s selon le cycle) est MESURÉE et jugée par l'utilisateur, jamais
  consignée en échec.
- « Non observé » et « non applicable » sont des réponses valables ; **tout écart est un fait consigné au §3, jamais corrigé
  ici** ; il ouvre une phase 32.x ou une release 3.2.3 sur décision de l'utilisateur.
- Données personnelles : jamais de titre ni de motif de session ; identifiants tronqués à 8 caractères ; profil écrit
  `%USERPROFILE%`.
- `Chronos-v3.1.0.exe`, `Chronos-v3.2.0.exe`, `Chronos-v3.2.1.exe` restent sur le disque.
- La phase ne se clôt pas sans ce fichier rempli (§4 « Verdict »).

## Protocole (écrit le 2026-09-27 à 04:30, avant d'être joué)

### Point de contrôle (a) — quitter les trois anciennes, lancer la 3.2.2, tenter une seconde (CPT-03 en vrai)

1. Quitter les TROIS anciens Chronos, l'un après l'autre : clic droit sur chaque cadran → fenêtre de réglages →
   **« Quitter Chronos »** (les en-têtes disent « v3.1 », « v3.2 », « v3.2 » ; les anciennes versions ne connaissent pas le
   verrou : sans ce geste elles resteraient, et trois processus écriraient `treated.json` et `chronos.log`).
   Quand il ne reste AUCUN cadran, passer à l'étape 2.
2. Dans l'**Explorateur de fichiers**, ouvrir le dossier du dépôt et **double-cliquer `Chronos-v3.2.2.exe`**. Jamais depuis un
   terminal ouvert dans l'app Claude ni depuis une session (vue virtualisée d'AppData : le journal irait dans le paquet).
   SmartScreen éventuel : « Informations complémentaires » → « Exécuter quand même ».
3. Attendre le cadran et le widget de sessions (quelques secondes : extraction des DLL natives). **Noter l'heure** du lancement
   (T0 du journal).
4. **Double-cliquer `Chronos-v3.2.2.exe` une SECONDE fois.** Attendu : une boîte « Chronos tourne déjà (une seule instance à la
   fois). Cette copie se retire ; l'autre continue. » — cliquer OK ; **un seul cadran** reste, inchangé.
5. Clic droit sur le cadran : l'en-tête des réglages doit afficher **« v3.2 »** ; dans DONNÉES, sous « Sonde d'en-têtes », une
   carte **« Journal des relevés »** avec une ligne d'état (« aucune écriture depuis le démarrage » ou « dernière écriture à
   l'instant · 1 relevé depuis le démarrage ») et **pas** de pastille orange. Refermer.
6. Facultatif : « Lancer au démarrage » depuis CETTE fenêtre (le raccourci visera la 3.2.2). Rien ne l'exige.
7. **Laisser l'overlay tourner au moins une heure** (le constat des lignes prend ~30 min ; le journal sera relevé à T+10 min
   et à T+60 min par une sonde hors de l'arbre de l'app). Ne supprimer aucun ancien exe.
   Attendus (relevés par l'agent après le geste) : un seul processus `Chronos-v3.2.2.exe` sans argument, parent `explorer.exe` ;
   aucun `Chronos-v3.1.0/3.2.0/3.2.1.exe` ; dans `~/.claude/settings.json`, une sauvegarde neuve
   `%APPDATA%\Chronos\backups\claude-settings-<date du lancement>.json` de md5 `52827d6a52cb148b08165f36f46f2401` (l'état du
   temps 0), puis 9 remplacements `Chronos-v3.2.1.exe` → `Chronos-v3.2.2.exe` (8 `--hook` + statusLine), 3 `gsd-*` intacts, un
   seul groupe Chronos par clé ; `chronos.log` « Version : 3.2.2 », « Vue AppData : réelle », « Processus Chronos : 1 (dont ce
   processus) — 0 autre(s) », « Verrou mono-instance (Local\Chronos-overlay) : tenu par ce processus » ;
   `%APPDATA%\Chronos\historique\releves-2026-09.jsonl` dont la première ligne porte `"ev":"demarrage"` et `"version":"3.2.2"`.

### Point de contrôle (b) — les trois lignes du tableau, une par une (≈ 15 min ; protocole 31-CONSTAT, inchangé)

Consignes : ne jamais cliquer sur le widget pendant une mesure ; « invite longue » = « Sans utiliser aucun outil, écris un texte
d'environ 1200 mots sur <sujet>. » ; ne pas se servir de la session GSD pour les mesures. Le journal est relevé pendant ce
temps par une sonde hors de l'arbre de l'app : ne pas fermer l'overlay.

1. Préparation (≈ 3 min) : ouvrir trois NOUVELLES sessions de code dans `chronos-constat`, S1, S2, S3 ; dans chacune, envoyer
   « Constat Chronos S1 : réponds seulement prêt. » (puis S2, S3) et attendre la réponse.
2. L1 + L3 — S1 regardée du début à la fin : invite longue (les horloges), rester sur S1, fenêtre Claude au premier plan.
   Attendu L1 : « Réflexion » sous le TITRE. Attendu L3 : à la fin du tour, « En attente » au plus ~5 s, puis disparition sans
   clic ; compter les secondes, dire si cela gêne.
3. L2 — S2 finie sans être regardée, puis ouverte : invite longue (les phares), puis cliquer aussitôt S1 et y rester. Attendu :
   S2 « Réflexion » puis « En attente », et y reste (15 s). Cliquer S2 dans la barre latérale : S2 disparaît sans clic sur le
   widget ; compter les secondes.
4. L2b — S3, fenêtre Claude au second plan : invite longue (les cartes marines), puis passer sur l'Explorateur avant la fin du
   tour. Attendu : S3 « En attente » tant que l'Explorateur est devant (15 s) ; revenir par alt-tab sans cliquer une autre
   session : S3 disparaît en quelques secondes ; compter.
5. Diagnostic — MAINTENANT, avant L4 : clic droit → « Diagnostic… » → cliquer dans la boîte → Ctrl+C → coller dans la réponse.
   Attendu : S1, S2, S3 parmi les « Sessions MASQUÉES », chacune avec une cause ; ET la section `[Magasins persistants]` :
   « Vue AppData : réelle », « dernier exact : … dernière écriture il y a N min » (N ≤ 2), « journal des relevés : … dernière
   écriture il y a N min » (N ≤ 5), aucune ligne « ALERTE — journal muet », « Processus Chronos : 1 (dont ce processus) — 0
   autre(s) » (des hooks de moins de 10 s peuvent s'y ajouter), « Verrou mono-instance (Local\Chronos-overlay) : tenu par ce
   processus ».
6. L4 — S2, déjà lue, qui se remet à travailler : invite longue (les sabliers), cliquer aussitôt S1. Attendu : S2 réapparaît
   « Réflexion », puis « En attente » à la fin, et reste.
7. Q — une question posée, non regardée : dans S1, « Réfléchis quelques secondes sans outil, puis pose-moi une question à choix
   multiple (A ou B) avec l'outil AskUserQuestion. » ; cliquer aussitôt S3. Attendu : S1 « En attente » AU-DESSUS de S2. Si S1
   affiche « Réflexion », le noter (limite connue). NE PAS répondre à la question avant le point (c).

### Point de contrôle (c) — les douze vérifications renvoyées par les phases 28 à 30 (≈ 15 min ; protocole 31-CONSTAT, inchangé)

L'overlay reste ouvert (la sonde T+60 attend).

1. V07 (suite) : cliquer S1 (la question est à l'écran). Attendu : S1 quitte le widget avant la réponse ; dire si cela gêne.
   Répondre à la question : S1 « Réflexion » pendant le travail.
2. V08 + V06 : dans S2, « Écris trois phrases, puis termine en me demandant de choisir entre deux options, sans utiliser l'outil
   AskUserQuestion. » ; cliquer aussitôt S3. Attendu : S2 « En attente » ; survoler sa ligne : si l'app a classé `blocked`,
   l'info-bulle porte le motif, lisible.
3. V09 : dans S3, invite longue (les boussoles), puis AVANT la fin aller sur l'accueil ou une conversation Chat (fenêtre Claude
   au premier plan). Attendu (limite écrite) : S3 disparaît probablement sans être vue.
4. V12 (seulement si Claude Code tourne aussi en console) : refaire 3 avec la console devant ; attendu : S3 reste « En attente ».
   Sinon « non applicable ».
5. V01 : Win+R, `"<dépôt>\Chronos-v3.2.2.exe" --sessions` (n'écrit rien, ne réconcilie rien, ne prend pas le verrou). Attendu :
   8 tuiles, les trois mots (« En attente ? » atténué mais lisible), le titre long `api-migration` coupé par une ellipse sans
   élargir la tuile, info-bulle « titre — dossier — mot », celle d'`overlay` sur deux lignes avec « permission demandée ».
   Fermer la galerie.
6. V02 (facultatif) : clic droit → THÈME, passer les 9 thèmes, les mots restent lisibles.
7. V05 : un titre long est coupé proprement par une ellipse sans élargir le widget (« vu » / « non observé »).
8. V03, V04 : clic droit → « Diagnostic… » → Ctrl+C → coller (rapport final : il porte aussi `[Magasins persistants]` — le journal
   doit dire « dernière écriture il y a N min » avec N ≤ 5 et toujours pas d'ALERTE).
9. V11 : pendant tout le constat, une session est-elle restée « En attente » après avoir été ouverte et lue ?

### Le journal s'écrit (critère 6) — relevé par l'agent, sondes différées lancées au point (a)

1. **T+10 min** (sonde `sonde-t10.ps1`, `Start-Sleep` jusqu'à T0 + 10 min 30 s) : `releves-2026-09.jsonl` existe dans la vue
   réelle ; (heure de la sonde − mtime) **< 6 min** ; ≥ 3 lignes (1 `demarrage` + ≥ 2 relevés) ; première ligne `demarrage`
   `version 3.2.2` ; dernières lignes = relevés `"source":"SondeEnTetes"` avec `u5`, `u7`, `r5`, `r7` et deux `t` distincts.
2. **T+60 min** (sonde `sonde-t60.ps1`, jusqu'à T0 + 60 min 30 s) : **≈ 12 ± 2** relevés depuis T0 (sonde 300 s) ; aucun doublon
   de `t` ; mtime < 6 min ; `demarrage` présent ; pas d'`arret` (l'overlay tourne) ; `sonde_refusee` / `jeton_invalide` seulement si
   l'utilisateur a subi un 429 ou une déconnexion pendant l'heure (à noter).
3. Si le fichier n'existe pas ou n'a pas bougé : ÉCART (le journal ne s'écrit pas en production) — consigné, jamais corrigé ici.

## 0. Temps 0, une seule instance, réconciliation, journal (VAL-04)

| contrôle | attendu | constaté |
|---|---|---|
| Vue réelle de la sonde t0 (04:27:35) | parent `WmiPrvSE.exe` ; `Claude` absent de `%APPDATA%` | `parent: WmiPrvSE.exe` ; `APPDATA\Claude existe : False` ✅ |
| Processus Chronos au temps 0 | 3.1.0, 3.2.0, 3.2.1 sans argument, parent explorer ; aucune 3.2.2 | PID 40772 (3.1.0, 23/09 11:09:31), 126160 (3.2.0, 26/09 14:18:37), 121900 (3.2.1, 26/09 22:34:35), tous parent 27872 `explorer.exe`, sans argument ; 0 × 3.2.2 ✅ |
| `%APPDATA%\Chronos\historique` (réel) | absent avant la 3.2.2 | `historique existe : False` ✅ |
| `~/.claude/settings.json` au temps 0 | md5, taille, date | md5 **`52827d6a52cb148b08165f36f46f2401`**, 3 384 o, 2026-09-26 18:53:11 (même valeur lue depuis la session et par la sonde) |
| Occurrences `Chronos-v3.2.1.exe` | 9 | **9** (3.2.2 : 0 ; 3.2.0 : 0 ; 3.1.0 : 0) |
| Groupes `--hook` Chronos 3.2.1 | 8 | **8** (un groupe par clé : Notification, PermissionRequest, PostToolUse, PreToolUse, SessionEnd, SessionStart, Stop, UserPromptSubmit) |
| Groupes tiers `gsd-` | 3 | **3** |
| Fin de `statusLine.command` | `…/Chronos-v3.2.1.exe" --statusline` | `OVERLAY/Chronos-v3.2.1.exe" --statusline` ✅ |
| `%APPDATA%\Chronos\backups\` (réel) | liste | `claude-settings-20260923-110933.json` (962 o, md5 `ffe9c80c…`), `claude-settings-20260926-141838.json` (3 384 o, md5 `9eab8a8e…`), `claude-settings-20260926-163746.json` (3 384 o, md5 `3c684224…`) — les trois de la phase 31 |
| `%APPDATA%\Chronos\settings.json` (réel) | `SessionsWidgetEnabled = True` (sinon STOP : purge) | **True** (536 o, écrit le 2026-09-27 01:33:39 ; `SondeEnTetesActivee` True, `RefreshIntervalSeconds` 60, thème `ardoise`) ✅ pas de STOP |
| `%APPDATA%\Chronos\last-exact.json` (réel) | réécrit chaque minute | 324 o, mtime **04:26:37** (58 s avant la sonde) ; `captured_at 2026-09-27T02:24:37Z`, u5 0,57 / r5 `04:50Z`, u7 0,53 / r7 `2026-10-02T22:00Z` — vivant ✅ |
| `%APPDATA%\Chronos\chronos.log` (réel) | rapport d'un ancien overlay | 6 599 o, mtime 2026-09-26 22:38:48, « Date : 2026-09-26 22:34:38 », « **Version : 3.2.1** » (rapport du dernier lancement, la 3.2.1) |
| `%APPDATA%\Chronos\treated.json` (réel) | présent (trois écrivains) | 266 o, mtime 2026-09-27 03:18:09 |
| `%APPDATA%\Chronos\sessions` (réel) | absent (APP-06) | absent ✅ |
| Dossier Démarrage (réel) | aucun `Chronos.lnk` | aucun (autostart non activé, facultatif) |
| `Chronos-v3.2.2.exe` | 77 557 997 o, md5 `51f4d95b…`, 3.2.2.0 / 3.2.2 | **77 557 997 o, md5 `51f4d95bb346b9a4ce33f3f63f3d434c`, FileVersion 3.2.2.0, ProductVersion 3.2.2** ✅ |
| App Claude | 2.9939.2.0 | 2.9939.2.0 ✅ |
| Dossier de test `Documents\chronos-constat` | existe, vide | existe (créé en phase 31), vide après suppression de `sonde-t0.*` |
| Processus après lancement (sonde a, 11:20:30 ; preuve : parent `WmiPrvSE.exe`, `Claude` absent) | un seul : `Chronos-v3.2.2.exe`, sans argument, parent explorer ; 0 ancienne | ⚠ **QUATRE overlays** : `Chronos-v3.2.2.exe` PID **87604**, parent 27872 `explorer.exe`, sans argument, créé le **2026-09-27 à 08:07:09** (= T0) — ET 3.1.0 (40772), 3.2.0 (126160), 3.2.1 (121900) toujours en marche : les trois anciennes n'ont PAS été quittées — écart **E1-ter** §3 ; « une seule instance » NON CONSTATÉE |
| Second double-clic (CPT-03) | message « Chronos tourne déjà… » vu ; un seul cadran ; aucun second processus résident | **non rapporté** : l'utilisateur a répondu « continue » sans dire s'il a fait le second lancement ni vu la boîte ; la sonde ne voit qu'un seul processus 3.2.2 résident (cohérent avec un refus, ou avec l'absence de second lancement) — NON CONSTATÉ, à rejouer (E1-ter) |
| **Réconciliation** : sauvegarde horodatée | neuve, datée du lancement, md5 = `52827d6a52cb148b08165f36f46f2401` | `claude-settings-20260927-080710.json`, 3 384 o, md5 **`52827d6a52cb148b08165f36f46f2401`** = temps 0 (mtime conservé 2026-09-26 18:53:11) ✅ |
| **Réconciliation** : fichier courant | = sauvegarde + 9 remplacements `Chronos-v3.2.1.exe` → `Chronos-v3.2.2.exe` (égalité structurelle) ; 9 / 0 | écrit le 2026-09-27 08:07:10 (1 s après T0), md5 `b010cd8733344a134875d2c8a8d43b37`, 3 384 o ; **égalité OCTET à octet** : md5(courant avec `3.2.2` → `3.2.1`) = `52827d6a…` ; égalité structurelle True ; 3.2.2 : **9** / 3.2.1 : **0** (3.2.0 et 3.1.0 : 0) ✅ |
| **Réconciliation** : groupes | 8 `--hook` 3.2.2 + statusLine 3.2.2, 3 `gsd-` intacts, un seul groupe Chronos par clé (8 clés) | **8** hooks 3.2.2, statusLine `…/Chronos-v3.2.2.exe" --statusline`, **3** `gsd-`, un seul groupe Chronos par clé (Notification, PermissionRequest, PostToolUse, PreToolUse, SessionEnd, SessionStart, Stop, UserPromptSubmit) — contrôle automatique (0, 9, 8, 3, True) ✅ |
| Rapport `chronos.log` (réel, 6 710 o, écrit 08:10:32, rapport daté 08:07:11) | « Version : 3.2.2 » ; « Exe courant : …\Chronos-v3.2.2.exe » ; « Vue AppData : réelle » ; « Processus Chronos : 1 (dont ce processus) — 0 autre(s) » ; « Verrou mono-instance (Local\Chronos-overlay) : tenu par ce processus » | « **Version : 3.2.2** » ✅ ; « Exe courant : …\PROJET OVERLAY\Chronos-v3.2.2.exe » ✅ ; `[Magasins persistants]` : « **Vue AppData : réelle** » ✅ ; « dernier exact : %APPDATA%\Chronos\last-exact.json — dernière écriture il y a 3 min (321 o) » ; « journal des relevés : %APPDATA%\Chronos\historique\releves-2026-09.jsonl — dernière écriture il y a 3 min (322 o) » ✅ ; pas de ligne ALERTE ✅ ; « **Processus Chronos : 4 (dont ce processus) — 3 autre(s)** » puis `Chronos-v3.1.0#40772 — démarré il y a 3 j`, `Chronos-v3.2.0#126160 — il y a 17 h 51`, `Chronos-v3.2.1#121900 — il y a 9 h 35`, `Chronos-v3.2.2#87604 — ce processus, il y a 3 min` (⚠ attendu 1 : E1-ter, le rapport dit vrai) ; « **Verrou mono-instance (Local\Chronos-overlay) : tenu par ce processus** » ✅ ; « agrégats de tokens : aucun (phase 33) » |
| **Journal à T+10 min** (constaté a posteriori sur les premières lignes, sonde a) | fichier existe ; mtime < 6 min ; ≥ 2 relevés de `t` distincts ; `demarrage` en tête ; `version 3.2.2` | `historique existe : True` ; `releves-2026-09.jsonl` ; ligne 1 = `{"v":1,"t":"2026-09-27T06:07:10.0489553+00:00","ev":"demarrage","version":"3.2.2"}` ✅ ; relevés à `t` = 06:07:10Z (u5 0 / r5 09:50Z, u7 0,55 / r7 2026-10-02T22:00Z, statut5/7 Autorise, overage_statut Rejete, source SondeEnTetes) puis 06:12:10Z (u5 0,04, u7 0,56) : **2 relevés distincts avant T+10** ✅ ; l'âge « < 6 min » n'a pas été mesuré à T+10 (sonde différée non lancée à T0, cf. §3) mais l'est à T+193 : mtime 11:18:12 pour une sonde à 11:20:30, soit **2 min 18 s** ✅ |
| **Journal à T+60 min** (sonde t60, jouée à 11:21:42 = T+194 min ; preuve : parent `WmiPrvSE.exe`, `Claude` absent) | ≈ 12 ± 2 relevés ; aucun doublon de `t` ; mtime < 6 min ; pas d'`arret` | **12 relevés dans T+0..T+60** ✅ ; puis 11 (T+60..120), 11 (T+120..180), 2 (T+180..194) ; total **36 relevés, 36 `t` distincts, 0 doublon** ✅ ; intervalles min 5,00 / max 6,00 / moyenne 5,46 min, aucun trou > 7 min ; dernier `t` 09:18:11Z, âge 3,5 min ✅ ; événements : `demarrage` seul (pas d'`arret`, pas de `sonde_refusee` ni `jeton_invalide`) ✅ ; 8 790 o, 37 lignes |
| Diagnostic collé (point b) | « Vue AppData : réelle » ; « dernier exact : … il y a N min » (N ≤ 2) ; « journal des relevés : … dernière écriture il y a N min » (N ≤ 5) ; pas d'ALERTE ; « Processus Chronos : 1 … » ; « Verrou … tenu par ce processus » | à relever |
| Carte « Journal des relevés » (réglages) | texte vu par l'utilisateur ; pastille absente ; en-tête « v3.2 » | non rapporté au point (a) (« continue » sans détail) ; à relever au point (b) avec le diagnostic |
| Autres fichiers réels après lancement (sonde a) | vivants | `last-exact.json` 326 o mtime 11:20:11 ; `treated.json` 372 o 11:18:47 ; `settings.json` 536 o 11:17:52 ; `oauth.dat` 08:53:10 — quatre écrivains possibles sur `treated.json`/`last-exact.json` tant que E1-ter dure ; le journal, lui, n'a qu'un écrivain (les anciennes n'en ont pas) |

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
| V01 | galerie des 8 styles (28, 29) | `"<dépôt>\Chronos-v3.2.2.exe" --sessions` via Win+R | trois mots, titre long en ellipse, info-bulles | à relever | à relever | à relever |
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

- **E1-ter (point a, constaté à 11:20 par sonde hors arbre) — point (a) partiellement joué : QUATRE overlays.** L'utilisateur a
  lancé `Chronos-v3.2.2.exe` par l'Explorateur à 08:07:09 (PID 87604, parent explorer — le lanceur est le bon) et a répondu
  « continue » sans rapporter les gestes ; les trois anciennes (3.1.0 PID 40772, 3.2.0 PID 126160, 3.2.1 PID 121900) n'ont PAS été
  quittées. Le mutex `Local\Chronos-overlay` de la 3.2.2 ne les connaît pas : elle l'a pris (« tenu par ce processus ») et le rapport
  compte honnêtement « 4 (dont ce processus) — 3 autre(s) ». Conséquences : **« une seule instance » NON CONSTATÉE** ; **le refus du
  second lancement (CPT-03 en vrai) NON CONSTATÉ** (second double-clic non rapporté ; la sonde ne voit qu'un 3.2.2 résident, ce qui
  ne distingue pas « refusé » de « pas tenté »). La réconciliation et le journal, eux, sont constatés (§0). Rien n'est corrigé
  ici ; **à rejouer** quand l'utilisateur aura quitté les trois anciennes (réglages « v3.1 », « v3.2 », « v3.2 » → « Quitter
  Chronos »), puis double-cliqué `Chronos-v3.2.2.exe` une seconde fois (attendu : boîte « Chronos tourne déjà… », un seul cadran) —
  l'agent relèvera alors par sonde : 1 seul processus Chronos, « Processus Chronos : 1 (dont ce processus) — 0 autre(s) ». Les
  mesures du point (b) restent possibles pour le widget (le journal n'a qu'un écrivain), mais `treated.json` et `last-exact.json`
  ont quatre écrivains tant que E1-ter dure.
- **Note (non écart) — sondes différées T+10 et T+60 non lancées à T0.** L'agent n'a été rappelé qu'à T+193 min ; le journal a
  été relevé une fois pour toutes à T+193/T+194 (sonde a et sonde t60), et les attendus T+10 (≥ 2 relevés distincts avant T0+10,
  `demarrage` en tête) et T+60 (12 relevés dans la première heure) ont été constatés a posteriori sur le contenu daté du fichier,
  l'âge de dernière écriture (2 min 18 s) l'étant à l'heure de la sonde. Le critère « le journal s'écrit » est tenu.
- **Note (non écart)** : le rapport de démarrage dit « usage.json : présent — … (maj il y a 114312 min) » : vestige du pont
  statusLine de juillet, source non retenue par l'arbitrage (l'affichage dit « EXACT · sonde d'en-têtes · frais »).

## 4. Verdict

à rendre (après le point (c) et la sonde T+60).
