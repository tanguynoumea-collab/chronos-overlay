# Phase 31 : Écrit, publié, constaté — Contexte

**Gathered:** 2026-09-25
**Status:** Ready for planning (dernière phase, après 28-30)
**Source:** procédure de la release 3.1.0 (commit `cd26b31`), `docs/publish.md`, mémoire du projet (version dans l'exe ET dans le nom du fichier), rétrospective v1.6

<domain>
## Phase Boundary

Trois livrables, dans cet ordre : (1) `docs/desktop-app-sessions.md` décrit le contrat FINAL de la source
app-bureau (VAL-02) ; (2) l'exe **3.2.0** est publié et réconcilié (VAL-03) ; (3) l'utilisateur constate sur sa
machine les trois lignes de son tableau — « Réflexion », « En attente », rien — et le résultat est consigné
(point de contrôle humain, leçon de la rétrospective v1.6).

</domain>

<decisions>
## Implementation Decisions

### Le document (VAL-02)
- `docs/desktop-app-sessions.md`, même structure que `docs/hooks-contract.md` : ce qui est lu (chemin,
  virtualisation AppData par MSIX — PAS une jonction —, champs), ce qui est produit (titre, dernier focus, `blocked`), ce qui n'est PAS garanti
  (format interne non documenté, réécriture intégrale, `postTurnSummary` transitoire, `lastFocusedAt` tel
  que relevé en phase 27 : mis à jour par alt-tab et par re-sélection, PAS par la fin d'un tour), la date du
  relevé (2026-09-25, app 2.9939.2.0) et la réserve de la phase 27 (16:23 → 19:51 sans mise à jour).
- Une garde croisée (test) compare les champs cités dans le document à ceux que le lecteur lit réellement,
  sur le modèle de `ContratHooksDocumenteTests` (chemin de `docs/` injecté par MSBuild : `CheminDocsChronos`).
- `docs/hooks-contract.md` §3 est relu : les trois mots (« Réflexion », « En attente », « En attente ? ») et la
  règle « lue » (référence vers le nouveau document) ; `docs/data-sources.md` mentionne la nouvelle source.
- README : la section widget de sessions décrit les deux libellés, la disparition à la lecture, le titre.

### La release (VAL-03) — procédure de la 3.1.0, à reproduire
- `src/Chronos/Chronos.csproj` : les QUATRE propriétés passent à 3.2.0 (`Version` 3.2.0, `FileVersion`
  3.2.0.0, `AssemblyVersion` 3.2.0.0, `InformationalVersion` 3.2.0).
- Publication : `dotnet publish src/Chronos -c Release -r win-x64 -p:PublishSingleFile=true
  --self-contained true` (détails `docs/publish.md`), puis copie de l'exe à la racine du dépôt sous le nom
  **`Chronos-v3.2.0.exe`** (NON suivi : le `.gitignore` n'ignorait PAS les exe — `/Chronos-v*.exe` y est ajouté le 2026-09-26 ;
  ne jamais faire `git add -A`). Attendu : mono-fichier, ~77 Mo (< 120), 0 DLL à côté.
- Smoke test AVANT toute réconciliation : `Chronos-v3.2.0.exe --hook SessionStart` avec stdin vide ⇒ code 0,
  aucune écriture, `~/.claude/settings.json` inchangé (md5 avant/après).
- Réconciliation : au premier lancement de la 3.2.0, `SessionHookInstaller` et l'installateur de statusLine
  remplacent les entrées `Chronos-v3.1.0.exe` par `Chronos-v3.2.0.exe` (8 groupes de hooks + statusLine), sans
  doublon, sans toucher aux groupes tiers (`gsd-*`). Constaté dans le fichier, pas supposé. **C'est
  l'utilisateur qui lance l'exe** (l'agent ne lance pas l'overlay : précédent 19-05).
- L'ancien processus `Chronos-v3.1.0.exe` (PID 40772, démarré le 23.09) doit être fermé par l'utilisateur
  avant de lancer la 3.2.0 : il n'existe AUCUN mutex mono-instance dans `src/` (recherche de phase 31). Aucun
  raccourci Chronos dans le dossier Démarrage réel, ni clé `Run`, ni tâche planifiée : rien à repointer.
- La réconciliation se constate dans `~/.claude/settings.json` (sauvegarde horodatée dont le md5 égale celui
  relevé avant le lancement ; fichier courant = sauvegarde + 9 remplacements `Chronos-v3.1.0.exe` →
  `Chronos-v3.2.0.exe`), PAS dans `chronos.log` (écrit avant l'appel à `Reconcile`).
- Le diagnostic gagne une ligne `Version :` (une ligne, un test) avant la publication : le critère 2 la lit.
- Commit `release: Chronos 3.2.0 - milestone v1.7 << Lue ou non lue >>` avec le compte de tests et la taille
  de l'exe ; tag local `milestone-v1.7` à la clôture du milestone (pas de push sans accord).

### Le constat en production (critère 4) — point de contrôle humain
- Protocole écrit avant d'être joué, trois lignes :
  1. lancer un tour dans une session ⇒ le widget affiche « Réflexion » ;
  2. laisser un tour se terminer dans une session NON regardée (autre session au premier plan) ⇒
     « En attente » ; l'ouvrir ⇒ elle disparaît sans clic ;
  3. regarder un tour se terminer ⇒ la session disparaît après la grâce (2,5 s après la fin du tour, jusqu'à
     ~5,5 s selon le cycle de 2 s) : mesurer la durée, la faire juger par l'utilisateur, ne pas consigner en
     échec une disparition dans ce délai.
  Plus : un titre de session à la place du dossier ; une question posée (AskUserQuestion ou `blocked`) ⇒
  « En attente » au premier rang.
- Résultat consigné dans `31-CONSTAT.md`, écarts compris (y compris la réserve 16:23 → 19:51 de la phase 27
  si elle se reproduit). Le milestone ne se clôt pas sans ce fichier.

### Claude's Discretion
- Découpage en plans (documentation | release | constat), ordre des tâches de documentation.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

- `docs/publish.md`, `src/Chronos/Chronos.csproj` (l. 16-19), commit `cd26b31` (procédure 3.1.0)
- `docs/hooks-contract.md` + `tests/Chronos.Tests/ContratHooksDocumenteTests.cs` (modèle de garde croisée)
- `src/Chronos/Services/SessionHookInstaller.cs`, `ClaudeSettingsReconciler` (réconciliation au lancement)
- `.planning/phases/27-le-relev-avant-la-r-gle/27-RELEVE.md` (ce qui n'est pas garanti)
- `.planning/RETROSPECTIVE.md` (v1.6 : la validation humaine est une phase)
- Mémoire projet : la version est embarquée dans l'exe ET dans le nom du fichier publié, à chaque release

</canonical_refs>

<specifics>
## Specific Ideas

- Le tableau de l'utilisateur, tel quel, comme grille du constat :
  « session qui réfléchit → Réflexion ; finie ou question, non lue → En attente ; finie et lue → rien ».

</specifics>

<deferred>
## Deferred Ideas

- Signature de l'exe (SmartScreen) — hors périmètre depuis v1.0.
- Push du tag et release GitHub — sur demande explicite de l'utilisateur.

</deferred>

---

*Phase: 31-crit-publi-constat*
*Context gathered: 2026-09-25 — décisions consignées par l'agent en mode autonome*
