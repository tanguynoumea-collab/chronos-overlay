<p align="center">
  <img src="docs/logo.png" alt="Chronos" width="128" height="128">
</p>

<h1 align="center">Chronos</h1>

**Overlay Windows en forme d'horloge qui affiche, d'un coup d'œil, l'état de tes limites d'usage Claude** (fenêtre 5 h + fenêtre hebdomadaire) — pour Claude Code et Cowork.

Un petit cadran semi-transparent, toujours au premier plan, posé sur ton bureau. Cinq formes au choix (des anneaux concentriques aux volets d'un afficheur), un compte à rebours, des couleurs qui passent du vert au rouge à mesure que tu consommes ton quota.

<!-- Astuce : remplace ce lien par une vraie capture une fois la release publiée -->
<!-- ![Chronos](docs/screenshot.png) -->

## Ce que montre le cadran

Quelle que soit sa forme, le cadran dit la même chose pour les deux fenêtres (5 h glissante et hebdomadaire) : la
**partie remplie** dit le temps déjà écoulé de la fenêtre (vide au début, pleine au reset), la **couleur** dit le %
de quota consommé.

### Cinq cadrans, huit variantes

Réglages → **Apparence** → style du cadran :

- **Anneaux** (défaut) — en mode **Normal** (défaut), deux anneaux : la fenêtre hebdomadaire à l'intérieur et une
  timeline 24 h colorée par l'usage 5 h, avec un sous-tiret par heure et une marque à chaque reset 5 h. En mode
  **Étendu** (carte « Mode étendu »), trois anneaux : hebdo, 5 h, timeline 24 h. **Au centre** : les deux pourcentages.
- **Braises** — deux couronnes de braises. L'anneau extérieur est ta journée : une braise par heure, minuit en haut,
  groupée par tranches de 5 h ; les heures passées sont allumées (l'heure en cours en demi-lueur), dans la couleur du
  quota 5 h. Seul le reset en cours vient du serveur, les autres tranches sont projetées de 5 h en 5 h. Sans reset 5 h
  connu, les 24 braises sont régulières, sans tranches.
  L'anneau intérieur est la fenêtre hebdomadaire en 14 braises, un groupe par jour (une braise par demi-journée) : elles
  s'allument depuis midi, dans le sens horaire, au fil du temps écoulé, et l'anneau est plein au reset. En mode temps, le
  centre ajoute l'heure locale du reset 5 h (« ↻ HH:MM »).
- **Fusible** — une mèche dont le cordon avance avec le temps écoulé (depuis la gauche, ou depuis le bas à la verticale).
- **Marée** — une bande que l'eau gagne : la marée monte avec le temps écoulé (depuis la gauche, ou depuis le bas).
- **Volets** — une plaque chiffrée et une rangée de volets, comme un afficheur de gare ; les volets s'allument avec le
  temps écoulé (depuis la gauche, ou depuis le bas).

Fusible, Marée et Volets se posent à l'horizontale ou à la verticale (carte **« Orientation »** : Horizontal ·
Vertical), et chaque cadran retient la sienne (Marée est verticale par défaut, les deux autres horizontaux). Avec les
deux modes d'Anneaux, cela fait huit variantes. La fenêtre prend la taille réelle du cadran choisi et reste collée à
son coin d'écran quand on en change.

### Lire le cadran

- **Gestes, sur tout le cadran** : un **clic** bascule entre les pourcentages et le **temps avant reset** (après le délai de double-clic de Windows, ≈ 0,5 s) ; un **double-clic** ouvre l'**Historique** (voir [plus bas](#historique-dutilisation)) ; **appuyer puis glisser** déplace le cadran, qui s'accroche ensuite au coin le plus proche ; **clic droit** ouvre les **Réglages**. À côté du cadran, le clic va au bureau.
- **Couleurs** : vert → ambre → rouge selon l'utilisation, **gris** quand le quota est épuisé, **neutre** quand la donnée est inconnue (jamais de valeur inventée). Un `≥` devant un pourcentage signale un **plancher** : le dernier relevé exact a vieilli pendant que Claude Code travaillait — le vrai chiffre est au moins celui-là.

## Installation (portable, sans droits admin)

1. Télécharge **`Chronos-v3.5.0.exe`** depuis la [dernière release](../../releases/latest).
2. Vérifie son empreinte **SHA-256**, publiée avec la release : dans PowerShell,
   `Get-FileHash .\Chronos-v3.5.0.exe -Algorithm SHA256` doit rendre la même valeur.
3. Double-clique dessus depuis l'Explorateur. C'est tout — pas de .NET à installer, pas de droits administrateur, rien n'est écrit hors de ton profil utilisateur.
4. Windows SmartScreen affichera peut-être « Éditeur inconnu » (l'exe n'est pas signé) : clique **Informations complémentaires → Exécuter quand même**.

L'overlay apparaît dans un coin de l'écran. **Clic droit** dessus ouvre les **Réglages** : une fenêtre classique,
redimensionnable (bords et poignée ◢), agrandissable, présente dans la barre des tâches, qui retient sa taille, sa
position et la dernière section ouverte. Elle ne se ferme plus quand tu cliques ailleurs : **Échap** ou **✕** la
ferment, et un second clic droit la ramène au premier plan. Six sections dans le rail de gauche (clic, **↑ / ↓**,
**Ctrl+1…6**) :

- **Données** — connexion à Claude, sonde d'en-têtes (son coût est écrit).
- **Historique** — ouvre la fenêtre Historique.
- **Apparence** — style du cadran (5), avec un **aperçu en direct** du vrai cadran ; **Orientation** (Fusible, Marée,
  Volets) ; mode étendu (Anneaux) ; thème (15, en 3 groupes Pâle · Classique · Vive).
- **Sessions** — widget des sessions Claude Code, son style (8) avec aperçu, disposition verticale.
- **Comportement** — arrière-plan, lancer au démarrage.
- **Diagnostic** — ce que Chronos voit en ce moment, dans la fenêtre : **↻ Actualiser**, **⧉ Copier**.

**« ⏻ Quitter Chronos »** est isolé en bas du rail.

Déplace l'overlay en le **glissant** depuis n'importe quel point de sa silhouette ; il s'accroche au coin d'écran le plus proche (multi-écrans géré).

## D'où viennent les chiffres

Une seule chaîne — la sonde d'en-têtes d'abord, son secours ensuite —, décrite en détail dans [`docs/data-sources.md`](docs/data-sources.md) :

1. **Sonde d'en-têtes** (source principale) — au plus une micro-requête toutes les 5 minutes sur ton compte ; les
   pourcentages exacts des deux fenêtres, leurs resets et le statut déclaré par le serveur arrivent dans les en-têtes de
   la réponse. Elle répond même quand l'API sature. Désactivable dans les réglages (section **Données**), son coût est
   écrit à côté.
2. **Secours OAuth du login Chronos** — si la sonde n'a pas de chiffre pour une fenêtre, Chronos interroge
   `/api/oauth/usage` avec son propre login (« Se connecter à Claude »).
3. **Dernier exact** — le dernier relevé exact est gardé sur disque : il reste affiché tel quel tant qu'il est frais
   (quelques minutes) ou que Claude Code n'a pas travaillé depuis.
4. **Plancher** — si Claude Code a travaillé depuis ce relevé, le chiffre devient un plancher **« ≥ X % »** : c'est le
   **seul chiffre non exact** que Chronos affiche. Sans aucun relevé, la fenêtre est dite indisponible — jamais une valeur
   inventée.

Les resets viennent toujours du serveur. Les transcripts locaux (`~/.claude/projects`) ne servent qu'à savoir si Claude
Code a travaillé et à l'Historique : jamais à fabriquer un pourcentage.

Limites connues (détail au §4 de [`docs/data-sources.md`](docs/data-sources.md)) : se reconnecter par la pastille avec
un **autre compte** peut laisser l'ancien relevé affiché comme exact quelques minutes ; des transcripts écrits **hors de
`~/.claude/projects`** (par exemple via `CLAUDE_CONFIG_DIR`) rendent l'activité invisible, et un relevé vieilli peut
alors rester « exact » jusqu'au reset.

### À propos du token (transparence)

Chronos a **son propre login** : le jeton obtenu par « Se connecter à Claude » est propre à Chronos, **chiffré par DPAPI**
(lisible par ton seul compte Windows) dans `%APPDATA%\Chronos\oauth.dat`. Il ne sert **qu'à** l'en-tête `Authorization`
vers `api.anthropic.com` (sonde et secours), **n'est jamais écrit** dans un journal ni dans le diagnostic, et n'est envoyé
nulle part ailleurs. **Aucun autre coffre n'est lu** : ni celui de l'app bureau Claude, ni le gestionnaire d'identifiants
Windows.

La famille d'en-têtes de la sonde et l'endpoint `/api/oauth/usage` ne sont pas documentés publiquement : ils peuvent
changer à une mise à jour de Claude. En cas d'échec, la fenêtre passe à « indisponible » (jamais de plantage), et le
diagnostic dit ce qui a été reçu.

## Widget de sessions Claude Code

Un second petit panneau, à côté du cadran, dit **quelle session Claude Code t'attend** — dans l'app bureau Claude
comme en terminal. Active-le dans les réglages (clic droit sur le cadran → **Sessions**) : **« Widget sessions Claude Code »**.
Chronos inscrit alors 8 hooks dans `~/.claude/settings.json`, sans toucher à ceux des autres outils, et les tient à
jour à chaque lancement (le fichier est sauvegardé avant toute réécriture) ; seules les sessions ouvertes ensuite sont
suivies.

Chaque session tient en un mot :

- **« Réflexion »** — la session travaille.
- **« En attente »** — elle a fini son tour, demande une permission ou te pose une question, et tu ne l'as pas encore lue.
- **« En attente ? »** — plus rien n'arrive depuis 20 minutes alors qu'elle travaillait : l'attente est **déduite**,
  d'où le point d'interrogation.

Les questions et les permissions passent devant les tours finis, puis les attentes déduites, puis « Réflexion ».

**Une session lue disparaît, sans clic.** Ouvre-la dans l'app après la fin de son tour, ou regarde-la finir (fenêtre
Claude au premier plan) : elle quitte le widget au bout de quelques secondes. Elle revient si elle te redemande
quelque chose, et une session qui se remet à travailler est toujours visible. **Le titre** que l'app donne à la
session remplace le nom du dossier ; le dossier reste dans l'info-bulle.

Clic droit sur une ligne : **« Marquer traitée »** (elle revient si elle te redemande) ou **« Archiver
définitivement »**. Réglages → **Diagnostic** dit, pour chaque session masquée, pourquoi.

Limites connues : une session de terminal sans l'app bureau n'a pas de lecture automatique (elle reste « En attente »
jusqu'à ta réponse, ton geste ou 8 h) ; une session Cowork dans sa machine virtuelle n'est pas détectée ; l'accueil ou
une conversation Chat de l'app au premier plan compte comme « regardée ».

Contrats : [`docs/hooks-contract.md`](docs/hooks-contract.md) (les hooks) et
[`docs/desktop-app-sessions.md`](docs/desktop-app-sessions.md) (ce que l'app bureau écrit de chaque session).

## Historique d'utilisation

Une fenêtre à part, **Historique**, montre ce que le journal de Chronos a relevé : la semaine de forfait, la journée,
les quatre dernières semaines. Ce n'est pas un overlay : c'est une fenêtre ordinaire, présente dans la barre des
tâches, redimensionnable, qui retient sa position et sa taille.

### Ouvrir

- Deux gestes : **double-clic sur le cadran**, ou **Réglages** (clic droit sur le cadran) → section **Historique**
  → carte **« Historique d'utilisation »** → **« Ouvrir »**.
- Si la fenêtre est déjà ouverte, elle revient au premier plan (et se rouvre si elle était réduite). **Échap** ou
  **✕** la ferment.
- Le prix du double-clic : sur le cadran, le simple clic attend le délai de double-clic de Windows (≈ 0,5 s) avant de
  basculer entre % et temps avant reset, pour savoir si un second clic arrive.

### Trois vues

En haut : **Jour · Semaine · 4 semaines**, ‹ › pour la période précédente ou suivante, « Cette semaine » /
« Aujourd'hui » pour revenir au présent, et une ligne de fraîcheur (« Dernier relevé il y a N min · source ·
N relevés · N interruptions · journal ouvert le … »).

- **Semaine de forfait** (vue par défaut) — du samedi 00:00 au samedi 00:00 suivant, bornée par le reset hebdo que
  le serveur annonce.
- **Jour** — de 0 h à 24 h, un relevé toutes les 5 min (« 288 relevés attendus · N présents »). Le % 5 h passe au
  premier plan, avec un trait à chaque reset 5 h observé (« reset 5 h HH:MM ») ; les tokens sont comptés par quart
  d'heure et par modèle. Un trou qui commence la veille est nommé avec sa cause, sans rien dessiner avant minuit.
- **4 semaines** — quatre semaines de forfait superposées sur le même axe samedi → samedi : la courante en couleur,
  les trois précédentes en gris de plus en plus pâle, chacune étiquetée à droite (valeur finale, ou « pas de relevés
  (avant le journal) »). Une semaine épuisée devient un plateau gris « épuisée <jour> HH:MM → bloquée jusqu'au
  reset ». Une bande de couverture par semaine ; pas de piste de tokens dans cette vue.

### Les pistes de la Semaine

Une seule forme, en pistes superposées : Niveau · Rythme · Tokens Claude Code · Couverture.

- **Niveau** — le % hebdo en escalier (couleur = niveau), la semaine précédente en gris pointillé ; le % 5 h en
  dents de scie et un tiret à chaque reset 5 h.
- **Rythme** — par heure, de combien le compteur 5 h a monté entre deux relevés (jamais à travers un reset).
- **Tokens Claude Code** — par heure, principal et sous-agents empilés, sur leur propre axe.
- **Couverture** — une bande toujours visible : relevé présent, « Chronos arrêté », « jeton invalide » ou « sonde
  refusée ».

### Plein écran

Le bouton « ⛶ Plein écran », à droite de la ligne de fraîcheur, ou la touche F11, étend la fenêtre à tout l'écran où elle
se trouve, barre des tâches comprise. Les pistes se partagent la hauteur dans leurs proportions (avec un plafond), textes et
traits grandissent d'environ un tiers, et la vue ne défile plus. Trous, annotations et pied de page restent les mêmes.
On en sort par « ⤢ Quitter le plein écran · Échap », par F11 ou par Échap : la fenêtre retrouve alors sa position et sa
taille d'avant ; un second Échap la ferme.

### Règles d'honnêteté

- Les pourcentages sont des **relevés exacts du serveur**, écrits dans un journal depuis sa première ligne
  (« journal ouvert le … »). Avant, rien n'est dessiné : aucune courbe n'est reconstituée.
- Un **trou** (plus de 10 min sans relevé) interrompt la ligne et porte sa cause : « Chronos arrêté », « jeton
  invalide », « sonde refusée » — ou aucune, quand elle n'est pas connue.
- Ce qui a été consommé pendant une absence est un bloc plat : « +N % pendant l'absence (répartition inconnue) ».
  Jamais une barre au réveil.
- Une marche de % sans tokens Claude Code est encadrée en violet : **consommé ailleurs** (Cowork, claude.ai).
- Les tokens sont comptés localement dans les transcripts de Claude Code : bruts, non pondérés, hors Cowork et
  claude.ai, sur leur propre axe. Ce n'est **jamais** un % du forfait.
- Aucun trou n'est interpolé et rien n'annonce l'avenir. Le reset 5 h et le reset hebdo sont ceux que le serveur
  annonce.

### Le journal

- `%APPDATA%\Chronos\historique\` : relevés dans `releves-AAAA-MM.jsonl`, tokens dans `tokens-AAAA-MM.jsonl`
  (format et lecture : [`docs/data-sources.md`](docs/data-sources.md), §7 à §9).
- La carte des réglages (section Historique) et le **Diagnostic** (bloc « Journal d'historique ») disent la **dernière écriture** du
  journal, avec une alerte au-delà de 15 min sans écriture alors que Chronos tourne.
- Au premier lancement de la 3.3.0, les tokens des transcripts sont reconstruits en arrière-plan, du plus récent au
  plus ancien ; un bandeau dans la fenêtre suit la progression. Les pourcentages, eux, ne se reconstruisent pas : ils
  commencent à l'ouverture du journal.

## Prérequis

- **Windows 10/11 (x64)**.
- Un abonnement Claude **Pro ou Max** (les blocs `rate_limits` ne sont exposés que pour ces offres).
- L'app bureau Claude et/ou Claude Code installés et utilisés (c'est ce qui alimente les sources locales).

## Construire depuis les sources

Prérequis : SDK **.NET 8** (ou ultérieur, capable de cibler `net8.0-windows`).

```bash
# Lancer en dev
dotnet run --project src/Chronos

# Publier l'exe portable mono-fichier (win-x64)
dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
# → src/Chronos/bin/Release/net8.0-windows/win-x64/publish/Chronos.exe
```

La sortie est ensuite copiée sous son nom versionné, à la version des quatre propriétés du csproj : pour cette release,
**`Chronos-v3.5.0.exe`**.

Détails de publication dans [`docs/publish.md`](docs/publish.md). Contrat des sources de données dans [`docs/data-sources.md`](docs/data-sources.md).

## Stack

C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm) · rendu du cadran en XAML pur (aucune dépendance native) · exe self-contained mono-fichier. plus de 2 200 tests unitaires.

## Licence

[MIT](LICENSE).
