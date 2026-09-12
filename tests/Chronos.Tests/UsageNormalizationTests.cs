using System.Globalization;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HDR-05 — preuve du POINT UNIQUE de normalisation des unités d'usage.
///
/// Le test central de cette classe grave un piège vérifié empiriquement sur la machine cible
/// (fr-FR, <c>InvariantGlobalization=false</c> verrouillé par CLAUDE.md) : une lecture d'en-tête
/// écrite « naturellement » ne plante pas — elle rend <c>null</c> EN SILENCE, et la source paraît
/// simplement muette sur une machine française. C'est exactement la panne silencieuse que le
/// milestone v1.5 éradique, d'où l'assertion qui prouve le piège AVANT celle qui prouve le remède.
///
/// Aucun accès fichier, aucun réseau, aucun jeton : <see cref="UsageNormalization"/> est une classe
/// pure. Pas de <c>[Collection("XAML WPF")]</c> : aucun BAML n'est chargé ici.
/// </summary>
public class UsageNormalizationTests
{
    // ------------------------------------------------------------------ le piège de culture

    [Fact]
    public void Un_en_tete_en_point_decimal_se_lit_meme_sous_une_culture_a_virgule()
    {
        var precedente = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            Assert.False(double.TryParse("0.63", out _));                      // le piège, prouvé
            Assert.Equal(0.63, UsageNormalization.FractionDepuisTexteFraction("0.63")!.Value, 6);
            Assert.Equal(new DateTimeOffset(2026, 3, 3, 4, 0, 0, TimeSpan.Zero),
                         UsageNormalization.InstantDepuisTexteEpoch("1772510400"));
            Assert.Equal("63 %", UsageNormalization.PourcentagePourAffichage(0.63));
        }
        finally { CultureInfo.CurrentCulture = precedente; }
    }

    [Fact]
    public void Une_date_ISO_se_lit_aussi_sous_une_culture_a_virgule()
    {
        var precedente = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            Assert.Equal(new DateTimeOffset(2026, 9, 9, 18, 30, 0, TimeSpan.Zero),
                         UsageNormalization.InstantDepuisIso("2026-09-09T18:30:00+00:00"));
        }
        finally { CultureInfo.CurrentCulture = precedente; }
    }

    // ------------------------------------------------------- convergence des trois unités

    /// <summary>
    /// Les trois unités concurrentes du projet : en-têtes anthropic-ratelimit-unified-*
    /// (utilization 0..1, en TEXTE), /api/oauth/usage (utilization 0..100) et le pont statusLine
    /// (used_percentage 0..100). Elles doivent rendre la MÊME fraction.
    /// </summary>
    [Fact]
    public void Les_trois_unites_convergent_vers_la_meme_fraction()
    {
        Assert.Equal(0.63, UsageNormalization.FractionDepuisTexteFraction("0.63")!.Value, 9);
        Assert.Equal(0.63, UsageNormalization.FractionDepuisPourcentage(63)!.Value, 9);
        Assert.Equal(0.63, UsageNormalization.FractionDepuisPourcentage(63.0)!.Value, 9);
    }

    // ------------------------------------------------------------------ plancher d'epoch

    /// <summary>
    /// Cas RÉEL de cette machine : le usage.json de production contient
    /// {"five_hour":{"used_percentage":10,"resets_at":9}}. L'epoch 9 = janvier 1970 ne décrit
    /// rien ; le servir produisait une géométrie fausse sur le cadran.
    /// </summary>
    [Fact]
    public void Un_epoch_qui_ne_decrit_rien_rend_l_inconnu_et_pas_une_geometrie_fausse()
    {
        Assert.Null(UsageNormalization.InstantDepuisEpochSecondes(9));
        Assert.Null(UsageNormalization.InstantDepuisTexteEpoch("9"));
        Assert.Null(UsageNormalization.InstantDepuisEpochMillisecondes(9000));
        Assert.Null(UsageNormalization.InstantDepuisIso("1970-01-01T00:00:09+00:00"));
    }

    [Fact]
    public void Les_epochs_des_fixtures_existantes_traversent_intacts()
    {
        // usage-valid.json : resets_at 1738425600 et capturedAt 1751976000000, tous deux
        // POSTÉRIEURS au plancher — aucune fixture de la suite ne doit casser.
        Assert.Equal(new DateTimeOffset(2025, 2, 1, 16, 0, 0, TimeSpan.Zero),
                     UsageNormalization.InstantDepuisEpochSecondes(1738425600));
        Assert.Equal(new DateTimeOffset(2025, 7, 8, 12, 0, 0, TimeSpan.Zero),
                     UsageNormalization.InstantDepuisEpochMillisecondes(1751976000000));
    }

    [Fact]
    public void Le_plancher_est_2020_01_01_anterieur_a_l_existence_des_fenetres_d_usage()
    {
        Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                     UsageNormalization.PlancherEpoch);
    }

    [Fact]
    public void Un_epoch_hors_des_bornes_representables_rend_l_inconnu_sans_lever()
    {
        Assert.Null(UsageNormalization.InstantDepuisEpochSecondes(long.MinValue));
        Assert.Null(UsageNormalization.InstantDepuisEpochSecondes(long.MaxValue));
        Assert.Null(UsageNormalization.InstantDepuisEpochMillisecondes(long.MinValue));
        Assert.Null(UsageNormalization.InstantDepuisEpochMillisecondes(long.MaxValue));
        Assert.Null(UsageNormalization.InstantDepuisTexteEpoch("99999999999999999999"));
    }

    // ----------------------------------------------- jamais zéro à la place de l'inconnu

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("-0.5")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("0,63")]
    public void Un_texte_de_fraction_illisible_rend_l_inconnu_et_jamais_zero(string? valeur)
    {
        var r = UsageNormalization.FractionDepuisTexteFraction(valeur);
        Assert.Null(r);                       // null = inconnu ; 0.0 dirait « 0 % consommé »
        Assert.NotEqual(0.0, r ?? -1.0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Un_pourcentage_invalide_rend_l_inconnu_et_jamais_zero(double? valeur)
    {
        Assert.Null(UsageNormalization.FractionDepuisPourcentage(valeur));
        Assert.Null(UsageNormalization.FractionDepuisFraction(valeur));
    }

    [Fact]
    public void Un_zero_legitime_traverse_quand_meme()
    {
        // Un vrai 0 % mesuré n'est pas un inconnu : la porte de validation ne le jette pas.
        Assert.Equal(0.0, UsageNormalization.FractionDepuisFraction(0.0)!.Value, 9);
        Assert.Equal(0.0, UsageNormalization.FractionDepuisPourcentage(0)!.Value, 9);
        Assert.Equal(0.0, UsageNormalization.FractionDepuisTexteFraction("0")!.Value, 9);
    }

    // ------------------------------------------------------------ dépassement non clampé

    /// <summary>
    /// WindowState.Exhausted teste &gt;= 1.0 : un dépassement réel doit rester visible, donc aucun
    /// clamp vers le haut.
    /// </summary>
    [Fact]
    public void Un_depassement_traverse_intact_et_n_est_jamais_clampe_a_1()
    {
        Assert.Equal(1.42, UsageNormalization.FractionDepuisTexteFraction("1.42")!.Value, 9);
        Assert.Equal(1.42, UsageNormalization.FractionDepuisPourcentage(142)!.Value, 9);
        Assert.Equal(1.42, UsageNormalization.FractionDepuisFraction(1.42)!.Value, 9);
    }

    // ------------------------------------------------------------------ tolérance de forme

    [Fact]
    public void Les_espaces_autour_de_la_valeur_sont_toleres()
    {
        Assert.Equal(0.63, UsageNormalization.FractionDepuisTexteFraction(" 0.63 ")!.Value, 9);
        Assert.Equal(new DateTimeOffset(2025, 2, 1, 16, 0, 0, TimeSpan.Zero),
                     UsageNormalization.InstantDepuisTexteEpoch(" 1738425600 "));
    }

    // ------------------------------------------------------------------------- ISO 8601

    [Fact]
    public void Une_date_ISO_valide_se_lit_et_toute_autre_forme_rend_l_inconnu()
    {
        Assert.Equal(new DateTimeOffset(2026, 9, 9, 18, 30, 0, TimeSpan.Zero),
                     UsageNormalization.InstantDepuisIso("2026-09-09T18:30:00+00:00"));
        Assert.Null(UsageNormalization.InstantDepuisIso(null));
        Assert.Null(UsageNormalization.InstantDepuisIso(""));
        Assert.Null(UsageNormalization.InstantDepuisIso("pas une date"));
    }

    // ------------------------------------------------------------------------ affichage

    [Fact]
    public void L_affichage_du_pourcentage_est_stable_et_muet_sur_l_inconnu()
    {
        Assert.Equal("63 %", UsageNormalization.PourcentagePourAffichage(0.63));
        Assert.Equal("", UsageNormalization.PourcentagePourAffichage(null));
        Assert.Equal("142 %", UsageNormalization.PourcentagePourAffichage(1.42));
        Assert.Equal("0 %", UsageNormalization.PourcentagePourAffichage(0.0));
    }

    [Fact]
    public void Les_entrees_illisibles_de_texte_d_epoch_rendent_l_inconnu()
    {
        Assert.Null(UsageNormalization.InstantDepuisTexteEpoch(null));
        Assert.Null(UsageNormalization.InstantDepuisTexteEpoch(""));
        Assert.Null(UsageNormalization.InstantDepuisTexteEpoch("   "));
        Assert.Null(UsageNormalization.InstantDepuisTexteEpoch("pas un nombre"));
        Assert.Null(UsageNormalization.InstantDepuisTexteEpoch("1738425600.5"));
        Assert.Null(UsageNormalization.InstantDepuisEpochSecondes(null));
        Assert.Null(UsageNormalization.InstantDepuisEpochMillisecondes(null));
    }
}
