using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Persistance CHIFFRÉE des jetons OAuth de Chronos (%APPDATA%\Chronos\oauth.dat), via DPAPI
/// (<see cref="ProtectedData"/>, portée <see cref="DataProtectionScope.CurrentUser"/>) — lisible
/// uniquement par le compte Windows courant, aucun droit admin, aucune dépendance native.
///
/// SÉCURITÉ : les jetons ne transitent en clair qu'en mémoire, le temps de (dé)chiffrer. Toute
/// anomalie de lecture/déchiffrement → null (jamais d'exception, jamais de trace du secret).
/// </summary>
public sealed class ChronosOAuthStore
{
    private readonly string _path;

    public ChronosOAuthStore(string? path = null)
        => _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chronos", "oauth.dat");

    /// <summary>Un jeton est-il enregistré (l'utilisateur s'est-il connecté) ?</summary>
    public bool Exists => File.Exists(_path);

    /// <summary>Chiffre et enregistre les jetons (écriture atomique).</summary>
    public void Save(OAuthTokens tokens)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tokens));
        var enc = ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);
        var tmp = _path + ".tmp-" + Environment.ProcessId;
        File.WriteAllBytes(tmp, enc);
        File.Move(tmp, _path, overwrite: true);
    }

    /// <summary>Déchiffre et lit les jetons. Fichier absent/corrompu/illisible → null.</summary>
    public OAuthTokens? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var enc = File.ReadAllBytes(_path);
            var plain = ProtectedData.Unprotect(enc, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<OAuthTokens>(Encoding.UTF8.GetString(plain));
        }
        catch { return null; }
    }

    /// <summary>Supprime les jetons (déconnexion).</summary>
    public void Clear()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { /* best-effort */ }
    }
}
