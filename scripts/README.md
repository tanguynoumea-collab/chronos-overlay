# Chronos — Pont statusLine (`scripts/`)

Ce dossier contient la **source primaire** de Chronos : le pont qui materialise le bloc
`rate_limits` de Claude Code dans un fichier watchable, plus son installeur idempotent.

| Fichier | Role |
|---------|------|
| `chronos-statusline-bridge.js` | Le **pont** : lit le JSON de session sur `stdin`, ecrit `%APPDATA%\Chronos\usage.json`, puis re-execute la statusLine existante. |
| `install-bridge.mjs` | L'**installeur** idempotent : branche le pont dans `~/.claude/settings.json` de facon non destructive (backup + chainage). |
| `fixtures/statusline-input.json` | Echantillon `stdin` realiste (avec `rate_limits`) pour tester le pont a la main. |

---

## 1. Role du pont

Claude Code **POUSSE** le JSON de session (dont le bloc `rate_limits` : fenetres `five_hour` /
`seven_day`) sur le `stdin` de la commande `statusLine`. **Cet objet n'est persiste dans aucun
fichier** (voir `docs/data-sources.md` § 1). Pour qu'un overlay externe (Chronos) le consomme,
il faut un **pont** qui :

1. bufferise le `stdin`,
2. en extrait `data.rate_limits`,
3. l'ecrit **atomiquement** (temp + `renameSync`) dans `%APPDATA%\Chronos\usage.json`,
4. **re-execute la statusLine existante** (`gsd-statusline.js`) avec le meme `stdin` et re-emet
   sa sortie intacte — la barre affichee n'est jamais cassee.

L'ecriture se fait **AVANT** le re-lancement de la statusLine enfant, pour aboutir meme si
Claude Code annule le rendu en vol (debounce ~300 ms).

**Contrainte non destructive** : une **seule** commande `statusLine` est configurable dans
`settings.json`. Le pont **enveloppe** la commande existante au lieu de la remplacer.

---

## 2. Installation

### Automatique (recommandee)

```bash
node scripts/install-bridge.mjs
```

L'installeur :

1. lit `~/.claude/settings.json` (JSON tolerant ; s'il est illisible -> **abandon**, rien n'est
   ecrit) ;
2. cree une **sauvegarde** `~/.claude/settings.json.chronos.bak` (jamais ecrasee : si un backup
   existe deja, un timestamp est suffixe) ;
3. detecte la commande `statusLine` existante :
   - **deja pontee** (contient `chronos-statusline-bridge`) -> **no-op** (« deja installe »,
     idempotent : relancer ne re-modifie rien) ;
   - **une autre commande** existe -> le pont doit la re-executer. Le pont chaine en dur
     `gsd-statusline.js` (`CHILD_STATUSLINE`). Si la commande existante **correspond** a cet
     enfant, l'installeur procede ; si elle **differe**, il **avertit et abandonne** (pour ne pas
     perdre ta barre) et te demande soit d'editer `CHILD_STATUSLINE`, soit de relancer avec
     `--force` ;
   - **aucune commande** -> le pont est installe seul (sans chainage) ;
4. ecrit **uniquement** la cle `statusLine` (toutes les autres cles de `settings.json` sont
   preservees), en ecriture **atomique**.

Ouvre ensuite (ou continue) une session Claude Code : apres la 1re reponse de l'assistant,
`%APPDATA%\Chronos\usage.json` se remplit.

### Manuelle

Si tu preferes editer toi-meme `~/.claude/settings.json` :

1. **Sauvegarde** d'abord le fichier (copie en `.chronos.bak`).
2. Remplace la valeur de `statusLine.command` par :

   ```json
   "statusLine": {
     "type": "command",
     "command": "node \"<chemin-absolu>/scripts/chronos-statusline-bridge.js\""
   }
   ```

3. **Garde ton ancienne commande** : le pont la re-execute lui-meme. Le pont chaine en dur
   `gsd-statusline.js` via la constante `CHILD_STATUSLINE` en tete de
   `chronos-statusline-bridge.js`. Si ta commande d'origine est differente, **edite
   `CHILD_STATUSLINE`** pour la faire pointer sur ta commande, sinon elle ne sera plus executee.

---

## 3. Desinstallation

### Automatique

```bash
node scripts/install-bridge.mjs --uninstall
```

Restaure la `statusLine` d'origine depuis `~/.claude/settings.json.chronos.bak` (ou, a defaut de
backup exploitable, restaure la commande enfant `CHILD_STATUSLINE` que le pont chainait). Ne
touche qu'a la cle `statusLine`.

### Manuelle

Restaure `~/.claude/settings.json` depuis la sauvegarde `.chronos.bak`, ou remets a la main
`statusLine.command` sur ton ancienne commande.

---

## 4. Verification

Apres installation et **au moins une reponse de l'assistant** dans une session Claude Code
reelle (le bloc `rate_limits` n'apparait qu'apres la 1re reponse API, abonnes Pro/Max) :

- la statusLine s'affiche **normalement** (model | task | dir | contexte) — le pont ne l'a pas
  cassee ;
- `%APPDATA%\Chronos\usage.json` existe et contient `five_hour` / `seven_day` (ou `null`) +
  `capturedAt`.

```bash
cat "$APPDATA/Chronos/usage.json"   # bash
```

```cmd
type "%APPDATA%\Chronos\usage.json"  :: cmd
```

En cas de statusLine cassee : `node scripts/install-bridge.mjs --uninstall` (ou restaure le
`.chronos.bak`), puis decris le symptome.

---

## 5. Securite

- Le pont ne lit **QUE** `data.rate_limits` du `stdin`. Il ne touche **jamais** aux jetons OAuth
  du profil utilisateur ni au **contenu** des conversations.
- Toute l'ecriture de `usage.json` est en `try/catch` **best-effort** : une erreur d'ecriture ne
  casse **jamais** la statusLine.
- Chemins uniquement sous le profil utilisateur (`%APPDATA%`, `~/.claude`), aucun droit admin.
