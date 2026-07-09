using System.Text.Json;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve le cœur PUR du pont statusLine (<see cref="StatusLineBridge.BuildUsageJson"/>) : extraction
/// de rate_limits.{five_hour,seven_day}.{used_percentage,resets_at} au schéma usage.json attendu par
/// <see cref="ClaudeUsageObjectProvider"/>, omission des fenêtres absentes, tolérance totale au stdin
/// invalide, et NON-COPIE de tout autre champ (sécurité : le contenu de session ne fuite pas).
/// </summary>
public class StatusLineBridgeTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Extrait_les_deux_fenetres_et_capturedAt()
    {
        var stdin = """
        { "model": {"id":"x"}, "rate_limits": {
            "five_hour": { "used_percentage": 23.5, "resets_at": 1738425600 },
            "seven_day": { "used_percentage": 41,   "resets_at": 1738857600 } } }
        """;

        var root = Parse(StatusLineBridge.BuildUsageJson(stdin, 1700000000000));

        Assert.Equal(23.5, root.GetProperty("five_hour").GetProperty("used_percentage").GetDouble());
        Assert.Equal(1738425600, root.GetProperty("five_hour").GetProperty("resets_at").GetInt64());
        Assert.Equal(41, root.GetProperty("seven_day").GetProperty("used_percentage").GetDouble());
        Assert.Equal(1738857600, root.GetProperty("seven_day").GetProperty("resets_at").GetInt64());
        Assert.Equal(1700000000000, root.GetProperty("capturedAt").GetInt64());
    }

    [Fact]
    public void Fenetre_absente_est_omise()
    {
        var stdin = """{ "rate_limits": { "five_hour": { "used_percentage": 10, "resets_at": 1 } } }""";
        var root = Parse(StatusLineBridge.BuildUsageJson(stdin, 0));

        Assert.True(root.TryGetProperty("five_hour", out _));
        Assert.False(root.TryGetProperty("seven_day", out _)); // hebdo absente → omise
    }

    [Fact]
    public void Sans_rate_limits_seul_capturedAt()
    {
        var root = Parse(StatusLineBridge.BuildUsageJson("""{ "cost": {"total_cost_usd": 0.42} }""", 123));
        Assert.False(root.TryGetProperty("five_hour", out _));
        Assert.False(root.TryGetProperty("seven_day", out _));
        Assert.Equal(123, root.GetProperty("capturedAt").GetInt64());
    }

    [Fact]
    public void Stdin_invalide_ne_leve_pas_et_ecrit_capturedAt()
    {
        var root = Parse(StatusLineBridge.BuildUsageJson("ceci n'est pas du json", 7));
        Assert.Equal(7, root.GetProperty("capturedAt").GetInt64());
        Assert.False(root.TryGetProperty("five_hour", out _));
    }

    [Fact]
    public void Ne_copie_que_les_deux_champs_numeriques_pas_le_reste_du_stdin()
    {
        // La fenêtre porte un champ « secret » : il ne doit JAMAIS se retrouver dans usage.json.
        var stdin = """
        { "rate_limits": { "five_hour": {
            "used_percentage": 12, "resets_at": 99, "secret_transcript": "NE PAS FUITER" } } }
        """;
        var text = StatusLineBridge.BuildUsageJson(stdin, 0);

        Assert.DoesNotContain("secret_transcript", text);
        Assert.DoesNotContain("NE PAS FUITER", text);
        var five = Parse(text).GetProperty("five_hour");
        Assert.Equal(2, five.EnumerateObject().Count()); // exactement used_percentage + resets_at
    }
}
