using Chronos.Models;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve HDR-03 et HDR-04 au niveau du VOCABULAIRE, avant toute sonde.
///
/// Le point décisif de cette classe n'est pas le mappage des trois valeurs connues — c'est le REFUS de
/// deviner. La famille d'en-têtes <c>anthropic-ratelimit-unified-*</c> est ABSENTE de la documentation
/// publique Anthropic : <c>allowed</c> / <c>allowed_warning</c> / <c>rejected</c> sont rapportés par le
/// code d'origine et par la communauté, rien de plus. Une valeur hors de cet ensemble doit donc produire
/// <see cref="StatutServeur.NonReconnu"/> et jamais <see cref="StatutServeur.Autorise"/> : ranger
/// l'inconnu dans « autorisé » serait exactement le mensonge que v1.5 corrige, sous un autre nom.
///
/// Et les DEUX inconnus sont distincts : <c>null</c> = « le serveur n'a rien dit », <c>NonReconnu</c> =
/// « le serveur a dit quelque chose que nous ne savons pas lire ». Deux faits, deux valeurs.
///
/// Tests PURS : aucune horloge, aucune entrée/sortie, aucun réseau.
/// </summary>
public class StatutServeurTests
{
    // --- HDR-03 : le vocabulaire CONNU se lit, quelle que soit la casse et l'enrobage d'espaces ---

    [Theory]
    [InlineData("allowed", StatutServeur.Autorise)]
    [InlineData("ALLOWED", StatutServeur.Autorise)]
    [InlineData("Allowed", StatutServeur.Autorise)]
    [InlineData("  allowed  ", StatutServeur.Autorise)]
    [InlineData("\tallowed\r\n", StatutServeur.Autorise)]
    [InlineData("allowed_warning", StatutServeur.AutoriseAvertissement)]
    [InlineData("Allowed_Warning", StatutServeur.AutoriseAvertissement)]
    [InlineData("ALLOWED_WARNING", StatutServeur.AutoriseAvertissement)]
    [InlineData("  allowed_warning ", StatutServeur.AutoriseAvertissement)]
    [InlineData("rejected", StatutServeur.Rejete)]
    [InlineData("REJECTED", StatutServeur.Rejete)]
    [InlineData(" Rejected ", StatutServeur.Rejete)]
    public void Le_vocabulaire_connu_se_lit_sans_egard_a_la_casse_ni_aux_espaces(string valeur, StatutServeur attendu)
    {
        var lu = StatutServeurTexte.DepuisEnTete(valeur);

        Assert.NotNull(lu);
        Assert.Equal(attendu, lu!.Value);
    }

    // --- HDR-03, le cœur : un statut PRÉSENT mais hors de l'ensemble connu ne se devine pas ---

    [Theory]
    [InlineData("active")]              // mentionné par une proposition d'issue, jamais confirmé
    [InlineData("blocked")]
    [InlineData("throttled")]
    [InlineData("allow")]               // préfixe du connu : ne doit PAS suffire
    [InlineData("allowed_")]
    [InlineData("allowedwarning")]      // le tiret bas fait partie du jeton
    [InlineData("warning")]
    [InlineData("0.5")]
    [InlineData("n'importe quoi")]
    public void Un_statut_inconnu_ne_se_devine_pas(string valeur)
    {
        var lu = StatutServeurTexte.DepuisEnTete(valeur);

        Assert.NotNull(lu);
        Assert.Equal(StatutServeur.NonReconnu, lu!.Value);
        Assert.NotEqual(StatutServeur.Autorise, lu!.Value);   // la règle explicitement, pas par déduction
    }

    // --- HDR-03 : « rien rapporté » n'est PAS « rapporté mais inconnu » ---

    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void Un_en_tete_absent_ou_vide_rend_l_inconnu_et_non_un_statut(string? valeur)
        => Assert.Null(StatutServeurTexte.DepuisEnTete(valeur));

    [Fact]
    public void Les_deux_inconnus_sont_des_valeurs_distinctes()
    {
        // Absence de l'en-tête : le serveur n'a rien dit.
        var rienDit = StatutServeurTexte.DepuisEnTete(null);
        // En-tête présent, valeur hors ensemble connu : le serveur a dit quelque chose d'illisible.
        var ditIllisible = StatutServeurTexte.DepuisEnTete("active");

        Assert.Null(rienDit);
        Assert.Equal(StatutServeur.NonReconnu, ditIllisible);
        Assert.NotEqual(rienDit, ditIllisible);
    }

    [Fact]
    public void L_enum_porte_exactement_les_quatre_membres_attendus()
    {
        // Garde de vocabulaire : le quatrième membre est la condition de l'honnêteté. S'il disparaît,
        // une valeur inconnue devra forcément atterrir dans une des trois autres — donc mentir.
        Assert.Equal(
            new[] { "Autorise", "AutoriseAvertissement", "Rejete", "NonReconnu" },
            Enum.GetNames<StatutServeur>());
    }

    // --- HDR-04 : EtatDepassement — tout optionnel, rien d'inventé ---

    [Fact]
    public void Un_depassement_vide_n_est_pas_renseigne()
        => Assert.False(new EtatDepassement().EstRenseigne);

    [Fact]
    public void Une_utilization_seule_suffit_a_renseigner_le_depassement()
        => Assert.True(new EtatDepassement { Utilization = 0.12 }.EstRenseigne);

    [Fact]
    public void Un_reset_seul_suffit_a_renseigner_le_depassement()
        => Assert.True(new EtatDepassement { ResetsAt = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero) }.EstRenseigne);

    [Fact]
    public void Un_statut_seul_suffit_a_renseigner_le_depassement()
        => Assert.True(new EtatDepassement { Statut = StatutServeur.AutoriseAvertissement }.EstRenseigne);

    [Fact]
    public void Un_statut_NON_RECONNU_renseigne_aussi_le_depassement()
        // « Le serveur a parlé sans que nous sachions le lire » est une information, pas une absence.
        => Assert.True(new EtatDepassement { Statut = StatutServeur.NonReconnu }.EstRenseigne);

    [Fact]
    public void Un_depassement_entierement_renseigne_l_est_aussi()
    {
        var d = new EtatDepassement
        {
            Utilization = 0.34,
            ResetsAt = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero),
            Statut = StatutServeur.Rejete,
        };

        Assert.True(d.EstRenseigne);
    }

    [Fact]
    public void Deux_depassements_aux_memes_champs_sont_egaux()
    {
        var t = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

        var a = new EtatDepassement { Utilization = 0.34, ResetsAt = t, Statut = StatutServeur.Rejete };
        var b = new EtatDepassement { Utilization = 0.34, ResetsAt = t, Statut = StatutServeur.Rejete };

        Assert.Equal(a, b);                     // égalité structurelle de record
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Deux_depassements_differents_ne_sont_pas_egaux()
    {
        var a = new EtatDepassement { Utilization = 0.34 };
        var b = new EtatDepassement { Utilization = 0.35 };
        var vide = new EtatDepassement();

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, vide);
    }
}
