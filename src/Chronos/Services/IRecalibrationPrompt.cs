namespace Chronos.Services;

/// <summary>
/// Contrat NEUTRE (aucun type WPF en signature) permettant au ViewModel de demander une date
/// d'ancrage hebdomadaire (recalibrage best-effort ROB-03) sans dépendre du dialogue WPF concret.
/// L'implémentation WPF <c>RecalibrationPrompt</c> (namespace Chronos.Views) est fournie en Task 2 ;
/// garder cette interface exempte de type WPF laisse la garde de pureté Services verte et le VM testable.
/// </summary>
public interface IRecalibrationPrompt
{
    /// <summary>Demande une date d'ancrage hebdo. Retourne null si l'utilisateur annule.</summary>
    DateTimeOffset? Ask(DateTimeOffset? current);
}
