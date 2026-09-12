using System.IO;
using System.Net.Http;
using System.Text.Json;
using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Provider EXACT via le jeton PROPRE de Chronos (login OAuth intégré) : appelle
/// GET /api/oauth/usage. Fonctionne quel que soit le mode d'usage de Claude (bureau ou terminal).
///
/// CE PROVIDER NE RAFRAÎCHIT PLUS (TOK-01, plan 17-04). Il ne connaît ni le coffre chiffré,
/// ni le refresh token, ni le client OAuth : il demande un access token valide à l'autorité unique
/// (<see cref="ChronosTokenAuthority.GetAccessTokenAsync"/>) et s'arrête là.
/// POURQUOI : le flux OAuth fait TOURNER le refresh token. Deux rafraîchisseurs concurrents présentant
/// le même refresh token produisent un <c>invalid_grant</c> sur le second, donc — puisqu'un
/// <c>invalid_grant</c> signifie « déconnecté » — une FAUSSE déconnexion sur un compte parfaitement
/// sain (bug documenté <c>anthropics/claude-code#25609</c>). Le droit de rafraîchir est donc retiré à
/// tout le monde sauf un.
///
/// En-têtes requis (vérifiés) : Authorization: Bearer, anthropic-beta: oauth-2025-04-20, et
/// User-Agent: claude-code/&lt;version&gt; (sinon 429 agressifs).
///
/// Anti-429 : au plus un appel / <see cref="MinInterval"/>, recul fort sur 429, cache du dernier exact
/// (staleness honnête via SourceCapturedAt). Le droit d'appeler est DÉCOUPLÉ de l'existence d'un cache
/// (plan 17-04) : le garde-fou d'origine exigeait <c>now &lt; _nextAllowedCall</c> ET
/// <c>_cached is not null</c>, or le cache est un champ d'instance en RAM donc vide à CHAQUE démarrage
/// de l'exe — le recul ne freinait alors rien du tout, précisément au moment où le martèlement se produit.
///
/// 401 : le serveur peut révoquer un jeton AVANT son <c>ExpiresAt</c> local (cas documenté
/// <c>claude-code#54443</c>), donc le rafraîchissement préventif seul ne suffit pas — on invalide, on
/// rejoue UNE fois, et si le refus persiste on le DIT (plus jamais de 401 muet).
///
/// Pas connecté (aucun jeton) → <see cref="UsageSnapshot.Empty"/> : le composite bascule proprement sur
/// le repli. Tolérance totale : jamais d'exception, jamais de valeur inventée.
/// </summary>
public sealed class ChronosOAuthUsageProvider : IUsageProvider
{
    private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";
    private const string UserAgent = "claude-code/2.1.30";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan Backoff429 = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CacheUsable = TimeSpan.FromMinutes(15);

    private readonly ChronosTokenAuthority _autorite;
    private readonly HttpClient _http;
    private readonly IClock _clock;

    private UsageSnapshot? _cached;
    private DateTimeOffset _cachedAt;
    private DateTimeOffset _nextAllowedCall;

    public ChronosOAuthUsageProvider(ChronosTokenAuthority autorite, HttpClient http, IClock clock)
    {
        _autorite = autorite;
        _http = http;
        _clock = clock;
    }

    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        // FREIN INCONDITIONNEL. « Ai-je le droit d'appeler ? » est une question DISTINCTE de « ai-je un
        // cache à servir ? » : les confondre (le défaut d'origine) faisait disparaître le frein à chaque
        // redémarrage de l'exe, donc exactement quand un 429 en cours devait être respecté.
        if (now < _nextAllowedCall)
            return ServeCachedOr(UsageSnapshot.Empty, now);

        // SOURCE UNIQUE DE JETON. Le provider ne connaît ni le coffre, ni le refresh token, ni le
        // client OAuth : l'autorité sérialise, rafraîchit et classe les échecs pour tout le monde.
        var jeton = await _autorite.GetAccessTokenAsync(ct);
        if (jeton is null)
            return ServeCachedOr(UsageSnapshot.Empty, now);   // état déjà publié par l'autorité

        try
        {
            var resp = await EnvoyerAsync(jeton, ct);
            var rejoue = false;

            // Le serveur peut révoquer un jeton AVANT son ExpiresAt local (cas documenté
            // claude-code#54443) : le rafraîchissement préventif ne suffit donc pas. UN SEUL rejeu —
            // sans cette garde, on boucle refresh+401 jusqu'à déclencher un 429 sur l'endpoint de jeton.
            if ((int)resp.StatusCode == 401 && !rejoue)
            {
                resp.Dispose();
                rejoue = true;
                _autorite.InvaliderAccessToken();
                var frais = await _autorite.GetAccessTokenAsync(ct);
                if (frais is null)
                {
                    _nextAllowedCall = now + MinInterval;
                    return ServeCachedOr(UsageSnapshot.Empty, now);
                }
                resp = await EnvoyerAsync(frais, ct);
            }

            using (resp)
            {
                var code = (int)resp.StatusCode;

                if (code is 401 or 403)
                {
                    // Jeton FRAIS et pourtant refusé (ou scope insuffisant) : ce n'est pas un problème
                    // de fraîcheur, c'est un refus de compte. Seule une reconnexion répare.
                    _autorite.SignalerRefusServeur();
                    _nextAllowedCall = now + MinInterval;
                    return ServeCachedOr(UsageSnapshot.Empty, now);
                }
                if (code == 429)
                {
                    // TEMPORAIRE : ne JAMAIS déclarer une déconnexion sur un rate-limit (fausse alerte).
                    _nextAllowedCall = now + Backoff429;
                    return ServeCachedOr(UsageSnapshot.Empty, now);
                }
                if (!resp.IsSuccessStatusCode)
                {
                    _nextAllowedCall = now + MinInterval;
                    return ServeCachedOr(UsageSnapshot.Empty, now);
                }

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                var root = doc.RootElement;

                var snap = new UsageSnapshot
                {
                    FiveHour = Read(root, "five_hour", WindowKind.FiveHour, TimeSpan.FromHours(5), now),
                    SevenDay = Read(root, "seven_day", WindowKind.SevenDay, TimeSpan.FromDays(7), now),
                    SourceCapturedAt = now,
                };
                _cached = snap; _cachedAt = now; _nextAllowedCall = now + MinInterval;
                _autorite.SignalerSucces();   // un 2xx exploité déverrouille l'état
                return snap;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                     or OperationCanceledException or JsonException or IOException)
        {
            // Réseau/timeout : surtout PAS de « déconnecté » — c'est peut-être seulement le wifi.
            _nextAllowedCall = now + MinInterval;
            return ServeCachedOr(UsageSnapshot.Empty, now);
        }
    }

    // Envoi d'UNE requête d'usage avec le jeton fourni. SÉCURITÉ : le jeton ne vit qu'en variable
    // locale, le temps de construire l'en-tête Authorization — jamais journalisé, jamais en URL.
    private async Task<HttpResponseMessage> EnvoyerAsync(string jeton, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);

        using var req = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + jeton);
        req.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
        req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        return await _http.SendAsync(req, cts.Token);
    }

    private UsageSnapshot ServeCachedOr(UsageSnapshot fallback, DateTimeOffset now)
        => _cached is not null && (now - _cachedAt) < CacheUsable ? _cached : fallback;

    // Schéma /api/oauth/usage : utilization en 0..100, resets_at en ISO 8601 (PAS epoch).
    // Fenêtre absente → Unavailable.
    // HDR-05 : plus aucune conversion d'unité locale — tout passe par UsageNormalization.
    // EXA-02 — instant de la RÉPONSE HTTP : ici la lecture EST la capture. Le cache sert l'instance
    // d'origine, donc son horodatage d'origine vieillit honnêtement.
    private WindowState Read(JsonElement root, string name, WindowKind kind, TimeSpan len,
                             DateTimeOffset capturedAt)
    {
        if (!root.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object)
            return WindowState.Unavailable(kind);

        double? util = w.TryGetProperty("utilization", out var u) && u.TryGetDouble(out var p)
            ? UsageNormalization.FractionDepuisPourcentage(p) : null;
        DateTimeOffset? reset = w.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.String
            ? UsageNormalization.InstantDepuisIso(r.GetString()) : null;

        return new WindowState
        {
            Kind = kind,
            Utilization = util,
            ResetsAt = reset,
            Reliability = SourceReliability.Exact,
            CapturedAt = capturedAt,
            FractionTimeRemaining = WindowState.FractionRemaining(reset, _clock.UtcNow, len),
            // EXA-06 — le nom du producteur voyage PAR RÉFÉRENCE à travers Best() : posé ici, il arrive
            // intact au ViewModel même à travers les trois composites imbriqués.
            Source = SourceUsage.EndpointOAuthChronos,
        };
    }
}
