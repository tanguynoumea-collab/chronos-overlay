using System.IO;
using System.Net;
using System.Net.Http;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve le portillon INT-03 : OAuthUsageEnabled==false → GetAsync retourne UsageSnapshot.Empty
/// SANS lire le token (reader.ReadCount==0) ni appeler l'endpoint (handler.SendCount==0) — le coffre
/// n'est JAMAIS ouvert. OAuthUsageEnabled==true → délègue au provider interne EXACT. Le flag est relu
/// FRAIS depuis settings.json à chaque appel (bascule sans reconstruire le provider).
///
/// Tests PURS : SettingsService sur un ChronosPaths temp, ClaudeOAuthUsageProvider réel enveloppant
/// un FakeClaudeTokenReader + FakeHttpMessageHandler + FakeClock → aucun coffre ni réseau réel.
/// </summary>
public sealed class GatedOAuthUsageProviderTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 07, 09, 08, 00, 00, TimeSpan.Zero);

    // Corps OAuth minimal : resets_at futurs par rapport à Now (fractions de temps positives).
    private const string NominalBody = """
        {
          "five_hour": { "utilization": 65, "resets_at": "2026-07-09T09:39:59+00:00" },
          "seven_day": { "utilization": 92, "resets_at": "2026-07-10T21:59:59+00:00" }
        }
        """;

    private readonly string _dir;
    private readonly SettingsService _settings;
    private readonly FakeClaudeTokenReader _reader;
    private readonly FakeHttpMessageHandler _handler;
    private readonly GatedOAuthUsageProvider _gated;

    public GatedOAuthUsageProviderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "chronos-gated-tests", Path.GetRandomFileName());
        var usage = Path.Combine(_dir, "Chronos", "usage.json");
        var projects = Path.Combine(_dir, "projects");
        _settings = new SettingsService(new ChronosPaths(usage, projects));

        _reader = new FakeClaudeTokenReader { Token = "secret", Expires = null };
        _handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, NominalBody);
        var inner = new ClaudeOAuthUsageProvider(_reader, new HttpClient(_handler), new FakeClock(Now));
        _gated = new GatedOAuthUsageProvider(inner, _settings);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* nettoyage best-effort */ }
    }

    [Fact]
    public async Task Desactive_retourne_Empty_sans_lire_le_token_ni_appeler_le_reseau()
    {
        _settings.Save(new ChronosSettings { OAuthUsageEnabled = false });

        var snap = await _gated.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
        Assert.Equal(0, _reader.ReadCount);   // PREUVE sécurité : coffre jamais ouvert
        Assert.Equal(0, _handler.SendCount);  // PREUVE : aucun appel endpoint
    }

    [Fact]
    public async Task Active_delegue_au_provider_interne_exact()
    {
        _settings.Save(new ChronosSettings { OAuthUsageEnabled = true });

        var snap = await _gated.GetAsync();

        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.True(_reader.ReadCount >= 1);
        Assert.True(_handler.SendCount >= 1);
    }

    [Fact]
    public async Task Flag_relu_frais_entre_deux_appels_change_le_comportement()
    {
        // Désactivé d'abord → Empty, zéro accès.
        _settings.Save(new ChronosSettings { OAuthUsageEnabled = false });
        var off = await _gated.GetAsync();
        Assert.Equal(SourceReliability.Unavailable, off.FiveHour.Reliability);
        Assert.Equal(0, _reader.ReadCount);

        // Bascule à true SANS reconstruire le provider → l'appel suivant délègue (Exact).
        _settings.Save(new ChronosSettings { OAuthUsageEnabled = true });
        var on = await _gated.GetAsync();
        Assert.Equal(SourceReliability.Exact, on.FiveHour.Reliability);
        Assert.True(_reader.ReadCount >= 1);
    }
}
