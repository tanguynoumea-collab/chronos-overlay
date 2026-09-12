# Quick 260912-ika — Le diagnostic ne décrit plus un état antérieur à sa propre exécution

**Date :** 2026-09-12
**Type :** correctif d'observabilité

## Le défaut, constaté en production

Diagnostic réel du 2026-09-12 13:15, deux sections du **même rapport** se contredisant :

```
[Source exacte — sonde d'en-têtes de rate-limit]
  Dernière sonde : pas encore sondé (la première sonde arrive au prochain tick)
  En-têtes « unified » reconnus : AUCUN
...
[Ce qui est affiché maintenant]
  5 h   : EXACT — 21 % · source : sonde d'en-têtes de rate-limit · relevé à l'instant · frais
```

On lisait « aucun en-tête reconnu » trois lignes au-dessus des chiffres que ces en-têtes venaient de
fournir. Trompeur pour un rapport dont le rôle est précisément de dire la vérité sur les sources.

## Cause

`DiagnosticService.BuildReportAsync` rendait la section « sonde » (qui lit `_etatServeur`) **avant**
d'appeler `await _composite.GetAsync(ct)` dans la section « Ce qui est affiché maintenant » — or c'est
cet appel qui déclenche la première sonde et peuple `_etatServeur`. Le rapport décrivait donc l'état du
système **avant** sa propre exécution.

## Correction

L'appel est hissé en **tête** de `BuildReportAsync`, avant le rendu de la moindre section. Le snapshot
(et l'éventuel message d'échec) sont mémorisés en variables locales ; la section « Ce qui est affiché
maintenant » les **consomme** au lieu de relancer la chaîne.

**L'ordre des sections du rapport ne change pas.** Seul l'instant de l'appel change.

Effet de bord favorable : la chaîne n'est plus interrogée qu'**une** fois par rapport. Auparavant, un
lecteur attentif aurait pu craindre un double coût ; c'est désormais structurellement impossible, et
c'est écrit dans le commentaire du point de consommation.

## Preuve

Nouveau test `DiagnosticServiceTests.Le_rapport_interroge_la_chaine_AVANT_de_decrire_la_sonde`, avec
deux doublures :
- `EtatServeurQuiSAllumeALaSonde` — canal latéral dont l'issue ne devient connue qu'après interrogation,
  comme la vraie sonde ;
- `ProviderQuiDeclencheLaSonde` — allume ce canal au moment du `GetAsync`.

Le test exige que la section « sonde » rende l'état d'**après** l'interrogation, et que les deux sections
racontent la même histoire.

**Falsifiabilité vérifiée** : remettre l'appel dans la section « Ce qui est affiché maintenant » fait
retomber le test (voir le SUMMARY d'exécution ci-dessous).

## Résultat

- **749 / 749 tests, 0 échec**, suite en 5 s (748 + 1 nouveau).
- Les 5 gardes permanentes vertes.
- Signature de `DiagnosticService` **inchangée** — aucun des 15 sites de construction retouché.
- 2 fichiers modifiés : `src/Chronos/Services/DiagnosticService.cs`,
  `tests/Chronos.Tests/DiagnosticServiceTests.cs`.

## Non traité (hors périmètre)

L'overlay **n'a pas été relancé** : une instance v3.0 tourne et l'utilisateur est en train de s'en servir.
Le correctif ne sera visible dans son diagnostic qu'après republication et redémarrage.
