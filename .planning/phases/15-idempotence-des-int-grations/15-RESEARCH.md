# Phase 15 : Idempotence des intégrations — Research

**Researched:** 2026-09-09
**Domain:** Édition idempotente et non destructive d'un fichier de configuration tiers (`~/.claude/settings.json`) depuis .NET 8 / System.Text.Json
**Confidence:** HIGH (schéma officiel Claude Code vérifié ; comportements System.Text.Json vérifiés **empiriquement** sur .NET 8 ; défaut de production observé sur le fichier réel)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

*(aucune décision d'implémentation verrouillée — `workflow.skip_discuss=true`)*

### Claude's Discretion

Tous les choix d'implémentation sont à la discrétion de Claude — la phase de discussion est
désactivée (`workflow.skip_discuss=true`). S'appuyer sur le goal de la ROADMAP, les critères de
succès et les conventions de la base de code.

### Contraintes non négociables (portées par l'utilisateur)

- **Sauvegarder `~/.claude/settings.json` avant toute modification.**
- **Ne jamais toucher aux entrées non-Chronos** (hooks GSD, autres outils) : elles survivent intactes.
- **Un fichier malformé ou illisible ne provoque aucun crash** au démarrage — dégradation silencieuse.
- Repérage par **marqueur d'argument** (`--hook`, `--statusline`), jamais par chemin d'exe : c'est
  précisément le chemin qui change à chaque version et qui a causé le cumul.
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin.
- UI et commentaires en français.

### Deferred Ideas (OUT OF SCOPE)

Aucune — la phase de discussion a été sautée.
Hors périmètre explicite du CONTEXT : **tout le pipeline d'usage** (persistance, delta, en-têtes,
jeton, doctrine du composite).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **PUR-01** | L'installation des **hooks remplace** les entrées Chronos existantes au lieu de les cumuler (match sur `--hook`, pas sur le chemin d'exe) | § Architecture Pattern 1 (prédicat d'identité), Pattern 2 (retirer-puis-ajouter), Pitfall 1 (cause racine `IsOurEntry` ligne 100-106) |
| **PUR-02** | L'installation du **pont statusLine remplace** l'entrée Chronos existante (match sur `--statusline`) | § Architecture Pattern 3 (`statusLine` est un objet unique → le vrai défaut est la **reconstruction** qui détruit `padding`), Pitfall 2 (`IsEnabled()` renvoie `true` sur un exe périmé → jamais repointé) |
| **PUR-03** | Les **entrées fantômes** déjà présentes dans `~/.claude/settings.json` sont **purgées** (25 hooks au lieu de 5) | § Architecture Pattern 4 (réconciliation au démarrage), § Runtime State Inventory, Open Question 2 (point d'entrée sûr) |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Directives actionnables extraites de `./CLAUDE.md` — le planner doit vérifier la conformité :

| Directive | Impact sur la Phase 15 |
|-----------|------------------------|
| **MVVM strict**, `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers `Models/Views/ViewModels/Services` | Le réconciliateur est un **service neutre** → `src/Chronos/Services/`. Aucun `MessageBox` dedans (sinon `ServicesLayerPurityTests` tombe). |
| **Chemins sous profil utilisateur uniquement, aucun droit admin** | Sauvegardes dans `%APPDATA%\Chronos\`, cible `%USERPROFILE%\.claude\settings.json`. Aucun accès `HKLM`, `Program Files`, `ProgramData`. |
| **Aucune dépendance native, aucun paquet ajouté** | `System.Text.Json` (intégré à net8.0). **Ne rien installer.** |
| **Robustesse : aucune source disponible ≠ crash ; parsing tolérant** | Fichier absent / malformé / verrouillé → aucune exception remonte au démarrage, et surtout **aucune réécriture**. |
| **UI et commentaires en français** | XML-doc, commentaires et noms de tests en français (`Install_ne_cumule_pas_...`). |
| **`PublishTrimmed=false`, pas d'`Assembly.Location`** | Utiliser `Environment.ProcessPath` (déjà le cas dans `SessionsController.cs:35` et `StatusLineSetup.cs:26`) — fiable en mono-fichier. |
| **GSD Workflow Enforcement** | Aucune édition hors `/gsd:execute-phase`. |
| **Statut des tests** | Les **328** tests xUnit doivent rester verts (mesuré : `dotnet test` → 328/328, 38 s), dont `ServicesLayerPurityTests` et `CompositionRootTests`. |

## Summary

Le défaut n'est **pas** « les installateurs oublient de nettoyer ». Il est plus précis, et la
distinction commande la conception : **les deux installateurs identifient une entrée Chronos par le
chemin de l'exe courant**. `SessionHookInstaller.IsOurEntry` (ligne 100-106) exige
`c.Contains("--hook") && c.Contains(cheminExeCourant)`. Depuis un exe fraîchement téléchargé, aucune
des entrées existantes ne matche → le garde d'idempotence ligne 69 (`if (arr.Any(IsOurEntry)) continue;`)
ne se déclenche jamais → un groupe de plus est ajouté à chaque version. Cinq versions = 25 groupes,
exactement ce qu'on observe. La logique de retrait existe déjà et fonctionne
(`TransformForUninstall`) : elle n'est simplement jamais appelée sur les *autres* versions.

Symétriquement pour `statusLine`, le défaut est **inverse et invisible** : `IsChronosCommand` accepte
`--statusline` + n'importe quelle commande contenant le mot « Chronos » — donc `IsEnabled()` renvoie
`true` alors que la commande pointe `Chronos-v2.8.1.exe` et qu'on tourne depuis le build Debug.
`OfferOnFirstRun` sort immédiatement, l'utilisateur n'est jamais reconsulté, et le pont reste **branché
sur un exe périmé pour toujours**. `statusLine` étant un objet unique (schéma officiel vérifié), il n'y
a pas de cumul possible : PUR-02 n'est pas « dédupliquer », c'est **repointer** et **cesser de
reconstruire l'objet** (la reconstruction actuelle détruit le champ officiel optionnel `padding`).

Troisième découverte, la plus grave, non énoncée dans les exigences mais qui les rend caduques si on
l'ignore : **le mode de défaillance actuel est destructif.** `ParseObject` / `Parse` attrapent toute
exception et renvoient `new JsonObject()`, puis le code écrit ce nouvel objet par-dessus le fichier.
Un `settings.json` contenant un commentaire, une virgule traînante, une clé dupliquée, ou dont la
racine n'est pas un objet, est donc **intégralement effacé** et remplacé par `{ "statusLine": … }` ou
`{ "hooks": … }`. Permissions, `env`, `model`, hooks GSD : tout disparaît, silencieusement. C'est la
violation directe du critère de succès 4 de la ROADMAP (« un fichier illisible ou malformé ne provoque
aucun crash ») — il n'y a effectivement pas de crash, il y a pire.

**Primary recommendation :** introduire un service neutre `ClaudeSettingsReconciler` dans
`Services/`, bâti sur un **cœur pur** `Reconcilier(string? json, string exeCourant, bool hooksVoulus) → string?`
(renvoie `null` = « ne rien écrire »), qui **mute l'arbre `JsonNode` analysé** au lieu de le
reconstruire, identifie Chronos par **marqueur d'argument + nom de fichier `Chronos*.exe`** (jamais le
chemin), et **abandonne** (sans écrire) dès que le fichier n'est pas un objet JSON exploitable.
L'appeler **une seule fois, au démarrage en mode overlay** (`App.xaml.cs`, juste avant
`OfferOnFirstRun()`), jamais en mode `--hook` ni `--statusline`. Sauvegarde horodatée dans
`%APPDATA%\Chronos\backups\` **uniquement quand une écriture va réellement avoir lieu**.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Text.Json` (`JsonNode` / `JsonObject` / `JsonArray`) | intégré à `net8.0` | Lecture/mutation/réécriture de `~/.claude/settings.json` en préservant les clés inconnues | **Déjà utilisé par les deux installateurs.** Le modèle DOM mutable `JsonNode` est le seul de la BCL qui préserve l'ordre des clés et les champs qu'on ne connaît pas. Aucun paquet à ajouter (contrainte projet). |
| `System.Text.Encodings.Web.JavaScriptEncoder` | intégré à `net8.0` | `UnsafeRelaxedJsonEscaping` — éviter d'échapper accents et `& < > +` à la réécriture | Sans lui, réécrire le fichier transforme `Téléchargements` en `T\u00E9l\u00E9chargements` **dans toutes les valeurs du fichier**, y compris celles des autres outils. Vérifié empiriquement (voir Pitfall 5). |
| `System.IO.File` (`ReadAllText` / `WriteAllText` / `Move(overwrite)` / `Copy`) | intégré | E/S + écriture atomique temp→rename + copie de sauvegarde | Motif déjà en place (`WriteAtomic` dans les deux installateurs, `SettingsService.Save`). Ne pas réinventer. |
| xUnit + Xunit.StaFact | 2.9.2 / 1.1.11 | Tests | Déjà en place, 328 tests verts. |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Text.RegularExpressions` | intégré | Extraction du premier jeton exécutable d'une chaîne de commande | **Optionnel.** Un parseur manuel de 8 lignes (guillemet ouvrant → guillemet fermant, sinon jusqu'au premier espace) est plus lisible et plus prévisible qu'une regex. Recommandé : pas de regex. |
| `System.Threading.Mutex` | intégré | Sérialiser le cycle lire-modifier-écrire entre instances Chronos | **Optionnel, faible valeur.** Ne protège pas contre Claude Code lui-même qui réécrit le fichier. Voir Pitfall 8. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `JsonNode` (DOM mutable) | `JsonSerializer` vers un POCO `ClaudeSettings` typé | **Rédhibitoire.** Toute clé non modélisée (`permissions`, `env`, `model`, `agentPushNotifEnabled`, clés futures de Claude Code) serait perdue à la sérialisation. Viole directement « ne jamais toucher aux entrées non-Chronos ». `[JsonExtensionData]` récupérerait les clés mais **perdrait leur ordre**. |
| Mutation du DOM analysé | Reconstruction (`root["statusLine"] = new JsonObject{…}`) | C'est **le code actuel** de `StatusLineInstaller:79-83`, et c'est le défaut : il détruit `padding` (champ officiel documenté) et toute clé future du même objet. À supprimer. |
| Réécriture complète | Édition textuelle ciblée (regex sur le fichier brut) | Préserverait commentaires et mise en forme, mais fragile sur une structure imbriquée à 3 niveaux. Rejeté : le risque de corruption dépasse le gain. |
| Sauvegarde dans `%APPDATA%\Chronos\backups\` | `~/.claude/settings.json.bak` | Rejeté : pollue un répertoire que Chronos ne possède pas, et un `.bak` écrasé perd **la sauvegarde la plus précieuse** — celle du tout premier passage, seule à contenir l'état pré-purge complet. |
| Purge au démarrage **et** idempotence à l'installation | Idempotence à l'installation seule | Rejeté : ne satisfait pas PUR-03. La machine polluée doit se nettoyer « sans intervention manuelle de l'utilisateur » (critère de succès 3), donc sans qu'il rouvre le menu. |

**Installation :** aucune. Aucun paquet NuGet à ajouter.

**Version verification :** sans objet — zéro nouvelle dépendance. Toolchain vérifiée sur la machine :
`dotnet --version` → **10.0.201** (SDK), cible `net8.0-windows`, build et 328 tests verts en 38 s.

## Architecture Patterns

### Structure recommandée

```
src/Chronos/Services/
├── ClaudeSettingsReconciler.cs   # NOUVEAU — service neutre : cœur pur + E/S + sauvegarde
├── SessionHookInstaller.cs       # MODIFIÉ — prédicat d'identité + retirer-puis-ajouter + parse tolérant
└── StatusLineInstaller.cs        # MODIFIÉ — mutation au lieu de reconstruction + parse tolérant

src/Chronos/App.xaml.cs           # MODIFIÉ — enregistrement DI + 1 appel après window.Show()

tests/Chronos.Tests/
├── ClaudeSettingsReconcilerTests.cs  # NOUVEAU — cœur pur + E/S sur dossier temp
├── SessionsTests.cs                  # ÉTENDU — non-cumul multi-chemins, hooks tiers intacts
└── StatusLineInstallerTests.cs       # ÉTENDU — repointage, préservation de `padding`
```

### Pattern 1 : prédicat d'identité en deux volets (marqueur + nom d'exe)

**What:** une entrée est « à Chronos » si **(a)** sa commande contient le marqueur d'argument
(`--hook` / `--statusline`) **et (b)** le premier jeton exécutable de la commande, réduit à son nom de
fichier, correspond à `Chronos*.exe`.

**When to use:** partout où l'on décide de retirer, remplacer ou repointer une entrée. **Jamais** le
chemin complet.

**Why the second condition:** le marqueur seul est insuffisant. `~/.claude/settings.json` contient
déjà `node "C:/Users/Tanguy/.claude/hooks/gsd-check-update.js"` ; rien n'empêche GSD ou un plugin
d'adopter demain un drapeau `--hook` ou `--statusline`. Le volet (b) borne le rayon d'action à des
binaires que Chronos est le seul à produire. Vérifié contre le fichier réel : les 25 entrées Chronos
matchent (`Chronos-v2.5.exe`, `Chronos-v2.5.1.exe`, `Chronos-v2.6.exe`, `Chronos-v2.8.1.exe`,
`Chronos.exe` du build Debug), les 3 entrées GSD ne matchent pas.

**Why not the exe path:** c'est la cause racine. Le chemin change à chaque version ; c'est
littéralement la variable qu'on refuse de suivre.

```csharp
// Extrait le premier jeton exécutable : segment entre guillemets s'il y en a, sinon jusqu'au
// premier espace. Sûr sur « "C:/a b/Chronos.exe" --hook Stop » comme sur « node x.js ».
private static string PremierJeton(string commande)
{
    var s = commande.TrimStart();
    if (s.Length == 0) return "";
    if (s[0] == '"')
    {
        var fin = s.IndexOf('"', 1);
        return fin > 0 ? s[1..fin] : s[1..];
    }
    var esp = s.IndexOf(' ');
    return esp > 0 ? s[..esp] : s;
}

/// <summary>Une commande est « à Chronos » si elle porte le MARQUEUR et que son exécutable
/// s'appelle Chronos*.exe. Jamais de comparaison sur le CHEMIN (cause du cumul historique).</summary>
internal static bool EstCommandeChronos(string? commande, string marqueur)
{
    if (string.IsNullOrWhiteSpace(commande)) return false;
    if (commande.IndexOf(marqueur, StringComparison.OrdinalIgnoreCase) < 0) return false;

    var nom = Path.GetFileName(PremierJeton(commande).Replace('\\', '/'));
    return nom.StartsWith("Chronos", StringComparison.OrdinalIgnoreCase)
        && nom.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
}
```

### Pattern 2 : retirer-puis-ajouter (hooks), en mutant le tableau en place

**What:** pour chaque événement, supprimer **tous** les groupes Chronos par `RemoveAt` en parcours
descendant, puis ajouter le groupe courant unique. Jamais « chercher, si absent ajouter ».

**When to use:** `SessionHookInstaller.TransformForInstall`, et la partie hooks du réconciliateur.

**Why:** un remplacement inconditionnel est idempotent **par construction** — il ne dépend d'aucun
prédicat de présence exact. Même si le prédicat d'identité rate une variante exotique un jour, on
n'empile plus : au pire on laisse une orpheline, on n'en crée pas une de plus.

**Why `RemoveAt` descendant plutôt que reconstruire le tableau :** vérifié empiriquement — rattacher
un nœud déjà parenté à un nouveau `JsonArray` lève
`InvalidOperationException: The node already has a parent`. La parade habituelle (`DeepClone()` sur
chaque élément, comme le fait `TransformForUninstall:91`) fonctionne mais recrée inutilement tout le
sous-arbre. `RemoveAt` en place est plus simple, préserve l'ordre des survivants, et ne peut pas
reparenter.

```csharp
var arr = hooks[ev] as JsonArray ?? new JsonArray();
hooks[ev] = arr;                                    // réassigner la MÊME instance : vérifié sans levée

for (int i = arr.Count - 1; i >= 0; i--)            // descendant : les indices restants restent valides
    if (EstGroupeChronos(arr[i], "--hook")) arr.RemoveAt(i);

if (hooksVoulus)
    arr.Add(new JsonObject                          // pas de clé "matcher" → défaut « * », conforme à l'existant
    {
        ["hooks"] = new JsonArray(new JsonObject
        {
            ["type"] = "command",
            ["command"] = HookCommand(exePath, ev),  // slashes AVANT : leçon terrain, à conserver
            ["timeout"] = 10,
        }),
    });

if (arr.Count == 0) hooks.Remove(ev);               // événement devenu vide → retirer la clé
```

### Pattern 3 : `statusLine` — muter `command`, jamais reconstruire l'objet

**What:** `statusLine` est un **objet unique**, pas un tableau (schéma officiel vérifié). Il n'y a
donc structurellement aucun cumul possible. Le correctif PUR-02 est double :

1. **Repointer** : si `statusLine.command` est une commande Chronos (Pattern 1) mais ne désigne pas
   l'exe courant, réécrire **uniquement** la valeur `command`.
2. **Cesser de reconstruire** : remplacer
   `root["statusLine"] = new JsonObject { ["type"]=…, ["command"]=… }` par une mutation ciblée qui
   préserve les clés voisines.

**Why:** le champ `padding` est **officiellement documenté et optionnel**
(`{"type":"command","command":"…","padding":2}`). La reconstruction actuelle l'efface sans bruit, et
effacera de la même façon toute clé ajoutée par Claude Code à l'avenir.

```csharp
if (root["statusLine"] is JsonObject sl && sl["command"] is JsonValue cv
    && cv.TryGetValue<string>(out var cmd) && EstCommandeChronos(cmd, "--statusline"))
{
    sl["command"] = ChronosCommand(exePath);   // MUTATION ciblée : "type", "padding", clés futures intactes
}
// Si la commande n'est PAS à Chronos → on ne touche à rien. Le consentement d'installation
// reste porté par StatusLinePromptDismissed / le menu, pas par le réconciliateur.
```

### Pattern 4 : réconciliation vers un état désiré, au démarrage

**What:** au lieu de « purger » impérativement, calculer l'**état désiré** du fichier à partir des
réglages persistés de Chronos, puis converger. Le cœur est une fonction pure ; l'appelant n'écrit que
si le résultat diffère de l'entrée.

| Réglage Chronos | État désiré dans `~/.claude/settings.json` |
|---|---|
| `SessionsWidgetEnabled == true` | **Exactement un** groupe Chronos par événement des 5, pointant sur l'exe courant |
| `SessionsWidgetEnabled == false` | **Zéro** groupe Chronos (tous les groupes présents sont des fantômes) |
| `statusLine.command` est une commande Chronos | Repointée sur l'exe courant |
| `statusLine.command` n'est pas à Chronos, ou absente | **Intouchée** (ne jamais installer sans consentement) |
| Toute entrée non-Chronos | **Intouchée, à sa place, avec ses champs** |

**Why:** un état désiré est testable comme un point fixe. `Reconcilier(Reconcilier(x)) == Reconcilier(x)`
est la formulation exacte de PUR-01/02 et se teste en une assertion.

**Signature recommandée** — `null` signifie « rien à faire », ce qui porte à la fois l'abandon sur
fichier illisible et l'absence de changement :

```csharp
/// <summary>
/// Cœur PUR. Renvoie le JSON réconcilié, ou <c>null</c> si rien ne doit être écrit
/// (fichier déjà conforme, ou inexploitable → on n'y touche pas).
/// </summary>
public static string? Reconcilier(string? settingsJson, string exePath, bool hooksVoulus)
```

### Anti-Patterns to Avoid

- **Se rabattre sur `new JsonObject()` quand l'analyse échoue.** C'est le code actuel
  (`StatusLineInstaller:120`, `SessionHookInstaller:114`) et il **efface tout le fichier**. Un échec
  d'analyse doit produire « je n'écris rien », jamais « je repars d'une page blanche ».
- **Reconstruire un nœud pour en changer un champ.** Détruit les clés voisines inconnues.
- **Déclencher la réconciliation en mode `--hook`.** Cinq processus concurrents en lire-modifier-écrire
  sur le même fichier : la dernière écriture gagne, les autres purges sont perdues, et le fichier peut
  finir dans un état incohérent. Le court-circuit `App.xaml.cs:29-35` sort avant le host — il faut que
  ça reste vrai.
- **Déclencher la réconciliation en mode `--statusline`.** Ce mode est invoqué à **chaque rendu** de la
  barre de statut de Claude Code. Une écriture de settings.json à cette cadence est inacceptable.
- **Écrire (et donc sauvegarder) alors que rien n'a changé.** Produit une sauvegarde à chaque
  démarrage, fait tourner la rétention, et finit par évincer la seule sauvegarde qui comptait.
- **Sérialiser avec l'encodeur par défaut.** Réécrit en `\uXXXX` toutes les valeurs non-ASCII du
  fichier, y compris celles des autres outils. Diff illisible et churn gratuit.
- **`GetValue<string>()` sur un champ dont on n'a pas prouvé le type.** Lève
  `InvalidOperationException` — pas une `JsonException`.
- **Élargir la purge aux `settings.json` de projet.** Voir Open Question 6 : hors périmètre, et le
  scan a confirmé qu'il n'y a rien à y purger.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Préserver clés inconnues, ordre, `matcher`, `timeout`, `args`, `if` | Un POCO typé + `JsonSerializer` | Mutation du DOM `JsonNode` | Vérifié : le DOM préserve ordre, clés inconnues et **texte brut des nombres** (`5817635413`, `1.0`, `1e3`, `-0.500` inchangés). Un POCO perd tout ce qu'il ne modélise pas. |
| Écriture sans fichier partiel observable | `File.WriteAllText` direct sur la cible | `WriteAtomic` (temp + `File.Move(overwrite:true)`) | **Déjà écrit et éprouvé** dans les deux installateurs et `SettingsService.Save`. Réutiliser tel quel. |
| Retirer un événement devenu vide | Nouvelle logique | `SessionHookInstaller.cs:92` (`if (kept.Length == 0) hooks.Remove(ev)`) | Existe et est testé. |
| Résoudre le chemin de l'exe courant en mono-fichier | `Assembly.Location` / `GetExecutingAssembly()` | `Environment.ProcessPath` | `Assembly.Location` est **vide** en mono-fichier (piège documenté dans CLAUDE.md). `Environment.ProcessPath` est déjà utilisé aux deux points d'appel. |
| Tolérance commentaires / virgules traînantes | Pré-nettoyage textuel maison | `JsonDocumentOptions { CommentHandling = Skip, AllowTrailingCommas = true }` | Un « strip comments » maison casse sur `//` à l'intérieur d'une chaîne — et les chemins contiennent des `//`. |
| Normaliser un chemin en séparateurs avant | Manipulation ad hoc | `HookCommand` (`SessionHookInstaller:31-32`) | Le motif « slashes avant » est une leçon terrain vérifiée. Ne pas le réinventer, ne pas le contredire. |

**Key insight :** presque tout le code nécessaire existe déjà dans le dépôt et fonctionne. Cette phase
n'ajoute pas de mécanique, elle **corrige un prédicat, remplace un ajout conditionnel par un
remplacement inconditionnel, et transforme un repli destructif en abandon**. Le volume de code neuf
est faible ; le volume de tests neufs est le vrai livrable.

## Runtime State Inventory

Phase de correction d'état runtime : c'est le cœur du sujet, l'inventaire est le livrable principal.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| **Stored data** | `~/.claude/settings.json` (6 872 octets, dernière écriture 2026-07-30) : **25 groupes de hooks Chronos** répartis sur 5 événements × 5 chemins d'exe, **plus** `statusLine.command` pointant `"C:\Users\Tanguy\Downloads\Chronos-v2.8.1.exe" --statusline`. Doivent survivre : `SessionStart` grp0 (`node ".../gsd-check-update.js"`), `PostToolUse` (matcher `Bash\|Edit\|Write\|MultiEdit\|Agent\|Task`, `gsd-context-monitor.js`, timeout 10), `PreToolUse` (matcher `Write\|Edit`, `gsd-prompt-guard.js`, timeout 5), et la clé racine `agentPushNotifEnabled`. | **Migration de données** (purge des 20 fantômes + repointage `statusLine`) **ET édition de code** (l'installateur doit cesser d'en créer). Les deux sont nécessaires : l'une n'implique pas l'autre. |
| | `%APPDATA%\Chronos\sessions\*.json` — états de session écrits en concurrence par les 5 exes fantômes à chaque événement Claude Code. | **Aucune action.** Ces fichiers sont clés par `session_id` et réécrits atomiquement ; la purge des hooks tarit la source. Ne pas ajouter de nettoyage (hors périmètre). |
| **Live service config** | Aucune autre. `~/.claude/settings.json` est un fichier, lu à chaud par Claude Code. Vérifié : **aucun** `settings.json` de projet ni `settings.local.json` (utilisateur ou projet) ne contient `--hook` — seul le fichier utilisateur est pollué. Pas de scope entreprise/`managed-settings.json` sur cette machine. | **Aucune** — mais le périmètre doit rester le fichier **utilisateur** (voir Open Question 6). |
| **OS-registered state** | Raccourci d'autostart `shell:startup` (`AutostartService`) : pointe l'exe, indépendant de `settings.json`. Non affecté par cette phase. | **Aucune.** |
| **Secrets / env vars** | Aucun. `ChronosSettings.InnerStatusLineCommand` (chaînage de la barre préexistante) est une préférence, pas un secret. Vérifié : sur cette machine elle est nulle/absente (aucune barre tierce n'a jamais été capturée), donc **rien à préserver** au repointage. Le coffre `ChronosOAuthStore` (DPAPI) n'est pas touché. | **Aucune** — mais le code doit continuer de **ne pas écraser** `InnerStatusLineCommand` avec `null` lors d'une réconciliation. |
| **Build artifacts** | `src/Chronos/bin/Debug/net8.0-windows/Chronos.exe` est **enregistré comme hook** dans le fichier réel : un `dotnet build` ou un `git clean` peut le supprimer, laissant un hook pointant un exe inexistant. C'est l'un des 5 fantômes. | Couvert par la purge. **Ne pas** ré-enregistrer le build Debug lors des tests manuels — ou accepter qu'il soit purgé au prochain lancement d'un exe publié. |

**Le test de vérité de la phase :** après avoir lancé Chronos une fois depuis un exe publié, le
fichier réel doit contenir **5 groupes Chronos** (ou 0 si le widget est désactivé), **les 3 groupes GSD
intacts avec leurs `matcher` et `timeout`**, `agentPushNotifEnabled` intact, et `statusLine.command`
pointant l'exe courant.

## Common Pitfalls

### Pitfall 1 : le prédicat d'identité fondé sur le chemin d'exe (CAUSE RACINE)

**What goes wrong :** `SessionHookInstaller.IsOurEntry(entry, exePath)` exige que la commande contienne
le chemin de l'exe **courant**. Le garde d'idempotence `if (arr.Any(e => IsOurEntry(e, exePath))) continue;`
(ligne 69) ne voit donc jamais les entrées des versions précédentes.
**Why it happens :** le prédicat a été écrit pour `Uninstall` (où « mon exe » est le bon critère) et
réutilisé tel quel pour `Install` (où le bon critère est « n'importe quel Chronos »).
**How to avoid :** deux prédicats distincts. `EstCommandeChronos(cmd, marqueur)` pour l'installation et
la purge (n'importe quel Chronos), `EstCommandeChronos(cmd, marqueur) && cmd.Contains(exePath)` si l'on
veut une désinstallation strictement limitée à soi. **Recommandation : utiliser le prédicat large
partout**, y compris à la désinstallation — sinon `Disable()` depuis une nouvelle version laisserait
les hooks des anciennes.
**Warning signs :** compter les groupes après trois installations depuis trois chemins. C'est
exactement le critère de succès 1 de la ROADMAP.

### Pitfall 2 : `IsEnabled()` répond « oui » pour un exe périmé, donc rien ne se répare jamais

**What goes wrong :** `StatusLineInstaller.IsChronosCommand` accepte
`chemin.Contains(exePath) || chemin.Contains("Chronos")`. La commande `Chronos-v2.8.1.exe --statusline`
contient « Chronos » ⇒ `IsEnabled()` → `true` ⇒ `OfferOnFirstRun` sort ligne 70 ⇒ le pont reste branché
sur un exe périmé indéfiniment. C'est l'état observé.
**Why it happens :** un prédicat unique sert deux questions différentes — « est-ce à Chronos ? » et
« est-ce **ce** Chronos ? ».
**How to avoid :** séparer explicitement les deux. `EstAChronos(cmd)` (appartenance) et
`PointeVersExe(cmd, exePath)` (fraîcheur). `IsEnabled()` doit exiger les deux ; le réconciliateur agit
sur `EstAChronos && !PointeVersExe`.
**Warning signs :** un `IsInstalled` qui renvoie `true` alors que le chemin affiché dans le diagnostic
n'est pas celui du processus courant.

### Pitfall 3 : le repli sur `new JsonObject()` **efface tout le fichier** (le plus grave)

**What goes wrong :** `ParseObject` / `Parse` attrapent toute exception et renvoient un objet vide ;
l'appelant y écrit sa clé et `WriteAtomic` l'écrase sur le fichier. Le `settings.json` de l'utilisateur
est remplacé par `{ "statusLine": {…} }`. Silencieusement, sans erreur, sans sauvegarde.
Cas déclencheurs **vérifiés empiriquement** :

| Entrée | Comportement de `JsonNode.Parse` (.NET 8) |
|---|---|
| Commentaire `//` ou virgule traînante | `JsonReaderException` (options par défaut) |
| Clé dupliquée `{"a":1,"a":2}` | `Parse` **réussit**, puis **tout accès** (`o["a"]`, `o["c"] = …`) lève `ArgumentException: An item with the same key has already been added` — **pas** une `JsonException` |
| Racine tableau `[1,2,3]` ou scalaire | `Parse` réussit, `as JsonObject` → `null` |
| BOM UTF-8 en tête de la **chaîne** | `JsonReaderException` (mais `File.ReadAllText` retire le BOM — vérifié, risque faible) |

**How to avoid :**
1. Analyser avec `JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }`.
2. Envelopper l'analyse **et** le premier accès dans un `try/catch (Exception)` **large** (`ArgumentException`
   n'est pas une `JsonException`).
3. En cas d'échec ou de racine non-objet : **renvoyer `null` = ne rien écrire**. Ne jamais substituer
   un objet vide.
4. Consigner l'abandon (log `%APPDATA%\Chronos\chronos.log` via le motif existant) plutôt que de
   l'avaler complètement — « dégradation silencieuse » côté UI, pas côté journal.

**Warning signs :** un test qui passe un JSON malformé et vérifie seulement « pas d'exception » est
insuffisant — il doit vérifier que **le fichier sur disque est inchangé, octet pour octet**.

### Pitfall 4 : reconstruire un objet détruit ses clés voisines

**What goes wrong :** `root["statusLine"] = new JsonObject { ["type"]=…, ["command"]=… }` efface
`padding`, champ officiel documenté, et toute clé future.
**How to avoid :** muter `sl["command"]` en place. Ne créer un objet neuf que lorsque la clé est
totalement absente.
**Warning signs :** un test doit poser `"padding": 2` en entrée et l'exiger en sortie.

### Pitfall 5 : l'encodeur par défaut mutile tout le fichier

**What goes wrong :** vérifié — `JsonSerializerOptions { WriteIndented = true }` produit
`"C:/Users/Tanguy/T\u00E9l\u00E9chargements/Chronos.exe"` et `"a\u0026b\u003Cc\u003Ed\u002Be"`.
L'échappement s'applique à **toutes** les valeurs du fichier, pas seulement aux nôtres. Le
`settings.json` d'un utilisateur francophone (chemins accentués, `Téléchargements`, `Bureau`,
messages de hooks en français) devient illisible et le diff explose.
**Why it happens :** `JavaScriptEncoder.Default` échappe tout le non-ASCII et les caractères sensibles
au HTML, par prudence pour l'injection dans une page web. Sans objet pour un fichier de config local.
**How to avoid :** `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`. Vérifié : conserve les
accents et `& < > +` littéraux, tout en échappant correctement `"` et les caractères de contrôle
(`\t`). Le nom « Unsafe » vise le contexte HTML, pas la validité JSON.
**Warning signs :** un test avec un chemin accentué en entrée qui doit ressortir littéral.

### Pitfall 6 : les commentaires sont perdus à la réécriture (irréductible)

**What goes wrong :** `CommentHandling = Skip` permet de **lire** un fichier commenté, mais
System.Text.Json ne sait pas **réémettre** les commentaires. Les réécrire les efface.
**How to avoid :** impossible techniquement sans édition textuelle. **La sauvegarde horodatée est la
parade** — et c'est précisément l'argument qui justifie l'exigence de sauvegarde de l'utilisateur.
À documenter dans la XML-doc du réconciliateur. Risque réel faible (le `settings.json` observé est du
JSON strict).

### Pitfall 7 : reparenter un `JsonNode` lève

**What goes wrong :** vérifié — ajouter dans un `JsonArray` un nœud qui appartient déjà à un autre
lève `InvalidOperationException: The node already has a parent`.
**How to avoid :** `RemoveAt` en parcours descendant (mutation en place, aucun reparentage), ou
`DeepClone()` comme le fait déjà `TransformForUninstall:91`. Vérifié aussi : réassigner **la même
instance** de tableau à **la même clé** (`hooks[ev] = arr`) ne lève pas — le code existant est sain.

### Pitfall 8 : concurrence lire-modifier-écrire

**What goes wrong :** `WriteAtomic` garantit qu'on n'observe jamais un fichier partiel, mais **pas**
que deux écrivains ne s'écrasent pas. Trois écrivains possibles : (a) l'overlay Chronos, (b) les
processus `--hook` (5 en parallèle par événement, dans l'état pollué), (c) **Claude Code lui-même**.
**How to avoid :**
- (b) est éliminé par construction : le mode `--hook` sort ligne 33 avant le host et ne doit **jamais**
  toucher `settings.json`. À garder explicite dans le plan.
- (a) une seule réconciliation, une seule fois par démarrage, en mode overlay.
- (c) irréductible, et de faible probabilité (fenêtre de quelques millisecondes au démarrage). Un
  `Mutex` ne protégerait pas contre Claude Code de toute façon. **Accepter et documenter** ; la
  sauvegarde couvre le pire cas.

### Pitfall 9 : `GetValue<string>()` sur un champ non-string

**What goes wrong :** vérifié — `GetValue<string>()` sur un nombre lève
`InvalidOperationException: An element of type 'Number' cannot be converted to a 'System.String'`.
`SessionHookInstaller.cs:104` fait exactement `ho["command"]?.GetValue<string>()`. Le schéma officiel
autorise désormais cinq types de handlers (`command`, `http`, `mcp_tool`, `prompt`, `agent`) : un
handler `http` n'a **pas** de champ `command`, un handler mal formé peut en avoir un non-string.
**How to avoid :** `node is JsonValue v && v.TryGetValue<string>(out var s)` — vérifié : renvoie
`false` proprement sur un nombre.

### Pitfall 10 : sauvegarder à chaque démarrage

**What goes wrong :** sauvegarder inconditionnellement fait tourner la rétention et évince la
sauvegarde du **premier** passage — la seule qui contienne l'état pré-purge complet.
**How to avoid :** comparer la chaîne réconciliée à la chaîne lue ; si identiques, **ne pas écrire, ne
pas sauvegarder**. C'est aussi le test le plus simple de l'idempotence (deuxième passe ⇒ `null`).

## Code Examples

### Analyse tolérante qui abandonne au lieu d'effacer

```csharp
// Source : comportements vérifiés empiriquement sur .NET 8 (sonde 2026-09-09)
private static readonly JsonDocumentOptions LectureTolerante = new()
{
    CommentHandling = JsonCommentHandling.Skip,   // sinon « // » lève JsonReaderException
    AllowTrailingCommas = true,
};

/// <summary>
/// Analyse tolérante. Renvoie null si le contenu n'est pas un OBJET JSON exploitable —
/// l'appelant doit alors NE RIEN ÉCRIRE (jamais repartir d'un objet vide : cela effacerait
/// permissions, env, model et les hooks des autres outils).
/// </summary>
private static JsonObject? AnalyserOuNull(string? json)
{
    if (string.IsNullOrWhiteSpace(json)) return new JsonObject();   // fichier absent/vide → départ légitime
    try
    {
        if (JsonNode.Parse(json, nodeOptions: null, LectureTolerante) is not JsonObject o) return null;
        _ = o.Count;   // force la matérialisation : lève ArgumentException si clés dupliquées
        return o;
    }
    catch { return null; }   // catch LARGE : ArgumentException n'est PAS une JsonException
}
```

### Sérialisation fidèle

```csharp
// Source : System.Text.Encodings.Web — vérifié : conserve accents et & < > + littéraux
private static readonly JsonSerializerOptions Sortie = new()
{
    WriteIndented = true,                                       // 2 espaces — identique au style Claude Code
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,      // pas de \u00E9 sur « Téléchargements »
};
```

### Sauvegarde horodatée conditionnelle + rétention

```csharp
/// <summary>
/// Copie l'original dans %APPDATA%\Chronos\backups\ AVANT toute écriture. Ne conserve que les
/// <paramref name="garder"/> plus récentes. Renvoie false si la sauvegarde a échoué → l'appelant
/// ABANDONNE l'écriture (le settings.json de l'utilisateur vaut plus que la fonctionnalité).
/// </summary>
private bool Sauvegarder(int garder = 5)
{
    try
    {
        Directory.CreateDirectory(_backupDir);
        var cible = Path.Combine(_backupDir,
            $"claude-settings-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.Copy(_settingsPath, cible, overwrite: true);

        foreach (var vieux in new DirectoryInfo(_backupDir)
                     .GetFiles("claude-settings-*.json")
                     .OrderByDescending(f => f.Name)
                     .Skip(garder))
            try { vieux.Delete(); } catch { }   // rétention best-effort : un échec ici n'est pas bloquant

        return true;
    }
    catch { return false; }
}
```

### Point d'appel dans `App.xaml.cs` (mode overlay uniquement)

```csharp
window.Show();

// PUR-01/02/03 : réconcilier ~/.claude/settings.json AVANT de proposer la source exacte, pour que
// l'offre porte sur un état propre. Mode overlay UNIQUEMENT — jamais en --hook (5 processus
// concurrents en lire-modifier-écrire) ni en --statusline (invoqué à chaque rendu de la barre).
// Best-effort et silencieux : ne peut pas empêcher le démarrage.
try { _host.Services.GetRequiredService<ClaudeSettingsReconciler>().Reconcilier(); } catch { }

_host.Services.GetRequiredService<IStatusLineSetup>().OfferOnFirstRun();
_host.Services.GetRequiredService<ISessionsController>().ShowIfEnabled();
```

### Test d'idempotence multi-chemins (critère de succès 1)

```csharp
[Fact]
public void Trois_chemins_dexe_successifs_ne_laissent_que_cinq_hooks()
{
    string? json = null;
    foreach (var exe in new[] { @"C:\DL\Chronos-v2.5.exe", @"C:\DL\Chronos-v2.6.exe", @"C:\DL\Chronos-v2.8.1.exe" })
        json = SessionHookInstaller.TransformForInstall(json, exe);

    var hooks = (JsonNode.Parse(json!) as JsonObject)!["hooks"] as JsonObject;
    foreach (var ev in SessionHookInstaller.Events)
    {
        var groupes = (hooks![ev] as JsonArray)!;
        Assert.Single(groupes);   // 1 groupe, pas 3
        Assert.Contains("C:/DL/Chronos-v2.8.1.exe", groupes[0]!["hooks"]![0]!["command"]!.GetValue<string>());
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hook handler = `{ type, command, timeout }` uniquement | 5 types de handlers : `command`, `http`, `mcp_tool`, `prompt`, `agent` ; champs `if`, `args`, `async`, `asyncRewake`, `shell`, `statusMessage`, `once` | Documentation actuelle de Claude Code | Un handler voisin peut **ne pas avoir** de champ `command`. Le prédicat doit le tolérer sans lever. |
| ~8 événements de hooks | ~30 événements (`Setup`, `StopFailure`, `PermissionRequest`, `PostToolBatch`, `SubagentStart`, `FileChanged`, `PreCompact`…) | Documentation actuelle | Le réconciliateur ne doit itérer que sur **les 5 événements de Chronos** et ne jamais toucher aux clés d'événements qu'il ne connaît pas. |
| `statusLine = { type, command }` | `statusLine = { type, command, padding? }` | Documentation actuelle | `padding` est officiel et optionnel → la reconstruction de l'objet est un bug. |
| Dédoublonnage supposé côté Claude Code | **« All matching hooks run in parallel. If you define the same handler in more than one settings file, it runs once. »** | Documentation actuelle | Le dédoublonnage n'opère qu'entre **fichiers de settings différents**, jamais entre groupes du **même** fichier. Confirme que les 25 groupes lancent bien 25 processus. Aucun secours à attendre de Claude Code. |
| `~/.claude/settings.json` seul | 5 niveaux : managed → `claude --settings` → `.claude/settings.local.json` → `.claude/settings.json` → `~/.claude/settings.json` | Documentation actuelle | Le périmètre reste le fichier **utilisateur** (le seul que Chronos écrit). Vérifié : aucun autre niveau n'est pollué sur cette machine. |

**Déprécié / à ne plus faire dans ce code :**
- Repli sur `new JsonObject()` en cas d'échec d'analyse — destructif.
- Reconstruction d'un objet JSON pour en changer un champ — perd les clés voisines.
- Identification d'une entrée par le chemin d'exe — cause racine du cumul.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build / test / publish | ✓ | 10.0.201 (cible `net8.0-windows`) | — |
| xUnit + runner | Suite de 328 tests | ✓ | xunit 2.9.2 / runner.visualstudio 2.8.2 / Xunit.StaFact 1.1.11 | — |
| `System.Text.Json` | Édition de `settings.json` | ✓ | intégré à net8.0 | — |
| `~/.claude/settings.json` | Cible de la purge (PUR-03) | ✓ | 6 872 o, 2026-07-30, JSON strict, 3 clés racine (`hooks`, `statusLine`, `agentPushNotifEnabled`) | Fichier absent → objet vide, comportement déjà correct |
| `%APPDATA%\Chronos\` | Dossier des sauvegardes | ✓ | existant (`usage.json`, `settings.json`, `sessions/`, `chronos.log`) | `Directory.CreateDirectory` |
| Droits admin | — | non requis | — | — |

**Missing dependencies with no fallback :** aucune.
**Missing dependencies with fallback :** aucune.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]` pour les tests STA) |
| Config file | `tests/Chronos.Tests/Chronos.Tests.csproj` (`net8.0-windows`, `UseWPF`, `IsTestProject`) |
| Quick run command | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --nologo -v q --filter "FullyQualifiedName~Installer\|FullyQualifiedName~Reconciler"` |
| Full suite command | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --nologo -v q` |
| Baseline mesuré | **328 réussis / 0 échec, 38 s** (2026-09-09) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PUR-01 | 3 installations depuis 3 chemins d'exe ⇒ exactement 5 groupes Chronos | unit (cœur pur) | `dotnet test --filter "FullyQualifiedName~SessionsTests"` | ✅ `tests/Chronos.Tests/SessionsTests.cs` (à étendre) |
| PUR-01 | Les 3 groupes GSD survivent avec leurs `matcher` et `timeout` | unit | idem | ✅ à étendre |
| PUR-01 | `Reconcilier(Reconcilier(x)) == Reconcilier(x)` (point fixe) | unit | `--filter "FullyQualifiedName~ClaudeSettingsReconcilerTests"` | ❌ Wave 0 |
| PUR-02 | `statusLine` Chronos périmée ⇒ repointée sur l'exe courant | unit | `--filter "FullyQualifiedName~StatusLineInstallerTests"` | ✅ à étendre |
| PUR-02 | `padding` et clés inconnues de `statusLine` préservées | unit | idem | ✅ à étendre |
| PUR-02 | `statusLine` tierce (non-Chronos) intouchée | unit | idem | ✅ déjà couvert par `Uninstall_ne_touche_pas_une_barre_tierce`, à dupliquer pour l'install |
| PUR-03 | Fixture reproduisant les **25 groupes réels + 3 groupes GSD** ⇒ 5 groupes Chronos, 3 GSD intacts, `agentPushNotifEnabled` intacte | unit (cœur pur, fixture figée) | `--filter "FullyQualifiedName~ClaudeSettingsReconcilerTests"` | ❌ Wave 0 |
| PUR-03 | Widget désactivé ⇒ **zéro** groupe Chronos, clé d'événement retirée si vide | unit | idem | ❌ Wave 0 |
| PUR-03 | Sauvegarde créée dans le dossier temp avant écriture ; **aucune** sauvegarde quand rien ne change | integration (dossier temp) | idem | ❌ Wave 0 |
| Critère 4 | JSON malformé (commentaire, virgule traînante, clé dupliquée, racine tableau) ⇒ **fichier inchangé octet pour octet**, aucune exception | integration (dossier temp) | idem | ❌ Wave 0 |
| Critère 4 | Chemin accentué (`Téléchargements`) ressort littéral, non échappé en `\uXXXX` | unit | idem | ❌ Wave 0 |
| Garde | `ServicesLayerPurityTests` : aucun type WPF dans le nouveau service | unit | `--filter "FullyQualifiedName~ServicesLayerPurityTests"` | ✅ existe |
| Garde | `CompositionRootTests` : le graphe DI résout toujours | unit | `--filter "FullyQualifiedName~CompositionRootTests"` | ✅ existe |

### Sampling Rate

- **Per task commit :** `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --nologo -v q --filter "FullyQualifiedName~Installer|FullyQualifiedName~Reconciler|FullyQualifiedName~SessionsTests"` (< 15 s)
- **Per wave merge :** suite complète (38 s), attendu **≥ 328** réussis, 0 échec
- **Phase gate :** suite complète verte **+ UAT manuelle sur le vrai `~/.claude/settings.json`** : sauvegarder le fichier hors du dépôt, lancer l'exe publié, vérifier 5 groupes Chronos / 3 groupes GSD intacts / `statusLine` repointée / `agentPushNotifEnabled` présente, puis vérifier qu'un second lancement **ne crée aucune nouvelle sauvegarde**.

### Wave 0 Gaps

- [ ] `tests/Chronos.Tests/ClaudeSettingsReconcilerTests.cs` — couvre PUR-03 + critère de succès 4
- [ ] `tests/Chronos.Tests/TestData/claude-settings-pollue.json` — **fixture figée reproduisant l'état réel** (25 groupes Chronos sur 5 chemins + 3 groupes GSD avec `matcher`/`timeout` + `statusLine` périmée + `agentPushNotifEnabled`). Le dossier `tests/Chronos.Tests/TestData/` existe déjà.
- [ ] Helper `DossierTemp()` pour les tests d'E/S — le motif existe (`SessionsTests.TempDir()`, `AutostartServiceTests`) ; à factoriser ou dupliquer.
- [ ] **Garde anti-accident :** aucun test ne doit pouvoir toucher le vrai `~/.claude/settings.json`. Les installateurs ont déjà un paramètre `settingsPath` optionnel au constructeur — chaque test d'E/S **doit** le fournir depuis `Path.GetTempPath()`.
- Installation du framework : aucune, tout est en place.

## Open Questions

### 1. Quelle granularité de repérage d'une entrée « Chronos » ? — **TRANCHÉ**

- **Ce qu'on sait :** le fichier réel contient déjà des hooks tiers (`node ".../gsd-check-update.js"`)
  qui n'utilisent pas `--hook`. Les 5 chemins d'exe Chronos ont tous un nom de fichier en
  `Chronos*.exe` (`Chronos.exe`, `Chronos-v2.5.exe`, `Chronos-v2.5.1.exe`, `Chronos-v2.6.exe`,
  `Chronos-v2.8.1.exe`).
- **Recommandation :** **marqueur d'argument + nom de fichier de l'exécutable**, jamais le chemin.
  `EstCommandeChronos(cmd, marqueur)` = `cmd` contient le marqueur **ET** `Path.GetFileName(premier
  jeton)` correspond à `Chronos*.exe` (insensible à la casse). Le marqueur seul serait imprudent : rien
  n'empêche un outil tiers d'adopter `--hook` ou `--statusline`. Le nom de fichier borne le rayon
  d'action sans réintroduire la dépendance au chemin qui a causé le cumul.
- **Résidu accepté :** un binaire tiers nommé `Chronos*.exe` utilisant `--hook` serait capturé.
  Probabilité négligeable, et la sauvegarde couvre le cas.

### 2. Où déclencher la purge ? — **TRANCHÉ : les deux, avec des rôles distincts**

- **À l'installation** (`TransformForInstall`) : retirer-puis-ajouter inconditionnel ⇒ satisfait
  PUR-01/02 pour l'avenir.
- **Au démarrage** (réconciliation) : ⇒ satisfait PUR-03 sur la machine déjà polluée, « sans
  intervention manuelle de l'utilisateur » (critère de succès 3). L'installation seule ne suffit pas :
  l'utilisateur devrait rouvrir le menu et recliquer.
- **Point d'entrée le plus sûr :** `App.xaml.cs`, après `window.Show()` (ligne 88) et **avant**
  `OfferOnFirstRun()` (ligne 92), pour que l'offre porte sur un état déjà propre. Ce point est
  atteint **uniquement en mode overlay** : `--statusline` sort ligne 23, `--hook` ligne 33,
  `--cadrans` ligne 46, `--sessions` ligne 57. C'est précisément la propriété qu'on veut — voir
  Pitfall 8.

### 3. Comment rendre la purge testable sans toucher au vrai fichier ? — **TRANCHÉ : le motif existe déjà**

- **Cœur pur :** les deux installateurs exposent déjà des `static TransformForX(string? json, …) → string`
  sans E/S ; `StatusLineInstallerTests` ne fait *que* ça (6 tests, zéro fichier). Étendre ce motif au
  réconciliateur : `static string? Reconcilier(string? json, string exePath, bool hooksVoulus)`.
  Couvre PUR-01/02/03 et la préservation des tiers.
- **Couche E/S :** les constructeurs acceptent déjà un `settingsPath` optionnel
  (`SessionHookInstaller.cs:24`, `StatusLineInstaller.cs:23`) — la DI (`App.xaml.cs:192, 229`) les
  construit sans argument, donc sur le vrai fichier ; **les tests doivent fournir un chemin temp**.
  Ajouter un second paramètre optionnel `backupDir` (défaut `%APPDATA%\Chronos\backups`).
  Précédents dans le dépôt : `AutostartServiceTests` (dossier temp), `ArchiveStore` / `TreatedStore`
  avec chemin injecté dans `CompositionRootTests:126,130`.
- **Ne pas** ajouter le chemin de `~/.claude/settings.json` à `ChronosPaths` : il n'est pas dérivable
  de `UsageFile`, et le ferait sortir de l'isolation temp sur laquelle repose la doc de `ChronosPaths`
  (lignes 13-18).

### 4. Quelle stratégie de sauvegarde, et où ? — **TRANCHÉ**

- **Où :** `%APPDATA%\Chronos\backups\claude-settings-yyyyMMdd-HHmmss.json`. Convention projet
  (`ChronosPaths`), aucun droit admin, et surtout **ne pollue pas `~/.claude`** que Chronos ne
  possède pas.
- **Horodatée, pas écrasée :** un `.bak` unique écrasé détruirait au deuxième lancement la seule
  sauvegarde intéressante — celle de l'état pollué mais complet. Rétention des **5** plus récentes,
  purge des plus vieilles en best-effort.
- **Conditionnelle :** sauvegarder uniquement si une écriture va réellement avoir lieu (comparer la
  chaîne réconciliée à la chaîne lue). Sinon, une sauvegarde par démarrage évincerait la première.
- **Bloquante :** échec de la sauvegarde ⇒ **on n'écrit pas**. Le `settings.json` de l'utilisateur
  vaut plus que la fonctionnalité. (La purge de rétention, elle, reste non bloquante.)

### 5. Faut-il préserver l'ordre et les champs inconnus ? Quel risque en round-trip ? — **TRANCHÉ : oui, et le risque est réel mais entièrement cerné**

Vérifié empiriquement sur .NET 8 le 2026-09-09 (programme sonde exécuté sur cette machine) :

| Comportement | Résultat | Conséquence |
|---|---|---|
| Ordre des clés, clés inconnues, `matcher`, `timeout`, champs futurs | ✅ **préservés** — *à condition de muter l'arbre analysé* | Muter, jamais reconstruire |
| Nombres | ✅ **texte brut préservé** (`5817635413`, `1.0`, `1e3`, `-0.500` inchangés) | Aucun risque de reformatage |
| Indentation | ✅ 2 espaces, identique au style Claude Code | Diff propre |
| Non-ASCII et `& < > +` | ❌ **échappés** par l'encodeur par défaut | `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (obligatoire) |
| Commentaires / virgules traînantes | ❌ `JsonReaderException` par défaut | `JsonDocumentOptions { CommentHandling = Skip, AllowTrailingCommas = true }` |
| Commentaires à la réécriture | ❌ **perdus** (limite de System.Text.Json) | Irréductible ; couvert par la sauvegarde |
| Clés dupliquées | ❌ `Parse` réussit puis **tout accès** lève `ArgumentException` (pas `JsonException`) | `catch (Exception)` large, puis abandon |
| Racine non-objet | ❌ `as JsonObject` → `null` | Abandon, **jamais** `new JsonObject()` |
| Reparentage d'un nœud | ❌ `InvalidOperationException: The node already has a parent` | `RemoveAt` descendant, ou `DeepClone()` |
| Réassignation de la même instance à la même clé | ✅ sans levée | Le code existant est sain |
| BOM UTF-8 | ✅ retiré par `File.ReadAllText` ; `File.WriteAllText` n'en écrit pas | Risque faible |
| `GetValue<string>()` sur non-string | ❌ `InvalidOperationException` | `TryGetValue<string>` (renvoie `false` proprement) |

### 6. Étendre la purge aux `settings.json` de projet ? — **NON (périmètre)**

- **Ce qu'on sait :** Claude Code lit 5 niveaux de configuration (managed → `--settings` →
  `.claude/settings.local.json` → `.claude/settings.json` → `~/.claude/settings.json`). Chronos
  n'écrit **que** le niveau utilisateur. Scan effectué : `--hook` n'apparaît que dans
  `~/.claude/settings.json`. Le projet a bien un `.claude/settings.local.json` (199 o) mais sans hook
  Chronos.
- **Recommandation :** **rester au fichier utilisateur.** PUR-03 le nomme explicitement. Scanner tous
  les projets de la machine pour purger serait disproportionné et risqué (écriture dans des dépôts
  git de l'utilisateur). À noter en `Future Requirements` si le cas se présente un jour.

### 7. Aligner `statusLine` sur les slashes avant ? — **RECOMMANDÉ, à confirmer par le planner**

- **Ce qu'on sait :** `HookCommand` (`SessionHookInstaller:31`) convertit en slashes avant, avec la
  mention « leçon vérifiée sur la vraie machine — des backslashes seraient avalés par le shell ».
  `StatusLineInstaller.ChronosCommand:103` **ne le fait pas** : le fichier réel contient
  `"C:\\Users\\Tanguy\\Downloads\\Chronos-v2.8.1.exe" --statusline`.
- **Ce qui n'est pas clair :** le pont statusLine n'est de toute façon jamais invoqué par l'app bureau
  (`usage.json` figé au 2026-07-10) — impossible de dire s'il échouait pour cette raison ou par
  absence d'invocation.
- **Recommandation :** puisque PUR-02 réécrit la commande de toute façon, **aligner sur les slashes
  avant**. Risque quasi nul (les slashes avant fonctionnent aussi bien sous `cmd.exe` que sous les
  shells POSIX), la leçon terrain est documentée, et la sauvegarde couvre le cas. Le planner peut
  choisir la variante conservatrice (conserver le format exact) si l'on veut découpler strictement la
  phase du pipeline d'usage.

## Sources

### Primary (HIGH confidence)

- **Sonde .NET 8 exécutée localement le 2026-09-09** (`scratchpad/jsonprobe`, 22 cas) — ordre des clés,
  round-trip des nombres, échappement de l'encodeur, commentaires, virgules traînantes, clés
  dupliquées, BOM, indentation, reparentage `JsonNode`, `GetValue`/`TryGetValue`, racine non-objet.
  **Tous les comportements System.Text.Json cités dans ce document sont mesurés, pas supposés.**
- **`C:\Users\Tanguy\.claude\settings.json` — fichier réel inspecté** (6 872 o, 2026-07-30) : 25 groupes
  Chronos, 3 groupes GSD (avec `matcher`/`timeout`), `statusLine` périmée, `agentPushNotifEnabled`.
- **Documentation officielle Claude Code — Hooks** (https://code.claude.com/docs/en/hooks) : schéma à
  3 niveaux, `matcher` optionnel (défaut `*`), 5 types de handlers, `timeout` optionnel, et la phrase
  décisive « All matching hooks run in parallel. If you define the same handler in more than one
  settings file, it runs once. »
- **Documentation officielle Claude Code — Status line** (https://code.claude.com/docs/en/statusline) :
  `statusLine` = objet unique `{ type, command, padding? }`, `padding` optionnel (défaut 0).
- **Documentation officielle Claude Code — Settings precedence** (https://code.claude.com/docs/en/settings) :
  5 niveaux de configuration, ordre de précédence.
- **Code source du dépôt** : `SessionHookInstaller.cs`, `StatusLineInstaller.cs`, `SessionsController.cs`,
  `StatusLineSetup.cs`, `App.xaml.cs`, `SettingsService.cs`, `ChronosPaths.cs`,
  `StatusLineInstallerTests.cs`, `SessionsTests.cs`, `ServicesLayerPurityTests.cs`, `CompositionRootTests.cs`.
- **`dotnet test` exécuté le 2026-09-09** : 328 réussis / 0 échec / 38 s. `dotnet --version` → 10.0.201.

### Secondary (MEDIUM confidence)

- Aucun. Toutes les affirmations de ce document sont adossées à une source primaire.

### Tertiary (LOW confidence)

- Aucune affirmation non vérifiée n'a été retenue. La seule zone d'incertitude assumée est
  l'Open Question 7 (backslashes vs slashes avant dans `statusLine`), explicitement signalée.

## Metadata

**Confidence breakdown :**

| Area | Level | Reason |
|------|-------|--------|
| Standard stack | **HIGH** | Aucune dépendance nouvelle ; toutes les briques sont déjà dans le dépôt et exercées par 328 tests verts. |
| Diagnostic de la cause racine | **HIGH** | Lu directement dans le code (`IsOurEntry` ligne 100-106, garde ligne 69) **et** corroboré par la structure exacte du fichier réel (5 chemins × 5 événements). |
| Comportements System.Text.Json | **HIGH** | Mesurés empiriquement sur .NET 8, pas déduits de la mémoire d'entraînement. |
| Schéma Claude Code | **HIGH** | Documentation officielle courante, recoupée avec le fichier réel de la machine. |
| Architecture (réconciliation, prédicat, sauvegarde) | **HIGH** | Contraint par des faits vérifiés ; le seul choix discrétionnaire (emplacement des sauvegardes) est argumenté et réversible. |
| Pitfalls | **HIGH** | 10 pièges, tous adossés soit à une ligne de code du dépôt, soit à un résultat de sonde. |
| Open Question 7 (slashes `statusLine`) | **MEDIUM** | La leçon terrain sur les hooks est documentée dans le code ; son extension à `statusLine` n'a pas pu être vérifiée (le pont n'est jamais invoqué). |

**Research date:** 2026-09-09
**Valid until:** 2026-10-09 (30 jours). Le schéma des hooks de Claude Code évolue vite — les
**événements** et les **types de handlers** ont déjà changé depuis l'écriture des installateurs. Le
choix de conception « ne toucher qu'aux 5 événements de Chronos, ne jamais réécrire ce qu'on ne
comprend pas » rend la phase robuste à ces évolutions.
