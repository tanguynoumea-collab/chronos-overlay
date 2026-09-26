---
phase: 30-la-lecture-fait-dispara-tre
plan: 02
subsystem: widget-sessions
tags: [csharp, win32, p-invoke, premier-plan, app-bureau, selection, lecture-seule, gardes, mutation, xunit, tdd]

sha_entree_de_plan: af60f59
one_liner: "Les deux faits dont LUE-02 a besoin existent, chacun derrière une interface neutre qui sait dire « je ne sais pas ». Côté OS, IPremierPlan / PremierPlanWin32 lit le NOM du processus de la fenêtre au premier plan (GetForegroundWindow → GetWindowThreadProcessId → ProcessName), le compare par égalité sans casse avec claude (jamais le titre, jamais un préfixe, pas d'UIA) et dit depuis quand, avec un cache de 1 s, un trou de 5 s et quatre issues nommées ; une sonde en échec rend « indisponible » avec la raison. Côté app, LectureAppBureau.Selection prend le lastFocusedAt le plus récent sur TOUS les fichiers servis, doublons écartés et fichiers sans cliSessionId compris ; sans identifiant, la sélection ne désigne personne. Mutations (m7) et (m3) jouées puis révoquées (sha256 identiques). 1062 → 1084 isolé (+22), deux exécutions ; 1110 / 0 combiné en fin de vague 1 avec 30-01, deux exécutions"

requires:
  - phase: 29-02
    provides: "LecteurAppBureau (Lire, cache par date d'écriture et taille, Interpreter, Instant), liste Interdits de la lecture seule, helper RacineAppBureau"
  - phase: 29-05
    provides: "Garde n° 2 d'APP-05 (aucun Claude_ ni claude-code-sessions hors RacinesEtat.cs, commentaires compris)"
  - phase: 21
    provides: "GardesPerimetreTests : aucun type contenant Uia ni finissant par ForegroundWatch"
provides:
  - "StatutPremierPlan (NonBranche, Claude, AutreProcessus, AucuneFenetre, Indisponible)"
  - "EtatPremierPlan(Statut, Processus, Depuis, Raison) + ClaudeDepuis, la seule chose que le détecteur en recevra"
  - "IPremierPlan.Lire(now) : best-effort, jamais d'exception"
  - "PremierPlanWin32 : sonde Win32 réelle, sonde injectable (constructeur internal), Classer, cache 1 s, trou 5 s, verrou"
  - "SessionSelectionnee(CliSessionId?, DernierFocus) et LectureAppBureau.Selection (dernier paramètre, nul par défaut)"
  - "LecteurAppBureau.Interpreter(json, out meta, out focus) : le focus est lu avant l'identifiant"
  - "tests/Chronos.Tests/Fakes/FakePremierPlan.cs : faux réglable pour 30-03 et 30-04 (compteur d'appels, mode « lève »)"
affects: [30-03-moniteur-et-cablage, 30-04-diagnostic, 31-constat]

tech-stack:
  added: []
  patterns:
    - "Sonde OS mince derrière un délégué : l'état (depuis, cache, trou, horloge qui recule, panne) se prouve sans fenêtre ; la sonde réelle n'a qu'un test de fumée sans assertion de nom"
    - "Une observation qui peut dire « je ne sais pas » : issues nommées (dont Indisponible avec la raison) au lieu d'un booléen"
    - "Un maximum pris AVANT le regroupement par identifiant, pour que les fichiers écartés ou non joignables comptent"
    - "Mesure isolée d'un plan parallèle : un worktree temporaire à l'entrée de plan, ses seuls commits rejoués, puis supprimé"

key-files:
  created:
    - src/Chronos/Services/PremierPlanWin32.cs
    - tests/Chronos.Tests/PremierPlanWin32Tests.cs
    - tests/Chronos.Tests/Fakes/FakePremierPlan.cs
  modified:
    - src/Chronos/Services/LecteurAppBureau.cs
    - tests/Chronos.Tests/LecteurAppBureauTests.cs

key-decisions:
  - "D-30-04 : la session sélectionnée se calcule DANS le lecteur, sur tous les fichiers servis au cycle (valides, doublons écartés, sans cliSessionId), avant le regroupement ; ex aequo : le premier chemin ordinal ; CliSessionId nul si le maximum appartient à un fichier sans identifiant, et alors aucune session du widget n'est sélectionnée"
  - "Premier plan : cinq statuts, et non un booléen. Claude, AutreProcessus (nommé), AucuneFenetre (bascule, pas une panne), Indisponible (avec la raison), NonBranche (personne n'a regardé). Le détecteur ne reçoit que ClaudeDepuis"
  - "Depuis : tenu sur une suite ininterrompue d'échantillons claude, écart ≤ 5 s (borne incluse) et horloge qui avance ; sinon il repart de maintenant. Cache 1 s, jamais servi si l'horloge a reculé"
  - "Égalité ordinale sans casse avec claude, jamais StartsWith (« claudette » n'est pas claude), jamais le titre (un onglet « Claude » passerait) ; machine verrouillée ⇒ LockApp ⇒ AutreProcessus"
  - "Limites écrites, non parées : la CLI Claude Code s'appelle aussi claude (Open Question 3, constat en phase 31) ; un fichier de l'app écrit il y a plus de 24 h n'est pas ouvert, donc ne peut pas être sélectionné (LOW)"

requirements-completed: []

duration: 9min
completed: 2026-09-26
---

# Phase 30 Plan 02 : Ce que l'OS a au premier plan, ce que l'app a sélectionné — Summary

**LUE-02 s'appuie désormais sur deux observations honnêtes, chacune capable de dire « je ne sais pas ».
`PremierPlanWin32` dit quel processus a la fenêtre au premier plan et depuis quand c'est `claude`, sans UIA ni titre.
`LectureAppBureau.Selection` dit quelle session l'app a sélectionnée, y compris « une session neuve que rien ne joint ».
Le détecteur (30-01) et le moniteur (30-03) n'ont pas été touchés.**

## Performance

- **Duration :** ~9 min
- **Started :** 2026-09-26T08:38:36Z
- **Completed :** 2026-09-26T08:47:44Z
- **Tasks :** 2/2 (TDD, RED puis GREEN pour chacune)
- **Files :** 3 créés, 2 modifiés
- **SHA d'entrée :** `af60f59`

## Accomplishments

- **La sonde de premier plan (LUE-02, LUE-04).** `IPremierPlan` / `PremierPlanWin32` enchaîne deux `DllImport` user32
  privés (`GetForegroundWindow`, puis `GetWindowThreadProcessId(nint hWnd, out uint processId)`) et
  `Process.GetProcessById(pid).ProcessName`. Le nom est comparé par égalité ordinale sans casse avec `claude`. L'état
  « depuis » est géré sous verrou : cache de 1 s, trou de plus de 5 s, horloge qui recule. Une sonde en échec rend
  `Indisponible` avec le type de l'exception, et `ClaudeDepuis` reste nul. Aucun HWND ni type WPF dans les signatures
  publiques.
- **La sélection de l'app (LUE-02, LUE-04).** `Interpreter` lit `lastFocusedAt` AVANT `cliSessionId`, et le rend par un
  `out` supplémentaire (la surcharge à deux arguments est gardée). L'entrée de cache garde ce focus pour tout fichier
  lu. `Lire` collecte les focus servis avant le regroupement par identifiant, puis rend
  `Selection = new SessionSelectionnee(id?, focus)` : le maximum, et en cas d'égalité le premier chemin ordinal.
  `ParSession`, les compteurs et `ChampsAbsents` ne changent pas. Aucun jeton `Interdits` n'a été ajouté.
- **Le faux des plans suivants.** `FakePremierPlan` (état réglable entre deux cycles, `Appels`, `Leve`, fabriques
  `Claude` / `Autre` / `Indisponible`) est prêt pour 30-03 et 30-04.

## Task Commits

1. **Tâche 1 : IPremierPlan, PremierPlanWin32, FakePremierPlan**
   - RED `4b2f26c` : `test(30-02): la sonde de premier plan - P01 a P09, 14 cas, RED 11/14 sur squelette`
   - GREEN `b7b77cb` : `feat(30-02): ce que l'OS a au premier plan - PremierPlanWin32, le processus et depuis quand (LUE-02, LUE-04)`
2. **Tâche 2 : le focus lu avant l'identifiant, LectureAppBureau.Selection**
   - RED `1955d0c` : `test(30-02): la session selectionnee dans l'app - S01 a S08, RED 6/39 sur squelette`
   - GREEN `b10620f` : `feat(30-02): ce que l'app a selectionne - le focus lu avant l'identifiant, LectureAppBureau.Selection sur tous les fichiers (LUE-02, LUE-04)`

Pas de REFACTOR : aucun nettoyage n'était nécessaire après le vert.

## RED constatés

- **Tâche 1** (squelette : `Lire` rend toujours `NonBranche`, `Classer` rend toujours `AutreProcessus`) : **11 rouges sur 14**.
  Sont rouges P01 `Le_premier_plan_est_mis_en_cache_une_seconde`, P02 `Depuis_tient_tant_que_claude_et_se_repose_au_retour`
  et quatre cas de P03 `Le_nom_du_processus_se_compare_par_egalite_sans_casse` (`"claude"`, `"CLAUDE"`, `"Claude"`, `""`).
  Suivent P04 `Une_sonde_qui_leve_rend_indisponible_sans_exception`, P05 `Une_fenetre_nulle_est_aucune_fenetre_pas_une_panne`,
  P06 `Une_horloge_qui_recule_resonde_et_repose_depuis`, P07 `Un_trou_d_echantillonnage_repose_depuis` et
  P08 `La_sonde_reelle_ne_leve_pas_et_rend_un_statut_defini`.
  Trois cas restent verts par construction : `"claudette"`, `"explorer"` et P09, la garde au texte, que le squelette
  satisfait déjà.
- **Tâche 2** (squelette : `Selection` toujours nulle, `focus` toujours nul) : **6 rouges sur 39** —
  S01 `La_selection_est_le_dernier_focus_de_tous_les_fichiers`, S02 `Le_focus_d_un_doublon_ecarte_compte_pour_la_selection`,
  S03 `Le_dernier_focus_sur_un_fichier_sans_cliSessionId_ne_selectionne_personne`,
  S06 `A_focus_egal_la_selection_est_le_premier_chemin_ordinal`, S07 `Interpreter_rend_le_focus_meme_sans_cliSessionId`,
  S08 `Le_cache_garde_le_focus_pour_la_selection`. S04 (racine absente) et S05 (aucun focus lisible) sont verts par
  construction, comme prévu.

## Mutations (jouées, rouges constatés, révoquées)

| Id | Mutation | Rouges constatés | sha256 après révocation |
|----|----------|------------------|-------------------------|
| (m7) | `Classer` par `nomProcessus.StartsWith(ProcessusClaude, OrdinalIgnoreCase)` | `Le_nom_du_processus_se_compare_par_egalite_sans_casse(nom: "claudette", attendu: AutreProcessus)`, `La_sonde_ne_lit_ni_UI_Automation_ni_titre_de_fenetre` | `PremierPlanWin32.cs` = `4667de50c6d1d4ed0964c33520219a1b9c6871a68d7eae1771888b5a287767c7` (identique à avant) |
| (m3) | sélection calculée sur `parSession.Values` (max de `DernierFocus`) au lieu des focus servis | `Le_dernier_focus_sur_un_fichier_sans_cliSessionId_ne_selectionne_personne`, `Le_focus_d_un_doublon_ecarte_compte_pour_la_selection` (les deux exigés), plus `A_focus_egal_la_selection_est_le_premier_chemin_ordinal` et `Le_cache_garde_le_focus_pour_la_selection` | `LecteurAppBureau.cs` = `6b862e90be6025a3f7da1e0d67d4287863d46c37dc01e9439dc493e51e0a2a11` (identique à avant) |

Chaque mutation a été appliquée sur une copie de sauvegarde gardée dans le bloc-notes de session, puis révoquée par
restauration. Les deux sha256 sont identiques avant et après.

## Fumée P08 et constat sur la machine (lecture seule, overlay non lancé)

- P08 est verte : la sonde réelle ne lève pas et rend un statut défini (jamais `NonBranche`).
- Contrôle en lecture seule (PowerShell, mêmes appels user32 via `Add-Type`) : **un seul** processus `claude` a une
  fenêtre principale, et son `ProcessName` vaut `claude`. Au moment du contrôle, le premier plan se classait **`Claude`**.
  Rien n'a été écrit.

## Vérification

- Filtre de la tâche 1 (`PremierPlanWin32Tests`, `GardesPerimetreTests`, `ServicesLayerPurityTests`,
  `LectureSeuleAppBureauTests`, `HorizonsSessionsTests`, `NormalisationUniqueTests`) : **52 / 0**.
  `PremierPlanWin32Tests` compte **14** cas.
- Filtre élargi de la tâche 2 (plus `LecteurAppBureauTests`, `MoniteurAppBureauTests`, `GardesDoctrineTests`) :
  **112 / 0**. `LecteurAppBureauTests` passe de 31 à **39** cas, et `LectureSeuleAppBureauTests` reste à 6/6 sans que
  la liste `Interdits` ait été modifiée.
- Greps d'acceptation sur `PremierPlanWin32.cs` : 0 occurrence de `Claude_|claude-code-sessions|HorizonsSessions.Silence|GetWindowText|Automation`,
  0 `StartsWith`, **2** `DllImport("user32.dll"`, **1** `public interface IPremierPlan`.
- Greps d'acceptation sur `LecteurAppBureau.cs` : **1** record `SessionSelectionnee` exact, **1**
  `SessionSelectionnee? Selection = null`, **1** `new SessionSelectionnee(`, 0 `Claude_` ou `claude-code-sessions`,
  et aucun des 31 jetons `Interdits`.
- **Total isolé : 1084 / 0**, deux exécutions consécutives. Mesure faite dans un worktree temporaire à `af60f59`,
  avec seulement les 4 commits de ce plan rejoués ; le worktree a été supprimé ensuite. Cela fait 1062 + 14 + 8.
- **Total combiné, fin de vague 1 : 1110 / 0**, deux exécutions consécutives (9 s, 8 s), à `284fcc7` (après
  `f0ab69a`, dernier commit de code de 30-01). C'est exactement le total attendu : 1062 + 22 (30-02) + 26 (30-01). Une
  mesure intermédiaire à `b10620f` donnait 1103 / 0 : 30-01 n'avait alors commité que sa tâche 1 (19 cas).
- `git diff --stat af60f59..HEAD` limité aux commits de ce plan : seuls les 5 fichiers de `files_modified` sont
  touchés. `SessionMonitor.cs`, `SessionTreatmentTracker.cs` et `App.xaml.cs` ne le sont pas : le câblage revient à 30-03.
- Aucun test ne lit le vrai `%APPDATA%`, `%LOCALAPPDATA%` ni `~/.claude`. Seule P08 touche l'OS, en lecture, sans
  asserter de nom de processus.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Correction] Un nom de processus vide dans la sonde réelle dit pourquoi elle est indisponible**
- **Trouvé pendant :** tâche 1 (GREEN)
- **Problème :** selon le plan, `SonderWin32` rendait `(Classer(nom), nom, null)`. Or `Classer("")` rend
  `Indisponible` : la sonde réelle aurait donc rendu « indisponible » sans raison, alors que LUE-04 exige de dire pourquoi.
- **Correction :** si `Classer` rend `Indisponible`, la sonde rend `(Indisponible, null, "nom de processus vide")`.
  P08 vérifie en plus qu'un statut `Indisponible` porte une raison non vide.
- **Fichiers :** `src/Chronos/Services/PremierPlanWin32.cs`, `tests/Chronos.Tests/PremierPlanWin32Tests.cs`
- **Commit :** `b7b77cb` (correction) ; l'assertion P08 date de `4b2f26c`.

**Écarts de mesure (pas des corrections) :**
- (m3) a fait rougir **quatre** tests : les deux exigés, plus S06 et S08. Sur `ParSession`, l'égalité se départage par
  identifiant et non par chemin, et le fichier sans identifiant, servi par le cache, disparaît de la sélection.
- Aucun écart sur les totaux : 1084 isolé et 1110 combiné, comme prévu.

**Outil :** le heredoc bash échoue sur le texte français (apostrophes) dans ce shell. Les fichiers neufs ont donc été
écrits avec l'outil d'écriture, sans effet sur le contenu.

## Known Stubs

Aucun. `FakePremierPlan` est un faux de test voulu, pas un bouchon de production. `PremierPlanWin32` n'est encore
enregistré nulle part (pas de DI, pas de paramètre `premierPlan:` du moniteur), mais c'est le périmètre explicite de
30-03 : garde de source `Le_moniteur_de_production_recoit_le_premier_plan`, mutation (m5).

## Issues Encountered

Aucune. La compilation n'a jamais été cassée par un squelette de 30-01 pendant les mesures.

## Next Phase Readiness

- **30-03** : le moniteur peut appeler `_premierPlan?.Lire(now)` sous `try`, passer `ClaudeDepuis` au détecteur et
  prendre la sélection dans `appBureau.Selection`, jamais dans `ParSession` (la mutation (m3 bis) le gardera). Il
  enregistre `IPremierPlan` → `PremierPlanWin32` en singleton et câble `premierPlan:` par argument nommé dans
  `App.xaml.cs`.
- **30-04** : le rapport lit `EtatPremierPlan` (statut, processus, raison) et `Selection` (y compris un `CliSessionId`
  nul) dans la même lecture que le widget.
- LUE-02 et LUE-04 restent **Pending** : la détection et la sélection existent, mais leur branchement relève de 30-03
  et du rapport.

## Self-Check: PASSED

- FOUND : `src/Chronos/Services/PremierPlanWin32.cs`, `tests/Chronos.Tests/PremierPlanWin32Tests.cs`,
  `tests/Chronos.Tests/Fakes/FakePremierPlan.cs`, `src/Chronos/Services/LecteurAppBureau.cs`,
  `tests/Chronos.Tests/LecteurAppBureauTests.cs`
- FOUND : commits `4b2f26c`, `b7b77cb`, `1955d0c`, `b10620f`

---
*Phase : 30-la-lecture-fait-dispara-tre*
*Terminé : 2026-09-26*
