---
phase: 31-crit-publi-constat
plan: 02
subsystem: release
tags: [release, version, diagnostic, publication, mono-fichier, smoke, val-03, mutation, xunit, tdd]

sha_entree_de_plan: 118a921
sha_release: f321180
one_liner: "Chronos 3.2.0 est publié sous son nom, vérifié sans être lancé, et commité. Le rapport de diagnostic dit « Version : » juste sous « Date : », valeur lue sur AssemblyInformationalVersionAttribute et jamais écrite en dur. VersionPublieeTests exige la cohérence des quatre propriétés du csproj entre elles et avec l'assembly compilée. Mutations (v1) à (v3) jouées puis révoquées ; sous (v3), le SDK ajoute bien « +<sha> ». Le csproj passe à 3.2.0 (4 lignes), docs/publish.md suit la procédure réelle (nom versionné, smoke --hook, §7 premier lancement et réconciliation). Chronos-v3.2.0.exe : 77 377 466 o, 0 DLL, VersionInfo 3.2.0.0 / 3.2.0, md5 2cffcec5a50a7b296b869f4fb5732170, ignoré par git. Smoke --hook SessionStart sur stdin vide : code 0, settings.json inchangé (md5 9eab8a8e… avant et après), aucun processus résident. L'overlay n'a jamais été lancé. Tests : 1149 → 1152 (+3), 0 échec, deux exécutions. Release f321180, sans étiquette ni push."

requires:
  - phase: 31-crit-publi-constat
    provides: "31-01 : contrat de la source app-bureau et ses gardes (1149 / 0 à l'entrée, docs et tests seulement)"
  - phase: 15-idempotence-des-int-grations
    provides: "ClaudeSettingsReconciler (PUR-03, 9d5388f : réconciliation au lancement, sauvegarde avant écriture), inchangé depuis cd26b31"
provides:
  - "Ligne « Version : » de l'en-tête du rapport de diagnostic, lue sur l'assembly (VersionEmbarquee)"
  - "VersionPublieeTests : cohérence des quatre propriétés de version du csproj et de l'assembly compilée"
  - "Chronos.csproj en 3.2.0 (Version, FileVersion, AssemblyVersion, InformationalVersion)"
  - "docs/publish.md à jour : nom versionné, taille mesurée, smoke --hook sans overlay, §7 premier lancement et réconciliation"
  - "Chronos-v3.2.0.exe à la racine du dépôt (non suivi, ignoré par /Chronos-v*.exe), prêt pour le premier lancement par l'utilisateur"
affects: [31-03-constat]

tech-stack:
  added: []
  patterns:
    - "Version : cohérence, pas épinglage. Les tests tiennent l'égalité csproj ↔ csproj ↔ assembly ; la valeur publiée est tenue par grep et VersionInfo au moment de la release"
    - "Smoke d'un exe publié par son mode --hook sur stdin vide (sortie avant tout Host), jamais par l'overlay"

key-files:
  created:
    - tests/Chronos.Tests/VersionPublieeTests.cs
  modified:
    - src/Chronos/Services/DiagnosticService.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - src/Chronos/Chronos.csproj
    - docs/publish.md

key-decisions:
  - "D-31-04 : la version se lit, elle ne s'écrit pas. Le rapport lit AssemblyInformationalVersionAttribute (une seule source : le csproj) ; aucun test n'épingle « 3.2.0 »"
  - "D-31-05 : l'exe va à la racine du dépôt PRINCIPAL (git rev-parse --git-common-dir), ici l'arbre courant ; Chronos-v3.1.0.exe n'est pas supprimé"
  - "D-31-06 : le .gitignore (/Chronos-v*.exe) est vérifié par git check-ignore, pas modifié ; git add par chemins explicites uniquement"
  - "Release 3.2.0 = commit f321180, sans étiquette (milestone-v1.7 à la clôture du milestone), sans push"

requirements-completed: []
requirements-advanced: [VAL-03]

duration: 10min
completed: 2026-09-26
---

# Phase 31 Plan 02 : Chronos 3.2.0 publié sous son nom, vérifié sans être lancé — Summary

**La version 3.2.0 est embarquée à deux endroits : les propriétés du fichier (VersionInfo 3.2.0.0 / 3.2.0) et le rapport
de diagnostic (ligne « Version : », lue sur l'assembly). Elle figure aussi dans le nom du fichier publié,
`Chronos-v3.2.0.exe`, qui attend l'utilisateur à la racine du dépôt. Une garde rougit si les quatre propriétés du csproj
divergent entre elles ou de l'assembly compilée. L'exe a été publié, contrôlé et soumis au smoke `--hook` sans jamais
être lancé en overlay. Le commit de release `f321180` le date. Tests : 1149 → 1152 (+3), 0 échec, deux exécutions
consécutives.**

## Performance

- **Début :** 2026-09-26T10:49:06Z. **Fin :** 2026-09-26T10:59Z environ. **Durée :** environ 10 min.
- **SHA d'entrée :** `118a921`, à **1149 verts / 0 échec**, mesuré à l'entrée (9 s).
- **Exécution :** en séquence après 31-01, seul sur l'arbre, sans arbre de travail isolé. Le dépôt principal est l'arbre
  courant : `git rev-parse --path-format=absolute --git-common-dir` rend `<dépôt>/.git`. Les totaux ci-dessous sont
  donc ceux de l'arbre réel, soit « isolé + 6 », comme le demande la consigne en tête du plan.
- **Tâches :** 2 sur 2, soit 3 commits de tâche : RED et GREEN pour la tâche 1, release pour la tâche 2.
- **Fichiers :** les 5 de `files_modified` (126 insertions, 12 suppressions). `31-VALIDATION.md` n'est pas modifié.
  `git diff --stat 118a921..HEAD -- src/Chronos/ViewModels src/Chronos/Views src/Chronos/Resources` est vide.

## Mesures (tests)

| Moment | Filtre de la tâche (6 classes) | Suite complète |
|---|---|---|
| Entrée (`118a921`) | — | **1149 / 0** (9 s) |
| Tâche 1, RED (`864f398`) | **1 échec / 79** | — |
| Tâche 1, GREEN (`4c03cce`) | 79 / 79 | **1152 / 0** (8 s) = 1149 + 3 |
| Tâche 2, après le bump 3.2.0 | — | **1152 / 0**, deux exécutions consécutives (9 s, 9 s) |

Le total est atteint exactement : 1152 en séquence, soit 1146 si le plan avait été isolé, plus les 6 gardes
documentaires de 31-01. Après le bump, le test de cohérence lit le csproj 3.2.0 **et** l'assembly recompilée.

## Le rouge (TDD)

**1 rouge sur 79 :** `Le_rapport_dit_la_version_embarquee`, avec « Expected: "Version : 3.1.0" / Actual: "" ». La ligne 3
du rapport était encore la ligne vide qui suit « Date : ». Les deux cas de `VersionPublieeTests` sont **verts au RED
par construction**, comme le plan l'annonçait : le csproj 3.1.0 est cohérent. Ce sont des gardes, et les mutations (v1)
et (v3) les falsifient.

## Mutations : jouées, constatées, révoquées

Chaque fichier a été copié dans le bloc-notes de session avant la mutation, puis restauré par copie.

| Mutation | Fichier (sha256 avant = après) | Rouges constatés (filtre de 79) |
|---|---|---|
| **(v1)** `<InformationalVersion>3.1.0` → `3.1.1` | `Chronos.csproj` `4bf40a19…29b9c211` | **1 / 79** : `Les_quatre_proprietes_de_version_du_csproj_sont_coherentes` |
| **(v2)** ligne `sb.AppendLine("Version : " + VersionEmbarquee());` retirée | `DiagnosticService.cs` `49c2ef63…274d2b3e` | **1 / 79** : `Le_rapport_dit_la_version_embarquee` |
| **(v3)** `IncludeSourceRevisionInInformationalVersion` → `true` | `Chronos.csproj` `4bf40a19…29b9c211` | **3 / 79** : `Les_quatre_proprietes…` (« Expected: "false" / Actual: "true" »), `L_assembly_compilee_porte_la_version_du_csproj` (« Expected: "3.1.0" / Actual: "3.1.0+864f398f8b95…" ») et `Le_rapport_dit_la_version_embarquee` (anti-muet `^\d+\.\d+\.\d+$` : « Pattern not found ») |

**(v3) : le SDK ajoute bien le suffixe.** Sous (v3), la version informative compilée devient `3.1.0+<sha de HEAD>` : le
SDK .NET 10, qui cible net8.0-windows dans un dépôt git, ajoute le suffixe. Les deux gardes qui lisent l'assembly
rougissent donc aussi, en plus de celle qui lit la propriété.

sha256 complets, avant et après révocation :
- `src/Chronos/Chronos.csproj` (3.1.0, avant le bump) : `4bf40a19fed754e54e9c970088852387d270579f7fa8357e86c792b329b9c211` ;
- `src/Chronos/Services/DiagnosticService.cs` (GREEN) : `49c2ef63a4536f390f1abdb917d6688e87e3fe52f3c7c70f5f65655e274d2b3e`.

Après chaque révocation, le sha256 est identique et `git diff -- src/Chronos/Chronos.csproj` est vide. Une suite
complète a recompilé l'ensemble : 1152 / 0. Aucune mutation n'a été commitée.

## La release

| Contrôle | Attendu | Mesuré |
|---|---|---|
| Bump du csproj | 4 lignes ; `3.2.0` × 4, `3.1.0` × 0, 0 CR | **4 lignes remplacées ; 4 / 0 ; 0 CR** |
| `dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true` | code 0 | **code 0** (32 s) |
| Sortie `publish/` | `Chronos.exe` (+ `.pdb`), 0 DLL | **`Chronos.exe` 77 377 466 o + `Chronos.pdb` 178 956 o ; 0 DLL** |
| Taille | < 120 000 000 o (~77 Mo) | **77 377 466 o** (77,4 Mo ; 73,8 Mio) ; la 3.1.0 faisait 77 218 013 o |
| VersionInfo de la sortie | 3.2.0.0 / 3.2.0 / Chronos | **FileVersion 3.2.0.0, ProductVersion 3.2.0, ProductName Chronos** |
| md5 de la sortie | ≠ `ab93b3270abdec55cec42ac710552661` (3.1.0) | **`2cffcec5a50a7b296b869f4fb5732170`** |
| Copie `Chronos-v3.2.0.exe` | md5 égal à celui de la sortie ; VersionInfo 3.2.0.0 / 3.2.0 | **`2cffcec5a50a7b296b869f4fb5732170` ; 3.2.0.0 / 3.2.0** |
| `git check-ignore -v Chronos-v3.2.0.exe` | `/Chronos-v*.exe` | **`.gitignore:22:/Chronos-v*.exe`**, code 0 ; `git status` ne le montre pas |
| `Chronos-v3.1.0.exe` | toujours présent, intact | **présent, md5 `ab93b327…` inchangé** ; l'overlay 3.1.0 (PID 40772) tourne toujours |

**Où l'exe attend l'utilisateur :** `%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.2.0.exe`.

### Smoke `--hook SessionStart`, entrée standard vide

| Relevé | Valeur |
|---|---|
| `~/.claude/settings.json` avant publication (12:56:47) | md5 `9eab8a8ecdb7f1d541841dd1c6ea418b`, 3 384 o, modifié le 2026-09-25 à 11:07:56, 9 occurrences de `Chronos-v3.1.0.exe` |
| Processus `Chronos-v3.2.0.exe` avant | 0 |
| md5 juste avant le smoke (12:58:00) | `9eab8a8ecdb7f1d541841dd1c6ea418b` |
| `"…/Chronos-v3.2.0.exe" --hook SessionStart < /dev/null` | **code 0**, sortie à 12:58:04 |
| md5 après | **`9eab8a8ecdb7f1d541841dd1c6ea418b`, identique** (taille et date inchangées) |
| Processus `Chronos-v3.2.0.exe` après | **0** ; seul `Chronos-v3.1.0.exe` PID 40772, l'overlay de l'utilisateur, tourne |

Le smoke s'exécute dans le processus de l'agent, qui voit la vue **virtualisée** d'AppData. Il prouve seulement que le
mode `--hook` sort proprement sans rien écrire. Il ne prouve rien de la réconciliation, qui n'a lieu qu'en mode
overlay, au premier lancement par l'utilisateur, au plan 31-03. Seule écriture attendue de l'hôte mono-fichier : le
dossier d'extraction native `%LOCALAPPDATA%\Temp\.net\Chronos-v3.2.0\`, créé à 12:58 vu depuis ce processus. Il est
hors du périmètre « aucune écriture » (31-RESEARCH, 1.b).

**L'overlay n'a jamais été lancé par l'agent**, ni sans argument ni avec `--statusline`. Aucune option `--install`,
aucune réconciliation.

### Commit de release

`f321180` — `release: Chronos 3.2.0 - milestone v1.7 << Lue ou non lue >>`. `git show --stat` : exactement
`docs/publish.md` et `src/Chronos/Chronos.csproj`. Le corps donne la taille, le md5, VersionInfo, le smoke et les tests
(1152 / 0, deux exécutions). `git tag --points-at HEAD` est vide, et rien n'a été poussé.

## Task Commits

1. **Tâche 1 : le rapport dit la version embarquée, et la version du csproj est une.**
   - `864f398` (test, RED 1/79)
   - `4c03cce` (feat, GREEN 79/79, suite 1152/0)
2. **Tâche 2 : Chronos 3.2.0 publié sous son nom, vérifié sans être lancé, et commité.**
   - `f321180` (release, suite 1152/0 deux fois)

**Plan metadata :** commit `docs(31-02)` final (SUMMARY, STATE, ROADMAP).

## Critères grep

| Critère | Attendu | Mesuré |
|---|---|---|
| `sb.AppendLine("Version : " + VersionEmbarquee());` (DiagnosticService.cs) | 1 | **1** |
| `3.1.0` / `3.2.0` dans DiagnosticService.cs | 0 / 0 | **0 / 0** |
| `Inspecter(` dans DiagnosticService.cs | 1 | **1** |
| `IPremierPlan`, `PremierPlanWin32`, `.Lire(`, `new LecteurAppBureau`, `RacinesEtat`, `new SessionMonitor` | 0 | **0** |
| DiagnosticService.cs CR = LF | égaux | **924 = 924** |
| `[Fact]` dans VersionPublieeTests.cs ; CR = LF, sans BOM | 2 ; égaux | **2 ; 57 = 57, sans BOM** |
| DiagnosticServiceTests.cs : insertions seulement | oui | **22 insertions, 0 suppression ; CR = LF 1313** |
| `3.2.0` / `3.1.0` dans Chronos.csproj ; CR | 4 / 0 ; 0 | **4 / 0 ; 0** |
| `106/106` / `le laisser vivre ~8 s` (publish.md) | 0 / 0 | **0 / 0** |
| `--hook SessionStart` / `^## 7. Premier lancement et réconciliation` / `Chronos-v<X.Y.Z>.exe` | ≥ 1 / 1 / ≥ 1 | **1 / 1 / 2** |
| CR dans publish.md | 0 | **0** |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `using System.IO;` ajouté à `VersionPublieeTests.cs`**
- **Trouvé pendant :** tâche 1, RED.
- **Problème :** erreurs CS0103 (`Path` et `File` inconnus). Dans un projet `UseWPF`, les usings implicites n'importent
  pas `System.IO`, à cause du conflit avec `System.Windows.Shapes.Path`.
- **Correctif :** `using System.IO;` en tête de la classe, comme dans `GardesPerimetreTests.cs`. Il a été ajouté avant
  le commit RED ; le RED commité compile et rougit pour la bonne raison.
- **Commit :** `864f398`.

### Le code fait foi sur le texte attendu

**2. `docs/publish.md` §7 : précision tirée du code.** `App.xaml.cs` appelle
`Reconcile(settings.SessionsWidgetEnabled)` : les huit groupes ne sont repointés que si le widget de sessions est
activé, et ils sont retirés s'il est désactivé. Le texte du plan devient donc « repointe les huit groupes de hooks
(widget de sessions activé ; désactivé, il les retire) et la statusLine… ». Le chemin de sauvegarde
`claude-settings-yyyyMMdd-HHmmss` et le libellé « Quitter Chronos » ont été vérifiés dans le code.

**3. `docs/publish.md` §6, point 2 :** « (attendu ~60-70 Mo) » devient « (mesuré ~77 Mo) », au nom du même fait que la
taille du §2, qui figure au plan. Le point 2 ne figurait pas dans la liste du plan. Le tableau du §3 garde son
estimation historique « ~140 Mo → ~60-70 Mo », justification de `EnableCompressionInSingleFile` hors de la liste du
plan : c'est une incohérence mineure, laissée en l'état.

**4. Message de release :** la phrase du plan « s'y ajoutent à la fusion » visait l'exécution isolée. En séquence, elle
devient « les 6 gardes documentaires de 31-01, sans code, en font partie et n'entrent pas dans l'exe ».

### Forme et méthode

**5. §5 remis à la largeur du document** après le remplacement « (menu contextuel) » → « (fenêtre de réglages, clic
droit sur le cadran) ». Le texte est inchangé.

**6. Trois faux pas d'outillage, tous corrigés avant commit.**
- Le premier script d'insertion du test laissait la ligne vide du mauvais côté. Il a été annulé par `git checkout`,
  puis rejoué.
- Dans `publish.md`, un `\b` du chemin de sauvegarde a été interprété par Python comme un retour arrière. Il a été
  corrigé par `chr(92)` ; aucun caractère de contrôle ne reste dans le fichier (0).
- Le heredoc Bash qui devait écrire ce SUMMARY a échoué au parsing (« unexpected EOF while looking for matching `'` »),
  comme en phases 29 et 31-01. Rien n'a été écrit, et le SUMMARY a été posé avec l'outil d'écriture.

Aucun d'eux n'a atteint un commit de code.

**Total :** 1 correctif automatique (Rule 3) et 3 formulations tirées du code ou du contexte. S'y ajoutent 2 écarts de
forme ou de méthode. Aucune garde n'a été assouplie, et aucune assertion existante n'a été touchée.

## Known Stubs

Aucun. `TODO|FIXME|placeholder|coming soon` ne donne rien dans les fichiers créés ou modifiés.

## Exigences

- **VAL-03 : avancée, non cochée.** Le critère 2 de la phase est vérifiable. La version 3.2.0 est embarquée dans les
  propriétés du fichier (VersionInfo) et dans le diagnostic (ligne « Version : », sous garde), et elle figure dans le
  nom du fichier publié. Il reste la réconciliation des hooks et de la statusLine vers `Chronos-v3.2.0.exe` au premier
  lancement. L'utilisateur la fera en 31-03, et elle se constate dans le fichier. VAL-03 reste **Pending** jusque-là.

## Next Phase Readiness

**31-03 (réconciliation et constat) :**
- l'exe attend à `%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.2.0.exe`, md5
  `2cffcec5a50a7b296b869f4fb5732170`, 77 377 466 o ;
- `~/.claude/settings.json` avait le md5 `9eab8a8ecdb7f1d541841dd1c6ea418b` à 12:58:04, avec 9 occurrences de
  `Chronos-v3.1.0.exe`. Le re-relever juste avant le lancement : c'est la valeur que la sauvegarde neuve doit
  reproduire ;
- l'overlay 3.1.0 (PID 40772) tourne encore. L'utilisateur doit le quitter par réglages → « Quitter Chronos » avant
  de lancer la 3.2.0 par l'Explorateur ;
- une fois lancé, « Diagnostic… » doit afficher `Version : 3.2.0` en ligne 3 ;
- `chronos.log` ne prouve pas la réconciliation : il est écrit avant elle ;
- reporter dans `31-VALIDATION.md` les valeurs de ce SUMMARY : RED 1/79 ; (v1) 1, (v2) 1, (v3) 3 rouges ; 1152
  deux fois ; taille, md5, VersionInfo, md5 du smoke, SHA de release `f321180`.

## Self-Check: PASSED

- FOUND : `tests/Chronos.Tests/VersionPublieeTests.cs`, `src/Chronos/Services/DiagnosticService.cs` (ligne Version),
  `docs/publish.md` (§7), `Chronos-v3.2.0.exe` (racine, md5 `2cffcec5…`), `Chronos-v3.1.0.exe` (intact), ce SUMMARY
- FOUND : commits `864f398`, `4c03cce`, `f321180` (et la référence d'entrée `118a921`)
- Suite : 1152 / 0, deux exécutions consécutives (9 s, 9 s), sur le code final ; aucune étiquette, aucun push.

---
*Phase : 31-crit-publi-constat*
*Terminé : 2026-09-26*
