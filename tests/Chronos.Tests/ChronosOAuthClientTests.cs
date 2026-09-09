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
    /// Helper UNIQUE de construction : aucun réseau réel, aucun coffre. Seule sa ligne de retour a
    /// changé au plan 17-02, quand RefreshAsync est devenu typé (ResultatRafraichissement).
    /// </summary>
    private static async Task<ResultatRafraichissement> RefreshAvec(FakeHttpMessageHandler handler, IClock? horloge = null)
    {
        var client = new ChronosOAuthClient(new HttpClient(handler), horloge);
        return await client.RefreshAsync("REF-VIEUX-BIDON");
    }

    [Fact]
    public async Task Refresh_200_rend_des_jetons_ET_fait_tourner_le_refresh_token()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsNominal);
        // Horloge figée : ExpiresAt devient vérifiable à la seconde près, l'InRange approximatif
        // de la Wave 0 n'a plus lieu d'être.
        var horloge = new FakeClock(new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero));

        var res = await RefreshAvec(handler, horloge);

        Assert.Equal(IssueRafraichissement.Succes, res.Issue);
        Assert.NotNull(res.Jetons);
        Assert.Equal("ACC-2", res.Jetons!.AccessToken);
        Assert.Equal("REF-2", res.Jetons.RefreshToken);            // ROTATION : ce n'est plus REF-VIEUX-BIDON
        Assert.NotEqual("REF-VIEUX-BIDON", res.Jetons.RefreshToken);
        Assert.Equal(horloge.UtcNow.AddSeconds(3600), res.Jetons.ExpiresAt);
        Assert.Equal(1, handler.SendCount);
    }

    [Fact]
    public async Task Refresh_200_sans_refresh_token_est_rejete()
    {
        // access ET refresh sont obligatoires. Un 200 amputé du refresh n'est pas exploitable : le
        // serveur a déjà roulé le jeton de son côté, l'ancien est mort — réessayer est vain.
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"access_token":"ACC-2","expires_in":3600}""");

        var res = await RefreshAvec(handler);

        Assert.Equal(IssueRafraichissement.IdentifiantsRejetes, res.Issue);
        Assert.Null(res.Jetons);
    }

    [Fact]
    public async Task Refresh_200_a_corps_tronque_ne_leve_pas()
    {
        var ex = await Record.ExceptionAsync(async () =>
            await RefreshAvec(FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsTronque)));

        Assert.Null(ex);

        var res = await RefreshAvec(FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsTronque));
        Assert.Equal(IssueRafraichissement.IdentifiantsRejetes, res.Issue);
        Assert.Null(res.Jetons);
    }

    [Fact]
    public async Task Refresh_exception_reseau_ne_leve_pas()
    {
        var ex = await Record.ExceptionAsync(async () =>
            await RefreshAvec(FakeHttpMessageHandler.Throws(new HttpRequestException("réseau"))));

        Assert.Null(ex);

        // Wifi coupé : le jeton est peut-être parfaitement bon. Surtout pas « déconnecté ».
        var res = await RefreshAvec(FakeHttpMessageHandler.Throws(new HttpRequestException("réseau")));
        Assert.Equal(IssueRafraichissement.EchecTemporaire, res.Issue);
        Assert.Null(res.Jetons);
    }

    [Fact]
    public async Task Refresh_timeout_ne_leve_pas()
    {
        var ex = await Record.ExceptionAsync(async () =>
            await RefreshAvec(FakeHttpMessageHandler.Throws(new TaskCanceledException())));

        Assert.Null(ex);

        var res = await RefreshAvec(FakeHttpMessageHandler.Throws(new TaskCanceledException()));
        Assert.Equal(IssueRafraichissement.EchecTemporaire, res.Issue);
        Assert.Null(res.Jetons);
    }

    /// <summary>
    /// TOK-02 — RÉÉCRITURE du [Theory] de la Wave 0 (plan 17-01, commit cef82e1), qui prouvait que les
    /// cinq causes se confondaient en un seul null. Chaque cause appelle désormais une réaction différente : reconnexion
    /// pour l'une, patience pour l'autre. Le cas 429 est le plus important : c'est un mode d'échec
    /// RÉEL et vérifié du point de terminaison de jeton — le classer « déconnecté » afficherait une
    /// fausse alerte sur un compte parfaitement sain.
    /// </summary>
    [Theory]
    [InlineData(400, """{"error":"invalid_grant"}""",              IssueRafraichissement.IdentifiantsRejetes)]
    [InlineData(401, """{"error":"invalid_client"}""",             IssueRafraichissement.IdentifiantsRejetes)]
    [InlineData(429, """{"error":{"type":"rate_limit_error"}}""",  IssueRafraichissement.EchecTemporaire)]
    [InlineData(500, "",                                           IssueRafraichissement.EchecTemporaire)]
    [InlineData(200, CorpsTronque,                                 IssueRafraichissement.IdentifiantsRejetes)]
    [InlineData(200, """{"access_token":"ACC-2"}""",               IssueRafraichissement.IdentifiantsRejetes)]
    public async Task Chaque_cause_d_echec_est_desormais_distinguee(int statut, string corps, IssueRafraichissement attendue)
    {
        var res = await RefreshAvec(FakeHttpMessageHandler.Json((HttpStatusCode)statut, corps));

        Assert.Equal(attendue, res.Issue);
        Assert.Null(res.Jetons);
    }

    /// <summary>SÉCURITÉ : impossible de faire fuiter un corps HTTP ou un message d'exception par ce
    /// type — il ne contient aucune propriété texte. Ce test échoue si quelqu'un ajoute un champ.</summary>
    [Fact]
    public void Le_resultat_de_rafraichissement_ne_peut_transporter_aucun_texte()
    {
        var proprietes = typeof(ResultatRafraichissement).GetProperties();

        Assert.DoesNotContain(proprietes, p => p.PropertyType == typeof(string));
        Assert.Equal(2, proprietes.Length);   // Issue + Jetons, rien d'autre
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
