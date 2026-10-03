using System.Windows;
using System.Windows.Media;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;
using Chronos.Theming;

namespace Chronos.Controls.Historique;

/// <summary>Ce que la piste Niveau montre selon la vue (DESIGN_PLAN §2.2 / §2.3).</summary>
public enum VariantePisteNiveau
{
    /// <summary>Semaine : escalier hebdo par bandes, dents de scie 5 h, tirets de reset, fantôme S-1.</summary>
    SemaineComplete,

    /// <summary>Jour : le % 5 h au premier plan (gris quand épuisée ou refusée), l'hebdo en trait fin, un trait par reset observé.</summary>
    Jour,
}

/// <summary>
/// HIS-07 / HIS-02 / HIS-04 — la piste NIVEAU. Semaine : grille 0 / 50 / 100 %, semaine précédente en pointillé gris
/// (projetée par fraction de SA plage — D-34-20), rectangles de trous (fond à <c>OpaciteTrou</c>, bordure pointillée grise si
/// « arrêté » ou inconnue, ambre si « jeton » ou « sonde »), blocs plats des sauts non localisés, dents de scie du % 5 h
/// (réduites par min/max au-delà de <c>Binning.SeuilReduction</c> points), tirets de reset 5 h, escalier hebdo par bandes de
/// rampe (un pinceau gelé par bande, la bande « épuisé » en <see cref="Gris"/>), cadres pointillés des divergences. Jour : le
/// % 5 h au premier plan avec un statut refusé compté comme 1,0 (D-34-19 : « gris à 100 % »), l'hebdo en trait fin, un trait
/// pleine hauteur par reset observé. Chaque primitive vérifie SA brosse : une brosse absente saute la primitive, jamais la
/// piste, et rien ne lève.
/// </summary>
public sealed class PisteNiveau : PisteBase
{
    private static DependencyProperty Dp(string nom, Type type, object? defaut) => Rendu(nom, type, typeof(PisteNiveau), defaut);

    // --- données (références immuables du VM) ---
    public static readonly DependencyProperty AnalyseProperty = Dp(nameof(Analyse), typeof(AnalyseJournal), null);
    public static readonly DependencyProperty PrecedenteProperty = Dp(nameof(Precedente), typeof(AnalyseJournal), null);
    public static readonly DependencyProperty PlagePrecedenteProperty = Dp(nameof(PlagePrecedente), typeof(Plage), null);
    public static readonly DependencyProperty DivergencesProperty = Dp(nameof(Divergences), typeof(IReadOnlyList<Divergence>), null);
    public static readonly DependencyProperty InstantLectureProperty = Dp(nameof(InstantLecture), typeof(DateTimeOffset), default(DateTimeOffset));
    public static readonly DependencyProperty VarianteProperty = Dp(nameof(Variante), typeof(VariantePisteNiveau), VariantePisteNiveau.SemaineComplete);

    // --- brosses (tokens ou thème ; null = la primitive n'est pas dessinée) ---
    public static readonly DependencyProperty RampeProperty = Dp(nameof(Rampe), typeof(ChronosTheme), null);
    public static readonly DependencyProperty TraitResetProperty = Dp(nameof(TraitReset), typeof(Brush), null);
    public static readonly DependencyProperty TraitFinProperty = Dp(nameof(TraitFin), typeof(Brush), null);
    public static readonly DependencyProperty GrisProperty = Dp(nameof(Gris), typeof(Brush), null);
    public static readonly DependencyProperty FondTrouProperty = Dp(nameof(FondTrou), typeof(Brush), null);
    public static readonly DependencyProperty BordTrouArreteProperty = Dp(nameof(BordTrouArrete), typeof(Brush), null);
    public static readonly DependencyProperty BordTrouJetonProperty = Dp(nameof(BordTrouJeton), typeof(Brush), null);
    public static readonly DependencyProperty CadreDivergenceProperty = Dp(nameof(CadreDivergence), typeof(Brush), null);

    // --- tailles (tokens sys:Double) ---
    public static readonly DependencyProperty EpaisseurEscalierProperty = Dp(nameof(EpaisseurEscalier), typeof(double), 0.0);
    public static readonly DependencyProperty EpaisseurPremierPlanProperty = Dp(nameof(EpaisseurPremierPlan), typeof(double), 0.0);
    public static readonly DependencyProperty LongueurTiretResetProperty = Dp(nameof(LongueurTiretReset), typeof(double), 0.0);
    public static readonly DependencyProperty OpaciteTrouProperty = Dp(nameof(OpaciteTrou), typeof(double), 0.0);
    public static readonly DependencyProperty NbBandesProperty = Dp(nameof(NbBandes), typeof(int), 12);

    public AnalyseJournal? Analyse { get => (AnalyseJournal?)GetValue(AnalyseProperty); set => SetValue(AnalyseProperty, value); }
    public AnalyseJournal? Precedente { get => (AnalyseJournal?)GetValue(PrecedenteProperty); set => SetValue(PrecedenteProperty, value); }
    public Plage? PlagePrecedente { get => (Plage?)GetValue(PlagePrecedenteProperty); set => SetValue(PlagePrecedenteProperty, value); }
    public IReadOnlyList<Divergence>? Divergences { get => (IReadOnlyList<Divergence>?)GetValue(DivergencesProperty); set => SetValue(DivergencesProperty, value); }
    public DateTimeOffset InstantLecture { get => (DateTimeOffset)GetValue(InstantLectureProperty); set => SetValue(InstantLectureProperty, value); }
    public VariantePisteNiveau Variante { get => (VariantePisteNiveau)GetValue(VarianteProperty); set => SetValue(VarianteProperty, value); }

    public ChronosTheme? Rampe { get => (ChronosTheme?)GetValue(RampeProperty); set => SetValue(RampeProperty, value); }
    public Brush? TraitReset { get => (Brush?)GetValue(TraitResetProperty); set => SetValue(TraitResetProperty, value); }
    public Brush? TraitFin { get => (Brush?)GetValue(TraitFinProperty); set => SetValue(TraitFinProperty, value); }
    public Brush? Gris { get => (Brush?)GetValue(GrisProperty); set => SetValue(GrisProperty, value); }
    public Brush? FondTrou { get => (Brush?)GetValue(FondTrouProperty); set => SetValue(FondTrouProperty, value); }
    public Brush? BordTrouArrete { get => (Brush?)GetValue(BordTrouArreteProperty); set => SetValue(BordTrouArreteProperty, value); }
    public Brush? BordTrouJeton { get => (Brush?)GetValue(BordTrouJetonProperty); set => SetValue(BordTrouJetonProperty, value); }
    public Brush? CadreDivergence { get => (Brush?)GetValue(CadreDivergenceProperty); set => SetValue(CadreDivergenceProperty, value); }

    public double EpaisseurEscalier { get => (double)GetValue(EpaisseurEscalierProperty); set => SetValue(EpaisseurEscalierProperty, value); }
    public double EpaisseurPremierPlan { get => (double)GetValue(EpaisseurPremierPlanProperty); set => SetValue(EpaisseurPremierPlanProperty, value); }
    public double LongueurTiretReset { get => (double)GetValue(LongueurTiretResetProperty); set => SetValue(LongueurTiretResetProperty, value); }
    public double OpaciteTrou { get => (double)GetValue(OpaciteTrouProperty); set => SetValue(OpaciteTrouProperty, value); }
    public int NbBandes { get => (int)GetValue(NbBandesProperty); set => SetValue(NbBandesProperty, value); }

    protected override void Dessiner(DrawingContext dc, double w, double h)
    {
        var plage = Plage!;
        DessinerGrille(dc, w, h, 0.0, 0.5, 1.0);
        if (Variante != VariantePisteNiveau.Jour) DessinerFantome(dc, w, h);

        if (Analyse is not { } analyse) return;
        DessinerTrous(dc, analyse, plage, w, h);

        switch (Variante)
        {
            case VariantePisteNiveau.SemaineComplete:
                DessinerSauts(dc, analyse, plage, WindowKind.SevenDay, w, h);
                DessinerDents(dc, analyse, plage, w, h);
                DessinerTirets(dc, analyse, plage, w, h);
                DessinerEscalierHebdo(dc, analyse, plage, w, h);
                DessinerDivergences(dc, analyse, plage, w, h);
                break;
            default:
                DessinerSauts(dc, analyse, plage, WindowKind.FiveHour, w, h);
                DessinerHebdoFin(dc, analyse, plage, w, h);
                DessinerTraitsReset(dc, analyse, plage, w, h);
                DessinerPremierPlan(dc, analyse, plage, w, h);
                break;
        }
    }

    // D-34-20 : la semaine précédente est projetée par fraction de SA plage (168 h sur 169 : au plus une heure d'écart, deux
    // semaines par an) ; pointillé gris, jamais la rampe.
    private void DessinerFantome(DrawingContext dc, double w, double h)
    {
        if (Precedente is not { } precedente || PlagePrecedente is not { } plagePrecedente || Gris is not { } gris) return;
        var segments = Escalier.Segments(precedente.Serie, precedente.Trous, r => r.U7, plagePrecedente);
        if (segments.Count == 0) return;
        dc.DrawGeometry(null, PlumePointillee(gris, EpaisseurFine), GeometrieEscalier(segments, plagePrecedente, w, h, 1.0));
        Tracer($"fantome {segments.Count}");
    }

    // Un trou : fond à OpaciteTrou + bordure pointillée dont la couleur dit la cause. Un trou ouvert finit à l'instant de lecture.
    private void DessinerTrous(DrawingContext dc, AnalyseJournal analyse, Plage plage, double w, double h)
    {
        foreach (var rect in Escalier.RectanglesTrous(analyse.Trous, plage, InstantLecture))
        {
            var bord = rect.Trou.Cause is CauseTrou.ChronosArrete or CauseTrou.Inconnue ? BordTrouArrete : BordTrouJeton;
            var fond = FondTrou;
            if (fond is null && bord is null) continue;

            var x0 = EchelleTemps.X(rect.Debut, plage, w);
            var zone = new Rect(x0, 0, EchelleTemps.Largeur(rect.Debut, rect.Fin, plage, w), h);
            if (fond is not null)
            {
                dc.PushOpacity(OpaciteTrou);
                dc.DrawRectangle(fond, null, zone);
                dc.Pop();
            }
            if (bord is not null) dc.DrawRectangle(null, PlumePointillee(bord, EpaisseurFine), zone);
            Tracer($"trou {F(EchelleTemps.Fraction(rect.Debut, plage))} {F(EchelleTemps.Fraction(rect.Fin, plage))} {rect.Trou.Cause}");
        }
    }

    // Un saut non localisé : bloc plat gris de la hauteur du Δ sur toute la durée du trou — jamais une barre au réveil.
    private void DessinerSauts(DrawingContext dc, AnalyseJournal analyse, Plage plage, WindowKind fenetre, double w, double h)
    {
        if (Gris is not { } gris) return;
        var sauts = analyse.Sauts.Where(s => s.Fenetre == fenetre).ToList();
        foreach (var bloc in Escalier.BlocsSauts(sauts, plage))
        {
            if (bloc.Haut <= bloc.Bas) continue;   // Δ nul : rien à peindre
            var yHaut = EchelleValeur.Y(bloc.Haut, 1.0, h);
            var yBas = EchelleValeur.Y(bloc.Bas, 1.0, h);
            dc.DrawRectangle(gris, null, new Rect(EchelleTemps.X(bloc.Debut, plage, w), yHaut, EchelleTemps.Largeur(bloc.Debut, bloc.Fin, plage, w), yBas - yHaut));
            Tracer($"saut {F(EchelleTemps.Fraction(bloc.Debut, plage))} {F(EchelleTemps.Fraction(bloc.Fin, plage))} {F(bloc.Bas)} {F(bloc.Haut)}");
        }
    }

    // Les dents de scie du % 5 h (1 px, retour à 0 au reset) ; au-delà du seuil, un trait vertical min–max par colonne de pixels (D-34-11).
    private void DessinerDents(DrawingContext dc, AnalyseJournal analyse, Plage plage, double w, double h)
    {
        if (TraitReset is not { } trait) return;
        var segments = Escalier.Segments(analyse.Serie, analyse.Trous, r => r.U5, plage);
        if (segments.Count == 0) return;
        var plume = Plume(trait, EpaisseurFine);

        if (Binning.DoitReduire(analyse.Serie.Count))
        {
            var points = new List<(double X, double Y)>(segments.Count);
            foreach (var s in segments) points.Add((EchelleTemps.X(s.T0, plage, w), EchelleValeur.Y(s.U, 1.0, h)));
            var reduits = Binning.ReductionMinMax(points, (int)w);

            var geometrie = new StreamGeometry();
            using (var ctx = geometrie.Open())
            {
                foreach (var p in reduits)
                {
                    ctx.BeginFigure(new Point(p.X, p.Min), false, false);
                    ctx.LineTo(new Point(p.X, p.Max), true, false);
                }
            }
            geometrie.Freeze();
            dc.DrawGeometry(null, plume, geometrie);
            Tracer($"reduction {reduits.Count} colonnes");
            return;
        }

        dc.DrawGeometry(null, plume, GeometrieEscalier(segments, plage, w, h, 1.0));
        Tracer($"dents {segments.Count}");
    }

    // Un tiret en bas de piste par reset 5 h observé dans la plage.
    private void DessinerTirets(DrawingContext dc, AnalyseJournal analyse, Plage plage, double w, double h)
    {
        if (TraitReset is not { } trait || LongueurTiretReset <= 0) return;
        var plume = Plume(trait, EpaisseurFine);
        foreach (var reset in analyse.Resets5h)
        {
            if (!plage.Contient(reset.Instant)) continue;
            var x = EchelleTemps.X(reset.Instant, plage, w);
            dc.DrawLine(plume, new Point(x, h - LongueurTiretReset), new Point(x, h));
            Tracer($"tiret {F(EchelleTemps.Fraction(reset.Instant, plage))}");
        }
    }

    // L'escalier hebdo par bandes de rampe (D-34-09) : un pinceau gelé et une géométrie gelée par bande ; la bande « épuisé » en gris.
    private void DessinerEscalierHebdo(DrawingContext dc, AnalyseJournal analyse, Plage plage, double w, double h)
    {
        var bandes = Escalier.ParBandes(Escalier.Segments(analyse.Serie, analyse.Trous, r => r.U7, plage), NbBandes);
        DessinerBandes(dc, bandes, plage, w, h, EpaisseurEscalier, "",
            niveau => niveau >= 1.0 ? Gris : Rampe is null ? null : Rampe.ArcBrush(niveau));
    }

    // D-34-19 : le % 5 h du Jour au premier plan ; un statut refusé est dessiné comme 1,0 → bande « épuisé » → gris, pas la rampe.
    private void DessinerPremierPlan(DrawingContext dc, AnalyseJournal analyse, Plage plage, double w, double h)
    {
        var bandes = Escalier.ParBandes(Escalier.Segments(analyse.Serie, analyse.Trous, r => r.Statut5 == StatutServeur.Rejete ? 1.0 : r.U5, plage), NbBandes);
        DessinerBandes(dc, bandes, plage, w, h, EpaisseurPremierPlan, "premierplan ",
            niveau => niveau >= 1.0 ? Gris : Rampe is null ? null : Rampe.ArcBrush(niveau));
    }

    private void DessinerBandes(DrawingContext dc, IReadOnlyList<BandeRampe> bandes, Plage plage, double w, double h,
                                double epaisseur, string marque, Func<double, Brush?> brossePour)
    {
        for (var k = 0; k < bandes.Count; k++)
        {
            var bande = bandes[k];
            if (bande.Segments.Count == 0 || brossePour(bande.Niveau) is not { } brosse) continue;
            dc.DrawGeometry(null, Plume(brosse, epaisseur), GeometrieEscalier(bande.Segments, plage, w, h, 1.0));

            if (!TracerPourTests) continue;
            var nature = ReferenceEquals(brosse, Gris) ? "gris" : "rampe";   // la brosse qui a PEINT, pas le niveau supposé
            Tracer($"bande {k} {F(bande.Niveau)} {marque}{nature} {F(epaisseur)}");
            foreach (var s in bande.Segments)
                Tracer($"palier {F(EchelleTemps.Fraction(s.T0, plage))} {F(EchelleTemps.Fraction(s.T1, plage))} {F(s.U)} {marque}{nature}");
        }
    }

    // Une divergence : cadre pointillé de la marche de % sans tokens Code, du relevé le plus proche du début à celui de la fin.
    private void DessinerDivergences(DrawingContext dc, AnalyseJournal analyse, Plage plage, double w, double h)
    {
        if (CadreDivergence is not { } cadre || Divergences is not { } divergences) return;
        var plume = PlumePointillee(cadre, EpaisseurFine);
        var marge = EpaisseurEscalier + EpaisseurFine;   // le cadre dégage le trait de l'escalier
        foreach (var d in divergences)
        {
            if (Reticule.PlusProche(analyse.Serie, d.Debut)?.U7 is not { } avant || Reticule.PlusProche(analyse.Serie, d.Fin)?.U7 is not { } apres) continue;
            var yHaut = EchelleValeur.Y(Math.Max(avant, apres), 1.0, h) - marge;
            var yBas = EchelleValeur.Y(Math.Min(avant, apres), 1.0, h) + marge;
            dc.DrawRectangle(null, plume, new Rect(EchelleTemps.X(d.Debut, plage, w), yHaut, EchelleTemps.Largeur(d.Debut, d.Fin, plage, w), Math.Max(0.0, yBas - yHaut)));
            Tracer($"divergence {F(EchelleTemps.Fraction(d.Debut, plage))} {F(EchelleTemps.Fraction(d.Fin, plage))}");
        }
    }

    // Jour : l'hebdo en trait fin, sous le 5 h.
    private void DessinerHebdoFin(DrawingContext dc, AnalyseJournal analyse, Plage plage, double w, double h)
    {
        if (TraitFin is not { } trait) return;
        var segments = Escalier.Segments(analyse.Serie, analyse.Trous, r => r.U7, plage);
        if (segments.Count == 0) return;
        dc.DrawGeometry(null, Plume(trait, EpaisseurFine), GeometrieEscalier(segments, plage, w, h, 1.0));
        Tracer($"hebdo {segments.Count}");
    }

    // Jour : un trait pleine hauteur par reset 5 h observé (l'annotation « reset 5 h HH:MM » est posée par la vue).
    private void DessinerTraitsReset(DrawingContext dc, AnalyseJournal analyse, Plage plage, double w, double h)
    {
        if (TraitReset is not { } trait) return;
        var plume = Plume(trait, EpaisseurFine);
        foreach (var reset in analyse.Resets5h)
        {
            if (!plage.Contient(reset.Instant)) continue;
            var x = EchelleTemps.X(reset.Instant, plage, w);
            dc.DrawLine(plume, new Point(x, 0), new Point(x, h));
            Tracer($"trait {F(EchelleTemps.Fraction(reset.Instant, plage))}");
        }
    }
}
