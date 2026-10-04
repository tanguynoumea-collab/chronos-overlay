# Phase 43 — Release 3.5.0 et constat — CONTEXT

Go utilisateur (checkpoint 3 ZEUS) le 2026-10-04 : « Oui, lancer la phase 43 » ; DS3-02 : « Corriger et documenter ».
Sources : ROADMAP.md §Phase 43 (critères 1-4, VAL-06/VAL-07), `.zeus/reports/dev-senior-3.md` (DS3-01, DS3-02, verdict
publiable après phase 43), `.zeus/reports/dev-senior-2.md` (DS-MAINT-03 reliquat `docs/publish.md` §5),
`.zeus/reports/cycle2/council/reverif-securite-packaging.md` (PKG-7 reliquat `README.md:199,206`, `Chronos.csproj:15` ;
PKG-R2 convergence de l'autostart non documentée, cible non affichée au diagnostic), `.zeus/DOD-CYCLE2.md`.

## Décisions verrouillées

### D-01 — DS3-02 (code, en premier)
- `TranscriptActivityProvider` (~l.233-246, `EnumerateJsonl`) : une racine des transcripts PRÉSENTE mais INACCESSIBLE
  (UnauthorizedAccessException / IOException, y compris levée pendant l'itération paresseuse d'`EnumerateFiles`) fait échouer
  la passe → exception → journal nul → « Indisponible » (même chemin que DS2-02, 42.4-02). Une racine ABSENTE reste « aucune
  activité » (comportement actuel, cas normal d'une machine sans Claude Code) — hypothèse documentée (D-02).
- Corriger le commentaire faux « jamais d'exception ».
- Ne PAS ajouter la lecture de `CLAUDE_CONFIG_DIR` (hors périmètre → roadmap) ; la documenter comme limite.
- Tests sous `Path.GetTempPath()` : racine inaccessible (dossier dont l'énumération lève — injection si l'ACL n'est pas
  manipulable sans droits admin), racine absente → journal vide sans exception ; doctrine : pas de provenance exacte.

### D-02 — Documentation des limites (data-sources §4)
- DS3-01 : reconnexion par la pastille avec un AUTRE compte → l'ancien relevé peut rester « exact » ≤ ~5 min.
- DS3-02 : transcripts hors de `~/.claude/projects` (ex. `CLAUDE_CONFIG_DIR`) ou racine absente → l'activité est invisible,
  « encore valide » peut tenir jusqu'au reset ; racine inaccessible → « Indisponible ».
- Gardes documentaires existantes à garder vertes (GardesReliquatsTests, gardes de doc).

### D-03 — Version 3.5.0 (VAL-06)
- Les 4 propriétés du csproj (`Version`, `FileVersion`, `AssemblyVersion`, `InformationalVersion`) → 3.5.0 / 3.5.0.0 ; corriger
  le commentaire `Chronos.csproj:15` (nom du fichier publié `Chronos-vX.Y.Z.exe`). Nom publié : `Chronos-v3.5.0.exe`.
- Mémoire projet : version embarquée + dans le nom du fichier à chaque release.

### D-04 — Documentation 3.5 (VAL-06)
- README : cadrans et orientations, plein écran Historique, thèmes en trois groupes, « D'où viennent les chiffres » (chaîne
  unique sonde + secours OAuth, tête « dernier relevé exact », plancher « ≥ » seul non exact), commande de publication avec le
  nom versionné (corrige `README.md:199,206`), compte de tests à jour (« plus de 2 200 »).
- `docs/publish.md` : §2 commande et nom versionné ; §5 l'autostart se REPOINTE seul vers l'exe courant au démarrage (sauf build
  sous `bin\` ou `%TEMP%`, ou cible plus récente — PKG-R2) ; §7 « Premier lancement de la 3.5.0 » (retrait de la barre statusLine,
  sauvegarde, hooks repointés) ; empreinte SHA-256 à publier avec l'exe ; tags `exe-vX.Y.Z` (convention, posés par l'utilisateur).
- `docs/data-sources.md` à jour (D-02 + chaîne).

### D-05 — Construction de l'exe (VAL-06)
- `dotnet publish` canonique (docs/publish.md §2) → copie renommée `Chronos-v3.5.0.exe` (emplacement documenté dans publish.md),
  vérification de la FileVersion embarquée (lecture de métadonnées, sans lancer), SHA-256 calculé et écrit dans le SUMMARY.
- Contrôles et smoke `--hook` de la procédure 32-07 : UNIQUEMENT ceux qui n'exécutent pas l'overlay. Si un smoke exige de lancer
  l'exe, il est transféré au constat utilisateur (D-06) — l'agent ne lance JAMAIS l'exe, même avec `--hook`.
- Suite verte, 0 avertissement Debug et Release. Commit de release SANS tag NI push.

### D-06 — Constat utilisateur (VAL-07), plan `autonomous: false`
- Rédiger `43-CONSTAT.md` (protocole à cocher) reprenant le critère 4 de la ROADMAP + 32-CONSTAT (E1-ter : quitter tous les
  exécutables à la main d'abord) + 35-CONSTAT + 38/42-HUMAN-UAT : 8 variantes × 4 coins à 100 % et 150 %, gestes en clics réels
  sur toute la silhouette et clic qui traverse dehors, Braises, plein écran (Échap 2 niveaux, F11), 15 thèmes / 3 groupes,
  Alt+F4 termine le processus, `--zzz` sort en silence, barre statusLine retirée de `~/.claude/settings.json` après lancement par
  l'Explorateur (relevé par sonde WMI hors arbre, jamais depuis une session Claude Code), sauvegarde présente, hooks repointés,
  déconnexion → « données indisponibles ».
- Point de contrôle humain : l'utilisateur exécute et rend son verdict ; l'orchestrateur consigne les écarts.

## Hors périmètre
Publication GitHub (push, tag, release) : seulement après le constat ET un accord explicite séparé de l'utilisateur.
`CLAUDE_CONFIG_DIR`, DS3-03/04/05, DS2-04/05, findings reportés → roadmap.

## Contraintes permanentes
Ne jamais lancer l'exe ; tests sous `Path.GetTempPath()` ; jamais le vrai `~/.claude/settings.json`, `%APPDATA%\Chronos`,
`shell:startup`, `oauth.dat` ; jamais de secret affiché ; UI/commentaires en français ; commits terminés par
`Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
