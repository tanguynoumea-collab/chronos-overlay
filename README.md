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
- **Au centre** : les deux pourcentages d'utilisation. **Clique au centre** pour basculer vers le **temps avant reset**, et re-clique pour revenir aux pourcentages.
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

C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm) · rendu du cadran en XAML pur (aucune dépendance native) · exe self-contained mono-fichier. 215 tests unitaires.

## Licence

[MIT](LICENSE).
