# `scripts/` — pont statusLine retiré

Le pont statusLine en Node (`chronos-statusline-bridge.js`), son installeur (`install-bridge.mjs`) et
son échantillon d'entrée (`fixtures/statusline-input.json`) ont été **retirés en 3.5**.

Pourquoi :

- la barre de statut Chronos n'existe plus : depuis la 3.5, Chronos la **retire** de
  `~/.claude/settings.json` au démarrage (avec sauvegarde) ;
- `%APPDATA%\Chronos\usage.json`, que le pont écrivait, n'est plus lu par Chronos.

**Ne pas réinstaller l'ancien pont.** Une barre `node …chronos-statusline-bridge.js` n'est pas
reconnue comme une commande Chronos : elle serait classée « tierce », laissée telle quelle et
jamais retirée par Chronos, sans plus rien alimenter.

Historique des fichiers retirés : `git log -- scripts/`.
