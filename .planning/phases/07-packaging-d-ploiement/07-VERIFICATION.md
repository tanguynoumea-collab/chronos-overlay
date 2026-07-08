---
phase: 07-packaging-d-ploiement
verified: 2026-07-08T22:40:00Z
status: passed
score: 5/5 must-haves verified
---

# Phase 7: Packaging + déploiement Verification Report

**Phase Goal:** L'application se distribue en un exécutable unique autonome et fonctionne sur une machine propre.
**Verified:** 2026-07-08
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
| --- | --- | --- | --- |
| 1 | La publication produit un unique `Chronos.exe` self-contained mono-fichier win-x64 (aucune DLL managée ni native à côté, hors .pdb) | ✓ VERIFIED | `dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true` exécuté ; `publish/` contient exactement `Chronos.exe` (76 765 077 octets ≈ 73,2 Mo) + `Chronos.pdb` — zéro `.dll` |
| 2 | Les propriétés de publication ne s'activent QU'au publish : `dotnet build -c Release` reste un build normal | ✓ VERIFIED | Suppression du dossier `publish/win-x64` puis `dotnet build Chronos.sln -c Release` : succès, aucun sous-dossier `win-x64` régénéré, aucun `hostfxr`/`coreclr`/`clrjit` dans `bin/Release/net8.0-windows/` (grep = 0 résultat) |
| 3 | L'exe PUBLIÉ (pas le build debug) se lance et reste vivant sans crash, puis se ferme proprement | ✓ VERIFIED | Smoke PowerShell réel sur `publish/Chronos.exe` : process vivant après 6 s, `Stop-Process` propre, sortie « SMOKE OK » |
| 4 | La suite de tests reste verte (106/106) après packaging | ✓ VERIFIED | `dotnet test Chronos.sln -c Debug` → « Réussi ! - échec : 0, réussite : 106, ignorée(s) : 0, total : 106 » |
| 5 | La commande de publication exacte est documentée dans docs/publish.md, avec la limite autostart après déplacement de l'exe | ✓ VERIFIED | `docs/publish.md` (112 lignes) contient la commande verbatim (section 2), le tableau des 8 propriétés + rationale (section 3), et la limite autostart chemin stable/re-toggle (section 5) |

**Score:** 5/5 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
| --- | --- | --- | --- |
| `src/Chronos/Chronos.csproj` | PropertyGroup conditionné avec les 8 propriétés verrouillées (dont `PublishReadyToRun`, `InvariantGlobalization`) | ✓ VERIFIED | Lignes 14-23 : `SelfContained`, `RuntimeIdentifier=win-x64`, `PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`, `EnableCompressionInSingleFile`, `PublishTrimmed=false`, `PublishReadyToRun=true`, `InvariantGlobalization=false` — toutes sous `Condition="'$(PublishSingleFile)' == 'true'"` |
| `src/Chronos/Properties/PublishProfiles/win-x64.pubxml` | Profil de publication miroir des propriétés verrouillées | ✓ VERIFIED | Fichier de 724 octets présent, contient `PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`, `PublishTrimmed=false`, `PublishReadyToRun`, `InvariantGlobalization` |
| `docs/publish.md` | Commande de publication + rôle des propriétés + limite autostart, ≥25 lignes | ✓ VERIFIED | 112 lignes, 6 sections (prérequis, commande, propriétés/pourquoi, distribution, autostart, vérifications post-publication) |
| Sortie publish `Chronos.exe` | Exécutable self-contained mono-fichier produit | ✓ VERIFIED | Présent dans `src/Chronos/bin/Release/net8.0-windows/win-x64/publish/`, 73,2 Mo (< 120 Mo garde-fou), seul avec `.pdb` |

### Key Link Verification

| From | To | Via | Status | Details |
| --- | --- | --- | --- | --- |
| `src/Chronos/Chronos.csproj` | propriétés self-contained/single-file | `PropertyGroup` conditionné `Condition="'$(PublishSingleFile)' == 'true'"` | ✓ WIRED | Pattern trouvé ligne 14 du csproj, exact match |
| `src/Chronos/Services/AutostartService.cs` | chemin de l'exe publié | `Environment.ProcessPath` (single-file-safe) | ✓ WIRED | Ligne 37 : `var exe = Environment.ProcessPath!;` utilisé comme `lnk.TargetPath`, commentaire explicite anti-`Assembly.Location` |

### Data-Flow Trace (Level 4)

N/A — phase de packaging pure, aucun composant ne rend de données dynamiques. Le seul « flux » pertinent
(chemin de l'exe → raccourci autostart) est couvert par la vérification du Key Link ci-dessus.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| --- | --- | --- | --- |
| Build Release reste normal (non self-contained) après suppression du dossier publish | `rm -rf .../win-x64` puis `dotnet build Chronos.sln -c Release` | Succès, aucun sous-dossier win-x64 régénéré, aucun binaire runtime natif (hostfxr/coreclr/clrjit) | ✓ PASS |
| Publish produit un exe unique | `dotnet publish ... -p:PublishSingleFile=true --self-contained true` | `Chronos.exe` (73,2 Mo) + `.pdb` seuls dans `publish/` | ✓ PASS |
| Exe publié survit sans crash | Smoke PowerShell (`Start-Process` + `Start-Sleep 6` + vérif `HasExited` + `Stop-Process`) | Process vivant après 6 s, kill propre | ✓ PASS |
| Non-régression suite de tests | `dotnet test Chronos.sln -c Debug` | 106/106 verts | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
| --- | --- | --- | --- | --- |
| DEP-01 | 07-01 | L'app se publie en exe self-contained mono-fichier win-x64 (PublishSingleFile, PublishTrimmed=false, IncludeNativeLibrariesForSelfExtract=true) | ✓ SATISFIED | csproj + pubxml portent les 3 flags exigés ; publish réel produit l'exe unique conforme ; smoke + non-régression verts |

Aucune exigence orpheline détectée : REQUIREMENTS.md ne mappe que DEP-01 à la Phase 7, et DEP-01 est
bien déclaré dans le frontmatter du plan 07-01.

### Anti-Patterns Found

Aucun. Scan de `Chronos.csproj`, `win-x64.pubxml`, `docs/publish.md` et `AutostartService.cs` sur les
patterns TODO/FIXME/XXX/HACK/PLACEHOLDER/« not implemented »/« coming soon » : 0 résultat.

### Human Verification Required

Voir `.planning/phases/07-packaging-d-ploiement/07-HUMAN-UAT.md` (créé à cette vérification). Ces items
étaient déjà identifiés dans `07-VALIDATION.md` (Human Verification Items) et sont maintenant trackés au
format UAT standard — ils ne bloquent pas le statut `passed` de cette phase (couverture v1 hors
périmètre autonome, machine propre / reboot réel).

1. **Cadran visible à l'écran** — lancer l'exe publié et confirmer visuellement l'affichage du cadran
   (le smoke automatisé ne vérifie que « process vivant », pas le rendu visuel).
2. **Machine réellement propre sans .NET** — copier `Chronos.exe` seul sur une VM/machine sans SDK/Runtime
   installé et lancer.
3. **Autostart après reboot Windows** — activer le toggle depuis l'exe publié, redémarrer, confirmer le
   lancement automatique.
4. **Limite autostart après déplacement de l'exe** — déplacer l'exe après activation, redémarrer,
   confirmer le comportement documenté (raccourci cassé) puis re-toggle correctif.

### Gaps Summary

Aucun gap. Les 5 vérités observables sont vérifiées, les 3 artefacts et les 2 liens clés sont conformes,
la non-régression est confirmée (106/106), et DEP-01 est satisfait. Les seuls items restants
(cadran visible, machine propre sans .NET, autostart après reboot réel) relèvent structurellement de
l'UAT humain — déjà anticipés dans `07-VALIDATION.md` et maintenant formalisés dans `07-HUMAN-UAT.md`.

---

_Verified: 2026-07-08_
_Verifier: Claude (gsd-verifier)_
