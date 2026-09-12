namespace Chronos.Services;

/// <summary>
/// Les DEUX sondages d'environnement MESURÉS comme chers dans <see cref="DiagnosticService.BuildReportAsync"/>
/// (2026-09-12, machine cible) : recherche des coffres OAuth 17 703 ms (94 %) et poll UIA 936 ms (5 %) —
/// 99,4 % du coût d'un rapport. Tout le reste du rapport coûte moins de 50 ms cumulés et n'a aucun
/// besoin de couture.
///
/// Cette interface n'existe QUE pour rendre ces deux sondages substituables sous test. Elle n'est pas
/// un point d'extension : n'y ajouter un membre que si une mesure montre qu'il coûte des secondes.
///
/// Type NEUTRE (aucun type WPF) : la garde <c>ServicesLayerPurityTests</c> s'applique.
/// </summary>
public interface IInventaireMachine
{
    /// <summary>Fichiers config.json contenant « oauth:tokenCache » sous <paramref name="racine"/>.</summary>
    IReadOnlyList<string> CoffresOAuth(string racine);

    /// <summary>Sessions de l'app bureau vues par UI Automation, AVEC la santé du sondage — les deux
    /// sont rendues ensemble parce que le rapport imprime les deux et qu'un second appel re-paierait
    /// le poll.</summary>
    (IReadOnlyList<SessionSnapshot> Sessions, DesktopHealth Sante) SessionsBureau(DateTimeOffset now);
}
