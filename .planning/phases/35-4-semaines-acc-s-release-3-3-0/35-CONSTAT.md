# Phase 35 — Constat en production sur la 3.3.0 (critère 5, VAL-05 ; reprise de VAL-04)

**Date :** 2026-09-27. Temps 0 relevé à **19:49:38** (heure locale, UTC+02:00). Protocole écrit à 19:50.
**App bureau Claude :** paquet MSIX `Claude`, version **2.9939.2.0** (celle des phases 27, 31 et 32), famille `Claude_pzs8sxrjxfjjc`.
**Exe publié :** `Chronos-v3.3.0.exe` à la racine du dépôt (`%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY`) —
**78 104 918 o**, md5 `7ec9fbcf95c5716e6b5bbce77ea9c0bc`, FileVersion 3.3.0.0 / ProductVersion 3.3.0 / ProductName Chronos
(identique à 35-06-SUMMARY, release `9b74ed5`). Jamais lancé en overlay par l'agent. Anciens exe présents et intacts :
`Chronos-v3.1.0.exe` (77 218 013 o), `Chronos-v3.2.0.exe` (77 377 466 o), `Chronos-v3.2.1.exe` (77 385 116 o),
`Chronos-v3.2.2.exe` (77 557 997 o).
**Overlays en marche au temps 0 (sonde t0, 19:49:38) :** QUATRE, tous sans argument, tous parent 27872 (`explorer.exe`) :
- `Chronos-v3.1.0.exe` PID **40772**, démarré le 2026-09-23 à 11:09:31 ;
- `Chronos-v3.2.0.exe` PID **126160**, démarré le 2026-09-26 à 14:18:37 ;
- `Chronos-v3.2.1.exe` PID **121900**, démarré le 2026-09-26 à 22:34:35 ;
- `Chronos-v3.2.2.exe` PID **87604**, démarré le 2026-09-27 à 08:07:09 — tient le verrou `Local\Chronos-overlay` et écrit le journal ;
- aucun `Chronos-v3.3.0.exe`.
L'écart E1-ter de `32-CONSTAT.md` n'est pas soldé : le point (a) commence par quitter les QUATRE, à la main.
**Preuve de vue réelle :** tout relevé de fichier Chronos est fait par un processus créé par WMI (`Win32_Process.Create`,
`ReturnValue = 0`), hors de l'arbre de l'app bureau. Sonde t0 : `parent: WmiPrvSE.exe` ; `APPDATA\Claude existe : False`.
C'est ce que voit l'overlay lancé par l'Explorateur, pas la vue virtualisée (copie COW du paquet) que lit toute session.
**Fin de phase côté code :** `dotnet test Chronos.sln -c Debug --nologo -v q` deux fois ⇒ **1648 / 0** et **1648 / 0**
(suite 28 s et 26 s ; 36 s et 30 s mur, compilation comprise) — total identique à 35-06.
**INCLURE_32 = oui** : `32-CONSTAT.md` porte encore 20 lignes « à relever » (§1 L1…Q, §2 V01…V12, deux lignes du §0) et son
§4 « Verdict » est « à rendre » ; dernier commit sur ce fichier : `3fb1cad` (point (a) PARTIEL, E1-ter). Le point (c) du présent
constat rejoue donc le tableau de 32-08, sur la 3.3.0, dans la même séance.

## Règles du constat

- Le protocole est écrit et commité AVANT d'être joué ; les gestes sont ceux de l'utilisateur ; l'agent relève en lecture
  seule, hors de l'arbre de l'app, et **ne lance, n'arrête ni ne clique jamais un overlay, ni la galerie** ; il ne tue aucun
  processus.
- L'agent n'écrit QUE dans `.planning/phases/35-4-semaines-acc-s-release-3-3-0/`, dans `32-CONSTAT.md` (section de reprise,
  seulement si VAL-04 se conclut) et dans `%USERPROFILE%\Documents\chronos-constat` (scripts de sonde `.ps1` et sorties `.txt`,
  supprimés après copie des valeurs ici ; le dossier reste, vide). Jamais d'écriture dans `%APPDATA%\Claude`,
  `%APPDATA%\Chronos`, le cache du paquet, `~/.claude/settings.json`.
- **Sonde hors de l'arbre de l'app, obligatoire** : chaque relevé de fichier Chronos passe par un processus créé par WMI
  (`Invoke-CimMethod -ClassName Win32_Process -MethodName Create`), dont les DEUX premières lignes de sortie prouvent la vue
  réelle : parent `WmiPrvSE.exe` (≠ `claude.exe`) et `Test-Path $env:APPDATA\Claude` = **False**. Une sonde dont la preuve
  échoue est jetée et relancée. Jamais de lecture de `%APPDATA%\Chronos` depuis une session.
- Lectures brèves ; jamais de guet en boucle (les sondes différées attendent par `Start-Sleep` en tête de script, hors de
  l'arbre de l'app).
- **Un écart est un fait consigné au §5, jamais corrigé ici** ; il ouvre une phase 35.x / une 3.3.1 sur décision de
  l'utilisateur, ou part en RETOUR ROADMAP. « Non observé » et « non applicable » sont des réponses valables.
  « cause inconnue » pour une veille est un résultat honnête (D-32-26), pas un écart ; le « hachuré » de VAL-05 se lit
  « rectangle gris à bordure pointillée annoté de sa cause » (la hachure est réservée à « avant le journal », décision 8).
- **Coût annoncé AVANT le geste (décision 7)** : le simple clic au centre bascule % / temps après le délai de double-clic de
  Windows (≈ 0,5 s, 500 ms par défaut) — c'est le prix du double-clic qui ouvre l'Historique.
- La grâce de LUE-02 (2,5 s après la fin du tour, jusqu'à ~5,5 s selon le cycle) est MESURÉE et jugée par l'utilisateur,
  jamais consignée en échec.
- Données personnelles : jamais de titre ni de motif de session ; identifiants tronqués à 8 caractères ; profil écrit
  `%USERPROFILE%`.
- Les anciens exe (`Chronos-v3.1.0.exe` … `Chronos-v3.2.2.exe`) restent sur le disque.
- La phase ne se clôt pas sans ce fichier rempli (§6 « Verdict »).

## Protocole (écrit le 2026-09-27 à 19:50, avant d'être joué)

### Point de contrôle (a) — quitter TOUTES les anciennes, lancer la 3.3.0, tenter une seconde (E1-ter et CPT-03 en vrai)

Dossier du dépôt : `%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY`. md5 de `~/.claude/settings.json` au temps 0 :
`b010cd8733344a134875d2c8a8d43b37`. Overlays encore en marche : 3.1.0 (lancée le 23/09 à 11:09), 3.2.0 (26/09 14:18),
3.2.1 (26/09 22:34), 3.2.2 (27/09 08:07).

1. Quitte CHAQUE Chronos encore en marche, l'un après l'autre (liste ci-dessus : 3.1.0, 3.2.0, 3.2.1, 3.2.2) : clic droit sur
   le cadran → fenêtre de réglages → **« Quitter Chronos »** (la 3.1.0 peut avoir l'ancien menu contextuel).
   **Il ne doit rester AUCUN cadran.** Dis-le quand c'est fait : l'agent le vérifie par une sonde AVANT que tu lances la 3.3.0
   (attends son « 0 processus »).
2. Dans l'**Explorateur de fichiers**, ouvre le dossier du dépôt et **double-clique `Chronos-v3.3.0.exe`** (jamais depuis un
   terminal de l'app Claude ni une session). SmartScreen : « Informations complémentaires » → « Exécuter quand même ».
3. Attends le cadran et le widget (quelques secondes). **Note l'heure** (T0). La reconstruction des tokens démarre en
   arrière-plan (≈ 15 s à froid) : rien à faire.
4. **Double-clique `Chronos-v3.3.0.exe` une SECONDE fois.** Attendu : la boîte « Chronos tourne déjà (une seule instance à la
   fois). Cette copie se retire ; l'autre continue. » — clique OK ; **un seul cadran** reste.
5. Clic droit sur le cadran : l'en-tête affiche **« v3.3 »** ; dans DONNÉES, sous « Sonde d'en-têtes », la carte
   **« Historique d'utilisation »** (bordure violette) avec « Ouvrir », « hebdo / 5 h / tokens · journal du 27 sept. 2026 »,
   une ligne « dernière écriture … », « Style de la vue Semaine : Pistes · Simplifié · Tuiles », « Aussi : double-clic au
   centre du cadran », et **pas** de pastille orange. Referme SANS cliquer « Ouvrir » (c'est le point (b)).
6. Facultatif : « Lancer au démarrage » depuis cette fenêtre (le raccourci visera la 3.3.0).
7. **Laisse l'overlay tourner** jusqu'au lendemain au moins (point (d) : trou réel après une nuit). Ne supprime aucun ancien
   exe.

Signal de reprise : « anciennes quittées : N/N — lancé à HH:MM — second lancement : message vu oui/non, un seul cadran oui/non
— v3.3 vu oui/non — carte Historique : texte vu — autostart : activé/non ».

Attendus relevés par l'agent après le geste (sonde (a), hors arbre) :
- entre l'étape 1 et l'étape 2 : sonde « processus » ⇒ **0 processus Chronos** ;
- après : UN processus `Chronos-v3.3.0.exe` sans argument, parent `explorer.exe`, créé à T0 ; AUCUN
  `Chronos-v3.1.0/3.2.0/3.2.1/3.2.2.exe` résident (des `--hook` fugaces sont normaux) ;
- `~/.claude/settings.json` : sauvegarde NEUVE `%APPDATA%\Chronos\backups\claude-settings-<date de T0>.json` de md5
  `b010cd8733344a134875d2c8a8d43b37` (le temps 0) ; puis 9 remplacements `Chronos-v3.2.2.exe` → `Chronos-v3.3.0.exe`
  (égalité structurelle `apres == json.loads(avant.replace(…))`), 0 / 9, 8 `--hook` 3.3.0 + statusLine 3.3.0, 3 `gsd-`
  intacts, un seul groupe Chronos par clé (8 clés) ;
- `chronos.log` : « Version : 3.3.0 », `[Magasins persistants]` « Vue AppData : réelle », « Processus Chronos : 1 (dont ce
  processus) — 0 autre(s) », « Verrou mono-instance (Local\Chronos-overlay) : tenu par ce processus », section
  `[Journal d'historique]` présente ;
- `%APPDATA%\Chronos\historique\releves-2026-09.jsonl` (ou `-10` si T0 tombe en octobre) : un `"ev":"arret"` de la 3.2.2
  (si elle en écrit un au « Quitter ») puis un `"ev":"demarrage"` `"version":"3.3.0"` à T0, puis des relevés ;
- tokens reconstruits : `tokens-2026-06..09.jsonl`, `ids-*.jsonl`, `curseurs.json`, `couverture.json` présents dans
  `historique\` (sinon, une seule nouvelle sonde 1 min plus tard) ;
- sonde différée **T+10** (`Start-Sleep` jusqu'à T0 + 10 min 30 s), lancée au relevé (a) : ≥ 2 relevés de `t` distincts
  depuis T0, mtime du journal < 6 min.

### Point de contrôle (b) — la fenêtre Historique par les deux gestes, sur les vraies données (H01…H12, ≈ 15 min)

**Coût annoncé : un simple clic au centre bascule après ≈ 0,5 s (le délai de double-clic de Windows) — c'est le prix du
double-clic.**

1. **H01** — Clic droit sur le cadran → carte « Historique d'utilisation » → **« Ouvrir »**. Attendu : la fenêtre Historique
   s'ouvre sur « Semaine de forfait · sam. 26 sept. 00:00 → sam. 3 oct. 00:00 ». Ferme-la (✕ ou Échap).
2. **H02** — **Double-clic au centre du cadran**. Attendu : la fenêtre s'ouvre, et le centre du cadran NE bascule PAS entre %
   et temps.
3. **H03** — Mets une autre fenêtre (l'app Claude) devant l'Historique, puis double-clic au centre. Attendu : l'Historique
   revient au premier plan ; UNE seule entrée « Historique » dans la barre des tâches.
4. **H04** — Un **simple clic** au centre. Attendu : bascule % ↔ temps, une seule fois, après ≈ 0,5 s. Dis si le délai gêne.
5. **H05** — Fais glisser le cadran par ses anneaux (il se range dans un coin) ; clic droit → la fenêtre de réglages s'ouvre
   comme avant.
6. **H06** — Dans l'Historique, vue Semaine : zone hachurée de sam. 26 00:00 à dim. 27 ≈ 08:07 et « journal ouvert le 27 sept.
   2026 » ; la courbe NIVEAU commence après ; ligne du haut « Dernier relevé il y a N min · … ».
7. **H07** — Piste TOKENS CLAUDE CODE : des barres, y compris AVANT dim. 27 08:07 (les tokens sont comptés dans les
   transcripts, les % ne se reconstruisent pas) ; libellé « par heure, comptés localement … ce n'est PAS un % du forfait ».
   Bandeau « Reconstruction des tokens … » vu ou non.
8. **H08** — Puces « Style : Pistes · Simplifié · Tuiles » : les trois se sélectionnent ; puis clic droit sur le cadran → la
   carte des réglages montre le MÊME style en surbrillance ; change-le là, reviens à la fenêtre : il a suivi.
9. **H09** — Segment « Jour » : « Jour · <aujourd'hui> … », ligne « 288 relevés attendus · N présents · … », ligne
   « maintenant ».
10. **H10** — Segment « 4 semaines » : « 4 semaines de forfait · du sam. 5 sept. au sam. 3 oct. 2026 » ; la semaine courante
    en couleur, marquée « journal ouvert le 27 sept. 2026 » ; S-1, S-2, S-3 : « pas de relevés (avant le journal) » ; bande
    COUVERTURE PAR SEMAINE à quatre rangées ; pied « Rien n'est inventé avant l'ouverture du journal. ».
11. **H11** — Clic droit → **« Diagnostic… »** → Ctrl+C → colle le texte dans ta réponse. Attendu : section
    `[Journal d'historique]` (fichiers, « 288 relevés attendus · … », 5 derniers événements, « Reconstruction des tokens :
    terminée le … », « Instances Chronos : 1 »).
12. **H12** — Carte des réglages : sous-texte « hebdo / 5 h / tokens · journal du 27 sept. 2026 » ; ligne « dernière écriture
    il y a N min · … » ; pas de pastille orange.

Signal de reprise : « b fait / H01 : … / H02 : ouverte oui/non, bascule parasite oui/non / H03 : premier plan oui/non, une
entrée oui/non / H04 : bascule après ≈ … s, gênant oui/non / H05 : … / H06 : … / H07 : barres avant le 27 oui/non, bandeau vu
oui/non / H08 : … / H09 : … / H10 : … / H12 : … / Diagnostic : <texte collé> ».

Attendus chiffrés relevés au temps 0 (vue réelle) pour les vérifier : `JournalOuvertLe` = premier `demarrage`
`2026-09-27T06:07:10Z` (08:07:10 locale) ; 134 relevés de `t` distincts de 08:07 à 19:44 (intervalles 1 à 6 min, moyenne
5,24 min, aucun trou > 7 min) ; épisode 16:31 → 16:50 (`sonde_refusee` `SaturationEnTetesLus`, puis `reprise` « trou de
19 min ») pendant lequel les relevés ont CONTINUÉ par l'endpoint OAuth (7 relevés `EndpointOAuthChronos`) : aucun trou de
relevés n'est attendu à cet endroit de la courbe (les événements peuvent apparaître au diagnostic) ; hebdo à 87 % au dernier
relevé (reset `2026-10-02T22:00Z` = sam. 3 oct. 00:00 locale) : aucune semaine épuisée attendue sauf si 100 % est atteint d'ici
là. Si le passage 3.2.2 → 3.3.0 dure plus que l'intervalle de sonde, un petit trou « Chronos arrêté » à T0 est juste, pas un
écart.

### Point de contrôle (c) — le tableau de 32-08 (L1…Q) et les douze vérifications (V01…V12), sur la 3.3.0 (≈ 30 min)

INCLURE_32 = oui. Protocole de 31-CONSTAT / 32-CONSTAT, mot pour mot, `3.2.2` → `3.3.0`. L'overlay reste ouvert.

**Tableau des gestes (point (b) de 32-08).** Consignes : ne jamais cliquer sur le widget pendant une mesure ; « invite longue »
= « Sans utiliser aucun outil, écris un texte d'environ 1200 mots sur <sujet>. » ; ne pas se servir de la session GSD pour les
mesures. Le journal est relevé pendant ce temps par une sonde hors de l'arbre de l'app : ne pas fermer l'overlay.

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
   affiche « Réflexion », le noter (limite connue). NE PAS répondre à la question avant les vérifications ci-dessous.

**Les douze vérifications (point (c) de 32-08).**

1. V07 (suite) : cliquer S1 (la question est à l'écran). Attendu : S1 quitte le widget avant la réponse ; dire si cela gêne.
   Répondre à la question : S1 « Réflexion » pendant le travail.
2. V08 + V06 : dans S2, « Écris trois phrases, puis termine en me demandant de choisir entre deux options, sans utiliser l'outil
   AskUserQuestion. » ; cliquer aussitôt S3. Attendu : S2 « En attente » ; survoler sa ligne : si l'app a classé `blocked`,
   l'info-bulle porte le motif, lisible.
3. V09 : dans S3, invite longue (les boussoles), puis AVANT la fin aller sur l'accueil ou une conversation Chat (fenêtre Claude
   au premier plan). Attendu (limite écrite) : S3 disparaît probablement sans être vue.
4. V12 (seulement si Claude Code tourne aussi en console) : refaire 3 avec la console devant ; attendu : S3 reste « En attente ».
   Sinon « non applicable ».
5. V01 : Win+R, `"%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.3.0.exe" --sessions` (n'écrit rien, ne
   réconcilie rien, ne prend pas le verrou). Attendu : 8 tuiles, les trois mots (« En attente ? » atténué mais lisible), le
   titre long `api-migration` coupé par une ellipse sans élargir la tuile, info-bulle « titre — dossier — mot », celle
   d'`overlay` sur deux lignes avec « permission demandée ». Fermer la galerie.
6. V02 (facultatif) : clic droit → THÈME, passer les 9 thèmes, les mots restent lisibles.
7. V05 : un titre long est coupé proprement par une ellipse sans élargir le widget (« vu » / « non observé »).
8. V03, V04 : clic droit → « Diagnostic… » → Ctrl+C → coller (rapport final : il porte aussi `[Magasins persistants]` et
   `[Journal d'historique]` — le journal doit dire « dernière écriture il y a N min » avec N ≤ 5 et toujours pas d'ALERTE).
9. V11 : pendant tout le constat, une session est-elle restée « En attente » après avoir été ouverte et lue ?

V10 (latence d'écriture de `lastFocusedAt`) est relevée par l'agent seul, en lecture brève des fichiers d'état du paquet.

Signaux de reprise : « b fait / L1 : … / L3 : … / L2 : … / L2b : … / L4 : … / Q : … / Diagnostic : <collé> » puis « c fait /
V07 : … / V08+V06 : … / V09 : … / V12 : … / V01 : … / V02 : … / V05 : … / V11 : … / Rapport final : <collé> ».

### Point de contrôle (d) — le lendemain : un trou réel, les libellés relus, le verdict de l'utilisateur

Au moins une nuit s'est écoulée avec l'overlay 3.3.0 (PC éteint, ou mis en veille). Avant de présenter ce point, l'agent lance
une sonde hors arbre (`sonde-d`) qui relève les événements du journal de la nuit (dernier relevé de la veille, premier du matin,
`arret` / `demarrage` / `reprise` entre les deux) et annonce la cause que la fenêtre DOIT afficher (« Chronos arrêté » si arrêt
/ redémarrage, « cause inconnue » si veille — les deux sont honnêtes).

1. Ouvre l'Historique (double-clic au centre). Vue **Semaine** : le trou de la nuit est un **rectangle gris à bordure
   pointillée**, la ligne NIVEAU s'y interrompt, et il porte sa cause (celle annoncée par l'agent). S'il y a eu consommation
   pendant l'absence : bloc plat « +N % pendant l'absence (répartition inconnue) ».
2. Vue **Jour** d'aujourd'hui (si le trou chevauche minuit) : « 1 interruption (<cause>, <jour> HH:MM → HH:MM) » dans la ligne
   de fraîcheur, et le trou nommé au début de la journée ; rien n'est dessiné avant le premier relevé du matin.
3. **Relis les libellés d'honnêteté**, sur chaque vue, et dis s'ils sont justes et lisibles : pied de page « Aucun trou n'est
   interpolé. Les pourcentages sont des relevés exacts du serveur ; les tokens sont un comptage local partiel, sur leur propre
   axe. » ; libellé des tokens « … ce n'est PAS un % du forfait » ; « journal ouvert le 27 sept. 2026 » ; « pas de relevés
   (avant le journal) » ; « Rien n'est inventé avant l'ouverture du journal. » ; cadre violet « consommé ailleurs » s'il
   apparaît ; causes des trous.
4. **Ton verdict** en une ou deux phrases : ce qui te sert, ce qui te gêne, écarts compris (lisibilité, délai du simple clic,
   maquettes).

Signal de reprise : « d fait / PC la nuit : éteint / veille / allumé / Semaine : trou vu oui/non, cause lue : … / Jour : ligne
lue : … / Libellés : justes oui/non, remarques : … / Verdict : … ».

## 0. Temps 0, une seule instance, réconciliation, journal

| contrôle | attendu | constaté |
|---|---|---|
| Vue réelle de la sonde t0 (19:49:38) | parent `WmiPrvSE.exe` ; `Claude` absent de `%APPDATA%` | `parent: WmiPrvSE.exe` ; `APPDATA\Claude existe : False` ✅ (idem pour la sonde complémentaire t0b de 19:50:08) |
| Processus Chronos au temps 0 | 3.1.0, 3.2.0, 3.2.1, 3.2.2 sans argument, parent explorer ; aucune 3.3.0 | PID 40772 (3.1.0, 23/09 11:09:31), 126160 (3.2.0, 26/09 14:18:37), 121900 (3.2.1, 26/09 22:34:35), 87604 (3.2.2, 27/09 08:07:09), tous parent 27872 `explorer.exe`, sans argument ; 0 × 3.3.0 ✅ (E1-ter toujours ouvert) |
| `~/.claude/settings.json` au temps 0 | md5, taille, date | md5 **`b010cd8733344a134875d2c8a8d43b37`**, 3 384 o, 2026-09-27 08:07:10 (même valeur lue depuis la session et par la sonde) |
| Occurrences `Chronos-v3.2.2.exe` | 9 | **9** (3.3.0 : 0 ; 3.2.1 : 0 ; 3.2.0 : 0 ; 3.1.0 : 0) |
| Groupes `--hook` Chronos 3.2.2 | 8 | **8** (un groupe par clé : Notification, PermissionRequest, PostToolUse, PreToolUse, SessionEnd, SessionStart, Stop, UserPromptSubmit) |
| Groupes tiers `gsd-` | 3 | **3** |
| Fin de `statusLine.command` | `…/Chronos-v3.2.2.exe" --statusline` | `OVERLAY/Chronos-v3.2.2.exe" --statusline` ✅ |
| `%APPDATA%\Chronos\backups\` (réel) | liste | `claude-settings-20260923-110933.json` (962 o, md5 `ffe9c80c…`), `…-20260926-141838.json` (3 384 o, `9eab8a8e…`), `…-20260926-163746.json` (3 384 o, `3c684224…`), `…-20260927-080710.json` (3 384 o, `52827d6a…`) — la dernière est celle du lancement de la 3.2.2 |
| `%APPDATA%\Chronos\settings.json` (réel) | `SessionsWidgetEnabled = True` (sinon STOP : purge des 8 groupes) | **True** (536 o, écrit le 2026-09-27 17:23:32 ; `SondeEnTetesActivee` True, `RefreshIntervalSeconds` 60, thème `ardoise`, `CadranMode` Normal) ✅ pas de STOP |
| `%APPDATA%\Chronos\last-exact.json` (réel) | vivant | 326 o, mtime 19:49:11 ; `captured_at 2026-09-27T17:44:11Z`, u5 0,18 / r5 `19:50Z`, u7 0,87 / r7 `2026-10-02T22:00Z` ✅ |
| `%APPDATA%\Chronos\historique\` (réel) | `releves-2026-09.jsonl` seul ; aucun `tokens-*` (la 3.2.2 ne reconstruit pas) | **`releves-2026-09.jsonl` seul** (32 999 o, mtime 19:44:12) ; aucun `tokens-*`, `ids-*`, `curseurs.json`, `couverture.json` ; aucun sous-dossier ✅ |
| Journal au temps 0 | `demarrage` 3.2.2 en tête ; relevés ≈ 1 / 5 min | 137 lignes : 134 relevés (134 `t` distincts, 0 doublon) + 3 événements ; ligne 1 `{"v":1,"t":"2026-09-27T06:07:10.0489553+00:00","ev":"demarrage","version":"3.2.2"}` ✅ ; événements : `demarrage` 3.2.2 (06:07:10Z), `sonde_refusee` `SaturationEnTetesLus` (14:31:11Z), `reprise` « trou de 19 min » (14:50:11Z) — relevés continus pendant l'épisode par `EndpointOAuthChronos` ; intervalles min 1,00 / max 6,00 / moyenne 5,24 min, aucun trou > 7 min ; dernier `t` **17:44:11Z** (19:44:11 locale), âge 5 min 27 s à la sonde ✅ |
| `%APPDATA%\Chronos\chronos.log` (réel) | rapport d'un ancien overlay | 6 710 o, mtime 08:10:32, rapport daté 08:07:11, « **Version : 3.2.2** », « Processus Chronos : 4 (dont ce processus) — 3 autre(s) », « Verrou mono-instance (Local\Chronos-overlay) : tenu par ce processus », « agrégats de tokens : aucun (phase 33) » |
| Autres fichiers réels | — | `treated.json` 319 o 19:42:58 ; `oauth.dat` 518 o 16:41:12 ; `archived.json` et `usage.json` (juillet, vestiges) ; `sessions\` absent (APP-06) |
| Dossier Démarrage (réel) | aucun `Chronos.lnk` | aucun fichier (autostart non activé, facultatif) |
| `Chronos-v3.3.0.exe` | 78 104 918 o, md5 `7ec9fbcf…`, 3.3.0.0 / 3.3.0 | **78 104 918 o, md5 `7ec9fbcf95c5716e6b5bbce77ea9c0bc`, FileVersion 3.3.0.0, ProductVersion 3.3.0, ProductName Chronos** ✅ |
| App Claude | 2.9939.2.0 | 2.9939.2.0 ✅ |
| Dossier de sonde `Documents\chronos-constat` | existe, vide | existe, vide après suppression de `sonde-t0.*` et `sonde-t0b.*` |
| Processus avant lancement (sonde entre l'étape 1 et l'étape 2 du point (a)) | **0 processus Chronos** | à relever |
| Processus après lancement (sonde a) | un seul : `Chronos-v3.3.0.exe`, sans argument, parent explorer, créé à T0 ; 0 ancienne | à relever |
| Second double-clic (CPT-03) | message « Chronos tourne déjà… » vu ; un seul cadran ; aucun second processus résident | à relever |
| **Réconciliation** : sauvegarde horodatée | neuve, datée de T0, md5 = `b010cd8733344a134875d2c8a8d43b37` | à relever |
| **Réconciliation** : fichier courant | = sauvegarde + 9 remplacements `Chronos-v3.2.2.exe` → `Chronos-v3.3.0.exe` (égalité structurelle) ; 0 / 9 | à relever |
| **Réconciliation** : groupes | 8 `--hook` 3.3.0 + statusLine 3.3.0, 3 `gsd-` intacts, un seul groupe Chronos par clé (8 clés) | à relever |
| Rapport `chronos.log` après lancement | « Version : 3.3.0 » ; « Vue AppData : réelle » ; « Processus Chronos : 1 (dont ce processus) — 0 autre(s) » ; « Verrou … tenu par ce processus » ; `[Journal d'historique]` présent | à relever |
| Journal qui continue | `demarrage` `"version":"3.3.0"` à T0, puis relevés | à relever |
| Tokens reconstruits | `tokens-*.jsonl`, `ids-*.jsonl`, `curseurs.json`, `couverture.json` dans `historique\` | à relever |
| Journal à T+10 min (sonde t10) | ≥ 2 relevés de `t` distincts depuis T0 ; mtime < 6 min | à relever |

## 1. La fenêtre Historique (VAL-05)

| id | geste | attendu | vu (utilisateur) | relevé (agent) | verdict |
|---|---|---|---|---|---|
| H01 | Réglages → carte « Historique d'utilisation » → « Ouvrir » | fenêtre ouverte sur « Semaine de forfait · sam. 26 sept. 00:00 → sam. 3 oct. 00:00 » | à relever | à relever | à relever |
| H02 | double-clic au centre, fenêtre fermée | fenêtre ouverte ; aucune bascule % / temps | à relever | à relever | à relever |
| H03 | double-clic au centre, fenêtre ouverte derrière une autre | premier plan ; une seule entrée « Historique » dans la barre des tâches | à relever | à relever | à relever |
| H04 | simple clic au centre | une bascule % ↔ temps, après ≈ 0,5 s (coût annoncé, jugé par l'utilisateur) | à relever | à relever | à relever |
| H05 | drag par les anneaux ; clic droit | rangement dans un coin ; réglages comme avant | à relever | à relever | à relever |
| H06 | vue Semaine, semaine courante | zone hachurée sam. 26 00:00 → dim. 27 ≈ 08:07, « journal ouvert le 27 sept. 2026 », NIVEAU après, « Dernier relevé il y a N min · … » | à relever | à relever | à relever |
| H07 | piste TOKENS CLAUDE CODE | barres y compris avant dim. 27 08:07 ; « … ce n'est PAS un % du forfait » ; bandeau de reconstruction vu ou non | à relever | à relever | à relever |
| H08 | puces Pistes · Simplifié · Tuiles ; carte des réglages | trois styles sélectionnables ; la carte et la fenêtre se suivent dans les deux sens | à relever | à relever | à relever |
| H09 | segment « Jour » | « Jour · <aujourd'hui> … », « 288 relevés attendus · N présents · … », ligne « maintenant » | à relever | à relever | à relever |
| H10 | segment « 4 semaines » | « du sam. 5 sept. au sam. 3 oct. 2026 » ; S en couleur avec « journal ouvert le 27 sept. 2026 » ; S-1…S-3 « pas de relevés (avant le journal) » ; couverture à 4 rangées ; pied « Rien n'est inventé avant l'ouverture du journal. » | à relever | à relever | à relever |
| H11 | « Diagnostic… » collé | `[Journal d'historique]` : fichiers, « 288 relevés attendus · … », 5 derniers événements, « Reconstruction des tokens : terminée le … », « Instances Chronos : 1 » | à relever | à relever | à relever |
| H12 | carte des réglages | « hebdo / 5 h / tokens · journal du 27 sept. 2026 » ; « dernière écriture il y a N min · … » ; pas de pastille orange | à relever | à relever | à relever |
| H13 | le lendemain : trou réel (§4) | rectangle gris à bordure pointillée annoté de sa cause (celle annoncée par la sonde d) | à relever | à relever | à relever |
| H14 | le lendemain : libellés relus, verdict (§4) | libellés justes et lisibles ; verdict écrit | à relever | à relever | à relever |

## 2. Les lignes du tableau de 32-08

| id | situation (tableau de l'utilisateur) | geste | attendu | vu (utilisateur) | relevé (agent) | verdict |
|---|---|---|---|---|---|---|
| L1 | session qui réfléchit | S1, invite longue, regardée | « Réflexion » sous le titre | à relever | à relever | à relever |
| L2 | finie, non lue, puis ouverte | S2 finie pendant que S1 est ouverte, puis clic S2 | « En attente » qui reste ; disparaît sans clic après le clic S2 | à relever | à relever | à relever |
| L2b | finie, fenêtre Claude au second plan, puis alt-tab | S3, Explorateur devant | « En attente » qui reste ; disparaît après l'alt-tab | à relever | à relever | à relever |
| L3 | finie et lue en direct | S1 regardée jusqu'à la fin | « En attente » ≤ ~5 s puis disparition sans clic | à relever | à relever | à relever |
| L4 | lue, qui se remet à travailler | S2, invite longue | réapparaît « Réflexion », puis « En attente » | à relever | à relever | à relever |
| Q | question posée, non regardée | S1 AskUserQuestion, S3 devant | S1 « En attente » au-dessus de S2 | à relever | à relever | à relever |

## 3. Vérifications déférées

| id | vérification (phase d'origine) | geste | attendu | vu (utilisateur) | relevé (agent) | verdict |
|---|---|---|---|---|---|---|
| V01 | galerie des 8 styles (28, 29) | `"<dépôt>\Chronos-v3.3.0.exe" --sessions` via Win+R | trois mots, titre long en ellipse, info-bulles | à relever | à relever | à relever |
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

## 4. Trou réel et libellés (le lendemain)

à relever (sonde d : dernier relevé de la veille, premier du matin, événements entre les deux, cause attendue ; puis réponse de
l'utilisateur au point (d)).

## 5. Écarts

(aucun à ce stade — E1-ter de `32-CONSTAT.md` reste ouvert jusqu'au relevé du point (a))

## 6. Verdict

à rendre (après le point (d)).

**Clôture de v1.8 (2026-10-03) :** verdict non rendu — le constat n'est pas joué (gestes de l'utilisateur requis). Le milestone v1.8
est clos avec cet écart connu (VAL-05) ; le constat est **reporté dans la phase de constat du milestone suivant** (phase 43, exe
3.5.0), avec le tableau L1…Q / V01…V12 de `32-CONSTAT.md`. Voir `.planning/MILESTONES.md` › v1.8 › Known Gaps.
