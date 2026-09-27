using System.Windows.Controls;

namespace Chronos.Views.Historique;

/// <summary>Vue « Jour » (HIS-04, 34-07). Aucune logique : tout vient du VM (<c>HistoriqueViewModel</c>, DataContext hérité)
/// et des tokens ; la table des hauteurs §2.3 est vérifiée par <c>VueJourBindingTests</c> sur l'arbre visuel.</summary>
public partial class VueJourView : UserControl
{
    public VueJourView() => InitializeComponent();
}
