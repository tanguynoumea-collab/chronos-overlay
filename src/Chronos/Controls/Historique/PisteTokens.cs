using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
using Chronos.Rendering.Historique;

namespace Chronos.Controls.Historique;

/// <summary>
/// HIS-03 (côté rendu) — la piste TOKENS CLAUDE CODE, sur SON axe (des entiers comptés localement, jamais un % du
/// forfait). <b>D-34-21</b> : elle montre le compteur de SORTIE (D-34-06), un seul compteur, jamais une somme des quatre ;
/// le plafond de l'axe est la DP <see cref="Plafond"/> bindée à <c>HistoriqueViewModel.PlafondTokens</c> (une seule source pour
/// l'échelle textuelle et les barres).
///
/// <para>Semaine (<see cref="Barres"/>) : une barre par heure, principal puis sous-agents empilés au-dessus. Jour
/// (<see cref="Colonnes"/>, prioritaire si les deux sont posées) : une colonne par quart d'heure, empilée par modèle dans
/// l'ordre des parts (sortie décroissante) — rang 0 → <see cref="Modele1"/>, 1 → <see cref="Modele2"/>, au-delà →
/// <see cref="Modele3"/>. Une barre ou une colonne dont l'état n'est pas « couverte » est HACHURÉE (<see cref="Hachure"/>),
/// jamais dessinée comme un zéro muet (doctrine 33-04) ; couverte et vide = un vrai zéro, rien. Les heures postérieures à
/// <see cref="InstantLecture"/> ne sont ni dessinées ni hachurées : le futur est vide, pas « transcripts absents ».</para>
/// </summary>
public sealed class PisteTokens : PisteBase
{
    private static DependencyProperty Dp(string nom, Type type, object? defaut) => Rendu(nom, type, typeof(PisteTokens), defaut);

    public static readonly DependencyProperty BarresProperty = Dp(nameof(Barres), typeof(IReadOnlyList<BarreHeure>), null);
    public static readonly DependencyProperty ColonnesProperty = Dp(nameof(Colonnes), typeof(IReadOnlyList<ColonneQuartDHeure>), null);
    public static readonly DependencyProperty PlafondProperty = Dp(nameof(Plafond), typeof(long), 1L);
    public static readonly DependencyProperty InstantLectureProperty = Dp(nameof(InstantLecture), typeof(DateTimeOffset?), null);
    public static readonly DependencyProperty PrincipalProperty = Dp(nameof(Principal), typeof(Brush), null);
    public static readonly DependencyProperty SousAgentsProperty = Dp(nameof(SousAgents), typeof(Brush), null);
    public static readonly DependencyProperty Modele1Property = Dp(nameof(Modele1), typeof(Brush), null);
    public static readonly DependencyProperty Modele2Property = Dp(nameof(Modele2), typeof(Brush), null);
    public static readonly DependencyProperty Modele3Property = Dp(nameof(Modele3), typeof(Brush), null);
    public static readonly DependencyProperty HachureProperty = Dp(nameof(Hachure), typeof(Brush), null);

    public IReadOnlyList<BarreHeure>? Barres { get => (IReadOnlyList<BarreHeure>?)GetValue(BarresProperty); set => SetValue(BarresProperty, value); }
    public IReadOnlyList<ColonneQuartDHeure>? Colonnes { get => (IReadOnlyList<ColonneQuartDHeure>?)GetValue(ColonnesProperty); set => SetValue(ColonnesProperty, value); }

    /// <summary>Le plafond de l'axe des tokens (entier « joli » du VM) ; ≤ 0 vaut 1.</summary>
    public long Plafond { get => (long)GetValue(PlafondProperty); set => SetValue(PlafondProperty, value); }

    /// <summary>L'instant de la lecture : au-delà, rien n'est dessiné (le futur est vide) ; <c>null</c> = pas de limite.</summary>
    public DateTimeOffset? InstantLecture { get => (DateTimeOffset?)GetValue(InstantLectureProperty); set => SetValue(InstantLectureProperty, value); }

    public Brush? Principal { get => (Brush?)GetValue(PrincipalProperty); set => SetValue(PrincipalProperty, value); }
    public Brush? SousAgents { get => (Brush?)GetValue(SousAgentsProperty); set => SetValue(SousAgentsProperty, value); }
    public Brush? Modele1 { get => (Brush?)GetValue(Modele1Property); set => SetValue(Modele1Property, value); }
    public Brush? Modele2 { get => (Brush?)GetValue(Modele2Property); set => SetValue(Modele2Property, value); }
    public Brush? Modele3 { get => (Brush?)GetValue(Modele3Property); set => SetValue(Modele3Property, value); }
    public Brush? Hachure { get => (Brush?)GetValue(HachureProperty); set => SetValue(HachureProperty, value); }

    protected override void Dessiner(DrawingContext dc, double w, double h)
    {
        var plage = Plage!;
        var plafond = Plafond <= 0 ? 1 : Plafond;
        if (Colonnes is { } colonnes) DessinerColonnes(dc, colonnes, plage, plafond, w, h);
        else if (Barres is { } barres) DessinerBarres(dc, barres, plage, plafond, w, h);
    }

    // Semaine : principal du bas jusqu'à Y(principal), sous-agents de là jusqu'à Y(principal + sous-agents).
    private void DessinerBarres(DrawingContext dc, IReadOnlyList<BarreHeure> barres, Plage plage, long plafond, double w, double h)
    {
        foreach (var barre in barres)
        {
            if (InstantLecture is { } lecture && barre.DebutUtc >= lecture) continue;
            var fin = barre.DebutUtc + TimeSpan.FromHours(1);
            var x0 = EchelleTemps.X(barre.DebutUtc, plage, w);
            var largeur = EchelleTemps.Largeur(barre.DebutUtc, fin, plage, w);
            var f0 = EchelleTemps.Fraction(barre.DebutUtc, plage);
            var f1 = EchelleTemps.Fraction(fin, plage);

            if (barre.Etat != EtatCouverture.Couverte)
            {
                Hachurer(dc, x0, largeur, h, f0, f1);
                continue;
            }

            var principal = barre.Principal.Out;
            var sousAgents = barre.SousAgents.Out;
            if (principal + sousAgents <= 0 || (Principal is null && SousAgents is null)) continue;
            var yPrincipal = EchelleValeur.Y(principal, plafond, h);
            var ySommet = EchelleValeur.Y(principal + sousAgents, plafond, h);
            if (Principal is { } bp && principal > 0) dc.DrawRectangle(bp, null, new Rect(x0, yPrincipal, largeur, h - yPrincipal));
            if (SousAgents is { } bs && sousAgents > 0) dc.DrawRectangle(bs, null, new Rect(x0, ySommet, largeur, yPrincipal - ySommet));
            if (!TracerPourTests) continue;
            Tracer($"tokens {F(f0)} {F(f1)} {principal.ToString(CultureInfo.InvariantCulture)} {sousAgents.ToString(CultureInfo.InvariantCulture)}");
            Tracer($"empile {F(ySommet)} {F(yPrincipal)}");
        }
    }

    // Jour : un quart d'heure par colonne, empilé par modèle dans l'ordre des parts.
    private void DessinerColonnes(DrawingContext dc, IReadOnlyList<ColonneQuartDHeure> colonnes, Plage plage, long plafond, double w, double h)
    {
        foreach (var colonne in colonnes)
        {
            if (InstantLecture is { } lecture && colonne.Slot >= lecture) continue;
            var fin = colonne.Slot + TrancheTokens.Tranche;
            var x0 = EchelleTemps.X(colonne.Slot, plage, w);
            var largeur = EchelleTemps.Largeur(colonne.Slot, fin, plage, w);
            var f0 = EchelleTemps.Fraction(colonne.Slot, plage);
            var f1 = EchelleTemps.Fraction(fin, plage);

            if (colonne.Etat != EtatCouverture.Couverte)
            {
                Hachurer(dc, x0, largeur, h, f0, f1);
                continue;
            }

            long cumul = 0;
            for (var rang = 0; rang < colonne.ParModele.Count; rang++)
            {
                var sortie = colonne.ParModele[rang].Totaux.Out;
                if (sortie <= 0) continue;
                var yBas = EchelleValeur.Y(cumul, plafond, h);
                cumul += sortie;
                var yHaut = EchelleValeur.Y(cumul, plafond, h);
                var numero = Math.Min(rang, 2) + 1;
                var brosse = numero switch { 1 => Modele1, 2 => Modele2, _ => Modele3 };
                if (brosse is null) continue;
                dc.DrawRectangle(brosse, null, new Rect(x0, yHaut, largeur, yBas - yHaut));
                if (TracerPourTests)
                    Tracer($"modele rang={rang} brosse=Modele{numero} {F(f0)} {F(f1)} {sortie.ToString(CultureInfo.InvariantCulture)}");
            }
        }
    }

    // Hors couverture ou transcripts absents : la zone est hachurée sur toute la hauteur — on ne sait pas, on le montre.
    private void Hachurer(DrawingContext dc, double x0, double largeur, double h, double f0, double f1)
    {
        if (Hachure is not { } hachure) return;
        dc.DrawRectangle(hachure, null, new Rect(x0, 0, largeur, h));
        Tracer($"hachure {F(f0)} {F(f1)}");
    }
}
