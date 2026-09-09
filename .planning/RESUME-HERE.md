# Point de reprise — milestone v1.5 « Exactitude permanente »

**Pause demandée par l'utilisateur le 2026-09-09** (limite de jetons proche).
Arbre de travail **propre**, tout committé, dernier commit `706bf74`.

---

## Où on en est

| Phase | État | Tests |
|-------|------|-------|
| 15 — Idempotence des intégrations | ✅ **Terminée et vérifiée** (4/4 must-haves) | 328 → 405 |
| 16 — Fondations du delta | ✅ **Terminée et vérifiée** (5/5 must-haves) | 405 → 419 |
| 17 — Jeton vivant, panne visible | 🚧 **2 plans sur 5** | 419 → **445** |
| 18 — Source en-têtes de rate-limit | ⬜ Non commencée | — |
| 19 — Nouvelle doctrine du composite | ⬜ Non commencée | — |
| 20 — Honnêteté visible | ⬜ Non commencée | — |

**Suite de tests : 445 verts, 0 échec.**

---

## Reprendre exactement ici

```
/gsd:execute-phase 17
```

Les 5 plans de la phase 17 sont écrits, révisés et **vérifiés** (`17-01` à `17-05-PLAN.md`).
Les plans `17-01` et `17-02` portent leur SUMMARY et sont committés. L'exécution reprend au **plan 17-03**.

Ensuite, pour enchaîner le reste du milestone :

```
/gsd:autonomous --from 18
```

---

## Ce qui reste à faire dans la phase 17

| Plan | Vague | Contenu | État |
|------|-------|---------|------|
| 17-01 | 1 | Wave 0 de couverture (`ChronosOAuthUsageProvider`, `RefreshAsync`) | ✅ fait |
| 17-02 | 2 | `EtatAuthentification` + `IAuthStatus` + `ResultatRafraichissement` | ✅ fait |
| 17-03 | 3 | `ChronosTokenAuthority` + `TokenRefreshService` (tick 60 s, 1er tick immédiat) | ⬜ **à faire** |
| 17-04 | 4 | Le provider perd son refresh, rejeu unique sur 401, câblage DI, diagnostic | ⬜ à faire |
| 17-05 | 5 | VM + `ReconnecterCommand` dédiée + pastille (checkpoint humain) | ⬜ à faire |

---

## À NE PAS PERDRE en reprenant

### 1. Sécurité — contrainte qui prime sur tout
**Ne JAMAIS appeler l'endpoint de refresh (`console.anthropic.com/v1/oauth/token`) avec le refresh token
RÉEL de `%APPDATA%\Chronos\oauth.dat`.** Le flux OAuth fait TOURNER le refresh token : un appel dont le
résultat n'est pas re-sauvegardé **invaliderait définitivement le login de l'utilisateur**. Tout passe par
`FakeHttpMessageHandler`. Le coffre réel n'est ni lu, ni déchiffré, ni affiché.
Contrôle à chaque plan : `oauth.dat` doit rester **518 o, mtime 1783863147**.

### 2. Découverte du plan 17-01 à porter impérativement dans 17-03/17-04
Le garde-fou anti-429 actuel (`ChronosOAuthUsageProvider.cs:53`) exige **deux** conditions :
`now < _nextAllowedCall` **ET** `_cached is not null`. Or `_cached` est un champ d'instance en RAM, donc
**vide à chaque démarrage de l'exe**. Conséquence : un exe qui redémarre avec un jeton mort **remartèle
l'endpoint à chaque tick sans aucun frein** — ce qui entretient le 429 qui l'a causé. C'est le cas nominal
de la panne des deux derniers mois.
➡ **Le recul (backoff) doit vivre dans `ChronosTokenAuthority`, indépendamment de tout cache d'usage**,
sinon le symptôme survivra à la refonte. Gravé dans le test
`Un_429_sans_cache_ne_freine_aujourd_hui_absolument_rien`.

### 3. Piège TOK-03
**Ne PAS binder la pastille sur `LoginClaudeCommand`** : elle BASCULE, et `IsLoggedIn == _store.Exists`
vaut `true` même avec un jeton expiré → un clic **supprimerait le coffre**. Une commande DÉDIÉE
(`ReconnecterCommand`) est spécifiée dans le plan 17-05.

### 4. Traçabilité volontairement en retard
`TOK-01` et `TOK-02` sont laissés **`Pending`** dans `REQUIREMENTS.md` bien que les frontmatters de 17-01 et
17-02 les déclarent : ces deux plans posent la couverture et le vocabulaire, ils ne satisfont aucune
exigence. Ils seront réellement refermés par 17-03 → 17-05. C'est délibéré, pas un oubli.

### 5. Invariant de compilation
`tests/Chronos.Tests` a un `ProjectReference` vers `Chronos` : une erreur de compilation dans `App.xaml.cs`
fait échouer TOUTE l'invocation `dotnet test`. Corollaire : quand une tâche change une signature, elle doit
adapter ses sites d'appel **dans la même tâche**. Le plan 17-04 a été révisé pour ça (tâches 1 et 2 fusionnées).

### 6. Flakiness résolue, à ne pas réintroduire
Une course du chargeur BAML de WPF (`WpfXamlType.FindKnownMember`, table non thread-safe) faisait échouer
`CompositionRootTests` ~2 fois sur 9. Corrigée en phase 16 par une collection xUnit `DisableParallelization`
sur les 4 classes chargeant du BAML. Le plan 17-05 ajoute un `[WpfFact]` → **exécuter la suite deux fois**
avant de conclure.

---

## Élément différé à traiter en phase 20
Trou visuel dans la `UniformGrid` des réglages : 3 boutons dans une grille à `Columns="2"`, cellule
bas-droite vide (hérité de la suppression du bouton « Plafonds… » en phase 16). Passer à `Columns="3"`
tronquerait les libellés (panneau 330 px, « Recalibrer hebdo… » demande ~105 px à `FontSize=12`).
Piste : grille 2×2 avec « Diagnostic… » en `ColumnSpan=2`.

---

## État réel de la machine (inchangé, à savoir en reprenant)
- `~/.claude/settings.json` : **toujours 25 hooks Chronos** au lieu de 5. La purge est codée et testée
  (phase 15) mais ne s'appliquera **qu'au prochain lancement de Chronos**, avec sa sauvegarde horodatée.
- Jeton OAuth Chronos : expiré depuis le **2026-07-12**, `GET /api/oauth/usage` → **401**.
- Anneau hebdo : gris depuis la phase 16 (perte assumée de la couleur qui était fausse à ~4×).
  Le compte à rebours survit. L'anneau 5 h est inchangé.
