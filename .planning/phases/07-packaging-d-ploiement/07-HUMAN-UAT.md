# Phase 7 — UAT humain (validation sur machine réelle)

**Statut : EN ATTENTE de validation utilisateur**
Généré le 2026-07-08 à la vérification du plan 07-01 (mode autonome : tout ce qui est automatisable a été
vérifié programmatiquement ; les critères ci-dessous exigent une machine physique/VM et un reboot réel
et ne peuvent PAS être automatisés).

## Contexte

Le plan 07-01 finalise le packaging DEP-01 : `Chronos.csproj` porte les 8 propriétés publish verrouillées
(conditionnées au publish uniquement), le profil `win-x64.pubxml` est créé, et `docs/publish.md` documente
la commande exacte + la limite autostart. Les vérifications programmatiques suivantes ont déjà été
confirmées automatiquement :

- `dotnet build Chronos.sln -c Release` : build normal, **aucun** sous-dossier `win-x64` self-contained
  généré (vérifié en supprimant `publish/win-x64` puis en rebuildant Release — aucune régénération, aucun
  `hostfxr`/`coreclr`/`clrjit` dans `bin/Release/net8.0-windows/`).
- `dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true` :
  produit **un seul** `Chronos.exe` (73,2 Mo) + `Chronos.pdb` dans
  `src/Chronos/bin/Release/net8.0-windows/win-x64/publish/` — zéro DLL managée ou native à côté.
- Smoke de l'exe **publié** : lancé, vivant après 6 s (extraction native OK au 1er run), tué proprement,
  aucun crash.
- `dotnet test Chronos.sln -c Debug` : **106/106 verts** — non-régression confirmée après packaging.
- `docs/publish.md` (112 lignes) contient la commande exacte, le tableau des 8 propriétés + rationale,
  et la section 5 « Autostart — chemin stable et limite » (re-toggle après déplacement de l'exe).

Ce qui **ne peut pas** être vérifié en environnement de développement (SDK .NET déjà installé, pas de
reboot Windows autonome) est listé ci-dessous.

## Comment lancer

```
dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
```

Puis copier `src/Chronos/bin/Release/net8.0-windows/win-x64/publish/Chronos.exe` (seul fichier requis)
vers la machine/VM cible.

## Critères à valider manuellement (machine réelle requise)

| # | Exigence | Étape de vérification | Résultat attendu | Statut |
|---|----------|-----------------------|------------------|--------|
| 1 | DEP-01 | Lancer l'exe publié sur la machine de dev et observer l'écran | Le cadran (arcs + texte) est réellement **visible** à l'écran, pas seulement « process vivant » | ⬜ |
| 2 | DEP-01 | Copier `Chronos.exe` seul sur une VM/machine **sans** SDK/Runtime .NET installé et le lancer | L'application démarre et affiche le cadran, **sans** installation de runtime .NET préalable (preuve du self-contained) | ⬜ |
| 3 | DEP-01 / DEP-02 | Depuis l'exe publié (pas le build debug), activer « Lancer au démarrage », puis redémarrer Windows | L'overlay se relance automatiquement au boot, sans intervention | ⬜ |
| 4 | DEP-02 | Après avoir activé l'autostart, déplacer `Chronos.exe` vers un autre dossier puis redémarrer | Le raccourci `Chronos.lnk` pointe vers l'**ancien** emplacement (comportement documenté, pas un bug) ; re-toggler l'autostart depuis le nouvel emplacement corrige le raccourci | ⬜ |

## Consigne en cas d'écart

Toute anomalie constatée est consignée comme **gap** pour un plan de clôture (`--gaps`) ;
ne pas « corriger en douce » sans replanifier.
