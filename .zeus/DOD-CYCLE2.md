# Definition of Done — Chronos (cycle ZEUS n°2 : R1-R7, v1.9 / exe 3.5.0)

> Instanciée le 2026-10-03 à la sortie du checkpoint 1 (plan de design validé). Remplace `.zeus/DOD.md` (cycle 1)
> pour ce cycle ; toute modification est une décision explicite consignée dans `.zeus/state.json`. Seul critère de
> sortie de la phase GSD.

## Socle (tout projet)

- [ ] Phases 36 à 42 du milestone v1.9 `done`, VERIFICATION.md passée (la 43 = publication, hors GSD)
- [ ] `dotnet build Chronos.sln -c Release` propre, 0 avertissement (`-warnaserror`)
- [ ] Tests : 100 % verts (aucun test retiré sans décision ; les retraits de la purge R5 sont couverts par la liste validée)
- [ ] Aucune valeur de couleur ni de taille hors `Resources/DesignTokens.xaml` ou des pinceaux du thème (gardes vertes)
- [ ] Chaînes visibles en français, vocabulaire de `.zeus/DESIGN_PLAN_CYCLE2.md` respecté

## Spécifique Chronos

- [ ] Doctrine « exact ou rien » : seul le plancher « ≥ » n'est pas exact ; « ↻ HH:MM » absent si inconnu ou dépassé
- [ ] Chaîne de données = sonde d'en-têtes + secours OAuth Chronos, un seul composite (garde verte)
- [ ] Retrait de la barre statusLine : sauvegarde avant écriture, idempotent, barre tierce intacte (tests sur fichiers témoins)
- [ ] Un `settings.json` ancien (style d'Historique, enums inconnus, clés retirées) se lit sans perte
- [ ] Aucun test n'écrit dans le vrai `~/.claude` ni dans le vrai `%APPDATA%\Chronos`

## Par fonctionnalité du cycle

| Fonctionnalité | Critère d'acceptation observable | Fait |
|---|---|---|
| R1 Orientations | Fusible V, Marée H, Volets V existent, reconnaissables, sélectionnables par la carte Orientation, mémorisés par cadran | ☐ |
| R2 +20 % | Fusible 190 × 92 et Volets 190 × 66 à l'écran, fenêtre à l'empreinte, collée à son coin | ☐ |
| R3 Braises | 20 braises en 5 groupes d'une heure, flèche à midi, « ↻ HH:MM » en mode temps | ☐ |
| R4 Historique | Pistes seul ; plein écran général (F11 / bouton), Échap à deux niveaux, géométrie restaurée | ☐ |
| R5 Purge | Liste validée appliquée, diagnostic « Chaîne de données », méthodologie unique documentée | ☐ |
| R6 Thèmes | 15 thèmes en Pâle / Classique / Vive, gris épuisé ≥ 3:1, cadrans alternatifs thémés | ☐ |
| R7 Gestes | Clic, double-clic, glisser, clic droit sur toute la silhouette des 8 variantes ; clic qui traverse hors silhouette | ☐ |

Les critères « vus à l'écran » (R1, R2, R4 plein écran, R7 clics réels) sont confirmés en DESIGN-REVIEW puis au
constat de la phase 43 ; la DoD de GSD est atteinte quand le code et les tests les couvrent.
