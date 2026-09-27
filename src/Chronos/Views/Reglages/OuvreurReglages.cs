using System.Windows;
using Chronos.Services;

namespace Chronos.Views.Reglages;

/// <summary>RED — squelette de l'ouvreur singleton des réglages.</summary>
public sealed class OuvreurReglages(Func<Window> fabrique) : IOuvreurReglages
{
    private readonly Func<Window> _fabrique = fabrique;

    public void Ouvrir() { }
}
