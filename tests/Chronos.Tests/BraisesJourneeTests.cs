using System.IO;
using Chronos.Rendering;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Plan 43-09 (constat du 2026-10-05, conception validée par l'utilisateur) — l'anneau EXTÉRIEUR de Braises est la JOURNÉE
/// locale : 24 braises, une par heure d'horloge, minuit en haut, sens horaire (même repère que DayTimeline : 0° = minuit,
/// 15° par heure). Groupes = tranches de la grille des resets 5 h, ancrée sur le resets_at 5 h COURANT ; une braise est
/// rapprochée du centre angulaire de son groupe par un facteur 0,8. Allumage : heures passées pleines, heure en cours en
/// demi-lueur, futur en cendre. Toutes les heures sont données avec un offset EXPLICITE (+02:00) : la math lit l'heure
/// d'horloge du VALUE, indépendante du fuseau de la machine et de l'horloge réelle.
/// </summary>
public class BraisesJourneeTests
{
    private static readonly TimeSpan Paris = TimeSpan.FromHours(2);
    private static DateTimeOffset Le(int jour, int h, int m, int s = 0) => new(2026, 10, jour, h, m, s, Paris);

    private static double Nominal(int i) => (i + 0.5) * 15.0;

    /// <summary>Écart angulaire orienté de a vers b (sens horaire), dans [0, 360).</summary>
    private static double Ecart(double a, double b) => ((b - a) % 360.0 + 360.0) % 360.0;

    // ------------------------------------------------------------------ groupes (tranches 5 h)

    [Fact]
    public void Reset_14h50_cinq_tranches_5_5_5_5_4()
    {
        // Exemple de la spécification : tranches [00-04], [05-09], [10-14], [15-19], [20-23].
        Assert.Equal(new[] { 5, 5, 5, 5, 4 }, BraisesJournee.TaillesGroupes(Le(5, 11, 35), Le(5, 14, 50)));
    }

    [Theory]
    [InlineData(5, 0, 50, new[] { 1, 5, 5, 5, 5, 3 })]    // la tranche 19:50 → 00:50 couvre minuit : braise 0 seule avant la grille
    [InlineData(5, 5, 0, new[] { 5, 5, 5, 5, 4 })]        // grille 00:00 / 05:00 / … : la journée commence avec une tranche
    [InlineData(5, 23, 10, new[] { 3, 5, 5, 5, 5, 1 })]   // grille 03:10 / … / 23:10 : la braise 23 ouvre la tranche suivante
    public void Plusieurs_ancres_groupes_coherents(int jour, int h, int m, int[] attendu)
    {
        var tailles = BraisesJournee.TaillesGroupes(Le(5, 12, 0), Le(jour, h, m));
        Assert.Equal(attendu, tailles);
        Assert.Equal(24, tailles.Sum());
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(4)]
    public void Passage_de_minuit_l_ancre_decalee_de_k_fois_5h_donne_la_meme_grille(int k)
    {
        // 14:50 + k × 5 h tombe la veille (-3 → 23:50 du 4) ou le lendemain (4 → 10:50 du 6) : même grille, mêmes tranches.
        var maintenant = Le(5, 11, 35);
        var ancre = Le(5, 14, 50);
        Assert.Equal(BraisesJournee.TaillesGroupes(maintenant, ancre),
                     BraisesJournee.TaillesGroupes(maintenant, ancre + TimeSpan.FromHours(5 * k)));
        Assert.Equal(BraisesJournee.Angles(maintenant, ancre),
                     BraisesJournee.Angles(maintenant, ancre + TimeSpan.FromHours(5 * k)));
    }

    [Fact]
    public void Reset_inconnu_aucune_grille_24_braises_uniformes()
    {
        var angles = BraisesJournee.Angles(Le(5, 11, 35), null);
        Assert.Equal(24, angles.Count);
        for (int i = 0; i < 24; i++) Assert.Equal(Nominal(i), angles[i], 6);
        Assert.Equal(new[] { 24 }, BraisesJournee.TaillesGroupes(Le(5, 11, 35), null));
    }

    // ------------------------------------------------------------------ positions

    [Fact]
    public void Reset_14h50_angles_par_le_facteur_0_8()
    {
        // Centres de groupe 37,5 / 112,5 / 187,5 / 262,5 / 330 ; angle = c + 0,8 × (nominal − c).
        var a = BraisesJournee.Angles(Le(5, 11, 35), Le(5, 14, 50));
        Assert.Equal(13.5, a[0], 6);
        Assert.Equal(25.5, a[1], 6);
        Assert.Equal(100.5, a[6], 6);    // à droite (≈ 97,5°)
        Assert.Equal(187.5, a[12], 6);   // en bas
        Assert.Equal(274.5, a[18], 6);   // à gauche
        Assert.Equal(348.0, a[23], 6);
    }

    [Theory]
    [InlineData(14, 50)]
    [InlineData(0, 50)]
    [InlineData(5, 0)]
    [InlineData(23, 10)]
    public void Chaque_braise_reste_a_6_degres_au_plus_de_son_heure_et_l_ecart_intra_vaut_12(int h, int m)
    {
        var maintenant = Le(5, 12, 0);
        var ancre = Le(5, h, m);
        var a = BraisesJournee.Angles(maintenant, ancre);
        var tranches = BraisesJournee.Tranches(maintenant, ancre)!;

        Assert.Equal(24, a.Count);
        for (int i = 0; i < 24; i++)
            Assert.True(Math.Abs(a[i] - Nominal(i)) <= 6.0 + 1e-9, $"braise {i} à {a[i]}°, son heure est à {Nominal(i)}°");
        for (int i = 0; i < 23; i++)
            if (tranches[i] == tranches[i + 1]) Assert.Equal(12.0, a[i + 1] - a[i], 6);
    }

    [Theory]
    [InlineData(14, 50)]
    [InlineData(5, 0)]
    public void Grille_5_5_5_5_4_vide_entre_groupes_au_moins_deux_fois_l_ecart_intra(int h, int m)
    {
        var maintenant = Le(5, 12, 0);
        var ancre = Le(5, h, m);
        var a = BraisesJournee.Angles(maintenant, ancre);
        var tranches = BraisesJournee.Tranches(maintenant, ancre)!;

        int vides = 0;
        for (int i = 0; i < 24; i++)
        {
            int j = (i + 1) % 24;   // la paire 23 → 0 traverse minuit
            if (tranches[i] == tranches[j]) continue;
            vides++;
            Assert.True(Ecart(a[i], a[j]) >= 24.0 - 1e-9, $"vide {i}→{j} de {Ecart(a[i], a[j])}° < 2 × 12°");
        }
        Assert.Equal(5, vides);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(23, 10)]
    public void Grille_avec_groupe_d_une_braise_le_vide_reste_plus_large_que_l_ecart_intra(int h, int m)
    {
        // Limite signalée au plan 43-09 : avec un groupe d'une braise (60 % des ancres possibles), « ≤ 6° de l'heure » et
        // « vide ≥ 2 × 12° » sont incompatibles. Le facteur 0,8 de la spécification donne alors un vide de 18° ou 21° :
        // toujours visible (≥ 1,5 × l'écart intra), jamais un contact.
        var maintenant = Le(5, 12, 0);
        var ancre = Le(5, h, m);
        var a = BraisesJournee.Angles(maintenant, ancre);
        var tranches = BraisesJournee.Tranches(maintenant, ancre)!;

        for (int i = 0; i < 24; i++)
        {
            int j = (i + 1) % 24;
            if (tranches[i] == tranches[j]) continue;
            Assert.True(Ecart(a[i], a[j]) >= 18.0 - 1e-9, $"vide {i}→{j} de {Ecart(a[i], a[j])}° < 18°");
        }
    }

    [Theory]
    [InlineData(14, 50)]
    [InlineData(0, 50)]
    [InlineData(23, 10)]
    public void Braises_sans_contact_au_rayon_66(int h, int m)
    {
        var a = BraisesJournee.Angles(Le(5, 12, 0), Le(5, h, m));
        for (int i = 0; i < 24; i++)
        {
            double d = Ecart(a[i], a[(i + 1) % 24]) * Math.PI / 180.0;
            double corde = 2 * 66 * Math.Sin(d / 2);
            Assert.True(corde > 2 * 4.0 + 2, $"braises {i} et {(i + 1) % 24} trop proches : {corde:F2} px");
        }
    }

    // ------------------------------------------------------------------ allumage

    [Fact]
    public void A_11h35_onze_pleines_demi_lueur_sur_11_puis_cendre()
    {
        var e = BraisesJournee.Etats(Le(5, 11, 35));
        Assert.Equal(24, e.Count);
        for (int i = 0; i <= 10; i++) Assert.Equal(EtatBraise.Pleine, e[i]);
        Assert.Equal(EtatBraise.DemiLueur, e[11]);
        for (int i = 12; i < 24; i++) Assert.Equal(EtatBraise.Cendre, e[i]);
    }

    [Fact]
    public void A_11h35_la_tranche_09h50_14h50_montre_une_pleine_et_une_demi_lueur()
    {
        var maintenant = Le(5, 11, 35);
        var tranches = BraisesJournee.Tranches(maintenant, Le(5, 14, 50))!;
        var e = BraisesJournee.Etats(maintenant);
        var groupe = Enumerable.Range(0, 24).Where(i => tranches[i] == tranches[10]).ToList();

        Assert.Equal(new[] { 10, 11, 12, 13, 14 }, groupe);
        Assert.Equal(1, groupe.Count(i => e[i] == EtatBraise.Pleine));
        Assert.Equal(1, groupe.Count(i => e[i] == EtatBraise.DemiLueur));
    }

    [Fact]
    public void A_00h10_aucune_pleine_demi_lueur_sur_0()
    {
        var e = BraisesJournee.Etats(Le(5, 0, 10));
        Assert.Equal(EtatBraise.DemiLueur, e[0]);
        Assert.All(e.Skip(1), x => Assert.Equal(EtatBraise.Cendre, x));
    }

    [Fact]
    public void A_23h59_vingt_trois_pleines_demi_lueur_sur_23()
    {
        var e = BraisesJournee.Etats(Le(5, 23, 59));
        Assert.All(e.Take(23), x => Assert.Equal(EtatBraise.Pleine, x));
        Assert.Equal(EtatBraise.DemiLueur, e[23]);
    }

    [Fact]
    public void A_minuit_la_journee_repart_de_zero()
    {
        var veille = BraisesJournee.Etats(Le(5, 23, 59, 59));
        var minuit = BraisesJournee.Etats(Le(6, 0, 0, 0));
        Assert.Equal(23, veille.Count(x => x == EtatBraise.Pleine));
        Assert.Equal(0, minuit.Count(x => x == EtatBraise.Pleine));
        Assert.Equal(EtatBraise.DemiLueur, minuit[0]);
    }

    // ------------------------------------------------------------------ pureté et documentation

    [Fact]
    public void BraisesJournee_ne_reference_aucun_assembly_WPF()
    {
        string[] interdits = { "PresentationCore", "PresentationFramework", "WindowsBase" };
        var t = typeof(BraisesJournee);
        var touches = t.GetMethods()
            .SelectMany(m => new[] { m.ReturnType }.Concat(m.GetParameters().Select(p => p.ParameterType)))
            .Select(x => x.Assembly.GetName().Name);
        Assert.DoesNotContain(touches, n => interdits.Contains(n));

        var source = File.ReadAllText(Path.Combine(GardesPerimetreTests.CheminSources(), "Rendering", "BraisesJournee.cs"));
        Assert.DoesNotContain("System.Windows", source);
        Assert.DoesNotContain("DateTime.Now", source);
        Assert.DoesNotContain("UtcNow", source);
        Assert.DoesNotContain("TimeZoneInfo.Local", source);
    }

    [Fact]
    public void Les_tranches_projetees_sont_documentees_dans_le_README_et_data_sources()
    {
        var racine = Path.GetFullPath(Path.Combine(GardesPerimetreTests.CheminSources(), "..", ".."));
        var readme = File.ReadAllText(Path.Combine(racine, "README.md"));
        var sources = File.ReadAllText(Path.Combine(racine, "docs", "data-sources.md"));

        Assert.Contains("une braise par heure", readme);
        Assert.Contains("tranches de 5 h", readme);
        Assert.Contains("projetées", readme);
        Assert.Contains("Braises", sources);
        Assert.Contains("projetées", sources);
    }
}
