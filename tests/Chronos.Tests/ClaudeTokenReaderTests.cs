using System.Globalization;
using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve TOK-01 (déchiffrement d'un blob v10 synthétique : découpe nonce/tag, AES-256-GCM,
/// énumération de la MAP, sélection de l'entrée dont la clé contient "claude_code", lecture du
/// champ `token` et de `expiresAt`), TOK-02 (tolérance TOTALE : base64 invalide, blob trop court,
/// tag GCM faux, mauvaise clé, MAP sans claude_code, entrée sans token, fichiers absents → null
/// SANS exception) et TOK-03 (lecture seule prouvée : snapshot du répertoire coffre avant/après
/// l'appel → identique, aucun fichier créé/modifié).
///
/// Le cœur crypto est testé via <see cref="ClaudeTokenReader.DecryptAndSelectToken"/> (internal,
/// visible via InternalsVisibleTo) avec des blobs fabriqués par <see cref="V10TestVault"/> — clé de
/// TEST connue, JAMAIS le vrai coffre ni un vrai token. Tests PURS (aucun Dispatcher) → [Fact].
/// </summary>
public class ClaudeTokenReaderTests
{
    // Date d'expiration future déterministe (ISO 8601 avec offset, format du coffre réel).
    private const string ExpiryIso = "2027-01-01T00:00:00.0000000+00:00";

    private static string MapJson(string firstKey, string firstToken, string secondKey, string secondBody)
        => $$"""
        {
          "{{firstKey}}": { "token": "{{firstToken}}", "refreshToken": "R1", "expiresAt": "{{ExpiryIso}}" },
          "{{secondKey}}": {{secondBody}}
        }
        """;

    // --- TOK-01 : blob v10 synthétique → entrée claude_code → champ token + expiresAt ---

    [Fact]
    public void TOK01_Nominal_dechiffre_et_retourne_token_et_expiresAt()
    {
        // MAP à 2 entrées : la 1re (sans claude_code) et la 2e (avec claude_code) portent un token distinct.
        var json = MapJson(
            firstKey: "acc:org:https://api.anthropic.com:profile",
            firstToken: "AUTRE-TOKEN",
            secondKey: "acc:org:https://api.anthropic.com:user:inference:claude_code",
            secondBody: $$"""{ "token": "TOK-abc", "refreshToken": "R2", "expiresAt": "{{ExpiryIso}}" }""");
        var b64 = V10TestVault.MakeTokenCacheB64(json);

        var token = ClaudeTokenReader.DecryptAndSelectToken(V10TestVault.TestKey, b64, out var exp);

        Assert.Equal("TOK-abc", token);
        Assert.NotNull(exp);
        Assert.Equal(
            DateTimeOffset.Parse(ExpiryIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            exp!.Value);
    }

    [Fact]
    public void TOK01_Selection_ignore_les_entrees_sans_claude_code()
    {
        // 1re entrée SANS claude_code, 2e AVEC → doit retourner le token de la 2e.
        var json = MapJson(
            firstKey: "acc:org:https://api.anthropic.com:autre_scope",
            firstToken: "IGNORE",
            secondKey: "acc:org:https://api.anthropic.com:claude_code",
            secondBody: """{ "token": "TOK-selectionne" }""");
        var b64 = V10TestVault.MakeTokenCacheB64(json);

        var token = ClaudeTokenReader.DecryptAndSelectToken(V10TestVault.TestKey, b64, out var exp);

        Assert.Equal("TOK-selectionne", token);
        Assert.Null(exp); // pas d'expiresAt sur cette entrée → null, pas d'exception.
    }

    // --- TOK-02 : tolérance totale → null SANS exception ---

    [Fact]
    public void TOK02_Base64_invalide_retourne_null()
    {
        var token = ClaudeTokenReader.DecryptAndSelectToken(V10TestVault.TestKey, "!!!", out var exp);
        Assert.Null(token);
        Assert.Null(exp);
    }

    [Fact]
    public void TOK02_Blob_trop_court_retourne_null()
    {
        // Moins de 3 + 12 + 16 = 31 octets → découpe impossible → null.
        var court = Convert.ToBase64String(new byte[10]);
        var token = ClaudeTokenReader.DecryptAndSelectToken(V10TestVault.TestKey, court, out var exp);
        Assert.Null(token);
        Assert.Null(exp);
    }

    [Fact]
    public void TOK02_Tag_GCM_falsifie_retourne_null()
    {
        var json = MapJson(
            "acc:org:base:claude_code", "X",
            "acc:org:base2:claude_code", """{ "token": "T" }""");
        var b64 = V10TestVault.MakeTokenCacheB64(json);
        var blob = Convert.FromBase64String(b64);
        blob[^1] ^= 0xFF; // flippe un octet du tag → CryptographicException interne → null.
        var altere = Convert.ToBase64String(blob);

        var token = ClaudeTokenReader.DecryptAndSelectToken(V10TestVault.TestKey, altere, out var exp);

        Assert.Null(token);
        Assert.Null(exp);
    }

    [Fact]
    public void TOK02_Mauvaise_cle_retourne_null()
    {
        var json = MapJson(
            "acc:org:base:claude_code", "X",
            "acc:org:base2:claude_code", """{ "token": "T" }""");
        var b64 = V10TestVault.MakeTokenCacheB64(json); // chiffré avec TestKey
        var mauvaiseCle = new byte[32]; // que des zéros → déchiffrement GCM échoue.

        var token = ClaudeTokenReader.DecryptAndSelectToken(mauvaiseCle, b64, out var exp);

        Assert.Null(token);
        Assert.Null(exp);
    }

    [Fact]
    public void TOK02_Map_sans_claude_code_retourne_null()
    {
        var json = MapJson(
            "acc:org:https://api.anthropic.com:profile", "A",
            "acc:org:https://api.anthropic.com:autre", """{ "token": "B" }""");
        var b64 = V10TestVault.MakeTokenCacheB64(json);

        var token = ClaudeTokenReader.DecryptAndSelectToken(V10TestVault.TestKey, b64, out var exp);

        Assert.Null(token);
        Assert.Null(exp);
    }

    [Fact]
    public void TOK02_Entree_claude_code_sans_token_retourne_null()
    {
        var json = """
        {
          "acc:org:base:claude_code": { "refreshToken": "R", "expiresAt": "2027-01-01T00:00:00+00:00" }
        }
        """;
        var b64 = V10TestVault.MakeTokenCacheB64(json);

        var token = ClaudeTokenReader.DecryptAndSelectToken(V10TestVault.TestKey, b64, out var exp);

        Assert.Null(token);
        Assert.Null(exp);
    }

    [Fact]
    public void TOK02_Plaintext_non_json_retourne_null()
    {
        // Blob v10 valide mais plaintext = texte brut (pas un JSON) → JsonException interne → null.
        var b64 = V10TestVault.MakeTokenCacheB64("ceci n'est pas du json");
        var token = ClaudeTokenReader.DecryptAndSelectToken(V10TestVault.TestKey, b64, out var exp);
        Assert.Null(token);
        Assert.Null(exp);
    }

    // --- TOK-02 (fichiers) : chemins inexistants → null, aucune exception ---

    [Fact]
    public void TOK02_Fichiers_absents_retourne_null_sans_exception()
    {
        var reader = new ClaudeTokenReader(
            configJsonPath: Path.Combine(Path.GetTempPath(), "chronos-inexistant-config.json"),
            localStatePath: Path.Combine(Path.GetTempPath(), "chronos-inexistant-localstate"));

        var token = reader.TryReadAccessToken(out var exp);

        Assert.Null(token);
        Assert.Null(exp);
    }

    // --- TOK-03 : preuve de non-écriture (snapshot répertoire avant/après) ---

    [Fact]
    public void TOK03_TryReadAccessToken_n_ecrit_aucun_fichier()
    {
        var dir = Path.Combine(Path.GetTempPath(), "chronos-tok03-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var configPath = Path.Combine(dir, "config.json");
            var localStatePath = Path.Combine(dir, "Local State");
            // Fichiers coffre FACTICES (contenu quelconque : le déchiffrement échouera → null, mais
            // aucune écriture ne doit avoir lieu). AUCUN secret réel.
            File.WriteAllText(configPath, """{ "oauth:tokenCache": "invalide" }""");
            File.WriteAllText(localStatePath, """{ "os_crypt": { "encrypted_key": "invalide" } }""");

            var avant = SnapshotRepertoire(dir);

            var reader = new ClaudeTokenReader(configPath, localStatePath);
            var token = reader.TryReadAccessToken(out var exp);

            var apres = SnapshotRepertoire(dir);

            Assert.Null(token);           // déchiffrement impossible → null
            Assert.Null(exp);
            Assert.Equal(avant, apres);   // AUCUN fichier créé/modifié (liste + timestamps identiques)
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // Instantané déterministe : chemin + taille + timestamp d'écriture de chaque fichier du répertoire.
    private static string SnapshotRepertoire(string dir)
    {
        var entries = Directory.GetFileSystemEntries(dir, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => $"{p}|{new FileInfo(p).Length}|{File.GetLastWriteTimeUtc(p):O}");
        return string.Join("\n", entries);
    }
}
