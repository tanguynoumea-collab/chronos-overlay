using System.Windows.Controls;

namespace Chronos.Views.Historique;

/// <summary>Vue « Semaine de forfait » (HIS-02 / HIS-03, 34-06). Aucune logique : tout vient du VM (<c>HistoriqueViewModel</c>,
/// DataContext hérité) et des tokens ; la table des hauteurs §2.2 est vérifiée par <c>VueSemaineBindingTests</c> sur l'arbre visuel.</summary>
public partial class VueSemaineView : UserControl
{
    public VueSemaineView() => InitializeComponent();
}
