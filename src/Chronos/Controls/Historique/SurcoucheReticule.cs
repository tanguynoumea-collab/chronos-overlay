using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;
using Chronos.Text;

namespace Chronos.Controls.Historique;

/// <summary>
/// HIS-02 / HIS-04 — la SURCOUCHE posée sur toutes les pistes d'une vue : le réticule vertical du survol, l'infobulle à
/// quatre lignes (<c>TextesHistorique.Infobulle</c> : l'heure DU RELEVÉ, jamais celle du pointeur) et la ligne « maintenant »
/// de la vue Jour. Elle est la SEULE à bouger au survol et au tick : les pistes, elles, n'ont aucune DP bindée à ce qui
/// change chaque seconde (HIS-07). Le fond est un rectangle transparent pour rester hit-testable (le seul pinceau nommé
/// toléré par la garde de 34-01 — D-34-18).
///
/// <para>Les résultats du survol sont des DP en LECTURE SEULE : <see cref="XReticule"/> (dessinée ici, <c>AffectsRender</c>),
/// <see cref="TexteInfobulle"/>, <see cref="InfobulleVisible"/> et <see cref="InstantSurvole"/> (bindées par la vue à un
/// <c>Border</c> d'infobulle, elles n'invalident pas le rendu).</para>
/// </summary>
public sealed class SurcoucheReticule : PisteBase
{
    private static DependencyProperty Dp(string nom, Type type, object? defaut) => Rendu(nom, type, typeof(SurcoucheReticule), defaut);

    public static readonly DependencyProperty SerieProperty = Dp(nameof(Serie), typeof(IReadOnlyList<ReleveJournal>), null);
    public static readonly DependencyProperty MaintenantProperty = Dp(nameof(Maintenant), typeof(DateTimeOffset?), null);
    public static readonly DependencyProperty AfficherMaintenantProperty = Dp(nameof(AfficherMaintenant), typeof(bool), false);
    public static readonly DependencyProperty InstantLectureProperty = Dp(nameof(InstantLecture), typeof(DateTimeOffset), default(DateTimeOffset));
    public static readonly DependencyProperty FuseauProperty = Dp(nameof(Fuseau), typeof(TimeZoneInfo), null);
    public static readonly DependencyProperty TraitReticuleProperty = Dp(nameof(TraitReticule), typeof(Brush), null);
    public static readonly DependencyProperty TraitMaintenantProperty = Dp(nameof(TraitMaintenant), typeof(Brush), null);
    public static readonly DependencyProperty OpaciteMaintenantProperty = Dp(nameof(OpaciteMaintenant), typeof(double), 0.0);

    private static readonly DependencyPropertyKey XReticuleKey = DependencyProperty.RegisterReadOnly(nameof(XReticule), typeof(double), typeof(SurcoucheReticule),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyPropertyKey TexteInfobulleKey = DependencyProperty.RegisterReadOnly(nameof(TexteInfobulle), typeof(string), typeof(SurcoucheReticule),
        new FrameworkPropertyMetadata(""));
    private static readonly DependencyPropertyKey InfobulleVisibleKey = DependencyProperty.RegisterReadOnly(nameof(InfobulleVisible), typeof(bool), typeof(SurcoucheReticule),
        new FrameworkPropertyMetadata(false));
    private static readonly DependencyPropertyKey InstantSurvoleKey = DependencyProperty.RegisterReadOnly(nameof(InstantSurvole), typeof(DateTimeOffset?), typeof(SurcoucheReticule),
        new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty XReticuleProperty = XReticuleKey.DependencyProperty;
    public static readonly DependencyProperty TexteInfobulleProperty = TexteInfobulleKey.DependencyProperty;
    public static readonly DependencyProperty InfobulleVisibleProperty = InfobulleVisibleKey.DependencyProperty;
    public static readonly DependencyProperty InstantSurvoleProperty = InstantSurvoleKey.DependencyProperty;

    public IReadOnlyList<ReleveJournal>? Serie { get => (IReadOnlyList<ReleveJournal>?)GetValue(SerieProperty); set => SetValue(SerieProperty, value); }
    public DateTimeOffset? Maintenant { get => (DateTimeOffset?)GetValue(MaintenantProperty); set => SetValue(MaintenantProperty, value); }
    public bool AfficherMaintenant { get => (bool)GetValue(AfficherMaintenantProperty); set => SetValue(AfficherMaintenantProperty, value); }
    public DateTimeOffset InstantLecture { get => (DateTimeOffset)GetValue(InstantLectureProperty); set => SetValue(InstantLectureProperty, value); }
    public TimeZoneInfo? Fuseau { get => (TimeZoneInfo?)GetValue(FuseauProperty); set => SetValue(FuseauProperty, value); }
    public Brush? TraitReticule { get => (Brush?)GetValue(TraitReticuleProperty); set => SetValue(TraitReticuleProperty, value); }
    public Brush? TraitMaintenant { get => (Brush?)GetValue(TraitMaintenantProperty); set => SetValue(TraitMaintenantProperty, value); }
    public double OpaciteMaintenant { get => (double)GetValue(OpaciteMaintenantProperty); set => SetValue(OpaciteMaintenantProperty, value); }

    /// <summary>Abscisse du relevé survolé (pixels de la surcouche), 0 quand rien n'est survolé.</summary>
    public double XReticule => (double)GetValue(XReticuleProperty);

    /// <summary>Les quatre lignes de l'infobulle, ou vide.</summary>
    public string TexteInfobulle => (string)GetValue(TexteInfobulleProperty);

    /// <summary>Un relevé est survolé.</summary>
    public bool InfobulleVisible => (bool)GetValue(InfobulleVisibleProperty);

    /// <summary>L'instant du relevé survolé (pas celui du pointeur), ou <c>null</c>.</summary>
    public DateTimeOffset? InstantSurvole => (DateTimeOffset?)GetValue(InstantSurvoleProperty);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Survoler(e.GetPosition(this).X);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        Quitter();
    }

    /// <summary>Le pointeur est à l'abscisse <paramref name="x"/> : montrer le relevé le plus proche (borné à la plage).</summary>
    internal void Survoler(double x)
    {
        if (Plage is not { } plage || Serie is not { } serie || serie.Count == 0)
        {
            Quitter();
            return;
        }

        var largeur = ActualWidth;
        var releve = Reticule.PlusProche(serie, EchelleTemps.Instant(x, plage, largeur));
        if (releve is null)
        {
            Quitter();
            return;
        }

        SetValue(InstantSurvoleKey, releve.T);
        SetValue(XReticuleKey, EchelleTemps.X(releve.T, plage, largeur));
        SetValue(TexteInfobulleKey, Fuseau is { } fuseau ? TextesHistorique.Infobulle(releve, Maintenant ?? InstantLecture, fuseau) : "");
        SetValue(InfobulleVisibleKey, true);
        InvalidateVisual();
    }

    /// <summary>Le pointeur est parti : plus de réticule ni d'infobulle.</summary>
    internal void Quitter()
    {
        SetValue(InfobulleVisibleKey, false);
        SetValue(TexteInfobulleKey, "");
        SetValue(InstantSurvoleKey, null);
        SetValue(XReticuleKey, 0.0);
        InvalidateVisual();
    }

    protected override void Dessiner(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        var plage = Plage!;

        if (InfobulleVisible && TraitReticule is { } traitReticule)
        {
            var x = XReticule;
            dc.DrawLine(Plume(traitReticule, EpaisseurFine), new Point(x, 0), new Point(x, h));
            Tracer($"reticule {F(x / w)}");
        }

        if (AfficherMaintenant && Maintenant is { } maintenant && plage.Contient(maintenant) && TraitMaintenant is { } traitMaintenant)
        {
            var x = EchelleTemps.X(maintenant, plage, w);
            dc.PushOpacity(OpaciteMaintenant);
            dc.DrawLine(Plume(traitMaintenant, EpaisseurFine), new Point(x, 0), new Point(x, h));
            dc.Pop();
            Tracer($"maintenant {F(EchelleTemps.Fraction(maintenant, plage))}");
        }
    }
}
