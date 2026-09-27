using System.Windows.Controls;

namespace Chronos.Views.Historique;

/// <summary>Vue « 4 semaines » (HIS-05, 35-04). Aucune logique : tout vient du VM (<c>HistoriqueViewModel</c>, DataContext hérité)
/// et des tokens ; hauteurs, largeurs et opacités §2.4 vérifiées par <c>VueQuatreSemainesBindingTests</c> sur l'arbre visuel.</summary>
public partial class VueQuatreSemainesView : UserControl
{
    public VueQuatreSemainesView() => InitializeComponent();
}
