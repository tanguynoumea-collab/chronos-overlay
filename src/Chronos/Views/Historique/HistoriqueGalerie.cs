using System.Windows;
using System.Windows.Threading;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.ViewModels.Historique;

namespace Chronos.Views.Historique;

/// <summary>
/// D-34-26 — galerie <c>--historique</c> : la fenêtre RÉELLE (<see cref="HistoriqueWindow"/>) sur la semaine de référence des maquettes
/// (<see cref="ScenariosHistorique"/> servie par la source de démonstration en mémoire), horloge figée (jeu. 24 sept. 2026 17:12 local),
/// réglages EN MÉMOIRE (jamais <c>settings.json</c>), reconstruction figée à 886 / 1603 (bandeau F2 visible) — pour la revue
/// visuelle DESIGN_PLAN §8. Composition STATIQUE : aucun conteneur, aucun service, aucune écriture ; branchée dans <c>App.OnStartup</c>
/// AVANT le verrou d'instance unique et AVANT le Host, comme <c>--sessions</c> (multi-instances par construction).
/// Le badge de version de la maquette est omis en phase 34 (affaire de la release 35).
/// </summary>
public static class HistoriqueGalerie
{
    public static HistoriqueWindow Creer()
    {
        var tz = TimeZoneInfo.Local;   // site de composition : le fuseau du système est autorisé ici (D-32-29)
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;   // Application absente dans le hôte de test
        var vm = new HistoriqueViewModel(new SourceHistoriqueDemonstration(tz), new WpfUiDispatcher(dispatcher),
                                         ScenariosHistorique.HorlogeFigee(tz), tz, new ReglagesHistoriqueMemoire(),
                                         journal: null, reconstruction: ScenariosHistorique.ReconstructionEnCours());
        return new HistoriqueWindow(vm);
    }
}
