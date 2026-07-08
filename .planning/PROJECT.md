# Chronos

## What This Is

Chronos est un overlay Windows always-on-top en forme d'horloge, posé sur le bureau,
qui affiche en temps réel — d'un coup d'œil — l'état des limites d'usage Claude
(fenêtre 5 h glissante + fenêtre hebdomadaire) pour Claude Code et Cowork.
Le cadran encode deux variables par anneau : longueur d'arc = temps restant avant reset,
couleur = pourcentage de quota consommé. Pour un utilisateur intensif de Claude qui veut
savoir sans y penser combien de marge il lui reste avant d'être bloqué.

## Core Value

Voir instantanément, sans ouvrir de terminal ni taper `/usage`, combien de quota et de
temps il reste sur les deux fenêtres — et ne jamais présenter une estimation comme un
chiffre exact.

## Requirements

### Validated

<!-- Shipped and confirmed valuable. -->

(None yet — ship to validate)

### Active

<!-- Current scope. Building toward these. -->

- [ ] Fenêtre WPF borderless, transparente, always-on-top, sans barre des tâches, déplaçable
- [ ] Cadran circulaire sombre avec graduations (ticks mineurs/majeurs) rendu en XAML pur
- [ ] Arc extérieur = fenêtre 5 h : longueur liée au temps restant avant reset
- [ ] Arc intérieur = fenêtre hebdo : longueur liée au temps restant avant reset
- [ ] Couleur des arcs = utilization (vert → ambre → rouge), gris si utilization ≥ 1 (épuisé)
- [ ] Compte à rebours texte central des deux fenêtres (reset 5 h + reset hebdo)
- [ ] Abstraction IUsageProvider (sources interchangeables sans toucher au cadran)
- [ ] Provider primaire : objet d'usage Claude Code (five_hour/seven_day : utilization + resets_at)
- [ ] Provider de repli : estimation par tokens des transcripts JSONL, toujours marquée « estimée »
- [ ] Provider composite : primaire puis repli automatique
- [ ] Rafraîchissement sur écriture des sources (FileSystemWatcher) + périodique (PeriodicTimer)
- [ ] Tick 1 s (DispatcherTimer) mettant à jour la longueur des arcs et le compte à rebours
- [ ] Déplacement par glisser + accroche automatique au coin d'écran le plus proche (multi-écrans)
- [ ] Bouton « arrière-plan » basculant Topmost / renvoyant la fenêtre au fond
- [ ] Persistance position/coin/réglages dans %APPDATA%/Chronos/settings.json
- [ ] État « données indisponibles » sans crash quand aucune source n'est lisible
- [ ] Reset hebdo best-effort et recalibrable par l'utilisateur
- [ ] Publication exe self-contained mono-fichier (win-x64, PublishSingleFile)
- [ ] Lancement au démarrage Windows via raccourci shell:startup

### Out of Scope

<!-- Explicit boundaries. Includes reasoning to prevent re-adding. -->

- Source de données Cowork séparée — le pool est partagé au niveau du compte, donc l'objet d'usage de Code inclut déjà Cowork
- Notifications Windows / toasts — l'alerte est purement visuelle (couleur + grisé)
- Dépendances de rendu natives (SkiaSharp, etc.) — arcs en XAML pur (Path/ArcSegment)
- Droits administrateur / modifications système — chemins sous profil utilisateur uniquement
- Comptage de tokens présenté comme exact contre des plafonds publiés — plafonds non documentés et mouvants
- ClickOnce / SharePoint — déploiement exe mono-fichier uniquement
- Bande d'activité des sous-agents (blocs Task JSONL) — optionnelle, différée après le cœur fonctionnel

## Context

- **Écosystème :** app desktop Windows mono-utilisateur, .NET 8 / WPF / MVVM. SDK .NET 10 installé sur la machine, cible net8.0.
- **Sources non documentées :** il n'existe aucune API publique pour ces données. On lit des sources locales et non documentées, d'où l'abstraction IUsageProvider — si une source casse à une MAJ de l'app Claude, on remplace le provider sans toucher au cadran.
- **Emplacement de l'objet d'usage à découvrir :** les champs five_hour/seven_day (utilization + resets_at) alimentent la commande `/usage`. Leur mécanisme d'obtention exact est une tâche de découverte à faire en tout début de projet, documentée dans docs/data-sources.md, AVANT de coder les providers.
- **Repli JSONL :** %USERPROFILE%\.claude\projects\**\*.jsonl — somme des tokens dans la fenêtre → estimation, toujours marquée comme telle dans l'UI.
- **Sous-agents (secondaire) :** blocs Task (tool_use) parsés dans les JSONL en direct, pour une bande d'activité optionnelle.
- **Reset hebdo dérivant :** le reset « 7 jours » dérive en pratique (~72 h à un horaire d'ancrage fixe non documenté). Afficher resets_at tel que fourni, traiter le compte à rebours hebdo comme best-effort, le rendre facile à recalibrer.
- **Plafonds mouvants :** ×2 le 6 mai, +50 % hebdo jusqu'au 13 juillet 2026 — raison de privilégier utilization/resets_at sur le comptage de tokens.

### Tokens de design (validés sur maquette)

- Fond cadran `#16151B`, rim `#2C2B34`
- Ticks : mineurs `#34333D`, majeurs `#46454F`
- Arc 5 h (piste) `#2A2932`, arc hebdo (piste) `#26252E`
- Rampe utilization : vert `#7BB13C` (0 %) → ambre `#EFA23A` → rouge `#D8503A` (~100 %) → gris `#5A5960` (épuisé)
- Texte principal `#F4F2EC`, secondaire `#A9A8B2` / `#C7C6D0`

## Constraints

- **Tech stack**: C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm) + Microsoft.Extensions.DependencyInjection — imposé.
- **Rendu**: arcs en XAML pur (Path/ArcSegment), aucune dépendance native — portabilité et simplicité de packaging.
- **Fenêtre**: WindowStyle=None, AllowsTransparency=True, Topmost=True, ShowInTaskbar=False — comportement overlay exigé.
- **Chemins**: uniquement sous %USERPROFILE% / %APPDATA%, aucun droit admin — contrainte de sécurité/déploiement.
- **Honnêteté des chiffres**: utilization/resets_at prioritaires ; ne jamais présenter une estimation comme exacte — confiance utilisateur.
- **Robustesse**: aucune source disponible ≠ crash → état « données indisponibles » ; parsing tolérant (lignes/champs invalides ignorés).
- **Langue**: UI et commentaires en français.
- **Déploiement**: exe self-contained mono-fichier win-x64 + autostart shell:startup, sans ClickOnce.

## Key Decisions

<!-- Decisions that constrain future work. Add throughout project lifecycle. -->

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Abstraction IUsageProvider entre sources et cadran | Sources locales non documentées susceptibles de casser ; isoler le point de rupture | — Pending |
| Objet d'usage (utilization/resets_at) en source primaire, JSONL en repli | Chiffres fiables prioritaires sur estimation par tokens | — Pending |
| Découverte de source (docs/data-sources.md) avant de coder les providers | Tout le pipeline données en dépend | — Pending |
| Rendu des arcs en XAML pur | Éviter dépendance native, simplifier le packaging mono-fichier | — Pending |
| Pas de source Cowork séparée | Pool partagé compte : Cowork déjà inclus dans l'usage de Code | — Pending |
| Reset hebdo traité comme best-effort recalibrable | Le reset 7 jours dérive (~72 h, ancrage non documenté) | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd:transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-07-08 after initialization*
