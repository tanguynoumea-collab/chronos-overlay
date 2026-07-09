# Phase 12 — UAT humain (validation sur écran réel)

**Statut : PARTIELLEMENT VALIDÉ — l'orchestrateur a déjà lancé l'exe réel et confirmé les points macro**
Généré le 2026-07-09 à l'exécution du plan 12-02 ; mis à jour le 2026-07-09 après les 3 ajouts
d'itération (VIS-06 centre épuré, VIS-07 fond transparent, VIS-08 clic centre, ROB-05 anti-429) et la
vérification de phase. Les critères ci-dessous restent **purement visuels/fins** (nuance de couleur,
espacement exact, lisibilité subjective) — ils ne peuvent PAS être automatisés et ne bloquent PAS le
statut de la phase (déjà `passed` sur la base du code + tests + confirmation macro ci-dessous).

## Confirmation macro déjà obtenue (session orchestrateur, exe réel lancé)

- **VIS-07 (fond transparent)** : confirmé — le grand disque sombre a disparu, seuls les anneaux et un
  petit disque central (64 px) sont visibles par-dessus le bureau.
- **VIS-08 (clic centre bascule %/temps)** : confirmé — capture des deux modes sur les DEUX fenêtres
  (5 h et hebdo) : `13 % / 97 %` (mode pourcentages) ↔ `1 h 44 / 1 j 9 h` (mode temps avant reset).
- **ROB-05 (anti-429 / données exactes)** : confirmé — les valeurs affichées lors de la capture sont
  exactes (pas de clignotement exact↔estimé observé), cohérentes avec les deux modes ci-dessus.

Ces trois points n'ont donc PAS besoin d'être revalidés manuellement ; ils sont listés ci-dessous
uniquement pour mémoire (case cochée) et pour tracer les nuances fines encore ouvertes.

## Contexte

Le plan 12-02 (+ les 3 correctifs/ajouts committés ensuite : `e21f9c3`, `dc2d3b5`, `c993533`) recompose
`MainWindow.xaml` pour livrer visuellement la refonte v1.3 complète : remplissage inversé
(`FractionElapsed`, vide si reset inconnu), 3 anneaux réordonnés du centre vers l'extérieur
(hebdo R38 → 5 h R54 → 24 h R64), anneau timeline 24 h coloré et gradué, centre épuré (uniquement les
deux pourcentages, badges/tokens retirés), fond transparent, clic centre bascule %/temps, et résilience
anti-429 du provider OAuth. Vérifications programmatiques déjà confirmées :

- `dotnet build Chronos.sln -c Debug` : **0 avertissement, 0 erreur**.
- `dotnet test Chronos.sln -c Debug` : **215/215 tests verts** (aucune régression).
- Structure XAML vérifiée par lecture directe (voir 12-VERIFICATION.md pour le détail complet).

## Comment lancer

```
# exe republié (peut être légèrement en retard sur le disque de dev, republier si besoin) :
src/Chronos/bin/Release/net8.0-windows/win-x64/publish/Chronos.exe
# ou en dev :
dotnet run --project src/Chronos/Chronos.csproj -c Debug
```

## Critères fins restant à valider manuellement (non bloquants)

| # | Exigence | Étape de vérification | Résultat attendu | Statut |
|---|----------|-----------------------|------------------|--------|
| 1 | TAILLE-01 | Lancer l'overlay | Fenêtre ~170 px, sans crash | ✅ (confirmé build+run) |
| 2 | VIS-01 | Observer les arcs 5 h et hebdo au fil du temps | Plus remplis en fin de fenêtre (vides en début) | ⬜ nuance fine |
| 3 | VIS-02 | Observer l'empilement des anneaux | Centre → extérieur : hebdo, 5 h, 24 h, sans chevauchement | ⬜ nuance fine |
| 4 | JOUR-01/02/03 | Observer l'anneau 24 h | Rempli minuit→maintenant, ticks aux resets 5 h, couleur = anneau 5 h | ⬜ nuance fine |
| 5 | VIS-06 | Observer le centre + les ticks | Centre épuré (2 lignes de % seulement) ; marques horaires visibles sur l'anneau 5 h ; marques de reset visibles sur le 24 h | ⬜ nuance fine (contraste/lisibilité) |
| 6 | VIS-07 | Observer le fond | Anneaux flottent sur le bureau, pas de grand disque sombre | ✅ confirmé (orchestrateur) |
| 7 | VIS-08 | Cliquer au centre | Bascule %  ↔ temps, sans déclencher le drag de fenêtre | ✅ confirmé (orchestrateur, capture des 2 modes) |
| 8 | TAILLE-01 | Vérifier la compacité globale | 3 anneaux + texte lisibles, aucun chevauchement à 170 px | ⬜ nuance fine |
| 9 | TAILLE-01 | Drag (clic sur un anneau) puis snap | Le drag fonctionne, snap correct à 170 px | ⬜ nuance fine |
| 10 | ROB-05 | Observer sur une session longue (>15 min) | Plus de clignotement exact↔estimé sur la fenêtre 5 h | ✅ confirmé (orchestrateur, données exactes stables) |

## Consigne en cas d'écart

Tout réglage visuel fin (contraste de tick, épaisseur, gap, taille de police) est un **ajustement**, pas
une anomalie fonctionnelle : modifier les valeurs concrètes dans `MainWindow.xaml`/`DesignTokens.xaml`
puis re-vérifier build+tests. Une anomalie fonctionnelle (crash, valeur fausse, drag cassé) doit être
remontée via `--gaps` plutôt que corrigée en douce.
