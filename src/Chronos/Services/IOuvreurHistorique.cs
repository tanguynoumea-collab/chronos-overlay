namespace Chronos.Services;

/// <summary>
/// ACC-02 / D-35-07 — ouvre la fenêtre Historique, ou la RAMÈNE au premier plan si elle est déjà ouverte.
/// Singleton : deux gestes (double-clic sur le cadran, bouton « Ouvrir » des réglages), une seule fenêtre.
/// Contrat NEUTRE (aucun type WPF, même motif que les autres contrats d'ouverture de fenêtre) : l'implémentation WPF vit sous
/// <c>Views/Historique</c>, le ViewModel ne voit que cette interface.
/// </summary>
public interface IOuvreurHistorique
{
    /// <summary>Ouvre la fenêtre Historique ou la ramène au premier plan (restaurée si elle était minimisée).</summary>
    void Ouvrir();
}
