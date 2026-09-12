namespace Chronos.Services;

/// <summary>
/// Le sondage d'environnement MESURÉ comme cher dans <see cref="DiagnosticService.BuildReportAsync"/>
/// (2026-09-12, machine cible) : recherche des coffres OAuth, 17 703 ms — 94 % du coût d'un rapport. Tout
/// le reste du rapport coûte moins de 50 ms cumulés et n'a aucun besoin de couture.
///
/// <para>Phase 21 : le second membre (poll de la source app-bureau, 936 ms) a disparu avec cette source.</para>
///
/// Cette interface n'existe QUE pour rendre ce sondage substituable sous test. Elle n'est pas un point
/// d'extension : n'y ajouter un membre que si une mesure montre qu'il coûte des secondes.
///
/// Type NEUTRE (aucun type WPF) : la garde <c>ServicesLayerPurityTests</c> s'applique.
/// </summary>
public interface IInventaireMachine
{
    /// <summary>Fichiers config.json contenant « oauth:tokenCache » sous <paramref name="racine"/>.</summary>
    IReadOnlyList<string> CoffresOAuth(string racine);
}
