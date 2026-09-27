using System.Windows;
using System.Windows.Media;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;

namespace Chronos.Controls.Historique;

/// <summary>
/// HIS-06 (côté rendu) — la bande COUVERTURE, toujours visible, jamais repliable : <see cref="Present"/> à
/// <see cref="OpacitePresent"/> du premier au dernier relevé de la série (relevés présents), puis un rectangle opaque par
/// trou — <see cref="Arrete"/> (gris) si « Chronos arrêté » ou cause inconnue, <see cref="Jeton"/> (ambre) si « jeton
/// invalide » ou « sonde refusée ». Un trou encore ouvert finit à <see cref="InstantLecture"/>.
/// </summary>
public sealed class PisteCouverture : PisteBase
{
    private static DependencyProperty Dp(string nom, Type type, object? defaut) => Rendu(nom, type, typeof(PisteCouverture), defaut);

    public static readonly DependencyProperty SerieProperty = Dp(nameof(Serie), typeof(IReadOnlyList<ReleveJournal>), null);
    public static readonly DependencyProperty TrousProperty = Dp(nameof(Trous), typeof(IReadOnlyList<Trou>), null);
    public static readonly DependencyProperty InstantLectureProperty = Dp(nameof(InstantLecture), typeof(DateTimeOffset), default(DateTimeOffset));
    public static readonly DependencyProperty PresentProperty = Dp(nameof(Present), typeof(Brush), null);
    public static readonly DependencyProperty OpacitePresentProperty = Dp(nameof(OpacitePresent), typeof(double), 0.0);
    public static readonly DependencyProperty ArreteProperty = Dp(nameof(Arrete), typeof(Brush), null);
    public static readonly DependencyProperty JetonProperty = Dp(nameof(Jeton), typeof(Brush), null);

    public IReadOnlyList<ReleveJournal>? Serie { get => (IReadOnlyList<ReleveJournal>?)GetValue(SerieProperty); set => SetValue(SerieProperty, value); }
    public IReadOnlyList<Trou>? Trous { get => (IReadOnlyList<Trou>?)GetValue(TrousProperty); set => SetValue(TrousProperty, value); }
    public DateTimeOffset InstantLecture { get => (DateTimeOffset)GetValue(InstantLectureProperty); set => SetValue(InstantLectureProperty, value); }
    public Brush? Present { get => (Brush?)GetValue(PresentProperty); set => SetValue(PresentProperty, value); }
    public double OpacitePresent { get => (double)GetValue(OpacitePresentProperty); set => SetValue(OpacitePresentProperty, value); }
    public Brush? Arrete { get => (Brush?)GetValue(ArreteProperty); set => SetValue(ArreteProperty, value); }
    public Brush? Jeton { get => (Brush?)GetValue(JetonProperty); set => SetValue(JetonProperty, value); }

    protected override void Dessiner(DrawingContext dc, double w, double h)
    {
        var plage = Plage!;

        if (Serie is { Count: > 0 } serie && Present is { } present)
        {
            var premier = serie[0].T;
            var dernier = serie[^1].T;
            dc.PushOpacity(OpacitePresent);
            dc.DrawRectangle(present, null, new Rect(EchelleTemps.X(premier, plage, w), 0, EchelleTemps.Largeur(premier, dernier, plage, w), h));
            dc.Pop();
            Tracer($"present {F(EchelleTemps.Fraction(premier, plage))} {F(EchelleTemps.Fraction(dernier, plage))} {F(OpacitePresent)}");
        }

        if (Trous is not { } trous) return;
        foreach (var rect in Escalier.RectanglesTrous(trous, plage, InstantLecture))
        {
            var jeton = rect.Trou.Cause is CauseTrou.JetonInvalide or CauseTrou.SondeRefusee;
            if ((jeton ? Jeton : Arrete) is not { } brosse) continue;
            dc.DrawRectangle(brosse, null, new Rect(EchelleTemps.X(rect.Debut, plage, w), 0, EchelleTemps.Largeur(rect.Debut, rect.Fin, plage, w), h));
            Tracer($"trou {F(EchelleTemps.Fraction(rect.Debut, plage))} {F(EchelleTemps.Fraction(rect.Fin, plage))} {(jeton ? "jeton" : "arrete")}");
        }
    }
}
