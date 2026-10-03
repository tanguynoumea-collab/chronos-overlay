using System.Windows.Controls;

namespace Chronos.Views.Cadrans;

/// <summary>Cadran « Anneaux » (style historique par défaut), extrait de MainWindow en phase 40 pour la galerie.
/// Aucune logique : uniquement des liaisons sur le DataContext (FiveHour / SevenDay / modes Normal et Étendu).</summary>
public partial class CadranArcsView : UserControl
{
    public CadranArcsView() => InitializeComponent();
}
