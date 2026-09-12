using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// LE CŒUR DE PREUVE DE LA PHASE 18. <see cref="RateLimitHeaderUsageProvider"/> est la seule source du
/// projet qui rende des chiffres EXACTS quand l'API refuse : toute la valeur de la phase tient dans le
/// fait qu'un 429 livre quand même ses compteurs, et qu'un 429 MUET n'invente rien du tout.
///
/// TDD STRICT IMPOSSIBLE, ET C'EST DOCUMENTÉ : <c>tests/Chronos.Tests</c> porte un
/// <c>ProjectReference</c> vers <c>Chronos</c>, donc un test référençant un type inexistant empêche
/// TOUTE la solution de compiler et aucun test ne s'exécute — l'étape RED serait un commit où
/// <c>dotnet test</c> n'est même pas invocable. Source et tests sont donc livrés dans un commit unique
/// et compilable (précédent explicite des plans 17-04, 17-05, 18-01 et 18-02), et la falsifiabilité est
/// obtenue par MUTATION mesurée puis révoquée — le décompte est consigné dans le SUMMARY du plan.
///
/// SÉCURITÉ — contrainte qui prime sur tout :
/// <list type="bullet">
///   <item>aucun test n'émet de requête réseau réelle : tout passe par
///         <see cref="FakeHttpMessageHandler"/>, qui intercepte avant toute résolution de nom ;</item>
///   <item>le coffre et le <c>settings.json</c> de test vivent TOUJOURS sous
///         <c>Path.GetTempPath()</c> (gardes <c>Assert.StartsWith</c>) : le jeton de renouvellement réel
///         de l'utilisateur n'est ni lu, ni déchiffré, ni présenté au serveur — la rotation le tuerait
///         DÉFINITIVEMENT ;</item>
///   <item>aucune écriture sous <c>%APPDATA%\Chronos\</c>.</item>
/// </list>
///
/// Pas de [Collection("XAML WPF")] : aucun BAML chargé, la classe reste parallélisable.
/// </summary>
public class RateLimitHeaderUsageProviderTests
{
    // Horloge figée, antérieure aux resets des jeux de référence (fractions de temps positives).
    private static readonly DateTimeOffset Maintenant = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    // Corps de renouvellement nominal du point de terminaison de jeton (faux transport UNIQUEMENT).
    private const string CorpsJetonNeuf =
        """{"access_token":"ACC-2","refresh_token":"REF-2","expires_in":3600}""";

    // SÉCURITÉ : dossier de travail TOUJOURS sous %TEMP%, un par montage de test.
    private static string DossierTemp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosSondeEnTetesTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// POINT UNIQUE de construction de la sonde. Rend aussi l'autorité — pour observer l'ÉTAT
    /// d'authentification, ce qu'un 401 muet ne permettait pas — et le service de réglages, pour pouvoir
    /// basculer l'interrupteur SANS reconstruire la sonde (preuve de la relecture fraîche).
    ///
    /// SÉCURITÉ : coffre ET settings.json sous %TEMP% (gardes Assert.StartsWith), transport entièrement
    /// faux des deux côtés (usage et jeton).
    /// </summary>
    private static (RateLimitHeaderUsageProvider sonde, ChronosTokenAuthority autorite, SettingsService reglages)
        Sonde(DateTimeOffset? expiration, FakeHttpMessageHandler transport, FakeClock horloge,
              bool activee = true, FakeHttpMessageHandler? jeton = null)
    {
        var dir = DossierTemp();
        var cheminCoffre = Path.Combine(dir, "oauth.dat");
        var cheminUsage = Path.Combine(dir, "Chronos", "usage.json");
        Assert.StartsWith(Path.GetTempPath(), cheminCoffre);   // garde anti-accident (motif CompositionRootTests)
        Assert.StartsWith(Path.GetTempPath(), cheminUsage);

        var coffre = new ChronosOAuthStore(cheminCoffre);
        if (expiration is { } e) coffre.Save(new OAuthTokens("ACC-VALIDE", "REF-VALIDE", e));

        var client = new ChronosOAuthClient(
            new HttpClient(jeton ?? FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsJetonNeuf)),
            horloge);
        var autorite = new ChronosTokenAuthority(coffre, client, horloge);

        var reglages = new SettingsService(new ChronosPaths(cheminUsage, Path.Combine(dir, "projects")));
        reglages.Save(new ChronosSettings { SondeEnTetesActivee = activee });

        return (new RateLimitHeaderUsageProvider(autorite, new HttpClient(transport), horloge, reglages),
                autorite, reglages);
    }

    /// <summary>Transport qui capture le CORPS de la requête pendant l'envoi. Nécessaire parce que la
    /// sonde dispose sa requête après l'envoi (motif <c>using var req</c>) : le contenu n'est plus lisible
    /// une fois <c>LastRequest</c> consultable.</summary>
    private static FakeHttpMessageHandler AvecCaptureDuCorps(
        IReadOnlyDictionary<string, string> enTetes, Action<string> surCorps)
        => new(req =>
        {
            surCorps(req.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            foreach (var (k, v) in enTetes) r.Headers.TryAddWithoutValidation(k, v);
            return r;
        });

    // --- Isolation : les gardes qui protègent le coffre et les réglages RÉELS ---

    [Fact]
    public void Le_coffre_et_les_reglages_de_test_vivent_sous_le_dossier_temporaire()
    {
        var dir = DossierTemp();

        Assert.StartsWith(Path.GetTempPath(), dir);
        Assert.Contains("ChronosSondeEnTetesTest_", dir);
        Assert.StartsWith(Path.GetTempPath(), Path.Combine(dir, "Chronos", "settings.json"));
    }

    // --- Le réglage : défaut assumé, et un interrupteur DISTINCT ---

    /// <summary>Défaut TRUE, par cohérence avec OAuthUsageEnabled (« vrais chiffres dès l'installation »).
    /// Champ DISTINCT à dessein : mélanger les deux empêcherait l'utilisateur de couper la seule source
    /// qui dépense réellement du quota.</summary>
    [Fact]
    public void Le_reglage_de_sonde_est_actif_par_defaut_et_distinct_de_celui_d_OAuth()
    {
        var defauts = new ChronosSettings();

        Assert.True(defauts.SondeEnTetesActivee);
        Assert.True(defauts.OAuthUsageEnabled);
        // Couper la sonde ne coupe PAS la source OAuth, et réciproquement : deux interrupteurs, deux
        // profils de coût (la sonde dépense une micro-requête, l'autre source ne dépense rien).
        Assert.True(new ChronosSettings { SondeEnTetesActivee = false }.OAuthUsageEnabled);
        Assert.True(new ChronosSettings { OAuthUsageEnabled = false }.SondeEnTetesActivee);
    }

    // --- Inertie : avant tout appel, et quand il ne faut rien envoyer ---

    [Fact]
    public void Avant_tout_appel_la_sonde_ne_pretend_rien()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, EnTetesDeReference.Nominal);
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, new FakeClock(Maintenant));

        Assert.Equal(ResultatSonde.JamaisSondee, sonde.DernierResultat);
        Assert.Empty(sonde.NomsEnTetesRecus);
        Assert.Equal(0, transport.SendCount);
    }

    /// <summary>Interrupteur à false : ZÉRO envoi HTTP, et le jeton n'est même pas DEMANDÉ — l'autorité
    /// reste à NonConnecte alors qu'un coffre parfaitement valide est présent. C'est la preuve que le
    /// court-circuit est bien AVANT l'accès au jeton (motif GatedOAuthUsageProvider).</summary>
    [Fact]
    public async Task L_interrupteur_a_false_rend_la_sonde_totalement_inerte()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, EnTetesDeReference.Nominal);
        var (sonde, autorite, _) = Sonde(Maintenant.AddHours(2), transport, new FakeClock(Maintenant),
                                        activee: false);

        var snap = await sonde.GetAsync();

        Assert.Equal(0, transport.SendCount);
        Assert.Equal(ResultatSonde.Desactivee, sonde.DernierResultat);
        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
        Assert.Equal(EtatAuthentification.NonConnecte, autorite.Etat);   // le jeton n'a PAS été demandé
    }

    /// <summary>Coffre vide : l'utilisateur ne s'est jamais connecté. Ce n'est PAS une panne, et cela ne
    /// doit allumer aucune pastille de déconnexion.</summary>
    [Fact]
    public async Task Un_coffre_vide_n_est_pas_une_panne()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, EnTetesDeReference.Nominal);
        var (sonde, autorite, _) = Sonde(null, transport, new FakeClock(Maintenant));

        var snap = await sonde.GetAsync();

        Assert.Equal(0, transport.SendCount);
        Assert.Equal(ResultatSonde.PasDeJeton, sonde.DernierResultat);
        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.NotEqual(EtatAuthentification.Deconnecte, autorite.Etat);
    }

    // --- Le frein de cadence (HDR-06), y compris sans cache ---

    [Fact]
    public async Task Le_frein_borne_la_cadence_et_s_ouvre_apres_la_cadence_nominale()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, EnTetesDeReference.Nominal);
        var horloge = new FakeClock(Maintenant);
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, horloge);

        await sonde.GetAsync();
        Assert.Equal(1, transport.SendCount);

        await sonde.GetAsync();                                   // même instant : refus LOCAL
        Assert.Equal(1, transport.SendCount);
        Assert.Equal(ResultatSonde.FreinActif, sonde.DernierResultat);

        horloge.UtcNow = Maintenant + RateLimitHeaderUsageProvider.CadenceNominale;
        await sonde.GetAsync();
        Assert.Equal(2, transport.SendCount);
        Assert.Equal(ResultatSonde.SuccesEnTetesLus, sonde.DernierResultat);
    }

    /// <summary>LEÇON DE LA PHASE 17 (défaut n° 3 du plan 17-01) : le garde-fou d'origine exigeait
    /// « un cache existe » EN PLUS du recul, or le cache vit en RAM donc est vide à chaque démarrage de
    /// l'exe — le frein disparaissait précisément quand le martèlement se produit. Ici le premier appel
    /// échoue en réseau, donc AUCUN cache n'existe, et le frein tient quand même.</summary>
    [Fact]
    public async Task Le_frein_tient_meme_quand_aucun_cache_n_existe()
    {
        var transport = FakeHttpMessageHandler.Throws(new HttpRequestException("réseau"));
        var horloge = new FakeClock(Maintenant);
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, horloge);

        await sonde.GetAsync();
        Assert.Equal(1, transport.SendCount);
        Assert.Equal(ResultatSonde.PanneReseau, sonde.DernierResultat);

        await sonde.GetAsync();
        Assert.Equal(1, transport.SendCount);                     // aucun cache, et pourtant pas d'envoi
        Assert.Equal(ResultatSonde.FreinActif, sonde.DernierResultat);
    }

    // --- La requête émise : méthode, URL, les QUATRE en-têtes, et le corps exact ---

    [Fact]
    public async Task La_requete_est_un_POST_sur_l_endpoint_de_messages_avec_les_quatre_en_tetes()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, EnTetesDeReference.Nominal);
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, new FakeClock(Maintenant));

        await sonde.GetAsync();

        var req = transport.LastRequest!;
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://api.anthropic.com/v1/messages", req.RequestUri!.ToString());
        Assert.Equal("Bearer ACC-VALIDE", Assert.Single(req.Headers.GetValues("Authorization")));
        // PROUVÉ obligatoire : sans cet en-tête, le jeton porteur est lu comme une clé d'API et le 401
        // renvoie « invalid x-api-key » — un message qui ne parle même pas d'OAuth.
        Assert.Equal("oauth-2025-04-20", Assert.Single(req.Headers.GetValues("anthropic-beta")));
        Assert.Equal("2023-06-01", Assert.Single(req.Headers.GetValues("anthropic-version")));
        Assert.Equal("claude-code/2.1.30", Assert.Single(req.Headers.GetValues("User-Agent")));
    }

    /// <summary>Corps MINIMAL : aucun prompt système, aucun outil, aucun réglage d'échantillonnage, aucune
    /// métadonnée. Un prompt système volumineux ferait basculer la requête sur la voie « crédits d'API » et
    /// rendrait un 429 de plafond de DÉPENSE, qui ne parle pas du quota d'abonnement.</summary>
    [Fact]
    public async Task Le_corps_de_la_sonde_est_minimal_et_exact()
    {
        string? corps = null;
        var transport = AvecCaptureDuCorps(EnTetesDeReference.Nominal, c => corps = c);
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, new FakeClock(Maintenant));

        await sonde.GetAsync();

        Assert.Equal(
            """{"model":"claude-haiku-4-5","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}""",
            corps);
        foreach (var interdit in new[] { "system", "tools", "temperature", "top_p", "top_k", "metadata", "effort" })
            Assert.DoesNotContain(interdit, corps!);
    }

    // --- HDR-01/HDR-02 : les en-têtes, lus AVANT toute décision sur le code de statut ---

    /// <summary>Jeu NOMINAL avec quelques valeurs remplacées. Local au fichier : les huit jeux publics de
    /// <see cref="EnTetesDeReference"/> restent au nombre de huit, et une variante de test ne s'ajoute
    /// jamais au point unique sans se nommer.</summary>
    private static IReadOnlyDictionary<string, string> Variante(
        params (string Nom, string Valeur)[] remplacements)
    {
        var d = new Dictionary<string, string>(EnTetesDeReference.Nominal);
        foreach (var (nom, valeur) in remplacements) d[nom] = valeur;
        return d;
    }

    /// <summary>HDR-01. Les fractions arrivent DÉJÀ en 0..1 dans les en-têtes et ne sont donc PAS divisées —
    /// c'est le piège HDR-05 sous sa troisième forme : la même donnée circule en 0..1 (en-têtes), en 0..100
    /// (/api/oauth/usage) et en 0..100 (pont statusLine). Diviser ici rendrait « 0,01 % » au lieu de 1 %.</summary>
    [Fact]
    public async Task Un_200_porteur_d_en_tetes_rend_deux_fenetres_exactes()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, EnTetesDeReference.Nominal);
        var horloge = new FakeClock(Maintenant);
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, horloge);

        var snap = await sonde.GetAsync();

        Assert.Equal(0.01, snap.FiveHour.Utilization!.Value, 9);
        Assert.Equal(0.63, snap.SevenDay.Utilization!.Value, 9);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1783180800), snap.FiveHour.ResetsAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1783713600), snap.SevenDay.ResetsAt);
        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Exact, snap.SevenDay.Reliability);

        // CapturedAt PAR FENÊTRE : prérequis de la doctrine de la phase 19 (limite d'âge, delta).
        Assert.Equal(horloge.UtcNow, snap.FiveHour.CapturedAt);
        Assert.Equal(horloge.UtcNow, snap.SevenDay.CapturedAt);
        Assert.Equal(horloge.UtcNow, snap.SourceCapturedAt);

        Assert.Equal(ResultatSonde.SuccesEnTetesLus, sonde.DernierResultat);
        Assert.Contains(EnTetesDeReference.H5hUtil, sonde.NomsEnTetesRecus);
        Assert.Contains(EnTetesDeReference.H5hReset, sonde.NomsEnTetesRecus);
        Assert.Contains(EnTetesDeReference.H7dUtil, sonde.NomsEnTetesRecus);
        Assert.Contains(EnTetesDeReference.H7dReset, sonde.NomsEnTetesRecus);
        Assert.Contains(EnTetesDeReference.HClaim, sonde.NomsEnTetesRecus);
    }

    /// <summary>
    /// HDR-02 — LE TEST CENTRAL DE LA PHASE. C'est l'avantage STRUCTUREL sur <c>/api/oauth/usage</c>, qui ne
    /// rend rien d'utile en erreur (son 401 est rendu en bordure, sans aucun en-tête de limite) : ici, un
    /// refus livre quand même des chiffres exacts et FRAIS. C'est aussi la preuve que le contrôle du succès
    /// n'est pas le chemin de contrôle — les en-têtes sont lus avant tout aiguillage par code de statut.
    ///
    /// CONTRAINTE D'HONNÊTETÉ : ce test prouve que LE CODE exploite un 429 porteur d'en-têtes. Il ne prouve
    /// PAS qu'un 429 RÉEL d'Anthropic porte la famille unifiée — cela repose sur trois sources concordantes
    /// mais AUCUNE officielle, et n'est pas vérifiable sans un jeton valide (celui de cette machine est
    /// expiré depuis le 2026-07-12) ET un compte réellement saturé. La vérification manuelle est la tâche 3
    /// du plan 18-06. HDR-02 ne doit pas être coché « prouvé en production » avant.
    /// </summary>
    [Fact]
    public async Task Un_429_livre_quand_meme_les_chiffres()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(
            HttpStatusCode.TooManyRequests, EnTetesDeReference.Refus);
        var horloge = new FakeClock(Maintenant);
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, horloge);

        var snap = await sonde.GetAsync();

        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(1.0, snap.FiveHour.Utilization!.Value, 9);
        Assert.True(snap.FiveHour.Exhausted);
        Assert.Equal(horloge.UtcNow, snap.FiveHour.CapturedAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1783180800), snap.FiveHour.ResetsAt);
        Assert.Equal(0.63, snap.SevenDay.Utilization!.Value, 9);
        Assert.Equal(ResultatSonde.SaturationEnTetesLus, sonde.DernierResultat);
    }

    /// <summary>Un 429 PROUVE que le jeton est valide : il déverrouille donc l'état au lieu de le dégrader.
    /// Sans cela, la pastille de déconnexion MENTIRAIT pendant toute la saturation — exactement quand
    /// l'utilisateur a le plus besoin de croire son overlay.</summary>
    [Fact]
    public async Task Un_429_ne_declare_jamais_de_deconnexion()
    {
        var porteur = FakeHttpMessageHandler.AvecEnTetes(
            HttpStatusCode.TooManyRequests, EnTetesDeReference.Refus);
        var (sonde, autorite, _) = Sonde(Maintenant.AddHours(2), porteur, new FakeClock(Maintenant));

        await sonde.GetAsync();

        Assert.NotEqual(EtatAuthentification.Deconnecte, autorite.Etat);
        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);

        // Et un 429 MUET ne dégrade pas davantage : il n'apprend rien, il ne déclare rien.
        var muet = FakeHttpMessageHandler.AvecEnTetes(
            HttpStatusCode.TooManyRequests, EnTetesDeReference.Absents);
        var (sonde2, autorite2, _) = Sonde(Maintenant.AddHours(2), muet, new FakeClock(Maintenant));

        await sonde2.GetAsync();

        Assert.NotEqual(EtatAuthentification.Deconnecte, autorite2.Etat);
        Assert.Equal(ResultatSonde.SaturationSansEnTetes, sonde2.DernierResultat);
    }

    /// <summary>Cas de PREMIÈRE CLASSE, pas un cas limite : un 429 de plafond de DÉPENSE, ou un plan sans la
    /// famille unifiée, ne porte aucun compteur. La bonne réponse est « rien » — ni 0 %, ni 100 %.</summary>
    [Fact]
    public async Task Un_429_muet_n_invente_rien()
    {
        var avecRetryAfter = new Dictionary<string, string>(EnTetesDeReference.Absents)
        {
            ["Retry-After"] = "300",
        };
        var transport = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.TooManyRequests, avecRetryAfter);
        var horloge = new FakeClock(Maintenant);
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, horloge);

        var snap = await sonde.GetAsync();

        Assert.Null(snap.FiveHour.Utilization);
        Assert.Null(snap.SevenDay.Utilization);
        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
        Assert.False(snap.FiveHour.Exhausted);                 // « inconnu » n'est pas « épuisé »
        Assert.Equal(ResultatSonde.SaturationSansEnTetes, sonde.DernierResultat);

        // Le recul HONORE la valeur du serveur : 300 s, pas le plancher de 60 s.
        horloge.UtcNow = Maintenant.AddSeconds(299);
        await sonde.GetAsync();
        Assert.Equal(1, transport.SendCount);
        horloge.UtcNow = Maintenant.AddSeconds(301);
        await sonde.GetAsync();
        Assert.Equal(2, transport.SendCount);

        // SANS valeur du serveur, le recul vaut le PLANCHER de 60 s : une sonde rejetée ne consomme pas de
        // quota, on peut donc rester réactif pendant la saturation.
        var sansRetryAfter = FakeHttpMessageHandler.AvecEnTetes(
            HttpStatusCode.TooManyRequests, EnTetesDeReference.Absents);
        var horloge2 = new FakeClock(Maintenant);
        var (sonde2, _, _) = Sonde(Maintenant.AddHours(2), sansRetryAfter, horloge2);

        await sonde2.GetAsync();
        horloge2.UtcNow = Maintenant.AddSeconds(59);
        await sonde2.GetAsync();
        Assert.Equal(1, sansRetryAfter.SendCount);
        horloge2.UtcNow = Maintenant.AddSeconds(61);
        await sonde2.GetAsync();
        Assert.Equal(2, sansRetryAfter.SendCount);
    }

    /// <summary>Un 2xx sans la famille unifiée est le SIGNAL que les noms d'en-têtes ont été renommés côté
    /// serveur, ou que ce plan n'en a pas — la famille n'est documentée NULLE PART chez Anthropic. À dire,
    /// jamais à confondre avec « pas de données », et jamais à combler par un zéro.</summary>
    [Fact]
    public async Task Un_200_sans_en_tete_unified_ne_produit_rien()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, EnTetesDeReference.Absents);
        var (sonde, autorite, _) = Sonde(Maintenant.AddHours(2), transport, new FakeClock(Maintenant));

        var snap = await sonde.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
        Assert.Equal(ResultatSonde.SuccesSansEnTetes, sonde.DernierResultat);
        Assert.Empty(sonde.NomsEnTetesRecus);
        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);   // le 2xx déverrouille quand même
    }

    /// <summary>Rejeu UNIQUE sur 401 : le serveur peut révoquer un jeton AVANT son expiration locale, donc le
    /// renouvellement préventif ne suffit pas. Mais un refus PERSISTANT se DIT (plus jamais de 401 muet), et
    /// une seconde tentative suffit — sans garde, on boucle jusqu'à déclencher une limitation sur le point de
    /// terminaison de jeton.</summary>
    [Fact]
    public async Task Un_401_degrade_et_dit_le_refus()
    {
        var transport = FakeHttpMessageHandler.SequenceAvecEnTetes(
            (HttpStatusCode.Unauthorized, EnTetesDeReference.Absents),
            (HttpStatusCode.Unauthorized, EnTetesDeReference.Absents));
        var (sonde, autorite, _) = Sonde(Maintenant.AddHours(2), transport, new FakeClock(Maintenant));

        var snap = await sonde.GetAsync();

        Assert.Equal(2, transport.SendCount);                  // rejeu UNIQUE
        Assert.NotEqual(3, transport.SendCount);
        Assert.Equal(EtatAuthentification.Deconnecte, autorite.Etat);
        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(ResultatSonde.RefusServeur, sonde.DernierResultat);

        // Un 401 ne porte AUCUN en-tête de limite (confirmé sur 6 sondes réelles) : rien à espérer.
        Assert.Empty(sonde.NomsEnTetesRecus);
    }

    /// <summary>403 : portée insuffisante ou compte refusé. Aucun rejeu — renouveler un jeton ne répare pas
    /// une portée.</summary>
    [Fact]
    public async Task Un_403_degrade_sans_rejeu()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(
            HttpStatusCode.Forbidden, EnTetesDeReference.Absents);
        var (sonde, autorite, _) = Sonde(Maintenant.AddHours(2), transport, new FakeClock(Maintenant));

        await sonde.GetAsync();

        Assert.Equal(1, transport.SendCount);
        Assert.Equal(EtatAuthentification.Deconnecte, autorite.Etat);
        Assert.Equal(ResultatSonde.RefusServeur, sonde.DernierResultat);
    }

    /// <summary>Un identifiant de modèle périmé est une panne de CONFIGURATION nommée, reculée d'une heure,
    /// jamais confondue avec « pas de données » ni avec une déconnexion. L'authentification étant évaluée
    /// AVANT le corps (corps malformé sans jeton -> 401, pas 400), un modèle périmé ne peut pas se déguiser
    /// en problème d'authentification.</summary>
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Un_modele_refuse_est_une_panne_de_configuration(HttpStatusCode statut)
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(statut, EnTetesDeReference.Absents);
        var horloge = new FakeClock(Maintenant);
        var (sonde, autorite, _) = Sonde(Maintenant.AddHours(2), transport, horloge);

        var snap = await sonde.GetAsync();

        Assert.Equal(ResultatSonde.ModeleRefuse, sonde.DernierResultat);
        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.NotEqual(EtatAuthentification.Deconnecte, autorite.Etat);

        // Recul LONG : réessayer toutes les 5 minutes un modèle qui n'existe plus ne répare rien et consomme.
        horloge.UtcNow = Maintenant.AddMinutes(59);
        await sonde.GetAsync();
        Assert.Equal(1, transport.SendCount);
        horloge.UtcNow = Maintenant.AddMinutes(61);
        await sonde.GetAsync();
        Assert.Equal(2, transport.SendCount);
    }

    /// <summary>Réseau ou délai dépassé : surtout PAS de « déconnecté » — c'est peut-être seulement le wifi.
    /// Recul exponentiel PLAFONNÉ (motif ChronosTokenAuthority) : hors ligne, un tick de 60 s tenterait
    /// 1 440 requêtes par jour et transformerait une panne bénigne en limitation.</summary>
    [Fact]
    public async Task Une_panne_reseau_recule_exponentiellement_et_ne_declare_pas_de_deconnexion()
    {
        var transport = FakeHttpMessageHandler.Throws(new HttpRequestException("réseau"));
        var horloge = new FakeClock(Maintenant);
        var (sonde, autorite, _) = Sonde(Maintenant.AddDays(30), transport, horloge);

        var snap = await sonde.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(ResultatSonde.PanneReseau, sonde.DernierResultat);
        Assert.NotEqual(EtatAuthentification.Deconnecte, autorite.Etat);
        Assert.Equal(1, transport.SendCount);

        // 1er recul = 60 s : à 61 s l'appel passe, échoue, et le recul DOUBLE à 120 s.
        horloge.UtcNow = Maintenant.AddSeconds(61);
        await sonde.GetAsync();
        Assert.Equal(2, transport.SendCount);

        // Preuve du doublement : 61 s plus tard l'appel est REFUSÉ (il serait passé si le recul valait
        // encore 60 s), et 121 s plus tard il passe.
        horloge.UtcNow = Maintenant.AddSeconds(61 + 61);
        await sonde.GetAsync();
        Assert.Equal(2, transport.SendCount);
        Assert.Equal(ResultatSonde.FreinActif, sonde.DernierResultat);

        var t = Maintenant.AddSeconds(61 + 121);
        horloge.UtcNow = t;
        await sonde.GetAsync();
        Assert.Equal(3, transport.SendCount);                  // recul -> 240 s

        // Saturation du recul : 480 s, puis 960 s écrêté au PLAFOND de 15 min, puis 15 min à jamais.
        for (var i = 0; i < 5; i++)
        {
            t = t.AddMinutes(20);
            horloge.UtcNow = t;
            await sonde.GetAsync();
        }
        Assert.Equal(8, transport.SendCount);

        horloge.UtcNow = t.AddSeconds(899);
        await sonde.GetAsync();
        Assert.Equal(8, transport.SendCount);                  // plafonné, mais bien à 15 min
        horloge.UtcNow = t.AddSeconds(901);
        await sonde.GetAsync();
        Assert.Equal(9, transport.SendCount);

        // REMISE À ZÉRO par un succès : un échec suivant recule de nouveau de 60 s, pas de 15 min — sans
        // quoi une panne passagère condamnerait l'overlay au silence pour le reste de la session.
        var n = 0;
        var mixte = new FakeHttpMessageHandler(_ =>
        {
            if (n++ != 3) throw new HttpRequestException("réseau");
            var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            foreach (var (k, v) in EnTetesDeReference.Nominal) r.Headers.TryAddWithoutValidation(k, v);
            return r;
        });
        var h2 = new FakeClock(Maintenant);
        var (sonde2, _, _) = Sonde(Maintenant.AddDays(30), mixte, h2);

        await sonde2.GetAsync();                               // échec 1 -> recul 60 s
        h2.UtcNow = Maintenant.AddSeconds(61);
        await sonde2.GetAsync();                               // échec 2 -> recul 120 s
        h2.UtcNow = Maintenant.AddSeconds(182);
        await sonde2.GetAsync();                               // échec 3 -> recul 240 s
        h2.UtcNow = Maintenant.AddSeconds(423);
        await sonde2.GetAsync();                               // SUCCÈS -> recul remis à zéro
        Assert.Equal(ResultatSonde.SuccesEnTetesLus, sonde2.DernierResultat);

        h2.UtcNow = Maintenant.AddSeconds(423 + 300);
        await sonde2.GetAsync();                               // échec 4 -> recul 60 s, et non 480 s
        Assert.Equal(5, mixte.SendCount);
        h2.UtcNow = Maintenant.AddSeconds(423 + 300 + 59);
        await sonde2.GetAsync();
        Assert.Equal(5, mixte.SendCount);
        h2.UtcNow = Maintenant.AddSeconds(423 + 300 + 61);
        await sonde2.GetAsync();
        Assert.Equal(6, mixte.SendCount);                      // 60 s ont suffi : le recul est bien reparti
    }

    /// <summary>Le code d'origine rend ZÉRO dans sa branche d'erreur. Transposé tel quel, un en-tête absent
    /// ou illisible afficherait « 0 % de quota consommé » — le mensonge exactement INVERSE de celui que v1.5
    /// corrige. Ici : trois valeurs illisibles, trois inconnus, aucun zéro.</summary>
    [Fact]
    public async Task Des_en_tetes_illisibles_ne_produisent_jamais_de_zero()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, EnTetesDeReference.Illisibles);
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, new FakeClock(Maintenant));

        var snap = await sonde.GetAsync();

        Assert.Null(snap.FiveHour.Utilization);        // « pas-un-nombre »
        Assert.Null(snap.FiveHour.ResetsAt);           // epoch 9 : sous le plancher de sanité 2020-01-01
        Assert.Null(snap.SevenDay.Utilization);        // « 0,63 » : les en-têtes HTTP s'écrivent à POINT
        Assert.Null(snap.SevenDay.ResetsAt);
        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
        Assert.False(snap.FiveHour.Exhausted);
        Assert.Equal(ResultatSonde.SuccesSansEnTetes, sonde.DernierResultat);
    }

    /// <summary>
    /// Lecture TOLÉRANTE, en-tête par en-tête : une valeur illisible n'invalide ni l'autre fenêtre, ni même
    /// l'autre moitié de la sienne.
    ///
    /// ÉCART ASSUMÉ AVEC LA LETTRE DU PLAN 18-03 (tâche 2, test n° 11), documenté dans le SUMMARY : le plan
    /// attendait une 5 h <c>Unavailable</c> quand seule son <c>utilization</c> est illisible. Or son
    /// <c>reset</c> reste parfaitement lisible, et la règle de construction énoncée par la tâche 1 du MÊME
    /// plan ne rend <c>Unavailable</c> que si les DEUX valeurs sont inconnues. Jeter un instant de reset
    /// réellement obtenu serait une perte d'information — exactement le défaut que la phase corrige. Le cas
    /// « fenêtre vraiment inconnue » est couvert par le second volet, où les deux en-têtes sont illisibles.
    /// </summary>
    [Fact]
    public async Task Une_fenetre_illisible_n_invalide_pas_l_autre()
    {
        // Volet 1 : seule l'utilisation de la 5 h est illisible. Son reset survit, donc la fenêtre reste
        // exacte avec une utilisation INCONNUE (null), jamais un zéro.
        var partiel = FakeHttpMessageHandler.AvecEnTetes(
            HttpStatusCode.OK, Variante((EnTetesDeReference.H5hUtil, "xxx")));
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), partiel, new FakeClock(Maintenant));

        var snap = await sonde.GetAsync();

        Assert.Null(snap.FiveHour.Utilization);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1783180800), snap.FiveHour.ResetsAt);
        Assert.Equal(0.63, snap.SevenDay.Utilization!.Value, 9);
        Assert.Equal(SourceReliability.Exact, snap.SevenDay.Reliability);
        Assert.Equal(ResultatSonde.SuccesEnTetesLus, sonde.DernierResultat);

        // Volet 2 : les DEUX en-têtes de la 5 h sont illisibles -> fenêtre réellement inconnue, et la 7 j
        // reste exacte. Le retour n'est PAS Empty.
        var total = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, Variante(
            (EnTetesDeReference.H5hUtil, "xxx"), (EnTetesDeReference.H5hReset, "9")));
        var (sonde2, _, _) = Sonde(Maintenant.AddHours(2), total, new FakeClock(Maintenant));

        var snap2 = await sonde2.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap2.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Exact, snap2.SevenDay.Reliability);
        Assert.Equal(0.63, snap2.SevenDay.Utilization!.Value, 9);
        Assert.Equal(ResultatSonde.SuccesEnTetesLus, sonde2.DernierResultat);
    }

    /// <summary>AUCUNE normalisation de casse n'est écrite dans la sonde : c'est la collection d'en-têtes de
    /// .NET qui s'en charge (vérifié empiriquement, plan 18-02). Le jeu en casse mélangée ne porte pas le
    /// reset hebdomadaire ni la revendication représentative — seuls les noms qu'il porte sont donc
    /// attendus.</summary>
    [Fact]
    public async Task La_casse_des_noms_d_en_tete_est_indifferente()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(
            HttpStatusCode.OK, EnTetesDeReference.NominalCasseMelangee);
        var horloge = new FakeClock(Maintenant);
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, horloge);

        var snap = await sonde.GetAsync();

        Assert.Equal(0.01, snap.FiveHour.Utilization!.Value, 9);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1783180800), snap.FiveHour.ResetsAt);
        Assert.Equal(0.63, snap.SevenDay.Utilization!.Value, 9);
        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Exact, snap.SevenDay.Reliability);
        Assert.Equal(horloge.UtcNow, snap.FiveHour.CapturedAt);
        Assert.Equal(ResultatSonde.SuccesEnTetesLus, sonde.DernierResultat);
        Assert.Contains(EnTetesDeReference.H5hUtil, sonde.NomsEnTetesRecus);
        Assert.Contains(EnTetesDeReference.H7dUtil, sonde.NomsEnTetesRecus);
    }

    /// <summary>PREUVE DE BOUT EN BOUT du piège de culture, au niveau de la SONDE et plus seulement du point
    /// de normalisation : la lecture « naturelle » de « 0.63 » rend false sous fr-FR, et
    /// <c>InvariantGlobalization=false</c> est verrouillé par CLAUDE.md — la machine cible est donc
    /// RÉELLEMENT en fr-FR. Une conversion locale rendrait la source silencieusement muette.</summary>
    [Fact]
    public async Task Un_en_tete_se_lit_meme_sous_une_culture_a_virgule()
    {
        var precedente = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            Assert.False(double.TryParse("0.63", out _));       // le piège lui-même, prouvé sur place

            var transport = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, EnTetesDeReference.Nominal);
            var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, new FakeClock(Maintenant));

            var snap = await sonde.GetAsync();

            Assert.Equal(0.01, snap.FiveHour.Utilization!.Value, 9);
            Assert.Equal(0.63, snap.SevenDay.Utilization!.Value, 9);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1783180800), snap.FiveHour.ResetsAt);
            Assert.Equal(ResultatSonde.SuccesEnTetesLus, sonde.DernierResultat);
        }
        finally { CultureInfo.CurrentCulture = precedente; }
    }

    // --- HDR-06 : cadence bornée, coût maîtrisé, aucun couplage au tick de 60 s ---

    /// <summary>HDR-06. Le composite appelle les deux <c>GetAsync</c> SANS court-circuit : la sonde est donc
    /// sollicitée à chaque tick de <c>RefreshOrchestrator</c> (<c>RefreshOptions.PeriodicInterval</c> = 60 s)
    /// et REFUSE elle-même 4 fois sur 5. Aucun couplage à <c>RefreshOptions</c>, aucun service de fond
    /// supplémentaire : le frein vit dans la sonde, là où il ne peut pas être oublié par un appelant.</summary>
    [Fact]
    public async Task La_cadence_est_bornee_a_une_sonde_par_cadence_nominale()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, EnTetesDeReference.Nominal);
        var horloge = new FakeClock(Maintenant);
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, horloge);

        for (var i = 0; i < 5; i++) await sonde.GetAsync();
        Assert.Equal(1, transport.SendCount);

        // Le tick RÉEL de l'orchestrateur est de 60 s : seuls les pas cumulant >= 300 s déclenchent un envoi.
        foreach (var secondes in new[] { 60, 120, 180, 240 })
        {
            horloge.UtcNow = Maintenant.AddSeconds(secondes);
            await sonde.GetAsync();
            Assert.Equal(1, transport.SendCount);
            Assert.Equal(ResultatSonde.FreinActif, sonde.DernierResultat);
        }

        horloge.UtcNow = Maintenant.AddSeconds(300);
        await sonde.GetAsync();
        Assert.Equal(2, transport.SendCount);
        Assert.Equal(ResultatSonde.SuccesEnTetesLus, sonde.DernierResultat);
    }

    /// <summary>LEÇON DE LA PHASE 17, défaut n° 3 du plan 17-01 : le garde-fou d'origine exigeait « un cache
    /// existe » EN PLUS du recul, or le cache vit en RAM et est donc vide à CHAQUE démarrage de l'exe — le
    /// frein disparaissait précisément quand le martèlement se produit. Ici un 401 ne produit aucun cache, et
    /// le frein tient quand même.</summary>
    [Fact]
    public async Task Le_frein_tient_meme_sans_cache()
    {
        var transport = FakeHttpMessageHandler.SequenceAvecEnTetes(
            (HttpStatusCode.Unauthorized, EnTetesDeReference.Absents),
            (HttpStatusCode.Unauthorized, EnTetesDeReference.Absents));
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, new FakeClock(Maintenant));

        await sonde.GetAsync();
        Assert.Equal(2, transport.SendCount);                  // l'envoi initial + le rejeu unique
        Assert.Equal(ResultatSonde.RefusServeur, sonde.DernierResultat);

        var snap = await sonde.GetAsync();

        Assert.Equal(2, transport.SendCount);                  // AUCUN cache, et pourtant aucun envoi
        Assert.Equal(ResultatSonde.FreinActif, sonde.DernierResultat);
        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
    }

    /// <summary>Le cache de la sonde est volontairement PLUS COURT que les 15 min du provider
    /// <c>/api/oauth/usage</c> : à fiabilité égale, <c>CompositeUsageProvider.Best()</c> privilégie le
    /// <c>primary</c>, donc un cache long de la sonde battrait une lecture FRAÎCHE de l'autre source. Le
    /// classement par fraîcheur est la phase 19 ; en attendant, on n'aggrave pas le problème.</summary>
    [Fact]
    public async Task Le_cache_de_la_sonde_ne_survit_pas_a_la_cadence()
    {
        var n = 0;
        var transport = new FakeHttpMessageHandler(_ =>
        {
            if (n++ > 0) throw new HttpRequestException("réseau");
            var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            foreach (var (k, v) in EnTetesDeReference.Nominal) r.Headers.TryAddWithoutValidation(k, v);
            return r;
        });
        var horloge = new FakeClock(Maintenant);
        var (sonde, _, _) = Sonde(Maintenant.AddHours(2), transport, horloge);

        await sonde.GetAsync();

        // À 299 s le frein est actif ET le cache est encore utilisable : les chiffres du premier appel sont
        // servis tels quels, avec leur CapturedAt d'origine — la fraîcheur n'est jamais maquillée.
        horloge.UtcNow = Maintenant.AddSeconds(299);
        var cache = await sonde.GetAsync();
        Assert.Equal(1, transport.SendCount);
        Assert.Equal(ResultatSonde.FreinActif, sonde.DernierResultat);
        Assert.Equal(0.01, cache.FiveHour.Utilization!.Value, 9);
        Assert.Equal(Maintenant, cache.FiveHour.CapturedAt);

        // À 301 s le cache a dépassé sa durée d'utilisation : une panne réseau ne peut plus le ressortir.
        horloge.UtcNow = Maintenant.AddSeconds(301);
        var vide = await sonde.GetAsync();
        Assert.Equal(2, transport.SendCount);
        Assert.Equal(ResultatSonde.PanneReseau, sonde.DernierResultat);
        Assert.Null(vide.FiveHour.Utilization);
        Assert.Equal(SourceReliability.Unavailable, vide.FiveHour.Reliability);
    }

    /// <summary>L'interrupteur coupe l'accès RÉSEAU et l'accès au JETON, et sa relecture est FRAÎCHE à chaque
    /// appel (motif <c>GatedOAuthUsageProvider</c>) : basculer le réglage prend effet au prochain passage,
    /// sans redémarrer l'exe et sans reconstruire la sonde.</summary>
    [Fact]
    public async Task L_interrupteur_coupe_tout_acces_reseau_ET_tout_acces_au_jeton()
    {
        var transport = FakeHttpMessageHandler.AvecEnTetes(HttpStatusCode.OK, EnTetesDeReference.Nominal);
        var (sonde, autorite, reglages) = Sonde(Maintenant.AddHours(2), transport, new FakeClock(Maintenant),
                                                activee: false);

        await sonde.GetAsync();

        Assert.Equal(0, transport.SendCount);
        Assert.Equal(ResultatSonde.Desactivee, sonde.DernierResultat);
        // Le coffre est VALIDE : si le jeton avait été demandé, l'autorité aurait publié Connecte.
        Assert.Equal(EtatAuthentification.NonConnecte, autorite.Etat);

        reglages.Save(reglages.Load() with { SondeEnTetesActivee = true });   // SANS reconstruire la sonde

        var snap = await sonde.GetAsync();

        Assert.Equal(1, transport.SendCount);
        Assert.Equal(ResultatSonde.SuccesEnTetesLus, sonde.DernierResultat);
        Assert.Equal(0.01, snap.FiveHour.Utilization!.Value, 9);
        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);
    }

    /// <summary>Test de DOCUMENTATION EXÉCUTABLE : 288 sondes par jour est le chiffre qui sera écrit dans les
    /// réglages au plan 18-06. S'ils divergent, ce test tombe.
    ///
    /// EFFET D'OBSERVATION, à dire et jamais à « compenser » : les chiffres rendus par les en-têtes INCLUENT
    /// la sonde elle-même. C'est exact et honnête ; retrancher une estimation de sa propre consommation
    /// serait inventer, donc exactement ce que v1.5 interdit.</summary>
    [Fact]
    public void Le_cout_annonce_correspond_a_la_cadence()
    {
        Assert.Equal(TimeSpan.FromSeconds(300), RateLimitHeaderUsageProvider.CadenceNominale);
        Assert.Equal(288, (int)(TimeSpan.FromDays(1) / RateLimitHeaderUsageProvider.CadenceNominale));
    }

    /// <summary>GARDE STRUCTURELLE PERMANENTE sur la sonde. Trois interdits, chacun pour une raison :
    /// aucun type WPF (la couche Services reste neutre, motif <c>ServicesLayerPurityTests</c>) ; aucune
    /// horloge système (sans quoi aucun test de cadence ne serait déterministe) ; aucun contrôle de succès
    /// par exception et aucune lecture du corps de la réponse (les deux videraient HDR-02 de son sens, et la
    /// seconde ferait remonter du texte venu du réseau).</summary>
    [Fact]
    public void Aucun_type_WPF_ni_aucune_horloge_systeme_dans_la_sonde()
    {
        var type = typeof(RateLimitHeaderUsageProvider);
        string[] interditsWpf = { "PresentationCore", "PresentationFramework", "WindowsBase" };

        var assembliesTouches = type.GetMethods()
            .SelectMany(m => new[] { m.ReturnType }.Concat(m.GetParameters().Select(p => p.ParameterType)))
            .Concat(type.GetProperties().Select(p => p.PropertyType))
            .Select(x => x.Assembly.GetName().Name)
            .Where(n => n is not null);

        Assert.DoesNotContain(assembliesTouches, n => interditsWpf.Contains(n));

        // Garde TEXTUELLE : la réflexion ne peut pas voir une horloge système ni un appel absent. Le chemin
        // des sources est INJECTÉ par MSBuild (motif NormalisationUniqueTests) et jamais deviné.
        var racine = typeof(RateLimitHeaderUsageProviderTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "CheminSourcesChronos")?.Value ?? "";
        Assert.False(string.IsNullOrWhiteSpace(racine));

        var texte = File.ReadAllText(Path.Combine(racine, "Services", "RateLimitHeaderUsageProvider.cs"));

        Assert.DoesNotContain("DateTimeOffset.UtcNow", texte);       // toute horloge passe par IClock
        Assert.DoesNotContain("EnsureSuccessStatusCode", texte);     // jamais le chemin de contrôle (HDR-02)
        Assert.DoesNotContain("JsonDocument", texte);                // le corps n'est jamais lu
        Assert.DoesNotContain("ReadAsStreamAsync", texte);
        Assert.DoesNotContain("ReadAsStringAsync", texte);
        Assert.DoesNotContain("RefreshAsync", texte);                // jamais un troisième rafraîchisseur
        Assert.Contains("UsageNormalization.", texte);               // aucune conversion locale
    }
}
