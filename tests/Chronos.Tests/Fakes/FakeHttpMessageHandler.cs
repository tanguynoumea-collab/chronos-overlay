using System.Net;
using System.Net.Http;

namespace Chronos.Tests;

/// <summary>Handler HTTP scripté : renvoie une réponse fixe (statut + JSON) ou lève une exception
/// (réseau/timeout), et compte les envois — pour tester le provider sans réseau réel.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    public int SendCount { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => _responder = responder;

    /// <summary>Réponse simple : statut + corps JSON.</summary>
    public static FakeHttpMessageHandler Json(HttpStatusCode status, string body) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

    /// <summary>Simule une panne réseau / un timeout via l'exception fournie.</summary>
    public static FakeHttpMessageHandler Throws(Exception ex) =>
        new(_ => throw ex);

    /// <summary>Réponse porteuse d'EN-TÊTES, sur N'IMPORTE QUEL code de statut. Le corps n'a aucune
    /// importance : la sonde d'en-têtes ne le lit JAMAIS (contrainte de sécurité — un corps d'erreur ne
    /// doit pas pouvoir remonter). Sert le cas décisif de HDR-02 : un 429 qui livre quand même les
    /// chiffres. Vérifié empiriquement : SendAsync ne lève pas sur 4xx/5xx, et les en-têtes non standard
    /// atterrissent dans resp.Headers — pas dans resp.Content.Headers.
    ///
    /// L'ajout SANS validation est OBLIGATOIRE : Headers.Add REJETTE les noms d'en-têtes non standard
    /// comme anthropic-ratelimit-unified-5h-utilization, ce qui rendrait HDR-02 intestable.</summary>
    public static FakeHttpMessageHandler AvecEnTetes(
        HttpStatusCode statut, IReadOnlyDictionary<string, string> enTetes, string corps = "{}") =>
        new(_ =>
        {
            var r = new HttpResponseMessage(statut) { Content = new StringContent(corps) };
            foreach (var (k, v) in enTetes) r.Headers.TryAddWithoutValidation(k, v);
            return r;
        });

    /// <summary>Séquence de réponses PORTEUSES D'EN-TÊTES (une par envoi, puis la dernière se répète).
    /// Indispensable pour le rejeu unique sur 401 et pour prouver le frein de cadence : deux passages
    /// consécutifs doivent pouvoir différer.</summary>
    public static FakeHttpMessageHandler SequenceAvecEnTetes(
        params (HttpStatusCode statut, IReadOnlyDictionary<string, string> enTetes)[] etapes)
    {
        var i = 0;
        return new FakeHttpMessageHandler(_ =>
        {
            var (s, h) = etapes[Math.Min(i, etapes.Length - 1)];
            i++;
            var r = new HttpResponseMessage(s) { Content = new StringContent("{}") };
            foreach (var (k, v) in h) r.Headers.TryAddWithoutValidation(k, v);
            return r;
        });
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        SendCount++; LastRequest = request;
        return Task.FromResult(_responder(request));
    }
}
