using System.Windows;
using System.Windows.Media;
using Chronos.Services;
using Chronos.Models.Historique.Tokens;
using Chronos.Services.Historique;
using Chronos.Theming;
using Chronos.ViewModels.Historique;
using Chronos.Views.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// 35-01 (D-35-05, pièges 2, 3 et 4 de 35-RESEARCH) — le <see cref="HistoriqueViewModel"/> devient un SINGLETON ré-ouvrable
/// (35-02 : double-clic, carte F1) : une fenêtre fermée puis recréée doit retrouver le bandeau F2 VIVANT (réabonnement à la
/// reconstruction), le thème ACTIF (relu à la construction de la fenêtre) et ne laisser AUCUNE fenêtre fermée accrochée au VM
/// (<c>FermetureDemandee</c> retiré au <c>Closed</c>). Collection sérialisée (chargeur BAML) ; aucun fichier, aucune fenêtre montrée.
/// </summary>
[Collection("XAML WPF")]
public class ReouvertureHistoriqueTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();

    private static HistoriqueViewModel Vm(IUiDispatcher? ui = null, FakeEtatReconstruction? recon = null,
                                          ReglagesHistoriqueMemoire? reglages = null)
        => new(new SourceHistoriqueDemonstration(Tz), ui ?? new FakeUiDispatcher { OnUiThread = true }, ScenariosHistorique.HorlogeFigee(Tz), Tz,
               reglages ?? new ReglagesHistoriqueMemoire(), null, recon);

    private static Color CouleurDe(object? b) => Assert.IsType<SolidColorBrush>(b).Color;

    [WpfFact]
    public void Le_bandeau_F2_revit_a_la_deuxieme_ouverture()
    {
        var ui = new FakeUiDispatcherDiffere { OnUiThread = true };
        var recon = new FakeEtatReconstruction { Phase = PhaseReconstruction.Reconstruction, FichiersTraites = 1, FichiersTotal = 10 };
        var vm = Vm(ui, recon);

        vm.DemarrerHorloge();   // première ouverture
        vm.ArreterHorloge();    // fenêtre fermée : désabonnement
        vm.DemarrerHorloge();   // deuxième ouverture : le bandeau doit revivre
        ui.Vider();

        var avant = ui.PostCount;
        for (var i = 0; i < 50; i++) recon.Declencher();
        Assert.Equal(avant + 1, ui.PostCount);   // réabonné, et toujours coalescé
        recon.FichiersTraites = 5;
        ui.Vider();
        Assert.Contains("5 / 10", vm.TexteBandeauF2, StringComparison.Ordinal);

        vm.DemarrerHorloge();   // idempotent : pas de second abonnement
        avant = ui.PostCount;
        for (var i = 0; i < 50; i++) recon.Declencher();
        Assert.Equal(avant + 1, ui.PostCount);
        ui.Vider();

        vm.ArreterHorloge();    // UN désabonnement suffit : il n'y avait qu'un abonnement
        avant = ui.PostCount;
        recon.Declencher();
        Assert.Equal(avant, ui.PostCount);
    }

    [WpfFact]
    public void Une_fenetre_fermee_ne_reste_pas_accrochee_au_VM()
    {
        var vm = Vm();

        var a = new HistoriqueWindow(vm);
        var fermee = false;
        a.Closed += (_, _) => fermee = true;
        a.Close();
        Assert.True(fermee);

        vm.FermerCommand.Execute(null);
        Assert.Equal(0, a.DemandesDeFermeture);   // la fenêtre fermée n'écoute plus le VM

        var b = new HistoriqueWindow(vm);
        vm.FermerCommand.Execute(null);
        Assert.Equal(1, b.DemandesDeFermeture);
        Assert.Equal(0, a.DemandesDeFermeture);
        b.Close();
    }

    [WpfFact]
    public void La_fenetre_injecte_le_theme_actif_a_sa_construction()
    {
        var reglages = new ReglagesHistoriqueMemoire(new ChronosSettings { ThemeKey = "nord" });
        var vm = Vm(reglages: reglages);
        Assert.Equal("nord", vm.Theme.Key);

        reglages.Modifier(s => s with { ThemeKey = "aurore" });   // l'utilisateur change de thème pendant que la fenêtre est fermée
        var fenetre = new HistoriqueWindow(vm);

        Assert.Equal("aurore", vm.Theme.Key);
        var attendu = CouleurDe(ThemeCatalog.ByKey("aurore").BrushTokens()["Alerte"]);
        Assert.Equal(attendu, CouleurDe(fenetre.Resources["Alerte"]));
        Assert.NotEqual(CouleurDe(ThemeCatalog.ByKey("nord").BrushTokens()["Alerte"]), attendu);
        fenetre.Close();
    }
}
