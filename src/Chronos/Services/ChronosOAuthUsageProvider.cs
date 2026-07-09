using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Provider EXACT via le jeton PROPRE de Chronos (login OAuth intégré). Contrairement à
/// <see cref="ClaudeOAuthUsageProvider"/> (qui récupérait le jeton d'une autre app), celui-ci lit ses
/// jetons dans <see cref="ChronosOAuthStore"/>, les RAFRAÎCHIT au besoin via <see cref="ChronosOAuthClient"/>,
/// puis appelle GET /api/oauth/usage. Fonctionne quel que soit le mode d'usage de Claude (bureau ou terminal).
///
/// En-têtes requis (vérifiés) : Authorization: Bearer, anthropic-beta: oauth-2025-04-20, et
/// User-Agent: claude-code/&lt;version&gt; (sinon 429 agressifs). Anti-429 : au plus un appel /
/// <see cref="MinInterval"/>, recul fort sur 429, cache du dernier exact (staleness honnête via SourceCapturedAt).
///
/// Pas connecté (aucun jeton) → <see cref="UsageSnapshot.Empty"/> : le composite bascule proprement sur
/// le repli (pont statusLine puis estimation). Tolérance totale : jamais d'exception, jamais de valeur inventée.
/// </summary>
public sealed class ChronosOAuthUsageProvider : IUsageProvider
{
    private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";
    private const string UserAgent = "claude-code/2.1.30";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan Backoff429 = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CacheUsable = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private readonly ChronosOAuthStore _store;
    private readonly ChronosOAuthClient _client;
    private readonly HttpClient _http;
    private readonly IClock _clock;

    private UsageSnapshot? _cached;
    private DateTimeOffset _cachedAt;
    private DateTimeOffset _nextAllowedCall;

    public ChronosOAuthUsageProvider(ChronosOAuthStore store, ChronosOAuthClient client, HttpClient http, IClock clock)
    {
        _store = store;
        _client = client;
        _http = http;
        _clock = clock;
    }

    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        if (now < _nextAllowedCall && _cached is not null)
            return _cached;

        var tokens = _store.Load();
        if (tokens is null)
            return ServeCachedOr(UsageSnapshot.Empty, now); // pas connecté → repli (sans bloquer les rappels)

        // Rafraîchissement silencieux si l'access token est expiré ou proche de l'être.
        if (tokens.ExpiresAt - RefreshMargin <= now)
        {
            var refreshed = await _client.RefreshAsync(tokens.RefreshToken, ct);
            if (refreshed is null)
            {
                // Refresh échoué → on tente quand même l'appel avec le jeton courant (peut encore marcher),
                // mais on ne martèle pas le réseau.
                _nextAllowedCall = now + MinInterval;
            }
            else
            {
                tokens = refreshed;
                _store.Save(tokens); // rotation : mémoriser le nouveau refresh_token
            }
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);

            using var req = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + tokens.AccessToken);
            req.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
            req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            using var resp = await _http.SendAsync(req, cts.Token);

            if ((int)resp.StatusCode == 429)
            {
                _nextAllowedCall = now + Backoff429;
                return ServeCachedOr(UsageSnapshot.Empty, now);
            }
            if (!resp.IsSuccessStatusCode)
            {
                _nextAllowedCall = now + MinInterval;
                return ServeCachedOr(UsageSnapshot.Empty, now);
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
            var root = doc.RootElement;

            var snap = new UsageSnapshot
            {
                FiveHour = Read(root, "five_hour", WindowKind.FiveHour, TimeSpan.FromHours(5)),
                SevenDay = Read(root, "seven_day", WindowKind.SevenDay, TimeSpan.FromDays(7)),
                SourceCapturedAt = now,
            };
            _cached = snap; _cachedAt = now; _nextAllowedCall = now + MinInterval;
            return snap;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                     or OperationCanceledException or JsonException or IOException)
        {
            _nextAllowedCall = now + MinInterval;
            return ServeCachedOr(UsageSnapshot.Empty, now);
        }
    }

    private UsageSnapshot ServeCachedOr(UsageSnapshot fallback, DateTimeOffset now)
        => _cached is not null && (now - _cachedAt) < CacheUsable ? _cached : fallback;

    // Schéma /api/oauth/usage : utilization 0..100, resets_at ISO 8601 (PAS epoch). Fenêtre absente → Unavailable.
    private WindowState Read(JsonElement root, string name, WindowKind kind, TimeSpan len)
    {
        if (!root.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object)
            return WindowState.Unavailable(kind);

        double? util = w.TryGetProperty("utilization", out var u) && u.TryGetDouble(out var p) ? p / 100.0 : null;
        DateTimeOffset? reset = w.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(r.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
            ? d : null;

        return new WindowState
        {
            Kind = kind,
            Utilization = util,
            ResetsAt = reset,
            Reliability = SourceReliability.Exact,
            FractionTimeRemaining = WindowState.FractionRemaining(reset, _clock.UtcNow, len),
        };
    }
}
