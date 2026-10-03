using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Chronos.Rendering;

/// <summary>
/// Géométrie PURE par axe des cadrans rectangulaires (CAD-03) : Fusible, Marée, Volets.
/// Les contrôles de <c>Controls/Cadrans</c> ne font que consommer ces fonctions ; tout se prouve en [Fact]
/// sans fenêtre (Rect/Point/Size sont des structures), seule la ligne d'eau construit une Geometry gelée.
///
/// Conventions de sens (maquette cycle 2) :
/// - Fusible horizontal : le cordon restant est à DROITE du front ; vertical : il reste en BAS (brûle de haut en bas).
/// - Marée horizontale : la lumière part de la GAUCHE ; verticale : depuis le HAUT.
/// - Volets : allumés de l'indice 0 vers n − 1 (gauche → droite, haut → bas).
/// </summary>
public static class GeometrieCadrans
{
    /// <summary>Épaisseur du sillon du Fusible (maquette : 5,4).</summary>
    public const double SillonFusible = 5.4;

    /// <summary>Écart entre deux volets (maquette : 2,5).</summary>
    public const double EcartVolets = 2.5;

    /// <summary>Amplitude de l'ondulation de la ligne d'eau de la Marée.</summary>
    public const double AmplitudeOndulation = 3;

    /// <summary>Pas des traits du grain « plancher » de la Marée.</summary>
    public const double PasGrain = 4;

    /// <summary>
    /// Géométrie d'un Fusible : sillon, cordon restant, cordon d'attente (pleine longueur, 0,6 × épaisseur),
    /// front (centre de l'étincelle), rayon de l'étincelle, et segment du trait brisé « plancher » (du front au bout).
    /// </summary>
    public readonly record struct GeometrieFusible(
        Rect Sillon, Rect Cordon, Rect Attente, Point Front, double RayonEtincelle, Point DebutGrain, Point FinGrain);

    /// <summary>Géométrie d'une Marée : canal, lumière, position de la ligne d'eau sur l'axe, visibilité de la ligne.</summary>
    public readonly record struct GeometrieMaree(Rect Canal, Rect Lumiere, double PositionLigne, bool LigneVisible);

    /// <summary>Fraction normalisée : NaN → 0, bornée à [0, 1].</summary>
    private static double Normaliser(double fraction) => double.IsNaN(fraction) ? 0 : Math.Clamp(fraction, 0, 1);

    /// <summary>
    /// Fusible dans <paramref name="taille"/> selon <paramref name="axe"/>. La fraction est la part RESTANTE :
    /// le front se place à w − w·f (horizontal) ou h − h·f (vertical), le cordon s'étend du front jusqu'au bout.
    /// </summary>
    public static GeometrieFusible Fusible(Size taille, Orientation axe, double fraction, double epaisseur)
    {
        double f = Normaliser(fraction);
        double w = taille.Width, h = taille.Height, th = epaisseur;
        double rayon = th / 2 + 1.5;
        double demiSillon = SillonFusible / 2;

        if (axe == Orientation.Horizontal)
        {
            double cy = h / 2;
            double fx = w - w * f;
            return new GeometrieFusible(
                Sillon: new Rect(0, cy - demiSillon, w, SillonFusible),
                Cordon: new Rect(fx, cy - th / 2, w - fx, th),
                Attente: new Rect(0, cy - 0.3 * th, w, 0.6 * th),
                Front: new Point(fx, cy),
                RayonEtincelle: rayon,
                DebutGrain: new Point(fx, cy),
                FinGrain: new Point(w, cy));
        }

        // Vertical : le cordon RESTE EN BAS, le front descend (brûle de haut en bas).
        double cx = w / 2;
        double fy = h - h * f;
        return new GeometrieFusible(
            Sillon: new Rect(cx - demiSillon, 0, SillonFusible, h),
            Cordon: new Rect(cx - th / 2, fy, th, h - fy),
            Attente: new Rect(cx - 0.3 * th, 0, 0.6 * th, h),
            Front: new Point(cx, fy),
            RayonEtincelle: rayon,
            DebutGrain: new Point(cx, fy),
            FinGrain: new Point(cx, h));
    }

    /// <summary>
    /// Marée dans <paramref name="taille"/> : la lumière occupe la fraction f depuis la gauche (horizontal)
    /// ou depuis le haut (vertical). La ligne d'eau n'est visible que strictement entre 0,1 % et 99,9 %.
    /// </summary>
    public static GeometrieMaree Maree(Size taille, Orientation axe, double fraction)
    {
        double f = Normaliser(fraction);
        double w = taille.Width, h = taille.Height;
        var canal = new Rect(0, 0, w, h);
        bool visible = f > 0.001 && f < 0.999;
        return axe == Orientation.Horizontal
            ? new GeometrieMaree(canal, new Rect(0, 0, w * f, h), w * f, visible)
            : new GeometrieMaree(canal, new Rect(0, 0, w, h * f), h * f, visible);
    }

    /// <summary>
    /// Ligne d'eau ondulée verticale de la Marée horizontale, en x, sur la hauteur h : deux Bézier quadratiques
    /// (maquette <c>M x,0 q a h/4 0 h/2 q −a h/4 0 h/2</c>). Géométrie gelée, partageable entre threads.
    /// </summary>
    public static Geometry LigneDEauOndulee(double x, double h, double amplitude = AmplitudeOndulation)
    {
        var figure = new PathFigure { StartPoint = new Point(x, 0), IsClosed = false, IsFilled = false };
        figure.Segments.Add(new QuadraticBezierSegment(new Point(x + amplitude, h / 4), new Point(x, h / 2), true));
        figure.Segments.Add(new QuadraticBezierSegment(new Point(x - amplitude, 3 * h / 4), new Point(x, h), true));
        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        geo.Freeze();
        return geo;
    }

    /// <summary>
    /// Grain « plancher » de la Marée : traits perpendiculaires à l'axe, tous les <paramref name="pas"/> px,
    /// en retrait de 2 px sur les bords de la lumière (comportement historique de TideColumn).
    /// </summary>
    public static IReadOnlyList<(Point A, Point B)> GrainMaree(Rect lumiere, Orientation axe, double pas = PasGrain)
    {
        var traits = new List<(Point A, Point B)>();
        if (pas <= 0) return traits;
        if (axe == Orientation.Horizontal)
        {
            for (double gx = lumiere.X + 3; gx < lumiere.Right - 1; gx += pas)
                traits.Add((new Point(gx, lumiere.Y + 2), new Point(gx, lumiere.Bottom - 2)));
        }
        else
        {
            for (double gy = lumiere.Y + 3; gy < lumiere.Bottom - 1; gy += pas)
                traits.Add((new Point(lumiere.X + 2, gy), new Point(lumiere.Right - 2, gy)));
        }
        return traits;
    }

    /// <summary>
    /// Rectangles des <paramref name="n"/> volets (n ≥ 1) séparés de <paramref name="ecart"/> : en ligne (horizontal)
    /// ou empilés (vertical). Liste vide si la place ne suffit pas.
    /// </summary>
    public static IReadOnlyList<Rect> Volets(Size taille, Orientation axe, int n, double ecart = EcartVolets)
    {
        n = Math.Max(1, n);
        var volets = new List<Rect>(n);
        if (axe == Orientation.Horizontal)
        {
            double fw = (taille.Width - (n - 1) * ecart) / n;
            if (fw <= 0) return volets;
            for (int i = 0; i < n; i++)
                volets.Add(new Rect(i * (fw + ecart), 0, fw, taille.Height));
        }
        else
        {
            double fh = (taille.Height - (n - 1) * ecart) / n;
            if (fh <= 0) return volets;
            for (int i = 0; i < n; i++)
                volets.Add(new Rect(0, i * (fh + ecart), taille.Width, fh));
        }
        return volets;
    }

    /// <summary>Nombre de volets allumés : Round(f · n), milieu arrondi loin de zéro ; NaN / négatif → 0.</summary>
    public static int VoletsAllumes(double fraction, int n)
        => (int)Math.Round(Normaliser(fraction) * Math.Max(1, n), MidpointRounding.AwayFromZero);
}
