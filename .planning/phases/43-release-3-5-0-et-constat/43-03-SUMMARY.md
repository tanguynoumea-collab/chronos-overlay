---
phase: 43-release-3-5-0-et-constat
plan: 03
subsystem: release / packaging
tags: [VAL-06, D-05, release, publish]
requires: [43-02 (version 3.5.0 au csproj, publish.md de la 3.5)]
provides:
  - Chronos-v3.5.0.exe à la racine du dépôt principal (non versionné)
  - docs/publish.md §2 (taille 3.5.0) et §4 « Empreintes publiées »
  - commit de release e56fe57 (sans étiquette ni push)
affects: [43-04 (constat utilisateur)]
tech-stack:
  added: []
  patterns: [vérification statique de l'exe : VersionInfo + SHA-256, sans exécution]
key-files:
  created:
    - Chronos-v3.5.0.exe (ignoré par git, .gitignore:22)
  modified:
    - docs/publish.md
decisions:
  - "Aucun smoke exécutable par l'agent : tout contrôle qui lance l'exe passe au constat 43-04 (D-05, D-06)"
metrics:
  duration: ~5 min
  completed: 2026-10-04
  tasks: 2
  files: 1
---

# Phase 43 Plan 03 : publication de Chronos-v3.5.0.exe — Summary

`Chronos-v3.5.0.exe` est construit par la commande canonique, vérifié par ses métadonnées et son empreinte sans
jamais être lancé, et la release est datée par le commit e56fe57 (sans étiquette ni push).

## L'exe

| Champ | Valeur |
|-------|--------|
| Chemin | `C:\Users\Tanguy\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.5.0.exe` |
| Taille | 78 199 851 o (< 120 000 000) |
| FileVersion / ProductVersion / ProductName | 3.5.0.0 / 3.5.0 / Chronos (OriginalFilename `Chronos.dll`) |
| SHA-256 | `3577e72a1a8eb71fa85edf88750da918d04d01154f718458382ca4a4482d8071` |
| SHA-256 de la sortie de publish | identique (copie octet pour octet) ; ≠ 3.4.0 (`593a33a3…`) |
| Sortie de publish | `Chronos.exe` + `Chronos.pdb`, 0 DLL |
| git | `.gitignore:22:/Chronos-v*.exe` ; absent de `git status` |
| Anciens exe | 3.1.0 à 3.4.0 (7) toujours présents, 8 exe au total |

Métadonnées lues par `(Get-Item).VersionInfo`, empreinte par `sha256sum`. Empreinte écrite dans `docs/publish.md` §4
puis recomparée à `sha256sum` de la copie : identique.

## Porte

- `dotnet build Chronos.sln -c Debug -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln -c Debug` ×2 : 2 223 réussis, 0 échec, 1 ignoré, total 2 224 (les deux fois).
- `dotnet build Chronos.sln -c Release -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln -c Release` : 2 223 réussis, 0 échec, 1 ignoré, total 2 224.
- Gardes documentaires (GardeDocumentationReleaseTests, ContratJournalDocumenteTests, GardeGestesCadranTests) après
  l'édition de publish.md : 20 / 20.

## Aucun exe lancé

- md5 de `~/.claude/settings.json` : `6d7712d75d5934d72be0643ecff47d48` avant (15:05) et après (15:10). Identique.
- Processus `Chronos-v3.5.0.exe` : 0 avant, 0 après.
- Aucune écriture dans `~/.claude`, `%APPDATA%\Chronos`, `shell:startup`.

## Transféré au constat (43-04)

Ces contrôles exigent de lancer l'exe. L'agent ne les a pas faits :
- smoke `--hook SessionStart` (code 0, settings.json inchangé, 0 processus résident) ;
- sortie silencieuse de `--zzz` (argument inconnu, code 0, aucune fenêtre) ;
- instance unique ;
- retrait de la barre statusLine de `~/.claude/settings.json` ;
- sauvegarde présente ;
- hooks repointés.

## Écarts par rapport au plan

Aucun.

## Known Stubs

Aucun.

## Commits

- e56fe57 — release: Chronos 3.5.0 - v1.9 cadrans à l'échelle, Braises, Historique plein écran, thèmes, une chaîne de données

Pas d'étiquette, pas de push (`main...origin/main [ahead 719]`). STATE.md, ROADMAP.md et REQUIREMENTS.md non modifiés.

## Self-Check: PASSED

- FOUND: Chronos-v3.5.0.exe (SHA-256 vérifié)
- FOUND: e56fe57
