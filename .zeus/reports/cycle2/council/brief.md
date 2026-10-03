# Brief dev-team-council — Chronos, cycle ZEUS 2 (2026-10-03)

Projet : C:\Users\Tanguy\Documents\PROGRAMMES\DEV\PROJET OVERLAY — Chronos, overlay WPF .NET 8 (net8.0-windows),
MVVM CommunityToolkit, DI/Host, ~1950 tests xUnit, exe mono-fichier self-contained. Application desktop mono-utilisateur
locale (pas de web, pas de multi-tenant). Doctrine produit : « exact ou rien » (jamais une estimation présentée comme exacte).

Périmètre prioritaire :
1. Cycle 2 (v1.9) : `git diff v1.8..HEAD -- src tests` (167 fichiers) — purge de la chaîne de données (phase 37), retrait
   de la barre statusLine dans ~/.claude/settings.json (ClaudeSettingsReconciler), lecture tolérante des réglages
   (LectureReglages / SettingsService), garde d'arguments (ArgumentsDemarrage), Historique plein écran, 15 thèmes,
   cadrans à l'échelle 1 + orientations + recalage au coin, Braises, geste unique (AutomateGeste, ZoneGeste, répartiteur
   MainWindow), corrections de revue visuelle (phase 42.1).
2. Cycle 1 (v1.8), jamais audité : `git diff 937e980..v1.8 -- src tests` (journal des relevés, agrégats de tokens,
   reconstruction de fond, fenêtre Historique, verrou mono-instance, arrêt du Host).
Le reste du code est dans le périmètre si utile à votre domaine.

Documents de contexte : CLAUDE.md, docs/data-sources.md, .planning/REQUIREMENTS.md, .zeus/DESIGN_PLAN_CYCLE2.md.
Règles : lecture seule stricte ; ne lancez JAMAIS l'exe Chronos ; n'écrivez rien hors de votre fichier de findings.
Format de finding imposé : §7 de la constitution dev-team-council (ID, Sévérité, Rôle, Localisation, Preuve, Constat,
Impact, Recommandation, Applicabilité, Statut challenge). Pas de preuve → Info.
Écrivez vos findings dans .zeus/reports/cycle2/council/<role>.md (en français) et renvoyez un résumé court.
