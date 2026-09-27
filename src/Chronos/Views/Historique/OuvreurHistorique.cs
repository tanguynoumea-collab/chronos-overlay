using System.Windows;
using Chronos.Services;

namespace Chronos.Views.Historique;

/// <summary>
/// ACC-02 / D-35-07 — implémentation WPF d'<see cref="IOuvreurHistorique"/> : UNE fenêtre Historique pour les deux gestes
/// (double-clic au centre du cadran, bouton « Ouvrir » des réglages), donc une seule entrée dans la barre des tâches.
///
/// <para>Pourquoi RECRÉER après fermeture : une <see cref="Window"/> WPF fermée ne se ré-affiche pas (<c>Show</c> lève
/// <see cref="InvalidOperationException"/>) ; on oublie la référence à <c>Closed</c> et la prochaine ouverture en fabrique une
/// neuve (le ViewModel, lui, est le singleton du conteneur : l'état de navigation survit).</para>
///
/// <para>Pourquoi <c>Activate</c> suffit pour la ramener : l'appel suit toujours un geste de l'utilisateur, Windows autorise
/// alors le passage au premier plan. Minimisée, elle revient d'abord en <see cref="WindowState.Normal"/>.</para>
/// </summary>
public sealed class OuvreurHistorique(Func<Window> fabrique) : IOuvreurHistorique
{
    private Window? _fenetre;

    public void Ouvrir()
    {
        if (_fenetre is null)
        {
            var fenetre = fabrique();
            _fenetre = fenetre;
            fenetre.Closed += (_, _) => { if (ReferenceEquals(_fenetre, fenetre)) _fenetre = null; };
            fenetre.Show();
            return;
        }

        if (_fenetre.WindowState == WindowState.Minimized) _fenetre.WindowState = WindowState.Normal;
        _fenetre.Activate();
    }
}
