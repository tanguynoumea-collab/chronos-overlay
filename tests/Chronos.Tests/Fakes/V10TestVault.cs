using System.Security.Cryptography;
using System.Text;

namespace Chronos.Tests;

/// <summary>Fabrique un blob safeStorage v10 (préfixe "v10" + nonce 12o + cipher + tag 16o)
/// chiffré AES-256-GCM par une CLÉ DE TEST connue — pour prouver le déchiffrement du reader
/// sans jamais toucher le vrai coffre ni un vrai token.</summary>
internal static class V10TestVault
{
    /// <summary>Clé AES-256 de test (32 octets fixes, publique, sans valeur réelle).</summary>
    public static byte[] TestKey => Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    /// <summary>Chiffre <paramref name="plaintextJson"/> en un blob v10 base64 (schéma RESEARCH :
    /// "v10" + nonce + cipher + tag).</summary>
    public static string MakeTokenCacheB64(string plaintextJson, byte[]? key = null)
    {
        key ??= TestKey;
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);
        var plain = Encoding.UTF8.GetBytes(plaintextJson);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using (var gcm = new AesGcm(key, 16))
            gcm.Encrypt(nonce, plain, cipher, tag);
        var blob = new byte[3 + 12 + cipher.Length + 16];
        Encoding.ASCII.GetBytes("v10").CopyTo(blob, 0);
        nonce.CopyTo(blob, 3);
        cipher.CopyTo(blob, 15);
        tag.CopyTo(blob, 15 + cipher.Length);
        return Convert.ToBase64String(blob);
    }
}
