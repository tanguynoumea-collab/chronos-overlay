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
/// galerie partage les gabarits de l'écran, elle doit parler comme lui. Le nom et l'info-bulle aussi (phase 29,
/// Piège 7 de la phase 28 : une info-bulle non posée serait vide, sans erreur).</para>
/// <para>Un titre LONG et un motif sont échantillonnés pour juger la coupe et l'info-bulle : « api-migration » porte
/// un titre de 51 caractères, au-delà du plus long titre réel mesuré (43) ; « overlay » une permission demandée,
/// dont l'info-bulle a deux lignes.</para>
/// </summary>
public sealed class SessionsPreviewViewModel : ObservableObject
{
    public ObservableCollection<SessionItemVm> Items { get; } = new();
    public int WaitingCount => 3;
    /// <summary>Mot du compteur de l'Annonciateur, tiré du producteur : jamais en dur dans un gabarit.</summary>
    public string LibelleCompteur => AffichageSessions.EnAttente;
    public int TotalCount => Items.Count;
    public bool HasWaiting => WaitingCount > 0;
    /// <summary>Rangée (défaut) ou colonne pour les styles en rangée — réglable pour que l'aperçu des réglages suive
    /// « Disposition verticale » ; la galerie garde la rangée.</summary>
    public Orientation RowOrientation
    {
        get => _rowOrientation;
        set => SetProperty(ref _rowOrientation, value);
    }
    private Orientation _rowOrientation = Orientation.Horizontal;

    private static readonly Brush Amber = Frozen("#E9A23C");
    private static readonly Brush Green = Frozen("#3FB98A");

    // Aucune horloge en prévisualisation : le détail temporel est écrit, l'instant ne sert à rien.
    private static readonly System.DateTimeOffset Instant = System.DateTimeOffset.UnixEpoch;

    public SessionsPreviewViewModel()
    {
        Add(new SessionSnapshot("overlay", "overlay", SessionActivity.WaitingAttention, "PermissionRequest", Instant),
            Amber, attention: true, detail: "à l'instant");
        Add(new SessionSnapshot("api-migration", "api-migration", SessionActivity.WaitingTurn, null, Instant,
                Titre: "Migration de l'API de facturation vers la v2 du SDK"),
            Amber, turn: true, detail: "il y a 3 min");
        Add(new SessionSnapshot("chronos", "chronos", SessionActivity.Working, null, Instant),
            Green, working: true, detail: "à l'instant");
        Add(new SessionSnapshot("docs-site", "docs-site", SessionActivity.Working, null, Instant),
            Green, working: true, detail: "il y a 1 min");
        Add(new SessionSnapshot("interrompu", "interrompu", SessionActivity.WaitingDeduced, null, Instant),
            Amber, deduced: true, detail: "il y a 25 min");
    }

    /// <summary>Un échantillon, mis en forme par le MÊME producteur que le widget : nom, mot, info-bulle.</summary>
    private void Add(SessionSnapshot s, Brush brush, bool attention = false, bool turn = false,
                     bool working = false, bool deduced = false, string detail = "")
    {
        var it = new SessionItemVm(s.SessionId, 0, _ => { }, _ => { }, () => { })   // gestes no-op en prévisualisation
        {
            Project = AffichageSessions.Nom(s),
            StateText = AffichageSessions.Etat(s.Activity),
            Infobulle = AffichageSessions.Infobulle(s),
            StateBrush = brush,
            Detail = detail,
            IsWaiting = attention || turn || deduced,
            IsAttention = attention,
            IsTurn = turn || deduced,        // la déduction garde la forme de la famille des attentes
            IsWorking = working,
            IsDeduced = deduced,
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
