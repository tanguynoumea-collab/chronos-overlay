---
phase: 35-4-semaines-acc-s-release-3-3-0
plan: 06
subsystem: release
tags: [ACC-04, release, 3.3.0, publish, mono-fichier, smoke, zero-warning]

sha_entree_de_plan: 7d3fdc8
sha_release: 9b74ed5
one_liner: "Chronos 3.3.0 (v1.8 Historique d'utilisation) est publié sous son nom, contrôlé sans être lancé, et commité (9b74ed5, csproj seul, sans tag ni push) : 78 104 918 o, 0 DLL, VersionInfo 3.3.0.0 / 3.3.0 / Chronos, md5 7ec9fbcf95c5716e6b5bbce77ea9c0bc (≠ 3.2.2 51f4d95b…). Smoke --hook SessionStart stdin vide : code 0, ~/.claude/settings.json md5 b010cd87… identique avant/après, 0 processus 3.3.0 résident ; les quatre anciens overlays (3.1.0, 3.2.0, 3.2.1, 3.2.2) intacts. Suite 1648 / 0, quatre exécutions (deux avant, deux après le bump), 0 avertissement Release."

requires:
  - phase: 35-01
    provides: "vue 4 semaines côté données et VM, veille de minuit de la vue Jour"
  - phase: 35-02
    provides: "deux gestes d'ouverture (double-clic au centre, carte des réglages), ouvreur singleton"
  - phase: 35-03
    provides: "section [Journal d'historique] du diagnostic"
  - phase: 35-04
    provides: "VueQuatreSemainesView, segment 4 semaines actif (HIS-05), infobulle bornée, annotations Jour sur deux rangées"
  - phase: 35-05
    provides: "README « Historique d'utilisation », data-sources §9, publish.md note de premier lancement 3.3.0, fuseau du diagnostic"
  - phase: 32-07
    provides: "procédure de release (bump × 4, suite × 2, publish mono-fichier, contrôles, copie versionnée, smoke --hook, commit sans étiquette)"
provides:
  - "src/Chronos/Chronos.csproj en 3.3.0 (Version, FileVersion 3.3.0.0, AssemblyVersion 3.3.0.0, InformationalVersion)"
  - "Chronos-v3.3.0.exe à la racine du dépôt (non suivi, ignoré par .gitignore:22:/Chronos-v*.exe), prêt pour le premier lancement par l'utilisateur"
affects: [35-07-constat]

tech-stack:
  added: []
  patterns:
    - "Release : même procédure que 31-02 / 32-07 ; la version se lit (VersionInfo, grep), ne s'épingle pas dans un test"

key-files:
  created: []
  modified:
    - src/Chronos/Chronos.csproj

key-decisions:
  - "D-35-20 — même procédure que 32-07 (D-32-33 / D-32-34) : exe à la racine du dépôt principal, .gitignore vérifié et non modifié ; le smoke --hook ne prouve ni le verrou ni le journal ni la fenêtre, constatés en 35-07. Référence : 3.2.2 = 77 557 997 o, md5 51f4d95bb346b9a4ce33f3f63f3d434c"

requirements-completed: [ACC-04]

duration: 6min
completed: 2026-09-27
---

# Phase 35 Plan 06 : Chronos 3.3.0 publié, contrôlé sans être lancé, et commité — Summary

**`Chronos-v3.3.0.exe` attend l'utilisateur à la racine du dépôt, version 3.3.0 dans les quatre propriétés du csproj,
dans son VersionInfo et dans son nom. Il a été publié, contrôlé et soumis au seul smoke `--hook` autorisé ; l'agent n'a
lancé ni l'overlay ni la galerie `--historique`. Le commit de release `9b74ed5` ne touche que le csproj.**

## Performance

- **Début :** 2026-09-27T17:41:50Z. **Fin :** 2026-09-27T17:47Z environ. **Durée :** environ 6 min (dont ≈ 2 min 30 de
  tests, 41 s de publication).
- **SHA d'entrée :** `7d3fdc8`, arbre propre.
- **Exécution :** seul sur l'arbre réel (vague 3), stage explicite du csproj, pas d'amend, aucun tag, aucun push.
- **Tâches :** 2 sur 2. La tâche 1 (porte, bump, suite) n'a pas de commit propre : le bump est commité dans le commit de
  release de la tâche 2, comme le plan le demande (un seul commit de release, csproj seul).

## Mesures (tests)

| Moment | Suite complète (`dotnet test Chronos.sln -c Debug --nologo -v q`) | Avertissements |
|---|---|---|
| Entrée `7d3fdc8`, avant bump | **1648 / 0**, deux exécutions (32 s, 25 s) | Release : **0 avertissement, 0 erreur** ; 0 ligne « warning » dans les tests |
| Après bump 3.3.0 | **1648 / 0**, deux exécutions (27 s, 26 s) | Release : **0 avertissement, 0 erreur** |
| `--filter FullyQualifiedName~VersionPubliee` après bump | **2 / 2** (relit 3.3.0 dans le csproj et l'assembly) | — |

## Le bump

| Critère | Attendu | Mesuré |
|---|---|---|
| `grep -cE "<(Version\|InformationalVersion)>3\.3\.0</\|<(FileVersion\|AssemblyVersion)>3\.3\.0\.0</"` | 4 | **4** |
| `grep -c "3\.2\.2"` | 0 | **0** |
| CR dans le csproj | 0 | **0** (LF conservé) |
| `git diff --stat` | 4 −, 4 + | **4 insertions, 4 suppressions** ; bloc de publication conditionnel intact |

## La release

| Contrôle | Attendu | Mesuré |
|---|---|---|
| `dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true` | code 0 | **code 0** (17:44:47Z → 17:45:28Z, 41 s) |
| Sortie `publish/` | `Chronos.exe` (+ `.pdb`), 0 DLL | **`Chronos.exe` 78 104 918 o + `Chronos.pdb` 305 556 o ; 0 DLL** |
| Taille | < 120 000 000 o | **78 104 918 o** (78,1 Mo ; 74,5 Mio) ; 3.2.2 = 77 557 997 o (+546 921 o : fenêtre Historique) |
| VersionInfo de la sortie | 3.3.0.0 / 3.3.0 / Chronos | **FileVersion 3.3.0.0, ProductVersion 3.3.0, ProductName Chronos** |
| md5 de la sortie | ≠ `51f4d95bb346b9a4ce33f3f63f3d434c` | **`7ec9fbcf95c5716e6b5bbce77ea9c0bc`** |
| Copie `Chronos-v3.3.0.exe` | md5 égal ; VersionInfo 3.3.0.0 / 3.3.0 | **`7ec9fbcf95c5716e6b5bbce77ea9c0bc` ; 3.3.0.0 / 3.3.0** (absente avant la copie) |
| `git check-ignore -v Chronos-v3.3.0.exe` | `/Chronos-v*.exe` | **`.gitignore:22:/Chronos-v*.exe`** ; `git status` ne le montre pas |
| Anciens exe | 3.1.0 / 3.2.0 / 3.2.1 / 3.2.2 intacts | **présents** (77 218 013 / 77 377 466 / 77 385 116 / 77 557 997 o) ; 3.2.2 md5 `51f4d95b…` inchangé |

**Où l'exe attend l'utilisateur :** `%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.3.0.exe`.

### Smoke `--hook SessionStart`, entrée standard vide (D-35-20)

| Relevé | Valeur |
|---|---|
| `~/.claude/settings.json` avant publication (17:44:45Z) | md5 `b010cd8733344a134875d2c8a8d43b37`, 3 384 o, modifié le 2026-09-27 à 08:07:10 (+02:00) |
| Processus Chronos avant | `Chronos-v3.1.0.exe` PID 40772, `Chronos-v3.2.0.exe` PID 126160, `Chronos-v3.2.1.exe` PID 121900, `Chronos-v3.2.2.exe` PID 87604 ; 0 × 3.3.0 |
| md5 juste avant le smoke (17:45:53Z) | `b010cd8733344a134875d2c8a8d43b37` |
| `"…/Chronos-v3.3.0.exe" --hook SessionStart < /dev/null` | **code 0**, sortie à 17:45:58Z |
| md5 après | **`b010cd8733344a134875d2c8a8d43b37`, identique** (taille et date inchangées) |
| Processus `Chronos-v3.3.0.exe` après | **0** ; les quatre anciens overlays toujours là, mêmes PID |

Le smoke sort avant le Host : il prouve que l'exe démarre et sort proprement sans écrire. Il ne prouve ni le verrou
mono-instance, ni le journal, ni la fenêtre Historique : ils se constatent avec l'utilisateur en 35-07. **L'agent n'a
lancé ni l'overlay ni la galerie** ; aucun processus Chronos n'a été arrêté.

### Commit de release

`9b74ed5` — `release: Chronos 3.3.0 - v1.8 Historique d'utilisation`. `git show --stat HEAD` : exactement
`src/Chronos/Chronos.csproj` (4 +, 4 −). Le corps donne le périmètre, la taille, le md5, VersionInfo, le smoke et les
tests (1648 / 0, deux exécutions). `git tag --points-at HEAD` est vide ; rien n'a été poussé. La publication GitHub est
l'étape ZEUS après le constat.

## Ce que contient la 3.3.0 (pour le constat 35-07)

- **Fenêtre Historique d'utilisation** (phase 34 + 35) : Semaine de forfait en trois styles, vue Jour (lit la veille de
  minuit, interruptions dites sans rien inventer), vue **4 semaines** (S-3 / S-2 / S-1 en gris d'opacités croissantes, S
  par bandes de la rampe du thème, « avant le journal — aucun relevé » hachuré, marqueur « journal ouvert le … »,
  annotation « épuisée … → bloquée jusqu'au reset » reportée sur S, pied « Rien n'est inventé avant l'ouverture du
  journal. ») — HIS-05.
- **Deux gestes d'ouverture** : double-clic au centre du cadran (le simple clic bascule après le délai de double-clic
  ≈ 0,5 s), carte « Historique d'utilisation » dans les réglages ; fenêtre singleton, VM survivant aux réouvertures.
- **Diagnostic** : section `[Journal d'historique]` (dossier, fuseau, fichiers, âge de la dernière écriture, journée lue
  par la même façade que la fenêtre, derniers événements, reconstruction des tokens, nombre d'instances Chronos).
- **Docs** : README « Historique d'utilisation », `docs/data-sources.md` §9, `docs/publish.md` note de premier lancement
  3.3.0.
- Infobulles bornées au bord droit, annotations Jour sur deux rangées (reprises 34-08).

## Task Commits

1. **Tâche 1 : porte zéro avertissement, bump 3.3.0 × 4, suite × 2** — pas de commit propre (bump inclus dans `9b74ed5`)
2. **Tâche 2 : publication, contrôles, copie versionnée, smoke, commit de release** — `9b74ed5`

**Plan metadata :** commit `docs(35-06)` final (SUMMARY, REQUIREMENTS).

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

Aucun (seul le csproj est modifié).

## Exigences

- **ACC-04 : complète.** Release 3.3.0 publiée avec les contrôles de JRN-06 : version aux quatre propriétés et dans le
  nom, mono-fichier sans DLL, VersionInfo, md5, smoke `--hook` sans effet de bord ; README et data-sources documentés en
  35-05. La réconciliation au premier lancement se constate en 35-07.

## Next Phase Readiness

**35-07 (constat par l'utilisateur) :**
- l'exe attend à `%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.3.0.exe`, md5
  `7ec9fbcf95c5716e6b5bbce77ea9c0bc`, 78 104 918 o ;
- **quatre anciens overlays tournent** (3.1.0 PID 40772, 3.2.0 PID 126160, 3.2.1 PID 121900, 3.2.2 PID 87604 — la 3.2.2
  écrit le journal réel et tient `Local\Chronos-overlay`) : les quitter tous (réglages → « Quitter Chronos ») avant de
  lancer la 3.3.0 par l'Explorateur ;
- `~/.claude/settings.json` avait le md5 `b010cd8733344a134875d2c8a8d43b37` à 17:45:58Z (3 384 o) : le re-relever juste
  avant le lancement ;
- une fois lancé : « Diagnostic… » doit afficher `Version : 3.3.0` et la section `[Journal d'historique]` ; le journal
  doit porter un `demarrage` avec `"version":"3.3.0"` (vue réelle, par sonde hors arbre) ; double-clic au centre →
  fenêtre Historique, trois vues.

## Self-Check: PASSED

- FOUND : `src/Chronos/Chronos.csproj` (3.3.0 × 4), `Chronos-v3.3.0.exe` (racine, md5 `7ec9fbcf…`), `Chronos-v3.2.2.exe`
  (intact, md5 `51f4d95b…`), ce SUMMARY
- FOUND : commit `9b74ed5` (et la référence d'entrée `7d3fdc8`)
- Suite 1648 / 0 quatre fois, 0 avertissement ; aucune étiquette, aucun push ; aucun overlay lancé ; ACC-04 cochée.

---
*Phase : 35-4-semaines-acc-s-release-3-3-0*
*Terminé : 2026-09-27*
