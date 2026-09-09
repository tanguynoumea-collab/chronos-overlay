using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>Jetons OAuth de Chronos (obtenus par SON PROPRE login). Neutre, sérialisable pour le coffre DPAPI.</summary>
public sealed record OAuthTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>Issue d'un rafraîchissement de jeton. Deux branches et pas trois : ce qui compte pour
/// l'utilisateur est « faut-il que je fasse quelque chose ? » (IdentifiantsRejetes) ou « faut-il
/// simplement attendre ? » (EchecTemporaire).</summary>
public enum IssueRafraichissement
{
    /// <summary>Jetons obtenus (avec rotation du refresh token).</summary>
    Succes,

    /// <summary>Le serveur a refusé les identifiants (RFC 6749 §5.2 invalid_grant / invalid_client), ou
    /// a répondu 200 avec un corps inexploitable — auquel cas la rotation a DÉJÀ eu lieu de son côté et
    /// l'ancien refresh token est définitivement mort. Aucun réessai ne peut aider : il faut un login.</summary>
    IdentifiantsRejetes,

    /// <summary>Réseau, timeout, 429 (rate_limit_error — mode d'échec RÉEL et vérifié de ce point de
    /// terminaison) ou 5xx. Le jeton est peut-être parfaitement bon : réessayer plus tard, avec recul.</summary>
    EchecTemporaire,
}

/// <summary>
/// Résultat d'un rafraîchissement. SÉCURITÉ — ce type ne transporte NI le corps HTTP, NI les en-têtes,
/// NI le message d'exception : un corps d'erreur peut échoïser la requête, donc le refresh token.
/// Seuls l'issue (deux valeurs d'échec) et les jetons obtenus en sortent. Ne JAMAIS y ajouter de champ texte.
/// </summary>
public sealed record ResultatRafraichissement(IssueRafraichissement Issue, OAuthTokens? Jetons);

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
    private readonly IClock _horloge;

    /// <param name="http">Client HTTP partagé (faux sous test, aucun réseau réel).</param>
    /// <param name="clock">Horloge injectable : rend ExpiresAt déterministe sous test. Par défaut
    /// l'horloge système — aucun site d'appel existant n'est cassé.</param>
    public ChronosOAuthClient(HttpClient http, IClock? clock = null)
    {
        _http = http;
        _horloge = clock ?? new SystemClock();
    }

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

    /// <summary>
    /// Rafraîchit les jetons via le refresh_token. Le serveur fait TOURNER le refresh token : celui du
    /// résultat remplace l'ancien et doit être persisté par l'appelant avant toute autre action.
    ///
    /// Rend une CAUSE et non un null muet : sans elle, distinguer « compte révoqué » de « wifi coupé »
    /// est impossible, et TOK-02 avec. SÉCURITÉ : ni le corps, ni les en-têtes, ni le message
    /// d'exception ne sortent d'ici.
    /// </summary>
    public async Task<ResultatRafraichissement> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var body = new { grant_type = "refresh_token", refresh_token = refreshToken, client_id = ClientId };
        HttpResponseMessage? resp = null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            resp = await _http.SendAsync(req, cts.Token);

            var code = (int)resp.StatusCode;
            // RFC 6749 §5.2 : invalid_grant / invalid_client => seule une reconnexion répare.
            if (code is 400 or 401) return new(IssueRafraichissement.IdentifiantsRejetes, null);
            // 429 rate_limit_error (constaté en direct sur ce point de terminaison), 5xx, 408…
            if (!resp.IsSuccessStatusCode) return new(IssueRafraichissement.EchecTemporaire, null);

            var jetons = await LireJetonsAsync(resp, cts.Token);
            // 200 + corps inexploitable : le serveur a DÉJÀ roulé le refresh token, l'ancien est mort et
            // le nouveau est perdu. Réessayer est vain — déconnexion honnête plutôt que boucle inutile.
            return jetons is null
                ? new(IssueRafraichissement.IdentifiantsRejetes, null)
                : new(IssueRafraichissement.Succes, jetons);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                     or OperationCanceledException or JsonException or IOException)
        {
            // Si une réponse 2xx avait déjà été reçue, la rotation a pu avoir lieu => même raisonnement.
            return resp is { IsSuccessStatusCode: true }
                ? new(IssueRafraichissement.IdentifiantsRejetes, null)
                : new(IssueRafraichissement.EchecTemporaire, null);
        }
        finally { resp?.Dispose(); }
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

            return await LireJetonsAsync(resp, cts.Token);
        }
        catch { return null; } // réseau/timeout/JSON → échec silencieux (l'appelant affiche l'erreur générique)
    }

    /// <summary>Lit le corps d'une réponse 2xx du point de terminaison de jeton. null = corps
    /// inexploitable (flux tronqué, JSON invalide, access_token ou refresh_token manquant). Un seul
    /// parsing pour les deux chemins (échange de code ET rafraîchissement) : ils ne peuvent pas diverger.</summary>
    private async Task<OAuthTokens?> LireJetonsAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
        var root = doc.RootElement;

        var access = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
        var refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh)) return null;

        var expiresIn = root.TryGetProperty("expires_in", out var e) && e.TryGetInt64(out var secs) ? secs : 3600;
        return new OAuthTokens(access!, refresh!, _horloge.UtcNow.AddSeconds(expiresIn));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
