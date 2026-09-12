using System.Net;
using System.Net.Http;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// POINT UNIQUE des jeux d'en-têtes de référence de la phase 18. Les plans 18-03 et 18-04 les CONSOMMENT
/// et ne les redéclarent pas : un jeu dupliqué serait un jeu qui divergerait.
///
/// Chaque nom d'en-tête est une HYPOTHÈSE, pas un fait. La famille <c>anthropic-ratelimit-unified-*</c> est
/// ABSENTE de la documentation publique Anthropic (qui documente <c>-requests-*</c>, <c>-tokens-*</c> et
/// <c>retry-after</c>, avec <c>reset</c> en RFC 3339 et non en epoch). Les noms retenus ici proviennent du
/// code d'origine de <c>claude-session-browser</c> et d'un dump indépendant — d'où le niveau de confiance
/// indiqué sur chaque constante.
///
/// Les valeurs sont celles du dump réel, et leurs epochs sont POSTÉRIEURS au plancher de sanité
/// 2020-01-01 de <c>UsageNormalization</c> — sauf le jeu <see cref="Illisibles"/>, qui porte
/// volontairement l'epoch 1970 RÉEL trouvé dans le <c>usage.json</c> de cette machine.
/// </summary>
internal static class EnTetesDeReference
{
    // --- Noms d'en-têtes. CONFIANCE indiquée un par un : rien n'est présenté comme officiel. ---

    /// <summary>Observé dans un dump indépendant ET lu par le code d'origine. Fraction 0..1, en TEXTE.</summary>
    public const string H5hUtil = "anthropic-ratelimit-unified-5h-utilization";

    /// <summary>Observé dans un dump indépendant ET lu par le code d'origine. Epoch SECONDES, en TEXTE.</summary>
    public const string H5hReset = "anthropic-ratelimit-unified-5h-reset";

    /// <summary>Observé dans un dump indépendant ET lu par le code d'origine (valeur vue : allowed).</summary>
    public const string H5hStatut = "anthropic-ratelimit-unified-5h-status";

    /// <summary>Observé dans un dump indépendant ET lu par le code d'origine. Fraction 0..1.</summary>
    public const string H7dUtil = "anthropic-ratelimit-unified-7d-utilization";

    /// <summary>Observé dans un dump indépendant ET lu par le code d'origine. Epoch SECONDES.</summary>
    public const string H7dReset = "anthropic-ratelimit-unified-7d-reset";

    /// <summary>NON confirmé : aucune observation, aucune lecture dans le code d'origine. Lecture
    /// OPTIONNELLE — son absence ne doit jamais être traitée comme une anomalie.</summary>
    public const string H7dStatut = "anthropic-ratelimit-unified-7d-status";

    /// <summary>Lu par le code d'origine, dans une branche ALTERNATIVE liée au type de compte. Jamais
    /// observé sur cette machine (abonnement d'un autre type).</summary>
    public const string HOverUtil = "anthropic-ratelimit-unified-overage-utilization";

    /// <summary>Lu par le code d'origine (branche alternative). Epoch SECONDES.</summary>
    public const string HOverReset = "anthropic-ratelimit-unified-overage-reset";

    /// <summary>Annoncé par CONTEXT.md, NON confirmé : le code d'origine lit le statut global sans segment
    /// de fenêtre. Lu quand même, par précaution, et jamais exigé.</summary>
    public const string HOverStatut = "anthropic-ratelimit-unified-overage-status";

    /// <summary>Statut GLOBAL, SANS segment de fenêtre — c'est ce nom que lit réellement le code d'origine
    /// dans la branche de dépassement. Correction d'un écart de CONTEXT.md, établie par lecture du code.</summary>
    public const string HStatutGlobal = "anthropic-ratelimit-unified-status";

    /// <summary>Observé dans le dump (valeur vue : five_hour). Indique quelle fenêtre le serveur estime
    /// représentative. Purement informatif, jamais nécessaire à un calcul.</summary>
    public const string HClaim = "anthropic-ratelimit-unified-representative-claim";

    // --- Les sept formes réelles de réponse, plus la garde de casse. ---

    /// <summary>1. NOMINAL. Prouve HDR-01 : deux fenêtres exactes, fractions en 0..1 à point décimal,
    /// resets en epoch secondes. Valeurs reprises telles quelles du dump réel indépendant.</summary>
    public static IReadOnlyDictionary<string, string> Nominal => new Dictionary<string, string>
    {
        [H5hUtil] = "0.01",
        [H5hReset] = "1783180800",
        [H5hStatut] = "allowed",
        [H7dUtil] = "0.63",
        [H7dReset] = "1783713600",
        [HClaim] = "five_hour",
    };

    /// <summary>2. AVERTISSEMENT. Prouve HDR-03 : le serveur avertit AVANT de refuser, et cette nuance
    /// doit survivre jusqu'à l'affichage au lieu d'être écrasée en « autorisé ».</summary>
    public static IReadOnlyDictionary<string, string> Avertissement => Variante((H5hStatut, "allowed_warning"));

    /// <summary>3. REFUS. Le cas 429 : refusé ET épuisé en même temps. Les chiffres restent exacts pendant
    /// le refus — c'est toute la raison d'être de HDR-02.</summary>
    public static IReadOnlyDictionary<string, string> Refus => Variante((H5hStatut, "rejected"), (H5hUtil, "1.0"));

    /// <summary>4. STATUT INCONNU. Prouve que la lecture rend NonReconnu et JAMAIS Autorise. La valeur
    /// « active » est mentionnée par une proposition d'issue, que rien ne confirme : exactement le genre de
    /// valeur qui arrivera un jour sans préavis.</summary>
    public static IReadOnlyDictionary<string, string> StatutInconnu => Variante((H5hStatut, "active"));

    /// <summary>5. DÉPASSEMENT SEUL. Forme ALTERNATIVE liée au type de compte : AUCUNE fenêtre 5 h ni 7 j,
    /// et le statut sur le nom GLOBAL. Prouve HDR-04 dans le seul cas qui compte : les deux fenêtres sont
    /// légitimement Unavailable, donc Best() peut écarter les instances porteuses — d'où le canal latéral
    /// IEtatServeur.</summary>
    public static IReadOnlyDictionary<string, string> DepassementSeul => new Dictionary<string, string>
    {
        [HOverUtil] = "0.34",
        [HOverReset] = "1783800000",
        [HStatutGlobal] = "allowed_warning",
    };

    /// <summary>6. ABSENTS. Cas de PREMIÈRE CLASSE, pas un cas limite : plan sans la famille unifiée, 429 de
    /// plafond de dépense, ou famille renommée côté serveur. La bonne réponse est UsageSnapshot.Empty —
    /// jamais « 0 % de quota consommé », qui serait le mensonge inverse de celui que v1.5 corrige.</summary>
    public static IReadOnlyDictionary<string, string> Absents => new Dictionary<string, string>();

    /// <summary>7. ILLISIBLES. Prouve la tolérance : chaque en-tête se lit INDÉPENDAMMENT, une valeur
    /// illisible n'invalide pas les autres, et rien ne devient zéro. L'epoch « 9 » est RÉEL — il vient du
    /// usage.json de cette machine et il est antérieur au plancher de sanité. La virgule décimale est la
    /// forme qu'un serveur n'envoie pas, mais qu'une culture fr-FR produirait si la conversion passait par
    /// la culture courante.</summary>
    public static IReadOnlyDictionary<string, string> Illisibles => new Dictionary<string, string>
    {
        [H5hUtil] = "pas-un-nombre",
        [H5hReset] = "9",
        [H7dUtil] = "0,63",
        [H5hStatut] = "",
    };

    /// <summary>8. NOMINAL EN CASSE MÉLANGÉE. Prouve que la recherche d'en-tête est insensible à la casse —
    /// vérifié, pas supposé. Conséquence directe : AUCUNE normalisation de casse à écrire dans la sonde.</summary>
    public static IReadOnlyDictionary<string, string> NominalCasseMelangee => new Dictionary<string, string>
    {
        ["Anthropic-RateLimit-Unified-5h-Utilization"] = "0.01",
        ["ANTHROPIC-RATELIMIT-UNIFIED-5H-RESET"] = "1783180800",
        ["Anthropic-Ratelimit-Unified-5h-Status"] = "allowed",
        ["anthropic-RateLimit-unified-7d-Utilization"] = "0.63",
    };

    /// <summary>Jeu NOMINAL avec quelques valeurs remplacées. Privé : les jeux publics restent au nombre de
    /// huit, et une variante ne s'ajoute qu'en se nommant.</summary>
    private static IReadOnlyDictionary<string, string> Variante(params (string Nom, string Valeur)[] remplacements)
    {
        var d = new Dictionary<string, string>(Nominal);
        foreach (var (nom, valeur) in remplacements) d[nom] = valeur;
        return d;
    }
}

/// <summary>
/// Test de fumée de la WAVE 0 DE TRANSPORT. Sans les trois faits .NET assertés ici, HDR-02 n'est pas
/// testable du tout — et toute la phase 18 repose sur eux :
/// <list type="number">
///   <item>SendAsync ne LÈVE pas sur un 429 : la réponse arrive intacte, en-têtes compris ;</item>
///   <item>les en-têtes non standard atterrissent dans <c>resp.Headers</c> ;</item>
///   <item>et surtout PAS dans <c>resp.Content.Headers</c> — le mauvais sac est une erreur silencieuse,
///         puisque tout y paraît simplement absent.</item>
/// </list>
/// SÉCURITÉ : aucun appel réseau (tout passe par FakeHttpMessageHandler, qui intercepte avant toute
/// résolution de nom), aucune écriture dans le dossier de données, aucun accès au coffre.
/// </summary>
public class EnTetesDeReferenceTests
{
    private const string UrlFictive = "https://exemple.invalide/v1/messages";

    [Fact]
    public async Task Un_429_porte_ses_en_tetes_et_ils_vivent_sur_la_reponse_pas_sur_le_contenu()
    {
        var client = new HttpClient(
            FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.TooManyRequests, EnTetesDeReference.Nominal));

        // Aucune exception : c'est la condition sine qua non de HDR-02.
        var resp = await client.GetAsync(UrlFictive);

        Assert.Equal(HttpStatusCode.TooManyRequests, resp.StatusCode);
        Assert.True(resp.Headers.TryGetValues(EnTetesDeReference.H5hUtil, out var surLaReponse));
        Assert.Equal("0.01", Assert.Single(surLaReponse!));

        // Le mauvais sac : si la sonde cherchait ici, elle ne verrait JAMAIS rien et paraîtrait muette.
        Assert.False(resp.Content.Headers.TryGetValues(EnTetesDeReference.H5hUtil, out _));
    }

    [Fact]
    public async Task La_recherche_d_en_tete_est_insensible_a_la_casse()
    {
        var client = new HttpClient(
            FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, EnTetesDeReference.NominalCasseMelangee));

        var resp = await client.GetAsync(UrlFictive);

        // Posé en casse mélangée, retrouvé par la constante en minuscules : aucune normalisation à écrire.
        Assert.True(resp.Headers.TryGetValues(EnTetesDeReference.H5hUtil, out var v));
        Assert.Equal("0.01", Assert.Single(v!));
        Assert.True(resp.Headers.TryGetValues(EnTetesDeReference.H5hStatut, out _));
    }

    [Fact]
    public async Task Une_sequence_avance_puis_repete_sa_derniere_etape()
    {
        var handler = FakeHttpMessageHandler.SequenceAvecEnTetes(
            (HttpStatusCode.Unauthorized, EnTetesDeReference.Absents),
            (HttpStatusCode.TooManyRequests, EnTetesDeReference.Refus));
        var client = new HttpClient(handler);

        var un = await client.GetAsync(UrlFictive);
        var deux = await client.GetAsync(UrlFictive);
        var trois = await client.GetAsync(UrlFictive);

        Assert.Equal(HttpStatusCode.Unauthorized, un.StatusCode);
        Assert.Empty(un.Headers);
        Assert.Equal(HttpStatusCode.TooManyRequests, deux.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, trois.StatusCode);   // la dernière étape se répète
        Assert.True(trois.Headers.TryGetValues(EnTetesDeReference.H5hStatut, out _));
        Assert.Equal(3, handler.SendCount);
    }

    [Fact]
    public void Les_huit_jeux_portent_bien_les_formes_annoncees()
    {
        // Nominal : les deux fenêtres renseignées.
        Assert.True(EnTetesDeReference.Nominal.ContainsKey(EnTetesDeReference.H5hUtil));
        Assert.True(EnTetesDeReference.Nominal.ContainsKey(EnTetesDeReference.H7dUtil));

        // Dépassement SEUL : aucune fenêtre, et le statut sur le nom GLOBAL (sans segment de fenêtre).
        Assert.False(EnTetesDeReference.DepassementSeul.ContainsKey(EnTetesDeReference.H5hUtil));
        Assert.False(EnTetesDeReference.DepassementSeul.ContainsKey(EnTetesDeReference.H7dUtil));
        Assert.True(EnTetesDeReference.DepassementSeul.ContainsKey(EnTetesDeReference.HStatutGlobal));

        // Absents : réellement vide (le cas de première classe, pas un cas limite).
        Assert.Empty(EnTetesDeReference.Absents);

        // Une variante ne contamine pas le jeu dont elle dérive.
        Assert.Equal("allowed", EnTetesDeReference.Nominal[EnTetesDeReference.H5hStatut]);
        Assert.Equal("allowed_warning", EnTetesDeReference.Avertissement[EnTetesDeReference.H5hStatut]);
        Assert.Equal("rejected", EnTetesDeReference.Refus[EnTetesDeReference.H5hStatut]);
        Assert.Equal("active", EnTetesDeReference.StatutInconnu[EnTetesDeReference.H5hStatut]);
    }

    [Fact]
    public void Le_jeu_illisible_est_reellement_illisible_pour_le_point_unique_de_normalisation()
    {
        var jeu = EnTetesDeReference.Illisibles;

        // Texte non numérique : inconnu, jamais zéro.
        Assert.Null(UsageNormalization.FractionDepuisTexteFraction(jeu[EnTetesDeReference.H5hUtil]));

        // L'epoch RÉEL de cette machine, antérieur au plancher de sanité : inconnu, pas janvier 1970.
        Assert.Null(UsageNormalization.InstantDepuisTexteEpoch(jeu[EnTetesDeReference.H5hReset]));

        // Virgule décimale : rejetée par la culture invariante, donc inconnue — et surtout pas 63 fois trop.
        Assert.Null(UsageNormalization.FractionDepuisTexteFraction(jeu[EnTetesDeReference.H7dUtil]));

        // En-tête présent mais vide : « rien rapporté », pas « statut inconnu ».
        Assert.Null(Chronos.Models.StatutServeurTexte.DepuisEnTete(jeu[EnTetesDeReference.H5hStatut]));
    }
}
