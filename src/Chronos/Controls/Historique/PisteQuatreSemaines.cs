using System.Windows;
using System.Windows.Media;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;
using Chronos.Theming;

namespace Chronos.Controls.Historique;

/// <summary>
/// HIS-05 (DESIGN_PLAN §2.4) — la piste NIVEAU de la vue 4 semaines : quatre semaines de forfait superposées sur un même axe
/// samedi → samedi. S-3, S-2, S-1 en escaliers <see cref="Gris"/> qui s'effacent avec l'âge (<see cref="OpaciteS3"/>,
/// <see cref="OpaciteS2"/>, <see cref="OpaciteS1"/>, liées aux tokens <c>HistoOpaciteSemaine3/2/1</c>), puis la semaine
/// courante S par bandes de la rampe du thème actif (<see cref="Rampe"/>), la bande « épuisé » en gris — un plateau épuisé ne
/// prend jamais la rampe.
///
/// <para><b>D-35-13 — une piste dédiée</b> : une grille (0 / 50 / 100 %, dessinée UNE fois), un ordre de peinture (S-3 le plus
/// pâle d'abord, S en dernier, par-dessus), une trace. <c>PisteNiveau</c> n'est pas touchée. Chaque escalier se projette par
/// fraction de SA plage (<c>GeometrieEscalier(segs, a.Plage, …)</c> → <see cref="EchelleTemps"/>) : une semaine de 169 h (passage
/// à l'heure d'hiver) tombe sur le même axe de sept colonnes que les autres. Un refus serveur (<c>Statut7 == Rejete</c>) compte
/// comme 1,0 : « épuisée » monte à 100 %, en gris.</para>
///
/// <para><b>Pourquoi une opacité poussée dans le contexte de dessin</b> : l'opacité est appliquée À L'INTÉRIEUR de la piste, primitive par primitive, et non
/// par <c>UIElement.Opacity</c> : la grille et la courante restent à pleine opacité, seuls les fantômes s'effacent — et la
/// brosse <see cref="Gris"/> reste la brosse du token (jamais une copie atténuée fabriquée en C#).</para>
///
/// <para>Une semaine sans relevé (antérieure au journal, ou vide) ne dessine RIEN et ne trace rien : rien n'est inventé
/// avant l'ouverture du journal. Pas de dents 5 h, de tirets, de trous, de sauts ni de divergences ici : §2.4 ne montre que
/// les niveaux hebdo ; la couverture de chaque semaine a sa propre rangée dans la vue.</para>
/// </summary>
public sealed class PisteQuatreSemaines : PisteBase
{
    private static DependencyProperty Dp(string nom, Type type, object? defaut) => Rendu(nom, type, typeof(PisteQuatreSemaines), defaut);

    // --- données : les quatre analyses du VM, ordre CHRONOLOGIQUE ([0] = S-3 … [3] = S) ---
    public static readonly DependencyProperty SemainesProperty = Dp(nameof(Semaines), typeof(IReadOnlyList<AnalyseJournal>), null);

    // --- brosses (thème ou tokens ; null = la primitive n'est pas dessinée) ---
    public static readonly DependencyProperty RampeProperty = Dp(nameof(Rampe), typeof(ChronosTheme), null);
    public static readonly DependencyProperty GrisProperty = Dp(nameof(Gris), typeof(Brush), null);

    // --- tailles et opacités (tokens sys:Double) ---
    public static readonly DependencyProperty EpaisseurEscalierProperty = Dp(nameof(EpaisseurEscalier), typeof(double), 0.0);
    public static readonly DependencyProperty OpaciteS1Property = Dp(nameof(OpaciteS1), typeof(double), 0.0);
    public static readonly DependencyProperty OpaciteS2Property = Dp(nameof(OpaciteS2), typeof(double), 0.0);
    public static readonly DependencyProperty OpaciteS3Property = Dp(nameof(OpaciteS3), typeof(double), 0.0);
    public static readonly DependencyProperty NbBandesProperty = Dp(nameof(NbBandes), typeof(int), 12);

    public IReadOnlyList<AnalyseJournal>? Semaines { get => (IReadOnlyList<AnalyseJournal>?)GetValue(SemainesProperty); set => SetValue(SemainesProperty, value); }
    public ChronosTheme? Rampe { get => (ChronosTheme?)GetValue(RampeProperty); set => SetValue(RampeProperty, value); }
    public Brush? Gris { get => (Brush?)GetValue(GrisProperty); set => SetValue(GrisProperty, value); }
    public double EpaisseurEscalier { get => (double)GetValue(EpaisseurEscalierProperty); set => SetValue(EpaisseurEscalierProperty, value); }
    public double OpaciteS1 { get => (double)GetValue(OpaciteS1Property); set => SetValue(OpaciteS1Property, value); }
    public double OpaciteS2 { get => (double)GetValue(OpaciteS2Property); set => SetValue(OpaciteS2Property, value); }
    public double OpaciteS3 { get => (double)GetValue(OpaciteS3Property); set => SetValue(OpaciteS3Property, value); }
    public int NbBandes { get => (int)GetValue(NbBandesProperty); set => SetValue(NbBandesProperty, value); }

    // L'hebdo d'un relevé : un refus serveur compte comme épuisé (1,0), sinon l'utilisation brute.
    private static double? Hebdo(ReleveJournal r) => r.Statut7 == StatutServeur.Rejete ? 1.0 : r.U7;

    private double Opacite(int age) => age switch { 1 => OpaciteS1, 2 => OpaciteS2, _ => OpaciteS3 };

    protected override void Dessiner(DrawingContext dc, double w, double h)
    {
        if (Semaines is not { Count: 4 } semaines) return;
        DessinerGrille(dc, w, h, 0.0, 0.5, 1.0);

        // Les fantômes, du plus ancien (le plus pâle) au plus récent.
        for (var age = 3; age >= 1; age--)
        {
            var a = semaines[3 - age];
            var segments = Escalier.Segments(a.Serie, a.Trous, Hebdo, a.Plage);
            if (segments.Count == 0 || Gris is not { } gris) continue;   // avant le journal : rien n'est dessiné

            var opacite = Opacite(age);
            Brush brosse = gris;                                          // un fantôme ne prend JAMAIS la rampe
            dc.PushOpacity(opacite);
            dc.DrawGeometry(null, Plume(brosse, EpaisseurEscalier), GeometrieEscalier(segments, a.Plage, w, h, 1.0));
            dc.Pop();

            if (!TracerPourTests) continue;
            var nature = ReferenceEquals(brosse, Gris) ? "gris" : "rampe";   // la brosse qui a PEINT
            Tracer($"semaine {age} {segments.Count} {F(opacite)} {F(segments.Max(s => s.U))} {nature}");
        }

        DessinerCourante(dc, semaines[3], w, h);
    }

    // La courante, comme l'escalier hebdo de PisteNiveau (D-34-09) : un pinceau gelé par bande, la bande « épuisé » en gris.
    private void DessinerCourante(DrawingContext dc, AnalyseJournal courante, double w, double h)
    {
        var bandes = Escalier.ParBandes(Escalier.Segments(courante.Serie, courante.Trous, Hebdo, courante.Plage), NbBandes);
        for (var k = 0; k < bandes.Count; k++)
        {
            var bande = bandes[k];
            var brosse = bande.Niveau >= 1.0 ? Gris : Rampe?.ArcBrush(bande.Niveau);
            if (bande.Segments.Count == 0 || brosse is null) continue;
            dc.DrawGeometry(null, Plume(brosse, EpaisseurEscalier), GeometrieEscalier(bande.Segments, courante.Plage, w, h, 1.0));

            if (!TracerPourTests) continue;
            var nature = ReferenceEquals(brosse, Gris) ? "gris" : "rampe";   // la brosse qui a PEINT, pas le niveau supposé
            Tracer($"bande {k} {F(bande.Niveau)} {nature} {F(EpaisseurEscalier)}");
            foreach (var s in bande.Segments)
                Tracer($"palier {F(EchelleTemps.Fraction(s.T0, courante.Plage))} {F(EchelleTemps.Fraction(s.T1, courante.Plage))} {F(s.U)} {nature}");
        }
    }
}
