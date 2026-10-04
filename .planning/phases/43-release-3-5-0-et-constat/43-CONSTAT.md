# Phase 43 — Constat en production sur la 3.5.0 (critères 3 et 4, VAL-06 constatée, VAL-07 ; reprises de VAL-04 et VAL-05)

**Date :** 2026-10-04. Temps 0 relevé à **15:13:57** (heure locale, UTC+02:00). Protocole écrit à 15:20.
**App bureau Claude :** paquet MSIX `Claude`, version **2.19675.0.0** (2.9939.2.0 aux constats 32 et 35 : l'app a été mise à
jour depuis), famille `Claude_pzs8sxrjxfjjc`.
**Exe publié :** `Chronos-v3.5.0.exe` à la racine du dépôt (`%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY`) —
**78 199 851 o**, SHA-256 **`3577e72a1a8eb71fa85edf88750da918d04d01154f718458382ca4a4482d8071`** (recalculé par la sonde t0 :
identique à 43-03-SUMMARY et à `docs/publish.md` §4), FileVersion 3.5.0.0 / ProductVersion 3.5.0 / ProductName Chronos
(release `e56fe57`, sans étiquette ni push). **Jamais lancé par l'agent, ni en overlay, ni avec `--hook`, ni avec `--sessions`.**
Anciens exe présents et intacts : `Chronos-v3.1.0.exe` (77 218 013 o), `Chronos-v3.2.0.exe` (77 377 466 o), `Chronos-v3.2.1.exe`
(77 385 116 o), `Chronos-v3.2.2.exe` (77 557 997 o), `Chronos-v3.3.0.exe` (78 104 918 o), `Chronos-v3.3.1.exe` (78 111 106 o),
`Chronos-v3.4.0.exe` (78 142 335 o) — 8 exe au total.
**Overlays en marche au temps 0 (sonde t0, 15:13:57) :** UN seul, sans argument, parent 27872 (`explorer.exe`) :
- `Chronos-v3.4.0.exe` PID **38256**, démarré le 2026-09-28 à 14:28:29, dans le dossier du dépôt ;
- aucun `Chronos-v3.5.0.exe`, aucune version antérieure à la 3.4.0, aucun `--hook` résident.
La 3.4.0 connaît le verrou `Local\Chronos-overlay` : tant qu'elle tourne, la 3.5.0 répondrait « Chronos tourne déjà… » et se
retirerait. Le point (a) commence donc par la quitter, à la main (E1-ter).
**Preuve de vue réelle :** tout relevé de fichier Chronos est fait par un processus créé par WMI (`Win32_Process.Create`,
`ReturnValue = 0`), hors de l'arbre de l'app bureau. Sonde t0 : `parent: WmiPrvSE.exe` ; `APPDATA\Claude existe : False`.
C'est ce que voit l'overlay lancé par l'Explorateur, pas la vue virtualisée (copie COW du paquet) que lit toute session.
(Première tentative de la sonde t0 jetée sans sortie — script réécrit avec un bloc `finally` qui écrit toujours sa sortie, puis
relancé ; aucune valeur n'a été tirée de la tentative jetée.)
**Fin de phase côté code (43-03) :** `dotnet test Chronos.sln -c Debug` deux fois ⇒ **2 223 réussis, 0 échec, 1 ignoré
(total 2 224)** les deux fois ; `-c Release` ⇒ 2 223 / 0 / 1 (2 224) ; builds Debug et Release `-warnaserror` : 0 avertissement.
**Reprises :** `32-CONSTAT.md` (VAL-04, verdict non rendu, E1-ter ouvert) et `35-CONSTAT.md` (VAL-05, verdict non rendu) ont été
reportés ici à la clôture de v1.8. Ils ne sont PAS modifiés : leur reprise est consignée dans le présent fichier (§4, §5, §6).

## Règles du constat

- Le protocole est écrit et commité AVANT d'être joué ; les gestes sont ceux de l'utilisateur ; l'agent relève en lecture
  seule, hors de l'arbre de l'app, et **ne lance JAMAIS l'exe, même avec `--hook` ou `--sessions`** ; il n'arrête ni ne clique
  aucun overlay, ni la galerie ; il ne tue aucun processus.
- L'agent n'écrit QUE dans `.planning/phases/43-release-3-5-0-et-constat/` et dans `%USERPROFILE%\Documents\chronos-constat`
  (scripts de sonde `.ps1` et sorties `.txt`, supprimés après copie des valeurs ici ; le dossier reste, vide). Jamais d'écriture
  dans `%APPDATA%\Claude`, `%APPDATA%\Chronos`, le cache du paquet, `~/.claude/settings.json`, `shell:startup`, `oauth.dat`.
- **Sonde hors de l'arbre de l'app, obligatoire** : chaque relevé de fichier Chronos passe par un processus créé par WMI
  (`Invoke-CimMethod -ClassName Win32_Process -MethodName Create`), dont les DEUX premières lignes de sortie prouvent la vue
  réelle : parent `WmiPrvSE.exe` (≠ `claude.exe`) et `Test-Path $env:APPDATA\Claude` = **False**. Une sonde dont la preuve
  échoue est jetée et relancée. Jamais de lecture de `%APPDATA%\Chronos` ni de `~/.claude/settings.json` depuis une session pour
  conclure (vue virtualisée : le malentendu de CPT-02).
- `~/.claude/settings.json` : la sonde n'en recopie que le md5, la taille, la date, les occurrences de chaque `Chronos-vX.Y.Z.exe`,
  le nombre de groupes `--hook` Chronos (par clé) et `gsd-`, et la présence / la fin de `statusLine.command` si elle est
  Chronos — aucun autre contenu. `oauth.dat` n'est jamais lu.
- Lectures brèves ; jamais de guet en boucle (une sonde différée attend par `Start-Sleep` en tête de script, hors de l'arbre).
- **Un écart est un fait consigné au §8, jamais corrigé ici** ; il ouvre une 3.5.1 / une phase 43.x sur décision de
  l'utilisateur, ou part en RETOUR ROADMAP. « Non observé » et « non applicable » sont des réponses valables. « cause
  inconnue » pour une veille est un résultat honnête (D-32-26), pas un écart ; le trou se lit « rectangle gris à bordure
  pointillée annoté de sa cause » (la hachure est réservée à « avant le journal »).
- **Coût annoncé AVANT le geste** : le simple clic sur le cadran bascule % / temps ≈ 0,5 s après le relâchement (délai de
  double-clic de Windows, 500 ms par défaut) — c'est le prix du double-clic qui ouvre l'Historique.
- La grâce de LUE-02 (2,5 s après la fin du tour, jusqu'à ~5,5 s selon le cycle) est MESURÉE et jugée par l'utilisateur,
  jamais consignée en échec.
- Données personnelles : jamais de titre ni de motif de session ; identifiants tronqués à 8 caractères ; profil écrit
  `%USERPROFILE%`.
- Les anciens exe (`Chronos-v3.1.0.exe` … `Chronos-v3.4.0.exe`) restent sur le disque (des sessions ouvertes avant la
  réconciliation les appellent encore).
- **Publication GitHub hors périmètre** (push, étiquette `exe-v3.5.0`, release) : seulement après ce constat ET un accord
  explicite séparé de l'utilisateur.
- La phase ne se clôt pas sans ce fichier rempli (`## Verdict`).

## Protocole (écrit le 2026-10-04 à 15:20, avant d'être joué)

Dossier du dépôt, noté `<dépôt>` : `%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY`. md5 de `~/.claude/settings.json`
au temps 0 : **`6d7712d75d5934d72be0643ecff47d48`**. Durée totale ≈ 2 h, dont 15 min d'attente au point (h) ; les points
(c) à (g) peuvent se jouer dans l'ordre qui t'arrange, (h) → (i) dans cet ordre.

### Point (a) — Préalable : tout quitter, vérifier l'empreinte, smoke (E1-ter ; smoke transféré de 43-03)

1. Quitte le Chronos encore en marche relevé au temps 0 : **`Chronos-v3.4.0.exe`** (PID 38256, lancé le 28/09 à 14:28) — clic
   droit sur le cadran → fenêtre de réglages → **« Quitter Chronos »** (en bas du rail). Si un autre cadran apparaît, quitte-le
   aussi. **Il ne doit rester AUCUN cadran.** Contrôle : Gestionnaire des tâches → onglet « Détails » → aucune ligne
   `Chronos*.exe` (la sonde a le confirmera par `chronos.log` : « Processus Chronos : 1 (dont ce processus) — 0 autre(s) »).
2. Empreinte : ouvre PowerShell par le menu Démarrer (pas depuis l'app Claude) et tape
   `Get-FileHash "$env:USERPROFILE\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.5.0.exe" -Algorithm SHA256`.
   Attendu : `3577E72A1A8EB71FA85EDF88750DA918D04D01154F718458382CA4A4482D8071` (casse indifférente).
3. Ouvre une invite de commandes par **Win+R → `cmd` → Entrée** (jamais depuis un terminal de l'app Claude ni d'une session :
   l'exe y verrait la vue virtualisée d'AppData). Puis tape `cd /d "%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY"`.
4. Smoke de l'argument inconnu : `start "" /wait "Chronos-v3.5.0.exe" --zzz` puis `echo %ERRORLEVEL%`.
   Attendu : **0**, aucune fenêtre, aucune boîte, aucun cadran. (`start /wait` est nécessaire : sans lui, `cmd` n'attend pas un
   exe fenêtré et `%ERRORLEVEL%` serait celui de la commande précédente.) SmartScreen éventuel au tout premier lancement :
   « Informations complémentaires » → « Exécuter quand même » (à noter).
5. Smoke du mode hook : `start "" /wait "Chronos-v3.5.0.exe" --hook SessionStart < NUL` puis `echo %ERRORLEVEL%`.
   Attendu : **0**, aucune fenêtre, aucun processus `Chronos-v3.5.0.exe` qui reste dans le Gestionnaire des tâches. Ce mode
   sort avant le Host : il ne prouve ni le verrou ni le journal, et ne doit PAS toucher `~/.claude/settings.json` (preuve au §1 :
   la sauvegarde du point (b) aura le md5 du temps 0).
6. Ferme l'invite de commandes.

Signal de reprise (a) : « a fait / 3.4.0 quittée oui/non, aucun Chronos dans Détails oui/non / empreinte identique oui/non /
--zzz : code …, fenêtre oui/non, SmartScreen oui/non / --hook SessionStart : code …, fenêtre oui/non, processus resté oui/non ».

### Point (b) — Lancement par l'Explorateur, instance unique, barre retirée côté Claude Code

1. Dans l'**Explorateur de fichiers**, ouvre `<dépôt>` et **double-clique `Chronos-v3.5.0.exe`**. **Note l'heure** (T0).
   Attends le cadran et le widget de sessions (quelques secondes : extraction des DLL natives au premier lancement).
2. **Double-clique `Chronos-v3.5.0.exe` une SECONDE fois.** Attendu : la boîte « Chronos tourne déjà (une seule instance à la
   fois). Cette copie se retire ; l'autre continue. » — clique OK ; **un seul cadran** reste, inchangé.
3. Clic droit sur le cadran : l'en-tête de la fenêtre de réglages affiche **« v3.5 »** ; section Données : la carte « Barre de
   statut de Claude Code » a disparu. Referme.
4. Ouvre une **NOUVELLE** session Claude Code (pas une session déjà ouverte : celles-ci appellent encore la 3.4.0). Attendu :
   **plus de barre de statut Chronos** en bas de la session ; la session apparaît dans le widget de sessions (hooks repointés
   vers la 3.5.0).
5. Le reste (sauvegarde, md5, 8 groupes, statusLine, rapport) est relevé par la sonde a après le point de contrôle.

Signal de reprise (b) : « b fait / lancé à HH:MM / second lancement : message vu oui/non, un seul cadran oui/non / v3.5 vu
oui/non, carte barre absente oui/non / nouvelle session : barre Chronos absente oui/non, visible dans le widget oui/non ».

### Point (c) — Huit variantes × quatre coins, à 100 % puis 150 %, gestes en clics réels (≈ 40 min)

**Mode d'emploi.** Changer de variante : clic droit sur le cadran → Réglages → **Apparence** → style du cadran (Anneaux,
Braises, Fusible, Marée, Volets) ; carte **« Orientation »** pour Fusible, Marée, Volets (Horizontal / Vertical) ; **« Mode
étendu »** pour Anneaux (Normal puis Étendu). Amener le cadran dans chaque coin en le **glissant** (il s'accroche au coin le plus
proche). Empreintes attendues : Fusible 190 × 92 DIP, Volets 190 × 66 DIP ; Anneaux et Braises = disque.

**Pour chaque variante, dans chaque coin** (HG haut-gauche, HD haut-droite, BG bas-gauche, BD bas-droite) : aucun débordement
(rien de coupé par le bord de l'écran ni par la barre des tâches), fenêtre à l'empreinte du cadran (pas de grand rectangle
invisible qui mange les clics autour), collée au coin, juste après le changement de style ou d'orientation.

**Gestes, sur toute la silhouette** (le disque pour Anneaux et Braises, le rectangle arrondi pour Fusible, Marée, Volets) —
essaie le bord comme le centre :
- **clic** sans bouger = bascule % ↔ temps, ≈ 0,5 s après le relâchement (coût annoncé) ;
- **double-clic** = la fenêtre Historique s'ouvre, SANS bascule du cadran (referme-la) ;
- **appuyer-glisser** au-delà du seuil de Windows = le cadran suit, puis s'accroche au coin le plus proche ;
- **clic droit** = Réglages ;
- **clic à côté** de la silhouette (dans un coin du carré englobant d'un disque, ou juste à côté d'un rectangle) = le clic
  **traverse** vers le bureau ou la fenêtre derrière (ex. une icône du bureau se sélectionne).

**À 150 %** : Paramètres Windows → Système → Affichage → Échelle → 150 % ; refaire les quatre coins de chaque variante (les
gestes une fois, sur une variante disque et une variante rectangle, suffisent si tout a marché à 100 %). Remettre ton échelle
habituelle à la fin. **Second écran** : si tu en as un, glisser le cadran dessus et refaire un coin par variante ; sinon
« non applicable ».

**Pastilles d'Arcs** (42-UAT test 2) : en Anneaux, les pastilles (âge du relevé, alertes) gardent leur propre clic et leur
info-bulle, SANS déclencher la bascule ; les pastilles inertes hors du disque captent la souris sans geste (voulu).

Le tableau à cocher est au §2. Signal de reprise (c) : une ligne par variante — « <variante> : coins 100 % HG/HD/BG/BD ✓✗ ;
gestes clic/double/glisser/droit/dehors ✓✗ ; coins 150 % HG/HD/BG/BD ✓✗ ; remarque » — plus « pastilles : … » et « second
écran : … / non applicable ».

### Point (d) — Braises

En style Braises : **20 braises en 5 groupes d'une heure séparés par un vide** ; **flèche fixe à midi** ; après un clic
(mode temps) : **« ↻ HH:MM »** = heure du reset de la fenêtre de 5 h ; absent si le reset est inconnu ou dépassé (« non
observé » si tu ne peux pas provoquer ce cas). Dire si on lit d'un coup d'œil où s'arrête la fenêtre.

Signal de reprise (d) : « d : 20 braises / 5 groupes oui/non ; flèche à midi oui/non ; ↻ HH:MM vu : <heure> ; lisible : … ».

### Point (e) — Quinze thèmes en trois groupes

Réglages → Apparence → thème : les **15** thèmes, rangés en trois groupes **Pâle · Classique · Vive**. Pour chacun : les mots
du cadran et du widget restent lisibles ; le **gris épuisé** est visible (≥ 3:1 attendu ; « non observé » si aucune fenêtre
n'est épuisée et que l'aperçu ne le montre pas) ; les quatre cadrans alternatifs (**Braises, Fusible, Marée, Volets**) suivent le
thème (vérifier au moins un thème par groupe sur chacun). Remettre ton thème habituel (au temps 0 : `aurore`).

Signal de reprise (e) : « e : 15 thèmes en 3 groupes oui/non ; illisible(s) : … ; gris épuisé vu / non observé ; cadrans
alternatifs thémés oui/non ».

### Point (f) — Historique sur tes vraies données, plein écran (35-CONSTAT H01…H14 adaptés, 38-UAT)

H08 « trois styles » est **retiré** : la 3.5 n'a plus qu'un style (Pistes) et aucun sélecteur de style.

1. **H01** — Clic droit → Réglages → section **Historique** → « Ouvrir ». Attendu : la fenêtre s'ouvre sur « Semaine de forfait ·
   <début> → <fin> » (la semaine courante, bornée par le reset hebdo du serveur). Ferme-la (✕ ou Échap).
2. **H02** — Double-clic sur le cadran : la fenêtre s'ouvre, le cadran NE bascule PAS.
3. **H03** — Une autre fenêtre devant l'Historique, puis double-clic sur le cadran : l'Historique revient au premier plan ; UNE
   seule entrée « Historique » dans la barre des tâches.
4. **H04** — Simple clic : une bascule, une seule fois, ≈ 0,5 s après. Dire si le délai gêne.
5. **H05** — Glisser le cadran (accroche au coin) ; clic droit → Réglages comme d'habitude.
6. **H06** — Vue Semaine : ligne de fraîcheur « Dernier relevé il y a N min · … » ; la courbe NIVEAU suit tes relevés ; aucun
   trou inventé ; « journal ouvert le 27 sept. 2026 » si l'ouverture du journal tombe dans la vue.
7. **H07** — Piste **TOKENS CLAUDE CODE** : des barres ; libellé « par heure, comptés localement … ce n'est PAS un % du forfait ».
8. **H09** — Segment « Jour » : « Jour · <aujourd'hui> … », « 288 relevés attendus · N présents · … », ligne « maintenant ».
9. **H10** — Segment « 4 semaines » : quatre semaines de forfait ; les semaines d'avant le 27 sept. « pas de relevés (avant le
   journal) » (zone hachurée) ; bande COUVERTURE PAR SEMAINE ; pied « Rien n'est inventé avant l'ouverture du journal. ».
10. **H11** — Réglages → **Diagnostic** → Actualiser → Copier → colle le texte dans ta réponse. Attendu : section `[Journal
    d'historique]` (fichiers, « 288 relevés attendus · … », derniers événements, reconstruction des tokens terminée, « Instances
    Chronos : 1 ») et section « [Réglages de Claude Code] » (barre retirée, nom de la sauvegarde).
11. **H12** — Réglages → Historique : sous-texte « hebdo / 5 h / tokens · journal du 27 sept. 2026 », « dernière écriture il y a
    N min · … », pas de pastille orange.
12. **Plein écran (38-UAT test 1)** — Dans l'Historique, bouton **« ⛶ Plein écran »** : la fenêtre couvre l'écran courant, barre
    des tâches comprise ; pistes agrandies dans leurs proportions ; textes ≈ ×1,35 ; aucun défilement ; « ⤢ Quitter le plein
    écran · Échap » visible. **Échap** : sort du plein écran ; **Échap** une seconde fois : ferme la fenêtre. Rouvre, passe en
    plein écran par **F11**, ressors par **F11** : la position et la taille d'avant reviennent.
13. **Échelle et multi-écrans (38-UAT test 2)** — Même chose à 150 % et sur un second écran (« non applicable » sinon) ; rien de
    tronqué sur un petit écran (< 1 280 px de large) si tu en as un.

H13 (trou réel) et H14 (libellés relus, verdict) sont joués aux points (i) et (k).

Signal de reprise (f) : « f fait / H01 : … / H02 : ouverte oui/non, bascule parasite oui/non / H03 : … / H04 : … s, gênant
oui/non / H05 : … / H06 : … / H07 : … / H09 : … / H10 : … / H12 : … / plein écran bouton : … / Échap 1 : … / Échap 2 : … / F11 : … /
géométrie restaurée oui/non / 150 % : … / second écran : … / Diagnostic : <texte collé> ».

### Point (g) — Widget de sessions : reprise du tableau de 32-08 (L1…Q) et des douze vérifications (V01…V12), sur la 3.5.0 (≈ 30 min)

Protocole de 31/32/35-CONSTAT, mot pour mot, version `3.5.0`. L'overlay reste ouvert.

**Tableau des gestes.** Consignes : ne jamais cliquer sur le widget pendant une mesure ; « invite longue » = « Sans utiliser
aucun outil, écris un texte d'environ 1200 mots sur <sujet>. » ; ne pas se servir de la session GSD pour les mesures.

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
5. Diagnostic — MAINTENANT, avant L4 : Réglages → Diagnostic → Actualiser → Copier → coller dans la réponse. Attendu : S1, S2,
   S3 parmi les « Sessions MASQUÉES », chacune avec une cause ; section `[Magasins persistants]` : « Vue AppData : réelle »,
   « dernier exact : … dernière écriture il y a N min » (N ≤ 2), « journal des relevés : … dernière écriture il y a N min »
   (N ≤ 5), aucune ligne « ALERTE — journal muet », « Processus Chronos : 1 (dont ce processus) — 0 autre(s) » (des hooks de moins
   de 10 s peuvent s'y ajouter), « Verrou mono-instance (Local\Chronos-overlay) : tenu par ce processus ».
6. L4 — S2, déjà lue, qui se remet à travailler : invite longue (les sabliers), cliquer aussitôt S1. Attendu : S2 réapparaît
   « Réflexion », puis « En attente » à la fin, et reste.
7. Q — une question posée, non regardée : dans S1, « Réfléchis quelques secondes sans outil, puis pose-moi une question à choix
   multiple (A ou B) avec l'outil AskUserQuestion. » ; cliquer aussitôt S3. Attendu : S1 « En attente » AU-DESSUS de S2. Si S1
   affiche « Réflexion », le noter (limite connue). NE PAS répondre à la question avant les vérifications ci-dessous.

**Les douze vérifications.**

1. V07 (suite) : cliquer S1 (la question est à l'écran). Attendu : S1 quitte le widget avant la réponse ; dire si cela gêne.
   Répondre à la question : S1 « Réflexion » pendant le travail.
2. V08 + V06 : dans S2, « Écris trois phrases, puis termine en me demandant de choisir entre deux options, sans utiliser l'outil
   AskUserQuestion. » ; cliquer aussitôt S3. Attendu : S2 « En attente » ; survoler sa ligne : si l'app a classé `blocked`,
   l'info-bulle porte le motif, lisible.
3. V09 : dans S3, invite longue (les boussoles), puis AVANT la fin aller sur l'accueil ou une conversation Chat (fenêtre Claude
   au premier plan). Attendu (limite écrite) : S3 disparaît probablement sans être vue.
4. V12 (seulement si Claude Code tourne aussi en console) : refaire 3 avec la console devant ; attendu : S3 reste « En attente ».
   Sinon « non applicable ».
5. V01 : Win+R, `"%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.5.0.exe" --sessions` (n'écrit rien, ne
   réconcilie rien, ne prend pas le verrou). Attendu : 8 tuiles, les trois mots (« En attente ? » atténué mais lisible), le titre
   long `api-migration` coupé par une ellipse sans élargir la tuile, info-bulle « titre — dossier — mot », celle d'`overlay` sur
   deux lignes avec « permission demandée ». Fermer la galerie.
6. V02 : les **15** thèmes (déjà passés au point (e)) : les mots du widget restent lisibles.
7. V05 : un titre long est coupé proprement par une ellipse sans élargir le widget (« vu » / « non observé »).
8. V03, V04 : Réglages → Diagnostic → Actualiser → Copier → coller (rapport final : `[Magasins persistants]` et `[Journal
   d'historique]` — le journal doit dire « dernière écriture il y a N min » avec N ≤ 5 et toujours pas d'ALERTE).
9. V11 : pendant tout le constat, une session est-elle restée « En attente » après avoir été ouverte et lue ?

V10 (latence d'écriture de `lastFocusedAt`) est relevée par l'agent seul, en lecture brève des fichiers d'état du paquet, par
sonde hors arbre.

Signaux de reprise (g) : « g fait / L1 : … / L3 : … / L2 : … / L2b : … / L4 : … / Q : … / Diagnostic : <collé> » puis « V07 : … /
V08+V06 : … / V09 : … / V12 : … / V01 : … / V02 : … / V05 : … / V11 : … / Rapport final : <collé> ».

### Point (h) — Alt+F4 termine le processus

1. Un clic sur le cadran pour le mettre au premier plan (il basculera : sans importance), puis **Alt+F4**. Attendu : le cadran
   disparaît, le widget aussi.
2. Gestionnaire des tâches → « Détails » : plus de `Chronos-v3.5.0.exe` (la sonde a le confirmera, et le journal doit porter un
   `arret`). **Note l'heure.**
3. **Ne relance PAS pendant au moins 15 minutes** : c'est le trou réel du point (i).

Signal de reprise (h) : « h : Alt+F4 à HH:MM, cadran disparu oui/non, processus absent des Détails oui/non ».

### Point (i) — Relance par l'Explorateur et trou réel annoté (35-07, H13 / H14)

1. Après ≥ 15 min, relance `Chronos-v3.5.0.exe` par double-clic dans l'Explorateur. **Note l'heure.**
2. Double-clic sur le cadran → Historique → vue **Jour** puis vue **Semaine** : le trou de (h) est un **rectangle gris à bordure
   pointillée** annoté **« Chronos arrêté »** ; la ligne NIVEAU s'y interrompt ; vue Jour : « 1 interruption (Chronos arrêté,
   HH:MM → HH:MM) » dans la ligne de fraîcheur. S'il y a eu consommation pendant l'absence : bloc plat « +N % pendant l'absence
   (répartition inconnue) ».
3. **Relis les libellés d'honnêteté**, sur chaque vue, et dis s'ils sont justes et lisibles : pied de page « Aucun trou n'est
   interpolé. Les pourcentages sont des relevés exacts du serveur ; les tokens sont un comptage local partiel, sur leur propre
   axe. » ; libellé des tokens « … ce n'est PAS un % du forfait » ; « journal ouvert le 27 sept. 2026 » ; « pas de relevés (avant
   le journal) » ; « Rien n'est inventé avant l'ouverture du journal. » ; cadre violet « consommé ailleurs » s'il apparaît ;
   causes des trous.
4. Relancer ne doit PAS réécrire `~/.claude/settings.json` (idempotence : relevé par la sonde, aucune seconde sauvegarde).

Signal de reprise (i) : « i : relancé à HH:MM / trou vu oui/non, annotation lue : … / Jour : ligne lue : … / libellés justes
oui/non, remarques : … ».

### Point (j) — Déconnexion → « données indisponibles »

1. Réglages → **Données** → **« Se déconnecter »**. Attendu : le cadran passe à **« données indisponibles »** — aucun chiffre,
   ni % ni « ≥ ». Note combien de temps cela prend.
2. Reconnecte-toi avec le **MÊME** compte (pastille ou Réglages → Données). Attendu : les chiffres reviennent en ≤ ~5 min.
   (Limite DS3-01 documentée : ne pas tester ici avec un AUTRE compte.)

Signal de reprise (j) : « j : déconnecté → données indisponibles oui/non (après … s), chiffres affichés pendant oui/non /
reconnecté, chiffres revenus en … min ».

### Point (k) — Ton verdict

En une ou deux phrases : ce qui te sert, ce qui te gêne, écarts compris (lisibilité des huit variantes, gestes, délai du
simple clic, Braises, thèmes, plein écran, widget). C'est aussi la revue visuelle (`.zeus/DESIGN_PLAN_CYCLE2.md` §9 : huit
variantes à 100 % et 150 % dans les quatre coins sans débordement ; zones en clics réels et clic qui traverse ; Braises ;
Historique sans sélecteur de style, plein écran, Échap à deux niveaux, F11 ; 3 groupes, 15 thèmes, gris épuisé visible,
cadrans alternatifs thémés).

## Relevés de l'agent après le point de contrôle (attendus écrits maintenant)

Sonde **a** (WMI, mêmes règles et même preuve que t0), lancée au retour de l'utilisateur :
- **Processus** : 1 seul `Chronos-v3.5.0.exe` sans argument, parent `explorer.exe`, créé à l'heure de la relance du point (i) ;
  0 `Chronos-v3.4.0.exe` ni aucune ancienne (des `--hook` fugaces sont normaux).
- **Sauvegarde** : UNE sauvegarde neuve `%APPDATA%\Chronos\backups\claude-settings-<horodatage de T0>.json` (T0 = premier lancement
  du point (b)), de md5 **`6d7712d75d5934d72be0643ecff47d48`** = md5 du temps 0 — preuve que ni le « Quitter » de la 3.4.0 ni le
  smoke `--hook` n'ont écrit `~/.claude/settings.json`. Aucune seconde sauvegarde neuve due à la relance (i) (idempotence).
- **`~/.claude/settings.json`** : écrit à T0 ; **statusLine absente** (aucune barre de l'utilisateur à restaurer :
  `InnerStatusLineCommand` absente/vide au temps 0) ; **8** groupes `--hook` vers `Chronos-v3.5.0.exe`, un seul groupe Chronos par
  clé (Notification, PermissionRequest, PostToolUse, PreToolUse, SessionEnd, SessionStart, Stop, UserPromptSubmit) ; occurrences
  `Chronos-v3.5.0.exe` = 8, `Chronos-v3.4.0.exe` = 0, aucune autre version ; **3** groupes `gsd-` intacts. mtime inchangé par la
  relance (i).
- **`chronos.log`** (rapport de la relance, sinon celui de T0) : « Version : 3.5.0 », « Exe courant : …\Chronos-v3.5.0.exe »,
  « Vue AppData : réelle », « Processus Chronos : 1 (dont ce processus) — 0 autre(s) », « Verrou mono-instance
  (Local\Chronos-overlay) : tenu par ce processus », section « [Réglages de Claude Code] » (barre retirée et nom de la
  sauvegarde à T0 ; rien à faire à la relance) — sans aucun jeton recopié.
- **Journal** `%APPDATA%\Chronos\historique\releves-2026-10.jsonl` : `arret` de la 3.4.0 au point (a) (si elle en écrit un) ;
  `"ev":"demarrage"` `"version":"3.5.0"` à T0 ; relevés ≈ 1 / 5 min (≥ 2 `t` distincts entre T0 et T0 + 10 min) ; `arret` au point
  (h) ; trou ≥ 15 min sans relevé ; `demarrage` 3.5.0 à la relance (i) ; mtime < 6 min à la sonde.
- **Raccourci Démarrage** : absent au temps 0 → reste absent (la 3.5 ne le crée jamais).
- **V10** : écart date d'écriture − `lastFocusedAt` sur les fichiers d'état du paquet (lecture brève).

## 0. Temps 0 et réconciliation

| contrôle | attendu | vu (utilisateur) | relevé (agent) | verdict |
|---|---|---|---|---|
| Vue réelle de la sonde t0 (15:13:57) | parent `WmiPrvSE.exe` ; `Claude` absent de `%APPDATA%` | — | `parent: WmiPrvSE.exe` ; `APPDATA\Claude existe : False` | ✅ |
| Processus Chronos au temps 0 | liste | — | 1 : `Chronos-v3.4.0.exe` PID 38256, sans argument, parent 27872 `explorer.exe`, créé le 2026-09-28 14:28:29, dans le dépôt ; 0 × 3.5.0 | relevé |
| `~/.claude/settings.json` au temps 0 | md5, taille, date | — | md5 **`6d7712d75d5934d72be0643ecff47d48`** (= 43-03, avant et après la construction), 3 384 o, 2026-09-28 06:40:57 | relevé |
| Occurrences d'exe dans `~/.claude/settings.json` | une seule version | — | `Chronos-v3.4.0.exe` = **9** (8 hooks + statusLine) ; aucune autre version | relevé |
| Groupes `--hook` Chronos | 8, un par clé | — | **8** (SessionStart, PostToolUse, PreToolUse, UserPromptSubmit, Stop, SessionEnd, PermissionRequest, Notification : 1 chacun, tous 3.4.0) | relevé |
| Groupes tiers `gsd-` | 3 | — | **3** | relevé |
| `statusLine.command` | Chronos 3.4.0 | — | Chronos, fin `…OJET OVERLAY/Chronos-v3.4.0.exe" --statusline` | relevé |
| `%APPDATA%\Chronos\backups\` (réel) | liste | — | `claude-settings-20260926-163746.json` (3 384 o, md5 `3c684224…`), `…-20260927-080710.json` (`52827d6a…`), `…-20260927-203946.json` (`b010cd87…`), `…-20260927-211009.json` (`7ba368b2…`), `…-20260928-064057.json` (`5ba5bc6f…`) — 5 fichiers, tous 3 384 o ; la dernière date du lancement de la 3.4.0 | relevé |
| `%APPDATA%\Chronos\settings.json` (réel) | `SessionsWidgetEnabled = True` (sinon la réconciliation RETIRERAIT les 8 groupes) | — | **True** (874 o, écrit le 2026-10-04 14:22:00) ; `CadranStyle` Arcs, `CadranMode` Normal, `ThemeKey` `aurore`, orientations non enregistrées (valeurs par défaut), `InnerStatusLineCommand` absente/vide → barre retirée, rien à restaurer | ✅ attendu inchangé |
| `chronos.log` (réel) | rapport de l'overlay en marche | — | 8 073 o, mtime 2026-09-28 14:33:54, « Date : 2026-09-28 14:28:31 », « Version : 3.4.0 », « Vue AppData : réelle », « Processus Chronos : 1 (dont ce processus) — 0 autre(s) », « Exe courant : %USERPROFILE%\…\Chronos-v3.4.0.exe » | relevé |
| `last-exact.json` (réel) | vivant | — | 324 o, mtime 15:13:35 (22 s avant la sonde) | ✅ |
| Journal (réel) | vivant | — | `releves-2026-09.jsonl`, `releves-2026-10.jsonl` (245 287 o, 942 lignes, mtime 15:12:36) ; dernière ligne = relevé `t` 13:12:35Z ; aucun événement dans le fichier d'octobre (le `demarrage` 3.4.0 du 28/09 est dans celui de septembre) ; 12 autres fichiers dans `historique\` (tokens, ids, curseurs, couverture) | ✅ |
| Dossier Démarrage (réel) | — | — | aucun `Chronos.lnk` ; aucun autre fichier | relevé |
| `Chronos-v3.5.0.exe` | 78 199 851 o, SHA-256 `3577e72a…`, 3.5.0.0 | — | 78 199 851 o, FileVersion 3.5.0.0, SHA-256 `3577e72a1a8eb71fa85edf88750da918d04d01154f718458382ca4a4482d8071` | ✅ |
| Dossier de sonde `Documents\chronos-constat` | existe, vide | — | existe, vide après suppression de `sonde-t0.*` | ✅ |
| (a) 3.4.0 quittée à la main, aucun cadran (E1-ter) | aucun `Chronos*.exe` dans « Détails » avant le lancement | à relever | à relever | à relever |
| (a) Empreinte comparée par l'utilisateur | `3577E72A…8071` | à relever | à relever | à relever |
| (a) Smoke `--zzz` | code 0, aucune fenêtre | à relever | à relever | à relever |
| (a) Smoke `--hook SessionStart < NUL` | code 0, aucune fenêtre, aucun processus résident ; `~/.claude/settings.json` non écrit (md5 de la sauvegarde = t0) | à relever | à relever | à relever |
| (b) Lancement par l'Explorateur | cadran et widget ; T0 noté | à relever | à relever | à relever |
| (b) Second double-clic (instance unique) | « Chronos tourne déjà… » ; un seul cadran ; aucun second processus résident | à relever | à relever | à relever |
| (b) En-tête « v3.5 », carte barre absente | vu | à relever | à relever | à relever |
| Processus après le constat (sonde a) | 1 seul `Chronos-v3.5.0.exe`, sans argument, parent `explorer.exe` ; 0 ancienne | — | à relever | à relever |
| Rapport `chronos.log` (sonde a) | « Version : 3.5.0 » ; « Vue AppData : réelle » ; « Processus Chronos : 1 … 0 autre(s) » ; « Verrou … tenu par ce processus » ; « [Réglages de Claude Code] » | — | à relever | à relever |
| Journal depuis T0 (sonde a) | `demarrage` 3.5.0 à T0 ; ≥ 2 `t` distincts à T0 + 10 min ; ≈ 1 / 5 min | — | à relever | à relever |
| Raccourci Démarrage (sonde a) | toujours absent | — | à relever | à relever |

## 1. Barre, sauvegarde, hooks (critère 3, transfert de 43-03)

| contrôle | attendu | vu (utilisateur) | relevé (agent) | verdict |
|---|---|---|---|---|
| Barre de statut d'une NOUVELLE session Claude Code | plus de barre Chronos | à relever | — | à relever |
| statusLine de `~/.claude/settings.json` (sonde a) | absente (rien à restaurer) | — | à relever | à relever |
| Sauvegarde neuve (sonde a) | une seule, datée de T0, md5 `6d7712d75d5934d72be0643ecff47d48` (= t0) | — | à relever | à relever |
| Hooks repointés (sonde a) | 8 groupes `--hook` vers `Chronos-v3.5.0.exe`, un par clé ; 0 `Chronos-v3.4.0.exe` ; 3 `gsd-` intacts | — | à relever | à relever |
| Nouvelle session visible dans le widget | apparaît (hooks 3.5.0 actifs) | à relever | — | à relever |
| Idempotence à la relance (i) (sonde a) | `~/.claude/settings.json` non réécrit ; aucune seconde sauvegarde | — | à relever | à relever |

## 2. Variantes et gestes (critère 4 ; 42-UAT ; revue visuelle §9-1 et §9-2)

| variante | coins 100 % : HG · HD · BG · BD | gestes : clic · double-clic · glisser · clic droit · clic dehors traverse | coins 150 % : HG · HD · BG · BD | remarques | verdict |
|---|---|---|---|---|---|
| Anneaux (Arcs ; Normal puis Étendu) | à relever | à relever | à relever | à relever | à relever |
| Braises | à relever | à relever | à relever | à relever | à relever |
| Fusible H | à relever | à relever | à relever | à relever | à relever |
| Fusible V | à relever | à relever | à relever | à relever | à relever |
| Marée V | à relever | à relever | à relever | à relever | à relever |
| Marée H | à relever | à relever | à relever | à relever | à relever |
| Volets H | à relever | à relever | à relever | à relever | à relever |
| Volets V | à relever | à relever | à relever | à relever | à relever |
| Pastilles d'Arcs (42-UAT test 2) | — | propre clic et info-bulle, sans bascule ; pastilles inertes captent la souris sans geste | — | à relever | à relever |
| Second écran | un coin par variante | — | — | à relever | à relever |

Délai du simple clic jugé par l'utilisateur (coût annoncé ≈ 0,5 s) : à relever.

## 3. Braises et thèmes (revue visuelle §9-3 et §9-5)

| contrôle | attendu | vu (utilisateur) | relevé (agent) | verdict |
|---|---|---|---|---|
| Braises : groupes | 20 braises en 5 groupes d'une heure séparés par un vide | à relever | — | à relever |
| Braises : flèche | fixe à midi | à relever | — | à relever |
| Braises : mode temps | « ↻ HH:MM » (reset de la fenêtre de 5 h) ; absent si inconnu ou dépassé | à relever | — | à relever |
| Thèmes : rangement | 15 thèmes en 3 groupes Pâle · Classique · Vive | à relever | — | à relever |
| Thèmes : lisibilité | mots du cadran et du widget lisibles sur les 15 | à relever | — | à relever |
| Thèmes : gris épuisé | visible sur chaque thème (≥ 3:1), ou « non observé » | à relever | — | à relever |
| Thèmes : cadrans alternatifs | Braises, Fusible, Marée, Volets suivent le thème | à relever | — | à relever |

## 4. Historique et plein écran (reprise de VAL-05 / 35-07 ; 38-UAT ; revue visuelle §9-4)

| id | geste | attendu | vu (utilisateur) | relevé (agent) | verdict |
|---|---|---|---|---|---|
| H01 | Réglages → Historique → « Ouvrir » | fenêtre sur « Semaine de forfait · <début> → <fin> » | à relever | à relever | à relever |
| H02 | double-clic sur le cadran, fenêtre fermée | fenêtre ouverte ; aucune bascule | à relever | à relever | à relever |
| H03 | double-clic, fenêtre ouverte derrière une autre | premier plan ; une seule entrée dans la barre des tâches | à relever | à relever | à relever |
| H04 | simple clic | une bascule après ≈ 0,5 s (coût annoncé, jugé par l'utilisateur) | à relever | à relever | à relever |
| H05 | glisser ; clic droit | accroche au coin ; Réglages | à relever | à relever | à relever |
| H06 | vue Semaine | « Dernier relevé il y a N min · … » ; NIVEAU sur les relevés ; aucun trou inventé | à relever | à relever | à relever |
| H07 | piste TOKENS CLAUDE CODE | barres ; « … ce n'est PAS un % du forfait » | à relever | à relever | à relever |
| H08 | (retiré : un seul style, Pistes ; aucun sélecteur) | aucun sélecteur de style dans la fenêtre ni dans les Réglages | à relever | — | à relever |
| H09 | segment « Jour » | « Jour · <aujourd'hui> … », « 288 relevés attendus · N présents · … », ligne « maintenant » | à relever | à relever | à relever |
| H10 | segment « 4 semaines » | avant le 27 sept. « pas de relevés (avant le journal) » ; couverture par semaine ; « Rien n'est inventé avant l'ouverture du journal. » | à relever | à relever | à relever |
| H11 | Diagnostic copié | `[Journal d'historique]` ; « Instances Chronos : 1 » ; « [Réglages de Claude Code] » | à relever | à relever | à relever |
| H12 | Réglages → Historique | « hebdo / 5 h / tokens · journal du 27 sept. 2026 » ; « dernière écriture il y a N min » ; pas de pastille orange | à relever | à relever | à relever |
| H13 | trou réel du point (h) | rectangle gris à bordure pointillée « Chronos arrêté » ; ligne interrompue | à relever | à relever | à relever |
| H14 | libellés relus, verdict | libellés justes et lisibles ; verdict écrit | à relever | — | à relever |
| P1 | « ⛶ Plein écran » | écran courant, barre des tâches comprise ; pistes proportionnelles ; textes ≈ ×1,35 ; aucun défilement ; « ⤢ Quitter le plein écran · Échap » | à relever | — | à relever |
| P2 | Échap, puis Échap | sort du plein écran, puis ferme la fenêtre | à relever | — | à relever |
| P3 | F11, puis F11 | entre puis sort ; position et taille restaurées | à relever | — | à relever |
| P4 | 150 % ; second écran ; petit écran | même comportement ; rien de tronqué (< 1 280 px) | à relever | — | à relever |

## 5. Widget de sessions — lignes du tableau de 32-08 (reprise de VAL-04)

| id | situation (tableau de l'utilisateur) | geste | attendu | vu (utilisateur) | relevé (agent) | verdict |
|---|---|---|---|---|---|---|
| L1 | session qui réfléchit | S1, invite longue, regardée | « Réflexion » sous le titre | à relever | à relever | à relever |
| L2 | finie, non lue, puis ouverte | S2 finie pendant que S1 est ouverte, puis clic S2 | « En attente » qui reste ; disparaît sans clic après le clic S2 | à relever | à relever | à relever |
| L2b | finie, fenêtre Claude au second plan, puis alt-tab | S3, Explorateur devant | « En attente » qui reste ; disparaît après l'alt-tab | à relever | à relever | à relever |
| L3 | finie et lue en direct | S1 regardée jusqu'à la fin | « En attente » ≤ ~5 s puis disparition sans clic | à relever | à relever | à relever |
| L4 | lue, qui se remet à travailler | S2, invite longue | réapparaît « Réflexion », puis « En attente » | à relever | à relever | à relever |
| Q | question posée, non regardée | S1 AskUserQuestion, S3 devant | S1 « En attente » au-dessus de S2 | à relever | à relever | à relever |

## 6. Vérifications déférées (V01…V12)

| id | vérification (phase d'origine) | geste | attendu | vu (utilisateur) | relevé (agent) | verdict |
|---|---|---|---|---|---|---|
| V01 | galerie des 8 styles (28, 29) | `"<dépôt>\Chronos-v3.5.0.exe" --sessions` via Win+R | trois mots, titre long en ellipse, info-bulles | à relever | à relever | à relever |
| V02 | 15 thèmes (28 ; 9 thèmes à l'origine) | Réglages → Apparence | mots lisibles | à relever | à relever | à relever |
| V03 | section « Source app-bureau » (29) | rapport final | trouvée, racine du paquet, fichiers d'état | à relever | à relever | à relever |
| V04 | causes de masquage et « Règle « lue » » (30) | rapport final | chaque masquée a une cause ; section présente | à relever | à relever | à relever |
| V05 | borne de 160 DIP (29) | widget | titre coupé sans élargir | à relever | à relever | à relever |
| V06 | info-bulle longue (29) | survol de S2 | lisible | à relever | à relever | à relever |
| V07 | PermissionRequest pour AskUserQuestion ; question vue puis répondue (28, 30) | S1 | disparition avant la réponse ; « Réflexion » après | à relever | à relever | à relever |
| V08 | question classée `blocked` par l'app (29) | S2 | « En attente » + motif si classée | à relever | à relever | à relever |
| V09 | accueil ou Chat au premier plan (30) | S3 | limite écrite : disparition probable | à relever | à relever | à relever |
| V10 | latence d'écriture de `lastFocusedAt` (27, 30) | relevé agent | écart date d'écriture − `lastFocusedAt` | — | à relever | à relever |
| V11 | réserve de la phase 27 | tout le constat | aucune session lue restée « En attente » | à relever | à relever | à relever |
| V12 | CLI homonyme `claude` (30) | console devant | reste « En attente » | à relever | à relever | à relever |

## 7. Arrêt, trou réel, déconnexion

| contrôle | attendu | vu (utilisateur) | relevé (agent) | verdict |
|---|---|---|---|---|
| (h) Alt+F4 sur le cadran | le cadran et le widget disparaissent | à relever | — | à relever |
| (h) Processus après Alt+F4 | plus de `Chronos-v3.5.0.exe` ; `arret` dans le journal à l'heure notée | à relever | à relever | à relever |
| (i) Relance par l'Explorateur | `demarrage` 3.5.0 ; trou ≥ 15 min sans relevé entre `arret` et `demarrage` | à relever | à relever | à relever |
| (i) Trou dans l'Historique | rectangle gris à bordure pointillée « Chronos arrêté », ligne interrompue, « 1 interruption (Chronos arrêté, …) » en vue Jour | à relever | à relever | à relever |
| (i) Libellés d'honnêteté | justes et lisibles | à relever | — | à relever |
| (j) « Se déconnecter » | « données indisponibles », aucun chiffre ni « ≥ » | à relever | — | à relever |
| (j) Reconnexion, même compte | chiffres revenus en ≤ ~5 min | à relever | à relever | à relever |

## 8. Écarts

à relever (après le point de contrôle). Aucun à ce stade. Au temps 0, E1-ter de `32-CONSTAT.md` / `35-CONSTAT.md` se réduit à
UNE ancienne en marche (la 3.4.0, qui connaît le verrou) ; il est soldé si la sonde a ne voit qu'une 3.5.0.

## Verdict

à rendre (après le point (k)) : verdict de l'utilisateur, puis synthèse par critère — VAL-06 critère 3 (barre retirée constatée
par sonde hors arbre après un lancement par l'Explorateur) ; VAL-07 (constat joué, verdict écrit, écarts compris) ; reprise de
VAL-04 (32-08 : instance unique après E1-ter, L1…Q, V01…V12, journal qui s'écrit) et de VAL-05 (35-07 : Historique sur vraies
données, trou réel annoté) conclues ou non.

## Signal de reprise global

À recopier et compléter, point par point (« non observé » / « non applicable » valables ; décris ce qui bloque si un point
n'a pas pu être joué) :

```
a fait / 3.4.0 quittée oui/non, aucun Chronos dans Détails oui/non / empreinte identique oui/non /
  --zzz : code …, fenêtre oui/non / --hook SessionStart : code …, fenêtre oui/non, processus resté oui/non
b fait / lancé à HH:MM / second lancement : message vu oui/non, un seul cadran oui/non / v3.5 vu oui/non /
  nouvelle session : barre Chronos absente oui/non, visible dans le widget oui/non
c : Anneaux coins 100 % ✓✓✓✓, gestes ✓✓✓✓✓, 150 % ✓✓✓✓, remarque …
    Braises … / Fusible H … / Fusible V … / Marée V … / Marée H … / Volets H … / Volets V …
    pastilles : … / second écran : … ou non applicable / délai du clic : gênant oui/non
d : 20 braises / 5 groupes oui/non, flèche à midi oui/non, ↻ HH:MM vu : …, lisible : …
e : 15 thèmes / 3 groupes oui/non, illisible(s) : …, gris épuisé vu / non observé, cadrans alternatifs thémés oui/non
f fait / H01 … / H02 … / H03 … / H04 … / H05 … / H06 … / H07 … / H09 … / H10 … / H12 … /
  plein écran bouton … / Échap 1 … / Échap 2 … / F11 … / géométrie restaurée oui/non / 150 % … / second écran … /
  Diagnostic : <collé>
g fait / L1 … / L3 … / L2 … / L2b … / L4 … / Q … / Diagnostic : <collé> /
  V07 … / V08+V06 … / V09 … / V12 … / V01 … / V02 … / V05 … / V11 … / Rapport final : <collé>
h : Alt+F4 à HH:MM, cadran disparu oui/non, processus absent oui/non
i : relancé à HH:MM, trou vu oui/non, annotation : …, libellés justes oui/non, remarques : …
j : données indisponibles oui/non (après … s), chiffres revenus en … min
Verdict : …
```
