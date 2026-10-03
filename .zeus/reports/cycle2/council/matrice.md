# Matrice de cross-challenge (§6.2) — Chronos, cycle ZEUS 2 (2026-10-03)

Mécanisme : 4 paires fixes, chacune challengée **sur son intersection seulement** (pas all-vs-all). Lecture seule,
l'exe n'a jamais été lancé. Entrées : `architecte.md`, `securite.md`, `tests.md`, `fiabilite.md`, `donnees.md` + relecture
du code à HEAD `244263f`. Chaque verdict ci-dessous cite le code relu. Les findings nouveaux portent l'ID `MAT-n` au format §7.

Vérité-terrain propre à la matrice (lecture seule) :
- `~/.claude/settings.json` : **seuls** les chemins d'exe des commandes Chronos ont été extraits (regex `[A-Za-z]:/…Chronos….exe`),
  rien d'autre n'a été lu ni affiché → `C:/Users/Tanguy/Documents/PROGRAMMES/DEV/PROJET OVERLAY/Chronos-v3.4.0.exe` (×9 : 8 hooks + barre).
- `shell:startup` : vide (aucun `Chronos.lnk`) sur ce poste.
- Racine du dépôt : 7 exe `Chronos-v3.1.0.exe` … `Chronos-v3.4.0.exe` (ignorés par `.gitignore:/Chronos-v*.exe`).

---

## Paire 1 — Architecte ↔ Sécurité

Question : un choix d'archi a-t-il des implications sécu (secrets OAuth, écriture de `~/.claude/settings.json`, hooks qui lancent
l'exe, emplacement de l'exe à la racine du dépôt) ?

### Confirmés
- **SEC-4 (coffre DPAPI) — confirmé, et l'archi le renforce.** Le type `OAuthTokens` n'apparaît que dans 3 fichiers
  (`ChronosOAuthClient.cs`, `ChronosOAuthStore.cs`, `ChronosTokenAuthority.cs`, grep). Le VM-dieu d'ARCH-1 ne voit que
  `IOAuthLogin.IsLoggedIn` (`MainViewModel.cs:37,470`) ; `DiagnosticService` ne reçoit que `IAuthStatus` (`DiagnosticService.cs:84`),
  interface qui n'expose qu'un `Etat` et un événement (`IAuthStatus.cs:12-19`). **ARCH-1 n'a donc aucune implication secrets.**
- **SEC-3 (ToString des records) — confirmé, rayon atténué par l'archi.** Le piège ne peut se déclencher que dans les 3 fichiers
  ci-dessus ; le seul écrivain de journal (`DiagnosticService`) n'a pas accès au type. Suggestion au Sceptique : Info plutôt que Mineur.
- **SEC-9 / SEC-7 — confirmés.** Le mode `--hook` ne sort jamais en 2 et n'écrit rien sur stdout (`App.xaml.cs:162-165, 166-193`) :
  Chronos lui-même n'utilise pas le pouvoir bloquant d'un hook.
- **ARCH-3 — confirmé côté prod, sans implication sécu prouvée.** `SessionHookInstaller(string? settingsPath = null)` et
  `ClaudeSettingsReconciler(string? settingsPath = null, …)` ciblent par défaut le VRAI `~/.claude/settings.json`
  (`SessionHookInstaller.cs:129-131`, `ClaudeSettingsReconciler.cs:82-87`). Tous les tests injectent un chemin temp
  (`CompositionRootTests.cs:94-97, 274-275`) ; grep `new SessionHookInstaller()|new ClaudeSettingsReconciler()` dans `tests/` : 0.

### Nouveaux

```
ID            : MAT-1
Sévérité      : Mineur
Rôle émetteur : Matrice Architecte ↔ Sécurité
Localisation  : src/Chronos/Services/SessionHookInstaller.cs:158-172 ; src/Chronos/Services/ClaudeSettingsReconciler.cs:213-255, 305-311 ;
                src/Chronos/Views/SessionsController.cs:45, 62 ; src/Chronos/App.xaml.cs:314, 319
Preuve        : CITATION SessionHookInstaller.cs:158-163 : `var current = File.Exists(_settingsPath) ? File.ReadAllText(_settingsPath) : null;
                var updated = TransformForInstall(current, exePath); if (updated is null) return; WriteAtomic(updated);`
                CITATION ClaudeSettingsReconciler.cs:217 : `if (!File.Exists(_settingsPath)) // on ne CRÉE jamais le fichier`
                puis `var sauvegarde = Sauvegarder(); if (sauvegarde is null) … return false;` (l.238-243).
Constat       : Deux classes indépendantes font chacune leur lire-modifier-écrire sur la configuration EXÉCUTABLE de Claude Code
                (hooks, statusLine), avec deux doctrines contradictoires : le réconciliateur ne crée jamais le fichier, sauvegarde
                avant d'écrire, ne réécrit pas un fichier déjà conforme ; l'installateur (appelé à chaque bascule du widget)
                crée le fichier s'il est absent, n'a aucune sauvegarde et réécrit sans test de conformité. Chacune a sa propre
                WriteAtomic. Les défauts DATA-4 / FIAB-8 (fichier vide pris pour neuf, pas de contrôle de concurrence, lien
                symbolique remplacé) devront être corrigés DEUX fois.
Impact        : la moitié non sauvegardée du chemin (installateur) est celle qui tourne sur un geste utilisateur ; une correction
                appliquée au seul réconciliateur laisse le trou ouvert. Pour un mainteneur, rien n'indique qu'il existe deux
                écrivains du même fichier tiers.
Recommandation: une passerelle unique (ex. `PasserelleReglagesClaude`) : lecture tri-état (absent / lu / inexploitable),
                sauvegarde du texte lu, contrôle mtime+taille avant Move, détection ReparsePoint, écriture atomique ; installateur
                et réconciliateur n'en gardent que leur cœur pur (`TransformForInstall`, `ReconcileJson`).
Applicabilité : desktop — écriture dans la configuration d'un autre outil local, exécutée par lui.
Statut challenge : non contesté
```

```
ID            : MAT-2
Sévérité      : Mineur
Rôle émetteur : Matrice Architecte ↔ Sécurité
Localisation  : src/Chronos/Services/SessionHookInstaller.cs:68-77, 136-137 ; src/Chronos/App.xaml.cs:130-134 ; docs/publish.md:43-44, 88-98 ;
                src/Chronos/Services/AutostartService.cs
Preuve        : OUTIL (extraction des seuls chemins d'exe de ~/.claude/settings.json) : 9 commandes ciblent
                `C:/Users/Tanguy/Documents/PROGRAMMES/DEV/PROJET OVERLAY/Chronos-v3.4.0.exe` — un fichier de l'ARBRE DE TRAVAIL du dépôt.
                CITATION docs/publish.md:43 : « Copier ensuite la sortie à la racine du dépôt sous le nom versionné Chronos-v<X.Y.Z>.exe ».
                CITATION SessionHookInstaller.cs:73-77 : PermissionRequest, PreToolUse, PostToolUse câblés (exe lancé à CHAQUE appel d'outil).
                CITATION App.xaml.cs:162-163 : « 2 = erreur BLOQUANTE (stderr renvoyé à Claude, action bloquée) ».
                CITATION publish.md:94-98 : le .lnk d'autostart n'est pas repointé quand l'exe change.
Constat       : La cible persistante des hooks est « le dernier exe overlay lancé » (Environment.ProcessPath, repointé à chaque
                démarrage), et la convention de publication place cet exe sous un nom versionné dans le dossier du dépôt — un
                dossier où écrivent des outils et des agents (dont Claude Code lui-même en mode bypass). Trois conséquences
                architecturales : (1) supprimer l'exe (ménage des anciennes versions, `git clean -xfd`) fait échouer un hook à
                chaque appel d'outil de TOUTES les sessions Claude Code tant qu'un overlay n'a pas été relancé ; (2) remplacer ce
                fichier donne une exécution à chaque appel d'outil, en position PreToolUse/PermissionRequest où le contrat Claude
                Code permet de bloquer une action (exit 2) — c'est le rayon d'action de SEC-1, élargi à toute écriture locale dans
                le dépôt ; (3) lancer une ancienne version repointe tous les hooks vers elle (et l'autostart, lui, n'est jamais
                repointé), ce qui rend DATA-10 (version ancienne qui efface les données récentes) atteignable par un geste courant.
Impact        : disponibilité des sessions Claude Code dépendante d'un fichier jetable ; surface de substitution plus large que
                le seul téléchargement GitHub. Non observé : pas d'autostart sur ce poste (shell:startup vide).
Recommandation: emplacement d'installation stable hors dépôt et à nom fixe (ex. %LOCALAPPDATA%\Programs\Chronos\Chronos.exe),
                version portée par les métadonnées de l'exe ; refuser (avec bilan au log) de repointer les hooks vers un exe de
                version inférieure à celle déjà inscrite ; repointer l'autostart comme les hooks.
Applicabilité : desktop, même compte : aucune élévation ; c'est l'intégrité et la disponibilité d'un exécutable inscrit dans la
                configuration d'un autre outil.
Statut challenge : non contesté
```

### Contredits
- Aucun. Nuance sur ARCH-1 : sa mention « OAuth » ne recouvre qu'un déclencheur de login, pas une détention de secret (voir ci-dessus).

---

## Paire 2 — Architecte ↔ Tests

Question : le découpage rend-il testable ce qui compte ? Un finding de l'un invalide-t-il ou aggrave-t-il un finding de l'autre ?

### Confirmés
- **ARCH-2 ≡ TEST-2 (même défaut, deux angles) — confirmé et AGGRAVÉ.** La racine de composition est privée
  (`App.xaml.cs:238`) et les tests la recopient. Relecture : les miroirs ne sont même pas UNE copie complète, ils sont
  **fragmentés par test**. Hosted services réels, dans l'ordre : `TokenRefreshService` (l.371), `JournalisationUsageProvider`
  (l.426), `ReconstructionTokens` (l.438), `RefreshOrchestrator` (l.468). Miroirs : l.324 (Token seul), l.484/495 (Journal →
  Orchestrateur, sans Reconstruction), l.601/605 (Reconstruction → Orchestrateur, sans Journal). **Aucun conteneur de test
  n'inscrit les quatre dans l'ordre réel.** S'ajoute FIAB-1 : une erreur de résolution dans `OnStartup` (`async void`, aucun filet)
  tue le processus sans trace. **Arbitrage proposé : Majeur (position ARCH-2) plutôt que Mineur (TEST-2)** — « un dev qui débarque
  ajoute un service dans App, oublie le miroir, la suite reste verte, l'exe meurt au lancement sans log ».
- **TEST-2 (garde DAT-03) — confirmé.** `LireCommandeInterneHeritee` est l.106, `_host.StartAsync()` l.114, `window.Show()` l.123 ;
  la garde textuelle ne vérifie que « avant Show ». Aucun `Save` n'est aujourd'hui atteignable depuis un `StartAsync` (grep des 12
  sites de `Save`, aucun dans un service hébergé) : risque latent, pas défaut actif.
- **TEST-6 / TEST-7 ↔ ARCH-10 — confirmés, même cause.** Le couplage temporel Host/Dispatcher (ARCH-10) est précisément ce que
  les gardes textuelles tentent d'épingler faute de pouvoir exécuter `OnStartup`/`OnExit`. L'extraction recommandée par ARCH-2
  (composition) et TEST-6 (séquence d'arrêt pure) est la même opération.
- **ARCH-3 × tests — confirmé sans incident.** Les défauts « ressource réelle » ne sont pas un danger de test aujourd'hui :
  `new ArchiveStore()` sans argument : 0 dans `tests/` ; seule exception en lecture, `CompositionRootTests.cs:209`
  (`new SessionMonitor(null, null, …)` → racines réelles, lecture seule). TEST-8 relève de `ChronosPaths.Default()`, pas d'ARCH-3.

### Aggravation croisée (sans nouvel ID)
- **ARCH-1 est le correctif structurel du groupe Fiabilité/Données.** Les séquences `Save(Load() with …)` sont **12**, réparties
  sur 4 classes : `MainViewModel.cs:323,342,357,384,418,821,834` (7), `OverlayController.cs:104,219,227` (3),
  `SessionsController.cs:122` (1), `ReglagesHistorique.cs:30` (1). Corriger FIAB-2 / FIAB-3 / DATA-5 / DATA-11 site par site =
  12 modifications et 12 tests ; derrière la façade `IReglagesChronos.Modifier(mutation)` recommandée par ARCH-1 = 1 point de
  verrou, de reprise et de refus d'écrire. Le motif existe déjà : `ReglagesHistoriqueSurDisque` (`ReglagesHistorique.cs:30`).

### Contredits
- **ARCH-1, champ Impact « testable seulement avec les vrais services » — nuancé.** Le dossier ViewModels est couvert à 95,8 %
  (tests.md) : le VM EST testé, avec de vrais services sur chemins temp. La testabilité n'est pas bloquée ; le coût réel d'ARCH-1
  est la dispersion des 12 sites d'écriture (ci-dessus) et la largeur du constructeur. La sévérité Majeur tient sur ce motif-là.

---

## Paire 3 — Fiabilité ↔ Données

Question : transactions / intégrité sous erreur. Scénarios relus dans le code.

### Doublons à fusionner (même défaut, deux rôles)
| Défaut | IDs | Verdict |
|---|---|---|
| `SettingsService.Save` lève, non protégé, sur le thread UI | FIAB-2 ≡ DATA-11 | garder FIAB-2 (Majeur) |
| Lecture ratée ⇒ défauts ⇒ réécriture de `settings.json` | FIAB-3 ⊃ DATA-5 | garder les deux angles sous MAT-3 |
| `archived.json` / `treated.json` vidés par une lecture ratée | FIAB-3 ≡ DATA-7 ≡ TEST-3 | un seul finding |
| `~/.claude/settings.json` vide pris pour neuf, lire-modifier-écrire sans contrôle | DATA-4 ≡ FIAB-8 (b)(a) | un seul finding (+ MAT-1) |
| Lecture inaccessible lue comme vide (Historique) | DATA-13 ≡ TEST-4 (partie LecteurJournal) | un seul finding |

### Confirmés (code relu)
- **FIAB-2 — confirmé.** `Save` : `File.WriteAllText(tmp…); File.Move(tmp, …, overwrite: true)` sans try (`SettingsService.cs:155-166`) ;
  `_window.LocationChanged += (_, _) => PersistPosition();` → `_settings.Save(mutate(_settings.Load()))` (`SessionsController.cs:109, 121-122`).
  Tous les `Save` sont sur le thread UI, donc le temp `.tmp-{ProcessId}` partagé n'entre pas en collision dans le processus.
  Le lecteur concurrent du processus (`RateLimitHeaderUsageProvider.cs:205`, `File.ReadAllText` sans `FileShare.Delete`) n'offre
  qu'une fenêtre de l'ordre de la ms par passe ; l'ouvreur réaliste est externe (antivirus, indexeur) — **probabilité NON VÉRIFIÉE**.
  Majeur maintenu à cause de FIAB-1 (toute collision = crash sans trace).
- **FIAB-3 / DATA-5 — confirmés.** `Load` rend `new ChronosSettings()` sur `IOException`/`UnauthorizedAccessException` comme sur une
  erreur de syntaxe (`SettingsService.cs:124-130`). `RestorePlacement` finit TOUJOURS par `SendToBackground()`/`BringToForeground()`
  (`OverlayController.cs:179-180`) qui font `Save(Load() with { Background = … })` (l.219, 227) : écriture garantie à chaque démarrage.
- **DATA-1 — mécanisme confirmé, sévérité contestée.** `Curseurs.Charger` vide sur JSON invalide / version / `IOException` /
  `UnauthorizedAccessException` (`Curseurs.cs:101-103`) ; `IndexMessages.Charger` ne charge que `MoisOuverts()` = 45 j
  (`IndexMessages.cs:104-130`) ; `Ajouter` traite un id inconnu comme nouveau (l.155-158) ; `MagasinAgregats.Appliquer` charge le mois
  gelé depuis son fichier puis additionne (`MagasinAgregats.cs:162-168`). Le commentaire `Curseurs.cs:73` (« l'index d'ids rend la
  relecture idempotente ») est faux au-delà de 45 j. **Mais** les déclencheurs ne sont pas de l'usage normal : `curseurs.json` a un
  seul écrivain (thread de reconstruction) et s'écrit par temp+Move. Seul déclencheur « normal » : la limite documentée de la copie
  fork d'un message de plus de 45 j (`docs/data-sources.md:351`, « jamais observé »). **Proposition : Majeur** (critère §4 Bloquant
  = « usage normal »), en arbitrage council.
- **DATA-2 — confirmé.** `Flush()` enchaîne index → agrégats → curseurs « sans interrompre les étapes suivantes »
  (`ReconstructionTokens.cs:329-347`). Nuance juste : les lignes d'index restent en attente et sont retentées (`IndexMessages.cs:172-196`),
  la perte exige un arrêt avant tout flush réussi. **Extension** : le même schéma vaut pour un delta tombé dans un mois GELÉ — si
  `EcrireMoisSales` échoue et que les curseurs sont écrits, le mois gelé n'est jamais reprojeté (`ReconstructionTokens.cs:277-282` ne
  reprojette que les mois ouverts) : perte définitive aussi.
- **DATA-3 — confirmé.** `ChargerMoisSousVerrou` pose `_parMois[mois] = etat` même après exception (`MagasinAgregats.cs:297-305`) ;
  `ChargerShard` avale l'E/S sans compteur (`IndexMessages.cs:286-287`). Le commentaire l.299 « le mois reste chargeable plus tard »
  contredit le code (le mois est marqué connu et ne sera plus relu).
- **DATA-4 / FIAB-8 — confirmés.** `ParseOrNull` : `if (string.IsNullOrWhiteSpace(json)) return new JsonObject();`
  (`ClaudeSettingsJson.cs:136`) alors que `Reconcile` a déjà vérifié `File.Exists` (`ClaudeSettingsReconciler.cs:217`). La sauvegarde
  `File.Copy` (l.270) copie le disque au moment de la copie, pas le texte `actuel` (l.225) sur lequel la fusion a été calculée.
- **FIAB-5 — confirmé, sans corruption.** Deux `LastExactStore.Save` concurrents sur `.tmp-{ProcessId}` (`LastExactStore.cs:128-130`) :
  le second `WriteAllText`/`Move` échoue (partage), l'exception est relancée puis rattrapée par la tête (`LastExactUsageProvider.cs:74-79`) →
  faux `ecriture_ratee`, jamais de fichier partiel.

### Cause racine commune

```
ID            : MAT-3
Sévérité      : Majeur
Rôle émetteur : Matrice Fiabilité ↔ Données
Localisation  : SettingsService.cs:124-130 & 155-166 ; ArchiveStore.cs:62-86 ; TreatedStore.cs:96-130 ; Curseurs.cs:101-103 ;
                MagasinAgregats.cs:274-306 ; IndexMessages.cs:286-287 ; ClaudeSettingsJson.cs:136 ; LecteurJournal.cs:41-57
Preuve        : CITATION SettingsService.cs:127-129 : `// Illisible (syntaxe, clés strictement dupliquées, E/S) → défauts ENTIERS` /
                `return new ChronosSettings();` — contre SettingsService.cs:164-165 : `File.WriteAllText(tmp, json); File.Move(…)` sans try.
                CITATION ArchiveStore.cs:73-74 : `catch { }  map[sessionId] = now;` puis écriture de la map entière.
                CITATION MagasinAgregats.cs:275 : « même vide, même après une erreur d'E/S (consignée) : le mois est alors « connu » ».
                CITATION Curseurs.cs:101-103 : `catch (IOException) { c._parCle.Clear(); }`.
Constat       : Une seule décision de conception, répétée dans 8 magasins, produit FIAB-3, DATA-1, DATA-3, DATA-4, DATA-5, DATA-7,
                DATA-13, TEST-3 et TEST-4 : la LECTURE n'a que deux issues (« valeur » ou « vide/défaut ») et absorbe toute erreur
                — y compris une E/S transitoire — en un état vide marqué VALIDE ; l'ÉCRITURE suivante, qui réécrit le fichier entier,
                ne peut donc pas savoir qu'elle part d'une ignorance. L'asymétrie est exactement inversée par rapport à ce que
                l'intégrité exige : `SettingsService.Load` ne lève jamais (il faudrait qu'il bloque l'écriture), `Save` lève toujours
                (il faudrait qu'il soit contenu, FIAB-2). Le contre-modèle existe dans le dépôt : `ClaudeSettingsJson.ParseOrNull`
                rend `null` = « ne rien écrire » sur un contenu inexploitable, et le réconciliateur s'y conforme — mais seulement
                pour la syntaxe, pas pour le fichier vide.
Impact        : toute erreur de lecture passagère (antivirus, verrou, disque) se convertit en perte définitive : réglages remis
                aux défauts, archives vidées, agrégats de mois gelés écrasés ou doublés, configuration Claude Code réécrite —
                contraire direct à la doctrine « exact ou rien », puisque « je n'ai pas pu lire » est présenté comme « il n'y a rien ».
Recommandation: un résultat de lecture tri-état partagé (`Absent | Lu(T) | Inaccessible(cause)`, plus `Illisible` pour la syntaxe)
                et une règle unique : aucune réécriture complète d'un fichier dont la dernière lecture n'est ni `Lu` ni `Absent` ;
                sur `Illisible`, copie `.bak` avant toute écriture. Pour settings.json, la porter dans la façade d'ARCH-1
                (`IReglagesChronos.Modifier`) qui sérialise, réessaie, contient l'exception et refuse d'écrire. À corriger AVANT
                les correctifs locaux DATA-1/3/5/7, qui en sont des instances.
Applicabilité : desktop mono-utilisateur — fichiers locaux sous %APPDATA% (vue MSIX possible) et ~/.claude, lecteurs concurrents
                réels (overlay, sonde, antivirus).
Statut challenge : non contesté
```

### Nouveaux nés de l'intersection

```
ID            : MAT-4
Sévérité      : Mineur
Rôle émetteur : Matrice Fiabilité ↔ Données
Localisation  : src/Chronos/Services/DiagnosticService.cs:145-147 ; src/Chronos/App.xaml.cs:118-141 ; src/Chronos/Services/SettingsService.cs:17 ;
                src/Chronos/Services/Historique/Tokens/MagasinAgregats.cs:228, 299 ; src/Chronos/Services/Historique/Tokens/IndexMessages.cs:286-287
Preuve        : CITATION DiagnosticService.cs:145-147 : `var s = _settings.Load();` puis `var lectureReglages = _settings.DerniereLecture;`
                — relecture FRAÎCHE, faite par LogStartupAsync (App.xaml.cs:141) APRÈS window.Show() (l.123) dont SourceInitialized →
                RestorePlacement → Save(Load() with { Background }) a déjà réécrit le fichier (OverlayController.cs:179-180, 219, 227).
                CITATION SettingsService.cs:17 : « journalisée une fois par démarrage par le rapport de diagnostic ».
                CITATION MagasinAgregats.cs:299 : `DerniereErreur = Decrire(ex);` (lecture ratée) puis l.228 `DerniereErreur = null;`
                à la première écriture réussie — celle-là même qui écrase le fichier avec l'état partiel.
Constat       : Les traces d'une lecture ratée sont effacées par l'écriture qu'elle provoque. (a) Un settings.json illisible
                est réécrit avec les défauts pendant Show ; le diagnostic, lancé après, relit un fichier désormais VALIDE et
                consigne « Lu » : l'issue Illisible n'atteint jamais chronos.log au démarrage. (b) L'erreur de lecture d'un mois
                d'agrégats est remise à null par le Move qui écrase ce mois ; la reconstruction rapporte un flush réussi.
                (c) L'échec de lecture d'un shard d'ids ne laisse aucune trace.
Impact        : les pertes de MAT-3 sont non seulement silencieuses mais indétectables après coup — même un utilisateur qui
                ouvre le diagnostic ne peut pas savoir que ses réglages ou un mois d'agrégats ont été écrasés.
Recommandation: relever `DerniereLecture` de la PREMIÈRE lecture du processus (ou un compteur cumulatif « lectures illisibles
                depuis le démarrage ») et la rapporter ; dans les magasins, une erreur de lecture ne s'efface que par une LECTURE
                réussie, pas par une écriture. Test : settings.json illisible + séquence de démarrage → chronos.log contient « illisible ».
Applicabilité : desktop — observabilité locale, chronos.log est le seul canal de diagnostic d'un overlay sans console.
Statut challenge : non contesté
```

```
ID            : MAT-5
Sévérité      : Mineur
Rôle émetteur : Matrice Fiabilité ↔ Données
Localisation  : src/Chronos/App.xaml.cs:266, 118, 133-134 ; src/Chronos/Services/ChronosSettings.cs:80 ; src/Chronos/Services/ClaudeSettingsReconciler.cs:213-255
Preuve        : CITATION App.xaml.cs:266 : `services.AddSingleton(sp => sp.GetRequiredService<SettingsService>().Load()); // ChronosSettings (une lecture)` ;
                App.xaml.cs:133-134 : `.Reconcile(settings.SessionsWidgetEnabled, commandeHeritee);` ;
                ChronosSettings.cs:80 : `public bool SessionsWidgetEnabled { get; init; }` (défaut false).
Constat       : Quand la lecture de démarrage de settings.json échoue (syntaxe, ou E/S passagère), le « défaut » false de
                SessionsWidgetEnabled est traité comme une DÉCISION et propagé à un fichier tiers : le réconciliateur retire tous
                les hooks Chronos de ~/.claude/settings.json (après sauvegarde), puis la Save de RestorePlacement persiste false
                dans settings.json (MAT-3). Une ignorance sur un fichier Chronos devient une écriture dans la configuration
                exécutable de Claude Code.
Impact        : widget de sessions désactivé et hooks désinstallés sans geste de l'utilisateur ni trace (MAT-4) ; réversible
                en réactivant le widget, et la sauvegarde horodatée existe.
Recommandation: ne pas réconcilier les hooks quand `DerniereLecture.Issue == Illisible` (ne toucher qu'à la barre, ou rien) ;
                plus généralement, passer l'issue de lecture avec la valeur (MAT-3).
Applicabilité : desktop — chaîne entre deux fichiers locaux de deux outils du même compte.
Statut challenge : non contesté
```

### Contredits
- **DATA-5, Constat « L'issue Illisible est bien journalisée, mais APRÈS que le fichier a été écrasé » — contredit** : elle n'est
  PAS journalisée du tout au démarrage (MAT-4 (a)).
- **DATA-1, Sévérité Bloquant — contestée** (voir Confirmés) : Majeur proposé, en arbitrage council.
- **FIAB-8, Impact « fenêtre étroite : un passage par démarrage » — incomplet** : l'installateur écrit aussi à chaque bascule du
  widget, sans sauvegarde (MAT-1).

---

## Paire 4 — Sécurité ↔ Données

Question : parsing / injection sur données persistées (SEC-2, SEC-8, DATA-4).

### Confirmés
- **SEC-2 — confirmé, conséquences aggravées par Données.** `sid = Str(r, "session_id")` sans validation
  (`SessionHookProcessor.cs:79`) → `Path.Combine(dossier, resultat.SessionId + ".json")` puis `File.Delete` sur `SessionEnd`
  (`EcritureEtatSession.cs:67-71`). Depuis `%APPDATA%\Chronos\sessions`, `..\historique\curseurs` supprime `curseurs.json` →
  **active DATA-1** (relecture complète, recomptage des mois gelés) ; `..\settings` supprime les réglages ; un chemin absolu peut
  viser `~/.claude/settings.json`. La sévérité Mineur reste juste (aucun vecteur hors du même compte : les `session_id` sont des UUID
  générés par Claude Code), mais l'ordre de correction doit suivre : SEC-2 avec ou avant DATA-1. Aucun autre usage de `session_id`
  dans un chemin (grep `Path.Combine` dans SessionMonitor / TranscriptSessionSource / ArchiveStore / TreatedStore : clés JSON seulement).
- **SEC-8 — confirmé.** `(reste ??= new MemoryStream()).Write(…)` sans plafond puis `reste.ToArray()` (`LecteurTranscript.cs:116, 137-141`).
  Sans effet d'intégrité : le curseur n'avance qu'après un `\n` traité, une ligne non terminée n'est jamais comptée.
- **DATA-4 — confirmé, avec une dimension sécurité absente des deux rapports.** Les clés effacées dans le scénario « fichier existant
  mais vide » incluent `permissions` — donc les règles `deny` de Claude Code. La perte n'est pas seulement de confort : elle relâche
  en silence le modèle de permission d'un outil qui exécute des commandes. Argument supplémentaire pour la sévérité Majeur de DATA-4
  et pour MAT-1 (le chemin installateur n'a aucune sauvegarde).
- **SEC-6 ↔ DATA-6 — confirmés, compatibles.** La commande héritée est lue brute avant tout Save (`App.xaml.cs:106`,
  `ClaudeSettingsReconciler.cs:140-144`) ; SEC-6 (pas de franchissement de frontière) et DATA-6 (perte si la réconciliation échoue au
  premier passage 3.5) décrivent deux faces sans se contredire.

### Nouveaux

```
ID            : MAT-6
Sévérité      : Info
Rôle émetteur : Matrice Sécurité ↔ Données
Localisation  : src/Chronos/Services/TranscriptActivityProvider.cs:66-80 ; src/Chronos/App.xaml.cs:170-174
Preuve        : CITATION TranscriptActivityProvider.cs:73-77 : `while ((line = await reader.ReadLineAsync(ct)) is not null) { … using var doc = JsonDocument.Parse(line);`
                — aucun plafond, aucun préfiltre (chaque ligne de chaque transcript récent est parsée).
                CITATION App.xaml.cs:171-172 : `using (var sr = new System.IO.StreamReader(Console.OpenStandardInput(), utf8)) input = sr.ReadToEnd();`
Constat       : SEC-8 ne cite qu'un lecteur ; il en existe deux autres sans borne sur des données d'origine externe :
                (a) TranscriptActivityProvider (inscrit en production, App.xaml.cs:342-346) lit les lignes en chaînes UTF-16
                entières et les parse toutes ; (b) le mode --hook lit tout le stdin puis le parse — or le stdin de PostToolUse
                contient la sortie de l'outil, potentiellement de plusieurs Mo, à chaque appel d'outil, dans un délai de grâce de 3 s.
Impact        : pics mémoire et latence ; aucun contenu exécuté ni réinjecté. Pas de vecteur d'attaque.
Recommandation: même plafond que celui recommandé par SEC-8 (ligne > N Mo → ignorée et comptée) dans TranscriptActivityProvider ;
                pour --hook, lire au plus quelques centaines de Ko de stdin ou extraire `session_id` par un Utf8JsonReader qui
                s'arrête dès les champs utiles trouvés.
Applicabilité : desktop — parsing local de fichiers et de flux produits par un autre outil.
Statut challenge : non contesté
```

### Contredits
- Aucun.

---

## Récapitulatif

| Paire | Confirmés | Nouveaux | Contredits / nuancés |
|---|---|---|---|
| Architecte ↔ Sécurité | SEC-3 (atténué), SEC-4, SEC-7, SEC-9, ARCH-3 | MAT-1, MAT-2 | ARCH-1 (OAuth = login seul, nuance) |
| Architecte ↔ Tests | ARCH-2 ≡ TEST-2 (aggravé → Majeur proposé), TEST-6/7 ↔ ARCH-10, ARCH-3 | — (ARCH-1 relié au groupe Fiab/Données) | ARCH-1 Impact « testable seulement avec vrais services » (nuancé) |
| Fiabilité ↔ Données | FIAB-2, FIAB-3, FIAB-5, DATA-1 (mécanisme), DATA-2 (+extension), DATA-3, DATA-4/FIAB-8 | MAT-3 (cause racine), MAT-4, MAT-5 | DATA-5 (journalisation), DATA-1 (sévérité), FIAB-8 (fenêtre) |
| Sécurité ↔ Données | SEC-2 (conséquences aggravées), SEC-8, DATA-4 (+dimension permissions), SEC-6/DATA-6 | MAT-6 | — |

Nouveaux : **Majeur 1** (MAT-3) · **Mineur 4** (MAT-1, MAT-2, MAT-4, MAT-5) · **Info 1** (MAT-6).

À transmettre à l'arbitrage council (§6.3) : sévérité de DATA-1 (Bloquant vs Majeur) ; sévérité d'ARCH-2/TEST-2 (Majeur vs Mineur) ;
ordre de correction proposé : MAT-3 (lecture tri-état + façade ARCH-1) → FIAB-1/FIAB-2 → DATA-1/2/3 → MAT-1 (passerelle unique) + DATA-4.

Non vérifié : probabilité réelle de collision d'ouverture sur `settings.json` (antivirus/indexeur, FIAB-2) ; comportement de
Claude Code face à une commande de hook introuvable (MAT-2 (1)) ; exe jamais lancé, suite de tests non relancée par la matrice.
