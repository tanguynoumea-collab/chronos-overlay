# Phase 6 — UAT humain (validation sur écran réel)

**Statut : EN ATTENTE de validation utilisateur**
Généré le 2026-07-08 à la clôture du plan 06-04 (mode autonome : checkpoint `human-verify` auto-vérifié pour tout ce qui est automatisable ; les critères ci-dessous exigent un écran/interaction réels et ne peuvent PAS être automatisés).

## Contexte

Le plan 06-04 câble le SEUL point d'accès/sortie de l'overlay (menu contextuel clic droit à 4 items),
les commandes MVVM, le dialogue de recalibrage hebdo et la persistance. Les vérifications
programmatiques suivantes ont déjà été confirmées automatiquement :

- `dotnet build Chronos.sln -c Debug` : **0 erreur**.
- `dotnet test Chronos.sln -c Debug` : **106 tests verts** (99 antérieurs + 7 nouveaux commandes/recalibrage), garde de pureté verte.
- Menu : les 4 `MenuItem` (Arrière-plan / Recalibrer le reset hebdo… / Lancer au démarrage / Quitter) présents dans `MainWindow.xaml`, bindés aux 4 `[RelayCommand]` du VM.
- Dialogue : `DatePicker` + « Caler sur maintenant » + Valider/Annuler présents dans `RecalibrationDialog.xaml`.
- Badges « estimée » (5 h + hebdo) intacts dans le cadran.
- Smoke run de l'exe (~8 s) : l'application démarre (« Application started »), aucun crash.
- `%APPDATA%\Chronos\settings.json` présent avec le schéma complet (Corner, MonitorDeviceName, X, Y, Background, RefreshIntervalSeconds, WeeklyAnchor).

## Comment lancer

```
dotnet run --project src/Chronos/Chronos.csproj -c Debug
```

## Critères à valider manuellement (écran réel requis)

| # | Exigence | Étape de vérification | Résultat attendu | Statut |
|---|----------|-----------------------|------------------|--------|
| 1 | FEN-02/03 | Glisser l'overlay (clic gauche maintenu) puis relâcher, tester les 4 coins | Accroche au coin d'écran LE PLUS PROCHE, sans chevaucher la barre des tâches (marge ~12 px) | ⬜ |
| 2 | FEN-04 | Sur 2e moniteur (idéalement DPI différent), glisser l'overlay dessus et relâcher | Snap correct sur le coin du BON moniteur, sans décalage ni flou (si 1 seul écran : noter « non testé ») | ⬜ |
| 3 | FEN-06 | Clic DROIT sur le cadran | Menu à 4 items : Arrière-plan / Recalibrer le reset hebdo… / Lancer au démarrage / Quitter | ⬜ |
| 4 | FEN-06 | Cliquer « Quitter » | L'application se ferme (seul point de sortie) | ⬜ |
| 5 | FEN-05 | Cliquer « Arrière-plan » puis re-cocher | Passe derrière les autres fenêtres puis revient au premier plan (topmost réaffirmé), sans clignotement ni vol de focus | ⬜ |
| 6 | FEN-07 | Déplacer à un coin, activer « Arrière-plan », Quitter, relancer | Réapparaît au même coin/écran et dans le même mode ; settings.json reflète Corner/MonitorDeviceName/Background | ⬜ |
| 7 | ROB-03 | Fenêtre hebdo en repli (badge « estimée ») → « Recalibrer le reset hebdo… » → choisir une date ou « Caler sur maintenant » | L'arc/compte à rebours hebdo se recale MAIS garde le badge « estimée » (si source hebdo Exacte : le recalibrage ne change RIEN) | ⬜ |
| 8 | DEP-02 | Cocher « Lancer au démarrage » | `Chronos.lnk` apparaît dans `shell:startup` (`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`) | ⬜ |
| 9 | DEP-02 | Décocher « Lancer au démarrage » | Le raccourci `Chronos.lnk` disparaît | ⬜ |
| 10 | DEP-02 | (Optionnel) Redémarrer Windows avec l'autostart activé | L'overlay se lance automatiquement au boot | ⬜ |

## Consigne en cas d'écart

Toute anomalie constatée est consignée comme **gap** pour un plan de clôture (`--gaps`) ;
ne pas « corriger en douce » sans replanifier (décision verrouillée du plan 06-04).
