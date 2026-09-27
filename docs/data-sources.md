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

> **2026-09-27 (phase 32, CPT-01)** : une ligne `assistant` par bloc de contenu d'un même `message.id`,
> `output_tokens` partiel et croissant (8 → 8 → 256), les trois autres champs identiques ; 491 ids sur
> 8 jours présents dans 2 à 3 fichiers (fork/resume). Toute somme passe par `DedupUsage` (max par champ
> par `message.id`, repli `requestId`, dictionnaire global à la passe) : sommer les lignes compterait
> l'entrée ×2,1.

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

**Lu par le widget de sessions depuis la phase 30.1 (SUB-01)** — seulement l'instant du dernier message
de chaque `agent-<id>.jsonl`, comme signal de TRAVAIL de sa session, jamais comme ligne ni comme compte de
tokens : voir `docs/hooks-contract.md` §3 et §4. La bande d'activité V2-01 reste différée.

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

## 6. Les sources du widget de sessions

> Ajouté le 2026-09-26 (phase 31). Ce document reste celui des sources d'USAGE du cadran ; le widget de sessions a
> les siennes, chacune décrite par son contrat.

| Source | Ce qu'elle dit | Contrat |
|---|---|---|
| Hooks Claude Code (mode `--hook` de Chronos) | l'activité observée de chaque session : Réflexion, En attente | `docs/hooks-contract.md` |
| Métadonnées de session de l'app bureau Claude | le titre, le dernier focus (`lastFocusedAt`), la classification de fin de tour | `docs/desktop-app-sessions.md` |
| Transcripts JSONL (même emplacement qu'au §2) | la date du dernier message et l'état d'un tour sans fichier de hook | `docs/hooks-contract.md` §3 |
| Transcripts des sous-agents (`subagents/agent-*.jsonl`, même emplacement) | la date du dernier message d'un sous-agent : un signal de TRAVAIL de sa session (SUB-01), jamais une ligne | `docs/hooks-contract.md` §3 et §4 |

**Deux vues d'AppData.** Les hooks lancés sous l'app bureau écrivent dans le cache du paquet MSIX
(`%LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\…`), que l'overlay lit par candidats ; un relevé fait depuis une
session Claude Code voit la vue virtualisée et ne vaut pas pour l'overlay (`docs/desktop-app-sessions.md`, §7).

---

## 7. Journal d'historique

> Ajouté le 2026-09-27 (phase 32, exe 3.2.2). Le journal ne sonde rien et n'appelle rien : il observe ce que la chaîne
> exacte produit (décorateur `JournalisationUsageProvider`, entre `LastExactUsageProvider` et le composite) et l'écrit.
> Lecture seule stricte de `~/.claude` et de `%APPDATA%\Claude`, inchangée. Types : `Services/Historique/{JournalReleves,
> JournalisationUsageProvider, IEtatJournal, LigneJournal, LecteurJournal, BornesPlage, AnalyseReleves}`,
> `Models/Historique/{ReleveJournal, EvenementJournal, LectureJournal}`.

### Emplacement et fichiers

`%APPDATA%\Chronos\historique\releves-AAAA-MM.jsonl` (chemin par `ChronosPaths.HistoriqueDir`) — un fichier par **mois
UTC** de `t` (`JournalReleves.NomFichier`, culture invariante : un relevé du 1er octobre à 01:30+02:00 tombe dans
`releves-2026-09`) ; UTF-8 sans BOM, une ligne JSON par entrée, terminée par `\n` seul, < 4 Ko (216 o mesurés pour un
relevé complet sans overage, 270 o avec ; ≈ 75 Ko/jour, ≈ 2,3 Mo/mois). **Rétention 24 mois** (`JournalReleves.RetentionMois`) :
au démarrage, les fichiers dont AAAA-MM est strictement antérieur à (mois courant − 24) sont supprimés ; les noms non
conformes sont ignorés et comptés ; **aucune compaction** (JRN-07 différé).

### Ligne de relevé (un relevé exact d'UNE source à UN instant)

| champ | type / unité | d'où il vient |
|---|---|---|
| `v` | entier, version du schéma de ligne (1 ; une autre valeur = ligne sautée, pas le fichier) | `LigneJournal.SchemaVersion` |
| `t` | ISO 8601 UTC, format aller-retour « O » ; `CapturedAt` de la fenêtre | sonde : `now` à la réponse |
| `u5`, `u7` | fraction 0..1 **brute, sans arrondi** (utilisation 5 h / 7 j) ; `double` invariant (« 0.12 », jamais « 0,12 ») | en-têtes `anthropic-ratelimit-unified-*` |
| `r5`, `r7` | ISO 8601 UTC, `resets_at` annoncé par le serveur | idem |
| `statut5`, `statut7` | `Autorise` · `AutoriseAvertissement` · `Rejete` · `NonReconnu`, ou absent | statut serveur de la fenêtre |
| `overage` | fraction du dépassement (HDR-04), ou absent | en-têtes d'overage |
| `overage_statut` | statut du dépassement (mêmes valeurs que `statut5`) ; peut exister SEUL, ou absent | idem |
| `source` | `SondeEnTetes` · `EndpointOAuthChronos` · `EndpointOAuthClaude` · `PontStatusLine` | producteur ; **jamais** `MagasinDernierExact` |

Les `null` sont omis à l'écriture ; un champ absent est `null` à la lecture, **jamais 0**. Un snapshot mixte (5 h de la
sonde, hebdo d'un repli) fait UNE ligne par couple `(t, source)`, les fenêtres de l'autre couple restant absentes.
N'entrent JAMAIS dans le journal : un plancher (`Estimated`), une fenêtre indisponible, une valeur rejouée du magasin
`last-exact.json` (le décorateur voit l'inner BRUT), et tout relevé dont `t` n'est pas **strictement croissant** pour sa
source — l'orchestrateur ressert le même relevé 5 fois sur 6 (sonde toutes les 300 s, tick toutes les 60 s) et le journal
n'en garde qu'un.

### Ligne d'événement (la cause d'un trou)

`{"v":1,"t":"…","ev":"…"}` plus, selon le cas, `magasin`, `cause`, `version`. `ev` prend l'une des six valeurs de
`TypeEvenementTexte.NomsDeFil` :

| `ev` | quand | porte |
|---|---|---|
| `demarrage` | le décorateur démarre (date un changement de comportement de l'exe) | `version` (version embarquée, ex. `3.2.2`) |
| `arret` | arrêt PROPRE (`StopAsync`) ; un kill ou une veille n'en écrit pas — le lecteur fait alors un trou « Chronos arrêté » | — |
| `jeton_invalide` | transition d'authentification vers Déconnecté | — |
| `sonde_refusee` | la sonde d'en-têtes est refusée (429, saturation, statut `Rejete`), sur transition seulement | `cause` |
| `reprise` | le relevé qui suit un trou > `SeuilReprise` = 2 × 300 s = 10 min ; `t` est celui du relevé | `cause` (« trou de N min ») |
| `ecriture_ratee` | un magasin persistant n'a pas pu écrire (`last-exact`, `journal`…) : une écriture qui échoue n'est plus silencieuse | `magasin`, `cause` |

Un `ev` inconnu à la lecture est **conservé** (`TypeEvenement.NonReconnu`, avec son nom brut) et compté au diagnostic, jamais
fatal ; il n'est jamais écrit. La lecture par plage (`AnalyseReleves`) attache à chaque trou le dernier `arret` /
`jeton_invalide` / `sonde_refusee` qui le précède, sinon « Chronos arrêté » s'il y a un `demarrage` dans le trou, sinon
« cause inconnue » ; `reprise` et `ecriture_ratee` ne causent rien.

### Idempotence et atomicité

Clé d'idempotence **`(t, source)`**. `FileMode.Append` n'est **pas** atomique sous Windows (deux écrivains → une ligne
perdue ou entrelacée, mesuré le 2026-09-27) : l'écrivain ouvre le fichier du mois en **partage exclusif**
(`FileShare.None`, jusqu'à 600 reprises ≈ 0,6 s au pire), relit la queue du fichier (16 Ko ≈ 70 lignes ≈ 6 h) **sous le
même verrou**, compare `t` au dernier `t` de la même source, puis écrit la ligne d'une seule écriture. Deux processus
n'écrivent donc pas deux fois le même relevé, et une écriture qui échoue est consignée (`DerniereErreur`, événement
`ecriture_ratee`). Lecture tolérante ligne par ligne (`LecteurJournal`) : ligne tronquée, `v` inconnu, `t` illisible,
relevé sans `source` → ignorées et **comptées** (`LignesIgnorees`), le reste du fichier est lu.

L'âge de la dernière écriture (`IEtatJournal.DerniereEcriture`) est affiché dans les réglages et le diagnostic ;
au-delà de `SeuilMuet` = 3 × 300 s = 15 min sans écriture (mesuré depuis le démarrage du processus au plus tôt) :
« journal muet depuis N min ».

### Les deux vues d'AppData (à relire avant tout constat)

Tout processus lancé sous l'app bureau Claude (session, hook, `dotnet test`, agent) lit et écrit la copie copy-on-write du
paquet MSIX (`%LOCALAPPDATA%\Packages\Claude_…\LocalCache\Roaming\Chronos\`) — la **vue virtualisée** ; l'overlay lancé par
l'Explorateur écrit la **vue réelle** (`%APPDATA%\Chronos\`). C'est ainsi qu'un `last-exact.json` figé au **2026-09-13**
12:44:59 (vue virtualisée) a été pris pour un gel, alors que le fichier réel était réécrit chaque minute (sonde WMI hors
arbre du 2026-09-27 01:56). Le diagnostic dit désormais « Vue AppData : réelle | virtualisée » ; les relevés d'un constat
se font par une sonde hors de l'arbre de l'app (§6 ; `desktop-app-sessions.md` §7). **Lancer l'exe depuis un terminal de
l'app produit un second jeu de fichiers**, journal compris — c'est l'utilisateur qui lance l'overlay, par l'Explorateur.

### Trois hypothèses à vérifier AVEC le journal (pas avant)

- **HYP-1 — granularité des en-têtes** : `utilization` semble arrondie au centième (0,01 constaté sur les en-têtes ;
  0,12 puis 0,13, jamais 0,125). Le journal écrit le `double` brut, sans arrondi (D-32-18) ; si des valeurs à trois
  décimales apparaissent dans `u5`/`u7`, l'hypothèse tombe et la vue Jour (phase 34) gagne en finesse.
- **HYP-2 — Δ = consommation** : la différence d'`utilization` entre deux relevés consécutifs de même `resets_at` est la
  consommation de l'intervalle (pas de **recalcul rétroactif** hors reset). La lecture par plage ne calcule un Δ que si
  `r[i] == r[i−1]`, `u` connu des deux côtés et aucun trou entre les deux ; un Δ négatif est conservé et marqué
  `Anormal` : s'il s'en produit sans changement de `resets_at`, l'hypothèse tombe.
- **HYP-3 — reset hebdo à l'heure locale au changement d'heure** : `resets_at` 7 j = samedi 00:00 heure locale
  (`2026-09-18T22:00Z` constaté = samedi 19/09 00:00 Paris). Le **25/10/2026** (fin de l'heure d'été), la semaine du
  24 au 31 octobre dure 169 h et l'attendu est **`2026-10-30T23:00Z`** ; `WeeklyWindow`/`WeeklyRecalibration.NextReset`
  (7 × 24 h fixes) diraient `2026-10-30T22:00Z` — écart d'exactement 1 h (`BornesPlage`, 32-06). Le journal tranchera
  (`r7` de la première semaine de novembre) ; jusque-là la borne de forfait est un calcul local, best-effort.

### Ce que le journal n'est pas

Pas une source d'affichage du cadran (il n'est jamais relu par l'overlay pour afficher) ; pas un pourcentage dérivé de
tokens — les tokens des transcripts vivent au §8 (`tokens-AAAA-MM.jsonl`), sur leur propre axe, dédoublonnés
par `message.id` (voir §2 : une ligne `assistant` par bloc de contenu, `output_tokens` partiel croissant, dédup = max par
champ via `DedupUsage`) ; aucune projection ; aucun trou interpolé ; aucune estimation présentée comme exacte.

---

## 8. Agrégats de tokens

> Ajouté le 2026-09-27 (phase 33, exe 3.3.0). Les agrégats ne sondent rien et n'appellent rien : ils comptent les tokens des
> transcripts locaux de Claude Code, en lecture seule stricte de `~/.claude/projects`, et les écrivent à côté du journal (§7).
> Types : `Services/Historique/Tokens/{LigneAgregat, MagasinAgregats, CouvertureTokens, LecteurTranscript, IndexMessages, Curseurs,
> ReconstructionTokens, ProjectionAgregats, IEtatReconstruction, LecteurAgregats, RenduLocalTokens}`,
> `Models/Historique/Tokens/{TrancheTokens, Couverture, PhaseReconstruction, LectureAgregats}`.

**Périmètre : Claude Code seulement — hors Cowork et claude.ai ; bruts, non pondérés.** (`LigneAgregat.Perimetre`, cité tel quel par
le diagnostic, section `[Magasins persistants]`.) Ces agrégats comptent les tokens des transcripts locaux de Claude Code (session
principale et `subagents/`) ; ils ne voient ni l'app de bureau, ni Cowork, ni claude.ai, et ne sont pas pondérés par modèle. Ils
vivent sur LEUR axe : aucun type du quartier `…Historique.Tokens` n'expose un flottant (garde `GardeTokensSansPourcentageTests`,
réflexive et textuelle), et **jamais** un **pourcentage** du forfait n'en est dérivé (TOK-05) — le forfait, c'est le §7.

### Emplacement et fichiers

Sous `%APPDATA%\Chronos\historique\` (chemin par `ChronosPaths.HistoriqueDir`, le dossier du journal), vue RÉELLE quand l'overlay
est lancé par l'Explorateur — les deux vues d'AppData du §7 s'appliquent mot pour mot. UTF-8 sans BOM, `\n` seul, instants au format
aller-retour « O » UTC (`2026-09-01T00:00:00.0000000+00:00`), le même dialecte que `t` au §7.

| Fichier | Rôle | Écriture |
|---|---|---|
| `tokens-AAAA-MM.jsonl` | les agrégats, un fichier par **mois UTC du `slot`** — la PROJECTION de l'index | réécriture atomique du mois entier (temp + `File.Move`), triée (`slot`, `model` ordinal, `sub` false avant true) : même état = mêmes octets |
| `ids-AAAA-MM.jsonl` | l'index des messages vus, un shard par mois UTC du premier `ts` — la MÉMOIRE D'IDEMPOTENCE | ajout seul sous partage exclusif ; une ligne par delta ; à la relecture, le **max par champ** gagne |
| `curseurs.json` | `{v, fichiers: {chemin relatif à la racine → {offset, taille, mtime}}}` | atomique (temp + `File.Move`), APRÈS les agrégats de chaque lot |
| `couverture.json` | `{v, plus_ancienne_ligne_vue, intervalles: [{debut, fin}]}` — les intervalles garantis | atomique, après chaque passe COMPLÈTE seulement (une passe annulée ne garantit rien) |

### Ligne d'agrégat (une tranche de 15 min UTC × modèle × origine)

| champ | type / unité | d'où il vient |
|---|---|---|
| `v` | entier, version du schéma de ligne (1 ; une autre valeur = ligne sautée et comptée) | `LigneAgregat.SchemaVersion` |
| `slot` | début de tranche de **15 min**, UTC, format « O » ; non aligné = ligne refusée | `TrancheTokens.SlotDe(premier timestamp du message)` |
| `model` | identifiant tel que lu (`claude-opus-5`, `claude-sonnet-5`…), jamais normalisé | `message.model` |
| `sub` | booléen : le transcript vit sous un dossier `subagents/` | chemin du fichier (`LecteurTranscript.EstSousAgent`) |
| `in` | entier, `input_tokens` sommés sur les messages distincts de la tranche | `usage.input_tokens` via `DedupUsage.LireUsage` |
| `out` | entier, `output_tokens` sommés | `usage.output_tokens` |
| `cache_w` | entier, `cache_creation_input_tokens` sommés | `usage.cache_creation_input_tokens` |
| `cache_r` | entier, `cache_read_input_tokens` sommés | `usage.cache_read_input_tokens` |
| `n` | entier, nombre de messages DISTINCTS dans la tranche | taille du groupe d'ids |

Quatre compteurs SÉPARÉS, **jamais leur somme** ; jamais un message individuel, jamais du texte. Un compteur absent, non entier ou
négatif REFUSE la ligne (une somme sans compteur n'existe pas — à la différence du §7 où un champ absent vaut `null`) ; `model` absent
refuse ; `sub` absent vaut `false`. Exemple :
`{"v":1,"slot":"2026-09-27T09:15:00.0000000+00:00","model":"claude-opus-5","sub":false,"in":12,"out":3480,"cache_w":52110,"cache_r":918204,"n":7}`
(≈ 146 o par ligne).

### Ligne d'index (un message vu, tel que compté)

`v` (1), `id` (`message.id`, repli `requestId` ; une ligne sans aucun identifiant reçoit une **clé synthétique** déterministe
`sans-id:{ticks UTC}:{modèle}:{0|1}:{quatre compteurs}` — D-33-18), `ts` (premier timestamp vu, « O » UTC), `model`, `sub`, puis les quatre
compteurs de l'agrégat (mêmes noms, mêmes unités : entrée, sortie, écriture de cache, lecture de cache). Le shard est **en ajout seul** :
un id re-rencontré avec des compteurs plus grands (bloc partiel qui a grandi) écrit une nouvelle ligne, et la relecture garde le max de
chaque champ. Ordre d'écriture à chaque lot : ids (ajout) → agrégats (`File.Move`) → `curseurs.json` (`File.Move`) ; au démarrage, les mois
OUVERTS sont reprojetés depuis l'index (`ProjectionAgregats.Projeter`) — un arrêt à n'importe quel point est rattrapé, prouvé octet pour octet.

### Dédup et idempotence

Une ligne `assistant` par bloc de contenu (§2) ; dédup par `message.id` (repli `requestId`), **max par champ** via `DedupUsage.Fusionner`
(la règle CPT-01, à UN endroit). Sur cette machine, **1 377 ids vivent dans 2 à 4 fichiers** (fork / resume, copies indiscernables
structurellement, âge max 3,6 j) : une dédup par fichier sur-compterait de 2,1 % à 2,4 % — d'où l'index. Re-rencontrer un id applique
`max − déjà compté` : reprise après arrêt, fichier raccourci ou renommé, copie fork, bloc partiel = un seul et même cas. Limites écrites :
une ligne `assistant` à quatre compteurs nuls (`<synthetic>`) n'est ni comptée ni indexée (D-33-10) ; deux lignes sans id strictement
identiques (même instant, même modèle, même origine, mêmes quatre compteurs) sont indiscernables et comptent une fois (D-33-18 ; 0 ligne
sans id observée sur 238 857) ; une ligne datée de plus de 24 h dans le futur est ignorée et comptée, une ligne future de moins de 24 h
bloque le curseur devant elle (D-33-09).

### Bornes (valeurs du code, gardées par `ContratAgregatsDocumenteTests`)

- `IndexMessages.HorizonIndex` = **45 j** : les shards des mois qui chevauchent [maintenant − 45 j, maintenant] sont chargés en mémoire
  (≈ 118 k ids, tas résident ≈ 40–50 Mo mesurés).
- `IndexMessages.RetentionIndexMois` = **3 mois** : au-delà, le shard est supprimé et le mois d'agrégats est GELÉ — une copie fork d'un
  message de plus de 3 mois serait comptée deux fois (jamais observé : âge max des copies 3,6 j).
- `MagasinAgregats.RetentionMois` = rétention du journal = **24 mois** ; purge au démarrage, noms non conformes ignorés et comptés.
- `CouvertureTokens.HorizonPurge` = **30 j** (HYP-4 ci-dessous) ; `ReconstructionTokens.CadenceIncrementale` = **60 s** ;
  `SemaineCourante` = 7 j (par mtime) ; flush par lot de 100 fichiers ou 2 s.

### Reconstruction et incrémental

Service hébergé (`ReconstructionTokens`, inscrit AVANT l'orchestrateur : démarre avec la tête, s'arrête avant elle) qui possède un thread
dédié `IsBackground` **`BelowNormal`** nommé `Chronos.AgregatsTokens`, boucle synchrone, annulation honorée entre deux fichiers et toutes
les 4 096 lignes. Passe : inventaire, tri par **mtime décroissant**, lecture en flux au niveau octet (`FileShare.ReadWrite | Delete`,
offset de la dernière ligne COMPLÈTE), pré-filtre texte `"type":"assistant"` puis autorité `type` / `role` ; **la semaine courante est
disponible** (sur le disque) au premier fichier plus vieux que 7 j. Puis toutes les 60 s : inchangé (taille, mtime, offset == taille)
→ pas ouvert ; grandi → relu depuis l'offset ; raccourci ou renommé → relu de zéro ; disparu → retiré des curseurs, **rien n'est
soustrait** (une purge par Claude Code ne fait pas disparaître l'historique déjà journalisé). Une passe en échec pose `EnEchec` +
`DerniereErreur` (préfixée par la brique : « index : », « agrégats : », « curseurs.json : », « couverture : ») et la passe suivante
recommence. Progression exposée en ENTIERS (`IEtatReconstruction` : N / M fichiers, phase, dernier fichier relatif) au ViewModel
(« reconstruction des tokens — N / M fichiers · la semaine courante est déjà complète ») et au diagnostic.

### Couverture — pourquoi une plage vide dit POURQUOI elle est vide

Trois états, jamais un zéro implicite : **« hors couverture »** = avant la plus ancienne ligne jamais vue (`plus_ancienne_ligne_vue`,
ici `2026-06-23T12:44:22Z`) ; **« transcripts absents »** = hors de tout intervalle garanti `[début de passe − HorizonPurge, fin de passe[`
(juillet 2026 : 458 tuples survivent dans des fichiers d'août — présence PARTIELLE, jamais « zéro token ») ; **« couverte »** = dans un
intervalle garanti : l'absence de tranche est une vraie absence d'activité. La couverture est un état persisté et daté, jamais déduit
des mtimes. Rendu local (`RenduLocalTokens`) : une barre par DÉBUT D'HEURE UTC libellée en heure locale — **25 barres le 25/10/2026**
(deux « 02:00 »), **23 le 28/03/2027** (aucune « 02:00 » ; le lundi 29 est un jour ordinaire), 24 sinon ; l'état de couverture voyage
avec chaque barre.

### Hypothèse à vérifier AVEC les fichiers (pas avant)

- **HYP-4 — horizon de purge de Claude Code** : `HorizonPurge` = **30 j** = `cleanupPeriodDays` par défaut (non défini dans
  `~/.claude/settings.json` sur cette machine, NON lu par Chronos en v1.8). Si Claude Code purge plus tôt, des tranches classées
  « couvertes » pourraient manquer — à vérifier après un mois en comparant le plus vieux mtime des transcripts au début du dernier
  intervalle garanti de `couverture.json`. Le mécanisme exact de purge n'est pas vérifié (38 fichiers de juin survivent au 27/09).

### Mesures (vraie machine, lecture seule, 2026-09-27)

| Mesure | Prototype de recherche (11:20–11:45, cache chaud) | Service réel `ReconstructionTokens` (harnais hors arbre, 11:40) |
|---|---|---|
| Transcripts | 1 603 fichiers / 2,08 Go / 238 857 lignes `assistant` / 118 401 ids | 1 611 fichiers / 118 641 ids |
| Passe complète (mur / CPU) | **2,4–2,6 s / 2,7 s** (788–800 Mo/s) | **3,2 s / 3,6 s** à chaud (× 2 runs) ; **15,0 s / 3,75 s** au premier run (cache froid, SATA) |
| Semaine courante disponible | 1,7 s (886 fichiers) | — (checkpoint par mtime, flush réussi) |
| Cycle incrémental, rien n'a bougé | 63 ms de contrôle (+ 93 ms d'inventaire) | **47 ms** (0 fichier ouvert) |
| Mémoire | pic +90 Mo, tas 31 Mo | pic working set 120–176 Mo (+93 à +149 Mo), tas GC 40–51 Mo |
| Agrégats produits | 3 886 tuples = 524 Ko | **3 853 tuples** (− 34 `<synthetic>` exclus) ≈ 570 Ko, dont `tokens-2026-09.jsonl` 331 Ko / 2 233 lignes |
| Index d'ids | ≈ 11 Mo/mois estimés | 170 234 lignes = **28,8 Mo**, dont `ids-2026-09.jsonl` 19,6 Mo (les blocs partiels écrivent plusieurs lignes par id) |
| `curseurs.json` / `couverture.json` | 351 816 o | 356 290 o / 173 o |

L'estimation d'entrée « 10–20 s CPU » est périmée : ≈ 3,5 s CPU à priorité basse, une fois par lancement, puis 47 ms par minute. Le
service réel coûte plus que le prototype (flush par lot, shards d'ids, reprojection) et c'est accepté : c'est le prix de la reprise
idempotente. Le premier lancement à froid peut prendre une quinzaine de secondes sur ce disque SATA — l'UI n'attend pas.

---

*Fin du document — capturé le 2026-07-08, à revalider à chaque MAJ majeure de Claude Code (schéma = API privée de facto) ;
complété le 2026-09-27 (§7, note CPT-01 du §2 ; §8 agrégats de tokens).*
