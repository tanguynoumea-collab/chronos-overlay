# Council cycle 2 — Packaging / Déploiement (Tier 3)

Rôle : Packaging/Déploiement. Projet : Chronos (WPF net8.0-windows, exe self-contained mono-fichier win-x64,
autostart shell:startup, pas de WiX/ClickOnce, pas de manifest `.addin` — ce n'est pas un add-in Revit : le
volet « multi-target Revit 2024/2025/2026 » du prompt de rôle est sans objet).

Périmètre calibré : le produit est d'abord un outil personnel publié en local (`Chronos-vX.Y.Z.exe` à la racine du
dépôt), MAIS le dépôt GitHub est PUBLIC et le README invite à télécharger l'exe depuis les releases. Le zèle est donc
modéré sur la chaîne de build et porté sur la mise à jour d'une version à l'autre (hooks, autostart, verrou d'instance).

## Phase 1 — Vérité-terrain

| Contrôle | Résultat |
|---|---|
| `dotnet build -c Release` (copie isolée `git archive HEAD` dans le scratchpad, pour ne rien écrire dans l'arbre) | Réussi, **0 avertissement, 0 erreur**, 8 s. SDK utilisé : 10.0.201 (seul SDK installé). |
| `dotnet publish` | **Non lancé** (consigne). La sortie de la dernière publication est encore dans `src/Chronos/bin/Release/net8.0-windows/win-x64/publish/Chronos.exe` (FileVersion 3.4.0.0, 27/09 22:07). |
| `dotnet pack` | Sans objet (pas de paquet NuGet). |
| Versioning csproj | `Version 3.4.0`, `FileVersion 3.4.0.0`, `AssemblyVersion 3.4.0.0`, `InformationalVersion 3.4.0`, `IncludeSourceRevisionInInformationalVersion=false`. Cohérent ; tenu par `tests/Chronos.Tests/VersionPublieeTests.cs`. Pas de `Directory.Build.props`. Le bump vers 3.5.0 est prévu en phase 43 (ROADMAP, critère 1). |
| Exe publiés à la racine | 3.1.0, 3.2.0, 3.2.1, 3.2.2, 3.3.0, 3.3.1, 3.4.0 : nom ↔ VersionInfo cohérents pour les 7 (`FileVersion` X.Y.Z.0 / `ProductVersion` X.Y.Z). Tous **NotSigned** (Get-AuthenticodeSignature). ~77-78 Mo chacun (≈ 545 Mo cumulés, ignorés par `.gitignore:22`). |
| Profil de publication | `src/Chronos/Properties/PublishProfiles/win-x64.pubxml` : mêmes propriétés que le `PropertyGroup` conditionné du csproj (doublon assumé, documenté dans `docs/publish.md` §3). Trim désactivé, R2R activé, compression activée, globalisation conservée : conforme. |
| Manifeste | `src/Chronos/app.manifest` : PerMonitorV2 + repli dpiAware, supportedOS Win10/11, pas de `requestedExecutionLevel` (asInvoker implicite → aucun droit admin) : conforme. |
| Reproductibilité | Pas de `global.json`, pas de `packages.lock.json`. Pack runtime self-contained restauré : `Microsoft.NETCore.App.Runtime.win-x64` **8.0.25** (version implicite choisie par le SDK installé). |
| Tags / releases | Aucun tag git pour les exe 3.x (dernier tag d'exe : `v2.8.1`). Tags de milestone `v1.7`, `v1.8` dans le même espace de noms que les anciens tags d'exe `v1.0`…`v2.8.1` (et `milestone-v1.6` pour contourner la collision avec le tag d'exe `v1.6`). GitHub (`gh release list`) : dernière release publique = **v2.8.1 (12/07/2026)**, dépôt `PUBLIC`. |
| CI | Aucune (`.github` absent). Publication 100 % manuelle (procédure `docs/publish.md` + plans de release). |

## Phase 2 — Findings

### PKG-1 — Le raccourci d'autostart reste sur l'ancien exe versionné : au reboot suivant la mise à jour, c'est l'ANCIENNE version qui démarre et prend le verrou
- **Sévérité** : Majeur
- **Rôle** : Packaging/Déploiement
- **Localisation** : `src/Chronos/Services/AutostartService.cs:24` (`IsEnabled() => File.Exists(LinkPath)`) et `:37` (`TargetPath = Environment.ProcessPath`) ; `src/Chronos/ViewModels/MainViewModel.cs:468` et `:794-798` ; `src/Chronos/App.xaml.cs:87-97` (verrou) ; `docs/publish.md:143` et section « Premier lancement de la 3.5.0 » (`:173` et suiv.).
- **Preuve** : `IsEnabled()` ne teste que l'EXISTENCE de `Chronos.lnk`, jamais sa cible. Aucune réconciliation de la cible du raccourci au démarrage (grep `Autostart` dans `src` : seuls App.xaml.cs:271 (DI) et MainViewModel). Le nom de l'exe change à chaque release (`Chronos-v3.4.0.exe` → `Chronos-v3.5.0.exe`) et l'ancien est volontairement gardé sur le disque (`docs/publish.md:142`). La section 3.5.0 de `publish.md` ne parle pas de l'autostart ; la consigne générique (`:143`, « Activer … DEPUIS le nouvel exe ») suppose un toggle décoché, alors que la 3.5 l'affichera COCHÉ (le .lnk existe).
- **Constat** : un utilisateur qui avait activé « Lancer au démarrage » en 3.4 voit la case cochée dans la 3.5 et n'y touche pas. Au reboot suivant, `shell:startup` lance `Chronos-v3.4.0.exe`. Celui-ci prend le mutex `Local\Chronos-overlay`, réconcilie `~/.claude/settings.json` en repointant les huit groupes de hooks vers la 3.4.0 (`git show 8bec859:…/ClaudeSettingsReconciler.cs:90-91`), et toute tentative de lancer la 3.5 répond « Chronos tourne déjà ». Le retour en arrière est silencieux : l'utilisateur voit l'ancien cadran (barre de statut dans les réglages, pas de geste sur toute la silhouette, etc.) sans savoir pourquoi.
- **Impact** : mise à jour qui ne survit pas à un redémarrage, régression silencieuse des hooks vers l'ancien exe. Risque complémentaire (preuve partielle, non exécuté) : ni la 3.4 ni la 3.5 ne portent `[JsonExtensionData]` sur `ChronosSettings`, et la 3.4 réécrit le fichier par `JsonSerializer.Serialize(settings)` (`git show 8bec859:src/Chronos/Services/SettingsService.cs:59-64`). Un Save de la 3.4 effacerait donc les réglages propres à la 3.5 (thèmes, orientations, mode de cadran…). Note : la barre de statut n'est PAS réinstallée par la 3.4, qui ne fait que repointer une barre existante.
- **Recommandation** : au démarrage en mode overlay, si `Chronos.lnk` existe, lire sa cible (WScript.Shell `CreateShortcut(path).TargetPath`) et la réécrire vers `Environment.ProcessPath` quand elle diffère : même logique « converger vers l'exe courant » que `ClaudeSettingsReconciler`, idempotente, journalisée dans le bilan. À défaut, pour la 3.5.0, ajouter dans `docs/publish.md` §7 (section 3.5.0) et le README une étape explicite : décocher puis recocher « Lancer au démarrage » depuis la 3.5. Et faire afficher la cible réelle du raccourci par le diagnostic.
- **Applicabilité** : concerne tout utilisateur qui a activé l'autostart, donc au moins l'auteur. À traiter avant ou pendant la phase 43.
- **Statut challenge** : non challengé.

### PKG-2 — Canal de distribution public figé à la v2.8.1 alors que le README décrit la 3.5 et renvoie à « la dernière release »
- **Sévérité** : Majeur
- **Rôle** : Packaging/Déploiement
- **Localisation** : `README.md:23-27` (installation), `README.md:197-199` (commande de publication, sortie `Chronos.exe`) ; GitHub `tanguynoumea-collab/chronos-overlay` ; `.planning/ROADMAP.md:369-371` (« commit de release sans tag ni push »).
- **Preuve** : `gh repo view` → `"visibility":"PUBLIC"`. `gh release list` → dernière release `v2.8.1` (2026-07-12), « Latest ». Aucun tag pour les exe 3.0 à 3.4 (`git for-each-ref refs/tags`). Le README actuel décrit les gestes et l'interface de la 3.5 (`README.md:20`) et dit « Télécharge `Chronos.exe` depuis la dernière release ».
- **Constat** : la procédure de release (publish.md + plans 31-02 / 32-07 / phase 43) s'arrête volontairement à un exe local, sans tag ni push. Ce choix tient pour un usage personnel, mais le dépôt public promet autre chose : un visiteur qui suit le README télécharge la 2.8.1, dont la chaîne de données a été purgée depuis (pont statusLine, OAuth, UIA retirés en v1.5 à v1.9) et dont le nom (`Chronos.exe`) ne suit pas la convention versionnée. Il n'existe pas non plus de lien traçable exe ↔ commit pour les 3.x : seul le sujet « release: Chronos 3.x.y » du commit le donne.
- **Impact** : un produit public incohérent avec sa documentation. Pas de rollback traçable : impossible de reconstruire un `Chronos-v3.3.1.exe` à l'identique sans retrouver le commit à la main.
- **Recommandation** : trancher explicitement et l'écrire. Soit (a) le projet est personnel : README « Installation » → « construire depuis les sources » (renvoi à `docs/publish.md`), et retirer ou marquer obsolètes les releases 2.x. Soit (b) il est distribué : tag annoté `exe-v3.5.0` (préfixe distinct des tags de milestone `vX.Y` pour mettre fin à la collision visible avec `milestone-v1.6`), release GitHub avec `Chronos-v3.5.0.exe` et son SHA-256. Dans les deux cas, ajouter un tag local léger par release pour la traçabilité, même sans push.
- **Applicabilité** : dépend de la décision produit (personnel vs distribué). En l'état, la promesse publique est fausse.
- **Statut challenge** : non challengé.

### PKG-3 — Fin de support de .NET 8 dans 5 semaines pour un exe self-contained qui embarque son runtime
- **Sévérité** : Mineur
- **Rôle** : Packaging/Déploiement
- **Localisation** : `src/Chronos/Chronos.csproj:4` (`net8.0-windows`), `CLAUDE.md` (« support jusqu'en nov. 2026 »).
- **Preuve** : nous sommes le 2026-10-03, .NET 8 LTS sort du support le 10/11/2026 (le CLAUDE.md du projet le note). En self-contained, le runtime embarqué (pack `8.0.25` restauré, `src/Chronos/obj/project.assets.json`) est figé dans l'exe : seule une republication le met à jour, Windows Update ne le patche pas.
- **Constat** : la 3.5.0 sortira avec un runtime qui ne recevra plus de correctifs de sécurité un mois plus tard. Chronos fait des appels réseau (OAuth/usage) et parse des JSON locaux : la surface reste faible, mais elle existe.
- **Impact** : dette de sécurité qui grandit dans le temps, sur un exe d'autostart.
- **Recommandation** : planifier la montée `net10.0-windows` (LTS, SDK déjà installé) au milestone suivant, Extensions.* alignées en 10.0.x. Republier au moins à chaque patch .NET tant que la cible reste net8.
- **Applicabilité** : pas bloquant pour la 3.5.0, à inscrire dans la roadmap.
- **Statut challenge** : non challengé.

### PKG-4 — Build non épinglé : SDK, runtime pack et graphe NuGet dépendent de la machine de build
- **Sévérité** : Mineur
- **Rôle** : Packaging/Déploiement
- **Localisation** : racine du dépôt (pas de `global.json`), `src/Chronos/Chronos.csproj` (pas de `RestorePackagesWithLockFile`, pas de `RuntimeFrameworkVersion`).
- **Preuve** : `git ls-files | grep -i "global.json\|lock.json"` ne renvoie rien. Le pack runtime self-contained restauré (`8.0.25`) est celui que connaît le SDK 10.0.201 installé : un autre SDK embarquerait un autre patch de runtime sans changement de code.
- **Constat** : deux postes, ou le même poste après une mise à jour du SDK, produisent des exe différents (runtime embarqué, versions transitives des Extensions.*) pour le même commit. La procédure compare des md5 d'une release à l'autre (plan 32-07), mais rien ne fige les intrants.
- **Impact** : faible pour un poste unique, mais le diagnostic d'une régression entre deux exe ne peut pas exclure l'outillage.
- **Recommandation** : `global.json` (`"version": "10.0.201", "rollForward": "latestFeature"`) et `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` avec `packages.lock.json` versionné. Optionnellement, journaliser `RuntimeInformation.FrameworkDescription` dans le diagnostic pour savoir quel runtime porte un exe donné.
- **Applicabilité** : amélioration de chaîne, à faire en même temps que PKG-3.
- **Statut challenge** : non challengé.

### PKG-5 — Exe non signés
- **Sévérité** : Mineur
- **Rôle** : Packaging/Déploiement (à recouper avec Sécurité)
- **Localisation** : les 7 `Chronos-v3.*.exe` à la racine ; `README.md:27` ; `docs/publish.md` §4.
- **Preuve** : `Get-AuthenticodeSignature` → `NotSigned` pour les 7 exe 3.1.0 à 3.4.0.
- **Constat** : l'absence de signature est assumée et documentée (SmartScreen « Éditeur inconnu »). Le barème place un binaire non signé distribué en Majeur, mais la ligne 3.x n'est distribuée nulle part (cf. PKG-2) et le binaire public 2.8.1 n'est plus le produit actuel. Mineur en l'état. **Majeur dès qu'une 3.x est publiée** : l'exe s'inscrit en autostart et se fait appeler par huit groupes de hooks de Claude Code, donc un binaire substitué sur le même chemin s'exécuterait à chaque événement de session.
- **Impact** : pas de vérification d'intégrité ou de provenance au téléchargement, ni de défense contre le remplacement local de l'exe référencé par les hooks.
- **Recommandation** : en cas de distribution, publier au minimum le SHA-256 avec chaque release ; signature (certificat OV, ou Azure Trusted Signing) si l'audience grandit.
- **Applicabilité** : liée à la décision de PKG-2.
- **Statut challenge** : non challengé.

### PKG-6 — Emplacement d'installation de fait = racine du dépôt de dev, référencée par les hooks et l'autostart
- **Sévérité** : Mineur
- **Rôle** : Packaging/Déploiement
- **Localisation** : `docs/publish.md` §2 (« Copier ensuite la sortie à la racine du dépôt ») ; `.gitignore:22` ; `src/Chronos/Services/SessionHookInstaller.cs:137` (chemin absolu de l'exe dans chaque commande de hook).
- **Preuve** : les exe publiés vivent dans `…\PROJET OVERLAY\Chronos-v*.exe`. Ils sont ignorés par git, donc un `git clean -fdX`, un re-clone ou un déplacement du dossier de projet les supprime. Les huit hooks (`"<chemin>/Chronos-vX.Y.Z.exe" --hook …`) et le `.lnk` pointent sur ce chemin.
- **Constat** : le dossier de travail sert aussi de dossier d'installation. Les releases s'y accumulent (7 exe, ≈ 545 Mo) sans politique de rétention : on ne sait pas quand une version peut être supprimée sans casser une session encore ouverte (`publish.md:142` laisse ce choix à l'utilisateur).
- **Impact** : un nettoyage banal du dépôt casse silencieusement le widget de sessions (les hooks appellent un exe absent) et l'autostart.
- **Recommandation** : sans aller jusqu'à un installeur, définir un dossier d'installation stable sous le profil (`%LOCALAPPDATA%\Programs\Chronos\`) où la release copie `Chronos-vX.Y.Z.exe`. Documenter une règle de rétention, par exemple garder N-1 et supprimer les versions antérieures une fois toutes les sessions Claude Code redémarrées.
- **Applicabilité** : confort et robustesse, non bloquant pour la 3.5.0.
- **Statut challenge** : non challengé.

### PKG-7 — Artefacts et documentation de déploiement périmés dans le dépôt public
- **Sévérité** : Mineur
- **Rôle** : Packaging/Déploiement
- **Localisation** : `scripts/README.md`, `scripts/install-bridge.mjs`, `scripts/chronos-statusline-bridge.js` ; `README.md:197-199`, `README.md:206` ; `src/Chronos/Chronos.csproj:15` (commentaire).
- **Preuve** : `scripts/README.md` présente encore le pont Node comme « la **source primaire** de Chronos » et `install-bridge.mjs` comme l'installeur qui branche une `statusLine` dans `~/.claude/settings.json`, alors que la 3.5 retire toute barre de statut. Aucune référence à ces scripts dans `src`, `tests`, `docs` ni le README. Le README annonce une sortie `publish/Chronos.exe` sans l'étape de renommage versionné, et « plus de 1 600 tests » (le brief en compte ~1950). Le commentaire du csproj dit `Chronos-vX.Y.exe` au lieu de `X.Y.Z`.
- **Constat** : un installeur obsolète qui modifie `settings.json` reste exécutable et documenté. Ce n'est pas dangereux pour la réconciliation : une commande `node …bridge.js` n'est pas reconnue comme Chronos par `IsChronosCommand` (`ClaudeSettingsJson.cs:87-96`), elle serait classée « Tierce » et laissée intacte, mais elle n'alimenterait plus rien.
- **Impact** : confusion pour un lecteur du dépôt. Un utilisateur qui lance l'installeur obtient une barre orpheline, que la 3.5 ne retirera jamais.
- **Recommandation** : retirer `scripts/` (bridge + installeur + fixture) ou le marquer « historique, ne pas utiliser » ; aligner README §publication sur `docs/publish.md` (nom versionné) ; corriger le commentaire du csproj ; ne plus donner de compte de tests en dur.
- **Applicabilité** : à traiter dans la mise à jour documentaire de la phase 43 (critère 2).
- **Statut challenge** : non challengé.

### PKG-8 — Points conformes (pour mémoire)
- **Sévérité** : Info
- **Rôle** : Packaging/Déploiement
- **Localisation** : `src/Chronos/Chronos.csproj:28-37`, `win-x64.pubxml`, `app.manifest`, `VersionPublieeTests.cs`, `ClaudeSettingsReconciler.cs`.
- **Preuve / Constat** : propriétés de publication conditionnées (le build Debug/Release reste framework-dependent et rapide) ; `PublishTrimmed=false`, pas d'AOT, globalisation conservée ; `Environment.ProcessPath` partout, aucun `Assembly.Location` dans `src` ; manifeste asInvoker implicite + PerMonitorV2 ; version unique tenue par un test qui ne fige aucune valeur ; réconciliation idempotente, sauvegarde avant écriture, écriture atomique, abandon si fichier illisible ; hooks en chemin absolu entre guillemets (espaces du chemin `PROJET OVERLAY` gérés) ; 0 avertissement en Release.
- **Recommandation** : aucune.
- **Statut challenge** : non challengé.

## Compte par sévérité

| Bloquant | Majeur | Mineur | Info |
|---|---|---|---|
| 0 | 2 | 5 | 1 |

## Non vérifié faute d'outil ou par consigne
- `dotnet publish` non lancé (consigne) : taille, absence de DLL à côté de l'exe et smoke `--hook` de la 3.5.0 non revérifiés. Le dernier artefact publié (3.4.0) date du 27/09.
- Aucun exe lancé : le scénario de PKG-1 (3.4.0 relancée par l'autostart après la 3.5) est déduit du code des deux versions, pas observé. La perte des réglages 3.5 par un Save de la 3.4 est une déduction (pas de `[JsonExtensionData]`), non reproduite.
- Contenu réel de `shell:startup` et de `~/.claude/settings.json` sur la machine non lu : la cible actuelle du `.lnk` et des hooks n'est pas connue.
- Signature : seul le statut Authenticode a été lu. Pas de vérification SmartScreen/réputation.
