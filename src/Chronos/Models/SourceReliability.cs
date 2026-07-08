namespace Chronos.Models;

/// <summary>Provenance d'une donnée d'usage : exacte (source primaire), estimée (repli JSONL) ou indisponible.</summary>
public enum SourceReliability { Exact, Estimated, Unavailable }
