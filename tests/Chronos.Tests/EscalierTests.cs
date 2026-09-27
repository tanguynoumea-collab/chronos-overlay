using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-02 / HIS-06 — l'ESCALIER pur d'une série de relevés et les formes de l'absence, sur la fixture réelle
/// <c>trou-arrete</c> (trou 10:10Z → 11:03Z « Chronos arrêté », saut 5 h 0,14 → 0,20, hebdo 0,41 → 0,43) et sur des séries
/// construites en mémoire.
///
/// Ce que ces tests gravent : un palier va jusqu'au relevé suivant et s'ARRÊTE au dernier relevé avant un trou (D-34-08) ;
/// la longueur d'un palier suit la série, pas le sélecteur ; tout est borné à la fin de plage ; les paliers se regroupent
/// par bande de rampe avec une bande « épuisé » séparée (D-34-09) ; un trou est un rectangle borné (ouvert → finit à now) ;
/// un saut est un bloc plat de la hauteur du Δ, et un saut indéterminable n'a AUCUNE forme (jamais une barre au réveil) ;
/// un trou est VIDE de géométrie.
/// </summary>
public class EscalierTests
{
    private static readonly TimeSpan Cadence = RateLimitHeaderUsageProvider.CadenceNominale;
    private const string R5 = "2026-09-27T14:50:00Z";

    private static string TestDataDir(string cas, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "journal", cas);

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static ReleveJournal R(string t, double? u5, double? u7 = null)
        => new(Utc(t), SourceUsage.SondeEnTetes, u5, Utc(R5), StatutServeur.Autorise, u7, u7 is null ? null : Utc("2026-10-02T22:00:00Z"), null, null, null);

    private static SegmentPalier P(double u) => new(Utc("2026-09-27T10:00:00Z"), Utc("2026-09-27T10:05:00Z"), u);

    // La fixture trou-arrete lue et analysée EXACTEMENT comme dans AnalyseRelevesTests.
    private static AnalyseJournal TrouArrete()
        => AnalyseReleves.Analyser(
            LecteurJournal.Lire(TestDataDir("trou-arrete"), Utc("2026-09-27T10:00:00Z"), Utc("2026-09-27T11:20:00Z")),
            Utc("2026-09-27T11:20:00Z"), Cadence);

    [Fact]
    public void Un_palier_va_jusqu_au_releve_suivant_et_s_arrete_au_trou()
    {
        var a = TrouArrete();
        var trou = Assert.Single(a.Trous);

        var segs = Escalier.Segments(a.Serie, a.Trous, r => r.U5, a.Plage);

        Assert.Equal(a.Serie.Count, segs.Count);   // un palier par relevé porteur d'U5
        var arret = Assert.Single(segs, s => s.T0 == Utc("2026-09-27T10:10:00Z"));
        Assert.Equal(Utc("2026-09-27T10:10:00Z"), arret.T1);   // longueur nulle : la ligne s'interrompt
        Assert.Equal(0.14, arret.U);
        var reprise = segs[segs.ToList().IndexOf(arret) + 1];
        Assert.Equal(Utc("2026-09-27T11:03:00Z"), reprise.T0);
        Assert.Equal(0.20, reprise.U);

        // Aucun palier ne traverse le début du trou.
        var juste = trou.Debut + TimeSpan.FromSeconds(1);
        Assert.All(segs, s => Assert.False(s.T0 < juste && s.T1 > juste, $"le palier {s.T0:HH:mm}→{s.T1:HH:mm} traverse le trou"));

        // Hors trou, les paliers se touchent : T1[i] == T0[i+1].
        for (var i = 0; i < segs.Count - 1; i++)
            if (segs[i].T0 != trou.Debut)
                Assert.Equal(segs[i + 1].T0, segs[i].T1);
    }

    [Fact]
    public void Un_releve_sans_valeur_pour_le_selecteur_ne_fait_pas_de_palier()
    {
        var serie = new[]
        {
            R("2026-09-27T10:00:00Z", 0.10, u7: 0.40),
            R("2026-09-27T10:05:00Z", 0.11, u7: null),
            R("2026-09-27T10:10:00Z", 0.12, u7: 0.42),
        };
        var plage = new Plage(Utc("2026-09-27T10:00:00Z"), Utc("2026-09-27T11:00:00Z"));

        var segs = Escalier.Segments(serie, Array.Empty<Trou>(), r => r.U7, plage);

        Assert.Equal(2, segs.Count);
        // La LONGUEUR d'un palier suit la série, pas le sélecteur : le 1er palier s'arrête au 2e relevé (T[1]), pas au 3e.
        Assert.Equal(Utc("2026-09-27T10:05:00Z"), segs[0].T1);
        Assert.Equal(Utc("2026-09-27T10:10:00Z"), segs[1].T0);
        Assert.Equal(0.40, segs[0].U);
        Assert.Equal(0.42, segs[1].U);
    }

    [Fact]
    public void Le_dernier_palier_est_borne_a_la_fin_de_plage()
    {
        var plage = new Plage(Utc("2026-09-27T10:00:00Z"), Utc("2026-09-27T11:00:00Z"));

        // Série déjà filtrée par la lecture : le dernier relevé fait un point.
        var filtree = new[] { R("2026-09-27T10:50:00Z", 0.10), R("2026-09-27T10:58:00Z", 0.11) };
        var segs = Escalier.Segments(filtree, Array.Empty<Trou>(), r => r.U5, plage);
        Assert.Equal(2, segs.Count);
        Assert.Equal(segs[1].T0, segs[1].T1);
        Assert.Equal(Utc("2026-09-27T10:58:00Z"), segs[0].T1);

        // Série construite à la main avec un T[i+1] au-delà de la plage : le palier est coupé à Plage.Fin.
        var deborde = new[] { R("2026-09-27T10:55:00Z", 0.10), R("2026-09-27T11:07:00Z", 0.11) };
        var coupes = Escalier.Segments(deborde, Array.Empty<Trou>(), r => r.U5, plage);
        Assert.Equal(plage.Fin, coupes[0].T1);
        Assert.All(coupes, s => Assert.True(s.T0 <= s.T1 && s.T1 <= plage.Fin, "aucun palier ne sort de la plage"));
    }

    [Fact]
    public void ParBandes_regroupe_par_niveau_et_isole_l_epuise()
    {
        var segs = new[] { P(0.04), P(0.08), P(0.083), P(0.5), P(0.99), P(1.0), P(1.2) };

        var bandes = Escalier.ParBandes(segs, 12);

        Assert.Equal(4, bandes.Count);
        Assert.Equal(new[] { 0.5 / 12, 6.5 / 12, 11.5 / 12, 1.0 }, bandes.Select(b => b.Niveau));   // ordre croissant par Niveau
        Assert.Equal(new[] { 0.04, 0.08, 0.083 }, bandes[0].Segments.Select(s => s.U));   // floor(0,083 × 12) = 0 : même bande
        Assert.Equal(new[] { 0.5 }, bandes[1].Segments.Select(s => s.U));
        Assert.Equal(new[] { 0.99 }, bandes[2].Segments.Select(s => s.U));
        Assert.Equal(new[] { 1.0, 1.2 }, bandes[3].Segments.Select(s => s.U));   // épuisé : SA bande, jamais fondue dans la 11e
        Assert.True(bandes.SequenceEqual(bandes.OrderBy(b => b.Niveau)));

        var unique = Assert.Single(Escalier.ParBandes(segs, 0));
        Assert.Equal(0.5, unique.Niveau);
        Assert.Equal(7, unique.Segments.Count);
    }

    [Fact]
    public void Les_rectangles_de_trous_sont_bornes_et_un_trou_ouvert_finit_a_now()
    {
        var a = TrouArrete();
        var now = Utc("2026-09-27T11:20:00Z");

        var rect = Assert.Single(Escalier.RectanglesTrous(a.Trous, a.Plage, now));
        Assert.Equal(Utc("2026-09-27T10:10:00Z"), rect.Debut);
        Assert.Equal(Utc("2026-09-27T11:03:00Z"), rect.Fin);
        Assert.Equal(a.Trous[0], rect.Trou);
        Assert.Equal(CauseTrou.ChronosArrete, rect.Trou.Cause);

        var plage = new Plage(Utc("2026-09-27T10:00:00Z"), Utc("2026-09-27T12:00:00Z"));
        var finSiOuvert = plage.Fin - TimeSpan.FromMinutes(5);
        var trous = new[]
        {
            new Trou(Utc("2026-09-27T10:30:00Z"), null, CauseTrou.Inconnue),                               // ouvert → finit à now
            new Trou(Utc("2026-09-27T09:00:00Z"), Utc("2026-09-27T09:30:00Z"), CauseTrou.ChronosArrete),   // entièrement avant → omis
            new Trou(Utc("2026-09-27T09:50:00Z"), Utc("2026-09-27T10:05:00Z"), CauseTrou.JetonInvalide),   // à cheval → coupé au Debut
        };

        var rects = Escalier.RectanglesTrous(trous, plage, finSiOuvert);

        Assert.Equal(2, rects.Count);
        var ouvert = Assert.Single(rects, r => r.Trou.Fin is null);
        Assert.Equal(finSiOuvert, ouvert.Fin);
        var cheval = Assert.Single(rects, r => r.Trou.Cause == CauseTrou.JetonInvalide);
        Assert.Equal(plage.Debut, cheval.Debut);
        Assert.Equal(Utc("2026-09-27T10:05:00Z"), cheval.Fin);
        Assert.DoesNotContain(rects, r => r.Trou.Cause == CauseTrou.ChronosArrete);
    }

    [Fact]
    public void Les_blocs_de_sauts_ont_la_hauteur_du_delta_et_ignorent_l_indeterminable()
    {
        var a = TrouArrete();

        var blocs = Escalier.BlocsSauts(a.Sauts, a.Plage);

        Assert.Equal(2, blocs.Count);
        var cinq = Assert.Single(blocs, b => b.Saut.Fenetre == WindowKind.FiveHour);
        Assert.Equal(0.14, cinq.Bas);
        Assert.Equal(0.20, cinq.Haut);
        Assert.Equal(Utc("2026-09-27T10:10:00Z"), cinq.Debut);
        Assert.Equal(Utc("2026-09-27T11:03:00Z"), cinq.Fin);
        var hebdo = Assert.Single(blocs, b => b.Saut.Fenetre == WindowKind.SevenDay);
        Assert.Equal(0.41, hebdo.Bas);
        Assert.Equal(0.43, hebdo.Haut);

        // Un reset pendant l'absence : Δ indéterminable → AUCUN bloc (et surtout pas une barre au réveil).
        var trou = new Trou(Utc("2026-09-27T10:00:00Z"), Utc("2026-09-27T11:00:00Z"), CauseTrou.Inconnue);
        var indeterminable = new[] { new SautNonLocalise(WindowKind.FiveHour, trou, Avant: 0.9, Apres: 0.1, Delta: null) };
        Assert.Empty(Escalier.BlocsSauts(indeterminable, a.Plage));
    }

    [Fact]
    public void Rien_n_est_interpole_a_travers_un_trou()
    {
        var a = TrouArrete();
        var segs = Escalier.Segments(a.Serie, a.Trous, r => r.U5, a.Plage);
        var segsHebdo = Escalier.Segments(a.Serie, a.Trous, r => r.U7, a.Plage);

        foreach (var trou in a.Trous)
        {
            var fin = trou.Fin ?? Utc("2026-09-27T11:20:00Z");
            foreach (var s in segs.Concat(segsHebdo))
            {
                // Aucun palier n'intersecte l'intérieur ]Debut, Fin[ du trou : ni en le traversant, ni en y commençant.
                Assert.False(s.T0 < fin && s.T1 > trou.Debut, $"le palier {s.T0:HH:mm}→{s.T1:HH:mm} entre dans le trou {trou.Debut:HH:mm}→{fin:HH:mm}");
                Assert.False(trou.Debut < s.T0 && s.T0 < fin, $"un palier commence dans le trou à {s.T0:HH:mm}");
            }
        }
    }
}
