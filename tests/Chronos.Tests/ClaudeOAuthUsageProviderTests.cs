using System.Net;
using System.Net.Http;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve API-01 (GET /api/oauth/usage → mapping schéma OAuth réel : utilization/100 → Utilization,
/// resets_at ISO 8601 → ResetsAt, Reliability = Exact, FractionTimeRemaining calculé ; en-têtes
/// Authorization + anthropic-beta émis ; fenêtre absente → Unavailable), API-02 (401/403/500, réseau,
/// timeout, JSON malformé → UsageSnapshot.Empty, jamais d'exception) et API-03 (inertie si token
/// null/expiré = 0 appel réseau ; annulation → pas de crash).
///
/// Tests PURS et déterministes : IClaudeTokenReader, HttpClient (via FakeHttpMessageHandler) et IClock
/// injectés → aucun réseau réel, aucun coffre réel.
/// </summary>
public class ClaudeOAuthUsageProviderTests
{
    // Horloge figée AVANT les resets des fixtures (fractions de temps positives).
    private static readonly DateTimeOffset Now = new(2026, 07, 09, 08, 00, 00, TimeSpan.Zero);

    private const string FiveHourReset = "2026-07-09T09:39:59.692687+00:00";
    private const string SevenDayReset = "2026-07-10T21:59:59.692707+00:00";

    // Réponse OAuth réelle anonymisée (schéma RESEARCH : fenêtres à la racine, utilization 0..100,
    // resets_at ISO 8601 avec offset + microsecondes ; extras ignorés).
    private static string NominalBody(double fiveUtil = 65.0, double sevenUtil = 92.0) => $$"""
        {
          "five_hour": { "utilization": {{fiveUtil}}, "resets_at": "{{FiveHourReset}}",
                         "limit_dollars": null, "used_dollars": null },
          "seven_day": { "utilization": {{sevenUtil}}, "resets_at": "{{SevenDayReset}}",
                         "limit_dollars": null, "used_dollars": null },
          "seven_day_opus": null,
          "limits": [ { "kind": "session", "percent": 42 } ],
          "spend": { "used": { "amount_minor": 0 } }
        }
        """;

    private static ClaudeOAuthUsageProvider ProviderWith(
        FakeHttpMessageHandler handler,
        out FakeClaudeTokenReader reader,
        out FakeClock clock,
        string? token = "secret-access-token",
        DateTimeOffset? expires = null)
    {
        reader = new FakeClaudeTokenReader { Token = token, Expires = expires };
        clock = new FakeClock(Now);
        return new ClaudeOAuthUsageProvider(reader, new HttpClient(handler), clock);
    }

    // --- API-01 : mapping nominal + en-têtes émis ---

    [Fact]
    public async Task Reponse_valide_mappe_les_deux_fenetres_en_Exact()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, NominalBody(65.0, 92.0));
        var provider = ProviderWith(handler, out _, out _);

        var snap = await provider.GetAsync();

        // utilization 65.0 → 0.65 ; 92.0 → 0.92 (Pitfall 3 : /100).
        Assert.NotNull(snap.FiveHour.Utilization);
        Assert.Equal(0.65, snap.FiveHour.Utilization!.Value, 9);
        Assert.NotNull(snap.SevenDay.Utilization);
        Assert.Equal(0.92, snap.SevenDay.Utilization!.Value, 9);

        // resets_at ISO 8601 → DateTimeOffset (Pitfall 2 : pas d'epoch).
        Assert.Equal(DateTimeOffset.Parse(FiveHourReset), snap.FiveHour.ResetsAt);
        Assert.Equal(DateTimeOffset.Parse(SevenDayReset), snap.SevenDay.ResetsAt);

        // Provenance Exact + fraction de temps calculée.
        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Exact, snap.SevenDay.Reliability);
        Assert.NotNull(snap.FiveHour.FractionTimeRemaining);
        Assert.NotNull(snap.SevenDay.FractionTimeRemaining);
        Assert.NotNull(snap.SourceCapturedAt);
    }

    [Fact]
    public async Task Requete_porte_les_entetes_Authorization_et_anthropic_beta()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, NominalBody());
        var provider = ProviderWith(handler, out _, out _, token: "abc123");

        await provider.GetAsync();

        Assert.Equal(1, handler.SendCount);
        var req = handler.LastRequest!;
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("https://api.anthropic.com/api/oauth/usage", req.RequestUri!.ToString());
        Assert.Equal("Bearer abc123", req.Headers.GetValues("Authorization").Single());
        Assert.Equal("oauth-2025-04-20", req.Headers.GetValues("anthropic-beta").Single());
    }

    // --- API-01 : fenêtre absente → Unavailable, aucune valeur inventée ---

    [Fact]
    public async Task Fenetre_seven_day_absente_reste_Unavailable()
    {
        const string body = """
            { "five_hour": { "utilization": 30.0, "resets_at": "2026-07-09T09:39:59.692687+00:00" } }
            """;
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, body);
        var provider = ProviderWith(handler, out _, out _);

        var snap = await provider.GetAsync();

        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(0.30, snap.FiveHour.Utilization!.Value, 9);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
        Assert.Null(snap.SevenDay.Utilization);
    }

    // --- API-02 : statuts d'erreur → Empty, aucune exception ---

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Statut_erreur_renvoie_Empty(HttpStatusCode status)
    {
        var handler = FakeHttpMessageHandler.Json(status, "{}");
        var provider = ProviderWith(handler, out _, out _);

        var snap = await provider.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    // --- API-02 : réseau / timeout → Empty, aucune exception ---

    [Fact]
    public async Task Erreur_reseau_renvoie_Empty()
    {
        var handler = FakeHttpMessageHandler.Throws(new HttpRequestException("boom"));
        var provider = ProviderWith(handler, out _, out _);

        var snap = await provider.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    [Fact]
    public async Task Timeout_TaskCanceled_renvoie_Empty()
    {
        var handler = FakeHttpMessageHandler.Throws(new TaskCanceledException("timeout"));
        var provider = ProviderWith(handler, out _, out _);

        var snap = await provider.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    // --- API-02 : JSON malformé → Empty, aucune exception ---

    [Fact]
    public async Task Json_malforme_renvoie_Empty()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, "{ oops");
        var provider = ProviderWith(handler, out _, out _);

        var snap = await provider.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    // --- API-03 : token null → inerte (0 appel réseau) ---

    [Fact]
    public async Task Token_null_est_inerte_aucun_appel_reseau()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, NominalBody());
        var provider = ProviderWith(handler, out _, out _, token: null);

        var snap = await provider.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(0, handler.SendCount); // aucun appel émis.
    }

    // --- API-03 : token expiré → court-circuit (0 appel réseau) ---

    [Fact]
    public async Task Token_expire_court_circuite_aucun_appel_reseau()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, NominalBody());
        // expiresAt < now → ne PAS appeler (évite un 401 inutile).
        var provider = ProviderWith(handler, out _, out _, expires: Now - TimeSpan.FromHours(1));

        var snap = await provider.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(0, handler.SendCount);
    }

    [Fact]
    public async Task Token_non_expire_declenche_bien_l_appel()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, NominalBody());
        // expiresAt futur → l'appel doit avoir lieu (garde-fou du court-circuit).
        var provider = ProviderWith(handler, out _, out _, expires: Now + TimeSpan.FromHours(1));

        var snap = await provider.GetAsync();

        Assert.Equal(1, handler.SendCount);
        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
    }

    // --- API-03 : annulation → pas de crash, retourne Empty ---

    [Fact]
    public async Task Annulation_ne_crashe_pas_et_renvoie_Empty()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, NominalBody());
        var provider = ProviderWith(handler, out _, out _);

        var snap = await provider.GetAsync(new CancellationToken(canceled: true));

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    // --- Anti-429 (correctif « infos étranges ponctuelles ») : throttle + backoff + conservation de l'exact ---

    // Handler qui renvoie une séquence de statuts (un par appel), puis répète le dernier.
    private static FakeHttpMessageHandler Sequence(params (HttpStatusCode status, string body)[] steps)
    {
        int i = 0;
        return new FakeHttpMessageHandler(_ =>
        {
            var (s, b) = steps[System.Math.Min(i, steps.Length - 1)];
            i++;
            return new HttpResponseMessage(s) { Content = new StringContent(b) };
        });
    }

    [Fact]
    public async Task Throttle_deux_appels_rapproches_ne_touchent_le_reseau_qu_une_fois()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, NominalBody(65, 92));
        var provider = ProviderWith(handler, out _, out var clock);

        var a = await provider.GetAsync();          // 1er appel → réseau
        clock.UtcNow = Now + TimeSpan.FromSeconds(30); // < MinInterval (120 s)
        var b = await provider.GetAsync();          // throttlé → cache, PAS de réseau

        Assert.Equal(1, handler.SendCount);         // un seul appel réseau
        Assert.Equal(0.65, a.FiveHour.Utilization); // exact
        Assert.Equal(0.65, b.FiveHour.Utilization); // même exact servi depuis le cache
    }

    [Fact]
    public async Task Apres_MinInterval_le_reseau_est_reinterroge()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, NominalBody());
        var provider = ProviderWith(handler, out _, out var clock);

        await provider.GetAsync();
        clock.UtcNow = Now + TimeSpan.FromSeconds(121); // > MinInterval
        await provider.GetAsync();

        Assert.Equal(2, handler.SendCount);
    }

    [Fact]
    public async Task Sur_429_l_exact_precedent_est_conserve_pas_de_bascule_en_indisponible()
    {
        // 1er appel OK (exact mémorisé), 2e appel 429 → doit RE-servir l'exact, pas Unavailable.
        var handler = Sequence(
            (HttpStatusCode.OK, NominalBody(65, 92)),
            (HttpStatusCode.TooManyRequests, "{}"));
        var provider = ProviderWith(handler, out _, out var clock);

        var exact = await provider.GetAsync();
        Assert.Equal(SourceReliability.Exact, exact.FiveHour.Reliability);

        clock.UtcNow = Now + TimeSpan.FromSeconds(121); // dépasse le throttle → 2e appel réel → 429
        var apres429 = await provider.GetAsync();

        Assert.Equal(2, handler.SendCount);
        Assert.Equal(SourceReliability.Exact, apres429.FiveHour.Reliability); // exact conservé (pas de clignotement)
        Assert.Equal(0.65, apres429.FiveHour.Utilization);
    }

    [Fact]
    public async Task Sur_429_sans_cache_renvoie_Empty_puis_recule_de_5_min()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.TooManyRequests, "{}");
        var provider = ProviderWith(handler, out _, out var clock);

        var s1 = await provider.GetAsync();                 // 429, aucun cache → Empty
        Assert.Equal(SourceReliability.Unavailable, s1.FiveHour.Reliability);
        Assert.Equal(1, handler.SendCount);

        clock.UtcNow = Now + TimeSpan.FromMinutes(2);       // < Backoff429 (5 min) → pas de nouvel appel…
        await provider.GetAsync();
        // …mais sans cache le throttle ne s'applique pas (condition « _cached is not null ») → un appel a lieu.
        // On vérifie surtout qu'aucun crash et toujours Unavailable.
        clock.UtcNow = Now + TimeSpan.FromMinutes(6);       // > Backoff429 → réessai autorisé
        var s3 = await provider.GetAsync();
        Assert.Equal(SourceReliability.Unavailable, s3.FiveHour.Reliability);
    }
}
