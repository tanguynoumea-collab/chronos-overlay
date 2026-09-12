using System.IO;
using System.Net;
using System.Net.Http;
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
}
