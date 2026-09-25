using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;
using Chronos.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>
/// VM de la galerie de prévisualisation des styles de sessions (prototype, lancé via « --sessions »).
/// Expose des <see cref="SessionItemVm"/> d'ÉCHANTILLON couvrant les trois mots du widget — « En attente »
/// (demande et tour fini), « En attente ? » (attente déduite), « Réflexion » — et le compteur d'attentes, pour
/// juger les 8 templates au coup d'œil. Aucune source réelle.
/// <para>Les mots viennent du producteur unique (<see cref="AffichageSessions"/>), jamais d'un littéral : la
/// galerie partage les gabarits de l'écran, elle doit parler comme lui.</para>
/// </summary>
public sealed class SessionsPreviewViewModel : ObservableObject
{
    public ObservableCollection<SessionItemVm> Items { get; } = new();
    public int WaitingCount => 3;
    /// <summary>Mot du compteur de l'Annonciateur, tiré du producteur : jamais en dur dans un gabarit.</summary>
    public string LibelleCompteur => AffichageSessions.EnAttente;
    public int TotalCount => Items.Count;
    public bool HasWaiting => WaitingCount > 0;
    public Orientation RowOrientation => Orientation.Horizontal;

    private static readonly Brush Amber = Frozen("#E9A23C");
    private static readonly Brush Green = Frozen("#3FB98A");

    public SessionsPreviewViewModel()
    {
        Add("overlay", AffichageSessions.Etat(SessionActivity.WaitingAttention), Amber, attention: true, detail: "à l'instant");
        Add("api-migration", AffichageSessions.Etat(SessionActivity.WaitingTurn), Amber, turn: true, detail: "il y a 3 min");
        Add("chronos", AffichageSessions.Etat(SessionActivity.Working), Green, working: true, detail: "à l'instant");
        Add("docs-site", AffichageSessions.Etat(SessionActivity.Working), Green, working: true, detail: "il y a 1 min");
        Add("interrompu", AffichageSessions.Etat(SessionActivity.WaitingDeduced), Amber, turn: true, detail: "il y a 25 min");
    }

    private void Add(string project, string state, Brush brush, bool attention = false, bool turn = false,
                     bool working = false, string detail = "")
    {
        var it = new SessionItemVm(project, 0, _ => { }, _ => { }, () => { })   // gestes no-op en prévisualisation
        {
            Project = project,
            StateText = state,
            StateBrush = brush,
            Detail = detail,
            IsWaiting = attention || turn,
            IsAttention = attention,
            IsTurn = turn,
            IsWorking = working,
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
