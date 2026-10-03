# Audit dev-team-council — Rôle Données / Persistance (Tier 2) — Chronos, cycle ZEUS 2 (2026-10-03)

Périmètre : persistance FICHIERS (pas de SQLite/EF). Journal des relevés `releves-AAAA-MM.jsonl`, index d'ids
`ids-AAAA-MM.jsonl`, agrégats `tokens-AAAA-MM.jsonl`, `curseurs.json`, `couverture.json`, `last-exact.json`,
`settings.json` (Chronos), `archived.json` / `treated.json`, écriture de `~/.claude/settings.json` (réconciliateur,
installateur de hooks) et sauvegardes `%APPDATA%\Chronos\backups`. Cycle 1 (v1.8, magasins historiques) et cycle 2
(purge phase 37, lecture tolérante des réglages, réconciliateur 3.5).

## Phase 1 — Vérité-terrain

| Contrôle | Résultat |
|---|---|
| EF Core / SQLite / SQL brut | `grep SqlCommand|FromSqlRaw|DbContext|Sqlite` sur `src` : **0 occurrence**. Migrations EF : sans objet. |
| Inventaire des écritures | `grep File.(WriteAll|AppendAll|Move|Replace|Delete|Copy)|FileStream(` : 52 sites. Temp+`Move` : settings, last-exact, curseurs, couverture, agrégats, archived (Add), treated, oauth.dat, ~/.claude/settings.json. Ajout exclusif (`FileShare.None` + relecture sous verrou + reprises) : journal, shards d'ids, états de session. Écriture directe non atomique : `ArchiveStore.PurgerPrefixe` (code mort en prod), `chronos.log`. |
| Versionnage de schéma | `v`/`version` = 1 sur les 6 formats Chronos (LigneJournal, LigneAgregat, IndexMessages, Curseurs, CouvertureTokens, LastExactStore) ; ligne/fichier de version inconnue sauté. `settings.json` : non versionné, lecture tolérante valeur par valeur. |
| Données réelles (lecture seule, `%APPDATA%\Chronos\historique`) | `releves-2026-09` : 913 lignes (894 SondeEnTetes, 7 EndpointOAuthChronos, **0** EndpointOAuthClaude, **0** PontStatusLine) ; `releves-2026-10` : 722 lignes (toutes SondeEnTetes). Shards d'ids : 31 Mo (dont `ids-2026-09` = 21 Mo). |
| Exposition « relecture complète » (mesurée) | Sur 1 545 transcripts de `~/.claude/projects` modifiés depuis < 30 j, **228** commencent avant l'horizon de l'index (2026-08-19) et **170** avant le 1er août (mois d'agrégats GELÉS ; plus ancien : 2026-06-23). |
| Tests de panne du flush | `ReconstructionTokensTests` l.422-449 : panne simulée par EXCEPTION entre deux étapes (avorte la chaîne). Aucun test d'un `_index.Flush()` qui rend `false` suivi d'une sauvegarde des curseurs. `IndexMessagesTests` l.216 : un id de juin re-rencontré est **recompté** (limite assumée). |

## Phase 2 — Findings

### DATA-1 — Une perte de `curseurs.json` recompte en double l'historique des mois gelés
- **Sévérité** : Bloquant
- **Rôle** : Données / Persistance
- **Localisation** : `src/Chronos/Services/Historique/Tokens/Curseurs.cs:72-105` ; `IndexMessages.cs:29-41,104-112` ; `MagasinAgregats.cs:152-172` ; `ReconstructionTokens.cs:316-321`
- **Preuve** : `Curseurs.Charger` rend des curseurs VIDES sur JSON invalide, version inconnue, `IOException` ou `UnauthorizedAccessException` (l.101-103), avec la justification « tout sera relu de 0 : coûteux mais juste, l'index d'ids rend la relecture idempotente » (l.73). Or l'index ne charge que les mois qui chevauchent `[now − 45 j, now]` (`MoisOuverts`, l.104-112) et `IndexMessagesTests` l.216 prouve qu'un id plus ancien est recompté. `MagasinAgregats.Appliquer` charge le mois gelé depuis son fichier PUIS ajoute le delta (l.162-168). Mesure : 170 transcripts vivants commencent avant le 1er août.
- **Constat** : l'affirmation d'idempotence n'est vraie que sur 45 jours. Une relecture complète (curseurs perdus ou illisibles, racine `ProjectsRoot` qui change de forme → toutes les clés relatives changent, transcript « raccourci » relu depuis 0) ajoute une deuxième fois tous les messages de plus de 45 jours aux tranches des mois gelés déjà écrites.
- **Impact** : agrégats de tokens des mois gelés doublés (en tout ou partie), en silence, de façon permanente (aucune reprojection des mois gelés), et affichés par l'Historique comme des comptes exacts. Contraire au principe « exact ou rien ».
- **Recommandation** : (1) dans `Traiter`, ignorer (et compter au diagnostic) tout message dont le premier timestamp tombe dans un mois GELÉ, au lieu de l'appliquer. (2) Ou bien : après des curseurs vides alors que des agrégats existent, ne pas toucher aux mois gelés. (3) Corriger le commentaire l.73. (4) Ajouter un test : curseurs supprimés + transcript contenant un message de plus de 45 jours → octets du mois gelé inchangés.
- **Applicabilité** : déclencheurs rares (curseurs.json est écrit par temp+Move, donc rarement corrompu), mais une erreur d'E/S passagère à l'amorçage suffit, et le rayon d'impact est mesuré (170 fichiers). Sévérité fixée par l'impact (corruption silencieuse et permanente d'une donnée présentée comme exacte).
- **Statut challenge** : à challenger

### DATA-2 — Le flush continue après un échec de l'index : les curseurs avancent au-delà de ce qui est persisté
- **Sévérité** : Majeur
- **Rôle** : Données / Persistance
- **Localisation** : `src/Chronos/Services/Historique/Tokens/ReconstructionTokens.cs:329-347` (et 30-34 pour l'invariant annoncé)
- **Preuve** : `Flush()` exécute `_index.Flush()` → `_magasin.EcrireMoisSales()` → `_curseurs.Sauvegarder()` « sans interrompre les étapes suivantes » ; un `false` de l'index n'empêche pas la sauvegarde des curseurs (l.336-342). Au démarrage, les mois ouverts sont REPROJETÉS depuis l'index seul (l.277-282).
- **Constat** : l'invariant D-33-14 (« un arrêt à n'importe quel point est rattrapé ») suppose qu'un curseur ne dépasse jamais ce que l'index a persisté. Si l'écriture du shard échoue (disque plein, verrou tenu au-delà de 600 essais, droits) et que le processus s'arrête avant un flush réussi, les lignes en attente de l'index (en mémoire seulement) sont perdues, les curseurs ont avancé, et la reprojection du redémarrage efface ces messages des agrégats. Ils ne seront jamais relus.
- **Impact** : sous-comptage permanent des tokens d'un mois ouvert, sans trace après redémarrage (l'erreur n'était qu'en mémoire).
- **Recommandation** : arrêter la chaîne au premier échec (ne pas écrire les curseurs si l'index n'a pas été écrit ; facultativement ne pas écrire les agrégats non plus). Ajouter un test « l'index rend false → curseurs.json inchangé ».
- **Applicabilité** : nécessite un échec d'écriture persistant puis un arrêt ; probabilité faible, perte définitive.
- **Statut challenge** : à challenger

### DATA-3 — Une lecture ratée d'un mois d'agrégats est prise pour un état vrai, puis réécrite par-dessus le fichier
- **Sévérité** : Majeur
- **Rôle** : Données / Persistance
- **Localisation** : `src/Chronos/Services/Historique/Tokens/MagasinAgregats.cs:274-306` ; `IndexMessages.cs:263-288`
- **Preuve** : `ChargerMoisSousVerrou` : « L'état mémoire du mois est remplacé par ce qui a été lu — même vide, même après une erreur d'E/S (consignée) : le mois est alors « connu » » (l.274-275, 297-305). Le prochain `Appliquer` sur ce mois le salit (l.170) et `EcrireMoisSales` réécrit le fichier ENTIER avec cet état partiel (l.208-213). Côté index, `ChargerShard` avale `IOException`/`UnauthorizedAccessException` sans compteur ni `DerniereErreur` (l.286-287) ; la reprojection du démarrage remplace alors le fichier d'agrégats du mois ouvert par la projection de cet index incomplet (`ReconstructionTokens.cs:277-282`).
- **Constat** : « je n'ai pas pu lire » est traité comme « il n'y a rien ». Pour un mois GELÉ, la seule copie des agrégats est ce fichier : une lecture interrompue suivie d'un delta l'écrase. Pour un mois ouvert, l'écrasement guérit au redémarrage suivant (les shards restent la vérité), mais l'Historique affiche entre-temps un sous-comptage, sans rien signaler.
- **Impact** : perte définitive des agrégats d'un mois gelé (chemin rare : verrou externe, antivirus, disque) ; sous-comptage silencieux et passager sur un mois ouvert.
- **Recommandation** : en cas d'erreur de lecture, NE PAS marquer le mois comme connu (le retirer de `_parMois`) et refuser `Appliquer`/`RemplacerMois` sur ce mois jusqu'à une lecture réussie. Dans `ChargerShard`, reprendre quelques fois (motif `LecteurJournal`), puis poser une erreur qui empêche la reprojection du mois concerné.
- **Applicabilité** : probabilité faible ; l'impact (perte définitive) justifie Majeur.
- **Statut challenge** : à challenger

### DATA-4 — Un `~/.claude/settings.json` présent mais vide est traité comme un fichier neuf et réécrit
- **Sévérité** : Majeur
- **Rôle** : Données / Persistance
- **Localisation** : `src/Chronos/Services/ClaudeSettingsJson.cs:134-137` ; `ClaudeSettingsReconciler.cs:213-248` ; `SessionHookInstaller.cs:158-172`
- **Preuve** : `ParseOrNull` : `if (string.IsNullOrWhiteSpace(json)) return new JsonObject();` avec le commentaire « fichier absent = départ légitime ». Mais `Reconcile` a déjà vérifié `File.Exists` (l.217) et `Install`/`Uninstall` lisent un fichier qui existe. Contenu vide + hooks voulus → `{ "hooks": … }` écrit par-dessus. `Install`/`Uninstall` n'ont AUCUNE sauvegarde (l.158-172). Entre la lecture (l.225) et le `Move` (l.310) : aucune vérification que le fichier n'a pas changé ; la sauvegarde `File.Copy` (l.270) copie l'état du disque à ce moment-là, pas le texte `actuel` sur lequel la fusion a été calculée.
- **Constat** : la doctrine (b) du fichier (« jamais une page blanche ») est contournée par le cas « existe mais vide ». C'est justement ce que voit un lecteur qui tombe pendant une écriture non atomique (tronquer puis écrire) de Claude Code, ou d'un éditeur. Plus généralement, le lire-modifier-écrire n'a pas de contrôle de concurrence avec Claude Code, qui écrit lui aussi ce fichier.
- **Impact** : effacement des `permissions`, `env`, `model` et des hooks tiers de l'utilisateur. Côté réconciliateur, la sauvegarde horodatée limite souvent les dégâts ; elle n'existe pas côté installateur.
- **Recommandation** : (1) fichier EXISTANT et vide ou blanc → `null` (« ne rien écrire »), un objet vide uniquement si le fichier est ABSENT. (2) Avant le `Move`, relire le fichier (ou son mtime et sa taille) et abandonner s'il a changé depuis la lecture. (3) Sauvegarder exactement le texte `actuel` (et non le disque au moment de la copie), et sauvegarder aussi dans `Install`/`Uninstall`.
- **Applicabilité** : course étroite (au démarrage, ou au basculement du widget de sessions, pendant une écriture de Claude Code) ; impact élevé sur la configuration d'un autre outil.
- **Statut challenge** : à challenger

### DATA-5 — Un `settings.json` Chronos illisible est remplacé par les défauts au démarrage suivant
- **Sévérité** : Majeur
- **Rôle** : Données / Persistance
- **Localisation** : `src/Chronos/Services/SettingsService.cs:92-131,155-166` ; `OverlayController.cs:104-110,179-180,219,227`
- **Preuve** : `Load()` : JSON illisible, racine non objet ou `IOException` → `new ChronosSettings()` (l.124-130). Tous les sites d'écriture suivent `Save(Load() with { … })`. `RestorePlacement` appelle TOUJOURS `SendToBackground()` ou `BringToForeground()` (l.179-180), qui font `_settings.Save(_settings.Load() with { Background = … })` (l.219, 227) : il y a donc une écriture à CHAQUE démarrage.
- **Constat** : une seule virgule de trop dans une édition manuelle (les commentaires et virgules finales sont tolérés, pas une accolade manquante), ou une erreur d'E/S passagère, et le démarrage suivant écrase silencieusement le fichier avec les défauts : coin, moniteur, thème, styles, orientations, `WeeklyAnchor`, géométrie de l'Historique. L'issue `Illisible` est bien journalisée, mais APRÈS que le fichier a été écrasé.
- **Impact** : perte de toute la configuration utilisateur sans sauvegarde ; l'ancre hebdo (recalibrage) est perdue avec le reste.
- **Recommandation** : sur `Illisible`, copier le fichier en `settings.illisible-<horodatage>.json` avant toute réécriture, ou bloquer les `Save` tant que l'utilisateur n'a pas tranché ; ne pas écrire `Background` au démarrage si la valeur n'a pas changé.
- **Applicabilité** : se produit à coup sûr dès que le fichier est illisible ; la gravité dépend de la valeur que l'utilisateur accorde à ses réglages.
- **Statut challenge** : à challenger

### DATA-6 — La commande `statusLine` d'origine de l'utilisateur peut être perdue définitivement pendant la migration 3.5
- **Sévérité** : Mineur
- **Rôle** : Données / Persistance
- **Localisation** : `src/Chronos/App.xaml.cs:103-107,123,131-136` ; `ClaudeSettingsReconciler.cs:105-144,213-255` ; `SettingsService.cs:155-166`
- **Preuve** : la clé héritée `InnerStatusLineCommand` n'existe plus que dans `%APPDATA%\Chronos\settings.json` ; elle est lue une fois en mémoire (l.106) ; `window.Show()` (l.123) déclenche `Save(Load() with …)` qui RÉÉCRIT le fichier sans les membres inconnus. `Reconcile` peut ne rien écrire (« illisible — rien écrit », « sauvegarde impossible », exception) et ne fait que poser un bilan.
- **Constat** : si la réconciliation échoue au premier démarrage 3.5, la clé a déjà disparu du disque. Au démarrage suivant, la barre Chronos est RETIRÉE au lieu d'être restaurée : la commande d'origine n'existe plus nulle part (les sauvegardes de `~/.claude/settings.json` ne contiennent que la barre Chronos chaînée).
- **Impact** : l'utilisateur perd la barre de statut qu'il avait avant Chronos.
- **Recommandation** : conserver les membres inconnus dans `settings.json` (`[JsonExtensionData]` sur `ChronosSettings`, ou fusion `JsonObject`), ou recopier la clé héritée dans un fichier dédié tant que le bilan n'est pas `Restauree`/`Retiree` avec `Ecrit = true`.
- **Applicabilité** : migration ponctuelle ; il faut un utilisateur qui avait chaîné une barre ET une réconciliation en échec à ce démarrage-là.
- **Statut challenge** : à challenger

### DATA-7 — `archived.json` / `treated.json` : une lecture en échec repart d'un ensemble vide puis réécrit
- **Sévérité** : Mineur
- **Rôle** : Données / Persistance
- **Localisation** : `src/Chronos/Services/ArchiveStore.cs:57-87,110-138` ; `TreatedStore.cs:76-131`
- **Preuve** : `Add` : `catch { }` autour de la relecture (l.63-74) → `map` vide → `map[sessionId] = now` → temp+`Move` (l.77-86). Même schéma dans `TreatedStore.LoadMutable` + `WriteAtomic`. Les échecs d'écriture sont avalés (`catch { }` l.86, l.130). `PurgerPrefixe` écrit en DIRECT (l.133), sans appelant en production (grep : seulement des tests et des commentaires). Le commentaire l.99-101 parle encore d'une « purge des entrées expirées » dans `Add`, qui n'existe plus.
- **Constat** : un fichier corrompu ou momentanément illisible efface TOUTES les archives au prochain geste, alors que le contrat est « ce qui est archivé NE REVIENT JAMAIS ». Un geste dont l'écriture échoue est perdu sans être signalé.
- **Impact** : des sessions archivées réapparaissent ; perte silencieuse de gestes de l'utilisateur.
- **Recommandation** : distinguer « absent » (vide légitime) et « illisible » (ne pas écrire, ou sauvegarder l'original d'abord) ; exposer l'échec (`DerniereErreur`) comme les magasins de l'historique ; supprimer `PurgerPrefixe` ou le passer en temp+`Move`.
- **Applicabilité** : faible fréquence, données de faible valeur.
- **Statut challenge** : à challenger

### DATA-8 — La rétention des sauvegardes de `~/.claude/settings.json` évince la sauvegarde pré-purge qu'elle prétend protéger
- **Sévérité** : Mineur
- **Rôle** : Données / Persistance
- **Localisation** : `src/Chronos/Services/ClaudeSettingsReconciler.cs:55-59,63-64,262-281`
- **Preuve** : commentaire (d) : sauvegarder « uniquement quand une écriture va réellement avoir lieu, sinon la rétention évincerait la sauvegarde la plus précieuse : celle du tout premier passage ». Mais la rétention garde les 5 plus récentes par nom (`OrderByDescending(f => f.Name).Skip(garder)`). Chaque nouvelle version publiée sous un nouveau nom (`Chronos-vX.Y.exe`, convention de versionnage) repointe les hooks et déclenche donc une écriture et une sauvegarde.
- **Constat** : au bout de 5 versions, la sauvegarde d'avant la purge (25 groupes de hooks, barre `statusLine` d'origine) est supprimée.
- **Impact** : plus aucun retour possible à l'état d'avant Chronos.
- **Recommandation** : épingler la première sauvegarde (`claude-settings-initial.json`, jamais évincée) et appliquer la rétention aux suivantes seulement.
- **Applicabilité** : certaine à moyen terme vu le rythme des versions.
- **Statut challenge** : à challenger

### DATA-9 — Un ajout après une dernière ligne tronquée colle la nouvelle ligne au fragment (deux lignes perdues)
- **Sévérité** : Mineur
- **Rôle** : Données / Persistance
- **Localisation** : `src/Chronos/Services/Historique/JournalReleves.cs:224-236` ; `Tokens/IndexMessages.cs:330-341`
- **Preuve** : `fs.Seek(0, SeekOrigin.End); fs.Write(ligne…)` sans vérifier que le dernier octet existant est `\n`. `LecteurJournal` documente lui-même le cas « dernière ligne tronquée (processus tué en pleine écriture) » (l.27-28). `Flush(flushToDisk: false)`.
- **Constat** : après une coupure de courant ou un disque plein pendant un ajout, le fragment n'est pas isolé : la ligne suivante est concaténée et devient illisible elle aussi. Pour l'index, la ligne perdue est un id oublié, ce qui ouvre un recomptage possible.
- **Impact** : une ligne valide perdue en plus du fragment (relevé exact ou entrée d'index).
- **Recommandation** : sous le verrou, si `fs.Length > 0` et que le dernier octet n'est pas `\n`, écrire d'abord un `\n` (le fragment devient une ligne ignorée isolée).
- **Applicabilité** : rare (arrêt brutal pendant une écriture d'environ 230 octets).
- **Statut challenge** : à challenger

### DATA-10 — Revenir à une version antérieure détruit les données d'une version plus récente
- **Sévérité** : Mineur
- **Rôle** : Données / Persistance
- **Localisation** : `src/Chronos/Services/Historique/Tokens/MagasinAgregats.cs:191-231,276-306` ; `LigneAgregat.cs:102-103` ; `IndexMessages.cs:299` ; `SettingsService.cs:115,155-166`
- **Preuve** : une ligne de version ≠ 1 est SAUTÉE à la lecture, puis le mois est RÉÉCRIT en entier (agrégats, et reprojection des mois ouverts au démarrage) ; `SettingsService.Save` sérialise le record, donc tout membre inconnu (réglage ajouté par une version plus récente) disparaît.
- **Constat** : la règle « une version inconnue est sautée, pas devinée » est juste en LECTURE, mais les magasins à réécriture complète transforment « sauté » en « supprimé ». Les exe versionnés coexistent sur la machine (`Chronos-vX.Y.exe`), donc relancer une ancienne version est un geste plausible.
- **Impact** : une ancienne version efface les agrégats v2 et les réglages ajoutés depuis.
- **Recommandation** : dans les magasins à réécriture, conserver telles quelles les lignes de version supérieure (ou refuser d'écrire un fichier qui en contient) ; préserver les membres inconnus de `settings.json` (`[JsonExtensionData]`, voir DATA-6).
- **Applicabilité** : latent tant qu'aucun schéma v2 n'existe ; à traiter avant le premier changement de schéma.
- **Statut challenge** : à challenger

### DATA-11 — `SettingsService.Save` peut lever face à un `Load` concurrent, sur le thread UI, sans filet
- **Sévérité** : Mineur
- **Rôle** : Données / Persistance (recoupe Fiabilité)
- **Localisation** : `src/Chronos/Services/SettingsService.cs:101,164-165` ; `RateLimitHeaderUsageProvider.cs:205` ; `OverlayController.cs:104,219,227`
- **Preuve** : `Load` ouvre par `File.ReadAllText` (partage lecture, sans `FileShare.Delete`) ; la sonde relit les réglages à chaque passage (l.205), hors du thread UI ; `Save` fait `File.Move(tmp, cible, overwrite: true)`. Fait mesuré par le projet lui-même (`MagasinAgregats.cs:32-34`) : un lecteur qui tient la cible fait échouer le `Move` (`UnauthorizedAccessException`). Les appels `Save` d'`OverlayController` ne sont pas protégés ; grep `DispatcherUnhandledException|UnhandledException` sur `src/Chronos` : 0 occurrence.
- **Constat** : la collision (une lecture d'environ 1 ms toutes les 5 minutes contre un geste de placement ou de bascule) fait sortir une exception non gérée sur le thread UI. De plus, `Save(Load() with …)` n'est pas sérialisé entre threads (perte de mise à jour possible).
- **Impact** : réglage non enregistré, voire arrêt de l'application.
- **Recommandation** : verrou interne à `SettingsService` (Load/Save sérialisés), reprises bornées sur le `Move` (motif `EcrireSousVerrou`), et `try/catch` aux sites UI avec erreur exposée au diagnostic.
- **Applicabilité** : fenêtre très étroite.
- **Statut challenge** : à challenger

### DATA-12 — Sources retirées : les relevés v1.8 `EndpointOAuthClaude` / `PontStatusLine` deviennent invisibles
- **Sévérité** : Info
- **Rôle** : Données / Persistance
- **Localisation** : `src/Chronos/Models/SourceUsage.cs` (diff v1.8..HEAD) ; `LigneJournal.cs:151-152` ; `tests/Chronos.Tests/LigneJournalTests.cs:204-231`
- **Preuve** : les deux membres ont été retirés de l'enum ; une ligne qui les cite est refusée (`LireEnum` → null → `return false`) et comptée parmi les lignes ignorées, ce qui est testé. Données locales : 0 ligne concernée sur 1 635.
- **Constat** : la compatibilité de LECTURE est tenue (aucune exception, la ligne suivante est lue). En revanche, des relevés EXACTS historiques sortent de l'Historique et gonflent le compteur « lignes ignorées » du diagnostic, ce qui peut se lire comme une corruption. Sans impact sur cette machine.
- **Recommandation** : documenter dans `docs/data-sources.md` que ces lignes sont ignorées volontairement, ou les compter à part (« source retirée ») au diagnostic.
- **Statut challenge** : à challenger

### DATA-13 — « Inaccessible » se lit comme « vide »
- **Sévérité** : Info
- **Rôle** : Données / Persistance
- **Localisation** : `src/Chronos/Services/Historique/LecteurJournal.cs:41-57,89-93`
- **Preuve** : après 20 essais d'ouverture, `IOException`/`UnauthorizedAccessException` → `LectureFichier.Vide` ; une E/S cassée en cours de lecture → fin de lecture.
- **Constat** : l'Historique ne peut pas distinguer « aucun relevé » de « fichier momentanément illisible » et affichera un trou de couverture qui n'existe pas. L'erreur est honnête (aucune donnée inventée), mais elle n'est pas dite.
- **Recommandation** : marquer la lecture comme « partielle/inaccessible » et l'afficher comme telle.
- **Statut challenge** : à challenger

### DATA-14 — Hygiène : durabilité, fichiers orphelins, code mort
- **Sévérité** : Info
- **Rôle** : Données / Persistance
- **Localisation** : `JournalReleves.cs:232`, `IndexMessages.cs:339` (`Flush(flushToDisk: false)`) ; `DiagnosticService.cs:115,133` (`chronos.log` en écriture directe) ; `ChronosPaths.cs:7-8` (`usage.json` résiduel « ni lu ni supprimé ») ; `ArchiveStore.PurgerPrefixe` (sans appelant en production) ; `ids-2026-06.jsonl` encore présent en octobre (purge au prochain démarrage ; non concluant).
- **Constat** : acceptable pour une app desktop (perte d'au plus la dernière ligne sur coupure de courant), mais à consigner. Les orphelins de la purge (`usage.json`) pourraient être retirés une fois, par geste explicite et journalisé.
- **Statut challenge** : à challenger

## Points solides (constatés, sans finding)
- Écriture atomique temp+`Move` sur toutes les PROJECTIONS (settings, last-exact, curseurs, couverture, agrégats) ; ajout exclusif avec relecture de la queue SOUS LE MÊME verrou pour le journal : l'idempotence (t, source) tient entre processus et entre threads.
- Versionnage `v` uniforme, lu EN PREMIER, et lecture tolérante ligne par ligne avec compteurs exposés au diagnostic.
- `LastExactStore` ne persiste ni la fiabilité ni la géométrie (dérivées), et ne persiste pas la source : la purge des sources ne le touche pas.
- `SettingsService.Load` tolérant valeur par valeur ; propriétés supprimées (`OAuthUsageEnabled`, `InnerStatusLineCommand`, `StatusLinePromptDismissed`, `HistoriqueStyleSemaine`) ignorées sans perte des autres valeurs.
- Réconciliateur : jamais d'écriture sur un JSON inexploitable, sauvegarde AVANT écriture, comparaison sur la forme normalisée (pas de churn).

## Bilan
| Sévérité | Nombre | IDs |
|---|---|---|
| Bloquant | 1 | DATA-1 |
| Majeur | 4 | DATA-2, DATA-3, DATA-4, DATA-5 |
| Mineur | 6 | DATA-6, DATA-7, DATA-8, DATA-9, DATA-10, DATA-11 |
| Info | 3 | DATA-12, DATA-13, DATA-14 |

**Non vérifié faute d'outil / par règle** : aucune exécution de la suite de tests ni de l'exe (lecture seule) ; les scénarios DATA-1 à DATA-5 sont établis par lecture du code et par les tests existants, sans reproduction. Mesures disque faites sur `%APPDATA%\Chronos\historique` et `~/.claude/projects` tels que vus par le shell de session (vue MSIX virtualisée possible, voir mémoire projet). Migrations EF : sans objet (aucun EF/SQLite).
