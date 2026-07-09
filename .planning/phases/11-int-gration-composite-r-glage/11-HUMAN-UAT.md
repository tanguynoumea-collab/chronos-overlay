# Phase 11 — UAT humain (validation sur écran réel)

**Statut : EN ATTENTE de validation utilisateur**
Généré le 2026-07-09 à l'exécution du plan 11-02 (mode autonome : tout ce qui est automatisable a été
vérifié programmatiquement ; les critères ci-dessous exigent un écran réel + le VRAI token/endpoint OAuth
et ne peuvent PAS être automatisés — l'endpoint réel et le token du poste ne sont pas simulables en test).

## Contexte

La Phase 11 branche le provider OAuth exact en tête d'une chaîne composite à 3 niveaux
(OAuth gated → statusLine → JSONL, plan 11-01) et expose le réglage on/off « Usage exact (OAuth) »
dans le menu contextuel (plan 11-02, INT-03) tout en prouvant que le badge « estimée » disparaît sur une
fenêtre Exact (INT-02). Les vérifications programmatiques suivantes ont déjà été confirmées automatiquement :

- `dotnet build Chronos.sln -c Debug` : **0 avertissement, 0 erreur** (XAML inclus).
- `dotnet test Chronos.sln -c Debug` : **188/188 tests verts** (183 après 11-01 + 5 nouveaux 11-02),
  `ServicesLayerPurityTests` verte.
- `MainViewModel.ToggleOAuthUsage` : bascule `IsOAuthUsageEnabled`, `Load()` DISQUE frais → `with {
  OAuthUsageEnabled }` → `Save()` → `RequestRefresh()`, couvert par 3 `[Fact]` (init défaut true,
  bascule + persistance, non-écrasement GAP-1 d'un writer concurrent).
- INT-02 au niveau VM : `WindowGaugeViewModel.Apply(Exact)` → `IsEstimated == false` + `Utilization`
  réelle + `HasTokens == false` ; `Apply(Estimated)` → `IsEstimated == true`. Couvert par 2 `[Fact]`.
- `MenuItem "Usage exact (OAuth)"` présent dans `MainWindow.xaml`, `IsCheckable="True"`, lié à
  `IsOAuthUsageEnabled` (IsChecked) et `ToggleOAuthUsageCommand` (Command).
- ctor `MainViewModel` inchangé → `CadranBindingTests` / `OverlayWindowConfigTests` /
  `CompositionRootTests` restent verts (aucune régression DI).
- Republication exe self-contained mono-fichier win-x64 (~76 Mo) : **build de publication réussi**,
  processus lancé et **resté vivant 8 s sans crash**.

## Comment lancer

```
# exe republié (mode réel, token/endpoint OAuth du poste) :
src/Chronos/bin/Release/net8.0-windows/win-x64/publish/Chronos.exe
# ou en dev :
dotnet run --project src/Chronos/Chronos.csproj -c Debug
```

## Critères à valider manuellement (écran réel + vrai token OAuth requis)

| # | Exigence | Étape de vérification | Résultat attendu | Statut |
|---|----------|-----------------------|------------------|--------|
| 1 | INT-02 | OAuth ACTIVÉ (défaut), observer le cadran | Les deux arcs affichent les VRAIS pourcentages (ordre de grandeur ~74 % / ~93 %, cf. app bureau `/usage`), SANS le texte « estimée » sous les compteurs | ⬜ |
| 2 | INT-02 | Observer la couleur des arcs en OAuth activé | Arcs en couleur d'utilisation réelle (rampe vert→ambre→rouge selon le %), pas de gris/indisponible | ⬜ |
| 3 | INT-03 | Clic droit → menu → item « Usage exact (OAuth) » | L'item est présent, cochable, et coché par défaut (reflète `OAuthUsageEnabled == true`) | ⬜ |
| 4 | INT-03 | Décocher « Usage exact (OAuth) » | Le cadran bascule sur l'estimation (le badge « estimée » réapparaît si la source de repli est estimée), SANS erreur ni gel | ⬜ |
| 5 | INT-03 | Recocher « Usage exact (OAuth) » | Les vrais pourcentages reviennent au prochain rafraîchissement (bascule à chaud, sans redémarrage) | ⬜ |
| 6 | GAP-1 | Fermer puis relancer l'app après avoir décoché (puis recoché) | L'état coché/décoché survit au redémarrage (`%APPDATA%\Chronos\settings.json` → `OAuthUsageEnabled` reflète le dernier choix) | ⬜ |
| 7 | Honnêteté | Comparer badge « estimée » entre OAuth activé (Exact) et désactivé (repli) | Badge masqué en Exact, réaffiché en repli estimé — l'honnêteté joue dans les deux sens | ⬜ |

## Consigne en cas d'écart

Toute anomalie constatée (badge résiduel en Exact, mauvais chiffres, pas de bascule, non-persistance)
est consignée comme **gap** pour un plan de clôture (`--gaps`) ; ne pas « corriger en douce » sans replanifier.
