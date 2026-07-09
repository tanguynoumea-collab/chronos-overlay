using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Déchiffre l'access token OAuth du coffre safeStorage de l'app bureau Claude et le renvoie EN
/// MÉMOIRE seulement (TOK-01/03). Schéma prouvé (10-RESEARCH.md) :
///
///   Local State (JSON) → os_crypt.encrypted_key (base64) → retirer préfixe "DPAPI" (5o)
///     → ProtectedData.Unprotect(CurrentUser) → 32o = clé AES-256.
///   config.json (JSON) → oauth:tokenCache (base64) → préfixe "v10" (3o) + nonce 12o + cipher + tag 16o
///     → AES-256-GCM → JSON MAP → 1re entrée dont la clé contient "claude_code" → champ `token`.
///
/// SÉCURITÉ (NON NÉGOCIABLE) : le token/clé en clair ne vit qu'en variables locales ; JAMAIS logué,
/// écrit, mis en exception ou concaténé. Fichiers ouverts en <see cref="FileAccess.Read"/> STRICT
/// (aucune réécriture de %APPDATA%/Claude). Tolérance TOTALE (TOK-02) : toute anomalie → null, jamais
/// d'exception non gérée, et le détail de l'exception n'est JAMAIS journalisé (fragments sensibles).
/// </summary>
public sealed class ClaudeTokenReader : IClaudeTokenReader
{
    private readonly string _configJsonPath;
    private readonly string _localStatePath;
    private readonly string _credentialsPath;   // repli Claude Code CLI (~/.claude/.credentials.json)

    /// <summary>Construit un reader sur des chemins explicites (injectables pour la testabilité).
    /// <paramref name="credentialsPath"/> = coffre du CLI (repli) ; si null, ~/.claude/.credentials.json.</summary>
    public ClaudeTokenReader(string configJsonPath, string localStatePath, string? credentialsPath = null)
    {
        _configJsonPath = configJsonPath;
        _localStatePath = localStatePath;
        _credentialsPath = credentialsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");
    }

    /// <summary>Reader par défaut : coffre app bureau (config.json/Local State) + repli CLI (.credentials.json).</summary>
    public static ClaudeTokenReader Default()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new ClaudeTokenReader(
            Path.Combine(appData, "Claude", "config.json"),
            Path.Combine(appData, "Claude", "Local State"));
    }

    /// <summary>Le fichier de credentials CLI existe-t-il ? (pour le diagnostic — pas le token).</summary>
    public bool HasCliCredentials => File.Exists(_credentialsPath);

    /// <inheritdoc/>
    public string? TryReadAccessToken(out DateTimeOffset? expiresAt)
    {
        // 1) Coffre de l'app BUREAU Claude (safeStorage v10 / DPAPI).
        var desktop = TryReadDesktopToken(out expiresAt);
        if (!string.IsNullOrEmpty(desktop)) return desktop;

        // 2) Repli : token de Claude Code CLI (~/.claude/.credentials.json, clé claudeAiOauth, EN CLAIR).
        //    Couvre les machines qui utilisent le CLI sans l'app bureau (config.json absent).
        return TryReadCliToken(out expiresAt);
    }

    // Repli CLI : claudeAiOauth.accessToken (chaîne en clair) + expiresAt (epoch ms OU ISO). Mémoire seule.
    private string? TryReadCliToken(out DateTimeOffset? expiresAt)
    {
        expiresAt = null;
        try
        {
            if (!File.Exists(_credentialsPath)) return null;
            using var fs = new FileStream(_credentialsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var doc = JsonDocument.Parse(fs);
            if (!doc.RootElement.TryGetProperty("claudeAiOauth", out var o) || o.ValueKind != JsonValueKind.Object)
                return null;
            if (!o.TryGetProperty("accessToken", out var at) || at.ValueKind != JsonValueKind.String)
                return null;
            var token = at.GetString();
            if (string.IsNullOrEmpty(token)) return null;
            if (o.TryGetProperty("expiresAt", out var e))
            {
                if (e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var ms))
                    expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(ms);
                else if (e.ValueKind == JsonValueKind.String
                         && DateTimeOffset.TryParse(e.GetString(), CultureInfo.InvariantCulture,
                              DateTimeStyles.RoundtripKind, out var d))
                    expiresAt = d;
            }
            return token; // token en mémoire seulement — jamais logué/écrit
        }
        catch (Exception) { expiresAt = null; return null; }
    }

    // Coffre app bureau (schéma safeStorage v10 / DPAPI) — inchangé.
    private string? TryReadDesktopToken(out DateTimeOffset? expiresAt)
    {
        expiresAt = null;
        try
        {
            // 1) Clé AES depuis Local State (LECTURE SEULE) → os_crypt.encrypted_key (base64).
            byte[] encryptedKey;
            using (var ls = new FileStream(_localStatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var lsDoc = JsonDocument.Parse(ls))
            {
                if (!lsDoc.RootElement.TryGetProperty("os_crypt", out var osCrypt)
                    || !osCrypt.TryGetProperty("encrypted_key", out var ek)
                    || ek.ValueKind != JsonValueKind.String)
                    return null;
                encryptedKey = Convert.FromBase64String(ek.GetString()!);
            }

            // Retirer le préfixe ASCII "DPAPI" (5o) puis dé-envelopper via DPAPI (compte courant) → clé 32o.
            byte[] aesKey = ProtectedData.Unprotect(encryptedKey[5..], null, DataProtectionScope.CurrentUser);

            // 2) Blob v10 depuis config.json (LECTURE SEULE) → oauth:tokenCache (base64).
            string tokenCacheB64;
            using (var cfg = new FileStream(_configJsonPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var cfgDoc = JsonDocument.Parse(cfg))
            {
                if (!cfgDoc.RootElement.TryGetProperty("oauth:tokenCache", out var tc)
                    || tc.ValueKind != JsonValueKind.String)
                    return null;
                tokenCacheB64 = tc.GetString()!;
            }

            // 3) Déchiffrement + sélection de l'entrée claude_code (cœur testable).
            return DecryptAndSelectToken(aesKey, tokenCacheB64, out expiresAt);
        }
        catch (Exception)
        {
            // Fichier absent/corrompu, DPAPI échoué, base64/JSON invalide… → « pas de token ».
            // NE PAS journaliser l'exception (elle pourrait contenir des fragments sensibles).
            expiresAt = null;
            return null;
        }
    }

    /// <summary>
    /// Cœur déchiffrement isolé et testable (blob v10 → clé AES → MAP → champ `token`). Schéma
    /// RESEARCH verbatim. Toute anomalie crypto/JSON → null, jamais d'exception (TOK-02).
    /// </summary>
    internal static string? DecryptAndSelectToken(byte[] aesKey, string tokenCacheB64, out DateTimeOffset? expiresAt)
    {
        expiresAt = null;
        try
        {
            byte[] blob = Convert.FromBase64String(tokenCacheB64);
            if (blob.Length < 3 + 12 + 16) return null;          // trop court pour "v10" + nonce + tag
            byte[] nonce = blob[3..15];                          // 12o après le préfixe "v10"
            byte[] tag = blob[^16..];                            // 16o finaux
            byte[] cipher = blob[15..^16];
            byte[] plain = new byte[cipher.Length];
            using (var gcm = new AesGcm(aesKey, 16))
                gcm.Decrypt(nonce, cipher, tag, plain);          // tag invalide → CryptographicException

            string json = Encoding.UTF8.GetString(plain);
            using var doc = JsonDocument.Parse(json);            // MAP par scope
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                if (!entry.Name.Contains("claude_code")) continue;
                if (entry.Value.ValueKind != JsonValueKind.Object) continue;
                if (entry.Value.TryGetProperty("token", out var t) && t.ValueKind == JsonValueKind.String)
                {
                    if (entry.Value.TryGetProperty("expiresAt", out var e)
                        && e.ValueKind == JsonValueKind.String
                        && DateTimeOffset.TryParse(e.GetString(), CultureInfo.InvariantCulture,
                             DateTimeStyles.RoundtripKind, out var d))
                        expiresAt = d;
                    return t.GetString();                        // token en mémoire seulement
                }
            }
            return null;                                          // aucune entrée claude_code exploitable
        }
        catch (Exception)
        {
            // GCM/base64/JSON → null (Pitfall 5). Aucun détail journalisé.
            expiresAt = null;
            return null;
        }
    }
}
