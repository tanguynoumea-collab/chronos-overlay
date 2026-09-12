
## Reporte par le plan 19-04 (hors perimetre, ne pas corriger ici)

- `MainWindow.xaml` l. 87 : commentaire perime (« UtilizationText (« ~ » si estime… ») ). NON corrige
  volontairement — le critere d'acceptation du plan 19-04 exigeait ZERO ligne supprimee dans ce fichier.
  A rectifier en phase 20, qui touchera ce bloc.
- `TokensText` / `HasTokens` (WindowGaugeViewModel) : calcules et testes, bindes NULLE PART dans les XAML
  (le « centre epure » de la v1.3 avait retire la ligne de tokens). La phase 20 doit trancher : les
  surfacer sous le plancher, ou acter qu'ils restent un signal de diagnostic seulement.
- `DataUnavailable` (MainViewModel) : binde nulle part. Le label explicite « indisponible » a l'ecran
  appartient a EXA-03, phase 20.
- `IsStale` (seuil 2 min) coexiste avec la limite d'age de la doctrine (6 min) et n'est binde nulle part :
  deux definitions concurrentes de « perime » dans le meme ViewModel. A retirer au profit de `Provenance`.
- Trou de la `UniformGrid` des reglages et dette de duree de `DiagnosticServiceTests` : toujours ouverts,
  explicitement hors perimetre du plan 19-04.
