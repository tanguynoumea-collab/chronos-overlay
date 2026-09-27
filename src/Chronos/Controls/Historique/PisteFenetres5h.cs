using System.Windows;
using System.Windows.Media;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;
using Chronos.Theming;

namespace Chronos.Controls.Historique;

/// <summary>
/// HIS-02 (style Tuiles) — la piste FENÊTRES 5 H : une tuile par fenêtre OBSERVÉE (du premier relevé qui porte un
/// <c>resets_at</c> jusqu'à ce <c>resets_at</c>, bornée à la plage), hauteur = max % 5 h, couleur = rampe du thème à ce max,
/// <see cref="Gris"/> si épuisée (compteur à 1 ou refus du serveur) — sans <see cref="Gris"/>, la tuile épuisée est sautée,
/// jamais peinte par la rampe ; un tiret <see cref="TraitReset"/> pleine hauteur à droite quand le reset est dans la plage.
/// </summary>
public sealed class PisteFenetres5h : PisteBase
{
    private static DependencyProperty Dp(string nom, Type type, object? defaut) => Rendu(nom, type, typeof(PisteFenetres5h), defaut);

    public static readonly DependencyProperty SerieProperty = Dp(nameof(Serie), typeof(IReadOnlyList<ReleveJournal>), null);
    public static readonly DependencyProperty RampeProperty = Dp(nameof(Rampe), typeof(ChronosTheme), null);
    public static readonly DependencyProperty GrisProperty = Dp(nameof(Gris), typeof(Brush), null);
    public static readonly DependencyProperty TraitResetProperty = Dp(nameof(TraitReset), typeof(Brush), null);

    public IReadOnlyList<ReleveJournal>? Serie { get => (IReadOnlyList<ReleveJournal>?)GetValue(SerieProperty); set => SetValue(SerieProperty, value); }
    public ChronosTheme? Rampe { get => (ChronosTheme?)GetValue(RampeProperty); set => SetValue(RampeProperty, value); }
    public Brush? Gris { get => (Brush?)GetValue(GrisProperty); set => SetValue(GrisProperty, value); }
    public Brush? TraitReset { get => (Brush?)GetValue(TraitResetProperty); set => SetValue(TraitResetProperty, value); }

    protected override void Dessiner(DrawingContext dc, double w, double h)
    {
        var plage = Plage!;
        if (Serie is not { } serie) return;
        var plumeReset = TraitReset is { } trait ? Plume(trait, EpaisseurFine) : null;

        foreach (var tuile in Tuiles5h.Depuis(serie, plage))
        {
            var brosse = tuile.Epuisee ? Gris : Rampe is null ? null : Rampe.ArcBrush(tuile.UMax);
            if (brosse is not null)
            {
                var y = EchelleValeur.Y(tuile.UMax, 1.0, h);
                dc.DrawRectangle(brosse, null, new Rect(EchelleTemps.X(tuile.Debut, plage, w), y, EchelleTemps.Largeur(tuile.Debut, tuile.Fin, plage, w), h - y));
                if (TracerPourTests)
                    Tracer($"tuile {F(EchelleTemps.Fraction(tuile.Debut, plage))} {F(EchelleTemps.Fraction(tuile.Fin, plage))} {F(tuile.UMax)} {(ReferenceEquals(brosse, Gris) ? "grise" : "rampe")} {F(h - y)}");
            }

            if (tuile.ResetDansPlage && plumeReset is not null)
            {
                var x = EchelleTemps.X(tuile.Fin, plage, w);
                dc.DrawLine(plumeReset, new Point(x, 0), new Point(x, h));
                Tracer($"tiret {F(EchelleTemps.Fraction(tuile.Fin, plage))}");
            }
        }
    }
}
