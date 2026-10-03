# Chronos — Sources de données

> **Réécrit en 2026-10 (phase 37, purge R5) — méthodologie unique.** Ce document décrit la seule chaîne qui alimente le
> cadran : d'où vient chaque chiffre, ce qui est exact et ce qui ne l'est pas. Les sources retirées sont nommées au §5,
> une ligne chacune ; les §6 à §9 (widget de sessions, journal d'historique, agrégats de tokens, lecture par la fenêtre
> Historique) sont inchangés.

```
 sonde d'en-têtes de rate-limit ─┐
                                 ├─► composite PAR FENÊTRE ─► journal ─► dernier exact ─► cadran
 secours OAuth du login Chronos ─┘   (sonde prioritaire)     (observe)   (tête + doctrine
                                                                          de fraîcheur)
```

Règle cardinale : `utilization` / `resets_at` fournis par le serveur priment sur tout comptage local, et **le seul chiffre
non exact que Chronos affiche est le plancher « ≥ X % »** (§3). Jamais une estimation présentée comme exacte.

---

## 1. La chaîne exacte — source → cadran

**Sonde d'en-têtes (source primaire).** Une requête jetable `POST /v1/messages` (`max_tokens` à 1, modèle le moins cher
de la gamme), au plus **une toutes les 300 s** (`RateLimitHeaderUsageProvider.CadenceNominale`), authentifiée par le
jeton du login Chronos. Le corps de la réponse n'est jamais lu : les chiffres viennent des **en-têtes**
`anthropic-ratelimit-unified-*` — utilisation et reset de la fenêtre 5 h et de la fenêtre hebdomadaire, **statut serveur**
déclaré (autorisé / avertissement / rejeté) et **dépassement** éventuel (canal latéral `IEtatServeur`). Elle répond même
quand l'API sature : un 429 porte ses en-têtes. Interrupteur dans les réglages (section Données), coût écrit à côté.

**Secours OAuth du login Chronos.** `GET /api/oauth/usage` avec le jeton **propre à Chronos**, obtenu par le login intégré
(« Se connecter à Claude ») et rangé chiffré par DPAPI (portée utilisateur) dans `%APPDATA%\Chronos\oauth.dat`. Aucun autre
coffre n'est lu. Une seule autorité de jeton (`ChronosTokenAuthority`) le distribue à la sonde et au secours : deux
détenteurs concurrents du jeton de renouvellement produiraient une fausse déconnexion.

**Composite PAR FENÊTRE.** `CompositeUsageProvider` retient, fenêtre par fenêtre, la source la plus fiable ; à fiabilité
égale, la sonde gagne — `Best()` ne retient le secours que s'il est **strictement** plus fiable. Il ne juge jamais la
fraîcheur ni la récence entre deux exacts : la porte d'âge vit plus haut.

**Journal.** `JournalisationUsageProvider`, entre la tête et le composite, voit le relevé brut (avant toute substitution
par le dernier exact) et l'écrit dans le journal d'historique (§7). Il observe, il ne décide rien.

**Tête : dernier exact + doctrine de fraîcheur.** `LastExactUsageProvider` persiste chaque relevé exact
(`last-exact.json`) et `DoctrineFraicheur` statue sur chaque fenêtre (§3) : frais, encore valide, plancher ou indisponible.
Le cadran n'affiche que ce que la tête rend.

---

## 2. Transcripts JSONL — activité et plancher, jamais un pourcentage

### Localisation

`~/.claude/projects/<slug-projet>/<session-uuid>.jsonl` — un transcript par session, en
append continu pendant que la session tourne.

### Ce qu'ils servent

Les transcripts ne produisent **jamais un pourcentage** : les plafonds du forfait ne sont pas publiés, et les transcripts
ignorent l'app bureau et Cowork, qui consomment le même pool. Ils servent à trois choses :

- **l'activité** : y a-t-il eu une réponse assistant depuis le dernier relevé exact ? C'est ce qui sépare « encore
  valide » du plancher (§3) ;
- **le plancher** : le nombre de tokens consommés depuis ce relevé, porté avec le plancher ;
- **l'Historique** : les agrégats de tokens du §8, sur leur propre axe, jamais convertis en pourcentage du forfait.

### Schéma d'une ligne `assistant`

Chaque ligne est un objet JSON autonome. Clés de haut niveau observées :

`cwd`, `entrypoint`, `gitBranch`, `isSidechain`, `message`, `parentUuid`, `requestId`,
`sessionId`, `timestamp`, `type`, `userType`, `uuid`, `version`.

Filtrer sur `o["type"] == "assistant"` (et `message.role == "assistant"`) pour ne retenir que
les réponses porteuses d'usage. Objet `message.usage` (valeurs synthétiques) :

```jsonc
"usage": {
  "input_tokens": 20863,
  "output_tokens": 1496,
  "cache_creation_input_tokens": 7814,
  "cache_read_input_tokens": 30962,
  "service_tier": "standard"
}
```

> **2026-09-27 (phase 32, CPT-01)** : une ligne `assistant` par bloc de contenu d'un même `message.id`,
> `output_tokens` partiel et croissant (8 → 8 → 256), les trois autres champs identiques ; 491 ids sur
> 8 jours présents dans 2 à 3 fichiers (fork/resume). Toute somme passe par `DedupUsage` (max par champ
> par `message.id`, repli `requestId`, dictionnaire global à la passe) : sommer les lignes compterait
> l'entrée ×2,1.

`timestamp` = **ISO 8601 UTC** avec millisecondes et suffixe `Z`. Lecture en streaming, `FileShare.ReadWrite` (le fichier
est en cours d'écriture), dernière ligne partielle ignorée sans bruit.

### Blocs sous-agents

Les sous-agents vivent dans `~/.claude/projects/<slug>/<session-uuid>/subagents/` (`agent-<id>.jsonl`, lignes
`isSidechain: true`, et `agent-<id>.meta.json`).

**Lu par le widget de sessions depuis la phase 30.1 (SUB-01)** — seulement l'instant du dernier message
de chaque `agent-<id>.jsonl`, comme signal de TRAVAIL de sa session, jamais comme ligne ni comme compte de
tokens : voir `docs/hooks-contract.md` §3 et §4. La bande d'activité V2-01 reste différée.

---

## 3. Ce qui est exact, ce qui ne l'est pas

Une fenêtre affichée est dans l'un de ces quatre états, décidés par `DoctrineFraicheur` :

| État | Condition | Affichage |
|------|-----------|-----------|
| **Exact — frais** | relevé de la sonde ou du secours OAuth, ou dernier exact persisté, âgé d'au plus **360 s** (`DoctrineFraicheur.LimiteAge` = cadence de la sonde + 60 s, non réglable) | le pourcentage, sans signe |
| **Exact — encore valide** | relevé plus ancien, mais **aucune activité Claude Code** depuis (transcripts) : l'utilisation n'a pas bougé | le pourcentage, sans signe |
| **Plancher (non exact)** | relevé plus ancien **et** Claude Code a travaillé depuis | **« ≥ X % »** : le vrai chiffre est au moins celui-là ; la borne supérieure est inconnue |
| **Indisponible** | aucun relevé, relevé sans horodatage, ou activité impossible à établir | neutre, « données indisponibles » — jamais une valeur inventée |

**Seul le plancher « ≥ » n'est pas exact.** Sa valeur n'est jamais augmentée d'un delta calculé : c'est la qualification
du chiffre qui change, pas le chiffre. Le « ≥ » et non un « ~ » : l'incertitude est unilatérale.

**Les resets viennent toujours du serveur** (`resets_at` des en-têtes ou du secours). Aucune ancre manuelle n'entre dans le
cadran ; l'ancre hebdomadaire déjà enregistrée par une ancienne version n'est plus que **lue en secours** par l'Historique.

Toutes les conversions d'unité (pourcentage ↔ fraction, epoch ↔ instant) passent par `UsageNormalization`, point unique.

---

## 4. Hypothèses & points de fragilité

- **Famille d'en-têtes « unified » non documentée.** `anthropic-ratelimit-unified-*` n'apparaît nulle part dans la
  documentation publique d'Anthropic : elle peut être renommée à tout moment. Le diagnostic liste les noms reconnus (jamais
  leurs valeurs) ; un 200 sans aucun en-tête reconnu est dit tel quel (« la famille a peut-être été renommée ») et rien
  n'est affiché plutôt qu'un chiffre inventé.
- **Endpoint OAuth non documenté.** `/api/oauth/usage` n'est pas un contrat public ; il peut changer à une mise à jour de
  Claude. En cas d'échec, la fenêtre passe à « indisponible » (ou reste sur la sonde), jamais un plantage.
- **Modèle de la sonde.** La sonde vise le modèle le moins cher de la gamme ; un identifiant retiré par le serveur est dit
  « modèle refusé » dans le diagnostic et se met à jour dans le code.
- **Coût.** Une micro-requête toutes les 300 s au plus (≈ 288 par jour), sur le compte de l'utilisateur ; une sonde
  rejetée (429) ne consomme pas de quota. Désactivable dans les réglages.
- **Présence conditionnelle.** Chaque fenêtre peut être indépendamment absente d'une réponse : le composite choisit par
  fenêtre et l'absence se dégrade en « indisponible » (ROB-01), sans crash.
- **Sécurité.** Le jeton n'est jamais écrit dans un rapport ni un journal ; le contenu des conversations n'est jamais lu
  (tokens et métadonnées seulement). Lecture seule sous le profil utilisateur, aucun droit admin.

---

## 5. Sources retirées en 3.5

Retirées par la purge de la phase 37 ; chacune décrivait un chemin mort ou trompeur.

- **Lecture du jeton de l'app bureau Claude** (coffre chiffré de l'app, gestionnaire d'identifiants Windows) : remplacée par
  le login propre à Chronos ; plus aucun autre coffre n'est lu.
- **Barre de statut de Claude Code** comme source d'usage : le pont qui recopiait son bloc d'usage dans un fichier local
  disparaît, et la barre installée par Chronos est retirée au premier lancement (une barre d'un tiers reste intacte).
- **Recalibrage manuel du reset hebdomadaire** : les resets viennent toujours du serveur.
- **Estimation par comptage de tokens** rapportée à un plafond supposé : les transcripts ne donnent plus qu'activité et
  plancher (§2).

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
| `source` | `SondeEnTetes` · `EndpointOAuthChronos` | producteur ; **jamais** `MagasinDernierExact` |

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
  24 au 31 octobre dure 169 h et l'attendu est **`2026-10-30T23:00Z`** ; un calcul local à 7 × 24 h fixes depuis la
  dernière ancre dirait `2026-10-30T22:00Z` — écart d'exactement 1 h (`BornesPlage`, 32-06). Le journal tranchera
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

## 9. Lecture par la fenêtre Historique

La fenêtre Historique (phases 34-35) ne lit jamais le journal elle-même : elle passe par une façade neutre,
`ISourceHistorique`, qui rend des données déjà analysées pour une plage déjà calculée. Deux implémentations :
`SourceHistoriqueDisque` (les fichiers du §7 et du §8, en production) et `SourceHistoriqueDemonstration` (les scénarios de la
galerie `--historique`, sans aucun fichier).

### Plages

`BornesPlage` calcule la plage AVANT la lecture, dans le fuseau injecté par la racine de composition :

- **Semaine de forfait** : alignée sur le `resets_at` 7 j observé dans le journal (`r7` du dernier relevé) ; repli
  `WeeklyAnchor` des réglages ; repli le samedi du calendrier.
- **Jour** : de minuit local à minuit local (23 h, 24 h ou 25 h selon le changement d'heure).
- **4 semaines** : `BornesPlage.QuatreSemaines`, la semaine de forfait courante et les trois précédentes.

### Règles de lecture

- **Instant d'analyse** : chaque plage est analysée à `InstantDAnalyse` = min(maintenant, fin de la plage). Une plage révolue
  analysée au vrai « maintenant » finirait par un faux trou ouvert entre sa fin et aujourd'hui.
- **Quatre semaines, une seule lecture** : `LireQuatreSemaines` lit le journal une fois sur [S-3 début, S fin[ et rend
  quatre analyses, chacune à son `InstantDAnalyse` ; les semaines passées sont reportées sur l'axe de la semaine courante par
  fraction de leur durée (`EchelleTemps.Reporter` : une semaine de 169 h ou de 167 h au changement d'heure tient sur le même
  axe). Aucun agrégat de tokens n'est lu pour cette vue.
- **Une source par vue** (D-32-25) : l'analyse retient la source demandée, sinon la plus fréquente de la plage ; deux sources ne
  sont jamais mélangées sur une même ligne.
- **Veille de minuit, vue Jour seulement** : pour nommer un trou qui chevauche minuit, la façade lit aussi la veille — le dernier
  relevé de la même source dans les sept jours précédents et les événements qui le suivent. L'analyse voit le trou et sa cause
  (« Chronos arrêté, mar. 23:00 → 07:00 ») ; la série RENDUE est ensuite restreinte au jour : rien n'est dessiné ni compté avant
  minuit. La Semaine et les 4 semaines ne lisent pas la veille.
- **Divergence « consommé ailleurs »** : une marche du % hebdo d'au moins 0,01 (`Divergences.SeuilDelta`, la granularité
  constatée des en-têtes) sur une heure dont les transcripts sont couverts mais sans aucune tranche de tokens Claude Code. Une
  heure non couverte n'accuse jamais Cowork.
- **Le diagnostic lit par la MÊME façade** : la section « Journal d'historique » de « Diagnostic… » dit la journée par
  `SourceHistoriqueDisque.LireJour` et par les mots de la vue Jour, jamais par un second chemin de lecture.

### Écarts connus des maquettes

Écarts assumés entre les maquettes Figma (frames A à F) et la fenêtre livrée en 3.3.0 :

- Coins arrondis par DWM (rayon système ≈ 8 px au lieu de 16) sous Windows 11, coins droits sous Windows 10 : la fenêtre n'est
  pas layered (`AllowsTransparency=False`), elle ne peut pas dessiner ses propres coins ni son ombre.
- Badge de version de l'en-tête omis.
- Segoe UI au lieu d'Inter, aux mêmes corps.
- Tirets de reset 5 h dessinés dans un trou quand le serveur avait annoncé l'instant du reset avant le trou.
- Pas de réticule ni d'infobulle en vue 4 semaines (le §2.4 du plan de design n'en prévoit pas).
- La semaine épuisée de la vue 4 semaines n'existe qu'en fixture de test : la galerie `--historique` ne la montre pas.
- Rangées de la couverture par semaine au pas de 16 px (lecture de « espacées de 16 px » de la frame E).
- Simple clic au centre du cadran retardé du délai de double-clic de Windows (≈ 0,5 s) pour que le double-clic ouvre
  l'Historique sans déclencher de bascule.

---

*Fin du document — §1 à §5 réécrits en 2026-10 (phase 37, méthodologie unique : sonde d'en-têtes, secours OAuth du login
Chronos, journal, dernier exact ; seul le plancher « ≥ » n'est pas exact) ; §6 à §9 inchangés (§7 journal d'historique,
§8 agrégats de tokens, §9 lecture par la fenêtre Historique).*
