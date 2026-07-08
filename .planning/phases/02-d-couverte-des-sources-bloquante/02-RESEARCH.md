# Phase 2 : Découverte des sources (bloquante) — Research

**Researched:** 2026-07-08
**Domain:** Localisation empirique de l'objet d'usage Claude Code (five_hour/seven_day) sur le poste local Windows + structure des transcripts JSONL (repli)
**Confidence:** HIGH — l'objet d'usage a été localisé et son schéma **confirmé par la doc officielle ET par les chaînes du binaire Claude Code**. Le repli JSONL a été échantillonné réellement.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
**Cibles d'investigation (ordre de priorité)**
- `%USERPROFILE%\.claude\` : inventorier les fichiers (settings, state, caches, *.json) susceptibles de contenir five_hour/seven_day, utilization, resets_at.
- Chercher les chaînes « five_hour », « seven_day », « utilization », « resets_at », « usage » dans les fichiers texte/JSON sous `~/.claude` (hors binaires), y compris `~/.claude.json` s'il existe.
- Transcripts JSONL : `%USERPROFILE%\.claude\projects\**\*.jsonl` — la commande `/usage` étant alimentée par une API, il est possible que des réponses d'usage soient loggées dans les transcripts ou ailleurs.
- Statsig/telemetry caches éventuels sous `~/.claude` (souvent porteurs d'état de compte).
- Si l'objet n'est trouvable dans AUCUN fichier local : documenter les alternatives (invocation CLI `claude /usage` scriptée ? endpoint API OAuth local ?) avec faisabilité, et déclarer le repli JSONL comme source primaire de fait — la Phase 3 s'adapte via CompositeUsageProvider sans changer l'UI.

**Contraintes d'honnêteté**
- Chaque source documentée reçoit un niveau de fiabilité (SourceReliability : Fiable / Estimé).
- Documenter la fréquence de mise à jour observée de chaque source (à quel moment le fichier bouge).
- Documenter le format exact des timestamps (epoch ? ISO ? timezone ?) avec échantillon réel anonymisé.

**Structure JSONL (repli) à documenter précisément**
- Champs de tokens par message (`usage.input_tokens`, `output_tokens`, `cache_creation_input_tokens`, `cache_read_input_tokens`, etc.).
- Identification des blocs Task (tool_use) pour la future bande sous-agents (V2-01).
- Taille typique des fichiers et implications perf (streaming, FileShare.ReadWrite).

### Claude's Discretion
Méthodologie d'investigation libre. Le livrable est de la DOCUMENTATION, pas du code — aucun provider ne doit être codé dans cette phase.

### Deferred Ideas (OUT OF SCOPE)
None — discuss phase skipped.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DAT-01 | La méthode d'obtention de l'objet d'usage Claude Code (five_hour/seven_day : utilization + resets_at) est découverte et documentée dans `docs/data-sources.md` AVANT le code des providers | **Objet localisé et schéma confirmé** : il s'agit du bloc `rate_limits` du contrat JSON **statusLine** de Claude Code (poussé sur stdin). Schéma exact, format des timestamps, conditions de présence, mécanisme d'accès et repli JSONL entièrement caractérisés ci-dessous. Le planner dispose de tout le contenu factuel pour rédiger `docs/data-sources.md`. |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Stack imposée** : C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm) / Microsoft.Extensions.DependencyInjection. (Note : Phase 1 a livré en `net8.0-windows`.)
- **Sources** : primaire = objet d'usage Claude (five_hour/seven_day) ; repli = transcripts JSONL marqués « estimé » ; abstraction `IUsageProvider` interchangeable.
- **Chemins sous profil utilisateur uniquement, aucun droit admin.** → toute l'investigation est en **lecture seule stricte** sous `%USERPROFILE%`.
- `utilization`/`resets_at` prioritaires sur le comptage de tokens ; **ne jamais présenter une estimation comme exacte**.
- Reset hebdo best-effort et recalibrable.
- **UI et commentaires en français** (ce document et `docs/data-sources.md` sont en français).
- Ne rien persister du contenu des conversations (fuite de données) — ne compter que tokens/métadonnées.

---

## Summary

**L'objet d'usage a été localisé, et c'est une excellente nouvelle : ce n'est PAS un format privé non documenté.** Les champs `five_hour`/`seven_day` qui alimentent `/usage` sont exposés par Claude Code via le **contrat JSON de la fonctionnalité `statusLine`** — un point d'extension **officiellement documenté et supporté** (docs.claude.com → code.claude.com/docs/en/statusline). Claude Code **pousse** ce JSON sur le `stdin` d'une commande configurée dans `settings.json` à chaque rendu de la barre de statut. Le bloc pertinent est `rate_limits` :

```jsonc
"rate_limits": {              // Optional: présent seulement pour abonnés Claude.ai (Pro/Max), après la 1re réponse API de la session
  "five_hour": {              // peut être absent indépendamment
    "used_percentage": 23.5,  //  0..100  (PAS 0..1) — nombre, décimales possibles
    "resets_at": 1738425600   //  Unix epoch SECONDES  (PAS ISO, PAS millisecondes)
  },
  "seven_day": {
    "used_percentage": 41.2,
    "resets_at": 1738857600
  }
}
```

**Correction majeure pour le planner** : la modélisation projet parle d'`utilization` (0..1). Le champ RÉEL s'appelle **`used_percentage` (0..100)**. `UsageSnapshot.Utilization` devra donc être calculé par `used_percentage / 100.0`. Le nom `utilization` n'existe nulle part dans la source.

**Conséquence architecturale forte** : l'objet d'usage n'est stocké **dans AUCUN fichier** sur le disque (vérifié exhaustivement — voir Sondage ci-dessous). Il ne transite que **transitoirement par stdin** vers la commande statusLine, uniquement pendant qu'une session Claude Code tourne et rend sa barre. Pour qu'un overlay externe (Chronos) le consomme, la source primaire n'est pas « un fichier à poller » mais **un pont statusLine à mettre en place** : une petite commande statusLine (Chronos, ou un script) qui **persiste `rate_limits` dans un fichier** que l'overlay lit/surveille. Ce pont est trivial, documenté, et déjà exercé sur ce poste (une commande statusLine `gsd-statusline.js` est active et lit déjà ce JSON stdin).

Le **repli JSONL** (DAT-05) a été échantillonné réellement : structure des lignes `assistant` avec objet `usage` (tokens), timestamps **ISO 8601 UTC**, sous-agents désormais (v2.1.202) dans un sous-dossier `subagents/` dédié.

**Primary recommendation:** Documenter dans `docs/data-sources.md` la source primaire comme le bloc `rate_limits` du contrat **statusLine** (Fiable), consommé via un **pont statusLine → fichier JSON** que Chronos lit ; documenter le repli JSONL (Estimé) par sommation de tokens ; acter la correction `used_percentage` (0..100, epoch secondes) et la staleness inhérente hors session active.

---

## Sondage empirique réalisé (lecture seule stricte — aucun fichier modifié)

### 1. Inventaire `%USERPROFILE%\.claude\` (premier niveau)

| Élément | Type | Dernière modif | Pertinence usage |
|---------|------|----------------|------------------|
| `.credentials.json` | fichier (5.6 Ko) | 2026-05-25 | tokens OAuth (NE PAS lire/logger) — pas d'objet d'usage |
| `settings.json` | fichier (924 o) | — | **contient la config `statusLine`** (voir §3) |
| `.claude.json` (dans `%USERPROFILE%`, hors `.claude\`) | fichier (48 Ko) | bouge en continu | état global ; **aucun** five_hour/seven_day/utilization/resets_at |
| `projects/` | dossier | actif | **transcripts JSONL** (repli) — voir §4 |
| `sessions/`, `session-env/`, `sessions/30656.json` | dossiers/fichier | actifs | métadonnées de session (pid, sessionId, version) — pas d'objet d'usage |
| `cache/` (`gsd-update-check.json`) | dossier | — | pas d'objet d'usage |
| `statsig/` | **absent** | — | pas de dossier statsig sur ce poste |
| `downloads/claude-2.1.87-win32-x64.exe` | binaire (227 Mo) | 2026-03-29 | **contient le schéma documenté du JSON statusLine** (voir §3) |
| `backups/`, `hooks/`, `agents/`, `commands/`, `skills/`, `plugins/`, `tasks/`, `scheduled-tasks/`, `shell-snapshots/`, `get-shit-done/` | dossiers | divers | infra outillage — pas d'objet d'usage |

### 2. Grep des chaînes cibles (`five_hour`, `seven_day`, `utilization`, `resets_at`, `used_percentage`)

- **`~/.claude.json`** : **0 occurrence** de five_hour/seven_day/utilization/resets_at/usage.
- **Fichiers `*.json` de config/état** (hors `projects/`) : **0 occurrence**.
- **Motif d'objet structuré** `"utilization": <nombre>` : **0 occurrence** sur tout `~/.claude` → **aucun objet d'usage matérialisé sur disque**.
- **`"five_hour":{` et `"resets_at":"…"` structurés** : trouvés **uniquement dans le binaire** `downloads/claude-2.1.87-win32-x64.exe` (il s'agit du schéma/commentaires embarqués, pas de données).
- **Transcripts JSONL** : occurrences présentes, mais **ce sont du texte de conversation** (ce projet Chronos discute littéralement des chaînes « five_hour/seven_day ») — **faux positifs**, PAS un objet d'usage loggé. Vérifié par extraction du contexte (ex. « objet d'usage de Claude Code : champs `five_hour.utilization`… » = prose du prompt).

> **Conclusion §2** : l'objet d'usage n'est jamais écrit dans un fichier lisible. Il vit (a) dans le code du binaire, et (b) transitoirement sur le stdin de la commande statusLine.

### 3. Candidat gagnant : le contrat JSON `statusLine`

**Config active constatée** (`~/.claude/settings.json`) :
```jsonc
"statusLine": { "type": "command", "command": "node \"C:/Users/Tanguy/.claude/hooks/gsd-statusline.js\"" }
```
→ Claude Code invoke déjà une commande node en lui passant le JSON de session sur stdin. Le script actuel ne lit que `context_window.remaining_percentage` (il n'exploite pas encore `rate_limits`), mais **le mécanisme est actif et extensible trivialement**.

**Schéma extrait du binaire** (chaînes embarquées, section « How to use the statusLine command »), reproduit **verbatim** pour les champs utiles :
```jsonc
{
  "session_id": "string",
  "transcript_path": "string",        // chemin du transcript JSONL de la session
  "cwd": "string",
  "model": { "id": "string", "display_name": "string" },
  "workspace": { "current_dir": "string", "project_dir": "string", "added_dirs": ["string"] },
  "version": "string",
  "context_window": {
    "total_input_tokens": number, "total_output_tokens": number,
    "context_window_size": number,
    "current_usage": { "input_tokens": number, "output_tokens": number,
                       "cache_creation_input_tokens": number, "cache_read_input_tokens": number } | null,
    "used_percentage": number | null, "remaining_percentage": number | null
  },
  "rate_limits": {             // Optional: Claude.ai subscription usage limits. Only present for subscribers after first API response.
    "five_hour": {             // Optional: 5-hour session limit (may be absent)
      "used_percentage": number,   // Percentage of limit used (0-100)
      "resets_at": number          // Unix epoch seconds when this window resets
    },
    "seven_day": {             // Optional: 7-day weekly limit (may be absent)
      "used_percentage": number,
      "resets_at": number
    }
  }
}
```

**Confirmé par la doc officielle** (`code.claude.com/docs/en/statusline`, HIGH) — extraits :
- Table des champs : `rate_limits.five_hour.used_percentage` / `.seven_day.used_percentage` = « Percentage of the 5-hour or 7-day rate limit consumed, **from 0 to 100** » ; `.resets_at` = « **Unix epoch seconds** when the … rate limit window resets ».
- Exemple JSON officiel : `"five_hour": { "used_percentage": 23.5, "resets_at": 1738425600 }` → **décimales possibles**, epoch secondes.
- Condition de présence : « `rate_limits` **appears only for Claude.ai subscribers (Pro/Max) after the first API response in the session**. Each window (`five_hour`, `seven_day`) may be independently absent. »

> **Écart de version à noter** : le binaire sur disque est **2.1.87** ; la session active tourne en **2.1.202** (`sessions/30656.json`). La doc officielle (courante) et le binaire 2.1.87 concordent sur les noms de champs → confiance HIGH que 2.1.202 est identique, mais le document devra dater la capture et prévoir un test de contrat (rupture possible à une MAJ future).

### 4. Échantillon réel du repli JSONL (anonymisé)

Fichier examiné : `~/.claude/projects/<slug-projet>/<session-uuid>.jsonl` — **1.1 Mo, 336 lignes** pour la session en cours (ordre de grandeur : ~3 Ko/ligne, fichiers plurimégaoctets sur longues sessions).

**Ligne `assistant` (clés de haut niveau)** :
`cwd, entrypoint, gitBranch, isSidechain, message, parentUuid, requestId, sessionId, timestamp, type, userType, uuid, version`

**`message.usage`** (objet de tokens — cœur du repli DAT-05), échantillon anonymisé :
```jsonc
{
  "input_tokens": 20863,
  "output_tokens": 1496,
  "cache_creation_input_tokens": 7814,
  "cache_read_input_tokens": 30962,
  "server_tool_use": { "web_search_requests": 0, "web_fetch_requests": 0 },
  "service_tier": "standard",
  "cache_creation": { "ephemeral_1h_input_tokens": 7814, "ephemeral_5m_input_tokens": 0 }
  // + "iterations": [...], "speed", "inference_geo"
}
```
- `message.model` : ex. `claude-opus-4-8`.
- `o["type"]` = `"assistant"` (filtrer là-dessus), `message.role` = `"assistant"`.
- **`o["timestamp"]`** : **ISO 8601 UTC** avec millisecondes et suffixe `Z`, ex. `"2026-07-08T12:20:42.428Z"`.

> **Deux formats de temps distincts à documenter** : (a) source primaire `rate_limits.*.resets_at` = **Unix epoch secondes** ; (b) repli JSONL `timestamp` = **ISO 8601 UTC (Z)**. Ne pas les confondre dans le parsing.

**Blocs Task / sous-agents (pour V2-01, différé)** : en **v2.1.202**, les sous-agents ne sont **plus des blocs `tool_use` name=Task inline** dans le transcript principal. Ils ont leur **propre sous-dossier** :
`~/.claude/projects/<slug>/<session-uuid>/subagents/` contenant, par agent :
- `agent-<id>.jsonl` — transcript du sous-agent (lignes `assistant` avec `usage` tokens, `isSidechain: true`).
- `agent-<id>.meta.json` — métadonnées : `{ agentType, description, spawnDepth, toolUseId }` (ex. `agentType: "gsd-phase-researcher"`, `spawnDepth: 1`).
26 fichiers observés (13 agents × jsonl+meta) pour la session courante. **À consigner comme piste V2-01, sans coder** : la bande sous-agents lira ce dossier, pas des blocs Task inline.

### 5. Alternatives évaluées (au cas où statusLine indisponible)

| Alternative | Faisabilité | Verdict |
|-------------|-------------|---------|
| Fichier de cache d'usage sous `~/.claude` | **Inexistant** (vérifié §2) | Écarté — rien n'est persisté |
| `~/.claude.json` / statsig | Aucun champ d'usage ; pas de statsig | Écarté |
| Invoquer `claude` CLI en mode « /usage » scripté | Non exploré en exécution (hors scope lecture seule) ; coûteux, lance une session | Non retenu vs statusLine, plus simple et documenté |
| Endpoint API OAuth local | Non documenté ; nécessiterait le token de `.credentials.json` (à ne pas manipuler) | Écarté (fragile, sensible) |
| **Pont statusLine → fichier** | **Documenté, déjà actif sur le poste** | **Retenu comme source primaire** |
| Repli **JSONL** (sommation tokens) | Échantillonné, faisable | **Repli (Estimé)**, conforme DAT-05 |

---

## Standard Stack

> Phase de **documentation** : pas de dépendance runtime à installer. « Stack » = mécanismes/outils de la source. Le code des providers est Phase 3.

### Mécanismes de source (à documenter, pas à coder ici)
| Mécanisme | Rôle | Fiabilité | Pourquoi |
|-----------|------|-----------|----------|
| Contrat JSON `statusLine` (`rate_limits`) | Source primaire de l'objet d'usage | **Fiable** | Officiellement documenté et supporté ; noms de champs stables entre binaire local et doc courante |
| Pont statusLine → fichier JSON (ex. `%APPDATA%\Chronos\usage.json`) | Rendre `rate_limits` lisible par l'overlay | Fiable | statusLine ne « rend » pas un fichier ; il faut persister le stdin. Fichier watchable (cohérent RAF-01) |
| Transcripts JSONL `~/.claude/projects/**/*.jsonl` | Repli d'estimation par tokens | **Estimé** | Plafonds non publiés → structurellement approximatif (voir PITFALLS #7) |

### Outils d'investigation utilisés (traçabilité, reproductibles)
- `grep -rlI` ciblé (jamais `cat` intégral des gros fichiers), extraction de contexte tronqué.
- `python -c` pour parser/anonymiser lignes JSONL et extraire les chaînes printables du binaire.
- `WebFetch` doc officielle `code.claude.com/docs/en/statusline`.

**Version verification (source, pas package) :** binaire local `claude-2.1.87` ; runtime actif `2.1.202` (`sessions/30656.json`) ; schéma confirmé courant par la doc officielle au 2026-07-08.

---

## Architecture Patterns — Structure imposée de `docs/data-sources.md`

> Le livrable EST ce document. Voici la structure de sections que le planner doit faire produire (chaque section adossée aux findings ci-dessus), pour satisfaire les 3 Success Criteria.

```
docs/data-sources.md
├── 1. Source primaire — objet d'usage (rate_limits via statusLine)
│     ├── Localisation exacte : contrat JSON statusLine, clé `rate_limits`
│     ├── Mécanisme d'accès : pont statusLine → fichier (settings.json → command → écrit usage.json)
│     ├── Schéma des champs (five_hour/seven_day : used_percentage 0..100, resets_at epoch s)
│     ├── Échantillon réel anonymisé (bloc rate_limits + valeurs plausibles)
│     ├── Conditions de présence (Pro/Max, après 1re réponse API ; fenêtres absentes possibles)
│     ├── Fréquence de mise à jour (à chaque rendu statusLine, seulement session active)
│     └── SourceReliability = Fiable
├── 2. Source de repli — estimation JSONL
│     ├── Localisation : ~/.claude/projects/<slug>/<uuid>.jsonl (+ subagents/)
│     ├── Schéma d'une ligne assistant + objet usage (tokens)
│     ├── Format timestamp : ISO 8601 UTC (Z)
│     ├── Taille typique + implications perf (streaming, FileShare.ReadWrite)
│     ├── Blocs sous-agents (subagents/ dir, meta.json) → note V2-01
│     └── SourceReliability = Estimé
├── 3. Mapping vers UsageSnapshot (Phase 3)
│     └── used_percentage/100 → Utilization ; resets_at (epoch s → DateTimeOffset) → ResetsAt
├── 4. Hypothèses & points de fragilité
│     ├── Écart de version (2.1.87 disque vs 2.1.202 runtime) ; API privée de facto
│     ├── Staleness hors session active ; reset hebdo dérivant (~72 h)
│     └── Renommage/déplacement de champ possible à une MAJ → test de contrat
└── 5. Reproductibilité / recapture (commandes d'investigation lecture seule)
```

### Pattern 1 : Pont statusLine → fichier (source primaire consommable)
**What:** une commande statusLine (script, ou Chronos lui-même via un mode CLI) lit le JSON stdin, en extrait `rate_limits`, et l'écrit atomiquement dans `%APPDATA%\Chronos\usage.json`. L'overlay lit/surveille ce fichier (aligné RAF-01 FileSystemWatcher).
**When to use:** source primaire, dès Phase 3. **À documenter en Phase 2, PAS à coder.**
**Exemple (esquisse, référence pour Phase 3) :**
```javascript
// Source: contrat statusLine officiel (code.claude.com/docs/en/statusline)
// À NE PAS implémenter en Phase 2 — illustration du pont pour docs/data-sources.md
process.stdin.on('end', () => {
  const d = JSON.parse(input);
  const rl = d.rate_limits;            // peut être absent (non-abonné / avant 1re réponse)
  if (rl) fs.writeFileSync(usageTmp, JSON.stringify({
    five_hour: rl.five_hour ?? null,   // {used_percentage, resets_at} | null
    seven_day: rl.seven_day ?? null,
    capturedAt: Date.now()
  }));
  fs.renameSync(usageTmp, usageFinal); // écriture atomique
  process.stdout.write(originalStatusLine); // ne pas casser la barre existante
});
```

### Anti-Patterns à éviter
- **Coder un provider en Phase 2** : interdit — livrable = doc seule.
- **Modéliser `utilization` comme si le champ existait** : le champ est `used_percentage` (0..100). Documenter la conversion, pas un champ fantôme.
- **Traiter `resets_at` comme de l'ISO ou des millisecondes** : c'est **epoch secondes**.
- **Supposer `rate_limits` toujours présent** : absent hors Pro/Max et avant la 1re réponse API ; chaque fenêtre indépendamment absente → dégrader vers « indisponible », jamais inventer.
- **Lire `.credentials.json`** ou logger le contenu des transcripts : sécurité (PITFALLS Security).

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser à la place | Pourquoi |
|----------|-------------------|---------------------|----------|
| Obtenir l'objet d'usage | Un reverse-engineering de l'API `/usage` ou lecture du token OAuth pour appeler un endpoint | Le contrat **statusLine** officiel (`rate_limits`) | Documenté, supporté, sans manipulation de secrets |
| Rendre l'usage lisible par l'overlay | Un IPC maison / hook non standard | Pont statusLine → fichier JSON + FileSystemWatcher | Aligné sur l'architecture RAF déjà prévue |
| Parser les timestamps | Un parseur maison ambigu | `DateTimeOffset.FromUnixTimeSeconds` (primaire) / `DateTimeOffset.Parse` ISO (repli) | Deux formats distincts déjà identifiés |

**Key insight:** la « source non documentée » redoutée par PITFALLS #1 se révèle **documentée** (statusLine). Le risque résiduel n'est pas la localisation mais la **stabilité inter-versions** → répondre par test de contrat sur échantillon, pas par du code défensif exotique.

## Common Pitfalls (spécifiques à cette phase de découverte)

### Pitfall 1 : Confondre les faux positifs JSONL avec un objet d'usage loggé
**What goes wrong:** grep trouve « five_hour » dans les transcripts → on croit l'usage persité. **Why:** ce projet discute littéralement ces chaînes ; c'est de la prose. **How to avoid:** exiger un **objet structuré** (`"used_percentage": <nombre>`), pas une chaîne dans du texte. Vérifié : `"utilization"/"used_percentage": <nombre>` = 0 occurrence sur disque. **Warning sign:** la « donnée » trouvée est dans un champ `content`/`text`.

### Pitfall 2 : Prendre le nom modélisé (`utilization` 0..1) pour le nom réel
**What goes wrong:** on documente/parse un champ `utilization` inexistant. **How to avoid:** champ réel = `used_percentage` (0..100). Documenter explicitement la conversion `/100`.

### Pitfall 3 : Croire que statusLine « expose un fichier »
**What goes wrong:** on cherche un fichier de sortie statusLine à poller ; il n'existe pas — statusLine **pousse sur stdin** et n'écrit rien. **How to avoid:** documenter le **pont** (le script doit persister lui-même). **Warning sign:** aucun `usage.json` n'apparaît tant qu'aucun pont n'est en place.

### Pitfall 4 : Ignorer la staleness hors session active
**What goes wrong:** l'usage n'est rafraîchi que quand une session Claude tourne et rend sa barre ; overlay ouvert sans session → chiffres figés. **How to avoid:** documenter cette limite ; `resets_at` (epoch) permet quand même d'interpoler le compte à rebours ; `used_percentage` reste au dernier connu (à marquer comme potentiellement périmé).

### Pitfall 5 : Échantillon non anonymisé dans `docs/data-sources.md`
**What goes wrong:** capture d'un vrai bloc → fuite d'IDs de compte/valeurs sensibles. **How to avoid:** valeurs synthétiques plausibles (ex. `used_percentage: 23.5`, `resets_at: 1738425600`), pas de contenu de conversation, pas de token, pas d'UUID réel.

## Code Examples (références vérifiées — pour la doc, pas à implémenter en Phase 2)

### Extraction robuste de `rate_limits` (gestion des absences)
```jsonc
// Source: doc officielle statusLine (code.claude.com/docs/en/statusline)
// rate_limits absent si non Pro/Max ou avant 1re réponse API ; chaque fenêtre indépendamment absente
{
  "rate_limits": {
    "five_hour": { "used_percentage": 23.5, "resets_at": 1738425600 },
    "seven_day": { "used_percentage": 41.2, "resets_at": 1738857600 }
  }
}
```

### Lecture tolérante d'une ligne JSONL de repli (esquisse Phase 3)
```csharp
// Source: échantillon réel anonymisé (~/.claude/projects/.../<uuid>.jsonl)
// FileShare.ReadWrite + try/catch par ligne + ignorer dernière ligne partielle (PITFALLS #5)
using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
using var sr = new StreamReader(fs, Encoding.UTF8);
string? line;
while ((line = sr.ReadLine()) != null) {
    try {
        var o = JsonNode.Parse(line);
        if ((string?)o?["type"] == "assistant") {
            var u = o["message"]?["usage"];
            // u["input_tokens"], u["output_tokens"],
            // u["cache_creation_input_tokens"], u["cache_read_input_tokens"]
        }
    } catch { /* ligne invalide/partielle → ignorer, continuer */ }
}
```

## State of the Art

| Ancienne hypothèse | Réalité constatée | Impact |
|--------------------|-------------------|--------|
| Source d'usage = format privé non documenté (PITFALLS #1, STATE blocker) | Contrat **statusLine documenté** (`rate_limits`) | Risque fortement abaissé ; primaire = « Fiable » légitime |
| Champ `utilization` (0..1) | Champ `used_percentage` (0..100) | Conversion `/100` dans le mapping UsageSnapshot |
| `resets_at` format indéterminé | **Unix epoch secondes** | `DateTimeOffset.FromUnixTimeSeconds` |
| Sous-agents = blocs Task inline (V2-01) | Sous-dossier `subagents/` (jsonl+meta) en v2.1.202 | V2-01 lira ce dossier, pas des blocs Task |

**Déprécié/inexact :** l'idée que `/usage` écrit un cache lisible sur disque — faux, rien n'est persisté.

## Open Questions

1. **Stabilité inter-versions du schéma `rate_limits`**
   - Ce qu'on sait : identique entre binaire 2.1.87 et doc officielle courante.
   - Ce qui est flou : le runtime 2.1.202 n'a pas été vérifié champ par champ (pas de binaire 2.1.202 sur disque).
   - Recommandation : dater la capture dans `docs/data-sources.md` ; prévoir un **test de contrat** sur échantillon en Phase 3 ; le pont doit dégrader vers « indisponible » si `rate_limits`/fenêtre absent.

2. **Comportement du pont si l'utilisateur a déjà une commande statusLine (cas de ce poste : `gsd-statusline.js`)**
   - Ce qu'on sait : une seule commande statusLine est configurable dans `settings.json`.
   - Ce qui est flou : Chronos doit-il fournir sa propre commande (et préserver l'affichage existant) ou documenter une composition ?
   - Recommandation : documenter que le pont doit **ré-émettre la barre existante sur stdout** et n'ajouter que l'écriture `usage.json` (non destructif). Décision d'implémentation → Phase 3.

3. **Fréquence exacte de rendu statusLine**
   - Ce qu'on sait : à chaque rafraîchissement de la barre (interactif).
   - Ce qui est flou : cadence précise / debounce interne de Claude Code.
   - Recommandation : ne pas en dépendre ; le PeriodicTimer (RAF-02) reste le filet ; consigner « best-effort, session active requise ».

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Claude Code installé + utilisé | Découverte empirique de la source | ✓ | runtime 2.1.202 / binaire disque 2.1.87 | — |
| Session/transcripts réels sous `~/.claude/projects` | Échantillon JSONL (repli) | ✓ | — | — |
| Commande statusLine active | Preuve du mécanisme primaire | ✓ (`gsd-statusline.js`) | — | — |
| Abonnement Claude.ai Pro/Max | Présence de `rate_limits` | ✓ (bloc présent/attendu ; champ « subscribers only ») | — | Repli JSONL si absent |
| Python (parsing/anonymisation investigation) | Reproductibilité du sondage | ✓ | — | jq/PowerShell |
| Accès doc officielle statusLine | Confirmation schéma | ✓ | courante 2026-07-08 | Chaînes du binaire |

**Missing dependencies with no fallback:** aucune.
**Missing dependencies with fallback:** si un compte n'est pas Pro/Max (ou avant 1re réponse API), `rate_limits` est absent → **repli JSONL estimé** (déjà prévu par DAT-05/DAT-06).

## Validation Architecture

> `nyquist_validation: true` → section incluse. Le livrable est un **document** ; la « validation » vérifie sa complétude et l'exactitude vérifiable des échantillons, pas du code.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Aucun test automatisé de code en Phase 2 (livrable = documentation). Revue par checklist + re-grep reproductible. |
| Config file | none — voir Wave 0 (checklist doc) |
| Quick run command | `grep -c '^## ' docs/data-sources.md` (présence des sections) |
| Full suite command | Revue manuelle de la checklist ci-dessous + re-capture des échantillons via commandes du §5 |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DAT-01 | `docs/data-sources.md` existe | smoke | `test -f docs/data-sources.md` | ❌ Wave 0 (créé par la phase) |
| DAT-01 | Localise l'objet d'usage (rate_limits/statusLine) | manuel | `grep -Eiq 'rate_limits|statusLine' docs/data-sources.md` | ❌ Wave 0 |
| DAT-01 | Documente le schéma five_hour/seven_day + used_percentage + resets_at | manuel | `grep -Eiq 'used_percentage' docs/data-sources.md && grep -Eiq 'resets_at' docs/data-sources.md && grep -Eiq 'epoch' docs/data-sources.md` | ❌ Wave 0 |
| DAT-01 | Échantillon réel anonymisé présent | manuel | `grep -Eiq 'five_hour' docs/data-sources.md` + revue visuelle (aucun token/UUID réel) | ❌ Wave 0 |
| DAT-01 | Structure JSONL (repli) documentée | manuel | `grep -Eiq 'input_tokens' docs/data-sources.md && grep -Eiq 'jsonl' docs/data-sources.md` | ❌ Wave 0 |
| DAT-01 | Format timestamps (epoch s vs ISO Z) documenté | manuel | `grep -Eiq 'ISO 8601' docs/data-sources.md` | ❌ Wave 0 |
| DAT-01 | Hypothèses/fragilités consignées (versions, staleness) | manuel | `grep -Eiq 'fragilit|version|staleness|hypoth' docs/data-sources.md` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** vérifier que la section rédigée référence un finding réel (pas d'invention).
- **Per wave merge:** checklist de complétude ci-dessous entièrement cochée.
- **Phase gate:** les 7 lignes du map DAT-01 passent + revue d'anonymisation OK avant `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `docs/data-sources.md` — n'existe pas encore ; créé par cette phase (couvre DAT-01).
- [ ] Aucune infra de test à installer (phase documentaire).
- [ ] Checklist de complétude à intégrer au plan : (1) source primaire localisée+schéma, (2) mécanisme d'accès (pont statusLine), (3) échantillon anonymisé, (4) conditions de présence, (5) structure JSONL+tokens, (6) formats de temps, (7) hypothèses/fragilités, (8) reproductibilité.

## Sources

### Primary (HIGH confidence)
- **Doc officielle** `https://code.claude.com/docs/en/statusline` — table des champs `rate_limits.*.used_percentage` (0-100) et `.resets_at` (Unix epoch seconds), exemple JSON, condition « subscribers (Pro/Max) after first API response », absence indépendante des fenêtres.
- **Binaire local** `~/.claude/downloads/claude-2.1.87-win32-x64.exe` — schéma statusLine embarqué (verbatim), confirme les noms de champs.
- **Sondage filesystem** `~/.claude` + `~/.claude.json` — inventaire, grep ciblés, absence d'objet d'usage persisté (vérifié).
- **Échantillon réel** `~/.claude/projects/<slug>/<uuid>.jsonl` + `subagents/*.meta.json` — structure usage/tokens, timestamps ISO 8601 UTC, layout sous-agents v2.1.202.
- `~/.claude/settings.json` — statusLine actif (`gsd-statusline.js`).
- `~/.claude/sessions/30656.json` — version runtime active 2.1.202.

### Secondary (MEDIUM confidence)
- `.planning/research/PITFALLS.md` — fragilité des sources, JSONL en écriture, honnêteté des chiffres (cadre le « pourquoi »).

### Tertiary (LOW confidence)
- Concordance exacte du schéma en runtime 2.1.202 (non vérifié champ par champ ; inféré HIGH via doc courante + binaire 2.1.87).

## Metadata

**Confidence breakdown:**
- Localisation source primaire : **HIGH** — confirmée par doc officielle + binaire + absence prouvée sur disque.
- Schéma des champs (noms/types/unités) : **HIGH** — verbatim doc + binaire concordants.
- Stabilité inter-versions : **MEDIUM** — écart 2.1.87/2.1.202, API privée de facto.
- Structure JSONL (repli) : **HIGH** — échantillonnée réellement sur ce poste.
- Mécanisme de consommation (pont statusLine → fichier) : **HIGH** sur le principe (documenté, actif) ; détails d'implémentation = Phase 3.

**Research date:** 2026-07-08
**Valid until:** ~2026-08-07 (30 j) pour la structure ; **à revalider à chaque MAJ majeure de Claude Code** (schéma = API privée de facto).
