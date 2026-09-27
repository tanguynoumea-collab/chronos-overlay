using System.Windows;

namespace Chronos.Views;

/// <summary>
/// ACC-02 / D-35-07, généralisé par le quick 260927-reglages-v2 — UNE fenêtre par ouvreur, quel que soit le nombre de gestes : la
/// première ouverture la fabrique et l'affiche, les suivantes la RAMÈNENT au premier plan. Base commune de
/// <c>OuvreurHistorique</c> et <c>OuvreurReglages</c> (une seule entrée dans la barre des tâches pour chacune).
///
/// <para>Pourquoi RECRÉER après fermeture : une <see cref="Window"/> WPF fermée ne se ré-affiche pas (<c>Show</c> lève
/// <see cref="InvalidOperationException"/>) ; on oublie la référence à <c>Closed</c> et la prochaine ouverture en fabrique une
/// neuve (le ViewModel, lui, est un singleton du conteneur : son état survit).</para>
///
/// <para>Pourquoi <c>Activate</c> suffit pour la ramener : l'appel suit toujours un geste de l'utilisateur, Windows autorise
/// alors le passage au premier plan. Réduite, elle revient d'abord en <see cref="WindowState.Normal"/>.</para>
/// </summary>
public abstract class OuvreurFenetreUnique(Func<Window> fabrique)
{
    private readonly Func<Window> _fabrique = fabrique ?? throw new ArgumentNullException(nameof(fabrique));
    private Window? _fenetre;

    public void Ouvrir()
    {
        if (_fenetre is null)
        {
            var fenetre = _fabrique();
            _fenetre = fenetre;
            fenetre.Closed += (_, _) => { if (ReferenceEquals(_fenetre, fenetre)) _fenetre = null; };
            fenetre.Show();
            return;
        }

        if (_fenetre.WindowState == WindowState.Minimized) _fenetre.WindowState = WindowState.Normal;
        _fenetre.Activate();
    }
}
