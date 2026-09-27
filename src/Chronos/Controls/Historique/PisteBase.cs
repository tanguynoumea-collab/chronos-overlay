using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;

namespace Chronos.Controls.Historique;

/// <summary>
/// HIS-07 — la souche de toutes les PISTES de la fenêtre Historique : un <see cref="FrameworkElement"/> à <c>OnRender</c>
/// qui ne calcule rien (toute la géométrie vient de <c>Rendering/Historique</c>, pur et testé), ne choisit aucune couleur
/// (chaque brosse arrive par une DP dont le défaut est <c>null</c>, chaque épaisseur ou opacité par une DP <c>double</c> dont le
/// défaut est 0 — D-34-18 : une piste non bindée est invisible, jamais fausse) et ne se redessine que lorsqu'une DP
/// <c>AffectsRender</c> change : nouvelle référence de données, plage, brosses, taille — jamais au tick, parce qu'aucune de
/// ses DP n'est bindée à une propriété qui bouge au tick (le réticule et « maintenant » vivent dans <c>SurcoucheReticule</c>).
///
/// <para><b>D-34-17</b> — <see cref="OnRender"/> est SCELLÉ : il compte les rendus (<see cref="RendusPourTests"/>), vide la
/// trace, garde les tailles nulles et la plage absente, clippe au rectangle de la piste (les valeurs hors [0, 1] et la
/// semaine précédente peuvent déborder), pousse un <see cref="GuidelineSet"/> à 0,5 px (traits de 1 px nets à 100 % et 150 %)
/// puis délègue à <see cref="Dessiner"/>. La <b>trace de rendu</b> (<see cref="TraceRendu"/>, activée par
/// <see cref="TracerPourTests"/>) décrit chaque primitive dessinée en mots et en fractions de plage : les tests lisent la
/// trace, pas les pixels.</para>
/// </summary>
public abstract class PisteBase : FrameworkElement
{
    /// <summary>La plage affichée : l'axe du temps commun à toutes les pistes d'une vue. <c>null</c> = rien à dessiner.</summary>
    public static readonly DependencyProperty PlageProperty = Rendu(nameof(Plage), typeof(Plage), typeof(PisteBase), null);

    /// <summary>Brosse des lignes de grille horizontales (0 / 50 / 100 %). <c>null</c> = pas de grille.</summary>
    public static readonly DependencyProperty GrilleProperty = Rendu(nameof(Grille), typeof(Brush), typeof(PisteBase), null);

    /// <summary>Épaisseur des traits fins (grille, dents de scie, tirets, bordures pointillées, réticule) — token <c>HistoEpaisseurFin</c>.</summary>
    public static readonly DependencyProperty EpaisseurFineProperty = Rendu(nameof(EpaisseurFine), typeof(double), typeof(PisteBase), 0.0);

    public Plage? Plage { get => (Plage?)GetValue(PlageProperty); set => SetValue(PlageProperty, value); }
    public Brush? Grille { get => (Brush?)GetValue(GrilleProperty); set => SetValue(GrilleProperty, value); }
    public double EpaisseurFine { get => (double)GetValue(EpaisseurFineProperty); set => SetValue(EpaisseurFineProperty, value); }

    // Guides à mi-pixel, gelés une fois pour toutes : des traits de 1 px nets quel que soit le DPI.
    private static readonly GuidelineSet Guides = CreerGuides();

    /// <summary>Nombre d'appels d'<c>OnRender</c> depuis la construction (test « jamais au tick », niveau contrôle).</summary>
    internal int RendusPourTests;

    /// <summary>Nombre de changements de DP <c>AffectsRender</c> déclarées par <see cref="Rendu"/> (le repli de l'Open Question 4).</summary>
    internal int InvalidationsPourTests;

    /// <summary>Active la trace de rendu (tests seulement : l'application ne paie jamais les chaînes).</summary>
    internal static bool TracerPourTests;

    /// <summary>La trace du DERNIER rendu, une ligne par primitive dessinée (vidée à chaque <c>OnRender</c>).</summary>
    internal readonly List<string> TraceRendu = new();

    /// <summary>
    /// Déclare une DP <c>AffectsRender</c> d'une piste : un changement de valeur invalide le visuel (et compte une invalidation
    /// pour les tests). Toutes les DP de données, de brosses et de tailles des pistes passent ici.
    /// </summary>
    protected static DependencyProperty Rendu(string nom, Type type, Type proprietaire, object? defaut)
        => DependencyProperty.Register(nom, type, proprietaire,
            new FrameworkPropertyMetadata(defaut, FrameworkPropertyMetadataOptions.AffectsRender, SurInvalidation));

    private static void SurInvalidation(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PisteBase piste) piste.InvalidationsPourTests++;
    }

    /// <summary>D-34-17 : compteur, trace, gardes, clip, guides, puis <see cref="Dessiner"/>.</summary>
    protected sealed override void OnRender(DrawingContext dc)
    {
        RendusPourTests++;
        if (TracerPourTests) TraceRendu.Clear();

        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0 || Plage is null) return;

        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
        dc.PushGuidelineSet(Guides);
        Dessiner(dc, w, h);
        dc.Pop();
        dc.Pop();
    }

    /// <summary>Le dessin propre à la piste, avec une largeur et une hauteur strictement positives et une <see cref="Plage"/> non nulle.</summary>
    protected abstract void Dessiner(DrawingContext dc, double w, double h);

    /// <summary>Ajoute une ligne à la trace de rendu (sans effet hors tests).</summary>
    protected void Tracer(string ligne)
    {
        if (TracerPourTests) TraceRendu.Add(ligne);
    }

    /// <summary>Une fraction ou une ordonnée formatée pour la trace (invariant, trois décimales).</summary>
    protected static string F(double valeur) => valeur.ToString("0.000", CultureInfo.InvariantCulture);

    /// <summary>Un trait plein gelé.</summary>
    protected static Pen Plume(Brush brosse, double epaisseur)
    {
        var plume = new Pen(brosse, epaisseur);
        plume.Freeze();
        return plume;
    }

    /// <summary>Un trait pointillé gelé (tirets 3, espaces 2) : semaine précédente, bordures de trou, cadres de divergence.</summary>
    protected static Pen PlumePointillee(Brush brosse, double epaisseur)
    {
        var plume = new Pen(brosse, epaisseur) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0) };
        plume.Freeze();
        return plume;
    }

    /// <summary>
    /// L'escalier d'une liste de paliers : un trait horizontal par palier, et une jonction verticale vers le palier suivant
    /// SEULEMENT si celui-ci commence là où le précédent finit (<c>T1 == T0 suivant</c>). Un palier de longueur nulle au bord
    /// d'un trou n'a pas de jonction : c'est là que la ligne s'interrompt (D-34-08). Géométrie gelée.
    /// </summary>
    protected static StreamGeometry GeometrieEscalier(IReadOnlyList<SegmentPalier> segments, Plage plage, double w, double h, double max)
    {
        var geometrie = new StreamGeometry();
        using (var ctx = geometrie.Open())
        {
            for (var i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                var y = EchelleValeur.Y(s.U, max, h);
                var x1 = EchelleTemps.X(s.T1, plage, w);
                ctx.BeginFigure(new Point(EchelleTemps.X(s.T0, plage, w), y), false, false);
                ctx.LineTo(new Point(x1, y), true, false);
                if (i + 1 < segments.Count && segments[i + 1].T0 == s.T1)
                    ctx.LineTo(new Point(x1, EchelleValeur.Y(segments[i + 1].U, max, h)), true, false);
            }
        }
        geometrie.Freeze();
        return geometrie;
    }

    /// <summary>Les lignes de grille horizontales aux <paramref name="fractions"/> données (0 = bas, 1 = haut), si <see cref="Grille"/> et <see cref="EpaisseurFine"/> sont posées.</summary>
    protected void DessinerGrille(DrawingContext dc, double w, double h, params double[] fractions)
    {
        if (Grille is not { } grille || EpaisseurFine <= 0) return;
        var plume = Plume(grille, EpaisseurFine);
        foreach (var f in fractions)
        {
            var y = EchelleValeur.Y(f, 1.0, h);
            dc.DrawLine(plume, new Point(0, y), new Point(w, y));
            Tracer($"grille {F(f)}");
        }
    }

    private static GuidelineSet CreerGuides()
    {
        var guides = new GuidelineSet();
        guides.GuidelinesX.Add(0.5);
        guides.GuidelinesY.Add(0.5);
        guides.Freeze();
        return guides;
    }
}
