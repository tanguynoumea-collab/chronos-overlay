# Chronos — Sources de données

> **Capturé le 2026-07-08** — Claude Code runtime 2.1.202 / binaire disque 2.1.87 /
> doc officielle statusLine courante à cette date.
>
> **⚠️ API privée de facto** : bien que le contrat statusLine soit officiellement
> documenté, le bloc `rate_limits` n'est pas un contrat de données garanti. Le schéma est
> susceptible de changer à toute mise à jour de Claude Code (voir
> [§4 Hypothèses & points de fragilité](#4-hypothèses--points-de-fragilité)).

Ce document caractérise **empiriquement** la méthode d'obtention de l'objet d'usage Claude
Code (fenêtres `five_hour` / `seven_day`). Il est le préalable **STRICT et BLOQUANT** à tout
code de provider (Phase 3) : sans lui, l'abstraction `IUsageProvider` se bâtirait sur des
hypothèses fausses (le champ `utilization` 0..1 attendu par la modélisation projet **n'existe
pas** — c'est `used_percentage` 0..100).

Cette phase est **DOCUMENTAIRE** : le livrable est ce document, PAS du code. Aucun provider,
aucune classe C#, aucun script de pont n'est écrit ici.

---

## 1. Source primaire — objet d'usage (rate_limits via statusLine)

### Localisation exacte

L'objet d'usage est le bloc **`rate_limits`** du contrat JSON de la fonctionnalité
**statusLine** de Claude Code — un point d'extension **officiellement documenté et supporté**
(`code.claude.com/docs/en/statusline`). Ce sont ces champs qui alimentent la commande
`/usage`.

**Point crucial : cet objet n'est persisté dans AUCUN fichier sur disque.** Vérifié
exhaustivement en lecture seule sous `%USERPROFILE%\.claude` et `%USERPROFILE%\.claude.json` :
**0 occurrence structurée** d'un objet `"used_percentage": <nombre>` ou
`"utilization": <nombre>`. Les seules occurrences des chaînes `five_hour` / `seven_day` sur
disque sont (a) le schéma embarqué dans le binaire `claude-2.1.87-win32-x64.exe`, et (b) de la
**prose** dans les transcripts (ce projet Chronos discute littéralement ces noms) — jamais un
objet d'usage réellement loggé.

L'objet ne transite donc que **transitoirement par le `stdin`** de la commande statusLine,
pendant qu'une session Claude Code tourne et rend sa barre de statut.

### Mécanisme d'accès — pont statusLine → fichier

statusLine **ne « rend » pas un fichier** : Claude Code **POUSSE** le JSON de session sur le
`stdin` d'une commande configurée dans `~/.claude/settings.json`
(`statusLine.command`). Il n'existe donc aucun `usage.json` à poller tant qu'aucun mécanisme
ne le persiste.

Pour qu'un overlay externe (Chronos) consomme `rate_limits`, la source primaire n'est pas
« un fichier à surveiller » mais **un pont à mettre en place** :

- une commande statusLine (script, ou un mode CLI de Chronos) lit le JSON sur `stdin`,
- en extrait le bloc `rate_limits`,
- l'écrit **atomiquement** dans un fichier watchable, p. ex. `%APPDATA%\Chronos\usage.json`,
- que l'overlay surveille via `FileSystemWatcher` (aligné RAF-01).

**Contrainte non destructive** : ce poste a déjà une commande statusLine active
(`gsd-statusline.js`). Une seule commande est configurable dans `settings.json` ; le pont doit
donc **RÉ-ÉMETTRE la barre existante sur `stdout`** et n'ajouter QUE l'écriture du fichier
`usage.json` — jamais casser l'affichage en place.

> **`à documenter ici, à CODER en Phase 3 — aucun code de pont n'est écrit dans cette phase`.**
> L'esquisse ci-dessous est une **illustration** de référence pour la Phase 3, **à ne pas
> implémenter en Phase 2** :
>
> ```javascript
> // Source : contrat statusLine officiel (code.claude.com/docs/en/statusline)
> // ILLUSTRATION — À NE PAS IMPLÉMENTER EN PHASE 2
> process.stdin.on('end', () => {
>   const d  = JSON.parse(input);
>   const rl = d.rate_limits;              // peut être absent (non-abonné / avant 1re réponse)
>   if (rl) fs.writeFileSync(usageTmp, JSON.stringify({
>     five_hour: rl.five_hour ?? null,     // { used_percentage, resets_at } | null
>     seven_day: rl.seven_day ?? null,
>     capturedAt: Date.now()
>   }));
>   fs.renameSync(usageTmp, usageFinal);   // écriture atomique
>   process.stdout.write(originalStatusLine); // ne pas casser la barre existante
> });
> ```

### Schéma des champs

Documenté **verbatim** à partir du schéma embarqué dans le binaire `claude-2.1.87` et
**confirmé mot pour mot par la doc officielle** courante :

| Champ | Type | Unité / plage | Remarque |
|-------|------|---------------|----------|
| `rate_limits.five_hour.used_percentage` | nombre | **0 à 100** (décimales possibles) | Pourcentage de la limite 5 h consommé |
| `rate_limits.five_hour.resets_at`       | nombre | **Unix epoch SECONDES** | Instant de reset de la fenêtre 5 h |
| `rate_limits.seven_day.used_percentage` | nombre | **0 à 100** (décimales possibles) | Pourcentage de la limite 7 j consommé |
| `rate_limits.seven_day.resets_at`       | nombre | **Unix epoch SECONDES** | Instant de reset de la fenêtre 7 j |

**⚠️ CORRECTION MAJEURE.** La modélisation projet (PROJECT.md / CLAUDE.md) parle d'un champ
`utilization` normalisé **0..1**. **Ce nom N'EXISTE PAS dans la source.** Le champ réel
s'appelle **`used_percentage`** et vaut **0..100**. La normalisation `Utilization = used_percentage / 100`
doit être faite côté modèle (voir [§3](#3-mapping-vers-usagesnapshot-phase-3)). De même,
`resets_at` est en **epoch secondes** — PAS de l'ISO, PAS des millisecondes.

### Échantillon réel anonymisé

Valeurs synthétiques plausibles (aucune donnée réelle) :

```jsonc
"rate_limits": {
  "five_hour": { "used_percentage": 23.5, "resets_at": 1738425600 },
  "seven_day": { "used_percentage": 41.2, "resets_at": 1738857600 }
}
```

### Conditions de présence

Le bloc `rate_limits` est **optionnel** :

- il n'apparaît **que pour les abonnés Claude.ai (Pro / Max)** ;
- et seulement **APRÈS la 1re réponse API de la session** ;
- chaque fenêtre (`five_hour`, `seven_day`) peut être **indépendamment absente**.

**Conséquence** : le provider doit **dégrader** vers « indisponible » ou basculer sur le
repli JSONL, **jamais inventer de valeur**.

### Fréquence de mise à jour

`rate_limits` est rafraîchi **à chaque rendu de la barre statusLine**, donc **UNIQUEMENT
pendant qu'une session Claude Code est active** (best-effort ; la cadence interne / debounce de
Claude Code n'est pas documentée — **ne pas en dépendre**). Overlay ouvert sans session
active ⇒ dernière valeur figée (voir staleness en [§4](#4-hypothèses--points-de-fragilité)).

### SourceReliability

**`Fiable`** — objet officiellement documenté, noms de champs concordants entre binaire local
2.1.87 et doc courante.

---

## 2. Source de repli — estimation par transcripts JSONL

### Localisation

`~/.claude/projects/<slug-projet>/<session-uuid>.jsonl` — un transcript par session, en
append continu pendant que la session tourne.

### Schéma d'une ligne `assistant`

Chaque ligne est un objet JSON autonome. Clés de haut niveau observées :

`cwd`, `entrypoint`, `gitBranch`, `isSidechain`, `message`, `parentUuid`, `requestId`,
`sessionId`, `timestamp`, `type`, `userType`, `uuid`, `version`.

Filtrer sur `o["type"] == "assistant"` (et `message.role == "assistant"`) pour ne retenir que
les réponses porteuses d'usage.

### Objet `message.usage` (cœur du repli, DAT-05)

Échantillon anonymisé (valeurs synthétiques) :

```jsonc
"usage": {
  "input_tokens": 20863,
  "output_tokens": 1496,
  "cache_creation_input_tokens": 7814,
  "cache_read_input_tokens": 30962,
  "server_tool_use": { "web_search_requests": 0, "web_fetch_requests": 0 },
  "service_tier": "standard",
  "cache_creation": { "ephemeral_1h_input_tokens": 7814, "ephemeral_5m_input_tokens": 0 }
}
```

L'estimation **somme les tokens** sur la fenêtre considérée. Les **plafonds ne sont pas
publiés** (et sont mouvants : ×2 le 6 mai, +50 % hebdo jusqu'au 13 juillet 2026) ⇒ l'estimation
est **structurellement approximative**, d'où le marquage **`Estimé`** (jamais présenter comme
exact).

### Format des timestamps

`o["timestamp"]` = **ISO 8601 UTC** avec millisecondes et suffixe `Z`, p. ex.
`"2026-07-08T12:20:42.428Z"`.

**⚠️ AVERTISSEMENT — deux formats de temps distincts à NE PAS confondre :**

| Source | Champ | Format |
|--------|-------|--------|
| Primaire | `rate_limits.<window>.resets_at` | **Unix epoch SECONDES** |
| Repli    | `timestamp` (ligne JSONL) | **ISO 8601 UTC** (suffixe `Z`, millisecondes) |

### Taille typique & implications performance

~3 Ko / ligne ; à titre d'exemple, **1.1 Mo pour 336 lignes** sur une session en cours. Les
sessions longues produisent des fichiers **plurimégaoctets**. Conséquences pour la Phase 3
(ROB-02) :

- lecture en **streaming** (ne pas charger le fichier entier en mémoire) ;
- ouverture en **`FileShare.ReadWrite`** — le fichier est en cours d'écriture par Claude Code ;
- **tolérance de la dernière ligne partielle** (une ligne peut être en cours d'écriture) :
  ignorer silencieusement une ligne invalide et continuer.

### Blocs sous-agents (note V2-01 — différé, ne pas coder)

En **v2.1.202**, les sous-agents ne sont **PLUS** des blocs `tool_use` `name=Task` inline dans
le transcript principal. Ils vivent dans un sous-dossier dédié :

`~/.claude/projects/<slug>/<session-uuid>/subagents/`, contenant par agent :

- `agent-<id>.jsonl` — transcript du sous-agent (lignes `assistant` avec `usage` tokens,
  `isSidechain: true`) ;
- `agent-<id>.meta.json` — `{ agentType, description, spawnDepth, toolUseId }`.

**À consigner comme piste V2-01, sans coder** : la future bande d'activité des sous-agents lira
ce dossier `subagents/`, et non des blocs `Task` inline.

### SourceReliability

**`Estimé`** — plafonds non publiés ⇒ estimation par sommation de tokens, toujours marquée
comme telle dans l'UI.

---

## 3. Mapping vers UsageSnapshot (Phase 3)

Table de correspondance **source → modèle neutre** (guide direct pour `IUsageProvider`) :

| Source (champ réel) | Modèle `UsageSnapshot` | Conversion |
|---------------------|------------------------|------------|
| `rate_limits.<window>.used_percentage` (0..100) | `Utilization` (0..1) | `used_percentage / 100.0` |
| `rate_limits.<window>.resets_at` (epoch s) | `ResetsAt` (`DateTimeOffset`) | `DateTimeOffset.FromUnixTimeSeconds(resets_at)` |
| fenêtre / bloc absent | `SourceReliability` → repli ou indisponible | **jamais** de valeur inventée |
| repli JSONL `message.usage.*_tokens` (somme) | `Utilization` estimée | `SourceReliability = Estimé` |

> **Rappel : `utilization` (0..1) est un champ FANTÔME côté source.** Il n'existe que côté
> modèle, **après** la conversion `/ 100.0`. Ne jamais parser un champ `utilization` dans la
> source : le champ à lire est `used_percentage`.

`<window>` désigne indifféremment `five_hour` (→ arc extérieur 5 h) ou `seven_day` (→ arc
intérieur hebdo). Le repli hebdo dérive (~72 h, ancrage non documenté) : traiter `resets_at`
tel que fourni, best-effort et recalibrable (voir [§4](#4-hypothèses--points-de-fragilité)).

---

## 4. Hypothèses & points de fragilité

Chaque risque ci-dessous est un **guide direct pour la conception de `IUsageProvider`**
(Phase 3) : l'abstraction doit isoler ces points de rupture du cadran.

- **API privée de facto.** Le contrat statusLine est documenté, mais `rate_limits` n'est PAS
  un contrat de données garanti : un champ peut être renommé ou déplacé à toute mise à jour de
  Claude Code. *Recommandation* : **test de contrat** sur échantillon en Phase 3 ; dégradation
  vers « indisponible » si un champ ou une fenêtre est absent, plutôt que du code défensif
  exotique.

- **Écart de version.** Binaire sur disque **2.1.87** vs runtime actif **2.1.202**
  (`sessions/30656.json`). Le schéma est confirmé **identique** entre le binaire 2.1.87 et la
  doc officielle courante, mais **2.1.202 n'a pas été vérifié champ par champ** (pas de binaire
  2.1.202 sur disque) → confiance **MEDIUM** sur la stabilité inter-versions. *Recommandation* :
  dater la capture (fait dans l'en-tête) et **revalider à chaque MAJ majeure**.

- **Staleness hors session active.** `used_percentage` n'est rafraîchi que quand une session
  Claude tourne et rend sa barre. Overlay ouvert **sans session** ⇒ valeur **figée** au dernier
  connu (à marquer comme **potentiellement périmée**). Le `resets_at` (epoch) permet néanmoins
  d'**interpoler le compte à rebours** localement (aligné RAF-03), sans dépendre d'un
  rafraîchissement.

- **Présence conditionnelle.** Rappel : `rate_limits` n'existe que pour **Pro / Max**, **après
  la 1re réponse API**, et **chaque fenêtre peut être indépendamment absente**. Le provider doit
  gérer l'absence **sans crash** (ROB-01) et **basculer sur le repli JSONL** (DAT-06).

- **Reset hebdo dérivant.** La fenêtre 7 j dérive (~72 h, horaire d'ancrage non documenté) →
  traiter `resets_at` **tel que fourni**, best-effort et **recalibrable** par l'utilisateur
  (ROB-03).

- **Faux positifs JSONL.** Les chaînes « five_hour » / « seven_day » trouvées dans les
  transcripts sont de la **PROSE** (ce projet en discute), **PAS** un objet d'usage loggé.
  Exiger un **objet structuré** (`"used_percentage": <nombre>`), jamais une chaîne dans un champ
  `content` / `text`. Rappel : **aucun objet d'usage n'est matérialisé sur disque**.

- **Sécurité.** Ne **jamais** lire ni logger `.credentials.json` (tokens OAuth), ni le
  **contenu** des conversations. Ne compter que **tokens / métadonnées**. Lecture seule stricte
  sous le profil utilisateur, aucun droit admin.

---

## 5. Reproductibilité — recapture (lecture seule stricte)

Méthode pour **re-vérifier la source** à une future version de Claude Code, **sans écrire de
code de provider** et **sans jamais modifier** un fichier sous `~/.claude` :

1. **Vérifier la config statusLine active** : lire `~/.claude/settings.json`, clé
   `statusLine.command` (constate quelle commande reçoit le JSON stdin).
2. **Confirmer l'absence de persistance** : grep ciblé d'un objet **structuré**
   `"used_percentage":` / `"utilization":` sous `~/.claude` et `~/.claude.json`
   (attendu : **0 occurrence structurée**).
3. **Confirmer le schéma courant** : doc officielle `code.claude.com/docs/en/statusline`
   (table des champs) ; à défaut, extraire les **chaînes printables** du binaire
   `~/.claude/downloads/claude-<ver>-win32-x64.exe` (section « How to use the statusLine
   command »).
4. **Ré-échantillonner le repli** : dernières lignes `type=assistant` d'un
   `~/.claude/projects/<slug>/<uuid>.jsonl` pour `message.usage` + `timestamp` ; lister
   `subagents/` pour le layout sous-agents.
5. **Impératif** : NE MODIFIER aucun fichier de `~/.claude` ; **anonymiser** toute capture avant
   de la coller dans ce document (valeurs synthétiques, placeholders `<slug>` / `<uuid>` /
   `%USERPROFILE%`).

### Traçabilité des sources & niveaux de confiance

| Source consultée | Rôle | Confiance |
|------------------|------|-----------|
| Doc officielle `code.claude.com/docs/en/statusline` | Table des champs `rate_limits` (0-100, epoch s), conditions de présence | Localisation / schéma : **HIGH** |
| Binaire local `claude-2.1.87-win32-x64.exe` (chaînes embarquées) | Schéma statusLine verbatim, confirme les noms de champs | **HIGH** |
| Sondage filesystem `~/.claude` + `~/.claude.json` | Absence prouvée d'objet d'usage persisté | **HIGH** |
| Échantillon réel `~/.claude/projects/<slug>/<uuid>.jsonl` + `subagents/*.meta.json` | Structure `usage`/tokens, timestamps ISO 8601, layout sous-agents v2.1.202 | Structure JSONL : **HIGH** |
| Concordance exacte du schéma en runtime 2.1.202 | Non vérifié champ par champ | Stabilité inter-versions : **MEDIUM** |

---

*Fin du document — capturé le 2026-07-08, à revalider à chaque MAJ majeure de Claude Code
(schéma = API privée de facto).*
