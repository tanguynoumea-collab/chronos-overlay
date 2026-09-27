using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// TOK-04 — le rendu LOCAL des agrégats, juste aux changements d'heure (critère 4 de la phase).
///
/// Ce que ces tests gravent : le 25/10/2026 (Paris) compte 25 barres dont deux libellées « 02:00 » (+02:00 puis
/// +01:00) ; le 28/03/2027 — un DIMANCHE ; le 29 est un lundi ordinaire de 24 h, prouvé ici — compte 23 barres et
/// aucune « 02:00 » ; un jour ordinaire en compte 24 ; une barre = un DÉBUT D'HEURE UTC de la plage, libellé en local
/// (D-33-18) — deux tranches à « 02:30 local » restent deux barres ; la plage vient de <c>BornesPlage.Jour</c>, jamais
/// d'un TimeSpan de 24 h ; chaque barre et chaque colonne porte son état de couverture (D-33-19) ; la vue Jour empile
/// par modèle, sous-agents inclus (D-33-20) ; la part sous-agents est un couple d'ENTIERS, jamais un rapport (TOK-05).
///
/// Fuseau INJECTÉ (<c>BornesPlage.FuseauParisPourTests()</c>, D-33-21) ; fixtures versionnées sous TestData/tokens/ ;
/// aucune E/S hors des fixtures.
/// </summary>
public class RenduLocalTokensTests
{
    private static readonly TimeZoneInfo Paris = BornesPlage.FuseauParisPourTests();

    private static string TestDataDir(string cas, [CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "tokens", cas);

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static IReadOnlyList<TrancheTokens> Charger(string cas, Plage plage)
        => LecteurAgregats.Lire(TestDataDir(cas), plage.Debut, plage.Fin).Tranches;

    private static TrancheTokens Tranche(string slot, string model, bool sub, long entree, long sortie, long cacheW, long cacheR, int n)
        => new(Utc(slot), model, sub, entree, sortie, cacheW, cacheR, n);

    // Couverture qui garantit octobre 2026 et mars 2027 (passes complètes le 01/11/2026 et le 10/04/2027, 30 j de recul chacune).
    private static CouvertureTokens Couverte()
    {
        var c = new CouvertureTokens();
        c.VoirLigne(Utc("2026-01-01T00:00:00Z"));
        c.GarantirPasse(Utc("2026-11-01T00:00:00Z"), Utc("2026-11-01T00:01:00Z"));
        c.GarantirPasse(Utc("2027-04-10T00:00:00Z"), Utc("2027-04-10T00:01:00Z"));
        return c;
    }

    [Fact]
    public void Le_25_octobre_2026_a_vingt_cinq_barres_dont_deux_a_02h()
    {
        var plage = BornesPlage.Jour(Utc("2026-10-25T12:00:00Z"), Paris);
        Assert.Equal(TimeSpan.FromHours(25), plage.Duree);

        var barres = RenduLocalTokens.ParHeure(Charger("dst-2026-10-25", plage), plage, Paris, Couverte());

        Assert.Equal(25, barres.Count);
        Assert.Equal("00:00", barres[0].Libelle);
        Assert.Equal("01:00", barres[1].Libelle);
        Assert.Equal("02:00", barres[2].Libelle);   // +02:00
        Assert.Equal("02:00", barres[3].Libelle);   // +01:00 : la même heure d'horloge, une heure plus tard
        Assert.Equal("03:00", barres[4].Libelle);
        Assert.Equal("23:00", barres[24].Libelle);
        Assert.Equal(Utc("2026-10-25T00:00:00Z"), barres[2].DebutUtc);
        Assert.Equal(Utc("2026-10-25T01:00:00Z"), barres[3].DebutUtc);
        Assert.All(barres, b =>
        {
            Assert.Equal(4, b.Principal.N);
            Assert.Equal(4, b.Principal.Out);
            Assert.Equal(TotauxTokens.Vide, b.SousAgents);
            Assert.Equal(EtatCouverture.Couverte, b.Etat);
        });
        Assert.Equal(100, barres.Sum(b => b.Principal.N));
    }

    [Fact]
    public void Le_28_mars_2027_a_vingt_trois_barres_sans_02h()
    {
        var plage = BornesPlage.Jour(Utc("2027-03-28T12:00:00Z"), Paris);
        Assert.Equal(TimeSpan.FromHours(23), plage.Duree);

        var barres = RenduLocalTokens.ParHeure(Charger("dst-2027-03-28", plage), plage, Paris, Couverte());

        Assert.Equal(23, barres.Count);
        Assert.DoesNotContain("02:00", barres.Select(b => b.Libelle));
        Assert.Equal("00:00", barres[0].Libelle);
        Assert.Equal("01:00", barres[1].Libelle);
        Assert.Equal("03:00", barres[2].Libelle);   // 01:00Z : l'horloge saute de 02:00 à 03:00
        Assert.Equal("23:00", barres[22].Libelle);
        Assert.Equal(23, barres.Select(b => b.Libelle).Distinct().Count());
        Assert.Equal(92, barres.Sum(b => b.Principal.N));
        Assert.All(barres, b => Assert.Equal(EtatCouverture.Couverte, b.Etat));

        // Pitfall 3 : le 29/03/2027 est un LUNDI ordinaire — 24 h, 24 barres. La coquille « 29/03 » de l'énoncé initial est morte ici.
        var lundi = BornesPlage.Jour(Utc("2027-03-29T12:00:00Z"), Paris);
        Assert.Equal(TimeSpan.FromHours(24), lundi.Duree);
        Assert.Equal(24, RenduLocalTokens.ParHeure(Array.Empty<TrancheTokens>(), lundi, Paris, Couverte()).Count);
    }

    [Fact]
    public void Un_jour_ordinaire_a_vingt_quatre_barres_et_une_barre_sans_tranche_porte_son_etat()
    {
        var plage = BornesPlage.Jour(Utc("2026-09-26T12:00:00Z"), Paris);
        var aucune = Array.Empty<TrancheTokens>();

        // Couverture vide : rien n'a jamais été lu → tout est hors couverture, aucune barre n'est « zéro ».
        var horsCouverture = RenduLocalTokens.ParHeure(aucune, plage, Paris, new CouvertureTokens());
        Assert.Equal(24, horsCouverture.Count);
        Assert.All(horsCouverture, b =>
        {
            Assert.Equal(TotauxTokens.Vide, b.Principal);
            Assert.Equal(TotauxTokens.Vide, b.SousAgents);
            Assert.Equal(EtatCouverture.HorsCouverture, b.Etat);
        });
        Assert.Equal("00:00", horsCouverture[0].Libelle);
        Assert.Equal("23:00", horsCouverture[23].Libelle);

        // Après la plus ancienne ligne vue mais hors de tout intervalle garanti : transcripts absents.
        var absents = RenduLocalTokens.ParHeure(aucune, plage, Paris, Couverte());
        Assert.All(absents, b => Assert.Equal(EtatCouverture.TranscriptsAbsents, b.Etat));

        // Dans un intervalle garanti : une barre vide est VRAIMENT zéro activité.
        var c = Couverte();
        c.GarantirPasse(Utc("2026-10-01T00:00:00Z"), Utc("2026-10-01T00:01:00Z"));   // garantit [01/09, 01/10 00:01[
        var couverte = RenduLocalTokens.ParHeure(aucune, plage, Paris, c);
        Assert.All(couverte, b => Assert.Equal(EtatCouverture.Couverte, b.Etat));
    }

    [Fact]
    public void Le_regroupement_se_fait_sur_l_heure_UTC_pas_sur_l_heure_locale()
    {
        var plage = BornesPlage.Jour(Utc("2026-10-25T12:00:00Z"), Paris);
        var tranches = new[]
        {
            Tranche("2026-10-25T00:30:00Z", "claude-opus-5", false, 1, 10, 0, 0, 1),   // 02:30 local, +02:00
            Tranche("2026-10-25T01:30:00Z", "claude-opus-5", false, 1, 20, 0, 0, 1),   // 02:30 local, +01:00 — une heure PLUS TARD
        };

        var barres = RenduLocalTokens.ParHeure(tranches, plage, Paris, Couverte());

        // Deux barres distinctes, toutes deux libellées « 02:00 » : un regroupement sur l'heure locale les aurait fusionnées.
        Assert.Equal("02:00", barres[2].Libelle);
        Assert.Equal("02:00", barres[3].Libelle);
        Assert.Equal(10, barres[2].Principal.Out);
        Assert.Equal(20, barres[3].Principal.Out);
        Assert.Equal(1, barres[2].Principal.N);
        Assert.Equal(1, barres[3].Principal.N);
        Assert.Equal(2, barres.Sum(b => b.Principal.N));
        Assert.All(barres.Where((_, i) => i != 2 && i != 3), b => Assert.Equal(TotauxTokens.Vide, b.Principal));
    }

    [Fact]
    public void La_vue_jour_donne_une_colonne_par_quart_d_heure_empilee_par_modele_sous_agents_inclus()
    {
        var plage = BornesPlage.Jour(Utc("2026-10-25T12:00:00Z"), Paris);
        var tranches = Charger("dst-2026-10-25", plage)
            .Append(Tranche("2026-10-24T22:00:00Z", "claude-opus-5", true, 1, 9, 0, 0, 1))
            .Append(Tranche("2026-10-24T22:00:00Z", "claude-sonnet-5", false, 1, 5, 0, 0, 1))
            .ToList();

        var colonnes = RenduLocalTokens.ParQuartDHeure(tranches, plage, Paris, Couverte());

        Assert.Equal(100, colonnes.Count);   // 25 h × 4
        Assert.Equal(Utc("2026-10-24T22:00:00Z"), colonnes[0].Slot);
        Assert.Equal("00:00", colonnes[0].Libelle);
        Assert.Equal("00:15", colonnes[1].Libelle);
        Assert.Equal("02:00", colonnes[8].Libelle);    // 00:00Z, +02:00
        Assert.Equal("02:00", colonnes[12].Libelle);   // 01:00Z, +01:00
        Assert.Equal("23:45", colonnes[99].Libelle);

        // Colonne 0 : opus (fixture 1/1 + sous-agent 1/9 = 2/10, N 2 — sub FUSIONNÉ dans le modèle), puis sonnet (1/5) : Out décroissant.
        Assert.Equal(
            new[]
            {
                new PartModele("claude-opus-5", new TotauxTokens(2, 10, 0, 0, 2)),
                new PartModele("claude-sonnet-5", new TotauxTokens(1, 5, 0, 0, 1)),
            },
            colonnes[0].ParModele);
        Assert.Single(colonnes[1].ParModele);
        Assert.Equal(new PartModele("claude-opus-5", new TotauxTokens(1, 1, 0, 0, 1)), colonnes[1].ParModele[0]);
        Assert.All(colonnes, c => Assert.Equal(EtatCouverture.Couverte, c.Etat));

        // L'état vient de la couverture, pas des tranches : même colonnes, couverture vide → hors couverture.
        var sansCouverture = RenduLocalTokens.ParQuartDHeure(tranches, plage, Paris, new CouvertureTokens());
        Assert.All(sansCouverture, c => Assert.Equal(EtatCouverture.HorsCouverture, c.Etat));
        Assert.Equal(colonnes[0].ParModele, sansCouverture[0].ParModele);
    }

    [Fact]
    public void La_part_sous_agents_est_un_couple_de_totaux_entiers()
    {
        var tranches = new[]
        {
            Tranche("2026-10-25T10:00:00Z", "claude-opus-5", true, 10, 100, 1000, 5000, 1),
            Tranche("2026-10-25T10:15:00Z", "claude-opus-5", true, 20, 200, 0, 6000, 1),
            Tranche("2026-10-25T10:30:00Z", "claude-sonnet-5", true, 30, 300, 2000, 7000, 1),
            Tranche("2026-10-25T10:00:00Z", "claude-opus-5", false, 5, 50, 500, 2500, 1),
        };

        var (sousAgents, principal) = RenduLocalTokens.PartSousAgents(tranches);

        Assert.Equal(new TotauxTokens(60, 600, 3000, 18000, 3), sousAgents);
        Assert.Equal(new TotauxTokens(5, 50, 500, 2500, 1), principal);
        Assert.Equal((TotauxTokens.Vide, TotauxTokens.Vide), RenduLocalTokens.PartSousAgents(Array.Empty<TrancheTokens>()));

        // Par réflexion : le retour est un couple de TotauxTokens, et TotauxTokens n'expose que des long / int — jamais un rapport.
        var retour = typeof(RenduLocalTokens).GetMethod(nameof(RenduLocalTokens.PartSousAgents))!.ReturnType;
        Assert.True(retour.IsGenericType);
        Assert.All(retour.GetGenericArguments(), t => Assert.Equal(typeof(TotauxTokens), t));
        Assert.All(typeof(TotauxTokens).GetProperties(), p => Assert.True(p.PropertyType == typeof(long) || p.PropertyType == typeof(int), $"{p.Name} : {p.PropertyType}"));
    }

    [Fact]
    public void Les_sous_plages_de_couverture_decoupent_la_plage_aux_bornes_connues()
    {
        var c = new CouvertureTokens();
        c.VoirLigne(Utc("2026-06-23T12:44:22Z"));
        c.GarantirPasse(Utc("2026-09-27T09:00:00Z"), Utc("2026-09-27T09:05:00Z"));   // garantit [28/08 09:00Z, 27/09 09:05Z[

        var sousPlages = RenduLocalTokens.SousPlagesCouverture(new Plage(Utc("2026-06-01T00:00:00Z"), Utc("2026-10-01T00:00:00Z")), c);

        Assert.Equal(
            new[]
            {
                new SousPlageCouverture(Utc("2026-06-01T00:00:00Z"), Utc("2026-06-23T12:44:22Z"), EtatCouverture.HorsCouverture),
                new SousPlageCouverture(Utc("2026-06-23T12:44:22Z"), Utc("2026-08-28T09:00:00Z"), EtatCouverture.TranscriptsAbsents),
                new SousPlageCouverture(Utc("2026-08-28T09:00:00Z"), Utc("2026-09-27T09:05:00Z"), EtatCouverture.Couverte),
                new SousPlageCouverture(Utc("2026-09-27T09:05:00Z"), Utc("2026-10-01T00:00:00Z"), EtatCouverture.TranscriptsAbsents),
            },
            sousPlages);

        // Plage entièrement dans un intervalle garanti : une seule sous-plage, couverte.
        var dedans = RenduLocalTokens.SousPlagesCouverture(new Plage(Utc("2026-09-01T00:00:00Z"), Utc("2026-09-08T00:00:00Z")), c);
        Assert.Equal(new[] { new SousPlageCouverture(Utc("2026-09-01T00:00:00Z"), Utc("2026-09-08T00:00:00Z"), EtatCouverture.Couverte) }, dedans);

        // Plage vide : rien.
        Assert.Empty(RenduLocalTokens.SousPlagesCouverture(new Plage(Utc("2026-09-01T00:00:00Z"), Utc("2026-09-01T00:00:00Z")), c));
    }
}
