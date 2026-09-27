using System.Windows;
using System.Windows.Media;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;
using Chronos.Theming;

namespace Chronos.Controls.Historique;

/// <summary>
/// HIS-02 / HIS-04 — la piste RYTHME : une barre par heure, hauteur = somme des Δ du compteur 5 h arrivés dans l'heure
/// (jamais à travers un reset ni un trou, jamais un Δ anormal — D-34-10), couleur = rampe du thème au niveau 5 h ATTEINT
/// dans l'heure, sur l'échelle fixe « 0 – 25 % » (<see cref="Max"/>) : un Δ plus grand s'écrase au bord de la piste, il ne
/// sort pas. Toute la géométrie vient de <c>Binning.DeltasParHeure</c> ; la piste ne fait qu'un rectangle par barre.
/// </summary>
public sealed class PisteRythme : PisteBase
{
    private static DependencyProperty Dp(string nom, Type type, object? defaut) => Rendu(nom, type, typeof(PisteRythme), defaut);

    public static readonly DependencyProperty DeltasProperty = Dp(nameof(Deltas), typeof(IReadOnlyList<DeltaConsommation>), null);
    public static readonly DependencyProperty SerieProperty = Dp(nameof(Serie), typeof(IReadOnlyList<ReleveJournal>), null);
    public static readonly DependencyProperty RampeProperty = Dp(nameof(Rampe), typeof(ChronosTheme), null);
    public static readonly DependencyProperty MaxProperty = Dp(nameof(Max), typeof(double), EchelleValeur.MaxRythme);

    public IReadOnlyList<DeltaConsommation>? Deltas { get => (IReadOnlyList<DeltaConsommation>?)GetValue(DeltasProperty); set => SetValue(DeltasProperty, value); }
    public IReadOnlyList<ReleveJournal>? Serie { get => (IReadOnlyList<ReleveJournal>?)GetValue(SerieProperty); set => SetValue(SerieProperty, value); }
    public ChronosTheme? Rampe { get => (ChronosTheme?)GetValue(RampeProperty); set => SetValue(RampeProperty, value); }

    /// <summary>Le plafond de l'axe (fraction) : « 0 – 25 % » par défaut.</summary>
    public double Max { get => (double)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    protected override void Dessiner(DrawingContext dc, double w, double h)
    {
        var plage = Plage!;
        DessinerGrille(dc, w, h, 0.0);
        if (Deltas is not { } deltas || Serie is not { } serie || Rampe is not { } rampe) return;

        var air = EpaisseurFine / 2;   // un souffle entre deux barres voisines
        foreach (var barre in Binning.DeltasParHeure(deltas, serie, r => r.U5, plage))
        {
            var x0 = EchelleTemps.X(barre.Debut, plage, w) + air;
            var x1 = EchelleTemps.X(barre.Fin, plage, w) - air;
            if (x1 <= x0) x1 = x0 + EpaisseurFine;   // barre plus fine qu'un souffle : au moins un trait
            if (x1 <= x0) continue;

            var y = EchelleValeur.Y(barre.Delta, Max, h);
            dc.DrawRectangle(rampe.ArcBrush(barre.NiveauAtteint), null, new Rect(x0, y, x1 - x0, h - y));
            if (!TracerPourTests) continue;
            Tracer($"barre {F(EchelleTemps.Fraction(barre.Debut, plage))} {F(EchelleTemps.Fraction(barre.Fin, plage))} {F(barre.Delta)} {F(barre.NiveauAtteint)}");
            Tracer($"hauteur {F(h - y)}");
        }
    }
}
