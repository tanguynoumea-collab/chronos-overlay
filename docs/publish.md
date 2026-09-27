# Chronos — Publication (exe self-contained mono-fichier)

> **Objectif (DEP-01)** : distribuer Chronos en **un seul fichier `Chronos.exe`**, autonome,
> sans installation préalable du runtime .NET sur la machine cible.

---

## 1. Prérequis

- **SDK .NET 10.x** installé sur la machine de build (le SDK compile/publie sans problème une
  cible `net8.0-windows` — compatibilité descendante).
- **Cible** : `net8.0-windows` (WPF exige le TFM `-windows`).
- **Runtime identifier** : `win-x64`.
- **Machine cible** : aucun runtime .NET requis — l'exe est **self-contained** (le runtime pack
  .NET 8 est embarqué dans l'exe).

---

## 2. Commande de publication canonique

Commande **verrouillée** (verbatim `research/STACK.md` / `07-CONTEXT.md`) :

```
dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
```

Alternative équivalente via le profil de publication
(`src/Chronos/Properties/PublishProfiles/win-x64.pubxml`) :

```
dotnet publish src/Chronos/Chronos.csproj -c Release -p:PublishProfile=win-x64
```

**Sortie** :

```
src/Chronos/bin/Release/net8.0-windows/win-x64/publish/Chronos.exe
```

Un **unique** `Chronos.exe` (+ éventuellement `Chronos.pdb`, à ne pas distribuer). Taille
mesurée ~77 Mo (3.1.0, 3.2.0, 3.2.1) ; garde-fou < 120 Mo.

Copier ensuite la sortie à la racine du dépôt sous le nom versionné `Chronos-v<X.Y.Z>.exe`
(ignoré par `.gitignore` : `/Chronos-v*.exe`). La version vient des quatre propriétés du csproj
(`Version`, `FileVersion`, `AssemblyVersion`, `InformationalVersion`), tenues cohérentes par
`VersionPublieeTests`, et le rapport de diagnostic l'affiche (ligne `Version :`).

---

## 3. Propriétés de publication et le POURQUOI

Ces propriétés sont **conditionnées** dans `Chronos.csproj` sur
`Condition="'$(PublishSingleFile)' == 'true'"` : elles ne s'activent **qu'au publish**.
Un `dotnet build -c Release` normal reste rapide et non self-contained. Elles sont aussi
mirrorées dans `Properties/PublishProfiles/win-x64.pubxml`.

| Propriété | Valeur | Rationale |
|-----------|--------|-----------|
| `SelfContained` | `true` (explicite) | Depuis .NET 8, un `RuntimeIdentifier` **n'implique plus** self-contained. Explicite = zéro ambiguïté build vs publish. |
| `RuntimeIdentifier` | `win-x64` | Cible Windows 64 bits. |
| `PublishSingleFile` | `true` | Regroupe tout dans un seul exe. |
| `IncludeNativeLibrariesForSelfExtract` | `true` | WPF embarque des **DLL natives** (`PresentationNative`, `wpfgfx`, `vcruntime`…). Sans ce flag elles resteraient à côté de l'exe → pas de vrai mono-fichier. À `true`, elles sont extraites dans un dossier temp au 1er lancement. |
| `EnableCompressionInSingleFile` | `true` | Réduit l'exe (~140 Mo → ~60-70 Mo). Coût : léger surcoût de décompression au démarrage — acceptable pour un overlay lancé une fois au boot. |
| `PublishTrimmed` | **`false`** | **WPF n'est PAS trim-safe** : le trimmer supprime des types résolus par réflexion (XAML, styles, converters) → crash `Unhandled Exception` au lancement (dotnet/wpf #3386, #4216). **NON NÉGOCIABLE.** Compresser via `EnableCompressionInSingleFile` à la place. |
| `PublishReadyToRun` | `true` | Pré-compile en code natif → démarrage plus rapide (bénéfique pour une app d'autostart lancée au boot). Coût : +taille. |
| `InvariantGlobalization` | `false` | `true` réduirait la taille mais **casserait le formatage fr-FR** (dates de reset, comptes à rebours). L'UI est en français → garder la globalization. |

> **À ne jamais faire** : mettre `PublishSingleFile`/`SelfContained` dans un `<PropertyGroup>`
> inconditionnel (rend `build`/debug/F5 self-contained et lent) ; activer `PublishTrimmed=true`
> (crash WPF) ; `PublishAot=true` (incompatible WPF) ; `InvariantGlobalization=true` (casse fr-FR).

---

## 4. Distribution

- Copier **le seul fichier `Chronos.exe`** vers la machine cible (le `.pdb` n'est pas nécessaire).
- Aucune installation de runtime .NET n'est requise (self-contained).
- Les DLL natives WPF sont **extraites automatiquement au 1er lancement** dans un dossier temp.
- **Premier run** : un léger délai d'extraction et/ou une alerte **SmartScreen** est possible au
  tout premier lancement d'un exe non signé — c'est **normal**. Signer l'exe (optionnel) réduit
  ces alertes.

---

## 5. Autostart — chemin stable et limite

Le toggle « Lancer au démarrage » (fenêtre de réglages, clic droit sur le cadran) crée un
raccourci `Chronos.lnk` dans `shell:startup` (per-user, sans droit admin). Le raccourci cible
**`Environment.ProcessPath`**, c'est-à-dire **l'exe qui l'a créé** (voir
`src/Chronos/Services/AutostartService.cs`).

`Environment.ProcessPath` est **single-file-safe** — contrairement à `Assembly.Location` qui est
**vide en mono-fichier**. Le raccourci pointe donc toujours vers le bon exe au moment de l'activation.

> **⚠️ Limite à connaître** : le raccourci reste valide **tant que l'exe n'est pas déplacé**.
> Si l'utilisateur déplace `Chronos.exe` **après** avoir activé l'autostart, le `.lnk` continue de
> pointer vers l'**ancien** emplacement (raccourci cassé). Correctif : **re-basculer l'autostart**
> (désactiver puis réactiver) depuis le **nouvel** emplacement de l'exe.

---

## 6. Vérifications post-publication

Vérifications **automatisables** (rappel — faites lors du build de release) :

1. `publish/` ne contient **que** `Chronos.exe` (+ `.pdb`) — **zéro** `.dll` à côté.
2. Taille de `Chronos.exe` < 120 Mo (mesuré ~77 Mo).
3. **Smoke sans lancer l'overlay** : `Chronos-v<X.Y.Z>.exe --hook SessionStart` avec une entrée
   standard vide ⇒ code 0, aucune écriture, md5 de `~/.claude/settings.json` identique avant/après.
   L'agent ne lance jamais l'overlay : lancé depuis une session Claude Code, il verrait la vue
   virtualisée d'AppData (autres réglages, autre `treated.json`). Le smoke `--hook` ne prouve ni le
   verrou mono-instance ni le journal d'historique (ce mode sort avant le Host) : ils se constatent au
   premier lancement par l'utilisateur (32-CONSTAT).
4. Non-régression : `dotnet test Chronos.sln -c Debug` → suite complète verte (0 échec), deux
   exécutions.

Vérifications **humaines (UAT)** — hors périmètre automatisé (voir `07-VALIDATION.md`) :

- **Cadran réellement visible** au lancement de l'exe publié (la fenêtre étant borderless /
  `ShowInTaskbar=false`, un critère de handle de fenêtre n'est pas fiable — vérification visuelle).
- **Machine réellement propre** sans .NET : copier l'exe sur une VM/machine sans SDK et lancer.
- **Autostart après reboot** : activer le toggle depuis l'exe publié, redémarrer Windows,
  confirmer le lancement automatique.

---

## 7. Premier lancement et réconciliation

C'est l'utilisateur qui lance le nouvel exe, par l'Explorateur (double-clic) ou Win+R — jamais
depuis un terminal ouvert dans l'app Claude. Depuis la 3.2.2, **une seule instance** de l'overlay
tourne par session Windows (mutex `Local\Chronos-overlay`, `VerrouInstanceUnique`) : un second exe
affiche « Chronos tourne déjà… » et se retire sans toucher à l'autre. Le verrou ne connaît pas les exe
ANTÉRIEURS à la 3.2.2 : quitter d'abord ceux-là (réglages → « Quitter Chronos »). Les modes `--hook`
et `--statusline` restent multi-instances.

Au premier lancement en mode overlay, `ClaudeSettingsReconciler` repointe les huit groupes de hooks
(widget de sessions activé ; désactivé, il les retire) et la statusLine vers le nouvel exe, après
une sauvegarde `%APPDATA%\Chronos\backups\claude-settings-<horodatage>.json`. Le constater dans le
FICHIER — la sauvegarde est l'état avant ; le fichier courant est la sauvegarde où l'ancien nom
d'exe devient le nouveau — et non dans `chronos.log`, écrit avant la réconciliation.

Garder l'ancien exe sur le disque tant que des sessions ouvertes avant la réconciliation tournent :
elles l'appellent encore. Activer « Lancer au démarrage » DEPUIS le nouvel exe : le raccourci vise
l'exe qui l'a créé.
