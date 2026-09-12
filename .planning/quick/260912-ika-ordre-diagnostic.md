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

**Falsifiabilité VÉRIFIÉE par mutation réelle**, après le commit du correctif (leçon du plan 19-03 : une
révocation par `git checkout` emporte tout correctif non committé) :

- Mutation appliquée — appel retiré de la tête, remis dans la section « Ce qui est affiché maintenant ».
- Résultat : `Échoué! - échec : 1, réussite : 0` sur le test ciblé.
- Mutation révoquée par `git checkout --`, arbre propre, suite complète re-verte.

## Résultat

- **749 / 749 tests, 0 échec**, suite en 5 s (748 + 1 nouveau).
- Les 5 gardes permanentes vertes.
- Signature de `DiagnosticService` **inchangée** — aucun des 15 sites de construction retouché.
- 2 fichiers modifiés : `src/Chronos/Services/DiagnosticService.cs`,
  `tests/Chronos.Tests/DiagnosticServiceTests.cs`.

## Non traité (hors périmètre)

L'overlay **n'a pas été relancé** : une instance v3.0 tourne et l'utilisateur est en train de s'en servir.
Le correctif ne sera visible dans son diagnostic qu'après republication et redémarrage.

---

## Suite — un second défaut, révélé par le premier correctif (2026-09-12)

Une fois l'ordre corrigé, le diagnostic a enfin montré ce que la sonde reçoit réellement. Et il affichait :

```
Dépassement :  · serveur : REJETÉ
```

Sur un compte **Max x20 à 23 % d'usage**, dont les deux fenêtres disaient par ailleurs « AUTORISÉ ».

### Cause

Le serveur envoie `anthropic-ratelimit-unified-overage-status` **sans aucune utilisation ni reset**.
`EtatDepassement.EstRenseigne` étant vrai dès qu'UN champ est posé — statut compris — la branche
d'affichage traitait cette politique comme un dépassement rapporté, et réutilisait le libellé de statut
de FENÊTRE. Or le même enum a deux sens opposés selon le contexte :

- sur une fenêtre, `rejected` = **« tu es bloqué »** ;
- sur le dépassement sans quantité, `rejected` = **« le dépassement n'est pas autorisé sur ce compte »** —
  une politique, pas un refus.

Double défaut : un séparateur orphelin (cosmétique) et un contresens alarmant (grave — le rapport
inquiétait sans raison).

### Correction

- `EtatDepassement.EstEnCours` — nouveau garde-fou d'**affichage** (« il se passe quelque chose »), exigeant
  une quantité ou un reset. `EstRenseigne` reste le garde-fou de **publication** (« le serveur a dit quelque
  chose »). Les deux sont nécessaires et ne se confondent pas.
- La ligne « Dépassement » a désormais **trois** cas : rien rapporté / politique déclarée sans dépassement en
  cours / dépassement réel avec sa quantité.
- `LibellePolitiqueDepassement` — libellés propres au contexte « politique », distincts de ceux du statut de
  fenêtre.

**Périmètre volontairement minimal** : `MainViewModel` (l. 412) et `Describe` (l. 605) gardaient **déjà** sur
`Utilization: not null`. L'overlay n'a jamais affiché ce contresens — seul le diagnostic. Un seul point de
rendu corrigé, aucun changement de comportement ailleurs.

### Ce que les données réelles ont confirmé

Les **8** noms d'en-têtes de la famille `unified` sont ceux que la sonde postulait — production confirmée,
rien à ajouter aux constantes :

```
-5h-utilization  -5h-reset  -5h-status
-7d-utilization  -7d-reset  -7d-status
-overage-status  -representative-claim
```

À noter : `-overage-status` porte bien le segment `overage`, ce qui **contredit** la correction que la
recherche de la phase 18 avait apportée (elle annonçait `anthropic-ratelimit-unified-status` sans segment).
Le code lisait les deux en repli — il a eu raison de ne pas trancher.

### Résultat

- **752 / 752 tests, 0 échec** (749 + 3 : politique seule, dépassement réel, rien rapporté).
- 3 fichiers : `EtatDepassement.cs`, `DiagnosticService.cs`, `DiagnosticServiceTests.cs`.
- Version portée à **3.0.1**, publiée, et le repointage automatique des hooks re-testé en direct
  (5 hooks → `Chronos-v3.0.1.exe`, 3 hooks GSD préservés, 2ᵉ sauvegarde horodatée).
