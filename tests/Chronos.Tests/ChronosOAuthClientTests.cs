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
}
