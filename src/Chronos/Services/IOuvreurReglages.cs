namespace Chronos.Services;

/// <summary>
/// Quick 260927-reglages-v2 — ouvre la fenêtre de réglages, ou la RAMÈNE au premier plan si elle est déjà ouverte (même principe
/// qu'<see cref="IOuvreurHistorique"/>, 35-02). Singleton : le clic droit sur le cadran ne crée jamais une seconde fenêtre.
/// Contrat NEUTRE (aucun type WPF) : l'implémentation vit sous <c>Views/Reglages</c>.
/// </summary>
public interface IOuvreurReglages
{
    /// <summary>Ouvre la fenêtre de réglages ou la ramène au premier plan (restaurée si elle était réduite).</summary>
    void Ouvrir();
}
