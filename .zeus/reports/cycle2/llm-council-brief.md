# Brief LLM-COUNCIL — cycle 2 (2026-10-03)

Question : comment implémenter les points R1 à R7 de la roadmap de Chronos dans le code existant ?

Projet : `C:\Users\Tanguy\Documents\PROGRAMMES\DEV\PROJET OVERLAY` — overlay WPF .NET 8 always-on-top (fenêtre layered
transparente), MVVM CommunityToolkit, DI, ~1700 tests xUnit, XAML pur sans dépendance native, UI en français.
Doctrine : « exact ou rien », jamais d'estimation présentée comme exacte. Lecture seule de ~/.claude sauf réconciliation
contrôlée de `~/.claude/settings.json` (hooks + statusLine) déjà existante.

À lire (faits vérifiés, chemin:ligne) :
- `.zeus/ROADMAP-PROCHAINE-VERSION.md` (les demandes R1-R7)
- `.zeus/reports/cycle2/inventaire-cadrans-gestes.md` (R1, R2, R3, R7)
- `.zeus/reports/cycle2/inventaire-purge-donnees.md` (R5)
- `.zeus/reports/cycle2/inventaire-themes-historique.md` (R4, R6)
Tu peux ouvrir le code (`src/Chronos`, `tests/Chronos.Tests`) pour vérifier. Lecture seule : ne modifie rien.

Décisions à trancher :
1. Taille de fenêtre par style (aujourd'hui 170×170 fixe + Viewbox qui annule tout agrandissement) pour rendre R2 visible
   et accueillir les orientations R1 (Fusible / Volets verticaux, Marée horizontale).
2. Architecture de l'orientation : propriété Orientation dans FuseBar / TideColumn / FlapRow vs vues dédiées ; réglage
   propre au cadran ou partagé avec `VerticalLayout` (widget de sessions).
3. Stratégie de hit-testing R7 sur fenêtre layered (pixel alpha 0 = clic traverse) : où déclarer les zones par cadran
   (clic centre/bascule, double-clic Historique, clic droit, drag), comment les tester.
4. Délimitations 5 h de Braises : l'anneau 5 h de Braises représente la fenêtre de 5 h elle-même (16 pastilles), pas
   la journée comme Arcs. Divisions internes horaires vs grille 24 h des resets vs autre. L'utilisateur dit : « en le
   regardant je ne sais pas quand la limite de 5 h se réinitialise ».
5. Plein écran Historique : hauteurs proportionnelles vs dictionnaire de tokens dynamiques vs autre ; Échap.
6. Séquence de purge R5 et migration du pont statusLine (aujourd'hui `~/.claude/settings.json` pointe sur
   `Chronos-v3.4.0.exe --statusline`, et l'exe a un verrou mono-instance).
7. Thèmes : « Pâle » = fonds clairs ou sombres désaturés ? variante de `From()` ; Néon / Aurore n'ont pas de rampe
   vert→rouge ; nouvelles palettes.
8. Découpage en phases GSD (milestone v1.9, exe 3.5.0) et ordre, avec points de validation utilisateur
   (liste de purge validée avant suppression ; palettes validées avant intégration ; plan de design DAEDALUS avant XAML).
