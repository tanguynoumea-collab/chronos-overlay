using System.Windows;
using Chronos.Services;

namespace Chronos.Views.Reglages;

/// <summary>
/// Quick 260927-reglages-v2 — implémentation WPF d'<see cref="IOuvreurReglages"/> : le clic droit sur le cadran ouvre la fenêtre de
/// réglages, ou la ramène au premier plan si elle est déjà ouverte ; fermée, elle est recréée au clic suivant. Aucun
/// <c>Owner</c> : la fenêtre n'est plus possédée par le cadran topmost, elle peut passer derrière les autres (DESIGN_PLAN §4).
/// </summary>
public sealed class OuvreurReglages(Func<Window> fabrique) : OuvreurFenetreUnique(fabrique), IOuvreurReglages;
