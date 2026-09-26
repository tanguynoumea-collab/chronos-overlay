# Phase 31 : Écrit, publié, constaté — Recherche

**Researched:** 2026-09-26 (pendant l'exécution de 30-03 : HEAD à `5db55b0`)
**Domain:** contrat documenté d'une source externe non documentée + garde croisée document ↔ code ; release
mono-fichier et réconciliation de `~/.claude/settings.json` au premier lancement ; protocole de constat humain en
production, avec relevés en lecture seule dans la vue RÉELLE d'AppData
**Confidence:** HIGH sur la release et la réconciliation (code lu, état de la machine relevé en lecture seule ce jour :
processus, raccourcis, registre, fichiers réels) ; HIGH sur le mécanisme de garde (calqué sur une garde existante,
lue) ; MEDIUM sur deux comportements de l'app bureau que seul le constat tranchera (latence d'écriture de
`lastFocusedAt`, accueil/Chat au premier plan) et sur l'émission de `PermissionRequest` pour `AskUserQuestion`.

> **Huit faits à lire avant tout plan.**
>
> 1. **Le `.gitignore` n'ignore PAS les exe.** Le CONTEXT dit le contraire ; `git check-ignore -v Chronos-v3.1.0.exe`
>    ne rend rien et `git status` montre `?? Chronos-v3.1.0.exe` (NON SUIVI, pas ignoré). Un `git add -A` pendant la
>    phase committerait 77 Mo. Le plan de release doit rendre la prémisse vraie (ajouter `/Chronos-v*.exe` au
>    `.gitignore`) ou n'ajouter que des chemins explicites — de préférence les deux.
> 2. **Il n'existe AUCUN mécanisme de mono-instance** (aucun `Mutex`, aucun test de processus dans `src/`). Lancer la
>    3.2.0 sans quitter la 3.1.0 donne deux overlays, deux détecteurs qui écrivent le même `treated.json`, deux
>    widgets. L'utilisateur doit quitter la 3.1.0 (réglages → « Quitter Chronos ») AVANT le premier lancement.
> 3. **Il n'y a AUCUN autostart à repointer.** Vue réelle (Python, `%APPDATA%\Claude` absent) : le dossier Démarrage
>    ne contient que `desktop.ini` ; aucune entrée Chronos dans `HKCU\…\Run`, aucune tâche planifiée. L'overlay
>    3.1.0 (PID 40772) a été lancé à la main par l'Explorateur le 23.09 à 11:09:31. Activer l'autostart est un
>    choix de l'utilisateur, fait depuis la 3.2.0 (le raccourci cible alors `Environment.ProcessPath`).
> 4. **La preuve de la réconciliation est le FICHIER, pas le journal.** `chronos.log` est écrit par
>    `LogStartupAsync`, lancé sans attente AVANT l'appel à `Reconcile` : sa ligne « Commande hook » peut montrer
>    l'ancien exe. La sauvegarde horodatée créée par la réconciliation EST l'état « avant » ; « après » doit en
>    être l'exacte substitution `Chronos-v3.1.0.exe` → `Chronos-v3.2.0.exe` (9 occurrences), à l'égalité
>    structurelle près (contrôle Python donné en Code Examples).
> 5. **« Jonction » est faux.** CONTEXT, ROADMAP (critère 1) et APP-01 disent « jonction MSIX » ; l'erratum de
>    `27-RELEVE.md` et `RacinesEtat.cs` disent l'inverse : **virtualisation d'AppData du paquet MSIX, aucun point
>    d'analyse**. Le document doit écrire « ce n'est pas une jonction » — et la garde peut le tenir.
> 6. **Le diagnostic n'affiche pas la version embarquée.** Le critère 2 cite « (propriétés du fichier,
>    diagnostic) » ; le rapport n'a que « Exe courant : <chemin> » (le NOM porte la version, pas l'assembly). Une
>    ligne `Version : 3.2.0` (lue dans `AssemblyInformationalVersionAttribute`) est à ajouter AVANT la publication,
>    sous test — ou le critère se lit par le seul nom de fichier (décision du plan, voir Open Questions n° 1).
> 7. **La ligne 3 du protocole (« n'apparaît jamais « En attente » ») contredit le comportement livré.** La grâce
>    de 2,5 s (`HorizonsSessions.GraceLecture`) + la cadence du widget (2 s) + le cache du premier plan (1 s)
>    laissent la session « En attente » **2,5 à ~5,5 s** avant qu'elle disparaisse (30-RESEARCH, Piège 12 :
>    « voulu, à dire à l'utilisateur au constat de phase 31, pas à corriger »). Le protocole doit MESURER cette
>    durée et la faire juger, pas asserter « jamais » — sinon la ligne 3 sera consignée en échec pour un
>    comportement décidé.
> 8. **`treated.json` ne porte pas les motifs.** C'est une carte `id → ms` (épisode) ; la cause (« lue : focus à …
>    > attente à … », « sélectionnée au premier plan », « répondue ») vit en mémoire dans le détecteur de
>    l'overlay et ne se lit que dans « Diagnostic… » (une boîte de message : Ctrl+C en copie le texte). L'agent
>    relit le reste en lecture seule, dans la vue RÉELLE, par Python (prouvé ce jour : `'Claude' in
>    os.listdir(APPDATA)` = False) — une sonde WMI n'est plus nécessaire, l'overlay publié tourne lui-même hors
>    de l'arbre de l'app.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Phase Boundary (copié tel quel)

Trois livrables, dans cet ordre : (1) `docs/desktop-app-sessions.md` décrit le contrat FINAL de la source
app-bureau (VAL-02) ; (2) l'exe **3.2.0** est publié et réconcilié (VAL-03) ; (3) l'utilisateur constate sur sa
machine les trois lignes de son tableau — « Réflexion », « En attente », rien — et le résultat est consigné
(point de contrôle humain, leçon de la rétrospective v1.6).

### Locked Decisions (copiées telles quelles)

#### Le document (VAL-02)
- `docs/desktop-app-sessions.md`, même structure que `docs/hooks-contract.md` : ce qui est lu (chemin,
  jonction MSIX, champs), ce qui est produit (titre, dernier focus, `blocked`), ce qui n'est PAS garanti
  (format interne non documenté, réécriture intégrale, `postTurnSummary` transitoire, `lastFocusedAt` tel
  que relevé en phase 27 : mis à jour par alt-tab et par re-sélection, PAS par la fin d'un tour), la date du
  relevé (2026-09-25, app 2.9939.2.0) et la réserve de la phase 27 (16:23 → 19:51 sans mise à jour).
- Une garde croisée (test) compare les champs cités dans le document à ceux que le lecteur lit réellement,
  sur le modèle de `ContratHooksDocumenteTests` (chemin de `docs/` injecté par MSBuild : `CheminDocsChronos`).
- `docs/hooks-contract.md` §3 est relu : les trois mots (« Réflexion », « En attente », « En attente ? ») et la
  règle « lue » (référence vers le nouveau document) ; `docs/data-sources.md` mentionne la nouvelle source.
- README : la section widget de sessions décrit les deux libellés, la disparition à la lecture, le titre.

#### La release (VAL-03) — procédure de la 3.1.0, à reproduire
- `src/Chronos/Chronos.csproj` : les QUATRE propriétés passent à 3.2.0 (`Version` 3.2.0, `FileVersion`
  3.2.0.0, `AssemblyVersion` 3.2.0.0, `InformationalVersion` 3.2.0).
- Publication : `dotnet publish src/Chronos -c Release -r win-x64 -p:PublishSingleFile=true
  --self-contained true` (détails `docs/publish.md`), puis copie de l'exe à la racine du dépôt sous le nom
  **`Chronos-v3.2.0.exe`** (non versionné : le `.gitignore` ignore les exe ; `Chronos-v3.1.0.exe` est déjà
  là, non suivi). Attendu : mono-fichier, ~77 Mo (< 120), 0 DLL à côté.
- Smoke test AVANT toute réconciliation : `Chronos-v3.2.0.exe --hook SessionStart` avec stdin vide ⇒ code 0,
  aucune écriture, `~/.claude/settings.json` inchangé (md5 avant/après).
- Réconciliation : au premier lancement de la 3.2.0, `SessionHookInstaller` et l'installateur de statusLine
  remplacent les entrées `Chronos-v3.1.0.exe` par `Chronos-v3.2.0.exe` (8 groupes de hooks + statusLine), sans
  doublon, sans toucher aux groupes tiers (`gsd-*`). Constaté dans le fichier, pas supposé. **C'est
  l'utilisateur qui lance l'exe** (l'agent ne lance pas l'overlay : précédent 19-05).
- L'ancien processus `Chronos-v3.1.0.exe` (PID 40772, démarré le 23.09) doit être fermé par l'utilisateur
  avant de lancer la 3.2.0 (mono-instance ? à vérifier dans `App.xaml.cs`), et l'autostart `shell:startup`
  doit pointer le nouvel exe (vérifier le raccourci).
- Commit `release: Chronos 3.2.0 - milestone v1.7 << Lue ou non lue >>` avec le compte de tests et la taille
  de l'exe ; tag local `milestone-v1.7` à la clôture du milestone (pas de push sans accord).

#### Le constat en production (critère 4) — point de contrôle humain
- Protocole écrit avant d'être joué, trois lignes :
  1. lancer un tour dans une session ⇒ le widget affiche « Réflexion » ;
  2. laisser un tour se terminer dans une session NON regardée (autre session au premier plan) ⇒
     « En attente » ; l'ouvrir ⇒ elle disparaît sans clic ;
  3. regarder un tour se terminer ⇒ la session n'apparaît jamais « En attente ».
  Plus : un titre de session à la place du dossier ; une question posée (AskUserQuestion ou `blocked`) ⇒
  « En attente » au premier rang.
- Résultat consigné dans `31-CONSTAT.md`, écarts compris (y compris la réserve 16:23 → 19:51 de la phase 27
  si elle se reproduit). Le milestone ne se clôt pas sans ce fichier.

### Claude's Discretion (copiée telle quelle)
- Découpage en plans (documentation | release | constat), ordre des tâches de documentation.

### Deferred Ideas — OUT OF SCOPE (copiées telles quelles)
- Signature de l'exe (SmartScreen) — hors périmètre depuis v1.0.
- Push du tag et release GitHub — sur demande explicite de l'utilisateur.

### Specific Ideas (rappel)
- Le tableau de l'utilisateur, tel quel, comme grille du constat :
  « session qui réfléchit → Réflexion ; finie ou question, non lue → En attente ; finie et lue → rien ».

**Trois prémisses du CONTEXT démenties par le relevé du jour (à traiter, pas à contourner en silence) :**
« le `.gitignore` ignore les exe » (faux, fait n° 1) ; « jonction MSIX » (faux, fait n° 5) ; « l'autostart
`shell:startup` doit pointer le nouvel exe » (aucun raccourci n'existe, fait n° 3). Et une prémisse ambiguë :
« mono-instance ? » — il n'y en a pas (fait n° 2).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description (REQUIREMENTS.md) | Ce que la recherche apporte |
|----|-------------------------------|-----------------------------|
| VAL-02 | La source app-bureau est documentée dans `docs/desktop-app-sessions.md` (chemin, jonction, champs lus, ce qui n'est PAS garanti), au même titre que `docs/hooks-contract.md`. | Q4 : plan du document section par section, contenu exact à citer (14 noms de champs, 3 catégories, racines, 24 h, virtualisation ≠ jonction, réserves datées) ; mécanisme de garde croisée par extraction du texte de `LecteurAppBureau.Interpreter` (ensemble égal dans les deux sens), sur le modèle lu de `ContratHooksDocumenteTests` ; Q5 : renvois dans `hooks-contract.md` §3, `data-sources.md`, README, et extension de `LibellesSessionsTests` |
| VAL-03 | L'exe est publié en **3.2.0** — version embarquée et dans le nom du fichier (`Chronos-v3.2.0.exe`) — et les hooks et la statusLine sont réconciliés vers ce nouvel exe au premier lancement, comme en v1.6. | Q1 : procédure pas à pas avec contrôles mesurables (taille, 0 DLL, `VersionInfo`, md5, code de sortie) ; Q2 : ce que fait exactement `ClaudeSettingsReconciler` au changement de nom, et le contrôle Python qui le constate ; Q3 : arrêt de la 3.1.0, lancement par l'Explorateur, autostart ; test de cohérence des quatre propriétés du csproj |
| Critère 4 | Constat en production, consigné dans `31-CONSTAT.md` | Q6 : protocole ligne par ligne (gestes de l'utilisateur, relevés de l'agent en lecture seule), liste consolidée des 12 vérifications déférées par 28/29/30, format du fichier |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- C# / .NET 8 (`net8.0-windows`) / WPF / MVVM (CommunityToolkit.Mvvm) + Microsoft.Extensions.DependencyInjection/Hosting ;
  **aucune dépendance ajoutée** (la phase n'en requiert aucune ; `System.Xml.Linq` et `System.Text.RegularExpressions`
  sont dans le framework).
- Rendu XAML pur, aucune dépendance native ; exe self-contained mono-fichier win-x64, `PublishTrimmed=false`,
  propriétés de publication **conditionnées** (`'$(PublishSingleFile)' == 'true'`), autostart `shell:startup`, pas de
  ClickOnce.
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin. `Assembly.Location` interdit (vide en
  mono-fichier) : `Environment.ProcessPath` / chemins injectés par MSBuild dans les tests.
- Ne jamais présenter une estimation comme exacte ; « observé, jamais déduit » ; une source non documentée se lit avec
  tolérance et se dégrade vers « je ne sais pas ».
- UI et commentaires **en français** ; documents en français.
- GSD : toute modification passe par un plan de phase ; commits de documentation par l'outil GSD.
- **Mémoire projet :** version embarquée dans l'exe ET dans le nom du fichier publié, à chaque release ; pas de push de
  tag sans accord explicite.
- **Précédents du projet :** l'agent ne lance ni n'arrête l'overlay (19-05, 27, 29-05, 30) ; aucune écriture dans
  `%APPDATA%\Claude`, `%APPDATA%\Chronos` ni `~/.claude/settings.json` hors du geste de l'utilisateur ; aucun titre ni
  motif de session recopié dans un fichier versionné, profil masqué en `%USERPROFILE%` (29-05).

## Summary

La phase est à 80 % de la procédure et à 20 % du code. Le code tient en trois ajouts testés : une classe de garde
croisée (`ContratAppBureauDocumenteTests`, ~6 cas), un test de cohérence des quatre propriétés de version du csproj
(~2 cas) et, recommandé, une ligne `Version : …` dans l'en-tête du diagnostic (~1 cas). Tout le reste est écriture
(`docs/desktop-app-sessions.md`, renvois dans trois documents, section README) et procédure (publier, faire lancer,
constater). La garde la moins fragile ne compare pas le document à une liste recopiée dans un test : elle **extrait
du texte de `LecteurAppBureau.cs` les noms passés aux lecteurs de champ** (`Texte`, `Instant`, `Entier`, `Booleen`,
`TryGetProperty`) et exige l'égalité d'ensembles avec la table balisée du document — un champ ajouté au code sans
document, ou documenté sans être lu, la fait rougir.

La réconciliation est déjà livrée et testée (v1.5-v1.6) : un changement de NOM d'exe est précisément le cas pour
lequel elle a été écrite (repérage par marqueur `--hook`/`--statusline` + nom `Chronos*.exe`, jamais par chemin). Au
premier lancement en mode overlay, avec `SessionsWidgetEnabled = true` (vrai dans le `settings.json` RÉEL relevé ce
jour), elle purge les 8 groupes 3.1.0, en pose 8 neufs en fin de tableau (même position qu'aujourd'hui), repointe la
statusLine, laisse intacts les 3 groupes `gsd-*`, et sauvegarde le fichier dans le vrai `%APPDATA%\Chronos\backups`
AVANT d'écrire. Le seul travail de la phase est de le **constater** : sauvegarde neuve dont le md5 égale celui relevé
avant le lancement, et fichier courant égal à la sauvegarde où `Chronos-v3.1.0.exe` devient `Chronos-v3.2.0.exe`.

Le constat est un point de contrôle humain en trois temps (lancer, trois lignes, compléments), où l'utilisateur fait
les gestes et lit l'écran, et où l'agent relève en lecture seule, dans la vue réelle : `treated.json`, la sauvegarde,
`chronos.log`, les fichiers d'état du paquet, `lastFocusedAt` des fichiers de l'app, l'instant `end_turn` du
transcript. Douze vérifications manuelles déférées par les phases 28, 29 et 30 s'y greffent ; elles sont consolidées
en Q6 avec leur geste et leur attendu.

**Primary recommendation :** trois plans séquentiels — 31-01 documentation + gardes (autonome, TDD), 31-02 version
au diagnostic + bump + publication + smoke + commit de release (autonome, s'arrête AVANT tout lancement), 31-03
réconciliation et constat (`autonomous: false`, trois points de contrôle humains, livrable `31-CONSTAT.md`).

---

## Q1 — La release 3.2.0 : procédure exacte et vérifiable

### 1.a Ce que la 3.1.0 a fait (commit `cd26b31`, 2026-09-13 02:13)

`git show --stat cd26b31` : **un seul fichier**, `src/Chronos/Chronos.csproj`, 4 lignes. Message : « Version portée dans
les 4 propriétés du csproj et dans le nom du fichier publié (Chronos-v3.1.0.exe). Publication vérifiée : exe
self-contained mono-fichier, 77 Mo (< 120), 0 DLL à côté. Smoke test en mode --hook avec stdin vide : code 0, aucune
écriture, settings.json inchangé (md5 identique avant/après). 896 tests / 0 échec. »

### 1.b L'état relevé ce jour (lecture seule)

| Élément | Valeur |
|---|---|
| `Chronos.csproj` l. 16-19 | `Version` 3.1.0 · `FileVersion` 3.1.0.0 · `AssemblyVersion` 3.1.0.0 · `InformationalVersion` 3.1.0 ; l. 20 `IncludeSourceRevisionInInformationalVersion=false` (sinon le SDK ajoute `+<sha>` à la version produit) ; l. 29-38 propriétés de publication conditionnées |
| `Properties/PublishProfiles/win-x64.pubxml` | miroir des propriétés (alternative `-p:PublishProfile=win-x64`) |
| Dernière sortie de publication | `src/Chronos/bin/Release/net8.0-windows/win-x64/publish/` : `Chronos.exe` **77 218 013 o** + `Chronos.pdb` 167 740 o, 2026-09-13 02:12 — **aucune DLL** |
| `Chronos-v3.1.0.exe` (racine) | 77 218 013 o, md5 `ab93b3270abdec55cec42ac710552661`, `VersionInfo` : FileVersion 3.1.0.0, ProductVersion 3.1.0, ProductName Chronos |
| `.gitignore` | `bin/ obj/ .vs/ *.user … publish/ *.pubxml.user .claude/` — **aucune règle `*.exe`** ; `git status` : `?? Chronos-v3.1.0.exe` |
| SDK / packs | SDK 10.0.201 ; en cache NuGet : `microsoft.netcore.app.runtime.win-x64` 8.0.25, `microsoft.windowsdesktop.app.runtime.win-x64` 8.0.25, `microsoft.netcore.app.crossgen2.win-x64` 8.0.25 (ReadyToRun) — publication possible hors ligne |
| Extraction native (mono-fichier) | `%LOCALAPPDATA%\Temp\.net\Chronos-v3.1.0\…` existe (vue réelle) : le dossier porte le NOM de l'exe ; la 3.2.0 créera `…\.net\Chronos-v3.2.0\…` à son premier lancement (écriture attendue, hors périmètre des « aucune écriture ») |
| Étiquettes git | `milestone-v1.6` (annotée, sur `bbf3cca` = commit de clôture de milestone, pas le commit de release) ; **aucune** étiquette `v3.x` ; `origin` = GitHub |

### 1.c La procédure, avec un contrôle mesurable par étape (agent, sans lancer l'overlay)

| # | Étape | Contrôle (attendu) |
|---|---|---|
| 0 | Précondition : phase 30 close (ROADMAP 4/4, `30-VERIFICATION.md` passé), plans 31-01 et (s'il existe) la tâche « version au diagnostic » commités | `git status --short -- src tests docs` vide |
| 1 | Suite complète AVANT bump, deux fois | `dotnet test Chronos.sln -c Debug --nologo -v q` : 0 échec, total = attendu |
| 2 | Bump des quatre propriétés (l. 16-19), rien d'autre | `grep -c "3.2.0" src/Chronos/Chronos.csproj` = 4 ; `grep -c "3.1.0" …` = 0 ; test de cohérence (Validation) vert |
| 3 | Suite complète après bump, deux fois | 0 échec (le test de cohérence lit le csproj ET les attributs de l'assembly compilée) |
| 4 | Relevé AVANT publication | md5 + date de `~/.claude/settings.json` ; nombre de fichiers dans la racine d'état du paquet `…\LocalCache\Roaming\Chronos\sessions` |
| 5 | `dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true` | code 0 ; `publish/Chronos.exe` daté de maintenant ; `ls publish/*.dll` vide ; taille ≈ 77 Mo (< 120 Mo, garde-fou de `docs/publish.md`) |
| 6 | Vérifier que c'est bien la NOUVELLE sortie AVANT de copier | `(Get-Item publish\Chronos.exe).VersionInfo` : FileVersion **3.2.0.0**, ProductVersion **3.2.0** ; md5 ≠ `ab93b327…` |
| 7 | `cp …/publish/Chronos.exe "Chronos-v3.2.0.exe"` (racine) | md5 de la copie = md5 de `publish/Chronos.exe` ; `git status` ne montre PAS l'exe (après ajout au `.gitignore`) |
| 8 | Smoke : `./Chronos-v3.2.0.exe --hook SessionStart < /dev/null ; echo "code $?"` (Bash : il attend la fin d'un exe GUI et rend son code) | `code 0` ; md5 de `settings.json` inchangé ; racine d'état du paquet : même nombre de fichiers ; aucun nouveau processus `Chronos-v3.2.0.exe` résident |
| 9 | Commit de release (csproj + `.gitignore` [+ docs/publish.md si mis à jour]), chemins explicites | message `release: Chronos 3.2.0 - milestone v1.7 << Lue ou non lue >>` + compte de tests + taille ; **pas d'étiquette** (voir Open Questions n° 4) |

**Pourquoi le smoke `--hook` avec stdin vide est sûr, lu dans le code :** `App.OnStartup` sort par `Environment.Exit`
avant toute construction du Host quand `--hook` est présent ; `SessionHookProcessor.Process` rend `Ignored` sans
`session_id` ; `EcritureEtatSession.Appliquer` rend `Reussie` sur `Ignore` **avant** son `Directory.CreateDirectory`.
Lancé depuis une session Claude Code, le processus voit la vue VIRTUALISÉE d'AppData — sans conséquence ici puisqu'il
n'écrit rien ; il ne prouve donc rien de la réconciliation (qui n'a lieu qu'en mode overlay). **Ne jamais lancer l'exe
sans argument ni avec `--statusline`** depuis l'agent : le premier ouvre un overlay dans la vue virtualisée (il lirait
les réglages du paquet, datés du 13.09, et écrirait `treated.json` là) ; le second exécute la barre chaînée
(`InnerStatusLineCommand`) avec des effets de bord.

`docs/publish.md` §6 est périmé sur trois points (« 106/106 verts » ; smoke par lancement de l'overlay 8 s, contraire
au précédent 19-05 ; aucune mention du nom versionné ni de la vérification de réconciliation). Le mettre à jour est
recommandé (Q5), pas exigé.

---

## Q2 — La réconciliation au premier lancement

### 2.a Le chemin d'exécution (lu dans `App.xaml.cs`, par contenu)

Mode overlay uniquement (`--statusline`, `--hook`, `--cadrans`, `--sessions` court-circuitent avant le Host) :
`StartAsync` → `DiagnosticService.LogStartupAsync()` **sans attente** (écrit `%APPDATA%\Chronos\chronos.log`) →
`window.Show()` → `ClaudeSettingsReconciler.Reconcile(settings.SessionsWidgetEnabled)` dans un `try/catch` muet →
`ArchiveStore.PurgerPrefixe("desktop:")` → `BalayageMagasinSessions.Balayer()` → `OfferOnFirstRun()` →
`ShowIfEnabled()`.

`settings` est `ChronosSettings` chargé par `SettingsService` depuis `ChronosPaths.Default()` =
`%APPDATA%\Chronos\settings.json`. Lancé par l'Explorateur, l'overlay voit la vue **réelle** : relevé ce jour par
Python (vue réelle prouvée) → `SessionsWidgetEnabled = True` (fichier du 2026-09-26 09:51:12, 577 o). La vue du paquet
a un autre `settings.json` (560 o, 13.09) — sans effet sur l'overlay. **Donc `hooksWanted = true` : les hooks seront
repointés, pas purgés.** À re-vérifier juste avant le lancement : un widget désactivé entre-temps ferait PURGER les 8
groupes.

### 2.b Ce que fait `ReconcileJson` (cœur pur) sur le fichier de ce jour

`~/.claude/settings.json` relevé (hors AppData, donc identique dans les deux vues) : md5
`9eab8a8ecdb7f1d541841dd1c6ea418b`, 3 384 o, 2026-09-25 11:07:56 ; clés racine `hooks`, `statusLine`,
`agentPushNotifEnabled`, `skipWorkflowUsageWarning`.

| Clé `hooks` | Groupes aujourd'hui (ordre) | Après réconciliation |
|---|---|---|
| `SessionStart` | `gsd-check-update.js` · Chronos 3.1.0 | `gsd-check-update.js` · Chronos **3.2.0** |
| `PostToolUse` | `gsd-context-monitor.js` (matcher `Bash\|Edit\|Write\|MultiEdit\|Agent\|Task`, timeout 10) · Chronos 3.1.0 (timeout 3) | idem · Chronos **3.2.0** (timeout 3) |
| `PreToolUse` | `gsd-prompt-guard.js` (matcher `Write\|Edit`, timeout 5) · Chronos 3.1.0 (timeout 3) | idem · Chronos **3.2.0** |
| `UserPromptSubmit`, `Stop`, `SessionEnd`, `PermissionRequest` | Chronos 3.1.0 | Chronos **3.2.0** |
| `Notification` | Chronos 3.1.0, matcher `agent_needs_input\|elicitation_dialog\|elicitation_url_dialog` | idem, **3.2.0** |
| `statusLine.command` | `"…/PROJET OVERLAY/Chronos-v3.1.0.exe" --statusline` | `"…/PROJET OVERLAY/Chronos-v3.2.0.exe" --statusline` |

Mécanique (lue) : `ApplyHooks` purge **toutes** les clés des groupes dont une commande porte `--hook` ET un exécutable
nommé `Chronos*.exe` (parcours descendant), puis ajoute un groupe neuf par entrée de `Cablage` **en fin de tableau** —
la position d'aujourd'hui, d'où une égalité structurelle exacte ; une clé purgée et redevenue non vide reste à sa
place. `ApplyStatusLine` ne repointe que si la barre est à Chronos et ne pointe pas déjà l'exe courant (jamais
d'installation). La comparaison avant/après se fait sur la forme **normalisée** : si le fichier est déjà conforme,
rien n'est écrit ni sauvegardé.

Couche E/S (`Reconcile`) : fichier absent ⇒ rien (jamais de création) ; exe = `Environment.ProcessPath` ; si une
écriture est nécessaire, **copie octet pour octet** vers `%APPDATA%\Chronos\backups\claude-settings-yyyyMMdd-HHmmss.json`
(heure locale, suffixe `-1`… en collision), rétention 5, puis écriture atomique `.tmp-<pid>` → renommage. Tout échec
(verrou, JSON inexploitable, sauvegarde impossible) ⇒ **rien écrit, rien dit**. D'où la règle : constater dans le
fichier.

Dossier `backups` réel aujourd'hui : **un seul** fichier, `claude-settings-20260923-110933.json` (962 o — le premier
lancement de la 3.1.0 le 23.09 à 11:09:33). Le lancement de la 3.2.0 en créera un second.

### 2.c Ce qui garantit « sans doublon » et « groupes tiers intacts »

Déjà sous test (`ClaudeSettingsReconcilerTests`, lu) : `Purge_la_fixture_reelle_de_25_groupes_a_un_seul_par_evenement_cable`,
`Preserve_les_trois_hooks_GSD_avec_leur_matcher_et_leur_timeout`,
`Les_battements_n_evincent_pas_les_hooks_d_un_autre_outil_sur_PreToolUse_et_PostToolUse`,
`Repointe_la_statusLine_perimee_et_conserve_padding`, `Est_un_point_fixe_la_seconde_passe_ne_produit_rien`,
`La_retention_ne_garde_que_cinq_sauvegardes`. **Aucun test à ajouter** : le changement de nom n'est pas un cas neuf.
Le code de ces fichiers n'a pas bougé depuis `cd26b31` (`git diff --stat cd26b31..HEAD` sur `ClaudeSettings*`,
`StatusLine*`, `SessionHookProcessor`, `EcritureEtatSession`, `CatalogueEvenementsHooks` : seul
`SessionHookInstaller.cs`, 7 lignes, les libellés de rôle de `bf63366`).

**Conséquence utile :** le mode `--hook` de la 3.1.0 et celui de la 3.2.0 écrivent le MÊME format d'état. Les sessions
ouvertes avant la réconciliation continuent d'appeler `Chronos-v3.1.0.exe` (la configuration des hooks est lue au
démarrage d'une session) : c'est sans effet sur le constat, **à condition que `Chronos-v3.1.0.exe` reste sur le
disque** jusqu'à ce que ces sessions soient fermées (sinon chaque événement échoue en erreur non bloquante et la barre
de statut casse).

### 2.d Comment le constater (agent, lecture seule, vue réelle)

Voir Code Examples « Contrôle de la réconciliation ». Quatre assertions : (1) une sauvegarde neuve existe et son md5
= md5 relevé juste avant le lancement (personne d'autre n'a touché le fichier entre-temps) ; (2) le fichier courant
est structurellement égal à la sauvegarde où `Chronos-v3.1.0.exe` devient `Chronos-v3.2.0.exe` ; (3) comptes : 8
commandes `Chronos-v3.2.0.exe" --hook`, 1 `--statusline` 3.2.0, 0 occurrence de 3.1.0, 3 commandes `gsd-` ; (4) un
seul groupe Chronos par clé câblée.

---

## Q3 — Mono-instance, arrêt de la 3.1.0, autostart

| Question | Réponse (relevé du jour, lecture seule) |
|---|---|
| Mono-instance ? | **Non.** `grep -rn "Mutex\|GetProcessesByName\|SingleInstance" src/Chronos` : une seule occurrence, dans un commentaire de `ChronosTokenAuthority.cs`. Deux overlays cohabitent sans rien dire. |
| L'overlay en marche | `Chronos-v3.1.0.exe` PID **40772**, ligne de commande sans argument, parent **explorer.exe** (PID 27872), créé le 23.09.2026 **11:09:31**. D'autres PID `Chronos-v3.1.0.exe` apparaissent et disparaissent : vraisemblablement les invocations `--hook`/`--statusline` des sessions (PID 131476 créé à 11:01:48, disparu au relevé suivant ; ligne de commande non captée). |
| Comment le fermer | réglages (clic droit sur le cadran → fenêtre de réglages) → bouton **« Quitter Chronos »**. Constat agent : plus aucun processus `Chronos-v3.1.0.exe` **sans** `--hook`/`--statusline` dans sa ligne de commande (`Get-CimInstance Win32_Process`). |
| Autostart | **Aucun.** Dossier Démarrage réel : `desktop.ini` seul (Python) ; `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` : aucune entrée Chronos ; aucune tâche planifiée Chronos. Rien ne relancerait la 3.1.0 au redémarrage — ni ne réécrirait `settings.json` vers la 3.1.0 (ce qu'un raccourci périmé ferait : la réconciliation de la 3.1.0 repointerait tout vers elle au boot). |
| Réglage « Lancer au démarrage » | bascule dans les réglages ; `AutostartService` crée `Chronos.lnk` dans `Environment.GetFolderPath(Startup)`, cible `Environment.ProcessPath`, `IsEnabled` = le fichier existe. L'activer DEPUIS la 3.2.0 donne un raccourci vers `Chronos-v3.2.0.exe`. C'est une décision de l'utilisateur (point de contrôle) ; si elle est prise, constater la cible (Code Examples). |
| Où lancer la 3.2.0 | **Double-clic dans l'Explorateur** (ou Win+R). Jamais depuis un terminal ouvert dans l'app Claude ni depuis une session : ce processus hériterait de la vue virtualisée d'AppData (autre `settings.json`, autre `treated.json`). |
| SmartScreen | Un exe produit localement ne porte pas de marque « provenance Internet » : l'alerte est peu probable (MEDIUM) ; si elle apparaît, « Informations complémentaires → Exécuter quand même ». |

---

## Q4 — `docs/desktop-app-sessions.md` et sa garde croisée

### 4.a Ce que le lecteur lit réellement (`LecteurAppBureau.Interpreter`, fichier NON touché par 30-03/30-04)

Quatorze noms, dans l'ordre du code :

| Champ JSON | Lu par | Compté dans `ChampsAbsents` ? | Ce que Chronos en fait (consommateurs relevés par `grep` des propriétés de `MetadonneesAppBureau`) |
|---|---|---|---|
| `lastFocusedAt` | `Instant` (lu AVANT l'identifiant, pour la sélection) | oui (2e lecture) | règle « lue » : `SessionTreatmentTracker` (LUE-01) ; sélection = max sur TOUS les fichiers servis, sans identifiant compris (LUE-02) ; diagnostic |
| `cliSessionId` | `Texte` | non — absent ⇒ fichier « sans cliSessionId » | clé de jointure (sans casse) avec les hooks et les transcripts |
| `title` | `Texte` | oui | nom affiché (`AffichageSessions.Nom`, APP-02), dossier en repli et en info-bulle ; diagnostic |
| `titleSource` | `Texte` | oui | lu, non exploité (valeur relevée : `auto`) |
| `cwd` | `Texte` | oui | lu, non exploité |
| `createdAt` | `Instant` | oui | lu, non exploité |
| `lastActivityAt` | `Instant` | oui | départage des doublons ; instant d'une classification (figé par épisode) |
| `latestUserFrameAt` | `Instant` | oui | lu, non exploité (absent sur 5 des 18 sessions récentes, 29-SONDE-APP06) |
| `completedTurns` | `Entier` | oui | lu, non exploité |
| `isArchived` | `Booleen` | oui | une session archivée dans l'app ne dépose pas de question (`SessionMonitor`) ; diagnostic |
| `postTurnSummary` | `TryGetProperty` (objet) | non (transitoire, absence normale) | conteneur de la classification |
| `postTurnSummary.status_category` | `Texte(resume, …)` | non | `completed` → Terminée, `blocked` → Bloquée, `review_ready` → Prête à revue, **tout autre** → Inconnue (catégorie brute au diagnostic) |
| `postTurnSummary.needs_action` | `Texte(resume, …)` | non | motif de la question ; `blocked` + motif non vide = `WaitingAttention`, source `AppBureau` (APP-03) |
| `postTurnSummaryFor` | `Texte` | non | clé d'épisode : l'instant de la classification est figé à sa première apparition (TRT-02) |

Horodatages : epochs en **millisecondes**, convertis par le point unique `UsageNormalization.InstantDepuisEpochMillisecondes`
(plancher de sanité ⇒ nul et « absent »). Les consommateurs peuvent encore bouger en 30-03/30-04 (`SessionMonitor`,
`DiagnosticService`) : **le plan relit ces usages sur l'état final**, sans numéros de ligne.

### 4.b Où, et comment (à citer)

- Racines candidates, résolues UNE fois par `RacinesEtat.Candidats` (seul porteur du chemin dans `src/`, garde APP-05
  n° 2) : chaque dossier `%LOCALAPPDATA%\Packages\Claude_*` en ordre ordinal →
  `…\LocalCache\Roaming\Claude\claude-code-sessions`, puis `%APPDATA%\Claude\claude-code-sessions` ; **la première qui
  existe** est lue (`RacinesEtat.PremiereExistante`), à chaque cycle. Chemins par `Path.Combine`.
- **Ce n'est pas une jonction** : c'est la virtualisation d'AppData du paquet MSIX (erratum de `27-RELEVE.md`, sonde
  `29-SONDE-HORS-ARBRE.txt`). Vu de l'overlay, `%APPDATA%\Claude` n'existe pas ; vu d'une session Claude Code, il
  existe et montre les mêmes fichiers que le cache du paquet.
- Profondeur exacte `<org>\<user>\local_*.json` (les temporaires `.local_*` sont exclus ; jamais de motif récursif) ;
  fenêtre de lecture `HorizonsSessions.LectureAppBureau` = **24 h** sur la date d'écriture ; relecture seulement si
  (date d'écriture, taille) change ; plafond 16 Mo.
- Ouverture unique `FileMode.Open`, `FileAccess.Read`, `FileShare.ReadWrite | FileShare.Delete`, poignée tenue le temps
  de COPIER les octets (0,36 ms médian pour 289 Ko), analyse après fermeture — parce que l'app réécrit par temporaire +
  renommage (3 essais, 50 puis 100 ms) et se replie sur une écriture EN PLACE si le renommage échoue (lu dans
  `app.asar` 2.9939.2.0, MEDIUM : code minifié).
- Lecture seule stricte (APP-05) : trois gardes (texte du lecteur, comportement, chemin en un seul lieu sans jeton
  d'écriture), falsifiées par les mutations (a), (b), (c) de la phase 29.

### 4.c Ce qui n'est PAS garanti (le cœur du document)

| Trou | Fait daté | Conséquence livrée |
|---|---|---|
| Format interne non documenté | aucun document de l'éditeur ; relevé sur disque le 2026-09-25, app **2.9939.2.0** ; noms confirmés présents ce jour-là sur les fichiers réels | champ absent, renommé ou d'un autre type ⇒ nul + « absent » au diagnostic ⇒ la session garde le comportement v1.6 (LUE-04) |
| Réécriture intégrale | ~275 Ko réécrits EN ENTIER au changement de session et en fin de tour ; nouvel index NTFS à chaque réécriture (2/2) ; au redémarrage de l'app (16:58), TOUS les fichiers réécrits | la date d'écriture ne dit rien de l'âge d'une session (Piège 5 de 29) : la fenêtre de 24 h n'est qu'une économie |
| `postTurnSummary` transitoire | `blocked`/`review_ready` lus à 16:08 disparus à 16:58 (redémarrage) ; absent après 4-5 tours sur la session du relevé ; effacé par l'app au début du tour suivant ; catégories `need_input`, `failed` connues du code de l'app, jamais vues sur disque (0/138) | un indice daté, jamais un état ; seul `blocked` + motif dépose un signal |
| `lastActivityAt` bouge après la fin du tour | +3 min 10 s mesurés (activité d'agents en arrière-plan) | l'instant d'une classification est figé à la première apparition de `postTurnSummaryFor` |
| `lastFocusedAt` : ce qui le met à jour | re-sélection (16:05:46 → 16:23:51) ; **alt-tab** sans changer de session (16:23:51 → 20:30:44, geste A) ; **PAS** la fin d'un tour regardé (reste 20:30:44 < fin 20:34:04, geste B) | LUE-02 (premier plan `claude` + grâce 2,5 s) couvre le tour regardé |
| **Réserve 16:23 → 19:51** | `lastFocusedAt` resté à 16:23:51 pendant 3 h 28 alors que l'utilisateur est revenu écrire à 19:51 (retour après limite d'usage) ; non reproduit par le geste A | à observer au constat, sans en faire une règle |
| Latence d'écriture de `lastFocusedAt` | un seul point : focus 20:30:44.976, fichier écrit 20:31:09 (24 s, ambigu) | mesurée au constat (Q6, V9) |
| Le premier plan ne dit pas la VUE | le processus `claude` au premier plan ne distingue pas une session de code, l'accueil ou une conversation Chat (titre de fenêtre « Claude ») ; sans UI Automation, rien ne les sépare | limite ÉCRITE de LUE-02 ; constatée en Q6 (V6), jamais « corrigée » sans décision |
| Deux vues d'AppData | un relevé fait depuis une session Claude Code voit la vue virtualisée : il ne vaut pas pour l'overlay | tout re-relevé se fait hors de l'arbre de l'app (Python par alias, ou WMI) |
| Sélection au-delà de 24 h | un fichier écrit il y a plus de 24 h n'est pas ouvert, donc ne peut pas être sélectionné | limite LOW écrite dans le lecteur |

### 4.d Structure recommandée (calquée sur `hooks-contract.md`)

```
# Chronos — La source app-bureau (métadonnées de session de l'app bureau Claude)
> Relevé le 2026-09-25 — app bureau Claude 2.9939.2.0 (paquet MSIX), processus `claude.exe`.
> ⚠ Limite de la source : format interne NON documenté ; tout provient du disque (et, MEDIUM, du code minifié).
(pourquoi ce document existe ; pendant de hooks-contract.md et data-sources.md ; rappel « deux vues d'AppData »)
## 1. Où Chronos lit                       — racines, ordre, première qui existe, « ce n'est pas une jonction »
## 2. Les champs lus                        — table balisée <!-- CHAMPS-LUS:debut/fin --> + les trois catégories
## 3. Ce que Chronos en produit              — titre ; dernier focus et sélection (la RÈGLE : renvoi à hooks-contract §3) ;
                                               question `blocked` ; archivée ; section « Source app-bureau » du diagnostic
## 4. Lire sans gêner l'écrivain             — partage, poignée courte, cache, 24 h, lecture seule et ses gardes
## 5. CE QUI N'EST PAS GARANTI               — 4.c ci-dessus, une sous-section par trou, chacune DATÉE
## 6. Le relevé daté                         — 138 fichiers, 18 < 24 h, 23/138 sans cliSessionId, 127/138 sans résumé,
                                               latestUserFrameAt absent 5/18, coûts (Lire chaud 0,50 ms, froid 346 ms)
## 7. Re-relever et détecter une dérive      — procédure en lecture seule hors de l'arbre ; ce qu'une machine garantit
                                               (la garde croisée) et ce qu'aucune ne garantit (dérive de l'app)
```

**La règle « lue » reste écrite en un seul endroit** : `hooks-contract.md` §3 (réécrit par 30-04, décision D-30-12,
avec sa garde `GraceLecture`). Le nouveau document décrit la SOURCE et renvoie à la règle ; le §3 renvoie au nouveau
document. Deux copies de la règle dériveraient.

Aucun titre ni motif réel dans le document (valeurs anonymisées comme les fixtures) ; `%USERPROFILE%` à la place du
profil. Le nom de famille du paquet peut y figurer (`Claude_pzs8sxrjxfjjc`, déjà dans `29-SONDE-APP06.txt`) mais le
document ne doit pas le présenter comme fixe : `RacinesEtat` énumère `Claude_*` précisément parce que le suffixe est
un hachage d'éditeur.

### 4.e La garde croisée : le mécanisme le moins fragile

**Modèle lu (`ContratHooksDocumenteTests`)** : chemin de `docs/` par `AssemblyMetadata("CheminDocsChronos")` (injecté
par le csproj de tests, jamais deviné) ; `LireDocument()` anti-muet (attribut présent, fichier présent) ;
`TableEntre(texte, marqueurDebut, marqueurFin)` lit les lignes d'une table Markdown balisée par deux commentaires HTML
(barre échappée `\|` protégée, ligne de séparation ignorée, au moins un en-tête et une ligne, backticks retirés) ;
`SectionDe(texte, "## N.")` découpe une section ; égalité d'ensembles dans les deux sens + égalité des comptes (pas de
doublon) ; les gardes croisées lisent le code par `GardesPerimetreTests.CheminSources()` (`internal static`) et y
cherchent le fragment qui porte la règle.

**Trois options pour « les champs cités = les champs lus » :**

| Option | Détecte un champ lu non documenté ? | Détecte un champ documenté non lu ? | Fragilité |
|---|---|---|---|
| A. Liste recopiée dans le test | non (le test et le code dérivent ensemble… ou pas) | non | décorative — **rejetée** |
| B. Comportement : retirer chaque champ d'une fixture réelle et voir `ChampsAbsents` | non | oui (pour les 9 champs comptés) | robuste au refactoring, aveugle aux ajouts |
| **C. Extraction du texte de `LecteurAppBureau.cs`** | **oui** | **oui** | rougit si la FORME des appels change — bruyamment, jamais en silence |

**Recommandation : C**, avec anti-muet. Motifs (sur le texte du fichier) :
`\b(Texte|Instant|Entier|Booleen)\((racine|resume),\s*"([A-Za-z_]+)"` (préfixer `postTurnSummary.` quand le premier
argument est `resume`) et `TryGetProperty\("([A-Za-z_]+)"` (les helpers internes appellent `TryGetProperty(champ, …)`
sans littéral : non capturés). Ensemble attendu : les **14** noms de 4.a. Anti-muet : l'ensemble extrait contient
`cliSessionId` ET `lastFocusedAt` et compte au moins 12 noms, sinon message « la garde ne sait plus lire le lecteur :
adapter la garde, pas la désarmer ». Même principe pour les catégories : `"([a-z_]+)"\s*=>\s*ClassificationFinDeTour\.`
⇒ `{completed, blocked, review_ready}` égal à la table (ou liste balisée) du document.

**Cas proposés (`ContratAppBureauDocumenteTests`, nouvelle classe — la classe des hooks n'est pas touchée) :**

| Id | Cas | Croise |
|---|---|---|
| D1 | `Le_chemin_du_document_est_injecte_et_le_fichier_existe` | attribut `CheminDocsChronos`, `docs/desktop-app-sessions.md` |
| D2 | `La_table_documentee_liste_EXACTEMENT_les_champs_lus_par_le_lecteur` | table `CHAMPS-LUS` ↔ extraction C ; comptes égaux |
| D3 | `Les_categories_documentees_sont_celles_que_le_lecteur_reconnait` | 3 catégories ↔ bras du `switch` |
| D4 | `Le_document_dit_ou_il_lit_et_le_resolveur_construit_ce_chemin` | doc contient `LocalCache\Roaming\Claude\claude-code-sessions`, « n'est pas une jonction », `` `HorizonsSessions.LectureAppBureau` `` ; `RacinesEtat.cs` contient `"LocalCache", "Roaming", "Claude", "claude-code-sessions"` ; `Assert.Equal(TimeSpan.FromHours(24), HorizonsSessions.LectureAppBureau)` |
| D5 | `Le_document_porte_ce_qui_n_est_pas_garanti_avec_sa_date` | date `2026-09-25`, version `2.9939.2.0`, section « ## 5. » d'au moins N lignes, marqueurs d'une ligne chacun : « non documenté », « réécrit », « transitoire », « alt-tab », « fin d'un tour », « 16:23:51 », « accueil » |
| D6 | `Les_documents_voisins_renvoient_au_contrat_de_l_app` | `hooks-contract.md` section « ## 3. », `data-sources.md` et `README.md` contiennent `desktop-app-sessions.md` |

Chaque fragment cherché tient sur UNE ligne du document, sans mise en forme au milieu (la garde lit le texte brut —
même consigne que les gardes du §3). `TableEntre`/`SectionDe` sont `private` dans `ContratHooksDocumenteTests` : les
passer `internal static` et les appeler depuis la nouvelle classe (aucune assertion existante touchée) plutôt que les
recopier. README : `Path.Combine(CheminDocs(), "..", "README.md")` (dérivé du chemin injecté, pas deviné).

**Mutations de falsification (projet : jouées, rouges nommés, révoquées, sha256 identique) :** (d1) renommer le
littéral `"titleSource"` en `"titleOrigin"` dans `LecteurAppBureau.cs` ⇒ D2 rouge (dans les deux sens) ; (d2) retirer
la ligne `isArchived` de la table du document ⇒ D2 rouge ; (d3) remplacer `"review_ready"` par `"ready"` dans le
`switch` ⇒ D3 rouge.

---

## Q5 — Les autres documents

| Fichier | État relevé | À faire |
|---|---|---|
| `docs/hooks-contract.md` (CRLF) | 30-04 T2 réécrit le §3 (« quitte le widget de trois façons », paragraphe « La lecture (phase 30 — LUE-01 à LUE-04) », LUE-05) ; l'introduction dit encore « le pendant de `data-sources.md` pour la seconde source » | relire l'état FINAL ; ajouter dans « ## 3. » une phrase d'une ligne renvoyant à `docs/desktop-app-sessions.md` (paragraphe « Trois sources, un arbitrage » ou « La lecture ») ; adapter l'intro (« trois sources ») et le §9 (dérive de la source app : voir son document). Ne toucher à AUCUNE phrase gardée (« transition observée sur la MÊME source », « `WaitingDeduced` est une attente pour le détecteur », « daté par l'instant que le SIGNAL porte », « Deux vues d'AppData », « L'app qualifie une ligne, elle n'en crée pas », etc.) |
| `docs/data-sources.md` (LF) | exclusivement les sources d'USAGE (capturé 2026-07-08) ; aucune mention des hooks ni de l'app bureau | ajouter une courte section finale « ## 6. Les sources du widget de sessions » : hooks → `hooks-contract.md` ; métadonnées de l'app bureau → `desktop-app-sessions.md` ; transcripts (même emplacement que §2, lus pour l'activité) ; rappel des deux vues d'AppData. Ne rien réécrire du reste |
| `README.md` (LF) | **aucune section widget** ; menu décrit « clic droit … menu » (c'est désormais une fenêtre de réglages) ; « 215 tests unitaires » | créer « ## Widget de sessions Claude Code » (contenu ci-dessous). Le reste (menu, compte de tests) : discrétion — un compte en dur se périme, préférer « plus de 1 100 tests » ou le retirer |
| `docs/publish.md` (LF) | §6 périmé (voir Q1) | recommandé : §2 « copier sous `Chronos-v<X.Y.Z>.exe` », §6 smoke `--hook` stdin vide + md5, « premier lancement par l'Explorateur, réconciliation constatée dans le fichier » |
| `tests/…/LibellesSessionsTests.cs` | `FichiersSurveilles()` = 9 fichiers, `Assert.Equal(9, fichiers.Count)` | ajouter `docs/desktop-app-sessions.md` et `README.md` (11) : ces deux textes décrivent les libellés, aucun ancien mot (« à toi », « tour fini », « en cours », « inconnu » cités) ne doit y entrer. `Lire` exige ≥ 500 caractères : vrai pour les deux |

**Contenu de la section README (fond, pas la forme) :** activer par réglages → « Widget sessions Claude Code » (au
lancement suivant, Chronos inscrit 8 hooks dans `~/.claude/settings.json`, sauvegarde avant écriture ; seules les
sessions ouvertes ensuite sont suivies) ; **« Réflexion »** = la session travaille ; **« En attente »** = elle a fini
son tour, demande une permission ou pose une question ; **« En attente ? »** = déduit d'un silence de 20 min ; ordre :
questions et permissions, puis tours finis, puis déduites, puis « Réflexion » ; **une session disparaît quand elle est
lue** — ouverte dans l'app après la fin de son tour, ou regardée (fenêtre Claude au premier plan) quand il se termine,
après quelques secondes — et revient si elle redemande ; une session qui travaille reste toujours visible ; **le titre**
donné par l'app remplace le nom du dossier (le dossier reste dans l'info-bulle) ; clic droit sur une ligne : « Marquer
traitée » / « Archiver définitivement » ; réglages → « Diagnostic… » dit pourquoi une session est masquée ; limites :
sessions de terminal sans l'app (pas de règle « lue »), Cowork VM non détecté, l'accueil ou une conversation Chat de
l'app au premier plan compte comme « regardée ». Renvois vers les deux contrats.

Fins de ligne : conserver celles de chaque fichier (CRLF pour `hooks-contract.md` et les tests, LF pour README,
`data-sources.md`, `publish.md`, csproj, `.gitignore`). Les éditions par heredoc Bash ont échoué deux fois en phase
29 : écrire le bloc dans le bloc-notes, l'appliquer par Python en conservant les fins de ligne (précédent 29-05).

---

## Q6 — Le constat en production (critère 4)

### 6.a Ce que l'agent peut relever sans rien écrire

| Relevé | Comment | Vue |
|---|---|---|
| Preuve de vue réelle | Python : `'Claude' in os.listdir(os.environ['APPDATA'])` doit valoir **False** (vrai ce jour) | réelle |
| `treated.json`, `settings.json`, `chronos.log`, `backups\` de Chronos | Python sur `%APPDATA%\Chronos\…` | réelle |
| Fichiers d'état des hooks (`activity`, `reason`, `updated_at`) | `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Chronos\sessions\<id>.json` (5 fichiers ce jour) | chemin physique, identique partout |
| `lastFocusedAt`, `lastActivityAt`, `postTurnSummary` d'une session | `…\LocalCache\Roaming\Claude\claude-code-sessions\<org>\<user>\local_*.json` (138 fichiers, 18 < 24 h ce jour) — **lecture brève** : ouvrir, lire, fermer ; jamais de guet en boucle (une poignée tenue fait échouer le renommage de l'app) | physique |
| Instant de fin de tour | dernier message `assistant` à `stop_reason = end_turn` du transcript `~/.claude/projects/<slug>/<id>.jsonl` (`timestamp` UTC) | hors AppData |
| Processus | `Get-CimInstance Win32_Process` (nom, PID, parent, ligne de commande, date) | système |
| Version du fichier | `(Get-Item …).VersionInfo` | fichier |
| Rapport vivant « Diagnostic… » | **l'utilisateur** : réglages → « Diagnostic… » → Ctrl+C dans la boîte → coller dans la conversation | overlay |
| `chronos.log` | rapport complet écrit au démarrage (vue réelle) — utile pour « Source app-bureau », « Fichiers d'état », « Exe courant » ; **pas** pour la réconciliation (course, fait n° 4) ni pour les causes « lue » (le détecteur n'a encore rien vu) | réelle |

**Pas de sonde WMI en phase 31.** Elle servait en 29-05 à voir AppData comme l'overlay ; l'overlay publié le fait
lui-même. Une sonde qui rejouerait la règle « lue » devrait construire un `TreatedStore` (qui écrit) et n'aurait pas la
mémoire des causes du détecteur de l'overlay.

**Données personnelles :** dans `31-CONSTAT.md`, jamais de titre ni de motif — « titre joint (N car.) », « motif
présent » ; identifiants de session tronqués à 8 caractères ; `%USERPROFILE%` pour le profil (règle de 29-05).

**Sessions de test :** demander à l'utilisateur deux sessions dédiées dans un dossier de travail de son choix (le
dossier et l'identifiant les désignent sans lire les titres), avec des invites qui travaillent ~1 min **sans demander
de permission** (lecture de fichiers, ou génération longue sans outil) — une permission demandée est une autre ligne
du tableau. La session où tourne l'agent est elle-même dans le widget : ne pas la prendre pour S1/S2 ; en revanche,
chaque fin de tour de l'agent pendant que l'utilisateur la lit est une occurrence naturelle de la ligne 3, à noter.

### 6.b Le protocole, écrit avant d'être joué

**Temps 0 — avant (agent, lecture seule) :** md5 et date de `~/.claude/settings.json` ; liste de `backups\` ;
contenu de `treated.json` (1 entrée ce jour) ; `SessionsWidgetEnabled` réel ; PID de l'overlay 3.1.0 ; dossier
Démarrage. Rappel : les entrées de `treated.json` héritées de la 3.1.0 s'afficheront masquées avec la cause résiduelle
« traitée — marquée à la main, ou avant le démarrage de l'overlay » : ce n'est PAS un écart.

**Temps 1 — le lancement (point de contrôle `human-action`) :**
1. L'utilisateur quitte la 3.1.0 : clic droit sur le cadran → « Quitter Chronos ».
2. L'agent constate : plus de processus `Chronos-v3.1.0.exe` sans argument.
3. L'utilisateur double-clique `Chronos-v3.2.0.exe` dans l'Explorateur (dossier du dépôt), attend le cadran et le
   widget ; ouvre les réglages : l'en-tête dit **« v3.2 »** (`AppVersion` = `Version.ToString(2)`).
4. (Décision de l'utilisateur) activer « Lancer au démarrage » depuis cette instance.
5. L'agent constate (Q2.d) : sauvegarde neuve, md5 égal, substitution exacte, comptes ; processus 3.2.0 parent
   `explorer.exe` ; `chronos.log` daté du lancement, « Exe courant : …\Chronos-v3.2.0.exe », « Source app-bureau :
   trouvée — %LOCALAPPDATA%\Packages\Claude_…\claude-code-sessions » ; si l'autostart a été activé, `Chronos.lnk`
   cible `…\Chronos-v3.2.0.exe`.

**Temps 2 — les trois lignes (point de contrôle `human-verify`) :**

| # | Situation (tableau de l'utilisateur) | Geste | Attendu à l'écran | Relevé agent |
|---|---|---|---|---|
| L1 | session qui réfléchit | dans S1, envoyer une invite qui travaille ~1 min ; regarder le widget | S1 « Réflexion », sous son **titre** (pas le nom du dossier) | fichier d'état de S1 : `Working` (`UserPromptSubmit`/`PreToolUse`) ; `title` présent (longueur seulement) |
| L2 | finie, non lue | dans S2, envoyer une invite qui travaille ~1 min, puis **passer sur S1** avant la fin ; attendre la fin du tour de S2 | S2 « En attente » ; puis **ouvrir S2 dans l'app** ⇒ S2 disparaît **sans clic** ; noter le délai | fin de tour S2 (transcript, HH:MM:SS) ; `lastFocusedAt` S2 avant/après l'ouverture et date d'écriture du fichier (latence, V9) ; `treated.json` : +1 entrée pour S2 datée de l'épisode ; rapport copié : « masquée par treated.json — lue : focus à … > attente à … » |
| L2b | idem, fenêtre Claude PAS au premier plan | refaire L2 en restant sur l'Explorateur, S2 sélectionnée dans l'app, pendant la fin du tour ; puis alt-tab vers l'app | S2 reste « En attente » tant que l'Explorateur est devant ; disparaît au retour (alt-tab met `lastFocusedAt` à jour, geste A) | `lastFocusedAt` avant/après l'alt-tab |
| L3 | finie et lue | rester sur S1, fenêtre Claude au premier plan, **sans cliquer sur le widget** (un clic donne le premier plan à Chronos), jusqu'à la fin du tour | S1 ne reste pas « En attente » : au plus **~2,5 à 5,5 s** (grâce voulue), puis rien ; **noter la durée vue** et le jugement de l'utilisateur | fin de tour S1 ; `treated.json` : +1 entrée S1 ; rapport : « sélectionnée au premier plan » |

**Temps 3 — compléments et vérifications déférées (point de contrôle `human-verify`, peut être découpé) :**

| Id | Origine | Vérification | Geste | Attendu |
|---|---|---|---|---|
| V1 | CONTEXT | titre à la place du dossier | lire le widget ; survoler une ligne | titre ; info-bulle « titre — dossier — mot » |
| V2 | CONTEXT, 28 (LIB-02) | question `AskUserQuestion` ⇒ « En attente » au premier rang | dans S2 : « pose-moi une question avec l'outil AskUserQuestion » ; pendant que la question est affichée | S2 « En attente », au-dessus des tours finis ; agent : fichier d'état de S2 = `WaitingAttention` / `PermissionRequest` ? S'il porte `Working`/`PreToolUse`, S2 s'affichera « Réflexion » (limite écrite par `Sans_PermissionRequest_le_battement_d_une_question_gagne_par_fraicheur`) : consigner, rouvrir l'événement porteur plus tard, pas ici |
| V3 | 29 (APP-03) | une VRAIE question classée `blocked` par l'app | opportuniste : quand le rapport dit « fin de tour : question posée » | « En attente » au rang des questions, motif en info-bulle ; « Réflexion » dès la réponse |
| V4 | 29 (APP-06, APP-04) | la racine de production trouvée PAR L'OVERLAY | « Diagnostic… » (ou `chronos.log`) | « Source app-bureau : trouvée — …Packages\Claude_…\claude-code-sessions » ; une ligne « Fichiers d'état » pour la racine du paquet ET pour la vue réelle ; « Jointures » cohérentes avec les lignes du widget |
| V5 | 30 (LUE-03, LUE-04) | chaque masquée a sa cause | « Diagnostic… » | lignes « masquée par treated.json — … » avec motif et instants ; section « Règle « lue » » (sélection, premier plan — qui vaut alors Chronos : c'est vrai à cet instant) ; aucune ligne « un filtre non nommé » |
| V6 | 30 (LUE-02, Piège 1) | accueil ou Chat au premier plan | pendant la fin du tour de la dernière session de code sélectionnée, aller sur l'accueil ou une conversation Chat de l'app | limite CONNUE : la session disparaît probablement « sélectionnée au premier plan » ; consigner, ne rien corriger |
| V7 | 30 (LUE-05, D-30-08) | répondue ⇒ « Réflexion » puis « En attente » ; permission vue puis laissée | répondre à une session en attente ; ouvrir une demande de permission puis la quitter sans répondre | visible « Réflexion » pendant le travail ; « En attente » au tour suivant (non regardé) ; la permission vue quitte le widget — noter si c'est gênant |
| V8 | 27, 30 | réserve 16:23 → 19:51 | passive : toute session restée « En attente » après avoir été lue | relever « focus il y a … » (rapport) et la ligne « Premier plan » ; consigner comme reproduite / non observée |
| V9 | 30 (Piège 11) | latence d'écriture de `lastFocusedAt` | pendant L2/L2b | valeur de `lastFocusedAt` vs date d'écriture du fichier vs instant du geste ; délai de disparition à l'écran |
| V10 | 28, 29 (LIB-01/03, APP-02) | galerie 8 styles | Win+R : `"<dépôt>\Chronos-v3.2.0.exe" --sessions` (aucun Host, aucune écriture) | trois mots sur les 8 tuiles ; « En attente ? » atténuée mais lisible ; titre long (`api-migration`) coupé par une ellipse (Pastilles, Marge, Jetons, Annonciateur) sans élargir la tuile ; info-bulle d'`overlay` à deux lignes « permission demandée » |
| V11 | 28, 29 | 9 thèmes à l'œil | facultatif : réglages → THÈME sur le vrai widget (la galerie n'affiche que le thème par défaut, `ThemeCatalog.Default`) | lisibilité ; couvert mécaniquement par la matrice WPF 8 × 9 |
| V12 | 29 (borne 160 DIP) | dossiers réels de 161 à 167 DIP ; info-bulle à `needs_action` long | lire les lignes sans titre (repli dossier) et une info-bulle de question | ellipse propre à 160 ; info-bulle sans retour à la ligne démesuré |

### 6.c `31-CONSTAT.md` — format proposé

```
# Phase 31 — Constat en production (critère 4)
Date, heures (locales) ; app bureau (version) ; exe (nom, taille, md5, VersionInfo) ; overlay (PID, parent, heure) ;
preuve de vue réelle ; règles (l'agent n'a rien écrit ; aucun titre ni motif ; %USERPROFILE%).
## 0. Réconciliation (VAL-03)     — tableau contrôle | attendu | constaté (md5 avant = md5 sauvegarde ; égalité ; comptes)
## 1. Les trois lignes             — L1, L2, L2b, L3 : geste (qui, quand) | attendu | vu (utilisateur) | relevé (agent) | verdict ✅/⚠/❌
## 2. Compléments et vérifications déférées — V1 à V12, même colonnes ; « non observé » est une réponse
## 3. Écarts                      — chacun : fait, valeurs, hypothèse, suite proposée (phase décimale 31.x ou différé) — jamais corrigé ici sans décision
## 4. Verdict                     — critère 4 : tenu / tenu avec écarts / non tenu ; « le milestone peut être clos : oui/non »
```

Règle de clôture : la vérification de phase (31-VERIFICATION) exige la présence de `31-CONSTAT.md`, ses quatre lignes
L1-L3 (dont L2b) avec un verdict chacune, et une section « Verdict » ; `/gsd:complete-milestone` ne se lance pas sans
ce fichier. Un écart constaté n'est PAS corrigé dans cette phase (précédent : réserve R3 « observation in vivo
d'abord, correctif ensuite ») : il ouvre une phase décimale ou une release 3.2.1, sur décision.

---

## Standard Stack

### Core (déjà là — rien à installer)

| Élément | Version | Usage dans la phase |
|---|---|---|
| .NET SDK | 10.0.201, cible `net8.0-windows` | build, tests, `dotnet publish` |
| Packs d'exécution win-x64 | 8.0.25 (NETCore, WindowsDesktop, Crossgen2) en cache | self-contained + ReadyToRun, hors ligne |
| xUnit / Xunit.StaFact / Microsoft.NET.Test.Sdk | 2.9.2 / 1.1.11 / 17.11.1 | gardes et tests de version |
| `System.Text.RegularExpressions`, `System.Xml.Linq`, `System.Reflection` | framework | extraction des champs ; lecture du csproj ; attributs de version |

### Supporting (outils de relevé, hors dépôt, lecture seule)

| Outil | Usage |
|---|---|
| Python 3.14 (`python` dans le PATH — s'exécute HORS de l'arbre de l'app : vue réelle d'AppData, vérifiée ce jour) | contrôle de réconciliation, `treated.json`, `chronos.log`, `lastFocusedAt`, fin de tour |
| PowerShell 5 (`Get-CimInstance`, `VersionInfo`, `WScript.Shell.CreateShortcut` SANS `Save`) | processus, version du fichier, cible d'un `.lnk` |

**Aucune dépendance NuGet ajoutée.**

## Architecture Patterns

### Découpage recommandé (discrétion du CONTEXT)

| Plan | Vague | Autonome | Contenu | Fichiers |
|---|---|---|---|---|
| 31-01 | 1 | oui | T1 (TDD) garde D1-D6 rouge contre l'absence du document, puis `docs/desktop-app-sessions.md` ; T2 renvois `hooks-contract.md` §3 + intro, `data-sources.md` §6, section README, `LibellesSessionsTests` 9 → 11 ; (option) `docs/publish.md` ; mutations d1-d3 | `docs/*.md`, `README.md`, `tests/…/ContratAppBureauDocumenteTests.cs` (nouveau), `ContratHooksDocumenteTests.cs` (helpers `internal`), `LibellesSessionsTests.cs` |
| 31-02 | 2 | oui (s'arrête avant tout lancement) | T1 (TDD) ligne `Version :` au diagnostic + `VersionPublieeTests` ; T2 `.gitignore` + bump + suite ×2 + publication + contrôles 5-8 de Q1 ; commit de release | `DiagnosticService.cs`, `DiagnosticServiceTests.cs`, `tests/…/VersionPublieeTests.cs` (nouveau), `Chronos.csproj`, `.gitignore` |
| 31-03 | 3 | **non** (`checkpoint:human-action` puis `human-verify` ×2) | Temps 0-3 de Q6 ; `31-CONSTAT.md` ; lignes « Manual-Only » de 28/29/30-VALIDATION renvoyées au constat | `31-CONSTAT.md`, VALIDATION de 28-30 (optionnel) |

31-01 et 31-02 ne sont pas parallélisables : le commit de release porte le compte FINAL de tests, et l'exe doit
contenir la ligne de version. 31-02 lit l'état final de `DiagnosticService.cs` après 30-04 (gardes du fichier :
une seule occurrence de `Inspecter(` ; aucun `.Lire(`, `RacinesEtat`, `Claude_`, `claude-code-sessions`,
`HorizonsSessions.Silence`, `FromUnixTimeMilliseconds`, `IPremierPlan`, `PremierPlanWin32` — la ligne de version n'en
utilise aucun).

### Pattern 1 — La garde par extraction (VAL-02)
Le test lit le code comme du texte, en extrait l'ensemble de ce qu'il lit, et exige l'égalité avec une table balisée
du document ; un anti-muet rougit si l'extraction ne trouve plus rien. (Esquisse en Code Examples.)

### Pattern 2 — Version : cohérence, pas épinglage
Un test qui épinglerait « 3.2.0 » serait à réécrire à chaque release sans rien garder d'autre. Tenir plutôt : les
quatre propriétés du csproj sont cohérentes entre elles (`Version` = `InformationalVersion` = X.Y.Z, `FileVersion` =
`AssemblyVersion` = X.Y.Z.0) ET égales aux attributs de l'assembly compilée
(`AssemblyInformationalVersionAttribute`, `AssemblyFileVersionAttribute`, `GetName().Version`). La valeur 3.2.0 est
tenue par les critères `grep` du plan de release et par `VersionInfo` sur l'exe.

### Pattern 3 — Le constat « prédit puis constaté »
Chaque ligne du protocole porte son attendu chiffré AVANT le geste (y compris la fenêtre de grâce et le délai de
disparition), et chaque relevé de l'agent est horodaté ; un écart est un fait consigné, pas un correctif.

### Anti-patterns à éviter
- **Lancer l'overlay depuis l'agent** (ou depuis un terminal de l'app) : vue virtualisée, autres fichiers d'état, et
  précédent 19-05 violé.
- **Supprimer `Chronos-v3.1.0.exe` pendant la phase** : les sessions ouvertes avant la réconciliation l'appellent encore.
- **Lire la réconciliation dans `chronos.log`** (course avec `LogStartupAsync`).
- **`git add -A` / `git add .`** tant que `.gitignore` n'ignore pas les exe.
- **Recopier la règle « lue » dans le nouveau document** : elle vit dans `hooks-contract.md` §3, sous garde.
- **Écrire « jonction »**.
- **Garder un fichier de l'app ouvert** (guet en boucle) pendant le constat : l'app échoue son renommage et se replie
  sur une écriture en place.

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser | Pourquoi |
|---|---|---|---|
| Repointer les hooks et la statusLine vers le nouvel exe | une édition manuelle ou un script de remplacement dans `settings.json` | le lancement de la 3.2.0 (`ClaudeSettingsReconciler`) | sauvegarde, atomicité, purge large, refus nommés, point fixe : tout est livré et testé |
| Parser une table Markdown dans la garde | un nouveau parseur | `TableEntre` / `SectionDe` de `ContratHooksDocumenteTests` rendus `internal static` | barre échappée, séparateur, anti-muet déjà traités |
| Trouver `docs/` et `src/` dans un test | remontée depuis `AppContext.BaseDirectory` | `CheminDocsChronos`, `GardesPerimetreTests.CheminSources()` | CLAUDE.md ; une garde qui ne trouve plus son fichier ne garde rien |
| Voir AppData comme l'overlay | une sonde WMI compilée | Python (hors de l'arbre, vérifié) + `chronos.log` de l'overlay publié | plus simple, et lecture seule sans DLL à compiler |
| Obtenir le rapport vivant | lire `diagnostic.txt` (écrit seulement par `RunAsync`, que l'UI n'appelle pas) | « Diagnostic… » + Ctrl+C | c'est la seule sortie du rapport vivant |
| Version lisible dans le rapport | une chaîne en dur | `AssemblyInformationalVersionAttribute` | une seule source : le csproj |

## Common Pitfalls

### Piège 1 — Le `.gitignore` n'ignore pas les exe
**Ce qui se passe :** un commit large embarque 77 Mo. **Parade :** `/Chronos-v*.exe` dans `.gitignore` (commit de
release), et `git add` par chemins explicites. **Signe :** `git status` montre `?? Chronos-v3.2.0.exe`.

### Piège 2 — Deux overlays
**Ce qui se passe :** 3.1.0 non quittée ; deux widgets, deux détecteurs sur `treated.json`, deux `SessionsX/Y`
enregistrés. **Parade :** étape 1-2 du Temps 1, constat par processus. **Signe :** deux PID sans argument.

### Piège 3 — Lancer dans la mauvaise vue
**Ce qui se passe :** exe lancé depuis un terminal ouvert dans l'app ou par l'agent : réglages du paquet (13.09),
`treated.json` du paquet, rien de ce qui est constaté ne vaut pour l'overlay réel. **Parade :** Explorateur / Win+R ;
constat : parent `explorer.exe`. **Signe :** parent `claude.exe`, `bash.exe` ou `powershell.exe`.

### Piège 4 — Widget désactivé au moment du lancement
**Ce qui se passe :** `Reconcile(false)` PURGE les 8 groupes. **Parade :** relire `SessionsWidgetEnabled` réel au
Temps 0. **Signe :** 0 commande `--hook` Chronos après lancement.

### Piège 5 — Quelqu'un réécrit `settings.json` entre le relevé et le lancement
**Ce qui se passe :** Claude Code ou GSD réécrit le fichier (il l'a été le 25.09 à 11:07:56) ; la comparaison
« avant/après » mélange deux changements. **Parade :** comparer à la SAUVEGARDE (copie octet pour octet de l'état
avant) et vérifier md5(sauvegarde) = md5 relevé ; sinon, re-relever et le noter.

### Piège 6 — « Jamais « En attente » » en ligne 3
**Ce qui se passe :** la grâce de 2,5 s se voit (2,5 à ~5,5 s). **Parade :** mesurer et faire juger (fait n° 7).

### Piège 7 — Le clic qui vole le premier plan
**Ce qui se passe :** cliquer sur le widget ou ouvrir les réglages pendant L3 donne le premier plan à Chronos : LUE-02
cesse de s'appliquer, S1 reste « En attente ». **Parade :** regarder sans cliquer ; ouvrir « Diagnostic… » APRÈS.

### Piège 8 — Une permission qui s'invite dans L1/L2
**Ce qui se passe :** une invite qui appelle Bash fait apparaître une demande de permission : « En attente » au lieu de
« Réflexion ». **Parade :** invites sans outil à permission ; si elle apparaît, c'est V7, noté comme tel.

### Piège 9 — Garder ouvert un fichier de l'app
**Ce qui se passe :** le renommage de l'app échoue quel que soit notre partage ; repli en écriture en place (fichier
tronqué possible pour un lecteur). **Parade :** lectures brèves, à la demande, jamais en boucle.

### Piège 10 — Garde vacueuse
**Ce qui se passe :** le document cite un fragment qui figure déjà ailleurs (« même source » au §5 des hooks) ; la
garde est verte avant d'être écrite. **Parade :** RED constaté contre l'absence du document ; mutations d1-d3.

### Piège 11 — Recopier des données personnelles
**Parade :** ni titre, ni motif, ni nom d'utilisateur dans `docs/` ni dans `31-CONSTAT.md` (`grep -i tanguy` = 0).

### Piège 12 — Heredoc Bash sur un gros bloc
**Ce qui se passe :** « unexpected EOF » (29-03, 29-05). **Parade :** bloc-notes + Python, fins de ligne conservées.

## Code Examples

### Contrôle de la réconciliation (agent, Python, lecture seule, vue réelle)

```python
# Source : ClaudeSettingsReconciler (sauvegarde octet pour octet avant écriture) + SessionHookInstaller.Cablage
import json, os, glob, hashlib
ap = os.environ['APPDATA']
assert 'Claude' not in os.listdir(ap), "vue virtualisée : relancer hors de l'arbre de l'app"
p = os.path.expanduser('~/.claude/settings.json')
sauv = sorted(glob.glob(os.path.join(ap, 'Chronos', 'backups', 'claude-settings-*.json')))[-1]
print('sauvegarde', os.path.basename(sauv), 'md5', hashlib.md5(open(sauv, 'rb').read()).hexdigest())  # = md5 relevé au Temps 0
avant = open(sauv, encoding='utf-8').read()
apres = json.load(open(p, encoding='utf-8'))
attendu = json.loads(avant.replace('Chronos-v3.1.0.exe', 'Chronos-v3.2.0.exe'))
print('égalité structurelle :', apres == attendu)                                   # True
cmds = [h.get('command', '') for gs in apres['hooks'].values() for g in gs for h in g.get('hooks', [])]
print('hooks 3.2.0 :', sum('Chronos-v3.2.0.exe" --hook' in c for c in cmds))         # 8
print('restes 3.1.0 :', sum('Chronos-v3.1.0.exe' in c for c in cmds)
      + ('Chronos-v3.1.0.exe' in apres['statusLine']['command']))                   # 0
print('gsd :', sum('gsd-' in c for c in cmds))                                        # 3
print('statusLine :', apres['statusLine']['command'].endswith('Chronos-v3.2.0.exe" --statusline'))  # True
for cle, gs in apres['hooks'].items():
    n = sum(any('--hook' in h.get('command', '') and 'Chronos' in h.get('command', '') for h in g.get('hooks', [])) for g in gs)
    print(cle, len(gs), 'groupe(s), dont Chronos :', n)                              # 1 pour chacune des 8 clés
```

### Processus et cible du raccourci (PowerShell, lecture seule)

```powershell
Get-CimInstance Win32_Process -Filter "Name like 'Chronos%'" |
  Select-Object ProcessId, ParentProcessId, CreationDate, CommandLine | Format-List
(Get-Item '.\Chronos-v3.2.0.exe').VersionInfo | Select-Object FileVersion, ProductVersion
$s = New-Object -ComObject WScript.Shell            # CreateShortcut sur un .lnk EXISTANT le lit ; ne jamais appeler Save()
Get-ChildItem ([Environment]::GetFolderPath('Startup')) -Filter *.lnk |
  ForEach-Object { '{0} -> {1}' -f $_.Name, $s.CreateShortcut($_.FullName).TargetPath }
```

### Fin de tour et dernier focus d'une session (Python, lecture brève)

```python
import json, os, glob
sid = '<cliSessionId complet>'
t = glob.glob(os.path.expanduser(f'~/.claude/projects/*/{sid}.jsonl'))[0]
fin = None
for ligne in open(t, encoding='utf-8', errors='replace'):
    try: d = json.loads(ligne)
    except ValueError: continue
    if d.get('type') == 'assistant' and (d.get('message') or {}).get('stop_reason') == 'end_turn':
        fin = d.get('timestamp')                         # UTC ISO 8601
racine = os.path.join(os.environ['LOCALAPPDATA'], 'Packages', 'Claude_pzs8sxrjxfjjc',
                      'LocalCache', 'Roaming', 'Claude', 'claude-code-sessions')
for f in glob.glob(os.path.join(racine, '*', '*', 'local_*.json')):
    with open(f, 'rb') as h: brut = h.read()             # ouvrir, copier, fermer — rien de plus
    m = json.loads(brut.decode('utf-8-sig'))
    if m.get('cliSessionId') == sid:
        print('fin de tour', fin, '· lastFocusedAt (ms)', m.get('lastFocusedAt'), '· écrit', os.path.getmtime(f))
```

### Garde par extraction (esquisse C#, `ContratAppBureauDocumenteTests`)

```csharp
// Source : motif de ContratHooksDocumenteTests (CheminDocs, TableEntre) + GardesPerimetreTests.CheminSources()
private static readonly Regex AppelLecteur =
    new(@"\b(?:Texte|Instant|Entier|Booleen)\((?<objet>racine|resume),\s*""(?<champ>[A-Za-z_]+)""");
private static readonly Regex Propriete = new(@"TryGetProperty\(""(?<champ>[A-Za-z_]+)""");

private static SortedSet<string> ChampsLusParLeLecteur()
{
    var code = File.ReadAllText(Path.Combine(GardesPerimetreTests.CheminSources(), "Services", "LecteurAppBureau.cs"));
    var champs = new SortedSet<string>(StringComparer.Ordinal);
    foreach (Match m in AppelLecteur.Matches(code))
        champs.Add(m.Groups["objet"].Value == "resume" ? "postTurnSummary." + m.Groups["champ"].Value : m.Groups["champ"].Value);
    foreach (Match m in Propriete.Matches(code)) champs.Add(m.Groups["champ"].Value);
    Assert.True(champs.Contains("cliSessionId") && champs.Contains("lastFocusedAt") && champs.Count >= 12,
        "La garde ne sait plus lire le lecteur (forme des appels changée) : adapter la garde, pas la désarmer. Lu : "
        + string.Join(", ", champs));
    return champs;
}
// D2 : ensemble du document (TableEntre(texte, "CHAMPS-LUS:debut", "CHAMPS-LUS:fin"), colonne 0) == ChampsLusParLeLecteur(),
//      manquants et surnuméraires nommés, puis Assert.Equal(nombre de lignes, taille de l'ensemble) (aucun doublon).
```

### Ligne de version au diagnostic (esquisse)

```csharp
// En-tête du rapport, après « Date : ». Une seule source : le csproj (IncludeSourceRevisionInInformationalVersion=false).
var version = typeof(DiagnosticService).Assembly
    .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
sb.AppendLine("Version : " + version);
// Test : le rapport contient « Version : » + l'attribut lu sur l'assembly — jamais « 3.2.0 » en dur.
```

## State of the Art

| Avant | Maintenant | Quand | Conséquence |
|---|---|---|---|
| « `%APPDATA%\Claude` est une jonction » | virtualisation d'AppData du paquet MSIX, deux vues | erratum 2026-09-25 21:17 | le document et le constat le disent ; relevés depuis une session ≠ vue de l'overlay |
| Relevé « depuis ce shell » ($APPDATA de Bash) | Python hors de l'arbre (vue réelle, vérifiée) | 29-05, confirmé ce jour | tous les contrôles de fichiers Chronos du constat passent par Python |
| Smoke de publication = lancer l'overlay 8 s (`publish.md` §6) | smoke `--hook` stdin vide + md5 ; premier lancement par l'utilisateur | 3.1.0 (cd26b31), précédent 19-05 | `publish.md` §6 à mettre à jour (recommandé) |

## Open Questions

1. **La ligne `Version :` au diagnostic — ajout de code en phase 31 ?**
   - Ce qu'on sait : le critère 2 cite « diagnostic » ; le rapport n'a que « Exe courant : <chemin> » (le nom porte
     la version) ; les réglages affichent « v3.2 ».
   - Recommandation : l'ajouter (1 ligne, 1 test, sans risque pour les gardes de `DiagnosticService.cs`), dans 31-02
     AVANT la publication. À défaut, écrire dans le plan que le critère se lit par `VersionInfo` + « Exe courant ».
2. **Le rapport liste 5 événements de hooks sur 8.** La ligne « Hooks --hook installés » énumère seulement
   `Notification, Stop, UserPromptSubmit, SessionStart, SessionEnd`. Observation, hors périmètre ; au constat, la
   preuve des 8 groupes est le fichier. À noter comme écart mineur de diagnostic si le planificateur ne l'élargit pas.
3. **Audit d'intégration avant publication ?** La rétrospective v1.6 crédite l'audit de fin de milestone d'avoir
   trouvé 8 régressions « avant la mise en production » ; en v1.7 la publication précède `/gsd:audit-milestone`.
   Recommandation : garder l'ordre (le constat est lui-même le contrôle in vivo) ; une régression trouvée ensuite
   donne une 3.2.1. À signaler à l'utilisateur, pas à trancher par l'agent.
4. **Étiquette.** La mémoire dit « tag `v<version>` » ; la 3.1.0 n'en a pas eu (seule `milestone-v1.6`, posée à la
   clôture). Recommandation : aucune étiquette en phase 31 ; `milestone-v1.7` à la clôture ; jamais de push sans accord.
5. **Incohérences de `REQUIREMENTS.md` à la clôture** (hors exigences de la phase) : VAL-01 reste « Pending » alors que
   la phase 27 est close ; APP-01 et le Contexte disent encore « jonction ». À corriger au passage de VAL-02/VAL-03 en
   « Complete », ou à la clôture du milestone.
6. **Que vaut « En attente » pendant la grâce pour l'utilisateur ?** Seul le constat le dira (L3) ; si la durée gêne,
   la décision (raccourcir la grâce ? la cadence ?) appartient à une phase suivante, pas au constat.

## Environment Availability

| Dépendance | Requise par | Disponible | Version / état | Repli |
|---|---|---|---|---|
| .NET SDK | build, tests, publication | ✓ | 10.0.201 | — |
| Packs win-x64 8.0 (runtime, desktop, crossgen2) | self-contained + R2R | ✓ | 8.0.25 en cache | restauration NuGet (réseau) |
| Python | relevés vue réelle | ✓ | 3.14 (hors arbre de l'app : `'Claude'` absent d'`APPDATA`) | WMI (précédent 29-05) |
| PowerShell | processus, `VersionInfo`, `.lnk` | ✓ | 5.x | — |
| App bureau Claude | constat | ✓ | 2.9939.2.0 (`Claude_pzs8sxrjxfjjc`), processus `claude.exe` depuis le 25.09 16:59 | aucun : le constat l'exige |
| Overlay 3.1.0 | à quitter | en marche | PID 40772, parent explorer, depuis le 23.09 11:09:31 | — |
| Autostart | éventuel | ✗ (aucun) | Démarrage : `desktop.ini` seul ; `Run` et tâches : rien | activation depuis la 3.2.0 si l'utilisateur le veut |
| `SessionsWidgetEnabled` réel | réconciliation (hooks voulus) | ✓ | `True` (2026-09-26 09:51:12) | — |
| `~/.claude/settings.json` | réconciliation | ✓ | md5 `9eab8a8e…418b`, 3 384 o, 8 groupes 3.1.0 + 3 `gsd-*` + statusLine 3.1.0 | — |
| **L'utilisateur** | Temps 1-3 | à planifier | — | **aucun** : critère 4 bloquant sans lui |

**Bloquant sans repli :** la présence de l'utilisateur pour 31-03. **Avec repli :** rien d'autre.

## Validation Architecture

### Test Framework

| Propriété | Valeur |
|---|---|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11, Microsoft.NET.Test.Sdk 17.11.1 — `tests/Chronos.Tests` (`net8.0-windows`) |
| Config | `tests/Chronos.Tests/Chronos.Tests.csproj` (attributs `CheminSourcesChronos`, `CheminDocsChronos`) — aucun ajout nécessaire (README dérivé de `CheminDocsChronos`) |
| Commande rapide | `dotnet test Chronos.sln -c Debug --nologo -v q --filter "FullyQualifiedName~ContratAppBureauDocumenteTests\|FullyQualifiedName~ContratHooksDocumenteTests\|FullyQualifiedName~LibellesSessionsTests\|FullyQualifiedName~VersionPublieeTests\|FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~LectureSeuleAppBureauTests\|FullyQualifiedName~ClaudeSettingsReconcilerTests"` |
| Suite complète | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| Baseline | **à mesurer en entrée de phase** : 1143 attendus en fin de phase 30 (1110 mesurés à mi-phase, `284fcc7`) ; ~11 s de tests, ~25 s avec compilation |
| Cible indicative | 1143 + 6 (D1-D6) + 2 (cohérence de version) + 1 (ligne de version) = **≈ 1152** ; égalité, pas plancher : tout écart justifié nominativement au SUMMARY |

### Phase Requirements → Test Map

| Req | Comportement | Type | Commande | Existe ? |
|---|---|---|---|---|
| VAL-02 | le document existe, chemin injecté (D1) | garde fichier | `--filter ~ContratAppBureauDocumenteTests` | ❌ Wave 0 (créé par 31-01 T1) |
| VAL-02 | champs documentés = champs lus, deux sens, sans doublon (D2) | garde croisée texte | idem | ❌ |
| VAL-02 | catégories documentées = bras du `switch` (D3) | garde croisée texte | idem | ❌ |
| VAL-02 | racines, « pas une jonction », 24 h tenus contre `RacinesEtat` et `HorizonsSessions` (D4) | garde croisée | idem | ❌ |
| VAL-02 | section « non garanti » datée, marqueurs (D5) | garde texte | idem | ❌ |
| VAL-02 | renvois depuis `hooks-contract.md` §3, `data-sources.md`, README (D6) | garde texte | idem | ❌ |
| VAL-02 | aucun ancien libellé dans le nouveau document ni le README | garde texte | `--filter ~LibellesSessionsTests` | ✅ à étendre (9 → 11) |
| VAL-02 | le §3 des hooks garde ses phrases | garde existante | `--filter ~ContratHooksDocumenteTests` | ✅ (inchangée) |
| VAL-03 | quatre propriétés cohérentes ; attributs de l'assembly = csproj | unit (lecture du csproj par `CheminSourcesChronos` + réflexion) | `--filter ~VersionPublieeTests` | ❌ (31-02 T1) |
| VAL-03 | le rapport dit la version embarquée | intégration rapport | `--filter ~DiagnosticServiceTests` | ✅ à étendre (+1) |
| VAL-03 | exe publié : taille < 120 Mo, 0 DLL, `VersionInfo` 3.2.0(.0), md5 copie = sortie, smoke `--hook` code 0, md5 `settings.json` inchangé | contrôles shell automatisés (plan) | commandes de Q1.c | ❌ (critères d'acceptation de 31-02) |
| VAL-03 | réconciliation constatée dans le fichier | **manuel + relevé agent** | Code Examples « Contrôle de la réconciliation » | point de contrôle 31-03 |
| Critère 4 | trois lignes + V1-V12 | **manuel uniquement** (exige l'app, le premier plan réel, l'œil de l'utilisateur) | vérification automatisable : `31-CONSTAT.md` existe, contient L1, L2, L2b, L3 avec verdict et une section « Verdict » | ❌ (31-03) |

### Sampling Rate

- **Par tâche :** la commande rapide.
- **Par vague :** suite complète, deux exécutions consécutives.
- **Porte de phase :** suite complète verte deux fois ; mutations (d1), (d2), (d3) et (v1) `InformationalVersion`
  3.2.1 avec `Version` 3.2.0 ⇒ cohérence rouge ; (v2) ligne de version retirée ⇒ test du rapport rouge — jouées,
  rouges nommés, révoquées (sha256) ; contrôles de publication consignés ; `31-CONSTAT.md` présent.
- **Latence de retour :** ~25 s (suite complète avec compilation).

### Wave 0 Gaps

Pas de vague 0 séparée (précédent 28-30) : chaque tâche de code est TDD et crée ses tests d'abord.
- [ ] `tests/Chronos.Tests/ContratAppBureauDocumenteTests.cs` — D1-D6 (RED contre l'absence du document)
- [ ] `tests/Chronos.Tests/VersionPublieeTests.cs` — cohérence csproj ↔ assembly
- [ ] extension `DiagnosticServiceTests` — ligne de version (si Open Question 1 retenue)
- [ ] extension `LibellesSessionsTests` — 11 fichiers surveillés
- [ ] `ContratHooksDocumenteTests` — `TableEntre`/`SectionDe` passés `internal static` (aucune assertion touchée)
- Aucun framework à installer.

### Vérifications manuelles (point de contrôle humain de 31-03)

Réconciliation (Temps 1) ; L1, L2, L2b, L3 (Temps 2) ; V1-V12 (Temps 3). Toutes consignées dans `31-CONSTAT.md`, avec
leur relevé agent quand il existe.

## Sources

### Primary (HIGH)
- Dépôt à `5db55b0`, lu le 2026-09-26 : `Chronos.csproj`, `win-x64.pubxml`, `.gitignore`, `App.xaml.cs` (par contenu),
  `ClaudeSettingsReconciler.cs`, `ClaudeSettingsJson.cs`, `SessionHookInstaller.cs`, `StatusLineInstaller.cs`,
  `AutostartService.cs`, `LecteurAppBureau.cs`, `RacinesEtat.cs`, `HorizonsSessions.cs`, `TreatedStore.cs`,
  `DiagnosticService.cs` (par contenu), `ChronosPaths.cs`, `SessionsGalleryWindow.xaml.cs`, `SettingsWindow.xaml(.cs)`,
  `MainWindow.xaml.cs`, `SessionsViewModel.cs`, `PremierPlanWin32.cs` ; tests `ContratHooksDocumenteTests`,
  `LibellesSessionsTests`, `ClaudeSettingsReconcilerTests`, `GardesPerimetreTests`, `Chronos.Tests.csproj`,
  `TestData/DesktopAppSessions/README.md`.
- `git show --stat cd26b31` ; `git diff --stat cd26b31..HEAD` (fichiers des hooks et de la réconciliation) ; `git tag`.
- Relevés en lecture seule sur la machine, 2026-09-26 ~11:00 : `Get-CimInstance Win32_Process`, `Get-AppxPackage`,
  `reg query HKCU\…\Run`, `Get-ScheduledTask`, `VersionInfo`, md5 de `settings.json` et de l'exe 3.1.0 ; Python (vue
  réelle) : `%APPDATA%\Chronos\*`, dossier Démarrage, `%LOCALAPPDATA%\Temp\.net`, racines du paquet.
- Planification : `31-CONTEXT.md`, `ROADMAP.md` (Phase 31), `REQUIREMENTS.md`, `RETROSPECTIVE.md`, `27-RELEVE.md`,
  `29-05-SUMMARY.md`, `29-SONDE-APP06.txt`, `29-SONDE-HORS-ARBRE.txt`, `29-RESEARCH.md` (Q1, Q3, pièges 4-7),
  `28-VALIDATION.md`, `29-VALIDATION.md`, `30-VALIDATION.md`, `30-RESEARCH.md` (pièges 1, 11, 12), `30-04-PLAN.md`
  (D-30-12, texte du §3), `19-05-SUMMARY.md`, `docs/publish.md`, `docs/hooks-contract.md`, `docs/data-sources.md`,
  `README.md`.

### Secondary (MEDIUM)
- Comportement de l'écrivain de l'app (temporaire + renommage, 3 essais, repli en place) : code minifié de `app.asar`
  2.9939.2.0, lu en 29-RESEARCH.
- Copie du texte d'une boîte de message Windows par Ctrl+C : comportement standard de la boîte native utilisée par
  `MessageBox.Show` de WPF ; non vérifié sur cette machine.
- Absence probable d'alerte SmartScreen pour un exe produit localement (pas de marque de provenance) : non vérifié.

### Tertiary (LOW)
- Émission de `PermissionRequest` pour `AskUserQuestion` sur la version de Claude Code de l'utilisateur : inconnue,
  c'est l'objet de V2.

## Metadata

**Confidence breakdown :**
- Release : HIGH — procédure 3.1.0 lue, packs en cache, sortie précédente mesurée, smoke lu dans le code.
- Réconciliation : HIGH — code lu, tests existants lus, état réel relevé (8 + 3 groupes, statusLine, widget activé,
  sauvegardes).
- Mono-instance / autostart : HIGH — absence prouvée par `grep` et par le relevé (processus, Démarrage, registre, tâches).
- Garde croisée : HIGH — modèle lu ; extraction vérifiée à la main sur le texte actuel (14 noms).
- Constat : MEDIUM — protocole fondé sur le code livré ; trois comportements de l'app (latence du focus, accueil/Chat,
  `PermissionRequest`) ne se tranchent qu'en production.

**Research date :** 2026-09-26
**Valid until :** 2026-10-03 (l'app bureau et Claude Code changent souvent ; refaire le Temps 0 le jour du constat, et
vérifier que l'app est toujours 2.9939.2.0 — sinon, re-relever les champs avant d'écrire la date du document)
