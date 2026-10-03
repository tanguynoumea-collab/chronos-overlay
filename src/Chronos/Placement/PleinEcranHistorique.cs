namespace Chronos.Placement;

/// <summary>HIS-11 — ce que fait Échap dans la fenêtre Historique.</summary>
public enum ActionEchap { QuitterPleinEcran, Fermer }

/// <summary>
/// HIS-10 / HIS-11 — décisions PURES du plein écran de l'Historique (aucun type WPF, motif <see cref="PlacementHistorique"/>).
/// La fenêtre ne fait qu'appliquer le résultat : elle lit les bornes du moniteur et sa matrice TransformFromDevice, puis passe
/// les nombres ici.
/// </summary>
public static class PleinEcranHistorique
{
    /// <summary>Échap à deux niveaux (DESIGN_PLAN_CYCLE2 § 4.2) : d'abord quitter le plein écran, ensuite fermer.</summary>
    public static ActionEchap Echap(bool estPleinEcran) => estPleinEcran ? ActionEchap.QuitterPleinEcran : ActionEchap.Fermer;

    /// <summary>
    /// Bornes physiques d'un moniteur (rcMonitor : barre des tâches COUVERTE) → DIP par la matrice TransformFromDevice de la
    /// fenêtre (m11, m22). L'origine peut être négative (moniteur à gauche ou au-dessus du primaire).
    /// </summary>
    public static RectangleEcran BornesDip(int gauche, int haut, int droite, int bas, double m11, double m22)
        => new(gauche * m11, haut * m22, (droite - gauche) * m11, (bas - haut) * m22);
}
