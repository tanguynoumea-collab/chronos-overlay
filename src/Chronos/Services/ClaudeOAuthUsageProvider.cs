using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Provider EXACT via l'endpoint OAuth privé de l'app bureau Claude (API-01/02/03) : lit l'access token
/// via <see cref="IClaudeTokenReader"/>, appelle GET https://api.anthropic.com/api/oauth/usage et mappe
/// la réponse RÉELLE (five_hour/seven_day à la racine, utilization 0..100, resets_at ISO 8601) en
/// <see cref="UsageSnapshot"/> marqué <see cref="SourceReliability.Exact"/>.
///
/// SÉCURITÉ (non négociable) : le token ne vit qu'en variable locale, le temps de construire l'en-tête
/// Authorization: Bearer. JAMAIS logué, écrit, mis en exception, ni concaténé dans l'URL (constante).
///
/// TOLÉRANCE TOTALE (API-02) : 401/403/5xx, réseau (HttpRequestException), timeout/annulation
/// (TaskCanceledException/OperationCanceledException), JSON malformé (JsonException) → UsageSnapshot.Empty,
/// jamais d'exception non gérée, jamais de valeur inventée.
///
/// INERTIE (API-03) : token null OU expiresAt &lt; now → aucun appel HTTP (court-circuit). L'appel
/// respecte le CancellationToken de l'appelant et applique un timeout court (5 s) indépendant du tick.
/// Provider NEUTRE : aucun type WPF.
/// </summary>
public sealed class ClaudeOAuthUsageProvider : IUsageProvider
{
    // Seule destination réseau autorisée. Constante : le token n'y apparaît JAMAIS.
    private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // Anti-429 (bug « infos étranges ponctuelles ») : l'endpoint /usage limite la fréquence des appels.
    // On l'interroge donc AU PLUS une fois toutes les MinInterval, on recule FORT sur un 429 (Backoff429),
    // et on CONSERVE le dernier snapshot exact (CacheUsable) en cas d'échec → l'affichage ne clignote plus
    // entre exact et estimé. L'ancienneté reste honnête via SourceCapturedAt (staleness).
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan Backoff429  = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CacheUsable = TimeSpan.FromMinutes(15);

    private readonly IClaudeTokenReader _tokenReader;
    private readonly HttpClient _http;
    private readonly IClock _clock;

    private UsageSnapshot? _cached;          // dernier appel EXACT réussi (SourceCapturedAt = son heure)
    private DateTimeOffset _cachedAt;        // quand il a été récupéré
    private DateTimeOffset _nextAllowedCall; // avant cette heure : servir le cache sans appeler le réseau

    public ClaudeOAuthUsageProvider(IClaudeTokenReader tokenReader, HttpClient http, IClock clock)
    {
        _tokenReader = tokenReader;
        _http = http;
        _clock = clock;
    }

    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        // Throttle : tant qu'on n'a pas le droit de rappeler l'API, servir le cache (s'il existe) sans réseau.
        if (now < _nextAllowedCall && _cached is not null)
            return _cached;

        // Token en variable locale UNIQUEMENT (jamais logué/écrit/exposé).
        var token = _tokenReader.TryReadAccessToken(out var expiresAt);
        if (string.IsNullOrEmpty(token) || (expiresAt is { } exp && exp < now))
        {
            _nextAllowedCall = now + MinInterval;                          // inerte : token absent/expiré (API-03)
            return ServeCachedOr(UsageSnapshot.Empty, now);
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);                                      // timeout court 5 s, indépendant du tick 1 s

            using var req = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token); // seule sortie du token
            req.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
            req.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            req.Headers.TryAddWithoutValidation("User-Agent", "Chronos/1.2");

            using var resp = await _http.SendAsync(req, cts.Token);

            if ((int)resp.StatusCode == 429)                              // rate limité → reculer FORT + garder l'exact
            {
                _nextAllowedCall = now + Backoff429;
                return ServeCachedOr(UsageSnapshot.Empty, now);
            }
            if (!resp.IsSuccessStatusCode)                               // 401/403/5xx → indisponible (API-02)
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
            _cached = snap; _cachedAt = now; _nextAllowedCall = now + MinInterval; // mémorise l'exact frais
            return snap;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                     or OperationCanceledException or JsonException or IOException)
        {
            // Réseau / timeout / annulation / malformé → garder l'exact récent, jamais de crash (API-02/03).
            _nextAllowedCall = now + MinInterval;
            return ServeCachedOr(UsageSnapshot.Empty, now);
        }
    }

    // Sur échec : renvoie le dernier snapshot exact s'il est encore assez récent, sinon l'indisponible.
    // Évite le clignotement exact↔estimé tout en laissant la staleness (SourceCapturedAt) vieillir la donnée.
    private UsageSnapshot ServeCachedOr(UsageSnapshot fallback, DateTimeOffset now)
        => _cached is not null && (now - _cachedAt) < CacheUsable ? _cached : fallback;

    // Lit UNE fenêtre du schéma OAuth : fenêtre absente/non-objet → Unavailable ;
    // utilization 0..100 → /100 (Pitfall 3) ; resets_at ISO 8601 → DateTimeOffset.Parse RoundtripKind
    // (Pitfall 2 : PAS epoch secondes). Aucune valeur inventée.
    private WindowState Read(JsonElement root, string name, WindowKind kind, TimeSpan len)
    {
        if (!root.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object)
            return WindowState.Unavailable(kind);                          // fenêtre absente → Unavailable

        double? util = w.TryGetProperty("utilization", out var u) && u.TryGetDouble(out var p)
            ? p / 100.0 : null;                                            // 0..100 → 0..1
        DateTimeOffset? reset = w.TryGetProperty("resets_at", out var r)
            && r.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(r.GetString(), CultureInfo.InvariantCulture,
                 DateTimeStyles.RoundtripKind, out var d) ? d : null;      // ISO 8601 (offset + microsecondes)

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
