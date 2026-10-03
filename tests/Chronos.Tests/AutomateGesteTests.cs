using Chronos.ViewModels;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// GST-01 — l'automate PUR qui sépare, sur toute la silhouette du cadran, le clic, le double-clic et le glisser.
/// Clic sans déplacement = bascule (transmis à l'arbitre au relâchement) ; double-clic = Historique sans bascule
/// (décidé dès l'appui) ; appui puis déplacement au-delà du seuil Windows (strict, par axe) = glisser la fenêtre.
/// Deux pièges WPF couverts : <c>ClickCount</c> n'existe que sur l'appui, et <c>DragMove</c> envoie lui-même un
/// relâchement synthétique pendant son appel (réentrance). Aucun type WPF, aucune horloge : tests purs.
/// </summary>
public class AutomateGesteTests
{
    private const double Seuil = 3.2;   // 4 px physiques à 125 % = 3,2 DIP (mesuré sur la machine)

    private static AutomateGeste Glisse()
    {
        var a = new AutomateGeste();
        a.Appui(10, 10, 1);
        Assert.Equal(ActionGeste.CommencerGlisser, a.Deplacement(20, 10, Seuil, Seuil));
        return a;
    }

    [Fact]
    public void Appui_puis_relache_sans_deplacement_donne_un_clic_simple()
    {
        var a = new AutomateGeste();

        Assert.Equal(ActionGeste.Rien, a.Appui(10, 10, 1));
        Assert.Equal(EtatGeste.Appuye, a.Etat);

        Assert.Equal(ActionGeste.Clic(1), a.Relache());
        Assert.Equal(EtatGeste.Repos, a.Etat);
    }

    [Fact]
    public void Un_petit_deplacement_sous_le_seuil_reste_un_clic()
    {
        var a = new AutomateGeste();
        a.Appui(10, 10, 1);

        Assert.Equal(ActionGeste.Rien, a.Deplacement(13, 13, Seuil, Seuil));
        Assert.Equal(EtatGeste.Appuye, a.Etat);
        Assert.Equal(ActionGeste.Clic(1), a.Relache());
    }

    [Fact]
    public void Le_seuil_est_strict_l_egalite_ne_declenche_pas_le_glisser()
    {
        var a = new AutomateGeste();
        a.Appui(10, 10, 1);

        Assert.Equal(ActionGeste.Rien, a.Deplacement(13.2, 10, Seuil, Seuil));   // dx == seuil
        Assert.Equal(EtatGeste.Appuye, a.Etat);
    }

    [Fact]
    public void Un_seul_axe_au_dela_du_seuil_suffit_a_glisser()
    {
        var horizontal = new AutomateGeste();
        horizontal.Appui(10, 10, 1);
        Assert.Equal(ActionGeste.CommencerGlisser, horizontal.Deplacement(13.3, 10, Seuil, Seuil));
        Assert.Equal(EtatGeste.Glisse, horizontal.Etat);

        var vertical = new AutomateGeste();
        vertical.Appui(10, 10, 1);
        Assert.Equal(ActionGeste.CommencerGlisser, vertical.Deplacement(10, 13.3, Seuil, Seuil));
        Assert.Equal(EtatGeste.Glisse, vertical.Etat);

        var negatif = new AutomateGeste();
        negatif.Appui(10, 10, 1);
        Assert.Equal(ActionGeste.CommencerGlisser, negatif.Deplacement(6.7, 10, Seuil, Seuil));
        Assert.Equal(EtatGeste.Glisse, negatif.Etat);

        // Seuils asymétriques : chaque axe a le sien.
        var asymetrique = new AutomateGeste();
        asymetrique.Appui(10, 10, 1);
        Assert.Equal(ActionGeste.Rien, asymetrique.Deplacement(14, 10, 5, 1));
        Assert.Equal(EtatGeste.Appuye, asymetrique.Etat);
        Assert.Equal(ActionGeste.CommencerGlisser, asymetrique.Deplacement(10, 11.5, 5, 1));
        Assert.Equal(EtatGeste.Glisse, asymetrique.Etat);
    }

    [Fact]
    public void Le_relache_synthetique_de_DragMove_ne_produit_aucun_clic()
    {
        var a = Glisse();

        Assert.Equal(ActionGeste.Rien, a.Relache());   // réentrance : WM_LBUTTONUP envoyé par DragMove lui-même
        Assert.Equal(EtatGeste.Repos, a.Etat);
    }

    [Fact]
    public void Le_glisser_ne_commence_qu_une_seule_fois()
    {
        var a = Glisse();

        Assert.Equal(ActionGeste.Rien, a.Deplacement(40, 40, Seuil, Seuil));
        Assert.Equal(EtatGeste.Glisse, a.Etat);
    }

    [Fact]
    public void Le_double_clic_est_decide_des_l_appui_et_le_relache_ne_produit_rien()
    {
        var a = new AutomateGeste();

        var action = a.Appui(10, 10, 2);
        Assert.Equal(ActionGeste.Clic(2), action);
        Assert.True(action.EstClic);
        Assert.Equal(2, action.ClickCount);
        Assert.Equal(EtatGeste.Consomme, a.Etat);

        Assert.Equal(ActionGeste.Rien, a.Deplacement(30, 30, Seuil, Seuil));   // pas de glisser sur un double-clic
        Assert.Equal(ActionGeste.Rien, a.Relache());
        Assert.Equal(EtatGeste.Repos, a.Etat);

        Assert.Equal(ActionGeste.Clic(3), a.Appui(10, 10, 3));
    }

    [Fact]
    public void Une_perte_de_capture_pendant_l_appui_annule_le_geste()
    {
        var a = new AutomateGeste();
        a.Appui(10, 10, 1);

        a.PerteCapture();
        Assert.Equal(EtatGeste.Repos, a.Etat);
        Assert.Equal(ActionGeste.Rien, a.Relache());
    }

    [Fact]
    public void Une_perte_de_capture_pendant_le_glisser_est_ignoree()
    {
        var a = Glisse();

        a.PerteCapture();                              // provoquée par la boucle système de DragMove
        Assert.Equal(EtatGeste.Glisse, a.Etat);
        Assert.Equal(ActionGeste.Rien, a.Relache());
    }

    [Fact]
    public void Juste_apres_un_glisser_un_appui_compte_double_est_un_simple_appui()
    {
        var a = Glisse();
        a.Relache();

        // La fenêtre a suivi le curseur : WPF voit « le même endroit client » et compte double.
        Assert.Equal(ActionGeste.Rien, a.Appui(50, 50, 2));
        Assert.Equal(EtatGeste.Appuye, a.Etat);
        Assert.Equal(ActionGeste.Clic(1), a.Relache());

        // Le drapeau est consommé : le double-clic suivant est un vrai double-clic.
        Assert.Equal(ActionGeste.Clic(2), a.Appui(50, 50, 2));
    }

    [Fact]
    public void Au_repos_les_evenements_orphelins_ne_produisent_rien()
    {
        var a = new AutomateGeste();
        Assert.Equal(EtatGeste.Repos, a.Etat);

        Assert.Equal(ActionGeste.Rien, a.Deplacement(100, 100, Seuil, Seuil));
        Assert.Equal(EtatGeste.Repos, a.Etat);
        Assert.Equal(ActionGeste.Rien, a.Relache());
        Assert.Equal(EtatGeste.Repos, a.Etat);
        a.PerteCapture();
        Assert.Equal(EtatGeste.Repos, a.Etat);
    }

    [Fact]
    public void Un_nouvel_appui_repart_de_zero_meme_si_le_relache_a_ete_perdu()
    {
        var a = new AutomateGeste();
        a.Appui(0, 0, 1);
        a.Appui(100, 100, 1);                          // le relâchement du premier appui n'est jamais arrivé

        Assert.Equal(ActionGeste.Rien, a.Deplacement(102, 102, Seuil, Seuil));   // départ = (100, 100)
        Assert.Equal(EtatGeste.Appuye, a.Etat);
    }

    [Fact]
    public void L_automate_est_pur_sans_WPF_ni_horloge()
    {
        var fichier = Path.Combine(GardesPerimetreTests.CheminSources(), "ViewModels", "AutomateGeste.cs");
        var source = File.ReadAllText(fichier);

        Assert.True(source.Length > 200, "AutomateGeste.cs est vide ou introuvable.");
        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopwatch", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", source, StringComparison.Ordinal);
    }
}
