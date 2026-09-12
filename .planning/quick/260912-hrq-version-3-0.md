# Quick 260912-hrq — Version 3.0 + publication pour test

**Date :** 2026-09-12
**Type :** release / build

## Objectif

Monter la version de 2.8.1 à **3.0.0** et publier l'exe mono-fichier self-contained win-x64, pour que
l'utilisateur puisse tester le milestone v1.5 « Exactitude permanente ».

## Pourquoi 3.0 et pas 2.8.2

L'exe déployé (`~/Downloads/Chronos-v2.8.1.exe`, 13/07) est antérieur à **toutes** les phases 15 → 20.
Le milestone a changé la **doctrine d'affichage du produit** : suppression de l'estimation absolue,
correction par delta (« ≥ N % »), nouvelle source par en-têtes de rate-limit, cycle de vie du jeton,
quatre états visuels. Republier sous 2.8.x aurait fait porter le même numéro à deux binaires
radicalement différents — ce que la doctrine de versionnage du projet interdit explicitement
(commentaire du `.csproj`, ligne 14).

## Ce qui a été fait

1. `src/Chronos/Chronos.csproj` — les **4** propriétés de version portées à 3.0 :
   `Version` 3.0.0 · `FileVersion` 3.0.0.0 · `AssemblyVersion` 3.0.0.0 · `InformationalVersion` 3.0.0.
   Contrôle : `grep -c "2\.8\.1"` → **0**.
2. Suite de tests : **748 / 748, 0 échec, 5 s**.
3. Publication avec la commande canonique de `docs/publish.md` :
   `dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true`
4. Copie nommée selon la doctrine : `~/Downloads/Chronos-v3.0.exe`.

## Résultat

| | |
|---|---|
| Exe produit | `src/Chronos/bin/Release/net8.0-windows/win-x64/publish/Chronos.exe` |
| Taille | **77 188 649 octets** (~77,2 Mo) |
| Version embarquée (vérifiée) | `FileVersion 3.0.0.0` · `ProductVersion 3.0.0` |
| Copie de diffusion | `~/Downloads/Chronos-v3.0.exe` |

**Note sur le nom :** `dotnet publish` produit toujours `Chronos.exe` (l'`AssemblyName` gouverne). La
doctrine « le nom du fichier publié porte la version » se satisfait donc par une **copie nommée** au moment
de la diffusion, pas par la sortie de publication elle-même. C'est fait.

## Précautions prises avant le lancement de test

- Sauvegarde de `~/.claude/settings.json` → `~/.claude/settings.json.avant-v1.5` (6872 octets, 25 groupes
  de hooks Chronos). Le premier démarrage de la v3.0 déclenchera la réconciliation de la phase 15, qui les
  ramènera à 5 avec sa propre sauvegarde horodatée.
- Vérifié : **aucune instance de Chronos en cours** avant la copie (sinon le fichier aurait été verrouillé).
- L'ancien `Chronos-v2.8.1.exe` est **conservé** dans `~/Downloads` — repli possible.

## Reste à faire (hors périmètre de cette tâche)

Le protocole de vérification humaine en 9 points, dans
`.planning/phases/20-honn-tet-visible-cadran-diagnostic/20-05-SUMMARY.md`.
