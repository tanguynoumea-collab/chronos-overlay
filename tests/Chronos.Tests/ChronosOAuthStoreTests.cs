using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve le coffre OAuth chiffré (DPAPI) : round-trip Save→Load, chiffrement réel sur disque (le
/// jeton n'apparaît PAS en clair dans le fichier), tolérance (fichier absent/corrompu → null) et Clear.
/// </summary>
public class ChronosOAuthStoreTests
{
    private static string TempFile() => Path.Combine(Path.GetTempPath(), "chronos-oauth-" + Guid.NewGuid().ToString("N") + ".dat");

    [Fact]
    public void Save_puis_Load_restitue_les_jetons()
    {
        var path = TempFile();
        try
        {
            var store = new ChronosOAuthStore(path);
            Assert.False(store.Exists);

            var tokens = new OAuthTokens("ACCESS-abc", "REFRESH-xyz", new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
            store.Save(tokens);

            Assert.True(store.Exists);
            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Equal(tokens, loaded);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Le_fichier_sur_disque_est_chiffre_pas_de_jeton_en_clair()
    {
        var path = TempFile();
        try
        {
            new ChronosOAuthStore(path).Save(new OAuthTokens("ACCESS-SECRET-123", "REFRESH-SECRET-456", DateTimeOffset.UtcNow));
            var raw = File.ReadAllText(path);
            Assert.DoesNotContain("ACCESS-SECRET-123", raw);
            Assert.DoesNotContain("REFRESH-SECRET-456", raw);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Fichier_absent_ou_corrompu_retourne_null_sans_exception()
    {
        var path = TempFile();
        Assert.Null(new ChronosOAuthStore(path).Load()); // absent

        File.WriteAllText(path, "pas un blob DPAPI valide");
        try { Assert.Null(new ChronosOAuthStore(path).Load()); } // corrompu → null, pas d'exception
        finally { File.Delete(path); }
    }

    [Fact]
    public void Clear_supprime_le_coffre()
    {
        var path = TempFile();
        var store = new ChronosOAuthStore(path);
        store.Save(new OAuthTokens("a", "b", DateTimeOffset.UtcNow));
        Assert.True(store.Exists);
        store.Clear();
        Assert.False(store.Exists);
    }
}
