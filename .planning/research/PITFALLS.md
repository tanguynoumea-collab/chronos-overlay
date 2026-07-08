# Pitfalls Research

**Domain:** Overlay WPF transparent always-on-top lisant des sources locales non documentées (objet d'usage Claude Code + transcripts JSONL) pour afficher des quotas
**Researched:** 2026-07-08
**Confidence:** HIGH (technique WPF/.NET vérifiée par docs officielles) / MEDIUM (format des sources Claude non documenté, par nature incertain)

> Ce document existe pour empêcher trois familles d'erreurs qui provoquent des réécritures :
> 1. **Fragilité de la source non documentée** — justifie l'abstraction `IUsageProvider` et une tâche de découverte AVANT de coder les providers.
> 2. **Threading WPF** — tout accès UI hors du thread Dispatcher fait planter l'app de façon intermittente.
> 3. **Honnêteté des chiffres** — ne jamais présenter une estimation ou un compte à rebours dérivant comme un chiffre exact.

---

## Critical Pitfalls

### Pitfall 1 : Dépendre d'un format/emplacement de source non documenté sans point de rupture isolé

**What goes wrong:**
Les champs `five_hour` / `seven_day` (utilization + resets_at) qui alimentent `/usage` proviennent d'une source locale non publiée par Anthropic. Son emplacement exact, son format, sa clé, voire son existence même sur disque ne sont pas garantis. Une simple mise à jour de l'app Claude peut renommer un champ, changer un chemin, chiffrer le fichier, ou déplacer la donnée vers un endpoint réseau non lisible. Si le parsing est câblé en dur dans le ViewModel/cadran, toute la chaîne casse silencieusement et l'overlay affiche des chiffres faux ou rien.

**Why it happens:**
On code contre ce qu'on observe aujourd'hui en supposant que c'est stable. C'est une API privée de facto : aucun contrat, aucune promesse de compatibilité, aucun changelog.

**How to avoid:**
- **Découverte AVANT code.** Tâche de reconnaissance dédiée en tout début de projet : localiser précisément la source, capturer un échantillon réel, documenter chemin + schéma + hypothèses dans `docs/data-sources.md`. Ne pas coder de provider avant que ce document existe.
- **Abstraction `IUsageProvider`** stricte : le cadran ne connaît qu'un `UsageSnapshot` normalisé (utilization 0..1, resets_at, source, confiance/estimé). Aucun code de parsing ne fuit vers l'UI.
- **Parsing défensif et versionnable** : détecter le schéma, tolérer champ absent → renvoyer `null`/indisponible plutôt que de crasher ou d'inventer une valeur.
- **Test de contrat** sur l'échantillon capturé, pour détecter une rupture de format dès qu'on remplace l'échantillon par une capture récente.

**Warning signs:**
- Le parsing du provider primaire dépend d'un chemin absolu codé en dur sans fallback.
- Un champ manquant lève une exception au lieu de dégrader vers « indisponible ».
- `docs/data-sources.md` n'existe pas encore mais du code lit déjà la source.

**Phase to address:**
Phase 1 (Découverte des sources — préalable bloquant) ; conception de l'abstraction en Phase pipeline de données.

---

### Pitfall 2 : `AllowsTransparency=True` force le rendu logiciel — animations et effets s'effondrent

**What goes wrong:**
Activer `AllowsTransparency=True` sur une `Window` (obligatoire pour un overlay per-pixel sans fond) fait basculer WPF en **rendu logiciel** pour cette fenêtre, quelle que soit la carte graphique (limitation DirectX documentée : les bits matériels doivent être ramenés en logiciel pour composer l'alpha). Sur un cadran statique c'est indolore, mais dès qu'on ajoute animations continues, flous, ombres portées, dégradés animés ou une bande d'activité qui bouge à 60 fps, le CPU grimpe et le rendu tombe à quelques images/seconde.

**Why it happens:**
On teste sur une maquette statique, tout est fluide, puis on ajoute des effets « gratuits » en supposant l'accélération matérielle.

**How to avoid:**
- Garder le rendu **léger et majoritairement statique** : arcs `Path`/`ArcSegment` redessinés seulement quand la donnée change (tick 1 s), pas d'animation continue.
- Éviter `DropShadowEffect`, `BlurEffect`, `Opacity` animé sur de grandes surfaces.
- Le tick 1 s met à jour la géométrie des arcs et le texte, pas une timeline d'animation.
- Si un jour un effet fluide est requis : le confiner à une petite zone, ou envisager une fenêtre non transparente à coins arrondis via région Win32.

**Warning signs:**
- CPU non négligeable au repos alors que rien ne « bouge » visuellement.
- Ajout d'une `Storyboard`/animation → saccades.
- Tearing/latence sur le déplacement de la fenêtre.

**Phase to address:**
Phase fenêtre overlay (choix de rendu) et Phase cadran (contrainte : redessin sur changement, pas d'animation permanente).

---

### Pitfall 3 : `Topmost=True` n'est PAS un always-on-top fiable dans le temps

**What goes wrong:**
`Topmost=True` place la fenêtre dans la bande topmost, mais ce n'est pas exclusif : d'autres fenêtres topmost, une app plein écran exclusif (jeu, vidéo), le bureau sécurisé UAC, une session RDP, ou un simple réagencement du Z-order par Windows peuvent faire passer l'overlay derrière. Résultat classique : l'overlay « disparaît » après quelques heures ou après une bascule plein écran, sans erreur.

**Why it happens:**
On suppose que `Topmost=True` est un état permanent garanti. En réalité c'est une position dans un ordre que le système et les autres apps modifient.

**How to avoid:**
- **Réaffirmer périodiquement** le topmost : soit toggler `Topmost=false; Topmost=true`, soit appeler `SetWindowPos(hwnd, HWND_TOPMOST, ...)` avec `SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE` sur un timer léger (ex. quelques secondes) et/ou sur événement d'activation.
- Ne jamais voler le focus : `SWP_NOACTIVATE` / `ShowActivated=false` — un overlay ne doit jamais devenir la fenêtre active.
- Accepter et documenter que le plein écran exclusif d'un tiers gagnera toujours : c'est une limite Windows, pas un bug à corriger indéfiniment.
- Le bouton « arrière-plan » (bascule Topmost) doit être cohérent avec ce mécanisme de réaffirmation (ne pas réimposer topmost quand l'utilisateur a demandé l'arrière-plan).

**Warning signs:**
- L'overlay passe derrière après une session longue ou après une vidéo plein écran.
- La fenêtre vole le focus / apparaît dans Alt+Tab de façon indésirable.

**Phase to address:**
Phase fenêtre overlay (mécanisme de réaffirmation topmost + non-activation), en lien avec le bouton arrière-plan.

---

### Pitfall 4 : Accès UI depuis un thread non-Dispatcher (watcher, timers, providers async)

**What goes wrong:**
`FileSystemWatcher` lève ses événements sur un thread de pool, les providers lisent des fichiers en async, `PeriodicTimer` tourne hors UI. Toucher un objet WPF (propriété liée, `ObservableCollection`, géométrie du cadran) depuis ces threads provoque un `InvalidOperationException` (« The calling thread cannot access this object because a different thread owns it ») — souvent **intermittent**, donc invisible en dev et reproduit chez l'utilisateur.

**Why it happens:**
Les objets WPF ont une affinité de thread (DispatcherObject). Les callbacks d'I/O et de timers ne s'exécutent pas sur ce thread. En dev, le timing masque souvent le problème.

**How to avoid:**
- Frontière nette : les providers/watcher produisent des **DTO immuables** (`UsageSnapshot`) ; seul le ViewModel, marshalé via `Dispatcher`, met à jour l'état lié.
- Marshaler explicitement : `Dispatcher.Invoke`/`BeginInvoke`, ou capturer le `SynchronizationContext` UI, ou utiliser `IProgress<T>`.
- Pour la mise à jour visible à 1 s, préférer un `DispatcherTimer` (déjà sur le thread UI) — ne pas mélanger avec un timer de fond qui touche l'UI.
- Ne pas muter une `ObservableCollection` liée depuis un thread de fond (même avec BindingOperations, rester prudent) ; privilégier un remplacement d'instantané côté UI.

**Warning signs:**
- Crashs intermittents non reproductibles en dev, remontés « au bout d'un moment ».
- Exceptions mentionnant l'affinité de thread / « different thread owns it ».

**Phase to address:**
Phase pipeline de données / rafraîchissement (architecture threading), transverse à watcher, timers et providers.

---

### Pitfall 5 : Parser un JSONL en cours d'écriture (lignes partielles, verrous, encodage)

**What goes wrong:**
Les transcripts `*.jsonl` sont écrits en continu par Claude Code. Ouvrir un tel fichier en lecture exclusive échoue (« le fichier est utilisé par un autre processus »). Lire la dernière ligne donne souvent une **ligne partielle** (JSON tronqué) au moment du flush. `File.ReadAllLines` charge tout en mémoire (fichiers volumineux), et un `JsonSerializer` sur une ligne corrompue jette une exception qui tue le parsing de tout le fichier.

**Why it happens:**
On suppose des fichiers statiques et bien formés. En réalité ils sont ouverts en écriture, potentiellement gros, et la dernière ligne peut être incomplète.

**How to avoid:**
- Ouvrir en **`FileShare.ReadWrite`** (`FileStream` + `StreamReader`) pour ne pas se battre avec le processus écrivain ; ne jamais demander un verrou exclusif.
- **Parser ligne par ligne** et envelopper chaque `JsonSerializer.Deserialize` dans un try/catch : ligne invalide → ignorée, on continue (parsing tolérant exigé par le PROJECT).
- **Ignorer une dernière ligne non terminée** (pas de `\n` final) : ne la traiter qu'une fois complète.
- Ne pas charger tout le fichier : lire en streaming ; si seule une fenêtre temporelle importe, lire depuis la fin ou suivre un **offset** entre passes.
- Forcer l'encodage **UTF-8** (les JSONL Claude sont UTF-8) ; gérer un éventuel BOM.
- Ne pas supposer une seule structure de ligne : filtrer sur le type d'entrée attendu, ignorer le reste.

**Warning signs:**
- `IOException` « fichier utilisé par un autre processus » au démarrage.
- Pics mémoire/latence à l'ouverture sur de longs transcripts.
- Estimation qui « saute » à cause d'une ligne partielle comptée puis recomptée.

**Phase to address:**
Phase provider de repli (parsing JSONL tolérant + streaming + FileShare).

---

### Pitfall 6 : `FileSystemWatcher` peu fiable (buffer overflow, événements manqués/dupliqués, fichiers verrouillés)

**What goes wrong:**
`FileSystemWatcher` est réputé « source de vérité » alors qu'il perd des événements. Le buffer interne par défaut (~8 Ko) déborde en cas de rafale d'écritures (transcripts très actifs) → événement `Error` et **changements silencieusement perdus**. Il émet aussi souvent **plusieurs événements pour une seule sauvegarde** (Changed en double). Et l'événement se déclenche **avant** que l'écrivain ait fini/débloqué le fichier → lecture immédiate qui échoue.

**Why it happens:**
On traite le watcher comme un flux d'événements exact et complet. C'est en réalité un signal « quelque chose a peut-être changé », best-effort.

**How to avoid:**
- Traiter le watcher comme un **déclencheur de re-lecture**, jamais comme la donnée elle-même. À chaque événement : marquer « à relire » et recalculer depuis le fichier.
- **Débouncer/coalescer** : regrouper les événements sur une courte fenêtre (ex. 200–500 ms) avant de relire, pour absorber les doublons et rafales.
- **Filet de sécurité `PeriodicTimer`** (déjà prévu) : re-scan périodique indépendant du watcher, pour rattraper tout événement perdu — c'est la vraie garantie de fraîcheur.
- Gérer l'événement **`Error`** (overflow) : au lieu de crasher, forcer un re-scan complet ; envisager d'augmenter `InternalBufferSize` avec parcimonie.
- Régler `NotifyFilter` au minimum utile (LastWrite/FileName) et un `Filter` `*.jsonl` pour réduire le bruit.
- Watch récursif sur `**` : attention à la charge ; surveiller le dossier racine `.claude/projects` avec `IncludeSubdirectories=true` mais filtré.
- Relecture **résiliente aux verrous** : retry court si le fichier est momentanément verrouillé.

**Warning signs:**
- Chiffres qui ne se mettent pas à jour lors de sessions très actives (overflow silencieux).
- Multiples recalculs pour une seule écriture (pas de debounce).
- Exceptions de lecture juste après un événement (fichier pas encore libéré).

**Phase to address:**
Phase rafraîchissement (watcher débouncé + PeriodicTimer de secours + gestion Error).

---

### Pitfall 7 : Présenter une estimation ou un compte à rebours dérivant comme un chiffre exact

**What goes wrong:**
Deux sources d'inexactitude : (a) le **repli JSONL** est une estimation par sommation de tokens contre des plafonds **non publiés et mouvants** (×2 le 6 mai, +50 % hebdo jusqu'au 13 juillet 2026) — donc structurellement approximatif ; (b) le **reset hebdomadaire dérive** (~72 h autour d'un horaire d'ancrage non documenté), donc un compte à rebours « exact » vers le reset hebdo sera faux. Afficher ces valeurs comme des chiffres précis trahit la confiance utilisateur — or c'est la Core Value du projet (« ne jamais présenter une estimation comme exacte »).

**Why it happens:**
Un chiffre net à l'écran paraît plus « pro » qu'un intervalle flou. Tentation de lisser l'incertitude.

**How to avoid:**
- **Distinguer visuellement** source primaire (utilization/resets_at fiable) et repli (estimé) : marquage explicite « estimé » dans l'UI quand le composite bascule sur le repli.
- Le provider composite expose la **provenance** dans le `UsageSnapshot` (primaire vs estimé) ; l'UI la reflète (ex. libellé, teinte, icône).
- **Reset hebdo = best-effort** : afficher `resets_at` tel que fourni par la source quand elle existe ; sinon compte à rebours approximatif clairement signalé, et **recalibrage utilisateur** facile (bouton/réglage qui réancre le cycle).
- Prioriser toujours `utilization`/`resets_at` sur le comptage de tokens (décision clé du PROJECT).
- Ne pas afficher de faux précision (ex. pas de « 47,3 % » pour une estimation ; arrondir/qualifier).

**Warning signs:**
- L'UI affiche le même style pour données fiables et estimées.
- Aucun moyen pour l'utilisateur de recalibrer le cycle hebdo.
- Le compte à rebours hebdo diverge de la réalité sans que l'UI ne le signale.

**Phase to address:**
Phase pipeline (provenance dans le snapshot, composite), Phase UI/cadran (marquage estimé), Phase réglages (recalibrage hebdo).

---

### Pitfall 8 : Packaging mono-fichier WPF, autostart et faux positifs antivirus

**What goes wrong:**
Trois surprises au déploiement : (a) `PublishSingleFile` sur WPF n'inclut PAS les natives par défaut → il faut `IncludeNativeLibrariesForSelfExtract=true`, et même ainsi certaines natives du runtime peuvent être extraites sur disque (pas un vrai fichier unique sans `IncludeAllContentForSelfExtract`). (b) Un exe self-contained **non signé** déclenche régulièrement Windows Defender / SmartScreen (surtout si `PublishTrimmed=true`) → l'utilisateur voit un avertissement ou l'exe est mis en quarantaine. (c) L'autostart via `shell:startup` référence un chemin d'exe : si l'exe est déplacé/renommé, le raccourci casse silencieusement.

**Why it happens:**
On suppose que « single file » = un vrai fichier autonome et de confiance. En pratique : natives extraites, réputation nulle, chemin fragile.

**How to avoid:**
- Publier avec `-r win-x64 --self-contained /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true`. Tester le résultat sur une **machine propre**, pas seulement la machine de dev.
- **Éviter `PublishTrimmed=true`** avec WPF (trim fragile en WPF + déclencheur Defender) sauf besoin fort et test approfondi.
- Vérifier le comportement du dossier d'extraction (`DOTNET_BUNDLE_EXTRACT_BASE_DIR`) et les droits en écriture sous profil utilisateur (contrainte : pas d'admin).
- Autostart : créer le raccourci `.lnk` dans `shell:startup` pointant vers le chemin **réel et stable** de l'exe ; vérifier/réparer le raccourci au lancement ; ne pas écrire en HKLM (admin).
- Idéalement signer l'exe (au moins accepter que SmartScreen demande confirmation au premier lancement) et documenter la procédure pour l'utilisateur.

**Warning signs:**
- L'exe fonctionne en dev mais est bloqué/quarantaine sur une autre machine.
- Plusieurs fichiers à côté de l'exe alors qu'on attendait un seul.
- L'app ne démarre plus au boot après déplacement de l'exe.

**Phase to address:**
Phase packaging/déploiement (publish, test machine propre, autostart robuste).

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Coder le parsing de la source primaire en dur dans le ViewModel | Prototype visible plus vite | Rupture totale à la moindre MAJ Claude ; réécriture UI+données | **Jamais** — c'est précisément ce que `IUsageProvider` prévient |
| Sauter `docs/data-sources.md` et coder « à l'observation » | Gain de quelques heures | Aucune trace du contrat → impossible de diagnostiquer une rupture | Jamais pour la source primaire |
| Se fier au seul `FileSystemWatcher` sans PeriodicTimer | Moins de code | Chiffres périmés silencieux en cas d'overflow | Jamais — le timer de secours est la garantie |
| `File.ReadAllLines` sur les JSONL | Simplicité | Pics mémoire, crash sur ligne partielle, verrou | MVP seulement si transcripts petits ET FileShare.ReadWrite + try/catch par ligne |
| Afficher l'estimation sans marquage « estimé » | UI plus nette | Trahit la Core Value ; utilisateur bloqué par surprise | Jamais |
| `Topmost=True` sans réaffirmation | Une ligne de XAML | Overlay disparaît après plein écran/session longue | Acceptable en tout premier jet, à corriger avant packaging |
| `PublishTrimmed=true` pour réduire la taille | Exe plus petit | Trim casse WPF (réflexion/XAML), déclenche Defender | Éviter en WPF |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Objet d'usage Claude (source primaire) | Supposer chemin/format stables et câbler en dur | Découverte documentée + `IUsageProvider` + parsing défensif, champ absent → indisponible |
| Transcripts JSONL en écriture | Ouvrir en lecture exclusive, `ReadAllLines`, parser tout ou rien | `FileShare.ReadWrite`, streaming, try/catch par ligne, ignorer dernière ligne partielle |
| `FileSystemWatcher` | Le traiter comme flux exact et complet | Déclencheur de re-lecture débouncé + PeriodicTimer de secours + gestion `Error` |
| Dispatcher WPF | Muter l'UI depuis watcher/timer/async | Marshaler via Dispatcher ; DTO immuables à la frontière |
| `resets_at` hebdo | Compte à rebours « exact » | Afficher tel quel si fourni, sinon best-effort signalé + recalibrage |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Rendu logiciel forcé par `AllowsTransparency` + effets animés | CPU élevé, saccades du cadran | Rendu statique, redessin sur changement, pas de blur/shadow/anim continue | Dès qu'on anime de grandes surfaces |
| Relecture intégrale des JSONL à chaque événement | Latence/CPU croissant avec la taille des transcripts | Lecture incrémentale par offset, fenêtre temporelle, debounce | Transcripts de plusieurs Mo / sessions longues |
| Rafale d'événements watcher recalculant à chaque fois | Recalculs redondants, UI qui tremble | Coalescence/debounce des événements | Sessions Claude très actives |
| Watch récursif large sur `.claude/projects/**` | Charge CPU du watcher, bruit | `NotifyFilter` minimal + `Filter=*.jsonl` + PeriodicTimer | Nombreux projets/fichiers |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Lire/logguer le contenu des transcripts (prompts, code) au-delà du comptage | Fuite de données sensibles utilisateur dans des logs | Ne compter que ce qui est nécessaire (tokens/métadonnées) ; ne jamais persister le contenu |
| Écrire hors du profil utilisateur | Nécessite admin, viole la contrainte | Uniquement `%APPDATA%/Chronos` et lecture sous `%USERPROFILE%/.claude` |
| Exe non signé distribué largement | Quarantaine antivirus, méfiance utilisateur | Signature si possible, doc SmartScreen, éviter PublishTrimmed |
| Suivre des chemins/symlinks sans validation | Lecture hors périmètre attendu | Contraindre au dossier `.claude/projects`, ignorer le reste |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Estimation affichée comme un chiffre exact | L'utilisateur se fait bloquer alors qu'il « avait de la marge » | Marquage « estimé », teinte/état distincts, arrondi qualifié |
| Overlay qui vole le focus / apparaît dans Alt+Tab | Interrompt le travail, agace | `ShowActivated=false`, `SWP_NOACTIVATE`, pas de fenêtre activable |
| Fenêtre non déplaçable ou qui s'accroche mal en multi-écrans | Overlay coincé hors zone visible | Drag + snap au coin le plus proche, clamp sur l'écran courant, persistance position |
| Aucun état « données indisponibles » | Cadran vide ou chiffres figés faux, l'utilisateur croit l'app cassée | État explicite « données indisponibles » sans crash |
| Compte à rebours hebdo faux sans recalibrage | Perte de confiance dans tout l'outil | Best-effort signalé + recalibrage utilisateur facile |
| Zone transparente qui capte les clics (bloque le bureau) | Impossible de cliquer « à travers » le vide du cadran | Hit-testing maîtrisé : `Background=Transparent` capte, `null` laisse passer ; définir la zone interactive |

## "Looks Done But Isn't" Checklist

- [ ] **Provider primaire :** fonctionne sur la machine de dev — vérifier qu'un champ manquant/renommé dégrade vers « indisponible » sans crash, pas seulement le cas nominal.
- [ ] **Topmost :** paraît always-on-top au lancement — vérifier après une vidéo plein écran et après plusieurs heures (réaffirmation active).
- [ ] **Parsing JSONL :** marche sur un fichier statique — vérifier sur un fichier en cours d'écriture (FileShare) et avec une dernière ligne tronquée.
- [ ] **FileSystemWatcher :** se déclenche en test — vérifier le comportement en rafale (overflow `Error`) et que le PeriodicTimer rattrape.
- [ ] **Threading :** aucun crash en dev — vérifier que watcher/timer/async ne touchent l'UI que via Dispatcher (tester sous charge).
- [ ] **Estimation :** chiffre affiché — vérifier le marquage « estimé » quand le composite bascule sur le repli.
- [ ] **Single-file :** publie sur la machine de dev — tester le lancement sur une machine propre sans .NET installé + réaction Defender.
- [ ] **Autostart :** démarre au boot — vérifier après déplacement/renommage de l'exe.
- [ ] **Multi-écrans/DPI :** correct sur l'écran principal — vérifier snap et netteté sur un second écran à DPI différent.
- [ ] **Aucune source :** cas nominal OK — vérifier l'état « données indisponibles » quand ni primaire ni JSONL ne sont lisibles.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Rupture de la source primaire à une MAJ Claude | LOW **si** `IUsageProvider` en place | Recapturer un échantillon, mettre à jour `docs/data-sources.md`, adapter/remplacer le seul provider primaire ; composite bascule déjà sur le repli entre-temps |
| Rupture de source SANS abstraction | HIGH | Refactor complet chaîne données→UI ; d'où l'obligation de l'abstraction dès le départ |
| Crash de threading intermittent | MEDIUM | Auditer les callbacks watcher/timer/async, insérer les marshaling Dispatcher manquants |
| Overlay perdu derrière (topmost) | LOW | Ajouter la réaffirmation périodique via SetWindowPos/toggle |
| Estimation présentée comme exacte (déjà livrée) | MEDIUM | Ajouter provenance au snapshot + marquage UI ; recalibrage hebdo |
| Exe en quarantaine antivirus | MEDIUM | Désactiver trimming, signer, documenter SmartScreen, éventuellement soumettre à l'éditeur AV |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Source non documentée fragile | Phase 1 Découverte (bloquante) + Phase pipeline (`IUsageProvider`) | `docs/data-sources.md` existe avant tout provider ; test de contrat sur échantillon ; champ manquant → indisponible |
| Rendu logiciel / effets | Phase fenêtre overlay + Phase cadran | CPU au repos négligeable ; pas d'animation continue ; fluide au déplacement |
| Topmost non fiable | Phase fenêtre overlay | Reste au premier plan après plein écran et session longue |
| Threading Dispatcher | Phase pipeline/rafraîchissement (transverse) | Aucun accès UI hors Dispatcher ; stable sous charge |
| JSONL en écriture | Phase provider de repli | Lecture d'un fichier ouvert en écriture + ligne partielle tolérée |
| FileSystemWatcher fiable | Phase rafraîchissement | Debounce + PeriodicTimer + gestion `Error` vérifiés en rafale |
| Honnêteté des chiffres | Phase pipeline (provenance) + UI (marquage) + réglages (recalibrage) | Estimé visuellement distinct ; recalibrage hebdo fonctionnel |
| Packaging / autostart / AV | Phase packaging | Lancement sur machine propre ; autostart robuste au déplacement |

## Sources

- Microsoft Learn — *Graphics Rendering Tiers (WPF)* et *Optimizing Performance: Taking Advantage of Hardware* : `AllowsTransparency` force le rendu logiciel (HIGH).
- Microsoft Learn / troubleshoot — *WPF Render Thread Failures* : `AllowsTransparency` comme source de défaillances DirectX (HIGH).
- Microsoft Learn — *Create a single file for application deployment* + dotnet/sdk issue #24181 : `IncludeNativeLibrariesForSelfExtract` requis, natives extraites (HIGH).
- dotnet/runtime issue #33745 : `PublishTrimmed=true` déclenche un faux positif Windows Defender (HIGH).
- Connaissance établie du comportement `FileSystemWatcher` (buffer overflow, événements dupliqués/manqués, `InternalBufferSize`, `NotifyFilter`) — documentation .NET (HIGH).
- Connaissance établie de l'affinité de thread WPF (`DispatcherObject`) et du marshaling Dispatcher (HIGH).
- Comportement `Topmost` non exclusif et réaffirmation via `SetWindowPos(HWND_TOPMOST)` — pratique Win32 établie (MEDIUM, plusieurs discussions communautaires convergentes).
- `.planning/PROJECT.md` (Chronos) — contraintes, sources non documentées, dérive du reset hebdo, plafonds mouvants.

---
*Pitfalls research for: overlay WPF transparent lisant des sources Claude Code non documentées*
*Researched: 2026-07-08*
