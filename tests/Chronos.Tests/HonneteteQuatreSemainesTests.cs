using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using Chronos.Controls.Historique;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;
using Chronos.Text;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-05 — les promesses de DESIGN_PLAN §2.4 prouvées de bout en bout sur les OBJETS du scénario (D-34-35 : jamais des pixels,
/// la trace de rendu et l'arbre visuel) : rien n'est dessiné ni inventé avant l'ouverture du journal, et ces semaines le DISENT
/// (étiquette, hachure, « avant le journal — aucun relevé ») ; le marqueur « journal ouvert le … » est posé sur la semaine
/// d'ouverture ; une semaine épuisée (fixtures dérivées 35-01 / 35-04) est un plateau gris annoté, jamais la rampe ; pas de piste
/// tokens, et le pied le dit. Collection sérialisée : course du chargeur BAML.
/// </summary>
[Collection("XAML WPF")]
public class HonneteteQuatreSemainesTests
{
    private static readonly TimeZoneInfo Tz = BancQuatreSemaines.Tz;
    private static readonly DateTimeOffset OuvertureJournal = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);   // lun. 14 sept. 12:00 Paris

    private static double[] Nombres(string ligne) => ligne.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN)
        .Where(v => !double.IsNaN(v)).ToArray();

    [WpfFact]
    public void Les_semaines_anterieures_au_journal_sont_vides_et_dites()
    {
        var (vm, vue) = BancQuatreSemaines.Monter();
        var hachure = vue.FindResource("HistoHachure");

        Assert.EndsWith("pas de relevés (avant le journal)", vm.EtiquettesSemaines[2].Texte);
        Assert.EndsWith("pas de relevés (avant le journal)", vm.EtiquettesSemaines[3].Texte);
        Assert.DoesNotContain("avant le journal", vm.EtiquettesSemaines[1].Texte);

        foreach (var rang in new[] { 2, 3 })
        {
            var rangee = BancQuatreSemaines.Rangee(vue, rang);
            var couverture = Assert.Single(BancQuatreSemaines.Visibles<PisteCouverture>(rangee));
            var zone = Assert.Single(BancQuatreSemaines.Visibles<Rectangle>(rangee), r => ReferenceEquals(r.Fill, hachure));
            Assert.InRange(zone.ActualWidth, couverture.ActualWidth - 1, couverture.ActualWidth + 1);
            Assert.Contains(TextesHistorique.AvantJournalAucunReleve, BancQuatreSemaines.TextesVisibles(rangee));
            Assert.DoesNotContain(BancQuatreSemaines.Trace(couverture), l => l.StartsWith("present ", StringComparison.Ordinal));
        }

        var trace = BancQuatreSemaines.Trace(BancQuatreSemaines.Piste(vue));
        Assert.DoesNotContain(trace, l => l.StartsWith("semaine 2 ", StringComparison.Ordinal));
        Assert.DoesNotContain(trace, l => l.StartsWith("semaine 3 ", StringComparison.Ordinal));
        Assert.Contains(trace, l => l.StartsWith("semaine 1 ", StringComparison.Ordinal));
    }

    [WpfFact]
    public void Le_marqueur_journal_ouvert_est_pose_sur_la_semaine_d_ouverture()
    {
        var (vm, vue) = BancQuatreSemaines.Monter();
        var hachure = vue.FindResource("HistoHachure");
        var sMoinsUn = vm.DonneesQuatreSemaines!.Semaines[2].Plage;
        Assert.Equal(OuvertureJournal, vm.DonneesQuatreSemaines.JournalOuvertLe);

        var rangee = BancQuatreSemaines.Rangee(vue, 1);
        var couverture = Assert.Single(BancQuatreSemaines.Visibles<PisteCouverture>(rangee));
        var marqueur = Assert.Single(BancQuatreSemaines.Visibles<TextBlock>(rangee), t => t.Text == "journal ouvert le 14 sept. 2026");
        var x = EchelleTemps.X(OuvertureJournal, sMoinsUn, couverture.ActualWidth);
        Assert.InRange(BancQuatreSemaines.XDans(marqueur, couverture), x - 1, x + 1);

        var zone = Assert.Single(BancQuatreSemaines.Visibles<Rectangle>(rangee), r => ReferenceEquals(r.Fill, hachure));
        var largeur = EchelleTemps.Largeur(sMoinsUn.Debut, OuvertureJournal, sMoinsUn, couverture.ActualWidth);
        Assert.InRange(zone.ActualWidth, largeur - 1, largeur + 1);
        Assert.InRange(BancQuatreSemaines.XDans(zone, couverture), -1, 1);
        Assert.DoesNotContain(TextesHistorique.AvantJournalAucunReleve, BancQuatreSemaines.TextesVisibles(rangee));

        // La semaine courante : ni marqueur, ni hachure, ni mention.
        var courante = BancQuatreSemaines.Rangee(vue, 0);
        Assert.DoesNotContain(BancQuatreSemaines.TextesVisibles(courante), t => t.StartsWith("journal ouvert", StringComparison.Ordinal));
        Assert.DoesNotContain(BancQuatreSemaines.Visibles<Rectangle>(courante), r => ReferenceEquals(r.Fill, hachure));
    }

    [WpfFact]
    public void La_semaine_epuisee_est_un_plateau_gris_annote()
    {
        var (vm, vue) = BancQuatreSemaines.Monter(d => FixturesQuatreSemaines.SemaineUnEpuisee(d, Tz));
        var d = vm.DonneesQuatreSemaines!;
        var piste = BancQuatreSemaines.Piste(vue);

        var annotation = Assert.Single(BancQuatreSemaines.Visibles<TextBlock>(vue), t => t.Text.StartsWith("épuisée jeu.", StringComparison.Ordinal));
        Assert.EndsWith("→ bloquée jusqu'au reset", annotation.Text);
        var premier = d.Semaines[2].Serie.First(r => r.T >= FixturesQuatreSemaines.DebutEpuisee);
        var x = EchelleTemps.X(EchelleTemps.Reporter(premier.T, d.Semaines[2].Plage, d.Courante.Plage), d.Courante.Plage, piste.ActualWidth);
        Assert.InRange(BancQuatreSemaines.XDans(annotation, piste), x - 1, x + 1);

        var s1 = Assert.Single(BancQuatreSemaines.Trace(piste), l => l.StartsWith("semaine 1 ", StringComparison.Ordinal));
        Assert.Equal(1.0, Nombres(s1)[3], 3);
        Assert.EndsWith(" gris", s1);

        var (_, courante) = BancQuatreSemaines.Monter(q => FixturesQuatreSemaines.SemaineCouranteEpuisee(q, Tz));
        var paliers = BancQuatreSemaines.Trace(BancQuatreSemaines.Piste(courante)).Where(l => l.StartsWith("palier ", StringComparison.Ordinal)).ToList();
        Assert.Contains(paliers, l => l.EndsWith(" 1.000 gris", StringComparison.Ordinal));
        Assert.DoesNotContain(paliers, l => l.EndsWith(" 1.000 rampe", StringComparison.Ordinal));
    }

    [WpfFact]
    public void Pas_de_piste_tokens_et_le_pied_le_dit()
    {
        var (_, vue) = BancQuatreSemaines.Monter();
        var textes = BancQuatreSemaines.TextesVisibles(vue);

        Assert.DoesNotContain(textes, t => t.Contains("tokens", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Rien n'est inventé avant l'ouverture du journal.", textes);
        Assert.Contains(TextesHistorique.CouvertureParSemaine, textes);
        Assert.Empty(BancQuatreSemaines.Visibles<PisteTokens>(vue));
    }
}
