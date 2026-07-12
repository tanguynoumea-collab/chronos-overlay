using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>
/// VM de la galerie de prévisualisation des styles de sessions (prototype, lancé via « --sessions »).
/// Expose des <see cref="SessionItemVm"/> d'ÉCHANTILLON couvrant les 4 états (à toi / tour fini / en cours /
/// fantôme) + le compteur d'attentes, pour juger les 8 templates au coup d'œil. Aucune source réelle.
/// </summary>
public sealed class SessionsPreviewViewModel : ObservableObject
{
    public ObservableCollection<SessionItemVm> Items { get; } = new();
    public int WaitingCount => 2;
    public int TotalCount => Items.Count;
    public bool HasWaiting => WaitingCount > 0;
    public Orientation RowOrientation => Orientation.Horizontal;

    private static readonly Brush Amber = Frozen("#E9A23C");
    private static readonly Brush Green = Frozen("#3FB98A");
    private static readonly Brush Gray = Frozen("#7A7A85");

    public SessionsPreviewViewModel()
    {
        Add("overlay", "à toi", Amber, attention: true, kind: "Code", detail: "à l'instant");
        Add("api-migration", "tour fini", Amber, turn: true, kind: "Code", detail: "il y a 3 min");
        Add("chronos", "en cours", Green, working: true, kind: "Cowork", detail: "à l'instant");
        Add("docs-site", "en cours", Green, working: true, kind: "", detail: "il y a 1 min");
        Add("legacy-vm", "inconnu", Gray, ghost: true, kind: "", detail: "il y a 12 min");
    }

    private void Add(string project, string state, Brush brush, bool attention = false, bool turn = false,
                     bool working = false, bool ghost = false, string kind = "", string detail = "")
    {
        var it = new SessionItemVm(project, _ => { })   // archive no-op en prévisualisation
        {
            Project = project,
            StateText = state,
            StateBrush = brush,
            Detail = detail,
            KindLabel = kind,
            IsWaiting = attention || turn,
            IsAttention = attention,
            IsTurn = turn,
            IsWorking = working,
            IsGhost = ghost,
        };
        Items.Add(it);
    }

    private static Brush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
