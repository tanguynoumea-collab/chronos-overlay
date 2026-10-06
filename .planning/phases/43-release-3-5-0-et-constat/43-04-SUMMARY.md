---
phase: 43-release-3-5-0-et-constat
plan: 04
subsystem: constat / release
tags: [VAL-06, VAL-07, VAL-04, VAL-05, constat, sonde-hors-arbre]
requires: [43-03 (Chronos-v3.5.0.exe), 43-05..43-09 (corrections pendant le constat)]
provides:
  - 43-CONSTAT.md rempli (protocole, résultats, écarts E43-1..6, verdict)
  - preuve hors arbre du critère 3 (barre retirée, sauvegarde md5 = t0, 8 hooks vers la 3.5.0)
affects: [clôture de la phase 43, RETOUR ROADMAP (E43-5, E43-6)]
tech-stack:
  added: []
  patterns: [sonde WMI Win32_Process.Create avec preuve de vue réelle (parent WmiPrvSE.exe, APPDATA\Claude absent)]
key-files:
  created:
    - .planning/phases/43-release-3-5-0-et-constat/43-04-SUMMARY.md
  modified:
    - .planning/phases/43-release-3-5-0-et-constat/43-CONSTAT.md
decisions:
  - "Réponse globale « Constats terminée je valide tout » consignée VG (validé, verdict global du 2026-10-06) sur chaque point, sans observation inventée"
  - "Écarts E43-1..4 consignés comme relevés PENDANT le constat et corrigés avant le verdict (43-05..43-09) ; E43-5/6 (libellés de diagnostic) en RETOUR ROADMAP"
  - "Sauvegardes du 2026-10-06 13:36:34/35 attribuées (déduction, non observée) à une bascule off/on du widget ; état final identique, pas un écart"
metrics:
  duration: ~25 min (tâche 3)
  completed: 2026-10-06
  tasks: 3
  files: 2
---

# Phase 43 Plan 04 : constat de la 3.5.0 — Summary

Le constat a été joué par l'utilisateur et son verdict est écrit : « Constats terminée je valide tout » (2026-10-06).
Il porte sur l'exe final `Chronos-v3.5.0.exe` (SHA-256 `e9884209a6bf61625aa574ada9dcca27b123b2282d692ef562e63fed99a220b3`,
78 200 548 o, revérifié sur le fichier). Une sonde hors de l'arbre de l'app prouve le critère 3.

## Sonde a (WMI, 2026-10-06 13:39:32, + a2 à 13:40:24)

- Preuve de vue réelle, les deux fois : `parent: WmiPrvSE.exe`, `APPDATA\Claude existe : False`.
- Processus : 1 seul `Chronos-v3.5.0.exe`, sans argument, parent `explorer.exe`, créé le 10-05 à 14:40:57, après la
  copie de l'exe final. Aucune ancienne version en marche. E1-ter soldé.
- `~/.claude/settings.json` :
  - md5 `862ae97a…`, 3 225 o ;
  - **statusLine absente** ;
  - `Chronos-v3.5.0.exe` = 8 occurrences, aucune autre version ;
  - 1 groupe `--hook` 3.5.0 par clé, sur 8 clés ;
  - 3 commandes `gsd-`.
- Sauvegardes :
  - `claude-settings-20261004-132656.json` de md5 **`6d7712d7…` = t0**. Ni le smoke `--hook` ni le « Quitter » de la
    3.4.0 n'ont donc écrit ce fichier.
  - Épingle `claude-settings-initial.json`, et rétention à 5 sauvegardes : les deux comportements sont voulus.
  - Aucune sauvegarde n'a été créée par les 6 relances : la réconciliation est idempotente.
  - Deux sauvegardes du 10-06 à 13:36 viennent d'une bascule off/on du widget (déduction). Le fichier a ensuite été
    reconstitué à l'identique.
- `chronos.log` :
  - Version 3.5.0, Vue AppData réelle ;
  - Processus 1 — 0 autre ;
  - verrou tenu par ce processus ;
  - « [Réglages de Claude Code] » : rien à retirer, barre absente.
- Journal :
  - 448 relevés au pas de 5 min depuis t0 ;
  - un `demarrage` 3.5.0 à chaque lancement et un `arret` à chaque arrêt ;
  - 5 trous de 28 min à 3 h 09, chacun encadré par un `arret` et un `demarrage`.
- Le raccourci Démarrage est absent.
- V10 : environ 1 à 4 s.

## Écarts (§8)

- E43-1 : anneau hebdo de Braises, 14 braises en 7 groupes de 2. Corrigé par 43-05.
- E43-2 : le rempli montre le temps consommé partout. Corrigé par 43-06.
- E43-3 : flèche de Braises retirée. Corrigé par 43-07.
- E43-4 : anneau 5 h, d'abord en 5 groupes de 5 (43-08), puis en journée découpée en tranches de 5 h (43-09).
  Limite connue : vides de 18 à 22,5° quand la première limite du jour tombe entre 00:30 et 03:30.
- E43-5 et E43-6 : deux libellés de diagnostic trompeurs (« Dernière sonde : cadence bornée… » et « Connecté : OUI —
  … prochain rafraîchissement »). Proposés en RETOUR ROADMAP.

## Statut des exigences

VAL-06 (critère 3) est atteint, tout comme VAL-07. Les reprises VAL-04 (32-08) et VAL-05 (35-07) sont conclues.
STATE.md, ROADMAP.md et REQUIREMENTS.md n'ont pas été modifiés, sur consigne de l'orchestrateur.

## Écarts au plan

- Le signal de reprise est global et non point par point. Chaque point est consigné VG, sans observation détaillée
  inventée.
- J'ai lancé une seconde sonde (a2), avec la même preuve de vue réelle, pour la ligne « Verrou mono-instance » que le
  filtre de la première avait manquée, et pour la cible de chaque hook.

## Commits

- 56036f3 — docs(43-04) : protocole du constat de la 3.5.0 (écrit avant d'être joué)
- db68645 — docs(43-04) : constat de la 3.5.0 — résultats, écarts et verdict

Aucune étiquette, aucun push. L'agent n'a lancé, arrêté ni cliqué aucun Chronos. `Documents\chronos-constat` est vide.

## Self-Check: PASSED

- FOUND : 43-CONSTAT.md (aucun « à relever », `## Verdict` présent, LF)
- FOUND : 56036f3, db68645
- FOUND : Chronos-v3.5.0.exe, SHA-256 e9884209…b220b3
