using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Les QUATRE branches de la doctrine (EXA-02, EXA-04, DEL-03, DEL-04), chacune avec au moins un test
/// qui TOMBE si la branche disparaît — la falsifiabilité de chacune a été jouée par mutation réelle du
/// code de production puis révoquée (inventaire dans le SUMMARY 19-02).
///
/// Tests PURS : <see cref="DoctrineFraicheur"/> n'a ni E/S, ni horloge propre, ni type WPF. <c>now</c>
/// est toujours un paramètre, donc les quatre branches sont déterministes en [Fact] classique — aucune
/// attente, aucun ordonnancement, aucun faux à programmer hors du journal d'activité lui-même.
/// </summary>
public class DoctrineFraicheurTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Un relevé exact CERTIFIABLE : il porte une utilization ET l'instant de sa capture.</summary>
    private static WindowState Exact(double util, DateTimeOffset captured, DateTimeOffset? resets = null)
        => new()
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = util,
            CapturedAt = captured,
            ResetsAt = resets ?? Now.AddHours(3),
        };

    /// <summary>Journal d'horizon nominal (8 jours, comme le filtre de parcours disque de la phase 16).</summary>
    private static TranscriptActivityLog Journal(params (DateTimeOffset Ts, long Tokens)[] e)
        => new(Now, Now - TimeSpan.FromDays(8), e);

    private static WindowState Muette => WindowState.Unavailable(WindowKind.FiveHour);

    // ---------------------------------------------------------------- Branche 1 : Exact / Frais

    [Fact]
    public void Releve_sous_la_limite_est_exact_frais()
    {
        // POURQUOI : EXA-02 est un LAISSEZ-PASSER. Sous la limite d'âge, le chiffre est servi tel quel,
        // sans qu'aucune preuve ne soit exigée — et donc sans qu'aucune passe disque ne soit payée.
        var w = DoctrineFraicheur.Statuer(Exact(0.42, Now.AddMinutes(-1)), memorisee: null,
                                          journal: null, Now);

        Assert.Equal(SourceReliability.Exact, w.Reliability);
        Assert.Equal(ProvenanceReleve.Frais, w.Provenance);
        Assert.Equal(0.42, w.Utilization);
        Assert.Null(w.TokensDepuisReleve);   // rien n'a été mesuré : ne rien affirmer
    }

    [Fact]
    public void La_branche_fraiche_ne_consulte_PAS_le_journal()
    {
        // POURQUOI : le chemin nominal ne doit JAMAIS payer la passe de transcripts (2,7-3,2 s et
        // 536 Mo sur la machine cible). Ici la preuve est sémantique et non instrumentale : si la
        // branche 1 regardait le journal, un journal riche changerait le verdict. Il ne le change pas.
        var vivante = Exact(0.42, Now.AddMinutes(-1));

        var sans = DoctrineFraicheur.Statuer(vivante, memorisee: null, journal: null, Now);
        var avec = DoctrineFraicheur.Statuer(vivante, memorisee: null,
                                             Journal((Now.AddMinutes(-2), 9_000)), Now);

        Assert.Equal(sans, avec);            // égalité de record : verdict rigoureusement identique
    }

    // ------------------------------------------- Branche 2 : Exact / EncoreValide (DEL-03)

    [Fact]
    public void Trois_heures_sans_aucune_activite_reste_EXACT_et_non_perime()
    {
        // POURQUOI : l'utilisation est fonction de la consommation ; sans consommation, elle ne bouge
        // pas. « Zéro réponse assistant depuis T » n'est donc pas une approximation mais une DÉDUCTION.
        // Déclarer périmé un chiffre qu'on peut PROUVER juste reviendrait à cacher une information vraie,
        // et l'overlay deviendrait muet précisément quand il est le plus utile (retour le matin, rien
        // n'a tourné). La seule entrée du journal est ANTÉRIEURE au relevé : elle ne compte pas.
        var memorisee = Exact(0.42, Now.AddHours(-3));

        var w = DoctrineFraicheur.Statuer(Muette, memorisee,
                                          Journal((Now.AddHours(-4), 5_000)), Now);

        Assert.Equal(SourceReliability.Exact, w.Reliability);
        Assert.Equal(ProvenanceReleve.EncoreValide, w.Provenance);
        Assert.Equal(0.42, w.Utilization);
        Assert.Equal(0L, w.TokensDepuisReleve);   // mesuré, et mesuré à zéro : ce n'est pas « inconnu »
    }

    // ------------------------------------ Branche 3 : Estimated / PlancherAvecActivite (DEL-04)

    [Fact]
    public void Activite_depuis_le_releve_donne_un_plancher_marque()
    {
        // POURQUOI : avec de l'activité, le chiffre cesse d'être une valeur et devient une BORNE
        // INFÉRIEURE. Estimated est réaffecté à cette sémantique (il est mort en production depuis la
        // phase 16) : son câblage de présentation dit déjà « ce chiffre n'est pas un exact courant ».
        // Les tokens accompagnent comme matière première BRUTE, jamais convertis.
        var memorisee = Exact(0.42, Now.AddHours(-3));

        var w = DoctrineFraicheur.Statuer(Muette, memorisee,
                                          Journal((Now.AddHours(-2), 120_000),
                                                  (Now.AddMinutes(-5), 880_000)), Now);

        Assert.Equal(SourceReliability.Estimated, w.Reliability);
        Assert.Equal(ProvenanceReleve.PlancherAvecActivite, w.Provenance);
        Assert.Equal(1_000_000L, w.TokensDepuisReleve);
    }

    [Fact]
    public void Le_plancher_ne_gonfle_JAMAIS_l_utilization()
    {
        // EXA-04 — LE test du milestone. Convertir des tokens en points de pourcentage exige un taux,
        // c'est-à-dire un plafond, que trois faits rendent faux : les limites Anthropic pondèrent par
        // modèle ; les transcripts ignorent l'app de bureau et Cowork, qui consomment le même pool ; et
        // la mesure du 2026-09-12 donne 643 649 933 tokens sur 5 h, là où l'ancien plafond valait
        // 230 000 000 — soit 280 %. Un delta chiffré serait une fiction. Le delta change donc la NATURE
        // du chiffre (« 42 % » devient « au moins 42 % »), jamais sa valeur.
        var memorisee = Exact(0.42, Now.AddHours(-3));

        var w = DoctrineFraicheur.Statuer(Muette, memorisee,
                                          Journal((Now.AddHours(-2), 120_000),
                                                  (Now.AddMinutes(-5), 880_000)), Now);

        Assert.Equal(0.42, w.Utilization);   // rigoureusement la valeur mémorisée, à l'identique
    }

    // ------------------------------------------------ Branche 4 : Unavailable (4a puis 4b)

    [Fact]
    public void Releve_anterieur_a_l_horizon_des_transcripts_est_indisponible_et_non_sous_evalue()
    {
        // POURQUOI : c'est ICI que meurt le « 10 % » figé depuis deux mois qui a motivé le milestone —
        // par Covers(T), et non par la limite d'âge. Le filtre mtime de 8 jours rendrait un delta
        // SILENCIEUSEMENT sous-évalué : la doctrine refuse alors de produire un delta qu'elle ne peut
        // pas garantir, plutôt que d'en inventer un.
        var t = Now.AddDays(-60);
        var journal = Journal();

        Assert.False(journal.Covers(t));     // la prémisse, rendue explicite plutôt que supposée

        var w = DoctrineFraicheur.Statuer(Muette, Exact(0.10, t), journal, Now);

        Assert.Equal(SourceReliability.Unavailable, w.Reliability);
        Assert.Null(w.Utilization);
        Assert.Null(w.Provenance);
    }

    [Fact]
    public void Un_exact_vivant_sans_CapturedAt_est_incertifiable_et_ne_passe_pas_pour_frais()
    {
        // EXA-02, seconde moitié : un relevé marqué exact dont personne ne sait QUAND il a été pris est
        // incertifiable — on ne peut ni mesurer son âge, ni poser la question « activité depuis T ? ».
        // Rendre un âge inconnu (et non zéro) est le cœur de la correction.
        var vivante = new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = 0.10,
            CapturedAt = null,
        };

        var w = DoctrineFraicheur.Statuer(vivante, memorisee: null, journal: null, Now);

        Assert.Equal(SourceReliability.Unavailable, w.Reliability);
        Assert.Null(w.Utilization);
    }

    [Fact]
    public void Sans_journal_un_releve_trop_vieux_est_indisponible()
    {
        // Au-delà de la limite, la preuve est EXIGÉE. Pas de journal = pas de preuve = pas de chiffre.
        var w = DoctrineFraicheur.Statuer(Exact(0.42, Now.AddHours(-3)), memorisee: null,
                                          journal: null, Now);

        Assert.Equal(SourceReliability.Unavailable, w.Reliability);
        Assert.Null(w.Utilization);
    }

    // ------------------------------------------------------------- Préséance et conservation

    [Fact]
    public void Un_exact_vivant_certifiable_prime_toujours_sur_le_magasin()
    {
        // Le magasin est un FILET, pas un concurrent : il ne prend la main que lorsque la chaîne vivante
        // ne produit rien de certifiable. Inverser cette préséance ferait vivre un chiffre mémorisé
        // par-dessus une mesure fraîche.
        var w = DoctrineFraicheur.Statuer(Exact(0.20, Now), Exact(0.80, Now.AddHours(-2)),
                                          journal: null, Now);

        Assert.Equal(0.20, w.Utilization);
    }

    [Fact]
    public void La_demotion_conserve_le_reset_et_le_statut_serveur_mais_efface_le_pourcentage()
    {
        // POURQUOI l'effacement : WindowGaugeViewModel.Apply affecte Utilization SANS consulter
        // Reliability. Une fenêtre indisponible qui conserverait 0,10 peindrait encore l'arc à dix pour
        // cent, et le thème lui donnerait sa couleur — le bug survivrait à sa propre correction.
        // POURQUOI la conservation : un reset connu et un statut déclaré par le serveur restent des
        // FAITS, indépendants de l'âge du pourcentage qui les accompagnait.
        var vivante = new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = 0.10,
            CapturedAt = null,
            ResetsAt = Now.AddHours(2),
            StatutServeur = StatutServeur.Autorise,
        };

        var w = DoctrineFraicheur.Statuer(vivante, memorisee: null, journal: null, Now);

        Assert.Null(w.Utilization);
        Assert.Equal(Now.AddHours(2), w.ResetsAt);
        Assert.Equal(StatutServeur.Autorise, w.StatutServeur);
    }

    [Fact]
    public void Le_statut_serveur_du_tick_survit_a_une_substitution_par_le_magasin()
    {
        // HDR-03/HDR-04 (phase 18) disparaîtraient à la PREMIÈRE substitution par le magasin si le
        // statut du tick n'était pas reporté : le magasin ne persiste ni le statut serveur ni le
        // dépassement, et la sonde d'en-têtes en est l'unique porteuse.
        var vivante = Muette with { StatutServeur = StatutServeur.Autorise };

        var w = DoctrineFraicheur.Statuer(vivante, Exact(0.42, Now.AddMinutes(-1)),
                                          journal: null, Now);

        Assert.Equal(ProvenanceReleve.Frais, w.Provenance);
        Assert.Equal(StatutServeur.Autorise, w.StatutServeur);
    }

    // ------------------------------------------------- Cohérence de la paresse (contrat 19-03)

    [Fact]
    public void ABesoinDuJournal_est_coherent_avec_Statuer()
    {
        // CONTRAT : quand ABesoinDuJournal rend false, le plan 19-03 s'autorisera à ne PAS payer la
        // passe disque. Cette économie n'est légitime que si elle ne change rien au verdict. Ce test
        // l'impose sur une matrice de cinq cas, par égalité de record.
        var sansHorodatage = new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = 0.10,
            CapturedAt = null,
        };

        var cas = new (string Nom, WindowState Vivante, WindowState? Memorisee, bool BesoinAttendu)[]
        {
            ("frais",                       Exact(0.42, Now.AddMinutes(-1)), null,                            false),
            ("vieux",                       Muette,                          Exact(0.42, Now.AddHours(-3)),   true),
            ("vivante sans horodatage",     sansHorodatage,                  null,                            false),
            ("magasin sans horodatage",     Muette,                          sansHorodatage,                  false),
            ("aucun candidat",              Muette,                          null,                            false),
        };

        var riche = Journal((Now.AddMinutes(-30), 777_000));

        foreach (var c in cas)
        {
            var besoin = DoctrineFraicheur.ABesoinDuJournal(c.Vivante, c.Memorisee, Now);
            Assert.Equal(c.BesoinAttendu, besoin);

            if (besoin) continue;

            Assert.Equal(DoctrineFraicheur.Statuer(c.Vivante, c.Memorisee, riche, Now),
                         DoctrineFraicheur.Statuer(c.Vivante, c.Memorisee, null, Now));
        }
    }

    [Fact]
    public void La_limite_d_age_est_DERIVEE_de_la_cadence_de_la_sonde()
    {
        // Elle n'est pas un chiffre choisi : elle SUIT la cadence de la sonde. Au-dessus du cache
        // légitime de celle-ci, donc le chemin nominal reste gratuit ; au-dessous du cache OAuth de
        // 15 min, donc un relevé OAuth profond bascule vers la branche CERTIFIÉE, plus honnête.
        Assert.Equal(RateLimitHeaderUsageProvider.CadenceNominale + TimeSpan.FromSeconds(60),
                     DoctrineFraicheur.LimiteAge);
        Assert.True(DoctrineFraicheur.LimiteAge > RateLimitHeaderUsageProvider.CadenceNominale);
    }
}
