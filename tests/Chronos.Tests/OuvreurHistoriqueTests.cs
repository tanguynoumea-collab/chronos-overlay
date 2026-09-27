using System.Windows;
using Chronos.Views.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// ACC-02 / D-35-07 — l'ouvreur de la fenêtre Historique est un SINGLETON ré-affiché : deux ouvertures = une fenêtre ;
/// une fenêtre fermée ne se ré-affiche pas (WPF lève à <c>Show</c>) → une nouvelle est recréée ; minimisée, elle revient
/// en Normal. Fenêtres de test hors écran, non activées, hors barre des tâches, toujours fermées en fin de test.
/// </summary>
[Collection("XAML WPF")]
public class OuvreurHistoriqueTests
{
    private readonly List<Window> _creees = new();

    private Func<Window> Fabrique() => () =>
    {
        var w = new Window
        {
            Left = -20000, Top = -20000, Width = 40, Height = 40,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
        };
        _creees.Add(w);
        return w;
    };

    private void FermerTout()
    {
        foreach (var w in _creees)
            if (w.IsLoaded) w.Close();
    }

    [WpfFact]
    public void Deux_ouvertures_une_seule_fenetre()
    {
        try
        {
            var ouvreur = new OuvreurHistorique(Fabrique());

            ouvreur.Ouvrir();
            ouvreur.Ouvrir();

            Assert.Single(_creees);
            Assert.True(_creees[0].IsVisible);
        }
        finally { FermerTout(); }
    }

    [WpfFact]
    public void Apres_fermeture_une_nouvelle_fenetre_est_creee()
    {
        try
        {
            var ouvreur = new OuvreurHistorique(Fabrique());

            ouvreur.Ouvrir();
            var premiere = _creees[0];
            premiere.Close();
            ouvreur.Ouvrir();

            Assert.Equal(2, _creees.Count);
            Assert.NotSame(premiere, _creees[1]);
            Assert.True(_creees[1].IsVisible);
            Assert.False(premiere.IsVisible);
        }
        finally { FermerTout(); }
    }

    [WpfFact]
    public void Une_fenetre_minimisee_revient_en_normal()
    {
        try
        {
            var ouvreur = new OuvreurHistorique(Fabrique());

            ouvreur.Ouvrir();
            _creees[0].WindowState = WindowState.Minimized;
            ouvreur.Ouvrir();

            Assert.Single(_creees);
            Assert.Equal(WindowState.Normal, _creees[0].WindowState);
        }
        finally { FermerTout(); }
    }
}
