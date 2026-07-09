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

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        SendCount++; LastRequest = request;
        return Task.FromResult(_responder(request));
    }
}
