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
/// Trois tests NOMMENT des défauts et sont VERTS aujourd'hui : ils décrivent l'état d'avant-phase.
/// Les plans 17-02 et 17-04 les RÉÉCRIVENT (ils ne les suppriment jamais) — c'est la trace exécutable
/// du bug avant/après.
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

    /// <summary>Coffre isolé, éventuellement pré-rempli. expiration null =&gt; aucun jeton enregistré.</summary>
    private static ChronosOAuthStore CoffreAvec(DateTimeOffset? expiration)
    {
        var chemin = CheminCoffreTemp();
        Assert.StartsWith(Path.GetTempPath(), chemin);   // garde anti-accident (motif CompositionRootTests:109)
        var coffre = new ChronosOAuthStore(chemin);
        if (expiration is { } e) coffre.Save(new OAuthTokens("ACC-VALIDE", "REF-VALIDE", e));
        return coffre;
    }

    /// <summary>
    /// POINT UNIQUE de construction du provider : le plan 17-04 ne réécrira QUE ce corps quand le
    /// constructeur passera à (ChronosTokenAuthority, HttpClient, IClock).
    /// </summary>
    private static ChronosOAuthUsageProvider Provider(
        ChronosOAuthStore coffre, FakeHttpMessageHandler usage, FakeClock horloge,
        FakeHttpMessageHandler? jeton = null)
        => new ChronosOAuthUsageProvider(
               coffre,
               new ChronosOAuthClient(new HttpClient(jeton ?? FakeHttpMessageHandler.Json(HttpStatusCode.BadRequest, "{}"))),
               new HttpClient(usage),
               horloge);

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
        var p = Provider(CoffreAvec(null), usage, new FakeClock(Maintenant));

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
        var p = Provider(CoffreAvec(Maintenant.AddHours(1)), usage, new FakeClock(Maintenant));

        var snap = await p.GetAsync();

        Assert.Equal(0.42, snap.FiveHour.Utilization!.Value, 6);   // 42 (0..100) devient 0.42 (0..1)
        Assert.Equal(0.63, snap.SevenDay.Utilization!.Value, 6);
        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Exact, snap.SevenDay.Reliability);
        Assert.Equal(DateTimeOffset.Parse("2026-09-09T18:30:00+00:00"), snap.FiveHour.ResetsAt);
        Assert.Equal(DateTimeOffset.Parse("2026-09-14T09:00:00+00:00"), snap.SevenDay.ResetsAt);
        Assert.Equal(Maintenant, snap.SourceCapturedAt!.Value);
        Assert.NotNull(snap.FiveHour.FractionTimeRemaining);
    }

    [Fact]
    public async Task La_requete_porte_les_trois_entetes_exiges_par_l_endpoint()
    {
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsNominal);
        var p = Provider(CoffreAvec(Maintenant.AddHours(1)), usage, new FakeClock(Maintenant));

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
        var p = Provider(CoffreAvec(Maintenant.AddHours(1)), usage, new FakeClock(Maintenant));

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
        var p = Provider(CoffreAvec(Maintenant.AddHours(1)), usage, new FakeClock(Maintenant));

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
        var p = Provider(CoffreAvec(Maintenant.AddHours(1)), usage, new FakeClock(Maintenant));

        var snap = await p.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    [Fact]
    public async Task Une_exception_reseau_rend_Empty_sans_jamais_lever()
    {
        var usage = FakeHttpMessageHandler.Throws(new HttpRequestException("réseau coupé"));
        var p = Provider(CoffreAvec(Maintenant.AddHours(1)), usage, new FakeClock(Maintenant));

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
        var p = Provider(CoffreAvec(Maintenant.AddHours(1)), usage, horloge);

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
        var p = Provider(CoffreAvec(Maintenant.AddHours(1)), usage, horloge);

        await p.GetAsync();                            // 200 : cache posé
        horloge.UtcNow = Maintenant.AddMinutes(3);     // > MinInterval : 2e appel réel
        await p.GetAsync();                            // 429 : recul de 5 min
        Assert.Equal(2, usage.SendCount);

        horloge.UtcNow = Maintenant.AddMinutes(6);     // < 3 min + 5 min de recul
        await p.GetAsync();

        Assert.Equal(2, usage.SendCount);              // le recul tient : aucune requête de plus
    }

    /// <summary>
    /// DÉFAUT DOCUMENTÉ (état AVANT la phase 17). Le garde-fou de tête exige DEUX conditions —
    /// <c>now &lt; _nextAllowedCall</c> ET <c>_cached is not null</c> (ChronosOAuthUsageProvider.cs:53).
    /// Conséquence : tant qu'aucun appel n'a JAMAIS réussi, le recul sur 429 ne freine rien du tout.
    /// C'est précisément la situation d'un exe qui vient de démarrer avec un jeton mort — le cache est
    /// un champ d'instance en RAM, donc vide à chaque lancement. Le provider remartèle alors l'endpoint
    /// à chaque tick, ce qui entretient le 429 qui l'a causé.
    /// Le plan 17-04 RÉÉCRIT ce test : le recul devra tenir sans dépendre de l'existence d'un cache.
    /// </summary>
    [Fact]
    public async Task Un_429_sans_cache_ne_freine_aujourd_hui_absolument_rien()
    {
        var usage = FakeHttpMessageHandler.Json((HttpStatusCode)429, """{"error":{"type":"rate_limit_error"}}""");
        var p = Provider(CoffreAvec(Maintenant.AddHours(1)), usage, new FakeClock(Maintenant));

        await p.GetAsync();
        await p.GetAsync();   // même instant, en plein recul de 5 min... et pourtant :

        Assert.Equal(2, usage.SendCount);   // LE DÉFAUT : le recul est inopérant tant qu'il n'y a pas de cache
    }

    // --- Les deux défauts au cœur de la phase 17 ---

    /// <summary>
    /// DÉFAUT DOCUMENTÉ (état AVANT la phase 17) — TOK-02. Un 401 est traité EXACTEMENT comme un 500
    /// ou une coupure de wifi : <c>_nextAllowedCall = now + MinInterval; return ServeCachedOr(Empty)</c>
    /// (ChronosOAuthUsageProvider.cs:94-98). Recul silencieux, repli sur le cache, aucun état exposé,
    /// aucune trace, aucun signal remontant au ViewModel. C'est ainsi que l'utilisateur a vécu deux
    /// mois avec une source exacte morte sans le savoir : le cadran a continué d'afficher des chiffres.
    /// Le plan 17-04 RÉÉCRIT ce test (invalidation + un seul rejeu + état « Deconnecte » observable).
    /// </summary>
    [Fact]
    public async Task Un_401_est_aujourd_hui_totalement_muet()
    {
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.Unauthorized, """{"error":"unauthorized"}""");
        var p = Provider(CoffreAvec(Maintenant.AddHours(1)), usage, new FakeClock(Maintenant));

        var snap = await p.GetAsync();

        // Rigoureusement indiscernable d'un 500 ou d'un câble débranché :
        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
        Assert.Null(snap.SourceCapturedAt);
        Assert.Equal(1, usage.SendCount);   // un seul essai, aucun rejeu, aucune invalidation du coffre
    }

    /// <summary>
    /// DÉFAUT DOCUMENTÉ (état AVANT la phase 17) — TOK-01. Le rafraîchissement est PARESSEUX : il n'a
    /// lieu qu'au moment d'un GetAsync (ChronosOAuthUsageProvider.cs:61-63), c'est-à-dire seulement
    /// quand quelqu'un demande un chiffre. Aucune horloge de fond ne renouvelle le jeton ; un exe
    /// laissé tourner voit donc son jeton mourir, et un échec de refresh n'est jamais réessayé
    /// autrement qu'à la faveur d'une lecture d'usage.
    /// Le plan 17-04 RÉÉCRIT ce test : le provider ne rafraîchira plus lui-même (autorité unique).
    /// </summary>
    [Fact]
    public async Task Le_rafraichissement_est_aujourd_hui_paresseux_declenche_par_le_GetAsync()
    {
        var jeton = FakeHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"access_token":"ACC-2","refresh_token":"REF-2","expires_in":3600}""");
        var usage = FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsNominal);
        var p = Provider(CoffreAvec(Maintenant.AddMinutes(-1)), usage, new FakeClock(Maintenant), jeton);

        Assert.Equal(0, jeton.SendCount);   // avant : rien au monde ne rafraîchit ce jeton expiré

        await p.GetAsync();

        Assert.Equal(1, jeton.SendCount);   // le refresh n'arrive QUE parce qu'on a demandé un chiffre
        // ...et le jeton fraîchement tourné est bien celui qui part sur l'appel d'usage :
        Assert.Equal("Bearer ACC-2", string.Concat(usage.LastRequest!.Headers.GetValues("Authorization")));
    }
}
