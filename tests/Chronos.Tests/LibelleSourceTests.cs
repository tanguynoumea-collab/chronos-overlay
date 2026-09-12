using Chronos.Models;
using Chronos.Text;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// EXA-06 — le vocabulaire FR du relevé, gravé à l'endroit UNIQUE où il est calculé.
///
/// Classe PURE : <see cref="LibelleSource"/> n'a ni WPF, ni I/O, ni horloge propre (<c>now</c> est
/// toujours un paramètre). Aucun BAML n'est chargé, donc PAS de rattachement à la collection sérialisée
/// des tests XAML : l'y mettre ralentirait la suite pour rien.
///
/// Ces tests gravent les littéraux eux-mêmes, et c'est délibéré. Le jour où quelqu'un voudra les
/// changer, il devra les changer ICI — c'est-à-dire à un seul endroit, ce qui est exactement la
/// propriété que cette classe existe pour garantir.
/// </summary>
public class LibelleSourceTests
{
    private static readonly System.DateTimeOffset Maintenant =
        new(2026, 9, 12, 12, 0, 0, System.TimeSpan.Zero);

    // ==================== Le nom du producteur ====================

    /// <summary>Les cinq membres de l'enum, nommés un par un. Ce sont ces chaînes que l'utilisateur
    /// lira dans le diagnostic : « alimenté par la sonde d'en-têtes de rate-limit ».</summary>
    [Theory]
    [InlineData(SourceUsage.SondeEnTetes, "sonde d'en-têtes de rate-limit")]
    [InlineData(SourceUsage.EndpointOAuthChronos, "endpoint OAuth (login Chronos)")]
    [InlineData(SourceUsage.EndpointOAuthClaude, "endpoint OAuth (jeton app bureau / CLI)")]
    [InlineData(SourceUsage.PontStatusLine, "pont statusLine Claude Code")]
    [InlineData(SourceUsage.MagasinDernierExact, "dernier exact persisté")]
    public void Chaque_source_a_son_libelle_francais(SourceUsage source, string attendu)
        => Assert.Equal(attendu, LibelleSource.Format(source));

    /// <summary>Une fenêtre qui ne nomme personne ne se voit JAMAIS attribuer un producteur par défaut.
    /// Un repli qui dirait « pont statusLine » ferait croire à une source vivante là où il n'y a
    /// qu'une absence d'information.</summary>
    [Fact]
    public void Une_source_absente_ne_recoit_aucun_nom_de_producteur()
    {
        var libelle = LibelleSource.Format(null);

        Assert.Equal("non renseignée", libelle);
        Assert.DoesNotContain("sonde", libelle);
        Assert.DoesNotContain("OAuth", libelle);
    }

    /// <summary>
    /// GARDE D'EXHAUSTIVITÉ, par réflexion : un membre ajouté à l'enum sans libellé tomberait
    /// silencieusement sur le repli, et le diagnostic afficherait « alimenté par : non renseignée »
    /// alors qu'une source bien identifiée l'alimente. Ce test transforme cet oubli en échec.
    /// </summary>
    [Fact]
    public void Aucun_membre_de_l_enum_ne_retombe_sur_le_libelle_de_repli()
    {
        var membres = System.Enum.GetValues<SourceUsage>();

        Assert.True(membres.Length >= 5, $"Seulement {membres.Length} membres vus : la garde est muette.");

        foreach (var m in membres)
        {
            var libelle = LibelleSource.Format(m);
            Assert.False(string.IsNullOrWhiteSpace(libelle), $"{m} n'a aucun libellé français.");
            Assert.NotEqual(LibelleSource.Format(null), libelle);
        }
    }

    // ==================== L'ancienneté du relevé ====================

    /// <summary>Sans horodatage, on ne prétend pas connaître l'âge. Surtout pas « à l'instant » :
    /// la phase 19 a établi qu'un exact sans horodatage est INCERTIFIABLE, pas frais.</summary>
    [Fact]
    public void Un_releve_sans_horodatage_est_de_date_inconnue()
        => Assert.Equal("de date inconnue", LibelleSource.Anciennete(null, Maintenant));

    [Fact]
    public void Moins_d_une_minute_se_dit_a_l_instant()
        => Assert.Equal("à l'instant",
                        LibelleSource.Anciennete(Maintenant.AddSeconds(-30), Maintenant));

    [Fact]
    public void Sous_l_heure_l_anciennete_se_compte_en_minutes()
        => Assert.Equal("il y a 12 min",
                        LibelleSource.Anciennete(Maintenant.AddMinutes(-12), Maintenant));

    [Fact]
    public void Au_dela_de_l_heure_les_minutes_sont_sur_deux_chiffres()
        => Assert.Equal("il y a 3 h 12",
                        LibelleSource.Anciennete(Maintenant.AddMinutes(-192), Maintenant));

    [Fact]
    public void Au_dela_de_la_journee_l_anciennete_se_compte_en_jours()
        => Assert.Equal("il y a 2 j",
                        LibelleSource.Anciennete(Maintenant.AddDays(-2), Maintenant));

    /// <summary>Bascule exacte à la 60ᵉ minute : c'est la borne, donc c'est là que se cache l'erreur
    /// d'un cran. « il y a 60 min » serait juste mais illisible ; « il y a 1 h 00 » est la forme
    /// retenue, et la minute est bien sur deux chiffres.</summary>
    [Fact]
    public void La_soixantieme_minute_bascule_sur_la_forme_en_heures()
        => Assert.Equal("il y a 1 h 00",
                        LibelleSource.Anciennete(Maintenant.AddMinutes(-60), Maintenant));

    /// <summary>
    /// HORLOGE QUI RECULE — un relevé horodaté dans le FUTUR ne produit jamais une durée négative.
    /// Le cas est réel : les sources horodatent côté serveur, la machine côté client, et rien ne
    /// garantit leur accord. « il y a -5 min » ferait passer un défaut d'horloge pour une précision.
    /// </summary>
    [Fact]
    public void Un_releve_horodate_dans_le_futur_se_dit_a_l_instant_jamais_une_duree_negative()
    {
        var libelle = LibelleSource.Anciennete(Maintenant.AddMinutes(5), Maintenant);

        Assert.Equal("à l'instant", libelle);
        Assert.DoesNotContain("-", libelle);
    }

    // ==================== Ce qu'on a vérifié ====================

    /// <summary>Les trois provenances de la doctrine. « borne inférieure » et non « estimation » :
    /// l'incertitude d'un plancher est UNILATÉRALE (DEL-04).</summary>
    [Theory]
    [InlineData(ProvenanceReleve.Frais, "frais")]
    [InlineData(ProvenanceReleve.EncoreValide, "encore valide (aucune activité depuis)")]
    [InlineData(ProvenanceReleve.PlancherAvecActivite, "borne inférieure (activité depuis)")]
    public void Chaque_provenance_a_son_libelle_francais(ProvenanceReleve p, string attendu)
        => Assert.Equal(attendu, LibelleSource.Provenance(p));

    /// <summary>La doctrine n'a pas statué → on ne dit RIEN. Pas « frais » par défaut : l'absence de
    /// jugement n'est pas un bon jugement.</summary>
    [Fact]
    public void Une_provenance_non_statuee_rend_la_chaine_vide()
        => Assert.Equal("", LibelleSource.Provenance(null));
}
