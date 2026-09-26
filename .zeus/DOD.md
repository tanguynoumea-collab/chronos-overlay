# Definition of Done — Chronos (cycle ZEUS n°1 : trackeur d'utilisation, v1.8 / exe 3.3.0)

> Instanciée à l'initialisation ZEUS le 2026-09-26, puis STABLE (toute modification est une décision
> explicite, consignée dans `.zeus/state.json`). C'est le SEUL critère de sortie de la phase GSD.

## Socle (tout projet)

- [ ] Toutes les tâches GSD du cycle (phases du milestone v1.8) sont `done`, VERIFICATION.md passée
- [ ] `dotnet build -c Release` propre sur `net8.0-windows` (solution `Chronos.sln`)
- [ ] Tests unitaires : 100 % verts (`tests/Chronos.Tests`, ≥ 1200 tests, aucun test retiré sans décision)
- [ ] Zéro warning nouveau à la compilation
- [ ] Aucune valeur magique UI : couleurs/tailles/épaisseurs via `src/Chronos/Resources/DesignTokens.xaml`
- [ ] Aucun blocage du thread UI : lecture des transcripts et du journal hors UI, marshaling unique
- [ ] Chaînes visibles en français, vocabulaire du DESIGN_PLAN respecté

## Spécifique Chronos

- [ ] Honnêteté : jamais un pourcentage qui n'est pas un relevé exact ; les trous du journal restent des trous
      (pas d'interpolation présentée comme donnée) ; les tokens des transcripts sont annoncés comme
      « Claude Code seulement », jamais convertis en pourcentage de forfait
- [ ] Chemins sous `%APPDATA%\Chronos` uniquement ; écriture atomique (temp + renommage) ; lecture tolérante
      (fichier absent / ligne invalide / version inconnue → ignoré, jamais de crash)
- [ ] Lecture seule stricte de `~/.claude` et de `%APPDATA%\Claude` ; aucun appel réseau supplémentaire
- [ ] Rétention bornée du journal (taille et durée), documentée dans `docs/data-sources.md`
- [ ] Exe self-contained mono-fichier publié `Chronos-v3.3.0.exe`, version embarquée (`csproj` × 4),
      réconciliation hooks/statusLine au premier lancement constatée dans `~/.claude/settings.json`

## Par fonctionnalité du cycle

| Fonctionnalité | Critère d'acceptation observable | Fait |
|---|---|---|
| Journal des relevés exacts | Après 1 h d'overlay ouvert, le journal contient des relevés 5 h et hebdo horodatés, sans doublon inutile, et survit à un redémarrage de l'exe | ☐ |
| Journal des tokens (transcripts) | L'historique des tokens par tranche est reconstruit depuis les transcripts existants, puis mis à jour incrémentalement ; une purge des transcripts par Claude Code ne fait pas disparaître l'historique déjà journalisé | ☐ |
| Vue « semaine de forfait » | Une fenêtre affiche la semaine courante (samedi 00:00 → samedi 00:00) avec la courbe hebdo et les dents de scie 5 h superposées, navigation vers les semaines passées | ☐ |
| Lecture des rythmes | L'utilisateur peut voir à quelles heures / quels jours il consomme le plus, sur plusieurs semaines | ☐ |
| Accès | Ouverture depuis le menu contextuel de l'overlay, sans casser le cadran ni le widget de sessions | ☐ |
