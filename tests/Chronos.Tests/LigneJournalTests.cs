using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// JRN-03 — une ligne de journal s'écrit et se relit sans ambiguïté, et une ligne cassée se saute sans
/// casser le fichier.
///
/// Ce que ces tests gravent : l'ORDRE et les NOMS des champs sont ceux du contrat de données
/// (.zeus/DESIGN_PLAN.md §3) ; un <c>double</c> part BRUT (D-32-18 : la granularité 0,01 des en-têtes
/// est une hypothèse à vérifier AVEC le journal, pas à figer avant) et jamais localisé (piège fr-FR
/// déjà rencontré par <c>UsageNormalization</c>) ; <c>v</c> est lu EN PREMIER ; un <c>ev</c> inconnu est
/// conservé, pas jeté (le vocabulaire des événements est ouvert : <c>ecriture_ratee</c> est déjà une
/// extension du conseil).
///
/// Tests purs : aucune E/S hors de la fixture versionnée, aucun %APPDATA%, aucune horloge système.
/// </summary>
public class LigneJournalTests
{
    private static readonly DateTimeOffset T = new(2026, 09, 27, 10, 05, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset R5 = new(2026, 09, 27, 14, 50, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset R7 = new(2026, 10, 02, 22, 0, 0, TimeSpan.Zero);

    private static string CheminFixture(string relatif, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "journal", relatif);

    private static ReleveJournal ReleveComplet() => new(
        T, SourceUsage.SondeEnTetes,
        U5: 0.12, R5: R5, Statut5: StatutServeur.Autorise,
        U7: 0.41, R7: R7, Statut7: StatutServeur.AutoriseAvertissement,
        Overage: 0.05, OverageStatut: StatutServeur.Rejete);

    // --- Sérialisation ---

    [Fact]
    public void Un_releve_se_serialise_avec_les_champs_du_contrat_dans_l_ordre()
    {
        var ligne = LigneJournal.Serialiser(ReleveComplet());

        Assert.Equal(
            "{\"v\":1,\"t\":\"2026-09-27T10:05:00.0000000+00:00\","
            + "\"u5\":0.12,\"r5\":\"2026-09-27T14:50:00.0000000+00:00\","
            + "\"u7\":0.41,\"r7\":\"2026-10-02T22:00:00.0000000+00:00\","
            + "\"statut5\":\"Autorise\",\"statut7\":\"AutoriseAvertissement\","
            + "\"overage\":0.05,\"overage_statut\":\"Rejete\",\"source\":\"SondeEnTetes\"}",
            ligne);
        Assert.DoesNotContain("\n", ligne);
        Assert.DoesNotContain(" ", ligne);
    }

    [Fact]
    public void Les_champs_nuls_sont_omis()
    {
        var cinqHeuresSeule = new ReleveJournal(T, SourceUsage.SondeEnTetes, 0.12, R5, StatutServeur.Autorise,
                                                null, null, null, null, null);

        var ligne = LigneJournal.Serialiser(cinqHeuresSeule);

        Assert.DoesNotContain("\"u7\"", ligne);
        Assert.DoesNotContain("\"r7\"", ligne);
        Assert.DoesNotContain("\"statut7\"", ligne);
        Assert.DoesNotContain("\"overage\"", ligne);
        Assert.DoesNotContain("\"overage_statut\"", ligne);
        Assert.Contains("\"u5\":0.12", ligne);
        Assert.Contains("\"source\":\"SondeEnTetes\"", ligne);
    }

    [Fact]
    public void Un_double_brut_n_est_jamais_arrondi_ni_localise()
    {
        var releve = new ReleveJournal(T, SourceUsage.SondeEnTetes, 0.123456789, R5, null, null, null, null, null, null);

        var cultureAvant = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");   // la culture où « 0,12 » guette

            var ligne = LigneJournal.Serialiser(releve);

            Assert.Contains("\"u5\":0.123456789", ligne);
            Assert.DoesNotContain("0,12", ligne);
            Assert.DoesNotContain("\"u5\":0.12,", ligne);
        }
        finally
        {
            CultureInfo.CurrentCulture = cultureAvant;
        }
    }

    [Fact]
    public void Un_evenement_se_serialise_avec_son_nom_de_fil()
    {
        var demarrage = LigneJournal.Serialiser(new EvenementJournal(T, TypeEvenement.Demarrage, Version: "3.2.2"));
        var ratee = LigneJournal.Serialiser(new EvenementJournal(T, TypeEvenement.EcritureRatee,
                                                                 Magasin: "last-exact", Cause: "IOException : x"));
        var reprise = LigneJournal.Serialiser(new EvenementJournal(T, TypeEvenement.Reprise, Cause: "trou de 25 min"));

        Assert.Equal("{\"v\":1,\"t\":\"2026-09-27T10:05:00.0000000+00:00\",\"ev\":\"demarrage\",\"version\":\"3.2.2\"}", demarrage);
        Assert.Equal("{\"v\":1,\"t\":\"2026-09-27T10:05:00.0000000+00:00\",\"ev\":\"ecriture_ratee\","
                     + "\"magasin\":\"last-exact\",\"cause\":\"IOException : x\"}", ratee);
        Assert.Equal("{\"v\":1,\"t\":\"2026-09-27T10:05:00.0000000+00:00\",\"ev\":\"reprise\",\"cause\":\"trou de 25 min\"}", reprise);
    }

    // --- Aller-retour et refus ---

    [Fact]
    public void Serialiser_puis_Parser_rend_le_meme_releve()
    {
        var releve = ReleveComplet();
        var evenement = new EvenementJournal(T, TypeEvenement.EcritureRatee, Magasin: "last-exact", Cause: "IOException : x");

        Assert.True(LigneJournal.Parser(LigneJournal.Serialiser(releve), out var releveRelu, out var evenementNul));
        Assert.Null(evenementNul);
        Assert.Equal(releve, releveRelu);   // égalité de record : chaque champ, dont les nullables

        Assert.True(LigneJournal.Parser(LigneJournal.Serialiser(evenement), out var releveNul, out var evenementRelu));
        Assert.Null(releveNul);
        // EvBrut est renseigné à la relecture (le nom de fil tel qu'il a été lu) : on compare le reste.
        Assert.Equal(evenement with { EvBrut = "ecriture_ratee" }, evenementRelu);
    }

    [Fact]
    public void Une_ligne_avec_v_inconnu_ou_sans_t_est_refusee()
    {
        Assert.False(LigneJournal.Parser("{\"v\":2,\"t\":\"2026-09-27T10:15:00.0000000+00:00\",\"u5\":0.5,\"source\":\"SondeEnTetes\"}",
                                         out _, out _));
        Assert.False(LigneJournal.Parser("{\"v\":1}", out _, out _));
        Assert.False(LigneJournal.Parser("{\"t\":\"2026-09-27T10:15:00.0000000+00:00\",\"u5\":0.5,\"source\":\"SondeEnTetes\"}",
                                         out _, out _));   // v absent : refusé AVANT de regarder t
        Assert.False(LigneJournal.Parser("", out _, out _));
        Assert.False(LigneJournal.Parser("{tronqué", out _, out _));
        Assert.False(LigneJournal.Parser("{\"v\":1,\"t\":\"pas-une-date\",\"ev\":\"arret\"}", out _, out _));
        // Un relevé sans source n'a pas de clé d'idempotence : il est ignoré, pas deviné.
        Assert.False(LigneJournal.Parser("{\"v\":1,\"t\":\"2026-09-27T10:15:00.0000000+00:00\",\"u5\":0.5}", out _, out _));
    }

    [Fact]
    public void Un_ev_inconnu_est_conserve_comme_non_reconnu()
    {
        var ok = LigneJournal.Parser("{\"v\":1,\"t\":\"2026-09-27T10:20:00.0000000+00:00\",\"ev\":\"teleportation\"}",
                                     out var releve, out var evenement);

        Assert.True(ok);
        Assert.Null(releve);
        Assert.NotNull(evenement);
        Assert.Equal(TypeEvenement.NonReconnu, evenement!.Type);
        Assert.Equal("teleportation", evenement.EvBrut);
    }

    [Fact]
    public void ChampsReleve_et_NomsEvenements_sont_ceux_du_contrat()
    {
        Assert.Equal(new[] { "v", "t", "u5", "r5", "u7", "r7", "statut5", "statut7", "overage", "overage_statut", "source" },
                     LigneJournal.ChampsReleve);
        Assert.Equal(new[] { "demarrage", "arret", "jeton_invalide", "sonde_refusee", "reprise", "ecriture_ratee" },
                     TypeEvenementTexte.NomsDeFil);

        // Un inconnu ne s'écrit JAMAIS : pas de nom de fil, et l'écrivain refuse.
        Assert.Null(TypeEvenementTexte.Nom(TypeEvenement.NonReconnu));
        Assert.Throws<ArgumentException>(() => LigneJournal.Serialiser(new EvenementJournal(T, TypeEvenement.NonReconnu)));

        // Chaque nom de fil revient à son type, et l'inconnu retombe sur NonReconnu.
        foreach (var nom in TypeEvenementTexte.NomsDeFil)
            Assert.Equal(nom, TypeEvenementTexte.Nom(TypeEvenementTexte.Depuis(nom)));
        Assert.Equal(TypeEvenement.NonReconnu, TypeEvenementTexte.Depuis("teleportation"));
        Assert.Equal(TypeEvenement.NonReconnu, TypeEvenementTexte.Depuis(null));
    }

    // --- Lecture tolérante d'un fichier ---

    [Fact]
    public void LireFichier_sur_la_fixture_de_tolerance_garde_les_lignes_valides_et_compte_les_autres()
    {
        var lecture = LecteurJournal.LireFichier(CheminFixture(Path.Combine("tolerance", "releves-2026-09.jsonl")));

        // 2 relevés : 10:05 (complet) et 10:30 (5 h seule, sans statut).
        Assert.Equal(2, lecture.Releves.Count);
        Assert.Equal(new DateTimeOffset(2026, 09, 27, 10, 05, 0, TimeSpan.Zero), lecture.Releves[0].T);
        Assert.Equal(0.41, lecture.Releves[0].U7);
        Assert.Equal(new DateTimeOffset(2026, 09, 27, 10, 30, 0, TimeSpan.Zero), lecture.Releves[1].T);
        Assert.Equal(0.13, lecture.Releves[1].U5);
        Assert.Null(lecture.Releves[1].U7);
        Assert.Null(lecture.Releves[1].R7);
        Assert.Null(lecture.Releves[1].Statut5);
        Assert.Equal(SourceUsage.SondeEnTetes, lecture.Releves[1].Source);

        // 2 événements : 10:20 non reconnu (conservé avec son nom brut), 10:25 arrêt.
        Assert.Equal(2, lecture.Evenements.Count);
        Assert.Equal(TypeEvenement.NonReconnu, lecture.Evenements[0].Type);
        Assert.Equal("teleportation", lecture.Evenements[0].EvBrut);
        Assert.Equal(new DateTimeOffset(2026, 09, 27, 10, 25, 0, TimeSpan.Zero), lecture.Evenements[1].T);
        Assert.Equal(TypeEvenement.Arret, lecture.Evenements[1].Type);

        // 4 lignes ignorées : tronquée, v=2, sans t, t illisible. La ligne vide n'est ni lue ni comptée.
        Assert.Equal(4, lecture.LignesIgnorees);
    }

    [Fact]
    public void LireFichier_sur_un_fichier_absent_rend_une_lecture_vide_sans_lever()
    {
        var chemin = Path.Combine(Path.GetTempPath(), "ChronosJournal_" + Guid.NewGuid().ToString("N"), "releves-2026-09.jsonl");

        var lecture = LecteurJournal.LireFichier(chemin);

        Assert.Empty(lecture.Releves);
        Assert.Empty(lecture.Evenements);
        Assert.Equal(0, lecture.LignesIgnorees);
    }
}
