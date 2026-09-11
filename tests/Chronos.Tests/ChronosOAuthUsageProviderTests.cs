using System.IO;
using System.Net;
using System.Net.Http;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// WAVE 0 DE LA PHASE 17 — pose de couverture AVANT réécriture.
///
/// <see cref="ChronosOAuthUsageProvider"/> est la classe centrale de la phase (c'est elle qui a servi
/// un 401 muet pendant deux mois) et elle n'avait jusqu'ici AUCUN fichier de test dédié. Ce fichier
/// grave son comportement ACTUEL — en-têtes émis, normalisation d'unité 0..100 vers 0..1, resets_at
/// ISO, recul sur 429, repli sur cache, tolérance aux pannes — pour que le plan 17-04 puisse le
/// refondre (extraction de ChronosTokenAuthority) sans régression silencieuse.
///
/// RÉÉCRIT PAR LE PLAN 17-04. Les trois tests qui NOMMAIENT les défauts d'avant-phase (401 muet,
/// refresh paresseux, recul 429 inopérant sans cache) ont été RÉÉCRITS — jamais supprimés — en
/// tests du comportement CORRIGÉ : chacun porte en doc le défaut qu'il remplaçait. C'est la trace
/// exécutable du bug avant/après.
///
/// Le provider est désormais un simple CONSOMMATEUR de <see cref="ChronosTokenAuthority"/> : il ne
/// rafraîchit plus rien lui-même. Le montage de test construit donc l'autorité (coffre temporaire +
/// client à faux transport) et la lui injecte.
///
/// SÉCURITÉ — contrainte qui prime sur tout : aucun test n'ouvre le coffre réel de l'utilisateur et
/// aucun ne joint le réseau. Le coffre est toujours construit sur un chemin issu de
/// <c>Path.GetTempPath()</c> (garde <c>Assert.StartsWith</c>), et tout le trafic passe par
/// <c>FakeHttpMessageHandler</c>. Motif : l'endpoint de refresh fait TOURNER le refresh token — un
/// appel réel dont le résultat n'est pas re-sauvegardé invaliderait DÉFINITIVEMENT le login.
///
/// Pas de [Collection("XAML WPF")] : cette classe ne charge aucun BAML, elle reste parallélisable.
/// </summary>
public class ChronosOAuthUsageProviderTests
{
    // Horloge figée AVANT les resets des fixtures (fractions de temps positives).
    private static readonly DateTimeOffset Maintenant = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    // Schéma réel de GET /api/oauth/usage : utilization en 0..100 (PAS 0..1), resets_at en ISO 8601
    // (PAS epoch). Les deux pièges d'unité de la phase 17 sont encodés ici.
    private const string CorpsNominal = """
        {"five_hour":{"utilization":42,"resets_at":"2026-09-09T18:30:00+00:00"},
         "seven_day":{"utilization":63,"resets_at":"2026-09-14T09:00:00+00:00"}}
        """;

    // SÉCURITÉ : chemin de coffre TOUJOURS sous %TEMP%. Aucun test n'ouvre le coffre réel :
    // le refresh token de l'utilisateur ne doit être ni lu, ni déchiffré, ni envoyé (rotation !).
    private static string CheminCoffreTemp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosOAuthProviderTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "oauth.dat");
    }

    // Corps de rafraîchissement nominal du point de terminaison de jeton (faux transport uniquement).
    private const string CorpsJetonNeuf = """{"access_token":"ACC-2","refresh_token":"REF-2","expires_in":3600}""";

    // Corps d'erreur RÉELS de l'endpoint d'usage : 429 rate-limit (TEMPORAIRE, jamais une déconnexion)
    // et refus d'identifiants (401/403).
    private const string CorpsRateLimit = """{"error":{"type":"rate_limit_error"}}""";
    private const string CorpsRefus = """{"error":"unauthorized"}""";

    /// <summary>
    /// POINT UNIQUE de construction du provider (réécrit par le plan 17-04 : le ctor est passé à
    /// (ChronosTokenAuthority, HttpClient, IClock)). Rend aussi l'autorité pour que les tests puissent
    /// observer l'ÉTAT d'authentification — ce que le 401 muet ne permettait pas.
    ///
    /// SÉCURITÉ : le coffre est TOUJOURS sous %TEMP% (garde Assert.StartsWith) et le client OAuth ne
    /// parle qu'à un FakeHttpMessageHandler. Le refresh token réel de l'utilisateur n'est ni lu, ni
    /// déchiffré, ni présenté au serveur (la rotation le tuerait définitivement).
    /// </summary>
    private static (ChronosOAuthUsageProvider provider, ChronosTokenAuthority autorite) Provider(
        DateTimeOffset? expiration, FakeHttpMessageHandler usage, FakeClock horloge,
        FakeHttpMessageHandler? jeton = null)
    {
        var chemin = CheminCoffreTemp();
        Assert.StartsWith(Path.GetTempPath(), chemin);   // garde anti-accident (motif CompositionRootTests:109)
        var coffre = new ChronosOAuthStore(chemin);
        if (expiration is { } e) coffre.Save(new OAuthTokens("ACC-VALIDE", "REF-VALIDE", e));
        var client = new ChronosOAuthClient(
            new HttpClient(jeton ?? FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsJetonNeuf)),
            horloge);
        var autorite = new ChronosTokenAuthority(coffre, client, horloge);
        return (new ChronosOAuthUsageProvider(autorite, new HttpClient(usage), horloge), autorite);
    }

    // Handler qui renvoie une séquence de réponses (une par appel), puis répète la dernière.
    private static FakeHttpMessageHandler Sequence(params (HttpStatusCode statut, string corps)[] etapes)
    {
        var i = 0;
        return new FakeHttpMessageHandler(_ =>
        {
            var (s, b) = etapes[Math.Min(i, etapes.Length - 1)];
            i++;
            return new HttpResponseMessage(s) { Content = new StringContent(b) };
        });
    }

    // --- Isolation : la garde qui protège le coffre réel ---

    [Fact]
    public void Le_coffre_de_test_vit_toujours_sous_le_dossier_temporaire()
    {
        var chemin = CheminCoffreTemp();

        Assert.StartsWith(Path.GetTempPath(), chemin);
        Assert.Contains("ChronosOAuthProviderTest_", chemin);
    }

    // --- Pas connecté : inerte, aucun appel réseau ---

    [Fact]
    public async Task Sans_jeton_le_provider_rend_Empty_sans_appeler_le_reseau()
    {
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsNominal);
        var (p, _) = Provider(null, usage, new FakeClock(Maintenant));

        var snap = await p.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
        Assert.Equal(0, usage.SendCount);
    }

    // --- Nominal : normalisation d'unité, resets_at ISO, provenance Exact ---

    [Fact]
    public async Task Un_200_normalise_l_utilization_de_0_100_vers_0_1()
    {
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsNominal);
        var (p, autorite) = Provider(Maintenant.AddHours(1), usage, new FakeClock(Maintenant));

        var snap = await p.GetAsync();

        Assert.Equal(0.42, snap.FiveHour.Utilization!.Value, 6);   // 42 (0..100) devient 0.42 (0..1)
        Assert.Equal(0.63, snap.SevenDay.Utilization!.Value, 6);
        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Exact, snap.SevenDay.Reliability);
        Assert.Equal(DateTimeOffset.Parse("2026-09-09T18:30:00+00:00"), snap.FiveHour.ResetsAt);
        Assert.Equal(DateTimeOffset.Parse("2026-09-14T09:00:00+00:00"), snap.SevenDay.ResetsAt);
        Assert.Equal(Maintenant, snap.SourceCapturedAt!.Value);
        Assert.NotNull(snap.FiveHour.FractionTimeRemaining);
        // TOK-02 : un 2xx EXPLOITÉ déverrouille l'état — la pastille pourra disparaître d'elle-même.
        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);
    }

    [Fact]
    public async Task La_requete_porte_les_trois_entetes_exiges_par_l_endpoint()
    {
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsNominal);
        var (p, _) = Provider(Maintenant.AddHours(1), usage, new FakeClock(Maintenant));

        await p.GetAsync();

        var req = usage.LastRequest!;
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("Bearer ACC-VALIDE", string.Concat(req.Headers.GetValues("Authorization")));
        Assert.Equal("oauth-2025-04-20", string.Concat(req.Headers.GetValues("anthropic-beta")));
        // Sans User-Agent claude-code/*, l'endpoint répond des 429 agressifs (vérifié).
        Assert.StartsWith("claude-code/", string.Concat(req.Headers.GetValues("User-Agent")));
    }

    [Fact]
    public async Task Aucun_jeton_ne_transite_JAMAIS_par_l_URL()
    {
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsNominal);
        var (p, _) = Provider(Maintenant.AddHours(1), usage, new FakeClock(Maintenant));

        await p.GetAsync();

        var url = usage.LastRequest!.RequestUri!.ToString();
        Assert.Equal("https://api.anthropic.com/api/oauth/usage", url);
        Assert.DoesNotContain("ACC-VALIDE", url);
        Assert.DoesNotContain("REF-VALIDE", url);
    }

    [Fact]
    public async Task Une_fenetre_absente_du_JSON_reste_Unavailable()
    {
        const string corps = """{"five_hour":{"utilization":30,"resets_at":"2026-09-09T18:30:00+00:00"}}""";
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.OK, corps);
        var (p, _) = Provider(Maintenant.AddHours(1), usage, new FakeClock(Maintenant));

        var snap = await p.GetAsync();

        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(0.30, snap.FiveHour.Utilization!.Value, 6);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
        Assert.Null(snap.SevenDay.Utilization);   // aucune valeur inventée
    }

    // --- Échecs : jamais d'exception, repli honnête ---

    [Fact]
    public async Task Un_500_rend_Empty()
    {
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.InternalServerError, "");
        var (p, _) = Provider(Maintenant.AddHours(1), usage, new FakeClock(Maintenant));

        var snap = await p.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    [Fact]
    public async Task Une_exception_reseau_rend_Empty_sans_jamais_lever()
    {
        var usage = FakeHttpMessageHandler.Throws(new HttpRequestException("réseau coupé"));
        var (p, _) = Provider(Maintenant.AddHours(1), usage, new FakeClock(Maintenant));

        UsageSnapshot? snap = null;
        var ex = await Record.ExceptionAsync(async () => snap = await p.GetAsync());

        Assert.Null(ex);
        Assert.Equal(SourceReliability.Unavailable, snap!.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    [Fact]
    public async Task Le_dernier_exact_est_resservi_quand_l_appel_suivant_echoue()
    {
        // 200 puis 500 : le cache (utilisable 15 min) doit couvrir la panne plutôt que clignoter.
        var usage = Sequence((HttpStatusCode.OK, CorpsNominal), (HttpStatusCode.InternalServerError, ""));
        var horloge = new FakeClock(Maintenant);
        var (p, _) = Provider(Maintenant.AddHours(1), usage, horloge);

        await p.GetAsync();
        horloge.UtcNow = Maintenant.AddMinutes(3);   // > MinInterval (120 s), < CacheUsable (15 min)
        var apres = await p.GetAsync();

        Assert.Equal(2, usage.SendCount);
        Assert.Equal(0.42, apres.FiveHour.Utilization!.Value, 6);
        Assert.Equal(SourceReliability.Exact, apres.FiveHour.Reliability);
    }

    // --- Anti-429 ---

    [Fact]
    public async Task Apres_un_200_le_recul_sur_429_tient_et_le_rappel_n_emet_aucune_requete()
    {
        var usage = Sequence(
            (HttpStatusCode.OK, CorpsNominal),
            ((HttpStatusCode)429, """{"error":{"type":"rate_limit_error"}}"""));
        var horloge = new FakeClock(Maintenant);
        var (p, _) = Provider(Maintenant.AddHours(1), usage, horloge);

        await p.GetAsync();                            // 200 : cache posé
        horloge.UtcNow = Maintenant.AddMinutes(3);     // > MinInterval : 2e appel réel
        await p.GetAsync();                            // 429 : recul de 5 min
        Assert.Equal(2, usage.SendCount);

        horloge.UtcNow = Maintenant.AddMinutes(6);     // < 3 min + 5 min de recul
        await p.GetAsync();

        Assert.Equal(2, usage.SendCount);              // le recul tient : aucune requête de plus
    }

    /// <summary>
    /// TOK-01 — remplace <c>Un_429_sans_cache_ne_freine_aujourd_hui_absolument_rien</c> (plan 17-01).
    /// Le garde-fou d'origine exigeait DEUX conditions — <c>now &lt; _nextAllowedCall</c> ET
    /// <c>_cached is not null</c> — or le cache est un champ d'instance en RAM, donc vide À CHAQUE
    /// démarrage de l'exe : un exe relancé après un 429 remartelait l'endpoint sans aucun frein, ce
    /// qui entretenait le 429 qui l'avait causé. « Ai-je le droit d'appeler ? » est désormais
    /// DISTINCT de « ai-je un cache à servir ? ».
    /// </summary>
    [Fact]
    public async Task Le_recul_freine_des_le_PREMIER_429_sans_aucun_cache_prealable()
    {
        var usage = FakeHttpMessageHandler.Json((HttpStatusCode)429, CorpsRateLimit);
        var (p, _) = Provider(Maintenant.AddHours(1), usage, new FakeClock(Maintenant));

        await p.GetAsync();                 // 1er 429 : le recul de 5 min se pose
        Assert.Equal(1, usage.SendCount);

        await p.GetAsync();                 // même instant, en plein recul, et SANS aucun cache

        Assert.Equal(1, usage.SendCount);   // le frein existe sans qu'aucun appel n'ait jamais réussi
    }

    /// <summary>TOK-02 — un rate-limit n'est PAS une déconnexion. Crier « reconnecte-toi » sur un 429
    /// est exactement la fausse alerte que la phase 17 interdit : le compte est parfaitement sain.</summary>
    [Fact]
    public async Task Un_429_ne_declare_JAMAIS_une_deconnexion()
    {
        var usage = FakeHttpMessageHandler.Json((HttpStatusCode)429, CorpsRateLimit);
        var (p, autorite) = Provider(Maintenant.AddHours(1), usage, new FakeClock(Maintenant));

        var snap = await p.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.NotEqual(EtatAuthentification.Deconnecte, autorite.Etat);
    }

    /// <summary>TOK-02 — une panne réseau non plus : c'est peut-être seulement le wifi. Confondre
    /// « hors ligne » et « déconnecté » ferait relancer un login voué à l'échec.</summary>
    [Fact]
    public async Task Une_exception_reseau_ne_declare_JAMAIS_une_deconnexion()
    {
        var usage = FakeHttpMessageHandler.Throws(new HttpRequestException("réseau coupé"));
        var (p, autorite) = Provider(Maintenant.AddHours(1), usage, new FakeClock(Maintenant));

        await p.GetAsync();

        Assert.NotEqual(EtatAuthentification.Deconnecte, autorite.Etat);
    }

    // --- Les deux défauts au cœur de la phase 17, désormais corrigés ---

    /// <summary>
    /// TOK-02 — remplace <c>Un_401_est_aujourd_hui_totalement_muet</c> (plan 17-01, qui documentait le
    /// 401 muet de deux mois : traité exactement comme un 500, sans aucun état exposé). Le serveur peut
    /// révoquer un jeton avant son ExpiresAt local (cas documenté claude-code#54443) : on invalide, on
    /// rejoue UNE fois avec un jeton frais, et si le refus persiste on le DIT.
    /// </summary>
    [Fact]
    public async Task Un_401_persistant_declenche_UN_seul_rejeu_puis_declare_Deconnecte()
    {
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.Unauthorized, CorpsRefus);
        var (p, autorite) = Provider(Maintenant.AddHours(1), usage, new FakeClock(Maintenant));

        var snap = await p.GetAsync();

        Assert.Equal(2, usage.SendCount);                              // un rejeu, UN SEUL
        Assert.Equal(EtatAuthentification.Deconnecte, autorite.Etat);  // plus jamais muet
        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Null(snap.SourceCapturedAt);
    }

    /// <summary>Le rejeu ne dégénère pas en boucle : sans cette garde, on enchaînerait refresh+401
    /// jusqu'à déclencher un 429 sur le point de terminaison de JETON — en pleine fausse déconnexion.</summary>
    [Fact]
    public async Task Un_troisieme_appel_apres_un_401_persistant_ne_relance_aucun_rejeu()
    {
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.Unauthorized, CorpsRefus);
        var horloge = new FakeClock(Maintenant);
        var (p, _) = Provider(Maintenant.AddHours(1), usage, horloge);

        await p.GetAsync();
        Assert.Equal(2, usage.SendCount);

        horloge.UtcNow = Maintenant.AddMinutes(3);   // recul purgé : le frein n'explique plus rien
        await p.GetAsync();

        Assert.Equal(2, usage.SendCount);            // refus définitif : l'autorité ne rend plus de jeton
    }

    /// <summary>TOK-01/TOK-02 — le cas RÉPARABLE : le jeton avait été révoqué AVANT son ExpiresAt
    /// local, le rejeu répare tout seul et rien n'est signalé à l'utilisateur. Pendant indispensable du
    /// test précédent : un rejeu qui crierait « déconnecté » à tort serait pire que le silence d'origine.</summary>
    [Fact]
    public async Task Un_401_suivi_d_un_200_rend_des_chiffres_exacts_sans_rien_signaler()
    {
        var usage = Sequence((HttpStatusCode.Unauthorized, CorpsRefus), (HttpStatusCode.OK, CorpsNominal));
        var (p, autorite) = Provider(Maintenant.AddHours(1), usage, new FakeClock(Maintenant));

        var snap = await p.GetAsync();

        Assert.Equal(2, usage.SendCount);
        Assert.Equal(0.42, snap.FiveHour.Utilization!.Value, 6);
        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);
        // Le jeton FRAIS obtenu de l'autorité est bien celui qui part sur le rejeu.
        Assert.Equal("Bearer ACC-2", string.Concat(usage.LastRequest!.Headers.GetValues("Authorization")));
    }

    /// <summary>TOK-02 — un 403 est un refus de SCOPE/abonnement : aucun rafraîchissement n'y changera
    /// quoi que ce soit, donc aucun rejeu. On le dit immédiatement.</summary>
    [Fact]
    public async Task Un_403_declare_Deconnecte_sans_rejeu()
    {
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.Forbidden, CorpsRefus);
        var (p, autorite) = Provider(Maintenant.AddHours(1), usage, new FakeClock(Maintenant));

        var snap = await p.GetAsync();

        Assert.Equal(1, usage.SendCount);                              // aucun rejeu : inutile
        Assert.Equal(EtatAuthentification.Deconnecte, autorite.Etat);
        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
    }

    /// <summary>
    /// TOK-01 — remplace <c>Le_rafraichissement_est_aujourd_hui_paresseux_declenche_par_le_GetAsync</c>
    /// (plan 17-01). Le provider ne rafraîchit plus : il n'existe qu'UN SEUL rafraîchisseur dans
    /// l'application. Deux auraient produit deux rotations concurrentes du refresh token, donc un
    /// invalid_grant sur le second, donc une FAUSSE déconnexion sur un compte sain (claude-code#25609).
    /// </summary>
    [Fact]
    public async Task Le_provider_ne_rafraichit_plus_lui_meme_il_consomme_l_autorite()
    {
        var jeton = FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsJetonNeuf);
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsNominal);
        var horloge = new FakeClock(Maintenant);
        var (p, _) = Provider(Maintenant.AddMinutes(-1), usage, horloge, jeton);

        await p.GetAsync();
        horloge.UtcNow = Maintenant.AddMinutes(3);   // > MinInterval : un VRAI second appel d'usage part
        await p.GetAsync();

        Assert.Equal(2, usage.SendCount);   // deux lectures d'usage...
        Assert.Equal(1, jeton.SendCount);   // ...mais UNE seule rotation, portée par l'autorité
        Assert.Equal("Bearer ACC-2", string.Concat(usage.LastRequest!.Headers.GetValues("Authorization")));
    }
}
