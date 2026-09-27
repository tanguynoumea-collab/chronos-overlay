using System.Windows;
using Chronos.Services;

namespace Chronos.Views.Historique;

/// <summary>
/// ACC-02 / D-35-07 — implémentation WPF d'<see cref="IOuvreurHistorique"/> : UNE fenêtre Historique pour les deux gestes
/// (double-clic au centre du cadran, bouton « Ouvrir » des réglages), donc une seule entrée dans la barre des tâches. Le
/// mécanisme (ramener, recréer après fermeture, restaurer une fenêtre réduite) vit dans <see cref="OuvreurFenetreUnique"/>,
/// partagé avec l'ouvreur des réglages depuis le quick 260927-reglages-v2.
/// </summary>
public sealed class OuvreurHistorique(Func<Window> fabrique) : OuvreurFenetreUnique(fabrique), IOuvreurHistorique;
