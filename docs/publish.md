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
mesurée ~77 Mo (3.1.0, 3.2.0, 3.2.1), 78 200 845 o (3.5.0, reconstruite le 2026-10-04) ; garde-fou < 120 Mo.

Copier ensuite la sortie à la racine du dépôt principal sous le nom versionné `Chronos-v<X.Y.Z>.exe`
(`Chronos-v3.5.0.exe` pour cette release ; ignoré par `.gitignore` : `/Chronos-v*.exe`). La version vient des
quatre propriétés du csproj (`Version`, `FileVersion`, `AssemblyVersion`, `InformationalVersion`), tenues
cohérentes par `VersionPublieeTests`, et le rapport de diagnostic l'affiche (ligne `Version :`).

**Empreinte** : calculer le SHA-256 de l'exe versionné (`sha256sum Chronos-v3.5.0.exe` ou
`Get-FileHash .\Chronos-v3.5.0.exe -Algorithm SHA256`) et le publier avec l'exe (notes de release) ;
l'utilisateur la compare avant le premier lancement.

**Étiquette** : convention `exe-vX.Y.Z` (`exe-v3.5.0`) sur le commit de release, posée par l'utilisateur au
moment de la publication GitHub — jamais par l'agent.

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

- Copier **le seul fichier `Chronos-vX.Y.Z.exe`** vers la machine cible (le `.pdb` n'est pas nécessaire).
- Aucune installation de runtime .NET n'est requise (self-contained).
- Les DLL natives WPF sont **extraites automatiquement au 1er lancement** dans un dossier temp.
- **Premier run** : un léger délai d'extraction et/ou une alerte **SmartScreen** est possible au
  tout premier lancement d'un exe non signé — c'est **normal**. Signer l'exe (optionnel) réduit
  ces alertes.

**Empreintes publiées**

| exe | taille (o) | SHA-256 |
|-----|-----------:|---------|
| `Chronos-v3.5.0.exe` | 78 200 845 | `cca57384af57dcd66575a7cad571c8974faecb7e56b592210c8c62c98d36f6f3` |

Vérifier avant le premier lancement : `Get-FileHash .\Chronos-v3.5.0.exe -Algorithm SHA256`.

---

## 5. Autostart — raccourci repointé au démarrage

Le toggle « Lancer au démarrage » (réglages → Comportement, clic droit sur le cadran) crée un
raccourci `Chronos.lnk` dans `shell:startup` (per-user, sans droit admin). Le raccourci cible
**`Environment.ProcessPath`**, c'est-à-dire **l'exe qui l'a créé** (voir
`src/Chronos/Services/AutostartService.cs`).

`Environment.ProcessPath` est **single-file-safe** — contrairement à `Assembly.Location` qui est
**vide en mono-fichier**.

**Au démarrage, Chronos repointe seul un raccourci existant vers l'exe courant**
(`AutostartService.ConvergerVersExeCourant`, avant que la case « Lancer au démarrage » ne soit lue) si la cible du raccourci a disparu
du disque (exe déplacé ou supprimé) ou si elle est d'une version plus ancienne que l'exe courant. Il ne le fait pas :

- si l'exe courant est un build de développement (un dossier `bin\` dans son chemin) ou une copie lancée depuis le
  dossier temporaire (`%TEMP%`) ;
- si la cible est d'une version égale ou plus récente : une version plus ancienne relancée ne reprend pas
  l'autostart ;
- si l'une des deux versions est illisible (le raccourci est conservé).

Il ne crée jamais le raccourci : absent, il reste absent. Un repointage, un raccourci conservé pour l'une de ces
raisons ou un échec est écrit au journal d'incidents.

> **Recours manuel** : la case « Lancer au démarrage » n'est cochée que si le raccourci vise l'exe courant. Depuis
> un exe que Chronos ne repointe pas (dossier `bin\`, dossier temporaire, version plus ancienne), cocher la case
> réécrit le raccourci vers cet exe.
>
> **Limite connue (PKG-R2)** : le diagnostic n'affiche pas la cible du raccourci ; pour la connaître, ouvrir
> `shell:startup` et lire les propriétés de `Chronos.lnk`.

---

## 6. Vérifications post-publication

Vérifications **automatisables** (rappel — faites lors du build de release) :

1. `publish/` ne contient **que** `Chronos.exe` (+ `.pdb`) — **zéro** `.dll` à côté.
2. Taille de `Chronos.exe` < 120 Mo (mesuré ~77 Mo).
3. **Smoke `--hook`** : `Chronos-v<X.Y.Z>.exe --hook SessionStart` avec une entrée standard vide ⇒
   code 0, aucune écriture, md5 de `~/.claude/settings.json` identique avant/après. Depuis la 3.5.0,
   l'agent ne lance JAMAIS l'exe, même en mode `--hook` : le smoke est joué par l'utilisateur au constat
   (43-CONSTAT), depuis une invite de commandes ouverte par l'Explorateur. Lancé depuis une session
   Claude Code, l'exe verrait la vue virtualisée d'AppData (autres réglages, autre `treated.json`). Le
   smoke `--hook` ne prouve ni le verrou mono-instance ni le journal d'historique (ce mode sort avant le
   Host) : ils se constatent au premier lancement par l'utilisateur.
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
ANTÉRIEURS à la 3.2.2 : quitter d'abord ceux-là (réglages → « Quitter Chronos », en bas du rail depuis la 3.4.0). Le mode `--hook`
reste multi-instances.

À chaque lancement en mode overlay, `ClaudeSettingsReconciler` repointe les huit groupes de hooks
(widget de sessions activé ; désactivé, il les retire) vers le nouvel exe, après une sauvegarde
`%APPDATA%\Chronos\backups\claude-settings-<horodatage>.json`. Depuis la 3.5, le bilan du passage est écrit dans
`chronos.log` (section « [Réglages de Claude Code] » : ce qui a été fait, nom de la sauvegarde, état actuel de la
barre de statut) : le log de démarrage est écrit APRÈS la réconciliation.

Garder l'ancien exe sur le disque tant que des sessions ouvertes avant la réconciliation tournent :
elles l'appellent encore. Un raccourci « Lancer au démarrage » existant est repointé vers le nouvel exe
s'il est plus récent que sa cible (§5).

### Premier lancement de la 3.3.0

- **Avant** : quitter à la main tous les anciens exe (3.1.0 … 3.2.2), par réglages → « Quitter Chronos ». Les exe antérieurs
  à la 3.2.2 ne connaissent pas le verrou ; une 3.2.2 encore lancée le connaît, elle, et ferait se retirer la 3.3.0
  (« Chronos tourne déjà… »).
- **Réconciliation inchangée** : sauvegarde de `~/.claude/settings.json` sous `%APPDATA%\Chronos\backups\`, puis 9
  remplacements du chemin de l'exe (les huit groupes de hooks et la statusLine).
- **Reconstruction des tokens** : elle démarre en arrière-plan dès le lancement, à priorité basse — ≈ 15 s à froid la
  première fois, quelques dizaines de millisecondes ensuite. L'overlay ne l'attend pas ; la fenêtre Historique affiche un
  bandeau de progression tant qu'elle tourne.
- **Historique** : il s'ouvre par un double-clic au centre du cadran ou par Réglages → Historique d'utilisation → Ouvrir.
  Les pourcentages ne se reconstruisent pas : ils commencent à la première ligne du journal.

### Premier lancement de la 3.4.0

- **Avant** : quitter la version en cours (clic droit sur le cadran → « Quitter Chronos »). Depuis la 3.3.1 l'arrêt est
  borné : le processus disparaît en moins d'une seconde (sinon, sortie forcée au bout de 10 s au plus). Sans cela, le verrou
  d'instance unique ferait répondre « Chronos tourne déjà » à la 3.4.0.
- **Réglages refondus** : le clic droit ouvre une fenêtre classique (860 × 580 par défaut, 640 × 440 au minimum),
  redimensionnable, agrandissable, dans la barre des tâches, qui ne se ferme plus quand on clique ailleurs. Six sections
  (Données, Historique, Apparence, Sessions, Comportement, Diagnostic) ; « Source terminal » s'appelle désormais « Barre de
  statut de Claude Code » (Données) ; le diagnostic s'affiche dans la fenêtre (Actualiser, Copier) au lieu d'une boîte de
  message.
- **Nouveaux champs de `settings.json`** : `ReglagesX`, `ReglagesY`, `ReglagesWidth`, `ReglagesHeight`, `ReglagesSection`.
  Absents d'un ancien fichier → fenêtre centrée, section Données ; bornés à l'écran au rétablissement.
- **Réconciliation inchangée** (chemins des hooks et de la statusLine réécrits vers le nouvel exe, sauvegarde préalable).

### Premier lancement de la 3.5.0

- **Avant** : quitter à la main TOUS les Chronos en marche (réglages → « Quitter Chronos »), anciennes versions
  comprises (écart E1-ter des constats 32 et 35). Puis lancer `Chronos-v3.5.0.exe` par l'Explorateur, après avoir
  comparé son empreinte SHA-256 (§2).
- **Autostart** : un raccourci « Lancer au démarrage » existant est repointé vers la 3.5.0 au démarrage s'il visait
  une version plus ancienne ou un exe disparu (§5) ; absent, il n'est pas créé.
- **Barre de statut retirée** : la 3.5 ne fournit plus de barre de statut à Claude Code. Au premier lancement, la
  réconciliation sauvegarde `~/.claude/settings.json` (sous `%APPDATA%\Chronos\backups\`), puis retire la barre
  Chronos, quels que soient son chemin et sa version ; si l'ancienne barre de l'utilisateur est connue (réglages ≤ 3.4),
  elle est restaurée à la place. Une barre d'un tiers reste intacte. Les hooks sont repointés dans la même écriture.
  Relancer ne change plus rien (idempotent).
- **Bilan** : dans `chronos.log`, section « [Réglages de Claude Code] » (barre retirée ou restaurée, nom de la
  sauvegarde), écrit après la réconciliation.
- **Sessions Claude Code déjà ouvertes** : elles appellent encore l'ancien exe pour leur barre jusqu'à leur redémarrage —
  garder l'ancien exe sur le disque tant qu'elles tournent (s'il a été supprimé, leur barre reste vide, sans gravité). Un
  appel `--statusline` qui atteint l'exe 3.5 sort en silence (argument inconnu, code 0, aucune fenêtre).
- **Carte retirée** : la carte « Barre de statut de Claude Code » disparaît de la section Données.
- **Un geste sur tout le cadran** : le petit disque cliquable du centre disparaît. Sur toute la silhouette du cadran (le disque pour Anneaux et Braises, le rectangle arrondi pour Fusible, Marée et Volets) : clic = bascule % ↔ temps, double-clic = Historique (sans bascule), appuyer puis glisser au-delà du seuil de Windows = déplacer puis accrocher au coin le plus proche, clic droit = Réglages. À côté du cadran, le clic traverse vers le bureau. Le simple clic bascule ≈ 0,5 s après le relâchement (délai de double-clic de Windows).
