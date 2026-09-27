<p align="center">
  <img src="docs/logo.png" alt="Chronos" width="128" height="128">
</p>

<h1 align="center">Chronos</h1>

**Overlay Windows en forme d'horloge qui affiche, d'un coup d'œil, l'état de tes limites d'usage Claude** (fenêtre 5 h + fenêtre hebdomadaire) — pour Claude Code et Cowork.

Un petit cadran semi-transparent, toujours au premier plan, posé sur ton bureau. Trois anneaux concentriques, un compte à rebours, des couleurs qui passent du vert au rouge à mesure que tu consommes ton quota.

<!-- Astuce : remplace ce lien par une vraie capture une fois la release publiée -->
<!-- ![Chronos](docs/screenshot.png) -->

## Ce que montre le cadran

- **Anneau interne — fenêtre hebdomadaire** : se remplit à l'approche du reset ; couleur = % de quota consommé.
- **Anneau du milieu — fenêtre 5 h glissante** : idem pour la fenêtre de 5 heures.
- **Anneau externe — timeline 24 h** : où tu en es dans la journée, avec des marques à chaque reset 5 h.
- **Au centre** : les deux pourcentages d'utilisation. Un **clic** au centre bascule entre les pourcentages et le **temps avant reset** (après le délai de double-clic de Windows, ≈ 0,5 s) ; un **double-clic** ouvre l'**Historique** (voir [plus bas](#historique-dutilisation)).
- **Couleurs** : vert → ambre → rouge selon l'utilisation, **gris** quand le quota est épuisé, **neutre** quand la donnée est inconnue (jamais de valeur inventée). Un `~` devant un pourcentage signale une **estimation**.

## Installation (portable, sans droits admin)

1. Télécharge **`Chronos.exe`** depuis la [dernière release](../../releases/latest).
2. Double-clique dessus. C'est tout — pas de .NET à installer, pas de droits administrateur, rien n'est écrit hors de ton profil utilisateur.
3. Windows SmartScreen affichera peut-être « Éditeur inconnu » (l'exe n'est pas signé) : clique **Informations complémentaires → Exécuter quand même**.

L'overlay apparaît dans un coin de l'écran. **Clic droit** dessus pour le menu :

- **Arrière-plan** — bascule l'overlay au fond / au premier plan.
- **Recalibrer le reset hebdo…** — cale la date de reset hebdomadaire (utile en mode estimation).
- **Calibrer les plafonds…** — renseigne tes plafonds de tokens pour colorer les arcs en mode estimation.
- **Lancer au démarrage** — ajoute/retire un raccourci dans le dossier Démarrage de Windows.
- **Usage exact (OAuth)** — active/désactive la récupération des chiffres exacts (voir ci-dessous).
- **Quitter**.

Déplace l'overlay en le **glissant** par les anneaux ; il s'accroche au coin d'écran le plus proche (multi-écrans géré).

## D'où viennent les chiffres

Chronos lit **uniquement des sources locales**, dans ton profil utilisateur — il n'existe pas d'API publique pour ces données. Trois sources, en cascade :

1. **Exact (OAuth)** — Chronos rejoue l'appel `/api/oauth/usage` que fait l'app bureau Claude pour son `/usage`, ce qui donne les **pourcentages exacts** des deux fenêtres, automatiquement.
2. **Pont statusLine** — si tu utilises Claude Code en terminal, un petit pont peut matérialiser le bloc `rate_limits` de la statusLine dans un fichier local.
3. **Estimation (repli)** — à défaut, Chronos estime l'usage à partir des transcripts JSONL (`~/.claude/projects`). Ces valeurs sont **toujours marquées « estimée »** (`~`).

### À propos du token (transparence)

Pour la source **exacte**, Chronos doit lire ton token OAuth Claude, que l'app bureau stocke **chiffré** (safeStorage/DPAPI) sur ta machine. Chronos le **déchiffre en mémoire uniquement**, l'utilise **exclusivement** dans l'en-tête `Authorization` vers `api.anthropic.com`, et **ne le stocke jamais, ne le journalise jamais, ne l'envoie nulle part ailleurs**. Le coffre est lu en **lecture seule**. Tu peux **désactiver** complètement cet accès via le menu **« Usage exact (OAuth) »** : Chronos se rabat alors sur l'estimation sans jamais toucher au token.

L'endpoint `/api/oauth/usage` n'est pas documenté publiquement : il peut changer à une mise à jour de Claude. En cas d'échec, Chronos bascule proprement sur l'estimation (jamais de plantage).

## Widget de sessions Claude Code

Un second petit panneau, à côté du cadran, dit **quelle session Claude Code t'attend** — dans l'app bureau Claude
comme en terminal. Active-le dans les réglages (clic droit sur le cadran) : **« Widget sessions Claude Code »**.
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
définitivement »**. Réglages → **« Diagnostic… »** dit, pour chaque session masquée, pourquoi.

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

- Deux gestes : **double-clic au centre du cadran**, ou **Réglages** (clic droit sur le cadran) → carte **« Historique
  d'utilisation »** → **« Ouvrir »**.
- Si la fenêtre est déjà ouverte, elle revient au premier plan (et se rouvre si elle était réduite). **Échap** ou
  **✕** la ferment.
- Le prix du double-clic : au centre, le simple clic attend le délai de double-clic de Windows (≈ 0,5 s) avant de
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

### Trois styles pour la Semaine

Le style se choisit dans la fenêtre (« Style : Pistes · Simplifié · Tuiles ») ou dans la carte des réglages : c'est
le même réglage.

| Style | Pistes affichées |
|---|---|
| **Pistes** (par défaut) | Niveau · Rythme · Tokens Claude Code · Couverture |
| **Simplifié** | Niveau · Tokens Claude Code · Couverture |
| **Tuiles** | Niveau · Fenêtres 5 h · Rythme · Tokens Claude Code · Couverture |

- **Niveau** — le % hebdo en escalier (couleur = niveau), la semaine précédente en gris pointillé ; en Pistes et
  Simplifié, le % 5 h en dents de scie et un tiret à chaque reset 5 h.
- **Fenêtres 5 h** — une tuile par fenêtre, du premier relevé au reset 5 h annoncé ; hauteur = % 5 h le plus haut,
  grise quand la fenêtre est « épuisée ».
- **Rythme** — par heure, de combien le compteur 5 h a monté entre deux relevés (jamais à travers un reset).
- **Tokens Claude Code** — par heure, principal et sous-agents empilés, sur leur propre axe.
- **Couverture** — une bande toujours visible : relevé présent, « Chronos arrêté », « jeton invalide » ou « sonde
  refusée ».

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
- La carte des réglages et **« Diagnostic… »** (section « Journal d'historique ») disent la **dernière écriture** du
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
dotnet publish src/Chronos -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
# → src/Chronos/bin/Release/net8.0-windows/win-x64/publish/Chronos.exe
```

Détails de publication dans [`docs/publish.md`](docs/publish.md). Contrat des sources de données dans [`docs/data-sources.md`](docs/data-sources.md).

## Stack

C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm) · rendu du cadran en XAML pur (aucune dépendance native) · exe self-contained mono-fichier. plus de 1 600 tests unitaires.

## Licence

[MIT](LICENSE).
