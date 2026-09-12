namespace Chronos.Services;

/// <summary>SQUELETTE (étape RED) — implémenté dans le commit suivant.</summary>
public sealed class SourceActiviteMemoisee : ITranscriptActivitySource
{
    public static readonly TimeSpan ValiditeParDefaut = TimeSpan.FromSeconds(60);

    public SourceActiviteMemoisee(ITranscriptActivitySource inner, IClock horloge, TimeSpan? validite = null)
    {
        _ = inner; _ = horloge; _ = validite;
    }

    public Task<TranscriptActivityLog> ReadAsync(CancellationToken ct = default)
        => throw new NotImplementedException();
}
