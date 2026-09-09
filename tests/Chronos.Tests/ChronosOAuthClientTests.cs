using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve le cœur PUR du client OAuth de Chronos : génération PKCE conforme S256 (challenge =
/// base64url(SHA256(verifier))), URL d'autorisation bien formée (client_id, S256, redirect, scope,
/// state, code=true), et découpe correcte du code renvoyé au format « code#state ».
/// </summary>
public class ChronosOAuthClientTests
{
    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public void CreatePkce_challenge_est_bien_le_S256_du_verifier()
    {
        var (verifier, challenge, state) = ChronosOAuthClient.CreatePkce();

        var expected = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        Assert.Equal(expected, challenge);

        // base64url : ni '+', ni '/', ni padding '='.
        foreach (var s in new[] { verifier, challenge, state })
        {
            Assert.DoesNotContain('+', s);
            Assert.DoesNotContain('/', s);
            Assert.DoesNotContain('=', s);
            Assert.True(s.Length >= 20);
        }
    }

    [Fact]
    public void CreatePkce_produit_des_valeurs_distinctes_a_chaque_appel()
    {
        var a = ChronosOAuthClient.CreatePkce();
        var b = ChronosOAuthClient.CreatePkce();
        Assert.NotEqual(a.verifier, b.verifier);
        Assert.NotEqual(a.state, b.state);
    }

    [Fact]
    public void BuildAuthorizeUrl_contient_les_parametres_attendus()
    {
        var url = ChronosOAuthClient.BuildAuthorizeUrl("CHAL", "STATE");

        Assert.StartsWith("https://claude.ai/oauth/authorize?", url);
        Assert.Contains("client_id=" + ChronosOAuthClient.ClientId, url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("code_challenge=CHAL", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("state=STATE", url);
        Assert.Contains("code=true", url);
        Assert.Contains("redirect_uri=" + Uri.EscapeDataString("https://console.anthropic.com/oauth/code/callback"), url);
        Assert.Contains("scope=" + Uri.EscapeDataString("user:inference user:profile"), url);
    }

    [Theory]
    [InlineData("abc#xyz", "abc", "xyz")]     // le serveur accole « code#state »
    [InlineData("  abc#xyz  ", "abc", "xyz")] // espaces autour → nettoyés
    [InlineData("justcode", "justcode", "FB")] // sans '#' → state de repli conservé
    public void SplitCodeState_decoupe_correctement(string pasted, string code, string state)
    {
        var (c, s) = ChronosOAuthClient.SplitCodeState(pasted, "FB");
        Assert.Equal(code, c);
        Assert.Equal(state, s);
    }

    // ------------------------------------------------------------------------------------------
    // RefreshAsync — couverture posée AVANT réécriture (Wave 0 de la phase 17).
    //
    // SÉCURITÉ — non négociable : « REF-VIEUX-BIDON » est une valeur INVENTÉE. Aucun test de ce dépôt
    // ne doit lire, déchiffrer ni envoyer le refresh token réel de %APPDATA%\Chronos\oauth.dat :
    // l'endpoint fait TOURNER le refresh token, et un appel dont le résultat n'est pas re-sauvegardé
    // INVALIDERAIT DÉFINITIVEMENT le login de l'utilisateur. Tout passe par FakeHttpMessageHandler.
    // ------------------------------------------------------------------------------------------

    private const string CorpsNominal = """{"access_token":"ACC-2","refresh_token":"REF-2","expires_in":3600}""";

    // 200 dont le corps s'interrompt au milieu : la rotation a DÉJÀ eu lieu côté serveur, mais le
    // nouveau refresh token est perdu. Écrit en littéral échappé (pas en raw string) pour lever
    // toute ambiguïté sur le comptage des guillemets de fermeture.
    private const string CorpsTronque = "{\"access_token\":\"ACC-2\"";

    /// <summary>
    /// Helper UNIQUE de construction : aucun réseau réel, aucun coffre. Le plan 17-02 ne changera
    /// que la ligne de retour quand RefreshAsync deviendra typé (ResultatRafraichissement).
    /// </summary>
    private static async Task<OAuthTokens?> RefreshAvec(FakeHttpMessageHandler handler)
    {
        var client = new ChronosOAuthClient(new HttpClient(handler));
        return await client.RefreshAsync("REF-VIEUX-BIDON");
    }

    [Fact]
    public async Task Refresh_200_rend_des_jetons_ET_fait_tourner_le_refresh_token()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsNominal);

        var jetons = await RefreshAvec(handler);

        Assert.NotNull(jetons);
        Assert.Equal("ACC-2", jetons!.AccessToken);
        Assert.Equal("REF-2", jetons.RefreshToken);            // ROTATION : ce n'est plus REF-VIEUX-BIDON
        Assert.NotEqual("REF-VIEUX-BIDON", jetons.RefreshToken);
        Assert.InRange(jetons.ExpiresAt,
            DateTimeOffset.UtcNow.AddMinutes(50), DateTimeOffset.UtcNow.AddMinutes(70));
        Assert.Equal(1, handler.SendCount);
    }

    [Fact]
    public async Task Refresh_200_sans_refresh_token_est_rejete()
    {
        // ChronosOAuthClient.cs:114 — access ET refresh sont obligatoires. Un 200 amputé du refresh
        // n'est pas exploitable : sans nouveau refresh, l'appel SUIVANT échouerait (rotation).
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"access_token":"ACC-2","expires_in":3600}""");

        Assert.Null(await RefreshAvec(handler));
    }

    [Fact]
    public async Task Refresh_200_a_corps_tronque_ne_leve_pas()
    {
        var ex = await Record.ExceptionAsync(async () =>
            await RefreshAvec(FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsTronque)));

        Assert.Null(ex);
        Assert.Null(await RefreshAvec(FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsTronque)));
    }

    [Fact]
    public async Task Refresh_exception_reseau_ne_leve_pas()
    {
        var ex = await Record.ExceptionAsync(async () =>
            await RefreshAvec(FakeHttpMessageHandler.Throws(new HttpRequestException("réseau"))));

        Assert.Null(ex);
        Assert.Null(await RefreshAvec(FakeHttpMessageHandler.Throws(new HttpRequestException("réseau"))));
    }

    [Fact]
    public async Task Refresh_timeout_ne_leve_pas()
    {
        var ex = await Record.ExceptionAsync(async () =>
            await RefreshAvec(FakeHttpMessageHandler.Throws(new TaskCanceledException())));

        Assert.Null(ex);
        Assert.Null(await RefreshAvec(FakeHttpMessageHandler.Throws(new TaskCanceledException())));
    }

    /// <summary>
    /// DÉFAUT DOCUMENTÉ (état AVANT la phase 17). Le <c>catch { return null; }</c> de PostTokenAsync
    /// (ChronosOAuthClient.cs:119), doublé du <c>if (!resp.IsSuccessStatusCode) return null;</c>
    /// (ligne 106), confond cinq causes qui appellent des réactions OPPOSÉES :
    /// « refresh token révoqué » (seule une reconnexion répare), « identifiants rejetés »,
    /// « rate limité » (attendre suffit), « panne serveur » (réessayer), « corps illisible après une
    /// rotation déjà faite côté serveur » (irréparable). Sans cause, TOK-02 — rendre la panne visible
    /// et actionnable — est mécaniquement impossible : on ne peut pas afficher ce qui a été détruit.
    /// Le plan 17-02 RÉÉCRIT ce test (il ne le supprime pas) avec un résultat typé par cause.
    /// </summary>
    [Theory]
    [InlineData(400, """{"error":"invalid_grant"}""")]              // identifiants révoqués
    [InlineData(401, """{"error":"invalid_client"}""")]             // identifiants rejetés
    [InlineData(429, """{"error":{"type":"rate_limit_error"}}""")]  // TEMPORAIRE — mode d'échec RÉEL
    [InlineData(500, "")]                                           // panne serveur
    [InlineData(200, CorpsTronque)]                                 // rotation déjà faite, corps perdu
    public async Task Toutes_les_causes_d_echec_se_confondent_en_un_seul_null(int statut, string corps)
    {
        var jetons = await RefreshAvec(FakeHttpMessageHandler.Json((HttpStatusCode)statut, corps));

        Assert.Null(jetons);   // <- LE DÉFAUT : aucune de ces cinq situations n'est distinguable
    }

    [Fact]
    public async Task Le_refresh_token_ne_transite_JAMAIS_par_l_URL()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsNominal);

        await RefreshAvec(handler);

        var url = handler.LastRequest!.RequestUri!.ToString();
        Assert.Equal("https://console.anthropic.com/v1/oauth/token", url);
        Assert.DoesNotContain("REF-VIEUX-BIDON", url);
        Assert.DoesNotContain("ACC-2", url);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
    }
}
