using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>Jetons OAuth de Chronos (obtenus par SON PROPRE login). Neutre, sérialisable pour le coffre DPAPI.</summary>
public sealed record OAuthTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Client OAuth PKCE de Chronos : réalise le MÊME flux que <c>claude login</c> pour obtenir un jeton
/// PROPRE à Chronos (login navigateur une fois, puis refresh silencieux), au lieu de récupérer celui
/// d'une autre app. Paramètres vérifiés (client public de Claude Code) :
///   authorize : https://claude.ai/oauth/authorize  · token/refresh : https://console.anthropic.com/v1/oauth/token
///   redirect  : https://console.anthropic.com/oauth/code/callback (flux « code à copier », code=true)
///   PKCE S256 · code renvoyé au format « code#state » (à re-découper).
///
/// SÉCURITÉ : le code_verifier et les jetons ne vivent qu'en mémoire ici ; la persistance chiffrée
/// (DPAPI) est déléguée à <see cref="ChronosOAuthStore"/>. Rien n'est journalisé.
/// </summary>
public sealed class ChronosOAuthClient
{
    public const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
    private const string AuthorizeUrl = "https://claude.ai/oauth/authorize";
    private const string TokenUrl = "https://console.anthropic.com/v1/oauth/token";
    private const string RedirectUri = "https://console.anthropic.com/oauth/code/callback";
    // user:inference (appeler l'usage) + user:profile (contexte abonnement, sinon 403). On évite
    // org:create_api_key qui casse le login sur les versions récentes (« Unknown scope »).
    private const string Scopes = "user:inference user:profile";

    private readonly HttpClient _http;

    public ChronosOAuthClient(HttpClient http) => _http = http;

    /// <summary>Génère (code_verifier, code_challenge S256, state) — tout en base64url sans padding.</summary>
    public static (string verifier, string challenge, string state) CreatePkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (verifier, challenge, state);
    }

    /// <summary>Construit l'URL d'autorisation (à ouvrir dans le navigateur).</summary>
    public static string BuildAuthorizeUrl(string challenge, string state)
    {
        var q = new Dictionary<string, string>
        {
            ["code"] = "true",                       // affiche le code à copier (flux hors-ligne)
            ["client_id"] = ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = RedirectUri,
            ["scope"] = Scopes,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
        };
        return AuthorizeUrl + "?" + string.Join("&", q.Select(kv => kv.Key + "=" + Uri.EscapeDataString(kv.Value)));
    }

    /// <summary>Échange le code collé (« code » ou « code#state ») contre des jetons. null si échec.</summary>
    public async Task<OAuthTokens?> ExchangeCodeAsync(string pastedCode, string verifier, string state, CancellationToken ct = default)
    {
        var (code, st) = SplitCodeState(pastedCode, state);
        var body = new
        {
            grant_type = "authorization_code",
            code,
            state = st,
            client_id = ClientId,
            redirect_uri = RedirectUri,
            code_verifier = verifier,
        };
        return await PostTokenAsync(body, ct);
    }

    /// <summary>Rafraîchit les jetons via le refresh_token (rotation : un nouveau refresh est renvoyé). null si échec.</summary>
    public async Task<OAuthTokens?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var body = new { grant_type = "refresh_token", refresh_token = refreshToken, client_id = ClientId };
        return await PostTokenAsync(body, ct);
    }

    // Découpe « code#state » (le serveur renvoie le state accolé). Sans '#', on garde le state d'origine.
    internal static (string code, string state) SplitCodeState(string pasted, string fallbackState)
    {
        var trimmed = (pasted ?? "").Trim();
        var i = trimmed.IndexOf('#');
        return i >= 0 ? (trimmed[..i], trimmed[(i + 1)..]) : (trimmed, fallbackState);
    }

    private async Task<OAuthTokens?> PostTokenAsync(object body, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            using var resp = await _http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;

            await using var s = await resp.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(s, cancellationToken: cts.Token);
            var root = doc.RootElement;

            var access = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
            var refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
            if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh)) return null;

            var expiresIn = root.TryGetProperty("expires_in", out var e) && e.TryGetInt64(out var secs) ? secs : 3600;
            return new OAuthTokens(access!, refresh!, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        }
        catch { return null; } // réseau/timeout/JSON → échec silencieux (l'appelant affiche l'erreur générique)
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
